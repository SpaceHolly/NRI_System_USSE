using System;
using Nri.Server.Application;
using Nri.Server.Configuration;
using Nri.Server.Logging;
using Nri.Server.Networking;

namespace Nri.Server;

internal static class Program
{
    private static void Main(string[] args)
    {
        var config = ConfigLoader.Load("server.config.json");
        var logger = new CompositeLogger(config.Logging);
        var runtime = ServiceRegistry.Build(config, logger);
        var listener = new TcpJsonServer(config, logger, runtime.Dispatcher, runtime.Sessions);

        logger.Debug("Server bootstrap completed.");
        listener.Start();

        Console.WriteLine("NRI Server started. Press Enter to stop.");
        Console.ReadLine();

        listener.Stop();
        logger.Debug("Server shutdown completed.");
    }
}
