using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Nri.Shared.Utilities;

public sealed class MapViewportState
{
    private const double Epsilon = 0.000000001d;
    private const double DefaultResetZoom = 1d;

    public MapViewportState(
        double mapWidthMeters,
        double mapHeightMeters,
        double viewportWidthPixels,
        double viewportHeightPixels,
        double gridSizeMeters)
    {
        MapWidthMeters = Positive(mapWidthMeters, 1d);
        MapHeightMeters = Positive(mapHeightMeters, 1d);
        ViewportWidthPixels = Positive(viewportWidthPixels, 1d);
        ViewportHeightPixels = Positive(viewportHeightPixels, 1d);
        GridSizeMeters = Positive(gridSizeMeters, 1d);
        RecalculateLimits();
        FitMap();
    }

    public double MapWidthMeters { get; private set; }
    public double MapHeightMeters { get; private set; }
    public double ViewportWidthPixels { get; private set; }
    public double ViewportHeightPixels { get; private set; }
    public double Zoom { get; private set; }
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }
    public double MinZoom { get; private set; }
    public double MaxZoom { get; private set; }
    public double GridSizeMeters { get; private set; }
    public MapPoint? CursorWorldPosition { get; private set; }
    public double ZoomPercent => Zoom / DefaultResetZoom * 100d;
    public string ZoomDisplay => ZoomPercent.ToString("0.#", CultureInfo.CurrentCulture) + "%";
    public bool CanZoomIn => Zoom < MaxZoom - Epsilon;
    public bool CanZoomOut => Zoom > MinZoom + Epsilon;

    public void SetMap(double widthMeters, double heightMeters, double gridSizeMeters, bool fit = true)
    {
        MapWidthMeters = Positive(widthMeters, 1d);
        MapHeightMeters = Positive(heightMeters, 1d);
        GridSizeMeters = Positive(gridSizeMeters, 1d);
        RecalculateLimits();
        if (fit) FitMap();
        else
        {
            Zoom = ClampFinite(Zoom, MinZoom, MaxZoom, FitZoom());
            ClampToMap();
        }
    }

    public MapPoint WorldToScreen(MapPoint world)
        => new MapPoint(world.X * Zoom + OffsetX, world.Y * Zoom + OffsetY);

    public MapPoint ScreenToWorld(MapPoint screen)
    {
        var safeZoom = ClampFinite(Zoom, MinZoom, MaxZoom, FitZoom());
        return new MapPoint((screen.X - OffsetX) / safeZoom, (screen.Y - OffsetY) / safeZoom);
    }

    public void PanByPixels(double deltaX, double deltaY)
    {
        OffsetX = Finite(OffsetX + Finite(deltaX, 0d), OffsetX);
        OffsetY = Finite(OffsetY + Finite(deltaY, 0d), OffsetY);
        ClampToMap();
    }

    public void ZoomAtScreenPoint(double requestedZoom, MapPoint screenAnchor)
    {
        var worldAnchor = ScreenToWorld(screenAnchor);
        Zoom = ClampFinite(requestedZoom, MinZoom, MaxZoom, Zoom);
        OffsetX = screenAnchor.X - worldAnchor.X * Zoom;
        OffsetY = screenAnchor.Y - worldAnchor.Y * Zoom;
        ClampToMap();
    }

    public void ZoomByFactor(double factor, MapPoint screenAnchor)
    {
        var safeFactor = Positive(factor, 1d);
        ZoomAtScreenPoint(Zoom * safeFactor, screenAnchor);
    }

    public void FitMap(double paddingPixels = 16d)
        => FitBounds(new MapRect(0d, 0d, MapWidthMeters, MapHeightMeters), paddingPixels);

    public void FitBounds(MapRect bounds, double paddingPixels = 16d)
    {
        var normalized = bounds.Normalize();
        var width = Positive(normalized.Width, 1d);
        var height = Positive(normalized.Height, 1d);
        var padding = Math.Max(0d, Finite(paddingPixels, 0d));
        var availableWidth = Math.Max(1d, ViewportWidthPixels - padding * 2d);
        var availableHeight = Math.Max(1d, ViewportHeightPixels - padding * 2d);
        Zoom = ClampFinite(Math.Min(availableWidth / width, availableHeight / height), MinZoom, MaxZoom, FitZoom());
        var center = normalized.Center;
        OffsetX = ViewportWidthPixels / 2d - center.X * Zoom;
        OffsetY = ViewportHeightPixels / 2d - center.Y * Zoom;
        ClampToMap();
    }

    public void Reset()
    {
        Zoom = ClampFinite(DefaultResetZoom, MinZoom, MaxZoom, FitZoom());
        OffsetX = MapWidthMeters * Zoom < ViewportWidthPixels
            ? (ViewportWidthPixels - MapWidthMeters * Zoom) / 2d
            : 0d;
        OffsetY = MapHeightMeters * Zoom < ViewportHeightPixels
            ? (ViewportHeightPixels - MapHeightMeters * Zoom) / 2d
            : 0d;
        ClampToMap();
    }

    public void ResizeViewport(double widthPixels, double heightPixels)
    {
        var anchor = ScreenToWorld(new MapPoint(ViewportWidthPixels / 2d, ViewportHeightPixels / 2d));
        ViewportWidthPixels = Positive(widthPixels, 1d);
        ViewportHeightPixels = Positive(heightPixels, 1d);
        RecalculateLimits();
        Zoom = ClampFinite(Zoom, MinZoom, MaxZoom, FitZoom());
        OffsetX = ViewportWidthPixels / 2d - anchor.X * Zoom;
        OffsetY = ViewportHeightPixels / 2d - anchor.Y * Zoom;
        ClampToMap();
    }

    public void ClampToMap()
    {
        Zoom = ClampFinite(Zoom, MinZoom, MaxZoom, FitZoom());
        OffsetX = ClampAxis(OffsetX, MapWidthMeters * Zoom, ViewportWidthPixels);
        OffsetY = ClampAxis(OffsetY, MapHeightMeters * Zoom, ViewportHeightPixels);
    }

    public MapPoint ClampWorldPoint(MapPoint world)
        => new MapPoint(Clamp(world.X, 0d, MapWidthMeters), Clamp(world.Y, 0d, MapHeightMeters));

    public MapPoint SnapWorldPoint(MapPoint world, double? snapStepMeters = null)
    {
        var step = Positive(snapStepMeters ?? GridSizeMeters, GridSizeMeters);
        var clamped = ClampWorldPoint(world);
        return new MapPoint(
            Clamp(Math.Round(clamped.X / step, MidpointRounding.AwayFromZero) * step, 0d, MapWidthMeters),
            Clamp(Math.Round(clamped.Y / step, MidpointRounding.AwayFromZero) * step, 0d, MapHeightMeters));
    }

    public void UpdateCursor(MapPoint screenPosition, bool clampToMap = true)
    {
        var world = ScreenToWorld(screenPosition);
        CursorWorldPosition = clampToMap ? ClampWorldPoint(world) : world;
    }

    public MapRect VisibleWorldBounds()
    {
        var topLeft = ScreenToWorld(new MapPoint(0d, 0d));
        var bottomRight = ScreenToWorld(new MapPoint(ViewportWidthPixels, ViewportHeightPixels));
        var bounds = new MapRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y).Normalize();
        var left = Clamp(bounds.X, 0d, MapWidthMeters);
        var top = Clamp(bounds.Y, 0d, MapHeightMeters);
        var right = Clamp(bounds.Right, 0d, MapWidthMeters);
        var bottom = Clamp(bounds.Bottom, 0d, MapHeightMeters);
        return new MapRect(left, top, Math.Max(0d, right - left), Math.Max(0d, bottom - top));
    }

    private void RecalculateLimits()
    {
        var fit = FitZoom();
        MinZoom = Math.Max(0.0001d, fit * 0.25d);
        MaxZoom = Math.Max(8d, Math.Min(32d, fit * 8d));
        if (MaxZoom < MinZoom) MaxZoom = MinZoom;
        Zoom = ClampFinite(Zoom, MinZoom, MaxZoom, fit);
    }

    private double FitZoom()
        => Math.Max(0.0001d, Math.Min(ViewportWidthPixels / MapWidthMeters, ViewportHeightPixels / MapHeightMeters));

    private static double ClampAxis(double offset, double contentPixels, double viewportPixels)
    {
        if (contentPixels <= viewportPixels)
            return (viewportPixels - contentPixels) / 2d;
        return Clamp(Finite(offset, 0d), viewportPixels - contentPixels, 0d);
    }

    private static double Positive(double value, double fallback)
        => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d ? value : fallback;

    private static double Finite(double value, double fallback)
        => double.IsNaN(value) || double.IsInfinity(value) ? fallback : value;

    private static double ClampFinite(double value, double min, double max, double fallback)
        => Clamp(Finite(value, fallback), min, max);

    private static double Clamp(double value, double min, double max)
        => value < min ? min : value > max ? max : value;
}

