using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Nri.ChatClient.Diagnostics;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Diagnostics;

namespace Nri.ChatClient.Networking;

public class ClientSessionState
{
    public string? AuthToken { get; set; }

    public event Action? AuthenticationInvalidated;

    public void InvalidateAuthentication()
    {
        AuthToken = null;
        AuthenticationInvalidated?.Invoke();
    }
}

public interface IJsonTcpClient : IDisposable
{
    string ServerHost { get; }
    int ServerPort { get; }
    void Connect();
    void Disconnect();
    void UpdateEndpoint(string host, int port);
    ResponseEnvelope Send(RequestEnvelope request);
}

public class JsonTcpClient : IJsonTcpClient
{
    private readonly ClientConfig _config;
    private readonly ClientSessionState _session;
    private readonly object _syncRoot = new();
    private TcpClient? _tcpClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    public ConnectionLifecycleCoordinator Lifecycle { get; } = new();

    public JsonTcpClient(ClientConfig config, ClientSessionState session)
    {
        _config = config;
        _session = session;
    }

    public string ServerHost => _config.ServerHost;
    public int ServerPort => _config.ServerPort;
    public long ConnectionGeneration => Lifecycle.Current.ConnectionGeneration;

    public void UpdateEndpoint(string host, int port)
    {
        lock (_syncRoot)
        {
            var normalizedHost = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
            if (string.Equals(_config.ServerHost, normalizedHost, StringComparison.OrdinalIgnoreCase) && _config.ServerPort == port)
                return;

            DisconnectUnsafe();
            _config.ServerHost = normalizedHost;
            _config.ServerPort = port;
            ClientLogService.Instance.Info($"connect.endpoint.updated value={normalizedHost}:{port}");
        }
    }

    public void Connect()
    {
        lock (_syncRoot)
        {
            ConnectUnsafe();
        }
    }

    private void ConnectUnsafe()
    {
        if (_tcpClient is { Connected: true } && _reader != null && _writer != null)
            return;

        var reconnect = Lifecycle.Current.ConnectionGeneration > 0;
        Lifecycle.BeginConnect(reconnect);
        ClientLogService.Instance.Info($"connect.start endpoint={_config.ServerHost}:{_config.ServerPort}");
        DisconnectUnsafe(false);
        var connectingClient = new TcpClient();
        var connectTask = connectingClient.ConnectAsync(_config.ServerHost, _config.ServerPort);
        if (!connectTask.Wait(TimeSpan.FromSeconds(5)))
        {
            ObserveFaultedTask(connectTask);
            connectingClient.Dispose();
            var timeout = new TimeoutException("Connection timeout.");
            ClientLogService.Instance.Warn($"connect.timeout endpoint={_config.ServerHost}:{_config.ServerPort}");
            throw timeout;
        }

        if (connectTask.IsFaulted)
        {
            connectingClient.Dispose();
            var root = connectTask.Exception?.GetBaseException() ?? new IOException($"Failed to connect to {_config.ServerHost}:{_config.ServerPort}.");
            ClientLogService.Instance.Warn($"connect.failed endpoint={_config.ServerHost}:{_config.ServerPort}; message={root.Message}");
            throw root;
        }

        connectTask.GetAwaiter().GetResult();

        _tcpClient = connectingClient;
        var stream = connectingClient.GetStream();
        stream.ReadTimeout = 5000;
        stream.WriteTimeout = 5000;
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
        Lifecycle.MarkPhysicalConnectionEstablished();
        ClientLogService.Instance.Info($"connect.success endpoint={_config.ServerHost}:{_config.ServerPort}");
    }

