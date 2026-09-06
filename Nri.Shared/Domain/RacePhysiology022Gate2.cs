using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class BodyZoneIds
{
    public const string Head = "head";
    public const string Torso = "torso";
    public const string LeftArm = "left_arm";
    public const string RightArm = "right_arm";
    public const string LeftLeg = "left_leg";
    public const string RightLeg = "right_leg";
    public const string Tail = "tail";
    public const string LeftWing = "left_wing";
    public const string RightWing = "right_wing";
}

public static class RacialMovementModeIds
{
    public const string PoweredFlight = "powered_flight";
    public const string Glide = "glide";
}

public static class RacialSenseModalityIds
{
    public const string VisualLowLight = "visual_low_light";
    public const string VisualDark = "visual_dark";
    public const string VisualLongRange = "visual_long_range";
    public const string HearingDirectional = "hearing_directional";
    public const string Smell = "smell";
    public const string Thermal = "thermal";
    public const string Vibration = "vibration";
    public const string MagicPresence = "magic_presence";
    public const string RuneStructure = "rune_structure";
}

public sealed class BodyZoneDefinition
{
    public string ZoneId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal RandomWeight { get; set; }
    public int CalledShotAccuracyModifier { get; set; }
    public int NaturalPenetrationResistanceModifier { get; set; }
    public List<string> CapabilityTags { get; set; } = new List<string>();
}

public static class BodyZoneRules022Gate2
{
    public static BodyZoneDefinition ResolveWeighted(IEnumerable<BodyZoneDefinition>? zones, decimal unitRoll)
    {
        var available = (zones ?? Enumerable.Empty<BodyZoneDefinition>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ZoneId) && x.RandomWeight > 0m)
            .ToList();
        if (available.Count == 0)
            return new BodyZoneDefinition { ZoneId = BodyZoneIds.Torso, DisplayName = "Корпус", RandomWeight = 1m };

        var total = available.Sum(x => x.RandomWeight);
        var normalized = Math.Max(0m, Math.Min(.999999999m, unitRoll));
        var threshold = normalized * total;
        decimal accumulated = 0m;
        foreach (var zone in available)
        {
            accumulated += zone.RandomWeight;
            if (threshold < accumulated) return zone;
        }
        return available[available.Count - 1];
    }
}

public sealed class RaceEquipmentFitProfile
{
    public string SizeClass { get; set; } = "medium";
    public int MinimumEquipmentHeightCm { get; set; }
    public int MaximumEquipmentHeightCm { get; set; }
    public List<string> RequiredFitTags { get; set; } = new List<string>();
    public List<string> BodyShapeTags { get; set; } = new List<string>();
    public string PublicWarning { get; set; } = string.Empty;
}

public sealed class RacialSenseDefinition
{
    public string SenseId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public decimal PassiveRangeMeters { get; set; }
    public decimal FocusedRangeMeters { get; set; }
    public decimal RangeMultiplier { get; set; } = 1m;
    public bool RequiresConnectedSurface { get; set; }
    public bool BlockedBySealedBarrier { get; set; }
    public bool PenetratesWalls { get; set; }
    public bool WorksInAbsoluteDarkness { get; set; }
    public string PublicLimitations { get; set; } = string.Empty;
}

public sealed class RacialMovementAbilityDefinition
{
    public string AbilityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string MovementMode { get; set; } = string.Empty;
    public int ActionCostHalfActions { get; set; } = 1;
    public decimal SpeedMeters { get; set; }
    public decimal MaximumLoadFraction { get; set; }
    public decimal ReducedSpeedLoadFraction { get; set; }
    public decimal ReducedSpeedMultiplier { get; set; } = 1m;
    public decimal RequiredClearanceMeters { get; set; }
    public decimal MaximumIndependentTakeoffWindMetersPerSecond { get; set; }
    public decimal GlideRatio { get; set; }
    public bool CanHover { get; set; }
    public List<string> RequiredBodyZoneIds { get; set; } = new List<string>();
    public List<string> RequiredEquipmentFitTags { get; set; } = new List<string>();
}

public sealed class RacialMovementAvailabilityContext022Gate2
{
    public RacialMovementAbilityDefinition Ability { get; set; } = new RacialMovementAbilityDefinition();
    public decimal CarriedLoadFraction { get; set; }
    public decimal WindMetersPerSecond { get; set; }
    public decimal AvailableClearanceMeters { get; set; }
    public List<string> FunctionalBodyZoneIds { get; set; } = new List<string>();
    public List<string> EquipmentFitTags { get; set; } = new List<string>();
    public bool AlreadyAirborne { get; set; }
}

