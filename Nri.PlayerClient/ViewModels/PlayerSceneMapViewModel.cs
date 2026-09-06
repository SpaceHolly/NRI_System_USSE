using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerSceneMapViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;

    private string _campaignId = "dev-campaign-core";
    private string _sessionId = "dev_session_0162";
    private string _activeGroupId = string.Empty;
    private bool _manualMapIdMode;
    private string _mapId = string.Empty;
    private string _mapName = "Карта сцены не выбрана.";
    private string _mapDescription = string.Empty;
    private int _widthMeters;
    private int _heightMeters;
    private int _gridCellSizeMeters = 50;
    private bool _showGrid = true;
    private bool _showCoordinates = true;
    private bool _isLoading;
    private string _statusMessage = "Откройте активную карту сцены.";
    private string _warningMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private double _canvasWidth = 760;
    private double _canvasHeight = 500;
    private string _canvasScaleLabel = "1м = 0.0px";
    private DateTime _lastRefreshAtUtc;
    private PlayerSceneMarkerUiItem? _selectedMarker;
    private PlayerSceneTokenUiItem? _selectedToken;
    private PlayerSceneShapeUiItem? _selectedShape;
    private PlayerSceneAssetInstanceUiItem? _selectedAssetInstance;
    private PlayerMapObjectUiItem0204? _selectedObject;
    private long _projectionRevision;
    private bool _isReconnecting;
    private bool _showMarkersCategory = true;
    private bool _showTokensCategory = true;
    private bool _showAssetsCategory = true;
    private bool _showShapesCategory = true;
    private readonly MapViewportState _viewport = new(2000d, 2000d, 760d, 500d, 50d);
    private string _coordinateIndicator = "X=0 м, Y=0 м";
    private string _gridStepLabel = "Сетка: 50 м";

    private bool _fogEnabled;
    private string _fogMode = FogOfWarModeIds.Disabled;
    private int _fogCellSizeMeters = 25;
    private readonly List<MapFogCellRange> _fogHiddenRanges = new();

    public PlayerSceneMapViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        OpenMapCommand = new RelayCommand(LoadActiveMap);
        RefreshCommand = new RelayCommand(Refresh);
        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);
        ResetViewCommand = new RelayCommand(ResetView);
        FitToMapCommand = new RelayCommand(FitToMap);
        SetOneHundredPercentCommand = new RelayCommand(SetOneHundredPercent);
        ClearErrorCommand = new RelayCommand(() =>
        {
            ErrorMessage = string.Empty;
            WarningMessage = string.Empty;
        });
        ReconnectCommand = new RelayCommand(Reconnect);
    }

    public ObservableCollection<MapGridLineUiItem> GridLines { get; } = new();
    public ObservableCollection<MapFogOverlayUiItem> FogOverlays { get; } = new();
    public ObservableCollection<PlayerSceneMarkerUiItem> Markers { get; } = new();
    public ObservableCollection<PlayerSceneTokenUiItem> Tokens { get; } = new();
    public ObservableCollection<PlayerSceneShapeUiItem> Shapes { get; } = new();
    public ObservableCollection<PlayerSceneTilePatchUiItem> TilePatches { get; } = new();
    public ObservableCollection<PlayerSceneAssetInstanceUiItem> AssetInstances { get; } = new();
    public ObservableCollection<string> CoordinateHints { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();
    public ObservableCollection<PlayerMapObjectUiItem0204> VisibleObjects { get; } = new();
    public ObservableCollection<PlayerMapLabelUiItem0204> Labels { get; } = new();
    public ObservableCollection<PlayerMapLegendUiItem0204> LegendEntries { get; } = new();

    public ICommand OpenMapCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetViewCommand { get; }
    public ICommand FitToMapCommand { get; }
    public ICommand SetOneHundredPercentCommand { get; }
    public ICommand ClearErrorCommand { get; }
    public ICommand ReconnectCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string SessionId { get => _sessionId; set { if (_sessionId != value) { _sessionId = value; Notify(); } } }
    public string ActiveGroupId { get => _activeGroupId; set { if (_activeGroupId != value) { _activeGroupId = value; Notify(); } } }
    public bool ManualMapIdMode { get => _manualMapIdMode; set { if (_manualMapIdMode != value) { _manualMapIdMode = value; Notify(); Notify(nameof(IsManualMapIdMode)); } } }
    public bool IsManualMapIdMode => ManualMapIdMode;
    public string MapId { get => _mapId; set { if (_mapId != value) { _mapId = value; Notify(); } } }
    public string MapName { get => _mapName; private set { if (_mapName != value) { _mapName = value; Notify(); } } }
    public string MapDescription { get => _mapDescription; private set { if (_mapDescription != value) { _mapDescription = value; Notify(); } } }
    public int WidthMeters { get => _widthMeters; private set { if (_widthMeters != value) { _widthMeters = value; Notify(); Notify(nameof(MapMetaText)); } } }
    public int HeightMeters { get => _heightMeters; private set { if (_heightMeters != value) { _heightMeters = value; Notify(); Notify(nameof(MapMetaText)); } } }
    public int GridCellSizeMeters { get => _gridCellSizeMeters; private set { if (_gridCellSizeMeters != value) { _gridCellSizeMeters = value; Notify(); Notify(nameof(MapMetaText)); } } }
    public bool ShowGrid { get => _showGrid; private set { if (_showGrid != value) { _showGrid = value; Notify(); } } }
    public bool ShowCoordinates { get => _showCoordinates; private set { if (_showCoordinates != value) { _showCoordinates = value; Notify(); } } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string WarningMessage { get => _warningMessage; private set { if (_warningMessage != value) { _warningMessage = value; Notify(); Notify(nameof(HasWarning)); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public double CanvasWidth { get => _canvasWidth; private set { if (Math.Abs(_canvasWidth - value) > 0.01) { _canvasWidth = value; Notify(); } } }
    public double CanvasHeight { get => _canvasHeight; private set { if (Math.Abs(_canvasHeight - value) > 0.01) { _canvasHeight = value; Notify(); } } }
    public string CanvasScaleLabel { get => _canvasScaleLabel; private set { if (_canvasScaleLabel != value) { _canvasScaleLabel = value; Notify(); } } }
    public string ZoomIndicator => _viewport.ZoomDisplay;
    public string CoordinateIndicator { get => _coordinateIndicator; private set { if (_coordinateIndicator != value) { _coordinateIndicator = value; Notify(); } } }
    public string GridStepLabel { get => _gridStepLabel; private set { if (_gridStepLabel != value) { _gridStepLabel = value; Notify(); } } }
    public bool CanZoomIn => _viewport.CanZoomIn;
    public bool CanZoomOut => _viewport.CanZoomOut;
    public long ProjectionRevision { get => _projectionRevision; private set { if (_projectionRevision != value) { _projectionRevision = value; Notify(); Notify(nameof(ProjectionStatusText)); } } }
    public bool IsReconnecting { get => _isReconnecting; private set { if (_isReconnecting != value) { _isReconnecting = value; Notify(); Notify(nameof(ConnectionStateText)); } } }
    public string ConnectionStateText => IsReconnecting ? "Восстанавливаем актуальную карту…" : "Соединение активно";
    public string ProjectionStatusText => ProjectionRevision > 0 ? $"Снимок карты: {ProjectionRevision}" : "Снимок ещё не загружен";
    public bool HasVisibleObjects => VisibleObjects.Count > 0;
    public bool HasSelection => SelectedObject != null;
    public bool ShowMarkersCategory { get => _showMarkersCategory; private set { if (_showMarkersCategory != value) { _showMarkersCategory = value; Notify(); } } }
    public bool ShowTokensCategory { get => _showTokensCategory; private set { if (_showTokensCategory != value) { _showTokensCategory = value; Notify(); } } }
    public bool ShowAssetsCategory { get => _showAssetsCategory; private set { if (_showAssetsCategory != value) { _showAssetsCategory = value; Notify(); } } }
    public bool ShowShapesCategory { get => _showShapesCategory; private set { if (_showShapesCategory != value) { _showShapesCategory = value; Notify(); } } }

    public PlayerMapObjectUiItem0204? SelectedObject
    {
        get => _selectedObject;
        private set
        {
            if (_selectedObject == value) return;
            _selectedObject = value;
            Notify();
            Notify(nameof(HasSelection));
            Notify(nameof(SelectedObjectTitle));
            Notify(nameof(SelectedObjectType));
            Notify(nameof(SelectedObjectDescription));
            Notify(nameof(SelectedObjectCoordinates));
            Notify(nameof(SelectedObjectReference));
            RebuildCanvas();
        }
    }

    public string SelectedObjectTitle => SelectedObject?.Name ?? "Объект не выбран";
    public string SelectedObjectType => SelectedObject?.TypeDisplay ?? "Выберите видимый объект на карте.";
    public string SelectedObjectDescription => SelectedObject == null ? "" : FirstNonEmpty(SelectedObject.Description, "Публичное описание отсутствует.");
    public string SelectedObjectCoordinates => SelectedObject == null || !ShowCoordinates ? "" : $"X={SelectedObject.X:0.##} м, Y={SelectedObject.Y:0.##} м";
    public string SelectedObjectReference => SelectedObject == null || string.IsNullOrWhiteSpace(SelectedObject.LinkedEntityDisplayName)
        ? "" : SelectedObject.LinkedEntityDisplayName;

    public string MapMetaText => WidthMeters > 0 && HeightMeters > 0
        ? $"{WidthMeters}×{HeightMeters}м • шаг {GridCellSizeMeters}м"
        : "Размер карты неизвестен";

    public string FogStatusText => _fogEnabled
        ? $"Туман: {FogOverlays.Count} зон"
        : (string.Equals(_fogMode, FogOfWarModeIds.Disabled, StringComparison.OrdinalIgnoreCase)
            ? "Туман: выключен"
            : "Туман: не настроен");

    public DateTime LastRefreshAtUtc
    {
        get => _lastRefreshAtUtc;
        private set { if (_lastRefreshAtUtc != value) { _lastRefreshAtUtc = value; Notify(); Notify(nameof(LastRefreshText)); } }
    }

    public string LastRefreshText => LastRefreshAtUtc == default
        ? "ещё не обновлялось"
        : LastRefreshAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public PlayerSceneMarkerUiItem? SelectedMarker
    {
        get => _selectedMarker;
        set
        {
            if (_selectedMarker == value) return;
            _selectedMarker = value;
            Notify();
            Notify(nameof(SelectedMarkerTitle));
            Notify(nameof(SelectedMarkerTypeText));
            Notify(nameof(SelectedMarkerCoordsText));
            Notify(nameof(SelectedMarkerDescription));
            Notify(nameof(SelectedMarkerBindingText));
        }
    }

    public string SelectedMarkerTitle => SelectedMarker?.Name ?? "Маркер не выбран";
    public string SelectedMarkerTypeText => SelectedMarker == null ? "—" : SelectedMarker.MarkerTypeDisplay;
    public string SelectedMarkerCoordsText => SelectedMarker == null ? "—" : $"X={SelectedMarker.X:0.##}, Y={SelectedMarker.Y:0.##}";
    public string SelectedMarkerDescription => SelectedMarker == null ? "—" : FirstNonEmpty(SelectedMarker.CardDescription, "Описание отсутствует.");
    public string SelectedMarkerBindingText => SelectedMarker == null
        ? "Без привязки"
        : (string.IsNullOrWhiteSpace(SelectedMarker.LinkedEntityDisplayName)
            ? (string.IsNullOrWhiteSpace(SelectedMarker.LinkedEntityType) ? "Без привязки" : SelectedMarker.LinkedEntityType)
            : $"{SelectedMarker.LinkedEntityType}: {SelectedMarker.LinkedEntityDisplayName}");

    public PlayerSceneShapeUiItem? SelectedShape
    {
        get => _selectedShape;
        set
        {
            if (_selectedShape == value) return;
            _selectedShape = value;
            Notify();
            Notify(nameof(SelectedShapeTitle));
            Notify(nameof(SelectedShapeTypeText));
            Notify(nameof(SelectedShapeCoordsText));
            Notify(nameof(SelectedShapeDescription));
            Notify(nameof(SelectedShapeBindingText));
        }
    }

    public string SelectedShapeTitle => SelectedShape?.DisplayName ?? "Объект локации не выбран";
    public string SelectedShapeTypeText => SelectedShape == null ? "—" : SelectedShape.ObjectKindDisplay;
    public string SelectedShapeCoordsText => SelectedShape == null ? "—" : $"X={SelectedShape.X:0.##}, Y={SelectedShape.Y:0.##}";
    public string SelectedShapeDescription => SelectedShape == null ? "—" : FirstNonEmpty(SelectedShape.DescriptionPlayer, "Описание отсутствует.");
    public string SelectedShapeBindingText => SelectedShape == null
        ? "Без привязки"
        : FirstNonEmpty(SelectedShape.LinkedEntityType, "Без привязки");

    public PlayerSceneAssetInstanceUiItem? SelectedAssetInstance
    {
        get => _selectedAssetInstance;
        set
        {
            if (_selectedAssetInstance == value) return;
            _selectedAssetInstance = value;
            Notify();
            Notify(nameof(SelectedAssetTitle));
            Notify(nameof(SelectedAssetTypeText));
            Notify(nameof(SelectedAssetCoordsText));
            Notify(nameof(SelectedAssetDescription));
            Notify(nameof(SelectedAssetBindingText));
        }
    }

    public string SelectedAssetTitle => SelectedAssetInstance?.DisplayName ?? "Объект карты не выбран";
    public string SelectedAssetTypeText => SelectedAssetInstance == null ? "—" : SelectedAssetInstance.AssetKindDisplay;
    public string SelectedAssetCoordsText => SelectedAssetInstance == null ? "—" : $"X={SelectedAssetInstance.X:0.##}, Y={SelectedAssetInstance.Y:0.##}";
    public string SelectedAssetDescription => SelectedAssetInstance == null ? "—" : FirstNonEmpty(SelectedAssetInstance.DescriptionPlayer, "Описание отсутствует.");
    public string SelectedAssetBindingText => SelectedAssetInstance == null
        ? "Без привязки"
        : FirstNonEmpty(SelectedAssetInstance.LinkedEntityType, "Без привязки");

    public PlayerSceneTokenUiItem? SelectedToken
    {
        get => _selectedToken;
        set
        {
            if (_selectedToken == value) return;
            _selectedToken = value;
            Notify();
            Notify(nameof(SelectedTokenTitle));
            Notify(nameof(SelectedTokenTypeText));
            Notify(nameof(SelectedTokenCoordsText));
            Notify(nameof(SelectedTokenDescription));
            Notify(nameof(SelectedTokenBindingText));
        }
    }

    public string SelectedTokenTitle => SelectedToken?.DisplayName ?? "Токен не выбран";
    public string SelectedTokenTypeText => SelectedToken == null ? "—" : SelectedToken.TokenTypeDisplay;
    public string SelectedTokenCoordsText => SelectedToken == null ? "—" : $"X={SelectedToken.X:0.##}, Y={SelectedToken.Y:0.##}";
    public string SelectedTokenDescription => SelectedToken == null ? "—" : FirstNonEmpty(SelectedToken.DescriptionPlayer, "Описание отсутствует.");
    public string SelectedTokenBindingText => SelectedToken == null
        ? "Без привязки"
        : (string.IsNullOrWhiteSpace(SelectedToken.LinkedEntityDisplayName)
            ? (string.IsNullOrWhiteSpace(SelectedToken.LinkedEntityType) ? "Без привязки" : SelectedToken.LinkedEntityType)
            : $"{SelectedToken.LinkedEntityType}: {SelectedToken.LinkedEntityDisplayName}");

    public string TokenSummaryText => Tokens.Count == 0
        ? "Видимых токенов на карте нет."
        : "Видимые токены: " + string.Join(" · ", Tokens.Select(token => token.DisplayName));

    public void Refresh()
    {
        if (ManualMapIdMode)
        {
            if (string.IsNullOrWhiteSpace(MapId))
            {
                ErrorMessage = "Карта сцены не выбрана.";
                StatusMessage = "Введите MapId и нажмите «Открыть карту».";
                return;
            }

            LoadByMapId(MapId);
            return;
        }

        if (string.IsNullOrWhiteSpace(MapId)) LoadActiveMap();
        else SyncCurrentMap();
    }

    private void Reconnect()
    {
        IsReconnecting = true;
        StatusMessage = "Соединение восстановлено. Получаем полный актуальный снимок карты…";
        try { LoadActiveMap(); }
        finally { IsReconnecting = false; }
    }

    private void SyncCurrentMap()
    {
        if (string.IsNullOrWhiteSpace(MapId)) { LoadActiveMap(); return; }
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.MapPlayerSceneSync(new Dictionary<string, object>
            {
                ["mapId"] = MapId,
                ["characterId"] = _activeCharacterIdAccessor() ?? string.Empty,
                ["campaignId"] = CampaignId,
                ["sessionId"] = SessionId ?? string.Empty,
                ["activeGroupId"] = ActiveGroupId ?? string.Empty,
                ["projectionRevision"] = ProjectionRevision,
                ["includeMarkers"] = true
            });
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = MapResponseError(response);
                StatusMessage = "Не удалось обновить безопасный снимок карты.";
                return;
            }
            if (string.Equals(Str(Get(response.Payload, "snapshotKind")), "no_change", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Карта уже актуальна.";
                LastRefreshAtUtc = DateTime.UtcNow;
                return;
            }
            var map = AsMap(Get(response.Payload, "map"));
            ApplyMapPayload(map, response.Payload);
            StatusMessage = "Видимость карты обновлена.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Связь с картой потеряна: {ex.Message}";
            StatusMessage = "Соединение потеряно. Используйте восстановление без закрытия карты.";
        }
        finally { IsLoading = false; }
    }

    private void LoadActiveMap()
    {
        if (string.IsNullOrWhiteSpace(CampaignId))
        {
            ErrorMessage = "Кампания не выбрана.";
            StatusMessage = "Укажите CampaignId или обратитесь к GM.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        Warnings.Clear();
        try
        {
            ClientLogService.Instance.Info("player.map.active.load.start");
            var response = _api.MapPlayerSceneActiveGet(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId ?? string.Empty },
                { "activeGroupId", ActiveGroupId ?? string.Empty },
                { "characterId", _activeCharacterIdAccessor() ?? string.Empty },
                { "includeMarkers", true }
            });

            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = MapResponseError(response);
                StatusMessage = "Карта сцены пока недоступна.";
                ClientLogService.Instance.Warn($"player.map.active.load.error message={response.Message}");
                return;
            }

            if (!Bool(Get(response.Payload, "hasActiveMap"), false))
            {
                Markers.Clear();
                Tokens.Clear();
                Shapes.Clear();
                TilePatches.Clear();
                AssetInstances.Clear();
                VisibleObjects.Clear();
                Labels.Clear();
                LegendEntries.Clear();
                SelectedObject = null;
                ProjectionRevision = 0;
                FogOverlays.Clear();
                MapName = "Карта сцены не назначена";
                MapDescription = string.Empty;
                WidthMeters = 0;
                HeightMeters = 0;
                GridCellSizeMeters = 25;
                ShowGrid = true;
                ShowCoordinates = true;
                StatusMessage = "GM ещё не назначил активную карту сцены.";
                LastRefreshAtUtc = DateTime.UtcNow;
                ClientLogService.Instance.Info("player.map.active.none");
                return;
            }

            var map = AsMap(Get(response.Payload, "map"));
            MapId = FirstNonEmpty(Str(Get(response.Payload, "mapId")), Str(Get(map, "mapId")), MapId);
            ApplyMapPayload(map, response.Payload);
            StatusMessage = Markers.Count == 0
                ? "На карте нет видимых маркеров."
                : $"Активная карта загружена. Видимых маркеров: {Markers.Count}.";
            ClientLogService.Instance.Info("player.map.active.load.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка загрузки карты сцены: {ex.Message}";
            StatusMessage = "Карта сцены пока недоступна.";
            ClientLogService.Instance.Warn($"player.map.active.load.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadByMapId(string mapId)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        Warnings.Clear();
        try
        {
            ClientLogService.Instance.Info("player.map.scene.open.start");
            var response = _api.MapPlayerSceneGet(new Dictionary<string, object>
            {
                { "mapId", mapId },
                { "characterId", _activeCharacterIdAccessor() ?? string.Empty },
                { "activeGroupId", ActiveGroupId ?? string.Empty },
                { "includeMarkers", true }
            });

            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = MapResponseError(response);
                StatusMessage = "Карта сцены пока недоступна.";
                ClientLogService.Instance.Warn($"player.map.scene.open.error message={response.Message}");
                return;
            }

            var map = AsMap(Get(response.Payload, "map"));
            MapId = mapId;
            ApplyMapPayload(map, response.Payload);
            StatusMessage = Markers.Count == 0
                ? "На карте нет видимых маркеров."
                : $"Карта загружена. Видимых маркеров: {Markers.Count}.";
            ClientLogService.Instance.Info("player.map.scene.open.done");
            ClientLogService.Instance.Info("player.map.scene.refresh");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка загрузки карты сцены: {ex.Message}";
            StatusMessage = "Карта сцены пока недоступна.";
            ClientLogService.Instance.Warn($"player.map.scene.open.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyMapPayload(Dictionary<string, object> map, IDictionary<string, object> rootPayload)
    {
        var nextRevision = Long(Get(rootPayload, "projectionRevision"), Long(Get(map, "projectionRevision"), 0L));
        var revisionCheck = PlayerMapSnapshotReducer0204.Reduce(ProjectionRevision, nextRevision, SelectedObject?.ObjectId, Array.Empty<string>());
        if (revisionCheck.StaleRejected)
        {
            StatusMessage = "Устаревшее обновление карты отклонено.";
            return;
        }
        var selectedObjectId = SelectedObject?.ObjectId ?? string.Empty;
        MapName = FirstNonEmpty(Str(Get(map, "name")), "Карта сцены");
        MapDescription = Str(Get(map, "description"));
        WidthMeters = Int(Get(map, "widthMeters"), 0);
        HeightMeters = Int(Get(map, "heightMeters"), 0);
        GridCellSizeMeters = Int(Get(map, "gridCellSizeMeters"), 25);
        ShowGrid = !map.ContainsKey("showGrid") || Bool(Get(map, "showGrid"), true);
        ShowCoordinates = !map.ContainsKey("showCoordinates") || Bool(Get(map, "showCoordinates"), true);
        ApplyFogPayload(AsMap(Get(map, "fogOfWarVisibleState")), Bool(Get(map, "fogEnabled"), false));

        Markers.Clear();
        foreach (var markerPayload in Dictionaries(Get(map, "markers")))
            Markers.Add(PlayerSceneMarkerUiItem.From(markerPayload));
        SelectedMarker = null;

        Tokens.Clear();
        foreach (var tokenPayload in Dictionaries(Get(map, "tokens")))
            Tokens.Add(PlayerSceneTokenUiItem.From(tokenPayload));
        SelectedToken = null;
        Notify(nameof(TokenSummaryText));

        TilePatches.Clear();
        var tilePatchPayloads = Dictionaries(Get(map, "tilePatches")).ToList();
        if (tilePatchPayloads.Count == 0)
            tilePatchPayloads = Dictionaries(Get(rootPayload, "tilePatches")).ToList();
        foreach (var tilePatchPayload in tilePatchPayloads)
            TilePatches.Add(PlayerSceneTilePatchUiItem.From(tilePatchPayload));

        AssetInstances.Clear();
        var assetPayloads = Dictionaries(Get(map, "assetInstances")).ToList();
        if (assetPayloads.Count == 0)
            assetPayloads = Dictionaries(Get(rootPayload, "assetInstances")).ToList();
        foreach (var assetPayload in assetPayloads)
            AssetInstances.Add(PlayerSceneAssetInstanceUiItem.From(assetPayload));
        SelectedAssetInstance = null;

        Shapes.Clear();
        var shapePayloads = Dictionaries(Get(map, "shapes")).ToList();
        if (shapePayloads.Count == 0)
            shapePayloads = Dictionaries(Get(rootPayload, "shapes")).ToList();
        foreach (var shapePayload in shapePayloads)
            Shapes.Add(PlayerSceneShapeUiItem.From(shapePayload));
        SelectedShape = null;

        VisibleObjects.Clear();
        foreach (var objectPayload in Dictionaries(Get(map, "objects")))
            VisibleObjects.Add(PlayerMapObjectUiItem0204.From(objectPayload));
        if (VisibleObjects.Count == 0)
        {
            foreach (var marker in Markers) VisibleObjects.Add(PlayerMapObjectUiItem0204.From(marker));
            foreach (var token in Tokens) VisibleObjects.Add(PlayerMapObjectUiItem0204.From(token));
            foreach (var shape in Shapes) VisibleObjects.Add(PlayerMapObjectUiItem0204.From(shape));
            foreach (var asset in AssetInstances) VisibleObjects.Add(PlayerMapObjectUiItem0204.From(asset));
        }

        BuildLegend(Dictionaries(Get(map, "legend")));
        var reduction = PlayerMapSnapshotReducer0204.Reduce(
            ProjectionRevision,
            nextRevision,
            selectedObjectId,
            VisibleObjects.Select(x => x.ObjectId));
        ProjectionRevision = reduction.Revision;
        SelectedObject = reduction.SelectedObjectId.Length == 0
            ? null
            : VisibleObjects.FirstOrDefault(x => string.Equals(x.ObjectId, reduction.SelectedObjectId, StringComparison.OrdinalIgnoreCase));
        Notify(nameof(HasVisibleObjects));

        foreach (var warning in ToStrings(Get(rootPayload, "warnings")))
            Warnings.Add(warning);
        WarningMessage = Warnings.Count > 0 ? string.Join(" | ", Warnings) : string.Empty;

        RebuildCanvas();
        LastRefreshAtUtc = DateTime.UtcNow;
    }

    private void RebuildCanvas()
    {
        GridLines.Clear();
        FogOverlays.Clear();
        CoordinateHints.Clear();
        if (WidthMeters <= 0 || HeightMeters <= 0)
        {
            CanvasWidth = _viewport.ViewportWidthPixels;
            CanvasHeight = _viewport.ViewportHeightPixels;
            return;
        }
        if (Math.Abs(_viewport.MapWidthMeters - WidthMeters) > 0.01 || Math.Abs(_viewport.MapHeightMeters - HeightMeters) > 0.01 || Math.Abs(_viewport.GridSizeMeters - GridCellSizeMeters) > 0.01)
            _viewport.SetMap(WidthMeters, HeightMeters, GridCellSizeMeters, fit: true);
        CanvasWidth = _viewport.ViewportWidthPixels;
        CanvasHeight = _viewport.ViewportHeightPixels;
        CanvasScaleLabel = $"1м = {_viewport.Zoom:0.###}px";
        Notify(nameof(ZoomIndicator));
        Notify(nameof(CanZoomIn));
        Notify(nameof(CanZoomOut));
        var lod = MapGridLodCalculator.Calculate(GridCellSizeMeters, _viewport.Zoom);
        GridStepLabel = $"Сетка: {lod.MinorStepMeters:0.##} м · основная {lod.MajorStepMeters:0.##} м";
        var visible = _viewport.VisibleWorldBounds();

        if (ShowGrid && WidthMeters > 0 && HeightMeters > 0)
        {
            var step = lod.MinorStepMeters;
            var startX = Math.Floor(visible.X / step) * step;
            var endX = Math.Min(WidthMeters, visible.Right + step);
            for (var x = startX; x <= endX; x += step)
            {
                var px = _viewport.WorldToScreen(new MapPoint(x, 0)).X;
                var major = Math.Abs(x / lod.MajorStepMeters - Math.Round(x / lod.MajorStepMeters)) < 0.001;
                GridLines.Add(new MapGridLineUiItem { X1 = px, Y1 = 0, X2 = px, Y2 = CanvasHeight, Opacity = major ? 0.7 : 0.32, Thickness = major ? 1.4 : 0.8 });
            }

            var startY = Math.Floor(visible.Y / step) * step;
            var endY = Math.Min(HeightMeters, visible.Bottom + step);
            for (var y = startY; y <= endY; y += step)
            {
                var py = _viewport.WorldToScreen(new MapPoint(0, y)).Y;
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

        foreach (var token in Tokens)
        {
            var point = _viewport.WorldToScreen(new MapPoint(token.X, token.Y));
            token.PixelX = point.X;
            token.PixelY = point.Y;
        }

        foreach (var patch in TilePatches)
            patch.ApplyScale(_viewport.Zoom, _viewport.OffsetX, _viewport.OffsetY);

        foreach (var asset in AssetInstances)
            asset.ApplyScale(_viewport.Zoom, _viewport.OffsetX, _viewport.OffsetY);

        foreach (var shape in Shapes)
            shape.ApplyScale(_viewport.Zoom, _viewport.OffsetX, _viewport.OffsetY);

        BuildFogOverlay(_viewport.Zoom, _viewport.OffsetX, _viewport.OffsetY);
        BuildLabels();

        if (ShowCoordinates && WidthMeters > 0 && HeightMeters > 0)
        {
            CoordinateHints.Add("Начало координат: X=0, Y=0 (левый верхний угол)");
            CoordinateHints.Add($"Границы: X 0..{WidthMeters}, Y 0..{HeightMeters}");
        }
    }

    private void ApplyFogPayload(Dictionary<string, object> fogPayload, bool fogEnabled)
    {
        _fogEnabled = fogEnabled;
        _fogMode = FirstNonEmpty(Str(Get(fogPayload, "mode")), FogOfWarModeIds.Disabled);
        _fogCellSizeMeters = Int(Get(fogPayload, "cellSizeMeters"), Math.Max(1, GridCellSizeMeters));
        _fogHiddenRanges.Clear();
        foreach (var range in ReadFogRanges(Get(fogPayload, "hiddenCells")))
            _fogHiddenRanges.Add(range);
        Notify(nameof(FogStatusText));
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
    }

    public void SelectObjectAt(double pixelX, double pixelY)
    {
        var candidate = VisibleObjects
            .Where(IsCategoryEnabled)
            .Select(x => new { Item = x, Point = _viewport.WorldToScreen(new MapPoint(x.X, x.Y)) })
            .Select(x => new { x.Item, Distance = Math.Sqrt(Math.Pow(x.Point.X - pixelX, 2) + Math.Pow(x.Point.Y - pixelY, 2)) })
            .Where(x => x.Distance <= 24d)
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Item.LabelPriority)
            .FirstOrDefault();
        SelectedObject = candidate?.Item;
    }

    private void BuildLegend(IEnumerable<Dictionary<string, object>> payloads)
    {
        var preferences = LegendEntries.ToDictionary(x => x.Category, x => x.IsEnabled, StringComparer.OrdinalIgnoreCase);
        LegendEntries.Clear();
        var source = payloads.Select(x => new
        {
            Category = FirstNonEmpty(Str(Get(x, "category")), "other"),
            Name = FirstNonEmpty(Str(Get(x, "displayName")), "Прочее"),
            Count = Int(Get(x, "visibleCount"), 0)
        }).Where(x => x.Count > 0).ToList();
        if (source.Count == 0)
        {
            source = VisibleObjects.GroupBy(x => x.Kind, StringComparer.OrdinalIgnoreCase)
                .Select(x => new { Category = x.Key, Name = PlayerMapObjectUiItem0204.CategoryDisplay(x.Key), Count = x.Count() }).ToList();
        }
        foreach (var entry in source)
        {
            var item = new PlayerMapLegendUiItem0204(entry.Category, entry.Name, entry.Count,
                preferences.TryGetValue(entry.Category, out var enabled) ? enabled : true,
                ToggleLegendCategory);
            LegendEntries.Add(item);
        }
        ApplyLegendPreferences();
    }

    private void ToggleLegendCategory(PlayerMapLegendUiItem0204 item)
    {
        ApplyLegendPreferences();
        if (SelectedObject != null && !IsCategoryEnabled(SelectedObject)) SelectedObject = null;
        RebuildCanvas();
    }

    private void ApplyLegendPreferences()
    {
        bool Enabled(string kind) => LegendEntries.FirstOrDefault(x => string.Equals(x.Category, kind, StringComparison.OrdinalIgnoreCase))?.IsEnabled ?? true;
        ShowMarkersCategory = Enabled("marker");
        ShowTokensCategory = Enabled("token");
        ShowAssetsCategory = Enabled("asset");
        ShowShapesCategory = Enabled("shape");
    }

    private bool IsCategoryEnabled(PlayerMapObjectUiItem0204 item) => item.Kind.ToLowerInvariant() switch
    {
        "marker" => ShowMarkersCategory,
        "token" => ShowTokensCategory,
        "asset" => ShowAssetsCategory,
        "shape" => ShowShapesCategory,
        _ => true
    };

    private void BuildLabels()
    {
        Labels.Clear();
        var candidates = VisibleObjects.Where(IsCategoryEnabled).Select(item =>
        {
            var selected = SelectedObject != null && string.Equals(item.ObjectId, SelectedObject.ObjectId, StringComparison.OrdinalIgnoreCase);
            var anchor = _viewport.WorldToScreen(new MapPoint(item.X, item.Y));
            return new PlayerMapLabelCandidate0204
            {
                ObjectId = item.ObjectId,
                Text = item.Name,
                Kind = item.Kind,
                Priority = item.LabelPriority,
                AnchorX = anchor.X,
                AnchorY = anchor.Y,
                IsSelected = selected
            };
        });
        foreach (var placement in PlayerMapLabelLayout0204.Layout(candidates, CanvasWidth, CanvasHeight, _viewport.Zoom))
        {
            Labels.Add(new PlayerMapLabelUiItem0204
            {
                ObjectId = placement.ObjectId,
                Text = placement.Text,
                Kind = placement.Kind,
                PixelX = placement.X,
                PixelY = placement.Y,
                Width = placement.Width,
                IsSelected = placement.IsSelected
            });
        }
    }

    private void BuildFogOverlay(double scale, double offsetX, double offsetY)
    {
        if (!_fogEnabled || _fogHiddenRanges.Count == 0) return;
        var cell = Math.Max(1, _fogCellSizeMeters);
        foreach (var range in _fogHiddenRanges)
        {
            var fromX = MapCanvasProjectionHelper.CellToMeters(range.FromX, cell);
            var fromY = MapCanvasProjectionHelper.CellToMeters(range.FromY, cell);
            var widthMeters = (range.ToX - range.FromX + 1) * cell;
            var heightMeters = (range.ToY - range.FromY + 1) * cell;
            var clampedWidth = Math.Min(widthMeters, Math.Max(0, WidthMeters - fromX));
            var clampedHeight = Math.Min(heightMeters, Math.Max(0, HeightMeters - fromY));
            if (clampedWidth <= 0 || clampedHeight <= 0) continue;

            FogOverlays.Add(new MapFogOverlayUiItem
            {
                X = MapCanvasProjectionHelper.ToPixel(fromX, scale) + offsetX,
                Y = MapCanvasProjectionHelper.ToPixel(fromY, scale) + offsetY,
                Width = Math.Max(1, MapCanvasProjectionHelper.ToPixel(clampedWidth, scale)),
                Height = Math.Max(1, MapCanvasProjectionHelper.ToPixel(clampedHeight, scale))
            });
        }

        Notify(nameof(FogStatusText));
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

    private static bool IsValidFogRange(MapFogCellRange range) => range != null && range.FromX <= range.ToX && range.FromY <= range.ToY;

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

    private static string MapResponseError(ResponseEnvelope response)
    {
        var text = (response.Message ?? string.Empty).Trim();
        if (response.Status == ResponseStatus.Forbidden && text.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Карта сцены пока недоступна.";
        if (text.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Карта сцены пока недоступна.";
        if (response.Status == ResponseStatus.NotFound)
            return "Карта сцены не найдена.";
        if (response.Status == ResponseStatus.Forbidden)
            return "Недостаточно прав для просмотра карты сцены.";
        return string.IsNullOrWhiteSpace(text) ? "Не удалось загрузить карту сцены." : text;
    }

    private static object? Get(IDictionary<string, object>? map, string key)
    {
        if (map == null || string.IsNullOrWhiteSpace(key)) return null;
        return map.TryGetValue(key, out var value) ? value : null;
    }

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

    private static IEnumerable<string> ToStrings(object? value)
    {
        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                var text = Convert.ToString(item);
                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
            }
        }
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static long Long(object? value, long fallback) => long.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
}

public static class PlayerLocationMapVisualBrushes
{
    public static Brush MaterialBrush(string key)
    {
        var normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
        var baseColor = normalized switch
        {
            "grass" or "terrain" => Color.FromRgb(66, 107, 44),
            "dirt" or "packed_dirt" or "road" => Color.FromRgb(137, 93, 49),
            "stone" or "stone_floor" or "dark_stone" => Color.FromRgb(95, 109, 126),
            "sand" => Color.FromRgb(178, 146, 88),
            "mud" => Color.FromRgb(83, 67, 43),
            "water" => Color.FromRgb(35, 112, 151),
            "wood_floor" => Color.FromRgb(127, 82, 45),
            "warm_wood" or "tavern" => Color.FromRgb(142, 79, 43),
            "cobblestone" => Color.FromRgb(113, 122, 132),
            "roof_tile" => Color.FromRgb(136, 53, 44),
            "canvas_red" or "stall" => Color.FromRgb(157, 73, 47),
            "iron_wood" or "entrance" => Color.FromRgb(92, 65, 48),
            "hazard" => Color.FromRgb(162, 62, 42),
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
            _ => Color.FromRgb(226, 232, 240)
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
        if (normalized.Contains("shop")) return "выв.";
        if (normalized.Contains("tavern")) return "тракт.";
        if (normalized.Contains("crate")) return "ящ.";
        if (normalized.Contains("barrel")) return "боч.";
        if (normalized.Contains("cart")) return "тел.";
        if (normalized.Contains("lantern")) return "фон.";
        if (normalized.Contains("well")) return "кол.";
        if (normalized.Contains("gate")) return "вор.";
        if (normalized.Contains("campfire")) return "огн.";
        if (normalized.Contains("tree")) return "дер.";
        if (normalized.Contains("cover")) return "укр.";
        if (normalized.Contains("hazard")) return "опас.";
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

public sealed class PlayerSceneTilePatchUiItem : ViewModelBase
{
    private double _pixelX;
    private double _pixelY;
    private double _pixelWidth;
    private double _pixelHeight;

    public string TilePatchId { get; set; } = string.Empty;
    public string MaterialKey { get; set; } = "grass";
    public string TextureKey { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Opacity { get; set; } = 1d;
    public int SortOrder { get; set; }

    public double PixelX { get => _pixelX; private set { if (Math.Abs(_pixelX - value) > 0.0001) { _pixelX = value; Notify(); } } }
    public double PixelY { get => _pixelY; private set { if (Math.Abs(_pixelY - value) > 0.0001) { _pixelY = value; Notify(); } } }
    public double PixelWidth { get => _pixelWidth; private set { if (Math.Abs(_pixelWidth - value) > 0.0001) { _pixelWidth = value; Notify(); } } }
    public double PixelHeight { get => _pixelHeight; private set { if (Math.Abs(_pixelHeight - value) > 0.0001) { _pixelHeight = value; Notify(); } } }
    public Brush FillBrush => PlayerLocationMapVisualBrushes.MaterialBrush(FirstNonEmpty(MaterialKey, TextureKey));
    public Brush StrokeBrush => PlayerLocationMapVisualBrushes.StrokeBrush(MaterialKey);
    public int VisualZIndex => Math.Max(-1000, SortOrder);

    public void ApplyScale(double scale, double offsetX = 0d, double offsetY = 0d)
    {
        PixelX = MapCanvasProjectionHelper.ToPixel(X, scale) + offsetX;
        PixelY = MapCanvasProjectionHelper.ToPixel(Y, scale) + offsetY;
        PixelWidth = Math.Max(1, MapCanvasProjectionHelper.ToPixel(Math.Max(1, Width), scale));
        PixelHeight = Math.Max(1, MapCanvasProjectionHelper.ToPixel(Math.Max(1, Height), scale));
    }

    public static PlayerSceneTilePatchUiItem From(IDictionary<string, object> payload)
    {
        return new PlayerSceneTilePatchUiItem
        {
            TilePatchId = Str(Get(payload, "tilePatchId")),
            MaterialKey = FirstNonEmpty(Str(Get(payload, "materialKey")), "grass"),
            TextureKey = Str(Get(payload, "textureKey")),
            X = Double(Get(payload, "x"), 0),
            Y = Double(Get(payload, "y"), 0),
            Width = Double(Get(payload, "width"), 1),
            Height = Double(Get(payload, "height"), 1),
            Opacity = Double(Get(payload, "opacity"), 1),
            SortOrder = Int(Get(payload, "sortOrder"), -100)
        };
    }

    private static object? Get(IDictionary<string, object>? map, string key) => map != null && map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class PlayerSceneAssetInstanceUiItem : ViewModelBase
{
    private double _pixelX;
    private double _pixelY;
    private double _pixelWidth;
    private double _pixelHeight;

    public string AssetInstanceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Объект карты";
    public string AssetKind { get; set; } = "prop";
    public string ObjectKind { get; set; } = "Decoration";
    public string AssetKey { get; set; } = string.Empty;
    public string MaterialKey { get; set; } = string.Empty;
    public string DescriptionPlayer { get; set; } = string.Empty;
    public string LinkedEntityType { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int ZIndex { get; set; }

    public double PixelX { get => _pixelX; private set { if (Math.Abs(_pixelX - value) > 0.0001) { _pixelX = value; Notify(); } } }
    public double PixelY { get => _pixelY; private set { if (Math.Abs(_pixelY - value) > 0.0001) { _pixelY = value; Notify(); } } }
    public double PixelWidth { get => _pixelWidth; private set { if (Math.Abs(_pixelWidth - value) > 0.0001) { _pixelWidth = value; Notify(); } } }
    public double PixelHeight { get => _pixelHeight; private set { if (Math.Abs(_pixelHeight - value) > 0.0001) { _pixelHeight = value; Notify(); } } }
    public int VisualZIndex => 200 + ZIndex;
    public Brush FillBrush => PlayerLocationMapVisualBrushes.MaterialBrush(FirstNonEmpty(MaterialKey, AssetKind, ObjectKind));
    public Brush StrokeBrush => PlayerLocationMapVisualBrushes.StrokeBrush(FirstNonEmpty(ObjectKind, AssetKind));
    public Brush GlyphBrush => PlayerLocationMapVisualBrushes.AssetBrush(FirstNonEmpty(AssetKey, AssetKind, ObjectKind));
    public string AssetGlyph => PlayerLocationMapVisualBrushes.AssetGlyph(FirstNonEmpty(AssetKey, AssetKind, ObjectKind));
    public string AssetKindDisplay => string.IsNullOrWhiteSpace(AssetKind) ? "Объект" : AssetKind.Replace('_', ' ');

    public void ApplyScale(double scale, double offsetX = 0d, double offsetY = 0d)
    {
        PixelX = MapCanvasProjectionHelper.ToPixel(X, scale) + offsetX;
        PixelY = MapCanvasProjectionHelper.ToPixel(Y, scale) + offsetY;
        PixelWidth = Math.Max(8, MapCanvasProjectionHelper.ToPixel(Math.Max(1, Width), scale));
        PixelHeight = Math.Max(8, MapCanvasProjectionHelper.ToPixel(Math.Max(1, Height), scale));
    }

    public static PlayerSceneAssetInstanceUiItem From(IDictionary<string, object> payload)
    {
        return new PlayerSceneAssetInstanceUiItem
        {
            AssetInstanceId = Str(Get(payload, "assetInstanceId")),
            DisplayName = FirstNonEmpty(Str(Get(payload, "displayName")), Str(Get(payload, "name")), "Объект карты"),
            AssetKind = FirstNonEmpty(Str(Get(payload, "assetKind")), "prop"),
            ObjectKind = FirstNonEmpty(Str(Get(payload, "objectKind")), "Decoration"),
            AssetKey = Str(Get(payload, "assetKey")),
            MaterialKey = Str(Get(payload, "materialKey")),
            DescriptionPlayer = FirstNonEmpty(Str(Get(payload, "descriptionPlayer")), Str(Get(payload, "cardDescription"))),
            LinkedEntityType = Str(Get(payload, "linkedEntityType")),
            X = Double(Get(payload, "x"), 0),
            Y = Double(Get(payload, "y"), 0),
            Width = Double(Get(payload, "width"), 1),
            Height = Double(Get(payload, "height"), 1),
            ZIndex = Int(Get(payload, "zIndex"), 0)
        };
    }

    private static object? Get(IDictionary<string, object>? map, string key) => map != null && map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class PlayerSceneShapeUiItem : ViewModelBase
{
    private string _displayName = string.Empty;
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

    public string ShapeId { get; set; } = string.Empty;
    public string LayerId { get; set; } = string.Empty;
    public string ShapeKind { get; set; } = "Rectangle";
    public string DescriptionPlayer { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Points { get; set; } = string.Empty;
    public string FillKey { get; set; } = string.Empty;
    public string StrokeKey { get; set; } = string.Empty;
    public double Opacity { get; set; } = 0.65;
    public string MaterialKey { get; set; } = string.Empty;
    public string TextureKey { get; set; } = string.Empty;
    public string AssetKey { get; set; } = string.Empty;
    public string VisualStyleKey { get; set; } = string.Empty;
    public string RenderMode { get; set; } = "TexturedShape";
    public double VisualOpacity { get; set; } = 0.88;
    public double StrokeThickness { get; set; } = 1.4;
    public int ZIndex { get; set; }
    public string LinkedEntityType { get; set; } = string.Empty;

    public string DisplayName { get => _displayName; set { if (_displayName != value) { _displayName = value; Notify(); Notify(nameof(Label)); } } }
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
    public Visibility AreaVisibility => IsPathLike ? Visibility.Collapsed : Visibility.Visible;
    public Visibility PathVisibility => IsPathLike ? Visibility.Visible : Visibility.Collapsed;
    public bool IsPathLike => RenderMode is "RoadPath" or "LineWall" || ShapeKind is "Polyline" or "Line";
    public Brush VisualFillBrush => PlayerLocationMapVisualBrushes.MaterialBrush(FirstNonEmpty(MaterialKey, TextureKey, FillKey, ObjectKind));
    public Brush VisualStrokeBrush => PlayerLocationMapVisualBrushes.StrokeBrush(FirstNonEmpty(VisualStyleKey, ObjectKind, StrokeKey));
    public Brush AssetGlyphBrush => PlayerLocationMapVisualBrushes.AssetBrush(FirstNonEmpty(AssetKey, ObjectKind));
    public double EffectiveVisualOpacity => Math.Max(0.15d, Math.Min(1d, VisualOpacity > 0 ? VisualOpacity : Opacity));
    public double EffectiveStrokeThickness => Math.Max(1d, StrokeThickness);
    public int VisualZIndex => ZIndex;
    public string AssetGlyph => PlayerLocationMapVisualBrushes.AssetGlyph(FirstNonEmpty(AssetKey, ObjectKind));
    public string VisualLabel => string.IsNullOrWhiteSpace(Text) ? DisplayName : Text;
    public Geometry PathGeometry => PlayerLocationMapVisualBrushes.BuildPathGeometry(Points, X, Y, Width, Height, PixelWidth, PixelHeight);

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
        _ => ObjectKind
    };

    public string Label => $"{DisplayName} [{ObjectKindDisplay}] X={X:0.##}, Y={Y:0.##}";

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

    public static PlayerSceneShapeUiItem From(IDictionary<string, object> payload)
    {
        return new PlayerSceneShapeUiItem
        {
            ShapeId = Str(Get(payload, "shapeId")),
            LayerId = Str(Get(payload, "layerId")),
            DisplayName = FirstNonEmpty(Str(Get(payload, "displayName")), Str(Get(payload, "name")), "Объект локации"),
            DescriptionPlayer = Str(Get(payload, "descriptionPlayer")),
            ShapeKind = FirstNonEmpty(Str(Get(payload, "shapeKind")), "Rectangle"),
            ObjectKind = FirstNonEmpty(Str(Get(payload, "objectKind")), "Decoration"),
            X = Double(Get(payload, "x"), 0),
            Y = Double(Get(payload, "y"), 0),
            Width = Double(Get(payload, "width"), 1),
            Height = Double(Get(payload, "height"), 1),
            Radius = Double(Get(payload, "radius"), 0),
            Text = Str(Get(payload, "text")),
            Points = Str(Get(payload, "points")),
            FillKey = Str(Get(payload, "fillKey")),
            StrokeKey = Str(Get(payload, "strokeKey")),
            Opacity = Double(Get(payload, "opacity"), 0.65),
            MaterialKey = FirstNonEmpty(Str(Get(payload, "materialKey")), Str(Get(payload, "fillKey"))),
            TextureKey = Str(Get(payload, "textureKey")),
            AssetKey = Str(Get(payload, "assetKey")),
            VisualStyleKey = Str(Get(payload, "visualStyleKey")),
            RenderMode = FirstNonEmpty(Str(Get(payload, "renderMode")), "TexturedShape"),
            VisualOpacity = Double(Get(payload, "visualOpacity"), 0.88),
            StrokeThickness = Double(Get(payload, "strokeThickness"), 1.4),
            ZIndex = Int(Get(payload, "zIndex"), 0),
            LinkedEntityType = Str(Get(payload, "linkedEntityType"))
        };
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class PlayerSceneMarkerUiItem : ViewModelBase
{
    private string _name = string.Empty;
    private string _markerType = "custom";
    private double _x;
    private double _y;
    private double _pixelX;
    private double _pixelY;

    public string MarkerId { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityDisplayName { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;

    public string Name { get => _name; set { if (_name != value) { _name = value; Notify(); Notify(nameof(Label)); } } }
    public string MarkerType { get => _markerType; set { if (_markerType != value) { _markerType = value; Notify(); Notify(nameof(MarkerTypeDisplay)); Notify(nameof(Label)); } } }
    public double X { get => _x; set { if (Math.Abs(_x - value) > 0.0001) { _x = value; Notify(); Notify(nameof(Label)); } } }
    public double Y { get => _y; set { if (Math.Abs(_y - value) > 0.0001) { _y = value; Notify(); Notify(nameof(Label)); } } }
    public double PixelX { get => _pixelX; set { if (Math.Abs(_pixelX - value) > 0.0001) { _pixelX = value; Notify(); } } }
    public double PixelY { get => _pixelY; set { if (Math.Abs(_pixelY - value) > 0.0001) { _pixelY = value; Notify(); } } }

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

    public static PlayerSceneMarkerUiItem From(IDictionary<string, object> payload)
    {
        return new PlayerSceneMarkerUiItem
        {
            MarkerId = Str(Get(payload, "markerId")),
            Name = FirstNonEmpty(Str(Get(payload, "name")), "Маркер"),
            MarkerType = FirstNonEmpty(Str(Get(payload, "markerType")), "custom"),
            X = Double(Get(payload, "x"), 0),
            Y = Double(Get(payload, "y"), 0),
            IconKey = Str(Get(payload, "iconKey")),
            ColorKey = Str(Get(payload, "colorKey")),
            CardTitle = Str(Get(payload, "cardTitle")),
            CardDescription = Str(Get(payload, "cardDescription")),
            LinkedEntityType = Str(Get(payload, "linkedEntityType")),
            LinkedEntityDisplayName = Str(Get(payload, "linkedEntityDisplayName")),
            IsVisible = Bool(Get(payload, "isVisible"), true)
        };
    }

    public override string ToString() => Label;

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class PlayerSceneTokenUiItem : ViewModelBase
{
    private string _displayName = string.Empty;
    private string _tokenType = "Object";
    private double _x;
    private double _y;
    private double _pixelX;
    private double _pixelY;

    public string TokenId { get; set; } = string.Empty;
    public string DescriptionPlayer { get; set; } = string.Empty;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityDisplayName { get; set; } = string.Empty;

    public string DisplayName { get => _displayName; set { if (_displayName != value) { _displayName = value; Notify(); Notify(nameof(Label)); } } }
    public string TokenType { get => _tokenType; set { if (_tokenType != value) { _tokenType = value; Notify(); Notify(nameof(TokenTypeDisplay)); Notify(nameof(Label)); } } }
    public double X { get => _x; set { if (Math.Abs(_x - value) > 0.0001) { _x = value; Notify(); Notify(nameof(Label)); } } }
    public double Y { get => _y; set { if (Math.Abs(_y - value) > 0.0001) { _y = value; Notify(); Notify(nameof(Label)); } } }
    public double PixelX { get => _pixelX; set { if (Math.Abs(_pixelX - value) > 0.0001) { _pixelX = value; Notify(); } } }
    public double PixelY { get => _pixelY; set { if (Math.Abs(_pixelY - value) > 0.0001) { _pixelY = value; Notify(); } } }

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

    public string Label => $"{DisplayName} [{TokenTypeDisplay}] X={X:0.##}, Y={Y:0.##}";

    public static PlayerSceneTokenUiItem From(IDictionary<string, object> payload)
    {
        return new PlayerSceneTokenUiItem
        {
            TokenId = Str(Get(payload, "tokenId")),
            DisplayName = FirstNonEmpty(Str(Get(payload, "displayName")), Str(Get(payload, "name")), "Токен"),
            TokenType = FirstNonEmpty(Str(Get(payload, "tokenType")), "Object"),
            X = Double(Get(payload, "x"), 0),
            Y = Double(Get(payload, "y"), 0),
            DescriptionPlayer = FirstNonEmpty(Str(Get(payload, "descriptionPlayer")), Str(Get(payload, "cardDescription"))),
            LinkedEntityType = Str(Get(payload, "linkedEntityType")),
            LinkedEntityDisplayName = FirstNonEmpty(Str(Get(payload, "linkedEntityDisplayName")), Str(Get(payload, "publicLabel")))
        };
    }

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class PlayerMapObjectUiItem0204
{
    public string ObjectId { get; set; } = string.Empty;
    public string Kind { get; set; } = "object";
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LinkedEntityDisplayName { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public int LabelPriority { get; set; }
    public string CategoryDisplayName => CategoryDisplay(Kind);
    public string TypeDisplay => string.IsNullOrWhiteSpace(Type) ? CategoryDisplayName : Type.Replace('_', ' ');

    public static PlayerMapObjectUiItem0204 From(IDictionary<string, object> payload) => new()
    {
        ObjectId = FirstNonEmpty(Str(Get(payload, "objectId")), Str(Get(payload, "id"))),
        Kind = FirstNonEmpty(Str(Get(payload, "kind")), "object"),
        Name = Str(Get(payload, "name")),
        Type = FirstNonEmpty(Str(Get(payload, "type")), Str(Get(payload, "markerType")), Str(Get(payload, "tokenType")), Str(Get(payload, "assetKind")), Str(Get(payload, "objectKind"))),
        Description = FirstNonEmpty(Str(Get(payload, "cardDescription")), Str(Get(payload, "descriptionPlayer"))),
        LinkedEntityDisplayName = Str(Get(payload, "linkedEntityDisplayName")),
        X = Double(Get(payload, "x")), Y = Double(Get(payload, "y")), LabelPriority = Int(Get(payload, "labelPriority"), 0)
    };

    public static PlayerMapObjectUiItem0204 From(PlayerSceneMarkerUiItem item) => new() { ObjectId = item.MarkerId, Kind = "marker", Name = item.Name, Type = item.MarkerTypeDisplay, Description = item.CardDescription, LinkedEntityDisplayName = item.LinkedEntityDisplayName, X = item.X, Y = item.Y, LabelPriority = 300 };
    public static PlayerMapObjectUiItem0204 From(PlayerSceneTokenUiItem item) => new() { ObjectId = item.TokenId, Kind = "token", Name = item.DisplayName, Type = item.TokenTypeDisplay, Description = item.DescriptionPlayer, LinkedEntityDisplayName = item.LinkedEntityDisplayName, X = item.X, Y = item.Y, LabelPriority = 400 };
    public static PlayerMapObjectUiItem0204 From(PlayerSceneShapeUiItem item) => new() { ObjectId = item.ShapeId, Kind = "shape", Name = item.DisplayName, Type = item.ObjectKindDisplay, Description = item.DescriptionPlayer, X = item.X, Y = item.Y, LabelPriority = 100 };
    public static PlayerMapObjectUiItem0204 From(PlayerSceneAssetInstanceUiItem item) => new() { ObjectId = item.AssetInstanceId, Kind = "asset", Name = item.DisplayName, Type = item.AssetKindDisplay, Description = item.DescriptionPlayer, X = item.X, Y = item.Y, LabelPriority = 200 };
    public static string CategoryDisplay(string kind) => (kind ?? string.Empty).ToLowerInvariant() switch { "marker" => "Маркеры", "token" => "Токены", "asset" => "Объекты", "shape" => "Области", _ => "Прочее" };
    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static int Int(object? value, int fallback) => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static double Double(object? value) => double.TryParse(Convert.ToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

public sealed class PlayerMapLabelUiItem0204
{
    public string ObjectId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public double PixelX { get; set; }
    public double PixelY { get; set; }
    public double Width { get; set; }
    public bool IsSelected { get; set; }
}

public sealed class PlayerMapLegendUiItem0204 : ViewModelBase
{
    private bool _isEnabled;
    private readonly Action<PlayerMapLegendUiItem0204> _changed;
    public PlayerMapLegendUiItem0204(string category, string displayName, int visibleCount, bool isEnabled, Action<PlayerMapLegendUiItem0204> changed)
    {
        Category = category; DisplayName = displayName; VisibleCount = visibleCount; _isEnabled = isEnabled; _changed = changed;
    }
    public string Category { get; }
    public string DisplayName { get; }
    public int VisibleCount { get; }
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled == value) return; _isEnabled = value; Notify(); _changed(this); } }
    public string Summary => $"{DisplayName}: {VisibleCount}";
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
