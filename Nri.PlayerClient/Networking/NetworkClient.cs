using System;
using System.IO;
using System.Net.Sockets;
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
    }

    public void Connect()
    {
        if (_tcpClient != null && _tcpClient.Connected)
            return;

        Disconnect();
        _tcpClient = new TcpClient();
        var connectTask = _tcpClient.ConnectAsync(_config.ServerHost, _config.ServerPort);
        if (!connectTask.Wait(TimeSpan.FromSeconds(5)))
        {
            Disconnect();
            throw new TimeoutException("Connection timeout.");
        }

        var stream = _tcpClient.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    public ResponseEnvelope Send(RequestEnvelope request)
    {
        if (_tcpClient == null || !_tcpClient.Connected || _reader == null || _writer == null)
            Connect();

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

    public void Disconnect()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _tcpClient?.Close();
        _reader = null;
        _writer = null;
        _tcpClient = null;
    }

    public void Dispose() => Disconnect();
}
