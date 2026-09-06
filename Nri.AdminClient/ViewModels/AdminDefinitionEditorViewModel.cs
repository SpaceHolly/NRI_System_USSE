using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Ui.Wpf.Controls;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminDefinitionEditorViewModel : ViewModelBase, IUnsavedChangesAware
{
    private readonly CommandApi _api;
    private DefinitionProfileRow? _selectedProfile;
    private DefinitionRecordRow? _selectedDefinition;
    private DefinitionRecordRow? _selectedReferenceOption;
    private DefinitionRecordRow? _selectedLinkedReference;
    private string _searchText = string.Empty;
    private string _statusMessage = "Справочники ещё не загружены.";
    private string _name = string.Empty;
    private string _displayName = string.Empty;
    private string _shortCode = string.Empty;
    private string _tags = string.Empty;
    private string _systemTags = string.Empty;
    private string _visibilityRule = "player_visible";
    private string _publicDescription = string.Empty;
    private string _gmDescription = string.Empty;
    private string _serverOnlyData = string.Empty;
    private string _validationSummary = string.Empty;
    private string _playerPreviewText = string.Empty;
    private string _gmPreviewText = string.Empty;
    private string _auditText = string.Empty;
    private bool _isHydratingDefinition;
    private string _selectedFamily = "Все";
    private string _selectedType = "Все типы";
    private string _selectedVisibility = "Любая";
    private string _selectedPhysiologyFilter = "Все особенности";
    private string _ruleSetFilter = string.Empty;
    private string _referenceSearchText = string.Empty;
    private string _referencePickerStatus = "Выберите профиль, затем найдите связанную запись.";
    private string _referencePickerValidation = string.Empty;
    private string _selectedReferenceDisplayName = "Без связи";
    private string _referenceTargetCategory = string.Empty;
    private string _confirmationTitle = string.Empty;
    private string _confirmationMessage = string.Empty;
    private string _confirmationTarget = string.Empty;
    private Action? _pendingConfirmation;
    private bool _includeArchived;
    private bool _hasUnsavedChanges;
    private bool _isAdvancedOpen;
    private bool _isInspectorOpen;
    private bool _isConfirmationOpen;
    private bool _isReferencePickerOpen;
    private bool _includeArchivedReferences;
    private bool _hasRoutePermission;
    private bool _isLoading;
    private int _entityRevision;
    private string _errorMessage = string.Empty;

    public AdminDefinitionEditorViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
        CreateCommand = new RelayCommand(Create);
        SaveCommand = new RelayCommand(Save, () => CanSaveDraft);
        CloneCommand = new RelayCommand(Clone, () => SelectedDefinition != null);
        ArchiveCommand = new RelayCommand(() => RequestConfirmation("Архивировать запись", "Запись исчезнет из активного списка, но связи сохранятся.", SelectedDefinition?.DisplayName ?? string.Empty, ArchiveConfirmed), () => SelectedDefinition != null && !SelectedDefinition.IsArchived);
        RestoreCommand = new RelayCommand(() => RequestConfirmation("Восстановить запись", "Запись вернётся в активный список.", SelectedDefinition?.DisplayName ?? string.Empty, RestoreConfirmed), () => SelectedDefinition != null && SelectedDefinition.IsArchived);
        ValidateCommand = new RelayCommand(Validate, () => SelectedDefinition != null || HasUnsavedChanges);
        PlayerPreviewCommand = new RelayCommand(PlayerPreview, () => SelectedDefinition != null);
        LoadAuditCommand = new RelayCommand(LoadAudit, () => SelectedDefinition != null);
        CheckBrokenReferencesCommand = new RelayCommand(CheckBrokenReferences);
        ReferenceSearchCommand = new RelayCommand(SearchReferences);
        AddReferenceCommand = new RelayCommand(AddSelectedReference, () => SelectedReferenceOption != null);
        ClearReferenceCommand = new RelayCommand(ClearReference);
        RemoveReferenceCommand = new RelayCommand(RemoveSelectedReference, () => SelectedLinkedReference != null);
        OpenRelatedRecordCommand = new RelayCommand(OpenRelatedRecord, () => SelectedLinkedReference != null);
        CloseReferencePickerCommand = new RelayCommand(() => IsReferencePickerOpen = false);
        ToggleAdvancedCommand = new RelayCommand(() => IsAdvancedOpen = !IsAdvancedOpen);
        ToggleInspectorCommand = new RelayCommand(() => IsInspectorOpen = !IsInspectorOpen);
        ConfirmActionCommand = new RelayCommand(ConfirmPendingAction);
        CancelConfirmationCommand = new RelayCommand(() => IsConfirmationOpen = false);
        ValidationIssues.CollectionChanged += (_, _) =>
        {
            Notify(nameof(HasValidationIssues));
            Notify(nameof(ValidationFeedbackSummary));
            Notify(nameof(CanSaveDraft));
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        };
    }

    public ObservableCollection<DefinitionProfileRow> Profiles { get; } = new();
    public ObservableCollection<DefinitionRecordRow> Definitions { get; } = new();
    public ObservableCollection<DefinitionRecordRow> FilteredDefinitions { get; } = new();
    public ObservableCollection<DefinitionFieldEditVm> FieldEditors { get; } = new();
    public ObservableCollection<string> CategoryFilters { get; } = new();
    public ObservableCollection<string> FamilyFilters { get; } = new();
    public ObservableCollection<string> TypeFilters { get; } = new();
    public ObservableCollection<string> VisibilityFilters { get; } = new();
    public ObservableCollection<string> PhysiologyFilters { get; } = new(new[] { "Все особенности", "Игровые", "Базовые расы", "Гибриды", "Крылатые", "Естественная броня", "Особые чувства", "Дикие" });
    public ObservableCollection<DefinitionRecordRow> ReferenceOptions { get; } = new();
    public ObservableCollection<DefinitionRecordRow> SelectedReferences { get; } = new();
    public ObservableCollection<string> ReferenceLabels { get; } = new();
    public ObservableCollection<string> ValidationItems { get; } = new();
    public ObservableCollection<ValidationIssueVm> ValidationIssues { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CloneCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand PlayerPreviewCommand { get; }
    public ICommand LoadAuditCommand { get; }
    public ICommand CheckBrokenReferencesCommand { get; }
    public ICommand ReferenceSearchCommand { get; }
    public ICommand AddReferenceCommand { get; }
    public ICommand ClearReferenceCommand { get; }
    public ICommand RemoveReferenceCommand { get; }
    public ICommand OpenRelatedRecordCommand { get; }
    public ICommand CloseReferencePickerCommand { get; }
    public ICommand ToggleAdvancedCommand { get; }
    public ICommand ToggleInspectorCommand { get; }
    public ICommand ConfirmActionCommand { get; }
    public ICommand CancelConfirmationCommand { get; }

    public DefinitionProfileRow? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (_selectedProfile == value) return;
            if (HasUnsavedChanges) StatusMessage = "Есть несохранённые изменения. Сохраните или отмените изменения перед сменой профиля.";
            _selectedProfile = value;
            Notify();
            Notify(nameof(IsResolutionBalancePreviewVisible));
            Notify(nameof(ResolutionBalancePreviewText));
            Notify(nameof(RouteState));
            RebuildFieldEditors(null);
            ApplyFilter();
            RefreshCommandStates();
        }
    }

    public DefinitionRecordRow? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (_selectedDefinition == value) return;
            if (HasUnsavedChanges && _selectedDefinition != null && value != null)
            {
                var pending = value;
                RequestConfirmation(
                    "Несохранённые изменения",
                    "Локальные правки ещё не сохранены. Отмените переход или подтвердите сброс правок.",
                    pending.DisplayName,
                    () =>
                    {
                        HasUnsavedChanges = false;
                        SelectedDefinition = pending;
                    });
                StatusMessage = "Переход остановлен: есть несохранённые изменения.";
                Notify();
                return;
            }
            _selectedDefinition = value;
            Notify();
            Notify(nameof(RouteState));
            LoadSelectedDefinition();
            RefreshCommandStates();
        }
    }

    public DefinitionRecordRow? SelectedReferenceOption
    {
        get => _selectedReferenceOption;
        set { if (_selectedReferenceOption != value) { _selectedReferenceOption = value; Notify(); ((RelayCommand)AddReferenceCommand).RaiseCanExecuteChanged(); } }
    }

    public DefinitionRecordRow? SelectedLinkedReference
    {
        get => _selectedLinkedReference;
        set
        {
            if (_selectedLinkedReference == value) return;
            _selectedLinkedReference = value;
            Notify();
            ((RelayCommand)RemoveReferenceCommand).RaiseCanExecuteChanged();
            ((RelayCommand)OpenRelatedRecordCommand).RaiseCanExecuteChanged();
        }
    }

    public string SearchText { get => _searchText; set { if (_searchText != value) { _searchText = value; Notify(); ApplyFilter(); } } }
    public bool IncludeArchived { get => _includeArchived; set { if (_includeArchived != value) { _includeArchived = value; Notify(); RefreshDefinitions(); } } }
    public bool HasUnsavedChanges { get => _hasUnsavedChanges; private set { if (_hasUnsavedChanges != value) { _hasUnsavedChanges = value; Notify(); Notify(nameof(UnsavedChangesSummary)); Notify(nameof(HasValidationIssues)); Notify(nameof(ValidationFeedbackSummary)); Notify(nameof(CanSaveDraft)); } } }
    public string UnsavedChangesSummary => HasUnsavedChanges ? "Есть несохранённые изменения" : "Изменений нет";
    public bool HasRoutePermission { get => _hasRoutePermission; set { if (_hasRoutePermission == value) return; _hasRoutePermission = value; Notify(); Notify(nameof(PermissionState)); Notify(nameof(RouteState)); } }
    public string PermissionState => HasRoutePermission ? "Раздел доступен." : "Войдите администратором, чтобы открыть справочники.";
    public bool IsLoading { get => _isLoading; private set { if (_isLoading == value) return; _isLoading = value; Notify(); Notify(nameof(RouteState)); } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage == value) return; _errorMessage = value; Notify(); Notify(nameof(HasError)); Notify(nameof(RouteState)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasActiveFilter => !string.IsNullOrWhiteSpace(SearchText)
                                   || !string.Equals(SelectedFamily, "Все", StringComparison.OrdinalIgnoreCase)
                                   || !string.Equals(SelectedType, "Все типы", StringComparison.OrdinalIgnoreCase)
                                   || !string.Equals(SelectedVisibility, "Любая", StringComparison.OrdinalIgnoreCase)
                                   || (IsRaceFamilySelected && !string.Equals(SelectedPhysiologyFilter, "Все особенности", StringComparison.OrdinalIgnoreCase))
                                   || !string.IsNullOrWhiteSpace(RuleSetFilter);
    public string RouteState => AdminRouteStateResolver.ResolveCollection(
        HasRoutePermission,
        IsLoading,
        HasError,
        HasActiveFilter,
        FilteredDefinitions.Count > 0,
        true,
        SelectedDefinition != null || HasUnsavedChanges);
    public Task<bool> CanNavigateAwayAsync()
    {
        if (!HasUnsavedChanges) return Task.FromResult(true);
        RequestConfirmation(
            "Несохранённые изменения",
            "Сохраните запись или подтвердите сброс правок перед переходом.",
            DisplayName,
            () =>
            {
                HasUnsavedChanges = false;
                RefreshDefinitions();
            });
        StatusMessage = "Переход остановлен: есть несохранённые изменения.";
        return Task.FromResult(false);
    }
    public bool IsAdvancedOpen { get => _isAdvancedOpen; set { if (_isAdvancedOpen != value) { _isAdvancedOpen = value; Notify(); Notify(nameof(AdvancedButtonText)); } } }
    public string AdvancedButtonText => IsAdvancedOpen ? "Скрыть технические сведения" : "Показать технические сведения";
    public bool IsInspectorOpen { get => _isInspectorOpen; set { if (_isInspectorOpen != value) { _isInspectorOpen = value; Notify(); Notify(nameof(InspectorButtonText)); } } }
    public string InspectorButtonText => IsInspectorOpen ? "Скрыть инспектор" : "Показать инспектор";
    public bool IsConfirmationOpen { get => _isConfirmationOpen; set { if (_isConfirmationOpen != value) { _isConfirmationOpen = value; Notify(); } } }

    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); Notify(nameof(HasStatusMessage)); } } }
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public string Name { get => _name; set { if (_name != value) { _name = value; Notify(); MarkDirty(); } } }
    public string DisplayName { get => _displayName; set { if (_displayName != value) { _displayName = value; Notify(); if (SelectedDefinition == null) SuggestIdFromDisplayName(value); MarkDirty(); } } }
    public string ShortCode { get => _shortCode; set { if (_shortCode != value) { _shortCode = value; Notify(); MarkDirty(); } } }
    public string Tags { get => _tags; set { if (_tags != value) { _tags = value; Notify(); MarkDirty(); } } }
    public string SystemTags { get => _systemTags; private set { if (_systemTags != value) { _systemTags = value; Notify(); } } }
    public string VisibilityRule { get => _visibilityRule; set { if (_visibilityRule != value) { _visibilityRule = value; Notify(); MarkDirty(); } } }
    public string PublicDescription { get => _publicDescription; set { if (_publicDescription != value) { _publicDescription = value; Notify(); MarkDirty(); } } }
    public string GmDescription { get => _gmDescription; set { if (_gmDescription != value) { _gmDescription = value; Notify(); MarkDirty(); } } }
    public string ServerOnlyData { get => _serverOnlyData; set { if (_serverOnlyData != value) { _serverOnlyData = value; Notify(); MarkDirty(); } } }
    public string ValidationSummary { get => _validationSummary; private set { if (_validationSummary != value) { _validationSummary = value; Notify(); Notify(nameof(ValidationFeedbackSummary)); } } }
    public bool HasValidationIssues => ValidationIssues.Count > 0
                                       || (HasUnsavedChanges
                                           && (string.IsNullOrWhiteSpace(DisplayName)
                                               || string.IsNullOrWhiteSpace(Name)
                                               || FieldEditors.Any(x => x.IsRequired && string.IsNullOrWhiteSpace(x.Value))
                                               || FieldEditors.Any(x => x.HasValidationError)));
    public string ValidationFeedbackSummary => HasValidationIssues ? FirstNonEmpty(ValidationSummary, "Исправьте ошибки в форме.") : string.Empty;
    public bool CanSaveDraft => SelectedProfile != null && !HasValidationIssues;
    public string PlayerPreviewText { get => _playerPreviewText; private set { if (_playerPreviewText != value) { _playerPreviewText = value; Notify(); } } }
    public string GmPreviewText { get => _gmPreviewText; private set { if (_gmPreviewText != value) { _gmPreviewText = value; Notify(); } } }
    public bool IsResolutionBalancePreviewVisible => SelectedProfile != null && new[]
    {
        "resolution_profile", "ability_modifier_profile", "skill_mastery_profile", "modifier_category_profile",
        "advantage_policy", "difficulty_profile", "degree_of_success_profile", "attempt_gate_profile",
        "hit_resolution_profile", "penetration_damage_profile"
    }.Contains(SelectedProfile.Category, StringComparer.OrdinalIgnoreCase);
    public string ResolutionBalancePreviewText
    {
        get
        {
            var difficulty = FieldIntValue("standard", 12);
            var mastery = SelectedProfile?.Category == "skill_mastery_profile" ? FieldIntValue("rank5To8Bonus", 2) : 2;
            var success = 0;
            var strongOrBetter = 0;
            var criticalFailure = 0;
            for (var roll = 1; roll <= 20; roll++)
            {
                if (roll == 1) { criticalFailure++; continue; }
                var margin = roll + mastery - difficulty;
                var passed = roll == 20 || margin >= 0;
                if (!passed) continue;
                success++;
                if (roll == 20 || margin >= 4) strongOrBetter++;
            }
            return $"Характеристика +0 · ранг 5 ({mastery:+0;-0;0}) · без временных бонусов · обычная сложность {difficulty}\n" +
                   $"Успех: {success * 5}% · сильный или лучше: {strongOrBetter * 5}% · критическая неудача: {criticalFailure * 5}%\n" +
                   "Расчёт перебирает все 20 результатов d20; ранг навыка напрямую к броску не прибавляется.";
        }
    }
    public string AuditText { get => _auditText; private set { if (_auditText != value) { _auditText = value; Notify(); } } }
    public string SelectedFamily { get => _selectedFamily; set { if (_selectedFamily != value) { _selectedFamily = value; Notify(); Notify(nameof(IsRaceFamilySelected)); ApplyFilter(); } } }
    public string SelectedType { get => _selectedType; set { if (_selectedType != value) { _selectedType = value; Notify(); ApplyFilter(); } } }
    public string SelectedVisibility { get => _selectedVisibility; set { if (_selectedVisibility != value) { _selectedVisibility = value; Notify(); ApplyFilter(); } } }
    public string SelectedPhysiologyFilter { get => _selectedPhysiologyFilter; set { if (_selectedPhysiologyFilter != value) { _selectedPhysiologyFilter = value; Notify(); ApplyFilter(); } } }
    public bool IsRaceFamilySelected => string.Equals(SelectedFamily, "Расы", StringComparison.OrdinalIgnoreCase);
    public string RuleSetFilter { get => _ruleSetFilter; set { if (_ruleSetFilter != value) { _ruleSetFilter = value; Notify(); ApplyFilter(); } } }
    public string ReferenceSearchText { get => _referenceSearchText; set { if (_referenceSearchText != value) { _referenceSearchText = value; Notify(); } } }
    public bool IncludeArchivedReferences { get => _includeArchivedReferences; set { if (_includeArchivedReferences != value) { _includeArchivedReferences = value; Notify(); SearchReferences(); } } }
    public bool IsReferencePickerOpen { get => _isReferencePickerOpen; set { if (_isReferencePickerOpen != value) { _isReferencePickerOpen = value; Notify(); } } }
    public string ReferenceTargetCategory { get => _referenceTargetCategory; private set { if (_referenceTargetCategory != value) { _referenceTargetCategory = value; Notify(); Notify(nameof(ReferenceTargetLabel)); } } }
    public string ReferenceTargetLabel => string.IsNullOrWhiteSpace(ReferenceTargetCategory) ? "Все доступные типы" : DefinitionLabels.Category(ReferenceTargetCategory);
    public string ReferencePickerStatus { get => _referencePickerStatus; private set { if (_referencePickerStatus != value) { _referencePickerStatus = value; Notify(); } } }
    public string ReferencePickerValidation { get => _referencePickerValidation; private set { if (_referencePickerValidation != value) { _referencePickerValidation = value; Notify(); } } }
    public string SelectedReferenceDisplayName { get => _selectedReferenceDisplayName; private set { if (_selectedReferenceDisplayName != value) { _selectedReferenceDisplayName = value; Notify(); } } }
    public string ConfirmationTitle { get => _confirmationTitle; private set { _confirmationTitle = value; Notify(); } }
    public string ConfirmationMessage { get => _confirmationMessage; private set { _confirmationMessage = value; Notify(); } }
    public string ConfirmationTarget { get => _confirmationTarget; private set { _confirmationTarget = value; Notify(); } }
    public ObservableCollection<DefinitionValueOption> VisibilityRuleOptions { get; } = new(new[]
    {
        new DefinitionValueOption("player_visible", "Видно игрокам"),
        new DefinitionValueOption("public", "Публично"),
        new DefinitionValueOption("gm_only", "Только мастер"),
        new DefinitionValueOption("hidden", "Скрыто")
    });

    public void Refresh()
    {
        ClientLogService.Instance.Info("admin.definitionEditor.load.start");
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var profiles = _api.ContentDefinitionAdminListProfiles();
            if (profiles.Status != ResponseStatus.Ok)
            {
                ErrorMessage = "Не удалось загрузить профили правил.";
                StatusMessage = ErrorMessage;
                return;
            }

            ClearGeneratedPanels();
            Profiles.Clear();
            CategoryFilters.Clear();
            FamilyFilters.Clear();
            VisibilityFilters.Clear();
            CategoryFilters.Add("Все категории");
            foreach (var family in new[] { "Все", "Мир, языки и знания", "Фракции, организации и экономика", "Технологии, рецепты и проекты", "Расы", "Характеристики", "Навыки", "Развитие", "Проверки и бой", "Прочее" }) FamilyFilters.Add(family);
            foreach (var visibility in new[] { "Любая", "Видно игрокам", "Публично", "Только GM", "Скрыто" }) VisibilityFilters.Add(visibility);
            foreach (var item in ReadArray(profiles.Payload, "profiles"))
            {
                var row = new DefinitionProfileRow(AsMap(item));
                Profiles.Add(row);
                CategoryFilters.Add(row.DisplayName);
            }
            if (!FamilyFilters.Contains(SelectedFamily)) SelectedFamily = "Все";
            if (!VisibilityFilters.Contains(SelectedVisibility)) SelectedVisibility = "Любая";
            SelectedProfile ??= Profiles.FirstOrDefault();
            RefreshDefinitions();
            if (!TryApplyCanonicalBlueprintDraft0187())
            {
                HasUnsavedChanges = false;
                StatusMessage = $"Профилей: {Profiles.Count}; записей: {Definitions.Count}.";
            }
            ClientLogService.Instance.Info($"admin.definitionEditor.load.done profiles={Profiles.Count} definitions={Definitions.Count}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshDefinitions()
    {
        var response = _api.ContentDefinitionAdminList(new Dictionary<string, object> { ["includeArchived"] = IncludeArchived });
        if (response.Status != ResponseStatus.Ok)
        {
            ErrorMessage = "Не удалось загрузить записи справочников.";
            StatusMessage = ErrorMessage;
            return;
        }
        ErrorMessage = string.Empty;
        Definitions.Clear();
        foreach (var item in ReadArray(response.Payload, "definitions")) Definitions.Add(new DefinitionRecordRow(AsMap(item)));
        RebuildTypeFilters();
        ApplyFilter();
        if (IsReferencePickerOpen) SearchReferences();
    }

    private void ApplyFilter()
    {
        FilteredDefinitions.Clear();
        var query = SearchText ?? string.Empty;
        foreach (var item in Definitions.Where(x =>
                     FamilyMatches(x.Category) &&
                     (SelectedType == "Все типы" || string.Equals(x.CategoryLabel, SelectedType, StringComparison.OrdinalIgnoreCase)) &&
                     VisibilityMatches(x.VisibilityRule) &&
                     PhysiologyMatches(x) &&
                     (string.IsNullOrWhiteSpace(RuleSetFilter) || Contains(x.RuleSetId, RuleSetFilter)) &&
                     (string.IsNullOrWhiteSpace(query) || Contains(x.DisplayName, query) || Contains(x.RawDisplayName, query) || Contains(x.Name, query) || Contains(x.ShortCode, query) || Contains(x.CategoryLabel, query) || Contains(x.Tags, query))))
        {
            FilteredDefinitions.Add(item);
        }
        Notify(nameof(HasActiveFilter));
        Notify(nameof(RouteState));
    }

    private void Create()
    {
        SelectedDefinition = null;
        DisplayName = "Новая запись";
        SuggestIdFromDisplayName(DisplayName);
        Tags = SelectedProfile?.CategoryLabel ?? string.Empty;
        VisibilityRule = "player_visible";
        PublicDescription = string.Empty;
        GmDescription = string.Empty;
        ServerOnlyData = string.Empty;
        _entityRevision = 0;
        SelectedReferences.Clear();
        ReferenceLabels.Clear();
        SelectedReferenceDisplayName = "Без связи";
        RebuildFieldEditors(null);
        HasUnsavedChanges = true;
        StatusMessage = "Технический идентификатор будет создан автоматически.";
    }

    private void Save()
    {
        if (SelectedProfile == null) return;
        var localError = ValidateLocalFields();
        if (!string.IsNullOrWhiteSpace(localError))
        {
            ValidationIssues.Clear();
            AddValidationIssue("Ошибка", string.Empty, localError, "Проверка обязательных полей формы");
            ValidationSummary = localError;
            StatusMessage = "Исправьте поля перед сохранением.";
            return;
        }

        var response = SelectedDefinition == null
            ? _api.ContentDefinitionAdminCreate(BuildSavePayload())
            : _api.ContentDefinitionAdminUpdate(BuildSavePayload());
        HandleDefinitionSaveResponse(response);
    }

    private void Clone()
    {
        if (SelectedDefinition == null) return;
        var response = _api.ContentDefinitionAdminClone(new Dictionary<string, object>
        {
            ["definitionId"] = SelectedDefinition.DefinitionId,
            ["name"] = SelectedDefinition.Name + "_copy",
            ["displayName"] = SelectedDefinition.DisplayName + " (копия)",
            ["shortCode"] = SelectedDefinition.ShortCode + "_copy"
        });
        HandleDefinitionSaveResponse(response);
    }

    private void ArchiveConfirmed()
    {
        if (SelectedDefinition == null) return;
        var response = _api.ContentDefinitionAdminArchive(new Dictionary<string, object> { ["definitionId"] = SelectedDefinition.DefinitionId });
        StatusMessage = response.Status == ResponseStatus.Ok
            ? "Запись отправлена в архив."
            : "Не удалось изменить состояние архива записи.";
        RefreshDefinitions();
    }

    private void RestoreConfirmed()
    {
        if (SelectedDefinition == null) return;
        var response = _api.ContentDefinitionAdminRestore(new Dictionary<string, object> { ["definitionId"] = SelectedDefinition.DefinitionId });
        StatusMessage = response.Status == ResponseStatus.Ok
            ? "Запись восстановлена из архива."
            : "Не удалось изменить состояние архива записи.";
        RefreshDefinitions();
    }

    private void Validate()
    {
        ValidationItems.Clear();
        ValidationIssues.Clear();
        var local = ValidateLocalFields();
        if (!string.IsNullOrWhiteSpace(local))
        {
            foreach (var message in local.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                ValidationItems.Add(message);
                AddValidationIssue("Ошибка", string.Empty, message, "Проверка обязательных полей формы");
            }
        }
        if (SelectedDefinition == null)
        {
            ValidationSummary = string.IsNullOrWhiteSpace(local) ? "Проверка формы пройдена." : "Исправьте ошибки в форме.";
            Notify(nameof(HasValidationIssues));
            StatusMessage = string.IsNullOrWhiteSpace(local) ? "Локальная проверка пройдена. Сохраните запись для server validation." : "Есть ошибки в форме.";
            return;
        }
        var response = _api.ContentDefinitionAdminValidate(new Dictionary<string, object> { ["definitionId"] = SelectedDefinition.DefinitionId });
        if (response.Status == ResponseStatus.Ok)
        {
            ApplyValidationPayload(AsMap(response.Payload.TryGetValue("validation", out var v) ? v : null));
            StatusMessage = "Проверка записи завершена.";
        }
        else
        {
            AddValidationIssue("Ошибка", string.Empty, "Не удалось проверить запись.", response.Message);
            ValidationSummary = "Проверка записи не выполнена.";
            StatusMessage = "Не удалось проверить запись.";
        }
    }

    private bool TryApplyCanonicalBlueprintDraft0187()
    {
        var draft = CanonicalBlueprintDraftTransfer0187.Take();
        if (draft == null) return false;
        var profile = Profiles.FirstOrDefault(x => string.Equals(x.ProfileId, draft.ProfileId, StringComparison.OrdinalIgnoreCase))
                      ?? Profiles.FirstOrDefault(x => string.Equals(x.Category, TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint, StringComparison.OrdinalIgnoreCase));
        if (profile == null)
        {
            ErrorMessage = "Профиль канонического чертежа не найден.";
            return false;
        }
        SelectedFamily = "Технологии, рецепты и проекты";
        SelectedDefinition = null;
        SelectedProfile = profile;
        Name = draft.Name;
        DisplayName = draft.DisplayName;
        ShortCode = string.Empty;
        Tags = string.Empty;
        VisibilityRule = draft.VisibilityRule;
        PublicDescription = draft.PublicDescription;
        GmDescription = draft.GMDescription;
        ServerOnlyData = string.Empty;
        RebuildFieldEditors(draft.CustomFields);
        HasUnsavedChanges = true;
        ValidationSummary = draft.UnresolvedComponentCount > 0
            ? $"Проверьте компоненты: сопоставлено {draft.ResolvedComponentCount}, требуют ручного выбора {draft.UnresolvedComponentCount}. Пока обязательные строки не сопоставлены, сохранение будет отклонено сервером."
            : $"Все {draft.ResolvedComponentCount} компонентов сопоставлены. Проверьте черновик перед сохранением.";
        StatusMessage = "Открыт несохранённый канонический черновик. Исходный личный чертёж не изменён.";
        RefreshCommandStates();
        return true;
    }

    private void PlayerPreview()
    {
        if (SelectedDefinition == null) return;
        var response = _api.ContentDefinitionAdminPreviewAsPlayer(new Dictionary<string, object> { ["definitionId"] = SelectedDefinition.DefinitionId });
        PlayerPreviewText = response.Status == ResponseStatus.Ok
            ? HumanizeFlatten(AsMap(response.Payload.TryGetValue("definition", out var v) ? v : null), playerSafe: true)
            : "Предпросмотр для игрока недоступен.";
        IsInspectorOpen = true;
        StatusMessage = response.Status == ResponseStatus.Ok
            ? "Предпросмотр для игрока обновлён."
            : "Не удалось подготовить предпросмотр для игрока.";
    }

    private void LoadAudit()
    {
        if (SelectedDefinition == null) return;
        var response = _api.ContentDefinitionAdminListAudit(new Dictionary<string, object> { ["definitionId"] = SelectedDefinition.DefinitionId });
        AuditText = response.Status == ResponseStatus.Ok
            ? string.Join(Environment.NewLine, ReadArray(response.Payload, "audit").Select(x => HumanizeFlatten(AsMap(x))))
            : "История изменений недоступна.";
        StatusMessage = response.Status == ResponseStatus.Ok
            ? "История изменений загружена."
            : "Не удалось загрузить историю изменений.";
    }

    private void CheckBrokenReferences()
    {
        var response = _api.ContentDefinitionAdminCheckBrokenReferences();
        if (response.Status == ResponseStatus.Ok)
        {
            ApplyValidationPayload(response.Payload);
            StatusMessage = "Проверка связей завершена.";
        }
        else
        {
            AddValidationIssue("Ошибка", string.Empty, "Не удалось проверить связи.", response.Message);
            ValidationSummary = "Проверка связей не выполнена.";
            StatusMessage = "Не удалось проверить связи.";
        }
    }

    private void LoadSelectedDefinition()
    {
        if (SelectedDefinition == null)
        {
            RebuildFieldEditors(null);
            return;
        }
        var response = _api.ContentDefinitionAdminGet(new Dictionary<string, object> { ["definitionId"] = SelectedDefinition.DefinitionId });
        if (response.Status != ResponseStatus.Ok)
        {
            ErrorMessage = "Не удалось открыть запись справочника.";
            StatusMessage = ErrorMessage;
            return;
        }
        ErrorMessage = string.Empty;
        _isHydratingDefinition = true;
        var map = AsMap(response.Payload.TryGetValue("definition", out var value) ? value : null);
        GmPreviewText = HumanizeFlatten(map);
        Name = Get(map, "name");
        DisplayName = Get(map, "displayName");
        ShortCode = Get(map, "shortCode");
        var publicTags = ReadArray(map, "publicTags");
        var allTags = ReadArray(map, "tags");
        Tags = Join((publicTags.Length > 0 ? publicTags : allTags)
            .Where(x => IsPlayerSafeTag(Convert.ToString(x) ?? string.Empty)));
        var systemTags = ReadArray(map, "systemTags");
        SystemTags = Join(systemTags.Length > 0 ? systemTags : allTags.Where(x => !IsPlayerSafeTag(Convert.ToString(x) ?? string.Empty)));
        VisibilityRule = Get(map, "visibilityRule");
        PublicDescription = Get(map, "publicDescription");
        GmDescription = Get(map, "gmDescription");
        ServerOnlyData = HumanizeFlatten(AsMap(map.TryGetValue("serverOnlyData", out var server) ? server : null));
        _entityRevision = int.TryParse(Get(map, "entityRevision"), out var revision) ? revision : 1;
        SelectedReferences.Clear();
        ReferenceLabels.Clear();
        foreach (var referenceId in ReadArray(map, "referenceIds").Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var option = Definitions.FirstOrDefault(x => string.Equals(x.DefinitionId, referenceId, StringComparison.OrdinalIgnoreCase));
            if (option == null)
            {
                ReferencePickerValidation = "Одна из связанных записей недоступна или была удалена.";
                continue;
            }
            SelectedReferences.Add(option);
            ReferenceLabels.Add(option.DisplayName);
        }
        SelectedReferenceDisplayName = ReferenceLabels.Count == 0 ? "Без связи" : string.Join(", ", ReferenceLabels);
        var profile = Profiles.FirstOrDefault(x => string.Equals(x.Category, SelectedDefinition.Category, StringComparison.OrdinalIgnoreCase));
        if (profile != null) _selectedProfile = profile;
        Notify(nameof(SelectedProfile));
        RebuildFieldEditors(AsMap(map.TryGetValue("customFields", out var custom) ? custom : null));
        ApplyValidationPayload(AsMap(map.TryGetValue("validation", out var validation) ? validation : null));
        AuditText = string.Join(Environment.NewLine, ReadArray(response.Payload, "audit").Select(x => HumanizeFlatten(AsMap(x))));
        HasUnsavedChanges = false;
        StatusMessage = "Запись загружена.";
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        Action completeHydration = () =>
        {
            _isHydratingDefinition = false;
            HasUnsavedChanges = false;
            RefreshCommandStates();
        };
        if (dispatcher == null) completeHydration();
        else dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, completeHydration);
    }

    private Dictionary<string, object> BuildSavePayload()
    {
        var custom = FieldEditors.ToDictionary(x => x.FieldName, x => (object)(x.Value ?? string.Empty), StringComparer.Ordinal);
        var payload = new Dictionary<string, object>
        {
            ["category"] = SelectedProfile?.Category ?? string.Empty,
            ["name"] = Name,
            ["displayName"] = DisplayName,
            ["shortCode"] = ShortCode,
            ["tags"] = Split(Tags).Cast<object>().ToArray(),
            ["systemTags"] = Split(SystemTags).Cast<object>().ToArray(),
            ["visibilityRule"] = VisibilityRule,
            ["publicDescription"] = PublicDescription,
            ["gmDescription"] = GmDescription,
            ["customFields"] = custom,
            ["serverOnlyData"] = ParseKeyValue(ServerOnlyData),
            ["referenceIds"] = SelectedReferences.Select(x => (object)x.DefinitionId).ToArray()
        };
        if (SelectedDefinition != null)
        {
            payload["definitionId"] = SelectedDefinition.DefinitionId;
            payload["expectedRevision"] = _entityRevision;
        }
        foreach (var item in custom) payload["customField_" + item.Key] = item.Value;
        return payload;
    }

    private void HandleDefinitionSaveResponse(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
        {
            ErrorMessage = "Не удалось сохранить запись справочника.";
            StatusMessage = "Не удалось сохранить запись справочника.";
            return;
        }
        ErrorMessage = string.Empty;
        StatusMessage = "Изменения сохранены.";
        HasUnsavedChanges = false;
        RefreshDefinitions();
        var definition = AsMap(response.Payload.TryGetValue("definition", out var value) ? value : null);
        var id = Get(definition, "definitionId");
        _entityRevision = int.TryParse(Get(definition, "entityRevision"), out var revision) ? revision : _entityRevision;
        SelectedDefinition = Definitions.FirstOrDefault(x => x.DefinitionId == id);
        ApplyValidationPayload(AsMap(definition.TryGetValue("validation", out var validation) ? validation : null));
        StatusMessage = "Изменения сохранены.";
    }

    private void RebuildFieldEditors(Dictionary<string, object>? values)
    {
        FieldEditors.Clear();
        ReferenceTargetCategory = string.Empty;
        foreach (var field in SelectedProfile?.Fields ?? Enumerable.Empty<DefinitionFieldRow>())
        {
            object? value = null;
            values?.TryGetValue(field.FieldName, out value);
            var vm = new DefinitionFieldEditVm(field, FieldEditorValue(field, value), SearchFieldReferenceOptions);
            vm.ValueChanged += (_, _) =>
            {
                MarkDirty();
                Notify(nameof(ResolutionBalancePreviewText));
                Notify(nameof(CanSaveDraft));
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            };
            FieldEditors.Add(vm);
        }
        Notify(nameof(IsResolutionBalancePreviewVisible));
        Notify(nameof(ResolutionBalancePreviewText));
    }

    private static string FieldEditorValue(DefinitionFieldRow field, object? value)
    {
        if (value == null) return string.Empty;
        if ((string.Equals(field.FieldType, ContentDefinitionFieldTypes.ReferenceList, StringComparison.OrdinalIgnoreCase)
             || string.Equals(field.FieldType, ContentDefinitionFieldTypes.Tags, StringComparison.OrdinalIgnoreCase))
            && value is IEnumerable values && value is not string)
        {
            return string.Join(", ", values.Cast<object>()
                .Select(Convert.ToString)
                .Where(item => !string.IsNullOrWhiteSpace(item)));
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private int FieldIntValue(string fieldName, int fallback)
    {
        var value = FieldEditors.FirstOrDefault(x => string.Equals(x.FieldName, fieldName, StringComparison.OrdinalIgnoreCase))?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || int.TryParse(value, out parsed) ? parsed : fallback;
    }

    private IReadOnlyList<NriReferenceOption> SearchFieldReferenceOptions(DefinitionFieldEditVm field)
    {
        var category = FirstNonEmpty(field.Schema.ReferenceCategory, field.Schema.ReferenceTargetTypes.FirstOrDefault() ?? string.Empty);
        var search = field.ReferenceSearchText ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var localMatches = Definitions
                .Where(row => (string.IsNullOrWhiteSpace(category) || string.Equals(row.Category, category, StringComparison.OrdinalIgnoreCase))
                    && (IncludeArchivedReferences || !row.IsArchived)
                    && (string.IsNullOrWhiteSpace(RuleSetFilter) || string.Equals(row.RuleSetId, RuleSetFilter, StringComparison.OrdinalIgnoreCase))
                    && (string.Equals(row.DefinitionId, search, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(row.ShortCode, search, StringComparison.OrdinalIgnoreCase)))
                .Select(ToReferenceOption)
                .Take(30)
                .ToArray();
            if (localMatches.Length > 0) return localMatches;
        }
        var response = _api.ContentDefinitionAdminSearchReferenceOptions(new Dictionary<string, object>
        {
            ["search"] = search,
            ["referenceCategory"] = category,
            ["ruleSetId"] = RuleSetFilter ?? string.Empty,
            ["includeArchived"] = IncludeArchivedReferences,
            ["excludeDefinitionId"] = SelectedDefinition?.DefinitionId ?? string.Empty,
            ["limit"] = 30
        });
        if (response.Status != ResponseStatus.Ok) return Array.Empty<NriReferenceOption>();
        return ReadArray(response.Payload, "options")
            .Select(x => ToReferenceOption(new DefinitionRecordRow(AsMap(x))))
            .ToArray();
    }

    private static NriReferenceOption ToReferenceOption(DefinitionRecordRow row)
        => new()
        {
            Id = row.DefinitionId,
            CanonicalKey = FirstNonEmpty(row.ShortCode, row.Name),
            DisplayName = row.DisplayName,
            TypeLabel = row.CategoryLabel,
            StatusLabel = row.StatusLabel,
            IsArchived = row.IsArchived
        };

    private void SearchReferences()
    {
        IsReferencePickerOpen = true;
        ReferenceOptions.Clear();
        ReferencePickerStatus = "Загрузка вариантов...";
        var response = _api.ContentDefinitionAdminSearchReferenceOptions(new Dictionary<string, object>
        {
            ["search"] = ReferenceSearchText ?? string.Empty,
            ["referenceCategory"] = ReferenceTargetCategory,
            ["ruleSetId"] = RuleSetFilter ?? string.Empty,
            ["includeArchived"] = IncludeArchivedReferences,
            ["excludeDefinitionId"] = SelectedDefinition?.DefinitionId ?? string.Empty,
            ["limit"] = 30
        });
        if (response.Status != ResponseStatus.Ok)
        {
            ReferencePickerStatus = "Не удалось загрузить варианты связи.";
            return;
        }
        foreach (var item in ReadArray(response.Payload, "options")) ReferenceOptions.Add(new DefinitionRecordRow(AsMap(item)));
        ReferencePickerStatus = ReferenceOptions.Count == 0 ? "Совпадений нет." : $"Найдено вариантов: {ReferenceOptions.Count}.";
    }

    private void AddSelectedReference()
    {
        if (SelectedReferenceOption == null) return;
        if (SelectedReferences.Any(x => string.Equals(x.DefinitionId, SelectedReferenceOption.DefinitionId, StringComparison.OrdinalIgnoreCase)))
        {
            ReferencePickerValidation = "Эта связь уже добавлена.";
            return;
        }
        SelectedReferences.Add(SelectedReferenceOption);
        ReferenceLabels.Add(SelectedReferenceOption.DisplayName);
        SelectedReferenceDisplayName = string.Join(", ", ReferenceLabels);
        ReferencePickerValidation = SelectedReferenceOption.IsArchived ? "Связанная запись в архиве." : string.Empty;
        MarkDirty();
    }

    private void RemoveSelectedReference()
    {
        if (SelectedLinkedReference == null) return;
        var removed = SelectedLinkedReference;
        SelectedReferences.Remove(removed);
        ReferenceLabels.Remove(removed.DisplayName);
        SelectedReferenceDisplayName = ReferenceLabels.Count == 0 ? "Без связи" : string.Join(", ", ReferenceLabels);
        SelectedLinkedReference = null;
        ReferencePickerValidation = string.Empty;
        MarkDirty();
    }

    private void OpenRelatedRecord()
    {
        if (SelectedLinkedReference == null) return;
        SelectedDefinition = Definitions.FirstOrDefault(x => string.Equals(x.DefinitionId, SelectedLinkedReference.DefinitionId, StringComparison.OrdinalIgnoreCase));
        IsReferencePickerOpen = false;
    }

    private void ClearReference()
    {
        ReferenceLabels.Clear();
        SelectedReferences.Clear();
        SelectedReferenceDisplayName = "Без связи";
        ReferencePickerValidation = string.Empty;
        MarkDirty();
    }

    private void RequestConfirmation(string title, string message, string target, Action action)
    {
        ConfirmationTitle = title;
        ConfirmationMessage = message;
        ConfirmationTarget = target;
        _pendingConfirmation = action;
        IsConfirmationOpen = true;
    }

    private void ConfirmPendingAction()
    {
        IsConfirmationOpen = false;
        var action = _pendingConfirmation;
        _pendingConfirmation = null;
        action?.Invoke();
    }

    private void MarkDirty()
    {
        if (_isHydratingDefinition) return;
        HasUnsavedChanges = true;
        Notify(nameof(HasValidationIssues));
        Notify(nameof(ValidationFeedbackSummary));
        Notify(nameof(CanSaveDraft));
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
    }

    private void ClearGeneratedPanels()
    {
        ValidationSummary = string.Empty;
        PlayerPreviewText = string.Empty;
        GmPreviewText = string.Empty;
        AuditText = string.Empty;
        ValidationItems.Clear();
        ValidationIssues.Clear();
        Notify(nameof(HasValidationIssues));
        ReferenceLabels.Clear();
        SelectedReferences.Clear();
        ReferenceOptions.Clear();
    }

    private string ValidateLocalFields()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(DisplayName)) errors.Add("Название обязательно.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Системный ID должен быть создан до сохранения.");
        foreach (var field in FieldEditors.Where(x => x.IsRequired && string.IsNullOrWhiteSpace(x.Value))) errors.Add($"Заполните поле: {field.DisplayName}.");
        foreach (var field in FieldEditors.Where(x => x.HasValidationError)) errors.Add($"Исправьте значение поля: {field.DisplayName}.");
        return string.Join(Environment.NewLine, errors);
    }

    private void ApplyValidationPayload(IDictionary<string, object> validation)
    {
        ValidationItems.Clear();
        ValidationIssues.Clear();
        AddValidationArray(validation, "errors", "Ошибка");
        AddValidationArray(validation, "warnings", "Предупреждение");
        AddValidationArray(validation, "visibilityWarnings", "Предупреждение");
        AddValidationArray(validation, "schemaWarnings", "Информация");
        AddValidationArray(validation, "brokenReferences", "Ошибка");
        var errors = ValidationIssues.Count(x => x.Severity == "Ошибка");
        var warnings = ValidationIssues.Count(x => x.Severity == "Предупреждение");
        ValidationSummary = ValidationIssues.Count == 0
            ? "Проверка пройдена: ошибок и предупреждений нет."
            : $"Ошибок: {errors}; предупреждений: {warnings}.";
        Notify(nameof(HasValidationIssues));
    }

    private void AddValidationArray(IDictionary<string, object> validation, string key, string defaultSeverity)
    {
        foreach (var item in ReadArray(validation, key))
        {
            var map = AsMap(item);
            if (map.Count == 0)
            {
                var text = Convert.ToString(item) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text)) AddValidationIssue(defaultSeverity, string.Empty, LocalizeValidationMessage(text), text);
                continue;
            }

            var fieldKey = FirstNonEmpty(Get(map, "fieldKey"), Get(map, "field"), Get(map, "fieldName"));
            var message = FirstNonEmpty(Get(map, "userMessage"), Get(map, "message"), Get(map, "description"), "Проверьте значение поля.");
            var rawMessage = FirstNonEmpty(Get(map, "message"), Get(map, "description"));
            var details = FirstNonEmpty(Get(map, "technicalDetails"), Get(map, "code"), Get(map, "rule"), rawMessage);
            message = LocalizeValidationMessage(message);
            AddValidationIssue(defaultSeverity, fieldKey, message, details);
        }
    }

    private static string LocalizeValidationMessage(string message)
    {
        var value = message ?? string.Empty;
        var required = Regex.Match(value, "Required field ['\\\"](?<field>[^'\\\"]+)['\\\"] is missing\\.", RegexOptions.IgnoreCase);
        if (required.Success) return $"Заполните поле «{DefinitionLabels.Field(required.Groups["field"].Value)}».";
        if (value.IndexOf("Duplicate ShortCode", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Короткий код уже используется другой записью.";
        if (value.IndexOf("schema version", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Запись требует обновления профиля правил.";
        if (value.IndexOf("Player-visible race must have FullPlayerDescription", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Для открытой игрокам расы заполните полное описание для игрока.";
        if (value.IndexOf("Player-visible field", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Проверьте видимость поля и его безопасное содержимое для игрока.";
        if (value.IndexOf("invalid value", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Выберите допустимое значение из списка.";
        return value;
    }

    private void AddValidationIssue(string severity, string fieldKey, string message, string technicalDetails)
    {
        var field = FieldEditors.FirstOrDefault(x => string.Equals(x.FieldName, fieldKey, StringComparison.OrdinalIgnoreCase));
        ValidationIssues.Add(new ValidationIssueVm
        {
            Severity = severity,
            FieldKey = fieldKey,
            FieldDisplayName = field?.DisplayName ?? (string.IsNullOrWhiteSpace(fieldKey) ? "Запись" : fieldKey),
            UserMessage = message,
            SourceRuleLabel = "Проверка записи",
            TechnicalDetails = technicalDetails,
            CanFocusField = field != null
        });
    }

    private void SuggestIdFromDisplayName(string text)
    {
        if (SelectedDefinition != null) return;
        var slug = Transliterate(text);
        if (string.IsNullOrWhiteSpace(slug)) slug = "definition";
        Name = slug;
        ShortCode = slug;
    }

    private static string Transliterate(string text)
    {
        var map = new Dictionary<char, string>
        {
            ['а']="a", ['б']="b", ['в']="v", ['г']="g", ['д']="d", ['е']="e", ['ё']="e", ['ж']="zh", ['з']="z", ['и']="i", ['й']="y",
            ['к']="k", ['л']="l", ['м']="m", ['н']="n", ['о']="o", ['п']="p", ['р']="r", ['с']="s", ['т']="t", ['у']="u", ['ф']="f",
            ['х']="h", ['ц']="c", ['ч']="ch", ['ш']="sh", ['щ']="sch", ['ъ']="", ['ы']="y", ['ь']="", ['э']="e", ['ю']="yu", ['я']="ya"
        };
        var sb = new StringBuilder();
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
            else if (map.TryGetValue(c, out var mapped)) sb.Append(mapped);
            else if (char.IsWhiteSpace(c) || c == '-' || c == '_') sb.Append('_');
        }
        return Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
    }

    private void RefreshCommandStates()
    {
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CloneCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ArchiveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RestoreCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ValidateCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PlayerPreviewCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LoadAuditCommand).RaiseCanExecuteChanged();
    }

    private void RebuildTypeFilters()
    {
        TypeFilters.Clear();
        TypeFilters.Add("Все типы");
        foreach (var category in Definitions.Select(x => x.Category).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            TypeFilters.Add(DefinitionLabels.Category(category));
        if (!TypeFilters.Contains(SelectedType)) SelectedType = "Все типы";
    }

    private bool VisibilityMatches(string visibility)
    {
        return SelectedVisibility switch
        {
            "Видно игрокам" => string.Equals(visibility, "player_visible", StringComparison.OrdinalIgnoreCase),
            "Публично" => string.Equals(visibility, "public", StringComparison.OrdinalIgnoreCase),
            "Только GM" => string.Equals(visibility, "gm_only", StringComparison.OrdinalIgnoreCase),
            "Скрыто" => string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private bool PhysiologyMatches(DefinitionRecordRow row)
    {
        if (!IsRaceFamilySelected || string.Equals(SelectedPhysiologyFilter, "Все особенности", StringComparison.OrdinalIgnoreCase)) return true;
        var tag = SelectedPhysiologyFilter switch
        {
            "Игровые" => "playable",
            "Базовые расы" => "base_race",
            "Гибриды" => "hybrid",
            "Крылатые" => "winged",
            "Естественная броня" => "natural_armor",
            "Особые чувства" => "special_sense",
            "Дикие" => "wild",
            _ => string.Empty
        };
        return string.IsNullOrWhiteSpace(tag) || Split(row.Tags).Any(value => string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));
    }

    private bool FamilyMatches(string category)
    {
        if (SelectedFamily == "Все") return true;
        if (SelectedFamily == "Мир, языки и знания") return WorldLoreCalendarDefinitionCategories.IsSupported(category);
        if (SelectedFamily == "Фракции, организации и экономика") return FactionOrganizationEconomyDefinitionCategories.IsSupported(category);
        if (SelectedFamily == "Технологии, рецепты и проекты") return TechnologyRecipeBlueprintProjectDefinitionCategories.IsSupported(category);
        if (SelectedFamily == "Расы") return IsIn(category, "race_definition", "subspecies_definition", "hybrid_definition", "hybrid_subtype_definition", "race_trait_definition", "race_equipment_fit_profile", "race_npc_reaction_rule", "race_language_grant", "race_knowledge_grant");
        if (SelectedFamily == "Характеристики") return IsIn(category, "attribute_definition", "subattribute_definition", "derived_stat_definition", "attribute_set_profile", "derived_stat_set_profile");
        if (SelectedFamily == "Навыки") return IsIn(category, "skill_definition", "skill_group_definition", "skill_roll_context_template", "skill_technique_definition");
        if (SelectedFamily == "Развитие") return IsIn(category, "development_node_definition", "development_requirement_definition", "development_reward_definition", "development_direction_definition", "development_hexagon_profile");
        if (SelectedFamily == "Проверки и бой") return IsIn(category, "resolution_profile", "ability_modifier_profile", "skill_mastery_profile", "modifier_category_profile", "advantage_policy", "difficulty_profile", "degree_of_success_profile", "attempt_gate_profile", "hit_resolution_profile", "penetration_damage_profile");
        return true;
    }

    private static bool IsIn(string value, params string[] values) => values.Contains(value, StringComparer.OrdinalIgnoreCase);
    private static bool Contains(string value, string query) => !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    private static string Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static string Join(IEnumerable<object> values) => string.Join(", ", values.Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)));
    private static IEnumerable<string> Split(string text) => (text ?? string.Empty).Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static object[] ReadArray(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return Array.Empty<object>();
        if (value is object[] array) return array;
        if (value is IEnumerable enumerable && value is not string) return enumerable.Cast<object>().ToArray();
        return Array.Empty<object>();
    }

    private static Dictionary<string, object> AsMap(object? value) => DefinitionProfileRow.AdminDefinitionEditorViewModelAsMap(value);

    private static Dictionary<string, object> ParseKeyValue(string text)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var line in Split(text))
        {
            var parts = line.Split(new[] { '=' }, 2);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0])) result[parts[0].Trim()] = parts[1].Trim();
        }
        return result;
    }

    private static string HumanizeFlatten(IDictionary<string, object> map, bool playerSafe = false)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["displayName"] = "Название",
            ["name"] = "Код",
            ["category"] = "Тип",
            ["shortCode"] = "Короткий код",
            ["publicTags"] = "Игровые теги",
            ["publicDescription"] = "Описание",
            ["visibilityRule"] = "Видимость",
            ["tags"] = "Теги",
            ["isValid"] = "Проверка",
            ["errors"] = "Ошибки",
            ["warnings"] = "Предупреждения",
            ["fields"] = "Поля",
            ["customFields"] = "Свойства"
        };
        var forbidden = new[] { "definitionId", "profileId", "name", "shortCode", "definitionType", "schemaVersion", "entityRevision", "serverOnlyData", "gmDescription", "audit", "command", "payload", "referenceIds", "updatedAtUtc", "visibilityRule" };
        var lines = new List<string>();
        foreach (var pair in map)
        {
            if (playerSafe && forbidden.Contains(pair.Key, StringComparer.OrdinalIgnoreCase)) continue;
            if (playerSafe && new[] { "categoryLabel", "family", "tags", "playerSafe" }.Contains(pair.Key, StringComparer.OrdinalIgnoreCase)) continue;
            if (!labels.TryGetValue(pair.Key, out var label) && (pair.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || pair.Key.Contains("Revision"))) continue;
            if (playerSafe && string.Equals(pair.Key, "playerFacts", StringComparison.OrdinalIgnoreCase))
            {
                var facts = ReadArray(map, pair.Key)
                    .Select(AsMap)
                    .Select(x =>
                    {
                        var factLabel = Get(x, "label");
                        var factValue = Get(x, "value");
                        return string.IsNullOrWhiteSpace(factLabel)
                            ? string.Empty
                            : "• " + factLabel + (string.IsNullOrWhiteSpace(factValue) ? string.Empty : ": " + factValue);
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();
                if (facts.Length > 0)
                {
                    lines.Add("Доступные игроку сведения:");
                    lines.AddRange(facts);
                }
                continue;
            }
            label ??= HumanizeKey(pair.Key);
            if (playerSafe && string.Equals(pair.Key, "category", StringComparison.OrdinalIgnoreCase))
                label = DefinitionLabels.Category(Convert.ToString(pair.Value) ?? string.Empty);
            if (pair.Value is object[] array)
            {
                var values = playerSafe && string.Equals(pair.Key, "publicTags", StringComparison.OrdinalIgnoreCase)
                    ? array.Select(x => Convert.ToString(x) ?? string.Empty).Where(IsPlayerSafeTag).ToArray()
                    : array.Select(x => Convert.ToString(x) ?? string.Empty).ToArray();
                if (values.Length > 0) lines.Add(label + ": " + string.Join(", ", values));
            }
            else if (pair.Value is IDictionary nested) lines.Add(label + ": " + HumanizeFlatten(AsMap(nested), playerSafe));
            else
            {
                var displayValue = Convert.ToString(pair.Value) ?? string.Empty;
                if (playerSafe && string.Equals(pair.Key, "category", StringComparison.OrdinalIgnoreCase))
                    displayValue = DefinitionLabels.Category(displayValue);
                lines.Add(label + ": " + displayValue);
            }
        }
        return string.Join(Environment.NewLine, lines.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string HumanizeKey(string key)
        => Regex.Replace(key, "([a-z])([A-Z])", "$1 $2").Replace("_", " ");

    private static bool IsPlayerSafeTag(string tag)
        => !tag.StartsWith("gm:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("server:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("hidden:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("foundation_", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("dev", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("test", StringComparison.OrdinalIgnoreCase)
           && !tag.Equals("character_foundation", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("acceptance", StringComparison.OrdinalIgnoreCase)
           && tag.IndexOf("0182", StringComparison.OrdinalIgnoreCase) < 0
           && !Regex.IsMatch(tag, @"^\d+(?:\.\d+)+$")
           && !tag.Equals("world_lore_calendar", StringComparison.OrdinalIgnoreCase)
           && !tag.Equals("faction_organization_economy", StringComparison.OrdinalIgnoreCase)
           && !tag.EndsWith("_definition", StringComparison.OrdinalIgnoreCase);
}

public sealed class DefinitionProfileRow
{
    public DefinitionProfileRow(Dictionary<string, object> map)
    {
        ProfileId = Get(map, "profileId");
        Category = Get(map, "category");
        DisplayName = FirstNonEmpty(Get(map, "displayName"), CategoryLabel);
        Description = Get(map, "description");
        StorageMode = Get(map, "storageMode");
        Fields = ReadArray(map, "fieldSchemas").Select(x => new DefinitionFieldRow(AsMap(x))).ToList();
    }

    public string ProfileId { get; }
    public string Category { get; }
    public string CategoryLabel => DefinitionLabels.Category(Category);
    public string DisplayName { get; }
    public string Description { get; }
    public string StorageMode { get; }
    public List<DefinitionFieldRow> Fields { get; }
    public override string ToString() => DisplayName;

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static object[] ReadArray(IDictionary<string, object> map, string key) => AdminDefinitionEditorViewModelReadArray(map, key);
    private static Dictionary<string, object> AsMap(object? value) => AdminDefinitionEditorViewModelAsMap(value);
    internal static object[] AdminDefinitionEditorViewModelReadArray(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return Array.Empty<object>();
        if (value is object[] array) return array;
        if (value is IEnumerable enumerable && value is not string) return enumerable.Cast<object>().ToArray();
        return Array.Empty<object>();
    }
    internal static Dictionary<string, object> AdminDefinitionEditorViewModelAsMap(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }
        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                if (!TryReadPair(item, out var key, out var mappedValue)) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = mappedValue ?? string.Empty;
            }
        }
        return result;
    }

    private static bool TryReadPair(object? value, out string key, out object? mappedValue)
    {
        key = string.Empty;
        mappedValue = null;
        if (value is DictionaryEntry entry)
        {
            key = Convert.ToString(entry.Key) ?? string.Empty;
            mappedValue = entry.Value;
            return !string.IsNullOrWhiteSpace(key);
        }
        if (value is IDictionary pair)
        {
            object? keyValue = null;
            object? contentValue = null;
            var hasKey = false;
            var hasValue = false;
            foreach (DictionaryEntry pairEntry in pair)
            {
                var pairKey = Convert.ToString(pairEntry.Key);
                if (string.Equals(pairKey, "Key", StringComparison.OrdinalIgnoreCase)) { keyValue = pairEntry.Value; hasKey = true; }
                else if (string.Equals(pairKey, "Value", StringComparison.OrdinalIgnoreCase)) { contentValue = pairEntry.Value; hasValue = true; }
            }
            if (hasKey && hasValue)
            {
                key = Convert.ToString(keyValue) ?? string.Empty;
                mappedValue = contentValue;
                return !string.IsNullOrWhiteSpace(key);
            }
        }
        if (value is IList list && list.Count == 2)
        {
            key = Convert.ToString(list[0]) ?? string.Empty;
            mappedValue = list[1];
            return !string.IsNullOrWhiteSpace(key);
        }
        return false;
    }
}

public sealed class DefinitionFieldRow
{
    public DefinitionFieldRow(Dictionary<string, object> map)
    {
        FieldName = Get(map, "fieldName");
        DisplayName = DefinitionLabels.Field(FirstNonEmpty(Get(map, "displayName"), FieldName));
        FieldType = Get(map, "fieldType");
        IsRequired = bool.TryParse(Get(map, "isRequired"), out var req) && req;
        IsPlayerVisible = bool.TryParse(Get(map, "isPlayerVisible"), out var visible) && visible;
        IsGmOnly = bool.TryParse(Get(map, "isGmOnly"), out var gm) && gm;
        IsServerOnly = bool.TryParse(Get(map, "isServerOnly"), out var server) && server;
        HelpText = Get(map, "helpText");
        ShortLabel = Get(map, "shortLabel");
        SectionKey = Get(map, "sectionKey");
        Placeholder = Get(map, "placeholder");
        var canonicalValues = ReadArray(map, "allowedValues").Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList();
        AllowedValues = string.Join(", ", canonicalValues);
        ReferenceCategory = Get(map, "referenceCategory");
        ReferenceTargetTypes = ReadArray(map, "referenceTargetTypes").Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList();
        ReferenceSelectionMode = Get(map, "referenceSelectionMode");
        SectionTitle = FirstNonEmpty(Get(map, "sectionTitle"), IsServerOnly ? "Технические сведения" : IsGmOnly ? "Сведения мастера" : "Правила и свойства");
        Minimum = FirstDecimal(Get(map, "minimum"), Get(map, "minValue"));
        Maximum = FirstDecimal(Get(map, "maximum"), Get(map, "maxValue"));
        Step = FirstDecimal(Get(map, "step")) ?? (FieldType == "Decimal" ? 0.1m : 1m);
        UnitLabel = Get(map, "unitLabel");
        AllowEmpty = !bool.TryParse(Get(map, "allowEmpty"), out var allowEmpty) || allowEmpty;
        IsMultiline = bool.TryParse(Get(map, "isMultiline"), out var multiline) && multiline;
        IsAdvanced = bool.TryParse(Get(map, "isAdvanced"), out var advanced) && advanced;
        IsReadOnly = bool.TryParse(Get(map, "isReadOnly"), out var readOnly) && readOnly;
        IsSecret = bool.TryParse(Get(map, "isSecret"), out var secret) && secret;
        IsSearchable = bool.TryParse(Get(map, "isSearchable"), out var searchable) && searchable;
        SupportsUnknownLegacyValue = !bool.TryParse(Get(map, "supportsUnknownLegacyValue"), out var supportsUnknown) || supportsUnknown;
        UnknownValuePolicy = FirstNonEmpty(Get(map, "unknownValuePolicy"), "PreserveAndWarn");
        EditorKind = NormalizeEditorKind(FirstNonEmpty(Get(map, "editorKind"), EditorKindFor(FieldType, IsMultiline)));
        DisplayOrder = int.TryParse(Get(map, "displayOrder"), out var order) ? order : 0;
        LocalizedValueLabels = ReadLocalizedLabels(map);
        AllowedValueOptions = canonicalValues
            .Select(value => new DefinitionValueOption(value, LocalizedValueLabels.TryGetValue(value, out var label) ? label : value.Replace('_', ' ')))
            .ToList();
    }

    public string FieldName { get; }
    public string DisplayName { get; }
    public string ShortLabel { get; }
    public string FieldType { get; }
    public bool IsRequired { get; }
    public bool IsPlayerVisible { get; }
    public bool IsGmOnly { get; }
    public bool IsServerOnly { get; }
    public string HelpText { get; }
    public string AllowedValues { get; }
    public string ReferenceCategory { get; }
    public IReadOnlyList<string> ReferenceTargetTypes { get; }
    public string ReferenceSelectionMode { get; }
    public string SectionKey { get; }
    public string SectionTitle { get; }
    public string EditorKind { get; }
    public string Placeholder { get; }
    public decimal? Minimum { get; }
    public decimal? Maximum { get; }
    public decimal Step { get; }
    public string UnitLabel { get; }
    public bool AllowEmpty { get; }
    public bool IsMultiline { get; }
    public bool IsAdvanced { get; }
    public bool IsReadOnly { get; }
    public bool IsSecret { get; }
    public bool IsSearchable { get; }
    public bool SupportsUnknownLegacyValue { get; }
    public string UnknownValuePolicy { get; }
    public int DisplayOrder { get; }
    public Dictionary<string, string> LocalizedValueLabels { get; }
    public IReadOnlyList<DefinitionValueOption> AllowedValueOptions { get; }
    public string RequiredLabel => IsRequired ? "обязательно" : "опционально";
    public string ReferenceLabel => string.IsNullOrWhiteSpace(ReferenceCategory) ? string.Empty : "Ссылка на " + DefinitionLabels.Category(ReferenceCategory);
    public string VisibilityLabel => IsServerOnly ? "Только для мастера" : IsGmOnly ? "Только для GM" : IsPlayerVisible ? "Игрокам" : "Скрыто";
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static object[] ReadArray(IDictionary<string, object> map, string key) => DefinitionProfileRow.AdminDefinitionEditorViewModelReadArray(map, key);
    private static decimal? FirstDecimal(params string[] values)
    {
        foreach (var value in values)
        {
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result)) return result;
            if (decimal.TryParse(value, out result)) return result;
        }
        return null;
    }

    private static string EditorKindFor(string fieldType, bool isMultiline)
        => isMultiline || fieldType == "LongText" || fieldType == "LocalizedText" ? "multiline_text" : fieldType == "Boolean" ? "toggle" : fieldType == "Enum" || fieldType == "VisibilityRule" ? "select" : fieldType == "Reference" ? "reference_picker" : fieldType == "ReferenceList" ? "reference_picker_multiple" : fieldType == "Integer" ? "integer" : fieldType == "Decimal" ? "decimal" : fieldType == "Tags" ? "tags" : "text";
    private static string NormalizeEditorKind(string editorKind)
    {
        var value = (editorKind ?? string.Empty).Trim();
        if (value.Equals("number", StringComparison.OrdinalIgnoreCase)) return "bounded_number";
        if (value.Equals("combo", StringComparison.OrdinalIgnoreCase) || value.Equals("combobox", StringComparison.OrdinalIgnoreCase)) return "select";
        if (value.Equals("boolean", StringComparison.OrdinalIgnoreCase) || value.Equals("checkbox", StringComparison.OrdinalIgnoreCase)) return "toggle";
        if (value.Equals("reference", StringComparison.OrdinalIgnoreCase)) return "reference_picker";
        if (value.Equals("reference_list", StringComparison.OrdinalIgnoreCase)) return "reference_picker_multiple";
        if (value.Equals("multiline", StringComparison.OrdinalIgnoreCase)) return "multiline_text";
        return string.IsNullOrWhiteSpace(value) ? "text" : value;
    }
    private static Dictionary<string, string> ReadLocalizedLabels(IDictionary<string, object> map)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (map.TryGetValue("localizedValueLabels", out var raw))
            foreach (var pair in DefinitionProfileRow.AdminDefinitionEditorViewModelAsMap(raw)) result[pair.Key] = Convert.ToString(pair.Value) ?? pair.Key;
        if (map.TryGetValue("optionLabels", out var optionLabels))
            foreach (var pair in DefinitionProfileRow.AdminDefinitionEditorViewModelAsMap(optionLabels)) result[pair.Key] = Convert.ToString(pair.Value) ?? pair.Key;
        return result;
    }
}

public sealed class ValidationIssueVm
{
    public string Severity { get; set; } = "Информация";
    public string FieldKey { get; set; } = string.Empty;
    public string FieldDisplayName { get; set; } = "Запись";
    public string UserMessage { get; set; } = string.Empty;
    public string SourceRuleLabel { get; set; } = string.Empty;
    public bool CanFocusField { get; set; }
    public string TechnicalDetails { get; set; } = string.Empty;
}

public sealed class DefinitionFieldEditVm : ViewModelBase
{
    private string _value;
    private bool _hasValidationError;
    private string _controlValidationMessage = string.Empty;
    private readonly Func<DefinitionFieldEditVm, IReadOnlyList<NriReferenceOption>> _searchReferenceOptions;
    private NriReferenceOption? _selectedReferenceOption;
    private string _referenceSearchText = string.Empty;
    private string _referencePickerValidation = string.Empty;

    public DefinitionFieldEditVm(DefinitionFieldRow schema, string value, Func<DefinitionFieldEditVm, IReadOnlyList<NriReferenceOption>>? searchReferenceOptions = null)
    {
        Schema = schema;
        _value = value;
        _searchReferenceOptions = searchReferenceOptions ?? (_ => Array.Empty<NriReferenceOption>());
        ValueOptions = Schema.AllowedValueOptions.ToList();
        if (!string.IsNullOrWhiteSpace(value) && !Schema.AllowedValueOptions.Any(x => string.Equals(x.Value, value, StringComparison.OrdinalIgnoreCase)))
            ValueOptions.Add(new DefinitionValueOption(value, "Неизвестное значение: " + value.Replace('_', ' '), true));
        ReferenceChips = new ObservableCollection<NriReferenceOption>();
        ReferenceOptions = new ObservableCollection<NriReferenceOption>();
        ReferenceSearchCommand = new RelayCommand(SearchReferences);
        AddReferenceCommand = new RelayCommand(AddSelectedReference, () => SelectedReferenceOption != null);
        ClearReferenceCommand = new RelayCommand(ClearReferences);
        RemoveReferenceCommand = new RelayCommand<NriReferenceOption>(RemoveReference);
        RebuildReferenceChips();
    }

    public event EventHandler? ValueChanged;
    public DefinitionFieldRow Schema { get; }
    public string FieldName => Schema.FieldName;
    public string DisplayName => Schema.DisplayName;
    public string ShortLabel => string.IsNullOrWhiteSpace(Schema.ShortLabel) ? DisplayName : Schema.ShortLabel;
    public string FieldType => Schema.FieldType;
    public string VisibilityLabel => Schema.VisibilityLabel;
    public bool IsRequired => Schema.IsRequired;
    public string RequiredLabel => Schema.RequiredLabel;
    public string HelpText => string.IsNullOrWhiteSpace(Schema.HelpText) ? Schema.ReferenceLabel : Schema.HelpText;
    public string AllowedValues => Schema.AllowedValues;
    public string SectionTitle => Schema.SectionTitle;
    public string EditorKind => Schema.EditorKind;
    public string Placeholder => Schema.Placeholder;
    public decimal? Minimum => Schema.Minimum;
    public decimal? Maximum => Schema.Maximum;
    public decimal Step => Schema.Step;
    public int? IntegerMinimum => Schema.Minimum.HasValue ? (int)Math.Ceiling(Schema.Minimum.Value) : null;
    public int? IntegerMaximum => Schema.Maximum.HasValue ? (int)Math.Floor(Schema.Maximum.Value) : null;
    public int IntegerStep => Schema.Step <= 0 ? 1 : Math.Max(1, (int)Math.Round(Schema.Step));
    public int? MinimumMinutes => Schema.Minimum.HasValue ? (int)Math.Ceiling(Schema.Minimum.Value) : null;
    public int? MaximumMinutes => Schema.Maximum.HasValue ? (int)Math.Floor(Schema.Maximum.Value) : null;
    public string UnitLabel => Schema.UnitLabel;
    public bool IsAdvanced => Schema.IsAdvanced;
    public bool IsReadOnly => Schema.IsReadOnly;
    public int DisplayOrder => Schema.DisplayOrder;
    public string AutomationId => "AdminDefinitionEditor_Field_" + FieldName;
    public string LocalizedAllowedValues => string.Join(", ", Schema.AllowedValues.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Select(x => Schema.LocalizedValueLabels.TryGetValue(x, out var label) ? label : x));
    public List<DefinitionValueOption> ValueOptions { get; }
    public ObservableCollection<NriReferenceOption> ReferenceChips { get; }
    public ObservableCollection<NriReferenceOption> ReferenceOptions { get; }
    public ICommand ReferenceSearchCommand { get; }
    public ICommand AddReferenceCommand { get; }
    public ICommand ClearReferenceCommand { get; }
    public ICommand RemoveReferenceCommand { get; }
    public bool IsTextEditor => EditorKind == "text";
    public bool IsMultilineTextEditor => EditorKind == "multiline_text" && !IsLoreInformationVersionsEditor && !IsLawActionRulesEditor && !IsEconomicBandEditor && !IsTechnologyStructuredRowsEditor;
    public bool IsLoreInformationVersionsEditor => string.Equals(FieldName, "informationVersions", StringComparison.OrdinalIgnoreCase);
    public bool IsLawActionRulesEditor => string.Equals(FieldName, "actionRules", StringComparison.OrdinalIgnoreCase);
    public bool IsEconomicBandEditor => FieldName is "incomeBand" or "expenseBand" or "taxRentBand";
    public bool IsTechnologyStructuredRowsEditor => string.Equals(EditorKind, "technology_structured_rows", StringComparison.OrdinalIgnoreCase);
    public bool IsOptionEditor => EditorKind == "select";
    public bool IsSearchableOptionEditor => EditorKind == "searchable_select";
    public bool IsBooleanEditor => EditorKind == "toggle";
    public bool IsIntegerEditor => EditorKind == "integer";
    public bool IsDecimalEditor => EditorKind == "decimal";
    public bool IsBoundedNumberEditor => EditorKind == "bounded_number";
    public bool IsDateEditor => EditorKind == "date";
    public bool IsDateTimeEditor => EditorKind == "date_time";
    public bool IsDurationEditor => EditorKind == "duration";
    public bool IsReferenceEditor => EditorKind == "reference_picker";
    public bool IsMultiReferenceEditor => EditorKind == "reference_picker_multiple";
    public bool IsTagsEditor => EditorKind == "tags";
    public bool IsGeneratedIdentifierEditor => EditorKind == "generated_identifier";
    public string ReferenceDisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Value)) return "Не выбрано";
            var resolved = ReferenceChips.FirstOrDefault()?.DisplayName;
            return string.IsNullOrWhiteSpace(resolved) ? "Выбрана связанная запись" : resolved;
        }
    }
    public string ReferenceStatusText => string.IsNullOrWhiteSpace(Schema.ReferenceLabel) ? "Связь выбирается через поиск" : Schema.ReferenceLabel;
    public string ReferenceChipSummary
    {
        get
        {
            var count = ValueParts().Count();
            if (count == 0) return "Связи не выбраны";
            var labels = ReferenceChips.Select(x => x.DisplayName).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            return labels.Length == 0
                ? $"Выбрано связей: {count}"
                : $"Выбрано связей: {count} ({string.Join(", ", labels)})";
        }
    }
    public string ReferencePickerValidation
    {
        get => _referencePickerValidation;
        private set
        {
            if (_referencePickerValidation == value) return;
            _referencePickerValidation = value;
            Notify();
        }
    }
    public string ReferenceSearchText
    {
        get => _referenceSearchText;
        set
        {
            if (_referenceSearchText == value) return;
            _referenceSearchText = value;
            Notify();
        }
    }
    public NriReferenceOption? SelectedReferenceOption
    {
        get => _selectedReferenceOption;
        set
        {
            if (_selectedReferenceOption == value) return;
            _selectedReferenceOption = value;
            Notify();
            ((RelayCommand)AddReferenceCommand).RaiseCanExecuteChanged();
        }
    }
    public bool HasUnknownLegacyValue => !string.IsNullOrWhiteSpace(Value) && Schema.AllowedValueOptions.Count > 0 && !Schema.AllowedValueOptions.Any(x => string.Equals(x.Value, Value, StringComparison.OrdinalIgnoreCase));
    public string UnknownLegacyValue => HasUnknownLegacyValue ? Value : string.Empty;
    public string UnknownLegacyWarning => HasUnknownLegacyValue ? "Неизвестное legacy-значение сохранено без изменения. Выберите поддерживаемый вариант только если хотите заменить его." : string.Empty;
    public string ValidationHint => HasValidationError
        ? (string.IsNullOrWhiteSpace(ControlValidationMessage) ? "Исправьте значение поля." : ControlValidationMessage)
        : IsRequired && string.IsNullOrWhiteSpace(Value) ? "Заполните обязательное поле." : string.Empty;
    public bool HasValidationError
    {
        get => _hasValidationError;
        set
        {
            if (_hasValidationError == value) return;
            _hasValidationError = value;
            Notify();
            Notify(nameof(ValidationHint));
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public string ControlValidationMessage
    {
        get => _controlValidationMessage;
        set
        {
            if (_controlValidationMessage == value) return;
            _controlValidationMessage = value;
            Notify();
            Notify(nameof(ValidationHint));
        }
    }
    public bool? BooleanValue
    {
        get => bool.TryParse(Value, out var result) ? result : false;
        set => Value = value == true ? "true" : "false";
    }
    public string LoreInformationVersionsDisplayValue
    {
        get => IsLoreInformationVersionsEditor ? FormatLoreInformationVersions(Value, resolveReferences: true) : Value;
        set
        {
            if (!IsLoreInformationVersionsEditor)
            {
                Value = value;
                return;
            }
            Value = FormatLoreInformationVersions(value, resolveReferences: false);
        }
    }
    public string LawActionRulesDisplayValue
    {
        get => IsLawActionRulesEditor ? FormatLawActionRules(Value, resolveReferences: true) : Value;
        set
        {
            if (!IsLawActionRulesEditor)
            {
                Value = value;
                return;
            }
            Value = FormatLawActionRules(value, resolveReferences: false);
        }
    }
    public string EconomicBandDisplayValue
    {
        get => IsEconomicBandEditor ? FormatEconomicBand(Value, resolveReferences: true) : Value;
        set
        {
            if (!IsEconomicBandEditor)
            {
                Value = value;
                return;
            }
            Value = FormatEconomicBand(value, resolveReferences: false);
        }
    }
    public string TechnologyStructuredRowsDisplayValue
    {
        get => IsTechnologyStructuredRowsEditor ? FormatTechnologyStructuredRows(Value, resolveReferences: true) : Value;
        set
        {
            if (!IsTechnologyStructuredRowsEditor)
            {
                Value = value;
                return;
            }
            Value = FormatTechnologyStructuredRows(value, resolveReferences: false);
        }
    }
    public int? IntegerValue
    {
        get => int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : int.TryParse(Value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var current) ? current : null;
        set => Value = value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }
    public decimal? DecimalValue
    {
        get
        {
            var text = (Value ?? string.Empty).Trim();
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant)) return invariant;
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var current) ? current : null;
        }
        set => Value = value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }
    public DateTime? DateValue
    {
        get => DateTime.TryParse(Value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var value) ? value.Date : null;
        set => Value = value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;
    }
    public DateTime? DateTimeValue
    {
        get => DateTime.TryParse(Value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var value) || DateTime.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value) ? value : null;
        set => Value = value.HasValue ? value.Value.ToString("o", CultureInfo.InvariantCulture) : string.Empty;
    }
    public TimeSpan? DurationValue
    {
        get
        {
            if (int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)) return TimeSpan.FromMinutes(minutes);
            if (TimeSpan.TryParse(Value, CultureInfo.InvariantCulture, out var value)) return value;
            return null;
        }
        set => Value = value.HasValue ? value.Value.TotalMinutes.ToString("0", CultureInfo.InvariantCulture) : string.Empty;
    }
    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                Notify();
                Notify(nameof(BooleanValue));
                Notify(nameof(IntegerValue));
                Notify(nameof(DecimalValue));
                Notify(nameof(DateValue));
                Notify(nameof(DateTimeValue));
                Notify(nameof(DurationValue));
                Notify(nameof(LoreInformationVersionsDisplayValue));
                Notify(nameof(LawActionRulesDisplayValue));
                Notify(nameof(EconomicBandDisplayValue));
                Notify(nameof(TechnologyStructuredRowsDisplayValue));
                Notify(nameof(ReferenceDisplayName));
                Notify(nameof(HasUnknownLegacyValue));
                Notify(nameof(UnknownLegacyValue));
                Notify(nameof(UnknownLegacyWarning));
                Notify(nameof(ValidationHint));
                RebuildReferenceChips();
                Notify(nameof(ReferenceDisplayName));
                Notify(nameof(ReferenceChipSummary));
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void RebuildReferenceChips()
    {
        ReferenceChips.Clear();
        var parts = (Value ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        var previousSearchText = ReferenceSearchText;
        for (var i = 0; i < parts.Length; i++)
        {
            var displayName = ResolveReferenceDisplayName(parts[i], i + 1, previousSearchText);
            ReferenceChips.Add(new NriReferenceOption
            {
                Id = parts[i],
                DisplayName = displayName,
                TypeLabel = string.IsNullOrWhiteSpace(Schema.ReferenceLabel) ? "Справочник" : Schema.ReferenceLabel,
                StatusLabel = "Выбрано"
            });
        }
        ReferenceSearchText = previousSearchText;
    }

    private string ResolveReferenceDisplayName(string id, int ordinal, string previousSearchText)
    {
        try
        {
            ReferenceSearchText = id;
            var options = _searchReferenceOptions(this);
            var match = options
                .FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.CanonicalKey, id, StringComparison.OrdinalIgnoreCase));
            // The server performs the bounded exact canonical-key search. Older
            // records may store that key instead of the generated definition id.
            if (match == null && options.Count == 1) match = options[0];
            if (match != null && !string.IsNullOrWhiteSpace(match.DisplayName)) return match.DisplayName;
        }
        catch
        {
            // A missing reference should remain editable; the server still owns validation.
        }
        finally
        {
            ReferenceSearchText = previousSearchText;
        }
        return $"Связь {ordinal}";
    }

    private string FormatLoreInformationVersions(string source, bool resolveReferences)
    {
        var previousSearchText = ReferenceSearchText;
        try
        {
            return string.Join(Environment.NewLine,
                (source ?? string.Empty)
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line =>
                    {
                        var parts = line.Split('|').Select(x => x.Trim()).ToArray();
                        if (parts.Length == 0) return line;
                        parts[0] = resolveReferences
                            ? LocalizeLoreVersionKind(parts[0])
                            : CanonicalLoreVersionKind(parts[0]);
                        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                        {
                            ReferenceSearchText = parts[1];
                            var options = _searchReferenceOptions(this);
                            var match = resolveReferences
                                ? options.FirstOrDefault(x => string.Equals(x.Id, parts[1], StringComparison.OrdinalIgnoreCase))
                                : options.FirstOrDefault(x =>
                                    string.Equals(x.DisplayName, parts[1], StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(x.Id, parts[1], StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                                parts[1] = resolveReferences ? match.DisplayName : match.Id;
                        }
                        return string.Join(" | ", parts);
                    }));
        }
        finally
        {
            ReferenceSearchText = previousSearchText;
        }
    }

    private static string LocalizeLoreVersionKind(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "official" => "Официальная",
            "rumor" => "Слух",
            "hidden_truth" => "Скрытая истина",
            "partial" => "Частичная",
            "false" => "Ложная",
            "outdated" => "Устаревшая",
            _ => value.Replace('_', ' ')
        };

    private static string CanonicalLoreVersionKind(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "официальная" => "official",
            "слух" => "rumor",
            "скрытая истина" => "hidden_truth",
            "частичная" => "partial",
            "ложная" => "false",
            "устаревшая" => "outdated",
            _ => value.Trim().Replace(' ', '_').ToLowerInvariant()
        };

    private string FormatLawActionRules(string source, bool resolveReferences)
    {
        var previousSearchText = ReferenceSearchText;
        try
        {
            return string.Join(Environment.NewLine,
                (source ?? string.Empty)
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line =>
                    {
                        var parts = line.Split('|').Select(x => x.Trim()).ToArray();
                        if (parts.Length == 0) return line;
                        parts[0] = resolveReferences ? LocalizeLawAction(parts[0]) : CanonicalLawAction(parts[0]);
                        if (parts.Length > 1)
                            parts[1] = ResolveStructuredReference(parts[1], resolveReferences);
                        if (parts.Length > 2)
                            parts[2] = string.Join("; ", parts[2]
                                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => ResolveStructuredReference(x.Trim(), resolveReferences))
                                .Where(x => !string.IsNullOrWhiteSpace(x)));
                        if (parts.Length > 7)
                            parts[7] = resolveReferences ? LocalizeLawResult(parts[7]) : CanonicalLawResult(parts[7]);
                        return string.Join(" | ", parts);
                    }));
        }
        finally
        {
            ReferenceSearchText = previousSearchText;
        }
    }

    private string FormatEconomicBand(string source, bool resolveReferences)
    {
        var previousSearchText = ReferenceSearchText;
        try
        {
            return string.Join(Environment.NewLine,
                (source ?? string.Empty)
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line =>
                    {
                        var parts = line.Split('|').Select(x => x.Trim()).ToArray();
                        if (parts.Length > 2)
                            parts[2] = ResolveStructuredReference(parts[2], resolveReferences);
                        return string.Join(" | ", parts);
                    }));
        }
        finally
        {
            ReferenceSearchText = previousSearchText;
        }
    }

    private string FormatTechnologyStructuredRows(string source, bool resolveReferences)
    {
        var previousSearchText = ReferenceSearchText;
        try
        {
            return string.Join(Environment.NewLine,
                (source ?? string.Empty)
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line =>
                    {
                        var parts = line.Split('|').Select(x => x.Trim()).ToArray();
                        if (parts.Length == 0) return line;
                        var referenceColumn = TechnologyReferenceColumn(FieldName);
                        if (referenceColumn >= 0 && parts.Length > referenceColumn)
                        {
                            if (resolveReferences && parts[referenceColumn].StartsWith("unresolved:", StringComparison.OrdinalIgnoreCase))
                                parts[referenceColumn] = "Требует сопоставления";
                            else
                                parts[referenceColumn] = ResolveStructuredReference(parts[referenceColumn], resolveReferences);
                        }
                        if (FieldName == "stageRows")
                        {
                            if (parts.Length > 6) parts[6] = LocalizeBoolean0187(parts[6], resolveReferences);
                            if (parts.Length > 7) parts[7] = LocalizeBoolean0187(parts[7], resolveReferences);
                        }
                        if (FieldName is "requirementRows" or "repairRequirements")
                        {
                            if (parts.Length > 0) parts[0] = LocalizeRequirementKind0187(parts[0], resolveReferences);
                            if (parts.Length > 4) parts[4] = LocalizeBoolean0187(parts[4], resolveReferences);
                        }
                        if (FieldName == "componentRows" && parts.Length > 4)
                            parts[4] = LocalizeBoolean0187(parts[4], resolveReferences);
                        return string.Join(" | ", parts);
                    }));
        }
        finally
        {
            ReferenceSearchText = previousSearchText;
        }
    }

    private static int TechnologyReferenceColumn(string fieldName)
        => fieldName switch
        {
            "inputRows" or "catalystRows" or "outputRows" or "byproductRows" or "wasteRows" or "componentRows" => 0,
            "requirementRows" or "repairRequirements" => 1,
            _ => -1
        };

    private static string LocalizeBoolean0187(string value, bool toDisplay)
    {
        if (toDisplay)
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "Да"
                : string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ? "Нет" : value;
        return string.Equals(value, "да", StringComparison.OrdinalIgnoreCase) ? "true"
            : string.Equals(value, "нет", StringComparison.OrdinalIgnoreCase) ? "false" : value;
    }

    private static string LocalizeRequirementKind0187(string value, bool toDisplay)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Technology"] = "Технология", ["Knowledge"] = "Знание", ["Blueprint"] = "Чертёж",
            ["Method"] = "Метод", ["Recipe"] = "Рецепт", ["Skill"] = "Навык", ["Resource"] = "Ресурс",
            ["Item"] = "Предмет", ["MaterialQuality"] = "Качество материала", ["Specialist"] = "Специалист",
            ["PersonnelRole"] = "Роль персонала", ["Facility"] = "Площадка", ["ToolCapability"] = "Возможность инструмента",
            ["Money"] = "Деньги", ["Time"] = "Время", ["License"] = "Лицензия",
            ["LegalStatus"] = "Правовой статус", ["GMApproval"] = "Решение GM", ["CustomManual"] = "Ручное требование"
        };
        if (toDisplay) return labels.TryGetValue(value, out var label) ? label : value;
        var pair = labels.FirstOrDefault(x => string.Equals(x.Value, value, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(pair.Key) ? value : pair.Key;
    }

    private string ResolveStructuredReference(string value, bool resolveReferences)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        ReferenceSearchText = value;
        var options = _searchReferenceOptions(this);
        var match = resolveReferences
            ? options.FirstOrDefault(x => string.Equals(x.Id, value, StringComparison.OrdinalIgnoreCase))
            : options.FirstOrDefault(x =>
                string.Equals(x.DisplayName, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Id, value, StringComparison.OrdinalIgnoreCase));
        return match == null ? value : resolveReferences ? match.DisplayName : match.Id;
    }

    private static string LocalizeLawAction(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "buy" => "Покупка",
            "sell" => "Продажа",
            "own" => "Владение",
            "carry" => "Ношение",
            "transport" => "Перевозка",
            "use" => "Использование",
            "build" => "Строительство",
            "produce" => "Производство",
            "repair" => "Ремонт",
            _ => value.Replace('_', ' ')
        };

    private static string CanonicalLawAction(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "покупка" => "buy",
            "продажа" => "sell",
            "владение" => "own",
            "ношение" => "carry",
            "перевозка" => "transport",
            "использование" => "use",
            "строительство" => "build",
            "производство" => "produce",
            "ремонт" => "repair",
            _ => value.Trim().Replace(' ', '_').ToLowerInvariant()
        };

    private static string LocalizeLawResult(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "allowed" => "Разрешено",
            "registration_required" => "Нужна регистрация",
            "licensed" => "Нужна лицензия",
            "restricted" => "Ограничено",
            "military_only" => "Только военным",
            "prohibited" => "Запрещено",
            _ => value.Replace('_', ' ')
        };

    private static string CanonicalLawResult(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "разрешено" => "allowed",
            "нужна регистрация" => "registration_required",
            "нужна лицензия" => "licensed",
            "ограничено" => "restricted",
            "только военным" => "military_only",
            "запрещено" => "prohibited",
            _ => value.Trim().Replace(' ', '_').ToLowerInvariant()
        };

    private void SearchReferences()
    {
        ReferenceOptions.Clear();
        foreach (var option in _searchReferenceOptions(this)) ReferenceOptions.Add(option);
        ReferencePickerValidation = ReferenceOptions.Count == 0 ? "Совпадений нет." : string.Empty;
    }

    private void AddSelectedReference()
    {
        if (SelectedReferenceOption == null) return;
        if (IsMultiReferenceEditor)
        {
            var existing = ValueParts().ToList();
            if (!existing.Any(x => string.Equals(x, SelectedReferenceOption.Id, StringComparison.OrdinalIgnoreCase)))
                existing.Add(SelectedReferenceOption.Id);
            Value = string.Join(", ", existing);
        }
        else
        {
            Value = SelectedReferenceOption.Id;
        }

        ReferencePickerValidation = SelectedReferenceOption.IsArchived ? "Связанная запись в архиве." : string.Empty;
    }

    private void RemoveReference(NriReferenceOption? option)
    {
        if (option == null) return;
        var remaining = ValueParts()
            .Where(x => !string.Equals(x, option.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Value = string.Join(", ", remaining);
        ReferencePickerValidation = string.Empty;
    }

    private void ClearReferences()
    {
        Value = string.Empty;
        SelectedReferenceOption = null;
        ReferencePickerValidation = string.Empty;
    }

    private IEnumerable<string> ValueParts()
        => (Value ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x));
}

