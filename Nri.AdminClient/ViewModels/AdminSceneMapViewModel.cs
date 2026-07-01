using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminSceneMapViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = string.Empty;
    private string _ruleSetId = string.Empty;
    private string _statusMessage = "Задайте CampaignId и загрузите карты сцены.";
    private string _errorMessage = string.Empty;
    private string _warningMessage = string.Empty;
    private bool _isLoading;
    private bool _isSceneMapEnabled;
    private bool _isSceneMarkersEnabled;
    private bool _isSceneFogEnabled;
    private bool _isSceneSessionLinkEnabled;
    private DateTime _lastRefreshAtUtc;
    private SceneMapListUiItem? _selectedMap;
    private SceneMarkerUiItem? _selectedMarker;
    private string _newMapName = "Новая карта сцены";
    private string _newMapDescription = string.Empty;
    private int _newMapWidthMeters = 2000;
    private int _newMapHeightMeters = 2000;
    private int _newGridCellSizeMeters = 25;
    private bool _showGrid = true;
    private bool _showCoordinates = true;
    private string _markerName = "Маркер";
    private string _markerType = "point_of_interest";
    private double _markerX;
    private double _markerY;
    private string _markerIconKey = string.Empty;
    private string _markerColorKey = string.Empty;
    private bool _markerPlayerVisible = true;
    private string _markerLinkedEntityType = string.Empty;
    private string _markerLinkedEntityId = string.Empty;
    private string _markerCardTitle = string.Empty;
    private string _markerCardDescription = string.Empty;
    private string _markerPublicNotes = string.Empty;
    private string _markerGmNotes = string.Empty;
    private string _fogMode = FogOfWarModeIds.Manual;
    private string _fogDefaultState = FogDefaultStateIds.Revealed;
    private int _fogCellSizeMeters = 25;
    private long _fogRevision;
    private string _fogBrushMode = FogBrushModeIds.Reveal;
    private string _fogBrushShape = FogShapeIds.Cell;
    private int _fogBrushWidthMeters = 50;
    private int _fogBrushHeightMeters = 50;
    private int _fogBrushRadiusMeters = 25;
    private string _fogSummary = "Туман войны не загружен.";
    private double _canvasWidth = 760;
    private double _canvasHeight = 500;
    private string _canvasScaleLabel = "1м = 0.0px";
    private string _selectedMapSummary = "Карта не выбрана.";
    private string _selectedMarkerSummary = "Маркер не выбран.";
    private string _sessionId = "default";
    private string _activeGroupId = string.Empty;
    private string _sceneId = string.Empty;
    private bool _hasActiveMap;
    private string _activeMapId = string.Empty;
    private string _activeMapName = "Не выбрана";
    private readonly List<MapFogCellRange> _fogHiddenRanges = new List<MapFogCellRange>();
    private readonly List<MapFogCellRange> _fogRevealedRanges = new List<MapFogCellRange>();

    public AdminSceneMapViewModel(CommandApi api)
    {
        _api = api;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshMapsCommand = new RelayCommand(RefreshMaps);
        CreateMapCommand = new RelayCommand(CreateMap);
        LoadSelectedMapCommand = new RelayCommand(LoadSelectedMap);
        SaveMapSettingsCommand = new RelayCommand(SaveMapSettings);
        ArchiveMapCommand = new RelayCommand(ArchiveSelectedMap);
        AddMarkerCommand = new RelayCommand(AddMarker);
        MoveMarkerCommand = new RelayCommand(MoveMarker);
        SaveMarkerCommand = new RelayCommand(UpdateMarker);
        RemoveMarkerCommand = new RelayCommand(RemoveMarker);
        RefreshFogCommand = new RelayCommand(RefreshFog);
        PaintFogCommand = new RelayCommand(PaintFogFromFields);
        RevealAllFogCommand = new RelayCommand(RevealAllFog);
        HideAllFogCommand = new RelayCommand(HideAllFog);
        ClearFogCommand = new RelayCommand(ClearFogCustom);
        ResetFogCommand = new RelayCommand(ResetFog);
        RefreshActiveMapCommand = new RelayCommand(LoadActiveMapLink);
        SetActiveMapCommand = new RelayCommand(SetSelectedMapActive);
        ClearActiveMapCommand = new RelayCommand(ClearActiveMap);
        ClearErrorCommand = new RelayCommand(() => { ErrorMessage = string.Empty; WarningMessage = string.Empty; });
    }

    public ObservableCollection<SceneMapListUiItem> Maps { get; } = new ObservableCollection<SceneMapListUiItem>();
    public ObservableCollection<SceneMarkerUiItem> Markers { get; } = new ObservableCollection<SceneMarkerUiItem>();
    public ObservableCollection<MapGridLineUiItem> GridLines { get; } = new ObservableCollection<MapGridLineUiItem>();
    public ObservableCollection<MapFogOverlayUiItem> FogOverlays { get; } = new ObservableCollection<MapFogOverlayUiItem>();
    public ObservableCollection<SceneMarkerBindingUiItem> MarkerBindings { get; } = new ObservableCollection<SceneMarkerBindingUiItem>();
    public ObservableCollection<string> CanvasCoordinateHints { get; } = new ObservableCollection<string>();

    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshMapsCommand { get; }
    public ICommand CreateMapCommand { get; }
    public ICommand LoadSelectedMapCommand { get; }
    public ICommand SaveMapSettingsCommand { get; }
    public ICommand ArchiveMapCommand { get; }
    public ICommand AddMarkerCommand { get; }
    public ICommand MoveMarkerCommand { get; }
    public ICommand SaveMarkerCommand { get; }
    public ICommand RemoveMarkerCommand { get; }
    public ICommand RefreshFogCommand { get; }
    public ICommand PaintFogCommand { get; }
    public ICommand RevealAllFogCommand { get; }
    public ICommand HideAllFogCommand { get; }
    public ICommand ClearFogCommand { get; }
    public ICommand ResetFogCommand { get; }
    public ICommand RefreshActiveMapCommand { get; }
    public ICommand SetActiveMapCommand { get; }
    public ICommand ClearActiveMapCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId
    {
        get => _campaignId;
        set { if (_campaignId != value) { _campaignId = value; Notify(); Notify(nameof(CanLoadMaps)); } }
    }

    public string RuleSetId
    {
        get => _ruleSetId;
        set { if (_ruleSetId != value) { _ruleSetId = value; Notify(); Notify(nameof(CanCreateMap)); } }
    }

    public string SessionId
    {
        get => _sessionId;
        set { if (_sessionId != value) { _sessionId = value; Notify(); } }
    }

    public string ActiveGroupId
    {
        get => _activeGroupId;
        set { if (_activeGroupId != value) { _activeGroupId = value; Notify(); } }
    }

    public string SceneId
    {
        get => _sceneId;
        set { if (_sceneId != value) { _sceneId = value; Notify(); } }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } }
    }

    public string WarningMessage
    {
        get => _warningMessage;
        private set { if (_warningMessage != value) { _warningMessage = value; Notify(); Notify(nameof(HasWarning)); } }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                Notify();
                Notify(nameof(IsIdle));
                Notify(nameof(CanLoadMaps));
                Notify(nameof(CanCreateMap));
                Notify(nameof(CanWorkWithSelectedMap));
                Notify(nameof(CanWorkWithMarkers));
                Notify(nameof(CanWorkWithFog));
                Notify(nameof(CanManageActiveMap));
            }
        }
    }

    public bool IsIdle => !IsLoading;

    public bool IsSceneMapEnabled
    {
        get => _isSceneMapEnabled;
        private set
        {
            if (_isSceneMapEnabled != value)
            {
                _isSceneMapEnabled = value;
                Notify();
                Notify(nameof(IsSceneMapDisabled));
                Notify(nameof(CanLoadMaps));
                Notify(nameof(CanCreateMap));
                Notify(nameof(CanWorkWithSelectedMap));
                Notify(nameof(CanWorkWithFog));
                Notify(nameof(CanManageActiveMap));
            }
        }
    }

    public bool IsSceneMarkersEnabled
    {
        get => _isSceneMarkersEnabled;
        private set
        {
            if (_isSceneMarkersEnabled != value)
            {
                _isSceneMarkersEnabled = value;
                Notify();
                Notify(nameof(CanWorkWithMarkers));
            }
        }
    }

    public bool IsSceneFogEnabled
    {
        get => _isSceneFogEnabled;
        private set
        {
            if (_isSceneFogEnabled != value)
            {
                _isSceneFogEnabled = value;
                Notify();
                Notify(nameof(CanWorkWithFog));
            }
        }
    }

    public bool IsSceneSessionLinkEnabled
    {
        get => _isSceneSessionLinkEnabled;
        private set
        {
            if (_isSceneSessionLinkEnabled != value)
            {
                _isSceneSessionLinkEnabled = value;
                Notify();
                Notify(nameof(CanManageActiveMap));
            }
        }
    }

    public bool IsSceneMapDisabled => !IsSceneMapEnabled;
    public bool CanLoadMaps => IsSceneMapEnabled && IsIdle && !string.IsNullOrWhiteSpace(CampaignId);
    public bool CanCreateMap => IsSceneMapEnabled && IsIdle && !string.IsNullOrWhiteSpace(CampaignId) && !string.IsNullOrWhiteSpace(RuleSetId);
    public bool CanWorkWithSelectedMap => IsSceneMapEnabled && IsIdle && SelectedMap != null;
    public bool CanWorkWithMarkers => CanWorkWithSelectedMap && IsSceneMarkersEnabled;
    public bool CanWorkWithFog => CanWorkWithSelectedMap && IsSceneFogEnabled;
    public bool CanManageActiveMap => CanWorkWithSelectedMap && IsSceneSessionLinkEnabled;

    public bool HasActiveMap
    {
        get => _hasActiveMap;
        private set { if (_hasActiveMap != value) { _hasActiveMap = value; Notify(); Notify(nameof(ActiveMapSummary)); } }
    }

    public string ActiveMapId
    {
        get => _activeMapId;
        private set { if (_activeMapId != value) { _activeMapId = value; Notify(); Notify(nameof(ActiveMapSummary)); } }
    }

    public string ActiveMapName
    {
        get => _activeMapName;
        private set { if (_activeMapName != value) { _activeMapName = value; Notify(); Notify(nameof(ActiveMapSummary)); } }
    }

    public string ActiveMapSummary => HasActiveMap
        ? $"{ActiveMapName} ({ActiveMapId})"
        : "Не выбрана";

    public DateTime LastRefreshAtUtc
    {
        get => _lastRefreshAtUtc;
        private set { if (_lastRefreshAtUtc != value) { _lastRefreshAtUtc = value; Notify(); Notify(nameof(LastRefreshText)); } }
    }

    public string LastRefreshText => LastRefreshAtUtc == default ? "ещё не обновлялось" : LastRefreshAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public SceneMapListUiItem? SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (_selectedMap != value)
            {
                _selectedMap = value;
                Notify();
                Notify(nameof(CanWorkWithSelectedMap));
                Notify(nameof(CanWorkWithMarkers));
                Notify(nameof(CanManageActiveMap));
                SelectedMapSummary = value == null
                    ? "Карта не выбрана."
                    : $"{value.Name} • {value.WidthMeters}×{value.HeightMeters}м • шаг {value.GridCellSizeMeters}м";
                if (value != null)
                {
                    NewMapName = value.Name;
                    NewMapDescription = value.Description;
                    NewMapWidthMeters = value.WidthMeters;
                    NewMapHeightMeters = value.HeightMeters;
                    NewGridCellSizeMeters = value.GridCellSizeMeters;
                    ShowGrid = value.ShowGrid;
                    ShowCoordinates = value.ShowCoordinates;
                }
            }
        }
    }

    public SceneMarkerUiItem? SelectedMarker
    {
        get => _selectedMarker;
        set
        {
            if (_selectedMarker != value)
            {
                _selectedMarker = value;
                foreach (var marker in Markers) marker.IsSelected = ReferenceEquals(marker, value);
                Notify();
                Notify(nameof(SelectedMarkerBindingText));
                SelectedMarkerSummary = value == null
                    ? "Маркер не выбран."
                    : $"{value.Name} • {value.MarkerTypeDisplay} • X={value.X:0.##}, Y={value.Y:0.##}";
                if (value != null)
                {
                    MarkerName = value.Name;
                    MarkerType = value.MarkerType;
                    MarkerX = value.X;
                    MarkerY = value.Y;
                    MarkerIconKey = value.IconKey;
                    MarkerColorKey = value.ColorKey;
                    MarkerPlayerVisible = value.IsPlayerVisible;
                    MarkerLinkedEntityType = value.LinkedEntityType;
                    MarkerLinkedEntityId = value.LinkedEntityId;
                    MarkerCardTitle = value.CardTitle;
                    MarkerCardDescription = value.CardDescription;
                    MarkerPublicNotes = value.PublicNotes;
                    MarkerGmNotes = value.GMNotes;
                    ClientLogService.Instance.Info("admin.map.marker.selected");
                }
            }
        }
    }

    public string NewMapName { get => _newMapName; set { if (_newMapName != value) { _newMapName = value; Notify(); } } }
    public string NewMapDescription { get => _newMapDescription; set { if (_newMapDescription != value) { _newMapDescription = value; Notify(); } } }
    public int NewMapWidthMeters { get => _newMapWidthMeters; set { if (_newMapWidthMeters != value) { _newMapWidthMeters = value; Notify(); } } }
    public int NewMapHeightMeters { get => _newMapHeightMeters; set { if (_newMapHeightMeters != value) { _newMapHeightMeters = value; Notify(); } } }
    public int NewGridCellSizeMeters { get => _newGridCellSizeMeters; set { if (_newGridCellSizeMeters != value) { _newGridCellSizeMeters = value; Notify(); } } }
    public bool ShowGrid { get => _showGrid; set { if (_showGrid != value) { _showGrid = value; Notify(); } } }
    public bool ShowCoordinates { get => _showCoordinates; set { if (_showCoordinates != value) { _showCoordinates = value; Notify(); } } }

    public string MarkerName { get => _markerName; set { if (_markerName != value) { _markerName = value; Notify(); } } }
    public string MarkerType { get => _markerType; set { if (_markerType != value) { _markerType = value; Notify(); } } }
    public double MarkerX { get => _markerX; set { if (Math.Abs(_markerX - value) > 0.0001) { _markerX = value; Notify(); } } }
    public double MarkerY { get => _markerY; set { if (Math.Abs(_markerY - value) > 0.0001) { _markerY = value; Notify(); } } }
    public string MarkerIconKey { get => _markerIconKey; set { if (_markerIconKey != value) { _markerIconKey = value; Notify(); } } }
    public string MarkerColorKey { get => _markerColorKey; set { if (_markerColorKey != value) { _markerColorKey = value; Notify(); } } }
    public bool MarkerPlayerVisible { get => _markerPlayerVisible; set { if (_markerPlayerVisible != value) { _markerPlayerVisible = value; Notify(); } } }
    public string MarkerLinkedEntityType { get => _markerLinkedEntityType; set { if (_markerLinkedEntityType != value) { _markerLinkedEntityType = value; Notify(); } } }
    public string MarkerLinkedEntityId { get => _markerLinkedEntityId; set { if (_markerLinkedEntityId != value) { _markerLinkedEntityId = value; Notify(); } } }
    public string MarkerCardTitle { get => _markerCardTitle; set { if (_markerCardTitle != value) { _markerCardTitle = value; Notify(); } } }
    public string MarkerCardDescription { get => _markerCardDescription; set { if (_markerCardDescription != value) { _markerCardDescription = value; Notify(); } } }
    public string MarkerPublicNotes { get => _markerPublicNotes; set { if (_markerPublicNotes != value) { _markerPublicNotes = value; Notify(); } } }
    public string MarkerGmNotes { get => _markerGmNotes; set { if (_markerGmNotes != value) { _markerGmNotes = value; Notify(); } } }
    public string FogMode { get => _fogMode; set { if (_fogMode != value) { _fogMode = value; Notify(); } } }
    public string FogDefaultState { get => _fogDefaultState; set { if (_fogDefaultState != value) { _fogDefaultState = value; Notify(); } } }
    public int FogCellSizeMeters { get => _fogCellSizeMeters; set { if (_fogCellSizeMeters != value) { _fogCellSizeMeters = value; Notify(); } } }
    public long FogRevision { get => _fogRevision; private set { if (_fogRevision != value) { _fogRevision = value; Notify(); } } }
    public string FogBrushMode { get => _fogBrushMode; set { if (_fogBrushMode != value) { _fogBrushMode = value; Notify(); ClientLogService.Instance.Info("admin.map.fog.tool.changed"); } } }
    public string FogBrushShape { get => _fogBrushShape; set { if (_fogBrushShape != value) { _fogBrushShape = value; Notify(); } } }
    public int FogBrushWidthMeters { get => _fogBrushWidthMeters; set { if (_fogBrushWidthMeters != value) { _fogBrushWidthMeters = value; Notify(); } } }
    public int FogBrushHeightMeters { get => _fogBrushHeightMeters; set { if (_fogBrushHeightMeters != value) { _fogBrushHeightMeters = value; Notify(); } } }
    public int FogBrushRadiusMeters { get => _fogBrushRadiusMeters; set { if (_fogBrushRadiusMeters != value) { _fogBrushRadiusMeters = value; Notify(); } } }
    public string FogSummary { get => _fogSummary; private set { if (_fogSummary != value) { _fogSummary = value; Notify(); } } }

    public double CanvasWidth { get => _canvasWidth; private set { if (Math.Abs(_canvasWidth - value) > 0.01) { _canvasWidth = value; Notify(); } } }
    public double CanvasHeight { get => _canvasHeight; private set { if (Math.Abs(_canvasHeight - value) > 0.01) { _canvasHeight = value; Notify(); } } }
    public string CanvasScaleLabel { get => _canvasScaleLabel; private set { if (_canvasScaleLabel != value) { _canvasScaleLabel = value; Notify(); } } }
    public string SelectedMapSummary { get => _selectedMapSummary; private set { if (_selectedMapSummary != value) { _selectedMapSummary = value; Notify(); } } }
    public string SelectedMarkerSummary { get => _selectedMarkerSummary; private set { if (_selectedMarkerSummary != value) { _selectedMarkerSummary = value; Notify(); } } }
    public string SelectedMarkerBindingText => BuildSelectedMarkerBindingText();

    public void RefreshFlags()
    {
        try
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            if (response.Status != ResponseStatus.Ok)
            {
                WarningMessage = $"Не удалось загрузить флаги функций: {response.Message}";
                return;
            }

            var flagMaps = Dictionaries(Get(response.Payload, "flags")).ToList();
            var mapSystem = Flag(flagMaps, "UseMapSystemV1");
            var hierarchy = Flag(flagMaps, "UseSpaceHierarchyV1");
            var scene = Flag(flagMaps, "UseSceneMapV1");
            var markers = Flag(flagMaps, "UseSceneMapMarkers");
            var fog = Flag(flagMaps, "UseSceneMapFogOfWar");
            var sessionLink = Flag(flagMaps, "UseSceneMapSessionLink");
            IsSceneMapEnabled = mapSystem && hierarchy && scene;
            IsSceneMarkersEnabled = IsSceneMapEnabled && markers;
            IsSceneFogEnabled = IsSceneMapEnabled && fog;
            IsSceneSessionLinkEnabled = IsSceneMapEnabled && sessionLink;
            WarningMessage = IsSceneMapEnabled
                ? (IsSceneMarkersEnabled ? string.Empty : "Маркерные команды выключены флагами функций.")
                : "Карта сцены выключена флагами функций.";
            if (IsSceneMapEnabled && !IsSceneFogEnabled)
                WarningMessage = string.IsNullOrWhiteSpace(WarningMessage)
                    ? "Туман войны выключен флагами функций."
                    : $"{WarningMessage} Туман войны выключен флагами функций.";
            if (IsSceneMapEnabled && !IsSceneSessionLinkEnabled)
                WarningMessage = string.IsNullOrWhiteSpace(WarningMessage)
                    ? "Привязка активной карты к сессии выключена флагами функций."
                    : $"{WarningMessage} Привязка активной карты к сессии выключена флагами функций.";
        }
        catch (Exception ex)
        {
            WarningMessage = $"Снимок функций и модулей недоступен: {ex.Message}";
        }
    }

    public void RefreshMaps()
    {
        if (string.IsNullOrWhiteSpace(CampaignId))
        {
            ErrorMessage = "Укажите CampaignId для загрузки карт сцены.";
            return;
        }

        ClientLogService.Instance.Info("admin.map.scene.load.start");
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            RefreshFlags();
            if (!IsSceneMapEnabled)
            {
                StatusMessage = "Карта сцены выключена флагами функций.";
                return;
            }

            var response = _api.MapSceneList(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId ?? string.Empty },
                { "activeGroupId", ActiveGroupId ?? string.Empty },
                { "sceneId", SceneId ?? string.Empty },
                { "includeArchived", false }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                StatusMessage = "Не удалось загрузить список карт сцены.";
                return;
            }

            Maps.Clear();
            foreach (var item in Dictionaries(Get(response.Payload, "items")))
            {
                Maps.Add(SceneMapListUiItem.From(item));
            }

            StatusMessage = Maps.Count == 0 ? "Карты сцены пока не созданы." : $"Загружено карт: {Maps.Count}.";
            LastRefreshAtUtc = DateTime.UtcNow;
            if (SelectedMap == null || !Maps.Any(x => x.MapId == SelectedMap.MapId))
                SelectedMap = Maps.FirstOrDefault();
            LoadActiveMapLink();
            if (SelectedMap != null)
                LoadSelectedMap();
            ClientLogService.Instance.Info("admin.map.scene.load.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Ошибка загрузки карт сцены.";
            ClientLogService.Instance.Warn($"admin.map.scene.load.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void CreateMap()
    {
        if (!CanCreateMap)
        {
            ErrorMessage = "Проверьте flags, CampaignId и RuleSetId.";
            return;
        }

        if (!ValidateSceneSettings(NewMapWidthMeters, NewMapHeightMeters, NewGridCellSizeMeters))
            return;

        ClientLogService.Instance.Info("admin.map.scene.create.start");
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneCreate(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "ruleSetId", RuleSetId },
                { "name", FirstNonEmpty(NewMapName, "Новая карта сцены") },
                { "description", NewMapDescription ?? string.Empty },
                { "widthMeters", NewMapWidthMeters },
                { "heightMeters", NewMapHeightMeters },
                { "gridCellSizeMeters", NewGridCellSizeMeters },
                { "showGrid", ShowGrid },
                { "showCoordinates", ShowCoordinates }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            var createdId = Str(Get(response.Payload, "mapId"));
            StatusMessage = $"Карта создана: {FirstNonEmpty(createdId, NewMapName)}";
            RefreshMaps();
            if (!string.IsNullOrWhiteSpace(createdId))
            {
                SelectedMap = Maps.FirstOrDefault(item => item.MapId == createdId) ?? Maps.FirstOrDefault();
                LoadSelectedMap();
            }
            ClientLogService.Instance.Info("admin.map.scene.create.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.scene.create.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void LoadActiveMapLink()
    {
        if (!IsSceneSessionLinkEnabled || string.IsNullOrWhiteSpace(CampaignId))
        {
            HasActiveMap = false;
            ActiveMapId = string.Empty;
            ActiveMapName = "Не выбрана";
            foreach (var map in Maps)
                map.IsActive = false;
            return;
        }

        try
        {
            var response = _api.MapSceneActiveGet(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId ?? string.Empty },
                { "activeGroupId", ActiveGroupId ?? string.Empty },
                { "sceneId", SceneId ?? string.Empty }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                WarningMessage = response.Message;
                return;
            }

            HasActiveMap = Bool(Get(response.Payload, "hasActiveMap"));
            ActiveMapId = Str(Get(response.Payload, "mapId"));
            ActiveMapName = FirstNonEmpty(Str(Get(response.Payload, "mapName")), "Не выбрана");
            foreach (var map in Maps)
                map.IsActive = HasActiveMap && string.Equals(map.MapId, ActiveMapId, StringComparison.OrdinalIgnoreCase);

            ClientLogService.Instance.Info("admin.map.active.loaded");
        }
        catch (Exception ex)
        {
            WarningMessage = $"Не удалось загрузить активную карту: {ex.Message}";
            ClientLogService.Instance.Warn($"admin.map.active.load.error message={ex.Message}");
        }
    }

    public void SetSelectedMapActive()
    {
        if (!CanManageActiveMap || SelectedMap == null)
        {
            ErrorMessage = "Выберите карту сцены для назначения активной.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info("admin.map.active.set.start");
            var response = _api.MapSceneActiveSet(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId ?? string.Empty },
                { "activeGroupId", ActiveGroupId ?? string.Empty },
                { "sceneId", SceneId ?? string.Empty },
                { "mapId", SelectedMap.MapId }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            StatusMessage = $"Активная карта сцены: {SelectedMap.Name}.";
            LoadActiveMapLink();
            ClientLogService.Instance.Info("admin.map.active.set.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.active.set.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ClearActiveMap()
    {
        if (!IsSceneSessionLinkEnabled || string.IsNullOrWhiteSpace(CampaignId))
        {
            ErrorMessage = "Привязка активной карты недоступна.";
            return;
        }

        var confirmation = MessageBox.Show(
            "Игроки больше не будут видеть активную карту сцены. Продолжить?",
            "Снять активную карту",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info("admin.map.active.clear");
            var response = _api.MapSceneActiveClear(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId ?? string.Empty },
                { "activeGroupId", ActiveGroupId ?? string.Empty },
                { "sceneId", SceneId ?? string.Empty }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            HasActiveMap = false;
            ActiveMapId = string.Empty;
            ActiveMapName = "Не выбрана";
            foreach (var map in Maps)
                map.IsActive = false;
            StatusMessage = "Активная карта сцены снята.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.active.clear.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void LoadSelectedMap()
    {
        if (SelectedMap == null)
        {
            StatusMessage = "Выберите карту сцены.";
            return;
        }

        ClientLogService.Instance.Info("admin.map.scene.load.start");
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneGet(new Dictionary<string, object> { { "mapId", SelectedMap.MapId } });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            var map = AsMap(Get(response.Payload, "map"));
            if (map.Count > 0)
            {
                SelectedMap.Apply(map);
                NewMapName = SelectedMap.Name;
                NewMapDescription = SelectedMap.Description;
                NewMapWidthMeters = SelectedMap.WidthMeters;
                NewMapHeightMeters = SelectedMap.HeightMeters;
                NewGridCellSizeMeters = SelectedMap.GridCellSizeMeters;
                ShowGrid = SelectedMap.ShowGrid;
                ShowCoordinates = SelectedMap.ShowCoordinates;
                SelectedMapSummary = $"{SelectedMap.Name} • {SelectedMap.WidthMeters}×{SelectedMap.HeightMeters}м • шаг {SelectedMap.GridCellSizeMeters}м";
            }

            MarkerBindings.Clear();
            foreach (var binding in Dictionaries(Get(response.Payload, "markerBindings")))
                MarkerBindings.Add(SceneMarkerBindingUiItem.From(binding));
            Notify(nameof(SelectedMarkerBindingText));

            Markers.Clear();
            foreach (var markerPayload in Dictionaries(Get(response.Payload, "markers")))
                Markers.Add(SceneMarkerUiItem.From(markerPayload));

            ApplyFogPayload(AsMap(Get(response.Payload, "fog")));

            SelectedMarker = Markers.FirstOrDefault();
            RebuildCanvas();
            StatusMessage = $"Карта загружена: {SelectedMap.Name}. Маркеров: {Markers.Count}.";
            LastRefreshAtUtc = DateTime.UtcNow;
            ClientLogService.Instance.Info("admin.map.scene.load.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.scene.load.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SaveMapSettings()
    {
        if (SelectedMap == null) return;
        if (!ValidateSceneSettings(NewMapWidthMeters, NewMapHeightMeters, NewGridCellSizeMeters))
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneUpdateSettings(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "name", FirstNonEmpty(NewMapName, SelectedMap.Name) },
                { "description", NewMapDescription ?? string.Empty },
                { "widthMeters", NewMapWidthMeters },
                { "heightMeters", NewMapHeightMeters },
                { "gridCellSizeMeters", NewGridCellSizeMeters },
                { "showGrid", ShowGrid },
                { "showCoordinates", ShowCoordinates }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            SelectedMap.Name = FirstNonEmpty(NewMapName, SelectedMap.Name);
            SelectedMap.Description = NewMapDescription ?? string.Empty;
            SelectedMap.WidthMeters = NewMapWidthMeters;
            SelectedMap.HeightMeters = NewMapHeightMeters;
            SelectedMap.GridCellSizeMeters = NewGridCellSizeMeters;
            SelectedMap.ShowGrid = ShowGrid;
            SelectedMap.ShowCoordinates = ShowCoordinates;
            RebuildCanvas();
            StatusMessage = "Настройки карты сохранены.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ArchiveSelectedMap()
    {
        if (SelectedMap == null) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneArchive(new Dictionary<string, object> { { "mapId", SelectedMap.MapId } });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            StatusMessage = $"Карта архивирована: {SelectedMap.Name}.";
            RefreshMaps();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void AddMarker()
    {
        if (!CanWorkWithMarkers || SelectedMap == null)
        {
            ErrorMessage = "Выберите карту и проверьте marker flags.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneMarkerAdd(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "name", FirstNonEmpty(MarkerName, "Маркер") },
                { "markerType", FirstNonEmpty(MarkerType, "custom") },
                { "x", MarkerX },
                { "y", MarkerY },
                { "iconKey", MarkerIconKey ?? string.Empty },
                { "colorKey", MarkerColorKey ?? string.Empty },
                { "isPlayerVisible", MarkerPlayerVisible },
                { "linkedEntityType", MarkerLinkedEntityType ?? string.Empty },
                { "linkedEntityId", MarkerLinkedEntityId ?? string.Empty },
                { "cardTitle", MarkerCardTitle ?? string.Empty },
                { "cardDescription", MarkerCardDescription ?? string.Empty },
                { "publicNotes", MarkerPublicNotes ?? string.Empty },
                { "gmNotes", MarkerGmNotes ?? string.Empty }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            var marker = SceneMarkerUiItem.From(AsMap(Get(response.Payload, "marker")));
            if (string.IsNullOrWhiteSpace(marker.MarkerId))
                marker.MarkerId = Str(Get(response.Payload, "markerId"));
            if (!string.IsNullOrWhiteSpace(marker.MarkerId))
            {
                Markers.Add(marker);
                SelectedMarker = marker;
                RebuildCanvas();
            }

            StatusMessage = "Маркер добавлен.";
            ClientLogService.Instance.Info("admin.map.marker.add");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.marker.add.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void MoveMarker()
    {
        if (!CanWorkWithMarkers || SelectedMarker == null)
        {
            ErrorMessage = "Выберите маркер для перемещения.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneMarkerMove(new Dictionary<string, object>
            {
                { "markerId", SelectedMarker.MarkerId },
                { "x", MarkerX },
                { "y", MarkerY }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            SelectedMarker.X = MarkerX;
            SelectedMarker.Y = MarkerY;
            RebuildCanvas();
            StatusMessage = "Маркер перемещён.";
            ClientLogService.Instance.Info("admin.map.marker.move");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.marker.move.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void UpdateMarker()
    {
        if (!CanWorkWithMarkers || SelectedMarker == null)
        {
            ErrorMessage = "Выберите маркер для сохранения.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneMarkerUpdate(new Dictionary<string, object>
            {
                { "markerId", SelectedMarker.MarkerId },
                { "name", FirstNonEmpty(MarkerName, "Маркер") },
                { "markerType", FirstNonEmpty(MarkerType, "custom") },
                { "x", MarkerX },
                { "y", MarkerY },
                { "iconKey", MarkerIconKey ?? string.Empty },
                { "colorKey", MarkerColorKey ?? string.Empty },
                { "isPlayerVisible", MarkerPlayerVisible },
                { "linkedEntityType", MarkerLinkedEntityType ?? string.Empty },
                { "linkedEntityId", MarkerLinkedEntityId ?? string.Empty },
                { "cardTitle", MarkerCardTitle ?? string.Empty },
                { "cardDescription", MarkerCardDescription ?? string.Empty },
                { "publicNotes", MarkerPublicNotes ?? string.Empty },
                { "gmNotes", MarkerGmNotes ?? string.Empty }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            SelectedMarker.Apply(AsMap(Get(response.Payload, "marker")));
            RebuildCanvas();
            StatusMessage = "Маркер обновлён.";
            ClientLogService.Instance.Info("admin.map.marker.update");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.marker.update.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void RemoveMarker()
    {
        if (!CanWorkWithMarkers || SelectedMarker == null)
        {
            ErrorMessage = "Выберите маркер для удаления.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Удалить маркер «{SelectedMarker.Name}»?",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
            return;

        var markerId = SelectedMarker.MarkerId;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneMarkerRemove(new Dictionary<string, object> { { "markerId", markerId } });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            var found = Markers.FirstOrDefault(marker => marker.MarkerId == markerId);
            if (found != null) Markers.Remove(found);
            SelectedMarker = Markers.FirstOrDefault();
            RebuildCanvas();
            StatusMessage = "Маркер удалён.";
            ClientLogService.Instance.Info("admin.map.marker.remove");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.marker.remove.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void RefreshFog()
    {
        if (!CanWorkWithSelectedMap || SelectedMap == null)
        {
            ErrorMessage = "Выберите карту сцены.";
            return;
        }

        if (!IsSceneFogEnabled)
        {
            WarningMessage = "Туман войны выключен флагами функций.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info("admin.map.fog.refresh");
            var response = _api.MapSceneFogGet(new Dictionary<string, object> { { "mapId", SelectedMap.MapId } });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            ApplyFogPayload(AsMap(Get(response.Payload, "fog")));
            RebuildCanvas();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.fog.refresh.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void PaintFogFromFields()
    {
        if (!CanWorkWithFog || SelectedMap == null)
        {
            ErrorMessage = "Туман войны недоступен.";
            return;
        }

        var centerX = MarkerX;
        var centerY = MarkerY;
        PaintFog(centerX, centerY, FogBrushShape);
    }

    public void PaintFogAtPixel(double pixelX, double pixelY)
    {
        if (!CanWorkWithFog || SelectedMap == null) return;
        var projection = MapCanvasProjectionHelper.Calculate(SelectedMap.WidthMeters, SelectedMap.HeightMeters, 860, 540);
        var metersX = MapCanvasProjectionHelper.ToMeters(pixelX, projection.Scale);
        var metersY = MapCanvasProjectionHelper.ToMeters(pixelY, projection.Scale);
        PaintFog(metersX, metersY, FogShapeIds.Cell);
    }

    public void HideAllFog()
    {
        if (!ConfirmFogWideAction()) return;
        ExecuteFogFill(FogDefaultStateIds.Hidden, "admin.map.fog.fill.confirmed");
    }

    public void RevealAllFog()
    {
        if (!ConfirmFogWideAction()) return;
        ExecuteFogFill(FogDefaultStateIds.Revealed, "admin.map.fog.fill.confirmed");
    }

    public void ClearFogCustom()
    {
        if (!ConfirmFogWideAction()) return;
        ExecuteFogClear(FogClearModeIds.ClearCustom);
    }

    public void ResetFog()
    {
        if (!ConfirmFogWideAction()) return;
        if (!CanWorkWithFog || SelectedMap == null) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneFogReset(new Dictionary<string, object> { { "mapId", SelectedMap.MapId } });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            ApplyFogPayload(AsMap(Get(response.Payload, "fog")));
            RebuildCanvas();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.fog.reset.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool ValidateSceneSettings(int widthMeters, int heightMeters, int gridCellSizeMeters)
    {
        if (widthMeters < 250 || widthMeters > 4000)
        {
            ErrorMessage = "Ширина карты сцены должна быть в диапазоне 250..4000 м.";
            return false;
        }

        if (heightMeters < 250 || heightMeters > 4000)
        {
            ErrorMessage = "Высота карты сцены должна быть в диапазоне 250..4000 м.";
            return false;
        }

        if (gridCellSizeMeters < 1 || gridCellSizeMeters > 500)
        {
            ErrorMessage = "Размер клетки сетки должен быть в диапазоне 1..500 м.";
            return false;
        }

        return true;
    }

    private void RebuildCanvas()
    {
        GridLines.Clear();
        FogOverlays.Clear();
        CanvasCoordinateHints.Clear();
        if (SelectedMap == null)
        {
            CanvasWidth = 760;
            CanvasHeight = 500;
            CanvasScaleLabel = "1м = 0.0px";
            return;
        }

        var projection = MapCanvasProjectionHelper.Calculate(SelectedMap.WidthMeters, SelectedMap.HeightMeters, 860, 540);
        var scale = projection.Scale;
        CanvasWidth = projection.CanvasWidth;
        CanvasHeight = projection.CanvasHeight;
        CanvasScaleLabel = $"1м = {scale:0.###}px";

        if (ShowGrid)
        {
            var cell = Math.Max(1, SelectedMap.GridCellSizeMeters);
            for (var x = 0; x <= SelectedMap.WidthMeters; x += cell)
            {
                var px = MapCanvasProjectionHelper.ToPixel(x, scale);
                GridLines.Add(new MapGridLineUiItem { X1 = px, Y1 = 0, X2 = px, Y2 = CanvasHeight });
            }

            for (var y = 0; y <= SelectedMap.HeightMeters; y += cell)
            {
                var py = MapCanvasProjectionHelper.ToPixel(y, scale);
                GridLines.Add(new MapGridLineUiItem { X1 = 0, Y1 = py, X2 = CanvasWidth, Y2 = py });
            }
        }

        foreach (var marker in Markers)
        {
            marker.PixelX = MapCanvasProjectionHelper.ToPixel(marker.X, scale);
            marker.PixelY = MapCanvasProjectionHelper.ToPixel(marker.Y, scale);
        }

        BuildFogOverlay(scale);

        if (ShowCoordinates)
        {
            CanvasCoordinateHints.Add("Начало координат: X=0, Y=0 (левый верхний угол)");
            CanvasCoordinateHints.Add($"Границы: X 0..{SelectedMap.WidthMeters}, Y 0..{SelectedMap.HeightMeters}");
        }

        ClientLogService.Instance.Debug("admin.map.canvas.render");
    }

    public void SelectMarkerFromUi(SceneMarkerUiItem? marker)
    {
        if (marker == null) return;
        SelectedMarker = marker;
    }

    private string BuildSelectedMarkerBindingText()
    {
        if (SelectedMarker == null) return "Привязка: Без привязки";
        var bindings = MarkerBindings
            .Where(binding => string.Equals(binding.MarkerId, SelectedMarker.MarkerId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (bindings.Count == 0) return "Привязка: Без привязки";

        var first = bindings[0];
        var label = string.IsNullOrWhiteSpace(first.DisplayName) ? first.EntityId : first.DisplayName;
        return $"Привязка: {first.BindingType} / {FirstNonEmpty(label, "—")}";
    }

    private void ApplyFogPayload(Dictionary<string, object> fogPayload)
    {
        var hasFog = Bool(Get(fogPayload, "hasFog"));
        FogMode = FirstNonEmpty(Str(Get(fogPayload, "mode")), FogOfWarModeIds.Manual);
        FogDefaultState = FirstNonEmpty(Str(Get(fogPayload, "defaultState")), FogDefaultStateIds.Revealed);
        FogCellSizeMeters = Int(Get(fogPayload, "cellSizeMeters"), Math.Max(5, NewGridCellSizeMeters));
        FogRevision = Long(Get(fogPayload, "revision"), 0L);
        _fogHiddenRanges.Clear();
        _fogHiddenRanges.AddRange(ReadFogRanges(Get(fogPayload, "hiddenCells")));
        _fogRevealedRanges.Clear();
        _fogRevealedRanges.AddRange(ReadFogRanges(Get(fogPayload, "revealedCells")));
        FogSummary = hasFog
            ? $"Режим: {FogMode}; default: {FogDefaultState}; скрыто областей: {_fogHiddenRanges.Count}; раскрыто областей: {_fogRevealedRanges.Count}; rev {FogRevision}"
            : "Туман войны не настроен.";
    }

    private void BuildFogOverlay(double scale)
    {
        if (!IsSceneFogEnabled || SelectedMap == null) return;
        if (string.Equals(FogMode, FogOfWarModeIds.Disabled, StringComparison.OrdinalIgnoreCase)) return;

        var cell = Math.Max(1, FogCellSizeMeters);
        var ranges = BuildHiddenRangesForOverlay();
        foreach (var range in ranges)
        {
            var fromX = MapCanvasProjectionHelper.CellToMeters(range.FromX, cell);
            var fromY = MapCanvasProjectionHelper.CellToMeters(range.FromY, cell);
            var widthMeters = (range.ToX - range.FromX + 1) * cell;
            var heightMeters = (range.ToY - range.FromY + 1) * cell;
            var clampedWidth = Math.Min(widthMeters, Math.Max(0, SelectedMap.WidthMeters - fromX));
            var clampedHeight = Math.Min(heightMeters, Math.Max(0, SelectedMap.HeightMeters - fromY));
            if (clampedWidth <= 0 || clampedHeight <= 0) continue;

            FogOverlays.Add(new MapFogOverlayUiItem
            {
                X = MapCanvasProjectionHelper.ToPixel(fromX, scale),
                Y = MapCanvasProjectionHelper.ToPixel(fromY, scale),
                Width = Math.Max(1, MapCanvasProjectionHelper.ToPixel(clampedWidth, scale)),
                Height = Math.Max(1, MapCanvasProjectionHelper.ToPixel(clampedHeight, scale))
            });
        }
    }

    private IReadOnlyCollection<MapFogCellRange> BuildHiddenRangesForOverlay()
    {
        if (SelectedMap == null) return Array.Empty<MapFogCellRange>();
        var hidden = _fogHiddenRanges.Select(CloneFogRange).ToList();
        var revealed = _fogRevealedRanges.Select(CloneFogRange).ToList();
        if (string.Equals(FogDefaultState, FogDefaultStateIds.Hidden, StringComparison.OrdinalIgnoreCase))
        {
            var mapCellsX = Math.Max(0, MapCanvasProjectionHelper.ToCellIndex(Math.Max(0d, SelectedMap.WidthMeters - 0.0001d), Math.Max(1, FogCellSizeMeters)));
            var mapCellsY = Math.Max(0, MapCanvasProjectionHelper.ToCellIndex(Math.Max(0d, SelectedMap.HeightMeters - 0.0001d), Math.Max(1, FogCellSizeMeters)));
            var current = new List<MapFogCellRange> { new MapFogCellRange { FromX = 0, FromY = 0, ToX = mapCellsX, ToY = mapCellsY } };
            foreach (var reveal in revealed)
                current = SubtractFogRanges(current, reveal);
            current.AddRange(hidden);
            return current.Where(IsValidFogRange).Select(CloneFogRange).ToArray();
        }

        return hidden.Where(IsValidFogRange).Select(CloneFogRange).ToArray();
    }

    private void PaintFog(double centerX, double centerY, string shape)
    {
        if (!CanWorkWithFog || SelectedMap == null)
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info("admin.map.fog.paint.start");
            var response = _api.MapSceneFogPaint(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "brushMode", FirstNonEmpty(FogBrushMode, FogBrushModeIds.Reveal) },
                { "shape", FirstNonEmpty(shape, FogShapeIds.Cell) },
                { "centerX", Math.Max(0d, centerX) },
                { "centerY", Math.Max(0d, centerY) },
                { "widthMeters", Math.Max(1, FogBrushWidthMeters) },
                { "heightMeters", Math.Max(1, FogBrushHeightMeters) },
                { "radiusMeters", Math.Max(1, FogBrushRadiusMeters) },
                { "cellSizeMeters", Math.Max(1, FogCellSizeMeters) }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                ClientLogService.Instance.Warn($"admin.map.fog.paint.error message={response.Message}");
                return;
            }

            ApplyFogPayload(AsMap(Get(response.Payload, "fog")));
            RebuildCanvas();
            ClientLogService.Instance.Info("admin.map.fog.paint.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.fog.paint.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ExecuteFogFill(string state, string logEvent)
    {
        if (!CanWorkWithFog || SelectedMap == null) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneFogFill(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "state", state }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            ApplyFogPayload(AsMap(Get(response.Payload, "fog")));
            RebuildCanvas();
            ClientLogService.Instance.Info(logEvent);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.fog.fill.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ExecuteFogClear(string clearMode)
    {
        if (!CanWorkWithFog || SelectedMap == null) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapSceneFogClear(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "clearMode", clearMode }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            ApplyFogPayload(AsMap(Get(response.Payload, "fog")));
            RebuildCanvas();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"admin.map.fog.clear.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static List<MapFogCellRange> SubtractFogRanges(IEnumerable<MapFogCellRange> source, MapFogCellRange cut)
    {
        var result = new List<MapFogCellRange>();
        foreach (var current in source)
        {
            if (!FogIntersects(current, cut))
            {
                result.Add(CloneFogRange(current));
                continue;
            }

            var overlapFromX = Math.Max(current.FromX, cut.FromX);
            var overlapToX = Math.Min(current.ToX, cut.ToX);
            var overlapFromY = Math.Max(current.FromY, cut.FromY);
            var overlapToY = Math.Min(current.ToY, cut.ToY);

            if (current.FromY <= overlapFromY - 1)
                result.Add(new MapFogCellRange { FromX = current.FromX, ToX = current.ToX, FromY = current.FromY, ToY = overlapFromY - 1 });
            if (overlapToY + 1 <= current.ToY)
                result.Add(new MapFogCellRange { FromX = current.FromX, ToX = current.ToX, FromY = overlapToY + 1, ToY = current.ToY });
            if (current.FromX <= overlapFromX - 1)
                result.Add(new MapFogCellRange { FromX = current.FromX, ToX = overlapFromX - 1, FromY = overlapFromY, ToY = overlapToY });
            if (overlapToX + 1 <= current.ToX)
                result.Add(new MapFogCellRange { FromX = overlapToX + 1, ToX = current.ToX, FromY = overlapFromY, ToY = overlapToY });
        }

        return result.Where(IsValidFogRange).Select(CloneFogRange).ToList();
    }

    private static bool FogIntersects(MapFogCellRange left, MapFogCellRange right)
    {
        return left.FromX <= right.ToX
            && left.ToX >= right.FromX
            && left.FromY <= right.ToY
            && left.ToY >= right.FromY;
    }

    private static bool IsValidFogRange(MapFogCellRange range)
    {
        return range != null && range.FromX <= range.ToX && range.FromY <= range.ToY;
    }

    private static MapFogCellRange CloneFogRange(MapFogCellRange range)
    {
        return new MapFogCellRange
        {
            FromX = range.FromX,
            FromY = range.FromY,
            ToX = range.ToX,
            ToY = range.ToY
        };
    }

    private static IReadOnlyCollection<MapFogCellRange> ReadFogRanges(object? payload)
    {
        var result = new List<MapFogCellRange>();
        foreach (var map in Dictionaries(payload))
        {
            result.Add(new MapFogCellRange
            {
                FromX = Int(Get(map, "fromX"), 0),
                FromY = Int(Get(map, "fromY"), 0),
                ToX = Int(Get(map, "toX"), 0),
                ToY = Int(Get(map, "toY"), 0)
            });
        }

        return result.Where(IsValidFogRange).Select(CloneFogRange).ToArray();
    }

    private bool ConfirmFogWideAction()
    {
        var confirmation = MessageBox.Show(
            "Это действие изменит видимость карты для игроков. Продолжить?",
            "Подтверждение действия",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return confirmation == MessageBoxResult.Yes;
    }

    private static bool Flag(IReadOnlyCollection<Dictionary<string, object>> flags, string name)
    {
        foreach (var flag in flags)
        {
            if (string.Equals(Str(Get(flag, "name")), name, StringComparison.OrdinalIgnoreCase))
                return Bool(Get(flag, "effectiveValue"));
        }

        return false;
    }

    private static bool Bool(object? value)
    {
        if (value is bool boolValue) return boolValue;
        return bool.TryParse(Convert.ToString(value), out var parsed) && parsed;
    }

    private static object? Get(IDictionary<string, object>? map, string key)
    {
        if (map == null || key == null) return null;
        return map.TryGetValue(key, out var value) ? value : null;
    }

    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static long Long(object? value, long fallback) => long.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;

    private static Dictionary<string, object> AsMap(object? value)
    {
        if (value is Dictionary<string, object> direct) return direct;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                result[key] = entry.Value!;
            }

            return result;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Dictionary<string, object>> Dictionaries(object? value)
    {
        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                var map = AsMap(item);
                if (map.Count > 0) yield return map;
            }
        }
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class SceneMapListUiItem : ViewModelBase
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private int _widthMeters = 2000;
    private int _heightMeters = 2000;
    private int _gridCellSizeMeters = 25;
    private bool _showGrid = true;
    private bool _showCoordinates = true;
    private int _markerCount;
    private bool _fogEnabled;
    private bool _isActive;

    public string MapId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string SpaceNodeId { get; set; } = string.Empty;
    public bool Archived { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int MarkerCount
    {
        get => _markerCount;
        set { if (_markerCount != value) { _markerCount = value; Notify(); Notify(nameof(Label)); } }
    }

    public bool FogEnabled
    {
        get => _fogEnabled;
        set { if (_fogEnabled != value) { _fogEnabled = value; Notify(); Notify(nameof(Label)); } }
    }

    public bool IsActive
    {
        get => _isActive;
        set { if (_isActive != value) { _isActive = value; Notify(); Notify(nameof(Label)); } }
    }

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; Notify(); Notify(nameof(Label)); } }
    }

    public string Description
    {
        get => _description;
        set { if (_description != value) { _description = value; Notify(); Notify(nameof(Label)); } }
    }

    public int WidthMeters
    {
        get => _widthMeters;
        set { if (_widthMeters != value) { _widthMeters = value; Notify(); Notify(nameof(Label)); } }
    }

    public int HeightMeters
    {
        get => _heightMeters;
        set { if (_heightMeters != value) { _heightMeters = value; Notify(); Notify(nameof(Label)); } }
    }

    public int GridCellSizeMeters
    {
        get => _gridCellSizeMeters;
        set { if (_gridCellSizeMeters != value) { _gridCellSizeMeters = value; Notify(); Notify(nameof(Label)); } }
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set { if (_showGrid != value) { _showGrid = value; Notify(); } }
    }

    public bool ShowCoordinates
    {
        get => _showCoordinates;
        set { if (_showCoordinates != value) { _showCoordinates = value; Notify(); } }
    }

    public string Label => $"{(IsActive ? "★ " : string.Empty)}{Name} ({WidthMeters}×{HeightMeters}м; шаг {GridCellSizeMeters}м; маркеры {MarkerCount}; туман {(FogEnabled ? "вкл" : "выкл")})";

    public void Apply(IDictionary<string, object> payload)
    {
        Name = FirstNonEmpty(Str(Get(payload, "name")), Name);
        Description = Str(Get(payload, "description"));
        WidthMeters = Int(Get(payload, "widthMeters"), WidthMeters);
        HeightMeters = Int(Get(payload, "heightMeters"), HeightMeters);
        GridCellSizeMeters = Int(Get(payload, "gridCellSizeMeters"), GridCellSizeMeters);
        ShowGrid = Bool(Get(payload, "showGrid"), ShowGrid);
        ShowCoordinates = Bool(Get(payload, "showCoordinates"), ShowCoordinates);
        MarkerCount = Int(Get(payload, "markerCount"), MarkerCount);
        FogEnabled = Bool(Get(payload, "fogEnabled"), FogEnabled);
        IsActive = Bool(Get(payload, "isActive"), IsActive);
    }

    public static SceneMapListUiItem From(IDictionary<string, object> payload)
    {
        var item = new SceneMapListUiItem
        {
            MapId = Str(Get(payload, "mapId")),
            CampaignId = Str(Get(payload, "campaignId")),
            RuleSetId = Str(Get(payload, "ruleSetId")),
            SpaceNodeId = Str(Get(payload, "spaceNodeId")),
            Archived = Bool(Get(payload, "archived"), false),
            UpdatedAtUtc = Date(Get(payload, "updatedAtUtc")),
            MarkerCount = Int(Get(payload, "markerCount"), 0),
            FogEnabled = Bool(Get(payload, "fogEnabled"), false),
            IsActive = Bool(Get(payload, "isActive"), false)
        };
        item.Apply(payload);
        return item;
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static DateTime Date(object? value) => value is DateTime dt ? dt : DateTime.MinValue;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class SceneMarkerUiItem : ViewModelBase
{
    private string _name = string.Empty;
    private string _markerType = "custom";
    private double _x;
    private double _y;
    private double _pixelX;
    private double _pixelY;
    private bool _isSelected;

    public string MarkerId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; Notify(); Notify(nameof(Label)); } }
    }

    public string MarkerType
    {
        get => _markerType;
        set { if (_markerType != value) { _markerType = value; Notify(); Notify(nameof(Label)); } }
    }

    public double X
    {
        get => _x;
        set { if (Math.Abs(_x - value) > 0.0001) { _x = value; Notify(); Notify(nameof(Label)); } }
    }

    public double Y
    {
        get => _y;
        set { if (Math.Abs(_y - value) > 0.0001) { _y = value; Notify(); Notify(nameof(Label)); } }
    }

    public double PixelX
    {
        get => _pixelX;
        set { if (Math.Abs(_pixelX - value) > 0.0001) { _pixelX = value; Notify(); } }
    }

    public double PixelY
    {
        get => _pixelY;
        set { if (Math.Abs(_pixelY - value) > 0.0001) { _pixelY = value; Notify(); } }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; Notify(); } }
    }

    public string MarkerTypeDisplay => MarkerType switch
    {
        "player_character" => "Персонаж",
        "npc" => "NPC",
        "companion" => "Компаньон",
        "enemy" => "Враг",
        "neutral" => "Нейтральный",
        "point_of_interest" => "Точка интереса",
        "entrance" => "Вход",
        "exit" => "Выход",
        "cover" => "Укрытие",
        "objective" => "Цель",
        "hazard" => "Опасность",
        "item" => "Предмет",
        "vehicle" => "Техника",
        _ => "Другое"
    };

    public string Label => $"{Name} [{MarkerTypeDisplay}] X={X:0.##}, Y={Y:0.##}";

    public void Apply(IDictionary<string, object> payload)
    {
        Name = FirstNonEmpty(Str(Get(payload, "name")), Name);
        MarkerType = FirstNonEmpty(Str(Get(payload, "markerType")), MarkerType);
        X = Double(Get(payload, "x"), X);
        Y = Double(Get(payload, "y"), Y);
        IconKey = Str(Get(payload, "iconKey"));
        ColorKey = Str(Get(payload, "colorKey"));
        IsPlayerVisible = Bool(Get(payload, "isPlayerVisible"), IsPlayerVisible);
        LinkedEntityType = Str(Get(payload, "linkedEntityType"));
        LinkedEntityId = Str(Get(payload, "linkedEntityId"));
        CardTitle = Str(Get(payload, "cardTitle"));
        CardDescription = Str(Get(payload, "cardDescription"));
        PublicNotes = Str(Get(payload, "publicNotes"));
        GMNotes = Str(Get(payload, "gmNotes"));
    }

    public static SceneMarkerUiItem From(IDictionary<string, object> payload)
    {
        var marker = new SceneMarkerUiItem
        {
            MarkerId = Str(Get(payload, "markerId")),
            MapId = Str(Get(payload, "mapId")),
            CampaignId = Str(Get(payload, "campaignId"))
        };
        marker.Apply(payload);
        return marker;
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class SceneMarkerBindingUiItem
{
    public string BindingId { get; set; } = string.Empty;
    public string MarkerId { get; set; } = string.Empty;
    public string BindingType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string Visibility { get; set; } = string.Empty;

    public string Label => string.IsNullOrWhiteSpace(DisplayName)
        ? $"{BindingType}: {EntityId}"
        : $"{BindingType}: {DisplayName}";

    public static SceneMarkerBindingUiItem From(IDictionary<string, object> payload)
    {
        return new SceneMarkerBindingUiItem
        {
            BindingId = Str(Get(payload, "bindingId")),
            MarkerId = Str(Get(payload, "markerId")),
            BindingType = Str(Get(payload, "bindingType")),
            EntityId = Str(Get(payload, "entityId")),
            DisplayName = Str(Get(payload, "displayName")),
            IsPrimary = Bool(Get(payload, "isPrimary"), false),
            Visibility = Str(Get(payload, "visibility"))
        };
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
}

public sealed class MapGridLineUiItem
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
}

public sealed class MapFogOverlayUiItem
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
