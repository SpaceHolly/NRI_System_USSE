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

namespace Nri.AdminClient.ViewModels;

public sealed class AdminCraftingViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "default";
    private string _ruleSetId = "default";
    private string _statusMessage = "Крафт MVP: задайте CampaignId/RuleSetId и обновите рецепты или проекты.";
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private CraftingRecipeUiItem? _selectedRecipe;
    private CraftingProjectUiItem? _selectedProject;
    private CraftingReservationUiItem? _selectedReservation;

    public AdminCraftingViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(RefreshAll);
        CreateRecipeCommand = new RelayCommand(CreateRecipe);
        ArchiveRecipeCommand = new RelayCommand(ArchiveSelectedRecipe);
        CreateProjectCommand = new RelayCommand(CreateProject);
        StartProjectCommand = new RelayCommand(() => ProjectAction(_api.CraftingProjectStart, "Проект запущен."));
        AddProgressCommand = new RelayCommand(AddProgress);
        CompleteProjectCommand = new RelayCommand(() => ProjectAction(_api.CraftingProjectComplete, "Проект завершён."));
        CancelProjectCommand = new RelayCommand(() => ProjectAction(_api.CraftingProjectCancel, "Проект отменён."));
        FailProjectCommand = new RelayCommand(() => ProjectAction(_api.CraftingProjectFail, "Проект провален."));
        PreviewReservationsCommand = new RelayCommand(PreviewReservations);
        ReserveResourceCommand = new RelayCommand(ReserveResource);
        ReleaseReservationCommand = new RelayCommand(ReleaseReservation);
        ConsumeReservationsCommand = new RelayCommand(ConsumeReservations);
        PrepareResultCommand = new RelayCommand(PrepareResult);
        AcceptResultCommand = new RelayCommand(AcceptResult);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<CraftingRecipeUiItem> Recipes { get; } = new();
    public ObservableCollection<CraftingProjectUiItem> Projects { get; } = new();
    public ObservableCollection<CraftingReservationUiItem> ReservationPreview { get; } = new();
    public ObservableCollection<string> ProjectDetails { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand CreateRecipeCommand { get; }
    public ICommand ArchiveRecipeCommand { get; }
    public ICommand CreateProjectCommand { get; }
    public ICommand StartProjectCommand { get; }
    public ICommand AddProgressCommand { get; }
    public ICommand CompleteProjectCommand { get; }
    public ICommand CancelProjectCommand { get; }
    public ICommand FailProjectCommand { get; }
    public ICommand PreviewReservationsCommand { get; }
    public ICommand ReserveResourceCommand { get; }
    public ICommand ReleaseReservationCommand { get; }
    public ICommand ConsumeReservationsCommand { get; }
    public ICommand PrepareResultCommand { get; }
    public ICommand AcceptResultCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string RuleSetId { get => _ruleSetId; set { if (_ruleSetId != value) { _ruleSetId = value; Notify(); } } }
    public string NewRecipeName { get; set; } = "Новый рецепт";
    public string NewRecipeOutputName { get; set; } = "Результат крафта";
    public string NewRecipeOutputType { get; set; } = "inventory_item";
    public int NewRecipeOutputQuantity { get; set; } = 1;
    public string ProjectOwnerUserId { get; set; } = string.Empty;
    public string ProjectActorEntityType { get; set; } = "character";
    public string ProjectActorEntityId { get; set; } = string.Empty;
    public string ProjectTargetCharacterId { get; set; } = string.Empty;
    public decimal ProgressAmount { get; set; } = 10m;
    public string ReserveItemInstanceId { get; set; } = string.Empty;
    public int ReserveQuantity { get; set; } = 1;
    public string ResultItemDefinitionId { get; set; } = string.Empty;
    public string ResultItemName { get; set; } = "Созданный предмет";
    public int ResultQuantity { get; set; } = 1;

    public CraftingRecipeUiItem? SelectedRecipe
    {
        get => _selectedRecipe;
        set { if (_selectedRecipe != value) { _selectedRecipe = value; Notify(); } }
    }

    public CraftingProjectUiItem? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (_selectedProject == value) return;
            _selectedProject = value;
            Notify();
            if (value != null)
            {
                ProjectTargetCharacterId = value.TargetCharacterId;
                Notify(nameof(ProjectTargetCharacterId));
                LoadProjectDetails();
            }
        }
    }

    public CraftingReservationUiItem? SelectedReservation
    {
        get => _selectedReservation;
        set
        {
            if (_selectedReservation == value) return;
            _selectedReservation = value;
            Notify();
            if (value != null)
            {
                ReserveItemInstanceId = value.ItemInstanceId;
                ReserveQuantity = Math.Max(1, value.AvailableQuantity);
                Notify(nameof(ReserveItemInstanceId));
                Notify(nameof(ReserveQuantity));
            }
        }
    }

    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set { if (_isBusy != value) { _isBusy = value; Notify(); } } }

    private void RefreshAll()
    {
        Run("crafting.refresh", () =>
        {
            LoadRecipes();
            LoadProjects();
            StatusMessage = $"Крафт обновлён: рецептов {Recipes.Count}, проектов {Projects.Count}.";
        });
    }

    private void LoadRecipes()
    {
        var response = _api.CraftingRecipeList(new Dictionary<string, object> { { "campaignId", CampaignId }, { "includeArchived", false } });
        if (!EnsureOk(response, "Не удалось загрузить рецепты крафта.")) return;
        Recipes.Clear();
        foreach (var item in Items(response.Payload))
        {
            var map = Dict(item);
            if (map == null) continue;
            Recipes.Add(CraftingRecipeUiItem.From(map));
        }
        SelectedRecipe ??= Recipes.FirstOrDefault();
    }

    private void LoadProjects()
    {
        var response = _api.CraftingProjectList(new Dictionary<string, object> { { "campaignId", CampaignId }, { "includeArchived", false } });
        if (!EnsureOk(response, "Не удалось загрузить крафт-проекты.")) return;
        Projects.Clear();
        foreach (var item in Items(response.Payload))
        {
            var map = Dict(item);
            if (map == null) continue;
            Projects.Add(CraftingProjectUiItem.From(map));
        }
        SelectedProject ??= Projects.FirstOrDefault();
    }

    private void CreateRecipe()
    {
        Run("crafting.recipe.create", () =>
        {
            var response = _api.CraftingRecipeCreate(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "ruleSetId", RuleSetId },
                { "name", NewRecipeName },
                { "outputName", NewRecipeOutputName },
                { "outputType", NewRecipeOutputType },
                { "outputQuantity", NewRecipeOutputQuantity },
                { "isPlayerVisible", false },
                { "visibilityMode", "gm_only" }
            });
            if (!EnsureOk(response, "Не удалось создать рецепт.")) return;
            LoadRecipes();
            StatusMessage = "Рецепт создан.";
        });
    }

    private void ArchiveSelectedRecipe()
    {
        if (SelectedRecipe == null) return;
        Run("crafting.recipe.archive", () =>
        {
            var response = _api.CraftingRecipeArchive(new Dictionary<string, object> { { "recipeId", SelectedRecipe.RecipeId } });
            if (!EnsureOk(response, "Не удалось архивировать рецепт.")) return;
            SelectedRecipe = null;
            LoadRecipes();
            StatusMessage = "Рецепт архивирован.";
        });
    }

    private void CreateProject()
    {
        if (SelectedRecipe == null) return;
        Run("crafting.project.create", () =>
        {
            var response = _api.CraftingProjectCreate(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "recipeId", SelectedRecipe.RecipeId },
                { "ownerUserId", ProjectOwnerUserId },
                { "actorEntityType", ProjectActorEntityType },
                { "actorEntityId", ProjectActorEntityId },
                { "targetInventoryCharacterId", ProjectTargetCharacterId },
                { "isPlayerVisible", true },
                { "visibilityMode", "party" }
            });
            if (!EnsureOk(response, "Не удалось создать крафт-проект.")) return;
            LoadProjects();
            StatusMessage = "Крафт-проект создан.";
        });
    }

    private void ProjectAction(Func<Dictionary<string, object>, ResponseEnvelope> action, string doneMessage)
    {
        if (SelectedProject == null) return;
        Run("crafting.project.action", () =>
        {
            var response = action(new Dictionary<string, object> { { "craftingProjectId", SelectedProject.CraftingProjectId } });
            if (!EnsureOk(response, "Не удалось изменить статус проекта.")) return;
            LoadProjects();
            StatusMessage = doneMessage;
        });
    }

    private void AddProgress()
    {
        if (SelectedProject == null) return;
        Run("crafting.project.progress", () =>
        {
            var response = _api.CraftingProjectProgressAdd(new Dictionary<string, object>
            {
                { "craftingProjectId", SelectedProject.CraftingProjectId },
                { "amount", ProgressAmount },
                { "summary", "GM progress update" }
            });
            if (!EnsureOk(response, "Не удалось добавить прогресс.")) return;
            LoadProjects();
            StatusMessage = "Прогресс добавлен.";
        });
    }

    private void PreviewReservations()
    {
        if (SelectedProject == null) return;
        Run("crafting.reservation.preview", () =>
        {
            var response = _api.CraftingReservationPreview(new Dictionary<string, object>
            {
                { "craftingProjectId", SelectedProject.CraftingProjectId },
                { "characterId", FirstNonEmpty(ProjectTargetCharacterId, SelectedProject.TargetCharacterId) }
            });
            if (!EnsureOk(response, "Не удалось получить доступность ресурсов.")) return;
            ReservationPreview.Clear();
            foreach (var item in Items(response.Payload))
            {
                var map = Dict(item);
                if (map == null) continue;
                ReservationPreview.Add(CraftingReservationUiItem.From(map));
            }
            StatusMessage = $"Доступность ресурсов обновлена: {ReservationPreview.Count}.";
        });
    }

    private void ReserveResource()
    {
        if (SelectedProject == null) return;
        Run("crafting.reservation.reserve", () =>
        {
            var response = _api.CraftingReservationReserve(new Dictionary<string, object>
            {
                { "craftingProjectId", SelectedProject.CraftingProjectId },
                { "characterId", FirstNonEmpty(ProjectTargetCharacterId, SelectedProject.TargetCharacterId) },
                { "itemInstanceId", ReserveItemInstanceId },
                { "quantity", ReserveQuantity }
            });
            if (!EnsureOk(response, "Не удалось зарезервировать ресурс.")) return;
            PreviewReservations();
            LoadProjectDetails();
            StatusMessage = "Ресурс зарезервирован.";
        });
    }

    private void ReleaseReservation()
    {
        if (SelectedReservation == null) return;
        Run("crafting.reservation.release", () =>
        {
            var response = _api.CraftingReservationRelease(new Dictionary<string, object> { { "reservationId", SelectedReservation.ReservationId } });
            if (!EnsureOk(response, "Не удалось снять резерв.")) return;
            PreviewReservations();
            LoadProjectDetails();
            StatusMessage = "Резерв снят.";
        });
    }

    private void ConsumeReservations()
    {
        if (SelectedProject == null) return;
        Run("crafting.reservation.consume", () =>
        {
            var response = _api.CraftingReservationConsume(new Dictionary<string, object> { { "craftingProjectId", SelectedProject.CraftingProjectId } });
            if (!EnsureOk(response, "Не удалось списать ресурсы.")) return;
            LoadProjectDetails();
            StatusMessage = "Зарезервированные ресурсы списаны.";
        });
    }

    private void PrepareResult()
    {
        if (SelectedProject == null) return;
        Run("crafting.result.prepare", () =>
        {
            var response = _api.CraftingResultPrepare(new Dictionary<string, object>
            {
                { "craftingProjectId", SelectedProject.CraftingProjectId },
                { "targetCharacterId", FirstNonEmpty(ProjectTargetCharacterId, SelectedProject.TargetCharacterId) },
                { "itemDefinitionId", ResultItemDefinitionId },
                { "itemName", ResultItemName },
                { "quantity", ResultQuantity }
            });
            if (!EnsureOk(response, "Не удалось подготовить результат.")) return;
            LoadProjectDetails();
            StatusMessage = "Результат подготовлен.";
        });
    }

    private void AcceptResult()
    {
        if (SelectedProject == null) return;
        Run("crafting.result.accept", () =>
        {
            var response = _api.CraftingResultAccept(new Dictionary<string, object>
            {
                { "craftingProjectId", SelectedProject.CraftingProjectId },
                { "targetCharacterId", FirstNonEmpty(ProjectTargetCharacterId, SelectedProject.TargetCharacterId) },
                { "consumeResources", false }
            });
            if (!EnsureOk(response, "Не удалось принять результат.")) return;
            LoadProjectDetails();
            LoadProjects();
            StatusMessage = "Результат принят и добавлен в профильный инвентарь.";
        });
    }

    private void LoadProjectDetails()
    {
        ProjectDetails.Clear();
        if (SelectedProject == null) return;
        var response = _api.CraftingProjectGet(new Dictionary<string, object> { { "craftingProjectId", SelectedProject.CraftingProjectId } });
        if (!EnsureOk(response, "Не удалось загрузить карточку проекта.")) return;
        var item = Dict(response.Payload.TryGetValue("item", out var raw) ? raw : response.Payload);
        if (item == null) return;
        ProjectDetails.Add($"ID: {SelectedProject.CraftingProjectId}");
        ProjectDetails.Add($"Статус: {Str(Get(item, "status"), "—")}");
        ProjectDetails.Add($"Прогресс: {Str(Get(item, "progressPercent"), "0")}%");
        ProjectDetails.Add($"Результат: {Str(Get(item, "resultStatus"), "—")}");
        foreach (var reservation in ItemsByKey(item, "reservations"))
        {
            var map = Dict(reservation);
            if (map == null) continue;
            ProjectDetails.Add($"Резерв: {Str(Get(map, "itemName"), Str(Get(map, "itemInstanceId"), "item"))} x{Str(Get(map, "quantityReserved"), "0")} | {Str(Get(map, "status"), "—")}");
        }
        foreach (var result in ItemsByKey(item, "results"))
        {
            var map = Dict(result);
            if (map == null) continue;
            ProjectDetails.Add($"Итог: {Str(Get(map, "itemName"), "предмет")} x{Str(Get(map, "quantity"), "1")} | {Str(Get(map, "status"), "—")}");
        }
    }

    private void Run(string operation, Action action)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            ClientLogService.Instance.Info($"admin.crafting.{operation}.start");
            action();
            ClientLogService.Instance.Info($"admin.crafting.{operation}.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Error($"admin.crafting.{operation}.error", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool EnsureOk(ResponseEnvelope response, string fallback)
    {
        if (response.Status == ResponseStatus.Ok) return true;
        ErrorMessage = string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
        StatusMessage = fallback;
        return false;
    }

    private static IEnumerable<object> Items(Dictionary<string, object> payload)
        => ItemsByKey(payload, "items");

    private static IEnumerable<object> ItemsByKey(Dictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var raw) || raw == null) return Array.Empty<object>();
        if (raw is IEnumerable enumerable && raw is not string) return enumerable.Cast<object>().ToArray();
        return Array.Empty<object>();
    }

    private static Dictionary<string, object>? Dict(object? value)
    {
        if (value is Dictionary<string, object> map) return map;
        return null;
    }

    private static object? Get(Dictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) ? value : null;

    private static string Str(object? value, string fallback = "")
        => value == null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;

    private static decimal Dec(object? value, decimal fallback = 0m)
        => decimal.TryParse(Str(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static int Int(object? value, int fallback = 0)
        => int.TryParse(Str(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class CraftingRecipeUiItem
{
    public string RecipeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OutputName { get; set; } = string.Empty;
    public int OutputQuantity { get; set; }
    public string VisibilityMode { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string Summary => $"{Name} → {OutputName} x{OutputQuantity}";
    public string VisibilitySummary => IsPlayerVisible ? $"Игрокам: да ({VisibilityMode})" : $"Игрокам: нет ({VisibilityMode})";

    public static CraftingRecipeUiItem From(Dictionary<string, object> map) => new()
    {
        RecipeId = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "recipeId"), AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "id"))),
        Name = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "name"), "Рецепт"),
        OutputName = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "outputName"), "результат"),
        OutputQuantity = AdminCraftingViewModelString.Int(AdminCraftingViewModelString.Get(map, "outputQuantity"), 1),
        VisibilityMode = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "visibilityMode"), "gm_only"),
        IsPlayerVisible = AdminCraftingViewModelString.Bool(AdminCraftingViewModelString.Get(map, "isPlayerVisible"), false)
    };
}

