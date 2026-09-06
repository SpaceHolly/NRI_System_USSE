using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Diagnostics;

namespace Nri.Shared.Domain;

public enum ConnectionLifecycleState
{
    Disconnected,
    Connecting,
    Authenticating,
    Connected,
    Reconnecting,
    RestoringContext,
    RestoringModules,
    Ready,
    SessionExpired,
    Fatal
}

public sealed class ReconnectRetryPolicy
{
    public int MaxAttempts { get; set; } = 8;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.Zero;
    public TimeSpan MaximumDelay { get; set; } = TimeSpan.FromSeconds(15);
    public double Multiplier { get; set; } = 1.8d;

    public TimeSpan DelayForAttempt(int attemptNumber)
    {
        if (attemptNumber <= 1) return InitialDelay;
        var milliseconds = InitialDelay <= TimeSpan.Zero
            ? 500d
            : InitialDelay.TotalMilliseconds;
        milliseconds *= Math.Pow(Math.Max(1d, Multiplier), attemptNumber - 2);
        return TimeSpan.FromMilliseconds(Math.Min(MaximumDelay.TotalMilliseconds, milliseconds));
    }
}

public sealed class ConnectionLifecycleSnapshot
{
    public long ConnectionGeneration { get; set; }
    public ConnectionLifecycleState State { get; set; } = ConnectionLifecycleState.Disconnected;
    public int AttemptNumber { get; set; }
    public DateTime? ConnectedAtUtc { get; set; }
    public string LastDisconnectReason { get; set; } = string.Empty;
    public long CurrentContextRevision { get; set; } = -1;
    public DateTime? LastSuccessfulRestoreAtUtc { get; set; }
    public DateTime? LastReconnectAttemptAtUtc { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public string ReadableStatus { get; set; } = "Нет подключения";
    public ReconnectRetryPolicy RetryPolicy { get; set; } = new();

    public bool IsRecovering => State == ConnectionLifecycleState.Reconnecting
                                || State == ConnectionLifecycleState.Connecting
                                || State == ConnectionLifecycleState.Authenticating
                                || State == ConnectionLifecycleState.RestoringContext
                                || State == ConnectionLifecycleState.RestoringModules;
    public bool CanMutate => State == ConnectionLifecycleState.Ready;
    public bool IsStaleReadOnly => IsRecovering;

    public ConnectionLifecycleSnapshot Clone() => new()
    {
        ConnectionGeneration = ConnectionGeneration,
        State = State,
        AttemptNumber = AttemptNumber,
        ConnectedAtUtc = ConnectedAtUtc,
        LastDisconnectReason = LastDisconnectReason,
        CurrentContextRevision = CurrentContextRevision,
        LastSuccessfulRestoreAtUtc = LastSuccessfulRestoreAtUtc,
        LastReconnectAttemptAtUtc = LastReconnectAttemptAtUtc,
        NextRetryAtUtc = NextRetryAtUtc,
        ReadableStatus = ReadableStatus,
        RetryPolicy = RetryPolicy
    };
}

public sealed class ConnectionLifecycleChangedEventArgs : EventArgs
{
    public ConnectionLifecycleChangedEventArgs(ConnectionLifecycleSnapshot previous, ConnectionLifecycleSnapshot current)
    {
        Previous = previous;
        Current = current;
    }

    public ConnectionLifecycleSnapshot Previous { get; }
    public ConnectionLifecycleSnapshot Current { get; }
}

public sealed class ConnectionLifecycleCoordinator
{
    private readonly object _gate = new();
    private readonly ConnectionLifecycleSnapshot _current = new();

    public event EventHandler<ConnectionLifecycleChangedEventArgs>? StateChanged;

    public ConnectionLifecycleSnapshot Current
    {
        get { lock (_gate) return _current.Clone(); }
    }

    public void BeginConnect(bool reconnect)
    {
        lock (_gate)
        {
            var previous = _current.Clone();
            _current.State = reconnect ? ConnectionLifecycleState.Reconnecting : ConnectionLifecycleState.Connecting;
            if (reconnect) _current.AttemptNumber++;
            else _current.AttemptNumber = 0;
            _current.LastReconnectAttemptAtUtc = reconnect ? DateTime.UtcNow : null;
            _current.NextRetryAtUtc = null;
            _current.ReadableStatus = reconnect ? "Повторное подключение" : "Подключение к серверу";
            Publish(previous);
        }
    }

