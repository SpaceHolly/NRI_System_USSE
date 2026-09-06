using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class LiveActorFeatureFlags
{
    public const bool UseLiveActorStateV1 = true;
    public const bool UseEffectiveCapabilitiesV1 = true;
    public const bool UseRuntimeEffectsV1 = true;
    public const bool UseActionExecutionV1 = true;
    public const bool UseOperationalLoadoutV1 = true;
    public const bool UseLiveActorPlayerView = true;
    public const bool UseLiveActorAdminView = true;
    public const bool UseLiveActorSyncEvents = true;
}

public static class RuntimeSubjectTypes
{
    public const string Character = "character";
    public const string Companion = "companion";
    public const string Npc = "npc";
    public const string Summon = "summon";
    public const string Construct = "construct";
    public const string VehicleCrewActor = "vehicle_crew_actor";
    public const string Custom = "custom";
}

public static class LiveActorRules
{
    public static decimal EffectiveCapability(decimal baseValue, decimal permanentModifier, decimal temporaryModifier)
        => baseValue + permanentModifier + temporaryModifier;

    public static decimal EffectiveMaximum(decimal baseMaximum, decimal temporaryModifier)
        => Math.Max(0, baseMaximum + temporaryModifier);

    public static decimal ClampCurrent(decimal current, decimal effectiveMaximum, bool allowOvercap)
        => allowOvercap ? Math.Max(0, current) : Math.Max(0, Math.Min(current, effectiveMaximum));

    public static bool IsActionReady(ActionRuntimeState action)
        => action.IsEnabled
           && action.RemainingTurns <= 0
           && action.RemainingRounds <= 0
           && (!action.ReadyAtUtc.HasValue || action.ReadyAtUtc.Value <= DateTime.UtcNow)
           && (action.MaximumCharges <= 0 || action.CurrentCharges > 0);

    public static int ReloadTransfer(int loaded, int capacity, int reserve)
        => Math.Min(Math.Max(0, capacity - loaded), Math.Max(0, reserve));

