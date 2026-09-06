using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminEnvironmentImpactRow0217B
{
    public string ActorName { get; set; } = string.Empty;
    public string Tolerance { get; set; } = string.Empty;
    public string ThermalState { get; set; } = string.Empty;
    public string ExposureRateDisplay { get; set; } = string.Empty;
    public string Protection { get; set; } = string.Empty;
    public string AccessibleName => $"{ActorName} — {ThermalState} — воздействие {ExposureRateDisplay}";
}

public partial class AdminMainViewModel
{
    private string _adminWeather0217BMeasurementPreview = "Предпросмотр измерений ещё не рассчитан.";
    private string _adminWeather0217BWindVector = "Вектор ветра ещё не рассчитан.";
    public ObservableCollection<AdminEnvironmentImpactRow0217B> AdminEnvironmentImpactRows0217B { get; } = new();
    public string AdminWeather0217BMeasurementPreview { get => _adminWeather0217BMeasurementPreview; private set { _adminWeather0217BMeasurementPreview = value; Notify(); } }
    public string AdminWeather0217BWindVector { get => _adminWeather0217BWindVector; private set { _adminWeather0217BWindVector = value; Notify(); } }

    private void RefreshAdminEnvironmentAssessment0217B()
    {
        var preview = _api.WorldAdminMeasurementPreview(AdminWeatherScope0217());
        if (preview.Status == Nri.Shared.Contracts.ResponseStatus.Ok)
        {
            AdminWeather0217BMeasurementPreview = string.Join(Environment.NewLine, new[]
            {
                $"Термометр: {AdminWeatherText0217(preview.Payload, "thermometer")}",
                $"Анемометр: {AdminWeatherText0217(preview.Payload, "anemometer")}",
                $"Флюгер: {AdminWeatherText0217(preview.Payload, "vane")}",
                $"Расстояние: {AdminWeatherText0217(preview.Payload, "distanceOrdinary")}; опытный наблюдатель — {AdminWeatherText0217(preview.Payload, "distanceSkilled")}."
            });
        }
        var weatherResponse = _api.WorldAdminWeatherGet(AdminWeatherScope0217());
        var weather = weatherResponse.Status == Nri.Shared.Contracts.ResponseStatus.Ok ? AdminWeatherMap0217(weatherResponse.Payload.TryGetValue("weather", out var raw) ? raw : null) : new Dictionary<string, object>();
        var vector = AdminWeatherMap0217(weather.TryGetValue("windVector", out var rawVector) ? rawVector : null);
        AdminWeather0217BWindVector = vector.Count == 0
            ? "Вектор ветра недоступен."
            : $"Ветер {AdminWeatherNumber0217(vector, "speedMps").ToString("0.#", CultureInfo.CurrentCulture)} м/с, с {AdminWeatherText0217(vector, "cardinalDirectionLabel")} ({AdminWeatherNumber0217(vector, "directionFromDegrees").ToString("0.#", CultureInfo.CurrentCulture)}°). Поток направлен на {AdminWeatherNumber0217(vector, "flowDirectionDegrees").ToString("0.#", CultureInfo.CurrentCulture)}°. Компоненты: восток {AdminWeatherNumber0217(vector, "vectorEastMps").ToString("0.##", CultureInfo.CurrentCulture)} м/с, север {AdminWeatherNumber0217(vector, "vectorNorthMps").ToString("0.##", CultureInfo.CurrentCulture)} м/с.";
        var party = _api.WorldAdminEnvironmentImpactPartyGet(AdminWeatherScope0217());
        AdminEnvironmentImpactRows0217B.Clear();
        if (party.Status != Nri.Shared.Contracts.ResponseStatus.Ok) return;
        foreach (var row in AdminWeatherList0217(party.Payload, "items").Select(AdminWeatherMap0217))
            AdminEnvironmentImpactRows0217B.Add(new AdminEnvironmentImpactRow0217B
            {
                ActorName = AdminWeatherText0217(row, "actorName"), Tolerance = AdminWeatherText0217(row, "tolerance"),
                ThermalState = AdminWeatherText0217(row, "thermalState"),
                ExposureRateDisplay = $"{AdminWeatherNumber0217(row, "exposureRate").ToString("0.00", CultureInfo.CurrentCulture)}×",
                Protection = AdminWeatherText0217(row, "protection")
            });
    }
}
