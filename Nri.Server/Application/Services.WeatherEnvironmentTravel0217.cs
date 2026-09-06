using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string Weather0217ForecastKnowledgeId = "weather_forecast_0217";
    private const string Weather0217ExposureAction = "environment.exposure.resolve";

    public ResponseEnvelope WorldPlayerWeatherGet0217(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Ok("Погода для текущего места ещё не задана.", new Dictionary<string, object> { ["hasWeather"] = false });
        var worldSecond = Weather0217WorldSecond(weather.CampaignId);
        return Ok("Наблюдаемая погода загружена.", new Dictionary<string, object>
        {
            ["hasWeather"] = true,
            ["weather"] = Weather0217ObservationPayload(weather, worldSecond),
            ["environment"] = Weather0217EnvironmentPayload(Weather0217BuildEnvironment(weather, worldSecond, payload), admin: false),
            ["forecast"] = Weather0217PlayerForecast(actor, weather.CampaignId, weather.Scope.ScopeId),
            ["playerSafe"] = true
        });
    }

    public ResponseEnvelope WorldPlayerEnvironmentGet0217(CommandContext context) => WorldPlayerWeatherGet0217(context);

    public ResponseEnvelope WorldPlayerForecastGet0217(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Weather0217CampaignId(payload);
        return Ok("Известный прогноз загружен.", new Dictionary<string, object>
        {
            ["items"] = Weather0217PlayerForecasts(actor, campaignId),
            ["playerSafe"] = true
        });
    }

    public ResponseEnvelope WorldPlayerTravelGet0217(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Weather0217CampaignId(payload);
        var travelId = PayloadReader.GetString(payload, "travelId") ?? string.Empty;
        var filter = Builders<TravelSession>.Filter.Eq(x => x.CampaignId, campaignId)
                     & Builders<TravelSession>.Filter.AnyEq(x => x.PartyOwnerUserIds, actor.Id)
                     & Builders<TravelSession>.Filter.Ne(x => x.Status, TravelStatusIds.Archived);
        if (!string.IsNullOrWhiteSpace(travelId)) filter &= Builders<TravelSession>.Filter.Eq(x => x.Id, travelId);
        var items = _mongo.TravelSessions0217.Find(filter).SortByDescending(x => x.UpdatedAtUtc).Limit(20).ToList();
        return Ok(items.Count == 0 ? "Активных путешествий нет." : "Путешествие загружено.", new Dictionary<string, object>
        {
            ["items"] = items.Select(x => (object)Weather0217TravelPayload(x, admin: false)).ToArray(),
            ["playerSafe"] = true
        });
    }

    public ResponseEnvelope WorldPlayerTravelPreview0217(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Weather0217CampaignId(payload);
        var known = _mongo.TravelSessions0217.Find(x => x.CampaignId == campaignId && x.PartyOwnerUserIds.Contains(actor.Id))
            .SortByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
        if (known == null) return Ok("Нет известного маршрута для предварительного расчёта.", new Dictionary<string, object> { ["hasPreview"] = false });
        return Ok("Предварительный расчёт готов.", new Dictionary<string, object>
        {
            ["hasPreview"] = true,
            ["travel"] = Weather0217TravelPayload(known, admin: false),
            ["worldTimeCanBeAdvancedByPlayer"] = false
        });
    }

    public ResponseEnvelope WorldAdminWeatherGet0217(CommandContext context)
    {
        GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Weather0217CampaignId(payload);
        Weather0217ReconcileCampaign(campaignId, "admin_read", string.Empty, context.Request.RequestId ?? string.Empty);
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Ok("Погода для выбранной области не задана.", new Dictionary<string, object> { ["hasWeather"] = false });
        var worldSecond = Weather0217WorldSecond(campaignId);
        return Ok("Истинное состояние погоды загружено.", new Dictionary<string, object>
        {
            ["hasWeather"] = true,
            ["resolvedContext"] = Weather0217AdminResolvedContext(payload, weather, worldSecond),
            ["weather"] = Weather0217AdminWeatherPayload(weather, worldSecond),
            ["observationPreview"] = Weather0217ObservationPayload(weather, worldSecond),
            ["environment"] = Weather0217EnvironmentPayload(Weather0217BuildEnvironment(weather, worldSecond, payload), admin: true)
        });
    }

    public ResponseEnvelope WorldAdminEnvironmentGet0217(CommandContext context)
    {
        GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Ok("Окружение пока не рассчитано.", new Dictionary<string, object> { ["hasEnvironment"] = false });
        var worldSecond = Weather0217WorldSecond(weather.CampaignId);
        var outdoor = Weather0217BuildEnvironment(weather, worldSecond, payload);
        var shelteredPayload = new Dictionary<string, object>(payload, StringComparer.OrdinalIgnoreCase)
        {
            ["isIndoor"] = true,
            ["shelterName"] = "Каменный навес",
            ["shelterReduction"] = 0.75d
        };
        var sheltered = Weather0217BuildEnvironment(weather, worldSecond, shelteredPayload);
        return Ok("Снимок окружения рассчитан.", new Dictionary<string, object>
        {
            ["hasEnvironment"] = true,
            ["outdoor"] = Weather0217EnvironmentPayload(outdoor, admin: true),
            ["sheltered"] = Weather0217EnvironmentPayload(sheltered, admin: true),
            ["trueWeatherChangedByShelter"] = false
        });
    }

    public ResponseEnvelope WorldAdminForecastPreview0217(CommandContext context)
    {
        GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Error("Сначала выберите область с погодой.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Предварительный прогноз готов.", new Dictionary<string, object>
        {
            ["summary"] = "В ближайшие несколько часов ожидается усиление дождя и ветра.",
            ["reliability"] = 0.75m,
            ["approximateWindowMinutes"] = new[] { 60, 180 },
            ["hiddenExactTransitionIncluded"] = false,
            ["scopeLabel"] = Weather0217ScopeLabel(weather)
        });
    }

    public ResponseEnvelope WorldAdminTravelGet0217(CommandContext context)
    {
        GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Weather0217CampaignId(payload);
        var items = _mongo.TravelSessions0217.Find(x => x.CampaignId == campaignId && x.Status != TravelStatusIds.Archived)
            .SortByDescending(x => x.UpdatedAtUtc).Limit(50).ToList();
        return Ok(items.Count == 0 ? "Путешествий пока нет." : "Путешествия загружены.", new Dictionary<string, object>
        {
            ["items"] = items.Select(x => (object)Weather0217TravelPayload(x, admin: true)).ToArray(),
            ["resolutionSuggestions"] = Weather0217ExposureQueue(campaignId)
        });
    }

    public ResponseEnvelope WorldAdminTravelPreview0217(CommandContext context)
    {
        GetCurrentAccount(context);
        var travel = Weather0217RequireTravel(context.Request.Payload);
        Weather0217RecalculateTravel(travel);
        return Ok("План путешествия рассчитан.", new Dictionary<string, object>
        {
            ["travel"] = Weather0217TravelPayload(travel, admin: true),
            ["formula"] = "скорость режима × местность × погода × нагрузка",
            ["inventoryDuplicated"] = false
        });
    }

    public ResponseEnvelope WorldAdminWeatherOverride0217(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var inheritedWeather = Weather0217Resolve(payload);
        if (inheritedWeather == null) return Error("Погода для выбранной области не найдена.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var requestedScope = Weather0217RequestedScope(payload, inheritedWeather);
        var weather = _mongo.WeatherStates0217.Find(x => x.CampaignId == inheritedWeather.CampaignId
                                                         && x.Scope.ScopeType == requestedScope.ScopeType
                                                         && x.Scope.ScopeId == requestedScope.ScopeId
                                                         && !x.Deleted && !x.Archived).FirstOrDefault();
        var createdLocalOverride = weather == null;
        if (createdLocalOverride)
        {
            weather = Weather0217CloneForScope(inheritedWeather, requestedScope, actor.Id);
            _mongo.WeatherStates0217.InsertOne(weather);
        }
        var reason = RequireLength(PayloadReader.GetString(payload, "reason"), 3, 512, "reason");
        var expected = PayloadReader.GetInt(payload, "expectedRevision");
        if (expected.HasValue && expected.Value != weather.EntityRevision) return Error("Состояние погоды изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        Weather0217ApplyPattern(weather, FirstNonEmpty(PayloadReader.GetString(payload, "patternId"), weather.CurrentPatternId));
        weather.SourceType = FirstNonEmpty(PayloadReader.GetString(payload, "sourceType"), WeatherSourceTypeIds.GmOverride);
        weather.SourceId = actor.Id;
        weather.OverrideReason = reason;
        weather.IsLocked = PayloadReader.GetBool(payload, "isLocked");
        weather.LockUntilWorldSecond = PayloadReader.GetLong(payload, "lockUntilWorldSecond");
        Weather0217Touch(weather, actor.Id);
        _mongo.WeatherStates0217.ReplaceOne(x => x.Id == weather.Id, weather);
        WriteAudit("weather", actor.Id, "world.weather.override", weather.Id);
        Weather0217Sync("world.weather.override.changed", weather.CampaignId, "weather_state", weather.Id, "override", actor.Id, context.Request.RequestId);
        return Ok("Погода изменена и опубликована.", new Dictionary<string, object> { ["weather"] = Weather0217AdminWeatherPayload(weather, Weather0217WorldSecond(weather.CampaignId)), ["createdLocalOverride"] = createdLocalOverride });
    }

    public ResponseEnvelope WorldAdminWeatherLock0217(CommandContext context) => Weather0217SetLock(context, true);
    public ResponseEnvelope WorldAdminWeatherUnlock0217(CommandContext context) => Weather0217SetLock(context, false);

    public ResponseEnvelope WorldAdminForecastPublish0217(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Error("Погода для прогноза не найдена.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var characterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 1, 128, "characterId");
        var ownerUserId = RequireLength(PayloadReader.GetString(payload, "ownerUserId"), 1, 128, "ownerUserId");
        var summary = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "summary"), "В ближайшие несколько часов ожидается усиление дождя и ветра."), 3, 1024, "summary");
        var existing = _mongo.EntityKnowledgeStates.Find(x => x.CampaignId == weather.CampaignId && x.KnowledgeId == Weather0217ForecastKnowledgeId && x.EntityId == characterId && x.SourceId == weather.Scope.ScopeId && !x.IsArchived).FirstOrDefault();
        var knowledge = existing ?? new EntityKnowledgeState
        {
            CampaignId = weather.CampaignId,
            KnowledgeDefinitionId = Weather0217ForecastKnowledgeId,
            KnowledgeId = Weather0217ForecastKnowledgeId,
            EntityType = KnowledgeEntityTypeIds.Character,
            EntityId = characterId,
            OwnerUserId = ownerUserId,
            SourceId = weather.Scope.ScopeId,
            SourceLabel = Weather0217ScopeLabel(weather),
            GrantedAtUtc = DateTime.UtcNow
        };
        knowledge.PlayerSummary = summary;
        knowledge.Level = KnowledgeLevelIds.Partial;
        knowledge.TruthRelation = KnowledgeTruthRelationIds.Partial;
        knowledge.IsApplied = true;
        knowledge.IsPlayerVisible = true;
        knowledge.VisibilityMode = ProjectVisibilityModeIds.PlayerVisible;
        knowledge.GrantedByUserId = actor.Id;
        knowledge.UpdatedByUserId = actor.Id;
        knowledge.UpdatedAtUtc = DateTime.UtcNow;
        knowledge.ExtraData["reliability"] = PayloadReader.GetDouble(payload, "reliability") ?? 0.75d;
        knowledge.ExtraData["learnedAtWorldSecond"] = Weather0217WorldSecond(weather.CampaignId);
        knowledge.ExtraData["windowStartMinutes"] = 60;
        knowledge.ExtraData["windowEndMinutes"] = 180;
        knowledge.ExtraData["isOutdated"] = false;
        knowledge.ServerOnlyData["trueTransitionWorldSecond"] = weather.ScheduledTransitionAtWorldSecond;
        if (existing == null) _mongo.EntityKnowledgeStates.InsertOne(knowledge);
        else _mongo.EntityKnowledgeStates.ReplaceOne(x => x.Id == knowledge.Id, knowledge);
        WriteAudit("weather", actor.Id, "world.forecast.published", knowledge.Id);
        Weather0217Sync("world.forecast.changed", weather.CampaignId, "weather_forecast", knowledge.Id, "published", actor.Id, context.Request.RequestId);
        return Ok("Прогноз опубликован персонажу.", new Dictionary<string, object> { ["forecast"] = Weather0217ForecastPayload(knowledge, admin: true) });
    }

    public ResponseEnvelope WorldAdminTravelCreate0217(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Weather0217CampaignId(payload);
        var travel = new TravelSession
        {
            CampaignId = campaignId,
            WorldId = FirstNonEmpty(PayloadReader.GetString(payload, "worldId"), "world-0217"),
            PartyId = RequireLength(PayloadReader.GetString(payload, "partyId"), 1, 128, "partyId"),
            PartyName = RequireLength(PayloadReader.GetString(payload, "partyName"), 1, 180, "partyName"),
            PartyActorIds = Weather0217StringList(payload, "partyActorIds"),
            PartyOwnerUserIds = Weather0217StringList(payload, "partyOwnerUserIds"),
            OriginLocationId = RequireLength(PayloadReader.GetString(payload, "originLocationId"), 1, 128, "originLocationId"),
            OriginLocationName = RequireLength(PayloadReader.GetString(payload, "originLocationName"), 1, 180, "originLocationName"),
            DestinationLocationId = RequireLength(PayloadReader.GetString(payload, "destinationLocationId"), 1, 128, "destinationLocationId"),
            DestinationLocationName = RequireLength(PayloadReader.GetString(payload, "destinationLocationName"), 1, 180, "destinationLocationName"),
            ModeDefinitionId = RequireLength(PayloadReader.GetString(payload, "modeDefinitionId"), 1, 128, "modeDefinitionId"),
            ModeName = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "modeName"), "Пешком"), 1, 180, "modeName"),
            ModeBaseSpeedKmh = (decimal)(PayloadReader.GetDouble(payload, "baseSpeedKmh") ?? 4d),
            Status = TravelStatusIds.Prepared,
            UpdatedByUserId = actor.Id
        };
        travel.Segments = Weather0217ReadSegments(payload);
        if (travel.Segments.Count == 0) return Error("Добавьте хотя бы один участок маршрута.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        Weather0217RecalculateTravel(travel);
        _mongo.TravelSessions0217.InsertOne(travel);
        WriteAudit("travel", actor.Id, "world.travel.created", travel.Id);
        Weather0217Sync("world.travel.created", campaignId, "travel_session", travel.Id, "created", actor.Id, context.Request.RequestId);
        return Ok("План путешествия создан.", new Dictionary<string, object> { ["travel"] = Weather0217TravelPayload(travel, admin: true) });
    }

    public ResponseEnvelope WorldAdminTravelStart0217(CommandContext context) => Weather0217SetTravelStatus(context, TravelStatusIds.Active, "world.travel.started");
    public ResponseEnvelope WorldAdminTravelPause0217(CommandContext context) => Weather0217SetTravelStatus(context, TravelStatusIds.Paused, "world.travel.paused");
    public ResponseEnvelope WorldAdminTravelResume0217(CommandContext context) => Weather0217SetTravelStatus(context, TravelStatusIds.Active, "world.travel.resumed");
    public ResponseEnvelope WorldAdminTravelCancel0217(CommandContext context) => Weather0217SetTravelStatus(context, TravelStatusIds.Cancelled, "world.travel.cancelled");

    public ResponseEnvelope WorldAdminTravelSegmentComplete0217(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var travel = Weather0217RequireTravel(payload);
        var operationId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "operationId"), context.Request.RequestId), 1, 128, "operationId");
        var expected = PayloadReader.GetInt(payload, "expectedRevision");
        if (expected.HasValue && expected.Value != travel.Revision) return Error("План путешествия изменился. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var replay = travel.Segments.FirstOrDefault(x => string.Equals(x.CompletionOperationId, operationId, StringComparison.Ordinal));
        if (replay != null) return Ok("Этот участок уже завершён.", new Dictionary<string, object> { ["travel"] = Weather0217TravelPayload(travel, admin: true), ["idempotentReplay"] = true, ["worldTimeAdvanced"] = false });
        if (!string.Equals(travel.Status, TravelStatusIds.Active, StringComparison.OrdinalIgnoreCase)) return Error("Путешествие должно быть активно.", ResponseStatus.Conflict, ErrorCode.Conflict);
        if (travel.CurrentSegmentIndex < 0 || travel.CurrentSegmentIndex >= travel.Segments.Count) return Error("Текущий участок маршрута не найден.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var segment = travel.Segments[travel.CurrentSegmentIndex];
        var advanced = Weather0217AdvanceWorldTime(travel.CampaignId, segment.AuthoritativeDurationMinutes, actor.Id, $"Завершён участок {segment.FromLocationName} — {segment.ToLocationName}");
        segment.IsCompleted = true;
        segment.CompletionOperationId = operationId;
        segment.CompletedAtWorldSecond = advanced.CurrentDateTime.AbsoluteSecondIndex;
        travel.CurrentSegmentIndex++;
        travel.Progress = travel.Segments.Count == 0 ? 0 : travel.CurrentSegmentIndex / (decimal)travel.Segments.Count;
        travel.Status = travel.CurrentSegmentIndex >= travel.Segments.Count ? TravelStatusIds.Arrived : TravelStatusIds.Active;
        travel.Revision++;
        travel.UpdatedAtUtc = DateTime.UtcNow;
        travel.UpdatedByUserId = actor.Id;
        Weather0217ReconcileCampaign(travel.CampaignId, "travel_segment", actor.Id, context.Request.RequestId ?? string.Empty);
        Weather0217RecalculateTravel(travel);
        _mongo.TravelSessions0217.ReplaceOne(x => x.Id == travel.Id, travel);
        if (segment.HazardTags.Contains("cold_wet", StringComparer.OrdinalIgnoreCase) || segment.WeatherMultiplier < 1m)
            Weather0217EnsureExposureSuggestion(travel, segment, actor.Id);
        if (travel.Status == TravelStatusIds.Arrived)
        {
            var linkedSessions = _repositories.CurrentSessions.Find(
                Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, travel.CampaignId)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.ActiveTravelSessionId, travel.Id)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)).ToList();
            foreach (var linkedSession in linkedSessions)
            {
                linkedSession.ActiveTravelSessionId = string.Empty;
                TouchSession(linkedSession, actor.Id);
                SaveSession(linkedSession);
            }
        }
        WriteAudit("travel", actor.Id, "world.travel.segment.completed", travel.Id);
        Weather0217Sync("world.travel.segment.completed", travel.CampaignId, "travel_session", travel.Id, "segment_completed", actor.Id, context.Request.RequestId);
        if (travel.Status == TravelStatusIds.Arrived) Weather0217Sync("world.travel.arrived", travel.CampaignId, "travel_session", travel.Id, "arrived", actor.Id, context.Request.RequestId);
        return Ok(travel.Status == TravelStatusIds.Arrived ? "Путешествие завершено." : "Участок завершён.", new Dictionary<string, object>
        {
            ["travel"] = Weather0217TravelPayload(travel, admin: true),
            ["worldTimeAdvanced"] = true,
            ["advancedMinutes"] = segment.AuthoritativeDurationMinutes,
            ["worldSecond"] = advanced.CurrentDateTime.AbsoluteSecondIndex,
            ["idempotentReplay"] = false
        });
    }

    public ResponseEnvelope WorldAdminEnvironmentFatePreview0217(CommandContext context)
    {
        GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var route = FirstNonEmpty(PayloadReader.GetString(payload, "route"), "travel_navigation");
        var weather = Weather0217Resolve(payload);
        var isInitiative = string.Equals(route, "fantasy_nri_default_initiative", StringComparison.OrdinalIgnoreCase);
        var storm = weather != null && string.Equals(weather.Severity, "severe", StringComparison.OrdinalIgnoreCase);
        return Ok("Маршрут влияния окружения проверен.", new Dictionary<string, object>
        {
            ["route"] = route,
            ["fateApplied"] = !isInitiative,
            ["weatherFateLayerApplied"] = !isInitiative && weather != null,
            ["deterministicSkillPenaltyApplied"] = false,
            ["doubleApplied"] = false,
            ["playerExplanation"] = storm ? "Сильный ветер и дождь осложняют ориентирование." : "Дождь и мокрая дорога требуют осторожности.",
            ["initiativeBypass"] = isInitiative,
            ["internalCoefficientIncluded"] = false
        });
    }

    public ResponseEnvelope WorldAdminExposureApprove0217(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var requestId = RequireLength(PayloadReader.GetString(payload, "suggestionId"), 1, 128, "suggestionId");
        var suggestion = _mongo.ActionRequests.Find(x => x.Id == requestId && x.ActionCode == Weather0217ExposureAction).FirstOrDefault();
        if (suggestion == null) return Error("Предложение воздействия не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (suggestion.Status == RequestStatus.Approved) return Ok("Воздействие уже подтверждено.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var details = BsonDocument.Parse(suggestion.PayloadJson);
        var effectContext = new CommandContext
        {
            ConnectionId = context.ConnectionId,
            Session = context.Session,
            Request = new RequestEnvelope
            {
                Command = CommandNames.ActorAdminEffectApply,
                RequestId = FirstNonEmpty(PayloadReader.GetString(payload, "operationId"), context.Request.RequestId),
                AuthToken = context.Request.AuthToken,
                Payload = new Dictionary<string, object>
                {
                    ["subjectType"] = "character",
                    ["subjectId"] = details.GetValue("characterId", string.Empty).AsString,
                    ["conditionDefinitionId"] = "condition_cold_wet_0217",
                    ["displayName"] = "Промок и замёрз",
                    ["description"] = "Холод и сырость сказываются на состоянии персонажа.",
                    ["gmNote"] = "Подтверждено из очереди воздействия среды.",
                    ["reason"] = "Воздействие холода и сырости во время путешествия.",
                    ["operationId"] = FirstNonEmpty(PayloadReader.GetString(payload, "operationId"), context.Request.RequestId),
                    ["isPlayerVisible"] = true
                }
            }
        };
        var applied = ActorAdminEffectApply(effectContext);
        if (applied.Status != ResponseStatus.Ok) return applied;
        suggestion.Status = RequestStatus.Approved;
        suggestion.Decision.DecidedByUserId = actor.Id;
        suggestion.Decision.DecidedAtUtc = DateTime.UtcNow;
        suggestion.Decision.AdminComment = "Воздействие подтверждено мастером.";
        suggestion.UpdatedUtc = DateTime.UtcNow;
        _mongo.ActionRequests.ReplaceOne(x => x.Id == suggestion.Id, suggestion);
        return Ok("Воздействие применено через состояние персонажа.", new Dictionary<string, object> { ["suggestionId"] = suggestion.Id, ["liveActorIntegration"] = true });
    }

    public ResponseEnvelope WorldAdminWeatherFixtureEnsure0217(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), "northern-path-0217");
        var worldId = FirstNonEmpty(PayloadReader.GetString(payload, "worldId"), "northern-world-0217");
        var ownerUserId = FirstNonEmpty(PayloadReader.GetString(payload, "ownerUserId"), Weather0217FindUserId("dev_player"));
        var characterId = FirstNonEmpty(PayloadReader.GetString(payload, "characterId"), Weather0217FindActiveCharacterId(ownerUserId));
        var characterBId = FirstNonEmpty(PayloadReader.GetString(payload, "characterBId"), "character-b-0217");
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            var staleEffects = _mongo.RuntimeEffectInstances.Find(x =>
                x.TargetSubject.SubjectId == characterId &&
                x.ConditionDefinitionId == "condition_cold_wet_0217" &&
                x.IsActive).ToList();
            foreach (var effect in staleEffects)
            {
                effect.IsActive = false;
                effect.IsExpired = true;
                effect.Revision++;
                effect.UpdatedUtc = DateTime.UtcNow;
                _mongo.RuntimeEffectInstances.ReplaceOne(x => x.Id == effect.Id, effect);
            }
        }
        var staleSuggestions = _mongo.ActionRequests.Find(x =>
            x.ActionCode == Weather0217ExposureAction &&
            x.Status == RequestStatus.Pending).ToList();
        foreach (var suggestion in staleSuggestions)
        {
            suggestion.Status = RequestStatus.Cancelled;
            suggestion.UpdatedUtc = DateTime.UtcNow;
            _mongo.ActionRequests.ReplaceOne(x => x.Id == suggestion.Id, suggestion);
        }
        Weather0217EnsureFixtureDefinitions(actor.Id);
        var staleLocalWeather = _mongo.WeatherStates0217.Find(x => x.CampaignId == campaignId && x.Scope.ScopeId != "northern-valley-0217" && !x.Archived).ToList();
        foreach (var stale in staleLocalWeather)
        {
            stale.Archived = true;
            stale.UpdatedAtUtc = DateTime.UtcNow;
            stale.UpdatedUtc = stale.UpdatedAtUtc;
            stale.UpdatedBy = actor.Id;
            _mongo.WeatherStates0217.ReplaceOne(x => x.Id == stale.Id, stale);
        }
        var calendar = FindActiveWorldCalendar(campaignId) ?? Weather0217CreateCalendar(campaignId, actor.Id);
        var worldTime = EnsureWorldTime(campaignId, calendar, actor.Id);
        worldTime.CurrentDateTime = WorldCalendarMath.FromAbsoluteSeconds(calendar.Id, 0, calendar.HoursPerDay, calendar.MinutesPerHour, calendar.SecondsPerMinute);
        worldTime.Revision++;
        worldTime.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.CampaignWorldTimes.Replace(worldTime);

        var weather = _mongo.WeatherStates0217.Find(x => x.CampaignId == campaignId && x.Scope.ScopeId == "northern-valley-0217").FirstOrDefault() ?? new WeatherStateDocument();
        weather.WorldId = worldId;
        weather.CampaignId = campaignId;
        weather.Scope = new WeatherScopeReference { ScopeType = WeatherScopeTypeIds.Region, ScopeId = "northern-valley-0217", WorldId = worldId, CampaignId = campaignId };
        weather.ClimateProfileId = "climate_northern_temperate_0217";
        weather.CurrentPatternId = "weather_cold_rain_0217";
        weather.CurrentPatternName = "Холодный дождь";
        weather.TrueTemperatureC = 8m;
        weather.TruePrecipitation = "Умеренный дождь";
        weather.TrueWindSpeedMetersPerSecond = 5m;
        weather.TrueWindDirectionDegreesFromNorth = 315m;
        weather.TrueWindGustMetersPerSecond = 7m;
        weather.TrueWindKmh = 0m;
        weather.WindUnitSchemaVersion = 2;
        weather.TrueVisibilityM = 900;
        weather.TrueCloudCover = "Плотная облачность";
        weather.TrueSurfaceCondition = "Мокрая";
        weather.Severity = "minor";
        weather.SourceType = WeatherSourceTypeIds.Natural;
        weather.StartedAtWorldSecond = 0;
        weather.ScheduledTransitionAtWorldSecond = 3600;
        weather.GenerationSeed = 2170217;
        weather.RandomAlgorithmId = WeatherDeterministicRandom.AlgorithmId;
        weather.RandomAlgorithmVersion = WeatherDeterministicRandom.AlgorithmVersion;
        weather.TransitionIndex = 0;
        weather.IsLocked = false;
        weather.OverrideReason = string.Empty;
        weather.EntityRevision++;
        weather.UpdatedBy = actor.Id;
        weather.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.WeatherStates0217.ReplaceOne(x => x.Id == weather.Id, weather, new ReplaceOptions { IsUpsert = true });
        Weather0217BEnsureFixtures(ownerUserId, characterId);
        // A fixture reset starts the acceptance observer with no prior exact knowledge.
        _mongo.EnvironmentObservations0217B.DeleteMany(x => x.CampaignId == campaignId && x.OwnerUserId == ownerUserId);

        var travel = _mongo.TravelSessions0217.Find(x => x.CampaignId == campaignId && x.PartyId == "party-northern-0217" && x.Status != TravelStatusIds.Archived).FirstOrDefault() ?? new TravelSession();
        travel.WorldId = worldId;
        travel.CampaignId = campaignId;
        travel.PartyId = "party-northern-0217";
        travel.PartyName = "Путники Северного пути";
        travel.PartyActorIds = new List<string> { characterId, "companion-argo-0217" }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        travel.PartyOwnerUserIds = new List<string> { ownerUserId }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        travel.PartyMemberNames = new List<string> { "Адель Вард", "Арго" };
        travel.OriginLocationId = "north-gate-0217";
        travel.OriginLocationName = "Северные ворота";
        travel.DestinationLocationId = "stone-pass-0217";
        travel.DestinationLocationName = "Каменный перевал";
        travel.ModeDefinitionId = "travel_walking_0217";
        travel.ModeName = "Пешком";
        travel.ModeBaseSpeedKmh = 4m;
        travel.Status = TravelStatusIds.Prepared;
        travel.CurrentSegmentIndex = 0;
        travel.Progress = 0;
        travel.Segments = new List<TravelRouteSegment>
        {
            new() { Order = 1, FromLocationId = "north-gate-0217", FromLocationName = "Северные ворота", ToLocationId = "forest-station-0217", ToLocationName = "Лесная станция", DistanceKm = 4m, TerrainProfileId = "terrain_northern_road_0217", TerrainName = "Северная дорога", TerrainMultiplier = 1m, WeatherPatternId = "weather_cold_rain_0217", WeatherPatternName = "Холодный дождь", WeatherMultiplier = 1m, ModeMultiplier = 1m, LoadMultiplier = 1m, AuthoritativeDurationMinutes = 60, PlayerEtaMinMinutes = 55, PlayerEtaMaxMinutes = 75, WeatherScopeId = weather.Scope.ScopeId },
            new() { Order = 2, FromLocationId = "forest-station-0217", FromLocationName = "Лесная станция", ToLocationId = "stone-pass-0217", ToLocationName = "Каменный перевал", DistanceKm = 4m, TerrainProfileId = "terrain_stone_pass_0217", TerrainName = "Каменистый подъём", TerrainMultiplier = 0.8m, WeatherPatternId = "weather_storm_front_0217", WeatherPatternName = "Штормовой фронт", WeatherMultiplier = 0.625m, ModeMultiplier = 1m, LoadMultiplier = 1m, AuthoritativeDurationMinutes = 120, PlayerEtaMinMinutes = 105, PlayerEtaMaxMinutes = 150, WeatherScopeId = weather.Scope.ScopeId, HazardTags = new List<string> { "cold_wet" } }
        };
        travel.RequiredSupplyCategories = new List<string> { "вода", "пища", "укрытие" };
        travel.AvailableSupplySummary = new List<string> { "Запасы читаются из инвентаря группы", "Каменный навес доступен на маршруте" };
        travel.Revision++;
        travel.UpdatedAtUtc = DateTime.UtcNow;
        travel.UpdatedByUserId = actor.Id;
        Weather0217RecalculateTravel(travel);
        _mongo.TravelSessions0217.ReplaceOne(x => x.Id == travel.Id, travel, new ReplaceOptions { IsUpsert = true });

        if (!string.IsNullOrWhiteSpace(characterId) && !string.IsNullOrWhiteSpace(ownerUserId))
        {
            var forecastContext = new CommandContext
            {
                ConnectionId = context.ConnectionId,
                Session = context.Session,
                Request = new RequestEnvelope
                {
                    Command = CommandNames.WorldAdminForecastPublish,
                    RequestId = $"fixture-forecast-{Guid.NewGuid():N}",
                    AuthToken = context.Request.AuthToken,
                    Payload = new Dictionary<string, object>
                    {
                        ["campaignId"] = campaignId, ["regionId"] = weather.Scope.ScopeId,
                        ["characterId"] = characterId, ["ownerUserId"] = ownerUserId,
                        ["summary"] = "В ближайшие несколько часов ожидается усиление дождя и ветра.", ["reliability"] = 0.75d
                    }
                }
            };
            WorldAdminForecastPublish0217(forecastContext);
        }

        WriteAudit("weather", actor.Id, "world.weather.fixture.ensure", campaignId);
        return Ok("Тестовая среда «Северный путь» подготовлена.", new Dictionary<string, object>
        {
            ["campaignId"] = campaignId,
            ["campaignName"] = "Северный путь",
            ["regionName"] = "Северная долина",
            ["weather"] = Weather0217AdminWeatherPayload(weather, 0),
            ["travel"] = Weather0217TravelPayload(travel, admin: true),
            ["characterAId"] = characterId,
            ["characterBId"] = characterBId,
            ["sourceOfTruth"] = new[] { "campaign_world_times", "weather_states", "travel_sessions", "entity_knowledge_states" }
        });
    }

    private ResponseEnvelope Weather0217SetLock(CommandContext context, bool locked)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var weather = Weather0217Resolve(payload);
        if (weather == null) return Error("Погода для выбранной области не найдена.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var reason = RequireLength(PayloadReader.GetString(payload, "reason"), 3, 512, "reason");
        weather.IsLocked = locked;
        weather.LockUntilWorldSecond = locked ? PayloadReader.GetLong(payload, "lockUntilWorldSecond") : null;
        weather.OverrideReason = reason;
        Weather0217Touch(weather, actor.Id);
        _mongo.WeatherStates0217.ReplaceOne(x => x.Id == weather.Id, weather);
        WriteAudit("weather", actor.Id, locked ? "world.weather.lock" : "world.weather.unlock", weather.Id);
        Weather0217Sync("world.weather.override.changed", weather.CampaignId, "weather_state", weather.Id, locked ? "locked" : "unlocked", actor.Id, context.Request.RequestId);
        return Ok(locked ? "Естественные переходы погоды приостановлены." : "Естественные переходы погоды возобновлены.", new Dictionary<string, object> { ["weather"] = Weather0217AdminWeatherPayload(weather, Weather0217WorldSecond(weather.CampaignId)) });
    }

    private ResponseEnvelope Weather0217SetTravelStatus(CommandContext context, string status, string eventType)
    {
        var actor = GetCurrentAccount(context);
        var travel = Weather0217RequireTravel(context.Request.Payload);
        var sessionId = PayloadReader.GetString(context.Request.Payload, "sessionId") ?? string.Empty;
        CurrentSessionState? currentSession = null;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            currentSession = _repositories.CurrentSessions.Find(
                Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, sessionId)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();
            if (currentSession == null || !string.Equals(currentSession.CampaignId, travel.CampaignId, StringComparison.Ordinal))
                return Error("current session not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        }
        var expected = PayloadReader.GetInt(context.Request.Payload, "expectedRevision");
        if (expected.HasValue && expected.Value != travel.Revision) return Error("План путешествия изменился. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        if (status == TravelStatusIds.Active && travel.DepartureWorldSecond == 0) travel.DepartureWorldSecond = Weather0217WorldSecond(travel.CampaignId);
        travel.Status = status;
        travel.Revision++;
        travel.UpdatedAtUtc = DateTime.UtcNow;
        travel.UpdatedByUserId = actor.Id;
        _mongo.TravelSessions0217.ReplaceOne(x => x.Id == travel.Id, travel);
        if (currentSession != null)
        {
            if (status == TravelStatusIds.Active)
            {
                currentSession.ActiveTravelSessionId = travel.Id;
                currentSession.Mode = CurrentSessionModeIds.Travel;
            }
            else if (status == TravelStatusIds.Cancelled && string.Equals(currentSession.ActiveTravelSessionId, travel.Id, StringComparison.Ordinal))
            {
                currentSession.ActiveTravelSessionId = string.Empty;
            }
            TouchSession(currentSession, actor.Id);
            SaveSession(currentSession);
        }
        WriteAudit("travel", actor.Id, eventType, travel.Id);
        Weather0217Sync("world.travel.changed", travel.CampaignId, "travel_session", travel.Id, status, actor.Id, context.Request.RequestId);
        return Ok("Состояние путешествия обновлено.", new Dictionary<string, object> { ["travel"] = Weather0217TravelPayload(travel, admin: true) });
    }

    private CampaignWorldTimeState Weather0217AdvanceWorldTime(string campaignId, int minutes, string actorId, string reason)
    {
        var calendar = FindActiveWorldCalendar(campaignId) ?? Weather0217CreateCalendar(campaignId, actorId);
        var state = EnsureWorldTime(campaignId, calendar, actorId);
        var seconds = checked((long)Math.Max(0, minutes) * calendar.SecondsPerMinute);
        state.CurrentDateTime = WorldCalendarMath.FromAbsoluteSeconds(calendar.Id, state.CurrentDateTime.AbsoluteSecondIndex + seconds, calendar.HoursPerDay, calendar.MinutesPerHour, calendar.SecondsPerMinute);
        state.LastAdvancedAtUtc = DateTime.UtcNow;
        state.LastAdvancedByUserId = actorId;
        state.LastAdvanceReason = reason;
        state.Revision++;
        state.UpdatedAtUtc = DateTime.UtcNow;
        state.UpdatedUtc = DateTime.UtcNow;
        _repositories.CampaignWorldTimes.Replace(state);
        SyncCurrentSessionWorldDate(campaignId, WorldCalendarMath.Format(state.CurrentDateTime, calendar));
        WriteAudit("world_calendar", actorId, "calendar.time.advanced.travel", state.Id);
        return state;
    }

    private void Weather0217ReconcileCampaign(string campaignId, string trigger, string actorId, string requestId)
    {
        var worldSecond = Weather0217WorldSecond(campaignId);
        var items = _mongo.WeatherStates0217.Find(x => x.CampaignId == campaignId && !x.Deleted && !x.Archived).ToList();
        foreach (var weather in items)
        {
            var transitions = 0;
            while (!weather.IsLocked && weather.ScheduledTransitionAtWorldSecond > 0 && worldSecond >= weather.ScheduledTransitionAtWorldSecond && transitions < 256)
            {
                var startedAt = weather.ScheduledTransitionAtWorldSecond;
                var next = Weather0217NextPattern(weather);
                Weather0217ApplyPattern(weather, next);
                weather.StartedAtWorldSecond = startedAt;
                weather.TransitionIndex++;
                var min = weather.CurrentPatternId == "weather_storm_front_0217" ? 180 : 60;
                var duration = WeatherDeterministicRandom.Range(weather.GenerationSeed, weather.TransitionIndex, min, min + 121, 17);
                weather.ScheduledTransitionAtWorldSecond = startedAt + duration * 60L;
                Weather0217Touch(weather, actorId);
                transitions++;
            }
            if (weather.IsLocked && weather.LockUntilWorldSecond.HasValue && worldSecond >= weather.LockUntilWorldSecond.Value)
            {
                weather.IsLocked = false;
                weather.LockUntilWorldSecond = null;
                Weather0217Touch(weather, actorId);
            }
            if (transitions > 0)
            {
                _mongo.WeatherStates0217.ReplaceOne(x => x.Id == weather.Id, weather);
                Weather0217MarkForecastsStale(weather.CampaignId, worldSecond);
                Weather0217Sync("world.weather.changed", weather.CampaignId, "weather_state", weather.Id, trigger, actorId, requestId);
                Weather0217Sync("world.environment.invalidated", weather.CampaignId, "environment_snapshot", weather.Scope.ScopeId, "invalidated", actorId, requestId);
            }
        }
    }

    private string Weather0217NextPattern(WeatherStateDocument weather)
    {
        if (weather.CurrentPatternId == "weather_cold_rain_0217") return "weather_storm_front_0217";
        var seasonalIds = Weather0217SeasonalPatternIds(weather);
        var candidates = _mongo.ContentDefinitionRecords.Find(x => x.Category == WeatherDefinitionFamilyIds.WeatherPattern && !x.IsArchived).ToList();
        if (seasonalIds.Count > 0)
            candidates = candidates.Where(x => seasonalIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        if (candidates.Count == 0) return weather.CurrentPatternId;
        return candidates[WeatherDeterministicRandom.Range(weather.GenerationSeed, weather.TransitionIndex, 0, candidates.Count)].Id;
    }

    private List<string> Weather0217SeasonalPatternIds(WeatherStateDocument weather)
    {
        var climate = _mongo.ContentDefinitionRecords.Find(x =>
            x.Category == WeatherDefinitionFamilyIds.Climate &&
            x.Id == weather.ClimateProfileId &&
            !x.IsArchived).FirstOrDefault();
        if (climate == null) return new List<string>();

        var worldTime = _mongo.CampaignWorldTimes.Find(x => x.CampaignId == weather.CampaignId && !x.Deleted && !x.Archived)
            .SortByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
        var seasonKey = Weather0217SeasonKey(WorldCalendarMath.SeasonName(worldTime?.CurrentDateTime));
        var seasonal = Weather0217StringList(climate.CustomFields, seasonKey + "PatternIds");
        return seasonal.Count > 0 ? seasonal : Weather0217StringList(climate.CustomFields, "allowedPatternIds");
    }

    private static string Weather0217SeasonKey(string seasonName)
    {
        if (seasonName.IndexOf("зим", StringComparison.OrdinalIgnoreCase) >= 0) return "winter";
        if (seasonName.IndexOf("вес", StringComparison.OrdinalIgnoreCase) >= 0) return "spring";
        if (seasonName.IndexOf("лет", StringComparison.OrdinalIgnoreCase) >= 0) return "summer";
        if (seasonName.IndexOf("осен", StringComparison.OrdinalIgnoreCase) >= 0) return "autumn";
        return "allSeason";
    }

    private void Weather0217ApplyPattern(WeatherStateDocument weather, string patternId)
    {
        var definition = _mongo.ContentDefinitionRecords.Find(x => x.Category == WeatherDefinitionFamilyIds.WeatherPattern && x.Id == patternId && !x.IsArchived).FirstOrDefault();
        weather.CurrentPatternId = patternId;
        if (definition == null)
        {
            if (patternId == "weather_storm_front_0217")
            {
                weather.CurrentPatternName = "Штормовой фронт"; weather.TrueTemperatureC = 6m; weather.TruePrecipitation = "Сильный дождь";
                weather.TrueWindSpeedMetersPerSecond = 12.5m; weather.TrueWindDirectionDegreesFromNorth = 300m; weather.TrueWindGustMetersPerSecond = 16m;
                weather.TrueWindKmh = 0m; weather.WindUnitSchemaVersion = 2; weather.TrueVisibilityM = 350; weather.TrueCloudCover = "Грозовые облака";
                weather.TrueSurfaceCondition = "Грязь"; weather.Severity = "severe";
            }
            return;
        }
        weather.CurrentPatternName = FirstNonEmpty(definition.DisplayName, definition.Name);
        weather.TrueTemperatureC = Weather0217Decimal(definition.CustomFields, "temperatureC", weather.TrueTemperatureC);
        weather.TruePrecipitation = Weather0217String(definition.CustomFields, "precipitation", weather.TruePrecipitation);
        weather.TrueWindSpeedMetersPerSecond = Weather0217Decimal(definition.CustomFields, "windSpeedMps", weather.TrueWindSpeedMetersPerSecond);
        if (weather.TrueWindSpeedMetersPerSecond <= 0m)
            weather.TrueWindSpeedMetersPerSecond = EnvironmentMeasurementMath.MetersPerSecondFromKilometersPerHour(Weather0217Decimal(definition.CustomFields, "windKmh", 0m));
        weather.TrueWindDirectionDegreesFromNorth = WindVectorSnapshot.NormalizeDegrees(Weather0217Decimal(definition.CustomFields, "windDirectionFromDegrees", weather.TrueWindDirectionDegreesFromNorth));
        weather.TrueWindKmh = 0m;
        weather.WindUnitSchemaVersion = 2;
        weather.TrueVisibilityM = (int)Weather0217Decimal(definition.CustomFields, "visibilityM", weather.TrueVisibilityM);
        weather.TrueCloudCover = Weather0217String(definition.CustomFields, "cloudCover", weather.TrueCloudCover);
        weather.TrueSurfaceCondition = Weather0217String(definition.CustomFields, "surfaceCondition", weather.TrueSurfaceCondition);
        weather.Severity = Weather0217String(definition.CustomFields, "severity", weather.Severity);
    }

    private WeatherStateDocument? Weather0217Resolve(Dictionary<string, object> payload)
    {
        var campaignId = Weather0217CampaignId(payload);
        var all = _mongo.WeatherStates0217.Find(x => x.CampaignId == campaignId && !x.Deleted && !x.Archived).ToList();
        var scopes = new[]
        {
            (WeatherScopeTypeIds.Scene, PayloadReader.GetString(payload, "sceneId")),
            (WeatherScopeTypeIds.Location, PayloadReader.GetString(payload, "locationId")),
            (WeatherScopeTypeIds.Region, PayloadReader.GetString(payload, "regionId")),
            (WeatherScopeTypeIds.World, PayloadReader.GetString(payload, "worldId"))
        };
        foreach (var (type, id) in scopes)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            var found = all.OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefault(x => x.Scope.ScopeType == type && x.Scope.ScopeId == id);
            if (found != null) return Weather0217BNormalizeWind(found);
        }
        var fallback = all.OrderByDescending(x => x.Scope.ScopeType == WeatherScopeTypeIds.Region).ThenByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
        return fallback == null ? null : Weather0217BNormalizeWind(fallback);
    }

    private WeatherScopeReference Weather0217RequestedScope(Dictionary<string, object> payload, WeatherStateDocument inherited)
    {
        var candidates = new[]
        {
            (WeatherScopeTypeIds.Scene, PayloadReader.GetString(payload, "sceneId")),
            (WeatherScopeTypeIds.Location, PayloadReader.GetString(payload, "locationId")),
            (WeatherScopeTypeIds.Region, PayloadReader.GetString(payload, "regionId")),
            (WeatherScopeTypeIds.World, PayloadReader.GetString(payload, "worldId"))
        };
        var requested = candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Item2));
        if (string.IsNullOrWhiteSpace(requested.Item2)) return inherited.Scope;
        return new WeatherScopeReference
        {
            ScopeType = requested.Item1,
            ScopeId = requested.Item2!,
            WorldId = inherited.WorldId,
            CampaignId = inherited.CampaignId
        };
    }

    private static WeatherStateDocument Weather0217CloneForScope(WeatherStateDocument source, WeatherScopeReference scope, string actorId)
        => new()
        {
            WorldId = source.WorldId,
            CampaignId = source.CampaignId,
            Scope = scope,
            ClimateProfileId = source.ClimateProfileId,
            CurrentPatternId = source.CurrentPatternId,
            CurrentPatternName = source.CurrentPatternName,
            TrueTemperatureC = source.TrueTemperatureC,
            TruePrecipitation = source.TruePrecipitation,
            TrueWindSpeedMetersPerSecond = source.TrueWindSpeedMetersPerSecond,
            TrueWindDirectionDegreesFromNorth = source.TrueWindDirectionDegreesFromNorth,
            TrueWindGustMetersPerSecond = source.TrueWindGustMetersPerSecond,
            WindUnitSchemaVersion = 2,
            TrueVisibilityM = source.TrueVisibilityM,
            TrueCloudCover = source.TrueCloudCover,
            TrueSurfaceCondition = source.TrueSurfaceCondition,
            Severity = source.Severity,
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            StartedAtWorldSecond = source.StartedAtWorldSecond,
            ScheduledTransitionAtWorldSecond = source.ScheduledTransitionAtWorldSecond,
            GenerationSeed = source.GenerationSeed,
            RandomAlgorithmId = source.RandomAlgorithmId,
            RandomAlgorithmVersion = source.RandomAlgorithmVersion,
            TransitionIndex = source.TransitionIndex,
            EntityRevision = 1,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = actorId
        };

    private EnvironmentSnapshot Weather0217BuildEnvironment(WeatherStateDocument weather, long worldSecond, Dictionary<string, object> payload)
    {
        var indoor = PayloadReader.GetBool(payload, "isIndoor");
        var reduction = (decimal)(PayloadReader.GetDouble(payload, "shelterReduction") ?? (indoor ? 0.75d : 0d));
        reduction = Math.Max(0m, Math.Min(1m, reduction));
        var severe = string.Equals(weather.Severity, "severe", StringComparison.OrdinalIgnoreCase);
        var movement = severe ? 0.625m : 1m;
        var snapshot = new EnvironmentSnapshot
        {
            CampaignId = weather.CampaignId,
            ScopeId = weather.Scope.ScopeId,
            WorldSecond = worldSecond,
            PatternName = weather.CurrentPatternName,
            EffectiveTemperatureC = indoor ? weather.TrueTemperatureC + 2m : weather.TrueTemperatureC,
            Wind = WindVectorSnapshot.FromMeteorological(weather.TrueWindSpeedMetersPerSecond, weather.TrueWindDirectionDegreesFromNorth, weather.TrueWindGustMetersPerSecond),
            WeatherRevision = weather.EntityRevision,
            VisibilityM = indoor ? Math.Max(weather.TrueVisibilityM, 900) : weather.TrueVisibilityM,
            MovementMultiplier = indoor ? 1m : movement,
            SurfaceCondition = indoor ? "Сухая под навесом" : weather.TrueSurfaceCondition,
            IsIndoor = indoor,
            ShelterName = PayloadReader.GetString(payload, "shelterName") ?? (indoor ? "Укрытие" : string.Empty),
            ExposureMultiplier = 1m - reduction,
            ExposureSources = indoor ? new List<string>() : new List<string> { "Холод", "Сырость" },
            PublicWarnings = severe && !indoor ? new List<string> { "Сильный ветер и дождь ухудшают видимость и замедляют путь." } : new List<string>(),
            GmDiagnostics = new List<string> { $"weather:{weather.CurrentPatternId}", $"source:{weather.SourceType}", $"revision:{weather.EntityRevision}" }
        };
        return snapshot;
    }

    private Dictionary<string, object> Weather0217ObservationPayload(WeatherStateDocument weather, long worldSecond)
    {
        var temperatureBand = weather.TrueTemperatureC <= 0 ? "Морозно" : weather.TrueTemperatureC < 10 ? "Холодно" : weather.TrueTemperatureC < 20 ? "Прохладно" : "Тепло";
        var windBand = weather.TrueWindSpeedMetersPerSecond >= 11m ? "Штормовой ветер" : weather.TrueWindSpeedMetersPerSecond >= 8m ? "Сильный ветер" : weather.TrueWindSpeedMetersPerSecond >= 3m ? "Умеренный ветер" : "Слабый ветер";
        var visibilityBand = weather.TrueVisibilityM < 500 ? "Очень плохая" : weather.TrueVisibilityM < 1000 ? "Ограниченная" : "Хорошая";
        return new Dictionary<string, object>
        {
            ["scopeLabel"] = Weather0217ScopeLabel(weather), ["patternName"] = weather.CurrentPatternName,
            ["temperatureBand"] = temperatureBand,
            ["precipitation"] = weather.TruePrecipitation, ["windBand"] = windBand,
            ["visibilityBand"] = visibilityBand, ["surfaceCondition"] = weather.TrueSurfaceCondition,
            ["severity"] = weather.Severity, ["observedAtWorldSecond"] = worldSecond, ["confidence"] = 1m,
            ["summary"] = $"{weather.CurrentPatternName}. {temperatureBand}, {windBand.ToLowerInvariant()}. Поверхность: {weather.TrueSurfaceCondition.ToLowerInvariant()}.",
            ["hasHiddenSchedule"] = false
        };
    }

    private Dictionary<string, object> Weather0217AdminWeatherPayload(WeatherStateDocument weather, long worldSecond)
    {
        var worldTime = _mongo.CampaignWorldTimes.Find(x => x.CampaignId == weather.CampaignId && !x.Deleted && !x.Archived)
            .SortByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
        var seasonName = WorldCalendarMath.SeasonName(worldTime?.CurrentDateTime);
        var seasonalCandidates = Weather0217SeasonalPatternIds(weather);
        return new Dictionary<string, object>
        {
            ["id"] = weather.Id, ["campaignId"] = weather.CampaignId, ["worldId"] = weather.WorldId,
            ["scopeType"] = weather.Scope.ScopeType, ["scopeId"] = weather.Scope.ScopeId, ["scopeLabel"] = Weather0217ScopeLabel(weather),
            ["climateProfileId"] = weather.ClimateProfileId, ["patternId"] = weather.CurrentPatternId, ["patternName"] = weather.CurrentPatternName,
            ["temperatureC"] = weather.TrueTemperatureC, ["precipitation"] = weather.TruePrecipitation,
            ["windSpeedMps"] = weather.TrueWindSpeedMetersPerSecond, ["windDirectionFromDegrees"] = weather.TrueWindDirectionDegreesFromNorth,
            ["windGustMps"] = weather.TrueWindGustMetersPerSecond ?? 0m,
            ["windVector"] = Weather0217WindVectorPayload(WindVectorSnapshot.FromMeteorological(weather.TrueWindSpeedMetersPerSecond, weather.TrueWindDirectionDegreesFromNorth, weather.TrueWindGustMetersPerSecond)),
            ["windUnitSchemaVersion"] = weather.WindUnitSchemaVersion,
            ["visibilityM"] = weather.TrueVisibilityM, ["cloudCover"] = weather.TrueCloudCover, ["surfaceCondition"] = weather.TrueSurfaceCondition,
            ["severity"] = weather.Severity, ["sourceType"] = weather.SourceType, ["sourceId"] = weather.SourceId,
            ["startedAtWorldSecond"] = weather.StartedAtWorldSecond, ["scheduledTransitionAtWorldSecond"] = weather.ScheduledTransitionAtWorldSecond,
            ["worldSecond"] = worldSecond, ["generationSeed"] = weather.GenerationSeed, ["randomAlgorithmId"] = weather.RandomAlgorithmId,
            ["randomAlgorithmVersion"] = weather.RandomAlgorithmVersion, ["transitionIndex"] = weather.TransitionIndex,
            ["seasonName"] = seasonName, ["seasonalProfileApplied"] = seasonalCandidates.Count > 0,
            ["seasonalCandidateCount"] = seasonalCandidates.Count,
            ["isLocked"] = weather.IsLocked, ["lockUntilWorldSecond"] = weather.LockUntilWorldSecond ?? 0,
            ["overrideReason"] = weather.OverrideReason, ["revision"] = weather.EntityRevision, ["updatedAtUtc"] = weather.UpdatedAtUtc
        };
    }

    private Dictionary<string, object> Weather0217AdminResolvedContext(Dictionary<string, object> request, WeatherStateDocument weather, long worldSecond)
        => new()
        {
            ["contextMode"] = "explicit_route_context",
            ["campaignId"] = weather.CampaignId,
            ["campaignName"] = weather.CampaignId == "northern-path-0217" ? "Северный путь" : weather.CampaignId,
            ["sceneId"] = FirstNonEmpty(PayloadReader.GetString(request, "sceneId"), "north-road-scene-0217"),
            ["sceneName"] = "Дорога через северный лес",
            ["regionId"] = FirstNonEmpty(PayloadReader.GetString(request, "regionId"), weather.Scope.ScopeId),
            ["regionName"] = weather.Scope.ScopeId == "northern-valley-0217" ? "Северная долина" : Weather0217ScopeLabel(weather),
            ["resolvedWeatherScopeType"] = weather.Scope.ScopeType,
            ["resolvedWeatherScopeId"] = weather.Scope.ScopeId,
            ["resolvedWeatherScopeLabel"] = Weather0217ScopeLabel(weather),
            ["worldSecond"] = worldSecond
        };

    private static Dictionary<string, object> Weather0217WindVectorPayload(WindVectorSnapshot vector) => new()
    {
        ["speedMps"] = vector.SpeedMps,
        ["directionFromDegrees"] = vector.DirectionFromDegrees,
        ["flowDirectionDegrees"] = vector.FlowDirectionDegrees,
        ["vectorEastMps"] = vector.VectorEastMps,
        ["vectorNorthMps"] = vector.VectorNorthMps,
        ["gustSpeedMps"] = vector.GustSpeedMps ?? 0m,
        ["cardinalDirectionLabel"] = vector.CardinalDirectionLabel,
        ["calculationVersion"] = vector.CalculationVersion
    };

    private Dictionary<string, object> Weather0217EnvironmentPayload(EnvironmentSnapshot snapshot, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            ["scopeId"] = snapshot.ScopeId, ["worldSecond"] = snapshot.WorldSecond, ["patternName"] = snapshot.PatternName,
            ["temperatureC"] = snapshot.EffectiveTemperatureC, ["visibilityM"] = snapshot.VisibilityM,
            ["movementMultiplier"] = snapshot.MovementMultiplier, ["surfaceCondition"] = snapshot.SurfaceCondition,
            ["isIndoor"] = snapshot.IsIndoor, ["shelterName"] = snapshot.ShelterName,
            ["exposureMultiplier"] = snapshot.ExposureMultiplier, ["exposureSources"] = snapshot.ExposureSources.Cast<object>().ToArray(),
            ["warnings"] = snapshot.PublicWarnings.Cast<object>().ToArray(), ["calculationVersion"] = snapshot.CalculationVersion
        };
        if (admin) result["gmDiagnostics"] = snapshot.GmDiagnostics.Cast<object>().ToArray();
        return result;
    }

    private Dictionary<string, object> Weather0217TravelPayload(TravelSession travel, bool admin)
    {
        var segments = travel.Segments.Select(x => (object)new Dictionary<string, object>
        {
            ["order"] = x.Order, ["from"] = x.FromLocationName, ["to"] = x.ToLocationName, ["distanceKm"] = x.DistanceKm,
            ["terrain"] = x.TerrainName, ["durationMinutes"] = admin ? x.AuthoritativeDurationMinutes : x.PlayerEtaMaxMinutes,
            ["etaMinMinutes"] = x.PlayerEtaMinMinutes, ["etaMaxMinutes"] = x.PlayerEtaMaxMinutes,
            ["isCompleted"] = x.IsCompleted, ["warnings"] = x.HazardTags.Select(Weather0217HazardLabel).Cast<object>().ToArray()
        }).ToArray();
        if (admin)
        {
            segments = travel.Segments.Select(x => (object)new Dictionary<string, object>
            {
                ["order"] = x.Order, ["from"] = x.FromLocationName, ["to"] = x.ToLocationName, ["distanceKm"] = x.DistanceKm,
                ["terrain"] = x.TerrainName, ["durationMinutes"] = x.AuthoritativeDurationMinutes,
                ["etaMinMinutes"] = x.PlayerEtaMinMinutes, ["etaMaxMinutes"] = x.PlayerEtaMaxMinutes,
                ["isCompleted"] = x.IsCompleted, ["warnings"] = x.HazardTags.Select(Weather0217HazardLabel).Cast<object>().ToArray(),
                ["weatherPatternName"] = x.WeatherPatternName, ["weatherMultiplier"] = x.WeatherMultiplier,
                ["effectiveSpeedKmh"] = x.EffectiveSpeedKmh
            }).ToArray();
        }
        var result = new Dictionary<string, object>
        {
            ["travelId"] = travel.Id, ["partyName"] = travel.PartyName, ["origin"] = travel.OriginLocationName,
            ["destination"] = travel.DestinationLocationName, ["modeName"] = travel.ModeName, ["status"] = travel.Status,
            ["currentSegmentIndex"] = travel.CurrentSegmentIndex, ["progress"] = travel.Progress,
            ["etaMinMinutes"] = travel.PlayerEtaMinMinutes, ["etaMaxMinutes"] = travel.PlayerEtaMaxMinutes,
            ["segments"] = segments, ["requiredSupplies"] = travel.RequiredSupplyCategories.Cast<object>().ToArray(),
            ["availableSupplies"] = travel.AvailableSupplySummary.Cast<object>().ToArray(), ["interruptions"] = travel.ActiveInterruptions.Cast<object>().ToArray(),
            ["revision"] = travel.Revision
        };
        if (admin)
        {
            result["authoritativeDurationMinutes"] = travel.AuthoritativeEstimatedMinutes;
            result["partyActorIds"] = travel.PartyActorIds.Cast<object>().ToArray();
            result["partyMembers"] = travel.PartyMemberNames.Cast<object>().ToArray();
            result["inventorySource"] = "InventoryProfile";
        }
        return result;
    }

    private object Weather0217PlayerForecast(UserAccount actor, string campaignId, string scopeId)
    {
        var item = _mongo.EntityKnowledgeStates.Find(x => x.CampaignId == campaignId && x.OwnerUserId == actor.Id && x.KnowledgeId == Weather0217ForecastKnowledgeId && x.SourceId == scopeId && x.IsPlayerVisible && !x.IsArchived).FirstOrDefault();
        return item == null ? new Dictionary<string, object> { ["hasForecast"] = false } : Weather0217ForecastPayload(item, admin: false);
    }

    private object[] Weather0217PlayerForecasts(UserAccount actor, string campaignId)
        => _mongo.EntityKnowledgeStates.Find(x => x.CampaignId == campaignId && x.OwnerUserId == actor.Id && x.KnowledgeId == Weather0217ForecastKnowledgeId && x.IsPlayerVisible && !x.IsArchived)
            .ToList().Select(x => (object)Weather0217ForecastPayload(x, admin: false)).ToArray();

    private Dictionary<string, object> Weather0217ForecastPayload(EntityKnowledgeState item, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            ["hasForecast"] = true, ["scopeLabel"] = item.SourceLabel, ["summary"] = item.PlayerSummary,
            ["reliability"] = Weather0217Decimal(item.ExtraData, "reliability", 0.5m),
            ["windowStartMinutes"] = (int)Weather0217Decimal(item.ExtraData, "windowStartMinutes", 0),
            ["windowEndMinutes"] = (int)Weather0217Decimal(item.ExtraData, "windowEndMinutes", 0),
            ["isOutdated"] = Weather0217Bool(item.ExtraData, "isOutdated"), ["sourceLabel"] = item.SourceLabel
        };
        if (admin) result["characterId"] = item.EntityId;
        return result;
    }

    private void Weather0217RecalculateTravel(TravelSession travel)
    {
        foreach (var segment in travel.Segments)
        {
            var speed = travel.ModeBaseSpeedKmh * segment.ModeMultiplier * segment.TerrainMultiplier * segment.WeatherMultiplier * segment.LoadMultiplier;
            segment.EffectiveSpeedKmh = Math.Round(speed, 2, MidpointRounding.AwayFromZero);
            if (segment.AuthoritativeDurationMinutes <= 0)
            {
                segment.AuthoritativeDurationMinutes = speed <= 0 ? 0 : (int)Math.Ceiling(segment.DistanceKm / speed * 60m);
            }
            if (segment.PlayerEtaMinMinutes <= 0) segment.PlayerEtaMinMinutes = Math.Max(1, (int)Math.Floor(segment.AuthoritativeDurationMinutes * 0.9m));
            if (segment.PlayerEtaMaxMinutes <= 0) segment.PlayerEtaMaxMinutes = Math.Max(segment.PlayerEtaMinMinutes, (int)Math.Ceiling(segment.AuthoritativeDurationMinutes * 1.25m));
        }
        var remaining = travel.Segments.Where(x => !x.IsCompleted).ToList();
        travel.AuthoritativeEstimatedMinutes = remaining.Sum(x => x.AuthoritativeDurationMinutes);
        travel.PlayerEtaMinMinutes = remaining.Sum(x => x.PlayerEtaMinMinutes);
        travel.PlayerEtaMaxMinutes = remaining.Sum(x => x.PlayerEtaMaxMinutes);
    }

    private TravelSession Weather0217RequireTravel(Dictionary<string, object>? payload)
    {
        var id = RequireLength(PayloadReader.GetString(payload, "travelId"), 1, 128, "travelId");
        return _mongo.TravelSessions0217.Find(x => x.Id == id && x.Status != TravelStatusIds.Archived).FirstOrDefault()
               ?? throw new InvalidOperationException("Путешествие не найдено.");
    }

    private List<TravelRouteSegment> Weather0217ReadSegments(Dictionary<string, object> payload)
    {
        var result = new List<TravelRouteSegment>();
        if (!payload.TryGetValue("segments", out var raw) || raw is not IEnumerable sequence || raw is string) return result;
        foreach (var value in sequence)
        {
            var map = value as Dictionary<string, object> ?? (value is BsonDocument bson ? bson.ToDictionary(x => x.Name, x => BsonTypeMapper.MapToDotNetValue(x.Value)) : null);
            if (map == null) continue;
            result.Add(new TravelRouteSegment
            {
                Order = PayloadReader.GetInt(map, "order") ?? result.Count + 1,
                FromLocationId = PayloadReader.GetString(map, "fromLocationId") ?? string.Empty,
                FromLocationName = PayloadReader.GetString(map, "fromLocationName") ?? string.Empty,
                ToLocationId = PayloadReader.GetString(map, "toLocationId") ?? string.Empty,
                ToLocationName = PayloadReader.GetString(map, "toLocationName") ?? string.Empty,
                DistanceKm = (decimal)(PayloadReader.GetDouble(map, "distanceKm") ?? 0d),
                TerrainProfileId = PayloadReader.GetString(map, "terrainProfileId") ?? string.Empty,
                TerrainName = PayloadReader.GetString(map, "terrainName") ?? string.Empty,
                WeatherPatternId = PayloadReader.GetString(map, "weatherPatternId") ?? string.Empty,
                WeatherPatternName = PayloadReader.GetString(map, "weatherPatternName") ?? string.Empty,
                ModeMultiplier = (decimal)(PayloadReader.GetDouble(map, "modeMultiplier") ?? 1d),
                TerrainMultiplier = (decimal)(PayloadReader.GetDouble(map, "terrainMultiplier") ?? 1d),
                WeatherMultiplier = (decimal)(PayloadReader.GetDouble(map, "weatherMultiplier") ?? 1d),
                LoadMultiplier = (decimal)(PayloadReader.GetDouble(map, "loadMultiplier") ?? 1d),
                HazardTags = Weather0217StringList(map, "hazardTags")
            });
        }
        return result;
    }

    private void Weather0217EnsureExposureSuggestion(TravelSession travel, TravelRouteSegment segment, string actorId)
    {
        var characterId = travel.PartyActorIds.FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(characterId)) return;
        var fingerprint = $"weather-exposure:{travel.Id}:{segment.Order}:{characterId}";
        if (_mongo.ActionRequests.Find(x => x.Fingerprint == fingerprint && x.Status == RequestStatus.Pending).Any()) return;
        var request = new ActionRequest
        {
            RequestType = "environment_exposure",
            ActionCode = Weather0217ExposureAction,
            CreatorUserId = actorId,
            RelatedUserId = travel.PartyOwnerUserIds.FirstOrDefault() ?? string.Empty,
            CharacterId = characterId,
            Status = RequestStatus.Pending,
            Description = "Холод и сырость достигли порога. Требуется решение мастера.",
            Fingerprint = fingerprint,
            PayloadJson = new BsonDocument { ["characterId"] = characterId, ["travelId"] = travel.Id, ["segmentOrder"] = segment.Order, ["effectDefinitionId"] = "condition_cold_wet_0217" }.ToJson()
        };
        _mongo.ActionRequests.InsertOne(request);
        var session = _repositories.CurrentSessions.Find(
            Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, travel.CampaignId)
            & Builders<CurrentSessionState>.Filter.Eq(x => x.ActiveTravelSessionId, travel.Id)
            & Builders<CurrentSessionState>.Filter.Ne(x => x.Status, CurrentSessionStatusIds.Completed)).FirstOrDefault();
        if (session != null)
            EvaluateAutomationEvent02110(session, "weather.exposure.harmful", request.Id, actorId);
    }

    private object[] Weather0217ExposureQueue(string campaignId)
        => _mongo.ActionRequests.Find(x => x.ActionCode == Weather0217ExposureAction && x.Status == RequestStatus.Pending).ToList()
            .Select(x => (object)new Dictionary<string, object> { ["suggestionId"] = x.Id, ["title"] = "Воздействие холода и сырости", ["summary"] = x.Description, ["requiresGmApproval"] = true }).ToArray();

    private void Weather0217MarkForecastsStale(string campaignId, long worldSecond)
    {
        var items = _mongo.EntityKnowledgeStates.Find(x => x.CampaignId == campaignId && x.KnowledgeId == Weather0217ForecastKnowledgeId && !x.IsArchived).ToList();
        foreach (var item in items)
        {
            var learned = (long)Weather0217Decimal(item.ExtraData, "learnedAtWorldSecond", 0);
            if (worldSecond - learned < 6 * 3600) continue;
            item.ExtraData["isOutdated"] = true;
            item.TruthRelation = KnowledgeTruthRelationIds.Outdated;
            item.UpdatedAtUtc = DateTime.UtcNow;
            _mongo.EntityKnowledgeStates.ReplaceOne(x => x.Id == item.Id, item);
        }
    }

    private void Weather0217EnsureFixtureDefinitions(string actorId)
    {
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.Climate, "climate_northern_temperate_0217", "Северный умеренный", "Умеренно-холодный климат северных долин.", new Dictionary<string, object>
        {
            ["transitionProfileId"] = "transition_northern_0217",
            ["allowedPatternIds"] = new[] { "weather_cold_rain_0217", "weather_storm_front_0217" },
            ["seasonBindings"] = new[] { "Зима", "Весна", "Лето", "Осень" },
            ["winterPatternIds"] = new[] { "weather_cold_rain_0217", "weather_storm_front_0217" },
            ["springPatternIds"] = new[] { "weather_cold_rain_0217", "weather_storm_front_0217" },
            ["summerPatternIds"] = new[] { "weather_cold_rain_0217" },
            ["autumnPatternIds"] = new[] { "weather_cold_rain_0217", "weather_storm_front_0217" },
            ["allowsSevereWeather"] = true,
            ["defaultForecastProfileId"] = "forecast_northern_0217"
        }, actorId);
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.WeatherPattern, "weather_cold_rain_0217", "Холодный дождь", "Холодный умеренный дождь и мокрая дорога.", new Dictionary<string, object> { ["temperatureC"] = 8d, ["precipitation"] = "Умеренный дождь", ["windSpeedMps"] = 5d, ["windDirectionFromDegrees"] = 315d, ["visibilityM"] = 900, ["cloudCover"] = "Плотная облачность", ["surfaceCondition"] = "Мокрая", ["severity"] = "minor" }, actorId);
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.WeatherPattern, "weather_storm_front_0217", "Штормовой фронт", "Сильный дождь, порывистый ветер и плохая видимость.", new Dictionary<string, object> { ["temperatureC"] = 6d, ["precipitation"] = "Сильный дождь", ["windSpeedMps"] = 12.5d, ["windDirectionFromDegrees"] = 300d, ["visibilityM"] = 350, ["cloudCover"] = "Грозовые облака", ["surfaceCondition"] = "Грязь", ["severity"] = "severe" }, actorId);
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.WeatherTransition, "transition_northern_0217", "Северный погодный переход", "Версионированный переход от холодного дождя к шторму.", new Dictionary<string, object> { ["sourcePatternId"] = "weather_cold_rain_0217", ["destinationPatternIds"] = new[] { "weather_storm_front_0217" } }, actorId);
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.EnvironmentInteraction, "interaction_storm_navigation_0217", "Шторм и ориентирование", "Погодный слой Fate для ориентирования без двойного штрафа.", new Dictionary<string, object> { ["applicationChannel"] = EnvironmentApplicationChannelIds.FateLayer, ["targetTags"] = new[] { "travel_navigation" }, ["doubleApplicationAllowed"] = false }, actorId);
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.Exposure, "exposure_cold_wet_0217", "Холод и сырость", "Накапливается в холодном дожде; вредный эффект требует решения мастера.", new Dictionary<string, object> { ["automationMode"] = ExposureAutomationModeIds.RequiresGmApproval, ["runtimeEffectDefinitionId"] = "condition_cold_wet_0217" }, actorId);
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.Shelter, "shelter_stone_canopy_0217", "Каменный навес", "Уменьшает воздействие сырости, не изменяя наружную погоду.", new Dictionary<string, object> { ["wetReduction"] = 0.75d }, actorId);
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.Forecast, "forecast_northern_0217", "Северный краткий прогноз", "Приблизительный прогноз на несколько часов.", new Dictionary<string, object> { ["reliability"] = 0.75d, ["horizonMinutes"] = 180 }, actorId);
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.TravelMode, "travel_walking_0217", "Пешком", "Обычное пешее путешествие.", new Dictionary<string, object> { ["baseSpeedKmh"] = 4d }, actorId);
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.TerrainTravel, "terrain_northern_road_0217", "Северная дорога", "Лесная дорога северной долины.", new Dictionary<string, object> { ["movementMultiplier"] = 1d }, actorId);
        Weather0217UpsertDefinition(WeatherDefinitionFamilyIds.TerrainTravel, "terrain_stone_pass_0217", "Каменистый подъём", "Сложный подъём к перевалу.", new Dictionary<string, object> { ["movementMultiplier"] = 0.8d }, actorId);
    }

    private void Weather0217UpsertDefinition(string family, string id, string name, string publicDescription, Dictionary<string, object> extra, string actorId)
    {
        var now = DateTime.UtcNow;
        var item = _mongo.ContentDefinitionRecords.Find(x => x.Category == family && x.Id == id).FirstOrDefault()
                   ?? new ContentDefinitionRecord { Id = id, Category = family, CreatedAtUtc = now, CreatedUtc = now, CreatedByUserId = actorId };
        item.Name = id;
        item.DisplayName = name;
        item.PublicDescription = publicDescription;
        item.GMDescription = publicDescription;
        item.AllowedRuleSetIds = new List<string> { RuleSetIds.FantasyNriDefault };
        item.VisibilityRule = ContentDefinitionVisibilityRules.PlayerVisible;
        item.IsArchived = false;
        item.Archived = false;
        item.UpdatedAtUtc = now;
        item.UpdatedUtc = now;
        item.UpdatedByUserId = actorId;
        item.SourceDocument = "foundation_0_21_7_fixture";
        item.CustomFields = extra;
        item.ReferenceIds = extra
            .Where(x => x.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || x.Key.EndsWith("Ids", StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => Weather0217ReferenceValues(x.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        item.ServerOnlyData = new Dictionary<string, object> { ["updatedByUserId"] = actorId };
        item.Revision = Math.Max(1, item.Revision + 1);
        _mongo.ContentDefinitionRecords.ReplaceOne(x => x.Category == family && x.Id == id, item, new ReplaceOptions { IsUpsert = true });
    }

    private static IEnumerable<string> Weather0217ReferenceValues(object? raw)
    {
        if (raw == null) yield break;
        if (raw is string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) yield return text.Trim();
            yield break;
        }
        if (raw is IEnumerable values)
        {
            foreach (var value in values)
            {
                var textValue = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(textValue)) yield return textValue.Trim();
            }
        }
    }

    private WorldCalendarDefinition Weather0217CreateCalendar(string campaignId, string actorId)
    {
        var calendar = new WorldCalendarDefinition { CampaignId = campaignId, RuleSetId = RuleSetIds.FantasyNriDefault, Name = "Календарь Северного пути", IsDefault = true, IsActive = true, CreatedByUserId = actorId, UpdatedByUserId = actorId };
        _repositories.WorldCalendarDefinitions.Insert(calendar);
        return calendar;
    }

    private string Weather0217CampaignId(Dictionary<string, object> payload) => FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), "northern-path-0217");
    private long Weather0217WorldSecond(string campaignId) => _mongo.CampaignWorldTimes.Find(x => x.CampaignId == campaignId && !x.Deleted && !x.Archived).SortByDescending(x => x.UpdatedAtUtc).FirstOrDefault()?.CurrentDateTime.AbsoluteSecondIndex ?? 0;
    private string Weather0217ScopeLabel(WeatherStateDocument weather) => weather.Scope.ScopeId == "northern-valley-0217" ? "Северная долина" : FirstNonEmpty(weather.Scope.ScopeId, "Область мира");
    private string Weather0217HazardLabel(string value) => value == "cold_wet" ? "Холод и сырость" : value.Replace('_', ' ');
    private string Weather0217FindUserId(string login) => _mongo.Accounts.Find(x => x.Login == login && x.Status == AccountStatus.Active).FirstOrDefault()?.Id ?? string.Empty;
    private string Weather0217FindActiveCharacterId(string ownerUserId) => _mongo.CharacterOwnerships.Find(x => x.OwnerUserId == ownerUserId && x.IsActive && !x.IsArchived).FirstOrDefault()?.CharacterId ?? string.Empty;
    private void Weather0217Touch(WeatherStateDocument weather, string actorId) { weather.EntityRevision++; weather.UpdatedAtUtc = DateTime.UtcNow; weather.UpdatedUtc = weather.UpdatedAtUtc; weather.UpdatedBy = actorId; }
    private void Weather0217Sync(string type, string campaignId, string entityType, string entityId, string operation, string actorId, string? requestId) => TryPublishSyncEvent(type, campaignId, entityType, entityId, operation, actorId, new Dictionary<string, object> { ["campaignId"] = campaignId, ["entityId"] = entityId }, requestId ?? string.Empty);

    private static List<string> Weather0217StringList(Dictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var raw) || raw == null) return new List<string>();
        if (raw is string text) return text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (raw is IEnumerable values) return values.Cast<object>().Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new List<string>();
    }

    private static decimal Weather0217Decimal(Dictionary<string, object>? map, string key, decimal fallback)
    {
        if (map == null || !map.TryGetValue(key, out var raw) || raw == null) return fallback;
        return decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static string Weather0217String(Dictionary<string, object>? map, string key, string fallback) => map != null && map.TryGetValue(key, out var raw) ? Convert.ToString(raw, CultureInfo.InvariantCulture) ?? fallback : fallback;
    private static bool Weather0217Bool(Dictionary<string, object>? map, string key) => map != null && map.TryGetValue(key, out var raw) && bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var value) && value;
}