    public long MarkPhysicalConnectionEstablished()
    {
        lock (_gate)
        {
            var previous = _current.Clone();
            _current.ConnectionGeneration++;
            _current.State = ConnectionLifecycleState.Connected;
            _current.ConnectedAtUtc = DateTime.UtcNow;
            _current.ReadableStatus = "Соединение установлено";
            Publish(previous);
            return _current.ConnectionGeneration;
        }
    }

    public void MarkAuthenticating() => Transition(ConnectionLifecycleState.Authenticating, "Проверка учётной записи");
    public void MarkAuthenticated() => Transition(ConnectionLifecycleState.Connected, "Учётная запись подтверждена");
    public void MarkRestoringContext() => Transition(ConnectionLifecycleState.RestoringContext, "Восстановление контекста");
    public void MarkRestoringModules() => Transition(ConnectionLifecycleState.RestoringModules, "Обновление данных");

    public void MarkReady(long contextRevision)
    {
        lock (_gate)
        {
            var previous = _current.Clone();
            _current.State = ConnectionLifecycleState.Ready;
            _current.AttemptNumber = 0;
            _current.NextRetryAtUtc = null;
            _current.CurrentContextRevision = contextRevision;
            _current.LastSuccessfulRestoreAtUtc = DateTime.UtcNow;
            _current.ReadableStatus = "Готово";
            Publish(previous);
        }
    }

    public void MarkTransportLost(string reason)
    {
        lock (_gate)
        {
            var previous = _current.Clone();
            _current.State = _current.ConnectionGeneration > 0
                ? ConnectionLifecycleState.Reconnecting
                : ConnectionLifecycleState.Disconnected;
            _current.LastDisconnectReason = reason ?? string.Empty;
            _current.NextRetryAtUtc = _current.State == ConnectionLifecycleState.Reconnecting
                ? DateTime.UtcNow + _current.RetryPolicy.DelayForAttempt(_current.AttemptNumber + 1)
                : null;
            _current.ReadableStatus = _current.State == ConnectionLifecycleState.Reconnecting
                ? _current.AttemptNumber >= _current.RetryPolicy.MaxAttempts
                    ? "Автоматическое подключение приостановлено"
                    : "Соединение потеряно"
                : "Нет подключения";
            Publish(previous);
        }
    }

    public void MarkDisconnected(string reason = "")
    {
        lock (_gate)
        {
            var previous = _current.Clone();
            _current.State = ConnectionLifecycleState.Disconnected;
            _current.LastDisconnectReason = reason ?? string.Empty;
            _current.ReadableStatus = "Нет подключения";
            Publish(previous);
        }
    }

    public void MarkSessionExpired(string reason)
    {
        lock (_gate)
        {
            var previous = _current.Clone();
            _current.State = ConnectionLifecycleState.SessionExpired;
            _current.LastDisconnectReason = reason ?? string.Empty;
            _current.ReadableStatus = "Сессия завершена — войдите снова";
            Publish(previous);
        }
    }

    public bool Accepts(long generation, long contextRevision)
    {
        lock (_gate)
        {
            return generation == _current.ConnectionGeneration
                   && contextRevision >= _current.CurrentContextRevision
                   && _current.State != ConnectionLifecycleState.SessionExpired
                   && _current.State != ConnectionLifecycleState.Fatal;
        }
    }

    public bool CanAttemptReconnect(DateTime utcNow)
    {
        lock (_gate)
        {
            return _current.State == ConnectionLifecycleState.Reconnecting
                   && _current.AttemptNumber < _current.RetryPolicy.MaxAttempts
                   && (!_current.NextRetryAtUtc.HasValue || utcNow >= _current.NextRetryAtUtc.Value);
        }
    }

    public void ResetRetryBudget()
    {
        lock (_gate)
        {
            _current.AttemptNumber = 0;
            _current.NextRetryAtUtc = DateTime.UtcNow;
        }
    }

    private void Transition(ConnectionLifecycleState state, string readableStatus, bool incrementAttempt = false)
    {
        lock (_gate)
        {
            var previous = _current.Clone();
            _current.State = state;
            if (incrementAttempt) _current.AttemptNumber++;
            _current.ReadableStatus = readableStatus;
            Publish(previous);
        }
    }

