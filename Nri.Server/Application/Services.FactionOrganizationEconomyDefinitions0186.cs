using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private static List<DefinitionEditorProfile> BuildFactionOrganizationEconomyDefinitionEditorProfiles0186()
    {
        var c = new DefinitionCategoryAliases0186();
        var profiles = new List<DefinitionEditorProfile>
        {
            Profile0181("faction_definition_profile_0186", c.Faction, "Фракции",
                "Authored faction identity. Campaign relationship scores remain runtime.", new[]
                {
                    Field0181("factionCategory", "Категория", ContentDefinitionFieldTypes.String, true),
                    Field0181("parentFaction", "Родительская фракция", ContentDefinitionFieldTypes.Reference, false, referenceCategory: c.Faction),
                    Field0181("relatedFactions", "Связанные фракции", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Faction),
                    Field0181("alliedFactions", "Союзные фракции", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Faction),
                    Field0181("rivalFactions", "Соперники и противники", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Faction),
                    Field0181("publicIdentity", "Публичный образ", ContentDefinitionFieldTypes.LongText, true),
                    Field0181("publicGoals", "Публичные цели", ContentDefinitionFieldTypes.LongText, true),
                    HiddenField0186("hiddenGoals", "Скрытые цели"),
                    Field0181("ideologyTags", "Идеология и принципы", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("homeLocations", "Родные территории", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("claimedLocations", "Заявленные территории", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("languages", "Языки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Language),
                    Field0181("jurisdictions", "Юрисдикции", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Jurisdiction),
                    Field0181("currencies", "Валюты", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Currency),
                    Field0181("organizations", "Связанные организации", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Organization),
                    StructuredField0186("relationshipLabels", "Названия отношений", "Одна строка: ключ | публичное название | пояснение GM.", false)
                }),
            Profile0181("organization_definition_profile_0186", c.Organization, "Организации",
                "Authored organization identity and defaults. Owners, personnel, assets and balances remain runtime.", new[]
                {
                    Field0181("organizationKind", "Вид организации", ContentDefinitionFieldTypes.String, true),
                    Field0181("parentOrganization", "Родительская организация", ContentDefinitionFieldTypes.Reference, false, referenceCategory: c.Organization),
                    Field0181("controllingFactions", "Контролирующие фракции", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Faction),
                    Field0181("publicImage", "Публичный образ", ContentDefinitionFieldTypes.LongText, true),
                    Field0181("legalStatus", "Юридический статус", ContentDefinitionFieldTypes.String, true),
                    Field0181("declaredActivity", "Заявленная деятельность", ContentDefinitionFieldTypes.LongText, true),
                    HiddenField0186("actualActivity", "Фактическая деятельность"),
                    HiddenField0186("hiddenActivity", "Скрытая деятельность"),
                    Field0181("headquartersLocations", "Штаб-квартиры", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("operatingLocations", "Места деятельности", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("businessProfile", "Экономический профиль", ContentDefinitionFieldTypes.Reference, false, referenceCategory: c.BusinessProfile),
                    Field0181("currencies", "Валюты", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Currency),
                    Field0181("markets", "Рынки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Market),
                    Field0181("licenses", "Требуемые лицензии", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.License),
                    Field0181("personnelRoles", "Типовые роли персонала", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("supplierCustomerTags", "Поставщики и клиенты", ContentDefinitionFieldTypes.Tags, false),
                    HiddenBooleanField0186("allowIndependentOrganization", "Разрешить независимую организацию")
                }),
            Profile0181("jurisdiction_definition_profile_0186", c.Jurisdiction, "Юрисдикции",
                "Authored legal jurisdiction hierarchy.", new[]
                {
                    Field0181("jurisdictionKind", "Вид юрисдикции", ContentDefinitionFieldTypes.String, true),
                    Field0181("governingFaction", "Управляющая фракция", ContentDefinitionFieldTypes.Reference, false, referenceCategory: c.Faction),
                    Field0181("governingOrganization", "Управляющая организация", ContentDefinitionFieldTypes.Reference, false, referenceCategory: c.Organization),
                    Field0181("locations", "Применимые территории", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("parentJurisdiction", "Родительская юрисдикция", ContentDefinitionFieldTypes.Reference, false, referenceCategory: c.Jurisdiction),
                    Field0181("defaultLaws", "Законы по умолчанию", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Law),
                    Field0181("defaultControlLevel", "Уровень контроля по умолчанию", ContentDefinitionFieldTypes.Reference, true, referenceCategory: c.ControlLevel),
                    Field0181("recognizedLicenses", "Признаваемые лицензии", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.License),
                    Field0181("acceptedCurrencies", "Принимаемые валюты", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: c.Currency),
                    Field0181("enforcementLevel", "Интенсивность контроля", ContentDefinitionFieldTypes.String, true),
                    Field0181("appealExceptions", "Исключения и обжалование", ContentDefinitionFieldTypes.LongText, false)
                }),
            Profile0181("law_definition_profile_0186", c.Law, "Законы",
                "Authored action-control policy. It does not execute arrests, fines or investigations.", new[]
                {
                    Field0181("lawCategory", "Категория закона", ContentDefinitionFieldTypes.String, true),
                    Field0181("jurisdictions", "Юрисдикции", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: c.Jurisdiction),
                    Field0181("applicableCategories", "Категории объектов", ContentDefinitionFieldTypes.Tags, true),
                    LawActionRulesField0186(),
                    Field0181("defaultControlLevel", "Уровень контроля", ContentDefinitionFieldTypes.Reference, true, referenceCategory: c.ControlLevel),
                    Field0181("requiredLicenses", "Требуемые лицензии", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.License),
                    Field0181("exemptions", "Исключения", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("prohibitedTags", "Запрещённые признаки", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("restrictedTags", "Ограниченные признаки", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("militaryFlag", "Военное регулирование", ContentDefinitionFieldTypes.Boolean, false),
                    Field0181("strategicFlag", "Стратегическое регулирование", ContentDefinitionFieldTypes.Boolean, false),
                    Field0181("publicConsequence", "Публичные последствия", ContentDefinitionFieldTypes.LongText, true),
                    HiddenField0186("gmConsequence", "Последствия для GM"),
                    HiddenField0186("enforcementGuidance", "Рекомендации по применению")
                }),
            Profile0181("license_definition_profile_0186", c.License, "Лицензии",
                "Authored license requirements. Issued documents remain entity-scoped runtime.", new[]
                {
                    Field0181("licenseKind", "Вид лицензии", ContentDefinitionFieldTypes.String, true),
                    Field0181("issuerFaction", "Фракция-издатель", ContentDefinitionFieldTypes.Reference, false, referenceCategory: c.Faction),
                    Field0181("issuerOrganization", "Организация-издатель", ContentDefinitionFieldTypes.Reference, false, referenceCategory: c.Organization),
                    Field0181("issuerJurisdiction", "Юрисдикция-издатель", ContentDefinitionFieldTypes.Reference, false, referenceCategory: c.Jurisdiction),
                    Field0181("laws", "Связанные законы", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Law),
                    Field0181("coveredActions", "Разрешённые действия", ContentDefinitionFieldTypes.Tags, true),
                    Field0181("coveredCategories", "Покрываемые категории", ContentDefinitionFieldTypes.Tags, true),
                    Field0181("prerequisiteLicenses", "Предварительные лицензии", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.License),
                    Field0181("fees", "Пошлины и стоимость", ContentDefinitionFieldTypes.String, false),
                    Field0181("validityModel", "Срок действия", ContentDefinitionFieldTypes.String, true),
                    Field0181("renewalRules", "Правила продления", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("transferable", "Можно передавать", ContentDefinitionFieldTypes.Boolean, false),
                    Field0181("revocable", "Можно отозвать", ContentDefinitionFieldTypes.Boolean, false),
                    Field0181("publicRequirements", "Публичные требования", ContentDefinitionFieldTypes.LongText, true),
                    HiddenField0186("hiddenRequirements", "Скрытые требования")
                }),
            Profile0181("currency_definition_profile_0186", c.Currency, "Валюты",
                "Authored currency metadata. Wallet balances and current exchange rates remain runtime.", new[]
                {
                    Field0181("symbol", "Символ", ContentDefinitionFieldTypes.String, true),
                    Field0181("issuer", "Эмитент", ContentDefinitionFieldTypes.Reference, false),
                    Field0181("currencyKind", "Вид валюты", ContentDefinitionFieldTypes.Enum, true, new[] { "physical_currency", "digital_currency", "commodity_currency", "custom" }),
                    Field0181("decimalPrecision", "Знаков после запятой", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 8),
                    StructuredField0186("denominations", "Номиналы", "Одна строка: название | символ | множитель.", true),
                    Field0181("jurisdictions", "Юрисдикции", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Jurisdiction),
                    Field0181("preferredMarkets", "Предпочтительные рынки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Market),
                    Field0181("legality", "Правовой статус", ContentDefinitionFieldTypes.String, true),
                    Field0181("rarityStabilityTags", "Редкость и стабильность", ContentDefinitionFieldTypes.Tags, false)
                }),
            Profile0181("market_definition_profile_0186", c.Market, "Рынки",
                "Authored market profile. Offers, stock, prices and transactions remain runtime.", new[]
                {
                    Field0181("marketKind", "Вид рынка", ContentDefinitionFieldTypes.String, true),
                    Field0181("locations", "Места", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("jurisdictions", "Юрисдикции", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Jurisdiction),
                    Field0181("factions", "Контролирующие фракции", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Faction),
                    Field0181("organizations", "Контролирующие организации", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.Organization),
                    Field0181("currencies", "Принимаемые валюты", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: c.Currency),
                    Field0181("offerKinds", "Разрешённые виды предложений", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: c.MarketOfferKind),
                    Field0181("allowedCategories", "Разрешённые категории", ContentDefinitionFieldTypes.Tags, true),
                    Field0181("restrictedCategories", "Ограниченные категории", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("prohibitedCategories", "Запрещённые категории", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("defaultLegalPolicy", "Правовая политика", ContentDefinitionFieldTypes.String, true),
                    Field0181("priceBands", "Диапазоны цен", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("availabilityBands", "Доступность и редкость", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("scheduleAccess", "Расписание и доступ", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("publicRiskSummary", "Публичное описание риска", ContentDefinitionFieldTypes.LongText, true),
                    HiddenField0186("personnelPolicy", "Политика живых предложений"),
                    HiddenField0186("largeAssetPolicy", "Политика крупных активов")
                }),
            Profile0181("business_profile_definition_profile_0186", c.BusinessProfile, "Экономические профили",
                "Authored organization economy template. Actual accounting and maintenance remain runtime.", new[]
                {
                    Field0181("businessKind", "Вид деятельности", ContentDefinitionFieldTypes.String, true),
                    Field0181("economicScale", "Экономический масштаб", ContentDefinitionFieldTypes.Reference, true, referenceCategory: c.EconomicScale),
                    Field0181("declaredActivities", "Типовая заявленная деятельность", ContentDefinitionFieldTypes.Tags, true),
                    HiddenField0186("possibleActualActivities", "Возможная фактическая деятельность"),
                    Field0181("requiredLocations", "Требуемые места", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WorldLoreCalendarDefinitionCategories.Location),
                    Field0181("requiredFacilities", "Требуемая инфраструктура", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("personnelRequirements", "Требования к персоналу", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("resourceRequirements", "Требования к ресурсам", ContentDefinitionFieldTypes.LongText, false),
                    EconomicBandField0186("incomeBand", "Доход"),
                    EconomicBandField0186("expenseBand", "Расходы"),
                    EconomicBandField0186("taxRentBand", "Налоги и аренда"),
                    Field0181("securityRequirements", "Требования безопасности", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("maintenanceRequirements", "Требования обслуживания", ContentDefinitionFieldTypes.LongText, false),
                    Field0181("licenses", "Требуемые лицензии", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: c.License),
                    Field0181("supplierCustomerCategories", "Категории поставщиков и клиентов", ContentDefinitionFieldTypes.Tags, false),
                    Field0181("riskTags", "Риски", ContentDefinitionFieldTypes.Tags, false)
                }),
            OrderedOptionProfile0186("control_level_option_profile_0186", c.ControlLevel, "Уровни правового контроля", true),
            OrderedOptionProfile0186("economic_scale_option_profile_0186", c.EconomicScale, "Экономические масштабы", true),
            OrderedOptionProfile0186("market_offer_kind_option_profile_0186", c.MarketOfferKind, "Виды рыночных предложений", false)
        };

        foreach (var profile in profiles)
        {
            profile.SchemaVersion = 4;
            profile.DefaultTags = profile.DefaultTags
                .Concat(new[] { "foundation_0_18_6", "faction_organization_economy" })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            profile.ValidationRules.Add("faction-organization-law-market-typed-validation");
            foreach (var field in profile.FieldSchemas)
            {
                if (string.IsNullOrWhiteSpace(field.HelpText))
                    field.HelpText = field.IsRequired ? "Обязательное поле." : "Поле можно оставить пустым.";
            }
        }
        return profiles;
    }

    private static DefinitionEditorProfile OrderedOptionProfile0186(
        string id,
        string category,
        string displayName,
        bool includeRank)
    {
        var fields = new List<DefinitionFieldSchema>
        {
            Field0181("order", "Порядок", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10000),
            Field0181("playerLabel", "Название для игрока", ContentDefinitionFieldTypes.String, true)
        };
        if (includeRank)
            fields.Add(Field0181("rank", "Уровень", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10000));
        else
            fields.Add(Field0181("offerCategory", "Категория предложения", ContentDefinitionFieldTypes.String, true));
        return Profile0181(id, category, displayName, "RuleSet-driven ordered option definition.", fields);
    }

    private static DefinitionFieldSchema StructuredField0186(
        string name,
        string label,
        string help,
        bool required)
    {
        var field = Field0181(name, label, ContentDefinitionFieldTypes.LongText, required);
        field.EditorKind = "multiline_text";
        field.HelpText = help + " Это структурированный список, не JSON.";
        field.SectionTitle = "Структурированные данные";
        return field;
    }

    private static DefinitionFieldSchema LawActionRulesField0186()
    {
        var field = StructuredField0186(
            "actionRules",
            "Правила действий",
            "Одна строка: действие | уровень контроля | лицензии через ; | разрешённые субъекты | ограниченные субъекты | разрешённые места | ограниченные места | результат | публичное предупреждение | заметки GM.",
            true);
        field.IsPlayerVisible = false;
        field.IsGmOnly = true;
        field.SectionTitle = "Матрица действий";
        return field;
    }

    private static DefinitionFieldSchema EconomicBandField0186(string name, string label)
        => StructuredField0186(name, label, "Одна строка: минимум | максимум | валюта | период.", false);

    private static DefinitionFieldSchema HiddenField0186(string name, string label)
    {
        var field = Field0181(name, label, ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true);
        field.SectionTitle = "Только GM";
        return field;
    }

    private static DefinitionFieldSchema HiddenBooleanField0186(string name, string label)
    {
        var field = Field0181(name, label, ContentDefinitionFieldTypes.Boolean, false, isPlayerVisible: false, isGmOnly: true);
        field.SectionTitle = "Только GM";
        return field;
    }

    private void ApplyFactionOrganizationEconomyDefinitionValidation0186(
        ContentDefinitionRecord record,
        DefinitionEditorProfile profile,
        ContentDefinitionValidationResult result)
    {
        if (!FactionOrganizationEconomyDefinitionCategories.IsSupported(record.Category)) return;

        ValidateVisibleDefinition0186(record, profile, result);
        ValidateNoExecutableRules0186(record, result);

        switch (record.Category)
        {
            case FactionOrganizationEconomyDefinitionCategories.Faction:
                ValidateParentCycle0186(record, "parentFaction", FactionOrganizationEconomyDefinitionCategories.Faction, "фракций", result);
                break;
            case FactionOrganizationEconomyDefinitionCategories.Organization:
                ValidateParentCycle0186(record, "parentOrganization", FactionOrganizationEconomyDefinitionCategories.Organization, "организаций", result);
                if (string.IsNullOrWhiteSpace(Field0186(record, "parentOrganization"))
                    && !SplitRefs0181(Field0186(record, "controllingFactions")).Any()
                    && !FieldBool0186(record, "allowIndependentOrganization"))
                    result.Errors.Add("Организация без родителя и контролирующей фракции должна быть явно отмечена как независимая.");
                break;
            case FactionOrganizationEconomyDefinitionCategories.Jurisdiction:
                ValidateParentCycle0186(record, "parentJurisdiction", FactionOrganizationEconomyDefinitionCategories.Jurisdiction, "юрисдикций", result);
                break;
            case FactionOrganizationEconomyDefinitionCategories.Law:
                ValidateLaw0186(record, result);
                break;
            case FactionOrganizationEconomyDefinitionCategories.License:
                ValidateParentCycle0186(record, "prerequisiteLicenses", FactionOrganizationEconomyDefinitionCategories.License, "предварительных лицензий", result, multiple: true);
                ValidateIssuer0186(record, result);
                break;
            case FactionOrganizationEconomyDefinitionCategories.Currency:
                ValidateCurrency0186(record, result);
                break;
            case FactionOrganizationEconomyDefinitionCategories.Market:
                ValidateMarket0186(record, result);
                break;
            case FactionOrganizationEconomyDefinitionCategories.BusinessProfile:
                ValidateBusinessProfile0186(record, result);
                break;
            case FactionOrganizationEconomyDefinitionCategories.ControlLevel:
            case FactionOrganizationEconomyDefinitionCategories.EconomicScale:
            case FactionOrganizationEconomyDefinitionCategories.MarketOfferKind:
                ValidateOption0186(record, result);
                break;
        }
    }

    private void EnsureFactionOrganizationEconomyDefinitionCanPersist0186(
        ContentDefinitionRecord record,
        DefinitionEditorProfile profile)
    {
        if (!FactionOrganizationEconomyDefinitionCategories.IsSupported(record.Category)) return;
        var validation = ValidateContentDefinition0181(record, profile);
        if (validation.Errors.Count == 0) return;
        throw new ArgumentException("Запись не сохранена: " + string.Join(" ", validation.Errors.Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    private void ValidateVisibleDefinition0186(
        ContentDefinitionRecord record,
        DefinitionEditorProfile profile,
        ContentDefinitionValidationResult result)
    {
        if (!IsDefinitionPlayerVisible0181(record)) return;
        if (string.IsNullOrWhiteSpace(record.PublicDescription))
            result.Errors.Add("Для видимой игрокам записи заполните публичное описание.");

        foreach (var schema in profile.FieldSchemas.Where(x => x.IsPlayerVisible))
        {
            foreach (var id in SplitRefs0181(Field0186(record, schema.FieldName)))
            {
                var target = FindContent0186(id);
                if (target != null && !IsDefinitionPlayerVisible0181(target))
                    result.Errors.Add($"Поле «{schema.DisplayName}» не может ссылаться на скрытую запись.");
            }
        }
    }

    private static void ValidateNoExecutableRules0186(
        ContentDefinitionRecord record,
        ContentDefinitionValidationResult result)
    {
        foreach (var pair in record.CustomFields)
        {
            var value = Convert.ToString(pair.Value) ?? string.Empty;
            if (pair.Key.IndexOf("script", StringComparison.OrdinalIgnoreCase) >= 0
                || pair.Key.IndexOf("executable", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("<script", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("javascript:", StringComparison.OrdinalIgnoreCase) >= 0)
                result.Errors.Add("Исполняемые scripts/rules не поддерживаются typed editor.");
        }
    }

    private void ValidateParentCycle0186(
        ContentDefinitionRecord record,
        string field,
        string expectedCategory,
        string label,
        ContentDefinitionValidationResult result,
        bool multiple = false)
    {
        var starts = multiple ? SplitRefs0181(Field0186(record, field)) : new List<string> { Field0186(record, field) };
        foreach (var start in starts.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (string.Equals(start, record.Id, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Обнаружена self-reference в иерархии {label}.");
                continue;
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { record.Id };
            var queue = new Queue<string>();
            queue.Enqueue(start);
            var depth = 0;
            while (queue.Count > 0 && depth++ < 256)
            {
                var currentId = queue.Dequeue();
                if (!seen.Add(currentId))
                {
                    result.Errors.Add($"Обнаружен цикл в иерархии {label}.");
                    break;
                }
                var current = FindContent0186(currentId);
                if (current == null) continue;
                if (!string.Equals(current.Category, expectedCategory, StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"Иерархия {label} содержит запись неправильного типа.");
                    continue;
                }
                var next = Field0186(current, field);
                foreach (var id in multiple ? SplitRefs0181(next) : new List<string> { next })
                    if (!string.IsNullOrWhiteSpace(id)) queue.Enqueue(id);
            }
            if (queue.Count > 0) result.Errors.Add($"Иерархия {label} превышает безопасную глубину.");
        }
    }

    private void ValidateLaw0186(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var rows = ParseRows0186(Field0186(record, "actionRules"));
        if (rows.Count == 0)
        {
            result.Errors.Add("Добавьте хотя бы одно правило действия.");
            return;
        }
        var actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requirePlayerVisibleReferences = IsDefinitionPlayerVisible0181(record);
        var validActions = new HashSet<string>(new[] { "buy", "sell", "own", "carry", "transport", "use", "build", "produce", "repair" }, StringComparer.OrdinalIgnoreCase);
        var validResults = new HashSet<string>(new[] { "allowed", "registration_required", "licensed", "restricted", "military_only", "prohibited", "custom" }, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.Length < 10)
            {
                result.Errors.Add("Каждое правило действия должно содержать десять значений.");
                continue;
            }
            if (!validActions.Contains(row[0])) result.Errors.Add($"Неизвестное действие закона: {row[0]}.");
            if (!actions.Add(row[0])) result.Errors.Add($"Действие «{row[0]}» повторяется.");
            ValidateReferenceCategory0186(row[1], FactionOrganizationEconomyDefinitionCategories.ControlLevel, "уровень контроля", result, requirePlayerVisibleReferences);
            foreach (var licenseId in SplitSemicolon0186(row[2]))
                ValidateReferenceCategory0186(licenseId, FactionOrganizationEconomyDefinitionCategories.License, "лицензию", result, requirePlayerVisibleReferences);
            if (!validResults.Contains(row[7])) result.Errors.Add($"Неизвестный результат правила: {row[7]}.");
        }
    }

    private void ValidateIssuer0186(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var issuers = new[]
        {
            Field0186(record, "issuerFaction"),
            Field0186(record, "issuerOrganization"),
            Field0186(record, "issuerJurisdiction")
        }.Count(x => !string.IsNullOrWhiteSpace(x));
        if (issuers == 0) result.Errors.Add("Укажите хотя бы одного издателя лицензии.");
    }

    private void ValidateCurrency0186(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ParseRows0186(Field0186(record, "denominations")))
        {
            if (row.Length < 3 || string.IsNullOrWhiteSpace(row[0]) || !names.Add(row[0]))
            {
                result.Errors.Add("Названия номиналов должны быть заполнены и не повторяться.");
                continue;
            }
            if ((!decimal.TryParse(row[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var multiplier)
                 && !decimal.TryParse(row[2], out multiplier)) || multiplier <= 0)
                result.Errors.Add("Множитель номинала должен быть положительным числом.");
        }
    }

    private void ValidateMarket0186(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        if (!SplitRefs0181(Field0186(record, "currencies")).Any())
            result.Errors.Add("Рынок должен принимать хотя бы одну валюту.");
        if (!SplitRefs0181(Field0186(record, "locations")).Any()
            && !SplitRefs0181(Field0186(record, "jurisdictions")).Any())
            result.Errors.Add("Рынок должен быть связан с местом или юрисдикцией.");
        if (!SplitRefs0181(Field0186(record, "offerKinds")).Any())
            result.Errors.Add("Выберите хотя бы один разрешённый вид предложения.");
    }

    private void ValidateBusinessProfile0186(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var requirePlayerVisibleReferences = IsDefinitionPlayerVisible0181(record);
        foreach (var field in new[] { "incomeBand", "expenseBand", "taxRentBand" })
        {
            foreach (var row in ParseRows0186(Field0186(record, field)))
            {
                if (row.Length < 4
                    || (!decimal.TryParse(row[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var min) && !decimal.TryParse(row[0], out min))
                    || (!decimal.TryParse(row[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var max) && !decimal.TryParse(row[1], out max))
                    || min < 0 || max < 0 || min > max)
                {
                    result.Errors.Add($"Диапазон «{field}» должен содержать неотрицательные минимум и максимум.");
                    continue;
                }
                ValidateReferenceCategory0186(row[2], FactionOrganizationEconomyDefinitionCategories.Currency, "валюту", result, requirePlayerVisibleReferences);
            }
        }
    }

    private static void ValidateOption0186(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        if (!int.TryParse(Field0186(record, "order"), out _))
            result.Errors.Add("Порядок RuleSet option должен быть целым числом.");
        if (!string.IsNullOrWhiteSpace(Field0186(record, "rank")) && !int.TryParse(Field0186(record, "rank"), out _))
            result.Errors.Add("Уровень RuleSet option должен быть целым числом.");
    }

    private void ValidateReferenceCategory0186(
        string id,
        string category,
        string label,
        ContentDefinitionValidationResult result,
        bool requirePlayerVisible = false)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var target = FindContent0186(id);
        if (target == null)
        {
            result.BrokenReferences.Add(id);
            result.Errors.Add($"Выбранная {label} не найдена.");
        }
        else if (target.IsArchived)
            result.Errors.Add($"Нельзя использовать архивную {label}.");
        else if (!string.Equals(target.Category, category, StringComparison.OrdinalIgnoreCase))
            result.Errors.Add($"Выбрана запись неправильного типа для поля «{label}».");
        else if (requirePlayerVisible && !IsDefinitionPlayerVisible0181(target))
            result.Errors.Add($"Видимая игрокам запись не может ссылаться на скрытую запись «{target.DisplayName}» в поле «{label}».");
    }

    public ResponseEnvelope FactionOrganizationEconomyPlayerList0186(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var records = _mongo.ContentDefinitionRecords.Find(
                Builders<ContentDefinitionRecord>.Filter.In(x => x.Category, FactionOrganizationEconomyDefinitionCategories.All)
                & Builders<ContentDefinitionRecord>.Filter.Ne(x => x.IsArchived, true))
            .ToList()
            .Where(IsDefinitionPlayerVisible0181)
            .OrderBy(x => Array.IndexOf(FactionOrganizationEconomyDefinitionCategories.All, x.Category))
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var lookup = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Empty)
            .ToList()
            .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
        var items = records.Select(x => (object)FactionEconomy0186PlayerPayload(x, lookup)).ToArray();
        return Ok("Справочник фракций, организаций и рынков загружен.", new Dictionary<string, object>
        {
            ["items"] = items,
            ["count"] = items.Length,
            ["familyLabel"] = "Фракции, организации и рынки",
            ["playerSafe"] = true
        });
    }

    public ResponseEnvelope FactionOrganizationEconomyPlayerGet0186(CommandContext context)
    {
        GetCurrentAccount(context);
        var id = RequireDefinitionId0181(context.Request.Payload);
        var record = GetContentDefinitionRecord0181(id);
        if (!FactionOrganizationEconomyDefinitionCategories.IsSupported(record.Category)
            || record.IsArchived
            || !IsDefinitionPlayerVisible0181(record))
            throw new KeyNotFoundException("Открытая игрокам запись не найдена.");
        var lookup = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Empty)
            .ToList()
            .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
        return Ok("Запись открыта.", new Dictionary<string, object>
        {
            ["definition"] = FactionEconomy0186PlayerPayload(record, lookup)
        });
    }

    private Dictionary<string, object> FactionEconomy0186PlayerPayload(
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
            => AddRefIds(label, SplitRefs0181(Field0186(record, field)));
        void AddRefIds(string label, IEnumerable<string> ids)
        {
            var names = ids
                .Select(id => lookup.TryGetValue(id, out var target) && IsDefinitionPlayerVisible0181(target) ? target.DisplayName : string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names.Length > 0) Add(label, string.Join(", ", names));
        }

        switch (record.Category)
        {
            case FactionOrganizationEconomyDefinitionCategories.Faction:
                Add("Категория", Field0186(record, "factionCategory"));
                AddRefs("Родительская фракция", "parentFaction");
                AddRefs("Союзники", "alliedFactions");
                AddRefs("Соперники", "rivalFactions");
                Add("Публичный образ", Field0186(record, "publicIdentity"));
                Add("Публичные цели", Field0186(record, "publicGoals"));
                AddRefs("Родные территории", "homeLocations");
                AddRefs("Заявленные территории", "claimedLocations");
                AddRefs("Языки", "languages");
                AddRefs("Юрисдикции", "jurisdictions");
                AddRefs("Валюты", "currencies");
                AddRefs("Организации", "organizations");
                break;
            case FactionOrganizationEconomyDefinitionCategories.Organization:
                Add("Вид организации", Field0186(record, "organizationKind"));
                AddRefs("Родительская организация", "parentOrganization");
                AddRefs("Контролирующие фракции", "controllingFactions");
                Add("Публичный образ", Field0186(record, "publicImage"));
                Add("Юридический статус", Field0186(record, "legalStatus"));
                Add("Заявленная деятельность", Field0186(record, "declaredActivity"));
                AddRefs("Штаб-квартиры", "headquartersLocations");
                AddRefs("Места деятельности", "operatingLocations");
                AddRefs("Экономический профиль", "businessProfile");
                AddRefs("Валюты", "currencies");
                AddRefs("Рынки", "markets");
                AddRefs("Требуемые лицензии", "licenses");
                break;
            case FactionOrganizationEconomyDefinitionCategories.Jurisdiction:
                Add("Вид юрисдикции", Field0186(record, "jurisdictionKind"));
                AddRefs("Управляющая фракция", "governingFaction");
                AddRefs("Управляющая организация", "governingOrganization");
                AddRefs("Территории", "locations");
                AddRefs("Родительская юрисдикция", "parentJurisdiction");
                AddRefs("Законы", "defaultLaws");
                AddRefs("Уровень контроля", "defaultControlLevel");
                AddRefs("Лицензии", "recognizedLicenses");
                AddRefs("Валюты", "acceptedCurrencies");
                Add("Интенсивность контроля", Field0186(record, "enforcementLevel"));
                break;
            case FactionOrganizationEconomyDefinitionCategories.Law:
                Add("Категория закона", Field0186(record, "lawCategory"));
                AddRefs("Юрисдикции", "jurisdictions");
                Add("Категории объектов", Field0186(record, "applicableCategories"));
                AddRefs("Уровень контроля", "defaultControlLevel");
                AddRefs("Требуемые лицензии", "requiredLicenses");
                foreach (var row in ParseRows0186(Field0186(record, "actionRules")))
                {
                    if (row.Length < 10) continue;
                    var control = lookup.TryGetValue(row[1], out var level) && IsDefinitionPlayerVisible0181(level) ? level.DisplayName : string.Empty;
                    var licenseNames = SplitSemicolon0186(row[2])
                        .Select(id => lookup.TryGetValue(id, out var license) && IsDefinitionPlayerVisible0181(license) ? license.DisplayName : string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x));
                    var value = $"{LocalizeLegalAction0186(row[0])}: {LocalizeLegalResult0186(row[7])}";
                    if (!string.IsNullOrWhiteSpace(control)) value += $"; контроль: {control}";
                    var licenses = string.Join(", ", licenseNames);
                    if (!string.IsNullOrWhiteSpace(licenses)) value += $"; лицензии: {licenses}";
                    if (!string.IsNullOrWhiteSpace(row[8])) value += $"; {row[8]}";
                    Add("Правило", value);
                }
                Add("Публичные последствия", Field0186(record, "publicConsequence"));
                break;
            case FactionOrganizationEconomyDefinitionCategories.License:
                Add("Вид лицензии", Field0186(record, "licenseKind"));
                AddRefs("Фракция-издатель", "issuerFaction");
                AddRefs("Организация-издатель", "issuerOrganization");
                AddRefs("Юрисдикция-издатель", "issuerJurisdiction");
                AddRefs("Законы", "laws");
                Add("Разрешённые действия", Field0186(record, "coveredActions"));
                Add("Покрываемые категории", Field0186(record, "coveredCategories"));
                AddRefs("Предварительные лицензии", "prerequisiteLicenses");
                Add("Пошлины", Field0186(record, "fees"));
                Add("Срок действия", Field0186(record, "validityModel"));
                Add("Публичные требования", Field0186(record, "publicRequirements"));
                break;
            case FactionOrganizationEconomyDefinitionCategories.Currency:
                Add("Символ", Field0186(record, "symbol"));
                AddRefs("Эмитент", "issuer");
                Add("Вид валюты", LocalizeCurrencyKind0186(Field0186(record, "currencyKind")));
                Add("Номиналы", ReadableRows0186(Field0186(record, "denominations")));
                AddRefs("Юрисдикции", "jurisdictions");
                AddRefs("Рынки", "preferredMarkets");
                Add("Правовой статус", Field0186(record, "legality"));
                break;
            case FactionOrganizationEconomyDefinitionCategories.Market:
                Add("Вид рынка", Field0186(record, "marketKind"));
                AddRefs("Места", "locations");
                AddRefs("Юрисдикции", "jurisdictions");
                AddRefs("Фракции", "factions");
                AddRefs("Организации", "organizations");
                AddRefs("Валюты", "currencies");
                AddRefs("Виды предложений", "offerKinds");
                Add("Доступные категории", Field0186(record, "allowedCategories"));
                Add("Правовая политика", Field0186(record, "defaultLegalPolicy"));
                Add("Расписание и доступ", Field0186(record, "scheduleAccess"));
                Add("Риски", Field0186(record, "publicRiskSummary"));
                break;
            case FactionOrganizationEconomyDefinitionCategories.BusinessProfile:
                Add("Вид деятельности", Field0186(record, "businessKind"));
                AddRefs("Экономический масштаб", "economicScale");
                Add("Заявленная деятельность", Field0186(record, "declaredActivities"));
                AddRefs("Требуемые места", "requiredLocations");
                Add("Инфраструктура", Field0186(record, "requiredFacilities"));
                Add("Персонал", Field0186(record, "personnelRequirements"));
                Add("Ресурсы", Field0186(record, "resourceRequirements"));
                Add("Доход", ReadableEconomicBand0186(Field0186(record, "incomeBand"), lookup));
                Add("Расходы", ReadableEconomicBand0186(Field0186(record, "expenseBand"), lookup));
                Add("Налоги и аренда", ReadableEconomicBand0186(Field0186(record, "taxRentBand"), lookup));
                Add("Безопасность", Field0186(record, "securityRequirements"));
                Add("Обслуживание", Field0186(record, "maintenanceRequirements"));
                AddRefs("Лицензии", "licenses");
                break;
            case FactionOrganizationEconomyDefinitionCategories.ControlLevel:
            case FactionOrganizationEconomyDefinitionCategories.EconomicScale:
            case FactionOrganizationEconomyDefinitionCategories.MarketOfferKind:
                Add("Название", Field0186(record, "playerLabel"));
                Add("Порядок", Field0186(record, "order"));
                Add("Уровень", Field0186(record, "rank"));
                break;
        }

        return new Dictionary<string, object>
        {
            ["displayName"] = record.DisplayName,
            ["name"] = record.DisplayName,
            ["category"] = record.Category,
            ["categoryLabel"] = FactionEconomy0186CategoryLabel(record.Category),
            ["family"] = record.Category,
            ["publicDescription"] = record.PublicDescription,
            ["publicTags"] = record.Tags.Where(IsPlayerSafeTag0186).Cast<object>().ToArray(),
            ["tags"] = record.Tags.Where(IsPlayerSafeTag0186).Cast<object>().ToArray(),
            ["playerFacts"] = facts.ToArray(),
            ["playerSafe"] = true
        };
    }

    private ContentDefinitionRecord? FindContent0186(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, id)).FirstOrDefault()
               ?? _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.ShortCode, id)).FirstOrDefault();
    }

    private static List<string[]> ParseRows0186(string value)
        => (value ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('|').Select(x => x.Trim()).ToArray())
            .Where(parts => parts.Any(x => !string.IsNullOrWhiteSpace(x)))
            .ToList();

    private static IEnumerable<string> SplitSemicolon0186(string value)
        => (value ?? string.Empty)
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x));

    private static string ReadableRows0186(string value)
        => string.Join(Environment.NewLine, ParseRows0186(value).Select(x => string.Join(" — ", x.Where(y => !string.IsNullOrWhiteSpace(y)))));

    private static string ReadableEconomicBand0186(
        string value,
        IReadOnlyDictionary<string, ContentDefinitionRecord> lookup)
    {
        var row = ParseRows0186(value).FirstOrDefault();
        if (row == null || row.Length < 4) return string.Empty;
        var currency = lookup.TryGetValue(row[2], out var record) && IsDefinitionPlayerVisible0181(record)
            ? record.DisplayName
            : string.Empty;
        return $"{row[0]}–{row[1]} {currency} / {row[3]}".Trim();
    }

    private static string Field0186(ContentDefinitionRecord record, string key)
        => record.CustomFields.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private static bool FieldBool0186(ContentDefinitionRecord record, string key)
        => bool.TryParse(Field0186(record, key), out var value) && value;

    private static bool IsPlayerSafeTag0186(string value)
        => !string.IsNullOrWhiteSpace(value)
           && !value.StartsWith("gm:", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("server:", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("hidden:", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("foundation_", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("dev", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("test", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("0.", StringComparison.OrdinalIgnoreCase)
           && !value.Equals("faction_organization_economy", StringComparison.OrdinalIgnoreCase)
           && !FactionOrganizationEconomyDefinitionCategories.All.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static string FactionEconomy0186CategoryLabel(string value) => value switch
    {
        FactionOrganizationEconomyDefinitionCategories.Faction => "Фракция",
        FactionOrganizationEconomyDefinitionCategories.Organization => "Организация",
        FactionOrganizationEconomyDefinitionCategories.Jurisdiction => "Юрисдикция",
        FactionOrganizationEconomyDefinitionCategories.Law => "Закон",
        FactionOrganizationEconomyDefinitionCategories.License => "Лицензия",
        FactionOrganizationEconomyDefinitionCategories.Currency => "Валюта",
        FactionOrganizationEconomyDefinitionCategories.Market => "Рынок",
        FactionOrganizationEconomyDefinitionCategories.BusinessProfile => "Экономический профиль",
        FactionOrganizationEconomyDefinitionCategories.ControlLevel => "Уровень контроля",
        FactionOrganizationEconomyDefinitionCategories.EconomicScale => "Экономический масштаб",
        FactionOrganizationEconomyDefinitionCategories.MarketOfferKind => "Вид предложения",
        _ => "Экономика"
    };

    private static string LocalizeLegalAction0186(string value) => value switch
    {
        "buy" => "Покупка",
        "sell" => "Продажа",
        "own" => "Владение",
        "carry" => "Ношение",
        "transport" => "Перевозка",
        "use" => "Использование",
        "build" => "Строительство",
        "produce" => "Производство",
        "repair" => "Ремонт",
        _ => "Особое действие"
    };

    private static string LocalizeLegalResult0186(string value) => value switch
    {
        "allowed" => "Разрешено",
        "registration_required" => "Нужна регистрация",
        "licensed" => "Нужна лицензия",
        "restricted" => "Ограничено",
        "military_only" => "Только военным",
        "prohibited" => "Запрещено",
        _ => "Особое правило"
    };

    private static string LocalizeCurrencyKind0186(string value) => value switch
    {
        "physical_currency" => "Физическая",
        "digital_currency" => "Цифровая",
        "commodity_currency" => "Товарная",
        _ => "Особая"
    };

    private sealed class DefinitionCategoryAliases0186
    {
        public string Faction => FactionOrganizationEconomyDefinitionCategories.Faction;
        public string Organization => FactionOrganizationEconomyDefinitionCategories.Organization;
        public string Jurisdiction => FactionOrganizationEconomyDefinitionCategories.Jurisdiction;
        public string Law => FactionOrganizationEconomyDefinitionCategories.Law;
        public string License => FactionOrganizationEconomyDefinitionCategories.License;
        public string Currency => FactionOrganizationEconomyDefinitionCategories.Currency;
        public string Market => FactionOrganizationEconomyDefinitionCategories.Market;
        public string BusinessProfile => FactionOrganizationEconomyDefinitionCategories.BusinessProfile;
        public string ControlLevel => FactionOrganizationEconomyDefinitionCategories.ControlLevel;
        public string EconomicScale => FactionOrganizationEconomyDefinitionCategories.EconomicScale;
        public string MarketOfferKind => FactionOrganizationEconomyDefinitionCategories.MarketOfferKind;
    }
}
