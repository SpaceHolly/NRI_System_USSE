using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatMvpSmokeService
{
    Task<CombatMvpSmokeResult> RunCombatMvpSmokeAsync(CombatMvpSmokeRequest request, UserAccount actor);
}

public sealed class CombatMvpSmokeService : ICombatMvpSmokeService
{
    private readonly ICombatEncounterManagementService _encounters;
    private readonly ICombatTurnEngineService _turns;
    private readonly ICombatAttackRollService _attacks;
    private readonly ICombatDefenseCalculationService _defense;
    private readonly ICombatDamageApplicationService _damage;
    private readonly ICombatConditionService _conditions;
    private readonly ICombatWeaponIntegrationService _weapons;
    private readonly ICombatLogReadService _logs;
    private readonly ICombatSnapshotService _snapshots;

    public CombatMvpSmokeService(
        ICombatEncounterManagementService encounters,
        ICombatTurnEngineService turns,
        ICombatAttackRollService attacks,
        ICombatDefenseCalculationService defense,
        ICombatDamageApplicationService damage,
        ICombatConditionService conditions,
        ICombatWeaponIntegrationService weapons,
        ICombatLogReadService logs,
        ICombatSnapshotService snapshots)
    {
        _encounters = encounters;
        _turns = turns;
        _attacks = attacks;
        _defense = defense;
        _damage = damage;
        _conditions = conditions;
        _weapons = weapons;
        _logs = logs;
        _snapshots = snapshots;
    }