    public static bool IsAmmunitionCompatible(IEnumerable<string> compatibleTags, IEnumerable<string> ammunitionTags)
    {
        var required = compatibleTags?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        if (required.Length == 0) return true;
        var available = new HashSet<string>(ammunitionTags ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        return required.Any(available.Contains);
    }

    public static bool IsEffectExpired(RuntimeEffectInstance effect, DateTime utcNow)
        => !effect.IsActive || effect.RemainingRounds.HasValue && effect.RemainingRounds.Value <= 0 || effect.ExpiresAtUtc.HasValue && effect.ExpiresAtUtc.Value <= utcNow;

    public static (bool CanAct, bool CanReact) LifePermissions(string stateCode)
        => (stateCode ?? string.Empty).ToLowerInvariant() switch
        {
            "healthy" or "active" => (true, true),
            "impaired" => (true, true),
            "stable" => (true, false),
            _ => (false, false)
        };
}

public sealed class RuntimeSubjectReference
{
    public string SubjectType { get; set; } = RuntimeSubjectTypes.Character;
    public string SubjectId { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string? SceneId { get; set; }
    public string? CombatParticipantId { get; set; }
    public string? OwnerCharacterId { get; set; }
    public string VisibilityContextKey { get; set; } = string.Empty;
    public string DisplayNameSnapshot { get; set; } = string.Empty;
}

public sealed class RuntimeResourceState
{
    public string ResourceDefinitionId { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal TemporaryCurrentModifier { get; set; }
    public decimal TemporaryMaximumModifier { get; set; }
    public decimal OvercapValue { get; set; }
    public string LastChangeReasonCode { get; set; } = string.Empty;
    public string LastChangeSourceType { get; set; } = string.Empty;
    public string LastChangeSourceId { get; set; } = string.Empty;
    public string LastChangedAtWorldTime { get; set; } = string.Empty;
    public DateTime LastChangedAtUtc { get; set; } = DateTime.UtcNow;
    public long Revision { get; set; }
}

public sealed class ActionRuntimeState
{
    public string ActionDefinitionId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceDefinitionId { get; set; } = string.Empty;
    public string CooldownMode { get; set; } = "none";
    public int RemainingTurns { get; set; }
    public int RemainingRounds { get; set; }
    public string ReadyAtWorldTime { get; set; } = string.Empty;
    public string ReadyAtSceneTime { get; set; } = string.Empty;
    public DateTime? ReadyAtUtc { get; set; }
    public int CurrentCharges { get; set; }
    public int MaximumCharges { get; set; }
    public Dictionary<string, decimal> ResourceCosts { get; set; } = new();
    public int CooldownRoundsOnUse { get; set; }
    public int CooldownTurnsOnUse { get; set; }
    public string RestResetPolicy { get; set; } = string.Empty;
    public int AmmunitionUnitsOnUse { get; set; }
    public string RequiredWeaponItemInstanceId { get; set; } = string.Empty;
    public int UsesSinceShortRest { get; set; }
    public int UsesSinceLongRest { get; set; }
    public bool IsPrepared { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<string> UnavailableReasonCodes { get; set; } = new();
    public DateTime? LastUsedAtUtc { get; set; }
    public string LastUsedAtWorldTime { get; set; } = string.Empty;
    public int? LastUsedCombatRound { get; set; }
    public string LastOperationId { get; set; } = string.Empty;
    public long Revision { get; set; }
}

public sealed class AmmunitionFeedState
{
    public string FeedKind { get; set; } = "magazine";
    public string LoadedAmmunitionDefinitionId { get; set; } = string.Empty;
    public int LoadedQuantity { get; set; }
    public int Capacity { get; set; }
    public int ChamberedQuantity { get; set; }
    public string FireMode { get; set; } = string.Empty;
    public List<string> CompatibleAmmunitionTags { get; set; } = new();
    public bool ReloadRequired { get; set; }
    public string ReloadState { get; set; } = "ready";
    public decimal ReloadProgress { get; set; }
    public List<string> SourceItemInstanceIds { get; set; } = new();
    public long Revision { get; set; }
}

public sealed class ItemOperationalState
{
    public string ItemInstanceId { get; set; } = string.Empty;
    public string OperationalMode { get; set; } = string.Empty;
    public bool IsEquipped { get; set; }
    public bool IsActive { get; set; }
    public bool IsJammed { get; set; }
    public bool IsBroken { get; set; }
    public decimal DurabilityCurrent { get; set; }
    public decimal DurabilityMaximum { get; set; }
    public int CurrentCharges { get; set; }
    public int MaximumCharges { get; set; }
    public AmmunitionFeedState? AmmunitionFeed { get; set; }
    public string LastOperationId { get; set; } = string.Empty;
    public long Revision { get; set; }
}

public sealed class LifeOperationalState
{
    public string StateCode { get; set; } = "healthy";
    public string PreviousStateCode { get; set; } = string.Empty;
    public string TransitionReasonCode { get; set; } = string.Empty;
    public string TransitionSourceType { get; set; } = string.Empty;
    public string TransitionSourceId { get; set; } = string.Empty;
    public string SinceWorldTime { get; set; } = string.Empty;
    public DateTime SinceUtc { get; set; } = DateTime.UtcNow;
    public bool CanAct { get; set; } = true;
    public bool CanReact { get; set; } = true;
    public bool CanCommunicate { get; set; } = true;
    public bool RequiresGmResolution { get; set; }
    public int? StabilizationSuccesses { get; set; }
    public int? StabilizationFailures { get; set; }
    public long Revision { get; set; }
}

public sealed class ActiveLoadoutState
{
    public string SubjectId { get; set; } = string.Empty;
    public string PrimaryHandItemInstanceId { get; set; } = string.Empty;
    public string SecondaryHandItemInstanceId { get; set; } = string.Empty;
    public string ActiveWeaponItemInstanceId { get; set; } = string.Empty;
    public string ActiveAttackProfileId { get; set; } = string.Empty;
    public string GripMode { get; set; } = string.Empty;
    public string SelectedFireMode { get; set; } = string.Empty;
    public string SelectedAmmunitionDefinitionId { get; set; } = string.Empty;
    public string SafetyState { get; set; } = string.Empty;
    public bool IsReadied { get; set; }
    public List<string> AttunedItemInstanceIds { get; set; } = new();
    public int AttunementLimit { get; set; }
    public long Revision { get; set; }
}

public sealed class ActorRuntimeStateDocument : EntityBase
{
    public string SubjectType { get; set; } = RuntimeSubjectTypes.Character;
    public string SubjectId { get; set; } = string.Empty;
    public string? CharacterId { get; set; }
    public string WorldId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string DisplayNameSnapshot { get; set; } = string.Empty;
    public List<RuntimeResourceState> ResourceStates { get; set; } = new();
    public List<ActionRuntimeState> ActionStates { get; set; } = new();
    public List<ItemOperationalState> ItemOperationalStates { get; set; } = new();
    public Dictionary<string, decimal> RuntimeCounters { get; set; } = new();
    public List<string> ActiveRuntimeReferences { get; set; } = new();
    public LifeOperationalState LifeState { get; set; } = new();
    public ActiveLoadoutState Loadout { get; set; } = new();
    public long EntityRevision { get; set; }
    public string CalculationVersion { get; set; } = "0.21.6";
    public string UpdatedBy { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}

public sealed class RuntimeSubjectCapacityProfile : EntityBase
{
    public string SubjectType { get; set; } = RuntimeSubjectTypes.Npc;
    public string SubjectId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public Dictionary<string, decimal> ResourceMaximums { get; set; } = new();
    public string Source { get; set; } = "profile_native";
    public string UpdatedByUserId { get; set; } = string.Empty;
    public long Revision { get; set; } = 1;
}

public sealed class RuntimeEffectInstance : EntityBase
{
    public string EffectInstanceId { get; set; } = Guid.NewGuid().ToString("N");
    public string ConditionDefinitionId { get; set; } = string.Empty;
    public RuntimeSubjectReference SourceSubject { get; set; } = new();
    public RuntimeSubjectReference TargetSubject { get; set; } = new();
    public string PublicNameSnapshot { get; set; } = string.Empty;
    public string PublicDescriptionSnapshot { get; set; } = string.Empty;
    public string GmNameSnapshot { get; set; } = string.Empty;
    public int StackCount { get; set; } = 1;
    public string DurationMode { get; set; } = "until_removed";
    public int? RemainingTurns { get; set; }
    public int? RemainingRounds { get; set; }
    public string RemainingSceneTime { get; set; } = string.Empty;
    public string RemainingWorldTime { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public string StackingPolicySnapshot { get; set; } = "independent";
    public string ConcentrationExecutionId { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public bool IsPlayerVisible { get; set; }
    public bool IsModifierReasonPlayerVisible { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, decimal> CapabilityModifiers { get; set; } = new();
    public Dictionary<string, decimal> ResourceMaximumModifiers { get; set; } = new();
    public string AppliedByUserId { get; set; } = string.Empty;
    public long Revision { get; set; }
}

public sealed class ActionExecutionState : EntityBase
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");
    public string ActionDefinitionId { get; set; } = string.Empty;
    public RuntimeSubjectReference ActorSubject { get; set; } = new();
    public List<RuntimeSubjectReference> TargetReferences { get; set; } = new();
    public string State { get; set; } = "prepared";
    public int CurrentStage { get; set; }
    public int TotalStages { get; set; }
    public int RemainingRounds { get; set; }
    public string ConcentrationSlotId { get; set; } = string.Empty;
    public List<string> ReservedResourceReferences { get; set; } = new();
    public List<string> SpentResourceReferences { get; set; } = new();
    public List<string> InterruptConditionReferences { get; set; } = new();
    public string PublicProgress { get; set; } = string.Empty;
    public string GmProgress { get; set; } = string.Empty;
    public string LastOperationId { get; set; } = string.Empty;
    public long Revision { get; set; }
}

public sealed class ResourceReservationState : EntityBase
{
    public string ReservationId { get; set; } = Guid.NewGuid().ToString("N");
    public string SubjectId { get; set; } = string.Empty;
    public string ResourceDefinitionId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public decimal ReservedAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public string ReleasePolicy { get; set; } = "release";
    public string State { get; set; } = "active";
    public long Revision { get; set; }
}

public sealed class LiveStateEventRecord : EntityBase
{
    public string SubjectId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public string GmOnlyDetail { get; set; } = string.Empty;
    public string OldSummary { get; set; } = string.Empty;
    public string NewSummary { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string CompensationForEventId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public long Revision { get; set; }
}

public sealed class LiveCapabilitySnapshot
{
    public string CapabilityType { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal BaseValue { get; set; }
    public decimal PermanentModifier { get; set; }
    public decimal TemporaryModifier { get; set; }
    public decimal EffectiveValue { get; set; }
    public List<string> PublicModifierReasons { get; set; } = new();
    public List<string> GmModifierReasons { get; set; } = new();
    public bool IsDisabled { get; set; }
    public List<string> DisabledReasonCodes { get; set; } = new();
    public string ToolEquipmentContext { get; set; } = string.Empty;
    public string CalculationVersion { get; set; } = "0.21.6";
    public long CalculatedAtRevision { get; set; }
}

public sealed class RuntimeResourceDefinition : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string DisplayMode { get; set; } = "bar";
    public decimal Minimum { get; set; }
    public string MaximumFormula { get; set; } = string.Empty;
    public string MaximumProfileReference { get; set; } = string.Empty;
    public bool AllowOvercap { get; set; }
    public string ClampPolicy { get; set; } = "clamp";
    public List<string> RecoveryPolicies { get; set; } = new();
    public List<string> SpendPolicies { get; set; } = new();
    public string Visibility { get; set; } = "player";
    public Dictionary<string, decimal> CriticalThresholds { get; set; } = new();
    public int SortOrder { get; set; }
    public string RuleSetId { get; set; } = string.Empty;
    public int DefinitionVersion { get; set; } = 1;
}

public sealed class RuntimeActionDefinition : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceDefinitionId { get; set; } = string.Empty;
    public string ActionCategory { get; set; } = string.Empty;
    public Dictionary<string, decimal> ResourceCosts { get; set; } = new();
    public string CooldownPolicyId { get; set; } = string.Empty;
    public int MaximumCharges { get; set; }
    public string RestResetPolicy { get; set; } = string.Empty;
    public bool RequiresWeapon { get; set; }
    public bool RequiresAmmunition { get; set; }
    public List<string> ConditionRestrictions { get; set; } = new();
    public List<string> PublicUnavailableReasons { get; set; } = new();
    public string Visibility { get; set; } = "player";
    public string RuleSetId { get; set; } = string.Empty;
    public int DefinitionVersion { get; set; } = 1;
}

public sealed class CooldownPolicyDefinition : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Mode { get; set; } = "none";
    public int Turns { get; set; }
    public int Rounds { get; set; }
    public string ResetPolicy { get; set; } = string.Empty;
    public int Charges { get; set; }
    public string CustomServerPolicy { get; set; } = string.Empty;
}

public sealed class AmmunitionFeedProfileDefinition : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string FeedKind { get; set; } = "magazine";
    public int Capacity { get; set; }
    public string ChamberRules { get; set; } = string.Empty;
    public string ReloadActionCost { get; set; } = string.Empty;
    public List<string> CompatibleAmmunitionTags { get; set; } = new();
    public string MixedAmmunitionPolicy { get; set; } = "forbidden";
    public bool PartialReloadAllowed { get; set; }
    public string UnloadPolicy { get; set; } = string.Empty;
    public string ReserveCalculation { get; set; } = "inventory_profile";
    public string Visibility { get; set; } = "player";
}

public sealed class LifeStateProfileDefinition : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public List<string> States { get; set; } = new();
    public Dictionary<string, List<string>> TransitionRules { get; set; } = new();
    public string ZeroResourceBehavior { get; set; } = "ruleset_transition";
    public string StabilizationPolicy { get; set; } = string.Empty;
    public Dictionary<string, bool> CanActPolicies { get; set; } = new();
    public Dictionary<string, bool> CanReactPolicies { get; set; } = new();
    public List<string> ActorTypes { get; set; } = new();
    public string RuleSetId { get; set; } = string.Empty;
    public int DefinitionVersion { get; set; } = 1;
}

public sealed class ActionExecutionPolicyDefinition : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public List<string> AllowedStates { get; set; } = new();
    public string StageModel { get; set; } = string.Empty;
    public string PreparationPolicy { get; set; } = string.Empty;
    public int ConcentrationSlots { get; set; }
    public int SustainSlots { get; set; }
    public List<string> InterruptConditions { get; set; } = new();
    public string ReservationTiming { get; set; } = string.Empty;
    public string SpendTiming { get; set; } = string.Empty;
    public string Visibility { get; set; } = "player";
}

public sealed class LoadoutProfileDefinition : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public List<string> HandSlots { get; set; } = new();
    public List<string> EquipmentSlots { get; set; } = new();
    public string ReadyStowedPolicy { get; set; } = string.Empty;
    public List<string> GripModes { get; set; } = new();
    public int AttunementLimit { get; set; }
    public int QuickSlotCount { get; set; }
    public List<string> BodyCompatibilityTags { get; set; } = new();
    public string RuleSetId { get; set; } = string.Empty;
    public int DefinitionVersion { get; set; } = 1;
}

public sealed class PlayerLiveResourceView
{
    public string ResourceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Current { get; set; }
    public decimal EffectiveMaximum { get; set; }
    public decimal BaseMaximum { get; set; }
    public decimal Reserved { get; set; }
    public decimal Overcap { get; set; }
    public string CapacitySource { get; set; } = string.Empty;
    public string CalculationVersion { get; set; } = "0.21.6A";
}

public sealed class PlayerLiveWeaponView
{
    public string ItemInstanceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string OperationalMode { get; set; } = string.Empty;
    public bool IsEquipped { get; set; }
    public bool IsActive { get; set; }
    public bool IsJammed { get; set; }
    public bool IsBroken { get; set; }
    public decimal DurabilityCurrent { get; set; }
    public decimal DurabilityMaximum { get; set; }
    public int LoadedQuantity { get; set; }
    public int ReserveQuantity { get; set; }
    public int Capacity { get; set; }
    public int ChamberedQuantity { get; set; }
    public string FireMode { get; set; } = string.Empty;
    public long Revision { get; set; }
}

public sealed class LiveCombatContextView
{
    public bool IsInCombat { get; set; }
    public int ActionPoints { get; set; }
    public int MinorActionPoints { get; set; }
    public int ReactionCount { get; set; }
    public int ReactionLimit { get; set; }
    public bool HasActedThisRound { get; set; }
}

public sealed class PlayerLiveActorView
{
    public string SubjectType { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LifeState { get; set; } = string.Empty;
    public bool CanAct { get; set; }
    public bool CanReact { get; set; }
    public long Revision { get; set; }
    public List<PlayerLiveResourceView> Resources { get; set; } = new();
    public List<LiveCapabilitySnapshot> Capabilities { get; set; } = new();
    public List<RuntimeEffectInstance> Effects { get; set; } = new();
    public List<ActionRuntimeState> Actions { get; set; } = new();
    public List<PlayerLiveWeaponView> Weapons { get; set; } = new();
    public ActiveLoadoutState Loadout { get; set; } = new();
    public LiveCombatContextView Combat { get; set; } = new();
    public List<ActionExecutionState> Executions { get; set; } = new();
    public List<LiveStateEventRecord> History { get; set; } = new();
    public List<string> ReconciliationWarnings { get; set; } = new();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}
