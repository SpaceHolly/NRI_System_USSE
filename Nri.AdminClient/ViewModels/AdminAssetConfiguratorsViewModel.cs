using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Nri.AdminClient.Networking;
using Nri.AssetConfigurators.Wpf.Models;
using Nri.AssetConfigurators.Wpf.Services;
using Nri.AssetConfigurators.Wpf.ViewModels;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminAssetConfiguratorsViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private AssetBlueprintPresentation? _selectedBlueprint;
    private string _statusMessage = "Откройте конструктор или загрузите чертежи игроков.";
    private string _errorMessage = string.Empty;
    private bool _includeArchived;
    private string _ownerFilter = string.Empty;
    private string _kindFilter = "all";
    private string _canonicalDraftStatus = "Канонический черновик ещё не подготовлен.";

    public AdminAssetConfiguratorsViewModel(CommandApi api)
    {
        _api = api;
        Workspace = new AssetConfiguratorWorkspaceViewModel(showGmFields: true);
        Blueprints = new ObservableCollection<AssetBlueprintPresentation>();
        BlueprintsView = CollectionViewSource.GetDefaultView(Blueprints);
        BlueprintsView.Filter = FilterBlueprint;
        KindOptions = new[]
        {
            new AssetBlueprintFilterOption("all", "Все типы"),
            new AssetBlueprintFilterOption("spacecraft", "Космические корабли и станции"),
            new AssetBlueprintFilterOption("land_marine", "Наземная и морская техника"),
            new AssetBlueprintFilterOption("building", "Здания и укрепления")
        };
        RefreshBlueprintsCommand = new RelayCommand(RefreshBlueprints);
        OpenBlueprintCommand = new RelayCommand(OpenSelectedBlueprint, () => SelectedBlueprint != null);
        PrepareCanonicalBlueprintDraftCommand = new RelayCommand(PrepareCanonicalBlueprintDraft, () => SelectedBlueprint != null);
    }

    public AssetConfiguratorWorkspaceViewModel Workspace { get; }
    public ObservableCollection<AssetBlueprintPresentation> Blueprints { get; }
    public ICollectionView BlueprintsView { get; }
    public IReadOnlyList<AssetBlueprintFilterOption> KindOptions { get; }
    public ICommand RefreshBlueprintsCommand { get; }
    public ICommand OpenBlueprintCommand { get; }
    public ICommand PrepareCanonicalBlueprintDraftCommand { get; }

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
            ((RelayCommand)OpenBlueprintCommand).RaiseCanExecuteChanged();
            ((RelayCommand)PrepareCanonicalBlueprintDraftCommand).RaiseCanExecuteChanged();
        }
    }

    public bool HasSelectedBlueprint => SelectedBlueprint != null;

    public bool IncludeArchived
    {
        get => _includeArchived;
        set
        {
            if (_includeArchived == value)
                return;
            _includeArchived = value;
            Notify();
        }
    }

    public string OwnerFilter
    {
        get => _ownerFilter;
        set
        {
            if (_ownerFilter == (value ?? string.Empty))
                return;
            _ownerFilter = value ?? string.Empty;
            Notify();
            BlueprintsView.Refresh();
        }
    }

    public AssetBlueprintFilterOption SelectedKindOption
    {
        get => KindOptions.First(item => item.Id == _kindFilter);
        set
        {
            if (value == null || _kindFilter == value.Id)
                return;
            _kindFilter = value.Id;
            Notify();
            BlueprintsView.Refresh();
        }
    }

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

    public string CanonicalDraftStatus
    {
        get => _canonicalDraftStatus;
        private set
        {
            if (_canonicalDraftStatus == value) return;
            _canonicalDraftStatus = value;
            Notify();
        }
    }

    private void RefreshBlueprints()
    {
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.AssetBlueprintAdminList(new Dictionary<string, object>
            {
                ["includeArchived"] = IncludeArchived
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(response.Message)
                    ? "Не удалось загрузить чертежи игроков."
                    : response.Message;
                return;
            }

            Blueprints.Clear();
            foreach (var item in AssetBlueprintPresentationParser.ParseItems(response.Payload))
                Blueprints.Add(item);
            SelectedBlueprint = Blueprints.FirstOrDefault();
            StatusMessage = Blueprints.Count == 0
                ? "Чертежей игроков пока нет."
                : $"Загружено чертежей: {Blueprints.Count}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Сервер недоступен: " + ex.Message;
        }
    }

    private void OpenSelectedBlueprint()
    {
        if (SelectedBlueprint == null)
            return;

        ErrorMessage = string.Empty;
        try
        {
            var response = _api.AssetBlueprintAdminGet(new Dictionary<string, object>
            {
                ["blueprintId"] = SelectedBlueprint.BlueprintId
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(response.Message)
                    ? "Не удалось открыть чертёж."
                    : response.Message;
                return;
            }

            var item = AssetBlueprintPresentationParser.ParseSingle(response.Payload);
            if (item == null)
            {
                ErrorMessage = "Сервер вернул пустой чертёж.";
                return;
            }

            SelectedBlueprint = item;
            Workspace.ApplyInput(
                item.ConfiguratorKind,
                AssetConfiguratorPayloadCodec.FromPayload(item.ConfiguratorKind, item.Configuration));
            StatusMessage = $"Открыт чертёж «{item.Name}» игрока {item.OwnerText}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось открыть чертёж: " + ex.Message;
        }
    }

    private void PrepareCanonicalBlueprintDraft()
    {
        if (SelectedBlueprint == null) return;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.TechnologyBlueprintAdminPrepareFromAsset(new Dictionary<string, object>
            {
                ["assetBlueprintId"] = SelectedBlueprint.BlueprintId
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(response.Message)
                    ? "Не удалось подготовить канонический черновик."
                    : response.Message;
                return;
            }
            CanonicalBlueprintDraftTransfer0187.Store(response.Payload);
            var resolved = response.Payload.TryGetValue("resolvedComponentCount", out var resolvedValue) ? Convert.ToString(resolvedValue) : "0";
            var unresolved = response.Payload.TryGetValue("unresolvedComponentCount", out var unresolvedValue) ? Convert.ToString(unresolvedValue) : "0";
            CanonicalDraftStatus = $"Черновик подготовлен: сопоставлено {resolved}, требуют проверки {unresolved}. Откройте «Справочники» → «Технологии, рецепты и проекты».";
            StatusMessage = "Подготовлен несохранённый канонический черновик. Исходный личный чертёж не изменён.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось подготовить канонический черновик: " + ex.Message;
        }
    }

    private bool FilterBlueprint(object raw)
    {
        if (!(raw is AssetBlueprintPresentation item))
            return false;
        if (_kindFilter != "all" && !string.Equals(item.ConfiguratorKind, _kindFilter, StringComparison.Ordinal))
            return false;
        var owner = OwnerFilter.Trim();
        return owner.Length == 0 ||
               item.OwnerText.IndexOf(owner, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }
}

public sealed class AssetBlueprintFilterOption
{
    public AssetBlueprintFilterOption(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }
}
