using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Utilities;

public static class PlayerMapVisibilityPolicy0204
{
    public static bool IsIncluded(string? visibility, bool archived = false)
    {
        if (archived) return false;
        var value = (visibility ?? string.Empty).Trim().Replace("_", string.Empty).ToLowerInvariant();
        return value is "public" or "party" or "playervisible" or "visible" or "player";
    }

    public static bool MostRestrictive(params bool[] rules) => rules != null && rules.Length > 0 && rules.All(x => x);
}

public sealed class PlayerMapSnapshotReduction0204
{
    public bool Applied { get; set; }
    public bool StaleRejected { get; set; }
    public long Revision { get; set; }
    public string SelectedObjectId { get; set; } = string.Empty;
}

public static class PlayerMapSnapshotReducer0204
{
    public static PlayerMapSnapshotReduction0204 Reduce(long currentRevision, long incomingRevision, string? selectedObjectId, IEnumerable<string>? incomingObjectIds)
    {
        if (incomingRevision > 0 && currentRevision > 0 && incomingRevision < currentRevision)
            return new PlayerMapSnapshotReduction0204 { StaleRejected = true, Revision = currentRevision, SelectedObjectId = selectedObjectId ?? string.Empty };
        var ids = new HashSet<string>(incomingObjectIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var selection = !string.IsNullOrWhiteSpace(selectedObjectId) && ids.Contains(selectedObjectId) ? selectedObjectId : string.Empty;
        return new PlayerMapSnapshotReduction0204 { Applied = true, Revision = incomingRevision, SelectedObjectId = selection ?? string.Empty };
    }
}

public sealed class PlayerMapLabelCandidate0204
{
    public string ObjectId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int Priority { get; set; }
    public double AnchorX { get; set; }
    public double AnchorY { get; set; }
    public bool IsSelected { get; set; }
}

public sealed class PlayerMapLabelPlacement0204
{
    public string ObjectId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsSelected { get; set; }
}

public static class PlayerMapLabelLayout0204
{
    public static IReadOnlyList<PlayerMapLabelPlacement0204> Layout(IEnumerable<PlayerMapLabelCandidate0204> candidates, double viewportWidth, double viewportHeight, double zoom)
    {
        var minimumPriority = zoom < 0.45d ? 300 : zoom < 0.8d ? 200 : 0;
        var occupied = new List<PlayerMapLabelPlacement0204>();
        var offsets = new[] { (10d, -20d), (10d, 6d), (-10d, -20d), (-10d, 6d), (0d, -34d) };
        foreach (var item in (candidates ?? Array.Empty<PlayerMapLabelCandidate0204>())
                     .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                     .OrderByDescending(x => x.IsSelected).ThenByDescending(x => x.Priority)
                     .ThenBy(x => x.Text, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.ObjectId, StringComparer.Ordinal))
        {
            if (!item.IsSelected && item.Priority < minimumPriority) continue;
            var width = Math.Min(220d, Math.Max(64d, item.Text.Length * 7.2d + 14d));
            const double height = 24d;
            PlayerMapLabelPlacement0204? placement = null;
            foreach (var (offsetX, offsetY) in offsets)
            {
                var left = offsetX < 0 ? item.AnchorX + offsetX - width : item.AnchorX + offsetX;
                var top = item.AnchorY + offsetY;
                if (left < 0 || top < 0 || left + width > viewportWidth || top + height > viewportHeight) continue;
                if (occupied.Any(x => Intersects(x.X, x.Y, x.Width, x.Height, left, top, width, height))) continue;
                placement = New(item, left, top, width, height);
                break;
            }
            if (placement == null && item.IsSelected)
            {
                var left = Math.Max(0, Math.Min(Math.Max(0, viewportWidth - width), item.AnchorX + 8));
                var top = Math.Max(0, Math.Min(Math.Max(0, viewportHeight - height), item.AnchorY - 20));
                placement = New(item, left, top, width, height);
            }
            if (placement != null) occupied.Add(placement);
        }
        return occupied;
    }

    private static PlayerMapLabelPlacement0204 New(PlayerMapLabelCandidate0204 item, double x, double y, double width, double height)
        => new() { ObjectId = item.ObjectId, Text = item.Text, Kind = item.Kind, X = x, Y = y, Width = width, Height = height, IsSelected = item.IsSelected };
    private static bool Intersects(double ax, double ay, double aw, double ah, double bx, double by, double bw, double bh)
        => ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
}
