using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminCharacterCreationViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _campaignId;
    private string _policy = CharacterCreationPolicyIds.RequireGmApproval;
    private AdminCreationDraftRow? _selectedPending;
    private AdminCreationOwnerRow? _selectedOwner;
    private AdminCreationOriginRow? _parent1;
    private AdminCreationOriginRow? _parent2;
    private AdminCreationSubtypeRow? _subtype;
    private string _draftId = string.Empty;
    private string _resolvedOriginId = string.Empty;
    private long _revision;
    private long _policyRevision;
    private string _name = string.Empty;
    private string _backstory = string.Empty;
    private int _heightCm = 170;
    private int _ageYears = 24;
    private string _returnComment = string.Empty;
    private string _status = "Ожидание загрузки";
    private string _resolvedOrigin = "Происхождение не выбрано";
    private string _originDescription = "Выберите линии родителей и выполните проверку.";
    private string _validation = "Предпросмотр ещё не выполнен.";
    private bool _isBusy;
    private bool _isValid;
    private bool _isStructuralMode;
    private AdminCreationCharacterRow? _selectedStructuralCharacter;
    private long _structuralRevision;
    private string _structuralReason = string.Empty;
    private string _initialDevelopmentClassRule = "Правила не загружены.";
    private string _initialDevelopmentMagicRule = "Правила не загружены.";
    private string _initialDevelopmentSessionRule = "Правила не загружены.";
    private string _initialDevelopmentResetReason = string.Empty;

    public AdminCharacterCreationViewModel(CommandApi api, Func<string> campaignId)
    {
        _api = api;
        _campaignId = campaignId;
        Policies = new ObservableCollection<AdminCreationOptionRow>(new[]
        {
            new AdminCreationOptionRow(CharacterCreationPolicyIds.Free, "Свободное создание"),
            new AdminCreationOptionRow(CharacterCreationPolicyIds.RequireGmApproval, "Требуется одобрение GM"),
            new AdminCreationOptionRow(CharacterCreationPolicyIds.GmOnly, "Создание выполняет GM")
        });
        AttributeOptions = new ObservableCollection<int>(new[] { -2, -1, 0, 1, 2 });
        LoadCommand = new RelayCommand(Load);
        SavePolicyCommand = new RelayCommand(SavePolicy);
        NewDirectCommand = new RelayCommand(NewDirect);
        SaveDirectCommand = new RelayCommand(SaveDirect);
        PreviewCommand = new RelayCommand(Preview);
        FinalizeDirectCommand = new RelayCommand(FinalizeDirect);
        ApproveCommand = new RelayCommand(ApproveSelected);
        ReturnCommand = new RelayCommand(ReturnSelected);
        LoadStructuralCommand = new RelayCommand(LoadStructural);
        ApplyStructuralCommand = new RelayCommand(ApplyStructural);
        ResetInitialDevelopmentCommand = new RelayCommand(ResetInitialDevelopment);
    }

    public ObservableCollection<AdminCreationOptionRow> Policies { get; }
    public ObservableCollection<AdminCreationOwnerRow> Owners { get; } = new();
    public ObservableCollection<AdminCreationOriginRow> ParentOrigins { get; } = new();
    public ObservableCollection<AdminCreationSubtypeRow> AllSubtypes { get; } = new();
    public ObservableCollection<AdminCreationSubtypeRow> AvailableSubtypes { get; } = new();
    public ObservableCollection<AdminCreationDraftRow> PendingDrafts { get; } = new();
    public ObservableCollection<AdminCreationCharacterRow> ExistingCharacters { get; } = new();
    public ObservableCollection<AdminCreationValueRow> Attributes { get; } = new();
    public ObservableCollection<AdminCreationValueRow> SubAttributes { get; } = new();
    public ObservableCollection<string> StrongSides { get; } = new();
    public ObservableCollection<string> WeakSides { get; } = new();
    public ObservableCollection<string> Traits { get; } = new();
    public ObservableCollection<string> Languages { get; } = new();
    public ObservableCollection<string> StructuralImpactItems { get; } = new();
    public ObservableCollection<int> AttributeOptions { get; }
    public ICommand LoadCommand { get; }
    public ICommand SavePolicyCommand { get; }
    public ICommand NewDirectCommand { get; }
    public ICommand SaveDirectCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand FinalizeDirectCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand ReturnCommand { get; }
    public ICommand LoadStructuralCommand { get; }
    public ICommand ApplyStructuralCommand { get; }
    public ICommand ResetInitialDevelopmentCommand { get; }

    public string Policy { get => _policy; set { _policy = value ?? CharacterCreationPolicyIds.RequireGmApproval; Notify(); } }
    public AdminCreationDraftRow? SelectedPending { get => _selectedPending; set { _selectedPending = value; Notify(); if (value != null) OpenPending(value); } }
    public AdminCreationOwnerRow? SelectedOwner { get => _selectedOwner; set { _selectedOwner = value; Notify(); } }
    public AdminCreationOriginRow? Parent1 { get => _parent1; set { _parent1 = value; Notify(); if (_parent2 == null) Parent2 = value; UpdateSubtypes(); Invalidate(); } }
    public AdminCreationOriginRow? Parent2 { get => _parent2; set { _parent2 = value; Notify(); UpdateSubtypes(); Invalidate(); } }
    public AdminCreationSubtypeRow? Subtype { get => _subtype; set { _subtype = value; Notify(); Invalidate(); } }
    public string Name { get => _name; set { _name = value ?? string.Empty; Notify(); Invalidate(); } }
    public string Backstory { get => _backstory; set { _backstory = value ?? string.Empty; Notify(); Invalidate(); } }
    public int HeightCm { get => _heightCm; set { _heightCm = value; Notify(); Invalidate(); } }
    public int AgeYears { get => _ageYears; set { _ageYears = value; Notify(); Invalidate(); } }
    public string ReturnComment { get => _returnComment; set { _returnComment = value ?? string.Empty; Notify(); } }
    public string Status { get => _status; private set { _status = value; Notify(); } }
    public string ResolvedOrigin { get => _resolvedOrigin; private set { _resolvedOrigin = value; Notify(); } }
    public string OriginDescription { get => _originDescription; private set { _originDescription = value; Notify(); } }
    public string Validation { get => _validation; private set { _validation = value; Notify(); } }
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; Notify(); } }
    public bool IsValid { get => _isValid; private set { _isValid = value; Notify(); } }
    public bool HasPending => PendingDrafts.Count > 0;
    public bool IsStructuralMode { get => _isStructuralMode; private set { _isStructuralMode = value; Notify(); } }
    public AdminCreationCharacterRow? SelectedStructuralCharacter { get => _selectedStructuralCharacter; set { _selectedStructuralCharacter = value; Notify(); } }
    public string StructuralReason { get => _structuralReason; set { _structuralReason = value ?? string.Empty; Notify(); } }
    public string StructuralImpactSummary { get; private set; } = "Откройте структурный профиль, чтобы оценить последствия.";
    public string InitialDevelopmentClassRule { get => _initialDevelopmentClassRule; private set { _initialDevelopmentClassRule = value; Notify(); } }
    public string InitialDevelopmentMagicRule { get => _initialDevelopmentMagicRule; private set { _initialDevelopmentMagicRule = value; Notify(); } }
    public string InitialDevelopmentSessionRule { get => _initialDevelopmentSessionRule; private set { _initialDevelopmentSessionRule = value; Notify(); } }
    public string InitialDevelopmentResetReason { get => _initialDevelopmentResetReason; set { _initialDevelopmentResetReason = value ?? string.Empty; Notify(); } }

    public void Load()
    {
        var campaignId = _campaignId();
        if (string.IsNullOrWhiteSpace(campaignId)) { Status = "Сначала выберите кампанию."; return; }
        IsBusy = true;
        try
        {
            var policy = Require(_api.CharacterCreationPolicyGet(BasePayload()));
            Policy = Text(policy.Payload, "policy");
            _policyRevision = Number64(policy.Payload, "entityRevision");
            ApplyDefinitions(Require(_api.CharacterCreationDefinitionsList(BasePayload())).Payload);
            ApplyExistingCharacters(Require(_api.GetAllCharacters()).Payload);
            ApplyInitialDevelopmentPolicy(Require(_api.InitialDevelopmentAdminPolicyGet(BasePayload())).Payload);
            RefreshPending();
            Status = "Рабочее место создания персонажей загружено.";
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { IsBusy = false; }
    }

    private void SavePolicy()
    {
        try
        {
            var response = Require(_api.CharacterCreationPolicyUpdate(new Dictionary<string, object>
            {
                ["campaignId"] = _campaignId(), ["policy"] = Policy, ["expectedRevision"] = _policyRevision,
                ["playerMayRenameFinalized"] = true, ["playerMayEditFinalizedBackstory"] = true
            }));
            _policyRevision = Number64(response.Payload, "entityRevision");
            Status = response.Message;
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    private void RefreshPending()
    {
        var response = Require(_api.CharacterCreationAdminPending(BasePayload()));
        PendingDrafts.Clear();
        foreach (var map in Maps(Value(response.Payload, "items"))) PendingDrafts.Add(AdminCreationDraftRow.From(map));
        Notify(nameof(HasPending));
    }

    private void NewDirect()
    {
        _draftId = string.Empty; _resolvedOriginId = string.Empty; _revision = 0; SelectedPending = null; IsStructuralMode = false;
        Name = string.Empty; Backstory = string.Empty; Parent1 = null; Parent2 = null; Subtype = null;
        HeightCm = 170; AgeYears = 24; IsValid = false; ResolvedOrigin = "Происхождение не выбрано";
        ResetAllocations(); Validation = "Новый GM-черновик. Выберите владельца и заполните обязательные поля."; Status = "Режим прямого создания.";
    }

    private void SaveDirect()
    {
        if (SelectedOwner == null) { Status = "Выберите владельца персонажа."; return; }
        try
        {
            var response = Require(_api.CharacterCreationDraftSave(BuildPayload()));
            ApplyDraft(Map(Value(response.Payload, "draft")));
            ApplyPreview(Map(Value(response.Payload, "preview")));
            Status = response.Message;
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    private void Preview()
    {
        if (string.IsNullOrWhiteSpace(_draftId)) { SaveDirect(); return; }
        try { ApplyPreview(Require(_api.CharacterCreationPreview(new Dictionary<string, object> { ["draftId"] = _draftId })).Payload); Status = "Предпросмотр обновлён."; }
        catch (Exception ex) { Status = ex.Message; }
    }

    private void FinalizeDirect()
    {
        if (!IsValid || string.IsNullOrWhiteSpace(_draftId)) { Status = "Сначала сохраните и успешно проверьте черновик."; return; }
        try { Status = Require(_api.CharacterCreationFinalize(new Dictionary<string, object> { ["draftId"] = _draftId, ["expectedRevision"] = _revision, ["operationId"] = Guid.NewGuid().ToString("N") })).Message; NewDirect(); RefreshPending(); }
        catch (Exception ex) { Status = ex.Message; }
    }

    private void OpenPending(AdminCreationDraftRow row)
    {
        IsStructuralMode = false;
        _draftId = row.Id; _revision = row.Revision; Name = row.Name; Backstory = row.Backstory;
        SelectedOwner = Owners.FirstOrDefault(x => x.Id == row.OwnerId);
        Parent1 = ParentOrigins.FirstOrDefault(x => x.Id == row.Parent1Id); Parent2 = ParentOrigins.FirstOrDefault(x => x.Id == row.Parent2Id);
        _resolvedOriginId = row.ResolvedOriginId; UpdateSubtypes(); Subtype = AvailableSubtypes.FirstOrDefault(x => x.Id == row.SubtypeId);
        HeightCm = row.HeightCm; AgeYears = row.AgeYears;
        ApplyAllocation(Attributes, row.Attributes); ApplyAllocation(SubAttributes, row.SubAttributes);
        Preview();
    }

    private void ApproveSelected()
    {
        if (SelectedPending == null) { Status = "Выберите заявку."; return; }
        try { Status = Require(_api.CharacterCreationFinalize(new Dictionary<string, object> { ["draftId"] = SelectedPending.Id, ["expectedRevision"] = SelectedPending.Revision, ["operationId"] = Guid.NewGuid().ToString("N") })).Message; RefreshPending(); NewDirect(); }
        catch (Exception ex) { Status = ex.Message; }
    }

    private void ReturnSelected()
    {
        if (SelectedPending == null) { Status = "Выберите заявку."; return; }
        if (ReturnComment.Trim().Length < 3) { Status = "Укажите понятный комментарий игроку."; return; }
        try { Status = Require(_api.CharacterCreationAdminReturn(new Dictionary<string, object> { ["draftId"] = SelectedPending.Id, ["comment"] = ReturnComment.Trim() })).Message; ReturnComment = string.Empty; RefreshPending(); NewDirect(); }
        catch (Exception ex) { Status = ex.Message; }
    }

    private void LoadStructural()
    {
        if (SelectedStructuralCharacter == null) { Status = "Выберите персонажа для структурного изменения."; return; }
        try
        {
            var response = Require(_api.CharacterStructuralEditPreview(new Dictionary<string, object> { ["characterId"] = SelectedStructuralCharacter.Id }));
            var map = response.Payload;
            IsStructuralMode = true; _draftId = string.Empty; SelectedPending = null;
            Name = SelectedStructuralCharacter.Name;
            Parent1 = ParentOrigins.FirstOrDefault(x => x.Id == Text(map, "parent1RaceId"));
            Parent2 = ParentOrigins.FirstOrDefault(x => x.Id == Text(map, "parent2RaceId"));
            _resolvedOriginId = Text(map, "resolvedOriginId"); UpdateSubtypes(); Subtype = AvailableSubtypes.FirstOrDefault(x => x.Id == Text(map, "subtypeId"));
            HeightCm = Number(map, "heightCm"); AgeYears = Number(map, "ageYears"); _structuralRevision = Number64(map, "entityRevision");
            ApplyPreview(map); Status = "Структурный профиль загружен. Изменения ещё не сохранены.";
            ApplyStructuralImpact(map);
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    private void ApplyStructural()
    {
        if (!IsStructuralMode || SelectedStructuralCharacter == null) { Status = "Откройте структурный профиль персонажа."; return; }
        if (StructuralReason.Trim().Length < 5) { Status = "Укажите причину структурного изменения."; return; }
        try
        {
            var payload = new Dictionary<string, object>
            {
                ["characterId"] = SelectedStructuralCharacter.Id, ["expectedRevision"] = _structuralRevision, ["reason"] = StructuralReason.Trim(),
                ["parent1RaceId"] = Parent1?.Id ?? string.Empty, ["parent2RaceId"] = Parent2?.Id ?? string.Empty, ["subtypeId"] = Subtype?.Id ?? string.Empty,
                ["heightCm"] = HeightCm, ["ageYears"] = AgeYears
            };
            var response = Require(_api.CharacterStructuralEditApply(payload));
            _structuralRevision = Number64(response.Payload, "entityRevision");
            ApplyPreview(Map(Value(response.Payload, "preview")));
            Status = response.Message; StructuralReason = string.Empty;
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    private void ApplyInitialDevelopmentPolicy(Dictionary<string, object> payload)
    {
        InitialDevelopmentClassRule = First(Text(payload, "classRule"), "Правило выбора классов не настроено.");
        InitialDevelopmentMagicRule = First(Text(payload, "magicRule"), "Правило выбора магии не настроено.");
        InitialDevelopmentSessionRule = Flag(payload, "mustCompleteBeforeActiveSession")
            ? "До завершения стартового пакета персонаж не может участвовать в активной сессии."
            : "Незавершённый стартовый пакет не блокирует участие в сессии.";
    }

    private void ResetInitialDevelopment()
    {
        if (SelectedStructuralCharacter == null) { Status = "Выберите персонажа для сброса начального развития."; return; }
        if (InitialDevelopmentResetReason.Trim().Length < 5) { Status = "Укажите причину сброса длиной не менее 5 символов."; return; }
        try
        {
            var response = Require(_api.InitialDevelopmentAdminReset(new Dictionary<string, object>
            {
                ["characterId"] = SelectedStructuralCharacter.Id,
                ["reason"] = InitialDevelopmentResetReason.Trim()
            }));
            InitialDevelopmentResetReason = string.Empty;
            Status = response.Message;
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    private Dictionary<string, object> BuildPayload() => new()
    {
        ["campaignId"] = _campaignId(), ["ruleSetId"] = RuleSetIds.FantasyNriDefault, ["draftId"] = _draftId, ["expectedRevision"] = _revision,
        ["ownerUserId"] = SelectedOwner?.Id ?? string.Empty, ["displayName"] = Name, ["backstory"] = Backstory,
        ["parent1RaceId"] = Parent1?.Id ?? string.Empty, ["parent2RaceId"] = Parent2?.Id ?? string.Empty, ["subtypeId"] = Subtype?.Id ?? string.Empty,
        ["heightCm"] = HeightCm, ["ageYears"] = AgeYears,
        ["attributeAllocation"] = Attributes.ToDictionary(x => x.Id, x => (object)x.Allocated),
        ["subAttributeAllocation"] = SubAttributes.ToDictionary(x => x.Id, x => (object)x.Allocated)
    };

    private Dictionary<string, object> BasePayload() => new() { ["campaignId"] = _campaignId(), ["ruleSetId"] = RuleSetIds.FantasyNriDefault };

    private void ApplyDefinitions(Dictionary<string, object> payload)
    {
        ParentOrigins.Clear(); AllSubtypes.Clear(); Attributes.Clear(); SubAttributes.Clear();
        foreach (var map in Maps(Value(payload, "origins")).Where(x => Text(x, "originKind") == CharacterOriginKinds.Race)) ParentOrigins.Add(AdminCreationOriginRow.From(map));
        foreach (var map in Maps(Value(payload, "subtypes"))) AllSubtypes.Add(AdminCreationSubtypeRow.From(map));
        foreach (var map in Maps(Value(payload, "attributeDefinitions"))) Attributes.Add(AdminCreationValueRow.From(map, "attributeId"));
        foreach (var map in Maps(Value(payload, "subAttributeDefinitions"))) SubAttributes.Add(AdminCreationValueRow.From(map, "subAttributeId"));
        Owners.Clear();
        foreach (var map in Maps(Value(payload, "eligibleOwners"))) Owners.Add(new AdminCreationOwnerRow(Text(map, "ownerUserId"), Text(map, "displayName"), "Участник кампании"));
        if (Attributes.Count == 0)
        {
            var values = new[] { (CharacterAttributeIds.Strength, "Сила"), (CharacterAttributeIds.Dexterity, "Ловкость"), (CharacterAttributeIds.Endurance, "Выносливость"), (CharacterAttributeIds.Intellect, "Интеллект"), (CharacterAttributeIds.Wisdom, "Мудрость"), (CharacterAttributeIds.Charisma, "Харизма") };
            foreach (var value in values) Attributes.Add(new AdminCreationValueRow(value.Item1, value.Item2));
        }
        ResetAllocations();
    }

    private void ApplyExistingCharacters(Dictionary<string, object> payload)
    {
        ExistingCharacters.Clear();
        foreach (var map in Maps(Value(payload, "items")))
            ExistingCharacters.Add(new AdminCreationCharacterRow(Text(map, "characterId"), First(Text(map, "name"), "Персонаж без имени")));
    }

    private void ApplyDraft(Dictionary<string, object> map)
    {
        _draftId = Text(map, "draftId"); _revision = Number64(map, "entityRevision"); _resolvedOriginId = Text(map, "resolvedOriginId");
        Name = Text(map, "displayName"); Backstory = Text(map, "backstory");
        UpdateSubtypes();
    }

    private void ApplyPreview(Dictionary<string, object> map)
    {
        IsValid = Flag(map, "isValid"); _resolvedOriginId = Text(map, "resolvedOriginId"); UpdateSubtypes();
        ResolvedOrigin = First(Text(map, "resolvedOriginName"), "Происхождение не определено");
        OriginDescription = First(Text(map, "description"), "Публичное описание отсутствует.");
        Replace(StrongSides, Strings(Value(map, "strongSides"))); Replace(WeakSides, Strings(Value(map, "weakSides")));
        Replace(Traits, Strings(Value(map, "traits"))); Replace(Languages, Strings(Value(map, "languages")));
        ApplyBreakdown(Attributes, Map(Value(map, "attributeBreakdown"))); ApplyBreakdown(SubAttributes, Map(Value(map, "subAttributeBreakdown")));
        var errors = Strings(Value(map, "errors")).ToArray(); Validation = errors.Length == 0 ? "Проверка пройдена. Персонажа можно создать." : string.Join(" ", errors);
    }

    private void ApplyStructuralImpact(Dictionary<string, object> map)
    {
        StructuralImpactSummary = First(Text(map, "impactSummary"), "Последствия не рассчитаны.");
        Replace(StructuralImpactItems, Strings(Value(map, "impactItems")));
        Notify(nameof(StructuralImpactSummary));
    }

    private void UpdateSubtypes()
    {
        var selected = Subtype?.Id; AvailableSubtypes.Clear();
        var originId = Parent1 != null && Parent2 != null && Parent1.Id == Parent2.Id ? Parent1.Id : _resolvedOriginId;
        foreach (var item in AllSubtypes.Where(x => x.OriginId == originId)) AvailableSubtypes.Add(item);
        _subtype = AvailableSubtypes.FirstOrDefault(x => x.Id == selected); Notify(nameof(Subtype));
    }

    private void ResetAllocations() { var preset = new[] { 2, 1, 0, 0, -1, -2 }; for (var i = 0; i < Attributes.Count; i++) Attributes[i].Allocated = i < preset.Length ? preset[i] : 0; foreach (var x in SubAttributes) x.Allocated = 0; }
    private void Invalidate() { IsValid = false; Validation = "Есть несохранённые изменения. Сохраните черновик и обновите предпросмотр."; }
    private static void ApplyAllocation(IEnumerable<AdminCreationValueRow> rows, Dictionary<string, int> values) { foreach (var row in rows) if (values.TryGetValue(row.Id, out var value)) row.Allocated = value; }
    private static void ApplyBreakdown(IEnumerable<AdminCreationValueRow> rows, Dictionary<string, object> values) { foreach (var row in rows) if (values.TryGetValue(row.Id, out var raw)) { var map = Map(raw); row.Origin = Number(map, "origin"); row.Effective = Number(map, "effective"); } }
    private static ResponseEnvelope Require(ResponseEnvelope response) { if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(response.Message); return response; }
    private static object? Value(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static Dictionary<string, object> Map(object? value) => value as Dictionary<string, object> ?? new Dictionary<string, object>();
    private static IEnumerable<Dictionary<string, object>> Maps(object? value) { if (value is IEnumerable values && value is not string) foreach (var item in values) if (item is Dictionary<string, object> map) yield return map; }
    private static IEnumerable<string> Strings(object? value) { if (value is IEnumerable values && value is not string) foreach (var item in values) { var text = Convert.ToString(item); if (!string.IsNullOrWhiteSpace(text)) yield return text; } }
    private static string Text(IDictionary<string, object> map, string key) => Convert.ToString(Value(map, key)) ?? string.Empty;
    private static int Number(IDictionary<string, object> map, string key) => int.TryParse(Text(map, key), out var value) ? value : 0;
    private static long Number64(IDictionary<string, object> map, string key) => long.TryParse(Text(map, key), out var value) ? value : 0;
    private static bool Flag(IDictionary<string, object> map, string key) => Value(map, key) is bool value ? value : bool.TryParse(Text(map, key), out value) && value;
    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}

public sealed class AdminCreationOptionRow { public AdminCreationOptionRow(string id, string name) { Id = id; Name = name; } public string Id { get; } public string Name { get; } public override string ToString() => Name; }
public sealed class AdminCreationOwnerRow { public AdminCreationOwnerRow(string id, string login, string status) { Id = id; Login = login; Status = status; } public string Id { get; } public string Login { get; } public string Status { get; } public string Summary => $"{Login} · {Status}"; public override string ToString() => Login; }
public sealed class AdminCreationCharacterRow { public AdminCreationCharacterRow(string id, string name) { Id = id; Name = name; } public string Id { get; } public string Name { get; } public override string ToString() => Name; }
public sealed class AdminCreationOriginRow { public string Id { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public override string ToString() => Name; public static AdminCreationOriginRow From(Dictionary<string, object> x) => new() { Id = Convert.ToString(x.TryGetValue("originId", out var id) ? id : null) ?? string.Empty, Name = Convert.ToString(x.TryGetValue("displayName", out var name) ? name : null) ?? string.Empty }; }
public sealed class AdminCreationSubtypeRow { public string Id { get; set; } = string.Empty; public string OriginId { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public override string ToString() => Name; public static AdminCreationSubtypeRow From(Dictionary<string, object> x) => new() { Id = Convert.ToString(x.TryGetValue("subtypeId", out var id) ? id : null) ?? string.Empty, OriginId = Convert.ToString(x.TryGetValue("originId", out var origin) ? origin : null) ?? string.Empty, Name = Convert.ToString(x.TryGetValue("displayName", out var name) ? name : null) ?? string.Empty }; }
public sealed class AdminCreationValueRow : INotifyPropertyChanged { private int _allocated; private int _origin; private int _effective; public AdminCreationValueRow(string id, string name) { Id = id; Name = name; } public string Id { get; } public string Name { get; } public int Allocated { get => _allocated; set { _allocated = value; Changed(nameof(Allocated)); } } public int Origin { get => _origin; set { _origin = value; Changed(nameof(Origin)); } } public int Effective { get => _effective; set { _effective = value; Changed(nameof(Effective)); } } public event PropertyChangedEventHandler? PropertyChanged; private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); public static AdminCreationValueRow From(Dictionary<string, object> x, string key) => new(Convert.ToString(x.TryGetValue(key, out var id) ? id : null) ?? string.Empty, Convert.ToString(x.TryGetValue("displayName", out var name) ? name : null) ?? string.Empty); }
public sealed class AdminCreationDraftRow
{
    public string Id { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string OwnerId { get; set; } = string.Empty; public string Backstory { get; set; } = string.Empty;
    public string Parent1Id { get; set; } = string.Empty; public string Parent2Id { get; set; } = string.Empty; public string ResolvedOriginId { get; set; } = string.Empty; public string SubtypeId { get; set; } = string.Empty;
    public int HeightCm { get; set; } public int AgeYears { get; set; } public long Revision { get; set; } public string Status { get; set; } = string.Empty;
    public Dictionary<string, int> Attributes { get; set; } = new(); public Dictionary<string, int> SubAttributes { get; set; } = new(); public string Summary => $"{Name} · {Status}";
    public static AdminCreationDraftRow From(Dictionary<string, object> x) => new()
    {
        Id = Convert.ToString(x.TryGetValue("draftId", out var id) ? id : null) ?? string.Empty, Name = Convert.ToString(x.TryGetValue("displayName", out var name) ? name : null) ?? "Без имени",
        OwnerId = Convert.ToString(x.TryGetValue("ownerUserId", out var owner) ? owner : null) ?? string.Empty, Backstory = Convert.ToString(x.TryGetValue("backstory", out var backstory) ? backstory : null) ?? string.Empty,
        Parent1Id = Convert.ToString(x.TryGetValue("parent1RaceId", out var p1) ? p1 : null) ?? string.Empty, Parent2Id = Convert.ToString(x.TryGetValue("parent2RaceId", out var p2) ? p2 : null) ?? string.Empty,
        ResolvedOriginId = Convert.ToString(x.TryGetValue("resolvedOriginId", out var origin) ? origin : null) ?? string.Empty, SubtypeId = Convert.ToString(x.TryGetValue("subtypeId", out var subtype) ? subtype : null) ?? string.Empty,
        HeightCm = int.TryParse(Convert.ToString(x.TryGetValue("heightCm", out var h) ? h : null), out var height) ? height : 0, AgeYears = int.TryParse(Convert.ToString(x.TryGetValue("ageYears", out var a) ? a : null), out var age) ? age : 0,
        Revision = long.TryParse(Convert.ToString(x.TryGetValue("entityRevision", out var r) ? r : null), out var revision) ? revision : 0, Status = Convert.ToString(x.TryGetValue("statusDisplay", out var status) ? status : null) ?? string.Empty,
        Attributes = IntMap(x.TryGetValue("attributeAllocation", out var attributes) ? attributes : null), SubAttributes = IntMap(x.TryGetValue("subAttributeAllocation", out var subAttributes) ? subAttributes : null)
    };
    private static Dictionary<string, int> IntMap(object? value) { var result = new Dictionary<string, int>(); if (value is Dictionary<string, object> map) foreach (var pair in map) if (int.TryParse(Convert.ToString(pair.Value), out var number)) result[pair.Key] = number; return result; }
}
