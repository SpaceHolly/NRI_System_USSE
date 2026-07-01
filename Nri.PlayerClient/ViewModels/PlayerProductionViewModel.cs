using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerProductionViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private string _campaignId = "default";
    private string _statusMessage = "Производство: здесь видны раскрытые GM мощности, ваши оценки/заказы, производственные проекты и готовые активы.";
    private string _errorMessage = string.Empty;
    private PlayerProductionUiItem? _selectedFacility;
    private PlayerProductionUiItem? _selectedQuote;
    private PlayerProductionUiItem? _selectedOrder;
    private PlayerProductionUiItem? _selectedManufacturingProject;

    public PlayerProductionViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        RefreshCommand = new RelayCommand(RefreshAll);
        RequestQuoteCommand = new RelayCommand(RequestQuote);
        RequestOrderCommand = new RelayCommand(RequestOrder);
        RequestManufacturingProgressCommand = new RelayCommand(() => SubmitManufacturingRequest("Запросить прогресс производства"));
        RequestAcceptanceCommand = new RelayCommand(() => SubmitManufacturingRequest("Запросить приёмку"));
        RequestTransferCommand = new RelayCommand(() => SubmitManufacturingRequest("Запросить передачу техники"));
        AcceptQuoteCommand = new RelayCommand(AcceptQuote);
        RejectQuoteCommand = new RelayCommand(RejectQuote);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<PlayerProductionUiItem> Facilities { get; } = new();
    public ObservableCollection<PlayerProductionUiItem> Quotes { get; } = new();
    public ObservableCollection<PlayerProductionUiItem> Orders { get; } = new();
    public ObservableCollection<PlayerProductionUiItem> ManufacturingProjects { get; } = new();
    public ObservableCollection<PlayerProductionUiItem> Stages { get; } = new();
    public ObservableCollection<PlayerProductionUiItem> Resources { get; } = new();
    public ObservableCollection<PlayerProductionUiItem> Payments { get; } = new();
    public ObservableCollection<PlayerProductionUiItem> Tests { get; } = new();
    public ObservableCollection<PlayerProductionUiItem> Defects { get; } = new();
    public ObservableCollection<PlayerProductionUiItem> Assets { get; } = new();
    public ObservableCollection<string> AuditRows { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand RequestQuoteCommand { get; }
    public ICommand RequestOrderCommand { get; }
    public ICommand RequestManufacturingProgressCommand { get; }
    public ICommand RequestAcceptanceCommand { get; }
    public ICommand RequestTransferCommand { get; }
    public ICommand AcceptQuoteCommand { get; }
    public ICommand RejectQuoteCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string RequestSummary { get; set; } = "Нужна оценка производства";
    public string RequestDetails { get; set; } = string.Empty;
    public string BlueprintId { get; set; } = string.Empty;
    public string PresetId { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); } } }

    public PlayerProductionUiItem? SelectedFacility
    {
        get => _selectedFacility;
        set
        {
            if (_selectedFacility != value)
            {
                _selectedFacility = value;
                if (value != null) FacilityId = value.Id;
                Notify();
                Notify(nameof(FacilityId));
            }
        }
    }

    public PlayerProductionUiItem? SelectedQuote { get => _selectedQuote; set { if (_selectedQuote != value) { _selectedQuote = value; Notify(); } } }
    public PlayerProductionUiItem? SelectedOrder { get => _selectedOrder; set { if (_selectedOrder != value) { _selectedOrder = value; Notify(); } } }
    public PlayerProductionUiItem? SelectedManufacturingProject
    {
        get => _selectedManufacturingProject;
        set
        {
            if (_selectedManufacturingProject != value)
            {
                _selectedManufacturingProject = value;
                Notify();
                LoadManufacturingDetails();
            }
        }
    }

    public void RefreshAll()
    {
        Run("player.production.refresh", () =>
        {
            LoadList(_api.ProductionPlayerFacilityList(BasePayload()), Facilities, "Доступные производственные мощности пока не раскрыты.");
            LoadList(_api.FactoryPlayerQuoteList(BasePayload()), Quotes, "У вас пока нет производственных оценок.");
            LoadList(_api.FactoryPlayerOrderList(BasePayload()), Orders, "У вас пока нет производственных заказов.");
            LoadList(_api.ManufacturingPlayerProjectList(BasePayload()), ManufacturingProjects, "Активного производства пока нет.");
            LoadList(_api.ManufacturingPlayerAssetList(BasePayload()), Assets, "Готовой техники пока нет.");
            LoadManufacturingDetails();
            StatusMessage = "Производственные данные обновлены.";
        });
    }

    private void LoadManufacturingDetails()
    {
        Stages.Clear();
        Resources.Clear();
        Payments.Clear();
        Tests.Clear();
        Defects.Clear();
        if (SelectedManufacturingProject == null || SelectedManufacturingProject.IsPlaceholder) return;
        var response = _api.ManufacturingPlayerProjectGet(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id });
        EnsureOk(response);
        if (!response.Payload.TryGetValue("item", out var raw)) return;
        var item = ToDictionary(raw);
        AddNestedItems(item, "stages", Stages);
        AddNestedItems(item, "resourcePlans", Resources);
        AddNestedItems(item, "payments", Payments);
        AddNestedItems(item, "testResults", Tests);
        AddNestedItems(item, "defects", Defects);
        AddNestedItems(item, "assets", Assets);
    }

    private void RequestQuote()
    {
        Run("player.production.quote.request", () =>
        {
            var response = _api.FactoryPlayerQuoteRequest(RequestPayload());
            EnsureOk(response);
            AuditRows.Insert(0, $"{DateTime.Now:HH:mm:ss} Заявка на оценку отправлена GM.");
            StatusMessage = "Заявка на оценку отправлена GM.";
        });
    }

    private void RequestOrder()
    {
        Run("player.production.order.request", () =>
        {
            var response = _api.FactoryPlayerOrderRequest(RequestPayload());
            EnsureOk(response);
            AuditRows.Insert(0, $"{DateTime.Now:HH:mm:ss} Заявка на заказ отправлена GM.");
            StatusMessage = "Заявка на производственный заказ отправлена GM.";
        });
    }

    private void SubmitManufacturingRequest(string title)
    {
        Run("player.production.manufacturing.request", () =>
        {
            var payload = RequestPayload();
            payload["summary"] = title;
            if (SelectedManufacturingProject != null && !SelectedManufacturingProject.IsPlaceholder) payload["manufacturingProjectId"] = SelectedManufacturingProject.Id;
            var response = _api.ManufacturingPlayerContributionSubmit(payload);
            EnsureOk(response);
            AuditRows.Insert(0, $"{DateTime.Now:HH:mm:ss} {title}: отправлено GM.");
            StatusMessage = $"{title}: отправлено GM.";
        });
    }

    private void AcceptQuote()
    {
        if (SelectedQuote == null || SelectedQuote.IsPlaceholder) { ErrorMessage = "Выберите предложенную оценку."; return; }
        Run("player.production.quote.accept", () =>
        {
            var response = _api.FactoryPlayerQuoteAccept(new Dictionary<string, object>(BasePayload()) { ["quoteId"] = SelectedQuote.Id });
            EnsureOk(response);
            AuditRows.Insert(0, $"{DateTime.Now:HH:mm:ss} Оценка принята.");
            RefreshAll();
        });
    }

    private void RejectQuote()
    {
        if (SelectedQuote == null || SelectedQuote.IsPlaceholder) { ErrorMessage = "Выберите оценку."; return; }
        Run("player.production.quote.reject", () =>
        {
            var response = _api.FactoryPlayerQuoteReject(new Dictionary<string, object>(BasePayload()) { ["quoteId"] = SelectedQuote.Id });
            EnsureOk(response);
            AuditRows.Insert(0, $"{DateTime.Now:HH:mm:ss} Оценка отклонена.");
            RefreshAll();
        });
    }

    private Dictionary<string, object> RequestPayload()
    {
        var payload = BasePayload();
        payload["facilityId"] = FirstNonEmpty(FacilityId, SelectedFacility?.Id);
        payload["blueprintId"] = BlueprintId;
        payload["presetId"] = PresetId;
        payload["summary"] = RequestSummary;
        payload["description"] = RequestDetails;
        payload["comment"] = RequestDetails;
        return payload;
    }

    private Dictionary<string, object> BasePayload()
    {
        var payload = new Dictionary<string, object> { ["campaignId"] = CampaignId };
        var characterId = _activeCharacterIdAccessor();
        if (!string.IsNullOrWhiteSpace(characterId)) payload["characterId"] = characterId;
        return payload;
    }

    private void LoadList(ResponseEnvelope response, ObservableCollection<PlayerProductionUiItem> target, string emptyText)
    {
        target.Clear();
        EnsureOk(response);
        foreach (var item in Items(response)) target.Add(PlayerProductionUiItem.From(item));
        if (target.Count == 0) target.Add(new PlayerProductionUiItem { Name = emptyText, Summary = emptyText, Secondary = "—", IsPlaceholder = true });
    }

    private void Run(string scope, Action action)
    {
        try
        {
            ErrorMessage = string.Empty;
            ClientLogService.Instance.Info(scope + ".start");
            action();
            ClientLogService.Instance.Info(scope + ".done");
        }
        catch (Exception ex)
        {
            ErrorMessage = PlayerFacingMessage(ex.Message, "Производственный раздел пока недоступен.");
            StatusMessage = "Производственный раздел пока недоступен.";
            ClientLogService.Instance.Error(scope + ".error " + ex.Message);
        }
    }

    private static void EnsureOk(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(PlayerFacingMessage(response.Message, "Производственный раздел пока недоступен."));
    }

    private static string PlayerFacingMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        if (message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0)
            return fallback;
        return message;
    }

    private static void AddNestedItems(IDictionary<string, object> parent, string key, ObservableCollection<PlayerProductionUiItem> target)
    {
        target.Clear();
        if (!parent.TryGetValue(key, out var raw) || raw == null) return;
        if (raw is IEnumerable enumerable && raw is not string)
        {
            foreach (var item in enumerable)
            {
                var map = ToDictionary(item);
                if (map.Count > 0) target.Add(PlayerProductionUiItem.From(map));
            }
        }
    }

    private static IEnumerable<IDictionary<string, object>> Items(ResponseEnvelope response)
    {
        if (!response.Payload.TryGetValue("items", out var raw) || raw == null) yield break;
        if (raw is IEnumerable enumerable && raw is not string)
        {
            foreach (var item in enumerable)
            {
                var map = ToDictionary(item);
                if (map.Count > 0) yield return map;
            }
        }
    }

    private static Dictionary<string, object> ToDictionary(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }
        return new Dictionary<string, object>();
    }

    private static string FirstNonEmpty(params string?[] values) => values.Select(x => (x ?? string.Empty).Trim()).FirstOrDefault(x => x.Length > 0) ?? string.Empty;
}

