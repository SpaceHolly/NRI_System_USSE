using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private static List<DefinitionEditorProfile> BuildWeatherEnvironmentTravelDefinitionEditorProfiles0217()
    {
        var profiles = new List<DefinitionEditorProfile>
        {
            WeatherProfile0217("weather_climate_profile_0217", WeatherDefinitionFamilyIds.Climate, "Климат", "Сезонные правила и допустимые погодные шаблоны.", new[]
            {
                Field0181("applicableScopeTags", "Области применения", ContentDefinitionFieldTypes.Tags, false),
                Field0181("seasonBindings", "Сезоны", ContentDefinitionFieldTypes.Tags, false),
                Field0181("baselineTemperatureProfile", "Температурный профиль", ContentDefinitionFieldTypes.LongText, false),
                Field0181("moistureProfile", "Профиль влажности", ContentDefinitionFieldTypes.LongText, false),
                Field0181("windProfile", "Профиль ветра", ContentDefinitionFieldTypes.LongText, false),
                Field0181("transitionProfileId", "Правила переходов", ContentDefinitionFieldTypes.Reference, true, referenceCategory: WeatherDefinitionFamilyIds.WeatherTransition),
                Field0181("allowedPatternIds", "Допустимая погода", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: WeatherDefinitionFamilyIds.WeatherPattern),
                Field0181("winterPatternIds", "Погода зимой", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WeatherDefinitionFamilyIds.WeatherPattern),
                Field0181("springPatternIds", "Погода весной", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WeatherDefinitionFamilyIds.WeatherPattern),
                Field0181("summerPatternIds", "Погода летом", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WeatherDefinitionFamilyIds.WeatherPattern),
                Field0181("autumnPatternIds", "Погода осенью", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WeatherDefinitionFamilyIds.WeatherPattern),
                Field0181("allowsSevereWeather", "Разрешить опасную погоду", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("defaultForecastProfileId", "Прогноз по умолчанию", ContentDefinitionFieldTypes.Reference, false, referenceCategory: WeatherDefinitionFamilyIds.Forecast)
            }),
            WeatherProfile0217("weather_pattern_profile_0217", WeatherDefinitionFamilyIds.WeatherPattern, "Погодные шаблоны", "Наблюдаемые параметры одного состояния погоды.", new[]
            {
                Field0181("temperatureC", "Температура", ContentDefinitionFieldTypes.Decimal, true, min: -150, max: 150),
                Field0181("precipitation", "Осадки", ContentDefinitionFieldTypes.Enum, true, new[] { "none", "light_rain", "moderate_rain", "heavy_rain", "snow", "hail", "custom" }),
                Field0181("windKmh", "Ветер, км/ч", ContentDefinitionFieldTypes.Decimal, true, min: 0, max: 500),
                Field0181("visibilityM", "Видимость, м", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100000),
                Field0181("cloudCover", "Облачность", ContentDefinitionFieldTypes.String, false),
                Field0181("surfaceCondition", "Состояние поверхности", ContentDefinitionFieldTypes.String, false),
                Field0181("humidityPercent", "Влажность, %", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("severity", "Тяжесть", ContentDefinitionFieldTypes.Enum, true, new[] { "none", "minor", "moderate", "severe", "extreme", "custom" }),
                Field0181("minimumDurationMinutes", "Минимальная длительность, мин", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 525600),
                Field0181("maximumDurationMinutes", "Максимальная длительность, мин", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 525600),
                Field0181("possibleNextPatternIds", "Возможные следующие состояния", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WeatherDefinitionFamilyIds.WeatherPattern),
                Field0181("environmentInteractionProfileId", "Взаимодействие со средой", ContentDefinitionFieldTypes.Reference, false, referenceCategory: WeatherDefinitionFamilyIds.EnvironmentInteraction),
                Field0181("publicObservationTemplate", "Описание для игроков", ContentDefinitionFieldTypes.LongText, false)
            }),
            WeatherProfile0217("weather_transition_profile_0217", WeatherDefinitionFamilyIds.WeatherTransition, "Переходы погоды", "Детерминированные варианты смены погодных шаблонов.", new[]
            {
                Field0181("sourcePatternId", "Исходная погода", ContentDefinitionFieldTypes.Reference, true, referenceCategory: WeatherDefinitionFamilyIds.WeatherPattern),
                Field0181("destinationPatternIds", "Следующая погода", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: WeatherDefinitionFamilyIds.WeatherPattern),
                Field0181("seasonModifiers", "Сезонные поправки", ContentDefinitionFieldTypes.Tags, false),
                Field0181("minimumDurationMinutes", "Минимальная длительность, мин", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 525600),
                Field0181("maximumDurationMinutes", "Максимальная длительность, мин", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 525600),
                Field0181("severeEscalationAllowed", "Разрешить усиление до опасной погоды", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("deterministicSeedScope", "Область детерминированного зерна", ContentDefinitionFieldTypes.Enum, true, new[] { "scope", "world", "campaign", "custom" }),
                Field0181("repetitionCooldown", "Защита от повторения", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 1000)
            }),
            WeatherProfile0217("environment_profile_0217", WeatherDefinitionFamilyIds.Environment, "Окружение", "Базовые условия местности до применения локальной погоды и укрытия.", new[]
            {
                Field0181("medium", "Среда", ContentDefinitionFieldTypes.Enum, true, new[] { "air", "water", "vacuum", "custom" }),
                Field0181("temperatureC", "Температура", ContentDefinitionFieldTypes.Decimal, false, min: -273, max: 10000),
                Field0181("pressureKpa", "Давление, кПа", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100000),
                Field0181("isBreathable", "Можно дышать", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("radiation", "Радиация", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100000),
                Field0181("toxicity", "Токсичность", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100000),
                Field0181("gravityBand", "Гравитация", ContentDefinitionFieldTypes.String, false),
                Field0181("lightBand", "Освещение", ContentDefinitionFieldTypes.String, false),
                Field0181("soundProfile", "Распространение звука", ContentDefinitionFieldTypes.String, false),
                Field0181("anomalousField", "Аномальное поле", ContentDefinitionFieldTypes.String, false),
                Field0181("surfaceState", "Состояние поверхности", ContentDefinitionFieldTypes.String, false),
                Field0181("isIndoor", "В помещении", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("terrainTags", "Признаки местности", ContentDefinitionFieldTypes.Tags, false)
            }),
            WeatherProfile0217("environment_interaction_profile_0217", WeatherDefinitionFamilyIds.EnvironmentInteraction, "Взаимодействия среды", "Явный маршрут влияния среды на Fate или другой разрешённый слой.", new[]
            {
                Field0181("applicationChannel", "Канал применения", ContentDefinitionFieldTypes.Enum, true, new[] { EnvironmentApplicationChannelIds.FateLayer, EnvironmentApplicationChannelIds.DeterministicModifier, EnvironmentApplicationChannelIds.PresentationOnly, EnvironmentApplicationChannelIds.RuntimeEffect, EnvironmentApplicationChannelIds.Travel, EnvironmentApplicationChannelIds.MultipleExplicit }),
                Field0181("targetTags", "Применимые действия", ContentDefinitionFieldTypes.Tags, true),
                Field0181("requiredEnvironmentTags", "Условия среды", ContentDefinitionFieldTypes.Tags, false),
                Field0181("movementMultiplier", "Множитель движения", ContentDefinitionFieldTypes.Decimal, false, min: 0.05m, max: 10m),
                Field0181("capabilityModifier", "Поправка к способности", ContentDefinitionFieldTypes.Decimal, false, min: -1000, max: 1000),
                Field0181("fateEnvironmentProfileId", "Профиль слоя Fate", ContentDefinitionFieldTypes.String, false),
                Field0181("availabilityPolicy", "Доступность действий", ContentDefinitionFieldTypes.LongText, false),
                Field0181("exposureProfileId", "Профиль воздействия", ContentDefinitionFieldTypes.Reference, false, referenceCategory: WeatherDefinitionFamilyIds.Exposure),
                Field0181("explicitChannels", "Явно разрешённые каналы", ContentDefinitionFieldTypes.Tags, false),
                Field0181("doubleApplicationAllowed", "Разрешить двойное применение", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("playerExplanation", "Объяснение для игрока", ContentDefinitionFieldTypes.LongText, false)
            }),
            WeatherProfile0217("exposure_profile_0217", WeatherDefinitionFamilyIds.Exposure, "Воздействие среды", "Накопление риска и политика автоматизации последствия.", new[]
            {
                Field0181("automationMode", "Политика применения", ContentDefinitionFieldTypes.Enum, true, new[] { ExposureAutomationModeIds.TrackOnly, ExposureAutomationModeIds.SuggestCheck, ExposureAutomationModeIds.RequiresGmApproval, ExposureAutomationModeIds.AutoApplyPreauthorized, ExposureAutomationModeIds.Blocked }),
                Field0181("runtimeEffectDefinitionId", "Применяемый эффект", ContentDefinitionFieldTypes.Reference, true, referenceCategory: DefinitionCategoryIds.Condition),
                Field0181("exposureKind", "Вид воздействия", ContentDefinitionFieldTypes.String, true),
                Field0181("sourceTags", "Источники", ContentDefinitionFieldTypes.Tags, false),
                Field0181("applicabilityTags", "К кому применяется", ContentDefinitionFieldTypes.Tags, false),
                Field0181("accumulationUnit", "Единица накопления", ContentDefinitionFieldTypes.String, false),
                Field0181("threshold", "Порог", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100000),
                Field0181("decayPerHour", "Восстановление в час", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100000),
                Field0181("shelterReduction", "Снижение в укрытии", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 1),
                Field0181("equipmentReduction", "Снижение экипировкой", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 1),
                Field0181("suggestedCheck", "Рекомендуемая проверка", ContentDefinitionFieldTypes.String, false),
                Field0181("publicWarning", "Предупреждение игроку", ContentDefinitionFieldTypes.LongText, false)
            }),
            WeatherProfile0217("shelter_profile_0217", WeatherDefinitionFamilyIds.Shelter, "Укрытия", "Снижение воздействия без изменения истинной наружной погоды.", new[]
            {
                Field0181("wetReduction", "Защита от сырости", ContentDefinitionFieldTypes.Decimal, true, min: 0, max: 1),
                Field0181("coldReduction", "Защита от холода", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 1),
                Field0181("windReduction", "Защита от ветра", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 1),
                Field0181("capacity", "Вместимость", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("setupRequirement", "Требования установки", ContentDefinitionFieldTypes.LongText, false),
                Field0181("setupMinutes", "Время установки, мин", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 525600),
                Field0181("itemReferences", "Связанные предметы", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: DefinitionCategoryIds.Item),
                Field0181("locationReferences", "Связанные локации", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: DefinitionCategoryIds.Location),
                Field0181("failurePolicy", "Повреждение и отказ", ContentDefinitionFieldTypes.LongText, false)
            }),
            WeatherProfile0217("forecast_profile_0217", WeatherDefinitionFamilyIds.Forecast, "Прогнозы", "Правила надёжности и горизонта доступного персонажу прогноза.", new[]
            {
                Field0181("reliability", "Надёжность", ContentDefinitionFieldTypes.Decimal, true, min: 0, max: 1),
                Field0181("horizonMinutes", "Горизонт, мин", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 525600),
                Field0181("sourceType", "Источник наблюдения", ContentDefinitionFieldTypes.String, false),
                Field0181("reliabilityDecayPerHour", "Снижение надёжности в час", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 1),
                Field0181("uncertaintyPolicy", "Представление неопределённости", ContentDefinitionFieldTypes.Enum, true, new[] { "exact", "approximate", "qualitative", "custom" }),
                Field0181("publicTemplate", "Шаблон для игрока", ContentDefinitionFieldTypes.LongText, false),
                Field0181("staleAfterMinutes", "Считать устаревшим через, мин", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 525600),
                Field0181("requiredKnowledgeLevel", "Требуемый уровень знания", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 100)
            }),
            WeatherProfile0217("travel_mode_profile_0217", WeatherDefinitionFamilyIds.TravelMode, "Режимы путешествия", "Скорость и требования способа передвижения.", new[]
            {
                Field0181("baseSpeedKmh", "Базовая скорость, км/ч", ContentDefinitionFieldTypes.Decimal, true, min: 0.1m, max: 10000m),
                Field0181("movementMedium", "Среда движения", ContentDefinitionFieldTypes.Enum, true, new[] { "land", "water", "air", "space", "custom" }),
                Field0181("requiredTags", "Требования", ContentDefinitionFieldTypes.Tags, false),
                Field0181("terrainCompatibilityTags", "Совместимая местность", ContentDefinitionFieldTypes.Tags, false),
                Field0181("weatherCompatibilityTags", "Допустимая погода", ContentDefinitionFieldTypes.Tags, false),
                Field0181("encumbrancePolicy", "Правила нагрузки", ContentDefinitionFieldTypes.LongText, false),
                Field0181("requiredSkillIds", "Необходимые навыки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: DefinitionCategoryIds.Skill),
                Field0181("requiredToolIds", "Необходимые инструменты", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: DefinitionCategoryIds.Item),
                Field0181("requiredSupplyCategories", "Необходимые запасы", ContentDefinitionFieldTypes.Tags, false),
                Field0181("restPolicy", "Правила отдыха", ContentDefinitionFieldTypes.LongText, false),
                Field0181("passengerCargoPolicy", "Пассажиры и груз", ContentDefinitionFieldTypes.LongText, false),
                Field0181("environmentProtectionIds", "Защита от среды", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WeatherDefinitionFamilyIds.Shelter),
                Field0181("automationPolicy", "Политика автоматизации", ContentDefinitionFieldTypes.Enum, true, new[] { "safe_auto", "confirmation", "gm_approval", "gm_only" })
            }),
            WeatherProfile0217("terrain_travel_profile_0217", WeatherDefinitionFamilyIds.TerrainTravel, "Проходимость местности", "Множитель времени пути для типа местности.", new[]
            {
                Field0181("movementMultiplier", "Множитель движения", ContentDefinitionFieldTypes.Decimal, true, min: 0.05m, max: 10m),
                Field0181("terrainTags", "Признаки местности", ContentDefinitionFieldTypes.Tags, false),
                Field0181("allowedModeIds", "Допустимые способы пути", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: WeatherDefinitionFamilyIds.TravelMode),
                Field0181("weatherInteractionTags", "Влияние погоды", ContentDefinitionFieldTypes.Tags, false),
                Field0181("hazardTags", "Опасности", ContentDefinitionFieldTypes.Tags, false),
                Field0181("shelterHints", "Доступные укрытия", ContentDefinitionFieldTypes.LongText, false),
                Field0181("navigationDifficulty", "Сложность ориентирования", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 100)
            })
        };
        return profiles;
    }

    private static DefinitionEditorProfile WeatherProfile0217(string id, string category, string name, string description, IEnumerable<DefinitionFieldSchema> fields)
    {
        var profile = Profile0181(id, category, name, description, fields);
        profile.SchemaVersion = 1;
        profile.DefaultTags = profile.DefaultTags.Concat(new[] { "foundation_0_21_7", "weather_environment_travel" }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        profile.ValidationRules.Add("weather-environment-travel-typed-validation");
        foreach (var field in profile.FieldSchemas)
            field.HelpText = (field.IsRequired ? "Обязательное поле. " : string.Empty) + "Используется сервером при расчёте погоды, окружения или пути.";
        return profile;
    }
}
