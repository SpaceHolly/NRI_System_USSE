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

public sealed class AdminMagicDefinitionsViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private MagicDefinitionFamilyOptionVm? _selectedFamily;
    private MagicDefinitionListItemVm? _selectedDefinition;
    private string _searchText = string.Empty;
    private string _archiveFilter = "Активные";
    private string _visibilityFilter = "Все";
    private string _statusMessage = "Выберите семейство и загрузите справочник.";
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private string _referenceWarning = string.Empty;
    private string _previewText = "Откройте предпросмотр, чтобы увидеть карточку для игрока.";
    private bool _isLoading;
    private bool _hasRoutePermission;

    public AdminMagicDefinitionsViewModel(CommandApi api)
    {
        _api = api;
        Editor = new MagicDefinitionEditorVm();
        Editor.Changed += NotifyEditorState;
        foreach (var option in MagicDefinitionLabels.Families()) Families.Add(option);
        _selectedFamily = Families.FirstOrDefault();
        RefreshCommand = new RelayCommand(Refresh);
        NewCommand = new RelayCommand(NewDefinition);
        SaveCommand = new RelayCommand(Save);
        CloneCommand = new RelayCommand(Clone);
        ArchiveCommand = new RelayCommand(() => SetArchived(true));
        RestoreCommand = new RelayCommand(() => SetArchived(false));
        PreviewCommand = new RelayCommand(Preview);
        AddResourceCostCommand = new RelayCommand(AddResourceCost);
        AddRitualStageCommand = new RelayCommand(() => { Editor.RitualStages.Add(new RitualStageEditorVm(Editor.MarkDirty)); Editor.MarkDirty(); });
    }

    public ObservableCollection<MagicDefinitionFamilyOptionVm> Families { get; } = new();
    public ObservableCollection<MagicDefinitionListItemVm> Definitions { get; } = new();
    public ObservableCollection<MagicReferenceOptionVm> References { get; } = new();
    public ObservableCollection<string> ArchiveFilters { get; } = new() { "Активные", "Архив", "Все" };
    public ObservableCollection<string> VisibilityFilters { get; } = new() { "Все", "Видно игрокам", "Только GM" };
    public ObservableCollection<MagicChoiceOptionVm> VisibilityOptions { get; } = new()
    {
        new(VisibilityRuleIds.Public, "Видно игрокам"),
        new(VisibilityRuleIds.GmOnly, "Только GM"),
        new(VisibilityRuleIds.HiddenUntilDiscovered, "Скрыто до открытия")
    };
    public MagicDefinitionEditorVm Editor { get; }

    public ICommand RefreshCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CloneCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand AddResourceCostCommand { get; }
    public ICommand AddRitualStageCommand { get; }

    public MagicDefinitionFamilyOptionVm? SelectedFamily
    {
        get => _selectedFamily;
        set
        {
            if (_selectedFamily == value) return;
            if (Editor.HasUnsavedChanges)
            {
                ErrorMessage = "Сохраните или отмените изменения перед сменой семейства.";
                return;
            }
            _selectedFamily = value;
            Notify();
            Notify(nameof(SelectedFamilyDescription));
            Refresh();
        }
    }

    public MagicDefinitionListItemVm? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (_selectedDefinition == value) return;
            if (Editor.HasUnsavedChanges && _selectedDefinition != null)
            {
                ErrorMessage = "Сохраните изменения перед выбором другой записи.";
                return;
            }
            _selectedDefinition = value;
            Notify();
            NotifyEditorState();
            if (value != null) LoadSelected();
        }
    }

    public string SelectedFamilyDescription => SelectedFamily?.Description ?? string.Empty;
    public string SearchText { get => _searchText; set { if (_searchText == value) return; _searchText = value ?? string.Empty; Notify(); } }
    public string SelectedArchiveFilter { get => _archiveFilter; set { if (_archiveFilter == value) return; _archiveFilter = value ?? "Активные"; Notify(); Refresh(); } }
    public string SelectedVisibilityFilter { get => _visibilityFilter; set { if (_visibilityFilter == value) return; _visibilityFilter = value ?? "Все"; Notify(); Refresh(); } }
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value ?? string.Empty; Notify(); } }
    public string ErrorMessage { get => _errorMessage; private set { _errorMessage = value ?? string.Empty; Notify(); Notify(nameof(HasError)); } }
    public string SuccessMessage { get => _successMessage; private set { _successMessage = value ?? string.Empty; Notify(); Notify(nameof(HasSuccess)); } }
    public string ReferenceWarning { get => _referenceWarning; private set { _referenceWarning = value ?? string.Empty; Notify(); Notify(nameof(HasReferenceWarning)); } }
    public string PreviewText { get => _previewText; private set { _previewText = value ?? string.Empty; Notify(); } }
    public bool IsLoading { get => _isLoading; private set { _isLoading = value; Notify(); NotifyEditorState(); } }
    public bool HasRoutePermission { get => _hasRoutePermission; set { _hasRoutePermission = value; Notify(); NotifyEditorState(); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);
    public bool HasReferenceWarning => !string.IsNullOrWhiteSpace(ReferenceWarning);
    public bool HasUnsavedChanges => Editor.HasUnsavedChanges;
    public bool CanSave => HasRoutePermission && !IsLoading && !Editor.HasValidationIssues;
    public bool CanClone => SelectedDefinition != null && !SelectedDefinition.IsArchived;
    public bool CanArchive => SelectedDefinition != null && !SelectedDefinition.IsArchived;
    public bool CanRestore => SelectedDefinition != null && SelectedDefinition.IsArchived;
    public string RouteState => !HasRoutePermission ? "Войдите администратором для редактирования." :
        IsLoading ? "Загрузка..." :
        Definitions.Count == 0 ? "В выбранном семействе записей пока нет." :
        $"Записей: {Definitions.Count}.";

    public void Refresh()
    {
        if (!HasRoutePermission) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        try
        {
            LoadReferences();
            var payload = new Dictionary<string, object>
            {
                ["family"] = SelectedFamily?.Id ?? string.Empty,
                ["search"] = SearchText,
                ["includeArchived"] = SelectedArchiveFilter != "Активные",
                ["visibility"] = SelectedVisibilityFilter == "Видно игрокам" ? VisibilityRuleIds.Public :
                    SelectedVisibilityFilter == "Только GM" ? VisibilityRuleIds.GmOnly : "all"
            };
            var response = _api.MagicDefinitionsAdminList(payload);
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }
            Definitions.Clear();
            foreach (var row in AdminItemsEquipmentCatalogViewModel.ReadList(response.Payload, "items"))
            {
                var item = MagicDefinitionListItemVm.FromMap(row);
                if (SelectedArchiveFilter == "Архив" && !item.IsArchived) continue;
                Definitions.Add(item);
            }
            StatusMessage = $"Загружено: {Definitions.Count}.";
            ClientLogService.Instance.Info($"admin.magicDefinitions.load.done family={SelectedFamily?.Id} count={Definitions.Count}");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось загрузить определения: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            Notify(nameof(RouteState));
        }
    }

    private void LoadReferences()
    {
        var response = _api.MagicDefinitionsAdminReferences(new Dictionary<string, object>());
        if (response.Status != ResponseStatus.Ok) return;
        References.Clear();
        foreach (var map in AdminItemsEquipmentCatalogViewModel.ReadList(response.Payload, "references"))
        {
            References.Add(MagicReferenceOptionVm.FromMap(map));
        }
    }

    private void LoadSelected()
    {
        if (SelectedDefinition == null) return;
        IsLoading = true;
        try
        {
            var response = _api.MagicDefinitionsAdminGet(new Dictionary<string, object>
            {
                ["family"] = SelectedDefinition.Family,
                ["definitionId"] = SelectedDefinition.DefinitionId
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }
            Editor.Load(AdminItemsEquipmentCatalogViewModel.ReadMap(response.Payload, "item"), References);
            ReferenceWarning = string.Join(Environment.NewLine,
                AdminItemsEquipmentCatalogViewModel.ReadList(response.Payload, "warnings").Select(x => Convert.ToString(x.Values.FirstOrDefault()) ?? string.Empty)
                    .Concat(ReadStrings(response.Payload, "warnings"))
                    .Concat(ReadStrings(response.Payload, "brokenReferences"))
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
            PreviewText = "Откройте предпросмотр для игрока после проверки полей.";
            StatusMessage = $"Открыто: {Editor.Name}.";
        }
        finally
        {
            IsLoading = false;
            NotifyEditorState();
        }
    }

    private void NewDefinition()
    {
        if (SelectedFamily == null) return;
        _selectedDefinition = null;
        Notify(nameof(SelectedDefinition));
        Editor.New(SelectedFamily.Id, References);
        PreviewText = "Новая запись ещё не сохранена.";
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        NotifyEditorState();
    }

    private void AddResourceCost()
    {
        var cost = new MagicResourceCostEditorVm(Editor.MarkDirty);
        foreach (var option in Editor.SingleResourceOptions)
        {
            cost.ResourceOptions.Add(option);
        }
        Editor.ResourceCosts.Add(cost);
        Editor.MarkDirty();
    }

    private void Save()
    {
        Editor.Validate();
        NotifyEditorState();
        if (!CanSave)
        {
            ErrorMessage = Editor.ValidationSummary;
            return;
        }
        var response = _api.MagicDefinitionsAdminSave(Editor.ToPayload());
        if (response.Status != ResponseStatus.Ok)
        {
            ErrorMessage = response.Message;
            return;
        }
        var map = AdminItemsEquipmentCatalogViewModel.ReadMap(response.Payload, "item");
        Editor.Load(map, References);
        ErrorMessage = string.Empty;
        SuccessMessage = response.Message;
        ReferenceWarning = string.Join(Environment.NewLine, ReadStrings(response.Payload, "warnings"));
        RefreshPreserving(map);
    }

    private void Clone()
    {
        if (SelectedDefinition == null) return;
        var response = _api.MagicDefinitionsAdminClone(new Dictionary<string, object>
        {
            ["family"] = SelectedDefinition.Family,
            ["definitionId"] = SelectedDefinition.DefinitionId
        });
        if (response.Status != ResponseStatus.Ok) { ErrorMessage = response.Message; return; }
        SuccessMessage = response.Message;
        Refresh();
    }

    private void SetArchived(bool archived)
    {
        if (SelectedDefinition == null) return;
        var response = _api.MagicDefinitionsAdminSetArchived(new Dictionary<string, object>
        {
            ["family"] = SelectedDefinition.Family,
            ["definitionId"] = SelectedDefinition.DefinitionId,
            ["isArchived"] = archived
        });
        if (response.Status != ResponseStatus.Ok) { ErrorMessage = response.Message; return; }
        Editor.MarkClean();
        _selectedDefinition = null;
        Notify(nameof(SelectedDefinition));
        SuccessMessage = response.Message;
        Refresh();
    }

    private void Preview()
    {
        if (string.IsNullOrWhiteSpace(Editor.DefinitionId))
        {
            PreviewText = "Сначала сохраните запись.";
            return;
        }
        var response = _api.MagicDefinitionsPlayerGet(new Dictionary<string, object>
        {
            ["family"] = Editor.Family,
            ["definitionId"] = Editor.DefinitionId
        });
        if (response.Status != ResponseStatus.Ok)
        {
            PreviewText = "Запись скрыта от игроков или содержит небезопасные связи.";
            return;
        }
        var item = AdminItemsEquipmentCatalogViewModel.ReadMap(response.Payload, "item");
        var lines = new List<string>
        {
            AdminItemsEquipmentCatalogViewModel.S(item, "displayName"),
            AdminItemsEquipmentCatalogViewModel.S(item, "publicDescription")
        };
        foreach (var fact in AdminItemsEquipmentCatalogViewModel.ReadList(item, "playerFacts"))
        {
            lines.Add($"{AdminItemsEquipmentCatalogViewModel.S(fact, "label")}: {AdminItemsEquipmentCatalogViewModel.S(fact, "value")}");
        }
        PreviewText = string.Join(Environment.NewLine, lines.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private void RefreshPreserving(Dictionary<string, object> map)
    {
        var id = AdminItemsEquipmentCatalogViewModel.S(map, "definitionId");
        Editor.MarkClean();
        Refresh();
        _selectedDefinition = Definitions.FirstOrDefault(x => string.Equals(x.DefinitionId, id, StringComparison.OrdinalIgnoreCase));
        Notify(nameof(SelectedDefinition));
        NotifyEditorState();
    }

    private void NotifyEditorState()
    {
        Notify(nameof(HasUnsavedChanges));
        Notify(nameof(CanSave));
        Notify(nameof(CanClone));
        Notify(nameof(CanArchive));
        Notify(nameof(CanRestore));
        Notify(nameof(RouteState));
    }

    private static IEnumerable<string> ReadStrings(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || raw == null) return Array.Empty<string>();
        if (raw is string text) return string.IsNullOrWhiteSpace(text) ? Array.Empty<string>() : new[] { text };
        if (raw is IEnumerable values) return values.Cast<object>().Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!);
        return Array.Empty<string>();
    }
}

public sealed class MagicDefinitionFamilyOptionVm
{
    public MagicDefinitionFamilyOptionVm(string id, string displayName, string description)
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

public sealed class MagicChoiceOptionVm
{
    public MagicChoiceOptionVm(string value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public string Value { get; }
    public string DisplayName { get; }
    public override string ToString() => DisplayName;
}

public sealed class MagicDefinitionListItemVm
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string FamilyDisplay => MagicDefinitionLabels.Family(Family);
    public string Summary => $"{FamilyDisplay} · {(IsPlayerVisible ? "Видно игрокам" : "Только GM")} · {(IsArchived ? "В архиве" : "Активно")}";
    public static MagicDefinitionListItemVm FromMap(Dictionary<string, object> map) => new()
    {
        DefinitionId = AdminItemsEquipmentCatalogViewModel.S(map, "definitionId"),
        Family = AdminItemsEquipmentCatalogViewModel.S(map, "family"),
        DisplayName = AdminItemsEquipmentCatalogViewModel.S(map, "displayName"),
        PublicDescription = AdminItemsEquipmentCatalogViewModel.S(map, "publicDescription"),
        IsArchived = AdminItemsEquipmentCatalogViewModel.B(map, "isArchived"),
        IsPlayerVisible = AdminItemsEquipmentCatalogViewModel.B(map, "isPlayerVisible")
    };
}

public sealed class MagicReferenceOptionVm
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }
    public override string ToString() => DisplayName;
    public static MagicReferenceOptionVm FromMap(Dictionary<string, object> map) => new()
    {
        DefinitionId = AdminItemsEquipmentCatalogViewModel.S(map, "definitionId"),
        Family = AdminItemsEquipmentCatalogViewModel.S(map, "family"),
        DisplayName = AdminItemsEquipmentCatalogViewModel.S(map, "displayName"),
        Summary = AdminItemsEquipmentCatalogViewModel.S(map, "summary"),
        IsPlayerVisible = AdminItemsEquipmentCatalogViewModel.B(map, "isPlayerVisible"),
        IsArchived = AdminItemsEquipmentCatalogViewModel.B(map, "isArchived")
    };
}

