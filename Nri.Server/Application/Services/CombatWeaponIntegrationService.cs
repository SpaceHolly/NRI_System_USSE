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
    private static readonly Regex DiceRegex = new Regex(@"^\s*(\d*)d(4|6|8|10|12)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        var match = DiceRegex.Match(value);
        if (!match.Success) return false;

        var countText = match.Groups[1].Value;
        var count = string.IsNullOrWhiteSpace(countText) ? 1 : int.Parse(countText, CultureInfo.InvariantCulture);
        var sides = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        if (count < 1 || count > 20) return false;

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
    private readonly CharacterProfileService _profiles;
    private readonly IItemEquipmentDefinitionResolver _definitionResolver;
    private readonly ICombatAttackRollService _attackRollService;
    private readonly ICombatDamageApplicationService _damageApplicationService;
    private readonly ICombatSnapshotService _snapshotService;
    private readonly ICombatLogWriter _logWriter;
    private readonly ICombatDamageRoller _damageRoller;
    private readonly ICombatFateHookService? _fateHookService;
    private readonly IServerLogger _logger;

    public CombatWeaponIntegrationService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        CharacterProfileService profiles,
        IItemEquipmentDefinitionResolver definitionResolver,
        ICombatAttackRollService attackRollService,
        ICombatDamageApplicationService damageApplicationService,
        ICombatSnapshotService snapshotService,
        ICombatLogWriter logWriter,
        ICombatDamageRoller damageRoller,
        IServerLogger logger,
        ICombatFateHookService? fateHookService = null)
    {
        _encounters = encounters;
        _participants = participants;
        _profiles = profiles;
        _definitionResolver = definitionResolver;
        _attackRollService = attackRollService;
        _damageApplicationService = damageApplicationService;
        _snapshotService = snapshotService;
        _logWriter = logWriter;
        _damageRoller = damageRoller;
        _fateHookService = fateHookService;
        _logger = logger;
    }

    public async Task<CombatWeaponAttackResponse> ExecuteWeaponAttackAsync(CombatWeaponAttackRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        _logger.Debug($"combat.weapon_attack.start encounterId={request.EncounterId} actor={request.ActorParticipantId} target={request.TargetParticipantId}");

        var encounter = await RequireEncounterAsync(request.EncounterId);
        var attacker = await RequireParticipantAsync(request.ActorParticipantId, encounter.Id, "attacker_missing");
        var target = await RequireParticipantAsync(request.TargetParticipantId, encounter.Id, "target_missing");
        EnsureParticipantCanAct(attacker, "attacker");
        EnsureParticipantCanAct(target, "target");

        var warnings = new List<string>();
        var weaponItem = await ResolveEquippedWeaponAsync(attacker, request);
        var weaponDefinitionId = FirstNonEmpty(request.WeaponDefinitionId, weaponItem?.DefinitionId);
        var weapon = await ResolveWeaponDefinitionAsync(weaponDefinitionId, encounter.RuleSetId, warnings);
        if (weapon == null && !request.DamageOverride.HasValue)
            throw new InvalidOperationException("weapon_definition_required_for_damage_preview");

        if (weapon != null)
            _logger.Debug($"combat.weapon_attack.weapon_resolved definitionId={weapon.DefinitionId}");

        var ammoItem = await ResolveAmmoAsync(attacker, weapon, request);
        var ammoDefinitionId = FirstNonEmpty(request.AmmoDefinitionId, ammoItem?.DefinitionId);
        var ammo = await ResolveAmmoDefinitionAsync(ammoDefinitionId, encounter.RuleSetId, warnings);
        if (ammo != null)
            _logger.Debug($"combat.weapon_attack.ammo_resolved definitionId={ammo.DefinitionId}");

        await ValidateWeaponAmmoCompatibilityAsync(weapon, ammo, request, warnings);
        AddDisabledSafetyWarnings(weapon, ammo, warnings);

        var attackResult = await _attackRollService.DeclareAttackAsync(new CombatAttackDeclareRequest
        {
            EncounterId = encounter.Id,
            ActorParticipantId = attacker.Id,
            TargetParticipantId = target.Id,
            WeaponDefinitionId = weaponDefinitionId,
            AttackSkillId = request.AttackSkillId ?? string.Empty,
            AttackAttributeId = request.AttackAttributeId ?? string.Empty,
            AttackBonus = request.AttackBonus,
            WeaponAccuracyBonus = 0,
            DistanceMeters = request.DistanceMeters,
            CoverModifier = request.CoverModifier,
            SituationalModifier = request.SituationalModifier,
            UseFateEngine = request.UseFateEngine,
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
        if (attackResult.IsHit)
        {
            damagePreview = await CalculateDamagePreviewAsync(weapon, ammo, attackResult, request, warnings);
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
                        DamageSource = "weapon_attack",
                        IsCriticalDamage = attackResult.IsCritical,
                        AllowAutoDefeat = true,
                        Reason = "weapon_attack",
                        RequestId = request.RequestId ?? string.Empty
                    }, actor);
                    damageApplied = true;
                    warnings.AddRange(damageResult.Warnings);
                }
                else
                {
                    warnings.Add("attack_damage_auto_apply_disabled");
                }
            }
        }
        else
        {
            damagePreview.FinalDamage = 0;
        }

        var weaponSummary = BuildWeaponSummary(weaponItem, weapon, weaponDefinitionId);
        var ammoSummary = BuildAmmoSummary(ammoItem, ammo, ammoDefinitionId, ammo != null && weapon != null && IsAmmoCompatible(weapon, ammo));
        var message = BuildWeaponAttackLogMessage(attacker, target, weaponSummary, attackResult, damagePreview, damageApplied);
        await WriteWeaponAttackLogAsync(encounter, attacker.Id, target.Id, weaponSummary, ammoSummary, attackResult, damagePreview, damageApplied, message, request.RequestId ?? string.Empty);

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
            AmmoDefinitionId = ammoDefinitionId,
            AttackResult = attackResult,
            DamageResult = damageResult,
            WeaponSummary = weaponSummary,
            AmmoSummary = ammoSummary,
            DamagePreview = damagePreview,
            Warnings = warnings.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Message = message,
            Snapshot = snapshot
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
        if (!CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAmmoCompatibilityMvp)))
        {
            warnings.Add("ammo_compatibility_disabled");
            return Task.FromResult(true);
        }

        if (weapon == null) return Task.FromResult(true);
        var weaponRequiresAmmo = weapon.AmmoDefinitionIds != null && weapon.AmmoDefinitionIds.Count > 0;
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
        if (request.UseFateEngine)
        {
            if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateHookMvp)) && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateDamageModifier)) && _fateHookService != null)
            {
                fate = await _fateHookService.ApplyFateToDamageRollAsync(new CombatFateHookRequest
                {
                    EncounterId = request.EncounterId ?? string.Empty,
                    RollContext = "damage_roll",
                    ActorParticipantId = request.ActorParticipantId ?? string.Empty,
                    TargetParticipantId = request.TargetParticipantId ?? string.Empty,
                    BaseRoll = baseDamage,
                    DiceExpression = weapon?.DamageDraft ?? string.Empty,
                    UseFateEngine = request.UseFateEngine,
                    RequestId = request.RequestId ?? string.Empty
                }, null);
                fateModifier = fate.FateModifier;
                warnings.AddRange(fate.Warnings);
            }
            else
            {
                warnings.Add("fate_damage_hook_disabled");
            }
        }

        var criticalMultiplier = attackResult.IsCritical ? 2 : 1;
        return new CombatDamagePreview
        {
            BaseDamage = baseDamage,
            AmmoDamageModifier = ammoModifier,
            FateModifier = fateModifier,
            CriticalMultiplier = criticalMultiplier,
            FinalDamage = Math.Max(0, (baseDamage + ammoModifier + fateModifier) * criticalMultiplier),
            DamageType = NormalizeDamageType(request.DamageType),
            IsDraftBased = isDraftBased,
            Fate = CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateBreakdownInResponse)) ? fate : new CombatFateHookResult()
        };
    }

    public string BuildWeaponAttackLogMessage(CombatParticipantState attacker, CombatParticipantState target, CombatWeaponCombatSummary weapon, CombatAttackResultResponse attackResult, CombatDamagePreview preview, bool damageApplied)
    {
        var attackerName = string.IsNullOrWhiteSpace(attacker.DisplayName) ? attacker.Id : attacker.DisplayName;
        var targetName = string.IsNullOrWhiteSpace(target.DisplayName) ? target.Id : target.DisplayName;
        var weaponName = string.IsNullOrWhiteSpace(weapon.DisplayName) ? FirstNonEmpty(weapon.WeaponDefinitionId, "weapon") : weapon.DisplayName;
        var suffix = attackResult.IsHit ? $" Damage preview: {preview.FinalDamage}." : string.Empty;
        if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateLogging)) && (attackResult.Fate.Applied || preview.Fate.Applied))
        {
            var modifier = attackResult.Fate.FateModifier + preview.Fate.FateModifier;
            suffix += $" Fate modifier: {FormatSigned(modifier)}.";
        }
        if (damageApplied) suffix += " Damage applied.";
        return $"{attackerName} attacks {targetName} with {weaponName}: {attackResult.HitResult}.{suffix}";
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
        if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatArmorDamageReduction)))
            warnings.Add("armor_damage_reduction_not_enabled");
        if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatArmorPenetration)) || (weapon != null && !string.IsNullOrWhiteSpace(weapon.PenetrationDraft)))
            warnings.Add("armor_penetration_not_enabled");
    }

    private async Task WriteWeaponAttackLogAsync(CombatEncounterState encounter, string actorParticipantId, string targetParticipantId, CombatWeaponCombatSummary weapon, CombatAmmoCombatSummary ammo, CombatAttackResultResponse attack, CombatDamagePreview preview, bool damageApplied, string message, string requestId)
    {
        var payload = new Dictionary<string, object>
        {
            { "attackActionId", attack.ActionId ?? string.Empty },
            { "actorParticipantId", actorParticipantId ?? string.Empty },
            { "targetParticipantId", targetParticipantId ?? string.Empty },
            { "weaponDefinitionId", weapon.WeaponDefinitionId ?? string.Empty },
            { "ammoDefinitionId", ammo.AmmoDefinitionId ?? string.Empty },
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

    private static CombatWeaponCombatSummary BuildWeaponSummary(InventoryItemInstanceState? item, WeaponDefinitionView? definition, string definitionId)
    {
        return new CombatWeaponCombatSummary
        {
            WeaponItemInstanceId = item?.ItemInstanceId ?? string.Empty,
            WeaponDefinitionId = FirstNonEmpty(definition?.DefinitionId, definitionId, item?.DefinitionId),
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
