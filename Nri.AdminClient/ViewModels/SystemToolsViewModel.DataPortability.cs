using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed partial class SystemToolsViewModel
{
    private string _devAccessStatusText = "Dev Access не загружен.";
    private string _devAccessCredentialsText = string.Empty;
    private string _exportDefinitionsPackageName = "EXPORT_DEFINITIONS_01459";
    private string _exportDefinitionsStatusText = "Экспорт definitions ещё не запускался.";
    private string _exportDefinitionsManifestPreview = string.Empty;
    private string _importDefinitionsPackagePath = string.Empty;
    private string _importDefinitionsMode = "merge";
    private string _importDefinitionsConfirmation = string.Empty;
    private string _importDefinitionsPlannedChanges = string.Empty;
    private string _importDefinitionsStatusText = "Импорт definitions ещё не запускался.";
    private string _exportCampaignPackageName = "EXPORT_CAMPAIGN_01459";
    private bool _exportCampaignIncludeSensitive;
    private string _exportCampaignStatusText = "Экспорт campaign data ещё не запускался.";
    private string _importCampaignPackagePath = string.Empty;
    private string _importCampaignMode = "merge";
    private string _importCampaignConfirmation = string.Empty;
    private string _importCampaignSafetyBackupId = string.Empty;
    private string _importCampaignStatusText = "Импорт campaign data ещё не запускался.";
    private object? _selectedExportRecord;
    private object? _selectedImportRecord;
    private string _selectedImportExportDetails = "Выберите запись истории.";

    public ObservableCollection<DataPortabilityAccountUiItem> DevAccessAccounts { get; } = new ObservableCollection<DataPortabilityAccountUiItem>();
    public ObservableCollection<DataPortabilityHistoryUiItem> ExportRecords { get; } = new ObservableCollection<DataPortabilityHistoryUiItem>();
    public ObservableCollection<DataPortabilityHistoryUiItem> ImportRecords { get; } = new ObservableCollection<DataPortabilityHistoryUiItem>();

    public ICommand LoadDevAccessCommand { get; private set; } = null!;
    public ICommand ResetKnownAccountsCommand { get; private set; } = null!;
    public ICommand PrintKnownCredentialsCommand { get; private set; } = null!;
    public ICommand VerifyKnownLoginCommand { get; private set; } = null!;
    public ICommand ExportDefinitionsCommand { get; private set; } = null!;
    public ICommand ValidateDefinitionsPackageCommand { get; private set; } = null!;
    public ICommand DryRunDefinitionsImportCommand { get; private set; } = null!;
    public ICommand ImportDefinitionsCommand { get; private set; } = null!;
    public ICommand ExportCampaignDataCommand { get; private set; } = null!;
    public ICommand ValidateCampaignPackageCommand { get; private set; } = null!;
    public ICommand DryRunCampaignImportCommand { get; private set; } = null!;
    public ICommand ImportCampaignDataCommand { get; private set; } = null!;
    public ICommand RefreshImportExportHistoryCommand { get; private set; } = null!;

    public string DevAccessStatusText { get => _devAccessStatusText; private set { if (_devAccessStatusText != value) { _devAccessStatusText = value; Notify(); } } }
    public string DevAccessCredentialsText { get => _devAccessCredentialsText; private set { if (_devAccessCredentialsText != value) { _devAccessCredentialsText = value; Notify(); } } }
    public string ExportDefinitionsPackageName { get => _exportDefinitionsPackageName; set { if (_exportDefinitionsPackageName != value) { _exportDefinitionsPackageName = value; Notify(); } } }
    public string ExportDefinitionsStatusText { get => _exportDefinitionsStatusText; private set { if (_exportDefinitionsStatusText != value) { _exportDefinitionsStatusText = value; Notify(); } } }
    public string ExportDefinitionsManifestPreview { get => _exportDefinitionsManifestPreview; private set { if (_exportDefinitionsManifestPreview != value) { _exportDefinitionsManifestPreview = value; Notify(); } } }
    public string ImportDefinitionsPackagePath { get => _importDefinitionsPackagePath; set { if (_importDefinitionsPackagePath != value) { _importDefinitionsPackagePath = value; Notify(); } } }
    public string ImportDefinitionsMode { get => _importDefinitionsMode; set { if (_importDefinitionsMode != value) { _importDefinitionsMode = value; Notify(); } } }
    public string ImportDefinitionsConfirmation { get => _importDefinitionsConfirmation; set { if (_importDefinitionsConfirmation != value) { _importDefinitionsConfirmation = value; Notify(); } } }
    public string ImportDefinitionsPlannedChanges { get => _importDefinitionsPlannedChanges; private set { if (_importDefinitionsPlannedChanges != value) { _importDefinitionsPlannedChanges = value; Notify(); } } }
    public string ImportDefinitionsStatusText { get => _importDefinitionsStatusText; private set { if (_importDefinitionsStatusText != value) { _importDefinitionsStatusText = value; Notify(); } } }
    public string ExportCampaignPackageName { get => _exportCampaignPackageName; set { if (_exportCampaignPackageName != value) { _exportCampaignPackageName = value; Notify(); } } }
    public bool ExportCampaignIncludeSensitive { get => _exportCampaignIncludeSensitive; set { if (_exportCampaignIncludeSensitive != value) { _exportCampaignIncludeSensitive = value; Notify(); } } }
    public string ExportCampaignStatusText { get => _exportCampaignStatusText; private set { if (_exportCampaignStatusText != value) { _exportCampaignStatusText = value; Notify(); } } }
    public string ImportCampaignPackagePath { get => _importCampaignPackagePath; set { if (_importCampaignPackagePath != value) { _importCampaignPackagePath = value; Notify(); } } }
    public string ImportCampaignMode { get => _importCampaignMode; set { if (_importCampaignMode != value) { _importCampaignMode = value; Notify(); } } }
    public string ImportCampaignConfirmation { get => _importCampaignConfirmation; set { if (_importCampaignConfirmation != value) { _importCampaignConfirmation = value; Notify(); } } }
    public string ImportCampaignSafetyBackupId { get => _importCampaignSafetyBackupId; private set { if (_importCampaignSafetyBackupId != value) { _importCampaignSafetyBackupId = value; Notify(); } } }
    public string ImportCampaignStatusText { get => _importCampaignStatusText; private set { if (_importCampaignStatusText != value) { _importCampaignStatusText = value; Notify(); } } }
    public string SelectedImportExportDetails { get => _selectedImportExportDetails; private set { if (_selectedImportExportDetails != value) { _selectedImportExportDetails = value; Notify(); } } }

    public object? SelectedExportRecord
    {
        get => _selectedExportRecord;
        set
        {
            if (_selectedExportRecord != value)
            {
                _selectedExportRecord = value;
                Notify();
                SelectedImportExportDetails = value is DataPortabilityHistoryUiItem item ? item.Details : "Выберите запись истории.";
            }
        }
    }

    public object? SelectedImportRecord
    {
        get => _selectedImportRecord;
        set
        {
            if (_selectedImportRecord != value)
            {
                _selectedImportRecord = value;
                Notify();
                SelectedImportExportDetails = value is DataPortabilityHistoryUiItem item ? item.Details : "Выберите запись истории.";
            }
        }
    }

    private void InitializeDataPortabilityCommands()
    {
        LoadDevAccessCommand = new RelayCommand(() => RunSafe("system.ui.dev_access.status", LoadDevAccessCore), () => CanRun);
        ResetKnownAccountsCommand = new RelayCommand(() => RunSafe("system.ui.dev_access.reset", ResetKnownAccountsCore), () => CanRun);
        PrintKnownCredentialsCommand = new RelayCommand(() => RunSafe("system.ui.dev_access.print", PrintKnownCredentialsCore), () => CanRun);
        VerifyKnownLoginCommand = new RelayCommand(() => RunSafe("system.ui.dev_access.verify", VerifyKnownLoginCore), () => CanRun);
        ExportDefinitionsCommand = new RelayCommand(() => RunSafe("system.ui.data_portability.export_definitions", ExportDefinitionsCore), () => CanRun);
        ValidateDefinitionsPackageCommand = new RelayCommand(() => RunSafe("system.ui.data_portability.validate_definitions", ValidateDefinitionsPackageCore), () => CanRun);
        DryRunDefinitionsImportCommand = new RelayCommand(() => RunSafe("system.ui.data_portability.dryrun_definitions", DryRunDefinitionsImportCore), () => CanRun);
        ImportDefinitionsCommand = new RelayCommand(() => RunSafe("system.ui.data_portability.import_definitions", ImportDefinitionsCore), () => CanRun);
        ExportCampaignDataCommand = new RelayCommand(() => RunSafe("system.ui.data_portability.export_campaign", ExportCampaignDataCore), () => CanRun);
        ValidateCampaignPackageCommand = new RelayCommand(() => RunSafe("system.ui.data_portability.validate_campaign", ValidateCampaignPackageCore), () => CanRun);
        DryRunCampaignImportCommand = new RelayCommand(() => RunSafe("system.ui.data_portability.dryrun_campaign", DryRunCampaignImportCore), () => CanRun);
        ImportCampaignDataCommand = new RelayCommand(() => RunSafe("system.ui.data_portability.import_campaign", ImportCampaignDataCore), () => CanRun);
        RefreshImportExportHistoryCommand = new RelayCommand(() => RunSafe("system.ui.data_portability.history", RefreshImportExportHistoryCore), () => CanRun);
    }

    private void LoadDevAccessCore()
    {
        var response = _api.DevAccessStatus();
        EnsureOk(response);
        FillDevAccess(response.Payload);
        DevAccessStatusText = $"Dev Access loaded: {DevAccessAccounts.Count} accounts.";
    }

    private void ResetKnownAccountsCore()
    {
        var response = _api.DevAccessResetKnownAccounts();
        EnsureOk(response);
        FillDevAccess(response.Payload);
        DevAccessStatusText = "Known dev accounts reset. Passwords are hashed in MongoDB.";
    }

    private void PrintKnownCredentialsCore()
    {
        var response = _api.DevAccessPrintKnownCredentials();
        EnsureOk(response);
        var rows = AsDictionaries(Get(response.Payload, "accounts"))
            .Select(x => $"{Str(x, "login")} / {Str(x, "password")}")
            .ToArray();
        DevAccessCredentialsText = string.Join(Environment.NewLine, rows);
        DevAccessStatusText = "Known credentials printed for local dev only.";
    }

    private void VerifyKnownLoginCore()
    {
        var response = _api.DevAccessVerifyKnownLogin();
        EnsureOk(response);
        var count = AsDictionaries(Get(response.Payload, "items")).Count(x => Bool(x, "passwordValid"));
        DevAccessStatusText = $"Known login verification completed: {count} valid.";
    }

    private void ExportDefinitionsCore()
    {
        var response = _api.DataPortabilityExportDefinitions(new Dictionary<string, object> { ["packageName"] = ExportDefinitionsPackageName });
        EnsureOk(response);
        ExportDefinitionsStatusText = PackageStatus(response.Payload);
        ExportDefinitionsManifestPreview = ManifestPreview(response.Payload);
        ImportDefinitionsPackagePath = Str(response.Payload, "packagePath");
        RefreshImportExportHistoryCore();
    }

    private void ValidateDefinitionsPackageCore()
    {
        var response = _api.DataPortabilityValidatePackage(new Dictionary<string, object> { ["packagePath"] = ImportDefinitionsPackagePath });
        EnsureOk(response);
        ImportDefinitionsStatusText = ValidationStatus(response.Payload);
    }

    private void DryRunDefinitionsImportCore()
    {
        var response = _api.DataPortabilityImportDefinitionsDryRun(new Dictionary<string, object> { ["packagePath"] = ImportDefinitionsPackagePath, ["mode"] = ImportDefinitionsMode });
        EnsureOk(response);
        ImportDefinitionsPlannedChanges = PlanText(response.Payload);
        ImportDefinitionsStatusText = "Definitions dry-run completed.";
        RefreshImportExportHistoryCore();
    }

    private void ImportDefinitionsCore()
    {
        var response = _api.DataPortabilityImportDefinitions(new Dictionary<string, object> { ["packagePath"] = ImportDefinitionsPackagePath, ["mode"] = ImportDefinitionsMode, ["confirmation"] = ImportDefinitionsConfirmation });
        EnsureOk(response);
        ImportDefinitionsPlannedChanges = PlanText(response.Payload);
        ImportDefinitionsStatusText = "Definitions import completed.";
        RefreshImportExportHistoryCore();
    }

    private void ExportCampaignDataCore()
    {
        var response = _api.DataPortabilityExportCampaignData(new Dictionary<string, object> { ["packageName"] = ExportCampaignPackageName, ["includeSensitive"] = ExportCampaignIncludeSensitive });
        EnsureOk(response);
        ExportCampaignStatusText = PackageStatus(response.Payload);
        ImportCampaignPackagePath = Str(response.Payload, "packagePath");
        RefreshImportExportHistoryCore();
    }

    private void ValidateCampaignPackageCore()
    {
        var response = _api.DataPortabilityValidatePackage(new Dictionary<string, object> { ["packagePath"] = ImportCampaignPackagePath });
        EnsureOk(response);
        ImportCampaignStatusText = ValidationStatus(response.Payload);
    }

    private void DryRunCampaignImportCore()
    {
        var response = _api.DataPortabilityImportCampaignDataDryRun(new Dictionary<string, object> { ["packagePath"] = ImportCampaignPackagePath, ["mode"] = ImportCampaignMode });
        EnsureOk(response);
        ImportCampaignStatusText = "Campaign dry-run completed.";
        ImportCampaignSafetyBackupId = Str(response.Payload, "safetyBackupId");
        RefreshImportExportHistoryCore();
    }

    private void ImportCampaignDataCore()
    {
        var response = _api.DataPortabilityImportCampaignData(new Dictionary<string, object> { ["packagePath"] = ImportCampaignPackagePath, ["mode"] = ImportCampaignMode, ["confirmation"] = ImportCampaignConfirmation });
        EnsureOk(response);
        ImportCampaignStatusText = "Campaign import completed.";
        ImportCampaignSafetyBackupId = Str(response.Payload, "safetyBackupId");
        RefreshImportExportHistoryCore();
    }

    private void RefreshImportExportHistoryCore()
    {
        var exports = _api.DataPortabilityExportList();
        if (exports.Status == ResponseStatus.Ok)
            FillHistory(ExportRecords, Get(exports.Payload, "items"), "export");
        var imports = _api.DataPortabilityImportList();
        if (imports.Status == ResponseStatus.Ok)
            FillHistory(ImportRecords, Get(imports.Payload, "items"), "import");
    }

    private void FillDevAccess(IDictionary<string, object> payload)
    {
        DevAccessAccounts.Clear();
        foreach (var item in AsDictionaries(Get(payload, "accounts")))
        {
            DevAccessAccounts.Add(new DataPortabilityAccountUiItem
            {
                Login = Str(item, "login"),
                AccountId = Str(item, "accountId"),
                Status = Str(item, "status"),
                Roles = JoinList(Get(item, "roles")),
                Exists = Bool(item, "exists")
            });
        }
    }

    private void FillHistory(ObservableCollection<DataPortabilityHistoryUiItem> target, object? value, string kind)
    {
        target.Clear();
        foreach (var item in AsDictionaries(value))
        {
            target.Add(new DataPortabilityHistoryUiItem
            {
                Kind = kind,
                Id = First(Str(item, "exportId"), Str(item, "importId"), Str(item, "_id")),
                PackageName = Str(item, "packageName"),
                Type = First(Str(item, "exportType"), Str(item, "importType")),
                Status = Str(item, "status"),
                CreatedAt = First(Str(item, "createdAtUtc"), Str(item, "CreatedAtUtc")),
                PackagePath = Str(item, "packagePath"),
                Details = string.Join(Environment.NewLine, item.Select(pair => $"{pair.Key}: {Safe(pair.Value)}"))
            });
        }
    }

    private static void EnsureOk(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
            throw new InvalidOperationException($"{response.Status}: {response.Message}");
    }

    private static string PackageStatus(IDictionary<string, object> payload)
        => $"Package: {Str(payload, "packageName")} | {Str(payload, "packagePath")} | checksum {Str(payload, "checksumSha256")}";

    private static string ManifestPreview(IDictionary<string, object> payload)
        => $"Manifest: {Str(payload, "manifestPath")}{Environment.NewLine}Collections: {JoinList(Get(payload, "collections"))}";

    private static string ValidationStatus(IDictionary<string, object> payload)
        => $"Valid={Bool(payload, "isValid")} | Package={Str(payload, "packageName")} | Errors={JoinList(Get(payload, "errors"))}";

    private static string PlanText(IDictionary<string, object> payload)
        => $"Planned={Str(payload, "plannedCount")}{Environment.NewLine}{JoinList(Get(payload, "planned"))}{Environment.NewLine}Skipped: {JoinList(Get(payload, "skippedCollections"))}";

    private static string First(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

public sealed class DataPortabilityAccountUiItem
{
    public string Login { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
    public bool Exists { get; set; }
}

public sealed class DataPortabilityHistoryUiItem
{
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string PackagePath { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
