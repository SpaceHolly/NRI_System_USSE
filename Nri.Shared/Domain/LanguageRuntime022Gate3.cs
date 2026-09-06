using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class LanguageRoleIds022Gate3
{
    public const string Continental = "continental";
    public const string State = "state";
    public const string PoliticalCultural = "political_cultural";
    public const string Racial = "racial";
    public const string Religious = "religious";
    public const string Ancient = "ancient";
    public const string Contact = "contact";
}

public static class LanguageComprehensionResultIds022Gate3
{
    public const string Full = "full";
    public const string Partial = "partial";
    public const string Fragments = "fragments";
    public const string Unavailable = "unavailable";
}

public static class LanguageTrainingSourceTypeIds022Gate3
{
    public const string SelfStudy = "self_study";
    public const string Teacher = "teacher";
    public const string ActiveImmersion = "active_immersion";
    public const string TeachingMaterials = "teaching_materials";
    public const string ReligiousCorpus = "religious_corpus";
    public const string ArchiveResearch = "archive_research";
    public const string GmApproved = "gm_approved";
}

public static class LanguageTrainingSourceStatusIds022Gate3
{
    public const string Pending = "pending";
    public const string Valid = "valid";
    public const string Rejected = "rejected";
}

public static class CharacterLanguageGrantProfileIds022Gate3
{
    public const string Custom = "custom";
    public const string Lutwein = "lutwein";
    public const string Rashid = "rashid_environment";
    public const string Tarad = "tarad_environment";
    public const string Lichtenburg = "lichtenburg";
    public const string Bergenby = "bergenby";
    public const string Launtown = "launtown";
    public const string Fugu = "fugu";
    public const string Dzhau = "dzhau_local";
    public const string Istal = "istal_local";
    public const string Nalpa = "nalpa_local";
    public const string Paven = "paven_local";
    public const string Taura = "taura_local";
}

public sealed class LanguageTrainingState022Gate3
{
    public string LanguageId { get; set; } = string.Empty;
    public int FromLevel { get; set; }
    public int TargetLevel { get; set; }
    public string CostClass { get; set; } = LanguageCostClassIds.Modern;
    public int RequiredStudyHours { get; set; }
    public int AccumulatedStudyHours { get; set; }
    public int RequiredMo { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceStatus { get; set; } = LanguageTrainingSourceStatusIds022Gate3.Pending;
    public string SourcePublicLabel { get; set; } = string.Empty;
    public string LastWorldTimeReference { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
}

public static class LanguageTrainingRules022Gate3
{
    private static readonly int[] StudyHours = { 28, 56, 120, 240, 480 };
    private static readonly Dictionary<string, int[]> MoCosts = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
    {
        [LanguageCostClassIds.Modern] = new[] { 2, 3, 5, 8, 12 },
        [LanguageCostClassIds.Religious] = new[] { 3, 5, 8, 12, 18 },
        [LanguageCostClassIds.Ancient] = new[] { 5, 8, 12, 18, 25 }
    };

    public static int RequiredStudyHoursFor(int fromLevel)
    {
        ValidateFromLevel(fromLevel);
        return StudyHours[fromLevel];
    }

    public static int RequiredMoFor(string costClass, int fromLevel)
    {
        ValidateFromLevel(fromLevel);
        if (!MoCosts.TryGetValue(costClass ?? string.Empty, out var table))
            throw new ArgumentException("Неизвестный класс стоимости языка.", nameof(costClass));
        return table[fromLevel];
    }

    public static string ResolveComprehension(int characterLevel, int requiredLevel)
    {
        if (requiredLevel < 1 || requiredLevel > 5) throw new ArgumentOutOfRangeException(nameof(requiredLevel));
        if (characterLevel <= 0) return LanguageComprehensionResultIds022Gate3.Unavailable;
        var deficit = requiredLevel - Math.Min(characterLevel, 5);
        if (deficit <= 0) return LanguageComprehensionResultIds022Gate3.Full;
        if (deficit == 1) return LanguageComprehensionResultIds022Gate3.Partial;
        if (deficit == 2) return LanguageComprehensionResultIds022Gate3.Fragments;
        return LanguageComprehensionResultIds022Gate3.Unavailable;
    }

    public static bool IsSourceSufficient(string costClass, int targetLevel, string sourceType, bool gmApproved)
    {
        if (targetLevel < 1 || targetLevel > 5) return false;
        if (gmApproved || string.Equals(sourceType, LanguageTrainingSourceTypeIds022Gate3.GmApproved, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(costClass, LanguageCostClassIds.Ancient, StringComparison.OrdinalIgnoreCase))
            return targetLevel <= 3 && string.Equals(sourceType, LanguageTrainingSourceTypeIds022Gate3.ArchiveResearch, StringComparison.OrdinalIgnoreCase);
        if (string.Equals(costClass, LanguageCostClassIds.Religious, StringComparison.OrdinalIgnoreCase))
            return targetLevel <= 3 || string.Equals(sourceType, LanguageTrainingSourceTypeIds022Gate3.ReligiousCorpus, StringComparison.OrdinalIgnoreCase);
        if (targetLevel <= 3) return !string.IsNullOrWhiteSpace(sourceType);
        return string.Equals(sourceType, LanguageTrainingSourceTypeIds022Gate3.Teacher, StringComparison.OrdinalIgnoreCase)
               || string.Equals(sourceType, LanguageTrainingSourceTypeIds022Gate3.ActiveImmersion, StringComparison.OrdinalIgnoreCase)
               || string.Equals(sourceType, LanguageTrainingSourceTypeIds022Gate3.TeachingMaterials, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateFromLevel(int fromLevel)
    {
        if (fromLevel < 0 || fromLevel > 4) throw new ArgumentOutOfRangeException(nameof(fromLevel));
    }
}