public sealed class SelectableMagicReferenceVm : ViewModelBase
{
    private readonly Action _changed;
    private bool _isSelected;
    public SelectableMagicReferenceVm(MagicReferenceOptionVm reference, bool selected, Action changed)
    {
        Reference = reference;
        _isSelected = selected;
        _changed = changed;
    }
    public MagicReferenceOptionVm Reference { get; }
    public string DefinitionId => Reference.DefinitionId;
    public string DisplayName => Reference.DisplayName;
    public string Summary => Reference.Summary;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; Notify(); _changed(); } }
}

public sealed class MagicResourceCostEditorVm : ViewModelBase
{
    private readonly Action _changed;
    private MagicReferenceOptionVm? _selectedResource;
    private decimal _amount;
    private string _requirement = string.Empty;
    public MagicResourceCostEditorVm(Action changed) => _changed = changed;
    public ObservableCollection<MagicReferenceOptionVm> ResourceOptions { get; } = new();
    public MagicReferenceOptionVm? SelectedResource { get => _selectedResource; set { _selectedResource = value; Notify(); _changed(); } }
    public decimal Amount { get => _amount; set { _amount = value; Notify(); _changed(); } }
    public string Requirement { get => _requirement; set { _requirement = value ?? string.Empty; Notify(); _changed(); } }
}

