using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope WorldAdminMapGeneratePreview0218(CommandContext context)
        => GeneratePreview0218(context, false, false);

    public ResponseEnvelope WorldAdminMapGenerateRegeneratePreview0218(CommandContext context)
        => GeneratePreview0218(context, true, false);

    public ResponseEnvelope WorldAdminMapGeneratePartialPreview0218(CommandContext context)
        => GeneratePreview0218(context, true, true);

    public ResponseEnvelope WorldAdminMapGenerateValidate0218(CommandContext context)
    {
        RequireAdmin(context);
        var job = RequireGenerationJob0218(PayloadReader.GetString(context.Request.Payload, "jobId"));
        var findings = new List<string>();
        if (job.PreviewFeatures.Count == 0) findings.Add("Предпросмотр не содержит объектов.");
        if (job.PreviewFeatures.Select(item => item.GenerationIdentity).Distinct(StringComparer.Ordinal).Count() != job.PreviewFeatures.Count)
            findings.Add("Нарушена воспроизводимость семантического результата.");
        if (job.OutputSemanticHash != StableMapPrng0218.SemanticHash(job.PreviewFeatures)) findings.Add("Семантический hash не совпадает.");
        return Ok(findings.Count == 0 ? "Предпросмотр прошёл проверку." : "Предпросмотр содержит ошибки.", new Dictionary<string, object>
        {
            { "valid", findings.Count == 0 }, { "findings", findings.Cast<object>().ToArray() }, { "semanticHash", job.OutputSemanticHash }
        });
    }

    public ResponseEnvelope WorldAdminMapGenerateAccept0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var job = RequireGenerationJob0218(PayloadReader.GetString(payload, "jobId"));
        if (job.Status != "preview") throw new InvalidOperationException("Этот предпросмотр уже принят или закрыт.");
        var current = _mongo.MapSemanticFeatures0218.Find(x => x.MapId == job.MapId && !x.IsArchived).ToList();
        var manualRetained = current.Count(item => item.IsManual);
        var generatedModifiedRetained = 0;
        foreach (var preview in job.PreviewFeatures)
        {
            var existing = current.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.GenerationIdentity) && item.GenerationIdentity == preview.GenerationIdentity);
            if (existing != null && existing.IsManual)
            {
                generatedModifiedRetained++;
                continue;
            }
            preview.Id = existing?.Id ?? preview.Id;
            preview.Revision = (existing?.Revision ?? 0) + 1;
            preview.UpdatedAtUtc = DateTime.UtcNow;
            _mongo.MapSemanticFeatures0218.ReplaceOne(x => x.Id == preview.Id, preview, new ReplaceOptions { IsUpsert = true });
        }
        var acceptedIdentities = new HashSet<string>(
            job.PreviewFeatures.Select(item => item.GenerationIdentity).Where(value => !string.IsNullOrWhiteSpace(value)),
            StringComparer.Ordinal);
        foreach (var obsolete in current.Where(item =>
                     !item.IsManual &&
                     !string.IsNullOrWhiteSpace(item.GenerationIdentity) &&
                     !acceptedIdentities.Contains(item.GenerationIdentity)))
        {
            obsolete.IsArchived = true;
            obsolete.Revision++;
            obsolete.UpdatedAtUtc = DateTime.UtcNow;
            _mongo.MapSemanticFeatures0218.ReplaceOne(x => x.Id == obsolete.Id, obsolete);
        }
        job.Status = "accepted";
        job.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.MapGenerationJobs0218.ReplaceOne(x => x.Id == job.Id, job);
        var map = RequireMap0218(job.MapId);
        map.GeneratorProvenanceId = job.Id;
        map.EntityRevision++;
        map.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.MapCanvases.ReplaceOne(x => x.Id == map.Id, map);
        WriteAudit("map_generation", job.Id, "accept", actor.Id);
        return Ok("Сгенерированные объекты приняты.", new Dictionary<string, object>
        {
            { "acceptedCount", job.PreviewFeatures.Count }, { "manualFeatureCountRetained", manualRetained },
            { "modifiedGeneratedRetained", generatedModifiedRetained }, { "semanticHash", job.OutputSemanticHash }
        });
    }

    public ResponseEnvelope WorldAdminMapExport0218(CommandContext context)
    {
        RequireAdmin(context);
        var map = RequireMap0218(PayloadReader.GetString(context.Request.Payload, "mapId"));
        var package = BuildPackage0218(map);
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
        var json = serializer.Serialize(package);
        var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports", "maps");
        Directory.CreateDirectory(directory);
        var fileName = SanitizeFileName0218(map.Name) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".nrimap";
        var path = Path.Combine(directory, fileName);
        using (var file = File.Create(path))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("package.json", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write(json);
        }
        var features = _mongo.MapSemanticFeatures0218.Find(x => x.MapId == map.Id && !x.IsArchived).ToList();
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var pngPath = Path.Combine(directory, baseName + ".png");
        var svgPath = Path.Combine(directory, baseName + ".svg");
        WritePngSnapshot0218(map, features, pngPath);
        WriteSemanticSvg0218(map, features, svgPath);
        return Ok("Пакет карты экспортирован.", new Dictionary<string, object>
        {
            { "fileName", fileName }, { "path", path }, { "format", "nrimap" }, { "mapName", map.Name },
            { "pngFileName", Path.GetFileName(pngPath) }, { "pngPath", pngPath },
            { "svgFileName", Path.GetFileName(svgPath) }, { "svgPath", svgPath }, { "svgSemantic", true },
            { "featureCount", ((object[])package["features"]).Length }, { "containsRawServerData", false }
        });
    }

    private static void WritePngSnapshot0218(MapCanvasState map, IList<MapSemanticFeatureState0218> features, string path)
    {
        const int width = 1200;
        const int height = 760;
        using (var bitmap = new System.Drawing.Bitmap(width, height))
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        using (var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(35, 67, 92), 1f))
        using (var labelBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 235, 245)))
        using (var font = new System.Drawing.Font("Segoe UI", 12f))
        using (var titleFont = new System.Drawing.Font("Segoe UI", 20f, System.Drawing.FontStyle.Bold))
        {
            graphics.Clear(System.Drawing.Color.FromArgb(12, 25, 42));
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            for (var x = 0; x < width; x += 60) graphics.DrawLine(gridPen, x, 0, x, height);
            for (var y = 0; y < height; y += 60) graphics.DrawLine(gridPen, 0, y, width, y);
            graphics.DrawString(map.Name, titleFont, labelBrush, 24, 18);
            foreach (var feature in features.Take(MaxActivePrimitives0218)) DrawSnapshotFeature0218(graphics, feature, width, height, labelBrush, font);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private static void DrawSnapshotFeature0218(System.Drawing.Graphics graphics, MapSemanticFeatureState0218 feature, int width, int height, System.Drawing.Brush labelBrush, System.Drawing.Font font)
    {
        if (feature.Points.Count == 0) return;
        var points = feature.Points.Select(point => new System.Drawing.PointF((float)(point.X / 100d * width), (float)(point.Y / 100d * height))).ToArray();
        var color = feature.SemanticKind == MapSemanticKindIds0218.River ? System.Drawing.Color.FromArgb(65, 145, 230)
            : feature.SemanticKind == MapSemanticKindIds0218.Road ? System.Drawing.Color.FromArgb(220, 170, 75)
            : feature.IsSecret ? System.Drawing.Color.FromArgb(150, 95, 205) : System.Drawing.Color.FromArgb(65, 185, 125);
        using (var pen = new System.Drawing.Pen(color, 4f))
        using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(120, color)))
        {
            if (feature.GeometryKind == MapGeometryKindIds0218.Polygon && points.Length >= 3) { graphics.FillPolygon(brush, points); graphics.DrawPolygon(pen, points); }
            else if (feature.GeometryKind == MapGeometryKindIds0218.Polyline && points.Length >= 2) graphics.DrawLines(pen, points);
            else graphics.FillEllipse(brush, points[0].X - 8, points[0].Y - 8, 16, 16);
            graphics.DrawString(feature.Name, font, labelBrush, points[0].X + 10, points[0].Y + 4);
        }
    }

    private static void WriteSemanticSvg0218(MapCanvasState map, IList<MapSemanticFeatureState0218> features, string path)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1200 760\">");
        builder.AppendLine("<rect width=\"1200\" height=\"760\" fill=\"#0c192a\"/>");
        builder.Append("<title>").Append(System.Security.SecurityElement.Escape(map.Name)).AppendLine("</title>");
        foreach (var feature in features.Take(MaxActivePrimitives0218))
        {
            if (feature.Points.Count == 0) continue;
            var points = string.Join(" ", feature.Points.Select(point => string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", point.X * 12d, point.Y * 7.6d)));
            var color = feature.SemanticKind == MapSemanticKindIds0218.River ? "#4191e6" : feature.SemanticKind == MapSemanticKindIds0218.Road ? "#dcaa4b" : feature.IsSecret ? "#965fcd" : "#41b97d";
            if (feature.GeometryKind == MapGeometryKindIds0218.Polygon && feature.Points.Count >= 3) builder.Append("<polygon points=\"").Append(points).Append("\" fill=\"").Append(color).Append("\" fill-opacity=\"0.35\" stroke=\"").Append(color).AppendLine("\"/>");
            else if (feature.GeometryKind == MapGeometryKindIds0218.Polyline && feature.Points.Count >= 2) builder.Append("<polyline points=\"").Append(points).Append("\" fill=\"none\" stroke=\"").Append(color).AppendLine("\" stroke-width=\"4\"/>");
            else builder.AppendFormat(CultureInfo.InvariantCulture, "<circle cx=\"{0:0.##}\" cy=\"{1:0.##}\" r=\"8\" fill=\"{2}\"/>\n", feature.Points[0].X * 12d, feature.Points[0].Y * 7.6d, color);
        }
        builder.AppendLine("</svg>");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    public ResponseEnvelope WorldAdminMapImportDryRun0218(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var path = ValidateImportPath0218(PayloadReader.GetString(payload, "path"));
        var package = ReadPackage0218(path);
        var validation = ValidatePackage0218(package);
        return Ok(validation.Count == 0 ? "Пакет карты в порядке." : "Пакет не прошёл проверку.", new Dictionary<string, object>
        {
            { "dryRun", true }, { "liveDatabaseWrites", 0 }, { "valid", validation.Count == 0 },
            { "findings", validation.Cast<object>().ToArray() }, { "plannedCollections", new object[] { "map_states", "map_semantic_layers", "map_semantic_features", "map_portals" } },
            { "sourcePackage", path }
        });
    }

    public ResponseEnvelope WorldAdminMapImportApply0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (!PayloadReader.GetBool(payload, "confirmApply")) throw new ArgumentException("Запись импорта требует явного подтверждения.");
        var path = ValidateImportPath0218(PayloadReader.GetString(payload, "path"));
        var package = ReadPackage0218(path);
        var validation = ValidatePackage0218(package);
        if (validation.Count > 0) throw new ArgumentException("Пакет не прошёл dry-run: " + string.Join("; ", validation));
        var mapDictionary = PackageDictionary0218(package, "map");
        var map = PackageMap0218(mapDictionary);
        map.Id = Guid.NewGuid().ToString("N");
        map.Name += " (импорт)";
        map.EntityRevision = 1;
        map.CreatedAtUtc = DateTime.UtcNow;
        map.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.MapCanvases.InsertOne(map);
        var count = 0;
        foreach (var raw in PackageList0218(package, "features"))
        {
            var feature = PackageFeature0218(raw);
            feature.Id = Guid.NewGuid().ToString("N");
            feature.MapId = map.Id;
            feature.Revision = 1;
            feature.UpdatedAtUtc = DateTime.UtcNow;
            feature.GMNotes = string.Empty;
            feature.ServerOnlyData.Clear();
            _mongo.MapSemanticFeatures0218.InsertOne(feature);
            count++;
        }
        WriteAudit("world_map", map.Id, "import_0218", actor.Id);
        return Ok("Пакет импортирован.", new Dictionary<string, object> { { "mapId", map.Id }, { "featureCount", count } });
    }

    private ResponseEnvelope GeneratePreview0218(CommandContext context, bool regeneration, bool partial)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var map = RequireMap0218(PayloadReader.GetString(payload, "mapId"));
        var scope = First0218(PayloadReader.GetString(payload, "scope"), DefaultGeneratorScope0218(map.MapType));
        if (partial && scope != MapGenerationScopeIds0218.Settlement && scope != "district")
            throw new ArgumentException("Генератор 0.21.8 поддерживает регион, поселение, подземелье, сектор, систему и планету.");
        var seed = First0218(PayloadReader.GetString(payload, "seed"), map.Id + "|0218");
        var features = GenerateSemanticFeatures0218(map, scope, seed);
        var job = new MapGenerationJobState0218
        {
            CampaignId = map.CampaignId,
            MapId = map.Id,
            RecipeDefinitionId = First0218(PayloadReader.GetString(payload, "recipeDefinitionId"), "recipe_" + scope + "_0218"),
            RecipeVersion = 1,
            GeneratorKind = scope,
            GeneratorAlgorithmId = "nri_semantic_map_sha256_counter",
            GeneratorAlgorithmVersion = 1,
            Seed = seed,
            Scope = scope,
            InputConstraints = new Dictionary<string, object> { { "mapType", map.MapType }, { "partial", partial } },
            PreviewFeatures = features,
            OutputSemanticHash = StableMapPrng0218.SemanticHash(features),
            Status = "preview",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actor.Id
        };
        _mongo.MapGenerationJobs0218.InsertOne(job);
        var existing = _mongo.MapSemanticFeatures0218.Find(x => x.MapId == map.Id && !x.IsArchived).ToList();
        var generatedIds = new HashSet<string>(features.Select(item => item.GenerationIdentity), StringComparer.Ordinal);
        var currentGenerated = existing.Where(item => !item.IsManual && !string.IsNullOrWhiteSpace(item.GenerationIdentity)).ToList();
        var added = features.Count(item => currentGenerated.All(old => old.GenerationIdentity != item.GenerationIdentity));
        var removed = currentGenerated.Count(item => !generatedIds.Contains(item.GenerationIdentity));
        var changed = features.Count(item => currentGenerated.Any(old => old.GenerationIdentity == item.GenerationIdentity
            && StableMapPrng0218.SemanticHash(new[] { old }) != StableMapPrng0218.SemanticHash(new[] { item })));
        var unchanged = features.Count - added - changed;
        var modifiedConflict = existing.Count(item => item.IsManual && !string.IsNullOrWhiteSpace(item.GenerationIdentity)
            && generatedIds.Contains(item.GenerationIdentity));
        return Ok(regeneration ? "Предпросмотр повторной генерации построен." : "Предпросмотр генерации построен.", new Dictionary<string, object>
        {
            { "jobId", job.Id }, { "seed", seed }, { "scope", scope }, { "semanticHash", job.OutputSemanticHash },
            { "features", features.Select(AdminFeaturePayload0218).Cast<object>().ToArray() },
            { "diff", new Dictionary<string, object> { { "added", added }, { "changed", changed }, { "removedGenerated", removed }, { "unchanged", unchanged },
                { "manualRetained", existing.Count(item => item.IsManual) }, { "modifiedGeneratedConflict", modifiedConflict } } },
            { "requiresExplicitAccept", true }, { "mutatedMap", false }
        });
    }

    private List<MapSemanticFeatureState0218> GenerateSemanticFeatures0218(MapCanvasState map, string scope, string seed)
    {
        if (scope == MapGenerationScopeIds0218.Settlement || scope == "district") return GenerateSettlementFeatures0218(map, seed);
        if (scope == MapGenerationScopeIds0218.Dungeon) return GenerateDungeonFeatures0218(map, seed);
        var count = scope == MapGenerationScopeIds0218.Dungeon ? 18 : scope == MapGenerationScopeIds0218.Sector ? 14 : 12;
        var kinds = GeneratorKinds0218(scope);
        var features = new List<MapSemanticFeatureState0218>();
        for (var index = 0; index < count; index++)
        {
            var kind = kinds[index % kinds.Length];
            var x = 8d + StableMapPrng0218.Unit(seed, scope + "_x", index) * 84d;
            var y = 8d + StableMapPrng0218.Unit(seed, scope + "_y", index) * 84d;
            var geometry = kind == MapSemanticKindIds0218.Road || kind == MapSemanticKindIds0218.River || kind == MapSemanticKindIds0218.Border
                ? MapGeometryKindIds0218.Polyline
                : kind == MapSemanticKindIds0218.Area || kind == MapSemanticKindIds0218.District || kind == MapSemanticKindIds0218.Room
                    ? MapGeometryKindIds0218.Polygon
                    : MapGeometryKindIds0218.Point;
            var identity = string.Join("_", "generated", scope, kind, index.ToString("D2"));
            var points = geometry == MapGeometryKindIds0218.Point
                ? new List<MapPoint0218> { new MapPoint0218 { X = x, Y = y } }
                : geometry == MapGeometryKindIds0218.Polyline
                    ? new List<MapPoint0218> { new MapPoint0218 { X = x, Y = y }, new MapPoint0218 { X = Math.Min(98, x + 12), Y = Math.Min(98, y + 8) } }
                    : new List<MapPoint0218> { new MapPoint0218 { X = x, Y = y }, new MapPoint0218 { X = Math.Min(98, x + 8), Y = y }, new MapPoint0218 { X = Math.Min(98, x + 8), Y = Math.Min(98, y + 7) }, new MapPoint0218 { X = x, Y = Math.Min(98, y + 7) } };
            features.Add(new MapSemanticFeatureState0218
            {
                Id = StableId0218(map.Id, seed, identity), CampaignId = map.CampaignId, MapId = map.Id,
                LayerId = map.Id + "_generated", Name = GeneratorName0218(scope, kind, index + 1), SemanticKind = kind,
                GeometryKind = geometry, Points = points, IsPlayerVisible = index % 5 != 0, IsSecret = scope == MapGenerationScopeIds0218.Dungeon && index % 6 == 0,
                IsManual = false, GenerationIdentity = identity, GeneratorProvenanceId = seed, PublicDescription = "Сгенерированный объект местности.",
                StyleKey = kind, Revision = 1, UpdatedAtUtc = DateTime.UtcNow
            });
        }
        return features;
    }

    private static List<MapSemanticFeatureState0218> GenerateSettlementFeatures0218(MapCanvasState map, string seed)
    {
        var result = new List<MapSemanticFeatureState0218>();
        result.Add(GeneratedFeature0218(map, seed, "settlement_boundary", "Граница Грейхейвена", MapSemanticKindIds0218.Area, MapGeometryKindIds0218.Polygon,
            P0218(7, 8), P0218(93, 8), P0218(93, 91), P0218(7, 91)));
        var districts = new[]
        {
            ("old_quarter", "Старый квартал", 12d, 14d, 38d, 42d),
            ("river_quarter", "Речной квартал", 43d, 14d, 82d, 43d),
            ("citadel_quarter", "Крепостной квартал", 26d, 50d, 72d, 84d)
        };
        foreach (var item in districts)
            result.Add(GeneratedFeature0218(map, seed, "district_" + item.Item1, item.Item2, MapSemanticKindIds0218.District, MapGeometryKindIds0218.Polygon,
                P0218(item.Item3, item.Item4), P0218(item.Item5, item.Item4), P0218(item.Item5, item.Item6), P0218(item.Item3, item.Item6)));
        result.Add(GeneratedFeature0218(map, seed, "road_north_south", "Северный тракт", MapSemanticKindIds0218.Road, MapGeometryKindIds0218.Polyline, P0218(50, 5), P0218(50, 94)));
        result.Add(GeneratedFeature0218(map, seed, "road_market", "Рыночная дорога", MapSemanticKindIds0218.Road, MapGeometryKindIds0218.Polyline, P0218(8, 46), P0218(92, 46)));
        var structures = new[]
        {
            ("guild", "Дом гильдии", 16d, 20d), ("archive", "Городской архив", 28d, 20d),
            ("market", "Крытый рынок", 48d, 20d), ("inn", "Постоялый двор", 64d, 20d),
            ("watch", "Караульный дом", 34d, 57d), ("temple", "Храм пути", 49d, 57d),
            ("workshop", "Мастерские", 62d, 57d), ("warehouse", "Речной склад", 74d, 57d)
        };
        foreach (var item in structures)
            result.Add(GeneratedFeature0218(map, seed, "structure_" + item.Item1, item.Item2, MapSemanticKindIds0218.Structure, MapGeometryKindIds0218.Polygon,
                P0218(item.Item3, item.Item4), P0218(item.Item3 + 8, item.Item4), P0218(item.Item3 + 8, item.Item4 + 7), P0218(item.Item3, item.Item4 + 7)));
        result.Add(GeneratedFeature0218(map, seed, "poi_west_gate", "Западные ворота", MapSemanticKindIds0218.Entrance, MapGeometryKindIds0218.Point, P0218(7, 46)));
        result.Add(GeneratedFeature0218(map, seed, "poi_square", "Площадь картографов", MapSemanticKindIds0218.PointOfInterest, MapGeometryKindIds0218.Point, P0218(50, 46)));
        result.Add(GeneratedFeature0218(map, seed, "poi_dock", "Речная пристань", MapSemanticKindIds0218.PointOfInterest, MapGeometryKindIds0218.Point, P0218(91, 38)));
        foreach (var item in districts)
            result.Add(GeneratedFeature0218(map, seed, "label_" + item.Item1, item.Item2, MapSemanticKindIds0218.Label, MapGeometryKindIds0218.Point,
                P0218((item.Item3 + item.Item5) / 2d, (item.Item4 + item.Item6) / 2d)));
        return result;
    }

    private static List<MapSemanticFeatureState0218> GenerateDungeonFeatures0218(MapCanvasState map, string seed)
    {
        var result = new List<MapSemanticFeatureState0218>();
        var rooms = new[]
        {
            ("entrance_hall", "Входной зал", "вестибюль", 8d, 42d),
            ("catalogue", "Зал каталогов", "архив", 22d, 20d),
            ("scribes", "Комната писцов", "служебная", 42d, 18d),
            ("maps", "Хранилище карт", "хранилище", 62d, 18d),
            ("reading", "Читальный зал", "зал", 22d, 61d),
            ("vault", "Защищённое хранилище", "хранилище", 44d, 59d),
            ("stairs", "Зал нижней лестницы", "переход", 66d, 58d),
            ("secret", "Тайный фонд", "секрет", 82d, 34d)
        };
        foreach (var room in rooms)
        {
            var feature = GeneratedFeature0218(map, seed, "room_" + room.Item1, room.Item2, MapSemanticKindIds0218.Room, MapGeometryKindIds0218.Polygon,
                P0218(room.Item4, room.Item5), P0218(room.Item4 + 12, room.Item5), P0218(room.Item4 + 12, room.Item5 + 13), P0218(room.Item4, room.Item5 + 13));
            feature.IsSecret = room.Item1 == "secret";
            feature.IsPlayerVisible = !feature.IsSecret;
            feature.ExtraData["roomType"] = room.Item3;
            result.Add(feature);
        }
        var links = new[] { (14d,48d,28d,27d), (34d,27d,48d,25d), (54d,25d,68d,25d), (14d,48d,28d,68d), (34d,68d,50d,66d), (56d,66d,72d,65d), (74d,25d,88d,40d) };
        for (var i = 0; i < links.Length; i++)
            result.Add(GeneratedFeature0218(map, seed, "corridor_" + (i + 1), "Коридор " + (i + 1), MapSemanticKindIds0218.Road, MapGeometryKindIds0218.Polyline,
                P0218(links[i].Item1, links[i].Item2), P0218(links[i].Item3, links[i].Item4)));
        result.Add(GeneratedFeature0218(map, seed, "entrance", "Вход из дома картографа", MapSemanticKindIds0218.Entrance, MapGeometryKindIds0218.Point, P0218(8, 49)));
        result.Add(GeneratedFeature0218(map, seed, "stairs", "Лестница на нижний уровень", MapSemanticKindIds0218.Stairs, MapGeometryKindIds0218.Point, P0218(72, 66)));
        return result;
    }

    private static MapSemanticFeatureState0218 GeneratedFeature0218(MapCanvasState map, string seed, string identity, string name, string kind, string geometry, params MapPoint0218[] points)
        => new MapSemanticFeatureState0218
        {
            Id = StableId0218(map.Id, seed, "generated_" + identity), CampaignId = map.CampaignId, MapId = map.Id,
            LayerId = map.Id + "_generated", Name = name, SemanticKind = kind, GeometryKind = geometry, Points = points.ToList(),
            IsPlayerVisible = true, IsManual = false, GenerationIdentity = "generated_" + identity, GeneratorProvenanceId = seed,
            PublicDescription = "Семантический объект карты.", StyleKey = kind, Revision = 1, UpdatedAtUtc = DateTime.UtcNow
        };

    private void EnsureFixture0218(string actorId)
    {
        EnsureProfiles0218();
        var fantasyNodes = new[]
        {
            Node0218("node_eldaris_0218", "", "Элдарис", MapSpaceNodeTypeIds.World, 10),
            Node0218("node_north_valley_0218", "node_eldaris_0218", "Северная долина", MapSpaceNodeTypeIds.Region, 20),
            Node0218("node_greyhaven_0218", "node_north_valley_0218", "Грейхейвен", MapSpaceNodeTypeIds.Settlement, 30),
            Node0218("node_old_quarter_0218", "node_greyhaven_0218", "Старый квартал", MapSpaceNodeTypeIds.District, 40),
            Node0218("node_cartographer_house_0218", "node_old_quarter_0218", "Дом картографа", MapSpaceNodeTypeIds.Interior, 50),
            Node0218("node_underground_archive_0218", "node_cartographer_house_0218", "Подземный архив", MapSpaceNodeTypeIds.Dungeon, 60)
        };
        var sciNodes = new[]
        {
            Node0218("node_orion_arc_0218", "", "Орионская дуга", MapSpaceNodeTypeIds.Galaxy, 110),
            Node0218("node_sector_k12_0218", "node_orion_arc_0218", "Сектор K-12", MapSpaceNodeTypeIds.Sector, 120),
            Node0218("node_helios_system_0218", "node_sector_k12_0218", "Система Гелиос", MapSpaceNodeTypeIds.StarSystem, 130),
            Node0218("node_asterion_0218", "node_helios_system_0218", "Астерион", MapSpaceNodeTypeIds.Planet, 140),
            Node0218("node_beacon_station_0218", "node_asterion_0218", "Станция Маяк", MapSpaceNodeTypeIds.Orbital, 150)
        };
        foreach (var node in fantasyNodes.Concat(sciNodes))
            _mongo.MapSpaceNodes.ReplaceOne(x => x.Id == node.Id, node, new ReplaceOptions { IsUpsert = true });

        var maps = new[]
        {
            Map0218("map_eldaris_0218", "", "node_eldaris_0218", "Элдарис", MapTypeIds.World, "coord_geo_0218", "scale_geo_0218", 40000000, 20000000),
            Map0218("map_north_valley_0218", "map_eldaris_0218", "node_north_valley_0218", "Северная долина", MapTypeIds.Region, "coord_local_0218", "scale_region_0218", 240000, 160000),
            Map0218("map_greyhaven_0218", "map_north_valley_0218", "node_greyhaven_0218", "Грейхейвен", MapTypeIds.Settlement, "coord_local_0218", "scale_local_0218", 12000, 9000),
            Map0218("map_old_quarter_0218", "map_greyhaven_0218", "node_old_quarter_0218", "Старый квартал", MapTypeIds.District, "coord_local_0218", "scale_local_0218", 2200, 1800),
            Map0218("map_cartographer_house_0218", "map_old_quarter_0218", "node_cartographer_house_0218", "Дом картографа", MapTypeIds.Interior, "coord_local_0218", "scale_room_0218", 60, 45),
            Map0218("map_underground_archive_0218", "map_cartographer_house_0218", "node_underground_archive_0218", "Подземный архив", MapTypeIds.Dungeon, "coord_grid_0218", "scale_grid_0218", 120, 80),
            Map0218("map_orion_arc_0218", "", "node_orion_arc_0218", "Орионская дуга", MapTypeIds.Galaxy, "coord_schematic_0218", "scale_schematic_0218", 100, 100),
            Map0218("map_sector_k12_0218", "map_orion_arc_0218", "node_sector_k12_0218", "Сектор K-12", MapTypeIds.Sector, "coord_hex_0218", "scale_sector_hex_0218", 10000000, 10000000),
            Map0218("map_helios_system_0218", "map_sector_k12_0218", "node_helios_system_0218", "Система Гелиос", MapTypeIds.StarSystem, "coord_schematic_0218", "scale_schematic_0218", 100, 100),
            Map0218("map_asterion_0218", "map_helios_system_0218", "node_asterion_0218", "Астерион", MapTypeIds.Planet, "coord_geo_0218", "scale_geo_0218", 40000000, 20000000),
            Map0218("map_beacon_station_0218", "map_asterion_0218", "node_beacon_station_0218", "Станция Маяк", MapTypeIds.Orbital, "coord_schematic_0218", "scale_schematic_0218", 100, 100)
        };
        foreach (var map in maps)
            if (!_mongo.MapCanvases.Find(x => x.Id == map.Id).Any()) _mongo.MapCanvases.InsertOne(map);
        foreach (var map in maps) EnsureLayerAndFeatures0218(map);
        for (var index = 1; index < fantasyNodes.Length - 1; index++) EnsurePortal0218(maps[index - 1], maps[index], "Перейти: " + maps[index].Name, false);
        for (var index = 7; index < maps.Length; index++) EnsurePortal0218(maps[index - 1], maps[index], "Перейти: " + maps[index].Name, false);
        EnsurePortal0218(maps[4], maps[5], "Скрытая дверь в архив", true);

        var player = _mongo.Accounts.Find(x => x.Login == "dev_player").FirstOrDefault();
        if (player != null)
        {
            foreach (var map in maps) EnsureKnowledge0218(player.Id, map.Id, map.Name, MapDiscoveryPrecisionIds0218.Exact, actorId, 0, 0);
        EnsureKnowledge0218(player.Id, "feature_north_ruins_0218", "Руины Сторожевой башни", MapDiscoveryPrecisionIds0218.Approximate, actorId, 71, 33);
            _mongo.EntityKnowledgeStates.DeleteMany(item =>
                item.OwnerUserId == player.Id &&
                (item.EntityId == "feature_archive_secret_0218" ||
                 item.EntityId == "portal_map_cartographer_house_0218_map_underground_archive_0218_secret"));
        }
    }

    private void EnsureProfiles0218()
    {
        var coordinateProfiles = new[]
        {
            Coordinate0218("coord_local_0218", "Локальные координаты", MapCoordinateProfileKindIds0218.LocalCartesian2D),
            Coordinate0218("coord_grid_0218", "Квадратная сетка", MapCoordinateProfileKindIds0218.SquareGrid),
            Coordinate0218("coord_hex_0218", "Гексагональная сетка сектора", MapCoordinateProfileKindIds0218.HexGrid),
            Coordinate0218("coord_geo_0218", "Географические координаты", MapCoordinateProfileKindIds0218.Geographic2D),
            Coordinate0218("coord_schematic_0218", "Схема узлов", MapCoordinateProfileKindIds0218.SchematicNodeSpace)
        };
        foreach (var profile in coordinateProfiles) _mongo.MapCoordinateProfiles0218.ReplaceOne(x => x.Id == profile.Id, profile, new ReplaceOptions { IsUpsert = true });
        var scales = new[]
        {
            Scale0218("scale_geo_0218", "Географический масштаб", MapScaleKindIds0218.Geographic, 1000, true, "км"),
            Scale0218("scale_region_0218", "Региональный масштаб", MapScaleKindIds0218.PhysicalLinear, 1000, true, "км"),
            Scale0218("scale_local_0218", "Локальный масштаб", MapScaleKindIds0218.PhysicalLinear, 100, true, "м"),
            Scale0218("scale_room_0218", "Масштаб помещения", MapScaleKindIds0218.PhysicalLinear, 1, true, "м"),
            Scale0218("scale_grid_0218", "Физическая сетка", MapScaleKindIds0218.GridPhysical, 1, true, "м"),
            Scale0218("scale_sector_hex_0218", "Секторная гексагональная сетка", MapScaleKindIds0218.GridPhysical, 1000000, true, "км"),
            Scale0218("scale_schematic_0218", "Схематический масштаб", MapScaleKindIds0218.Schematic, 0, false, "")
        };
        foreach (var scale in scales) _mongo.MapScaleProfiles0218.ReplaceOne(x => x.Id == scale.Id, scale, new ReplaceOptions { IsUpsert = true });
        foreach (var scope in new[] { MapGenerationScopeIds0218.Region, MapGenerationScopeIds0218.Settlement, MapGenerationScopeIds0218.Dungeon, MapGenerationScopeIds0218.Sector, MapGenerationScopeIds0218.System, MapGenerationScopeIds0218.Planet })
        {
            var recipe = new MapGeneratorRecipeDefinition0218
            {
                Id = "recipe_" + scope + "_0218", CampaignId = Campaign0218, Name = GeneratorScopeLabel0218(scope),
                GeneratorKind = scope, AlgorithmId = "nri_semantic_map", AlgorithmVersion = 1, RecipeVersion = 1,
                Constraints = new Dictionary<string, object> { { "maxFeatures", scope == MapGenerationScopeIds0218.Dungeon ? 18 : 14 }, { "semanticOutput", true } }
            };
            _mongo.MapGeneratorRecipes0218.ReplaceOne(x => x.Id == recipe.Id, recipe, new ReplaceOptions { IsUpsert = true });
        }
    }

    private void EnsureLayerAndFeatures0218(MapCanvasState map)
    {
        var layer = new MapSemanticLayerState0218 { Id = map.Id + "_geography", CampaignId = map.CampaignId, MapId = map.Id, Name = "География", LayerKind = MapSemanticKindIds0218.Area, SortOrder = 10, IsVisibleToPlayers = true, Revision = 1 };
        if (!_mongo.MapSemanticLayers0218.Find(x => x.Id == layer.Id).Any()) _mongo.MapSemanticLayers0218.InsertOne(layer);
        var generated = new MapSemanticLayerState0218 { Id = map.Id + "_generated", CampaignId = map.CampaignId, MapId = map.Id, Name = "Семантические объекты", LayerKind = MapSemanticKindIds0218.PointOfInterest, SortOrder = 20, IsVisibleToPlayers = true, Revision = 1 };
        if (!_mongo.MapSemanticLayers0218.Find(x => x.Id == generated.Id).Any()) _mongo.MapSemanticLayers0218.InsertOne(generated);
        var notes = new MapSemanticLayerState0218 { Id = map.Id + "_gm_notes", CampaignId = map.CampaignId, MapId = map.Id, Name = "Заметки мастера", LayerKind = MapSemanticKindIds0218.Label, SortOrder = 30, IsVisibleToPlayers = false, IsLocked = false, Revision = 1 };
        if (!_mongo.MapSemanticLayers0218.Find(x => x.Id == notes.Id).Any()) _mongo.MapSemanticLayers0218.InsertOne(notes);
        var features = FixtureFeatures0218(map);
        foreach (var feature in features)
            if (!_mongo.MapSemanticFeatures0218.Find(x => x.Id == feature.Id).Any()) _mongo.MapSemanticFeatures0218.InsertOne(feature);
        if (map.Id == "map_underground_archive_0218")
        {
            var obsoleteIds = new[]
            {
                "feature_archive_hall_0218",
                "feature_archive_secret_0218",
                "feature_archive_hidden_unrevealed_0218"
            };
            _mongo.MapSemanticFeatures0218.UpdateMany(
                item => obsoleteIds.Contains(item.Id),
                Builders<MapSemanticFeatureState0218>.Update
                    .Set(item => item.IsArchived, true)
                    .Set(item => item.UpdatedAtUtc, DateTime.UtcNow));
        }
    }

    private static List<MapSemanticFeatureState0218> FixtureFeatures0218(MapCanvasState map)
    {
        if (map.Id == "map_greyhaven_0218")
            return GenerateSettlementFeatures0218(map, "greyhaven-product-fixture-0218");
        if (map.Id == "map_underground_archive_0218")
        {
            var dungeon = GenerateDungeonFeatures0218(map, "archive-product-fixture-0218");
            var secret = dungeon.First(item => item.IsSecret);
            secret.Id = "feature_archive_secret_product_0218";
            return dungeon;
        }
        var items = new List<MapSemanticFeatureState0218>
        {
            FixtureFeature0218(map, "feature_" + map.Id + "_center", map.Name, MapSemanticKindIds0218.Area, MapGeometryKindIds0218.Polygon, false, true, new [] { P0218(10,10), P0218(90,10), P0218(90,90), P0218(10,90) })
        };
        if (map.Id == "map_north_valley_0218")
        {
        items.Add(FixtureFeature0218(map, "feature_north_road_0218", "Северный тракт", MapSemanticKindIds0218.Road, MapGeometryKindIds0218.Polyline, false, true, new[] { P0218(12,72), P0218(45,52), P0218(82,20) }));
        items.Add(FixtureFeature0218(map, "feature_north_river_0218", "Река Лин", MapSemanticKindIds0218.River, MapGeometryKindIds0218.Polyline, false, true, new[] { P0218(8,18), P0218(55,44), P0218(92,70) }));
        items.Add(FixtureFeature0218(map, "feature_north_ruins_0218", "Руины Сторожевой башни", MapSemanticKindIds0218.PointOfInterest, MapGeometryKindIds0218.Point, true, false, new[] { P0218(76,29) }));
        }
        if (map.Id == "map_sector_k12_0218")
        {
            items.Add(FixtureFeature0218(map, "feature_helios_system_0218", "Система Гелиос", MapSemanticKindIds0218.Star, MapGeometryKindIds0218.Point, false, true, new[] { P0218(38,42) }));
        items.Add(FixtureFeature0218(map, "feature_hidden_system_0218", "Скрытая система", MapSemanticKindIds0218.Secret, MapGeometryKindIds0218.Point, true, false, new[] { P0218(76,70) }));
        }
        if (map.Id == "map_helios_system_0218")
        {
            items.Add(FixtureFeature0218(map, "feature_helios_star_0218", "Гелиос", MapSemanticKindIds0218.Star, MapGeometryKindIds0218.Point, false, true, new[] { P0218(50,50) }));
        items.Add(FixtureFeature0218(map, "feature_asterion_0218", "Астерион", MapSemanticKindIds0218.Planet, MapGeometryKindIds0218.Point, false, true, new[] { P0218(70,50) }));
            items.Add(FixtureFeature0218(map, "feature_beacon_0218", "Станция Маяк", MapSemanticKindIds0218.Station, MapGeometryKindIds0218.Point, false, true, new[] { P0218(76,44) }));
        }
        return items;
    }

    private void EnsurePortal0218(MapCanvasState source, MapCanvasState target, string name, bool secret)
    {
        var id = "portal_" + source.Id + "_" + target.Id + (secret ? "_secret" : string.Empty);
        var portal = new MapPortalState0218 { Id = id, CampaignId = source.CampaignId, SourceMapId = source.Id, TargetMapId = target.Id, Name = name, IsPlayerVisible = !secret, IsSecret = secret, Revision = 1, UpdatedAtUtc = DateTime.UtcNow };
        if (!_mongo.MapPortals0218.Find(x => x.Id == id).Any()) _mongo.MapPortals0218.InsertOne(portal);
    }

    private void EnsureKnowledge0218(string ownerId, string entityId, string name, string precision, string actorId, double approximateX, double approximateY)
    {
        var id = "map_knowledge_0218_" + ownerId + "_" + entityId;
        var knowledge = new EntityKnowledgeState
        {
            Id = id, CampaignId = Campaign0218, KnowledgeId = id, EntityType = "map_geography", EntityId = entityId,
            EntityDisplayName = name, OwnerUserId = ownerId, Level = precision == MapDiscoveryPrecisionIds0218.Exact ? KnowledgeLevelIds.Truth : KnowledgeLevelIds.Partial,
            TruthRelation = KnowledgeTruthRelationIds.Accurate, PlayerSummary = "Местность исследована.", IsApplied = true,
            IsPlayerVisible = true, VisibilityMode = ProjectVisibilityModeIds.PlayerVisible, GrantedByUserId = actorId,
            GrantedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, UpdatedByUserId = actorId,
            ExtraData = new Dictionary<string, object> { { "precision", precision }, { "approximateX", approximateX }, { "approximateY", approximateY } }
        };
        _mongo.EntityKnowledgeStates.ReplaceOne(x => x.Id == id, knowledge, new ReplaceOptions { IsUpsert = true });
    }

    private MapGenerationJobState0218 RequireGenerationJob0218(string id)
    {
        id = RequireLength(id, 1, 128, "jobId");
        return _mongo.MapGenerationJobs0218.Find(x => x.Id == id).FirstOrDefault() ?? throw new InvalidOperationException("Задание генерации не найдено.");
    }

    private Dictionary<string, object> BuildPackage0218(MapCanvasState map)
    {
        var features = _mongo.MapSemanticFeatures0218.Find(x => x.MapId == map.Id && !x.IsArchived).ToList();
        var layers = _mongo.MapSemanticLayers0218.Find(x => x.MapId == map.Id && !x.IsArchived).ToList();
        var portals = _mongo.MapPortals0218.Find(x => x.SourceMapId == map.Id && !x.IsArchived).ToList();
        return new Dictionary<string, object>
        {
            { "format", "nrimap" }, { "schemaVersion", 1 }, { "exportedAtUtc", DateTime.UtcNow },
            { "map", AdminMapSummary0218(map) },
            { "layers", layers.Select(LayerPayload0218).Cast<object>().ToArray() },
            { "features", features.Select(feature => ExportFeaturePayload0218(feature)).Cast<object>().ToArray() },
            { "portals", portals.Select(AdminPortalPayload0218).Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> ExportFeaturePayload0218(MapSemanticFeatureState0218 feature)
    {
        var payload = AdminFeaturePayload0218(feature);
        payload.Remove("gmNotes");
        return payload;
    }

    private static string ValidateImportPath0218(string supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied)) throw new ArgumentException("Укажите пакет карты.");
        var full = Path.GetFullPath(supplied);
        if (!string.Equals(Path.GetExtension(full), ".nrimap", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Поддерживаются только пакеты .nrimap.");
        if (!File.Exists(full)) throw new FileNotFoundException("Пакет карты не найден.", full);
        return full;
    }

    private static Dictionary<string, object> ReadPackage0218(string path)
    {
        using (var file = File.OpenRead(path))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
        {
        if (archive.Entries.Any(entry => entry.FullName.Contains("..") || Path.IsPathRooted(entry.FullName))) throw new InvalidDataException("Пакет содержит небезопасный путь.");
            var entry = archive.GetEntry("package.json") ?? throw new InvalidDataException("В пакете отсутствует package.json.");
        if (entry.Length > 20 * 1024 * 1024) throw new InvalidDataException("Пакет превышает допустимый размер.");
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 }.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
        }
    }

    private static List<string> ValidatePackage0218(Dictionary<string, object> package)
    {
        var findings = new List<string>();
        if (!string.Equals(Convert.ToString(package.TryGetValue("format", out var format) ? format : null), "nrimap", StringComparison.Ordinal)) findings.Add("Неизвестный формат пакета.");
        if (!package.ContainsKey("map")) findings.Add("Отсутствует описание карты.");
        if (!package.ContainsKey("features")) findings.Add("Отсутствуют семантические объекты.");
        if (PackageList0218(package, "features").Count > 100000) findings.Add("Слишком много объектов карты.");
        return findings;
    }

    private static MapCanvasState PackageMap0218(IDictionary<string, object> raw) => new MapCanvasState
    {
        CampaignId = First0218(PayloadReader.GetString(raw, "campaignId"), Campaign0218), RuleSetId = RuleSet0218,
            Name = First0218(PayloadReader.GetString(raw, "name"), "Импортированная карта"), Description = PayloadReader.GetString(raw, "description"),
        MapType = First0218(PayloadReader.GetString(raw, "mapType"), MapTypeIds.Custom), ParentMapId = PayloadReader.GetString(raw, "parentMapId"),
        PrimaryBoundWorldEntityId = PayloadReader.GetString(raw, "primaryWorldEntityId"), CoordinateProfileId = PayloadReader.GetString(raw, "coordinateProfileId"),
        ScaleProfileId = PayloadReader.GetString(raw, "scaleProfileId"), WidthMeters = PayloadReader.GetInt(raw, "widthMeters") ?? 1,
        HeightMeters = PayloadReader.GetInt(raw, "heightMeters") ?? 1, VisibilityMode = MapVisibilityModes.GmOnly
    };

    private static MapSemanticFeatureState0218 PackageFeature0218(IDictionary<string, object> raw) => new MapSemanticFeatureState0218
    {
            Name = First0218(PayloadReader.GetString(raw, "name"), "Объект карты"), LayerId = PayloadReader.GetString(raw, "layerId"),
        SemanticKind = First0218(PayloadReader.GetString(raw, "semanticKind"), MapSemanticKindIds0218.PointOfInterest),
        GeometryKind = First0218(PayloadReader.GetString(raw, "geometryKind"), MapGeometryKindIds0218.Point),
        Points = ReadPackagePoints0218(raw), BoundWorldEntityId = PayloadReader.GetString(raw, "boundWorldEntityId"),
        IsPlayerVisible = PayloadReader.GetBool(raw, "isPlayerVisible"), IsSecret = PayloadReader.GetBool(raw, "isSecret"),
        IsManual = PayloadReader.GetBool(raw, "isManual"), GenerationIdentity = PayloadReader.GetString(raw, "generationIdentity"),
        PublicDescription = PayloadReader.GetString(raw, "publicDescription"), StyleKey = PayloadReader.GetString(raw, "styleKey")
    };

    private static List<MapPoint0218> ReadPackagePoints0218(IDictionary<string, object> raw)
    {
        var result = new List<MapPoint0218>();
        foreach (var item in PayloadReader.GetList(raw, "points") ?? new List<object>())
            if (item is IDictionary<string, object> map) result.Add(new MapPoint0218 { X = PayloadReader.GetDouble(map, "x") ?? 0, Y = PayloadReader.GetDouble(map, "y") ?? 0 });
        return result;
    }

    private static Dictionary<string, object> PackageDictionary0218(Dictionary<string, object> package, string key)
    {
        if (!package.TryGetValue(key, out var value)) return new Dictionary<string, object>();
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary<string, object> generic) return new Dictionary<string, object>(generic);
        throw new InvalidDataException("Неверное значение пакета: " + key);
    }

    private static List<IDictionary<string, object>> PackageList0218(Dictionary<string, object> package, string key)
    {
        if (!package.TryGetValue(key, out var value) || !(value is IEnumerable<object> enumerable)) return new List<IDictionary<string, object>>();
        return enumerable.OfType<IDictionary<string, object>>().ToList();
    }

    private static MapSpaceNodeState Node0218(string id, string parentId, string name, string type, int order) => new MapSpaceNodeState
    {
        Id = id, CampaignId = Campaign0218, RuleSetId = RuleSet0218, ParentId = parentId, Name = name, NodeType = type,
        SortOrder = order, Visibility = MapVisibilityModes.Party, UpdatedAtUtc = DateTime.UtcNow
    };

    private static MapCanvasState Map0218(string id, string parentMapId, string nodeId, string name, string type, string coordinateId, string scaleId, int width, int height) => new MapCanvasState
    {
        Id = id, CampaignId = Campaign0218, RuleSetId = RuleSet0218, WorldId = type == MapTypeIds.Galaxy || type == MapTypeIds.Sector || type == MapTypeIds.StarSystem || type == MapTypeIds.Planet || type == MapTypeIds.Orbital ? "world_scifi_0218" : "world_fantasy_0218",
        SpaceNodeId = nodeId, PrimaryBoundWorldEntityId = nodeId, BoundWorldEntityIds = new List<string> { nodeId }, ParentMapId = parentMapId,
            Name = name, Description = "Демонстрационная карта 0.21.8: " + name, MapType = type, CoordinateProfileId = coordinateId, ScaleProfileId = scaleId,
        WidthMeters = width, HeightMeters = height, CoordinateMode = CoordinateKindForFixture0218(coordinateId),
        VisibilityMode = MapVisibilityModes.Party, KnowledgePolicy = "character_discovery", EntityRevision = 1, EditorRevision = 1, UpdatedAtUtc = DateTime.UtcNow
    };

    private static MapCoordinateProfileDefinition0218 Coordinate0218(string id, string name, string kind) => new MapCoordinateProfileDefinition0218
    {
        Id = id, CampaignId = Campaign0218, Name = name, Kind = kind, MinX = 0, MinY = 0, MaxX = 100, MaxY = 100,
        UnitsPerMapUnit = 1, CanonicalUnit = kind == MapCoordinateProfileKindIds0218.SchematicNodeSpace ? "none" : "metre", Revision = 1
    };

    private static MapScaleProfileDefinition0218 Scale0218(string id, string name, string kind, double metres, bool exact, string display) => new MapScaleProfileDefinition0218
    {
        Id = id, CampaignId = Campaign0218, Name = name, Kind = kind, MetresPerMapUnit = metres, SupportsExactDistance = exact, DisplayUnit = display, Revision = 1
    };

    private static MapSemanticFeatureState0218 FixtureFeature0218(MapCanvasState map, string id, string name, string kind, string geometry, bool secret, bool visible, IEnumerable<MapPoint0218> points) => new MapSemanticFeatureState0218
    {
        Id = id, CampaignId = map.CampaignId, MapId = map.Id, LayerId = map.Id + "_geography", Name = name, SemanticKind = kind,
        GeometryKind = geometry, Points = points.ToList(), IsSecret = secret, IsPlayerVisible = visible, IsManual = true,
            PublicDescription = secret ? "Открытая тайна местности." : "Известный объект местности.", GMNotes = secret ? "Скрыто до открытия." : string.Empty,
        StyleKey = kind, Revision = 1, UpdatedAtUtc = DateTime.UtcNow
    };

    private static MapPoint0218 P0218(double x, double y) => new MapPoint0218 { X = x, Y = y };
    private static string StableId0218(string mapId, string seed, string identity) => "mapgen_" + StableMapPrng0218.Value(seed, mapId + "|" + identity, 0).ToString("x16");
    private static string[] GeneratorKinds0218(string scope)
    {
        if (scope == MapGenerationScopeIds0218.Dungeon) return new[] { MapSemanticKindIds0218.Room, MapSemanticKindIds0218.Road, MapSemanticKindIds0218.Secret };
        if (scope == MapGenerationScopeIds0218.Sector || scope == MapGenerationScopeIds0218.System) return new[] { MapSemanticKindIds0218.Star, MapSemanticKindIds0218.Planet, MapSemanticKindIds0218.Station };
        if (scope == MapGenerationScopeIds0218.Settlement || scope == "district") return new[] { MapSemanticKindIds0218.District, MapSemanticKindIds0218.Road, MapSemanticKindIds0218.PointOfInterest };
        return new[] { MapSemanticKindIds0218.Area, MapSemanticKindIds0218.Road, MapSemanticKindIds0218.River, MapSemanticKindIds0218.PointOfInterest };
    }

    private static string GeneratorName0218(string scope, string kind, int ordinal) => $"{GeneratorKindLabel0218(kind)} {ordinal}";
    private static string GeneratorScopeLabel0218(string scope) => scope switch
    {
        MapGenerationScopeIds0218.Region => "Фэнтезийный регион",
        MapGenerationScopeIds0218.Settlement => "Поселение",
        MapGenerationScopeIds0218.Dungeon => "Подземелье",
        MapGenerationScopeIds0218.Sector => "Космический сектор",
        MapGenerationScopeIds0218.System => "Звёздная система",
        MapGenerationScopeIds0218.Planet => "Основа поверхности планеты",
        _ => "Пользовательская генерация"
    };

    private static string CoordinateKindForFixture0218(string coordinateId) => coordinateId switch
    {
        "coord_grid_0218" => MapCoordinateProfileKindIds0218.SquareGrid,
        "coord_hex_0218" => MapCoordinateProfileKindIds0218.HexGrid,
        "coord_geo_0218" => MapCoordinateProfileKindIds0218.Geographic2D,
        "coord_schematic_0218" => MapCoordinateProfileKindIds0218.SchematicNodeSpace,
        _ => MapCoordinateProfileKindIds0218.LocalCartesian2D
    };
    private static string GeneratorKindLabel0218(string kind)
    {
        switch (kind)
        {
            case MapSemanticKindIds0218.Road: return "Дорога";
            case MapSemanticKindIds0218.River: return "Река";
            case MapSemanticKindIds0218.Area: return "Область";
            case MapSemanticKindIds0218.District: return "Район";
            case MapSemanticKindIds0218.Room: return "Помещение";
            case MapSemanticKindIds0218.Secret: return "Тайна";
            case MapSemanticKindIds0218.Star: return "Звезда";
            case MapSemanticKindIds0218.Planet: return "Планета";
            case MapSemanticKindIds0218.Station: return "Станция";
            default: return "Точка интереса";
        }
    }

    private static string DefaultGeneratorScope0218(string mapType)
    {
        if (mapType == MapTypeIds.Dungeon || mapType == MapTypeIds.Interior) return MapGenerationScopeIds0218.Dungeon;
        if (mapType == MapTypeIds.Settlement || mapType == MapTypeIds.District) return MapGenerationScopeIds0218.Settlement;
        if (mapType == MapTypeIds.Sector || mapType == MapTypeIds.Galaxy) return MapGenerationScopeIds0218.Sector;
        if (mapType == MapTypeIds.StarSystem) return MapGenerationScopeIds0218.System;
        if (mapType == MapTypeIds.Planet) return MapGenerationScopeIds0218.Planet;
        return MapGenerationScopeIds0218.Region;
    }

    private static string SanitizeFileName0218(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string((value ?? "map").Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(result) ? "map" : result;
    }
}
