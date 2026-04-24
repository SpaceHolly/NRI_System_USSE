using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Nri.AdminClient.Diagnostics;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.Networking;

public class ClientSessionState
{
    public string? AuthToken { get; set; }
}

public interface IJsonTcpClient : IDisposable
{
    string ServerHost { get; }
    int ServerPort { get; }
    void UpdateEndpoint(string host, int port);
    void Connect();
    void Disconnect();
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
        _config.ServerHost = host;
        _config.ServerPort = port;
        ClientLogService.Instance.Info($"Endpoint updated: {host}:{port}");
        Disconnect();
    }

    public void Connect()
    {
        if (_tcpClient is { Connected: true } && _reader != null && _writer != null)
        {
            return;
        }

        ClientLogService.Instance.Info($"Connecting to server: {ServerHost}:{ServerPort}");

        Disconnect();
        var connectingClient = new TcpClient();
        var connectTask = connectingClient.ConnectAsync(ServerHost, ServerPort);
        if (!connectTask.Wait(TimeSpan.FromSeconds(5)))
        {
            ObserveFaultedTask(connectTask);
            connectingClient.Dispose();
            var timeout = new TimeoutException($"Timed out connecting to {ServerHost}:{ServerPort}.");
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
        ClientLogService.Instance.Info($"Connected to server: {ServerHost}:{ServerPort}");
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

    public ResponseEnvelope Send(RequestEnvelope request)
    {
        if (_tcpClient is not { Connected: true } || _reader == null || _writer == null)
        {
            Connect();
        }

        try
        {
            request.AuthToken = request.AuthToken ?? _session.AuthToken;
            var json = JsonProtocolSerializer.Serialize(request);
            _writer!.WriteLine(json);

            var responseJson = _reader!.ReadLine();
            var response = JsonProtocolSerializer.Deserialize<ResponseEnvelope>(responseJson ?? string.Empty)
                           ?? new ResponseEnvelope { Status = ResponseStatus.Error, ErrorCode = ErrorCode.InvalidRequest, Message = "Empty response." };

            if (response.Payload.ContainsKey("authToken"))
            {
                _session.AuthToken = Convert.ToString(response.Payload["authToken"]);
            }

            return response;
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error($"Network send failed for command={request.Command}", ex);
            throw;
        }
    }

    public void Dispose()
    {
        Disconnect();
    }

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
