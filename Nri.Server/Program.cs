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
        using (var shellCts = new CancellationTokenSource())
        {
            var startedUtc = DateTime.UtcNow;
            var shell = new ServerConsoleShell(bootstrap, startedUtc, waitHandle.Set);

            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                waitHandle.Set();
            };
            AppDomain.CurrentDomain.ProcessExit += (_, __) => waitHandle.Set();

            bootstrap.Start();
            shell.PrintStartupSummary();
            var shellTask = shell.RunAsync(shellCts.Token);

            waitHandle.Wait();
            shellCts.Cancel();
            bootstrap.Stop();

            try
            {
                shellTask.Wait(200);
            }
            catch
            {
            }
        }
    }
}
