using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Views;
using Nri.Shared.Configuration;
using Nri.Shared.Diagnostics;
using Nri.Ui.Wpf.Diagnostics;

namespace Nri.AdminClient;

public partial class App : Application
{
    private WpfPerformanceMonitor0214? _performanceMonitor;
    public static ClientConfig ClientConfig { get; private set; } = new ClientConfig();

    protected override void OnStartup(StartupEventArgs e)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "client.config.json");
        ClientConfig = ClientLogService.LoadClientConfig(configPath);
        var logger = ClientLogService.Initialize("AdminClient", ClientConfig.PreserveClientLogs);
        PerformanceTelemetry0214.Initialize("AdminClient");
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

        EnsureThemeFallbackResources();

        base.OnStartup(e);
        _performanceMonitor = new WpfPerformanceMonitor0214(Dispatcher);

        var window = new MainShellWindow();
        MainWindow = window;
        window.Show();
    }

    private static void EnsureThemeFallbackResources()
    {
        var resources = Current?.Resources;
        if (resources == null)
            return;

        EnsureBrush(resources, "MutedTextBrush", "#FF9AA7C7");
        EnsureBrush(resources, "MutedBrush", "#FF9AA7C7");
        EnsureBrush(resources, "TextBrush", "#FF43D397");
        EnsureBrush(resources, "TextSecondaryBrush", "#FF43F397");
        EnsureBrush(resources, "BgBrush", "#FF111827");
        EnsureBrush(resources, "PanelBrush", "#FF182235");
        EnsureBrush(resources, "PanelAltBrush", "#FF1E2A40");
        EnsureBrush(resources, "PanelSoftBrush", "#FF223452");
        EnsureBrush(resources, "BorderBrushTheme", "#FF2C3A57");
        EnsureBrush(resources, "AccentBrush", "#FF4EA3FF");
        EnsureBrush(resources, "AccentStrongBrush", "#FF2F87E8");
        EnsureBrush(resources, "DangerBrush", "#FFFF6B6B");
        EnsureBrush(resources, "SuccessBrush", "#FF43D397");
        EnsureBrush(resources, "WarningBrush", "#FFF2B84B");
        EnsureColor(resources, "ColorBg", "#FF111827");

        EnsureStyle(resources, "CardStyle", typeof(Border), _ =>
            new Style(typeof(Border))
            {
                Setters =
                {
                    new Setter(Border.BackgroundProperty, GetBrush(resources, "PanelBrush")),
                    new Setter(Border.BorderBrushProperty, GetBrush(resources, "BorderBrushTheme")),
                    new Setter(Border.BorderThicknessProperty, new Thickness(1)),
                    new Setter(Border.CornerRadiusProperty, new CornerRadius(8)),
                    new Setter(Border.PaddingProperty, new Thickness(12)),
                    new Setter(Border.MarginProperty, new Thickness(4)),
                }
            });

        EnsureStyle(resources, "SubtleCardStyle", typeof(Border), _ =>
            new Style(typeof(Border), (Style?)resources["CardStyle"])
            {
                Setters = { new Setter(Border.BackgroundProperty, GetBrush(resources, "PanelAltBrush")) }
            });

        EnsureStyle(resources, "HintCardStyle", typeof(Border), _ =>
            new Style(typeof(Border), (Style?)resources["SubtleCardStyle"])
            {
                Setters = { new Setter(Border.BorderBrushProperty, GetBrush(resources, "WarningBrush")) }
            });

        EnsureStyle(resources, "GhostButtonStyle", typeof(Button), _ =>
            new Style(typeof(Button))
            {
                Setters =
                {
                    new Setter(Control.BackgroundProperty, GetBrush(resources, "PanelSoftBrush")),
                    new Setter(Control.BorderBrushProperty, GetBrush(resources, "BorderBrushTheme")),
                    new Setter(Control.ForegroundProperty, GetBrush(resources, "TextBrush")),
                    new Setter(Control.PaddingProperty, new Thickness(12, 6, 12, 6)),
                    new Setter(Control.BorderThicknessProperty, new Thickness(1)),
                    new Setter(Control.MarginProperty, new Thickness(2)),
                    new Setter(Control.CursorProperty, Cursors.Hand),
                }
            });

        EnsureStyle(resources, "SectionNavButtonStyle", typeof(Button), _ =>
            new Style(typeof(Button), GetStyle(resources, "GhostButtonStyle"))
            {
                Setters =
                {
                    new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                    new Setter(Control.PaddingProperty, new Thickness(14, 10, 14, 10)),
                    new Setter(Control.MarginProperty, new Thickness(0, 0, 0, 6)),
                }
            });

        EnsureStyle(resources, "StatusBadgeStyle", typeof(Border), _ =>
            new Style(typeof(Border))
            {
                Setters =
                {
                    new Setter(Border.CornerRadiusProperty, new CornerRadius(10)),
                    new Setter(Border.PaddingProperty, new Thickness(8, 3, 8, 3)),
                    new Setter(Border.BackgroundProperty, GetBrush(resources, "DangerBrush")),
                }
            });

        EnsureStyle(resources, "SummaryMetricCardStyle", typeof(Border), _ =>
            new Style(typeof(Border), (Style?)resources["SubtleCardStyle"])
            {
                Setters =
                {
                    new Setter(Border.PaddingProperty, new Thickness(10)),
                    new Setter(Border.MinHeightProperty, 90d),
                }
            });

        EnsureResource(resources, "BooleanToVisibilityConverter",
            new BooleanToVisibilityConverter());
        EnsureResource(resources, "BoolToVisibilityConverter",
            new BooleanToVisibilityConverter());
    }

    private static Brush GetBrush(ResourceDictionary resources, string key)
    {
        return resources[key] as Brush ?? new SolidColorBrush(Colors.Transparent);
    }

    private static Style? GetStyle(ResourceDictionary resources, string key)
    {
        return resources[key] as Style;
    }

    private static void EnsureBrush(ResourceDictionary resources, string key, string hex)
    {
        if (!resources.Contains(key))
        {
            resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        }
    }

    private static void EnsureColor(ResourceDictionary resources, string key, string hex)
    {
        if (!resources.Contains(key))
        {
            resources[key] = (Color)ColorConverter.ConvertFromString(hex)!;
        }
    }

    private static void EnsureStyle(ResourceDictionary resources, string key, Type targetType, Func<ResourceDictionary, Style> factory)
    {
        if (resources.Contains(key))
            return;

        resources[key] = factory(resources);
    }

    private static void EnsureResource<T>(ResourceDictionary resources, string key, T value)
    {
        if (!resources.Contains(key))
        {
            resources[key] = value!;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _performanceMonitor?.Dispose();
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
