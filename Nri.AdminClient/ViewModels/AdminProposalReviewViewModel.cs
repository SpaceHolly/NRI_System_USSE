using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminProposalItemVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string ProposalType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string Player { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Display => $"{FirstNonEmpty(Title, "Без названия")} | {FirstNonEmpty(TypeLabel, ProposalType)} | {FirstNonEmpty(StatusLabel, Status)}";
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

public sealed class AdminProposalReviewViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private AdminProposalItemVm? _selectedProposal;
    private string _statusFilter = string.Empty;
    private string _statusMessage = "Рабочее место предложений готово к загрузке.";
    private string _errorMessage = string.Empty;
    private string _selectedSummary = "Предложение не выбрано.";
    private string _gmComment = string.Empty;
    private string _playerComment = string.Empty;
    private string _requestedChanges = string.Empty;
    private string _targetEntityType = string.Empty;
    private string _targetEntityId = string.Empty;

    public AdminProposalReviewViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
        LoadSelectedCommand = new RelayCommand(LoadSelected);
        StartReviewCommand = new RelayCommand(() => Review("start"));
        RequestChangesCommand = new RelayCommand(() => Review("changes"));
        ApproveCommand = new RelayCommand(() => Review("approve"));
        RejectCommand = new RelayCommand(() => Review("reject"));
        ValidateCommand = new RelayCommand(ValidateSelected);
        ArchiveCommand = new RelayCommand(ArchiveSelected);
        ConvertToResearchCommand = new RelayCommand(() => ConvertSelectedProposal("research"));
        ConvertToCraftingCommand = new RelayCommand(() => ConvertSelectedProposal("crafting"));
        ConvertToEngineeringCommand = new RelayCommand(() => ConvertSelectedProposal("engineering"));
        ConvertToFactoryOrderCommand = new RelayCommand(() => ConvertSelectedProposal("factory_order"));
        ConvertToManufacturingCommand = new RelayCommand(() => ConvertSelectedProposal("manufacturing"));
        ConvertToLegalCheckCommand = new RelayCommand(() => ConvertSelectedProposal("legal_check"));
        ConvertToLicenseApplicationCommand = new RelayCommand(() => ConvertSelectedProposal("license_application"));
        ConvertToDevelopmentCommand = new RelayCommand(() => ConvertSelectedProposal("development"));
        ConvertToGenericProjectCommand = new RelayCommand(() => ConvertSelectedProposal("generic"));
        LinkExistingCommand = new RelayCommand(LinkExisting);
    }

    public ObservableCollection<AdminProposalItemVm> Proposals { get; } = new();
    public ObservableCollection<string> StructuredFields { get; } = new();
    public ObservableCollection<string> ValidationRows { get; } = new();
    public ObservableCollection<string> ReviewRows { get; } = new();
    public string[] StatusFilters { get; } = { "", "submitted", "in_gm_review", "changes_requested", "approved", "rejected", "converted", "archived" };

    public AdminProposalItemVm? SelectedProposal
    {
        get => _selectedProposal;
        set { if (_selectedProposal != value) { _selectedProposal = value; Notify(); } }
    }

    public string StatusFilter { get => _statusFilter; set { if (_statusFilter != value) { _statusFilter = value ?? string.Empty; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value ?? string.Empty; Notify(); } }
    public string ErrorMessage { get => _errorMessage; private set { _errorMessage = value ?? string.Empty; Notify(); } }
    public string SelectedSummary { get => _selectedSummary; private set { _selectedSummary = value ?? string.Empty; Notify(); } }
    public string GMComment { get => _gmComment; set { if (_gmComment != value) { _gmComment = value ?? string.Empty; Notify(); } } }
    public string PlayerComment { get => _playerComment; set { if (_playerComment != value) { _playerComment = value ?? string.Empty; Notify(); } } }
    public string RequestedChanges { get => _requestedChanges; set { if (_requestedChanges != value) { _requestedChanges = value ?? string.Empty; Notify(); } } }
    public string TargetEntityType { get => _targetEntityType; set { if (_targetEntityType != value) { _targetEntityType = value ?? string.Empty; Notify(); } } }
    public string TargetEntityId { get => _targetEntityId; set { if (_targetEntityId != value) { _targetEntityId = value ?? string.Empty; Notify(); } } }

    public ICommand RefreshCommand { get; }
    public ICommand LoadSelectedCommand { get; }
    public ICommand StartReviewCommand { get; }
    public ICommand RequestChangesCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand RejectCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand ConvertToResearchCommand { get; }
    public ICommand ConvertToCraftingCommand { get; }
    public ICommand ConvertToEngineeringCommand { get; }
    public ICommand ConvertToFactoryOrderCommand { get; }
    public ICommand ConvertToManufacturingCommand { get; }
    public ICommand ConvertToLegalCheckCommand { get; }
    public ICommand ConvertToLicenseApplicationCommand { get; }
    public ICommand ConvertToDevelopmentCommand { get; }
    public ICommand ConvertToGenericProjectCommand { get; }
    public ICommand LinkExistingCommand { get; }

    public void Refresh()
    {
        Run(() =>
        {
            Proposals.Clear();
            var payload = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(StatusFilter)) payload["status"] = StatusFilter;
            var response = _api.ProposalAdminList(payload);
            if (!IsOk(response))
            {
                StatusMessage = DisabledOrError(response, "Проверка предложений выключена флагами функций.");
                return;
            }

            foreach (var item in GetItems(response))
                Proposals.Add(ToItem(AsMap(item)));

            StatusMessage = $"Предложений: {Proposals.Count}.";
        });
    }

    private void LoadSelected()
    {
        if (!EnsureSelected()) return;
        Run(() => ApplyDetails(_api.ProposalAdminGet(IdPayload())));
    }

    private void Review(string action)
    {
        if (!EnsureSelected()) return;
        Run(() =>
        {
            var payload = IdPayload();
            payload["playerComment"] = PlayerComment;
            payload["gmComment"] = GMComment;
            payload["requestedChanges"] = RequestedChanges;
            payload["decisionReason"] = FirstNonEmpty(GMComment, RequestedChanges);

            var response = action switch
            {
                "start" => _api.ProposalAdminReviewStart(payload),
                "changes" => _api.ProposalAdminReviewRequestChanges(payload),
                "approve" => _api.ProposalAdminReviewApprove(payload),
                "reject" => _api.ProposalAdminReviewReject(payload),
                _ => _api.ProposalAdminReviewStart(payload)
            };
            ApplyDetails(response);
            Refresh();
        });
    }

    private void ValidateSelected()
    {
        if (!EnsureSelected()) return;
        Run(() => ApplyDetails(_api.ProposalAdminValidationRun(IdPayload())));
    }

    private void ArchiveSelected()
    {
        if (!EnsureSelected()) return;
        Run(() =>
        {
            ApplyDetails(_api.ProposalAdminArchive(IdPayload()));
            Refresh();
        });
    }

    private void ConvertSelectedProposal(string target)
    {
        if (!EnsureSelected()) return;
        Run(() =>
        {
            var payload = IdPayload();
            payload["gmNotes"] = GMComment;
            payload["createPlayerVisibleProject"] = true;
            var response = target switch
            {
                "research" => _api.ProposalAdminConvertToResearch(payload),
                "crafting" => _api.ProposalAdminConvertToCrafting(payload),
                "engineering" => _api.ProposalAdminConvertToEngineering(payload),
                "factory_order" => _api.ProposalAdminConvertToFactoryOrder(payload),
                "manufacturing" => _api.ProposalAdminConvertToManufacturing(payload),
                "legal_check" => _api.ProposalAdminConvertToLegalCheck(payload),
                "license_application" => _api.ProposalAdminConvertToLicenseApplication(payload),
                "development" => _api.ProposalAdminConvertToDevelopmentPurchase(payload),
                _ => _api.ProposalAdminConvertToGenericProject(payload)
            };
            ApplyDetails(response);
            Refresh();
        });
    }

    private void LinkExisting()
    {
        if (!EnsureSelected()) return;
        if (string.IsNullOrWhiteSpace(TargetEntityType) || string.IsNullOrWhiteSpace(TargetEntityId))
        {
            ErrorMessage = "Укажите тип и id существующей сущности.";
            return;
        }

        Run(() =>
        {
            var payload = IdPayload();
            payload["targetEntityType"] = TargetEntityType;
            payload["targetEntityId"] = TargetEntityId;
            ApplyDetails(_api.ProposalAdminLinkExisting(payload));
            Refresh();
        });
    }

    private void ApplyDetails(ResponseEnvelope response)
    {
        if (!IsOk(response))
        {
            ErrorMessage = DisabledOrError(response, "Операция с предложением не выполнена.");
            return;
        }

        var item = AsMap(GetValue(response.Payload, "item"));
        if (item.Count > 0)
        {
            SelectedProposal = ToItem(item);
            SelectedSummary = $"{SelectedProposal.Display}\n{FirstNonEmpty(S(item, "summary"), S(item, "publicSummary"), S(item, "description"))}";
            StructuredFields.Clear();
            foreach (var field in GetList(item, "structuredFields"))
            {
                var map = AsMap(field);
                StructuredFields.Add($"{S(map, "label")}: {S(map, "value")}");
            }
        }

        ValidationRows.Clear();
        var validation = AsMap(GetValue(response.Payload, "validation"));
        AddValidationRows(validation);

        ReviewRows.Clear();
        foreach (var review in GetList(response.Payload, "reviews"))
        {
            var map = AsMap(review);
            ReviewRows.Add($"{S(map, "reviewStatus")} | {FirstNonEmpty(S(map, "playerVisibleComment"), S(map, "requestedChanges"), S(map, "decisionReason"))}");
        }

        var conversion = AsMap(GetValue(response.Payload, "conversion"));
        if (conversion.Count > 0)
            ReviewRows.Add($"Конвертация: {S(conversion, "conversionType")} -> {S(conversion, "targetEntityType")}:{S(conversion, "targetEntityId")}");

        StatusMessage = "Предложение обновлено.";
    }

    private void AddValidationRows(IDictionary<string, object> validation)
    {
        if (validation.Count == 0) return;
        AddRow("Статус", FirstNonEmpty(S(validation, "summary"), S(validation, "status")));
        foreach (var value in GetList(validation, "missingFields")) AddRow("Заполнить", Convert.ToString(value) ?? string.Empty);
        foreach (var value in GetList(validation, "errors")) AddRow("Ошибка", Convert.ToString(value) ?? string.Empty);
        foreach (var value in GetList(validation, "warnings")) AddRow("Предупреждение", Convert.ToString(value) ?? string.Empty);
    }

    private void AddRow(string kind, string text)
    {
        if (!string.IsNullOrWhiteSpace(text)) ValidationRows.Add($"{kind}: {text}");
    }

    private Dictionary<string, object> IdPayload() => new() { ["proposalDraftId"] = SelectedProposal?.Id ?? string.Empty };

    private bool EnsureSelected()
    {
        if (!string.IsNullOrWhiteSpace(SelectedProposal?.Id)) return true;
        ErrorMessage = "Выберите предложение.";
        return false;
    }

    private void Run(Action action)
    {
        try
        {
            ErrorMessage = string.Empty;
            action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static AdminProposalItemVm ToItem(IDictionary<string, object> map)
        => new()
        {
            Id = FirstNonEmpty(S(map, "proposalDraftId"), S(map, "id")),
            Title = S(map, "title"),
            ProposalType = S(map, "proposalType"),
            TypeLabel = S(map, "proposalTypeLabel"),
            Status = S(map, "status"),
            StatusLabel = S(map, "statusLabel"),
            Player = S(map, "createdByDisplayName"),
            Summary = FirstNonEmpty(S(map, "summary"), S(map, "publicSummary"), S(map, "description"))
        };

    private static bool IsOk(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;
    private static string DisabledOrError(ResponseEnvelope response, string fallback) => string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
    private static IEnumerable<object> GetItems(ResponseEnvelope response) => GetList(response.Payload, "items");
    private static object GetValue(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : new Dictionary<string, object>();
    private static List<object> GetList(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? ToList(value) : new List<object>();
    private static List<object> ToList(object value) => value is IEnumerable enumerable && value is not string ? enumerable.Cast<object>().ToList() : new List<object>();
    private static Dictionary<string, object> AsMap(object value)
        => value is Dictionary<string, object> dictionary
            ? dictionary
            : value is IDictionary<string, object> map
                ? new Dictionary<string, object>(map, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    private static string S(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}
