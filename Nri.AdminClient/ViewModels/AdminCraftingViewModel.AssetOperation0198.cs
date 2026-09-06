using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed partial class AdminCraftingViewModel
{
    private AdminAssetOperationItem0198? _selectedAdminAssetOperation0198;
    private AdminAssetOperationRequirement0198? _selectedAdminAssetRequirement0198;
    private AdminAssetReferenceOption0198? _selectedAdminAssetSpecialist0198;
    private AdminAssetReferenceOption0198? _selectedAdminAssetLicense0198;

    public ObservableCollection<AdminAssetOperationItem0198> AdminAssetOperations0198 { get; } = new();
    public ObservableCollection<AdminAssetOperationRequirement0198> AdminAssetOperationRequirements0198 { get; } = new();
    public ObservableCollection<string> AdminAssetServiceHistory0198 { get; } = new();
    public ObservableCollection<AdminAssetReferenceOption0198> AdminAssetSpecialists0198 { get; } = new();
    public ObservableCollection<AdminAssetReferenceOption0198> AdminAssetLicenses0198 { get; } = new();

    public ICommand ConfirmAssetOperationRequirementCommand0198 { get; private set; } = null!;
    public ICommand ActivateAssetOperationCommand0198 { get; private set; } = null!;
    public ICommand MarkAssetMaintenanceDueCommand0198 { get; private set; } = null!;
    public ICommand RefreshAssetOperationCommand0198 { get; private set; } = null!;
    public ICommand SaveAssetOperationReferencesCommand0198 { get; private set; } = null!;

    public AdminAssetReferenceOption0198? SelectedAdminAssetSpecialist0198
    {
        get => _selectedAdminAssetSpecialist0198;
        set { if (_selectedAdminAssetSpecialist0198 != value) { _selectedAdminAssetSpecialist0198 = value; Notify(); } }
    }

    public AdminAssetReferenceOption0198? SelectedAdminAssetLicense0198
    {
        get => _selectedAdminAssetLicense0198;
        set { if (_selectedAdminAssetLicense0198 != value) { _selectedAdminAssetLicense0198 = value; Notify(); } }
    }

    public AdminAssetOperationItem0198? SelectedAdminAssetOperation0198
    {
        get => _selectedAdminAssetOperation0198;
        set
        {
            if (_selectedAdminAssetOperation0198 == value) return;
            _selectedAdminAssetOperation0198 = value;
            Notify();
            if (value != null && !value.IsPlaceholder) LoadAssetOperationAdmin0198();
        }
    }

    public AdminAssetOperationRequirement0198? SelectedAdminAssetRequirement0198
    {
        get => _selectedAdminAssetRequirement0198;
        set { if (_selectedAdminAssetRequirement0198 != value) { _selectedAdminAssetRequirement0198 = value; Notify(); } }
    }

    private void InitializeAssetOperationAdmin0198()
    {
        ConfirmAssetOperationRequirementCommand0198 = new RelayCommand(ConfirmAssetOperationRequirement0198);
        ActivateAssetOperationCommand0198 = new RelayCommand(ActivateAssetOperation0198);
        MarkAssetMaintenanceDueCommand0198 = new RelayCommand(MarkAssetMaintenanceDue0198);
        RefreshAssetOperationCommand0198 = new RelayCommand(RefreshAssetOperationAdmin0198);
        SaveAssetOperationReferencesCommand0198 = new RelayCommand(SaveAssetOperationReferences0198);
    }

    private void RefreshAssetOperationAdmin0198()
    {
        Run(() =>
        {
            var selected = SelectedAdminAssetOperation0198?.AssetId;
            var response = _api.AssetOperationList(new Dictionary<string, object> { ["campaignId"] = AssetMaintenanceCampaignId0198 });
            EnsureOk(response);
            AdminAssetOperations0198.Clear();
            foreach (var row in OperationRows0198(response.Payload, "items")) AdminAssetOperations0198.Add(AdminAssetOperationItem0198.From(row));
            if (AdminAssetOperations0198.Count == 0) AdminAssetOperations0198.Add(AdminAssetOperationItem0198.Placeholder("Крупные здания пока не созданы."));
            SelectedAdminAssetOperation0198 = AdminAssetOperations0198.FirstOrDefault(x => x.AssetId == selected) ?? AdminAssetOperations0198.FirstOrDefault(x => !x.IsPlaceholder);
        });
    }

    private void LoadAssetOperationAdmin0198()
    {
        Run(() =>
        {
            var response = _api.AssetOperationGet(new Dictionary<string, object> { ["assetId"] = SelectedAdminAssetOperation0198!.AssetId });
            EnsureOk(response);
            var item = OperationMap0198(response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedAdminAssetOperation0198.Apply(item);
            AdminAssetOperationRequirements0198.Clear();
            foreach (var row in OperationRows0198(item, "requirements")) AdminAssetOperationRequirements0198.Add(AdminAssetOperationRequirement0198.From(row));
            AdminAssetServiceHistory0198.Clear();
            foreach (var row in OperationRows0198(item, "serviceHistory")) AdminAssetServiceHistory0198.Add($"{OperationRead0198(row, "summary")} — {OperationRead0198(row, "specialistName")}");
            if (AdminAssetServiceHistory0198.Count == 0) AdminAssetServiceHistory0198.Add("История обслуживания пока пуста.");
            LoadAssetReferenceOptions0198();
            StatusMessage = SelectedAdminAssetOperation0198.ReadinessStatusLabel;
            Notify(nameof(SelectedAdminAssetOperation0198));
        });
    }

    private void LoadAssetReferenceOptions0198()
    {
        var response = _api.AssetOperationReferenceOptions(new Dictionary<string, object> { ["assetId"] = SelectedAdminAssetOperation0198!.AssetId });
        EnsureOk(response);
        AdminAssetSpecialists0198.Clear();
        foreach (var row in OperationRows0198(response.Payload, "specialists")) AdminAssetSpecialists0198.Add(AdminAssetReferenceOption0198.From(row));
        AdminAssetLicenses0198.Clear();
        foreach (var row in OperationRows0198(response.Payload, "licenses")) AdminAssetLicenses0198.Add(AdminAssetReferenceOption0198.From(row));
        SelectedAdminAssetSpecialist0198 = AdminAssetSpecialists0198.FirstOrDefault(x => x.DisplayName == SelectedAdminAssetOperation0198.SpecialistName);
        SelectedAdminAssetLicense0198 = AdminAssetLicenses0198.FirstOrDefault();
    }

    private void SaveAssetOperationReferences0198()
    {
        if (SelectedAdminAssetSpecialist0198 == null || SelectedAdminAssetLicense0198 == null)
        { ErrorMessage = "Выберите активного NPC-специалиста и действующий документ владельца."; return; }
        MutateAssetOperationAdmin0198(_api.AssetOperationReferencesUpdate, "Специалист и документ назначены.", payload =>
        {
            payload["specialistCharacterId"] = SelectedAdminAssetSpecialist0198.Id;
            payload["licenseId"] = SelectedAdminAssetLicense0198.Id;
        });
    }

    private void ConfirmAssetOperationRequirement0198()
    {
        if (SelectedAdminAssetOperation0198 == null || SelectedAdminAssetOperation0198.IsPlaceholder || SelectedAdminAssetRequirement0198 == null)
        { ErrorMessage = "Выберите актив и ручное условие."; return; }
        MutateAssetOperationAdmin0198(_api.AssetOperationRequirementConfirm, "Условие эксплуатации подтверждено.", payload => payload["requirementKind"] = SelectedAdminAssetRequirement0198.Kind);
    }

    private void ActivateAssetOperation0198()
    {
        if (!Confirm("Ввести выбранный актив в эксплуатацию после проверки всех условий?")) return;
        MutateAssetOperationAdmin0198(_api.AssetOperationActivate, "Актив введён в эксплуатацию.");
    }

    private void MarkAssetMaintenanceDue0198()
    {
        if (!Confirm("Отметить наступление срока обслуживания и ограничить эксплуатацию?")) return;
        MutateAssetOperationAdmin0198(_api.AssetMaintenanceMarkDue, "Срок обслуживания наступил; эксплуатация ограничена.");
    }

    private void MutateAssetOperationAdmin0198(Func<Dictionary<string, object>, ResponseEnvelope> command, string success, Action<Dictionary<string, object>>? extend = null)
    {
        if (SelectedAdminAssetOperation0198 == null || SelectedAdminAssetOperation0198.IsPlaceholder) { ErrorMessage = "Выберите крупный актив."; return; }
        Run(() =>
        {
            var payload = new Dictionary<string, object> { ["assetId"] = SelectedAdminAssetOperation0198.AssetId, ["expectedRevision"] = SelectedAdminAssetOperation0198.Revision, ["operationId"] = Guid.NewGuid().ToString("N") };
            extend?.Invoke(payload);
            EnsureOk(command(payload));
            StatusMessage = success;
        });
        RefreshAssetOperationAdmin0198();
    }

    private static Dictionary<string, object> OperationMap0198(object? raw)
    {
        if (raw is Dictionary<string, object> map) return map;
        if (raw is IDictionary<string, object> typed) return typed.ToDictionary(x => x.Key, x => x.Value);
        if (raw is IDictionary loose) return loose.Keys.Cast<object>().ToDictionary(x => Convert.ToString(x) ?? string.Empty, x => loose[x]!);
        return new Dictionary<string, object>();
    }

    private static IEnumerable<Dictionary<string, object>> OperationRows0198(IDictionary<string, object> parent, string key)
    {
        if (!parent.TryGetValue(key, out var raw) || raw is not IEnumerable rows || raw is string) yield break;
        foreach (var row in rows) { var map = OperationMap0198(row); if (map.Count > 0) yield return map; }
    }

    private static string OperationRead0198(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
}

public sealed class AdminAssetOperationItem0198
{
    public string AssetId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string OwnerDisplayName { get; private set; } = string.Empty;
    public string LocationName { get; private set; } = string.Empty;
    public string OperationStatusLabel { get; private set; } = string.Empty;
    public string MaintenanceStatusLabel { get; private set; } = string.Empty;
    public string ReadinessStatusLabel { get; private set; } = string.Empty;
    public string SpecialistName { get; private set; } = string.Empty;
    public int Revision { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => $"{Name} — {OperationStatusLabel}; {MaintenanceStatusLabel}";
    public string Details => $"Владелец: {OwnerDisplayName}\nМесто: {LocationName}\nГотовность: {ReadinessStatusLabel}\nСпециалист: {SpecialistName}";
    public void Apply(IDictionary<string, object> map) { AssetId = Read(map, "assetId"); Name = Read(map, "name"); OwnerDisplayName = Read(map, "ownerDisplayName"); LocationName = Read(map, "locationName"); OperationStatusLabel = Read(map, "operationStatusLabel"); MaintenanceStatusLabel = Read(map, "maintenanceStatusLabel"); ReadinessStatusLabel = Read(map, "readinessStatusLabel"); SpecialistName = Read(map, "specialistName"); Revision = int.TryParse(Read(map, "revision"), out var revision) ? revision : 0; }
    public static AdminAssetOperationItem0198 From(IDictionary<string, object> map) { var item = new AdminAssetOperationItem0198(); item.Apply(map); return item; }
    public static AdminAssetOperationItem0198 Placeholder(string text) => new() { Name = text, IsPlaceholder = true };
    private static string Read(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
}

public sealed class AdminAssetOperationRequirement0198
{
    public string Kind { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string ResolutionLabel { get; private set; } = string.Empty;
    public string ReferenceDisplayName { get; private set; } = string.Empty;
    public string Display => $"{Name}: {StatusLabel} · {ResolutionLabel}" + (string.IsNullOrWhiteSpace(ReferenceDisplayName) ? string.Empty : " · " + ReferenceDisplayName);
    public static AdminAssetOperationRequirement0198 From(IDictionary<string, object> map) => new() { Kind = Read(map, "kind"), Name = Read(map, "name"), StatusLabel = Read(map, "statusLabel"), ResolutionLabel = Read(map, "resolutionLabel"), ReferenceDisplayName = Read(map, "referenceDisplayName") };
    private static string Read(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
}

public sealed class AdminAssetReferenceOption0198
{
    public string Id { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Display => string.IsNullOrWhiteSpace(Description) ? DisplayName : $"{DisplayName} — {Description}";
    public static AdminAssetReferenceOption0198 From(IDictionary<string, object> map) => new()
    {
        Id = OperationRead(map, "id"),
        DisplayName = OperationRead(map, "displayName"),
        Description = OperationRead(map, "description")
    };
    private static string OperationRead(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
}
