using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed partial class AdminCraftingViewModel : ViewModelBase
{
    private const string AssetMaintenanceCampaignId0198 = "dev-campaign-core";
    private readonly CommandApi _api;
    private AdminCraftProjectItem0191? _selectedProject;
    private AdminCraftDetailLine0191? _selectedRequirement;
    private string _statusMessage = "Откройте проект изготовления или обновите очередь.";
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private AdminProjectKindChoice0192? _selectedProjectKind;

    public AdminCraftingViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
        ConfirmRequirementCommand = new RelayCommand(ConfirmRequirement);
        ApproveCommand = new RelayCommand(Approve);
        RejectCommand = new RelayCommand(Reject);
        ReserveCommand = new RelayCommand(Reserve);
        StartCommand = new RelayCommand(Start);
        CompleteStageCommand = new RelayCommand(CompleteStage);
        ExecuteTestCommand = new RelayCommand(ExecuteTest);
        ExecuteRetestCommand = new RelayCommand(ExecuteRetest);
        ApproveLimitedProductionCommand = new RelayCommand(ApproveLimitedProduction);
        CompleteCommand = new RelayCommand(Complete);
        CancelCommand = new RelayCommand(Cancel);
        FailCommand = new RelayCommand(Fail);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
        ProjectKinds.Add(new AdminProjectKindChoice0192("craft", "Изготовление"));
        ProjectKinds.Add(new AdminProjectKindChoice0192("research", "Исследование теории"));
        ProjectKinds.Add(new AdminProjectKindChoice0192("reverse", "Обратная инженерия"));
        ProjectKinds.Add(new AdminProjectKindChoice0192("prototype", "Создание прототипа"));
        ProjectKinds.Add(new AdminProjectKindChoice0192("prototype_repair", "Ремонт прототипа"));
        ProjectKinds.Add(new AdminProjectKindChoice0192("limited_production", "Ограниченная партия"));
        ProjectKinds.Add(new AdminProjectKindChoice0192("asset_construction", "Строительство актива"));
        ProjectKinds.Add(new AdminProjectKindChoice0192("asset_maintenance", "Эксплуатация и обслуживание"));
        InitializeAssetOperationAdmin0198();
        _selectedProjectKind = ProjectKinds[0];
    }

    public ObservableCollection<AdminProjectKindChoice0192> ProjectKinds { get; } = new();
    public ObservableCollection<AdminCraftProjectItem0191> Projects { get; } = new();
    public ObservableCollection<AdminCraftDetailLine0191> Requirements { get; } = new();
    public ObservableCollection<AdminCraftDetailLine0191> Resources { get; } = new();
    public ObservableCollection<AdminCraftDetailLine0191> Stages { get; } = new();
    public ObservableCollection<AdminCraftDetailLine0191> Audit { get; } = new();
    public ObservableCollection<AdminCraftDetailLine0191> TestHistory { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ConfirmRequirementCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand RejectCommand { get; }
    public ICommand ReserveCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand CompleteStageCommand { get; }
    public ICommand ExecuteTestCommand { get; }
    public ICommand ExecuteRetestCommand { get; }
    public ICommand ApproveLimitedProductionCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand FailCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public AdminCraftProjectItem0191? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (_selectedProject == value) return;
            _selectedProject = value;
            Notify();
            Notify(nameof(HasSelectedProject));
            Notify(nameof(CanExecuteRetest));
            Notify(nameof(CanApproveLimitedProduction));
            Notify(nameof(IsAssetConstructionMode));
            Notify(nameof(IsAssetMaintenanceMode));
            if (value != null && !value.IsPlaceholder) LoadSelectedProject();
        }
    }

    public AdminCraftDetailLine0191? SelectedRequirement
    {
        get => _selectedRequirement;
        set
        {
            if (_selectedRequirement == value) return;
            _selectedRequirement = value;
            Notify();
            Notify(nameof(CanConfirmSelectedRequirement));
        }
    }

    public bool HasSelectedProject => SelectedProject != null && !SelectedProject.IsPlaceholder;
    public bool CanConfirmSelectedRequirement => SelectedRequirement?.CanConfirm == true;
    public AdminProjectKindChoice0192? SelectedProjectKind
    {
        get => _selectedProjectKind;
        set
        {
            if (_selectedProjectKind == value) return;
            _selectedProjectKind = value;
            Notify();
            Notify(nameof(IsResearchMode));
            Notify(nameof(IsReverseEngineeringMode));
            Notify(nameof(IsPrototypeMode));
            Notify(nameof(IsPrototypeRepairMode));
            Notify(nameof(IsLimitedProductionMode));
            Notify(nameof(IsAssetConstructionMode));
            Notify(nameof(IsAssetMaintenanceMode));
            Notify(nameof(IsPrototypeActionMode));
            Notify(nameof(CanShowCompleteProject));
            Refresh();
        }
    }
    public bool IsResearchMode => string.Equals(SelectedProjectKind?.Key, "research", StringComparison.Ordinal);
    public bool IsReverseEngineeringMode => string.Equals(SelectedProjectKind?.Key, "reverse", StringComparison.Ordinal);
    public bool IsPrototypeMode => string.Equals(SelectedProjectKind?.Key, "prototype", StringComparison.Ordinal);
    public bool IsPrototypeRepairMode => string.Equals(SelectedProjectKind?.Key, "prototype_repair", StringComparison.Ordinal);
    public bool IsLimitedProductionMode => string.Equals(SelectedProjectKind?.Key, "limited_production", StringComparison.Ordinal);
    public bool IsAssetConstructionMode => string.Equals(SelectedProjectKind?.Key, "asset_construction", StringComparison.Ordinal);
    public bool IsAssetMaintenanceMode => string.Equals(SelectedProjectKind?.Key, "asset_maintenance", StringComparison.Ordinal);
    public bool IsPrototypeActionMode => IsPrototypeMode || IsPrototypeRepairMode;
    public bool CanShowCompleteProject => !IsPrototypeRepairMode;
    public bool CanExecuteRetest => IsPrototypeRepairMode
                                    && string.Equals(SelectedProject?.Status, "testing", StringComparison.Ordinal);
    public bool CanApproveLimitedProduction => IsPrototypeRepairMode
                                                && string.Equals(SelectedProject?.Status, "awaiting_acceptance", StringComparison.Ordinal);
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set { if (_isBusy != value) { _isBusy = value; Notify(); } } }

    private void Refresh()
    {
        Run(() =>
        {
            var selectedId = SelectedProject?.ProjectId;
            var campaignId = IsAssetMaintenanceMode ? AssetMaintenanceCampaignId0198 : "default";
            var response = ListProjects(new Dictionary<string, object> { ["campaignId"] = campaignId });
            EnsureOk(response);
            Projects.Clear();
            foreach (var item in Items(response, "items"))
                Projects.Add(AdminCraftProjectItem0191.From(item));
            if (Projects.Count == 0)
                Projects.Add(AdminCraftProjectItem0191.Placeholder(EmptyQueueMessage()));
            SelectedProject = Projects.FirstOrDefault(x => x.ProjectId == selectedId)
                              ?? Projects.FirstOrDefault(x => !x.IsPlaceholder);
            StatusMessage = $"Очередь обновлена: {Projects.Count(x => !x.IsPlaceholder)}.";
        });
        if (HasSelectedProject) LoadSelectedProject();
        if (IsAssetMaintenanceMode) RefreshAssetOperationAdmin0198();
    }

    private void LoadSelectedProject()
    {
        Run(() =>
        {
            var response = GetProject(new Dictionary<string, object>
            {
                ["projectId"] = SelectedProject!.ProjectId
            });
            EnsureOk(response);
            var item = Map(response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedProject.Apply(item);
            SelectedRequirement = null;
            Fill(Requirements, item, "requirements");
            Fill(Resources, item, "resources");
            Fill(Stages, item, "stages");
            Fill(Audit, item, "audit");
            Fill(TestHistory, item, "testHistory");
            StatusMessage = SelectedProject.StatusLabel;
            Notify(nameof(SelectedProject));
            Notify(nameof(CanExecuteRetest));
            Notify(nameof(CanApproveLimitedProduction));
        });
    }

    private void ConfirmRequirement()
    {
        if (!RequireProject() || SelectedRequirement == null || string.IsNullOrWhiteSpace(SelectedRequirement.InternalId))
        {
            ErrorMessage = "Выберите неподтверждённое условие проекта.";
            return;
        }
        Mutate(SelectCommand(
            _api.ProjectCraftRequirementConfirm,
            _api.ProjectResearchRequirementConfirm,
            _api.ProjectReverseEngineeringRequirementConfirm,
            _api.ProjectPrototypeRequirementConfirm,
            _api.ProjectPrototypeRepairRequirementConfirm,
            _api.ProjectLimitedProductionRequirementConfirm,
            _api.ProjectAssetConstructionRequirementConfirm,
            _api.ProjectAssetMaintenanceRequirementConfirm), "Условие подтверждено.", payload =>
        {
            payload["requirementId"] = SelectedRequirement.InternalId;
            payload["publicNote"] = "Условие подтверждено GM.";
        });
    }

    private void Approve() => Mutate(SelectCommand(
        _api.ProjectCraftApprove,
        _api.ProjectResearchApprove,
        _api.ProjectReverseEngineeringApprove,
        _api.ProjectPrototypeApprove,
        _api.ProjectPrototypeRepairApprove,
        _api.ProjectLimitedProductionApprove,
        _api.ProjectAssetConstructionApprove,
        _api.ProjectAssetMaintenanceApprove), "Проект одобрен GM.");

    private void Reject()
    {
        if (!Confirm("Отклонить проект? Игрок увидит, что проект завершён неудачей.")) return;
        Mutate(SelectCommand(
            _api.ProjectCraftReject,
            _api.ProjectResearchReject,
            _api.ProjectReverseEngineeringReject,
            _api.ProjectPrototypeReject,
            _api.ProjectPrototypeRepairReject,
            _api.ProjectLimitedProductionReject,
            _api.ProjectAssetConstructionReject,
            _api.ProjectAssetMaintenanceReject), "Проект отклонён.", x => x["publicReason"] = "Проект отклонён GM.");
    }

    private void Reserve()
    {
        if (!Confirm("Зарезервировать требуемые материалы в инвентаре персонажа?")) return;
        Mutate(SelectCommand(
            _api.ProjectCraftReserve,
            _api.ProjectResearchReserve,
            _api.ProjectReverseEngineeringReserve,
            _api.ProjectPrototypeReserve,
            _api.ProjectPrototypeRepairReserve,
            _api.ProjectLimitedProductionReserve,
            _api.ProjectAssetConstructionReserve,
            _api.ProjectAssetMaintenanceReserve), IsAssetConstructionMode ? "Материалы зарезервированы, площадка создана." : "Ресурсы зарезервированы.");
    }

    private void Start() => Mutate(SelectCommand(
        _api.ProjectCraftStart,
        _api.ProjectResearchStart,
        _api.ProjectReverseEngineeringStart,
        _api.ProjectPrototypeStart,
        _api.ProjectPrototypeRepairStart,
        _api.ProjectLimitedProductionStart,
        _api.ProjectAssetConstructionStart,
        _api.ProjectAssetMaintenanceStart), "Работа над проектом началась.");
    private void CompleteStage() => Mutate(SelectCommand(
        _api.ProjectCraftStageComplete,
        _api.ProjectResearchStageComplete,
        _api.ProjectReverseEngineeringStageComplete,
        _api.ProjectPrototypeStageComplete,
        _api.ProjectPrototypeRepairStageComplete,
        _api.ProjectLimitedProductionStageComplete,
        _api.ProjectAssetConstructionStageComplete,
        _api.ProjectAssetMaintenanceStageComplete), "Текущая стадия завершена.", payload =>
        {
            if (IsAssetConstructionMode || IsAssetMaintenanceMode)
                payload["stageKey"] = SelectedProject?.CurrentStageKey ?? string.Empty;
        });

    private void ExecuteTest()
    {
        if (!IsPrototypeMode)
        {
            ErrorMessage = "Испытание доступно только проекту создания прототипа.";
            return;
        }
        if (!Confirm("Провести обязательное испытание прототипа? Результат и дефект определяет сервер.")) return;
        Mutate(_api.ProjectPrototypeTestExecute, "Испытание проведено. Результат и выявленный дефект сохранены.");
    }

    private void ExecuteRetest()
    {
        if (!IsPrototypeRepairMode)
        {
            ErrorMessage = "Повторное испытание доступно только проекту ремонта прототипа.";
            return;
        }
        if (!Confirm("Провести повторное испытание? Результат определяет серверный TestProtocol.")) return;
        Mutate(_api.ProjectPrototypeRetestExecute,
            "Повторное испытание завершено: Attempt 2 — Pass.");
    }

    private void ApproveLimitedProduction()
    {
        if (!IsPrototypeRepairMode)
        {
            ErrorMessage = "Допуск к производству доступен только после ремонта и повторного испытания.";
            return;
        }
        if (!Confirm("Допустить прототип к ограниченному производству? Canonical Blueprint не изменится.")) return;
        Mutate(_api.ProjectPrototypeProductionApprove,
            "Прототип допущен к ограниченному производству.");
    }

    private void Complete()
    {
        if (IsPrototypeRepairMode)
        {
            ErrorMessage = "Ремонт завершается отдельным повторным испытанием и решением о допуске.";
            return;
        }
        var confirmation = IsPrototypeMode
            ? "Завершить проект прототипа после обязательного испытания? Прототип останется не допущенным к производству."
            : IsReverseEngineeringMode
            ? "Завершить разбор? Исходный предмет будет уничтожен, а открытие сохранено персонажу."
            : IsResearchMode
                ? "Завершить исследование, списать ресурсы и открыть знание персонажу?"
                : IsAssetConstructionMode
                    ? "Завершить строительство и создать крупный актив? Здание не попадёт в инвентарь персонажа."
                    : IsAssetMaintenanceMode
                        ? "Завершить обслуживание и вернуть тот же актив в эксплуатацию?"
                    : "Завершить проект, списать материалы и создать итоговый предмет?";
        if (!Confirm(confirmation)) return;
        Mutate(SelectCommand(
                _api.ProjectCraftComplete,
                _api.ProjectResearchComplete,
                _api.ProjectReverseEngineeringComplete,
                _api.ProjectPrototypeComplete,
                _api.ProjectPrototypeProductionApprove,
                _api.ProjectLimitedProductionComplete,
                _api.ProjectAssetConstructionComplete,
                _api.ProjectAssetMaintenanceComplete),
            IsPrototypeMode
                ? "Проект завершён: прототип сохранён с результатом испытания и открытым дефектом."
                : IsReverseEngineeringMode
                ? "Обратная инженерия завершена. Частное открытие сохранено персонажу."
                : IsResearchMode
                    ? "Исследование завершено. Знание открыто персонажу."
                    : IsAssetConstructionMode
                        ? "Строительство завершено. Крупный актив введён в эксплуатацию."
                        : IsAssetMaintenanceMode
                            ? "Обслуживание завершено. Актив снова эксплуатируется."
                        : "Проект завершён. Предмет добавлен в инвентарь.");
    }

    private void Cancel()
    {
        if (!Confirm("Отменить проект и освободить зарезервированные материалы?")) return;
        Mutate(SelectCommand(
            _api.ProjectCraftCancel,
            _api.ProjectResearchCancel,
            _api.ProjectReverseEngineeringCancel,
            _api.ProjectPrototypeCancel,
            _api.ProjectPrototypeRepairCancel,
            _api.ProjectLimitedProductionCancel,
            _api.ProjectAssetConstructionCancel,
            _api.ProjectAssetMaintenanceCancel), "Проект отменён.");
    }

    private void Fail()
    {
        if (!Confirm("Завершить проект неудачей и освободить резерв?")) return;
        Mutate(SelectCommand(
            _api.ProjectCraftFail,
            _api.ProjectResearchFail,
            _api.ProjectReverseEngineeringFail,
            _api.ProjectPrototypeFail,
            _api.ProjectPrototypeRepairFail,
            _api.ProjectLimitedProductionFail,
            _api.ProjectAssetConstructionFail,
            _api.ProjectAssetMaintenanceFail), "Проект завершён неудачей.", payload =>
        {
            payload["publicReason"] = "Проект завершён неудачей.";
            payload["gmReason"] = "Проект остановлен GM.";
        });
    }

    private void Mutate(
        Func<Dictionary<string, object>, ResponseEnvelope> command,
        string success,
        Action<Dictionary<string, object>>? extend = null)
    {
        if (!RequireProject()) return;
        var applied = false;
        Run(() =>
        {
            var payload = new Dictionary<string, object>
            {
                ["projectId"] = SelectedProject!.ProjectId,
                ["expectedRevision"] = SelectedProject.Revision,
                ["operationId"] = Guid.NewGuid().ToString("N")
            };
            extend?.Invoke(payload);
            EnsureOk(command(payload));
            StatusMessage = success;
            applied = true;
        });
        if (applied) Refresh();
    }

    private bool RequireProject()
    {
        ErrorMessage = string.Empty;
        if (HasSelectedProject) return true;
        ErrorMessage = IsPrototypeRepairMode
            ? "Выберите проект ремонта прототипа."
            : IsPrototypeMode
            ? "Выберите проект создания прототипа."
            : IsReverseEngineeringMode
            ? "Выберите проект обратной инженерии."
            : IsResearchMode
                ? "Выберите исследование теории."
                : "Выберите проект изготовления.";
        return false;
    }

    private ResponseEnvelope ListProjects(Dictionary<string, object> payload)
        => IsAssetMaintenanceMode
            ? _api.ProjectAssetMaintenanceList(payload)
            : IsAssetConstructionMode
            ? _api.ProjectAssetConstructionList(payload)
            : IsLimitedProductionMode
            ? _api.ProjectLimitedProductionList(payload)
            : IsPrototypeRepairMode
            ? _api.ProjectPrototypeRepairList(payload)
            : IsPrototypeMode
            ? _api.ProjectPrototypeList(payload)
            : IsReverseEngineeringMode
            ? _api.ProjectReverseEngineeringList(payload)
            : IsResearchMode
                ? _api.ProjectResearchList(payload)
                : _api.ProjectCraftList(payload);

    private ResponseEnvelope GetProject(Dictionary<string, object> payload)
        => IsAssetMaintenanceMode
            ? _api.ProjectAssetMaintenanceGet(payload)
            : IsAssetConstructionMode
            ? _api.ProjectAssetConstructionGet(payload)
            : IsLimitedProductionMode
            ? _api.ProjectLimitedProductionGet(payload)
            : IsPrototypeRepairMode
            ? _api.ProjectPrototypeRepairGet(payload)
            : IsPrototypeMode
            ? _api.ProjectPrototypeGet(payload)
            : IsReverseEngineeringMode
            ? _api.ProjectReverseEngineeringGet(payload)
            : IsResearchMode
                ? _api.ProjectResearchGet(payload)
                : _api.ProjectCraftGet(payload);

    private Func<Dictionary<string, object>, ResponseEnvelope> SelectCommand(
        Func<Dictionary<string, object>, ResponseEnvelope> craft,
        Func<Dictionary<string, object>, ResponseEnvelope> research,
        Func<Dictionary<string, object>, ResponseEnvelope> reverse,
        Func<Dictionary<string, object>, ResponseEnvelope> prototype,
        Func<Dictionary<string, object>, ResponseEnvelope> prototypeRepair,
        Func<Dictionary<string, object>, ResponseEnvelope> limitedProduction,
        Func<Dictionary<string, object>, ResponseEnvelope> assetConstruction,
        Func<Dictionary<string, object>, ResponseEnvelope> assetMaintenance)
        => IsAssetMaintenanceMode
            ? assetMaintenance
            : IsAssetConstructionMode
            ? assetConstruction
            : IsLimitedProductionMode
            ? limitedProduction
            : IsPrototypeRepairMode
            ? prototypeRepair
            : IsPrototypeMode ? prototype : IsReverseEngineeringMode ? reverse : IsResearchMode ? research : craft;

    private string EmptyQueueMessage()
        => IsAssetMaintenanceMode
            ? "Проекты обслуживания активов пока не созданы."
            : IsAssetConstructionMode
            ? "Проекты строительства активов пока не созданы."
            : IsLimitedProductionMode
            ? "Проекты ограниченных партий пока не созданы."
            : IsPrototypeRepairMode
            ? "Проекты ремонта прототипов пока не созданы."
            : IsPrototypeMode
            ? "Проекты создания прототипов пока не созданы."
            : IsReverseEngineeringMode
            ? "Проекты обратной инженерии пока не созданы."
            : IsResearchMode
                ? "Исследования теории пока не созданы."
                : "Проекты изготовления пока не созданы.";

    private static bool Confirm(string message)
        => MessageBox.Show(
               message,
               "Единый проект",
               MessageBoxButton.YesNo,
               MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void Run(Action action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try { action(); }
        catch (Exception ex)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? "Операция не выполнена." : ex.Message;
        }
        finally { IsBusy = false; }
    }

    private static void EnsureOk(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message)
                ? "Единые проекты недоступны."
                : response.Message);
    }

    private static void Fill(
        ObservableCollection<AdminCraftDetailLine0191> target,
        IDictionary<string, object> parent,
        string key)
    {
        target.Clear();
        if (!parent.TryGetValue(key, out var raw) || raw is not IEnumerable rows || raw is string) return;
        foreach (var row in rows)
        {
            var item = Map(row);
            if (item.Count > 0) target.Add(AdminCraftDetailLine0191.From(item));
        }
    }

    private static IEnumerable<IDictionary<string, object>> Items(ResponseEnvelope response, string key)
    {
        if (!response.Payload.TryGetValue(key, out var raw) || raw is not IEnumerable rows || raw is string) yield break;
        foreach (var row in rows)
        {
            var item = Map(row);
            if (item.Count > 0) yield return item;
        }
    }

    internal static Dictionary<string, object> Map(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (raw is not IDictionary source) return result;
        foreach (DictionaryEntry entry in source)
        {
            var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
        }
        return result;
    }
}

