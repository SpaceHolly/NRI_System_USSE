using System;
using System.Collections;
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
    private const string Combat0166SessionsCollection = "combat_sessions_0166";
    private const string Combat0166ParticipantsCollection = "combat_participants_0166";
    private const string Combat0166EventsCollection = "combat_turn_events_0166";
    private const int Combat0166RoundDurationSeconds = 5;
    private const string Combat0166DefaultCampaignId = "dev-campaign-core";
    private const string Combat0166DefaultSessionId = "dev-session-core";

    public ResponseEnvelope CombatAdminListForSession0166(CommandContext context)
    {
        RequireAdmin(context);
        if (!Combat0166AdminReadEnabled()) return Combat0166Disabled(context.Request.Command);
        EnsureCombat0166Indexes();

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "campaignId"), Combat0166DefaultCampaignId);
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), Combat0166DefaultSessionId);
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.Eq("SessionId", sessionId);
        if (!includeArchived) filter &= Builders<BsonDocument>.Filter.Ne("IsArchived", true);

        var items = Combat0166Sessions()
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(100)
            .ToList()
            .Select(doc => Combat0166ListPayload(doc))
            .Cast<object>()
            .ToArray();

        return Ok("Combat trackers loaded.", new Dictionary<string, object> { ["items"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope CombatAdminGet0166(CommandContext context)
    {
        RequireAdmin(context);
        if (!Combat0166AdminReadEnabled()) return Combat0166Disabled(context.Request.Command);
        EnsureCombat0166Indexes();

        var combat = Combat0166RequireSession(context.Request.Payload);
        return Ok("Combat tracker loaded.", Combat0166AdminPayload(combat, includeLog: true));
    }

    public ResponseEnvelope CombatAdminCreate0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled()) return Combat0166Disabled(context.Request.Command);
        EnsureCombat0166Indexes();

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var now = DateTime.UtcNow;
        var combatId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "combatId"), PayloadReader.GetString(payload, "id"), Guid.NewGuid().ToString("N"));
        var name = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "name"), PayloadReader.GetString(payload, "displayName"), "Бой"), 1, 160, "name");
        var campaignId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "campaignId"), Combat0166DefaultCampaignId);
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), Combat0166DefaultSessionId);
        var suppliedSceneMapId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sceneMapId"), PayloadReader.GetString(payload, "mapId"));
        string sceneMapId;
        if (string.IsNullOrWhiteSpace(suppliedSceneMapId))
        {
            sceneMapId = string.Empty;
        }
        else
        {
            var mapIdentity = _mapIdentityResolver.ResolveSceneMap(suppliedSceneMapId);
            if (!mapIdentity.IsResolved) return MapIdentityError0202(mapIdentity);
            sceneMapId = mapIdentity.CanonicalMapId;
        }
        var existing = Combat0166Sessions().Find(Combat0166AnyIdFilter(combatId)).FirstOrDefault();
        var doc = existing == null ? new BsonDocument { ["_id"] = combatId, ["Id"] = combatId, ["CreatedAtUtc"] = now, ["CreatedByUserId"] = actor.Id } : new BsonDocument(existing);
        doc["CampaignId"] = campaignId;
        doc["SessionId"] = sessionId;
        doc["SceneMapId"] = string.IsNullOrWhiteSpace(sceneMapId) && existing != null
            ? Combat0166String(existing, "SceneMapId")
            : sceneMapId;
        doc["ActiveGroupId"] = PayloadReader.GetString(payload, "activeGroupId") ?? string.Empty;
        doc["SceneId"] = PayloadReader.GetString(payload, "sceneId") ?? string.Empty;
        doc["DisplayName"] = name;
        doc["Description"] = PayloadReader.GetString(payload, "description") ?? string.Empty;
        doc["Status"] = "setup";
        doc["RoundNumber"] = 0;
        doc["CurrentTurnIndex"] = -1;
        doc["CurrentParticipantId"] = string.Empty;
        doc["RoundDurationSeconds"] = Combat0166RoundDurationSeconds;
        doc["InitiativeDie"] = "d20";
        doc["InitiativeModifierMode"] = "none";
        doc["InitiativeOrder"] = new BsonArray();
        doc["PreRoundQueue"] = new BsonArray();
        doc["PreRoundTurnIndex"] = -1;
        doc["VisibilityMode"] = NormalizeCombat0166Visibility(PayloadReader.GetString(payload, "visibilityMode"), "player_visible");
        doc["IsArchived"] = false;
        doc["UpdatedAtUtc"] = now;
        doc["UpdatedByUserId"] = actor.Id;
        doc["Revision"] = Combat0166Int(doc, "Revision", 0) + 1;
        doc["ServerOnlyData"] = new BsonDocument { ["source"] = "combat_tracker_0166" };

        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(combatId), doc, new ReplaceOptions { IsUpsert = true });
        AppendCombat0166Event(doc, "combat.created", $"{actor.Login} создал бой.", $"createdBy={actor.Id}", actor.Id, "gm_only", string.Empty);
        PublishCombat0166Sync(doc, "combat.created", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Combat tracker created.", Combat0166AdminPayload(doc, includeLog: true));
    }

    public ResponseEnvelope CombatAdminUpdate0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled()) return Combat0166Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var combat = Combat0166RequireSession(payload);
        if (payload.ContainsKey("name") || payload.ContainsKey("displayName"))
            combat["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "name"), PayloadReader.GetString(payload, "displayName"), Combat0166String(combat, "DisplayName", "Бой")), 1, 160, "name");
        if (payload.ContainsKey("description"))
            combat["Description"] = RequireLength(PayloadReader.GetString(payload, "description") ?? string.Empty, 0, 4096, "description");
        if (payload.ContainsKey("visibilityMode"))
            combat["VisibilityMode"] = NormalizeCombat0166Visibility(PayloadReader.GetString(payload, "visibilityMode"), Combat0166String(combat, "VisibilityMode", "player_visible"));
        Combat0166TouchSession(combat, actor.Id);
        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(combat, "Id")), combat);
        AppendCombat0166Event(combat, "combat.updated", "Параметры боя обновлены.", "combat updated", actor.Id, "gm_only", string.Empty);
        PublishCombat0166Sync(combat, "combat.updated", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Combat tracker updated.", Combat0166AdminPayload(combat, includeLog: true));
    }

    public ResponseEnvelope CombatAdminArchive0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled()) return Combat0166Disabled(context.Request.Command);
        var combat = Combat0166RequireSession(context.Request.Payload);
        combat["IsArchived"] = true;
        combat["Status"] = "archived";
        Combat0166TouchSession(combat, actor.Id);
        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(combat, "Id")), combat);
        AppendCombat0166Event(combat, "combat.archived", "Бой перенесен в архив.", "combat archived", actor.Id, "gm_only", string.Empty);
        PublishCombat0166Sync(combat, "combat.archived", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Combat tracker archived.", Combat0166AdminPayload(combat, includeLog: true));
    }

    public ResponseEnvelope CombatAdminAddParticipant0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled()) return Combat0166Disabled(context.Request.Command);
        EnsureCombat0166Indexes();

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var combat = Combat0166RequireSession(payload);
        var now = DateTime.UtcNow;
        var participantId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "participantId"), PayloadReader.GetString(payload, "id"), Guid.NewGuid().ToString("N"));
        var existing = Combat0166Participants().Find(Combat0166AnyIdFilter(participantId)).FirstOrDefault();
        var doc = existing == null ? new BsonDocument { ["_id"] = participantId, ["Id"] = participantId, ["CreatedAtUtc"] = now, ["CreatedByUserId"] = actor.Id } : new BsonDocument(existing);
        doc["CombatId"] = Combat0166String(combat, "Id");
        doc["CampaignId"] = Combat0166String(combat, "CampaignId");
        doc["SessionId"] = Combat0166String(combat, "SessionId");
        doc["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), existing == null ? "Участник" : Combat0166String(existing, "DisplayName")), 1, 160, "displayName");
        doc["ParticipantType"] = NormalizeCombat0166ParticipantType(PayloadReader.GetString(payload, "participantType"));
        doc["TeamId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "teamId"), existing == null ? "neutral" : Combat0166String(existing, "TeamId", "neutral"));
        doc["CharacterId"] = PayloadReader.GetString(payload, "characterId") ?? (existing == null ? string.Empty : Combat0166String(existing, "CharacterId"));
        doc["ControllerUserId"] = PayloadReader.GetString(payload, "controllerUserId") ?? (existing == null ? string.Empty : Combat0166String(existing, "ControllerUserId"));
        doc["IsPlayerControlled"] = PayloadReader.GetBool(payload, "isPlayerControlled") || !string.IsNullOrWhiteSpace(Combat0166String(doc, "ControllerUserId"));
        doc["VisibilityMode"] = NormalizeCombat0166Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "visibilityMode"), PayloadReader.GetString(payload, "visibility")), existing == null ? "hidden" : Combat0166String(existing, "VisibilityMode", "hidden"));
        doc["IsPlayerVisible"] = string.Equals(Combat0166String(doc, "VisibilityMode"), "player_visible", StringComparison.OrdinalIgnoreCase) || PayloadReader.GetBool(payload, "isPlayerVisible");
        doc["InitiativeRoll"] = PayloadReader.GetInt(payload, "initiativeRoll") ?? PayloadReader.GetInt(payload, "initiative") ?? Combat0166Int(existing ?? new BsonDocument(), "InitiativeRoll", 0);
        doc["InitiativeTieBreakRolls"] = existing != null ? existing.GetValue("InitiativeTieBreakRolls", new BsonArray()) : new BsonArray();
        doc["InitiativeOrderIndex"] = Combat0166Int(existing ?? new BsonDocument(), "InitiativeOrderIndex", 9999);
        doc["Natural20BonusTurn"] = Combat0166Bool(existing ?? new BsonDocument(), "Natural20BonusTurn");
        doc["Natural20BonusTurnUsed"] = Combat0166Bool(existing ?? new BsonDocument(), "Natural20BonusTurnUsed");
        doc["Natural1FirstTurnPenalty"] = Combat0166Bool(existing ?? new BsonDocument(), "Natural1FirstTurnPenalty");
        doc["Natural1PenaltyConsumed"] = Combat0166Bool(existing ?? new BsonDocument(), "Natural1PenaltyConsumed");
        doc["TurnStatus"] = "waiting";
        doc["StandardActions"] = 1;
        doc["MinorActions"] = 2;
        doc["ReactionSlots"] = 1;
        doc["ReactionUsedThisRound"] = false;
        doc["MapTokenId"] = PayloadReader.GetString(payload, "mapTokenId") ?? (existing == null ? string.Empty : Combat0166String(existing, "MapTokenId"));
        doc["MapTokenVisibility"] = NormalizeCombat0166Visibility(PayloadReader.GetString(payload, "mapTokenVisibility"), "hidden");
        doc["PublicStateText"] = PayloadReader.GetString(payload, "publicStateText") ?? string.Empty;
        doc["GmStateText"] = PayloadReader.GetString(payload, "gmStateText") ?? string.Empty;
        doc["PublicNotes"] = PayloadReader.GetString(payload, "publicNotes") ?? string.Empty;
        doc["GmNotes"] = PayloadReader.GetString(payload, "gmNotes") ?? string.Empty;
        doc["IsArchived"] = false;
        doc["UpdatedAtUtc"] = now;
        doc["UpdatedByUserId"] = actor.Id;
        doc["Revision"] = Combat0166Int(doc, "Revision", 0) + 1;
        doc["ServerOnlyData"] = new BsonDocument { ["source"] = "combat_tracker_0166" };

        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(participantId), doc, new ReplaceOptions { IsUpsert = true });
        AppendCombat0166Event(combat, "participant.added", $"{Combat0166String(doc, "DisplayName")} добавлен в бой.", $"participantId={participantId}", actor.Id, "gm_only", participantId);
        PublishCombat0166Sync(combat, "participant.added", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Participant added.", Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    public ResponseEnvelope CombatAdminUpdateParticipant0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled()) return Combat0166Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var participant = Combat0166RequireParticipant(payload);
        var combat = Combat0166RequireSessionById(Combat0166String(participant, "CombatId"));
        if (payload.ContainsKey("displayName") || payload.ContainsKey("name"))
            participant["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), Combat0166String(participant, "DisplayName", "Участник")), 1, 160, "displayName");
        if (payload.ContainsKey("participantType")) participant["ParticipantType"] = NormalizeCombat0166ParticipantType(PayloadReader.GetString(payload, "participantType"));
        if (payload.ContainsKey("teamId")) participant["TeamId"] = PayloadReader.GetString(payload, "teamId") ?? string.Empty;
        if (payload.ContainsKey("characterId")) participant["CharacterId"] = PayloadReader.GetString(payload, "characterId") ?? string.Empty;
        if (payload.ContainsKey("controllerUserId")) participant["ControllerUserId"] = PayloadReader.GetString(payload, "controllerUserId") ?? string.Empty;
        if (payload.ContainsKey("visibilityMode") || payload.ContainsKey("visibility") || payload.ContainsKey("isPlayerVisible"))
        {
            var visibility = NormalizeCombat0166Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "visibilityMode"), PayloadReader.GetString(payload, "visibility"), PayloadReader.GetBool(payload, "isPlayerVisible") ? "player_visible" : Combat0166String(participant, "VisibilityMode", "hidden")), "hidden");
            participant["VisibilityMode"] = visibility;
            participant["IsPlayerVisible"] = string.Equals(visibility, "player_visible", StringComparison.OrdinalIgnoreCase);
        }
        if (payload.ContainsKey("publicStateText")) participant["PublicStateText"] = PayloadReader.GetString(payload, "publicStateText") ?? string.Empty;
        if (payload.ContainsKey("gmStateText")) participant["GmStateText"] = PayloadReader.GetString(payload, "gmStateText") ?? string.Empty;
        if (payload.ContainsKey("publicNotes")) participant["PublicNotes"] = PayloadReader.GetString(payload, "publicNotes") ?? string.Empty;
        if (payload.ContainsKey("gmNotes")) participant["GmNotes"] = PayloadReader.GetString(payload, "gmNotes") ?? string.Empty;
        Combat0166TouchParticipant(participant, actor.Id);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(participant, "Id")), participant);
        AppendCombat0166Event(combat, "participant.updated", $"{Combat0166String(participant, "DisplayName")} обновлен.", $"participantId={Combat0166String(participant, "Id")}", actor.Id, "gm_only", Combat0166String(participant, "Id"));
        return Ok("Participant updated.", Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    public ResponseEnvelope CombatAdminRemoveParticipant0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled()) return Combat0166Disabled(context.Request.Command);
        var participant = Combat0166RequireParticipant(context.Request.Payload);
        var combat = Combat0166RequireSessionById(Combat0166String(participant, "CombatId"));
        participant["IsArchived"] = true;
        participant["TurnStatus"] = "removed";
        Combat0166TouchParticipant(participant, actor.Id);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(participant, "Id")), participant);
        RebuildCombat0166Order(combat);
        AppendCombat0166Event(combat, "participant.removed", $"{Combat0166String(participant, "DisplayName")} удален из порядка инициативы.", $"participantId={Combat0166String(participant, "Id")}", actor.Id, "gm_only", Combat0166String(participant, "Id"));
        return Ok("Participant removed.", Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    public ResponseEnvelope CombatAdminSetParticipantVisibility0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled()) return Combat0166Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var participant = Combat0166RequireParticipant(payload);
        var visibility = NormalizeCombat0166Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "visibilityMode"), PayloadReader.GetString(payload, "visibility")), "hidden");
        participant["VisibilityMode"] = visibility;
        participant["IsPlayerVisible"] = string.Equals(visibility, "player_visible", StringComparison.OrdinalIgnoreCase);
        Combat0166TouchParticipant(participant, actor.Id);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(participant, "Id")), participant);
        var combat = Combat0166RequireSessionById(Combat0166String(participant, "CombatId"));
        AppendCombat0166Event(combat, "participant.visibility", "Видимость участника обновлена.", $"participantId={Combat0166String(participant, "Id")} visibility={visibility}", actor.Id, "gm_only", Combat0166String(participant, "Id"));
        return Ok("Participant visibility updated.", Combat0166AdminPayload(combat, includeLog: true));
    }

    public ResponseEnvelope CombatAdminLinkMapToken0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled() || !_featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatMapTokenLinks))) return Combat0166Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var participant = Combat0166RequireParticipant(payload);
        participant["MapTokenId"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapTokenId"), PayloadReader.GetString(payload, "tokenId")), 1, 128, "mapTokenId");
        participant["MapTokenDisplayName"] = PayloadReader.GetString(payload, "mapTokenDisplayName") ?? PayloadReader.GetString(payload, "tokenName") ?? string.Empty;
        participant["MapTokenVisibility"] = NormalizeCombat0166Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapTokenVisibility"), PayloadReader.GetString(payload, "visibilityMode")), "hidden");
        Combat0166TouchParticipant(participant, actor.Id);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(participant, "Id")), participant);
        var combat = Combat0166RequireSessionById(Combat0166String(participant, "CombatId"));
        AppendCombat0166Event(combat, "participant.map_token.linked", "Токен карты привязан к участнику.", $"participantId={Combat0166String(participant, "Id")} token={Combat0166String(participant, "MapTokenId")}", actor.Id, "gm_only", Combat0166String(participant, "Id"));
        return Ok("Map token linked.", Combat0166AdminPayload(combat, includeLog: true));
    }

    public ResponseEnvelope CombatAdminUnlinkMapToken0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled() || !_featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatMapTokenLinks))) return Combat0166Disabled(context.Request.Command);
        var participant = Combat0166RequireParticipant(context.Request.Payload);
        participant["MapTokenId"] = string.Empty;
        participant["MapTokenDisplayName"] = string.Empty;
        participant["MapTokenVisibility"] = "hidden";
        Combat0166TouchParticipant(participant, actor.Id);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(participant, "Id")), participant);
        var combat = Combat0166RequireSessionById(Combat0166String(participant, "CombatId"));
        AppendCombat0166Event(combat, "participant.map_token.unlinked", "Токен карты отвязан от участника.", $"participantId={Combat0166String(participant, "Id")}", actor.Id, "gm_only", Combat0166String(participant, "Id"));
        return Ok("Map token unlinked.", Combat0166AdminPayload(combat, includeLog: true));
    }

    public ResponseEnvelope CombatAdminRollInitiative0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166InitiativeEnabled()) return Combat0166Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var combat = Combat0166RequireSession(payload);
        var participants = Combat0166ParticipantDocs(Combat0166String(combat, "Id"), includeArchived: false).ToList();
        if (participants.Count == 0) return Error("Add at least one participant before rolling initiative.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var rng = new Random();
        var forcedRolls = PayloadReader.GetDictionary(payload, "forcedRolls") ?? new Dictionary<string, object>();
        var forcedTieBreaks = PayloadReader.GetDictionary(payload, "forcedTieBreakRolls") ?? new Dictionary<string, object>();
        foreach (var participant in participants)
        {
            var id = Combat0166String(participant, "Id");
            var roll = Combat0166ForcedInt(forcedRolls, id) ?? rng.Next(1, 21);
            roll = Math.Max(1, Math.Min(20, roll));
            participant["InitiativeRoll"] = roll;
            participant["InitiativeTieBreakRolls"] = new BsonArray();
            participant["Natural20BonusTurn"] = roll == 20;
            participant["Natural20BonusTurnUsed"] = false;
            participant["Natural1FirstTurnPenalty"] = roll == 1;
            participant["Natural1PenaltyConsumed"] = false;
            participant["ReactionUsedThisRound"] = false;
        }

        var groups = participants.GroupBy(x => Combat0166Int(x, "InitiativeRoll", 0)).Where(g => g.Count() > 1).ToList();
        foreach (var group in groups)
        {
            var used = new HashSet<int>();
            foreach (var participant in group)
            {
                var id = Combat0166String(participant, "Id");
                var tiebreak = Combat0166ForcedInt(forcedTieBreaks, id) ?? rng.Next(1, 21);
                var guard = 0;
                while (used.Contains(tiebreak) && guard++ < 100)
                    tiebreak = rng.Next(1, 21);
                used.Add(tiebreak);
                participant["InitiativeTieBreakRolls"] = new BsonArray { tiebreak };
            }
        }

        var ordered = participants
            .OrderByDescending(x => Combat0166Int(x, "InitiativeRoll", 0))
            .ThenByDescending(x => Combat0166FirstTieBreak(x))
            .ThenBy(x => Combat0166String(x, "DisplayName"), StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i]["InitiativeOrderIndex"] = i;
            ordered[i]["TurnStatus"] = "waiting";
            Combat0166TouchParticipant(ordered[i], actor.Id);
            Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(ordered[i], "Id")), ordered[i]);
        }

        combat["InitiativeOrder"] = new BsonArray(ordered.Select(x => Combat0166String(x, "Id")));
        combat["PreRoundQueue"] = new BsonArray(ordered.Where(x => Combat0166Bool(x, "Natural20BonusTurn")).Select(x => Combat0166String(x, "Id")));
        combat["PreRoundTurnIndex"] = -1;
        combat["RoundNumber"] = 0;
        combat["CurrentTurnIndex"] = -1;
        combat["CurrentParticipantId"] = string.Empty;
        Combat0166TouchSession(combat, actor.Id);
        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(combat, "Id")), combat);
        AppendCombat0166Event(combat, "initiative.rolled", "Инициатива d20 определена. Ничьи переброшены только между связанными участниками.", "initiative d20 no modifiers", actor.Id, "public", string.Empty);
        PublishCombat0166Sync(combat, "initiative.rolled", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Initiative rolled.", Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    public ResponseEnvelope CombatAdminRerollTie0166(CommandContext context) => CombatAdminRollInitiative0166(context);

    public ResponseEnvelope CombatAdminSetInitiativeOrder0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166InitiativeEnabled()) return Combat0166Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var combat = Combat0166RequireSession(payload);
        var ids = Combat0166StringArray(PayloadReader.GetDictionary(payload, "order") ?? PayloadReader.GetDictionary(payload, "participantIds"));
        if (ids.Count == 0 && payload.TryGetValue("participantIds", out var raw)) ids = Combat0166EnumerableStrings(raw);
        if (ids.Count == 0) return Error("participantIds are required.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var participants = Combat0166ParticipantDocs(Combat0166String(combat, "Id"), includeArchived: false).ToDictionary(x => Combat0166String(x, "Id"), StringComparer.OrdinalIgnoreCase);
        var ordered = ids.Where(participants.ContainsKey).Select(id => participants[id]).ToList();
        if (ordered.Count != participants.Count) return Error("Initiative order must include every active participant.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i]["InitiativeOrderIndex"] = i;
            Combat0166TouchParticipant(ordered[i], actor.Id);
            Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(ordered[i], "Id")), ordered[i]);
        }
        combat["InitiativeOrder"] = new BsonArray(ordered.Select(x => Combat0166String(x, "Id")));
        combat["PreRoundQueue"] = new BsonArray(ordered.Where(x => Combat0166Bool(x, "Natural20BonusTurn")).Select(x => Combat0166String(x, "Id")));
        Combat0166TouchSession(combat, actor.Id);
        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(combat, "Id")), combat);
        AppendCombat0166Event(combat, "initiative.order.set", "Порядок инициативы задан вручную.", "manual initiative order", actor.Id, "gm_only", string.Empty);
        return Ok("Initiative order set.", Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    public ResponseEnvelope CombatAdminStart0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166TurnEngineEnabled()) return Combat0166Disabled(context.Request.Command);
        var combat = Combat0166RequireSession(context.Request.Payload);
        if (!Combat0166OrderIds(combat).Any()) RebuildCombat0166Order(combat);
        var first = Combat0166SelectFirstTurn(combat, actor.Id);
        if (string.IsNullOrWhiteSpace(first)) return Error("No participant is available for turn order.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        combat["Status"] = "active";
        Combat0166TouchSession(combat, actor.Id);
        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(combat, "Id")), combat);
        AppendCombat0166Event(combat, "combat.started", "Бой начат.", "combat started", actor.Id, "public", first);
        PublishCombat0166Sync(combat, "combat.started", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Combat started.", Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    public ResponseEnvelope CombatAdminPause0166(CommandContext context) => Combat0166SetStatus(context, "paused", "Combat paused.", "Бой поставлен на паузу.", "combat.paused");
    public ResponseEnvelope CombatAdminResume0166(CommandContext context) => Combat0166SetStatus(context, "active", "Combat resumed.", "Бой продолжен.", "combat.resumed");

    public ResponseEnvelope CombatAdminNextTurn0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166TurnEngineEnabled()) return Combat0166Disabled(context.Request.Command);
        var combat = Combat0166RequireSession(context.Request.Payload);
        var current = Combat0166String(combat, "CurrentParticipantId");
        if (!string.IsNullOrWhiteSpace(current))
            Combat0166MarkTurnCompleted(combat, current, actor.Id, skipped: false);
        var next = Combat0166AdvanceToNextTurn(combat, actor.Id);
        if (string.IsNullOrWhiteSpace(next)) return Error("No next turn is available.", ResponseStatus.Conflict, ErrorCode.Conflict);
        AppendCombat0166Event(combat, "turn.next", "Переход к следующему ходу.", $"current={next}", actor.Id, "public", next);
        PublishCombat0166Sync(combat, "turn.next", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Moved to next turn.", Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    public ResponseEnvelope CombatAdminSkipTurn0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166TurnEngineEnabled()) return Combat0166Disabled(context.Request.Command);
        var combat = Combat0166RequireSession(context.Request.Payload);
        var current = FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload ?? new Dictionary<string, object>(), "participantId"), Combat0166String(combat, "CurrentParticipantId"));
        if (string.IsNullOrWhiteSpace(current)) return Error("No current participant to skip.", ResponseStatus.Conflict, ErrorCode.Conflict);
        Combat0166MarkTurnCompleted(combat, current, actor.Id, skipped: true);
        var next = Combat0166AdvanceToNextTurn(combat, actor.Id);
        AppendCombat0166Event(combat, "turn.skipped", "Ход пропущен.", $"skipped={current}", actor.Id, "public", current);
        return Ok("Turn skipped.", Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    public ResponseEnvelope CombatAdminPreviousTurn0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166TurnEngineEnabled()) return Combat0166Disabled(context.Request.Command);
        var combat = Combat0166RequireSession(context.Request.Payload);
        var order = Combat0166OrderIds(combat).ToList();
        if (order.Count == 0) return Error("Initiative order is empty.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var current = Combat0166Int(combat, "CurrentTurnIndex", 0);
        var previous = current <= 0 ? order.Count - 1 : current - 1;
        combat["RoundNumber"] = Math.Max(1, Combat0166Int(combat, "RoundNumber", 1));
        combat["CurrentTurnIndex"] = previous;
        combat["CurrentParticipantId"] = order[previous];
        Combat0166SetParticipantActive(order[previous], actor.Id);
        Combat0166TouchSession(combat, actor.Id);
        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(combat, "Id")), combat);
        AppendCombat0166Event(combat, "turn.previous", "GM вернул предыдущий ход.", $"participant={order[previous]}", actor.Id, "gm_only", order[previous]);
        return Ok("Moved to previous turn.", Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    public ResponseEnvelope CombatAdminEnd0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled()) return Combat0166Disabled(context.Request.Command);
        var combat = Combat0166RequireSession(context.Request.Payload);
        combat["Status"] = "ended";
        combat["EndedAtUtc"] = DateTime.UtcNow;
        Combat0166TouchSession(combat, actor.Id);
        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(combat, "Id")), combat);
        Combat0166Participants().UpdateMany(
            Builders<BsonDocument>.Filter.Eq("CombatId", Combat0166String(combat, "Id")),
            Builders<BsonDocument>.Update
                .Set("MapOverlayState", "none")
                .Set("MapBadgeText", string.Empty)
                .Set("TurnStatus", "completed")
                .Set("UpdatedAtUtc", DateTime.UtcNow)
                .Set("UpdatedByUserId", actor.Id));
        AppendCombat0166Event(combat, "combat.ended", "Бой завершен.", "combat ended", actor.Id, "public", string.Empty);
        PublishCombat0166Sync(combat, "combat.ended", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Combat ended.", Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    public ResponseEnvelope CombatAdminAddTurnEvent0166(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled()) return Combat0166Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var combat = Combat0166RequireSession(payload);
        var visibility = NormalizeCombat0166LogVisibility(PayloadReader.GetString(payload, "visibility"));
        var message = RequireLength(PayloadReader.GetString(payload, "message") ?? "Событие боя", 1, 1000, "message");
        AppendCombat0166Event(combat, FirstNonEmptyWorld(PayloadReader.GetString(payload, "eventType"), "turn.event"), visibility == "gm_only" ? string.Empty : message, message, actor.Id, visibility, PayloadReader.GetString(payload, "participantId") ?? string.Empty);
        return Ok("Combat event added.", Combat0166AdminPayload(combat, includeLog: true));
    }

    public ResponseEnvelope CombatAdminGetLog0166(CommandContext context)
    {
        RequireAdmin(context);
        if (!Combat0166AdminReadEnabled()) return Combat0166Disabled(context.Request.Command);
        var combat = Combat0166RequireSession(context.Request.Payload);
        return Ok("Combat log loaded.", new Dictionary<string, object>
        {
            ["combatId"] = Combat0166String(combat, "Id"),
            ["items"] = Combat0166LogPayloads(Combat0166String(combat, "Id"), admin: true).Cast<object>().ToArray()
        });
    }

    public ResponseEnvelope CombatPlayerGetActiveForSession0166(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!Combat0166PlayerReadEnabled()) return Combat0166Disabled(context.Request.Command);
        EnsureCombat0166Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "campaignId"), Combat0166DefaultCampaignId);
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), Combat0166DefaultSessionId);
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.Eq("SessionId", sessionId)
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true)
            & Builders<BsonDocument>.Filter.Eq("VisibilityMode", "player_visible")
            & Builders<BsonDocument>.Filter.In("Status", new[] { "setup", "active", "paused" });
        var combat = Combat0166Sessions().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).FirstOrDefault();
        if (combat == null)
            return Ok("No active combat tracker.", new Dictionary<string, object> { ["hasActiveCombat"] = false, ["warnings"] = new object[] { "GM еще не начал бой." } });
        return Ok("Active combat loaded.", Combat0166PlayerPayload(combat, actor.Id));
    }

    public ResponseEnvelope CombatPlayerGetMyTurnState0166(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!Combat0166PlayerReadEnabled()) return Combat0166Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var combatId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "combatId"), PayloadReader.GetString(payload, "id"));
        var combat = string.IsNullOrWhiteSpace(combatId) ? Combat0166FindActiveForPlayerPayload(payload) : Combat0166RequireSessionById(combatId);
        if (!Combat0166CanPlayerSeeCombat(combat))
            return Error("Combat tracker is not available.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var participant = Combat0166ParticipantDocs(Combat0166String(combat, "Id"), includeArchived: false)
            .FirstOrDefault(x => string.Equals(Combat0166String(x, "ControllerUserId"), actor.Id, StringComparison.OrdinalIgnoreCase) && Combat0166CanPlayerSeeParticipant(x));
        return Ok("My combat turn state loaded.", new Dictionary<string, object>
        {
            ["combatId"] = Combat0166String(combat, "Id"),
            ["hasParticipant"] = participant != null,
            ["myTurnState"] = participant == null ? new Dictionary<string, object>() : Combat0166ParticipantPayload(participant, admin: false, actorUserId: actor.Id)
        });
    }

    public ResponseEnvelope CombatPlayerGetLog0166(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!Combat0166PlayerReadEnabled()) return Combat0166Disabled(context.Request.Command);
        var combat = Combat0166RequireSession(context.Request.Payload);
        if (!Combat0166CanPlayerSeeCombat(combat))
            return Error("Combat tracker is not available.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Combat log loaded.", new Dictionary<string, object>
        {
            ["combatId"] = Combat0166String(combat, "Id"),
            ["items"] = Combat0166LogPayloads(Combat0166String(combat, "Id"), admin: false).Cast<object>().ToArray()
        });
    }

    private ResponseEnvelope Combat0166SetStatus(CommandContext context, string status, string message, string publicMessage, string eventType)
    {
        var actor = RequireAdmin(context);
        if (!Combat0166AdminWriteEnabled()) return Combat0166Disabled(context.Request.Command);
        var combat = Combat0166RequireSession(context.Request.Payload);
        combat["Status"] = status;
        Combat0166TouchSession(combat, actor.Id);
        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(combat, "Id")), combat);
        AppendCombat0166Event(combat, eventType, publicMessage, eventType, actor.Id, "public", string.Empty);
        return Ok(message, Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true));
    }

    private bool Combat0166AdminReadEnabled()
        => _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatTrackerMvp))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints));

    private bool Combat0166AdminWriteEnabled()
        => Combat0166AdminReadEnabled()
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));

    private bool Combat0166InitiativeEnabled()
        => Combat0166AdminWriteEnabled()
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatInitiativeOrder));

    private bool Combat0166TurnEngineEnabled()
        => Combat0166AdminWriteEnabled()
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatTurnEngine));

    private bool Combat0166PlayerReadEnabled()
        => _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatTrackerMvp))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatPlayerReadEndpoints))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatPlayerTrackerUi));

    private ResponseEnvelope Combat0166Disabled(string command)
    {
        _logger.Admin($"combat.tracker0166.disabled command={command}");
        return Error("Combat Tracker MVP is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private void EnsureCombat0166Indexes()
    {
        Combat0166Sessions().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId").Ascending("SessionId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Status")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        Combat0166Participants().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CombatId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("ControllerUserId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("VisibilityMode")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("MapTokenId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        Combat0166Events().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CombatId").Ascending("SequenceNumber")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Visibility")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CreatedAtUtc"))
        });
    }

    private IMongoCollection<BsonDocument> Combat0166Sessions() => _mongo.Database.GetCollection<BsonDocument>(Combat0166SessionsCollection);
    private IMongoCollection<BsonDocument> Combat0166Participants() => _mongo.Database.GetCollection<BsonDocument>(Combat0166ParticipantsCollection);
    private IMongoCollection<BsonDocument> Combat0166Events() => _mongo.Database.GetCollection<BsonDocument>(Combat0166EventsCollection);

    private BsonDocument Combat0166RequireSession(IDictionary<string, object>? payload)
    {
        var map = payload ?? new Dictionary<string, object>();
        var id = FirstNonEmptyWorld(PayloadReader.GetString(map, "combatId"), PayloadReader.GetString(map, "id"), PayloadReader.GetString(map, "encounterId"));
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("combatId is required.");
        return Combat0166RequireSessionById(id);
    }

    private BsonDocument Combat0166RequireSessionById(string combatId)
    {
        var doc = Combat0166Sessions().Find(Combat0166ActiveIdFilter(combatId)).FirstOrDefault();
        if (doc == null) throw new KeyNotFoundException("Combat tracker not found.");
        return doc;
    }

    private BsonDocument Combat0166FindActiveForPlayerPayload(IDictionary<string, object> payload)
    {
        var campaignId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "campaignId"), Combat0166DefaultCampaignId);
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), Combat0166DefaultSessionId);
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.Eq("SessionId", sessionId)
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true)
            & Builders<BsonDocument>.Filter.Eq("VisibilityMode", "player_visible")
            & Builders<BsonDocument>.Filter.In("Status", new[] { "setup", "active", "paused" });
        var doc = Combat0166Sessions().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).FirstOrDefault();
        if (doc == null) throw new KeyNotFoundException("Active combat tracker not found.");
        return doc;
    }

    private BsonDocument Combat0166RequireParticipant(IDictionary<string, object>? payload)
    {
        var map = payload ?? new Dictionary<string, object>();
        var id = FirstNonEmptyWorld(PayloadReader.GetString(map, "participantId"), PayloadReader.GetString(map, "id"));
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("participantId is required.");
        var doc = Combat0166Participants().Find(Combat0166ActiveIdFilter(id)).FirstOrDefault();
        if (doc == null) throw new KeyNotFoundException("Combat participant not found.");
        return doc;
    }

    private BsonDocument Combat0166ReloadSession(BsonDocument combat) => Combat0166RequireSessionById(Combat0166String(combat, "Id"));

    private IEnumerable<BsonDocument> Combat0166ParticipantDocs(string combatId, bool includeArchived)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("CombatId", combatId);
        if (!includeArchived) filter &= Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        return Combat0166Participants().Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("InitiativeOrderIndex").Ascending("DisplayName")).ToList();
    }

    private Dictionary<string, object> Combat0166ListPayload(BsonDocument combat)
    {
        var combatId = Combat0166String(combat, "Id");
        return new Dictionary<string, object>
        {
            ["combatId"] = combatId,
            ["id"] = combatId,
            ["name"] = Combat0166String(combat, "DisplayName", "Бой"),
            ["status"] = Combat0166String(combat, "Status", "setup"),
            ["campaignId"] = Combat0166String(combat, "CampaignId"),
            ["sessionId"] = Combat0166String(combat, "SessionId"),
            ["roundNumber"] = Combat0166Int(combat, "RoundNumber", 0),
            ["currentTurnIndex"] = Combat0166Int(combat, "CurrentTurnIndex", -1),
            ["currentParticipantId"] = Combat0166String(combat, "CurrentParticipantId"),
            ["participantCount"] = Combat0166ParticipantDocs(combatId, includeArchived: false).Count(),
            ["updatedAtUtc"] = Combat0166Date(combat, "UpdatedAtUtc"),
            ["isArchived"] = Combat0166Bool(combat, "IsArchived")
        };
    }

    private Dictionary<string, object> Combat0166AdminPayload(BsonDocument combat, bool includeLog)
    {
        var combatId = Combat0166String(combat, "Id");
        var participants = Combat0166ParticipantDocs(combatId, includeArchived: false).Select(x => Combat0166ParticipantPayload(x, admin: true, actorUserId: string.Empty)).Cast<object>().ToArray();
        return new Dictionary<string, object>
        {
            ["combat"] = Combat0166CombatPayload(combat, admin: true),
            ["combatId"] = combatId,
            ["participants"] = participants,
            ["initiativeOrder"] = participants.OrderBy(x => ((Dictionary<string, object>)x)["initiativeOrderIndex"]).ToArray(),
            ["logs"] = includeLog ? Combat0166LogPayloads(combatId, admin: true).Cast<object>().ToArray() : Array.Empty<object>(),
            ["sourceCollections"] = new object[] { Combat0166SessionsCollection, Combat0166ParticipantsCollection, Combat0166EventsCollection }
        };
    }

    private Dictionary<string, object> Combat0166PlayerPayload(BsonDocument combat, string actorUserId)
    {
        var combatId = Combat0166String(combat, "Id");
        var visibleParticipants = Combat0166ParticipantDocs(combatId, includeArchived: false)
            .Where(Combat0166CanPlayerSeeParticipant)
            .Select(x => Combat0166ParticipantPayload(x, admin: false, actorUserId: actorUserId))
            .Cast<object>()
            .ToArray();
        return new Dictionary<string, object>
        {
            ["hasActiveCombat"] = true,
            ["combat"] = Combat0166CombatPayload(combat, admin: false),
            ["participants"] = visibleParticipants,
            ["initiativeOrder"] = visibleParticipants,
            ["logs"] = Combat0166LogPayloads(combatId, admin: false).Cast<object>().ToArray(),
            ["myTurnState"] = visibleParticipants.Cast<Dictionary<string, object>>().FirstOrDefault(x =>
            {
                return x.TryGetValue("controllerUserId", out var controllerUserId)
                    && string.Equals(Convert.ToString(controllerUserId), actorUserId, StringComparison.OrdinalIgnoreCase);
            }) ?? new Dictionary<string, object>(),
            ["warnings"] = Array.Empty<object>(),
            ["builtAtUtc"] = DateTime.UtcNow
        };
    }

    private Dictionary<string, object> Combat0166CombatPayload(BsonDocument combat, bool admin)
    {
        var currentId = Combat0166String(combat, "CurrentParticipantId");
        var current = string.IsNullOrWhiteSpace(currentId) ? null : Combat0166Participants().Find(Combat0166AnyIdFilter(currentId)).FirstOrDefault();
        var currentVisible = current != null && (admin || Combat0166CanPlayerSeeParticipant(current));
        var payload = new Dictionary<string, object>
        {
            ["combatId"] = Combat0166String(combat, "Id"),
            ["id"] = Combat0166String(combat, "Id"),
            ["campaignId"] = Combat0166String(combat, "CampaignId"),
            ["sessionId"] = Combat0166String(combat, "SessionId"),
            ["name"] = Combat0166String(combat, "DisplayName", "Бой"),
            ["description"] = admin ? Combat0166String(combat, "Description") : string.Empty,
            ["status"] = Combat0166String(combat, "Status", "setup"),
            ["roundNumber"] = Combat0166Int(combat, "RoundNumber", 0),
            ["roundDurationSeconds"] = Combat0166RoundDurationSeconds,
            ["elapsedCombatSeconds"] = Math.Max(0, Combat0166Int(combat, "RoundNumber", 0) - 1) * Combat0166RoundDurationSeconds,
            ["currentTurnIndex"] = Combat0166Int(combat, "CurrentTurnIndex", -1),
            ["currentParticipantId"] = currentVisible ? currentId : string.Empty,
            ["currentParticipantName"] = currentVisible && current != null ? Combat0166String(current, "DisplayName", "Участник") : "Скрытый участник",
            ["initiativeDie"] = "d20",
            ["initiativeModifierMode"] = "none",
            ["updatedAtUtc"] = Combat0166Date(combat, "UpdatedAtUtc")
        };
        if (admin)
        {
            payload["serverDiagnostics"] = $"source={Combat0166SessionsCollection}";
            payload["visibilityMode"] = Combat0166String(combat, "VisibilityMode", "player_visible");
        }
        return payload;
    }

    private Dictionary<string, object> Combat0166ParticipantPayload(BsonDocument participant, bool admin, string actorUserId)
    {
        var participantId = Combat0166String(participant, "Id");
        var losesFullAction = Combat0166Bool(participant, "Natural1FirstTurnPenalty") && !Combat0166Bool(participant, "Natural1PenaltyConsumed");
        var payload = new Dictionary<string, object>
        {
            ["participantId"] = participantId,
            ["id"] = participantId,
            ["displayName"] = Combat0166String(participant, "DisplayName", "Участник"),
            ["participantType"] = Combat0166String(participant, "ParticipantType", "custom"),
            ["teamId"] = Combat0166String(participant, "TeamId", "neutral"),
            ["controllerUserId"] = admin ? Combat0166String(participant, "ControllerUserId") : (string.Equals(Combat0166String(participant, "ControllerUserId"), actorUserId, StringComparison.OrdinalIgnoreCase) ? actorUserId : string.Empty),
            ["initiativeRoll"] = Combat0166Int(participant, "InitiativeRoll", 0),
            ["initiative"] = Combat0166Int(participant, "InitiativeRoll", 0),
            ["initiativeTieBreakRolls"] = Combat0166BsonArray(participant, "InitiativeTieBreakRolls").Select(x => x.ToString()).Cast<object>().ToArray(),
            ["initiativeOrderIndex"] = Combat0166Int(participant, "InitiativeOrderIndex", 9999),
            ["turnStatus"] = Combat0166String(participant, "TurnStatus", "waiting"),
            ["standardActions"] = losesFullAction ? 0 : Combat0166Int(participant, "StandardActions", 1),
            ["minorActions"] = Combat0166Int(participant, "MinorActions", 2),
            ["reactionSlots"] = Combat0166Int(participant, "ReactionSlots", 1),
            ["reactionAvailable"] = !Combat0166Bool(participant, "ReactionUsedThisRound"),
            ["natural20BonusTurn"] = Combat0166Bool(participant, "Natural20BonusTurn"),
            ["natural1FirstTurnPenalty"] = losesFullAction,
            ["visibilityMode"] = admin ? Combat0166String(participant, "VisibilityMode", "hidden") : "player_visible",
            ["isPlayerVisible"] = Combat0166CanPlayerSeeParticipant(participant),
            ["publicStateText"] = Combat0166String(participant, "PublicStateText"),
            ["publicNotes"] = Combat0166String(participant, "PublicNotes"),
            ["mapTokenId"] = Combat0166CanPlayerSeeMapToken(participant) || admin ? Combat0166String(participant, "MapTokenId") : string.Empty,
            ["mapTokenDisplayName"] = Combat0166CanPlayerSeeMapToken(participant) || admin ? Combat0166String(participant, "MapTokenDisplayName") : string.Empty
        };
        if (admin)
        {
            payload["characterId"] = Combat0166String(participant, "CharacterId");
            payload["gmStateText"] = Combat0166String(participant, "GmStateText");
            payload["gmNotes"] = Combat0166String(participant, "GmNotes");
            payload["mapTokenVisibility"] = Combat0166String(participant, "MapTokenVisibility", "hidden");
            payload["updatedAtUtc"] = Combat0166Date(participant, "UpdatedAtUtc");
        }
        return payload;
    }

    private IEnumerable<Dictionary<string, object>> Combat0166LogPayloads(string combatId, bool admin)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("CombatId", combatId);
        if (!admin)
            filter &= Builders<BsonDocument>.Filter.In("Visibility", new[] { "public", "player_visible" });
        return Combat0166Events()
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("SequenceNumber"))
            .Limit(100)
            .ToList()
            .OrderBy(x => Combat0166Long(x, "SequenceNumber", 0L))
            .Select(x =>
            {
                var payload = new Dictionary<string, object>
                {
                    ["eventId"] = Combat0166String(x, "Id"),
                    ["sequenceNumber"] = Combat0166Long(x, "SequenceNumber", 0L),
                    ["createdAtUtc"] = Combat0166Date(x, "CreatedAtUtc"),
                    ["eventType"] = Combat0166String(x, "EventType"),
                    ["roundNumber"] = Combat0166Int(x, "RoundNumber", 0),
                    ["turnIndex"] = Combat0166Int(x, "TurnIndex", -1),
                    ["actorParticipantId"] = admin ? Combat0166String(x, "ActorParticipantId") : string.Empty,
                    ["message"] = admin ? FirstNonEmptyWorld(Combat0166String(x, "PublicMessage"), Combat0166String(x, "GmMessage")) : Combat0166String(x, "PublicMessage"),
                    ["visibility"] = admin ? Combat0166String(x, "Visibility") : "public"
                };
                if (admin) payload["gmMessage"] = Combat0166String(x, "GmMessage");
                return payload;
            });
    }

    private void AppendCombat0166Event(BsonDocument combat, string eventType, string publicMessage, string gmMessage, string actorUserId, string visibility, string actorParticipantId)
    {
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid().ToString("N");
        var doc = new BsonDocument
        {
            ["_id"] = id,
            ["Id"] = id,
            ["CombatId"] = Combat0166String(combat, "Id"),
            ["CampaignId"] = Combat0166String(combat, "CampaignId"),
            ["SessionId"] = Combat0166String(combat, "SessionId"),
            ["SequenceNumber"] = now.Ticks,
            ["CreatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId ?? string.Empty,
            ["EventType"] = eventType,
            ["RoundNumber"] = Combat0166Int(combat, "RoundNumber", 0),
            ["TurnIndex"] = Combat0166Int(combat, "CurrentTurnIndex", -1),
            ["ActorParticipantId"] = actorParticipantId ?? string.Empty,
            ["Visibility"] = NormalizeCombat0166LogVisibility(visibility),
            ["PublicMessage"] = publicMessage ?? string.Empty,
            ["GmMessage"] = gmMessage ?? string.Empty,
            ["ServerOnlyData"] = new BsonDocument { ["source"] = "combat_tracker_0166" }
        };
        Combat0166Events().InsertOne(doc);
    }

    private string Combat0166SelectFirstTurn(BsonDocument combat, string actorUserId)
    {
        var preRound = Combat0166BsonArray(combat, "PreRoundQueue").Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (preRound.Count > 0)
        {
            combat["RoundNumber"] = 0;
            combat["PreRoundTurnIndex"] = 0;
            combat["CurrentTurnIndex"] = -1;
            combat["CurrentParticipantId"] = preRound[0];
            Combat0166SetParticipantActive(preRound[0], actorUserId);
            return preRound[0];
        }
        var order = Combat0166OrderIds(combat).ToList();
        if (order.Count == 0) return string.Empty;
        Combat0166ResetReactions(Combat0166String(combat, "Id"), actorUserId);
        combat["RoundNumber"] = 1;
        combat["CurrentTurnIndex"] = 0;
        combat["CurrentParticipantId"] = order[0];
        Combat0166SetParticipantActive(order[0], actorUserId);
        return order[0];
    }

    private string Combat0166AdvanceToNextTurn(BsonDocument combat, string actorUserId)
    {
        var combatId = Combat0166String(combat, "Id");
        var preRound = Combat0166BsonArray(combat, "PreRoundQueue").Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var round = Combat0166Int(combat, "RoundNumber", 0);
        if (round == 0 && preRound.Count > 0)
        {
            var nextPre = Combat0166Int(combat, "PreRoundTurnIndex", -1) + 1;
            if (nextPre < preRound.Count)
            {
                combat["PreRoundTurnIndex"] = nextPre;
                combat["CurrentTurnIndex"] = -1;
                combat["CurrentParticipantId"] = preRound[nextPre];
                Combat0166SetParticipantActive(preRound[nextPre], actorUserId);
                Combat0166TouchSession(combat, actorUserId);
                Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(combatId), combat);
                return preRound[nextPre];
            }
            Combat0166ResetReactions(combatId, actorUserId);
            var normalOrder = Combat0166OrderIds(combat).ToList();
            if (normalOrder.Count == 0) return string.Empty;
            combat["RoundNumber"] = 1;
            combat["CurrentTurnIndex"] = 0;
            combat["CurrentParticipantId"] = normalOrder[0];
            Combat0166SetParticipantActive(normalOrder[0], actorUserId);
            Combat0166TouchSession(combat, actorUserId);
            Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(combatId), combat);
            AppendCombat0166Event(combat, "round.started", "Раунд 1 начат. Реакции восстановлены.", "round=1", actorUserId, "public", string.Empty);
            return normalOrder[0];
        }

        var order = Combat0166OrderIds(combat).ToList();
        if (order.Count == 0) return string.Empty;
        var currentIndex = Combat0166Int(combat, "CurrentTurnIndex", -1);
        var nextIndex = currentIndex + 1;
        if (nextIndex >= order.Count)
        {
            nextIndex = 0;
            combat["RoundNumber"] = Math.Max(1, round) + 1;
            Combat0166ResetReactions(combatId, actorUserId);
            AppendCombat0166Event(combat, "round.started", $"Раунд {Combat0166Int(combat, "RoundNumber", 1)} начат. Реакции восстановлены.", $"round={Combat0166Int(combat, "RoundNumber", 1)}", actorUserId, "public", string.Empty);
        }
        combat["CurrentTurnIndex"] = nextIndex;
        combat["CurrentParticipantId"] = order[nextIndex];
        Combat0166SetParticipantActive(order[nextIndex], actorUserId);
        Combat0166TouchSession(combat, actorUserId);
        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(combatId), combat);
        return order[nextIndex];
    }

    private void Combat0166MarkTurnCompleted(BsonDocument combat, string participantId, string actorUserId, bool skipped)
    {
        var participant = Combat0166Participants().Find(Combat0166AnyIdFilter(participantId)).FirstOrDefault();
        if (participant == null) return;
        if (Combat0166Bool(participant, "Natural20BonusTurn") && Combat0166Int(combat, "RoundNumber", 0) == 0)
            participant["Natural20BonusTurnUsed"] = true;
        if (Combat0166Bool(participant, "Natural1FirstTurnPenalty") && Combat0166Int(combat, "RoundNumber", 0) >= 1 && !Combat0166Bool(participant, "Natural1PenaltyConsumed"))
            participant["Natural1PenaltyConsumed"] = true;
        participant["TurnStatus"] = skipped ? "skipped" : "completed";
        Combat0166TouchParticipant(participant, actorUserId);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(participantId), participant);
    }

    private void Combat0166SetParticipantActive(string participantId, string actorUserId)
    {
        var participant = Combat0166Participants().Find(Combat0166AnyIdFilter(participantId)).FirstOrDefault();
        if (participant == null) return;
        participant["TurnStatus"] = "active";
        Combat0166TouchParticipant(participant, actorUserId);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(participantId), participant);
    }

    private void Combat0166ResetReactions(string combatId, string actorUserId)
    {
        var update = Builders<BsonDocument>.Update
            .Set("ReactionUsedThisRound", false)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actorUserId);
        Combat0166Participants().UpdateMany(Builders<BsonDocument>.Filter.Eq("CombatId", combatId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true), update);
    }

    private void RebuildCombat0166Order(BsonDocument combat)
    {
        var ordered = Combat0166ParticipantDocs(Combat0166String(combat, "Id"), includeArchived: false)
            .OrderBy(x => Combat0166Int(x, "InitiativeOrderIndex", 9999))
            .ThenByDescending(x => Combat0166Int(x, "InitiativeRoll", 0))
            .ThenBy(x => Combat0166String(x, "DisplayName"), StringComparer.OrdinalIgnoreCase)
            .Select(x => Combat0166String(x, "Id"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        combat["InitiativeOrder"] = new BsonArray(ordered);
        Combat0166TouchSession(combat, Combat0166String(combat, "UpdatedByUserId"));
        Combat0166Sessions().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(combat, "Id")), combat);
    }

    private IEnumerable<string> Combat0166OrderIds(BsonDocument combat)
    {
        var ids = Combat0166BsonArray(combat, "InitiativeOrder").Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (ids.Count > 0) return ids;
        return Combat0166ParticipantDocs(Combat0166String(combat, "Id"), includeArchived: false)
            .OrderBy(x => Combat0166Int(x, "InitiativeOrderIndex", 9999))
            .Select(x => Combat0166String(x, "Id"))
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private void Combat0166TouchSession(BsonDocument doc, string actorUserId)
    {
        doc["UpdatedAtUtc"] = DateTime.UtcNow;
        doc["UpdatedByUserId"] = actorUserId ?? string.Empty;
        doc["Revision"] = Combat0166Int(doc, "Revision", 0) + 1;
    }

    private void Combat0166TouchParticipant(BsonDocument doc, string actorUserId)
    {
        doc["UpdatedAtUtc"] = DateTime.UtcNow;
        doc["UpdatedByUserId"] = actorUserId ?? string.Empty;
        doc["Revision"] = Combat0166Int(doc, "Revision", 0) + 1;
    }

    private void PublishCombat0166Sync(BsonDocument combat, string eventType, string actorUserId, string requestId)
    {
        if (!_featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatSyncEvents))) return;
        TryPublishSyncEvent(eventType, Combat0166String(combat, "SessionId"), "combat_tracker", Combat0166String(combat, "Id"), "changed", actorUserId, new Dictionary<string, object>
        {
            ["combatId"] = Combat0166String(combat, "Id"),
            ["roundNumber"] = Combat0166Int(combat, "RoundNumber", 0),
            ["eventType"] = eventType
        }, requestId);
    }

    private static bool Combat0166CanPlayerSeeParticipant(BsonDocument participant)
    {
        return !Combat0166Bool(participant, "IsArchived")
            && (Combat0166Bool(participant, "IsPlayerVisible") || string.Equals(Combat0166String(participant, "VisibilityMode"), "player_visible", StringComparison.OrdinalIgnoreCase));
    }

    private static bool Combat0166CanPlayerSeeCombat(BsonDocument combat)
        => !Combat0166Bool(combat, "IsArchived")
           && string.Equals(Combat0166String(combat, "VisibilityMode", "hidden"), "player_visible", StringComparison.OrdinalIgnoreCase)
           && new[] { "setup", "active", "paused" }.Contains(Combat0166String(combat, "Status", "setup"), StringComparer.OrdinalIgnoreCase);

    private static bool Combat0166CanPlayerSeeMapToken(BsonDocument participant)
        => Combat0166CanPlayerSeeParticipant(participant) && string.Equals(Combat0166String(participant, "MapTokenVisibility"), "player_visible", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCombat0166ParticipantType(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "player" or "pc" or "player_character" => "player_character",
            "npc" => "npc",
            "companion" => "companion",
            "enemy" => "enemy",
            "neutral" => "neutral",
            "creature" => "creature",
            "vehicle" => "vehicle",
            _ => "custom"
        };
    }

    private static string NormalizeCombat0166Visibility(string? value, string fallback)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "player" or "players" or "player_visible" or "playervisible" or "public" or "party" => "player_visible",
            "gm" or "gm_only" or "gmonly" => "gm_only",
            "hidden" or "server_only" or "serveronly" => "hidden",
            _ => fallback
        };
    }

    private static string NormalizeCombat0166LogVisibility(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "gm" or "gm_only" or "gmonly" => "gm_only",
            "player" or "player_visible" or "playervisible" => "player_visible",
            _ => "public"
        };
    }

    private static int? Combat0166ForcedInt(Dictionary<string, object> values, string key)
    {
        if (values.TryGetValue(key, out var raw) && int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static int Combat0166FirstTieBreak(BsonDocument doc)
    {
        var arr = Combat0166BsonArray(doc, "InitiativeTieBreakRolls");
        if (arr.Count == 0) return 0;
        return int.TryParse(arr[0].ToString(), out var parsed) ? parsed : 0;
    }

    private static List<string> Combat0166StringArray(Dictionary<string, object>? map)
    {
        if (map == null) return new List<string>();
        return map.Values.Select(x => Convert.ToString(x) ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    private static List<string> Combat0166EnumerableStrings(object raw)
    {
        if (raw is string text) return text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
        if (raw is IEnumerable enumerable)
        {
            var result = new List<string>();
            foreach (var item in enumerable)
            {
                var value = Convert.ToString(item);
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
            }
            return result;
        }
        return new List<string>();
    }

    private static FilterDefinition<BsonDocument> Combat0166AnyIdFilter(string id)
        => Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Filter.Eq("Id", id));

    private static FilterDefinition<BsonDocument> Combat0166ActiveIdFilter(string id)
        => Builders<BsonDocument>.Filter.And(Combat0166AnyIdFilter(id), Builders<BsonDocument>.Filter.Ne("IsArchived", true));

    private static string Combat0166String(BsonDocument? doc, string name, string fallback = "")
    {
        if (doc == null || !doc.TryGetValue(name, out var value) || value.IsBsonNull) return fallback;
        return value.ToString();
    }

    private static int Combat0166Int(BsonDocument? doc, string name, int fallback)
    {
        if (doc == null || !doc.TryGetValue(name, out var value) || value.IsBsonNull) return fallback;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return (int)value.AsInt64;
        return int.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static long Combat0166Long(BsonDocument? doc, string name, long fallback)
    {
        if (doc == null || !doc.TryGetValue(name, out var value) || value.IsBsonNull) return fallback;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsInt32) return value.AsInt32;
        return long.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static bool Combat0166Bool(BsonDocument? doc, string name)
    {
        if (doc == null || !doc.TryGetValue(name, out var value) || value.IsBsonNull) return false;
        if (value.IsBoolean) return value.AsBoolean;
        return bool.TryParse(value.ToString(), out var parsed) && parsed;
    }

    private static DateTime Combat0166Date(BsonDocument? doc, string name)
    {
        if (doc == null || !doc.TryGetValue(name, out var value) || value.IsBsonNull) return DateTime.MinValue;
        return value.IsValidDateTime ? value.ToUniversalTime() : DateTime.MinValue;
    }

    private static BsonArray Combat0166BsonArray(BsonDocument? doc, string name)
    {
        if (doc == null || !doc.TryGetValue(name, out var value) || value.IsBsonNull || !value.IsBsonArray) return new BsonArray();
        return value.AsBsonArray;
    }
}
