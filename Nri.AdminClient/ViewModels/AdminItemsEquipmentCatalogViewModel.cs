using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminItemsEquipmentCatalogViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private EquipmentDefinitionListItemVm? _selectedDefinition;
    private EquipmentFamilyOptionVm? _selectedFamily;
    private string _searchText = string.Empty;
    private string _selectedArchiveFilter = "Активные";
    private string _selectedVisibilityFilter = "Все";
    private string _ruleSetFilter = string.Empty;
    private string _statusMessage = "Выберите семейство и загрузите справочник.";
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private string _referenceWarning = string.Empty;
    private bool _isLoading;
    private bool _hasRoutePermission;

    public AdminItemsEquipmentCatalogViewModel(CommandApi api)
    {
        _api = api;
        Families.Add(new EquipmentFamilyOptionVm(DefinitionCategoryIds.Resource, "Ресурсы", "Материалы, топливо, пища, медицина и реагенты."));
        Families.Add(new EquipmentFamilyOptionVm(DefinitionCategoryIds.Item, "Предметы", "Общая предметная основа и расходники."));
        Families.Add(new EquipmentFamilyOptionVm(DefinitionCategoryIds.DamageType, "Типы урона", "Расширяемые виды физического, магического и иного урона."));
        Families.Add(new EquipmentFamilyOptionVm(DefinitionCategoryIds.Weapon, "Оружие", "Оружие и вложенные профили атак."));
        Families.Add(new EquipmentFamilyOptionVm(DefinitionCategoryIds.Ammo, "Боеприпасы", "Совместимость, расход и модификаторы боеприпасов."));
        Families.Add(new EquipmentFamilyOptionVm(DefinitionCategoryIds.Armor, "Броня и щиты", "Защита, зоны тела и щитовые профили."));
        _selectedFamily = Families.FirstOrDefault();

        Editor = new CoreEquipmentDefinitionEditorVm();
        Editor.PropertyChanged += (_, _) => NotifyEditorState();

        RefreshCommand = new RelayCommand(Refresh);
        NewCommand = new RelayCommand(StartNew);
        SaveCommand = new RelayCommand(Save);
        CloneCommand = new RelayCommand(CloneSelected);
        ArchiveCommand = new RelayCommand(() => SetArchived(true));
        RestoreCommand = new RelayCommand(() => SetArchived(false));
        PreviewCommand = new RelayCommand(LoadPlayerPreview);
        AddAttackProfileCommand = new RelayCommand(() => Editor.AddAttackProfile(false));
        AddShieldProfileCommand = new RelayCommand(() => Editor.AddAttackProfile(true));
        RemoveAttackProfileCommand = new RelayCommand<EquipmentAttackProfileVm>(profile => Editor.RemoveAttackProfile(profile, false));
        RemoveShieldProfileCommand = new RelayCommand<EquipmentAttackProfileVm>(profile => Editor.RemoveAttackProfile(profile, true));
    }

    public ObservableCollection<EquipmentFamilyOptionVm> Families { get; } = new();
    public ObservableCollection<EquipmentDefinitionListItemVm> Definitions { get; } = new();
    public ObservableCollection<EquipmentReferenceOptionVm> References { get; } = new();
    public ObservableCollection<string> ArchiveFilters { get; } = new() { "Активные", "Архив", "Все" };
    public ObservableCollection<string> VisibilityFilters { get; } = new() { "Все", "Видно игрокам", "Только GM" };
    public CoreEquipmentDefinitionEditorVm Editor { get; }

    public ICommand RefreshCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CloneCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand AddAttackProfileCommand { get; }
    public ICommand AddShieldProfileCommand { get; }
    public ICommand RemoveAttackProfileCommand { get; }
    public ICommand RemoveShieldProfileCommand { get; }

    public EquipmentFamilyOptionVm? SelectedFamily
    {
        get => _selectedFamily;
        set
        {
            if (_selectedFamily == value) return;
            _selectedFamily = value;
            Notify();
            Notify(nameof(SelectedFamilyDescription));
            StartNew();
            Refresh();
        }
    }

    public string SelectedFamilyDescription => SelectedFamily?.Description ?? string.Empty;

    public EquipmentDefinitionListItemVm? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (_selectedDefinition == value) return;
            _selectedDefinition = value;
            Notify();
            if (value != null) LoadDefinition(value);
            NotifyEditorState();
        }
    }

    public string SearchText { get => _searchText; set { if (_searchText == value) return; _searchText = value ?? string.Empty; Notify(); } }
    public string SelectedArchiveFilter { get => _selectedArchiveFilter; set { if (_selectedArchiveFilter == value) return; _selectedArchiveFilter = value ?? "Активные"; Notify(); Refresh(); } }
    public string SelectedVisibilityFilter { get => _selectedVisibilityFilter; set { if (_selectedVisibilityFilter == value) return; _selectedVisibilityFilter = value ?? "Все"; Notify(); Refresh(); } }
    public string RuleSetFilter { get => _ruleSetFilter; set { if (_ruleSetFilter == value) return; _ruleSetFilter = value ?? string.Empty; Notify(); } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage == value) return; _statusMessage = value; Notify(); } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage == value) return; _errorMessage = value; Notify(); Notify(nameof(HasError)); } }
    public string SuccessMessage { get => _successMessage; private set { if (_successMessage == value) return; _successMessage = value; Notify(); Notify(nameof(HasSuccessMessage)); } }
    public string ReferenceWarning { get => _referenceWarning; private set { if (_referenceWarning == value) return; _referenceWarning = value; Notify(); Notify(nameof(HasReferenceWarning)); } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading == value) return; _isLoading = value; Notify(); Notify(nameof(RouteState)); NotifyEditorState(); } }
    public bool HasRoutePermission { get => _hasRoutePermission; set { if (_hasRoutePermission == value) return; _hasRoutePermission = value; Notify(); Notify(nameof(RouteState)); NotifyEditorState(); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasSuccessMessage => !string.IsNullOrWhiteSpace(SuccessMessage);
    public bool HasReferenceWarning => !string.IsNullOrWhiteSpace(ReferenceWarning);
    public bool HasUnsavedChanges => Editor.HasUnsavedChanges;
    public bool HasValidationIssues => Editor.ValidationErrors.Count > 0;
    public string ValidationSummary => string.Join(Environment.NewLine, Editor.ValidationErrors);
    public bool CanSaveDraft => HasRoutePermission && !IsLoading && !HasValidationIssues;
    public bool CanArchive => SelectedDefinition != null && !SelectedDefinition.IsArchived;
    public bool CanRestore => SelectedDefinition != null && SelectedDefinition.IsArchived;
    public string PermissionState => HasRoutePermission ? "Раздел доступен." : "Войдите администратором, чтобы редактировать справочник.";
    public string RouteState => !HasRoutePermission ? "permission"
        : IsLoading ? "loading"
        : HasError ? "error"
        : Definitions.Count == 0 ? "empty"
        : SelectedDefinition == null && string.IsNullOrWhiteSpace(Editor.DefinitionId) ? "no-selection"
        : "content";

    public void Refresh()
    {
        if (IsLoading || SelectedFamily == null || !HasRoutePermission) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        try
        {
            LoadReferences();
            var payload = new Dictionary<string, object>
            {
                ["family"] = SelectedFamily.Id,
                ["search"] = SearchText,
                ["ruleSetId"] = RuleSetFilter,
                ["includeArchived"] = !string.Equals(SelectedArchiveFilter, "Активные", StringComparison.OrdinalIgnoreCase)
            };
            var response = _api.CoreEquipmentAdminList(payload);
            EnsureOk(response);
            var rows = ReadList(response.Payload, "items")
                .Select(EquipmentDefinitionListItemVm.FromMap)
                .Where(FilterArchive)
                .Where(FilterVisibility)
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Definitions.Clear();
            foreach (var row in rows) Definitions.Add(row);
            StatusMessage = $"Загружено: {rows.Count}. Источник: unified_definitions.";
            ClientLogService.Instance.Info($"admin.equipmentDefinitions.load.done family={SelectedFamily.Id} count={rows.Count}");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Не удалось загрузить справочник.";
            ClientLogService.Instance.Error("admin.equipmentDefinitions.load.error", ex);
        }
        finally
        {
            IsLoading = false;
            Notify(nameof(RouteState));
        }
    }

    private void LoadReferences()
    {
        var response = _api.CoreEquipmentAdminReferences(new Dictionary<string, object>());
        EnsureOk(response);
        References.Clear();
        foreach (var row in ReadList(response.Payload, "items").Select(EquipmentReferenceOptionVm.FromMap))
        {
            References.Add(row);
        }
        Editor.ApplyReferences(References);
    }

    private void StartNew()
    {
        if (SelectedFamily == null) return;
        _selectedDefinition = null;
        Notify(nameof(SelectedDefinition));
        Editor.Reset(SelectedFamily.Id, References);
        ReferenceWarning = string.Empty;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        StatusMessage = $"Новая запись: {SelectedFamily.DisplayName}.";
        NotifyEditorState();
    }

    private void LoadDefinition(EquipmentDefinitionListItemVm item)
    {
        try
        {
            var response = _api.CoreEquipmentAdminGet(new Dictionary<string, object>
            {
                ["family"] = item.Family,
                ["definitionId"] = item.DefinitionId
            });
            EnsureOk(response);
            var map = ReadMap(response.Payload, "item");
            Editor.Load(map, References);
            var warnings = ReadStrings(response.Payload, "warnings");
            var broken = ReadStrings(response.Payload, "brokenReferences");
            ReferenceWarning = string.Join(Environment.NewLine, warnings.Concat(broken));
            StatusMessage = "Запись открыта.";
            ClientLogService.Instance.Info($"admin.equipmentDefinitions.open family={item.Family} name={item.DisplayName}");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Error("admin.equipmentDefinitions.open.error", ex);
        }
    }

    private void Save()
    {
        Editor.Validate();
        NotifyEditorState();
        if (!CanSaveDraft) return;
        try
        {
            ErrorMessage = string.Empty;
            var response = _api.CoreEquipmentAdminSave(Editor.ToPayload());
            EnsureOk(response);
            var map = ReadMap(response.Payload, "item");
            Editor.Load(map, References);
            ReferenceWarning = string.Join(Environment.NewLine, ReadStrings(response.Payload, "warnings"));
            Refresh();
            SelectById(Editor.DefinitionId);
            SuccessMessage = "Определение сохранено.";
            StatusMessage = "Изменения сохранены.";
            ClientLogService.Instance.Info($"admin.equipmentDefinitions.save.done family={Editor.Family} name={Editor.Name}");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Сохранение не выполнено.";
            ClientLogService.Instance.Error("admin.equipmentDefinitions.save.error", ex);
        }
    }

    private void CloneSelected()
    {
        if (SelectedDefinition == null) return;
        try
        {
            var response = _api.CoreEquipmentAdminClone(new Dictionary<string, object>
            {
                ["family"] = SelectedDefinition.Family,
                ["definitionId"] = SelectedDefinition.DefinitionId
            });
            EnsureOk(response);
            var id = S(ReadMap(response.Payload, "item"), "definitionId");
            Refresh();
            SelectById(id);
            SuccessMessage = "Копия создана.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private void SetArchived(bool archived)
    {
        if (SelectedDefinition == null) return;
        try
        {
            var response = _api.CoreEquipmentAdminSetArchived(new Dictionary<string, object>
            {
                ["family"] = SelectedDefinition.Family,
                ["definitionId"] = SelectedDefinition.DefinitionId,
                ["isArchived"] = archived
            });
            EnsureOk(response);
            Refresh();
            StartNew();
            SuccessMessage = archived ? "Запись архивирована." : "Запись восстановлена.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private void LoadPlayerPreview()
    {
        if (string.IsNullOrWhiteSpace(Editor.DefinitionId))
        {
            Editor.BuildLocalPreview();
            StatusMessage = "Показан предварительный локальный preview. Сохраните запись для server-safe проверки.";
            return;
        }
        try
        {
            var response = _api.CoreEquipmentPlayerGet(new Dictionary<string, object>
            {
                ["family"] = Editor.Family,
                ["definitionId"] = Editor.DefinitionId
            });
            EnsureOk(response);
            Editor.LoadPlayerPreview(ReadMap(response.Payload, "item"));
            StatusMessage = "Player-safe preview обновлён с сервера.";
        }
        catch (Exception ex)
        {
            Editor.PlayerPreviewTitle = "Запись не видна игрокам";
            Editor.PlayerPreviewDescription = ex.Message;
        }
    }

    private void SelectById(string definitionId)
    {
        SelectedDefinition = Definitions.FirstOrDefault(x => string.Equals(x.DefinitionId, definitionId, StringComparison.OrdinalIgnoreCase));
    }

    private bool FilterArchive(EquipmentDefinitionListItemVm item)
        => SelectedArchiveFilter == "Все"
           || (SelectedArchiveFilter == "Архив" && item.IsArchived)
           || (SelectedArchiveFilter == "Активные" && !item.IsArchived);

    private bool FilterVisibility(EquipmentDefinitionListItemVm item)
        => SelectedVisibilityFilter == "Все"
           || (SelectedVisibilityFilter == "Видно игрокам" && item.IsPlayerVisible)
           || (SelectedVisibilityFilter == "Только GM" && !item.IsPlayerVisible);

    private void NotifyEditorState()
    {
        Notify(nameof(HasUnsavedChanges));
        Notify(nameof(HasValidationIssues));
        Notify(nameof(ValidationSummary));
        Notify(nameof(CanSaveDraft));
        Notify(nameof(CanArchive));
        Notify(nameof(CanRestore));
        Notify(nameof(RouteState));
    }

    private static void EnsureOk(ResponseEnvelope response)
    {
        if (response.Status == ResponseStatus.Ok) return;
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? response.Status.ToString() : response.Message);
    }

    internal static IReadOnlyList<Dictionary<string, object>> ReadList(IDictionary<string, object> source, string key)
    {
        if (!source.TryGetValue(key, out var raw) || raw is not IEnumerable enumerable || raw is string) return Array.Empty<Dictionary<string, object>>();
        return enumerable.Cast<object>().Select(ToMap).Where(x => x.Count > 0).ToList();
    }

    internal static Dictionary<string, object> ReadMap(IDictionary<string, object> source, string key)
        => source.TryGetValue(key, out var raw) ? ToMap(raw) : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    internal static Dictionary<string, object> ToMap(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (raw is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
        }
        return result;
    }

    internal static string S(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var raw) ? Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    internal static int I(IDictionary<string, object> map, string key)
        => int.TryParse(S(map, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;

    internal static decimal D(IDictionary<string, object> map, string key)
        => decimal.TryParse(S(map, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    internal static bool B(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var raw) && (raw is bool value ? value : bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed) && parsed);

    internal static List<string> ReadStrings(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || raw == null) return new List<string>();
        if (raw is string text) return Split(text);
        if (raw is IEnumerable enumerable) return enumerable.Cast<object>().Select(x => Convert.ToString(x, CultureInfo.InvariantCulture) ?? string.Empty).Where(x => x.Length > 0).ToList();
        return new List<string>();
    }

    internal static List<string> Split(string text)
        => (text ?? string.Empty).Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

public sealed class EquipmentFamilyOptionVm
{
    public EquipmentFamilyOptionVm(string id, string displayName, string description)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
    }
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public override string ToString() => DisplayName;
}

public sealed class EquipmentDefinitionListItemVm
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CategoryDisplay { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string VisibilityDisplay => IsPlayerVisible ? "Видно игрокам" : "Только GM";
    public string StatusDisplay => IsArchived ? "В архиве" : "Активно";
    public string Summary => $"{CategoryDisplay} · {VisibilityDisplay} · {StatusDisplay}";

    public static EquipmentDefinitionListItemVm FromMap(Dictionary<string, object> map) => new()
    {
        DefinitionId = AdminItemsEquipmentCatalogViewModel.S(map, "definitionId"),
        Family = AdminItemsEquipmentCatalogViewModel.S(map, "family"),
        DisplayName = AdminItemsEquipmentCatalogViewModel.S(map, "displayName"),
        CategoryDisplay = EquipmentDefinitionLabels.Family(AdminItemsEquipmentCatalogViewModel.S(map, "family")),
        PublicDescription = AdminItemsEquipmentCatalogViewModel.S(map, "publicDescription"),
        IsArchived = AdminItemsEquipmentCatalogViewModel.B(map, "isArchived"),
        IsPlayerVisible = AdminItemsEquipmentCatalogViewModel.B(map, "isPlayerVisible")
    };
}

// Character v2 inventory still consumes the legacy catalog list shape.
// Keep this read-only adapter until that picker moves to the typed equipment endpoint.
public sealed class CatalogDefinitionUiItem
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }

    public static CatalogDefinitionUiItem FromMap(Dictionary<string, object> map) => new()
    {
        DefinitionId = AdminItemsEquipmentCatalogViewModel.S(map, "definitionId"),
        Code = FirstNonEmpty(
            AdminItemsEquipmentCatalogViewModel.S(map, "code"),
            AdminItemsEquipmentCatalogViewModel.S(map, "id"),
            AdminItemsEquipmentCatalogViewModel.S(map, "definitionId")),
        Category = AdminItemsEquipmentCatalogViewModel.S(map, "category"),
        DisplayName = FirstNonEmpty(
            AdminItemsEquipmentCatalogViewModel.S(map, "displayName"),
            AdminItemsEquipmentCatalogViewModel.S(map, "name"),
            AdminItemsEquipmentCatalogViewModel.S(map, "code")),
        IsPlayerVisible = AdminItemsEquipmentCatalogViewModel.B(map, "isPlayerVisible"),
        IsArchived = AdminItemsEquipmentCatalogViewModel.B(map, "isArchived")
    };

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class EquipmentReferenceOptionVm
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }
    public override string ToString() => DisplayName;

    public static EquipmentReferenceOptionVm FromMap(Dictionary<string, object> map) => new()
    {
        DefinitionId = AdminItemsEquipmentCatalogViewModel.S(map, "definitionId"),
        Family = AdminItemsEquipmentCatalogViewModel.S(map, "family"),
        DisplayName = AdminItemsEquipmentCatalogViewModel.S(map, "displayName"),
        Summary = AdminItemsEquipmentCatalogViewModel.S(map, "summary"),
        IsPlayerVisible = AdminItemsEquipmentCatalogViewModel.B(map, "isPlayerVisible"),
        IsArchived = AdminItemsEquipmentCatalogViewModel.B(map, "isArchived")
    };
}

public sealed class SelectableEquipmentReferenceVm : ViewModelBase
{
    private bool _isSelected;
    public SelectableEquipmentReferenceVm(EquipmentReferenceOptionVm reference, bool selected)
    {
        Reference = reference;
        _isSelected = selected;
    }
    public EquipmentReferenceOptionVm Reference { get; }
    public string DefinitionId => Reference.DefinitionId;
    public string DisplayName => Reference.DisplayName;
    public string Summary => Reference.Summary;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; Notify(); } }
}

public sealed class CoreEquipmentDefinitionEditorVm : ViewModelBase
{
    private bool _loading;
    private bool _hasUnsavedChanges;
    private string _definitionId = string.Empty;
    private string _family = DefinitionCategoryIds.Resource;
    private string _name = string.Empty;
    private string _profileCategory = string.Empty;
    private string _ruleSetId = RuleSetIds.FantasyNriDefault;
    private string _tagsText = string.Empty;
    private string _publicDescription = string.Empty;
    private string _gmDescription = string.Empty;
    private string _visibilityRule = VisibilityRuleIds.Public;
    private bool _isArchived;
    private string _playerPreviewTitle = "Player preview";
    private string _playerPreviewDescription = "Сохраните запись или обновите preview.";
    private string _playerPreviewFacts = string.Empty;

    public CoreEquipmentDefinitionEditorVm()
    {
        AttackProfiles.CollectionChanged += (_, _) => MarkDirty();
        ShieldAttackProfiles.CollectionChanged += (_, _) => MarkDirty();
    }

    public ObservableCollection<string> ValidationErrors { get; } = new();
    public ObservableCollection<EquipmentAttackProfileVm> AttackProfiles { get; } = new();
    public ObservableCollection<EquipmentAttackProfileVm> ShieldAttackProfiles { get; } = new();
    public ObservableCollection<EquipmentReferenceOptionVm> SkillOptions { get; } = new();
    public ObservableCollection<EquipmentReferenceOptionVm> SubAttributeOptions { get; } = new();
    public ObservableCollection<EquipmentReferenceOptionVm> DamageTypeOptions { get; } = new();
    public ObservableCollection<SelectableEquipmentReferenceVm> AmmoOptions { get; } = new();
    public ObservableCollection<SelectableEquipmentReferenceVm> RequiredSkillOptions { get; } = new();
    public ObservableCollection<SelectableEquipmentReferenceVm> RequiredAttributeOptions { get; } = new();
    public ObservableCollection<SelectableEquipmentReferenceVm> BodyRequirementOptions { get; } = new();
    public ObservableCollection<SelectableEquipmentReferenceVm> AllowedWeaponOptions { get; } = new();
    public ObservableCollection<SelectableEquipmentReferenceVm> ForbiddenWeaponOptions { get; } = new();
    public ObservableCollection<SelectableEquipmentReferenceVm> ProtectedBodyZoneOptions { get; } = new();
    public ObservableCollection<SelectableEquipmentReferenceVm> BodyCompatibilityOptions { get; } = new();
    public ObservableCollection<SelectableEquipmentReferenceVm> DamageAdditionOptions { get; } = new();
    public ObservableCollection<SelectableEquipmentReferenceVm> DamageReplacementOptions { get; } = new();

    public bool HasUnsavedChanges { get => _hasUnsavedChanges; private set { if (_hasUnsavedChanges == value) return; _hasUnsavedChanges = value; Notify(); } }
    public string DefinitionId { get => _definitionId; private set => Set(ref _definitionId, value, nameof(DefinitionId), false); }
    public string Family { get => _family; private set { if (Set(ref _family, value, nameof(Family), false)) NotifyFamily(); } }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string ProfileCategory { get => _profileCategory; set => Set(ref _profileCategory, value); }
    public string RuleSetId { get => _ruleSetId; set => Set(ref _ruleSetId, value); }
    public string TagsText { get => _tagsText; set => Set(ref _tagsText, value); }
    public string PublicDescription { get => _publicDescription; set => Set(ref _publicDescription, value); }
    public string GMDescription { get => _gmDescription; set => Set(ref _gmDescription, value); }
    public string VisibilityRule { get => _visibilityRule; set { if (Set(ref _visibilityRule, value)) Notify(nameof(IsPlayerVisible)); } }
    public bool IsPlayerVisible { get => VisibilityRule == VisibilityRuleIds.Public || VisibilityRule == VisibilityRuleIds.PlayerVisible; set => VisibilityRule = value ? VisibilityRuleIds.Public : VisibilityRuleIds.GmOnly; }
    public bool IsArchived { get => _isArchived; private set => Set(ref _isArchived, value, nameof(IsArchived), false); }
    public string PlayerPreviewTitle { get => _playerPreviewTitle; set => Set(ref _playerPreviewTitle, value, nameof(PlayerPreviewTitle), false); }
    public string PlayerPreviewDescription { get => _playerPreviewDescription; set => Set(ref _playerPreviewDescription, value, nameof(PlayerPreviewDescription), false); }
    public string PlayerPreviewFacts { get => _playerPreviewFacts; set => Set(ref _playerPreviewFacts, value, nameof(PlayerPreviewFacts), false); }

    public bool IsResource => Family == DefinitionCategoryIds.Resource;
    public bool IsItem => Family == DefinitionCategoryIds.Item;
    public bool IsDamageType => Family == DefinitionCategoryIds.DamageType;
    public bool IsWeapon => Family == DefinitionCategoryIds.Weapon;
    public bool IsAmmo => Family == DefinitionCategoryIds.Ammo;
    public bool IsArmor => Family == DefinitionCategoryIds.Armor;
    public string FamilyDisplay => EquipmentDefinitionLabels.Family(Family);
    public string UnsavedSummary => HasUnsavedChanges ? "Есть несохранённые изменения." : "Изменения сохранены.";

    private string _unit = string.Empty;
    private string _physicalState = string.Empty;
    private decimal _massPerUnit;
    private decimal _volumePerUnit;
    private string _rarity = string.Empty;
    private bool _supportsQuality;
    private decimal _baseValue;
    private string _legality = string.Empty;
    private string _storageRequirements = string.Empty;
    public string Unit { get => _unit; set => Set(ref _unit, value); }
    public string PhysicalState { get => _physicalState; set => Set(ref _physicalState, value); }
    public decimal MassPerUnit { get => _massPerUnit; set => Set(ref _massPerUnit, value); }
    public decimal VolumePerUnit { get => _volumePerUnit; set => Set(ref _volumePerUnit, value); }
    public string Rarity { get => _rarity; set => Set(ref _rarity, value); }
    public bool SupportsQuality { get => _supportsQuality; set => Set(ref _supportsQuality, value); }
    public decimal BaseValue { get => _baseValue; set => Set(ref _baseValue, value); }
    public string Legality { get => _legality; set => Set(ref _legality, value); }
    public string StorageRequirements { get => _storageRequirements; set => Set(ref _storageRequirements, value); }

    private string _itemType = string.Empty;
    private decimal _mass;
    private string _size = string.Empty;
    private bool _stackable;
    private int _maxStack = 1;
    private int _durability;
    private string _quality = string.Empty;
    public string ItemType { get => _itemType; set => Set(ref _itemType, value); }
    public decimal Mass { get => _mass; set => Set(ref _mass, value); }
    public string Size { get => _size; set => Set(ref _size, value); }
    public bool Stackable { get => _stackable; set => Set(ref _stackable, value); }
    public int MaxStack { get => _maxStack; set => Set(ref _maxStack, value); }
    public int Durability { get => _durability; set => Set(ref _durability, value); }
    public string Quality { get => _quality; set => Set(ref _quality, value); }

    private string _nature = string.Empty;
    private string _classification = string.Empty;
    private string _resistanceTags = string.Empty;
    private string _vulnerabilityTags = string.Empty;
    private string _immunityTags = string.Empty;
    public string Nature { get => _nature; set => Set(ref _nature, value); }
    public string Classification { get => _classification; set => Set(ref _classification, value); }
    public string ResistanceTags { get => _resistanceTags; set => Set(ref _resistanceTags, value); }
    public string VulnerabilityTags { get => _vulnerabilityTags; set => Set(ref _vulnerabilityTags, value); }
    public string ImmunityTags { get => _immunityTags; set => Set(ref _immunityTags, value); }

    private string _scale = string.Empty;
    private string _weaponNatures = string.Empty;
    private string _range = string.Empty;
    private string _reloadRules = string.Empty;
    public string Scale { get => _scale; set => Set(ref _scale, value); }
    public string WeaponNatures { get => _weaponNatures; set => Set(ref _weaponNatures, value); }
    public string Range { get => _range; set => Set(ref _range, value); }
    public string ReloadRules { get => _reloadRules; set => Set(ref _reloadRules, value); }

    private string _ammoType = string.Empty;
    private string _caliber = string.Empty;
    private string _compatibilityTags = string.Empty;
    private string _requiredFireModes = string.Empty;
    private int _physicalPenetrationModifier;
    private int _armorPenetrationModifier;
    private int _magicPenetrationModifier;
    private int _moralePenetrationModifier;
    private string _consumptionModel = string.Empty;
    private string _chargeModel = string.Empty;
    private string _failureMetadata = string.Empty;
    public string AmmoType { get => _ammoType; set => Set(ref _ammoType, value); }
    public string Caliber { get => _caliber; set => Set(ref _caliber, value); }
    public string CompatibilityTags { get => _compatibilityTags; set => Set(ref _compatibilityTags, value); }
    public string RequiredFireModes { get => _requiredFireModes; set => Set(ref _requiredFireModes, value); }
    public int PhysicalPenetrationModifier { get => _physicalPenetrationModifier; set => Set(ref _physicalPenetrationModifier, value); }
    public int ArmorPenetrationModifier { get => _armorPenetrationModifier; set => Set(ref _armorPenetrationModifier, value); }
    public int MagicPenetrationModifier { get => _magicPenetrationModifier; set => Set(ref _magicPenetrationModifier, value); }
    public int MoralePenetrationModifier { get => _moralePenetrationModifier; set => Set(ref _moralePenetrationModifier, value); }
    public string ConsumptionModel { get => _consumptionModel; set => Set(ref _consumptionModel, value); }
    public string ChargeModel { get => _chargeModel; set => Set(ref _chargeModel, value); }
    public string FailureMetadata { get => _failureMetadata; set => Set(ref _failureMetadata, value); }

    private string _designedSize = string.Empty;
    private int _physicalDefense;
    private int _magicalDefense;
    private string _specialResistanceTags = string.Empty;
    private int _stealthPenalty;
    private int _noise;
    private string _concealability = string.Empty;
    private int _strengthRequirement;
    private bool _hasShieldProfile;
    public string DesignedSize { get => _designedSize; set => Set(ref _designedSize, value); }
    public int PhysicalDefense { get => _physicalDefense; set => Set(ref _physicalDefense, value); }
    public int MagicalDefense { get => _magicalDefense; set => Set(ref _magicalDefense, value); }
    public string SpecialResistanceTags { get => _specialResistanceTags; set => Set(ref _specialResistanceTags, value); }
    public int StealthPenalty { get => _stealthPenalty; set => Set(ref _stealthPenalty, value); }
    public int Noise { get => _noise; set => Set(ref _noise, value); }
    public string Concealability { get => _concealability; set => Set(ref _concealability, value); }
    public int StrengthRequirement { get => _strengthRequirement; set => Set(ref _strengthRequirement, value); }
    public bool HasShieldProfile { get => _hasShieldProfile; set => Set(ref _hasShieldProfile, value); }

    public void Reset(string family, IEnumerable<EquipmentReferenceOptionVm> references)
    {
        _loading = true;
        DefinitionId = string.Empty;
        Family = family;
        Name = string.Empty;
        ProfileCategory = string.Empty;
        RuleSetId = RuleSetIds.FantasyNriDefault;
        TagsText = string.Empty;
        PublicDescription = string.Empty;
        GMDescription = string.Empty;
        VisibilityRule = VisibilityRuleIds.Public;
        IsArchived = false;
        Unit = string.Empty;
        PhysicalState = string.Empty;
        MassPerUnit = 0;
        VolumePerUnit = 0;
        Rarity = string.Empty;
        SupportsQuality = false;
        BaseValue = 0;
        Legality = string.Empty;
        StorageRequirements = string.Empty;
        ItemType = string.Empty;
        Mass = 0;
        Size = string.Empty;
        Stackable = false;
        MaxStack = 1;
        Durability = 0;
        Quality = string.Empty;
        Nature = string.Empty;
        Classification = string.Empty;
        ResistanceTags = string.Empty;
        VulnerabilityTags = string.Empty;
        ImmunityTags = string.Empty;
        Scale = string.Empty;
        WeaponNatures = string.Empty;
        Range = string.Empty;
        ReloadRules = string.Empty;
        AmmoType = string.Empty;
        Caliber = string.Empty;
        CompatibilityTags = string.Empty;
        RequiredFireModes = string.Empty;
        PhysicalPenetrationModifier = 0;
        ArmorPenetrationModifier = 0;
        MagicPenetrationModifier = 0;
        MoralePenetrationModifier = 0;
        ConsumptionModel = string.Empty;
        ChargeModel = string.Empty;
        FailureMetadata = string.Empty;
        DesignedSize = string.Empty;
        PhysicalDefense = 0;
        MagicalDefense = 0;
        SpecialResistanceTags = string.Empty;
        StealthPenalty = 0;
        Noise = 0;
        Concealability = string.Empty;
        StrengthRequirement = 0;
        HasShieldProfile = false;
        AttackProfiles.Clear();
        ShieldAttackProfiles.Clear();
        ApplyReferences(references);
        PlayerPreviewTitle = "Player preview";
        PlayerPreviewDescription = "Сохраните запись или обновите preview.";
        PlayerPreviewFacts = string.Empty;
        ValidationErrors.Clear();
        _loading = false;
        HasUnsavedChanges = false;
        NotifyAll();
    }

    public void ApplyReferences(IEnumerable<EquipmentReferenceOptionVm> references)
    {
        var list = references.Where(x => !x.IsArchived).ToList();
        ReplaceOptions(SkillOptions, list.Where(x => x.Family == DefinitionCategoryIds.Skill));
        ReplaceOptions(SubAttributeOptions, list.Where(x => x.Family == DefinitionCategoryIds.SubAttribute));
        ReplaceOptions(DamageTypeOptions, list.Where(x => x.Family == DefinitionCategoryIds.DamageType));
        RebuildSelectable(AmmoOptions, list.Where(x => x.Family == DefinitionCategoryIds.Ammo), SelectedIds(AmmoOptions));
        RebuildSelectable(RequiredSkillOptions, list.Where(x => x.Family == DefinitionCategoryIds.Skill), SelectedIds(RequiredSkillOptions));
        RebuildSelectable(RequiredAttributeOptions, list.Where(x => x.Family == DefinitionCategoryIds.Attribute), SelectedIds(RequiredAttributeOptions));
        RebuildSelectable(BodyRequirementOptions, list.Where(x => x.Family == DefinitionCategoryIds.EquipmentSlot), SelectedIds(BodyRequirementOptions));
        RebuildSelectable(AllowedWeaponOptions, list.Where(x => x.Family == DefinitionCategoryIds.Weapon), SelectedIds(AllowedWeaponOptions));
        RebuildSelectable(ForbiddenWeaponOptions, list.Where(x => x.Family == DefinitionCategoryIds.Weapon), SelectedIds(ForbiddenWeaponOptions));
        RebuildSelectable(ProtectedBodyZoneOptions, list.Where(x => x.Family == DefinitionCategoryIds.EquipmentSlot), SelectedIds(ProtectedBodyZoneOptions));
        RebuildSelectable(BodyCompatibilityOptions, list.Where(x => x.Family == DefinitionCategoryIds.EquipmentSlot), SelectedIds(BodyCompatibilityOptions));
        RebuildSelectable(DamageAdditionOptions, list.Where(x => x.Family == DefinitionCategoryIds.DamageType), SelectedIds(DamageAdditionOptions));
        RebuildSelectable(DamageReplacementOptions, list.Where(x => x.Family == DefinitionCategoryIds.DamageType), SelectedIds(DamageReplacementOptions));
        foreach (var profile in AttackProfiles.Concat(ShieldAttackProfiles)) profile.ApplyReferences(SkillOptions, SubAttributeOptions, DamageTypeOptions);
    }

    public void Load(Dictionary<string, object> map, IEnumerable<EquipmentReferenceOptionVm> references)
    {
        _loading = true;
        DefinitionId = AdminItemsEquipmentCatalogViewModel.S(map, "definitionId");
        Family = AdminItemsEquipmentCatalogViewModel.S(map, "family");
        Name = AdminItemsEquipmentCatalogViewModel.S(map, "displayName");
        ProfileCategory = FirstNonEmpty(
            AdminItemsEquipmentCatalogViewModel.S(map, "resourceCategory"),
            AdminItemsEquipmentCatalogViewModel.S(map, "itemType"),
            AdminItemsEquipmentCatalogViewModel.S(map, "nature"),
            AdminItemsEquipmentCatalogViewModel.S(map, "weaponCategory"),
            AdminItemsEquipmentCatalogViewModel.S(map, "ammoType"),
            AdminItemsEquipmentCatalogViewModel.S(map, "armorCategory"));
        RuleSetId = AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "ruleSetIds").FirstOrDefault() ?? RuleSetIds.FantasyNriDefault;
        TagsText = string.Join(", ", AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "tags"));
        PublicDescription = AdminItemsEquipmentCatalogViewModel.S(map, "publicDescription");
        GMDescription = AdminItemsEquipmentCatalogViewModel.S(map, "gmDescription");
        VisibilityRule = AdminItemsEquipmentCatalogViewModel.S(map, "visibilityRule");
        IsArchived = AdminItemsEquipmentCatalogViewModel.B(map, "isArchived");
        Unit = AdminItemsEquipmentCatalogViewModel.S(map, "unit");
        PhysicalState = AdminItemsEquipmentCatalogViewModel.S(map, "physicalState");
        MassPerUnit = AdminItemsEquipmentCatalogViewModel.D(map, "massPerUnit");
        VolumePerUnit = AdminItemsEquipmentCatalogViewModel.D(map, "volumePerUnit");
        Rarity = AdminItemsEquipmentCatalogViewModel.S(map, "rarity");
        SupportsQuality = AdminItemsEquipmentCatalogViewModel.B(map, "supportsQuality");
        BaseValue = AdminItemsEquipmentCatalogViewModel.D(map, "baseValue");
        Legality = AdminItemsEquipmentCatalogViewModel.S(map, "legality");
        StorageRequirements = AdminItemsEquipmentCatalogViewModel.S(map, "storageRequirements");
        ItemType = AdminItemsEquipmentCatalogViewModel.S(map, "itemType");
        Mass = AdminItemsEquipmentCatalogViewModel.D(map, "mass");
        Size = AdminItemsEquipmentCatalogViewModel.S(map, "size");
        Stackable = AdminItemsEquipmentCatalogViewModel.B(map, "stackable");
        MaxStack = Math.Max(1, AdminItemsEquipmentCatalogViewModel.I(map, "maxStack"));
        Durability = AdminItemsEquipmentCatalogViewModel.I(map, "durability");
        Quality = AdminItemsEquipmentCatalogViewModel.S(map, "quality");
        Nature = AdminItemsEquipmentCatalogViewModel.S(map, "nature");
        Classification = AdminItemsEquipmentCatalogViewModel.S(map, "classification");
        ResistanceTags = string.Join(", ", AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "resistanceTags"));
        VulnerabilityTags = string.Join(", ", AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "vulnerabilityTags"));
        ImmunityTags = string.Join(", ", AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "immunityTags"));
        Scale = AdminItemsEquipmentCatalogViewModel.S(map, "scale");
        WeaponNatures = string.Join(", ", AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "weaponNatures"));
        Range = AdminItemsEquipmentCatalogViewModel.S(map, "range");
        ReloadRules = AdminItemsEquipmentCatalogViewModel.S(map, "reloadRules");
        AmmoType = AdminItemsEquipmentCatalogViewModel.S(map, "ammoType");
        Caliber = AdminItemsEquipmentCatalogViewModel.S(map, "caliber");
        CompatibilityTags = string.Join(", ", AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "compatibilityTags"));
        RequiredFireModes = string.Join(", ", AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "requiredFireModes"));
        PhysicalPenetrationModifier = AdminItemsEquipmentCatalogViewModel.I(map, "physicalPenetrationModifier");
        ArmorPenetrationModifier = AdminItemsEquipmentCatalogViewModel.I(map, "armorPenetrationModifier");
        MagicPenetrationModifier = AdminItemsEquipmentCatalogViewModel.I(map, "magicPenetrationModifier");
        MoralePenetrationModifier = AdminItemsEquipmentCatalogViewModel.I(map, "moralePenetrationModifier");
        ConsumptionModel = AdminItemsEquipmentCatalogViewModel.S(map, "consumptionModel");
        ChargeModel = AdminItemsEquipmentCatalogViewModel.S(map, "chargeModel");
        FailureMetadata = AdminItemsEquipmentCatalogViewModel.S(map, "failureMetadata");
        DesignedSize = AdminItemsEquipmentCatalogViewModel.S(map, "designedSize");
        PhysicalDefense = AdminItemsEquipmentCatalogViewModel.I(map, "physicalDefense");
        MagicalDefense = AdminItemsEquipmentCatalogViewModel.I(map, "magicalDefense");
        SpecialResistanceTags = string.Join(", ", AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "specialResistanceTags"));
        StealthPenalty = AdminItemsEquipmentCatalogViewModel.I(map, "stealthPenalty");
        Noise = AdminItemsEquipmentCatalogViewModel.I(map, "noise");
        Concealability = AdminItemsEquipmentCatalogViewModel.S(map, "concealability");
        StrengthRequirement = AdminItemsEquipmentCatalogViewModel.I(map, "strengthRequirement");
        HasShieldProfile = AdminItemsEquipmentCatalogViewModel.B(map, "hasShieldProfile");
        AttackProfiles.Clear();
        foreach (var profileMap in AdminItemsEquipmentCatalogViewModel.ReadList(map, "attackProfiles"))
        {
            AddLoadedProfile(AttackProfiles, EquipmentAttackProfileVm.FromMap(profileMap));
        }
        ShieldAttackProfiles.Clear();
        foreach (var profileMap in AdminItemsEquipmentCatalogViewModel.ReadList(map, "shieldAttackProfiles"))
        {
            AddLoadedProfile(ShieldAttackProfiles, EquipmentAttackProfileVm.FromMap(profileMap));
        }
        var refs = references.ToList();
        ApplyReferences(refs);
        Select(AmmoOptions, AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "ammoDefinitionIds"));
        Select(RequiredSkillOptions, AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "requiredSkillIds"));
        Select(RequiredAttributeOptions, AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "requiredAttributeIds"));
        Select(BodyRequirementOptions, AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "bodyRequirements"));
        Select(AllowedWeaponOptions, AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "allowedWeaponIds"));
        Select(ForbiddenWeaponOptions, AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "forbiddenWeaponIds"));
        Select(ProtectedBodyZoneOptions, AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "protectedBodyZones"));
        Select(BodyCompatibilityOptions, AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "bodyCompatibilityTags"));
        Select(DamageAdditionOptions, AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "damageTypeAdditions"));
        Select(DamageReplacementOptions, AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "damageTypeReplacements"));
        foreach (var profile in AttackProfiles.Concat(ShieldAttackProfiles)) profile.ResolveSelections();
        ValidationErrors.Clear();
        _loading = false;
        HasUnsavedChanges = false;
        NotifyAll();
    }

    public void AddAttackProfile(bool shield)
    {
        var profile = EquipmentAttackProfileVm.CreateDefault();
        profile.ApplyReferences(SkillOptions, SubAttributeOptions, DamageTypeOptions);
        profile.PropertyChanged += (_, _) => MarkDirty();
        (shield ? ShieldAttackProfiles : AttackProfiles).Add(profile);
        MarkDirty();
    }

    public void RemoveAttackProfile(EquipmentAttackProfileVm? profile, bool shield)
    {
        if (profile == null) return;
        (shield ? ShieldAttackProfiles : AttackProfiles).Remove(profile);
        MarkDirty();
    }

    public void Validate()
    {
        ValidationErrors.Clear();
        if (string.IsNullOrWhiteSpace(Name)) ValidationErrors.Add("Укажите название.");
        if (string.IsNullOrWhiteSpace(ProfileCategory)) ValidationErrors.Add($"Укажите поле «{EquipmentDefinitionLabels.RequiredCategory(Family)}».");
        if (IsResource && string.IsNullOrWhiteSpace(Unit)) ValidationErrors.Add("Укажите единицу измерения.");
        if (IsItem && MaxStack < 1) ValidationErrors.Add("Размер стека должен быть не меньше 1.");
        if (IsDamageType && string.IsNullOrWhiteSpace(Classification)) ValidationErrors.Add("Укажите классификацию урона.");
        if (IsWeapon)
        {
            if (AttackProfiles.Count == 0) ValidationErrors.Add("Добавьте хотя бы один профиль атаки.");
            foreach (var profile in AttackProfiles) profile.Validate(ValidationErrors);
        }
        if (IsArmor && HasShieldProfile)
        {
            foreach (var profile in ShieldAttackProfiles) profile.Validate(ValidationErrors);
        }
        Notify(nameof(ValidationErrors));
    }

    public Dictionary<string, object> ToPayload()
    {
        Validate();
        var payload = new Dictionary<string, object>
        {
            ["definitionId"] = DefinitionId,
            ["family"] = Family,
            ["isCreate"] = string.IsNullOrWhiteSpace(DefinitionId),
            ["name"] = Name,
            ["ruleSetId"] = RuleSetId,
            ["tags"] = AdminItemsEquipmentCatalogViewModel.Split(TagsText).Cast<object>().ToArray(),
            ["publicDescription"] = PublicDescription,
            ["gmDescription"] = GMDescription,
            ["visibilityRule"] = VisibilityRule,
            ["isPlayerVisible"] = IsPlayerVisible,
            ["resourceCategory"] = IsResource ? ProfileCategory : string.Empty,
            ["unit"] = Unit,
            ["physicalState"] = PhysicalState,
            ["massPerUnit"] = MassPerUnit,
            ["volumePerUnit"] = VolumePerUnit,
            ["rarity"] = Rarity,
            ["supportsQuality"] = SupportsQuality,
            ["baseValue"] = BaseValue,
            ["legality"] = Legality,
            ["storageRequirements"] = StorageRequirements,
            ["itemType"] = IsItem ? ProfileCategory : ItemType,
            ["mass"] = Mass,
            ["size"] = Size,
            ["stackable"] = Stackable,
            ["maxStack"] = MaxStack,
            ["durability"] = Durability,
            ["quality"] = Quality,
            ["bodyCompatibilityTags"] = SelectedIds(BodyCompatibilityOptions).Cast<object>().ToArray(),
            ["nature"] = IsDamageType ? ProfileCategory : Nature,
            ["classification"] = Classification,
            ["resistanceTags"] = AdminItemsEquipmentCatalogViewModel.Split(ResistanceTags).Cast<object>().ToArray(),
            ["vulnerabilityTags"] = AdminItemsEquipmentCatalogViewModel.Split(VulnerabilityTags).Cast<object>().ToArray(),
            ["immunityTags"] = AdminItemsEquipmentCatalogViewModel.Split(ImmunityTags).Cast<object>().ToArray(),
            ["weaponCategory"] = IsWeapon ? ProfileCategory : string.Empty,
            ["scale"] = Scale,
            ["weaponNatures"] = AdminItemsEquipmentCatalogViewModel.Split(WeaponNatures).Cast<object>().ToArray(),
            ["requiredSkillIds"] = SelectedIds(RequiredSkillOptions).Cast<object>().ToArray(),
            ["requiredAttributeIds"] = SelectedIds(RequiredAttributeOptions).Cast<object>().ToArray(),
            ["bodyRequirements"] = SelectedIds(BodyRequirementOptions).Cast<object>().ToArray(),
            ["range"] = Range,
            ["reloadRules"] = ReloadRules,
            ["ammoDefinitionIds"] = SelectedIds(AmmoOptions).Cast<object>().ToArray(),
            ["attackProfiles"] = AttackProfiles.Select(x => (object)x.ToPayload()).ToArray(),
            ["ammoType"] = IsAmmo ? ProfileCategory : AmmoType,
            ["caliber"] = Caliber,
            ["compatibilityTags"] = AdminItemsEquipmentCatalogViewModel.Split(CompatibilityTags).Cast<object>().ToArray(),
            ["allowedWeaponIds"] = SelectedIds(AllowedWeaponOptions).Cast<object>().ToArray(),
            ["forbiddenWeaponIds"] = SelectedIds(ForbiddenWeaponOptions).Cast<object>().ToArray(),
            ["requiredFireModes"] = AdminItemsEquipmentCatalogViewModel.Split(RequiredFireModes).Cast<object>().ToArray(),
            ["damageTypeAdditions"] = SelectedIds(DamageAdditionOptions).Cast<object>().ToArray(),
            ["damageTypeReplacements"] = SelectedIds(DamageReplacementOptions).Cast<object>().ToArray(),
            ["physicalPenetrationModifier"] = PhysicalPenetrationModifier,
            ["armorPenetrationModifier"] = ArmorPenetrationModifier,
            ["magicPenetrationModifier"] = MagicPenetrationModifier,
            ["moralePenetrationModifier"] = MoralePenetrationModifier,
            ["consumptionModel"] = ConsumptionModel,
            ["chargeModel"] = ChargeModel,
            ["failureMetadata"] = FailureMetadata,
            ["armorCategory"] = IsArmor ? ProfileCategory : string.Empty,
            ["protectedBodyZones"] = SelectedIds(ProtectedBodyZoneOptions).Cast<object>().ToArray(),
            ["designedSize"] = DesignedSize,
            ["physicalDefense"] = PhysicalDefense,
            ["magicalDefense"] = MagicalDefense,
            ["specialResistanceTags"] = AdminItemsEquipmentCatalogViewModel.Split(SpecialResistanceTags).Cast<object>().ToArray(),
            ["stealthPenalty"] = StealthPenalty,
            ["noise"] = Noise,
            ["concealability"] = Concealability,
            ["strengthRequirement"] = StrengthRequirement,
            ["hasShieldProfile"] = HasShieldProfile,
            ["shieldAttackProfiles"] = ShieldAttackProfiles.Select(x => (object)x.ToPayload()).ToArray()
        };
        return payload;
    }

    public void BuildLocalPreview()
    {
        PlayerPreviewTitle = string.IsNullOrWhiteSpace(Name) ? "Без названия" : Name;
        PlayerPreviewDescription = PublicDescription;
        PlayerPreviewFacts = $"{FamilyDisplay}; {EquipmentDefinitionLabels.RequiredCategory(Family)}: {ProfileCategory}";
    }

    public void LoadPlayerPreview(Dictionary<string, object> map)
    {
        PlayerPreviewTitle = AdminItemsEquipmentCatalogViewModel.S(map, "displayName");
        PlayerPreviewDescription = AdminItemsEquipmentCatalogViewModel.S(map, "publicDescription");
        PlayerPreviewFacts = string.Join(Environment.NewLine,
            AdminItemsEquipmentCatalogViewModel.ReadList(map, "playerFacts")
                .Select(fact => $"{AdminItemsEquipmentCatalogViewModel.S(fact, "label")}: {AdminItemsEquipmentCatalogViewModel.S(fact, "value")}"));
    }

    private void AddLoadedProfile(ObservableCollection<EquipmentAttackProfileVm> target, EquipmentAttackProfileVm profile)
    {
        profile.PropertyChanged += (_, _) => MarkDirty();
        target.Add(profile);
    }

    private void MarkDirty()
    {
        if (_loading) return;
        HasUnsavedChanges = true;
        Validate();
        Notify(nameof(UnsavedSummary));
    }

    private bool Set<T>(ref T field, T value, string? propertyName = null, bool dirty = true)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(propertyName);
        if (dirty) MarkDirty();
        return true;
    }

    private void NotifyFamily()
    {
        Notify(nameof(IsResource));
        Notify(nameof(IsItem));
        Notify(nameof(IsDamageType));
        Notify(nameof(IsWeapon));
        Notify(nameof(IsAmmo));
        Notify(nameof(IsArmor));
        Notify(nameof(FamilyDisplay));
    }

    private void NotifyAll()
    {
        foreach (var property in GetType().GetProperties()) Notify(property.Name);
    }

    private static void ReplaceOptions(ObservableCollection<EquipmentReferenceOptionVm> target, IEnumerable<EquipmentReferenceOptionVm> items)
    {
        target.Clear();
        foreach (var item in items.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)) target.Add(item);
    }

    private void RebuildSelectable(
        ObservableCollection<SelectableEquipmentReferenceVm> target,
        IEnumerable<EquipmentReferenceOptionVm> items,
        IEnumerable<string> selectedIds)
    {
        var selected = new HashSet<string>(selectedIds, StringComparer.OrdinalIgnoreCase);
        target.Clear();
        foreach (var item in items.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var option = new SelectableEquipmentReferenceVm(item, selected.Contains(item.DefinitionId));
            option.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SelectableEquipmentReferenceVm.IsSelected)) MarkDirty();
            };
            target.Add(option);
        }
    }

    private static IEnumerable<string> SelectedIds(IEnumerable<SelectableEquipmentReferenceVm> options)
        => options.Where(x => x.IsSelected).Select(x => x.DefinitionId);

    private static void Select(IEnumerable<SelectableEquipmentReferenceVm> options, IEnumerable<string> ids)
    {
        var selected = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        foreach (var option in options) option.IsSelected = selected.Contains(option.DefinitionId);
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

public sealed class EquipmentAttackProfileVm : ViewModelBase
{
    private string _profileId = Guid.NewGuid().ToString("N");
    private string _name = "Основная атака";
    private string _attackType = "standard";
    private int _actionCost = 1;
    private string _attackRollType = "skill";
    private string _skillDefinitionId = string.Empty;
    private string _subAttributeDefinitionId = string.Empty;
    private string _damageTypeDefinitionId = string.Empty;
    private EquipmentReferenceOptionVm? _selectedSkill;
    private EquipmentReferenceOptionVm? _selectedSubAttribute;
    private EquipmentReferenceOptionVm? _selectedDamageType;
    private int _accuracyModifier;
    private string _range = string.Empty;
    private string _damageExpression = "1d6";
    private int _physicalPenetration;
    private int _armorPenetration;
    private int _magicPenetration;
    private int _moralePenetration;
    private string _area = string.Empty;
    private string _fireMode = "single";
    private int _reloadCost;
    private int _ammoCost = 1;
    private bool _canReact;
    private bool _canReturnFire;
    private bool _canParry;
    private bool _canBlock;

    public ObservableCollection<EquipmentReferenceOptionVm> SkillOptions { get; } = new();
    public ObservableCollection<EquipmentReferenceOptionVm> SubAttributeOptions { get; } = new();
    public ObservableCollection<EquipmentReferenceOptionVm> DamageTypeOptions { get; } = new();

    public string ProfileId { get => _profileId; set => Set(ref _profileId, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string AttackType { get => _attackType; set => Set(ref _attackType, value); }
    public int ActionCost { get => _actionCost; set => Set(ref _actionCost, value); }
    public string AttackRollType { get => _attackRollType; set => Set(ref _attackRollType, value); }
    public EquipmentReferenceOptionVm? SelectedSkill { get => _selectedSkill; set { if (Set(ref _selectedSkill, value)) SkillDefinitionId = value?.DefinitionId ?? string.Empty; } }
    public EquipmentReferenceOptionVm? SelectedSubAttribute { get => _selectedSubAttribute; set { if (Set(ref _selectedSubAttribute, value)) SubAttributeDefinitionId = value?.DefinitionId ?? string.Empty; } }
    public EquipmentReferenceOptionVm? SelectedDamageType { get => _selectedDamageType; set { if (Set(ref _selectedDamageType, value)) DamageTypeDefinitionId = value?.DefinitionId ?? string.Empty; } }
    public string SkillDefinitionId { get => _skillDefinitionId; private set => Set(ref _skillDefinitionId, value); }
    public string SubAttributeDefinitionId { get => _subAttributeDefinitionId; private set => Set(ref _subAttributeDefinitionId, value); }
    public string DamageTypeDefinitionId { get => _damageTypeDefinitionId; private set => Set(ref _damageTypeDefinitionId, value); }
    public int AccuracyModifier { get => _accuracyModifier; set => Set(ref _accuracyModifier, value); }
    public string Range { get => _range; set => Set(ref _range, value); }
    public string DamageExpression { get => _damageExpression; set => Set(ref _damageExpression, value); }
    public int PhysicalPenetration { get => _physicalPenetration; set => Set(ref _physicalPenetration, value); }
    public int ArmorPenetration { get => _armorPenetration; set => Set(ref _armorPenetration, value); }
    public int MagicPenetration { get => _magicPenetration; set => Set(ref _magicPenetration, value); }
    public int MoralePenetration { get => _moralePenetration; set => Set(ref _moralePenetration, value); }
    public string Area { get => _area; set => Set(ref _area, value); }
    public string FireMode { get => _fireMode; set => Set(ref _fireMode, value); }
    public int ReloadCost { get => _reloadCost; set => Set(ref _reloadCost, value); }
    public int AmmoCost { get => _ammoCost; set => Set(ref _ammoCost, value); }
    public bool CanReact { get => _canReact; set => Set(ref _canReact, value); }
    public bool CanReturnFire { get => _canReturnFire; set => Set(ref _canReturnFire, value); }
    public bool CanParry { get => _canParry; set => Set(ref _canParry, value); }
    public bool CanBlock { get => _canBlock; set => Set(ref _canBlock, value); }

    public static EquipmentAttackProfileVm CreateDefault() => new();

    public static EquipmentAttackProfileVm FromMap(Dictionary<string, object> map) => new()
    {
        ProfileId = AdminItemsEquipmentCatalogViewModel.S(map, "profileId"),
        Name = AdminItemsEquipmentCatalogViewModel.S(map, "name"),
        AttackType = AdminItemsEquipmentCatalogViewModel.S(map, "attackType"),
        ActionCost = AdminItemsEquipmentCatalogViewModel.I(map, "actionCost"),
        AttackRollType = AdminItemsEquipmentCatalogViewModel.S(map, "attackRollType"),
        SkillDefinitionId = AdminItemsEquipmentCatalogViewModel.S(map, "skillDefinitionId"),
        SubAttributeDefinitionId = AdminItemsEquipmentCatalogViewModel.S(map, "subAttributeDefinitionId"),
        DamageTypeDefinitionId = AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "damageTypeDefinitionIds").FirstOrDefault() ?? string.Empty,
        AccuracyModifier = AdminItemsEquipmentCatalogViewModel.I(map, "accuracyModifier"),
        Range = AdminItemsEquipmentCatalogViewModel.S(map, "range"),
        DamageExpression = AdminItemsEquipmentCatalogViewModel.S(map, "damageExpression"),
        PhysicalPenetration = AdminItemsEquipmentCatalogViewModel.I(map, "physicalPenetration"),
        ArmorPenetration = AdminItemsEquipmentCatalogViewModel.I(map, "armorPenetration"),
        MagicPenetration = AdminItemsEquipmentCatalogViewModel.I(map, "magicPenetration"),
        MoralePenetration = AdminItemsEquipmentCatalogViewModel.I(map, "moralePenetration"),
        Area = AdminItemsEquipmentCatalogViewModel.S(map, "area"),
        FireMode = AdminItemsEquipmentCatalogViewModel.S(map, "fireMode"),
        ReloadCost = AdminItemsEquipmentCatalogViewModel.I(map, "reloadCost"),
        AmmoCost = AdminItemsEquipmentCatalogViewModel.I(map, "ammoCost"),
        CanReact = AdminItemsEquipmentCatalogViewModel.B(map, "canReact"),
        CanReturnFire = AdminItemsEquipmentCatalogViewModel.B(map, "canReturnFire"),
        CanParry = AdminItemsEquipmentCatalogViewModel.B(map, "canParry"),
        CanBlock = AdminItemsEquipmentCatalogViewModel.B(map, "canBlock")
    };

    public void ApplyReferences(
        IEnumerable<EquipmentReferenceOptionVm> skills,
        IEnumerable<EquipmentReferenceOptionVm> subAttributes,
        IEnumerable<EquipmentReferenceOptionVm> damageTypes)
    {
        Replace(SkillOptions, skills);
        Replace(SubAttributeOptions, subAttributes);
        Replace(DamageTypeOptions, damageTypes);
        ResolveSelections();
    }

    public void ResolveSelections()
    {
        _selectedSkill = SkillOptions.FirstOrDefault(x => x.DefinitionId == SkillDefinitionId);
        _selectedSubAttribute = SubAttributeOptions.FirstOrDefault(x => x.DefinitionId == SubAttributeDefinitionId);
        _selectedDamageType = DamageTypeOptions.FirstOrDefault(x => x.DefinitionId == DamageTypeDefinitionId);
        Notify(nameof(SelectedSkill));
        Notify(nameof(SelectedSubAttribute));
        Notify(nameof(SelectedDamageType));
    }

    public void Validate(ObservableCollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("У профиля атаки должно быть название.");
        if (string.IsNullOrWhiteSpace(DamageExpression)) errors.Add($"Профиль «{Name}»: укажите формулу урона.");
        if (SelectedSkill == null) errors.Add($"Профиль «{Name}»: выберите навык.");
        if (SelectedDamageType == null) errors.Add($"Профиль «{Name}»: выберите тип урона.");
    }

    public Dictionary<string, object> ToPayload() => new()
    {
        ["profileId"] = string.IsNullOrWhiteSpace(ProfileId) ? Guid.NewGuid().ToString("N") : ProfileId,
        ["name"] = Name,
        ["attackType"] = AttackType,
        ["actionCost"] = ActionCost,
        ["attackRollType"] = AttackRollType,
        ["skillDefinitionId"] = SkillDefinitionId,
        ["subAttributeDefinitionId"] = SubAttributeDefinitionId,
        ["accuracyModifier"] = AccuracyModifier,
        ["range"] = Range,
        ["damageExpression"] = DamageExpression,
        ["damageTypeDefinitionIds"] = string.IsNullOrWhiteSpace(DamageTypeDefinitionId) ? Array.Empty<object>() : new object[] { DamageTypeDefinitionId },
        ["physicalPenetration"] = PhysicalPenetration,
        ["armorPenetration"] = ArmorPenetration,
        ["magicPenetration"] = MagicPenetration,
        ["moralePenetration"] = MoralePenetration,
        ["area"] = Area,
        ["fireMode"] = FireMode,
        ["reloadCost"] = ReloadCost,
        ["ammoCost"] = AmmoCost,
        ["canReact"] = CanReact,
        ["canReturnFire"] = CanReturnFire,
        ["canParry"] = CanParry,
        ["canBlock"] = CanBlock
    };

    private bool Set<T>(ref T field, T value, string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(name);
        return true;
    }

    private static void Replace(ObservableCollection<EquipmentReferenceOptionVm> target, IEnumerable<EquipmentReferenceOptionVm> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }
}

internal static class EquipmentDefinitionLabels
{
    public static string Family(string family) => family switch
    {
        DefinitionCategoryIds.Resource => "Ресурс",
        DefinitionCategoryIds.Item => "Предмет",
        DefinitionCategoryIds.DamageType => "Тип урона",
        DefinitionCategoryIds.Weapon => "Оружие",
        DefinitionCategoryIds.Ammo => "Боеприпасы",
        DefinitionCategoryIds.Armor => "Броня или щит",
        _ => family
    };

    public static string RequiredCategory(string family) => family switch
    {
        DefinitionCategoryIds.Resource => "Категория ресурса",
        DefinitionCategoryIds.Item => "Тип предмета",
        DefinitionCategoryIds.DamageType => "Природа урона",
        DefinitionCategoryIds.Weapon => "Категория оружия",
        DefinitionCategoryIds.Ammo => "Тип боеприпасов",
        DefinitionCategoryIds.Armor => "Категория брони",
        _ => "Категория"
    };
}
