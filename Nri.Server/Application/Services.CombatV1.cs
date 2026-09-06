using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.Application.Services;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope CombatV1EncounterList(CommandContext context)
    {
        var actor = RequireCombatV1Read(context);
        if (actor == null) return Error("combat v1 read endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var campaignId = PayloadReader.GetString(context.Request.Payload, "campaignId") ?? string.Empty;
        var sessionId = PayloadReader.GetString(context.Request.Payload, "sessionId") ?? string.Empty;
        var includeEnded = PayloadReader.GetBool(context.Request.Payload, "includeEnded");
        var limit = Math.Max(1, Math.Min(PayloadReader.GetInt(context.Request.Payload, "limit") ?? 100, 200));
        if (string.IsNullOrWhiteSpace(campaignId) && string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("campaign_or_session_required");

        var encounters = !string.IsNullOrWhiteSpace(sessionId)
            ? _repositories.CombatEncounters.ListBySessionAsync(sessionId, limit).GetAwaiter().GetResult()
            : _repositories.CombatEncounters.ListByCampaignAsync(campaignId, limit).GetAwaiter().GetResult();
        var items = encounters
            .Where(x => string.IsNullOrWhiteSpace(campaignId) || string.Equals(x.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase))
            .Where(x => includeEnded || string.Equals(x.Status, CombatRuntimeStatuses.Active, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Status, CombatRuntimeStatuses.Draft, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Status, CombatRuntimeStatuses.Paused, StringComparison.OrdinalIgnoreCase))
            .Select(x => CombatEncounterSummaryPayload(new CombatEncounterSummary
            {
                Id = x.Id,
                CampaignId = x.CampaignId,
                SessionId = x.SessionId,
                Name = x.Name,
                Status = x.Status,
                RuleSetId = x.RuleSetId,
                RoundNumber = x.RoundNumber,
                ActiveTurnIndex = x.ActiveTurnIndex,
                ActiveParticipantId = x.ActiveParticipantId,
                StartedAtUtc = x.StartedAtUtc,
                EndedAtUtc = x.EndedAtUtc,
                Tags = x.Tags == null ? new List<string>() : x.Tags.ToList()
            })).Cast<object>().ToArray();
        return Ok("Combat v1 encounters loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope CombatV1EncounterCreate(CommandContext context)
    {
        var actor = RequireCombatV1Write(context);
        if (actor == null) return Error("combat v1 write endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatEncounterCreateRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        _logger.Admin($"combat.v1.encounter.create.start campaignId={request.CampaignId} sessionId={request.SessionId}");
        var result = CombatV1Service().CreateEncounterAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 encounter created.", CombatEncounterCreatePayload(result));
    }

    public ResponseEnvelope CombatV1EncounterEnd(CommandContext context)
    {
        var actor = RequireCombatV1Write(context);
        if (actor == null) return Error("combat v1 write endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatEncounterEndRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1Service().EndEncounterAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 encounter ended.", new Dictionary<string, object> { { "encounter", CombatEncounterSummaryPayload(result) } });
    }

    public ResponseEnvelope CombatV1EncounterCancel(CommandContext context)
    {
        var actor = RequireCombatV1Write(context);
        if (actor == null) return Error("combat v1 write endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatEncounterCancelRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1Service().CancelEncounterAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 encounter cancelled.", new Dictionary<string, object> { { "encounter", CombatEncounterSummaryPayload(result) } });
    }

    public ResponseEnvelope CombatV1ParticipantAdd(CommandContext context)
    {
        var actor = RequireCombatV1Write(context);
        if (actor == null) return Error("combat v1 write endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatParticipantAddRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1Service().AddParticipantAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 participant added.", new Dictionary<string, object> { { "participant", CombatParticipantSummaryPayload(result) } });
    }

    public ResponseEnvelope CombatV1ParticipantRemove(CommandContext context)
    {
        var actor = RequireCombatV1Write(context);
        if (actor == null) return Error("combat v1 write endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatParticipantRemoveRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1Service().RemoveParticipantAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 participant removed.", new Dictionary<string, object> { { "participant", CombatParticipantSummaryPayload(result) } });
    }

    public ResponseEnvelope CombatV1EncounterSnapshot(CommandContext context)
    {
        var actor = RequireCombatV1Read(context);
        if (actor == null) return Error("combat v1 read endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatEncounterSnapshotRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1Service().GetSnapshotAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 encounter snapshot loaded.", CombatEncounterSnapshotPayload(result));
    }

    public ResponseEnvelope CombatV1InitiativeSort(CommandContext context)
    {
        var actor = RequireCombatV1TurnWrite(context);
        if (actor == null) return Error("combat v1 turn engine disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatInitiativeSortRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1TurnService().SortInitiativeAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 initiative sorted.", CombatTurnEngineResponsePayload(result));
    }

    public ResponseEnvelope CombatV1RoundStart(CommandContext context)
    {
        var actor = RequireCombatV1TurnWrite(context);
        if (actor == null) return Error("combat v1 turn engine disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatRoundStartRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1TurnService().StartRoundAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 round started.", CombatTurnEngineResponsePayload(result));
    }

    public ResponseEnvelope CombatV1TurnStart(CommandContext context)
    {
        var actor = RequireCombatV1TurnWrite(context);
        if (actor == null) return Error("combat v1 turn engine disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatTurnStartRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1TurnService().StartTurnAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 turn started.", CombatTurnEngineResponsePayload(result));
    }

    public ResponseEnvelope CombatV1TurnEnd(CommandContext context)
    {
        var actor = RequireCombatV1TurnWrite(context);
        if (actor == null) return Error("combat v1 turn engine disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatTurnEndRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1TurnService().EndTurnAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 turn ended.", CombatTurnEngineResponsePayload(result));
    }

    public ResponseEnvelope CombatV1TurnNext(CommandContext context)
    {
        var actor = RequireCombatV1TurnWrite(context);
        if (actor == null) return Error("combat v1 turn engine disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatNextTurnRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1TurnService().MoveToNextTurnAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 next turn loaded.", CombatTurnEngineResponsePayload(result));
    }

    public ResponseEnvelope CombatV1RoundNext(CommandContext context)
    {
        var actor = RequireCombatV1TurnWrite(context);
        if (actor == null) return Error("combat v1 turn engine disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatNextRoundRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1TurnService().MoveToNextRoundAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 next round loaded.", CombatTurnEngineResponsePayload(result));
    }

    public ResponseEnvelope CombatV1TurnSkip(CommandContext context)
    {
        var actor = RequireCombatV1TurnWrite(context);
        if (actor == null) return Error("combat v1 turn engine disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatSkipTurnRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1TurnService().SkipTurnAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 turn skipped.", CombatTurnEngineResponsePayload(result));
    }

    public ResponseEnvelope CombatV1TurnDelay(CommandContext context)
    {
        var actor = RequireCombatV1TurnWrite(context);
        if (actor == null) return Error("combat v1 turn engine disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatDelayTurnRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1TurnService().DelayTurnAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 turn delayed.", CombatTurnEngineResponsePayload(result));
    }

    public ResponseEnvelope CombatV1LogsList(CommandContext context)
    {
        var actor = RequireCombatV1LogRead(context);
        if (actor == null) return Error("combat v1 log read endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatLogListRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1LogReadService().ListLogsAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 logs loaded.", CombatLogListResponsePayload(result));
    }

    public ResponseEnvelope CombatV1ReplayList(CommandContext context)
    {
        var actor = RequireCombatV1ReplayRead(context);
        if (actor == null) return Error("combat v1 replay read endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatReplayListRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1LogReadService().ListReplayEventsAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 replay events loaded.", CombatReplayListResponsePayload(result));
    }

    public ResponseEnvelope CombatV1SnapshotFull(CommandContext context)
    {
        var actor = RequireCombatV1SnapshotRead(context);
        if (actor == null) return Error("combat v1 snapshot read endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatFullSnapshotRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1SnapshotService().BuildFullSnapshotAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 full snapshot loaded.", CombatFullSnapshotResponsePayload(result));
    }

    public ResponseEnvelope CombatV1DiagnosticsRun(CommandContext context)
    {
        var actor = RequireCombatV1DiagnosticsRead(context);
        if (actor == null) return Error("combat v1 diagnostics endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatDiagnosticsRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1DiagnosticsService().RunDiagnosticsAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 diagnostics loaded.", CombatDiagnosticsResponsePayload(result));
    }

    public ResponseEnvelope CombatV1ActionDeclare(CommandContext context)
    {
        var actor = RequireCombatV1ActionWrite(context);
        if (actor == null) return Error("combat v1 action economy disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatActionDeclareRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1ActionEconomyService().DeclareActionAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 action declared.", CombatActionEconomyResponsePayload(result));
    }

    public ResponseEnvelope CombatV1ActionComplete(CommandContext context)
    {
        var actor = RequireCombatV1ActionWrite(context);
        if (actor == null) return Error("combat v1 action economy disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatActionCompleteRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1ActionEconomyService().CompleteActionAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 action completed.", CombatActionEconomyResponsePayload(result));
    }

    public ResponseEnvelope CombatV1ActionCancel(CommandContext context)
    {
        var actor = RequireCombatV1ActionWrite(context);
        if (actor == null) return Error("combat v1 action economy disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatActionCancelRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1ActionEconomyService().CancelActionAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 action cancelled.", CombatActionEconomyResponsePayload(result));
    }

    public ResponseEnvelope CombatV1ActionSpend(CommandContext context)
    {
        var actor = RequireCombatV1ActionWrite(context);
        if (actor == null) return Error("combat v1 action economy disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatActionSpendRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1ActionEconomyService().SpendActionPointsAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 action points processed.", CombatActionEconomyResponsePayload(result));
    }

    public ResponseEnvelope CombatV1PreparedActionTrigger(CommandContext context)
    {
        var actor = RequireCombatV1ActionWrite(context);
        if (actor == null) return Error("combat v1 prepared action disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatPreparedActionTriggerRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1ActionEconomyService().TriggerPreparedActionAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 prepared action triggered.", CombatActionEconomyResponsePayload(result));
    }

    public ResponseEnvelope CombatV1AttackRoll(CommandContext context)
    {
        var actor = RequireCombatV1AttackWrite(context);
        if (actor == null) return Error("combat v1 attack roll disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatAttackDeclareRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1AttackRollService().DeclareAttackAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 attack roll resolved.", CombatAttackResultResponsePayload(result));
    }

    public ResponseEnvelope CombatV1DefensePreview(CommandContext context)
    {
        var actor = RequireCombatV1DefensePreviewRead(context);
        if (actor == null) return Error("combat v1 defense preview disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatDefenseCalculationRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1DefenseCalculationService().BuildDefensePreviewAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 defense preview calculated.", CombatDefenseCalculationResultPayload(result));
    }

    public ResponseEnvelope CombatV1ParticipantVitalsSet(CommandContext context)
    {
        var actor = RequireCombatV1VitalsWrite(context);
        if (actor == null) return Error("combat v1 participant vitals disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatParticipantVitalsSetRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1DamageApplicationService().SetParticipantVitalsAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 participant vitals set.", CombatVitalsSetResponsePayload(result));
    }

    public ResponseEnvelope CombatV1DamageApply(CommandContext context)
    {
        var actor = RequireCombatV1DamageWrite(context);
        if (actor == null) return Error("combat v1 damage application disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatDamageApplyRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1DamageApplicationService().ApplyDamageAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 damage applied.", CombatDamageResultResponsePayload(result));
    }

    public ResponseEnvelope CombatV1ConditionApply(CommandContext context)
    {
        var actor = RequireCombatV1ConditionApplyWrite(context);
        if (actor == null) return Error("combat v1 condition application disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatConditionApplyRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1ConditionService().ApplyConditionAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 condition applied.", CombatConditionResultResponsePayload(result));
    }

    public ResponseEnvelope CombatV1ConditionRemove(CommandContext context)
    {
        var actor = RequireCombatV1ConditionRemoveWrite(context);
        if (actor == null) return Error("combat v1 condition removal disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatConditionRemoveRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1ConditionService().RemoveConditionAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 condition removed.", CombatConditionResultResponsePayload(result));
    }

    public ResponseEnvelope CombatV1ConditionList(CommandContext context)
    {
        var actor = RequireCombatV1ConditionRead(context);
        if (actor == null) return Error("combat v1 condition read endpoint disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatConditionListRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1ConditionService().ListConditionsAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 conditions loaded.", CombatConditionListResponsePayload(result));
    }

    public ResponseEnvelope CombatV1WeaponAttackResolve(CommandContext context)
    {
        var actor = RequireCombatV1WeaponAttackWrite(context);
        if (actor == null) return Error("combat v1 weapon attack disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatWeaponAttackRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1WeaponIntegrationService().ExecuteWeaponAttackAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 weapon attack resolved.", CombatWeaponAttackResponsePayload(result));
    }

    public ResponseEnvelope CombatV1FatePreview(CommandContext context)
    {
        var actor = RequireCombatV1FatePreviewRead(context);
        if (actor == null) return Error("combat v1 fate preview disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var request = ParseCombatFateHookRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = string.Equals(request.RollContext, "damage_roll", StringComparison.OrdinalIgnoreCase)
            ? CombatV1FateHookService().ApplyFateToDamageRollAsync(request, actor).GetAwaiter().GetResult()
            : CombatV1FateHookService().ApplyFateToAttackRollAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 fate preview loaded.", CombatFateHookResultPayload(result));
    }

    private UserAccount? RequireCombatV1Write(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.v1.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1WriteEnabled())
        {
            _logger.Admin($"combat.v1.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1Read(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.v1.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1ReadEnabled())
        {
            _logger.Admin($"combat.v1.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1TurnWrite(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.v1.turn.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1TurnWriteEnabled())
        {
            _logger.Admin($"combat.v1.turn.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1LogRead(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.log.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1LogReadEnabled())
        {
            _logger.Admin($"combat.log.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1ReplayRead(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.log.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1ReplayReadEnabled())
        {
            _logger.Admin($"combat.log.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1SnapshotRead(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.snapshot.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1SnapshotReadEnabled())
        {
            _logger.Admin($"combat.snapshot.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1DiagnosticsRead(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.diagnostics.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1DiagnosticsReadEnabled())
        {
            _logger.Admin($"combat.diagnostics.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1ActionWrite(CommandContext context)
    {
        var actor = GetCurrentAccount(context);

        if (!CombatV1ActionWriteEnabled())
        {
            _logger.Admin($"combat.action.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1AttackWrite(CommandContext context)
    {
        var actor = GetCurrentAccount(context);

        if (!CombatV1AttackWriteEnabled())
        {
            _logger.Admin($"combat.attack.roll.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1DefensePreviewRead(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.defense.preview.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1DefensePreviewReadEnabled())
        {
            _logger.Admin($"combat.defense.preview.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1VitalsWrite(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.damage.apply.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1VitalsWriteEnabled())
        {
            _logger.Admin($"combat.damage.apply.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1DamageWrite(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.damage.apply.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1DamageWriteEnabled())
        {
            _logger.Admin($"combat.damage.apply.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1ConditionApplyWrite(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.condition.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1ConditionApplyWriteEnabled())
        {
            _logger.Admin($"combat.condition.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1ConditionRemoveWrite(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.condition.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1ConditionRemoveWriteEnabled())
        {
            _logger.Admin($"combat.condition.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1ConditionRead(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.condition.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1ConditionReadEnabled())
        {
            _logger.Admin($"combat.condition.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1WeaponAttackWrite(CommandContext context)
    {
        var actor = GetCurrentAccount(context);

        if (!CombatV1WeaponAttackWriteEnabled())
        {
            _logger.Admin($"combat.weapon_attack.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private UserAccount? RequireCombatV1FatePreviewRead(CommandContext context)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"combat.fate.preview.forbidden command={context.Request.Command}");
            throw;
        }

        if (!CombatV1FatePreviewReadEnabled())
        {
            _logger.Admin($"combat.fate.preview.disabled command={context.Request.Command}");
            return null;
        }

        return actor;
    }

    private ICombatEncounterManagementService CombatV1Service()
    {
        if (_combatEncounterManagementService == null)
            throw new InvalidOperationException("Combat v1 management service unavailable.");
        return _combatEncounterManagementService;
    }

    private ICombatTurnEngineService CombatV1TurnService()
    {
        if (_combatTurnEngineService == null)
            throw new InvalidOperationException("Combat v1 turn engine service unavailable.");
        return _combatTurnEngineService;
    }

    private ICombatLogReadService CombatV1LogReadService()
    {
        if (_combatLogReadService == null)
            throw new InvalidOperationException("Combat v1 log read service unavailable.");
        return _combatLogReadService;
    }

    private ICombatSnapshotService CombatV1SnapshotService()
    {
        if (_combatSnapshotService == null)
            throw new InvalidOperationException("Combat v1 snapshot service unavailable.");
        return _combatSnapshotService;
    }

    private ICombatDiagnosticsService CombatV1DiagnosticsService()
    {
        if (_combatDiagnosticsService == null)
            throw new InvalidOperationException("Combat v1 diagnostics service unavailable.");
        return _combatDiagnosticsService;
    }

    private ICombatActionEconomyService CombatV1ActionEconomyService()
    {
        if (_combatActionEconomyService == null)
            throw new InvalidOperationException("Combat v1 action economy service unavailable.");
        return _combatActionEconomyService;
    }

    private ICombatAttackRollService CombatV1AttackRollService()
    {
        if (_combatAttackRollService == null)
            throw new InvalidOperationException("Combat v1 attack roll service unavailable.");
        return _combatAttackRollService;
    }

    private ICombatDefenseCalculationService CombatV1DefenseCalculationService()
    {
        if (_combatDefenseCalculationService == null)
            throw new InvalidOperationException("Combat v1 defense calculation service unavailable.");
        return _combatDefenseCalculationService;
    }

    private ICombatDamageApplicationService CombatV1DamageApplicationService()
    {
        if (_combatDamageApplicationService == null)
            throw new InvalidOperationException("Combat v1 damage application service unavailable.");
        return _combatDamageApplicationService;
    }

    private ICombatConditionService CombatV1ConditionService()
    {
        if (_combatConditionService == null)
            throw new InvalidOperationException("Combat v1 condition service unavailable.");
        return _combatConditionService;
    }

    private ICombatWeaponIntegrationService CombatV1WeaponIntegrationService()
    {
        if (_combatWeaponIntegrationService == null)
            throw new InvalidOperationException("Combat v1 weapon integration service unavailable.");
        return _combatWeaponIntegrationService;
    }

    private ICombatFateHookService CombatV1FateHookService()
    {
        if (_combatFateHookService == null)
            throw new InvalidOperationException("Combat v1 fate hook service unavailable.");
        return _combatFateHookService;
    }

    private static bool CombatV1WriteEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));
    }

    private static bool CombatV1ReadEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints));
    }

    private static bool CombatV1TurnWriteEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatInitiativeOrder))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatTurnEngine))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));
    }

    private static bool CombatV1LogReadEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatLogReadEndpoints));
    }

    private static bool CombatV1ReplayReadEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReplayReadEndpoints));
    }

    private static bool CombatV1SnapshotReadEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSnapshotReadEndpoints));
    }

    private static bool CombatV1DiagnosticsReadEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDiagnosticsEndpoints));
    }

    private static bool CombatV1ActionWriteEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatTurnEngine))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatActionEconomySkeleton))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatActionDeclareEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));
    }

    private static bool CombatV1AttackWriteEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatTurnEngine))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatActionEconomySkeleton))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAttackRollMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatHitCalculationMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAttackActionEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));
    }

    private static bool CombatV1DefensePreviewReadEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDefenseMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDefensePreviewEndpoint));
    }

    private static bool CombatV1VitalsWriteEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatParticipantVitals))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));
    }

    private static bool CombatV1DamageWriteEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDamageMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDamageApplicationEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));
    }

    private static bool CombatV1ConditionApplyWriteEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionsMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionApplyEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));
    }

    private static bool CombatV1ConditionRemoveWriteEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionsMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionRemoveEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));
    }

    private static bool CombatV1ConditionReadEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionsMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionReadEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints));
    }

    private static bool CombatV1WeaponAttackWriteEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatTurnEngine))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatActionEconomySkeleton))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAttackRollMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatHitCalculationMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAttackActionEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWeaponIntegrationMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));
    }

    private static bool CombatV1FatePreviewReadEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateHookMvp));
    }

    private static CombatEncounterCreateRequest ParseCombatEncounterCreateRequest(IDictionary<string, object> payload)
    {
        return new CombatEncounterCreateRequest
        {
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
            SessionId = PayloadReader.GetString(payload, "sessionId") ?? string.Empty,
            RuleSetId = PayloadReader.GetString(payload, "ruleSetId") ?? string.Empty,
            Name = PayloadReader.GetString(payload, "name") ?? string.Empty,
            TeamIds = GetStringList(payload, "teamIds"),
            Tags = GetStringList(payload, "tags"),
            Notes = PayloadReader.GetString(payload, "notes") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatEncounterEndRequest ParseCombatEncounterEndRequest(IDictionary<string, object> payload)
    {
        return new CombatEncounterEndRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatEncounterCancelRequest ParseCombatEncounterCancelRequest(IDictionary<string, object> payload)
    {
        return new CombatEncounterCancelRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatParticipantAddRequest ParseCombatParticipantAddRequest(IDictionary<string, object> payload)
    {
        return new CombatParticipantAddRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            CharacterId = PayloadReader.GetString(payload, "characterId") ?? string.Empty,
            DisplayName = PayloadReader.GetString(payload, "displayName") ?? string.Empty,
            ParticipantType = PayloadReader.GetString(payload, "participantType") ?? string.Empty,
            TeamId = PayloadReader.GetString(payload, "teamId") ?? string.Empty,
            ControllerUserId = PayloadReader.GetString(payload, "controllerUserId") ?? string.Empty,
            IsNpc = PayloadReader.GetBool(payload, "isNpc"),
            IsPlayerControlled = PayloadReader.GetBool(payload, "isPlayerControlled"),
            IsHidden = PayloadReader.GetBool(payload, "isHidden"),
            Initiative = PayloadReader.GetInt(payload, "initiative") ?? 0,
            InitiativeTieBreaker = PayloadReader.GetInt(payload, "initiativeTieBreaker") ?? 0,
            MaxStructure = PayloadReader.GetInt(payload, "maxStructure") ?? 0,
            CurrentStructure = PayloadReader.GetInt(payload, "currentStructure") ?? 0,
            FrontProtection = PayloadReader.GetInt(payload, "frontProtection") ?? 0,
            SideProtection = PayloadReader.GetInt(payload, "sideProtection") ?? 0,
            RearProtection = PayloadReader.GetInt(payload, "rearProtection") ?? 0,
            Tags = GetStringList(payload, "tags"),
            Notes = PayloadReader.GetString(payload, "notes") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatParticipantRemoveRequest ParseCombatParticipantRemoveRequest(IDictionary<string, object> payload)
    {
        return new CombatParticipantRemoveRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ParticipantId = PayloadReader.GetString(payload, "participantId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatEncounterSnapshotRequest ParseCombatEncounterSnapshotRequest(IDictionary<string, object> payload)
    {
        return new CombatEncounterSnapshotRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            IncludeParticipants = GetBoolDefault(payload, "includeParticipants", true),
            IncludeLogs = PayloadReader.GetBool(payload, "includeLogs"),
            IncludeReplayEvents = PayloadReader.GetBool(payload, "includeReplayEvents"),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatInitiativeSortRequest ParseCombatInitiativeSortRequest(IDictionary<string, object> payload)
    {
        return new CombatInitiativeSortRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            SortMode = PayloadReader.GetString(payload, "sortMode") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatRoundStartRequest ParseCombatRoundStartRequest(IDictionary<string, object> payload)
    {
        return new CombatRoundStartRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            RoundNumber = PayloadReader.GetInt(payload, "roundNumber") ?? 0,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatTurnStartRequest ParseCombatTurnStartRequest(IDictionary<string, object> payload)
    {
        return new CombatTurnStartRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ParticipantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatTurnEndRequest ParseCombatTurnEndRequest(IDictionary<string, object> payload)
    {
        return new CombatTurnEndRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ParticipantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty,
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatNextTurnRequest ParseCombatNextTurnRequest(IDictionary<string, object> payload)
    {
        return new CombatNextTurnRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatNextRoundRequest ParseCombatNextRoundRequest(IDictionary<string, object> payload)
    {
        return new CombatNextRoundRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatSkipTurnRequest ParseCombatSkipTurnRequest(IDictionary<string, object> payload)
    {
        return new CombatSkipTurnRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ParticipantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty,
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatDelayTurnRequest ParseCombatDelayTurnRequest(IDictionary<string, object> payload)
    {
        return new CombatDelayTurnRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ParticipantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty,
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatLogListRequest ParseCombatLogListRequest(IDictionary<string, object> payload)
    {
        return new CombatLogListRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            Visibility = PayloadReader.GetString(payload, "visibility") ?? string.Empty,
            EventType = PayloadReader.GetString(payload, "eventType") ?? string.Empty,
            FromRound = PayloadReader.GetInt(payload, "fromRound") ?? 0,
            ToRound = PayloadReader.GetInt(payload, "toRound") ?? 0,
            Limit = PayloadReader.GetInt(payload, "limit") ?? 100,
            Offset = PayloadReader.GetInt(payload, "offset") ?? 0,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatReplayListRequest ParseCombatReplayListRequest(IDictionary<string, object> payload)
    {
        return new CombatReplayListRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            FromSequence = GetLong(payload, "fromSequence"),
            ToSequence = GetLong(payload, "toSequence"),
            Limit = PayloadReader.GetInt(payload, "limit") ?? 200,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatFullSnapshotRequest ParseCombatFullSnapshotRequest(IDictionary<string, object> payload)
    {
        return new CombatFullSnapshotRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            IncludeParticipants = GetBoolDefault(payload, "includeParticipants", true),
            IncludeTurns = GetBoolDefault(payload, "includeTurns", true),
            IncludeRounds = GetBoolDefault(payload, "includeRounds", true),
            IncludeActions = GetBoolDefault(payload, "includeActions", true),
            IncludeLogs = GetBoolDefault(payload, "includeLogs", true),
            IncludeReplayEvents = PayloadReader.GetBool(payload, "includeReplayEvents"),
            IncludeDiagnostics = PayloadReader.GetBool(payload, "includeDiagnostics"),
            LimitLogs = PayloadReader.GetInt(payload, "limitLogs") ?? 100,
            LimitActions = PayloadReader.GetInt(payload, "limitActions") ?? 100,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatDiagnosticsRequest ParseCombatDiagnosticsRequest(IDictionary<string, object> payload)
    {
        return new CombatDiagnosticsRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            IncludeEncounterValidation = GetBoolDefault(payload, "includeEncounterValidation", true),
            IncludeParticipantValidation = GetBoolDefault(payload, "includeParticipantValidation", true),
            IncludeInitiativeValidation = GetBoolDefault(payload, "includeInitiativeValidation", true),
            IncludeTurnValidation = GetBoolDefault(payload, "includeTurnValidation", true),
            IncludeActionValidation = GetBoolDefault(payload, "includeActionValidation", true),
            StrictMode = PayloadReader.GetBool(payload, "strictMode"),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatActionDeclareRequest ParseCombatActionDeclareRequest(IDictionary<string, object> payload)
    {
        var payloadSummary = GetObjectDictionary(payload, "payloadSummary");
        var triggerDefinitionId = PayloadReader.GetString(payload, "triggerDefinitionId") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(triggerDefinitionId))
            payloadSummary["triggerDefinitionId"] = triggerDefinitionId.Trim();

        return new CombatActionDeclareRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ActorParticipantId = PayloadReader.GetString(payload, "actorParticipantId") ?? string.Empty,
            ActionType = PayloadReader.GetString(payload, "actionType") ?? string.Empty,
            ActionName = PayloadReader.GetString(payload, "actionName") ?? string.Empty,
            TargetParticipantIds = GetStringList(payload, "targetParticipantIds"),
            TargetLocationSummary = PayloadReader.GetString(payload, "targetLocationSummary") ?? string.Empty,
            ActionPointCost = PayloadReader.GetInt(payload, "actionPointCost") ?? 0,
            MinorActionPointCost = PayloadReader.GetInt(payload, "minorActionPointCost") ?? 0,
            ReactionCost = PayloadReader.GetInt(payload, "reactionCost") ?? 0,
            PayloadSummary = payloadSummary,
            Notes = PayloadReader.GetString(payload, "notes") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatActionCompleteRequest ParseCombatActionCompleteRequest(IDictionary<string, object> payload)
    {
        return new CombatActionCompleteRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ActionId = PayloadReader.GetString(payload, "actionId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            ResultStatus = PayloadReader.GetString(payload, "resultStatus") ?? string.Empty,
            Message = PayloadReader.GetString(payload, "message") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatActionCancelRequest ParseCombatActionCancelRequest(IDictionary<string, object> payload)
    {
        return new CombatActionCancelRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ActionId = PayloadReader.GetString(payload, "actionId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatActionSpendRequest ParseCombatActionSpendRequest(IDictionary<string, object> payload)
    {
        return new CombatActionSpendRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ParticipantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty,
            ActionPointCost = PayloadReader.GetInt(payload, "actionPointCost") ?? 0,
            MinorActionPointCost = PayloadReader.GetInt(payload, "minorActionPointCost") ?? 0,
            ReactionCost = PayloadReader.GetInt(payload, "reactionCost") ?? 0,
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatPreparedActionTriggerRequest ParseCombatPreparedActionTriggerRequest(IDictionary<string, object> payload)
    {
        return new CombatPreparedActionTriggerRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            PreparedActionId = PayloadReader.GetString(payload, "preparedActionId") ?? string.Empty,
            TriggerDefinitionId = PayloadReader.GetString(payload, "triggerDefinitionId") ?? string.Empty,
            TargetParticipantIds = GetStringList(payload, "targetParticipantIds"),
            TriggerContext = PayloadReader.GetString(payload, "triggerContext") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatAttackDeclareRequest ParseCombatAttackDeclareRequest(IDictionary<string, object> payload)
    {
        return new CombatAttackDeclareRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ActorParticipantId = PayloadReader.GetString(payload, "actorParticipantId") ?? string.Empty,
            TargetParticipantId = PayloadReader.GetString(payload, "targetParticipantId") ?? string.Empty,
            WeaponDefinitionId = PayloadReader.GetString(payload, "weaponDefinitionId") ?? string.Empty,
            AttackProfileId = PayloadReader.GetString(payload, "attackProfileId") ?? string.Empty,
            NaturalAttackId = PayloadReader.GetString(payload, "naturalAttackId") ?? string.Empty,
            AttackSkillId = PayloadReader.GetString(payload, "attackSkillId") ?? string.Empty,
            AttackAttributeId = PayloadReader.GetString(payload, "attackAttributeId") ?? string.Empty,
            AttackBonus = PayloadReader.GetInt(payload, "attackBonus") ?? 0,
            WeaponAccuracyBonus = PayloadReader.GetInt(payload, "weaponAccuracyBonus") ?? 0,
            TargetDefenseOverride = PayloadReader.GetInt(payload, "targetDefenseOverride"),
            DistanceMeters = GetDecimalNullable(payload, "distanceMeters"),
            CoverModifier = PayloadReader.GetInt(payload, "coverModifier") ?? 0,
            SituationalModifier = PayloadReader.GetInt(payload, "situationalModifier") ?? 0,
            UseFateEngine = PayloadReader.GetBool(payload, "useFateEngine"),
            SpendActionPoint = PayloadReader.GetBool(payload, "spendActionPoint"),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatDefenseCalculationRequest ParseCombatDefenseCalculationRequest(IDictionary<string, object> payload)
    {
        return new CombatDefenseCalculationRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            TargetParticipantId = PayloadReader.GetString(payload, "targetParticipantId") ?? string.Empty,
            AttackerParticipantId = PayloadReader.GetString(payload, "attackerParticipantId") ?? string.Empty,
            RuleSetId = PayloadReader.GetString(payload, "ruleSetId") ?? string.Empty,
            AttackType = PayloadReader.GetString(payload, "attackType") ?? string.Empty,
            WeaponDefinitionId = PayloadReader.GetString(payload, "weaponDefinitionId") ?? string.Empty,
            DistanceMeters = GetDecimalNullable(payload, "distanceMeters"),
            CoverState = PayloadReader.GetString(payload, "coverState") ?? string.Empty,
            CoverModifierOverride = PayloadReader.GetInt(payload, "coverModifierOverride"),
            TargetDefenseOverride = PayloadReader.GetInt(payload, "targetDefenseOverride"),
            IncludeArmor = GetBoolDefault(payload, "includeArmor", true),
            IncludeShield = GetBoolDefault(payload, "includeShield", true),
            IncludeCover = GetBoolDefault(payload, "includeCover", true),
            IncludeDistance = GetBoolDefault(payload, "includeDistance", true),
            StrictMode = PayloadReader.GetBool(payload, "strictMode"),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatParticipantVitalsSetRequest ParseCombatParticipantVitalsSetRequest(IDictionary<string, object> payload)
    {
        return new CombatParticipantVitalsSetRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ParticipantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty,
            MaxHealth = PayloadReader.GetInt(payload, "maxHealth") ?? 0,
            CurrentHealth = PayloadReader.GetInt(payload, "currentHealth") ?? 0,
            TemporaryHealth = PayloadReader.GetInt(payload, "temporaryHealth") ?? 0,
            MaxMorale = PayloadReader.GetInt(payload, "maxMorale") ?? 0,
            CurrentMorale = PayloadReader.GetInt(payload, "currentMorale") ?? 0,
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatDamageApplyRequest ParseCombatDamageApplyRequest(IDictionary<string, object> payload)
    {
        return new CombatDamageApplyRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            SourceActionId = PayloadReader.GetString(payload, "sourceActionId") ?? string.Empty,
            AttackerParticipantId = PayloadReader.GetString(payload, "attackerParticipantId") ?? string.Empty,
            TargetParticipantId = PayloadReader.GetString(payload, "targetParticipantId") ?? string.Empty,
            DamageAmount = PayloadReader.GetInt(payload, "damageAmount") ?? 0,
            DamageType = PayloadReader.GetString(payload, "damageType") ?? string.Empty,
            DamageSource = PayloadReader.GetString(payload, "damageSource") ?? string.Empty,
            IsCriticalDamage = PayloadReader.GetBool(payload, "isCriticalDamage"),
            IgnoreTemporaryHealth = PayloadReader.GetBool(payload, "ignoreTemporaryHealth"),
            AllowAutoDefeat = PayloadReader.GetBool(payload, "allowAutoDefeat"),
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatConditionApplyRequest ParseCombatConditionApplyRequest(IDictionary<string, object> payload)
    {
        return new CombatConditionApplyRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            TargetParticipantId = PayloadReader.GetString(payload, "targetParticipantId") ?? string.Empty,
            ConditionDefinitionId = PayloadReader.GetString(payload, "conditionDefinitionId") ?? string.Empty,
            SourceParticipantId = PayloadReader.GetString(payload, "sourceParticipantId") ?? string.Empty,
            SourceActionId = PayloadReader.GetString(payload, "sourceActionId") ?? string.Empty,
            StackCount = PayloadReader.GetInt(payload, "stackCount") ?? 0,
            DurationMode = PayloadReader.GetString(payload, "durationMode") ?? string.Empty,
            DurationRounds = PayloadReader.GetInt(payload, "durationRounds") ?? 0,
            SeverityOverride = PayloadReader.GetString(payload, "severityOverride") ?? string.Empty,
            IsHiddenFromPlayer = PayloadReader.GetBool(payload, "isHiddenFromPlayer"),
            Notes = PayloadReader.GetString(payload, "notes") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatConditionRemoveRequest ParseCombatConditionRemoveRequest(IDictionary<string, object> payload)
    {
        return new CombatConditionRemoveRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            TargetParticipantId = PayloadReader.GetString(payload, "targetParticipantId") ?? string.Empty,
            ConditionInstanceId = PayloadReader.GetString(payload, "conditionInstanceId") ?? string.Empty,
            ConditionDefinitionId = PayloadReader.GetString(payload, "conditionDefinitionId") ?? string.Empty,
            Reason = PayloadReader.GetString(payload, "reason") ?? string.Empty,
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatConditionListRequest ParseCombatConditionListRequest(IDictionary<string, object> payload)
    {
        return new CombatConditionListRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ParticipantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty,
            IncludeRemoved = PayloadReader.GetBool(payload, "includeRemoved"),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatWeaponAttackRequest ParseCombatWeaponAttackRequest(IDictionary<string, object> payload)
    {
        return new CombatWeaponAttackRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            ActorParticipantId = PayloadReader.GetString(payload, "actorParticipantId") ?? string.Empty,
            TargetParticipantId = PayloadReader.GetString(payload, "targetParticipantId") ?? string.Empty,
            WeaponItemInstanceId = PayloadReader.GetString(payload, "weaponItemInstanceId") ?? string.Empty,
            WeaponDefinitionId = PayloadReader.GetString(payload, "weaponDefinitionId") ?? string.Empty,
            AttackProfileId = PayloadReader.GetString(payload, "attackProfileId") ?? string.Empty,
            NaturalAttackId = PayloadReader.GetString(payload, "naturalAttackId") ?? string.Empty,
            AmmoItemInstanceId = PayloadReader.GetString(payload, "ammoItemInstanceId") ?? string.Empty,
            AmmoDefinitionId = PayloadReader.GetString(payload, "ammoDefinitionId") ?? string.Empty,
            AttackSkillId = PayloadReader.GetString(payload, "attackSkillId") ?? string.Empty,
            AttackAttributeId = PayloadReader.GetString(payload, "attackAttributeId") ?? string.Empty,
            AttackBonus = PayloadReader.GetInt(payload, "attackBonus") ?? 0,
            DamageOverride = PayloadReader.GetInt(payload, "damageOverride"),
            DamageType = PayloadReader.GetString(payload, "damageType") ?? string.Empty,
            TargetProtectionZone = PayloadReader.GetString(payload, "targetProtectionZone") ?? "torso",
            DistanceMeters = GetDecimalNullable(payload, "distanceMeters"),
            CoverModifier = PayloadReader.GetInt(payload, "coverModifier") ?? 0,
            SituationalModifier = PayloadReader.GetInt(payload, "situationalModifier") ?? 0,
            UseFateEngine = PayloadReader.GetBool(payload, "useFateEngine"),
            SpendActionPoint = PayloadReader.GetBool(payload, "spendActionPoint"),
            AutoApplyDamage = PayloadReader.GetBool(payload, "autoApplyDamage"),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatFateHookRequest ParseCombatFateHookRequest(IDictionary<string, object> payload)
    {
        return new CombatFateHookRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            RollContext = PayloadReader.GetString(payload, "rollContext") ?? string.Empty,
            ActorParticipantId = PayloadReader.GetString(payload, "actorParticipantId") ?? string.Empty,
            TargetParticipantId = PayloadReader.GetString(payload, "targetParticipantId") ?? string.Empty,
            BaseRoll = PayloadReader.GetInt(payload, "baseRoll") ?? 0,
            DiceExpression = PayloadReader.GetString(payload, "diceExpression") ?? string.Empty,
            UseFateEngine = PayloadReader.GetBool(payload, "useFateEngine"),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static List<string> GetStringList(IDictionary<string, object> payload, string key)
    {
        var values = PayloadReader.GetList(payload, key);
        if (values == null) return new List<string>();
        return values
            .Select(x => Convert.ToString(x) ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, object> GetObjectDictionary(IDictionary<string, object> payload, string key)
    {
        return PayloadReader.GetDictionary(payload, key)
            ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static long GetLong(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null) return 0;
        if (value is long l) return l;
        if (value is int i) return i;
        return long.TryParse(Convert.ToString(value), out var parsed) ? parsed : 0;
    }

    private static decimal? GetDecimalNullable(IDictionary<string, object> payload, string key)
    {
        if (payload == null || !payload.TryGetValue(key, out var value) || value == null) return null;
        if (value is decimal d) return d;
        if (value is double db) return Convert.ToDecimal(db);
        if (value is float f) return Convert.ToDecimal(f);
        if (value is int i) return i;
        if (value is long l) return l;
        return decimal.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;
    }

    private static Dictionary<string, object> CombatEncounterCreatePayload(CombatEncounterCreateResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "status", response.Status },
            { "campaignId", response.CampaignId },
            { "sessionId", response.SessionId },
            { "roundNumber", response.RoundNumber },
            { "activeTurnIndex", response.ActiveTurnIndex },
            { "createdAtUtc", response.CreatedAtUtc }
        };
    }

    private static Dictionary<string, object> CombatEncounterSnapshotPayload(CombatEncounterSnapshotResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounter", CombatEncounterSummaryPayload(response.Encounter) },
            { "participants", response.Participants.Select(CombatParticipantSummaryPayload).Cast<object>().ToArray() },
            { "logs", response.Logs.Select(CombatLogSummaryPayload).Cast<object>().ToArray() },
            { "replayEvents", response.ReplayEvents.Select(CombatReplayEventSummaryPayload).Cast<object>().ToArray() },
            { "warnings", response.Warnings.Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> CombatTurnEngineResponsePayload(CombatTurnEngineResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "status", response.Status },
            { "roundNumber", response.RoundNumber },
            { "activeTurnIndex", response.ActiveTurnIndex },
            { "activeParticipantId", response.ActiveParticipantId },
            { "previousParticipantId", response.PreviousParticipantId },
            { "changed", response.Changed },
            { "message", response.Message },
            { "snapshot", CombatEncounterSnapshotPayload(response.Snapshot) },
            { "warnings", response.Warnings.Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> CombatEncounterSummaryPayload(CombatEncounterSummary encounter)
    {
        return new Dictionary<string, object>
        {
            { "id", encounter.Id },
            { "campaignId", encounter.CampaignId },
            { "sessionId", encounter.SessionId },
            { "name", encounter.Name },
            { "status", encounter.Status },
            { "ruleSetId", encounter.RuleSetId },
            { "roundNumber", encounter.RoundNumber },
            { "activeTurnIndex", encounter.ActiveTurnIndex },
            { "activeParticipantId", encounter.ActiveParticipantId },
            { "participantCount", encounter.ParticipantCount },
            { "startedAtUtc", CombatDatePayload(encounter.StartedAtUtc) },
            { "endedAtUtc", CombatDatePayload(encounter.EndedAtUtc) },
            { "tags", encounter.Tags.Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> CombatParticipantSummaryPayload(CombatParticipantSummary participant)
    {
        return new Dictionary<string, object>
        {
            { "id", participant.Id },
            { "encounterId", participant.EncounterId },
            { "characterId", participant.CharacterId },
            { "displayName", participant.DisplayName },
            { "participantType", participant.ParticipantType },
            { "teamId", participant.TeamId },
            { "controllerUserId", participant.ControllerUserId },
            { "isNpc", participant.IsNpc },
            { "isPlayerControlled", participant.IsPlayerControlled },
            { "initiative", participant.Initiative },
            { "natural20BonusTurn", participant.Natural20BonusTurn },
            { "natural20BonusTurnUsed", participant.Natural20BonusTurnUsed },
            { "natural1FirstTurnPenalty", participant.Natural1FirstTurnPenalty },
            { "natural1PenaltyConsumed", participant.Natural1PenaltyConsumed },
            { "natural1PenaltyActive", participant.Natural1PenaltyActive },
            { "isActive", participant.IsActive },
            { "isDefeated", participant.IsDefeated },
            { "isHidden", participant.IsHidden },
            { "hasActedThisRound", participant.HasActedThisRound },
            { "actionPoints", participant.ActionPoints },
            { "minorActionPoints", participant.MinorActionPoints },
            { "reactionCount", participant.ReactionCount },
            { "reactionLimit", participant.ReactionLimit },
            { "maxHealth", participant.MaxHealth },
            { "currentHealth", participant.CurrentHealth },
            { "maxStructure", participant.MaxStructure },
            { "currentStructure", participant.CurrentStructure },
            { "frontProtection", participant.FrontProtection },
            { "sideProtection", participant.SideProtection },
            { "rearProtection", participant.RearProtection },
            { "disabledModuleName", participant.DisabledModuleName },
            { "temporaryHealth", participant.TemporaryHealth },
            { "maxMorale", participant.MaxMorale },
            { "currentMorale", participant.CurrentMorale },
            { "lastDamageTaken", participant.LastDamageTaken },
            { "lastDamageType", participant.LastDamageType },
            { "defeatedAtUtc", CombatDatePayload(participant.DefeatedAtUtc) },
            { "defeatedReason", participant.DefeatedReason },
            { "conditionCount", participant.ConditionCount },
            { "activeConditionIds", participant.ActiveConditionIds.Cast<object>().ToArray() },
            { "positionSummary", participant.PositionSummary },
            { "sceneMapId", participant.SceneMapId },
            { "mapTokenId", participant.MapTokenId },
            { "mapTokenDisplayName", participant.MapTokenDisplayName },
            { "mapTokenVisibility", participant.MapTokenVisibility },
            { "mapLinkStatus", participant.MapLinkStatus },
            { "mapBadgeText", participant.MapBadgeText },
            { "mapBadgeColorKey", participant.MapBadgeColorKey },
            { "distanceMeters", participant.DistanceMeters },
            { "coverState", participant.CoverState },
            { "visibilityState", participant.VisibilityState },
            { "tags", participant.Tags.Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> CombatLogSummaryPayload(CombatLogSummary entry)
    {
        return new Dictionary<string, object>
        {
            { "id", entry.Id },
            { "encounterId", entry.EncounterId },
            { "roundNumber", entry.RoundNumber },
            { "turnIndex", entry.TurnIndex },
            { "actorParticipantId", entry.ActorParticipantId },
            { "eventType", entry.EventType },
            { "message", entry.Message },
            { "visibility", entry.Visibility },
            { "createdAtUtc", CombatDatePayload(entry.CreatedAtUtc) },
            { "requestId", entry.RequestId },
            { "payloadSummary", DictionaryPayload(entry.PayloadSummary) }
        };
    }

    private static Dictionary<string, object> CombatReplayEventSummaryPayload(CombatReplayEventSummary entry)
    {
        return new Dictionary<string, object>
        {
            { "id", entry.Id },
            { "encounterId", entry.EncounterId },
            { "sequenceNumber", entry.SequenceNumber },
            { "eventType", entry.EventType },
            { "roundNumber", entry.RoundNumber },
            { "turnIndex", entry.TurnIndex },
            { "actorParticipantId", entry.ActorParticipantId },
            { "visibility", entry.Visibility },
            { "createdAtUtc", CombatDatePayload(entry.CreatedAtUtc) },
            { "requestId", entry.RequestId },
            { "dataSummary", DictionaryPayload(entry.DataSummary) }
        };
    }

    private static Dictionary<string, object> CombatLogListResponsePayload(CombatLogListResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "items", response.Items.Select(CombatLogSummaryPayload).Cast<object>().ToArray() },
            { "total", response.Total },
            { "limit", response.Limit },
            { "offset", response.Offset },
            { "hasMore", response.HasMore }
        };
    }

    private static Dictionary<string, object> CombatReplayListResponsePayload(CombatReplayListResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "items", response.Items.Select(CombatReplayEventSummaryPayload).Cast<object>().ToArray() },
            { "fromSequence", response.FromSequence },
            { "toSequence", response.ToSequence },
            { "hasMore", response.HasMore }
        };
    }

    private static Dictionary<string, object> CombatFullSnapshotResponsePayload(CombatFullSnapshotResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounter", CombatEncounterSummaryPayload(response.Encounter) },
            { "participants", response.Participants.Select(CombatParticipantSummaryPayload).Cast<object>().ToArray() },
            { "currentRound", CombatRoundSummaryPayload(response.CurrentRound) },
            { "currentTurn", CombatTurnSummaryPayload(response.CurrentTurn) },
            { "initiativeOrder", response.InitiativeOrder.Select(CombatInitiativeEntrySummaryPayload).Cast<object>().ToArray() },
            { "recentActions", response.RecentActions.Select(CombatActionSummaryPayload).Cast<object>().ToArray() },
            { "recentLogs", response.RecentLogs.Select(CombatLogSummaryPayload).Cast<object>().ToArray() },
            { "recentReplayEvents", response.RecentReplayEvents.Select(CombatReplayEventSummaryPayload).Cast<object>().ToArray() },
            { "diagnostics", CombatDiagnosticsSummaryPayload(response.Diagnostics) },
            { "warnings", response.Warnings.Cast<object>().ToArray() },
            { "builtAtUtc", CombatDatePayload(response.BuiltAtUtc) }
        };
    }

    private static object CombatDatePayload(DateTime value)
        => value <= DateTime.MinValue.AddDays(1) ? string.Empty : (object)value;

    private static object CombatDatePayload(DateTime? value)
        => value.HasValue ? CombatDatePayload(value.Value) : string.Empty;

    private static Dictionary<string, object> CombatRoundSummaryPayload(CombatRoundSummary round)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", round.EncounterId },
            { "roundNumber", round.RoundNumber },
            { "startedAtUtc", CombatDatePayload(round.StartedAtUtc) },
            { "endedAtUtc", CombatDatePayload(round.EndedAtUtc) },
            { "turnCount", round.TurnCount },
            { "completedParticipantIds", round.CompletedParticipantIds.Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> CombatTurnSummaryPayload(CombatTurnSummary turn)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", turn.EncounterId },
            { "roundNumber", turn.RoundNumber },
            { "turnIndex", turn.TurnIndex },
            { "participantId", turn.ParticipantId },
            { "status", turn.Status },
            { "startedAtUtc", CombatDatePayload(turn.StartedAtUtc) },
            { "endedAtUtc", CombatDatePayload(turn.EndedAtUtc) },
            { "skipped", turn.Skipped },
            { "skipReason", turn.SkipReason },
            { "actionPointsStarted", turn.ActionPointsStarted },
            { "actionPointsSpent", turn.ActionPointsSpent },
            { "minorActionPointsStarted", turn.MinorActionPointsStarted },
            { "minorActionPointsSpent", turn.MinorActionPointsSpent },
            { "reactionsUsed", turn.ReactionsUsed }
        };
    }

    private static Dictionary<string, object> CombatInitiativeEntrySummaryPayload(CombatInitiativeEntrySummary entry)
    {
        return new Dictionary<string, object>
        {
            { "participantId", entry.ParticipantId },
            { "displayName", entry.DisplayName },
            { "initiative", entry.Initiative },
            { "tieBreaker", entry.TieBreaker },
            { "orderIndex", entry.OrderIndex },
            { "isDelayed", entry.IsDelayed },
            { "isSkipped", entry.IsSkipped },
            { "isActive", entry.IsActive },
            { "isDefeated", entry.IsDefeated }
        };
    }

    private static Dictionary<string, object> CombatActionSummaryPayload(CombatActionSummary action)
    {
        return new Dictionary<string, object>
        {
            { "id", action.Id },
            { "encounterId", action.EncounterId },
            { "roundNumber", action.RoundNumber },
            { "turnIndex", action.TurnIndex },
            { "actorParticipantId", action.ActorParticipantId },
            { "actionType", action.ActionType },
            { "actionName", action.ActionName },
            { "targetParticipantIds", action.TargetParticipantIds.Cast<object>().ToArray() },
            { "targetLocationSummary", action.TargetLocationSummary },
            { "actionPointCost", action.ActionPointCost },
            { "minorActionPointCost", action.MinorActionPointCost },
            { "reactionCost", action.ReactionCost },
            { "status", action.Status },
            { "createdAtUtc", CombatDatePayload(action.CreatedAtUtc) },
            { "requestId", action.RequestId }
        };
    }

    private static Dictionary<string, object> CombatDiagnosticsResponsePayload(CombatDiagnosticsResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "isValid", response.IsValid },
            { "sections", response.Sections.Select(CombatDiagnosticsSectionPayload).Cast<object>().ToArray() },
            { "errors", response.Errors.Select(CombatValidationIssuePayload).Cast<object>().ToArray() },
            { "warnings", response.Warnings.Select(CombatValidationIssuePayload).Cast<object>().ToArray() },
            { "summary", CombatDiagnosticsSummaryPayload(response.Summary) },
            { "checkedAtUtc", response.CheckedAtUtc }
        };
    }

    private static Dictionary<string, object> CombatDiagnosticsSectionPayload(CombatDiagnosticsSection section)
    {
        return new Dictionary<string, object>
        {
            { "section", section.Section },
            { "isValid", section.IsValid },
            { "errors", section.Errors.Select(CombatValidationIssuePayload).Cast<object>().ToArray() },
            { "warnings", section.Warnings.Select(CombatValidationIssuePayload).Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> CombatValidationIssuePayload(CombatValidationIssue issue)
    {
        return new Dictionary<string, object>
        {
            { "code", issue.Code },
            { "severity", issue.Severity },
            { "message", issue.Message },
            { "entityId", issue.EntityId },
            { "entityType", issue.EntityType }
        };
    }

    private static Dictionary<string, object> CombatDiagnosticsSummaryPayload(CombatDiagnosticsSummary summary)
    {
        return new Dictionary<string, object>
        {
            { "participantCount", summary.ParticipantCount },
            { "activeParticipantCount", summary.ActiveParticipantCount },
            { "defeatedParticipantCount", summary.DefeatedParticipantCount },
            { "initiativeEntryCount", summary.InitiativeEntryCount },
            { "roundNumber", summary.RoundNumber },
            { "activeTurnIndex", summary.ActiveTurnIndex },
            { "actionCount", summary.ActionCount },
            { "logCount", summary.LogCount },
            { "errorCount", summary.ErrorCount },
            { "warningCount", summary.WarningCount }
        };
    }

    private static Dictionary<string, object> CombatActionEconomyResponsePayload(CombatActionEconomyResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "actionId", response.ActionId },
            { "actorParticipantId", response.ActorParticipantId },
            { "status", response.Status },
            { "actionPointsRemaining", response.ActionPointsRemaining },
            { "minorActionPointsRemaining", response.MinorActionPointsRemaining },
            { "reactionsUsed", response.ReactionsUsed },
            { "reactionLimit", response.ReactionLimit },
            { "alreadyApplied", response.AlreadyApplied },
            { "message", response.Message },
            { "warnings", response.Warnings.Cast<object>().ToArray() },
            { "snapshot", CombatFullSnapshotResponsePayload(response.Snapshot) }
        };
    }

    private static Dictionary<string, object> CombatAttackResultResponsePayload(CombatAttackResultResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "actionId", response.ActionId },
            { "alreadyApplied", response.AlreadyApplied },
            { "actorParticipantId", response.ActorParticipantId },
            { "targetParticipantId", response.TargetParticipantId },
            { "weaponDefinitionId", response.WeaponDefinitionId },
            { "attackProfileId", response.AttackProfileId },
            { "roll", response.Roll },
            { "naturalRoll", response.NaturalRoll },
            { "attackTotal", response.AttackTotal },
            { "targetDefense", response.TargetDefense },
            { "hitResult", response.HitResult },
            { "isHit", response.IsHit },
            { "isCritical", response.IsCritical },
            { "isFumble", response.IsFumble },
            { "isNaturalCritical", response.IsNaturalCritical },
            { "isNaturalFumble", response.IsNaturalFumble },
            { "modifiers", CombatAttackModifierBreakdownPayload(response.Modifiers) },
            { "fate", CombatFateHookResultPayload(response.Fate) },
            { "message", response.Message },
            { "warnings", response.Warnings.Cast<object>().ToArray() },
            { "snapshot", CombatFullSnapshotResponsePayload(response.Snapshot) }
        };
    }

    private static Dictionary<string, object> CombatAttackModifierBreakdownPayload(CombatAttackModifierBreakdown modifiers)
    {
        return new Dictionary<string, object>
        {
            { "attackBonus", modifiers.AttackBonus },
            { "weaponAccuracyBonus", modifiers.WeaponAccuracyBonus },
            { "skillBonus", modifiers.SkillBonus },
            { "attributeBonus", modifiers.AttributeBonus },
            { "distanceModifier", modifiers.DistanceModifier },
            { "coverModifier", modifiers.CoverModifier },
            { "situationalModifier", modifiers.SituationalModifier },
            { "fateModifier", modifiers.FateModifier },
            { "totalModifier", modifiers.TotalModifier }
        };
    }

    private static Dictionary<string, object> CombatDefenseCalculationResultPayload(CombatDefenseCalculationResult response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "targetParticipantId", response.TargetParticipantId },
            { "attackerParticipantId", response.AttackerParticipantId },
            { "targetDefense", response.TargetDefense },
            { "baseDefense", response.BaseDefense },
            { "armorDefenseBonus", response.ArmorDefenseBonus },
            { "armorMobilityPenalty", response.ArmorMobilityPenalty },
            { "armorTrainingRank", response.ArmorTrainingRank },
            { "effectiveMobilityPenalty", response.EffectiveMobilityPenalty },
            { "shieldDefenseBonus", response.ShieldDefenseBonus },
            { "coverDefenseBonus", response.CoverDefenseBonus },
            { "distanceDefenseBonus", response.DistanceDefenseBonus },
            { "situationalDefenseBonus", response.SituationalDefenseBonus },
            { "targetDefenseOverrideUsed", response.TargetDefenseOverrideUsed },
            { "armorItems", response.ArmorItems.Select(CombatDefenseEquipmentSummaryPayload).Cast<object>().ToArray() },
            { "shieldItems", response.ShieldItems.Select(CombatDefenseEquipmentSummaryPayload).Cast<object>().ToArray() },
            { "warnings", response.Warnings.Cast<object>().ToArray() },
            { "errors", response.Errors.Cast<object>().ToArray() },
            { "checkedAtUtc", response.CheckedAtUtc }
        };
    }

    private static Dictionary<string, object> CombatDefenseEquipmentSummaryPayload(CombatDefenseEquipmentSummary item)
    {
        return new Dictionary<string, object>
        {
            { "itemInstanceId", item.ItemInstanceId },
            { "definitionId", item.DefinitionId },
            { "displayName", item.DisplayName },
            { "equipmentSlotId", item.EquipmentSlotId },
            { "defenseBonus", item.DefenseBonus },
            { "source", item.Source }
        };
    }

    private static Dictionary<string, object> CombatVitalsSetResponsePayload(CombatVitalsSetResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "participantId", response.ParticipantId },
            { "maxHealth", response.MaxHealth },
            { "currentHealth", response.CurrentHealth },
            { "temporaryHealth", response.TemporaryHealth },
            { "maxMorale", response.MaxMorale },
            { "currentMorale", response.CurrentMorale },
            { "message", response.Message },
            { "snapshot", CombatFullSnapshotResponsePayload(response.Snapshot) }
        };
    }

    private static Dictionary<string, object> CombatDamageResultResponsePayload(CombatDamageResultResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "sourceActionId", response.SourceActionId },
            { "attackerParticipantId", response.AttackerParticipantId },
            { "targetParticipantId", response.TargetParticipantId },
            { "damageAmount", response.DamageAmount },
            { "damageApplied", response.DamageApplied },
            { "damagePrevented", response.DamagePrevented },
            { "damageType", response.DamageType },
            { "previousHealth", response.PreviousHealth },
            { "currentHealth", response.CurrentHealth },
            { "resourceType", response.ResourceType },
            { "previousResource", response.PreviousResource },
            { "currentResource", response.CurrentResource },
            { "previousTemporaryHealth", response.PreviousTemporaryHealth },
            { "currentTemporaryHealth", response.CurrentTemporaryHealth },
            { "targetDefeated", response.TargetDefeated },
            { "defeatedReason", response.DefeatedReason },
            { "actionId", response.ActionId },
            { "alreadyApplied", response.AlreadyApplied },
            { "message", response.Message },
            { "warnings", response.Warnings.Cast<object>().ToArray() },
            { "snapshot", CombatFullSnapshotResponsePayload(response.Snapshot) }
        };
    }

    private static Dictionary<string, object> CombatConditionResultResponsePayload(CombatConditionResultResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "participantId", response.ParticipantId },
            { "condition", CombatConditionSummaryPayload(response.Condition) },
            { "message", response.Message },
            { "warnings", response.Warnings.Cast<object>().ToArray() },
            { "snapshot", CombatFullSnapshotResponsePayload(response.Snapshot) }
        };
    }

    private static Dictionary<string, object> CombatConditionListResponsePayload(CombatConditionListResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "participantId", response.ParticipantId },
            { "conditions", response.Conditions.Select(CombatConditionSummaryPayload).Cast<object>().ToArray() },
            { "warnings", response.Warnings.Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> CombatConditionSummaryPayload(CombatConditionSummary condition)
    {
        return new Dictionary<string, object>
        {
            { "conditionInstanceId", condition.ConditionInstanceId },
            { "conditionDefinitionId", condition.ConditionDefinitionId },
            { "displayName", condition.DisplayName },
            { "conditionGroup", condition.ConditionGroup },
            { "severity", condition.Severity },
            { "stackCount", condition.StackCount },
            { "maxStacks", condition.MaxStacks },
            { "durationMode", condition.DurationMode },
            { "remainingRounds", condition.RemainingRounds },
            { "isHiddenFromPlayer", condition.IsHiddenFromPlayer },
            { "isPositive", condition.IsPositive },
            { "isNegative", condition.IsNegative },
            { "status", condition.Status },
            { "appliedRoundNumber", condition.AppliedRoundNumber },
            { "appliedTurnIndex", condition.AppliedTurnIndex },
            { "appliedAtUtc", condition.AppliedAtUtc }
        };
    }

    private static Dictionary<string, object> CombatWeaponAttackResponsePayload(CombatWeaponAttackResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", response.EncounterId },
            { "attackActionId", response.AttackActionId },
            { "damageActionId", response.DamageActionId },
            { "actorParticipantId", response.ActorParticipantId },
            { "targetParticipantId", response.TargetParticipantId },
            { "weaponDefinitionId", response.WeaponDefinitionId },
            { "ammoDefinitionId", response.AmmoDefinitionId },
            { "attackResult", CombatAttackResultResponsePayload(response.AttackResult) },
            { "damageResult", CombatDamageResultResponsePayload(response.DamageResult) },
            { "weaponSummary", CombatWeaponCombatSummaryPayload(response.WeaponSummary) },
            { "ammoSummary", CombatAmmoCombatSummaryPayload(response.AmmoSummary) },
            { "penetrationResult", CombatPenetrationResultPayload(response.PenetrationResult) },
            { "damagePreview", CombatDamagePreviewPayload(response.DamagePreview) },
            { "areaTargetResults", response.AreaTargetResults.Select(CombatAreaTargetResultPayload022Gate2).Cast<object>().ToArray() },
            { "warnings", response.Warnings.Cast<object>().ToArray() },
            { "message", response.Message },
            { "snapshot", CombatFullSnapshotResponsePayload(response.Snapshot) }
        };
    }

    private static Dictionary<string, object> CombatAreaTargetResultPayload022Gate2(CombatAreaTargetResult022Gate2 result)
    {
        return new Dictionary<string, object>
        {
            { "targetParticipantId", result.TargetParticipantId },
            { "targetDisplayName", result.TargetDisplayName },
            { "isHit", result.IsHit },
            { "attackTotal", result.AttackTotal },
            { "targetDefense", result.TargetDefense },
            { "damagePreview", CombatDamagePreviewPayload(result.DamagePreview) },
            { "damageResult", CombatDamageResultResponsePayload(result.DamageResult) }
        };
    }

    private static Dictionary<string, object> CombatWeaponCombatSummaryPayload(CombatWeaponCombatSummary weapon)
    {
        return new Dictionary<string, object>
        {
            { "weaponItemInstanceId", weapon.WeaponItemInstanceId },
            { "weaponDefinitionId", weapon.WeaponDefinitionId },
            { "attackProfileId", weapon.AttackProfileId },
            { "attackProfileName", weapon.AttackProfileName },
            { "displayName", weapon.DisplayName },
            { "weaponType", weapon.WeaponType },
            { "handedness", weapon.Handedness },
            { "damageDraft", weapon.DamageDraft },
            { "accuracyDraft", weapon.AccuracyDraft },
            { "linkedSkillIds", weapon.LinkedSkillIds.Cast<object>().ToArray() },
            { "equipmentSlotIds", weapon.EquipmentSlotIds.Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> CombatAmmoCombatSummaryPayload(CombatAmmoCombatSummary ammo)
    {
        return new Dictionary<string, object>
        {
            { "ammoItemInstanceId", ammo.AmmoItemInstanceId },
            { "ammoDefinitionId", ammo.AmmoDefinitionId },
            { "displayName", ammo.DisplayName },
            { "ammoType", ammo.AmmoType },
            { "compatible", ammo.Compatible },
            { "damageModifierDraft", ammo.DamageModifierDraft },
            { "quantity", ammo.Quantity }
        };
    }

    private static Dictionary<string, object> CombatDamagePreviewPayload(CombatDamagePreview preview)
    {
        return new Dictionary<string, object>
        {
            { "baseDamage", preview.BaseDamage },
            { "ammoDamageModifier", preview.AmmoDamageModifier },
            { "fateModifier", preview.FateModifier },
            { "criticalMultiplier", preview.CriticalMultiplier },
            { "damageBeforeMitigation", preview.DamageBeforeMitigation },
            { "finalDamage", preview.FinalDamage },
            { "protectionValue", preview.ProtectionValue },
            { "penetrationValue", preview.PenetrationValue },
            { "mitigatedDamage", preview.MitigatedDamage },
            { "failedPenetrationDamageTransfer", preview.FailedPenetrationDamageTransfer },
            { "isPenetrated", preview.IsPenetrated },
            { "penetrationType", preview.PenetrationType },
            { "protectionZone", preview.ProtectionZone },
            { "damageType", preview.DamageType },
            { "isDraftBased", preview.IsDraftBased },
            { "fate", CombatFateHookResultPayload(preview.Fate) }
        };
    }

    private static Dictionary<string, object> CombatPenetrationResultPayload(CombatPenetrationResult0219 result)
    {
        return new Dictionary<string, object>
        {
            { "penetrationType", result.PenetrationType },
            { "totalPenetration", result.TotalPenetration },
            { "targetProtection", result.TargetProtection },
            { "effectiveProtection", result.EffectiveProtection },
            { "isPenetrated", result.IsPenetrated }
        };
    }

    private static Dictionary<string, object> CombatFateHookResultPayload(CombatFateHookResult result)
    {
        return new Dictionary<string, object>
        {
            { "applied", result.Applied },
            { "rollContext", result.RollContext },
            { "baseRoll", result.BaseRoll },
            { "fateModifiedRoll", result.FateModifiedRoll },
            { "fateModifier", result.FateModifier },
            { "fateSummary", result.FateSummary },
            { "fateLayerSummaries", result.FateLayerSummaries.Select(CombatFateLayerSummaryPayload).Cast<object>().ToArray() },
            { "warnings", result.Warnings.Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> CombatFateLayerSummaryPayload(CombatFateLayerSummary layer)
    {
        return new Dictionary<string, object>
        {
            { "layerIndex", layer.LayerIndex },
            { "layerName", layer.LayerName },
            { "layerType", layer.LayerType },
            { "modifier", layer.Modifier },
            { "isEnabled", layer.IsEnabled },
            { "summary", layer.Summary }
        };
    }

    private static Dictionary<string, object> DictionaryPayload(Dictionary<string, object> values)
    {
        return (values ?? new Dictionary<string, object>())
            .Take(50)
            .ToDictionary(x => x.Key, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }
}