public sealed class RitualStageEditorVm : ViewModelBase
{
    private readonly Action _changed;
    private string _name = string.Empty;
    private string _duration = string.Empty;
    private string _requirements = string.Empty;
    public RitualStageEditorVm(Action changed) => _changed = changed;
    public string Name { get => _name; set { _name = value ?? string.Empty; Notify(); _changed(); } }
    public string Duration { get => _duration; set { _duration = value ?? string.Empty; Notify(); _changed(); } }
    public string Requirements { get => _requirements; set { _requirements = value ?? string.Empty; Notify(); _changed(); } }
}

public sealed class MagicDefinitionEditorVm : ViewModelBase
{
    private bool _loading;
    private bool _dirty;
    private string _definitionId = string.Empty;
    private string _family = DefinitionCategoryIds.MagicMethod;
    private string _name = string.Empty;
    private string _profileCategory = string.Empty;
    private string _ruleSetId = RuleSetIds.FantasyNriDefault;
    private string _tagsText = string.Empty;
    private string _publicDescription = string.Empty;
    private string _gmDescription = string.Empty;
    private string _visibilityRule = VisibilityRuleIds.Public;
    private string _resourceModel = string.Empty;
    private string _preparationModel = string.Empty;
    private string _castingModel = string.Empty;
    private string _defaultRiskProfile = string.Empty;
    private string _legality = string.Empty;
    private string _directionKind = string.Empty;
    private string _rarity = string.Empty;
    private int _tier;
    private string _checkType = string.Empty;
    private string _rollProfile = string.Empty;
    private string _castingTime = string.Empty;
    private int _actionCost;
    private string _preparationRequirements = string.Empty;
    private string _range = string.Empty;
    private string _targetModel = string.Empty;
    private string _area = string.Empty;
    private string _duration = string.Empty;
    private bool _requiresConcentration;
    private bool _requiresChanneling;
    private bool _isInterruptible;
    private string _failureMetadata = string.Empty;
    private string _riskMetadata = string.Empty;
    private string _license = string.Empty;
    private string _triggerType = string.Empty;
    private string _activationRequirements = string.Empty;
    private string _persistence = string.Empty;
    private int _charges;
    private string _interruptionRules = string.Empty;
    private string _destructionRules = string.Empty;
    private decimal _arcanaCost;
    private string _channelTime = string.Empty;
    private string _overload = string.Empty;
    private string _stability = string.Empty;
    private string _requirements = string.Empty;
    private int _requiredParticipants = 1;
    private string _participantRoles = string.Empty;
    private string _executionDuration = string.Empty;
    private string _locationRequirements = string.Empty;
    private string _failureConsequences = string.Empty;
    private string _resultDuration = string.Empty;
    private string _effectKind = "custom_manual";
    private string _targetSelector = string.Empty;
    private string _timing = "immediate";
    private string _operation = string.Empty;
    private string _valueExpression = string.Empty;
    private string _interval = string.Empty;
    private string _stackingBehavior = string.Empty;
    private string _sourceRestrictions = string.Empty;
    private string _manualResolution = string.Empty;
    private string _severity = string.Empty;
    private string _durationModel = string.Empty;
    private string _defaultDuration = string.Empty;
    private string _stackingModel = string.Empty;
    private int _maximumStacks = 1;
    private string _refreshReplaceRules = string.Empty;
    private bool _isHiddenState;
    private string _dispelRemovalRules = string.Empty;
    private string _immunityTags = string.Empty;
    private string _resistanceTags = string.Empty;
    private string _iconKey = string.Empty;
    private MagicReferenceOptionVm? _effectDamageType;
    private MagicReferenceOptionVm? _effectResource;
    private MagicReferenceOptionVm? _effectDerivedStat;
    private MagicReferenceOptionVm? _effectAttribute;
    private MagicReferenceOptionVm? _effectSubAttribute;
    private MagicReferenceOptionVm? _effectSkill;
    private MagicReferenceOptionVm? _effectCondition;
    private bool _targetSelf;
    private bool _targetOtherActor;
    private bool _targetObject;
    private bool _targetPosition;
    private bool _targetArea;

    public event Action? Changed;
    public ObservableCollection<string> ValidationErrors { get; } = new();
    public ObservableCollection<MagicChoiceOptionVm> EffectKinds { get; } = new()
    {
        new("damage", "Урон"),
        new("healing", "Лечение"),
        new("resource_change", "Изменение ресурса"),
        new("modifier", "Модификатор"),
        new("grant_action", "Добавить действие"),
        new("revoke_action", "Запретить действие"),
        new("apply_condition", "Наложить состояние"),
        new("remove_condition", "Снять состояние"),
        new("resistance", "Сопротивление"),
        new("vulnerability", "Уязвимость"),
        new("movement_control", "Контроль перемещения"),
        new("custom_manual", "Другое, разрешается вручную")
    };
    public ObservableCollection<MagicChoiceOptionVm> EffectTimings { get; } = new()
    {
        new("immediate", "Сразу"),
        new("on_apply", "При применении"),
        new("periodic", "Периодически"),
        new("on_remove", "При снятии"),
        new("reaction", "Как реакция")
    };
    public ObservableCollection<MagicChoiceOptionVm> ConditionStackingOptions { get; } = new()
    {
        new("none", "Не складывается"),
        new("replace", "Заменяет предыдущее"),
        new("refresh", "Обновляет длительность"),
        new("stack", "Складывает уровни"),
        new("highest", "Использует сильнейшее"),
        new("custom_manual", "Особое правило GM")
    };
    public ObservableCollection<SelectableMagicReferenceVm> MethodOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> DirectionOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> EffectOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> ConditionOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> SkillOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> AttributeOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> SubAttributeOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> ResourceOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> ItemOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> DamageTypeOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> DevelopmentNodeOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> ParentDirectionOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> RelatedDirectionOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> OpposedDirectionOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> EffectsOnApplyOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> PeriodicEffectOptions { get; } = new();
    public ObservableCollection<SelectableMagicReferenceVm> EffectsOnRemoveOptions { get; } = new();
    public ObservableCollection<MagicReferenceOptionVm> SingleDamageTypeOptions { get; } = new();
    public ObservableCollection<MagicReferenceOptionVm> SingleResourceOptions { get; } = new();
    public ObservableCollection<MagicReferenceOptionVm> SingleDerivedStatOptions { get; } = new();
    public ObservableCollection<MagicReferenceOptionVm> SingleAttributeOptions { get; } = new();
    public ObservableCollection<MagicReferenceOptionVm> SingleSubAttributeOptions { get; } = new();
    public ObservableCollection<MagicReferenceOptionVm> SingleSkillOptions { get; } = new();
    public ObservableCollection<MagicReferenceOptionVm> SingleConditionOptions { get; } = new();
    public ObservableCollection<MagicResourceCostEditorVm> ResourceCosts { get; } = new();
    public ObservableCollection<RitualStageEditorVm> RitualStages { get; } = new();

