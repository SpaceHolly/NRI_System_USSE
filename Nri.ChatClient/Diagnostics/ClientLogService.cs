using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Nri.ChatClient.Diagnostics;

public sealed class ClientLogService
{
    private static readonly object Sync = new object();
    private static ClientLogService? _instance;

    private readonly object _writeSync = new object();
    private readonly string _appName;
    private readonly bool _preserveLogs;
    private readonly bool _enabled;
    private readonly string? _logFilePath;
    private readonly StreamWriter? _writer;
    private bool _gracefulShutdown;
    private bool _abnormalTermination;
    private bool _completed;

    private ClientLogService(string appName, bool preserveLogs)
    {
        _appName = appName;
        _preserveLogs = preserveLogs;
#if CHATCLIENT_FILE_LOGS
        _enabled = true;
        var stamp = DateTime.Now.ToString("dd.MM.yyyy+HH.mm.ss", CultureInfo.InvariantCulture);
        _logFilePath = Path.Combine(AppContext.BaseDirectory, $"{stamp}-{appName}-log.txt");
        var stream = new FileStream(_logFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream) { AutoFlush = true };
#else
        _enabled = false;
        _logFilePath = null;
        _writer = null;
#endif

        Info($"Application start: {_appName}");
        if (_enabled && _logFilePath is not null)
        {
            Info($"Log file path: {_logFilePath}");
        }
        Info($"PreserveClientLogs: {_preserveLogs}");
    }

    public static ClientLogService Initialize(string appName, bool preserveLogs)
    {
        lock (Sync)
        {
            _instance ??= new ClientLogService(appName, preserveLogs);
            return _instance;
        }
    }

    public static ClientLogService Instance => _instance ?? throw new InvalidOperationException("ClientLogService is not initialized.");

    public void Info(string message) => Write("INFO", message);
    public void Debug(string message) => Write("DEBUG", message);
    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? ex = null)
    {
        if (ex == null)
        {
            Write("ERROR", message);
            return;
        }

        Write("ERROR", message + " | " + ex);
    }

    public void MarkGracefulShutdown(string reason)
    {
        _gracefulShutdown = true;
        Info("Graceful shutdown requested: " + reason);
    }

    public void MarkAbnormalTermination(string reason, Exception? ex = null)
    {
        _abnormalTermination = true;
        Error("Abnormal termination detected: " + reason, ex);
    }

    public void CompleteLifetime()
    {
        lock (_writeSync)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            var keepLog = _preserveLogs || !_gracefulShutdown || _abnormalTermination;
            WriteCore("INFO", $"Application closing. graceful={_gracefulShutdown}, abnormal={_abnormalTermination}, preserveLogs={_preserveLogs}, keepLog={keepLog}");
            _writer?.Dispose();

            if (!keepLog && _logFilePath is not null)
            {
                try
                {
                    File.Delete(_logFilePath);
                }
                catch
                {
                    // noop
                }
            }
        }
    }

    private void Write(string level, string message)
    {
        lock (_writeSync)
        {
            if (_completed)
            {
                return;
            }

            WriteCore(level, message);
        }
    }

    private void WriteCore(string level, string message)
    {
        if (!_enabled || _writer is null)
        {
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var threadId = Thread.CurrentThread.ManagedThreadId;
        _writer.WriteLine($"{timestamp} [{level}] [t:{threadId}] {message}");
    }
}
