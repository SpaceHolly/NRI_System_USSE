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

public sealed class PlayerRoomInteriorViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private bool _isBusy;
    private bool _isRoomEnabled;
    private string _campaignId = "dev-campaign";
    private string _statusMessage = "Раздел помещений готов.";
    private string _warningMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _roomName = "Помещение не выбрано";
    private string _roomDescription = string.Empty;
    private string _roomMeta = string.Empty;
    private string _roomPublicNotes = string.Empty;
    private PlayerRoomListItemVm? _selectedRoom;
    private PlayerRoomMarkerVm? _selectedMarker;
    private string _selectedMarkerTitle = "Маркер не выбран";
    private string _selectedMarkerDescription = string.Empty;
    private string _selectedMarkerMeta = string.Empty;

    public PlayerRoomInteriorViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshRoomsCommand = new RelayCommand(LoadRooms);
        OpenSelectedRoomCommand = new RelayCommand(OpenSelectedRoom);
    }

    public ObservableCollection<PlayerRoomListItemVm> Rooms { get; } = new();
    public ObservableCollection<PlayerRoomMarkerVm> Markers { get; } = new();

    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshRoomsCommand { get; }
    public ICommand OpenSelectedRoomCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public bool IsBusy { get => _isBusy; private set { if (_isBusy != value) { _isBusy = value; Notify(); Notify(nameof(CanOpenSelectedRoom)); } } }
    public bool IsRoomEnabled { get => _isRoomEnabled; private set { if (_isRoomEnabled != value) { _isRoomEnabled = value; Notify(); Notify(nameof(IsRoomDisabled)); Notify(nameof(CanOpenSelectedRoom)); } } }
    public bool IsRoomDisabled => !IsRoomEnabled;
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string WarningMessage { get => _warningMessage; private set { if (_warningMessage != value) { _warningMessage = value; Notify(); Notify(nameof(HasWarning)); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string RoomName { get => _roomName; private set { if (_roomName != value) { _roomName = value; Notify(); } } }
    public string RoomDescription { get => _roomDescription; private set { if (_roomDescription != value) { _roomDescription = value; Notify(); } } }
    public string RoomMeta { get => _roomMeta; private set { if (_roomMeta != value) { _roomMeta = value; Notify(); } } }
    public string RoomPublicNotes { get => _roomPublicNotes; private set { if (_roomPublicNotes != value) { _roomPublicNotes = value; Notify(); } } }
    public string SelectedMarkerTitle { get => _selectedMarkerTitle; private set { if (_selectedMarkerTitle != value) { _selectedMarkerTitle = value; Notify(); } } }
    public string SelectedMarkerDescription { get => _selectedMarkerDescription; private set { if (_selectedMarkerDescription != value) { _selectedMarkerDescription = value; Notify(); } } }
    public string SelectedMarkerMeta { get => _selectedMarkerMeta; private set { if (_selectedMarkerMeta != value) { _selectedMarkerMeta = value; Notify(); } } }

    public PlayerRoomListItemVm? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            if (_selectedRoom == value) return;
            _selectedRoom = value;
            Notify();
            Notify(nameof(CanOpenSelectedRoom));
        }
    }

    public PlayerRoomMarkerVm? SelectedMarker
    {
        get => _selectedMarker;
        set
        {
            if (_selectedMarker == value) return;
            _selectedMarker = value;
            Notify();
            if (value == null)
            {
                SelectedMarkerTitle = "Маркер не выбран";
                SelectedMarkerDescription = string.Empty;
                SelectedMarkerMeta = string.Empty;
                return;
            }

            SelectedMarkerTitle = value.Name;
            SelectedMarkerDescription = value.CardDescription;
            SelectedMarkerMeta = $"{value.MarkerTypeDisplay} • {value.Coordinates}";
        }
    }

    public bool CanOpenSelectedRoom => IsRoomEnabled && !IsBusy && SelectedRoom != null;

    public void RefreshFlags()
    {
        RunSafe("player.room.flags.refresh", () =>
        {
            var response = _api.SendSystemFeatureFlagsSnapshotForPlayer();
            if (!IsOk(response))
            {
                IsRoomEnabled = false;
                StatusMessage = "Помещения недоступны.";
                WarningMessage = PlayerFacingMessage(response.Message, "Не удалось проверить доступность помещений.");
                return;
            }

            var mapSystem = false;
            var space = false;
            var room = false;
            var roomMap = false;
            var playerView = false;

            foreach (var flag in AsDictionaries(Get(response.Payload, "flags")))
            {
                var name = Str(flag, "name");
                var enabled = Bool(flag, "effectiveValue");
                if (name == nameof(MapFeatureFlags.UseMapSystemV1)) mapSystem = enabled;
                else if (name == nameof(MapFeatureFlags.UseSpaceHierarchyV1)) space = enabled;
                else if (name == nameof(MapFeatureFlags.UseRoomInteriorV1)) room = enabled;
                else if (name == nameof(MapFeatureFlags.UseRoomMapMvp)) roomMap = enabled;
                else if (name == nameof(MapFeatureFlags.UseRoomPlayerView)) playerView = enabled;
            }

            IsRoomEnabled = mapSystem && space && room && roomMap && playerView;
            WarningMessage = IsRoomEnabled ? string.Empty : "Карта помещений пока недоступна.";
            StatusMessage = IsRoomEnabled
                ? "Загрузите доступные помещения."
                : "Помещения будут доступны после подключения GM.";
        });
    }

    public void LoadRooms()
    {
        RunSafe("player.room.load", () =>
        {
            var response = _api.MapPlayerRoomList(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "characterId", _activeCharacterIdAccessor() }
            });

            Rooms.Clear();
            Markers.Clear();
            if (!IsOk(response))
            {
                StatusMessage = PlayerFacingMessage(response.Message, "Не удалось загрузить помещения.");
                return;
            }

            foreach (var room in AsDictionaries(Get(response.Payload, "items")))
                Rooms.Add(PlayerRoomListItemVm.From(room));

            StatusMessage = Rooms.Count == 0 ? "GM ещё не открыл помещения игрокам." : $"Доступно помещений: {Rooms.Count}.";
            if (Rooms.Count > 0 && SelectedRoom == null) SelectedRoom = Rooms[0];
        });
    }

    public void OpenSelectedRoom()
    {
        if (SelectedRoom == null) return;
        RunSafe("player.room.open", () =>
        {
            var response = _api.MapPlayerRoomGet(new Dictionary<string, object>
            {
                { "roomId", SelectedRoom.RoomId },
                { "characterId", _activeCharacterIdAccessor() }
            });
            Markers.Clear();
            if (!IsOk(response))
            {
                StatusMessage = PlayerFacingMessage(response.Message, "Не удалось открыть помещение.");
                return;
            }

            var map = AsDictionary(Get(response.Payload, "map"));
            RoomName = Str(map, "name", "Без названия");
            RoomDescription = Str(map, "description");
            RoomMeta = $"{Str(map, "roomType", "room")} • {Str(map, "interiorType", "building")} • {Dbl(map, "widthMeters"):0.#}x{Dbl(map, "heightMeters"):0.#} м";
            RoomPublicNotes = Str(map, "publicNotes");

            foreach (var marker in AsDictionaries(Get(map, "markers")))
                Markers.Add(PlayerRoomMarkerVm.From(marker));

            SelectedMarker = Markers.FirstOrDefault();
            StatusMessage = Markers.Count == 0 ? "Помещение открыто. Видимых маркеров нет." : $"Помещение открыто. Видимых маркеров: {Markers.Count}.";
        });
    }

    private void RunSafe(string operation, Action action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        ClientLogService.Instance.Info(operation + ".start");
        try
        {
            action();
            ClientLogService.Instance.Info(operation + ".done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Операция завершилась с ошибкой.";
            ClientLogService.Instance.Warn(operation + ".error message=" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool IsOk(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;
    private static string Friendly(ResponseEnvelope response, string fallback) => string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
    private static string PlayerFacingMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        if (message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("UseMap", StringComparison.OrdinalIgnoreCase) >= 0)
            return fallback;
        return message;
    }
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

    private static double Dbl(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return 0d;
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is int i) return i;
        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;
    }
}

public sealed class PlayerRoomListItemVm
{
    public string RoomId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RoomType { get; set; } = RoomTypeIds.Room;
    public string InteriorType { get; set; } = InteriorTypeIds.Building;
    public string Label => $"{Name} ({RoomType})";

    public static PlayerRoomListItemVm From(IDictionary<string, object> map)
    {
        return new PlayerRoomListItemVm
        {
            RoomId = Str(map, "roomId"),
            Name = Str(map, "name", "Без названия"),
            Description = Str(map, "description"),
            RoomType = Str(map, "roomType", RoomTypeIds.Room),
            InteriorType = Str(map, "interiorType", InteriorTypeIds.Building)
        };
    }

    private static string Str(IDictionary<string, object> map, string key, string fallback = "")
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }
}

public sealed class PlayerRoomMarkerVm
{
    public string MarkerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = MapMarkerTypeIds.Custom;
    public double X { get; set; }
    public double Y { get; set; }
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public string MarkerTypeDisplay => MarkerType;
    public string Coordinates => $"{X:0.##}, {Y:0.##}";

    public static PlayerRoomMarkerVm From(IDictionary<string, object> map)
    {
        return new PlayerRoomMarkerVm
        {
            MarkerId = Str(map, "markerId"),
            Name = Str(map, "name", "Маркер"),
            MarkerType = Str(map, "markerType", MapMarkerTypeIds.Custom),
            X = Dbl(map, "x"),
            Y = Dbl(map, "y"),
            CardTitle = Str(map, "cardTitle"),
            CardDescription = Str(map, "cardDescription")
        };
    }

    private static string Str(IDictionary<string, object> map, string key, string fallback = "")
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
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
