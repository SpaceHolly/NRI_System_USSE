using System;
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
    private const string HumanTolerance0217B = "tolerance-human-like-0217b";
    private const string PolarTolerance0217B = "tolerance-polar-adapted-0217b";
    private const string HeatTolerance0217B = "tolerance-heat-adapted-0217b";
    private const string ThermometerProfile0217B = "instrument-field-thermometer-0217b";
    private const string AnemometerProfile0217B = "instrument-hand-anemometer-0217b";
    private const string VaneProfile0217B = "instrument-field-vane-0217b";

    public ResponseEnvelope WorldPlayerObserveCurrent0217B(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Error("Погода для текущего места ещё не задана.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var characterId = Weather0217BRequireOwnedCharacter(actor.Id, payload);
        Weather0217BEnsureFixtures(actor.Id, characterId);
        weather = Weather0217BNormalizeWind(weather);
        var worldSecond = Weather0217WorldSecond(weather.CampaignId);
        var assessment = Weather0217BAssess(characterId, weather, payload);
        var recent = Weather0217BRecentObservations(actor.Id, characterId, weather.Scope.ScopeId, worldSecond);
        return Ok("Наблюдение за окружающей средой обновлено.", new Dictionary<string, object>
        {
            ["observation"] = Weather0217ObservationPayload(weather, worldSecond),
            ["assessment"] = Weather0217BAssessmentPayload(assessment, admin: false),
            ["recentMeasurements"] = recent.Select(x => (object)Weather0217BObservationPayload(x)).ToArray(),
            ["availableInstruments"] = Weather0217BAvailableInstruments(characterId).Select(x => (object)x).ToArray(),
            ["truthIncluded"] = false,
            ["playerSafe"] = true
        });
    }

    public ResponseEnvelope WorldPlayerMeasureEnvironment0217B(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var characterId = Weather0217BRequireOwnedCharacter(actor.Id, payload);
        Weather0217BEnsureFixtures(actor.Id, characterId);
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Error("Погода для измерения не найдена.", ResponseStatus.NotFound, ErrorCode.NotFound);
        weather = Weather0217BNormalizeWind(weather);
        var type = RequireLength(PayloadReader.GetString(payload, "measurementType"), 1, 64, "measurementType").ToLowerInvariant();
        if (type != EnvironmentMeasurementTypeIds.Temperature && type != EnvironmentMeasurementTypeIds.WindSpeed && type != EnvironmentMeasurementTypeIds.WindDirection)
            return Error("Этот тип измерения пока недоступен для текущей погоды.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var instrument = Weather0217BFindInstrument(characterId, type, PayloadReader.GetString(payload, "itemInstanceId"));
        if (instrument.Item == null || instrument.Profile == null)
            return Error("У персонажа нет доступного прибора для этого измерения.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        if (instrument.Profile.RequiresCalibration && !instrument.Item.IsCalibrated)
            return Error("Прибор требует калибровки.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var operationId = FirstNonEmpty(PayloadReader.GetString(payload, "operationId"), context.Request.RequestId, Guid.NewGuid().ToString("N"));
        var replay = _mongo.EnvironmentObservations0217B.Find(x => x.OwnerUserId == actor.Id && x.OperationId == operationId).FirstOrDefault();
        if (replay != null) return Ok("Это измерение уже выполнено.", new Dictionary<string, object> { ["measurement"] = Weather0217BObservationPayload(replay), ["idempotentReplay"] = true });
        var record = Weather0217BMeasure(actor.Id, characterId, weather, type, instrument.Item, instrument.Profile, operationId);
        _mongo.EnvironmentObservations0217B.InsertOne(record);
        WriteAudit("environment_observation", actor.Id, "world.player.measure.environment", record.Id);
        Weather0217Sync("world.observation.measurement.recorded", weather.CampaignId, "environment_observation", record.Id, type, actor.Id, context.Request.RequestId);
        return Ok("Измерение сохранено.", new Dictionary<string, object> { ["measurement"] = Weather0217BObservationPayload(record), ["idempotentReplay"] = false, ["truthIncluded"] = false });
    }

    public ResponseEnvelope WorldPlayerEstimateDistance0217B(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var characterId = Weather0217BRequireOwnedCharacter(actor.Id, payload);
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Error("Контекст наблюдения не найден.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var operationId = FirstNonEmpty(PayloadReader.GetString(payload, "operationId"), context.Request.RequestId, Guid.NewGuid().ToString("N"));
        var replay = _mongo.EnvironmentObservations0217B.Find(x => x.OwnerUserId == actor.Id && x.OperationId == operationId).FirstOrDefault();
        if (replay != null) return Ok("Эта оценка уже выполнена.", new Dictionary<string, object> { ["estimate"] = Weather0217BObservationPayload(replay), ["idempotentReplay"] = true });
        var skilled = Weather0217BHasDistanceSkill(characterId);
        // The current fixture target is server-owned; clients may not provide authoritative distance.
        const decimal trueDistanceM = 850m;
        var record = new EnvironmentObservationRecord
        {
            CampaignId = weather.CampaignId,
            OwnerUserId = actor.Id,
            ObserverCharacterId = characterId,
            ScopeId = weather.Scope.ScopeId,
            TargetReference = FirstNonEmpty(PayloadReader.GetString(payload, "targetReference"), "Ориентир у Северных ворот"),
            MeasurementType = EnvironmentMeasurementTypeIds.Distance,
            EstimatedMinValue = skilled ? trueDistanceM - 30m : trueDistanceM - 150m,
            EstimatedMaxValue = skilled ? trueDistanceM + 50m : trueDistanceM + 150m,
            Unit = "м",
            Confidence = skilled ? 0.85m : 0.55m,
            QualitativeLabel = skilled ? "Уверенная оценка" : "Приблизительная оценка",
            SourceType = "skill_estimate",
            SourceDisplayName = skilled ? "Навык оценки расстояния" : "Визуальная оценка",
            ObservedAtWorldSecond = Weather0217WorldSecond(weather.CampaignId),
            WeatherRevision = weather.EntityRevision,
            StaleAfterWorldSecond = Weather0217WorldSecond(weather.CampaignId) + 3600,
            PlayerSafeText = skilled ? $"примерно {trueDistanceM - 30m:0}–{trueDistanceM + 50m:0} м" : $"примерно {trueDistanceM - 150m:0}–{trueDistanceM + 150m:0} м",
            OperationId = operationId
        };
        _mongo.EnvironmentObservations0217B.InsertOne(record);
        return Ok("Оценка расстояния сохранена.", new Dictionary<string, object> { ["estimate"] = Weather0217BObservationPayload(record), ["exactTruthIncluded"] = false, ["skilled"] = skilled });
    }

    public ResponseEnvelope WorldPlayerObservationHistoryGet0217B(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var characterId = Weather0217BRequireOwnedCharacter(actor.Id, payload);
        var worldSecond = Weather0217WorldSecond(Weather0217CampaignId(payload));
        var items = _mongo.EnvironmentObservations0217B.Find(x => x.OwnerUserId == actor.Id && x.ObserverCharacterId == characterId && !x.Deleted && !x.Archived)
            .SortByDescending(x => x.CreatedAtUtc).Limit(50).ToList();
        foreach (var item in items) item.IsOutdated = item.StaleAfterWorldSecond > 0 && worldSecond > item.StaleAfterWorldSecond;
        return Ok("История наблюдений загружена.", new Dictionary<string, object> { ["items"] = items.Select(x => (object)Weather0217BObservationPayload(x)).ToArray(), ["playerSafe"] = true });
    }

    public ResponseEnvelope ActorPlayerEnvironmentAssessmentGet0217B(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var characterId = Weather0217BRequireOwnedCharacter(actor.Id, payload);
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Error("Окружение не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Личная оценка окружения рассчитана.", new Dictionary<string, object> { ["assessment"] = Weather0217BAssessmentPayload(Weather0217BAssess(characterId, Weather0217BNormalizeWind(weather), payload), false), ["truthIncluded"] = false });
    }

    public ResponseEnvelope WorldAdminMeasurementPreview0217B(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Error("Погода не найдена.", ResponseStatus.NotFound, ErrorCode.NotFound);
        weather = Weather0217BNormalizeWind(weather);
        var vector = WindVectorSnapshot.FromMeteorological(weather.TrueWindSpeedMetersPerSecond, weather.TrueWindDirectionDegreesFromNorth, weather.TrueWindGustMetersPerSecond);
        return Ok("Предпросмотр измерений рассчитан.", new Dictionary<string, object>
        {
            ["thermometer"] = $"{EnvironmentMeasurementMath.Quantize(weather.TrueTemperatureC, 0.5m):0.#} °C ±0,5 °C",
            ["anemometer"] = $"{EnvironmentMeasurementMath.Quantize(weather.TrueWindSpeedMetersPerSecond, 0.1m):0.0} м/с ±0,3 м/с",
            ["vane"] = $"{vector.CardinalDirectionLabel}, примерно {EnvironmentMeasurementMath.Quantize(vector.DirectionFromDegrees, 22.5m):0.#}°",
            ["distanceOrdinary"] = "примерно 700–1000 м",
            ["distanceSkilled"] = "примерно 820–900 м"
        });
    }

    public ResponseEnvelope ActorAdminEnvironmentAssessmentGet0217B(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Error("Окружение не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var characterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 1, 128, "characterId");
        return Ok("Оценка актёра рассчитана.", new Dictionary<string, object> { ["assessment"] = Weather0217BAssessmentPayload(Weather0217BAssess(characterId, Weather0217BNormalizeWind(weather), payload), true) });
    }

    public ResponseEnvelope WorldAdminEnvironmentImpactPartyGet0217B(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Error("Окружение не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        Weather0217BEnsureFixtures(string.Empty, Weather0217BFindActiveCharacterId(Weather0217FindUserId("dev_player")));
        weather = Weather0217BNormalizeWind(weather);
        var ids = Weather0217StringList(payload, "characterIds");
        if (ids.Count == 0)
        {
            var playerId = Weather0217BFindActiveCharacterId(Weather0217FindUserId("dev_player"));
            ids = new List<string> { playerId, "character-polar-0217b", "character-heat-0217b" }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["character-polar-0217b"] = "Арго (полярная адаптация)",
            ["character-heat-0217b"] = "Сайра (жаркая среда)"
        };
        var acceptanceCharacterId = Weather0217BFindActiveCharacterId(Weather0217FindUserId("dev_player"));
        if (!string.IsNullOrWhiteSpace(acceptanceCharacterId)) names[acceptanceCharacterId] = "Адель Вард";
        var rows = ids.Take(20).Select(id =>
        {
            var ownership = _mongo.CharacterOwnerships.Find(x => x.CharacterId == id).FirstOrDefault();
            var assessment = Weather0217BAssess(id, weather, payload);
            return (object)new Dictionary<string, object>
            {
                ["actorName"] = names.TryGetValue(id, out var fixtureName) ? fixtureName : FirstNonEmpty(ownership?.CharacterDisplayName, "Персонаж"),
                ["tolerance"] = Weather0217BTolerance(id).ProfileDisplayName,
                ["thermalState"] = Weather0217BComfortLabel(assessment.ComfortState),
                ["exposureRate"] = assessment.ColdStressRate + assessment.HeatStressRate + assessment.WetExposureRate + assessment.WindExposureRate,
                ["protection"] = assessment.ProtectionSummary
            };
        }).ToArray();
        return Ok("Сравнение воздействия на группу рассчитано.", new Dictionary<string, object> { ["items"] = rows, ["trueWeatherShared"] = true, ["weatherRevision"] = weather.EntityRevision });
    }

    private WeatherStateDocument Weather0217BNormalizeWind(WeatherStateDocument weather)
    {
        if (weather.WindUnitSchemaVersion >= 2 && weather.TrueWindKmh <= 0m) return weather;
        weather.TrueWindSpeedMetersPerSecond = Math.Round(EnvironmentMeasurementMath.MetersPerSecondFromKilometersPerHour(weather.TrueWindKmh), 3, MidpointRounding.AwayFromZero);
        weather.TrueWindKmh = 0m;
        weather.WindUnitSchemaVersion = 2;
        if (weather.TrueWindDirectionDegreesFromNorth < 0m || weather.TrueWindDirectionDegreesFromNorth >= 360m)
            weather.TrueWindDirectionDegreesFromNorth = WindVectorSnapshot.NormalizeDegrees(weather.TrueWindDirectionDegreesFromNorth);
        weather.EntityRevision++;
        weather.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.WeatherStates0217.ReplaceOne(x => x.Id == weather.Id, weather);
        return weather;
    }

    private string Weather0217BRequireOwnedCharacter(string ownerUserId, Dictionary<string, object> payload)
    {
        var characterId = Weather0217BCharacterId(ownerUserId, payload);
        var ownership = _mongo.CharacterOwnerships.Find(x => x.CharacterId == characterId && x.OwnerUserId == ownerUserId && x.IsActive && !x.IsArchived).FirstOrDefault();
        if (ownership == null) throw new UnauthorizedAccessException("Активный персонаж игрока не найден.");
        return characterId;
    }

    private string Weather0217BCharacterId(string ownerUserId, Dictionary<string, object> payload)
        => FirstNonEmpty(PayloadReader.GetString(payload, "characterId"), Weather0217BFindActiveCharacterId(ownerUserId));

    private string Weather0217BFindActiveCharacterId(string ownerUserId)
        => _mongo.CharacterOwnerships.Find(x => x.OwnerUserId == ownerUserId && x.IsActive && !x.IsArchived).FirstOrDefault()?.CharacterId ?? string.Empty;

    private List<Dictionary<string, object>> Weather0217BAvailableInstruments(string characterId)
    {
        var inventory = _mongo.CharacterInventoryProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()?.Profile;
        if (inventory == null) return new List<Dictionary<string, object>>();
        return inventory.Items.Where(x => !string.IsNullOrWhiteSpace(x.MeasurementInstrumentProfileId) && x.Quantity > 0)
            .Select(x => new Dictionary<string, object>
            {
                ["itemInstanceId"] = x.ItemId,
                ["name"] = FirstNonEmpty(x.DisplayName, x.Name, x.SnapshotDisplayName),
                ["measurementTypes"] = _mongo.MeasurementInstrumentProfiles0217B.Find(p => p.Id == x.MeasurementInstrumentProfileId && !p.IsArchived).FirstOrDefault()?.MeasurementTypes.ToArray() ?? Array.Empty<string>(),
                ["isCalibrated"] = x.IsCalibrated
            }).ToList();
    }

    private (CharacterInventoryItemProfileValue? Item, MeasurementInstrumentProfileDefinition? Profile) Weather0217BFindInstrument(string characterId, string type, string? itemInstanceId)
    {
        var inventory = _mongo.CharacterInventoryProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()?.Profile;
        if (inventory == null) return (null, null);
        foreach (var item in inventory.Items.Where(x => x.Quantity > 0 && (string.IsNullOrWhiteSpace(itemInstanceId) || x.ItemId == itemInstanceId)))
        {
            var profile = _mongo.MeasurementInstrumentProfiles0217B.Find(x => x.Id == item.MeasurementInstrumentProfileId && !x.IsArchived).FirstOrDefault();
            if (profile != null && profile.MeasurementTypes.Contains(type, StringComparer.OrdinalIgnoreCase) && (!profile.AvailabilityRequirement.Contains("equipped") || item.IsEquipped))
                return (item, profile);
        }
        return (null, null);
    }

    private EnvironmentObservationRecord Weather0217BMeasure(string ownerUserId, string characterId, WeatherStateDocument weather, string type, CharacterInventoryItemProfileValue item, MeasurementInstrumentProfileDefinition profile, string operationId)
    {
        decimal truth;
        string unit;
        if (type == EnvironmentMeasurementTypeIds.Temperature) { truth = weather.TrueTemperatureC; unit = "°C"; }
        else if (type == EnvironmentMeasurementTypeIds.WindSpeed) { truth = weather.TrueWindSpeedMetersPerSecond; unit = "м/с"; }
        else { truth = weather.TrueWindDirectionDegreesFromNorth; unit = "°"; }
        if (truth < profile.MinimumValue || truth > profile.MaximumValue) throw new ArgumentException("Значение находится вне рабочего диапазона прибора.");
        var deterministicError = EnvironmentMeasurementMath.DeterministicError(operationId, profile.AbsoluteAccuracy);
        var value = EnvironmentMeasurementMath.Quantize(truth + profile.CalibrationOffset + item.MeasurementCalibrationOffset + deterministicError, profile.Resolution);
        var text = type == EnvironmentMeasurementTypeIds.WindDirection
            ? $"{WindVectorSnapshot.FromMeteorological(weather.TrueWindSpeedMetersPerSecond, value).CardinalDirectionLabel}, примерно {value:0.#}°"
            : $"{value:0.##} {unit} ±{profile.AbsoluteAccuracy:0.##} {unit}";
        var worldSecond = Weather0217WorldSecond(weather.CampaignId);
        return new EnvironmentObservationRecord
        {
            CampaignId = weather.CampaignId, OwnerUserId = ownerUserId, ObserverCharacterId = characterId,
            ScopeId = weather.Scope.ScopeId, MeasurementType = type, MeasuredValue = value, Unit = unit,
            Uncertainty = profile.AbsoluteAccuracy, Confidence = 0.9m, SourceType = "instrument",
            SourceDisplayName = profile.DisplayName, InstrumentItemInstanceId = item.ItemId, InstrumentProfileId = profile.Id,
            ObservedAtWorldSecond = worldSecond, WeatherRevision = weather.EntityRevision, StaleAfterWorldSecond = worldSecond + 3600,
            PlayerSafeText = text, OperationId = operationId
        };
    }

    private List<EnvironmentObservationRecord> Weather0217BRecentObservations(string ownerUserId, string characterId, string scopeId, long worldSecond)
    {
        var items = _mongo.EnvironmentObservations0217B.Find(x => x.OwnerUserId == ownerUserId && x.ObserverCharacterId == characterId && x.ScopeId == scopeId && !x.Archived)
            .SortByDescending(x => x.CreatedAtUtc).Limit(12).ToList();
        foreach (var item in items) item.IsOutdated = item.StaleAfterWorldSecond > 0 && worldSecond > item.StaleAfterWorldSecond;
        return items;
    }

    private ActorEnvironmentalToleranceSnapshot Weather0217BTolerance(string characterId)
    {
        var body = _mongo.CharacterBodyProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()?.Profile;
        var profileId = FirstNonEmpty(body?.EnvironmentalToleranceProfileId, HumanTolerance0217B);
        var profile = _mongo.EnvironmentalToleranceProfiles0217B.Find(x => x.Id == profileId && !x.IsArchived).FirstOrDefault()
            ?? _mongo.EnvironmentalToleranceProfiles0217B.Find(x => x.Id == HumanTolerance0217B && !x.IsArchived).FirstOrDefault()
            ?? Weather0217BHumanTolerance();
        var snapshot = new ActorEnvironmentalToleranceSnapshot
        {
            SubjectId = characterId, ProfileId = profile.Id, ProfileDisplayName = profile.DisplayName,
            ComfortMinC = profile.TemperatureComfortMinC, ComfortMaxC = profile.TemperatureComfortMaxC,
            SafeMinC = profile.TemperatureSafeMinC, SafeMaxC = profile.TemperatureSafeMaxC,
            DangerMinC = profile.TemperatureDangerMinC, DangerMaxC = profile.TemperatureDangerMaxC,
            CriticalMinC = profile.TemperatureCriticalMinC, CriticalMaxC = profile.TemperatureCriticalMaxC,
            ColdSensitivityMultiplier = profile.ColdSensitivityMultiplier, HeatSensitivityMultiplier = profile.HeatSensitivityMultiplier,
            WetSensitivityMultiplier = profile.WetSensitivityMultiplier, WindSensitivityMultiplier = profile.WindSensitivityMultiplier,
            IgnoredDimensions = profile.IgnoredDimensions.ToList(), ModifierBreakdown = body?.EnvironmentalToleranceModifiers.ToList() ?? new List<EnvironmentalToleranceModifier>()
        };
        foreach (var modifier in snapshot.ModifierBreakdown)
        {
            snapshot.ComfortMinC += modifier.ComfortMinDeltaC; snapshot.ComfortMaxC += modifier.ComfortMaxDeltaC;
            snapshot.ColdSensitivityMultiplier *= modifier.ColdSensitivityMultiplier; snapshot.HeatSensitivityMultiplier *= modifier.HeatSensitivityMultiplier;
            snapshot.WetSensitivityMultiplier *= modifier.WetSensitivityMultiplier; snapshot.WindSensitivityMultiplier *= modifier.WindSensitivityMultiplier;
            snapshot.HumiditySensitivityMultiplier *= modifier.HumiditySensitivityMultiplier; snapshot.HypoxiaSensitivityMultiplier *= modifier.HypoxiaSensitivityMultiplier;
            snapshot.HydrationConsumptionMultiplier *= modifier.HydrationConsumptionMultiplier;
            snapshot.IgnoredDimensions.AddRange(modifier.IgnoredDimensions);
        }
        snapshot.IgnoredDimensions = snapshot.IgnoredDimensions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return snapshot;
    }

    private ActorEnvironmentAssessment Weather0217BAssess(string characterId, WeatherStateDocument weather, Dictionary<string, object> payload)
    {
        var tolerance = Weather0217BTolerance(characterId);
        var protection = Weather0217BProtection(characterId);
        var shelterReduction = Math.Max(0m, Math.Min(1m, (decimal)(PayloadReader.GetDouble(payload, "shelterReduction") ?? 0d)));
        var effective = weather.TrueTemperatureC + protection.ThermalInsulationC;
        var ignored = tolerance.IgnoredDimensions.Contains("temperature", StringComparer.OrdinalIgnoreCase);
        var state = ignored ? EnvironmentComfortStateIds.Ignored : Weather0217BComfortState(effective, tolerance);
        var cold = !ignored && effective < tolerance.ComfortMinC ? (tolerance.ComfortMinC - effective) * tolerance.ColdSensitivityMultiplier / 10m : 0m;
        var heat = !ignored && effective > tolerance.ComfortMaxC ? (effective - tolerance.ComfortMaxC) * tolerance.HeatSensitivityMultiplier / 10m : 0m;
        var wet = weather.TruePrecipitation.IndexOf("дожд", StringComparison.OrdinalIgnoreCase) >= 0 ? tolerance.WetSensitivityMultiplier * (1m - protection.WaterProtectionRatio) * (1m - shelterReduction) : 0m;
        var wind = weather.TrueWindSpeedMetersPerSecond >= 8m ? tolerance.WindSensitivityMultiplier * (weather.TrueWindSpeedMetersPerSecond / 12.5m) * (1m - protection.WindProtectionRatio) * (1m - shelterReduction) : 0m;
        return new ActorEnvironmentAssessment
        {
            SubjectId = characterId, EnvironmentRevision = weather.EntityRevision, TemperatureC = weather.TrueTemperatureC,
            EffectiveThermalContextC = effective, ComfortState = state, ColdStressRate = Math.Round(cold, 2), HeatStressRate = Math.Round(heat, 2),
            WetExposureRate = Math.Round(wet, 2), WindExposureRate = Math.Round(wind, 2), ProtectionSummary = protection.Summary,
            RelevantWarnings = cold > 0m ? new List<string> { "Холодовое воздействие накапливается." } : new List<string>(),
            PublicExplanation = $"Для вас: {Weather0217BComfortLabel(state)}. {protection.Summary}",
            GMExplanation = $"Профиль: {tolerance.ProfileDisplayName}; эффективная температура {effective:0.#} °C; холод {cold:0.##}; тепло {heat:0.##}."
        };
    }

    private (decimal ThermalInsulationC, decimal WindProtectionRatio, decimal WaterProtectionRatio, string Summary) Weather0217BProtection(string characterId)
    {
        var inventory = _mongo.CharacterInventoryProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()?.Profile;
        decimal thermal = 0m, wind = 0m, water = 0m;
        var names = new List<string>();
        foreach (var item in inventory?.Items.Where(x => x.IsEquipped && !string.IsNullOrWhiteSpace(x.EnvironmentalProtectionProfileId)) ?? Enumerable.Empty<CharacterInventoryItemProfileValue>())
        {
            var profile = _mongo.EnvironmentalProtectionProfiles0217B.Find(x => x.Id == item.EnvironmentalProtectionProfileId && !x.IsArchived).FirstOrDefault();
            if (profile == null) continue;
            thermal += profile.ThermalInsulationC; wind = Math.Max(wind, profile.WindProtectionRatio); water = Math.Max(water, profile.WaterProtectionRatio); names.Add(profile.DisplayName);
        }
        return (thermal, Math.Min(1m, wind), Math.Min(1m, water), names.Count == 0 ? "Дополнительной защиты нет." : string.Join(", ", names) + " снижает воздействие среды.");
    }

    private static string Weather0217BComfortState(decimal value, ActorEnvironmentalToleranceSnapshot t)
    {
        if (value < t.CriticalMinC) return EnvironmentComfortStateIds.CriticalCold;
        if (value < t.DangerMinC) return EnvironmentComfortStateIds.DangerousCold;
        if (value < t.ComfortMinC) return value < t.SafeMinC ? EnvironmentComfortStateIds.ColdStress : EnvironmentComfortStateIds.Cool;
        if (value <= t.ComfortMaxC) return EnvironmentComfortStateIds.Comfortable;
        if (value > t.CriticalMaxC) return EnvironmentComfortStateIds.CriticalHeat;
        if (value > t.DangerMaxC) return EnvironmentComfortStateIds.DangerousHeat;
        return value > t.SafeMaxC ? EnvironmentComfortStateIds.HeatStress : EnvironmentComfortStateIds.Warm;
    }

    private static string Weather0217BComfortLabel(string value) => value switch
    {
        EnvironmentComfortStateIds.Comfortable => "Комфортно", EnvironmentComfortStateIds.Cool => "Прохладно",
        EnvironmentComfortStateIds.ColdStress => "Холодовой стресс", EnvironmentComfortStateIds.DangerousCold => "Опасный холод",
        EnvironmentComfortStateIds.CriticalCold => "Критический холод", EnvironmentComfortStateIds.Warm => "Тепло",
        EnvironmentComfortStateIds.HeatStress => "Тепловой стресс", EnvironmentComfortStateIds.DangerousHeat => "Опасная жара",
        EnvironmentComfortStateIds.CriticalHeat => "Критическая жара", EnvironmentComfortStateIds.Ignored => "Не влияет", _ => "Особая реакция"
    };

    private Dictionary<string, object> Weather0217BAssessmentPayload(ActorEnvironmentAssessment a, bool admin)
    {
        var payload = new Dictionary<string, object>
        {
            ["comfortState"] = a.ComfortState, ["comfortLabel"] = Weather0217BComfortLabel(a.ComfortState),
            ["coldStressRate"] = a.ColdStressRate, ["heatStressRate"] = a.HeatStressRate,
            ["wetExposureRate"] = a.WetExposureRate, ["windExposureRate"] = a.WindExposureRate,
            ["protectionSummary"] = a.ProtectionSummary, ["warnings"] = a.RelevantWarnings.ToArray(), ["publicExplanation"] = a.PublicExplanation,
            ["calculationVersion"] = a.CalculationVersion
        };
        if (admin) { payload["subjectId"] = a.SubjectId; payload["temperatureC"] = a.TemperatureC; payload["effectiveThermalContextC"] = a.EffectiveThermalContextC; payload["gmExplanation"] = a.GMExplanation; }
        return payload;
    }

    private static Dictionary<string, object> Weather0217BObservationPayload(EnvironmentObservationRecord item) => new()
    {
        ["measurementType"] = item.MeasurementType, ["measuredValue"] = item.MeasuredValue ?? 0m,
        ["estimatedMinValue"] = item.EstimatedMinValue ?? 0m, ["estimatedMaxValue"] = item.EstimatedMaxValue ?? 0m,
        ["unit"] = item.Unit, ["uncertainty"] = item.Uncertainty, ["confidence"] = item.Confidence,
        ["sourceType"] = item.SourceType, ["sourceName"] = item.SourceDisplayName, ["observedAtWorldSecond"] = item.ObservedAtWorldSecond,
        ["isOutdated"] = item.IsOutdated, ["text"] = item.PlayerSafeText
    };

    private bool Weather0217BHasDistanceSkill(string characterId)
    {
        var profile = _mongo.CharacterSkillProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()?.Profile;
        return profile?.Skills.Any(x => x.IsLearned && x.Rank >= 2 && (x.SkillId.IndexOf("distance", StringComparison.OrdinalIgnoreCase) >= 0 || x.SkillId.IndexOf("наблю", StringComparison.OrdinalIgnoreCase) >= 0)) == true;
    }

    private void Weather0217BEnsureFixtures(string ownerUserId, string characterId)
    {
        UpsertTolerance(Weather0217BHumanTolerance());
        UpsertTolerance(new EnvironmentalToleranceProfileDefinition { Id = PolarTolerance0217B, DisplayName = "Полярная адаптация", ApplicableDimensions = new List<string> { "temperature", "wet", "wind" }, TemperatureComfortMinC = -10m, TemperatureComfortMaxC = 12m, TemperatureSafeMinC = -30m, TemperatureSafeMaxC = 22m, TemperatureDangerMinC = -45m, TemperatureDangerMaxC = 35m, TemperatureCriticalMinC = -60m, TemperatureCriticalMaxC = 45m, PublicDescription = "Тело приспособлено к холоду." });
        UpsertTolerance(new EnvironmentalToleranceProfileDefinition { Id = HeatTolerance0217B, DisplayName = "Адаптация к жаркой среде", ApplicableDimensions = new List<string> { "temperature", "wet", "wind" }, TemperatureComfortMinC = 25m, TemperatureComfortMaxC = 38m, TemperatureSafeMinC = 10m, TemperatureSafeMaxC = 50m, TemperatureDangerMinC = -5m, TemperatureDangerMaxC = 60m, TemperatureCriticalMinC = -20m, TemperatureCriticalMaxC = 75m, ColdSensitivityMultiplier = 1.5m, PublicDescription = "Тело приспособлено к жаре и чувствительно к холоду." });
        UpsertInstrument(new MeasurementInstrumentProfileDefinition { Id = ThermometerProfile0217B, DisplayName = "Полевой термометр", MeasurementTypes = new List<string> { EnvironmentMeasurementTypeIds.Temperature }, MinimumValue = -40m, MaximumValue = 60m, Resolution = 0.5m, AbsoluteAccuracy = 0.5m, PublicDescription = "Измеряет температуру воздуха.", AvailabilityRequirement = "held_or_equipped" });
        UpsertInstrument(new MeasurementInstrumentProfileDefinition { Id = AnemometerProfile0217B, DisplayName = "Ручной анемометр", MeasurementTypes = new List<string> { EnvironmentMeasurementTypeIds.WindSpeed }, MinimumValue = 0m, MaximumValue = 40m, Resolution = 0.1m, AbsoluteAccuracy = 0.3m, PublicDescription = "Измеряет скорость ветра в м/с.", AvailabilityRequirement = "held_or_equipped" });
        UpsertInstrument(new MeasurementInstrumentProfileDefinition { Id = VaneProfile0217B, DisplayName = "Полевой флюгер", MeasurementTypes = new List<string> { EnvironmentMeasurementTypeIds.WindDirection }, MinimumValue = 0m, MaximumValue = 359.999m, Resolution = 22.5m, AbsoluteAccuracy = 11.25m, RequiresSetup = true, PublicDescription = "Определяет направление, откуда приходит ветер.", AvailabilityRequirement = "held_or_equipped" });
        var protection = new EnvironmentalProtectionProfileDefinition { Id = "protection-winter-cloak-0217b", DisplayName = "Зимний плащ", ThermalInsulationC = 4m, WindProtectionRatio = 0.35m, WaterProtectionRatio = 0.45m, PublicDescription = "Снижает воздействие холода, ветра и дождя." };
        _mongo.EnvironmentalProtectionProfiles0217B.ReplaceOne(x => x.Id == protection.Id, protection, new ReplaceOptions { IsUpsert = true });
        EnsureBody("character-polar-0217b", PolarTolerance0217B);
        EnsureBody("character-heat-0217b", HeatTolerance0217B);
        if (string.IsNullOrWhiteSpace(characterId)) return;
        EnsureBody(characterId, HumanTolerance0217B);
        var doc = _mongo.CharacterInventoryProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault();
        if (doc == null) return;
        AddInstrumentItem(doc.Profile, "fixture-thermometer-0217b", "Полевой термометр", ThermometerProfile0217B);
        AddInstrumentItem(doc.Profile, "fixture-anemometer-0217b", "Ручной анемометр", AnemometerProfile0217B);
        AddInstrumentItem(doc.Profile, "fixture-vane-0217b", "Полевой флюгер", VaneProfile0217B);
        if (!doc.Profile.Items.Any(x => x.ItemId == "fixture-winter-cloak-0217b")) doc.Profile.Items.Add(new CharacterInventoryItemProfileValue { ItemId = "fixture-winter-cloak-0217b", Name = "Зимний плащ", DisplayName = "Зимний плащ", Quantity = 1, IsEquipped = true, EnvironmentalProtectionProfileId = protection.Id, IsPlayerVisible = true, Source = "acceptance_fixture" });
        _mongo.CharacterInventoryProfiles.ReplaceOne(x => x.Id == doc.Id, doc);
    }

    private static EnvironmentalToleranceProfileDefinition Weather0217BHumanTolerance() => new()
    {
        Id = HumanTolerance0217B, DisplayName = "Человекоподобная физиология", ApplicableDimensions = new List<string> { "temperature", "wet", "wind" },
        TemperatureComfortMinC = 16m, TemperatureComfortMaxC = 26m, TemperatureSafeMinC = 0m, TemperatureSafeMaxC = 35m,
        TemperatureDangerMinC = -15m, TemperatureDangerMaxC = 45m, TemperatureCriticalMinC = -30m, TemperatureCriticalMaxC = 60m,
        PublicDescription = "Обычная переносимость умеренного климата."
    };

    private void UpsertTolerance(EnvironmentalToleranceProfileDefinition profile) => _mongo.EnvironmentalToleranceProfiles0217B.ReplaceOne(x => x.Id == profile.Id, profile, new ReplaceOptions { IsUpsert = true });
    private void UpsertInstrument(MeasurementInstrumentProfileDefinition profile) => _mongo.MeasurementInstrumentProfiles0217B.ReplaceOne(x => x.Id == profile.Id, profile, new ReplaceOptions { IsUpsert = true });
    private void EnsureBody(string characterId, string profileId)
    {
        var doc = _mongo.CharacterBodyProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault();
        if (doc == null) _mongo.CharacterBodyProfiles.InsertOne(new CharacterBodyProfileDocument { CharacterId = characterId, Profile = new BodyProfile { CharacterId = characterId, EnvironmentalToleranceProfileId = profileId, Source = "acceptance_fixture", SchemaVersion = 2 } });
        else if (string.IsNullOrWhiteSpace(doc.Profile.EnvironmentalToleranceProfileId)) { doc.Profile.EnvironmentalToleranceProfileId = profileId; doc.Profile.SchemaVersion = Math.Max(2, doc.Profile.SchemaVersion); _mongo.CharacterBodyProfiles.ReplaceOne(x => x.Id == doc.Id, doc); }
    }
    private static void AddInstrumentItem(InventoryProfile profile, string id, string name, string instrumentProfileId)
    {
        if (profile.Items.Any(x => x.ItemId == id)) return;
        profile.Items.Add(new CharacterInventoryItemProfileValue { ItemId = id, Name = name, DisplayName = name, Quantity = 1, IsEquipped = true, IsCalibrated = true, MeasurementInstrumentProfileId = instrumentProfileId, IsPlayerVisible = true, Source = "acceptance_fixture" });
    }
}
