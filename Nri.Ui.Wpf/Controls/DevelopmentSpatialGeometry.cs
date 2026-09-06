using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace Nri.Ui.Wpf.Controls;

public sealed class NriDevelopmentSectorField : FrameworkElement
{
    public static readonly DependencyProperty SectorOpacityProperty = DependencyProperty.Register(
        nameof(SectorOpacity), typeof(double), typeof(NriDevelopmentSectorField),
        new FrameworkPropertyMetadata(0.10d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double SectorOpacity
    {
        get => (double)GetValue(SectorOpacityProperty);
        set => SetValue(SectorOpacityProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
        => new FrameworkElementAutomationPeer(this);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        foreach (var sector in DevelopmentSpatialGeometry.CreateSectorPolygons(ActualWidth, ActualHeight))
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sector.Color));
            brush.Opacity = Math.Max(0, Math.Min(1, SectorOpacity));
            brush.Freeze();

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(sector.Points[0], true, true);
                context.PolyLineTo(sector.Points.Skip(1).ToList(), true, false);
            }
            geometry.Freeze();
            drawingContext.DrawGeometry(brush, null, geometry);
        }
    }
}

public sealed class DevelopmentSectorPolygon
{
    public string SemanticId { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFFFF";
    public double CenterAngle { get; set; }
    public double StartAngle { get; set; }
    public double EndAngle { get; set; }
    public IReadOnlyList<Point> Points { get; set; } = Array.Empty<Point>();
    public double Area { get; set; }
    public Rect Bounds { get; set; }
}

public static class DevelopmentSpatialGeometry
{
    public const double DesignWidth = 1000d;
    public const double DesignHeight = 600d;
    public const double CenterX = 500d;
    public const double CenterY = 300d;
    public const double SectorSpan = 60d;

    private static readonly string[] SectorIds =
    {
        "strength", "dexterity", "charisma", "wisdom", "intelligence", "endurance"
    };

    private static readonly double[] SectorCenters = { -90d, -30d, 30d, 90d, 150d, 210d };
    private static readonly string[] SectorColors =
    {
        "#FFEF6A78", "#FF57C7ED", "#FFD980E8", "#FF5DD18A", "#FFF0C45D", "#FF7898F0"
    };

    public static IReadOnlyList<DevelopmentSectorPolygon> CreateSectorPolygons(double width, double height)
    {
        var cx = width / 2d;
        var cy = height / 2d;
        var radius = Math.Max(width, height) * 4d;
        var result = new List<DevelopmentSectorPolygon>(6);
        for (var index = 0; index < 6; index++)
        {
            var center = SectorCenters[index];
            var start = center - SectorSpan / 2d;
            var end = center + SectorSpan / 2d;
            var triangle = new List<Point>
            {
                new Point(cx, cy),
                RayPoint(cx, cy, start, radius),
                RayPoint(cx, cy, end, radius)
            };
            var points = ClipToRectangle(triangle, width, height);
            result.Add(new DevelopmentSectorPolygon
            {
                SemanticId = SectorIds[index],
                Color = SectorColors[index],
                CenterAngle = center,
                StartAngle = start,
                EndAngle = end,
                Points = points,
                Area = PolygonArea(points),
                Bounds = PolygonBounds(points)
            });
        }
        return result;
    }

    public static IReadOnlyList<Point> CreateSectorSlice(double width, double height, double startAngle, double endAngle)
    {
        var cx = width / 2d;
        var cy = height / 2d;
        var radius = Math.Max(width, height) * 4d;
        return ClipToRectangle(new[]
        {
            new Point(cx, cy),
            RayPoint(cx, cy, startAngle, radius),
            RayPoint(cx, cy, endAngle, radius)
        }, width, height);
    }

    public static int ResolveSectorIndex(string directionId, int fallback = 0)
    {
        var key = (directionId ?? string.Empty).ToLowerInvariant();
        if (key.Contains("strength")) return 0;
        if (key.Contains("dexterity") || key.Contains("agility")) return 1;
        if (key.Contains("charisma")) return 2;
        if (key.Contains("wisdom")) return 3;
        if (key.Contains("intellect")) return 4;
        if (key.Contains("endurance") || key.Contains("constitution")) return 5;
        return Math.Max(0, Math.Min(5, fallback));
    }

    public static double SectorCenterAngle(int sectorIndex)
        => SectorCenters[Math.Max(0, Math.Min(5, sectorIndex))];

    public static string SectorColor(int sectorIndex)
        => SectorColors[Math.Max(0, Math.Min(5, sectorIndex))];

    public static Point OverviewDirectionCenter(int sectorIndex)
        => Polar(CenterX, CenterY, SectorCenterAngle(sectorIndex), 105d);

