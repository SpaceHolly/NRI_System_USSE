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
    private PlayerCraftRecipeItem0191? _selectedCraftRecipe0191;
    private PlayerCraftProjectItem0191? _selectedCraftProject0191;
    private string _craftProjectName0191 = string.Empty;
    private string _craftState0191 = "Выберите рецепт и проверьте требования.";
    private string _craftExpectedDuration0191 = "Расчётное время появится после проверки условий.";

    public ObservableCollection<PlayerCraftRecipeItem0191> CraftRecipes0191 { get; } = new();
    public ObservableCollection<PlayerCraftProjectItem0191> CraftProjects0191 { get; } = new();
    public ObservableCollection<PlayerCraftLine0191> CraftRequirements0191 { get; } = new();
    public ObservableCollection<PlayerCraftLine0191> CraftResources0191 { get; } = new();
    public ObservableCollection<PlayerCraftLine0191> CraftStages0191 { get; } = new();

    public ICommand RefreshCraftCommand0191 { get; private set; } = null!;
    public ICommand PreviewCraftCommand0191 { get; private set; } = null!;
    public ICommand CreateCraftCommand0191 { get; private set; } = null!;
    public ICommand SubmitCraftCommand0191 { get; private set; } = null!;
    public ICommand CancelCraftCommand0191 { get; private set; } = null!;

    public PlayerCraftRecipeItem0191? SelectedCraftRecipe0191
    {
        get => _selectedCraftRecipe0191;
        set
        {
            if (_selectedCraftRecipe0191 == value) return;
            _selectedCraftRecipe0191 = value;
            Notify();
            CraftProjectName0191 = value?.Name ?? string.Empty;
            CraftRequirements0191.Clear();
            CraftResources0191.Clear();
        }
    }

    public PlayerCraftProjectItem0191? SelectedCraftProject0191
    {
        get => _selectedCraftProject0191;
        set
        {
            if (_selectedCraftProject0191 == value) return;
            _selectedCraftProject0191 = value;
            Notify();
            if (value != null && !value.IsPlaceholder) LoadCraftProject0191();
        }
    }

    public string CraftProjectName0191
    {
        get => _craftProjectName0191;
        set
        {
            if (_craftProjectName0191 == value) return;
            _craftProjectName0191 = value;
            Notify();
        }
    }

    public string CraftState0191
    {
        get => _craftState0191;
        private set
        {
            if (_craftState0191 == value) return;
            _craftState0191 = value;
            Notify();
        }
    }

    public string CraftExpectedDuration0191
    {
        get => _craftExpectedDuration0191;
        private set
        {
            if (_craftExpectedDuration0191 == value) return;
            _craftExpectedDuration0191 = value;
            Notify();
        }
    }

    public string CraftResult0191 => SelectedCraftProject0191?.ResultName ?? string.Empty;
    public bool HasCraftResult0191 => !string.IsNullOrWhiteSpace(CraftResult0191);

    private void InitializeCraftRuntime0191()
    {
        RefreshCraftCommand0191 = new RelayCommand(() => RefreshCraftRuntime0191(silent: false));
        PreviewCraftCommand0191 = new RelayCommand(PreviewCraft0191);
        CreateCraftCommand0191 = new RelayCommand(CreateCraft0191);
        SubmitCraftCommand0191 = new RelayCommand(SubmitCraft0191);
        CancelCraftCommand0191 = new RelayCommand(CancelCraft0191);
    }

    private void RefreshCraftRuntime0191(bool silent)
    {
        try
        {
            var recipes = _api.ProjectCraftRecipeList(CraftBasePayload0191());
            EnsureCraftOk0191(recipes);
            CraftRecipes0191.Clear();
            foreach (var item in CraftItems0191(recipes))
                CraftRecipes0191.Add(PlayerCraftRecipeItem0191.From(item));

            var projects = _api.ProjectCraftList(CraftBasePayload0191());
            EnsureCraftOk0191(projects);
            CraftProjects0191.Clear();
            foreach (var item in CraftItems0191(projects))
                CraftProjects0191.Add(PlayerCraftProjectItem0191.From(item));

            if (CraftRecipes0191.Count == 0)
                CraftRecipes0191.Add(PlayerCraftRecipeItem0191.Placeholder("GM пока не опубликовал доступные рецепты."));
            if (CraftProjects0191.Count == 0)
                CraftProjects0191.Add(PlayerCraftProjectItem0191.Placeholder("У вас пока нет проектов изготовления."));
            SelectedCraftRecipe0191 ??= CraftRecipes0191.FirstOrDefault(x => !x.IsPlaceholder);
            if (SelectedCraftProject0191 == null || CraftProjects0191.All(x => x.ProjectId != SelectedCraftProject0191.ProjectId))
                SelectedCraftProject0191 = CraftProjects0191.FirstOrDefault(x => !x.IsPlaceholder);
            CraftState0191 = "Проекты изготовления обновлены.";
        }
        catch (Exception ex)
        {
            CraftState0191 = ex.Message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Проекты изготовления выключены feature flags."
                : "Проекты изготовления пока недоступны.";
            if (!silent) ErrorMessage = ex.Message;
        }
    }

    private void PreviewCraft0191()
    {
        if (!RequireCraftSelection0191()) return;
        try
        {
            var payload = CraftBasePayload0191();
            payload["recipeId"] = SelectedCraftRecipe0191!.RecipeId;
            var response = _api.ProjectCraftPreview(payload);
            EnsureCraftOk0191(response);
            var preview = CraftMap0191(response.Payload.TryGetValue("preview", out var raw) ? raw : null);
            FillCraftLines0191(preview, "requirements", CraftRequirements0191);
            FillCraftLines0191(preview, "resources", CraftResources0191);
            var duration = PlayerCraftParsing0191.ReadInt(preview, "estimatedDurationMinutes");
            CraftExpectedDuration0191 = duration > 0
                ? $"Ожидаемое время: {duration} мин."
                : "Ожидаемое время не задано.";
            CraftState0191 = "Требования проверены. Ресурсы не списаны.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CraftState0191 = "Не удалось проверить требования.";
        }
    }

    private void CreateCraft0191()
    {
        if (!RequireCraftSelection0191()) return;
        if (string.IsNullOrWhiteSpace(CraftProjectName0191))
        {
            ErrorMessage = "Укажите понятное название проекта.";
            return;
        }
        try
        {
            var payload = CraftBasePayload0191();
            payload["recipeId"] = SelectedCraftRecipe0191!.RecipeId;
            payload["name"] = CraftProjectName0191.Trim();
            payload["operationId"] = Guid.NewGuid().ToString("N");
            var response = _api.ProjectCraftCreate(payload);
            EnsureCraftOk0191(response);
            CraftState0191 = "Черновик проекта создан.";
            RefreshCraftRuntime0191(silent: false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CraftState0191 = "Не удалось создать проект.";
        }
    }

    private void SubmitCraft0191()
    {
        if (SelectedCraftProject0191 == null || SelectedCraftProject0191.IsPlaceholder)
        {
            ErrorMessage = "Выберите созданный черновик.";
            return;
        }
        if (MessageBox.Show("Отправить проект на рассмотрение GM?", "Проект изготовления",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        MutateOwnCraft0191(_api.ProjectCraftSubmit, "Проект отправлен GM.");
    }

    private void CancelCraft0191()
    {
        if (SelectedCraftProject0191 == null || SelectedCraftProject0191.IsPlaceholder)
        {
            ErrorMessage = "Выберите проект.";
            return;
        }
        if (MessageBox.Show("Отменить проект? Зарезервированные ресурсы будут освобождены.",
                "Проект изготовления", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        MutateOwnCraft0191(_api.ProjectCraftCancel, "Проект отменён.");
    }

    private void MutateOwnCraft0191(Func<Dictionary<string, object>, ResponseEnvelope> action, string success)
    {
        try
        {
            var payload = CraftBasePayload0191();
            payload["projectId"] = SelectedCraftProject0191!.ProjectId;
            payload["expectedRevision"] = SelectedCraftProject0191.Revision;
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsureCraftOk0191(action(payload));
            CraftState0191 = success;
            RefreshCraftRuntime0191(silent: false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CraftState0191 = "Действие не выполнено.";
        }
    }

    private void LoadCraftProject0191()
    {
        try
        {
            var response = _api.ProjectCraftGet(new Dictionary<string, object>
            {
                ["projectId"] = SelectedCraftProject0191!.ProjectId
            });
            EnsureCraftOk0191(response);
            var item = CraftMap0191(response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedCraftProject0191.Apply(item);
            FillCraftLines0191(item, "requirements", CraftRequirements0191);
            FillCraftLines0191(item, "resources", CraftResources0191);
            FillCraftLines0191(item, "stages", CraftStages0191);
            var duration = PlayerCraftParsing0191.ReadInt(item, "estimatedDurationMinutes");
            CraftExpectedDuration0191 = duration > 0
                ? $"Ожидаемое время: {duration} мин."
                : "Ожидаемое время не задано.";
            Notify(nameof(CraftResult0191));
            Notify(nameof(HasCraftResult0191));
            CraftState0191 = SelectedCraftProject0191.StatusLabel;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CraftState0191 = "Не удалось открыть проект.";
        }
    }

    private bool RequireCraftSelection0191()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(_activeCharacterIdAccessor()))
        {
            ErrorMessage = "Сначала выберите активного персонажа.";
            return false;
        }
        if (SelectedCraftRecipe0191 == null || SelectedCraftRecipe0191.IsPlaceholder)
        {
            ErrorMessage = "Выберите доступный рецепт.";
            return false;
        }
        return true;
    }

    private Dictionary<string, object> CraftBasePayload0191()
    {
        var payload = new Dictionary<string, object> { ["campaignId"] = CampaignId };
        var characterId = _activeCharacterIdAccessor();
        if (!string.IsNullOrWhiteSpace(characterId)) payload["characterId"] = characterId;
        return payload;
    }

    private static void FillCraftLines0191(
        IDictionary<string, object> parent,
        string key,
        ObservableCollection<PlayerCraftLine0191> target)
    {
        target.Clear();
        if (!parent.TryGetValue(key, out var raw) || raw is not IEnumerable sequence || raw is string) return;
        foreach (var row in sequence)
        {
            var map = CraftMap0191(row);
            target.Add(PlayerCraftLine0191.From(map));
        }
    }

    private static IEnumerable<IDictionary<string, object>> CraftItems0191(ResponseEnvelope response)
    {
        if (!response.Payload.TryGetValue("items", out var raw) || raw is not IEnumerable sequence || raw is string) yield break;
        foreach (var item in sequence)
        {
            var map = CraftMap0191(item);
            if (map.Count > 0) yield return map;
        }
    }

    internal static Dictionary<string, object> CraftMap0191(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        if (raw is IDictionary source)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in source)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static void EnsureCraftOk0191(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? "Проект изготовления недоступен." : response.Message);
    }
}

public sealed class PlayerCraftRecipeItem0191
{
    public string RecipeId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string OutputName { get; private set; } = string.Empty;
    public bool IsPlaceholder { get; private set; }
    public string Summary => IsPlaceholder ? Name : $"{Name}\nРезультат: {OutputName}";

    public static PlayerCraftRecipeItem0191 From(IDictionary<string, object> map) => new()
    {
        RecipeId = PlayerCraftParsing0191.Read(map, "recipeId"),
        Name = PlayerCraftParsing0191.First(PlayerCraftParsing0191.Read(map, "name"), "Рецепт"),
        Description = PlayerCraftParsing0191.Read(map, "description"),
        OutputName = PlayerCraftParsing0191.First(PlayerCraftParsing0191.Read(map, "outputName"), "Предмет")
    };

    public static PlayerCraftRecipeItem0191 Placeholder(string text) => new() { Name = text, IsPlaceholder = true };
}

public sealed class PlayerCraftProjectItem0191
{
    public string ProjectId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string RecipeName { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string CurrentStageName { get; private set; } = string.Empty;
    public string ResultName { get; private set; } = string.Empty;
    public int Revision { get; private set; }
    public int ProgressPercent { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => IsPlaceholder ? Name : $"{Name}\n{StatusLabel} · {ProgressPercent}%";

    public static PlayerCraftProjectItem0191 From(IDictionary<string, object> map)
    {
        var item = new PlayerCraftProjectItem0191();
        item.Apply(map);
        return item;
    }

    public void Apply(IDictionary<string, object> map)
    {
        ProjectId = PlayerCraftParsing0191.Read(map, "projectId");
        Name = PlayerCraftParsing0191.First(PlayerCraftParsing0191.Read(map, "name"), "Проект");
        RecipeName = PlayerCraftParsing0191.Read(map, "recipeName");
        StatusLabel = PlayerCraftParsing0191.First(
            PlayerCraftParsing0191.Read(map, "statusLabel"),
            "Состояние не указано");
        CurrentStageName = PlayerCraftParsing0191.Read(map, "currentStageName");
        Revision = PlayerCraftParsing0191.ReadInt(map, "revision");
        ProgressPercent = PlayerCraftParsing0191.ReadInt(map, "progressPercent");
        if (map.TryGetValue("result", out var raw))
        {
            var result = PlayerProductionViewModel.CraftMap0191(raw);
            ResultName = PlayerCraftParsing0191.Read(result, "name");
        }
    }

    public static PlayerCraftProjectItem0191 Placeholder(string text) => new() { Name = text, IsPlaceholder = true };
}

public sealed class PlayerCraftLine0191
{
    public string Name { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;

    public static PlayerCraftLine0191 From(IDictionary<string, object> map)
    {
        var quantity = PlayerCraftParsing0191.Read(map, "quantityRequired");
        if (string.IsNullOrWhiteSpace(quantity)) quantity = PlayerCraftParsing0191.Read(map, "quantity");
        var unit = PlayerCraftParsing0191.Read(map, "unit");
        var details = PlayerCraftParsing0191.First(
            PlayerCraftParsing0191.Read(map, "summary"),
            PlayerCraftParsing0191.Read(map, "statusLabel"));
        if (!string.IsNullOrWhiteSpace(quantity)) details = $"{quantity} {unit} · {details}".Trim();
        var name = PlayerCraftParsing0191.First(
            PlayerCraftParsing0191.Read(map, "name"),
            "Условие");
        return new PlayerCraftLine0191
        {
            Name = name,
            Status = PlayerCraftParsing0191.First(
                PlayerCraftParsing0191.Read(map, "statusLabel"),
                PlayerCraftParsing0191.Read(map, "status")),
            Summary = string.IsNullOrWhiteSpace(details) ? name : $"{name}\n{details}"
        };
    }
}

internal static class PlayerCraftParsing0191
{
    internal static string Read(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value != null
            ? Convert.ToString(value) ?? string.Empty
            : string.Empty;

    internal static string First(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    internal static int ReadInt(IDictionary<string, object> map, string key)
        => int.TryParse(Read(map, key), out var value) ? value : 0;
}
