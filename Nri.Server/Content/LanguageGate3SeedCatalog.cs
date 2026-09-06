using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Content;

public static class LanguageGate3SeedCatalog
{
    public const string PackId = "fantasy_nri_default_language_gate3_v1";
    public const string PackVersion = "1.0.0";

    private static readonly string[] LevelDescriptions =
    {
        "0 - язык неизвестен",
        "1 - отдельные слова, заученные фразы и распространённые знаки",
        "2 - простой разговор и базовые тексты",
        "3 - свободное современное владение",
        "4 - сложные документы, формальная и литературная речь",
        "5 - глубокое владение, архаизмы и тонкие оттенки"
    };

    public static IReadOnlyList<ContentDefinitionRecord> BuildAll()
    {
        var scripts = BuildScripts();
        var families = BuildFamilies();
        var languages = BuildLanguages();
        var traditions = BuildTraditions();
        if (languages.Count != 53) throw new InvalidOperationException($"Language Gate 3 seed must contain 53 languages, found {languages.Count}.");
        if (scripts.Count != 22) throw new InvalidOperationException($"Language Gate 3 seed must contain 22 scripts, found {scripts.Count}.");
        return scripts.Concat(families).Concat(languages).Concat(traditions).ToList();
    }

    public static IReadOnlyList<ContentDefinitionRecord> BuildScripts() => new[]
    {
        Script("script.valten", "Вальтен", "Вестар и большинство западных современных и религиозных языков"),
        Script("script.saarif", "Саариф", "Рашидский и Тарадский"),
        Script("script.tensho", "Тэнсё", "Тэйро, Шихуадийский и Цзайрен"),
        Script("script.keimon", "Кэймон", "Кунь-ёмийский и Кэйсо"),
        Script("script.suimo", "Суймо", "Фугу"),
        Script("script.dzhavara", "Джавара", "Джау и Аджара"),
        Script("script.tlakana", "Тлакана", "Исталь, Нальпа, Текаль и Ниальпа"),
        Script("script.vaeru", "Ваэру", "Павен, Таура, Мавен и Атура"),
        Script("script.liariel", "Лиарэль", "Эльфийский"),
        Script("script.kharum", "Кхарум", "Дварфийский"),
        Script("script.grakha", "Гракха", "Орочий"),
        Script("script.shivrik", "Шиврик", "Гоблинский"),
        Script("script.lavel", "Лавель", "Полурослый"),
        Script("script.nimbar", "Нимбар", "Гномий"),
        Script("script.kaira", "Кайра", "Звериный"),
        Script("script.sarkan", "Саркан", "Драконий"),
        Script("script.urdal", "Урдал", "Великанский"),
        Script("script.aernak", "Аэрнак", "Эгнар"),
        Script("script.ryusen", "Рюсэн", "Рюкан"),
        Script("script.maoran", "Маоран", "Ваэнуа"),
        Script("script.tlakan", "Тлакан", "Тлалпа"),
        Script("script.nadzhara", "Наджара", "Наджар")
    };

    public static IReadOnlyList<ContentDefinitionRecord> BuildFamilies() => new[]
    {
        Family("family.northern_egun", "Северная эгунская"),
        Family("family.eastern_egun", "Восточная эгунская"),
        Family("family.kelreno", "Кёльренская"),
        Family("family.rashid", "Рашидская"),
        Family("family.tarad", "Тарадская"),
        Family("family.fimeland", "Файмлэндская"),
        Family("family.egnar", "Эгнарская древняя линия"),
        Family("family.ryukan_shihuadi", "Рюкано-шихуадийская"),
        Family("family.ryukan_kunyomi", "Рюкано-куньёмийская"),
        Family("family.fugu", "Изолированная традиция Фугу", "Истинное внеземное происхождение линии Фугу; игрокам без разрешённого знания не раскрывать."),
        Family("family.vaenua", "Ваэнуаская"),
        Family("family.tlalpa", "Тлалпская"),
        Family("family.nadjar", "Наджарская"),
        Family("family.racial.elven", "Эльфийская"),
        Family("family.racial.dwarven", "Дварфийская"),
        Family("family.racial.orcish", "Орочья"),
        Family("family.racial.goblin", "Гоблинская"),
        Family("family.racial.halfling", "Полурослая"),
        Family("family.racial.gnomish", "Гномья"),
        Family("family.racial.beastfolk", "Звериная"),
        Family("family.racial.draconic", "Драконья"),
        Family("family.racial.giant", "Великанская")
    };

