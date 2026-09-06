using System;
using System.Collections.Generic;
using Nri.Shared.Contracts;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope MapPlayerSceneSync0204(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!MapPlayerSceneEnabled())
            return Error("Player scene map endpoints are disabled.", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var knownRevision = PayloadReader.GetLong(payload, "projectionRevision") ?? 0L;
        var projection = _playerMapProjectionService.BuildSceneMap(mapId, ProjectionContext0204(actor.Id, payload, adminPreview: false));
        if (!projection.Success) return ProjectionError0204(projection);
        var currentRevision = PayloadReader.GetLong(projection.Payload, "projectionRevision") ?? 0L;
        if (knownRevision == currentRevision)
        {
            return Ok("Player map projection is current.", new Dictionary<string, object>
            {
                ["snapshotKind"] = "no_change",
                ["projectionRevision"] = currentRevision,
                ["fullSnapshotVersion"] = 1,
                ["builtAtUtc"] = DateTime.UtcNow
            });
        }

        projection.Payload["fallbackReason"] = knownRevision > currentRevision ? "stale_or_future_revision" : "projection_changed";
        return Ok("Player map projection refreshed.", projection.Payload);
    }

    public ResponseEnvelope MapAdminPlayerPreviewGet0204(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapPlayerSceneEnabled())
            return Error("Player scene map endpoints are disabled.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var characterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 1, 128, "characterId");
        var projection = _playerMapProjectionService.BuildSceneMap(mapId, ProjectionContext0204(actor.Id, payload, adminPreview: true));
        if (!projection.Success) return ProjectionError0204(projection);
        projection.Payload["previewCharacterId"] = characterId;
        projection.Payload["previewMode"] = "server_player_projection";
        return Ok("Player map preview loaded.", projection.Payload);
    }

    private static PlayerMapProjectionContext0204 ProjectionContext0204(string actorUserId, IDictionary<string, object> payload, bool adminPreview)
        => new()
        {
            ActorUserId = actorUserId,
            CharacterId = PayloadReader.GetString(payload, "characterId") ?? string.Empty,
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
            SessionId = PayloadReader.GetString(payload, "sessionId") ?? string.Empty,
            ActiveGroupId = PayloadReader.GetString(payload, "activeGroupId") ?? string.Empty,
            AdminPreview = adminPreview,
            IncludeMarkers = !payload.ContainsKey("includeMarkers") || PayloadReader.GetBool(payload, "includeMarkers")
        };

    private ResponseEnvelope ProjectionError0204(PlayerMapProjectionResult0204 projection)
    {
        var status = projection.ErrorKind == "not_found" ? ResponseStatus.NotFound
            : projection.ErrorKind == "forbidden" ? ResponseStatus.Forbidden
            : ResponseStatus.Conflict;
        return Error(projection.Message, status, status == ResponseStatus.NotFound ? ErrorCode.NotFound : status == ResponseStatus.Forbidden ? ErrorCode.Forbidden : ErrorCode.Conflict);
    }
}
