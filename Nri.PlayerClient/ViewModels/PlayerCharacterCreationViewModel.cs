using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerCharacterCreationViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _campaignId;
    private readonly Func<string> _campaignName;
    private bool _applying;
    private string _draftId = string.Empty;
    private string _resolvedOriginId = string.Empty;
    private long _revision;
    private string _name = string.Empty;
    private string _backstory = string.Empty;
    private OriginChoice02111? _parent1;
    private OriginChoice02111? _parent2;
    private SubtypeChoice02111? _subtype;
    private DraftChoice02111? _selectedDraft;
    private int _heightCm = 170;
    private int _ageYears = 24;
    private string _resolvedOrigin = "Выберите линии родителей";
    private string _originDescription = "Описание происхождения появится после проверки.";
    private string _heightRange = "Диапазон определит сервер";
    private string _ageRange = "Диапазон определит сервер";
    private string _lifespanSummary = "Физиология происхождения появится после проверки.";
    private string _protectionSummary = string.Empty;
    private string _equipmentFitWarning = string.Empty;
    private string _status = "Новый черновик";
    private string _feedback = "Заполните основные поля и проверьте персонажа.";
    private string _returnComment = string.Empty;
    private bool _isBusy;
    private bool _isValid;
    private bool _isReadOnly;
    private CharacterLanguageGrantProfileChoice022Gate3? _selectedLanguageGrantProfile;

    public PlayerCharacterCreationViewModel(CommandApi api, Func<string> campaignId, Func<string> campaignName)
    {
        _api = api;
        _campaignId = campaignId;
        _campaignName = campaignName;
        LoadCommand = new RelayCommand(Load);
        NewDraftCommand = new RelayCommand(NewDraft);
        OpenDraftCommand = new RelayCommand(OpenSelectedDraft);
        SaveDraftCommand = new RelayCommand(SaveDraft);
        PreviewCommand = new RelayCommand(Preview);
        SubmitCommand = new RelayCommand(Submit);
        CancelCommand = new RelayCommand(Cancel);
        AttributeOptions = new ObservableCollection<int>(new[] { -2, -1, 0, 1, 2 });
        LanguageLevelOptions = new ObservableCollection<int>(new[] { 0, 1, 2, 3, 4, 5 });
        foreach (var item in CharacterLanguageGrantProfileChoice022Gate3.All()) LanguageGrantProfiles.Add(item);
        SelectedLanguageGrantProfile = LanguageGrantProfiles[0];
    }

    public ObservableCollection<OriginChoice02111> ParentOrigins { get; } = new();
    public ObservableCollection<SubtypeChoice02111> AvailableSubtypes { get; } = new();
    public ObservableCollection<SubtypeChoice02111> AllSubtypes { get; } = new();
    public ObservableCollection<AttributeAllocationRow02111> Attributes { get; } = new();
    public ObservableCollection<AttributeAllocationRow02111> SubAttributes { get; } = new();
    public ObservableCollection<DraftChoice02111> Drafts { get; } = new();
    public ObservableCollection<string> StrongSides { get; } = new();
    public ObservableCollection<string> WeakSides { get; } = new();
    public ObservableCollection<string> Traits { get; } = new();
    public ObservableCollection<string> Languages { get; } = new();
    public ObservableCollection<string> OriginBonusExplanations { get; } = new();
    public ObservableCollection<string> SpecialSenses { get; } = new();
    public ObservableCollection<string> MovementAbilities { get; } = new();
    public ObservableCollection<int> AttributeOptions { get; }
    public ObservableCollection<CharacterCreationLanguageRow022Gate3> LanguageAllocation { get; } = new();
    public ObservableCollection<int> LanguageLevelOptions { get; }
    public ObservableCollection<CharacterLanguageGrantProfileChoice022Gate3> LanguageGrantProfiles { get; } = new();
    public CharacterLanguageGrantProfileChoice022Gate3? SelectedLanguageGrantProfile { get => _selectedLanguageGrantProfile; set { _selectedLanguageGrantProfile = value; Notify(); MarkChanged(); } }
    public string LanguageAllocationSummary
    {
        get
        {
            var selected = LanguageAllocation.Where(x => x.Level > 0).Select(x => $"{x.Name}: {x.Level} — {LanguageLevelLabel(x.Level)}").ToArray();
            return selected.Length == 0 ? "Изученные языки пока не выбраны." : string.Join(" · ", selected);
        }
    }

    public ICommand LoadCommand { get; }
    public ICommand NewDraftCommand { get; }
    public ICommand OpenDraftCommand { get; }
    public ICommand SaveDraftCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand CancelCommand { get; }

    public string CampaignDisplay => string.IsNullOrWhiteSpace(_campaignName()) ? "Кампания не выбрана" : _campaignName();
    public string Name { get => _name; set { if (_name == value) return; _name = value; Notify(); MarkChanged(); } }
    public string Backstory { get => _backstory; set { if (_backstory == value) return; _backstory = value; Notify(); MarkChanged(); } }
    public int HeightCm { get => _heightCm; set { if (_heightCm == value) return; _heightCm = value; Notify(); MarkChanged(); } }
    public int AgeYears { get => _ageYears; set { if (_ageYears == value) return; _ageYears = value; Notify(); MarkChanged(); } }
    public OriginChoice02111? Parent1 { get => _parent1; set { if (_parent1 == value) return; _parent1 = value; Notify(); if (!_applying && _parent2 == null) Parent2 = value; UpdateSubtypes(); MarkChanged(); } }
    public OriginChoice02111? Parent2 { get => _parent2; set { if (_parent2 == value) return; _parent2 = value; Notify(); UpdateSubtypes(); MarkChanged(); } }
    public SubtypeChoice02111? Subtype { get => _subtype; set { if (_subtype == value) return; _subtype = value; Notify(); MarkChanged(); } }
    public DraftChoice02111? SelectedDraft { get => _selectedDraft; set { _selectedDraft = value; Notify(); } }
    public string ResolvedOrigin { get => _resolvedOrigin; private set { _resolvedOrigin = value; Notify(); } }
    public string OriginDescription { get => _originDescription; private set { _originDescription = value; Notify(); } }
    public string HeightRange { get => _heightRange; private set { _heightRange = value; Notify(); } }
    public string AgeRange { get => _ageRange; private set { _ageRange = value; Notify(); } }
    public string LifespanSummary { get => _lifespanSummary; private set { _lifespanSummary = value; Notify(); } }
    public string ProtectionSummary { get => _protectionSummary; private set { _protectionSummary = value; Notify(); } }
    public string EquipmentFitWarning { get => _equipmentFitWarning; private set { _equipmentFitWarning = value; Notify(); Notify(nameof(HasEquipmentFitWarning)); } }
    public string Status { get => _status; private set { _status = value; Notify(); } }
    public string Feedback { get => _feedback; private set { _feedback = value; Notify(); } }
    public string ReturnComment { get => _returnComment; private set { _returnComment = value; Notify(); Notify(nameof(HasReturnComment)); } }
    public bool HasReturnComment => !string.IsNullOrWhiteSpace(ReturnComment);
    public bool HasStrongSides => StrongSides.Count > 0;
    public bool HasWeakSides => WeakSides.Count > 0;
    public bool HasTraits => Traits.Count > 0;
    public bool HasLanguages => Languages.Count > 0;
    public bool HasOriginBonuses => OriginBonusExplanations.Count > 0;
    public bool HasSpecialSenses => SpecialSenses.Count > 0;
    public bool HasMovementAbilities => MovementAbilities.Count > 0;
    public bool HasEquipmentFitWarning => !string.IsNullOrWhiteSpace(EquipmentFitWarning);
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            Notify();
            Notify(nameof(CanEdit));
            Notify(nameof(CanSubmit));
        }
    }
    public bool IsValid { get => _isValid; private set { _isValid = value; Notify(); Notify(nameof(CanSubmit)); } }
    public bool IsReadOnly { get => _isReadOnly; private set { _isReadOnly = value; Notify(); Notify(nameof(CanEdit)); Notify(nameof(CanSubmit)); } }
    public bool CanEdit => !IsReadOnly && !IsBusy;
    public bool CanSubmit => IsValid && CanEdit && !string.IsNullOrWhiteSpace(_draftId);

    public void Load()
    {
        if (IsBusy) return;
        var campaignId = _campaignId();
        if (string.IsNullOrWhiteSpace(campaignId)) { Feedback = "Сначала выберите кампанию."; return; }
        IsBusy = true;
        Notify(nameof(CampaignDisplay));
        try
        {
            var definitions = _api.CharacterCreationDefinitionsList(Payload(campaignId));
            EnsureOk(definitions);
            ApplyDefinitions(definitions.Payload);
            LoadLanguageChoices();
            LoadDraftList(campaignId);
            Feedback = ParentOrigins.Count == 0 ? "В кампании пока нет доступных происхождений." : "Данные создания персонажа загружены.";
        }
        catch (Exception ex) { Feedback = Friendly(ex); ClientLogService.Instance.Error("player.character.creation.load.error", ex); }
        finally { IsBusy = false; Notify(nameof(CanEdit)); Notify(nameof(CanSubmit)); }
    }

    private void LoadDraftList(string campaignId)
    {
        var response = _api.CharacterCreationDraftList(Payload(campaignId));
        EnsureOk(response);
        Drafts.Clear();
        foreach (var map in Maps(Get(response.Payload, "items"))) Drafts.Add(DraftChoice02111.From(map));
        SelectedDraft = Drafts.FirstOrDefault();
    }

    private void NewDraft()
    {
        _applying = true;
        try
        {
            _draftId = string.Empty; _resolvedOriginId = string.Empty; _revision = 0; Name = string.Empty; Backstory = string.Empty; Parent1 = null; Parent2 = null; Subtype = null;
            HeightCm = 170; AgeYears = 24; ResolvedOrigin = "Выберите линии родителей"; OriginDescription = "Описание происхождения появится после проверки.";
            Status = "Новый черновик"; ReturnComment = string.Empty; IsReadOnly = false; IsValid = false;
            ResetAllocations(); Feedback = "Новый черновик готов к заполнению.";
            foreach (var language in LanguageAllocation) language.Level = 0;
            SelectedLanguageGrantProfile = LanguageGrantProfiles[0];
        }
        finally { _applying = false; }
    }

    private void OpenSelectedDraft()
    {
        if (SelectedDraft == null) { Feedback = "Выберите черновик."; return; }
        IsBusy = true;
        try
        {
            var response = _api.CharacterCreationDraftGet(new Dictionary<string, object> { ["draftId"] = SelectedDraft.Id });
            EnsureOk(response); ApplyDraft(response.Payload); Preview();
        }
        catch (Exception ex) { Feedback = Friendly(ex); }
        finally { IsBusy = false; Notify(nameof(CanEdit)); }
    }

    private void SaveDraft()
    {
        if (!CanEdit) return;
        IsBusy = true;
        try
        {
            var response = _api.CharacterCreationDraftSave(BuildDraftPayload());
            EnsureOk(response);
            ApplyDraft(Map(Get(response.Payload, "draft")));
            ApplyPreview(Map(Get(response.Payload, "preview")));
            LoadDraftList(_campaignId());
            Feedback = response.Message;
            ClientLogService.Instance.Info("player.character.creation.draft.saved");
        }
        catch (Exception ex) { Feedback = Friendly(ex); ClientLogService.Instance.Warn("player.character.creation.draft.save.error " + ex.Message); }
        finally { IsBusy = false; Notify(nameof(CanEdit)); Notify(nameof(CanSubmit)); }
    }

    private void Preview()
    {
        if (string.IsNullOrWhiteSpace(_draftId)) { SaveDraft(); return; }
        try
        {
            var response = _api.CharacterCreationPreview(new Dictionary<string, object> { ["draftId"] = _draftId });
            EnsureOk(response); ApplyPreview(response.Payload); Feedback = IsValid ? "Персонаж готов к отправке." : "Исправьте отмеченные поля.";
            ClientLogService.Instance.Info($"player.character.creation.preview.state valid={IsValid.ToString().ToLowerInvariant()} editable={CanEdit.ToString().ToLowerInvariant()} draftLoaded={(!string.IsNullOrWhiteSpace(_draftId)).ToString().ToLowerInvariant()} canSubmit={CanSubmit.ToString().ToLowerInvariant()}");
        }
        catch (Exception ex) { Feedback = Friendly(ex); }
    }

    private void Submit()
    {
        if (!CanSubmit) { Feedback = "Сначала сохраните и успешно проверьте черновик."; return; }
        IsBusy = true;
        try
        {
            var response = _api.CharacterCreationSubmit(new Dictionary<string, object> { ["draftId"] = _draftId, ["operationId"] = Guid.NewGuid().ToString("N") });
            EnsureOk(response); ApplyDraft(response.Payload); Feedback = response.Message; LoadDraftList(_campaignId());
        }
        catch (Exception ex) { Feedback = Friendly(ex); }
        finally { IsBusy = false; Notify(nameof(CanEdit)); Notify(nameof(CanSubmit)); }
    }

    private void Cancel()
    {
        if (string.IsNullOrWhiteSpace(_draftId)) { NewDraft(); return; }
        try { var response = _api.CharacterCreationCancel(new Dictionary<string, object> { ["draftId"] = _draftId }); EnsureOk(response); NewDraft(); LoadDraftList(_campaignId()); Feedback = response.Message; }
        catch (Exception ex) { Feedback = Friendly(ex); }
    }

    private Dictionary<string, object> BuildDraftPayload() => new()
    {
        ["campaignId"] = _campaignId(), ["ruleSetId"] = RuleSetIds.FantasyNriDefault, ["draftId"] = _draftId,
        ["expectedRevision"] = _revision, ["displayName"] = Name, ["backstory"] = Backstory,
        ["parent1RaceId"] = Parent1?.Id ?? string.Empty, ["parent2RaceId"] = Parent2?.Id ?? string.Empty,
        ["subtypeId"] = Subtype?.Id ?? string.Empty, ["heightCm"] = HeightCm, ["ageYears"] = AgeYears,
        ["attributeAllocation"] = Attributes.ToDictionary(x => x.Id, x => (object)x.Allocated),
        ["subAttributeAllocation"] = SubAttributes.ToDictionary(x => x.Id, x => (object)x.Allocated),
        ["languageGrantProfileId"] = SelectedLanguageGrantProfile?.Id ?? CharacterLanguageGrantProfileIds022Gate3.Custom,
        ["languageAllocation"] = LanguageAllocation.Where(x => x.Level > 0).ToDictionary(x => x.LanguageId, x => (object)x.Level)
    };

    private void ApplyDefinitions(Dictionary<string, object> payload)
    {
        ParentOrigins.Clear(); AllSubtypes.Clear(); Attributes.Clear(); SubAttributes.Clear();
        foreach (var map in Maps(Get(payload, "origins")).Where(x => Str(Get(x, "originKind")) == CharacterOriginKinds.Race)) ParentOrigins.Add(OriginChoice02111.From(map));
        foreach (var map in Maps(Get(payload, "subtypes"))) AllSubtypes.Add(SubtypeChoice02111.From(map));
        foreach (var map in Maps(Get(payload, "attributeDefinitions"))) Attributes.Add(AttributeAllocationRow02111.From(map, "attributeId"));
        foreach (var map in Maps(Get(payload, "subAttributeDefinitions")))
        {
            var row = AttributeAllocationRow02111.From(map, "subAttributeId");
            row.ParentDisplayName = Attributes.FirstOrDefault(x => string.Equals(x.Id, row.ParentAttributeId, StringComparison.Ordinal))?.DisplayName ?? row.ParentAttributeId;
            SubAttributes.Add(row);
        }
        if (Attributes.Count == 0)
        {
            var labels = new[] { (CharacterAttributeIds.Strength, "Сила"), (CharacterAttributeIds.Dexterity, "Ловкость"), (CharacterAttributeIds.Endurance, "Выносливость"), (CharacterAttributeIds.Intellect, "Интеллект"), (CharacterAttributeIds.Wisdom, "Мудрость"), (CharacterAttributeIds.Charisma, "Харизма") };
            foreach (var item in labels) Attributes.Add(new AttributeAllocationRow02111(item.Item1, item.Item2));
        }
        ResetAllocations(); UpdateSubtypes();
    }

    private void ResetAllocations()
    {
        var preset = new[] { 2, 1, 0, 0, -1, -2 };
        for (var i = 0; i < Attributes.Count; i++) Attributes[i].Allocated = i < preset.Length ? preset[i] : 0;
        foreach (var row in SubAttributes) row.Allocated = 0;
    }

    private void ApplyDraft(Dictionary<string, object> map)
    {
        _applying = true;
        try
        {
            _draftId = Str(Get(map, "draftId")); _revision = Long(Get(map, "entityRevision")); Name = Str(Get(map, "displayName")); Backstory = Str(Get(map, "backstory"));
            Parent1 = ParentOrigins.FirstOrDefault(x => x.Id == Str(Get(map, "parent1RaceId"))); Parent2 = ParentOrigins.FirstOrDefault(x => x.Id == Str(Get(map, "parent2RaceId")));
            UpdateSubtypes(); Subtype = AvailableSubtypes.FirstOrDefault(x => x.Id == Str(Get(map, "subtypeId"))); HeightCm = Int(Get(map, "heightCm"), 170); AgeYears = Int(Get(map, "ageYears"), 24);
            ApplyAllocations(Attributes, Map(Get(map, "attributeAllocation"))); ApplyAllocations(SubAttributes, Map(Get(map, "subAttributeAllocation")));
            var languageValues = Map(Get(map, "languageAllocation"));
            foreach (var language in LanguageAllocation) language.Level = languageValues.TryGetValue(language.LanguageId, out var rawLevel) ? Int(rawLevel, 0) : 0;
            Notify(nameof(LanguageAllocationSummary));
            SelectedLanguageGrantProfile = LanguageGrantProfiles.FirstOrDefault(x => x.Id == Str(Get(map, "languageGrantProfileId"))) ?? LanguageGrantProfiles[0];
            _resolvedOriginId = Str(Get(map, "resolvedOriginId"));
            ResolvedOrigin = First(Str(Get(map, "resolvedOriginName")), "Происхождение ещё не определено"); Status = First(Str(Get(map, "statusDisplay")), "Черновик");
            ReturnComment = Str(Get(map, "returnComment")); IsReadOnly = Bool(Get(map, "isReadOnly"));
        }
        finally { _applying = false; }
    }

    private void LoadLanguageChoices()
    {
        var response = _api.ContentDefinitionPlayerLanguagesList();
        if (response.Status != ResponseStatus.Ok) return;
        var previous = LanguageAllocation.ToDictionary(x => x.LanguageId, x => x.Level, StringComparer.Ordinal);
        LanguageAllocation.Clear();
        foreach (var map in Maps(Get(response.Payload, "languages")))
        {
            var id = Str(Get(map, "languageId"));
            if (string.IsNullOrWhiteSpace(id)) continue;
            var row = new CharacterCreationLanguageRow022Gate3
            {
                LanguageId = id,
                Name = Str(Get(map, "name")),
                Roles = string.Join(", ", Strings(Get(map, "roles")).Select(LanguageRoleLabel)),
                Level = previous.TryGetValue(id, out var level) ? level : 0
            };
            row.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(CharacterCreationLanguageRow022Gate3.Level)) return;
                Notify(nameof(LanguageAllocationSummary));
                MarkChanged();
            };
            LanguageAllocation.Add(row);
        }
        Notify(nameof(LanguageAllocationSummary));
    }

    private void ApplyPreview(Dictionary<string, object> map)
    {
        var wasApplying = _applying;
        _applying = true;
        try
        {
            IsValid = Bool(Get(map, "isValid"));
            _resolvedOriginId = Str(Get(map, "resolvedOriginId"));
            ResolvedOrigin = First(Str(Get(map, "resolvedOriginName")), "Происхождение не определено");
            UpdateSubtypes();
            OriginDescription = First(Str(Get(map, "subtypeDescription")), Str(Get(map, "description")), "Для этого происхождения пока нет публичного описания.");
            HeightRange = $"Допустимо: {Int(Get(map, "minimumHeightCm"), 0)}–{Int(Get(map, "maximumHeightCm"), 0)} см";
            AgeRange = $"Допустимо: {Int(Get(map, "minimumAgeYears"), 0)}–{Int(Get(map, "maximumAgeYears"), 0)}";
            LifespanSummary = $"Взросление: {Int(Get(map, "adultAgeYears"), 0)} лет · Ожидаемая жизнь: {Int(Get(map, "averageLifespanYears"), 0)} лет · Предел: {Int(Get(map, "maximumLifespanYears"), 0)} лет";
            ProtectionSummary = $"Базовое здоровье: {Int(Get(map, "baseHealth"), 0)} · Естественная броня: {Int(Get(map, "naturalArmorRating"), 0)} · Стойкость к пробитию: {Int(Get(map, "naturalPenetrationResistance"), 0)}";
            EquipmentFitWarning = Str(Get(map, "equipmentFitWarning"));
            Replace(StrongSides, Strings(Get(map, "strongSides"))); Replace(WeakSides, Strings(Get(map, "weakSides"))); Replace(Traits, Strings(Get(map, "traits"))); Replace(Languages, Strings(Get(map, "languages")));
            Replace(SpecialSenses, DisplayNames(Get(map, "senses"))); Replace(MovementAbilities, DisplayNames(Get(map, "movementAbilities")));
            ApplyBreakdown(Attributes, Map(Get(map, "attributeBreakdown"))); ApplyBreakdown(SubAttributes, Map(Get(map, "subAttributeBreakdown")));
            OriginBonusExplanations.Clear();
            foreach (var row in Attributes.Where(x => x.Origin != 0)) OriginBonusExplanations.Add($"{row.DisplayName}: {row.Origin:+0;-0;0} к итоговому значению при создании.");
            foreach (var row in SubAttributes.Where(x => x.Origin != 0)) OriginBonusExplanations.Add($"{row.DisplayLabel}: {row.Origin:+0;-0;0} к итоговому значению при создании.");
            Notify(nameof(HasStrongSides)); Notify(nameof(HasWeakSides)); Notify(nameof(HasTraits)); Notify(nameof(HasLanguages)); Notify(nameof(HasOriginBonuses)); Notify(nameof(HasSpecialSenses)); Notify(nameof(HasMovementAbilities));
            var errors = Strings(Get(map, "errors")).ToList(); if (errors.Count > 0) Feedback = string.Join(" ", errors);
        }
        finally
        {
            _applying = wasApplying;
        }
    }

    private void UpdateSubtypes()
    {
        var current = Subtype?.Id; AvailableSubtypes.Clear();
        var originId = _parent1 != null && _parent2 != null && _parent1.Id == _parent2.Id
            ? _parent1.Id
            : _resolvedOriginId;
        if (!string.IsNullOrWhiteSpace(originId))
            foreach (var item in AllSubtypes.Where(x => x.OriginId == originId)) AvailableSubtypes.Add(item);
        Subtype = AvailableSubtypes.FirstOrDefault(x => x.Id == current);
    }

    private void MarkChanged() { if (_applying) return; IsValid = false; Feedback = "Есть несохранённые изменения. Сохраните черновик и проверьте его."; }
    private static void ApplyAllocations(IEnumerable<AttributeAllocationRow02111> rows, Dictionary<string, object> values) { foreach (var row in rows) if (values.TryGetValue(row.Id, out var value)) row.Allocated = Int(value); }
    private static void ApplyBreakdown(IEnumerable<AttributeAllocationRow02111> rows, Dictionary<string, object> values) { foreach (var row in rows) if (values.TryGetValue(row.Id, out var raw)) { var map = Map(raw); row.Origin = Int(Get(map, "origin")); row.Effective = Int(Get(map, "effective"), row.Allocated + row.Origin); } }
    private static Dictionary<string, object> Payload(string campaignId) => new() { ["campaignId"] = campaignId, ["ruleSetId"] = RuleSetIds.FantasyNriDefault };
    private static void EnsureOk(ResponseEnvelope value) { if (value.Status != ResponseStatus.Ok) throw new InvalidOperationException(value.Message); }
    private static string Friendly(Exception ex) => string.IsNullOrWhiteSpace(ex.Message) ? "Операция не выполнена." : ex.Message;
    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static Dictionary<string, object> Map(object? value) => value as Dictionary<string, object> ?? new Dictionary<string, object>();
    private static IEnumerable<Dictionary<string, object>> Maps(object? value) { if (value is IEnumerable items && value is not string) foreach (var item in items) if (item is Dictionary<string, object> map) yield return map; }
    private static IEnumerable<string> Strings(object? value) { if (value is IEnumerable items && value is not string) foreach (var item in items) { var text = Str(item); if (!string.IsNullOrWhiteSpace(text)) yield return text; } }
    private static IEnumerable<string> DisplayNames(object? value) { foreach (var map in Maps(value)) { var text = Str(Get(map, "name")); if (!string.IsNullOrWhiteSpace(text)) yield return text; } }
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string LanguageRoleLabel(string value) => value switch
    {
        LanguageRoleIds022Gate3.Continental => "общий язык континента",
        LanguageRoleIds022Gate3.State => "государственный",
        LanguageRoleIds022Gate3.PoliticalCultural => "политический и культурный",
        LanguageRoleIds022Gate3.Racial => "культурное наследие",
        LanguageRoleIds022Gate3.Religious => "религиозный",
        LanguageRoleIds022Gate3.Ancient => "древний",
        LanguageRoleIds022Gate3.Contact => "контактный",
        _ => string.IsNullOrWhiteSpace(value) ? "другое назначение" : value
    };
    private static string LanguageLevelLabel(int value) => value switch
    {
        1 => "начальные знания", 2 => "бытовое владение", 3 => "свободное владение",
        4 => "высокое владение", 5 => "глубокое мастерство", _ => "неизвестен"
    };
    private static int Int(object? value, int fallback = 0) => int.TryParse(Str(value), out var result) ? result : fallback;
    private static long Long(object? value) => long.TryParse(Str(value), out var result) ? result : 0;
    private static bool Bool(object? value) => value is bool flag ? flag : bool.TryParse(Str(value), out var result) && result;
    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}