public sealed class RacialMovementAvailability022Gate2
{
    public bool IsAvailable { get; set; }
    public decimal EffectiveSpeedMeters { get; set; }
    public List<string> BlockingReasons { get; set; } = new List<string>();
}

public static class RacialMovementRules022Gate2
{
    public static RacialMovementAvailability022Gate2 Resolve(RacialMovementAvailabilityContext022Gate2 context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        var ability = context.Ability ?? throw new ArgumentNullException(nameof(context.Ability));
        var blockers = new List<string>();
        if (ability.RequiredBodyZoneIds.Except(context.FunctionalBodyZoneIds ?? new List<string>(), StringComparer.Ordinal).Any())
            blockers.Add("required_body_zone_impaired");
        if (ability.RequiredEquipmentFitTags.Except(context.EquipmentFitTags ?? new List<string>(), StringComparer.Ordinal).Any())
            blockers.Add("equipment_blocks_movement");
        if (ability.MaximumLoadFraction > 0m && context.CarriedLoadFraction > ability.MaximumLoadFraction)
            blockers.Add("load_limit_exceeded");
        if (!context.AlreadyAirborne && ability.RequiredClearanceMeters > context.AvailableClearanceMeters)
            blockers.Add("takeoff_clearance_insufficient");
        if (!context.AlreadyAirborne && ability.MaximumIndependentTakeoffWindMetersPerSecond > 0m
            && context.WindMetersPerSecond > ability.MaximumIndependentTakeoffWindMetersPerSecond)
            blockers.Add("takeoff_wind_limit_exceeded");
        var speed = Math.Max(0m, ability.SpeedMeters);
        if (blockers.Count == 0 && ability.ReducedSpeedLoadFraction > 0m
            && context.CarriedLoadFraction > ability.ReducedSpeedLoadFraction)
            speed *= Math.Max(0m, ability.ReducedSpeedMultiplier);
        return new RacialMovementAvailability022Gate2
        {
            IsAvailable = blockers.Count == 0,
            EffectiveSpeedMeters = blockers.Count == 0 ? speed : 0m,
            BlockingReasons = blockers
        };
    }
}

public sealed class ElementalResistanceTier
{
    public string DamageTypeId { get; set; } = string.Empty;
    public int Tier { get; set; }
}

public sealed class DamageExpressionDefinition
{
    public int DiceCount { get; set; } = 1;
    public int DieSides { get; set; } = 2;
    public int PerDieModifier { get; set; }
    public int TotalModifier { get; set; }

    public string Display => $"{DiceCount}(d{DieSides}{(PerDieModifier >= 0 ? "+" : string.Empty)}{PerDieModifier}){(TotalModifier >= 0 ? "+" : string.Empty)}{TotalModifier}";
}

public sealed class DamageDieResolution022Gate2
{
    public int RawValue { get; set; }
    public int ModifiedValue { get; set; }
}

public sealed class DamageExpressionResolution022Gate2
{
    public List<DamageDieResolution022Gate2> Dice { get; set; } = new List<DamageDieResolution022Gate2>();
    public int TotalModifier { get; set; }
    public int TotalDamage { get; set; }
}

public static class DamageExpressionRules022Gate2
{
    public static DamageExpressionResolution022Gate2 Roll(DamageExpressionDefinition expression, Func<int, int> rollDie)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        if (rollDie == null) throw new ArgumentNullException(nameof(rollDie));
        if (expression.DiceCount < 1) throw new ArgumentOutOfRangeException(nameof(expression.DiceCount));
        if (expression.DieSides < 2) throw new ArgumentOutOfRangeException(nameof(expression.DieSides));
        var result = new DamageExpressionResolution022Gate2 { TotalModifier = expression.TotalModifier };
        for (var i = 0; i < expression.DiceCount; i++)
        {
            var raw = rollDie(expression.DieSides);
            if (raw < 1 || raw > expression.DieSides) throw new InvalidOperationException("Die resolver returned a value outside the die range.");
            result.Dice.Add(new DamageDieResolution022Gate2 { RawValue = raw, ModifiedValue = raw + expression.PerDieModifier });
        }
        result.TotalDamage = Math.Max(0, result.Dice.Sum(v => v.ModifiedValue) + expression.TotalModifier);
        return result;
    }
}

