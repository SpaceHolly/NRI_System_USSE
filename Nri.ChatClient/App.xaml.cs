using System;
using System.Threading.Tasks;
using System.Windows;
using Nri.ChatClient.Diagnostics;
using Nri.ChatClient.Views;
using Nri.Shared.Configuration;

namespace Nri.ChatClient;

public partial class App : Application
{
    public static ClientConfig ClientConfig { get; } = new ClientConfig
    {
        ServerHost = "127.0.0.1",
        ServerPort = 4600,
        PreserveClientLogs = false
    };

    protected override void OnStartup(StartupEventArgs e)
    {
        var logger = ClientLogService.Initialize("ChatClient", ClientConfig.PreserveClientLogs);
        logger.Info($"Loaded client config defaults: host={ClientConfig.ServerHost}, port={ClientConfig.ServerPort}, preserveClientLogs={ClientConfig.PreserveClientLogs}");

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