public sealed class PlayerProductionUiItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Secondary { get; set; } = string.Empty;
    public bool IsPlaceholder { get; set; }

    public static PlayerProductionUiItem From(IDictionary<string, object> map)
    {
        var id = Get(map, "id");
        var name = First(Get(map, "name"), Get(map, "displayName"), Get(map, "resourceName"), id, "Без названия");
        var status = First(Get(map, "status"), Get(map, "manufacturingStatus"), Get(map, "operationalStatus"), "—");
        var type = First(Get(map, "facilityCategory"), Get(map, "sourceType"), Get(map, "productionDomain"), Get(map, "manufacturingType"), Get(map, "stageType"), Get(map, "assetType"), "—");
        var cost = First(Get(map, "estimatedCost"), Get(map, "estimatedTotalCost"), Get(map, "amount"));
        var progress = Get(map, "progressPercent");
        return new PlayerProductionUiItem
        {
            Id = id,
            Name = name,
            Status = status,
            Type = type,
            Summary = !string.IsNullOrWhiteSpace(progress)
                ? $"{name} · {status} · {progress}%"
                : string.IsNullOrWhiteSpace(cost) ? $"{name} · {type} · {status}" : $"{name} · {status} · {cost} МО",
            Secondary = First(Get(map, "publicTermsSummary"), Get(map, "resourceRequirementSummary"), Get(map, "paymentPlanSummary"), Get(map, "defectSummary"), Get(map, "publicSummary"), Get(map, "publicStatusSummary"), Get(map, "description"), "—")
        };
    }

    private static string Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}
