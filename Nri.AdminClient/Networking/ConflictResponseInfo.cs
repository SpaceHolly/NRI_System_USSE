using System;
using System.Collections.Generic;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.Networking;

public sealed class ConflictResponseInfo
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public long ExpectedRevision { get; set; }
    public long CurrentRevision { get; set; }
    public string Message { get; set; } = string.Empty;
}

public static class ConflictResponseParser
{
    public static bool TryParseConflict(ResponseEnvelope response, out ConflictResponseInfo conflict)
    {
        conflict = new ConflictResponseInfo();
        if (response == null || response.Status != ResponseStatus.Conflict) return false;
        var payload = response.Payload ?? new Dictionary<string, object>();
        conflict.EntityType = payload.TryGetValue("entityType", out var entityType) ? Convert.ToString(entityType) ?? string.Empty : string.Empty;
        conflict.EntityId = payload.TryGetValue("entityId", out var entityId) ? Convert.ToString(entityId) ?? string.Empty : string.Empty;
        conflict.ExpectedRevision = ParseLong(payload, "expectedRevision");
        conflict.CurrentRevision = ParseLong(payload, "currentRevision");
        conflict.Message = response.Message ?? string.Empty;
        return true;
    }

    private static long ParseLong(Dictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null) return 0;
        if (value is long l) return l;
        if (value is int i) return i;
        return long.TryParse(Convert.ToString(value), out var parsed) ? parsed : 0;
    }
}

public sealed class EntityRevisionState
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
}

public sealed class EntityRevisionStore
{
    private readonly Dictionary<string, EntityRevisionState> _items = new Dictionary<string, EntityRevisionState>(StringComparer.OrdinalIgnoreCase);

    public long GetRevision(string entityType, string entityId)
    {
        return _items.TryGetValue(Key(entityType, entityId), out var state) ? state.Revision : 0;
    }

    public void SetRevision(string entityType, string entityId, long revision, string source)
    {
        _items[Key(entityType, entityId)] = new EntityRevisionState
        {
            EntityType = entityType,
            EntityId = entityId,
            Revision = revision,
            LastSeenUtc = DateTime.UtcNow,
            Source = source
        };
    }

    public bool TryGetExpectedRevision(string entityType, string entityId, out long revision)
    {
        if (_items.TryGetValue(Key(entityType, entityId), out var state))
        {
            revision = state.Revision;
            return true;
        }

        revision = 0;
        return false;
    }

    public void Clear() => _items.Clear();

    public void MarkStale(string entityType, string entityId)
    {
        var key = Key(entityType, entityId);
        if (_items.TryGetValue(key, out var state))
        {
            state.Source = "stale";
            state.LastSeenUtc = DateTime.UtcNow;
        }
    }

    private static string Key(string entityType, string entityId) => $"{entityType}:{entityId}";
}

public static class RevisionFeatureFlags
{
    public const bool UseDefinitionExpectedRevision = false;
}