public sealed class DefinitionRecordRow
{
    public DefinitionRecordRow(Dictionary<string, object> map)
    {
        DefinitionId = Get(map, "definitionId");
        Category = Get(map, "category");
        Name = Get(map, "name");
        ShortCode = Get(map, "shortCode");
        VisibilityRule = Get(map, "visibilityRule");
        IsArchived = bool.TryParse(Get(map, "isArchived"), out var archived) && archived;
        RawDisplayName = FirstNonEmpty(Get(map, "displayName"), Get(map, "name"), "Без названия");
        DisplayName = HumanizeDisplayName(RawDisplayName, IsArchived, Category);
        UpdatedAt = Get(map, "updatedAtUtc");
        RuleSetId = Get(map, "ruleSetId");
        EntityRevision = int.TryParse(Get(map, "entityRevision"), out var revision) ? revision : 1;
        var tags = DefinitionProfileRow.AdminDefinitionEditorViewModelReadArray(map, "tags");
        var publicTags = DefinitionProfileRow.AdminDefinitionEditorViewModelReadArray(map, "publicTags");
        var systemTags = DefinitionProfileRow.AdminDefinitionEditorViewModelReadArray(map, "systemTags");
        Tags = Join((publicTags.Length > 0 ? publicTags : tags)
            .Where(x => IsPlayerSafeTag(Convert.ToString(x) ?? string.Empty)));
        SystemTags = Join(systemTags.Length > 0 ? systemTags : tags.Where(x => !IsPlayerSafeTag(Convert.ToString(x) ?? string.Empty)));
    }

