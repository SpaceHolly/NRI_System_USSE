using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminMultiscaleMapViewModel0218 : ViewModelBase
{
    private readonly CommandApi _api;
    private bool _isBusy;
    private string _status = "Подготовка рабочего пространства карты.";
    private string _mode = "overview";
    private MapWorkspaceItem0218? _selectedMap;
    private string _mapTitle = "Карта не выбрана";
    private string _mapSubtitle = "Выберите область мира слева.";
    private string _selectedFeatureTitle = "Объект не выбран";
    private string _selectedFeatureDescription = "Выберите объект на карте, чтобы открыть сведения.";
    private string _seed = "eldaris-0218";
    private string _generationScope = "region";
    private string _generationSummary = "Предпросмотр ещё не построен.";
    private string _generationJobId = string.Empty;
    private string _packageSummary = "Экспорт и проверка пакетов доступны для выбранной карты.";
    private string _packagePath = string.Empty;
    private MapVisualFeature0218? _selectedFeature;
    private MapLayerItem0218? _selectedLayer;
    private MapPortalItem0218? _selectedPortal;
    private MapWorkspaceItem0218? _selectedPortalTarget;
    private string _editorFeatureName = string.Empty;
    private string _portalName = "Переход на связанную карту";
    private string _editorSummary = "Выберите объект или создайте новый.";
    private string _playerPreviewSummary = "Предпросмотр игрока ещё не построен.";
    private long _mapRevision;
    private bool _isHexMap;
    private bool _isSchematicMap;
    private string _coordinateNotice = "Локальные координаты";
    private readonly Stack<MapVisualFeature0218> _undo = new();
    private readonly Stack<MapVisualFeature0218> _redo = new();

    public AdminMultiscaleMapViewModel0218(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Initialize);
        OpenSelectedMapCommand = new RelayCommand(OpenSelectedMap);
        ShowOverviewCommand = new RelayCommand(() => Mode = "overview");
        ShowEditorCommand = new RelayCommand(() => Mode = "editor");
        ShowGeneratorCommand = new RelayCommand(() => Mode = "generator");
        ShowPortabilityCommand = new RelayCommand(() => Mode = "portability");
        GeneratePreviewCommand = new RelayCommand(GeneratePreview);
        AcceptGenerationCommand = new RelayCommand(AcceptGeneration);
        ExportCommand = new RelayCommand(ExportMap);
        PickImportPackageCommand = new RelayCommand(PickImportPackage);
        DryRunImportCommand = new RelayCommand(DryRunImport);
        SelectFeatureCommand = new RelayCommand<MapVisualFeature0218>(SelectFeature);
        OpenFantasyCommand = new RelayCommand(() => OpenById("map_north_valley_0218"));
        OpenSettlementCommand = new RelayCommand(() => OpenById("map_greyhaven_0218"));
        OpenDungeonCommand = new RelayCommand(() => OpenById("map_underground_archive_0218"));
        OpenSciFiCommand = new RelayCommand(() => OpenById("map_sector_k12_0218"));
        OpenSystemCommand = new RelayCommand(() => OpenById("map_helios_system_0218"));
        CreatePointCommand = new RelayCommand(() => CreateDraftFeature("Новая точка интереса", "point_of_interest", "point", new[] { new MapPointVm0218(52, 38) }));
        CreatePolylineCommand = new RelayCommand(() => CreateDraftFeature("Новый маршрут", "road", "polyline", new[] { new MapPointVm0218(22, 72), new MapPointVm0218(72, 32) }));
        CreatePolygonCommand = new RelayCommand(() => CreateDraftFeature("Новая область", "area", "polygon", new[] { new MapPointVm0218(58, 58), new MapPointVm0218(76, 58), new MapPointVm0218(76, 76), new MapPointVm0218(58, 76) }));
        CreateLabelCommand = new RelayCommand(() => CreateDraftFeature("Новая подпись", "label", "point", new[] { new MapPointVm0218(42, 18) }));
        MoveSelectedCommand = new RelayCommand(MoveSelected);
        ApplyFeatureNameCommand = new RelayCommand(ApplyFeatureName);
        UndoCommand = new RelayCommand(UndoEditor);
        RedoCommand = new RelayCommand(RedoEditor);
        ValidateCommand = new RelayCommand(ValidateEditor);
        PlayerPreviewCommand = new RelayCommand(OpenPlayerPreview);
        SaveEditorCommand = new RelayCommand(SaveEditor);
        FitCommand = new RelayCommand(() => EditorSummary = "Карта вписана в рабочую область.");
        PanCommand = new RelayCommand(() => EditorSummary = "Режим навигации: перетаскивание карты.");
        SelectCommand = new RelayCommand(() => EditorSummary = "Режим выбора: выберите объект на карте или в инспекторе.");
        NewPortalCommand = new RelayCommand(NewPortal);
        SavePortalCommand = new RelayCommand(SavePortal);
        ToggleLayerVisibilityCommand = new RelayCommand(ToggleLayerVisibility);
        ToggleLayerLockCommand = new RelayCommand(ToggleLayerLock);
    }

    public ObservableCollection<MapWorkspaceItem0218> Maps { get; } = new();
    public ObservableCollection<MapHierarchyItem0218> Hierarchy { get; } = new();
    public ObservableCollection<MapVisualFeature0218> Areas { get; } = new();
    public ObservableCollection<MapVisualFeature0218> Lines { get; } = new();
    public ObservableCollection<MapVisualFeature0218> Points { get; } = new();
    public ObservableCollection<MapLayerItem0218> Layers { get; } = new();
    public ObservableCollection<MapPortalItem0218> Portals { get; } = new();
    public ObservableCollection<MapVisualFeature0218> PreviewFeatures { get; } = new();
    public ObservableCollection<MapVisualFeature0218> EditorFeatures { get; } = new();
    public ObservableCollection<MapHexCell0218> HexCells { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand OpenSelectedMapCommand { get; }
    public ICommand ShowOverviewCommand { get; }
    public ICommand ShowEditorCommand { get; }
    public ICommand ShowGeneratorCommand { get; }
    public ICommand ShowPortabilityCommand { get; }
    public ICommand GeneratePreviewCommand { get; }
    public ICommand AcceptGenerationCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand PickImportPackageCommand { get; }
    public ICommand DryRunImportCommand { get; }
    public ICommand SelectFeatureCommand { get; }
    public ICommand OpenFantasyCommand { get; }
    public ICommand OpenSettlementCommand { get; }
    public ICommand OpenDungeonCommand { get; }
    public ICommand OpenSciFiCommand { get; }
    public ICommand OpenSystemCommand { get; }
    public ICommand CreatePointCommand { get; }
    public ICommand CreatePolylineCommand { get; }
    public ICommand CreatePolygonCommand { get; }
    public ICommand CreateLabelCommand { get; }
    public ICommand MoveSelectedCommand { get; }
    public ICommand ApplyFeatureNameCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand PlayerPreviewCommand { get; }
    public ICommand SaveEditorCommand { get; }
    public ICommand FitCommand { get; }
    public ICommand PanCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand NewPortalCommand { get; }
    public ICommand SavePortalCommand { get; }
    public ICommand ToggleLayerVisibilityCommand { get; }
    public ICommand ToggleLayerLockCommand { get; }

    public bool IsBusy { get => _isBusy; private set { if (_isBusy != value) { _isBusy = value; Notify(); Notify(nameof(IsIdle)); } } }
    public bool IsIdle => !IsBusy;
    public string Status { get => _status; private set { if (_status != value) { _status = value; Notify(); } } }
    public string Mode
    {
        get => _mode;
        private set
        {
            if (_mode == value) return;
            _mode = value;
            Notify(); Notify(nameof(IsOverview)); Notify(nameof(IsEditor)); Notify(nameof(IsGenerator)); Notify(nameof(IsPortability));
        }
    }
    public bool IsOverview => Mode == "overview";
    public bool IsEditor => Mode == "editor";
    public bool IsGenerator => Mode == "generator";
    public bool IsPortability => Mode == "portability";
    public MapWorkspaceItem0218? SelectedMap
    {
        get => _selectedMap;
        set { if (_selectedMap != value) { _selectedMap = value; Notify(); } }
    }
    public string MapTitle { get => _mapTitle; private set { if (_mapTitle != value) { _mapTitle = value; Notify(); } } }
    public string MapSubtitle { get => _mapSubtitle; private set { if (_mapSubtitle != value) { _mapSubtitle = value; Notify(); } } }
    public string SelectedFeatureTitle { get => _selectedFeatureTitle; private set { if (_selectedFeatureTitle != value) { _selectedFeatureTitle = value; Notify(); } } }
    public string SelectedFeatureDescription { get => _selectedFeatureDescription; private set { if (_selectedFeatureDescription != value) { _selectedFeatureDescription = value; Notify(); } } }
    public string Seed { get => _seed; set { if (_seed != value) { _seed = value; Notify(); } } }
    public string GenerationScope { get => _generationScope; set { if (_generationScope != value) { _generationScope = value; Notify(); } } }
    public string GenerationSummary { get => _generationSummary; private set { if (_generationSummary != value) { _generationSummary = value; Notify(); } } }
    public string PackageSummary { get => _packageSummary; private set { if (_packageSummary != value) { _packageSummary = value; Notify(); } } }
    public string PackagePath { get => _packagePath; private set { if (_packagePath != value) { _packagePath = value; Notify(); } } }
    public double CanvasWidth => 820;
    public double CanvasHeight => 540;
    public bool IsWorldMapEnabled => true;
    public MapVisualFeature0218? SelectedFeature { get => _selectedFeature; private set { if (_selectedFeature != value) { _selectedFeature = value; Notify(); } } }
    public MapLayerItem0218? SelectedLayer { get => _selectedLayer; set { if (_selectedLayer != value) { _selectedLayer = value; Notify(); } } }
    public MapPortalItem0218? SelectedPortal { get => _selectedPortal; set { if (_selectedPortal != value) { _selectedPortal = value; PortalName = value?.Name ?? "Переход на связанную карту"; SelectedPortalTarget = Maps.FirstOrDefault(item => item.MapId == value?.TargetMapId); Notify(); } } }
    public MapWorkspaceItem0218? SelectedPortalTarget { get => _selectedPortalTarget; set { if (_selectedPortalTarget != value) { _selectedPortalTarget = value; Notify(); } } }
    public string EditorFeatureName { get => _editorFeatureName; set { if (_editorFeatureName != value) { _editorFeatureName = value; Notify(); } } }
    public string PortalName { get => _portalName; set { if (_portalName != value) { _portalName = value; Notify(); } } }
    public string EditorSummary { get => _editorSummary; private set { if (_editorSummary != value) { _editorSummary = value; Notify(); } } }
    public string PlayerPreviewSummary { get => _playerPreviewSummary; private set { if (_playerPreviewSummary != value) { _playerPreviewSummary = value; Notify(); } } }
    public bool IsHexMap { get => _isHexMap; private set { if (_isHexMap != value) { _isHexMap = value; Notify(); } } }
    public bool IsSchematicMap { get => _isSchematicMap; private set { if (_isSchematicMap != value) { _isSchematicMap = value; Notify(); } } }
    public string CoordinateNotice { get => _coordinateNotice; private set { if (_coordinateNotice != value) { _coordinateNotice = value; Notify(); } } }

    public void RefreshFlags() => Notify(nameof(IsWorldMapEnabled));
    public void RefreshMaps() => Initialize();

    public void Initialize()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var response = _api.WorldAdminMapHierarchyGet0218(new Dictionary<string, object> { { "campaignId", "dev-campaign-core" }, { "ensureFixture", true } });
            EnsureOk(response);
            LoadHierarchy(response.Payload);
            Status = "Мир и карты синхронизированы.";
            OpenById(SelectedMap?.MapId ?? "map_north_valley_0218");
        }
        catch (Exception ex) { Status = "Не удалось загрузить карты: " + ex.Message; }
        finally { IsBusy = false; }
    }

    private void LoadHierarchy(IDictionary<string, object> payload)
    {
        Maps.Clear(); Hierarchy.Clear();
        foreach (var raw in List(payload, "maps"))
        {
            var map = Dict(raw);
            Maps.Add(new MapWorkspaceItem0218
            {
                MapId = Text(map, "mapId"), Name = Text(map, "name"), MapType = Text(map, "mapType"),
                ParentMapId = Text(map, "parentMapId"), PrimaryNodeId = Text(map, "primaryWorldEntityId")
            });
        }
        var nodes = List(payload, "nodes").Select(Dict).ToList();
        var byId = nodes.ToDictionary(item => Text(item, "nodeId"), StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var id = Text(node, "nodeId");
            var depth = 0; var cursor = Text(node, "parentId");
            while (!string.IsNullOrWhiteSpace(cursor) && byId.TryGetValue(cursor, out var parent) && depth < 12) { depth++; cursor = Text(parent, "parentId"); }
            var map = Maps.FirstOrDefault(item => item.PrimaryNodeId == id);
            Hierarchy.Add(new MapHierarchyItem0218 { NodeId = id, MapId = map?.MapId ?? string.Empty, Name = Text(node, "name"), Type = Text(node, "nodeType"), Depth = depth });
        }
        SelectedMap = Maps.FirstOrDefault(item => item.MapId == "map_north_valley_0218") ?? Maps.FirstOrDefault();
    }

    private void OpenSelectedMap()
    {
        if (SelectedMap != null) OpenById(SelectedMap.MapId);
    }

    private void OpenById(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId)) return;
        var response = _api.WorldAdminMapGet0218(new Dictionary<string, object> { { "mapId", mapId } });
        EnsureOk(response);
        ApplyMap(response.Payload);
        SelectedMap = Maps.FirstOrDefault(item => item.MapId == mapId) ?? SelectedMap;
            Status = "Открыта карта «" + MapTitle + "».";
    }

    private void ApplyMap(IDictionary<string, object> payload)
    {
        MapTitle = Text(payload, "name");
        MapSubtitle = MapTypeLabel(Text(payload, "mapType")) + " · " + SizeLabel(payload);
        CoordinateNotice = Text(payload, "coordinateProfileKind");
        IsHexMap = CoordinateNotice == "Гексагональная сетка";
        IsSchematicMap = CoordinateNotice == "Схематическая карта";
        BuildHexCells();
        _mapRevision = Long(payload, "revision");
        _undo.Clear(); _redo.Clear(); SelectedFeature = null;
        Areas.Clear(); Lines.Clear(); Points.Clear(); Layers.Clear(); Portals.Clear(); EditorFeatures.Clear();
        foreach (var raw in List(payload, "features")) AddFeature(ParseFeature(Dict(raw)), false);
        foreach (var raw in List(payload, "layers"))
        {
            var layer = Dict(raw);
            Layers.Add(new MapLayerItem0218 { LayerId = Text(layer, "layerId"), Name = Text(layer, "name"), Kind = Text(layer, "layerKind"), VisibleToPlayers = Bool(layer, "visibleToPlayers"), IsLocked = Bool(layer, "isLocked"), Revision = Long(layer, "revision") });
        }
        foreach (var raw in List(payload, "portals"))
        {
            var portal = Dict(raw);
            Portals.Add(new MapPortalItem0218 { PortalId = Text(portal, "portalId"), Name = Text(portal, "name"), TargetMapId = Text(portal, "targetMapId"), IsSecret = Bool(portal, "isSecret"), IsPlayerVisible = Bool(portal, "isPlayerVisible"), Revision = Long(portal, "revision") });
        }
        SelectedLayer = Layers.FirstOrDefault();
        SelectedPortal = Portals.FirstOrDefault();
        SelectedPortalTarget ??= Maps.FirstOrDefault(item => item.MapId != SelectedMap?.MapId);
        EditorSummary = "Карта загружена. Изменения отсутствуют.";
    }

    private void AddFeature(MapVisualFeature0218 feature, bool preview)
    {
        if (preview) PreviewFeatures.Add(feature);
        else
        {
            EditorFeatures.Add(feature);
            if (feature.GeometryKind == "polygon") Areas.Add(feature);
            else if (feature.GeometryKind == "polyline") Lines.Add(feature);
            else Points.Add(feature);
        }
    }

    private MapVisualFeature0218 ParseFeature(IDictionary<string, object> raw)
    {
        var points = List(raw, "points").Select(item => Dict(item)).Select(item => new MapPointVm0218(Number(item, "x"), Number(item, "y"))).ToList();
        if (points.Count == 0) points.Add(new MapPointVm0218(50, 50));
        var minX = points.Min(item => item.X); var maxX = points.Max(item => item.X);
        var minY = points.Min(item => item.Y); var maxY = points.Max(item => item.Y);
        return new MapVisualFeature0218
        {
            FeatureId = Text(raw, "featureId"), Name = Text(raw, "name"), SemanticKind = Text(raw, "semanticKind"), GeometryKind = Text(raw, "geometryKind"),
            Description = Text(raw, "publicDescription"), IsManual = Bool(raw, "isManual"), IsSecret = Bool(raw, "isSecret"),
            LayerId = Text(raw, "layerId"), IsPlayerVisible = Bool(raw, "isPlayerVisible"), Revision = Long(raw, "revision"), Points = points,
            X = minX / 100d * CanvasWidth, Y = minY / 100d * CanvasHeight,
            X2 = points.Last().X / 100d * CanvasWidth, Y2 = points.Last().Y / 100d * CanvasHeight,
            Width = Math.Max(18, (maxX - minX) / 100d * CanvasWidth), Height = Math.Max(18, (maxY - minY) / 100d * CanvasHeight),
                Fill = FeatureFill(Text(raw, "semanticKind"), Bool(raw, "isManual")), Badge = Bool(raw, "isManual") ? "Вручную" : "Создано"
        };
    }

    private void GeneratePreview()
    {
        if (SelectedMap == null) return;
        try
        {
            var response = _api.WorldAdminMapGeneratePreview0218(new Dictionary<string, object> { { "mapId", SelectedMap.MapId }, { "scope", GenerationScope }, { "seed", Seed } });
            EnsureOk(response);
            _generationJobId = Text(response.Payload, "jobId");
            PreviewFeatures.Clear();
            foreach (var raw in List(response.Payload, "features")) AddFeature(ParseFeature(Dict(raw)), true);
            var diff = response.Payload.TryGetValue("diff", out var diffRaw) ? Dict(diffRaw) : new Dictionary<string, object>();
            GenerationSummary = $"Создано: {Int(diff, "added")} · Изменено: {Int(diff, "changed")} · Удалено: {Int(diff, "removedGenerated")} · " +
                $"Ручные объекты сохранены: {Int(diff, "manualRetained")} · Изменённые вручную сгенерированные объекты требуют решения: {Int(diff, "modifiedGeneratedConflict")}. Карта не изменена.";
            Mode = "generator";
        }
        catch (Exception ex) { GenerationSummary = "Ошибка генерации: " + ex.Message; }
    }

    private void AcceptGeneration()
    {
        if (string.IsNullOrWhiteSpace(_generationJobId)) { GenerationSummary = "Сначала создайте предпросмотр."; return; }
        try
        {
            var response = _api.WorldAdminMapGenerateAccept0218(new Dictionary<string, object> { { "jobId", _generationJobId } });
            EnsureOk(response);
            GenerationSummary = "Результат принят. Ручные объекты сохранены.";
            if (SelectedMap != null) OpenById(SelectedMap.MapId);
        }
        catch (Exception ex) { GenerationSummary = "Не удалось принять результат: " + ex.Message; }
    }

    private void ExportMap()
    {
        if (SelectedMap == null) return;
        try
        {
            var response = _api.WorldAdminMapExport0218(new Dictionary<string, object> { { "mapId", SelectedMap.MapId } });
            EnsureOk(response);
            PackagePath = Text(response.Payload, "path");
            PackageSummary = $"Экспортированы пакет «{Text(response.Payload, "fileName")}», PNG и семантический SVG. Объектов: {Int(response.Payload, "featureCount")}.";
            Mode = "portability";
        }
        catch (Exception ex) { PackageSummary = "Ошибка экспорта: " + ex.Message; }
    }

    private void PickImportPackage()
    {
        var dialog = new OpenFileDialog { Title = "Выберите пакет карты", Filter = "Пакеты карт (*.nrimap)|*.nrimap", CheckFileExists = true };
        if (dialog.ShowDialog() == true) { PackagePath = dialog.FileName; PackageSummary = "Выбран пакет: " + dialog.SafeFileName; }
    }

    private void DryRunImport()
    {
        if (string.IsNullOrWhiteSpace(PackagePath)) { PackageSummary = "Сначала выберите пакет карты."; return; }
        try
        {
            var response = _api.WorldAdminMapImportDryRun0218(new Dictionary<string, object> { { "path", PackagePath } });
            EnsureOk(response);
            PackageSummary = Bool(response.Payload, "valid") ? "Dry-run PASS: пакет проверен, записи в базу не выполнялись." : "Dry-run обнаружил ошибки пакета.";
        }
        catch (Exception ex) { PackageSummary = "Dry-run не выполнен: " + ex.Message; }
    }

    private void SelectFeature(MapVisualFeature0218? feature)
    {
        if (feature == null) return;
        SelectedFeature = feature;
        EditorFeatureName = feature.Name;
        SelectedLayer = Layers.FirstOrDefault(layer => layer.LayerId == feature.LayerId) ?? SelectedLayer;
        SelectedFeatureTitle = feature.Name;
        SelectedFeatureDescription = $"{SemanticLabel(feature.SemanticKind)} · {feature.Badge}. {feature.Description}";
        EditorSummary = "Выбран объект «" + feature.Name + "».";
    }

    private IEnumerable<MapVisualFeature0218> Features() => Areas.Concat(Lines).Concat(Points);

    private void CreateDraftFeature(string name, string kind, string geometry, IEnumerable<MapPointVm0218> points)
    {
        var feature = new MapVisualFeature0218
        {
            Name = name, SemanticKind = kind, GeometryKind = geometry, Description = "Ручной объект карты.",
            IsManual = true, IsPlayerVisible = true, IsDirty = true, LayerId = SelectedLayer?.LayerId ?? Layers.FirstOrDefault()?.LayerId ?? string.Empty,
            Points = points.ToList(), Badge = "Новый · не сохранён", Fill = FeatureFill(kind, true)
        };
        UpdatePlacement(feature); AddFeature(feature, false); SelectFeature(feature);
        EditorSummary = "Создан черновик «" + name + "». Сохраните карту, чтобы записать его на сервере.";
    }

    private void MoveSelected()
    {
        if (SelectedFeature == null) { EditorSummary = "Сначала выберите объект."; return; }
        PushUndo();
        SelectedFeature.Points = SelectedFeature.Points.Select(point => new MapPointVm0218(Math.Min(98, point.X + 3), Math.Min(98, point.Y + 2))).ToList();
        SelectedFeature.IsDirty = true; UpdatePlacement(SelectedFeature); RefreshVisual(SelectedFeature);
        EditorSummary = "Объект перемещён. Изменение ещё не сохранено.";
    }

    private void ApplyFeatureName()
    {
        if (SelectedFeature == null || string.IsNullOrWhiteSpace(EditorFeatureName)) return;
        PushUndo(); SelectedFeature.Name = EditorFeatureName.Trim(); SelectedFeature.IsDirty = true; RefreshVisual(SelectedFeature);
        SelectedFeatureTitle = SelectedFeature.Name; EditorSummary = "Название изменено. Изменение ещё не сохранено.";
    }

    private void PushUndo()
    {
        if (SelectedFeature == null) return;
        _undo.Push(SelectedFeature.Clone()); _redo.Clear();
    }

    private void UndoEditor()
    {
        if (SelectedFeature == null || _undo.Count == 0) { EditorSummary = "Нет изменений для отмены."; return; }
        _redo.Push(SelectedFeature.Clone()); ApplySnapshot(_undo.Pop()); EditorSummary = "Последнее изменение отменено.";
    }

    private void RedoEditor()
    {
        if (SelectedFeature == null || _redo.Count == 0) { EditorSummary = "Нет изменений для повтора."; return; }
        _undo.Push(SelectedFeature.Clone()); ApplySnapshot(_redo.Pop()); EditorSummary = "Изменение повторено.";
    }

    private void ApplySnapshot(MapVisualFeature0218 snapshot)
    {
        if (SelectedFeature == null) return;
        SelectedFeature.CopyFrom(snapshot); SelectedFeature.IsDirty = true; UpdatePlacement(SelectedFeature); RefreshVisual(SelectedFeature); SelectFeature(SelectedFeature);
    }

    private void RefreshVisual(MapVisualFeature0218 feature)
    {
        if (Areas.Remove(feature)) Areas.Add(feature); else if (Lines.Remove(feature)) Lines.Add(feature); else if (Points.Remove(feature)) Points.Add(feature);
        if (EditorFeatures.Remove(feature)) EditorFeatures.Add(feature);
    }

    private void UpdatePlacement(MapVisualFeature0218 feature)
    {
        if (feature.Points.Count == 0) feature.Points.Add(new MapPointVm0218(50, 50));
        var minX = feature.Points.Min(item => item.X); var maxX = feature.Points.Max(item => item.X);
        var minY = feature.Points.Min(item => item.Y); var maxY = feature.Points.Max(item => item.Y);
        feature.X = minX / 100d * CanvasWidth; feature.Y = minY / 100d * CanvasHeight;
        feature.X2 = feature.Points.Last().X / 100d * CanvasWidth; feature.Y2 = feature.Points.Last().Y / 100d * CanvasHeight;
        feature.Width = Math.Max(18, (maxX - minX) / 100d * CanvasWidth); feature.Height = Math.Max(18, (maxY - minY) / 100d * CanvasHeight);
    }

    private void BuildHexCells()
    {
        HexCells.Clear();
        if (!IsHexMap) return;
        const double width = 92; const double height = 80;
        for (var r = -2; r <= 2; r++)
        for (var q = -3; q <= 3; q++)
            HexCells.Add(new MapHexCell0218 { Q = q, R = r, X = 325 + q * 69 + r * 34.5, Y = 225 + r * 60, Width = width, Height = height });
    }

    private void ValidateEditor()
    {
        if (SelectedMap == null) return;
        try { var response = _api.WorldAdminMapValidate0218(new Dictionary<string, object> { { "mapId", SelectedMap.MapId } }); EnsureOk(response); EditorSummary = Bool(response.Payload, "valid") ? "Проверка пройдена: ошибок не найдено." : "Проверка завершена с предупреждениями."; }
        catch (Exception ex) { EditorSummary = "Проверка не выполнена: " + ex.Message; }
    }

    private void OpenPlayerPreview()
    {
        if (SelectedMap == null) return;
        try { var response = _api.WorldAdminMapPlayerPreview0218(new Dictionary<string, object> { { "mapId", SelectedMap.MapId } }); EnsureOk(response); PlayerPreviewSummary = "Предпросмотр игрока: видимых объектов " + List(response.Payload, "features").Count + ", переходов " + List(response.Payload, "portals").Count + "."; EditorSummary = PlayerPreviewSummary; }
        catch (Exception ex) { PlayerPreviewSummary = "Предпросмотр недоступен: " + ex.Message; EditorSummary = PlayerPreviewSummary; }
    }

    private void SaveEditor()
    {
        if (SelectedMap == null) return;
        try
        {
            foreach (var feature in Features().Where(item => item.IsDirty).ToList())
            {
                if (feature == SelectedFeature && SelectedLayer != null) feature.LayerId = SelectedLayer.LayerId;
                var payload = FeaturePayload(feature);
                var response = string.IsNullOrWhiteSpace(feature.FeatureId) ? _api.WorldAdminMapFeatureCreate0218(payload) : _api.WorldAdminMapFeatureUpdate0218(payload);
                EnsureOk(response);
            }
            var mapResponse = _api.WorldAdminMapUpdate0218(new Dictionary<string, object> { { "mapId", SelectedMap.MapId }, { "expectedRevision", _mapRevision }, { "name", MapTitle } });
            EnsureOk(mapResponse); EditorSummary = "Изменения сохранены. Ревизия карты обновлена."; OpenById(SelectedMap.MapId);
        }
        catch (Exception ex) { EditorSummary = "Не удалось сохранить карту: " + ex.Message; }
    }

    private Dictionary<string, object> FeaturePayload(MapVisualFeature0218 feature)
    {
        var payload = new Dictionary<string, object>
        {
            { "mapId", SelectedMap?.MapId ?? string.Empty }, { "layerId", feature.LayerId }, { "name", feature.Name },
            { "semanticKind", feature.SemanticKind }, { "geometryKind", feature.GeometryKind }, { "publicDescription", feature.Description },
            { "isPlayerVisible", feature.IsPlayerVisible }, { "isSecret", feature.IsSecret },
            { "points", feature.Points.Select(point => (object)new Dictionary<string, object> { { "x", point.X }, { "y", point.Y } }).ToArray() }
        };
        if (!string.IsNullOrWhiteSpace(feature.FeatureId)) { payload["featureId"] = feature.FeatureId; payload["expectedRevision"] = feature.Revision; }
        return payload;
    }

    private void NewPortal()
    {
        SelectedPortal = null; PortalName = "Переход на связанную карту";
        SelectedPortalTarget = Maps.FirstOrDefault(item => item.MapId != SelectedMap?.MapId);
        EditorSummary = "Новый переход подготовлен. Выберите целевую карту и сохраните переход.";
    }

    private void SavePortal()
    {
        if (SelectedMap == null || SelectedPortalTarget == null || string.IsNullOrWhiteSpace(PortalName)) { EditorSummary = "Выберите целевую карту и укажите название перехода."; return; }
        try
        {
            var payload = new Dictionary<string, object> { { "sourceMapId", SelectedMap.MapId }, { "targetMapId", SelectedPortalTarget.MapId }, { "name", PortalName.Trim() }, { "isPlayerVisible", true }, { "isSecret", false } };
            ResponseEnvelope response;
            if (SelectedPortal == null) response = _api.WorldAdminMapPortalCreate0218(payload);
            else { payload["portalId"] = SelectedPortal.PortalId; payload["expectedRevision"] = SelectedPortal.Revision; response = _api.WorldAdminMapPortalUpdate0218(payload); }
            EnsureOk(response); EditorSummary = "Переход сохранён."; OpenById(SelectedMap.MapId);
        }
        catch (Exception ex) { EditorSummary = "Не удалось сохранить переход: " + ex.Message; }
    }

    private void ToggleLayerVisibility() => UpdateLayer(true);
    private void ToggleLayerLock() => UpdateLayer(false);
    private void UpdateLayer(bool visibility)
    {
        if (SelectedLayer == null) { EditorSummary = "Выберите слой."; return; }
        try
        {
            var payload = new Dictionary<string, object> { { "layerId", SelectedLayer.LayerId }, { "expectedRevision", SelectedLayer.Revision } };
            if (visibility) payload["visibleToPlayers"] = !SelectedLayer.VisibleToPlayers; else payload["isLocked"] = !SelectedLayer.IsLocked;
            var response = _api.WorldAdminMapLayerUpdate0218(payload); EnsureOk(response); EditorSummary = visibility ? "Видимость слоя изменена." : "Блокировка слоя изменена.";
            if (SelectedMap != null) OpenById(SelectedMap.MapId);
        }
        catch (Exception ex) { EditorSummary = "Не удалось изменить слой: " + ex.Message; }
    }

    private static void EnsureOk(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? "Сервер отклонил операцию." : response.Message);
    }

    internal static IList<object> List(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return Array.Empty<object>();
        if (value is IList<object> typed) return typed;
        if (value is IEnumerable enumerable && !(value is string)) return enumerable.Cast<object>().ToList();
        return Array.Empty<object>();
    }
    internal static Dictionary<string, object> Dict(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary<string, object> generic) return new Dictionary<string, object>(generic);
        if (value is IDictionary legacy) return legacy.Keys.Cast<object>().ToDictionary(key => Convert.ToString(key) ?? string.Empty, key => legacy[key]!);
        return new Dictionary<string, object>();
    }
    internal static string Text(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty : string.Empty;
    internal static bool Bool(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    internal static int Int(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
    internal static long Long(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : 0L;
    internal static double Number(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : 0;
    private static string SizeLabel(IDictionary<string, object> map) => $"{Int(map, "widthMeters") / 1000d:0.#} × {Int(map, "heightMeters") / 1000d:0.#} км";
    private static string MapTypeLabel(string value) => value switch { "world" => "Мир", "region" => "Регион", "settlement" => "Поселение", "district" => "Район", "interior" => "Интерьер", "dungeon" => "Подземелье", "galaxy" => "Галактика", "sector" => "Сектор", "star_system" => "Звёздная система", "planet" => "Планета", "orbital" => "Орбитальный объект", _ => "Карта" };
    private static string SemanticLabel(string value) => value switch { "road" => "Дорога", "river" => "Река", "border" => "Граница", "area" => "Область", "district" => "Район", "room" => "Помещение", "structure" => "Строение", "entrance" => "Вход", "stairs" => "Лестница", "label" => "Подпись", "star" => "Звезда", "planet" => "Планета", "station" => "Станция", "secret" => "Тайна", _ => "Точка интереса" };
    private static string FeatureFill(string kind, bool manual) => kind switch { "river" => "#2563A8", "road" => "#B58955", "secret" => "#7C3AED", "star" => "#F6C453", "planet" => "#2FA38A", "station" => "#A7B1C2", "district" => "#345A78", "room" => "#4E5D6C", "structure" => "#64748B", "entrance" => "#E59E3A", "stairs" => "#D97706", "label" => "#256D5A", _ => manual ? "#2D6A4F" : "#334E68" };
}

public sealed class MapWorkspaceItem0218 { public string MapId { get; set; } = ""; public string Name { get; set; } = ""; public string MapType { get; set; } = ""; public string ParentMapId { get; set; } = ""; public string PrimaryNodeId { get; set; } = ""; public string Label => Name + " · " + MapType; public override string ToString() => Name; }
public sealed class MapHierarchyItem0218 { public string NodeId { get; set; } = ""; public string MapId { get; set; } = ""; public string Name { get; set; } = ""; public string Type { get; set; } = ""; public int Depth { get; set; } public string Label => new string(' ', Depth * 3) + (Depth == 0 ? "● " : "└ ") + Name; public override string ToString() => Label; }
public sealed class MapLayerItem0218 { public string LayerId { get; set; } = ""; public string Name { get; set; } = ""; public string Kind { get; set; } = ""; public bool VisibleToPlayers { get; set; } public bool IsLocked { get; set; } public long Revision { get; set; } public string Label => Name + (VisibleToPlayers ? " · игрокам" : " · только GM") + (IsLocked ? " · заблокирован" : ""); public override string ToString() => Label; }
public sealed class MapPortalItem0218 { public string PortalId { get; set; } = ""; public string Name { get; set; } = ""; public string TargetMapId { get; set; } = ""; public bool IsSecret { get; set; } public bool IsPlayerVisible { get; set; } public long Revision { get; set; } public string Label => Name + (IsSecret ? " · секретный" : ""); public override string ToString() => Label; }
public sealed class MapPointVm0218 { public MapPointVm0218(double x, double y) { X = x; Y = y; } public double X { get; } public double Y { get; } }
public sealed class MapHexCell0218 { public int Q { get; set; } public int R { get; set; } public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Height { get; set; } public string Points => "46,0 92,20 92,60 46,80 0,60 0,20"; public string Label => $"q {Q} · r {R}"; }
public sealed class MapVisualFeature0218
{
    public string FeatureId { get; set; } = ""; public string Name { get; set; } = ""; public string SemanticKind { get; set; } = ""; public string GeometryKind { get; set; } = "";
    public string LayerId { get; set; } = ""; public string Description { get; set; } = ""; public string Badge { get; set; } = ""; public bool IsManual { get; set; } public bool IsSecret { get; set; } public bool IsPlayerVisible { get; set; } public bool IsDirty { get; set; } public long Revision { get; set; }
    public List<MapPointVm0218> Points { get; set; } = new();
    public double X { get; set; } public double Y { get; set; } public double X2 { get; set; } public double Y2 { get; set; } public double Width { get; set; } public double Height { get; set; }
    public string Fill { get; set; } = "#334E68";
    public MapVisualFeature0218 Clone() => new MapVisualFeature0218 { FeatureId = FeatureId, Name = Name, SemanticKind = SemanticKind, GeometryKind = GeometryKind, LayerId = LayerId, Description = Description, Badge = Badge, IsManual = IsManual, IsSecret = IsSecret, IsPlayerVisible = IsPlayerVisible, IsDirty = IsDirty, Revision = Revision, Points = Points.Select(point => new MapPointVm0218(point.X, point.Y)).ToList(), X = X, Y = Y, X2 = X2, Y2 = Y2, Width = Width, Height = Height, Fill = Fill };
    public void CopyFrom(MapVisualFeature0218 other) { Name = other.Name; SemanticKind = other.SemanticKind; GeometryKind = other.GeometryKind; LayerId = other.LayerId; Description = other.Description; Badge = other.Badge; IsManual = other.IsManual; IsSecret = other.IsSecret; IsPlayerVisible = other.IsPlayerVisible; Revision = other.Revision; Points = other.Points.Select(point => new MapPointVm0218(point.X, point.Y)).ToList(); Fill = other.Fill; }
}
