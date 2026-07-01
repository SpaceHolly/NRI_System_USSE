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

public sealed class PlayerEngineeringViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private string _campaignId = "default";
    private string _statusMessage = "Инженерные проекты создают предложения и чертежи. Готовая техника здесь не выдаётся.";
    private string _errorMessage = string.Empty;
    private EngineeringDraftPlayerItem? _selectedDraft;

    public PlayerEngineeringViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        RefreshCommand = new RelayCommand(RefreshAll);
        CreateDraftCommand = new RelayCommand(CreateDraft);
        ValidateDraftCommand = new RelayCommand(ValidateDraft);
        SubmitDraftCommand = new RelayCommand(SubmitDraft);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<string> Platforms { get; } = new();
    public ObservableCollection<string> Modules { get; } = new();
    public ObservableCollection<string> Presets { get; } = new();
    public ObservableCollection<EngineeringDraftPlayerItem> Drafts { get; } = new();
    public ObservableCollection<string> Projects { get; } = new();
    public ObservableCollection<string> Blueprints { get; } = new();
    public ObservableCollection<string> ValidationRows { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand CreateDraftCommand { get; }
    public ICommand ValidateDraftCommand { get; }
    public ICommand SubmitDraftCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); } } }
    public EngineeringDraftPlayerItem? SelectedDraft { get => _selectedDraft; set { if (_selectedDraft != value) { _selectedDraft = value; Notify(); } } }

    public string DraftName { get; set; } = "Предложение конструкции";
    public string DraftPlatformId { get; set; } = string.Empty;
    public string DraftModuleIds { get; set; } = string.Empty;
    public string DraftRole { get; set; } = string.Empty;
    public string DraftNotes { get; set; } = string.Empty;

    public void RefreshAll()
    {
        Run("player.engineering.refresh", () =>
        {
            LoadLines(_api.EngineeringPlayerPlatformList(BasePayload()), Platforms, m => $"{Str(Get(m, "name"), "Платформа")} • {Str(Get(m, "platformKind"))} • {Str(Get(m, "sizeClassId"))}");
            LoadLines(_api.EngineeringPlayerModuleList(BasePayload()), Modules, m => $"{Str(Get(m, "name"), "Модуль")} • {Str(Get(m, "moduleCategory"))} • {Str(Get(m, "diceExpression"))}");
            LoadLines(_api.EngineeringPlayerPresetList(BasePayload()), Presets, m => $"{Str(Get(m, "name"), "Пресет")} • {Str(Get(m, "roleSummary"))}");
            LoadDrafts();
            LoadLines(_api.EngineeringPlayerProjectList(BasePayload()), Projects, m => $"{Str(Get(m, "name"), "Проект")} • {Str(Get(m, "status"))} • {Str(Get(m, "progressPercent"), "0")}%");
            LoadLines(_api.EngineeringPlayerBlueprintList(BasePayload()), Blueprints, m => $"{Str(Get(m, "name"), "Чертёж")} • {Str(Get(m, "status"))}");
            StatusMessage = "Инженерные данные обновлены.";
        });
    }

    private void LoadDrafts()
    {
        Drafts.Clear();
        var response = _api.EngineeringPlayerDraftList(BasePayload());
        EnsureOk(response);
        foreach (var map in Items(response)) Drafts.Add(EngineeringDraftPlayerItem.From(map));
    }

    private void CreateDraft()
    {
        Run("player.engineering.draft.create", () =>
        {
            var response = _api.EngineeringPlayerDraftCreate(new Dictionary<string, object>(BasePayload())
            {
                { "name", DraftName },
                { "platformId", DraftPlatformId },
                { "moduleIds", ModuleIds() },
                { "intendedRole", DraftRole },
                { "playerNotes", DraftNotes }
            });
            EnsureOk(response);
            LoadDrafts();
            StatusMessage = "Черновик инженерного предложения создан.";
        });
    }

    private void ValidateDraft()
    {
        if (SelectedDraft == null) return;
        Run("player.engineering.draft.validate", () =>
        {
            ValidationRows.Clear();
            var response = _api.EngineeringPlayerDraftValidate(new Dictionary<string, object>(BasePayload()) { { "draftId", SelectedDraft.DraftId } });
            EnsureOk(response);
            var validation = Dict(Get(response.Payload, "validation"));
            ValidationRows.Add(Str(Get(validation, "summary"), "Проверка выполнена."));
            foreach (var issue in List(Get(validation, "issues")).Select(Dict))
                ValidationRows.Add($"{Str(Get(issue, "severity"))}: {Str(Get(issue, "message"))}");
        });
    }

    private void SubmitDraft()
    {
        if (SelectedDraft == null) return;
        Run("player.engineering.draft.submit", () =>
        {
            var response = _api.EngineeringPlayerDraftSubmit(new Dictionary<string, object>(BasePayload()) { { "draftId", SelectedDraft.DraftId } });
            EnsureOk(response);
            LoadDrafts();
            StatusMessage = "Предложение отправлено GM.";
        });
    }

    private Dictionary<string, object> BasePayload()
    {
        var characterId = _activeCharacterIdAccessor();
        var payload = new Dictionary<string, object> { { "campaignId", CampaignId } };
        if (!string.IsNullOrWhiteSpace(characterId)) payload["characterId"] = characterId;
        return payload;
    }

    private object[] ModuleIds() => DraftModuleIds.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Cast<object>().ToArray();

    private void LoadLines(ResponseEnvelope response, ObservableCollection<string> target, Func<Dictionary<string, object>, string> formatter)
    {
        target.Clear();
        EnsureOk(response);
        foreach (var map in Items(response)) target.Add(formatter(map));
        if (target.Count == 0) target.Add("Данных пока нет или они не раскрыты игрокам.");
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
            ErrorMessage = PlayerFacingMessage(ex.Message, "Инженерный раздел пока недоступен.");
            StatusMessage = "Инженерный раздел пока недоступен.";
            ClientLogService.Instance.Error(scope + ".error " + ex.Message);
        }
    }

    private static void EnsureOk(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(PlayerFacingMessage(response.Message, "Инженерный раздел пока недоступен."));
    }

    private static string PlayerFacingMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        if (message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0)
            return fallback;
        return message;
    }

    private static IEnumerable<Dictionary<string, object>> Items(ResponseEnvelope response) => List(Get(response.Payload, "items")).Select(Dict);
    internal static object? Get(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    internal static Dictionary<string, object> Dict(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value!;
            }
            return result;
        }
        return new Dictionary<string, object>();
    }

    internal static IEnumerable<object> List(object? raw)
    {
        if (raw is IEnumerable enumerable && raw is not string)
        {
            foreach (var item in enumerable) yield return item!;
        }
    }

    internal static string Str(object? raw, string fallback = "") => string.IsNullOrWhiteSpace(Convert.ToString(raw)) ? fallback : Convert.ToString(raw)!;
}

public sealed class EngineeringDraftPlayerItem
{
    public string DraftId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Summary => $"{Name} • {Status}";

    public static EngineeringDraftPlayerItem From(Dictionary<string, object> map) => new()
    {
        DraftId = PlayerEngineeringViewModel.Str(PlayerEngineeringViewModel.Get(map, "draftId"), PlayerEngineeringViewModel.Str(PlayerEngineeringViewModel.Get(map, "id"))),
        Name = PlayerEngineeringViewModel.Str(PlayerEngineeringViewModel.Get(map, "name"), "Черновик"),
        Status = PlayerEngineeringViewModel.Str(PlayerEngineeringViewModel.Get(map, "status"), "draft")
    };
}
