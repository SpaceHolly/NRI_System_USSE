using System;
using System.IO;
using System.Text.Json;
using Nri.Server.Logging;

namespace Nri.Server.FateEngine;

public enum FateEngineSettingsLoadSource
{
    MainFile,
    BackupFile,
    Default
}

public sealed class FateEngineSettingsLoadResult
{
    public FateEngineSettings Settings { get; set; } = FateEngineSettings.CreateDefault().Normalize();
    public FateEngineSettingsLoadSource Source { get; set; } = FateEngineSettingsLoadSource.Default;
}

public sealed class FateEngineSettingsStore
{
    private readonly IServerLogger _logger;
    private readonly string _directoryPath;
    private readonly string _mainFilePath;
    private readonly string _backupFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

    public FateEngineSettingsStore(IServerLogger logger, string? rootPath = null)
    {
        _logger = logger;
        var basePath = string.IsNullOrWhiteSpace(rootPath) ? AppContext.BaseDirectory : rootPath;
        _directoryPath = Path.Combine(basePath, "ServerData", "fate");
        _mainFilePath = Path.Combine(_directoryPath, "fate_engine_settings.json");
        _backupFilePath = Path.Combine(_directoryPath, "fate_engine_settings.bak.json");
    }

    public FateEngineSettingsLoadResult LoadOrCreateDefault()
    {
        Directory.CreateDirectory(_directoryPath);

        var main = TryLoad(_mainFilePath);
        if (main != null)
        {
            return new FateEngineSettingsLoadResult { Settings = main, Source = FateEngineSettingsLoadSource.MainFile };
        }

        var backup = TryLoadBackup();
        if (backup != null)
        {
            Save(backup);
            return new FateEngineSettingsLoadResult { Settings = backup, Source = FateEngineSettingsLoadSource.BackupFile };
        }

        var fallback = FateEngineSettings.CreateDefault().Normalize();
        Save(fallback);
        return new FateEngineSettingsLoadResult { Settings = fallback, Source = FateEngineSettingsLoadSource.Default };
    }

    public FateEngineSettings? TryLoadBackup() => TryLoad(_backupFilePath);

    public bool Save(FateEngineSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_directoryPath);
            var normalized = (settings ?? FateEngineSettings.CreateDefault()).Normalize();
            var json = JsonSerializer.Serialize(normalized, JsonOptions);

            if (File.Exists(_mainFilePath))
            {
                File.Copy(_mainFilePath, _backupFilePath, true);
            }

            File.WriteAllText(_mainFilePath, json);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Debug($"fate.settings.store.save.failed error={ex.GetType().Name} message={ex.Message}");
            return false;
        }
    }

    private FateEngineSettings? TryLoad(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<FateEngineSettings>(json);
            return (settings ?? FateEngineSettings.CreateDefault()).Normalize();
        }
        catch (Exception ex)
        {
            _logger.Debug($"fate.settings.store.load.failed file={filePath} error={ex.GetType().Name} message={ex.Message}");
            return null;
        }
    }
}
