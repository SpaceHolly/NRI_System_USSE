using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Ui.Wpf.Controls;

namespace Nri.DevelopmentProduct.Contracts;

internal static class Program
{
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_21_5/development_hexagon_product_projection_contract.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        var root = Node("novice", DevelopmentNodeRoleIds.NoviceRoot, DevelopmentNodeTypes.Class, 0);
        var tier1 = Node("path_t1", DevelopmentNodeRoleIds.MainBranchLevel, DevelopmentNodeTypes.Class, 1, "strength_assault", "warrior");
        var tier2 = Node("path_t2", DevelopmentNodeRoleIds.MainBranchLevel, DevelopmentNodeTypes.Class, 2, "strength_assault", "warrior");
        var specialization = Node("path_spec", DevelopmentNodeRoleIds.SubbranchLevel, DevelopmentNodeTypes.Specialization, 3, "strength_assault", "warrior_spec");
        var milestone = Node("path_t5", DevelopmentNodeRoleIds.UnlockNode, DevelopmentNodeTypes.License, 5, "strength_assault", "warrior");
        var hidden = Node("gm_hidden", DevelopmentNodeRoleIds.HiddenNode, DevelopmentNodeTypes.HiddenDevelopment, 1);
        hidden.IsGMOnly = true;
        hidden.IsPlayerVisible = false;
        var diagnostic = Node("perf_0153_node_001", DevelopmentNodeRoleIds.Custom, DevelopmentNodeTypes.Other, 1);
        var paladin = Node("class_paladin", "cross_class", DevelopmentNodeTypes.Specialization, 10, "endurance_resilience", "class_paladin");
        var wallborn = Node("class_wallborn", "cross_class", DevelopmentNodeTypes.Specialization, 10, "endurance_resilience", "class_wallborn");
        var assassin = Node("class_assassin", "cross_class", DevelopmentNodeTypes.Specialization, 10, "dexterity_maneuver", "class_assassin");
        var unresolved = Node("unresolved", DevelopmentNodeRoleIds.MainBranchLevel, DevelopmentNodeTypes.Class, 1);
        unresolved.PurchasePolicy = DevelopmentPurchasePolicyIds.UnavailableUntilDefined;
        var explicitGm = Node("explicit_gm", DevelopmentNodeRoleIds.MainBranchLevel, DevelopmentNodeTypes.Class, 1);
        explicitGm.PurchasePolicy = DevelopmentPurchasePolicyIds.RequiresGMApproval;
        var explicitRequest = Node("explicit_request", DevelopmentNodeRoleIds.MainBranchLevel, DevelopmentNodeTypes.Class, 1);
        explicitRequest.PurchasePolicy = DevelopmentPurchasePolicyIds.RequestOnly;

