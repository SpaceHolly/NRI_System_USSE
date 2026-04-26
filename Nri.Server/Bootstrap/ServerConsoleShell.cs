using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nri.Server.FateEngine;

namespace Nri.Server.Bootstrap;

public sealed class ServerConsoleShell
{
    private readonly ServerBootstrap _bootstrap;
    private readonly DateTime _startedUtc;
    private readonly Action _requestStop;

    public ServerConsoleShell(ServerBootstrap bootstrap, DateTime startedUtc, Action requestStop)
    {
        _bootstrap = bootstrap;
        _startedUtc = startedUtc;
        _requestStop = requestStop;
    }

    public Task RunAsync(CancellationToken token)
    {
        return Task.Run(() => CommandLoop(token), CancellationToken.None);
    }

    public void PrintStartupSummary()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        Console.WriteLine("NRI Server started");
        Console.WriteLine($"Version: {version}");
        Console.WriteLine($"TCP: listening on {_bootstrap.ListeningEndpoint}");
        Console.WriteLine("MongoDB: connected");
        Console.WriteLine("Commands: help, status, sessions, fate-test, clear, stop");
        Console.Write("> ");
    }

    private void CommandLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = Console.ReadLine();
            }
            catch
            {
                return;
            }

            if (line == null)
            {
                return;
            }

            try
            {
                Execute(line);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command error: {ex.Message}");
            }
            if (token.IsCancellationRequested)
            {
                return;
            }

            Console.Write("> ");
        }
    }

    private void Execute(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return;
        }

        var command = parts[0].ToLowerInvariant();
        switch (command)
        {
            case "help":
                PrintHelp();
                break;
            case "status":
                PrintStatus();
                break;
            case "sessions":
                PrintSessions();
                break;
            case "fate-test":
                RunFateTest(parts.Skip(1).ToArray());
                break;
            case "clear":
                Console.Clear();
                break;
            case "stop":
                Console.WriteLine("Stopping server...");
                _requestStop();
                break;
            default:
                Console.WriteLine("Unknown command. Type 'help'.");
                break;
        }
    }

    private void PrintHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  help                      Show command list.");
        Console.WriteLine("  status                    Show server diagnostics summary.");
        Console.WriteLine("  sessions                  Show active sessions diagnostics.");
        Console.WriteLine("  fate-test d100 67 1..5    Run Fate Engine test with optional layer modifiers.");
        Console.WriteLine("  clear                     Clear console.");
        Console.WriteLine("  stop                      Gracefully stop server.");
    }

    private void PrintStatus()
    {
        var now = DateTime.UtcNow;
        var uptime = now - _startedUtc;
        Console.WriteLine("Server status");
        Console.WriteLine($"TCP: {(_bootstrap.IsTcpRunning ? "listening" : "stopped")} ({_bootstrap.ListeningEndpoint})");
        Console.WriteLine("MongoDB: connected");
        Console.WriteLine($"Uptime: {uptime:dd\\.hh\\:mm\\:ss}");
        Console.WriteLine($"UTC now: {now:O}");
        Console.WriteLine($"Online connections: {_bootstrap.OnlineConnections}");
        Console.WriteLine($"Active sessions: {_bootstrap.Runtime.Sessions.GetActiveSessionCount()}");
    }

    private void PrintSessions()
    {
        var sessions = _bootstrap.Runtime.Sessions.GetActiveSessionsSnapshot();
        if (sessions.Count == 0)
        {
            Console.WriteLine("Active sessions: 0");
            return;
        }

        Console.WriteLine($"Active sessions: {sessions.Count}");
        foreach (var session in sessions)
        {
            Console.WriteLine($"- user={session.UserId} conn={session.ConnectionId} created={session.CreatedUtc:O} expires={session.ExpiresUtc:O}");
        }
    }

    private static void RunFateTest(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            Console.WriteLine("Usage: fate-test d100 67 [m1 m2 m3 m4 m5]");
            return;
        }

        if (!TryParseDieSides(args[0], out var dieSides))
        {
            Console.WriteLine("Invalid die value. Use d100 or 100 format.");
            return;
        }

        if (!int.TryParse(args[1], out var baseRoll))
        {
            Console.WriteLine("Invalid baseRoll value.");
            return;
        }

        if (args.Count - 2 > FateEngineSettings.LayerCount)
        {
            Console.WriteLine($"Too many modifiers. Maximum is {FateEngineSettings.LayerCount}.");
            return;
        }

        var settings = FateEngineSettings.CreateDefault();
        var modifierCount = Math.Max(args.Count - 2, 0);
        for (var i = 0; i < modifierCount; i++)
        {
            if (!int.TryParse(args[i + 2], out var modifier))
            {
                Console.WriteLine($"Invalid modifier at position {i + 1}.");
                return;
            }

            settings.Layers[i].FlatModifier = modifier;
        }

        settings.Normalize();
        var request = new FateEngineRequest
        {
            BaseRoll = baseRoll,
            DieSides = dieSides,
            RollType = "console-test",
            SceneId = "default"
        };

        var result = new FateEnginePipeline().Process(request, settings);
        Console.WriteLine("Fate Engine test");
        Console.WriteLine($"BaseRoll: {result.BaseRoll}");
        Console.WriteLine($"Die: d{result.DieSides}");
        Console.WriteLine($"Applied: {result.Applied}");
        Console.WriteLine($"FateValue: {result.FateValue}");
        if (!string.IsNullOrWhiteSpace(result.SkippedReason))
        {
            Console.WriteLine($"SkippedReason: {result.SkippedReason}");
        }

        foreach (var layer in result.Layers.OrderBy(x => x.LayerNumber))
        {
            var state = layer.Applied ? "applied" : "skipped";
            Console.WriteLine($"Layer {layer.LayerNumber}: {state} modifier={layer.Modifier} input={layer.InputValue} output={layer.OutputValue} reason={layer.Reason}");
        }
    }

    private static bool TryParseDieSides(string raw, out int dieSides)
    {
        var normalized = raw.StartsWith("d", StringComparison.OrdinalIgnoreCase)
            ? raw.Substring(1)
            : raw;

        return int.TryParse(normalized, out dieSides) && dieSides > 0;
    }
}
