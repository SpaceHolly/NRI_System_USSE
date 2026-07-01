using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Application;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatDefenseCalculationService
{
    Task<CombatDefenseCalculationResult> CalculateDefenseAsync(CombatDefenseCalculationRequest request, UserAccount? actor);
    Task<int> CalculateBaseDefenseAsync(CombatParticipantState targetParticipant, CombatDefenseCalculationRequest request);
    Task<IReadOnlyCollection<CombatDefenseEquipmentSummary>> CalculateArmorDefenseAsync(CombatParticipantState targetParticipant, CombatDefenseCalculationRequest request);
    Task<IReadOnlyCollection<CombatDefenseEquipmentSummary>> CalculateShieldDefenseAsync(CombatParticipantState targetParticipant, CombatDefenseCalculationRequest request);
    CombatCoverModifierResult CalculateCoverModifier(CombatDefenseCalculationRequest request);
    CombatDistanceModifierResult CalculateDistanceModifier(CombatDefenseCalculationRequest request);
    Task<CombatDefenseCalculationResult> BuildDefensePreviewAsync(CombatDefenseCalculationRequest request, UserAccount? actor);
}

public sealed class CombatDefenseCalculationService : ICombatDefenseCalculationService
{
    private const int DefaultBaseDefense = 10;
    private const int ArmorDefenseCap = 10;

    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICharacterProfileService _profiles;
    private readonly IItemEquipmentDefinitionResolver _definitionResolver;
    private readonly IServerLogger _logger;