        checks["projection.rootKind"] = DevelopmentProductProjectionPolicy0215.Classify(root, 1) == DevelopmentPresentationKinds0215.Root;
        checks["projection.pathKind"] = DevelopmentProductProjectionPolicy0215.Classify(tier1, 1) == DevelopmentPresentationKinds0215.Path;
        checks["projection.groupedTierKind"] = DevelopmentProductProjectionPolicy0215.Classify(tier1, 2) == DevelopmentPresentationKinds0215.InternalProgression;
        checks["projection.specializationKind"] = DevelopmentProductProjectionPolicy0215.Classify(specialization, 1) == DevelopmentPresentationKinds0215.Specialization;
        checks["projection.milestoneKind"] = DevelopmentProductProjectionPolicy0215.Classify(milestone, 1) == DevelopmentPresentationKinds0215.Milestone;
        checks["projection.hiddenRemoved"] = !DevelopmentProductProjectionPolicy0215.IsPlayerSafeCandidate(hidden);
        checks["projection.diagnosticRemoved"] = !DevelopmentProductProjectionPolicy0215.IsPlayerSafeCandidate(diagnostic);
        checks["projection.paladinMixedPath"] = DevelopmentProductProjectionPolicy0215.Classify(paladin, 1) == DevelopmentPresentationKinds0215.MixedPath;
        checks["projection.wallbornMixedPath"] = DevelopmentProductProjectionPolicy0215.Classify(wallborn, 1) == DevelopmentPresentationKinds0215.MixedPath;
        checks["projection.assassinSingleParent"] = DevelopmentProductProjectionPolicy0215.Classify(assassin, 1) == DevelopmentPresentationKinds0215.Specialization;
        checks["projection.stablePathGrouping"] = DevelopmentProductProjectionPolicy0215.StablePathKey(DevelopmentHexagonIds.Main, tier1, "strength_assault") == DevelopmentProductProjectionPolicy0215.StablePathKey(DevelopmentHexagonIds.Main, tier2, "strength_assault");
        checks["projection.overviewBounded"] = DevelopmentProductProjectionPolicy0215.BoundOverview(Enumerable.Range(1, 600)).Count == 48;
        checks["projection.stableOrdering"] = DevelopmentProductProjectionPolicy0215.BoundOverview(new[] { 1, 2, 3 }).SequenceEqual(new[] { 1, 2, 3 });
        checks["projection.affordabilityBoundary"] = !DevelopmentProductProjectionPolicy0215.CanAfford(10, 11)
            && DevelopmentProductProjectionPolicy0215.CanAfford(11, 11)
            && DevelopmentProductProjectionPolicy0215.CanAfford(0, 0);
        checks["identity.mainRoot"] = DevelopmentProductProjectionPolicy0215.RootLabel(DevelopmentHexagonIds.Main) == "Новичок";
        checks["identity.magicRoot"] = DevelopmentProductProjectionPolicy0215.RootLabel(DevelopmentHexagonIds.Magic) == "Магия";
        checks["identity.sixMainDirections"] = DevelopmentProductProjectionPolicy0215.MainDirectionLabels.Count == 6 && DevelopmentProductProjectionPolicy0215.MainDirectionLabels.Distinct().Count() == 6;
        checks["mutation.proceed"] = DevelopmentProductMutationGuard0215.Evaluate(4, 4, Array.Empty<string>(), "op-123456") == DevelopmentProductMutationDecision0215.Proceed;
        checks["mutation.replayBeforeRevision"] = DevelopmentProductMutationGuard0215.Evaluate(5, 4, new[] { "op-123456" }, "op-123456") == DevelopmentProductMutationDecision0215.Replay;
        checks["mutation.revisionConflict"] = DevelopmentProductMutationGuard0215.Evaluate(5, 4, Array.Empty<string>(), "op-654321") == DevelopmentProductMutationDecision0215.Conflict;
        checks["approval.unresolvedNotGm"] = !DevelopmentApprovalPolicy.RequiresGMApproval(unresolved);
        checks["approval.unresolvedNotRequest"] = !DevelopmentApprovalPolicy.RequiresPlayerRequest(unresolved);
        checks["approval.explicitGmPreserved"] = DevelopmentApprovalPolicy.RequiresGMApproval(explicitGm);
        checks["approval.explicitGmDoesNotInventRequest"] = !DevelopmentApprovalPolicy.RequiresPlayerRequest(explicitGm);
        checks["approval.explicitRequestPreserved"] = DevelopmentApprovalPolicy.RequiresPlayerRequest(explicitRequest);

