using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed partial class PlayerProductionViewModel
{
    private PlayerAssetConstructionChoice0197? _selectedConstructionBlueprint0197;
    private PlayerAssetConstructionChoice0197? _selectedConstructionLocation0197;
    private PlayerAssetConstructionProject0197? _selectedConstructionProject0197;
    private string _constructionProjectName0197 = string.Empty;
    private string _constructionState0197 = "Выберите опубликованный чертёж здания и место строительства.";
    private string _constructionPreviewSummary0197 = "Предварительная проверка ещё не выполнялась.";

    public ObservableCollection<PlayerAssetConstructionChoice0197> ConstructionBlueprints0197 { get; } = new();
    public ObservableCollection<PlayerAssetConstructionChoice0197> ConstructionLocations0197 { get; } = new();
    public ObservableCollection<PlayerAssetConstructionProject0197> ConstructionProjects0197 { get; } = new();
    public ObservableCollection<PlayerCraftLine0191> ConstructionRequirements0197 { get; } = new();
    public ObservableCollection<PlayerCraftLine0191> ConstructionResources0197 { get; } = new();
    public ObservableCollection<PlayerCraftLine0191> ConstructionStages0197 { get; } = new();

    public ICommand RefreshConstructionCommand0197 { get; private set; } = null!;
    public ICommand PreviewConstructionCommand0197 { get; private set; } = null!;
    public ICommand CreateConstructionCommand0197 { get; private set; } = null!;
    public ICommand SubmitConstructionCommand0197 { get; private set; } = null!;
    public ICommand CancelConstructionCommand0197 { get; private set; } = null!;

    public PlayerAssetConstructionChoice0197? SelectedConstructionBlueprint0197
    {
        get => _selectedConstructionBlueprint0197;
        set
        {
            if (_selectedConstructionBlueprint0197 == value) return;
            _selectedConstructionBlueprint0197 = value;
            Notify();
            if (value != null && !value.IsPlaceholder)
                ConstructionProjectName0197 = $"Строительство: {value.Name}";
            ClearConstructionPreview0197();
            Notify(nameof(ConstructionSelectionSummary0197));
        }
    }

    public PlayerAssetConstructionChoice0197? SelectedConstructionLocation0197
    {
        get => _selectedConstructionLocation0197;
        set
        {
            if (_selectedConstructionLocation0197 == value) return;
            _selectedConstructionLocation0197 = value;
            Notify();
            ClearConstructionPreview0197();
            Notify(nameof(ConstructionSelectionSummary0197));
        }
    }

    public PlayerAssetConstructionProject0197? SelectedConstructionProject0197
    {
        get => _selectedConstructionProject0197;
        set
        {
            if (_selectedConstructionProject0197 == value) return;
            _selectedConstructionProject0197 = value;
            Notify();
            Notify(nameof(ConstructionAssetSummary0197));
            if (value != null && !value.IsPlaceholder) LoadConstructionProject0197();
        }
    }

    public string ConstructionProjectName0197
    {
        get => _constructionProjectName0197;
        set { if (_constructionProjectName0197 != value) { _constructionProjectName0197 = value; Notify(); Notify(nameof(ConstructionSelectionSummary0197)); } }
    }

    public string ConstructionState0197
    {
        get => _constructionState0197;
        private set { if (_constructionState0197 != value) { _constructionState0197 = value; Notify(); } }
    }

    public string ConstructionPreviewSummary0197
    {
        get => _constructionPreviewSummary0197;
        private set { if (_constructionPreviewSummary0197 != value) { _constructionPreviewSummary0197 = value; Notify(); } }
    }

    public string ConstructionSelectionSummary0197
        => $"Здание: {SelectedConstructionBlueprint0197?.DisplayName ?? "не выбрано"}\nМесто: {SelectedConstructionLocation0197?.DisplayName ?? "не выбрано"}\nПроект: {ConstructionProjectName0197}";

    public string ConstructionAssetSummary0197
        => SelectedConstructionProject0197?.AssetSummary ?? "Готовое здание появится здесь после завершения трёх стадий.";

    private void InitializeAssetConstruction0197()
    {
        RefreshConstructionCommand0197 = new RelayCommand(() => RefreshAssetConstruction0197(false));
        PreviewConstructionCommand0197 = new RelayCommand(PreviewAssetConstruction0197);
        CreateConstructionCommand0197 = new RelayCommand(CreateAssetConstruction0197);
        SubmitConstructionCommand0197 = new RelayCommand(SubmitAssetConstruction0197);
        CancelConstructionCommand0197 = new RelayCommand(CancelAssetConstruction0197);
    }

    private void RefreshAssetConstruction0197(bool silent)
    {
        try
        {
            var characterId = _activeCharacterIdAccessor();
            if (string.IsNullOrWhiteSpace(characterId))
            {
                ConstructionState0197 = "Сначала выберите активного персонажа.";
                return;
            }
            var selectedBlueprint = SelectedConstructionBlueprint0197?.Reference;
            var selectedLocation = SelectedConstructionLocation0197?.Reference;
            var selectedProject = SelectedConstructionProject0197?.ProjectId;
            var available = _api.ProjectAssetConstructionAvailableList(ConstructionBasePayload0197());
            EnsureConstructionOk0197(available);
            FillConstructionChoices0197(available.Payload, "blueprints", ConstructionBlueprints0197, "GM пока не опубликовал готовый чертёж здания.");
            FillConstructionChoices0197(available.Payload, "locations", ConstructionLocations0197, "GM пока не открыл подходящее место строительства.");
            SelectedConstructionBlueprint0197 = ConstructionBlueprints0197.FirstOrDefault(x => x.Reference == selectedBlueprint)
                                                   ?? ConstructionBlueprints0197.FirstOrDefault(x => !x.IsPlaceholder);
            SelectedConstructionLocation0197 = ConstructionLocations0197.FirstOrDefault(x => x.Reference == selectedLocation)
                                                  ?? ConstructionLocations0197.FirstOrDefault(x => !x.IsPlaceholder);

            var projects = _api.ProjectAssetConstructionList(ConstructionBasePayload0197());
            EnsureConstructionOk0197(projects);
            ConstructionProjects0197.Clear();
            foreach (var item in ConstructionItems0197(projects.Payload, "items"))
                ConstructionProjects0197.Add(PlayerAssetConstructionProject0197.From(item));
            if (ConstructionProjects0197.Count == 0)
                ConstructionProjects0197.Add(PlayerAssetConstructionProject0197.Placeholder("У вас пока нет проектов строительства."));
            var nextProject = ConstructionProjects0197.FirstOrDefault(x => x.ProjectId == selectedProject)
                              ?? ConstructionProjects0197.FirstOrDefault(x => !x.IsPlaceholder);
            if (_selectedConstructionProject0197 != nextProject)
            {
                _selectedConstructionProject0197 = nextProject;
                Notify(nameof(SelectedConstructionProject0197));
                Notify(nameof(ConstructionAssetSummary0197));
            }
            if (nextProject != null && !nextProject.IsPlaceholder)
                LoadConstructionProject0197();
            ConstructionState0197 = "Строительные проекты обновлены.";
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error("player.assetConstruction.refresh.error", ex);
            ConstructionState0197 = ex.Message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Строительство крупных активов выключено feature flags."
                : "Строительство крупных активов пока недоступно.";
            if (!silent) ErrorMessage = ex.Message;
        }
    }

    private void PreviewAssetConstruction0197()
    {
        if (!RequireConstructionSelection0197()) return;
        try
        {
            var response = _api.ProjectAssetConstructionPreview(ConstructionSelectionPayload0197());
            EnsureConstructionOk0197(response);
            var preview = CraftMap0191(response.Payload.TryGetValue("preview", out var raw) ? raw : null);
            FillCraftLines0191(preview, "requirements", ConstructionRequirements0197);
            FillConstructionStagePreview0197(preview);
            ConstructionPreviewSummary0197 = string.Join("\n", new[]
            {
                PlayerCraftParsing0191.Read(preview, "configurationSummary"),
                $"Место: {PlayerCraftParsing0191.Read(preview, "locationName")}",
                $"Персонал: {PlayerCraftParsing0191.Read(preview, "personnelSummary")}",
                $"Оборудование: {PlayerCraftParsing0191.Read(preview, "facilitySummary")}",
                PlayerCraftParsing0191.Read(preview, "warning")
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
            ConstructionState0197 = "Требования и этапы рассчитаны сервером. Ресурсы ещё не зарезервированы.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; ConstructionState0197 = "Предварительная проверка не выполнена."; }
    }

    private void CreateAssetConstruction0197()
    {
        if (!RequireConstructionSelection0197()) return;
        if (string.IsNullOrWhiteSpace(ConstructionProjectName0197)) { ErrorMessage = "Укажите понятное название проекта."; return; }
        try
        {
            var payload = ConstructionSelectionPayload0197();
            payload["name"] = ConstructionProjectName0197.Trim();
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsureConstructionOk0197(_api.ProjectAssetConstructionCreate(payload));
            ConstructionState0197 = "Черновик строительства создан. Материалы не зарезервированы.";
            RefreshAssetConstruction0197(false);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; ConstructionState0197 = "Черновик не создан."; }
    }

    private void SubmitAssetConstruction0197()
    {
        if (!RequireConstructionProject0197()) return;
        if (MessageBox.Show("Отправить проект строительства GM?", "Строительство актива", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        MutateConstruction0197(_api.ProjectAssetConstructionSubmit, "Проект отправлен GM.");
    }

    private void CancelAssetConstruction0197()
    {
        if (!RequireConstructionProject0197()) return;
        if (MessageBox.Show("Отменить строительство? Уже использованные материалы не возвращаются.", "Строительство актива", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        MutateConstruction0197(_api.ProjectAssetConstructionCancel, "Строительство отменено; неиспользованные резервы освобождены.");
    }

    private void MutateConstruction0197(Func<Dictionary<string, object>, ResponseEnvelope> command, string success)
    {
        try
        {
            var payload = ConstructionBasePayload0197();
            payload["projectId"] = SelectedConstructionProject0197!.ProjectId;
            payload["expectedRevision"] = SelectedConstructionProject0197.Revision;
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsureConstructionOk0197(command(payload));
            ConstructionState0197 = success;
            RefreshAssetConstruction0197(false);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; ConstructionState0197 = "Действие не выполнено."; }
    }

    private void LoadConstructionProject0197()
    {
        try
        {
            var response = _api.ProjectAssetConstructionGet(new Dictionary<string, object> { ["projectId"] = SelectedConstructionProject0197!.ProjectId });
            EnsureConstructionOk0197(response);
            var item = CraftMap0191(response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedConstructionProject0197.Apply(item);
            FillCraftLines0191(item, "requirements", ConstructionRequirements0197);
            FillCraftLines0191(item, "resources", ConstructionResources0197);
            FillCraftLines0191(item, "stages", ConstructionStages0197);
            ConstructionPreviewSummary0197 = SelectedConstructionProject0197.DetailSummary;
            ConstructionState0197 = SelectedConstructionProject0197.StatusLabel;
            Notify(nameof(SelectedConstructionProject0197));
            Notify(nameof(ConstructionAssetSummary0197));
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error("player.assetConstruction.details.error", ex);
            ErrorMessage = ex.Message;
            ConstructionState0197 = "Не удалось открыть строительный проект.";
        }
    }

    private bool RequireConstructionSelection0197()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(_activeCharacterIdAccessor())) { ErrorMessage = "Сначала выберите активного персонажа."; return false; }
        if (SelectedConstructionBlueprint0197 == null || SelectedConstructionBlueprint0197.IsPlaceholder) { ErrorMessage = "Выберите опубликованный чертёж здания."; return false; }
        if (SelectedConstructionLocation0197 == null || SelectedConstructionLocation0197.IsPlaceholder) { ErrorMessage = "Выберите доступное место строительства."; return false; }
        return true;
    }

    private bool RequireConstructionProject0197()
    {
        if (SelectedConstructionProject0197 != null && !SelectedConstructionProject0197.IsPlaceholder) return true;
        ErrorMessage = "Выберите строительный проект.";
        return false;
    }

    private Dictionary<string, object> ConstructionBasePayload0197()
        => new() { ["characterId"] = _activeCharacterIdAccessor() };

    private Dictionary<string, object> ConstructionSelectionPayload0197()
    {
        var payload = ConstructionBasePayload0197();
        payload["blueprintId"] = SelectedConstructionBlueprint0197!.Reference;
        payload["locationId"] = SelectedConstructionLocation0197!.Reference;
        return payload;
    }

    private void ClearConstructionPreview0197()
    {
        ConstructionRequirements0197.Clear();
        ConstructionResources0197.Clear();
        ConstructionStages0197.Clear();
        ConstructionPreviewSummary0197 = "Нажмите «Проверить требования», чтобы получить server-side расчёт.";
    }

    private void FillConstructionStagePreview0197(IDictionary<string, object> preview)
    {
        ConstructionStages0197.Clear();
        ConstructionResources0197.Clear();
        foreach (var stage in ConstructionItems0197(preview, "stages"))
        {
            ConstructionStages0197.Add(PlayerCraftLine0191.From(stage));
            foreach (var resource in ConstructionItems0197(stage, "resources"))
            {
                resource["summary"] = $"Стадия: {PlayerCraftParsing0191.Read(stage, "name")}";
                ConstructionResources0197.Add(PlayerCraftLine0191.From(resource));
            }
        }
    }

    private static void FillConstructionChoices0197(IDictionary<string, object> parent, string key,
        ObservableCollection<PlayerAssetConstructionChoice0197> target, string empty)
    {
        target.Clear();
        foreach (var item in ConstructionItems0197(parent, key)) target.Add(PlayerAssetConstructionChoice0197.From(item));
        if (target.Count == 0) target.Add(PlayerAssetConstructionChoice0197.Placeholder(empty));
    }

    private static IEnumerable<Dictionary<string, object>> ConstructionItems0197(IDictionary<string, object> parent, string key)
    {
        if (!parent.TryGetValue(key, out var raw) || raw is not IEnumerable rows || raw is string) yield break;
        foreach (var row in rows)
        {
            var item = CraftMap0191(row);
            if (item.Count > 0) yield return item;
        }
    }

    private static void EnsureConstructionOk0197(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? "Строительство крупных активов недоступно." : response.Message);
    }
}

public sealed class PlayerAssetConstructionChoice0197
{
    public string Reference { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string TypeLabel { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public bool IsPlaceholder { get; private set; }
    public string DisplayName => IsPlaceholder ? Name : Name;
    public string DisplaySummary => IsPlaceholder ? Name : string.Join(" · ", new[] { TypeLabel, StatusLabel, Summary }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public static PlayerAssetConstructionChoice0197 From(IDictionary<string, object> map) => new()
    {
        Reference = PlayerCraftParsing0191.Read(map, "reference"),
        Name = PlayerCraftParsing0191.First(PlayerCraftParsing0191.Read(map, "name"), "Без названия"),
        Summary = PlayerCraftParsing0191.Read(map, "summary"),
        TypeLabel = PlayerCraftParsing0191.First(PlayerCraftParsing0191.Read(map, "typeLabel"), PlayerCraftParsing0191.Read(map, "type")),
        StatusLabel = PlayerCraftParsing0191.Read(map, "statusLabel")
    };

    public static PlayerAssetConstructionChoice0197 Placeholder(string text) => new() { Name = text, IsPlaceholder = true };
}

public sealed class PlayerAssetConstructionProject0197
{
    public string ProjectId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string BlueprintName { get; private set; } = string.Empty;
    public string LocationName { get; private set; } = string.Empty;
    public string ConfigurationSummary { get; private set; } = string.Empty;
    public string SiteStatusLabel { get; private set; } = string.Empty;
    public string CurrentStageName { get; private set; } = string.Empty;
    public string AssetSummary { get; private set; } = string.Empty;
    public int ProgressPercent { get; private set; }
    public int Revision { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => IsPlaceholder ? Name : $"{Name}\n{StatusLabel} · {ProgressPercent}%\n{LocationName}";
    public string DetailSummary => $"{BlueprintName}\n{ConfigurationSummary}\nМесто: {LocationName}\nПлощадка: {SiteStatusLabel}";

    public static PlayerAssetConstructionProject0197 From(IDictionary<string, object> map) { var item = new PlayerAssetConstructionProject0197(); item.Apply(map); return item; }

    public void Apply(IDictionary<string, object> map)
    {
        ProjectId = PlayerCraftParsing0191.Read(map, "projectId");
        Name = PlayerCraftParsing0191.First(PlayerCraftParsing0191.Read(map, "name"), "Строительство актива");
        StatusLabel = PlayerCraftParsing0191.Read(map, "statusLabel");
        BlueprintName = PlayerCraftParsing0191.Read(map, "blueprintName");
        LocationName = PlayerCraftParsing0191.Read(map, "locationName");
        ConfigurationSummary = PlayerCraftParsing0191.Read(map, "configurationSummary");
        SiteStatusLabel = PlayerCraftParsing0191.Read(map, "siteStatusLabel");
        CurrentStageName = PlayerCraftParsing0191.Read(map, "currentStageName");
        ProgressPercent = PlayerCraftParsing0191.ReadInt(map, "progressPercent");
        Revision = PlayerCraftParsing0191.ReadInt(map, "revision");
        var asset = PlayerProductionViewModel.CraftMap0191(map.TryGetValue("asset", out var rawAsset) ? rawAsset : null);
        AssetSummary = asset.Count == 0 ? string.Empty : string.Join("\n", new[]
        {
            PlayerCraftParsing0191.Read(asset, "name"),
            PlayerCraftParsing0191.Read(asset, "statusLabel"),
            $"Место: {PlayerCraftParsing0191.Read(asset, "locationName")}",
            PlayerCraftParsing0191.Read(asset, "configurationSummary"),
            $"Прочность: {PlayerCraftParsing0191.Read(asset, "structuralIntegrity")}",
            PlayerCraftParsing0191.Read(asset, "energyProfile")
        }.Where(x => !string.IsNullOrWhiteSpace(x) && !x.EndsWith(": ", StringComparison.Ordinal)));
    }

    public static PlayerAssetConstructionProject0197 Placeholder(string text) => new() { Name = text, IsPlaceholder = true };
}
