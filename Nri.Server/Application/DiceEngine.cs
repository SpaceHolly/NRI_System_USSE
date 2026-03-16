using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public static class DiceLimits
{
    public const int MaxDiceCount = 20;
    public const int MaxDiceSides = 1000;
    public const int MaxModifier = 1000;
}

public static class DiceFormulaParser
{
    private static readonly Regex Regex = new Regex(@"^\s*(\d+)\s*d\s*(\d+)\s*([+-]\s*\d+)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static DiceFormulaSpec Parse(string input)
    {
        var m = Regex.Match(input ?? string.Empty);
        if (!m.Success) throw new ArgumentException("Invalid dice formula.");

        var count = int.Parse(m.Groups[1].Value);
        var sides = int.Parse(m.Groups[2].Value);
        var modRaw = m.Groups[3].Success ? m.Groups[3].Value.Replace(" ", string.Empty) : "0";
        var mod = int.Parse(modRaw);

        DiceFormulaValidator.Validate(count, sides, mod);

        return new DiceFormulaSpec
        {
            DiceCount = count,
            DiceSides = sides,
            Modifier = mod,
            Normalized = $"{count}d{sides}{(mod == 0 ? string.Empty : mod > 0 ? "+" + mod : mod.ToString())}"
        };
    }
}

public static class DiceFormulaValidator
{
    public static void Validate(int count, int sides, int modifier)
    {
        if (count <= 0 || count > DiceLimits.MaxDiceCount) throw new ArgumentException($"Dice count must be 1..{DiceLimits.MaxDiceCount}");
        if (sides <= 1 || sides > DiceLimits.MaxDiceSides) throw new ArgumentException($"Dice sides must be 2..{DiceLimits.MaxDiceSides}");
        if (Math.Abs(modifier) > DiceLimits.MaxModifier) throw new ArgumentException($"Modifier absolute value must be <= {DiceLimits.MaxModifier}");
    }
}

public static class DiceRollExecutor
{
    public static DiceRollResult Execute(DiceFormulaSpec spec, RequestVisibility visibility, string approvedBy)
    {
        var rng = new Random();
        var rolls = new List<int>();
        var sum = 0;
        for (var i = 0; i < spec.DiceCount; i++)
        {
            var roll = rng.Next(1, spec.DiceSides + 1);
            rolls.Add(roll);
            sum += roll;
        }

        return new DiceRollResult
        {
            NormalizedFormula = spec.Normalized,
            Rolls = rolls,
            Modifier = spec.Modifier,
            Total = sum + spec.Modifier,
            Visibility = visibility,
            ApprovedByUserId = approvedBy,
            ApprovedAtUtc = DateTime.UtcNow
        };
    }
}
