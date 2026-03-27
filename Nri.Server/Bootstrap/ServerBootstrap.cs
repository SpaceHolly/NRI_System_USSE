using System;
using System.Threading;
using Nri.Server.Application;
using Nri.Server.Logging;
using Nri.Server.Networking;
using Nri.Shared.Configuration;

namespace Nri.Server.Bootstrap;

public sealed class ServerBootstrap : IDisposable
{
    private readonly IServerLogger _logger;
    private readonly TcpJsonServer _listener;
    private int _stopped;

    private ServerBootstrap(ServerConfig config, IServerLogger logger, ServerRuntime runtime)
    {
        Config = config;
        _logger = logger;
        Runtime = runtime;
        _listener = new TcpJsonServer(config, logger, runtime.Dispatcher, runtime.Sessions);
    }

    public ServerConfig Config { get; }
    public ServerRuntime Runtime { get; }

    public static ServerBootstrap Initialize(string configPath)
    {
        var config = ServerConfigProvider.Load(configPath);
        var logger = new CompositeLogger(config.Logging);
        try
        {
            var runtime = ServiceRegistry.Build(config, logger);
            logger.Debug($"Server bootstrap completed for {config.Host}:{config.Port}.");
            return new ServerBootstrap(config, logger, runtime);
        }
        catch (Exception ex)
        {
            logger.Debug($"Server bootstrap failed: {ex}");
            throw;
        }
    }

    public void Start()
    {
        _listener.Start();
        _logger.Debug("Server start completed.");
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _listener.Stop();
        _logger.Debug("Server shutdown completed.");
    }

    public void Dispose()
    {
        Stop();
    }
}
