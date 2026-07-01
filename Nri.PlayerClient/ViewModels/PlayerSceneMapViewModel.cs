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
using Nri.Shared.Utilities;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerSceneMapViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;

    private string _campaignId = "default";
    private string _sessionId = "default";
    private string _activeGroupId = string.Empty;
    private bool _manualMapIdMode;
    private string _mapId = string.Empty;
    private string _mapName = "Карта сцены не выбрана.";
    private string _mapDescription = string.Empty;
    private int _widthMeters;
    private int _heightMeters;
    private int _gridCellSizeMeters = 25;
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

    private bool _fogEnabled;
    private string _fogMode = FogOfWarModeIds.Disabled;
    private int _fogCellSizeMeters = 25;
    private readonly List<MapFogCellRange> _fogHiddenRanges = new();

    public PlayerSceneMapViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        OpenMapCommand = new RelayCommand(Refresh);
        RefreshCommand = new RelayCommand(Refresh);
        ClearErrorCommand = new RelayCommand(() =>
        {
            ErrorMessage = string.Empty;
            WarningMessage = string.Empty;
        });
    }

    public ObservableCollection<MapGridLineUiItem> GridLines { get; } = new();
    public ObservableCollection<MapFogOverlayUiItem> FogOverlays { get; } = new();
    public ObservableCollection<PlayerSceneMarkerUiItem> Markers { get; } = new();
    public ObservableCollection<string> CoordinateHints { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();

    public ICommand OpenMapCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearErrorCommand { get; }

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

        LoadActiveMap();
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
        SelectedMarker = Markers.FirstOrDefault();

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
        var projection = MapCanvasProjectionHelper.Calculate(WidthMeters, HeightMeters, 860, 540);
        CanvasWidth = projection.CanvasWidth;
        CanvasHeight = projection.CanvasHeight;
        CanvasScaleLabel = $"1м = {projection.Scale:0.###}px";

        if (ShowGrid && WidthMeters > 0 && HeightMeters > 0)
        {
            var step = Math.Max(1, GridCellSizeMeters);
            for (var x = 0; x <= WidthMeters; x += step)
            {
                var px = MapCanvasProjectionHelper.ToPixel(x, projection.Scale);
                GridLines.Add(new MapGridLineUiItem { X1 = px, Y1 = 0, X2 = px, Y2 = CanvasHeight });
            }

            for (var y = 0; y <= HeightMeters; y += step)
            {
                var py = MapCanvasProjectionHelper.ToPixel(y, projection.Scale);
                GridLines.Add(new MapGridLineUiItem { X1 = 0, Y1 = py, X2 = CanvasWidth, Y2 = py });
            }
        }

        foreach (var marker in Markers)
        {
            marker.PixelX = MapCanvasProjectionHelper.ToPixel(marker.X, projection.Scale);
            marker.PixelY = MapCanvasProjectionHelper.ToPixel(marker.Y, projection.Scale);
        }

        BuildFogOverlay(projection.Scale);

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

    private void BuildFogOverlay(double scale)
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
                X = MapCanvasProjectionHelper.ToPixel(fromX, scale),
                Y = MapCanvasProjectionHelper.ToPixel(fromY, scale),
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
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
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

    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static double Double(object? value, double fallback) => double.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static bool Bool(object? value, bool fallback) => bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
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
