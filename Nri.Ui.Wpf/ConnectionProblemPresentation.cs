using System;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Nri.Ui.Wpf;

public enum ConnectionProblemKind
{
    None,
    ServerUnavailable,
    ConnectionRefused,
    ConnectionLost,
    Timeout,
    AuthenticationRequired,
    AuthenticationExpired,
    InvalidCredentials,
    AccountPendingApproval,
    PermissionDenied,
    ProtocolError,
    Unknown
}

public sealed class ConnectionProblemPresentation
{
    public ConnectionProblemKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public bool CanRetry { get; set; }
    public bool ShowLoginAction { get; set; }
    public bool ShowAdvancedDetails { get; set; }
    public string Severity { get; set; } = "Info";

    public string UserMessage => string.IsNullOrWhiteSpace(RecommendedAction)
        ? Summary
        : $"{Summary} {RecommendedAction}";
}

public static class ConnectionProblemMapper
{
    private static readonly Regex EndpointPattern = new(
        @"(?i)\b(?:(?:\d{1,3}\.){3}\d{1,3}|localhost):\d{2,5}\b",
        RegexOptions.Compiled);

    private static readonly Regex BrokenFragmentPattern = new(
        @"(?i)^\s*[:;,\-]\s*\S*\s*$|^\s*\S+\s*[:;,\-]\s*$|^\s*сервер\s*[:;,\-]?\s*$|^\s*[:;,\-]?\s*сервер\s*$",
        RegexOptions.Compiled);

    public static ConnectionProblemPresentation FromException(Exception exception)
    {
        if (exception == null) return FromKind(ConnectionProblemKind.Unknown);
        return FromRawMessage(exception.Message, exception);
    }

    public static ConnectionProblemPresentation FromRawMessage(string? message, Exception? exception = null)
    {
        return FromKind(Classify(message, exception));
    }

