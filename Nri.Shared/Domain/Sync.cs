using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class SyncEvent : EntityBase
{
    public long Revision { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Scope { get; set; } = SyncScopes.Global;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
}

public sealed class SyncCounter : EntityBase
{
    public string CounterKey { get; set; } = "global";
    public long Value { get; set; }
}

public sealed class EntityRevisionInfo : EntityBase
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public static class SyncScopes
{
    public const string Global = "global";
    public const string Admin = "admin";
    public const string Dice = "dice";
    public const string Fate = "fate";
    public const string Definitions = "definitions";

    public static string Chat(string sessionId) => $"chat:{sessionId}";
    public static string Character(string characterId) => $"character:{characterId}";
    public static string Combat(string combatId) => $"combat:{combatId}";
}
