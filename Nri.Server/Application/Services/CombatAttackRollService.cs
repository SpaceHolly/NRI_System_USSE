using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatAttackRollService
{
    Task<CombatAttackResultResponse> DeclareAttackAsync(CombatAttackDeclareRequest request, UserAccount actor);
    Task<CombatAttackRollComputation> RollAttackAsync(CombatAttackDeclareRequest request, CombatParticipantState attacker, CombatParticipantState target, UserAccount? actor = null);
    int CalculateAttackTotal(int naturalRoll, CombatAttackModifierBreakdown modifiers);
    int CalculateTargetDefense(CombatAttackDeclareRequest request, CombatParticipantState target, List<string> warnings);
    string ClassifyHitResult(int naturalRoll, int attackTotal, int targetDefense, bool criticalRulesEnabled);
    CombatActionState BuildAttackActionState(CombatAttackDeclareRequest request, CombatEncounterState encounter, CombatParticipantState attacker, CombatAttackRollComputation roll, UserAccount actor);
    string BuildAttackLogMessage(CombatParticipantState attacker, CombatParticipantState target, string hitResult);
}

public sealed class CombatAttackRollService : ICombatAttackRollService
{
    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICombatActionRepository _actions;
    private readonly ICombatLogWriter _logWriter;
    private readonly ICombatSnapshotService _snapshotService;
    private readonly ICombatPayloadSummaryBuilder _payloadSummaryBuilder;
    private readonly ICombatActionEconomyService _actionEconomyService;
    private readonly IItemEquipmentDefinitionResolver _definitionResolver;
    private readonly ICombatDefenseCalculationService? _defenseCalculationService;
    private readonly ICombatFateHookService? _fateHookService;
    private readonly IServerLogger _logger;

