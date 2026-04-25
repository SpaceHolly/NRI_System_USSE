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
using System.Reflection;
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
    public string DisplayText => string.IsNullOrWhiteSpace(Sender) ? Text : $"{Sender}: {Text}";
}

public class ChatClientMainViewModel : ViewModelBase
{
    private readonly ClientSessionState _session = new ClientSessionState();
    private readonly JsonTcpClient _client;
    private readonly CommandApi _api;

    private string _connectionState = "Оффлайн";
    private string _statusText = "Укажите сервер и выполните вход";
    private bool _isAuthenticated;
    private bool _isConnectionPopupOpen;
    private bool _isAuthPopupOpen;

    private string _serverHostInput;
    private string _serverPortInput;
    private string _loginText = string.Empty;
    private string _passwordText = string.Empty;
    private string _registerLoginText = string.Empty;
    private string _registerPasswordText = string.Empty;
    private string _oldPasswordText = string.Empty;
    private string _newPasswordText = string.Empty;
    private string _chatTextInput = string.Empty;
    private string _chatSessionId = "default";
    private string _chatTypeInput = "Общее";
    private string _diceDescriptionInput = string.Empty;
    private string _diceVisibilityInput = "Общее";
    private string _diceModeInput = "Обычный";
    private string _diceCount = "1";
    private string _diceFaces = "20";
    private string _diceModifier = "0";

