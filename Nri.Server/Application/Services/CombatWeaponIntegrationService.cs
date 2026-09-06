using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nri.Server.Application;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatDamageRoller
{
    int RollDamage(string damageDraft);
    bool TryParseDamageDraft(string damageDraft, out int rolledDamage);
    bool TryParseDamageModifier(string modifierDraft, out int modifier);
}

public sealed class CombatDamageRoller : ICombatDamageRoller
{
    private static readonly Regex DiceRegex = new Regex(@"^\s*(\d*)d(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StructuredDiceRegex = new Regex(@"^\s*(\d+)\s*\(\s*d(\d+)\s*([+-]\s*\d+)?\s*\)\s*([+-]\s*\d+)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public int RollDamage(string damageDraft)
    {
        return TryParseDamageDraft(damageDraft, out var rolledDamage) ? rolledDamage : 0;
    }

    public bool TryParseDamageDraft(string damageDraft, out int rolledDamage)
    {
        rolledDamage = 0;
        var value = (damageDraft ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flat))
        {
            rolledDamage = Math.Max(0, flat);
            return true;
        }

        var structured = StructuredDiceRegex.Match(value);
        if (structured.Success)
        {
            var structuredCount = int.Parse(structured.Groups[1].Value, CultureInfo.InvariantCulture);
            var structuredSides = int.Parse(structured.Groups[2].Value, CultureInfo.InvariantCulture);
            var perDie = string.IsNullOrWhiteSpace(structured.Groups[3].Value) ? 0 : int.Parse(structured.Groups[3].Value.Replace(" ", string.Empty), CultureInfo.InvariantCulture);
            var totalModifier = string.IsNullOrWhiteSpace(structured.Groups[4].Value) ? 0 : int.Parse(structured.Groups[4].Value.Replace(" ", string.Empty), CultureInfo.InvariantCulture);
            if (structuredCount < 1 || structuredCount > 1000 || structuredSides < 2 || structuredSides > 1000000) return false;
            rolledDamage = DamageExpressionRules022Gate2.Roll(new DamageExpressionDefinition { DiceCount = structuredCount, DieSides = structuredSides, PerDieModifier = perDie, TotalModifier = totalModifier }, RollDie).TotalDamage;
            return true;
        }

        var match = DiceRegex.Match(value);
        if (!match.Success) return false;

        var countText = match.Groups[1].Value;
        var count = string.IsNullOrWhiteSpace(countText) ? 1 : int.Parse(countText, CultureInfo.InvariantCulture);
        var sides = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        if (count < 1 || count > 1000 || sides < 2 || sides > 1000000) return false;

        var total = 0;
        for (var i = 0; i < count; i++)
            total += RollDie(sides);
        rolledDamage = total;
        return true;
    }

    public bool TryParseDamageModifier(string modifierDraft, out int modifier)
    {
        modifier = 0;
        var value = (modifierDraft ?? string.Empty).Trim();
        return !string.IsNullOrWhiteSpace(value)
            && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out modifier);
    }

    private static int RollDie(int sides)
    {
        var bytes = new byte[4];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var value = BitConverter.ToUInt32(bytes, 0);
        return (int)(value % sides) + 1;
    }
}

public interface ICombatWeaponIntegrationService
{
    Task<CombatWeaponAttackResponse> ExecuteWeaponAttackAsync(CombatWeaponAttackRequest request, UserAccount actor);
    Task<InventoryItemInstanceState?> ResolveEquippedWeaponAsync(CombatParticipantState actorParticipant, CombatWeaponAttackRequest request);
    Task<InventoryItemInstanceState?> ResolveAmmoAsync(CombatParticipantState actorParticipant, WeaponDefinitionView? weapon, CombatWeaponAttackRequest request);
    Task<bool> ValidateWeaponAmmoCompatibilityAsync(WeaponDefinitionView? weapon, AmmoDefinitionView? ammo, CombatWeaponAttackRequest request, List<string> warnings);
    Task<CombatDamagePreview> CalculateDamagePreviewAsync(WeaponDefinitionView? weapon, AmmoDefinitionView? ammo, CombatAttackResultResponse attackResult, CombatWeaponAttackRequest request, List<string> warnings);
    string BuildWeaponAttackLogMessage(CombatParticipantState attacker, CombatParticipantState target, CombatWeaponCombatSummary weapon, CombatAttackResultResponse attackResult, CombatDamagePreview preview, bool damageApplied);
}

public sealed class CombatWeaponIntegrationService : ICombatWeaponIntegrationService
{
    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICombatLogRepository _logs;
    private readonly CharacterProfileService _profiles;
    private readonly IItemEquipmentDefinitionResolver _definitionResolver;
    private readonly ICombatAttackRollService _attackRollService;
    private readonly ICombatDamageApplicationService _damageApplicationService;
    private readonly ICombatSnapshotService _snapshotService;
    private readonly ICombatLogWriter _logWriter;
    private readonly ICombatDamageRoller _damageRoller;
    private readonly ICombatFateHookService? _fateHookService;
    private readonly ICombatDefenseCalculationService _defenseCalculationService;
    private readonly ICombatNaturalAttackAreaResolver022Gate2? _areaResolver;
    private readonly ICombatConditionPresentationResolver? _conditionPresentationResolver;
    private readonly IServerLogger _logger;

