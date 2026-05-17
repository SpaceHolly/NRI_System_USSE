using System;

namespace Nri.Shared.Domain;

// Profile documents are read-only skeletons at this stage.
// Legacy Character remains source of truth until feature-flagged migration.

public class CharacterModuleStateDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public CharacterModuleState ModuleState { get; set; } = new CharacterModuleState();
}

public class CharacterAttributeProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public AttributeProfile Profile { get; set; } = new AttributeProfile();
}

public class CharacterSkillProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public SkillProfile Profile { get; set; } = new SkillProfile();
}

public class CharacterDevelopmentProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public DevelopmentProfile Profile { get; set; } = new DevelopmentProfile();
}

public class CharacterWalletProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public WalletProfile Profile { get; set; } = new WalletProfile();
}

public class CharacterBodyProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public BodyProfile Profile { get; set; } = new BodyProfile();
}

public class CharacterKnowledgeProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public KnowledgeProfile Profile { get; set; } = new KnowledgeProfile();
}

public class CharacterConditionProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public ConditionProfile Profile { get; set; } = new ConditionProfile();
}
