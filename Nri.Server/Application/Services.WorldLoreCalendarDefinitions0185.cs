using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private static List<DefinitionEditorProfile> BuildWorldLoreCalendarDefinitionEditorProfiles0185()
    {
        var profiles = new List<DefinitionEditorProfile>
        {
            Profile0181("world_definition_profile_0185", WorldLoreCalendarDefinitionCategories.World, "Миры",
                "Authored world metadata. Current campaign time remains runtime state.", new[]
                {
                    Field0181("defaultCalendar", "Календарь по умолчанию", ContentDefinitionFieldTypes.Reference, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Calendar),
                    Field0181("defaultEra", "Эпоха по умолчанию", ContentDefinitionFieldTypes.Reference, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Era),
                    Field0181("topLevelLocations", "Верхнеуровневые локации", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("defaultLanguages", "Основные языки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Language),
                    Field0181("themes", "Темы мира", ContentDefinitionFieldTypes.Tags, false)
                }),
            Profile0181("location_definition_profile_0185", WorldLoreCalendarDefinitionCategories.Location, "Локации",
                "Authored location hierarchy. Map coordinates remain in map and marker storage.", new[]
                {
                    Field0181("locationKind", "Вид локации", ContentDefinitionFieldTypes.Enum, true, new[] { "world", "continent", "region", "state", "settlement", "district", "location", "sub_location", "custom" }),
                    Field0181("world", "Мир", ContentDefinitionFieldTypes.Reference, true, referenceCategory: WorldLoreCalendarDefinitionCategories.World),
                    Field0181("parentLocation", "Родительская локация", ContentDefinitionFieldTypes.Reference, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("languages", "Языки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Language),
                    Field0181("cultures", "Культуры", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("climateTerrain", "Климат и местность", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("travelMetadata", "Доступ и путешествие", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("relatedMap", "Связанная карта", ContentDefinitionFieldTypes.String, false),
                    Field0181("knownConnections", "Известные связи", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("hiddenConnections", "Скрытые связи", ContentDefinitionFieldTypes.ReferenceList, false, isPlayerVisible: false, isGmOnly: true, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("jurisdiction", "Юрисдикция", ContentDefinitionFieldTypes.String, false),
                    Field0181("allowCustomHierarchy", "Разрешить нестандартную иерархию", ContentDefinitionFieldTypes.Boolean, false, isPlayerVisible: false, isGmOnly: true)
                }),
            Profile0181("language_definition_profile_0185", WorldLoreCalendarDefinitionCategories.Language, "Языки",
                "Authored languages and writing systems; character proficiency remains character-scoped runtime.", new[]
                {
                    Field0181("roles", "Роли языка", ContentDefinitionFieldTypes.Tags, true),
                    Field0181("languageFamily", "Языковая семья", ContentDefinitionFieldTypes.Reference, false, referenceCategory: WorldLoreCalendarDefinitionCategories.LanguageFamily),
                    Field0181("primaryScript", "Основная письменность", ContentDefinitionFieldTypes.Reference, true, referenceCategory: WorldLoreCalendarDefinitionCategories.LanguageScript),
                    Field0181("regions", "Регионы распространения", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("cultures", "Культуры", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("stateOrganizations", "Государства и организации", ContentDefinitionFieldTypes.ReferenceList, false),
                    Field0181("ancestorLanguages", "Языки-предки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Language),
                    Field0181("contactInfluences", "Контактные влияния", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Language),
                    Field0181("heritageRaceIds", "Культурное наследие рас", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "race_definition"),
                    Field0181("costClass", "Класс обучения", ContentDefinitionFieldTypes.Enum, true, new[] { "modern", "religious", "ancient" }),
                    Field0181("levelDescriptions", "Описание уровней 0-5", ContentDefinitionFieldTypes.LongText, true),
                    Field0181("professionalTerminology", "Профессиональная терминология", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("ritualMagicApplication", "Ритуальное и магическое применение", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("translationRules", "Правила перевода", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("usageLimitations", "Ограничения применения", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("gmTruth", "Скрытые сведения GM", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
                }),
            Profile0181("language_script_definition_profile_022_gate3", WorldLoreCalendarDefinitionCategories.LanguageScript, "Письменности",
                "Системы письма являются справочными данными и не создают отдельный навык персонажа.", new[]
                {
                    Field0181("mainUse", "Основное применение", ContentDefinitionFieldTypes.LongText, true),
                    Field0181("writingDirection", "Направление письма", ContentDefinitionFieldTypes.Enum, true, new[] { "left_to_right", "right_to_left", "vertical", "custom" }),
                    Field0181("gmNotes", "Заметки GM", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
                }),
            Profile0181("language_family_definition_profile_022_gate3", WorldLoreCalendarDefinitionCategories.LanguageFamily, "Языковые семьи",
                "Историческая связь языков без автоматических бонусов владения.", new[]
                {
                    Field0181("parentFamily", "Родительская семья", ContentDefinitionFieldTypes.Reference, false, referenceCategory: WorldLoreCalendarDefinitionCategories.LanguageFamily),
                    Field0181("gmNotes", "Заметки GM", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
                }),
            Profile0181("language_origin_tradition_profile_022_gate3", WorldLoreCalendarDefinitionCategories.LanguageOriginTradition, "Предания о происхождении языков",
                "Культурное предание является утверждением традиции, а не подтверждённой истиной мира.", new[]
                {
                    Field0181("language", "Язык", ContentDefinitionFieldTypes.Reference, true, referenceCategory: WorldLoreCalendarDefinitionCategories.Language),
                    Field0181("associations", "Культуры, государства и религии", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("claimedOriginType", "Заявленный источник", ContentDefinitionFieldTypes.Enum, true, new[] { "deity", "hero", "dragon", "titan", "leviathan", "spirit", "mythic_creature", "unknown", "other" }),
                    Field0181("claimedGiverName", "Имя дарителя или героя", ContentDefinitionFieldTypes.String, false),
                    Field0181("gmTruth", "Скрытый контекст GM", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
                }),
            Profile0181("knowledge_type_definition_profile_0185", WorldLoreCalendarDefinitionCategories.KnowledgeType, "Типы знаний",
                "Extensible authored knowledge levels. Character knowledge remains runtime.", new[]
                {
                    Field0181("order", "Порядок", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10000),
                    Field0181("reliabilityMinimum", "Минимальная достоверность", ContentDefinitionFieldTypes.Decimal, true, min: 0, max: 100),
                    Field0181("reliabilityMaximum", "Максимальная достоверность", ContentDefinitionFieldTypes.Decimal, true, min: 0, max: 100),
                    Field0181("allowsPracticalUse", "Разрешает практическое применение", ContentDefinitionFieldTypes.Boolean, false),
                    Field0181("allowsIdentification", "Разрешает распознавание", ContentDefinitionFieldTypes.Boolean, false),
                    Field0181("allowsDetails", "Разрешает подробности", ContentDefinitionFieldTypes.Boolean, false),
                    Field0181("playerLabel", "Название для игрока", ContentDefinitionFieldTypes.String, true)
                }),
            Profile0181("lore_entry_definition_profile_0185", WorldLoreCalendarDefinitionCategories.LoreEntry, "Знания о мире",
                "Authored lore with explicitly visible and hidden information versions.", new[]
                {
                    Field0181("loreKind", "Вид материала", ContentDefinitionFieldTypes.Enum, true, new[] { "lore", "rumor", "document", "doctrine", "method", "historical_account", "custom" }),
                    Field0181("subjectType", "Вид предмета знания", ContentDefinitionFieldTypes.Enum, true, new[] { "world", "location", "language", "era", "event_type", "person", "faction", "organization", "custom" }),
                    Field0181("subject", "Предмет знания", ContentDefinitionFieldTypes.Reference, false),
                    Field0181("sources", "Источники", ContentDefinitionFieldTypes.ReferenceList, false),
                    Field0181("locations", "Связанные локации", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("languages", "Связанные языки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Language),
                    Field0181("eras", "Связанные эпохи", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Era),
                    Field0181("eventTypes", "Связанные типы событий", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.EventType),
                    LoreVersionsField0185()
                }),
            Profile0181("calendar_definition_profile_0185", WorldLoreCalendarDefinitionCategories.Calendar, "Календари",
                "Authored calendar structure. The current world date remains runtime state.", new[]
                {
                    Field0181("yearNumberingModel", "Система нумерации лет", ContentDefinitionFieldTypes.Enum, true, new[] { "era_based", "continuous", "custom" }),
                    Field0181("daysPerWeek", "Дней в неделе", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 100),
                    StructuredField0185("weekdays", "Дни недели", "Одна строка: порядок | название."),
                    StructuredField0185("months", "Месяцы", "Одна строка: порядок | название | дней | сезон."),
                    StructuredField0185("seasons", "Сезоны", "Одна строка: порядок | название | первый день года | последний день года."),
                    StructuredField0185("specialDays", "Особые дни", "Одна строка: название | после месяца | количество дней."),
                    Field0181("declaredDaysPerYear", "Заявлено дней в году", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 100000),
                    Field0181("defaultEra", "Эпоха по умолчанию", ContentDefinitionFieldTypes.Reference, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Era),
                    Field0181("dateDisplayFormat", "Формат отображения даты", ContentDefinitionFieldTypes.String, true)
                }),
            Profile0181("era_definition_profile_0185", WorldLoreCalendarDefinitionCategories.Era, "Эпохи",
                "Authored era structure; it does not store the campaign's current date.", new[]
                {
                    Field0181("calendar", "Календарь", ContentDefinitionFieldTypes.Reference, true, referenceCategory: WorldLoreCalendarDefinitionCategories.Calendar),
                    Field0181("yearZeroPolicy", "Правило нулевого года", ContentDefinitionFieldTypes.Enum, true, new[] { "has_year_zero", "no_year_zero", "custom" }),
                    Field0181("countingDirection", "Направление счёта", ContentDefinitionFieldTypes.Enum, true, new[] { "forward", "backward", "custom" }),
                    Field0181("startBoundary", "Начало эпохи", ContentDefinitionFieldTypes.String, true),
                    Field0181("endBoundary", "Конец эпохи", ContentDefinitionFieldTypes.String, false),
                    Field0181("displayPrefix", "Префикс даты", ContentDefinitionFieldTypes.String, false),
                    Field0181("displaySuffix", "Суффикс даты", ContentDefinitionFieldTypes.String, false),
                    Field0181("historicalTags", "Исторические теги", ContentDefinitionFieldTypes.Tags, false)
                }),
            Profile0181("event_type_definition_profile_0185", WorldLoreCalendarDefinitionCategories.EventType, "Типы событий",
                "Reusable event types for Chronicle and Future Event runtime instances.", new[]
                {
                    Field0181("eventCategory", "Категория события", ContentDefinitionFieldTypes.String, true),
                    Field0181("defaultSeverity", "Тяжесть по умолчанию", ContentDefinitionFieldTypes.Enum, true, new[] { "minor", "normal", "major", "critical", "custom" }),
                    Field0181("defaultVisibility", "Видимость по умолчанию", ContentDefinitionFieldTypes.VisibilityRule, true),
                    Field0181("allowedVersionKinds", "Разрешённые версии", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("iconKey", "Смысловой значок", ContentDefinitionFieldTypes.String, false),
                    Field0181("applicableLocationKinds", "Применимые виды локаций", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("applicableSubjectKinds", "Применимые виды объектов", ContentDefinitionFieldTypes.Tags, false)
                })
        };

        foreach (var profile in profiles)
        {
            profile.SchemaVersion = 3;
            profile.DefaultTags = profile.DefaultTags
                .Concat(new[] { "foundation_0_18_5", "world_lore_calendar", "foundation_0_22_gate3" })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            profile.ValidationRules.Add("world-lore-calendar-typed-validation");
            foreach (var field in profile.FieldSchemas)
            {
                if (string.IsNullOrWhiteSpace(field.HelpText))
                    field.HelpText = field.IsRequired ? "Обязательное поле." : "Поле можно оставить пустым.";
            }
        }

        return profiles;
    }

    private static DefinitionFieldSchema StructuredField0185(string name, string label, string help)
    {
        var field = Field0181(name, label, ContentDefinitionFieldTypes.LongText, false);
        field.EditorKind = "multiline_text";
        field.HelpText = help + " Это структурированный список, не JSON.";
        field.SectionTitle = "Структурированные данные";
        return field;
    }

    private static DefinitionFieldSchema LoreVersionsField0185()
    {
        var field = StructuredField0185("informationVersions", "Версии информации",
            "Одна строка: вид | тип знания | текст | достоверность 0–100 | источник | от | до | устарело да/нет | игрокам да/нет.");
        field.IsPlayerVisible = false;
        field.IsGmOnly = true;
        field.SectionTitle = "Версии информации";
        return field;
    }

    private void ApplyWorldLoreCalendarDefinitionValidation0185(
        ContentDefinitionRecord record,
        DefinitionEditorProfile profile,
        ContentDefinitionValidationResult result)
    {
        if (!WorldLoreCalendarDefinitionCategories.IsSupported(record.Category)) return;

        if (string.Equals(record.Category, WorldLoreCalendarDefinitionCategories.Location, StringComparison.OrdinalIgnoreCase))
            ValidateLocation0185(record, result);
        else if (string.Equals(record.Category, WorldLoreCalendarDefinitionCategories.KnowledgeType, StringComparison.OrdinalIgnoreCase))
            ValidateKnowledgeType0185(record, result);
        else if (string.Equals(record.Category, WorldLoreCalendarDefinitionCategories.LoreEntry, StringComparison.OrdinalIgnoreCase))
            ValidateLore0185(record, result);
        else if (string.Equals(record.Category, WorldLoreCalendarDefinitionCategories.Calendar, StringComparison.OrdinalIgnoreCase))
            ValidateCalendar0185(record, result);
        else if (string.Equals(record.Category, WorldLoreCalendarDefinitionCategories.Era, StringComparison.OrdinalIgnoreCase))
            ValidateEra0185(record, result);
    }

    private void EnsureWorldLoreCalendarDefinitionCanPersist0185(
        ContentDefinitionRecord record,
        DefinitionEditorProfile profile)
    {
        if (!WorldLoreCalendarDefinitionCategories.IsSupported(record.Category)) return;

        var validation = ValidateContentDefinition0181(record, profile);
        var archivedParentWarning = validation.Warnings.Any(x =>
            x.IndexOf("Родительская локация находится в архиве", StringComparison.OrdinalIgnoreCase) >= 0);
        if (validation.Errors.Count == 0 && !archivedParentWarning) return;

        throw new ArgumentException(
            "Запись не сохранена: "
            + string.Join(" ", validation.Errors
                .Concat(archivedParentWarning ? new[] { "Нельзя выбрать архивную родительскую локацию." } : Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    private void ValidateLocation0185(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var worldId = Field0185(record, "world");
        var parentId = Field0185(record, "parentLocation");
        var kind = Field0185(record, "locationKind");
        var world = FindContent0185(worldId);
        if (world == null)
            result.Errors.Add("Выбранный мир не найден.");
        else if (world.IsArchived)
            result.Errors.Add("Нельзя привязать локацию к архивному миру.");
        else if (!string.Equals(world.Category, WorldLoreCalendarDefinitionCategories.World, StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Поле «Мир» должно ссылаться на определение мира.");
        if (string.Equals(record.Id, parentId, StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Локация не может быть родителем самой себе.");
        if (string.IsNullOrWhiteSpace(parentId)) return;

        var parent = FindContent0185(parentId);
        if (parent == null)
        {
            result.BrokenReferences.Add(parentId);
            result.Errors.Add("Родительская локация не найдена.");
            return;
        }
        if (parent.IsArchived) result.Warnings.Add("Родительская локация находится в архиве.");
        if (!string.Equals(parent.Category, WorldLoreCalendarDefinitionCategories.Location, StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Родителем может быть только локация.");
        if (!string.Equals(worldId, Field0185(parent, "world"), StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Локация и её родитель должны принадлежать одному миру.");
        if (LocationCycle0185(record.Id, parentId))
            result.Errors.Add("Обнаружен цикл в иерархии локаций.");
        if (!FieldBool0185(record, "allowCustomHierarchy") && !LogicalParent0185(kind, Field0185(parent, "locationKind")))
            result.Errors.Add("Выбранный вид родителя не соответствует стандартной иерархии. Включите нестандартную иерархию только если это разрешено правилами мира.");
    }

    private void ValidateKnowledgeType0185(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var min = FieldDecimal0185(record, "reliabilityMinimum");
        var max = FieldDecimal0185(record, "reliabilityMaximum");
        if (min.HasValue && max.HasValue && min.Value > max.Value)
            result.Errors.Add("Минимальная достоверность не может превышать максимальную.");
    }

    private void ValidateLore0185(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var rows = ParseRows0185(Field0185(record, "informationVersions"));
        if (rows.Count == 0)
        {
            result.Errors.Add("Добавьте хотя бы одну версию информации.");
            return;
        }
        foreach (var row in rows)
        {
            if (row.Length < 9)
            {
                result.Errors.Add("Каждая версия информации должна содержать девять именованных значений.");
                continue;
            }
            if (!decimal.TryParse(row[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var reliability)
                && !decimal.TryParse(row[3], out reliability))
                result.Errors.Add("Достоверность версии должна быть числом.");
            else if (reliability < 0 || reliability > 100)
                result.Errors.Add("Достоверность версии должна быть от 0 до 100.");
            if (string.IsNullOrWhiteSpace(row[2]))
                result.Errors.Add("Текст версии информации обязателен.");
        }
    }

    private void ValidateCalendar0185(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var weekdays = ParseRows0185(Field0185(record, "weekdays"));
        var months = ParseRows0185(Field0185(record, "months"));
        var seasons = ParseRows0185(Field0185(record, "seasons"));
        var specialDays = ParseRows0185(Field0185(record, "specialDays"));
        ValidateOrderedRows0185(weekdays, "дней недели", 2, result);
        ValidateOrderedRows0185(months, "месяцев", 4, result);
        ValidateOrderedRows0185(seasons, "сезонов", 4, result);

        var calculatedDays = 0;
        foreach (var row in months)
        {
            if (row.Length < 4 || !int.TryParse(row[2], out var days) || days <= 0)
                result.Errors.Add("Количество дней каждого месяца должно быть положительным числом.");
            else calculatedDays += days;
        }
        foreach (var row in specialDays)
        {
            if (row.Length < 3 || !int.TryParse(row[2], out var days) || days <= 0)
                result.Errors.Add("Количество особых дней должно быть положительным числом.");
            else calculatedDays += days;
        }
        var declared = FieldInt0185(record, "declaredDaysPerYear");
        if (declared.HasValue && calculatedDays > 0 && declared.Value != calculatedDays)
            result.Warnings.Add($"Заявленная длина года ({declared.Value}) отличается от вычисленной ({calculatedDays}).");
        if (weekdays.Count != (FieldInt0185(record, "daysPerWeek") ?? 0))
            result.Warnings.Add("Количество записей дней недели отличается от значения «Дней в неделе».");

        var yearLength = Math.Max(calculatedDays, declared ?? 0);
        foreach (var row in seasons)
        {
            if (row.Length < 4 || !int.TryParse(row[2], out var start) || !int.TryParse(row[3], out var end)
                || start <= 0 || end < start || (yearLength > 0 && end > yearLength))
                result.Errors.Add("Границы сезона должны находиться внутри года и идти по возрастанию.");
        }
        var format = Field0185(record, "dateDisplayFormat");
        if (format.IndexOf("Id", StringComparison.OrdinalIgnoreCase) >= 0
            || format.IndexOf("Key", StringComparison.OrdinalIgnoreCase) >= 0)
            result.Errors.Add("Формат даты не должен показывать внутренние ключи.");
    }

    private void ValidateEra0185(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var calendarId = Field0185(record, "calendar");
        var calendar = FindContent0185(calendarId);
        if (calendar == null)
        {
            result.BrokenReferences.Add(calendarId);
            result.Errors.Add("Выбранный календарь не найден.");
        }
        else if (calendar.IsArchived)
            result.Errors.Add("Нельзя привязать эпоху к архивному календарю.");
        else if (!string.Equals(calendar.Category, WorldLoreCalendarDefinitionCategories.Calendar, StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Эпоха должна ссылаться на календарь.");
    }

    public ResponseEnvelope WorldLoreCalendarPlayerList0185(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var records = _mongo.ContentDefinitionRecords.Find(
                Builders<ContentDefinitionRecord>.Filter.In(x => x.Category, WorldLoreCalendarDefinitionCategories.All)
                & Builders<ContentDefinitionRecord>.Filter.Ne(x => x.IsArchived, true))
            .ToList()
            .Where(IsDefinitionPlayerVisible0181)
            .OrderBy(x => Array.IndexOf(WorldLoreCalendarDefinitionCategories.All, x.Category))
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var lookup = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Empty)
            .ToList()
            .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
        var items = records.Select(x => (object)WorldLore0185PlayerPayload(x, lookup)).ToArray();
        return Ok("Справочник мира, языков и знаний загружен.", new Dictionary<string, object>
        {
            ["items"] = items,
            ["count"] = items.Length,
            ["familyLabel"] = "Мир, языки и знания",
            ["playerSafe"] = true
        });
    }

    public ResponseEnvelope WorldLoreCalendarPlayerGet0185(CommandContext context)
    {
        GetCurrentAccount(context);
        var id = RequireDefinitionId0181(context.Request.Payload);
        var record = GetContentDefinitionRecord0181(id);
        if (!WorldLoreCalendarDefinitionCategories.IsSupported(record.Category)
            || record.IsArchived
            || !IsDefinitionPlayerVisible0181(record))
            throw new KeyNotFoundException("Открытая игрокам запись не найдена.");
        var lookup = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Empty)
            .ToList()
            .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
        return Ok("Запись открыта.", new Dictionary<string, object>
        {
            ["definition"] = WorldLore0185PlayerPayload(record, lookup)
        });
    }

    private Dictionary<string, object> WorldLore0185PlayerPayload(
        ContentDefinitionRecord record,
        IReadOnlyDictionary<string, ContentDefinitionRecord> lookup)
    {
        var facts = new List<object>();
        void Add(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                facts.Add(new Dictionary<string, object> { ["label"] = label, ["value"] = value });
        }
        void AddRefs(string label, string field)
        {
            var names = SplitRefs0181(Field0185(record, field))
                .Select(id => lookup.TryGetValue(id, out var target) && IsDefinitionPlayerVisible0181(target) ? target.DisplayName : string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names.Length > 0) Add(label, string.Join(", ", names));
        }

        switch (record.Category)
        {
            case WorldLoreCalendarDefinitionCategories.World:
                AddRefs("Календарь", "defaultCalendar");
                AddRefs("Эпоха", "defaultEra");
                AddRefs("Главные места", "topLevelLocations");
                AddRefs("Основные языки", "defaultLanguages");
                Add("Темы", Field0185(record, "themes"));
                break;
            case WorldLoreCalendarDefinitionCategories.Location:
                Add("Вид места", LocalizeLocationKind0185(Field0185(record, "locationKind")));
                AddRefs("Мир", "world");
                AddRefs("Родительское место", "parentLocation");
                AddRefs("Языки", "languages");
                Add("Культуры", Field0185(record, "cultures"));
                Add("Климат и местность", Field0185(record, "climateTerrain"));
                Add("Путешествие", Field0185(record, "travelMetadata"));
                AddRefs("Известные связи", "knownConnections");
                break;
            case WorldLoreCalendarDefinitionCategories.Language:
                Add("Языковая семья", Field0185(record, "languageFamily"));
                Add("Системы письма", ReadableRows0185(Field0185(record, "writingSystems")));
                AddRefs("Регионы", "regions");
                Add("Культуры", Field0185(record, "cultures"));
                Add("Аспекты владения", Field0185(record, "proficiencyAspects"));
                Add("Терминология", Field0185(record, "professionalTerminology"));
                Add("Перевод", Field0185(record, "translationRules"));
                AddRefs("Родственные языки", "relatedLanguages");
                break;
            case WorldLoreCalendarDefinitionCategories.KnowledgeType:
                Add("Название для игрока", Field0185(record, "playerLabel"));
                Add("Практическое применение", YesNo0185(FieldBool0185(record, "allowsPracticalUse")));
                Add("Распознавание", YesNo0185(FieldBool0185(record, "allowsIdentification")));
                Add("Подробности", YesNo0185(FieldBool0185(record, "allowsDetails")));
                break;
            case WorldLoreCalendarDefinitionCategories.LoreEntry:
                Add("Вид материала", LocalizeLoreKind0185(Field0185(record, "loreKind")));
                AddRefs("Места", "locations");
                AddRefs("Языки", "languages");
                AddRefs("Эпохи", "eras");
                AddRefs("Типы событий", "eventTypes");
                foreach (var row in ParseRows0185(Field0185(record, "informationVersions")))
                {
                    if (row.Length < 9 || !Truthy0185(row[8]) || IsHiddenLoreKind0185(row[0])) continue;
                    Add(LocalizeLoreVersion0185(row[0]), row[2]);
                    if (!string.IsNullOrWhiteSpace(row[4])) Add("Источник", row[4]);
                }
                break;
            case WorldLoreCalendarDefinitionCategories.Calendar:
                Add("Система лет", LocalizeYearModel0185(Field0185(record, "yearNumberingModel")));
                Add("Дни недели", ReadableRows0185(Field0185(record, "weekdays")));
                Add("Месяцы", ReadableRows0185(Field0185(record, "months")));
                Add("Сезоны", ReadableRows0185(Field0185(record, "seasons")));
                Add("Особые дни", ReadableRows0185(Field0185(record, "specialDays")));
                Add("Дней в году", Field0185(record, "declaredDaysPerYear"));
                Add("Формат даты", Field0185(record, "dateDisplayFormat"));
                break;
            case WorldLoreCalendarDefinitionCategories.Era:
                AddRefs("Календарь", "calendar");
                Add("Нулевой год", LocalizeYearZero0185(Field0185(record, "yearZeroPolicy")));
                Add("Направление счёта", LocalizeDirection0185(Field0185(record, "countingDirection")));
                Add("Начало", Field0185(record, "startBoundary"));
                Add("Конец", Field0185(record, "endBoundary"));
                break;
            case WorldLoreCalendarDefinitionCategories.EventType:
                Add("Категория", Field0185(record, "eventCategory"));
                Add("Тяжесть", Field0185(record, "defaultSeverity"));
                Add("Виды версий", Field0185(record, "allowedVersionKinds"));
                Add("Применимые места", Field0185(record, "applicableLocationKinds"));
                Add("Применимые объекты", Field0185(record, "applicableSubjectKinds"));
                break;
        }

        return new Dictionary<string, object>
        {
            ["displayName"] = record.DisplayName,
            ["name"] = record.DisplayName,
            ["category"] = record.Category,
            ["categoryLabel"] = WorldLore0185CategoryLabel(record.Category),
            ["family"] = record.Category,
            ["publicDescription"] = record.PublicDescription,
            ["publicTags"] = record.Tags.Where(IsPlayerSafeTag0185).Cast<object>().ToArray(),
            ["tags"] = record.Tags.Where(IsPlayerSafeTag0185).Cast<object>().ToArray(),
            ["playerFacts"] = facts.ToArray(),
            ["playerSafe"] = true
        };
    }

    private ContentDefinitionRecord? FindContent0185(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, id)).FirstOrDefault()
               ?? _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.ShortCode, id)).FirstOrDefault();
    }

    private bool LocationCycle0185(string recordId, string parentId)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { recordId };
        var current = parentId;
        for (var depth = 0; depth < 128 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            if (!seen.Add(current)) return true;
            var parent = FindContent0185(current);
            if (parent == null) return false;
            current = Field0185(parent, "parentLocation");
        }
        return !string.IsNullOrWhiteSpace(current);
    }

    private static bool LogicalParent0185(string child, string parent)
    {
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["world"] = 0, ["continent"] = 1, ["region"] = 2, ["state"] = 3,
            ["settlement"] = 4, ["district"] = 5, ["location"] = 6, ["sub_location"] = 7
        };
        if (string.Equals(child, "custom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parent, "custom", StringComparison.OrdinalIgnoreCase)) return true;
        return order.TryGetValue(child, out var childOrder)
               && order.TryGetValue(parent, out var parentOrder)
               && parentOrder < childOrder;
    }

    private static void ValidateOrderedRows0185(
        IReadOnlyCollection<string[]> rows,
        string label,
        int minimumColumns,
        ContentDefinitionValidationResult result)
    {
        var orders = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.Length < minimumColumns || !int.TryParse(row[0], out var order) || order <= 0)
            {
                result.Errors.Add($"У каждой записи {label} должен быть положительный порядок.");
                continue;
            }
            if (!orders.Add(order)) result.Errors.Add($"Порядок {order} повторяется в списке {label}.");
            if (string.IsNullOrWhiteSpace(row[1]) || !names.Add(row[1]))
                result.Errors.Add($"Названия в списке {label} должны быть заполнены и не повторяться.");
        }
    }

    private static List<string[]> ParseRows0185(string value)
        => (value ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('|').Select(x => x.Trim()).ToArray())
            .Where(parts => parts.Any(x => !string.IsNullOrWhiteSpace(x)))
            .ToList();

    private static string ReadableRows0185(string value)
        => string.Join(Environment.NewLine, ParseRows0185(value).Select(x => string.Join(" — ", x.Where(y => !string.IsNullOrWhiteSpace(y)))));

    private static string Field0185(ContentDefinitionRecord record, string key)
        => record.CustomFields.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static bool FieldBool0185(ContentDefinitionRecord record, string key)
        => bool.TryParse(Field0185(record, key), out var value) && value;
    private static int? FieldInt0185(ContentDefinitionRecord record, string key)
        => int.TryParse(Field0185(record, key), out var value) ? value : (int?)null;
    private static decimal? FieldDecimal0185(ContentDefinitionRecord record, string key)
        => decimal.TryParse(Field0185(record, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            || decimal.TryParse(Field0185(record, key), out value) ? value : (decimal?)null;
    private static bool Truthy0185(string value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "да", StringComparison.OrdinalIgnoreCase)
           || value == "1";
    private static bool IsHiddenLoreKind0185(string value)
        => value.IndexOf("truth", StringComparison.OrdinalIgnoreCase) >= 0
           || value.IndexOf("истин", StringComparison.OrdinalIgnoreCase) >= 0
           || value.IndexOf("gm", StringComparison.OrdinalIgnoreCase) >= 0
           || value.IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0
           || value.IndexOf("hidden", StringComparison.OrdinalIgnoreCase) >= 0;
    private static bool IsPlayerSafeTag0185(string value)
        => !string.IsNullOrWhiteSpace(value)
           && !value.StartsWith("gm:", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("server:", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("hidden:", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("foundation_", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("dev", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("test", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("0.", StringComparison.OrdinalIgnoreCase)
           && !value.Equals("world_lore_calendar", StringComparison.OrdinalIgnoreCase)
           && !WorldLoreCalendarDefinitionCategories.All.Contains(value, StringComparer.OrdinalIgnoreCase);
    private static string YesNo0185(bool value) => value ? "Да" : "Нет";
    private static string WorldLore0185CategoryLabel(string value) => value switch
    {
        WorldLoreCalendarDefinitionCategories.World => "Мир",
        WorldLoreCalendarDefinitionCategories.Location => "Локация",
        WorldLoreCalendarDefinitionCategories.Language => "Язык",
        WorldLoreCalendarDefinitionCategories.KnowledgeType => "Тип знания",
        WorldLoreCalendarDefinitionCategories.LoreEntry => "Знание о мире",
        WorldLoreCalendarDefinitionCategories.Calendar => "Календарь",
        WorldLoreCalendarDefinitionCategories.Era => "Эпоха",
        WorldLoreCalendarDefinitionCategories.EventType => "Тип события",
        _ => "Справочник мира"
    };
    private static string LocalizeLocationKind0185(string value) => value switch
    {
        "world" => "Мир", "continent" => "Материк", "region" => "Регион", "state" => "Государство",
        "settlement" => "Поселение", "district" => "Район", "location" => "Локация",
        "sub_location" => "Вложенная локация", _ => "Другое"
    };
    private static string LocalizeLoreKind0185(string value) => value switch
    {
        "lore" => "Сведения", "rumor" => "Слух", "document" => "Документ", "doctrine" => "Доктрина",
        "method" => "Метод", "historical_account" => "Историческое свидетельство", _ => "Другое"
    };
    private static string LocalizeLoreVersion0185(string value) => value switch
    {
        "official" => "Официальная версия", "rumor" => "Слух", "document" => "Документ", "outdated" => "Устаревшие сведения", _ => "Доступная версия"
    };
    private static string LocalizeYearModel0185(string value) => value switch
    {
        "era_based" => "По эпохам", "continuous" => "Непрерывный счёт", _ => "Особая система"
    };
    private static string LocalizeYearZero0185(string value) => value switch
    {
        "has_year_zero" => "Есть нулевой год", "no_year_zero" => "Без нулевого года", _ => "Особое правило"
    };
    private static string LocalizeDirection0185(string value) => value switch
    {
        "forward" => "Вперёд", "backward" => "Назад", _ => "Особое правило"
    };
}
