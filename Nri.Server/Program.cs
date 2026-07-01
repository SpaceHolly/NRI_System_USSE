using System;
using System.Linq;
using System.Threading;
using Nri.Server.Bootstrap;

namespace Nri.Server;

internal static class Program
{
    private static void Main(string[] args)
    {
        var configPath = GetArgumentValue(args, "--config") ?? "server.config.json";
        if (args.Any(x => string.Equals(x, "--seed-dev-core", StringComparison.OrdinalIgnoreCase)))
        {
            var result = DevTestCoreSeeder.Run(configPath);
            Console.WriteLine(result);
            return;
        }
        if (args.Any(x => string.Equals(x, "--dev-reset-known-accounts", StringComparison.OrdinalIgnoreCase)))
        {
            var result = DevKnownAccountsSeeder.Run(configPath);
            Console.WriteLine(result);
            return;
        }

        using (var waitHandle = new ManualResetEventSlim(false))
        using (var bootstrap = ServerBootstrap.Initialize(configPath))
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

    private static string? GetArgumentValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
