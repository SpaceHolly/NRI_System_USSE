using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Nri.Server.Content;
using Nri.Shared.Domain;

namespace Nri.LanguageGate3.Contracts;

internal static class Program
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };

    private static int Main(string[] args)
    {
        var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(root, "obj", "0_22", "gate3_languages"));
        Directory.CreateDirectory(output);
        var all = LanguageGate3SeedCatalog.BuildAll().ToList();
        var languages = all.Where(x => x.Category == WorldLoreCalendarDefinitionCategories.Language).ToList();
        var scripts = all.Where(x => x.Category == WorldLoreCalendarDefinitionCategories.LanguageScript).ToList();
        var families = all.Where(x => x.Category == WorldLoreCalendarDefinitionCategories.LanguageFamily).ToList();
        var traditions = all.Where(x => x.Category == WorldLoreCalendarDefinitionCategories.LanguageOriginTradition).ToList();
        var checks = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["seed.languages53"] = languages.Count == 53,
            ["seed.scripts22"] = scripts.Count == 22,
            ["seed.uniqueStableIds"] = all.GroupBy(x => x.Id, StringComparer.Ordinal).All(x => x.Count() == 1),
            ["seed.ruleSet"] = all.All(x => x.RuleSetId == RuleSetIds.FantasyNriDefault),
            ["seed.packVersioned"] = all.All(x => x.DefinitionPackId == LanguageGate3SeedCatalog.PackId && x.DefinitionPackVersion == LanguageGate3SeedCatalog.PackVersion),
            ["seed.continental7"] = RoleCount(languages, LanguageRoleIds022Gate3.Continental) == 7,
            ["seed.racial9"] = RoleCount(languages, LanguageRoleIds022Gate3.Racial) == 9,
            ["seed.racialHeritageLinks"] = languages.Where(x => Strings(x, "roles").Contains(LanguageRoleIds022Gate3.Racial)).SelectMany(x => Strings(x, "heritageRaceIds")).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "beastfolk", "dragonborn", "dwarf", "elf", "giant", "gnome", "goblin", "halfling", "orc" }),
            ["seed.religious17"] = RoleCount(languages, LanguageRoleIds022Gate3.Religious) == 19,
            ["seed.ancient5"] = RoleCount(languages, LanguageRoleIds022Gate3.Ancient) == 5,
            ["refs.primaryScriptsResolve"] = languages.All(x => scripts.Any(s => s.Id == Field(x, "primaryScript"))),
            ["refs.familiesResolve"] = languages.All(x => string.IsNullOrWhiteSpace(Field(x, "languageFamily")) || families.Any(f => f.Id == Field(x, "languageFamily"))),
            ["refs.ancestorsResolve"] = languages.SelectMany(x => Strings(x, "ancestorLanguages")).All(id => languages.Any(l => l.Id == id)),
            ["refs.contactsResolve"] = languages.SelectMany(x => Strings(x, "contactInfluences")).All(id => languages.Any(l => l.Id == id)),
            ["relations.rashidTaradSeparate"] = Field(One(languages, "lang.state.rashid"), "languageFamily") != Field(One(languages, "lang.state.tarad"), "languageFamily"),
            ["relations.vestarNoForcedFamily"] = string.IsNullOrWhiteSpace(Field(One(languages, "lang.continental.vestar"), "languageFamily")),
            ["relations.teyroNoForcedFamily"] = string.IsNullOrWhiteSpace(Field(One(languages, "lang.continental.teyro"), "languageFamily")),
            ["relations.fuguNoLocalAncestor"] = Strings(One(languages, "lang.culture.fugu"), "ancestorLanguages").Count == 0,
            ["relations.pavenTauraVaenua"] = new[] { "lang.continental.paven", "lang.continental.taura" }.All(id => Strings(One(languages, id), "ancestorLanguages").Contains("lang.ancient.vaenua")),
            ["relations.istalNalpaTlalpa"] = new[] { "lang.continental.istal", "lang.continental.nalpa" }.All(id => Strings(One(languages, id), "ancestorLanguages").Contains("lang.ancient.tlalpa")),
            ["relations.dzhauNadjar"] = Strings(One(languages, "lang.continental.dzhau"), "ancestorLanguages").Contains("lang.ancient.nadjar"),
            ["origin.rashidAndTaradPublic"] = traditions.Count(x => x.VisibilityRule == ContentDefinitionVisibilityRules.Public || x.VisibilityRule == ContentDefinitionVisibilityRules.PlayerVisible) >= 2,
            ["origin.fuguHidden"] = traditions.Any(x => Field(x, "language") == "lang.culture.fugu" && x.VisibilityRule == ContentDefinitionVisibilityRules.GmOnly),
            ["origin.noIsMythTrue"] = traditions.All(x => !x.CustomFields.ContainsKey("isMythTrue") && !x.CustomFields.ContainsKey("IsMythTrue")),
            ["security.fuguTruthOnlyGm"] = One(languages, "lang.culture.fugu").GMDescription.IndexOf("иного мира", StringComparison.CurrentCultureIgnoreCase) >= 0 && One(languages, "lang.culture.fugu").PublicDescription.IndexOf("иного мира", StringComparison.CurrentCultureIgnoreCase) < 0,
            ["limitations.vestarDataDriven"] = Field(One(languages, "lang.continental.vestar"), "usageLimitations").IndexOf("наук", StringComparison.CurrentCultureIgnoreCase) >= 0 && Field(One(languages, "lang.continental.vestar"), "usageLimitations").IndexOf("дипломат", StringComparison.CurrentCultureIgnoreCase) >= 0,
            ["seed.noCityStateLanguages"] = languages.All(x => !new[] { "lichtenburg", "bergenby", "launtown" }.Any(city => x.Id.IndexOf(city, StringComparison.OrdinalIgnoreCase) >= 0)),
            ["levels.allHaveSixDescriptions"] = languages.All(x => Strings(x, "levelDescriptions").Count == 6),
            ["rules.comprehension"] = LanguageTrainingRules022Gate3.ResolveComprehension(3, 3) == LanguageComprehensionResultIds022Gate3.Full && LanguageTrainingRules022Gate3.ResolveComprehension(2, 3) == LanguageComprehensionResultIds022Gate3.Partial && LanguageTrainingRules022Gate3.ResolveComprehension(1, 3) == LanguageComprehensionResultIds022Gate3.Fragments && LanguageTrainingRules022Gate3.ResolveComprehension(0, 3) == LanguageComprehensionResultIds022Gate3.Unavailable,
            ["rules.studyHours"] = Enumerable.Range(0, 5).Select(LanguageTrainingRules022Gate3.RequiredStudyHoursFor).SequenceEqual(new[] { 28, 56, 120, 240, 480 }),
            ["rules.modernMo"] = Enumerable.Range(0, 5).Select(x => LanguageTrainingRules022Gate3.RequiredMoFor(LanguageCostClassIds.Modern, x)).SequenceEqual(new[] { 2, 3, 5, 8, 12 }),
            ["rules.religiousMo"] = Enumerable.Range(0, 5).Select(x => LanguageTrainingRules022Gate3.RequiredMoFor(LanguageCostClassIds.Religious, x)).SequenceEqual(new[] { 3, 5, 8, 12, 18 }),
            ["rules.ancientMo"] = Enumerable.Range(0, 5).Select(x => LanguageTrainingRules022Gate3.RequiredMoFor(LanguageCostClassIds.Ancient, x)).SequenceEqual(new[] { 5, 8, 12, 18, 25 }),
            ["rules.ancientFiveNeedsApproval"] = !LanguageTrainingRules022Gate3.IsSourceSufficient(LanguageCostClassIds.Ancient, 5, LanguageTrainingSourceTypeIds022Gate3.ArchiveResearch, false) && LanguageTrainingRules022Gate3.IsSourceSufficient(LanguageCostClassIds.Ancient, 5, LanguageTrainingSourceTypeIds022Gate3.GmApproved, true),
            ["rules.religiousUpperNeedsCorpus"] = !LanguageTrainingRules022Gate3.IsSourceSufficient(LanguageCostClassIds.Religious, 4, LanguageTrainingSourceTypeIds022Gate3.SelfStudy, false) && LanguageTrainingRules022Gate3.IsSourceSufficient(LanguageCostClassIds.Religious, 4, LanguageTrainingSourceTypeIds022Gate3.ReligiousCorpus, false),
            ["rules.noRelatedLanguageBonusField"] = languages.All(x => !x.CustomFields.Keys.Any(k => k.IndexOf("bonus", StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf("discount", StringComparison.OrdinalIgnoreCase) >= 0))
        };
        var pass = checks.Values.All(x => x);
        Write(Path.Combine(output, "language_gate3_contracts.json"), new { status = pass ? "PASS" : "NOT_PASS", checkCount = checks.Count, checks });
        Write(Path.Combine(output, "language_seed_integrity_audit.json"), new { status = pass ? "PASS" : "NOT_PASS", languageCount = languages.Count, scriptCount = scripts.Count, familyCount = families.Count, originTraditionCount = traditions.Count, unresolvedReferences = checks.Where(x => x.Key.StartsWith("refs.") && !x.Value).Select(x => x.Key).ToArray() });
        Console.WriteLine($"Gate 3 language contracts: {(pass ? "PASS" : "NOT_PASS")} ({checks.Count} checks)");
        return pass ? 0 : 1;
    }

    private static int RoleCount(IEnumerable<ContentDefinitionRecord> records, string role) => records.Count(x => Strings(x, "roles").Contains(role));
    private static ContentDefinitionRecord One(IEnumerable<ContentDefinitionRecord> records, string id) => records.Single(x => x.Id == id);
    private static string Field(ContentDefinitionRecord record, string key) => record.CustomFields.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static List<string> Strings(ContentDefinitionRecord record, string key)
    {
        if (!record.CustomFields.TryGetValue(key, out var value) || value == null) return new List<string>();
        if (value is string text) return string.IsNullOrWhiteSpace(text) ? new List<string>() : new List<string> { text };
        return value is IEnumerable values ? values.Cast<object>().Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList() : new List<string>();
    }
    private static void Write(string path, object value) => File.WriteAllText(path, Json.Serialize(value), new UTF8Encoding(false));
}
