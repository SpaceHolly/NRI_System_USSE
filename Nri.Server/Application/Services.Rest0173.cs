using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string RestSessions0173Collection = "rest_sessions";
    private const string RestParticipants0173Collection = "rest_participants";
    private const string DowntimeActions0173Collection = "downtime_actions";
    private const string RecoveryGrants0173Collection = "recovery_grants";
    private const string RestAuditEvents0173Collection = "rest_audit_events";
    private bool _rest0173IndexesEnsured;

    public ResponseEnvelope RestAdminListForSession0173(CommandContext context)
    {
        RequireAdmin(context);
        EnsureRest0173Indexes();
        var payload = context.Request.Payload;
        var campaignId = Rest0173CampaignId(payload);
        var sessionId = Rest0173Text(payload, "sessionId", string.Empty, 128);
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId);
        if (!string.IsNullOrWhiteSpace(sessionId)) filter &= Builders<BsonDocument>.Filter.Eq("SessionId", sessionId);
        if (!includeArchived) filter &= Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var items = RestSessions0173().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(200).ToList()
            .Select(x => (object)Rest0173SessionPayload(x, admin: true)).ToArray();
        return Ok("Rest sessions loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope RestAdminGet0173(CommandContext context)
    {
        RequireAdmin(context);
        EnsureRest0173Indexes();
        var rest = Rest0173RequireSession(context.Request.Payload);
        return Ok("Rest session loaded.", Rest0173Envelope(rest, admin: true, viewer: null));
    }

    public ResponseEnvelope RestAdminCreate0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureRest0173Indexes();
        var payload = context.Request.Payload;
        var now = DateTime.UtcNow;
        var restType = Rest0173NormalizeOneOf(Rest0173Text(payload, "restType", "ShortRest", 64), Rest0173RestTypes, "ShortRest");
        var duration = Rest0173ValidateDuration(restType, PayloadReader.GetInt(payload, "plannedDurationMinutes"));
        var session = new BsonDocument
        {
            ["Id"] = Rest0173NewId("rest"),
            ["WorldId"] = Rest0173Text(payload, "worldId", "default-world", 128),
            ["CampaignId"] = Rest0173CampaignId(payload),
            ["SessionId"] = Rest0173Text(payload, "sessionId", "default", 128),
            ["SceneId"] = Rest0173Text(payload, "sceneId", string.Empty, 128),
            ["LocationId"] = Rest0173Text(payload, "locationId", string.Empty, 128),
            ["GroupId"] = Rest0173Text(payload, "groupId", string.Empty, 128),
            ["CreatedByUserId"] = actor.Id,
            ["RestType"] = restType,
            ["Status"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "status", "Planned", 64), Rest0173Statuses, "Planned"),
            ["Visibility"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "visibility", "PlayerVisible", 64), Rest0173VisibilityModes, "PlayerVisible"),
            ["StartWorldTime"] = Rest0173Text(payload, "startWorldTime", string.Empty, 128),
            ["PlannedEndWorldTime"] = Rest0173Text(payload, "plannedEndWorldTime", string.Empty, 128),
            ["ActualEndWorldTime"] = string.Empty,
            ["PlannedDurationMinutes"] = duration,
            ["ActualDurationMinutes"] = 0,
            ["MinimumDurationMinutes"] = Rest0173MinimumDuration(restType),
            ["MaximumRecommendedDurationMinutes"] = Rest0173MaximumDuration(restType),
            ["RestQuality"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "restQuality", "Normal", 64), Rest0173Qualities, "Normal"),
            ["RestLocationSafety"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "restLocationSafety", "Normal", 64), Rest0173LocationSafety, "Normal"),
            ["InterruptedReason"] = string.Empty,
            ["PlayerVisibleSummary"] = Rest0173Text(payload, "playerVisibleSummary", "Отдых запланирован мастером.", 1024),
            ["GmNotes"] = Rest0173Text(payload, "gmNotes", string.Empty, 4096),
            ["DisturbanceMode"] = "None",
            ["RecoveryImpact"] = "None",
            ["DisturbanceSummaryPlayer"] = string.Empty,
            ["DisturbanceGmNotes"] = string.Empty,
            ["ServerOnlyData"] = new BsonDocument { ["scope"] = "rest_0_17_3" },
            ["SchemaVersion"] = "0.17.3",
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["UpdatedByUserId"] = actor.Id,
            ["Revision"] = 1
        };
        RestSessions0173().InsertOne(session);
        Rest0173Audit(actor, CommandNames.RestAdminCreate, Rest0173String(session, "Id"), string.Empty, string.Empty, "rest.created", null, session, "Rest session created.");
        Rest0173Sync("rest.created", "rest_session", Rest0173String(session, "Id"), "created", actor.Id, context.Request.RequestId);
        return Ok("Rest session created.", new Dictionary<string, object> { ["restId"] = Rest0173String(session, "Id"), ["item"] = Rest0173SessionPayload(session, admin: true) });
    }

    public ResponseEnvelope RestAdminUpdate0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureRest0173Indexes();
        var rest = Rest0173RequireSession(context.Request.Payload, includeArchived: true);
        var before = rest.DeepClone().AsBsonDocument;
        var payload = context.Request.Payload;
        if (payload.ContainsKey("restType")) rest["RestType"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "restType", Rest0173String(rest, "RestType"), 64), Rest0173RestTypes, "ShortRest");
        if (payload.ContainsKey("plannedDurationMinutes")) rest["PlannedDurationMinutes"] = Rest0173ValidateDuration(Rest0173String(rest, "RestType"), PayloadReader.GetInt(payload, "plannedDurationMinutes"));
        if (payload.ContainsKey("visibility")) rest["Visibility"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "visibility", Rest0173String(rest, "Visibility"), 64), Rest0173VisibilityModes, "PlayerVisible");
        if (payload.ContainsKey("playerVisibleSummary")) rest["PlayerVisibleSummary"] = Rest0173Text(payload, "playerVisibleSummary", Rest0173String(rest, "PlayerVisibleSummary"), 1024);
        if (payload.ContainsKey("gmNotes")) rest["GmNotes"] = Rest0173Text(payload, "gmNotes", Rest0173String(rest, "GmNotes"), 4096);
        if (payload.ContainsKey("restQuality")) rest["RestQuality"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "restQuality", Rest0173String(rest, "RestQuality"), 64), Rest0173Qualities, "Normal");
        if (payload.ContainsKey("restLocationSafety")) rest["RestLocationSafety"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "restLocationSafety", Rest0173String(rest, "RestLocationSafety"), 64), Rest0173LocationSafety, "Normal");
        Rest0173Touch(rest, actor.Id);
        RestSessions0173().ReplaceOne(Rest0173IdFilter(Rest0173String(rest, "Id")), rest);
        Rest0173Audit(actor, CommandNames.RestAdminUpdate, Rest0173String(rest, "Id"), string.Empty, string.Empty, "rest.updated", before, rest, "Rest session updated.");
        Rest0173Sync("rest.updated", "rest_session", Rest0173String(rest, "Id"), "updated", actor.Id, context.Request.RequestId);
        return Ok("Rest session updated.", new Dictionary<string, object> { ["item"] = Rest0173SessionPayload(rest, admin: true) });
    }

    public ResponseEnvelope RestAdminArchive0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureRest0173Indexes();
        var rest = Rest0173RequireSession(context.Request.Payload, includeArchived: true);
        var before = rest.DeepClone().AsBsonDocument;
        rest["IsArchived"] = true;
        rest["Status"] = "Archived";
        Rest0173Touch(rest, actor.Id);
        RestSessions0173().ReplaceOne(Rest0173IdFilter(Rest0173String(rest, "Id")), rest);
        Rest0173Audit(actor, CommandNames.RestAdminArchive, Rest0173String(rest, "Id"), string.Empty, string.Empty, "rest.archived", before, rest, "Rest session archived.");
        Rest0173Sync("rest.updated", "rest_session", Rest0173String(rest, "Id"), "archived", actor.Id, context.Request.RequestId);
        return Ok("Rest session archived.", new Dictionary<string, object> { ["item"] = Rest0173SessionPayload(rest, admin: true) });
    }

    public ResponseEnvelope RestAdminAddParticipant0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureRest0173Indexes();
        var rest = Rest0173RequireSession(context.Request.Payload);
        var payload = context.Request.Payload;
        var now = DateTime.UtcNow;
        var participant = new BsonDocument
        {
            ["Id"] = Rest0173NewId("rest_participant"),
            ["RestSessionId"] = Rest0173String(rest, "Id"),
            ["CampaignId"] = Rest0173String(rest, "CampaignId"),
            ["CharacterId"] = Rest0173Text(payload, "characterId", string.Empty, 128),
            ["PlayerUserId"] = Rest0173Text(payload, "playerUserId", string.Empty, 128),
            ["ParticipantKind"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "participantKind", "PlayerCharacter", 64), Rest0173ParticipantKinds, "PlayerCharacter"),
            ["DisplayName"] = Rest0173Text(payload, "displayName", "Участник", 160, true),
            ["ParticipationStatus"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "participationStatus", "Planned", 64), Rest0173ParticipantStatuses, "Planned"),
            ["LocalStartWorldTime"] = Rest0173String(rest, "StartWorldTime"),
            ["LocalEndWorldTime"] = string.Empty,
            ["RestMinutesCompleted"] = 0,
            ["EligibleForRecovery"] = payload.ContainsKey("eligibleForRecovery") ? PayloadReader.GetBool(payload, "eligibleForRecovery") : true,
            ["RecoveryResult"] = "None",
            ["PlayerVisibleStatus"] = Rest0173Text(payload, "playerVisibleStatus", "Участвует в отдыхе.", 512),
            ["IsPlayerVisible"] = payload.ContainsKey("isPlayerVisible") ? PayloadReader.GetBool(payload, "isPlayerVisible") : true,
            ["GmNotes"] = Rest0173Text(payload, "gmNotes", string.Empty, 2048),
            ["ServerOnlyData"] = new BsonDocument { ["scope"] = "rest_participant_0_17_3" },
            ["SchemaVersion"] = "0.17.3",
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["UpdatedByUserId"] = actor.Id,
            ["Revision"] = 1
        };
        RestParticipants0173().InsertOne(participant);
        Rest0173Audit(actor, CommandNames.RestAdminAddParticipant, Rest0173String(rest, "Id"), Rest0173String(participant, "Id"), string.Empty, "rest.participant.added", null, participant, "Participant added.");
        Rest0173Sync("rest.participant.updated", "rest_participant", Rest0173String(participant, "Id"), "added", actor.Id, context.Request.RequestId);
        return Ok("Participant added.", new Dictionary<string, object> { ["participantId"] = Rest0173String(participant, "Id"), ["item"] = Rest0173ParticipantPayload(participant, admin: true, viewer: null) });
    }

    public ResponseEnvelope RestAdminRemoveParticipant0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var participant = Rest0173RequireParticipant(context.Request.Payload, includeArchived: true);
        var before = participant.DeepClone().AsBsonDocument;
        participant["IsArchived"] = true;
        participant["ParticipationStatus"] = "DidNotRest";
        participant["EligibleForRecovery"] = false;
        Rest0173Touch(participant, actor.Id);
        RestParticipants0173().ReplaceOne(Rest0173IdFilter(Rest0173String(participant, "Id")), participant);
        Rest0173Audit(actor, CommandNames.RestAdminRemoveParticipant, Rest0173String(participant, "RestSessionId"), Rest0173String(participant, "Id"), string.Empty, "rest.participant.removed", before, participant, "Participant removed.");
        Rest0173Sync("rest.participant.updated", "rest_participant", Rest0173String(participant, "Id"), "removed", actor.Id, context.Request.RequestId);
        return Ok("Participant removed.", new Dictionary<string, object> { ["item"] = Rest0173ParticipantPayload(participant, admin: true, viewer: null) });
    }

    public ResponseEnvelope RestAdminSetParticipantStatus0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var participant = Rest0173RequireParticipant(context.Request.Payload);
        var before = participant.DeepClone().AsBsonDocument;
        var status = Rest0173NormalizeOneOf(Rest0173Text(context.Request.Payload, "participationStatus", Rest0173String(participant, "ParticipationStatus"), 64), Rest0173ParticipantStatuses, "Planned");
        participant["ParticipationStatus"] = status;
        if (status == "ActingSeparately" || status == "DidNotRest")
        {
            participant["EligibleForRecovery"] = false;
            participant["RecoveryResult"] = "None";
        }
        if (context.Request.Payload.ContainsKey("eligibleForRecovery")) participant["EligibleForRecovery"] = PayloadReader.GetBool(context.Request.Payload, "eligibleForRecovery");
        participant["PlayerVisibleStatus"] = Rest0173Text(context.Request.Payload, "playerVisibleStatus", Rest0173String(participant, "PlayerVisibleStatus"), 512);
        Rest0173Touch(participant, actor.Id);
        RestParticipants0173().ReplaceOne(Rest0173IdFilter(Rest0173String(participant, "Id")), participant);
        Rest0173Audit(actor, CommandNames.RestAdminSetParticipantStatus, Rest0173String(participant, "RestSessionId"), Rest0173String(participant, "Id"), string.Empty, "rest.participant.status", before, participant, "Participant status updated.");
        Rest0173Sync("rest.participant.updated", "rest_participant", Rest0173String(participant, "Id"), "status", actor.Id, context.Request.RequestId);
        return Ok("Participant status updated.", new Dictionary<string, object> { ["item"] = Rest0173ParticipantPayload(participant, admin: true, viewer: null) });
    }

    public ResponseEnvelope RestAdminStart0173(CommandContext context) => Rest0173SetSessionStatus(context, "Active", CommandNames.RestAdminStart, "rest.started");
    public ResponseEnvelope RestAdminCancel0173(CommandContext context) => Rest0173SetSessionStatus(context, "Cancelled", CommandNames.RestAdminCancel, "rest.cancelled");

    public ResponseEnvelope RestAdminSetDisturbance0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureRest0173Indexes();
        var payload = context.Request.Payload;
        var rest = Rest0173RequireSession(payload);
        if (Rest0173String(rest, "Status") is "Completed" or "Interrupted" or "Cancelled" or "Archived")
            return Error("Disturbance can be changed only before rest is resolved.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var before = rest.DeepClone().AsBsonDocument;
        rest["DisturbanceMode"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "disturbanceMode", "None", 64), Rest0173DisturbanceModes, "None");
        rest["RecoveryImpact"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "recoveryImpact", "None", 64), Rest0173RecoveryImpacts, "None");
        rest["DisturbanceSummaryPlayer"] = Rest0173Text(payload, "disturbanceSummaryPlayer", Rest0173DefaultDisturbancePlayerSummary(Rest0173String(rest, "DisturbanceMode")), 1024);
        rest["DisturbanceGmNotes"] = Rest0173Text(payload, "disturbanceGmNotes", string.Empty, 2048);
        rest["DisturbanceUpdatedAtUtc"] = DateTime.UtcNow;
        rest["DisturbanceUpdatedByUserId"] = actor.Id;
        Rest0173Touch(rest, actor.Id);
        RestSessions0173().ReplaceOne(Rest0173IdFilter(Rest0173String(rest, "Id")), rest);
        Rest0173Audit(actor, CommandNames.RestAdminSetDisturbance, Rest0173String(rest, "Id"), string.Empty, string.Empty, "rest.disturbance.updated", before, rest, "Rest disturbance updated.");
        Rest0173Sync("rest.disturbance.updated", "rest_session", Rest0173String(rest, "Id"), "disturbance_updated", actor.Id, context.Request.RequestId);
        return Ok("Rest disturbance updated.", Rest0173Envelope(rest, admin: true, viewer: null));
    }

    public ResponseEnvelope RestAdminComplete0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var rest = Rest0173RequireSession(context.Request.Payload);
        if (!Rest0173String(rest, "Status").Equals("Active", StringComparison.OrdinalIgnoreCase))
            return Error("Only active rest can be completed.", ResponseStatus.Conflict, ErrorCode.Conflict);
        return Rest0173FinishRest(context, actor, rest, completed: true);
    }

    public ResponseEnvelope RestAdminInterrupt0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var rest = Rest0173RequireSession(context.Request.Payload);
        if (!Rest0173String(rest, "Status").Equals("Active", StringComparison.OrdinalIgnoreCase))
            return Error("Only active rest can be interrupted.", ResponseStatus.Conflict, ErrorCode.Conflict);
        return Rest0173FinishRest(context, actor, rest, completed: false);
    }

    public ResponseEnvelope RestAdminCreateDowntimeAction0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        return Rest0173CreateDowntimeAction(context, actor, admin: true);
    }

    public ResponseEnvelope RestAdminUpdateDowntimeAction0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var action = Rest0173RequireDowntimeAction(context.Request.Payload);
        var before = action.DeepClone().AsBsonDocument;
        var payload = context.Request.Payload;
        if (payload.ContainsKey("actionType")) action["ActionType"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "actionType", Rest0173String(action, "ActionType"), 64), Rest0173DowntimeTypes, "Custom");
        if (payload.ContainsKey("playerText")) action["PlayerText"] = Rest0173Text(payload, "playerText", Rest0173String(action, "PlayerText"), 2048);
        if (payload.ContainsKey("gmText")) action["GmText"] = Rest0173Text(payload, "gmText", Rest0173String(action, "GmText"), 2048);
        if (payload.ContainsKey("durationMinutes")) action["DurationMinutes"] = Math.Max(0, PayloadReader.GetInt(payload, "durationMinutes") ?? Rest0173Int(action, "DurationMinutes"));
        Rest0173Touch(action, actor.Id);
        DowntimeActions0173().ReplaceOne(Rest0173IdFilter(Rest0173String(action, "Id")), action);
        Rest0173Audit(actor, CommandNames.RestAdminUpdateDowntimeAction, Rest0173String(action, "RestSessionId"), string.Empty, Rest0173String(action, "Id"), "rest.downtime.updated", before, action, "Downtime action updated.");
        Rest0173Sync("rest.downtime.updated", "downtime_action", Rest0173String(action, "Id"), "updated", actor.Id, context.Request.RequestId);
        return Ok("Downtime action updated.", new Dictionary<string, object> { ["item"] = Rest0173DowntimePayload(action, admin: true, viewer: null) });
    }

    public ResponseEnvelope RestAdminApproveDowntimeAction0173(CommandContext context) => Rest0173SetDowntimeStatus(context, "Approved", CommandNames.RestAdminApproveDowntimeAction, "rest.downtime.approved");
    public ResponseEnvelope RestAdminRejectDowntimeAction0173(CommandContext context) => Rest0173SetDowntimeStatus(context, "Rejected", CommandNames.RestAdminRejectDowntimeAction, "rest.downtime.rejected");
    public ResponseEnvelope RestAdminCompleteDowntimeAction0173(CommandContext context) => Rest0173SetDowntimeStatus(context, "Completed", CommandNames.RestAdminCompleteDowntimeAction, "rest.downtime.completed");

    public ResponseEnvelope RestAdminCreateRecoveryGrant0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var rest = Rest0173RequireSession(context.Request.Payload, includeArchived: true);
        var grant = Rest0173CreateRecoveryGrant(actor, rest, context.Request.Payload, Rest0173Text(context.Request.Payload, "grantType", "Custom", 64), "PendingGmApply", false);
        Rest0173Sync("rest.recovery.grant.created", "recovery_grant", Rest0173String(grant, "Id"), "created", actor.Id, context.Request.RequestId);
        return Ok("Recovery grant created.", new Dictionary<string, object> { ["grantId"] = Rest0173String(grant, "Id"), ["item"] = Rest0173GrantPayload(grant, admin: true, viewer: null) });
    }

    public ResponseEnvelope RestAdminApplyRecoveryGrant0173(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var grant = Rest0173RequireRecoveryGrant(context.Request.Payload);
        if (Rest0173String(grant, "Status").Equals("Applied", StringComparison.OrdinalIgnoreCase))
            return Ok("Recovery grant already applied.", new Dictionary<string, object> { ["item"] = Rest0173GrantPayload(grant, admin: true, viewer: null), ["idempotent"] = true });
        var before = grant.DeepClone().AsBsonDocument;
        grant["Status"] = "Applied";
        grant["AppliedAtUtc"] = DateTime.UtcNow;
        grant["AppliedByUserId"] = actor.Id;
        grant["AppliedEffects"] = Rest0173Text(context.Request.Payload, "appliedEffects", "GM подтвердил восстановление вручную.", 2048);
        Rest0173Touch(grant, actor.Id);
        RecoveryGrants0173().ReplaceOne(Rest0173IdFilter(Rest0173String(grant, "Id")), grant);
        Rest0173Audit(actor, CommandNames.RestAdminApplyRecoveryGrant, Rest0173String(grant, "RestSessionId"), string.Empty, Rest0173String(grant, "Id"), "rest.recovery.grant.applied", before, grant, "Recovery grant applied by GM.");
        Rest0173Sync("rest.recovery.grant.applied", "recovery_grant", Rest0173String(grant, "Id"), "applied", actor.Id, context.Request.RequestId);
        return Ok("Recovery grant applied.", new Dictionary<string, object> { ["item"] = Rest0173GrantPayload(grant, admin: true, viewer: null) });
    }

    public ResponseEnvelope RestAdminGetAudit0173(CommandContext context)
    {
        RequireAdmin(context);
        var restId = Rest0173Text(context.Request.Payload, "restId", Rest0173Text(context.Request.Payload, "entityId", string.Empty, 128), 128);
        var filter = string.IsNullOrWhiteSpace(restId)
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.Eq("RestSessionId", restId);
        var items = RestAuditEvents0173().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("CreatedAtUtc")).Limit(200).ToList()
            .Select(x => (object)Rest0173AuditPayload(x)).ToArray();
        return Ok("Rest audit loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope RestPlayerGetActiveForSession0173(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureRest0173Indexes();
        var campaignId = Rest0173CampaignId(context.Request.Payload);
        var sessionId = Rest0173Text(context.Request.Payload, "sessionId", "default", 128);
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.Eq("SessionId", sessionId)
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var sessions = RestSessions0173().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(50).ToList()
            .Where(x => Rest0173CanPlayerSeeRest(x, actor))
            .Select(x => (object)Rest0173SessionPayload(x, admin: false)).ToArray();
        var current = sessions.FirstOrDefault();
        return Ok("Player rest status loaded.", new Dictionary<string, object> { ["items"] = sessions, ["current"] = current ?? new Dictionary<string, object>() });
    }

    public ResponseEnvelope RestPlayerGetMyRestStatus0173(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureRest0173Indexes();
        var restId = Rest0173Text(context.Request.Payload, "restId", string.Empty, 128);
        BsonDocument? rest = null;
        if (!string.IsNullOrWhiteSpace(restId))
        {
            rest = RestSessions0173().Find(Rest0173IdFilter(restId)).FirstOrDefault();
        }
        else
        {
            var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", Rest0173CampaignId(context.Request.Payload))
                & Builders<BsonDocument>.Filter.Eq("SessionId", Rest0173Text(context.Request.Payload, "sessionId", "default", 128))
                & Builders<BsonDocument>.Filter.Eq("Status", "Active")
                & Builders<BsonDocument>.Filter.Ne("IsArchived", true);
            rest = RestSessions0173().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).FirstOrDefault();
        }
        if (rest == null || !Rest0173CanPlayerSeeRest(rest, actor)) return Error("Rest session not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Player rest session loaded.", Rest0173Envelope(rest, admin: false, viewer: actor));
    }

    public ResponseEnvelope RestPlayerListMyDowntimeActions0173(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureRest0173Indexes();
        var filter = Builders<BsonDocument>.Filter.Eq("PlayerUserId", actor.Id) & Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var restId = Rest0173Text(context.Request.Payload, "restId", string.Empty, 128);
        if (!string.IsNullOrWhiteSpace(restId)) filter &= Builders<BsonDocument>.Filter.Eq("RestSessionId", restId);
        var items = DowntimeActions0173().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(100).ToList()
            .Select(x => (object)Rest0173DowntimePayload(x, admin: false, viewer: actor)).ToArray();
        return Ok("Player downtime actions loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope RestPlayerSubmitDowntimeAction0173(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        return Rest0173CreateDowntimeAction(context, actor, admin: false);
    }

    public ResponseEnvelope RestPlayerGetRecoveryGrants0173(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureRest0173Indexes();
        var filter = Builders<BsonDocument>.Filter.Eq("PlayerUserId", actor.Id) & Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var characterId = Rest0173Text(context.Request.Payload, "characterId", string.Empty, 128);
        if (!string.IsNullOrWhiteSpace(characterId)) filter &= Builders<BsonDocument>.Filter.Eq("CharacterId", characterId);
        var items = RecoveryGrants0173().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(100).ToList()
            .Select(x => (object)Rest0173GrantPayload(x, admin: false, viewer: actor)).ToArray();
        return Ok("Player recovery grants loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    private ResponseEnvelope Rest0173SetSessionStatus(CommandContext context, string status, string command, string auditAction)
    {
        var actor = RequireAdmin(context);
        var rest = Rest0173RequireSession(context.Request.Payload);
        var before = rest.DeepClone().AsBsonDocument;
        var current = Rest0173String(rest, "Status");
        if (status == "Active" && !(current is "Draft" or "Planned"))
            return Error("Only draft/planned rest can be started.", ResponseStatus.Conflict, ErrorCode.Conflict);
        if (status == "Cancelled" && current is "Completed" or "Interrupted" or "Archived")
            return Error("Completed/interrupted/archived rest cannot be cancelled.", ResponseStatus.Conflict, ErrorCode.Conflict);
        rest["Status"] = status;
        if (status == "Active")
        {
            rest["StartWorldTime"] = Rest0173NowWorldTime();
            rest["PlayerVisibleSummary"] = Rest0173String(rest, "RestType") == "LongRest" ? "Длинный отдых начат." : "Короткий отдых начат.";
            foreach (var participant in Rest0173Participants(Rest0173String(rest, "Id")))
            {
                participant["ParticipationStatus"] = "Resting";
                participant["LocalStartWorldTime"] = Rest0173String(rest, "StartWorldTime");
                Rest0173Touch(participant, actor.Id);
                RestParticipants0173().ReplaceOne(Rest0173IdFilter(Rest0173String(participant, "Id")), participant);
            }
        }
        else if (status == "Cancelled")
        {
            rest["PlayerVisibleSummary"] = "Отдых отменён мастером.";
        }
        Rest0173Touch(rest, actor.Id);
        RestSessions0173().ReplaceOne(Rest0173IdFilter(Rest0173String(rest, "Id")), rest);
        Rest0173Audit(actor, command, Rest0173String(rest, "Id"), string.Empty, string.Empty, auditAction, before, rest, $"Rest set to {status}.");
        Rest0173Sync(auditAction, "rest_session", Rest0173String(rest, "Id"), status.ToLowerInvariant(), actor.Id, context.Request.RequestId);
        return Ok($"Rest set to {status}.", Rest0173Envelope(rest, admin: true, viewer: null));
    }

    private ResponseEnvelope Rest0173FinishRest(CommandContext context, UserAccount actor, BsonDocument rest, bool completed)
    {
        var before = rest.DeepClone().AsBsonDocument;
        var actual = PayloadReader.GetInt(context.Request.Payload, "actualDurationMinutes") ?? Rest0173Int(rest, "PlannedDurationMinutes");
        var minimum = Rest0173Int(rest, "MinimumDurationMinutes");
        rest["Status"] = completed ? "Completed" : "Interrupted";
        rest["ActualDurationMinutes"] = Math.Max(0, actual);
        rest["ActualEndWorldTime"] = Rest0173NowWorldTime();
        rest["InterruptedReason"] = completed ? string.Empty : Rest0173Text(context.Request.Payload, "interruptedReason", "Отдых прерван мастером.", 1024);
        var baseEnough = completed && actual >= minimum;
        var disturbanceMode = Rest0173String(rest, "DisturbanceMode", "None");
        var recoveryImpact = Rest0173String(rest, "RecoveryImpact", "None");
        var disturbed = !disturbanceMode.Equals("None", StringComparison.OrdinalIgnoreCase) || !recoveryImpact.Equals("None", StringComparison.OrdinalIgnoreCase);
        var noRecoveryByDisturbance = recoveryImpact is "NoRecovery" or "NoMagicRecovery" or "NoHealthRecovery";
        var partialRecoveryByDisturbance = recoveryImpact is "PartialHealth" or "PartialMagicResources" or "Custom";
        var effectiveRecovery = baseEnough && !noRecoveryByDisturbance;
        rest["PlayerVisibleSummary"] = completed
            ? (Rest0173String(rest, "RestType") == "LongRest" ? "Длинный отдых завершён. Восстановление ожидает подтверждения мастера." : "Короткий отдых завершён. Восстановление ожидает подтверждения мастера.")
            : "Отдых был прерван. Восстановление не получено или требует решения мастера.";
        if (disturbed)
            rest["PlayerVisibleSummary"] = Rest0173DisturbancePlayerSummary(rest, effectiveRecovery, partialRecoveryByDisturbance);
        Rest0173Touch(rest, actor.Id);
        RestSessions0173().ReplaceOne(Rest0173IdFilter(Rest0173String(rest, "Id")), rest);

        var participants = Rest0173Participants(Rest0173String(rest, "Id"));
        var grants = new List<object>();
        foreach (var participant in participants)
        {
            var pBefore = participant.DeepClone().AsBsonDocument;
            var status = completed ? "Completed" : "Interrupted";
            participant["ParticipationStatus"] = Rest0173String(participant, "ParticipationStatus") == "ActingSeparately" ? "ActingSeparately" : status;
            participant["LocalEndWorldTime"] = Rest0173String(rest, "ActualEndWorldTime");
            participant["RestMinutesCompleted"] = Math.Max(0, actual);
            var eligible = Rest0173Bool(participant, "EligibleForRecovery") && Rest0173String(participant, "ParticipationStatus") != "ActingSeparately";
            var participantEffectiveRecovery = eligible && effectiveRecovery;
            var participantPartialRecovery = eligible && partialRecoveryByDisturbance && baseEnough && !noRecoveryByDisturbance;
            var enough = participantEffectiveRecovery;
            participant["RecoveryResult"] = eligible ? (participantEffectiveRecovery ? (participantPartialRecovery ? "Partial" : "Full") : (completed ? "PendingGmDecision" : "None")) : "None";
            participant["PlayerVisibleStatus"] = eligible
                ? (enough ? "Отдых завершён. Восстановление ожидает подтверждения мастера." : "Восстановление требует решения мастера.")
                : "Участник не получает восстановление по этому отдыху.";
            if (eligible && disturbed)
                participant["PlayerVisibleStatus"] = Rest0173DisturbancePlayerSummary(rest, participantEffectiveRecovery, participantPartialRecovery);
            Rest0173Touch(participant, actor.Id);
            RestParticipants0173().ReplaceOne(Rest0173IdFilter(Rest0173String(participant, "Id")), participant);
            Rest0173Audit(actor, CommandNames.RestAdminSetParticipantStatus, Rest0173String(rest, "Id"), Rest0173String(participant, "Id"), string.Empty, "rest.participant.updated", pBefore, participant, "Participant recovery eligibility updated.");

            if (!Rest0173String(participant, "ParticipantKind").Equals("Npc", StringComparison.OrdinalIgnoreCase))
            {
                var grantType = !participantEffectiveRecovery ? "NoRecovery" : participantPartialRecovery ? "PartialRecovery" : (Rest0173String(rest, "RestType") == "LongRest" ? "LongRestRecovery" : "ShortRestRecovery");
                var grant = Rest0173CreateRecoveryGrant(actor, rest, participant, grantType, "PendingGmApply", participantEffectiveRecovery);
                grants.Add(Rest0173GrantPayload(grant, admin: true, viewer: null));
            }
        }

        var command = completed ? CommandNames.RestAdminComplete : CommandNames.RestAdminInterrupt;
        var action = completed ? "rest.completed" : "rest.interrupted";
        Rest0173Audit(actor, command, Rest0173String(rest, "Id"), string.Empty, string.Empty, action, before, rest, completed ? "Rest completed." : "Rest interrupted.");
        Rest0173Sync(action, "rest_session", Rest0173String(rest, "Id"), completed ? "completed" : "interrupted", actor.Id, context.Request.RequestId);
        return Ok(completed ? "Rest completed." : "Rest interrupted.", new Dictionary<string, object>(Rest0173Envelope(rest, admin: true, viewer: null))
        {
            ["grantsCreated"] = grants.ToArray()
        });
    }

    private ResponseEnvelope Rest0173CreateDowntimeAction(CommandContext context, UserAccount actor, bool admin)
    {
        EnsureRest0173Indexes();
        var payload = context.Request.Payload;
        var rest = Rest0173RequireSession(payload);
        if (!admin && !Rest0173CanPlayerSeeRest(rest, actor)) return Error("Rest session not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var characterId = Rest0173Text(payload, "characterId", string.Empty, 128);
        if (!admin && !string.IsNullOrWhiteSpace(characterId) && !Rest0173IsOwnedOrControlledBy(actor.Id, characterId))
            return Error("Character is not available for this player.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var now = DateTime.UtcNow;
        var action = new BsonDocument
        {
            ["Id"] = Rest0173NewId("downtime"),
            ["WorldId"] = Rest0173String(rest, "WorldId"),
            ["CampaignId"] = Rest0173String(rest, "CampaignId"),
            ["SessionId"] = Rest0173String(rest, "SessionId"),
            ["RestSessionId"] = Rest0173String(rest, "Id"),
            ["CharacterId"] = characterId,
            ["PlayerUserId"] = admin ? Rest0173Text(payload, "playerUserId", string.Empty, 128) : actor.Id,
            ["ActionType"] = Rest0173NormalizeOneOf(Rest0173Text(payload, "actionType", "Watch", 64), Rest0173DowntimeTypes, "Custom"),
            ["Status"] = admin ? Rest0173NormalizeOneOf(Rest0173Text(payload, "status", "Approved", 64), Rest0173DowntimeStatuses, "Approved") : "Submitted",
            ["PlayerText"] = Rest0173Text(payload, "playerText", Rest0173Text(payload, "text", "Downtime action", 2048), 2048),
            ["GmText"] = admin ? Rest0173Text(payload, "gmText", string.Empty, 2048) : string.Empty,
            ["ServerOnlyData"] = new BsonDocument { ["scope"] = "downtime_0_17_3" },
            ["DurationMinutes"] = Math.Max(0, PayloadReader.GetInt(payload, "durationMinutes") ?? 60),
            ["StartWorldTime"] = Rest0173String(rest, "StartWorldTime"),
            ["EndWorldTime"] = string.Empty,
            ["TimeCostApplied"] = false,
            ["ResultPlayerVisible"] = Rest0173Text(payload, "resultPlayerVisible", string.Empty, 2048),
            ["ResultGm"] = admin ? Rest0173Text(payload, "resultGm", string.Empty, 2048) : string.Empty,
            ["RequiresGmApproval"] = !admin || PayloadReader.GetBool(payload, "requiresGmApproval"),
            ["SchemaVersion"] = "0.17.3",
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["UpdatedByUserId"] = actor.Id,
            ["Revision"] = 1
        };
        DowntimeActions0173().InsertOne(action);
        Rest0173Audit(actor, admin ? CommandNames.RestAdminCreateDowntimeAction : CommandNames.RestPlayerSubmitDowntimeAction, Rest0173String(rest, "Id"), string.Empty, Rest0173String(action, "Id"), "rest.downtime.submitted", null, action, admin ? "Downtime action created by GM." : "Downtime action submitted by player.");
        Rest0173Sync("rest.downtime.submitted", "downtime_action", Rest0173String(action, "Id"), "created", actor.Id, context.Request.RequestId);
        return Ok(admin ? "Downtime action created." : "Downtime action submitted.", new Dictionary<string, object> { ["actionId"] = Rest0173String(action, "Id"), ["item"] = Rest0173DowntimePayload(action, admin, admin ? null : actor) });
    }

    private ResponseEnvelope Rest0173SetDowntimeStatus(CommandContext context, string status, string command, string auditAction)
    {
        var actor = RequireAdmin(context);
        var action = Rest0173RequireDowntimeAction(context.Request.Payload);
        var before = action.DeepClone().AsBsonDocument;
        action["Status"] = status;
        if (status == "Completed")
        {
            action["EndWorldTime"] = Rest0173NowWorldTime();
            action["TimeCostApplied"] = true;
            action["ResultPlayerVisible"] = Rest0173Text(context.Request.Payload, "resultPlayerVisible", Rest0173String(action, "ResultPlayerVisible", "Действие завершено."), 2048);
            action["ResultGm"] = Rest0173Text(context.Request.Payload, "resultGm", Rest0173String(action, "ResultGm"), 2048);
        }
        Rest0173Touch(action, actor.Id);
        DowntimeActions0173().ReplaceOne(Rest0173IdFilter(Rest0173String(action, "Id")), action);
        Rest0173Audit(actor, command, Rest0173String(action, "RestSessionId"), string.Empty, Rest0173String(action, "Id"), auditAction, before, action, $"Downtime action set to {status}.");
        Rest0173Sync("rest.downtime.updated", "downtime_action", Rest0173String(action, "Id"), status.ToLowerInvariant(), actor.Id, context.Request.RequestId);
        return Ok($"Downtime action set to {status}.", new Dictionary<string, object> { ["item"] = Rest0173DowntimePayload(action, admin: true, viewer: null) });
    }

    private BsonDocument Rest0173CreateRecoveryGrant(UserAccount actor, BsonDocument rest, BsonDocument source, string grantType, string status, bool effectiveRecovery)
    {
        var payload = source;
        var now = DateTime.UtcNow;
        var grant = new BsonDocument
        {
            ["Id"] = Rest0173NewId("recovery"),
            ["RestSessionId"] = Rest0173String(rest, "Id"),
            ["CampaignId"] = Rest0173String(rest, "CampaignId"),
            ["CharacterId"] = Rest0173String(payload, "CharacterId"),
            ["PlayerUserId"] = Rest0173String(payload, "PlayerUserId"),
            ["GrantType"] = Rest0173NormalizeOneOf(grantType, Rest0173GrantTypes, "Custom"),
            ["Status"] = Rest0173NormalizeOneOf(status, Rest0173GrantStatuses, "PendingGmApply"),
            ["RecoverySummaryPlayer"] = Rest0173DefaultGrantPlayerSummary(Rest0173String(rest, "RestType"), effectiveRecovery),
            ["RecoverySummaryGm"] = $"Duration={Rest0173Int(rest, "ActualDurationMinutes")}; Quality={Rest0173String(rest, "RestQuality")}; Safety={Rest0173String(rest, "RestLocationSafety")}",
            ["SuggestedEffects"] = effectiveRecovery ? "Manual GM-controlled recovery pending. No automatic HP/condition mutation in 0.17.3." : "No automatic recovery; GM may override manually.",
            ["DisturbanceMode"] = Rest0173String(rest, "DisturbanceMode", "None"),
            ["RecoveryImpact"] = Rest0173String(rest, "RecoveryImpact", "None"),
            ["RecoveryOverrideReasonGm"] = Rest0173String(rest, "DisturbanceGmNotes"),
            ["AppliedEffects"] = string.Empty,
            ["AppliedAtUtc"] = BsonNull.Value,
            ["AppliedByUserId"] = string.Empty,
            ["SchemaVersion"] = "0.17.3",
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["UpdatedByUserId"] = actor.Id,
            ["Revision"] = 1,
            ["ServerOnlyData"] = new BsonDocument { ["scope"] = "recovery_0_17_3" }
        };
        if (!Rest0173String(grant, "DisturbanceMode").Equals("None", StringComparison.OrdinalIgnoreCase) || !Rest0173String(grant, "RecoveryImpact").Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            grant["RecoverySummaryPlayer"] = Rest0173DisturbancePlayerSummary(rest, effectiveRecovery, Rest0173String(grant, "GrantType") == "PartialRecovery");
            grant["RecoverySummaryGm"] = $"{Rest0173String(grant, "RecoverySummaryGm")}; Disturbance={Rest0173String(grant, "DisturbanceMode")}; Impact={Rest0173String(grant, "RecoveryImpact")}; GM={Rest0173String(rest, "DisturbanceGmNotes")}";
            grant["SuggestedEffects"] = effectiveRecovery ? "Disturbed rest: GM must apply partial/custom recovery manually." : "Disturbed rest: no automatic recovery; GM may override manually.";
        }
        RecoveryGrants0173().InsertOne(grant);
        Rest0173Audit(actor, "rest.recovery.internal.create", Rest0173String(rest, "Id"), Rest0173String(payload, "Id"), Rest0173String(grant, "Id"), "rest.recovery.grant.created", null, grant, "Recovery grant created.");
        Rest0173Sync("rest.recovery.grant.created", "recovery_grant", Rest0173String(grant, "Id"), "created", actor.Id, string.Empty);
        return grant;
    }

    private BsonDocument Rest0173CreateRecoveryGrant(UserAccount actor, BsonDocument rest, IDictionary<string, object> payload, string grantType, string status, bool effectiveRecovery)
    {
        var pseudo = new BsonDocument
        {
            ["Id"] = Rest0173Text(payload, "participantId", string.Empty, 128),
            ["CharacterId"] = Rest0173Text(payload, "characterId", string.Empty, 128),
            ["PlayerUserId"] = Rest0173Text(payload, "playerUserId", string.Empty, 128)
        };
        var grant = Rest0173CreateRecoveryGrant(actor, rest, pseudo, grantType, status, effectiveRecovery);
        if (payload.ContainsKey("recoverySummaryPlayer")) grant["RecoverySummaryPlayer"] = Rest0173Text(payload, "recoverySummaryPlayer", Rest0173String(grant, "RecoverySummaryPlayer"), 2048);
        if (payload.ContainsKey("recoverySummaryGm")) grant["RecoverySummaryGm"] = Rest0173Text(payload, "recoverySummaryGm", Rest0173String(grant, "RecoverySummaryGm"), 2048);
        RecoveryGrants0173().ReplaceOne(Rest0173IdFilter(Rest0173String(grant, "Id")), grant);
        return grant;
    }

    private Dictionary<string, object> Rest0173Envelope(BsonDocument rest, bool admin, UserAccount? viewer)
    {
        var restId = Rest0173String(rest, "Id");
        var participants = Rest0173Participants(restId)
            .Where(x => admin || Rest0173CanPlayerSeeParticipant(x, rest, viewer))
            .Select(x => (object)Rest0173ParticipantPayload(x, admin, viewer)).ToArray();
        var actions = DowntimeActions0173().Find(Builders<BsonDocument>.Filter.Eq("RestSessionId", restId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true)).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(100).ToList()
            .Where(x => admin || Rest0173CanPlayerSeeDowntime(x, viewer))
            .Select(x => (object)Rest0173DowntimePayload(x, admin, viewer)).ToArray();
        var grants = RecoveryGrants0173().Find(Builders<BsonDocument>.Filter.Eq("RestSessionId", restId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true)).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(100).ToList()
            .Where(x => admin || Rest0173CanPlayerSeeGrant(x, viewer))
            .Select(x => (object)Rest0173GrantPayload(x, admin, viewer)).ToArray();
        return new Dictionary<string, object>
        {
            ["rest"] = Rest0173SessionPayload(rest, admin),
            ["participants"] = participants,
            ["downtimeActions"] = actions,
            ["recoveryGrants"] = grants
        };
    }

    private Dictionary<string, object> Rest0173SessionPayload(BsonDocument doc, bool admin)
    {
        var map = new Dictionary<string, object>
        {
            ["restId"] = Rest0173String(doc, "Id"),
            ["campaignId"] = Rest0173String(doc, "CampaignId"),
            ["sessionId"] = Rest0173String(doc, "SessionId"),
            ["restType"] = Rest0173String(doc, "RestType"),
            ["status"] = Rest0173String(doc, "Status"),
            ["visibility"] = admin ? Rest0173String(doc, "Visibility") : "PlayerVisible",
            ["startWorldTime"] = Rest0173String(doc, "StartWorldTime"),
            ["plannedEndWorldTime"] = Rest0173String(doc, "PlannedEndWorldTime"),
            ["actualEndWorldTime"] = Rest0173String(doc, "ActualEndWorldTime"),
            ["plannedDurationMinutes"] = Rest0173Int(doc, "PlannedDurationMinutes"),
            ["actualDurationMinutes"] = Rest0173Int(doc, "ActualDurationMinutes"),
            ["minimumDurationMinutes"] = Rest0173Int(doc, "MinimumDurationMinutes"),
            ["maximumRecommendedDurationMinutes"] = Rest0173Int(doc, "MaximumRecommendedDurationMinutes"),
            ["restQuality"] = Rest0173String(doc, "RestQuality"),
            ["restLocationSafety"] = Rest0173String(doc, "RestLocationSafety"),
            ["playerVisibleSummary"] = Rest0173String(doc, "PlayerVisibleSummary"),
            ["disturbanceSummaryPlayer"] = Rest0173String(doc, "DisturbanceSummaryPlayer"),
            ["updatedAtUtc"] = Rest0173Date(doc, "UpdatedAtUtc")
        };
        if (admin)
        {
            map["worldId"] = Rest0173String(doc, "WorldId");
            map["sceneId"] = Rest0173String(doc, "SceneId");
            map["locationId"] = Rest0173String(doc, "LocationId");
            map["groupId"] = Rest0173String(doc, "GroupId");
            map["interruptedReason"] = Rest0173String(doc, "InterruptedReason");
            map["disturbanceMode"] = Rest0173String(doc, "DisturbanceMode", "None");
            map["recoveryImpact"] = Rest0173String(doc, "RecoveryImpact", "None");
            map["disturbanceGmNotes"] = Rest0173String(doc, "DisturbanceGmNotes");
            map["gmNotes"] = Rest0173String(doc, "GmNotes");
            map["isArchived"] = Rest0173Bool(doc, "IsArchived");
            map["revision"] = Rest0173Int(doc, "Revision", 1);
        }
        return map;
    }

    private Dictionary<string, object> Rest0173ParticipantPayload(BsonDocument doc, bool admin, UserAccount? viewer)
    {
        var map = new Dictionary<string, object>
        {
            ["participantId"] = Rest0173String(doc, "Id"),
            ["restId"] = Rest0173String(doc, "RestSessionId"),
            ["characterId"] = Rest0173String(doc, "CharacterId"),
            ["participantKind"] = Rest0173String(doc, "ParticipantKind"),
            ["displayName"] = Rest0173String(doc, "DisplayName"),
            ["participationStatus"] = Rest0173String(doc, "ParticipationStatus"),
            ["restMinutesCompleted"] = Rest0173Int(doc, "RestMinutesCompleted"),
            ["eligibleForRecovery"] = Rest0173Bool(doc, "EligibleForRecovery"),
            ["recoveryResult"] = Rest0173String(doc, "RecoveryResult"),
            ["playerVisibleStatus"] = Rest0173String(doc, "PlayerVisibleStatus")
        };
        if (admin)
        {
            map["playerUserId"] = Rest0173String(doc, "PlayerUserId");
            map["localStartWorldTime"] = Rest0173String(doc, "LocalStartWorldTime");
            map["localEndWorldTime"] = Rest0173String(doc, "LocalEndWorldTime");
            map["isPlayerVisible"] = Rest0173Bool(doc, "IsPlayerVisible", true);
            map["gmNotes"] = Rest0173String(doc, "GmNotes");
        }
        else if (viewer != null && !string.IsNullOrWhiteSpace(Rest0173String(doc, "PlayerUserId")) && Rest0173String(doc, "PlayerUserId") != viewer.Id)
        {
            map["characterId"] = string.Empty;
        }
        return map;
    }

    private Dictionary<string, object> Rest0173DowntimePayload(BsonDocument doc, bool admin, UserAccount? viewer)
    {
        var map = new Dictionary<string, object>
        {
            ["actionId"] = Rest0173String(doc, "Id"),
            ["restId"] = Rest0173String(doc, "RestSessionId"),
            ["characterId"] = Rest0173String(doc, "CharacterId"),
            ["actionType"] = Rest0173String(doc, "ActionType"),
            ["status"] = Rest0173String(doc, "Status"),
            ["playerText"] = Rest0173String(doc, "PlayerText"),
            ["durationMinutes"] = Rest0173Int(doc, "DurationMinutes"),
            ["resultPlayerVisible"] = Rest0173String(doc, "ResultPlayerVisible"),
            ["requiresGmApproval"] = Rest0173Bool(doc, "RequiresGmApproval"),
            ["updatedAtUtc"] = Rest0173Date(doc, "UpdatedAtUtc")
        };
        if (admin)
        {
            map["playerUserId"] = Rest0173String(doc, "PlayerUserId");
            map["gmText"] = Rest0173String(doc, "GmText");
            map["resultGm"] = Rest0173String(doc, "ResultGm");
            map["timeCostApplied"] = Rest0173Bool(doc, "TimeCostApplied");
        }
        return map;
    }

    private Dictionary<string, object> Rest0173GrantPayload(BsonDocument doc, bool admin, UserAccount? viewer)
    {
        var map = new Dictionary<string, object>
        {
            ["grantId"] = Rest0173String(doc, "Id"),
            ["restId"] = Rest0173String(doc, "RestSessionId"),
            ["characterId"] = Rest0173String(doc, "CharacterId"),
            ["grantType"] = Rest0173String(doc, "GrantType"),
            ["status"] = Rest0173String(doc, "Status"),
            ["recoverySummaryPlayer"] = Rest0173String(doc, "RecoverySummaryPlayer"),
            ["disturbanceMode"] = admin ? Rest0173String(doc, "DisturbanceMode", "None") : Rest0173PlayerSafeDisturbanceMode(Rest0173String(doc, "DisturbanceMode", "None")),
            ["updatedAtUtc"] = Rest0173Date(doc, "UpdatedAtUtc")
        };
        if (admin)
        {
            map["playerUserId"] = Rest0173String(doc, "PlayerUserId");
            map["recoverySummaryGm"] = Rest0173String(doc, "RecoverySummaryGm");
            map["suggestedEffects"] = Rest0173String(doc, "SuggestedEffects");
            map["recoveryImpact"] = Rest0173String(doc, "RecoveryImpact", "None");
            map["recoveryOverrideReasonGm"] = Rest0173String(doc, "RecoveryOverrideReasonGm");
            map["appliedEffects"] = Rest0173String(doc, "AppliedEffects");
            map["appliedAtUtc"] = Rest0173Date(doc, "AppliedAtUtc");
        }
        return map;
    }

    private Dictionary<string, object> Rest0173AuditPayload(BsonDocument doc) => new()
    {
        ["auditId"] = Rest0173String(doc, "Id"),
        ["restId"] = Rest0173String(doc, "RestSessionId"),
        ["actorUserId"] = Rest0173String(doc, "ActorUserId"),
        ["actorLogin"] = Rest0173String(doc, "ActorLogin"),
        ["actorRole"] = Rest0173String(doc, "ActorRole"),
        ["command"] = Rest0173String(doc, "Command"),
        ["action"] = Rest0173String(doc, "Action"),
        ["summary"] = Rest0173String(doc, "Summary"),
        ["createdAtUtc"] = Rest0173Date(doc, "CreatedAtUtc")
    };

    private bool Rest0173CanPlayerSeeRest(BsonDocument rest, UserAccount viewer)
    {
        if (Rest0173Bool(rest, "IsArchived")) return false;
        var visibility = Rest0173String(rest, "Visibility", "PlayerVisible");
        if (visibility is "GmOnly" or "Hidden") return false;
        if (visibility == "AssignedParticipantsOnly")
        {
            return Rest0173Participants(Rest0173String(rest, "Id")).Any(x => Rest0173String(x, "PlayerUserId") == viewer.Id || Rest0173IsOwnedOrControlledBy(viewer.Id, Rest0173String(x, "CharacterId")));
        }
        return visibility is "PlayerVisible" or "PartyVisible" or "AssignedParticipantsOnly";
    }

    private bool Rest0173CanPlayerSeeParticipant(BsonDocument participant, BsonDocument rest, UserAccount? viewer)
    {
        if (viewer == null || Rest0173Bool(participant, "IsArchived")) return false;
        if (!Rest0173Bool(participant, "IsPlayerVisible", true)) return false;
        if (Rest0173String(rest, "Visibility") == "AssignedParticipantsOnly")
            return Rest0173String(participant, "PlayerUserId") == viewer.Id || Rest0173IsOwnedOrControlledBy(viewer.Id, Rest0173String(participant, "CharacterId"));
        if (Rest0173String(participant, "ParticipantKind") == "Npc" && string.IsNullOrWhiteSpace(Rest0173String(participant, "PlayerVisibleStatus"))) return false;
        return true;
    }

    private bool Rest0173CanPlayerSeeDowntime(BsonDocument action, UserAccount? viewer) => viewer != null && Rest0173String(action, "PlayerUserId") == viewer.Id && !Rest0173Bool(action, "IsArchived");
    private bool Rest0173CanPlayerSeeGrant(BsonDocument grant, UserAccount? viewer) => viewer != null && Rest0173String(grant, "PlayerUserId") == viewer.Id && !Rest0173Bool(grant, "IsArchived");

    private bool Rest0173IsOwnedOrControlledBy(string userId, string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) return true;
        var ownership = _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        if (ownership != null)
            return string.Equals(ownership.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase) || string.Equals(ownership.ControlledByUserId, userId, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    private void Rest0173Audit(UserAccount actor, string command, string restId, string participantId, string downtimeOrGrantId, string action, BsonDocument? before, BsonDocument? after, string summary)
    {
        var audit = new BsonDocument
        {
            ["Id"] = Rest0173NewId("rest_audit"),
            ["RestSessionId"] = restId,
            ["ParticipantId"] = participantId,
            ["DowntimeOrGrantId"] = downtimeOrGrantId,
            ["ActorUserId"] = actor.Id,
            ["ActorLogin"] = actor.Login,
            ["ActorRole"] = IsAdminActor(actor) ? "Admin" : "Player",
            ["Command"] = command,
            ["Action"] = action,
            ["Summary"] = summary,
            ["Before"] = before == null ? BsonNull.Value : Rest0173SafeAuditDoc(before),
            ["After"] = after == null ? BsonNull.Value : Rest0173SafeAuditDoc(after),
            ["CreatedAtUtc"] = DateTime.UtcNow
        };
        RestAuditEvents0173().InsertOne(audit);
        WriteAudit("rests", actor.Id, action, restId);
    }

    private void Rest0173Sync(string type, string entityType, string entityId, string operation, string actorUserId, string? requestId)
    {
        TryPublishSyncEvent(type, "rests", entityType, entityId, operation, actorUserId, new Dictionary<string, object>
        {
            ["entityType"] = entityType,
            ["entityId"] = entityId,
            ["operation"] = operation
        }, requestId ?? string.Empty);
    }

    private void EnsureRest0173Indexes()
    {
        if (_rest0173IndexesEnsured) return;
        RestSessions0173().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId").Ascending("SessionId").Ascending("Status")));
        RestSessions0173().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId").Ascending("IsArchived")));
        RestParticipants0173().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("RestSessionId")));
        RestParticipants0173().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("PlayerUserId")));
        DowntimeActions0173().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("RestSessionId").Ascending("Status")));
        DowntimeActions0173().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("PlayerUserId").Descending("UpdatedAtUtc")));
        RecoveryGrants0173().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("RestSessionId").Ascending("Status")));
        RecoveryGrants0173().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("PlayerUserId").Descending("UpdatedAtUtc")));
        RestAuditEvents0173().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("RestSessionId").Descending("CreatedAtUtc")));
        _rest0173IndexesEnsured = true;
    }

    private BsonDocument Rest0173RequireSession(IDictionary<string, object> payload, bool includeArchived = false)
    {
        var id = Rest0173RequiredId(payload, "restId");
        var doc = RestSessions0173().Find(Rest0173IdFilter(id)).FirstOrDefault();
        if (doc == null || (!includeArchived && Rest0173Bool(doc, "IsArchived"))) throw new KeyNotFoundException("Rest session not found.");
        return doc;
    }

    private BsonDocument Rest0173RequireParticipant(IDictionary<string, object> payload, bool includeArchived = false)
    {
        var id = Rest0173RequiredId(payload, "participantId");
        var doc = RestParticipants0173().Find(Rest0173IdFilter(id)).FirstOrDefault();
        if (doc == null || (!includeArchived && Rest0173Bool(doc, "IsArchived"))) throw new KeyNotFoundException("Rest participant not found.");
        return doc;
    }

    private BsonDocument Rest0173RequireDowntimeAction(IDictionary<string, object> payload)
    {
        var id = Rest0173RequiredId(payload, "actionId");
        var doc = DowntimeActions0173().Find(Rest0173IdFilter(id)).FirstOrDefault();
        if (doc == null || Rest0173Bool(doc, "IsArchived")) throw new KeyNotFoundException("Downtime action not found.");
        return doc;
    }

    private BsonDocument Rest0173RequireRecoveryGrant(IDictionary<string, object> payload)
    {
        var id = Rest0173RequiredId(payload, "grantId");
        var doc = RecoveryGrants0173().Find(Rest0173IdFilter(id)).FirstOrDefault();
        if (doc == null || Rest0173Bool(doc, "IsArchived")) throw new KeyNotFoundException("Recovery grant not found.");
        return doc;
    }

    private List<BsonDocument> Rest0173Participants(string restId) => RestParticipants0173().Find(Builders<BsonDocument>.Filter.Eq("RestSessionId", restId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true)).ToList();
    private IMongoCollection<BsonDocument> RestSessions0173() => _mongo.Database.GetCollection<BsonDocument>(RestSessions0173Collection);
    private IMongoCollection<BsonDocument> RestParticipants0173() => _mongo.Database.GetCollection<BsonDocument>(RestParticipants0173Collection);
    private IMongoCollection<BsonDocument> DowntimeActions0173() => _mongo.Database.GetCollection<BsonDocument>(DowntimeActions0173Collection);
    private IMongoCollection<BsonDocument> RecoveryGrants0173() => _mongo.Database.GetCollection<BsonDocument>(RecoveryGrants0173Collection);
    private IMongoCollection<BsonDocument> RestAuditEvents0173() => _mongo.Database.GetCollection<BsonDocument>(RestAuditEvents0173Collection);

    private static FilterDefinition<BsonDocument> Rest0173IdFilter(string id) => Builders<BsonDocument>.Filter.Eq("Id", id);
    private static string Rest0173NewId(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N");
    private static string Rest0173RequiredId(IDictionary<string, object> payload, string key) => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, key), PayloadReader.GetString(payload, "id")), 1, 128, key);
    private static string Rest0173CampaignId(IDictionary<string, object> payload) => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), "dev-campaign-core"), 1, 128, "campaignId");
    private static string Rest0173Text(IDictionary<string, object> payload, string key, string fallback, int max, bool required = false) => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, key), fallback), required ? 1 : 0, max, key);

    private static int Rest0173ValidateDuration(string restType, int? requested)
    {
        var value = requested ?? Rest0173DefaultDuration(restType);
        if (restType == "ShortRest" && (value < 60 || value > 180)) throw new ArgumentException("ShortRest duration must be in range 60..180 minutes.");
        if (restType == "LongRest" && (value < 360 || value > 720)) throw new ArgumentException("LongRest duration must be in range 360..720 minutes.");
        if (value <= 0 || value > 1440) throw new ArgumentException("Rest duration must be in range 1..1440 minutes.");
        return value;
    }

    private static int Rest0173DefaultDuration(string restType) => restType == "LongRest" ? 480 : restType == "ShortRest" ? 60 : 60;
    private static int Rest0173MinimumDuration(string restType) => restType == "LongRest" ? 360 : restType == "ShortRest" ? 60 : 1;
    private static int Rest0173MaximumDuration(string restType) => restType == "LongRest" ? 720 : restType == "ShortRest" ? 180 : 1440;
    private static string Rest0173NowWorldTime() => DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.GetCultureInfo("ru-RU"));

    private static string Rest0173String(BsonDocument doc, string key, string fallback = "")
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return fallback;
        if (doc[key].IsString) return doc[key].AsString;
        return Convert.ToString(BsonTypeMapper.MapToDotNetValue(doc[key]), CultureInfo.InvariantCulture) ?? fallback;
    }

    private static int Rest0173Int(BsonDocument doc, string key, int fallback = 0)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return fallback;
        if (doc[key].IsInt32) return doc[key].AsInt32;
        if (doc[key].IsInt64) return (int)doc[key].AsInt64;
        return int.TryParse(Rest0173String(doc, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static bool Rest0173Bool(BsonDocument doc, string key, bool fallback = false)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return fallback;
        if (doc[key].IsBoolean) return doc[key].AsBoolean;
        return bool.TryParse(Rest0173String(doc, key), out var value) ? value : fallback;
    }

    private static string Rest0173Date(BsonDocument doc, string key)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return string.Empty;
        if (doc[key].IsValidDateTime) return doc[key].ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        return Rest0173String(doc, key);
    }

    private static string Rest0173NormalizeOneOf(string value, string[] allowed, string fallback) => allowed.FirstOrDefault(x => x.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? fallback;
    private static void Rest0173Touch(BsonDocument doc, string actorId)
    {
        doc["UpdatedAtUtc"] = DateTime.UtcNow;
        doc["UpdatedByUserId"] = actorId;
        doc["Revision"] = Rest0173Int(doc, "Revision") + 1;
    }

    private static BsonDocument Rest0173SafeAuditDoc(BsonDocument source)
    {
        var clone = source.DeepClone().AsBsonDocument;
        clone.Remove("ServerOnlyData");
        clone.Remove("GmNotes");
        clone.Remove("GmText");
        clone.Remove("ResultGm");
        clone.Remove("RecoverySummaryGm");
        clone.Remove("DisturbanceGmNotes");
        clone.Remove("RecoveryOverrideReasonGm");
        return clone;
    }

    private static string Rest0173DefaultDisturbancePlayerSummary(string mode) => mode switch
    {
        "UneasySleep" => "Отдых был тревожным. Итог восстановления требует решения мастера.",
        "Nightmare" => "Сон был тяжёлым. Итог восстановления требует решения мастера.",
        "WatchedFeeling" => "Во время отдыха сохранялось ощущение чужого внимания.",
        "StrangeSignal" => "Во время отдыха был замечен странный сигнал.",
        "AnomalousSilence" => "Отдых прошёл в неестественной тишине.",
        "FateDisturbance" => "Отдых был нарушен вмешательством судьбы.",
        "CustomGmParanoia" => "Отдых был нарушен неизвестным фактором.",
        _ => string.Empty
    };

    private static string Rest0173DisturbancePlayerSummary(BsonDocument rest, bool effectiveRecovery, bool partialRecovery)
    {
        var explicitSummary = Rest0173String(rest, "DisturbanceSummaryPlayer");
        if (!string.IsNullOrWhiteSpace(explicitSummary)) return explicitSummary;
        if (!effectiveRecovery) return "Отдых был нарушен. Восстановление не получено или требует решения мастера.";
        if (partialRecovery) return "Отдых был тревожным. Восстановление будет применено частично по решению мастера.";
        return "Отдых был тревожным, но восстановление ожидает подтверждения мастера.";
    }

    private static string Rest0173PlayerSafeDisturbanceMode(string mode) => mode.Equals("None", StringComparison.OrdinalIgnoreCase) ? "None" : "Disturbed";

    private static string Rest0173DefaultGrantPlayerSummary(string restType, bool effectiveRecovery)
    {
        if (!effectiveRecovery) return "Отдых был прерван. Восстановление не получено или требует решения мастера.";
        return restType == "LongRest"
            ? "Персонаж завершил длинный отдых. Восстановление ожидает подтверждения мастера."
            : "Персонаж завершил короткий отдых. Восстановление ожидает подтверждения мастера.";
    }

    private static readonly string[] Rest0173RestTypes = { "ShortRest", "LongRest", "CustomRest" };
    private static readonly string[] Rest0173Statuses = { "Draft", "Planned", "Active", "Completed", "Interrupted", "Cancelled", "Archived" };
    private static readonly string[] Rest0173VisibilityModes = { "GmOnly", "PlayerVisible", "PartyVisible", "AssignedParticipantsOnly", "Hidden" };
    private static readonly string[] Rest0173Qualities = { "Poor", "Normal", "Good", "Excellent" };
    private static readonly string[] Rest0173LocationSafety = { "Unsafe", "Risky", "Normal", "Safe" };
    private static readonly string[] Rest0173ParticipantKinds = { "PlayerCharacter", "Companion", "Npc", "Custom" };
    private static readonly string[] Rest0173ParticipantStatuses = { "Planned", "Resting", "Completed", "Interrupted", "DidNotRest", "ActingSeparately" };
    private static readonly string[] Rest0173DowntimeTypes = { "Watch", "Repair", "TreatWounds", "Study", "CraftPrep", "Shop", "Social", "Scout", "Personal", "Custom" };
    private static readonly string[] Rest0173DowntimeStatuses = { "Draft", "Submitted", "Approved", "Rejected", "Completed", "Cancelled" };
    private static readonly string[] Rest0173GrantTypes = { "ShortRestRecovery", "LongRestRecovery", "PartialRecovery", "NoRecovery", "Custom" };
    private static readonly string[] Rest0173GrantStatuses = { "PendingGmApply", "Applied", "PartiallyApplied", "Rejected", "Cancelled" };
    private static readonly string[] Rest0173DisturbanceModes = { "None", "UneasySleep", "Nightmare", "WatchedFeeling", "StrangeSignal", "AnomalousSilence", "FateDisturbance", "CustomGmParanoia" };
    private static readonly string[] Rest0173RecoveryImpacts = { "None", "PartialHealth", "PartialMagicResources", "NoMagicRecovery", "NoHealthRecovery", "NoRecovery", "Custom" };
}