public sealed class AdminCraftProjectItem0191
{
    public string ProjectId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string OwnerDisplayName { get; private set; } = string.Empty;
    public string OwnerCharacterDisplayName { get; private set; } = string.Empty;
    public string RecipeName { get; private set; } = string.Empty;
    public string TechnologyName { get; private set; } = string.Empty;
    public string ProjectTypeLabel { get; private set; } = string.Empty;
    public string KnowledgeStatus { get; private set; } = string.Empty;
    public string MethodName { get; private set; } = string.Empty;
    public string TemplateName { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string ApprovalStatus { get; private set; } = string.Empty;
    public string CurrentStageName { get; private set; } = string.Empty;
    public string ResultName { get; private set; } = string.Empty;
    public string SourceItemName { get; private set; } = string.Empty;
    public string SourceItemStatus { get; private set; } = string.Empty;
    public string SourceItemDisposition { get; private set; } = string.Empty;
    public string ExpectedDiscovery { get; private set; } = string.Empty;
    public string BlueprintName { get; private set; } = string.Empty;
    public string TargetItemName { get; private set; } = string.Empty;
    public string PrototypeStatus { get; private set; } = string.Empty;
    public string PrototypeWarning { get; private set; } = string.Empty;
    public string TestProtocolName { get; private set; } = string.Empty;
    public string TestStatus { get; private set; } = string.Empty;
    public string TestResultCategory { get; private set; } = string.Empty;
    public string TestPublicSummary { get; private set; } = string.Empty;
    public string DefectName { get; private set; } = string.Empty;
    public string DefectSeverity { get; private set; } = string.Empty;
    public string DefectSymptoms { get; private set; } = string.Empty;
    public string DefectLimitations { get; private set; } = string.Empty;
    public string DefectGmCause { get; private set; } = string.Empty;
    public string DefectStatus { get; private set; } = string.Empty;
    public string ResolutionSummary { get; private set; } = string.Empty;
    public string ProductionApprovalLabel { get; private set; } = string.Empty;
    public int BatchSize { get; private set; }
    public int MaxUnits { get; private set; }
    public int ReservedUnits { get; private set; }
    public int ProducedUnits { get; private set; }
    public string AuthorizationStatus { get; private set; } = string.Empty;
    public string LimitedWarning { get; private set; } = string.Empty;
    public string CurrentStageKey { get; private set; } = string.Empty;
    public string ConstructionConfigurationSummary { get; private set; } = string.Empty;
    public string ConstructionLocationName { get; private set; } = string.Empty;
    public string ConstructionMethod { get; private set; } = string.Empty;
    public string ConstructionSiteStatusLabel { get; private set; } = string.Empty;
    public string ConstructionAssetKindLabel { get; private set; } = string.Empty;
    public string ConstructionWarning { get; private set; } = string.Empty;
    public string MaintenanceSummary { get; private set; } = string.Empty;
    public int ConstructionFloorCount { get; private set; }
    public string ConstructionArea { get; private set; } = string.Empty;
    public int ProgressPercent { get; private set; }
    public int Revision { get; private set; }
    public bool IsPlaceholder { get; private set; }
    public string QueueSummary => IsPlaceholder ? Name : $"{Name}\n{OwnerDisplayName} · {OwnerCharacterDisplayName} · {StatusLabel}";
    public string RuntimeSummary
    {
        get
        {
            var subject = AdminCraftParsing0191.First(SourceItemName, TechnologyName, RecipeName, Name);
            var knowledge = string.IsNullOrWhiteSpace(KnowledgeStatus) ? string.Empty : $"\nЗнание: {KnowledgeStatus}";
            var sourceState = string.IsNullOrWhiteSpace(SourceItemName)
                ? string.Empty
                : $"\nПредмет: {SourceItemName} · {SourceItemStatus}\nСудьба предмета: {SourceItemDisposition}";
            var discovery = string.IsNullOrWhiteSpace(ExpectedDiscovery)
                ? string.Empty
                : $"\nОжидаемое открытие: {ExpectedDiscovery}";
            var prototypeWarning = string.IsNullOrWhiteSpace(PrototypeWarning)
                ? string.Empty
                : $"\nВажно: {PrototypeWarning}";
            var prototype = string.IsNullOrWhiteSpace(BlueprintName)
                ? string.Empty
                : $"\nЧертёж: {BlueprintName}\nОпытный образец: {TargetItemName}\nСостояние: {PrototypeStatus}\nИспытание: {TestProtocolName} · {TestStatus}{prototypeWarning}\nИстория: {TestResultCategory}\nРезультат: {TestPublicSummary}\nДефект: {DefectName} · {DefectSeverity} · {DefectStatus}\nСимптомы: {DefectSymptoms}\nОграничения: {DefectLimitations}\nРешение: {ResolutionSummary}\nПричина для GM: {DefectGmCause}\nГотовность: {ProductionApprovalLabel}";
            var limited = BatchSize <= 0
                ? string.Empty
                : $"\nПартия: {BatchSize} шт. · лимит допуска {MaxUnits}"
                  + $"\nДопуск: произведено {ProducedUnits}, зарезервировано {ReservedUnits} · {AuthorizationStatus}"
                  + $"\n{LimitedWarning}";
            var construction = string.IsNullOrWhiteSpace(ConstructionLocationName)
                ? string.Empty
                : $"\nЧертёж: {BlueprintName}\nТип: {ConstructionAssetKindLabel}"
                  + $"\nМесто: {ConstructionLocationName}\nМетод: {ConstructionMethod}"
                  + $"\nЭтажи: {ConstructionFloorCount} · площадь: {ConstructionArea}"
                  + $"\nПлощадка: {ConstructionSiteStatusLabel}\n{ConstructionConfigurationSummary}"
                  + (string.IsNullOrWhiteSpace(ConstructionWarning) ? string.Empty : $"\nВажно: {ConstructionWarning}")
                  + (string.IsNullOrWhiteSpace(MaintenanceSummary) ? string.Empty : $"\nОбслуживание: {MaintenanceSummary}");
            return $"{ProjectTypeLabel}: {subject}\nМетод: {MethodName}\nШаблон: {TemplateName}{sourceState}{discovery}{knowledge}{prototype}{limited}{construction}";
        }
    }
    public string ProgressSummary => $"{ProgressPercent}% · {CurrentStageName}";
    public bool HasResult => !string.IsNullOrWhiteSpace(ResultName);

    public static AdminCraftProjectItem0191 From(IDictionary<string, object> map)
    {
        var item = new AdminCraftProjectItem0191();
        item.Apply(map);
        return item;
    }

    public void Apply(IDictionary<string, object> map)
    {
        ProjectId = AdminCraftParsing0191.Read(map, "projectId");
        Name = AdminCraftParsing0191.First(AdminCraftParsing0191.Read(map, "name"), "Проект");
        OwnerDisplayName = AdminCraftParsing0191.First(AdminCraftParsing0191.Read(map, "ownerDisplayName"), "Владелец не указан");
        OwnerCharacterDisplayName = AdminCraftParsing0191.First(
            AdminCraftParsing0191.Read(map, "ownerCharacterDisplayName"),
            "Персонаж не указан");
        RecipeName = AdminCraftParsing0191.Read(map, "recipeName");
        TechnologyName = AdminCraftParsing0191.Read(map, "technologyName");
        ProjectTypeLabel = AdminCraftParsing0191.First(
            AdminCraftParsing0191.Read(map, "projectTypeLabel"),
            string.IsNullOrWhiteSpace(TechnologyName) ? "Изготовление" : "Исследование теории");
        KnowledgeStatus = AdminCraftParsing0191.Read(map, "knowledgeStatus");
        SourceItemName = AdminCraftParsing0191.Read(map, "sourceItemName");
        SourceItemStatus = AdminCraftParsing0191.Read(map, "sourceItemStatus");
        SourceItemDisposition = AdminCraftParsing0191.Read(map, "sourceItemDisposition");
        ExpectedDiscovery = AdminCraftParsing0191.Read(map, "expectedDiscovery");
        BlueprintName = AdminCraftParsing0191.Read(map, "blueprintName");
        TargetItemName = AdminCraftParsing0191.Read(map, "targetItemName");
        PrototypeStatus = AdminCraftParsing0191.Read(map, "prototypeStatus");
        PrototypeWarning = AdminCraftParsing0191.Read(map, "prototypeWarning");
        TestProtocolName = AdminCraftParsing0191.Read(map, "testProtocolName");
        TestStatus = AdminCraftParsing0191.Read(map, "testStatus");
        TestResultCategory = AdminCraftParsing0191.Read(map, "testResultCategory");
        TestPublicSummary = AdminCraftParsing0191.Read(map, "testPublicSummary");
        DefectName = AdminCraftParsing0191.Read(map, "defectName");
        DefectSeverity = AdminCraftParsing0191.Read(map, "defectSeverity");
        DefectSymptoms = AdminCraftParsing0191.Read(map, "defectSymptoms");
        DefectLimitations = AdminCraftParsing0191.Read(map, "defectLimitations");
        DefectGmCause = AdminCraftParsing0191.Read(map, "defectGmCause");
        DefectStatus = AdminCraftParsing0191.Read(map, "defectStatus");
        ResolutionSummary = AdminCraftParsing0191.Read(map, "resolutionSummary");
        ProductionApprovalLabel = AdminCraftParsing0191.Read(map, "productionApprovalLabel");
        BatchSize = AdminCraftParsing0191.ReadInt(map, "batchSize");
        MaxUnits = AdminCraftParsing0191.ReadInt(map, "maxUnits");
        LimitedWarning = AdminCraftParsing0191.Read(map, "warning");
        if (map.TryGetValue("authorization", out var rawAuthorization))
        {
            var authorization = AdminCraftingViewModel.Map(rawAuthorization);
            ReservedUnits = AdminCraftParsing0191.ReadInt(authorization, "reservedUnits");
            ProducedUnits = AdminCraftParsing0191.ReadInt(authorization, "producedUnits");
            AuthorizationStatus = AdminCraftParsing0191.Read(authorization, "status");
        }
        MethodName = AdminCraftParsing0191.Read(map, "methodName");
        TemplateName = AdminCraftParsing0191.Read(map, "templateName");
        StatusLabel = AdminCraftParsing0191.First(AdminCraftParsing0191.Read(map, "statusLabel"), "Состояние не указано");
        Status = AdminCraftParsing0191.Read(map, "status");
        ApprovalStatus = AdminCraftParsing0191.Read(map, "approvalStatus");
        CurrentStageName = AdminCraftParsing0191.First(AdminCraftParsing0191.Read(map, "currentStageName"), "Работа ещё не началась");
        CurrentStageKey = AdminCraftParsing0191.Read(map, "currentStageKey");
        ConstructionConfigurationSummary = AdminCraftParsing0191.Read(map, "configurationSummary");
        ConstructionLocationName = AdminCraftParsing0191.Read(map, "locationName");
        ConstructionMethod = AdminCraftParsing0191.Read(map, "constructionMethod");
        ConstructionSiteStatusLabel = AdminCraftParsing0191.Read(map, "siteStatusLabel");
        ConstructionAssetKindLabel = AdminCraftParsing0191.Read(map, "assetKindLabel");
        ConstructionWarning = AdminCraftParsing0191.Read(map, "warning");
        ConstructionFloorCount = AdminCraftParsing0191.ReadInt(map, "floorCount");
        ConstructionArea = AdminCraftParsing0191.Read(map, "totalArea");
        ProgressPercent = AdminCraftParsing0191.ReadInt(map, "progressPercent");
        Revision = AdminCraftParsing0191.ReadInt(map, "revision");
        if (map.TryGetValue("result", out var raw))
            ResultName = AdminCraftParsing0191.Read(AdminCraftingViewModel.Map(raw), "name");
        if (map.TryGetValue("asset", out var rawAsset))
            ResultName = AdminCraftParsing0191.First(AdminCraftParsing0191.Read(AdminCraftingViewModel.Map(rawAsset), "name"), ResultName);
        if (map.TryGetValue("maintenance", out var rawMaintenance))
            MaintenanceSummary = AdminCraftParsing0191.Read(AdminCraftingViewModel.Map(rawMaintenance), "statusLabel");
    }

    public static AdminCraftProjectItem0191 Placeholder(string text)
        => new() { Name = text, IsPlaceholder = true };
}

public sealed class AdminProjectKindChoice0192
{
    public AdminProjectKindChoice0192(string key, string name)
    {
        Key = key;
        Name = name;
    }

