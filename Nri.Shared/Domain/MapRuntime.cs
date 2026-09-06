using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class MapFeatureFlags
{
    public const bool UseMapSystemV1 = false;
    public const bool UseSpaceHierarchyV1 = false;
    public const bool UseSceneMapV1 = false;
    public const bool UseSceneMapMarkers = false;
    public const bool UseSceneMapFogOfWar = false;
    public const bool UseSceneMapPlayerView = false;
    public const bool UseSceneMapSessionLink = false;
    public const bool UseWorldMapV1 = false;
    public const bool UseWorldMapPainterMvp = false;
    public const bool UseWorldMapLayers = false;
    public const bool UseWorldMapHeightDepthLayer = false;
    public const bool UseWorldMapBiomeLayer = false;
    public const bool UseWorldMapPoliticalLayer = false;
    public const bool UseWorldMapMarkers = false;
    public const bool UseWorldMapPlayerView = false;
    public const bool UseRoomMapMvp = false;
    public const bool UseRoomInteriorV1 = false;
    public const bool UseRoomMarkers = false;
    public const bool UseRoomPlayerView = false;
    public const bool UseRoomGeneratorBoundary = false;
    public const bool UseMapDebugEndpoints = false;
}

public static class MapSpaceNodeTypeIds
{
    public const string Dimension = "dimension";
    public const string Galaxy = "galaxy";
    public const string Sector = "sector";
    public const string Subsector = "subsector";
    public const string World = "world";
    public const string Continent = "continent";
    public const string State = "state";
    public const string Settlement = "settlement";
    public const string District = "district";
    public const string SubLocation = "sub_location";
    public const string StarSystem = "star_system";
    public const string Star = "star";
    public const string Planet = "planet";
    public const string WorldMap = "world_map";
    public const string SceneMap = "scene_map";
    public const string Room = "room";
    public const string Location = "location";
    public const string Region = "region";
    public const string Country = "country";
    public const string City = "city";
    public const string Interior = "interior";
    public const string Dungeon = "dungeon";
    public const string Moon = "moon";
    public const string Orbital = "orbital";
    public const string Custom = "custom";
}

public static class MapTypeIds
{
    public const string World = "world";
    public const string Continent = "continent";
    public const string Region = "region";
    public const string State = "state";
    public const string Settlement = "settlement";
    public const string District = "district";
    public const string Location = "location";
    public const string WorldMap = "world_map";
    public const string Scene = "scene";
    public const string Room = "room";
    public const string Interior = "interior";
    public const string Dungeon = "dungeon";
    public const string BattleScene = "battle_scene";
    public const string Galaxy = "galaxy";
    public const string Sector = "sector";
    public const string Subsector = "subsector";
    public const string StarSystem = "star_system";
    public const string Planet = "planet";
    public const string Moon = "moon";
    public const string Orbital = "orbital";
    public const string PlanetMap = "planet_map";
    public const string Custom = "custom";
}

public static class RoomTypeIds
{
    public const string Room = "room";
    public const string Hall = "hall";
    public const string Corridor = "corridor";
    public const string Chamber = "chamber";
    public const string Entrance = "entrance";
    public const string Exit = "exit";
    public const string Storage = "storage";
    public const string LivingSpace = "living_space";
    public const string Workshop = "workshop";
    public const string Laboratory = "laboratory";
    public const string Office = "office";
    public const string Barracks = "barracks";
    public const string Hangar = "hangar";
    public const string EngineRoom = "engine_room";
    public const string Bridge = "bridge";
    public const string DungeonRoom = "dungeon_room";
    public const string Cave = "cave";
    public const string Ruin = "ruin";
    public const string Custom = "custom";
}

public static class InteriorTypeIds
{
    public const string Building = "building";
    public const string Dungeon = "dungeon";
    public const string Ship = "ship";
    public const string Airship = "airship";
    public const string Vehicle = "vehicle";
    public const string Station = "station";
    public const string Cave = "cave";
    public const string Camp = "camp";
    public const string Fortification = "fortification";
    public const string Underground = "underground";
    public const string Custom = "custom";
}

public static class RoomLayoutModeIds
{
    public const string None = "none";
    public const string Grid = "grid";
    public const string SimpleRect = "simple_rect";
    public const string ImportedLater = "imported_later";
    public const string GeneratedLater = "generated_later";
}

public static class WorldMapProjectionModeIds
{
    public const string FlatGrid = "flat_grid";
    public const string EquirectangularPlaceholder = "equirectangular_placeholder";
    public const string Custom = "custom";
}

public static class WorldMapCoordinateModeIds
{
    public const string Grid = "grid";
    public const string Normalized = "normalized";
    public const string WorldUnits = "world_units";
}