public sealed class CharacterCreationLanguageRow022Gate3 : INotifyPropertyChanged
{
    private int _level;
    public string LanguageId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
    public int Level { get => _level; set { if (_level == value) return; _level = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class CharacterLanguageGrantProfileChoice022Gate3
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public override string ToString() => Label;
    public static IEnumerable<CharacterLanguageGrantProfileChoice022Gate3> All()
    {
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Custom, "Свободный выбор / другое происхождение");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Lutwein, "Лютвейнская среда");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Rashid, "Рашидская среда Рашид-Аль-Тары");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Tarad, "Тарадская среда Рашид-Аль-Тары");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Lichtenburg, "Лихтенбург");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Bergenby, "Бергенби");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Launtown, "Лаунтаун");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Fugu, "Фугу");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Dzhau, "Танаджау, местная среда");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Istal, "Истактлалли, местная среда");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Nalpa, "Ухунинальпа, местная среда");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Paven, "Мотупавенуа, местная среда");
        yield return New(CharacterLanguageGrantProfileIds022Gate3.Taura, "Фенуатаура, местная среда");
    }
    private static CharacterLanguageGrantProfileChoice022Gate3 New(string id, string label) => new() { Id = id, Label = label };
}

public sealed class OriginChoice02111
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public override string ToString() => DisplayName;
    public static OriginChoice02111 From(Dictionary<string, object> x) => new() { Id = Convert.ToString(x.TryGetValue("originId", out var id) ? id : null) ?? string.Empty, DisplayName = Convert.ToString(x.TryGetValue("displayName", out var name) ? name : null) ?? string.Empty };
}

