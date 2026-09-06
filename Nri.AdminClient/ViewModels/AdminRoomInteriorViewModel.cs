using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminRoomInteriorViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private bool _isBusy;
    private bool _isRoomEnabled;
    private bool _isRoomMarkersEnabled;
    private string _campaignId = "dev-campaign";
    private string _ruleSetId = "fantasy_nri_default";
    private string _parentLocationId = string.Empty;
    private string _parentSceneMapId = string.Empty;
    private string _statusMessage = "Раздел помещений готов.";
    private string _warningMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _newRoomName = string.Empty;
    private string _newRoomDescription = string.Empty;
    private string _newRoomType = RoomTypeIds.Room;
    private string _newInteriorType = InteriorTypeIds.Building;
    private string _newRoomWidth = "20";
    private string _newRoomHeight = "20";
    private string _newRoomGridCell = "2";
    private bool _newRoomPlayerVisible = true;
    private string _newRoomVisibilityMode = MapVisibilityModes.Party;
    private RoomUiItem? _selectedRoom;
    private RoomMarkerUiItem? _selectedMarker;
    private string _markerName = string.Empty;
    private string _markerType = MapMarkerTypeIds.PointOfInterest;
    private string _markerX = "0";
    private string _markerY = "0";
    private bool _markerPlayerVisible = true;
    private string _markerVisibilityMode = MapVisibilityModes.Party;
    private string _markerLinkedEntityType = string.Empty;
    private string _markerLinkedEntityId = string.Empty;
    private string _markerPublicNotes = string.Empty;
    private string _markerGmNotes = string.Empty;
    private string _lastRefreshText = "не обновлялось";

    public AdminRoomInteriorViewModel(CommandApi api)
    {
        _api = api;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshRoomsCommand = new RelayCommand(RefreshRooms);
        CreateRoomCommand = new RelayCommand(CreateRoom);
        LoadSelectedRoomCommand = new RelayCommand(LoadSelectedRoom);
        SaveRoomCommand = new RelayCommand(SaveRoom);
        ArchiveRoomCommand = new RelayCommand(ArchiveRoom);
        AddMarkerCommand = new RelayCommand(AddMarker);
        MoveMarkerCommand = new RelayCommand(MoveMarker);
        SaveMarkerCommand = new RelayCommand(SaveMarker);
        RemoveMarkerCommand = new RelayCommand(RemoveMarker);
    }

    public ObservableCollection<RoomUiItem> Rooms { get; } = new();
    public ObservableCollection<RoomMarkerUiItem> Markers { get; } = new();
    public ObservableCollection<string> RoomTypeOptions { get; } = new ObservableCollection<string>
    {
        RoomTypeIds.Room, RoomTypeIds.Hall, RoomTypeIds.Corridor, RoomTypeIds.Chamber, RoomTypeIds.Entrance, RoomTypeIds.Exit,
        RoomTypeIds.Storage, RoomTypeIds.LivingSpace, RoomTypeIds.Workshop, RoomTypeIds.Laboratory, RoomTypeIds.Office, RoomTypeIds.Barracks,
        RoomTypeIds.Hangar, RoomTypeIds.EngineRoom, RoomTypeIds.Bridge, RoomTypeIds.DungeonRoom, RoomTypeIds.Cave, RoomTypeIds.Ruin, RoomTypeIds.Custom
    };
    public ObservableCollection<string> InteriorTypeOptions { get; } = new ObservableCollection<string>
    {
        InteriorTypeIds.Building, InteriorTypeIds.Dungeon, InteriorTypeIds.Ship, InteriorTypeIds.Airship, InteriorTypeIds.Vehicle,
        InteriorTypeIds.Station, InteriorTypeIds.Cave, InteriorTypeIds.Camp, InteriorTypeIds.Fortification, InteriorTypeIds.Underground, InteriorTypeIds.Custom
    };

    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshRoomsCommand { get; }
    public ICommand CreateRoomCommand { get; }
    public ICommand LoadSelectedRoomCommand { get; }
    public ICommand SaveRoomCommand { get; }
    public ICommand ArchiveRoomCommand { get; }
    public ICommand AddMarkerCommand { get; }
    public ICommand MoveMarkerCommand { get; }
    public ICommand SaveMarkerCommand { get; }
    public ICommand RemoveMarkerCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); Notify(nameof(CanLoadRooms)); } } }
    public string RuleSetId { get => _ruleSetId; set { if (_ruleSetId != value) { _ruleSetId = value; Notify(); Notify(nameof(CanCreateRoom)); } } }
    public string CampaignContextSummary => "Кампания: текущий контекст";
    public string RuleSetContextSummary => "Набор правил: текущий контекст";
    public string ParentLocationId { get => _parentLocationId; set { if (_parentLocationId != value) { _parentLocationId = value; Notify(); } } }
    public string ParentSceneMapId { get => _parentSceneMapId; set { if (_parentSceneMapId != value) { _parentSceneMapId = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string WarningMessage { get => _warningMessage; private set { if (_warningMessage != value) { _warningMessage = value; Notify(); Notify(nameof(HasWarning)); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set { if (_isBusy != value) { _isBusy = value; Notify(); Notify(nameof(IsIdle)); Notify(nameof(CanLoadRooms)); Notify(nameof(CanCreateRoom)); Notify(nameof(CanEditRoom)); Notify(nameof(CanEditMarkers)); } } }
    public bool IsIdle => !IsBusy;
    public bool IsRoomEnabled { get => _isRoomEnabled; private set { if (_isRoomEnabled != value) { _isRoomEnabled = value; Notify(); Notify(nameof(IsRoomDisabled)); Notify(nameof(CanLoadRooms)); Notify(nameof(CanCreateRoom)); Notify(nameof(CanEditRoom)); Notify(nameof(CanEditMarkers)); } } }
    public bool IsRoomDisabled => !IsRoomEnabled;
    public bool IsRoomMarkersEnabled { get => _isRoomMarkersEnabled; private set { if (_isRoomMarkersEnabled != value) { _isRoomMarkersEnabled = value; Notify(); Notify(nameof(CanEditMarkers)); } } }
    public string LastRefreshText { get => _lastRefreshText; private set { if (_lastRefreshText != value) { _lastRefreshText = value; Notify(); } } }

    public string NewRoomName { get => _newRoomName; set { if (_newRoomName != value) { _newRoomName = value; Notify(); Notify(nameof(CanCreateRoom)); } } }
    public string NewRoomDescription { get => _newRoomDescription; set { if (_newRoomDescription != value) { _newRoomDescription = value; Notify(); } } }
    public string NewRoomType { get => _newRoomType; set { if (_newRoomType != value) { _newRoomType = value; Notify(); } } }
    public string NewInteriorType { get => _newInteriorType; set { if (_newInteriorType != value) { _newInteriorType = value; Notify(); } } }
    public string NewRoomWidth { get => _newRoomWidth; set { if (_newRoomWidth != value) { _newRoomWidth = value; Notify(); } } }
    public string NewRoomHeight { get => _newRoomHeight; set { if (_newRoomHeight != value) { _newRoomHeight = value; Notify(); } } }
    public string NewRoomGridCell { get => _newRoomGridCell; set { if (_newRoomGridCell != value) { _newRoomGridCell = value; Notify(); } } }
    public bool NewRoomPlayerVisible { get => _newRoomPlayerVisible; set { if (_newRoomPlayerVisible != value) { _newRoomPlayerVisible = value; Notify(); } } }
    public string NewRoomVisibilityMode { get => _newRoomVisibilityMode; set { if (_newRoomVisibilityMode != value) { _newRoomVisibilityMode = value; Notify(); } } }

    public RoomUiItem? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            if (_selectedRoom == value) return;
            _selectedRoom = value;
            Notify();
            Notify(nameof(CanEditRoom));
            Notify(nameof(CanEditMarkers));
            if (value != null)
            {
                MarkerName = string.Empty;
                MarkerType = MapMarkerTypeIds.PointOfInterest;
                MarkerX = "0";
                MarkerY = "0";
                MarkerPlayerVisible = true;
                MarkerVisibilityMode = MapVisibilityModes.Party;
                MarkerLinkedEntityType = string.Empty;
                MarkerLinkedEntityId = string.Empty;
                MarkerPublicNotes = string.Empty;
                MarkerGmNotes = string.Empty;
            }
        }
    }

    public RoomMarkerUiItem? SelectedMarker
    {
        get => _selectedMarker;
        set
        {
            if (_selectedMarker == value) return;
            _selectedMarker = value;
            Notify();
            if (value == null) return;
            MarkerName = value.Name;
            MarkerType = value.MarkerType;
            MarkerX = value.X.ToString("0.##", CultureInfo.InvariantCulture);
            MarkerY = value.Y.ToString("0.##", CultureInfo.InvariantCulture);
            MarkerPlayerVisible = value.IsPlayerVisible;
            MarkerVisibilityMode = value.VisibilityMode;
            MarkerLinkedEntityType = value.LinkedEntityType;
            MarkerLinkedEntityId = value.LinkedEntityId;
            MarkerPublicNotes = value.PublicNotes;
            MarkerGmNotes = value.GmNotes;
        }
    }

    public string MarkerName { get => _markerName; set { if (_markerName != value) { _markerName = value; Notify(); } } }
    public string MarkerType { get => _markerType; set { if (_markerType != value) { _markerType = value; Notify(); } } }
    public string MarkerX { get => _markerX; set { if (_markerX != value) { _markerX = value; Notify(); } } }
    public string MarkerY { get => _markerY; set { if (_markerY != value) { _markerY = value; Notify(); } } }
    public bool MarkerPlayerVisible { get => _markerPlayerVisible; set { if (_markerPlayerVisible != value) { _markerPlayerVisible = value; Notify(); } } }
    public string MarkerVisibilityMode { get => _markerVisibilityMode; set { if (_markerVisibilityMode != value) { _markerVisibilityMode = value; Notify(); } } }
    public string MarkerLinkedEntityType { get => _markerLinkedEntityType; set { if (_markerLinkedEntityType != value) { _markerLinkedEntityType = value; Notify(); } } }
    public string MarkerLinkedEntityId { get => _markerLinkedEntityId; set { if (_markerLinkedEntityId != value) { _markerLinkedEntityId = value; Notify(); } } }
    public string MarkerPublicNotes { get => _markerPublicNotes; set { if (_markerPublicNotes != value) { _markerPublicNotes = value; Notify(); } } }
    public string MarkerGmNotes { get => _markerGmNotes; set { if (_markerGmNotes != value) { _markerGmNotes = value; Notify(); } } }

    public bool CanLoadRooms => IsRoomEnabled && IsIdle && !string.IsNullOrWhiteSpace(CampaignId);
    public bool CanCreateRoom => IsRoomEnabled && IsIdle && !string.IsNullOrWhiteSpace(CampaignId) && !string.IsNullOrWhiteSpace(RuleSetId) && !string.IsNullOrWhiteSpace(NewRoomName);
    public bool CanEditRoom => IsRoomEnabled && IsIdle && SelectedRoom != null;
    public bool CanEditMarkers => IsRoomEnabled && IsRoomMarkersEnabled && IsIdle && SelectedRoom != null;

    public void RefreshFlags()
    {
        RunSafe("admin.room.flags.refresh", () =>
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            if (!IsOk(response))
            {
                IsRoomEnabled = false;
                IsRoomMarkersEnabled = false;
                WarningMessage = Friendly(response, "Не удалось загрузить флаги функций.");
                StatusMessage = "Помещения недоступны.";
                return;
            }

            var mapSystem = false;
            var space = false;
            var room = false;
            var roomMap = false;
            var markers = false;
            foreach (var flag in AsDictionaries(Get(response.Payload, "flags")))
            {
                var name = Str(flag, "name");
                var enabled = Bool(flag, "effectiveValue");
                if (name == nameof(MapFeatureFlags.UseMapSystemV1)) mapSystem = enabled;
                else if (name == nameof(MapFeatureFlags.UseSpaceHierarchyV1)) space = enabled;
                else if (name == nameof(MapFeatureFlags.UseRoomInteriorV1)) room = enabled;
                else if (name == nameof(MapFeatureFlags.UseRoomMapMvp)) roomMap = enabled;
                else if (name == nameof(MapFeatureFlags.UseRoomMarkers)) markers = enabled;
            }

            IsRoomEnabled = mapSystem && space && room && roomMap;
            IsRoomMarkersEnabled = IsRoomEnabled && markers;
            WarningMessage = IsRoomEnabled
                ? (IsRoomMarkersEnabled ? string.Empty : "Маркерные команды выключены: включите UseRoomMarkers.")
                : "Помещения и интерьеры выключены флагами функций.";
            StatusMessage = IsRoomEnabled
                ? "Room/Interior MVP готов к работе."
                : "Room/Interior недоступен: включите UseMapSystemV1, UseSpaceHierarchyV1, UseRoomInteriorV1 и UseRoomMapMvp.";
        });
    }

    private void RefreshRooms()
    {
        RunSafe("admin.room.load", () =>
        {
            var response = _api.MapRoomList(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "parentLocationId", ParentLocationId },
                { "parentSceneMapId", ParentSceneMapId },
                { "includeArchived", false }
            });

            Rooms.Clear();
            if (!IsOk(response))
            {
                StatusMessage = Friendly(response, "Не удалось загрузить помещения.");
                return;
            }

            foreach (var room in AsDictionaries(Get(response.Payload, "items")))
                Rooms.Add(RoomUiItem.From(room));

            StatusMessage = Rooms.Count == 0 ? "Помещения не найдены." : $"Загружено помещений: {Rooms.Count}.";
            LastRefreshText = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);
            if (Rooms.Count > 0 && SelectedRoom == null) SelectedRoom = Rooms[0];
        });
    }

    private void CreateRoom()
    {
        RunSafe("admin.room.create", () =>
        {
            var response = _api.MapRoomCreate(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "ruleSetId", RuleSetId },
                { "parentLocationId", ParentLocationId },
                { "parentSceneMapId", ParentSceneMapId },
                { "name", NewRoomName },
                { "description", NewRoomDescription },
                { "roomType", NewRoomType },
                { "interiorType", NewInteriorType },
                { "widthMeters", ParseDouble(NewRoomWidth, 20d) },
                { "heightMeters", ParseDouble(NewRoomHeight, 20d) },
                { "gridCellSizeMeters", ParseInt(NewRoomGridCell, 2) },
                { "isPlayerVisible", NewRoomPlayerVisible },
                { "visibilityMode", NewRoomVisibilityMode }
            });

            if (!IsOk(response))
            {
                StatusMessage = Friendly(response, "Не удалось создать помещение.");
                return;
            }

            StatusMessage = "Помещение создано.";
            RefreshRooms();
        });
    }

    private void LoadSelectedRoom()
    {
        if (SelectedRoom == null) return;
        RunSafe("admin.room.select", () =>
        {
            var response = _api.MapRoomGet(new Dictionary<string, object> { { "roomId", SelectedRoom.RoomId } });
            Markers.Clear();
            if (!IsOk(response))
            {
                StatusMessage = Friendly(response, "Не удалось загрузить помещение.");
                return;
            }

            var roomMap = AsDictionary(Get(response.Payload, "room"));
            if (roomMap.Count > 0)
            {
                SelectedRoom.Name = Str(roomMap, "name");
                SelectedRoom.RoomType = Str(roomMap, "roomType");
                SelectedRoom.InteriorType = Str(roomMap, "interiorType");
                SelectedRoom.WidthMeters = Dbl(roomMap, "widthMeters");
                SelectedRoom.HeightMeters = Dbl(roomMap, "heightMeters");
                SelectedRoom.IsPlayerVisible = Bool(roomMap, "isPlayerVisible");
                SelectedRoom.VisibilityMode = Str(roomMap, "visibilityMode", MapVisibilityModes.Party);
                SelectedRoom.Description = Str(roomMap, "description");
                SelectedRoom.PublicNotes = Str(roomMap, "publicNotes");
                SelectedRoom.GmNotes = Str(roomMap, "gmNotes");
                SelectedRoom.NotifyFromModel();
            }

            foreach (var marker in AsDictionaries(Get(response.Payload, "markers")))
                Markers.Add(RoomMarkerUiItem.From(marker));

            StatusMessage = $"Помещение «{SelectedRoom.Name}» загружено. Маркеров: {Markers.Count}.";
        });
    }

    private void SaveRoom()
    {
        if (SelectedRoom == null) return;
        RunSafe("admin.room.update", () =>
        {
            var response = _api.MapRoomUpdate(new Dictionary<string, object>
            {
                { "roomId", SelectedRoom.RoomId },
                { "name", SelectedRoom.Name },
                { "description", SelectedRoom.Description },
                { "roomType", SelectedRoom.RoomType },
                { "interiorType", SelectedRoom.InteriorType },
                { "widthMeters", SelectedRoom.WidthMeters },
                { "heightMeters", SelectedRoom.HeightMeters },
                { "gridCellSizeMeters", SelectedRoom.GridCellSizeMeters },
                { "isPlayerVisible", SelectedRoom.IsPlayerVisible },
                { "visibilityMode", SelectedRoom.VisibilityMode },
                { "publicNotes", SelectedRoom.PublicNotes },
                { "gmNotes", SelectedRoom.GmNotes }
            });
            if (!IsOk(response))
            {
                StatusMessage = Friendly(response, "Не удалось сохранить помещение.");
                return;
            }

            StatusMessage = "Помещение сохранено.";
            RefreshRooms();
        });
    }

    private void ArchiveRoom()
    {
        if (SelectedRoom == null) return;
        RunSafe("admin.room.archive", () =>
        {
            var response = _api.MapRoomArchive(new Dictionary<string, object> { { "roomId", SelectedRoom.RoomId } });
            if (!IsOk(response))
            {
                StatusMessage = Friendly(response, "Не удалось архивировать помещение.");
                return;
            }

            StatusMessage = "Помещение архивировано.";
            Markers.Clear();
            SelectedRoom = null;
            RefreshRooms();
        });
    }

    private void AddMarker()
    {
        if (SelectedRoom == null) return;
        RunSafe("admin.room.marker.add", () =>
        {
            var response = _api.MapRoomMarkerAdd(new Dictionary<string, object>
            {
                { "roomId", SelectedRoom.RoomId },
                { "name", MarkerName },
                { "markerType", MarkerType },
                { "x", ParseDouble(MarkerX, 0d) },
                { "y", ParseDouble(MarkerY, 0d) },
                { "isPlayerVisible", MarkerPlayerVisible },
                { "visibilityMode", MarkerVisibilityMode },
                { "linkedEntityType", MarkerLinkedEntityType },
                { "linkedEntityId", MarkerLinkedEntityId },
                { "publicNotes", MarkerPublicNotes },
                { "gmNotes", MarkerGmNotes }
            });
            if (!IsOk(response))
            {
                StatusMessage = Friendly(response, "Не удалось добавить маркер.");
                return;
            }

            StatusMessage = "Маркер добавлен.";
            LoadSelectedRoom();
        });
    }

    private void MoveMarker()
    {
        if (SelectedMarker == null) return;
        RunSafe("admin.room.marker.move", () =>
        {
            var response = _api.MapRoomMarkerMove(new Dictionary<string, object>
            {
                { "markerId", SelectedMarker.MarkerId },
                { "x", ParseDouble(MarkerX, SelectedMarker.X) },
                { "y", ParseDouble(MarkerY, SelectedMarker.Y) }
            });
            if (!IsOk(response))
            {
                StatusMessage = Friendly(response, "Не удалось переместить маркер.");
                return;
            }

            StatusMessage = "Маркер перемещён.";
            LoadSelectedRoom();
        });
    }

    private void SaveMarker()
    {
        if (SelectedMarker == null) return;
        RunSafe("admin.room.marker.update", () =>
        {
            var response = _api.MapRoomMarkerUpdate(new Dictionary<string, object>
            {
                { "markerId", SelectedMarker.MarkerId },
                { "name", MarkerName },
                { "markerType", MarkerType },
                { "x", ParseDouble(MarkerX, SelectedMarker.X) },
                { "y", ParseDouble(MarkerY, SelectedMarker.Y) },
                { "isPlayerVisible", MarkerPlayerVisible },
                { "visibilityMode", MarkerVisibilityMode },
                { "linkedEntityType", MarkerLinkedEntityType },
                { "linkedEntityId", MarkerLinkedEntityId },
                { "publicNotes", MarkerPublicNotes },
                { "gmNotes", MarkerGmNotes }
            });
            if (!IsOk(response))
            {
                StatusMessage = Friendly(response, "Не удалось обновить маркер.");
                return;
            }

            StatusMessage = "Маркер обновлён.";
            LoadSelectedRoom();
        });
    }

    private void RemoveMarker()
    {
        if (SelectedMarker == null) return;
        RunSafe("admin.room.marker.remove", () =>
        {
            var response = _api.MapRoomMarkerRemove(new Dictionary<string, object> { { "markerId", SelectedMarker.MarkerId } });
            if (!IsOk(response))
            {
                StatusMessage = Friendly(response, "Не удалось удалить маркер.");
                return;
            }

            StatusMessage = "Маркер удалён.";
            LoadSelectedRoom();
        });
    }

    private void RunSafe(string op, Action action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        ClientLogService.Instance.Info(op + ".start");
        try
        {
            action();
            ClientLogService.Instance.Info(op + ".done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Операция завершилась с ошибкой.";
            ClientLogService.Instance.Warn(op + ".error message=" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool IsOk(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;
    private static string Friendly(ResponseEnvelope response, string fallback) => string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
    private static object? Get(IDictionary<string, object> payload, string key) => payload.TryGetValue(key, out var value) ? value : null;
    private static IDictionary<string, object> AsDictionary(object? value) => value as IDictionary<string, object> ?? new Dictionary<string, object>();
    private static IEnumerable<IDictionary<string, object>> AsDictionaries(object? value)
    {
        if (value is IEnumerable<IDictionary<string, object>> typed) return typed;
        if (value is not IEnumerable seq) return Array.Empty<IDictionary<string, object>>();
        var result = new List<IDictionary<string, object>>();
        foreach (var item in seq)
        {
            if (item is IDictionary<string, object> map) result.Add(map);
        }

        return result;
    }

    private static string Str(IDictionary<string, object> map, string key, string fallback = "")
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static bool Bool(IDictionary<string, object> map, string key, bool fallback = false)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        return value is bool b ? b : bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) ? parsed : fallback;
    }

    private static double Dbl(IDictionary<string, object> map, string key, double fallback = 0d)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is int i) return i;
        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static double ParseDouble(string text, double fallback)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static int ParseInt(string text, int fallback)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}

public sealed class RoomUiItem : ViewModelBase
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _roomType = RoomTypeIds.Room;
    private string _interiorType = InteriorTypeIds.Building;
    private double _widthMeters;
    private double _heightMeters;
    private int _gridCellSizeMeters = 2;
    private bool _isPlayerVisible = true;
    private string _visibilityMode = MapVisibilityModes.Party;
    private string _publicNotes = string.Empty;
    private string _gmNotes = string.Empty;

    public string RoomId { get; set; } = string.Empty;
    public string ParentLocationId { get; set; } = string.Empty;
    public string ParentSceneMapId { get; set; } = string.Empty;
    public string Name { get => _name; set { if (_name != value) { _name = value; Notify(); Notify(nameof(Label)); } } }
    public string Description { get => _description; set { if (_description != value) { _description = value; Notify(); } } }
    public string RoomType { get => _roomType; set { if (_roomType != value) { _roomType = value; Notify(); Notify(nameof(Label)); } } }
    public string InteriorType { get => _interiorType; set { if (_interiorType != value) { _interiorType = value; Notify(); Notify(nameof(Label)); } } }
    public double WidthMeters { get => _widthMeters; set { if (Math.Abs(_widthMeters - value) > 0.0001d) { _widthMeters = value; Notify(); Notify(nameof(Label)); } } }
    public double HeightMeters { get => _heightMeters; set { if (Math.Abs(_heightMeters - value) > 0.0001d) { _heightMeters = value; Notify(); Notify(nameof(Label)); } } }
    public int GridCellSizeMeters { get => _gridCellSizeMeters; set { if (_gridCellSizeMeters != value) { _gridCellSizeMeters = value; Notify(); } } }
    public bool IsPlayerVisible { get => _isPlayerVisible; set { if (_isPlayerVisible != value) { _isPlayerVisible = value; Notify(); Notify(nameof(Label)); } } }
    public string VisibilityMode { get => _visibilityMode; set { if (_visibilityMode != value) { _visibilityMode = value; Notify(); } } }
    public string PublicNotes { get => _publicNotes; set { if (_publicNotes != value) { _publicNotes = value; Notify(); } } }
    public string GmNotes { get => _gmNotes; set { if (_gmNotes != value) { _gmNotes = value; Notify(); } } }

    public string Label => $"{Name} ({RoomType}, {WidthMeters:0.#}x{HeightMeters:0.#} м){(IsPlayerVisible ? string.Empty : " [скрыто]")}";
    public void NotifyFromModel()
    {
        Notify(nameof(Name));
        Notify(nameof(Description));
        Notify(nameof(RoomType));
        Notify(nameof(InteriorType));
        Notify(nameof(WidthMeters));
        Notify(nameof(HeightMeters));
        Notify(nameof(GridCellSizeMeters));
        Notify(nameof(IsPlayerVisible));
        Notify(nameof(VisibilityMode));
        Notify(nameof(PublicNotes));
        Notify(nameof(GmNotes));
        Notify(nameof(Label));
    }

    public static RoomUiItem From(IDictionary<string, object> map)
    {
        return new RoomUiItem
        {
            RoomId = Str(map, "roomId"),
            Name = Str(map, "name", "Без имени"),
            RoomType = Str(map, "roomType", RoomTypeIds.Room),
            InteriorType = Str(map, "interiorType", InteriorTypeIds.Building),
            WidthMeters = Dbl(map, "widthMeters"),
            HeightMeters = Dbl(map, "heightMeters"),
            ParentLocationId = Str(map, "parentLocationId"),
            ParentSceneMapId = Str(map, "parentSceneMapId"),
            IsPlayerVisible = Bool(map, "isPlayerVisible", true),
            VisibilityMode = Str(map, "visibilityMode", MapVisibilityModes.Party)
        };
    }

    private static string Str(IDictionary<string, object> map, string key, string fallback = "")
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static bool Bool(IDictionary<string, object> map, string key, bool fallback = false)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        return value is bool b ? b : bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) ? parsed : fallback;
    }

    private static double Dbl(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return 0d;
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is int i) return i;
        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;
    }
}

