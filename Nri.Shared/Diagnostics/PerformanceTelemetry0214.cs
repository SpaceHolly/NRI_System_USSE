using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Nri.Shared.Diagnostics;

public sealed class ClientRuntimeDiagnostics0214
{
    public string ClientType { get; set; } = string.Empty;
    public long ConnectionGeneration { get; set; }
    public long PrivateBytes { get; set; }
    public long WorkingSetBytes { get; set; }
    public long ManagedHeapBytes { get; set; }
    public long PeakPrivateBytes { get; set; }
    public long PeakWorkingSetBytes { get; set; }
    public long PeakManagedHeapBytes { get; set; }
    public double CpuPercent { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public int PeakThreadCount { get; set; }
    public int PeakHandleCount { get; set; }
    public double UiLagP95Ms { get; set; }
    public double UiLagMaxMs { get; set; }
    public int ActivePollers { get; set; }
    public int ActiveReconnectLoops { get; set; }
    public int ActiveTimers { get; set; }
    public int InFlightRefreshes { get; set; }
    public int PendingOperations { get; set; }
    public int ReconciledOperations { get; set; }
    public int UnknownOperations { get; set; }
    public DateTime CapturedAtUtc { get; set; }
}

public sealed class PerformanceSample0214
{
    public DateTime AtUtc { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public long ElapsedMilliseconds { get; set; }
    public int RequestBytes { get; set; }
    public int ResponseBytes { get; set; }
    public long ConnectionGeneration { get; set; }
}

public sealed class ProcessResourceSnapshot0214
{
    public DateTime AtUtc { get; set; }
    public string Component { get; set; } = string.Empty;
    public long PrivateBytes { get; set; }
    public long WorkingSetBytes { get; set; }
    public long ManagedHeapBytes { get; set; }
    public double CpuPercent { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public long PeakPrivateBytes { get; set; }
    public long PeakWorkingSetBytes { get; set; }
    public long PeakManagedHeapBytes { get; set; }
    public int PeakThreadCount { get; set; }
    public int PeakHandleCount { get; set; }
}

public sealed class PerformanceCommandSummary0214
{
    public string Command { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ErrorCount { get; set; }
    public double P50Milliseconds { get; set; }
    public double P95Milliseconds { get; set; }
    public double P99Milliseconds { get; set; }
    public long MaximumMilliseconds { get; set; }
    public int MaximumRequestBytes { get; set; }
    public int MaximumResponseBytes { get; set; }
}

public sealed class RuntimePerformanceSnapshot0214
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime BuiltAtUtc { get; set; }
    public long ElapsedSeconds { get; set; }
    public int Capacity { get; set; }
    public int RetainedSampleCount { get; set; }
    public long TotalRecordedCount { get; set; }
    public long DroppedSampleCount { get; set; }
    public ProcessResourceSnapshot0214 Process { get; set; } = new();
    public IReadOnlyList<PerformanceCommandSummary0214> Commands { get; set; } = Array.Empty<PerformanceCommandSummary0214>();
    public IReadOnlyDictionary<string, int> Counters { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, ClientRuntimeDiagnostics0214> ConnectedClients { get; set; } = new Dictionary<string, ClientRuntimeDiagnostics0214>();
    public double UiLagP95Ms { get; set; }
    public double UiLagMaxMs { get; set; }
}

public interface IProcessMetricsAdapter0214
{
    ProcessResourceSnapshot0214 Capture(string component);
}

public sealed class ProcessMetricsAdapter0214 : IProcessMetricsAdapter0214
{
    private readonly object _gate = new();
    private TimeSpan _lastCpu;
    private DateTime _lastCapturedUtc;

    public ProcessResourceSnapshot0214 Capture(string component)
    {
        lock (_gate)
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            var now = DateTime.UtcNow;
            var cpu = process.TotalProcessorTime;
            var elapsedMs = _lastCapturedUtc == default ? 0d : (now - _lastCapturedUtc).TotalMilliseconds;
            var cpuMs = _lastCapturedUtc == default ? 0d : (cpu - _lastCpu).TotalMilliseconds;
            var cpuPercent = elapsedMs <= 0d
                ? 0d
                : Math.Max(0d, Math.Min(100d, cpuMs / (elapsedMs * Math.Max(1, Environment.ProcessorCount)) * 100d));
            _lastCpu = cpu;
            _lastCapturedUtc = now;

            return new ProcessResourceSnapshot0214
            {
                AtUtc = now,
                Component = component,
                PrivateBytes = process.PrivateMemorySize64,
                WorkingSetBytes = process.WorkingSet64,
                ManagedHeapBytes = GC.GetTotalMemory(false),
                CpuPercent = Math.Round(cpuPercent, 2),
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount
            };
        }
    }
}

public sealed class RuntimePerformanceTelemetry0214
{
    public const int DefaultCapacity = 4096;
    private const int MaximumCounterKeys = 64;
    private readonly object _gate = new();
    private readonly PerformanceSample0214?[] _samples;
    private readonly Dictionary<string, int> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClientRuntimeDiagnostics0214> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly IProcessMetricsAdapter0214 _processMetrics;
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;
    private int _nextIndex;
    private int _sampleCount;
    private long _totalRecorded;
    private readonly string _component;
    private readonly ProcessResourceSnapshot0214 _processPeak = new();
    private ClientRuntimeDiagnostics0214? _cachedClientDiagnostics;
    private DateTime _cachedClientDiagnosticsAtUtc;