    public string DefinitionId => _definitionId;
    public string Family { get => _family; private set { _family = value; Notify(); NotifyFamily(); } }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string ProfileCategory { get => _profileCategory; set => Set(ref _profileCategory, value); }
    public string RuleSetId { get => _ruleSetId; set => Set(ref _ruleSetId, value); }
    public string TagsText { get => _tagsText; set => Set(ref _tagsText, value); }
    public string PublicDescription { get => _publicDescription; set => Set(ref _publicDescription, value); }
    public string GMDescription { get => _gmDescription; set => Set(ref _gmDescription, value); }
    public string VisibilityRule { get => _visibilityRule; set => Set(ref _visibilityRule, value); }
    public string ResourceModel { get => _resourceModel; set => Set(ref _resourceModel, value); }
    public string PreparationModel { get => _preparationModel; set => Set(ref _preparationModel, value); }
    public string CastingModel { get => _castingModel; set => Set(ref _castingModel, value); }
    public string DefaultRiskProfile { get => _defaultRiskProfile; set => Set(ref _defaultRiskProfile, value); }
    public string Legality { get => _legality; set => Set(ref _legality, value); }
    public string DirectionKind { get => _directionKind; set => Set(ref _directionKind, value); }
    public string Rarity { get => _rarity; set => Set(ref _rarity, value); }
    public int Tier { get => _tier; set => Set(ref _tier, value); }
    public string CheckType { get => _checkType; set => Set(ref _checkType, value); }
    public string RollProfile { get => _rollProfile; set => Set(ref _rollProfile, value); }
    public string CastingTime { get => _castingTime; set => Set(ref _castingTime, value); }
    public int ActionCost { get => _actionCost; set => Set(ref _actionCost, value); }
    public string PreparationRequirements { get => _preparationRequirements; set => Set(ref _preparationRequirements, value); }
    public string Range { get => _range; set => Set(ref _range, value); }
    public string TargetModel { get => _targetModel; set => Set(ref _targetModel, value); }
    public string Area { get => _area; set => Set(ref _area, value); }
    public string Duration { get => _duration; set => Set(ref _duration, value); }
    public bool RequiresConcentration { get => _requiresConcentration; set => Set(ref _requiresConcentration, value); }
    public bool RequiresChanneling { get => _requiresChanneling; set => Set(ref _requiresChanneling, value); }
    public bool IsInterruptible { get => _isInterruptible; set => Set(ref _isInterruptible, value); }
    public string FailureMetadata { get => _failureMetadata; set => Set(ref _failureMetadata, value); }
    public string RiskMetadata { get => _riskMetadata; set => Set(ref _riskMetadata, value); }
    public string License { get => _license; set => Set(ref _license, value); }
    public string TriggerType { get => _triggerType; set => Set(ref _triggerType, value); }
    public string ActivationRequirements { get => _activationRequirements; set => Set(ref _activationRequirements, value); }
    public string Persistence { get => _persistence; set => Set(ref _persistence, value); }
    public int Charges { get => _charges; set => Set(ref _charges, value); }
    public string InterruptionRules { get => _interruptionRules; set => Set(ref _interruptionRules, value); }
    public string DestructionRules { get => _destructionRules; set => Set(ref _destructionRules, value); }
    public decimal ArcanaCost { get => _arcanaCost; set => Set(ref _arcanaCost, value); }
    public string ChannelTime { get => _channelTime; set => Set(ref _channelTime, value); }
    public string Overload { get => _overload; set => Set(ref _overload, value); }
    public string Stability { get => _stability; set => Set(ref _stability, value); }
    public string Requirements { get => _requirements; set => Set(ref _requirements, value); }
    public int RequiredParticipants { get => _requiredParticipants; set => Set(ref _requiredParticipants, value); }
    public string ParticipantRoles { get => _participantRoles; set => Set(ref _participantRoles, value); }
    public string ExecutionDuration { get => _executionDuration; set => Set(ref _executionDuration, value); }
    public string LocationRequirements { get => _locationRequirements; set => Set(ref _locationRequirements, value); }
    public string FailureConsequences { get => _failureConsequences; set => Set(ref _failureConsequences, value); }
    public string ResultDuration { get => _resultDuration; set => Set(ref _resultDuration, value); }
    public string EffectKind { get => _effectKind; set => Set(ref _effectKind, value); }
    public string TargetSelector { get => _targetSelector; set => Set(ref _targetSelector, value); }
    public string Timing { get => _timing; set => Set(ref _timing, value); }
    public string Operation { get => _operation; set => Set(ref _operation, value); }
    public string ValueExpression { get => _valueExpression; set => Set(ref _valueExpression, value); }
    public string Interval { get => _interval; set => Set(ref _interval, value); }
    public string StackingBehavior { get => _stackingBehavior; set => Set(ref _stackingBehavior, value); }
    public string SourceRestrictions { get => _sourceRestrictions; set => Set(ref _sourceRestrictions, value); }
    public string ManualResolution { get => _manualResolution; set => Set(ref _manualResolution, value); }
    public string Severity { get => _severity; set => Set(ref _severity, value); }
    public string DurationModel { get => _durationModel; set => Set(ref _durationModel, value); }
    public string DefaultDuration { get => _defaultDuration; set => Set(ref _defaultDuration, value); }
    public string StackingModel { get => _stackingModel; set => Set(ref _stackingModel, value); }
    public int MaximumStacks { get => _maximumStacks; set => Set(ref _maximumStacks, value); }
    public string RefreshReplaceRules { get => _refreshReplaceRules; set => Set(ref _refreshReplaceRules, value); }
    public bool IsHiddenState { get => _isHiddenState; set => Set(ref _isHiddenState, value); }
    public string DispelRemovalRules { get => _dispelRemovalRules; set => Set(ref _dispelRemovalRules, value); }
    public string ImmunityTags { get => _immunityTags; set => Set(ref _immunityTags, value); }
    public string ResistanceTags { get => _resistanceTags; set => Set(ref _resistanceTags, value); }
    public string IconKey { get => _iconKey; set => Set(ref _iconKey, value); }
    public MagicReferenceOptionVm? EffectDamageType { get => _effectDamageType; set => Set(ref _effectDamageType, value); }
    public MagicReferenceOptionVm? EffectResource { get => _effectResource; set => Set(ref _effectResource, value); }
    public MagicReferenceOptionVm? EffectDerivedStat { get => _effectDerivedStat; set => Set(ref _effectDerivedStat, value); }
    public MagicReferenceOptionVm? EffectAttribute { get => _effectAttribute; set => Set(ref _effectAttribute, value); }
    public MagicReferenceOptionVm? EffectSubAttribute { get => _effectSubAttribute; set => Set(ref _effectSubAttribute, value); }
    public MagicReferenceOptionVm? EffectSkill { get => _effectSkill; set => Set(ref _effectSkill, value); }
    public MagicReferenceOptionVm? EffectCondition { get => _effectCondition; set => Set(ref _effectCondition, value); }
    public bool HasUnsavedChanges => _dirty;
    public bool HasValidationIssues => ValidationErrors.Count > 0;
    public string ValidationSummary => string.Join(Environment.NewLine, ValidationErrors);
    public bool IsMagicMethod => Family == DefinitionCategoryIds.MagicMethod;
    public bool IsMagicDirection => Family == DefinitionCategoryIds.MagicDirection;
    public bool IsSpell => Family == DefinitionCategoryIds.Spell;
    public bool IsSeal => Family == DefinitionCategoryIds.Seal;
    public bool IsArcanaForm => Family == DefinitionCategoryIds.ArcanaForm;
    public bool IsRitual => Family == DefinitionCategoryIds.Ritual;
    public bool IsEffect => Family == DefinitionCategoryIds.Effect;
    public bool IsCondition => Family == DefinitionCategoryIds.Condition;
    public bool SupportsTargetScopes => IsMagicMethod || IsSpell || IsSeal || IsArcanaForm || IsRitual;
    public bool TargetSelf { get => _targetSelf; set => SetTargetScope(ref _targetSelf, value); }
    public bool TargetOtherActor { get => _targetOtherActor; set => SetTargetScope(ref _targetOtherActor, value); }
    public bool TargetObject { get => _targetObject; set => SetTargetScope(ref _targetObject, value); }
    public bool TargetPosition { get => _targetPosition; set => SetTargetScope(ref _targetPosition, value); }
    public bool TargetArea { get => _targetArea; set => SetTargetScope(ref _targetArea, value); }
    public string TargetScopeSummary
    {
        get
        {
            var labels = new List<string>();
            if (TargetSelf) labels.Add("на себя");
            if (TargetOtherActor) labels.Add("на другого персонажа");
            if (TargetObject) labels.Add("на объект");
            if (TargetPosition) labels.Add("на точку");
            if (TargetArea) labels.Add("на область");
            return labels.Count switch
            {
                0 => "Допустимые цели не выбраны.",
                1 => $"Ограничение: только {labels[0]}.",
                _ => $"Допустимые цели: {string.Join(", ", labels)}."
            };
        }
    }

