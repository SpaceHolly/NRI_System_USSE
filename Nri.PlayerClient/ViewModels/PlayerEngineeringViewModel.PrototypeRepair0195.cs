using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed partial class PlayerEngineeringViewModel
{
    private PlayerPrototypeRepairCandidate0195? _selectedPrototypeRepairCandidate0195;
    private PlayerPrototypeRepairProject0195? _selectedPrototypeRepairProject0195;
    private string _prototypeRepairProjectName0195 = string.Empty;
    private string _prototypeRepairState0195 = "Выберите прототип с открытым дефектом.";
    private string _prototypeRepairWarning0195 =
        "После ремонта обязателен повторный тест; допуск к производству выдаёт GM отдельно.";
    private string _prototypeRepairStatus0195 = "Ремонт ещё не подготовлен.";
    private string _prototypeRepairDefect0195 = "Открытый дефект не выбран.";
    private string _prototypeRepairTestHistory0195 = "История испытаний пока не загружена.";
    private string _prototypeRepairProduction0195 = "Не допущено к производству";

    public ObservableCollection<PlayerPrototypeRepairCandidate0195> PrototypeRepairCandidates0195 { get; } = new();
    public ObservableCollection<PlayerPrototypeRepairProject0195> PrototypeRepairProjects0195 { get; } = new();
    public ObservableCollection<PlayerPrototypeLine0194> PrototypeRepairRequirements0195 { get; } = new();
    public ObservableCollection<PlayerPrototypeLine0194> PrototypeRepairResources0195 { get; } = new();
    public ObservableCollection<PlayerPrototypeLine0194> PrototypeRepairStages0195 { get; } = new();
    public ObservableCollection<PlayerPrototypeLine0194> PrototypeRepairTestHistoryRows0195 { get; } = new();

    public ICommand RefreshPrototypeRepairCommand0195 { get; private set; } = null!;
    public ICommand PreviewPrototypeRepairCommand0195 { get; private set; } = null!;
    public ICommand CreatePrototypeRepairCommand0195 { get; private set; } = null!;
    public ICommand SubmitPrototypeRepairCommand0195 { get; private set; } = null!;
    public ICommand CancelPrototypeRepairCommand0195 { get; private set; } = null!;

    public PlayerPrototypeRepairCandidate0195? SelectedPrototypeRepairCandidate0195
    {
        get => _selectedPrototypeRepairCandidate0195;
        set
        {
            if (_selectedPrototypeRepairCandidate0195 == value) return;
            _selectedPrototypeRepairCandidate0195 = value;
            Notify();
            PrototypeRepairProjectName0195 = value?.IsPlaceholder == false
                ? "Ремонт: " + value.PrototypeName
                : string.Empty;
            ClearPrototypeRepairPreview0195();
        }
    }

    public PlayerPrototypeRepairProject0195? SelectedPrototypeRepairProject0195
    {
        get => _selectedPrototypeRepairProject0195;
        set
        {
            if (_selectedPrototypeRepairProject0195 == value) return;
            _selectedPrototypeRepairProject0195 = value;
            Notify();
            if (value?.IsPlaceholder == false) LoadPrototypeRepairProject0195();
        }
    }

    public string PrototypeRepairProjectName0195
    {
        get => _prototypeRepairProjectName0195;
        set
        {
            if (_prototypeRepairProjectName0195 == value) return;
            _prototypeRepairProjectName0195 = value;
            Notify();
        }
    }

    public string PrototypeRepairState0195
    {
        get => _prototypeRepairState0195;
        private set
        {
            if (_prototypeRepairState0195 == value) return;
            _prototypeRepairState0195 = value;
            Notify();
        }
    }

    public string PrototypeRepairWarning0195
    {
        get => _prototypeRepairWarning0195;
        private set
        {
            if (_prototypeRepairWarning0195 == value) return;
            _prototypeRepairWarning0195 = value;
            Notify();
        }
    }

    public string PrototypeRepairStatus0195
    {
        get => _prototypeRepairStatus0195;
        private set
        {
            if (_prototypeRepairStatus0195 == value) return;
            _prototypeRepairStatus0195 = value;
            Notify();
        }
    }

    public string PrototypeRepairDefect0195
    {
        get => _prototypeRepairDefect0195;
        private set
        {
            if (_prototypeRepairDefect0195 == value) return;
            _prototypeRepairDefect0195 = value;
            Notify();
        }
    }

    public string PrototypeRepairTestHistory0195
    {
        get => _prototypeRepairTestHistory0195;
        private set
        {
            if (_prototypeRepairTestHistory0195 == value) return;
            _prototypeRepairTestHistory0195 = value;
            Notify();
        }
    }

    public string PrototypeRepairProduction0195
    {
        get => _prototypeRepairProduction0195;
        private set
        {
            if (_prototypeRepairProduction0195 == value) return;
            _prototypeRepairProduction0195 = value;
            Notify();
        }
    }

    private void InitializePrototypeRepairRuntime0195()
    {
        RefreshPrototypeRepairCommand0195 = new RelayCommand(() => RefreshPrototypeRepairRuntime0195(false));
        PreviewPrototypeRepairCommand0195 = new RelayCommand(PreviewPrototypeRepair0195);
        CreatePrototypeRepairCommand0195 = new RelayCommand(CreatePrototypeRepair0195);
        SubmitPrototypeRepairCommand0195 = new RelayCommand(SubmitPrototypeRepair0195);
        CancelPrototypeRepairCommand0195 = new RelayCommand(CancelPrototypeRepair0195);
    }

    private void RefreshPrototypeRepairRuntime0195(bool silent)
    {
        try
        {
            var candidateId = SelectedPrototypeRepairCandidate0195?.PrototypeId;
            var projectId = SelectedPrototypeRepairProject0195?.ProjectId;
            var available = _api.ProjectPrototypeRepairAvailableList(BasePayload());
            EnsurePrototypeOk0194(available);
            PrototypeRepairCandidates0195.Clear();
            foreach (var row in PrototypeItems0194(available))
                PrototypeRepairCandidates0195.Add(PlayerPrototypeRepairCandidate0195.From(row));
            if (PrototypeRepairCandidates0195.Count == 0)
                PrototypeRepairCandidates0195.Add(PlayerPrototypeRepairCandidate0195.Placeholder(
                    "Нет принадлежащих персонажу прототипов с открытым дефектом."));

            var projects = _api.ProjectPrototypeRepairList(BasePayload());
            EnsurePrototypeOk0194(projects);
            PrototypeRepairProjects0195.Clear();
            foreach (var row in PrototypeItems0194(projects))
                PrototypeRepairProjects0195.Add(PlayerPrototypeRepairProject0195.From(row));
            if (PrototypeRepairProjects0195.Count == 0)
                PrototypeRepairProjects0195.Add(PlayerPrototypeRepairProject0195.Placeholder(
                    "Проекты ремонта пока не созданы."));

            SelectedPrototypeRepairCandidate0195 =
                PrototypeRepairCandidates0195.FirstOrDefault(x => x.PrototypeId == candidateId)
                ?? PrototypeRepairCandidates0195.FirstOrDefault(x => !x.IsPlaceholder);
            SelectedPrototypeRepairProject0195 =
                PrototypeRepairProjects0195.FirstOrDefault(x => x.ProjectId == projectId);
            PrototypeRepairState0195 = "Ремонты прототипов обновлены.";
        }
        catch (Exception ex)
        {
            PrototypeRepairState0195 = ex.Message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Ремонт прототипов выключен feature flags."
                : "Ремонт прототипов пока недоступен.";
            if (!silent) ErrorMessage = ex.Message;
        }
    }

    private void PreviewPrototypeRepair0195()
    {
        if (!RequirePrototypeRepairCandidate0195()) return;
        try
        {
            var payload = BasePayload();
            payload["prototypeId"] = SelectedPrototypeRepairCandidate0195!.PrototypeId;
            payload["defectId"] = SelectedPrototypeRepairCandidate0195.DefectId;
            var response = _api.ProjectPrototypeRepairPreview(payload);
            EnsurePrototypeOk0194(response);
            var preview = PrototypeMap0194(response.Payload.TryGetValue("preview", out var raw) ? raw : null);
            FillPrototypeLines0194(preview, "requirements", PrototypeRepairRequirements0195);
            FillPrototypeLines0194(preview, "resources", PrototypeRepairResources0195);
            PrototypeRepairWarning0195 = PrototypeRead0194(preview, "warning",
                "После ремонта обязателен повторный тест.");
            PrototypeRepairStatus0195 = "Прототип: "
                                        + PrototypeRead0194(preview, "prototypeName", "не определён");
            PrototypeRepairDefect0195 = PrototypeRead0194(preview, "defectName", "Дефект не определён")
                                        + " · " + PrototypeRead0194(preview, "defectSeverity")
                                        + "\nСимптомы: " + PrototypeRead0194(preview, "defectSymptoms")
                                        + "\nОграничения: " + PrototypeRead0194(preview, "defectLimitations");
            PrototypeRepairTestHistory0195 = "Повторное испытание: "
                                             + PrototypeRead0194(preview, "testProtocolName", "не определено");
            PrototypeRepairState0195 = "Требования ремонта проверены сервером.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            PrototypeRepairState0195 = "Не удалось проверить требования ремонта.";
        }
    }

    private void CreatePrototypeRepair0195()
    {
        if (!RequirePrototypeRepairCandidate0195()) return;
        if (string.IsNullOrWhiteSpace(PrototypeRepairProjectName0195))
        {
            ErrorMessage = "Укажите название проекта ремонта.";
            return;
        }
        if (MessageBox.Show(
                "Создать черновик ремонта? Прототип и ресурсы пока не резервируются.",
                "Ремонт прототипа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            var payload = BasePayload();
            payload["prototypeId"] = SelectedPrototypeRepairCandidate0195!.PrototypeId;
            payload["defectId"] = SelectedPrototypeRepairCandidate0195.DefectId;
            payload["name"] = PrototypeRepairProjectName0195.Trim();
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsurePrototypeOk0194(_api.ProjectPrototypeRepairCreate(payload));
            PrototypeRepairState0195 = "Черновик ремонта создан.";
            RefreshPrototypeRepairRuntime0195(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            PrototypeRepairState0195 = "Не удалось создать проект ремонта.";
        }
    }

    private void SubmitPrototypeRepair0195()
    {
        if (!RequirePrototypeRepairProject0195()) return;
        if (MessageBox.Show(
                "Отправить ремонт GM? После одобрения GM сможет зарезервировать прототип и материалы.",
                "Ремонт прототипа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        MutatePrototypeRepairProject0195(
            _api.ProjectPrototypeRepairSubmit, "Проект ремонта отправлен GM.");
    }

    private void CancelPrototypeRepair0195()
    {
        if (!RequirePrototypeRepairProject0195()) return;
        if (MessageBox.Show(
                "Отменить ремонт и освободить резерв? После применения ремонта отмена недоступна.",
                "Ремонт прототипа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        MutatePrototypeRepairProject0195(
            _api.ProjectPrototypeRepairCancel, "Ремонт отменён; резерв освобождён.");
    }

    private void MutatePrototypeRepairProject0195(
        Func<Dictionary<string, object>, ResponseEnvelope> command,
        string success)
    {
        try
        {
            var payload = BasePayload();
            payload["projectId"] = SelectedPrototypeRepairProject0195!.ProjectId;
            payload["expectedRevision"] = SelectedPrototypeRepairProject0195.Revision;
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsurePrototypeOk0194(command(payload));
            PrototypeRepairState0195 = success;
            RefreshPrototypeRepairRuntime0195(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            PrototypeRepairState0195 = "Действие не выполнено.";
        }
    }

    private void LoadPrototypeRepairProject0195()
    {
        try
        {
            var response = _api.ProjectPrototypeRepairGet(new Dictionary<string, object>
            {
                ["projectId"] = SelectedPrototypeRepairProject0195!.ProjectId
            });
            EnsurePrototypeOk0194(response);
            var item = PrototypeMap0194(response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedPrototypeRepairProject0195.Apply(item);
            FillPrototypeLines0194(item, "requirements", PrototypeRepairRequirements0195);
            FillPrototypeLines0194(item, "resources", PrototypeRepairResources0195);
            FillPrototypeLines0194(item, "stages", PrototypeRepairStages0195);
            FillPrototypeLines0194(item, "testHistory", PrototypeRepairTestHistoryRows0195);
            PrototypeRepairWarning0195 = PrototypeRead0194(item, "prototypeWarning",
                "После ремонта обязателен повторный TestProtocol.");
            PrototypeRepairStatus0195 = PrototypeRead0194(item, "prototypeStatus", "Состояние не определено")
                                        + " · " + PrototypeRead0194(item, "testStatus");
            PrototypeRepairDefect0195 =
                PrototypeRead0194(item, "defectName", "Дефект не указан")
                + " · " + PrototypeRead0194(item, "defectStatus")
                + "\nСимптомы: " + PrototypeRead0194(item, "defectSymptoms")
                + "\nОграничения: " + PrototypeRead0194(item, "defectLimitations")
                + (string.IsNullOrWhiteSpace(PrototypeRead0194(item, "resolutionSummary"))
                    ? string.Empty
                    : "\nРезультат ремонта: " + PrototypeRead0194(item, "resolutionSummary"));
            PrototypeRepairTestHistory0195 = PrototypeRead0194(item, "testResultCategory",
                "История испытаний пока пуста.");
            PrototypeRepairProduction0195 = PrototypeRead0194(item, "productionApprovalLabel",
                "Не допущено к производству");
            PrototypeRepairState0195 = SelectedPrototypeRepairProject0195.StatusLabel;
            Notify(nameof(SelectedPrototypeRepairProject0195));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            PrototypeRepairState0195 = "Не удалось открыть проект ремонта.";
        }
    }

    private bool RequirePrototypeRepairCandidate0195()
    {
        ErrorMessage = string.Empty;
        if (SelectedPrototypeRepairCandidate0195?.IsPlaceholder == false) return true;
        ErrorMessage = "Выберите свой прототип с открытым дефектом.";
        return false;
    }

    private bool RequirePrototypeRepairProject0195()
    {
        ErrorMessage = string.Empty;
        if (SelectedPrototypeRepairProject0195?.IsPlaceholder == false) return true;
        ErrorMessage = "Выберите собственный проект ремонта.";
        return false;
    }

    private void ClearPrototypeRepairPreview0195()
    {
        PrototypeRepairRequirements0195.Clear();
        PrototypeRepairResources0195.Clear();
        PrototypeRepairStages0195.Clear();
        PrototypeRepairTestHistoryRows0195.Clear();
        PrototypeRepairWarning0195 =
            "После ремонта обязателен повторный тест; допуск к производству выдаёт GM отдельно.";
        PrototypeRepairStatus0195 = "Ремонт ещё не подготовлен.";
        PrototypeRepairDefect0195 = "Открытый дефект не выбран.";
        PrototypeRepairTestHistory0195 = "История испытаний пока не загружена.";
        PrototypeRepairProduction0195 = "Не допущено к производству";
        PrototypeRepairState0195 = "Выберите прототип и проверьте требования ремонта.";
    }
}

public sealed class PlayerPrototypeRepairCandidate0195
{
    public string PrototypeId { get; private set; } = string.Empty;
    public string DefectId { get; private set; } = string.Empty;
    public string PrototypeName { get; private set; } = string.Empty;
    public string BlueprintName { get; private set; } = string.Empty;
    public string LifecycleStatus { get; private set; } = string.Empty;
    public string DefectName { get; private set; } = string.Empty;
    public string DefectSeverity { get; private set; } = string.Empty;
    public string DefectSymptoms { get; private set; } = string.Empty;
    public bool IsPlaceholder { get; private set; }
    public string Summary => IsPlaceholder
        ? PrototypeName
        : $"{PrototypeName}\n{DefectName} · {DefectSeverity}\n{LifecycleStatus}\n{DefectSymptoms}";

    public static PlayerPrototypeRepairCandidate0195 From(IDictionary<string, object> map)
        => new()
        {
            PrototypeId = Read(map, "prototypeId"),
            DefectId = Read(map, "defectId"),
            PrototypeName = Read(map, "name", "Прототип"),
            BlueprintName = Read(map, "blueprintName"),
            LifecycleStatus = Read(map, "lifecycleStatus"),
            DefectName = Read(map, "defectName", "Открытый дефект"),
            DefectSeverity = Read(map, "defectSeverity"),
            DefectSymptoms = Read(map, "defectSymptoms")
        };

    public static PlayerPrototypeRepairCandidate0195 Placeholder(string text)
        => new() { PrototypeName = text, IsPlaceholder = true };

    private static string Read(IDictionary<string, object> map, string key, string fallback = "")
        => map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw))
            ? Convert.ToString(raw)!
            : fallback;
}

public sealed class PlayerPrototypeRepairProject0195
{
    public string ProjectId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string PrototypeName { get; private set; } = string.Empty;
    public string DefectName { get; private set; } = string.Empty;
    public string ProductionApproval { get; private set; } = string.Empty;
    public int ProgressPercent { get; private set; }
    public int Revision { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string Summary => IsPlaceholder
        ? Name
        : $"{Name}\n{StatusLabel} · {ProgressPercent}%\n{PrototypeName}\n{DefectName}\n{ProductionApproval}";

    public static PlayerPrototypeRepairProject0195 From(IDictionary<string, object> map)
    {
        var item = new PlayerPrototypeRepairProject0195();
        item.Apply(map);
        return item;
    }

    public void Apply(IDictionary<string, object> map)
    {
        ProjectId = Read(map, "projectId");
        Name = Read(map, "name", "Ремонт прототипа");
        StatusLabel = Read(map, "statusLabel", "Состояние не указано");
        PrototypeName = Read(map, "prototypeName", Read(map, "targetItemName", "Прототип"));
        DefectName = Read(map, "defectName", "Дефект");
        ProductionApproval = Read(map, "productionApprovalLabel", "Не допущено к производству");
        ProgressPercent = int.TryParse(Read(map, "progressPercent"), out var progress) ? progress : 0;
        Revision = int.TryParse(Read(map, "revision"), out var revision) ? revision : 0;
    }

    public static PlayerPrototypeRepairProject0195 Placeholder(string text)
        => new() { Name = text, IsPlaceholder = true };

    private static string Read(IDictionary<string, object> map, string key, string fallback = "")
        => map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(Convert.ToString(raw))
            ? Convert.ToString(raw)!
            : fallback;
}
