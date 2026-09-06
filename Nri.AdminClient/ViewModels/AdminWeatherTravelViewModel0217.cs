using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.Shared.Contracts;
using Nri.Ui.Wpf;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminWeatherQueueRow0217
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class AdminTravelSegmentRow0217
{
    public string Route { get; set; } = string.Empty;
    public string DistanceAndTerrain { get; set; } = string.Empty;
    public string WeatherAndSpeed { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}

public partial class AdminMainViewModel
{
    private string _adminWeather0217Status = "Откройте рабочее пространство погоды.";
    private string _adminWeather0217Pattern = "Погода не настроена";
    private string _adminWeather0217Context = "Контекст погоды ещё не разрешён.";
    private string _adminWeather0217Scope = "Северная долина";
    private string _adminWeather0217WorldTime = "Мировое время не загружено";
    private string _adminWeather0217TrueState = string.Empty;
    private string _adminWeather0217Transition = string.Empty;
    private string _adminWeather0217Source = string.Empty;
    private string _adminWeather0217PlayerPreview = string.Empty;
    private string _adminWeather0217Outdoor = string.Empty;
    private string _adminWeather0217Sheltered = string.Empty;
    private string _adminWeather0217TravelTitle = "План путешествия не создан";
    private string _adminWeather0217TravelSummary = string.Empty;
    private string _adminWeather0217NextDecision = string.Empty;
    private int _adminWeather0217Revision;
    private string _adminWeather0217TravelId = string.Empty;
    private int _adminWeather0217TravelRevision;
    private AdminWeatherQueueRow0217? _selectedAdminWeatherQueue0217;
    private ICommand? _refreshAdminWeather0217Command;
    private ICommand? _ensureAdminWeatherFixture0217Command;
    private ICommand? _startAdminTravel0217Command;
    private ICommand? _completeAdminTravelSegment0217Command;
    private ICommand? _lockAdminWeather0217Command;
    private ICommand? _unlockAdminWeather0217Command;
    private ICommand? _approveAdminExposure0217Command;

    public ObservableCollection<AdminWeatherQueueRow0217> AdminWeatherQueue0217 { get; } = new();
    public ObservableCollection<AdminTravelSegmentRow0217> AdminWeatherTravelSegments0217 { get; } = new();
    public string AdminWeather0217Status { get => _adminWeather0217Status; private set { _adminWeather0217Status = value; Notify(); } }
    public string AdminWeather0217Pattern { get => _adminWeather0217Pattern; private set { _adminWeather0217Pattern = value; Notify(); } }
    public string AdminWeather0217Context { get => _adminWeather0217Context; private set { _adminWeather0217Context = value; Notify(); } }
    public string AdminWeather0217Scope { get => _adminWeather0217Scope; private set { _adminWeather0217Scope = value; Notify(); } }
    public string AdminWeather0217WorldTime { get => _adminWeather0217WorldTime; private set { _adminWeather0217WorldTime = value; Notify(); } }
    public string AdminWeather0217TrueState { get => _adminWeather0217TrueState; private set { _adminWeather0217TrueState = value; Notify(); } }
    public string AdminWeather0217Transition { get => _adminWeather0217Transition; private set { _adminWeather0217Transition = value; Notify(); } }
    public string AdminWeather0217Source { get => _adminWeather0217Source; private set { _adminWeather0217Source = value; Notify(); } }
    public string AdminWeather0217PlayerPreview { get => _adminWeather0217PlayerPreview; private set { _adminWeather0217PlayerPreview = value; Notify(); } }
    public string AdminWeather0217Outdoor { get => _adminWeather0217Outdoor; private set { _adminWeather0217Outdoor = value; Notify(); } }
    public string AdminWeather0217Sheltered { get => _adminWeather0217Sheltered; private set { _adminWeather0217Sheltered = value; Notify(); } }
    public string AdminWeather0217TravelTitle { get => _adminWeather0217TravelTitle; private set { _adminWeather0217TravelTitle = value; Notify(); } }
    public string AdminWeather0217TravelSummary { get => _adminWeather0217TravelSummary; private set { _adminWeather0217TravelSummary = value; Notify(); } }
    public string AdminWeather0217NextDecision { get => _adminWeather0217NextDecision; private set { _adminWeather0217NextDecision = value; Notify(); } }
    public AdminWeatherQueueRow0217? SelectedAdminWeatherQueue0217 { get => _selectedAdminWeatherQueue0217; set { _selectedAdminWeatherQueue0217 = value; Notify(); } }
    public ICommand RefreshAdminWeather0217Command => _refreshAdminWeather0217Command ??= new RelayCommand(RefreshAdminWeather0217);
    public ICommand EnsureAdminWeatherFixture0217Command => _ensureAdminWeatherFixture0217Command ??= new RelayCommand(EnsureAdminWeatherFixture0217);
    public ICommand StartAdminTravel0217Command => _startAdminTravel0217Command ??= new RelayCommand(StartAdminTravel0217);
    public ICommand CompleteAdminTravelSegment0217Command => _completeAdminTravelSegment0217Command ??= new RelayCommand(CompleteAdminTravelSegment0217);
    public ICommand LockAdminWeather0217Command => _lockAdminWeather0217Command ??= new RelayCommand(() => SetAdminWeatherLock0217(true));
    public ICommand UnlockAdminWeather0217Command => _unlockAdminWeather0217Command ??= new RelayCommand(() => SetAdminWeatherLock0217(false));
    public ICommand ApproveAdminExposure0217Command => _approveAdminExposure0217Command ??= new RelayCommand(ApproveAdminExposure0217);

    private Dictionary<string, object> AdminWeatherScope0217() => new()
    {
        ["campaignId"] = "northern-path-0217",
        ["sceneId"] = "north-road-scene-0217",
        ["regionId"] = "northern-valley-0217"
    };

    private void EnsureAdminWeatherFixture0217()
    {
        AdminWeather0217Status = "Подготовка тестовой среды...";
        var response = _api.WorldAdminWeatherFixtureEnsure(new Dictionary<string, object> { ["campaignId"] = "northern-path-0217" });
        AdminWeather0217Status = response.Status == ResponseStatus.Ok ? "Тестовая среда «Северный путь» готова." : response.Message;
        if (response.Status == ResponseStatus.Ok) RefreshAdminWeather0217();
    }

    private void RefreshAdminWeather0217()
    {
        AdminWeather0217Status = "Обновление...";
        var weatherResponse = _api.WorldAdminWeatherGet(AdminWeatherScope0217());
        if (weatherResponse.Status != ResponseStatus.Ok)
        {
            AdminWeather0217Status = weatherResponse.Message;
            return;
        }
        var weather = AdminWeatherMap0217(weatherResponse.Payload.TryGetValue("weather", out var rawWeather) ? rawWeather : null);
        var observation = AdminWeatherMap0217(weatherResponse.Payload.TryGetValue("observationPreview", out var rawObservation) ? rawObservation : null);
        var context = AdminWeatherMap0217(weatherResponse.Payload.TryGetValue("resolvedContext", out var rawContext) ? rawContext : null);
        if (weather.Count > 0)
        {
            AdminWeather0217Pattern = AdminWeatherText0217(weather, "patternName", "Погода не настроена");
            AdminWeather0217Scope = AdminWeatherText0217(weather, "scopeLabel", "Область мира");
            AdminWeather0217WorldTime = $"Мировое время: {AdminWeatherDuration0217(AdminWeatherNumber0217(weather, "worldSecond") / 60m)}";
            AdminWeather0217TrueState = $"{AdminWeatherText0217(weather, "temperatureC")} °C  •  {AdminWeatherText0217(weather, "precipitation")}  •  ветер {AdminWeatherText0217(weather, "windSpeedMps")} м/с, от {AdminWeatherText0217(weather, "windDirectionFromDegrees")}°  •  видимость {AdminWeatherText0217(weather, "visibilityM")} м  •  {AdminWeatherText0217(weather, "surfaceCondition")}";
            var transitionMinutes = Math.Max(0, (AdminWeatherNumber0217(weather, "scheduledTransitionAtWorldSecond") - AdminWeatherNumber0217(weather, "worldSecond")) / 60m);
            AdminWeather0217Transition = AdminWeatherBool0217(weather, "isLocked") ? "Естественный переход заблокирован мастером." : $"Следующая сверка через {AdminWeatherDuration0217(transitionMinutes)}";
            AdminWeather0217Source = $"Источник: {AdminWeatherSourceLabel0217(AdminWeatherText0217(weather, "sourceType"))}  •  версия {AdminWeatherText0217(weather, "revision")}";
            _adminWeather0217Revision = (int)AdminWeatherNumber0217(weather, "revision");
        }
        if (context.Count > 0)
        {
            AdminWeather0217Context = $"Явный контекст рабочего пространства  •  Кампания: {AdminWeatherText0217(context, "campaignName")}  •  Сцена: {AdminWeatherText0217(context, "sceneName")}  •  Регион: {AdminWeatherText0217(context, "regionName")}  •  Погода разрешена на уровне: {AdminWeatherScopeTypeLabel0217(AdminWeatherText0217(context, "resolvedWeatherScopeType"))} «{AdminWeatherText0217(context, "resolvedWeatherScopeLabel")}»";
        }
        AdminWeather0217PlayerPreview = AdminWeatherText0217(observation, "summary", "Предпросмотр для игрока недоступен.");

        var environmentResponse = _api.WorldAdminEnvironmentGet(AdminWeatherScope0217());
        if (environmentResponse.Status == ResponseStatus.Ok)
        {
            var outdoor = AdminWeatherMap0217(environmentResponse.Payload.TryGetValue("outdoor", out var o) ? o : null);
            var sheltered = AdminWeatherMap0217(environmentResponse.Payload.TryGetValue("sheltered", out var s) ? s : null);
            AdminWeather0217Outdoor = AdminWeatherEnvironmentSummary0217(outdoor, "Снаружи");
            AdminWeather0217Sheltered = AdminWeatherEnvironmentSummary0217(sheltered, "Под каменным навесом");
        }

        var travelResponse = _api.WorldAdminTravelGet(new Dictionary<string, object> { ["campaignId"] = "northern-path-0217" });
        AdminWeatherQueue0217.Clear();
        AdminWeatherTravelSegments0217.Clear();
        if (travelResponse.Status == ResponseStatus.Ok)
        {
            var travel = AdminWeatherList0217(travelResponse.Payload, "items").Select(AdminWeatherMap0217).FirstOrDefault(x => x.Count > 0);
            if (travel != null)
            {
                _adminWeather0217TravelId = AdminWeatherText0217(travel, "travelId");
                _adminWeather0217TravelRevision = (int)AdminWeatherNumber0217(travel, "revision");
                AdminWeather0217TravelTitle = $"{AdminWeatherText0217(travel, "origin")} — {AdminWeatherText0217(travel, "destination")}";
                var members = string.Join(", ", AdminWeatherList0217(travel, "partyMembers").Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)));
                AdminWeather0217TravelSummary = $"Группа: {members}  •  Режим: {AdminWeatherText0217(travel, "modeName")}  •  Итого: {AdminWeatherDuration0217(AdminWeatherNumber0217(travel, "authoritativeDurationMinutes"))}";
                AdminWeather0217NextDecision = $"Следующее решение: участок {(int)AdminWeatherNumber0217(travel, "currentSegmentIndex") + 1} из {AdminWeatherList0217(travel, "segments").Count}. Завершение подтверждает мастер.";
                foreach (var segment in AdminWeatherList0217(travel, "segments").Select(AdminWeatherMap0217))
                {
                    AdminWeatherTravelSegments0217.Add(new AdminTravelSegmentRow0217
                    {
                        Route = $"{AdminWeatherText0217(segment, "from")} → {AdminWeatherText0217(segment, "to")}",
                        DistanceAndTerrain = $"{AdminWeatherText0217(segment, "distanceKm")} км  •  Местность: {AdminWeatherText0217(segment, "terrain")}",
                        WeatherAndSpeed = $"Погода: {AdminWeatherText0217(segment, "weatherPatternName")}  •  Скорость: {AdminWeatherText0217(segment, "effectiveSpeedKmh")} км/ч  •  Темп: {NormalizedRatioFormatter.Format(AdminWeatherNumber0217(segment, "weatherMultiplier"))}",
                        Duration = $"Время: {AdminWeatherDuration0217(AdminWeatherNumber0217(segment, "durationMinutes"))}"
                    });
                }
            }
            foreach (var item in AdminWeatherList0217(travelResponse.Payload, "resolutionSuggestions").Select(AdminWeatherMap0217))
                AdminWeatherQueue0217.Add(new AdminWeatherQueueRow0217 { Id = AdminWeatherText0217(item, "suggestionId"), Title = AdminWeatherText0217(item, "title"), Summary = AdminWeatherText0217(item, "summary") });
            SelectedAdminWeatherQueue0217 = AdminWeatherQueue0217.FirstOrDefault();
        }
        RefreshAdminEnvironmentAssessment0217B();
        AdminWeather0217Status = "Состояние обновлено по мировому времени.";
    }

    private void StartAdminTravel0217()
    {
        if (string.IsNullOrWhiteSpace(_adminWeather0217TravelId)) { AdminWeather0217Status = "Сначала подготовьте маршрут."; return; }
        var response = _api.WorldAdminTravelStart(new Dictionary<string, object> { ["travelId"] = _adminWeather0217TravelId, ["expectedRevision"] = _adminWeather0217TravelRevision });
        AdminWeather0217Status = response.Status == ResponseStatus.Ok ? "Путешествие начато." : response.Message;
        RefreshAdminWeather0217();
    }

    private void CompleteAdminTravelSegment0217()
    {
        if (string.IsNullOrWhiteSpace(_adminWeather0217TravelId)) { AdminWeather0217Status = "Активный маршрут не выбран."; return; }
        var response = _api.WorldAdminTravelSegmentComplete(new Dictionary<string, object> { ["travelId"] = _adminWeather0217TravelId, ["expectedRevision"] = _adminWeather0217TravelRevision, ["operationId"] = $"admin-segment-{_adminWeather0217TravelId}-{_adminWeather0217TravelRevision}" });
        AdminWeather0217Status = response.Status == ResponseStatus.Ok ? response.Message : $"Не удалось завершить участок: {response.Message}";
        RefreshAdminWeather0217();
    }

    private void SetAdminWeatherLock0217(bool locked)
    {
        var payload = AdminWeatherScope0217(); payload["expectedRevision"] = _adminWeather0217Revision; payload["reason"] = locked ? "Погода зафиксирована мастером для текущей сцены." : "Мастер возобновил естественные переходы.";
        var response = locked ? _api.WorldAdminWeatherLock(payload) : _api.WorldAdminWeatherUnlock(payload);
        AdminWeather0217Status = response.Message;
        RefreshAdminWeather0217();
    }

    private void ApproveAdminExposure0217()
    {
        if (SelectedAdminWeatherQueue0217 == null) { AdminWeather0217Status = "Нет выбранного воздействия."; return; }
        var response = _api.WorldAdminExposureApprove(new Dictionary<string, object> { ["suggestionId"] = SelectedAdminWeatherQueue0217.Id, ["operationId"] = $"exposure-{SelectedAdminWeatherQueue0217.Id}" });
        AdminWeather0217Status = response.Message;
        RefreshAdminWeather0217();
    }

    private static Dictionary<string, object> AdminWeatherMap0217(object? value)
    {
        if (value is Dictionary<string, object> map) return map;
        if (value is IDictionary dictionary) return dictionary.Keys.Cast<object>().Where(x => x != null).ToDictionary(x => Convert.ToString(x) ?? string.Empty, x => dictionary[x] ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
    private static List<object> AdminWeatherList0217(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value is IEnumerable enumerable && value is not string ? enumerable.Cast<object>().ToList() : new List<object>();
    private static string AdminWeatherText0217(Dictionary<string, object> map, string key, string fallback = "") => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? fallback : fallback;
    private static decimal AdminWeatherNumber0217(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? NormalizedRatioFormatter.ToDecimal(value) : 0m;
    private static bool AdminWeatherBool0217(Dictionary<string, object> map, string key) => bool.TryParse(AdminWeatherText0217(map, key), out var value) && value;
    private static string AdminWeatherDuration0217(decimal minutes) => minutes >= 60 ? $"{minutes / 60m:0.#} ч" : $"{minutes:0} мин";
    private static string AdminWeatherSourceLabel0217(string value) => value switch { "natural" => "Естественная", "gm_override" => "Решение мастера", "magic" => "Магия", "anomaly" => "Аномалия", "technology" => "Технология", _ => "Особый источник" };
    private static string AdminWeatherTravelStatus0217(string value) => value switch { "prepared" => "Подготовлено", "active" => "В пути", "paused" => "Приостановлено", "arrived" => "Прибыли", "cancelled" => "Отменено", _ => "Планируется" };
    private static string AdminWeatherScopeTypeLabel0217(string value) => value switch { "world" => "мир", "region" => "регион", "location" => "локация", "scene" => "сцена", _ => "область" };
    private static string AdminWeatherEnvironmentSummary0217(Dictionary<string, object> map, string title) => $"{title}: {AdminWeatherText0217(map, "temperatureC")} °C  •  видимость {AdminWeatherText0217(map, "visibilityM")} м  •  темп движения {NormalizedRatioFormatter.Format(AdminWeatherNumber0217(map, "movementMultiplier"))}  •  воздействие среды {NormalizedRatioFormatter.Format(AdminWeatherNumber0217(map, "exposureMultiplier"))}";
}
