using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminLocationGeneratorViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private LocationGeneratorPresetUiItem? _selectedPreset;
    private LocationGeneratorTemplateUiItem? _selectedTemplate;
    private LocationGeneratorMapUiItem? _selectedMap;
    private string _campaignId = "dev-campaign-core";
    private string _ruleSetId = "fantasy_nri_default";
    private string _displayName = "Сгенерированная локация";
    private string _seed = "0165-market";
    private int _widthMeters = 200;
    private int _heightMeters = 200;
    private double _tileSizeMeters = 5;
    private double _gridSizeMeters = 5;
    private string _density = "Medium";
    private string _detailLevel = "Normal";
    private string _symmetry = "None";
    private bool _includeGmSecrets;
    private bool _includeHazards = true;
    private bool _includeSpawnZones = true;
    private bool _includeObjectiveZones = true;
    private bool _setActiveForSession = true;
    private bool _isBusy;
    private string _statusMessage = "Выберите пресет и создайте preview.";
    private string _errorMessage = string.Empty;
    private string _resultSummary = "Preview ещё не создан.";
    private string _lastRunId = string.Empty;
    private string _lastMapId = string.Empty;
    private string _lastHash = string.Empty;
    private string _previewId = string.Empty;
    private long _mapRevision;
    private string _previewStateText = "Предпросмотр ещё не создан.";
    private string _lastAppliedRunText = "Результаты генерации ещё не применялись.";
    private string _currentMapIdForTemplate = string.Empty;
    private string _templateNameInput = "Шаблон из текущей карты";
    private double _previewCanvasWidth = 760;
    private double _previewCanvasHeight = 500;

    public AdminLocationGeneratorViewModel(CommandApi api)
    {
        _api = api;
        _displayName = "Сгенерированная локация";
        _statusMessage = "Выберите пресет и создайте preview.";
        _resultSummary = "Preview ещё не создан.";
        _templateNameInput = "Шаблон из текущей карты";
        RefreshCommand = new RelayCommand(Refresh);
        PreviewCommand = new RelayCommand(Preview);
        RegenerateCommand = new RelayCommand(Regenerate);
        SaveAsSceneMapCommand = new RelayCommand(SaveAsSceneMap);
        SaveAndSetActiveCommand = new RelayCommand(SaveAndSetActive);
        CreateTemplateFromCurrentMapCommand = new RelayCommand(CreateTemplateFromCurrentMap);
        ArchiveTemplateCommand = new RelayCommand(ArchiveSelectedTemplate);
        ApplyPreviewCommand = new RelayCommand(ApplyPreview);
        CancelPreviewCommand = new RelayCommand(CancelPreview);
    }

    public ObservableCollection<LocationGeneratorMapUiItem> Maps { get; } = new();
    public ObservableCollection<LocationGeneratorPresetUiItem> Presets { get; } = new();
    public ObservableCollection<LocationGeneratorTemplateUiItem> Templates { get; } = new();
    public ObservableCollection<LocationGeneratorTilePreviewItem> PreviewTiles { get; } = new();
    public ObservableCollection<LocationGeneratorAssetPreviewItem> PreviewAssets { get; } = new();
    public ObservableCollection<LocationGeneratorMarkerPreviewItem> PreviewMarkers { get; } = new();
    public ObservableCollection<string> PreviewWarnings { get; } = new();

    public LocationGeneratorOptionUiItem[] DensityOptions { get; } =
    {
        new("Low", "Низкая"), new("Medium", "Средняя"), new("High", "Высокая")
    };
    public LocationGeneratorOptionUiItem[] DetailLevelOptions { get; } =
    {
        new("Basic", "Базовая"), new("Normal", "Обычная"), new("Rich", "Подробная")
    };
    public LocationGeneratorOptionUiItem[] SymmetryOptions { get; } =
    {
        new("None", "Свободная"), new("Loose", "Сбалансированная"), new("Structured", "Строгая")
    };

    public ICommand RefreshCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand RegenerateCommand { get; }
    public ICommand SaveAsSceneMapCommand { get; }
    public ICommand SaveAndSetActiveCommand { get; }
    public ICommand CreateTemplateFromCurrentMapCommand { get; }
    public ICommand ArchiveTemplateCommand { get; }
    public ICommand ApplyPreviewCommand { get; }
    public ICommand CancelPreviewCommand { get; }

    public LocationGeneratorMapUiItem? SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (_selectedMap == value) return;
            _selectedMap = value;
            Notify();
            Notify(nameof(SelectedMapSummary));
            ClearPreviewState();
            LoadSelectedMapState();
            NotifyCommandState();
        }
    }

    public LocationGeneratorPresetUiItem? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (_selectedPreset == value) return;
            _selectedPreset = value;
            Notify();
            Notify(nameof(CleanSelectedPresetSummary));
            ApplyPreset(value);
        }
    }

    public LocationGeneratorTemplateUiItem? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (_selectedTemplate == value) return;
            _selectedTemplate = value;
            Notify();
            Notify(nameof(CleanSelectedTemplateSummary));
        }
    }

    public string CampaignId { get => _campaignId; set => Set(ref _campaignId, value); }
    public string RuleSetId { get => _ruleSetId; set => Set(ref _ruleSetId, value); }
    public string DisplayName { get => _displayName; set => Set(ref _displayName, value); }
    public string Seed { get => _seed; set => Set(ref _seed, value); }
    public int WidthMeters { get => _widthMeters; set => Set(ref _widthMeters, value); }
    public int HeightMeters { get => _heightMeters; set => Set(ref _heightMeters, value); }
    public double TileSizeMeters { get => _tileSizeMeters; set => Set(ref _tileSizeMeters, value); }
    public double GridSizeMeters { get => _gridSizeMeters; set => Set(ref _gridSizeMeters, value); }
    public string Density { get => _density; set => Set(ref _density, value); }
    public string DetailLevel { get => _detailLevel; set => Set(ref _detailLevel, value); }
    public string Symmetry { get => _symmetry; set => Set(ref _symmetry, value); }
    public bool IncludeGmSecrets { get => _includeGmSecrets; set => Set(ref _includeGmSecrets, value); }
    public bool IncludeHazards { get => _includeHazards; set => Set(ref _includeHazards, value); }
    public bool IncludeSpawnZones { get => _includeSpawnZones; set => Set(ref _includeSpawnZones, value); }
    public bool IncludeObjectiveZones { get => _includeObjectiveZones; set => Set(ref _includeObjectiveZones, value); }
    public bool SetActiveForSession { get => _setActiveForSession; set => Set(ref _setActiveForSession, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (Set(ref _isBusy, value)) NotifyCommandState(); }
    }
    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => Set(ref _errorMessage, value); }
    public string ResultSummary { get => _resultSummary; private set => Set(ref _resultSummary, value); }
    public string LastRunId { get => _lastRunId; private set => Set(ref _lastRunId, value); }
    public string LastMapId { get => _lastMapId; private set => Set(ref _lastMapId, value); }
    public string LastHash { get => _lastHash; private set => Set(ref _lastHash, value); }
    public string CurrentMapIdForTemplate { get => _currentMapIdForTemplate; set => Set(ref _currentMapIdForTemplate, value); }
    public string TemplateNameInput { get => _templateNameInput; set => Set(ref _templateNameInput, value); }
    public double PreviewCanvasWidth { get => _previewCanvasWidth; private set => Set(ref _previewCanvasWidth, value); }
    public double PreviewCanvasHeight { get => _previewCanvasHeight; private set => Set(ref _previewCanvasHeight, value); }
    public string PreviewStateText { get => _previewStateText; private set => Set(ref _previewStateText, value); }
    public string LastAppliedRunText { get => _lastAppliedRunText; private set => Set(ref _lastAppliedRunText, value); }
    public bool IsIdle => !IsBusy;
    public bool HasPreview => !string.IsNullOrWhiteSpace(_previewId);
    public bool CanCreatePreview => IsIdle && SelectedMap != null && SelectedPreset != null;
    public bool CanApplyPreview => IsIdle && HasPreview && SelectedMap != null;
    public string SelectedMapSummary => SelectedMap == null
        ? "Карта не выбрана."
        : $"{SelectedMap.DisplayName}: {SelectedMap.WidthMeters}×{SelectedMap.HeightMeters} м, сетка {SelectedMap.GridCellSizeMeters} м";

    public string SelectedPresetSummary => SelectedPreset == null
        ? "Пресет не выбран."
        : $"{SelectedPreset.DisplayName}: {SelectedPreset.LocationKind}, {SelectedPreset.MapScale}, {SelectedPreset.DefaultWidthMeters}x{SelectedPreset.DefaultHeightMeters} м";

    public string SelectedTemplateSummary => SelectedTemplate == null
        ? "Шаблон не выбран."
        : $"{SelectedTemplate.DisplayName}: {SelectedTemplate.WidthMeters}x{SelectedTemplate.HeightMeters} м, объектов {SelectedTemplate.AssetInstanceCount}";

    public string CleanSelectedPresetSummary => SelectedPreset == null
        ? "Пресет не выбран."
        : $"{SelectedPreset.DisplayName}: {SelectedPreset.LocationKind}, {SelectedPreset.MapScale}, {SelectedPreset.DefaultWidthMeters}x{SelectedPreset.DefaultHeightMeters} м";

    public string CleanSelectedTemplateSummary => SelectedTemplate == null
        ? "Шаблон не выбран."
        : $"{SelectedTemplate.DisplayName}: {SelectedTemplate.WidthMeters}x{SelectedTemplate.HeightMeters} м, объектов {SelectedTemplate.AssetInstanceCount}";

    public void LoadIfNeeded()
    {
        if (Presets.Count == 0 && !IsBusy)
            Refresh();
    }

    private void Refresh()
    {
        RunClientAction(() =>
        {
            var payload = BasePayload();
            var maps = _api.MapSceneList(new Dictionary<string, object>
            {
                ["campaignId"] = CampaignId,
                ["includeArchived"] = false
            });
            EnsureOk(maps, "Не удалось загрузить карты сцены.");
            ReplaceMaps(GetArray(maps.Payload, "items", "maps"));

            var presets = _api.SceneMapGeneratorAdminListPresets(payload);
            EnsureOk(presets, "Не удалось загрузить пресеты генератора.");
            ReplacePresets(GetArray(presets.Payload, "items", "presets"));

            var templates = _api.SceneMapGeneratorAdminListTemplates(payload);
            EnsureOk(templates, "Не удалось загрузить шаблоны генератора.");
            ReplaceTemplates(GetArray(templates.Payload, "items", "templates"));

            if (SelectedPreset == null && Presets.Count > 0)
                SelectedPreset = Presets[0];
            if (SelectedMap == null && Maps.Count > 0)
                SelectedMap = Maps[0];

            StatusMessage = "Пресеты и шаблоны генератора загружены.";
        });
    }

    private void Preview()
    {
        RunClientAction(() =>
        {
            if (SelectedMap == null)
                throw new InvalidOperationException("Выберите карту для предпросмотра.");
            var response = _api.SceneMapGeneratorAdminPreview(BuildGenerationPayload());
            EnsureOk(response, "Preview генератора не создан.");
            ApplyGenerationPayload(response.Payload, "Preview создан.");
        });
    }

    private void ApplyPreview()
    {
        RunClientAction(() =>
        {
            if (SelectedMap == null || string.IsNullOrWhiteSpace(_previewId))
                throw new InvalidOperationException("Сначала создайте предпросмотр для выбранной карты.");
            var response = _api.SceneMapGeneratorAdminSavePreviewAsSceneMap(new Dictionary<string, object>
            {
                ["previewId"] = _previewId,
                ["operationId"] = "admin-map-generation-" + Guid.NewGuid().ToString("N"),
                ["mapId"] = SelectedMap.MapId,
                ["expectedMapRevision"] = _mapRevision,
                ["previewFingerprint"] = LastHash
            });
            EnsureOk(response, "Не удалось применить предпросмотр к карте.");
            _mapRevision = GetLong(response.Payload, "mapRevision", _mapRevision);
            LastAppliedRunText = "Последний результат применён к карте «" + SelectedMap.DisplayName + "».";
            ApplyGenerationPayload(response.Payload, "Результат применён.");
            ClearPreviewState(keepCanvas: true);
        });
    }

    private void CancelPreview()
    {
        RunClientAction(() =>
        {
            if (string.IsNullOrWhiteSpace(_previewId)) return;
            var response = _api.SceneMapGeneratorAdminCancelPreview(new Dictionary<string, object> { ["previewId"] = _previewId });
            EnsureOk(response, "Не удалось отменить предпросмотр.");
            ClearPreviewState();
            StatusMessage = "Предпросмотр отменён.";
        });
    }

    private void Regenerate()
    {
        Seed = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        RunClientAction(() =>
        {
            var response = _api.SceneMapGeneratorAdminRegenerate(BuildGenerationPayload());
            EnsureOk(response, "Preview генератора не пересоздан.");
            ApplyGenerationPayload(response.Payload, "Preview пересоздан с новым seed.");
        });
    }

    private void SaveAsSceneMap() => ApplyPreview();

    private void SaveAndSetActive() => ApplyPreview();

    private void CreateTemplateFromCurrentMap()
    {
        RunClientAction(() =>
        {
            var sourceMapId = FirstNonEmpty(CurrentMapIdForTemplate, LastMapId);
            if (string.IsNullOrWhiteSpace(sourceMapId))
                throw new InvalidOperationException("Укажите MapId текущей карты или сначала сохраните preview.");

            var response = _api.SceneMapGeneratorAdminCreateTemplateFromMap(new Dictionary<string, object>
            {
                ["mapId"] = sourceMapId,
                ["displayName"] = FirstNonEmpty(TemplateNameInput, "Шаблон локации"),
                ["campaignId"] = CampaignId,
                ["ruleSetId"] = RuleSetId
            });
            EnsureOk(response, "Шаблон не создан из карты.");
            StatusMessage = "Шаблон создан из текущей карты.";
            Refresh();
        });
    }

    private void ArchiveSelectedTemplate()
    {
        RunClientAction(() =>
        {
            if (SelectedTemplate == null)
                throw new InvalidOperationException("Выберите шаблон для архивации.");
            var response = _api.SceneMapGeneratorAdminArchiveTemplate(new Dictionary<string, object>
            {
                ["templateId"] = SelectedTemplate.TemplateId
            });
            EnsureOk(response, "Шаблон не архивирован.");
            StatusMessage = "Шаблон архивирован.";
            Refresh();
        });
    }

    private void RunClientAction(Action action)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Операция генератора не выполнена.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Dictionary<string, object> BasePayload() => new()
    {
        ["campaignId"] = CampaignId,
        ["ruleSetId"] = RuleSetId
    };

    private Dictionary<string, object> BuildGenerationPayload()
    {
        var payload = BasePayload();
        if (SelectedMap != null)
        {
            payload["mapId"] = SelectedMap.MapId;
            payload["expectedMapRevision"] = _mapRevision;
        }
        payload["presetId"] = SelectedPreset?.PresetId ?? string.Empty;
        payload["templateId"] = SelectedTemplate?.TemplateId ?? string.Empty;
        payload["displayName"] = DisplayName;
        payload["seed"] = Seed;
        payload["widthMeters"] = WidthMeters;
        payload["heightMeters"] = HeightMeters;
        payload["tileSizeMeters"] = TileSizeMeters;
        payload["gridSizeMeters"] = GridSizeMeters;
        payload["density"] = Density;
        payload["detailLevel"] = DetailLevel;
        payload["symmetry"] = Symmetry;
        payload["includeGmSecrets"] = IncludeGmSecrets;
        payload["includeHazards"] = IncludeHazards;
        payload["includeSpawnZones"] = IncludeSpawnZones;
        payload["includeObjectiveZones"] = IncludeObjectiveZones;
        payload["setActiveForSession"] = SetActiveForSession;
        return payload;
    }

    private void ApplyPreset(LocationGeneratorPresetUiItem? preset)
    {
        Notify(nameof(SelectedPresetSummary));
        if (preset == null) return;
        DisplayName = preset.DisplayName;
        WidthMeters = preset.DefaultWidthMeters;
        HeightMeters = preset.DefaultHeightMeters;
        TileSizeMeters = preset.DefaultTileSizeMeters;
        GridSizeMeters = preset.DefaultGridSizeMeters;
        if (string.IsNullOrWhiteSpace(Seed))
            Seed = preset.PresetId + "-seed";
    }

    private void ApplyGenerationPayload(Dictionary<string, object> payload, string status)
    {
        _previewId = GetString(payload, "previewId");
        LastRunId = GetString(payload, "runId", "generationRunId");
        LastMapId = GetString(payload, "mapId", "generatedSceneMapId");
        // Apply must echo the server-owned preview fingerprint, not the generator's input hash.
        LastHash = GetString(payload, "previewFingerprint");
        if (string.IsNullOrWhiteSpace(LastHash)) LastHash = GetString(payload, "normalizedHash");
        _mapRevision = GetLong(payload, "mapRevision", _mapRevision);
        CurrentMapIdForTemplate = FirstNonEmpty(LastMapId, CurrentMapIdForTemplate);

        var map = GetDict(payload, "map");
        if (map != null)
        {
            WidthMeters = GetInt(map, "widthMeters", WidthMeters);
            HeightMeters = GetInt(map, "heightMeters", HeightMeters);
        }

        RenderPreview(
            GetArray(payload, "tilePatches"),
            GetArray(payload, "assetInstances"),
            GetArray(payload, "markers"));

        var summary = GetDict(payload, "summary");
        var tileCount = GetInt(summary, "tilePatchCount", PreviewTiles.Count);
        var assetCount = GetInt(summary, "assetInstanceCount", PreviewAssets.Count);
        var markerCount = GetInt(summary, "markerCount", PreviewMarkers.Count);
        var cleanStatus = CleanStatusText(status);
        ResultSummary = $"{cleanStatus} Покрытия: {tileCount}; объекты: {assetCount}; метки: {markerCount}.";
        PreviewStateText = HasPreview ? "Предпросмотр готов. Карта ещё не изменена." : cleanStatus;
        PreviewWarnings.Clear();
        foreach (var warning in GetArray(payload, "warnings").Select(Convert.ToString).Where(value => !string.IsNullOrWhiteSpace(value)))
            PreviewWarnings.Add(warning!);
        StatusMessage = cleanStatus;
        NotifyCommandState();
    }

    private void RenderPreview(IEnumerable<object> tiles, IEnumerable<object> assets, IEnumerable<object> markers)
    {
        PreviewTiles.Clear();
        PreviewAssets.Clear();
        PreviewMarkers.Clear();

        var scale = Math.Min(PreviewCanvasWidth / Math.Max(WidthMeters, 1), PreviewCanvasHeight / Math.Max(HeightMeters, 1));
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) scale = 1d;

        foreach (var item in tiles.Select(GetDict).Where(x => x != null).Cast<Dictionary<string, object>>())
        {
            var x = GetDouble(item, "x");
            var y = GetDouble(item, "y");
            var width = Math.Max(GetDouble(item, "width"), GetDouble(item, "widthMeters"));
            var height = Math.Max(GetDouble(item, "height"), GetDouble(item, "heightMeters"));
            var material = GetString(item, "materialKey", "textureKey");
            PreviewTiles.Add(new LocationGeneratorTilePreviewItem
            {
                Label = material,
                Left = x * scale,
                Top = y * scale,
                Width = Math.Max(width * scale, 2),
                Height = Math.Max(height * scale, 2),
                Fill = MaterialBrush(material),
                Stroke = new SolidColorBrush(Color.FromRgb(80, 95, 110))
            });
        }

        foreach (var item in assets.Select(GetDict).Where(x => x != null).Cast<Dictionary<string, object>>())
        {
            var x = GetDouble(item, "x");
            var y = GetDouble(item, "y");
            var width = Math.Max(GetDouble(item, "widthMeters"), GetDouble(item, "width"));
            var height = Math.Max(GetDouble(item, "heightMeters"), GetDouble(item, "height"));
            var name = FirstNonEmpty(GetString(item, "displayName"), GetString(item, "name"), GetString(item, "assetKey"));
            PreviewAssets.Add(new LocationGeneratorAssetPreviewItem
            {
                Label = name,
                Left = x * scale,
                Top = y * scale,
                Width = Math.Max(width * scale, 8),
                Height = Math.Max(height * scale, 8),
                Fill = new SolidColorBrush(Color.FromRgb(74, 110, 82)),
                Stroke = new SolidColorBrush(Color.FromRgb(170, 207, 168))
            });
        }

        foreach (var item in markers.Select(GetDict).Where(x => x != null).Cast<Dictionary<string, object>>())
        {
            var x = GetDouble(item, "x");
            var y = GetDouble(item, "y");
            PreviewMarkers.Add(new LocationGeneratorMarkerPreviewItem
            {
                Label = FirstNonEmpty(GetString(item, "name"), GetString(item, "displayName"), "Маркер"),
                Left = x * scale,
                Top = y * scale
            });
        }
    }

    private void ReplaceMaps(IEnumerable<object> items)
    {
        var selectedId = SelectedMap?.MapId;
        Maps.Clear();
        foreach (var item in items.Select(GetDict).Where(value => value != null).Cast<Dictionary<string, object>>())
            Maps.Add(LocationGeneratorMapUiItem.From(item));
        _selectedMap = Maps.FirstOrDefault(item => string.Equals(item.MapId, selectedId, StringComparison.OrdinalIgnoreCase));
        Notify(nameof(SelectedMap));
        Notify(nameof(SelectedMapSummary));
    }

    private void LoadSelectedMapState()
    {
        if (SelectedMap == null) return;
        var response = _api.MapEditorAdminGetState(new Dictionary<string, object> { ["mapId"] = SelectedMap.MapId });
        if (response.Status != ResponseStatus.Ok)
        {
            ErrorMessage = response.Message;
            return;
        }
        _mapRevision = GetLong(response.Payload, "mapRevision", 0);
        WidthMeters = GetInt(response.Payload, "widthMeters", SelectedMap.WidthMeters);
        HeightMeters = GetInt(response.Payload, "heightMeters", SelectedMap.HeightMeters);
        GridSizeMeters = GetInt(response.Payload, "gridCellSizeMeters", SelectedMap.GridCellSizeMeters);
    }

    private void ClearPreviewState(bool keepCanvas = false)
    {
        _previewId = string.Empty;
        LastHash = string.Empty;
        PreviewWarnings.Clear();
        PreviewStateText = "Предпросмотр ещё не создан.";
        if (!keepCanvas)
        {
            PreviewTiles.Clear();
            PreviewAssets.Clear();
            PreviewMarkers.Clear();
            ResultSummary = "Предпросмотр ещё не создан.";
        }
        NotifyCommandState();
    }

    private void NotifyCommandState()
    {
        Notify(nameof(IsIdle));
        Notify(nameof(HasPreview));
        Notify(nameof(CanCreatePreview));
        Notify(nameof(CanApplyPreview));
    }

    private void ReplacePresets(IEnumerable<object> items)
    {
        Presets.Clear();
        foreach (var item in items.Select(GetDict).Where(x => x != null).Cast<Dictionary<string, object>>())
        {
            Presets.Add(new LocationGeneratorPresetUiItem
            {
                PresetId = GetString(item, "presetId", "id"),
                DisplayName = FirstNonEmpty(GetString(item, "displayName"), GetString(item, "name")),
                LocationKind = GetString(item, "locationKind"),
                MapScale = GetString(item, "mapScale"),
                DefaultWidthMeters = GetInt(item, "defaultWidthMeters", 200),
                DefaultHeightMeters = GetInt(item, "defaultHeightMeters", 200),
                DefaultTileSizeMeters = GetDouble(item, "defaultTileSizeMeters", 5),
                DefaultGridSizeMeters = GetDouble(item, "defaultGridSizeMeters", 5)
            });
        }
        Notify(nameof(SelectedPresetSummary));
    }

    private void ReplaceTemplates(IEnumerable<object> items)
    {
        Templates.Clear();
        foreach (var item in items.Select(GetDict).Where(x => x != null).Cast<Dictionary<string, object>>())
        {
            Templates.Add(new LocationGeneratorTemplateUiItem
            {
                TemplateId = GetString(item, "templateId", "id"),
                DisplayName = FirstNonEmpty(GetString(item, "displayName"), GetString(item, "name")),
                LocationKind = GetString(item, "locationKind"),
                MapScale = GetString(item, "mapScale"),
                WidthMeters = GetInt(item, "widthMeters", 100),
                HeightMeters = GetInt(item, "heightMeters", 100),
                TilePatchCount = GetInt(item, "tilePatchCount"),
                AssetInstanceCount = GetInt(item, "assetInstanceCount"),
                MarkerCount = GetInt(item, "markerCount")
            });
        }
        Notify(nameof(SelectedTemplateSummary));
    }

    private static void EnsureOk(ResponseEnvelope response, string fallback)
    {
        if (response.Status == ResponseStatus.Ok) return;
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(propertyName);
        return true;
    }

    private static IEnumerable<object> GetArray(Dictionary<string, object>? map, params string[] keys)
    {
        if (map == null) return Array.Empty<object>();
        foreach (var key in keys)
        {
            if (!map.TryGetValue(key, out var value) || value == null) continue;
            if (value is object[] array) return array;
            if (value is IEnumerable enumerable && value is not string)
                return enumerable.Cast<object>().ToArray();
        }
        return Array.Empty<object>();
    }

    private static Dictionary<string, object>? GetDict(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key == null) continue;
                result[entry.Key.ToString() ?? string.Empty] = entry.Value ?? string.Empty;
            }
            return result;
        }
        return null;
    }

    private static Dictionary<string, object>? GetDict(Dictionary<string, object>? map, string key)
        => map != null && map.TryGetValue(key, out var value) ? GetDict(value) : null;

    private static string GetString(Dictionary<string, object>? map, params string[] keys)
    {
        if (map == null) return string.Empty;
        foreach (var key in keys)
        {
            if (!map.TryGetValue(key, out var value) || value == null) continue;
            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(text)) return text!;
        }
        return string.Empty;
    }

    private static int GetInt(Dictionary<string, object>? map, string key, int fallback = 0)
    {
        if (map == null || !map.TryGetValue(key, out var value) || value == null) return fallback;
        if (value is int intValue) return intValue;
        if (value is long longValue) return (int)longValue;
        if (value is double doubleValue) return (int)Math.Round(doubleValue);
        return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static long GetLong(Dictionary<string, object>? map, string key, long fallback = 0)
    {
        if (map == null || !map.TryGetValue(key, out var value) || value == null) return fallback;
        if (value is long longValue) return longValue;
        if (value is int intValue) return intValue;
        return long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static double GetDouble(Dictionary<string, object>? map, string key, double fallback = 0)
    {
        if (map == null || !map.TryGetValue(key, out var value) || value == null) return fallback;
        if (value is double doubleValue) return doubleValue;
        if (value is float floatValue) return floatValue;
        if (value is int intValue) return intValue;
        if (value is long longValue) return longValue;
        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string CleanStatusText(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "Готово.";
        if (status.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0)
            return status.IndexOf("seed", StringComparison.OrdinalIgnoreCase) >= 0 ? "Preview пересоздан с новым seed." : "Preview создан.";
        if (status.IndexOf("active", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Карта сохранена и назначена активной для сессии.";
        if (status.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf("scene", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Preview сохранен как редактируемая карта сцены.";
        return status;
    }

    private static Brush MaterialBrush(string material)
    {
        var key = (material ?? string.Empty).ToLowerInvariant();
        if (key.Contains("water")) return new SolidColorBrush(Color.FromRgb(53, 102, 143));
        if (key.Contains("road") || key.Contains("cobble") || key.Contains("stone")) return new SolidColorBrush(Color.FromRgb(104, 107, 100));
        if (key.Contains("wood") || key.Contains("floor")) return new SolidColorBrush(Color.FromRgb(108, 82, 58));
        if (key.Contains("grass") || key.Contains("forest")) return new SolidColorBrush(Color.FromRgb(67, 105, 66));
        if (key.Contains("sand") || key.Contains("dirt")) return new SolidColorBrush(Color.FromRgb(139, 118, 76));
        return new SolidColorBrush(Color.FromRgb(78, 91, 97));
    }
}

public sealed class LocationGeneratorOptionUiItem
{
    public LocationGeneratorOptionUiItem(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public override string ToString() => DisplayName;
}

public sealed class LocationGeneratorMapUiItem
{
    public string MapId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int WidthMeters { get; set; }
    public int HeightMeters { get; set; }
    public int GridCellSizeMeters { get; set; }
    public override string ToString() => DisplayName;

    public static LocationGeneratorMapUiItem From(IDictionary<string, object> payload)
    {
        static string Text(IDictionary<string, object> source, string key)
            => source.TryGetValue(key, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
        static int Number(IDictionary<string, object> source, string key, int fallback)
            => int.TryParse(Text(source, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        return new LocationGeneratorMapUiItem
        {
            MapId = Text(payload, "mapId").Length > 0 ? Text(payload, "mapId") : Text(payload, "id"),
            DisplayName = Text(payload, "name").Length > 0 ? Text(payload, "name") : Text(payload, "displayName"),
            WidthMeters = Number(payload, "widthMeters", 2000),
            HeightMeters = Number(payload, "heightMeters", 2000),
            GridCellSizeMeters = Number(payload, "gridCellSizeMeters", 25)
        };
    }
}

public sealed class LocationGeneratorPresetUiItem
{
    public string PresetId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LocationKind { get; set; } = string.Empty;
    public string MapScale { get; set; } = string.Empty;
    public int DefaultWidthMeters { get; set; }
    public int DefaultHeightMeters { get; set; }
    public double DefaultTileSizeMeters { get; set; }
    public double DefaultGridSizeMeters { get; set; }
    public string CleanLabel => $"{DisplayName} · {LocationKind} · {DefaultWidthMeters}x{DefaultHeightMeters} м";
    public string Label => $"{DisplayName} · {LocationKind} · {DefaultWidthMeters}x{DefaultHeightMeters} м";
    public override string ToString() => DisplayName;
}

public sealed class LocationGeneratorTemplateUiItem
{
    public string TemplateId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LocationKind { get; set; } = string.Empty;
    public string MapScale { get; set; } = string.Empty;
    public int WidthMeters { get; set; }
    public int HeightMeters { get; set; }
    public int TilePatchCount { get; set; }
    public int AssetInstanceCount { get; set; }
    public int MarkerCount { get; set; }
    public string CleanLabel => $"{DisplayName} · {WidthMeters}x{HeightMeters} м · объекты {AssetInstanceCount}";
    public string Label => $"{DisplayName} · {WidthMeters}x{HeightMeters} м · объекты {AssetInstanceCount}";
    public override string ToString() => DisplayName;
}

public sealed class LocationGeneratorTilePreviewItem
{
    public string Label { get; set; } = string.Empty;
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public Brush Fill { get; set; } = Brushes.DimGray;
    public Brush Stroke { get; set; } = Brushes.Gray;
}

public sealed class LocationGeneratorAssetPreviewItem
{
    public string Label { get; set; } = string.Empty;
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public Brush Fill { get; set; } = Brushes.DarkOliveGreen;
    public Brush Stroke { get; set; } = Brushes.LightGreen;
}

public sealed class LocationGeneratorMarkerPreviewItem
{
    public string Label { get; set; } = string.Empty;
    public double Left { get; set; }
    public double Top { get; set; }
}
