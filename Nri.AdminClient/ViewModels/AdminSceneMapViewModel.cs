using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;
using Nri.Ui.Wpf.Controls;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminSceneMapViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "dev-campaign-core";
    private string _ruleSetId = "fantasy_nri_default";
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
    private int _newGridCellSizeMeters = 50;
    private bool _showGrid = true;
    private bool _showCoordinates = true;
    private string _markerName = "Маркер";
    private string _markerType = "point_of_interest";
    private double _markerX;
    private double _markerY;
    private string _markerIconKey = string.Empty;
    private string _markerColorKey = string.Empty;
    private bool _markerPlayerVisible = true;
    private string _markerVisibility = "PlayerVisible";
    private string _markerLinkedEntityType = string.Empty;
    private string _markerLinkedEntityId = string.Empty;
    private string _markerCardTitle = string.Empty;
    private string _markerCardDescription = string.Empty;
    private string _markerPublicNotes = string.Empty;
    private string _markerGmNotes = string.Empty;
    private SceneTokenUiItem? _selectedToken;
    private string _tokenName = "Токен";
    private string _tokenType = "Object";
    private double _tokenX;
    private double _tokenY;
    private string _tokenVisibility = "PlayerVisible";
    private string _tokenDescriptionPlayer = string.Empty;
    private string _tokenDescriptionGm = string.Empty;
    private string _tokenLinkedEntityType = string.Empty;
    private string _tokenLinkedEntityId = string.Empty;
    private bool _tokenCanJoinCombat;
    private SceneMapLayerUiItem? _selectedLayer;
    private SceneMapShapeUiItem? _selectedShape;
    private SceneMapTileLayerUiItem? _selectedTileLayer;
    private SceneMapTilePatchUiItem? _selectedTilePatch;
    private SceneMapAssetInstanceUiItem? _selectedAssetInstance;
    private string _locationTool = "Select";
    private string _layerName = "Объекты сцены";
    private string _layerKind = "Objects";
    private int _layerSortOrder = 40;
    private bool _layerVisibleByDefault = true;
    private string _layerVisibility = "PlayerVisible";
    private string _shapeName = "Объект локации";
    private string _shapeDescriptionPlayer = string.Empty;
    private string _shapeDescriptionGm = string.Empty;
    private string _shapeKind = "Rectangle";
    private string _objectKind = "Decoration";
    private string _shapeLayerId = string.Empty;
    private double _shapeX = 120;
    private double _shapeY = 120;
    private double _shapeWidth = 120;
    private double _shapeHeight = 80;
    private double _shapeRadius = 35;
    private double _shapeRotationDegrees;
    private string _shapePoints = string.Empty;
    private string _shapeText = string.Empty;
    private string _shapeFillKey = "terrain";
    private string _shapeStrokeKey = "default";
    private double _shapeOpacity = 0.65;
    private string _shapeMaterialKey = "cobblestone";
    private string _shapeTextureKey = "cobble_small";
    private string _shapePatternKey = string.Empty;
    private string _shapeAssetKey = string.Empty;
    private string _shapeVisualStyleKey = "object.default";
    private string _shapeRenderMode = "TexturedShape";
    private bool _shapeGridSnapEnabled = true;
    private double _shapeVisualOpacity = 0.88;
    private double _shapeStrokeThickness = 1.4;
    private int _shapeZIndex;
    private int _shapeSortOrder;
    private string _shapeVisibility = "PlayerVisible";
    private bool _shapeBlocksMovement;
    private bool _shapeBlocksVision;
    private bool _shapeProvidesCover;
    private bool _shapeIsInteractable;
    private string _shapeLinkedEntityType = "None";
    private string _shapeLinkedEntityId = string.Empty;
    private bool _showTokenLayer = true;
    private bool _showGmOnlyLayer = true;
    private bool _showHiddenLayer = true;
    private string _visualMode = "Карта";
    private string _selectedAssetCategory = "Рынок / магазин";
    private LocationMapAssetUiItem? _selectedAsset;
    private double _gridOpacity = 0.35;
    private double _brushSizeMeters = 20;
    private double _tileSizeMeters = 5;
    private bool _snapToGrid = true;
    private Visibility _placementGhostVisibility = Visibility.Collapsed;
    private double _placementGhostX;
    private double _placementGhostY;
    private double _placementGhostWidth = 24;
    private double _placementGhostHeight = 24;
    private string _placementGhostLabel = string.Empty;
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
    private string _sessionId = "dev_session_0162";
    private string _activeGroupId = string.Empty;
    private string _sceneId = string.Empty;
    private bool _hasActiveMap;
    private string _activeMapId = string.Empty;
    private string _activeMapName = "Не выбрана";
    private readonly MapViewportState _viewport = new MapViewportState(2000d, 2000d, 760d, 500d, 50d);
    private readonly MapEditorHistory<MapEditorHistoryEntry0203> _editorHistory = new MapEditorHistory<MapEditorHistoryEntry0203>(50);
    private long _mapEditorRevision;
    private string _canonicalEditorMapId = string.Empty;
    private string _paletteSearch = string.Empty;
    private double _snapStepMeters = 5d;
    private bool _editorDragActive;
    private double _editorDragStartX;
    private double _editorDragStartY;
    private double _editorDragOffsetX;
    private double _editorDragOffsetY;
    private string _coordinateIndicator = "X=0 м, Y=0 м";
    private string _gridStepLabel = "Сетка: 50 м";
    private readonly List<MapFogCellRange> _fogHiddenRanges = new List<MapFogCellRange>();
    private readonly List<MapFogCellRange> _fogRevealedRanges = new List<MapFogCellRange>();
    private string _selectedPlayerPreviewCharacterId = string.Empty;
    private NriReferenceOption? _selectedPlayerPreviewCharacterOption;
    private string _playerPreviewCharacterName = "Персонаж не выбран";
    private string _playerPreviewMapName = "Предпросмотр не загружен";
    private string _playerPreviewSummary = "Выберите тестового персонажа и запросите безопасную проекцию сервера.";
    private bool _isPlayerPreviewVisible;

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
        AddTokenCommand = new RelayCommand(AddToken);
        MoveTokenCommand = new RelayCommand(MoveToken);
        SaveTokenCommand = new RelayCommand(UpdateToken);
        ArchiveTokenCommand = new RelayCommand(ArchiveToken);
        CreateLayerCommand = new RelayCommand(CreateLayer);
        SaveLayerCommand = new RelayCommand(UpdateLayer);
        ArchiveLayerCommand = new RelayCommand(ArchiveLayer);
        MoveLayerUpCommand = new RelayCommand(() => MoveSelectedLayer(-1));
        MoveLayerDownCommand = new RelayCommand(() => MoveSelectedLayer(1));
        ToggleLayerLockCommand = new RelayCommand(ToggleSelectedLayerLock);
        AddShapeCommand = new RelayCommand(AddShape);
        SaveShapeCommand = new RelayCommand(UpdateShape);
        MoveShapeCommand = new RelayCommand(MoveShape);
        ResizeShapeCommand = new RelayCommand(ResizeShape);
        DuplicateShapeCommand = new RelayCommand(DuplicateShape);
        ArchiveShapeCommand = new RelayCommand(ArchiveShape);
        PaintTileCommand = new RelayCommand(PaintTileFromFields);
        EraseTileCommand = new RelayCommand(EraseSelectedTilePatch);
        StampAssetCommand = new RelayCommand(StampAssetFromFields);
        SaveAssetInstanceCommand = new RelayCommand(UpdateAssetInstance);
        ArchiveAssetInstanceCommand = new RelayCommand(ArchiveAssetInstance);
        UndoEditorCommand = new RelayCommand(UndoEditor);
        RedoEditorCommand = new RelayCommand(RedoEditor);
        DeleteSelectedEditorObjectCommand = new RelayCommand(DeleteSelectedEditorObject);
        SetPaintTileToolCommand = new RelayCommand(() => LocationTool = "PaintTile");
        SetEraseTileToolCommand = new RelayCommand(() => LocationTool = "EraseTile");
        SetStampAssetToolCommand = new RelayCommand(() => LocationTool = "StampAsset");
        SetWallToolCommand = new RelayCommand(() => LocationTool = "WallTool");
        SetDoorToolCommand = new RelayCommand(() => LocationTool = "DoorTool");
        SetRoomToolCommand = new RelayCommand(() => LocationTool = "RoomTool");
        SetRoadToolCommand = new RelayCommand(() => LocationTool = "RoadTool");
        SetZoneToolCommand = new RelayCommand(() => LocationTool = "ZoneTool");
        SetMapVisualModeCommand = new RelayCommand(() => VisualMode = "Карта");
        SetSchematicVisualModeCommand = new RelayCommand(() => VisualMode = "Схема");
        RefreshFogCommand = new RelayCommand(RefreshFog);
        PaintFogCommand = new RelayCommand(PaintFogFromFields);
        RevealAllFogCommand = new RelayCommand(RevealAllFog);
        HideAllFogCommand = new RelayCommand(HideAllFog);
        ClearFogCommand = new RelayCommand(ClearFogCustom);
        ResetFogCommand = new RelayCommand(ResetFog);
        RefreshActiveMapCommand = new RelayCommand(LoadActiveMapLink);
        SetActiveMapCommand = new RelayCommand(SetSelectedMapActive);
        ClearActiveMapCommand = new RelayCommand(ClearActiveMap);
        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);
        ResetViewCommand = new RelayCommand(ResetView);
        FitToMapCommand = new RelayCommand(FitToMap);
        SetOneHundredPercentCommand = new RelayCommand(SetOneHundredPercent);
        ClearErrorCommand = new RelayCommand(() => { ErrorMessage = string.Empty; WarningMessage = string.Empty; });
        LoadPlayerPreviewCommand = new RelayCommand(LoadServerPlayerPreview);
        SelectPlayerPreviewCharacterCommand = new RelayCommand<NriReferenceOption>(ApplyPlayerPreviewCharacterSelection);
        foreach (var asset in LocationMapAssetUiItem.CreateBuiltIn())
            BuiltInLocationAssets.Add(asset);
        RefreshPaletteFilter();
        SelectedAsset = BuiltInLocationAssets.FirstOrDefault(asset => asset.Category == SelectedAssetCategory) ?? BuiltInLocationAssets.FirstOrDefault();
    }

    public ObservableCollection<SceneMapListUiItem> Maps { get; } = new ObservableCollection<SceneMapListUiItem>();
    public ObservableCollection<SceneMarkerUiItem> Markers { get; } = new ObservableCollection<SceneMarkerUiItem>();
    public ObservableCollection<SceneTokenUiItem> Tokens { get; } = new ObservableCollection<SceneTokenUiItem>();
    public ObservableCollection<SceneTokenUiItem> VisibleTokens { get; } = new ObservableCollection<SceneTokenUiItem>();
    public ObservableCollection<SceneMapLayerUiItem> LocationLayers { get; } = new ObservableCollection<SceneMapLayerUiItem>();
    public ObservableCollection<SceneMapShapeUiItem> LocationShapes { get; } = new ObservableCollection<SceneMapShapeUiItem>();
    public ObservableCollection<SceneMapShapeUiItem> VisibleLocationShapes { get; } = new ObservableCollection<SceneMapShapeUiItem>();
    public ObservableCollection<SceneMapTileLayerUiItem> TileLayers { get; } = new ObservableCollection<SceneMapTileLayerUiItem>();
    public ObservableCollection<SceneMapTilePatchUiItem> TilePatches { get; } = new ObservableCollection<SceneMapTilePatchUiItem>();
    public ObservableCollection<SceneMapTilePatchUiItem> VisibleTilePatches { get; } = new ObservableCollection<SceneMapTilePatchUiItem>();
    public ObservableCollection<SceneMapAssetInstanceUiItem> AssetInstances { get; } = new ObservableCollection<SceneMapAssetInstanceUiItem>();
    public ObservableCollection<SceneMapAssetInstanceUiItem> VisibleAssetInstances { get; } = new ObservableCollection<SceneMapAssetInstanceUiItem>();
    public ObservableCollection<LocationMapAssetUiItem> BuiltInLocationAssets { get; } = new ObservableCollection<LocationMapAssetUiItem>();
    public ObservableCollection<LocationMapAssetUiItem> FilteredPaletteAssets { get; } = new ObservableCollection<LocationMapAssetUiItem>();
    public ObservableCollection<MapGridLineUiItem> GridLines { get; } = new ObservableCollection<MapGridLineUiItem>();
    public ObservableCollection<MapFogOverlayUiItem> FogOverlays { get; } = new ObservableCollection<MapFogOverlayUiItem>();
    public ObservableCollection<SceneMarkerBindingUiItem> MarkerBindings { get; } = new ObservableCollection<SceneMarkerBindingUiItem>();
    public ObservableCollection<string> CanvasCoordinateHints { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> MarkerVisibilityOptions { get; } = new ObservableCollection<string> { "PlayerVisible", "GmOnly", "Hidden" };
    public ObservableCollection<NriReferenceOption> PlayerPreviewCharacterOptions { get; } = new ObservableCollection<NriReferenceOption>();
    public ObservableCollection<AdminPlayerMapPreviewObjectUiItem0204> PlayerPreviewObjects { get; } = new ObservableCollection<AdminPlayerMapPreviewObjectUiItem0204>();
    public ObservableCollection<MapVisibilityOptionUiItem0204> TokenVisibilityOptions { get; } = MapVisibilityOptionUiItem0204.Create();
    public ObservableCollection<string> LocationToolOptions { get; } = new ObservableCollection<string> { "SelectMove", "PaintTile", "EraseTile", "StampAsset", "WallTool", "DoorTool", "RoomTool", "RoadTool", "ZoneTool", "RotateResize", "Rectangle", "Circle", "Line", "Polyline", "Polygon", "Text", "Stamp" };
    public ObservableCollection<string> LocationLayerKindOptions { get; } = new ObservableCollection<string> { "Terrain", "Buildings", "Roads", "Walls", "Objects", "Labels", "GmNotes" };
    public ObservableCollection<string> LocationObjectKindOptions { get; } = new ObservableCollection<string>
    {
        "TerrainZone", "Building", "Room", "Wall", "Road", "Alley", "Door", "Entrance", "Exit",
        "Cover", "Obstacle", "HazardZone", "MarketStall", "ShopArea", "TavernArea", "StorageArea",
        "ObjectiveZone", "SpawnZone", "Decoration", "TextLabel", "GmNote"
    };
    public ObservableCollection<MapVisibilityOptionUiItem0204> LocationVisibilityOptions { get; } = MapVisibilityOptionUiItem0204.Create();
    public ObservableCollection<string> LocationLinkedEntityTypeOptions { get; } = new ObservableCollection<string> { "None", "Shop", "Npc", "Faction", "Quest", "Location", "Object" };
    public ObservableCollection<string> VisualModeOptions { get; } = new ObservableCollection<string> { "Карта", "Схема" };
    public ObservableCollection<string> AssetCategoryOptions { get; } = new ObservableCollection<string>
    {
        "Местность", "Дороги", "Здания", "Интерьер", "Рынок / магазин", "Улица", "Лагерь", "Опасности", "Декор", "Зоны"
    };
    public ObservableCollection<LocationMapOptionUiItem> MaterialOptions { get; } = new ObservableCollection<LocationMapOptionUiItem>
    {
        new("grass", "Трава"),
        new("dirt", "Земля"),
        new("mud", "Грязь"),
        new("sand", "Песок"),
        new("stone", "Камень"),
        new("cobblestone", "Булыжная мостовая"),
        new("water", "Вода"),
        new("shallow_water", "Мелкая вода"),
        new("wood_floor", "Деревянный пол"),
        new("wood_planks", "Доски"),
        new("stone_floor", "Каменный пол"),
        new("stone_tiles", "Каменные плиты"),
        new("tavern_floor", "Пол трактира"),
        new("shop_floor", "Пол магазина"),
        new("warehouse_floor", "Пол склада"),
        new("roof_tile", "Черепица"),
        new("road_dirt", "Грунтовая дорога"),
        new("alley_stone", "Каменный переулок"),
        new("market_square_cobble", "Рыночная площадь"),
        new("bridge_wood", "Деревянный мост"),
        new("packed_dirt", "Утоптанная дорога"),
        new("dark_stone", "Тёмный камень"),
        new("warm_wood", "Тёплое дерево"),
        new("canvas_red", "Красный навес"),
        new("iron_wood", "Ворота / железо"),
        new("hazard", "Опасность"),
        new("hazard_red_overlay", "Красная опасная зона"),
        new("objective_gold_overlay", "Золотая цель"),
        new("spawn_blue_overlay", "Синяя зона старта"),
        new("gm_overlay", "GM overlay")
    };
    public ObservableCollection<LocationMapOptionUiItem> TextureOptions { get; } = new ObservableCollection<LocationMapOptionUiItem>
    {
        new("grass_noise", "Травяной шум"),
        new("dirt_track", "Грунтовая колея"),
        new("stone_tiles", "Каменные плиты"),
        new("sand_dots", "Песчаные точки"),
        new("mud_mottle", "Грязевая пятнистость"),
        new("water_ripple", "Рябь воды"),
        new("wood_planks", "Доски"),
        new("cobble_small", "Мелкий булыжник"),
        new("roof_tiles", "Черепица"),
        new("narrow_stone", "Узкая каменная кладка"),
        new("canvas_stripe", "Полосатый навес"),
        new("gate_planks", "Воротные доски"),
        new("hazard_cross", "Опасная штриховка")
        ,new("objective_hatch", "Целевая штриховка")
        ,new("spawn_grid", "Стартовая сетка")
    };
    public ObservableCollection<string> TokenTypeOptions { get; } = new ObservableCollection<string>
    {
        "Party",
        "PlayerCharacter",
        "Companion",
        "Npc",
        "Enemy",
        "Object",
        "Hazard",
        "Objective",
        "Vehicle",
        "GmNote"
    };

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
    public ICommand AddTokenCommand { get; }
    public ICommand MoveTokenCommand { get; }
    public ICommand SaveTokenCommand { get; }
    public ICommand ArchiveTokenCommand { get; }
    public ICommand CreateLayerCommand { get; }
    public ICommand SaveLayerCommand { get; }
    public ICommand ArchiveLayerCommand { get; }
    public ICommand MoveLayerUpCommand { get; }
    public ICommand MoveLayerDownCommand { get; }
    public ICommand ToggleLayerLockCommand { get; }
    public ICommand AddShapeCommand { get; }
    public ICommand SaveShapeCommand { get; }
    public ICommand MoveShapeCommand { get; }
    public ICommand ResizeShapeCommand { get; }
    public ICommand DuplicateShapeCommand { get; }
    public ICommand ArchiveShapeCommand { get; }
    public ICommand PaintTileCommand { get; }
    public ICommand EraseTileCommand { get; }
    public ICommand StampAssetCommand { get; }
    public ICommand SaveAssetInstanceCommand { get; }
    public ICommand ArchiveAssetInstanceCommand { get; }
    public ICommand UndoEditorCommand { get; }
    public ICommand RedoEditorCommand { get; }
    public ICommand DeleteSelectedEditorObjectCommand { get; }
    public ICommand SetPaintTileToolCommand { get; }
    public ICommand SetEraseTileToolCommand { get; }
    public ICommand SetStampAssetToolCommand { get; }
    public ICommand SetWallToolCommand { get; }
    public ICommand SetDoorToolCommand { get; }
    public ICommand SetRoomToolCommand { get; }
    public ICommand SetRoadToolCommand { get; }
    public ICommand SetZoneToolCommand { get; }
    public ICommand SetMapVisualModeCommand { get; }
    public ICommand SetSchematicVisualModeCommand { get; }
    public ICommand RefreshFogCommand { get; }
    public ICommand PaintFogCommand { get; }
    public ICommand RevealAllFogCommand { get; }
    public ICommand HideAllFogCommand { get; }
    public ICommand ClearFogCommand { get; }
    public ICommand ResetFogCommand { get; }
    public ICommand RefreshActiveMapCommand { get; }
    public ICommand SetActiveMapCommand { get; }
    public ICommand ClearActiveMapCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetViewCommand { get; }
    public ICommand FitToMapCommand { get; }
    public ICommand SetOneHundredPercentCommand { get; }
    public ICommand ClearErrorCommand { get; }
    public ICommand LoadPlayerPreviewCommand { get; }
    public ICommand SelectPlayerPreviewCharacterCommand { get; }

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
                Notify(nameof(CanWorkWithTokens));
                Notify(nameof(CanWorkWithLocationEditor));
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
                Notify(nameof(CanWorkWithLocationEditor));
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
                Notify(nameof(CanWorkWithTokens));
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
    public bool CanWorkWithTokens => CanWorkWithSelectedMap && IsSceneMarkersEnabled;
    public bool CanWorkWithLocationEditor => CanWorkWithSelectedMap;
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
        ? ActiveMapName
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
                Notify(nameof(CanWorkWithTokens));
                Notify(nameof(CanWorkWithLocationEditor));
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
                    MarkerVisibility = value.Visibility;
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

    public SceneTokenUiItem? SelectedToken
    {
        get => _selectedToken;
        set
        {
            if (_selectedToken != value)
            {
                _selectedToken = value;
                foreach (var token in Tokens) token.IsSelected = ReferenceEquals(token, value);
                Notify();
                Notify(nameof(SelectedTokenSummary));
                Notify(nameof(SelectedTokenCardText));
                if (value != null)
                {
                    TokenName = value.DisplayName;
                    TokenType = value.TokenType;
                    TokenX = value.X;
                    TokenY = value.Y;
                    TokenVisibility = value.Visibility;
                    TokenDescriptionPlayer = value.DescriptionPlayer;
                    TokenDescriptionGm = value.DescriptionGm;
                    TokenLinkedEntityType = value.LinkedEntityType;
                    TokenLinkedEntityId = value.LinkedEntityId;
                    TokenCanJoinCombat = value.CanJoinCombat;
                    ClientLogService.Instance.Info("admin.map.scene.token.selected");
                }
            }
        }
    }

    public SceneMapLayerUiItem? SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            if (_selectedLayer == value) return;
            _selectedLayer = value;
            Notify();
            Notify(nameof(IsSelectedLayerLocked));
            Notify(nameof(SelectedLayerLockText));
            if (value != null)
            {
                LayerName = value.DisplayName;
                LayerKind = value.LayerKind;
                LayerSortOrder = value.SortOrder;
                LayerVisibleByDefault = value.IsVisibleByDefault;
                LayerVisibility = value.Visibility;
                ShapeLayerId = value.LayerId;
            }
        }
    }

    public SceneMapShapeUiItem? SelectedShape
    {
        get => _selectedShape;
        set
        {
            if (_selectedShape == value) return;
            _selectedShape = value;
            foreach (var shape in LocationShapes) shape.IsSelected = ReferenceEquals(shape, value);
            if (value != null)
            {
                SelectedTilePatch = null;
                SelectedAssetInstance = null;
            }
            Notify();
            Notify(nameof(SelectedShapeSummary));
            Notify(nameof(SelectedShapeCardText));
            Notify(nameof(EditorSelectionSummary));
            Notify(nameof(EditorDeleteConfirmationText));
            if (value != null)
            {
                ShapeName = value.DisplayName;
                ShapeDescriptionPlayer = value.DescriptionPlayer;
                ShapeDescriptionGm = value.DescriptionGm;
                ShapeKind = value.ShapeKind;
                ObjectKind = value.ObjectKind;
                ShapeLayerId = value.LayerId;
                ShapeX = value.X;
                ShapeY = value.Y;
                ShapeWidth = value.Width;
                ShapeHeight = value.Height;
                ShapeRadius = value.Radius;
                ShapeRotationDegrees = value.RotationDegrees;
                ShapePoints = value.Points;
                ShapeText = value.Text;
                ShapeFillKey = value.FillKey;
                ShapeStrokeKey = value.StrokeKey;
                ShapeOpacity = value.Opacity;
                ShapeMaterialKey = value.MaterialKey;
                ShapeTextureKey = value.TextureKey;
                ShapePatternKey = value.PatternKey;
                ShapeAssetKey = value.AssetKey;
                ShapeVisualStyleKey = value.VisualStyleKey;
                ShapeRenderMode = value.RenderMode;
                ShapeGridSnapEnabled = value.GridSnapEnabled;
                ShapeVisualOpacity = value.VisualOpacity;
                ShapeStrokeThickness = value.StrokeThickness;
                ShapeZIndex = value.ZIndex;
                ShapeSortOrder = value.SortOrder;
                ShapeVisibility = value.Visibility;
                ShapeBlocksMovement = value.BlocksMovement;
                ShapeBlocksVision = value.BlocksVision;
                ShapeProvidesCover = value.ProvidesCover;
                ShapeIsInteractable = value.IsInteractable;
                ShapeLinkedEntityType = value.LinkedEntityType;
                ShapeLinkedEntityId = value.LinkedEntityId;
                ClientLogService.Instance.Info("admin.map.location.shape.selected");
            }
        }
    }

    public SceneMapTileLayerUiItem? SelectedTileLayer
    {
        get => _selectedTileLayer;
        set
        {
            if (_selectedTileLayer == value) return;
            _selectedTileLayer = value;
            Notify();
            if (value != null)
                TileSizeMeters = value.TileSizeMeters;
        }
    }

    public SceneMapTilePatchUiItem? SelectedTilePatch
    {
        get => _selectedTilePatch;
        set
        {
            if (_selectedTilePatch == value) return;
            _selectedTilePatch = value;
            foreach (var patch in TilePatches) patch.IsSelected = ReferenceEquals(patch, value);
            if (value != null)
            {
                _selectedShape = null;
                foreach (var shape in LocationShapes) shape.IsSelected = false;
                _selectedAssetInstance = null;
                foreach (var asset in AssetInstances) asset.IsSelected = false;
                Notify(nameof(SelectedShape));
                Notify(nameof(SelectedAssetInstance));
            }
            Notify();
            Notify(nameof(SelectedTilePatchPropertiesText));
            Notify(nameof(EditorSelectionSummary));
            Notify(nameof(EditorDeleteConfirmationText));
            if (value != null)
            {
                ShapeMaterialKey = value.MaterialKey;
                ShapeTextureKey = value.TextureKey;
                ShapeX = value.X;
                ShapeY = value.Y;
                ShapeWidth = value.Width;
                ShapeHeight = value.Height;
                ShapeRotationDegrees = value.RotationDegrees;
                ShapeOpacity = value.Opacity;
                ShapeSortOrder = value.SortOrder;
                ShapeVisibility = value.Visibility;
            }
        }
    }

    public SceneMapAssetInstanceUiItem? SelectedAssetInstance
    {
        get => _selectedAssetInstance;
        set
        {
            if (_selectedAssetInstance == value) return;
            _selectedAssetInstance = value;
            foreach (var asset in AssetInstances) asset.IsSelected = ReferenceEquals(asset, value);
            if (value != null)
            {
                _selectedShape = null;
                foreach (var shape in LocationShapes) shape.IsSelected = false;
                _selectedTilePatch = null;
                foreach (var patch in TilePatches) patch.IsSelected = false;
                Notify(nameof(SelectedShape));
                Notify(nameof(SelectedTilePatch));
            }
            Notify();
            Notify(nameof(SelectedAssetPropertiesText));
            Notify(nameof(EditorSelectionSummary));
            Notify(nameof(EditorDeleteConfirmationText));
            if (value != null)
            {
                ShapeAssetKey = value.AssetKey;
                ShapeName = value.DisplayName;
                ObjectKind = value.ObjectKind;
                ShapeX = value.X;
                ShapeY = value.Y;
                ShapeWidth = value.Width;
                ShapeHeight = value.Height;
                ShapeRotationDegrees = value.RotationDegrees;
                ShapeZIndex = value.ZIndex;
                ShapeVisibility = value.Visibility;
                ShapeDescriptionPlayer = value.DescriptionPlayer;
                ShapeDescriptionGm = value.DescriptionGm;
                ShapeBlocksMovement = value.BlocksMovement;
                ShapeBlocksVision = value.BlocksVision;
                ShapeProvidesCover = value.ProvidesCover;
                ShapeIsInteractable = value.IsInteractable;
                ShapeLinkedEntityType = value.LinkedEntityType;
                ShapeLinkedEntityId = value.LinkedEntityId;
            }
        }
    }

    public string NewMapName { get => _newMapName; set { if (_newMapName != value) { _newMapName = value; Notify(); } } }
    public string NewMapDescription { get => _newMapDescription; set { if (_newMapDescription != value) { _newMapDescription = value; Notify(); } } }
    public int NewMapWidthMeters { get => _newMapWidthMeters; set { if (_newMapWidthMeters != value) { _newMapWidthMeters = value; Notify(); } } }
    public int NewMapHeightMeters { get => _newMapHeightMeters; set { if (_newMapHeightMeters != value) { _newMapHeightMeters = value; Notify(); } } }
    public int NewGridCellSizeMeters { get => _newGridCellSizeMeters; set { if (_newGridCellSizeMeters != value) { _newGridCellSizeMeters = value; Notify(); } } }
    public bool ShowGrid { get => _showGrid; set { if (_showGrid != value) { _showGrid = value; Notify(); RebuildCanvas(); } } }
    public bool ShowCoordinates { get => _showCoordinates; set { if (_showCoordinates != value) { _showCoordinates = value; Notify(); } } }

    public string MarkerName { get => _markerName; set { if (_markerName != value) { _markerName = value; Notify(); } } }
    public string MarkerType { get => _markerType; set { if (_markerType != value) { _markerType = value; Notify(); } } }
    public double MarkerX { get => _markerX; set { if (Math.Abs(_markerX - value) > 0.0001) { _markerX = value; Notify(); } } }
    public double MarkerY { get => _markerY; set { if (Math.Abs(_markerY - value) > 0.0001) { _markerY = value; Notify(); } } }
    public string MarkerIconKey { get => _markerIconKey; set { if (_markerIconKey != value) { _markerIconKey = value; Notify(); } } }
    public string MarkerColorKey { get => _markerColorKey; set { if (_markerColorKey != value) { _markerColorKey = value; Notify(); } } }
    public bool MarkerPlayerVisible { get => _markerPlayerVisible; set { if (_markerPlayerVisible != value) { _markerPlayerVisible = value; Notify(); } } }
    public string MarkerVisibility
    {
        get => _markerVisibility;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? "Hidden" : value;
            if (_markerVisibility != next)
            {
                _markerVisibility = next;
                _markerPlayerVisible = string.Equals(next, "PlayerVisible", StringComparison.OrdinalIgnoreCase);
                Notify();
                Notify(nameof(MarkerPlayerVisible));
            }
        }
    }
    public string MarkerLinkedEntityType { get => _markerLinkedEntityType; set { if (_markerLinkedEntityType != value) { _markerLinkedEntityType = value; Notify(); } } }
    public string MarkerLinkedEntityId { get => _markerLinkedEntityId; set { if (_markerLinkedEntityId != value) { _markerLinkedEntityId = value; Notify(); } } }
    public string MarkerCardTitle { get => _markerCardTitle; set { if (_markerCardTitle != value) { _markerCardTitle = value; Notify(); } } }
    public string MarkerCardDescription { get => _markerCardDescription; set { if (_markerCardDescription != value) { _markerCardDescription = value; Notify(); } } }
    public string MarkerPublicNotes { get => _markerPublicNotes; set { if (_markerPublicNotes != value) { _markerPublicNotes = value; Notify(); } } }
    public string MarkerGmNotes { get => _markerGmNotes; set { if (_markerGmNotes != value) { _markerGmNotes = value; Notify(); } } }
    public string TokenSummaryText => Tokens.Count == 0
        ? "На карте сцены нет токенов."
        : "Токены: " + string.Join(" · ", Tokens.Select(token => token.DisplayName));
    public string SelectedTokenSummary => SelectedToken == null
        ? "Токен не выбран."
        : $"{SelectedToken.DisplayName} · {SelectedToken.TokenTypeDisplay} · X={SelectedToken.X:0.##}, Y={SelectedToken.Y:0.##}";
    public string SelectedTokenCardText => SelectedToken == null
        ? "Выберите токен на карте или в списке."
        : $"{SelectedToken.VisibilityDisplay} · {SelectedToken.BindingDisplayText}";
    public bool ShowTokenLayer { get => _showTokenLayer; set { if (_showTokenLayer != value) { _showTokenLayer = value; Notify(); RebuildVisibleTokens(); } } }
    public bool ShowGmOnlyLayer { get => _showGmOnlyLayer; set { if (_showGmOnlyLayer != value) { _showGmOnlyLayer = value; Notify(); RebuildVisibleTokens(); RebuildVisibleLocationShapes(); RebuildVisibleTilePatches(); RebuildVisibleAssetInstances(); } } }
    public bool ShowHiddenLayer { get => _showHiddenLayer; set { if (_showHiddenLayer != value) { _showHiddenLayer = value; Notify(); RebuildVisibleTokens(); RebuildVisibleLocationShapes(); RebuildVisibleTilePatches(); RebuildVisibleAssetInstances(); } } }
    public string SelectedPlayerPreviewCharacterId
    {
        get => _selectedPlayerPreviewCharacterId;
        set
        {
            if (_selectedPlayerPreviewCharacterId == value) return;
            _selectedPlayerPreviewCharacterId = value ?? string.Empty;
            PlayerPreviewCharacterName = PlayerPreviewCharacterOptions.FirstOrDefault(x => string.Equals(x.Id, value, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? "Персонаж не выбран";
            Notify();
        }
    }
    public NriReferenceOption? SelectedPlayerPreviewCharacterOption
    {
        get => _selectedPlayerPreviewCharacterOption;
        set
        {
            if (ReferenceEquals(_selectedPlayerPreviewCharacterOption, value)) return;
            _selectedPlayerPreviewCharacterOption = value;
            Notify();
            if (value != null) SelectedPlayerPreviewCharacterId = value.Id;
        }
    }
    public string PlayerPreviewCharacterName { get => _playerPreviewCharacterName; private set { if (_playerPreviewCharacterName != value) { _playerPreviewCharacterName = value; Notify(); } } }
    public string PlayerPreviewMapName { get => _playerPreviewMapName; private set { if (_playerPreviewMapName != value) { _playerPreviewMapName = value; Notify(); } } }
    public string PlayerPreviewSummary { get => _playerPreviewSummary; private set { if (_playerPreviewSummary != value) { _playerPreviewSummary = value; Notify(); } } }
    public bool IsPlayerPreviewVisible { get => _isPlayerPreviewVisible; private set { if (_isPlayerPreviewVisible != value) { _isPlayerPreviewVisible = value; Notify(); } } }
    public string VisualMode
    {
        get => _visualMode;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? "Карта" : value;
            if (_visualMode == next) return;
            _visualMode = next;
            Notify();
            Notify(nameof(IsMapVisualMode));
            Notify(nameof(IsSchematicVisualMode));
            RebuildVisibleLocationShapes();
            RebuildVisibleTilePatches();
            RebuildVisibleAssetInstances();
        }
    }
    public bool IsMapVisualMode => string.Equals(VisualMode, "Карта", StringComparison.OrdinalIgnoreCase);
    public bool IsSchematicVisualMode => !IsMapVisualMode;
    public string SelectedAssetCategory
    {
        get => _selectedAssetCategory;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? "Рынок / магазин" : value;
            if (_selectedAssetCategory == next) return;
            _selectedAssetCategory = next;
            Notify();
            Notify(nameof(FilteredLocationAssets));
            SelectedAsset = FilteredLocationAssets.FirstOrDefault() ?? BuiltInLocationAssets.FirstOrDefault();
        }
    }
    public IEnumerable<LocationMapAssetUiItem> FilteredLocationAssets => BuiltInLocationAssets.Where(asset => string.Equals(asset.Category, SelectedAssetCategory, StringComparison.OrdinalIgnoreCase));
    public LocationMapAssetUiItem? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (_selectedAsset == value) return;
            _selectedAsset = value;
            Notify();
            Notify(nameof(SelectedAssetCardText));
            if (value == null) return;
            LocationTool = value.RenderMode == "AssetStamp" ? "StampAsset" : value.ShapeKind;
            ObjectKind = value.DefaultObjectKind;
            ShapeKind = value.ShapeKind;
            ShapeMaterialKey = value.MaterialKey;
            ShapeTextureKey = value.TextureKey;
            ShapePatternKey = value.PatternKey;
            ShapeAssetKey = value.AssetKey;
            ShapeVisualStyleKey = value.VisualStyleKey;
            ShapeRenderMode = value.RenderMode;
            ShapeWidth = value.DefaultWidth;
            ShapeHeight = value.DefaultHeight;
            ShapeStrokeThickness = value.StrokeThickness;
            ShapeVisualOpacity = value.VisualOpacity;
        }
    }
    public string SelectedAssetCardText => SelectedAsset == null
        ? "Выберите ассет из палитры."
        : $"{SelectedAsset.DisplayName} · {SelectedAsset.AssetKindDisplay} · {SelectedAsset.Category}";
    public double GridOpacity { get => _gridOpacity; set { var next = Math.Max(0d, Math.Min(1d, value)); if (Math.Abs(_gridOpacity - next) > 0.0001) { _gridOpacity = next; Notify(); } } }
    public double BrushSizeMeters { get => _brushSizeMeters; set { var next = Math.Max(1d, Math.Min(200d, value)); if (Math.Abs(_brushSizeMeters - next) > 0.0001) { _brushSizeMeters = next; Notify(); } } }
    public double TileSizeMeters { get => _tileSizeMeters; set { var next = Math.Max(1d, Math.Min(100d, value)); if (Math.Abs(_tileSizeMeters - next) > 0.0001) { _tileSizeMeters = next; Notify(); } } }
    public bool SnapToGrid { get => _snapToGrid; set { if (_snapToGrid != value) { _snapToGrid = value; Notify(); Notify(nameof(SnapSummary)); } } }
    public double SnapStepMeters
    {
        get => _snapStepMeters;
        set
        {
            var next = new[] { 2d, 5d, 10d, 25d, 50d }.OrderBy(candidate => Math.Abs(candidate - value)).First();
            if (Math.Abs(_snapStepMeters - next) < 0.0001) return;
            _snapStepMeters = next;
            Notify();
            Notify(nameof(SnapSummary));
        }
    }
    public ObservableCollection<double> SnapStepOptions { get; } = new ObservableCollection<double> { 2d, 5d, 10d, 25d, 50d };
    public string SnapSummary => SnapToGrid ? $"Привязка: {SnapStepMeters:0.#} м" : "Привязка выключена";
    public Visibility PlacementGhostVisibility { get => _placementGhostVisibility; private set { if (_placementGhostVisibility != value) { _placementGhostVisibility = value; Notify(); } } }
    public double PlacementGhostX { get => _placementGhostX; private set { if (Math.Abs(_placementGhostX - value) > 0.01) { _placementGhostX = value; Notify(); } } }
    public double PlacementGhostY { get => _placementGhostY; private set { if (Math.Abs(_placementGhostY - value) > 0.01) { _placementGhostY = value; Notify(); } } }
    public double PlacementGhostWidth { get => _placementGhostWidth; private set { if (Math.Abs(_placementGhostWidth - value) > 0.01) { _placementGhostWidth = value; Notify(); } } }
    public double PlacementGhostHeight { get => _placementGhostHeight; private set { if (Math.Abs(_placementGhostHeight - value) > 0.01) { _placementGhostHeight = value; Notify(); } } }
    public string PlacementGhostLabel { get => _placementGhostLabel; private set { if (_placementGhostLabel != value) { _placementGhostLabel = value; Notify(); } } }
    public string PaletteSearch
    {
        get => _paletteSearch;
        set { if (_paletteSearch != value) { _paletteSearch = value ?? string.Empty; Notify(); RefreshPaletteFilter(); } }
    }
    public bool CanUndoEditor => _editorHistory.CanUndo && CanWorkWithLocationEditor;
    public bool CanRedoEditor => _editorHistory.CanRedo && CanWorkWithLocationEditor;
    public bool IsSelectedLayerLocked => SelectedLayer?.IsLocked == true;
    public string SelectedLayerLockText => IsSelectedLayerLocked ? "Разблокировать слой" : "Заблокировать слой";
    public string EditorSelectionSummary => SelectedShape != null
        ? $"{SelectedShape.DisplayName} · {SelectedShape.ObjectKindDisplay}"
        : SelectedAssetInstance != null ? $"{SelectedAssetInstance.DisplayName} · объект"
        : SelectedTilePatch != null ? $"{SelectedTilePatch.MaterialDisplay} · материал"
        : "Объект не выбран";
    public string EditorDeleteConfirmationText
    {
        get
        {
            var name = SelectedShape?.DisplayName ?? SelectedAssetInstance?.DisplayName ?? SelectedTilePatch?.MaterialDisplay ?? "выбранный объект";
            var layerId = SelectedShape?.LayerId ?? SelectedAssetInstance?.LayerId ?? SelectedTilePatch?.TileLayerId ?? string.Empty;
            var layerName = LocationLayers.FirstOrDefault(item => item.LayerId == layerId)?.DisplayName
                ?? TileLayers.FirstOrDefault(item => item.TileLayerId == layerId)?.DisplayName
                ?? "текущий слой";
            return $"Переместить «{name}» из слоя «{layerName}» в архив?";
        }
    }
    private string ResolveLayerDisplayName(string layerId)
        => LocationLayers.FirstOrDefault(item => string.Equals(item.LayerId, layerId, StringComparison.OrdinalIgnoreCase))?.DisplayName
           ?? TileLayers.FirstOrDefault(item => string.Equals(item.TileLayerId, layerId, StringComparison.OrdinalIgnoreCase))?.DisplayName
           ?? string.Empty;
    public string SelectedTilePatchPropertiesText => SelectedTilePatch == null
        ? "Патч материала не выбран."
        : $"{SelectedTilePatch.MaterialDisplay} · {SelectedTilePatch.Width:0.#}×{SelectedTilePatch.Height:0.#} м · X={SelectedTilePatch.X:0.#}, Y={SelectedTilePatch.Y:0.#}";
    public string SelectedAssetPropertiesText => SelectedAssetInstance == null
        ? "Asset stamp не выбран."
        : $"{SelectedAssetInstance.DisplayName} · {SelectedAssetInstance.AssetKindDisplay} · X={SelectedAssetInstance.X:0.#}, Y={SelectedAssetInstance.Y:0.#}";
    public string TokenName { get => _tokenName; set { if (_tokenName != value) { _tokenName = value; Notify(); } } }
    public string TokenType { get => _tokenType; set { if (_tokenType != value) { _tokenType = value; Notify(); } } }
    public double TokenX { get => _tokenX; set { if (Math.Abs(_tokenX - value) > 0.0001) { _tokenX = value; Notify(); } } }
    public double TokenY { get => _tokenY; set { if (Math.Abs(_tokenY - value) > 0.0001) { _tokenY = value; Notify(); } } }
    public string TokenVisibility { get => _tokenVisibility; set { if (_tokenVisibility != value) { _tokenVisibility = value; Notify(); } } }
    public string TokenDescriptionPlayer { get => _tokenDescriptionPlayer; set { if (_tokenDescriptionPlayer != value) { _tokenDescriptionPlayer = value; Notify(); } } }
    public string TokenDescriptionGm { get => _tokenDescriptionGm; set { if (_tokenDescriptionGm != value) { _tokenDescriptionGm = value; Notify(); } } }
    public string TokenLinkedEntityType { get => _tokenLinkedEntityType; set { if (_tokenLinkedEntityType != value) { _tokenLinkedEntityType = value; Notify(); } } }
    public string TokenLinkedEntityId { get => _tokenLinkedEntityId; set { if (_tokenLinkedEntityId != value) { _tokenLinkedEntityId = value; Notify(); } } }
    public bool TokenCanJoinCombat { get => _tokenCanJoinCombat; set { if (_tokenCanJoinCombat != value) { _tokenCanJoinCombat = value; Notify(); } } }
    public string LocationTool { get => _locationTool; set { if (_locationTool != value) { _locationTool = value; Notify(); } } }
    public string LayerName { get => _layerName; set { if (_layerName != value) { _layerName = value; Notify(); } } }
    public string LayerKind { get => _layerKind; set { if (_layerKind != value) { _layerKind = value; Notify(); } } }
    public int LayerSortOrder { get => _layerSortOrder; set { if (_layerSortOrder != value) { _layerSortOrder = value; Notify(); } } }
    public bool LayerVisibleByDefault { get => _layerVisibleByDefault; set { if (_layerVisibleByDefault != value) { _layerVisibleByDefault = value; Notify(); } } }
    public string LayerVisibility { get => _layerVisibility; set { if (_layerVisibility != value) { _layerVisibility = value; Notify(); } } }
    public string ShapeName { get => _shapeName; set { if (_shapeName != value) { _shapeName = value; Notify(); } } }
    public string ShapeDescriptionPlayer { get => _shapeDescriptionPlayer; set { if (_shapeDescriptionPlayer != value) { _shapeDescriptionPlayer = value; Notify(); } } }
    public string ShapeDescriptionGm { get => _shapeDescriptionGm; set { if (_shapeDescriptionGm != value) { _shapeDescriptionGm = value; Notify(); } } }
    public string ShapeKind { get => _shapeKind; set { if (_shapeKind != value) { _shapeKind = value; Notify(); } } }
    public string ObjectKind { get => _objectKind; set { if (_objectKind != value) { _objectKind = value; Notify(); } } }
    public string ShapeLayerId { get => _shapeLayerId; set { if (_shapeLayerId != value) { _shapeLayerId = value; Notify(); } } }
    public double ShapeX { get => _shapeX; set { if (Math.Abs(_shapeX - value) > 0.0001) { _shapeX = value; Notify(); } } }
    public double ShapeY { get => _shapeY; set { if (Math.Abs(_shapeY - value) > 0.0001) { _shapeY = value; Notify(); } } }
    public double ShapeWidth { get => _shapeWidth; set { if (Math.Abs(_shapeWidth - value) > 0.0001) { _shapeWidth = value; Notify(); } } }
    public double ShapeHeight { get => _shapeHeight; set { if (Math.Abs(_shapeHeight - value) > 0.0001) { _shapeHeight = value; Notify(); } } }
    public double ShapeRadius { get => _shapeRadius; set { if (Math.Abs(_shapeRadius - value) > 0.0001) { _shapeRadius = value; Notify(); } } }
    public double ShapeRotationDegrees { get => _shapeRotationDegrees; set { if (Math.Abs(_shapeRotationDegrees - value) > 0.0001) { _shapeRotationDegrees = value; Notify(); } } }
    public string ShapePoints { get => _shapePoints; set { if (_shapePoints != value) { _shapePoints = value; Notify(); } } }
    public string ShapeText { get => _shapeText; set { if (_shapeText != value) { _shapeText = value; Notify(); } } }
    public string ShapeFillKey { get => _shapeFillKey; set { if (_shapeFillKey != value) { _shapeFillKey = value; Notify(); } } }
    public string ShapeStrokeKey { get => _shapeStrokeKey; set { if (_shapeStrokeKey != value) { _shapeStrokeKey = value; Notify(); } } }
    public double ShapeOpacity { get => _shapeOpacity; set { if (Math.Abs(_shapeOpacity - value) > 0.0001) { _shapeOpacity = value; Notify(); } } }
    public string ShapeMaterialKey { get => _shapeMaterialKey; set { if (_shapeMaterialKey != value) { _shapeMaterialKey = value; Notify(); } } }
    public string ShapeTextureKey { get => _shapeTextureKey; set { if (_shapeTextureKey != value) { _shapeTextureKey = value; Notify(); } } }
    public string ShapePatternKey { get => _shapePatternKey; set { if (_shapePatternKey != value) { _shapePatternKey = value; Notify(); } } }
    public string ShapeAssetKey { get => _shapeAssetKey; set { if (_shapeAssetKey != value) { _shapeAssetKey = value; Notify(); } } }
    public string ShapeVisualStyleKey { get => _shapeVisualStyleKey; set { if (_shapeVisualStyleKey != value) { _shapeVisualStyleKey = value; Notify(); } } }
    public string ShapeRenderMode { get => _shapeRenderMode; set { if (_shapeRenderMode != value) { _shapeRenderMode = value; Notify(); } } }
    public bool ShapeGridSnapEnabled { get => _shapeGridSnapEnabled; set { if (_shapeGridSnapEnabled != value) { _shapeGridSnapEnabled = value; Notify(); } } }
    public double ShapeVisualOpacity { get => _shapeVisualOpacity; set { if (Math.Abs(_shapeVisualOpacity - value) > 0.0001) { _shapeVisualOpacity = value; Notify(); } } }
    public double ShapeStrokeThickness { get => _shapeStrokeThickness; set { if (Math.Abs(_shapeStrokeThickness - value) > 0.0001) { _shapeStrokeThickness = value; Notify(); } } }
    public int ShapeZIndex { get => _shapeZIndex; set { if (_shapeZIndex != value) { _shapeZIndex = value; Notify(); } } }
    public int ShapeSortOrder { get => _shapeSortOrder; set { if (_shapeSortOrder != value) { _shapeSortOrder = value; Notify(); } } }
    public string ShapeVisibility { get => _shapeVisibility; set { if (_shapeVisibility != value) { _shapeVisibility = value; Notify(); } } }
    public bool ShapeBlocksMovement { get => _shapeBlocksMovement; set { if (_shapeBlocksMovement != value) { _shapeBlocksMovement = value; Notify(); } } }
    public bool ShapeBlocksVision { get => _shapeBlocksVision; set { if (_shapeBlocksVision != value) { _shapeBlocksVision = value; Notify(); } } }
    public bool ShapeProvidesCover { get => _shapeProvidesCover; set { if (_shapeProvidesCover != value) { _shapeProvidesCover = value; Notify(); } } }
    public bool ShapeIsInteractable { get => _shapeIsInteractable; set { if (_shapeIsInteractable != value) { _shapeIsInteractable = value; Notify(); } } }
    public string ShapeLinkedEntityType { get => _shapeLinkedEntityType; set { if (_shapeLinkedEntityType != value) { _shapeLinkedEntityType = value; Notify(); } } }
    public string ShapeLinkedEntityId { get => _shapeLinkedEntityId; set { if (_shapeLinkedEntityId != value) { _shapeLinkedEntityId = value; Notify(); } } }
    public string SelectedShapeSummary => SelectedShape == null
        ? "Объект локации не выбран."
        : $"{SelectedShape.DisplayName} · {SelectedShape.ObjectKindDisplay} · X={SelectedShape.X:0.##}, Y={SelectedShape.Y:0.##}";
    public string SelectedShapeCardText => SelectedShape == null
        ? "Выберите объект на карте или в списке."
        : $"{SelectedShape.VisibilityDisplay} · слой {FirstNonEmpty(SelectedShape.LayerName, ResolveLayerDisplayName(SelectedShape.LayerId), "без названия")}";
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
    public string ZoomIndicator => _viewport.ZoomDisplay;
    public string CoordinateIndicator { get => _coordinateIndicator; private set { if (_coordinateIndicator != value) { _coordinateIndicator = value; Notify(); } } }
    public string GridStepLabel { get => _gridStepLabel; private set { if (_gridStepLabel != value) { _gridStepLabel = value; Notify(); } } }
    public bool CanZoomIn => _viewport.CanZoomIn;
    public bool CanZoomOut => _viewport.CanZoomOut;
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

            var flagMaps = ExtractFeatureFlagMaps(response.Payload).ToList();
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
            RefreshPlayerPreviewCharacters();
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

            LoadActiveMapLink();
            StatusMessage = Maps.Count == 0 ? "Карты сцены пока не созданы." : $"Загружено карт: {Maps.Count}.";
            LastRefreshAtUtc = DateTime.UtcNow;

            var activeMap = HasActiveMap && !string.IsNullOrWhiteSpace(ActiveMapId)
                ? Maps.FirstOrDefault(x => string.Equals(x.MapId, ActiveMapId, StringComparison.OrdinalIgnoreCase))
                : null;
            if (activeMap != null)
            {
                SelectedMap = activeMap;
            }
            else if (SelectedMap == null || !Maps.Any(x => x.MapId == SelectedMap.MapId))
            {
                SelectedMap = Maps.FirstOrDefault();
            }

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

    private void RefreshPlayerPreviewCharacters()
    {
        var selectedId = SelectedPlayerPreviewCharacterId;
        PlayerPreviewCharacterOptions.Clear();
        var response = _api.CharacterOwnershipList(new Dictionary<string, object>
        {
            ["campaignId"] = string.Empty,
            ["includeUnassigned"] = true,
            ["includeArchived"] = false
        });
        if (response.Status != ResponseStatus.Ok) return;
        foreach (var item in Dictionaries(Get(response.Payload, "items")))
        {
            var id = FirstNonEmpty(Str(Get(item, "characterId")), Str(Get(item, "id")));
            if (string.IsNullOrWhiteSpace(id)) continue;
            PlayerPreviewCharacterOptions.Add(new NriReferenceOption
            {
                Id = id,
                DisplayName = FirstNonEmpty(Str(Get(item, "characterDisplayName")), Str(Get(item, "displayName")), "Персонаж без имени"),
                TypeLabel = "Персонаж",
                StatusLabel = FirstNonEmpty(Str(Get(item, "assignmentStatus")), "Доступен")
            });
        }
        SelectedPlayerPreviewCharacterId = PlayerPreviewCharacterOptions.Any(x => string.Equals(x.Id, selectedId, StringComparison.OrdinalIgnoreCase))
            ? selectedId : PlayerPreviewCharacterOptions.FirstOrDefault()?.Id ?? string.Empty;
        SelectedPlayerPreviewCharacterOption = PlayerPreviewCharacterOptions.FirstOrDefault(x => string.Equals(x.Id, SelectedPlayerPreviewCharacterId, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyPlayerPreviewCharacterSelection(NriReferenceOption? option)
    {
        var selected = option ?? SelectedPlayerPreviewCharacterOption;
        if (selected == null) return;
        SelectedPlayerPreviewCharacterId = selected.Id;
    }

    private void LoadServerPlayerPreview()
    {
        if (SelectedMap == null)
        {
            ErrorMessage = "Сначала выберите карту сцены.";
            return;
        }
        if (string.IsNullOrWhiteSpace(SelectedPlayerPreviewCharacterId))
        {
            ErrorMessage = "Выберите тестового персонажа для предпросмотра.";
            return;
        }
        var response = _api.MapAdminPlayerPreviewGet(new Dictionary<string, object>
        {
            ["mapId"] = SelectedMap.MapId,
            ["characterId"] = SelectedPlayerPreviewCharacterId,
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId ?? string.Empty,
            ["activeGroupId"] = ActiveGroupId ?? string.Empty,
            ["includeMarkers"] = true
        });
        if (response.Status != ResponseStatus.Ok)
        {
            ErrorMessage = response.Message;
            IsPlayerPreviewVisible = false;
            return;
        }
        var map = AsMap(Get(response.Payload, "map"));
        var width = Math.Max(1d, Double(Get(map, "widthMeters"), 1d));
        var height = Math.Max(1d, Double(Get(map, "heightMeters"), 1d));
        PlayerPreviewObjects.Clear();
        foreach (var item in Dictionaries(Get(map, "objects")))
        {
            PlayerPreviewObjects.Add(new AdminPlayerMapPreviewObjectUiItem0204
            {
                DisplayName = Str(Get(item, "name")),
                Kind = Str(Get(item, "kind")),
                PixelX = Math.Max(2d, Math.Min(246d, Double(Get(item, "x"), 0d) / width * 248d)),
                PixelY = Math.Max(2d, Math.Min(126d, Double(Get(item, "y"), 0d) / height * 128d))
            });
        }
        PlayerPreviewMapName = FirstNonEmpty(Str(Get(map, "name")), "Карта сцены");
        PlayerPreviewSummary = $"Безопасная проекция для {PlayerPreviewCharacterName}: видимых объектов {PlayerPreviewObjects.Count}.";
        IsPlayerPreviewVisible = true;
        ErrorMessage = string.Empty;
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

            Tokens.Clear();
            foreach (var tokenPayload in Dictionaries(Get(response.Payload, "tokens")))
                Tokens.Add(SceneTokenUiItem.From(tokenPayload));
            Notify(nameof(TokenSummaryText));

            LocationLayers.Clear();
            foreach (var layerPayload in Dictionaries(Get(response.Payload, "layers")))
                LocationLayers.Add(SceneMapLayerUiItem.From(layerPayload));

            LocationShapes.Clear();
            foreach (var shapePayload in Dictionaries(Get(response.Payload, "shapes")))
                LocationShapes.Add(SceneMapShapeUiItem.From(shapePayload));

            TileLayers.Clear();
            foreach (var tileLayerPayload in Dictionaries(Get(response.Payload, "tileLayers")))
                TileLayers.Add(SceneMapTileLayerUiItem.From(tileLayerPayload));

            TilePatches.Clear();
            foreach (var tilePatchPayload in Dictionaries(Get(response.Payload, "tilePatches")))
                TilePatches.Add(SceneMapTilePatchUiItem.From(tilePatchPayload));

            AssetInstances.Clear();
            foreach (var assetPayload in Dictionaries(Get(response.Payload, "assetInstances")))
                AssetInstances.Add(SceneMapAssetInstanceUiItem.From(assetPayload));

            RefreshEditorMetadata();

            ApplyFogPayload(AsMap(Get(response.Payload, "fog")));

            SelectedMarker = Markers.FirstOrDefault();
            SelectedToken = Tokens.FirstOrDefault();
            SelectedLayer = LocationLayers.FirstOrDefault();
            SelectedShape = LocationShapes.FirstOrDefault();
            SelectedTileLayer = TileLayers.FirstOrDefault();
            SelectedTilePatch = TilePatches.FirstOrDefault();
            SelectedAssetInstance = AssetInstances.FirstOrDefault();
            RebuildCanvas();
            StatusMessage = $"Карта загружена: {SelectedMap.Name}. Тайлов: {TilePatches.Count}. Assets: {AssetInstances.Count}.";
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
                { "visibility", MarkerVisibility },
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
                { "visibility", MarkerVisibility },
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

    public void AddToken()
    {
        if (!CanWorkWithTokens || SelectedMap == null)
        {
            ErrorMessage = "Выберите карту и проверьте marker flags.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapTokenAdminCreate(new Dictionary<string, object>
            {
                { "mapKind", "Scene" },
                { "mapId", SelectedMap.MapId },
                { "worldId", CampaignId },
                { "sessionId", SessionId ?? string.Empty },
                { "displayName", FirstNonEmpty(TokenName, "Токен") },
                { "tokenType", FirstNonEmpty(TokenType, "Object") },
                { "x", TokenX },
                { "y", TokenY },
                { "visibility", FirstNonEmpty(TokenVisibility, "PlayerVisible") },
                { "descriptionPlayer", TokenDescriptionPlayer ?? string.Empty },
                { "descriptionGm", TokenDescriptionGm ?? string.Empty },
                { "linkedEntityType", TokenLinkedEntityType ?? string.Empty },
                { "linkedEntityId", TokenLinkedEntityId ?? string.Empty },
                { "canJoinCombat", TokenCanJoinCombat }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            var token = SceneTokenUiItem.From(AsMap(Get(response.Payload, "token")));
            if (string.IsNullOrWhiteSpace(token.TokenId))
                token.TokenId = Str(Get(response.Payload, "tokenId"));
            if (!string.IsNullOrWhiteSpace(token.TokenId))
            {
                Tokens.Add(token);
                SelectedToken = token;
                Notify(nameof(TokenSummaryText));
                RebuildCanvas();
            }

            StatusMessage = "Токен добавлен.";
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

    public void MoveToken()
    {
        if (!CanWorkWithTokens || SelectedToken == null)
        {
            ErrorMessage = "Выберите токен для перемещения.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapTokenAdminMove(new Dictionary<string, object>
            {
                { "tokenId", SelectedToken.TokenId },
                { "x", TokenX },
                { "y", TokenY },
                { "operationId", "admin-token-move-" + Guid.NewGuid().ToString("N") },
                { "expectedRevision", SelectedToken.Revision }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            SelectedToken.Apply(AsMap(Get(response.Payload, "token")));
            RebuildCanvas();
            StatusMessage = "Токен перемещён.";
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

    public void UpdateToken()
    {
        if (!CanWorkWithTokens || SelectedToken == null)
        {
            ErrorMessage = "Выберите токен для сохранения.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapTokenAdminUpdate(new Dictionary<string, object>
            {
                { "tokenId", SelectedToken.TokenId },
                { "displayName", FirstNonEmpty(TokenName, "Токен") },
                { "tokenType", FirstNonEmpty(TokenType, "Object") },
                { "x", TokenX },
                { "y", TokenY },
                { "visibility", FirstNonEmpty(TokenVisibility, "PlayerVisible") },
                { "descriptionPlayer", TokenDescriptionPlayer ?? string.Empty },
                { "descriptionGm", TokenDescriptionGm ?? string.Empty },
                { "linkedEntityType", TokenLinkedEntityType ?? string.Empty },
                { "linkedEntityId", TokenLinkedEntityId ?? string.Empty },
                { "canJoinCombat", TokenCanJoinCombat }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            SelectedToken.Apply(AsMap(Get(response.Payload, "token")));
            Notify(nameof(SelectedTokenSummary));
            Notify(nameof(SelectedTokenCardText));
            Notify(nameof(TokenSummaryText));
            RebuildCanvas();
            StatusMessage = "Токен сохранён.";
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

    public void ArchiveToken()
    {
        if (!CanWorkWithTokens || SelectedToken == null)
        {
            ErrorMessage = "Выберите токен для архивации.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Архивировать токен «{SelectedToken.DisplayName}»?",
            "Подтверждение архивации",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
            return;

        var tokenId = SelectedToken.TokenId;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapTokenAdminArchive(new Dictionary<string, object> { { "tokenId", tokenId } });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            var found = Tokens.FirstOrDefault(token => token.TokenId == tokenId);
            if (found != null) Tokens.Remove(found);
            SelectedToken = Tokens.FirstOrDefault();
            Notify(nameof(TokenSummaryText));
            RebuildCanvas();
            StatusMessage = "Токен архивирован.";
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

    public void CreateLayer()
    {
        if (!CanWorkWithLocationEditor || SelectedMap == null)
        {
            ErrorMessage = "Выберите карту сцены для создания слоя.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = MutateEditor("layer.create", string.Empty, string.Empty, 0L, null, new Dictionary<string, object>
            {
                { "displayName", FirstNonEmpty(LayerName, "Слой локации") },
                { "layerKind", FirstNonEmpty(LayerKind, "Objects") },
                { "sortOrder", LayerSortOrder },
                { "isVisible", LayerVisibleByDefault },
                { "visibility", FirstNonEmpty(LayerVisibility, "PlayerVisible") }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            var layerId = Str(Get(response.Payload, "targetId"));
            LoadSelectedMapAndReselect(layerId, "layer");
            StatusMessage = "Слой локации создан.";
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

    private void RefreshEditorMetadata()
    {
        if (SelectedMap == null) return;
        var response = _api.MapEditorAdminGetState(new Dictionary<string, object> { ["mapId"] = SelectedMap.MapId });
        if (response.Status != ResponseStatus.Ok)
        {
            WarningMessage = response.Message;
            return;
        }

        _canonicalEditorMapId = FirstNonEmpty(Str(Get(response.Payload, "mapId")), SelectedMap.MapId);
        _mapEditorRevision = Long(Get(response.Payload, "mapRevision"), 0L);
        foreach (var payload in Dictionaries(Get(response.Payload, "layers")))
        {
            var id = Str(Get(payload, "id"));
            var layer = LocationLayers.FirstOrDefault(item => string.Equals(item.LayerId, id, StringComparison.OrdinalIgnoreCase));
            if (layer != null) layer.ApplyEditorState(payload);
            var tileLayer = TileLayers.FirstOrDefault(item => string.Equals(item.TileLayerId, id, StringComparison.OrdinalIgnoreCase));
            if (tileLayer != null) tileLayer.ApplyEditorState(payload);
        }

        foreach (var payload in Dictionaries(Get(response.Payload, "objects")))
        {
            var id = Str(Get(payload, "id"));
            var kind = Str(Get(payload, "kind"));
            if (kind == "shape") LocationShapes.FirstOrDefault(item => item.ShapeId == id)?.ApplyEditorState(payload);
            else if (kind == "tilePatch") TilePatches.FirstOrDefault(item => item.TilePatchId == id)?.ApplyEditorState(payload);
            else if (kind == "assetInstance") AssetInstances.FirstOrDefault(item => item.AssetInstanceId == id)?.ApplyEditorState(payload);
        }
    }

    private ResponseEnvelope MutateEditor(
        string mutation,
        string targetId,
        string layerId,
        long? expectedLayerRevision,
        long? expectedObjectRevision,
        IDictionary<string, object>? values = null)
    {
        if (SelectedMap == null) return new ResponseEnvelope { Status = ResponseStatus.ValidationFailed, Message = "Карта сцены не выбрана." };
        var payload = new Dictionary<string, object>
        {
            ["operationId"] = Guid.NewGuid().ToString("N"),
            ["mapId"] = FirstNonEmpty(_canonicalEditorMapId, SelectedMap.MapId),
            ["mutation"] = mutation,
            ["targetId"] = targetId ?? string.Empty,
            ["layerId"] = layerId ?? string.Empty,
            ["expectedMapRevision"] = _mapEditorRevision,
            ["values"] = values == null ? new Dictionary<string, object>() : new Dictionary<string, object>(values)
        };
        if (expectedLayerRevision.HasValue) payload["expectedLayerRevision"] = expectedLayerRevision.Value;
        if (expectedObjectRevision.HasValue) payload["expectedObjectRevision"] = expectedObjectRevision.Value;
        var response = _api.MapEditorAdminMutate(payload);
        if (response.Status == ResponseStatus.Ok)
        {
            _mapEditorRevision = Long(Get(response.Payload, "mapRevision"), 0L);
            StatusMessage = response.Message;
            ErrorMessage = string.Empty;
        }
        else
        {
            ErrorMessage = response.Message;
        }
        return response;
    }

    private void MoveSelectedLayer(int direction)
    {
        if (SelectedLayer == null || SelectedMap == null) return;
        var ordered = LocationLayers.OrderBy(layer => layer.SortOrder).ThenBy(layer => layer.DisplayName).ToList();
        var index = ordered.IndexOf(SelectedLayer);
        var swapIndex = index + direction;
        if (index < 0 || swapIndex < 0 || swapIndex >= ordered.Count) return;
        var swapLayer = ordered[swapIndex];
        var values = new Dictionary<string, object> { ["swapLayerId"] = swapLayer.LayerId };
        if (MutateEditor("layer.reorder", SelectedLayer.LayerId, string.Empty, SelectedLayer.Revision, null, values).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203("layer.reorder", "layer.reorder", SelectedLayer.LayerId, SelectedLayer.LayerId,
            new Dictionary<string, object>(values), values));
        LoadSelectedMapAndReselect(SelectedLayer.LayerId, "layer");
    }

    private void ToggleSelectedLayerLock()
    {
        if (SelectedLayer == null) return;
        var previous = SelectedLayer.IsLocked;
        var values = new Dictionary<string, object> { ["isLocked"] = !previous, ["displayName"] = SelectedLayer.DisplayName, ["layerKind"] = SelectedLayer.LayerKind, ["sortOrder"] = SelectedLayer.SortOrder };
        if (MutateEditor("layer.setlock", SelectedLayer.LayerId, string.Empty, SelectedLayer.Revision, null, values).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203("layer.setlock", "layer.setlock", SelectedLayer.LayerId, SelectedLayer.LayerId,
            new Dictionary<string, object> { ["isLocked"] = previous, ["displayName"] = SelectedLayer.DisplayName, ["layerKind"] = SelectedLayer.LayerKind, ["sortOrder"] = SelectedLayer.SortOrder }, values));
        LoadSelectedMapAndReselect(SelectedLayer.LayerId, "layer");
    }

    private void RecordEditorHistory(MapEditorHistoryEntry0203 entry)
    {
        _editorHistory.Record(entry);
        Notify(nameof(CanUndoEditor));
        Notify(nameof(CanRedoEditor));
    }

    private void UndoEditor()
    {
        if (!_editorHistory.TryTakeUndo(out var entry)) return;
        if (!ExecuteHistoryMutation(entry.InverseMutation, entry, entry.InverseValues))
        {
            _editorHistory.TryTakeRedo(out _);
            return;
        }
        Notify(nameof(CanUndoEditor));
        Notify(nameof(CanRedoEditor));
        StatusMessage = "Последнее изменение отменено.";
    }

    private void RedoEditor()
    {
        if (!_editorHistory.TryTakeRedo(out var entry)) return;
        if (!ExecuteHistoryMutation(entry.RedoMutation, entry, entry.RedoValues))
        {
            _editorHistory.TryTakeUndo(out _);
            return;
        }
        Notify(nameof(CanUndoEditor));
        Notify(nameof(CanRedoEditor));
        StatusMessage = "Изменение повторено.";
    }

    private bool ExecuteHistoryMutation(string mutation, MapEditorHistoryEntry0203 entry, IDictionary<string, object> values)
    {
        RefreshEditorMetadata();
        var layerRevision = FindLayerRevision(entry.LayerId);
        var objectRevision = FindObjectRevision(entry.TargetId);
        var response = MutateEditor(mutation, entry.TargetId, entry.LayerId, layerRevision, mutation.StartsWith("layer.", StringComparison.Ordinal) ? null : objectRevision, values);
        if (response.Status != ResponseStatus.Ok) return false;
        LoadSelectedMapAndReselect(entry.TargetId, entry.Kind);
        return true;
    }

    private long? FindLayerRevision(string layerId)
    {
        var layer = LocationLayers.FirstOrDefault(item => item.LayerId == layerId);
        if (layer != null) return layer.Revision;
        return TileLayers.FirstOrDefault(item => item.TileLayerId == layerId)?.Revision;
    }

    private long? FindObjectRevision(string targetId)
    {
        var shape = LocationShapes.FirstOrDefault(item => item.ShapeId == targetId);
        if (shape != null) return shape.Revision;
        var tile = TilePatches.FirstOrDefault(item => item.TilePatchId == targetId);
        if (tile != null) return tile.Revision;
        return AssetInstances.FirstOrDefault(item => item.AssetInstanceId == targetId)?.Revision;
    }

    private void LoadSelectedMapAndReselect(string targetId, string kind)
    {
        LoadSelectedMap();
        if (kind == "layer") SelectedLayer = LocationLayers.FirstOrDefault(item => item.LayerId == targetId) ?? SelectedLayer;
        else if (kind == "shape") SelectedShape = LocationShapes.FirstOrDefault(item => item.ShapeId == targetId);
        else if (kind == "tilePatch") SelectedTilePatch = TilePatches.FirstOrDefault(item => item.TilePatchId == targetId);
        else if (kind == "assetInstance") SelectedAssetInstance = AssetInstances.FirstOrDefault(item => item.AssetInstanceId == targetId);
    }

    private void RefreshPaletteFilter()
    {
        var query = (_paletteSearch ?? string.Empty).Trim();
        FilteredPaletteAssets.Clear();
        foreach (var item in BuiltInLocationAssets.Where(item => query.Length == 0 || item.DisplayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 || item.Category.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0))
            FilteredPaletteAssets.Add(item);
    }

    private void DeleteSelectedEditorObject()
    {
        string mutation;
        string targetId;
        string layerId;
        long objectRevision;
        string kind;
        if (SelectedShape != null)
        {
            mutation = "shape.archive"; targetId = SelectedShape.ShapeId; layerId = SelectedShape.LayerId; objectRevision = SelectedShape.Revision; kind = "shape";
        }
        else if (SelectedAssetInstance != null)
        {
            mutation = "asset.archive"; targetId = SelectedAssetInstance.AssetInstanceId; layerId = SelectedAssetInstance.LayerId; objectRevision = SelectedAssetInstance.Revision; kind = "assetInstance";
        }
        else if (SelectedTilePatch != null)
        {
            mutation = "tilepatch.archive"; targetId = SelectedTilePatch.TilePatchId; layerId = SelectedTilePatch.TileLayerId; objectRevision = SelectedTilePatch.Revision; kind = "tilePatch";
        }
        else
        {
            ErrorMessage = "Выберите объект карты для удаления.";
            return;
        }

        var layerRevision = FindLayerRevision(layerId);
        if (MutateEditor(mutation, targetId, layerId, layerRevision, objectRevision).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203(mutation.Replace(".archive", ".restore"), mutation, targetId, layerId,
            new Dictionary<string, object>(), new Dictionary<string, object>(), kind));
        LoadSelectedMap();
        StatusMessage = "Объект перемещён в архив.";
    }

    public void DeleteSelectedEditorObjectConfirmed() => DeleteSelectedEditorObject();

    public bool SelectEditorObjectAtPixel(double pixelX, double pixelY)
    {
        var world = _viewport.ScreenToWorld(new MapPoint(pixelX, pixelY));
        var targets = new List<MapEditorHitTarget>();
        var visiblePatchIds = new HashSet<string>(VisibleTilePatches.Select(item => item.TilePatchId), StringComparer.OrdinalIgnoreCase);
        var visibleShapeIds = new HashSet<string>(VisibleLocationShapes.Select(item => item.ShapeId), StringComparer.OrdinalIgnoreCase);
        var visibleAssetIds = new HashSet<string>(VisibleAssetInstances.Select(item => item.AssetInstanceId), StringComparer.OrdinalIgnoreCase);
        foreach (var patch in TilePatches)
        {
            var layer = TileLayers.FirstOrDefault(item => item.TileLayerId == patch.TileLayerId);
            targets.Add(new MapEditorHitTarget { Id = patch.TilePatchId, LayerId = patch.TileLayerId, Kind = MapEditorObjectKind.TilePatch, LayerOrder = layer?.SortOrder ?? patch.SortOrder, ZIndex = patch.SortOrder, IsVisible = visiblePatchIds.Contains(patch.TilePatchId), IsLayerLocked = layer?.IsLocked == true, X = patch.X, Y = patch.Y, Width = patch.Width, Height = patch.Height });
        }
        foreach (var shape in LocationShapes)
        {
            var layer = LocationLayers.FirstOrDefault(item => item.LayerId == shape.LayerId);
            targets.Add(new MapEditorHitTarget { Id = shape.ShapeId, LayerId = shape.LayerId, Kind = MapEditorObjectKind.Shape, LayerOrder = layer?.SortOrder ?? 0, ZIndex = shape.ZIndex, IsVisible = visibleShapeIds.Contains(shape.ShapeId), IsLayerLocked = layer?.IsLocked == true, X = shape.X, Y = shape.Y, Width = shape.Width, Height = shape.Height });
        }
        foreach (var asset in AssetInstances)
        {
            var layer = LocationLayers.FirstOrDefault(item => item.LayerId == asset.LayerId);
            targets.Add(new MapEditorHitTarget { Id = asset.AssetInstanceId, LayerId = asset.LayerId, Kind = MapEditorObjectKind.AssetInstance, LayerOrder = layer?.SortOrder ?? 0, ZIndex = asset.ZIndex, IsVisible = visibleAssetIds.Contains(asset.AssetInstanceId), IsLayerLocked = layer?.IsLocked == true, X = asset.X, Y = asset.Y, Width = asset.Width, Height = asset.Height });
        }

        var hit = MapEditorHitTest.Resolve(targets, world.X, world.Y, _viewport.Zoom);
        if (hit == null)
        {
            SelectedShape = null;
            SelectedTilePatch = null;
            SelectedAssetInstance = null;
            return false;
        }
        if (hit.Kind == MapEditorObjectKind.AssetInstance) SelectedAssetInstance = AssetInstances.FirstOrDefault(item => item.AssetInstanceId == hit.Id);
        else if (hit.Kind == MapEditorObjectKind.Shape) SelectedShape = LocationShapes.FirstOrDefault(item => item.ShapeId == hit.Id);
        else SelectedTilePatch = TilePatches.FirstOrDefault(item => item.TilePatchId == hit.Id);
        return true;
    }

    public bool BeginSelectedEditorDrag(double pixelX, double pixelY)
    {
        if (SelectedShape == null && SelectedAssetInstance == null) return false;
        var layerId = SelectedShape?.LayerId ?? SelectedAssetInstance?.LayerId ?? string.Empty;
        var layer = LocationLayers.FirstOrDefault(item => item.LayerId == layerId);
        if (layer?.IsLocked == true)
        {
            ErrorMessage = "Слой заблокирован. Объект можно просмотреть, но нельзя перемещать.";
            return false;
        }
        _editorDragActive = true;
        _editorDragStartX = SelectedShape?.X ?? SelectedAssetInstance?.X ?? 0d;
        _editorDragStartY = SelectedShape?.Y ?? SelectedAssetInstance?.Y ?? 0d;
        var pointer = _viewport.ScreenToWorld(new MapPoint(pixelX, pixelY));
        _editorDragOffsetX = pointer.X - _editorDragStartX;
        _editorDragOffsetY = pointer.Y - _editorDragStartY;
        PreviewSelectedEditorDrag(pixelX, pixelY);
        return true;
    }

    public void PreviewSelectedEditorDrag(double pixelX, double pixelY)
    {
        if (!_editorDragActive) return;
        var world = _viewport.ScreenToWorld(new MapPoint(pixelX, pixelY));
        var width = SelectedShape?.Width ?? SelectedAssetInstance?.Width ?? 0d;
        var height = SelectedShape?.Height ?? SelectedAssetInstance?.Height ?? 0d;
        var point = MapEditorSnapPolicy.SnapPoint(world.X - _editorDragOffsetX, world.Y - _editorDragOffsetY, SnapToGrid, SnapStepMeters, 0d, 0d,
            Math.Max(0d, _viewport.MapWidthMeters - width), Math.Max(0d, _viewport.MapHeightMeters - height));
        if (SelectedShape != null) { SelectedShape.X = point.X; SelectedShape.Y = point.Y; }
        if (SelectedAssetInstance != null) { SelectedAssetInstance.X = point.X; SelectedAssetInstance.Y = point.Y; }
        RebuildCanvas();
    }

    public void CancelSelectedEditorDrag()
    {
        if (!_editorDragActive) return;
        if (SelectedShape != null) { SelectedShape.X = _editorDragStartX; SelectedShape.Y = _editorDragStartY; }
        if (SelectedAssetInstance != null) { SelectedAssetInstance.X = _editorDragStartX; SelectedAssetInstance.Y = _editorDragStartY; }
        _editorDragActive = false;
        RebuildCanvas();
    }

    public bool CommitSelectedEditorDrag()
    {
        if (!_editorDragActive) return false;
        _editorDragActive = false;
        var shape = SelectedShape;
        var asset = SelectedAssetInstance;
        var targetId = shape?.ShapeId ?? asset?.AssetInstanceId ?? string.Empty;
        var layerId = shape?.LayerId ?? asset?.LayerId ?? string.Empty;
        var objectRevision = shape?.Revision ?? asset?.Revision ?? 0L;
        var x = shape?.X ?? asset?.X ?? 0d;
        var y = shape?.Y ?? asset?.Y ?? 0d;
        var mutation = shape != null ? "shape.move" : "asset.move";
        var kind = shape != null ? "shape" : "assetInstance";
        var forward = new Dictionary<string, object> { ["x"] = x, ["y"] = y };
        var inverse = new Dictionary<string, object> { ["x"] = _editorDragStartX, ["y"] = _editorDragStartY };
        if (MutateEditor(mutation, targetId, layerId, FindLayerRevision(layerId), objectRevision, forward).Status != ResponseStatus.Ok)
        {
            if (shape != null) { shape.X = _editorDragStartX; shape.Y = _editorDragStartY; }
            if (asset != null) { asset.X = _editorDragStartX; asset.Y = _editorDragStartY; }
            RebuildCanvas();
            return false;
        }
        RecordEditorHistory(new MapEditorHistoryEntry0203(mutation, mutation, targetId, layerId, inverse, forward, kind));
        LoadSelectedMapAndReselect(targetId, kind);
        return true;
    }

    public bool NudgeSelectedEditorObject(int directionX, int directionY, bool largeStep)
    {
        if (SelectedMap == null || (SelectedShape == null && SelectedAssetInstance == null)) return false;
        var shape = SelectedShape;
        var asset = SelectedAssetInstance;
        var layerId = shape?.LayerId ?? asset?.LayerId ?? string.Empty;
        if (LocationLayers.FirstOrDefault(item => item.LayerId == layerId)?.IsLocked == true)
        {
            ErrorMessage = "Слой заблокирован. Перемещение недоступно.";
            return true;
        }
        var oldX = shape?.X ?? asset?.X ?? 0d;
        var oldY = shape?.Y ?? asset?.Y ?? 0d;
        var width = shape?.Width ?? asset?.Width ?? 0d;
        var height = shape?.Height ?? asset?.Height ?? 0d;
        var step = largeStep ? SnapStepMeters * 5d : SnapStepMeters;
        var point = MapEditorSnapPolicy.SnapPoint(oldX + directionX * step, oldY + directionY * step, SnapToGrid, SnapStepMeters,
            0d, 0d, Math.Max(0d, SelectedMap.WidthMeters - width), Math.Max(0d, SelectedMap.HeightMeters - height));
        var mutation = shape != null ? "shape.move" : "asset.move";
        var kind = shape != null ? "shape" : "assetInstance";
        var targetId = shape?.ShapeId ?? asset?.AssetInstanceId ?? string.Empty;
        var revision = shape?.Revision ?? asset?.Revision ?? 0L;
        var before = new Dictionary<string, object> { ["x"] = oldX, ["y"] = oldY };
        var after = new Dictionary<string, object> { ["x"] = point.X, ["y"] = point.Y };
        if (MutateEditor(mutation, targetId, layerId, FindLayerRevision(layerId), revision, after).Status != ResponseStatus.Ok) return true;
        RecordEditorHistory(new MapEditorHistoryEntry0203(mutation, mutation, targetId, layerId, before, after, kind));
        LoadSelectedMapAndReselect(targetId, kind);
        return true;
    }

    public void UpdateLayer()
    {
        if (!CanWorkWithLocationEditor || SelectedLayer == null)
        {
            ErrorMessage = "Выберите слой локации для сохранения.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = MutateEditor("layer.update", SelectedLayer.LayerId, string.Empty, SelectedLayer.Revision, null, new Dictionary<string, object>
            {
                { "displayName", FirstNonEmpty(LayerName, "Слой локации") },
                { "layerKind", FirstNonEmpty(LayerKind, "Objects") },
                { "sortOrder", LayerSortOrder },
                { "isVisible", LayerVisibleByDefault },
                { "isLocked", SelectedLayer.IsLocked },
                { "visibility", FirstNonEmpty(LayerVisibility, "PlayerVisible") }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            LoadSelectedMapAndReselect(SelectedLayer.LayerId, "layer");
            StatusMessage = "Слой локации сохранён.";
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

    public void ArchiveLayer()
    {
        if (!CanWorkWithLocationEditor || SelectedLayer == null)
        {
            ErrorMessage = "Выберите слой локации для архивации.";
            return;
        }

        var layerId = SelectedLayer.LayerId;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = MutateEditor("layer.archive", layerId, string.Empty, SelectedLayer.Revision, null,
                new Dictionary<string, object> { ["displayName"] = SelectedLayer.DisplayName, ["layerKind"] = SelectedLayer.LayerKind, ["sortOrder"] = SelectedLayer.SortOrder });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                return;
            }

            var found = LocationLayers.FirstOrDefault(layer => layer.LayerId == layerId);
            if (found != null) LocationLayers.Remove(found);
            SelectedLayer = LocationLayers.FirstOrDefault();
            RebuildCanvas();
            StatusMessage = "Слой локации архивирован.";
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

    public void AddShape()
    {
        if (!CanWorkWithLocationEditor || SelectedMap == null)
        {
            ErrorMessage = "Выберите карту сцены для добавления объекта.";
            return;
        }

        var layer = SelectedLayer;
        if (layer == null) { ErrorMessage = "Выберите слой для нового объекта."; return; }
        var targetId = Guid.NewGuid().ToString("N");
        var values = ShapeDraftValues();
        if (MutateEditor("shape.create", targetId, layer.LayerId, layer.Revision, 0L, values).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203("shape.archive", "shape.restore", targetId, layer.LayerId,
            new Dictionary<string, object>(), values, "shape"));
        LoadSelectedMapAndReselect(targetId, "shape");
        StatusMessage = "Объект добавлен и сохранён.";
    }

    public void UpdateShape()
    {
        if (!CanWorkWithLocationEditor || SelectedShape == null)
        {
            ErrorMessage = "Выберите объект локации для сохранения.";
            return;
        }

        var shape = SelectedShape;
        var before = ShapeValues(shape);
        var after = ShapeDraftValues();
        if (MutateEditor("shape.update", shape.ShapeId, shape.LayerId, FindLayerRevision(shape.LayerId), shape.Revision, after).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203("shape.update", "shape.update", shape.ShapeId, shape.LayerId, before, after, "shape"));
        LoadSelectedMapAndReselect(shape.ShapeId, "shape");
        StatusMessage = "Свойства объекта сохранены.";
    }

    public void MoveShape()
    {
        if (!CanWorkWithLocationEditor || SelectedShape == null)
        {
            ErrorMessage = "Выберите объект локации для перемещения.";
            return;
        }

        MoveSelectedObjectFromFields(SelectedShape.ShapeId, SelectedShape.LayerId, SelectedShape.Revision,
            SelectedShape.X, SelectedShape.Y, SelectedShape.Width, SelectedShape.Height, "shape", "shape.move");
    }

    public void ResizeShape()
    {
        if (!CanWorkWithLocationEditor || SelectedShape == null)
        {
            ErrorMessage = "Выберите объект локации для изменения размера.";
            return;
        }

        var shape = SelectedShape;
        var before = new Dictionary<string, object> { ["width"] = shape.Width, ["height"] = shape.Height, ["radius"] = shape.Radius };
        var after = new Dictionary<string, object> { ["width"] = ShapeWidth, ["height"] = ShapeHeight, ["radius"] = ShapeRadius };
        if (MutateEditor("shape.update", shape.ShapeId, shape.LayerId, FindLayerRevision(shape.LayerId), shape.Revision, after).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203("shape.update", "shape.update", shape.ShapeId, shape.LayerId, before, after, "shape"));
        LoadSelectedMapAndReselect(shape.ShapeId, "shape");
    }

    public void DuplicateShape()
    {
        if (!CanWorkWithLocationEditor || SelectedShape == null)
        {
            ErrorMessage = "Выберите объект локации для дублирования.";
            return;
        }

        var source = SelectedShape;
        var targetId = Guid.NewGuid().ToString("N");
        var values = ShapeValues(source);
        values["displayName"] = $"{source.DisplayName} — копия";
        values["x"] = Math.Min(source.X + SnapStepMeters, Math.Max(0d, (SelectedMap?.WidthMeters ?? 0d) - source.Width));
        values["y"] = Math.Min(source.Y + SnapStepMeters, Math.Max(0d, (SelectedMap?.HeightMeters ?? 0d) - source.Height));
        if (MutateEditor("shape.create", targetId, source.LayerId, FindLayerRevision(source.LayerId), 0L, values).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203("shape.archive", "shape.restore", targetId, source.LayerId,
            new Dictionary<string, object>(), values, "shape"));
        LoadSelectedMapAndReselect(targetId, "shape");
    }

    public void ArchiveShape()
    {
        if (!CanWorkWithLocationEditor || SelectedShape == null)
        {
            ErrorMessage = "Выберите объект локации для архивации.";
            return;
        }

        ArchiveEditorObject("shape", SelectedShape.ShapeId, SelectedShape.LayerId, SelectedShape.Revision, "shape");
    }

    public void PaintTileFromFields()
    {
        PaintTileAtMeters(ShapeX, ShapeY);
    }

    public void StampAssetFromFields()
    {
        StampAssetAtMeters(ShapeX, ShapeY);
    }

    public void EraseSelectedTilePatch()
    {
        if (!CanWorkWithLocationEditor || SelectedTilePatch == null)
        {
            ErrorMessage = "Выберите патч материала для удаления.";
            return;
        }

        ArchiveTilePatch(SelectedTilePatch.TilePatchId);
    }

    public void UpdateAssetInstance()
    {
        if (!CanWorkWithLocationEditor || SelectedAssetInstance == null)
        {
            ErrorMessage = "Выберите asset stamp для сохранения.";
            return;
        }

        var asset = SelectedAssetInstance;
        var before = AssetValues(asset);
        var after = AssetDraftValues();
        if (MutateEditor("asset.update", asset.AssetInstanceId, asset.LayerId, FindLayerRevision(asset.LayerId), asset.Revision, after).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203("asset.update", "asset.update", asset.AssetInstanceId, asset.LayerId, before, after, "assetInstance"));
        LoadSelectedMapAndReselect(asset.AssetInstanceId, "assetInstance");
    }

    public void ArchiveAssetInstance()
    {
        if (!CanWorkWithLocationEditor || SelectedAssetInstance == null)
        {
            ErrorMessage = "Выберите asset stamp для удаления.";
            return;
        }

        ArchiveEditorObject("asset", SelectedAssetInstance.AssetInstanceId, SelectedAssetInstance.LayerId, SelectedAssetInstance.Revision, "assetInstance");
    }

    public bool HandleLocationMapCanvasClick(double pixelX, double pixelY)
    {
        if (!CanWorkWithLocationEditor || SelectedMap == null)
            return false;

        var world = _viewport.ClampWorldPoint(_viewport.ScreenToWorld(new MapPoint(pixelX, pixelY)));
        var metersX = world.X;
        var metersY = world.Y;
        var tool = FirstNonEmpty(LocationTool, "SelectMove");

        if (string.Equals(tool, "PaintTile", StringComparison.OrdinalIgnoreCase) || string.Equals(tool, "RoadTool", StringComparison.OrdinalIgnoreCase) || string.Equals(tool, "RoomTool", StringComparison.OrdinalIgnoreCase))
        {
            PaintTileAtMeters(metersX, metersY);
            return true;
        }

        if (string.Equals(tool, "EraseTile", StringComparison.OrdinalIgnoreCase))
        {
            var patch = FindTilePatchAtMeters(metersX, metersY);
            if (patch != null)
            {
                SelectedTilePatch = patch;
                ArchiveTilePatch(patch.TilePatchId);
            }
            return true;
        }

        if (string.Equals(tool, "StampAsset", StringComparison.OrdinalIgnoreCase) || string.Equals(tool, "WallTool", StringComparison.OrdinalIgnoreCase) || string.Equals(tool, "DoorTool", StringComparison.OrdinalIgnoreCase) || string.Equals(tool, "ZoneTool", StringComparison.OrdinalIgnoreCase))
        {
            StampAssetAtMeters(metersX, metersY);
            return true;
        }

        return SelectEditorObjectAtPixel(pixelX, pixelY);
    }

    private void PaintTileAtMeters(double metersX, double metersY)
    {
        if (!CanWorkWithLocationEditor || SelectedMap == null) return;
        var tileSize = Math.Max(1d, TileSizeMeters);
        var brush = Math.Max(tileSize, BrushSizeMeters);
        var snapped = MapEditorSnapPolicy.SnapPoint(metersX, metersY, SnapToGrid, SnapStepMeters, 0d, 0d,
            Math.Max(0d, SelectedMap.WidthMeters - tileSize), Math.Max(0d, SelectedMap.HeightMeters - tileSize));
        var x = snapped.X;
        var y = snapped.Y;
        var width = Math.Min(brush, Math.Max(1d, SelectedMap.WidthMeters - x));
        var height = Math.Min(brush, Math.Max(1d, SelectedMap.HeightMeters - y));
        var layer = SelectedTileLayer;
        if (layer == null) { ErrorMessage = "Выберите слой материалов."; return; }
        var targetId = Guid.NewGuid().ToString("N");
        var values = new Dictionary<string, object>
        {
            ["materialKey"] = FirstNonEmpty(ShapeMaterialKey, "grass"),
            ["textureKey"] = FirstNonEmpty(ShapeTextureKey, LocationMapVisualBrushes.DefaultTextureForMaterial(ShapeMaterialKey)),
            ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height,
            ["rotationDegrees"] = ShapeRotationDegrees,
            ["opacity"] = Math.Max(0.2d, Math.Min(1d, ShapeOpacity <= 0 ? 1d : ShapeOpacity)),
            ["sortOrder"] = ShapeSortOrder,
            ["visibility"] = FirstNonEmpty(ShapeVisibility, "PlayerVisible")
        };
        if (MutateEditor("tilepatch.create", targetId, layer.TileLayerId, layer.Revision, 0L, values).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203("tilepatch.archive", "tilepatch.restore", targetId, layer.TileLayerId,
            new Dictionary<string, object>(), values, "tilePatch"));
        LoadSelectedMapAndReselect(targetId, "tilePatch");
        StatusMessage = "Материал нанесён и сохранён.";
    }

    private void StampAssetAtMeters(double metersX, double metersY)
    {
        if (!CanWorkWithLocationEditor || SelectedMap == null) return;
        var asset = SelectedAsset;
        var width = Math.Max(1d, ShapeWidth > 0 ? ShapeWidth : asset?.DefaultWidth ?? 5d);
        var height = Math.Max(1d, ShapeHeight > 0 ? ShapeHeight : asset?.DefaultHeight ?? 5d);
        var snapped = MapEditorSnapPolicy.SnapPoint(metersX, metersY, SnapToGrid, SnapStepMeters, 0d, 0d,
            Math.Max(0d, SelectedMap.WidthMeters - width), Math.Max(0d, SelectedMap.HeightMeters - height));
        ShapeX = snapped.X;
        ShapeY = snapped.Y;
        ShapeWidth = width;
        ShapeHeight = height;
        var layer = SelectedLayer;
        if (layer == null) { ErrorMessage = "Выберите слой объектов."; return; }
        var targetId = Guid.NewGuid().ToString("N");
        var values = AssetDraftValues();
        if (MutateEditor("asset.create", targetId, layer.LayerId, layer.Revision, 0L, values).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203("asset.archive", "asset.restore", targetId, layer.LayerId,
            new Dictionary<string, object>(), values, "assetInstance"));
        LoadSelectedMapAndReselect(targetId, "assetInstance");
        StatusMessage = "Объект размещён и сохранён.";
    }

    private void ArchiveTilePatch(string tilePatchId)
    {
        var patch = TilePatches.FirstOrDefault(item => item.TilePatchId == tilePatchId);
        if (patch != null) ArchiveEditorObject("tilepatch", patch.TilePatchId, patch.TileLayerId, patch.Revision, "tilePatch");
    }

    private void ArchiveEditorObject(string mutationPrefix, string targetId, string layerId, long revision, string kind)
    {
        if (MutateEditor($"{mutationPrefix}.archive", targetId, layerId, FindLayerRevision(layerId), revision).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203($"{mutationPrefix}.restore", $"{mutationPrefix}.archive", targetId, layerId,
            new Dictionary<string, object>(), new Dictionary<string, object>(), kind));
        LoadSelectedMap();
        StatusMessage = "Объект перемещён в архив.";
    }

    private void MoveSelectedObjectFromFields(string targetId, string layerId, long revision, double oldX, double oldY,
        double width, double height, string kind, string mutation)
    {
        if (SelectedMap == null) return;
        var snapped = MapEditorSnapPolicy.SnapPoint(ShapeX, ShapeY, SnapToGrid, SnapStepMeters, 0d, 0d,
            Math.Max(0d, SelectedMap.WidthMeters - width), Math.Max(0d, SelectedMap.HeightMeters - height));
        var before = new Dictionary<string, object> { ["x"] = oldX, ["y"] = oldY };
        var after = new Dictionary<string, object> { ["x"] = snapped.X, ["y"] = snapped.Y };
        if (MutateEditor(mutation, targetId, layerId, FindLayerRevision(layerId), revision, after).Status != ResponseStatus.Ok) return;
        RecordEditorHistory(new MapEditorHistoryEntry0203(mutation, mutation, targetId, layerId, before, after, kind));
        LoadSelectedMapAndReselect(targetId, kind);
        StatusMessage = $"Объект перемещён. {SnapSummary}.";
    }

    private Dictionary<string, object> ShapeDraftValues()
    {
        var values = BuildShapePayload(includeShapeId: false);
        values.Remove("sceneMapId");
        values.Remove("layerId");
        return values;
    }

    private static Dictionary<string, object> ShapeValues(SceneMapShapeUiItem shape) => new()
    {
        ["displayName"] = shape.DisplayName, ["shapeKind"] = shape.ShapeKind, ["objectKind"] = shape.ObjectKind,
        ["x"] = shape.X, ["y"] = shape.Y, ["width"] = shape.Width, ["height"] = shape.Height, ["radius"] = shape.Radius,
        ["rotationDegrees"] = shape.RotationDegrees, ["points"] = shape.Points, ["text"] = shape.Text,
        ["materialKey"] = shape.MaterialKey, ["textureKey"] = shape.TextureKey, ["assetKey"] = shape.AssetKey,
        ["renderMode"] = shape.RenderMode, ["visualStyleKey"] = shape.VisualStyleKey, ["opacity"] = shape.Opacity,
        ["visualOpacity"] = shape.VisualOpacity, ["strokeThickness"] = shape.StrokeThickness, ["zIndex"] = shape.ZIndex,
        ["sortOrder"] = shape.SortOrder, ["visibility"] = shape.Visibility, ["descriptionPlayer"] = shape.DescriptionPlayer,
        ["descriptionGm"] = shape.DescriptionGm, ["blocksMovement"] = shape.BlocksMovement, ["blocksVision"] = shape.BlocksVision,
        ["providesCover"] = shape.ProvidesCover, ["isInteractable"] = shape.IsInteractable
    };

    private Dictionary<string, object> AssetDraftValues()
    {
        var values = BuildAssetInstancePayload(includeAssetId: false);
        values.Remove("sceneMapId");
        return values;
    }

    private static Dictionary<string, object> AssetValues(SceneMapAssetInstanceUiItem asset) => new()
    {
        ["displayName"] = asset.DisplayName, ["assetKey"] = asset.AssetKey, ["assetKind"] = asset.AssetKind,
        ["objectKind"] = asset.ObjectKind, ["x"] = asset.X, ["y"] = asset.Y, ["width"] = asset.Width,
        ["height"] = asset.Height, ["rotationDegrees"] = asset.RotationDegrees, ["zIndex"] = asset.ZIndex,
        ["visibility"] = asset.Visibility, ["descriptionPlayer"] = asset.DescriptionPlayer, ["descriptionGm"] = asset.DescriptionGm,
        ["blocksMovement"] = asset.BlocksMovement, ["blocksVision"] = asset.BlocksVision,
        ["providesCover"] = asset.ProvidesCover, ["isInteractable"] = asset.IsInteractable
    };

    private SceneMapTilePatchUiItem? FindTilePatchAtMeters(double x, double y)
        => VisibleTilePatches.OrderByDescending(p => p.SortOrder).FirstOrDefault(p => x >= p.X && y >= p.Y && x <= p.X + p.Width && y <= p.Y + p.Height);

    private SceneMapAssetInstanceUiItem? FindAssetAtMeters(double x, double y)
        => VisibleAssetInstances.OrderByDescending(a => a.ZIndex).FirstOrDefault(a => x >= a.X && y >= a.Y && x <= a.X + a.Width && y <= a.Y + a.Height);

    private Dictionary<string, object> BuildAssetInstancePayload(bool includeAssetId)
    {
        var asset = SelectedAsset;
        var payload = new Dictionary<string, object>
        {
            { "sceneMapId", SelectedMap?.MapId ?? string.Empty },
            { "assetKey", FirstNonEmpty(ShapeAssetKey, asset?.AssetKey ?? "crate") },
            { "displayName", FirstNonEmpty(ShapeName, asset?.DisplayName ?? "Объект карты") },
            { "assetKind", FirstNonEmpty(asset?.AssetKind ?? string.Empty, "Prop") },
            { "objectKind", FirstNonEmpty(ObjectKind, asset?.DefaultObjectKind ?? "Decoration") },
            { "x", ShapeX },
            { "y", ShapeY },
            { "width", ShapeWidth },
            { "height", ShapeHeight },
            { "rotationDegrees", ShapeRotationDegrees },
            { "zIndex", ShapeZIndex <= 0 ? 100 : ShapeZIndex },
            { "visibility", FirstNonEmpty(ShapeVisibility, "PlayerVisible") },
            { "descriptionPlayer", ShapeDescriptionPlayer ?? string.Empty },
            { "descriptionGm", ShapeDescriptionGm ?? string.Empty },
            { "blocksMovement", ShapeBlocksMovement },
            { "blocksVision", ShapeBlocksVision },
            { "providesCover", ShapeProvidesCover },
            { "isInteractable", ShapeIsInteractable },
            { "linkedEntityType", FirstNonEmpty(ShapeLinkedEntityType, "None") },
            { "linkedEntityId", ShapeLinkedEntityId ?? string.Empty }
        };

        if (includeAssetId && SelectedAssetInstance != null)
            payload["assetInstanceId"] = SelectedAssetInstance.AssetInstanceId;

        return payload;
    }

    private Dictionary<string, object> BuildShapePayload(bool includeShapeId)
    {
        var payload = new Dictionary<string, object>
        {
            { "sceneMapId", SelectedMap?.MapId ?? string.Empty },
            { "layerId", FirstNonEmpty(ShapeLayerId, SelectedLayer?.LayerId ?? string.Empty) },
            { "displayName", FirstNonEmpty(ShapeName, "Объект локации") },
            { "descriptionPlayer", ShapeDescriptionPlayer ?? string.Empty },
            { "descriptionGm", ShapeDescriptionGm ?? string.Empty },
            { "shapeKind", FirstNonEmpty(ShapeKind, LocationTool, "Rectangle") },
            { "objectKind", FirstNonEmpty(ObjectKind, "Decoration") },
            { "x", ShapeX },
            { "y", ShapeY },
            { "width", ShapeWidth },
            { "height", ShapeHeight },
            { "radius", ShapeRadius },
            { "rotationDegrees", ShapeRotationDegrees },
            { "points", ShapePoints ?? string.Empty },
            { "text", ShapeText ?? string.Empty },
            { "fillKey", ShapeFillKey ?? string.Empty },
            { "strokeKey", ShapeStrokeKey ?? string.Empty },
            { "opacity", ShapeOpacity },
            { "materialKey", ShapeMaterialKey ?? string.Empty },
            { "textureKey", ShapeTextureKey ?? string.Empty },
            { "patternKey", ShapePatternKey ?? string.Empty },
            { "assetKey", ShapeAssetKey ?? string.Empty },
            { "visualStyleKey", ShapeVisualStyleKey ?? string.Empty },
            { "renderMode", ShapeRenderMode ?? string.Empty },
            { "gridSnapEnabled", ShapeGridSnapEnabled },
            { "visualOpacity", ShapeVisualOpacity },
            { "strokeThickness", ShapeStrokeThickness },
            { "zIndex", ShapeZIndex },
            { "sortOrder", ShapeSortOrder },
            { "visibility", FirstNonEmpty(ShapeVisibility, "PlayerVisible") },
            { "blocksMovement", ShapeBlocksMovement },
            { "blocksVision", ShapeBlocksVision },
            { "providesCover", ShapeProvidesCover },
            { "isInteractable", ShapeIsInteractable },
            { "linkedEntityType", FirstNonEmpty(ShapeLinkedEntityType, "None") },
            { "linkedEntityId", ShapeLinkedEntityId ?? string.Empty }
        };

        if (includeShapeId && SelectedShape != null)
            payload["shapeId"] = SelectedShape.ShapeId;

        return payload;
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
            CanvasWidth = _viewport.ViewportWidthPixels;
            CanvasHeight = _viewport.ViewportHeightPixels;
            CanvasScaleLabel = "1м = 0.0px";
            return;
        }

        if (Math.Abs(_viewport.MapWidthMeters - SelectedMap.WidthMeters) > 0.01 || Math.Abs(_viewport.MapHeightMeters - SelectedMap.HeightMeters) > 0.01 || Math.Abs(_viewport.GridSizeMeters - SelectedMap.GridCellSizeMeters) > 0.01)
            _viewport.SetMap(SelectedMap.WidthMeters, SelectedMap.HeightMeters, SelectedMap.GridCellSizeMeters, fit: true);
        var scale = _viewport.Zoom;
        CanvasWidth = _viewport.ViewportWidthPixels;
        CanvasHeight = _viewport.ViewportHeightPixels;
        CanvasScaleLabel = $"1м = {scale:0.###}px";
        Notify(nameof(ZoomIndicator));
        Notify(nameof(CanZoomIn));
        Notify(nameof(CanZoomOut));
        var lod = MapGridLodCalculator.Calculate(SelectedMap.GridCellSizeMeters, scale);
        GridStepLabel = $"Сетка: {lod.MinorStepMeters:0.##} м · основная {lod.MajorStepMeters:0.##} м";
        var visible = _viewport.VisibleWorldBounds();

        if (ShowGrid)
        {
            var cell = lod.MinorStepMeters;
            var startX = Math.Floor(visible.X / cell) * cell;
            var endX = Math.Min(SelectedMap.WidthMeters, visible.Right + cell);
            for (var x = startX; x <= endX; x += cell)
            {
                var px = _viewport.WorldToScreen(new MapPoint(x, 0d)).X;
                var major = Math.Abs(x / lod.MajorStepMeters - Math.Round(x / lod.MajorStepMeters)) < 0.001;
                GridLines.Add(new MapGridLineUiItem { X1 = px, Y1 = 0, X2 = px, Y2 = CanvasHeight, Opacity = major ? 0.7 : 0.32, Thickness = major ? 1.4 : 0.8 });
            }

            var startY = Math.Floor(visible.Y / cell) * cell;
            var endY = Math.Min(SelectedMap.HeightMeters, visible.Bottom + cell);
            for (var y = startY; y <= endY; y += cell)
            {
                var py = _viewport.WorldToScreen(new MapPoint(0d, y)).Y;
                var major = Math.Abs(y / lod.MajorStepMeters - Math.Round(y / lod.MajorStepMeters)) < 0.001;
                GridLines.Add(new MapGridLineUiItem { X1 = 0, Y1 = py, X2 = CanvasWidth, Y2 = py, Opacity = major ? 0.7 : 0.32, Thickness = major ? 1.4 : 0.8 });
            }
        }

        foreach (var marker in Markers)
        {
            var point = _viewport.WorldToScreen(new MapPoint(marker.X, marker.Y));
            marker.PixelX = point.X;
            marker.PixelY = point.Y;
        }

        RebuildVisibleTilePatches(scale);
        RebuildVisibleAssetInstances(scale);
        RebuildVisibleLocationShapes(scale);
        RebuildVisibleTokens(scale);

        BuildFogOverlay(scale, _viewport.OffsetX, _viewport.OffsetY);

        if (ShowCoordinates)
        {
            CanvasCoordinateHints.Add("Начало координат: X=0, Y=0 (левый верхний угол)");
            CanvasCoordinateHints.Add($"Границы: X 0..{SelectedMap.WidthMeters}, Y 0..{SelectedMap.HeightMeters}");
        }

        ClientLogService.Instance.Debug("admin.map.canvas.render");
    }

    private void ZoomIn()
    {
        _viewport.ZoomByFactor(1.25d, new MapPoint(CanvasWidth / 2d, CanvasHeight / 2d));
        RebuildCanvas();
    }

    private void ZoomOut()
    {
        _viewport.ZoomByFactor(1d / 1.25d, new MapPoint(CanvasWidth / 2d, CanvasHeight / 2d));
        RebuildCanvas();
    }

    private void ResetView()
    {
        _viewport.FitMap();
        RebuildCanvas();
    }

    private void FitToMap()
    {
        _viewport.FitMap();
        RebuildCanvas();
    }

    private void SetOneHundredPercent()
    {
        _viewport.Reset();
        RebuildCanvas();
    }

    public void ResizeViewport(double width, double height)
    {
        if (width < 40 || height < 40) return;
        _viewport.ResizeViewport(width, height);
        RebuildCanvas();
    }

    public void ZoomAtPixel(double pixelX, double pixelY, int wheelDelta)
    {
        _viewport.ZoomByFactor(wheelDelta > 0 ? 1.15d : 1d / 1.15d, new MapPoint(pixelX, pixelY));
        RebuildCanvas();
    }

    public void PanViewport(double deltaX, double deltaY)
    {
        _viewport.PanByPixels(deltaX, deltaY);
        RebuildCanvas();
    }

    public void UpdateCursor(double pixelX, double pixelY)
    {
        _viewport.UpdateCursor(new MapPoint(pixelX, pixelY));
        var point = _viewport.CursorWorldPosition ?? new MapPoint(0d, 0d);
        CoordinateIndicator = $"X={point.X:0.##} м, Y={point.Y:0.##} м";
        UpdatePlacementGhost(point);
    }

    private void UpdatePlacementGhost(MapPoint world)
    {
        var placement = LocationTool == "StampAsset" || LocationTool == "PaintTile" || LocationTool.EndsWith("Tool", StringComparison.Ordinal);
        if (!placement || SelectedMap == null) { PlacementGhostVisibility = Visibility.Collapsed; return; }
        var isTile = LocationTool == "PaintTile";
        var width = isTile ? Math.Max(TileSizeMeters, BrushSizeMeters) : Math.Max(1d, ShapeWidth > 0 ? ShapeWidth : SelectedAsset?.DefaultWidth ?? 5d);
        var height = isTile ? Math.Max(TileSizeMeters, BrushSizeMeters) : Math.Max(1d, ShapeHeight > 0 ? ShapeHeight : SelectedAsset?.DefaultHeight ?? 5d);
        var snapped = MapEditorSnapPolicy.SnapPoint(world.X, world.Y, SnapToGrid, SnapStepMeters, 0d, 0d,
            Math.Max(0d, SelectedMap.WidthMeters - width), Math.Max(0d, SelectedMap.HeightMeters - height));
        var topLeft = _viewport.WorldToScreen(new MapPoint(snapped.X, snapped.Y));
        var bottomRight = _viewport.WorldToScreen(new MapPoint(snapped.X + width, snapped.Y + height));
        PlacementGhostX = topLeft.X;
        PlacementGhostY = topLeft.Y;
        PlacementGhostWidth = Math.Max(8d, bottomRight.X - topLeft.X);
        PlacementGhostHeight = Math.Max(8d, bottomRight.Y - topLeft.Y);
        PlacementGhostLabel = isTile ? LocationMapVisualBrushes.MaterialDisplayName(ShapeMaterialKey) : SelectedAsset?.DisplayName ?? ShapeName;
        PlacementGhostVisibility = Visibility.Visible;
    }

    public void CancelEditorInteraction()
    {
        CancelSelectedEditorDrag();
        LocationTool = "Select";
        PlacementGhostVisibility = Visibility.Collapsed;
        StatusMessage = "Размещение отменено.";
    }

    public void SelectMarkerFromUi(SceneMarkerUiItem? marker)
    {
        if (marker == null) return;
        SelectedMarker = marker;
    }

    public void SelectTokenFromUi(SceneTokenUiItem? token)
    {
        if (token == null) return;
        SelectedToken = token;
    }

    public void SelectShapeFromUi(SceneMapShapeUiItem? shape)
    {
        if (shape == null) return;
        SelectedShape = shape;
    }

    private void RebuildVisibleLocationShapes(double? scaleOverride = null)
    {
        VisibleLocationShapes.Clear();
        var scale = scaleOverride ?? (SelectedMap == null ? 0d : _viewport.Zoom);

        foreach (var shape in LocationShapes.OrderBy(shape => shape.SortOrder).ThenBy(shape => shape.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            shape.IsMapVisualMode = IsMapVisualMode;
            var layer = LocationLayers.FirstOrDefault(item => string.Equals(item.LayerId, shape.LayerId, StringComparison.OrdinalIgnoreCase));
            var visibility = FirstNonEmpty(shape.Visibility, layer?.Visibility ?? "PlayerVisible");
            if (!ShowGmOnlyLayer && string.Equals(visibility, "GmOnly", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!ShowHiddenLayer && string.Equals(visibility, "Hidden", StringComparison.OrdinalIgnoreCase))
                continue;

            if (scale > 0)
                shape.ApplyScale(scale, _viewport.OffsetX, _viewport.OffsetY);

            VisibleLocationShapes.Add(shape);
        }
    }

    private void RebuildVisibleTilePatches(double? scaleOverride = null)
    {
        VisibleTilePatches.Clear();
        var scale = scaleOverride ?? (SelectedMap == null ? 0d : _viewport.Zoom);

        foreach (var patch in TilePatches.OrderBy(patch => patch.SortOrder).ThenBy(patch => patch.MaterialKey, StringComparer.OrdinalIgnoreCase))
        {
            var layer = TileLayers.FirstOrDefault(item => string.Equals(item.TileLayerId, patch.TileLayerId, StringComparison.OrdinalIgnoreCase));
            var visibility = FirstNonEmpty(patch.Visibility, layer?.Visibility ?? "PlayerVisible");
            if (!ShowGmOnlyLayer && string.Equals(visibility, "GmOnly", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!ShowHiddenLayer && string.Equals(visibility, "Hidden", StringComparison.OrdinalIgnoreCase))
                continue;
            patch.ApplyScale(scale, _viewport.OffsetX, _viewport.OffsetY);
            VisibleTilePatches.Add(patch);
        }
    }

    private void RebuildVisibleAssetInstances(double? scaleOverride = null)
    {
        VisibleAssetInstances.Clear();
        var scale = scaleOverride ?? (SelectedMap == null ? 0d : _viewport.Zoom);

        foreach (var asset in AssetInstances.OrderBy(asset => asset.ZIndex).ThenBy(asset => asset.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            if (!ShowGmOnlyLayer && string.Equals(asset.Visibility, "GmOnly", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!ShowHiddenLayer && string.Equals(asset.Visibility, "Hidden", StringComparison.OrdinalIgnoreCase))
                continue;
            asset.ApplyScale(scale, _viewport.OffsetX, _viewport.OffsetY);
            VisibleAssetInstances.Add(asset);
        }
    }

    private void RebuildVisibleTokens(double? scaleOverride = null)
    {
        VisibleTokens.Clear();
        if (!ShowTokenLayer)
            return;

        var scale = scaleOverride ?? (SelectedMap == null ? 0d : _viewport.Zoom);

        foreach (var token in Tokens)
        {
            if (!ShowGmOnlyLayer && string.Equals(token.Visibility, "GmOnly", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!ShowHiddenLayer && string.Equals(token.Visibility, "Hidden", StringComparison.OrdinalIgnoreCase))
                continue;

            if (scale > 0)
            {
                token.PixelX = MapCanvasProjectionHelper.ToPixel(token.X, scale) + _viewport.OffsetX;
                token.PixelY = MapCanvasProjectionHelper.ToPixel(token.Y, scale) + _viewport.OffsetY;
                token.NotifyPixel();
            }

            VisibleTokens.Add(token);
        }
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

    private void BuildFogOverlay(double scale, double offsetX, double offsetY)
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
                X = MapCanvasProjectionHelper.ToPixel(fromX, scale) + offsetX,
                Y = MapCanvasProjectionHelper.ToPixel(fromY, scale) + offsetY,
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
            var flagName = FirstNonEmpty(Str(Get(flag, "name")), Str(Get(flag, "key")), Str(Get(flag, "flagName")));
            if (string.Equals(flagName, name, StringComparison.OrdinalIgnoreCase) ||
                flagName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
            {
                return Bool(Get(flag, "effectiveValue")) ||
                       Bool(Get(flag, "effective")) ||
                       Bool(Get(flag, "enabled")) ||
                       Bool(Get(flag, "value"));
            }
        }

        return false;
    }

    private static IEnumerable<Dictionary<string, object>> ExtractFeatureFlagMaps(Dictionary<string, object>? payload)
    {
        var directItems = Dictionaries(Get(payload, "items")).ToList();
        if (directItems.Count > 0) return directItems;

        var directFlags = Dictionaries(Get(payload, "flags")).ToList();
        if (directFlags.Count > 0) return directFlags;

        var snapshot = AsMap(Get(payload, "snapshot"));
        return Dictionaries(Get(snapshot, "flags")).ToList();
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
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
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

        if (value is IEnumerable pairs && value is not string)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in pairs)
            {
                var pairMap = AsMap(pair);
                var keyEntry = pairMap.FirstOrDefault(entry => string.Equals(entry.Key, "key", StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(Convert.ToString(keyEntry.Value))) continue;
                var valueEntry = pairMap.FirstOrDefault(entry => string.Equals(entry.Key, "value", StringComparison.OrdinalIgnoreCase));
                result[Convert.ToString(keyEntry.Value)!] = valueEntry.Value!;
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

public sealed class AdminPlayerMapPreviewObjectUiItem0204
{
    public string DisplayName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public double PixelX { get; set; }
    public double PixelY { get; set; }
    public Brush Fill => Kind.ToLowerInvariant() switch
    {
        "token" => new SolidColorBrush(Color.FromRgb(37, 99, 235)),
        "marker" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
        "asset" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
    };
}

public sealed class MapVisibilityOptionUiItem0204
{
    public MapVisibilityOptionUiItem0204(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }
    public string DisplayName { get; }

    public static ObservableCollection<MapVisibilityOptionUiItem0204> Create() => new()
    {
        new MapVisibilityOptionUiItem0204("PlayerVisible", "Видно игрокам"),
        new MapVisibilityOptionUiItem0204("GmOnly", "Только GM"),
        new MapVisibilityOptionUiItem0204("Hidden", "Скрыто")
    };

    public override string ToString() => DisplayName;
}

public sealed class MapEditorHistoryEntry0203
{
    public MapEditorHistoryEntry0203(
        string inverseMutation,
        string redoMutation,
        string targetId,
        string layerId,
        IDictionary<string, object> inverseValues,
        IDictionary<string, object> redoValues,
        string kind = "layer")
    {
        InverseMutation = inverseMutation;
        RedoMutation = redoMutation;
        TargetId = targetId;
        LayerId = layerId;
        InverseValues = new Dictionary<string, object>(inverseValues);
        RedoValues = new Dictionary<string, object>(redoValues);
        Kind = kind;
    }

    public string InverseMutation { get; }
    public string RedoMutation { get; }
    public string TargetId { get; }
    public string LayerId { get; }
    public Dictionary<string, object> InverseValues { get; }
    public Dictionary<string, object> RedoValues { get; }
    public string Kind { get; }
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

    public override string ToString() => Label;

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static DateTime Date(object? value) => value is DateTime dt ? dt : DateTime.MinValue;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class SceneMapLayerUiItem : ViewModelBase
{
    private string _displayName = string.Empty;
    private string _layerKind = "Objects";
    private int _sortOrder;
    private bool _isVisibleByDefault = true;
    private string _visibility = "PlayerVisible";

    public string LayerId { get; set; } = string.Empty;
    public string SceneMapId { get; set; } = string.Empty;
    public long Revision { get; set; } = 1;
    public bool IsLocked { get; set; }
    public double Opacity { get; set; } = 1d;

    public string DisplayName { get => _displayName; set { if (_displayName != value) { _displayName = value; Notify(); Notify(nameof(Label)); } } }
    public string LayerKind { get => _layerKind; set { if (_layerKind != value) { _layerKind = value; Notify(); Notify(nameof(LayerKindDisplay)); Notify(nameof(Label)); } } }
    public int SortOrder { get => _sortOrder; set { if (_sortOrder != value) { _sortOrder = value; Notify(); Notify(nameof(Label)); } } }
    public bool IsVisibleByDefault { get => _isVisibleByDefault; set { if (_isVisibleByDefault != value) { _isVisibleByDefault = value; Notify(); } } }
    public string Visibility { get => _visibility; set { if (_visibility != value) { _visibility = value; Notify(); Notify(nameof(VisibilityDisplay)); Notify(nameof(Label)); } } }

    public string LayerKindDisplay => LayerKind switch
    {
        "Terrain" => "Местность",
        "Buildings" => "Здания",
        "Roads" => "Дороги",
        "Walls" => "Стены",
        "Objects" => "Объекты",
        "Labels" => "Подписи",
        "GmNotes" => "Заметки GM",
        _ => LayerKind
    };

    public string VisibilityDisplay => Visibility switch
    {
        "PlayerVisible" => "Видно игрокам",
        "GmOnly" => "Только GM",
        "Hidden" => "Скрыто",
        _ => Visibility
    };

    public string Label => $"{DisplayName} · {LayerKindDisplay} · {VisibilityDisplay}";

    public void Apply(IDictionary<string, object> payload)
    {
        DisplayName = FirstNonEmpty(Str(Get(payload, "displayName")), Str(Get(payload, "name")), DisplayName);
        LayerKind = FirstNonEmpty(Str(Get(payload, "layerKind")), LayerKind);
        SortOrder = Int(Get(payload, "sortOrder"), SortOrder);
        IsVisibleByDefault = Bool(Get(payload, "isVisibleByDefault"), IsVisibleByDefault);
        Visibility = FirstNonEmpty(Str(Get(payload, "visibility")), Visibility);
    }

    public void ApplyEditorState(IDictionary<string, object> payload)
    {
        LayerId = FirstNonEmpty(Str(Get(payload, "id")), LayerId);
        DisplayName = FirstNonEmpty(Str(Get(payload, "name")), DisplayName);
        LayerKind = FirstNonEmpty(Str(Get(payload, "layerKind")), LayerKind);
        SortOrder = Int(Get(payload, "order"), SortOrder);
        IsVisibleByDefault = Bool(Get(payload, "isVisible"), IsVisibleByDefault);
        IsLocked = Bool(Get(payload, "isLocked"), IsLocked);
        Revision = Long(Get(payload, "revision"), Revision);
        Opacity = Double(Get(payload, "opacity"), Opacity);
        Notify(nameof(IsLocked));
        Notify(nameof(Label));
    }

    public static SceneMapLayerUiItem From(IDictionary<string, object> payload)
    {
        var layer = new SceneMapLayerUiItem
        {
            LayerId = Str(Get(payload, "layerId")),
            SceneMapId = Str(Get(payload, "sceneMapId"))
        };
        layer.Apply(payload);
        return layer;
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static long Long(object? value, long fallback) => long.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class LocationMapAssetUiItem
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AssetKind { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string MaterialKey { get; set; } = string.Empty;
    public string TextureKey { get; set; } = string.Empty;
    public string PatternKey { get; set; } = string.Empty;
    public string AssetKey { get; set; } = string.Empty;
    public string VisualStyleKey { get; set; } = string.Empty;
    public string RenderMode { get; set; } = "TexturedShape";
    public string ShapeKind { get; set; } = "Rectangle";
    public string DefaultObjectKind { get; set; } = "Decoration";
    public double DefaultWidth { get; set; } = 80;
    public double DefaultHeight { get; set; } = 60;
    public double StrokeThickness { get; set; } = 1.4;
    public double VisualOpacity { get; set; } = 0.9;

    public string AssetKindDisplay => AssetKind switch
    {
        "TerrainTexture" => "Материал местности",
        "FloorTexture" => "Пол",
        "WallTexture" => "Стена",
        "RoadTexture" => "Дорога",
        "PropIcon" => "Объект",
        "BuildingPart" => "Здание",
        "ZoneOverlay" => "Зона",
        _ => AssetKind
    };

    public static IEnumerable<LocationMapAssetUiItem> CreateBuiltIn()
    {
        LocationMapAssetUiItem A(string id, string name, string kind, string category, string material, string texture, string asset, string style, string render, string shape, string objectKind, double width, double height, double stroke = 1.4, double opacity = 0.9)
        {
            return new LocationMapAssetUiItem
            {
                Id = id,
                DisplayName = name,
                AssetKind = kind,
                Category = category,
                MaterialKey = material,
                TextureKey = texture,
                PatternKey = texture,
                AssetKey = asset,
                VisualStyleKey = style,
                RenderMode = render,
                ShapeKind = shape,
                DefaultObjectKind = objectKind,
                DefaultWidth = width,
                DefaultHeight = height,
                StrokeThickness = stroke,
                VisualOpacity = opacity
            };
        }

        return new[]
        {
            A("asset_grass", "Трава", "TerrainTexture", "Местность", "grass", "grass_noise", "", "terrain.grass", "ZoneOverlay", "Rectangle", "TerrainZone", 160, 120),
            A("asset_cobblestone", "Булыжная мостовая", "TerrainTexture", "Местность", "cobblestone", "cobble_small", "", "terrain.cobblestone", "ZoneOverlay", "Rectangle", "TerrainZone", 180, 120),
            A("asset_water", "Вода", "TerrainTexture", "Местность", "water", "water_ripple", "", "terrain.water", "ZoneOverlay", "Rectangle", "TerrainZone", 140, 100),
            A("asset_road", "Дорога", "RoadTexture", "Дороги", "packed_dirt", "dirt_track", "", "road.main", "RoadPath", "Polyline", "Road", 240, 45, 6),
            A("asset_alley", "Переулок", "RoadTexture", "Дороги", "dark_stone", "narrow_stone", "", "road.alley", "RoadPath", "Polyline", "Alley", 120, 30, 4),
            A("asset_bridge", "Мост", "RoadTexture", "Дороги", "wood_floor", "wood_planks", "bridge_wood", "road.bridge", "AssetStamp", "Rectangle", "Road", 120, 45, 2),
            A("asset_wall", "Стена", "WallTexture", "Здания", "stone", "stone_tiles", "", "structure.wall", "LineWall", "Line", "Wall", 160, 18, 7),
            A("asset_room", "Комната", "FloorTexture", "Здания", "wood_floor", "wood_planks", "", "building.room", "TexturedShape", "Rectangle", "Room", 120, 90),
            A("asset_shop", "Магазин", "BuildingPart", "Здания", "shop_floor", "stone_tiles", "signboard", "building.shop", "TexturedShape", "Rectangle", "ShopArea", 150, 110),
            A("asset_tavern", "Трактир", "BuildingPart", "Интерьер", "tavern_floor", "wood_planks", "signboard", "building.tavern", "TexturedShape", "Rectangle", "TavernArea", 180, 120),
            A("asset_storage", "Склад", "BuildingPart", "Интерьер", "warehouse_floor", "wood_planks", "crate", "building.storage", "TexturedShape", "Rectangle", "StorageArea", 150, 110),
            A("asset_market_stall", "Торговая лавка", "PropIcon", "Рынок / магазин", "canvas_red", "canvas_stripe", "market_stall", "prop.market_stall", "AssetStamp", "Rectangle", "MarketStall", 80, 55),
            A("asset_counter", "Прилавок", "PropIcon", "Рынок / магазин", "wood_floor", "wood_planks", "counter", "prop.counter", "AssetStamp", "Rectangle", "Decoration", 70, 28),
            A("asset_shelves", "Полки", "PropIcon", "Рынок / магазин", "wood_floor", "wood_planks", "shelf", "prop.shelves", "AssetStamp", "Rectangle", "Decoration", 45, 70),
            A("asset_crates", "Ящики", "PropIcon", "Рынок / магазин", "wood_floor", "wood_planks", "crate", "prop.crates", "AssetStamp", "Rectangle", "Decoration", 48, 38),
            A("asset_barrels", "Бочки", "PropIcon", "Рынок / магазин", "wood_floor", "wood_planks", "barrel", "prop.barrels", "AssetStamp", "Circle", "Decoration", 44, 44),
            A("asset_cart", "Телега", "PropIcon", "Рынок / магазин", "wood_floor", "wood_planks", "cart", "prop.cart", "AssetStamp", "Rectangle", "Decoration", 85, 45),
            A("asset_lantern", "Фонарь", "PropIcon", "Декор", "iron_wood", "gate_planks", "lantern", "prop.lantern", "AssetStamp", "Rectangle", "Decoration", 28, 42),
            A("asset_well", "Колодец", "PropIcon", "Декор", "stone", "stone_tiles", "well", "prop.well", "AssetStamp", "Circle", "Decoration", 62, 62),
            A("asset_table", "Стол", "PropIcon", "Интерьер", "wood_floor", "wood_planks", "table", "prop.table", "AssetStamp", "Rectangle", "Decoration", 70, 45),
            A("asset_bench", "Скамья", "PropIcon", "Интерьер", "wood_floor", "wood_planks", "chair_or_bench", "prop.bench", "AssetStamp", "Rectangle", "Cover", 90, 28),
            A("asset_bed", "Кровать", "PropIcon", "Интерьер", "wood_floor", "wood_planks", "bed", "prop.bed", "AssetStamp", "Rectangle", "Decoration", 75, 45),
            A("asset_hearth", "Очаг", "PropIcon", "Интерьер", "hazard_red_overlay", "hazard_cross", "hearth", "prop.hearth", "AssetStamp", "Circle", "Decoration", 55, 55),
            A("asset_bar_counter", "Барная стойка", "PropIcon", "Интерьер", "wood_floor", "wood_planks", "bar_counter", "prop.bar_counter", "AssetStamp", "Rectangle", "Decoration", 110, 30),
            A("asset_signboard", "Вывеска", "PropIcon", "Улица", "wood_floor", "wood_planks", "signboard", "prop.signboard", "AssetStamp", "Rectangle", "Decoration", 42, 24),
            A("asset_fence", "Забор", "PropIcon", "Улица", "wood_floor", "wood_planks", "fence", "prop.fence", "AssetStamp", "Rectangle", "Obstacle", 90, 14),
            A("asset_door", "Дверь", "PropIcon", "Улица", "iron_wood", "gate_planks", "door", "prop.door", "AssetStamp", "Rectangle", "Entrance", 20, 34),
            A("asset_window", "Окно", "PropIcon", "Улица", "stone_tiles", "stone_tiles", "window", "prop.window", "AssetStamp", "Rectangle", "Decoration", 28, 18),
            A("asset_stairs", "Лестница", "PropIcon", "Улица", "stone_tiles", "stone_tiles", "stairs", "prop.stairs", "AssetStamp", "Rectangle", "Decoration", 42, 55),
            A("asset_tent", "Палатка", "PropIcon", "Лагерь", "canvas_red", "canvas_stripe", "tent", "prop.tent", "AssetStamp", "Polygon", "Decoration", 90, 70),
            A("asset_campfire", "Костёр", "PropIcon", "Лагерь", "hazard", "hazard_cross", "campfire", "prop.campfire", "AssetStamp", "Circle", "Decoration", 50, 50),
            A("asset_tree", "Дерево / куст", "PropIcon", "Декор", "grass", "grass_noise", "tree", "prop.tree", "AssetStamp", "Circle", "Obstacle", 70, 70),
            A("asset_bush", "Куст", "PropIcon", "Декор", "grass", "grass_noise", "bush", "prop.bush", "AssetStamp", "Circle", "Obstacle", 45, 45),
            A("asset_rock", "Камень", "PropIcon", "Декор", "stone", "stone_tiles", "rock", "prop.rock", "AssetStamp", "Circle", "Obstacle", 42, 36),
            A("asset_log", "Бревно", "PropIcon", "Декор", "wood_planks", "wood_planks", "log", "prop.log", "AssetStamp", "Rectangle", "Cover", 75, 22),
            A("asset_hazard", "Опасная зона", "ZoneOverlay", "Опасности", "hazard_red_overlay", "hazard_cross", "hazard_zone", "overlay.hazard", "ZoneOverlay", "Circle", "HazardZone", 90, 90, 2, 0.72),
            A("asset_cover", "Укрытие", "PropIcon", "Зоны", "stone", "stone_tiles", "cover_low", "overlay.cover", "AssetStamp", "Rectangle", "Cover", 80, 45),
            A("asset_cover_high", "Высокое укрытие", "PropIcon", "Зоны", "stone", "stone_tiles", "cover_high", "overlay.cover_high", "AssetStamp", "Rectangle", "Cover", 65, 65),
            A("asset_obstacle", "Препятствие", "PropIcon", "Зоны", "dark_stone", "stone_tiles", "obstacle", "overlay.obstacle", "AssetStamp", "Rectangle", "Obstacle", 75, 55),
            A("asset_objective", "Зона цели", "ZoneOverlay", "Зоны", "objective_gold_overlay", "objective_hatch", "objective_marker", "overlay.objective", "ZoneOverlay", "Rectangle", "ObjectiveZone", 120, 80, 2, 0.62),
            A("asset_spawn", "Зона старта", "ZoneOverlay", "Зоны", "spawn_blue_overlay", "spawn_grid", "spawn_zone", "overlay.spawn", "ZoneOverlay", "Rectangle", "SpawnZone", 120, 80, 2, 0.62)
        };
    }
}

public sealed class LocationMapOptionUiItem
{
    public LocationMapOptionUiItem(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
    }

    public string Key { get; set; }
    public string DisplayName { get; set; }

    public override string ToString() => DisplayName;
}

public sealed class SceneMapTileLayerUiItem : ViewModelBase
{
    private string _displayName = string.Empty;
    private double _tileSizeMeters = 5;
    private int _sortOrder;
    private string _visibility = "PlayerVisible";

    public string TileLayerId { get; set; } = string.Empty;
    public string SceneMapId { get; set; } = string.Empty;
    public long Revision { get; set; } = 1;
    public bool IsLocked { get; set; }
    public string DisplayName { get => _displayName; set { if (_displayName != value) { _displayName = value; Notify(); Notify(nameof(Label)); } } }
    public double TileSizeMeters { get => _tileSizeMeters; set { if (Math.Abs(_tileSizeMeters - value) > 0.0001) { _tileSizeMeters = value; Notify(); Notify(nameof(Label)); } } }
    public int SortOrder { get => _sortOrder; set { if (_sortOrder != value) { _sortOrder = value; Notify(); Notify(nameof(Label)); } } }
    public bool IsVisibleByDefault { get; set; } = true;
    public string Visibility { get => _visibility; set { if (_visibility != value) { _visibility = value; Notify(); Notify(nameof(VisibilityDisplay)); Notify(nameof(Label)); } } }
    public string VisibilityDisplay => Visibility switch { "PlayerVisible" => "Видно игрокам", "GmOnly" => "Только GM", "Hidden" => "Скрыто", _ => Visibility };
    public string Label => $"{DisplayName} · tile {TileSizeMeters:0.#} м · {VisibilityDisplay}";

    public static SceneMapTileLayerUiItem From(IDictionary<string, object> payload)
    {
        return new SceneMapTileLayerUiItem
        {
            TileLayerId = Str(Get(payload, "tileLayerId")),
            SceneMapId = Str(Get(payload, "sceneMapId")),
            DisplayName = FirstNonEmpty(Str(Get(payload, "displayName")), Str(Get(payload, "name")), "Материалы локации"),
            TileSizeMeters = Double(Get(payload, "tileSizeMeters"), 5),
            SortOrder = Int(Get(payload, "sortOrder"), 10),
            IsVisibleByDefault = Bool(Get(payload, "isVisibleByDefault"), true),
            Visibility = FirstNonEmpty(Str(Get(payload, "visibility")), "PlayerVisible")
        };
    }

    public void ApplyEditorState(IDictionary<string, object> payload)
    {
        Revision = Long(Get(payload, "revision"), Revision);
        IsLocked = Bool(Get(payload, "isLocked"), IsLocked);
        SortOrder = Int(Get(payload, "order"), SortOrder);
        Notify(nameof(IsLocked));
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static long Long(object? value, long fallback) => long.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class SceneMapTilePatchUiItem : ViewModelBase
{
    private bool _isSelected;
    public string TilePatchId { get; set; } = string.Empty;
    public string TileLayerId { get; set; } = string.Empty;
    public string SceneMapId { get; set; } = string.Empty;
    public long Revision { get; set; } = 1;
    public string MaterialKey { get; set; } = "grass";
    public string TextureKey { get; set; } = "grass_noise";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 1;
    public double Height { get; set; } = 1;
    public double RotationDegrees { get; set; }
    public double Opacity { get; set; } = 1;
    public int SortOrder { get; set; }
    public string Visibility { get; set; } = "PlayerVisible";
    public double PixelX { get; set; }
    public double PixelY { get; set; }
    public double PixelWidth { get; set; }
    public double PixelHeight { get; set; }
    public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; Notify(); } } }
    public Brush FillBrush => LocationMapVisualBrushes.MaterialBrush(FirstNonEmpty(MaterialKey, TextureKey));
    public Brush StrokeBrush => LocationMapVisualBrushes.StrokeBrush("terrain.tile");
    public string MaterialDisplay => LocationMapVisualBrushes.MaterialDisplayName(MaterialKey);
    public string Label => $"{MaterialDisplay} · {Width:0.#}×{Height:0.#} м";
    public int VisualZIndex => Math.Max(1, SortOrder);

    public void ApplyScale(double scale, double offsetX = 0d, double offsetY = 0d)
    {
        PixelX = MapCanvasProjectionHelper.ToPixel(X, scale) + offsetX;
        PixelY = MapCanvasProjectionHelper.ToPixel(Y, scale) + offsetY;
        PixelWidth = Math.Max(2, MapCanvasProjectionHelper.ToPixel(Width, scale));
        PixelHeight = Math.Max(2, MapCanvasProjectionHelper.ToPixel(Height, scale));
        Notify(nameof(PixelX));
        Notify(nameof(PixelY));
        Notify(nameof(PixelWidth));
        Notify(nameof(PixelHeight));
    }

    public static SceneMapTilePatchUiItem From(IDictionary<string, object> payload)
    {
        return new SceneMapTilePatchUiItem
        {
            TilePatchId = FirstNonEmpty(Str(Get(payload, "tilePatchId")), Str(Get(payload, "patchId")), Str(Get(payload, "id"))),
            TileLayerId = FirstNonEmpty(Str(Get(payload, "tileLayerId")), Str(Get(payload, "layerId"))),
            SceneMapId = Str(Get(payload, "sceneMapId")),
            MaterialKey = FirstNonEmpty(Str(Get(payload, "materialKey")), "grass"),
            TextureKey = FirstNonEmpty(Str(Get(payload, "textureKey")), LocationMapVisualBrushes.DefaultTextureForMaterial(Str(Get(payload, "materialKey")))),
            X = Double(Get(payload, "x"), 0),
            Y = Double(Get(payload, "y"), 0),
            Width = Double(Get(payload, "width"), 1),
            Height = Double(Get(payload, "height"), 1),
            RotationDegrees = Double(Get(payload, "rotationDegrees"), 0),
            Opacity = Double(Get(payload, "opacity"), 1),
            SortOrder = Int(Get(payload, "sortOrder"), 10),
            Visibility = FirstNonEmpty(Str(Get(payload, "visibility")), "PlayerVisible")
        };
    }

    public void ApplyEditorState(IDictionary<string, object> payload)
    {
        Revision = Long(Get(payload, "revision"), Revision);
        TileLayerId = FirstNonEmpty(Str(Get(payload, "layerId")), TileLayerId);
        X = Double(Get(payload, "x"), X);
        Y = Double(Get(payload, "y"), Y);
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static long Long(object? value, long fallback) => long.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class SceneMapAssetInstanceUiItem : ViewModelBase
{
    private bool _isSelected;
    public string AssetInstanceId { get; set; } = string.Empty;
    public string SceneMapId { get; set; } = string.Empty;
    public string LayerId { get; set; } = string.Empty;
    public long Revision { get; set; } = 1;
    public string AssetKey { get; set; } = "crate";
    public string DisplayName { get; set; } = "Объект карты";
    public string AssetKind { get; set; } = "Prop";
    public string ObjectKind { get; set; } = "Decoration";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 5;
    public double Height { get; set; } = 5;
    public double RotationDegrees { get; set; }
    public int ZIndex { get; set; } = 100;
    public string Visibility { get; set; } = "PlayerVisible";
    public string DescriptionPlayer { get; set; } = string.Empty;
    public string DescriptionGm { get; set; } = string.Empty;
    public bool BlocksMovement { get; set; }
    public bool BlocksVision { get; set; }
    public bool ProvidesCover { get; set; }
    public bool IsInteractable { get; set; }
    public string LinkedEntityType { get; set; } = "None";
    public string LinkedEntityId { get; set; } = string.Empty;
    public double PixelX { get; set; }
    public double PixelY { get; set; }
    public double PixelWidth { get; set; }
    public double PixelHeight { get; set; }
    public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; Notify(); } } }
    public Brush FillBrush => LocationMapVisualBrushes.AssetBackplateBrush(AssetKey);
    public Brush StrokeBrush => LocationMapVisualBrushes.StrokeBrush(ObjectKind);
    public Brush GlyphBrush => LocationMapVisualBrushes.AssetBrush(AssetKey);
    public string AssetGlyph => LocationMapVisualBrushes.AssetGlyph(AssetKey);
    public string AssetKindDisplay => LocationMapVisualBrushes.AssetKindDisplayName(AssetKind);
    public string VisibilityDisplay => Visibility switch { "PlayerVisible" => "Видно игрокам", "GmOnly" => "Только GM", "Hidden" => "Скрыто", _ => Visibility };
    public string Label => $"{DisplayName} · {AssetKindDisplay} · {VisibilityDisplay}";
    public int VisualZIndex => Math.Max(100, ZIndex);

    public void Apply(IDictionary<string, object> payload)
    {
        var next = From(payload);
        AssetInstanceId = next.AssetInstanceId;
        SceneMapId = next.SceneMapId;
        AssetKey = next.AssetKey;
        DisplayName = next.DisplayName;
        AssetKind = next.AssetKind;
        ObjectKind = next.ObjectKind;
        X = next.X;
        Y = next.Y;
        Width = next.Width;
        Height = next.Height;
        RotationDegrees = next.RotationDegrees;
        ZIndex = next.ZIndex;
        Visibility = next.Visibility;
        DescriptionPlayer = next.DescriptionPlayer;
        DescriptionGm = next.DescriptionGm;
        BlocksMovement = next.BlocksMovement;
        BlocksVision = next.BlocksVision;
        ProvidesCover = next.ProvidesCover;
        IsInteractable = next.IsInteractable;
        LinkedEntityType = next.LinkedEntityType;
        LinkedEntityId = next.LinkedEntityId;
        Notify(nameof(Label));
    }

    public void ApplyEditorState(IDictionary<string, object> payload)
    {
        Revision = Long(Get(payload, "revision"), Revision);
        LayerId = FirstNonEmpty(Str(Get(payload, "layerId")), LayerId);
        X = Double(Get(payload, "x"), X);
        Y = Double(Get(payload, "y"), Y);
    }

    public void ApplyScale(double scale, double offsetX = 0d, double offsetY = 0d)
    {
        PixelX = MapCanvasProjectionHelper.ToPixel(X, scale) + offsetX;
        PixelY = MapCanvasProjectionHelper.ToPixel(Y, scale) + offsetY;
        PixelWidth = Math.Max(8, MapCanvasProjectionHelper.ToPixel(Width, scale));
        PixelHeight = Math.Max(8, MapCanvasProjectionHelper.ToPixel(Height, scale));
        Notify(nameof(PixelX));
        Notify(nameof(PixelY));
        Notify(nameof(PixelWidth));
        Notify(nameof(PixelHeight));
    }

    public static SceneMapAssetInstanceUiItem From(IDictionary<string, object> payload)
    {
        return new SceneMapAssetInstanceUiItem
        {
            AssetInstanceId = FirstNonEmpty(Str(Get(payload, "assetInstanceId")), Str(Get(payload, "assetId")), Str(Get(payload, "id"))),
            SceneMapId = Str(Get(payload, "sceneMapId")),
            AssetKey = FirstNonEmpty(Str(Get(payload, "assetKey")), "crate"),
            DisplayName = FirstNonEmpty(Str(Get(payload, "displayName")), Str(Get(payload, "name")), "Объект карты"),
            AssetKind = FirstNonEmpty(Str(Get(payload, "assetKind")), "Prop"),
            ObjectKind = FirstNonEmpty(Str(Get(payload, "objectKind")), "Decoration"),
            X = Double(Get(payload, "x"), 0),
            Y = Double(Get(payload, "y"), 0),
            Width = Double(Get(payload, "width"), 5),
            Height = Double(Get(payload, "height"), 5),
            RotationDegrees = Double(Get(payload, "rotationDegrees"), 0),
            ZIndex = Int(Get(payload, "zIndex"), 100),
            Visibility = FirstNonEmpty(Str(Get(payload, "visibility")), "PlayerVisible"),
            DescriptionPlayer = Str(Get(payload, "descriptionPlayer")),
            DescriptionGm = Str(Get(payload, "descriptionGm")),
            BlocksMovement = Bool(Get(payload, "blocksMovement"), false),
            BlocksVision = Bool(Get(payload, "blocksVision"), false),
            ProvidesCover = Bool(Get(payload, "providesCover"), false),
            IsInteractable = Bool(Get(payload, "isInteractable"), false),
            LinkedEntityType = FirstNonEmpty(Str(Get(payload, "linkedEntityType")), "None"),
            LinkedEntityId = Str(Get(payload, "linkedEntityId"))
        };
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static long Long(object? value, long fallback) => long.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public static class LocationMapVisualBrushes
{
    public static Brush MaterialBrush(string key)
    {
        var normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
        var baseColor = normalized switch
        {
            "grass" or "terrain" => Color.FromRgb(66, 107, 44),
            "dirt" or "packed_dirt" or "road" or "road_dirt" => Color.FromRgb(137, 93, 49),
            "stone" or "stone_floor" or "dark_stone" or "stone_tiles" or "shop_floor" => Color.FromRgb(95, 109, 126),
            "sand" => Color.FromRgb(178, 146, 88),
            "mud" => Color.FromRgb(83, 67, 43),
            "water" or "shallow_water" => Color.FromRgb(35, 112, 151),
            "wood_floor" or "wood_planks" or "bridge_wood" or "warehouse_floor" => Color.FromRgb(127, 82, 45),
            "warm_wood" or "tavern" or "tavern_floor" => Color.FromRgb(142, 79, 43),
            "cobblestone" or "market_square_cobble" or "alley_stone" => Color.FromRgb(113, 122, 132),
            "roof_tile" => Color.FromRgb(136, 53, 44),
            "canvas_red" or "stall" => Color.FromRgb(157, 73, 47),
            "iron_wood" or "entrance" => Color.FromRgb(92, 65, 48),
            "hazard" or "hazard_red_overlay" => Color.FromRgb(162, 62, 42),
            "objective_gold_overlay" => Color.FromRgb(190, 139, 35),
            "spawn_blue_overlay" => Color.FromRgb(41, 92, 154),
            "gm_overlay" or "gm" => Color.FromRgb(95, 79, 132),
            _ => Color.FromRgb(86, 100, 117)
        };
        var dark = Color.Multiply(baseColor, 0.72f);
        var light = Color.Multiply(baseColor, 1.18f);
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(baseColor), null, new RectangleGeometry(new Rect(0, 0, 18, 18))));
        group.Children.Add(new GeometryDrawing(null, new Pen(new SolidColorBrush(dark), 1), new LineGeometry(new Point(0, 9), new Point(18, 9))));
        group.Children.Add(new GeometryDrawing(null, new Pen(new SolidColorBrush(light), 0.8), new LineGeometry(new Point(9, 0), new Point(9, 18))));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(45, light.R, light.G, light.B)), null, new EllipseGeometry(new Point(5, 5), 2, 2)));
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 18, 18),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        brush.Freeze();
        return brush;
    }

    public static string DefaultTextureForMaterial(string key)
    {
        return (key ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "grass" => "grass_noise",
            "dirt" or "road_dirt" or "packed_dirt" => "dirt_track",
            "mud" => "mud_mottle",
            "sand" => "sand_dots",
            "water" or "shallow_water" => "water_ripple",
            "wood_floor" or "wood_planks" or "warehouse_floor" or "bridge_wood" or "tavern_floor" => "wood_planks",
            "stone" or "stone_floor" or "stone_tiles" or "shop_floor" => "stone_tiles",
            "cobblestone" or "market_square_cobble" => "cobble_small",
            "alley_stone" => "narrow_stone",
            "hazard" or "hazard_red_overlay" => "hazard_cross",
            "objective_gold_overlay" => "objective_hatch",
            "spawn_blue_overlay" => "spawn_grid",
            _ => "cobble_small"
        };
    }

    public static string MaterialDisplayName(string key)
    {
        return (key ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "grass" => "Трава",
            "dirt" => "Земля",
            "mud" => "Грязь",
            "sand" => "Песок",
            "stone" => "Камень",
            "cobblestone" or "market_square_cobble" => "Булыжник",
            "wood_planks" or "wood_floor" => "Доски",
            "stone_tiles" or "stone_floor" => "Каменная плитка",
            "tavern_floor" => "Пол трактира",
            "shop_floor" => "Пол магазина",
            "warehouse_floor" => "Пол склада",
            "road_dirt" or "packed_dirt" => "Грунтовая дорога",
            "alley_stone" => "Каменный переулок",
            "bridge_wood" => "Деревянный мост",
            "shallow_water" or "water" => "Вода",
            "hazard_red_overlay" or "hazard" => "Опасность",
            "objective_gold_overlay" => "Цель",
            "spawn_blue_overlay" => "Старт",
            _ => key
        };
    }

    public static Brush AssetBackplateBrush(string key)
    {
        var normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
        var material = normalized switch
        {
            "market_stall" or "counter" or "shelf" or "crate" or "barrel" or "cart" or "signboard" => "wood_planks",
            "table" or "chair_or_bench" or "bed" or "bar_counter" => "tavern_floor",
            "hearth" or "campfire" => "hazard_red_overlay",
            "lantern" or "well" or "fence" or "door" or "window" or "stairs" => "stone_tiles",
            "tent" => "canvas_red",
            "tree" or "bush" => "grass",
            "rock" or "cover_low" or "cover_high" or "obstacle" => "stone",
            "hazard_zone" => "hazard_red_overlay",
            "objective_marker" => "objective_gold_overlay",
            "spawn_zone" => "spawn_blue_overlay",
            _ => "wood_planks"
        };
        return MaterialBrush(material);
    }

    public static string AssetKindDisplayName(string key)
    {
        return (key ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "market" => "Рынок / магазин",
            "interior" => "Интерьер",
            "street" => "Улица",
            "outdoor" => "Локация",
            "gameplay" => "Игровой объект",
            "building" => "Здание",
            _ => "Объект"
        };
    }

    public static Brush StrokeBrush(string key)
    {
        var normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
        return new SolidColorBrush(normalized switch
        {
            var value when value.Contains("road") => Color.FromRgb(231, 166, 77),
            var value when value.Contains("wall") || value.Contains("structure") => Color.FromRgb(218, 228, 240),
            var value when value.Contains("hazard") => Color.FromRgb(252, 165, 165),
            var value when value.Contains("tavern") => Color.FromRgb(251, 191, 36),
            var value when value.Contains("shop") => Color.FromRgb(103, 232, 249),
            var value when value.Contains("storage") => Color.FromRgb(203, 213, 225),
            var value when value.Contains("gm") => Color.FromRgb(216, 180, 254),
            _ => Color.FromRgb(226, 232, 240)
        });
    }

    public static Brush SchematicBrush(string objectKind)
    {
        return new SolidColorBrush((objectKind ?? string.Empty) switch
        {
            "TerrainZone" => Color.FromRgb(63, 98, 18),
            "Road" => Color.FromRgb(120, 53, 15),
            "Alley" => Color.FromRgb(75, 85, 99),
            "MarketStall" => Color.FromRgb(124, 45, 18),
            "ShopArea" => Color.FromRgb(22, 78, 99),
            "TavernArea" => Color.FromRgb(88, 28, 135),
            "StorageArea" => Color.FromRgb(51, 65, 85),
            "Entrance" => Color.FromRgb(6, 95, 70),
            _ => Color.FromRgb(71, 85, 105)
        });
    }

    public static Brush AssetBrush(string key)
    {
        return new SolidColorBrush((key ?? string.Empty).ToLowerInvariant() switch
        {
            var value when value.Contains("barrel") => Color.FromRgb(245, 158, 11),
            var value when value.Contains("crate") => Color.FromRgb(217, 119, 6),
            var value when value.Contains("cart") => Color.FromRgb(161, 98, 7),
            var value when value.Contains("lantern") => Color.FromRgb(250, 204, 21),
            var value when value.Contains("well") => Color.FromRgb(125, 211, 252),
            var value when value.Contains("campfire") => Color.FromRgb(248, 113, 113),
            var value when value.Contains("tree") => Color.FromRgb(134, 239, 172),
            _ => Color.FromRgb(248, 250, 252)
        });
    }

    public static string AssetGlyph(string key)
    {
        var normalized = (key ?? string.Empty).ToLowerInvariant();
        if (normalized.Contains("stall")) return "лавка";
        if (normalized.Contains("counter")) return "прилав.";
        if (normalized.Contains("shelf")) return "полка";
        if (normalized.Contains("shop")) return "выв.";
        if (normalized.Contains("tavern")) return "тракт.";
        if (normalized.Contains("crate")) return "ящ.";
        if (normalized.Contains("barrel")) return "боч.";
        if (normalized.Contains("cart")) return "тел.";
        if (normalized.Contains("sign")) return "выв.";
        if (normalized.Contains("table")) return "стол";
        if (normalized.Contains("chair") || normalized.Contains("bench")) return "скам.";
        if (normalized.Contains("bed")) return "кров.";
        if (normalized.Contains("hearth")) return "очаг";
        if (normalized.Contains("bar_counter")) return "стойк.";
        if (normalized.Contains("lantern")) return "фон.";
        if (normalized.Contains("well")) return "кол.";
        if (normalized.Contains("fence")) return "заб.";
        if (normalized.Contains("door")) return "дверь";
        if (normalized.Contains("window")) return "окно";
        if (normalized.Contains("stairs")) return "лест.";
        if (normalized.Contains("tent")) return "пал.";
        if (normalized.Contains("gate")) return "вор.";
        if (normalized.Contains("campfire")) return "огн.";
        if (normalized.Contains("tree")) return "дер.";
        if (normalized.Contains("bush")) return "куст";
        if (normalized.Contains("rock")) return "кам.";
        if (normalized.Contains("log")) return "брев.";
        if (normalized.Contains("cover")) return "укр.";
        if (normalized.Contains("obstacle")) return "преп.";
        if (normalized.Contains("hazard")) return "опас.";
        if (normalized.Contains("objective")) return "цель";
        if (normalized.Contains("spawn")) return "старт";
        return "";
    }

    public static Geometry BuildPathGeometry(string points, double originX, double originY, double widthMeters, double heightMeters, double pixelWidth, double pixelHeight)
    {
        var scaleX = widthMeters > 0 ? pixelWidth / widthMeters : 1d;
        var scaleY = heightMeters > 0 ? pixelHeight / heightMeters : scaleX;
        var parsed = new List<Point>();
        foreach (var part in (points ?? string.Empty).Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split(',');
            if (pieces.Length != 2) continue;
            if (double.TryParse(pieces[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                double.TryParse(pieces[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                parsed.Add(new Point((x - originX) * scaleX, (y - originY) * scaleY));
            }
        }

        if (parsed.Count == 0)
        {
            parsed.Add(new Point(0, pixelHeight / 2d));
            parsed.Add(new Point(Math.Max(12, pixelWidth), pixelHeight / 2d));
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(parsed[0], false, false);
            if (parsed.Count > 1)
                context.PolyLineTo(parsed.Skip(1).ToArray(), true, false);
        }
        geometry.Freeze();
        return geometry;
    }
}

public sealed class SceneMapShapeUiItem : ViewModelBase
{
    private string _displayName = string.Empty;
    private string _shapeKind = "Rectangle";
    private string _objectKind = "Decoration";
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private double _radius;
    private double _pixelX;
    private double _pixelY;
    private double _pixelWidth;
    private double _pixelHeight;
    private bool _isSelected;
    private bool _isMapVisualMode = true;

    public string ShapeId { get; set; } = string.Empty;
    public string SceneMapId { get; set; } = string.Empty;
    public string LayerId { get; set; } = string.Empty;
    public long Revision { get; set; } = 1;
    public string LayerName { get; set; } = string.Empty;
    public string DescriptionPlayer { get; set; } = string.Empty;
    public string DescriptionGm { get; set; } = string.Empty;
    public double RotationDegrees { get; set; }
    public string Points { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string FillKey { get; set; } = string.Empty;
    public string StrokeKey { get; set; } = string.Empty;
    public double Opacity { get; set; } = 0.65;
    public string MaterialKey { get; set; } = string.Empty;
    public string TextureKey { get; set; } = string.Empty;
    public string PatternKey { get; set; } = string.Empty;
    public string AssetKey { get; set; } = string.Empty;
    public string VisualStyleKey { get; set; } = string.Empty;
    public string RenderMode { get; set; } = "TexturedShape";
    public bool GridSnapEnabled { get; set; } = true;
    public double VisualOpacity { get; set; } = 0.88;
    public double StrokeThickness { get; set; } = 1.4;
    public int ZIndex { get; set; }
    public int SortOrder { get; set; }
    public string Visibility { get; set; } = "PlayerVisible";
    public bool BlocksMovement { get; set; }
    public bool BlocksVision { get; set; }
    public bool ProvidesCover { get; set; }
    public bool IsInteractable { get; set; }
    public string LinkedEntityType { get; set; } = "None";
    public string LinkedEntityId { get; set; } = string.Empty;

    public string DisplayName { get => _displayName; set { if (_displayName != value) { _displayName = value; Notify(); Notify(nameof(Label)); } } }
    public string ShapeKind { get => _shapeKind; set { if (_shapeKind != value) { _shapeKind = value; Notify(); Notify(nameof(Label)); } } }
    public string ObjectKind { get => _objectKind; set { if (_objectKind != value) { _objectKind = value; Notify(); Notify(nameof(ObjectKindDisplay)); Notify(nameof(Label)); } } }
    public double X { get => _x; set { if (Math.Abs(_x - value) > 0.0001) { _x = value; Notify(); Notify(nameof(Label)); } } }
    public double Y { get => _y; set { if (Math.Abs(_y - value) > 0.0001) { _y = value; Notify(); Notify(nameof(Label)); } } }
    public double Width { get => _width; set { if (Math.Abs(_width - value) > 0.0001) { _width = value; Notify(); } } }
    public double Height { get => _height; set { if (Math.Abs(_height - value) > 0.0001) { _height = value; Notify(); } } }
    public double Radius { get => _radius; set { if (Math.Abs(_radius - value) > 0.0001) { _radius = value; Notify(); } } }
    public double PixelX { get => _pixelX; private set { if (Math.Abs(_pixelX - value) > 0.0001) { _pixelX = value; Notify(); } } }
    public double PixelY { get => _pixelY; private set { if (Math.Abs(_pixelY - value) > 0.0001) { _pixelY = value; Notify(); } } }
    public double PixelWidth { get => _pixelWidth; private set { if (Math.Abs(_pixelWidth - value) > 0.0001) { _pixelWidth = value; Notify(); } } }
    public double PixelHeight { get => _pixelHeight; private set { if (Math.Abs(_pixelHeight - value) > 0.0001) { _pixelHeight = value; Notify(); } } }
    public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; Notify(); } } }
    public bool IsMapVisualMode
    {
        get => _isMapVisualMode;
        set
        {
            if (_isMapVisualMode == value) return;
            _isMapVisualMode = value;
            Notify();
            Notify(nameof(MapVisualVisibility));
            Notify(nameof(SchematicVisibility));
        }
    }
    public System.Windows.Visibility MapVisualVisibility => IsMapVisualMode ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public System.Windows.Visibility SchematicVisibility => IsMapVisualMode ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    public System.Windows.Visibility AreaVisibility => IsPathLike ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    public System.Windows.Visibility PathVisibility => IsPathLike ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public bool IsPathLike => RenderMode is "RoadPath" or "LineWall" || ShapeKind is "Polyline" or "Line";
    public bool IsAssetStamp => RenderMode == "AssetStamp" || !string.IsNullOrWhiteSpace(AssetKey);
    public Brush VisualFillBrush => LocationMapVisualBrushes.MaterialBrush(FirstNonEmpty(MaterialKey, TextureKey, FillKey, ObjectKind));
    public Brush VisualStrokeBrush => LocationMapVisualBrushes.StrokeBrush(FirstNonEmpty(VisualStyleKey, ObjectKind, StrokeKey));
    public Brush SchematicFillBrush => LocationMapVisualBrushes.SchematicBrush(ObjectKind);
    public Brush AssetGlyphBrush => LocationMapVisualBrushes.AssetBrush(FirstNonEmpty(AssetKey, ObjectKind));
    public double EffectiveVisualOpacity => Math.Max(0.15d, Math.Min(1d, VisualOpacity > 0 ? VisualOpacity : Opacity));
    public double EffectiveStrokeThickness => Math.Max(1d, StrokeThickness);
    public int VisualZIndex => ZIndex != 0 ? ZIndex : SortOrder;
    public string AssetGlyph => LocationMapVisualBrushes.AssetGlyph(FirstNonEmpty(AssetKey, ObjectKind));
    public string VisualLabel => string.IsNullOrWhiteSpace(Text) ? DisplayName : Text;
    public Geometry PathGeometry => LocationMapVisualBrushes.BuildPathGeometry(Points, X, Y, Width, Height, PixelWidth, PixelHeight);

    public string ObjectKindDisplay => ObjectKind switch
    {
        "TerrainZone" => "Зона местности",
        "Building" => "Здание",
        "Room" => "Помещение",
        "Wall" => "Стена",
        "Road" => "Дорога",
        "Alley" => "Переулок",
        "Door" => "Дверь",
        "Entrance" => "Вход",
        "Exit" => "Выход",
        "Cover" => "Укрытие",
        "Obstacle" => "Препятствие",
        "HazardZone" => "Опасная зона",
        "MarketStall" => "Торговая лавка",
        "ShopArea" => "Магазин",
        "TavernArea" => "Трактир",
        "StorageArea" => "Склад",
        "ObjectiveZone" => "Цель",
        "SpawnZone" => "Стартовая зона",
        "Decoration" => "Декорация",
        "TextLabel" => "Подпись",
        "GmNote" => "Заметка GM",
        _ => ObjectKind
    };

    public string VisibilityDisplay => Visibility switch
    {
        "PlayerVisible" => "Видно игрокам",
        "GmOnly" => "Только GM",
        "Hidden" => "Скрыто",
        _ => Visibility
    };

    public string Label => $"{DisplayName} · {ObjectKindDisplay} · X={X:0.##}, Y={Y:0.##}";

    public void Apply(IDictionary<string, object> payload)
    {
        DisplayName = FirstNonEmpty(Str(Get(payload, "displayName")), Str(Get(payload, "name")), DisplayName);
        DescriptionPlayer = Str(Get(payload, "descriptionPlayer"));
        DescriptionGm = Str(Get(payload, "descriptionGm"));
        ShapeKind = FirstNonEmpty(Str(Get(payload, "shapeKind")), ShapeKind);
        ObjectKind = FirstNonEmpty(Str(Get(payload, "objectKind")), ObjectKind);
        LayerId = Str(Get(payload, "layerId"));
        LayerName = Str(Get(payload, "layerName"));
        X = Double(Get(payload, "x"), X);
        Y = Double(Get(payload, "y"), Y);
        Width = Double(Get(payload, "width"), Width);
        Height = Double(Get(payload, "height"), Height);
        Radius = Double(Get(payload, "radius"), Radius);
        RotationDegrees = Double(Get(payload, "rotationDegrees"), RotationDegrees);
        Points = Str(Get(payload, "points"));
        Text = Str(Get(payload, "text"));
        FillKey = Str(Get(payload, "fillKey"));
        StrokeKey = Str(Get(payload, "strokeKey"));
        Opacity = Double(Get(payload, "opacity"), Opacity);
        MaterialKey = FirstNonEmpty(Str(Get(payload, "materialKey")), MaterialKey, FillKey);
        TextureKey = Str(Get(payload, "textureKey"));
        PatternKey = Str(Get(payload, "patternKey"));
        AssetKey = Str(Get(payload, "assetKey"));
        VisualStyleKey = Str(Get(payload, "visualStyleKey"));
        RenderMode = FirstNonEmpty(Str(Get(payload, "renderMode")), RenderMode);
        GridSnapEnabled = Bool(Get(payload, "gridSnapEnabled"), GridSnapEnabled);
        VisualOpacity = Double(Get(payload, "visualOpacity"), VisualOpacity);
        StrokeThickness = Double(Get(payload, "strokeThickness"), StrokeThickness);
        ZIndex = Int(Get(payload, "zIndex"), ZIndex);
        SortOrder = Int(Get(payload, "sortOrder"), SortOrder);
        Visibility = FirstNonEmpty(Str(Get(payload, "visibility")), Visibility);
        BlocksMovement = Bool(Get(payload, "blocksMovement"), BlocksMovement);
        BlocksVision = Bool(Get(payload, "blocksVision"), BlocksVision);
        ProvidesCover = Bool(Get(payload, "providesCover"), ProvidesCover);
        IsInteractable = Bool(Get(payload, "isInteractable"), IsInteractable);
        LinkedEntityType = FirstNonEmpty(Str(Get(payload, "linkedEntityType")), "None");
        LinkedEntityId = Str(Get(payload, "linkedEntityId"));
        Notify(nameof(ObjectKindDisplay));
        Notify(nameof(VisibilityDisplay));
        Notify(nameof(VisualFillBrush));
        Notify(nameof(VisualStrokeBrush));
        Notify(nameof(SchematicFillBrush));
        Notify(nameof(AssetGlyph));
        Notify(nameof(IsAssetStamp));
        Notify(nameof(VisualLabel));
        Notify(nameof(PathGeometry));
    }

    public void ApplyEditorState(IDictionary<string, object> payload)
    {
        Revision = Long(Get(payload, "revision"), Revision);
        LayerId = FirstNonEmpty(Str(Get(payload, "layerId")), LayerId);
        X = Double(Get(payload, "x"), X);
        Y = Double(Get(payload, "y"), Y);
    }

    public void ApplyScale(double scale, double offsetX = 0d, double offsetY = 0d)
    {
        PixelX = MapCanvasProjectionHelper.ToPixel(X, scale) + offsetX;
        PixelY = MapCanvasProjectionHelper.ToPixel(Y, scale) + offsetY;
        var widthMeters = Width > 0 ? Width : Math.Max(1, Radius * 2);
        var heightMeters = Height > 0 ? Height : Math.Max(1, Radius * 2);
        PixelWidth = Math.Max(8, MapCanvasProjectionHelper.ToPixel(widthMeters, scale));
        PixelHeight = Math.Max(8, MapCanvasProjectionHelper.ToPixel(heightMeters, scale));
        Notify(nameof(PathGeometry));
    }

    public static SceneMapShapeUiItem From(IDictionary<string, object> payload)
    {
        var shape = new SceneMapShapeUiItem
        {
            ShapeId = Str(Get(payload, "shapeId")),
            SceneMapId = Str(Get(payload, "sceneMapId"))
        };
        shape.Apply(payload);
        return shape;
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static long Long(object? value, long fallback) => long.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
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
    public string Visibility { get; set; } = "PlayerVisible";
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
        "PartyStart" => "Старт группы",
        "PointOfInterest" => "Точка интереса",
        "Entrance" => "Вход",
        "Exit" => "Выход",
        "Hazard" => "Опасность",
        "Objective" => "Цель",
        "GmNote" => "Заметка GM",
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
        Visibility = FirstNonEmpty(Str(Get(payload, "visibility")), Str(Get(payload, "visibilityMode")), IsPlayerVisible ? "PlayerVisible" : "Hidden");
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

    public override string ToString() => $"{Name} • {MarkerTypeDisplay} • X={X:0.#}, Y={Y:0.#}";

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class SceneTokenUiItem : ViewModelBase
{
    private string _displayName = string.Empty;
    private string _tokenType = "Object";
    private double _x;
    private double _y;
    private double _pixelX;
    private double _pixelY;
    private bool _isSelected;

    public string TokenId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string Visibility { get; set; } = "PlayerVisible";
    public string DescriptionPlayer { get; set; } = string.Empty;
    public string DescriptionGm { get; set; } = string.Empty;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public bool CanJoinCombat { get; set; }
    public long Revision { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; }

    public string DisplayName
    {
        get => _displayName;
        set { if (_displayName != value) { _displayName = value; Notify(); Notify(nameof(Label)); } }
    }

    public string TokenType
    {
        get => _tokenType;
        set { if (_tokenType != value) { _tokenType = value; Notify(); Notify(nameof(TokenTypeDisplay)); Notify(nameof(Label)); } }
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

    public string TokenTypeDisplay => TokenType switch
    {
        "Party" => "Группа",
        "PlayerCharacter" => "Персонаж",
        "Companion" => "Спутник",
        "Npc" => "NPC",
        "Enemy" => "Противник",
        "Object" => "Объект",
        "Hazard" => "Опасность",
        "Objective" => "Цель",
        "Vehicle" => "Техника",
        "GmNote" => "GM-заметка",
        _ => "Другое"
    };

    public string VisibilityDisplay => Visibility switch
    {
        "PlayerVisible" => "Видно игрокам",
        "GmOnly" => "Только GM",
        "Hidden" => "Скрыто",
        _ => Visibility
    };

    public string BindingDisplayText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LinkedEntityType) && string.IsNullOrWhiteSpace(LinkedEntityId))
                return "Без привязки";
            return string.IsNullOrWhiteSpace(LinkedEntityId)
                ? LinkedEntityType
                : $"{LinkedEntityType}: {LinkedEntityId}";
        }
    }

    public string Label => $"{DisplayName} [{TokenTypeDisplay}] X={X:0.##}, Y={Y:0.##}";

    public void Apply(IDictionary<string, object> payload)
    {
        DisplayName = FirstNonEmpty(Str(Get(payload, "displayName")), Str(Get(payload, "name")), DisplayName);
        TokenType = FirstNonEmpty(Str(Get(payload, "tokenType")), TokenType);
        X = Double(Get(payload, "x"), X);
        Y = Double(Get(payload, "y"), Y);
        Visibility = FirstNonEmpty(Str(Get(payload, "visibility")), Visibility);
        DescriptionPlayer = Str(Get(payload, "descriptionPlayer"));
        DescriptionGm = Str(Get(payload, "descriptionGm"));
        LinkedEntityType = Str(Get(payload, "linkedEntityType"));
        LinkedEntityId = Str(Get(payload, "linkedEntityId"));
        CanJoinCombat = Bool(Get(payload, "canJoinCombat"), CanJoinCombat);
        UpdatedAtUtc = Date(Get(payload, "updatedAtUtc"));
        Revision = Long(Get(payload, "revision"), Revision);
        Notify(nameof(VisibilityDisplay));
        Notify(nameof(BindingDisplayText));
    }

    public static SceneTokenUiItem From(IDictionary<string, object> payload)
    {
        var token = new SceneTokenUiItem
        {
            TokenId = Str(Get(payload, "tokenId")),
            MapId = Str(Get(payload, "mapId")),
            CampaignId = Str(Get(payload, "campaignId"))
        };
        token.Apply(payload);
        return token;
    }

    public void NotifyPixel()
    {
        Notify(nameof(PixelX));
        Notify(nameof(PixelY));
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static long Long(object? value, long fallback) => long.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static DateTime Date(object? value) => value is DateTime dt ? dt : DateTime.MinValue;
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
    public double Opacity { get; set; } = 0.35d;
    public double Thickness { get; set; } = 1d;
}

public sealed class MapFogOverlayUiItem
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