public sealed class RoomMarkerUiItem : ViewModelBase
{
    public string MarkerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = MapMarkerTypeIds.Custom;
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GmNotes { get; set; } = string.Empty;
    public string Coordinates => $"{X:0.##}, {Y:0.##}";
    public string BindingDisplay => string.IsNullOrWhiteSpace(LinkedEntityType) ? "Без привязки" : $"{LinkedEntityType}: {LinkedEntityId}";

    public static RoomMarkerUiItem From(IDictionary<string, object> map)
    {
        return new RoomMarkerUiItem
        {
            MarkerId = Str(map, "markerId"),
            Name = Str(map, "name", "Маркер"),
            MarkerType = Str(map, "markerType", MapMarkerTypeIds.Custom),
            X = Dbl(map, "x"),
            Y = Dbl(map, "y"),
            IsPlayerVisible = Bool(map, "isPlayerVisible", true),
            VisibilityMode = Str(map, "visibilityMode", MapVisibilityModes.Party),
            LinkedEntityType = Str(map, "linkedEntityType"),
            LinkedEntityId = Str(map, "linkedEntityId"),
            PublicNotes = Str(map, "publicNotes"),
            GmNotes = Str(map, "gmNotes")
        };
    }

    private static string Str(IDictionary<string, object> map, string key, string fallback = "")
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static bool Bool(IDictionary<string, object> map, string key, bool fallback = false)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        return value is bool b ? b : bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) ? parsed : fallback;
    }

    private static double Dbl(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return 0d;
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is int i) return i;
        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;
    }
}