public static class MapGridLodCalculator
{
    public static MapGridLod Calculate(double authoredGridMeters, double zoomPixelsPerMeter, double minimumMinorPixels = 24d)
    {
        var authored = SafePositive(authoredGridMeters, 1d);
        var zoom = SafePositive(zoomPixelsPerMeter, 1d);
        var minimum = Math.Max(8d, SafePositive(minimumMinorPixels, 24d));
        var requiredMeters = minimum / zoom;
        var multiplier = NiceMultiplier(Math.Max(1d, requiredMeters / authored));
        var minor = authored * multiplier;
        var major = minor * 5d;
        return new MapGridLod(authored, minor, major, minor * zoom, major * zoom);
    }

    private static double NiceMultiplier(double value)
    {
        var exponent = Math.Floor(Math.Log10(value));
        var power = Math.Pow(10d, exponent);
        var normalized = value / power;
        var nice = normalized <= 1d ? 1d : normalized <= 2d ? 2d : normalized <= 5d ? 5d : 10d;
        return Math.Max(1d, nice * power);
    }

    private static double SafePositive(double value, double fallback)
        => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d ? value : fallback;
}

public sealed class MapViewportPerformanceProbe
{
    private readonly Dictionary<string, List<double>> _samples = new(StringComparer.OrdinalIgnoreCase);