    public ResponseEnvelope Send(RequestEnvelope request)
    {
        lock (_syncRoot)
        {
            if (!CommandSafetyClassifier0213.CanSend(Lifecycle.Current, request.Command))
                return BuildRecoveryBlockedResponse(request);

            var stopwatch = Stopwatch.StartNew();
            var requestBytes = 0;
            var responseBytes = 0;
            PerformanceTelemetry0214.Current.IncrementCounter("in_flight_requests");
            try
            {
                if (_tcpClient is not { Connected: true } || _reader == null || _writer == null)
                    ConnectUnsafe();

                if (IsAnonymousAuthCommand(request.Command)) request.AuthToken = null;
                else request.AuthToken = request.AuthToken ?? _session.AuthToken;
                var sentAuthToken = request.AuthToken;
                request.ClientType = "ChatClient";
                request.ConnectionGeneration = ConnectionGeneration;
                request.ClientDiagnostics = PerformanceTelemetry0214.Current.CaptureClientDiagnostics(request.ClientType, request.ConnectionGeneration);
                var json = JsonProtocolSerializer.Serialize(request);
                requestBytes = Encoding.UTF8.GetByteCount(json);
                _writer!.WriteLine(json);

                var responseJson = _reader!.ReadLine();
                responseBytes = Encoding.UTF8.GetByteCount(responseJson ?? string.Empty);
                var response = JsonProtocolSerializer.Deserialize<ResponseEnvelope>(responseJson ?? string.Empty)
                               ?? new ResponseEnvelope { Status = ResponseStatus.Error, ErrorCode = ErrorCode.InvalidRequest, Message = "Empty response." };

                if (response.Payload.ContainsKey("authToken"))
                {
                    _session.AuthToken = Convert.ToString(response.Payload["authToken"]);
                    Lifecycle.MarkAuthenticated();
                }
                else if (response.Status == ResponseStatus.Unauthorized || response.ErrorCode == ErrorCode.InvalidToken)
                {
                    if (IsInvalidAuthenticationResponse(response)
                        && !string.IsNullOrWhiteSpace(sentAuthToken)
                        && string.Equals(_session.AuthToken, sentAuthToken, StringComparison.Ordinal))
                    {
                        _session.InvalidateAuthentication();
                        Lifecycle.MarkSessionExpired(response.Message);
                    }
                    response.Message = "Сеанс входа завершён. Войдите в учётную запись снова.";
                }

                RecordPerformance(request, response.Status.ToString(), "completed", stopwatch.ElapsedMilliseconds, requestBytes, responseBytes);
                return response;
            }
            catch (Exception ex) when (IsTransportException(ex))
            {
                DisconnectUnsafe(false);
                Lifecycle.MarkTransportLost(ex.Message);
                ClientLogService.Instance.Warn($"connect.unavailable command={request.Command}; endpoint={_config.ServerHost}:{_config.ServerPort}; message={ex.Message}");
                RecordPerformance(request, "Error", "transport", stopwatch.ElapsedMilliseconds, requestBytes, responseBytes);
                return BuildTransportErrorResponse(request, $"Сервер недоступен: {_config.ServerHost}:{_config.ServerPort}. Проверьте адрес и запущен ли сервер.");
            }
            finally
            {
                PerformanceTelemetry0214.Current.IncrementCounter("in_flight_requests", -1);
            }
        }
    }

    private void RecordPerformance(RequestEnvelope request, string status, string outcome, long elapsedMs, int requestBytes, int responseBytes)
    {
        PerformanceTelemetry0214.Current.Record(new PerformanceSample0214
        {
            Source = "ChatClient",
            Category = "client_request",
            Command = request.Command,
            Status = status,
            Outcome = outcome,
            ElapsedMilliseconds = elapsedMs,
            RequestBytes = requestBytes,
            ResponseBytes = responseBytes,
            ConnectionGeneration = ConnectionGeneration
        });
    }

    public void Disconnect()
    {
        lock (_syncRoot)
        {
            DisconnectUnsafe();
        }
    }

    private void DisconnectUnsafe(bool publishState = true)
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _tcpClient?.Close();
        _reader = null;
        _writer = null;
        _tcpClient = null;
        if (publishState) Lifecycle.MarkDisconnected();
        ClientLogService.Instance.Info("connect.disconnected");
    }

    public void Dispose() => Disconnect();

    private static void ObserveFaultedTask(Task task)
    {
        if (task.IsFaulted)
        {
            _ = task.Exception;
            return;
        }

        task.ContinueWith(
            continuation => _ = continuation.Exception,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);
    }

    private static bool IsTransportException(Exception ex)
        => ex is IOException
           || ex is SocketException
           || ex is TimeoutException
           || ex.InnerException is SocketException
           || ex.InnerException is IOException
           || ex.InnerException is TimeoutException;

    private static bool IsInvalidAuthenticationResponse(ResponseEnvelope response)
        => response.ErrorCode == ErrorCode.InvalidToken
           || string.Equals(response.Message?.Trim(), "Auth token is invalid.", StringComparison.OrdinalIgnoreCase);

    private static bool IsAnonymousAuthCommand(string? command)
        => string.Equals(command, CommandNames.AuthLogin, StringComparison.OrdinalIgnoreCase)
           || string.Equals(command, CommandNames.AuthRegister, StringComparison.OrdinalIgnoreCase);

    private static ResponseEnvelope BuildTransportErrorResponse(RequestEnvelope request, string message)
        => new ResponseEnvelope
        {
            RequestId = request.RequestId,
            Status = ResponseStatus.Error,
            ErrorCode = ErrorCode.InternalError,
            Message = message
        };

    private static ResponseEnvelope BuildRecoveryBlockedResponse(RequestEnvelope request)
        => new ResponseEnvelope
        {
            RequestId = request.RequestId,
            Status = ResponseStatus.Conflict,
            ErrorCode = ErrorCode.Conflict,
            Message = "Действие временно недоступно: соединение восстанавливается."
        };
}
