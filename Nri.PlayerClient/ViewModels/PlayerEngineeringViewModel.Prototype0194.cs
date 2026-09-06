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
    private PlayerPrototypeBlueprint0194? _selectedPrototypeBlueprint0194;
    private PlayerPrototypeProject0194? _selectedPrototypeProject0194;
    private string _prototypeProjectName0194 = string.Empty;
    private string _prototypeState0194 = "Выберите канонический чертёж.";
    private string _prototypeWarning0194 = "Это прототип, а не серийный предмет.";
    private string _prototypeStatus0194 = "Прототип ещё не создан.";
    private string _prototypeTestStatus0194 = "Испытание ещё недоступно.";
    private string _prototypeTestResult0194 = "Результата пока нет.";
    private string _prototypeDefect0194 = "Дефектов пока не выявлено.";
    private string _prototypeProductionStatus0194 = "Не допущено к производству";

    public ObservableCollection<PlayerPrototypeBlueprint0194> PrototypeBlueprints0194 { get; } = new();
    public ObservableCollection<PlayerPrototypeProject0194> PrototypeProjects0194 { get; } = new();
    public ObservableCollection<PlayerPrototypeLine0194> PrototypeRequirements0194 { get; } = new();
    public ObservableCollection<PlayerPrototypeLine0194> PrototypeResources0194 { get; } = new();
    public ObservableCollection<PlayerPrototypeLine0194> PrototypeStages0194 { get; } = new();
    public ObservableCollection<string> PrototypeTestSteps0194 { get; } = new();
    public ObservableCollection<string> PrototypeRisks0194 { get; } = new();

    public ICommand RefreshPrototypeCommand0194 { get; private set; } = null!;
    public ICommand PreviewPrototypeCommand0194 { get; private set; } = null!;
    public ICommand CreatePrototypeCommand0194 { get; private set; } = null!;
    public ICommand SubmitPrototypeCommand0194 { get; private set; } = null!;
    public ICommand CancelPrototypeCommand0194 { get; private set; } = null!;

    public PlayerPrototypeBlueprint0194? SelectedPrototypeBlueprint0194
    {
        get => _selectedPrototypeBlueprint0194;
        set
        {
            if (_selectedPrototypeBlueprint0194 == value) return;
            _selectedPrototypeBlueprint0194 = value;
            Notify();
            PrototypeProjectName0194 = value?.IsPlaceholder == false
                ? value.Name + " - опытный образец"
                : string.Empty;
            ClearPrototypePreview0194();
        }
    }

    public PlayerPrototypeProject0194? SelectedPrototypeProject0194
    {
        get => _selectedPrototypeProject0194;
        set
        {
            if (_selectedPrototypeProject0194 == value) return;
            _selectedPrototypeProject0194 = value;
            Notify();
            if (value?.IsPlaceholder == false) LoadPrototypeProject0194();
        }
    }

    public string PrototypeProjectName0194
    {
        get => _prototypeProjectName0194;
        set
        {
            if (_prototypeProjectName0194 == value) return;
            _prototypeProjectName0194 = value;
            Notify();
        }
    }

    public string PrototypeState0194
    {
        get => _prototypeState0194;
        private set
        {
            if (_prototypeState0194 == value) return;
            _prototypeState0194 = value;
            Notify();
        }
    }

    public string PrototypeWarning0194
    {
        get => _prototypeWarning0194;
        private set
        {
            if (_prototypeWarning0194 == value) return;
            _prototypeWarning0194 = value;
            Notify();
        }
    }

    public string PrototypeStatus0194
    {
        get => _prototypeStatus0194;
        private set
        {
            if (_prototypeStatus0194 == value) return;
            _prototypeStatus0194 = value;
            Notify();
        }
    }

    public string PrototypeTestStatus0194
    {
        get => _prototypeTestStatus0194;
        private set
        {
            if (_prototypeTestStatus0194 == value) return;
            _prototypeTestStatus0194 = value;
            Notify();
        }
    }

    public string PrototypeTestResult0194
    {
        get => _prototypeTestResult0194;
        private set
        {
            if (_prototypeTestResult0194 == value) return;
            _prototypeTestResult0194 = value;
            Notify();
        }
    }

    public string PrototypeDefect0194
    {
        get => _prototypeDefect0194;
        private set
        {
            if (_prototypeDefect0194 == value) return;
            _prototypeDefect0194 = value;
            Notify();
        }
    }

    public string PrototypeProductionStatus0194
    {
        get => _prototypeProductionStatus0194;
        private set
        {
            if (_prototypeProductionStatus0194 == value) return;
            _prototypeProductionStatus0194 = value;
            Notify();
        }
    }

    private void InitializePrototypeRuntime0194()
    {
        RefreshPrototypeCommand0194 = new RelayCommand(() => RefreshPrototypeRuntime0194(false));
        PreviewPrototypeCommand0194 = new RelayCommand(PreviewPrototype0194);
        CreatePrototypeCommand0194 = new RelayCommand(CreatePrototypeProject0194);
        SubmitPrototypeCommand0194 = new RelayCommand(SubmitPrototypeProject0194);
        CancelPrototypeCommand0194 = new RelayCommand(CancelPrototypeProject0194);
        InitializePrototypeRepairRuntime0195();
    }

    private void RefreshPrototypeRuntime0194(bool silent)
    {
        try
        {
            var selectedBlueprintId = SelectedPrototypeBlueprint0194?.BlueprintId;
            var selectedProjectId = SelectedPrototypeProject0194?.ProjectId;

            var blueprintResponse = _api.ProjectPrototypeBlueprintList(BasePayload());
            EnsurePrototypeOk0194(blueprintResponse);
            PrototypeBlueprints0194.Clear();
            foreach (var item in PrototypeItems0194(blueprintResponse))
                PrototypeBlueprints0194.Add(PlayerPrototypeBlueprint0194.From(item));
            if (PrototypeBlueprints0194.Count == 0)
                PrototypeBlueprints0194.Add(PlayerPrototypeBlueprint0194.Placeholder(
                    "GM пока не опубликовал доступные чертежи прототипов."));

            var projectResponse = _api.ProjectPrototypeList(BasePayload());
            EnsurePrototypeOk0194(projectResponse);
            PrototypeProjects0194.Clear();
            foreach (var item in PrototypeItems0194(projectResponse))
                PrototypeProjects0194.Add(PlayerPrototypeProject0194.From(item));
            if (PrototypeProjects0194.Count == 0)
                PrototypeProjects0194.Add(PlayerPrototypeProject0194.Placeholder(
                    "У активного персонажа пока нет проектов прототипов."));

            SelectedPrototypeBlueprint0194 =
                PrototypeBlueprints0194.FirstOrDefault(x => x.BlueprintId == selectedBlueprintId)
                ?? PrototypeBlueprints0194.FirstOrDefault(x => !x.IsPlaceholder);
            SelectedPrototypeProject0194 =
                PrototypeProjects0194.FirstOrDefault(x => x.ProjectId == selectedProjectId)
                ?? PrototypeProjects0194.FirstOrDefault(x => !x.IsPlaceholder);
            PrototypeState0194 = "Проекты прототипов обновлены.";
            RefreshPrototypeRepairRuntime0195(silent);
        }
        catch (Exception ex)
        {
            PrototypeState0194 = ex.Message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Создание прототипов выключено feature flags."
                : "Проекты прототипов пока недоступны.";
            if (!silent) ErrorMessage = ex.Message;
        }
    }

    private void PreviewPrototype0194()
    {
        if (!RequirePrototypeBlueprint0194()) return;
        try
        {
            var payload = BasePayload();
            payload["blueprintId"] = SelectedPrototypeBlueprint0194!.BlueprintId;
            var response = _api.ProjectPrototypePreview(payload);
            EnsurePrototypeOk0194(response);
            var preview = PrototypeMap0194(response.Payload.TryGetValue("preview", out var raw) ? raw : null);
            FillPrototypeLines0194(preview, "requirements", PrototypeRequirements0194);
            FillPrototypeLines0194(preview, "resources", PrototypeResources0194);
            FillPrototypeStrings0194(preview, "testSteps", PrototypeTestSteps0194);
            FillPrototypeStrings0194(preview, "publicRisks", PrototypeRisks0194);
            PrototypeWarning0194 = PrototypeRead0194(preview, "prototypeWarning",
                "Это опытный образец, а не серийный предмет.");
            PrototypeTestStatus0194 = "Обязательное испытание: "
                                     + PrototypeRead0194(preview, "testProtocolName", "не определено");
            PrototypeStatus0194 = "Ожидаемый образец: "
                                  + PrototypeRead0194(preview, "targetItemName", "не определён");
            PrototypeState0194 = "Требования проверены сервером. Ресурсы ещё не зарезервированы.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            PrototypeState0194 = "Не удалось проверить требования прототипа.";
        }
    }

    private void CreatePrototypeProject0194()
    {
        if (!RequirePrototypeBlueprint0194()) return;
        if (string.IsNullOrWhiteSpace(PrototypeProjectName0194))
        {
            ErrorMessage = "Укажите название проекта прототипа.";
            return;
        }
        if (MessageBox.Show(
                "Создать черновик проекта? На этом шаге ресурсы не резервируются.",
                "Создание прототипа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            var payload = BasePayload();
            payload["blueprintId"] = SelectedPrototypeBlueprint0194!.BlueprintId;
            payload["name"] = PrototypeProjectName0194.Trim();
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsurePrototypeOk0194(_api.ProjectPrototypeCreate(payload));
            PrototypeState0194 = "Черновик проекта прототипа создан.";
            RefreshPrototypeRuntime0194(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            PrototypeState0194 = "Не удалось создать проект прототипа.";
        }
    }

    private void SubmitPrototypeProject0194()
    {
        if (!RequirePrototypeProject0194()) return;
        if (MessageBox.Show(
                "Отправить проект GM? После одобрения GM сможет зарезервировать ресурсы.",
                "Создание прототипа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        MutatePrototypeProject0194(_api.ProjectPrototypeSubmit, "Проект прототипа отправлен GM.");
    }

    private void CancelPrototypeProject0194()
    {
        if (!RequirePrototypeProject0194()) return;
        if (MessageBox.Show(
                "Отменить проект? Это разрешено только до создания физического прототипа.",
                "Создание прототипа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        MutatePrototypeProject0194(_api.ProjectPrototypeCancel, "Проект прототипа отменён.");
    }

    private void MutatePrototypeProject0194(
        Func<Dictionary<string, object>, ResponseEnvelope> action,
        string success)
    {
        try
        {
            var payload = BasePayload();
            payload["projectId"] = SelectedPrototypeProject0194!.ProjectId;
            payload["expectedRevision"] = SelectedPrototypeProject0194.Revision;
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsurePrototypeOk0194(action(payload));
            PrototypeState0194 = success;
            RefreshPrototypeRuntime0194(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            PrototypeState0194 = "Действие не выполнено.";
        }
    }

    private void LoadPrototypeProject0194()
    {
        try
        {
            var response = _api.ProjectPrototypeGet(new Dictionary<string, object>
            {
                ["projectId"] = SelectedPrototypeProject0194!.ProjectId
            });
            EnsurePrototypeOk0194(response);
            var item = PrototypeMap0194(response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedPrototypeProject0194.Apply(item);
            FillPrototypeLines0194(item, "requirements", PrototypeRequirements0194);
            FillPrototypeLines0194(item, "resources", PrototypeResources0194);
            FillPrototypeLines0194(item, "stages", PrototypeStages0194);
            FillPrototypeStrings0194(item, "testSteps", PrototypeTestSteps0194);
            PrototypeWarning0194 = PrototypeRead0194(item, "prototypeWarning",
                "Это прототип, а не серийный предмет.");
            PrototypeStatus0194 = PrototypeRead0194(item, "prototypeStatus", "Прототип ещё не создан.");
            PrototypeTestStatus0194 = PrototypeRead0194(item, "testStatus", "Испытание ещё недоступно.");
            var resultCategory = PrototypeRead0194(item, "testResultCategory", "Результата пока нет.");
            var resultSummary = PrototypeRead0194(item, "testPublicSummary");
            PrototypeTestResult0194 = string.IsNullOrWhiteSpace(resultSummary)
                ? resultCategory
                : resultCategory + ": " + resultSummary;
            var defectName = PrototypeRead0194(item, "defectName");
            var symptoms = PrototypeRead0194(item, "defectSymptoms");
            var limitations = PrototypeRead0194(item, "defectLimitations");
            PrototypeDefect0194 = string.IsNullOrWhiteSpace(defectName)
                ? "Дефектов пока не выявлено."
                : $"{defectName} ({PrototypeRead0194(item, "defectSeverity", "серьёзность не указана")})\n"
                  + $"Симптомы: {symptoms}\nОграничения: {limitations}";
            PrototypeProductionStatus0194 = PrototypeRead0194(item, "productionApprovalLabel",
                "Не допущено к производству");
            PrototypeState0194 = SelectedPrototypeProject0194.StatusLabel;
            Notify(nameof(SelectedPrototypeProject0194));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            PrototypeState0194 = "Не удалось открыть проект прототипа.";
        }
    }

    private bool RequirePrototypeBlueprint0194()
    {
        ErrorMessage = string.Empty;
        if (SelectedPrototypeBlueprint0194?.IsPlaceholder == false) return true;
        ErrorMessage = "Выберите доступный канонический чертёж прототипа.";
        return false;
    }

    private bool RequirePrototypeProject0194()
    {
        ErrorMessage = string.Empty;
        if (SelectedPrototypeProject0194?.IsPlaceholder == false) return true;
        ErrorMessage = "Выберите собственный проект прототипа.";
        return false;
    }

    private void ClearPrototypePreview0194()
    {
        PrototypeRequirements0194.Clear();
        PrototypeResources0194.Clear();
        PrototypeTestSteps0194.Clear();
        PrototypeRisks0194.Clear();
        PrototypeWarning0194 = "Это прототип, а не серийный предмет.";
    }

    private static void FillPrototypeLines0194(
        IDictionary<string, object> source,
        string key,
        ObservableCollection<PlayerPrototypeLine0194> target)
    {
        target.Clear();
        if (!source.TryGetValue(key, out var raw) || raw is not IEnumerable rows || raw is string) return;
        foreach (var row in rows)
        {
            var map = PrototypeMap0194(row);
            if (map.Count > 0) target.Add(PlayerPrototypeLine0194.From(map));
        }
    }

    private static void FillPrototypeStrings0194(
        IDictionary<string, object> source,
        string key,
        ObservableCollection<string> target)
    {
        target.Clear();
        if (!source.TryGetValue(key, out var raw) || raw is not IEnumerable rows || raw is string) return;
        foreach (var row in rows)
        {
            var value = Convert.ToString(row);
            if (!string.IsNullOrWhiteSpace(value)) target.Add(value);
        }
    }

    private static IEnumerable<IDictionary<string, object>> PrototypeItems0194(ResponseEnvelope response)
    {
        if (!response.Payload.TryGetValue("items", out var raw) || raw is not IEnumerable rows || raw is string)
            yield break;
        foreach (var row in rows)
        {
            var map = PrototypeMap0194(row);
            if (map.Count > 0) yield return map;
        }
    }

    private static Dictionary<string, object> PrototypeMap0194(object? raw)
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

    private static string PrototypeRead0194(
        IDictionary<string, object> map,
        string key,
        string fallback = "")
        => map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw))
            ? Convert.ToString(raw)!
            : fallback;

    private static int PrototypeReadInt0194(IDictionary<string, object> map, string key)
        => int.TryParse(PrototypeRead0194(map, key), out var value) ? value : 0;

    private static void EnsurePrototypeOk0194(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message)
                ? "Проекты прототипов недоступны."
                : response.Message);
    }
}

public sealed class PlayerPrototypeBlueprint0194
{
    public string BlueprintId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string TargetItemName { get; private set; } = string.Empty;
    public string TestProtocolName { get; private set; } = string.Empty;
    public bool IsPlaceholder { get; private set; }
    public string AccessibleSummary => IsPlaceholder
        ? Name
        : $"{Name}. Опытный образец: {TargetItemName}. Испытание: {TestProtocolName}. {Description}";

    public static PlayerPrototypeBlueprint0194 From(IDictionary<string, object> map)
        => new()
        {
            BlueprintId = Read(map, "blueprintId"),
            Name = Read(map, "name", "Чертёж прототипа"),
            Description = Read(map, "description"),
            TargetItemName = Read(map, "targetItemName", "Опытный образец"),
            TestProtocolName = Read(map, "testProtocolName", "Обязательное испытание")
        };

    public static PlayerPrototypeBlueprint0194 Placeholder(string text)
        => new() { Name = text, IsPlaceholder = true };

    private static string Read(IDictionary<string, object> map, string key, string fallback = "")
        => map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw))
            ? Convert.ToString(raw)!
            : fallback;
}

public sealed class PlayerPrototypeProject0194
{
    public string ProjectId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string BlueprintName { get; private set; } = string.Empty;
    public string TargetItemName { get; private set; } = string.Empty;
    public string PrototypeStatus { get; private set; } = string.Empty;
    public int ProgressPercent { get; private set; }
    public int Revision { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => IsPlaceholder
        ? Name
        : $"{Name}\n{StatusLabel} · {ProgressPercent}%\n{BlueprintName} → {TargetItemName}\n{PrototypeStatus}";

    public static PlayerPrototypeProject0194 From(IDictionary<string, object> map)
    {
        var item = new PlayerPrototypeProject0194();
        item.Apply(map);
        return item;
    }

    public void Apply(IDictionary<string, object> map)
    {
        ProjectId = Read(map, "projectId");
        Name = Read(map, "name", "Проект прототипа");
        StatusLabel = Read(map, "statusLabel", "Состояние не указано");
        BlueprintName = Read(map, "blueprintName");
        TargetItemName = Read(map, "targetItemName");
        PrototypeStatus = Read(map, "prototypeStatus", "Прототип ещё не создан");
        ProgressPercent = int.TryParse(Read(map, "progressPercent"), out var progress) ? progress : 0;
        Revision = int.TryParse(Read(map, "revision"), out var revision) ? revision : 0;
    }

    public static PlayerPrototypeProject0194 Placeholder(string text)
        => new() { Name = text, IsPlaceholder = true };

    private static string Read(IDictionary<string, object> map, string key, string fallback = "")
        => map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw))
            ? Convert.ToString(raw)!
            : fallback;
}

public sealed class PlayerPrototypeLine0194
{
    public string Name { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string Display => $"{Name}\n{StatusLabel}{(string.IsNullOrWhiteSpace(Summary) ? string.Empty : " · " + Summary)}";

    public static PlayerPrototypeLine0194 From(IDictionary<string, object> map)
    {
        var attemptNumber = Read(map, "attemptNumber");
        var defaultName = string.IsNullOrWhiteSpace(attemptNumber)
            ? "Условие"
            : $"Испытание {attemptNumber}";
        return new PlayerPrototypeLine0194
        {
            Name = Read(map, "name", defaultName),
            Summary = Read(map, "summary"),
            StatusLabel = Read(
                map,
                "statusLabel",
                Read(map, "result", Read(map, "status", "Состояние не указано")))
        };
    }

    private static string Read(IDictionary<string, object> map, string key, string fallback = "")
        => map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw))
            ? Convert.ToString(raw)!
            : fallback;
}
