using Nri.ChatClient.Diagnostics;
using Nri.ChatClient.Networking;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace Nri.ChatClient.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommand(Action execute) : this(_ => execute()) { }
    public RelayCommand(Action<object?> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}

public class TimelineRowVm
{
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public long SortTicks { get; set; }
    public bool IsSystem { get; set; }
}

public class ChatClientMainViewModel : ViewModelBase
{
    private readonly ClientSessionState _session = new ClientSessionState();
    private readonly JsonTcpClient _client;
    private readonly CommandApi _api;

    private string _connectionState = "Оффлайн";
    private string _statusText = "Подключитесь и выполните вход";
    private bool _isAuthenticated;
    private string _serverHostInput;
    private string _serverPortInput;
    private string _loginText = string.Empty;
    private string _passwordText = string.Empty;
    private string _chatTextInput = string.Empty;
    private string _chatSessionId = "default";
    private string _chatTypeInput = "Общее";
    private string _diceFormulaInput = "1d20";
    private string _diceDescriptionInput = string.Empty;
    private string _diceVisibilityInput = "Общее";
    private bool _isTestRoll;

    public ChatClientMainViewModel()
    {
        var config = App.ClientConfig ?? new ClientConfig();
        _serverHostInput = config.ServerHost;
        _serverPortInput = config.ServerPort.ToString(CultureInfo.InvariantCulture);
        _client = new JsonTcpClient(config, _session);
        _api = new CommandApi(_client);

        LoginCommand = new RelayCommand(Login);
        RefreshCommand = new RelayCommand(RefreshAll);
        SendChatCommand = new RelayCommand(SendChat);
        RollDiceCommand = new RelayCommand(RollDice);

        ClientLogService.Instance.Info("chatclient.vm.initialized");
    }

    public ObservableCollection<TimelineRowVm> ChatRows { get; } = new ObservableCollection<TimelineRowVm>();
    public ObservableCollection<TimelineRowVm> DiceRows { get; } = new ObservableCollection<TimelineRowVm>();
    public ObservableCollection<TimelineRowVm> MergedTimelineRows { get; } = new ObservableCollection<TimelineRowVm>();
    public ObservableCollection<TimelineRowVm> MyLastRollRows { get; } = new ObservableCollection<TimelineRowVm>();

    public ObservableCollection<string> ChatTypeOptions { get; } = new ObservableCollection<string> { "Общее", "Системное" };
    public ObservableCollection<string> DiceVisibilityOptions { get; } = new ObservableCollection<string> { "Общее", "Только мастеру", "Теневой" };