    public RuntimePerformanceTelemetry0214(string component, int capacity = DefaultCapacity, IProcessMetricsAdapter0214? processMetrics = null)
    {
        if (capacity < 16 || capacity > 65536) throw new ArgumentOutOfRangeException(nameof(capacity));
        _component = Normalize(component, 48, "runtime");
        _samples = new PerformanceSample0214[capacity];
        _processMetrics = processMetrics ?? new ProcessMetricsAdapter0214();
    }

    public int Capacity => _samples.Length;

    public void Record(PerformanceSample0214 sample)
    {
        if (sample == null) throw new ArgumentNullException(nameof(sample));
        var safe = new PerformanceSample0214
        {
            AtUtc = sample.AtUtc == default ? DateTime.UtcNow : sample.AtUtc,
            Source = Normalize(sample.Source, 32, _component),
            Category = Normalize(sample.Category, 48, "other"),
            Command = Normalize(sample.Command, 160, "unknown"),
            Status = Normalize(sample.Status, 48, "unknown"),
            Outcome = Normalize(sample.Outcome, 48, "completed"),
            ElapsedMilliseconds = Math.Max(0, sample.ElapsedMilliseconds),
            RequestBytes = Math.Max(0, sample.RequestBytes),
            ResponseBytes = Math.Max(0, sample.ResponseBytes),
            ConnectionGeneration = Math.Max(0, sample.ConnectionGeneration)
        };

        lock (_gate)
        {
            _samples[_nextIndex] = safe;
            _nextIndex = (_nextIndex + 1) % _samples.Length;
            if (_sampleCount < _samples.Length) _sampleCount++;
            _totalRecorded++;
        }
    }

    public void RecordUiLag(double lagMilliseconds)
    {
        Record(new PerformanceSample0214
        {
            Source = _component,
            Category = "ui_dispatcher",
            Command = "ui.dispatcher.heartbeat",
            Status = "Ok",
            Outcome = "heartbeat",
            ElapsedMilliseconds = (long)Math.Round(Math.Max(0d, lagMilliseconds))
        });
    }

    public void SetCounter(string name, int value)
    {
        var key = Normalize(name, 64, string.Empty);
        if (key.Length == 0) return;
        lock (_gate)
        {
            if (!_counters.ContainsKey(key) && _counters.Count >= MaximumCounterKeys) return;
            _counters[key] = Math.Max(0, value);
        }
    }

    public void IncrementCounter(string name, int delta = 1)
    {
        var key = Normalize(name, 64, string.Empty);
        if (key.Length == 0) return;
        lock (_gate)
        {
            if (!_counters.ContainsKey(key) && _counters.Count >= MaximumCounterKeys) return;
            _counters.TryGetValue(key, out var current);
            _counters[key] = Math.Max(0, current + delta);
        }
    }

    public void ObserveClient(string connectionId, ClientRuntimeDiagnostics0214? diagnostics)
    {
        if (diagnostics == null || string.IsNullOrWhiteSpace(connectionId)) return;
        lock (_gate)
        {
            var key = Normalize(connectionId, 96, "connection");
            _clients.TryGetValue(key, out var previous);
            var next = Clone(diagnostics);
            next.PeakPrivateBytes = Math.Max(next.PrivateBytes, previous?.PeakPrivateBytes ?? 0);
            next.PeakWorkingSetBytes = Math.Max(next.WorkingSetBytes, previous?.PeakWorkingSetBytes ?? 0);
            next.PeakManagedHeapBytes = Math.Max(next.ManagedHeapBytes, previous?.PeakManagedHeapBytes ?? 0);
            next.PeakThreadCount = Math.Max(next.ThreadCount, previous?.PeakThreadCount ?? 0);
            next.PeakHandleCount = Math.Max(next.HandleCount, previous?.PeakHandleCount ?? 0);
            _clients[key] = next;
        }
    }

    public void RemoveClient(string connectionId)
    {
        lock (_gate) _clients.Remove(connectionId ?? string.Empty);
    }