    public CombatAttackRollService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        ICombatActionRepository actions,
        ICombatLogWriter logWriter,
        ICombatSnapshotService snapshotService,
        ICombatPayloadSummaryBuilder payloadSummaryBuilder,
        ICombatActionEconomyService actionEconomyService,
        IItemEquipmentDefinitionResolver definitionResolver,
        IServerLogger logger,
        ICombatDefenseCalculationService? defenseCalculationService = null,
        ICombatFateHookService? fateHookService = null)
    {
        _encounters = encounters;
        _participants = participants;
        _actions = actions;
        _logWriter = logWriter;
        _snapshotService = snapshotService;
        _payloadSummaryBuilder = payloadSummaryBuilder;
        _actionEconomyService = actionEconomyService;
        _definitionResolver = definitionResolver;
        _defenseCalculationService = defenseCalculationService;
        _fateHookService = fateHookService;
        _logger = logger;
    }

    private static bool AttackDefenseIntegrationEnabled => CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDefenseMvp))
        && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAttackDefenseIntegration));

    public async Task<CombatAttackResultResponse> DeclareAttackAsync(CombatAttackDeclareRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        _logger.Debug($"combat.attack.roll.start encounterId={request.EncounterId} actor={request.ActorParticipantId} target={request.TargetParticipantId}");

        var encounter = await RequireEncounterAsync(request.EncounterId);
        if (!string.Equals(encounter.Status, CombatRuntimeStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("encounter_not_active");

        var attacker = await RequireParticipantAsync(request.ActorParticipantId, encounter.Id, "attacker");
        var target = await RequireParticipantAsync(request.TargetParticipantId, encounter.Id, "target");
        EnsureParticipantCanAttack(attacker, "attacker");
        EnsureParticipantCanAttack(target, "target");
        if (!string.IsNullOrWhiteSpace(encounter.ActiveParticipantId)
            && !string.Equals(encounter.ActiveParticipantId, attacker.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("not_active_turn_participant");

        var warnings = new List<string>();
        await ResolveWeaponWarningsAsync(request, encounter.RuleSetId, warnings);
        var roll = await RollAttackAsync(request, attacker, target, actor);
        warnings.AddRange(roll.Warnings);

        var action = BuildAttackActionState(request, encounter, attacker, roll, actor);
        action.PayloadSummary = _payloadSummaryBuilder.BuildLogPayloadSummary(CombatEventTypes.AttackResolved, action.PayloadSummary);
        await _actions.AppendAsync(action);

        if (request.SpendActionPoint)
        {
            if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatActionPointSpending)))
            {
                await _actionEconomyService.SpendActionPointsAsync(new CombatActionSpendRequest
                {
                    EncounterId = encounter.Id,
                    ParticipantId = attacker.Id,
                    ActionPointCost = 1,
                    Reason = "Attack action point cost.",
                    RequestId = request.RequestId
                }, actor);
            }
            else
            {
                warnings.Add("action_point_spending_disabled");
            }
        }

        var message = BuildAttackLogMessage(attacker, target, roll.HitResult);
        await WriteAttackLogAsync(encounter, attacker.Id, target.Id, action.Id, roll, message, request.RequestId);
        var snapshot = await _snapshotService.BuildFullSnapshotAsync(new CombatFullSnapshotRequest
        {
            EncounterId = encounter.Id,
            IncludeParticipants = true,
            IncludeTurns = true,
            IncludeRounds = true,
            IncludeActions = true,
            IncludeLogs = true,
            LimitActions = 100,
            LimitLogs = 100
        }, actor);

        _logger.Debug($"combat.attack.roll.done encounterId={encounter.Id} result={roll.HitResult} naturalRoll={roll.NaturalRoll}");
        return new CombatAttackResultResponse
        {
            EncounterId = encounter.Id,
            ActionId = action.Id,
            ActorParticipantId = attacker.Id,
            TargetParticipantId = target.Id,
            WeaponDefinitionId = request.WeaponDefinitionId ?? string.Empty,
            Roll = roll.Roll,
            NaturalRoll = roll.NaturalRoll,
            AttackTotal = roll.AttackTotal,
            TargetDefense = roll.TargetDefense,
            HitResult = roll.HitResult,
            IsHit = roll.IsHit,
            IsCritical = roll.IsCritical,
            IsFumble = roll.IsFumble,
            IsNaturalCritical = roll.IsNaturalCritical,
            IsNaturalFumble = roll.IsNaturalFumble,
            Modifiers = roll.Modifiers,
            Fate = CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateBreakdownInResponse)) ? roll.Fate : new CombatFateHookResult(),
            Message = message,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Snapshot = snapshot
        };
    }

    public async Task<CombatAttackRollComputation> RollAttackAsync(CombatAttackDeclareRequest request, CombatParticipantState attacker, CombatParticipantState target, UserAccount? actor = null)
    {
        var warnings = new List<string>();
        var naturalRoll = RollD20();
        var fateModifier = 0;
        var fate = new CombatFateHookResult
        {
            RollContext = "attack_roll",
            BaseRoll = naturalRoll,
            FateModifiedRoll = naturalRoll
        };
        if (request.UseFateEngine)
        {
            if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateHookMvp)) && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateAttackModifier)) && _fateHookService != null)
            {
                fate = await _fateHookService.ApplyFateToAttackRollAsync(new CombatFateHookRequest
                {
                    EncounterId = request.EncounterId ?? string.Empty,
                    RollContext = "attack_roll",
                    ActorParticipantId = attacker?.Id ?? string.Empty,
                    TargetParticipantId = target?.Id ?? string.Empty,
                    BaseRoll = naturalRoll,
                    DiceExpression = "1d20",
                    UseFateEngine = request.UseFateEngine,
                    RequestId = request.RequestId ?? string.Empty
                }, actor);
                fateModifier = fate.FateModifier;
                warnings.AddRange(fate.Warnings);
            }
            else
            {
                warnings.Add("fate_roll_hook_disabled");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.AttackSkillId) || !string.IsNullOrWhiteSpace(request.AttackAttributeId))
            warnings.Add("skill_attribute_bonus_not_resolved");

        var modifiers = new CombatAttackModifierBreakdown
        {
            AttackBonus = request.AttackBonus,
            WeaponAccuracyBonus = request.WeaponAccuracyBonus,
            SkillBonus = 0,
            AttributeBonus = 0,
            DistanceModifier = CalculateDistanceModifier(request.DistanceMeters),
            CoverModifier = request.CoverModifier,
            SituationalModifier = request.SituationalModifier,
            FateModifier = fateModifier
        };
        modifiers.TotalModifier = modifiers.AttackBonus
            + modifiers.WeaponAccuracyBonus
            + modifiers.SkillBonus
            + modifiers.AttributeBonus
            + modifiers.DistanceModifier
            + modifiers.CoverModifier
            + modifiers.SituationalModifier
            + modifiers.FateModifier;

        var targetDefense = CalculateTargetDefense(request, target, warnings);
        if (AttackDefenseIntegrationEnabled && _defenseCalculationService != null)
        {
            var defenseRequest = new CombatDefenseCalculationRequest
            {
                EncounterId = request.EncounterId ?? string.Empty,
                TargetParticipantId = target.Id,
                AttackerParticipantId = attacker.Id,
                AttackType = CombatActionTypes.Attack,
                WeaponDefinitionId = request.WeaponDefinitionId ?? string.Empty,
                DistanceMeters = request.DistanceMeters,
                CoverModifierOverride = request.CoverModifier == 0 ? (int?)null : request.CoverModifier,
                TargetDefenseOverride = request.TargetDefenseOverride,
                IncludeArmor = true,
                IncludeShield = true,
                IncludeCover = true,
                IncludeDistance = true,
                RequestId = request.RequestId ?? string.Empty
            };
            var defense = await _defenseCalculationService.CalculateDefenseAsync(defenseRequest, null);
            warnings.AddRange(defense.Warnings);
            warnings.AddRange(defense.Errors);
            if (defense.Errors.Count == 0)
            {
                var distance = _defenseCalculationService.CalculateDistanceModifier(defenseRequest);
                modifiers.DistanceModifier = distance.AttackModifier;
                modifiers.CoverModifier = 0;
                modifiers.TotalModifier = modifiers.AttackBonus
                    + modifiers.WeaponAccuracyBonus
                    + modifiers.SkillBonus
                    + modifiers.AttributeBonus
                    + modifiers.DistanceModifier
                    + modifiers.CoverModifier
                    + modifiers.SituationalModifier
                    + modifiers.FateModifier;
                targetDefense = defense.TargetDefense;
                _logger.Debug($"combat.attack.defense.integration.used encounterId={request.EncounterId}");
            }
        }

        var attackTotal = CalculateAttackTotal(naturalRoll, modifiers);
        var result = ClassifyHitResult(naturalRoll, attackTotal, targetDefense, CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatCriticalRulesMvp)));
        return new CombatAttackRollComputation
        {
            NaturalRoll = naturalRoll,
            Roll = naturalRoll + fateModifier,
            AttackTotal = attackTotal,
            TargetDefense = targetDefense,
            HitResult = result,
            IsHit = string.Equals(result, CombatHitResultIds.Hit, StringComparison.OrdinalIgnoreCase)
                || string.Equals(result, CombatHitResultIds.CriticalHit, StringComparison.OrdinalIgnoreCase),
            IsCritical = string.Equals(result, CombatHitResultIds.CriticalHit, StringComparison.OrdinalIgnoreCase),
            IsFumble = string.Equals(result, CombatHitResultIds.Fumble, StringComparison.OrdinalIgnoreCase),
            IsNaturalCritical = naturalRoll == 20,
            IsNaturalFumble = naturalRoll == 1,
            Modifiers = modifiers,
            Fate = fate,
            Warnings = warnings
        };
    }

    public int CalculateAttackTotal(int naturalRoll, CombatAttackModifierBreakdown modifiers)
    {
        return naturalRoll + (modifiers?.TotalModifier ?? 0);
    }

    public int CalculateTargetDefense(CombatAttackDeclareRequest request, CombatParticipantState target, List<string> warnings)
    {
        if (request.TargetDefenseOverride.HasValue) return Math.Max(0, request.TargetDefenseOverride.Value);
        warnings?.Add("target_defense_defaulted");
        return 10;
    }

    public string ClassifyHitResult(int naturalRoll, int attackTotal, int targetDefense, bool criticalRulesEnabled)
    {
        if (criticalRulesEnabled && naturalRoll == 1) return CombatHitResultIds.Fumble;
        if (criticalRulesEnabled && naturalRoll == 20) return CombatHitResultIds.CriticalHit;
        return attackTotal >= targetDefense ? CombatHitResultIds.Hit : CombatHitResultIds.Miss;
    }

    public CombatActionState BuildAttackActionState(CombatAttackDeclareRequest request, CombatEncounterState encounter, CombatParticipantState attacker, CombatAttackRollComputation roll, UserAccount actor)
    {
        return new CombatActionState
        {
            Id = Guid.NewGuid().ToString("N"),
            EncounterId = encounter.Id,
            RoundNumber = Math.Max(0, encounter.RoundNumber),
            TurnIndex = Math.Max(0, encounter.ActiveTurnIndex),
            ActorParticipantId = attacker.Id,
            ActionType = CombatActionTypes.Attack,
            ActionName = "Attack",
            TargetParticipantIds = new List<string> { request.TargetParticipantId ?? string.Empty }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
            ActionPointCost = request.SpendActionPoint ? 1 : 0,
            Status = CombatActionStatuses.Resolved,
            RequestId = request.RequestId ?? string.Empty,
            ActorUserId = actor?.Id ?? string.Empty,
            CreatedAtUtc = DateTime.UtcNow,
            PayloadSummary = BuildAttackPayload(request, roll),
            Notes = string.Empty
        };
    }

    public string BuildAttackLogMessage(CombatParticipantState attacker, CombatParticipantState target, string hitResult)
    {
        var attackerName = string.IsNullOrWhiteSpace(attacker.DisplayName) ? attacker.Id : attacker.DisplayName;
        var targetName = string.IsNullOrWhiteSpace(target.DisplayName) ? target.Id : target.DisplayName;
        return $"{attackerName} attacks {targetName}: {hitResult}.";
    }

    private async Task ResolveWeaponWarningsAsync(CombatAttackDeclareRequest request, string ruleSetId, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(request.WeaponDefinitionId)) return;
        try
        {
            var resolved = await _definitionResolver.ResolveWeaponAsync(request.WeaponDefinitionId, ruleSetId ?? string.Empty);
            if (!resolved.Success || resolved.Value == null)
            {
                warnings.Add("weapon_definition_missing");
                return;
            }

            if (!string.IsNullOrWhiteSpace(request.AttackSkillId)
                && resolved.Value.LinkedSkillIds != null
                && resolved.Value.LinkedSkillIds.Count > 0
                && !resolved.Value.LinkedSkillIds.Contains(request.AttackSkillId, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add("attack_skill_not_linked_to_weapon");
            }
        }
        catch
        {
            warnings.Add("weapon_definition_missing");
        }
    }

    private async Task<CombatEncounterState> RequireEncounterAsync(string encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterId)) throw new ArgumentException("encounter_missing");
        var encounter = await _encounters.GetByIdAsync(encounterId);
        if (encounter == null) throw new KeyNotFoundException("encounter_missing");
        return encounter;
    }

    private async Task<CombatParticipantState> RequireParticipantAsync(string participantId, string encounterId, string role)
    {
        if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException(role == "target" ? "target_missing" : "attacker_missing");
        var participant = await _participants.GetByIdAsync(participantId);
        if (participant == null || !string.Equals(participant.EncounterId, encounterId, StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException(role == "target" ? "target_missing" : "attacker_missing");
        return participant;
    }

    private static void EnsureParticipantCanAttack(CombatParticipantState participant, string role)
    {
        if (!participant.IsActive) throw new InvalidOperationException(role == "target" ? "target_inactive" : "attacker_inactive");
        if (participant.IsDefeated) throw new InvalidOperationException(role == "target" ? "target_defeated" : "attacker_defeated");
    }

    private async Task WriteAttackLogAsync(CombatEncounterState encounter, string actorParticipantId, string targetParticipantId, string actionId, CombatAttackRollComputation roll, string message, string requestId)
    {
        var payload = new Dictionary<string, object>
        {
            { "actionId", actionId ?? string.Empty },
            { "actorParticipantId", actorParticipantId ?? string.Empty },
            { "targetParticipantId", targetParticipantId ?? string.Empty },
            { "naturalRoll", roll.NaturalRoll },
            { "attackTotal", roll.AttackTotal },
            { "targetDefense", roll.TargetDefense },
            { "hitResult", roll.HitResult }
        };
        if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateLogging)) && roll.Fate.Applied)
        {
            payload["fateApplied"] = true;
            payload["fateModifier"] = roll.Fate.FateModifier;
            payload["fateSummary"] = roll.Fate.FateSummary ?? string.Empty;
            message = $"{message} Fate modifier: {FormatSigned(roll.Fate.FateModifier)}.";
        }
        await _logWriter.AppendLogAndReplayAsync(
            new CombatLogWriteRequest
            {
                EncounterId = encounter.Id,
                CampaignId = encounter.CampaignId,
                SessionId = encounter.SessionId,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = actorParticipantId ?? string.Empty,
                EventType = CombatEventTypes.AttackResolved,
                Message = message ?? string.Empty,
                SourcePayload = payload,
                Visibility = CombatVisibilityIds.Public,
                RequestId = requestId ?? string.Empty
            },
            new CombatReplayWriteRequest
            {
                EncounterId = encounter.Id,
                EventType = CombatEventTypes.AttackResolved,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = actorParticipantId ?? string.Empty,
                SourcePayload = payload,
                Visibility = CombatVisibilityIds.Public,
                RequestId = requestId ?? string.Empty
            });
    }

    private static Dictionary<string, object> BuildAttackPayload(CombatAttackDeclareRequest request, CombatAttackRollComputation roll)
    {
        return new Dictionary<string, object>
        {
            { "naturalRoll", roll.NaturalRoll },
            { "roll", roll.Roll },
            { "attackTotal", roll.AttackTotal },
            { "targetDefense", roll.TargetDefense },
            { "hitResult", roll.HitResult },
            { "weaponDefinitionId", request.WeaponDefinitionId ?? string.Empty },
            { "fateApplied", roll.Fate.Applied },
            { "fateModifier", roll.Fate.FateModifier },
            { "fateSummary", CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateLogging)) ? roll.Fate.FateSummary ?? string.Empty : string.Empty },
            { "modifiers", new Dictionary<string, object>
                {
                    { "attackBonus", roll.Modifiers.AttackBonus },
                    { "weaponAccuracyBonus", roll.Modifiers.WeaponAccuracyBonus },
                    { "skillBonus", roll.Modifiers.SkillBonus },
                    { "attributeBonus", roll.Modifiers.AttributeBonus },
                    { "distanceModifier", roll.Modifiers.DistanceModifier },
                    { "coverModifier", roll.Modifiers.CoverModifier },
                    { "situationalModifier", roll.Modifiers.SituationalModifier },
                    { "fateModifier", roll.Modifiers.FateModifier },
                    { "totalModifier", roll.Modifiers.TotalModifier }
                }
            }
        };
    }

    private static int CalculateDistanceModifier(decimal? distanceMeters)
    {
        if (!distanceMeters.HasValue) return 0;
        var distance = distanceMeters.Value;
        if (distance < 0) return 0;
        if (distance <= 10) return 0;
        if (distance <= 30) return -1;
        if (distance <= 60) return -2;
        return -4;
    }

    private static int RollD20()
    {
        var bytes = new byte[4];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var value = BitConverter.ToUInt32(bytes, 0);
        return (int)(value % 20) + 1;
    }

    private static string FormatSigned(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }
}
