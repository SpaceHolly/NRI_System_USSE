using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerMultiscaleMapViewModel0218 : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _characterAccessor;
    private PlayerMapItem0218? _selectedMap;
    private PlayerMapFeature0218? _selectedFeature;
    private PlayerMapFeature0218? _measureFrom;
    private string _title = "Карта мира";
    private string _subtitle = "Выберите доступную карту.";
    private string _breadcrumb = "Мир";
    private string _status = "Загрузка доступной географии.";
    private string _weather = "Погода: наблюдение не загружено";
    private string _distance = "Выберите две известные точки для измерения.";
    private string _featureTitle = "Место не выбрано";
    private string _featureDescription = "Выберите известное место на карте.";
    private bool _isBusy;
    private bool _isHexMap;
    private bool _isSchematicMap;
    private string _coordinateNotice = "Локальные координаты";
    private string _parentActionLabel = "Родительская карта недоступна";

    public PlayerMultiscaleMapViewModel0218(CommandApi api, Func<string> characterAccessor)
    {
        _api = api;
        _characterAccessor = characterAccessor;
        RefreshCommand = new RelayCommand(Initialize);
        OpenSelectedCommand = new RelayCommand(OpenSelected);
        OpenPortalCommand = new RelayCommand(parameter => OpenPortal(parameter as PlayerMapPortal0218));
        SelectFeatureCommand = new RelayCommand(parameter => SelectFeature(parameter as PlayerMapFeature0218));
        MeasureCommand = new RelayCommand(parameter => Measure(parameter as PlayerMapFeature0218));
        OpenFantasyCommand = new RelayCommand(() => OpenById("map_north_valley_0218"));
        OpenDungeonCommand = new RelayCommand(() => OpenById("map_underground_archive_0218"));
        OpenMagicSecretCommand = new RelayCommand(() => OpenById("map_underground_archive_0218"));
        OpenSciFiCommand = new RelayCommand(() => OpenById("map_sector_k12_0218"));
        OpenSystemCommand = new RelayCommand(() => OpenById("map_helios_system_0218"));
        OpenParentCommand = new RelayCommand(OpenParent);
    }

    public ObservableCollection<PlayerMapItem0218> Maps { get; } = new();
    public ObservableCollection<PlayerMapFeature0218> Areas { get; } = new();
    public ObservableCollection<PlayerMapFeature0218> Lines { get; } = new();
    public ObservableCollection<PlayerMapFeature0218> Points { get; } = new();
    public ObservableCollection<PlayerMapPortal0218> Portals { get; } = new();
    public ObservableCollection<PlayerMapLayer0218> Layers { get; } = new();
    public ObservableCollection<PlayerMapHexCell0218> HexCells { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand RefreshMapsCommand => RefreshCommand;
    public ICommand OpenSelectedCommand { get; }
    public ICommand OpenPortalCommand { get; }
    public ICommand SelectFeatureCommand { get; }
    public ICommand MeasureCommand { get; }
    public ICommand OpenFantasyCommand { get; }
    public ICommand OpenDungeonCommand { get; }
    public ICommand OpenMagicSecretCommand { get; }
    public ICommand OpenSciFiCommand { get; }
    public ICommand OpenSystemCommand { get; }
    public ICommand OpenParentCommand { get; }

    public PlayerMapItem0218? SelectedMap { get => _selectedMap; set { if (_selectedMap != value) { _selectedMap = value; Notify(); } } }
    public PlayerMapFeature0218? SelectedFeature { get => _selectedFeature; private set { if (_selectedFeature != value) { _selectedFeature = value; Notify(); } } }
    public string Title { get => _title; private set { if (_title != value) { _title = value; Notify(); } } }
    public string Subtitle { get => _subtitle; private set { if (_subtitle != value) { _subtitle = value; Notify(); } } }
    public string Breadcrumb { get => _breadcrumb; private set { if (_breadcrumb != value) { _breadcrumb = value; Notify(); } } }
    public string Status { get => _status; private set { if (_status != value) { _status = value; Notify(); } } }
    public string Weather { get => _weather; private set { if (_weather != value) { _weather = value; Notify(); } } }
    public string Distance { get => _distance; private set { if (_distance != value) { _distance = value; Notify(); } } }
    public string FeatureTitle { get => _featureTitle; private set { if (_featureTitle != value) { _featureTitle = value; Notify(); } } }
    public string FeatureDescription { get => _featureDescription; private set { if (_featureDescription != value) { _featureDescription = value; Notify(); } } }
    public bool IsBusy { get => _isBusy; private set { if (_isBusy != value) { _isBusy = value; Notify(); } } }
    public bool IsHexMap { get => _isHexMap; private set { if (_isHexMap != value) { _isHexMap = value; Notify(); } } }
    public bool IsSchematicMap { get => _isSchematicMap; private set { if (_isSchematicMap != value) { _isSchematicMap = value; Notify(); } } }
    public string CoordinateNotice { get => _coordinateNotice; private set { if (_coordinateNotice != value) { _coordinateNotice = value; Notify(); } } }
    public string ParentActionLabel { get => _parentActionLabel; private set { if (_parentActionLabel != value) { _parentActionLabel = value; Notify(); } } }
    public string MapId => SelectedMap?.MapId ?? string.Empty;
    public double CanvasWidth => 820;
    public double CanvasHeight => 540;

    public void Initialize()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var response = _api.WorldPlayerMapsList0218(new Dictionary<string, object> { { "campaignId", "dev-campaign-core" }, { "characterId", _characterAccessor() ?? string.Empty } });
            EnsureOk(response);
            Maps.Clear();
            foreach (var raw in List(response.Payload, "maps"))
            {
                var map = Dict(raw);
                Maps.Add(new PlayerMapItem0218 { MapId = Text(map, "mapId"), Name = Text(map, "name"), MapType = Text(map, "mapType"), ParentMapId = Text(map, "parentMapId") });
            }
            SelectedMap = Maps.FirstOrDefault(item => item.MapId == "map_north_valley_0218") ?? Maps.FirstOrDefault();
            if (SelectedMap != null) OpenById(SelectedMap.MapId);
            else Status = "GM ещё не открыл доступные карты.";
        }
        catch (Exception ex) { Status = "Карта недоступна: " + ex.Message; }
        finally { IsBusy = false; }
    }

    public void RefreshCurrentMap()
    {
        if (SelectedMap != null) OpenById(SelectedMap.MapId);
        else Initialize();
    }

    private void OpenSelected() { if (SelectedMap != null) OpenById(SelectedMap.MapId); }

    private void OpenById(string mapId)
    {
        try
        {
            var response = _api.WorldPlayerMapGet0218(new Dictionary<string, object> { { "mapId", mapId }, { "characterId", _characterAccessor() ?? string.Empty } });
            EnsureOk(response);
            ApplyMap(response.Payload);
            SelectedMap = Maps.FirstOrDefault(item => item.MapId == mapId) ?? SelectedMap;
        }
        catch (Exception ex) { Status = "Не удалось открыть карту: " + ex.Message; }
    }

    private void OpenPortal(PlayerMapPortal0218? portal)
    {
        if (portal == null) return;
        try
        {
            var response = _api.WorldPlayerMapPortalOpen0218(new Dictionary<string, object> { { "portalId", portal.PortalId }, { "characterId", _characterAccessor() ?? string.Empty } });
            EnsureOk(response);
            ApplyMap(response.Payload);
        }
        catch (Exception ex) { Status = "Переход недоступен: " + ex.Message; }
    }

    private void ApplyMap(IDictionary<string, object> payload)
    {
        var map = payload.TryGetValue("map", out var rawMap) ? Dict(rawMap) : payload;
        var mapId = Text(map, "mapId");
        SelectedMap = Maps.FirstOrDefault(item => item.MapId == mapId) ?? SelectedMap;
        Title = Text(map, "name");
        Subtitle = MapTypeLabel(Text(map, "mapType")) + " · " + PhysicalSize(map);
        Breadcrumb = BuildBreadcrumb(Text(map, "mapId"));
        CoordinateNotice = Text(map, "coordinateProfileKind");
        IsHexMap = CoordinateNotice == "Гексагональная сетка";
        IsSchematicMap = CoordinateNotice == "Схематическая карта";
        BuildHexCells();
        var parent = Maps.FirstOrDefault(item => item.MapId == SelectedMap?.ParentMapId);
        ParentActionLabel = parent == null ? "Родительская карта недоступна" : "Назад: " + parent.Name;
        Areas.Clear(); Lines.Clear(); Points.Clear(); Portals.Clear(); Layers.Clear();
        foreach (var raw in List(payload, "features")) AddFeature(ParseFeature(Dict(raw)));
        foreach (var raw in List(payload, "portals"))
        {
            var portal = Dict(raw);
            Portals.Add(new PlayerMapPortal0218 { PortalId = Text(portal, "portalId"), Name = Text(portal, "name"), TargetMapId = Text(portal, "targetMapId") });
        }
        foreach (var raw in List(payload, "layers"))
        {
            var layer = Dict(raw);
            Layers.Add(new PlayerMapLayer0218 { Name = Text(layer, "name"), Kind = Text(layer, "layerKind") });
        }
        Weather = payload.TryGetValue("weatherBadge", out var weatherRaw) ? Text(Dict(weatherRaw), "label") : "Погода: наблюдение не загружено";
        Status = $"Открыта карта «{Title}». Известных объектов: {Areas.Count + Lines.Count + Points.Count}.";
        Distance = IsSchematicMap ? "Физическое расстояние нельзя определить по расположению объектов на этой схеме." : "Выберите две известные точки для измерения.";
        _measureFrom = null;
    }

    private void OpenParent()
    {
        var parentId = SelectedMap?.ParentMapId;
        if (!string.IsNullOrWhiteSpace(parentId) && Maps.Any(item => item.MapId == parentId)) OpenById(parentId);
        else Status = "Родительская карта не открыта персонажу.";
    }

    private void BuildHexCells()
    {
        HexCells.Clear();
        if (!IsHexMap) return;
        for (var r = -2; r <= 2; r++)
        for (var q = -3; q <= 3; q++)
            HexCells.Add(new PlayerMapHexCell0218 { Q = q, R = r, X = 325 + q * 69 + r * 34.5, Y = 225 + r * 60 });
    }

    private void AddFeature(PlayerMapFeature0218 feature)
    {
        if (feature.GeometryKind == "polygon") Areas.Add(feature);
        else if (feature.GeometryKind == "polyline") Lines.Add(feature);
        else Points.Add(feature);
    }

    private PlayerMapFeature0218 ParseFeature(IDictionary<string, object> raw)
    {
        var points = List(raw, "points").Select(item => Dict(item)).Select(item => new Point0218(Number(item, "x"), Number(item, "y"))).ToList();
        if (points.Count == 0) points.Add(new Point0218(50, 50));
        var minX = points.Min(item => item.X); var maxX = points.Max(item => item.X);
        var minY = points.Min(item => item.Y); var maxY = points.Max(item => item.Y);
        return new PlayerMapFeature0218
        {
            FeatureId = Text(raw, "featureId"), Name = Text(raw, "name"), SemanticKind = Text(raw, "semanticKind"), GeometryKind = Text(raw, "geometryKind"),
            Description = Text(raw, "publicDescription"), Precision = Text(raw, "precision"), X = minX / 100d * CanvasWidth, Y = minY / 100d * CanvasHeight,
            X2 = points.Last().X / 100d * CanvasWidth, Y2 = points.Last().Y / 100d * CanvasHeight,
            Width = Math.Max(18, (maxX - minX) / 100d * CanvasWidth), Height = Math.Max(18, (maxY - minY) / 100d * CanvasHeight),
            Fill = FeatureFill(Text(raw, "semanticKind")), PrecisionLabel = Text(raw, "precision") == "approximate" ? "примерное положение" : "точное положение"
        };
    }

    private void SelectFeature(PlayerMapFeature0218? feature)
    {
        if (feature == null) return;
        SelectedFeature = feature;
        FeatureTitle = feature.Name;
        FeatureDescription = $"{SemanticLabel(feature.SemanticKind)} · {feature.PrecisionLabel}. {feature.Description}";
    }

    private void Measure(PlayerMapFeature0218? feature)
    {
        if (feature == null) return;
        SelectFeature(feature);
        if (_measureFrom == null) { _measureFrom = feature; Distance = "Начальная точка: " + feature.Name + ". Выберите конечную точку."; return; }
        if (SelectedMap == null) return;
        try
        {
            var response = _api.WorldPlayerMapDistancePreview0218(new Dictionary<string, object> { { "mapId", SelectedMap.MapId }, { "fromFeatureId", _measureFrom.FeatureId }, { "toFeatureId", feature.FeatureId } });
            EnsureOk(response);
            Distance = Text(response.Payload, "display");
        }
        catch (Exception ex) { Distance = "Измерение недоступно: " + ex.Message; }
        finally { _measureFrom = null; }
    }

    private string BuildBreadcrumb(string mapId)
    {
        var chain = new List<string>();
        var current = Maps.FirstOrDefault(item => item.MapId == mapId);
        var guard = 0;
        while (current != null && guard++ < 12)
        {
            chain.Add(current.Name);
            current = Maps.FirstOrDefault(item => item.MapId == current.ParentMapId);
        }
        chain.Reverse();
        return chain.Count == 0 ? "Мир" : string.Join("  ›  ", chain);
    }

    private static void EnsureOk(ResponseEnvelope response) { if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? "Сервер отклонил операцию." : response.Message); }
    private static IList<object> List(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return Array.Empty<object>();
        if (value is IList<object> typed) return typed;
        if (value is IEnumerable enumerable && !(value is string)) return enumerable.Cast<object>().ToList();
        return Array.Empty<object>();
    }
    private static Dictionary<string, object> Dict(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary<string, object> generic) return new Dictionary<string, object>(generic);
        if (value is IDictionary legacy) return legacy.Keys.Cast<object>().ToDictionary(key => Convert.ToString(key) ?? string.Empty, key => legacy[key]!);
        return new Dictionary<string, object>();
    }
    private static string Text(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty : string.Empty;
    private static double Number(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : 0;
    private static int Int(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
    private static string PhysicalSize(IDictionary<string, object> map) => Int(map, "widthMeters") > 0 ? $"{Int(map, "widthMeters") / 1000d:0.#} × {Int(map, "heightMeters") / 1000d:0.#} км" : "масштаб не указан";
    private static string MapTypeLabel(string value) => value switch { "world" => "Мир", "region" => "Регион", "settlement" => "Поселение", "district" => "Район", "interior" => "Интерьер", "dungeon" => "Подземелье", "galaxy" => "Галактика", "sector" => "Сектор", "star_system" => "Звёздная система", "planet" => "Планета", "orbital" => "Орбитальный объект", _ => "Карта" };
    private static string SemanticLabel(string value) => value switch { "road" => "Дорога", "river" => "Река", "area" => "Область", "district" => "Район", "room" => "Помещение", "structure" => "Строение", "entrance" => "Вход", "stairs" => "Лестница", "label" => "Подпись", "star" => "Звезда", "planet" => "Планета", "station" => "Станция", "secret" => "Открытая тайна", _ => "Точка интереса" };
    private static string FeatureFill(string kind) => kind switch { "river" => "#2B6CB0", "road" => "#B7791F", "secret" => "#805AD5", "star" => "#ECC94B", "planet" => "#38A169", "station" => "#A0AEC0", "district" => "#2C5282", "room" => "#4A5568", "structure" => "#64748B", "entrance" => "#E59E3A", "stairs" => "#D97706", "label" => "#256D5A", _ => "#2F855A" };
}

public sealed class PlayerMapItem0218 { public string MapId { get; set; } = ""; public string Name { get; set; } = ""; public string MapType { get; set; } = ""; public string ParentMapId { get; set; } = ""; public string Label => Name + " · " + MapType; public override string ToString() => Name; }
public sealed class PlayerMapPortal0218 { public string PortalId { get; set; } = ""; public string Name { get; set; } = ""; public string TargetMapId { get; set; } = ""; }
public sealed class PlayerMapLayer0218 { public string Name { get; set; } = ""; public string Kind { get; set; } = ""; public string Label => Name; }
public sealed class Point0218 { public Point0218(double x, double y) { X = x; Y = y; } public double X { get; } public double Y { get; } }
public sealed class PlayerMapHexCell0218 { public int Q { get; set; } public int R { get; set; } public double X { get; set; } public double Y { get; set; } public string Points => "46,0 92,20 92,60 46,80 0,60 0,20"; public string Label => $"q {Q} · r {R}"; }
public sealed class PlayerMapFeature0218
{
    public string FeatureId { get; set; } = ""; public string Name { get; set; } = ""; public string SemanticKind { get; set; } = ""; public string GeometryKind { get; set; } = "";
    public string Description { get; set; } = ""; public string Precision { get; set; } = ""; public string PrecisionLabel { get; set; } = ""; public string Fill { get; set; } = "#2F855A";
    public double X { get; set; } public double Y { get; set; } public double X2 { get; set; } public double Y2 { get; set; } public double Width { get; set; } public double Height { get; set; }
}
