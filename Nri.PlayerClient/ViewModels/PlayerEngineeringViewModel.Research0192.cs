using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed partial class PlayerEngineeringViewModel
{
    private PlayerResearchTechnologyItem0192? _selectedResearchTechnology0192;
    private PlayerResearchProjectItem0192? _selectedResearchProject0192;
    private string _researchProjectName0192 = string.Empty;
    private string _researchState0192 = "Выберите технологию и проверьте требования.";
    private string _researchKnowledgeStatus0192 = "Знание не выбрано.";

    public ObservableCollection<PlayerResearchTechnologyItem0192> ResearchTechnologies0192 { get; } = new();
    public ObservableCollection<PlayerResearchProjectItem0192> ResearchProjects0192 { get; } = new();
    public ObservableCollection<PlayerResearchLine0192> ResearchRequirements0192 { get; } = new();
    public ObservableCollection<PlayerResearchLine0192> ResearchResources0192 { get; } = new();
    public ObservableCollection<PlayerResearchLine0192> ResearchStages0192 { get; } = new();

    public ICommand RefreshResearchCommand0192 { get; private set; } = null!;
    public ICommand PreviewResearchCommand0192 { get; private set; } = null!;
    public ICommand CreateResearchCommand0192 { get; private set; } = null!;
    public ICommand SubmitResearchCommand0192 { get; private set; } = null!;
    public ICommand CancelResearchCommand0192 { get; private set; } = null!;

    public PlayerResearchTechnologyItem0192? SelectedResearchTechnology0192
    {
        get => _selectedResearchTechnology0192;
        set
        {
            if (_selectedResearchTechnology0192 == value) return;
            _selectedResearchTechnology0192 = value;
            Notify();
            ResearchProjectName0192 = value?.IsPlaceholder == false ? "Исследование: " + value.Name : string.Empty;
            ResearchKnowledgeStatus0192 = value?.KnowledgeStatus ?? "Знание не выбрано.";
            ResearchRequirements0192.Clear();
            ResearchResources0192.Clear();
        }
    }

    public PlayerResearchProjectItem0192? SelectedResearchProject0192
    {
        get => _selectedResearchProject0192;
        set
        {
            if (_selectedResearchProject0192 == value) return;
            _selectedResearchProject0192 = value;
            Notify();
            if (value?.IsPlaceholder == false) LoadResearchProject0192();
        }
    }

    public string ResearchProjectName0192
    {
        get => _researchProjectName0192;
        set { if (_researchProjectName0192 != value) { _researchProjectName0192 = value; Notify(); } }
    }

    public string ResearchState0192
    {
        get => _researchState0192;
        private set { if (_researchState0192 != value) { _researchState0192 = value; Notify(); } }
    }

    public string ResearchKnowledgeStatus0192
    {
        get => _researchKnowledgeStatus0192;
        private set { if (_researchKnowledgeStatus0192 != value) { _researchKnowledgeStatus0192 = value; Notify(); } }
    }

    private void InitializeResearchRuntime0192()
    {
        RefreshResearchCommand0192 = new RelayCommand(() => RefreshResearchRuntime0192(silent: false));
        PreviewResearchCommand0192 = new RelayCommand(PreviewResearch0192);
        CreateResearchCommand0192 = new RelayCommand(CreateResearch0192);
        SubmitResearchCommand0192 = new RelayCommand(SubmitResearch0192);
        CancelResearchCommand0192 = new RelayCommand(CancelResearch0192);
    }

    private void RefreshResearchRuntime0192(bool silent)
    {
        try
        {
            var technologies = _api.ProjectResearchTechnologyList(ResearchBasePayload0192());
            EnsureResearchOk0192(technologies);
            ResearchTechnologies0192.Clear();
            foreach (var item in ResearchItems0192(technologies))
                ResearchTechnologies0192.Add(PlayerResearchTechnologyItem0192.From(item));

            var projects = _api.ProjectResearchList(ResearchBasePayload0192());
            EnsureResearchOk0192(projects);
            ResearchProjects0192.Clear();
            foreach (var item in ResearchItems0192(projects))
                ResearchProjects0192.Add(PlayerResearchProjectItem0192.From(item));

            if (ResearchTechnologies0192.Count == 0)
                ResearchTechnologies0192.Add(PlayerResearchTechnologyItem0192.Placeholder("GM пока не опубликовал доступные технологии."));
            if (ResearchProjects0192.Count == 0)
                ResearchProjects0192.Add(PlayerResearchProjectItem0192.Placeholder("У вас пока нет исследований."));
            SelectedResearchTechnology0192 ??= ResearchTechnologies0192.FirstOrDefault(x => !x.IsPlaceholder);
            if (SelectedResearchProject0192 == null || ResearchProjects0192.All(x => x.ProjectId != SelectedResearchProject0192.ProjectId))
                SelectedResearchProject0192 = ResearchProjects0192.FirstOrDefault(x => !x.IsPlaceholder);
            ResearchState0192 = "Исследования обновлены.";
        }
        catch (Exception ex)
        {
            ResearchState0192 = ex.Message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Исследования теории выключены feature flags."
                : "Исследования теории пока недоступны.";
            if (!silent) ErrorMessage = ex.Message;
        }
    }

    private void PreviewResearch0192()
    {
        if (!RequireResearchSelection0192()) return;
        try
        {
            var payload = ResearchBasePayload0192();
            payload["technologyId"] = SelectedResearchTechnology0192!.TechnologyId;
            var response = _api.ProjectResearchPreview(payload);
            EnsureResearchOk0192(response);
            var preview = ResearchMap0192(response.Payload.TryGetValue("preview", out var raw) ? raw : null);
            FillResearchLines0192(preview, "requirements", ResearchRequirements0192);
            FillResearchLines0192(preview, "resources", ResearchResources0192);
            ResearchKnowledgeStatus0192 = ResearchRead0192(preview, "knowledgeStatus", "Не изучено");
            ResearchState0192 = "Требования проверены сервером. Ресурсы не списаны.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ResearchState0192 = "Не удалось проверить требования исследования.";
        }
    }

    private void CreateResearch0192()
    {
        if (!RequireResearchSelection0192()) return;
        if (string.IsNullOrWhiteSpace(ResearchProjectName0192))
        {
            ErrorMessage = "Укажите понятное название исследования.";
            return;
        }
        try
        {
            var payload = ResearchBasePayload0192();
            payload["technologyId"] = SelectedResearchTechnology0192!.TechnologyId;
            payload["name"] = ResearchProjectName0192.Trim();
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsureResearchOk0192(_api.ProjectResearchCreate(payload));
            ResearchState0192 = "Черновик исследования создан.";
            RefreshResearchRuntime0192(silent: false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ResearchState0192 = "Не удалось создать исследование.";
        }
    }

    private void SubmitResearch0192()
    {
        if (SelectedResearchProject0192?.IsPlaceholder != false)
        {
            ErrorMessage = "Выберите созданный черновик исследования.";
            return;
        }
        if (MessageBox.Show("Отправить исследование на рассмотрение GM?", "Исследование теории",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        MutateResearch0192(_api.ProjectResearchSubmit, "Исследование отправлено GM.");
    }

    private void CancelResearch0192()
    {
        if (SelectedResearchProject0192?.IsPlaceholder != false)
        {
            ErrorMessage = "Выберите исследование.";
            return;
        }
        if (MessageBox.Show("Отменить исследование? Зарезервированные ресурсы будут освобождены.",
                "Исследование теории", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        MutateResearch0192(_api.ProjectResearchCancel, "Исследование отменено.");
    }

    private void MutateResearch0192(Func<Dictionary<string, object>, ResponseEnvelope> action, string success)
    {
        try
        {
            var payload = ResearchBasePayload0192();
            payload["projectId"] = SelectedResearchProject0192!.ProjectId;
            payload["expectedRevision"] = SelectedResearchProject0192.Revision;
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsureResearchOk0192(action(payload));
            ResearchState0192 = success;
            RefreshResearchRuntime0192(silent: false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ResearchState0192 = "Действие не выполнено.";
        }
    }

    private void LoadResearchProject0192()
    {
        try
        {
            var response = _api.ProjectResearchGet(new Dictionary<string, object>
            {
                ["projectId"] = SelectedResearchProject0192!.ProjectId
            });
            EnsureResearchOk0192(response);
            var item = ResearchMap0192(response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedResearchProject0192.Apply(item);
            FillResearchLines0192(item, "requirements", ResearchRequirements0192);
            FillResearchLines0192(item, "resources", ResearchResources0192);
            FillResearchLines0192(item, "stages", ResearchStages0192);
            ResearchKnowledgeStatus0192 = ResearchRead0192(item, "knowledgeStatus", "Не изучено");
            ResearchState0192 = SelectedResearchProject0192.StatusLabel;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ResearchState0192 = "Не удалось открыть исследование.";
        }
    }

    private bool RequireResearchSelection0192()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(_activeCharacterIdAccessor()))
        {
            ErrorMessage = "Сначала выберите активного персонажа.";
            return false;
        }
        if (SelectedResearchTechnology0192?.IsPlaceholder != false)
        {
            ErrorMessage = "Выберите доступную технологию.";
            return false;
        }
        return true;
    }

    private Dictionary<string, object> ResearchBasePayload0192()
    {
        var payload = new Dictionary<string, object> { ["campaignId"] = CampaignId };
        var characterId = _activeCharacterIdAccessor();
        if (!string.IsNullOrWhiteSpace(characterId)) payload["characterId"] = characterId;
        return payload;
    }

    private static void FillResearchLines0192(
        IDictionary<string, object> parent,
        string key,
        ObservableCollection<PlayerResearchLine0192> target)
    {
        target.Clear();
        if (!parent.TryGetValue(key, out var raw) || raw is not IEnumerable sequence || raw is string) return;
        foreach (var row in sequence)
        {
            var map = ResearchMap0192(row);
            if (map.Count > 0) target.Add(PlayerResearchLine0192.From(map));
        }
    }

    private static IEnumerable<IDictionary<string, object>> ResearchItems0192(ResponseEnvelope response)
    {
        if (!response.Payload.TryGetValue("items", out var raw) || raw is not IEnumerable sequence || raw is string) yield break;
        foreach (var item in sequence)
        {
            var map = ResearchMap0192(item);
            if (map.Count > 0) yield return map;
        }
    }

    private static Dictionary<string, object> ResearchMap0192(object? raw)
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

    private static string ResearchRead0192(IDictionary<string, object> map, string key, string fallback = "")
        => map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(Convert.ToString(value))
            ? Convert.ToString(value)!
            : fallback;

    private static int ResearchReadInt0192(IDictionary<string, object> map, string key)
        => int.TryParse(ResearchRead0192(map, key), out var value) ? value : 0;

    private static void EnsureResearchOk0192(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? "Исследование недоступно." : response.Message);
    }
}

public sealed class PlayerResearchTechnologyItem0192
{
    public string TechnologyId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string KnowledgeStatus { get; private set; } = string.Empty;
    public int Tier { get; private set; }
    public bool IsKnown { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => IsPlaceholder ? Name : $"{Name}\nУровень {Tier} · {KnowledgeStatus}";

    public static PlayerResearchTechnologyItem0192 From(IDictionary<string, object> map) => new()
    {
        TechnologyId = Read(map, "technologyId"),
        Name = Read(map, "name", "Технология"),
        Description = Read(map, "description"),
        KnowledgeStatus = Read(map, "knowledgeStatus", "Не изучено"),
        Tier = ReadInt(map, "tier"),
        IsKnown = bool.TryParse(Read(map, "isKnown"), out var known) && known
    };

    public static PlayerResearchTechnologyItem0192 Placeholder(string text) => new() { Name = text, IsPlaceholder = true };
    private static string Read(IDictionary<string, object> map, string key, string fallback = "") =>
        map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw)) ? Convert.ToString(raw)! : fallback;
    private static int ReadInt(IDictionary<string, object> map, string key) => int.TryParse(Read(map, key), out var value) ? value : 0;
}

public sealed class PlayerResearchProjectItem0192
{
    public string ProjectId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string TechnologyName { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string KnowledgeStatus { get; private set; } = string.Empty;
    public string ResultName { get; private set; } = string.Empty;
    public int Revision { get; private set; }
    public int ProgressPercent { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => IsPlaceholder ? Name : $"{Name}\n{StatusLabel} · {ProgressPercent}% · {KnowledgeStatus}";

    public static PlayerResearchProjectItem0192 From(IDictionary<string, object> map)
    {
        var item = new PlayerResearchProjectItem0192();
        item.Apply(map);
        return item;
    }

    public void Apply(IDictionary<string, object> map)
    {
        ProjectId = Read(map, "projectId");
        Name = Read(map, "name", "Исследование");
        TechnologyName = Read(map, "technologyName");
        StatusLabel = Read(map, "statusLabel", "Состояние не указано");
        KnowledgeStatus = Read(map, "knowledgeStatus", "Не изучено");
        Revision = ReadInt(map, "revision");
        ProgressPercent = ReadInt(map, "progressPercent");
        if (map.TryGetValue("result", out var raw))
            ResultName = Read(PlayerEngineeringViewModel.Dict(raw), "name");
    }

    public static PlayerResearchProjectItem0192 Placeholder(string text) => new() { Name = text, IsPlaceholder = true };
    private static string Read(IDictionary<string, object> map, string key, string fallback = "") =>
        map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw)) ? Convert.ToString(raw)! : fallback;
    private static int ReadInt(IDictionary<string, object> map, string key) => int.TryParse(Read(map, key), out var value) ? value : 0;
}

public sealed class PlayerResearchLine0192
{
    public string Name { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;

    public static PlayerResearchLine0192 From(IDictionary<string, object> map)
    {
        var quantity = Read(map, "quantityRequired", Read(map, "quantity"));
        var unit = Read(map, "unit");
        var status = Read(map, "statusLabel", Read(map, "status"));
        var summary = Read(map, "summary", status);
        if (!string.IsNullOrWhiteSpace(quantity)) summary = $"{quantity} {unit} · {summary}".Trim();
        return new PlayerResearchLine0192
        {
            Name = Read(map, "name", "Условие"),
            Status = status,
            Summary = summary
        };
    }

    private static string Read(IDictionary<string, object> map, string key, string fallback = "") =>
        map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw)) ? Convert.ToString(raw)! : fallback;
}