    public static IReadOnlyList<ContentDefinitionRecord> BuildLanguages()
    {
        var result = new List<ContentDefinitionRecord>
        {
            Language("lang.continental.vestar", "Вестар", "script.valten", null, LanguageCostClassIds.Modern, new[] { "continental", "contact" }, "Эгунсентилурра", limitations: "Недостаточен как единственный язык для сложного права, науки, богословия, высокой дипломатии, важных государственных советов и сложного военного планирования."),
            Language("lang.continental.teyro", "Тэйро", "script.tensho", null, LanguageCostClassIds.Modern, new[] { "continental", "contact" }, "Рютэндайти"),
            Language("lang.continental.dzhau", "Джау", "script.dzhavara", "family.nadjar", LanguageCostClassIds.Modern, new[] { "continental", "political_cultural" }, "Танаджау", ancestors: new[] { "lang.ancient.nadjar" }),
            Language("lang.continental.istal", "Исталь", "script.tlakana", "family.tlalpa", LanguageCostClassIds.Modern, new[] { "continental", "political_cultural" }, "Истактлалли", ancestors: new[] { "lang.ancient.tlalpa" }),
            Language("lang.continental.nalpa", "Нальпа", "script.tlakana", "family.tlalpa", LanguageCostClassIds.Modern, new[] { "continental", "political_cultural" }, "Ухунинальпа", ancestors: new[] { "lang.ancient.tlalpa" }),
            Language("lang.continental.paven", "Павен", "script.vaeru", "family.vaenua", LanguageCostClassIds.Modern, new[] { "continental", "political_cultural" }, "Мотупавенуа", ancestors: new[] { "lang.ancient.vaenua" }),
            Language("lang.continental.taura", "Таура", "script.vaeru", "family.vaenua", LanguageCostClassIds.Modern, new[] { "continental", "political_cultural" }, "Фенуатаура", ancestors: new[] { "lang.ancient.vaenua" })
        };

        result.AddRange(new[]
        {
            Language("lang.state.lutwein", "Лютвейнский", "script.valten", "family.northern_egun", LanguageCostClassIds.Modern, new[] { "state" }, "Лютвейн"),
            Language("lang.state.ostfront", "Остфронтский", "script.valten", "family.eastern_egun", LanguageCostClassIds.Modern, new[] { "state" }, "Остфронт"),
            Language("lang.state.kelreno", "Кёльренский", "script.valten", "family.kelreno", LanguageCostClassIds.Modern, new[] { "state" }, "Кёльрено"),
            Language("lang.state.rashid", "Рашидский", "script.saarif", "family.rashid", LanguageCostClassIds.Modern, new[] { "state", "religious" }, "Рашид-Аль-Тара"),
            Language("lang.state.tarad", "Тарадский", "script.saarif", "family.tarad", LanguageCostClassIds.Modern, new[] { "state", "religious" }, "Рашид-Аль-Тара"),
            Language("lang.state.gronnenland", "Грённенландский", "script.valten", "family.northern_egun", LanguageCostClassIds.Modern, new[] { "state" }, "Грённенланд"),
            Language("lang.state.runavania", "Рунаванийский", "script.valten", "family.northern_egun", LanguageCostClassIds.Modern, new[] { "state" }, "Рунавания"),
            Language("lang.state.darnwein", "Дарнвейнский", "script.valten", "family.northern_egun", LanguageCostClassIds.Modern, new[] { "state" }, "Дарнвейн"),
            Language("lang.state.kolymin", "Колыминьский", "script.valten", "family.eastern_egun", LanguageCostClassIds.Modern, new[] { "state" }, "Колыминь"),
            Language("lang.state.zania", "Занийский", "script.valten", "family.kelreno", LanguageCostClassIds.Modern, new[] { "state" }, "Зания"),
            Language("lang.state.fimeland", "Файмлэндский", "script.valten", "family.fimeland", LanguageCostClassIds.Modern, new[] { "state" }, "Файмлэнд"),
            Language("lang.state.doltaran", "Долтаранский", "script.valten", "family.eastern_egun", LanguageCostClassIds.Modern, new[] { "state" }, "Долтаран"),
            Language("lang.state.shihuadi", "Шихуадийский", "script.tensho", "family.ryukan_shihuadi", LanguageCostClassIds.Modern, new[] { "state", "political_cultural" }, "Империя Шихуади", ancestors: new[] { "lang.ancient.ryukan" }),
            Language("lang.state.kunyomi", "Кунь-ёмийский", "script.keimon", "family.ryukan_kunyomi", LanguageCostClassIds.Modern, new[] { "state", "political_cultural" }, "Конфедерация Кунь-Ёми", ancestors: new[] { "lang.ancient.ryukan" }),
            Language("lang.culture.fugu", "Фугу", "script.suimo", "family.fugu", LanguageCostClassIds.Modern, new[] { "political_cultural" }, "Скрытые островные и прибрежные общины", gmTruth: "Фугу являются пришельцами из иного мира; у языка нет местных генетических родственников или местного предка.")
        });

        var racial = new[]
        {
            ("elven", "Эльфийский", "liariel", "elf"), ("dwarven", "Дварфийский", "kharum", "dwarf"),
            ("orcish", "Орочий", "grakha", "orc"), ("goblin", "Гоблинский", "shivrik", "goblin"),
            ("halfling", "Полурослый", "lavel", "halfling"), ("gnomish", "Гномий", "nimbar", "gnome"),
            ("beastfolk", "Звериный", "kaira", "beastfolk"), ("draconic", "Драконий", "sarkan", "dragonborn"),
            ("giant", "Великанский", "urdal", "giant")
        };
        result.AddRange(racial.Select(x => Language($"lang.race.{x.Item1}", x.Item2, $"script.{x.Item3}", $"family.racial.{x.Item1}", LanguageCostClassIds.Modern, new[] { "racial" }, "Наследие и культура", heritageRaceIds: new[] { x.Item4 })));

        var religious = new[]
        {
            ("hairen", "Хайрен", "valten", "family.northern_egun", "Лютвейн"),
            ("velar", "Велар", "valten", "family.eastern_egun", "Остфронт"),
            ("kellan", "Келлан", "valten", "family.kelreno", "Кёльрено"),
            ("eidal", "Эйдаль", "valten", "family.northern_egun", "Грённенланд"),
            ("runar", "Рунар", "valten", "family.northern_egun", "Рунавания"),
            ("tarnek", "Тарнек", "valten", "family.northern_egun", "Дарнвейн"),
            ("kolvar", "Колвар", "valten", "family.eastern_egun", "Колыминь"),
            ("zakhir", "Захир", "valten", "family.kelreno", "Зания"),
            ("marin", "Мэрин", "valten", "family.fimeland", "Файмлэнд"),
            ("valta", "Вальта", "valten", "family.eastern_egun", "Долтаран"),
            ("tsairen", "Цзайрен", "tensho", "family.ryukan_shihuadi", "Шихуади"),
            ("keiso", "Кэйсо", "keimon", "family.ryukan_kunyomi", "Кунь-Ёми"),
            ("adjara", "Аджара", "dzhavara", "family.nadjar", "Танаджау"),
            ("tekal", "Текаль", "tlakana", "family.tlalpa", "Истактлалли"),
            ("nialpa", "Ниальпа", "tlakana", "family.tlalpa", "Ухунинальпа"),
            ("maven", "Мавен", "vaeru", "family.vaenua", "Мотупавенуа"),
            ("atura", "Атура", "vaeru", "family.vaenua", "Фенуатаура")
        };
        result.AddRange(religious.Select(x => Language($"lang.religious.{x.Item1}", x.Item2, $"script.{x.Item3}", x.Item4, LanguageCostClassIds.Religious, new[] { "religious" }, x.Item5)));

        result.AddRange(new[]
        {
            Language("lang.ancient.egnar", "Эгнар", "script.aernak", "family.egnar", LanguageCostClassIds.Ancient, new[] { "ancient" }, "Эгунсентилурра"),
            Language("lang.ancient.ryukan", "Рюкан", "script.ryusen", null, LanguageCostClassIds.Ancient, new[] { "ancient" }, "Рютэндайти"),
            Language("lang.ancient.vaenua", "Ваэнуа", "script.maoran", "family.vaenua", LanguageCostClassIds.Ancient, new[] { "ancient" }, "Оба нейтральных континента"),
            Language("lang.ancient.tlalpa", "Тлалпа", "script.tlakan", "family.tlalpa", LanguageCostClassIds.Ancient, new[] { "ancient" }, "Оба полярных континента"),
            Language("lang.ancient.nadjar", "Наджар", "script.nadzhara", "family.nadjar", LanguageCostClassIds.Ancient, new[] { "ancient" }, "Танаджау")
        });
        return result;
    }