public sealed class NaturalAttackDefinition
{
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AttackType { get; set; } = string.Empty;
    public int ActionCostHalfActions { get; set; } = 1;
    public int AccuracyModifier { get; set; }
    public decimal RangeMeters { get; set; }
    public DamageExpressionDefinition Damage { get; set; } = new DamageExpressionDefinition();
    public List<string> DamageTypeIds { get; set; } = new List<string>();
    public int PhysicalPenetration { get; set; }
    public decimal FailedPenetrationDamageTransfer { get; set; }
    public string AreaShape { get; set; } = "single";
    public decimal AreaAngleDegrees { get; set; }
    public decimal AreaWidthMeters { get; set; }
    public int CooldownRounds { get; set; }
    public bool FriendlyFire { get; set; }
    public bool FateEligibleForHitCheck { get; set; } = true;
    public bool FateEligibleForDamage { get; set; }
    public List<string> RequiredBodyZoneIds { get; set; } = new List<string>();
    public List<string> DisabledBodyZoneStates { get; set; } = new List<string>();
    public string AppliedConditionId { get; set; } = string.Empty;
    public int AppliedConditionRounds { get; set; }

    public AttackProfileDefinition ToAttackProfile() => new AttackProfileDefinition
    {
        ProfileId = DefinitionId, Name = DisplayName, AttackType = AttackType, ActionCost = ActionCostHalfActions,
        AccuracyModifier = AccuracyModifier, Range = RangeMeters.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m",
        DamageExpression = Damage.Display, DiceCount = Damage.DiceCount, DieSides = Damage.DieSides,
        PerDieModifier = Damage.PerDieModifier, TotalModifier = Damage.TotalModifier,
        DamageTypeDefinitionIds = DamageTypeIds.ToList(), PhysicalPenetration = PhysicalPenetration,
        ArmorPenetration = PhysicalPenetration, FailedPenetrationDamageTransfer = FailedPenetrationDamageTransfer,
        AreaShape = AreaShape, AreaAngleDegrees = AreaAngleDegrees, AreaWidthMeters = AreaWidthMeters,
        CooldownRounds = CooldownRounds, FateEligibleForHitCheck = FateEligibleForHitCheck, FateEligibleForDamage = false
    };
}

