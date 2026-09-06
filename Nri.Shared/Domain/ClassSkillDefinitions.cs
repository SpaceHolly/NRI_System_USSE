using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public class DefinitionVersion : EntityBase
{
    public string ContentName { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public DateTime LoadedAtUtc { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "json";
}

public class UnlockRequirement
{
    public string RequirementType { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class SkillRequirement : UnlockRequirement { }

public class SkillActivationCondition
{
    public string Description { get; set; } = string.Empty;
    public bool RequiresApprovalOnUse { get; set; }
}

public class PassiveEffectDefinition
{
    public string EffectId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class StatBonusDefinition
{
    public string Stat { get; set; } = string.Empty;
    public int Bonus { get; set; }
}

public class EquipmentRequirementUnlock
{
    public string UnlockCode { get; set; } = string.Empty;
}

public class AbilityRequirementUnlock
{
    public string UnlockCode { get; set; } = string.Empty;
}

public class ClassNodeDefinition
{
    public string NodeId { get; set; } = string.Empty;
    public string HexagonId { get; set; } = "main_development_hexagon";
    public string HexagonType { get; set; } = "main";
    public string ClassId { get; set; } = string.Empty;
    public string DirectionId { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PublicName { get; set; } = string.Empty;
    public string HiddenName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string NodeType { get; set; } = DevelopmentNodeTypes.Class;
    public string NodeRole { get; set; } = DevelopmentNodeRoleIds.MainBranchLevel;
    public int Tier { get; set; } = 1;
    public int MaxTier { get; set; } = 20;
    public int CostExperienceCoins { get; set; } = 1;
    public bool RequiresGMApproval { get; set; }
    public bool RequiresPlayerRequest { get; set; }
    public string UnlockPolicy { get; set; } = DevelopmentUnlockPolicyIds.VisibleByDefault;
    public string PurchasePolicy { get; set; } = DevelopmentPurchasePolicyIds.AutomaticIfRequirementsMet;
    public string VisibilityRule { get; set; } = "public";
    public bool IsHidden { get; set; }
    public bool IsArchived { get; set; }
    public string RequirementSummary { get; set; } = string.Empty;
    public string RewardSummary { get; set; } = string.Empty;
    public int GridX { get; set; }
    public int GridY { get; set; }
    public double Angle { get; set; }
    public int Ring { get; set; } = 1;
    public int Sector { get; set; }
    public int SortOrder { get; set; }
    public int LayoutVersion { get; set; } = 1;
    public string LayoutGroup { get; set; } = string.Empty;
    public int LayoutLayer { get; set; }
    public string LayoutBranch { get; set; } = string.Empty;
    public int LayoutWeight { get; set; }
    public string LayoutGeneratedBy { get; set; } = string.Empty;
    public DateTime? LayoutGeneratedAtUtc { get; set; }
    public bool LayoutLockedManualPosition { get; set; }
    public string LayoutPresetId { get; set; } = string.Empty;
    public string LayoutSnapshotId { get; set; } = string.Empty;
    public int LayoutSnapshotPositionX { get; set; }
    public int LayoutSnapshotPositionY { get; set; }
    public DateTime? LayoutSnapshotCreatedAtUtc { get; set; }
    public int Revision { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public string CurrencyId { get; set; } = "xp_coin";
    public string LinkedDefinitionKind { get; set; } = string.Empty;
    public string LinkedDefinitionId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsGMOnly { get; set; }
    public bool IsPrimaryMagicClass { get; set; }
    public string PrimaryMagicGroupId { get; set; } = string.Empty;
    public string MagicRestrictionSummary { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public List<string> NextNodeIds { get; set; } = new List<string>();
    public List<UnlockRequirement> Requirements { get; set; } = new List<UnlockRequirement>();
    public RequirementExpression? RequirementExpression { get; set; }
    public List<StatBonusDefinition> StatBonuses { get; set; } = new List<StatBonusDefinition>();
    public List<PassiveEffectDefinition> PassiveEffects { get; set; } = new List<PassiveEffectDefinition>();
    public List<string> UnlockSkillIds { get; set; } = new List<string>();
    public List<EquipmentRequirementUnlock> EquipmentUnlocks { get; set; } = new List<EquipmentRequirementUnlock>();
    public List<AbilityRequirementUnlock> AbilityUnlocks { get; set; } = new List<AbilityRequirementUnlock>();
}

public class ClassBranchDefinition
{
    public string BranchId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> NodeIds { get; set; } = new List<string>();
}

public class ClassDirectionDefinition
{
    public string DirectionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ClassBranchDefinition> Branches { get; set; } = new List<ClassBranchDefinition>();
}

public class ClassTreeDefinition : EntityBase
{
    public string DirectionId { get; set; } = string.Empty;
    public List<ClassNodeDefinition> Nodes { get; set; } = new List<ClassNodeDefinition>();
}

public class SkillDefinitionRecord : EntityBase
{
    public string SkillId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SkillType Type { get; set; }
    public List<SkillRequirement> Requirements { get; set; } = new List<SkillRequirement>();
    public RequirementExpression? RequirementExpression { get; set; }
    public SkillActivationCondition Activation { get; set; } = new SkillActivationCondition();
    public string UsageDescription { get; set; } = string.Empty;
    public string DefaultAttribute { get; set; } = string.Empty;
    public string DefaultSubAttribute { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public int RankMin { get; set; }
    public int RankMax { get; set; } = 20;
    public List<SkillRankMilestoneDefinition> RankMilestones { get; set; } = new List<SkillRankMilestoneDefinition>();
    public List<SkillTechniqueDefinition> Techniques { get; set; } = new List<SkillTechniqueDefinition>();
}

public sealed class SkillRankMilestoneDefinition
{
    public int Rank { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public RequirementExpression? RequirementExpression { get; set; }
}

public sealed class SkillTechniqueDefinition : EntityBase
{
    public string DisplayName { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public int MinimumRank { get; set; }
    public int? MaximumRank { get; set; }
    public string ActionDefinitionId { get; set; } = string.Empty;
    public List<string> RequiredEquipmentTags { get; set; } = new List<string>();
    public string RequiredAbilityId { get; set; } = string.Empty;
    public string RequiredStateId { get; set; } = string.Empty;
    public int HalfActionCost { get; set; } = 1;
    public int ReactionCost { get; set; }
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = "fantasy_nri_default";
    public int Revision { get; set; } = 1;
    public bool IsArchived { get; set; }
    public RequirementExpression? RequirementExpression { get; set; }
}

public class CharacterClassNodeState
{
    public string NodeId { get; set; } = string.Empty;
    public DateTime AcquiredAtUtc { get; set; } = DateTime.UtcNow;
}

public class CharacterClassDirectionState
{
    public string DirectionId { get; set; } = string.Empty;
    public string? SelectedBranchId { get; set; }
    public List<CharacterClassNodeState> AcquiredNodes { get; set; } = new List<CharacterClassNodeState>();
}

public class CharacterPassiveEffectState
{
    public string EffectId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class CharacterUnlockState
{
    public List<string> EquipmentUnlocks { get; set; } = new List<string>();
    public List<string> AbilityUnlocks { get; set; } = new List<string>();
}

public class CharacterSkillState
{
    public string SkillId { get; set; } = string.Empty;
    public string SkillCode { get => SkillId; set => SkillId = value ?? string.Empty; }
    public bool Acquired { get; set; }
    public bool Available { get; set; }
    public string UnavailableReason { get; set; } = string.Empty;
    public int Tier { get; set; } = 1;
    public int Level { get; set; } = 1;
    public DateTime LearnedUtc { get; set; } = DateTime.UtcNow;
}

public class CharacterClassState
{
    public string ClassCode { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public DateTime LearnedUtc { get; set; } = DateTime.UtcNow;
}

public class CharacterProgressSnapshot
{
    public string CharacterId { get; set; } = string.Empty;
    public List<CharacterClassDirectionState> Directions { get; set; } = new List<CharacterClassDirectionState>();
    public List<StatBonusDefinition> TotalStatBonuses { get; set; } = new List<StatBonusDefinition>();
    public List<CharacterPassiveEffectState> PassiveEffects { get; set; } = new List<CharacterPassiveEffectState>();
    public CharacterUnlockState Unlocks { get; set; } = new CharacterUnlockState();
    public List<CharacterSkillState> Skills { get; set; } = new List<CharacterSkillState>();
    public string DefinitionVersion { get; set; } = "1.0.0";
}