    public ChatClientMainViewModel()
    {
        var config = App.ClientConfig ?? new ClientConfig();
        _serverHostInput = string.IsNullOrWhiteSpace(config.ServerHost) ? "127.0.0.1" : config.ServerHost;
        var resolvedPort = config.ServerPort > 0 ? config.ServerPort : 4600;
        _serverPortInput = resolvedPort.ToString(CultureInfo.InvariantCulture);
        _client = new JsonTcpClient(config, _session);
        _api = new CommandApi(_client);

        ToggleConnectionPopupCommand = new RelayCommand(() => IsConnectionPopupOpen = !IsConnectionPopupOpen);
        ToggleAuthPopupCommand = new RelayCommand(() => IsAuthPopupOpen = !IsAuthPopupOpen);
        ConnectToServerCommand = new RelayCommand(ConnectToServer);
        RegisterCommand = new RelayCommand(Register);
        ChangePasswordCommand = new RelayCommand(ChangePassword);
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
    public ObservableCollection<string> DiceModeOptions { get; } = new ObservableCollection<string> { "Обычный", "Тестовый" };

    public ICommand ToggleConnectionPopupCommand { get; }
    public ICommand ToggleAuthPopupCommand { get; }
    public ICommand ConnectToServerCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SendChatCommand { get; }
    public ICommand RollDiceCommand { get; }

    public string ConnectionState
    {
        get => _connectionState;
        set
        {
            _connectionState = value;
            Notify();
            Notify(nameof(IsOnline));
        }
    }

    public string StatusText { get => _statusText; set { _statusText = value; Notify(); } }
    public bool IsAuthenticated { get => _isAuthenticated; set { _isAuthenticated = value; Notify(); Notify(nameof(UserSummary)); } }
    public bool IsConnectionPopupOpen { get => _isConnectionPopupOpen; set { _isConnectionPopupOpen = value; Notify(); } }
    public bool IsAuthPopupOpen { get => _isAuthPopupOpen; set { _isAuthPopupOpen = value; Notify(); } }

    public string ServerHostInput { get => _serverHostInput; set { _serverHostInput = value; Notify(); } }
    public string ServerPortInput { get => _serverPortInput; set { _serverPortInput = value; Notify(); } }
    public string LoginText { get => _loginText; set { _loginText = value; Notify(); Notify(nameof(UserSummary)); } }
    public string PasswordText { get => _passwordText; set { _passwordText = value; Notify(); } }
    public string RegisterLoginText { get => _registerLoginText; set { _registerLoginText = value; Notify(); } }
    public string RegisterPasswordText { get => _registerPasswordText; set { _registerPasswordText = value; Notify(); } }
    public string OldPasswordText { get => _oldPasswordText; set { _oldPasswordText = value; Notify(); } }
    public string NewPasswordText { get => _newPasswordText; set { _newPasswordText = value; Notify(); } }
    public string ChatTextInput { get => _chatTextInput; set { _chatTextInput = value; Notify(); } }
    public string ChatSessionId { get => _chatSessionId; set { _chatSessionId = value; Notify(); } }
    public string ChatTypeInput { get => _chatTypeInput; set { _chatTypeInput = value; Notify(); } }
    public string DiceDescriptionInput { get => _diceDescriptionInput; set { _diceDescriptionInput = value; Notify(); } }
    public string DiceVisibilityInput { get => _diceVisibilityInput; set { _diceVisibilityInput = value; Notify(); } }
    public string DiceModeInput { get => _diceModeInput; set { _diceModeInput = value; Notify(); } }
    public string DiceCount { get => _diceCount; set { _diceCount = value; Notify(); } }
    public string DiceFaces { get => _diceFaces; set { _diceFaces = value; Notify(); } }
    public string DiceModifier { get => _diceModifier; set { _diceModifier = value; Notify(); } }

    public bool IsOnline => string.Equals(ConnectionState, "Онлайн", StringComparison.OrdinalIgnoreCase);
    public string UserSummary => IsAuthenticated ? $"Пользователь: {LoginText}" : "Пользователь: не авторизован";

    private void ConnectToServer()
    {
        try
        {
            EnsureConnected();
            ConnectionState = "Онлайн";
            StatusText = $"Сервер доступен: {ServerHostInput}:{ServerPortInput}";
            IsConnectionPopupOpen = false;
            ClientLogService.Instance.Info($"connect.manual.success endpoint={ServerHostInput}:{ServerPortInput}");
        }
        catch (Exception ex)
        {
            HandleConnectionError(ex);
        }
    }

    private void Register()
    {
        if (string.IsNullOrWhiteSpace(RegisterLoginText) || string.IsNullOrWhiteSpace(RegisterPasswordText))
        {
            StatusText = "Укажите логин и пароль для регистрации.";
            return;
        }

        try
        {
            EnsureConnected();
            var result = _api.Register(RegisterLoginText.Trim(), RegisterPasswordText);
            StatusText = result.Status == ResponseStatus.Ok
                ? "Регистрация выполнена. Можно входить."
                : $"Регистрация не выполнена: {result.Message}";
            ClientLogService.Instance.Info($"register.result status={result.Status} login={RegisterLoginText}");
            if (result.Status == ResponseStatus.Ok)
            {
                LoginText = RegisterLoginText.Trim();
                RegisterPasswordText = string.Empty;
            }
        }
        catch (Exception ex)
        {
            HandleConnectionError(ex);
        }
    }

    private void ChangePassword()
    {
        if (!IsAuthenticated)
        {
            StatusText = "Сначала выполните вход в аккаунт.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OldPasswordText) || string.IsNullOrWhiteSpace(NewPasswordText))
        {
            StatusText = "Укажите старый и новый пароль.";
            return;
        }

        try
        {
            EnsureConnected();
            var result = _api.ChangePassword(OldPasswordText, NewPasswordText);
            StatusText = result.Status == ResponseStatus.Ok
                ? "Пароль успешно изменён."
                : $"Смена пароля не выполнена: {result.Message}";
            ClientLogService.Instance.Info($"changePassword.result status={result.Status}");
            if (result.Status == ResponseStatus.Ok)
            {
                OldPasswordText = string.Empty;
                NewPasswordText = string.Empty;
            }
        }
        catch (Exception ex)
        {
            HandleConnectionError(ex);
        }
    }

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
                StatusText = $"Ошибка входа: {result.Message}";
                ClientLogService.Instance.Warn($"login.fail user={LoginText} message={result.Message}");
                return;
            }

            ConnectionState = "Онлайн";
            IsAuthenticated = true;
            IsAuthPopupOpen = false;
            StatusText = $"Подключено к {ServerHostInput}:{ServerPortInput}";
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
            return;

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
            return;

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
        if (!IsAuthenticated)
            return;

        var formula = BuildFormula();
        if (string.IsNullOrWhiteSpace(formula))
        {
            StatusText = "Укажите корректные параметры броска.";
            return;
        }

        try
        {
            var visibility = ToServerDiceVisibility(DiceVisibilityInput);
            var isTest = string.Equals(DiceModeInput, "Тестовый", StringComparison.OrdinalIgnoreCase);
            var comment = DiceDescriptionInput.Trim();
            ClientLogService.Instance.Info($"dice.roll.comment input={comment}");
            ClientLogService.Instance.Info("dice.roll.payload.keys=formula,visibility,description");
            ClientLogService.Instance.Info($"dice.roll.payload.commentPresent={!string.IsNullOrWhiteSpace(comment)}");
            ClientLogService.Instance.Info($"dice.roll.send formula={formula} visibility={visibility} test={isTest}");
            if (isTest)
                _api.DiceRollTest(formula, visibility, comment);
            else
                _api.DiceRollStandard(formula, visibility, comment);

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
        var responseKeys = string.Join(",", response.Payload.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        var extraction = ExtractFeedCollection(response.Payload, "items", "messages", "feed", "history", "rows", "entries");

        ClientLogService.Instance.Info($"chat.feed.response.keys={responseKeys}");
        ClientLogService.Instance.Info($"chat.feed.rawCollectionKey={extraction.CollectionKey}");
        ClientLogService.Instance.Info($"chat.feed.rawCount={extraction.Items.Count}");

        var firstRaw = extraction.Items.Count > 0 ? extraction.Items[0] : null;
        var firstMap = ToObjectDictionary(firstRaw);
        var firstTextCandidate = firstMap == null ? string.Empty : FirstNonEmpty(GetStringByKeys(firstMap, "text", "message", "content", "body"));
        var firstTimeCandidate = firstMap == null ? string.Empty : FirstNonEmpty(GetStringByKeys(firstMap, "created", "createdUtc", "timestamp", "time", "sentAt", "sentAtUtc"));
        ClientLogService.Instance.Info($"chat.feed.firstRawType={(firstRaw == null ? "null" : firstRaw.GetType().FullName)}");
        ClientLogService.Instance.Info($"chat.feed.firstMapKeys={(firstMap == null ? "<none>" : string.Join(",", firstMap.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)))}");
        ClientLogService.Instance.Info($"chat.feed.firstMapTextCandidate={firstTextCandidate}");
        ClientLogService.Instance.Info($"chat.feed.firstMapTimeCandidate={firstTimeCandidate}");

        var mappedCount = 0;
        foreach (var item in extraction.Items)
        {
            var map = ToObjectDictionary(item);
            if (map == null) continue;

            var row = BuildChatMessageRow(map);
            if (row == null) continue;

            mappedCount++;
            ChatRows.Add(row);
        }

        var placeholderAdded = false;
        if (ChatRows.Count == 0)
        {
            placeholderAdded = true;
            ChatRows.Add(new TimelineRowVm { Sender = "Система", Text = "Нет сообщений", IsSystem = true });
        }

        ClientLogService.Instance.Info($"chat.feed.mappedCount={mappedCount}");
        ClientLogService.Instance.Info($"chat.feed.placeholderAdded={placeholderAdded}");
        ClientLogService.Instance.Info($"chat.feed.displayCount={ChatRows.Count}");
    }

    private void RefreshDiceFeed()
    {
        DiceRows.Clear();
        MyLastRollRows.Clear();

        var response = _api.DiceVisibleFeed();
        var responseKeys = string.Join(",", response.Payload.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        var extraction = ExtractFeedCollection(response.Payload, "items", "messages", "feed", "history", "rows", "entries");

        ClientLogService.Instance.Info($"dice.feed.response.keys={responseKeys}");
        ClientLogService.Instance.Info($"dice.feed.rawCollectionKey={extraction.CollectionKey}");
        ClientLogService.Instance.Info($"dice.feed.rawCount={extraction.Items.Count}");

        var firstRaw = extraction.Items.Count > 0 ? extraction.Items[0] : null;
        var firstMap = ToObjectDictionary(firstRaw);
        var firstResultCandidate = firstMap == null
            ? string.Empty
            : FirstNonEmpty(GetStringByKeys(firstMap, "result", "total", "value", "summary", "description"));
        var firstComment = firstMap == null
            ? string.Empty
            : FirstNonEmpty(GetStringByKeys(firstMap, "description", "comment", "note", "text"));
        ClientLogService.Instance.Info($"dice.feed.firstRawType={(firstRaw == null ? "null" : firstRaw.GetType().FullName)}");
        ClientLogService.Instance.Info($"dice.feed.firstMapKeys={(firstMap == null ? "<none>" : string.Join(",", firstMap.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)))}");
        ClientLogService.Instance.Info($"dice.feed.firstMapResultCandidate={firstResultCandidate}");
        ClientLogService.Instance.Info($"dice.feed.firstComment={firstComment}");

        var mappedCount = 0;
        var commentMapped = false;
        foreach (var item in extraction.Items)
        {
            var map = ToObjectDictionary(item);
            if (map == null) continue;

            var row = BuildDiceMessageRow(map);
            if (row == null) continue;

            mappedCount++;
            if (row.Text.IndexOf(" — ", StringComparison.Ordinal) >= 0) commentMapped = true;
            DiceRows.Add(row);
            if (string.Equals(row.Sender, LoginText, StringComparison.OrdinalIgnoreCase) && MyLastRollRows.Count < 10)
                MyLastRollRows.Add(row);
        }

        var placeholderAdded = false;
        if (DiceRows.Count == 0)
        {
            placeholderAdded = true;
            DiceRows.Add(new TimelineRowVm { Sender = "Система", Text = "Нет видимых бросков", IsSystem = true });
        }

        if (MyLastRollRows.Count == 0)
            MyLastRollRows.Add(new TimelineRowVm { Sender = "Система", Text = "Ваших бросков пока нет", IsSystem = true });

        ClientLogService.Instance.Info($"dice.feed.mappedCount={mappedCount}");
        ClientLogService.Instance.Info($"dice.feed.placeholderAdded={placeholderAdded}");
        ClientLogService.Instance.Info($"dice.feed.commentMapped={commentMapped}");
        ClientLogService.Instance.Info($"dice.feed.displayCount={DiceRows.Count}");
    }

    private void BuildMergedTimeline()
    {
        MergedTimelineRows.Clear();

        var chatPlaceholders = ChatRows.Count(row => string.Equals(row.Text, "Нет сообщений", StringComparison.OrdinalIgnoreCase));
        var dicePlaceholders = DiceRows.Count(row => string.Equals(row.Text, "Нет видимых бросков", StringComparison.OrdinalIgnoreCase));

        var merged = ChatRows
            .Concat(DiceRows.Where(row => !string.Equals(row.Text, "Нет видимых бросков", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.SortTicks == 0 ? long.MaxValue : item.SortTicks)
            .ThenBy(item => item.Timestamp, StringComparer.Ordinal)
            .ToList();

        foreach (var item in merged)
            MergedTimelineRows.Add(item);

        var placeholderAdded = false;
        if (MergedTimelineRows.Count == 0)
        {
            placeholderAdded = true;
            MergedTimelineRows.Add(new TimelineRowVm { Sender = "Система", Text = "Лента пуста", IsSystem = true });
        }

        ClientLogService.Instance.Info($"timeline.merged.chatRows={ChatRows.Count}");
        ClientLogService.Instance.Info($"timeline.merged.diceRows={DiceRows.Count}");
        ClientLogService.Instance.Info($"timeline.merged.chatPlaceholders={chatPlaceholders}");
        ClientLogService.Instance.Info($"timeline.merged.dicePlaceholders={dicePlaceholders}");
        ClientLogService.Instance.Info($"timeline.merged.placeholderAdded={placeholderAdded}");
        ClientLogService.Instance.Info($"timeline.merged.displayCount={MergedTimelineRows.Count}");
    }

    private void EnsureConnected()
    {
        if (!int.TryParse(ServerPortInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
            throw new InvalidOperationException("Порт указан неверно.");

        if (_client.ServerHost != ServerHostInput || _client.ServerPort != port)
            _client.UpdateEndpoint(ServerHostInput, port);

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
            ChatSessionId = "default";
        return ChatSessionId.Trim();
    }

    private string BuildFormula()
    {
        var count = ParsePositiveInt(DiceCount, 1);
        var faces = ParsePositiveInt(DiceFaces, 20);
        var modifier = ParseInt(DiceModifier, 0);
        return modifier == 0 ? $"{count}d{faces}" : modifier > 0 ? $"{count}d{faces}+{modifier}" : $"{count}d{faces}{modifier}";
    }

    private static TimelineRowVm? BuildChatMessageRow(Dictionary<string, object> map)
    {
        var text = FirstNonEmpty(GetStringByKeys(map, "text", "message", "content", "body"));
        if (string.IsNullOrWhiteSpace(text))
            text = FirstNonEmpty(GetStringByKeys(map, "summary", "description", "comment"));
        if (string.IsNullOrWhiteSpace(text))
            text = BuildFallbackText(map);

        if (string.IsNullOrWhiteSpace(text))
            return null;

        var sender = FirstNonEmpty(GetStringByKeys(map, "sender", "senderName", "author", "login", "user", "senderDisplayName", "senderUserId"), "System");
        var type = FirstNonEmpty(GetStringByKeys(map, "kind", "type", "messageType"), "Public");
        var createdRaw = FirstNonEmpty(GetStringByKeys(map, "created", "createdUtc", "timestamp", "time", "sentAt", "sentAtUtc", "createdAt", "at"));

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
        var formula = FirstNonEmpty(GetStringByKeys(map, "formula", "expression"));
        var result = FirstNonEmpty(GetStringByKeys(map, "total", "value"));

        if (string.IsNullOrWhiteSpace(result) && map.TryGetValue("result", out var rawResult))
        {
            var resultMap = ToObjectDictionary(rawResult);
            if (resultMap != null)
                result = FirstNonEmpty(GetStringByKeys(resultMap, "total", "value", "result"));
        }

        var summary = FirstNonEmpty(GetStringByKeys(map, "summary", "description", "comment", "text"));
        var comment = FirstNonEmpty(GetStringByKeys(map, "description", "comment", "note", "text"));
        var sender = FirstNonEmpty(GetStringByKeys(map, "actor", "actorName", "sender", "login", "user", "creatorLogin", "creatorUserId"), "System");
        var createdRaw = FirstNonEmpty(GetStringByKeys(map, "created", "createdUtc", "timestamp", "time", "rolledAt", "rolledAtUtc", "createdAtUtc", "requestedUtc", "resolvedUtc", "at"));

        string text;
        if (!string.IsNullOrWhiteSpace(formula) || !string.IsNullOrWhiteSpace(result))
        {
            var left = string.IsNullOrWhiteSpace(formula) ? "dice" : formula;
            var right = string.IsNullOrWhiteSpace(result) ? "?" : result;
            var details = BuildDiceRollDetails(map);
            var visibility = FirstNonEmpty(GetStringByKeys(map, "visibility"));
            text = string.IsNullOrWhiteSpace(visibility)
                ? $"{left} = {right}{details}"
                : $"{left} = {right}{details} | {visibility}";
        }
        else if (!string.IsNullOrWhiteSpace(summary))
        {
            text = summary;
        }
        else
        {
            text = BuildFallbackText(map);
        }

        if (!string.IsNullOrWhiteSpace(comment) && text.IndexOf(comment, StringComparison.OrdinalIgnoreCase) < 0)
            text += $" — {comment}";

        if (string.IsNullOrWhiteSpace(text))
            return null;

        return new TimelineRowVm
        {
            Sender = sender,
            Text = text,
            Timestamp = FormatTimestamp(createdRaw),
            SortTicks = ParseTimelineTicks(createdRaw),
            IsSystem = true
        };
    }

    private static string BuildDiceRollDetails(Dictionary<string, object> map)
    {
        if (!map.TryGetValue("result", out var rawResult)) return string.Empty;
        var resultMap = ToObjectDictionary(rawResult);
        if (resultMap == null || !resultMap.TryGetValue("rolls", out var rawRolls)) return string.Empty;

        var values = new List<string>();
        foreach (var item in ToObjectList(rawRolls))
        {
            var value = Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
        }

        return values.Count == 0 ? string.Empty : $" ({string.Join(",", values)})";
    }

    private static (string CollectionKey, IList Items) ExtractFeedCollection(Dictionary<string, object> payload, params string[] preferredKeys)
    {
        foreach (var key in preferredKeys)
        {
            if (!payload.TryGetValue(key, out var candidate)) continue;
            if (TryGetList(candidate, out var list)) return (key, list);
        }

        foreach (var entry in payload)
        {
            if (TryGetList(entry.Value, out var list)) return ($"fallback:{entry.Key}", list);
        }

        return ("none", new ArrayList());
    }

    private static bool TryGetList(object? value, out IList list)
    {
        if (value is string)
        {
            list = new ArrayList();
            return false;
        }

        if (value is IList typedList)
        {
            list = typedList;
            return true;
        }

        if (value is IEnumerable enumerable)
        {
            var result = new ArrayList();
            foreach (var item in enumerable) result.Add(item);
            list = result;
            return true;
        }

        list = new ArrayList();
        return false;
    }

    private static Dictionary<string, object>? ToObjectDictionary(object? raw)
    {
        if (raw == null) return null;

        if (raw is Dictionary<string, object> direct)
            return new Dictionary<string, object>(direct, StringComparer.OrdinalIgnoreCase);

        if (raw is IDictionary dictionary)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(key)) map[key] = entry.Value;
            }
            if (map.Count > 0) return map;
        }

        if (raw is DictionaryEntry singleEntry)
        {
            var key = Convert.ToString(singleEntry.Key, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(key))
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { [key] = singleEntry.Value };
        }

        if (raw is IEnumerable enumerable && raw is not string)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in enumerable)
            {
                if (TryReadKeyValue(item, out var key, out var value)) map[key] = value;
            }
            if (map.Count > 0) return map;
        }

        if (TryReadKeyValue(raw, out var objectKey, out var objectValue))
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { [objectKey] = objectValue };

        var props = raw.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        if (props.Length == 0) return null;

        var propertyMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in props)
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length != 0) continue;
            var value = prop.GetValue(raw);
            if (value != null) propertyMap[prop.Name] = value;
        }

