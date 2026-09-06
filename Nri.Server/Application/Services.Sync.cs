using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope SyncSnapshotGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var requestedScopes = ExtractScopes(context.Request.Payload);
        var allowedScopes = FilterScopesForActor(context, requestedScopes, actor);
        var latestRevision = _syncEvents.GetSnapshotInfo(context.Request.RequestId ?? string.Empty);

        return Ok("Sync snapshot loaded.", new Dictionary<string, object>
        {
            { "latestRevision", latestRevision },
            { "snapshotRequired", false },
            { "scopes", allowedScopes.Cast<object>().ToArray() }
        });
    }

    public ResponseEnvelope SyncChangesGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var afterRevision = PayloadReader.GetLong(context.Request.Payload, "afterRevision") ?? 0;
        var requestedScopes = ExtractScopes(context.Request.Payload);
        var allowedScopes = FilterScopesForActor(context, requestedScopes, actor);
        var limit = PayloadReader.GetInt(context.Request.Payload, "limit") ?? 100;
        if (limit <= 0) limit = 100;

        var result = requestedScopes.Count > 0 && allowedScopes.Count == 0
            ? (LatestRevision: _syncEvents.GetSnapshotInfo(context.Request.RequestId ?? string.Empty), Events: (IReadOnlyCollection<SyncEvent>)Array.Empty<SyncEvent>())
            : _syncEvents.GetChanges(afterRevision, allowedScopes, limit, context.Request.RequestId ?? string.Empty);
        var eventsPayload = result.Events
            .Where(evt => string.IsNullOrWhiteSpace(evt.CampaignId) || _campaignAuthorization.CanAccessCampaign(context.Session!, evt.CampaignId))
            .Where(evt => string.IsNullOrWhiteSpace(evt.SessionId) || SessionVisibleToActor02110(context, evt.SessionId))
            .Select(ToSyncEventPayload).Cast<object>().ToArray();

        return Ok("Sync changes loaded.", new Dictionary<string, object>
        {
            { "latestRevision", result.LatestRevision },
            { "events", eventsPayload }
        });
    }

    private bool SessionVisibleToActor02110(CommandContext context, string sessionId)
    {
        var session = _repositories.CurrentSessions.Find(MongoDB.Driver.Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault();
        if (session == null) return false;
        try { _campaignAuthorization.RequireSessionCapability(context.Session!, session, CampaignCapabilityIds.SessionView); return true; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static IReadOnlyCollection<string> ExtractScopes(Dictionary<string, object> payload)
    {
        var list = PayloadReader.GetList(payload, "scopes");
        if (list == null) return Array.Empty<string>();
        return list
            .Select(Convert.ToString)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyCollection<string> FilterScopesForActor(CommandContext context, IReadOnlyCollection<string> requested, UserAccount actor)
    {
        var isAdmin = actor.Roles.Any(r => r == UserRole.Admin || r == UserRole.SuperAdmin);
        if (requested.Count == 0)
        {
            return isAdmin
                ? new[] { SyncScopes.Global, SyncScopes.Dice, SyncScopes.Fate, SyncScopes.Definitions, SyncScopes.Admin }
                : new[] { SyncScopes.Global, SyncScopes.Dice, SyncScopes.Fate };
        }

        var result = new List<string>();
        foreach (var scope in requested)
        {
            if (string.Equals(scope, SyncScopes.Admin, StringComparison.OrdinalIgnoreCase) && !isAdmin) continue;
            if (scope.StartsWith("server:", StringComparison.OrdinalIgnoreCase) && !isAdmin) continue;
            if (scope.StartsWith("private:", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(scope.Substring("private:".Length), actor.Id, StringComparison.Ordinal)) continue;
            if (scope.StartsWith("campaign:", StringComparison.OrdinalIgnoreCase))
            {
                var campaignId = scope.Substring("campaign:".Length);
                if (!_campaignAuthorization.CanAccessCampaign(context.Session!, campaignId)) continue;
            }
            if (scope.StartsWith("session:", StringComparison.OrdinalIgnoreCase))
            {
                var sessionId = scope.Substring("session:".Length);
                if (!SessionVisibleToActor02110(context, sessionId)) continue;
            }
            if (scope.StartsWith("character:", StringComparison.OrdinalIgnoreCase))
            {
                var characterId = scope.Substring("character:".Length);
                var ownership = _repositories.CharacterOwnerships.Find(
                    MongoDB.Driver.Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)
                    & MongoDB.Driver.Builders<CharacterOwnershipState>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();
                var hasAccess = ownership != null
                    && (string.Equals(ownership.OwnerUserId, actor.Id, StringComparison.Ordinal)
                        || string.Equals(ownership.ControlledByUserId, actor.Id, StringComparison.Ordinal)
                        || _campaignAuthorization.GetEffectiveCapabilities(actor.Id, ownership.CampaignId)
                            .Contains(CampaignCapabilityIds.CharacterManageAnyInCampaign));
                if (!hasAccess) continue;
            }

            result.Add(scope);
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static Dictionary<string, object> ToSyncEventPayload(SyncEvent evt)
    {
        return new Dictionary<string, object>
        {
            { "id", evt.Id },
            { "revision", evt.Revision },
            { "type", evt.Type },
            { "scope", evt.Scope },
            { "entityType", evt.EntityType },
            { "entityId", evt.EntityId },
            { "operation", evt.Operation },
            { "actorUserId", evt.ActorUserId },
            { "createdUtc", evt.CreatedUtc },
            { "payload", NormalizeSyncPayload(evt.Payload) },
            { "schemaVersion", evt.SchemaVersion }
        };
    }

    private static object NormalizeSyncPayload(object? value)
    {
        if (value == null) return string.Empty;
        if (value is string || value is bool || value is byte || value is sbyte ||
            value is short || value is ushort || value is int || value is uint ||
            value is long || value is ulong || value is float || value is double ||
            value is decimal || value is DateTime || value is Guid)
            return value;

        if (value is IDictionary<string, object> typed)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in typed)
                result[item.Key] = NormalizeSyncPayload(item.Value);
            return result;
        }

        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                result[key] = NormalizeSyncPayload(entry.Value);
            }
            return result;
        }

        if (value is IEnumerable enumerable && value is not string)
            return enumerable.Cast<object?>().Select(NormalizeSyncPayload).Cast<object>().ToArray();

        return Convert.ToString(value) ?? string.Empty;
    }
}