    public static string ToUserMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        if (IsSafeUserMessage(message)) return message.Trim();
        return FromRawMessage(message).UserMessage;
    }

    public static string ToUserMessage(Exception exception)
        => FromException(exception).UserMessage;

    public static bool IsSafeUserMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var text = message.Trim();
        if (EndpointPattern.IsMatch(text) || BrokenFragmentPattern.IsMatch(text)) return false;
        if (ContainsTechnicalToken(text)) return false;
        return true;
    }

    public static bool ContainsForbiddenNormalUiToken(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var text = message.Trim();
        return EndpointPattern.IsMatch(text)
            || BrokenFragmentPattern.IsMatch(text)
            || ContainsTechnicalToken(text);
    }

    private static ConnectionProblemKind Classify(string? message, Exception? exception)
    {
        if (exception is TimeoutException) return ConnectionProblemKind.Timeout;
        if (exception is SocketException) return ConnectionProblemKind.ConnectionRefused;
        if (exception is IOException) return ConnectionProblemKind.ConnectionLost;
        if (exception?.InnerException is TimeoutException) return ConnectionProblemKind.Timeout;
        if (exception?.InnerException is SocketException) return ConnectionProblemKind.ConnectionRefused;
        if (exception?.InnerException is IOException) return ConnectionProblemKind.ConnectionLost;

        var text = (message ?? string.Empty).Trim();
        if (text.Length == 0) return ConnectionProblemKind.Unknown;
        var lower = text.ToLowerInvariant();

        if (lower.Contains("auth token is invalid") || lower.Contains("invalid token"))
            return ConnectionProblemKind.AuthenticationExpired;
        if (lower.Contains("unauthorized"))
            return ConnectionProblemKind.AuthenticationRequired;
        if (lower.Contains("invalid credentials") || lower.Contains("wrong password") || lower.Contains("неверный логин"))
            return ConnectionProblemKind.InvalidCredentials;
        if (lower.Contains("pending approval") || lower.Contains("ожидает подтверждения"))
            return ConnectionProblemKind.AccountPendingApproval;
        if (lower.Contains("permission denied") || lower.Contains("forbidden") || lower.Contains("нет доступа"))
            return ConnectionProblemKind.PermissionDenied;
        if (lower.Contains("timeout") || lower.Contains("timed out"))
            return ConnectionProblemKind.Timeout;
        if (lower.Contains("connection refused") || lower.Contains("actively refused") || lower.Contains("подключение не установлено"))
            return ConnectionProblemKind.ConnectionRefused;
        if (lower.Contains("failed to connect") || lower.Contains("сервер недоступен"))
            return ConnectionProblemKind.ServerUnavailable;
        if (lower.Contains("socketexception") || lower.Contains("ioexception") || lower.Contains("httprequestexception"))
            return ConnectionProblemKind.ConnectionLost;
        if (lower.Contains("protocol") || lower.Contains("deserialize") || lower.Contains("invalid response"))
            return ConnectionProblemKind.ProtocolError;
        if (EndpointPattern.IsMatch(text) || BrokenFragmentPattern.IsMatch(text) || ContainsTechnicalToken(text))
            return ConnectionProblemKind.Unknown;

        return ConnectionProblemKind.None;
    }

    private static bool ContainsTechnicalToken(string text)
    {
        return text.IndexOf("auth token", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("invalid token", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("SocketException", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("IOException", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("HttpRequestException", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("connection refused", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("actively refused", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("E_NOINTERFACE", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("0x80004002", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static ConnectionProblemPresentation FromKind(ConnectionProblemKind kind)
    {
        switch (kind)
        {
            case ConnectionProblemKind.None:
                return new ConnectionProblemPresentation
                {
                    Kind = kind,
                    Title = "Состояние подключения",
                    Summary = "Состояние обновлено.",
                    Severity = "Info"
                };
            case ConnectionProblemKind.ServerUnavailable:
            case ConnectionProblemKind.ConnectionRefused:
                return new ConnectionProblemPresentation
                {
                    Kind = kind,
                    Title = "Сервер недоступен",
                    Summary = "Не удалось подключиться к серверу.",
                    RecommendedAction = "Проверьте, запущен ли сервер, и повторите попытку.",
                    CanRetry = true,
                    ShowAdvancedDetails = true,
                    Severity = "Warning"
                };
            case ConnectionProblemKind.ConnectionLost:
                return new ConnectionProblemPresentation
                {
                    Kind = kind,
                    Title = "Соединение потеряно",
                    Summary = "Соединение с сервером потеряно.",
                    RecommendedAction = "Программа попробует восстановить подключение.",
                    CanRetry = true,
                    ShowAdvancedDetails = true,
                    Severity = "Warning"
                };
            case ConnectionProblemKind.Timeout:
                return new ConnectionProblemPresentation
                {
                    Kind = kind,
                    Title = "Сервер не ответил",
                    Summary = "Сервер не ответил вовремя.",
                    RecommendedAction = "Повторите попытку через несколько секунд.",
                    CanRetry = true,
                    ShowAdvancedDetails = true,
                    Severity = "Warning"
                };
            case ConnectionProblemKind.AuthenticationRequired:
            case ConnectionProblemKind.AuthenticationExpired:
                return new ConnectionProblemPresentation
                {
                    Kind = kind,
                    Title = "Требуется вход",
                    Summary = "Сеанс входа завершён.",
                    RecommendedAction = "Войдите в учётную запись снова.",
                    CanRetry = true,
                    ShowLoginAction = true,
                    Severity = "Warning"
                };
            case ConnectionProblemKind.InvalidCredentials:
                return new ConnectionProblemPresentation
                {
                    Kind = kind,
                    Title = "Ошибка входа",
                    Summary = "Неверный логин или пароль.",
                    RecommendedAction = "Проверьте введённые данные.",
                    ShowLoginAction = true,
                    Severity = "Warning"
                };
            case ConnectionProblemKind.AccountPendingApproval:
                return new ConnectionProblemPresentation
                {
                    Kind = kind,
                    Title = "Учётная запись ожидает подтверждения",
                    Summary = "Учётная запись ожидает подтверждения администратора.",
                    Severity = "Warning"
                };
            case ConnectionProblemKind.PermissionDenied:
                return new ConnectionProblemPresentation
                {
                    Kind = kind,
                    Title = "Нет доступа",
                    Summary = "У вашей учётной записи нет доступа к этому действию.",
                    Severity = "Warning"
                };
            case ConnectionProblemKind.ProtocolError:
                return new ConnectionProblemPresentation
                {
                    Kind = kind,
                    Title = "Ошибка ответа сервера",
                    Summary = "Сервер вернул ответ, который программа не смогла обработать.",
                    RecommendedAction = "Повторите попытку. Технические сведения записаны в журнал.",
                    CanRetry = true,
                    ShowAdvancedDetails = true,
                    Severity = "Error"
                };
            default:
                return new ConnectionProblemPresentation
                {
                    Kind = ConnectionProblemKind.Unknown,
                    Title = "Ошибка подключения",
                    Summary = "Произошла ошибка подключения.",
                    RecommendedAction = "Повторите попытку или откройте технические сведения.",
                    CanRetry = true,
                    ShowAdvancedDetails = true,
                    Severity = "Error"
                };
        }
    }
}