    public string DefinitionId { get; }
    public string Category { get; }
    public string CategoryLabel => DefinitionLabels.Category(Category);
    public string ListSummary => CategoryLabel + " · " + StatusLabel;
    public string Name { get; }
    // Keep the server name searchable while the normal UI can still present a safe human label.
    public string RawDisplayName { get; }
    public string DisplayName { get; }
    public string ShortCode { get; }
    public string VisibilityRule { get; }
    public string VisibilityLabel => DefinitionLabels.Visibility(VisibilityRule);
    public bool IsArchived { get; }
    public string UpdatedAt { get; }
    public string RuleSetId { get; }
    public int EntityRevision { get; }
    public string Tags { get; }
    public string SystemTags { get; }
    public string PublicTags => Tags;
    public string StatusLabel => IsArchived ? "В архиве" : VisibilityLabel;
    private static string HumanizeDisplayName(string value, bool archived, string category)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Без названия";
        if (value.IndexOf("DO_NOT_LIST", StringComparison.OrdinalIgnoreCase) >= 0
            || value.StartsWith("ARCHIVED_DEFINITION_", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("FOUNDATION_", StringComparison.OrdinalIgnoreCase))
            return archived ? "Архивная запись" : DefinitionLabels.Category(category);
        return value;
    }
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static string Join(IEnumerable<object> values) => string.Join(", ", values.Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)));
    private static bool IsPlayerSafeTag(string tag)
        => !tag.StartsWith("gm:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("server:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("hidden:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("foundation_", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("dev", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("test", StringComparison.OrdinalIgnoreCase)
           && !tag.Equals("character_foundation", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("acceptance", StringComparison.OrdinalIgnoreCase)
           && tag.IndexOf("0182", StringComparison.OrdinalIgnoreCase) < 0
           && !Regex.IsMatch(tag, @"^\d+(?:\.\d+)+$")
           && !tag.Equals("world_lore_calendar", StringComparison.OrdinalIgnoreCase)
           && !tag.Equals("faction_organization_economy", StringComparison.OrdinalIgnoreCase)
           && !tag.EndsWith("_definition", StringComparison.OrdinalIgnoreCase);
}

public sealed class DefinitionValueOption
{
    public DefinitionValueOption(string value, string label, bool isLegacyUnknown = false) { Value = value; Label = label; IsLegacyUnknown = isLegacyUnknown; }
    public string Value { get; }
    public string Label { get; }
    public bool IsLegacyUnknown { get; }
    public string DisplayName => Label;
    public override string ToString() => Label;
}

internal static class DefinitionLabels
{
    private static readonly Dictionary<string, string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["race_definition"] = "Раса",
        ["subspecies_definition"] = "Подвид",
        ["hybrid_definition"] = "Гибрид",
        ["hybrid_subtype_definition"] = "Подтип гибрида",
        ["race_trait_definition"] = "Расовая особенность",
        ["attribute_definition"] = "Характеристика",
        ["subattribute_definition"] = "Подхарактеристика",
        ["derived_stat_definition"] = "Производный параметр",
        ["skill_definition"] = "Навык",
        ["skill_group_definition"] = "Группа навыков",
        ["development_node_definition"] = "Узел развития",
        ["development_requirement_definition"] = "Требование развития",
        ["development_reward_definition"] = "Награда развития",
        ["development_direction_definition"] = "Направление развития",
        ["development_hexagon_profile"] = "Профиль шестиугольника",
        ["resolution_profile"] = "Основная проверка",
        ["ability_modifier_profile"] = "Модификаторы характеристик",
        ["skill_mastery_profile"] = "Мастерство навыков",
        ["modifier_category_profile"] = "Категории модификаторов",
        ["advantage_policy"] = "Преимущество и помеха",
        ["difficulty_profile"] = "Шкала сложности",
        ["degree_of_success_profile"] = "Степени успеха",
        ["attempt_gate_profile"] = "Допуск к попытке",
        ["hit_resolution_profile"] = "Попадание и защита",
        ["penetration_damage_profile"] = "Пробитие и урон",
        ["skill_technique_definition"] = "Техника навыка",
        [WorldLoreCalendarDefinitionCategories.World] = "Мир",
        [WorldLoreCalendarDefinitionCategories.Location] = "Локация",
        [WorldLoreCalendarDefinitionCategories.Language] = "Язык",
        [WorldLoreCalendarDefinitionCategories.KnowledgeType] = "Тип знания",
        [WorldLoreCalendarDefinitionCategories.LoreEntry] = "Знание о мире",
        [WorldLoreCalendarDefinitionCategories.Calendar] = "Календарь",
        [WorldLoreCalendarDefinitionCategories.Era] = "Эпоха",
        [WorldLoreCalendarDefinitionCategories.EventType] = "Тип события",
        [FactionOrganizationEconomyDefinitionCategories.Faction] = "Фракция",
        [FactionOrganizationEconomyDefinitionCategories.Organization] = "Организация",
        [FactionOrganizationEconomyDefinitionCategories.Jurisdiction] = "Юрисдикция",
        [FactionOrganizationEconomyDefinitionCategories.Law] = "Закон",
        [FactionOrganizationEconomyDefinitionCategories.License] = "Лицензия",
        [FactionOrganizationEconomyDefinitionCategories.Currency] = "Валюта",
        [FactionOrganizationEconomyDefinitionCategories.Market] = "Рынок",
        [FactionOrganizationEconomyDefinitionCategories.BusinessProfile] = "Экономический профиль",
        [FactionOrganizationEconomyDefinitionCategories.ControlLevel] = "Уровень контроля",
        [FactionOrganizationEconomyDefinitionCategories.EconomicScale] = "Экономический масштаб",
        [FactionOrganizationEconomyDefinitionCategories.MarketOfferKind] = "Вид предложения",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.Technology] = "Технология",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod] = "Метод производства",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.Recipe] = "Рецепт",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint] = "Канонический чертёж",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.Facility] = "Тип площадки",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.ProjectTemplate] = "Шаблон проекта",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.TestProtocol] = "Протокол испытаний",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.Defect] = "Тип дефекта"
    };

    private static readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["description"] = "Описание",
        ["publicDescription"] = "Описание для игроков",
        ["rulesText"] = "Правила",
        ["category"] = "Категория",
        ["group"] = "Группа",
        ["cost"] = "Стоимость",
        ["level"] = "Уровень",
        ["requirements"] = "Требования",
        ["rewards"] = "Награды"
    };

    public static string Category(string value) => Categories.TryGetValue(value ?? string.Empty, out var label) ? label : value;
    public static string Field(string value)
    {
        if (string.Equals(value, "RuleSet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "ruleSetId", StringComparison.OrdinalIgnoreCase)) return "Профиль правил";
        return Fields.TryGetValue(value ?? string.Empty, out var label) ? label : value;
    }
    public static string Visibility(string value) => value switch
    {
        "player_visible" => "Видно игрокам",
        "public" => "Публично",
        "gm_only" => "Только GM",
        "hidden" => "Скрыто",
        _ => string.IsNullOrWhiteSpace(value) ? "Не задано" : value
    };
}