    public CombatWeaponIntegrationService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        ICombatLogRepository logs,
        CharacterProfileService profiles,
        IItemEquipmentDefinitionResolver definitionResolver,
        ICombatAttackRollService attackRollService,
        ICombatDamageApplicationService damageApplicationService,
        ICombatSnapshotService snapshotService,
        ICombatLogWriter logWriter,
        ICombatDamageRoller damageRoller,
        IServerLogger logger,
        ICombatFateHookService? fateHookService = null,
        ICombatDefenseCalculationService? defenseCalculationService = null,
        ICombatNaturalAttackAreaResolver022Gate2? areaResolver = null,
        ICombatConditionPresentationResolver? conditionPresentationResolver = null)
    {
        _encounters = encounters;
        _participants = participants;
        _logs = logs;
        _profiles = profiles;
        _definitionResolver = definitionResolver;
        _attackRollService = attackRollService;
        _damageApplicationService = damageApplicationService;
        _snapshotService = snapshotService;
        _logWriter = logWriter;
        _damageRoller = damageRoller;
        _fateHookService = fateHookService;
        _defenseCalculationService = defenseCalculationService ?? throw new ArgumentNullException(nameof(defenseCalculationService));
        _areaResolver = areaResolver;
        _conditionPresentationResolver = conditionPresentationResolver;
        _logger = logger;
    }

    public async Task<CombatWeaponAttackResponse> ExecuteWeaponAttackAsync(CombatWeaponAttackRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        _logger.Debug($"combat.weapon_attack.start encounterId={request.EncounterId} actor={request.ActorParticipantId} target={request.TargetParticipantId}");

        var encounter = await RequireEncounterAsync(request.EncounterId);
        var attacker = await RequireParticipantAsync(request.ActorParticipantId, encounter.Id, "attacker_missing");
        var target = await RequireParticipantAsync(request.TargetParticipantId, encounter.Id, "target_missing");
        EnsureActorCanControl(attacker, actor);
        EnsureParticipantCanAct(attacker, "attacker");
        EnsureParticipantCanAct(target, "target");

        var warnings = new List<string>();
        var weaponItem = await ResolveEquippedWeaponAsync(attacker, request);
        var weaponDefinitionId = FirstNonEmpty(weaponItem?.DefinitionId, request.WeaponDefinitionId);
        var weapon = await ResolveWeaponDefinitionAsync(weaponDefinitionId, encounter.RuleSetId, warnings);
        NaturalAttackDefinition? naturalAttack = null;
        if (!string.IsNullOrWhiteSpace(request.NaturalAttackId))
        {
            naturalAttack = ResolveNaturalAttack(attacker, request.NaturalAttackId);
            await EnsureNaturalAttackCooldownAsync(encounter, attacker, naturalAttack, request.RequestId);
            weapon = BuildNaturalWeaponView(naturalAttack);
            weaponDefinitionId = "natural_attack";
            warnings.Add("natural_attack_resolved_from_character_v2_body_profile");
        }
        if (weapon == null && !request.DamageOverride.HasValue)
            throw new InvalidOperationException("weapon_definition_required_for_damage_preview");

        if (weapon != null)
            _logger.Debug($"combat.weapon_attack.weapon_resolved definitionId={weapon.DefinitionId}");
        var attackProfile = ResolveAttackProfile(weapon, request.AttackProfileId, warnings);
        var effectiveWeapon = ApplyAttackProfile(weapon, attackProfile);

        var ammoItem = await ResolveAmmoAsync(attacker, effectiveWeapon, request);
        var ammoDefinitionId = FirstNonEmpty(ammoItem?.DefinitionId, request.AmmoDefinitionId);
        var ammo = await ResolveAmmoDefinitionAsync(ammoDefinitionId, encounter.RuleSetId, warnings);
        if (ammo != null)
            _logger.Debug($"combat.weapon_attack.ammo_resolved definitionId={ammo.DefinitionId}");

        await ValidateWeaponAmmoCompatibilityAsync(effectiveWeapon, ammo, request, warnings);
        AddDisabledSafetyWarnings(effectiveWeapon, ammo, warnings);

        var attackResult = await _attackRollService.DeclareAttackAsync(new CombatAttackDeclareRequest
        {
            EncounterId = encounter.Id,
            ActorParticipantId = attacker.Id,
            TargetParticipantId = target.Id,
            WeaponDefinitionId = weaponDefinitionId,
            AttackProfileId = attackProfile?.ProfileId ?? string.Empty,
            AttackSkillId = FirstNonEmpty(request.AttackSkillId, attackProfile?.SkillDefinitionId, effectiveWeapon?.LinkedSkillIds?.FirstOrDefault()),
            AttackAttributeId = FirstNonEmpty(request.AttackAttributeId, attackProfile?.SubAttributeDefinitionId, effectiveWeapon?.AttributeHints?.FirstOrDefault()),
            AttackBonus = request.AttackBonus,
            WeaponAccuracyBonus = 0,
            DistanceMeters = request.DistanceMeters,
            CoverModifier = request.CoverModifier,
            SituationalModifier = request.SituationalModifier,
            UseFateEngine = request.UseFateEngine && (naturalAttack?.FateEligibleForHitCheck ?? true),
            SpendActionPoint = request.SpendActionPoint,
            RequestId = request.RequestId ?? string.Empty
        }, actor);
        warnings.AddRange(attackResult.Warnings);

        var damagePreview = new CombatDamagePreview
        {
            DamageType = NormalizeDamageType(request.DamageType),
            CriticalMultiplier = 1
        };
        var damageResult = new CombatDamageResultResponse
        {
            EncounterId = encounter.Id,
            SourceActionId = attackResult.ActionId,
            AttackerParticipantId = attacker.Id,
            TargetParticipantId = target.Id,
            DamageType = damagePreview.DamageType
        };

        var damageApplied = false;
        var isAreaNaturalAttack = naturalAttack != null && !string.Equals(naturalAttack.AreaShape, "single", StringComparison.OrdinalIgnoreCase);
        if (isAreaNaturalAttack)
            request.TargetProtectionZone = ResolveAreaTargetBodyZone(target, attackResult.ActionId);
        var sharedAreaBaseDamage = 0;
        CombatDamagePreview? sharedAreaPrimaryPreview = null;
        if (isAreaNaturalAttack && !attackResult.AlreadyApplied)
        {
            sharedAreaPrimaryPreview = await CalculateDamagePreviewAsync(effectiveWeapon, ammo, attackResult, request, warnings);
            sharedAreaBaseDamage = sharedAreaPrimaryPreview.BaseDamage;
            if (!attackResult.IsHit) damagePreview = sharedAreaPrimaryPreview;
        }
        if (attackResult.IsHit && !attackResult.AlreadyApplied)
        {
            damagePreview = sharedAreaPrimaryPreview
                ?? await CalculateDamagePreviewAsync(effectiveWeapon, ammo, attackResult, request, warnings);
            if (request.AutoApplyDamage)
            {
                if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAttackDamageAutoApply)))
                {
                    damageResult = await _damageApplicationService.ApplyDamageAsync(new CombatDamageApplyRequest
                    {
                        EncounterId = encounter.Id,
                        SourceActionId = attackResult.ActionId,
                        AttackerParticipantId = attacker.Id,
                        TargetParticipantId = target.Id,
                        DamageAmount = damagePreview.FinalDamage,
                        DamageType = damagePreview.DamageType,
                        DamageSource = naturalAttack == null ? "weapon_attack" : "natural_attack",
                        IsCriticalDamage = attackResult.IsCritical,
                        AllowAutoDefeat = true,
                        Reason = "weapon_attack",
                        RequestId = request.RequestId ?? string.Empty
                    }, actor);
                    damageApplied = damageResult.DamageApplied > 0;
                    warnings.AddRange(damageResult.Warnings);
                }
                else
                {
                    warnings.Add("attack_damage_auto_apply_disabled");
                }
            }
            if (naturalAttack != null && damagePreview.IsPenetrated && damagePreview.FinalDamage > 0
                && !string.IsNullOrWhiteSpace(naturalAttack.AppliedConditionId) && naturalAttack.AppliedConditionRounds > 0)
            {
                await ApplyNaturalAttackConditionAsync(encounter, attacker, target, naturalAttack, attackResult.ActionId);
            }
        }
        else
        {
            damagePreview.FinalDamage = 0;
            if (attackResult.AlreadyApplied) warnings.Add("weapon_attack_idempotent_replay_no_damage_reapply");
        }

        var areaTargetResults = new List<CombatAreaTargetResult022Gate2>
        {
            new CombatAreaTargetResult022Gate2
            {
                TargetParticipantId = target.Id,
                TargetDisplayName = target.DisplayName,
                IsHit = attackResult.IsHit,
                AttackTotal = attackResult.AttackTotal,
                TargetDefense = attackResult.TargetDefense,
                DamagePreview = damagePreview,
                DamageResult = damageResult
            }
        };
        if (isAreaNaturalAttack && !attackResult.AlreadyApplied)
        {
            if (_areaResolver == null) throw new InvalidOperationException("natural_attack_area_resolver_unavailable");
            var resolvedTargets = await _areaResolver.ResolveTargetsAsync(encounter, attacker, target, naturalAttack!);
            if (!resolvedTargets.Any(v => string.Equals(v.Id, target.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("natural_attack_area_primary_target_outside_area");
            foreach (var areaTarget in resolvedTargets.Where(v => !string.Equals(v.Id, target.Id, StringComparison.OrdinalIgnoreCase)))
            {
                var areaDefense = await _defenseCalculationService.CalculateDefenseAsync(new CombatDefenseCalculationRequest
                {
                    EncounterId = encounter.Id,
                    TargetParticipantId = areaTarget.Id,
                    AttackerParticipantId = attacker.Id,
                    WeaponDefinitionId = weaponDefinitionId,
                    IncludeArmor = true,
                    IncludeShield = true,
                    IncludeCover = false,
                    IncludeDistance = false,
                    RequestId = request.RequestId ?? string.Empty
                }, null);
                warnings.AddRange(areaDefense.Warnings);
                warnings.AddRange(areaDefense.Errors);
                var areaHit = !attackResult.IsNaturalFumble && (attackResult.IsNaturalCritical || attackResult.AttackTotal >= areaDefense.TargetDefense);
                var areaAttack = CopyAreaAttackResult(attackResult, areaTarget.Id, areaDefense.TargetDefense, areaHit);
                var areaRequest = CopyAreaDamageRequest(
                    request,
                    areaTarget.Id,
                    sharedAreaBaseDamage,
                    ResolveAreaTargetBodyZone(areaTarget, attackResult.ActionId));
                var areaPreview = areaHit
                    ? await CalculateDamagePreviewAsync(effectiveWeapon, ammo, areaAttack, areaRequest, warnings)
                    : new CombatDamagePreview { BaseDamage = sharedAreaBaseDamage, FinalDamage = 0, DamageType = NormalizeDamageType(request.DamageType) };
                var areaDamage = new CombatDamageResultResponse
                {
                    EncounterId = encounter.Id,
                    SourceActionId = attackResult.ActionId,
                    AttackerParticipantId = attacker.Id,
                    TargetParticipantId = areaTarget.Id,
                    DamageType = areaPreview.DamageType
                };
                if (areaHit && request.AutoApplyDamage && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAttackDamageAutoApply)))
                {
                    areaDamage = await _damageApplicationService.ApplyDamageAsync(new CombatDamageApplyRequest
                    {
                        EncounterId = encounter.Id,
                        SourceActionId = attackResult.ActionId + ":" + areaTarget.Id,
                        AttackerParticipantId = attacker.Id,
                        TargetParticipantId = areaTarget.Id,
                        DamageAmount = areaPreview.FinalDamage,
                        DamageType = areaPreview.DamageType,
                        DamageSource = "natural_attack_area",
                        IsCriticalDamage = areaAttack.IsCritical,
                        AllowAutoDefeat = true,
                        Reason = "natural_attack_area",
                        RequestId = (request.RequestId ?? string.Empty) + ":" + areaTarget.Id
                    }, actor);
                }
                if (areaHit && areaPreview.IsPenetrated && areaPreview.FinalDamage > 0
                    && !string.IsNullOrWhiteSpace(naturalAttack!.AppliedConditionId) && naturalAttack.AppliedConditionRounds > 0)
                    await ApplyNaturalAttackConditionAsync(encounter, attacker, areaTarget, naturalAttack, attackResult.ActionId);
                areaTargetResults.Add(new CombatAreaTargetResult022Gate2
                {
                    TargetParticipantId = areaTarget.Id,
                    TargetDisplayName = areaTarget.DisplayName,
                    IsHit = areaHit,
                    AttackTotal = attackResult.AttackTotal,
                    TargetDefense = areaDefense.TargetDefense,
                    DamagePreview = areaPreview,
                    DamageResult = areaDamage
                });
            }
            warnings.Add($"natural_attack_area_targets_resolved:{areaTargetResults.Count}");
        }

        var weaponSummary = BuildWeaponSummary(weaponItem, effectiveWeapon, weaponDefinitionId, attackProfile);
        var ammoSummary = BuildAmmoSummary(ammoItem, ammo, ammoDefinitionId, ammo != null && effectiveWeapon != null && IsAmmoCompatible(effectiveWeapon, ammo));
        var penetrationResult = new CombatPenetrationResult0219
        {
            PenetrationType = damagePreview.PenetrationType,
            TotalPenetration = damagePreview.PenetrationValue,
            TargetProtection = damagePreview.ProtectionValue,
            EffectiveProtection = Math.Max(0, damagePreview.ProtectionValue - damagePreview.PenetrationValue),
            IsPenetrated = damagePreview.IsPenetrated
        };
        var message = BuildWeaponAttackLogMessage(attacker, target, weaponSummary, attackResult, damagePreview, damageApplied);
        await WriteWeaponAttackLogAsync(encounter, attacker.Id, target.Id, weaponSummary, ammoSummary, attackResult, damagePreview, damageApplied, message, request.RequestId ?? string.Empty, naturalAttack);

        var snapshot = await _snapshotService.BuildFullSnapshotAsync(new CombatFullSnapshotRequest
        {
            EncounterId = encounter.Id,
            IncludeParticipants = true,
            IncludeTurns = true,
            IncludeRounds = true,
            IncludeActions = true,
            IncludeLogs = true,
            IncludeReplayEvents = false,
            LimitActions = 100,
            LimitLogs = 100,
            RequestId = request.RequestId ?? string.Empty
        }, actor);

        _logger.Debug($"combat.weapon_attack.done hitResult={attackResult.HitResult} finalDamage={damagePreview.FinalDamage}");
        return new CombatWeaponAttackResponse
        {
            EncounterId = encounter.Id,
            AttackActionId = attackResult.ActionId,
            DamageActionId = damageResult.ActionId,
            ActorParticipantId = attacker.Id,
            TargetParticipantId = target.Id,
            WeaponDefinitionId = weaponDefinitionId,
            AttackProfileId = attackProfile?.ProfileId ?? string.Empty,
            AmmoDefinitionId = ammoDefinitionId,
            AttackResult = attackResult,
            DamageResult = damageResult,
            WeaponSummary = weaponSummary,
            AmmoSummary = ammoSummary,
            PenetrationResult = penetrationResult,
            DamagePreview = damagePreview,
            AreaTargetResults = areaTargetResults,
            Warnings = warnings.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Message = message,
            Snapshot = snapshot
        };
    }

    private static CombatAttackResultResponse CopyAreaAttackResult(CombatAttackResultResponse source, string targetParticipantId, int targetDefense, bool isHit)
        => new CombatAttackResultResponse
        {
            EncounterId = source.EncounterId, ActionId = source.ActionId, ActorParticipantId = source.ActorParticipantId,
            TargetParticipantId = targetParticipantId, WeaponDefinitionId = source.WeaponDefinitionId, AttackProfileId = source.AttackProfileId,
            Roll = source.Roll, NaturalRoll = source.NaturalRoll, AttackTotal = source.AttackTotal, TargetDefense = targetDefense,
            HitResult = isHit ? (source.IsCritical ? CombatHitResultIds.CriticalHit : CombatHitResultIds.Hit) : CombatHitResultIds.Miss,
            IsHit = isHit, IsCritical = isHit && source.IsCritical, IsFumble = source.IsFumble,
            IsNaturalCritical = source.IsNaturalCritical, IsNaturalFumble = source.IsNaturalFumble,
            DegreeOfSuccess = isHit ? CoreResolutionPolicy0219.ClassifyDegree(source.AttackTotal - targetDefense) : CoreResolutionDegreeIds.Failure,
            Modifiers = source.Modifiers, Fate = source.Fate
        };

    private static CombatWeaponAttackRequest CopyAreaDamageRequest(CombatWeaponAttackRequest source, string targetParticipantId, int sharedBaseDamage, string targetProtectionZone)
        => new CombatWeaponAttackRequest
        {
            EncounterId = source.EncounterId, ActorParticipantId = source.ActorParticipantId, TargetParticipantId = targetParticipantId,
            WeaponDefinitionId = source.WeaponDefinitionId, AttackProfileId = source.AttackProfileId, NaturalAttackId = source.NaturalAttackId,
            DamageOverride = sharedBaseDamage, DamageType = source.DamageType, TargetProtectionZone = targetProtectionZone,
            RequestId = source.RequestId
        };

    private string ResolveAreaTargetBodyZone(CombatParticipantState target, string actionId)
    {
        if (target == null || string.IsNullOrWhiteSpace(target.CharacterId)) return BodyZoneIds.Torso;
        var body = _profiles.GetBodyProfile(target.CharacterId);
        var zones = body?.BodyZones;
        if (zones == null || zones.Count == 0) zones = RacePhysiologyRules022Gate2.HumanoidZones();
        var unitRoll = StableUnitRoll022Gate2((actionId ?? string.Empty) + ":" + target.Id);
        return BodyZoneRules022Gate2.ResolveWeighted(zones, unitRoll).ZoneId;
    }

    private static decimal StableUnitRoll022Gate2(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return (hash & 0x00FFFFFF) / 16777216m;
        }
    }

    private NaturalAttackDefinition ResolveNaturalAttack(CombatParticipantState participant, string naturalAttackId)
    {
        if (participant == null || string.IsNullOrWhiteSpace(participant.CharacterId)) throw new InvalidOperationException("natural_attack_requires_character");
        var body = _profiles.GetBodyProfile(participant.CharacterId);
        return body?.NaturalAttacks?.FirstOrDefault(v => string.Equals(v.DefinitionId, naturalAttackId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("natural_attack_not_available");
    }

    private async Task EnsureNaturalAttackCooldownAsync(CombatEncounterState encounter, CombatParticipantState attacker, NaturalAttackDefinition attack, string requestId)
    {
        if (attack.CooldownRounds <= 0) return;
        var logs = await _logs.ListByEncounterAsync(encounter.Id, 500);
        var lastUse = logs.Where(v => string.Equals(v.EventType, CombatEventTypes.WeaponAttackResolved, StringComparison.Ordinal)
                && string.Equals(v.ActorParticipantId, attacker.Id, StringComparison.Ordinal)
                && !string.Equals(v.RequestId, requestId ?? string.Empty, StringComparison.Ordinal)
                && PayloadText(v.PayloadSummary, "naturalAttackId") == attack.DefinitionId)
            .OrderByDescending(v => v.RoundNumber).FirstOrDefault();
        if (lastUse != null && encounter.RoundNumber - lastUse.RoundNumber < attack.CooldownRounds)
            throw new InvalidOperationException("natural_attack_cooldown_active");
    }

    private async Task ApplyNaturalAttackConditionAsync(CombatEncounterState encounter, CombatParticipantState attacker, CombatParticipantState target, NaturalAttackDefinition attack, string actionId)
    {
        if (target.Conditions.Any(v => string.Equals(v.SourceActionId, actionId, StringComparison.Ordinal)
            && string.Equals(v.ConditionDefinitionId, attack.AppliedConditionId, StringComparison.Ordinal))) return;
        target.Conditions.Add(new CombatConditionState
        {
            ConditionDefinitionId = attack.AppliedConditionId,
            DisplayName = _conditionPresentationResolver?.ResolveDisplayName(attack.AppliedConditionId)
                ?? CombatConditionPresentationRules.ReadableOrGeneric(attack.AppliedConditionId),
            SourceActionId = actionId,
            SourceParticipantId = attacker.Id,
            TargetParticipantId = target.Id,
            ConditionGroup = "movement",
            Severity = "minor",
            StackMode = "unique",
            DurationMode = "rounds",
            RemainingRounds = attack.AppliedConditionRounds,
            AppliedRoundNumber = encounter.RoundNumber,
            AppliedTurnIndex = encounter.ActiveTurnIndex,
            IsNegative = true,
            Status = CombatConditionStatuses.Active
        });
        await _participants.UpsertAsync(target);
    }

    private static string PayloadText(IDictionary<string, object> payload, string key)
        => payload != null && payload.TryGetValue(key, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    private static WeaponDefinitionView BuildNaturalWeaponView(NaturalAttackDefinition attack)
    {
        var profile = attack.ToAttackProfile();
        return new WeaponDefinitionView
        {
            DefinitionId = "natural_attack", Name = attack.DisplayName, DisplayNameRu = attack.DisplayName,
            WeaponType = "natural", RangeType = attack.AreaShape == "single" ? "melee" : "area",
            DamageDraft = attack.Damage.Display, AccuracyDraft = attack.AccuracyModifier.ToString(CultureInfo.InvariantCulture),
            PenetrationDraft = attack.PhysicalPenetration.ToString(CultureInfo.InvariantCulture),
            FailedPenetrationDamageTransfer = attack.FailedPenetrationDamageTransfer,
            AttackProfiles = new List<AttackProfileDefinition> { profile },
            Tags = new List<string> { "natural_attack", attack.AttackType }
        };
    }

    public Task<InventoryItemInstanceState?> ResolveEquippedWeaponAsync(CombatParticipantState actorParticipant, CombatWeaponAttackRequest request)
    {
        if (!CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEquippedWeaponLookup))) return Task.FromResult<InventoryItemInstanceState?>(null);
        if (actorParticipant == null || string.IsNullOrWhiteSpace(actorParticipant.CharacterId)) return Task.FromResult<InventoryItemInstanceState?>(null);

        var items = ReadInventoryItems(actorParticipant.CharacterId);
        InventoryItemInstanceState? item = null;
        if (!string.IsNullOrWhiteSpace(request.WeaponItemInstanceId))
        {
            item = items.FirstOrDefault(x => string.Equals(x.ItemInstanceId, request.WeaponItemInstanceId, StringComparison.OrdinalIgnoreCase));
        }

        item ??= items.FirstOrDefault(x => x.IsEquipped && IsWeaponSlot(x.EquipmentSlotId));
        item ??= items.FirstOrDefault(x => x.IsEquipped && !string.IsNullOrWhiteSpace(x.DefinitionId));
        return Task.FromResult(item);
    }

    public Task<InventoryItemInstanceState?> ResolveAmmoAsync(CombatParticipantState actorParticipant, WeaponDefinitionView? weapon, CombatWeaponAttackRequest request)
    {
        if (!CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAmmoReadOnlyCheck)) && !CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEquippedWeaponLookup)))
            return Task.FromResult<InventoryItemInstanceState?>(null);
        if (actorParticipant == null || string.IsNullOrWhiteSpace(actorParticipant.CharacterId)) return Task.FromResult<InventoryItemInstanceState?>(null);

        var items = ReadInventoryItems(actorParticipant.CharacterId);
        InventoryItemInstanceState? item = null;
        if (!string.IsNullOrWhiteSpace(request.AmmoItemInstanceId))
        {
            item = items.FirstOrDefault(x => string.Equals(x.ItemInstanceId, request.AmmoItemInstanceId, StringComparison.OrdinalIgnoreCase));
        }

        if (item == null && !string.IsNullOrWhiteSpace(request.AmmoDefinitionId))
        {
            item = items.FirstOrDefault(x => string.Equals(x.DefinitionId, request.AmmoDefinitionId, StringComparison.OrdinalIgnoreCase));
        }

        if (item == null && weapon != null && weapon.AmmoDefinitionIds.Count > 0)
        {
            item = items.FirstOrDefault(x => weapon.AmmoDefinitionIds.Contains(x.DefinitionId, StringComparer.OrdinalIgnoreCase));
        }

        return Task.FromResult(item);
    }

    public Task<bool> ValidateWeaponAmmoCompatibilityAsync(WeaponDefinitionView? weapon, AmmoDefinitionView? ammo, CombatWeaponAttackRequest request, List<string> warnings)
    {
        var weaponRequiresAmmo = weapon?.AmmoDefinitionIds != null && weapon.AmmoDefinitionIds.Count > 0;
        if (!weaponRequiresAmmo && ammo == null) return Task.FromResult(true);

        if (!CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAmmoCompatibilityMvp)))
        {
            warnings.Add("ammo_compatibility_disabled");
            return Task.FromResult(true);
        }

        if (weapon == null) return Task.FromResult(true);
        if (weaponRequiresAmmo && ammo == null)
            throw new InvalidOperationException("ammo_required");
        if (ammo == null) return Task.FromResult(true);
        var weaponListEmpty = weapon.AmmoDefinitionIds == null || weapon.AmmoDefinitionIds.Count == 0;
        var ammoListEmpty = ammo.CompatibleWeaponIds == null || ammo.CompatibleWeaponIds.Count == 0;
        if (weaponListEmpty && ammoListEmpty)
        {
            warnings.Add("ammo_weapon_compatibility_unknown");
            return Task.FromResult(true);
        }

        if (!IsAmmoCompatible(weapon, ammo))
            throw new InvalidOperationException("ammo_not_compatible");
        return Task.FromResult(true);
    }

    public async Task<CombatDamagePreview> CalculateDamagePreviewAsync(WeaponDefinitionView? weapon, AmmoDefinitionView? ammo, CombatAttackResultResponse attackResult, CombatWeaponAttackRequest request, List<string> warnings)
    {
        var baseDamage = 1;
        var isDraftBased = false;
        if (request.DamageOverride.HasValue)
        {
            baseDamage = Math.Max(0, request.DamageOverride.Value);
            warnings.Add("damage_override_used");
        }
        else if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWeaponDamageDraft)))
        {
            if (weapon != null && _damageRoller.TryParseDamageDraft(weapon.DamageDraft, out var rolled))
            {
                baseDamage = rolled;
                isDraftBased = true;
            }
            else
            {
                warnings.Add("weapon_damage_draft_unparsed");
            }
        }
        else
        {
            warnings.Add("weapon_damage_draft_disabled");
        }

        var ammoModifier = 0;
        if (ammo != null && !string.IsNullOrWhiteSpace(ammo.DamageModifierDraft))
        {
            if (!_damageRoller.TryParseDamageModifier(ammo.DamageModifierDraft, out ammoModifier))
                warnings.Add("ammo_damage_modifier_unparsed");
        }

        if (ammo != null && !CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAmmoConsumptionMvp)))
            warnings.Add("ammo_consumption_disabled");

        var fate = new CombatFateHookResult
        {
            RollContext = "damage_roll",
            BaseRoll = baseDamage,
            FateModifiedRoll = baseDamage
        };
        var fateModifier = 0;
        if (request.UseFateEngine) warnings.Add("fate_damage_excluded_by_ruleset");

        var criticalMultiplier = attackResult.IsCritical ? 2 : 1;
        var targetParticipant = await _participants.GetByIdAsync(request.TargetParticipantId ?? string.Empty);
        var vehicleTarget = targetParticipant != null
            && string.Equals(targetParticipant.ParticipantType, CombatParticipantTypes.Vehicle, StringComparison.OrdinalIgnoreCase);
        var targetProtection = 0;
        if (vehicleTarget)
        {
            targetProtection = VehicleProtection(targetParticipant!, request.TargetProtectionZone);
        }
        else
        {
            var defense = await _defenseCalculationService.CalculateDefenseAsync(new CombatDefenseCalculationRequest
            {
                EncounterId = request.EncounterId ?? string.Empty,
                TargetParticipantId = request.TargetParticipantId ?? string.Empty,
                AttackerParticipantId = request.ActorParticipantId ?? string.Empty,
                WeaponDefinitionId = request.WeaponDefinitionId ?? string.Empty,
                IncludeArmor = true,
                IncludeShield = false,
                IncludeCover = false,
                IncludeDistance = false,
                RequestId = request.RequestId ?? string.Empty
            }, null);
            warnings.AddRange(defense.Warnings);
            var zone = NormalizePersonalZone(request.TargetProtectionZone);
            var natural = string.IsNullOrWhiteSpace(targetParticipant?.CharacterId) ? 0 : Math.Max(0, _profiles.GetBodyProfile(targetParticipant.CharacterId)?.NaturalPenetrationResistance ?? 0);
            var equipment = defense.ArmorItems.Sum(v => v.PenetrationResistanceByBodyZone.TryGetValue(zone, out var value) ? Math.Max(0, value) : 0);
            targetProtection = natural + equipment;
        }
        var weaponPenetration = ParseSignedDraft(weapon?.PenetrationDraft);
        var ammoPenetration = ParseSignedDraft(ammo?.PenetrationModifierDraft);
        var penetration = CombatPenetrationPolicy0219.Resolve(new CombatPenetrationContext0219
        {
            PenetrationType = CombatPenetrationTypes0219.Armor,
            AttackProfilePenetration = weaponPenetration,
            AmmoPenetration = ammoPenetration,
            TargetProtection = targetProtection
        });
        var rawDamage = Math.Max(0, (baseDamage + ammoModifier + fateModifier) * criticalMultiplier);
        var failedPenetrationTransfer = Math.Max(0m, Math.Min(1m, weapon?.FailedPenetrationDamageTransfer ?? 0m));
        var finalDamage = penetration.IsPenetrated
            ? rawDamage
            : (int)Math.Floor(rawDamage * failedPenetrationTransfer);
        var mitigated = Math.Max(0, rawDamage - finalDamage);
        return new CombatDamagePreview
        {
            BaseDamage = baseDamage,
            AmmoDamageModifier = ammoModifier,
            FateModifier = fateModifier,
            CriticalMultiplier = criticalMultiplier,
            DamageBeforeMitigation = rawDamage,
            FinalDamage = finalDamage,
            ProtectionValue = penetration.TargetProtection,
            PenetrationValue = penetration.TotalPenetration,
            MitigatedDamage = mitigated,
            FailedPenetrationDamageTransfer = failedPenetrationTransfer,
            IsPenetrated = penetration.IsPenetrated,
            PenetrationType = penetration.PenetrationType,
            ProtectionZone = vehicleTarget ? NormalizeVehicleZone(request.TargetProtectionZone) : NormalizePersonalZone(request.TargetProtectionZone),
            DamageType = NormalizeDamageType(request.DamageType),
            IsDraftBased = isDraftBased,
            Fate = CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateBreakdownInResponse)) ? fate : new CombatFateHookResult()
        };
    }

    private static int VehicleProtection(CombatParticipantState participant, string zone)
    {
        return NormalizeVehicleZone(zone) switch
        {
            "rear" => Math.Max(0, participant.RearProtection),
            "side" => Math.Max(0, participant.SideProtection),
            _ => Math.Max(0, participant.FrontProtection)
        };
    }

    private static string NormalizeVehicleZone(string zone)
    {
        var value = (zone ?? string.Empty).Trim().ToLowerInvariant();
        return value == "rear" || value == "side" ? value : "front";
    }

    private static string NormalizePersonalZone(string zone)
    {
        var value = (zone ?? string.Empty).Trim().ToLowerInvariant();
        return value == BodyZoneIds.Head || value == BodyZoneIds.LeftArm || value == BodyZoneIds.RightArm || value == BodyZoneIds.LeftLeg || value == BodyZoneIds.RightLeg || value == BodyZoneIds.Tail || value == BodyZoneIds.LeftWing || value == BodyZoneIds.RightWing ? value : BodyZoneIds.Torso;
    }

    public string BuildWeaponAttackLogMessage(CombatParticipantState attacker, CombatParticipantState target, CombatWeaponCombatSummary weapon, CombatAttackResultResponse attackResult, CombatDamagePreview preview, bool damageApplied)
    {
        var attackerName = string.IsNullOrWhiteSpace(attacker.DisplayName) ? attacker.Id : attacker.DisplayName;
        var targetName = string.IsNullOrWhiteSpace(target.DisplayName) ? target.Id : target.DisplayName;
        var weaponName = string.IsNullOrWhiteSpace(weapon.DisplayName) ? FirstNonEmpty(weapon.WeaponDefinitionId, "weapon") : weapon.DisplayName;
        var suffix = attackResult.IsHit
            ? $" Пробитие {preview.PenetrationValue} против защиты {preview.ProtectionValue}: {(preview.IsPenetrated ? "успешно" : "остановлено")}. Урон {preview.DamageBeforeMitigation}, предотвращено {preview.MitigatedDamage}, применено {preview.FinalDamage}."
            : string.Empty;
        if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateLogging)) && (attackResult.Fate.Applied || preview.Fate.Applied))
        {
            var modifier = attackResult.Fate.FateModifier + preview.Fate.FateModifier;
            suffix += $" Модификатор Судьбы: {FormatSigned(modifier)}.";
        }
        if (damageApplied) suffix += " Урон применён.";
        var hit = attackResult.IsHit ? "попадание" : "промах";
        return $"{attackerName} атакует {targetName}, оружие: {weaponName}. Результат: {hit}.{suffix}";
    }

    private async Task<WeaponDefinitionView?> ResolveWeaponDefinitionAsync(string definitionId, string ruleSetId, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            warnings.Add("weapon_definition_missing");
            return null;
        }

        var result = await _definitionResolver.ResolveWeaponAsync(definitionId, ruleSetId ?? string.Empty);
        warnings.AddRange(result.Warnings);
        if (!result.Success || result.Value == null)
        {
            warnings.Add("weapon_definition_missing");
            return null;
        }

        return result.Value;
    }

    private static AttackProfileDefinition? ResolveAttackProfile(WeaponDefinitionView? weapon, string requestedProfileId, List<string> warnings)
    {
        if (weapon == null) return null;
        var profiles = weapon.AttackProfiles ?? new List<AttackProfileDefinition>();
        var selected = string.IsNullOrWhiteSpace(requestedProfileId)
            ? profiles.FirstOrDefault()
            : profiles.FirstOrDefault(x => string.Equals(x.ProfileId, requestedProfileId, StringComparison.OrdinalIgnoreCase));
        if (selected == null) warnings.Add("attack_profile_missing");
        return selected;
    }

    private static WeaponDefinitionView? ApplyAttackProfile(WeaponDefinitionView? source, AttackProfileDefinition? profile)
    {
        if (source == null || profile == null) return source;
        return new WeaponDefinitionView
        {
            DefinitionId = source.DefinitionId,
            Name = source.Name,
            DisplayNameRu = source.DisplayNameRu,
            WeaponType = source.WeaponType,
            Handedness = source.Handedness,
            RangeType = FirstNonEmpty(profile.Range, source.RangeType),
            DamageDraft = FirstNonEmpty(profile.DamageExpression, source.DamageDraft),
            AccuracyDraft = profile.AccuracyModifier.ToString(CultureInfo.InvariantCulture),
            PenetrationDraft = Math.Max(profile.ArmorPenetration, profile.PhysicalPenetration).ToString(CultureInfo.InvariantCulture),
            FailedPenetrationDamageTransfer = Math.Max(0m, Math.Min(1m, profile.FailedPenetrationDamageTransfer)),
            LinkedSkillIds = string.IsNullOrWhiteSpace(profile.SkillDefinitionId) ? new List<string>(source.LinkedSkillIds ?? new List<string>()) : new List<string> { profile.SkillDefinitionId },
            AttributeHints = string.IsNullOrWhiteSpace(profile.SubAttributeDefinitionId) ? new List<string>(source.AttributeHints ?? new List<string>()) : new List<string> { profile.SubAttributeDefinitionId },
            AmmoDefinitionIds = new List<string>(source.AmmoDefinitionIds ?? new List<string>()),
            EquipmentSlotIds = new List<string>(source.EquipmentSlotIds ?? new List<string>()),
            AttackProfiles = new List<AttackProfileDefinition>(source.AttackProfiles ?? new List<AttackProfileDefinition>()),
            WeightKg = source.WeightKg,
            ValueCurrencyId = source.ValueCurrencyId,
            ValueAmountDraft = source.ValueAmountDraft,
            TechTags = new List<string>(source.TechTags ?? new List<string>()),
            MagicTags = new List<string>(source.MagicTags ?? new List<string>()),
            LegalTags = new List<string>(source.LegalTags ?? new List<string>()),
            Tags = new List<string>(source.Tags ?? new List<string>()),
            SchemaVersion = source.SchemaVersion
        };
    }

    private async Task<AmmoDefinitionView?> ResolveAmmoDefinitionAsync(string definitionId, string ruleSetId, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(definitionId)) return null;
        var result = await _definitionResolver.ResolveAmmoAsync(definitionId, ruleSetId ?? string.Empty);
        warnings.AddRange(result.Warnings);
        if (!result.Success || result.Value == null)
        {
            warnings.Add("ammo_definition_missing");
            return null;
        }

        return result.Value;
    }

    private List<InventoryItemInstanceState> ReadInventoryItems(string characterId)
    {
        try
        {
            var profile = _profiles.GetInventoryProfile(characterId);
            return (profile.Items ?? new List<CharacterInventoryItemProfileValue>())
                .Where(x => x != null)
                .Select(InventoryDomainMapper.ToItemInstanceState)
                .ToList();
        }
        catch
        {
            return new List<InventoryItemInstanceState>();
        }
    }

    private void AddDisabledSafetyWarnings(WeaponDefinitionView? weapon, AmmoDefinitionView? ammo, List<string> warnings)
    {
        if (ammo != null && !CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAmmoConsumptionMvp)))
            warnings.Add("ammo_consumption_disabled");
        if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAmmoConsumptionMvp)))
            warnings.Add("ammo_consumption_not_implemented");
        if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWeaponDurabilityMvp)))
            warnings.Add("weapon_durability_not_implemented");
        if (weapon != null && string.IsNullOrWhiteSpace(weapon.PenetrationDraft)) warnings.Add("weapon_penetration_defaulted_to_zero");
    }

    private static int ParseSignedDraft(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var text = value.Trim();
        if (int.TryParse(text, out var direct)) return direct;
        var digits = new string(text.Where((ch, index) => char.IsDigit(ch) || (ch == '-' && index == 0) || (ch == '+' && index == 0)).ToArray());
        return int.TryParse(digits, out var parsed) ? parsed : 0;
    }

    private async Task WriteWeaponAttackLogAsync(CombatEncounterState encounter, string actorParticipantId, string targetParticipantId, CombatWeaponCombatSummary weapon, CombatAmmoCombatSummary ammo, CombatAttackResultResponse attack, CombatDamagePreview preview, bool damageApplied, string message, string requestId, NaturalAttackDefinition? naturalAttack)
    {
        var payload = new Dictionary<string, object>
        {
            { "attackActionId", attack.ActionId ?? string.Empty },
            { "actorParticipantId", actorParticipantId ?? string.Empty },
            { "targetParticipantId", targetParticipantId ?? string.Empty },
            { "weaponDefinitionId", weapon.WeaponDefinitionId ?? string.Empty },
            { "ammoDefinitionId", ammo.AmmoDefinitionId ?? string.Empty },
            { "naturalAttackId", naturalAttack?.DefinitionId ?? string.Empty },
            { "naturalAttackCooldownRounds", naturalAttack?.CooldownRounds ?? 0 },
            { "hitResult", attack.HitResult ?? string.Empty },
            { "finalDamage", preview.FinalDamage },
            { "damageApplied", damageApplied },
            { "fateApplied", attack.Fate.Applied || preview.Fate.Applied },
            { "fateModifier", attack.Fate.FateModifier + preview.Fate.FateModifier },
            { "fateSummary", CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateLogging)) ? FirstNonEmpty(attack.Fate.FateSummary, preview.Fate.FateSummary) : string.Empty }
        };
        await _logWriter.AppendLogAndReplayAsync(
            new CombatLogWriteRequest
            {
                EncounterId = encounter.Id,
                CampaignId = encounter.CampaignId,
                SessionId = encounter.SessionId,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = actorParticipantId ?? string.Empty,
                EventType = CombatEventTypes.WeaponAttackResolved,
                Message = message ?? string.Empty,
                SourcePayload = payload,
                Visibility = CombatVisibilityIds.Public,
                RequestId = requestId ?? string.Empty
            },
            new CombatReplayWriteRequest
            {
                EncounterId = encounter.Id,
                EventType = CombatEventTypes.WeaponAttackResolved,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = actorParticipantId ?? string.Empty,
                SourcePayload = payload,
                Visibility = CombatVisibilityIds.Public,
                RequestId = requestId ?? string.Empty
            });
    }

    private async Task<CombatEncounterState> RequireEncounterAsync(string encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterId)) throw new ArgumentException("encounter_missing");
        var encounter = await _encounters.GetByIdAsync(encounterId);
        if (encounter == null) throw new KeyNotFoundException("encounter_missing");
        return encounter;
    }

    private async Task<CombatParticipantState> RequireParticipantAsync(string participantId, string encounterId, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException(errorCode);
        var participant = await _participants.GetByIdAsync(participantId);
        if (participant == null || !string.Equals(participant.EncounterId, encounterId, StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException(errorCode);
        return participant;
    }

    private static void EnsureParticipantCanAct(CombatParticipantState participant, string role)
    {
        if (!participant.IsActive) throw new InvalidOperationException(role == "target" ? "target_inactive" : "attacker_inactive");
        if (participant.IsDefeated) throw new InvalidOperationException(role == "target" ? "target_defeated" : "attacker_defeated");
    }

    private static void EnsureActorCanControl(CombatParticipantState participant, UserAccount actor)
    {
        var isAdmin = actor?.Roles?.Any(role => role == UserRole.Admin || role == UserRole.SuperAdmin) == true;
        if (isAdmin) return;
        if (actor == null || string.IsNullOrWhiteSpace(participant.ControllerUserId)
            || !string.Equals(participant.ControllerUserId, actor.Id, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("combat_participant_control_forbidden");
    }

    private static CombatWeaponCombatSummary BuildWeaponSummary(InventoryItemInstanceState? item, WeaponDefinitionView? definition, string definitionId, AttackProfileDefinition? attackProfile)
    {
        return new CombatWeaponCombatSummary
        {
            WeaponItemInstanceId = item?.ItemInstanceId ?? string.Empty,
            WeaponDefinitionId = FirstNonEmpty(definition?.DefinitionId, definitionId, item?.DefinitionId),
            AttackProfileId = attackProfile?.ProfileId ?? string.Empty,
            AttackProfileName = attackProfile?.Name ?? string.Empty,
            DisplayName = FirstNonEmpty(definition?.DisplayNameRu, definition?.Name, item?.DisplayName),
            WeaponType = definition?.WeaponType ?? string.Empty,
            Handedness = definition?.Handedness ?? string.Empty,
            DamageDraft = definition?.DamageDraft ?? string.Empty,
            AccuracyDraft = definition?.AccuracyDraft ?? string.Empty,
            LinkedSkillIds = definition?.LinkedSkillIds == null ? new List<string>() : new List<string>(definition.LinkedSkillIds),
            EquipmentSlotIds = definition?.EquipmentSlotIds == null ? new List<string>() : new List<string>(definition.EquipmentSlotIds)
        };
    }

    private static CombatAmmoCombatSummary BuildAmmoSummary(InventoryItemInstanceState? item, AmmoDefinitionView? definition, string definitionId, bool compatible)
    {
        return new CombatAmmoCombatSummary
        {
            AmmoItemInstanceId = item?.ItemInstanceId ?? string.Empty,
            AmmoDefinitionId = FirstNonEmpty(definition?.DefinitionId, definitionId, item?.DefinitionId),
            DisplayName = FirstNonEmpty(definition?.DisplayNameRu, definition?.Name, item?.DisplayName),
            AmmoType = definition?.AmmoType ?? string.Empty,
            Compatible = compatible,
            DamageModifierDraft = definition?.DamageModifierDraft ?? string.Empty,
            Quantity = Math.Max(0, item?.Quantity ?? 0)
        };
    }

    private static bool IsAmmoCompatible(WeaponDefinitionView weapon, AmmoDefinitionView ammo)
    {
        if (weapon == null || ammo == null) return false;
        var weaponAllowsAmmo = weapon.AmmoDefinitionIds != null
            && weapon.AmmoDefinitionIds.Contains(ammo.DefinitionId, StringComparer.OrdinalIgnoreCase);
        var ammoAllowsWeapon = ammo.CompatibleWeaponIds != null
            && ammo.CompatibleWeaponIds.Contains(weapon.DefinitionId, StringComparer.OrdinalIgnoreCase);
        return weaponAllowsAmmo || ammoAllowsWeapon;
    }

    private static bool IsWeaponSlot(string slotId)
    {
        return string.Equals(slotId, "main_hand", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "two_handed", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDamageType(string damageType)
    {
        var value = (damageType ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(value) ? "physical" : value;
    }

    private static string FormatSigned(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(value)) return (value ?? string.Empty).Trim();
        }

        return string.Empty;
    }
}