    public T Measure<T>(string category, Func<T> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        try { return operation(); }
        finally { stopwatch.Stop(); Add(category, stopwatch.Elapsed.TotalMilliseconds); }
    }

    public void Measure(string category, Action operation)
        => Measure(category, () => { operation(); return true; });

    public void Add(string category, double milliseconds)
    {
        if (!_samples.TryGetValue(category, out var values))
        {
            values = new List<double>();
            _samples[category] = values;
        }
        values.Add(Math.Max(0d, milliseconds));
    }

    public MapPerformanceStatistics Statistics(string category)
    {
        if (!_samples.TryGetValue(category, out var values) || values.Count == 0)
            return new MapPerformanceStatistics(0, 0d, 0d, 0d);
        var ordered = values.OrderBy(value => value).ToArray();
        return new MapPerformanceStatistics(
            ordered.Length,
            Percentile(ordered, 0.5d),
            Percentile(ordered, 0.95d),
            ordered[ordered.Length - 1]);
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0) return 0d;
        var index = (int)Math.Ceiling(percentile * values.Count) - 1;
        return values[Math.Max(0, Math.Min(values.Count - 1, index))];
    }
}

public readonly struct MapPoint
{
    public MapPoint(double x, double y) { X = x; Y = y; }
    public double X { get; }
    public double Y { get; }
    public double DistanceTo(MapPoint other)
        => Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
}

public readonly struct MapRect
{
    public MapRect(double x, double y, double width, double height) { X = x; Y = y; Width = width; Height = height; }
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public MapPoint Center => new MapPoint(X + Width / 2d, Y + Height / 2d);
    public MapRect Normalize()
    {
        var left = Math.Min(X, Right);
        var top = Math.Min(Y, Bottom);
        return new MapRect(left, top, Math.Abs(Width), Math.Abs(Height));
    }
}

public readonly struct MapGridLod
{
    public MapGridLod(double authoredStepMeters, double minorStepMeters, double majorStepMeters, double minorPixels, double majorPixels)
    {
        AuthoredStepMeters = authoredStepMeters;
        MinorStepMeters = minorStepMeters;
        MajorStepMeters = majorStepMeters;
        MinorPixels = minorPixels;
        MajorPixels = majorPixels;
    }
    public double AuthoredStepMeters { get; }
    public double MinorStepMeters { get; }
    public double MajorStepMeters { get; }
    public double MinorPixels { get; }
    public double MajorPixels { get; }
    public string StepLabel => "Grid " + MinorStepMeters.ToString("0.###", CultureInfo.CurrentCulture) + " m";
}

public readonly struct MapPerformanceStatistics
{
    public MapPerformanceStatistics(int count, double medianMs, double p95Ms, double maxMs)
    {
        Count = count;
        MedianMs = medianMs;
        P95Ms = p95Ms;
        MaxMs = maxMs;
    }
    public int Count { get; }
    public double MedianMs { get; }
    public double P95Ms { get; }
    public double MaxMs { get; }
}
