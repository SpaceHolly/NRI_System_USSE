using System;
using System.Net;
using System.Net.Sockets;
using Nri.Server.Application;
using Nri.Server.Logging;
using Nri.Shared.Configuration;

namespace Nri.Server.Networking;

public class TcpJsonServer
{
    private readonly ServerConfig _config;
    private readonly IServerLogger _logger;
    private readonly CommandDispatcher _dispatcher;
    private TcpListener? _listener;

    public TcpJsonServer(ServerConfig config, IServerLogger logger, CommandDispatcher dispatcher)
    {
        _config = config;
        _logger = logger;
        _dispatcher = dispatcher;
    }

    public void Start()
    {
        _listener = new TcpListener(IPAddress.Parse(_config.Host), _config.Port);
        _listener.Start();
        _logger.Debug($"TCP listener started at {_config.Host}:{_config.Port}");
    }

    public void Stop()
    {
        _listener?.Stop();
        _logger.Debug("TCP listener stopped.");
    }
}
