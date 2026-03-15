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
    void Connect();
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

    public void Connect()
    {
        if (_tcpClient != null)
        {
            return;
        }

        _tcpClient = new TcpClient();
        _tcpClient.Connect(_config.ServerHost, _config.ServerPort);
        var stream = _tcpClient.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    public string? SessionId { get; set; }
}

public interface IJsonTcpClient
{
    ResponseEnvelope Send(RequestEnvelope request);
}

public class JsonTcpClientStub : IJsonTcpClient
{
    private readonly ClientConfig _config;

    public JsonTcpClientStub(ClientConfig config)
    {
        _config = config;
    }

    public ResponseEnvelope Send(RequestEnvelope request)
    {
        if (_tcpClient == null || _reader == null || _writer == null)
        {
            Connect();
        }

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

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _tcpClient?.Close();
        return new ResponseEnvelope
        {
            RequestId = request.RequestId,
            Status = ResponseStatus.Error,
            ErrorCode = ErrorCode.Unknown,
            Message = $"Player client TCP stub ({_config.ServerHost}:{_config.ServerPort})"
        };
    }
}
