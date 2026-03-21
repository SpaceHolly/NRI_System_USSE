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
    public string DirectionId { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> NextNodeIds { get; set; } = new List<string>();
    public List<UnlockRequirement> Requirements { get; set; } = new List<UnlockRequirement>();
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
    public SkillActivationCondition Activation { get; set; } = new SkillActivationCondition();
    public string UsageDescription { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
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
    public bool Acquired { get; set; }
    public bool Available { get; set; }
    public string UnavailableReason { get; set; } = string.Empty;
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
