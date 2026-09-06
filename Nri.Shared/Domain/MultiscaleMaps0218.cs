using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Nri.Shared.Domain;

public static class MapCoordinateProfileKindIds0218
{
    public const string LocalCartesian2D = "local_cartesian_2d";
    public const string SquareGrid = "square_grid";
    public const string HexGrid = "hex_grid_axial";
    public const string Geographic2D = "geographic_2d";
    public const string SchematicNodeSpace = "schematic_node_space";
}

public static class MapScaleKindIds0218
{
    public const string PhysicalLinear = "physical_linear";
    public const string GridPhysical = "grid_physical";
    public const string Geographic = "geographic";
    public const string Schematic = "schematic";
    public const string Abstract = "abstract";
}

public static class MapGeometryKindIds0218
{
    public const string Point = "point";
    public const string Polyline = "polyline";
    public const string Polygon = "polygon";
}

public static class MapSemanticKindIds0218
{
    public const string Road = "road";
    public const string River = "river";
    public const string Border = "border";
    public const string Area = "area";
    public const string PointOfInterest = "point_of_interest";
    public const string Secret = "secret";
    public const string Settlement = "settlement";
    public const string District = "district";
    public const string Room = "room";
    public const string Structure = "structure";
    public const string Entrance = "entrance";
    public const string Stairs = "stairs";
    public const string Label = "label";
    public const string Star = "star";
    public const string Planet = "planet";
    public const string Station = "station";
}

public static class MapDiscoveryPrecisionIds0218
{
    public const string Hidden = "hidden";
    public const string Approximate = "approximate";
    public const string Exact = "exact";
}

public static class MapGenerationScopeIds0218
{
    public const string Region = "region";
    public const string Settlement = "settlement";
    public const string Dungeon = "dungeon";
    public const string Sector = "sector";
    public const string System = "system";
    public const string Planet = "planet";
}

public sealed class MapCoordinateProfileDefinition0218 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = MapCoordinateProfileKindIds0218.LocalCartesian2D;
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double UnitsPerMapUnit { get; set; } = 1d;
    public string CanonicalUnit { get; set; } = "metre";
    public string AxisOrientation { get; set; } = "x_east_y_north";
    public double RotationDegrees { get; set; }
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public string HexOrientation { get; set; } = "pointy";
    public string HexCoordinateStorage { get; set; } = "axial_q_r";
    public double HexSize { get; set; } = 1d;
    public string ProjectionId { get; set; } = string.Empty;
    public string WrapPolicy { get; set; } = "none";
    public string DistancePolicy { get; set; } = "profile_defined";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public long Revision { get; set; }
}

public sealed class MapScaleProfileDefinition0218 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = MapScaleKindIds0218.PhysicalLinear;
    public double MetresPerMapUnit { get; set; } = 1d;
    public double MetresPerGridCell { get; set; }
    public bool SupportsExactDistance { get; set; } = true;
    public string DisplayUnit { get; set; } = "м";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public long Revision { get; set; }
}

public sealed class MapPoint0218
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class MapSemanticLayerState0218 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LayerKind { get; set; } = MapSemanticKindIds0218.Area;
    public int SortOrder { get; set; }
    public bool IsVisibleToPlayers { get; set; }
    public bool IsLocked { get; set; }
    public bool IsArchived { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public long Revision { get; set; }
}

public sealed class MapSemanticFeatureState0218 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string LayerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SemanticKind { get; set; } = MapSemanticKindIds0218.PointOfInterest;
    public string GeometryKind { get; set; } = MapGeometryKindIds0218.Point;
    public List<MapPoint0218> Points { get; set; } = new List<MapPoint0218>();
    public string BoundWorldEntityId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsSecret { get; set; }
    public bool IsManual { get; set; }
    public bool IsArchived { get; set; }
    public string GenerationIdentity { get; set; } = string.Empty;
    public string GeneratorProvenanceId { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string StyleKey { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public long Revision { get; set; }
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class MapPortalState0218 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string SourceMapId { get; set; } = string.Empty;
    public string TargetMapId { get; set; } = string.Empty;
    public string SourceFeatureId { get; set; } = string.Empty;
    public string TargetFeatureId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsSecret { get; set; }
    public bool IsArchived { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public long Revision { get; set; }
}

public sealed class MapGeneratorRecipeDefinition0218 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string GeneratorKind { get; set; } = MapGenerationScopeIds0218.Region;
    public string AlgorithmId { get; set; } = "nri_semantic_map";
    public int AlgorithmVersion { get; set; } = 1;
    public int RecipeVersion { get; set; } = 1;
    public Dictionary<string, object> Constraints { get; set; } = new Dictionary<string, object>();
    public bool IsArchived { get; set; }
}

public sealed class MapGenerationJobState0218 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string RecipeDefinitionId { get; set; } = string.Empty;
    public int RecipeVersion { get; set; }
    public string GeneratorKind { get; set; } = string.Empty;
    public string GeneratorAlgorithmId { get; set; } = string.Empty;
    public int GeneratorAlgorithmVersion { get; set; }
    public string Seed { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public Dictionary<string, object> InputConstraints { get; set; } = new Dictionary<string, object>();
    public List<MapSemanticFeatureState0218> PreviewFeatures { get; set; } = new List<MapSemanticFeatureState0218>();
    public string OutputSemanticHash { get; set; } = string.Empty;
    public string Status { get; set; } = "preview";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
}

public static class StableMapPrng0218
{
    public static ulong Value(string seed, string scope, int ordinal)
    {
        var bytes = Hash(Encoding.UTF8.GetBytes(string.Join("|", seed ?? string.Empty, scope ?? string.Empty, ordinal.ToString(CultureInfo.InvariantCulture))));
        return BitConverter.ToUInt64(bytes, 0);
    }

    public static double Unit(string seed, string scope, int ordinal)
        => Value(seed, scope, ordinal) / (double)ulong.MaxValue;

    public static string SemanticHash(IEnumerable<MapSemanticFeatureState0218> features)
    {
        var canonical = string.Join("\n", (features ?? Array.Empty<MapSemanticFeatureState0218>())
            .OrderBy(feature => feature.GenerationIdentity, StringComparer.Ordinal)
            .Select(feature => string.Join("|", feature.GenerationIdentity, feature.SemanticKind, feature.GeometryKind,
                string.Join(";", feature.Points.Select(point => FormattableString.Invariant($"{point.X:0.######},{point.Y:0.######}"))))));
        return BitConverter.ToString(Hash(Encoding.UTF8.GetBytes(canonical))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static byte[] Hash(byte[] input)
    {
        using (var sha = SHA256.Create())
            return sha.ComputeHash(input);
    }
}

public static class MapDistance0218
{
    public static double EuclideanMetres(MapPoint0218 first, MapPoint0218 second, MapScaleProfileDefinition0218 scale)
    {
        if (!scale.SupportsExactDistance || scale.Kind is MapScaleKindIds0218.Schematic or MapScaleKindIds0218.Abstract)
            throw new InvalidOperationException("Для схематической карты физическое расстояние не определено.");
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        return Math.Sqrt(dx * dx + dy * dy) * scale.MetresPerMapUnit;
    }
}
