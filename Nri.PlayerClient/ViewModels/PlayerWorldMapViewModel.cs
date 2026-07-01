using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerWorldMapViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private string _campaignId = "default";
    private string _mapId = string.Empty;
    private bool _advancedMapIdMode;
    private bool _isLoading;
    private string _statusMessage = "Откройте карту мира, доступную игрокам.";
    private string _warningMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _mapName = "Карта мира не выбрана.";
    private string _mapDescription = string.Empty;
    private string _mapProjection = WorldMapProjectionModeIds.FlatGrid;
    private int _widthCells;
    private int _heightCells;
    private double _canvasWidth = 820d;
    private double _canvasHeight = 500d;
    private double _cellPixelSize;
    private string _scaleText = "нет данных";
    private string _selectedLayerType = WorldMapLayerTypeIds.HeightDepth;
    private DateTime _lastRefreshAtUtc;
    private PlayerWorldMarkerUiItem? _selectedMarker;
    private PlayerWorldMapListItemVm? _selectedMapItem;

    private readonly Dictionary<string, List<PlayerLegendEntryVm>> _legendByLayer = new(StringComparer.OrdinalIgnoreCase);

    public PlayerWorldMapViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        RefreshMapsCommand = new RelayCommand(LoadAvailableMaps);
        OpenSelectedMapCommand = new RelayCommand(OpenSelectedMap);
        RefreshMapCommand = new RelayCommand(RefreshCurrentMap);
        OpenMapByIdCommand = new RelayCommand(() =>
        {
            if (string.IsNullOrWhiteSpace(MapId))
            {
                ErrorMessage = "Укажите MapId для открытия карты.";
                return;
            }

            OpenWorldMap(MapId);
        });
        SelectLayerCommand = new RelayCommand(param => SelectLayer(param as string));
    }

    public ObservableCollection<PlayerWorldMapListItemVm> AvailableMaps { get; } = new();
    public ObservableCollection<MapGridLineUiItem> GridLines { get; } = new();
    public ObservableCollection<PlayerWorldCellUiItem> LayerCells { get; } = new();
    public ObservableCollection<PlayerWorldMarkerUiItem> Markers { get; } = new();
    public ObservableCollection<PlayerLegendEntryVm> LegendEntries { get; } = new();
    public ObservableCollection<string> LayerOptions { get; } = new();
    public ObservableCollection<string> Hints { get; } = new();

    public ICommand RefreshMapsCommand { get; }
    public ICommand OpenSelectedMapCommand { get; }
    public ICommand RefreshMapCommand { get; }
    public ICommand OpenMapByIdCommand { get; }
    public ICommand SelectLayerCommand { get; }

    public string CampaignId
    {
        get => _campaignId;
        set { if (_campaignId != value) { _campaignId = value; Notify(); } }
    }

    public string MapId
    {
        get => _mapId;
        set { if (_mapId != value) { _mapId = value; Notify(); } }
    }

    public bool AdvancedMapIdMode
    {
        get => _advancedMapIdMode;
        set { if (_advancedMapIdMode != value) { _advancedMapIdMode = value; Notify(); } }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { if (_isLoading != value) { _isLoading = value; Notify(); } }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } }
    }

    public string WarningMessage
    {
        get => _warningMessage;
        private set { if (_warningMessage != value) { _warningMessage = value; Notify(); Notify(nameof(HasWarning)); } }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } }
    }

    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string MapName
    {
        get => _mapName;
        private set { if (_mapName != value) { _mapName = value; Notify(); } }
    }

    public string MapDescription
    {
        get => _mapDescription;
        private set { if (_mapDescription != value) { _mapDescription = value; Notify(); } }
    }

    public string MapProjection
    {
        get => _mapProjection;
        private set { if (_mapProjection != value) { _mapProjection = value; Notify(); Notify(nameof(MapMetaText)); } }
    }

    public int WidthCells
    {
        get => _widthCells;
        private set { if (_widthCells != value) { _widthCells = value; Notify(); Notify(nameof(MapMetaText)); } }
    }

    public int HeightCells
    {
        get => _heightCells;
        private set { if (_heightCells != value) { _heightCells = value; Notify(); Notify(nameof(MapMetaText)); } }
    }

    public string MapMetaText => WidthCells > 0 && HeightCells > 0
        ? $"{WidthCells}×{HeightCells} • {MapProjection}"
        : "Размер карты не загружен";

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

    public string ScaleText
    {
        get => _scaleText;
        private set { if (_scaleText != value) { _scaleText = value; Notify(); } }
    }

    public string SelectedLayerType
    {
        get => _selectedLayerType;
        set
        {
            if (_selectedLayerType == value) return;
            _selectedLayerType = value;
            Notify();
            Notify(nameof(SelectedLayerDisplayName));
            RebuildCells();
            RebuildLegend();
        }
    }

    public string SelectedLayerDisplayName => LayerDisplayName(SelectedLayerType);

    public DateTime LastRefreshAtUtc
    {
        get => _lastRefreshAtUtc;
        private set { if (_lastRefreshAtUtc != value) { _lastRefreshAtUtc = value; Notify(); Notify(nameof(LastRefreshText)); } }
    }

    public string LastRefreshText => LastRefreshAtUtc == default
        ? "ещё не обновлялось"
        : LastRefreshAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public PlayerWorldMapListItemVm? SelectedMapItem
    {
        get => _selectedMapItem;
        set
        {
            if (_selectedMapItem == value) return;
            _selectedMapItem = value;
            Notify();
            if (value != null)
                MapId = value.MapId;
        }
    }

    public PlayerWorldMarkerUiItem? SelectedMarker
    {
        get => _selectedMarker;
        set
        {
            if (_selectedMarker == value) return;
            _selectedMarker = value;
            foreach (var marker in Markers) marker.IsSelected = marker == value;
            Notify();
            Notify(nameof(SelectedMarkerTitle));
            Notify(nameof(SelectedMarkerType));
            Notify(nameof(SelectedMarkerCoords));
            Notify(nameof(SelectedMarkerBinding));
            Notify(nameof(SelectedMarkerDescription));
        }
    }

    public string SelectedMarkerTitle => SelectedMarker?.Name ?? "Маркер не выбран";
    public string SelectedMarkerType => SelectedMarker?.MarkerTypeDisplay ?? "—";
    public string SelectedMarkerCoords => SelectedMarker == null ? "—" : SelectedMarker.CoordinatesText;
    public string SelectedMarkerBinding => SelectedMarker?.BindingText ?? "Без привязки";
    public string SelectedMarkerDescription => string.IsNullOrWhiteSpace(SelectedMarker?.CardDescription)
        ? "Описание отсутствует."
        : SelectedMarker!.CardDescription;

    public string MarkerSummaryText => Markers.Count == 0
        ? "Видимых маркеров на карте нет."
        : "Видимые маркеры: " + string.Join(" · ", Markers.Select(marker => marker.Name));

    public void LoadAvailableMaps()
    {
        if (string.IsNullOrWhiteSpace(CampaignId))
        {
            ErrorMessage = "Кампания не выбрана.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info("player.map.world.list.load");
            var response = _api.MapPlayerWorldList(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "characterId", _activeCharacterIdAccessor() ?? string.Empty },
                { "includeMarkers", true }
            });

            if (!EnsureWorldOk(response, out var err))
            {
                ErrorMessage = err;
                StatusMessage = "Карта мира пока недоступна.";
                return;
            }

            AvailableMaps.Clear();
            foreach (var item in Dictionaries(Get(response.Payload, "items")))
            {
                AvailableMaps.Add(new PlayerWorldMapListItemVm
                {
                    MapId = Str(Get(item, "mapId")),
                    Name = FirstNonEmpty(Str(Get(item, "name")), "Карта мира"),
                    Description = Str(Get(item, "description")),
                    UpdatedAtUtc = Date(Get(item, "updatedAtUtc"))
                });
            }

            if (AvailableMaps.Count == 0)
            {
                StatusMessage = "GM ещё не открыл карту мира игрокам.";
                ClearMapState();
                LastRefreshAtUtc = DateTime.UtcNow;
                return;
            }

            if (SelectedMapItem == null || !AvailableMaps.Any(x => string.Equals(x.MapId, SelectedMapItem.MapId, StringComparison.OrdinalIgnoreCase)))
                SelectedMapItem = AvailableMaps[0];

            if (!string.IsNullOrWhiteSpace(SelectedMapItem?.MapId))
                OpenWorldMap(SelectedMapItem.MapId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка загрузки списка карт мира: {ex.Message}";
            StatusMessage = "Карта мира пока недоступна.";
            ClientLogService.Instance.Warn($"player.map.world.list.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void OpenSelectedMap()
    {
        if (SelectedMapItem == null)
        {
            ErrorMessage = "Карта мира не выбрана.";
            return;
        }

        OpenWorldMap(SelectedMapItem.MapId);
    }

    public void RefreshCurrentMap()
    {
        if (!string.IsNullOrWhiteSpace(MapId))
        {
            OpenWorldMap(MapId);
            return;
        }

        LoadAvailableMaps();
    }

    private void OpenWorldMap(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            ErrorMessage = "Карта мира не выбрана.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info("player.map.world.open");
            var response = _api.MapPlayerWorldGet(new Dictionary<string, object>
            {
                { "mapId", mapId },
                { "campaignId", CampaignId },
                { "characterId", _activeCharacterIdAccessor() ?? string.Empty },
                { "includeMarkers", true },
                { "includeLayers", true }
            });

            if (!EnsureWorldOk(response, out var err))
            {
                ErrorMessage = err;
                StatusMessage = "Карта мира пока недоступна.";
                return;
            }

            var map = AsMap(Get(response.Payload, "map"));
            MapId = FirstNonEmpty(Str(Get(map, "mapId")), mapId);
            MapName = FirstNonEmpty(Str(Get(map, "name")), "Карта мира");
            MapDescription = Str(Get(map, "description"));
            WidthCells = Int(Get(map, "widthCells"), 0);
            HeightCells = Int(Get(map, "heightCells"), 0);
            MapProjection = FirstNonEmpty(Str(Get(map, "projectionMode")), WorldMapProjectionModeIds.FlatGrid);

            ParseLayers(map);
            ParseLegends(map);
            ParseMarkers(map);
            RebuildCanvas();

            if (Markers.Count == 0)
                StatusMessage = "На карте нет видимых маркеров.";
            else
                StatusMessage = $"Карта загружена. Видимых маркеров: {Markers.Count}.";

            var warnings = ToStrings(Get(response.Payload, "warnings"));
            WarningMessage = warnings.Count > 0 ? string.Join(" | ", warnings) : string.Empty;
            LastRefreshAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка загрузки карты мира: {ex.Message}";
            StatusMessage = "Карта мира пока недоступна.";
            ClientLogService.Instance.Warn($"player.map.world.open.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ParseLayers(Dictionary<string, object> map)
    {
        LayerOptions.Clear();
        _layerCells.Clear();
        foreach (var layerPayload in Dictionaries(Get(map, "layers")))
        {
            var layerType = Str(Get(layerPayload, "layerType"));
            if (string.IsNullOrWhiteSpace(layerType)) continue;
            LayerOptions.Add(layerType);

            var cells = new List<PlayerWorldCellUiItem>();
            foreach (var cellPayload in Dictionaries(Get(layerPayload, "cells")))
            {
                var cell = new PlayerWorldCellUiItem
                {
                    LayerType = layerType,
                    CellX = Int(Get(cellPayload, "cellX"), -1),
                    CellY = Int(Get(cellPayload, "cellY"), -1),
                    Value = Str(Get(cellPayload, "value")),
                    Label = Str(Get(cellPayload, "label"))
                };
                if (cell.CellX >= 0 && cell.CellY >= 0)
                    cells.Add(cell);
            }

            _layerCells[layerType] = cells;
        }

        if (LayerOptions.Count == 0)
            LayerOptions.Add(WorldMapLayerTypeIds.Marker);

        if (!LayerOptions.Contains(SelectedLayerType))
            SelectedLayerType = LayerOptions[0];
    }

    private readonly Dictionary<string, List<PlayerWorldCellUiItem>> _layerCells = new(StringComparer.OrdinalIgnoreCase);

    private void ParseLegends(Dictionary<string, object> map)
    {
        _legendByLayer.Clear();
        foreach (var legendPayload in Dictionaries(Get(map, "legends")))
        {
            var layerType = Str(Get(legendPayload, "layerType"));
            if (string.IsNullOrWhiteSpace(layerType)) continue;

            var entries = new List<PlayerLegendEntryVm>();
            foreach (var entryPayload in Dictionaries(Get(legendPayload, "entries")))
            {
                entries.Add(new PlayerLegendEntryVm
                {
                    Key = Str(Get(entryPayload, "key")),
                    Label = Str(Get(entryPayload, "label"))
                });
            }

            _legendByLayer[layerType] = entries;
        }
    }

    private void ParseMarkers(Dictionary<string, object> map)
    {
        Markers.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var markerPayload in Dictionaries(Get(map, "markers")))
            AddMarkerIfNew(PlayerWorldMarkerUiItem.From(markerPayload), seen);
        foreach (var locationPayload in Dictionaries(Get(map, "locations")))
            AddMarkerIfNew(PlayerWorldMarkerUiItem.From(locationPayload), seen);
        foreach (var regionPayload in Dictionaries(Get(map, "regions")))
            AddMarkerIfNew(PlayerWorldMarkerUiItem.From(regionPayload), seen);
        SelectedMarker = Markers.FirstOrDefault();
        Notify(nameof(MarkerSummaryText));
    }

    private void AddMarkerIfNew(PlayerWorldMarkerUiItem marker, HashSet<string> seen)
    {
        var key = $"{marker.Name}|{marker.MarkerType}|{marker.CellX}|{marker.CellY}";
        if (seen.Add(key))
            Markers.Add(marker);
    }

    private void SelectLayer(string? layerType)
    {
        if (string.IsNullOrWhiteSpace(layerType)) return;
        SelectedLayerType = layerType;
        RebuildCells();
        RebuildLegend();
        ClientLogService.Instance.Info("player.map.world.layer.selected");
    }

    private void RebuildCanvas()
    {
        GridLines.Clear();
        Hints.Clear();
        RebuildLegend();

        if (WidthCells <= 0 || HeightCells <= 0)
        {
            CanvasWidth = 820d;
            CanvasHeight = 500d;
            _cellPixelSize = 0d;
            ScaleText = "нет данных";
            LayerCells.Clear();
            return;
        }

        var targetWidth = 860d;
        var targetHeight = 540d;
        var pxByWidth = targetWidth / Math.Max(1, WidthCells);
        var pxByHeight = targetHeight / Math.Max(1, HeightCells);
        _cellPixelSize = Math.Max(2d, Math.Min(16d, Math.Min(pxByWidth, pxByHeight)));

        CanvasWidth = Math.Round(Math.Max(240d, WidthCells * _cellPixelSize), 2);
        CanvasHeight = Math.Round(Math.Max(180d, HeightCells * _cellPixelSize), 2);
        ScaleText = $"1 клетка = {_cellPixelSize:0.##} px";

        for (var x = 0; x <= WidthCells; x++)
        {
            var px = x * _cellPixelSize;
            GridLines.Add(new MapGridLineUiItem { X1 = px, Y1 = 0, X2 = px, Y2 = CanvasHeight });
        }

        for (var y = 0; y <= HeightCells; y++)
        {
            var py = y * _cellPixelSize;
            GridLines.Add(new MapGridLineUiItem { X1 = 0, Y1 = py, X2 = CanvasWidth, Y2 = py });
        }

        RebuildCells();
        RebuildMarkers();
        Hints.Add($"Координаты: X 0..{Math.Max(0, WidthCells - 1)}, Y 0..{Math.Max(0, HeightCells - 1)}");
    }

    private void RebuildCells()
    {
        LayerCells.Clear();
        if (_cellPixelSize <= 0 || !_layerCells.TryGetValue(SelectedLayerType, out var cells)) return;

        foreach (var cell in cells)
        {
            if (cell.CellX < 0 || cell.CellY < 0 || cell.CellX >= WidthCells || cell.CellY >= HeightCells) continue;
            cell.X = cell.CellX * _cellPixelSize;
            cell.Y = cell.CellY * _cellPixelSize;
            cell.Width = _cellPixelSize;
            cell.Height = _cellPixelSize;
            cell.FillHex = ResolveCellColor(SelectedLayerType, cell.Value);
            LayerCells.Add(cell);
        }
    }

    private void RebuildMarkers()
    {
        if (_cellPixelSize <= 0) return;
        foreach (var marker in Markers)
        {
            if (marker.CellX >= 0 && marker.CellY >= 0)
            {
                marker.PixelX = (marker.CellX * _cellPixelSize) + (_cellPixelSize * 0.5d);
                marker.PixelY = (marker.CellY * _cellPixelSize) + (_cellPixelSize * 0.5d);
            }
            else if (marker.XNormalized >= 0d && marker.YNormalized >= 0d)
            {
                marker.PixelX = marker.XNormalized * CanvasWidth;
                marker.PixelY = marker.YNormalized * CanvasHeight;
            }
            else
            {
                marker.PixelX = 0d;
                marker.PixelY = 0d;
            }
        }
    }

    private void RebuildLegend()
    {
        LegendEntries.Clear();
        if (_legendByLayer.TryGetValue(SelectedLayerType, out var entries))
        {
            foreach (var entry in entries)
                LegendEntries.Add(entry);
        }
    }

    private void ClearMapState()
    {
        MapName = "Карта мира не выбрана.";
        MapDescription = string.Empty;
        WidthCells = 0;
        HeightCells = 0;
        LayerOptions.Clear();
        LegendEntries.Clear();
        Markers.Clear();
        Notify(nameof(MarkerSummaryText));
        LayerCells.Clear();
        GridLines.Clear();
        Hints.Clear();
    }

    private static bool EnsureWorldOk(ResponseEnvelope response, out string error)
    {
        if (response.Status == ResponseStatus.Ok)
        {
            error = string.Empty;
            return true;
        }

        var text = (response.Message ?? string.Empty).Trim();
        if (response.Status == ResponseStatus.Forbidden && text.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0)
            error = "Карта мира пока недоступна.";
        else if (text.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 text.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0)
            error = "Карта мира пока недоступна.";
        else if (response.Status == ResponseStatus.NotFound)
            error = "Карта мира не найдена.";
        else if (response.Status == ResponseStatus.Forbidden)
            error = "Недостаточно прав для просмотра карты мира.";
        else
            error = string.IsNullOrWhiteSpace(text) ? "Не удалось загрузить карту мира." : text;

        return false;
    }

    private static string LayerDisplayName(string layerType)
    {
        if (string.Equals(layerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase)) return "Высота / глубина";
        if (string.Equals(layerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase)) return "Биомы";
        if (string.Equals(layerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase)) return "Страны / области";
        if (string.Equals(layerType, WorldMapLayerTypeIds.Marker, StringComparison.OrdinalIgnoreCase)) return "Маркеры";
        return string.IsNullOrWhiteSpace(layerType) ? "Слой" : layerType;
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

        var hash = Math.Abs(key.GetHashCode());
        var palette = new[]
        {
            "#FFE879F9", "#FFF472B6", "#FF60A5FA", "#FF34D399",
            "#FFFBBF24", "#FFA78BFA", "#FFF97316", "#FF22D3EE"
        };
        return palette[hash % palette.Length];
    }

    private static object? Get(IDictionary<string, object>? map, string key)
    {
        if (map == null || string.IsNullOrWhiteSpace(key)) return null;
        return map.TryGetValue(key, out var value) ? value : null;
    }

    private static Dictionary<string, object> AsMap(object? value)
    {
        if (value is Dictionary<string, object> typed)
            return typed;
        if (value is IDictionary dict)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dict)
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
                var entry = AsMap(item);
                if (entry.Count == 0) continue;
                var key = Convert.ToString(Get(entry, "key"));
                if (string.IsNullOrWhiteSpace(key)) continue;
                result[key] = Get(entry, "value")!;
            }

            if (result.Count > 0)
                return result;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Dictionary<string, object>> Dictionaries(object? value)
    {
        if (value is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                var map = AsMap(item);
                if (map.Count > 0)
                    yield return map;
            }
        }
    }

    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static int Int(object? value, int fallback)
    {
        if (value is int typed) return typed;
        if (value is long l && l <= int.MaxValue && l >= int.MinValue) return (int)l;
        return int.TryParse(Convert.ToString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static double Dbl(object? value, double fallback)
    {
        if (value is double typed) return typed;
        if (value is float f) return f;
        if (value is decimal d) return (double)d;
        return double.TryParse(Convert.ToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static DateTime Date(object? value)
    {
        if (value is DateTime dt) return dt;
        return DateTime.TryParse(Convert.ToString(value), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : default;
    }

    private static List<string> ToStrings(object? value)
    {
        var list = new List<string>();
        if (value is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                var text = Convert.ToString(item);
                if (!string.IsNullOrWhiteSpace(text))
                    list.Add(text);
            }
        }

        return list;
    }
}

public sealed class PlayerWorldMapListItemVm
{
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public string Display => string.IsNullOrWhiteSpace(Description) ? Name : $"{Name} — {Description}";
}

public sealed class PlayerWorldCellUiItem
{
    public string LayerType { get; set; } = string.Empty;
    public int CellX { get; set; }
    public int CellY { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string FillHex { get; set; } = "#FF64748B";
}

public sealed class PlayerWorldMarkerUiItem : ViewModelBase
{
    private bool _isSelected;
    public string MarkerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = MapMarkerTypeIds.Custom;
    public double XNormalized { get; set; } = -1d;
    public double YNormalized { get; set; } = -1d;
    public int CellX { get; set; } = -1;
    public int CellY { get; set; } = -1;
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityDisplayName { get; set; } = string.Empty;
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

    public string BindingText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LinkedEntityType) && string.IsNullOrWhiteSpace(LinkedEntityDisplayName))
                return "Без привязки";
            if (string.IsNullOrWhiteSpace(LinkedEntityDisplayName))
                return LinkedEntityType;
            return $"{LinkedEntityType}: {LinkedEntityDisplayName}";
        }
    }

    public static PlayerWorldMarkerUiItem From(Dictionary<string, object> payload)
    {
        return new PlayerWorldMarkerUiItem
        {
            MarkerId = FirstNonEmpty(GetText(payload, "markerId"), GetText(payload, "id")),
            Name = FirstNonEmpty(GetText(payload, "name"), GetText(payload, "displayName"), GetText(payload, "label"), GetText(payload, "text"), "Маркер"),
            MarkerType = NormalizeMarkerType(FirstNonEmpty(GetText(payload, "markerType"), GetText(payload, "locationType"), GetText(payload, "regionType"), MapMarkerTypeIds.Custom)),
            XNormalized = ToDouble(GetAny(payload, "xNormalized"), -1d),
            YNormalized = ToDouble(GetAny(payload, "yNormalized"), -1d),
            CellX = ToInt(GetAny(payload, "cellX"), -1),
            CellY = ToInt(GetAny(payload, "cellY"), -1),
            IconKey = GetText(payload, "iconKey"),
            ColorKey = GetText(payload, "colorKey"),
            CardTitle = FirstNonEmpty(GetText(payload, "cardTitle"), GetText(payload, "displayName"), GetText(payload, "name")),
            CardDescription = FirstNonEmpty(GetText(payload, "cardDescription"), GetText(payload, "publicDescription"), GetText(payload, "publicNotes")),
            LinkedEntityType = FirstNonEmpty(GetText(payload, "linkedEntityType"), InferBindingType(payload)),
            LinkedEntityDisplayName = FirstNonEmpty(GetText(payload, "linkedEntityDisplayName"), GetText(payload, "displayName"), GetText(payload, "name"))
        };
    }

    private static object? GetAny(Dictionary<string, object> payload, string key)
    {
        if (payload.TryGetValue(key, out var value)) return value;
        foreach (var pair in payload)
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        return null;
    }

    private static string GetText(Dictionary<string, object> payload, string key)
        => Convert.ToString(GetAny(payload, key)) ?? string.Empty;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string InferBindingType(Dictionary<string, object> payload)
    {
        if (!string.IsNullOrWhiteSpace(GetText(payload, "regionType")))
            return MapMarkerBindingTypeIds.Region;
        if (!string.IsNullOrWhiteSpace(GetText(payload, "locationType")))
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

    private static int ToInt(object? value, int fallback)
    {
        if (value is int i) return i;
        if (value is long l && l <= int.MaxValue && l >= int.MinValue) return (int)l;
        return int.TryParse(Convert.ToString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static double ToDouble(object? value, double fallback)
    {
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is decimal m) return (double)m;
        return double.TryParse(Convert.ToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }
}

public sealed class PlayerLegendEntryVm
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Display => string.IsNullOrWhiteSpace(Key) ? Label : $"{Label} ({Key})";
}