    public string Key { get; }
    public string Name { get; }
    public override string ToString() => Name;
}

public sealed class AdminCraftDetailLine0191
{
    public string InternalId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string RawStatus { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public bool CanConfirm => !string.IsNullOrWhiteSpace(InternalId)
                              && (string.Equals(RawStatus, "open", StringComparison.Ordinal)
                                  || string.Equals(
                                      RawStatus,
                                      "gm_confirmation",
                                      StringComparison.Ordinal));
    public string Display => string.IsNullOrWhiteSpace(Summary)
        ? $"{Name}\n{Status}"
        : $"{Name}\n{Status} · {Summary}";

    public static AdminCraftDetailLine0191 From(IDictionary<string, object> map)
    {
        var quantity = AdminCraftParsing0191.First(
            AdminCraftParsing0191.Read(map, "quantityRequired"),
            AdminCraftParsing0191.Read(map, "quantity"));
        var unit = AdminCraftParsing0191.Read(map, "unit");
        var summary = AdminCraftParsing0191.First(
            AdminCraftParsing0191.Read(map, "summary"),
            AdminCraftParsing0191.Read(map, "publicSummary"));
        if (!string.IsNullOrWhiteSpace(quantity))
            summary = $"{quantity} {unit} · {summary}".Trim(' ', '·');
        return new AdminCraftDetailLine0191
        {
            InternalId = AdminCraftParsing0191.Read(map, "requirementId"),
            Name = AdminCraftParsing0191.First(
                AdminCraftParsing0191.Read(map, "name"),
                AdminCraftParsing0191.Read(map, "action"),
                string.IsNullOrWhiteSpace(AdminCraftParsing0191.Read(map, "attemptNumber"))
                    ? string.Empty
                    : "Испытание " + AdminCraftParsing0191.Read(map, "attemptNumber"),
                "Запись"),
            RawStatus = AdminCraftParsing0191.Read(map, "status"),
            Status = AdminCraftParsing0191.First(
                AdminCraftParsing0191.Read(map, "statusLabel"),
                AdminCraftParsing0191.Read(map, "status"),
                AdminCraftParsing0191.Read(map, "result"),
                AdminCraftParsing0191.Read(map, "actorDisplayName")),
            Summary = summary
        };
    }
}

internal static class AdminCraftParsing0191
{
    internal static string Read(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value != null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

    internal static string First(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    internal static int ReadInt(IDictionary<string, object> map, string key)
        => int.TryParse(Read(map, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