    public static IReadOnlyList<ContentDefinitionRecord> BuildTraditions() => new[]
    {
        Tradition("tradition.rashid.desert_gods", "Дар богов пустынь", "lang.state.rashid", "deity", "Боги пустынь", "Боги пустынь даровали язык прежде всего как путь искупления, а не наказание."),
        Tradition("tradition.tarad.snake_speech", "Речь песчаных и речных змей", "lang.state.tarad", "hero", "Герои древности", "Герои древности научились речи у песчаных и речных змей и передали её потомкам."),
        Tradition("tradition.fugu.hidden_origin", "Скрытое происхождение Фугу", "lang.culture.fugu", "unknown", string.Empty, string.Empty, "Фугу происходят из иного мира. Эти сведения не являются общедоступным преданием.", ContentDefinitionVisibilityRules.GmOnly)
    };

    private static ContentDefinitionRecord Script(string id, string name, string mainUse) => Record(id, WorldLoreCalendarDefinitionCategories.LanguageScript, name,
        "Письменность мира.", new Dictionary<string, object> { ["mainUse"] = mainUse, ["writingDirection"] = "left_to_right" });

    private static ContentDefinitionRecord Family(string id, string name, string gmDescription = "") => Record(id, WorldLoreCalendarDefinitionCategories.LanguageFamily, name,
        "Историко-лингвистическая связь; сама по себе не даёт владения или бонусов.", new Dictionary<string, object>(), gmDescription);

