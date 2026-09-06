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

public sealed partial class PlayerEngineeringViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private string _campaignId = "default";
    private string _statusMessage = "Инженерные проекты создают предложения и чертежи. Готовая техника здесь не выдаётся.";
    private string _errorMessage = string.Empty;
    private EngineeringDraftPlayerItem? _selectedDraft;
    private EngineeringChoiceItem? _selectedPlatform;
    private EngineeringChoiceItem? _selectedPreset;
    private string _draftName = "Предложение конструкции";
    private string _draftRole = string.Empty;
    private string _draftNotes = string.Empty;

    public PlayerEngineeringViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        RefreshCommand = new RelayCommand(RefreshAll);
        CreateDraftCommand = new RelayCommand(CreateDraft);
        ValidateDraftCommand = new RelayCommand(ValidateDraft);
        SubmitDraftCommand = new RelayCommand(SubmitDraft);
        PreviewProposalCommand = new RelayCommand(PreviewProposal);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
        InitializeResearchRuntime0192();
        InitializeReverseEngineeringRuntime0193();
        InitializePrototypeRuntime0194();
    }

    public ObservableCollection<EngineeringChoiceItem> Platforms { get; } = new();
    public ObservableCollection<EngineeringChoiceItem> Modules { get; } = new();
    public ObservableCollection<EngineeringChoiceItem> Presets { get; } = new();
    public ObservableCollection<EngineeringDraftPlayerItem> Drafts { get; } = new();
    public ObservableCollection<string> Projects { get; } = new();
    public ObservableCollection<string> Blueprints { get; } = new();
    public ObservableCollection<string> ValidationRows { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand CreateDraftCommand { get; }
    public ICommand ValidateDraftCommand { get; }
    public ICommand SubmitDraftCommand { get; }
    public ICommand PreviewProposalCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); } } }
    public EngineeringDraftPlayerItem? SelectedDraft { get => _selectedDraft; set { if (_selectedDraft != value) { _selectedDraft = value; Notify(); } } }
    public EngineeringChoiceItem? SelectedPlatform
    {
        get => _selectedPlatform;
        set
        {
            if (_selectedPlatform != value)
            {
                _selectedPlatform = value;
                Notify();
                Notify(nameof(DraftSummary));
            }
        }
    }
    public EngineeringChoiceItem? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (_selectedPreset != value)
            {
                _selectedPreset = value;
                Notify();
                ApplySelectedPreset();
                Notify(nameof(DraftSummary));
            }
        }
    }

    public string DraftName
    {
        get => _draftName;
        set
        {
            if (_draftName != value)
            {
                _draftName = value;
                Notify();
                Notify(nameof(DraftSummary));
            }
        }
    }
    public string DraftRole
    {
        get => _draftRole;
        set
        {
            if (_draftRole != value)
            {
                _draftRole = value;
                Notify();
                Notify(nameof(DraftSummary));
            }
        }
    }
    public string DraftNotes
    {
        get => _draftNotes;
        set
        {
            if (_draftNotes != value)
            {
                _draftNotes = value;
                Notify();
            }
        }
    }
    public string DraftSummary
    {
        get
        {
            var platform = SelectedPlatform?.IsPlaceholder == false ? SelectedPlatform.Name : "не выбрана";
            var modules = Modules.Where(item => item.IsSelected && !item.IsPlaceholder).Select(item => item.Name).ToArray();
            var moduleText = modules.Length == 0 ? "не выбраны" : string.Join(", ", modules);
            var preset = SelectedPreset?.IsPlaceholder == false ? SelectedPreset.Name : "не выбран";
            var purpose = string.IsNullOrWhiteSpace(DraftRole) ? "не указано" : DraftRole.Trim();
            return $"Платформа: {platform}\nМодули: {moduleText}\nГотовый вариант: {preset}\nНазначение: {purpose}";
        }
    }

    public void RefreshAll()
    {
        Run("player.engineering.refresh", () =>
        {
            LoadChoices(_api.EngineeringPlayerPlatformList(BasePayload()), Platforms, "platformId", "Платформа");
            LoadChoices(_api.EngineeringPlayerModuleList(BasePayload()), Modules, "moduleId", "Модуль");
            LoadChoices(_api.EngineeringPlayerPresetList(BasePayload()), Presets, "presetId", "Пресет");
            SelectedPlatform = Platforms.FirstOrDefault(item => !item.IsPlaceholder);
            SelectedPreset = null;
            LoadDrafts();
            LoadLines(_api.EngineeringPlayerProjectList(BasePayload()), Projects, m => $"{Str(Get(m, "name"), "Проект")} • {Str(Get(m, "status"))} • {Str(Get(m, "progressPercent"), "0")}%");
            LoadLines(_api.EngineeringPlayerBlueprintList(BasePayload()), Blueprints, m => $"{Str(Get(m, "name"), "Чертёж")} • {Str(Get(m, "status"))}");
            StatusMessage = "Инженерные данные обновлены.";
        });
        RefreshResearchRuntime0192(silent: true);
        RefreshReverseEngineeringRuntime0193(silent: true);
        RefreshPrototypeRuntime0194(silent: true);
    }

    private void LoadDrafts()
    {
        Drafts.Clear();
        var response = _api.EngineeringPlayerDraftList(BasePayload());
        EnsureOk(response);
        foreach (var map in Items(response)) Drafts.Add(EngineeringDraftPlayerItem.From(map));
        SelectedDraft = Drafts.FirstOrDefault();
    }

    private void CreateDraft()
    {
        Run("player.engineering.draft.create", () =>
        {
            var response = _api.EngineeringPlayerDraftCreate(new Dictionary<string, object>(BasePayload())
            {
                { "name", DraftName },
                { "platformId", SelectedPlatform?.Id ?? string.Empty },
                { "moduleIds", ModuleIds() },
                { "presetId", SelectedPreset?.Id ?? string.Empty },
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
        if (SelectedDraft == null)
        {
            ErrorMessage = "Выберите сохранённый черновик для серверной проверки.";
            return;
        }
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
        if (SelectedDraft == null)
        {
            ErrorMessage = "Сначала создайте и выберите черновик.";
            return;
        }
        var confirmation = MessageBox.Show(
            $"Отправить GM предложение «{SelectedDraft.Name}»?\n\nПосле отправки оно перейдёт на рассмотрение.",
            "Отправка инженерного предложения",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            StatusMessage = "Отправка предложения отменена.";
            return;
        }
        Run("player.engineering.draft.submit", () =>
        {
            var response = _api.EngineeringPlayerDraftSubmit(new Dictionary<string, object>(BasePayload()) { { "draftId", SelectedDraft.DraftId } });
            EnsureOk(response);
            LoadDrafts();
            StatusMessage = "Предложение отправлено GM.";
        });
    }

    private void PreviewProposal()
    {
        ErrorMessage = string.Empty;
        ValidationRows.Clear();
        if (SelectedPlatform == null || SelectedPlatform.IsPlaceholder)
        {
            ErrorMessage = "Выберите доступную платформу.";
            StatusMessage = "Предложение требует выбора платформы.";
            return;
        }
        if (Modules.All(item => !item.IsSelected || item.IsPlaceholder))
        {
            ErrorMessage = "Выберите хотя бы один модуль.";
            StatusMessage = "Предложение требует выбора модулей.";
            return;
        }
        if (string.IsNullOrWhiteSpace(DraftName))
        {
            ErrorMessage = "Укажите название предложения.";
            StatusMessage = "Предложение требует названия.";
            return;
        }

        ValidationRows.Add("Предложение готово к сохранению.");
        ValidationRows.Add($"Платформа: {SelectedPlatform.Name}");
        ValidationRows.Add($"Модули: {string.Join(", ", Modules.Where(item => item.IsSelected && !item.IsPlaceholder).Select(item => item.Name))}");
        if (SelectedPreset?.IsPlaceholder == false) ValidationRows.Add($"Готовый вариант: {SelectedPreset.Name}");
        ValidationRows.Add($"Назначение: {(string.IsNullOrWhiteSpace(DraftRole) ? "не указано" : DraftRole.Trim())}");
        StatusMessage = "Предложение проверено. Можно сохранить черновик.";
    }

    private void ClearSelection()
    {
        SelectedPlatform = null;
        SelectedPreset = null;
        foreach (var module in Modules) module.IsSelected = false;
        ValidationRows.Clear();
        ErrorMessage = string.Empty;
        StatusMessage = "Выбор платформы, модулей и готового варианта очищен.";
        Notify(nameof(DraftSummary));
    }

    private Dictionary<string, object> BasePayload()
    {
        var characterId = _activeCharacterIdAccessor();
        var payload = new Dictionary<string, object> { { "campaignId", CampaignId } };
        if (!string.IsNullOrWhiteSpace(characterId)) payload["characterId"] = characterId;
        return payload;
    }

    private object[] ModuleIds() => Modules
        .Where(item => item.IsSelected && !item.IsPlaceholder && !string.IsNullOrWhiteSpace(item.Id))
        .Select(item => (object)item.Id)
        .ToArray();

    private void LoadChoices(ResponseEnvelope response, ObservableCollection<EngineeringChoiceItem> target, string idKey, string fallbackName)
    {
        target.Clear();
        EnsureOk(response);
        foreach (var map in Items(response))
        {
            var name = Str(Get(map, "name"), fallbackName);
            target.Add(new EngineeringChoiceItem
            {
                Id = Str(Get(map, idKey), Str(Get(map, "id"))),
                Name = name,
                Summary = Str(
                    Get(map, "publicSummary"),
                    Str(Get(map, "description"), Str(Get(map, "roleSummary"), "Доступно для инженерного предложения."))),
                PreferredPlatformId = Str(Get(map, "platformId")),
                IncludedModuleIds = List(Get(map, "moduleIds")).Select(value => Str(value)).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
                SelectionChanged = () => Notify(nameof(DraftSummary))
            });
        }
        if (target.Count == 0)
        {
            var emptyText = fallbackName switch
            {
                "Платформа" => "Доступные платформы пока не раскрыты",
                "Модуль" => "Доступные модули пока не раскрыты",
                "Пресет" => "Доступные готовые варианты пока не раскрыты",
                _ => "Доступные варианты пока не раскрыты"
            };
            target.Add(new EngineeringChoiceItem
            {
                Name = emptyText,
                Summary = "Обратитесь к GM.",
                IsPlaceholder = true
            });
        }
    }

    private void ApplySelectedPreset()
    {
        if (SelectedPreset == null || SelectedPreset.IsPlaceholder) return;
        if (!string.IsNullOrWhiteSpace(SelectedPreset.PreferredPlatformId))
        {
            var platform = Platforms.FirstOrDefault(item =>
                string.Equals(item.Id, SelectedPreset.PreferredPlatformId, StringComparison.OrdinalIgnoreCase));
            if (platform != null) SelectedPlatform = platform;
        }

        if (SelectedPreset.IncludedModuleIds.Count > 0)
        {
            foreach (var module in Modules)
            {
                module.IsSelected = SelectedPreset.IncludedModuleIds.Contains(module.Id, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

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

public sealed class EngineeringChoiceItem : ViewModelBase
{
    private bool _isSelected;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PreferredPlatformId { get; set; } = string.Empty;
    public IReadOnlyCollection<string> IncludedModuleIds { get; set; } = Array.Empty<string>();
    public bool IsPlaceholder { get; set; }
    public bool CanSelect => !IsPlaceholder;
    internal Action? SelectionChanged { get; set; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                Notify();
                SelectionChanged?.Invoke();
            }
        }
    }
    public string AccessibleSummary => $"{Name}. {Summary}";
    public override string ToString() => Name;
}

public sealed class EngineeringDraftPlayerItem
{
    public string DraftId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Summary => $"{Name} • {StatusDisplay}";
    public string StatusDisplay => Status.ToLowerInvariant() switch
    {
        "draft" => "Черновик",
        "submitted" => "Отправлен GM",
        "in_review" => "На рассмотрении",
        "approved" => "Одобрен",
        "rejected" => "Отклонён",
        _ => "Состояние не указано"
    };

    public static EngineeringDraftPlayerItem From(Dictionary<string, object> map) => new()
    {
        DraftId = PlayerEngineeringViewModel.Str(PlayerEngineeringViewModel.Get(map, "draftId"), PlayerEngineeringViewModel.Str(PlayerEngineeringViewModel.Get(map, "id"))),
        Name = PlayerEngineeringViewModel.Str(PlayerEngineeringViewModel.Get(map, "name"), "Черновик"),
        Status = PlayerEngineeringViewModel.Str(PlayerEngineeringViewModel.Get(map, "status"), "draft")
    };
}
