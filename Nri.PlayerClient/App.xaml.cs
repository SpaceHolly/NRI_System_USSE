using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Views;
using Nri.Shared.Configuration;

namespace Nri.PlayerClient;

public partial class App : Application
{
    public static ClientConfig ClientConfig { get; private set; } = new ClientConfig();

    protected override void OnStartup(StartupEventArgs e)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "client.config.json");
        ClientConfig = ClientLogService.LoadClientConfig(configPath);
        var logger = ClientLogService.Initialize("PlayerClient", ClientConfig.PreserveClientLogs);
        logger.Info("Config load attempt path=" + configPath);
        logger.Info($"Loaded client config: host={ClientConfig.ServerHost}, port={ClientConfig.ServerPort}, preserveClientLogs={ClientConfig.PreserveClientLogs}");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            logger.MarkAbnormalTermination("AppDomain.CurrentDomain.UnhandledException", exception);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            logger.MarkAbnormalTermination("Application.DispatcherUnhandledException", args.Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.MarkAbnormalTermination("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        Exit += (_, _) => logger.MarkGracefulShutdown("Application.Exit");

        base.OnStartup(e);

        var window = new MainShellWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            ClientLogService.Instance.CompleteLifetime();
        }
        catch
        {
            // noop
        }

        base.OnExit(e);
    }
}
