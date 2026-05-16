using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface ICharacterWalletProfileFactory
{
    WalletProfile BuildFromLegacyCharacter(Character character);
    WalletProfile BuildEmpty(string characterId, string ruleSetId);
    WalletProfileComparisonResult CompareLegacyToProfile(Character character, WalletProfile profile);
}

public sealed class CharacterWalletProfileFactory : ICharacterWalletProfileFactory
{
    public WalletProfile BuildFromLegacyCharacter(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        character.Wallet.EnsureAllDenominations();

        var profile = BuildEmpty(character.Id, RuleSetIds.FantasyNriDefault);
        profile.Wallets = new List<CharacterWalletValue>
        {
            BuildValue(CharacterCurrencyIds.IronCoin, GetLegacyAmount(character, CurrencyDenomination.Iron)),
            BuildValue(CharacterCurrencyIds.BronzeCoin, GetLegacyAmount(character, CurrencyDenomination.Bronze)),
            BuildValue(CharacterCurrencyIds.SilverCoin, GetLegacyAmount(character, CurrencyDenomination.Silver)),
            BuildValue(CharacterCurrencyIds.GoldCoin, GetLegacyAmount(character, CurrencyDenomination.Gold)),
            BuildValue(CharacterCurrencyIds.PlatinumCoin, GetLegacyAmount(character, CurrencyDenomination.Platinum)),
            BuildValue(CharacterCurrencyIds.OrichalcumCoin, GetLegacyAmount(character, CurrencyDenomination.Orichalcum)),
            BuildValue(CharacterCurrencyIds.AdamantCoin, GetLegacyAmount(character, CurrencyDenomination.Adamant)),
            BuildValue(CharacterCurrencyIds.SovereignCoin, GetLegacyAmount(character, CurrencyDenomination.Sovereign)),
            BuildValue(CharacterCurrencyIds.XpCoin, character.XpCoins)
        };
        return profile;
    }

    public WalletProfile BuildEmpty(string characterId, string ruleSetId)
    {
        return new WalletProfile
        {
            CharacterId = characterId ?? string.Empty,
            RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
            Wallets = new List<CharacterWalletValue>(),
            SchemaVersion = 1
        };
    }

    public WalletProfileComparisonResult CompareLegacyToProfile(Character character, WalletProfile profile)
    {
        var expected = BuildFromLegacyCharacter(character);
        var actualMap = (profile?.Wallets ?? new List<CharacterWalletValue>())
            .GroupBy(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        var diffs = new List<string>();
        foreach (var wallet in expected.Wallets)
        {
            if (!actualMap.TryGetValue(wallet.CurrencyId, out var actual))
            {
                diffs.Add($"missing:{wallet.CurrencyId}");
                continue;
            }

            if (actual.Amount != wallet.Amount) diffs.Add($"amount:{wallet.CurrencyId}");
        }

        return new WalletProfileComparisonResult
        {
            CharacterId = character.Id,
            IsEquivalent = diffs.Count == 0,
            Differences = diffs,
            ComparedAtUtc = DateTime.UtcNow
        };
    }

    private static CharacterWalletValue BuildValue(string currencyId, long amount)
    {
        return new CharacterWalletValue { CurrencyId = currencyId, Amount = amount, Source = "legacy_shadow" };
    }

    private static long GetLegacyAmount(Character character, CurrencyDenomination denomination)
    {
        var key = denomination.ToString();
        return character.Wallet.Balance.Amounts.TryGetValue(key, out var value) ? value : 0L;
    }
}

public sealed class WalletProfileComparisonResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public DateTime ComparedAtUtc { get; set; }
}
