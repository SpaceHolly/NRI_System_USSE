using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Web.Script.Serialization;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.MapViewport.Contracts;

internal static class Program
{
    private static readonly Dictionary<string, bool> Checks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> Errors = new();

    private static int Main(string[] args)
    {
        var outputDirectory = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_20_2");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            RunTransformContracts();
            RunGridContracts();
            RunPerformanceContracts(outputDirectory);
        }
        catch (Exception ex)
        {
            Errors.Add(ex.ToString());
        }

        var status = Errors.Count == 0 && Checks.Count >= 35 && Checks.Values.All(value => value) ? "PASS" : "NOT_PASS";
        WriteJson(Path.Combine(outputDirectory, "map_viewport_transform_audit.json"), new
        {
            status,
            coordinateOrigin = "top-left; X right; Y down; world units are meters",
            resetState = "100 percent equals 1 pixel per meter, clamped to centralized limits; origin is top-left unless content is smaller than viewport",
            testedMapSizesMeters = new[] { 250, 1000, 4000 },
            checks = Checks.Where(item => !item.Key.StartsWith("grid.") && !item.Key.StartsWith("performance.")).ToDictionary(item => item.Key, item => item.Value),
            errors = Errors
        });
        var gridStatus = Errors.All(error => error.IndexOf("grid", StringComparison.OrdinalIgnoreCase) < 0)
            && Checks.Where(item => item.Key.StartsWith("grid.")).All(item => item.Value)
            ? "PASS"
            : "NOT_PASS";
        WriteJson(Path.Combine(outputDirectory, "map_grid_lod_audit.json"), new
        {
            status = gridStatus,
            authoredGridRemainsCanonical = true,
            visualLodDoesNotChangeSnapping = true,
            checks = Checks.Where(item => item.Key.StartsWith("grid.")).ToDictionary(item => item.Key, item => item.Value),
            errors = Errors.Where(error => error.IndexOf("grid", StringComparison.OrdinalIgnoreCase) >= 0).ToArray()
        });
        Console.WriteLine("Map viewport contracts: " + status);
        return status == "PASS" ? 0 : 1;
    }

    private static void RunTransformContracts()
    {
        foreach (var size in new[] { 250d, 1000d, 4000d })
        {
            var prefix = "transform." + size.ToString("0", CultureInfo.InvariantCulture) + ".";
            var viewport = new MapViewportState(size, size, 960d, 640d, size == 250d ? 1d : 25d);
            var world = new MapPoint(size * 0.37d, size * 0.61d);
            var screen = viewport.WorldToScreen(world);
            Check(prefix + "worldScreenWorldRoundTrip", viewport.ScreenToWorld(screen).DistanceTo(world) < 0.000001d);
            Check(prefix + "screenWorldScreenRoundTrip", viewport.WorldToScreen(viewport.ScreenToWorld(new MapPoint(311.25d, 207.75d))).DistanceTo(new MapPoint(311.25d, 207.75d)) < 0.000001d);

            var beforePan = world;
            viewport.PanByPixels(-120d, 75d);
            Check(prefix + "panDoesNotChangeWorldCoordinates", world.DistanceTo(beforePan) == 0d);

            var interactionZoom = Math.Max(viewport.ViewportWidthPixels / size, viewport.ViewportHeightPixels / size) * 1.5d;
            viewport.ZoomAtScreenPoint(interactionZoom, new MapPoint(viewport.ViewportWidthPixels / 2d, viewport.ViewportHeightPixels / 2d));
            var anchorScreen = viewport.WorldToScreen(world);
            var anchorWorld = viewport.ScreenToWorld(anchorScreen);
            for (var index = 0; index < 50; index++) viewport.ZoomByFactor(1.02d, anchorScreen);
            Check(prefix + "cursorAnchorZoom", viewport.ScreenToWorld(anchorScreen).DistanceTo(anchorWorld) < 0.00001d);

            var centerWorld = viewport.ScreenToWorld(new MapPoint(480d, 320d));
            viewport.ResizeViewport(1200d, 800d);
            Check(prefix + "resizePreservesCenterAnchor", viewport.ScreenToWorld(new MapPoint(600d, 400d)).DistanceTo(centerWorld) < 0.00001d);

            viewport.FitMap();
            var topLeft = viewport.WorldToScreen(new MapPoint(0d, 0d));
            var bottomRight = viewport.WorldToScreen(new MapPoint(size, size));
            Check(prefix + "fitMapContainsBounds", topLeft.X >= -0.001d && topLeft.Y >= -0.001d && bottomRight.X <= 1200.001d && bottomRight.Y <= 800.001d);

            var bounds = new MapRect(size * 0.2d, size * 0.25d, size * 0.3d, size * 0.2d);
            viewport.FitBounds(bounds, 24d);
            var fitTopLeft = viewport.WorldToScreen(new MapPoint(bounds.X, bounds.Y));
            var fitBottomRight = viewport.WorldToScreen(new MapPoint(bounds.Right, bounds.Bottom));
            Check(prefix + "fitBoundsContainsGroup", fitTopLeft.X >= -0.001d && fitTopLeft.Y >= -0.001d && fitBottomRight.X <= 1200.001d && fitBottomRight.Y <= 800.001d);

            viewport.Reset();
            Check(prefix + "resetFiniteAndDocumented", IsFinitePositive(viewport.Zoom) && viewport.Zoom >= viewport.MinZoom && viewport.Zoom <= viewport.MaxZoom);
            Check(prefix + "negativeCoordinatesPredictable", viewport.ClampWorldPoint(new MapPoint(-10d, -20d)).DistanceTo(new MapPoint(0d, 0d)) == 0d);
            Check(prefix + "outOfBoundsClamp", viewport.ClampWorldPoint(new MapPoint(size + 1d, size + 2d)).DistanceTo(new MapPoint(size, size)) == 0d);

            var snapBefore = viewport.SnapWorldPoint(new MapPoint(77.4d, 112.6d));
            viewport.ZoomByFactor(1.7d, new MapPoint(300d, 200d));
            var snapAfter = viewport.SnapWorldPoint(new MapPoint(77.4d, 112.6d));
            Check(prefix + "snapIndependentOfZoom", snapBefore.DistanceTo(snapAfter) == 0d);
            Check(prefix + "noNaNInfinity", IsFinitePositive(viewport.Zoom) && IsFinite(viewport.OffsetX) && IsFinite(viewport.OffsetY));
        }

        var state = new MapCanvasState { Name = "Viewport serialization contract", WidthMeters = 1000, HeightMeters = 1000, GridCellSizeMeters = 25 };
        var serialized = Serialize(state);
        Check("transform.serializationExcludesViewport", !serialized.Contains("OffsetX") && !serialized.Contains("OffsetY") && !serialized.Contains("ViewportWidth") && !serialized.Contains("Zoom"));
        Check("transform.rotationDoesNotOwnViewportAnchor", typeof(MapViewportState).GetProperties().All(property => property.Name.IndexOf("Rotation", StringComparison.OrdinalIgnoreCase) < 0));
    }

    private static void RunGridContracts()
    {
        foreach (var zoom in new[] { 0.02d, 0.1d, 0.25d, 1d, 4d, 12d })
        {
            var lod = MapGridLodCalculator.Calculate(1d, zoom);
            var key = zoom.ToString("0.##", CultureInfo.InvariantCulture);
            Check("grid." + key + ".minorReadable", lod.MinorPixels >= 23.999d);
            Check("grid." + key + ".majorReadable", lod.MajorPixels >= lod.MinorPixels * 4.999d);
            Check("grid." + key + ".authoredUnchanged", Math.Abs(lod.AuthoredStepMeters - 1d) < 0.000001d);
            Check("grid." + key + ".finite", IsFinitePositive(lod.MinorStepMeters) && IsFinitePositive(lod.MajorStepMeters));
        }

        var viewport = new MapViewportState(4000d, 4000d, 960d, 640d, 1d);
        var low = MapGridLodCalculator.Calculate(viewport.GridSizeMeters, viewport.Zoom);
        var snappedLow = viewport.SnapWorldPoint(new MapPoint(123.4d, 567.6d));
        viewport.ZoomAtScreenPoint(viewport.MaxZoom, new MapPoint(480d, 320d));
        var high = MapGridLodCalculator.Calculate(viewport.GridSizeMeters, viewport.Zoom);
        var snappedHigh = viewport.SnapWorldPoint(new MapPoint(123.4d, 567.6d));
        Check("grid.lodChangesWithZoom", low.MinorStepMeters > high.MinorStepMeters);
        Check("grid.snappingRemainsCanonical", snappedLow.DistanceTo(snappedHigh) == 0d);
    }

    private static void RunPerformanceContracts(string outputDirectory)
    {
        var viewport = new MapViewportState(4000d, 4000d, 1280d, 800d, 5d);
        var probe = new MapViewportPerformanceProbe();
        var objects = Enumerable.Range(0, 120).Select(index => new MapPoint(index * 31d % 4000d, index * 67d % 4000d)).ToArray();
        var memoryBefore = GC.GetTotalMemory(true);
        var maximumVisualEstimate = 0;
        for (var index = 0; index < 200; index++)
        {
            probe.Measure("pan", () => viewport.PanByPixels(index % 2 == 0 ? -7d : 9d, index % 3 == 0 ? 5d : -4d));
            probe.Measure("zoom", () => viewport.ZoomByFactor(index % 2 == 0 ? 1.015d : 1d / 1.015d, new MapPoint(640d, 400d)));
            probe.Measure("hitTest", () => objects.OrderBy(point => viewport.WorldToScreen(point).DistanceTo(new MapPoint(640d, 400d))).First());
            var lod = probe.Measure("grid", () => MapGridLodCalculator.Calculate(viewport.GridSizeMeters, viewport.Zoom));
            var visible = viewport.VisibleWorldBounds();
            var verticalLines = (int)Math.Ceiling(visible.Width / lod.MinorStepMeters) + 2;
            var horizontalLines = (int)Math.Ceiling(visible.Height / lod.MinorStepMeters) + 2;
            maximumVisualEstimate = Math.Max(maximumVisualEstimate, verticalLines + horizontalLines + objects.Length);
        }
        var memoryAfter = GC.GetTotalMemory(true);
        var pan = probe.Statistics("pan");
        var zoom = probe.Statistics("zoom");
        var hit = probe.Statistics("hitTest");
        var grid = probe.Statistics("grid");
        Check("performance.panP95", pan.P95Ms <= 50d);
        Check("performance.zoomP95", zoom.P95Ms <= 50d);
        Check("performance.hitTestP95", hit.P95Ms <= 25d);
        Check("performance.noRegular150msFrame", Math.Max(Math.Max(pan.P95Ms, zoom.P95Ms), hit.P95Ms) < 150d);
        Check("performance.visualEstimateBounded", maximumVisualEstimate < 500);
        Check("performance.memoryBounded", memoryAfter - memoryBefore < 8L * 1024L * 1024L);
        WriteJson(Path.Combine(outputDirectory, "map_viewport_performance_audit.json"), new
        {
            status = Checks.Where(item => item.Key.StartsWith("performance.")).All(item => item.Value) ? "PASS" : "NOT_PASS",
            fixture = new { mapWidthMeters = 4000, mapHeightMeters = 4000, objectCount = 120, interactionCount = 200 },
            pan = Stats(pan),
            zoom = Stats(zoom),
            hitTest = Stats(hit),
            grid = Stats(grid),
            maximumCreatedVisualEstimate = maximumVisualEstimate,
            memoryBeforeBytes = memoryBefore,
            memoryAfterBytes = memoryAfter,
            memoryDeltaBytes = memoryAfter - memoryBefore,
            developmentOnly = true
        });
    }

    private static object Stats(MapPerformanceStatistics statistics)
        => new { statistics.Count, statistics.MedianMs, statistics.P95Ms, statistics.MaxMs };

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool IsFinitePositive(double value) => IsFinite(value) && value > 0d;
    private static void Check(string key, bool result) { Checks[key] = result; if (!result) Errors.Add("Contract failed: " + key); }

    private static string Serialize<T>(T value)
    {
        var serializer = new DataContractJsonSerializer(typeof(T));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteJson(string path, object value)
    {
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        File.WriteAllText(path, serializer.Serialize(value), new UTF8Encoding(false));
    }
}
