using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Driver;
using Nri.AssetConfigurators.Core.Building;
using Nri.AssetConfigurators.Core.LandMarine;
using Nri.AssetConfigurators.Core.Spacecraft;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private static List<DefinitionEditorProfile> BuildTechnologyRecipeBlueprintProjectDefinitionEditorProfiles0187()
    {
        var c = new DefinitionCategoryAliases0187();
        return new List<DefinitionEditorProfile>
        {
            Profile0181("technology_definition_profile_0187", c.Technology, "Технологии",
                "Знания и технологические принципы. Владение рецептами и чертежами проверяется отдельно.", new[]
                {
                    EnumField0187("technologyKind", "Вид технологии", true, "theory", "applied", "industrial", "scientific", "magical", "hybrid", "custom"),
                    Field0181("fieldCategory", "Область знаний", ContentDefinitionFieldTypes.String, true),
                    Field0181("tier", "Уровень", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100),
                    Field0181("complexity", "Сложность", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100),
                    RefList0187("parentTechnologies", "Родительские технологии", c.Technology),
                    RefList0187("prerequisiteTechnologies", "Предварительные технологии", c.Technology),
                    RefList0187("relatedTechnologies", "Связанные технологии", c.Technology),
                    RefList0187("opposedTechnologies", "Противопоставленные технологии", c.Technology),
                    RefList0187("requiredKnowledgeTypes", "Требуемые типы знаний", WorldLoreCalendarDefinitionCategories.KnowledgeType),
                    RefList0187("requiredLore", "Требуемые знания о мире", WorldLoreCalendarDefinitionCategories.LoreEntry),
                    RefList0187("requiredSkills", "Требуемые навыки", "skill_definition"),
                    RefList0187("requiredDevelopmentNodes", "Требуемые узлы развития", "development_node_definition"),
                    RefList0187("unlockableMethods", "Открываемые методы", c.ProductionMethod),
                    RefList0187("unlockableRecipes", "Открываемые рецепты", c.Recipe),
                    RefList0187("unlockableBlueprints", "Открываемые чертежи", c.Blueprint),
                    RefList0187("requiredFacilities", "Требуемые типы площадок", c.Facility),
                    RefList0187("requiredTools", "Требуемые инструменты", DefinitionCategoryIds.Item),
                    RefList0187("requiredLicenses", "Требуемые лицензии", FactionOrganizationEconomyDefinitionCategories.License),
                    Field0181("knownRisks", "Известные риски", ContentDefinitionFieldTypes.Tags, false),
                    HiddenText0187("gmResearchTruth", "Скрытая исследовательская истина")
                }),
            Profile0181("production_method_definition_profile_0187", c.ProductionMethod, "Методы производства",
                "Воспроизводимые способы работы. Исполняемый код и runtime-проекты здесь не хранятся.", new[]
                {
                    EnumField0187("methodKind", "Вид метода", true, "craft", "repair", "modification", "research", "reverse_engineering", "prototype", "construction", "production", "custom"),
                    RefList0187("technologies", "Совместимые технологии", c.Technology),
                    RefList0187("recipes", "Совместимые рецепты", c.Recipe),
                    RefList0187("blueprints", "Совместимые чертежи", c.Blueprint),
                    RefList0187("requiredSkills", "Требуемые навыки", "skill_definition"),
                    RefList0187("requiredFacilities", "Требуемые площадки", c.Facility),
                    RefList0187("requiredTools", "Требуемые инструменты", DefinitionCategoryIds.Item),
                    Field0181("personnelRoles", "Роли персонала", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("preparationMinutes", "Подготовка", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 525600),
                    Field0181("workDurationModel", "Модель длительности", ContentDefinitionFieldTypes.LongText, true),
                    Field0181("qualityModel", "Модель качества", ContentDefinitionFieldTypes.LongText, true),
                    Field0181("resourceLossModel", "Модель потерь ресурсов", ContentDefinitionFieldTypes.LongText, true),
                    RefList0187("requiredLicenses", "Требуемые лицензии", FactionOrganizationEconomyDefinitionCategories.License),
                    Field0181("riskTags", "Метки рисков", ContentDefinitionFieldTypes.Tags, false)
                }),
            Profile0181("recipe_definition_profile_0187", c.Recipe, "Рецепты",
                "Преобразование ресурсов и предметов без фактического списания.", new[]
                {
                    EnumField0187("recipeKind", "Вид рецепта", true, "craft", "refine", "assemble", "repair", "modify", "construction", "custom"),
                    RefList0187("methods", "Совместимые методы", c.ProductionMethod, true),
                    RefList0187("technologies", "Требуемые технологии", c.Technology),
                    StructuredField0187("inputRows", "Входные материалы", "Строка: материал | количество | единица | качество | расходование | группа замены | необязательно.", true),
                    StructuredField0187("catalystRows", "Катализаторы и инструменты", "Строка: материал или инструмент | количество | единица | качество | режим | группа замены | необязательно.", false),
                    StructuredField0187("outputRows", "Результаты", "Строка: результат | количество | единица | качество | режим | группа | необязательно.", true),
                    StructuredField0187("byproductRows", "Побочные результаты", "Строка: результат | количество | единица | качество | режим | группа | необязательно.", false),
                    StructuredField0187("wasteRows", "Отходы", "Строка: отход | количество | единица | качество | режим | группа | необязательно.", false),
                    RefList0187("requiredSkills", "Требуемые навыки", "skill_definition"),
                    RefList0187("requiredFacilities", "Требуемые площадки", c.Facility),
                    Field0181("personnelRoles", "Роли персонала", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("estimatedDurationMinutes", "Оценка времени", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 5256000),
                    Field0181("moneyCostMetadata", "Денежная стоимость", ContentDefinitionFieldTypes.String, false),
                    RefList0187("requiredLicenses", "Требуемые лицензии", FactionOrganizationEconomyDefinitionCategories.License),
                    Field0181("failureWasteProfile", "Потери при неудаче", ContentDefinitionFieldTypes.LongText, false)
                }),
            Profile0181("canonical_blueprint_definition_profile_0187", c.Blueprint, "Канонические чертежи",
                "Проверенные переиспользуемые конструкции. Личные чертежи игроков остаются отдельными server-owned drafts.", new[]
                {
                    EnumField0187("blueprintKind", "Вид конструкции", true, "item", "weapon", "armor", "vehicle", "ship", "building", "facility", "magical_construct", "custom"),
                    AnyRef0187("targetDefinition", "Целевая запись", false, DefinitionCategoryIds.Item, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Armor, c.Facility),
                    RefList0187("technologies", "Совместимые технологии", c.Technology),
                    RefList0187("methods", "Совместимые методы", c.ProductionMethod),
                    RefList0187("recipes", "Связанные рецепты", c.Recipe),
                    StructuredField0187("componentRows", "Компоненты", "Строка: компонент | количество | единица | обязательный. Неразрешённые строки нельзя сохранить.", true),
                    RefList0187("requiredFacilities", "Требуемые площадки", c.Facility),
                    RefList0187("requiredTools", "Требуемые инструменты", DefinitionCategoryIds.Item),
                    Field0181("personnelRoles", "Роли персонала", ContentDefinitionFieldTypes.Tags, false),
                    RefList0187("requiredLicenses", "Требуемые лицензии", FactionOrganizationEconomyDefinitionCategories.License),
                    Field0181("estimatedDurationMinutes", "Оценка времени", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 5256000),
                    Field0181("estimatedCost", "Оценка стоимости", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 1000000000000),
                    Field0181("estimatedResources", "Оценка ресурсов", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("qualityTolerances", "Допуски качества", ContentDefinitionFieldTypes.LongText, false),
                    RefList0187("testProtocols", "Протоколы испытаний", c.TestProtocol),
                    RefList0187("knownDefects", "Известные типы дефектов", c.Defect),
                    Ref0187("parentBlueprint", "Предыдущая версия", c.Blueprint),
                    Field0181("versionLabel", "Версия конструкции", ContentDefinitionFieldTypes.String, false),
                    HiddenText0187("sourceAssetBlueprint", "Источник личного чертежа"),
                    HiddenText0187("serverProductionFormula", "Служебная формула производства", serverOnly: true)
                }),
            Profile0181("facility_definition_profile_0187", c.Facility, "Типы площадок",
                "Тип производственной площадки. Конкретная мастерская, лаборатория или верфь остаётся runtime-активом.", new[]
                {
                    EnumField0187("facilityKind", "Вид площадки", true, "workshop", "laboratory", "factory", "shipyard", "construction_site", "forge", "magical_lab", "custom"),
                    Field0181("capabilities", "Возможности", ContentDefinitionFieldTypes.Tags, true),
                    Field0181("supportedProjectKinds", "Поддерживаемые проекты", ContentDefinitionFieldTypes.Tags, false),
                    RefList0187("supportedMethods", "Поддерживаемые методы", c.ProductionMethod),
                    Field0181("scale", "Масштаб", ContentDefinitionFieldTypes.String, true),
                    Field0181("capacityBand", "Диапазон мощности", ContentDefinitionFieldTypes.String, true),
                    RefList0187("requiredLocations", "Допустимые места", WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("personnelRoles", "Роли персонала", ContentDefinitionFieldTypes.Tags, false),
                    RefList0187("requiredTools", "Оснащение", DefinitionCategoryIds.Item),
                    RefList0187("requiredResources", "Ресурсные требования", DefinitionCategoryIds.Resource),
                    Field0181("energyRequirements", "Энергетические требования", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("maintenanceProfile", "Обслуживание", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("securityRequirements", "Требования безопасности", ContentDefinitionFieldTypes.LongText, false),
                    RefList0187("requiredLicenses", "Требуемые лицензии", FactionOrganizationEconomyDefinitionCategories.License)
                }),
            Profile0181("project_template_definition_profile_0187", c.ProjectTemplate, "Шаблоны проектов",
                "Допустимая структура будущего проекта. Редактор не создаёт ProjectBaseState.", new[]
                {
                    EnumField0187("projectType", "Вид проекта", true, "CraftItem", "RepairItem", "ModifyItem", "ResearchTheory", "ReverseEngineering", "CreatePrototype", "ImprovePrototype", "LimitedProduction", "AssetConstruction", "Custom"),
                    RefList0187("technologies", "Допустимые технологии", c.Technology),
                    RefList0187("methods", "Допустимые методы", c.ProductionMethod),
                    RefList0187("recipes", "Допустимые рецепты", c.Recipe),
                    RefList0187("blueprints", "Допустимые чертежи", c.Blueprint),
                    StructuredField0187("stageRows", "Стадии проекта", "Строка: ключ | название | порядок | предыдущие | следующие | условия | решение GM | видно игроку | публичное описание | подсказка GM.", true),
                    StructuredField0187("requirementRows", "Требования проекта", "Строка: вид | объект | количество | минимум | обязательно | режим | публичное пояснение | пояснение GM.", false),
                    Field0181("approvalPolicy", "Политика согласования", ContentDefinitionFieldTypes.LongText, true),
                    Field0181("defaultProjectVisibility", "Видимость проекта", ContentDefinitionFieldTypes.VisibilityRule, true),
                    Field0181("progressModel", "Модель прогресса", ContentDefinitionFieldTypes.LongText, true),
                    Field0181("resourceReservationPolicy", "Политика резервирования", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("cancellationRefundPolicy", "Отмена и возврат", ContentDefinitionFieldTypes.LongText, false),
                    RefList0187("testProtocols", "Требуемые испытания", c.TestProtocol),
                    Field0181("defectHandlingPolicy", "Обработка дефектов", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("completionResultKind", "Результат завершения", ContentDefinitionFieldTypes.String, true)
                }),
            Profile0181("test_protocol_definition_profile_0187", c.TestProtocol, "Протоколы испытаний",
                "Шаблон проверки прототипа или результата. Конкретный TestResult остаётся runtime.", new[]
                {
                    Field0181("applicableBlueprintKinds", "Виды чертежей", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("applicableTechnologyKinds", "Виды технологий", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("applicableMethodKinds", "Виды методов", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("requiredStage", "Требуемая стадия", ContentDefinitionFieldTypes.String, false),
                    RefList0187("requiredFacilities", "Требуемые площадки", c.Facility),
                    RefList0187("requiredTools", "Требуемые инструменты", DefinitionCategoryIds.Item),
                    Field0181("personnelRoles", "Роли персонала", ContentDefinitionFieldTypes.Tags, false),
                    StructuredField0187("testSteps", "Шаги испытания", "Строка: порядок | название | публичная инструкция | инструкция GM.", true),
                    StructuredField0187("metrics", "Метрики", "Строка: ключ | название | единица | минимум | максимум.", false),
                    Field0181("passCriteria", "Критерии успеха", ContentDefinitionFieldTypes.LongText, true),
                    Field0181("partialPassCriteria", "Критерии частичного успеха", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("failureCriteria", "Критерии неудачи", ContentDefinitionFieldTypes.LongText, true),
                    Field0181("repeatRules", "Правила повторения", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("resourceTimeCost", "Стоимость времени и ресурсов", ContentDefinitionFieldTypes.LongText, false),
                    RefList0187("effects", "Связанные эффекты", DefinitionCategoryIds.Effect),
                    RefList0187("conditions", "Связанные состояния", DefinitionCategoryIds.Condition),
                    Field0181("publicResultTemplate", "Публичный шаблон результата", ContentDefinitionFieldTypes.LongText, true),
                    HiddenText0187("gmResultTemplate", "Шаблон результата GM")
                }),
            Profile0181("defect_definition_profile_0187", c.Defect, "Типы дефектов",
                "Возможная проблема конструкции. Конкретный дефект экземпляра остаётся runtime.", new[]
                {
                    Field0181("defectCategory", "Категория", ContentDefinitionFieldTypes.String, true),
                    EnumField0187("severity", "Тяжесть", true, "minor", "moderate", "major", "critical", "custom"),
                    Field0181("applicableTechnologyKinds", "Виды технологий", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("applicableMethodKinds", "Виды методов", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("applicableBlueprintKinds", "Виды чертежей", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("detectionStage", "Стадия обнаружения", ContentDefinitionFieldTypes.String, true),
                    HiddenText0187("possibleCauses", "Возможные причины"),
                    Field0181("publicSymptoms", "Публичные признаки", ContentDefinitionFieldTypes.Tags, true),
                    HiddenText0187("gmCauseDetails", "Причина и подробности GM"),
                    RefList0187("effects", "Связанные эффекты", DefinitionCategoryIds.Effect),
                    RefList0187("conditions", "Связанные состояния", DefinitionCategoryIds.Condition),
                    StructuredField0187("repairRequirements", "Ремонт и повторная проверка", "Строка: вид требования | объект | количество | минимум | обязательно | режим | публичное пояснение | пояснение GM.", false),
                    Field0181("addedResourceCostBand", "Дополнительные ресурсы", ContentDefinitionFieldTypes.String, false),
                    Field0181("addedTimeCostBand", "Дополнительное время", ContentDefinitionFieldTypes.String, false),
                    Field0181("limitationTags", "Ограничения", ContentDefinitionFieldTypes.Tags, false)
                })
        };
    }

    private static DefinitionFieldSchema EnumField0187(string name, string label, bool required, params string[] values)
        => Field0181(name, label, ContentDefinitionFieldTypes.Enum, required, values);

    private sealed class DefinitionCategoryAliases0187
    {
        public string Technology => TechnologyRecipeBlueprintProjectDefinitionCategories.Technology;
        public string ProductionMethod => TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod;
        public string Recipe => TechnologyRecipeBlueprintProjectDefinitionCategories.Recipe;
        public string Blueprint => TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint;
        public string Facility => TechnologyRecipeBlueprintProjectDefinitionCategories.Facility;
        public string ProjectTemplate => TechnologyRecipeBlueprintProjectDefinitionCategories.ProjectTemplate;
        public string TestProtocol => TechnologyRecipeBlueprintProjectDefinitionCategories.TestProtocol;
        public string Defect => TechnologyRecipeBlueprintProjectDefinitionCategories.Defect;
    }

    private static DefinitionFieldSchema Ref0187(string name, string label, string category, bool required = false)
        => Field0181(name, label, ContentDefinitionFieldTypes.Reference, required, referenceCategory: category);

    private static DefinitionFieldSchema RefList0187(string name, string label, string category, bool required = false)
        => Field0181(name, label, ContentDefinitionFieldTypes.ReferenceList, required, referenceCategory: category);

    private static DefinitionFieldSchema AnyRef0187(string name, string label, bool required, params string[] categories)
    {
        var field = Field0181(name, label, ContentDefinitionFieldTypes.Reference, required);
        field.ReferenceTargetTypes = categories.ToList();
        return field;
    }

    private static DefinitionFieldSchema StructuredField0187(string name, string label, string help, bool required)
    {
        var field = Field0181(name, label, ContentDefinitionFieldTypes.LongText, required);
        field.EditorKind = "technology_structured_rows";
        field.HelpText = help;
        field.IsMultiline = true;
        return field;
    }

    private static DefinitionFieldSchema HiddenText0187(string name, string label, bool serverOnly = false)
    {
        var field = Field0181(name, label, ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: !serverOnly, isServerOnly: serverOnly);
        field.SectionTitle = serverOnly ? "Технические сведения" : "Только GM";
        return field;
    }

    private void ApplyTechnologyRecipeBlueprintProjectDefinitionValidation0187(
        ContentDefinitionRecord record,
        DefinitionEditorProfile profile,
        ContentDefinitionValidationResult result)
    {
        if (!TechnologyRecipeBlueprintProjectDefinitionCategories.IsSupported(record.Category)) return;
        ValidateVisibleTechnologyDefinition0187(record, profile, result);
        ValidateNoExecutableTechnologyRules0187(record, result);

        switch (record.Category)
        {
            case TechnologyRecipeBlueprintProjectDefinitionCategories.Technology:
                ValidateTechnologyGraph0187(record, "prerequisiteTechnologies", "предварительных технологий", result);
                ValidateTechnologyGraph0187(record, "parentTechnologies", "родительских технологий", result);
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod:
                ValidateProductionMethod0187(record, result);
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.Recipe:
                ValidateRecipe0187(record, result);
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint:
                ValidateBlueprint0187(record, result);
                ValidateSingleParentCycle0187(record, "parentBlueprint", TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint, "версий чертежа", result);
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.Facility:
                ValidateFacility0187(record, result);
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.ProjectTemplate:
                ValidateProjectTemplate0187(record, result);
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.TestProtocol:
                ValidateTestProtocol0187(record, result);
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.Defect:
                ValidateDefect0187(record, result);
                break;
        }
    }

    private void EnsureTechnologyRecipeBlueprintProjectDefinitionCanPersist0187(
        ContentDefinitionRecord record,
        DefinitionEditorProfile profile)
    {
        if (!TechnologyRecipeBlueprintProjectDefinitionCategories.IsSupported(record.Category)) return;
        var validation = ValidateContentDefinition0181(record, profile);
        if (validation.Errors.Count == 0) return;
        throw new ArgumentException("Запись не сохранена: " + string.Join(" ", validation.Errors.Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    private void ValidateVisibleTechnologyDefinition0187(
        ContentDefinitionRecord record,
        DefinitionEditorProfile profile,
        ContentDefinitionValidationResult result)
    {
        if (!IsDefinitionPlayerVisible0181(record)) return;
        if (string.IsNullOrWhiteSpace(record.PublicDescription))
            result.Errors.Add("Для видимой игрокам записи заполните публичное описание.");
        foreach (var schema in profile.FieldSchemas.Where(x => x.IsPlayerVisible))
        {
            foreach (var id in SplitRefs0181(Field0187(record, schema.FieldName)))
            {
                var target = FindContent0186(id);
                if (target != null && !IsDefinitionPlayerVisible0181(target))
                    result.Errors.Add($"Поле «{schema.DisplayName}» не может ссылаться на скрытую запись.");
            }
        }
    }

    private static void ValidateNoExecutableTechnologyRules0187(
        ContentDefinitionRecord record,
        ContentDefinitionValidationResult result)
    {
        foreach (var pair in record.CustomFields)
        {
            var value = Convert.ToString(pair.Value) ?? string.Empty;
            if (pair.Key.IndexOf("script", StringComparison.OrdinalIgnoreCase) >= 0
                || pair.Key.IndexOf("executable", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("<script", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("javascript:", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("System.Reflection", StringComparison.OrdinalIgnoreCase) >= 0)
                result.Errors.Add("Исполняемые scripts/rules не поддерживаются typed editor.");
        }
    }

    private void ValidateTechnologyGraph0187(
        ContentDefinitionRecord record,
        string field,
        string label,
        ContentDefinitionValidationResult result)
    {
        foreach (var start in SplitRefs0181(Field0187(record, field)))
        {
            if (string.Equals(start, record.Id, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Обнаружена self-reference в графе {label}.");
                continue;
            }
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (HasTechnologyCycle0187(start, field, record.Id, visited, active, 0))
                result.Errors.Add($"Обнаружен цикл в графе {label}.");
        }
    }

    private bool HasTechnologyCycle0187(
        string id,
        string field,
        string targetId,
        ISet<string> visited,
        ISet<string> active,
        int depth)
    {
        if (depth > 256) return true;
        if (string.Equals(id, targetId, StringComparison.OrdinalIgnoreCase)) return true;
        if (active.Contains(id)) return true;
        if (!visited.Add(id)) return false;
        var record = FindContent0186(id);
        if (record == null || !string.Equals(record.Category, TechnologyRecipeBlueprintProjectDefinitionCategories.Technology, StringComparison.OrdinalIgnoreCase))
            return false;
        active.Add(id);
        foreach (var next in SplitRefs0181(Field0187(record, field)))
            if (HasTechnologyCycle0187(next, field, targetId, visited, active, depth + 1)) return true;
        active.Remove(id);
        return false;
    }

    private void ValidateSingleParentCycle0187(
        ContentDefinitionRecord record,
        string field,
        string category,
        string label,
        ContentDefinitionValidationResult result)
    {
        var id = Field0187(record, field);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { record.Id };
        for (var depth = 0; !string.IsNullOrWhiteSpace(id) && depth < 256; depth++)
        {
            if (!seen.Add(id))
            {
                result.Errors.Add($"Обнаружен цикл в иерархии {label}.");
                return;
            }
            var parent = FindContent0186(id);
            if (parent == null) return;
            if (!string.Equals(parent.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Иерархия {label} содержит запись неправильного типа.");
                return;
            }
            id = Field0187(parent, field);
        }
        if (!string.IsNullOrWhiteSpace(id)) result.Errors.Add($"Иерархия {label} превышает безопасную глубину.");
    }

    private void ValidateProductionMethod0187(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var kind = Field0187(record, "methodKind");
        if (new[] { "production", "construction", "prototype", "repair" }.Contains(kind, StringComparer.OrdinalIgnoreCase)
            && !SplitRefs0181(Field0187(record, "requiredFacilities")).Any()
            && !SplitRefs0181(Field0187(record, "requiredTools")).Any())
            result.Errors.Add("Для производственного метода укажите площадку или инструмент.");
        ValidateNonNegative0187(record, result, "preparationMinutes");
    }

    private void ValidateRecipe0187(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var inputs = ParseRows0187(Field0187(record, "inputRows"));
        var outputs = ParseRows0187(Field0187(record, "outputRows"));
        if (inputs.Count == 0) result.Errors.Add("Рецепт должен содержать хотя бы один входной материал.");
        if (outputs.Count == 0) result.Errors.Add("Рецепт должен содержать хотя бы один результат.");
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requirePlayerVisible = IsDefinitionPlayerVisible0181(record);
        foreach (var row in inputs.Concat(ParseRows0187(Field0187(record, "catalystRows"))))
        {
            ValidateMaterialRow0187(row, "входную строку", result, requirePlayerVisible);
            if (row.Length > 0 && !duplicates.Add(string.Join("|", row.Take(6))))
                result.Errors.Add("Обнаружена повторяющаяся несовместимая строка ингредиента.");
        }
        foreach (var row in outputs.Concat(ParseRows0187(Field0187(record, "byproductRows"))).Concat(ParseRows0187(Field0187(record, "wasteRows"))))
            ValidateMaterialRow0187(row, "выходную строку", result, requirePlayerVisible);
        ValidateNonNegative0187(record, result, "estimatedDurationMinutes");
    }

    private void ValidateMaterialRow0187(string[] row, string label, ContentDefinitionValidationResult result, bool requirePlayerVisible)
    {
        if (row.Length < 7)
        {
            result.Errors.Add($"Заполните {label}: объект, количество, единица, качество, режим, группа замены и необязательность.");
            return;
        }
        ValidateAnyItemResourceReference0187(row[0], label, result, requirePlayerVisible);
        if (!TryDecimal0187(row[1], out var quantity) || quantity <= 0)
            result.Errors.Add($"Количество в {label} должно быть положительным.");
    }

    private void ValidateBlueprint0187(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var rows = ParseRows0187(Field0187(record, "componentRows"));
        var requirePlayerVisible = IsDefinitionPlayerVisible0181(record);
        if (rows.Count == 0) result.Errors.Add("Канонический чертёж должен содержать компоненты.");
        foreach (var row in rows)
        {
            if (row.Length < 5)
            {
                result.Errors.Add("Строка компонента должна содержать объект, название, количество, единицу и обязательность.");
                continue;
            }
            var required = ParseBool0187(row[4], true);
            if (required && (row[0].StartsWith("unresolved:", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(row[0])))
                result.Errors.Add($"Обязательный компонент «{row[1]}» не сопоставлен со справочником.");
            else if (!row[0].StartsWith("unresolved:", StringComparison.OrdinalIgnoreCase))
                ValidateAnyItemResourceReference0187(row[0], "компонент", result, requirePlayerVisible);
            if (!TryDecimal0187(row[2], out var quantity) || quantity <= 0)
                result.Errors.Add("Количество компонента должно быть положительным.");
        }
        ValidateNonNegative0187(record, result, "estimatedDurationMinutes", "estimatedCost");
    }

    private void ValidateFacility0187(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        if (!SplitTags0187(Field0187(record, "capabilities")).Any())
            result.Errors.Add("Тип площадки должен иметь хотя бы одну возможность.");
    }

    private void ValidateProjectTemplate0187(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var stages = ParseRows0187(Field0187(record, "stageRows"));
        if (stages.Count == 0)
        {
            result.Errors.Add("Шаблон проекта должен содержать стадии.");
            return;
        }
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orders = new HashSet<int>();
        var next = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in stages)
        {
            if (row.Length < 10 || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]))
            {
                result.Errors.Add("Каждая стадия должна содержать десять читаемых значений.");
                continue;
            }
            if (!keys.Add(row[0])) result.Errors.Add($"Ключ стадии «{row[0]}» повторяется.");
            if (!int.TryParse(row[2], out var order) || !orders.Add(order)) result.Errors.Add("Порядок стадий должен быть уникальным целым числом.");
            next[row[0]] = SplitSemicolon0187(row[4]).ToList();
        }
        if (result.Errors.Any()) return;
        foreach (var pair in next)
            foreach (var target in pair.Value)
                if (!keys.Contains(target)) result.Errors.Add($"Стадия «{pair.Key}» ссылается на неизвестный следующий этап «{target}».");
        if (HasStageCycle0187(next)) result.Errors.Add("Переходы стадий содержат цикл.");
        var first = stages.OrderBy(x => int.Parse(x[2], CultureInfo.InvariantCulture)).First()[0];
        var reachable = ReachableStages0187(first, next);
        foreach (var key in keys.Where(x => !reachable.Contains(x)))
            result.Errors.Add($"Обязательная стадия «{key}» недостижима из начальной стадии.");
        ValidateRequirementRows0187(ParseRows0187(Field0187(record, "requirementRows")), result);
    }

    private static bool HasStageCycle0187(IReadOnlyDictionary<string, List<string>> graph)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool Visit(string key)
        {
            if (active.Contains(key)) return true;
            if (!visited.Add(key)) return false;
            active.Add(key);
            if (graph.TryGetValue(key, out var children))
                foreach (var child in children)
                    if (Visit(child)) return true;
            active.Remove(key);
            return false;
        }
        return graph.Keys.Any(Visit);
    }

    private static HashSet<string> ReachableStages0187(string first, IReadOnlyDictionary<string, List<string>> graph)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(first);
        while (queue.Count > 0)
        {
            var key = queue.Dequeue();
            if (!result.Add(key)) continue;
            if (graph.TryGetValue(key, out var children))
                foreach (var child in children) queue.Enqueue(child);
        }
        return result;
    }

    private void ValidateRequirementRows0187(IEnumerable<string[]> rows, ContentDefinitionValidationResult result)
    {
        var allowedKinds = new HashSet<string>(new[]
        {
            "Technology", "Knowledge", "Blueprint", "Method", "Recipe", "Skill", "Resource", "Item",
            "MaterialQuality", "Specialist", "PersonnelRole", "Facility", "ToolCapability", "Money", "Time",
            "License", "LegalStatus", "GMApproval", "CustomManual"
        }, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.Length < 8)
            {
                result.Errors.Add("Строка требования должна содержать восемь значений.");
                continue;
            }
            if (!allowedKinds.Contains(row[0])) result.Errors.Add($"Неизвестный вид требования: {row[0]}.");
            if (!string.IsNullOrWhiteSpace(row[2]) && (!TryDecimal0187(row[2], out var quantity) || quantity < 0))
                result.Errors.Add("Количество требования не может быть отрицательным.");
        }
    }

    private void ValidateTestProtocol0187(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        if (!ParseRows0187(Field0187(record, "testSteps")).Any())
            result.Errors.Add("Протокол испытаний должен содержать хотя бы один шаг.");
        if (string.IsNullOrWhiteSpace(Field0187(record, "passCriteria")))
            result.Errors.Add("Заполните критерии успешного испытания.");
        if (string.IsNullOrWhiteSpace(Field0187(record, "failureCriteria")))
            result.Errors.Add("Заполните критерии неудачного испытания.");
    }

    private void ValidateDefect0187(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        if (!SplitTags0187(Field0187(record, "applicableTechnologyKinds")).Any()
            && !SplitTags0187(Field0187(record, "applicableMethodKinds")).Any()
            && !SplitTags0187(Field0187(record, "applicableBlueprintKinds")).Any())
            result.Errors.Add("Тип дефекта должен иметь хотя бы одну применимую цель.");
        ValidateRequirementRows0187(ParseRows0187(Field0187(record, "repairRequirements")), result);
    }

    private void ValidateAnyItemResourceReference0187(string id, string label, ContentDefinitionValidationResult result, bool requirePlayerVisible)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            result.Errors.Add($"Не выбран объект для поля «{label}».");
            return;
        }
        var target = FindContent0186(id);
        var unified = target == null
            ? _mongo.UnifiedDefinitions.Find(Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, id)).FirstOrDefault()
            : null;
        if (target == null && unified == null)
        {
            result.BrokenReferences.Add(id);
            result.Errors.Add($"Связанная запись для поля «{label}» не найдена.");
            return;
        }
        var category = target?.Category ?? unified?.Category ?? string.Empty;
        var isArchived = target?.IsArchived == true || unified?.IsArchived == true;
        var playerVisible = target != null
            ? IsDefinitionPlayerVisible0181(target)
            : unified != null && (string.Equals(unified.VisibilityRule, VisibilityRuleIds.Public, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(unified.VisibilityRule, VisibilityRuleIds.PlayerVisible, StringComparison.OrdinalIgnoreCase));
        if (isArchived) result.Errors.Add($"Нельзя использовать архивную запись в поле «{label}».");
        if (!new[] { DefinitionCategoryIds.Item, DefinitionCategoryIds.Resource, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Ammo, DefinitionCategoryIds.Armor }
            .Contains(category, StringComparer.OrdinalIgnoreCase))
            result.Errors.Add($"Поле «{label}» ссылается на несовместимый тип записи.");
        if (requirePlayerVisible && !playerVisible)
            result.Errors.Add($"Видимая запись не может ссылаться на скрытый объект в поле «{label}».");
    }

    private static void ValidateNonNegative0187(ContentDefinitionRecord record, ContentDefinitionValidationResult result, params string[] fields)
    {
        foreach (var field in fields)
        {
            var text = Field0187(record, field);
            if (!string.IsNullOrWhiteSpace(text) && (!TryDecimal0187(text, out var value) || value < 0))
                result.Errors.Add($"Значение поля «{field}» не может быть отрицательным.");
        }
    }

    public ResponseEnvelope TechnologyRecipeBlueprintProjectPlayerList0187(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var records = _mongo.ContentDefinitionRecords.Find(
                Builders<ContentDefinitionRecord>.Filter.In(x => x.Category, TechnologyRecipeBlueprintProjectDefinitionCategories.All)
                & Builders<ContentDefinitionRecord>.Filter.Ne(x => x.IsArchived, true))
            .ToList()
            .Where(IsDefinitionPlayerVisible0181)
            .OrderBy(x => Array.IndexOf(TechnologyRecipeBlueprintProjectDefinitionCategories.All, x.Category))
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var lookup = BuildTechnologyDefinitionLookup0187();
        var items = records.Select(x => (object)TechnologyPlayerPayload0187(x, lookup)).ToArray();
        return Ok("Справочник технологий, рецептов и чертежей загружен.", new Dictionary<string, object>
        {
            ["items"] = items,
            ["count"] = items.Length,
            ["familyLabel"] = "Технологии, рецепты и чертежи",
            ["playerSafe"] = true
        });
    }

    public ResponseEnvelope TechnologyRecipeBlueprintProjectPlayerGet0187(CommandContext context)
    {
        GetCurrentAccount(context);
        var id = RequireDefinitionId0181(context.Request.Payload);
        var record = GetContentDefinitionRecord0181(id);
        if (!TechnologyRecipeBlueprintProjectDefinitionCategories.IsSupported(record.Category)
            || record.IsArchived
            || !IsDefinitionPlayerVisible0181(record))
            throw new KeyNotFoundException("Открытая игрокам запись не найдена.");
        return Ok("Запись открыта.", new Dictionary<string, object>
        {
            ["definition"] = TechnologyPlayerPayload0187(record, BuildTechnologyDefinitionLookup0187())
        });
    }

    public ResponseEnvelope TechnologyBlueprintAdminPrepareFromAsset0187(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sourceId = FirstNonEmpty0181(
            PayloadReader.GetString(context.Request.Payload, "assetBlueprintId"),
            PayloadReader.GetString(context.Request.Payload, "blueprintId"),
            PayloadReader.GetString(context.Request.Payload, "id"));
        if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("Выберите личный чертёж.");
        var source = _repositories.AssetConfigurationBlueprints.GetById(sourceId)
                     ?? throw new KeyNotFoundException("Личный чертёж не найден.");
        var rows = BuildAssetComponentDraftRows0187(source);
        var unresolved = rows.Count(x => x.StartsWith("unresolved:", StringComparison.OrdinalIgnoreCase));
        var kind = source.ConfiguratorKind == AssetConfiguratorKindIds.Spacecraft
            ? "ship"
            : source.ConfiguratorKind == AssetConfiguratorKindIds.LandMarine ? "vehicle" : "building";
        var customFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["blueprintKind"] = kind,
            ["componentRows"] = string.Join(Environment.NewLine, rows),
            ["estimatedCost"] = Math.Max(0, source.ServerCalculation.TotalCost).ToString(CultureInfo.InvariantCulture),
            ["estimatedResources"] = source.ReadableSummary,
            ["versionLabel"] = "draft-from-player-blueprint-r" + source.Revision,
            ["sourceAssetBlueprint"] = source.Id
        };
        _logger.Admin($"technology_blueprint.prepare_from_asset actor={actor.Login} source={source.Id} unresolved={unresolved} persisted=false");
        return Ok("Несохранённый канонический черновик подготовлен.", new Dictionary<string, object>
        {
            ["profileId"] = "canonical_blueprint_definition_profile_0187",
            ["category"] = TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint,
            ["draft"] = new Dictionary<string, object>
            {
                ["name"] = source.Name,
                ["displayName"] = source.Name,
                ["publicDescription"] = source.ReadableSummary,
                ["gmDescription"] = "Черновик подготовлен из личного чертежа игрока. Проверьте все связи перед сохранением.",
                ["visibilityRule"] = ContentDefinitionVisibilityRules.GmOnly,
                ["customFields"] = customFields
            },
            ["resolvedComponentCount"] = rows.Count - unresolved,
            ["unresolvedComponentCount"] = unresolved,
            ["sourceUnchanged"] = true,
            ["definitionPersisted"] = false
        });
    }

    private Dictionary<string, string> BuildTechnologyDefinitionLookup0187()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Empty).ToList())
        {
            if (!record.IsArchived && IsDefinitionPlayerVisible0181(record))
                result[record.Id] = record.DisplayName;
        }
        foreach (var document in _mongo.UnifiedDefinitions.Find(Builders<UnifiedDefinitionDocument>.Filter.Empty).ToList())
        {
            if (Equipment0183PlayerVisible(document))
                result[document.Id] = document.Name;
        }
        return result;
    }

    private Dictionary<string, object> TechnologyPlayerPayload0187(
        ContentDefinitionRecord record,
        IReadOnlyDictionary<string, string> lookup)
    {
        var facts = new List<object>();
        void Add(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                facts.Add(new Dictionary<string, object> { ["label"] = label, ["value"] = value });
        }
        void AddRefs(string label, string field)
        {
            var names = SplitRefs0181(Field0187(record, field))
                .Select(id => lookup.TryGetValue(id, out var displayName) ? displayName : string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names.Length > 0) Add(label, string.Join(", ", names));
        }
        void AddRows(string label, string field, int referenceColumn = 0, int maxPublicColumns = 7)
        {
            var rows = ParseRows0187(Field0187(record, field))
                .Select(row =>
                {
                    var copy = row.Take(maxPublicColumns).ToArray();
                    if (copy.Length > referenceColumn)
                    {
                        if (!lookup.TryGetValue(copy[referenceColumn], out var displayName))
                            return string.Empty;
                        copy[referenceColumn] = displayName;
                    }
                    return string.Join(" · ", copy.Where(x => !string.IsNullOrWhiteSpace(x)));
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            if (rows.Length > 0) Add(label, string.Join(Environment.NewLine, rows));
        }

        switch (record.Category)
        {
            case TechnologyRecipeBlueprintProjectDefinitionCategories.Technology:
                Add("Вид технологии", LocalizeTechnologyValue0187(Field0187(record, "technologyKind")));
                Add("Область", Field0187(record, "fieldCategory"));
                Add("Уровень", Field0187(record, "tier"));
                Add("Сложность", Field0187(record, "complexity"));
                AddRefs("Предварительные технологии", "prerequisiteTechnologies");
                AddRefs("Требуемые знания", "requiredKnowledgeTypes");
                AddRefs("Требуемые навыки", "requiredSkills");
                AddRefs("Открываемые методы", "unlockableMethods");
                AddRefs("Открываемые рецепты", "unlockableRecipes");
                AddRefs("Открываемые чертежи", "unlockableBlueprints");
                AddRefs("Требуемые площадки", "requiredFacilities");
                Add("Известные риски", Field0187(record, "knownRisks"));
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod:
                Add("Вид метода", LocalizeTechnologyValue0187(Field0187(record, "methodKind")));
                AddRefs("Технологии", "technologies");
                AddRefs("Требуемые навыки", "requiredSkills");
                AddRefs("Площадки", "requiredFacilities");
                Add("Подготовка, мин.", Field0187(record, "preparationMinutes"));
                Add("Длительность", Field0187(record, "workDurationModel"));
                Add("Качество", Field0187(record, "qualityModel"));
                Add("Потери ресурсов", Field0187(record, "resourceLossModel"));
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.Recipe:
                Add("Вид рецепта", LocalizeTechnologyValue0187(Field0187(record, "recipeKind")));
                AddRefs("Методы", "methods");
                AddRefs("Технологии", "technologies");
                AddRows("Входные материалы", "inputRows");
                AddRows("Катализаторы и инструменты", "catalystRows");
                AddRows("Результаты", "outputRows");
                AddRows("Побочные результаты", "byproductRows");
                Add("Оценка времени, мин.", Field0187(record, "estimatedDurationMinutes"));
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint:
                Add("Вид конструкции", LocalizeTechnologyValue0187(Field0187(record, "blueprintKind")));
                AddRefs("Технологии", "technologies");
                AddRefs("Методы", "methods");
                AddRefs("Рецепты", "recipes");
                AddRows("Компоненты", "componentRows", maxPublicColumns: 5);
                AddRefs("Площадки", "requiredFacilities");
                Add("Оценка времени, мин.", Field0187(record, "estimatedDurationMinutes"));
                Add("Оценка стоимости", Field0187(record, "estimatedCost"));
                Add("Оценка ресурсов", Field0187(record, "estimatedResources"));
                AddRefs("Публичные испытания", "testProtocols");
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.Facility:
                Add("Вид площадки", LocalizeTechnologyValue0187(Field0187(record, "facilityKind")));
                Add("Возможности", Field0187(record, "capabilities"));
                Add("Масштаб", Field0187(record, "scale"));
                Add("Мощность", Field0187(record, "capacityBand"));
                AddRefs("Поддерживаемые методы", "supportedMethods");
                Add("Энергетические требования", Field0187(record, "energyRequirements"));
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.ProjectTemplate:
                Add("Вид проекта", LocalizeTechnologyValue0187(Field0187(record, "projectType")));
                AddRows("Стадии", "stageRows", maxPublicColumns: 9);
                AddRows("Требования", "requirementRows", maxPublicColumns: 7);
                Add("Согласование", Field0187(record, "approvalPolicy"));
                Add("Прогресс", Field0187(record, "progressModel"));
                Add("Результат", Field0187(record, "completionResultKind"));
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.TestProtocol:
                AddRows("Шаги испытания", "testSteps", maxPublicColumns: 3);
                Add("Критерии успеха", Field0187(record, "passCriteria"));
                Add("Частичный успех", Field0187(record, "partialPassCriteria"));
                Add("Критерии неудачи", Field0187(record, "failureCriteria"));
                Add("Повторение", Field0187(record, "repeatRules"));
                Add("Результат", Field0187(record, "publicResultTemplate"));
                break;
            case TechnologyRecipeBlueprintProjectDefinitionCategories.Defect:
                Add("Категория", Field0187(record, "defectCategory"));
                Add("Тяжесть", LocalizeTechnologyValue0187(Field0187(record, "severity")));
                Add("Стадия обнаружения", Field0187(record, "detectionStage"));
                Add("Известные признаки", Field0187(record, "publicSymptoms"));
                Add("Дополнительные ресурсы", Field0187(record, "addedResourceCostBand"));
                Add("Дополнительное время", Field0187(record, "addedTimeCostBand"));
                Add("Ограничения", Field0187(record, "limitationTags"));
                break;
        }
        Add("Статус справочника", "Опубликовано в справочнике");
        Add("Знание персонажа", "Определяется знаниями персонажа и правилами кампании");
        Add("Воспроизводимость", "Требует выполнения метода, требований, лицензий и проекта");

        return new Dictionary<string, object>
        {
            ["family"] = record.Category,
            ["category"] = record.Category,
            ["categoryLabel"] = TechnologyCategoryLabel0187(record.Category),
            ["displayName"] = record.DisplayName,
            ["name"] = record.Name,
            ["publicDescription"] = record.PublicDescription,
            ["publicTags"] = record.Tags.Where(IsPlayerSafeTag).ToArray(),
            ["playerFacts"] = facts.ToArray(),
            ["publishedState"] = "Опубликовано в справочнике",
            ["knowledgeState"] = "Доступность персонажу определяется его знаниями и правилами кампании",
            ["reproductionState"] = "Воспроизводимость определяется методом, требованиями, лицензией и проектом",
            ["playerSafe"] = true
        };
    }

    private List<string> BuildAssetComponentDraftRows0187(AssetConfigurationBlueprintState source)
    {
        var rows = new List<string>();
        IEnumerable<AssetBlueprintComponentState> components = source.ConfiguratorKind switch
        {
            AssetConfiguratorKindIds.Spacecraft => source.Configuration.Spacecraft?.Components ?? new List<AssetBlueprintComponentState>(),
            AssetConfiguratorKindIds.LandMarine => source.Configuration.LandMarine?.Components ?? new List<AssetBlueprintComponentState>(),
            AssetConfiguratorKindIds.Building => source.Configuration.Building?.Components ?? new List<AssetBlueprintComponentState>(),
            _ => new List<AssetBlueprintComponentState>()
        };
        foreach (var component in components)
        {
            var target = ResolveAssetComponentDefinition0187(component.ComponentKey);
            var label = target?.DisplayName ?? AssetComponentDisplayName0187(source.ConfiguratorKind, component.ComponentKey);
            var reference = target?.Id ?? "unresolved:" + component.ComponentKey;
            rows.Add(string.Join(" | ", reference, label, Math.Max(1, component.Quantity).ToString(CultureInfo.InvariantCulture), "шт.", "true"));
        }
        if (rows.Count == 0)
        {
            var key = source.ConfiguratorKind + "_configuration";
            rows.Add(string.Join(" | ", "unresolved:" + key, "Основная конфигурация: " + source.Name, "1", "комплект", "true"));
        }
        return rows;
    }

    private ContentDefinitionRecord? ResolveAssetComponentDefinition0187(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var candidates = _mongo.ContentDefinitionRecords.Find(
                Builders<ContentDefinitionRecord>.Filter.In(x => x.Category, new[]
                {
                    DefinitionCategoryIds.Item, DefinitionCategoryIds.Resource, DefinitionCategoryIds.Weapon,
                    DefinitionCategoryIds.Ammo, DefinitionCategoryIds.Armor
                })
                & Builders<ContentDefinitionRecord>.Filter.Ne(x => x.IsArchived, true))
            .ToList();
        return candidates.FirstOrDefault(x => string.Equals(x.ShortCode, key, StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(x.Name, key, StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(x.Id, key, StringComparison.OrdinalIgnoreCase));
    }

    private static string AssetComponentDisplayName0187(string configuratorKind, string key)
    {
        var displayName = configuratorKind switch
        {
            AssetConfiguratorKindIds.Spacecraft => SpacecraftCatalog.Index.DisplayName(key),
            AssetConfiguratorKindIds.LandMarine => LandMarineCatalog.Index.DisplayName(key),
            AssetConfiguratorKindIds.Building => BuildingCatalog.Index.DisplayName(key),
            _ => string.Empty
        };
        return string.IsNullOrWhiteSpace(displayName) ? ReadableAssetKey0187(key) : displayName;
    }

    private static string ReadableAssetKey0187(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "Неизвестный компонент";
        var parts = key.Split(':');
        if (parts.Length >= 2)
        {
            var category = parts[1].Replace('_', ' ').Replace('-', ' ');
            return "Компонент категории «" + CultureInfo.GetCultureInfo("ru-RU").TextInfo.ToTitleCase(category) + "»";
        }
        return "Компонент требует ручного сопоставления";
    }

    private static string Field0187(ContentDefinitionRecord record, string name)
        => record.CustomFields.TryGetValue(name, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private static List<string[]> ParseRows0187(string value)
        => (value ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('|').Select(x => x.Trim()).ToArray())
            .Where(row => row.Any(x => !string.IsNullOrWhiteSpace(x)))
            .ToList();

    private static IEnumerable<string> SplitSemicolon0187(string value)
        => (value ?? string.Empty).Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0);

    private static IEnumerable<string> SplitTags0187(string value) => SplitSemicolon0187(value);

    private static bool TryDecimal0187(string value, out decimal result)
        => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
           || decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result);

    private static bool ParseBool0187(string value, bool fallback)
        => bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static string TechnologyCategoryLabel0187(string category)
    {
        if (category == TechnologyRecipeBlueprintProjectDefinitionCategories.Technology) return "Технология";
        if (category == TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod) return "Метод производства";
        if (category == TechnologyRecipeBlueprintProjectDefinitionCategories.Recipe) return "Рецепт";
        if (category == TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint) return "Канонический чертёж";
        if (category == TechnologyRecipeBlueprintProjectDefinitionCategories.Facility) return "Тип площадки";
        if (category == TechnologyRecipeBlueprintProjectDefinitionCategories.ProjectTemplate) return "Шаблон проекта";
        if (category == TechnologyRecipeBlueprintProjectDefinitionCategories.TestProtocol) return "Протокол испытаний";
        if (category == TechnologyRecipeBlueprintProjectDefinitionCategories.Defect) return "Тип дефекта";
        return category;
    }

    private static string LocalizeTechnologyValue0187(string value)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["theory"] = "Теория", ["applied"] = "Прикладная", ["industrial"] = "Промышленная",
            ["scientific"] = "Научная", ["magical"] = "Магическая", ["hybrid"] = "Гибридная",
            ["craft"] = "Изготовление", ["repair"] = "Ремонт", ["modification"] = "Модификация",
            ["research"] = "Исследование", ["reverse_engineering"] = "Обратная разработка",
            ["prototype"] = "Прототипирование", ["construction"] = "Строительство", ["production"] = "Производство",
            ["refine"] = "Переработка", ["assemble"] = "Сборка", ["item"] = "Предмет", ["weapon"] = "Оружие",
            ["armor"] = "Броня", ["vehicle"] = "Техника", ["ship"] = "Корабль", ["building"] = "Здание",
            ["facility"] = "Площадка", ["magical_construct"] = "Магическая конструкция",
            ["minor"] = "Незначительный", ["moderate"] = "Средний", ["major"] = "Серьёзный", ["critical"] = "Критический",
            ["custom"] = "Другое"
        };
        return labels.TryGetValue(value ?? string.Empty, out var label) ? label : value;
    }
}
