using System;
using System.Threading;
using Nri.Server.Bootstrap;

namespace Nri.Server;

internal static class Program
{
    private static void Main(string[] args)
    {
        using (var waitHandle = new ManualResetEventSlim(false))
        using (var bootstrap = ServerBootstrap.Initialize("server.config.json"))
        {
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                waitHandle.Set();
            };
            AppDomain.CurrentDomain.ProcessExit += (_, __) => waitHandle.Set();

            bootstrap.Start();
            Console.WriteLine("NRI Server started. Press Ctrl+C to stop.");
            waitHandle.Wait();
            bootstrap.Stop();
        }
    }
}
