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
        var allowedScopes = FilterScopesForActor(requestedScopes, actor);
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
        var allowedScopes = FilterScopesForActor(requestedScopes, actor);
        var limit = PayloadReader.GetInt(context.Request.Payload, "limit");
        if (limit <= 0) limit = 100;

        var result = _syncEvents.GetChanges(afterRevision, allowedScopes, limit, context.Request.RequestId ?? string.Empty);
        var eventsPayload = result.Events.Select(ToSyncEventPayload).Cast<object>().ToArray();

        return Ok("Sync changes loaded.", new Dictionary<string, object>
        {
            { "latestRevision", result.LatestRevision },
            { "events", eventsPayload }
        });
    }

    private static IReadOnlyCollection<string> ExtractScopes(Dictionary<string, object> payload)
    {
        var list = PayloadReader.GetList(payload, "scopes") ?? new ArrayList();
        return list.Cast<object>()
            .Select(Convert.ToString)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyCollection<string> FilterScopesForActor(IReadOnlyCollection<string> requested, UserAccount actor)
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
            if (scope.StartsWith("character:", StringComparison.OrdinalIgnoreCase) && !isAdmin)
            {
                var characterId = scope.Substring("character:".Length);
                var hasAccess = _repositories.Characters.Find(MongoDB.Driver.Builders<Character>.Filter.Eq(x => x.Id, characterId) & MongoDB.Driver.Builders<Character>.Filter.Eq(x => x.OwnerUserId, actor.Id)).Any();
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
            { "payload", evt.Payload },
            { "schemaVersion", evt.SchemaVersion }
        };
    }
}
