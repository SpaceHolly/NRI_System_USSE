using Nri.Server.Configuration;
using Nri.Shared.Configuration;

namespace Nri.Server.Bootstrap;

public static class ServerConfigProvider
{
    public static ServerConfig Load(string path)
    {
        return ConfigLoader.Load(path);
    }
}
