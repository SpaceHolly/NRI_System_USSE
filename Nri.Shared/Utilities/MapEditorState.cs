using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Utilities;

public enum MapEditorObjectKind
{
    TilePatch = 1,
    Shape = 2,
    AssetInstance = 3
}

public sealed class MapEditorHitTarget
{
    public string Id { get; set; } = string.Empty;
    public string LayerId { get; set; } = string.Empty;
    public MapEditorObjectKind Kind { get; set; }
    public int LayerOrder { get; set; }
    public int ZIndex { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsSelectable { get; set; } = true;
    public bool IsLayerLocked { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public static class MapEditorHitTest
{
    public static MapEditorHitTarget? Resolve(
        IEnumerable<MapEditorHitTarget> targets,
        double worldX,
        double worldY,
        double zoom,
        double pixelTolerance = 6d)
    {
        if (targets == null) throw new ArgumentNullException(nameof(targets));
        if (zoom <= 0d || double.IsNaN(zoom) || double.IsInfinity(zoom))
            throw new ArgumentOutOfRangeException(nameof(zoom));

        var tolerance = Math.Max(0d, pixelTolerance) / zoom;
        return targets
            .Where(target => target != null && target.IsVisible && target.IsSelectable)
            .Where(target => Contains(target, worldX, worldY, tolerance))
            .OrderByDescending(target => target.LayerOrder)
            .ThenByDescending(target => target.ZIndex)
            .ThenByDescending(target => (int)target.Kind)
            .ThenBy(target => target.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool Contains(MapEditorHitTarget target, double x, double y, double tolerance)
    {
        var left = Math.Min(target.X, target.X + target.Width) - tolerance;
        var right = Math.Max(target.X, target.X + target.Width) + tolerance;
        var top = Math.Min(target.Y, target.Y + target.Height) - tolerance;
        var bottom = Math.Max(target.Y, target.Y + target.Height) + tolerance;
        return x >= left && x <= right && y >= top && y <= bottom;
    }
}

public static class MapEditorSnapPolicy
{
    public static double Snap(double value, bool enabled, double step)
    {
        if (!enabled) return value;
        if (step <= 0d || double.IsNaN(step) || double.IsInfinity(step))
            throw new ArgumentOutOfRangeException(nameof(step));
        return Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
    }

    public static (double X, double Y) SnapPoint(
        double x,
        double y,
        bool enabled,
        double step,
        double minX,
        double minY,
        double maxX,
        double maxY)
    {
        if (maxX < minX || maxY < minY) throw new ArgumentException("Invalid map bounds.");
        var snappedX = Snap(x, enabled, step);
        var snappedY = Snap(y, enabled, step);
        return (Clamp(snappedX, minX, maxX), Clamp(snappedY, minY, maxY));
    }

    private static double Clamp(double value, double minimum, double maximum)
        => Math.Max(minimum, Math.Min(maximum, value));
}

public sealed class MapEditorHistory<T>
{
    private readonly int _capacity;
    private readonly List<T> _undo = new List<T>();
    private readonly List<T> _redo = new List<T>();

    public MapEditorHistory(int capacity = 50)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Record(T command)
    {
        _undo.Add(command);
        if (_undo.Count > _capacity) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public bool TryTakeUndo(out T command)
    {
        if (_undo.Count == 0)
        {
            command = default!;
            return false;
        }

        var index = _undo.Count - 1;
        command = _undo[index];
        _undo.RemoveAt(index);
        _redo.Add(command);
        return true;
    }

    public bool TryTakeRedo(out T command)
    {
        if (_redo.Count == 0)
        {
            command = default!;
            return false;
        }

        var index = _redo.Count - 1;
        command = _redo[index];
        _redo.RemoveAt(index);
        _undo.Add(command);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
