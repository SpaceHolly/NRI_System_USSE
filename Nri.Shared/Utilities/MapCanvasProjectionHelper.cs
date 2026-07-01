using System;

namespace Nri.Shared.Utilities;

public static class MapCanvasProjectionHelper
{
    public static MapCanvasProjection Calculate(double mapWidthMeters, double mapHeightMeters, double maxCanvasWidth, double maxCanvasHeight)
    {
        var safeMapWidth = Math.Max(1d, mapWidthMeters);
        var safeMapHeight = Math.Max(1d, mapHeightMeters);
        var safeMaxWidth = Math.Max(1d, maxCanvasWidth);
        var safeMaxHeight = Math.Max(1d, maxCanvasHeight);

        var scaleX = safeMaxWidth / safeMapWidth;
        var scaleY = safeMaxHeight / safeMapHeight;
        var scale = Math.Max(0.01d, Math.Min(scaleX, scaleY));
        var canvasWidth = Math.Max(320d, safeMapWidth * scale);
        var canvasHeight = Math.Max(220d, safeMapHeight * scale);
        return new MapCanvasProjection(scale, canvasWidth, canvasHeight);
    }

    public static double ToPixel(double meters, double scale)
    {
        return Math.Max(0d, meters) * Math.Max(0.01d, scale);
    }

    public static double ToMeters(double pixels, double scale)
    {
        var safeScale = Math.Max(0.01d, scale);
        return Math.Max(0d, pixels) / safeScale;
    }

    public static int ToCellIndex(double meters, int cellSizeMeters)
    {
        var safeCellSize = Math.Max(1, cellSizeMeters);
        var safeMeters = Math.Max(0d, meters);
        return (int)Math.Floor(safeMeters / safeCellSize);
    }

    public static double CellToMeters(int cellIndex, int cellSizeMeters)
    {
        var safeCellSize = Math.Max(1, cellSizeMeters);
        return Math.Max(0, cellIndex) * safeCellSize;
    }
}

public readonly struct MapCanvasProjection
{
    public MapCanvasProjection(double scale, double canvasWidth, double canvasHeight)
    {
        Scale = scale;
        CanvasWidth = canvasWidth;
        CanvasHeight = canvasHeight;
    }

    public double Scale { get; }
    public double CanvasWidth { get; }
    public double CanvasHeight { get; }
}
