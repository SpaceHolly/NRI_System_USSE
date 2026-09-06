using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.AssetConfigurators.Wpf.Models;
using Nri.AssetConfigurators.Wpf.Services;
using Nri.AssetConfigurators.Wpf.ViewModels;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerAssetConfiguratorsViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterId;
    private AssetBlueprintPresentation? _selectedBlueprint;
    private AssetBlueprintPresentation? _openedBlueprint;
    private string _statusMessage = "Выберите конструктор и настройте проект.";
    private string _errorMessage = string.Empty;
    private string _selectedStatus = "draft";
    private string _selectedVisibility = "private";
    private bool _isDirty;
    private bool _suppressDirty;
    private string _pendingCreateOperationId = Guid.NewGuid().ToString("N");

    public PlayerAssetConfiguratorsViewModel(CommandApi api, Func<string> activeCharacterId)
    {
        _api = api;
        _activeCharacterId = activeCharacterId;
        Workspace = new AssetConfiguratorWorkspaceViewModel(showGmFields: false);
        Blueprints = new ObservableCollection<AssetBlueprintPresentation>();
        StatusOptions = new[]
        {
            new AssetBlueprintOption("draft", "Черновик"),
            new AssetBlueprintOption("ready", "Готов")
        };
        VisibilityOptions = new[]
        {
            new AssetBlueprintOption("private", "Только мне"),
            new AssetBlueprintOption("shared", "Доступен GM")
        };

        RefreshCommand = new RelayCommand(Refresh);
        SaveCommand = new RelayCommand(Save);
        OpenCommand = new RelayCommand(OpenSelected);
        DuplicateCommand = new RelayCommand(DuplicateSelected);
        ArchiveCommand = new RelayCommand(ArchiveSelected);
        CopySummaryCommand = new RelayCommand(CopySelectedSummary);
        NewCommand = new RelayCommand(StartNew);

        SubscribeDirtyTracking();
    }

    public AssetConfiguratorWorkspaceViewModel Workspace { get; }
    public ObservableCollection<AssetBlueprintPresentation> Blueprints { get; }
    public IReadOnlyList<AssetBlueprintOption> StatusOptions { get; }
    public IReadOnlyList<AssetBlueprintOption> VisibilityOptions { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand CopySummaryCommand { get; }
    public ICommand NewCommand { get; }

    public AssetBlueprintPresentation? SelectedBlueprint
    {
        get => _selectedBlueprint;
        set
        {
            if (ReferenceEquals(_selectedBlueprint, value))
                return;
            _selectedBlueprint = value;
            Notify();
            Notify(nameof(HasSelectedBlueprint));
        }
    }

    public bool HasSelectedBlueprint => SelectedBlueprint != null;
    public bool HasOpenedBlueprint => _openedBlueprint != null;

    public AssetBlueprintOption SelectedStatusOption
    {
        get => StatusOptions.First(item => item.Id == _selectedStatus);
        set
        {
            if (value == null || _selectedStatus == value.Id)
                return;
            _selectedStatus = value.Id;
            IsDirty = true;
            Notify();
        }
    }

    public AssetBlueprintOption SelectedVisibilityOption
    {
        get => VisibilityOptions.First(item => item.Id == _selectedVisibility);
        set
        {
            if (value == null || _selectedVisibility == value.Id)
                return;
            _selectedVisibility = value.Id;
            IsDirty = true;
            Notify();
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value)
                return;
            _isDirty = value;
            Notify();
            Notify(nameof(UnsavedStateText));
        }
    }

    public string UnsavedStateText => IsDirty ? "Есть несохранённые изменения" : "Все изменения сохранены";

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            Notify();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            Notify();
        }
    }

    public void Refresh()
    {
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.AssetBlueprintPlayerList();
            if (!EnsureSuccess(response, "Не удалось загрузить ваши чертежи."))
                return;
            Blueprints.Clear();
            foreach (var item in AssetBlueprintPresentationParser.ParseItems(response.Payload))
                Blueprints.Add(item);
            SelectedBlueprint = Blueprints.FirstOrDefault(item =>
                _openedBlueprint != null && item.BlueprintId == _openedBlueprint.BlueprintId)
                ?? Blueprints.FirstOrDefault();
            StatusMessage = Blueprints.Count == 0
                ? "Сохранённых чертежей пока нет."
                : $"Ваших чертежей: {Blueprints.Count}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Сервер недоступен: " + ex.Message;
        }
    }

    private void Save()
    {
        ErrorMessage = string.Empty;
        try
        {
            var configuration = AssetConfiguratorPayloadCodec.ToPayload(Workspace.BuildActiveInput());
            var payload = new Dictionary<string, object>
            {
                ["name"] = ConfigurationName(configuration),
                ["configuratorKind"] = Workspace.ActiveConfiguratorKind,
                ["configuration"] = configuration,
                ["ownerCharacterId"] = _activeCharacterId() ?? string.Empty,
                ["status"] = _selectedStatus,
                ["visibility"] = _selectedVisibility
            };

            ResponseEnvelope response;
            if (_openedBlueprint == null)
            {
                payload["operationId"] = _pendingCreateOperationId;
                response = _api.AssetBlueprintPlayerCreate(payload);
            }
            else
            {
                payload["blueprintId"] = _openedBlueprint.BlueprintId;
                payload["expectedRevision"] = _openedBlueprint.Revision;
                response = _api.AssetBlueprintPlayerUpdate(payload);
            }

            if (!EnsureSuccess(response, "Не удалось сохранить чертёж."))
                return;
            var saved = AssetBlueprintPresentationParser.ParseSingle(response.Payload);
            if (saved == null)
            {
                ErrorMessage = "Сервер не вернул сохранённый чертёж.";
                return;
            }
            _openedBlueprint = saved;
            _pendingCreateOperationId = Guid.NewGuid().ToString("N");
            IsDirty = false;
            StatusMessage = $"Чертёж «{saved.Name}» сохранён. Серверная проверка: {saved.ValidationText}.";
            Refresh();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось сохранить чертёж: " + ex.Message;
        }
    }

    private void OpenSelected()
    {
        if (SelectedBlueprint == null)
        {
            ErrorMessage = "Сначала выберите чертёж.";
            return;
        }

        ErrorMessage = string.Empty;
        try
        {
            var response = _api.AssetBlueprintPlayerGet(new Dictionary<string, object>
            {
                ["blueprintId"] = SelectedBlueprint.BlueprintId
            });
            if (!EnsureSuccess(response, "Не удалось открыть чертёж."))
                return;
            var item = AssetBlueprintPresentationParser.ParseSingle(response.Payload);
            if (item == null)
            {
                ErrorMessage = "Сервер вернул пустой чертёж.";
                return;
            }

            _suppressDirty = true;
            Workspace.ApplyInput(
                item.ConfiguratorKind,
                AssetConfiguratorPayloadCodec.FromPayload(item.ConfiguratorKind, item.Configuration));
            _openedBlueprint = item;
            _selectedStatus = item.Status == "ready" ? "ready" : "draft";
            _selectedVisibility = item.Visibility == "shared" ? "shared" : "private";
            Notify(nameof(SelectedStatusOption));
            Notify(nameof(SelectedVisibilityOption));
            Notify(nameof(HasOpenedBlueprint));
            IsDirty = false;
            StatusMessage = $"Открыт чертёж «{item.Name}», редакция {item.Revision}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось открыть чертёж: " + ex.Message;
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    private void DuplicateSelected()
    {
        if (SelectedBlueprint == null)
        {
            ErrorMessage = "Сначала выберите чертёж.";
            return;
        }
        var response = _api.AssetBlueprintPlayerDuplicate(new Dictionary<string, object>
        {
            ["blueprintId"] = SelectedBlueprint.BlueprintId,
            ["name"] = SelectedBlueprint.Name + " — копия",
            ["operationId"] = Guid.NewGuid().ToString("N")
        });
        if (!EnsureSuccess(response, "Не удалось создать копию."))
            return;
        StatusMessage = "Копия чертежа создана.";
        Refresh();
    }

    private void ArchiveSelected()
    {
        if (SelectedBlueprint == null)
        {
            ErrorMessage = "Сначала выберите чертёж.";
            return;
        }
        if (MessageBox.Show(
                "Перенести выбранный чертёж в архив?",
                "Архивирование чертежа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var response = _api.AssetBlueprintPlayerArchive(new Dictionary<string, object>
        {
            ["blueprintId"] = SelectedBlueprint.BlueprintId,
            ["expectedRevision"] = SelectedBlueprint.Revision
        });
        if (!EnsureSuccess(response, "Не удалось архивировать чертёж."))
            return;
        if (_openedBlueprint?.BlueprintId == SelectedBlueprint.BlueprintId)
            _openedBlueprint = null;
        StatusMessage = "Чертёж перенесён в архив.";
        Refresh();
    }

    private void StartNew()
    {
        _openedBlueprint = null;
        _pendingCreateOperationId = Guid.NewGuid().ToString("N");
        _selectedStatus = "draft";
        _selectedVisibility = "private";
        Notify(nameof(SelectedStatusOption));
        Notify(nameof(SelectedVisibilityOption));
        Notify(nameof(HasOpenedBlueprint));
        IsDirty = true;
        StatusMessage = "Создаётся новый чертёж на основе текущей конфигурации.";
    }

    private void CopySelectedSummary()
    {
        var item = SelectedBlueprint ?? _openedBlueprint;
        if (item == null)
        {
            ErrorMessage = "Сначала выберите чертёж.";
            return;
        }
        Clipboard.SetText(
            $"{item.Name}{Environment.NewLine}" +
            $"{item.ConfiguratorKindLabel} · {item.ValidationText}{Environment.NewLine}" +
            $"{item.ReadableSummary}{Environment.NewLine}" +
            $"Стоимость: {item.CostText}{Environment.NewLine}" +
            $"Энергия: {item.EnergyText}");
        StatusMessage = "Читаемая сводка скопирована.";
    }

    private bool EnsureSuccess(ResponseEnvelope response, string fallback)
    {
        if (response.Status == ResponseStatus.Ok)
            return true;
        ErrorMessage = string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
        return false;
    }

    private void SubscribeDirtyTracking()
    {
        Workspace.PropertyChanged += MarkDirty;
        Workspace.Spacecraft.PropertyChanged += MarkDirty;
        Workspace.LandMarine.PropertyChanged += MarkDirty;
        Workspace.Building.PropertyChanged += MarkDirty;
        Subscribe(Workspace.Spacecraft.SelectedComponents);
        Subscribe(Workspace.Spacecraft.Engines);
        Subscribe(Workspace.Spacecraft.SelectedSensors);
        Subscribe(Workspace.Spacecraft.SelectedAuxiliaryModules);
        Subscribe(Workspace.LandMarine.SelectedComponents);
        Subscribe(Workspace.LandMarine.SelectedSensors);
        Subscribe(Workspace.LandMarine.SelectedAuxiliaryModules);
        Subscribe(Workspace.Building.SelectedComponents);
    }

    private void Subscribe(INotifyCollectionChanged collection) =>
        collection.CollectionChanged += (_, __) => MarkDirty(null, null);

    private void MarkDirty(object? sender, PropertyChangedEventArgs? args)
    {
        if (!_suppressDirty)
            IsDirty = true;
    }

    private static string ConfigurationName(IDictionary<string, object> configuration) =>
        configuration.TryGetValue("configurationName", out var raw)
            ? Convert.ToString(raw) ?? "Новый чертёж"
            : "Новый чертёж";
}

public sealed class AssetBlueprintOption
{
    public AssetBlueprintOption(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }
    public override string ToString() => Label;
}
