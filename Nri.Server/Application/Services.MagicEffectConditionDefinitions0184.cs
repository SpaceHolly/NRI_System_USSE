using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope MagicDefinitionsAdminList(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var families = Magic0184RequestedFamilies(payload);
        var filter = Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, families);
        if (!PayloadReader.GetBool(payload, "includeArchived"))
        {
            filter &= Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        }

        var items = _mongo.UnifiedDefinitions.Find(filter).ToList();
        var search = PayloadReader.GetString(payload, "search") ?? string.Empty;
        var ruleSetId = PayloadReader.GetString(payload, "ruleSetId") ?? string.Empty;
        var visibility = PayloadReader.GetString(payload, "visibility") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(search))
        {
            items = items.Where(x => Equipment0183Contains(Magic0184CanonicalId(x), search)
                                     || Equipment0183Contains(x.Name, search)
                                     || Equipment0183Contains(x.PublicDescription, search)
                                     || (x.Tags ?? new List<string>()).Any(tag => Equipment0183Contains(tag, search))
                                     || Equipment0183Contains(Equipment0183ExtraString(x.ExtraData, "profileCategory"), search))
                .ToList();
        }
        if (!string.IsNullOrWhiteSpace(ruleSetId))
        {
            items = items.Where(x => (x.RuleSetIds ?? new List<string>()).Contains(ruleSetId, StringComparer.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(visibility) && !string.Equals(visibility, "all", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(x => string.Equals(x.VisibilityRule, visibility, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var views = items.OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => (object)Magic0184AdminView(x))
            .ToArray();
        _logger.Admin($"definitions.magic.admin.list count={views.Length}");
        return Ok("Определения магии и состояний загружены.", new Dictionary<string, object>
        {
            ["items"] = views,
            ["sourceOfTruth"] = "unified_definitions",
            ["families"] = families.Cast<object>().ToArray()
        });
    }

    public ResponseEnvelope MagicDefinitionsAdminGet(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var family = Magic0184Family(payload);
        var definitionId = Magic0184DefinitionId(payload);
        var document = Magic0184Find(family, definitionId);
        if (document == null) return Error("Определение магии или состояния не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var validation = Magic0184Validate(document, document.ExtraData, true);
        return Ok("Определение загружено.", new Dictionary<string, object>
        {
            ["item"] = Magic0184AdminView(document),
            ["brokenReferences"] = validation.Errors.Cast<object>().ToArray(),
            ["warnings"] = validation.Warnings.Cast<object>().ToArray(),
            ["sourceOfTruth"] = "unified_definitions"
        });
    }

    public ResponseEnvelope MagicDefinitionsAdminSave(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var family = Magic0184Family(payload);
        if (!MagicEffectConditionDefinitionFamilies.IsSupported(family))
        {
            return Error("Выберите поддерживаемое семейство магии или состояний.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        }

        var name = FirstNonEmpty(PayloadReader.GetString(payload, "name"), PayloadReader.GetString(payload, "displayName")).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error("Название обязательно.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        }

        var definitionId = Magic0184DefinitionId(payload);
        var isCreate = string.IsNullOrWhiteSpace(definitionId) || PayloadReader.GetBool(payload, "isCreate");
        if (string.IsNullOrWhiteSpace(definitionId)) definitionId = Equipment0183GenerateId(family, name);
        var existing = Magic0184Find(family, definitionId);
        if (isCreate && existing != null && !existing.IsArchived)
        {
            return Error("Определение с таким идентификатором уже существует.", ResponseStatus.Error, ErrorCode.Conflict);
        }

        var now = DateTime.UtcNow;
        var document = existing ?? new UnifiedDefinitionDocument
        {
            Id = definitionId,
            Category = family,
            CreatedAtUtc = now,
            CreatedUtc = now,
            SourceDocument = "foundation_0_18_4_typed_editor"
        };
        document.Id = definitionId;
        document.Category = family;
        document.Name = name;
        document.PublicDescription = PayloadReader.GetString(payload, "publicDescription") ?? string.Empty;
        document.GMDescription = PayloadReader.GetString(payload, "gmDescription") ?? string.Empty;
        document.RuleSetIds = Equipment0183StringList(payload, "ruleSetIds");
        if (document.RuleSetIds.Count == 0)
        {
            var ruleSetId = PayloadReader.GetString(payload, "ruleSetId");
            if (!string.IsNullOrWhiteSpace(ruleSetId)) document.RuleSetIds.Add(ruleSetId.Trim());
        }
        if (document.RuleSetIds.Count == 0) document.RuleSetIds.Add(RuleSetIds.FantasyNriDefault);
        document.Tags = Equipment0183StringList(payload, "tags");
        document.VisibilityRule = FirstNonEmpty(
            PayloadReader.GetString(payload, "visibilityRule"),
            PayloadReader.GetBool(payload, "isPlayerVisible") ? VisibilityRuleIds.Public : VisibilityRuleIds.GmOnly);
        document.IsArchived = false;
        document.Archived = false;
        document.UpdatedAtUtc = now;
        document.UpdatedUtc = now;
        document.ExtraData = Magic0184BuildExtraData(family, payload, document.ExtraData);
        var revision = Equipment0183ExtraInt(document.ExtraData, "revision");
        document.ExtraData["revision"] = Math.Max(0, revision) + 1;
        document.ServerOnlyData ??= new Dictionary<string, object>();
        document.ServerOnlyData["lastAdminEditorUserId"] = actor.Id;
        document.ServerOnlyData["lastAdminEditorRequestId"] = context.Request.RequestId ?? string.Empty;

        var validation = Magic0184Validate(document, document.ExtraData, true);
        if (validation.Errors.Count > 0)
        {
            return Error(string.Join(" ", validation.Errors), ResponseStatus.Error, ErrorCode.ValidationFailed);
        }

        _mongo.UnifiedDefinitions.ReplaceOne(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, family)
            & Magic0184IdentityFilter(definitionId),
            document,
            new ReplaceOptions { IsUpsert = true });
        _logger.Admin($"definitions.magic.admin.save family={family} id={definitionId} create={isCreate} actor={actor.Id}");
        return Ok(isCreate ? "Определение создано." : "Определение сохранено.", new Dictionary<string, object>
        {
            ["item"] = Magic0184AdminView(document),
            ["warnings"] = validation.Warnings.Cast<object>().ToArray(),
            ["brokenReferences"] = Array.Empty<object>(),
            ["sourceOfTruth"] = "unified_definitions"
        });
    }

    public ResponseEnvelope MagicDefinitionsAdminClone(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var family = Magic0184Family(payload);
        var source = Magic0184Find(family, Magic0184DefinitionId(payload));
        if (source == null) return Error("Исходное определение не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var now = DateTime.UtcNow;
        var clone = new UnifiedDefinitionDocument
        {
            Id = Equipment0183GenerateId(family, source.Name + " copy"),
            Category = source.Category,
            Name = source.Name + " (копия)",
            PublicDescription = source.PublicDescription,
            GMDescription = source.GMDescription,
            RuleSetIds = new List<string>(source.RuleSetIds ?? new List<string>()),
            Tags = new List<string>(source.Tags ?? new List<string>()),
            VisibilityRule = source.VisibilityRule,
            IsArchived = false,
            Archived = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedUtc = now,
            UpdatedUtc = now,
            SourceDocument = $"clone:{source.Id}",
            ExtraData = Equipment0183CloneMap(source.ExtraData),
            ServerOnlyData = new Dictionary<string, object>
            {
                ["clonedByUserId"] = actor.Id,
                ["cloneSourceId"] = source.Id
            }
        };
        clone.ExtraData["revision"] = 1;
        _mongo.UnifiedDefinitions.InsertOne(clone);
        _logger.Admin($"definitions.magic.admin.clone family={family} source={source.Id} clone={clone.Id} actor={actor.Id}");
        return Ok("Копия определения создана.", new Dictionary<string, object>
        {
            ["item"] = Magic0184AdminView(clone),
            ["sourceOfTruth"] = "unified_definitions"
        });
    }

    public ResponseEnvelope MagicDefinitionsAdminSetArchived(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var family = Magic0184Family(payload);
        var definitionId = Magic0184DefinitionId(payload);
        var document = Magic0184Find(family, definitionId);
        if (document == null) return Error("Определение не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var archived = PayloadReader.GetBool(payload, "isArchived");
        document.IsArchived = archived;
        document.Archived = archived;
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.UpdatedUtc = document.UpdatedAtUtc;
        document.ExtraData ??= new Dictionary<string, object>();
        document.ExtraData["revision"] = Equipment0183ExtraInt(document.ExtraData, "revision") + 1;
        document.ServerOnlyData ??= new Dictionary<string, object>();
        document.ServerOnlyData[archived ? "archivedByUserId" : "restoredByUserId"] = actor.Id;
        _mongo.UnifiedDefinitions.ReplaceOne(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, family)
            & Magic0184IdentityFilter(definitionId),
            document);
        _logger.Admin($"definitions.magic.admin.archive family={family} id={definitionId} archived={archived} actor={actor.Id}");
        return Ok(archived ? "Определение архивировано." : "Определение восстановлено.", new Dictionary<string, object>
        {
            ["item"] = Magic0184AdminView(document),
            ["sourceOfTruth"] = "unified_definitions"
        });
    }

    public ResponseEnvelope MagicDefinitionsAdminReferences(CommandContext context)
    {
        RequireAdmin(context);
        var unifiedFamilies = MagicEffectConditionDefinitionFamilies.All.Concat(new[]
        {
            DefinitionCategoryIds.Attribute,
            DefinitionCategoryIds.SubAttribute,
            DefinitionCategoryIds.DerivedStat,
            DefinitionCategoryIds.DevelopmentNode,
            DefinitionCategoryIds.Resource,
            DefinitionCategoryIds.Item,
            DefinitionCategoryIds.Ammo,
            DefinitionCategoryIds.DamageType,
            DefinitionCategoryIds.Law,
            DefinitionCategoryIds.License
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var references = _mongo.UnifiedDefinitions.Find(
                Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, unifiedFamilies))
            .ToList()
            .Select(x => (object)Magic0184ReferenceView(x))
            .ToList();
        references.AddRange(_mongo.DefinitionSkills.Find(Builders<SkillDefinition>.Filter.Empty).ToList().Select(skill => (object)new Dictionary<string, object>
        {
            ["definitionId"] = FirstNonEmpty(skill.Code, skill.Id),
            ["family"] = DefinitionCategoryIds.Skill,
            ["displayName"] = FirstNonEmpty(skill.Name, skill.Code),
            ["summary"] = "Навык",
            ["isPlayerVisible"] = !skill.IsArchived && !skill.Archived,
            ["isArchived"] = skill.IsArchived || skill.Archived
        }));
        return Ok("Связанные справочники загружены.", new Dictionary<string, object>
        {
            ["references"] = references.OrderBy(x => Convert.ToString(((Dictionary<string, object>)x)["displayName"], CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase).ToArray(),
            ["sourceOfTruth"] = "unified_definitions + skill_definition_documents adapter"
        });
    }

    public ResponseEnvelope MagicDefinitionsPlayerList(CommandContext context)
    {
        GetCurrentAccount(context);
        var filter = Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, MagicEffectConditionDefinitionFamilies.All)
                     & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        var items = _mongo.UnifiedDefinitions.Find(filter).ToList()
            .Where(Magic0184PlayerVisible)
            .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => (object)Magic0184PlayerView(x))
            .ToArray();
        _logger.Admin($"definitions.magic.player.list visible={items.Length}");
        return Ok("Доступные определения магии и состояний загружены.", new Dictionary<string, object>
        {
            ["items"] = items,
            ["playerSafe"] = true
        });
    }

    public ResponseEnvelope MagicDefinitionsPlayerGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var document = Magic0184Find(Magic0184Family(payload), Magic0184DefinitionId(payload));
        if (document == null || !Magic0184PlayerVisible(document))
        {
            return Error("Определение недоступно.", ResponseStatus.NotFound, ErrorCode.NotFound);
        }
        return Ok("Доступное игроку определение загружено.", new Dictionary<string, object>
        {
            ["item"] = Magic0184PlayerView(document),
            ["playerSafe"] = true
        });
    }

    private UnifiedDefinitionDocument? Magic0184Find(string family, string definitionId)
    {
        if (!MagicEffectConditionDefinitionFamilies.IsSupported(family) || string.IsNullOrWhiteSpace(definitionId)) return null;
        return _mongo.UnifiedDefinitions.Find(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, family)
            & Magic0184IdentityFilter(definitionId.Trim())).FirstOrDefault();
    }

    private static FilterDefinition<UnifiedDefinitionDocument> Magic0184IdentityFilter(string definitionId)
        => Builders<UnifiedDefinitionDocument>.Filter.Or(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, definitionId),
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.StableKey, definitionId));

    private static string Magic0184CanonicalId(UnifiedDefinitionDocument document)
        => FirstNonEmpty(document.StableKey, document.Id);

    private static string Magic0184Family(IDictionary<string, object> payload)
        => FirstNonEmpty(PayloadReader.GetString(payload, "family"), PayloadReader.GetString(payload, "category")).Trim();

    private static string Magic0184DefinitionId(IDictionary<string, object> payload)
        => FirstNonEmpty(PayloadReader.GetString(payload, "definitionId"), PayloadReader.GetString(payload, "id")).Trim();

    private static string[] Magic0184RequestedFamilies(IDictionary<string, object> payload)
    {
        var family = Magic0184Family(payload);
        return MagicEffectConditionDefinitionFamilies.IsSupported(family)
            ? new[] { family }
            : MagicEffectConditionDefinitionFamilies.All;
    }

    private static Dictionary<string, object> Magic0184BuildExtraData(
        string family,
        IDictionary<string, object> payload,
        Dictionary<string, object>? existing)
    {
        var result = existing == null
            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(existing, StringComparer.OrdinalIgnoreCase);
        result["profileVersion"] = 1;
        result["profileFamily"] = family;
        foreach (var key in Magic0184StringKeys(family)) result[key] = PayloadReader.GetString(payload, key) ?? string.Empty;
        foreach (var key in Magic0184IntegerKeys(family)) result[key] = PayloadReader.GetInt(payload, key) ?? 0;
        foreach (var key in Magic0184DecimalKeys(family)) result[key] = Equipment0183Decimal(payload, key);
        foreach (var key in Magic0184BooleanKeys(family)) result[key] = PayloadReader.GetBool(payload, key);
        foreach (var key in Magic0184ListKeys(family)) result[key] = Equipment0183StringList(payload, key).Cast<object>().ToArray();
        if (Magic0184UsesResourceCosts(family)) result["resourceCosts"] = Magic0184ResourceCosts(payload).Cast<object>().ToArray();
        if (string.Equals(family, DefinitionCategoryIds.Ritual, StringComparison.OrdinalIgnoreCase))
        {
            result["stages"] = Magic0184Stages(payload).Cast<object>().ToArray();
        }
        return result;
    }

    private Magic0184ValidationResult Magic0184Validate(
        UnifiedDefinitionDocument document,
        Dictionary<string, object> extra,
        bool validateReferences)
    {
        var result = new Magic0184ValidationResult();
        if (string.IsNullOrWhiteSpace(document.Name)) result.Errors.Add("Название обязательно.");
        if (Magic0184PlayerVisibleByRule(document) && string.IsNullOrWhiteSpace(document.PublicDescription))
        {
            result.Errors.Add("Для видимой игрокам записи требуется публичное описание.");
        }
        foreach (var forbidden in new[] { "script", "rawScript", "executableCode", "codeToExecute" })
        {
            if (extra.Keys.Any(x => string.Equals(x, forbidden, StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(Equipment0183ExtraString(extra, forbidden)))
            {
                result.Errors.Add("Исполняемый raw script запрещён. Используйте typed effect или ручное разрешение GM.");
            }
        }

        var family = document.Category;
        foreach (var scope in Equipment0183ExtraList(extra, "allowedTargetScopes"))
        {
            if (!MagicTargetScopeIds.IsSupported(scope))
                result.Errors.Add("Выберите поддерживаемый тип магической цели.");
        }
        if (string.Equals(family, DefinitionCategoryIds.MagicMethod, StringComparison.OrdinalIgnoreCase))
        {
            Magic0184Require(extra, "methodCategory", "Укажите категорию магического метода.", result);
            Magic0184Require(extra, "resourceModel", "Укажите модель ресурса.", result);
            Magic0184Require(extra, "castingModel", "Укажите модель применения.", result);
        }
        else if (string.Equals(family, DefinitionCategoryIds.MagicDirection, StringComparison.OrdinalIgnoreCase))
        {
            Magic0184Require(extra, "directionKind", "Укажите вид магического направления.", result);
        }
        else if (string.Equals(family, DefinitionCategoryIds.Spell, StringComparison.OrdinalIgnoreCase))
        {
            Magic0184Require(extra, "spellCategory", "Укажите категорию заклинания.", result);
            Magic0184Require(extra, "targetModel", "Укажите модель цели.", result);
            if (Equipment0183ExtraInt(extra, "tier") < 0 || Equipment0183ExtraInt(extra, "actionCost") < 0)
                result.Errors.Add("Ранг и стоимость действия не могут быть отрицательными.");
            Magic0184RequireOutcome(extra, result);
        }
        else if (string.Equals(family, DefinitionCategoryIds.Seal, StringComparison.OrdinalIgnoreCase))
        {
            Magic0184Require(extra, "triggerType", "Укажите тип срабатывания печати.", result);
            if (Equipment0183ExtraInt(extra, "charges") < 0) result.Errors.Add("Число зарядов не может быть отрицательным.");
            Magic0184RequireOutcome(extra, result);
        }
        else if (string.Equals(family, DefinitionCategoryIds.ArcanaForm, StringComparison.OrdinalIgnoreCase))
        {
            Magic0184Require(extra, "formCategory", "Укажите категорию формы Арканы.", result);
            if (Magic0184ExtraDecimal(extra, "arcanaCost") < 0) result.Errors.Add("Стоимость Арканы не может быть отрицательной.");
            Magic0184RequireOutcome(extra, result);
        }
        else if (string.Equals(family, DefinitionCategoryIds.Ritual, StringComparison.OrdinalIgnoreCase))
        {
            Magic0184Require(extra, "ritualCategory", "Укажите категорию ритуала.", result);
            if (Equipment0183ExtraInt(extra, "requiredParticipants") < 1) result.Errors.Add("Для ритуала требуется хотя бы один участник.");
            Magic0184RequireOutcome(extra, result);
        }
        else if (string.Equals(family, DefinitionCategoryIds.Effect, StringComparison.OrdinalIgnoreCase))
        {
            var kind = Equipment0183ExtraString(extra, "effectKind");
            var timing = Equipment0183ExtraString(extra, "timing");
            if (!Magic0184EffectKinds.Contains(kind, StringComparer.OrdinalIgnoreCase)) result.Errors.Add("Выберите поддерживаемый тип эффекта.");
            if (!Magic0184EffectTimings.Contains(timing, StringComparer.OrdinalIgnoreCase)) result.Errors.Add("Выберите поддерживаемый момент эффекта.");
            Magic0184Require(extra, "targetSelector", "Укажите выбор цели эффекта.", result);
            Magic0184Require(extra, "operation", "Укажите typed operation эффекта.", result);
        }
        else if (string.Equals(family, DefinitionCategoryIds.Condition, StringComparison.OrdinalIgnoreCase))
        {
            Magic0184Require(extra, "conditionCategory", "Укажите категорию состояния.", result);
            Magic0184Require(extra, "durationModel", "Укажите модель длительности.", result);
            Magic0184Require(extra, "stackingModel", "Укажите модель сложения.", result);
            if (Equipment0183ExtraInt(extra, "maximumStacks") < 1) result.Errors.Add("Максимум стаков должен быть не меньше 1.");
            var stackingModel = Equipment0183ExtraString(extra, "stackingModel");
            if (!new[] { "none", "replace", "refresh", "stack", "highest", "custom_manual" }
                    .Contains(stackingModel, StringComparer.OrdinalIgnoreCase))
            {
                result.Errors.Add("Выберите поддерживаемую модель сложения состояния.");
            }
        }

        foreach (var cost in Equipment0183MapList(extra.TryGetValue("resourceCosts", out var costs) ? costs : null))
        {
            if (Magic0184MapDecimal(cost, "amount") < 0) result.Errors.Add("Стоимость ресурса не может быть отрицательной.");
        }
        foreach (var durationKey in new[] { "duration", "interval", "defaultDuration", "preparationTime", "channelTime", "executionDuration", "resultDuration" })
        {
            var duration = Equipment0183ExtraString(extra, durationKey);
            if (Magic0184IsNegativeNumber(duration)) result.Errors.Add($"Поле «{durationKey}» не может содержать отрицательную длительность.");
        }
        foreach (var stage in Equipment0183MapList(extra.TryGetValue("stages", out var stages) ? stages : null))
        {
            if (Magic0184IsNegativeNumber(Equipment0183MapString(stage, "duration")))
                result.Errors.Add("Длительность этапа ритуала не может быть отрицательной.");
        }

        if (validateReferences) Magic0184ValidateReferences(document, extra, result);
        if ((string.Equals(family, DefinitionCategoryIds.Effect, StringComparison.OrdinalIgnoreCase)
             || string.Equals(family, DefinitionCategoryIds.Condition, StringComparison.OrdinalIgnoreCase))
            && Magic0184HasApplicationCycle(document, extra))
        {
            result.Errors.Add("Обнаружен цикл повторного применения Effect → Condition → Effect.");
        }
        return result;
    }

    private static bool Magic0184IsNegativeNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var token = value.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant)
                || decimal.TryParse(token, NumberStyles.Number, CultureInfo.GetCultureInfo("ru-RU"), out invariant))
               && invariant < 0;
    }

    private void Magic0184ValidateReferences(
        UnifiedDefinitionDocument source,
        Dictionary<string, object> extra,
        Magic0184ValidationResult result)
    {
        var references = Magic0184References(source.Category, extra);
        foreach (var reference in references.Distinct(new Magic0184ReferenceTupleComparer()))
        {
            if (string.IsNullOrWhiteSpace(reference.Item2)) continue;
            if (string.Equals(reference.Item1, DefinitionCategoryIds.Skill, StringComparison.OrdinalIgnoreCase))
            {
                var skill = _mongo.DefinitionSkills.Find(
                    Builders<SkillDefinition>.Filter.Eq(x => x.Code, reference.Item2)
                    | Builders<SkillDefinition>.Filter.Eq(x => x.Id, reference.Item2)).FirstOrDefault();
                if (skill == null || skill.IsArchived || skill.Archived) result.Errors.Add($"Связанный навык «{reference.Item2}» не найден.");
                continue;
            }
            if (string.Equals(reference.Item1, DefinitionCategoryIds.DevelopmentNode, StringComparison.OrdinalIgnoreCase))
            {
                var contentNode = _mongo.ContentDefinitionRecords.Find(
                    Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, "development_node_definition")
                    & (Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, reference.Item2)
                       | Builders<ContentDefinitionRecord>.Filter.Eq(x => x.StableKey, reference.Item2)
                       | Builders<ContentDefinitionRecord>.Filter.Eq(x => x.ShortCode, reference.Item2))).FirstOrDefault();
                if (contentNode == null || contentNode.IsArchived)
                {
                    result.Errors.Add($"Связанная запись «{reference.Item2}» ({reference.Item1}) не найдена или архивирована.");
                    continue;
                }
                if (Magic0184PlayerVisibleByRule(source)
                    && !string.Equals(contentNode.VisibilityRule, ContentDefinitionVisibilityRules.PlayerVisible, StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"Видимая игрокам запись не может ссылаться на скрытое определение «{contentNode.DisplayName}».");
                }
                continue;
            }
            var target = _mongo.UnifiedDefinitions.Find(
                Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, reference.Item1)
                & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, reference.Item2)).FirstOrDefault();
            if (target == null || target.IsArchived)
            {
                result.Errors.Add($"Связанная запись «{reference.Item2}» ({reference.Item1}) не найдена или архивирована.");
                continue;
            }
            if (Magic0184PlayerVisibleByRule(source) && !Magic0184PlayerVisible(target))
            {
                result.Errors.Add($"Видимая игрокам запись не может ссылаться на скрытое определение «{target.Name}».");
            }
        }

        if (new[] { DefinitionCategoryIds.Spell, DefinitionCategoryIds.Seal, DefinitionCategoryIds.Ritual }
            .Contains(source.Category, StringComparer.OrdinalIgnoreCase))
        {
            var methodIds = Equipment0183ExtraList(extra, "magicMethodIds");
            foreach (var directionId in Equipment0183ExtraList(extra, "magicDirectionIds"))
            {
                var direction = Magic0184Find(DefinitionCategoryIds.MagicDirection, directionId);
                var compatibleMethods = Equipment0183ExtraList(direction?.ExtraData, "compatibleMethodIds");
                if (compatibleMethods.Count > 0 && methodIds.Count > 0 && !methodIds.Any(x => compatibleMethods.Contains(x, StringComparer.OrdinalIgnoreCase)))
                {
                    result.Errors.Add($"Направление «{direction?.Name ?? directionId}» несовместимо с выбранным методом.");
                }
            }
        }
    }

    private static List<Tuple<string, string>> Magic0184References(string family, Dictionary<string, object> extra)
    {
        var result = new List<Tuple<string, string>>();
        void Add(string targetFamily, IEnumerable<string> ids)
        {
            result.AddRange(ids.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Tuple.Create(targetFamily, x)));
        }
        void AddOne(string targetFamily, string id)
        {
            if (!string.IsNullOrWhiteSpace(id)) result.Add(Tuple.Create(targetFamily, id));
        }
        Add(DefinitionCategoryIds.MagicMethod, Equipment0183ExtraList(extra, "magicMethodIds"));
        Add(DefinitionCategoryIds.MagicMethod, Equipment0183ExtraList(extra, "compatibleMethodIds"));
        Add(DefinitionCategoryIds.MagicDirection, Equipment0183ExtraList(extra, "magicDirectionIds"));
        Add(DefinitionCategoryIds.MagicDirection, Equipment0183ExtraList(extra, "compatibleDirectionIds"));
        Add(DefinitionCategoryIds.MagicDirection, Equipment0183ExtraList(extra, "parentDirectionIds"));
        Add(DefinitionCategoryIds.MagicDirection, Equipment0183ExtraList(extra, "relatedDirectionIds"));
        Add(DefinitionCategoryIds.MagicDirection, Equipment0183ExtraList(extra, "opposedDirectionIds"));
        Add(DefinitionCategoryIds.Effect, Equipment0183ExtraList(extra, "effectDefinitionIds"));
        Add(DefinitionCategoryIds.Effect, Equipment0183ExtraList(extra, "effectsOnApplyIds"));
        Add(DefinitionCategoryIds.Effect, Equipment0183ExtraList(extra, "periodicEffectIds"));
        Add(DefinitionCategoryIds.Effect, Equipment0183ExtraList(extra, "effectsOnRemoveIds"));
        Add(DefinitionCategoryIds.Condition, Equipment0183ExtraList(extra, "conditionDefinitionIds"));
        Add(DefinitionCategoryIds.DamageType, Equipment0183ExtraList(extra, "damageTypeDefinitionIds"));
        Add(DefinitionCategoryIds.Skill, Equipment0183ExtraList(extra, "primarySkillIds"));
        Add(DefinitionCategoryIds.Skill, Equipment0183ExtraList(extra, "requiredSkillIds"));
        Add(DefinitionCategoryIds.Attribute, Equipment0183ExtraList(extra, "allowedAttributeIds"));
        Add(DefinitionCategoryIds.SubAttribute, Equipment0183ExtraList(extra, "allowedSubAttributeIds"));
        Add(DefinitionCategoryIds.Resource, Equipment0183ExtraList(extra, "resourceDefinitionIds"));
        Add(DefinitionCategoryIds.Resource, Equipment0183ExtraList(extra, "materialResourceIds"));
        Add(DefinitionCategoryIds.Item, Equipment0183ExtraList(extra, "materialItemIds"));
        Add(DefinitionCategoryIds.DevelopmentNode, Equipment0183ExtraList(extra, "developmentNodeIds"));
        AddOne(DefinitionCategoryIds.DamageType, Equipment0183ExtraString(extra, "damageTypeDefinitionId"));
        AddOne(DefinitionCategoryIds.Resource, Equipment0183ExtraString(extra, "resourceDefinitionId"));
        AddOne(DefinitionCategoryIds.DerivedStat, Equipment0183ExtraString(extra, "derivedStatDefinitionId"));
        AddOne(DefinitionCategoryIds.Attribute, Equipment0183ExtraString(extra, "attributeDefinitionId"));
        AddOne(DefinitionCategoryIds.SubAttribute, Equipment0183ExtraString(extra, "subAttributeDefinitionId"));
        AddOne(DefinitionCategoryIds.Skill, Equipment0183ExtraString(extra, "skillDefinitionId"));
        AddOne(DefinitionCategoryIds.Condition, Equipment0183ExtraString(extra, "conditionDefinitionId"));
        foreach (var cost in Equipment0183MapList(extra.TryGetValue("resourceCosts", out var costs) ? costs : null))
        {
            AddOne(DefinitionCategoryIds.Resource, Equipment0183MapString(cost, "resourceDefinitionId"));
        }
        return result;
    }

    private bool Magic0184HasApplicationCycle(UnifiedDefinitionDocument candidate, Dictionary<string, object> candidateExtra)
    {
        var docs = _mongo.UnifiedDefinitions.Find(
            Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, new[] { DefinitionCategoryIds.Effect, DefinitionCategoryIds.Condition })
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false)).ToList();
        docs.RemoveAll(x => string.Equals(x.Category, candidate.Category, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(x.Id, candidate.Id, StringComparison.OrdinalIgnoreCase));
        docs.Add(candidate);
        var edges = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in docs)
        {
            var key = Magic0184GraphKey(document.Category, document.Id);
            var targets = new List<string>();
            var extra = ReferenceEquals(document, candidate) ? candidateExtra : document.ExtraData;
            if (string.Equals(document.Category, DefinitionCategoryIds.Effect, StringComparison.OrdinalIgnoreCase))
            {
                var conditionId = Equipment0183ExtraString(extra, "conditionDefinitionId");
                if (!string.IsNullOrWhiteSpace(conditionId)) targets.Add(Magic0184GraphKey(DefinitionCategoryIds.Condition, conditionId));
            }
            else
            {
                foreach (var effectId in Equipment0183ExtraList(extra, "effectsOnApplyIds")
                    .Concat(Equipment0183ExtraList(extra, "periodicEffectIds"))
                    .Concat(Equipment0183ExtraList(extra, "effectsOnRemoveIds"))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    targets.Add(Magic0184GraphKey(DefinitionCategoryIds.Effect, effectId));
                }
            }
            edges[key] = targets;
        }
        var start = Magic0184GraphKey(candidate.Category, candidate.Id);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
        bool ReachesStart(string current, int depth)
        {
            if (depth > 64) return true;
            if (!edges.TryGetValue(current, out var targets)) return false;
            foreach (var target in targets)
            {
                if (string.Equals(target, start, StringComparison.OrdinalIgnoreCase)) return true;
                if (visited.Add(target) && ReachesStart(target, depth + 1)) return true;
            }
            return false;
        }
        return ReachesStart(start, 0);
    }

    private static string Magic0184GraphKey(string family, string id) => $"{family}:{id}";

    private static Dictionary<string, object> Magic0184AdminView(UnifiedDefinitionDocument document)
    {
        var result = Magic0184CommonView(document, true);
        result["gmDescription"] = document.GMDescription ?? string.Empty;
        result["visibilityRule"] = document.VisibilityRule ?? string.Empty;
        result["isArchived"] = document.IsArchived;
        result["createdAtUtc"] = document.CreatedAtUtc;
        result["updatedAtUtc"] = document.UpdatedAtUtc;
        foreach (var pair in document.ExtraData ?? new Dictionary<string, object>()) result[pair.Key] = pair.Value ?? string.Empty;
        return result;
    }

    private Dictionary<string, object> Magic0184PlayerView(UnifiedDefinitionDocument document)
    {
        var result = Magic0184CommonView(document, false);
        var extra = document.ExtraData ?? new Dictionary<string, object>();
        var facts = new List<object>();
        void Fact(string label, string key)
        {
            var value = Equipment0183ExtraDisplay(extra, key);
            if (!string.IsNullOrWhiteSpace(value)) facts.Add(new Dictionary<string, object> { ["label"] = label, ["value"] = value });
        }
        void Refs(string label, string family, string key)
        {
            var names = Equipment0183ReferenceNames(family, Equipment0183ExtraList(extra, key));
            if (names.Count > 0) facts.Add(new Dictionary<string, object> { ["label"] = label, ["value"] = string.Join(", ", names) });
        }
        switch (document.Category)
        {
            case DefinitionCategoryIds.MagicMethod:
                Fact("Категория", "methodCategory"); Fact("Ресурс", "resourceModel"); Fact("Подготовка", "preparationModel"); Fact("Применение", "castingModel");
                Refs("Навыки", DefinitionCategoryIds.Skill, "primarySkillIds"); Refs("Направления", DefinitionCategoryIds.MagicDirection, "compatibleDirectionIds");
                break;
            case DefinitionCategoryIds.MagicDirection:
                Fact("Вид направления", "directionKind"); Fact("Редкость", "rarity");
                Refs("Методы", DefinitionCategoryIds.MagicMethod, "compatibleMethodIds"); Refs("Типы урона", DefinitionCategoryIds.DamageType, "damageTypeDefinitionIds");
                break;
            case DefinitionCategoryIds.Spell:
                Fact("Категория", "spellCategory"); Fact("Ранг", "tier"); Fact("Применение", "castingTime"); Fact("Стоимость действия", "actionCost");
                Fact("Дальность", "range"); Fact("Цель", "targetModel"); Fact("Область", "area"); Fact("Длительность", "duration");
                Refs("Методы", DefinitionCategoryIds.MagicMethod, "magicMethodIds"); Refs("Направления", DefinitionCategoryIds.MagicDirection, "magicDirectionIds");
                Refs("Эффекты", DefinitionCategoryIds.Effect, "effectDefinitionIds"); Refs("Состояния", DefinitionCategoryIds.Condition, "conditionDefinitionIds");
                Magic0184PlayerResourceCosts(facts, extra);
                break;
            case DefinitionCategoryIds.Seal:
                Fact("Подготовка", "preparationTime"); Fact("Триггер", "triggerType"); Fact("Цель", "targetModel"); Fact("Область", "area"); Fact("Сохранение", "persistence"); Fact("Заряды", "charges");
                Refs("Эффекты", DefinitionCategoryIds.Effect, "effectDefinitionIds"); Refs("Состояния", DefinitionCategoryIds.Condition, "conditionDefinitionIds");
                Magic0184PlayerResourceCosts(facts, extra);
                break;
            case DefinitionCategoryIds.ArcanaForm:
                Fact("Категория формы", "formCategory"); Fact("Стоимость Арканы", "arcanaCost"); Fact("Канал", "channelTime"); Fact("Стабильность", "stability"); Fact("Риск", "risk");
                Refs("Направления", DefinitionCategoryIds.MagicDirection, "compatibleDirectionIds"); Refs("Эффекты", DefinitionCategoryIds.Effect, "effectDefinitionIds");
                break;
            case DefinitionCategoryIds.Ritual:
                Fact("Категория", "ritualCategory"); Fact("Участники", "requiredParticipants"); Fact("Подготовка", "preparationTime"); Fact("Проведение", "executionDuration"); Fact("Результат", "resultDuration");
                Refs("Методы", DefinitionCategoryIds.MagicMethod, "magicMethodIds"); Refs("Направления", DefinitionCategoryIds.MagicDirection, "magicDirectionIds");
                Refs("Эффекты", DefinitionCategoryIds.Effect, "effectDefinitionIds"); Refs("Состояния", DefinitionCategoryIds.Condition, "conditionDefinitionIds");
                Magic0184PlayerResourceCosts(facts, extra);
                break;
            case DefinitionCategoryIds.Effect:
                Fact("Тип", "effectKind"); Fact("Цель", "targetSelector"); Fact("Момент", "timing"); Fact("Операция", "operation"); Fact("Значение", "valueExpression"); Fact("Длительность", "duration");
                break;
            case DefinitionCategoryIds.Condition:
                Fact("Категория", "conditionCategory"); Fact("Тяжесть", "severity"); Fact("Длительность", "durationModel"); Fact("По умолчанию", "defaultDuration"); Fact("Сложение", "stackingModel"); Fact("Максимум", "maximumStacks");
                Refs("При применении", DefinitionCategoryIds.Effect, "effectsOnApplyIds"); Refs("Периодически", DefinitionCategoryIds.Effect, "periodicEffectIds"); Refs("При снятии", DefinitionCategoryIds.Effect, "effectsOnRemoveIds");
                break;
        }
        Magic0184PlayerTargetScopes(facts, extra);
        result["playerFacts"] = facts.ToArray();
        return result;
    }

    private void Magic0184PlayerResourceCosts(List<object> facts, Dictionary<string, object> extra)
    {
        var values = new List<string>();
        foreach (var cost in Equipment0183MapList(extra.TryGetValue("resourceCosts", out var raw) ? raw : null))
        {
            var name = Equipment0183ReferenceName(DefinitionCategoryIds.Resource, Equipment0183MapString(cost, "resourceDefinitionId"));
            if (!string.IsNullOrWhiteSpace(name)) values.Add($"{name}: {Magic0184MapDecimal(cost, "amount"):0.##}");
        }
        if (values.Count > 0) facts.Add(new Dictionary<string, object> { ["label"] = "Стоимость ресурсов", ["value"] = string.Join(", ", values) });
    }

    private static void Magic0184PlayerTargetScopes(List<object> facts, Dictionary<string, object> extra)
    {
        var scopes = Equipment0183ExtraList(extra, "allowedTargetScopes");
        if (scopes.Count == 0) return;
        facts.Add(new Dictionary<string, object>
        {
            ["label"] = "Допустимые цели",
            ["value"] = string.Join(", ", scopes.Select(MagicTargetScopeDisplay02112))
        });
    }

    private static Dictionary<string, object> Magic0184CommonView(UnifiedDefinitionDocument document, bool includeInternalId)
    {
        var result = new Dictionary<string, object>
        {
            ["family"] = document.Category ?? string.Empty,
            ["category"] = document.Category ?? string.Empty,
            ["name"] = document.Name ?? string.Empty,
            ["displayName"] = document.Name ?? string.Empty,
            ["publicDescription"] = document.PublicDescription ?? string.Empty,
            ["tags"] = (document.Tags ?? new List<string>()).Cast<object>().ToArray(),
            ["isPlayerVisible"] = Magic0184PlayerVisible(document)
        };
        if (includeInternalId)
        {
            result["definitionId"] = Magic0184CanonicalId(document);
            result["ruleSetIds"] = (document.RuleSetIds ?? new List<string>()).Cast<object>().ToArray();
        }
        return result;
    }

    private static Dictionary<string, object> Magic0184ReferenceView(UnifiedDefinitionDocument document)
    {
        return new Dictionary<string, object>
        {
            ["definitionId"] = Magic0184CanonicalId(document),
            ["family"] = document.Category ?? string.Empty,
            ["displayName"] = FirstNonEmpty(document.Name, Magic0184CanonicalId(document)),
            ["summary"] = Magic0184FamilyLabel(document.Category),
            ["isPlayerVisible"] = Magic0184PlayerVisible(document),
            ["isArchived"] = document.IsArchived
        };
    }

    private static bool Magic0184PlayerVisibleByRule(UnifiedDefinitionDocument document)
        => string.Equals(document.VisibilityRule, VisibilityRuleIds.Public, StringComparison.OrdinalIgnoreCase)
           || string.Equals(document.VisibilityRule, VisibilityRuleIds.PlayerVisible, StringComparison.OrdinalIgnoreCase);

    private static bool Magic0184PlayerVisible(UnifiedDefinitionDocument document)
    {
        if (document == null || document.IsArchived || !Magic0184PlayerVisibleByRule(document)) return false;
        return !string.Equals(document.Category, DefinitionCategoryIds.Condition, StringComparison.OrdinalIgnoreCase)
               || !Equipment0183MapBool(document.ExtraData ?? new Dictionary<string, object>(), "isHiddenState");
    }

    private static List<Dictionary<string, object>> Magic0184ResourceCosts(IDictionary<string, object> payload)
    {
        return Equipment0183MapList(payload.TryGetValue("resourceCosts", out var raw) ? raw : null)
            .Select(cost => new Dictionary<string, object>
            {
                ["resourceDefinitionId"] = Equipment0183MapString(cost, "resourceDefinitionId"),
                ["amount"] = Magic0184MapDecimal(cost, "amount"),
                ["requirement"] = Equipment0183MapString(cost, "requirement")
            }).ToList();
    }

    private static List<Dictionary<string, object>> Magic0184Stages(IDictionary<string, object> payload)
    {
        return Equipment0183MapList(payload.TryGetValue("stages", out var raw) ? raw : null)
            .Select(stage => new Dictionary<string, object>
            {
                ["name"] = Equipment0183MapString(stage, "name"),
                ["duration"] = Equipment0183MapString(stage, "duration"),
                ["requirements"] = Equipment0183MapString(stage, "requirements")
            }).Where(stage => !string.IsNullOrWhiteSpace(Convert.ToString(stage["name"], CultureInfo.InvariantCulture))).ToList();
    }

    private static decimal Magic0184MapDecimal(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || raw == null) return 0m;
        return decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }

    private static decimal Magic0184ExtraDecimal(IDictionary<string, object> map, string key)
        => Magic0184MapDecimal(map, key);

    private static bool Magic0184UsesResourceCosts(string family)
        => new[] { DefinitionCategoryIds.Spell, DefinitionCategoryIds.Seal, DefinitionCategoryIds.Ritual }
            .Contains(family, StringComparer.OrdinalIgnoreCase);

    private static void Magic0184Require(Dictionary<string, object> extra, string key, string message, Magic0184ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(Equipment0183ExtraDisplay(extra, key))) result.Errors.Add(message);
    }

    private static void Magic0184RequireOutcome(Dictionary<string, object> extra, Magic0184ValidationResult result)
    {
        if (Equipment0183ExtraList(extra, "effectDefinitionIds").Count == 0
            && Equipment0183ExtraList(extra, "conditionDefinitionIds").Count == 0)
        {
            result.Errors.Add("Добавьте хотя бы один эффект или состояние результата.");
        }
    }

    private static string Magic0184FamilyLabel(string family)
    {
        if (string.Equals(family, DefinitionCategoryIds.MagicMethod, StringComparison.OrdinalIgnoreCase)) return "Магический метод";
        if (string.Equals(family, DefinitionCategoryIds.MagicDirection, StringComparison.OrdinalIgnoreCase)) return "Магическое направление";
        if (string.Equals(family, DefinitionCategoryIds.Spell, StringComparison.OrdinalIgnoreCase)) return "Заклинание";
        if (string.Equals(family, DefinitionCategoryIds.Seal, StringComparison.OrdinalIgnoreCase)) return "Печать";
        if (string.Equals(family, DefinitionCategoryIds.ArcanaForm, StringComparison.OrdinalIgnoreCase)) return "Форма Арканы";
        if (string.Equals(family, DefinitionCategoryIds.Ritual, StringComparison.OrdinalIgnoreCase)) return "Ритуал";
        if (string.Equals(family, DefinitionCategoryIds.Effect, StringComparison.OrdinalIgnoreCase)) return "Эффект";
        if (string.Equals(family, DefinitionCategoryIds.Condition, StringComparison.OrdinalIgnoreCase)) return "Состояние";
        return family;
    }

    private static string[] Magic0184StringKeys(string family)
    {
        if (string.Equals(family, DefinitionCategoryIds.MagicMethod, StringComparison.OrdinalIgnoreCase))
            return new[] { "methodCategory", "resourceModel", "preparationModel", "castingModel", "defaultRiskProfile", "legality" };
        if (string.Equals(family, DefinitionCategoryIds.MagicDirection, StringComparison.OrdinalIgnoreCase))
            return new[] { "directionKind", "legality", "rarity" };
        if (string.Equals(family, DefinitionCategoryIds.Spell, StringComparison.OrdinalIgnoreCase))
            return new[] { "spellCategory", "checkType", "rollProfile", "castingTime", "preparationRequirements", "range", "targetModel", "area", "duration", "failureMetadata", "riskMetadata", "legality", "license" };
        if (string.Equals(family, DefinitionCategoryIds.Seal, StringComparison.OrdinalIgnoreCase))
            return new[] { "preparationTime", "triggerType", "activationRequirements", "targetModel", "area", "persistence", "interruptionRules", "destructionRules", "legality" };
        if (string.Equals(family, DefinitionCategoryIds.ArcanaForm, StringComparison.OrdinalIgnoreCase))
            return new[] { "formCategory", "channelTime", "overload", "stability", "risk", "targetModel", "area", "requirements", "legality" };
        if (string.Equals(family, DefinitionCategoryIds.Ritual, StringComparison.OrdinalIgnoreCase))
            return new[] { "ritualCategory", "preparationTime", "executionDuration", "locationRequirements", "interruptionRules", "failureConsequences", "resultDuration", "legality" };
        if (string.Equals(family, DefinitionCategoryIds.Effect, StringComparison.OrdinalIgnoreCase))
            return new[] { "effectKind", "targetSelector", "timing", "operation", "valueExpression", "damageTypeDefinitionId", "resourceDefinitionId", "derivedStatDefinitionId", "attributeDefinitionId", "subAttributeDefinitionId", "skillDefinitionId", "conditionDefinitionId", "duration", "interval", "stackingBehavior", "sourceRestrictions", "manualResolution" };
        if (string.Equals(family, DefinitionCategoryIds.Condition, StringComparison.OrdinalIgnoreCase))
            return new[] { "conditionCategory", "severity", "durationModel", "defaultDuration", "stackingModel", "refreshReplaceRules", "dispelRemovalRules", "iconKey" };
        return Array.Empty<string>();
    }

    private static string[] Magic0184IntegerKeys(string family)
    {
        if (string.Equals(family, DefinitionCategoryIds.Spell, StringComparison.OrdinalIgnoreCase)) return new[] { "tier", "actionCost" };
        if (string.Equals(family, DefinitionCategoryIds.Seal, StringComparison.OrdinalIgnoreCase)) return new[] { "charges" };
        if (string.Equals(family, DefinitionCategoryIds.Ritual, StringComparison.OrdinalIgnoreCase)) return new[] { "requiredParticipants" };
        if (string.Equals(family, DefinitionCategoryIds.Condition, StringComparison.OrdinalIgnoreCase)) return new[] { "maximumStacks" };
        return Array.Empty<string>();
    }

    private static string[] Magic0184DecimalKeys(string family)
        => string.Equals(family, DefinitionCategoryIds.ArcanaForm, StringComparison.OrdinalIgnoreCase) ? new[] { "arcanaCost" } : Array.Empty<string>();

    private static string[] Magic0184BooleanKeys(string family)
    {
        if (string.Equals(family, DefinitionCategoryIds.Spell, StringComparison.OrdinalIgnoreCase)) return new[] { "requiresConcentration", "requiresChanneling", "isInterruptible" };
        if (string.Equals(family, DefinitionCategoryIds.Condition, StringComparison.OrdinalIgnoreCase)) return new[] { "isHiddenState" };
        return Array.Empty<string>();
    }

    private static string[] Magic0184ListKeys(string family)
    {
        if (string.Equals(family, DefinitionCategoryIds.MagicMethod, StringComparison.OrdinalIgnoreCase))
            return new[] { "primarySkillIds", "allowedAttributeIds", "allowedSubAttributeIds", "compatibleDirectionIds", "resourceDefinitionIds", "developmentNodeIds", "allowedTargetScopes" };
        if (string.Equals(family, DefinitionCategoryIds.MagicDirection, StringComparison.OrdinalIgnoreCase))
            return new[] { "parentDirectionIds", "relatedDirectionIds", "opposedDirectionIds", "compatibleMethodIds", "damageTypeDefinitionIds", "effectTags" };
        if (string.Equals(family, DefinitionCategoryIds.Spell, StringComparison.OrdinalIgnoreCase))
            return new[] { "magicMethodIds", "magicDirectionIds", "requiredSkillIds", "allowedAttributeIds", "allowedSubAttributeIds", "materialItemIds", "materialResourceIds", "effectDefinitionIds", "conditionDefinitionIds", "damageTypeDefinitionIds", "developmentNodeIds", "allowedTargetScopes" };
        if (string.Equals(family, DefinitionCategoryIds.Seal, StringComparison.OrdinalIgnoreCase))
            return new[] { "magicMethodIds", "magicDirectionIds", "materialItemIds", "materialResourceIds", "effectDefinitionIds", "conditionDefinitionIds", "allowedTargetScopes" };
        if (string.Equals(family, DefinitionCategoryIds.ArcanaForm, StringComparison.OrdinalIgnoreCase))
            return new[] { "compatibleDirectionIds", "effectDefinitionIds", "conditionDefinitionIds", "allowedTargetScopes" };
        if (string.Equals(family, DefinitionCategoryIds.Ritual, StringComparison.OrdinalIgnoreCase))
            return new[] { "magicMethodIds", "magicDirectionIds", "participantRoles", "materialItemIds", "materialResourceIds", "effectDefinitionIds", "conditionDefinitionIds", "allowedTargetScopes" };
        if (string.Equals(family, DefinitionCategoryIds.Condition, StringComparison.OrdinalIgnoreCase))
            return new[] { "immunityTags", "resistanceTags", "effectsOnApplyIds", "periodicEffectIds", "effectsOnRemoveIds" };
        return Array.Empty<string>();
    }

    private static readonly string[] Magic0184EffectKinds =
    {
        "damage", "healing", "resource_change", "modifier", "grant_action", "revoke_action",
        "apply_condition", "remove_condition", "resistance", "vulnerability", "movement_control", "custom_manual"
    };

    private static readonly string[] Magic0184EffectTimings =
    {
        "immediate", "on_apply", "periodic", "on_remove", "reaction"
    };

    private sealed class Magic0184ValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    private sealed class Magic0184ReferenceTupleComparer : IEqualityComparer<Tuple<string, string>>
    {
        public bool Equals(Tuple<string, string>? x, Tuple<string, string>? y)
            => x != null && y != null
               && string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(Tuple<string, string> obj)
            => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1 ?? string.Empty) * 397
               ^ StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2 ?? string.Empty);
    }
}
