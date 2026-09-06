using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed partial class PlayerProductionViewModel
{
    private PlayerAssetOperationItem0198? _selectedAssetOperation0198;
    private PlayerAssetMaintenanceProject0198? _selectedAssetMaintenanceProject0198;
    private string _assetMaintenanceState0198 = "Выберите принадлежащее персонажу здание.";
    private string _assetMaintenanceProjectName0198 = "Плановое обслуживание";

    public ObservableCollection<PlayerAssetOperationItem0198> AssetOperations0198 { get; } = new();
    public ObservableCollection<PlayerAssetMaintenanceProject0198> AssetMaintenanceProjects0198 { get; } = new();
    public ObservableCollection<string> AssetOperationRequirements0198 { get; } = new();
    public ObservableCollection<string> AssetServiceHistory0198 { get; } = new();
    public ObservableCollection<string> AssetMaintenanceRequirements0198 { get; } = new();
    public ObservableCollection<string> AssetMaintenanceResources0198 { get; } = new();
    public ObservableCollection<string> AssetMaintenanceStages0198 { get; } = new();

    public ICommand RefreshAssetMaintenanceCommand0198 { get; private set; } = null!;
    public ICommand RequestAssetActivationCommand0198 { get; private set; } = null!;
    public ICommand CreateAssetMaintenanceCommand0198 { get; private set; } = null!;
    public ICommand SubmitAssetMaintenanceCommand0198 { get; private set; } = null!;
    public ICommand CancelAssetMaintenanceCommand0198 { get; private set; } = null!;

    public PlayerAssetOperationItem0198? SelectedAssetOperation0198
    {
        get => _selectedAssetOperation0198;
        set
        {
            if (_selectedAssetOperation0198 == value) return;
            _selectedAssetOperation0198 = value;
            Notify();
            Notify(nameof(AssetOperationSummary0198));
            if (value != null && !value.IsPlaceholder) LoadAssetOperation0198();
        }
    }

    public PlayerAssetMaintenanceProject0198? SelectedAssetMaintenanceProject0198
    {
        get => _selectedAssetMaintenanceProject0198;
        set
        {
            if (_selectedAssetMaintenanceProject0198 == value) return;
            _selectedAssetMaintenanceProject0198 = value;
            Notify();
            if (value != null && !value.IsPlaceholder) LoadAssetMaintenanceProject0198();
        }
    }

    public string AssetMaintenanceState0198 { get => _assetMaintenanceState0198; private set { if (_assetMaintenanceState0198 != value) { _assetMaintenanceState0198 = value; Notify(); } } }
    public string AssetMaintenanceProjectName0198 { get => _assetMaintenanceProjectName0198; set { if (_assetMaintenanceProjectName0198 != value) { _assetMaintenanceProjectName0198 = value; Notify(); } } }
    public string AssetOperationSummary0198 => SelectedAssetOperation0198?.DetailSummary ?? "Актив не выбран.";

    private void InitializeAssetMaintenance0198()
    {
        RefreshAssetMaintenanceCommand0198 = new RelayCommand(() => RefreshAssetMaintenance0198(false));
        RequestAssetActivationCommand0198 = new RelayCommand(RequestAssetActivation0198);
        CreateAssetMaintenanceCommand0198 = new RelayCommand(CreateAssetMaintenance0198);
        SubmitAssetMaintenanceCommand0198 = new RelayCommand(SubmitAssetMaintenance0198);
        CancelAssetMaintenanceCommand0198 = new RelayCommand(CancelAssetMaintenance0198);
    }

    private void RefreshAssetMaintenance0198(bool silent)
    {
        try
        {
            var characterId = _activeCharacterIdAccessor();
            if (string.IsNullOrWhiteSpace(characterId)) { AssetMaintenanceState0198 = "Сначала выберите активного персонажа."; return; }
            var selectedAsset = SelectedAssetOperation0198?.AssetId;
            var selectedProject = SelectedAssetMaintenanceProject0198?.ProjectId;
            var payload = new Dictionary<string, object> { ["characterId"] = characterId, ["campaignId"] = CampaignId };
            var assets = _api.AssetOperationList(payload);
            EnsureAssetMaintenanceOk0198(assets);
            AssetOperations0198.Clear();
            foreach (var row in Rows0198(assets.Payload, "items")) AssetOperations0198.Add(PlayerAssetOperationItem0198.From(row));
            if (AssetOperations0198.Count == 0) AssetOperations0198.Add(PlayerAssetOperationItem0198.Placeholder("У персонажа нет доступных крупных активов."));
            SelectedAssetOperation0198 = AssetOperations0198.FirstOrDefault(x => x.AssetId == selectedAsset) ?? AssetOperations0198.FirstOrDefault(x => !x.IsPlaceholder);

            var projects = _api.ProjectAssetMaintenanceList(payload);
            EnsureAssetMaintenanceOk0198(projects);
            AssetMaintenanceProjects0198.Clear();
            foreach (var row in Rows0198(projects.Payload, "items")) AssetMaintenanceProjects0198.Add(PlayerAssetMaintenanceProject0198.From(row));
            if (AssetMaintenanceProjects0198.Count == 0) AssetMaintenanceProjects0198.Add(PlayerAssetMaintenanceProject0198.Placeholder("Проекты обслуживания пока не созданы."));
            SelectedAssetMaintenanceProject0198 = AssetMaintenanceProjects0198.FirstOrDefault(x => x.ProjectId == selectedProject) ?? AssetMaintenanceProjects0198.FirstOrDefault(x => !x.IsPlaceholder);
            if (!silent) AssetMaintenanceState0198 = "Состояние эксплуатации обновлено.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            AssetMaintenanceState0198 = ex.Message.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Эксплуатация крупных активов выключена feature flags."
                : "Эксплуатационные данные пока недоступны.";
        }
    }

    private void LoadAssetOperation0198()
    {
        try
        {
            var response = _api.AssetOperationGet(new Dictionary<string, object> { ["assetId"] = SelectedAssetOperation0198!.AssetId });
            EnsureAssetMaintenanceOk0198(response);
            var item = Map0198(response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedAssetOperation0198.Apply(item);
            FillReadableRows0198(item, "requirements", AssetOperationRequirements0198, row => $"{Read0198(row, "name")}: {Read0198(row, "statusLabel")}");
            FillReadableRows0198(item, "serviceHistory", AssetServiceHistory0198, row => $"{Read0198(row, "summary")} — {Read0198(row, "specialistName")}");
            AssetMaintenanceState0198 = SelectedAssetOperation0198.ReadinessStatusLabel;
            Notify(nameof(SelectedAssetOperation0198));
            Notify(nameof(AssetOperationSummary0198));
        }
        catch (Exception ex) { ErrorMessage = ex.Message; AssetMaintenanceState0198 = "Не удалось открыть состояние актива."; }
    }

    private void LoadAssetMaintenanceProject0198()
    {
        try
        {
            var response = _api.ProjectAssetMaintenanceGet(new Dictionary<string, object> { ["projectId"] = SelectedAssetMaintenanceProject0198!.ProjectId });
            EnsureAssetMaintenanceOk0198(response);
            var item = Map0198(response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedAssetMaintenanceProject0198.Apply(item);
            FillReadableRows0198(item, "requirements", AssetMaintenanceRequirements0198, row => $"{Read0198(row, "name")}: {Read0198(row, "statusLabel")}");
            FillReadableRows0198(item, "resources", AssetMaintenanceResources0198, row => $"{Read0198(row, "name")}: {Read0198(row, "quantityRequired")} {Read0198(row, "unit")} — {Read0198(row, "statusLabel")}");
            FillReadableRows0198(item, "stages", AssetMaintenanceStages0198, row => $"{Read0198(row, "name")}: {Read0198(row, "statusLabel")}");
            AssetMaintenanceState0198 = SelectedAssetMaintenanceProject0198.StatusLabel;
            Notify(nameof(SelectedAssetMaintenanceProject0198));
        }
        catch (Exception ex) { ErrorMessage = ex.Message; AssetMaintenanceState0198 = "Не удалось открыть проект обслуживания."; }
    }

    private void RequestAssetActivation0198()
    {
        if (SelectedAssetOperation0198 == null || SelectedAssetOperation0198.IsPlaceholder) { ErrorMessage = "Выберите актив."; return; }
        MutateOperation0198(_api.AssetOperationActivationRequest, "Запрос на ввод в эксплуатацию отправлен GM.");
    }

    private void CreateAssetMaintenance0198()
    {
        if (SelectedAssetOperation0198 == null || SelectedAssetOperation0198.IsPlaceholder) { ErrorMessage = "Выберите актив."; return; }
        if (string.IsNullOrWhiteSpace(AssetMaintenanceProjectName0198)) { ErrorMessage = "Укажите название проекта обслуживания."; return; }
        try
        {
            var response = _api.ProjectAssetMaintenanceCreate(new Dictionary<string, object> { ["assetId"] = SelectedAssetOperation0198.AssetId, ["name"] = AssetMaintenanceProjectName0198.Trim(), ["operationId"] = Guid.NewGuid().ToString("N") });
            EnsureAssetMaintenanceOk0198(response);
            AssetMaintenanceState0198 = "Черновик обслуживания создан.";
            RefreshAssetMaintenance0198(false);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; AssetMaintenanceState0198 = "Черновик не создан."; }
    }

    private void SubmitAssetMaintenance0198()
    {
        if (!RequireMaintenanceProject0198()) return;
        if (MessageBox.Show("Отправить проект обслуживания GM?", "Обслуживание актива", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        MutateProject0198(_api.ProjectAssetMaintenanceSubmit, "Проект обслуживания отправлен GM.");
    }

    private void CancelAssetMaintenance0198()
    {
        if (!RequireMaintenanceProject0198()) return;
        if (MessageBox.Show("Отменить обслуживание до первого списания ресурсов?", "Обслуживание актива", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        MutateProject0198(_api.ProjectAssetMaintenanceCancel, "Проект отменён; неиспользованные резервы освобождены.");
    }

    private void MutateOperation0198(Func<Dictionary<string, object>, ResponseEnvelope> command, string success)
    {
        try
        {
            EnsureAssetMaintenanceOk0198(command(new Dictionary<string, object> { ["assetId"] = SelectedAssetOperation0198!.AssetId, ["expectedRevision"] = SelectedAssetOperation0198.Revision, ["operationId"] = Guid.NewGuid().ToString("N") }));
            AssetMaintenanceState0198 = success;
            RefreshAssetMaintenance0198(false);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; AssetMaintenanceState0198 = "Действие не выполнено."; }
    }

    private void MutateProject0198(Func<Dictionary<string, object>, ResponseEnvelope> command, string success)
    {
        try
        {
            EnsureAssetMaintenanceOk0198(command(new Dictionary<string, object> { ["projectId"] = SelectedAssetMaintenanceProject0198!.ProjectId, ["expectedRevision"] = SelectedAssetMaintenanceProject0198.Revision, ["operationId"] = Guid.NewGuid().ToString("N") }));
            AssetMaintenanceState0198 = success;
            RefreshAssetMaintenance0198(false);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; AssetMaintenanceState0198 = "Действие не выполнено."; }
    }

    private bool RequireMaintenanceProject0198()
    {
        if (SelectedAssetMaintenanceProject0198 != null && !SelectedAssetMaintenanceProject0198.IsPlaceholder) return true;
        ErrorMessage = "Выберите проект обслуживания.";
        return false;
    }

    private static void EnsureAssetMaintenanceOk0198(ResponseEnvelope response)
    { if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? "Эксплуатация активов недоступна." : response.Message); }

    private static Dictionary<string, object> Map0198(object? raw)
    {
        if (raw is Dictionary<string, object> map) return map;
        if (raw is IDictionary<string, object> typed) return typed.ToDictionary(x => x.Key, x => x.Value);
        if (raw is IDictionary loose) return loose.Keys.Cast<object>().ToDictionary(x => Convert.ToString(x) ?? string.Empty, x => loose[x]!);
        return new Dictionary<string, object>();
    }

    private static IEnumerable<Dictionary<string, object>> Rows0198(IDictionary<string, object> parent, string key)
    {
        if (!parent.TryGetValue(key, out var raw) || raw is not IEnumerable rows || raw is string) yield break;
        foreach (var row in rows) { var map = Map0198(row); if (map.Count > 0) yield return map; }
    }

    private static string Read0198(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static int ReadInt0198(IDictionary<string, object> map, string key) => int.TryParse(Read0198(map, key), out var value) ? value : 0;
    private static void FillReadableRows0198(IDictionary<string, object> parent, string key, ObservableCollection<string> target, Func<Dictionary<string, object>, string> format)
    { target.Clear(); foreach (var row in Rows0198(parent, key)) target.Add(format(row)); if (target.Count == 0) target.Add("Нет записей."); }
}

public sealed class PlayerAssetOperationItem0198
{
    public string AssetId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string LocationName { get; private set; } = string.Empty;
    public string OperationStatusLabel { get; private set; } = string.Empty;
    public string MaintenanceStatusLabel { get; private set; } = string.Empty;
    public string ReadinessStatusLabel { get; private set; } = string.Empty;
    public string SpecialistName { get; private set; } = string.Empty;
    public int Revision { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => $"{Name} — {OperationStatusLabel}; {MaintenanceStatusLabel}";
    public string DetailSummary => $"{Name}\nМесто: {LocationName}\nЭксплуатация: {OperationStatusLabel}\nОбслуживание: {MaintenanceStatusLabel}\nГотовность: {ReadinessStatusLabel}\nСпециалист: {SpecialistName}";
    public void Apply(IDictionary<string, object> map) { AssetId = Read(map, "assetId"); Name = Read(map, "name"); LocationName = Read(map, "locationName"); OperationStatusLabel = Read(map, "operationStatusLabel"); MaintenanceStatusLabel = Read(map, "maintenanceStatusLabel"); ReadinessStatusLabel = Read(map, "readinessStatusLabel"); SpecialistName = Read(map, "specialistName"); Revision = int.TryParse(Read(map, "revision"), out var revision) ? revision : 0; }
    public static PlayerAssetOperationItem0198 From(IDictionary<string, object> map) { var item = new PlayerAssetOperationItem0198(); item.Apply(map); return item; }
    public static PlayerAssetOperationItem0198 Placeholder(string text) => new() { Name = text, IsPlaceholder = true };
    private static string Read(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
}

public sealed class PlayerAssetMaintenanceProject0198
{
    public string ProjectId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string AssetName { get; private set; } = string.Empty;
    public string CurrentStageName { get; private set; } = string.Empty;
    public int ProgressPercent { get; private set; }
    public int Revision { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => $"{Name} — {StatusLabel}";
    public void Apply(IDictionary<string, object> map) { ProjectId = Read(map, "projectId"); Name = Read(map, "name"); StatusLabel = Read(map, "statusLabel"); AssetName = Read(map, "assetName"); CurrentStageName = Read(map, "currentStageName"); ProgressPercent = int.TryParse(Read(map, "progressPercent"), out var progress) ? progress : 0; Revision = int.TryParse(Read(map, "revision"), out var revision) ? revision : 0; }
    public static PlayerAssetMaintenanceProject0198 From(IDictionary<string, object> map) { var item = new PlayerAssetMaintenanceProject0198(); item.Apply(map); return item; }
    public static PlayerAssetMaintenanceProject0198 Placeholder(string text) => new() { Name = text, IsPlaceholder = true };
    private static string Read(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
}