    private void Publish(ConnectionLifecycleSnapshot previous)
        => StateChanged?.Invoke(this, new ConnectionLifecycleChangedEventArgs(previous, _current.Clone()));
}

public enum ModuleRevisionKind
{
    None,
    MonotonicRevision,
    Cursor,
    Timestamp
}

public sealed class ModuleSyncDescriptor
{
    public string ModuleKey { get; set; } = string.Empty;
    public string SnapshotCommand { get; set; } = string.Empty;
    public string DeltaChannel { get; set; } = string.Empty;
    public ModuleRevisionKind RevisionKind { get; set; }
    public IReadOnlyList<string> ContextDependencies { get; set; } = Array.Empty<string>();
    public int RestorePriority { get; set; }
    public bool SupportsDelta { get; set; }
    public bool SupportsFullReplacement { get; set; } = true;
    public bool ClearOnContextChange { get; set; }
    public bool PlayerSafeBoundary { get; set; } = true;
}

public static class ModuleSyncRegistry0213
{
    public static IReadOnlyList<ModuleSyncDescriptor> CreateDefault() => new[]
    {
        Descriptor("context", "context.current.get", 10, ModuleRevisionKind.MonotonicRevision, false, true, "campaign", "session", "character"),
        Descriptor("world_time", "session.current.get", 20, ModuleRevisionKind.Timestamp, false, true, "campaign", "session"),
        Descriptor("character", "character.player.hub.get", 30, ModuleRevisionKind.MonotonicRevision, true, true, "character"),
        Descriptor("map", "map.player.scene.active.get", 40, ModuleRevisionKind.MonotonicRevision, true, true, "campaign", "session", "character"),
        Descriptor("combat", "combat.player.snapshot", 41, ModuleRevisionKind.MonotonicRevision, true, true, "campaign", "session", "character"),
        Descriptor("inventory", "character.inventory.get", 50, ModuleRevisionKind.MonotonicRevision, true, true, "character"),
        Descriptor("projects", "project.player.list", 51, ModuleRevisionKind.MonotonicRevision, true, true, "campaign", "character"),
        Descriptor("assets", "asset.player.list", 52, ModuleRevisionKind.MonotonicRevision, true, true, "campaign", "character"),
        Descriptor("chat", "chat.visibleFeed", 60, ModuleRevisionKind.Cursor, true, false, "campaign", "session"),
        Descriptor("requests", "requests.player.list", 61, ModuleRevisionKind.MonotonicRevision, true, true, "character"),
        Descriptor("journal", "eventJournal.player.list", 62, ModuleRevisionKind.Cursor, true, true, "campaign", "character"),
        Descriptor("audio", "audio.player.state.get", 63, ModuleRevisionKind.MonotonicRevision, true, false, "campaign", "session"),
        Descriptor("secondary", string.Empty, 100, ModuleRevisionKind.None, false, true, "campaign", "session", "character")
    }.OrderBy(x => x.RestorePriority).ToArray();

    private static ModuleSyncDescriptor Descriptor(
        string key,
        string snapshot,
        int priority,
        ModuleRevisionKind revisionKind,
        bool supportsDelta,
        bool clearOnContextChange,
        params string[] dependencies) => new()
    {
        ModuleKey = key,
        SnapshotCommand = snapshot,
        DeltaChannel = supportsDelta ? "sync.changes.get" : string.Empty,
        RevisionKind = revisionKind,
        ContextDependencies = dependencies,
        RestorePriority = priority,
        SupportsDelta = supportsDelta,
        SupportsFullReplacement = true,
        ClearOnContextChange = clearOnContextChange,
        PlayerSafeBoundary = true
    };
}

public sealed class SyncVersionStamp
{
    public long ConnectionGeneration { get; set; }
    public long ContextRevision { get; set; }
    public long ModuleRevision { get; set; }
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
}

public enum SyncAcceptanceResult
{
    Accepted,
    Duplicate,
    StaleGeneration,
    StaleContext,
    BoundaryMismatch,
    StaleModuleRevision,
    DeltaGap,
    RequiresFullReplacement
}

public sealed class ModuleSyncStateStore
{
    private readonly Dictionary<string, SyncVersionStamp> _versions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BoundedIdentityWindow0214> _identities = new(StringComparer.OrdinalIgnoreCase);

    public SyncAcceptanceResult AcceptSnapshot(string moduleKey, SyncVersionStamp incoming)
    {
        if (_versions.TryGetValue(moduleKey, out var current))
        {
            var guard = ValidateBoundary(current, incoming);
            if (guard != SyncAcceptanceResult.Accepted) return guard;
            if (incoming.ModuleRevision < current.ModuleRevision) return SyncAcceptanceResult.StaleModuleRevision;
        }

        _versions[moduleKey] = Clone(incoming);
        _identities.Remove(moduleKey);
        return SyncAcceptanceResult.Accepted;
    }

