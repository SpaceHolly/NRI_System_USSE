using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed partial class SystemToolsViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly List<FeatureFlagUiItem> _allFeatureFlags = new List<FeatureFlagUiItem>();
    private int _selectedTabIndex;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private string _statusMessage = "Системные инструменты готовы.";
    private string _featureFlagSearchText = string.Empty;
    private FeatureFlagUiItem? _selectedFeatureFlag;
    private DateTime _lastRunAtUtc;
    private string _combatSmokeCampaignId = "dev-campaign";
    private string _combatSmokeSessionId = "dev-session";
    private string _combatSmokeRuleSetId = "fantasy_nri_default";
    private bool _combatSmokeRunWrite;
    private string _definitionPackPath = Path.Combine("Nri.Server", "Content", "DefinitionPacks", "fantasy_nri_default_starter");
    private string _inventoryCharacterId = string.Empty;
    private string _inventoryRuleSetId = "fantasy_nri_default";
    private string _inventoryCampaignId = string.Empty;
    private string _serverStatusSummary = "Статус сервера не загружен.";
    private string _clientStatusSummary = "Состояние клиента не загружено.";
    private string _logsStatus = "Логи клиента не загружены.";
    private string _backupStatus = "Резервные копии не загружены.";
    private string _backupLabel = "manual-backup";
    private string _backupDescription = string.Empty;
    private BackupListUiItem? _selectedBackup;
    private bool _backupMaintenanceEnabled;
    private string _backupMaintenanceReason = string.Empty;
    private string _restoreReason = string.Empty;
    private string _restoreConfirmation = string.Empty;
    private string _restorePreviewStatus = "Предпросмотр восстановления не запускался.";
    private string _restoreOperationStatus = "Восстановление не запускалось.";
    private string _combatSmokeSummary = "Проверка боя не запускалась.";
    private string _definitionDryRunSummary = "Проверка справочников не запускалась.";
    private string _inventoryDiagnosticsSummary = "Диагностика инвентаря не запускалась.";

    public SystemToolsViewModel(CommandApi api)
    {
        _api = api;
        RefreshAllCommand = new RelayCommand(RefreshAll, () => CanRun);
        LoadFeatureFlagsCommand = new RelayCommand(LoadFeatureFlags, () => CanRun);
        ToggleFeatureFlagCommand = new RelayCommand(ToggleSelectedFeatureFlag, () => CanModifySelectedFeatureFlag);
        ResetFeatureFlagCommand = new RelayCommand(ResetSelectedFeatureFlag, () => CanClearSelectedFeatureFlag);
        RunCombatSmokeCommand = new RelayCommand(RunCombatSmoke);
        RunDefinitionDryRunCommand = new RelayCommand(RunDefinitionDryRun);
        RunInventoryDiagnosticsCommand = new RelayCommand(RunInventoryDiagnostics);
        RefreshServerStatusCommand = new RelayCommand(RefreshServerStatus);
        RefreshLogsCommand = new RelayCommand(RefreshLogs);
        RefreshBackupsCommand = new RelayCommand(RefreshBackups);
        CreateBackupCommand = new RelayCommand(CreateBackup);
        VerifyBackupCommand = new RelayCommand(VerifyBackup);
        PreviewRestoreCommand = new RelayCommand(PreviewRestore);
        ExecuteRestoreCommand = new RelayCommand(ExecuteRestore);
        RefreshBackupMaintenanceCommand = new RelayCommand(RefreshBackupMaintenance);
        SetBackupMaintenanceCommand = new RelayCommand(SetBackupMaintenance);
        InitializeDataPortabilityCommands();
        ClearErrorCommand = new RelayCommand(() => { ErrorMessage = string.Empty; StatusMessage = "Ошибки очищены."; });
        RefreshLocalClientStatus();
    }

    public ObservableCollection<SystemMetricUiItem> OverviewCards { get; } = new ObservableCollection<SystemMetricUiItem>();
    public ObservableCollection<FeatureFlagUiItem> FeatureFlags { get; } = new ObservableCollection<FeatureFlagUiItem>();
    public ObservableCollection<CombatSmokeStepUiItem> CombatSmokeSteps { get; } = new ObservableCollection<CombatSmokeStepUiItem>();
    public ObservableCollection<SystemIssueUiItem> CombatSmokeIssues { get; } = new ObservableCollection<SystemIssueUiItem>();
    public ObservableCollection<SystemIssueUiItem> DefinitionDryRunIssues { get; } = new ObservableCollection<SystemIssueUiItem>();
    public ObservableCollection<DefinitionFileValidationUiItem> DefinitionDryRunFiles { get; } = new ObservableCollection<DefinitionFileValidationUiItem>();
    public ObservableCollection<SystemIssueUiItem> InventoryDiagnosticsIssues { get; } = new ObservableCollection<SystemIssueUiItem>();
    public ObservableCollection<SystemSectionUiItem> InventoryDiagnosticsSections { get; } = new ObservableCollection<SystemSectionUiItem>();
    public ObservableCollection<SystemMetricUiItem> ServerMetrics { get; } = new ObservableCollection<SystemMetricUiItem>();
    public ObservableCollection<LogLineUiItem> ClientLogLines { get; } = new ObservableCollection<LogLineUiItem>();
    public ObservableCollection<BackupListUiItem> Backups { get; } = new ObservableCollection<BackupListUiItem>();

    public ICommand RefreshAllCommand { get; }
    public ICommand LoadFeatureFlagsCommand { get; }
    public ICommand ToggleFeatureFlagCommand { get; }
    public ICommand ResetFeatureFlagCommand { get; }
    public ICommand RunCombatSmokeCommand { get; }
    public ICommand RunDefinitionDryRunCommand { get; }
    public ICommand RunInventoryDiagnosticsCommand { get; }
    public ICommand RefreshServerStatusCommand { get; }
    public ICommand RefreshLogsCommand { get; }
    public ICommand RefreshBackupsCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand VerifyBackupCommand { get; }
    public ICommand PreviewRestoreCommand { get; }
    public ICommand ExecuteRestoreCommand { get; }
    public ICommand RefreshBackupMaintenanceCommand { get; }
    public ICommand SetBackupMaintenanceCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public int SelectedTabIndex { get => _selectedTabIndex; set { if (_selectedTabIndex != value) { _selectedTabIndex = value; Notify(); } } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); Notify(nameof(CanRun)); Notify(nameof(CanModifySelectedFeatureFlag)); Notify(nameof(CanClearSelectedFeatureFlag)); RaiseFeatureFlagCommandStates(); } } }
    public bool CanRun => !IsLoading;
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public DateTime LastRunAtUtc { get => _lastRunAtUtc; private set { if (_lastRunAtUtc != value) { _lastRunAtUtc = value; Notify(); Notify(nameof(LastRunText)); } } }
    public string LastRunText => LastRunAtUtc == default ? "ещё не запускалось" : LastRunAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string FeatureFlagSearchText
    {
        get => _featureFlagSearchText;
        set
        {
            if (_featureFlagSearchText != value)
            {
                _featureFlagSearchText = value;
                Notify();
                ApplyFeatureFlagFilter();
            }
        }
    }
    public FeatureFlagUiItem? SelectedFeatureFlag
    {
        get => _selectedFeatureFlag;
        set
        {
            if (_selectedFeatureFlag != value)
            {
                _selectedFeatureFlag = value;
                Notify();
                Notify(nameof(SelectedFeatureFlagSummary));
                Notify(nameof(SelectedFeatureFlagToggleText));
                Notify(nameof(CanModifySelectedFeatureFlag));
                Notify(nameof(CanClearSelectedFeatureFlag));
                RaiseFeatureFlagCommandStates();
            }
        }
    }
    public bool CanModifySelectedFeatureFlag => !IsLoading && SelectedFeatureFlag != null;
    public bool CanClearSelectedFeatureFlag => !IsLoading && SelectedFeatureFlag?.HasOverride == true;
    public string SelectedFeatureFlagSummary => SelectedFeatureFlag == null
        ? "Выберите функцию или модуль."
        : $"{SelectedFeatureFlag.DisplayName}: сейчас={SelectedFeatureFlag.EffectiveValue}, источник={SelectedFeatureFlag.SourceDisplay}";
    public string SelectedFeatureFlagToggleText => SelectedFeatureFlag?.EffectiveValue == true ? "Переопределить: выключено" : "Переопределить: включено";

    public string CombatSmokeCampaignId { get => _combatSmokeCampaignId; set { if (_combatSmokeCampaignId != value) { _combatSmokeCampaignId = value; Notify(); } } }
    public string CombatSmokeSessionId { get => _combatSmokeSessionId; set { if (_combatSmokeSessionId != value) { _combatSmokeSessionId = value; Notify(); } } }
    public string CombatSmokeRuleSetId { get => _combatSmokeRuleSetId; set { if (_combatSmokeRuleSetId != value) { _combatSmokeRuleSetId = value; Notify(); } } }
    public bool CombatSmokeRunWrite { get => _combatSmokeRunWrite; set { if (_combatSmokeRunWrite != value) { _combatSmokeRunWrite = value; Notify(); } } }
    public string DefinitionPackPath { get => _definitionPackPath; set { if (_definitionPackPath != value) { _definitionPackPath = value; Notify(); } } }
    public string InventoryCharacterId { get => _inventoryCharacterId; set { if (_inventoryCharacterId != value) { _inventoryCharacterId = value; Notify(); } } }
    public string InventoryRuleSetId { get => _inventoryRuleSetId; set { if (_inventoryRuleSetId != value) { _inventoryRuleSetId = value; Notify(); } } }
    public string InventoryCampaignId { get => _inventoryCampaignId; set { if (_inventoryCampaignId != value) { _inventoryCampaignId = value; Notify(); } } }
    public string ServerStatusSummary { get => _serverStatusSummary; private set { if (_serverStatusSummary != value) { _serverStatusSummary = value; Notify(); } } }
    public string ClientStatusSummary { get => _clientStatusSummary; private set { if (_clientStatusSummary != value) { _clientStatusSummary = value; Notify(); } } }
    public string LogsStatus { get => _logsStatus; private set { if (_logsStatus != value) { _logsStatus = value; Notify(); } } }
    public string BackupStatus { get => _backupStatus; private set { if (_backupStatus != value) { _backupStatus = value; Notify(); } } }
    public string BackupLabel { get => _backupLabel; set { if (_backupLabel != value) { _backupLabel = value; Notify(); } } }
    public string BackupDescription { get => _backupDescription; set { if (_backupDescription != value) { _backupDescription = value; Notify(); } } }
    public BackupListUiItem? SelectedBackup
    {
        get => _selectedBackup;
        set
        {
            if (_selectedBackup != value)
            {
                _selectedBackup = value;
                Notify();
                Notify(nameof(SelectedBackupId));
                Notify(nameof(RestoreConfirmationPhrase));
            }
        }
    }
    public string SelectedBackupId => SelectedBackup?.BackupId ?? string.Empty;
    public bool BackupMaintenanceEnabled { get => _backupMaintenanceEnabled; set { if (_backupMaintenanceEnabled != value) { _backupMaintenanceEnabled = value; Notify(); } } }
    public string BackupMaintenanceReason { get => _backupMaintenanceReason; set { if (_backupMaintenanceReason != value) { _backupMaintenanceReason = value; Notify(); } } }
    public string RestoreReason { get => _restoreReason; set { if (_restoreReason != value) { _restoreReason = value; Notify(); } } }
    public string RestoreConfirmation { get => _restoreConfirmation; set { if (_restoreConfirmation != value) { _restoreConfirmation = value; Notify(); } } }
    public string RestoreConfirmationPhrase => string.IsNullOrWhiteSpace(SelectedBackupId) ? "Выберите резервную копию." : "RESTORE";
    public string RestorePreviewStatus { get => _restorePreviewStatus; private set { if (_restorePreviewStatus != value) { _restorePreviewStatus = value; Notify(); } } }
    public string RestoreOperationStatus { get => _restoreOperationStatus; private set { if (_restoreOperationStatus != value) { _restoreOperationStatus = value; Notify(); } } }
    public string CombatSmokeSummary { get => _combatSmokeSummary; private set { if (_combatSmokeSummary != value) { _combatSmokeSummary = value; Notify(); } } }
    public string DefinitionDryRunSummary { get => _definitionDryRunSummary; private set { if (_definitionDryRunSummary != value) { _definitionDryRunSummary = value; Notify(); } } }
    public string InventoryDiagnosticsSummary { get => _inventoryDiagnosticsSummary; private set { if (_inventoryDiagnosticsSummary != value) { _inventoryDiagnosticsSummary = value; Notify(); } } }

    public void SelectTab(string tabKey)
    {
        SelectedTabIndex = tabKey switch
        {
            "flags" => 1,
            "smoke" => 2,
            "logs" => 3,
            "backups" => 4,
            "data" => 5,
            "server" => 6,
            "definition_check" => 7,
            "inventory" => 8,
            _ => 0
        };

        if (SelectedTabIndex == 1 && FeatureFlags.Count == 0 && !IsLoading)
            LoadFeatureFlags();
    }

    private void RefreshAll()
    {
        RunSafe("system.ui.refresh_all", () =>
        {
            RefreshLocalClientStatus();
            RefreshServerStatusCore();
            LoadFeatureFlagsCore();
            RefreshLogsCore();
            RefreshBackupsCore();
            RefreshBackupMaintenanceCore();
            RefreshOverviewCards();
        });
    }

    private void LoadFeatureFlags() => RunSafe("system.ui.feature_flags.load", LoadFeatureFlagsCore);
    private void ToggleSelectedFeatureFlag() => RunSafe("system.ui.feature_flags.toggle", ToggleSelectedFeatureFlagCore);
    private void ResetSelectedFeatureFlag() => RunSafe("system.ui.feature_flags.reset", ResetSelectedFeatureFlagCore);
    private void RunCombatSmoke() => RunSafe("system.ui.combat_smoke", RunCombatSmokeCore);
    private void RunDefinitionDryRun() => RunSafe("system.ui.definition_dryrun", RunDefinitionDryRunCore);
    private void RunInventoryDiagnostics() => RunSafe("system.ui.inventory_diagnostics", RunInventoryDiagnosticsCore);
    private void RefreshServerStatus() => RunSafe("system.ui.server_status.refresh", () => { RefreshLocalClientStatus(); RefreshServerStatusCore(); RefreshOverviewCards(); });
    private void RefreshLogs() => RunSafe("system.ui.logs.opened", RefreshLogsCore);
    private void RefreshBackups() => RunSafe("system.ui.backups.refresh", () => { RefreshBackupsCore(); RefreshBackupMaintenanceCore(); });
    private void CreateBackup() => RunSafe("system.ui.backups.create", CreateBackupCore);
    private void VerifyBackup() => RunSafe("system.ui.backups.verify", VerifyBackupCore);
    private void PreviewRestore() => RunSafe("system.ui.backups.restore_preview", PreviewRestoreCore);
    private void ExecuteRestore() => RunSafe("system.ui.backups.restore_execute", ExecuteRestoreCore);
    private void RefreshBackupMaintenance() => RunSafe("system.ui.backups.maintenance_refresh", RefreshBackupMaintenanceCore);
    private void SetBackupMaintenance() => RunSafe("system.ui.backups.maintenance_set", SetBackupMaintenanceCore);

    private void RunSafe(string operation, Action action)
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        ClientLogService.Instance.Info(operation + ".start");
        try
        {
            action();
            LastRunAtUtc = DateTime.UtcNow;
            ClientLogService.Instance.Info(operation + ".done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn(operation + ".error message=" + ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadFeatureFlagsCore()
    {
        ClientLogService.Instance.Info("featureFlags.ui.load.start");
        var response = _api.FeatureFlagsAdminList();
        ClientLogService.Instance.Info($"featureFlags.ui.load.response status={response.Status} message={response.Message}");
        if (!IsOk(response))
        {
            StatusMessage = Friendly(response, "Снимок функций и модулей недоступен.");
            ErrorMessage = StatusMessage;
            return;
        }

        var flagsSource = Get(response.Payload, "flags");
        ClientLogService.Instance.Info($"featureFlags.ui.load.payloadType={response.Payload?.GetType().Name ?? "null"}");
        if (flagsSource == null)
        {
            var snapshot = AsDictionary(Get(response.Payload, "snapshot"));
            if (snapshot != null)
                flagsSource = Get(snapshot, "flags");
        }

        var serverCount = AsList(flagsSource).Count;
        ClientLogService.Instance.Info($"featureFlags.ui.load.serverCount={serverCount}");
        _allFeatureFlags.Clear();
        var parsedCount = 0;
        foreach (var map in AsDictionaries(flagsSource))
        {
            var name = Str(map, "name");
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var source = Str(map, "source", "default");
            var hasOverride = string.Equals(source, "database override", StringComparison.OrdinalIgnoreCase)
                || string.Equals(source, "database", StringComparison.OrdinalIgnoreCase);
            _allFeatureFlags.Add(new FeatureFlagUiItem
            {
                Name = name,
                Category = FirstNonEmpty(Str(map, "category"), CategoryForFlag(name)),
                DefaultValue = Bool(map, "defaultValue"),
                EffectiveValue = Bool(map, "effectiveValue"),
                Source = source,
                HasOverride = hasOverride,
                IsOverridden = hasOverride,
                Description = Str(map, "description"),
                UpdatedAtUtc = Str(map, "updatedAtUtc"),
                UpdatedByUserId = Str(map, "updatedByUserId")
            });
            parsedCount++;
        }

        ApplyFeatureFlagFilter();
        if (SelectedFeatureFlag == null && FeatureFlags.Count > 0)
            SelectedFeatureFlag = FeatureFlags[0];
        var enabled = _allFeatureFlags.Count(flag => flag.EffectiveValue);
        ClientLogService.Instance.Info($"featureFlags.ui.load.parsedCount={parsedCount}");
        ClientLogService.Instance.Info($"featureFlags.ui.load.collectionCount={FeatureFlags.Count}");
        ClientLogService.Instance.Info("featureFlags.ui.load.firstFlags=" + string.Join(",", _allFeatureFlags.Take(5).Select(x => x.Name)));
        if (_allFeatureFlags.Count == 0)
        {
            StatusMessage = "Функции и модули не загружены: сервер вернул пустой список или неожиданный payload.";
            ErrorMessage = StatusMessage;
            ClientLogService.Instance.Warn("system.ui.feature_flags.empty payloadKeys=" + string.Join(",", response.Payload.Keys));
        }
        else
        {
            ErrorMessage = string.Empty;
            StatusMessage = $"Функций и модулей загружено: {_allFeatureFlags.Count}; включено: {enabled}. Переопределения можно менять из этой вкладки.";
        }
        ClientLogService.Instance.Info("featureFlags.ui.load.done");
        RefreshOverviewCards();
    }

    private void ToggleSelectedFeatureFlagCore()
    {
        if (SelectedFeatureFlag == null)
        {
            StatusMessage = "Выберите функцию или модуль.";
            return;
        }

        var nextValue = !SelectedFeatureFlag.EffectiveValue;
        var selectedName = SelectedFeatureFlag.Name;
        ClientLogService.Instance.Info($"featureFlags.ui.set.start flag={selectedName} value={nextValue}");
        ClientLogService.Instance.Info("featureFlags.api.set.start");
        var response = _api.FeatureFlagsAdminSetOverride(selectedName, nextValue, "AdminClient ручное переопределение");
        ClientLogService.Instance.Info($"featureFlags.api.set.done status={response.Status} message={response.Message}");
        if (!IsOk(response))
        {
            StatusMessage = Friendly(response, "Не удалось сохранить переопределение функции.");
            ErrorMessage = StatusMessage;
            return;
        }

        StatusMessage = $"Переопределение сохранено: {SelectedFeatureFlag.DisplayName} = {nextValue}.";
        ClientLogService.Instance.Info("featureFlags.ui.reload.start");
        LoadFeatureFlagsCore();
        SelectedFeatureFlag = FeatureFlags.FirstOrDefault(x => string.Equals(x.Name, selectedName, StringComparison.OrdinalIgnoreCase)) ?? SelectedFeatureFlag;
        ClientLogService.Instance.Info($"featureFlags.ui.reload.done count={FeatureFlags.Count}");
        ClientLogService.Instance.Info($"featureFlags.ui.set.done flag={selectedName} effective={SelectedFeatureFlag?.EffectiveValue} source={SelectedFeatureFlag?.Source}");
        StatusMessage = $"Переопределение сохранено: {SelectedFeatureFlag?.DisplayName ?? selectedName}; сейчас={SelectedFeatureFlag?.EffectiveValue}; источник={SelectedFeatureFlag?.SourceDisplay}.";
    }

    private void ResetSelectedFeatureFlagCore()
    {
        if (SelectedFeatureFlag == null)
        {
            StatusMessage = "Выберите функцию или модуль.";
            return;
        }

        var selectedName = SelectedFeatureFlag.Name;
        ClientLogService.Instance.Info($"featureFlags.ui.clear.start flag={selectedName}");
        var response = _api.FeatureFlagsAdminClearOverride(selectedName);
        if (!IsOk(response))
        {
            StatusMessage = Friendly(response, "Не удалось сбросить переопределение функции.");
            ErrorMessage = StatusMessage;
            return;
        }

        StatusMessage = $"Переопределение сброшено: {selectedName}.";
        ClientLogService.Instance.Info("featureFlags.ui.reload.start");
        LoadFeatureFlagsCore();
        SelectedFeatureFlag = FeatureFlags.FirstOrDefault(x => string.Equals(x.Name, selectedName, StringComparison.OrdinalIgnoreCase)) ?? SelectedFeatureFlag;
        ClientLogService.Instance.Info($"featureFlags.ui.reload.done count={FeatureFlags.Count}");
        ClientLogService.Instance.Info($"featureFlags.ui.clear.done flag={selectedName} effective={SelectedFeatureFlag?.EffectiveValue} source={SelectedFeatureFlag?.Source}");
        StatusMessage = $"Переопределение сброшено: {selectedName}; сейчас={SelectedFeatureFlag?.EffectiveValue}; источник={SelectedFeatureFlag?.SourceDisplay}.";
    }

    private void ApplyFeatureFlagFilter()
    {
        var selectedName = SelectedFeatureFlag?.Name ?? string.Empty;
        FeatureFlags.Clear();
        var query = (FeatureFlagSearchText ?? string.Empty).Trim();
        var items = _allFeatureFlags.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(item =>
                Contains(item.Name, query)
                || Contains(item.Category, query)
                || Contains(item.Source, query)
                || Contains(item.Description, query));
        }

        foreach (var item in items.OrderBy(x => x.Category).ThenBy(x => x.Name))
            FeatureFlags.Add(item);

        SelectedFeatureFlag = FeatureFlags.FirstOrDefault(x => string.Equals(x.Name, selectedName, StringComparison.OrdinalIgnoreCase))
            ?? FeatureFlags.FirstOrDefault();
    }

    private void RaiseFeatureFlagCommandStates()
    {
        (LoadFeatureFlagsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleFeatureFlagCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetFeatureFlagCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RefreshAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void RunCombatSmokeCore()
    {
        var response = _api.CombatV1SmokeRun(new Dictionary<string, object>
        {
            { "campaignId", CombatSmokeCampaignId },
            { "sessionId", CombatSmokeSessionId },
            { "ruleSetId", CombatSmokeRuleSetId },
            { "runWriteSmoke", CombatSmokeRunWrite }
        });

        CombatSmokeSteps.Clear();
        CombatSmokeIssues.Clear();
        if (!IsOk(response))
        {
            CombatSmokeSummary = Friendly(response, "Проверка боя недоступна.");
            StatusMessage = CombatSmokeSummary;
            return;
        }

        var payload = response.Payload;
        foreach (var step in AsDictionaries(Get(payload, "steps")))
        {
            CombatSmokeSteps.Add(new CombatSmokeStepUiItem
            {
                StepName = Str(step, "stepName"),
                Success = Bool(step, "success"),
                Message = Str(step, "message"),
                Errors = JoinList(Get(step, "errors")),
                Warnings = JoinList(Get(step, "warnings"))
            });
        }

        AddIssues(CombatSmokeIssues, Get(payload, "errors"), "error");
        AddIssues(CombatSmokeIssues, Get(payload, "warnings"), "warning");
        CombatSmokeSummary = $"Успех: {Bool(payload, "success")}; шагов: {CombatSmokeSteps.Count}; бой: {Str(payload, "createdEncounterId", "—")}";
        StatusMessage = CombatSmokeSummary;
        RefreshOverviewCards();
    }

    private void RunDefinitionDryRunCore()
    {
        var response = _api.DefinitionsPackDryRun(DefinitionPackPath);
        DefinitionDryRunFiles.Clear();
        DefinitionDryRunIssues.Clear();
        if (!IsOk(response))
        {
            DefinitionDryRunSummary = Friendly(response, "Проверка пакета справочников недоступна.");
            StatusMessage = DefinitionDryRunSummary;
            return;
        }

        var payload = response.Payload;
        foreach (var file in AsDictionaries(Get(payload, "files")))
        {
            DefinitionDryRunFiles.Add(new DefinitionFileValidationUiItem
            {
                Category = Str(file, "category"),
                Path = Str(file, "path"),
                DefinitionCount = Int(file, "definitionCount"),
                Errors = AsList(Get(file, "errors")).Count,
                Warnings = AsList(Get(file, "warnings")).Count
            });
        }

        AddIssues(DefinitionDryRunIssues, Get(payload, "errors"), "error");
        AddIssues(DefinitionDryRunIssues, Get(payload, "warnings"), "warning");
        AddIssues(DefinitionDryRunIssues, Get(payload, "crossReferenceErrors"), "cross-ref error");
        AddIssues(DefinitionDryRunIssues, Get(payload, "crossReferenceWarnings"), "cross-ref warning");
        DefinitionDryRunSummary = $"Записей: {Int(payload, "loadedDefinitions")}; файлов: {DefinitionDryRunFiles.Count}; замечаний: {DefinitionDryRunIssues.Count}";
        StatusMessage = DefinitionDryRunSummary;
        RefreshOverviewCards();
    }

    private void RunInventoryDiagnosticsCore()
    {
        InventoryDiagnosticsIssues.Clear();
        InventoryDiagnosticsSections.Clear();
        if (string.IsNullOrWhiteSpace(InventoryCharacterId))
        {
            InventoryDiagnosticsSummary = "ID персонажа обязателен для диагностики инвентаря.";
            StatusMessage = InventoryDiagnosticsSummary;
            return;
        }

        var response = _api.InventoryDiagnosticsFull(new Dictionary<string, object>
        {
            { "characterId", InventoryCharacterId },
            { "ruleSetId", InventoryRuleSetId },
            { "campaignId", InventoryCampaignId }
        });

        if (!IsOk(response))
        {
            InventoryDiagnosticsSummary = Friendly(response, "Диагностика инвентаря недоступна.");
            StatusMessage = InventoryDiagnosticsSummary;
            return;
        }

        var payload = response.Payload;
        foreach (var section in AsDictionaries(Get(payload, "sections")))
        {
            InventoryDiagnosticsSections.Add(new SystemSectionUiItem
            {
                Name = Str(section, "section"),
                IsValid = Bool(section, "isValid"),
                ErrorCount = AsList(Get(section, "errors")).Count,
                WarningCount = AsList(Get(section, "warnings")).Count
            });
        }

        AddIssues(InventoryDiagnosticsIssues, Get(payload, "errors"), "error");
        AddIssues(InventoryDiagnosticsIssues, Get(payload, "warnings"), "warning");
        var summary = AsDictionary(Get(payload, "summary")) ?? new Dictionary<string, object>();
        InventoryDiagnosticsSummary = $"Корректно: {Bool(payload, "isValid")}; предметов: {Int(summary, "itemCount")}; ошибок: {Int(summary, "errorCount")}; предупреждений: {Int(summary, "warningCount")}";
        StatusMessage = InventoryDiagnosticsSummary;
        RefreshOverviewCards();
    }

    private void RefreshServerStatusCore()
    {
        ServerMetrics.Clear();
        var response = _api.AdminServerStatus();
        if (!IsOk(response))
        {
            ServerStatusSummary = Friendly(response, "Состояние сервера недоступно.");
            StatusMessage = ServerStatusSummary;
            return;
        }

        foreach (var pair in response.Payload.OrderBy(pair => pair.Key))
        {
            ServerMetrics.Add(new SystemMetricUiItem { Name = pair.Key, Value = Safe(pair.Value), State = "server" });
        }

        ServerStatusSummary = $"Состояние сервера загружено: метрик {ServerMetrics.Count}.";
        StatusMessage = ServerStatusSummary;
    }

    private void RefreshLocalClientStatus()
    {
        ClientStatusSummary = $"Клиент подключается к: {Nri.AdminClient.App.ClientConfig.ServerHost}:{Nri.AdminClient.App.ClientConfig.ServerPort}; журнал: {ClientLogService.Instance.LogFilePath}";
    }

    private void RefreshLogsCore()
    {
        ClientLogLines.Clear();
        var path = ClientLogService.Instance.LogFilePath;
        if (!File.Exists(path))
        {
            LogsStatus = "Файл журнала клиента не найден.";
            return;
        }

        List<string> lines;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream))
        {
            lines = ReadAllLines(reader).TakeLastSafe(250).ToList();
        }
        foreach (var line in lines)
        {
            ClientLogLines.Add(LogLineUiItem.From(line));
        }

        LogsStatus = $"Журнал клиента загружен: {ClientLogLines.Count}. Просмотр журнала сервера будет добавлен позже.";
        StatusMessage = LogsStatus;
    }

    private void RefreshBackupsCore()
    {
        var selectedId = SelectedBackupId;
        SelectedBackup = null;
        Backups.Clear();
        var response = _api.BackupList();
        if (!IsOk(response))
        {
            BackupStatus = Friendly(response, "Список резервных копий недоступен.");
            StatusMessage = BackupStatus;
            return;
        }

        foreach (var map in AsDictionaries(Get(response.Payload, "items")))
        {
            var item = new BackupListUiItem
            {
                BackupId = Str(map, "backupId"),
                Label = FirstNonEmpty(Str(map, "displayName"), Str(map, "label")),
                Status = Str(map, "status"),
                Scope = Str(map, "scope"),
                CreatedUtc = FirstNonEmpty(Str(map, "createdAtUtc"), Str(map, "createdUtc")),
                CompletedAtUtc = Str(map, "completedAtUtc"),
                CreatedByUserId = Str(map, "createdByUserId"),
                CreatedByDisplayName = Str(map, "createdByDisplayName"),
                VerificationStatus = Str(map, "verificationStatus"),
                VerificationMessage = Str(map, "verificationMessage"),
                IsVerified = Bool(map, "isVerified"),
                CollectionCount = Int(map, "collectionCount"),
                DocumentCount = Long(map, "documentCount"),
                SizeBytes = Long(map, "sizeBytes"),
                IsPreRestoreSafetyBackup = Bool(map, "isPreRestoreSafetyBackup")
            };
            Backups.Add(item);
            if (!string.IsNullOrWhiteSpace(selectedId) && string.Equals(item.BackupId, selectedId, StringComparison.OrdinalIgnoreCase))
                SelectedBackup = item;
        }

        if (SelectedBackup == null && Backups.Count > 0) SelectedBackup = Backups[0];
        BackupStatus = $"Резервных копий загружено: {Backups.Count}. Восстановление доступно только через предпросмотр, режим обслуживания и подтверждение.";
        StatusMessage = BackupStatus;
        RefreshOverviewCards();
    }

    private void CreateBackupCore()
    {
        var label = FirstNonEmpty(BackupLabel, "manual-backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        var response = _api.BackupCreate(label, BackupDescription);
        if (!IsOk(response))
        {
            BackupStatus = Friendly(response, "Резервную копию создать не удалось.");
            StatusMessage = BackupStatus;
            return;
        }

        BackupStatus = "Резервная копия создана: " + FirstNonEmpty(Str(response.Payload, "backupId"), label);
        StatusMessage = BackupStatus;
        RefreshBackupsCore();
    }

    private void VerifyBackupCore()
    {
        if (string.IsNullOrWhiteSpace(SelectedBackupId))
        {
            BackupStatus = "Выберите резервную копию для проверки.";
            StatusMessage = BackupStatus;
            return;
        }

        var response = _api.BackupVerify(SelectedBackupId);
        if (!IsOk(response))
        {
            BackupStatus = Friendly(response, "Резервную копию проверить не удалось.");
            StatusMessage = BackupStatus;
            return;
        }

        BackupStatus = "Проверка резервной копии завершена.";
        StatusMessage = BackupStatus;
        RefreshBackupsCore();
    }

    private void PreviewRestoreCore()
    {
        if (string.IsNullOrWhiteSpace(SelectedBackupId))
        {
            RestorePreviewStatus = "Выберите резервную копию для предпросмотра.";
            return;
        }

        var response = _api.BackupRestorePreview(SelectedBackupId, RestoreReason);
        RestorePreviewStatus = RestoreSummary(response);
        StatusMessage = RestorePreviewStatus;
    }

    private void ExecuteRestoreCore()
    {
        if (string.IsNullOrWhiteSpace(SelectedBackupId))
        {
            RestoreOperationStatus = "Выберите резервную копию для восстановления.";
            return;
        }

        var response = _api.BackupRestoreExecute(SelectedBackupId, RestoreReason, RestoreConfirmation);
        RestoreOperationStatus = RestoreSummary(response);
        StatusMessage = RestoreOperationStatus;
        RefreshBackupsCore();
        RefreshBackupMaintenanceCore();
    }

    private void RefreshBackupMaintenanceCore()
    {
        var response = _api.BackupMaintenanceGet();
        if (!IsOk(response))
        {
            BackupStatus = Friendly(response, "Режим обслуживания недоступен.");
            return;
        }

        var maintenance = AsDictionary(Get(response.Payload, "maintenance")) ?? new Dictionary<string, object>();
        BackupMaintenanceEnabled = Bool(maintenance, "isEnabled");
        BackupMaintenanceReason = Str(maintenance, "reason");
    }

    private void SetBackupMaintenanceCore()
    {
        var response = _api.BackupMaintenanceSet(BackupMaintenanceEnabled, BackupMaintenanceReason);
        if (!IsOk(response))
        {
            BackupStatus = Friendly(response, "Режим обслуживания обновить не удалось.");
            StatusMessage = BackupStatus;
            return;
        }

        BackupStatus = BackupMaintenanceEnabled ? "Режим обслуживания включён." : "Режим обслуживания выключен.";
        StatusMessage = BackupStatus;
        RefreshBackupMaintenanceCore();
    }

    private static string RestoreSummary(ResponseEnvelope response)
    {
        var payload = response.Payload ?? new Dictionary<string, object>();
        var blockers = JoinList(Get(payload, "blockers"));
        var warnings = JoinList(Get(payload, "warnings"));
        var operationId = Str(payload, "operationId");
        var status = Str(payload, "status");
        var message = string.IsNullOrWhiteSpace(response.Message) ? "Ответ восстановления получен." : response.Message;
        var summary = AsDictionary(Get(payload, "summary")) ?? new Dictionary<string, object>();
        var collections = JoinList(Get(summary, "collectionNames"));
        var safetyBackupId = Str(payload, "safetyBackupId");
        return $"{message} операция={FirstNonEmpty(operationId, "—")}; статус={FirstNonEmpty(status, response.Status.ToString())}; страховочная копия={FirstNonEmpty(safetyBackupId, "—")}; коллекции={collections}; блокеры={blockers}; предупреждения={warnings}";
    }

    private void RefreshOverviewCards()
    {
        OverviewCards.Clear();
        OverviewCards.Add(new SystemMetricUiItem { Name = "Сервер", Value = ServerStatusSummary, State = "только чтение" });
        OverviewCards.Add(new SystemMetricUiItem { Name = "Функции и модули", Value = $"Флагов: {FeatureFlags.Count}", State = "живые переопределения" });
        OverviewCards.Add(new SystemMetricUiItem { Name = "Проверка боя", Value = CombatSmokeSummary, State = CombatSmokeRunWrite ? "явная write-проверка" : "только валидация" });
        OverviewCards.Add(new SystemMetricUiItem { Name = "Справочники", Value = DefinitionDryRunSummary, State = "проверка без записи" });
        OverviewCards.Add(new SystemMetricUiItem { Name = "Инвентарь", Value = InventoryDiagnosticsSummary, State = "только чтение" });
        OverviewCards.Add(new SystemMetricUiItem { Name = "Резервные копии", Value = BackupStatus, State = BackupMaintenanceEnabled ? "режим обслуживания" : "защищено" });
    }

    private static void AddIssues(ObservableCollection<SystemIssueUiItem> target, object? source, string severity)
    {
        foreach (var item in AsList(source))
        {
            if (item is IDictionary<string, object> map)
            {
                target.Add(new SystemIssueUiItem
                {
                    Severity = FirstNonEmpty(Str(map, "severity"), severity),
                    Code = Str(map, "code"),
                    Message = FirstNonEmpty(Str(map, "message"), Safe(item))
                });
            }
            else
            {
                target.Add(new SystemIssueUiItem { Severity = severity, Message = Safe(item) });
            }
        }
    }

    private static bool IsOk(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;
    private static string Friendly(ResponseEnvelope response, string fallback)
    {
        if (response.Status == ResponseStatus.Forbidden || response.ErrorCode == ErrorCode.Forbidden)
            return (response.Message ?? string.Empty).IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0 ? "Команда выключена флагами функций." : "Недостаточно прав администратора.";
        if (response.Status == ResponseStatus.NotFound || response.ErrorCode == ErrorCode.NotFound) return "Данные не найдены.";
        if (!string.IsNullOrWhiteSpace(response.Message)) return response.Message;
        return fallback;
    }

    private static string CategoryForFlag(string name)
    {
        if (Contains(name, "Combat")) return "Игровые модули";
        if (Contains(name, "Inventory")) return "Основные функции";
        if (Contains(name, "Economy")) return "Игровые модули";
        if (Contains(name, "Map")) return "Игровые модули";
        if (Contains(name, "Definition")) return "Основные функции";
        if (Contains(name, "Profile") || Contains(name, "Character")) return "Основные функции";
        if (Contains(name, "Debug") || Contains(name, "Diagnostics")) return "Диагностика";
        if (Contains(name, "Experimental")) return "Экспериментальные";
        return "Системные";
    }

    private static bool Contains(string source, string token) => (source ?? string.Empty).IndexOf(token ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
    private static object? Get(IDictionary<string, object> source, string key)
    {
        foreach (var pair in source)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        }

        return null;
    }

    private static string Str(IDictionary<string, object> source, string key, string fallback = "") => Convert.ToString(Get(source, key), CultureInfo.InvariantCulture) ?? fallback;
    private static int Int(IDictionary<string, object> source, string key)
    {
        var value = Get(source, key);
        if (value is int i) return i;
        if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return 0;
    }

    private static long Long(IDictionary<string, object> source, string key)
    {
        var value = Get(source, key);
        if (value is long l) return l;
        if (value is int i) return i;
        if (long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return 0;
    }

    private static bool Bool(IDictionary<string, object> source, string key)
    {
        var value = Get(source, key);
        if (value is bool b) return b;
        return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) && parsed;
    }

    private static List<object> AsList(object? value)
    {
        if (value == null) return new List<object>();
        if (value is object[] array) return array.ToList();
        if (value is Array arr) return arr.Cast<object>().ToList();
        if (value is IList list) return list.Cast<object>().ToList();
        if (value is IEnumerable enumerable && value is not string) return enumerable.Cast<object>().ToList();
        return new List<object>();
    }

    private static Dictionary<string, object>? AsDictionary(object? value)
    {
        if (value is Dictionary<string, object> dictionary) return dictionary;
        if (value is IDictionary<string, object> generic) return new Dictionary<string, object>(generic);
        if (value is IDictionary legacy)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in legacy)
            {
                var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(key)) map[key] = entry.Value ?? string.Empty;
            }

            return map;
        }

        if (value is object[] objectArray && TryConvertPairEnumerable(objectArray, out var objectArrayMap))
            return objectArrayMap;

        if (value is IEnumerable enumerable && value is not string && TryConvertPairEnumerable(enumerable.Cast<object>(), out var enumerableMap))
            return enumerableMap;

        var propertyMap = TryConvertPublicProperties(value);
        if (propertyMap != null)
            return propertyMap;

        return null;
    }

    private static Dictionary<string, object>? TryConvertPublicProperties(object? value)
    {
        if (value == null) return null;
        var type = value.GetType();
        if (type.IsPrimitive || value is string || value is DateTime || value is decimal)
            return null;

        var properties = type.GetProperties()
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .ToList();
        if (properties.Count == 0)
            return null;

        var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties)
        {
            map[property.Name] = property.GetValue(value, null) ?? string.Empty;
        }

        return map.Count == 0 ? null : map;
    }

    private static bool TryConvertPairEnumerable(IEnumerable<object> items, out Dictionary<string, object> map)
    {
        map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var any = false;
        foreach (var item in items)
        {
            if (!TryReadPair(item, out var key, out var value))
                return false;
            if (string.IsNullOrWhiteSpace(key))
                return false;
            map[key] = value ?? string.Empty;
            any = true;
        }

        return any;
    }

    private static bool TryReadPair(object? item, out string key, out object? value)
    {
        key = string.Empty;
        value = null;
        if (item == null) return false;
        if (item is DictionaryEntry dictionaryEntry)
        {
            key = Convert.ToString(dictionaryEntry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
            value = dictionaryEntry.Value;
            return true;
        }

        if (item is IDictionary dictionary)
        {
            object? keyObject = null;
            object? valueObject = null;
            foreach (DictionaryEntry entry in dictionary)
            {
                var name = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                if (string.Equals(name, "key", StringComparison.OrdinalIgnoreCase)) keyObject = entry.Value;
                if (string.Equals(name, "value", StringComparison.OrdinalIgnoreCase)) valueObject = entry.Value;
            }

            if (keyObject != null)
            {
                key = Convert.ToString(keyObject, CultureInfo.InvariantCulture) ?? string.Empty;
                value = valueObject;
                return true;
            }
        }

        var itemType = item.GetType();
        var keyProperty = itemType.GetProperty("Key") ?? itemType.GetProperty("key");
        var valueProperty = itemType.GetProperty("Value") ?? itemType.GetProperty("value");
        if (keyProperty != null && valueProperty != null)
        {
            key = Convert.ToString(keyProperty.GetValue(item, null), CultureInfo.InvariantCulture) ?? string.Empty;
            value = valueProperty.GetValue(item, null);
            return true;
        }

        if (item is IEnumerable enumerable && item is not string)
        {
            var parts = enumerable.Cast<object?>().ToArray();
            if (parts.Length == 2)
            {
                key = Convert.ToString(parts[0], CultureInfo.InvariantCulture) ?? string.Empty;
                value = parts[1];
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Dictionary<string, object>> AsDictionaries(object? value)
    {
        foreach (var item in AsList(value))
        {
            var map = AsDictionary(item);
            if (map != null) yield return map;
        }
    }

    private static string JoinList(object? value)
    {
        var items = AsList(value).Select(Safe).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        return items.Count == 0 ? "—" : string.Join(", ", items);
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    private static string Safe(object? value)
    {
        if (value == null) return "—";
        if (value is Array arr) return string.Join(", ", arr.Cast<object>().Select(Safe));
        if (value is IDictionary) return "[object]";
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "—";
        return text.Length <= 500 ? text : text.Substring(0, 500) + "…";
    }

    private static IEnumerable<string> ReadAllLines(TextReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
            yield return line;
    }
}

public sealed class FeatureFlagUiItem
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool DefaultValue { get; set; }
    public bool EffectiveValue { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool IsOverridden { get; set; }
    public bool HasOverride { get; set; }
    public string Description { get; set; } = string.Empty;
    public string UpdatedAtUtc { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string DisplayName => FeatureFlagDisplayNames.TitleFor(Name);
    public string CategoryDisplay => string.IsNullOrWhiteSpace(Category) ? "Системные" : Category;
    public string SourceDisplay => Source switch
    {
        "default" => "По умолчанию",
        "database" => "Переопределение",
        "database override" => "Переопределение",
        _ => string.IsNullOrWhiteSpace(Source) ? "По умолчанию" : Source
    };
    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? FeatureFlagDisplayNames.DescriptionFor(Name) : Description;
}

internal static class FeatureFlagDisplayNames
{
    public static string TitleFor(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "Без названия";
        var compact = key.Split('.').LastOrDefault() ?? key;
        return compact switch
        {
            "UseMapSystemV1" => "Система карт",
            "UseSpaceHierarchyV1" => "Иерархия пространства",
            "UseSceneMapV1" => "Карта сцены",
            "UseWorldMapV1" => "Карта мира",
            "UseRoomMapMvp" => "Помещения",
            "UseCharacterGroupsMvp" => "Группы персонажей",
            "UseGroupMembershipV1" => "Состав групп",
            "UseActiveGroupMvp" => "Активная группа",
            "UseFateEngineMvp" => "Fate Engine",
            _ => SplitCamelCase(compact)
        };
    }

    public static string DescriptionFor(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "Описание пока не задано.";
        if (key.IndexOf("Map", StringComparison.OrdinalIgnoreCase) >= 0) return "Функции карт. В 0.14 показываются только как controlled disabled, если модуль не принят.";
        if (key.IndexOf("Combat", StringComparison.OrdinalIgnoreCase) >= 0) return "Функции боя и связанных проверок.";
        if (key.IndexOf("Character", StringComparison.OrdinalIgnoreCase) >= 0) return "Функции персонажей и Character v2.";
        if (key.IndexOf("Backup", StringComparison.OrdinalIgnoreCase) >= 0) return "Функции резервного копирования и восстановления.";
        return "Системная функция или защитный режим.";
    }

    private static string SplitCamelCase(string value)
    {
        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                chars.Add(' ');
            chars.Add(c);
        }

        return new string(chars.ToArray());
    }
}

public sealed class CombatSmokeStepUiItem
{
    public string StepName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Errors { get; set; } = string.Empty;
    public string Warnings { get; set; } = string.Empty;
}

public sealed class SystemIssueUiItem
{
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class SystemSectionUiItem
{
    public string Name { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}

public sealed class SystemMetricUiItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public sealed class LogLineUiItem
{
    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public static LogLineUiItem From(string line)
    {
        var item = new LogLineUiItem { Message = line };
        if (line.Length >= 23)
        {
            item.Timestamp = line.Substring(0, 23);
        }

        var start = line.IndexOf('[', 24);
        var end = start >= 0 ? line.IndexOf(']', start + 1) : -1;
        if (start >= 0 && end > start)
        {
            item.Level = line.Substring(start + 1, end - start - 1);
            item.Message = line.Substring(Math.Min(line.Length, end + 1)).Trim();
        }

        return item;
    }
}

public sealed class BackupListUiItem
{
    public string BackupId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CompletedAtUtc { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public string VerificationMessage { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public int CollectionCount { get; set; }
    public long DocumentCount { get; set; }
    public long SizeBytes { get; set; }
    public bool IsPreRestoreSafetyBackup { get; set; }
    public string CreatedBy => string.IsNullOrWhiteSpace(CreatedByDisplayName) ? CreatedByUserId : CreatedByDisplayName;
    public string SizeLabel => SizeBytes <= 0 ? "—" : $"{Math.Round(SizeBytes / 1024d / 1024d, 2)} MB";
    public string VerificationLabel => IsVerified ? "Проверен" : (string.IsNullOrWhiteSpace(VerificationStatus) ? "Не проверен" : VerificationStatus);
}

internal static class SystemToolsEnumerableExtensions
{
    public static IEnumerable<T> TakeLastSafe<T>(this IEnumerable<T> source, int count)
    {
        var queue = new Queue<T>();
        foreach (var item in source)
        {
            queue.Enqueue(item);
            while (queue.Count > count) queue.Dequeue();
        }

        return queue.ToArray();
    }
}
