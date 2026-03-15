using Nri.Shared.Configuration;

namespace Nri.Updater.Services;

public class UpdateServiceStub
{
    private readonly UpdaterConfig _config;

    public UpdateServiceStub(UpdaterConfig config)
    {
        _config = config;
    }

    public string CheckForUpdates()
    {
        return $"Updater stub. Feed={_config.UpdateFeedUrl}, Channel={_config.Channel}";
    }
}