        var pass = checks.Count == 25 && checks.Values.All(value => value);
        var artifact = new Dictionary<string, object>
        {
            { "status", pass ? "PASS" : "NOT_PASS" },
            { "computedFromContracts", true },
            { "overviewLimit", DevelopmentProductProjectionPolicy0215.OverviewLimit },
            { "checks", checks.ToDictionary(pair => pair.Key, pair => (object)pair.Value) },
            { "executedAtUtc", DateTime.UtcNow }
        };
        File.WriteAllText(output, JsonProtocolSerializer.Serialize(artifact), new UTF8Encoding(false));
        var sectorOutput = Path.GetFullPath(args.Length > 1
            ? args[1]
            : Path.Combine(Path.GetDirectoryName(output) ?? ".", "sector_geometry_contract.json"));
        var sectorPass = WriteSectorGeometryContract(sectorOutput);
        Console.WriteLine("0.21.5 development product contracts: " + (pass ? "PASS" : "NOT_PASS"));
        return pass && sectorPass ? 0 : 1;
    }

    private static ClassNodeDefinition Node(string id, string role, string type, int tier, string direction = "", string branch = "")
        => new ClassNodeDefinition
        {
            NodeId = id, NodeRole = role, NodeType = type, Tier = tier, DirectionId = direction, BranchId = branch,
            HexagonId = DevelopmentHexagonIds.Main, IsPlayerVisible = true, VisibilityRule = "public"
        };

    private static bool WriteSectorGeometryContract(string output)
    {
        const double width = DevelopmentSpatialGeometry.DesignWidth;
        const double height = DevelopmentSpatialGeometry.DesignHeight;
        var sectors = DevelopmentSpatialGeometry.CreateSectorPolygons(width, height).ToList();
        var counterpart = new[] { 3, 4, 5, 0, 1, 2 };
        var directionCenters = Enumerable.Range(0, 6)
            .Select(DevelopmentSpatialGeometry.OverviewDirectionCenter)
            .ToList();
        var rows = new List<object>();
        var maxPairDifference = 0d;
        for (var index = 0; index < sectors.Count; index++)
        {
            var sector = sectors[index];
            var pair = sectors[counterpart[index]];
            var pairDifference = RelativeDifference(sector.Area, pair.Area);
            maxPairDifference = Math.Max(maxPairDifference, pairDifference);
            var node = directionCenters[index];
            var nodeAngle = NormalizeAngle(Math.Atan2(node.Y - DevelopmentSpatialGeometry.CenterY, node.X - DevelopmentSpatialGeometry.CenterX) * 180d / Math.PI);
            var expectedAngle = NormalizeAngle(sector.CenterAngle);
            var alignmentError = AngularDifference(nodeAngle, expectedAngle);
            rows.Add(new Dictionary<string, object>
            {
                { "semanticId", sector.SemanticId },
                { "centerAngle", sector.CenterAngle },
                { "startAngle", sector.StartAngle },
                { "endAngle", sector.EndAngle },
                { "span", sector.EndAngle - sector.StartAngle },
                { "polygonArea", Math.Round(sector.Area, 4) },
                { "mirroredCounterpart", pair.SemanticId },
                { "mirrorAreaDifference", Math.Round(pairDifference, 6) },
                { "bounds", new Dictionary<string, object> { { "x", sector.Bounds.X }, { "y", sector.Bounds.Y }, { "width", sector.Bounds.Width }, { "height", sector.Bounds.Height } } },
                { "clipBounds", new Dictionary<string, object> { { "x", 0 }, { "y", 0 }, { "width", width }, { "height", height } } },
                { "directionNodeCenterAngle", Math.Round(nodeAngle, 4) },
                { "alignmentError", Math.Round(alignmentError, 6) }
            });
        }

        var strengthLeft = DevelopmentSpatialGeometry.PolygonArea(DevelopmentSpatialGeometry.CreateSectorSlice(width, height, -120, -90));
        var strengthRight = DevelopmentSpatialGeometry.PolygonArea(DevelopmentSpatialGeometry.CreateSectorSlice(width, height, -90, -60));
        var wisdomLeft = DevelopmentSpatialGeometry.PolygonArea(DevelopmentSpatialGeometry.CreateSectorSlice(width, height, 60, 90));
        var wisdomRight = DevelopmentSpatialGeometry.PolygonArea(DevelopmentSpatialGeometry.CreateSectorSlice(width, height, 90, 120));
        var coverageDegrees = sectors.Sum(sector => sector.EndAngle - sector.StartAngle);
        var coveredArea = sectors.Sum(sector => sector.Area);
        var areaCoverageDifference = RelativeDifference(coveredArea, width * height);
        var strengthMirrorDifference = RelativeDifference(strengthLeft, strengthRight);
        var wisdomMirrorDifference = RelativeDifference(wisdomLeft, wisdomRight);
        var alignmentMaximum = rows.Cast<Dictionary<string, object>>().Max(row => Convert.ToDouble(row["alignmentError"]));
        var gapCount = Math.Abs(coverageDegrees - 360d) <= 0.2d && areaCoverageDifference <= 0.002d ? 0 : 1;
        var overlapCount = coveredArea > width * height * 1.002d ? 1 : 0;
        var status = sectors.Count == 6
                     && sectors.All(sector => Math.Abs((sector.EndAngle - sector.StartAngle) - 60d) <= 0.1d)
                     && Math.Abs(coverageDegrees - 360d) <= 0.2d
                     && gapCount == 0
                     && overlapCount == 0
                     && alignmentMaximum <= 1d
                     && strengthLeft > 0 && strengthRight > 0
                     && strengthMirrorDifference <= 0.02d
                     && wisdomMirrorDifference <= 0.02d
                     && maxPairDifference <= 0.02d;

        var document = new Dictionary<string, object>
        {
            { "status", status ? "PASS" : "NOT_PASS" },
            { "computed", true },
            { "screenSpaceConvention", "top=-90deg; clockwise-positive" },
            { "sectorCount", sectors.Count },
            { "coverageDegrees", coverageDegrees },
            { "coveredArea", Math.Round(coveredArea, 4) },
            { "canvasArea", width * height },
            { "gapCount", gapCount },
            { "unexpectedOverlapCount", overlapCount },
            { "maximumDirectionCenterAlignmentError", alignmentMaximum },
            { "strengthLeftHalfPresent", strengthLeft > 0 },
            { "strengthRightHalfPresent", strengthRight > 0 },
            { "strengthMirrorAreaDifference", Math.Round(strengthMirrorDifference, 6) },
            { "wisdomMirrorAreaDifference", Math.Round(wisdomMirrorDifference, 6) },
            { "maximumPairedSectorAreaDifference", Math.Round(maxPairDifference, 6) },
            { "sectors", rows },
            { "executedAtUtc", DateTime.UtcNow }
        };
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
        File.WriteAllText(output, new JavaScriptSerializer().Serialize(document), new UTF8Encoding(false));
        Console.WriteLine("0.21.5B sector geometry: " + (status ? "PASS" : "NOT_PASS"));
        return status;
    }

    private static double RelativeDifference(double left, double right)
        => Math.Abs(left - right) / Math.Max(1d, Math.Max(Math.Abs(left), Math.Abs(right)));

    private static double NormalizeAngle(double angle)
    {
        var value = angle % 360d;
        return value < 0 ? value + 360d : value;
    }

    private static double AngularDifference(double left, double right)
    {
        var difference = Math.Abs(NormalizeAngle(left) - NormalizeAngle(right));
        return Math.Min(difference, 360d - difference);
    }
}
