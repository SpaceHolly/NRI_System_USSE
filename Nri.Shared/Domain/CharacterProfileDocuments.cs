using System;

namespace Nri.Shared.Domain;

// Character v2 profile documents are the profile-first source for migrated sections.
// Legacy Character can still be synchronized as a compatibility facade.

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

public class CharacterSubAttributeProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public SubAttributeProfile Profile { get; set; } = new SubAttributeProfile();
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

public class CharacterInventoryProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public InventoryProfile Profile { get; set; } = new InventoryProfile();
}

public class CharacterReputationProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public ReputationProfile Profile { get; set; } = new ReputationProfile();
}

public class CharacterHoldingsProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public HoldingsProfile Profile { get; set; } = new HoldingsProfile();
}

public class CharacterCompanionProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public CompanionProfile Profile { get; set; } = new CompanionProfile();
}

public class CharacterRaceOrSpeciesProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public RaceOrSpeciesProfile Profile { get; set; } = new RaceOrSpeciesProfile();
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
