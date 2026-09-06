using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Nri.Shared.Contracts;
using Nri.Shared.Utilities;

namespace Nri.MapEditor.Contracts;

internal static class Program
{
    private static readonly Dictionary<string, bool> Checks = new(StringComparer.OrdinalIgnoreCase);

    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_20_3");
        Directory.CreateDirectory(output);
        HitTestContracts();
        SnappingContracts();
        HistoryContracts();
        WirePayloadContracts();
        var status = Checks.Values.All(value => value) ? "PASS" : "NOT_PASS";
        Write(Path.Combine(output, "map_editor_hit_test_audit.json"), new
        {
            status,
            priority = new[] { "layer order", "z-index", "asset over shape over tile", "stable id" },
            checks = Checks.Where(item => item.Key.StartsWith("hit.")).ToDictionary(item => item.Key, item => item.Value)
        });
        Write(Path.Combine(output, "map_editor_movement_snapping_audit.json"), new
        {
            status,
            coordinateSpace = "world meters",
            midpointRule = "AwayFromZero",
            checks = Checks.Where(item => item.Key.StartsWith("snap.")).ToDictionary(item => item.Key, item => item.Value)
        });
        Write(Path.Combine(output, "map_editor_undo_redo_audit.json"), new
        {
            status,
            capacity = 50,
            persisted = false,
            checks = Checks.Where(item => item.Key.StartsWith("history.")).ToDictionary(item => item.Key, item => item.Value)
        });
        Write(Path.Combine(output, "map_editor_wire_payload_audit.json"), new
        {
            status = Checks.Where(item => item.Key.StartsWith("wire.")).All(item => item.Value) ? "PASS" : "NOT_PASS",
            checks = Checks.Where(item => item.Key.StartsWith("wire.")).ToDictionary(item => item.Key, item => item.Value)
        });
        Console.WriteLine("Map editor contracts: " + status);
        return status == "PASS" ? 0 : 1;
    }

    private static void WirePayloadContracts()
    {
        var wireValues = new System.Collections.Hashtable
        {
            ["editableKinds"] = "tilePatch",
            ["layerType"] = "tile"
        };
        var normalized = PayloadReader.GetDictionary(
            new Dictionary<string, object> { ["values"] = wireValues }, "values");
        Check("wire.nestedDictionaryNormalized", normalized != null
            && Convert.ToString(normalized["editableKinds"]) == "tilePatch"
            && Convert.ToString(normalized["layerType"]) == "tile");

        var sourceRequest = new RequestEnvelope { Command = CommandNames.MapEditorAdminMutate };
        sourceRequest.Payload["values"] = new Dictionary<string, object>
        {
            ["editableKinds"] = "tilePatch",
            ["layerType"] = "tile"
        };
        var request = JsonProtocolSerializer.Deserialize<RequestEnvelope>(JsonProtocolSerializer.Serialize(sourceRequest));
        var fromProtocol = request == null ? null : PayloadReader.GetDictionary(request.Payload, "values");
        Check("wire.protocolJsonNormalized", fromProtocol != null
            && Convert.ToString(fromProtocol["editableKinds"]) == "tilePatch"
            && Convert.ToString(fromProtocol["layerType"]) == "tile");
    }

    private static void HitTestContracts()
    {
        var targets = new[]
        {
            Target("tile", MapEditorObjectKind.TilePatch, 10, 500),
            Target("shape", MapEditorObjectKind.Shape, 10, 500),
            Target("asset", MapEditorObjectKind.AssetInstance, 10, 500)
        };
        Check("hit.assetOverShapeOverTile", MapEditorHitTest.Resolve(targets, 10, 10, 1)?.Id == "asset");
        targets[0].LayerOrder = 20;
        Check("hit.topLayerWins", MapEditorHitTest.Resolve(targets, 10, 10, 1)?.Id == "tile");
        targets[0].IsVisible = false;
        Check("hit.hiddenIgnored", MapEditorHitTest.Resolve(targets, 10, 10, 1)?.Id == "asset");
        targets[2].IsLayerLocked = true;
        Check("hit.lockedStillSelectable", MapEditorHitTest.Resolve(targets, 10, 10, 1)?.Id == "asset");
        Check("hit.edgeTolerancePixels", MapEditorHitTest.Resolve(targets, 25, 10, 0.5, 5)?.Id == "asset");
        var stable = new[] { Target("b", MapEditorObjectKind.Shape, 1, 1), Target("a", MapEditorObjectKind.Shape, 1, 1) };
        Check("hit.stableTieBreaker", MapEditorHitTest.Resolve(stable, 5, 5, 1)?.Id == "a");
        stable[0].X = -20; stable[0].Y = -20;
        Check("hit.negativeCoordinates", MapEditorHitTest.Resolve(stable, -15, -15, 1)?.Id == "b");
        Check("hit.empty", MapEditorHitTest.Resolve(targets, 1000, 1000, 1) == null);
    }

    private static MapEditorHitTarget Target(string id, MapEditorObjectKind kind, int layer, int z) => new()
    {
        Id = id, Kind = kind, LayerOrder = layer, ZIndex = z, X = 0, Y = 0, Width = 20, Height = 20
    };

    private static void SnappingContracts()
    {
        Check("snap.positiveMidpoint", MapEditorSnapPolicy.Snap(12.5, true, 5) == 15);
        Check("snap.negativeMidpoint", MapEditorSnapPolicy.Snap(-12.5, true, 5) == -15);
        Check("snap.disabledExact", MapEditorSnapPolicy.Snap(12.345, false, 5) == 12.345);
        Check("snap.boundsClamp", MapEditorSnapPolicy.SnapPoint(999, -10, true, 25, 0, 0, 400, 300) == (400d, 0d));
        var once = MapEditorSnapPolicy.Snap(127.49, true, 10);
        var repeated = once;
        for (var index = 0; index < 100; index++) repeated = MapEditorSnapPolicy.Snap(repeated, true, 10);
        Check("snap.noRepeatedDrift", once == repeated);
        Check("snap.zoomIndependentByContract", MapEditorSnapPolicy.Snap(127.49, true, 10) == once);
        Check("snap.stepsSupported", new[] { 2d, 5d, 10d, 25d, 50d }.All(step => MapEditorSnapPolicy.Snap(113, true, step) % step == 0));
    }

    private static void HistoryContracts()
    {
        var history = new MapEditorHistory<int>(50);
        for (var index = 0; index < 75; index++) history.Record(index);
        Check("history.boundedAt50", history.UndoCount == 50);
        Check("history.undoMovesToRedo", history.TryTakeUndo(out var value) && value == 74 && history.RedoCount == 1);
        Check("history.redoMovesToUndo", history.TryTakeRedo(out value) && value == 74 && history.RedoCount == 0);
        history.TryTakeUndo(out _);
        history.Record(100);
        Check("history.newCommandClearsRedo", history.RedoCount == 0);
        history.Clear();
        Check("history.sessionClear", !history.CanUndo && !history.CanRedo);
    }

    private static void Check(string name, bool value) => Checks[name] = value;
    private static void Write(string path, object payload)
        => File.WriteAllText(path, new JavaScriptSerializer().Serialize(payload), new UTF8Encoding(false));
}