public sealed class CraftingProjectUiItem
{
    public string CraftingProjectId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ProgressPercent { get; set; }
    public string TargetCharacterId { get; set; } = string.Empty;
    public string ResultStatus { get; set; } = string.Empty;
    public string Summary => $"{RecipeName} | {Status} | {ProgressPercent:0.#}%";

    public static CraftingProjectUiItem From(Dictionary<string, object> map) => new()
    {
        CraftingProjectId = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "craftingProjectId"), AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "id"))),
        ProjectId = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "projectId")),
        RecipeName = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "recipeName"), AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "recipeId"), "Крафт-проект")),
        Status = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "status"), "draft"),
        ProgressPercent = AdminCraftingViewModelString.Dec(AdminCraftingViewModelString.Get(map, "progressPercent"), 0m),
        TargetCharacterId = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "targetInventoryCharacterId")),
        ResultStatus = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "resultStatus"), "draft")
    };
}

public sealed class CraftingReservationUiItem
{
    public string ReservationId { get; set; } = string.Empty;
    public string ItemInstanceId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public string Summary => $"{ItemName} | доступно {AvailableQuantity} из {Quantity} | резерв {ReservedQuantity}";

    public static CraftingReservationUiItem From(Dictionary<string, object> map) => new()
    {
        ReservationId = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "reservationId"), AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "id"))),
        ItemInstanceId = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "itemInstanceId")),
        ItemName = AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "itemName"), AdminCraftingViewModelString.Str(AdminCraftingViewModelString.Get(map, "name"), "Предмет")),
        Quantity = AdminCraftingViewModelString.Int(AdminCraftingViewModelString.Get(map, "quantity"), 0),
        ReservedQuantity = AdminCraftingViewModelString.Int(AdminCraftingViewModelString.Get(map, "reservedQuantity"), 0),
        AvailableQuantity = AdminCraftingViewModelString.Int(AdminCraftingViewModelString.Get(map, "availableQuantity"), 0)
    };
}

internal static class AdminCraftingViewModelString
{
    public static object? Get(Dictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) ? value : null;

    public static string Str(object? value, string fallback = "")
        => value == null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;

    public static int Int(object? value, int fallback = 0)
        => int.TryParse(Str(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    public static decimal Dec(object? value, decimal fallback = 0m)
        => decimal.TryParse(Str(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    public static bool Bool(object? value, bool fallback = false)
        => bool.TryParse(Str(value), out var parsed) ? parsed : fallback;
}
