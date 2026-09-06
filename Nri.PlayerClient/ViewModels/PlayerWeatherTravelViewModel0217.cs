using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.Shared.Contracts;
using Nri.Ui.Wpf;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerTravelSegmentRow0217
{
    public string Route { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class PlayerObservationRow0217B
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Freshness { get; set; } = string.Empty;
}

public partial class PlayerMainViewModel
{
    private string _weather0217Status = "Загрузка погоды и путешествия...";
    private string _weather0217Pattern = "Погода пока неизвестна";
    private string _weather0217Summary = "Наблюдения появятся после загрузки.";
    private string _weather0217Conditions = string.Empty;
    private string _weather0217Environment = string.Empty;
    private string _weather0217Assessment = "Личная оценка окружения ещё не рассчитана.";
    private string _weather0217Protection = "Сведения о защите ещё не загружены.";
    private string _weather0217Forecast = "У персонажа нет известного прогноза.";
    private string _weather0217ForecastReliability = string.Empty;
    private string _weather0217TravelTitle = "Активного путешествия нет";
    private string _weather0217TravelProgress = string.Empty;
    private string _weather0217TravelEta = string.Empty;
    private string _weather0217Exposure = "Нет предупреждений о воздействии среды.";
    private string _weather0217AppliedEffects = "Воздействия среды не применены.";
    private ICommand? _refreshWeatherTravel0217Command;
    private ICommand? _measureTemperature0217BCommand;
    private ICommand? _measureWindSpeed0217BCommand;
    private ICommand? _measureWindDirection0217BCommand;
    private ICommand? _estimateDistance0217BCommand;

    public ObservableCollection<PlayerTravelSegmentRow0217> WeatherTravelSegments0217 { get; } = new();
    public ObservableCollection<PlayerObservationRow0217B> WeatherObservations0217B { get; } = new();
    public string Weather0217Status { get => _weather0217Status; private set { _weather0217Status = value; Notify(); } }
    public string Weather0217Pattern { get => _weather0217Pattern; private set { _weather0217Pattern = value; Notify(); } }
    public string Weather0217Summary { get => _weather0217Summary; private set { _weather0217Summary = value; Notify(); } }
    public string Weather0217Conditions { get => _weather0217Conditions; private set { _weather0217Conditions = value; Notify(); } }
    public string Weather0217Environment { get => _weather0217Environment; private set { _weather0217Environment = value; Notify(); } }
    public string Weather0217Assessment { get => _weather0217Assessment; private set { _weather0217Assessment = value; Notify(); } }
    public string Weather0217Protection { get => _weather0217Protection; private set { _weather0217Protection = value; Notify(); } }
    public string Weather0217Forecast { get => _weather0217Forecast; private set { _weather0217Forecast = value; Notify(); } }
    public string Weather0217ForecastReliability { get => _weather0217ForecastReliability; private set { _weather0217ForecastReliability = value; Notify(); } }
    public string Weather0217TravelTitle { get => _weather0217TravelTitle; private set { _weather0217TravelTitle = value; Notify(); } }
    public string Weather0217TravelProgress { get => _weather0217TravelProgress; private set { _weather0217TravelProgress = value; Notify(); } }
    public string Weather0217TravelEta { get => _weather0217TravelEta; private set { _weather0217TravelEta = value; Notify(); } }
    public string Weather0217Exposure { get => _weather0217Exposure; private set { _weather0217Exposure = value; Notify(); } }
    public string Weather0217AppliedEffects { get => _weather0217AppliedEffects; private set { _weather0217AppliedEffects = value; Notify(); } }
    public ICommand RefreshWeatherTravel0217Command => _refreshWeatherTravel0217Command ??= new RelayCommand(RefreshWeatherTravel0217);
    public ICommand MeasureTemperature0217BCommand => _measureTemperature0217BCommand ??= new RelayCommand(() => MeasureEnvironment0217B("temperature"));
    public ICommand MeasureWindSpeed0217BCommand => _measureWindSpeed0217BCommand ??= new RelayCommand(() => MeasureEnvironment0217B("wind_speed"));
    public ICommand MeasureWindDirection0217BCommand => _measureWindDirection0217BCommand ??= new RelayCommand(() => MeasureEnvironment0217B("wind_direction"));
    public ICommand EstimateDistance0217BCommand => _estimateDistance0217BCommand ??= new RelayCommand(EstimateDistance0217B);

    private Dictionary<string, object> WeatherScope0217B() => new()
    {
        ["campaignId"] = "northern-path-0217", ["regionId"] = "northern-valley-0217",
        ["characterId"] = FirstNonEmpty(ActiveCharacterId, SelectedCharacterId)
    };

    private void RefreshWeatherTravel0217()
    {
        Weather0217Status = "Обновление...";
        var response = _api.WorldPlayerObserveCurrent(WeatherScope0217B());
        if (response.Status != ResponseStatus.Ok)
        {
            Weather0217Status = string.IsNullOrWhiteSpace(response.Message) ? "Не удалось загрузить погоду." : response.Message;
            return;
        }
        var weather = Map0217(response.Payload.TryGetValue("observation", out var rawWeather) ? rawWeather : null);
        var assessment = Map0217(response.Payload.TryGetValue("assessment", out var rawAssessment) ? rawAssessment : null);
        Weather0217Pattern = Text0217(weather, "patternName", "Погода пока неизвестна");
        Weather0217Summary = Text0217(weather, "summary");
        Weather0217Conditions = string.Join("  •  ", new[] { Text0217(weather, "temperatureBand"), Text0217(weather, "windBand"), $"Видимость: {Text0217(weather, "visibilityBand")}", Text0217(weather, "precipitation") }.Where(x => !string.IsNullOrWhiteSpace(x)));
        Weather0217Environment = "Это наблюдаемая обстановка. Точные значения появляются только после явного измерения.";
        Weather0217Assessment = Text0217(assessment, "publicExplanation", "Личная оценка недоступна.");
        Weather0217Protection = Text0217(assessment, "protectionSummary", "Дополнительной защиты нет.");
        var warnings = List0217(assessment, "warnings").Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        Weather0217Exposure = warnings.Length == 0 ? "Нет предупреждений о физиологическом воздействии." : string.Join(" ", warnings!);
        LoadObservations0217B(List0217(response.Payload, "recentMeasurements"));
        RefreshForecastAndTravel0217();
        Weather0217Status = "Данные обновлены по мировому времени.";
    }

    private void MeasureEnvironment0217B(string measurementType)
    {
        var payload = WeatherScope0217B();
        payload["measurementType"] = measurementType;
        payload["operationId"] = $"player-measure-{measurementType}-{Guid.NewGuid():N}";
        var response = _api.WorldPlayerMeasureEnvironment(payload);
        Weather0217Status = response.Message;
        if (response.Status == ResponseStatus.Ok) RefreshWeatherTravel0217();
    }

    private void EstimateDistance0217B()
    {
        var payload = WeatherScope0217B();
        payload["targetReference"] = "Ориентир у Северных ворот";
        payload["operationId"] = $"player-distance-{Guid.NewGuid():N}";
        var response = _api.WorldPlayerEstimateDistance(payload);
        Weather0217Status = response.Message;
        if (response.Status == ResponseStatus.Ok) RefreshWeatherTravel0217();
    }

    private void LoadObservations0217B(IEnumerable<object> rawItems)
    {
        WeatherObservations0217B.Clear();
        foreach (var item in rawItems.Select(Map0217))
        {
            var type = Text0217(item, "measurementType");
            WeatherObservations0217B.Add(new PlayerObservationRow0217B
            {
                Title = type switch { "temperature" => "Температура", "wind_speed" => "Скорость ветра", "wind_direction" => "Направление ветра", "distance" => "Расстояние", _ => "Наблюдение" },
                Value = Text0217(item, "text"), Source = $"Источник: {Text0217(item, "sourceName")}",
                Freshness = Bool0217(item, "isOutdated") ? "Устарело" : "Недавнее измерение"
            });
        }
    }

    private void RefreshForecastAndTravel0217()
    {
        var forecastResponse = _api.WorldPlayerForecastGet(WeatherScope0217B());
        var forecast = forecastResponse.Status == ResponseStatus.Ok ? List0217(forecastResponse.Payload, "items").Select(Map0217).FirstOrDefault() : null;
        if (forecast != null && Bool0217(forecast, "hasForecast"))
        {
            Weather0217Forecast = Text0217(forecast, "summary");
            Weather0217ForecastReliability = $"Надёжность: {NormalizedRatioFormatter.Format(Number0217(forecast, "reliability"))}  •  {Text0217(forecast, "scopeLabel")}";
        }
        var travelResponse = _api.WorldPlayerTravelGet(new Dictionary<string, object> { ["campaignId"] = "northern-path-0217" });
        WeatherTravelSegments0217.Clear();
        var travel = travelResponse.Status == ResponseStatus.Ok ? List0217(travelResponse.Payload, "items").Select(Map0217).FirstOrDefault() : null;
        if (travel == null) return;
        Weather0217TravelTitle = $"{Text0217(travel, "origin")} — {Text0217(travel, "destination")}";
        Weather0217TravelProgress = $"{StatusLabel0217(Text0217(travel, "status"))}  •  {Text0217(travel, "modeName")}";
        Weather0217TravelEta = $"Оценка оставшегося времени: {Minutes0217(Number0217(travel, "etaMinMinutes"))}–{Minutes0217(Number0217(travel, "etaMaxMinutes"))}";
        foreach (var item in List0217(travel, "segments").Select(Map0217)) WeatherTravelSegments0217.Add(new PlayerTravelSegmentRow0217 { Route = $"{Text0217(item, "from")} → {Text0217(item, "to")}", Detail = $"{Text0217(item, "distanceKm")} км  •  {Text0217(item, "terrain")}", Status = Bool0217(item, "isCompleted") ? "Пройден" : "Впереди" });
    }

    private static Dictionary<string, object> Map0217(object? value)
    {
        if (value is Dictionary<string, object> map) return map;
        if (value is IDictionary dictionary) return dictionary.Keys.Cast<object>().Where(x => x != null).ToDictionary(x => Convert.ToString(x) ?? string.Empty, x => dictionary[x] ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
    private static List<object> List0217(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? List0217(value) : new List<object>();
    private static List<object> List0217(object? value) => value is IEnumerable enumerable && value is not string ? enumerable.Cast<object>().ToList() : new List<object>();
    private static string Text0217(Dictionary<string, object> map, string key, string fallback = "") => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? fallback : fallback;
    private static decimal Number0217(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? NormalizedRatioFormatter.ToDecimal(value) : 0m;
    private static bool Bool0217(Dictionary<string, object> map, string key) => bool.TryParse(Text0217(map, key), out var value) && value;
    private static string Minutes0217(decimal minutes) => minutes >= 60 ? $"{minutes / 60m:0.#} ч" : $"{minutes:0} мин";
    private static string StatusLabel0217(string value) => value switch { "prepared" => "Подготовлено", "active" => "В пути", "paused" => "Приостановлено", "arrived" => "Прибыли", "cancelled" => "Отменено", _ => "Планируется" };
}
