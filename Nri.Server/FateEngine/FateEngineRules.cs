namespace Nri.Server.FateEngine;

public static class FateEngineRules
{
    public static bool IsFateEligible(int dieSides)
    {
        return dieSides > 4;
    }

    public static bool IsLayerAllowedForDie(int dieSides, int layerNumber)
    {
        if (dieSides <= 4)
        {
            return false;
        }

        if (dieSides <= 12)
        {
            return layerNumber >= 3;
        }

        if (dieSides <= 19)
        {
            return layerNumber >= 2;
        }

        return true;
    }
}
