using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed partial class PlayerProductionViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private readonly Func<string> _activeCampaignIdAccessor;
    private string _campaignId = "default";
    private string _statusMessage = "Производство: здесь видны раскрытые GM мощности, ваши оценки/заказы, производственные проекты и готовые активы.";
    private string _errorMessage = string.Empty;
    private PlayerProductionUiItem? _selectedFacility;
    private PlayerProductionUiItem? _selectedQuote;
    private PlayerProductionUiItem? _selectedOrder;
    private PlayerProductionUiItem? _selectedManufacturingProject;
    private PlayerProductionReferenceItem? _selectedBlueprint;
    private PlayerProductionReferenceItem? _selectedPreset;
    private string _requestSummary = "Нужна оценка производства";
    private string _requestDetails = string.Empty;

    public PlayerProductionViewModel(
        CommandApi api,
        Func<string> activeCharacterIdAccessor,
        Func<string>? activeCampaignIdAccessor = null)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        _activeCampaignIdAccessor = activeCampaignIdAccessor ?? (() => string.Empty);
        RefreshCommand = new RelayCommand(RefreshAll);
        RequestQuoteCommand = new RelayCommand(RequestQuote);
        RequestOrderCommand = new RelayCommand(RequestOrder);
        RequestManufacturingProgressCommand = new RelayCommand(() => SubmitManufacturingRequest("Запросить прогресс производства"));
        RequestAcceptanceCommand = new RelayCommand(() => SubmitManufacturingRequest("Запросить приёмку"));
        RequestTransferCommand = new RelayCommand(() => SubmitManufacturingRequest("Запросить передачу техники"));
        AcceptQuoteCommand = new RelayCommand(AcceptQuote);
        RejectQuoteCommand = new RelayCommand(RejectQuote);
        PreviewRequestCommand = new RelayCommand(PreviewRequest);
        ClearRequestSelectionCommand = new RelayCommand(ClearRequestSelection);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
        InitializeCraftRuntime0191();
        InitializeLimitedProduction0196();
        InitializeAssetConstruction0197();
        InitializeAssetMaintenance0198();
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
    public ObservableCollection<PlayerProductionReferenceItem> AvailableBlueprints { get; } = new();
    public ObservableCollection<PlayerProductionReferenceItem> AvailablePresets { get; } = new();
    public ObservableCollection<string> AuditRows { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand RequestQuoteCommand { get; }
    public ICommand RequestOrderCommand { get; }
    public ICommand RequestManufacturingProgressCommand { get; }
    public ICommand RequestAcceptanceCommand { get; }
    public ICommand RequestTransferCommand { get; }
    public ICommand AcceptQuoteCommand { get; }
    public ICommand RejectQuoteCommand { get; }
    public ICommand PreviewRequestCommand { get; }
    public ICommand ClearRequestSelectionCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId
    {
        get
        {
            var activeCampaignId = _activeCampaignIdAccessor();
            return string.IsNullOrWhiteSpace(activeCampaignId) ? _campaignId : activeCampaignId;
        }
        set
        {
            if (_campaignId != value)
            {
                _campaignId = value;
                Notify();
            }
        }
    }
    public string RequestSummary
    {
        get => _requestSummary;
        set
        {
            if (_requestSummary != value)
            {
                _requestSummary = value;
                Notify();
                Notify(nameof(RequestDraftSummary));
            }
        }
    }
    public string RequestDetails
    {
        get => _requestDetails;
        set
        {
            if (_requestDetails != value)
            {
                _requestDetails = value;
                Notify();
            }
        }
    }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); } } }
    public string RequestDraftSummary
    {
        get
        {
            var facility = SelectedFacility?.IsPlaceholder == false ? SelectedFacility.Name : "не выбрана";
            var blueprint = SelectedBlueprint?.IsPlaceholder == false ? SelectedBlueprint.Name : "не выбран";
            var preset = SelectedPreset?.IsPlaceholder == false ? SelectedPreset.Name : "не выбран";
            var request = string.IsNullOrWhiteSpace(RequestSummary) ? "не указано" : RequestSummary.Trim();
            return $"Мощность: {facility}\nЧертёж: {blueprint}\nГотовый вариант: {preset}\nЗапрос: {request}";
        }
    }

    public PlayerProductionUiItem? SelectedFacility
    {
        get => _selectedFacility;
        set
        {
            if (_selectedFacility != value)
            {
                _selectedFacility = value;
                Notify();
                Notify(nameof(RequestDraftSummary));
            }
        }
    }
    public PlayerProductionReferenceItem? SelectedBlueprint
    {
        get => _selectedBlueprint;
        set
        {
            if (_selectedBlueprint != value)
            {
                _selectedBlueprint = value;
                Notify();
                Notify(nameof(RequestDraftSummary));
            }
        }
    }
    public PlayerProductionReferenceItem? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (_selectedPreset != value)
            {
                _selectedPreset = value;
                Notify();
                Notify(nameof(RequestDraftSummary));
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
        RefreshCraftRuntime0191(silent: true);
        RefreshLimitedProduction0196(silent: true);
        RefreshAssetConstruction0197(silent: true);
        RefreshAssetMaintenance0198(silent: true);
        Run("player.production.refresh", () =>
        {
            LoadList(_api.ProductionPlayerFacilityList(BasePayload()), Facilities, "Доступные производственные мощности пока не раскрыты.");
            LoadList(_api.FactoryPlayerQuoteList(BasePayload()), Quotes, "У вас пока нет производственных оценок.");
            LoadList(_api.FactoryPlayerOrderList(BasePayload()), Orders, "У вас пока нет производственных заказов.");
            LoadList(_api.ManufacturingPlayerProjectList(BasePayload()), ManufacturingProjects, "Активного производства пока нет.");
            LoadList(_api.ManufacturingPlayerAssetList(BasePayload()), Assets, "Готовой техники пока нет.");
            LoadReferences(_api.EngineeringPlayerBlueprintList(BasePayload()), AvailableBlueprints, "blueprintId", "Доступные чертежи пока не раскрыты.");
            LoadReferences(_api.EngineeringPlayerPresetList(BasePayload()), AvailablePresets, "presetId", "Доступные готовые варианты пока не раскрыты.");
            SelectedFacility = Facilities.FirstOrDefault(item => !item.IsPlaceholder);
            SelectedBlueprint = AvailableBlueprints.FirstOrDefault(item => !item.IsPlaceholder);
            SelectedPreset = AvailablePresets.FirstOrDefault(item => !item.IsPlaceholder);
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
        if (!CanSubmitRequest()) return;
        if (!ConfirmRequest("Отправить запрос на оценку GM?")) return;
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
        if (!CanSubmitRequest()) return;
        if (!ConfirmRequest("Отправить запрос на производственный заказ GM?")) return;
        Run("player.production.order.request", () =>
        {
            var response = _api.FactoryPlayerOrderRequest(RequestPayload());
            EnsureOk(response);
            AuditRows.Insert(0, $"{DateTime.Now:HH:mm:ss} Заявка на заказ отправлена GM.");
            StatusMessage = "Заявка на производственный заказ отправлена GM.";
        });
    }

    private void PreviewRequest()
    {
        if (!CanSubmitRequest()) return;
        ErrorMessage = string.Empty;
        StatusMessage = "Заявка проверена. Можно запросить оценку или заказ.";
    }

    private void ClearRequestSelection()
    {
        SelectedFacility = null;
        SelectedBlueprint = null;
        SelectedPreset = null;
        ErrorMessage = string.Empty;
        StatusMessage = "Выбор мощности, чертежа и готового варианта очищен.";
    }

    private bool CanSubmitRequest()
    {
        ErrorMessage = string.Empty;
        if (SelectedFacility == null || SelectedFacility.IsPlaceholder)
        {
            ErrorMessage = "Выберите доступную производственную мощность.";
            StatusMessage = "Заявка требует выбора мощности.";
            return false;
        }
        if ((SelectedBlueprint == null || SelectedBlueprint.IsPlaceholder) &&
            (SelectedPreset == null || SelectedPreset.IsPlaceholder))
        {
            ErrorMessage = "Выберите чертёж или готовый вариант.";
            StatusMessage = "Заявка требует выбора чертежа или готового варианта.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(RequestSummary))
        {
            ErrorMessage = "Кратко укажите, что требуется.";
            StatusMessage = "Заявка требует краткого описания.";
            return false;
        }
        return true;
    }

    private bool ConfirmRequest(string prompt)
    {
        var result = MessageBox.Show(
            $"{prompt}\n\n{RequestDraftSummary}",
            "Подтверждение производственной заявки",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes) return true;
        StatusMessage = "Отправка производственной заявки отменена.";
        return false;
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
        payload["facilityId"] = SelectedFacility?.IsPlaceholder == false ? SelectedFacility.Id : string.Empty;
        payload["blueprintId"] = SelectedBlueprint?.IsPlaceholder == false ? SelectedBlueprint.Id : string.Empty;
        payload["presetId"] = SelectedPreset?.IsPlaceholder == false ? SelectedPreset.Id : string.Empty;
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

    private void LoadReferences(ResponseEnvelope response, ObservableCollection<PlayerProductionReferenceItem> target, string idKey, string emptyText)
    {
        target.Clear();
        EnsureOk(response);
        foreach (var item in Items(response))
        {
            target.Add(new PlayerProductionReferenceItem
            {
                Id = FirstNonEmpty(Read(item, idKey), Read(item, "id")),
                Name = FirstNonEmpty(Read(item, "name"), Read(item, "displayName"), "Без названия"),
                Summary = FirstNonEmpty(Read(item, "publicSummary"), Read(item, "description"), Read(item, "roleSummary"), "Доступно для заявки."),
                Type = PlayerProductionUiItem.ReadableType(FirstNonEmpty(Read(item, "sourceType"), Read(item, "blueprintType"), Read(item, "presetType"), "production")),
                Availability = PlayerProductionUiItem.ReadableStatus(FirstNonEmpty(Read(item, "status"), Read(item, "availabilityStatus"), "available"))
            });
        }
        if (target.Count == 0)
            target.Add(new PlayerProductionReferenceItem { Name = emptyText, Summary = "Обратитесь к GM.", IsPlaceholder = true });
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
    private static string Read(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
}

public sealed class PlayerProductionReferenceItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public string DisplayMeta => string.Join(" · ", new[] { Type, Availability }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public bool IsPlaceholder { get; set; }
    public override string ToString() => Name;
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
        var name = First(Get(map, "name"), Get(map, "displayName"), Get(map, "resourceName"), "Без названия");
        var status = ReadableStatus(First(Get(map, "status"), Get(map, "manufacturingStatus"), Get(map, "operationalStatus"), "—"));
        var type = ReadableType(First(Get(map, "facilityCategory"), Get(map, "sourceType"), Get(map, "productionDomain"), Get(map, "manufacturingType"), Get(map, "stageType"), Get(map, "assetType"), "—"));
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
    internal static string ReadableStatus(string value)
    {
        if ((value ?? string.Empty).Any(ch => ch >= '\u0400' && ch <= '\u04FF')) return value;
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "draft" => "Черновик",
            "requested" or "submitted" => "Запрошено",
            "quoted" => "Оценено",
            "accepted" or "approved" => "Принято",
            "rejected" => "Отклонено",
            "active" or "in_progress" => "В работе",
            "completed" => "Завершено",
            "cancelled" => "Отменено",
            "available" => "Доступно",
            "unavailable" => "Недоступно",
            _ => "Состояние не указано"
        };
    }

    internal static string ReadableType(string value)
    {
        if ((value ?? string.Empty).Any(ch => ch >= '\u0400' && ch <= '\u04FF')) return value;
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "factory" => "Фабрика",
            "workshop" => "Мастерская",
            "shipyard" => "Верфь",
            "vehicle" => "Техника",
            "equipment" => "Снаряжение",
            "component" => "Компонент",
            "blueprint" => "Чертёж",
            "preset" => "Готовый вариант",
            _ => "Производство"
        };
    }
}
