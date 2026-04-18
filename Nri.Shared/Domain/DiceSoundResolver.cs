using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class DiceSoundResolver
{
    public static (string SoundKey, bool EasterTriggered) Resolve(DiceFormulaSpec formula, IReadOnlyList<int> rolls)
    {
        var diceCount = formula?.DiceCount ?? 1;
        var normalizedCount = diceCount < 1 ? 1 : diceCount > 6 ? 6 : diceCount;
        var easterTriggered = normalizedCount == 4 && rolls.Any(value => value == 1);
        if (easterTriggered)
        {
            return ("dice_4_easter", true);
        }

        return ($"dice_{normalizedCount}", false);
    }
}