    public void New(string family, IEnumerable<MagicReferenceOptionVm> references)
    {
        _definitionId = string.Empty;
        Family = family;
        ResetValues();
        RebuildReferences(references, new Dictionary<string, List<string>>());
        _dirty = true;
        Validate();
        Notify(nameof(HasUnsavedChanges));
    }

    public void Load(Dictionary<string, object> map, IEnumerable<MagicReferenceOptionVm> references)
    {
        _loading = true;
        _definitionId = AdminItemsEquipmentCatalogViewModel.S(map, "definitionId");
        Family = AdminItemsEquipmentCatalogViewModel.S(map, "family");
        _name = AdminItemsEquipmentCatalogViewModel.S(map, "displayName");
        _profileCategory = First(map, "methodCategory", "directionKind", "spellCategory", "formCategory", "ritualCategory", "conditionCategory");
        _ruleSetId = AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "ruleSetIds").FirstOrDefault() ?? RuleSetIds.FantasyNriDefault;
        _tagsText = string.Join(", ", AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "tags"));
        _publicDescription = AdminItemsEquipmentCatalogViewModel.S(map, "publicDescription");
        _gmDescription = AdminItemsEquipmentCatalogViewModel.S(map, "gmDescription");
        _visibilityRule = AdminItemsEquipmentCatalogViewModel.S(map, "visibilityRule");
        LoadScalarValues(map);
        var targetScopes = new HashSet<string>(AdminItemsEquipmentCatalogViewModel.ReadStrings(map, "allowedTargetScopes"), StringComparer.OrdinalIgnoreCase);
        _targetSelf = targetScopes.Contains(MagicTargetScopeIds.Self);
        _targetOtherActor = targetScopes.Contains(MagicTargetScopeIds.OtherActor);
        _targetObject = targetScopes.Contains(MagicTargetScopeIds.Object);
        _targetPosition = targetScopes.Contains(MagicTargetScopeIds.Position);
        _targetArea = targetScopes.Contains(MagicTargetScopeIds.Area);
        var selections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in SelectionKeys) selections[key] = AdminItemsEquipmentCatalogViewModel.ReadStrings(map, key);
        RebuildReferences(references, selections);
        _effectDamageType = Find(SingleDamageTypeOptions, S(map, "damageTypeDefinitionId"));
        _effectResource = Find(SingleResourceOptions, S(map, "resourceDefinitionId"));
        _effectDerivedStat = Find(SingleDerivedStatOptions, S(map, "derivedStatDefinitionId"));
        _effectAttribute = Find(SingleAttributeOptions, S(map, "attributeDefinitionId"));
        _effectSubAttribute = Find(SingleSubAttributeOptions, S(map, "subAttributeDefinitionId"));
        _effectSkill = Find(SingleSkillOptions, S(map, "skillDefinitionId"));
        _effectCondition = Find(SingleConditionOptions, S(map, "conditionDefinitionId"));
        LoadResourceCosts(map, references);
        LoadRitualStages(map);
        _loading = false;
        _dirty = false;
        Validate();
        NotifyAll();
    }

    public Dictionary<string, object> ToPayload()
    {
        var result = new Dictionary<string, object>
        {
            ["definitionId"] = DefinitionId,
            ["family"] = Family,
            ["isCreate"] = string.IsNullOrWhiteSpace(DefinitionId),
            ["name"] = Name,
            ["ruleSetIds"] = Split(RuleSetId).Cast<object>().ToArray(),
            ["tags"] = Split(TagsText).Cast<object>().ToArray(),
            ["publicDescription"] = PublicDescription,
            ["gmDescription"] = GMDescription,
            ["visibilityRule"] = VisibilityRule,
            ["methodCategory"] = ProfileCategory,
            ["directionKind"] = ProfileCategory,
            ["spellCategory"] = ProfileCategory,
            ["formCategory"] = ProfileCategory,
            ["ritualCategory"] = ProfileCategory,
            ["conditionCategory"] = ProfileCategory,
            ["resourceModel"] = ResourceModel,
            ["preparationModel"] = PreparationModel,
            ["castingModel"] = CastingModel,
            ["defaultRiskProfile"] = DefaultRiskProfile,
            ["legality"] = Legality,
            ["rarity"] = Rarity,
            ["tier"] = Tier,
            ["checkType"] = CheckType,
            ["rollProfile"] = RollProfile,
            ["castingTime"] = CastingTime,
            ["preparationTime"] = PreparationModel,
            ["actionCost"] = ActionCost,
            ["preparationRequirements"] = PreparationRequirements,
            ["range"] = Range,
            ["targetModel"] = TargetModel,
            ["area"] = Area,
            ["duration"] = Duration,
            ["requiresConcentration"] = RequiresConcentration,
            ["requiresChanneling"] = RequiresChanneling,
            ["isInterruptible"] = IsInterruptible,
            ["failureMetadata"] = FailureMetadata,
            ["riskMetadata"] = RiskMetadata,
            ["license"] = License,
            ["triggerType"] = TriggerType,
            ["activationRequirements"] = ActivationRequirements,
            ["persistence"] = Persistence,
            ["charges"] = Charges,
            ["interruptionRules"] = InterruptionRules,
            ["destructionRules"] = DestructionRules,
            ["arcanaCost"] = ArcanaCost,
            ["channelTime"] = ChannelTime,
            ["overload"] = Overload,
            ["stability"] = Stability,
            ["risk"] = RiskMetadata,
            ["requirements"] = Requirements,
            ["requiredParticipants"] = RequiredParticipants,
            ["participantRoles"] = Split(ParticipantRoles).Cast<object>().ToArray(),
            ["executionDuration"] = ExecutionDuration,
            ["locationRequirements"] = LocationRequirements,
            ["failureConsequences"] = FailureConsequences,
            ["resultDuration"] = ResultDuration,
            ["effectKind"] = EffectKind,
            ["targetSelector"] = TargetSelector,
            ["timing"] = Timing,
            ["operation"] = Operation,
            ["valueExpression"] = ValueExpression,
            ["interval"] = Interval,
            ["stackingBehavior"] = StackingBehavior,
            ["sourceRestrictions"] = SourceRestrictions,
            ["manualResolution"] = ManualResolution,
            ["severity"] = Severity,
            ["durationModel"] = DurationModel,
            ["defaultDuration"] = DefaultDuration,
            ["stackingModel"] = StackingModel,
            ["maximumStacks"] = MaximumStacks,
            ["refreshReplaceRules"] = RefreshReplaceRules,
            ["isHiddenState"] = IsHiddenState,
            ["dispelRemovalRules"] = DispelRemovalRules,
            ["immunityTags"] = Split(ImmunityTags).Cast<object>().ToArray(),
            ["resistanceTags"] = Split(ResistanceTags).Cast<object>().ToArray(),
            ["iconKey"] = IconKey
        };
        result["allowedTargetScopes"] = SelectedTargetScopes().Cast<object>().ToArray();
        PutSelected(result, "magicMethodIds", MethodOptions);
        PutSelected(result, "compatibleMethodIds", MethodOptions);
        PutSelected(result, "magicDirectionIds", DirectionOptions);
        PutSelected(result, "compatibleDirectionIds", DirectionOptions);
        PutSelected(result, "effectDefinitionIds", EffectOptions);
        PutSelected(result, "conditionDefinitionIds", ConditionOptions);
        PutSelected(result, "primarySkillIds", SkillOptions);
        PutSelected(result, "requiredSkillIds", SkillOptions);
        PutSelected(result, "allowedAttributeIds", AttributeOptions);
        PutSelected(result, "allowedSubAttributeIds", SubAttributeOptions);
        PutSelected(result, "resourceDefinitionIds", ResourceOptions);
        PutSelected(result, "materialResourceIds", ResourceOptions);
        PutSelected(result, "materialItemIds", ItemOptions);
        PutSelected(result, "damageTypeDefinitionIds", DamageTypeOptions);
        PutSelected(result, "developmentNodeIds", DevelopmentNodeOptions);
        PutSelected(result, "parentDirectionIds", ParentDirectionOptions);
        PutSelected(result, "relatedDirectionIds", RelatedDirectionOptions);
        PutSelected(result, "opposedDirectionIds", OpposedDirectionOptions);
        PutSelected(result, "effectsOnApplyIds", EffectsOnApplyOptions);
        PutSelected(result, "periodicEffectIds", PeriodicEffectOptions);
        PutSelected(result, "effectsOnRemoveIds", EffectsOnRemoveOptions);
        result["damageTypeDefinitionId"] = EffectDamageType?.DefinitionId ?? string.Empty;
        result["resourceDefinitionId"] = EffectResource?.DefinitionId ?? string.Empty;
        result["derivedStatDefinitionId"] = EffectDerivedStat?.DefinitionId ?? string.Empty;
        result["attributeDefinitionId"] = EffectAttribute?.DefinitionId ?? string.Empty;
        result["subAttributeDefinitionId"] = EffectSubAttribute?.DefinitionId ?? string.Empty;
        result["skillDefinitionId"] = EffectSkill?.DefinitionId ?? string.Empty;
        result["conditionDefinitionId"] = EffectCondition?.DefinitionId ?? string.Empty;
        result["resourceCosts"] = ResourceCosts.Where(x => x.SelectedResource != null).Select(x => (object)new Dictionary<string, object>
        {
            ["resourceDefinitionId"] = x.SelectedResource!.DefinitionId,
            ["amount"] = x.Amount,
            ["requirement"] = x.Requirement
        }).ToArray();
        result["stages"] = RitualStages.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Select(x => (object)new Dictionary<string, object>
        {
            ["name"] = x.Name,
            ["duration"] = x.Duration,
            ["requirements"] = x.Requirements
        }).ToArray();
        return result;
    }

    public void Validate()
    {
        ValidationErrors.Clear();
        if (string.IsNullOrWhiteSpace(Name)) ValidationErrors.Add("Название обязательно.");
        if (string.IsNullOrWhiteSpace(ProfileCategory)) ValidationErrors.Add("Категория обязательна.");
        if ((VisibilityRule == VisibilityRuleIds.Public || VisibilityRule == VisibilityRuleIds.PlayerVisible) && string.IsNullOrWhiteSpace(PublicDescription))
            ValidationErrors.Add("Для видимой игрокам записи требуется публичное описание.");
        if (Tier < 0 || ActionCost < 0 || Charges < 0 || ArcanaCost < 0) ValidationErrors.Add("Числовые стоимости не могут быть отрицательными.");
        if (IsCondition && MaximumStacks < 1) ValidationErrors.Add("Максимум стаков должен быть не меньше 1.");
        if ((IsSpell || IsSeal || IsArcanaForm || IsRitual) && !EffectOptions.Any(x => x.IsSelected) && !ConditionOptions.Any(x => x.IsSelected))
            ValidationErrors.Add("Добавьте хотя бы один эффект или состояние результата.");
        if (SupportsTargetScopes && !SelectedTargetScopes().Any())
            ValidationErrors.Add("Выберите хотя бы один допустимый тип цели.");
        Notify(nameof(HasValidationIssues));
        Notify(nameof(ValidationSummary));
    }

    public void MarkDirty()
    {
        if (_loading) return;
        _dirty = true;
        Validate();
        Notify(nameof(HasUnsavedChanges));
        Changed?.Invoke();
    }

    public void MarkClean()
    {
        _dirty = false;
        Notify(nameof(HasUnsavedChanges));
        Changed?.Invoke();
    }

    private void ResetValues()
    {
        _loading = true;
        _name = string.Empty; _profileCategory = string.Empty; _ruleSetId = RuleSetIds.FantasyNriDefault; _tagsText = string.Empty;
        _publicDescription = string.Empty; _gmDescription = string.Empty; _visibilityRule = VisibilityRuleIds.Public;
        _resourceModel = string.Empty; _preparationModel = string.Empty; _castingModel = string.Empty; _defaultRiskProfile = string.Empty; _legality = string.Empty;
        _directionKind = string.Empty; _rarity = string.Empty; _tier = 0; _checkType = string.Empty; _rollProfile = string.Empty; _castingTime = string.Empty; _actionCost = 0;
        _preparationRequirements = string.Empty; _range = string.Empty; _targetModel = string.Empty; _area = string.Empty; _duration = string.Empty;
        _requiresConcentration = false; _requiresChanneling = false; _isInterruptible = false; _failureMetadata = string.Empty; _riskMetadata = string.Empty; _license = string.Empty;
        _triggerType = string.Empty; _activationRequirements = string.Empty; _persistence = string.Empty; _charges = 0; _interruptionRules = string.Empty; _destructionRules = string.Empty;
        _arcanaCost = 0; _channelTime = string.Empty; _overload = string.Empty; _stability = string.Empty; _requirements = string.Empty;
        _requiredParticipants = 1; _participantRoles = string.Empty; _executionDuration = string.Empty; _locationRequirements = string.Empty; _failureConsequences = string.Empty; _resultDuration = string.Empty;
        _effectKind = "custom_manual"; _targetSelector = string.Empty; _timing = "immediate"; _operation = string.Empty; _valueExpression = string.Empty; _interval = string.Empty;
        _stackingBehavior = string.Empty; _sourceRestrictions = string.Empty; _manualResolution = string.Empty; _severity = string.Empty; _durationModel = string.Empty;
        _defaultDuration = string.Empty; _stackingModel = string.Empty; _maximumStacks = 1; _refreshReplaceRules = string.Empty; _isHiddenState = false;
        _dispelRemovalRules = string.Empty; _immunityTags = string.Empty; _resistanceTags = string.Empty; _iconKey = string.Empty;
        _targetSelf = false; _targetOtherActor = false; _targetObject = false; _targetPosition = false; _targetArea = false;
        ResourceCosts.Clear(); RitualStages.Clear(); _loading = false; NotifyAll();
    }

    private void LoadScalarValues(Dictionary<string, object> map)
    {
        _resourceModel = S(map, "resourceModel"); _preparationModel = First(map, "preparationModel", "preparationTime"); _castingModel = S(map, "castingModel");
        _defaultRiskProfile = S(map, "defaultRiskProfile"); _legality = S(map, "legality"); _directionKind = S(map, "directionKind"); _rarity = S(map, "rarity");
        _tier = I(map, "tier"); _checkType = S(map, "checkType"); _rollProfile = S(map, "rollProfile"); _castingTime = S(map, "castingTime"); _actionCost = I(map, "actionCost");
        _preparationRequirements = S(map, "preparationRequirements"); _range = S(map, "range"); _targetModel = S(map, "targetModel"); _area = S(map, "area"); _duration = S(map, "duration");
        _requiresConcentration = B(map, "requiresConcentration"); _requiresChanneling = B(map, "requiresChanneling"); _isInterruptible = B(map, "isInterruptible");
        _failureMetadata = S(map, "failureMetadata"); _riskMetadata = First(map, "riskMetadata", "risk"); _license = S(map, "license");
        _triggerType = S(map, "triggerType"); _activationRequirements = S(map, "activationRequirements"); _persistence = S(map, "persistence"); _charges = I(map, "charges");
        _interruptionRules = S(map, "interruptionRules"); _destructionRules = S(map, "destructionRules"); _arcanaCost = D(map, "arcanaCost");
        _channelTime = S(map, "channelTime"); _overload = S(map, "overload"); _stability = S(map, "stability"); _requirements = S(map, "requirements");
        _requiredParticipants = Math.Max(1, I(map, "requiredParticipants")); _participantRoles = string.Join(", ", L(map, "participantRoles"));
        _executionDuration = S(map, "executionDuration"); _locationRequirements = S(map, "locationRequirements"); _failureConsequences = S(map, "failureConsequences"); _resultDuration = S(map, "resultDuration");
        _effectKind = S(map, "effectKind"); _targetSelector = S(map, "targetSelector"); _timing = S(map, "timing"); _operation = S(map, "operation"); _valueExpression = S(map, "valueExpression");
        _interval = S(map, "interval"); _stackingBehavior = S(map, "stackingBehavior"); _sourceRestrictions = S(map, "sourceRestrictions"); _manualResolution = S(map, "manualResolution");
        _severity = S(map, "severity"); _durationModel = S(map, "durationModel"); _defaultDuration = S(map, "defaultDuration"); _stackingModel = S(map, "stackingModel");
        _maximumStacks = Math.Max(1, I(map, "maximumStacks")); _refreshReplaceRules = S(map, "refreshReplaceRules"); _isHiddenState = B(map, "isHiddenState");
        _dispelRemovalRules = S(map, "dispelRemovalRules"); _immunityTags = string.Join(", ", L(map, "immunityTags")); _resistanceTags = string.Join(", ", L(map, "resistanceTags")); _iconKey = S(map, "iconKey");
    }

    private void RebuildReferences(IEnumerable<MagicReferenceOptionVm> references, IDictionary<string, List<string>> selected)
    {
        var all = references.Where(x => !x.IsArchived).ToList();
        Fill(MethodOptions, all, DefinitionCategoryIds.MagicMethod, Selected(selected, "magicMethodIds", "compatibleMethodIds"));
        Fill(DirectionOptions, all, DefinitionCategoryIds.MagicDirection, Selected(selected, "magicDirectionIds", "compatibleDirectionIds"));
        Fill(EffectOptions, all, DefinitionCategoryIds.Effect, Selected(selected, "effectDefinitionIds"));
        Fill(ConditionOptions, all, DefinitionCategoryIds.Condition, Selected(selected, "conditionDefinitionIds"));
        Fill(SkillOptions, all, DefinitionCategoryIds.Skill, Selected(selected, "primarySkillIds", "requiredSkillIds"));
        Fill(AttributeOptions, all, DefinitionCategoryIds.Attribute, Selected(selected, "allowedAttributeIds"));
        Fill(SubAttributeOptions, all, DefinitionCategoryIds.SubAttribute, Selected(selected, "allowedSubAttributeIds"));
        Fill(ResourceOptions, all, DefinitionCategoryIds.Resource, Selected(selected, "resourceDefinitionIds", "materialResourceIds"));
        Fill(ItemOptions, all, DefinitionCategoryIds.Item, Selected(selected, "materialItemIds"));
        Fill(DamageTypeOptions, all, DefinitionCategoryIds.DamageType, Selected(selected, "damageTypeDefinitionIds"));
        Fill(DevelopmentNodeOptions, all, DefinitionCategoryIds.DevelopmentNode, Selected(selected, "developmentNodeIds"));
        Fill(ParentDirectionOptions, all, DefinitionCategoryIds.MagicDirection, Selected(selected, "parentDirectionIds"));
        Fill(RelatedDirectionOptions, all, DefinitionCategoryIds.MagicDirection, Selected(selected, "relatedDirectionIds"));
        Fill(OpposedDirectionOptions, all, DefinitionCategoryIds.MagicDirection, Selected(selected, "opposedDirectionIds"));
        Fill(EffectsOnApplyOptions, all, DefinitionCategoryIds.Effect, Selected(selected, "effectsOnApplyIds"));
        Fill(PeriodicEffectOptions, all, DefinitionCategoryIds.Effect, Selected(selected, "periodicEffectIds"));
        Fill(EffectsOnRemoveOptions, all, DefinitionCategoryIds.Effect, Selected(selected, "effectsOnRemoveIds"));
        FillSingle(SingleDamageTypeOptions, all, DefinitionCategoryIds.DamageType);
        FillSingle(SingleResourceOptions, all, DefinitionCategoryIds.Resource);
        FillSingle(SingleDerivedStatOptions, all, DefinitionCategoryIds.DerivedStat);
        FillSingle(SingleAttributeOptions, all, DefinitionCategoryIds.Attribute);
        FillSingle(SingleSubAttributeOptions, all, DefinitionCategoryIds.SubAttribute);
        FillSingle(SingleSkillOptions, all, DefinitionCategoryIds.Skill);
        FillSingle(SingleConditionOptions, all, DefinitionCategoryIds.Condition);
    }

    private void LoadResourceCosts(Dictionary<string, object> map, IEnumerable<MagicReferenceOptionVm> references)
    {
        ResourceCosts.Clear();
        var resources = references.Where(x => !x.IsArchived && x.Family == DefinitionCategoryIds.Resource).ToList();
        foreach (var cost in AdminItemsEquipmentCatalogViewModel.ReadList(map, "resourceCosts"))
        {
            var vm = new MagicResourceCostEditorVm(MarkDirty);
            foreach (var option in resources) vm.ResourceOptions.Add(option);
            vm.SelectedResource = resources.FirstOrDefault(x => x.DefinitionId == S(cost, "resourceDefinitionId"));
            vm.Amount = D(cost, "amount");
            vm.Requirement = S(cost, "requirement");
            ResourceCosts.Add(vm);
        }
    }

    private void LoadRitualStages(Dictionary<string, object> map)
    {
        RitualStages.Clear();
        foreach (var stage in AdminItemsEquipmentCatalogViewModel.ReadList(map, "stages"))
        {
            RitualStages.Add(new RitualStageEditorVm(MarkDirty)
            {
                Name = S(stage, "name"),
                Duration = S(stage, "duration"),
                Requirements = S(stage, "requirements")
            });
        }
    }

    private void Fill(ObservableCollection<SelectableMagicReferenceVm> target, IEnumerable<MagicReferenceOptionVm> all, string family, IEnumerable<string> selected)
    {
        target.Clear();
        var selectedSet = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
        foreach (var option in all.Where(x => string.Equals(x.Family, family, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.DisplayName))
            target.Add(new SelectableMagicReferenceVm(option, selectedSet.Contains(option.DefinitionId), MarkDirty));
    }

    private static void FillSingle(ObservableCollection<MagicReferenceOptionVm> target, IEnumerable<MagicReferenceOptionVm> all, string family)
    {
        target.Clear();
        foreach (var option in all.Where(x => string.Equals(x.Family, family, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.DisplayName)) target.Add(option);
    }

    private static MagicReferenceOptionVm? Find(IEnumerable<MagicReferenceOptionVm> values, string id)
        => values.FirstOrDefault(x => string.Equals(x.DefinitionId, id, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Selected(IDictionary<string, List<string>> source, params string[] keys)
        => keys.Where(source.ContainsKey).SelectMany(key => source[key]).Distinct(StringComparer.OrdinalIgnoreCase);

    private static void PutSelected(Dictionary<string, object> payload, string key, IEnumerable<SelectableMagicReferenceVm> values)
        => payload[key] = values.Where(x => x.IsSelected).Select(x => (object)x.DefinitionId).ToArray();

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Notify(propertyName);
        MarkDirty();
    }

    private void SetTargetScope(ref bool field, bool value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        if (field == value) return;
        field = value;
        Notify(propertyName);
        Notify(nameof(TargetScopeSummary));
        MarkDirty();
    }

    private void NotifyFamily()
    {
        Notify(nameof(IsMagicMethod)); Notify(nameof(IsMagicDirection)); Notify(nameof(IsSpell)); Notify(nameof(IsSeal));
        Notify(nameof(IsArcanaForm)); Notify(nameof(IsRitual)); Notify(nameof(IsEffect)); Notify(nameof(IsCondition));
        Notify(nameof(SupportsTargetScopes));
    }

    private IEnumerable<string> SelectedTargetScopes()
    {
        if (TargetSelf) yield return MagicTargetScopeIds.Self;
        if (TargetOtherActor) yield return MagicTargetScopeIds.OtherActor;
        if (TargetObject) yield return MagicTargetScopeIds.Object;
        if (TargetPosition) yield return MagicTargetScopeIds.Position;
        if (TargetArea) yield return MagicTargetScopeIds.Area;
    }

    private void NotifyAll()
    {
        foreach (var property in GetType().GetProperties().Where(x => x.GetIndexParameters().Length == 0)) Notify(property.Name);
        NotifyFamily();
    }

    private static string First(Dictionary<string, object> map, params string[] keys)
        => keys.Select(key => S(map, key)).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string S(IDictionary<string, object> map, string key) => AdminItemsEquipmentCatalogViewModel.S(map, key);
    private static int I(IDictionary<string, object> map, string key) => AdminItemsEquipmentCatalogViewModel.I(map, key);
    private static decimal D(IDictionary<string, object> map, string key) => AdminItemsEquipmentCatalogViewModel.D(map, key);
    private static bool B(IDictionary<string, object> map, string key) => AdminItemsEquipmentCatalogViewModel.B(map, key);
    private static List<string> L(IDictionary<string, object> map, string key) => AdminItemsEquipmentCatalogViewModel.ReadStrings(map, key);
    private static List<string> Split(string value) => AdminItemsEquipmentCatalogViewModel.Split(value);
    private static readonly string[] SelectionKeys =
    {
        "magicMethodIds", "compatibleMethodIds", "magicDirectionIds", "compatibleDirectionIds", "effectDefinitionIds",
        "conditionDefinitionIds", "primarySkillIds", "requiredSkillIds", "allowedAttributeIds", "allowedSubAttributeIds",
        "resourceDefinitionIds", "materialResourceIds", "materialItemIds", "damageTypeDefinitionIds", "developmentNodeIds",
        "parentDirectionIds", "relatedDirectionIds", "opposedDirectionIds", "effectsOnApplyIds", "periodicEffectIds", "effectsOnRemoveIds"
    };
}

public static class MagicDefinitionLabels
{
    public static IEnumerable<MagicDefinitionFamilyOptionVm> Families()
    {
        yield return new MagicDefinitionFamilyOptionVm(DefinitionCategoryIds.MagicMethod, "Магические методы", "Способ управления магией и его ресурсная модель.");
        yield return new MagicDefinitionFamilyOptionVm(DefinitionCategoryIds.MagicDirection, "Направления магии", "Стихии, школы, традиции и пользовательские направления.");
        yield return new MagicDefinitionFamilyOptionVm(DefinitionCategoryIds.Spell, "Заклинания", "Подготовка, проверка, стоимость и несколько результатов.");
        yield return new MagicDefinitionFamilyOptionVm(DefinitionCategoryIds.Seal, "Печати", "Подготовленные триггеры, заряды и последствия.");
        yield return new MagicDefinitionFamilyOptionVm(DefinitionCategoryIds.ArcanaForm, "Формы Арканы", "Формы, стоимость, перегруз и стабильность.");
        yield return new MagicDefinitionFamilyOptionVm(DefinitionCategoryIds.Ritual, "Ритуалы", "Участники, стадии, материалы и результаты.");
        yield return new MagicDefinitionFamilyOptionVm(DefinitionCategoryIds.Effect, "Эффекты", "Переиспользуемые механические результаты.");
        yield return new MagicDefinitionFamilyOptionVm(DefinitionCategoryIds.Condition, "Состояния", "Длительность, стаки и эффекты жизненного цикла.");
    }

    public static string Family(string family) => Families().FirstOrDefault(x => x.Id == family)?.DisplayName ?? family;
}