public sealed class SubtypeChoice02111
{
    public string Id { get; set; } = string.Empty;
    public string OriginId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public override string ToString() => DisplayName;
    public static SubtypeChoice02111 From(Dictionary<string, object> x) => new() { Id = Convert.ToString(x.TryGetValue("subtypeId", out var id) ? id : null) ?? string.Empty, OriginId = Convert.ToString(x.TryGetValue("originId", out var origin) ? origin : null) ?? string.Empty, DisplayName = Convert.ToString(x.TryGetValue("displayName", out var name) ? name : null) ?? string.Empty };
}

public sealed class DraftChoice02111
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Summary => $"{DisplayName} · {Status}";
    public override string ToString() => Summary;
    public static DraftChoice02111 From(Dictionary<string, object> x) => new() { Id = Convert.ToString(x.TryGetValue("draftId", out var id) ? id : null) ?? string.Empty, DisplayName = Convert.ToString(x.TryGetValue("displayName", out var name) ? name : null) ?? "Без имени", Status = Convert.ToString(x.TryGetValue("statusDisplay", out var status) ? status : null) ?? "Черновик" };
}

public sealed class AttributeAllocationRow02111 : INotifyPropertyChanged
{
    private int _allocated;
    private int _origin;
    private int _effective;
    public AttributeAllocationRow02111(string id, string displayName) { Id = id; DisplayName = displayName; }
    public string Id { get; }
    public string DisplayName { get; }
    public string ParentAttributeId { get; private set; } = string.Empty;
    public string ParentDisplayName { get; set; } = string.Empty;
    public string DisplayLabel => string.IsNullOrWhiteSpace(ParentDisplayName) ? DisplayName : $"{ParentDisplayName} → {DisplayName}";
    public int Allocated { get => _allocated; set { _allocated = value; _effective = _allocated + _origin; Changed(nameof(Allocated)); Changed(nameof(Effective)); } }
    public int Origin { get => _origin; set { _origin = value; Changed(nameof(Origin)); } }
    public int Effective { get => _effective; set { _effective = value; Changed(nameof(Effective)); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public static AttributeAllocationRow02111 From(Dictionary<string, object> map, string idKey)
    {
        var row = new AttributeAllocationRow02111(Convert.ToString(map.TryGetValue(idKey, out var id) ? id : null) ?? string.Empty, Convert.ToString(map.TryGetValue("displayName", out var name) ? name : null) ?? string.Empty);
        row.ParentAttributeId = Convert.ToString(map.TryGetValue("parentAttributeId", out var parent) ? parent : null) ?? string.Empty;
        return row;
    }
}