    public static Point OverviewDirectionTopLeft(int sectorIndex)
        => Math.Max(0, Math.Min(5, sectorIndex)) switch
        {
            0 => new Point(420d, 135d),
            1 => new Point(650d, 230d),
            2 => new Point(650d, 370d),
            3 => new Point(420d, 465d),
            4 => new Point(190d, 370d),
            _ => new Point(190d, 230d)
        };

    public static Point OverviewPathCenter(int sectorIndex, int pathIndex)
    {
        var slot = Math.Max(0, Math.Min(3, pathIndex));
        var angle = SectorCenterAngle(sectorIndex) + (slot % 2 == 0 ? -10d : 10d);
        var radius = slot < 2 ? 255d : 390d;
        return Polar(CenterX, CenterY, angle, radius, 0.55d);
    }

    public static Point OverviewPathTopLeft(int sectorIndex, int pathIndex)
    {
        var sector = Math.Max(0, Math.Min(5, sectorIndex));
        var slot = Math.Max(0, Math.Min(3, pathIndex));
        return sector switch
        {
            0 => new Point(220d + slot * 140d, 25d),
            1 => new Point(845d, 70d + slot * 65d),
            2 => new Point(845d, 325d + slot * 65d),
            3 => new Point(220d + slot * 140d, 530d),
            4 => new Point(23d, 325d + slot * 65d),
            _ => new Point(23d, 70d + slot * 65d)
        };
    }

    public static Point OverviewMilestoneCenter(int sectorIndex)
    {
        var point = Polar(CenterX, CenterY, SectorCenterAngle(sectorIndex), 360d, 0.70d);
        return new Point(Math.Max(44d, Math.Min(956d, point.X)), Math.Max(50d, Math.Min(550d, point.Y)));
    }

    public static Point Polar(double cx, double cy, double angleDegrees, double radius, double yRatio = 1d)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Point(cx + Math.Cos(radians) * radius, cy + Math.Sin(radians) * radius * yRatio);
    }

    public static double PolygonArea(IReadOnlyList<Point> points)
    {
        if (points == null || points.Count < 3) return 0;
        var sum = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            var next = points[(i + 1) % points.Count];
            sum += points[i].X * next.Y - next.X * points[i].Y;
        }
        return Math.Abs(sum) / 2d;
    }

    private static Point RayPoint(double cx, double cy, double angleDegrees, double radius)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Point(cx + Math.Cos(radians) * radius, cy + Math.Sin(radians) * radius);
    }

    private static IReadOnlyList<Point> ClipToRectangle(IReadOnlyList<Point> source, double width, double height)
    {
        IEnumerable<Point> points = source;
        points = Clip(points, point => point.X >= 0, (a, b) => IntersectVertical(a, b, 0));
        points = Clip(points, point => point.X <= width, (a, b) => IntersectVertical(a, b, width));
        points = Clip(points, point => point.Y >= 0, (a, b) => IntersectHorizontal(a, b, 0));
        points = Clip(points, point => point.Y <= height, (a, b) => IntersectHorizontal(a, b, height));
        return points.ToList();
    }

    private static IEnumerable<Point> Clip(IEnumerable<Point> source, Func<Point, bool> inside, Func<Point, Point, Point> intersect)
    {
        var input = source.ToList();
        if (input.Count == 0) return input;
        var output = new List<Point>();
        var previous = input[input.Count - 1];
        var previousInside = inside(previous);
        foreach (var current in input)
        {
            var currentInside = inside(current);
            if (currentInside)
            {
                if (!previousInside) output.Add(intersect(previous, current));
                output.Add(current);
            }
            else if (previousInside)
            {
                output.Add(intersect(previous, current));
            }
            previous = current;
            previousInside = currentInside;
        }
        return output;
    }

    private static Point IntersectVertical(Point a, Point b, double x)
    {
        var delta = b.X - a.X;
        var t = Math.Abs(delta) < 0.000001 ? 0 : (x - a.X) / delta;
        return new Point(x, a.Y + (b.Y - a.Y) * t);
    }

    private static Point IntersectHorizontal(Point a, Point b, double y)
    {
        var delta = b.Y - a.Y;
        var t = Math.Abs(delta) < 0.000001 ? 0 : (y - a.Y) / delta;
        return new Point(a.X + (b.X - a.X) * t, y);
    }

    private static Rect PolygonBounds(IReadOnlyList<Point> points)
    {
        if (points == null || points.Count == 0) return Rect.Empty;
        var minX = points.Min(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxX = points.Max(point => point.X);
        var maxY = points.Max(point => point.Y);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}
