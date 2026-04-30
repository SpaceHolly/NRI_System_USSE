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
    private static readonly FateEffectCatalog EffectCatalog = new FateEffectCatalog();

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
        Console.WriteLine("Commands: help, status, sessions, fate-test, fate-status, fate-get, fate-enable, fate-disable, fate-layer, fate-reset, fate-effects, clear, stop");
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
        var parts = line
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToArray();
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
            case "fate-status":
                PrintFateStatus();
                break;
            case "fate-get":
                PrintFateSettings();
                break;
            case "fate-enable":
                SetFateEnabled(true);
                break;
            case "fate-disable":
                SetFateEnabled(false);
                break;
            case "fate-layer":
                UpdateFateLayer(parts.Skip(1).ToArray());
                break;
            case "fate-reset":
                ResetFateSettings();
                break;
            case "fate-effects":
                PrintFateEffects(parts.Skip(1).ToArray());
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
        Console.WriteLine("  help                              Show command list.");
        Console.WriteLine("  status                            Show server diagnostics summary.");
        Console.WriteLine("  sessions                          Show active sessions diagnostics.");
        Console.WriteLine("  fate-test d100 67 [m1..m5]        Run Fate Engine test.");
        Console.WriteLine("  fate-status                       Show short Fate Engine status.");
        Console.WriteLine("  fate-get                          Show full Fate Engine settings.");
        Console.WriteLine("  fate-enable / fate-disable        Toggle Fate Engine.");
        Console.WriteLine("  fate-layer 1 on|off|mod 10        Update Fate layer settings.");
        Console.WriteLine("  fate-reset                        Reset Fate settings to default.");
        Console.WriteLine("  fate-effects                      Show all Fate Engine effects.");
        Console.WriteLine("  fate-effects <1-5>                Show effects for one Fate Engine layer.");
        Console.WriteLine("  clear                             Clear console.");
        Console.WriteLine("  stop                              Gracefully stop server.");
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

    private void PrintFateStatus()
    {
        var settings = _bootstrap.Runtime.FateState.GetSnapshot();
        var enabled = settings.Layers.Where(x => x.Enabled).Select(x => x.LayerNumber);
        Console.WriteLine($"Fate enabled: {settings.Enabled}");
        Console.WriteLine($"Layers: {settings.Layers.Count}");
        Console.WriteLine($"Enabled layers: {string.Join(", ", enabled)}");
        Console.WriteLine($"Flat modifiers: {string.Join(", ", settings.Layers.OrderBy(x => x.LayerNumber).Select(x => x.FlatModifier))}");
    }

    private void PrintFateSettings()
    {
        var settings = _bootstrap.Runtime.FateState.GetSnapshot();
        Console.WriteLine($"Fate enabled: {settings.Enabled}");
        foreach (var layer in settings.Layers.OrderBy(x => x.LayerNumber))
        {
            Console.WriteLine($"Layer {layer.LayerNumber}: enabled={layer.Enabled} mod={layer.FlatModifier} mode={layer.Mode} intensity={layer.Intensity}");
        }
    }

    private void SetFateEnabled(bool enabled)
    {
        var settings = _bootstrap.Runtime.FateState.SetEngineEnabled(enabled);
        Console.WriteLine($"Fate engine enabled={settings.Enabled}");
    }

    private void ResetFateSettings()
    {
        var settings = _bootstrap.Runtime.FateState.ResetToDefault();
        Console.WriteLine($"Fate settings reset. enabled={settings.Enabled}, layers={settings.Layers.Count}");
    }

    private void UpdateFateLayer(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            Console.WriteLine("Usage: fate-layer <1..5> on|off|mod <value>");
            return;
        }

        if (!int.TryParse(args[0], out var layerNumber))
        {
            Console.WriteLine("Layer number must be integer.");
            return;
        }

        var action = args[1].ToLowerInvariant();
        if (action == "on")
        {
            _bootstrap.Runtime.FateState.SetLayerEnabled(layerNumber, true);
            Console.WriteLine($"Layer {layerNumber} enabled.");
            return;
        }

        if (action == "off")
        {
            _bootstrap.Runtime.FateState.SetLayerEnabled(layerNumber, false);
            Console.WriteLine($"Layer {layerNumber} disabled.");
            return;
        }

        if (action == "mod")
        {
            if (args.Count < 3 || !int.TryParse(args[2], out var modifier))
            {
                Console.WriteLine("Usage: fate-layer <1..5> mod <integer>");
                return;
            }

            _bootstrap.Runtime.FateState.SetLayerFlatModifier(layerNumber, modifier);
            Console.WriteLine($"Layer {layerNumber} modifier set to {modifier}.");
            return;
        }

        Console.WriteLine("Usage: fate-layer <1..5> on|off|mod <value>");
    }

    private void RunFateTest(IReadOnlyList<string> args)
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

        FateEngineSettings settings;
        if (args.Count == 2)
        {
            settings = _bootstrap.Runtime.FateState.GetSnapshot();
        }
        else
        {
            if (args.Count - 2 > FateEngineSettings.LayerCount)
            {
                Console.WriteLine($"Too many modifiers. Maximum is {FateEngineSettings.LayerCount}.");
                return;
            }

            settings = FateEngineSettings.CreateDefault();
            var modifierCount = args.Count - 2;
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
        }

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
            Console.WriteLine($"Layer {layer.LayerNumber} — {layer.LayerName}");
            Console.WriteLine($"  Effect: {layer.EffectCode} / {layer.EffectDisplayName} [{layer.InfluenceType}/{layer.Strength}]");
            Console.WriteLine($"  Applied: {layer.Applied}");
            Console.WriteLine($"  Input: {layer.InputValue}");
            if (layer.CandidateRolls.Count > 1)
            {
                Console.WriteLine($"  Candidates: {string.Join(", ", layer.CandidateRolls)}");
            }
            Console.WriteLine($"  Selected: {layer.SelectedValue}");
            Console.WriteLine($"  DistributionShift: {layer.DistributionShift}");
            Console.WriteLine($"  AnomalyShift: {layer.AnomalyShift}");
            Console.WriteLine($"  ChaosShift: {layer.ChaosShift}");
            Console.WriteLine($"  Modifier: {layer.Modifier}");
            Console.WriteLine($"  Output: {layer.OutputValue}");
            if (!string.IsNullOrWhiteSpace(layer.CalculationDetails))
            {
                Console.WriteLine($"  Details: {layer.CalculationDetails}");
            }
            Console.WriteLine($"  Reason: {layer.Reason}");
            Console.WriteLine($"  State: {state}");
        }
    }


    private void PrintFateEffects(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            foreach (var group in EffectCatalog.GetAll().OrderBy(x => x.LayerNumber).GroupBy(x => new { x.LayerNumber, x.LayerName }))
            {
                Console.WriteLine($"Layer {group.Key.LayerNumber} — {group.Key.LayerName}");
                foreach (var effect in group.OrderBy(x => x.EffectCode))
                {
                    Console.WriteLine($"  {effect.EffectCode} — {effect.DisplayName} [{effect.InfluenceType}/{effect.Strength}]");
                }

                Console.WriteLine();
            }

            return;
        }

        if (!int.TryParse(args[0], out var layerNumber) || layerNumber < 1 || layerNumber > FateEngineSettings.LayerCount)
        {
            Console.WriteLine("Layer number must be between 1 and 5.");
            return;
        }

        var items = EffectCatalog.GetByLayer(layerNumber);
        if (items.Count == 0)
        {
            Console.WriteLine($"No effects found for layer {layerNumber}.");
            return;
        }

        Console.WriteLine($"Layer {layerNumber} — {items[0].LayerName}");
        foreach (var effect in items.OrderBy(x => x.EffectCode))
        {
            Console.WriteLine($"  {effect.EffectCode} — {effect.DisplayName} [{effect.InfluenceType}/{effect.Strength}]");
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
