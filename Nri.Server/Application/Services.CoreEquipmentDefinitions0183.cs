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
    public ResponseEnvelope CoreEquipmentAdminList(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var families = Equipment0183RequestedFamilies(payload);
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
            items = items.Where(x => Equipment0183Contains(x.Id, search)
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

        var views = items
            .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => (object)Equipment0183AdminView(x))
            .ToArray();
        _logger.Admin($"definitions.equipment.admin.list count={views.Length}");
        return Ok("Core equipment definitions loaded.", new Dictionary<string, object>
        {
            ["items"] = views,
            ["sourceOfTruth"] = "unified_definitions",
            ["families"] = families.Cast<object>().ToArray()
        });
    }

    public ResponseEnvelope CoreEquipmentAdminGet(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var family = Equipment0183Family(payload);
        var definitionId = Equipment0183DefinitionId(payload);
        var document = Equipment0183Find(family, definitionId);
        if (document == null) return Error("Определение снаряжения не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var validation = Equipment0183Validate(document, document.ExtraData, validateReferences: true);
        return Ok("Core equipment definition loaded.", new Dictionary<string, object>
        {
            ["item"] = Equipment0183AdminView(document),
            ["brokenReferences"] = validation.Errors.Cast<object>().ToArray(),
            ["warnings"] = validation.Warnings.Cast<object>().ToArray(),
            ["sourceOfTruth"] = "unified_definitions"
        });
    }

    public ResponseEnvelope CoreEquipmentAdminSave(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var family = Equipment0183Family(payload);
        if (!CoreEquipmentDefinitionFamilies.IsSupported(family))
        {
            return Error("Выберите поддерживаемое семейство определения.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        }

        var name = FirstNonEmpty(PayloadReader.GetString(payload, "name"), PayloadReader.GetString(payload, "displayName")).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error("Название обязательно.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        }

        var definitionId = Equipment0183DefinitionId(payload);
        var isCreate = string.IsNullOrWhiteSpace(definitionId) || PayloadReader.GetBool(payload, "isCreate");
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            definitionId = Equipment0183GenerateId(family, name);
        }

        var existing = Equipment0183Find(family, definitionId);
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
            SourceDocument = "foundation_0_18_3_typed_editor"
        };
        document.Id = definitionId;
        document.Category = family;
        document.Name = name;
        document.PublicDescription = PayloadReader.GetString(payload, "publicDescription")
                                     ?? PayloadReader.GetString(payload, "description")
                                     ?? string.Empty;
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
        document.ExtraData = Equipment0183BuildExtraData(family, payload, document.ExtraData);
        document.ServerOnlyData ??= new Dictionary<string, object>();
        document.ServerOnlyData["lastAdminEditorUserId"] = actor.Id;
        document.ServerOnlyData["lastAdminEditorRequestId"] = context.Request.RequestId ?? string.Empty;

        var validation = Equipment0183Validate(document, document.ExtraData, validateReferences: true);
        if (validation.Errors.Count > 0)
        {
            return Error(string.Join(" ", validation.Errors), ResponseStatus.Error, ErrorCode.ValidationFailed);
        }

        _mongo.UnifiedDefinitions.ReplaceOne(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, family)
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, definitionId),
            document,
            new ReplaceOptions { IsUpsert = true });

        _logger.Admin($"definitions.equipment.admin.save family={family} id={definitionId} create={isCreate} actor={actor.Id}");
        return Ok(isCreate ? "Определение создано." : "Определение сохранено.", new Dictionary<string, object>
        {
            ["item"] = Equipment0183AdminView(document),
            ["warnings"] = validation.Warnings.Cast<object>().ToArray(),
            ["brokenReferences"] = Array.Empty<object>(),
            ["sourceOfTruth"] = "unified_definitions"
        });
    }

    public ResponseEnvelope CoreEquipmentAdminClone(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var family = Equipment0183Family(payload);
        var sourceId = Equipment0183DefinitionId(payload);
        var source = Equipment0183Find(family, sourceId);
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
        _mongo.UnifiedDefinitions.InsertOne(clone);
        _logger.Admin($"definitions.equipment.admin.clone family={family} source={source.Id} clone={clone.Id} actor={actor.Id}");
        return Ok("Копия определения создана.", new Dictionary<string, object>
        {
            ["item"] = Equipment0183AdminView(clone),
            ["sourceOfTruth"] = "unified_definitions"
        });
    }

    public ResponseEnvelope CoreEquipmentAdminSetArchived(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var family = Equipment0183Family(payload);
        var definitionId = Equipment0183DefinitionId(payload);
        var document = Equipment0183Find(family, definitionId);
        if (document == null) return Error("Определение не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var archived = PayloadReader.GetBool(payload, "isArchived");
        document.IsArchived = archived;
        document.Archived = archived;
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.UpdatedUtc = document.UpdatedAtUtc;
        document.ServerOnlyData ??= new Dictionary<string, object>();
        document.ServerOnlyData[archived ? "archivedByUserId" : "restoredByUserId"] = actor.Id;
        _mongo.UnifiedDefinitions.ReplaceOne(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, family)
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, definitionId),
            document);
        _logger.Admin($"definitions.equipment.admin.archive family={family} id={definitionId} archived={archived} actor={actor.Id}");
        return Ok(archived ? "Определение архивировано." : "Определение восстановлено.", new Dictionary<string, object>
        {
            ["item"] = Equipment0183AdminView(document),
            ["sourceOfTruth"] = "unified_definitions"
        });
    }

    public ResponseEnvelope CoreEquipmentAdminReferences(CommandContext context)
    {
        RequireAdmin(context);
        var unifiedFamilies = CoreEquipmentDefinitionFamilies.All
            .Concat(new[]
            {
                DefinitionCategoryIds.Attribute,
                DefinitionCategoryIds.SubAttribute,
                DefinitionCategoryIds.Skill,
                DefinitionCategoryIds.EquipmentSlot,
                DefinitionCategoryIds.Law,
                DefinitionCategoryIds.License,
                DefinitionCategoryIds.Restriction
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var documents = _mongo.UnifiedDefinitions
            .Find(Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, unifiedFamilies))
            .ToList();
        var references = documents.Select(x => (object)Equipment0183ReferenceView(x)).ToList();

        foreach (var skill in _mongo.DefinitionSkills.Find(Builders<SkillDefinition>.Filter.Empty).ToList())
        {
            if (skill.IsArchived || skill.Archived) continue;
            references.Add(new Dictionary<string, object>
            {
                ["definitionId"] = FirstNonEmpty(skill.Code, skill.Id),
                ["family"] = DefinitionCategoryIds.Skill,
                ["displayName"] = FirstNonEmpty(skill.Name, skill.Code),
                ["summary"] = FirstNonEmpty(skill.DisplayGroup, "Навык"),
                ["isPlayerVisible"] = skill.IsActive,
                ["isArchived"] = false
            });
        }

        return Ok("Reference options loaded.", new Dictionary<string, object>
        {
            ["items"] = references
                .OrderBy(x => Convert.ToString(((Dictionary<string, object>)x)["family"], CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => Convert.ToString(((Dictionary<string, object>)x)["displayName"], CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ["sourceOfTruth"] = "unified_definitions + skill_definition_documents adapter"
        });
    }

    public ResponseEnvelope CoreEquipmentPlayerList(CommandContext context)
    {
        GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var families = Equipment0183RequestedFamilies(payload);
        var filter = Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, families)
                     & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        var documents = _mongo.UnifiedDefinitions.Find(filter).ToList()
            .Where(Equipment0183PlayerVisible)
            .ToList();
        var search = PayloadReader.GetString(payload, "search") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(search))
        {
            documents = documents.Where(x => Equipment0183Contains(x.Name, search)
                                             || Equipment0183Contains(x.PublicDescription, search)
                                             || (x.Tags ?? new List<string>()).Any(tag => Equipment0183Contains(tag, search)))
                .ToList();
        }

        var items = documents.OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => (object)Equipment0183PlayerView(x))
            .ToArray();
        _logger.Admin($"definitions.equipment.player.list visible={items.Length}");
        return Ok("Player-visible equipment definitions loaded.", new Dictionary<string, object>
        {
            ["items"] = items,
            ["playerSafe"] = true
        });
    }

    public ResponseEnvelope CoreEquipmentPlayerGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var family = Equipment0183Family(payload);
        var definitionId = Equipment0183DefinitionId(payload);
        var document = Equipment0183Find(family, definitionId);
        if (document == null || !Equipment0183PlayerVisible(document))
        {
            return Error("Определение недоступно.", ResponseStatus.NotFound, ErrorCode.NotFound);
        }
        return Ok("Player-visible equipment definition loaded.", new Dictionary<string, object>
        {
            ["item"] = Equipment0183PlayerView(document),
            ["playerSafe"] = true
        });
    }

    private UnifiedDefinitionDocument? Equipment0183Find(string family, string definitionId)
    {
        if (!CoreEquipmentDefinitionFamilies.IsSupported(family) || string.IsNullOrWhiteSpace(definitionId)) return null;
        return _mongo.UnifiedDefinitions.Find(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, family)
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, definitionId.Trim())).FirstOrDefault();
    }

    private static string Equipment0183Family(IDictionary<string, object> payload)
        => FirstNonEmpty(PayloadReader.GetString(payload, "family"), PayloadReader.GetString(payload, "category")).Trim();

    private static string Equipment0183DefinitionId(IDictionary<string, object> payload)
        => FirstNonEmpty(PayloadReader.GetString(payload, "definitionId"), PayloadReader.GetString(payload, "id")).Trim();

    private static string[] Equipment0183RequestedFamilies(IDictionary<string, object> payload)
    {
        var family = Equipment0183Family(payload);
        return CoreEquipmentDefinitionFamilies.IsSupported(family)
            ? new[] { family }
            : CoreEquipmentDefinitionFamilies.All;
    }

    private static string Equipment0183GenerateId(string family, string name)
    {
        var slug = new string((name ?? string.Empty).ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());
        while (slug.Contains("__")) slug = slug.Replace("__", "_");
        slug = slug.Trim('_');
        if (slug.Length > 32) slug = slug.Substring(0, 32).TrimEnd('_');
        if (string.IsNullOrWhiteSpace(slug)) slug = "definition";
        return $"{family}_{slug}_{Guid.NewGuid():N}".Substring(0, Math.Min(64, family.Length + slug.Length + 34));
    }

    private static Dictionary<string, object> Equipment0183BuildExtraData(
        string family,
        IDictionary<string, object> payload,
        Dictionary<string, object>? existing)
    {
        var result = existing == null
            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(existing, StringComparer.OrdinalIgnoreCase);
        result["profileVersion"] = 1;
        result["profileFamily"] = family;
        foreach (var key in Equipment0183StringKeys(family)) result[key] = PayloadReader.GetString(payload, key) ?? string.Empty;
        foreach (var key in Equipment0183IntegerKeys(family)) result[key] = PayloadReader.GetInt(payload, key) ?? 0;
        foreach (var key in Equipment0183DecimalKeys(family)) result[key] = Equipment0183Decimal(payload, key);
        foreach (var key in Equipment0183BooleanKeys(family)) result[key] = PayloadReader.GetBool(payload, key);
        foreach (var key in Equipment0183ListKeys(family)) result[key] = Equipment0183StringList(payload, key).Cast<object>().ToArray();
        if (string.Equals(family, DefinitionCategoryIds.Weapon, StringComparison.OrdinalIgnoreCase))
        {
            result["attackProfiles"] = Equipment0183AttackProfiles(payload).Cast<object>().ToArray();
        }
        if (string.Equals(family, DefinitionCategoryIds.Armor, StringComparison.OrdinalIgnoreCase))
        {
            result["shieldAttackProfiles"] = Equipment0183AttackProfiles(payload, "shieldAttackProfiles").Cast<object>().ToArray();
        }
        return result;
    }

    private Equipment0183ValidationResult Equipment0183Validate(
        UnifiedDefinitionDocument document,
        Dictionary<string, object> extra,
        bool validateReferences)
    {
        var result = new Equipment0183ValidationResult();
        if (string.IsNullOrWhiteSpace(document.Name)) result.Errors.Add("Название обязательно.");
        var family = document.Category;
        if (string.Equals(family, DefinitionCategoryIds.Resource, StringComparison.OrdinalIgnoreCase))
        {
            Equipment0183Require(extra, "resourceCategory", "Укажите категорию ресурса.", result);
            Equipment0183Require(extra, "unit", "Укажите единицу измерения.", result);
        }
        else if (string.Equals(family, DefinitionCategoryIds.Item, StringComparison.OrdinalIgnoreCase))
        {
            Equipment0183Require(extra, "itemType", "Укажите тип предмета.", result);
            if (Equipment0183ExtraInt(extra, "maxStack") < 1) result.Errors.Add("Размер стека должен быть не меньше 1.");
        }
        else if (string.Equals(family, DefinitionCategoryIds.DamageType, StringComparison.OrdinalIgnoreCase))
        {
            Equipment0183Require(extra, "nature", "Укажите природу урона.", result);
            Equipment0183Require(extra, "classification", "Укажите классификацию урона.", result);
        }
        else if (string.Equals(family, DefinitionCategoryIds.Weapon, StringComparison.OrdinalIgnoreCase))
        {
            Equipment0183Require(extra, "weaponCategory", "Укажите категорию оружия.", result);
            var profiles = Equipment0183MapList(extra.TryGetValue("attackProfiles", out var raw) ? raw : null);
            if (profiles.Count == 0) result.Errors.Add("Добавьте хотя бы один профиль атаки.");
            foreach (var profile in profiles)
            {
                if (string.IsNullOrWhiteSpace(Equipment0183MapString(profile, "name"))) result.Errors.Add("У каждого профиля атаки должно быть название.");
                if (string.IsNullOrWhiteSpace(Equipment0183MapString(profile, "damageExpression"))) result.Errors.Add("У каждого профиля атаки должна быть формула урона.");
            }
        }
        else if (string.Equals(family, DefinitionCategoryIds.Ammo, StringComparison.OrdinalIgnoreCase))
        {
            Equipment0183Require(extra, "ammoType", "Укажите тип боеприпасов.", result);
        }
        else if (string.Equals(family, DefinitionCategoryIds.Armor, StringComparison.OrdinalIgnoreCase))
        {
            Equipment0183Require(extra, "armorCategory", "Укажите категорию брони.", result);
        }

        if (validateReferences) Equipment0183ValidateReferences(document, extra, result);
        return result;
    }

    private void Equipment0183ValidateReferences(
        UnifiedDefinitionDocument source,
        Dictionary<string, object> extra,
        Equipment0183ValidationResult result)
    {
        var references = new List<Tuple<string, string>>();
        void Add(string family, IEnumerable<string> ids)
        {
            foreach (var id in ids.Where(x => !string.IsNullOrWhiteSpace(x))) references.Add(Tuple.Create(family, id));
        }

        if (string.Equals(source.Category, DefinitionCategoryIds.Weapon, StringComparison.OrdinalIgnoreCase))
        {
            Add(DefinitionCategoryIds.Ammo, Equipment0183ExtraList(extra, "ammoDefinitionIds"));
            Add(DefinitionCategoryIds.Skill, Equipment0183ExtraList(extra, "requiredSkillIds"));
            Add(DefinitionCategoryIds.Attribute, Equipment0183ExtraList(extra, "requiredAttributeIds"));
            Add(DefinitionCategoryIds.EquipmentSlot, Equipment0183ExtraList(extra, "bodyRequirements"));
            foreach (var profile in Equipment0183MapList(extra.TryGetValue("attackProfiles", out var profiles) ? profiles : null))
            {
                Add(DefinitionCategoryIds.Skill, new[] { Equipment0183MapString(profile, "skillDefinitionId") });
                Add(DefinitionCategoryIds.SubAttribute, new[] { Equipment0183MapString(profile, "subAttributeDefinitionId") });
                Add(DefinitionCategoryIds.DamageType, Equipment0183MapListStrings(profile, "damageTypeDefinitionIds"));
            }
        }
        else if (string.Equals(source.Category, DefinitionCategoryIds.Ammo, StringComparison.OrdinalIgnoreCase))
        {
            Add(DefinitionCategoryIds.Weapon, Equipment0183ExtraList(extra, "allowedWeaponIds"));
            Add(DefinitionCategoryIds.Weapon, Equipment0183ExtraList(extra, "forbiddenWeaponIds"));
            Add(DefinitionCategoryIds.DamageType, Equipment0183ExtraList(extra, "damageTypeAdditions"));
            Add(DefinitionCategoryIds.DamageType, Equipment0183ExtraList(extra, "damageTypeReplacements"));
        }
        else if (string.Equals(source.Category, DefinitionCategoryIds.Item, StringComparison.OrdinalIgnoreCase))
        {
            Add(DefinitionCategoryIds.EquipmentSlot, Equipment0183ExtraList(extra, "bodyCompatibilityTags"));
        }
        else if (string.Equals(source.Category, DefinitionCategoryIds.Armor, StringComparison.OrdinalIgnoreCase))
        {
            Add(DefinitionCategoryIds.EquipmentSlot, Equipment0183ExtraList(extra, "protectedBodyZones"));
            Add(DefinitionCategoryIds.EquipmentSlot, Equipment0183ExtraList(extra, "bodyCompatibilityTags"));
            foreach (var profile in Equipment0183MapList(extra.TryGetValue("shieldAttackProfiles", out var profiles) ? profiles : null))
            {
                Add(DefinitionCategoryIds.Skill, new[] { Equipment0183MapString(profile, "skillDefinitionId") });
                Add(DefinitionCategoryIds.SubAttribute, new[] { Equipment0183MapString(profile, "subAttributeDefinitionId") });
                Add(DefinitionCategoryIds.DamageType, Equipment0183MapListStrings(profile, "damageTypeDefinitionIds"));
            }
        }

        foreach (var reference in references.Distinct(new Equipment0183ReferenceTupleComparer()))
        {
            UnifiedDefinitionDocument? target = null;
            if (string.Equals(reference.Item1, DefinitionCategoryIds.Skill, StringComparison.OrdinalIgnoreCase))
            {
                var skill = _mongo.DefinitionSkills.Find(
                    Builders<SkillDefinition>.Filter.Eq(x => x.Code, reference.Item2)
                    | Builders<SkillDefinition>.Filter.Eq(x => x.Id, reference.Item2)).FirstOrDefault();
                if (skill == null || skill.IsArchived || skill.Archived)
                {
                    result.Errors.Add($"Связанный навык «{reference.Item2}» не найден.");
                }
                continue;
            }

            target = _mongo.UnifiedDefinitions.Find(
                Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, reference.Item1)
                & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, reference.Item2)).FirstOrDefault();
            if (target == null || target.IsArchived)
            {
                result.Errors.Add($"Связанная запись «{reference.Item2}» ({reference.Item1}) не найдена.");
                continue;
            }
            if (Equipment0183PlayerVisible(source) && !Equipment0183PlayerVisible(target))
            {
                result.Warnings.Add($"Видимое игрокам определение ссылается на скрытую запись «{target.Name}».");
            }
        }
    }

    private static Dictionary<string, object> Equipment0183AdminView(UnifiedDefinitionDocument document)
    {
        var result = Equipment0183CommonView(document, includeInternalId: true);
        result["gmDescription"] = document.GMDescription ?? string.Empty;
        result["visibilityRule"] = document.VisibilityRule ?? string.Empty;
        result["isArchived"] = document.IsArchived;
        result["createdAtUtc"] = document.CreatedAtUtc;
        result["updatedAtUtc"] = document.UpdatedAtUtc;
        foreach (var pair in document.ExtraData ?? new Dictionary<string, object>()) result[pair.Key] = pair.Value ?? string.Empty;
        return result;
    }

    private Dictionary<string, object> Equipment0183PlayerView(UnifiedDefinitionDocument document)
    {
        var result = Equipment0183CommonView(document, includeInternalId: false);
        var extra = document.ExtraData ?? new Dictionary<string, object>();
        var facts = new List<object>();
        void Fact(string label, string key)
        {
            var value = Equipment0183ExtraDisplay(extra, key);
            if (!string.IsNullOrWhiteSpace(value)) facts.Add(new Dictionary<string, object> { ["label"] = label, ["value"] = value });
        }

        switch (document.Category)
        {
            case DefinitionCategoryIds.Resource:
                Fact("Категория", "resourceCategory");
                Fact("Единица", "unit");
                Fact("Состояние", "physicalState");
                Fact("Редкость", "rarity");
                Fact("Базовая стоимость", "baseValue");
                Fact("Хранение", "storageRequirements");
                break;
            case DefinitionCategoryIds.Item:
                Fact("Тип", "itemType");
                Fact("Масса", "mass");
                Fact("Размер", "size");
                Fact("Стек", "maxStack");
                Fact("Прочность", "durability");
                Fact("Редкость", "rarity");
                Fact("Стоимость", "baseValue");
                break;
            case DefinitionCategoryIds.DamageType:
                Fact("Природа", "nature");
                Fact("Класс", "classification");
                Fact("Сопротивления", "resistanceTags");
                Fact("Уязвимости", "vulnerabilityTags");
                Fact("Иммунитеты", "immunityTags");
                break;
            case DefinitionCategoryIds.Weapon:
                Fact("Категория", "weaponCategory");
                Fact("Масштаб", "scale");
                Fact("Дальность", "range");
                Fact("Перезарядка", "reloadRules");
                result["attackProfiles"] = Equipment0183PlayerAttackProfiles(extra, "attackProfiles");
                result["compatibleAmmo"] = Equipment0183ReferenceNames(DefinitionCategoryIds.Ammo, Equipment0183ExtraList(extra, "ammoDefinitionIds")).Cast<object>().ToArray();
                break;
            case DefinitionCategoryIds.Ammo:
                Fact("Тип", "ammoType");
                Fact("Калибр", "caliber");
                Fact("Совместимость", "compatibilityTags");
                Fact("Расход", "consumptionModel");
                Fact("Качество", "quality");
                result["allowedWeapons"] = Equipment0183ReferenceNames(DefinitionCategoryIds.Weapon, Equipment0183ExtraList(extra, "allowedWeaponIds")).Cast<object>().ToArray();
                break;
            case DefinitionCategoryIds.Armor:
                Fact("Категория", "armorCategory");
                Fact("Зоны защиты", "protectedBodyZones");
                Fact("Физическая защита", "physicalDefense");
                Fact("Магическая защита", "magicalDefense");
                Fact("Прочность", "durability");
                Fact("Шум", "noise");
                Fact("Скрываемость", "concealability");
                break;
        }
        result["playerFacts"] = facts.ToArray();
        return result;
    }

    private static Dictionary<string, object> Equipment0183CommonView(UnifiedDefinitionDocument document, bool includeInternalId)
    {
        var result = new Dictionary<string, object>
        {
            ["family"] = document.Category ?? string.Empty,
            ["category"] = document.Category ?? string.Empty,
            ["name"] = document.Name ?? string.Empty,
            ["displayName"] = document.Name ?? string.Empty,
            ["publicDescription"] = document.PublicDescription ?? string.Empty,
            ["tags"] = (document.Tags ?? new List<string>()).Cast<object>().ToArray(),
            ["ruleSetIds"] = (document.RuleSetIds ?? new List<string>()).Cast<object>().ToArray(),
            ["isPlayerVisible"] = Equipment0183PlayerVisible(document)
        };
        if (includeInternalId) result["definitionId"] = document.Id ?? string.Empty;
        return result;
    }

    private static Dictionary<string, object> Equipment0183ReferenceView(UnifiedDefinitionDocument document)
    {
        return new Dictionary<string, object>
        {
            ["definitionId"] = document.Id ?? string.Empty,
            ["family"] = document.Category ?? string.Empty,
            ["displayName"] = FirstNonEmpty(document.Name, document.Id),
            ["summary"] = Equipment0183FamilyLabel(document.Category),
            ["isPlayerVisible"] = Equipment0183PlayerVisible(document),
            ["isArchived"] = document.IsArchived
        };
    }

    private object[] Equipment0183PlayerAttackProfiles(Dictionary<string, object> extra, string key)
    {
        return Equipment0183MapList(extra.TryGetValue(key, out var raw) ? raw : null).Select(profile =>
        {
            var damageTypeNames = Equipment0183ReferenceNames(
                DefinitionCategoryIds.DamageType,
                Equipment0183MapListStrings(profile, "damageTypeDefinitionIds"));
            return (object)new Dictionary<string, object>
            {
                ["name"] = Equipment0183MapString(profile, "name"),
                ["attackType"] = Equipment0183MapString(profile, "attackType"),
                ["actionCost"] = Equipment0183MapInt(profile, "actionCost"),
                ["attackRollType"] = Equipment0183MapString(profile, "attackRollType"),
                ["skill"] = Equipment0183ReferenceName(DefinitionCategoryIds.Skill, Equipment0183MapString(profile, "skillDefinitionId")),
                ["accuracyModifier"] = Equipment0183MapInt(profile, "accuracyModifier"),
                ["range"] = Equipment0183MapString(profile, "range"),
                ["damageExpression"] = Equipment0183MapString(profile, "damageExpression"),
                ["damageTypes"] = damageTypeNames.Cast<object>().ToArray(),
                ["physicalPenetration"] = Equipment0183MapInt(profile, "physicalPenetration"),
                ["armorPenetration"] = Equipment0183MapInt(profile, "armorPenetration"),
                ["magicPenetration"] = Equipment0183MapInt(profile, "magicPenetration"),
                ["moralePenetration"] = Equipment0183MapInt(profile, "moralePenetration"),
                ["fireMode"] = Equipment0183MapString(profile, "fireMode"),
                ["ammoCost"] = Equipment0183MapInt(profile, "ammoCost")
            };
        }).ToArray();
    }

    private List<string> Equipment0183ReferenceNames(string family, IEnumerable<string> ids)
        => ids.Select(id => Equipment0183ReferenceName(family, id)).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

    private string Equipment0183ReferenceName(string family, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        if (string.Equals(family, DefinitionCategoryIds.Skill, StringComparison.OrdinalIgnoreCase))
        {
            var skill = _mongo.DefinitionSkills.Find(
                Builders<SkillDefinition>.Filter.Eq(x => x.Code, id)
                | Builders<SkillDefinition>.Filter.Eq(x => x.Id, id)).FirstOrDefault();
            return FirstNonEmpty(skill?.Name, skill?.Code, "Недоступная связь");
        }
        var document = _mongo.UnifiedDefinitions.Find(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, family)
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, id)).FirstOrDefault();
        return document != null && Equipment0183PlayerVisible(document)
            ? FirstNonEmpty(document.Name, "Связанная запись")
            : "Недоступная связь";
    }

    private static bool Equipment0183PlayerVisible(UnifiedDefinitionDocument document)
    {
        if (document == null || document.IsArchived) return false;
        return string.Equals(document.VisibilityRule, VisibilityRuleIds.Public, StringComparison.OrdinalIgnoreCase)
               || string.Equals(document.VisibilityRule, VisibilityRuleIds.PlayerVisible, StringComparison.OrdinalIgnoreCase);
    }

    private static List<Dictionary<string, object>> Equipment0183AttackProfiles(IDictionary<string, object> payload, string key = "attackProfiles")
    {
        var profiles = Equipment0183MapList(payload.TryGetValue(key, out var raw) ? raw : null);
        var result = new List<Dictionary<string, object>>();
        foreach (var profile in profiles)
        {
            var normalized = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var stringKey in new[]
                     {
                         "profileId", "name", "attackType", "attackRollType", "skillDefinitionId",
                         "subAttributeDefinitionId", "range", "damageExpression", "area", "fireMode"
                     })
            {
                normalized[stringKey] = Equipment0183MapString(profile, stringKey);
            }
            foreach (var integerKey in new[]
                     {
                         "actionCost", "accuracyModifier", "physicalPenetration", "armorPenetration",
                         "magicPenetration", "moralePenetration", "reloadCost", "ammoCost"
                     })
            {
                normalized[integerKey] = Equipment0183MapInt(profile, integerKey);
            }
            foreach (var booleanKey in new[] { "canReact", "canReturnFire", "canParry", "canBlock" })
            {
                normalized[booleanKey] = Equipment0183MapBool(profile, booleanKey);
            }
            normalized["damageTypeDefinitionIds"] = Equipment0183MapListStrings(profile, "damageTypeDefinitionIds").Cast<object>().ToArray();
            if (string.IsNullOrWhiteSpace(Convert.ToString(normalized["profileId"], CultureInfo.InvariantCulture)))
            {
                normalized["profileId"] = Guid.NewGuid().ToString("N");
            }
            result.Add(normalized);
        }
        return result;
    }

    private static Dictionary<string, object> Equipment0183CloneMap(Dictionary<string, object>? source)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source ?? new Dictionary<string, object>())
        {
            if (pair.Value is IDictionary dictionary)
            {
                var nested = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(key)) nested[key] = entry.Value ?? string.Empty;
                }
                result[pair.Key] = nested;
            }
            else if (pair.Value is IEnumerable enumerable && pair.Value is not string)
            {
                result[pair.Key] = enumerable.Cast<object>().ToArray();
            }
            else
            {
                result[pair.Key] = pair.Value ?? string.Empty;
            }
        }
        return result;
    }

    private static List<string> Equipment0183StringList(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var raw) || raw == null) return new List<string>();
        if (raw is string text)
        {
            return text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        if (raw is IEnumerable enumerable)
        {
            return enumerable.Cast<object>()
                .Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        return new List<string>();
    }

    private static List<Dictionary<string, object>> Equipment0183MapList(object? raw)
    {
        var result = new List<Dictionary<string, object>>();
        if (raw is not IEnumerable enumerable || raw is string) return result;
        foreach (var item in enumerable)
        {
            var map = Equipment0183Map(item);
            if (map.Count > 0) result.Add(map);
        }
        return result;
    }

    private static Dictionary<string, object> Equipment0183Map(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (raw is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
        }
        return result;
    }

    private static string Equipment0183MapString(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var raw) ? Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    private static int Equipment0183MapInt(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || raw == null) return 0;
        return int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static bool Equipment0183MapBool(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || raw == null) return false;
        return raw is bool value ? value : bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed) && parsed;
    }

    private static List<string> Equipment0183MapListStrings(IDictionary<string, object> map, string key)
        => Equipment0183StringList(map, key);

    private static decimal Equipment0183Decimal(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var raw) || raw == null) return 0m;
        return decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }

    private static string Equipment0183ExtraString(IDictionary<string, object>? extra, string key)
        => extra != null && extra.TryGetValue(key, out var raw) ? Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    private static int Equipment0183ExtraInt(IDictionary<string, object>? extra, string key)
        => int.TryParse(Equipment0183ExtraString(extra, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static List<string> Equipment0183ExtraList(IDictionary<string, object>? extra, string key)
        => extra == null ? new List<string>() : Equipment0183StringList(extra, key);

    private static string Equipment0183ExtraDisplay(IDictionary<string, object>? extra, string key)
    {
        if (extra == null || !extra.TryGetValue(key, out var raw) || raw == null) return string.Empty;
        if (raw is IEnumerable enumerable && raw is not string)
        {
            return string.Join(", ", enumerable.Cast<object>().Select(x => Convert.ToString(x, CultureInfo.CurrentCulture)).Where(x => !string.IsNullOrWhiteSpace(x)));
        }
        return Convert.ToString(raw, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private static void Equipment0183Require(
        IDictionary<string, object> extra,
        string key,
        string message,
        Equipment0183ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(Equipment0183ExtraString(extra, key))) result.Errors.Add(message);
    }

    private static bool Equipment0183Contains(string value, string search)
        => !string.IsNullOrWhiteSpace(value) && value.IndexOf(search ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string Equipment0183FamilyLabel(string family)
    {
        return family switch
        {
            DefinitionCategoryIds.Resource => "Ресурс",
            DefinitionCategoryIds.Item => "Предмет",
            DefinitionCategoryIds.DamageType => "Тип урона",
            DefinitionCategoryIds.Weapon => "Оружие",
            DefinitionCategoryIds.Ammo => "Боеприпасы",
            DefinitionCategoryIds.Armor => "Броня",
            DefinitionCategoryIds.Skill => "Навык",
            DefinitionCategoryIds.SubAttribute => "Подхарактеристика",
            DefinitionCategoryIds.EquipmentSlot => "Зона или слот",
            _ => family
        };
    }

    private static string[] Equipment0183StringKeys(string family)
    {
        return family switch
        {
            DefinitionCategoryIds.Resource => new[] { "resourceCategory", "unit", "physicalState", "rarity", "legality", "storageRequirements" },
            DefinitionCategoryIds.Item => new[] { "itemType", "size", "quality", "rarity", "legality" },
            DefinitionCategoryIds.DamageType => new[] { "nature", "classification" },
            DefinitionCategoryIds.Weapon => new[] { "weaponCategory", "scale", "range", "reloadRules", "legality" },
            DefinitionCategoryIds.Ammo => new[] { "ammoType", "caliber", "consumptionModel", "chargeModel", "quality", "failureMetadata", "legality" },
            DefinitionCategoryIds.Armor => new[] { "armorCategory", "designedSize", "concealability", "legality" },
            _ => Array.Empty<string>()
        };
    }

    private static string[] Equipment0183IntegerKeys(string family)
    {
        return family switch
        {
            DefinitionCategoryIds.Item => new[] { "maxStack", "durability" },
            DefinitionCategoryIds.Ammo => new[] { "physicalPenetrationModifier", "armorPenetrationModifier", "magicPenetrationModifier", "moralePenetrationModifier" },
            DefinitionCategoryIds.Armor => new[] { "physicalDefense", "magicalDefense", "durability", "stealthPenalty", "noise", "strengthRequirement" },
            _ => Array.Empty<string>()
        };
    }

    private static string[] Equipment0183DecimalKeys(string family)
    {
        return family switch
        {
            DefinitionCategoryIds.Resource => new[] { "massPerUnit", "volumePerUnit", "baseValue" },
            DefinitionCategoryIds.Item => new[] { "mass", "baseValue" },
            _ => Array.Empty<string>()
        };
    }

    private static string[] Equipment0183BooleanKeys(string family)
    {
        return family switch
        {
            DefinitionCategoryIds.Resource => new[] { "supportsQuality" },
            DefinitionCategoryIds.Item => new[] { "stackable" },
            DefinitionCategoryIds.Armor => new[] { "hasShieldProfile" },
            _ => Array.Empty<string>()
        };
    }

    private static string[] Equipment0183ListKeys(string family)
    {
        return family switch
        {
            DefinitionCategoryIds.Item => new[] { "bodyCompatibilityTags" },
            DefinitionCategoryIds.DamageType => new[] { "resistanceTags", "vulnerabilityTags", "immunityTags" },
            DefinitionCategoryIds.Weapon => new[] { "weaponNatures", "requiredSkillIds", "requiredAttributeIds", "bodyRequirements", "ammoDefinitionIds" },
            DefinitionCategoryIds.Ammo => new[] { "compatibilityTags", "allowedWeaponIds", "forbiddenWeaponIds", "requiredFireModes", "damageTypeAdditions", "damageTypeReplacements" },
            DefinitionCategoryIds.Armor => new[] { "protectedBodyZones", "bodyCompatibilityTags", "specialResistanceTags" },
            _ => Array.Empty<string>()
        };
    }

    private sealed class Equipment0183ValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    private sealed class Equipment0183ReferenceTupleComparer : IEqualityComparer<Tuple<string, string>>
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