    private static ContentDefinitionRecord Language(string id, string name, string scriptId, string? familyId, string costClass,
        IEnumerable<string> roles, string association, IEnumerable<string>? ancestors = null, IEnumerable<string>? influences = null,
        string limitations = "", string gmTruth = "", IEnumerable<string>? heritageRaceIds = null)
    {
        var fields = new Dictionary<string, object>
        {
            ["roles"] = roles.ToArray(), ["primaryScript"] = scriptId, ["costClass"] = costClass,
            ["cultures"] = new[] { association }, ["levelDescriptions"] = LevelDescriptions,
            ["ancestorLanguages"] = (ancestors ?? Array.Empty<string>()).ToArray(),
            ["contactInfluences"] = (influences ?? Array.Empty<string>()).ToArray(),
            ["heritageRaceIds"] = (heritageRaceIds ?? Array.Empty<string>()).ToArray(),
            ["usageLimitations"] = limitations
        };
        if (!string.IsNullOrWhiteSpace(familyId)) fields["languageFamily"] = familyId;
        if (!string.IsNullOrWhiteSpace(gmTruth)) fields["gmTruth"] = gmTruth;
        return Record(id, WorldLoreCalendarDefinitionCategories.Language, name,
            $"{name}: язык мира fantasy_nri_default.", fields, gmTruth,
            new[] { scriptId }.Concat(string.IsNullOrWhiteSpace(familyId) ? Array.Empty<string>() : new[] { familyId! }).Concat(ancestors ?? Array.Empty<string>()));
    }

    private static ContentDefinitionRecord Tradition(string id, string name, string languageId, string originType, string giver,
        string publicDescription, string gmDescription = "", string visibility = ContentDefinitionVisibilityRules.PlayerVisible)
        => Record(id, WorldLoreCalendarDefinitionCategories.LanguageOriginTradition, name, publicDescription,
            new Dictionary<string, object> { ["language"] = languageId, ["claimedOriginType"] = originType, ["claimedGiverName"] = giver },
            gmDescription, new[] { languageId }, visibility);

    private static ContentDefinitionRecord Record(string id, string category, string name, string publicDescription,
        Dictionary<string, object> fields, string gmDescription = "", IEnumerable<string>? references = null,
        string visibility = ContentDefinitionVisibilityRules.PlayerVisible)
        => new ContentDefinitionRecord
        {
            Id = id,
            RuleSetId = RuleSetIds.FantasyNriDefault,
            Category = category,
            DefinitionType = category,
            Name = name,
            DisplayName = name,
            ShortCode = id,
            PublicDescription = publicDescription,
            GMDescription = gmDescription,
            VisibilityRule = visibility,
            AllowedRuleSetIds = new List<string> { RuleSetIds.FantasyNriDefault },
            Tags = new List<string> { "canonical", "foundation_0_22_gate3", category },
            CustomFields = fields,
            ReferenceIds = (references ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToList(),
            ContentStatus = "published",
            DefinitionPackId = PackId,
            DefinitionPackVersion = PackVersion,
            StableKey = id,
            RecordVersion = PackVersion,
            Revision = 1,
            SchemaVersion = 3,
            IsArchived = false
        };
}
