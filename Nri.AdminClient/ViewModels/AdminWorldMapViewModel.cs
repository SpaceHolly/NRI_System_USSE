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

namespace Nri.AdminClient.ViewModels;

public sealed class AdminWorldMapViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = string.Empty;
    private string _ruleSetId = string.Empty;
    private string _statusMessage = "Задайте CampaignId и RuleSetId, затем загрузите карты мира.";
    private string _errorMessage = string.Empty;
    private string _warningMessage = string.Empty;
    private bool _isLoading;
    private bool _isWorldMapEnabled;
    private bool _isWorldPainterEnabled;
    private bool _isWorldLayersEnabled;
    private bool _isHeightDepthEnabled;
    private bool _isBiomeEnabled;
    private bool _isPoliticalEnabled;
    private bool _isMarkersEnabled;
    private DateTime _lastRefreshAtUtc;
    private WorldMapUiItem? _selectedMap;
    private WorldMapMarkerUiItem? _selectedMarker;
    private string _newMapName = "Новая карта мира";
    private string _newMapDescription = string.Empty;
    private int _newMapWidthCells = MapRuntimeValidation.WorldDefaultWidthCells;
    private int _newMapHeightCells = MapRuntimeValidation.WorldDefaultHeightCells;
    private double _newMapCellSizeKm = 10d;
    private string _selectedLayerType = WorldMapLayerTypeIds.HeightDepth;
    private string _selectedBrushShape = "cell";
    private string _selectedBrushMode = "set";
    private string _selectedLayerValue = WorldMapHeightDepthCategoryIds.Lowland;
    private string _selectedLayerLabel = string.Empty;
    private int _brushX;
    private int _brushY;
    private int _brushWidth = 1;
    private int _brushHeight = 1;
    private int _brushRadius = 1;
    private double _canvasWidth = 820;
    private double _canvasHeight = 520;
    private string _canvasScaleLabel = "нет данных";
    private int _selectedCellX = -1;
    private int _selectedCellY = -1;
    private string _selectedCellSummary = "Клетка не выбрана.";
    private string _markerName = "Маркер";
    private string _markerType = MapMarkerTypeIds.Location;
    private int _markerCellX;
    private int _markerCellY;
    private double _markerXNormalized = 0.5d;
    private double _markerYNormalized = 0.5d;
    private string _markerLinkedEntityType = string.Empty;
    private string _markerLinkedEntityId = string.Empty;
    private string _markerLinkedEntityDisplayName = string.Empty;
    private string _markerLinkedEntityPublicLabel = string.Empty;
    private bool _markerPlayerVisible = true;
    private string _markerVisibilityMode = MapVisibilityModes.Party;
    private string _markerPublicNotes = string.Empty;
    private string _markerGmNotes = string.Empty;
    private string _markerIconKey = string.Empty;
    private string _markerColorKey = string.Empty;
    private string _markerCardTitle = string.Empty;
    private string _markerCardDescription = string.Empty;

    private readonly Dictionary<string, List<WorldLegendEntryUiItem>> _legendByLayerType = new(StringComparer.OrdinalIgnoreCase);

    public AdminWorldMapViewModel(CommandApi api)
    {
        _api = api;

        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshMapsCommand = new RelayCommand(RefreshMaps);
        SeedMvpCommand = new RelayCommand(SeedMvp);
        CreateMapCommand = new RelayCommand(CreateMap);
        LoadSelectedMapCommand = new RelayCommand(LoadSelectedMap);
        SaveMapSettingsCommand = new RelayCommand(SaveMapSettings);
        ArchiveMapCommand = new RelayCommand(ArchiveSelectedMap);

        PaintLayerCommand = new RelayCommand(PaintLayerFromFields);
        PaintSelectedCellCommand = new RelayCommand(PaintSelectedCell);
        ClearSelectedCellCommand = new RelayCommand(ClearSelectedCell);
        ClearLayerCommand = new RelayCommand(ClearLayer);
        SaveLayerVisibilityCommand = new RelayCommand(SaveLayerVisibility);

        AddMarkerCommand = new RelayCommand(AddMarker);
        MoveMarkerCommand = new RelayCommand(MoveMarker);
        SaveMarkerCommand = new RelayCommand(SaveMarker);
        RemoveMarkerCommand = new RelayCommand(RemoveMarker);

        SelectLayerValueCommand = new RelayCommand<WorldLegendEntryUiItem>(SelectLayerValue);
        ClearErrorCommand = new RelayCommand(() => { ErrorMessage = string.Empty; WarningMessage = string.Empty; });
    }

    public ObservableCollection<WorldMapUiItem> Maps { get; } = new();
    public ObservableCollection<MapGridLineUiItem> GridLines { get; } = new();
    public ObservableCollection<WorldMapCellUiItem> PaintedCells { get; } = new();
    public ObservableCollection<WorldMapMarkerUiItem> Markers { get; } = new();
    public ObservableCollection<WorldLegendEntryUiItem> LegendEntries { get; } = new();
    public ObservableCollection<string> CanvasHints { get; } = new();
    public ObservableCollection<string> LayerTypeOptions { get; } = new()
    {
        WorldMapLayerTypeIds.HeightDepth,
        WorldMapLayerTypeIds.Biome,
        WorldMapLayerTypeIds.Political
    };
    public ObservableCollection<string> BrushShapeOptions { get; } = new() { "cell", "rectangle", "circle" };
    public ObservableCollection<string> BrushModeOptions { get; } = new() { "set", "clear" };
    public ObservableCollection<string> MarkerBindingTypeOptions { get; } = new()
    {
        string.Empty,
        MapMarkerBindingTypeIds.SpaceNode,
        MapMarkerBindingTypeIds.Continent,
        MapMarkerBindingTypeIds.Country,
        MapMarkerBindingTypeIds.CityState,
        MapMarkerBindingTypeIds.Region,
        MapMarkerBindingTypeIds.Location,
        MapMarkerBindingTypeIds.Room,
        MapMarkerBindingTypeIds.Interior,
        MapMarkerBindingTypeIds.Faction,
        MapMarkerBindingTypeIds.Organization,
        MapMarkerBindingTypeIds.Custom
    };

    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshMapsCommand { get; }
    public ICommand SeedMvpCommand { get; }
    public ICommand CreateMapCommand { get; }
    public ICommand LoadSelectedMapCommand { get; }
    public ICommand SaveMapSettingsCommand { get; }
    public ICommand ArchiveMapCommand { get; }
    public ICommand PaintLayerCommand { get; }
    public ICommand PaintSelectedCellCommand { get; }
    public ICommand ClearSelectedCellCommand { get; }
    public ICommand ClearLayerCommand { get; }
    public ICommand SaveLayerVisibilityCommand { get; }
    public ICommand AddMarkerCommand { get; }
    public ICommand MoveMarkerCommand { get; }
    public ICommand SaveMarkerCommand { get; }
    public ICommand RemoveMarkerCommand { get; }
    public ICommand SelectLayerValueCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId
    {
        get => _campaignId;
        set { if (_campaignId != value) { _campaignId = value; Notify(); Notify(nameof(CanLoadMaps)); Notify(nameof(CanCreateMap)); } }
    }

    public string RuleSetId
    {
        get => _ruleSetId;
        set { if (_ruleSetId != value) { _ruleSetId = value; Notify(); Notify(nameof(CanCreateMap)); } }
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
                Notify(nameof(CanEditMap));
                Notify(nameof(CanPaintLayers));
                Notify(nameof(CanEditMarkers));
            }
        }
    }

    public bool IsIdle => !IsLoading;

    public bool IsWorldMapEnabled
    {
        get => _isWorldMapEnabled;
        private set
        {
            if (_isWorldMapEnabled != value)
            {
                _isWorldMapEnabled = value;
                Notify();
                Notify(nameof(IsWorldMapDisabled));
                Notify(nameof(CanLoadMaps));
                Notify(nameof(CanCreateMap));
                Notify(nameof(CanEditMap));
            }
        }
    }

    public bool IsWorldPainterEnabled
    {
        get => _isWorldPainterEnabled;
        private set
        {
            if (_isWorldPainterEnabled != value)
            {
                _isWorldPainterEnabled = value;
                Notify();
                Notify(nameof(CanPaintLayers));
            }
        }
    }

    public bool IsWorldLayersEnabled
    {
        get => _isWorldLayersEnabled;
        private set
        {
            if (_isWorldLayersEnabled != value)
            {
                _isWorldLayersEnabled = value;
                Notify();
                Notify(nameof(CanPaintLayers));
            }
        }
    }

    public bool IsHeightDepthEnabled
    {
        get => _isHeightDepthEnabled;
        private set { if (_isHeightDepthEnabled != value) { _isHeightDepthEnabled = value; Notify(); Notify(nameof(CanPaintCurrentLayer)); } }
    }

    public bool IsBiomeEnabled
    {
        get => _isBiomeEnabled;
        private set { if (_isBiomeEnabled != value) { _isBiomeEnabled = value; Notify(); Notify(nameof(CanPaintCurrentLayer)); } }
    }

    public bool IsPoliticalEnabled
    {
        get => _isPoliticalEnabled;
        private set { if (_isPoliticalEnabled != value) { _isPoliticalEnabled = value; Notify(); Notify(nameof(CanPaintCurrentLayer)); } }
    }

    public bool IsMarkersEnabled
    {
        get => _isMarkersEnabled;
        private set
        {
            if (_isMarkersEnabled != value)
            {
                _isMarkersEnabled = value;
                Notify();
                Notify(nameof(CanEditMarkers));
            }
        }
    }

    public bool IsWorldMapDisabled => !IsWorldMapEnabled;
    public bool CanLoadMaps => IsIdle && !string.IsNullOrWhiteSpace(CampaignId);
    public bool CanCreateMap => IsWorldMapEnabled && IsIdle && !string.IsNullOrWhiteSpace(CampaignId) && !string.IsNullOrWhiteSpace(RuleSetId);
    public bool CanEditMap => IsWorldMapEnabled && IsIdle && SelectedMap != null;
    public bool CanPaintLayers => CanEditMap && IsWorldPainterEnabled && IsWorldLayersEnabled;
    public bool CanPaintCurrentLayer => CanPaintLayers && IsLayerEnabled(SelectedLayerType);
    public bool CanEditMarkers => CanEditMap && IsMarkersEnabled;

    public DateTime LastRefreshAtUtc
    {
        get => _lastRefreshAtUtc;
        private set { if (_lastRefreshAtUtc != value) { _lastRefreshAtUtc = value; Notify(); Notify(nameof(LastRefreshText)); } }
    }

    public string LastRefreshText => LastRefreshAtUtc == default
        ? "ещё не обновлялось"
        : LastRefreshAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public WorldMapUiItem? SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (_selectedMap != value)
            {
                _selectedMap = value;
                Notify();
                Notify(nameof(CanEditMap));
                Notify(nameof(CanPaintLayers));
                Notify(nameof(CanEditMarkers));
                if (value != null)
                {
                    NewMapName = value.Name;
                    NewMapDescription = value.Description;
                    NewMapWidthCells = value.WidthCells;
                    NewMapHeightCells = value.HeightCells;
                    NewMapCellSizeKm = value.CellSizeKm <= 0 ? 10d : value.CellSizeKm;
                }
            }
        }
    }

    public WorldMapMarkerUiItem? SelectedMarker
    {
        get => _selectedMarker;
        set
        {
            if (_selectedMarker != value)
            {
                _selectedMarker = value;
                foreach (var marker in Markers) marker.IsSelected = marker == value;
                Notify();
                if (value != null)
                {
                    MarkerName = value.Name;
                    MarkerType = value.MarkerType;
                    MarkerCellX = value.CellX;
                    MarkerCellY = value.CellY;
                    MarkerXNormalized = value.XNormalized;
                    MarkerYNormalized = value.YNormalized;
                    MarkerLinkedEntityType = value.LinkedEntityType;
                    MarkerLinkedEntityId = value.LinkedEntityId;
                    MarkerLinkedEntityDisplayName = value.LinkedEntityDisplayName;
                    MarkerLinkedEntityPublicLabel = value.LinkedEntityPublicLabel;
                    MarkerPlayerVisible = value.IsPlayerVisible;
                    MarkerVisibilityMode = value.VisibilityMode;
                    MarkerPublicNotes = value.PublicNotes;
                    MarkerGmNotes = value.GMNotes;
                    MarkerIconKey = value.IconKey;
                    MarkerColorKey = value.ColorKey;
                    MarkerCardTitle = value.CardTitle;
                    MarkerCardDescription = value.CardDescription;
                }
            }
        }
    }

    public string NewMapName
    {
        get => _newMapName;
        set { if (_newMapName != value) { _newMapName = value; Notify(); } }
    }

    public string NewMapDescription
    {
        get => _newMapDescription;
        set { if (_newMapDescription != value) { _newMapDescription = value; Notify(); } }
    }

    public int NewMapWidthCells
    {
        get => _newMapWidthCells;
        set { if (_newMapWidthCells != value) { _newMapWidthCells = value; Notify(); } }
    }

    public int NewMapHeightCells
    {
        get => _newMapHeightCells;
        set { if (_newMapHeightCells != value) { _newMapHeightCells = value; Notify(); } }
    }

    public double NewMapCellSizeKm
    {
        get => _newMapCellSizeKm;
        set { if (Math.Abs(_newMapCellSizeKm - value) > 0.0001d) { _newMapCellSizeKm = value; Notify(); } }
    }

    public string SelectedLayerType
    {
        get => _selectedLayerType;
        set
        {
            if (_selectedLayerType != value)
            {
                _selectedLayerType = value;
                Notify();
                Notify(nameof(CanPaintCurrentLayer));
                Notify(nameof(SelectedLayerVisibleToPlayers));
                EnsureDefaultValueForLayer();
                RefreshLegendForLayer();
                RebuildPaintedCells();
            }
        }
    }

    public bool SelectedLayerVisibleToPlayers
    {
        get => GetLayer(SelectedLayerType)?.IsVisibleToPlayers ?? false;
        set
        {
            var layer = GetLayer(SelectedLayerType);
            if (layer == null || layer.IsVisibleToPlayers == value) return;
            layer.IsVisibleToPlayers = value;
            Notify();
        }
    }

    public string SelectedBrushShape
    {
        get => _selectedBrushShape;
        set { if (_selectedBrushShape != value) { _selectedBrushShape = value; Notify(); } }
    }

    public string SelectedBrushMode
    {
        get => _selectedBrushMode;
        set { if (_selectedBrushMode != value) { _selectedBrushMode = value; Notify(); } }
    }

    public string SelectedLayerValue
    {
        get => _selectedLayerValue;
        set { if (_selectedLayerValue != value) { _selectedLayerValue = value; Notify(); } }
    }

    public string SelectedLayerLabel
    {
        get => _selectedLayerLabel;
        set { if (_selectedLayerLabel != value) { _selectedLayerLabel = value; Notify(); } }
    }

    public int BrushX
    {
        get => _brushX;
        set { if (_brushX != value) { _brushX = value; Notify(); } }
    }

    public int BrushY
    {
        get => _brushY;
        set { if (_brushY != value) { _brushY = value; Notify(); } }
    }

    public int BrushWidth
    {
        get => _brushWidth;
        set { if (_brushWidth != value) { _brushWidth = value; Notify(); } }
    }

    public int BrushHeight
    {
        get => _brushHeight;
        set { if (_brushHeight != value) { _brushHeight = value; Notify(); } }
    }

    public int BrushRadius
    {
        get => _brushRadius;
        set { if (_brushRadius != value) { _brushRadius = value; Notify(); } }
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        private set { if (Math.Abs(_canvasWidth - value) > 0.01) { _canvasWidth = value; Notify(); } }
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        private set { if (Math.Abs(_canvasHeight - value) > 0.01) { _canvasHeight = value; Notify(); } }
    }

    public string CanvasScaleLabel
    {
        get => _canvasScaleLabel;
        private set { if (_canvasScaleLabel != value) { _canvasScaleLabel = value; Notify(); } }
    }

    public int SelectedCellX
    {
        get => _selectedCellX;
        private set { if (_selectedCellX != value) { _selectedCellX = value; Notify(); } }
    }

    public int SelectedCellY
    {
        get => _selectedCellY;
        private set { if (_selectedCellY != value) { _selectedCellY = value; Notify(); } }
    }

    public string SelectedCellSummary
    {
        get => _selectedCellSummary;
        private set { if (_selectedCellSummary != value) { _selectedCellSummary = value; Notify(); } }
    }

    public string MarkerSummaryText => Markers.Count == 0
        ? "Маркеров на карте пока нет."
        : "Маркеры: " + string.Join(" · ", Markers.Select(marker => marker.Name));

    public string MarkerName
    {
        get => _markerName;
        set { if (_markerName != value) { _markerName = value; Notify(); } }
    }

    public string MarkerType
    {
        get => _markerType;
        set { if (_markerType != value) { _markerType = value; Notify(); } }
    }

    public int MarkerCellX
    {
        get => _markerCellX;
        set { if (_markerCellX != value) { _markerCellX = value; Notify(); } }
    }

    public int MarkerCellY
    {
        get => _markerCellY;
        set { if (_markerCellY != value) { _markerCellY = value; Notify(); } }
    }

    public double MarkerXNormalized
    {
        get => _markerXNormalized;
        set { if (Math.Abs(_markerXNormalized - value) > 0.0001d) { _markerXNormalized = value; Notify(); } }
    }

    public double MarkerYNormalized
    {
        get => _markerYNormalized;
        set { if (Math.Abs(_markerYNormalized - value) > 0.0001d) { _markerYNormalized = value; Notify(); } }
    }

    public string MarkerLinkedEntityType
    {
        get => _markerLinkedEntityType;
        set { if (_markerLinkedEntityType != value) { _markerLinkedEntityType = value; Notify(); } }
    }

    public string MarkerLinkedEntityId
    {
        get => _markerLinkedEntityId;
        set { if (_markerLinkedEntityId != value) { _markerLinkedEntityId = value; Notify(); } }
    }

    public string MarkerLinkedEntityDisplayName
    {
        get => _markerLinkedEntityDisplayName;
        set { if (_markerLinkedEntityDisplayName != value) { _markerLinkedEntityDisplayName = value; Notify(); } }
    }

    public string MarkerLinkedEntityPublicLabel
    {
        get => _markerLinkedEntityPublicLabel;
        set { if (_markerLinkedEntityPublicLabel != value) { _markerLinkedEntityPublicLabel = value; Notify(); } }
    }

    public bool MarkerPlayerVisible
    {
        get => _markerPlayerVisible;
        set { if (_markerPlayerVisible != value) { _markerPlayerVisible = value; Notify(); } }
    }

    public string MarkerVisibilityMode
    {
        get => _markerVisibilityMode;
        set { if (_markerVisibilityMode != value) { _markerVisibilityMode = value; Notify(); } }
    }

    public string MarkerPublicNotes
    {
        get => _markerPublicNotes;
        set { if (_markerPublicNotes != value) { _markerPublicNotes = value; Notify(); } }
    }

    public string MarkerGmNotes
    {
        get => _markerGmNotes;
        set { if (_markerGmNotes != value) { _markerGmNotes = value; Notify(); } }
    }

    public string MarkerIconKey
    {
        get => _markerIconKey;
        set { if (_markerIconKey != value) { _markerIconKey = value; Notify(); } }
    }

    public string MarkerColorKey
    {
        get => _markerColorKey;
        set { if (_markerColorKey != value) { _markerColorKey = value; Notify(); } }
    }

    public string MarkerCardTitle
    {
        get => _markerCardTitle;
        set { if (_markerCardTitle != value) { _markerCardTitle = value; Notify(); } }
    }

    public string MarkerCardDescription
    {
        get => _markerCardDescription;
        set { if (_markerCardDescription != value) { _markerCardDescription = value; Notify(); } }
    }

    public string SelectedLayerDisplayName => LayerDisplayName(SelectedLayerType);

    public void RefreshFlags()
    {
        try
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            var items = response.Status == ResponseStatus.Ok ? ExtractFeatureFlagItems(response.Payload) : Array.Empty<object>();
            if (items.Length == 0)
            {
                var listResponse = _api.FeatureFlagsAdminList();
                if (listResponse.Status == ResponseStatus.Ok)
                {
                    response = listResponse;
                    items = ExtractFeatureFlagItems(listResponse.Payload);
                }
            }

            if (response.Status != ResponseStatus.Ok || items.Length == 0)
            {
                EnsureOk(response, "Не удалось загрузить флаги функций.");
                if (items.Length == 0)
                {
                    ErrorMessage = "Снимок функций и модулей вернул пустые или неожиданные данные.";
                    StatusMessage = "Карта мира недоступна: флаги функций не загружены.";
                }
                return;
            }

            bool mapSystem = false;
            bool space = false;
            bool world = false;
            bool painter = false;
            bool layers = false;
            bool height = false;
            bool biome = false;
            bool political = false;
            bool markers = false;

            foreach (var item in items)
            {
                var map = Dict(item);
                if (map == null) continue;
                var name = Str(Get(map, "name"), Get(map, "key"), Get(map, "flagName"));
                var enabled = Bool(Get(map, "effectiveValue"),
                    Bool(Get(map, "effective"), Bool(Get(map, "enabled"), Bool(Get(map, "value"), false))));
                if (name == nameof(MapFeatureFlags.UseMapSystemV1)) mapSystem = enabled;
                else if (name == nameof(MapFeatureFlags.UseSpaceHierarchyV1)) space = enabled;
                else if (name == nameof(MapFeatureFlags.UseWorldMapV1)) world = enabled;
                else if (name == nameof(MapFeatureFlags.UseWorldMapPainterMvp)) painter = enabled;
                else if (name == nameof(MapFeatureFlags.UseWorldMapLayers)) layers = enabled;
                else if (name == nameof(MapFeatureFlags.UseWorldMapHeightDepthLayer)) height = enabled;
                else if (name == nameof(MapFeatureFlags.UseWorldMapBiomeLayer)) biome = enabled;
                else if (name == nameof(MapFeatureFlags.UseWorldMapPoliticalLayer)) political = enabled;
                else if (name == nameof(MapFeatureFlags.UseWorldMapMarkers)) markers = enabled;
            }

            IsWorldMapEnabled = mapSystem && space && world;
            IsWorldPainterEnabled = painter;
            IsWorldLayersEnabled = layers;
            IsHeightDepthEnabled = height;
            IsBiomeEnabled = biome;
            IsPoliticalEnabled = political;
            IsMarkersEnabled = markers;

            WarningMessage = IsWorldMapEnabled
                ? string.Empty
                : "Карта мира выключена флагами функций.";

            StatusMessage = IsWorldMapEnabled
                ? "Флаги функций карты мира активны."
                : "World Map недоступна: включите UseMapSystemV1, UseSpaceHierarchyV1 и UseWorldMapV1.";

            Notify(nameof(CanPaintCurrentLayer));
        }
        catch (Exception ex)
        {
            WarningMessage = $"Снимок функций и модулей недоступен: {ex.Message}";
        }
    }

    public void RefreshMaps()
    {
        if (!CanLoadMaps) return;
        RunBusy("Загрузка карт мира...", RefreshMapsCore);
    }

    private void RefreshMapsCore()
    {
        var response = _api.MapWorldList(new Dictionary<string, object>
        {
            { "campaignId", CampaignId },
            { "includeArchived", false }
        });
        if (!EnsureOk(response, "Не удалось загрузить карты мира.")) return;

        IsWorldMapEnabled = true;
        var previousMapId = SelectedMap?.MapId ?? string.Empty;
        Maps.Clear();
        var items = Arr(response.Payload.TryGetValue("items", out var raw) ? raw : null);
        foreach (var item in items)
        {
            var map = Dict(item);
            if (map == null) continue;
            Maps.Add(new WorldMapUiItem
            {
                MapId = Str(Get(map, "mapId")),
                Name = Str(Get(map, "name")),
                Description = Str(Get(map, "description")),
                WidthCells = Int(Get(map, "widthCells"), MapRuntimeValidation.WorldDefaultWidthCells),
                HeightCells = Int(Get(map, "heightCells"), MapRuntimeValidation.WorldDefaultHeightCells),
                CellSizeKm = Dbl(Get(map, "cellSizeKm"), 0d),
                ProjectionMode = Str(Get(map, "projectionMode"), WorldMapProjectionModeIds.FlatGrid),
                VisibilityMode = Str(Get(map, "visibilityMode"), MapVisibilityModes.Party),
                IsPlayerVisible = Bool(Get(map, "isPlayerVisible"), true),
                MarkerCount = Int(Get(map, "markerCount"), 0),
                UpdatedAtUtc = Date(Get(map, "updatedAtUtc"))
            });
        }

        if (Maps.Count == 0)
        {
            SelectedMap = null;
            ClearMapVisuals();
            StatusMessage = "Карты мира пока не созданы.";
        }
        else
        {
            SelectedMap = Maps.FirstOrDefault(x => string.Equals(x.MapId, previousMapId, StringComparison.OrdinalIgnoreCase)) ?? Maps[0];
            StatusMessage = $"Загружено карт мира: {Maps.Count}.";
        }

        LastRefreshAtUtc = DateTime.UtcNow;
        ClientLogService.Instance.Info("admin.map.world.load.done");
    }

    private void SeedMvp()
    {
        RunBusy("Подготовка MVP-карты мира...", () =>
        {
            if (!IsWorldMapEnabled)
                RefreshFlags();

            var response = _api.WorldMapAdminSeedMvp(new Dictionary<string, object>());
            if (!EnsureOk(response, "Не удалось подготовить MVP-карту мира.")) return;
            IsWorldMapEnabled = true;
            IsWorldPainterEnabled = true;
            IsWorldLayersEnabled = true;
            IsHeightDepthEnabled = true;
            IsBiomeEnabled = true;
            IsPoliticalEnabled = true;
            IsMarkersEnabled = true;
            CampaignId = "dev-campaign-core";
            RuleSetId = "fantasy_nri_default";
            RefreshMapsCore();
            var seededMapId = Str(Get(response.Payload, "mapId"), "nri_world_map_mvp_01455");
            var seededMapName = Str(Get(response.Payload, "mapName"), "Карта мира NRI");
            EnsureSeededMapItem(seededMapId, seededMapName);
            SelectedMap = Maps.FirstOrDefault(x => string.Equals(x.MapId, seededMapId, StringComparison.OrdinalIgnoreCase)) ?? SelectedMap;
            if (SelectedMap != null) LoadSelectedMapCore();
            WarningMessage = string.Empty;
            StatusMessage = "MVP-карта мира подготовлена.";
        });
    }

    private void EnsureSeededMapItem(string mapId, string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapId)) return;
        if (Maps.Any(x => string.Equals(x.MapId, mapId, StringComparison.OrdinalIgnoreCase))) return;

        Maps.Insert(0, new WorldMapUiItem
        {
            MapId = mapId,
            Name = string.IsNullOrWhiteSpace(mapName) ? "Карта мира NRI" : mapName,
            Description = "Структурная MVP-карта мира для Foundation 0.14.55.",
            WidthCells = 120,
            HeightCells = 80,
            CellSizeKm = 10d,
            ProjectionMode = WorldMapProjectionModeIds.FlatGrid,
            VisibilityMode = MapVisibilityModes.Public,
            IsPlayerVisible = true,
            MarkerCount = 0,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    public void LoadSelectedMap()
    {
        if (!CanEditMap || SelectedMap == null) return;
        RunBusy("Загрузка карты мира...", LoadSelectedMapCore);
    }

    private void LoadSelectedMapCore()
    {
        if (SelectedMap == null) return;
        var response = _api.MapWorldGet(new Dictionary<string, object>
        {
            { "mapId", SelectedMap.MapId },
            { "includeLayers", true },
            { "includeMarkers", true }
        });
        if (!EnsureOk(response, "Не удалось загрузить выбранную карту мира.")) return;

        var mapPayload = Dict(Get(response.Payload, "map"));
        if (mapPayload != null)
        {
            SelectedMap.Name = Str(Get(mapPayload, "name"), SelectedMap.Name);
            SelectedMap.Description = Str(Get(mapPayload, "description"), SelectedMap.Description);
            SelectedMap.WidthCells = Int(Get(mapPayload, "widthCells"), SelectedMap.WidthCells);
            SelectedMap.HeightCells = Int(Get(mapPayload, "heightCells"), SelectedMap.HeightCells);
            SelectedMap.CellSizeKm = Dbl(Get(mapPayload, "cellSizeKm"), SelectedMap.CellSizeKm);
            SelectedMap.ProjectionMode = Str(Get(mapPayload, "projectionMode"), SelectedMap.ProjectionMode);
            SelectedMap.VisibilityMode = Str(Get(mapPayload, "visibilityMode"), SelectedMap.VisibilityMode);
            SelectedMap.IsPlayerVisible = Bool(Get(mapPayload, "isPlayerVisible"), SelectedMap.IsPlayerVisible);
            SelectedMap.UpdatedAtUtc = Date(Get(mapPayload, "updatedAtUtc"));
        }

        ParseLegend(response.Payload);
        ParseLayers(response.Payload);
        ParseMarkers(response.Payload);
        RebuildCanvas();
        RefreshLegendForLayer();
        SelectedMarker = Markers.FirstOrDefault();
        LastRefreshAtUtc = DateTime.UtcNow;
        StatusMessage = $"Карта '{SelectedMap.Name}' загружена.";
        ClientLogService.Instance.Info("admin.map.world.get.done");
    }

    public void PaintAtPixel(double pixelX, double pixelY)
    {
        if (!CanPaintCurrentLayer || SelectedMap == null) return;
        if (CellPixelSize <= 0) return;

        var cellX = (int)Math.Floor(pixelX / CellPixelSize);
        var cellY = (int)Math.Floor(pixelY / CellPixelSize);
        if (!MapRuntimeValidation.IsWorldCellInsideBounds(cellX, cellY, SelectedMap.WidthCells, SelectedMap.HeightCells)) return;

        SelectCell(cellX, cellY);
        PaintSelectedCell();
    }

    public void SelectMarkerFromUi(WorldMapMarkerUiItem marker)
    {
        SelectedMarker = marker;
        ClientLogService.Instance.Info("admin.map.world.marker.selected");
    }

    private void CreateMap()
    {
        if (!CanCreateMap) return;
        RunBusy("Создание карты мира...", () =>
        {
            var response = _api.MapWorldCreate(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "ruleSetId", RuleSetId },
                { "name", NewMapName },
                { "description", NewMapDescription },
                { "widthCells", NewMapWidthCells },
                { "heightCells", NewMapHeightCells },
                { "cellSizeKm", NewMapCellSizeKm },
                { "projectionMode", WorldMapProjectionModeIds.FlatGrid },
                { "coordinateMode", WorldMapCoordinateModeIds.Grid },
                { "visibilityMode", MapVisibilityModes.Party },
                { "isPlayerVisible", true }
            });
            if (!EnsureOk(response, "Не удалось создать карту мира.")) return;
            RefreshMaps();

            var createdMapId = Str(Get(response.Payload, "mapId"));
            if (!string.IsNullOrWhiteSpace(createdMapId))
                SelectedMap = Maps.FirstOrDefault(x => string.Equals(x.MapId, createdMapId, StringComparison.OrdinalIgnoreCase)) ?? SelectedMap;
            LoadSelectedMap();
        });
    }

    private void SaveMapSettings()
    {
        if (!CanEditMap || SelectedMap == null) return;
        RunBusy("Сохранение настроек карты мира...", () =>
        {
            var response = _api.MapWorldUpdateSettings(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "name", NewMapName },
                { "description", NewMapDescription },
                { "cellSizeKm", NewMapCellSizeKm },
                { "visibilityMode", SelectedMap.VisibilityMode },
                { "isPlayerVisible", SelectedMap.IsPlayerVisible }
            });
            if (!EnsureOk(response, "Не удалось сохранить настройки карты мира.")) return;
            LoadSelectedMap();
            RefreshMaps();
        });
    }

    private void ArchiveSelectedMap()
    {
        if (!CanEditMap || SelectedMap == null) return;
        if (MessageBox.Show(
                "Архивировать выбранную карту мира? Карта останется в хранилище, но будет скрыта из активного списка.",
                "Архивация карты мира",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        RunBusy("Архивация карты мира...", () =>
        {
            var response = _api.MapWorldArchive(new Dictionary<string, object> { { "mapId", SelectedMap.MapId } });
            if (!EnsureOk(response, "Не удалось архивировать карту мира.")) return;
            RefreshMaps();
            if (SelectedMap != null && SelectedMap.IsArchived)
                SelectedMap = Maps.FirstOrDefault();
            if (SelectedMap != null) LoadSelectedMap();
        });
    }

    private void PaintLayerFromFields()
    {
        if (!CanPaintCurrentLayer || SelectedMap == null) return;
        RunBusy("Покраска слоя карты мира...", () =>
        {
            var response = _api.MapWorldLayerPaint(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "layerType", SelectedLayerType },
                { "brushShape", SelectedBrushShape },
                { "brushMode", SelectedBrushMode },
                { "x", BrushX },
                { "y", BrushY },
                { "widthCells", Math.Max(1, BrushWidth) },
                { "heightCells", Math.Max(1, BrushHeight) },
                { "radiusCells", Math.Max(1, BrushRadius) },
                { "value", SelectedLayerValue },
                { "label", SelectedLayerLabel }
            });
            if (!EnsureOk(response, "Не удалось применить кисть к слою карты мира.")) return;
            ApplyLayerPayload(Dict(Get(response.Payload, "layer")));
            RebuildPaintedCells();
            LastRefreshAtUtc = DateTime.UtcNow;
            StatusMessage = "Слой обновлён.";
            ClientLogService.Instance.Info("admin.map.world.paint");
        });
    }

    private void PaintSelectedCell()
    {
        if (!CanPaintCurrentLayer || SelectedMap == null || SelectedCellX < 0 || SelectedCellY < 0) return;
        RunBusy("Обновление клетки слоя...", () =>
        {
            var response = _api.MapWorldLayerUpdateCell(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "layerType", SelectedLayerType },
                { "cellX", SelectedCellX },
                { "cellY", SelectedCellY },
                { "value", SelectedLayerValue },
                { "label", SelectedLayerLabel }
            });
            if (!EnsureOk(response, "Не удалось обновить клетку слоя.")) return;
            ApplyLayerPayload(Dict(Get(response.Payload, "layer")));
            RebuildPaintedCells();
        });
    }

    private void ClearSelectedCell()
    {
        if (!CanPaintCurrentLayer || SelectedMap == null || SelectedCellX < 0 || SelectedCellY < 0) return;
        RunBusy("Очистка клетки слоя...", () =>
        {
            var response = _api.MapWorldLayerPaint(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "layerType", SelectedLayerType },
                { "brushShape", "cell" },
                { "brushMode", "clear" },
                { "x", SelectedCellX },
                { "y", SelectedCellY },
                { "widthCells", 1 },
                { "heightCells", 1 },
                { "radiusCells", 1 },
                { "value", string.Empty },
                { "label", string.Empty }
            });
            if (!EnsureOk(response, "Не удалось очистить клетку слоя.")) return;
            ApplyLayerPayload(Dict(Get(response.Payload, "layer")));
            RebuildPaintedCells();
        });
    }

    private void ClearLayer()
    {
        if (!CanPaintCurrentLayer || SelectedMap == null) return;
        if (MessageBox.Show(
                $"Очистить слой '{LayerDisplayName(SelectedLayerType)}' целиком?",
                "Очистка слоя",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        RunBusy("Очистка слоя карты мира...", () =>
        {
            var response = _api.MapWorldLayerClear(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "layerType", SelectedLayerType }
            });
            if (!EnsureOk(response, "Не удалось очистить слой карты мира.")) return;
            ApplyLayerPayload(Dict(Get(response.Payload, "layer")));
            RebuildPaintedCells();
        });
    }

    private void SaveLayerVisibility()
    {
        if (!CanPaintLayers || SelectedMap == null) return;
        var layer = GetLayer(SelectedLayerType);
        if (layer == null) return;
        RunBusy("Сохранение видимости слоя...", () =>
        {
            var response = _api.MapWorldLayerSetVisibility(new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "layerType", SelectedLayerType },
                { "isVisibleToGM", layer.IsVisibleToGM },
                { "isVisibleToPlayers", layer.IsVisibleToPlayers },
                { "opacity", layer.Opacity }
            });
            if (!EnsureOk(response, "Не удалось сохранить видимость слоя.")) return;
            ApplyLayerPayload(Dict(Get(response.Payload, "layer")));
            RebuildPaintedCells();
        });
    }

    private void AddMarker()
    {
        if (!CanEditMarkers || SelectedMap == null) return;
        RunBusy("Добавление маркера карты мира...", () =>
        {
            var addingFromSelectedMarker = SelectedMarker != null
                && string.Equals(MarkerName, SelectedMarker.Name, StringComparison.Ordinal);
            var cardTitle = addingFromSelectedMarker
                ? FirstNonEmpty(MarkerCardTitle, MarkerName)
                : MarkerName;
            var cardDescription = addingFromSelectedMarker
                ? FirstNonEmpty(MarkerCardDescription, MarkerPublicNotes)
                : MarkerPublicNotes;
            var request = new Dictionary<string, object>
            {
                { "mapId", SelectedMap.MapId },
                { "name", MarkerName },
                { "markerType", MarkerType },
                { "cellX", MarkerCellX },
                { "cellY", MarkerCellY },
                { "xNormalized", MarkerXNormalized },
                { "yNormalized", MarkerYNormalized },
                { "linkedEntityType", MarkerLinkedEntityType },
                { "linkedEntityId", MarkerLinkedEntityId },
                { "linkedEntityDisplayName", MarkerLinkedEntityDisplayName },
                { "linkedEntityPublicLabel", MarkerLinkedEntityPublicLabel },
                { "isPlayerVisible", MarkerPlayerVisible },
                { "visibilityMode", MarkerVisibilityMode },
                { "publicNotes", MarkerPublicNotes },
                { "gmNotes", MarkerGmNotes },
                { "iconKey", MarkerIconKey },
                { "colorKey", MarkerColorKey },
                { "cardTitle", cardTitle },
                { "cardDescription", cardDescription }
            };
            EnrichMarkerBindingRequest(request);

            var response = _api.MapWorldMarkerAdd(request);
            if (!EnsureOk(response, "Не удалось добавить маркер карты мира.")) return;
            var markerPayload = Dict(Get(response.Payload, "marker"));
            if (markerPayload != null)
            {
                var marker = ToMarker(markerPayload);
                Markers.Add(marker);
                SelectedMarker = marker;
                Notify(nameof(MarkerSummaryText));
            }

            RebuildMarkersOnCanvas();
        });
    }

    private void MoveMarker()
    {
        if (!CanEditMarkers || SelectedMarker == null) return;
        RunBusy("Перемещение маркера...", () =>
        {
            var response = _api.MapWorldMarkerMove(new Dictionary<string, object>
            {
                { "markerId", SelectedMarker.MarkerId },
                { "cellX", MarkerCellX },
                { "cellY", MarkerCellY },
                { "xNormalized", MarkerXNormalized },
                { "yNormalized", MarkerYNormalized }
            });
            if (!EnsureOk(response, "Не удалось переместить маркер.")) return;
            var markerPayload = Dict(Get(response.Payload, "marker"));
            if (markerPayload != null)
            {
                ApplyMarkerUpdate(SelectedMarker, markerPayload);
                Notify(nameof(MarkerSummaryText));
            }
            RebuildMarkersOnCanvas();
        });
    }

    private void SaveMarker()
    {
        if (!CanEditMarkers || SelectedMarker == null) return;
        RunBusy("Сохранение маркера...", () =>
        {
            var request = new Dictionary<string, object>
            {
                { "markerId", SelectedMarker.MarkerId },
                { "name", MarkerName },
                { "markerType", MarkerType },
                { "cellX", MarkerCellX },
                { "cellY", MarkerCellY },
                { "xNormalized", MarkerXNormalized },
                { "yNormalized", MarkerYNormalized },
                { "linkedEntityType", MarkerLinkedEntityType },
                { "linkedEntityId", MarkerLinkedEntityId },
                { "linkedEntityDisplayName", MarkerLinkedEntityDisplayName },
                { "linkedEntityPublicLabel", MarkerLinkedEntityPublicLabel },
                { "isPlayerVisible", MarkerPlayerVisible },
                { "visibilityMode", MarkerVisibilityMode },
                { "publicNotes", MarkerPublicNotes },
                { "gmNotes", MarkerGmNotes },
                { "iconKey", MarkerIconKey },
                { "colorKey", MarkerColorKey },
                { "cardTitle", MarkerCardTitle },
                { "cardDescription", MarkerCardDescription }
            };
            EnrichMarkerBindingRequest(request);

            var response = _api.MapWorldMarkerUpdate(request);
            if (!EnsureOk(response, "Не удалось сохранить маркер.")) return;
            var markerPayload = Dict(Get(response.Payload, "marker"));
            if (markerPayload != null)
            {
                ApplyMarkerUpdate(SelectedMarker, markerPayload);
                Notify(nameof(MarkerSummaryText));
            }
            RebuildMarkersOnCanvas();
        });
    }

    private void RemoveMarker()
    {
        if (!CanEditMarkers || SelectedMarker == null) return;
        if (MessageBox.Show(
                $"Удалить маркер '{SelectedMarker.Name}'?",
                "Удаление маркера",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        RunBusy("Удаление маркера...", () =>
        {
            var markerId = SelectedMarker.MarkerId;
            var response = _api.MapWorldMarkerRemove(new Dictionary<string, object> { { "markerId", markerId } });
            if (!EnsureOk(response, "Не удалось удалить маркер.")) return;
            var existing = Markers.FirstOrDefault(x => string.Equals(x.MarkerId, markerId, StringComparison.OrdinalIgnoreCase));
            if (existing != null) Markers.Remove(existing);
            SelectedMarker = Markers.FirstOrDefault();
            Notify(nameof(MarkerSummaryText));
            RebuildMarkersOnCanvas();
        });
    }

    private void EnrichMarkerBindingRequest(Dictionary<string, object> request)
    {
        var bindingType = NormalizeBindingTypeForRequest(MarkerLinkedEntityType);
        var entityId = (MarkerLinkedEntityId ?? string.Empty).Trim();

        request["linkedEntityType"] = bindingType;
        request["linkedEntityId"] = entityId;
        request["linkedSpaceNodeId"] = string.Empty;
        request["linkedContinentId"] = string.Empty;
        request["linkedCountryId"] = string.Empty;
        request["linkedCityStateId"] = string.Empty;
        request["linkedRegionId"] = string.Empty;
        request["linkedLocationId"] = string.Empty;
        request["linkedFactionId"] = string.Empty;
        request["linkedOrganizationId"] = string.Empty;

        if (string.IsNullOrWhiteSpace(entityId))
            return;

        switch (bindingType)
        {
            case MapMarkerBindingTypeIds.SpaceNode:
                request["linkedSpaceNodeId"] = entityId;
                break;
            case MapMarkerBindingTypeIds.Continent:
                request["linkedContinentId"] = entityId;
                break;
            case MapMarkerBindingTypeIds.Country:
                request["linkedCountryId"] = entityId;
                break;
            case MapMarkerBindingTypeIds.CityState:
                request["linkedCityStateId"] = entityId;
                break;
            case MapMarkerBindingTypeIds.Region:
                request["linkedRegionId"] = entityId;
                break;
            case MapMarkerBindingTypeIds.Location:
                request["linkedLocationId"] = entityId;
                break;
            case MapMarkerBindingTypeIds.Faction:
                request["linkedFactionId"] = entityId;
                break;
            case MapMarkerBindingTypeIds.Organization:
                request["linkedOrganizationId"] = entityId;
                break;
        }
    }

    private static string NormalizeBindingTypeForRequest(string value)
    {
        var key = (value ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "" => string.Empty,
            "space node" or "узел пространства" => MapMarkerBindingTypeIds.SpaceNode,
            "continent" or "материк" => MapMarkerBindingTypeIds.Continent,
            "country" or "страна" => MapMarkerBindingTypeIds.Country,
            "city state" or "город-государство" => MapMarkerBindingTypeIds.CityState,
            "region" or "регион" => MapMarkerBindingTypeIds.Region,
            "location" or "локация" => MapMarkerBindingTypeIds.Location,
            "room" or "помещение" => MapMarkerBindingTypeIds.Room,
            "interior" or "интерьер" => MapMarkerBindingTypeIds.Interior,
            "faction" or "фракция" => MapMarkerBindingTypeIds.Faction,
            "organization" or "организация" => MapMarkerBindingTypeIds.Organization,
            "custom" or "другое" => MapMarkerBindingTypeIds.Custom,
            _ => key
        };
    }

    private void SelectLayerValue(WorldLegendEntryUiItem? item)
    {
        if (item == null) return;
        SelectedLayerValue = item.Key;
        SelectedLayerLabel = item.Label;
    }

    private void SelectCell(int cellX, int cellY)
    {
        SelectedCellX = cellX;
        SelectedCellY = cellY;
        BrushX = cellX;
        BrushY = cellY;
        SelectedCellSummary = $"Клетка: X={cellX}, Y={cellY}";
    }

    private void ParseLayers(Dictionary<string, object> payload)
    {
        Layers.Clear();
        var rawLayers = Arr(Get(payload, "layers"));
        foreach (var rawLayer in rawLayers)
        {
            var layer = Dict(rawLayer);
            if (layer == null) continue;
            var ui = ToLayer(layer);
            Layers.Add(ui);
        }

        EnsureLayerExists(SelectedLayerType);
        Notify(nameof(SelectedLayerVisibleToPlayers));
    }

    private void ParseMarkers(Dictionary<string, object> payload)
    {
        Markers.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rawMarkers = Arr(Get(payload, "markers"));
        foreach (var raw in rawMarkers)
        {
            var marker = Dict(raw);
            if (marker == null) continue;
            AddMarkerIfNew(ToMarker(marker), seen);
        }

        foreach (var raw in Arr(Get(payload, "locations")))
        {
            var location = Dict(raw);
            if (location == null) continue;
            AddMarkerIfNew(ToMarker(location), seen);
        }

        foreach (var raw in Arr(Get(payload, "regions")))
        {
            var region = Dict(raw);
            if (region == null) continue;
            AddMarkerIfNew(ToMarker(region), seen);
        }
        Notify(nameof(MarkerSummaryText));
    }

    private void AddMarkerIfNew(WorldMapMarkerUiItem marker, HashSet<string> seen)
    {
        var key = $"{marker.Name}|{marker.MarkerType}|{marker.CellX}|{marker.CellY}";
        if (seen.Add(key))
            Markers.Add(marker);
    }

    private void ParseLegend(Dictionary<string, object> payload)
    {
        _legendByLayerType.Clear();
        var legends = Arr(Get(payload, "legends"));
        foreach (var raw in legends)
        {
            var legendMap = Dict(raw);
            if (legendMap == null) continue;
            var layerType = Str(Get(legendMap, "layerType"));
            if (string.IsNullOrWhiteSpace(layerType)) continue;
            var entries = new List<WorldLegendEntryUiItem>();
            foreach (var rawEntry in Arr(Get(legendMap, "entries")))
            {
                var entry = Dict(rawEntry);
                if (entry == null) continue;
                entries.Add(new WorldLegendEntryUiItem
                {
                    LayerType = layerType,
                    Key = Str(Get(entry, "key")),
                    Label = Str(Get(entry, "label"))
                });
            }

            _legendByLayerType[layerType] = entries;
        }
    }

    private void RefreshLegendForLayer()
    {
        LegendEntries.Clear();
        if (_legendByLayerType.TryGetValue(SelectedLayerType, out var entries))
        {
            foreach (var entry in entries)
                LegendEntries.Add(entry);
        }
    }

    private void ApplyLayerPayload(Dictionary<string, object>? layerPayload)
    {
        if (layerPayload == null) return;
        var ui = ToLayer(layerPayload);
        var existing = GetLayer(ui.LayerType);
        if (existing == null)
        {
            Layers.Add(ui);
            return;
        }

        existing.Name = ui.Name;
        existing.LayerType = ui.LayerType;
        existing.IsVisibleToGM = ui.IsVisibleToGM;
        existing.IsVisibleToPlayers = ui.IsVisibleToPlayers;
        existing.Opacity = ui.Opacity;
        existing.Cells = ui.Cells;
        existing.CellsCount = ui.CellsCount;
        Notify(nameof(SelectedLayerVisibleToPlayers));
    }

    private void ApplyMarkerUpdate(WorldMapMarkerUiItem existing, Dictionary<string, object> markerPayload)
    {
        var updated = ToMarker(markerPayload);
        existing.Name = updated.Name;
        existing.MarkerType = updated.MarkerType;
        existing.CellX = updated.CellX;
        existing.CellY = updated.CellY;
        existing.XNormalized = updated.XNormalized;
        existing.YNormalized = updated.YNormalized;
        existing.LinkedEntityType = updated.LinkedEntityType;
        existing.LinkedEntityId = updated.LinkedEntityId;
        existing.LinkedEntityDisplayName = updated.LinkedEntityDisplayName;
        existing.LinkedEntityPublicLabel = updated.LinkedEntityPublicLabel;
        existing.IsPlayerVisible = updated.IsPlayerVisible;
        existing.VisibilityMode = updated.VisibilityMode;
        existing.PublicNotes = updated.PublicNotes;
        existing.GMNotes = updated.GMNotes;
        existing.IconKey = updated.IconKey;
        existing.ColorKey = updated.ColorKey;
        existing.CardTitle = updated.CardTitle;
        existing.CardDescription = updated.CardDescription;
        existing.UpdatedAtUtc = updated.UpdatedAtUtc;
        existing.NotifyAll();
    }

    private void RebuildCanvas()
    {
        GridLines.Clear();
        PaintedCells.Clear();
        RebuildMarkersOnCanvas();
        CanvasHints.Clear();

        if (SelectedMap == null)
        {
            CanvasWidth = 820;
            CanvasHeight = 520;
            CellPixelSize = 0;
            CanvasScaleLabel = "нет данных";
            return;
        }

        var targetWidth = 860d;
        var targetHeight = 540d;
        var pxByWidth = targetWidth / Math.Max(1, SelectedMap.WidthCells);
        var pxByHeight = targetHeight / Math.Max(1, SelectedMap.HeightCells);
        var cellPx = Math.Max(2d, Math.Min(22d, Math.Min(pxByWidth, pxByHeight)));
        CellPixelSize = cellPx;

        CanvasWidth = Math.Round(Math.Max(160d, SelectedMap.WidthCells * cellPx), 2);
        CanvasHeight = Math.Round(Math.Max(120d, SelectedMap.HeightCells * cellPx), 2);
        CanvasScaleLabel = $"1 клетка = {cellPx:0.##}px";

        var xStep = SelectedMap.WidthCells > 300 ? 2 : 1;
        var yStep = SelectedMap.HeightCells > 300 ? 2 : 1;

        for (var x = 0; x <= SelectedMap.WidthCells; x += xStep)
        {
            var px = x * cellPx;
            GridLines.Add(new MapGridLineUiItem { X1 = px, Y1 = 0, X2 = px, Y2 = CanvasHeight });
        }

        for (var y = 0; y <= SelectedMap.HeightCells; y += yStep)
        {
            var py = y * cellPx;
            GridLines.Add(new MapGridLineUiItem { X1 = 0, Y1 = py, X2 = CanvasWidth, Y2 = py });
        }

        RebuildPaintedCells();
        RebuildMarkersOnCanvas();
        CanvasHints.Add($"Координаты: X 0..{SelectedMap.WidthCells - 1}, Y 0..{SelectedMap.HeightCells - 1}");
        CanvasHints.Add("Хранилище координат мира: клетки и/или normalized 0..1.");
    }

    private void RebuildPaintedCells()
    {
        PaintedCells.Clear();
        var layer = GetLayer(SelectedLayerType);
        if (layer == null || CellPixelSize <= 0 || SelectedMap == null) return;

        foreach (var cell in layer.Cells.Values)
        {
            if (!MapRuntimeValidation.IsWorldCellInsideBounds(cell.CellX, cell.CellY, SelectedMap.WidthCells, SelectedMap.HeightCells)) continue;
            PaintedCells.Add(new WorldMapCellUiItem
            {
                CellX = cell.CellX,
                CellY = cell.CellY,
                ValueKey = cell.ValueKey,
                ValueLabel = cell.ValueLabel,
                X = cell.CellX * CellPixelSize,
                Y = cell.CellY * CellPixelSize,
                Width = CellPixelSize,
                Height = CellPixelSize,
                FillHex = ResolveCellColor(SelectedLayerType, cell.ValueKey)
            });
        }
    }

    private void RebuildMarkersOnCanvas()
    {
        if (CellPixelSize <= 0 || SelectedMap == null)
        {
            foreach (var marker in Markers)
            {
                marker.PixelX = 0;
                marker.PixelY = 0;
            }

            return;
        }

        foreach (var marker in Markers)
        {
            if (marker.CellX >= 0 && marker.CellY >= 0)
            {
                marker.PixelX = (marker.CellX + 0.5d) * CellPixelSize;
                marker.PixelY = (marker.CellY + 0.5d) * CellPixelSize;
            }
            else
            {
                var x = Math.Max(0d, Math.Min(1d, marker.XNormalized));
                var y = Math.Max(0d, Math.Min(1d, marker.YNormalized));
                marker.PixelX = x * CanvasWidth;
                marker.PixelY = y * CanvasHeight;
            }

            marker.NotifyPixel();
        }
    }

    private void EnsureLayerExists(string layerType)
    {
        if (GetLayer(layerType) != null || SelectedMap == null) return;
        var created = new WorldMapLayerUiItem
        {
            LayerType = layerType,
            Name = LayerDisplayName(layerType),
            IsVisibleToGM = true,
            IsVisibleToPlayers = false,
            Opacity = 1d
        };
        Layers.Add(created);
    }

    private WorldMapLayerUiItem? GetLayer(string layerType)
        => Layers.FirstOrDefault(x => string.Equals(x.LayerType, layerType, StringComparison.OrdinalIgnoreCase));

    private static string LayerDisplayName(string layerType)
    {
        if (string.Equals(layerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase)) return "Высота / глубина";
        if (string.Equals(layerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase)) return "Биомы";
        if (string.Equals(layerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase)) return "Страны / области";
        return layerType;
    }

    private void EnsureDefaultValueForLayer()
    {
        if (string.Equals(SelectedLayerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase))
        {
            if (!IsValidHeightDepth(SelectedLayerValue))
                SelectedLayerValue = WorldMapHeightDepthCategoryIds.Lowland;
        }
        else if (string.Equals(SelectedLayerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase))
        {
            if (!IsValidBiome(SelectedLayerValue))
                SelectedLayerValue = WorldMapBiomeIds.Plains;
        }
        else if (string.Equals(SelectedLayerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(SelectedLayerValue))
                SelectedLayerValue = "country";
        }
    }

    private bool IsLayerEnabled(string layerType)
    {
        if (string.Equals(layerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase)) return IsHeightDepthEnabled;
        if (string.Equals(layerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase)) return IsBiomeEnabled;
        if (string.Equals(layerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase)) return IsPoliticalEnabled;
        return false;
    }

    private void ClearMapVisuals()
    {
        Layers.Clear();
        Markers.Clear();
        GridLines.Clear();
        PaintedCells.Clear();
        LegendEntries.Clear();
        CanvasHints.Clear();
        SelectedCellX = -1;
        SelectedCellY = -1;
        SelectedCellSummary = "Клетка не выбрана.";
        CanvasWidth = 820;
        CanvasHeight = 520;
        CellPixelSize = 0;
        CanvasScaleLabel = "нет данных";
    }

    private WorldMapLayerUiItem ToLayer(Dictionary<string, object> map)
    {
        var cells = new Dictionary<string, WorldLayerCellData>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawCell in Arr(Get(map, "cells")))
        {
            var cellPayload = Dict(rawCell);
            if (cellPayload == null) continue;
            var cellX = Int(Get(cellPayload, "cellX"), -1);
            var cellY = Int(Get(cellPayload, "cellY"), -1);
            if (cellX < 0 || cellY < 0) continue;

            var valuePayload = Dict(Get(cellPayload, "value"));
            var valueKey = string.Empty;
            var valueLabel = string.Empty;
            if (valuePayload != null)
            {
                valueLabel = Str(Get(valuePayload, "label"));
                if (valuePayload.TryGetValue("category", out var cat)) valueKey = Str(cat);
                else if (valuePayload.TryGetValue("biomeId", out var biomeId)) valueKey = Str(biomeId);
                else if (valuePayload.TryGetValue("owner", out var owner)) valueKey = Str(owner);
                else valueKey = Str(Get(valuePayload, "value"));
            }

            cells[$"{cellX}:{cellY}"] = new WorldLayerCellData
            {
                CellX = cellX,
                CellY = cellY,
                ValueKey = valueKey,
                ValueLabel = valueLabel
            };
        }

        return new WorldMapLayerUiItem
        {
            LayerId = Str(Get(map, "layerId")),
            LayerType = Str(Get(map, "layerType"), WorldMapLayerTypeIds.Custom),
            Name = Str(Get(map, "name"), "Слой"),
            IsVisibleToGM = Bool(Get(map, "isVisibleToGM"), true),
            IsVisibleToPlayers = Bool(Get(map, "isVisibleToPlayers"), false),
            Opacity = Dbl(Get(map, "opacity"), 1d),
            CellsCount = Int(Get(map, "cellsCount"), cells.Count),
            Cells = cells
        };
    }

    private WorldMapMarkerUiItem ToMarker(Dictionary<string, object> map)
    {
        var cellX = Int(Get(map, "cellX"), -1);
        var cellY = Int(Get(map, "cellY"), -1);
        var normalizedX = Dbl(Get(map, "xNormalized"), -1d);
        var normalizedY = Dbl(Get(map, "yNormalized"), -1d);

        return new WorldMapMarkerUiItem
        {
            MarkerId = Str(Get(map, "markerId"), Get(map, "id")),
            Name = Str(Get(map, "name"), Get(map, "displayName"), Get(map, "label"), Get(map, "text"), "Маркер"),
            MarkerType = NormalizeMarkerType(Str(Get(map, "markerType"), Get(map, "locationType"), Get(map, "regionType"), MapMarkerTypeIds.Custom)),
            CellX = cellX,
            CellY = cellY,
            XNormalized = normalizedX < 0 ? 0.5d : normalizedX,
            YNormalized = normalizedY < 0 ? 0.5d : normalizedY,
            IsPlayerVisible = Bool(Get(map, "isPlayerVisible"), true),
            VisibilityMode = Str(Get(map, "visibilityMode"), MapVisibilityModes.Party),
            LinkedEntityType = Str(Get(map, "linkedEntityType"), InferBindingType(map)),
            LinkedEntityId = Str(Get(map, "linkedEntityId"), Get(map, "id")),
            LinkedEntityDisplayName = Str(Get(map, "linkedEntityDisplayName"), Get(map, "displayName"), Get(map, "name")),
            LinkedEntityPublicLabel = Str(Get(map, "linkedEntityPublicLabel"), Get(map, "publicLabel"), Get(map, "displayName"), Get(map, "name")),
            PublicNotes = Str(Get(map, "publicNotes"), Get(map, "publicDescription")),
            GMNotes = Str(Get(map, "gmNotes"), Get(map, "GMNotes")),
            IconKey = Str(Get(map, "iconKey")),
            ColorKey = Str(Get(map, "colorKey")),
            CardTitle = Str(Get(map, "cardTitle"), Get(map, "displayName"), Get(map, "name")),
            CardDescription = Str(Get(map, "cardDescription"), Get(map, "publicDescription"), Get(map, "publicNotes")),
            UpdatedAtUtc = Date(Get(map, "updatedAtUtc"))
        };
    }

    private static string InferBindingType(Dictionary<string, object> map)
    {
        if (!string.IsNullOrWhiteSpace(Str(Get(map, "regionType"))))
            return MapMarkerBindingTypeIds.Region;
        if (!string.IsNullOrWhiteSpace(Str(Get(map, "locationType"))))
            return MapMarkerBindingTypeIds.Location;
        return string.Empty;
    }

    private static string NormalizeMarkerType(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "continent" => MapMarkerTypeIds.Continent,
            "country" => MapMarkerTypeIds.Country,
            "capital" => MapMarkerTypeIds.Capital,
            "city" => MapMarkerTypeIds.City,
            "city_state" => MapMarkerTypeIds.CityState,
            "region" => MapMarkerTypeIds.Region,
            "location" => MapMarkerTypeIds.Location,
            "point_of_interest" => MapMarkerTypeIds.PointOfInterest,
            "port" => MapMarkerTypeIds.Port,
            "ruin" => MapMarkerTypeIds.Ruin,
            "dungeon" => MapMarkerTypeIds.Dungeon,
            "faction_base" => MapMarkerTypeIds.FactionBase,
            "sea" => MapMarkerTypeIds.Region,
            "border" => MapMarkerTypeIds.Region,
            _ => string.IsNullOrWhiteSpace(normalized) ? MapMarkerTypeIds.Custom : normalized
        };
    }

    private static string ResolveCellColor(string layerType, string valueKey)
    {
        var key = (valueKey ?? string.Empty).Trim().ToLowerInvariant();
        if (string.Equals(layerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase))
        {
            return key switch
            {
                WorldMapHeightDepthCategoryIds.DeepOcean => "#FF0B3C5D",
                WorldMapHeightDepthCategoryIds.ShallowSea => "#FF2563EB",
                WorldMapHeightDepthCategoryIds.Coast => "#FF38BDF8",
                WorldMapHeightDepthCategoryIds.Lowland => "#FF4ADE80",
                WorldMapHeightDepthCategoryIds.Highland => "#FFA3E635",
                WorldMapHeightDepthCategoryIds.Mountain => "#FF94A3B8",
                WorldMapHeightDepthCategoryIds.ExtremeMountain => "#FFE2E8F0",
                _ => "#FF64748B"
            };
        }

        if (string.Equals(layerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase))
        {
            return key switch
            {
                WorldMapBiomeIds.Ocean => "#FF1D4ED8",
                WorldMapBiomeIds.Coast => "#FF38BDF8",
                WorldMapBiomeIds.TropicalForest => "#FF15803D",
                WorldMapBiomeIds.Forest => "#FF166534",
                WorldMapBiomeIds.Plains => "#FF84CC16",
                WorldMapBiomeIds.Savanna => "#FFEAB308",
                WorldMapBiomeIds.Desert => "#FFF59E0B",
                WorldMapBiomeIds.Mountains => "#FF94A3B8",
                WorldMapBiomeIds.Tundra => "#FFD1D5DB",
                WorldMapBiomeIds.Subarctic => "#FF93C5FD",
                WorldMapBiomeIds.Swamp => "#FF365314",
                WorldMapBiomeIds.Urban => "#FF6B7280",
                _ => "#FF64748B"
            };
        }

        if (string.Equals(layerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase))
        {
            var hash = Math.Abs(key.GetHashCode());
            var palette = new[]
            {
                "#FFE879F9", "#FFF472B6", "#FF60A5FA", "#FF34D399",
                "#FFFBBF24", "#FFA78BFA", "#FFF97316", "#FF22D3EE"
            };
            return palette[hash % palette.Length];
        }

        return "#FF64748B";
    }

    private bool EnsureOk(ResponseEnvelope response, string fallbackError)
    {
        if (response.Status == ResponseStatus.Ok) return true;
        ErrorMessage = string.IsNullOrWhiteSpace(response.Message) ? fallbackError : response.Message;
        StatusMessage = fallbackError;
        if (response.Status == ResponseStatus.Forbidden && response.ErrorCode == ErrorCode.Forbidden)
            WarningMessage = "Функция отключена флагами функций или недоступна по правам.";
        ClientLogService.Instance.Warn($"admin.map.world.error status={response.Status} code={response.ErrorCode} message={response.Message}");
        return false;
    }

    private void RunBusy(string status, Action action)
    {
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        StatusMessage = status;
        IsLoading = true;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Операция не завершена.";
            ClientLogService.Instance.Error("admin.map.world.exception", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static bool IsValidHeightDepth(string value)
    {
        return value == WorldMapHeightDepthCategoryIds.DeepOcean
               || value == WorldMapHeightDepthCategoryIds.ShallowSea
               || value == WorldMapHeightDepthCategoryIds.Coast
               || value == WorldMapHeightDepthCategoryIds.Lowland
               || value == WorldMapHeightDepthCategoryIds.Highland
               || value == WorldMapHeightDepthCategoryIds.Mountain
               || value == WorldMapHeightDepthCategoryIds.ExtremeMountain
               || value == WorldMapHeightDepthCategoryIds.Custom;
    }

    private static bool IsValidBiome(string value)
    {
        return value == WorldMapBiomeIds.Ocean
               || value == WorldMapBiomeIds.Coast
               || value == WorldMapBiomeIds.TropicalForest
               || value == WorldMapBiomeIds.Forest
               || value == WorldMapBiomeIds.Plains
               || value == WorldMapBiomeIds.Savanna
               || value == WorldMapBiomeIds.Desert
               || value == WorldMapBiomeIds.Mountains
               || value == WorldMapBiomeIds.Tundra
               || value == WorldMapBiomeIds.Subarctic
               || value == WorldMapBiomeIds.Swamp
               || value == WorldMapBiomeIds.Urban
               || value == WorldMapBiomeIds.Custom;
    }

    private static object? Get(Dictionary<string, object> map, string key)
    {
        if (map.TryGetValue(key, out var value)) return value;
        foreach (var pair in map)
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        return null;
    }

    private static object[] ExtractFeatureFlagItems(Dictionary<string, object>? payload)
    {
        if (payload == null) return Array.Empty<object>();
        var items = Arr(payload.TryGetValue("items", out var rawItems) ? rawItems : null);
        if (items.Length > 0) return items;
        items = Arr(payload.TryGetValue("flags", out var rawFlags) ? rawFlags : null);
        if (items.Length > 0) return items;
        var snapshot = Dict(payload.TryGetValue("snapshot", out var rawSnapshot) ? rawSnapshot : null);
        return snapshot == null
            ? Array.Empty<object>()
            : Arr(snapshot.TryGetValue("flags", out var snapshotFlags) ? snapshotFlags : null);
    }

    private static Dictionary<string, object>? Dict(object? value)
    {
        if (value is Dictionary<string, object> typed)
            return typed;
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

        if (value is IEnumerable enumerable and not string)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in enumerable)
            {
                var entry = Dict(item);
                if (entry == null) continue;
                var key = Convert.ToString(Get(entry, "key"));
                if (string.IsNullOrWhiteSpace(key)) continue;
                result[key] = Get(entry, "value")!;
            }

            if (result.Count > 0)
                return result;
        }

        return null;
    }

    private static object[] Arr(object? value)
    {
        if (value is object[] arr) return arr;
        if (value is IEnumerable enumerable && value is not string)
            return enumerable.Cast<object>().ToArray();
        return Array.Empty<object>();
    }

    private static string Str(params object?[] values)
    {
        foreach (var value in values)
        {
            var parsed = Convert.ToString(value);
            if (!string.IsNullOrWhiteSpace(parsed)) return parsed;
        }

        return string.Empty;
    }

    private static int Int(object? value, int fallback)
    {
        if (value is int typed) return typed;
        if (value is long asLong && asLong <= int.MaxValue && asLong >= int.MinValue) return (int)asLong;
        return int.TryParse(Convert.ToString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static double Dbl(object? value, double fallback)
    {
        if (value is double typed) return typed;
        if (value is float asFloat) return asFloat;
        if (value is decimal asDecimal) return (double)asDecimal;
        return double.TryParse(Convert.ToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static bool Bool(object? value, bool fallback)
    {
        if (value is bool typed) return typed;
        return bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    }

    private static DateTime Date(object? value)
    {
        if (value is DateTime dt) return dt;
        return DateTime.TryParse(Convert.ToString(value), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : default;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private ObservableCollection<WorldMapLayerUiItem> Layers { get; } = new();
    private double CellPixelSize { get; set; }
}

public sealed class WorldMapUiItem : ViewModelBase
{
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int WidthCells { get; set; }
    public int HeightCells { get; set; }
    public double CellSizeKm { get; set; }
    public string ProjectionMode { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public bool IsPlayerVisible { get; set; } = true;
    public int MarkerCount { get; set; }
    public bool IsArchived { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string Label => $"{Name} • {WidthCells}×{HeightCells} • markers={MarkerCount} • {ProjectionMode}";
}

public sealed class WorldMapLayerUiItem : ViewModelBase
{
    public string LayerId { get; set; } = string.Empty;
    public string LayerType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsVisibleToGM { get; set; } = true;
    public bool IsVisibleToPlayers { get; set; }
    public double Opacity { get; set; } = 1d;
    public int CellsCount { get; set; }
    public Dictionary<string, WorldLayerCellData> Cells { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WorldLayerCellData
{
    public int CellX { get; set; }
    public int CellY { get; set; }
    public string ValueKey { get; set; } = string.Empty;
    public string ValueLabel { get; set; } = string.Empty;
}

public sealed class WorldMapCellUiItem
{
    public int CellX { get; set; }
    public int CellY { get; set; }
    public string ValueKey { get; set; } = string.Empty;
    public string ValueLabel { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string FillHex { get; set; } = "#FF64748B";
}

public sealed class WorldMapMarkerUiItem : ViewModelBase
{
    private bool _isSelected;
    public string MarkerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = MapMarkerTypeIds.Custom;
    public int CellX { get; set; } = -1;
    public int CellY { get; set; } = -1;
    public double XNormalized { get; set; } = 0.5d;
    public double YNormalized { get; set; } = 0.5d;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public string LinkedEntityDisplayName { get; set; } = string.Empty;
    public string LinkedEntityPublicLabel { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public double PixelX { get; set; }
    public double PixelY { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; Notify(); } }
    }

    public string MarkerTypeDisplay => MarkerType switch
    {
        MapMarkerTypeIds.Continent => "Материк",
        MapMarkerTypeIds.Country => "Страна",
        MapMarkerTypeIds.Capital => "Столица",
        MapMarkerTypeIds.City => "Город",
        MapMarkerTypeIds.CityState => "Город-государство",
        MapMarkerTypeIds.Region => "Регион",
        MapMarkerTypeIds.Location => "Локация",
        MapMarkerTypeIds.PointOfInterest => "Точка интереса",
        MapMarkerTypeIds.Port => "Порт",
        MapMarkerTypeIds.Ruin => "Руины",
        MapMarkerTypeIds.Dungeon => "Подземелье",
        MapMarkerTypeIds.FactionBase => "База фракции",
        _ => "Другое"
    };

    public string CoordinatesText => CellX >= 0 && CellY >= 0
        ? $"cell:{CellX},{CellY}"
        : $"n:{XNormalized:0.###},{YNormalized:0.###}";

    public string BindingDisplayText
    {
        get
        {
            var type = string.IsNullOrWhiteSpace(LinkedEntityType) ? "Без привязки" : LinkedEntityType;
            var label = FirstNonEmpty(LinkedEntityPublicLabel, LinkedEntityDisplayName, LinkedEntityId);
            return string.IsNullOrWhiteSpace(label) || string.Equals(type, "Без привязки", StringComparison.OrdinalIgnoreCase)
                ? type
                : $"{type}: {label}";
        }
    }

    public void NotifyPixel()
    {
        Notify(nameof(PixelX));
        Notify(nameof(PixelY));
        Notify(nameof(CoordinatesText));
    }

    public void NotifyAll()
    {
        Notify(nameof(Name));
        Notify(nameof(MarkerType));
        Notify(nameof(MarkerTypeDisplay));
        Notify(nameof(CellX));
        Notify(nameof(CellY));
        Notify(nameof(XNormalized));
        Notify(nameof(YNormalized));
        Notify(nameof(CoordinatesText));
        Notify(nameof(IsPlayerVisible));
        Notify(nameof(VisibilityMode));
        Notify(nameof(LinkedEntityType));
        Notify(nameof(LinkedEntityId));
        Notify(nameof(LinkedEntityDisplayName));
        Notify(nameof(LinkedEntityPublicLabel));
        Notify(nameof(BindingDisplayText));
        Notify(nameof(PublicNotes));
        Notify(nameof(GMNotes));
        Notify(nameof(IconKey));
        Notify(nameof(ColorKey));
        Notify(nameof(CardTitle));
        Notify(nameof(CardDescription));
        Notify(nameof(UpdatedAtUtc));
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }
}

public sealed class WorldLegendEntryUiItem
{
    public string LayerType { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Display => $"{Label} ({Key})";
}