public static class WorldMapDataEncodingIds
{
    public const string CellGrid = "cell_grid";
    public const string SparseCells = "sparse_cells";
    public const string RegionBlocks = "region_blocks";
    public const string JsonCompact = "json_compact";
}

public static class WorldMapLayerTypeIds
{
    public const string HeightDepth = "height_depth";
    public const string Biome = "biome";
    public const string Political = "political";
    public const string Marker = "marker";
    public const string Annotation = "annotation";
    public const string Custom = "custom";
}

public static class WorldMapHeightDepthCategoryIds
{
    public const string DeepOcean = "deep_ocean";
    public const string ShallowSea = "shallow_sea";
    public const string Coast = "coast";
    public const string Lowland = "lowland";
    public const string Highland = "highland";
    public const string Mountain = "mountain";
    public const string ExtremeMountain = "extreme_mountain";
    public const string Custom = "custom";
}

public static class WorldMapBiomeIds
{
    public const string Ocean = "ocean";
    public const string Coast = "coast";
    public const string TropicalForest = "tropical_forest";
    public const string Forest = "forest";
    public const string Plains = "plains";
    public const string Savanna = "savanna";
    public const string Desert = "desert";
    public const string Mountains = "mountains";
    public const string Tundra = "tundra";
    public const string Subarctic = "subarctic";
    public const string Swamp = "swamp";
    public const string Urban = "urban";
    public const string Custom = "custom";
}

public static class WorldMapOwnerTypeIds
{
    public const string Country = "country";
    public const string Region = "region";
    public const string Faction = "faction";
    public const string Organization = "organization";
    public const string Custom = "custom";
}

public static class MapCoordinateModes
{
    public const string MetersFromOrigin = "meters_from_origin";
    public const string ArbitraryGrid = "arbitrary_grid";
}

public static class MapBackgroundModes
{
    public const string None = "none";
    public const string Solid = "solid";
    public const string Image = "image";
}

public static class MapVisibilityModes
{
    public const string Public = "public";
    public const string Party = "party";
    public const string GmOnly = "gm_only";
    public const string Hidden = "hidden";
}

public static class MapMarkerTypeIds
{
    public const string Character = "character";
    public const string PlayerCharacter = "player_character";
    public const string Npc = "npc";
    public const string Companion = "companion";
    public const string Enemy = "enemy";
    public const string Neutral = "neutral";
    public const string PointOfInterest = "point_of_interest";
    public const string Entrance = "entrance";
    public const string Exit = "exit";
    public const string Cover = "cover";
    public const string Door = "door";
    public const string Window = "window";
    public const string Container = "container";
    public const string Furniture = "furniture";
    public const string Trap = "trap";
    public const string Objective = "objective";
    public const string Hazard = "hazard";
    public const string Item = "item";
    public const string Vehicle = "vehicle";
    public const string Continent = "continent";
    public const string Country = "country";
    public const string Capital = "capital";
    public const string City = "city";
    public const string CityState = "city_state";
    public const string Region = "region";
    public const string Location = "location";
    public const string RoutePoint = "route_point";
    public const string Port = "port";
    public const string Ruin = "ruin";
    public const string Dungeon = "dungeon";
    public const string FactionBase = "faction_base";
    public const string Custom = "custom";
}

public static class MapMarkerBindingTypeIds
{
    public const string Room = "room";
    public const string Interior = "interior";
    public const string Character = "character";
    public const string CombatParticipant = "combat_participant";
    public const string Npc = "npc";
    public const string Companion = "companion";
    public const string Location = "location";
    public const string Item = "item";
    public const string Faction = "faction";
    public const string Organization = "organization";
    public const string SpaceNode = "space_node";
    public const string Continent = "continent";
    public const string Country = "country";
    public const string CityState = "city_state";
    public const string Region = "region";
    public const string Custom = "custom";
}

public static class FogOfWarModeIds
{
    public const string Disabled = "disabled";
    public const string Manual = "manual";
}

public static class FogDefaultStateIds
{
    public const string Revealed = "revealed";
    public const string Hidden = "hidden";
}