    public ClientRuntimeDiagnostics0214 CaptureClientDiagnostics(string clientType, long connectionGeneration)
    {
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (_cachedClientDiagnostics != null && (now - _cachedClientDiagnosticsAtUtc).TotalSeconds < 1d)
            {
                var cached = Clone(_cachedClientDiagnostics);
                cached.ClientType = Normalize(clientType, 32, "Client");
                cached.ConnectionGeneration = Math.Max(0, connectionGeneration);
                return cached;
            }
        }

        var process = _processMetrics.Capture(clientType);
        double uiLagP95;
        double uiLagMaximum;
        Dictionary<string, int> counters;
        lock (_gate)
        {
            var uiLag = SnapshotSamplesUnsafe()
                .Where(item => item.Category == "ui_dispatcher")
                .Select(item => (double)item.ElapsedMilliseconds)
                .ToArray();
            uiLagP95 = Percentile(uiLag, 0.95d);
            uiLagMaximum = uiLag.Length == 0 ? 0d : uiLag.Max();
            counters = new Dictionary<string, int>(_counters, StringComparer.OrdinalIgnoreCase);
        }

        var diagnostics = new ClientRuntimeDiagnostics0214
        {
            ClientType = Normalize(clientType, 32, "Client"),
            ConnectionGeneration = Math.Max(0, connectionGeneration),
            PrivateBytes = process.PrivateBytes,
            WorkingSetBytes = process.WorkingSetBytes,
            ManagedHeapBytes = process.ManagedHeapBytes,
            CpuPercent = process.CpuPercent,
            ThreadCount = process.ThreadCount,
            HandleCount = process.HandleCount,
            UiLagP95Ms = uiLagP95,
            UiLagMaxMs = uiLagMaximum,
            ActivePollers = Counter(counters, "active_pollers"),
            ActiveReconnectLoops = Counter(counters, "active_reconnect_loops"),
            ActiveTimers = Counter(counters, "active_timers"),
            InFlightRefreshes = Counter(counters, "in_flight_refreshes"),
            PendingOperations = Counter(counters, "pending_operations"),
            ReconciledOperations = Counter(counters, "reconciled_operations"),
            UnknownOperations = Counter(counters, "unknown_operations"),
            CapturedAtUtc = now
        };

        lock (_gate)
        {
            _cachedClientDiagnostics = Clone(diagnostics);
            _cachedClientDiagnosticsAtUtc = now;
        }
        return diagnostics;
    }

    public RuntimePerformanceSnapshot0214 Snapshot(bool includeConnectedClients = true)
    {
        List<PerformanceSample0214> samples;
        Dictionary<string, int> counters;
        Dictionary<string, ClientRuntimeDiagnostics0214> clients;
        long total;
        lock (_gate)
        {
            samples = SnapshotSamplesUnsafe();
            counters = new Dictionary<string, int>(_counters, StringComparer.OrdinalIgnoreCase);
            clients = includeConnectedClients
                ? _clients.ToDictionary(pair => pair.Key, pair => Clone(pair.Value), StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ClientRuntimeDiagnostics0214>(StringComparer.OrdinalIgnoreCase);
            total = _totalRecorded;
        }

        var commandSamples = samples.Where(item => item.Category != "ui_dispatcher").ToArray();
        var ui = samples.Where(item => item.Category == "ui_dispatcher").Select(item => (double)item.ElapsedMilliseconds).ToArray();
        var commands = commandSamples
            .GroupBy(item => item.Command + "\n" + item.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildSummary(group.First().Command, group.First().Category, group.ToArray()))
            .OrderByDescending(item => item.P95Milliseconds)
            .ThenByDescending(item => item.MaximumResponseBytes)
            .ToArray();

        var process = CaptureProcessWithPeak();
        return new RuntimePerformanceSnapshot0214
        {
            StartedAtUtc = _startedAtUtc,
            BuiltAtUtc = DateTime.UtcNow,
            ElapsedSeconds = Math.Max(0, (long)(DateTime.UtcNow - _startedAtUtc).TotalSeconds),
            Capacity = _samples.Length,
            RetainedSampleCount = samples.Count,
            TotalRecordedCount = total,
            DroppedSampleCount = Math.Max(0, total - samples.Count),
            Process = process,
            Commands = commands,
            Counters = counters,
            ConnectedClients = clients,
            UiLagP95Ms = Percentile(ui, 0.95d),
            UiLagMaxMs = ui.Length == 0 ? 0d : ui.Max()
        };
    }

    private ProcessResourceSnapshot0214 CaptureProcessWithPeak()
    {
        var process = _processMetrics.Capture(_component);
        lock (_gate)
        {
            _processPeak.PeakPrivateBytes = Math.Max(_processPeak.PeakPrivateBytes, process.PrivateBytes);
            _processPeak.PeakWorkingSetBytes = Math.Max(_processPeak.PeakWorkingSetBytes, process.WorkingSetBytes);
            _processPeak.PeakManagedHeapBytes = Math.Max(_processPeak.PeakManagedHeapBytes, process.ManagedHeapBytes);
            _processPeak.PeakThreadCount = Math.Max(_processPeak.PeakThreadCount, process.ThreadCount);
            _processPeak.PeakHandleCount = Math.Max(_processPeak.PeakHandleCount, process.HandleCount);
            process.PeakPrivateBytes = _processPeak.PeakPrivateBytes;
            process.PeakWorkingSetBytes = _processPeak.PeakWorkingSetBytes;
            process.PeakManagedHeapBytes = _processPeak.PeakManagedHeapBytes;
            process.PeakThreadCount = _processPeak.PeakThreadCount;
            process.PeakHandleCount = _processPeak.PeakHandleCount;
        }
        return process;
    }

    public static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.Where(value => !double.IsNaN(value) && !double.IsInfinity(value)).OrderBy(value => value).ToArray();
        if (sorted.Length == 0) return 0d;
        var bounded = Math.Max(0d, Math.Min(1d, percentile));
        var rank = (sorted.Length - 1) * bounded;
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper) return Math.Round(sorted[lower], 2);
        var interpolated = sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
        return Math.Round(interpolated, 2);
    }

