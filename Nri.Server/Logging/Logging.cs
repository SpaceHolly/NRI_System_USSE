using System;
using System.IO;
using Nri.Shared.Configuration;

namespace Nri.Server.Logging;

public interface IServerLogger
{
    void Debug(string message);
    void Session(string message);
    void Admin(string message);
    void Audit(string message);
}

public class CompositeLogger : IServerLogger
{
    private readonly LoggingConfig _config;
    private static readonly object Sync = new object();

    public CompositeLogger(LoggingConfig config)
    {
        _config = config;
    }

    public void Debug(string message) => Write(_config.DebugLogPath, "DEBUG", message);
    public void Session(string message) => Write(_config.SessionLogPath, "SESSION", message);
    public void Admin(string message) => Write(_config.AdminLogPath, "ADMIN", message);
    public void Audit(string message) => Write(_config.AuditLogPath, "AUDIT", message);

    private static void Write(string path, string category, string message)
    {
        var line = $"{DateTime.UtcNow:O} [{category}] {message}{Environment.NewLine}";
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lock (Sync)
        {
            File.AppendAllText(path, line);
        }
        File.AppendAllText(path, line);
    }
}