public sealed class MapSpaceNodeState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public string NodeType { get; set; } = MapSpaceNodeTypeIds.Custom;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Visibility { get; set; } = MapVisibilityModes.Public;
    public bool IsArchived { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public string LinkedDefinitionId { get; set; } = string.Empty;
    public string LinkedDefinitionCategory { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class MapCanvasState : EntityBase
{
    public string WorldId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string SpaceNodeId { get; set; } = string.Empty;
    public List<string> BoundWorldEntityIds { get; set; } = new List<string>();
    public string PrimaryBoundWorldEntityId { get; set; } = string.Empty;
    public string CoordinateProfileId { get; set; } = string.Empty;
    public string ScaleProfileId { get; set; } = string.Empty;
    public string ParentMapId { get; set; } = string.Empty;
    public List<string> LayerIds { get; set; } = new List<string>();
    public string MapType { get; set; } = MapTypeIds.Custom;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int WidthMeters { get; set; } = MapRuntimeValidation.SceneDefaultSizeMeters;
    public int HeightMeters { get; set; } = MapRuntimeValidation.SceneDefaultSizeMeters;
    public int GridCellSizeMeters { get; set; } = 25;
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public string CoordinateMode { get; set; } = MapCoordinateModes.MetersFromOrigin;
    public string BackgroundMode { get; set; } = MapBackgroundModes.None;
    public string BackgroundAssetId { get; set; } = string.Empty;
    public string BackgroundImagePath { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Public;
    public string KnowledgePolicy { get; set; } = "character_discovery";
    public long EntityRevision { get; set; }
    public string GeneratorProvenanceId { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public long EditorRevision { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
}

public sealed class SceneMapSettingsState : EntityBase
{
    public string MapId { get; set; } = string.Empty;
    public int WidthMeters { get; set; } = MapRuntimeValidation.SceneDefaultSizeMeters;
    public int HeightMeters { get; set; } = MapRuntimeValidation.SceneDefaultSizeMeters;
    public int GridCellSizeMeters { get; set; } = 25;
    public bool ShowGrid { get; set; } = true;
    public bool ShowCoordinates { get; set; } = true;
    public bool MarkerLayerEnabled { get; set; } = true;
    public bool FogOfWarEnabled { get; set; }
    public bool PlayerViewEnabled { get; set; }
    public string DefaultPlayerVisibility { get; set; } = MapVisibilityModes.Party;
    public string ScaleLabel { get; set; } = "2x2 км";
    public string Notes { get; set; } = string.Empty;
}

public sealed class MapMarkerState : EntityBase
{
    public string MapId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = MapMarkerTypeIds.Custom;
    public double X { get; set; }
    public double Y { get; set; }
    public double? XNormalized { get; set; }
    public double? YNormalized { get; set; }
    public int? CellX { get; set; }
    public int? CellY { get; set; }
    public double Z { get; set; }
    public int Layer { get; set; }
    public double RotationDegrees { get; set; }
    public double SizeMeters { get; set; } = 1d;
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public string LinkedEntityDisplayName { get; set; } = string.Empty;
    public string LinkedEntityPublicLabel { get; set; } = string.Empty;
    public string LinkedCharacterId { get; set; } = string.Empty;
    public string LinkedCombatParticipantId { get; set; } = string.Empty;
    public string LinkedNpcId { get; set; } = string.Empty;
    public string LinkedCompanionId { get; set; } = string.Empty;
    public string LinkedLocationId { get; set; } = string.Empty;
    public string LinkedSpaceNodeId { get; set; } = string.Empty;
    public string LinkedContinentId { get; set; } = string.Empty;
    public string LinkedCountryId { get; set; } = string.Empty;
    public string LinkedCityStateId { get; set; } = string.Empty;
    public string LinkedRegionId { get; set; } = string.Empty;
    public string LinkedFactionId { get; set; } = string.Empty;
    public string LinkedOrganizationId { get; set; } = string.Empty;
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class MapMarkerBindingState : EntityBase
{
    public string MapId { get; set; } = string.Empty;
    public string MarkerId { get; set; } = string.Empty;
    public string BindingType { get; set; } = MapMarkerBindingTypeIds.Custom;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string Visibility { get; set; } = MapVisibilityModes.Party;
}

public sealed class MapFogCellRange
{
    public int FromX { get; set; }
    public int FromY { get; set; }
    public int ToX { get; set; }
    public int ToY { get; set; }
}

public sealed class FogOfWarState : EntityBase
{
    public string MapId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public int CellSizeMeters { get; set; } = 25;
    public string Mode { get; set; } = FogOfWarModeIds.Manual;
    public string DefaultState { get; set; } = FogDefaultStateIds.Revealed;
    public List<MapFogCellRange> HiddenCells { get; set; } = new List<MapFogCellRange>();
    public List<MapFogCellRange> RevealedCells { get; set; } = new List<MapFogCellRange>();
    public List<MapFogCellRange> GMOnlyCells { get; set; } = new List<MapFogCellRange>();
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public long Revision { get; set; }
}

public sealed class SceneMapActiveLinkState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActiveGroupId { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public string AssignedByUserId { get; set; } = string.Empty;
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class WorldMapState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string SpaceNodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MapScaleMode { get; set; } = string.Empty;
    public int WidthCells { get; set; } = MapRuntimeValidation.WorldDefaultWidthCells;
    public int HeightCells { get; set; } = MapRuntimeValidation.WorldDefaultHeightCells;
    public double? CellSizeKm { get; set; }
    public double? WidthWorldUnits { get; set; }
    public double? HeightWorldUnits { get; set; }
    public string CoordinateMode { get; set; } = WorldMapCoordinateModeIds.Grid;
    public string ProjectionMode { get; set; } = WorldMapProjectionModeIds.FlatGrid;
    public bool IsPlanetaryMap { get; set; }
    public string LinkedWorldId { get; set; } = string.Empty;
    public string LinkedPlanetId { get; set; } = string.Empty;
    public string LinkedContinentId { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class WorldMapLayerState : EntityBase
{
    public string WorldMapId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string LayerType { get; set; } = WorldMapLayerTypeIds.Custom;
    public string Name { get; set; } = string.Empty;
    public bool IsVisibleToGM { get; set; } = true;
    public bool IsVisibleToPlayers { get; set; }
    public int SortOrder { get; set; }
    public double Opacity { get; set; } = 1d;
    public int CellResolution { get; set; } = 1;
    public string DataEncoding { get; set; } = WorldMapDataEncodingIds.SparseCells;
    public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    public string LegendId { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class HeightDepthCellValue
{
    public int CellX { get; set; }
    public int CellY { get; set; }
    public double Value { get; set; }
    public string Category { get; set; } = string.Empty;
}

public sealed class BiomeCellValue
{
    public int CellX { get; set; }
    public int CellY { get; set; }
    public string BiomeId { get; set; } = string.Empty;
    public string BiomeName { get; set; } = string.Empty;
}

public sealed class PoliticalCellValue
{
    public int CellX { get; set; }
    public int CellY { get; set; }
    public string CountryId { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
    public string FactionId { get; set; } = string.Empty;
    public string OwnerType { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class WorldMapLegendState : EntityBase
{
    public string MapId { get; set; } = string.Empty;
    public string LayerType { get; set; } = WorldMapLayerTypeIds.Custom;
    public List<WorldMapLegendEntryState> Entries { get; set; } = new List<WorldMapLegendEntryState>();
}

public sealed class WorldMapLegendEntryState
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
}

public sealed class RoomInteriorState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string SpaceNodeId { get; set; } = string.Empty;
    public string ParentSpaceNodeId { get; set; } = string.Empty;
    public string ParentLocationId { get; set; } = string.Empty;
    public string ParentSceneMapId { get; set; } = string.Empty;
    public string ParentWorldMapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RoomType { get; set; } = RoomTypeIds.Room;
    public string InteriorType { get; set; } = InteriorTypeIds.Building;
    public double? WidthMeters { get; set; }
    public double? HeightMeters { get; set; }
    public double? AreaSquareMeters { get; set; }
    public int? FloorIndex { get; set; }
    public string BuildingId { get; set; } = string.Empty;
    public int GridCellSizeMeters { get; set; } = 2;
    public bool ShowGrid { get; set; } = true;
    public bool ShowCoordinates { get; set; } = true;
    public string LayoutMode { get; set; } = RoomLayoutModeIds.Grid;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class RoomLayoutState : EntityBase
{
    public string RoomId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public int WidthMeters { get; set; } = 20;
    public int HeightMeters { get; set; } = 20;
    public int GridCellSizeMeters { get; set; } = 2;
    public bool ShowGrid { get; set; } = true;
    public bool ShowCoordinates { get; set; } = true;
    public string LayoutMode { get; set; } = RoomLayoutModeIds.Grid;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
}

public sealed class PlayerMapFogVisibleState
{
    public int CellSizeMeters { get; set; } = 25;
    public string Mode { get; set; } = FogOfWarModeIds.Manual;
    public List<MapFogCellRange> HiddenCells { get; set; } = new List<MapFogCellRange>();
    public List<MapFogCellRange> RevealedCells { get; set; } = new List<MapFogCellRange>();
}

public sealed class PlayerMapMarker
{
    public string MarkerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
}

public sealed class PlayerMapView
{
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MapType { get; set; } = MapTypeIds.Scene;
    public int WidthMeters { get; set; } = MapRuntimeValidation.SceneDefaultSizeMeters;
    public int HeightMeters { get; set; } = MapRuntimeValidation.SceneDefaultSizeMeters;
    public int GridCellSizeMeters { get; set; } = 25;
    public bool ShowGrid { get; set; } = true;
    public bool ShowCoordinates { get; set; } = true;
    public List<PlayerMapMarker> Markers { get; set; } = new List<PlayerMapMarker>();
    public PlayerMapFogVisibleState FogOfWarVisibleState { get; set; } = new PlayerMapFogVisibleState();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AdminMapView
{
    public MapCanvasState Map { get; set; } = new MapCanvasState();
    public SceneMapSettingsState Settings { get; set; } = new SceneMapSettingsState();
    public List<MapMarkerState> Markers { get; set; } = new List<MapMarkerState>();
    public List<MapMarkerBindingState> MarkerBindings { get; set; } = new List<MapMarkerBindingState>();
    public FogOfWarState FogOfWar { get; set; } = new FogOfWarState();
    public bool HasFog { get; set; }
    public bool HasPlayerHiddenMarkers { get; set; }
    public List<string> Diagnostics { get; set; } = new List<string>();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MapSpaceNodeListRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public bool IncludeArchived { get; set; }
    public int Limit { get; set; } = 200;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSpaceNodeCreateRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public string NodeType { get; set; } = MapSpaceNodeTypeIds.Location;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Visibility { get; set; } = MapVisibilityModes.Party;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneCreateRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string SpaceNodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int WidthMeters { get; set; } = MapRuntimeValidation.SceneDefaultSizeMeters;
    public int HeightMeters { get; set; } = MapRuntimeValidation.SceneDefaultSizeMeters;
    public int GridCellSizeMeters { get; set; } = 25;
    public bool ShowGrid { get; set; } = true;
    public bool ShowCoordinates { get; set; } = true;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneListRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public bool IncludeArchived { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneGetRequest
{
    public string MapId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneUpdateSettingsRequest
{
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? WidthMeters { get; set; }
    public int? HeightMeters { get; set; }
    public int? GridCellSizeMeters { get; set; }
    public bool? ShowGrid { get; set; }
    public bool? ShowCoordinates { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneArchiveRequest
{
    public string MapId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneMarkerListRequest
{
    public string MapId { get; set; } = string.Empty;
    public bool IncludeArchived { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class PlayerSceneMapGetRequest
{
    public string MapId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string ActiveGroupId { get; set; } = string.Empty;
    public bool IncludeMarkers { get; set; } = true;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class PlayerSceneMapGetResponse
{
    public PlayerMapView Map { get; set; } = new PlayerMapView();
    public List<string> Warnings { get; set; } = new List<string>();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SceneMapActiveSetRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActiveGroupId { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public string Notes { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class SceneMapActiveGetRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActiveGroupId { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class SceneMapActiveClearRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActiveGroupId { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class SceneMapActiveResponse
{
    public bool HasActiveMap { get; set; }
    public string LinkId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActiveGroupId { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public string AssignedByUserId { get; set; } = string.Empty;
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public AdminMapView? AdminMap { get; set; }
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class PlayerSceneMapActiveGetRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActiveGroupId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class PlayerSceneMapActiveGetResponse
{
    public bool HasActiveMap { get; set; }
    public PlayerMapView Map { get; set; } = new PlayerMapView();
    public List<string> Warnings { get; set; } = new List<string>();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class WorldMapCreateRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string SpaceNodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int WidthCells { get; set; } = MapRuntimeValidation.WorldDefaultWidthCells;
    public int HeightCells { get; set; } = MapRuntimeValidation.WorldDefaultHeightCells;
    public double? CellSizeKm { get; set; }
    public string ProjectionMode { get; set; } = WorldMapProjectionModeIds.FlatGrid;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class WorldMapListRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public bool IncludeArchived { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class WorldMapGetRequest
{
    public string MapId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class WorldMapUpdateSettingsRequest
{
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? WidthCells { get; set; }
    public int? HeightCells { get; set; }
    public double? CellSizeKm { get; set; }
    public string ProjectionMode { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class WorldMapArchiveRequest
{
    public string MapId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class WorldMapLayerGetRequest
{
    public string MapId { get; set; } = string.Empty;
    public string LayerType { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class WorldMapLayerPaintRequest
{
    public string MapId { get; set; } = string.Empty;
    public string LayerType { get; set; } = WorldMapLayerTypeIds.Custom;
    public string BrushMode { get; set; } = string.Empty;
    public Dictionary<string, object> Area { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> Value { get; set; } = new Dictionary<string, object>();
    public string RequestId { get; set; } = string.Empty;
}

public sealed class WorldMapLayerUpdateCellRequest
{
    public string MapId { get; set; } = string.Empty;
    public string LayerType { get; set; } = WorldMapLayerTypeIds.Custom;
    public int CellX { get; set; }
    public int CellY { get; set; }
    public Dictionary<string, object> Value { get; set; } = new Dictionary<string, object>();
    public string RequestId { get; set; } = string.Empty;
}

public sealed class WorldMapLayerClearRequest
{
    public string MapId { get; set; } = string.Empty;
    public string LayerType { get; set; } = WorldMapLayerTypeIds.Custom;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class WorldMapLayerSetVisibilityRequest
{
    public string MapId { get; set; } = string.Empty;
    public string LayerType { get; set; } = WorldMapLayerTypeIds.Custom;
    public bool? IsVisibleToGM { get; set; }
    public bool? IsVisibleToPlayers { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class WorldMapMarkerAddRequest
{
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = MapMarkerTypeIds.Custom;
    public double? XNormalized { get; set; }
    public double? YNormalized { get; set; }
    public int? CellX { get; set; }
    public int? CellY { get; set; }
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public string LinkedEntityDisplayName { get; set; } = string.Empty;
    public string LinkedEntityPublicLabel { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class PlayerWorldMapListRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public bool IncludeMarkers { get; set; } = true;
    public string CharacterId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class PlayerWorldMapListItem
{
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SpaceNodeId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PlayerWorldMapGetRequest
{
    public string MapId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public bool IncludeMarkers { get; set; } = true;
    public bool IncludeLayers { get; set; } = true;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class PlayerWorldMapLayer
{
    public string LayerType { get; set; } = WorldMapLayerTypeIds.Custom;
    public string Name { get; set; } = string.Empty;
    public bool IsVisibleToPlayers { get; set; } = true;
    public double Opacity { get; set; } = 1d;
    public string DataEncoding { get; set; } = WorldMapDataEncodingIds.SparseCells;
    public List<Dictionary<string, object>> Cells { get; set; } = new List<Dictionary<string, object>>();
    public Dictionary<string, object> Legend { get; set; } = new Dictionary<string, object>();
}

public sealed class PlayerWorldMapLegend
{
    public string LayerType { get; set; } = WorldMapLayerTypeIds.Custom;
    public List<Dictionary<string, object>> Entries { get; set; } = new List<Dictionary<string, object>>();
}

public sealed class PlayerWorldMapMarker
{
    public string MarkerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = MapMarkerTypeIds.Custom;
    public double? XNormalized { get; set; }
    public double? YNormalized { get; set; }
    public int? CellX { get; set; }
    public int? CellY { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityDisplayName { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
}

public sealed class PlayerWorldMapView
{
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProjectionMode { get; set; } = WorldMapProjectionModeIds.FlatGrid;
    public int WidthCells { get; set; } = MapRuntimeValidation.WorldDefaultWidthCells;
    public int HeightCells { get; set; } = MapRuntimeValidation.WorldDefaultHeightCells;
    public double? CellSizeKm { get; set; }
    public List<PlayerWorldMapLayer> Layers { get; set; } = new List<PlayerWorldMapLayer>();
    public List<PlayerWorldMapMarker> Markers { get; set; } = new List<PlayerWorldMapMarker>();
    public List<PlayerWorldMapLegend> Legends { get; set; } = new List<PlayerWorldMapLegend>();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class RoomCreateRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string ParentSpaceNodeId { get; set; } = string.Empty;
    public string ParentLocationId { get; set; } = string.Empty;
    public string ParentSceneMapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RoomType { get; set; } = RoomTypeIds.Room;
    public string InteriorType { get; set; } = InteriorTypeIds.Building;
    public double? WidthMeters { get; set; }
    public double? HeightMeters { get; set; }
    public int? GridCellSizeMeters { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class RoomUpdateRequest
{
    public string RoomId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string InteriorType { get; set; } = string.Empty;
    public double? WidthMeters { get; set; }
    public double? HeightMeters { get; set; }
    public int? GridCellSizeMeters { get; set; }
    public bool? IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class RoomGetRequest
{
    public string RoomId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class RoomListRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string ParentLocationId { get; set; } = string.Empty;
    public string ParentSceneMapId { get; set; } = string.Empty;
    public bool IncludeArchived { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class RoomArchiveRequest
{
    public string RoomId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class RoomMarkerAddRequest
{
    public string RoomId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = MapMarkerTypeIds.Custom;
    public double X { get; set; }
    public double Y { get; set; }
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class PlayerRoomGetRequest
{
    public string RoomId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class PlayerRoomListRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string ParentLocationId { get; set; } = string.Empty;
    public string ParentSceneMapId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class PlayerRoomMarkerView
{
    public string MarkerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = MapMarkerTypeIds.Custom;
    public double X { get; set; }
    public double Y { get; set; }
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
}

public sealed class PlayerRoomView
{
    public string RoomId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RoomType { get; set; } = RoomTypeIds.Room;
    public string InteriorType { get; set; } = InteriorTypeIds.Building;
    public double WidthMeters { get; set; }
    public double HeightMeters { get; set; }
    public int GridCellSizeMeters { get; set; } = 2;
    public string PublicNotes { get; set; } = string.Empty;
    public List<PlayerRoomMarkerView> Markers { get; set; } = new List<PlayerRoomMarkerView>();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class RoomGeneratorRequestDraft
{
    public string CampaignId { get; set; } = string.Empty;
    public string ParentLocationId { get; set; } = string.Empty;
    public string ParentSceneMapId { get; set; } = string.Empty;
    public string BuildingType { get; set; } = string.Empty;
    public string RoomPurpose { get; set; } = string.Empty;
    public double? DesiredAreaSquareMeters { get; set; }
    public int? Occupancy { get; set; }
    public string TechLevel { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
}

public sealed class RoomGeneratorResultDraft
{
    public List<RoomInteriorState> Rooms { get; set; } = new List<RoomInteriorState>();
    public List<MapMarkerBindingState> Connections { get; set; } = new List<MapMarkerBindingState>();
    public List<MapMarkerState> Markers { get; set; } = new List<MapMarkerState>();
    public List<string> Warnings { get; set; } = new List<string>();
    public string GeneratorVersion { get; set; } = "draft-v1";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class FogBrushModeIds
{
    public const string Reveal = "reveal";
    public const string Hide = "hide";
    public const string GmOnly = "gm_only";
}

public static class FogShapeIds
{
    public const string Rectangle = "rectangle";
    public const string Circle = "circle";
    public const string Cell = "cell";
}

public static class FogClearModeIds
{
    public const string RevealAll = "reveal_all";
    public const string HideAll = "hide_all";
    public const string ClearCustom = "clear_custom";
}

public sealed class MapSceneFogGetRequest
{
    public string MapId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneFogSetModeRequest
{
    public string MapId { get; set; } = string.Empty;
    public string Mode { get; set; } = FogOfWarModeIds.Manual;
    public int? CellSizeMeters { get; set; }
    public string DefaultState { get; set; } = FogDefaultStateIds.Revealed;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneFogPaintRequest
{
    public string MapId { get; set; } = string.Empty;
    public string BrushMode { get; set; } = FogBrushModeIds.Reveal;
    public string Shape { get; set; } = FogShapeIds.Rectangle;
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double? WidthMeters { get; set; }
    public double? HeightMeters { get; set; }
    public double? RadiusMeters { get; set; }
    public int? CellSizeMeters { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneFogRevealRequest
{
    public string MapId { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double WidthMeters { get; set; }
    public double HeightMeters { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneFogHideRequest
{
    public string MapId { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double WidthMeters { get; set; }
    public double HeightMeters { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneFogClearRequest
{
    public string MapId { get; set; } = string.Empty;
    public string ClearMode { get; set; } = FogClearModeIds.ClearCustom;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneFogFillRequest
{
    public string MapId { get; set; } = string.Empty;
    public string State { get; set; } = FogDefaultStateIds.Revealed;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapSceneFogResponse
{
    public string MapId { get; set; } = string.Empty;
    public FogOfWarState Fog { get; set; } = new FogOfWarState();
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public long Revision { get; set; }
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class MapMarkerAddRequest
{
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = MapMarkerTypeIds.Custom;
    public double X { get; set; }
    public double Y { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapMarkerMoveRequest
{
    public string MarkerId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapMarkerUpdateRequest
{
    public string MarkerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarkerType { get; set; } = string.Empty;
    public double? X { get; set; }
    public double? Y { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public bool? IsPlayerVisible { get; set; }
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class MapMarkerRemoveRequest
{
    public string MarkerId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public static class MapRuntimeValidation
{
    public const int SceneMinSizeMeters = 250;
    public const int SceneDefaultSizeMeters = 2000;
    public const int SceneMaxSizeMeters = 4000;
    public const int WorldMinCells = 16;
    public const int WorldDefaultWidthCells = 200;
    public const int WorldDefaultHeightCells = 120;
    public const int WorldMaxCellsPerAxis = 2048;
    public const int WorldMaxTotalCells = 400000;
    public const int RoomDefaultSizeMeters = 20;
    public const int RoomMaxSizeMeters = 500;

    public static int ClampSceneSize(int meters)
    {
        if (meters < SceneMinSizeMeters) return SceneMinSizeMeters;
        if (meters > SceneMaxSizeMeters) return SceneMaxSizeMeters;
        return meters;
    }

    public static IReadOnlyCollection<string> ValidateSceneDimensions(int widthMeters, int heightMeters)
    {
        var errors = new List<string>();
        if (widthMeters < SceneMinSizeMeters || widthMeters > SceneMaxSizeMeters)
            errors.Add($"scene width must be between {SceneMinSizeMeters} and {SceneMaxSizeMeters} meters");
        if (heightMeters < SceneMinSizeMeters || heightMeters > SceneMaxSizeMeters)
            errors.Add($"scene height must be between {SceneMinSizeMeters} and {SceneMaxSizeMeters} meters");
        return errors;
    }

    public static bool IsValidGridCellSize(int gridCellSizeMeters)
    {
        return gridCellSizeMeters >= 1 && gridCellSizeMeters <= 500;
    }

    public static bool IsMarkerInsideBounds(MapMarkerState marker, MapCanvasState map)
    {
        if (marker == null || map == null) return false;
        return marker.X >= 0
            && marker.Y >= 0
            && marker.X <= map.WidthMeters
            && marker.Y <= map.HeightMeters;
    }

    public static bool IsAllowedNodeType(string nodeType)
    {
        var value = (nodeType ?? string.Empty).Trim();
        if (value.Length == 0) return false;

        return AllowedNodeTypes.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsAllowedMapType(string mapType)
    {
        var value = (mapType ?? string.Empty).Trim();
        if (value.Length == 0) return false;

        return AllowedMapTypes.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyCollection<string> ValidateWorldDimensions(int widthCells, int heightCells)
    {
        var errors = new List<string>();
        if (widthCells <= 0 || heightCells <= 0)
            errors.Add("world map width/height must be > 0");
        if (widthCells < WorldMinCells || heightCells < WorldMinCells)
            errors.Add($"world map width/height must be >= {WorldMinCells} cells");
        if (widthCells > WorldMaxCellsPerAxis || heightCells > WorldMaxCellsPerAxis)
            errors.Add($"world map width/height must be <= {WorldMaxCellsPerAxis} cells");

        var totalCells = (long)widthCells * (long)heightCells;
        if (totalCells > WorldMaxTotalCells)
            errors.Add($"world map total cells must be <= {WorldMaxTotalCells}");

        return errors;
    }

    public static bool IsNormalizedCoordinate(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d && value <= 1d;
    }

    public static bool IsWorldCellInsideBounds(int cellX, int cellY, int widthCells, int heightCells)
    {
        return cellX >= 0 && cellY >= 0 && cellX < widthCells && cellY < heightCells;
    }

    public static IReadOnlyCollection<string> ValidateRoomDimensions(double? widthMeters, double? heightMeters, int? gridCellSizeMeters = null)
    {
        var errors = new List<string>();
        if (widthMeters.HasValue && (widthMeters.Value <= 0 || widthMeters.Value > RoomMaxSizeMeters))
            errors.Add($"room width must be > 0 and <= {RoomMaxSizeMeters} meters");
        if (heightMeters.HasValue && (heightMeters.Value <= 0 || heightMeters.Value > RoomMaxSizeMeters))
            errors.Add($"room height must be > 0 and <= {RoomMaxSizeMeters} meters");
        if (gridCellSizeMeters.HasValue && !IsValidGridCellSize(gridCellSizeMeters.Value))
            errors.Add("gridCellSizeMeters must be between 1 and 500");
        return errors;
    }

    public static bool IsRoomMarkerInsideBounds(double x, double y, RoomInteriorState room)
    {
        if (room == null) return false;
        if (!room.WidthMeters.HasValue || !room.HeightMeters.HasValue) return true;
        return x >= 0 && y >= 0 && x <= room.WidthMeters.Value && y <= room.HeightMeters.Value;
    }

    private static readonly string[] AllowedNodeTypes =
    {
        MapSpaceNodeTypeIds.Dimension,
        MapSpaceNodeTypeIds.World,
        MapSpaceNodeTypeIds.StarSystem,
        MapSpaceNodeTypeIds.Star,
        MapSpaceNodeTypeIds.Planet,
        MapSpaceNodeTypeIds.WorldMap,
        MapSpaceNodeTypeIds.SceneMap,
        MapSpaceNodeTypeIds.Room,
        MapSpaceNodeTypeIds.Location,
        MapSpaceNodeTypeIds.Region,
        MapSpaceNodeTypeIds.Country,
        MapSpaceNodeTypeIds.City,
        MapSpaceNodeTypeIds.Interior,
        MapSpaceNodeTypeIds.Custom
    };

    private static readonly string[] AllowedMapTypes =
    {
        MapTypeIds.World,
        MapTypeIds.WorldMap,
        MapTypeIds.Scene,
        MapTypeIds.Room,
        MapTypeIds.Interior,
        MapTypeIds.StarSystem,
        MapTypeIds.Planet,
        MapTypeIds.PlanetMap,
        MapTypeIds.Custom
    };
}