    public async Task<CombatMvpSmokeResult> RunCombatMvpSmokeAsync(CombatMvpSmokeRequest request, UserAccount actor)
    {
        var result = new CombatMvpSmokeResult { CheckedAtUtc = DateTime.UtcNow };
        request ??= new CombatMvpSmokeRequest();
        actor ??= new UserAccount();

        if (!request.RunWriteSmoke)
        {
            AddStep(result, "validate_only", true, "Write smoke disabled; no combat runtime data was created.");
            result.Success = true;
            return result;
        }

        var requestId = FirstNonEmpty(request.RequestId, Guid.NewGuid().ToString("N"));
        var campaignId = FirstNonEmpty(request.CampaignId, $"combat_smoke_campaign_{DateTime.UtcNow:yyyyMMddHHmmss}");
        var sessionId = FirstNonEmpty(request.SessionId, $"combat_smoke_session_{DateTime.UtcNow:yyyyMMddHHmmss}");
        var ruleSetId = FirstNonEmpty(request.RuleSetId, RuleSetIds.FantasyNriDefault);
        var attackerId = string.Empty;
        var targetId = string.Empty;

        try
        {
            var created = await _encounters.CreateEncounterAsync(new CombatEncounterCreateRequest
            {
                CampaignId = campaignId,
                SessionId = sessionId,
                RuleSetId = ruleSetId,
                Name = "Combat MVP Smoke",
                Tags = new List<string> { "smoke", "foundation_0_10_1" },
                RequestId = requestId
            }, actor);
            result.CreatedEncounterId = created.EncounterId;
            AddStep(result, "create_encounter", true, created.EncounterId);

            var attacker = await _encounters.AddParticipantAsync(new CombatParticipantAddRequest
            {
                EncounterId = created.EncounterId,
                DisplayName = "Smoke Attacker",
                ParticipantType = CombatParticipantTypes.Npc,
                TeamId = "team_a",
                IsNpc = true,
                IsPlayerControlled = false,
                Initiative = 15,
                RequestId = requestId
            }, actor);
            attackerId = attacker.Id;
            AddStep(result, "add_attacker", true, attackerId);

            var target = await _encounters.AddParticipantAsync(new CombatParticipantAddRequest
            {
                EncounterId = created.EncounterId,
                DisplayName = "Smoke Target",
                ParticipantType = CombatParticipantTypes.Npc,
                TeamId = "team_b",
                IsNpc = true,
                IsPlayerControlled = false,
                Initiative = 10,
                RequestId = requestId
            }, actor);
            targetId = target.Id;
            AddStep(result, "add_target", true, targetId);

            await Step(result, "set_target_vitals", () => _damage.SetParticipantVitalsAsync(new CombatParticipantVitalsSetRequest { EncounterId = created.EncounterId, ParticipantId = targetId, MaxHealth = 20, CurrentHealth = 20, Reason = "smoke", RequestId = requestId }, actor));
            await Step(result, "sort_initiative", () => _turns.SortInitiativeAsync(new CombatInitiativeSortRequest { EncounterId = created.EncounterId, RequestId = requestId }, actor));
            await Step(result, "start_round", () => _turns.StartRoundAsync(new CombatRoundStartRequest { EncounterId = created.EncounterId, RoundNumber = 1, RequestId = requestId }, actor));
            await Step(result, "start_turn", () => _turns.StartTurnAsync(new CombatTurnStartRequest { EncounterId = created.EncounterId, ParticipantId = attackerId, RequestId = requestId }, actor));
            await Step(result, "attack_roll", () => _attacks.DeclareAttackAsync(new CombatAttackDeclareRequest { EncounterId = created.EncounterId, ActorParticipantId = attackerId, TargetParticipantId = targetId, AttackBonus = 5, TargetDefenseOverride = 10, RequestId = requestId }, actor));
            await Step(result, "defense_preview", () => _defense.BuildDefensePreviewAsync(new CombatDefenseCalculationRequest { EncounterId = created.EncounterId, TargetParticipantId = targetId, AttackerParticipantId = attackerId, TargetDefenseOverride = 10, RequestId = requestId }, actor));
            await Step(result, "damage_apply", () => _damage.ApplyDamageAsync(new CombatDamageApplyRequest { EncounterId = created.EncounterId, AttackerParticipantId = attackerId, TargetParticipantId = targetId, DamageAmount = 5, DamageType = "physical", Reason = "smoke", RequestId = requestId }, actor));
            var condition = await _conditions.ApplyConditionAsync(new CombatConditionApplyRequest { EncounterId = created.EncounterId, TargetParticipantId = targetId, SourceParticipantId = attackerId, ConditionDefinitionId = "wounded", StackCount = 1, DurationMode = "until_removed", RequestId = requestId }, actor);
            AddStep(result, "condition_apply", true, condition.Condition.ConditionInstanceId);
            await Step(result, "condition_list", () => _conditions.ListConditionsAsync(new CombatConditionListRequest { EncounterId = created.EncounterId, ParticipantId = targetId, RequestId = requestId }, actor));
            await Step(result, "condition_remove", () => _conditions.RemoveConditionAsync(new CombatConditionRemoveRequest { EncounterId = created.EncounterId, TargetParticipantId = targetId, ConditionInstanceId = condition.Condition.ConditionInstanceId, Reason = "smoke cleanup", RequestId = requestId }, actor));
            await Step(result, "weapon_attack_preview", () => _weapons.ExecuteWeaponAttackAsync(new CombatWeaponAttackRequest { EncounterId = created.EncounterId, ActorParticipantId = attackerId, TargetParticipantId = targetId, WeaponDefinitionId = "short_sword", DamageOverride = 1, AutoApplyDamage = false, RequestId = requestId }, actor));
            await Step(result, "logs_list", () => _logs.ListLogsAsync(new CombatLogListRequest { EncounterId = created.EncounterId, Limit = 100, RequestId = requestId }, actor));
            await Step(result, "snapshot_full", () => _snapshots.BuildFullSnapshotAsync(new CombatFullSnapshotRequest { EncounterId = created.EncounterId, IncludeParticipants = true, IncludeTurns = true, IncludeRounds = true, IncludeActions = true, IncludeLogs = true, RequestId = requestId }, actor));
            await Step(result, "end_encounter", () => _encounters.EndEncounterAsync(new CombatEncounterEndRequest { EncounterId = created.EncounterId, Reason = "smoke complete", RequestId = requestId }, actor));
        }
        catch (Exception ex)
        {
            AddStep(result, "smoke_failed", false, ex.Message);
        }

        result.Errors = result.Steps.SelectMany(x => x.Errors).ToList();
        result.Warnings = result.Steps.SelectMany(x => x.Warnings).ToList();
        result.Success = result.Errors.Count == 0 && result.Steps.Count > 0 && result.Steps.All(x => x.Success);
        return result;
    }

    private static async Task Step<T>(CombatMvpSmokeResult result, string name, Func<Task<T>> action)
    {
        try
        {
            await action();
            AddStep(result, name, true, "ok");
        }
        catch (Exception ex)
        {
            AddStep(result, name, false, ex.Message);
        }
    }

    private static void AddStep(CombatMvpSmokeResult result, string stepName, bool success, string message)
    {
        var step = new CombatMvpSmokeStepResult
        {
            StepName = stepName,
            Success = success,
            Message = message ?? string.Empty
        };
        if (!success) step.Errors.Add(message ?? "failed");
        result.Steps.Add(step);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        return string.Empty;
    }
}
