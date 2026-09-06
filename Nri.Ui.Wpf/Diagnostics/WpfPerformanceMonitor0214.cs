using System;
using System.Windows.Threading;
using Nri.Shared.Diagnostics;

namespace Nri.Ui.Wpf.Diagnostics;

public sealed class WpfPerformanceMonitor0214 : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly TimeSpan _interval;
    private DateTime _expectedAtUtc;
    private bool _disposed;

    public WpfPerformanceMonitor0214(Dispatcher dispatcher, TimeSpan? interval = null)
    {
        if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
        _interval = interval ?? TimeSpan.FromMilliseconds(250);
        _expectedAtUtc = DateTime.UtcNow + _interval;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = _interval
        };
        _timer.Tick += OnTick;
        _timer.Start();
        PerformanceTelemetry0214.Current.IncrementCounter("active_timers");
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var lag = Math.Max(0d, (now - _expectedAtUtc).TotalMilliseconds);
        PerformanceTelemetry0214.Current.RecordUiLag(lag);
        _expectedAtUtc = now + _interval;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        PerformanceTelemetry0214.Current.IncrementCounter("active_timers", -1);
    }
}
