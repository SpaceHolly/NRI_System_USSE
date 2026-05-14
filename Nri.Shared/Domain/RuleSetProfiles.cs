using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class RuleSetDefinition : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VersionTag { get; set; } = "1.0.0";
    public bool IsActive { get; set; } = true;
    public Dictionary<string, bool> EnabledProfiles { get; set; } = new Dictionary<string, bool>();
}

public sealed class CharacterModuleState
{
    public string RuleSetCode { get; set; } = string.Empty;
    public Dictionary<string, bool> Modules { get; set; } = new Dictionary<string, bool>();
    public int Revision { get; set; }
}

public sealed class AttributeProfile
{
    public Dictionary<string, int> Attributes { get; set; } = new Dictionary<string, int>();
}

public sealed class SkillProfile
{
    public List<SkillProfileEntry> Entries { get; set; } = new List<SkillProfileEntry>();
}

public sealed class SkillProfileEntry
{
    public string SkillId { get; set; } = string.Empty;
    public int Rank { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DevelopmentProfile
{
    public List<DevelopmentNodeState> Nodes { get; set; } = new List<DevelopmentNodeState>();
    public int DevelopmentCurrency { get; set; }
}

public sealed class DevelopmentNodeState
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public int Tier { get; set; }
    public bool Acquired { get; set; }
}

public sealed class WalletProfile
{
    public Dictionary<string, long> Balances { get; set; } = new Dictionary<string, long>();
}

public sealed class BodyProfile
{
    public Dictionary<string, int> BodyStats { get; set; } = new Dictionary<string, int>();
}

public sealed class KnowledgeProfile
{
    public List<string> KnownTopics { get; set; } = new List<string>();
    public List<string> Languages { get; set; } = new List<string>();
}

public sealed class ConditionProfile
{
    public List<string> Conditions { get; set; } = new List<string>();
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