    public CombatDefenseCalculationService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        ICharacterProfileService profiles,
        IItemEquipmentDefinitionResolver definitionResolver,
        IServerLogger logger)
    {
        _encounters = encounters;
        _participants = participants;
        _profiles = profiles;
        _definitionResolver = definitionResolver;
        _logger = logger;
    }

    private static bool ArmorDefenseEnabled => CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatArmorDefenseMvp));
    private static bool ShieldDefenseEnabled => CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatShieldDefenseMvp));
    private static bool CoverModifierEnabled => CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatCoverModifierMvp));
    private static bool DistanceModifierEnabled => CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDistanceModifierMvp));

    public async Task<CombatDefenseCalculationResult> CalculateDefenseAsync(CombatDefenseCalculationRequest request, UserAccount? actor)
    {
        var safeRequest = request ?? new CombatDefenseCalculationRequest();
        _logger.Debug($"combat.defense.calc.start encounterId={safeRequest.EncounterId} target={safeRequest.TargetParticipantId}");

        var result = new CombatDefenseCalculationResult
        {
            EncounterId = safeRequest.EncounterId ?? string.Empty,
            TargetParticipantId = safeRequest.TargetParticipantId ?? string.Empty,
            AttackerParticipantId = safeRequest.AttackerParticipantId ?? string.Empty,
            CheckedAtUtc = DateTime.UtcNow
        };

        var encounter = string.IsNullOrWhiteSpace(safeRequest.EncounterId)
            ? null
            : await _encounters.GetByIdAsync(safeRequest.EncounterId);
        if (encounter == null)
        {
            result.Errors.Add("encounter_missing");
            return result;
        }

        var target = string.IsNullOrWhiteSpace(safeRequest.TargetParticipantId)
            ? null
            : await _participants.GetByIdAsync(safeRequest.TargetParticipantId);
        if (target == null || !string.Equals(target.EncounterId, encounter.Id, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("target_missing");
            return result;
        }

        var effectiveRequest = CopyRequestWithDefaults(safeRequest, encounter);
        result.BaseDefense = await CalculateBaseDefenseAsync(target, effectiveRequest);

        if (effectiveRequest.IncludeArmor)
        {
            var armor = await CalculateEquipmentDefenseInternalAsync(target, effectiveRequest, includeShields: false);
            result.ArmorItems.AddRange(armor.Items);
            result.Warnings.AddRange(armor.Warnings);
            result.Errors.AddRange(armor.Errors);
            result.ArmorDefenseBonus = result.ArmorItems.Sum(x => Math.Max(0, x.DefenseBonus));
            if (result.ArmorDefenseBonus > ArmorDefenseCap)
            {
                result.Warnings.Add("armor_bonus_capped");
                result.ArmorDefenseBonus = ArmorDefenseCap;
            }
        }

        if (effectiveRequest.IncludeShield)
        {
            var shields = await CalculateEquipmentDefenseInternalAsync(target, effectiveRequest, includeShields: true);
            result.ShieldItems.AddRange(shields.Items);
            result.Warnings.AddRange(shields.Warnings);
            result.Errors.AddRange(shields.Errors);
            result.ShieldDefenseBonus = result.ShieldItems.Sum(x => Math.Max(0, x.DefenseBonus));
        }

        if (effectiveRequest.IncludeCover)
        {
            var cover = CalculateCoverModifier(effectiveRequest);
            result.CoverDefenseBonus = cover.CoverDefenseBonus;
            if (!string.IsNullOrWhiteSpace(cover.Warning)) result.Warnings.Add(cover.Warning);
        }

        if (effectiveRequest.IncludeDistance)
        {
            var distance = CalculateDistanceModifier(effectiveRequest);
            result.DistanceDefenseBonus = distance.DefenseModifier;
            if (!string.IsNullOrWhiteSpace(distance.Warning)) result.Warnings.Add(distance.Warning);
        }

        if (effectiveRequest.TargetDefenseOverride.HasValue)
        {
            result.TargetDefenseOverrideUsed = true;
            result.Warnings.Add("target_defense_override_used");
            result.TargetDefense = Math.Max(0, effectiveRequest.TargetDefenseOverride.Value);
        }
        else
        {
            result.TargetDefense = Math.Max(0, result.BaseDefense
                + result.ArmorDefenseBonus
                + result.ShieldDefenseBonus
                + result.CoverDefenseBonus
                + result.DistanceDefenseBonus
                + result.SituationalDefenseBonus);
        }

        result.Warnings = result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.Errors = result.Errors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _logger.Debug($"combat.defense.calc.done targetDefense={result.TargetDefense}");
        return result;
    }

    public Task<int> CalculateBaseDefenseAsync(CombatParticipantState targetParticipant, CombatDefenseCalculationRequest request)
    {
        return Task.FromResult(DefaultBaseDefense);
    }

    public async Task<IReadOnlyCollection<CombatDefenseEquipmentSummary>> CalculateArmorDefenseAsync(CombatParticipantState targetParticipant, CombatDefenseCalculationRequest request)
    {
        var calculated = await CalculateEquipmentDefenseInternalAsync(targetParticipant, request ?? new CombatDefenseCalculationRequest(), includeShields: false);
        return calculated.Items;
    }

    public async Task<IReadOnlyCollection<CombatDefenseEquipmentSummary>> CalculateShieldDefenseAsync(CombatParticipantState targetParticipant, CombatDefenseCalculationRequest request)
    {
        var calculated = await CalculateEquipmentDefenseInternalAsync(targetParticipant, request ?? new CombatDefenseCalculationRequest(), includeShields: true);
        return calculated.Items;
    }

    public CombatCoverModifierResult CalculateCoverModifier(CombatDefenseCalculationRequest request)
    {
        var state = FirstNonEmpty(request?.CoverState, string.Empty).Trim().ToLowerInvariant();
        if (request != null && request.CoverModifierOverride.HasValue)
        {
            return new CombatCoverModifierResult
            {
                CoverState = string.IsNullOrWhiteSpace(state) ? "override" : state,
                CoverDefenseBonus = Math.Max(0, request.CoverModifierOverride.Value),
                Warning = "cover_modifier_override_used"
            };
        }

        if (!CoverModifierEnabled)
        {
            return new CombatCoverModifierResult { CoverState = state, CoverDefenseBonus = 0 };
        }

        if (string.IsNullOrWhiteSpace(state) || string.Equals(state, "unknown", StringComparison.OrdinalIgnoreCase))
            return new CombatCoverModifierResult { CoverState = state, CoverDefenseBonus = 0, Warning = "cover_unknown" };
        if (string.Equals(state, "none", StringComparison.OrdinalIgnoreCase)) return new CombatCoverModifierResult { CoverState = state, CoverDefenseBonus = 0 };
        if (string.Equals(state, "light", StringComparison.OrdinalIgnoreCase)) return new CombatCoverModifierResult { CoverState = state, CoverDefenseBonus = 1 };
        if (string.Equals(state, "half", StringComparison.OrdinalIgnoreCase)) return new CombatCoverModifierResult { CoverState = state, CoverDefenseBonus = 2 };
        if (string.Equals(state, "heavy", StringComparison.OrdinalIgnoreCase)) return new CombatCoverModifierResult { CoverState = state, CoverDefenseBonus = 4 };
        if (string.Equals(state, "full", StringComparison.OrdinalIgnoreCase)) return new CombatCoverModifierResult { CoverState = state, CoverDefenseBonus = 8 };
        return new CombatCoverModifierResult { CoverState = state, CoverDefenseBonus = 0, Warning = "cover_unknown" };
    }

    public CombatDistanceModifierResult CalculateDistanceModifier(CombatDefenseCalculationRequest request)
    {
        if (!DistanceModifierEnabled)
            return new CombatDistanceModifierResult { DistanceMeters = request?.DistanceMeters ?? 0, DistanceBand = "disabled", AttackModifier = 0, DefenseModifier = 0 };

        if (request == null || !request.DistanceMeters.HasValue)
            return new CombatDistanceModifierResult { DistanceMeters = 0, DistanceBand = "unknown", AttackModifier = 0, DefenseModifier = 0, Warning = "distance_unknown" };

        var distance = Math.Max(0, request.DistanceMeters.Value);
        if (distance <= 2) return new CombatDistanceModifierResult { DistanceMeters = distance, DistanceBand = "0_2m", AttackModifier = 0, DefenseModifier = 0 };
        if (distance <= 10) return new CombatDistanceModifierResult { DistanceMeters = distance, DistanceBand = "2_10m", AttackModifier = 0, DefenseModifier = 0 };
        if (distance <= 30) return new CombatDistanceModifierResult { DistanceMeters = distance, DistanceBand = "10_30m", AttackModifier = -1, DefenseModifier = 0 };
        if (distance <= 60) return new CombatDistanceModifierResult { DistanceMeters = distance, DistanceBand = "30_60m", AttackModifier = -2, DefenseModifier = 0 };
        return new CombatDistanceModifierResult { DistanceMeters = distance, DistanceBand = "60m_plus", AttackModifier = -4, DefenseModifier = 0 };
    }

    public Task<CombatDefenseCalculationResult> BuildDefensePreviewAsync(CombatDefenseCalculationRequest request, UserAccount? actor)
    {
        return CalculateDefenseAsync(request, actor);
    }

    private async Task<EquipmentDefenseCalculation> CalculateEquipmentDefenseInternalAsync(CombatParticipantState targetParticipant, CombatDefenseCalculationRequest request, bool includeShields)
    {
        var result = new EquipmentDefenseCalculation();
        if (targetParticipant == null || string.IsNullOrWhiteSpace(targetParticipant.CharacterId))
        {
            result.Warnings.Add("armor_inventory_missing");
            return result;
        }

        if (includeShields && !ShieldDefenseEnabled) return result;
        if (!includeShields && !ArmorDefenseEnabled)
        {
            result.Warnings.Add("armor_defense_disabled");
            return result;
        }

        var items = ReadInventoryItems(targetParticipant.CharacterId, result.Warnings)
            .Where(x => x != null && x.IsEquipped)
            .ToList();
        if (items.Count == 0) return result;

        var ruleSetId = FirstNonEmpty(request.RuleSetId, RuleSetIds.FantasyNriDefault);
        var twoHandedWeaponEquipped = includeShields && await HasTwoHandedWeaponEquippedAsync(items, ruleSetId);
        foreach (var item in items)
        {
            var possibleShield = IsPotentialShieldItem(item);
            var possibleArmor = IsBodyArmorSlot(item.EquipmentSlotId) || HasTag(item, "armor");
            if (includeShields && !possibleShield) continue;
            if (!includeShields && !possibleArmor) continue;

            var resolved = await ResolveArmorDefinitionAsync(item, ruleSetId, result.Warnings, includeShields ? "shield_definition_missing" : "armor_definition_missing");
            var isShield = resolved != null && IsShieldDefinition(resolved, item);
            if (includeShields && !isShield && !possibleShield) continue;
            if (!includeShields && isShield) continue;

            var source = resolved == null ? "inventory_profile_fallback" : "armor_definition";
            var bonus = resolved == null
                ? ShieldFallbackBonus(item)
                : ParseDraftDefenseBonus(resolved.PhysicalArmorDraft, item.DefinitionId, result.Warnings);

            if (includeShields && bonus <= 0) bonus = ShieldFallbackBonus(item);
            if (includeShields && twoHandedWeaponEquipped)
            {
                result.Warnings.Add("shield_with_two_handed_weapon");
                if (request.StrictMode) bonus = 0;
            }

            if (!includeShields && bonus <= 0) continue;
            if (includeShields && bonus <= 0) continue;

            result.Items.Add(new CombatDefenseEquipmentSummary
            {
                ItemInstanceId = item.ItemInstanceId ?? string.Empty,
                DefinitionId = item.DefinitionId ?? string.Empty,
                DisplayName = FirstNonEmpty(item.DisplayName, resolved?.Name, item.DefinitionId),
                EquipmentSlotId = item.EquipmentSlotId ?? string.Empty,
                DefenseBonus = Math.Max(0, bonus),
                Source = source
            });
        }

        return result;
    }

    private IEnumerable<InventoryItemInstanceState> ReadInventoryItems(string characterId, List<string> warnings)
    {
        try
        {
            var profile = _profiles.GetInventoryProfile(characterId);
            return (profile?.Items ?? new List<CharacterInventoryItemProfileValue>())
                .Where(x => x != null)
                .Select(InventoryDomainMapper.ToItemInstanceState)
                .ToList();
        }
        catch
        {
            warnings.Add("armor_inventory_missing");
            return new List<InventoryItemInstanceState>();
        }
    }

    private async Task<ArmorDefinitionView?> ResolveArmorDefinitionAsync(InventoryItemInstanceState item, string ruleSetId, List<string> warnings, string missingCode)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.DefinitionId)) return null;
        try
        {
            var resolved = await _definitionResolver.ResolveArmorAsync(item.DefinitionId, ruleSetId ?? string.Empty);
            if (resolved.Success && resolved.Value != null) return resolved.Value;
        }
        catch
        {
            // Summary warning below is enough; do not log definition payloads.
        }

        warnings.Add(missingCode);
        return null;
    }

    private async Task<bool> HasTwoHandedWeaponEquippedAsync(IEnumerable<InventoryItemInstanceState> items, string ruleSetId)
    {
        foreach (var item in items.Where(x => x != null && x.IsEquipped))
        {
            if (string.Equals(item.EquipmentSlotId, "two_handed", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.IsNullOrWhiteSpace(item.DefinitionId)) continue;
            try
            {
                var resolved = await _definitionResolver.ResolveWeaponAsync(item.DefinitionId, ruleSetId ?? string.Empty);
                if (resolved.Success && resolved.Value != null
                    && string.Equals(resolved.Value.Handedness, "two_handed", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                // Missing weapon definitions are not blocker for defense preview.
            }
        }

        return false;
    }

    private static CombatDefenseCalculationRequest CopyRequestWithDefaults(CombatDefenseCalculationRequest request, CombatEncounterState encounter)
    {
        return new CombatDefenseCalculationRequest
        {
            EncounterId = encounter.Id,
            TargetParticipantId = request.TargetParticipantId ?? string.Empty,
            AttackerParticipantId = request.AttackerParticipantId ?? string.Empty,
            RuleSetId = FirstNonEmpty(request.RuleSetId, encounter.RuleSetId),
            AttackType = request.AttackType ?? string.Empty,
            WeaponDefinitionId = request.WeaponDefinitionId ?? string.Empty,
            DistanceMeters = request.DistanceMeters,
            CoverState = request.CoverState ?? string.Empty,
            CoverModifierOverride = request.CoverModifierOverride,
            TargetDefenseOverride = request.TargetDefenseOverride,
            IncludeArmor = request.IncludeArmor,
            IncludeShield = request.IncludeShield,
            IncludeCover = request.IncludeCover,
            IncludeDistance = request.IncludeDistance,
            StrictMode = request.StrictMode,
            RequestId = request.RequestId ?? string.Empty
        };
    }

    private static bool IsPotentialShieldItem(InventoryItemInstanceState item)
    {
        return item != null
            && (string.Equals(item.EquipmentSlotId, "off_hand", StringComparison.OrdinalIgnoreCase)
                || StartsWith(item.DefinitionId, "shield_")
                || HasTag(item, "shield"));
    }

    private static bool IsShieldDefinition(ArmorDefinitionView definition, InventoryItemInstanceState item)
    {
        return definition != null
            && (string.Equals(definition.ArmorType, "shield", StringComparison.OrdinalIgnoreCase)
                || StartsWith(definition.DefinitionId, "shield_")
                || (definition.Tags ?? new List<string>()).Any(x => string.Equals(x, "shield", StringComparison.OrdinalIgnoreCase))
                || IsPotentialShieldItem(item));
    }

    private static int ShieldFallbackBonus(InventoryItemInstanceState item)
    {
        var definitionId = item?.DefinitionId ?? string.Empty;
        if (string.Equals(definitionId, "shield_steel", StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(definitionId, "shield_wooden", StringComparison.OrdinalIgnoreCase)) return 1;
        if (StartsWith(definitionId, "shield_") || HasTag(item, "shield")) return 1;
        return 0;
    }

    private static int ParseDraftDefenseBonus(string draftValue, string definitionId, List<string> warnings)
    {
        var text = (draftValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            warnings.Add("armor_draft_bonus_unparsed");
            return 0;
        }

        if (int.TryParse(text.TrimStart('+'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var direct))
            return direct;

        var sign = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '-') sign = -1;
            if (!char.IsDigit(text[i])) continue;
            var start = i;
            while (i < text.Length && char.IsDigit(text[i])) i++;
            var number = text.Substring(start, i - start);
            if (int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return sign * parsed;
        }

        warnings.Add("armor_draft_bonus_unparsed");
        return 0;
    }

    private static bool IsBodyArmorSlot(string slotId)
    {
        return string.Equals(slotId, "head", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "torso", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "legs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "feet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "hands", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "back", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTag(InventoryItemInstanceState item, string tag)
    {
        return item?.Tags != null && item.Tags.Any(x => string.Equals(x, tag, StringComparison.OrdinalIgnoreCase));
    }

    private static bool StartsWith(string value, string prefix)
    {
        return !string.IsNullOrWhiteSpace(value) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return string.Empty;
    }

    private sealed class EquipmentDefenseCalculation
    {
        public List<CombatDefenseEquipmentSummary> Items { get; } = new List<CombatDefenseEquipmentSummary>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
    }
}
