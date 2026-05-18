using System;
using System.Collections.Generic;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public sealed class CharacterProfileShadowBundle
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public AttributeProfile AttributeProfile { get; set; } = new AttributeProfile();
    public WalletProfile WalletProfile { get; set; } = new WalletProfile();
    public SkillProfile SkillProfile { get; set; } = new SkillProfile();
    public DevelopmentProfile DevelopmentProfile { get; set; } = new DevelopmentProfile();
    public InventoryProfile InventoryProfile { get; set; } = new InventoryProfile();
    public ReputationProfile ReputationProfile { get; set; } = new ReputationProfile();
    public HoldingsProfile HoldingsProfile { get; set; } = new HoldingsProfile();
    public CompanionProfile CompanionProfile { get; set; } = new CompanionProfile();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
    public int SchemaVersion { get; set; } = 1;
}

public sealed class CharacterShadowSectionCompareResult
{
    public string Section { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public int DifferenceCount { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
}

public sealed class CharacterShadowCompareResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public DateTime ComparedAtUtc { get; set; } = DateTime.UtcNow;
    public List<CharacterShadowSectionCompareResult> SectionResults { get; set; } = new List<CharacterShadowSectionCompareResult>();
    public List<string> Differences { get; set; } = new List<string>();
}

public interface ICharacterProfileShadowBuilder
{
    CharacterProfileShadowBundle BuildShadowBundleFromLegacy(Character character);
    CharacterShadowCompareResult CompareLegacyToShadow(Character character);
}

public sealed class CharacterProfileShadowBuilder : ICharacterProfileShadowBuilder
{
    private readonly ICharacterAttributeProfileFactory _attributeFactory;
    private readonly ICharacterWalletProfileFactory _walletFactory;
    private readonly ICharacterSkillProfileFactory _skillFactory;
    private readonly ICharacterDevelopmentProfileFactory _developmentFactory;
    private readonly ICharacterInventoryProfileFactory _inventoryFactory;
    private readonly ICharacterReputationProfileFactory _reputationFactory;
    private readonly ICharacterHoldingsProfileFactory _holdingsFactory;
    private readonly ICharacterCompanionProfileFactory _companionFactory;
    private readonly IServerLogger _logger;

    public CharacterProfileShadowBuilder(
        ICharacterAttributeProfileFactory attributeFactory,
        ICharacterWalletProfileFactory walletFactory,
        ICharacterSkillProfileFactory skillFactory,
        ICharacterDevelopmentProfileFactory developmentFactory,
        ICharacterInventoryProfileFactory inventoryFactory,
        ICharacterReputationProfileFactory reputationFactory,
        ICharacterHoldingsProfileFactory holdingsFactory,
        ICharacterCompanionProfileFactory companionFactory,
        IServerLogger logger)
    {
        _attributeFactory = attributeFactory;
        _walletFactory = walletFactory;
        _skillFactory = skillFactory;
        _developmentFactory = developmentFactory;
        _inventoryFactory = inventoryFactory;
        _reputationFactory = reputationFactory;
        _holdingsFactory = holdingsFactory;
        _companionFactory = companionFactory;
        _logger = logger;
    }

    public CharacterProfileShadowBundle BuildShadowBundleFromLegacy(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));

        var bundle = new CharacterProfileShadowBundle
        {
            CharacterId = character.Id,
            RuleSetId = RuleSetIds.FantasyNriDefault,
            AttributeProfile = _attributeFactory.BuildFromLegacyCharacter(character),
            WalletProfile = _walletFactory.BuildFromLegacyCharacter(character),
            SkillProfile = _skillFactory.BuildFromLegacyCharacter(character),
            DevelopmentProfile = _developmentFactory.BuildFromLegacyCharacter(character),
            InventoryProfile = _inventoryFactory.BuildFromLegacyCharacter(character),
            ReputationProfile = _reputationFactory.BuildFromLegacyCharacter(character),
            HoldingsProfile = _holdingsFactory.BuildFromLegacyCharacter(character),
            CompanionProfile = _companionFactory.BuildFromLegacyCharacter(character),
            BuiltAtUtc = DateTime.UtcNow,
            SchemaVersion = 1
        };

        _logger.Debug($"character.shadow.bundle.build characterId={bundle.CharacterId} sections=attributes,wallet,skills,development,inventory,reputation,holdings,companions");
        return bundle;
    }

    public CharacterShadowCompareResult CompareLegacyToShadow(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));

        var shadow = BuildShadowBundleFromLegacy(character);
        var sectionResults = new List<CharacterShadowSectionCompareResult>();

        sectionResults.Add(ToSectionResult("attributes", _attributeFactory.CompareLegacyToProfile(character, shadow.AttributeProfile).Differences));
        sectionResults.Add(ToSectionResult("wallet", _walletFactory.CompareLegacyToProfile(character, shadow.WalletProfile).Differences));
        sectionResults.Add(ToSectionResult("skills", _skillFactory.CompareLegacyToProfile(character, shadow.SkillProfile).Differences));
        sectionResults.Add(ToSectionResult("development", _developmentFactory.CompareLegacyToProfile(character, shadow.DevelopmentProfile).Differences));
        sectionResults.Add(ToSectionResult("inventory", _inventoryFactory.CompareLegacyToProfile(character, shadow.InventoryProfile).Differences));
        sectionResults.Add(ToSectionResult("reputation", _reputationFactory.CompareLegacyToProfile(character, shadow.ReputationProfile).Differences));
        sectionResults.Add(ToSectionResult("holdings", _holdingsFactory.CompareLegacyToProfile(character, shadow.HoldingsProfile).Differences));
        sectionResults.Add(ToSectionResult("companions", _companionFactory.CompareLegacyToProfile(character, shadow.CompanionProfile).Differences));

        var differences = new List<string>();
        foreach (var section in sectionResults)
        {
            _logger.Debug($"character.shadow.section section={section.Section} equivalent={section.IsEquivalent} diffCount={section.DifferenceCount}");
            foreach (var diff in section.Differences)
            {
                differences.Add($"{section.Section}:{diff}");
            }
        }

        var result = new CharacterShadowCompareResult
        {
            CharacterId = character.Id,
            ComparedAtUtc = DateTime.UtcNow,
            IsEquivalent = differences.Count == 0,
            SectionResults = sectionResults,
            Differences = differences
        };

        _logger.Debug($"character.shadow.compare characterId={result.CharacterId} equivalent={result.IsEquivalent} diffCount={result.Differences.Count}");
        return result;
    }

    private static CharacterShadowSectionCompareResult ToSectionResult(string section, List<string> differences)
    {
        var safeDiffs = differences ?? new List<string>();
        return new CharacterShadowSectionCompareResult
        {
            Section = section,
            IsEquivalent = safeDiffs.Count == 0,
            DifferenceCount = safeDiffs.Count,
            Differences = safeDiffs
        };
    }
}
