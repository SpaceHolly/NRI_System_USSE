using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Nri.ChatClient.Diagnostics;
using Nri.ChatClient.Views;
using Nri.Shared.Configuration;
using Nri.Shared.Diagnostics;

namespace Nri.ChatClient;

public partial class App : Application
{
    private DispatcherTimer? _performanceTimer;
    private DateTime _performanceExpectedAtUtc;
    public static ClientConfig ClientConfig { get; } = new ClientConfig
    {
        ServerHost = "127.0.0.1",
        ServerPort = 4600,
        PreserveClientLogs = false
    };

    protected override void OnStartup(StartupEventArgs e)
    {
        var logger = ClientLogService.Initialize("ChatClient", ClientConfig.PreserveClientLogs);
        PerformanceTelemetry0214.Initialize("ChatClient");
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

        _performanceExpectedAtUtc = DateTime.UtcNow.AddMilliseconds(250);
        _performanceTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _performanceTimer.Tick += OnPerformanceHeartbeat;
        _performanceTimer.Start();
        PerformanceTelemetry0214.Current.IncrementCounter("active_timers");

        var window = new MainShellWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_performanceTimer != null)
        {
            _performanceTimer.Stop();
            _performanceTimer.Tick -= OnPerformanceHeartbeat;
            PerformanceTelemetry0214.Current.IncrementCounter("active_timers", -1);
        }
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

    private void OnPerformanceHeartbeat(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        PerformanceTelemetry0214.Current.RecordUiLag(Math.Max(0d, (now - _performanceExpectedAtUtc).TotalMilliseconds));
        _performanceExpectedAtUtc = now.AddMilliseconds(250);
    }
}