public sealed class CombatAreaPoint022Gate2
{
    public string ParticipantId { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
}

public static class NaturalAttackAreaRules022Gate2
{
    public static IReadOnlyList<string> ResolveTargets(
        NaturalAttackDefinition attack,
        CombatAreaPoint022Gate2 origin,
        CombatAreaPoint022Gate2 aim,
        IEnumerable<CombatAreaPoint022Gate2> candidates)
    {
        if (attack == null || origin == null || aim == null) return Array.Empty<string>();
        var shape = (attack.AreaShape ?? string.Empty).Trim().ToLowerInvariant();
        if (shape != "cone" && shape != "line") return new[] { aim.ParticipantId };

        var dx = aim.X - origin.X;
        var dy = aim.Y - origin.Y;
        var directionLength = Math.Sqrt(dx * dx + dy * dy);
        if (directionLength <= 0.000001d) return Array.Empty<string>();
        var ux = dx / directionLength;
        var uy = dy / directionLength;
        var range = Math.Max(0d, (double)attack.RangeMeters);
        var halfAngleRadians = Math.Max(0d, (double)attack.AreaAngleDegrees) * Math.PI / 360d;
        var halfWidth = Math.Max(0d, (double)attack.AreaWidthMeters) / 2d;

        return (candidates ?? Array.Empty<CombatAreaPoint022Gate2>())
            .Where(candidate => candidate != null && !string.Equals(candidate.ParticipantId, origin.ParticipantId, StringComparison.OrdinalIgnoreCase))
            .Where(candidate =>
            {
                var tx = candidate.X - origin.X;
                var ty = candidate.Y - origin.Y;
                var distance = Math.Sqrt(tx * tx + ty * ty);
                if (distance > range + 0.000001d || distance <= 0.000001d) return false;
                var forward = tx * ux + ty * uy;
                if (forward < 0d || forward > range + 0.000001d) return false;
                var perpendicular = Math.Abs(tx * uy - ty * ux);
                if (shape == "line") return perpendicular <= halfWidth + 0.000001d;
                var angle = Math.Acos(Math.Max(-1d, Math.Min(1d, forward / distance)));
                return angle <= halfAngleRadians + 0.000001d;
            })
            .Select(candidate => candidate.ParticipantId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class ResolvedOriginPhysiology
{
    public int MinimumHeightCm { get; set; }
    public int MaximumHeightCm { get; set; }
    public int AdultAgeYears { get; set; }
    public int AverageLifespanYears { get; set; }
    public int MaximumLifespanYears { get; set; }
    public int BaseHealth { get; set; }
    public int NaturalArmorRating { get; set; }
    public int NaturalPenetrationResistance { get; set; }
    public List<string> PublicTraits { get; set; } = new List<string>();
    public List<string> TraitDefinitionIds { get; set; } = new List<string>();
    public List<BodyZoneDefinition> BodyZones { get; set; } = new List<BodyZoneDefinition>();
    public RaceEquipmentFitProfile EquipmentFit { get; set; } = new RaceEquipmentFitProfile();
    public List<RacialSenseDefinition> Senses { get; set; } = new List<RacialSenseDefinition>();
    public List<RacialMovementAbilityDefinition> MovementAbilities { get; set; } = new List<RacialMovementAbilityDefinition>();
    public List<NaturalAttackDefinition> NaturalAttacks { get; set; } = new List<NaturalAttackDefinition>();
    public List<ElementalResistanceTier> ElementalResistances { get; set; } = new List<ElementalResistanceTier>();
    public List<EnvironmentalToleranceModifier> EnvironmentalToleranceModifiers { get; set; } = new List<EnvironmentalToleranceModifier>();
}

public static class RacePhysiologyRules022Gate2
{
    public static ResolvedOriginPhysiology Resolve(CharacterOriginDefinition origin, CharacterOriginSubtypeDefinition? subtype)
    {
        if (origin == null) throw new ArgumentNullException(nameof(origin));
        return new ResolvedOriginPhysiology
        {
            MinimumHeightCm = subtype?.MinimumHeightCm ?? origin.MinimumHeightCm,
            MaximumHeightCm = subtype?.MaximumHeightCm ?? origin.MaximumHeightCm,
            AdultAgeYears = subtype?.AdultAgeYears ?? origin.AdultAgeYears,
            AverageLifespanYears = subtype?.AverageLifespanYears ?? origin.AverageLifespanYears,
            MaximumLifespanYears = subtype?.MaximumLifespanYears ?? origin.MaximumLifespanYears,
            BaseHealth = subtype?.BaseHealth ?? origin.BaseHealth,
            NaturalArmorRating = subtype?.NaturalArmorRating ?? origin.NaturalArmorRating,
            NaturalPenetrationResistance = subtype?.NaturalPenetrationResistance ?? origin.NaturalPenetrationResistance,
            PublicTraits = origin.PublicTraits.Concat(subtype?.PublicTraits ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToList(),
            TraitDefinitionIds = origin.TraitDefinitionIds.Concat(subtype?.TraitDefinitionIds ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToList(),
            BodyZones = CloneZones(subtype?.BodyZones.Count > 0 ? subtype.BodyZones : origin.BodyZones),
            EquipmentFit = subtype?.EquipmentFit ?? origin.EquipmentFit,
            Senses = origin.Senses.Concat(subtype?.Senses ?? Enumerable.Empty<RacialSenseDefinition>()).GroupBy(x => x.SenseId, StringComparer.Ordinal).Select(x => x.Last()).ToList(),
            MovementAbilities = origin.MovementAbilities.Concat(subtype?.MovementAbilities ?? Enumerable.Empty<RacialMovementAbilityDefinition>()).GroupBy(x => x.AbilityId, StringComparer.Ordinal).Select(x => x.Last()).ToList(),
            NaturalAttacks = origin.NaturalAttacks.Concat(subtype?.NaturalAttacks ?? Enumerable.Empty<NaturalAttackDefinition>()).GroupBy(x => x.DefinitionId, StringComparer.Ordinal).Select(x => x.Last()).ToList(),
            ElementalResistances = origin.ElementalResistances.Concat(subtype?.ElementalResistances ?? Enumerable.Empty<ElementalResistanceTier>()).GroupBy(x => x.DamageTypeId, StringComparer.Ordinal).Select(x => x.OrderByDescending(v => v.Tier).First()).ToList(),
            EnvironmentalToleranceModifiers = origin.EnvironmentalToleranceModifiers.Concat(subtype?.EnvironmentalToleranceModifiers ?? Enumerable.Empty<EnvironmentalToleranceModifier>()).ToList()
        };
    }

    public static IReadOnlyList<string> Validate(ResolvedOriginPhysiology value, bool playable)
    {
        var errors = new List<string>();
        if (value.MinimumHeightCm <= 0 || value.MinimumHeightCm > value.MaximumHeightCm) errors.Add("height_range_invalid");
        if (value.AdultAgeYears < 0 || value.AdultAgeYears > value.AverageLifespanYears || value.AverageLifespanYears > value.MaximumLifespanYears) errors.Add("lifespan_order_invalid");
        if (playable && value.BaseHealth <= 0) errors.Add("base_health_required");
        if (playable && value.NaturalArmorRating < 1) errors.Add("natural_armor_rating_required");
        if (playable && value.NaturalPenetrationResistance < 1) errors.Add("natural_penetration_resistance_required");
        if (value.BodyZones.Count == 0 || value.BodyZones.Sum(x => x.RandomWeight) <= 0) errors.Add("body_zone_weights_invalid");
        if (value.NaturalAttacks.Any(x => x.Damage.DiceCount < 1 || x.Damage.DieSides < 2)) errors.Add("natural_attack_dice_invalid");
        if (value.NaturalAttacks.Any(x => x.FailedPenetrationDamageTransfer < 0 || x.FailedPenetrationDamageTransfer > 1)) errors.Add("natural_attack_transfer_invalid");
        if (value.NaturalAttacks.Any(x => x.CooldownRounds < 0)) errors.Add("natural_attack_cooldown_invalid");
        if (value.MovementAbilities.Any(x => x.RequiredBodyZoneIds.Except(value.BodyZones.Select(z => z.ZoneId), StringComparer.Ordinal).Any())) errors.Add("movement_body_zone_requirement_missing");
        return errors;
    }

    public static List<BodyZoneDefinition> HumanoidZones() => new List<BodyZoneDefinition>
    {
        new BodyZoneDefinition { ZoneId=BodyZoneIds.Head, DisplayName="Голова", RandomWeight=.10m, CalledShotAccuracyModifier=-4 },
        new BodyZoneDefinition { ZoneId=BodyZoneIds.Torso, DisplayName="Корпус", RandomWeight=.45m, CalledShotAccuracyModifier=0 },
        new BodyZoneDefinition { ZoneId=BodyZoneIds.LeftArm, DisplayName="Левая рука", RandomWeight=.10m, CalledShotAccuracyModifier=-2 },
        new BodyZoneDefinition { ZoneId=BodyZoneIds.RightArm, DisplayName="Правая рука", RandomWeight=.10m, CalledShotAccuracyModifier=-2 },
        new BodyZoneDefinition { ZoneId=BodyZoneIds.LeftLeg, DisplayName="Левая нога", RandomWeight=.125m, CalledShotAccuracyModifier=-2 },
        new BodyZoneDefinition { ZoneId=BodyZoneIds.RightLeg, DisplayName="Правая нога", RandomWeight=.125m, CalledShotAccuracyModifier=-2 }
    };

    private static List<BodyZoneDefinition> CloneZones(IEnumerable<BodyZoneDefinition> zones) => zones.Select(x => new BodyZoneDefinition
    {
        ZoneId=x.ZoneId, DisplayName=x.DisplayName, RandomWeight=x.RandomWeight, CalledShotAccuracyModifier=x.CalledShotAccuracyModifier,
        NaturalPenetrationResistanceModifier=x.NaturalPenetrationResistanceModifier, CapabilityTags=x.CapabilityTags.ToList()
    }).ToList();
}

public sealed class PersonalProtectionResolution022Gate2
{
    public int Defense { get; set; }
    public int TotalPenetrationResistance { get; set; }
    public bool Hit { get; set; }
    public bool Penetrated { get; set; }
    public int AppliedDamage { get; set; }
}

public static class PersonalProtectionRules022Gate2
{
    public static PersonalProtectionResolution022Gate2 Resolve(int attackTotal, int dodge, int naturalArmorRating, int equipmentArmorRating,
        int situationDefense, int penetration, int naturalPenetrationResistance, int equipmentZonePenetrationResistance, int damage, decimal failedPenetrationTransfer)
    {
        var defense = dodge + Math.Max(0, naturalArmorRating) + Math.Max(0, equipmentArmorRating) + situationDefense;
        var hit = attackTotal >= defense;
        var resistance = Math.Max(0, naturalPenetrationResistance) + Math.Max(0, equipmentZonePenetrationResistance);
        var penetrated = hit && penetration >= resistance;
        var applied = !hit ? 0 : penetrated ? Math.Max(0, damage) : (int)Math.Floor(Math.Max(0, damage) * Math.Max(0m, Math.Min(1m, failedPenetrationTransfer)));
        return new PersonalProtectionResolution022Gate2 { Defense=defense, TotalPenetrationResistance=resistance, Hit=hit, Penetrated=penetrated, AppliedDamage=applied };
    }
}
