using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nri.Shared.Contracts;
using Nri.Shared.Diagnostics;
using Nri.Shared.Domain;

namespace Nri.Performance.Contracts;

internal static class Program
{
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_21_4/performance_contracts.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var findings = new List<string>();

        Run(checks, findings, "histogram.percentiles", VerifyPercentiles);
        Run(checks, findings, "collector.boundedCapacity", VerifyBoundedCapacity);
        Run(checks, findings, "collector.concurrentRecording", VerifyConcurrentRecording);
        Run(checks, findings, "payload.utf8ByteCount", VerifyPayloadByteCount);
        Run(checks, findings, "sync.staleGenerationExcluded", VerifyStaleGeneration);
        Run(checks, findings, "poller.noOverlap", VerifyNoOverlap);
        Run(checks, findings, "refresh.cancellationAndDisposal", VerifyCancellation);
        Run(checks, findings, "reconnect.counterReset", VerifyReconnectReset);
        Run(checks, findings, "mutation.duplicatePreventionBounded", VerifyDuplicatePrevention);
        Run(checks, findings, "sync.fullReplacementOnlyOnGap", VerifyFullReplacementPolicy);

        var pass = checks.Count == 10 && checks.Values.All(value => value);
        var json = new Dictionary<string, object>
        {
            { "status", pass ? "PASS" : "NOT_PASS" },
            { "computedFromChecks", true },
            { "checks", checks.ToDictionary(pair => pair.Key, pair => (object)pair.Value) },
            { "findings", findings.Cast<object>().ToArray() },
            { "executedAtUtc", DateTime.UtcNow }
        };
        File.WriteAllText(output, JsonProtocolSerializer.Serialize(json), new UTF8Encoding(false));
        Console.WriteLine($"0.21.4 performance contracts: {(pass ? "PASS" : "NOT_PASS")}");
        Console.WriteLine("Artifact: " + output);
        return pass ? 0 : 1;
    }

    private static void VerifyPercentiles()
    {
        var values = new[] { 1d, 2d, 3d, 4d, 5d };
        Require(RuntimePerformanceTelemetry0214.Percentile(values, 0.50d) == 3d, "p50 mismatch");
        Require(RuntimePerformanceTelemetry0214.Percentile(values, 0.95d) == 4.8d, "p95 mismatch");
        Require(RuntimePerformanceTelemetry0214.Percentile(Array.Empty<double>(), 0.95d) == 0d, "empty percentile mismatch");
    }

    private static void VerifyBoundedCapacity()
    {
        var telemetry = new RuntimePerformanceTelemetry0214("test", 32, new FakeProcessMetrics());
        for (var i = 0; i < 100; i++) telemetry.Record(Sample(i));
        var snapshot = telemetry.Snapshot();
        Require(snapshot.Capacity == 32, "capacity mismatch");
        Require(snapshot.RetainedSampleCount == 32, "retained samples exceed capacity");
        Require(snapshot.TotalRecordedCount == 100, "total sample count mismatch");
        Require(snapshot.DroppedSampleCount == 68, "dropped count mismatch");
    }

    private static void VerifyConcurrentRecording()
    {
        var telemetry = new RuntimePerformanceTelemetry0214("concurrent", 128, new FakeProcessMetrics());
        var tasks = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++) telemetry.Record(Sample(worker * 1000 + i));
        })).ToArray();
        Task.WaitAll(tasks);
        var snapshot = telemetry.Snapshot();
        Require(snapshot.TotalRecordedCount == 8000, "concurrent records were lost");
        Require(snapshot.RetainedSampleCount == 128, "concurrent capacity mismatch");
    }

    private static void VerifyPayloadByteCount()
    {
        var request = new RequestEnvelope
        {
            Command = "performance.contract",
            Payload = new Dictionary<string, object> { { "text", "Привет" } }
        };
        var json = JsonProtocolSerializer.Serialize(request);
        var measured = Encoding.UTF8.GetByteCount(json);
        Require(measured > json.Length, "UTF-8 multibyte payload was counted as characters");
        Require(measured == Encoding.UTF8.GetBytes(json).Length, "UTF-8 byte count mismatch");
    }

    private static void VerifyStaleGeneration()
    {
        var store = new ModuleSyncStateStore();
        var current = Stamp(2, 10, 5);
        Require(store.AcceptSnapshot("character", current) == SyncAcceptanceResult.Accepted, "initial snapshot rejected");
        Require(store.AcceptSnapshot("character", Stamp(1, 10, 6)) == SyncAcceptanceResult.StaleGeneration, "stale generation accepted");
    }

    private static void VerifyNoOverlap()
    {
        var gate = new NonOverlappingOperationGate0214();
        Require(gate.TryEnter(), "first poller entry rejected");
        var second = Task.Run(() => gate.TryEnter()).Result;
        Require(!second, "overlapping poller entered");
        Require(gate.PreventedOverlapCount == 1, "prevented overlap not counted");
        gate.Exit();
        Require(gate.TryEnter(), "gate did not reset");
        gate.Exit();
    }

    private static void VerifyCancellation()
    {
        using var coordinator = new RefreshCancellationCoordinator0214();
        var first = coordinator.Begin();
        var second = coordinator.Begin();
        Require(first.Token.IsCancellationRequested, "old refresh was not cancelled");
        Require(!coordinator.IsCurrent(first), "old refresh remained current");
        Require(coordinator.IsCurrent(second), "latest refresh was rejected");
    }

    private static void VerifyReconnectReset()
    {
        var lifecycle = new ConnectionLifecycleCoordinator();
        lifecycle.BeginConnect(false);
        lifecycle.MarkPhysicalConnectionEstablished();
        lifecycle.MarkTransportLost("planned");
        lifecycle.BeginConnect(true);
        Require(lifecycle.Current.AttemptNumber == 1, "reconnect attempt not counted");
        lifecycle.MarkReady(7);
        Require(lifecycle.Current.AttemptNumber == 0, "reconnect attempts not reset at Ready");
    }

    private static void VerifyDuplicatePrevention()
    {
        var store = new ModuleSyncStateStore();
        Require(store.AcceptSnapshot("inventory", Stamp(1, 1, 1)) == SyncAcceptanceResult.Accepted, "snapshot rejected");
        Require(store.AcceptDelta("inventory", Stamp(1, 1, 2), 1, "operation-1") == SyncAcceptanceResult.Accepted, "first operation rejected");
        Require(store.AcceptDelta("inventory", Stamp(1, 1, 3), 2, "operation-1") == SyncAcceptanceResult.Duplicate, "duplicate operation accepted");
        var window = new BoundedIdentityWindow0214(16);
        for (var i = 0; i < 100; i++) Require(window.TryAdd("id-" + i), "unique identity rejected");
        Require(window.Count == 16, "identity window is unbounded");
    }

    private static void VerifyFullReplacementPolicy()
    {
        var store = new ModuleSyncStateStore();
        Require(store.AcceptDelta("map", Stamp(1, 1, 2), 1) == SyncAcceptanceResult.RequiresFullReplacement, "missing snapshot did not require replacement");
        Require(store.AcceptSnapshot("map", Stamp(1, 1, 5)) == SyncAcceptanceResult.Accepted, "snapshot rejected");
        Require(store.AcceptDelta("map", Stamp(1, 1, 6), 4) == SyncAcceptanceResult.DeltaGap, "delta gap accepted");
        Require(store.AcceptDelta("map", Stamp(1, 1, 6), 5) == SyncAcceptanceResult.Accepted, "contiguous delta rejected");
    }

    private static SyncVersionStamp Stamp(long generation, long context, long module) => new()
    {
        ConnectionGeneration = generation,
        ContextRevision = context,
        ModuleRevision = module,
        CampaignId = "campaign",
        SessionId = "session",
        CharacterId = "character"
    };

    private static PerformanceSample0214 Sample(int value) => new()
    {
        Source = "contract",
        Category = "read",
        Command = "command." + (value % 4),
        Status = "Ok",
        ElapsedMilliseconds = value % 100,
        RequestBytes = value,
        ResponseBytes = value * 2
    };

    private static void Run(Dictionary<string, bool> checks, List<string> findings, string name, Action action)
    {
        try
        {
            action();
            checks[name] = true;
        }
        catch (Exception ex)
        {
            checks[name] = false;
            findings.Add(name + ": " + ex.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeProcessMetrics : IProcessMetricsAdapter0214
    {
        public ProcessResourceSnapshot0214 Capture(string component) => new()
        {
            AtUtc = DateTime.UtcNow,
            Component = component,
            PrivateBytes = 100,
            WorkingSetBytes = 80,
            ManagedHeapBytes = 40,
            ThreadCount = 4,
            HandleCount = 12
        };
    }
}
