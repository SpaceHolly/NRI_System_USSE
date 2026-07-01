namespace Nri.Shared.Domain;

public static class FateFeatureFlags
{
    public const bool UseFateEngineMvp = false;
    public const bool UseFateServerPipeline = false;
    public const bool UseFateAutomatedModifiers = false;
    public const bool UseFateControlLayout = false;
    public const bool UseFateRollLogs = false;
    public const bool UseFatePlayerSafeFiltering = false;
}

public static class FateTerrainProfiles
{
    public const string Calm = "calm";
    public const string Battle = "battle";
    public const string CursedLand = "cursed_land";
    public const string BlessedLand = "blessed_land";
    public const string Hell = "hell";
    public const string Chaos = "chaos";
    public const string Drama = "drama";
    public const string KeyMoment = "key_moment";
    public const string AnomalousSpace = "anomalous_space";
}

public static class FateRollTypes
{
    public const string Dice = "dice";
    public const string SkillCheck = "skill_check";
    public const string CombatCheck = "combat_check";
    public const string SavingThrow = "saving_throw";
    public const string Custom = "custom";
}
