using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using Nri.Shared.Configuration;

namespace Nri.PlayerClient.Diagnostics;

public sealed class ClientLogService
{
    private static readonly object Sync = new object();
    private static ClientLogService? _instance;

    private readonly object _writeSync = new object();
    private readonly string _appName;
    private readonly bool _preserveLogs;
    private readonly string _logFilePath;
    private readonly StreamWriter _writer;
    private bool _gracefulShutdown;
    private bool _abnormalTermination;
    private bool _completed;
    private string _lastLogLine = string.Empty;
    private string _lastLogLevel = string.Empty;
    private int _lastLogRepeatCount;

    private ClientLogService(string appName, bool preserveLogs)
    {
        _appName = appName;
        _preserveLogs = preserveLogs;
        var stamp = DateTime.Now.ToString("dd.MM.yyyy+HH.mm.ss", CultureInfo.InvariantCulture);
        _logFilePath = Path.Combine(AppContext.BaseDirectory, $"{stamp}-{appName}-log.txt");
        var stream = new FileStream(_logFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream) { AutoFlush = true };

        Info($"Application start: {_appName}");
        Info($"Log file path: {_logFilePath}");
        Info($"AppContext.BaseDirectory: {AppContext.BaseDirectory}");
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

    public static ClientConfig LoadClientConfig(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                return new ClientConfig();
            }

            using var stream = File.OpenRead(configPath);
            var serializer = new DataContractJsonSerializer(typeof(ClientConfig));
            return serializer.ReadObject(stream) as ClientConfig ?? new ClientConfig();
        }
        catch
        {
            return new ClientConfig();
        }
    }

    public string LogFilePath => _logFilePath;

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
            FlushRepeatedLogs();
            WriteCore("INFO", $"Application closing. graceful={_gracefulShutdown}, abnormal={_abnormalTermination}, preserveLogs={_preserveLogs}, keepLog={keepLog}");

            _writer.Dispose();

            if (!keepLog)
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

            if (string.Equals(_lastLogLevel, level, StringComparison.Ordinal) && string.Equals(_lastLogLine, message, StringComparison.Ordinal))
            {
                _lastLogRepeatCount++;
                return;
            }

            FlushRepeatedLogs();
            _lastLogLevel = level;
            _lastLogLine = message;
            WriteCore(level, message);
        }
    }

    private void FlushRepeatedLogs()
    {
        if (_lastLogRepeatCount <= 0)
        {
            return;
        }

        WriteCore("DEBUG", $"suppressed repeated log line count={_lastLogRepeatCount} message={_lastLogLine}");
        _lastLogRepeatCount = 0;
    }

    private void WriteCore(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var threadId = Thread.CurrentThread.ManagedThreadId;
        _writer.WriteLine($"{timestamp} [{level}] [t:{threadId}] {message}");
    }
}