    private List<PerformanceSample0214> SnapshotSamplesUnsafe()
    {
        var result = new List<PerformanceSample0214>(_sampleCount);
        var start = _sampleCount == _samples.Length ? _nextIndex : 0;
        for (var i = 0; i < _sampleCount; i++)
        {
            var sample = _samples[(start + i) % _samples.Length];
            if (sample != null) result.Add(sample);
        }
        return result;
    }

    private static PerformanceCommandSummary0214 BuildSummary(string command, string category, PerformanceSample0214[] samples)
    {
        var latency = samples.Select(item => (double)item.ElapsedMilliseconds).ToArray();
        return new PerformanceCommandSummary0214
        {
            Command = command,
            Category = category,
            Count = samples.Length,
            ErrorCount = samples.Count(item => !string.Equals(item.Status, "Ok", StringComparison.OrdinalIgnoreCase)),
            P50Milliseconds = Percentile(latency, 0.50d),
            P95Milliseconds = Percentile(latency, 0.95d),
            P99Milliseconds = Percentile(latency, 0.99d),
            MaximumMilliseconds = samples.Max(item => item.ElapsedMilliseconds),
            MaximumRequestBytes = samples.Max(item => item.RequestBytes),
            MaximumResponseBytes = samples.Max(item => item.ResponseBytes)
        };
    }

    private static ClientRuntimeDiagnostics0214 Clone(ClientRuntimeDiagnostics0214 value) => new()
    {
        ClientType = Normalize(value.ClientType, 32, "Client"),
        ConnectionGeneration = value.ConnectionGeneration,
        PrivateBytes = value.PrivateBytes,
        WorkingSetBytes = value.WorkingSetBytes,
        ManagedHeapBytes = value.ManagedHeapBytes,
        PeakPrivateBytes = value.PeakPrivateBytes,
        PeakWorkingSetBytes = value.PeakWorkingSetBytes,
        PeakManagedHeapBytes = value.PeakManagedHeapBytes,
        CpuPercent = value.CpuPercent,
        ThreadCount = value.ThreadCount,
        HandleCount = value.HandleCount,
        PeakThreadCount = value.PeakThreadCount,
        PeakHandleCount = value.PeakHandleCount,
        UiLagP95Ms = value.UiLagP95Ms,
        UiLagMaxMs = value.UiLagMaxMs,
        ActivePollers = value.ActivePollers,
        ActiveReconnectLoops = value.ActiveReconnectLoops,
        ActiveTimers = value.ActiveTimers,
        InFlightRefreshes = value.InFlightRefreshes,
        PendingOperations = value.PendingOperations,
        ReconciledOperations = value.ReconciledOperations,
        UnknownOperations = value.UnknownOperations,
        CapturedAtUtc = value.CapturedAtUtc
    };

    private static int Counter(IReadOnlyDictionary<string, int> counters, string name)
        => counters.TryGetValue(name, out var value) ? value : 0;

    private static string Normalize(string? value, int maximumLength, string fallback)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0) normalized = fallback;
        return normalized.Length <= maximumLength ? normalized : normalized.Substring(0, maximumLength);
    }
}

public static class PerformanceTelemetry0214
{
    private static readonly object Gate = new();
    private static RuntimePerformanceTelemetry0214 _current = new("runtime");

    public static RuntimePerformanceTelemetry0214 Current
    {
        get { lock (Gate) return _current; }
    }

    public static void Initialize(string component, int capacity = RuntimePerformanceTelemetry0214.DefaultCapacity)
    {
        lock (Gate) _current = new RuntimePerformanceTelemetry0214(component, capacity);
    }
}