        return propertyMap.Count == 0 ? null : propertyMap;
    }

    private static bool TryReadKeyValue(object? raw, out string key, out object? value)
    {
        key = string.Empty;
        value = null;
        if (raw == null) return false;

        if (raw is DictionaryEntry entry)
        {
            key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
            value = entry.Value;
            return !string.IsNullOrWhiteSpace(key);
        }

        var type = raw.GetType();
        var keyProp = type.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public);
        var valueProp = type.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        if (keyProp == null || valueProp == null || !keyProp.CanRead || !valueProp.CanRead)
            return false;

        var rawKey = keyProp.GetValue(raw);
        key = Convert.ToString(rawKey, CultureInfo.InvariantCulture) ?? string.Empty;
        value = valueProp.GetValue(raw);
        return !string.IsNullOrWhiteSpace(key);
    }

    private static string GetStringByKeys(Dictionary<string, object> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!map.TryGetValue(key, out var rawValue) || rawValue == null) continue;
            var text = Convert.ToString(rawValue, CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        return string.Empty;
    }

    private static string BuildFallbackText(Dictionary<string, object> map)
    {
        foreach (var key in map.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var text = Convert.ToString(map[key], CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(text))
                return $"{key}: {text}";
        }

        return string.Empty;
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

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static int ParsePositiveInt(string raw, int fallback)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : fallback;

    private static int ParseInt(string raw, int fallback)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static string FormatTimestamp(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return string.Empty;

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
            return parsed.Ticks;
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
