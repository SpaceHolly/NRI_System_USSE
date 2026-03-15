using System.IO;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;

namespace Nri.Server.Configuration;

public static class ConfigLoader
{
    public static ServerConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ServerConfig();
        }

        var json = File.ReadAllText(path);
        return JsonProtocolSerializer.Deserialize<ServerConfig>(json) ?? new ServerConfig();
    }
}
