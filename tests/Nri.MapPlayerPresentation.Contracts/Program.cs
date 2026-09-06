using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using Nri.Server.Application;
using Nri.Shared.Contracts;
using Nri.Shared.Utilities;

namespace Nri.MapPlayerPresentation.Contracts;

internal static class Program
{
    private static readonly Dictionary<string, bool> Checks = new(StringComparer.OrdinalIgnoreCase);

    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_20_4");
        Directory.CreateDirectory(output);
        VisibilityContracts();
        SnapshotContracts();
        LabelContracts();
        PayloadContracts();
        var pass = Checks.Values.All(value => value);

        Write(Path.Combine(output, "map_visibility_projection_audit.json"), new
        {
            status = Status(GroupPass("visibility.")),
            evaluator = nameof(PlayerMapVisibilityPolicy0204),
            conflictRule = "most restrictive wins",
            checks = Group("visibility.")
        });
        Write(Path.Combine(output, "map_visibility_revoke_audit.json"), new
        {
            status = Status(GroupPass("snapshot.")),
            operation = "replace safe snapshot and clear selection when selected id is absent",
            checks = Group("snapshot.")
        });
        Write(Path.Combine(output, "map_label_lod_audit.json"), new
        {
            status = Status(GroupPass("label.lod.")),
            thresholds = new { distantMinimumPriority = 300, mediumMinimumPriority = 200, nearMinimumPriority = 0 },
            checks = Group("label.lod.")
        });
        Write(Path.Combine(output, "map_label_collision_audit.json"), new
        {
            status = Status(GroupPass("label.collision.")),
            deterministicOffsets = true,
            worldCoordinatesUnchanged = true,
            checks = Group("label.collision.")
        });
        Write(Path.Combine(output, "map_reconnect_sync_audit.json"), new
        {
            status = Status(GroupPass("snapshot.")),
            fullSnapshotReplacement = true,
            staleRevisionRejected = true,
            selectionRestoredOnlyWhenStillVisible = true,
            checks = Group("snapshot.")
        });
        Write(Path.Combine(output, "map_player_visibility_safety_audit.json"), new
        {
            status = Status(GroupPass("payload.") && GroupPass("visibility.")),
            serverBoundary = typeof(IPlayerMapProjectionService).FullName,
            forbiddenFields = new[] { "gmNotes", "serverOnlyData", "linkedEntityId", "operationId", "internalVisibilityReason" },
            checks = Group("payload.").Concat(Group("visibility.")).ToDictionary(item => item.Key, item => item.Value)
        });

