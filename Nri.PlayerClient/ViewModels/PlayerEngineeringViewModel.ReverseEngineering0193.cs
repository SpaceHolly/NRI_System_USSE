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
    private PlayerReverseEngineeringSourceItem0193? _selectedReverseSource0193;
    private PlayerReverseEngineeringProjectItem0193? _selectedReverseProject0193;
    private string _reverseProjectName0193 = string.Empty;
    private string _reverseState0193 = "Выберите собственный предмет для анализа.";
    private string _reverseDispositionWarning0193 = "Судьба предмета будет показана после проверки.";
    private string _reverseExpectedDiscovery0193 = "Ожидаемое открытие пока не определено.";
    private string _reverseSourceStatus0193 = "Предмет не выбран.";

    public ObservableCollection<PlayerReverseEngineeringSourceItem0193> ReverseSources0193 { get; } = new();
    public ObservableCollection<PlayerReverseEngineeringProjectItem0193> ReverseProjects0193 { get; } = new();
    public ObservableCollection<PlayerReverseEngineeringLine0193> ReverseRequirements0193 { get; } = new();
    public ObservableCollection<PlayerReverseEngineeringLine0193> ReverseResources0193 { get; } = new();
    public ObservableCollection<PlayerReverseEngineeringLine0193> ReverseStages0193 { get; } = new();

    public ICommand RefreshReverseCommand0193 { get; private set; } = null!;
    public ICommand PreviewReverseCommand0193 { get; private set; } = null!;
    public ICommand CreateReverseCommand0193 { get; private set; } = null!;
    public ICommand SubmitReverseCommand0193 { get; private set; } = null!;
    public ICommand CancelReverseCommand0193 { get; private set; } = null!;

    public PlayerReverseEngineeringSourceItem0193? SelectedReverseSource0193
    {
        get => _selectedReverseSource0193;
        set
        {
            if (_selectedReverseSource0193 == value) return;
            _selectedReverseSource0193 = value;
            Notify();
            ReverseProjectName0193 = value?.IsPlaceholder == false ? "Анализ: " + value.Name : string.Empty;
            ReverseSourceStatus0193 = value?.Availability ?? "Предмет не выбран.";
            ReverseDispositionWarning0193 = "Нажмите «Проверить анализ», чтобы увидеть судьбу предмета.";
            ReverseExpectedDiscovery0193 = "Ожидаемое открытие пока не определено.";
            ReverseRequirements0193.Clear();
            ReverseResources0193.Clear();
        }
    }

    public PlayerReverseEngineeringProjectItem0193? SelectedReverseProject0193
    {
        get => _selectedReverseProject0193;
        set
        {
            if (_selectedReverseProject0193 == value) return;
            _selectedReverseProject0193 = value;
            Notify();
            if (value?.IsPlaceholder == false) LoadReverseProject0193();
        }
    }

    public string ReverseProjectName0193
    {
        get => _reverseProjectName0193;
        set { if (_reverseProjectName0193 != value) { _reverseProjectName0193 = value; Notify(); } }
    }

    public string ReverseState0193
    {
        get => _reverseState0193;
        private set { if (_reverseState0193 != value) { _reverseState0193 = value; Notify(); } }
    }

    public string ReverseDispositionWarning0193
    {
        get => _reverseDispositionWarning0193;
        private set { if (_reverseDispositionWarning0193 != value) { _reverseDispositionWarning0193 = value; Notify(); } }
    }

    public string ReverseExpectedDiscovery0193
    {
        get => _reverseExpectedDiscovery0193;
        private set { if (_reverseExpectedDiscovery0193 != value) { _reverseExpectedDiscovery0193 = value; Notify(); } }
    }

    public string ReverseSourceStatus0193
    {
        get => _reverseSourceStatus0193;
        private set { if (_reverseSourceStatus0193 != value) { _reverseSourceStatus0193 = value; Notify(); } }
    }

    private void InitializeReverseEngineeringRuntime0193()
    {
        RefreshReverseCommand0193 = new RelayCommand(() => RefreshReverseEngineeringRuntime0193(silent: false));
        PreviewReverseCommand0193 = new RelayCommand(PreviewReverseEngineering0193);
        CreateReverseCommand0193 = new RelayCommand(CreateReverseEngineering0193);
        SubmitReverseCommand0193 = new RelayCommand(SubmitReverseEngineering0193);
        CancelReverseCommand0193 = new RelayCommand(CancelReverseEngineering0193);
    }

    private void RefreshReverseEngineeringRuntime0193(bool silent)
    {
        try
        {
            var selectedSourceId = SelectedReverseSource0193?.ItemInstanceId;
            var selectedProjectId = SelectedReverseProject0193?.ProjectId;

            var sources = _api.ProjectReverseEngineeringSourceList(ReverseBasePayload0193());
            EnsureReverseOk0193(sources);
            ReverseSources0193.Clear();
            foreach (var item in ReverseItems0193(sources))
                ReverseSources0193.Add(PlayerReverseEngineeringSourceItem0193.From(item));

            var projects = _api.ProjectReverseEngineeringList(ReverseBasePayload0193());
            EnsureReverseOk0193(projects);
            ReverseProjects0193.Clear();
            foreach (var item in ReverseItems0193(projects))
                ReverseProjects0193.Add(PlayerReverseEngineeringProjectItem0193.From(item));

            if (ReverseSources0193.Count == 0)
                ReverseSources0193.Add(PlayerReverseEngineeringSourceItem0193.Placeholder("В инвентаре нет предметов, доступных для анализа."));
            if (ReverseProjects0193.Count == 0)
                ReverseProjects0193.Add(PlayerReverseEngineeringProjectItem0193.Placeholder("У вас пока нет проектов обратной инженерии."));

            SelectedReverseSource0193 = ReverseSources0193.FirstOrDefault(x => x.ItemInstanceId == selectedSourceId)
                                           ?? ReverseSources0193.FirstOrDefault(x => !x.IsPlaceholder);
            SelectedReverseProject0193 = ReverseProjects0193.FirstOrDefault(x => x.ProjectId == selectedProjectId)
                                            ?? ReverseProjects0193.FirstOrDefault(x => !x.IsPlaceholder);
            ReverseState0193 = "Обратная инженерия обновлена.";
        }
        catch (Exception ex)
        {
            ReverseState0193 = ex.Message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Обратная инженерия выключена feature flags."
                : "Обратная инженерия пока недоступна.";
            if (!silent) ErrorMessage = ex.Message;
        }
    }

    private void PreviewReverseEngineering0193()
    {
        if (!RequireReverseSource0193()) return;
        try
        {
            var payload = ReverseBasePayload0193();
            payload["itemInstanceId"] = SelectedReverseSource0193!.ItemInstanceId;
            var response = _api.ProjectReverseEngineeringPreview(payload);
            EnsureReverseOk0193(response);
            var preview = ReverseMap0193(response.Payload.TryGetValue("preview", out var raw) ? raw : null);
            FillReverseLines0193(preview, "requirements", ReverseRequirements0193);
            FillReverseLines0193(preview, "resources", ReverseResources0193);
            ReverseDispositionWarning0193 = ReverseRead0193(preview, "dispositionWarning", "Судьба предмета не указана.");
            ReverseExpectedDiscovery0193 = "Ожидаемое открытие: " + ReverseRead0193(preview, "expectedDiscovery", "не определено");
            ReverseSourceStatus0193 = ReverseRead0193(preview, "sourceItemCondition", SelectedReverseSource0193.Availability);
            ReverseState0193 = "Требования проверены сервером. Предмет и ресурсы ещё не зарезервированы.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ReverseState0193 = "Не удалось проверить условия анализа.";
        }
    }

    private void CreateReverseEngineering0193()
    {
        if (!RequireReverseSource0193()) return;
        if (string.IsNullOrWhiteSpace(ReverseProjectName0193))
        {
            ErrorMessage = "Укажите понятное название проекта.";
            return;
        }
        if (MessageBox.Show(
                "Создать черновик? Перед отправкой ещё раз проверьте предупреждение о судьбе предмета.",
                "Обратная инженерия",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            var payload = ReverseBasePayload0193();
            payload["itemInstanceId"] = SelectedReverseSource0193!.ItemInstanceId;
            payload["name"] = ReverseProjectName0193.Trim();
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsureReverseOk0193(_api.ProjectReverseEngineeringCreate(payload));
            ReverseState0193 = "Черновик обратной инженерии создан.";
            RefreshReverseEngineeringRuntime0193(silent: false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ReverseState0193 = "Не удалось создать проект.";
        }
    }

    private void SubmitReverseEngineering0193()
    {
        if (SelectedReverseProject0193?.IsPlaceholder != false)
        {
            ErrorMessage = "Выберите созданный черновик.";
            return;
        }
        if (MessageBox.Show(
                "Отправить проект GM? После одобрения предмет может быть зарезервирован и уничтожен согласно предупреждению.",
                "Обратная инженерия",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        MutateReverseEngineering0193(_api.ProjectReverseEngineeringSubmit, "Проект отправлен GM.");
    }

    private void CancelReverseEngineering0193()
    {
        if (SelectedReverseProject0193?.IsPlaceholder != false)
        {
            ErrorMessage = "Выберите проект обратной инженерии.";
            return;
        }
        if (MessageBox.Show(
                "Отменить проект? До начала работы предмет и ресурсы будут полностью освобождены.",
                "Обратная инженерия",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        MutateReverseEngineering0193(_api.ProjectReverseEngineeringCancel, "Проект отменён.");
    }

    private void MutateReverseEngineering0193(
        Func<Dictionary<string, object>, ResponseEnvelope> action,
        string success)
    {
        try
        {
            var payload = ReverseBasePayload0193();
            payload["projectId"] = SelectedReverseProject0193!.ProjectId;
            payload["expectedRevision"] = SelectedReverseProject0193.Revision;
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsureReverseOk0193(action(payload));
            ReverseState0193 = success;
            RefreshReverseEngineeringRuntime0193(silent: false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ReverseState0193 = "Действие не выполнено.";
        }
    }

    private void LoadReverseProject0193()
    {
        try
        {
            var response = _api.ProjectReverseEngineeringGet(new Dictionary<string, object>
            {
                ["projectId"] = SelectedReverseProject0193!.ProjectId
            });
            EnsureReverseOk0193(response);
            var item = ReverseMap0193(response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedReverseProject0193.Apply(item);
            FillReverseLines0193(item, "requirements", ReverseRequirements0193);
            FillReverseLines0193(item, "resources", ReverseResources0193);
            FillReverseLines0193(item, "stages", ReverseStages0193);
            ReverseDispositionWarning0193 = "Судьба предмета: " + ReverseRead0193(item, "sourceItemDisposition", "не указана");
            ReverseExpectedDiscovery0193 = SelectedReverseProject0193.ResultName.Length > 0
                ? "Результат: " + SelectedReverseProject0193.ResultName
                : "Ожидаемое открытие: " + SelectedReverseProject0193.ExpectedDiscovery;
            ReverseSourceStatus0193 = SelectedReverseProject0193.SourceItemStatus;
            ReverseState0193 = SelectedReverseProject0193.StatusLabel;
            Notify(nameof(SelectedReverseProject0193));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ReverseState0193 = "Не удалось открыть проект.";
        }
    }

    private bool RequireReverseSource0193()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(_activeCharacterIdAccessor()))
        {
            ErrorMessage = "Сначала выберите активного персонажа.";
            return false;
        }
        if (SelectedReverseSource0193?.IsPlaceholder != false)
        {
            ErrorMessage = "Выберите доступный предмет из инвентаря.";
            return false;
        }
        if (SelectedReverseSource0193.IsReserved)
        {
            ErrorMessage = "Этот предмет уже зарезервирован для анализа.";
            return false;
        }
        return true;
    }

    private Dictionary<string, object> ReverseBasePayload0193()
    {
        var payload = new Dictionary<string, object> { ["campaignId"] = CampaignId };
        var characterId = _activeCharacterIdAccessor();
        if (!string.IsNullOrWhiteSpace(characterId)) payload["characterId"] = characterId;
        return payload;
    }

    private static void FillReverseLines0193(
        IDictionary<string, object> parent,
        string key,
        ObservableCollection<PlayerReverseEngineeringLine0193> target)
    {
        target.Clear();
        if (!parent.TryGetValue(key, out var raw) || raw is not IEnumerable sequence || raw is string) return;
        foreach (var row in sequence)
        {
            var map = ReverseMap0193(row);
            if (map.Count > 0) target.Add(PlayerReverseEngineeringLine0193.From(map));
        }
    }

    private static IEnumerable<IDictionary<string, object>> ReverseItems0193(ResponseEnvelope response)
    {
        if (!response.Payload.TryGetValue("items", out var raw) || raw is not IEnumerable sequence || raw is string) yield break;
        foreach (var item in sequence)
        {
            var map = ReverseMap0193(item);
            if (map.Count > 0) yield return map;
        }
    }

    private static Dictionary<string, object> ReverseMap0193(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (raw is not IDictionary source) return result;
        foreach (DictionaryEntry entry in source)
        {
            var key = Convert.ToString(entry.Key);
            if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
        }
        return result;
    }

    private static string ReverseRead0193(IDictionary<string, object> map, string key, string fallback = "")
        => map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(Convert.ToString(value))
            ? Convert.ToString(value)!
            : fallback;

    private static int ReverseReadInt0193(IDictionary<string, object> map, string key)
        => int.TryParse(ReverseRead0193(map, key), out var value) ? value : 0;

    private static bool ReverseReadBool0193(IDictionary<string, object> map, string key)
        => bool.TryParse(ReverseRead0193(map, key), out var value) && value;

    private static void EnsureReverseOk0193(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message)
                ? "Обратная инженерия недоступна."
                : response.Message);
    }
}

public sealed class PlayerReverseEngineeringSourceItem0193
{
    public string ItemInstanceId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Condition { get; private set; } = string.Empty;
    public string Quality { get; private set; } = string.Empty;
    public string Availability { get; private set; } = string.Empty;
    public bool IsReserved { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => IsPlaceholder
        ? Name
        : $"{Name}\n{Condition} · {Quality}\n{Availability}";

    public static PlayerReverseEngineeringSourceItem0193 From(IDictionary<string, object> map) => new()
    {
        ItemInstanceId = Read(map, "itemInstanceId"),
        Name = Read(map, "name", "Предмет"),
        Description = Read(map, "description"),
        Condition = Read(map, "condition", "Состояние не указано"),
        Quality = Read(map, "quality", "standard"),
        Availability = Read(map, "availability", "Доступность не определена"),
        IsReserved = ReadBool(map, "isReserved")
    };

    public static PlayerReverseEngineeringSourceItem0193 Placeholder(string text)
        => new() { Name = text, IsPlaceholder = true };

    private static string Read(IDictionary<string, object> map, string key, string fallback = "")
        => map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw))
            ? Convert.ToString(raw)!
            : fallback;

    private static bool ReadBool(IDictionary<string, object> map, string key)
        => bool.TryParse(Read(map, key), out var value) && value;
}

public sealed class PlayerReverseEngineeringProjectItem0193
{
    public string ProjectId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string SourceItemName { get; private set; } = string.Empty;
    public string SourceItemStatus { get; private set; } = string.Empty;
    public string SourceItemDisposition { get; private set; } = string.Empty;
    public string ExpectedDiscovery { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string KnowledgeStatus { get; private set; } = string.Empty;
    public string ResultName { get; private set; } = string.Empty;
    public int Revision { get; private set; }
    public int ProgressPercent { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => IsPlaceholder
        ? Name
        : $"{Name}\n{SourceItemName} · {StatusLabel} · {ProgressPercent}%\n{SourceItemStatus}";

    public static PlayerReverseEngineeringProjectItem0193 From(IDictionary<string, object> map)
    {
        var item = new PlayerReverseEngineeringProjectItem0193();
        item.Apply(map);
        return item;
    }

    public void Apply(IDictionary<string, object> map)
    {
        ProjectId = Read(map, "projectId");
        Name = Read(map, "name", "Обратная инженерия");
        SourceItemName = Read(map, "sourceItemName", "Предмет");
        SourceItemStatus = Read(map, "sourceItemStatus", "Состояние не указано");
        SourceItemDisposition = Read(map, "sourceItemDisposition", "Не указана");
        ExpectedDiscovery = Read(map, "expectedDiscovery", "не определено");
        StatusLabel = Read(map, "statusLabel", "Состояние не указано");
        KnowledgeStatus = Read(map, "knowledgeStatus", "Открытие не получено");
        Revision = ReadInt(map, "revision");
        ProgressPercent = ReadInt(map, "progressPercent");
        if (map.TryGetValue("result", out var raw))
            ResultName = Read(PlayerEngineeringViewModel.Dict(raw), "name");
    }

    public static PlayerReverseEngineeringProjectItem0193 Placeholder(string text)
        => new() { Name = text, IsPlaceholder = true };

    private static string Read(IDictionary<string, object> map, string key, string fallback = "")
        => map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw))
            ? Convert.ToString(raw)!
            : fallback;

    private static int ReadInt(IDictionary<string, object> map, string key)
        => int.TryParse(Read(map, key), out var value) ? value : 0;
}

public sealed class PlayerReverseEngineeringLine0193
{
    public string Name { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string Display => string.IsNullOrWhiteSpace(Status) ? $"{Name}\n{Summary}" : $"{Name} · {Status}\n{Summary}";

    public static PlayerReverseEngineeringLine0193 From(IDictionary<string, object> map)
    {
        var quantity = Read(map, "quantityRequired", Read(map, "quantity"));
        var unit = Read(map, "unit");
        var status = Read(map, "statusLabel", Read(map, "status"));
        var summary = Read(map, "summary", status);
        if (!string.IsNullOrWhiteSpace(quantity))
            summary = $"{quantity} {unit} · {summary}".Trim();
        return new PlayerReverseEngineeringLine0193
        {
            Name = Read(map, "name", "Условие"),
            Status = status,
            Summary = summary
        };
    }

    private static string Read(IDictionary<string, object> map, string key, string fallback = "")
        => map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw))
            ? Convert.ToString(raw)!
            : fallback;
}
