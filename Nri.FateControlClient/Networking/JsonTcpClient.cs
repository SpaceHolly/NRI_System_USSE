using System;
using System.IO;
using System.Net.Sockets;
using Nri.Shared.Contracts;

namespace Nri.FateControlClient.Networking;

public sealed class JsonTcpClient : IDisposable
{
    private TcpClient? _tcpClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public string Host { get; private set; } = "127.0.0.1";
    public int Port { get; private set; } = 4600;

    public bool IsConnected => _tcpClient != null && _tcpClient.Connected;

    public void SetEndpoint(string host, int port)
    {
        Host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
        Port = port;
        Disconnect();
    }

    public void Connect()
    {
        if (IsConnected)
        {
            return;
        }

        Disconnect();
        var tcpClient = new TcpClient();
        tcpClient.Connect(Host, Port);
        var stream = tcpClient.GetStream();
        stream.ReadTimeout = 7000;
        stream.WriteTimeout = 7000;

        _tcpClient = tcpClient;
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    public ResponseEnvelope Send(RequestEnvelope request)
    {
        if (!IsConnected)
        {
            Connect();
        }

        var payload = JsonProtocolSerializer.Serialize(request);
        _writer!.WriteLine(payload);

        var responseJson = _reader!.ReadLine();
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return new ResponseEnvelope
            {
                Status = ResponseStatus.Error,
                ErrorCode = ErrorCode.InvalidRequest,
                Message = "Empty response from server."
            };
        }

        return JsonProtocolSerializer.Deserialize<ResponseEnvelope>(responseJson)
               ?? new ResponseEnvelope
               {
                   Status = ResponseStatus.Error,
                   ErrorCode = ErrorCode.InvalidRequest,
                   Message = "Invalid response JSON."
               };
    }

    public void Disconnect()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _tcpClient?.Dispose();

        _reader = null;
        _writer = null;
        _tcpClient = null;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
