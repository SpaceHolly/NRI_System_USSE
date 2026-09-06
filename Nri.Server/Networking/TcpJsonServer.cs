using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Nri.Server.Application;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;
using Nri.Shared.Diagnostics;

namespace Nri.Server.Networking;

public class ClientConnection
{
    public string ConnectionId { get; set; } = Guid.NewGuid().ToString("N");
    public TcpClient Client { get; set; } = null!;
    public DateTime ConnectedUtc { get; set; } = DateTime.UtcNow;
}

public class ConnectionManager
{
    private readonly ConcurrentDictionary<string, ClientConnection> _connections = new ConcurrentDictionary<string, ClientConnection>();

    public void Add(ClientConnection connection)
    {
        _connections[connection.ConnectionId] = connection;
    }

    public void Remove(string connectionId)
    {
        ClientConnection _;
        _connections.TryRemove(connectionId, out _);
    }

    public int Count => _connections.Count;
}

public class TcpJsonServer
{
    private readonly ServerConfig _config;
    private readonly IServerLogger _logger;
    private readonly CommandDispatcher _dispatcher;
    private readonly SessionManager _sessionManager;
    private readonly ConnectionManager _connections = new ConnectionManager();
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private TcpListener? _listener;
    private volatile bool _isRunning;

    public TcpJsonServer(ServerConfig config, IServerLogger logger, CommandDispatcher dispatcher, SessionManager sessionManager)
    {
        _config = config;
        _logger = logger;
        _dispatcher = dispatcher;
        _sessionManager = sessionManager;
    }


    public bool IsRunning => _isRunning;
    public int OnlineConnections => _connections.Count;
    public string ListeningEndpoint => $"{_config.Host}:{_config.Port}";

    public void Start()
    {
        _listener = new TcpListener(IPAddress.Parse(_config.Host), _config.Port);
        _listener.Start();
        _isRunning = true;
        _logger.Debug($"TCP listener started at {_config.Host}:{_config.Port}");
        new Thread(AcceptLoop) { IsBackground = true }.Start();
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener?.Stop();
        _isRunning = false;
        _logger.Debug("TCP listener stopped.");
    }

    private void AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (_listener == null)
                {
                    return;
                }

                var client = _listener.AcceptTcpClient();
                var connection = new ClientConnection { Client = client };
                _connections.Add(connection);
                _logger.Session($"Connected {connection.ConnectionId}, online={_connections.Count}");
                new Thread(() => HandleClient(connection)) { IsBackground = true }.Start();
            }
            catch (SocketException ex)
            {
                if (_cts.IsCancellationRequested)
                {
                    return;
                }

                _logger.Debug($"Accept loop socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.Debug($"Accept loop error: {ex}");
            }
        }
    }

    private void HandleClient(ClientConnection connection)
    {
        try
        {
            using (connection.Client)
            using (var stream = connection.Client.GetStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream) { AutoFlush = true })
            {
                while (!_cts.IsCancellationRequested && connection.Client.Connected)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    var request = JsonProtocolSerializer.Deserialize<RequestEnvelope>(line);
                    if (request == null)
                    {
                        var invalid = new ResponseEnvelope
                        {
                            Status = ResponseStatus.ValidationFailed,
                            ErrorCode = ErrorCode.InvalidRequest,
                            Message = "Invalid JSON request."
                        };
                        writer.WriteLine(JsonProtocolSerializer.Serialize(invalid));
                        continue;
                    }

                    PerformanceTelemetry0214.Current.ObserveClient(connection.ConnectionId, request.ClientDiagnostics);
                    var stopwatch = Stopwatch.StartNew();
                    var response = _dispatcher.Dispatch(connection.ConnectionId, request);
                    var responseJson = JsonProtocolSerializer.Serialize(response);
                    writer.WriteLine(responseJson);
                    PerformanceTelemetry0214.Current.Record(new PerformanceSample0214
                    {
                        Source = "Nri.Server",
                        Category = "server_transport",
                        Command = request.Command,
                        Status = response.Status.ToString(),
                        Outcome = response.Status == ResponseStatus.Ok ? "completed" : "failed",
                        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                        RequestBytes = Encoding.UTF8.GetByteCount(line),
                        ResponseBytes = Encoding.UTF8.GetByteCount(responseJson),
                        ConnectionGeneration = request.ConnectionGeneration
                    });
                }
            }
        }
        catch (IOException ex)
        {
            _logger.Debug($"Connection {connection.ConnectionId} IO error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Debug($"Connection {connection.ConnectionId} error: {ex}");
        }
        finally
        {
            _sessionManager.DisconnectByConnection(connection.ConnectionId);
            PerformanceTelemetry0214.Current.RemoveClient(connection.ConnectionId);
            _connections.Remove(connection.ConnectionId);
            _logger.Session($"Disconnected {connection.ConnectionId}, online={_connections.Count}");
        }
    }
}