    public SyncAcceptanceResult AcceptDelta(string moduleKey, SyncVersionStamp incoming, long baseRevision, string? identity = null)
    {
        if (!_versions.TryGetValue(moduleKey, out var current)) return SyncAcceptanceResult.RequiresFullReplacement;
        var guard = ValidateBoundary(current, incoming);
        if (guard != SyncAcceptanceResult.Accepted) return guard;
        if (baseRevision != current.ModuleRevision) return SyncAcceptanceResult.DeltaGap;
        if (incoming.ModuleRevision <= current.ModuleRevision) return SyncAcceptanceResult.StaleModuleRevision;
        if (!string.IsNullOrWhiteSpace(identity))
        {
            if (!_identities.TryGetValue(moduleKey, out var set))
                _identities[moduleKey] = set = new BoundedIdentityWindow0214();
            if (!set.TryAdd(identity!)) return SyncAcceptanceResult.Duplicate;
        }
        _versions[moduleKey] = Clone(incoming);
        return SyncAcceptanceResult.Accepted;
    }

    public void ClearForContextChange(IEnumerable<ModuleSyncDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors.Where(x => x.ClearOnContextChange))
        {
            _versions.Remove(descriptor.ModuleKey);
            _identities.Remove(descriptor.ModuleKey);
        }
    }

    private static SyncAcceptanceResult ValidateBoundary(SyncVersionStamp current, SyncVersionStamp incoming)
    {
        if (incoming.ConnectionGeneration < current.ConnectionGeneration) return SyncAcceptanceResult.StaleGeneration;
        if (incoming.ContextRevision < current.ContextRevision) return SyncAcceptanceResult.StaleContext;
        if (!Same(incoming.CampaignId, current.CampaignId)
            || !Same(incoming.SessionId, current.SessionId)
            || !Same(incoming.CharacterId, current.CharacterId)) return SyncAcceptanceResult.BoundaryMismatch;
        return SyncAcceptanceResult.Accepted;
    }

    private static bool Same(string left, string right) => string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    private static SyncVersionStamp Clone(SyncVersionStamp value) => new()
    {
        ConnectionGeneration = value.ConnectionGeneration,
        ContextRevision = value.ContextRevision,
        ModuleRevision = value.ModuleRevision,
        CampaignId = value.CampaignId,
        SessionId = value.SessionId,
        CharacterId = value.CharacterId
    };
}

public enum PendingRequestKind
{
    ReadOnly,
    IdempotentMutation,
    NonIdempotentMutation
}

public enum PendingOperationResolution
{
    CancelAndRefresh,
    QueryOperationStatus,
    RequiresUserReconciliation
}

public static class PendingOperationPolicy0213
{
    public static PendingOperationResolution OnDisconnect(PendingRequestKind kind, bool hasOperationId)
    {
        if (kind == PendingRequestKind.ReadOnly) return PendingOperationResolution.CancelAndRefresh;
        if (kind == PendingRequestKind.IdempotentMutation && hasOperationId) return PendingOperationResolution.QueryOperationStatus;
        return PendingOperationResolution.RequiresUserReconciliation;
    }

    public const string UnknownResultMessage = "Результат операции неизвестен. Обновите данные.";
}

public static class CommandSafetyClassifier0213
{
    private static readonly string[] ReadMarkers =
    {
        ".get", ".list", ".snapshot", ".feed", ".search", ".preview", ".validate", ".status", "visibleFeed", "current.get"
    };

    private static readonly string[] MutationMarkers =
    {
        ".create", ".update", ".delete", ".remove", ".add", ".set", ".move", ".archive", ".restore",
        ".approve", ".reject", ".cancel", ".submit", ".send", ".roll", ".start", ".end", ".advance",
        ".assign", ".acquire", ".release", ".save", ".import", ".execute", ".complete"
    };

    public static bool IsAuthenticationCommand(string? command)
        => string.Equals(command, "auth.login", StringComparison.OrdinalIgnoreCase)
           || string.Equals(command, "auth.register", StringComparison.OrdinalIgnoreCase);

    public static bool IsReadOnly(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        if (ReadMarkers.Any(marker => command!.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)) return true;
        return !MutationMarkers.Any(marker => command!.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public static bool CanSend(ConnectionLifecycleSnapshot lifecycle, string? command)
    {
        if (IsAuthenticationCommand(command)) return true;
        if (lifecycle.State == ConnectionLifecycleState.Ready) return true;
        return IsReadOnly(command) && lifecycle.State != ConnectionLifecycleState.SessionExpired && lifecycle.State != ConnectionLifecycleState.Fatal;
    }
}