        Console.WriteLine("Player map presentation contracts: " + Status(pass));
        return pass ? 0 : 1;
    }

    private static void VisibilityContracts()
    {
        Check("visibility.public", PlayerMapVisibilityPolicy0204.IsIncluded("Public"));
        Check("visibility.party", PlayerMapVisibilityPolicy0204.IsIncluded("Party"));
        Check("visibility.playerVisible", PlayerMapVisibilityPolicy0204.IsIncluded("PlayerVisible"));
        Check("visibility.gmOnlyExcluded", !PlayerMapVisibilityPolicy0204.IsIncluded("GmOnly"));
        Check("visibility.hiddenExcluded", !PlayerMapVisibilityPolicy0204.IsIncluded("Hidden"));
        Check("visibility.archivedExcluded", !PlayerMapVisibilityPolicy0204.IsIncluded("Public", true));
        Check("visibility.unknownExcluded", !PlayerMapVisibilityPolicy0204.IsIncluded("LegacyCustomMode"));
        Check("visibility.layerMostRestrictive", !PlayerMapVisibilityPolicy0204.MostRestrictive(true, false, true));
        Check("visibility.objectMostRestrictive", PlayerMapVisibilityPolicy0204.MostRestrictive(true, true, true));
    }

    private static void SnapshotContracts()
    {
        var kept = PlayerMapSnapshotReducer0204.Reduce(10, 11, "visible", new[] { "visible", "other" });
        Check("snapshot.newRevisionApplied", kept.Applied && !kept.StaleRejected && kept.Revision == 11);
        Check("snapshot.visibleSelectionRestored", kept.SelectedObjectId == "visible");
        var revoked = PlayerMapSnapshotReducer0204.Reduce(11, 12, "revoked", new[] { "other" });
        Check("snapshot.revokedSelectionCleared", revoked.Applied && revoked.SelectedObjectId.Length == 0);
        var stale = PlayerMapSnapshotReducer0204.Reduce(12, 11, "visible", new[] { "visible" });
        Check("snapshot.staleRejected", stale.StaleRejected && !stale.Applied && stale.Revision == 12);
        var reconnect = PlayerMapSnapshotReducer0204.Reduce(12, 20, "visible", new[] { "visible" });
        Check("snapshot.reconnectCurrentApplied", reconnect.Applied && reconnect.Revision == 20 && reconnect.SelectedObjectId == "visible");
    }

    private static void LabelContracts()
    {
        var candidates = new[]
        {
            Label("selected", "Выбранный объект", 100, 100, 100, true),
            Label("important", "Важный маркер", 300, 100, 100),
            Label("token", "Видимый персонаж", 400, 104, 102),
            Label("asset", "Очень длинное читаемое название объекта инфраструктуры на карте", 200, 180, 110),
            Label("shape", "Подпись области", 100, 185, 115),
            Label("empty", "", 500, 250, 200),
            Label("edge", "Край карты", 300, 5, 5),
            Label("unicode", "Кириллица — Путь", 300, 360, 240)
        };
        var near = PlayerMapLabelLayout0204.Layout(candidates, 500, 300, 1.0);
        var repeat = PlayerMapLabelLayout0204.Layout(candidates, 500, 300, 1.0);
        var medium = PlayerMapLabelLayout0204.Layout(candidates, 500, 300, 0.7);
        var distant = PlayerMapLabelLayout0204.Layout(candidates, 500, 300, 0.4);
        var isolatedMedium = PlayerMapLabelLayout0204.Layout(new[] { Label("asset-only", "Объект", 200, 250, 150) }, 500, 300, 0.7);

        Check("label.lod.selectedAlwaysVisible", distant.Any(item => item.ObjectId == "selected"));
        Check("label.lod.lowPrioritySuppressedAtDistance", distant.All(item => item.ObjectId != "shape" && item.ObjectId != "asset"));
        Check("label.lod.mediumIncludesAssets", isolatedMedium.Any(item => item.ObjectId == "asset-only"));
        Check("label.lod.emptySuppressed", near.All(item => item.ObjectId != "empty"));
        Check("label.lod.unicodePreserved", near.Any(item => item.Text == "Кириллица — Путь"));
        Check("label.lod.longNameBounded", near.Where(item => item.ObjectId == "asset").All(item => item.Width <= 220));
        Check("label.collision.noOverlap", NoOverlap(near));
        Check("label.collision.clippedToViewport", near.All(item => item.X >= 0 && item.Y >= 0 && item.X + item.Width <= 500 && item.Y + item.Height <= 300));
        Check("label.collision.deterministic", Fingerprint(near) == Fingerprint(repeat));
        Check("label.collision.worldAnchorUnchanged", candidates.Single(item => item.ObjectId == "token").AnchorX == 104);
        Check("label.collision.visualCountBounded", near.Count <= candidates.Count(item => !string.IsNullOrWhiteSpace(item.Text)));
    }

    private static void PayloadContracts()
    {
        Check("payload.command.initial", CommandNames.MapPlayerSceneGet == "map.player.scene.get");
        Check("payload.command.active", CommandNames.MapPlayerSceneActiveGet == "map.player.scene.active.get");
        Check("payload.command.sync", CommandNames.MapPlayerSceneSync == "map.player.scene.sync");
        Check("payload.command.adminPreview", CommandNames.MapAdminPlayerPreviewGet == "map.admin.playerPreview.get");
        var method = typeof(PlayerMapProjectionService0204).GetMethod("SafeObject", BindingFlags.NonPublic | BindingFlags.Static);
        var safe = method?.Invoke(null, new object[] { "marker", "visible-id", "Читаемый объект", "location", 10d, 20d,
            "Публичное описание", "pin", "accent", "location", "Публичная локация", 300, 1d, 1d });
        var json = new JavaScriptSerializer().Serialize(safe).ToLowerInvariant();
        Check("payload.safeBuilderFound", method != null && safe != null);
        Check("payload.noGmNotes", !json.Contains("gmnotes"));
        Check("payload.noServerOnlyData", !json.Contains("serveronlydata"));
        Check("payload.noHiddenEntityId", !json.Contains("linkedentityid"));
        Check("payload.publicDescription", json.Contains("публичное описание"));
        Check("payload.readableReferenceOnly", json.Contains("публичная локация"));
    }

    private static PlayerMapLabelCandidate0204 Label(string id, string text, int priority, double x, double y, bool selected = false)
        => new() { ObjectId = id, Text = text, Kind = "marker", Priority = priority, AnchorX = x, AnchorY = y, IsSelected = selected };

    private static bool NoOverlap(IReadOnlyList<PlayerMapLabelPlacement0204> placements)
    {
        for (var left = 0; left < placements.Count; left++)
        for (var right = left + 1; right < placements.Count; right++)
        {
            var a = placements[left]; var b = placements[right];
            if (a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y) return false;
        }
        return true;
    }

    private static string Fingerprint(IEnumerable<PlayerMapLabelPlacement0204> values)
        => string.Join("|", values.Select(item => $"{item.ObjectId}:{item.X:0.###}:{item.Y:0.###}:{item.Width:0.###}"));
    private static void Check(string name, bool value) => Checks[name] = value;
    private static bool GroupPass(string prefix) => Checks.Where(item => item.Key.StartsWith(prefix)).All(item => item.Value);
    private static Dictionary<string, bool> Group(string prefix) => Checks.Where(item => item.Key.StartsWith(prefix)).ToDictionary(item => item.Key, item => item.Value);
    private static string Status(bool pass) => pass ? "PASS" : "NOT_PASS";
    private static void Write(string path, object payload) => File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(payload), new UTF8Encoding(false));
}
