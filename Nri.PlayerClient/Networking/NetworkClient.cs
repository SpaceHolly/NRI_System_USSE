using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Nri.PlayerClient.Diagnostics;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.Networking;

public class ClientSessionState
{
    public string? AuthToken { get; set; }
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
    private TcpClient? _tcpClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public JsonTcpClient(ClientConfig config, ClientSessionState session)
    {
        _config = config;
        _session = session;
    }

    public string ServerHost => _config.ServerHost;
    public int ServerPort => _config.ServerPort;

    public void UpdateEndpoint(string host, int port)
    {
        var normalizedHost = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
        if (string.Equals(_config.ServerHost, normalizedHost, StringComparison.OrdinalIgnoreCase) && _config.ServerPort == port)
            return;

        Disconnect();
        _config.ServerHost = normalizedHost;
        _config.ServerPort = port;
        ClientLogService.Instance.Info($"Endpoint updated: {normalizedHost}:{port}");
    }

    public void Connect()
    {
        if (_tcpClient != null && _tcpClient.Connected)
            return;

        ClientLogService.Instance.Info($"Connecting to server: {_config.ServerHost}:{_config.ServerPort}");
        Disconnect();
        var connectingClient = new TcpClient();
        var connectTask = connectingClient.ConnectAsync(_config.ServerHost, _config.ServerPort);
        if (!connectTask.Wait(TimeSpan.FromSeconds(5)))
        {
            ObserveFaultedTask(connectTask);
            connectingClient.Dispose();
            var timeout = new TimeoutException("Connection timeout.");
            ClientLogService.Instance.Error("Network connection timeout", timeout);
            throw timeout;
        }

        connectTask.GetAwaiter().GetResult();

        _tcpClient = connectingClient;
        var stream = connectingClient.GetStream();
        stream.ReadTimeout = 5000;
        stream.WriteTimeout = 5000;
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
        ClientLogService.Instance.Info($"Connected to server: {_config.ServerHost}:{_config.ServerPort}");
    }

    public ResponseEnvelope Send(RequestEnvelope request)
    {
        if (_tcpClient == null || !_tcpClient.Connected || _reader == null || _writer == null)
            Connect();

        try
        {
            request.AuthToken = request.AuthToken ?? _session.AuthToken;
            var json = JsonProtocolSerializer.Serialize(request);
            _writer!.WriteLine(json);

            var responseJson = _reader!.ReadLine();
            var response = JsonProtocolSerializer.Deserialize<ResponseEnvelope>(responseJson ?? string.Empty)
                           ?? new ResponseEnvelope { Status = ResponseStatus.Error, ErrorCode = ErrorCode.InvalidRequest, Message = "Empty response." };

            if (response.Payload.ContainsKey("authToken"))
                _session.AuthToken = Convert.ToString(response.Payload["authToken"]);

            return response;
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error($"Network send failed for command={request.Command}", ex);
            throw;
        }
    }

    public void Disconnect()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _tcpClient?.Close();
        _reader = null;
        _writer = null;
        _tcpClient = null;
        ClientLogService.Instance.Info("Disconnected from server");
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
}
