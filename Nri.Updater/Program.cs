using System;
using System.IO;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;
using Nri.Updater.Services;

namespace Nri.Updater;

internal static class Program
{
    private static void Main(string[] args)
    {
        var config = LoadConfig("updater.config.json");
        var updater = new UpdateServiceStub(config);
        Console.WriteLine(updater.CheckForUpdatesAndLaunch());
    }

    private static UpdaterConfig LoadConfig(string path)
    {
        if (!File.Exists(path))
        {
            return new UpdaterConfig();
        }

        return JsonProtocolSerializer.Deserialize<UpdaterConfig>(File.ReadAllText(path)) ?? new UpdaterConfig();
    }
}
