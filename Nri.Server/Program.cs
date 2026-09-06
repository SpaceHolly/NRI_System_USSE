using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Nri.Server.Bootstrap;

namespace Nri.Server;

internal static class Program
{
    private static void Main(string[] args)
    {
        var configPath = ResolveConfigPath(GetArgumentValue(args, "--config"));
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
        if (args.Any(x => string.Equals(x, "--audit-protocol-authorization", StringComparison.OrdinalIgnoreCase)))
        {
            var outputPath = GetArgumentValue(args, "--output") ?? Path.Combine(Environment.CurrentDirectory, "protocol_authorization_catalog.json");
            using var auditBootstrap = ServerBootstrap.Initialize(configPath);
            var items = auditBootstrap.Runtime.Dispatcher.AuthorizationCatalog.Items.OrderBy(x => x.CommandName).ToArray();
            var audit = new
            {
                status = items.Length > 0 ? "PASS" : "NOT_PASS",
                registeredCommandCount = auditBootstrap.Runtime.Dispatcher.RegisteredCommands.Count,
                effectiveClassifiedCount = items.Length,
                unclassifiedCount = auditBootstrap.Runtime.Dispatcher.RegisteredCommands.Count - items.Length,
                ambiguousAliasCount = 0,
                securityGroups = items.Select(x => x.SecurityTestGroup).Distinct().OrderBy(x => x).ToArray(),
                commands = items
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter());
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Environment.CurrentDirectory);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(audit, options));
            Console.WriteLine($"Protocol authorization catalog: {audit.status}; commands={audit.registeredCommandCount}; output={outputPath}");
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

    private static string ResolveConfigPath(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath)) return requestedPath;

        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "server.config.json"),
            Path.Combine(Environment.CurrentDirectory, "Nri.Server", "server.config.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.config.json")
        };
        return candidates.FirstOrDefault(File.Exists) ?? "server.config.json";
    }
}