    public ICommand LoginCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SendChatCommand { get; }
    public ICommand RollDiceCommand { get; }

    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); } }
    public string StatusText { get => _statusText; set { _statusText = value; Notify(); } }
    public bool IsAuthenticated { get => _isAuthenticated; set { _isAuthenticated = value; Notify(); } }
    public string ServerHostInput { get => _serverHostInput; set { _serverHostInput = value; Notify(); } }
    public string ServerPortInput { get => _serverPortInput; set { _serverPortInput = value; Notify(); } }
    public string LoginText { get => _loginText; set { _loginText = value; Notify(); } }
    public string PasswordText { get => _passwordText; set { _passwordText = value; Notify(); } }
    public string ChatTextInput { get => _chatTextInput; set { _chatTextInput = value; Notify(); } }
    public string ChatSessionId { get => _chatSessionId; set { _chatSessionId = value; Notify(); } }
    public string ChatTypeInput { get => _chatTypeInput; set { _chatTypeInput = value; Notify(); } }
    public string DiceFormulaInput { get => _diceFormulaInput; set { _diceFormulaInput = value; Notify(); } }
    public string DiceDescriptionInput { get => _diceDescriptionInput; set { _diceDescriptionInput = value; Notify(); } }
    public string DiceVisibilityInput { get => _diceVisibilityInput; set { _diceVisibilityInput = value; Notify(); } }
    public bool IsTestRoll { get => _isTestRoll; set { _isTestRoll = value; Notify(); } }

    private void Login()
    {
        try
        {
            EnsureConnected();
            ClientLogService.Instance.Info($"login.start user={LoginText}");
            var result = _api.Login(LoginText, PasswordText);
            if (result.Status != ResponseStatus.Ok)
            {
                IsAuthenticated = false;
                ConnectionState = "Оффлайн";
                StatusText = $"Ошибка входа: {result.Message}";
                ClientLogService.Instance.Warn($"login.fail user={LoginText} message={result.Message}");
                return;
            }

            IsAuthenticated = true;
            ConnectionState = "Онлайн";
            StatusText = $"Подключено к {ServerHostInput}:{ServerPortInput} как {LoginText}";
            ClientLogService.Instance.Info($"login.success user={LoginText}");
            RefreshAll();
        }
        catch (Exception ex)
        {
            HandleConnectionError(ex);
        }
    }

    private void RefreshAll()
    {
        if (!IsAuthenticated)
        {
            return;
        }

        try
        {
            RefreshChatFeed();
            RefreshDiceFeed();
            BuildMergedTimeline();
            ClientLogService.Instance.Info($"tab.render chat={ChatRows.Count} dice={DiceRows.Count} merged={MergedTimelineRows.Count}");
        }
        catch (Exception ex)
        {
            HandleConnectionError(ex);
        }
    }

    private void SendChat()
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(ChatTextInput))
        {
            return;
        }

        var sessionId = ResolveSessionId();
        var type = ToServerChatType(ChatTypeInput);
        if (string.Equals(type, "System", StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "Системные сообщения отправляются только сервером.";
            return;
        }

        try
        {
            ClientLogService.Instance.Info($"chat.send session={sessionId}");
            _api.ChatSend(sessionId, type, ChatTextInput.Trim());
            ChatTextInput = string.Empty;
            RefreshAll();
        }
        catch (Exception ex)
        {
            HandleConnectionError(ex);
        }
    }

    private void RollDice()
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(DiceFormulaInput))
        {
            return;
        }

        try
        {
            var visibility = ToServerDiceVisibility(DiceVisibilityInput);
            ClientLogService.Instance.Info($"dice.roll.send formula={DiceFormulaInput} visibility={visibility} test={IsTestRoll}");
            if (IsTestRoll)
            {
                _api.DiceRollTest(DiceFormulaInput.Trim(), visibility, DiceDescriptionInput.Trim());
            }
            else
            {
                _api.DiceRollStandard(DiceFormulaInput.Trim(), visibility, DiceDescriptionInput.Trim());
            }

            RefreshAll();
        }
        catch (Exception ex)
        {
            HandleConnectionError(ex);
        }
    }

    private void RefreshChatFeed()
    {
        var sessionId = ResolveSessionId();
        ChatRows.Clear();
        var response = _api.ChatVisibleFeed(sessionId, 120);
        var items = ExtractChatItems(response.Payload);

        foreach (var item in items)
        {
            var map = AsMap(item);
            if (map == null)
            {
                continue;
            }

            var row = BuildChatMessageRow(map);
            if (row != null)
            {
                ChatRows.Add(row);
            }
        }

        ClientLogService.Instance.Info($"chat.feed.load items={ChatRows.Count} session={sessionId}");
    }

    private void RefreshDiceFeed()
    {
        DiceRows.Clear();
        MyLastRollRows.Clear();
        var response = _api.DiceVisibleFeed();
        var items = ToObjectList(response.Payload.ContainsKey("items") ? response.Payload["items"] : new ArrayList());

        foreach (var item in items)
        {
            var map = AsMap(item);
            if (map == null)
            {
                continue;
            }

            var row = BuildDiceMessageRow(map);
            if (row == null)
            {
                continue;
            }

            DiceRows.Add(row);
            if (string.Equals(row.Sender, LoginText, StringComparison.OrdinalIgnoreCase) && MyLastRollRows.Count < 8)
            {
                MyLastRollRows.Add(row);
            }
        }

        ClientLogService.Instance.Info($"dice.feed.load items={DiceRows.Count} myLast={MyLastRollRows.Count}");
    }

    private void BuildMergedTimeline()
    {
        MergedTimelineRows.Clear();
        var merged = ChatRows.Concat(DiceRows)
            .OrderBy(item => item.SortTicks == 0 ? long.MaxValue : item.SortTicks)
            .ThenBy(item => item.Timestamp, StringComparer.Ordinal)
            .ToList();

        foreach (var item in merged)
        {
            MergedTimelineRows.Add(item);
        }

        ClientLogService.Instance.Info($"timeline.merged.count value={MergedTimelineRows.Count}");
    }

    private void EnsureConnected()
    {
        if (!int.TryParse(ServerPortInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
        {
            throw new InvalidOperationException("Порт указан неверно.");
        }

        if (_client.ServerHost != ServerHostInput || _client.ServerPort != port)
        {
            _client.UpdateEndpoint(ServerHostInput, port);
        }

        _client.Connect();
    }

    private void HandleConnectionError(Exception ex)
    {
        var message = ex switch
        {
            SocketException => "Сервер недоступен",
            TimeoutException => "Не удалось подключиться к серверу: timeout",
            InvalidOperationException => ex.Message,
            _ => string.IsNullOrWhiteSpace(ex.Message) ? "Ошибка соединения" : ex.Message
        };

        ConnectionState = "Оффлайн";
        StatusText = message;
        IsAuthenticated = false;
        _client.Disconnect();
        ClientLogService.Instance.Error("connection.error", ex);
    }

    private string ResolveSessionId()
    {
        if (string.IsNullOrWhiteSpace(ChatSessionId))
        {
            ChatSessionId = "default";
        }

        return ChatSessionId.Trim();
    }

    private static TimelineRowVm? BuildChatMessageRow(Dictionary<string, object> map)
    {
        var sender = FirstNonEmpty(GetString(map, "senderDisplayName"), GetString(map, "senderUserId"), "Система");
        var text = FirstNonEmpty(GetString(map, "text"), GetString(map, "message"), GetString(map, "body"));
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var type = FirstNonEmpty(GetString(map, "type"), "Public");
        var createdRaw = FirstNonEmpty(GetString(map, "createdUtc"), GetString(map, "createdAt"), GetString(map, "at"));
        return new TimelineRowVm
        {
            Sender = sender,
            Text = text,
            Timestamp = FormatTimestamp(createdRaw),
            SortTicks = ParseTimelineTicks(createdRaw),
            IsSystem = string.Equals(type, "System", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static TimelineRowVm? BuildDiceMessageRow(Dictionary<string, object> map)
    {
        var creator = FirstNonEmpty(GetString(map, "creatorLogin"), GetString(map, "creatorUserId"), "Unknown");
        var formula = GetString(map, "formula");
        var total = ExtractDiceTotal(map);
        if (string.IsNullOrWhiteSpace(formula))
        {
            return null;
        }

        var isTest = string.Equals(GetString(map, "isTestRoll"), "True", StringComparison.OrdinalIgnoreCase);
        var details = BuildDiceRollDetails(map);
        var visibility = GetString(map, "visibility");
        var createdRaw = FirstNonEmpty(GetString(map, "createdUtc"), GetString(map, "createdAtUtc"), GetString(map, "requestedUtc"), GetString(map, "resolvedUtc"), GetString(map, "at"));

        return new TimelineRowVm
        {
            Sender = creator,
            Text = $"{(isTest ? "[ТЕСТ] " : string.Empty)}{formula} = {total}{details} | {visibility}",
            Timestamp = FormatTimestamp(createdRaw),
            SortTicks = ParseTimelineTicks(createdRaw),
            IsSystem = true
        };
    }

    private static string ExtractDiceTotal(Dictionary<string, object> map)
    {
        if (!map.TryGetValue("result", out var rawResult)) return "?";
        var resultMap = AsMap(rawResult);
        if (resultMap == null) return "?";
        return FirstNonEmpty(GetString(resultMap, "total"), "?");
    }

    private static string BuildDiceRollDetails(Dictionary<string, object> map)
    {
        if (!map.TryGetValue("result", out var rawResult)) return string.Empty;
        var resultMap = AsMap(rawResult);
        if (resultMap == null || !resultMap.TryGetValue("rolls", out var rawRolls)) return string.Empty;

        var values = new List<string>();
        foreach (var item in ToObjectList(rawRolls))
        {
            var value = Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
        }

        if (values.Count == 0) return string.Empty;
        return $" ({string.Join(",", values)})";
    }

    private static IList ExtractChatItems(Dictionary<string, object> payload)
    {
        if (payload.TryGetValue("messages", out var messages)) return ToObjectList(messages);
        if (payload.TryGetValue("items", out var items)) return ToObjectList(items);
        return new ArrayList();
    }

    private static Dictionary<string, object>? AsMap(object? value)
    {
        if (value is Dictionary<string, object> typedMap)
        {
            return typedMap;
        }

        if (value is IDictionary dictionary)
        {
            var map = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    map[key] = entry.Value;
                }
            }

            return map;
        }

        return null;
    }

    private static IList ToObjectList(object payload) => payload as IList ?? new ArrayList();

    private static string ToServerChatType(string uiType)
    {
        return uiType switch
        {
            "Системное" => "System",
            _ => "Public"
        };
    }

    private static string ToServerDiceVisibility(string uiValue)
    {
        return uiValue switch
        {
            "Только мастеру" => "AdminOnly",
            "Теневой" => "HiddenToAdmins",
            _ => "Public"
        };
    }

    private static string GetString(Dictionary<string, object> map, string key)
        => map.ContainsKey(key) && map[key] != null ? Convert.ToString(map[key], CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string FormatTimestamp(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        if (TryParseServerTimestamp(rawValue, out var parsed))
        {
            var local = parsed.ToLocalTime();
            return local.Date == DateTime.Now.Date
                ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
                : local.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        return rawValue;
    }

    private static long ParseTimelineTicks(string rawValue)
    {
        if (TryParseServerTimestamp(rawValue, out var parsed))
        {
            return parsed.Ticks;
        }

        return 0;
    }

    private static bool TryParseServerTimestamp(string rawValue, out DateTime utcValue)
    {
        utcValue = default;
        var dateMatch = Regex.Match(rawValue, @"^/Date\(([-+]?\d+)");
        if (dateMatch.Success && long.TryParse(dateMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
        {
            utcValue = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
            return true;
        }

        if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            utcValue = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        return false;
    }
}
