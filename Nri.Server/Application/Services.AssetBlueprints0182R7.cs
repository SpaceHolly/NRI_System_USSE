using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.AssetConfigurators.Core.Building;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.LandMarine;
using Nri.AssetConfigurators.Core.Spacecraft;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope AssetBlueprintPlayerList0182R7(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var items = _repositories.AssetConfigurationBlueprints
            .Find(Builders<AssetConfigurationBlueprintState>.Filter.Eq(x => x.OwnerUserId, actor.Id))
            .Where(item => includeArchived || !item.Archived)
            .OrderByDescending(item => item.UpdatedUtc)
            .Select(item => (object)AssetBlueprintPayload(item, false))
            .ToArray();
        return Ok("Чертежи загружены.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope AssetBlueprintPlayerGet0182R7(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var item = RequireAssetBlueprint(context);
        if (!string.Equals(item.OwnerUserId, actor.Id, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Нельзя открыть чужой приватный чертёж.");
        return Ok("Чертёж загружен.", new Dictionary<string, object> { ["item"] = AssetBlueprintPayload(item, false) });
    }

    public ResponseEnvelope AssetBlueprintPlayerCreate0182R7(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var operationId = RequireLength(
            PayloadReader.GetString(context.Request.Payload, "operationId"),
            8,
            128,
            "operationId");
        var existing = _repositories.AssetConfigurationBlueprints
            .Find(Builders<AssetConfigurationBlueprintState>.Filter.And(
                Builders<AssetConfigurationBlueprintState>.Filter.Eq(x => x.OwnerUserId, actor.Id),
                Builders<AssetConfigurationBlueprintState>.Filter.Eq(x => x.ClientOperationId, operationId)))
            .FirstOrDefault();
        if (existing != null)
        {
            return Ok("Повторная команда распознана; возвращён ранее созданный чертёж.",
                new Dictionary<string, object> { ["item"] = AssetBlueprintPayload(existing, false), ["idempotentReplay"] = true });
        }

        var kind = NormalizeAssetConfiguratorKind(PayloadReader.GetString(context.Request.Payload, "configuratorKind"));
        var configurationMap = RequireAssetConfigurationMap(context.Request.Payload);
        var configuration = ParseAssetConfiguration(kind, configurationMap);
        var calculated = CalculateAssetBlueprint(configuration);
        var status = NormalizeAssetBlueprintStatus(PayloadReader.GetString(context.Request.Payload, "status"));
        if (status == AssetBlueprintStatusIds.Ready && !calculated.IsValid)
            throw new ArgumentException("Нельзя пометить как готовый чертёж с ошибками проверки.");

        var item = new AssetConfigurationBlueprintState
        {
            OwnerUserId = actor.Id,
            OwnerLoginSnapshot = actor.Login,
            OwnerCharacterId = RequireOwnedAssetBlueprintCharacter(
                actor.Id,
                PayloadReader.GetString(context.Request.Payload, "ownerCharacterId")),
            Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), ConfigurationName(configuration)), 2, 160, "name"),
            ConfiguratorKind = kind,
            CalculationMode = ConfigurationMode(configuration),
            CatalogSource = CatalogSource(kind),
            CatalogVersion = CatalogVersion(kind),
            CatalogCommitSha = CatalogCommitSha(kind),
            Configuration = configuration,
            ServerCalculation = calculated,
            ReadableSummary = calculated.Summary,
            Status = status,
            Visibility = NormalizeAssetBlueprintVisibility(PayloadReader.GetString(context.Request.Payload, "visibility")),
            Revision = 1,
            ClientOperationId = operationId,
            LastCalculatedBy = "server"
        };
        _repositories.AssetConfigurationBlueprints.Insert(item);
        WriteAudit("asset_configuration_blueprint", actor.Id, "create", item.Id);
        _logger.Session($"asset_blueprint.player.create owner={actor.Login} blueprintId={item.Id} kind={kind} revision=1 valid={calculated.IsValid}");
        return Ok("Чертёж сохранён.", new Dictionary<string, object> { ["item"] = AssetBlueprintPayload(item, false), ["idempotentReplay"] = false });
    }

    public ResponseEnvelope AssetBlueprintPlayerUpdate0182R7(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var item = RequireAssetBlueprint(context);
        if (!string.Equals(item.OwnerUserId, actor.Id, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Нельзя изменить чужой приватный чертёж.");
        var expectedRevision = PayloadReader.GetInt(context.Request.Payload, "expectedRevision")
                               ?? throw new ArgumentException("expectedRevision is required.");
        if (expectedRevision != item.Revision)
            throw new InvalidOperationException("Чертёж был изменён в другом окне. Обновите данные перед сохранением.");

        var kind = NormalizeAssetConfiguratorKind(FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "configuratorKind"),
            item.ConfiguratorKind));
        if (!string.Equals(kind, item.ConfiguratorKind, StringComparison.Ordinal))
            throw new ArgumentException("Тип существующего чертежа изменить нельзя.");
        var configuration = ParseAssetConfiguration(kind, RequireAssetConfigurationMap(context.Request.Payload));
        var calculated = CalculateAssetBlueprint(configuration);
        var status = NormalizeAssetBlueprintStatus(FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "status"),
            item.Status));
        if (status == AssetBlueprintStatusIds.Ready && !calculated.IsValid)
            throw new ArgumentException("Нельзя пометить как готовый чертёж с ошибками проверки.");

        item.Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), ConfigurationName(configuration)), 2, 160, "name");
        item.OwnerCharacterId = RequireOwnedAssetBlueprintCharacter(
            actor.Id,
            FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "ownerCharacterId"), item.OwnerCharacterId));
        item.Configuration = configuration;
        item.CalculationMode = ConfigurationMode(configuration);
        item.ServerCalculation = calculated;
        item.ReadableSummary = calculated.Summary;
        item.Status = status;
        item.Visibility = NormalizeAssetBlueprintVisibility(FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "visibility"),
            item.Visibility));
        item.Archived = false;
        item.Revision++;
        item.LastCalculatedBy = "server";
        _repositories.AssetConfigurationBlueprints.Replace(item);
        WriteAudit("asset_configuration_blueprint", actor.Id, "update", item.Id);
        _logger.Session($"asset_blueprint.player.update owner={actor.Login} blueprintId={item.Id} revision={item.Revision} valid={calculated.IsValid}");
        return Ok("Изменения чертежа сохранены.", new Dictionary<string, object> { ["item"] = AssetBlueprintPayload(item, false) });
    }

    public ResponseEnvelope AssetBlueprintPlayerDuplicate0182R7(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var source = RequireAssetBlueprint(context);
        if (!string.Equals(source.OwnerUserId, actor.Id, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Нельзя дублировать чужой приватный чертёж.");
        var operationId = RequireLength(PayloadReader.GetString(context.Request.Payload, "operationId"), 8, 128, "operationId");
        var replay = _repositories.AssetConfigurationBlueprints
            .Find(Builders<AssetConfigurationBlueprintState>.Filter.And(
                Builders<AssetConfigurationBlueprintState>.Filter.Eq(x => x.OwnerUserId, actor.Id),
                Builders<AssetConfigurationBlueprintState>.Filter.Eq(x => x.ClientOperationId, operationId)))
            .FirstOrDefault();
        if (replay != null)
            return Ok("Повторная команда распознана.", new Dictionary<string, object> { ["item"] = AssetBlueprintPayload(replay, false), ["idempotentReplay"] = true });

        var clone = new AssetConfigurationBlueprintState
        {
            OwnerUserId = actor.Id,
            OwnerLoginSnapshot = actor.Login,
            OwnerCharacterId = source.OwnerCharacterId,
            Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), source.Name + " — копия"), 2, 160, "name"),
            ConfiguratorKind = source.ConfiguratorKind,
            CalculationMode = source.CalculationMode,
            CatalogSource = source.CatalogSource,
            CatalogVersion = source.CatalogVersion,
            CatalogCommitSha = source.CatalogCommitSha,
            Configuration = CloneAssetConfiguration(source.Configuration),
            Status = AssetBlueprintStatusIds.Draft,
            Visibility = AssetBlueprintVisibilityIds.Private,
            Revision = 1,
            ClientOperationId = operationId,
            LastCalculatedBy = "server"
        };
        clone.ServerCalculation = CalculateAssetBlueprint(clone.Configuration);
        clone.ReadableSummary = clone.ServerCalculation.Summary;
        _repositories.AssetConfigurationBlueprints.Insert(clone);
        WriteAudit("asset_configuration_blueprint", actor.Id, "duplicate", clone.Id);
        return Ok("Копия чертежа создана.", new Dictionary<string, object> { ["item"] = AssetBlueprintPayload(clone, false), ["idempotentReplay"] = false });
    }

    public ResponseEnvelope AssetBlueprintPlayerArchive0182R7(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var item = RequireAssetBlueprint(context);
        if (!string.Equals(item.OwnerUserId, actor.Id, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Нельзя архивировать чужой приватный чертёж.");
        var expectedRevision = PayloadReader.GetInt(context.Request.Payload, "expectedRevision")
                               ?? throw new ArgumentException("expectedRevision is required.");
        if (expectedRevision != item.Revision)
            throw new InvalidOperationException("Чертёж был изменён в другом окне. Обновите данные перед архивированием.");
        item.Archived = true;
        item.Status = AssetBlueprintStatusIds.Archived;
        item.Revision++;
        _repositories.AssetConfigurationBlueprints.Replace(item);
        WriteAudit("asset_configuration_blueprint", actor.Id, "archive", item.Id);
        return Ok("Чертёж перенесён в архив.", new Dictionary<string, object> { ["item"] = AssetBlueprintPayload(item, false) });
    }

    public ResponseEnvelope AssetBlueprintAdminList0182R7(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var ownerUserId = PayloadReader.GetString(context.Request.Payload, "ownerUserId") ?? string.Empty;
        var filter = Builders<AssetConfigurationBlueprintState>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(ownerUserId))
            filter &= Builders<AssetConfigurationBlueprintState>.Filter.Eq(x => x.OwnerUserId, ownerUserId);
        var items = _repositories.AssetConfigurationBlueprints.Find(filter)
            .Where(item => includeArchived || !item.Archived)
            .OrderByDescending(item => item.UpdatedUtc)
            .Select(item => (object)AssetBlueprintPayload(item, true))
            .ToArray();
        _logger.Admin($"asset_blueprint.admin.list actor={actor.Login} count={items.Length}");
        return Ok("Чертежи игроков загружены.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope AssetBlueprintAdminGet0182R7(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var item = RequireAssetBlueprint(context);
        _logger.Admin($"asset_blueprint.admin.get actor={actor.Login} blueprintId={item.Id} owner={item.OwnerLoginSnapshot}");
        return Ok("Чертёж игрока загружен.", new Dictionary<string, object> { ["item"] = AssetBlueprintPayload(item, true) });
    }

    private AssetConfigurationBlueprintState RequireAssetBlueprint(CommandContext context)
    {
        var id = RequireLength(FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "blueprintId"),
            PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "blueprintId");
        return _repositories.AssetConfigurationBlueprints.GetById(id)
               ?? throw new KeyNotFoundException("Чертёж не найден.");
    }

    private string RequireOwnedAssetBlueprintCharacter(string actorUserId, string? characterId)
    {
        var normalized = RequireLength(characterId, 0, 128, "ownerCharacterId");
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        var ownership = _repositories.CharacterOwnerships
            .Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, normalized))
            .FirstOrDefault();
        if (ownership != null &&
            (string.Equals(ownership.OwnerUserId, actorUserId, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(ownership.ControlledByUserId, actorUserId, StringComparison.OrdinalIgnoreCase)))
            return normalized;

        throw new UnauthorizedAccessException("Нельзя связать чертёж с чужим персонажем.");
    }

    private static Dictionary<string, object> RequireAssetConfigurationMap(IDictionary<string, object> payload)
    {
        return PayloadReader.GetDictionary(payload, "configuration")
               ?? throw new ArgumentException("configuration is required.");
    }

    private static string NormalizeAssetConfiguratorKind(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_');
        if (normalized == "landmarine") normalized = AssetConfiguratorKindIds.LandMarine;
        if (normalized != AssetConfiguratorKindIds.Spacecraft &&
            normalized != AssetConfiguratorKindIds.LandMarine &&
            normalized != AssetConfiguratorKindIds.Building)
            throw new ArgumentException("Неизвестный тип конфигуратора.");
        return normalized;
    }

    private static string NormalizeAssetBlueprintStatus(string? value)
    {
        var normalized = (value ?? AssetBlueprintStatusIds.Draft).Trim().ToLowerInvariant();
        if (normalized != AssetBlueprintStatusIds.Draft && normalized != AssetBlueprintStatusIds.Ready)
            throw new ArgumentException("Допустимы только статусы «Черновик» и «Готов».");
        return normalized;
    }

    private static string NormalizeAssetBlueprintVisibility(string? value)
    {
        var normalized = (value ?? AssetBlueprintVisibilityIds.Private).Trim().ToLowerInvariant();
        if (normalized != AssetBlueprintVisibilityIds.Private && normalized != AssetBlueprintVisibilityIds.Shared)
            throw new ArgumentException("Неизвестный режим видимости чертежа.");
        return normalized;
    }

    private static AssetBlueprintConfigurationState ParseAssetConfiguration(
        string kind,
        IDictionary<string, object> map)
    {
        var result = new AssetBlueprintConfigurationState { Kind = kind };
        switch (kind)
        {
            case AssetConfiguratorKindIds.Spacecraft:
                result.Spacecraft = ParseSpacecraftConfiguration(map);
                break;
            case AssetConfiguratorKindIds.LandMarine:
                result.LandMarine = ParseLandMarineConfiguration(map);
                break;
            case AssetConfiguratorKindIds.Building:
                result.Building = ParseBuildingConfiguration(map);
                break;
        }
        return result;
    }

    private static SpacecraftBlueprintConfigurationState ParseSpacecraftConfiguration(IDictionary<string, object> map)
    {
        var value = new SpacecraftBlueprintConfigurationState
        {
            ConfigurationName = RequiredConfigurationName(map),
            Mode = ConfigurationMode(map),
            SizeKey = Value(map, "sizeKey"),
            ClassKey = Value(map, "classKey"),
            QualityKey = Value(map, "qualityKey"),
            PriceTierKey = Value(map, "priceTierKey"),
            ControlSystemKey = Value(map, "controlSystemKey"),
            ReactorTypeKey = Value(map, "reactorTypeKey"),
            ReactorLevelKey = Value(map, "reactorLevelKey"),
            ArmorThicknessPercent = IntValue(map, "armorThicknessPercent", 100),
            SensorKeys = StringList(map, "sensorKeys"),
            AuxiliaryHullModuleKeys = StringList(map, "auxiliaryHullModuleKeys"),
            Components = ComponentList(map)
        };
        foreach (var raw in ObjectList(map, "engines"))
        {
            var engine = ObjectMap(raw);
            value.Engines.Add(new SpacecraftBlueprintEngineState
            {
                TypeKey = Value(engine, "typeKey"),
                SizeKey = Value(engine, "sizeKey"),
                LevelKey = Value(engine, "levelKey"),
                Quantity = Math.Max(1, IntValue(engine, "quantity", 1))
            });
        }
        return value;
    }

    private static LandMarineBlueprintConfigurationState ParseLandMarineConfiguration(IDictionary<string, object> map)
    {
        return new LandMarineBlueprintConfigurationState
        {
            ConfigurationName = RequiredConfigurationName(map),
            Mode = ConfigurationMode(map),
            TypeKey = Value(map, "typeKey"),
            SizeKey = Value(map, "sizeKey"),
            ClassKey = Value(map, "classKey"),
            QualityKey = Value(map, "qualityKey"),
            LandEngineKey = Value(map, "landEngineKey"),
            LandEngineLevelKey = Value(map, "landEngineLevelKey"),
            WaterEngineKey = Value(map, "waterEngineKey"),
            WaterEngineLevelKey = Value(map, "waterEngineLevelKey"),
            ReactorTypeKey = Value(map, "reactorTypeKey"),
            ReactorLevelKey = Value(map, "reactorLevelKey"),
            PilotSystemKey = Value(map, "pilotSystemKey"),
            PriceTierKey = Value(map, "priceTierKey"),
            ArmorThicknessPercent = IntValue(map, "armorThicknessPercent", 100),
            SensorKeys = StringList(map, "sensorKeys"),
            AuxiliaryHullModuleKeys = StringList(map, "auxiliaryHullModuleKeys"),
            Components = ComponentList(map)
        };
    }

    private static BuildingBlueprintConfigurationState ParseBuildingConfiguration(IDictionary<string, object> map)
    {
        return new BuildingBlueprintConfigurationState
        {
            ConfigurationName = RequiredConfigurationName(map),
            Mode = ConfigurationMode(map),
            BuildingTypeKey = Value(map, "buildingTypeKey"),
            FloorSizeKey = Value(map, "floorSizeKey"),
            FloorCount = IntValue(map, "floorCount", 1),
            ConstructionMethodKey = Value(map, "constructionMethodKey"),
            HullMaterialKey = Value(map, "hullMaterialKey"),
            ArmorMaterialKey = Value(map, "armorMaterialKey"),
            ShieldMaterialKey = Value(map, "shieldMaterialKey"),
            QualityKey = Value(map, "qualityKey"),
            ReactorTypeKey = Value(map, "reactorTypeKey"),
            ReactorLevelKey = Value(map, "reactorLevelKey"),
            LocationDescription = Value(map, "locationDescription"),
            Purpose = Value(map, "purpose"),
            Components = ComponentList(map)
        };
    }

    private static AssetBlueprintCalculationState CalculateAssetBlueprint(AssetBlueprintConfigurationState state)
    {
        CalculationResult result;
        IReadOnlyDictionary<string, decimal> metrics;
        switch (state.Kind)
        {
            case AssetConfiguratorKindIds.Spacecraft:
                var spacecraftResult = new SpacecraftCalculatorService().Calculate(ToSpacecraftInput(state.Spacecraft!));
                result = spacecraftResult;
                metrics = spacecraftResult.Metrics();
                break;
            case AssetConfiguratorKindIds.LandMarine:
                var landResult = new LandMarineCalculatorService().Calculate(ToLandMarineInput(state.LandMarine!));
                result = landResult;
                metrics = landResult.Metrics();
                break;
            case AssetConfiguratorKindIds.Building:
                var buildingResult = new BuildingCalculatorService().Calculate(ToBuildingInput(state.Building!));
                result = buildingResult;
                metrics = buildingResult.Metrics();
                break;
            default:
                throw new ArgumentException("Неизвестный тип конфигуратора.");
        }

        return new AssetBlueprintCalculationState
        {
            IsValid = result.Validation.IsValid,
            TotalCost = result.TotalCost,
            EnergyProduced = result.EnergyProduced,
            EnergyConsumed = result.EnergyConsumed,
            Summary = result.Summary,
            Metrics = metrics.Select(item => new AssetBlueprintMetricState
            {
                Key = item.Key,
                Label = MetricLabel(item.Key),
                Value = item.Value
            }).ToList(),
            Breakdown = result.Breakdown.Select(item => new AssetBlueprintBreakdownState
            {
                Key = item.Key,
                Label = item.Label,
                Value = item.Value,
                Unit = item.Unit,
                Note = item.Note
            }).ToList(),
            Validation = result.Validation.Issues.Select(item => new AssetBlueprintValidationState
            {
                Code = item.Code,
                Message = item.Message,
                Severity = item.Severity.ToString().ToLowerInvariant(),
                Field = item.Field
            }).ToList(),
            Warnings = result.Warnings.Select(item => item.Message).ToList(),
            CalculatedAtUtc = DateTime.UtcNow
        };
    }

    private static SpacecraftInput ToSpacecraftInput(SpacecraftBlueprintConfigurationState value)
    {
        var input = new SpacecraftInput
        {
            ConfigurationName = value.ConfigurationName,
            Mode = ParseMode(value.Mode),
            SizeKey = value.SizeKey,
            ClassKey = value.ClassKey,
            QualityKey = value.QualityKey,
            PriceTierKey = value.PriceTierKey,
            ControlSystemKey = value.ControlSystemKey,
            ReactorTypeKey = value.ReactorTypeKey,
            ReactorLevelKey = value.ReactorLevelKey,
            ArmorThicknessPercent = value.ArmorThicknessPercent
        };
        foreach (var engine in value.Engines)
            input.Engines.Add(new SpacecraftEngineSelection(engine.TypeKey, engine.SizeKey, engine.LevelKey, engine.Quantity));
        foreach (var key in value.SensorKeys) input.SensorKeys.Add(key);
        foreach (var key in value.AuxiliaryHullModuleKeys) input.AuxiliaryHullModuleKeys.Add(key);
        foreach (var component in value.Components) input.Components.Add(ToSelectedComponent(component));
        return input;
    }

    private static LandMarineInput ToLandMarineInput(LandMarineBlueprintConfigurationState value)
    {
        var input = new LandMarineInput
        {
            ConfigurationName = value.ConfigurationName,
            Mode = ParseMode(value.Mode),
            TypeKey = value.TypeKey,
            SizeKey = value.SizeKey,
            ClassKey = value.ClassKey,
            QualityKey = value.QualityKey,
            LandEngineKey = value.LandEngineKey,
            LandEngineLevelKey = value.LandEngineLevelKey,
            WaterEngineKey = value.WaterEngineKey,
            WaterEngineLevelKey = value.WaterEngineLevelKey,
            ReactorTypeKey = value.ReactorTypeKey,
            ReactorLevelKey = value.ReactorLevelKey,
            PilotSystemKey = value.PilotSystemKey,
            PriceTierKey = value.PriceTierKey,
            ArmorThicknessPercent = value.ArmorThicknessPercent
        };
        foreach (var key in value.SensorKeys) input.SensorKeys.Add(key);
        foreach (var key in value.AuxiliaryHullModuleKeys) input.AuxiliaryHullModuleKeys.Add(key);
        foreach (var component in value.Components) input.Components.Add(ToSelectedComponent(component));
        return input;
    }

    private static BuildingInput ToBuildingInput(BuildingBlueprintConfigurationState value)
    {
        var input = new BuildingInput
        {
            ConfigurationName = value.ConfigurationName,
            Mode = ParseMode(value.Mode),
            BuildingTypeKey = value.BuildingTypeKey,
            FloorSizeKey = value.FloorSizeKey,
            FloorCount = value.FloorCount,
            ConstructionMethodKey = value.ConstructionMethodKey,
            HullMaterialKey = value.HullMaterialKey,
            ArmorMaterialKey = value.ArmorMaterialKey,
            ShieldMaterialKey = value.ShieldMaterialKey,
            QualityKey = value.QualityKey,
            ReactorTypeKey = value.ReactorTypeKey,
            ReactorLevelKey = value.ReactorLevelKey,
            LocationDescription = value.LocationDescription,
            Purpose = value.Purpose,
            GmComment = string.Empty
        };
        foreach (var component in value.Components) input.Components.Add(ToSelectedComponent(component));
        return input;
    }

    private static SelectedComponent ToSelectedComponent(AssetBlueprintComponentState value)
    {
        if (!Enum.TryParse(value.Category, true, out AssetComponentCategory category))
            throw new ArgumentException("Неизвестный способ установки компонента.");
        return new SelectedComponent(value.ComponentKey, value.Quantity, category);
    }

    private static AssetConfiguratorMode ParseMode(string value) =>
        string.Equals(value, "nri", StringComparison.OrdinalIgnoreCase)
            ? AssetConfiguratorMode.NriSystemUsse
            : AssetConfiguratorMode.Classic;

    private static Dictionary<string, object> AssetBlueprintPayload(
        AssetConfigurationBlueprintState item,
        bool includeAdminFields)
    {
        var payload = new Dictionary<string, object>
        {
            ["blueprintId"] = item.Id,
            ["name"] = item.Name,
            ["configuratorKind"] = item.ConfiguratorKind,
            ["configuratorKindLabel"] = ConfiguratorKindLabel(item.ConfiguratorKind),
            ["calculationMode"] = item.CalculationMode,
            ["catalogVersion"] = item.CatalogVersion,
            ["configuration"] = AssetConfigurationPayload(item.Configuration),
            ["serverCalculation"] = AssetCalculationPayload(item.ServerCalculation),
            ["readableSummary"] = item.ReadableSummary,
            ["status"] = item.Status,
            ["statusLabel"] = BlueprintStatusLabel(item.Status),
            ["visibility"] = item.Visibility,
            ["revision"] = item.Revision,
            ["createdAtUtc"] = item.CreatedUtc,
            ["updatedAtUtc"] = item.UpdatedUtc,
            ["isArchived"] = item.Archived
        };
        if (includeAdminFields)
        {
            payload["ownerUserId"] = item.OwnerUserId;
            payload["ownerLogin"] = item.OwnerLoginSnapshot;
            payload["ownerCharacterId"] = item.OwnerCharacterId;
            payload["catalogSource"] = item.CatalogSource;
            payload["catalogCommitSha"] = item.CatalogCommitSha;
            payload["adminGmNotes"] = item.AdminGmNotes;
            payload["lastCalculatedBy"] = item.LastCalculatedBy;
        }
        return payload;
    }

    private static Dictionary<string, object> AssetConfigurationPayload(AssetBlueprintConfigurationState state)
    {
        if (state.Spacecraft != null)
        {
            var value = state.Spacecraft;
            return new Dictionary<string, object>
            {
                ["configurationName"] = value.ConfigurationName,
                ["mode"] = value.Mode,
                ["sizeKey"] = value.SizeKey,
                ["classKey"] = value.ClassKey,
                ["qualityKey"] = value.QualityKey,
                ["priceTierKey"] = value.PriceTierKey,
                ["controlSystemKey"] = value.ControlSystemKey,
                ["reactorTypeKey"] = value.ReactorTypeKey,
                ["reactorLevelKey"] = value.ReactorLevelKey,
                ["armorThicknessPercent"] = value.ArmorThicknessPercent,
                ["engines"] = value.Engines.Select(engine => (object)new Dictionary<string, object>
                {
                    ["typeKey"] = engine.TypeKey,
                    ["sizeKey"] = engine.SizeKey,
                    ["levelKey"] = engine.LevelKey,
                    ["quantity"] = engine.Quantity
                }).ToArray(),
                ["sensorKeys"] = value.SensorKeys.Cast<object>().ToArray(),
                ["auxiliaryHullModuleKeys"] = value.AuxiliaryHullModuleKeys.Cast<object>().ToArray(),
                ["components"] = ComponentPayload(value.Components)
            };
        }
        if (state.LandMarine != null)
        {
            var value = state.LandMarine;
            return new Dictionary<string, object>
            {
                ["configurationName"] = value.ConfigurationName,
                ["mode"] = value.Mode,
                ["typeKey"] = value.TypeKey,
                ["sizeKey"] = value.SizeKey,
                ["classKey"] = value.ClassKey,
                ["qualityKey"] = value.QualityKey,
                ["landEngineKey"] = value.LandEngineKey,
                ["landEngineLevelKey"] = value.LandEngineLevelKey,
                ["waterEngineKey"] = value.WaterEngineKey,
                ["waterEngineLevelKey"] = value.WaterEngineLevelKey,
                ["reactorTypeKey"] = value.ReactorTypeKey,
                ["reactorLevelKey"] = value.ReactorLevelKey,
                ["pilotSystemKey"] = value.PilotSystemKey,
                ["priceTierKey"] = value.PriceTierKey,
                ["armorThicknessPercent"] = value.ArmorThicknessPercent,
                ["sensorKeys"] = value.SensorKeys.Cast<object>().ToArray(),
                ["auxiliaryHullModuleKeys"] = value.AuxiliaryHullModuleKeys.Cast<object>().ToArray(),
                ["components"] = ComponentPayload(value.Components)
            };
        }
        var building = state.Building ?? new BuildingBlueprintConfigurationState();
        return new Dictionary<string, object>
        {
            ["configurationName"] = building.ConfigurationName,
            ["mode"] = building.Mode,
            ["buildingTypeKey"] = building.BuildingTypeKey,
            ["floorSizeKey"] = building.FloorSizeKey,
            ["floorCount"] = building.FloorCount,
            ["constructionMethodKey"] = building.ConstructionMethodKey,
            ["hullMaterialKey"] = building.HullMaterialKey,
            ["armorMaterialKey"] = building.ArmorMaterialKey,
            ["shieldMaterialKey"] = building.ShieldMaterialKey,
            ["qualityKey"] = building.QualityKey,
            ["reactorTypeKey"] = building.ReactorTypeKey,
            ["reactorLevelKey"] = building.ReactorLevelKey,
            ["locationDescription"] = building.LocationDescription,
            ["purpose"] = building.Purpose,
            ["components"] = ComponentPayload(building.Components)
        };
    }

    private static object[] ComponentPayload(IEnumerable<AssetBlueprintComponentState> components) =>
        components.Select(component => (object)new Dictionary<string, object>
        {
            ["componentKey"] = component.ComponentKey,
            ["quantity"] = component.Quantity,
            ["category"] = component.Category
        }).ToArray();

    private static Dictionary<string, object> AssetCalculationPayload(AssetBlueprintCalculationState value)
    {
        return new Dictionary<string, object>
        {
            ["isValid"] = value.IsValid,
            ["totalCost"] = value.TotalCost,
            ["energyProduced"] = value.EnergyProduced,
            ["energyConsumed"] = value.EnergyConsumed,
            ["summary"] = value.Summary,
            ["metrics"] = value.Metrics.Select(item => (object)new Dictionary<string, object>
            {
                ["key"] = item.Key,
                ["label"] = item.Label,
                ["value"] = item.Value,
                ["unit"] = item.Unit
            }).ToArray(),
            ["breakdown"] = value.Breakdown.Select(item => (object)new Dictionary<string, object>
            {
                ["key"] = item.Key,
                ["label"] = item.Label,
                ["value"] = item.Value,
                ["unit"] = item.Unit,
                ["note"] = item.Note
            }).ToArray(),
            ["validation"] = value.Validation.Select(item => (object)new Dictionary<string, object>
            {
                ["code"] = item.Code,
                ["message"] = item.Message,
                ["severity"] = item.Severity,
                ["field"] = item.Field
            }).ToArray(),
            ["warnings"] = value.Warnings.Cast<object>().ToArray(),
            ["calculatedAtUtc"] = value.CalculatedAtUtc
        };
    }

    private static List<AssetBlueprintComponentState> ComponentList(IDictionary<string, object> map)
    {
        return ObjectList(map, "components").Select(raw =>
        {
            var component = ObjectMap(raw);
            var quantity = Math.Max(1, IntValue(component, "quantity", 1));
            if (quantity > 1000) throw new ArgumentException("Количество одного компонента не может превышать 1000.");
            return new AssetBlueprintComponentState
            {
                ComponentKey = Value(component, "componentKey"),
                Quantity = quantity,
                Category = Value(component, "category")
            };
        }).ToList();
    }

    private static AssetBlueprintConfigurationState CloneAssetConfiguration(AssetBlueprintConfigurationState source)
    {
        if (source.Spacecraft != null)
            return ParseAssetConfiguration(AssetConfiguratorKindIds.Spacecraft, AssetConfigurationPayload(source));
        if (source.LandMarine != null)
            return ParseAssetConfiguration(AssetConfiguratorKindIds.LandMarine, AssetConfigurationPayload(source));
        return ParseAssetConfiguration(AssetConfiguratorKindIds.Building, AssetConfigurationPayload(source));
    }

    private static string RequiredConfigurationName(IDictionary<string, object> map)
    {
        var value = Value(map, "configurationName").Trim();
        if (value.Length < 2 || value.Length > 160)
            throw new ArgumentException("Название конфигурации должно содержать от 2 до 160 символов.");
        return value;
    }

    private static string ConfigurationMode(IDictionary<string, object> map)
    {
        var value = Value(map, "mode").Trim().ToLowerInvariant();
        return value == "nri" || value == "nrisystemusse" ? "nri" : "classic";
    }

    private static string ConfigurationMode(AssetBlueprintConfigurationState value)
    {
        if (value.Spacecraft != null) return value.Spacecraft.Mode;
        if (value.LandMarine != null) return value.LandMarine.Mode;
        return value.Building?.Mode ?? "classic";
    }

    private static string ConfigurationName(AssetBlueprintConfigurationState value)
    {
        if (value.Spacecraft != null) return value.Spacecraft.ConfigurationName;
        if (value.LandMarine != null) return value.LandMarine.ConfigurationName;
        return value.Building?.ConfigurationName ?? string.Empty;
    }

    private static string Value(IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;

    private static int IntValue(IDictionary<string, object> map, string key, int fallback) =>
        map.TryGetValue(key, out var raw) && int.TryParse(Convert.ToString(raw), out var value) ? value : fallback;

    private static IList<object> ObjectList(IDictionary<string, object> map, string key) =>
        PayloadReader.GetList(map, key) ?? new List<object>();

    private static List<string> StringList(IDictionary<string, object> map, string key) =>
        ObjectList(map, key).Select(Convert.ToString).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList();

    private static Dictionary<string, object> ObjectMap(object raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value!;
            }
            return result;
        }
        var compact = PayloadReader.GetDictionary(
            new Dictionary<string, object> { ["value"] = raw }, "value");
        if (compact != null) return compact;
        throw new ArgumentException("Некорректное описание конфигурации.");
    }

    private static string CatalogSource(string kind)
    {
        switch (kind)
        {
            case AssetConfiguratorKindIds.Spacecraft: return SpacecraftCatalog.Source.RepositoryUrl;
            case AssetConfiguratorKindIds.LandMarine: return LandMarineCatalog.Source.RepositoryUrl;
            default: return BuildingCatalog.Source.RepositoryUrl;
        }
    }

    private static string CatalogVersion(string kind)
    {
        switch (kind)
        {
            case AssetConfiguratorKindIds.Spacecraft: return SpacecraftCatalog.Source.CatalogVersion;
            case AssetConfiguratorKindIds.LandMarine: return LandMarineCatalog.Source.CatalogVersion;
            default: return BuildingCatalog.Source.CatalogVersion;
        }
    }

    private static string CatalogCommitSha(string kind)
    {
        switch (kind)
        {
            case AssetConfiguratorKindIds.Spacecraft: return SpacecraftCatalog.Source.CommitSha;
            case AssetConfiguratorKindIds.LandMarine: return LandMarineCatalog.Source.CommitSha;
            default: return BuildingCatalog.Source.CommitSha;
        }
    }

    private static string ConfiguratorKindLabel(string kind)
    {
        switch (kind)
        {
            case AssetConfiguratorKindIds.Spacecraft: return "Космический корабль или станция";
            case AssetConfiguratorKindIds.LandMarine: return "Наземная или морская техника";
            default: return "Здание или укрепление";
        }
    }

    private static string BlueprintStatusLabel(string status)
    {
        switch (status)
        {
            case AssetBlueprintStatusIds.Ready: return "Готов";
            case AssetBlueprintStatusIds.Archived: return "В архиве";
            default: return "Черновик";
        }
    }

    private static string MetricLabel(string key)
    {
        switch (key)
        {
            case "hull": return "Прочность корпуса";
            case "armor": return "Броня";
            case "shields": return "Щиты";
            case "barrier": return "Барьер";
            case "maneuverability": return "Манёвренность";
            case "landManeuverability": return "Манёвренность на суше";
            case "waterManeuverability": return "Манёвренность на воде";
            case "landSpeed": return "Скорость на суше";
            case "waterSpeed": return "Скорость на воде";
            case "underwaterSpeed": return "Подводная скорость";
            case "totalArea": return "Общая площадь";
            case "structuralIntegrity": return "Прочность конструкции";
            case "armorIntegrity": return "Защита брони";
            case "shieldIntegrity": return "Защита щита";
            default: return key;
        }
    }
}
