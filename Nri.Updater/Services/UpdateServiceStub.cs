using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;

namespace Nri.Updater.Services;

public class UpdateServiceStub
{
    private readonly UpdaterConfig _config;

    public UpdateServiceStub(UpdaterConfig config)
    {
        _config = config;
    }

    public string CheckForUpdatesAndLaunch()
    {
        try
        {
            var localVersion = ReadLocalVersion();
            var manifest = DownloadManifest();
            var latestVersion = manifest.Payload.ContainsKey("latestVersion") ? Convert.ToString(manifest.Payload["latestVersion"]) ?? "0.0.0" : "0.0.0";

            if (!string.Equals(localVersion, latestVersion, StringComparison.OrdinalIgnoreCase))
            {
                DownloadAndApply(manifest);
                WriteLocalVersion(latestVersion);
            }

            LaunchTarget();
            return $"Updater finished. Local={localVersion}, Latest={latestVersion}";
        }
        catch (Exception ex)
        {
            return "Updater error: " + ex.Message;
        }
    }

    private ResponseEnvelope DownloadManifest()
    {
        var url = _config.UpdateFeedUrl.TrimEnd('/') + "/manifest-" + _config.Channel + ".json";
        using var wc = new WebClient();
        var json = wc.DownloadString(url);
        var env = JsonProtocolSerializer.Deserialize<ResponseEnvelope>(json);
        if (env == null) throw new InvalidOperationException("Invalid update manifest response.");
        return env;
    }

    private void DownloadAndApply(ResponseEnvelope manifest)
    {
        if (!manifest.Payload.ContainsKey("files")) return;
        if (manifest.Payload["files"] is not System.Collections.IList files) return;

        Directory.CreateDirectory(_config.InstallFolder);
        using var wc = new WebClient();
        foreach (var file in files)
        {
            if (file is not System.Collections.Generic.Dictionary<string, object> m) continue;
            var path = Convert.ToString(m.ContainsKey("path") ? m["path"] : string.Empty) ?? string.Empty;
            var uri = Convert.ToString(m.ContainsKey("url") ? m["url"] : string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(uri)) continue;

            var localPath = Path.Combine(_config.InstallFolder, path.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            wc.DownloadFile(uri, localPath);
        }
    }

    private string ReadLocalVersion()
    {
        if (!File.Exists(_config.LocalVersionFile)) return "0.0.0";
        return File.ReadAllText(_config.LocalVersionFile).Trim();
    }

    private void WriteLocalVersion(string version)
    {
        var dir = Path.GetDirectoryName(_config.LocalVersionFile);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_config.LocalVersionFile, version);
    }

    private void LaunchTarget()
    {
        var path = string.Equals(_config.LaunchTarget, "Admin", StringComparison.OrdinalIgnoreCase)
            ? _config.AdminClientExecutable
            : _config.PlayerClientExecutable;

        if (!File.Exists(path)) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
