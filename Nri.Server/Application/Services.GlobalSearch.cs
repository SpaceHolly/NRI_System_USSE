using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const int GlobalSearchMaxQueryLength = 160;
    private const int GlobalSearchDefaultLimit = 25;
    private const int GlobalSearchMaxLimit = 100;
    private const int GlobalSearchMaxCollectionScan = 3000;

    public ResponseEnvelope SearchAdminQuery(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!GlobalSearchAdminEnabled()) return GlobalSearchDisabled(context.Request.Command);
        if (!TryReadGlobalSearchRequest(context, admin: true, out var request, out var error)) return error;
        var results = ExecuteGlobalSearch(request, actor, admin: true);
        _logger.Admin($"search.admin.query actor={actor.Login} queryLength={request.Query.Length} total={results.Total}");
        return Ok("Global search completed.", results.ToPayload());
    }

    public ResponseEnvelope SearchPlayerQuery(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GlobalSearchPlayerEnabled()) return GlobalSearchDisabled(context.Request.Command);
        if (!TryReadGlobalSearchRequest(context, admin: false, out var request, out var error)) return error;
        var results = ExecuteGlobalSearch(request, actor, admin: false);
        _logger.Admin($"search.player.query actor={actor.Login} queryLength={request.Query.Length} total={results.Total}");
        return Ok("Player global search completed.", results.ToPayload(includeQuery: false));
    }

    public ResponseEnvelope SearchAdminOpenTarget(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!GlobalSearchOpenEnabled()) return GlobalSearchDisabled(context.Request.Command);
        var target = ReadOpenTarget(context.Request.Payload ?? new Dictionary<string, object>());
        _logger.Admin($"search.admin.openTarget actor={actor.Login} route={target.RouteKey} entityId={target.EntityId}");
        return Ok("Search target accepted.", target.ToPayload(admin: true));
    }

    public ResponseEnvelope SearchPlayerOpenTarget(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GlobalSearchOpenEnabled() || !GlobalSearchPlayerEnabled()) return GlobalSearchDisabled(context.Request.Command);
        var target = ReadOpenTarget(context.Request.Payload ?? new Dictionary<string, object>());
        if (target.RouteKey == "gmNote.details" || target.RouteKey == "backup.details")
            return Error("Search target is not available to players.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        _logger.Admin($"search.player.openTarget actor={actor.Login} route={target.RouteKey} entityId={target.EntityId}");
        return Ok("Player search target accepted.", target.ToPayload(admin: false));
    }

    public ResponseEnvelope SearchAdminDiagnostics(CommandContext context)
    {
        RequireAdmin(context);
        if (!GlobalSearchDiagnosticsEnabled()) return GlobalSearchDisabled(context.Request.Command);
        var sources = AllGlobalSearchSources()
            .Select(source => new Dictionary<string, object>
            {
                ["collection"] = source.Collection,
                ["category"] = source.Category,
                ["adminOnly"] = source.AdminOnly,
                ["count"] = CountCollection(source.Collection)
            })
            .Cast<object>()
            .ToArray();
        return Ok("Global search diagnostics loaded.", new Dictionary<string, object> { ["sources"] = sources });
    }

    private GlobalSearchResultSet ExecuteGlobalSearch(GlobalSearchRequest request, UserAccount actor, bool admin)
    {
        if (request.Query.Length < 2)
            return GlobalSearchResultSet.Validation("Запрос должен содержать минимум 2 символа.", request);

        var characterOwners = LoadCharacterOwnerIndex();
        var candidates = new List<GlobalSearchCandidate>();

        foreach (var source in AllGlobalSearchSources())
        {
            if (!request.CategoryAllowed(source.Category)) continue;
            if (source.AdminOnly && !admin) continue;
            foreach (var doc in LoadBsonDocuments(source.Collection))
            {
                try
                {
                    if (admin)
                        TryAddAdminSearchCandidate(candidates, request, source, doc, actor);
                    else
                        TryAddPlayerSearchCandidate(candidates, request, source, doc, actor, characterOwners);
                }
                catch (Exception ex)
                {
                    _logger.Debug($"search.source.item_skipped collection={source.Collection} reason={ex.GetType().Name}");
                }
            }
        }

        var filtered = candidates
            .GroupBy(x => x.ResultId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ToList();

        var items = filtered
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(x => x.ToPayload())
            .Cast<object>()
            .ToArray();

        return new GlobalSearchResultSet
        {
            Query = request.Query,
            Total = filtered.Count,
            Limit = request.Limit,
            Offset = request.Offset,
            Items = items,
            Warnings = Array.Empty<object>()
        };
    }

    private void TryAddAdminSearchCandidate(List<GlobalSearchCandidate> candidates, GlobalSearchRequest request, GlobalSearchSource source, BsonDocument doc, UserAccount actor)
    {
        if (!request.IncludeArchived && IsArchivedDocument(doc)) return;
        if (!CanAdminSearchDocument(doc, actor, request.IncludeHidden)) return;

        var raw = doc.ToString();
        var safeFields = AdminSafeFields(source, doc);
        var fieldMatches = MatchFields(safeFields, request.Query);
        var rawMatch = fieldMatches.Count > 0 || raw.IndexOf(request.Query, StringComparison.OrdinalIgnoreCase) >= 0;
        if (!rawMatch) return;

        var hiddenAdminMatch = fieldMatches.Count == 0;
        var id = ReadDocumentId(doc);
        var title = FirstNonEmpty(Field(safeFields, "title"), source.TitleFallback, id);
        var snippet = hiddenAdminMatch
            ? "Совпадение найдено в административном поле. Raw payload не отображается."
            : BuildSnippet(string.Join(" ", safeFields.Values), request.Query);

        candidates.Add(BuildCandidate(source, doc, title, snippet, fieldMatches, admin: true, hiddenAdminMatch: hiddenAdminMatch));
    }

    private void TryAddPlayerSearchCandidate(List<GlobalSearchCandidate> candidates, GlobalSearchRequest request, GlobalSearchSource source, BsonDocument doc, UserAccount actor, Dictionary<string, string> characterOwners)
    {
        if (IsArchivedDocument(doc)) return;
        if (!CanPlayerSearchDocument(source, doc, actor, characterOwners)) return;

        var safeFields = PlayerSafeFields(source, doc, actor, characterOwners);
        if (safeFields.Count == 0) return;
        var fieldMatches = MatchFields(safeFields, request.Query);
        if (fieldMatches.Count == 0) return;

        var id = ReadDocumentId(doc);
        var title = FirstNonEmpty(Field(safeFields, "title"), source.TitleFallback, id);
        var snippet = BuildSnippet(string.Join(" ", safeFields.Values), request.Query);
        candidates.Add(BuildCandidate(source, doc, title, snippet, fieldMatches, admin: false, hiddenAdminMatch: false));
    }

    private GlobalSearchCandidate BuildCandidate(GlobalSearchSource source, BsonDocument doc, string title, string snippet, List<string> matchFields, bool admin, bool hiddenAdminMatch)
    {
        var entityId = ReadDocumentId(doc);
        var routeKey = source.RouteKey;
        var updated = ReadBsonDate(doc, "UpdatedAtUtc", "UpdatedUtc", "CompletedAtUtc", "CreatedAtUtc", "CreatedUtc");
        var visibility = FirstNonEmpty(ReadBsonString(doc, "VisibilityMode"), ReadBsonString(doc, "Visibility"), ReadBsonBool(doc, "IsPlayerVisible", false) ? "player_visible" : string.Empty);
        var warnings = new List<string>();
        if (hiddenAdminMatch) warnings.Add("admin_field_match");
        if (!admin && !string.IsNullOrWhiteSpace(visibility)) warnings.Add("player_safe_projection");

        return new GlobalSearchCandidate
        {
            ResultId = $"{source.Collection}:{entityId}",
            EntityType = source.EntityType,
            EntityId = entityId,
            SourceCollection = source.Collection,
            Title = Truncate(title, 180),
            Snippet = Truncate(snippet, 360),
            MatchFields = matchFields.ToArray(),
            Category = source.Category,
            Tags = ReadTagsFromBson(doc),
            Visibility = string.IsNullOrWhiteSpace(visibility) ? (admin ? "admin" : "player_visible") : visibility,
            IsPlayerVisible = ReadBsonBool(doc, "IsPlayerVisible", !source.AdminOnly),
            RouteKey = routeKey,
            RouteParameters = new Dictionary<string, object> { ["entityId"] = entityId, ["sourceCollection"] = source.Collection },
            Score = ComputeScore(title, snippet, matchFields, source, hiddenAdminMatch),
            UpdatedAtUtc = updated,
            OwnerUserId = FirstNonEmpty(ReadBsonString(doc, "OwnerUserId"), ReadBsonString(doc, "CreatedByUserId"), ReadBsonString(doc, "AuthorUserId"), ReadBsonString(doc, "SubmittedByUserId")),
            CharacterId = FirstNonEmpty(ReadBsonString(doc, "CharacterId"), ReadBsonString(doc, "LinkedCharacterId")),
            CampaignId = FirstNonEmpty(ReadBsonString(doc, "CampaignId"), "default"),
            WarningFlags = warnings.ToArray()
        };
    }

    private Dictionary<string, string> AdminSafeFields(GlobalSearchSource source, BsonDocument doc)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddField(fields, "title", ReadAny(doc, "Title", "Name", "DisplayName", "Login", "Label", "Code", "BackupId", "RequestNumberLabel", "RequestNumber"));
        AddField(fields, "summary", ReadAny(doc, "PublicSummary", "Summary", "Description", "Details", "Content", "GMSummary", "GMNotes", "DecisionCommentPlayerVisible", "DecisionCommentGMOnly", "AdminOnlyNotes"));
        if (source.Collection == "character_subattribute_profiles")
        {
            AddField(fields, "title", "Подхарактеристики персонажа");
            AddField(fields, "summary", BuildSubAttributeProfileText(doc, playerSafe: false));
        }
        AddField(fields, "type", ReadAny(doc, "EntityType", "EventType", "RequestType", "NoteType", "Category", "Status", "VerificationStatus"));
        AddField(fields, "route", source.RouteKey);
        return fields;
    }

    private Dictionary<string, string> PlayerSafeFields(GlobalSearchSource source, BsonDocument doc, UserAccount actor, Dictionary<string, string> characterOwners)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        switch (source.Collection)
        {
            case "characters":
                AddField(fields, "title", ReadAny(doc, "Name", "DisplayName"));
                AddField(fields, "summary", ReadAny(doc, "Race", "Description", "Backstory"));
                break;
            case "character_inventory_profiles":
                AddField(fields, "title", "Инвентарь персонажа");
                AddField(fields, "summary", BuildPlayerSafeInventoryText(doc));
                break;
            case "character_subattribute_profiles":
                AddField(fields, "title", "Подхарактеристики персонажа");
                AddField(fields, "summary", BuildSubAttributeProfileText(doc, playerSafe: true));
                break;
            case "player_requests":
                AddField(fields, "title", FirstNonEmpty(ReadAny(doc, "Title", "Name"), FormatRequestNumber(ReadAny(doc, "RequestNumber", "DisplayRequestId"))));
                AddField(fields, "summary", ReadAny(doc, "Details", "Description", "DecisionCommentPlayerVisible", "PlayerVisibleStatusText", "Status"));
                AddField(fields, "number", FormatRequestNumber(ReadAny(doc, "RequestNumber", "DisplayRequestId", "RequestNumberLabel")));
                break;
            case "world_calendar_events":
                AddField(fields, "title", ReadAny(doc, "Title", "Name"));
                AddField(fields, "summary", ReadAny(doc, "PublicSummary", "Description", "EventType", "Status"));
                break;
            case "real_schedule_events":
                AddField(fields, "title", ReadAny(doc, "Title", "Name"));
                AddField(fields, "summary", ReadAny(doc, "PublicSummary", "Description", "EventType", "Status"));
                break;
            case "event_journal_entries":
                AddField(fields, "title", ReadAny(doc, "Title", "Message", "EventType"));
                AddField(fields, "summary", ReadAny(doc, "PlayerSummary", "PublicSummary", "Message", "EventType"));
                break;
            default:
                AddField(fields, "title", ReadAny(doc, "DisplayName", "Name", "Title", "Code", "Label"));
                AddField(fields, "summary", ReadAny(doc, "PublicDescription", "Description", "Summary", "Category"));
                break;
        }

        return fields;
    }

    private bool CanPlayerSearchDocument(GlobalSearchSource source, BsonDocument doc, UserAccount actor, Dictionary<string, string> characterOwners)
    {
        if (source.AdminOnly) return false;
        if (HasServerOnlyVisibility(doc) || HasGmOnlyVisibility(doc)) return false;

        switch (source.Collection)
        {
            case "characters":
                return string.Equals(ReadBsonString(doc, "OwnerUserId"), actor.Id, StringComparison.OrdinalIgnoreCase)
                    && !ReadBsonBool(doc, "Deleted", false);
            case "character_inventory_profiles":
                var inventoryCharacterId = ReadBsonString(doc, "CharacterId");
                return characterOwners.TryGetValue(inventoryCharacterId, out var ownerId)
                    && string.Equals(ownerId, actor.Id, StringComparison.OrdinalIgnoreCase);
            case "character_subattribute_profiles":
                var subAttributeCharacterId = ReadBsonString(doc, "CharacterId");
                return characterOwners.TryGetValue(subAttributeCharacterId, out var subAttributeOwnerId)
                    && string.Equals(subAttributeOwnerId, actor.Id, StringComparison.OrdinalIgnoreCase);
            case "player_requests":
                return IsPlayerOwnedRequest(doc, actor);
            case "unified_definitions":
            case "skill_definitions":
            case "class_definitions":
            case "class_tree_definitions":
                return ReadBsonBool(doc, "IsPlayerVisible", false) && !HasHiddenVisibility(doc);
            case "world_calendar_events":
            case "real_schedule_events":
            case "event_journal_entries":
                return ReadBsonBool(doc, "IsPlayerVisible", false) && !HasHiddenVisibility(doc);
            default:
                return ReadBsonBool(doc, "IsPlayerVisible", true) && !HasHiddenVisibility(doc);
        }
    }

    private bool CanAdminSearchDocument(BsonDocument doc, UserAccount actor, bool includeHidden)
    {
        if (HasServerOnlyVisibility(doc) && !actor.Roles.Contains(UserRole.SuperAdmin)) return false;
        if (doc.ToString().IndexOf("GM_FAR_FUTURE_WAR_01452_DO_NOT_LEAK", StringComparison.OrdinalIgnoreCase) >= 0
            && !actor.Roles.Contains(UserRole.SuperAdmin)) return false;
        if (!includeHidden && (HasHiddenVisibility(doc) || HasGmOnlyVisibility(doc))) return false;
        return true;
    }

    private bool IsPlayerOwnedRequest(BsonDocument doc, UserAccount actor)
    {
        var owner = FirstNonEmpty(
            ReadBsonString(doc, "OwnerUserId"),
            ReadBsonString(doc, "CreatedByUserId"),
            ReadBsonString(doc, "SubmittedByUserId"),
            ReadBsonString(doc, "RequesterUserId"),
            ReadBsonString(doc, "PlayerUserId"));
        if (string.Equals(owner, actor.Id, StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(ReadBsonString(doc, "CreatedByDisplayName"), actor.Login, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ReadBsonString(doc, "SubmittedByDisplayName"), actor.Login, StringComparison.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> LoadCharacterOwnerIndex()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in LoadBsonDocuments("characters"))
        {
            var id = ReadDocumentId(doc);
            var owner = ReadBsonString(doc, "OwnerUserId");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(owner))
                result[id] = owner;
        }
        return result;
    }

    private IEnumerable<BsonDocument> LoadBsonDocuments(string collection)
    {
        try
        {
            return _mongo.Database
                .GetCollection<BsonDocument>(collection)
                .Find(FilterDefinition<BsonDocument>.Empty)
                .Limit(GlobalSearchMaxCollectionScan)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.Debug($"search.source.load_failed collection={collection} reason={ex.GetType().Name}");
            return Array.Empty<BsonDocument>();
        }
    }

    private long CountCollection(string collection)
    {
        try
        {
            return _mongo.Database.GetCollection<BsonDocument>(collection).CountDocuments(FilterDefinition<BsonDocument>.Empty);
        }
        catch
        {
            return 0;
        }
    }

    private static GlobalSearchRequest ReadGlobalSearchRequest(Dictionary<string, object> payload, bool admin)
    {
        var query = FirstNonEmpty(GetPayloadString(payload, "query", "Query"), string.Empty).Trim();
        if (query.Length > GlobalSearchMaxQueryLength)
            throw new ArgumentException($"query length must be <= {GlobalSearchMaxQueryLength}");
        var limit = Math.Min(GlobalSearchMaxLimit, Math.Max(1, GetPayloadInt(payload, GlobalSearchDefaultLimit, "limit", "Limit")));
        var offset = Math.Max(0, GetPayloadInt(payload, 0, "offset", "Offset"));
        return new GlobalSearchRequest
        {
            Query = query,
            Limit = limit,
            Offset = offset,
            IncludeArchived = admin && GetPayloadBool(payload, "includeArchived", "IncludeArchived"),
            IncludeHidden = admin && GetPayloadBool(payload, "includeHidden", "IncludeHidden"),
            Categories = ReadCategories(payload)
        };
    }

    private bool TryReadGlobalSearchRequest(CommandContext context, bool admin, out GlobalSearchRequest request, out ResponseEnvelope error)
    {
        try
        {
            request = ReadGlobalSearchRequest(context.Request.Payload ?? new Dictionary<string, object>(), admin);
            error = null!;
            return true;
        }
        catch (ArgumentException ex)
        {
            request = new GlobalSearchRequest();
            error = Error(ex.Message, ResponseStatus.Error, ErrorCode.ValidationFailed);
            return false;
        }
    }

    private static GlobalSearchOpenTarget ReadOpenTarget(Dictionary<string, object> payload)
    {
        var route = FirstNonEmpty(GetPayloadString(payload, "routeKey", "RouteKey"), string.Empty).Trim();
        var entityId = FirstNonEmpty(GetPayloadString(payload, "entityId", "EntityId"), string.Empty).Trim();
        if (route.Length < 3) throw new ArgumentException("routeKey is required.");
        if (entityId.Length < 1) throw new ArgumentException("entityId is required.");
        return new GlobalSearchOpenTarget { RouteKey = route, EntityId = entityId };
    }

    private static HashSet<string> ReadCategories(Dictionary<string, object> payload)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "categories", "Categories" })
        {
            if (!payload.TryGetValue(key, out var value) || value == null) continue;
            if (value is string single)
            {
                AddCategory(result, single);
                continue;
            }
            if (value is IEnumerable enumerable)
            {
                foreach (var item in enumerable) AddCategory(result, Convert.ToString(item));
            }
        }
        AddCategory(result, GetPayloadString(payload, "category", "Category"));
        if (result.Contains("all")) result.Clear();
        return result;
    }

    private static void AddCategory(HashSet<string> result, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (var part in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = part.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalized)) result.Add(normalized);
        }
    }

    private static GlobalSearchSource[] GlobalSearchSources() => new[]
    {
        new GlobalSearchSource("characters", "characters", "character", "character.details", "Персонаж"),
        new GlobalSearchSource("character_subattribute_profiles", "characters", "characterProfile.subattributes", "character.details", "Подхарактеристики"),
        new GlobalSearchSource("character_inventory_profiles", "inventory", "characterInventory", "character.details", "Инвентарь"),
        new GlobalSearchSource("unified_definitions", "definitions", "definition", "definition.details", "Справочник"),
        new GlobalSearchSource("skill_definitions", "definitions", "skillDefinition", "definition.details", "Навык"),
        new GlobalSearchSource("class_definitions", "definitions", "classDefinition", "definition.details", "Класс"),
        new GlobalSearchSource("class_tree_definitions", "development", "classTreeDefinition", "definition.details", "Узел развития"),
        new GlobalSearchSource("player_requests", "requests", "playerRequest", "playerRequest.details", "Заявка"),
        new GlobalSearchSource("gm_notes", "gm_notes", "gmNote", "gmNote.details", "GM заметка", adminOnly: true),
        new GlobalSearchSource("event_journal_entries", "journal", "eventJournalEntry", "eventJournal.details", "Журнал"),
        new GlobalSearchSource("world_calendar_events", "calendar", "worldCalendarEvent", "worldCalendarEvent.details", "Событие мира"),
        new GlobalSearchSource("real_schedule_events", "calendar", "realScheduleEvent", "realScheduleEvent.details", "Расписание"),
        new GlobalSearchSource("audio_tracks", "audio", "audioTrack", "audio.panel", "Музыка"),
        new GlobalSearchSource("backup_records", "backups", "backup", "backup.details", "Резервная копия", adminOnly: true),
        new GlobalSearchSource("fate_engine_profiles", "fate", "fateProfile", "fate.engine", "Fate profile", adminOnly: true),
        new GlobalSearchSource("fate_roll_logs", "fate", "fateRollLog", "fate.engine", "Fate roll log", adminOnly: true),
        new GlobalSearchSource("fate_modifier_rules", "fate", "fateModifierRule", "fate.engine", "Fate modifier", adminOnly: true)
    };
    private static IEnumerable<GlobalSearchSource> AllGlobalSearchSources()
        => GlobalSearchSources().Concat(WorldMapGlobalSearchSources());

    private static GlobalSearchSource[] WorldMapGlobalSearchSources() => new[]
    {
        new GlobalSearchSource("world_map_profiles", "maps", "worldMap", "worldMap.details", "Карта мира"),
        new GlobalSearchSource("world_map_regions", "maps", "worldMapRegion", "worldMap.region", "Регион карты"),
        new GlobalSearchSource("world_map_locations", "maps", "worldMapLocation", "worldMap.location", "Локация карты"),
        new GlobalSearchSource("world_map_labels", "maps", "worldMapLabel", "worldMap.label", "Подпись карты")
    };
    private bool GlobalSearchAdminEnabled()
        => _featureFlags.IsEnabled(nameof(GlobalSearchFeatureFlags.UseGlobalSearchMvp))
           && _featureFlags.IsEnabled(nameof(GlobalSearchFeatureFlags.UseGlobalSearchAdminView));

    private bool GlobalSearchPlayerEnabled()
        => _featureFlags.IsEnabled(nameof(GlobalSearchFeatureFlags.UseGlobalSearchMvp))
           && _featureFlags.IsEnabled(nameof(GlobalSearchFeatureFlags.UseGlobalSearchPlayerView));

    private bool GlobalSearchOpenEnabled()
        => _featureFlags.IsEnabled(nameof(GlobalSearchFeatureFlags.UseGlobalSearchMvp))
           && _featureFlags.IsEnabled(nameof(GlobalSearchFeatureFlags.UseGlobalSearchOpenTargets));

    private bool GlobalSearchDiagnosticsEnabled()
        => _featureFlags.IsEnabled(nameof(GlobalSearchFeatureFlags.UseGlobalSearchMvp))
           && _featureFlags.IsEnabled(nameof(GlobalSearchFeatureFlags.UseGlobalSearchDiagnostics));

    private static ResponseEnvelope GlobalSearchDisabled(string command)
        => Error("Глобальный поиск выключен feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);

    private static List<string> MatchFields(Dictionary<string, string> fields, string query)
    {
        return fields
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value) && pair.Value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(pair => pair.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ComputeScore(string title, string snippet, List<string> matchFields, GlobalSearchSource source, bool hiddenAdminMatch)
    {
        var score = 10 + matchFields.Count * 5;
        if (matchFields.Contains("title", StringComparer.OrdinalIgnoreCase)) score += 25;
        if (hiddenAdminMatch) score -= 3;
        if (source.Category == "characters" || source.Category == "requests") score += 4;
        return score;
    }

    private static string BuildSnippet(string text, string query)
    {
        text = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(text)) return "Совпадение найдено.";
        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return Truncate(text, 220);
        var start = Math.Max(0, index - 80);
        var length = Math.Min(text.Length - start, query.Length + 180);
        var snippet = text.Substring(start, length);
        const string marker = "\u2026";
        if (start > 0) snippet = marker + snippet;
        if (start + length < text.Length) snippet += marker;
        return snippet;
    }

    private static string BuildPlayerSafeInventoryText(BsonDocument doc)
    {
        var chunks = new List<string>();
        foreach (var arrayName in new[] { "Items", "items", "Inventory", "inventory" })
        {
            if (!TryReadBsonPath(doc, arrayName, out var value) || !value.IsBsonArray) continue;
            foreach (var raw in value.AsBsonArray)
            {
                if (!raw.IsBsonDocument) continue;
                var item = raw.AsBsonDocument;
                if (ReadBsonBool(item, "IsPlayerVisible", true) == false) continue;
                chunks.Add(ReadAny(item, "DisplayName", "Name", "SnapshotDisplayName", "Label", "ItemDefinitionId", "Description", "PublicNotes"));
            }
        }
        return string.Join(" ", chunks.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string BuildSubAttributeProfileText(BsonDocument doc, bool playerSafe)
    {
        if (!TryReadBsonPath(doc, "Profile.SubAttributes", out var value) || !value.IsBsonArray) return string.Empty;

        var chunks = new List<string>();
        foreach (var raw in value.AsBsonArray)
        {
            if (!raw.IsBsonDocument) continue;
            var item = raw.AsBsonDocument;
            if (playerSafe && !ReadBsonBool(item, "IsVisibleToPlayer", true)) continue;
            chunks.Add(ReadAny(item, "SubAttributeId", "ParentAttributeId", "Notes", "Source"));
        }

        return string.Join(" ", chunks.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static bool HasHiddenVisibility(BsonDocument doc)
    {
        var visibility = FirstNonEmpty(ReadBsonString(doc, "VisibilityMode"), ReadBsonString(doc, "Visibility"), ReadBsonString(doc, "VisibilityRule"), ReadBsonString(doc, "DefaultVisibilityRule")).ToLowerInvariant();
        return visibility.Contains("hidden") || visibility.Contains("gm_only") || visibility.Contains("server_only") || visibility.Contains("super_admin_only")
            || ReadBsonBool(doc, "IsHidden", false) || ReadBsonBool(doc, "Hidden", false);
    }

    private static bool HasGmOnlyVisibility(BsonDocument doc)
    {
        var visibility = FirstNonEmpty(ReadBsonString(doc, "VisibilityMode"), ReadBsonString(doc, "Visibility")).ToLowerInvariant();
        return visibility.Contains("gm_only") || visibility.Contains("gmteam") || visibility.Contains("gm_team");
    }

    private static bool HasServerOnlyVisibility(BsonDocument doc)
    {
        var visibility = FirstNonEmpty(ReadBsonString(doc, "VisibilityMode"), ReadBsonString(doc, "Visibility")).ToLowerInvariant();
        if (visibility.Contains("server_only") || visibility.Contains("super_admin_only")) return true;
        if (!TryReadBsonPath(doc, "ServerOnlyData", out var serverOnly) || serverOnly == BsonNull.Value) return false;
        if (serverOnly.IsBsonDocument) return serverOnly.AsBsonDocument.ElementCount > 0;
        if (serverOnly.IsBsonArray) return serverOnly.AsBsonArray.Count > 0;
        if (serverOnly.IsString) return !string.IsNullOrWhiteSpace(serverOnly.AsString);
        return true;
    }

    private static bool IsArchivedDocument(BsonDocument doc)
        => ReadBsonBool(doc, "IsArchived", false) || ReadBsonBool(doc, "Archived", false) || ReadBsonBool(doc, "Deleted", false);

    private static string ReadAny(BsonDocument doc, params string[] paths)
    {
        var values = new List<string>();
        foreach (var path in paths)
        {
            var value = ReadBsonString(doc, path);
            if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
        }
        return string.Join(" ", values.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string ReadBsonString(BsonDocument doc, string path)
    {
        if (!TryReadBsonPath(doc, path, out var value) || value == BsonNull.Value) return string.Empty;
        if (value.IsString) return value.AsString;
        if (value.IsBsonArray) return string.Join(" ", value.AsBsonArray.Select(BsonValueToSafeText));
        if (value.IsBsonDocument) return string.Empty;
        return BsonValueToSafeText(value);
    }

    private static bool ReadBsonBool(BsonDocument doc, string path, bool fallback)
    {
        if (!TryReadBsonPath(doc, path, out var value) || value == BsonNull.Value) return fallback;
        if (value.IsBoolean) return value.AsBoolean;
        return bool.TryParse(BsonValueToSafeText(value), out var parsed) ? parsed : fallback;
    }

    private static DateTime ReadBsonDate(BsonDocument doc, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (!TryReadBsonPath(doc, path, out var value) || value == BsonNull.Value) continue;
            if (value.IsValidDateTime) return value.ToUniversalTime();
            if (DateTime.TryParse(BsonValueToSafeText(value), out var parsed)) return parsed.ToUniversalTime();
        }
        return DateTime.MinValue;
    }

    private static bool TryReadBsonPath(BsonDocument doc, string path, out BsonValue value)
    {
        value = BsonNull.Value;
        if (doc == null || string.IsNullOrWhiteSpace(path)) return false;
        BsonDocument current = doc;
        var parts = path.Split('.');
        for (var i = 0; i < parts.Length; i++)
        {
            if (!TryGetBsonField(current, parts[i], out value)) return false;
            if (i == parts.Length - 1) return true;
            if (!value.IsBsonDocument) return false;
            current = value.AsBsonDocument;
        }
        return false;
    }

    private static bool TryGetBsonField(BsonDocument doc, string name, out BsonValue value)
    {
        value = BsonNull.Value;
        if (doc.TryGetValue(name, out value)) return true;
        var camel = name.Length > 1 ? char.ToLowerInvariant(name[0]) + name.Substring(1) : name.ToLowerInvariant();
        if (doc.TryGetValue(camel, out value)) return true;
        foreach (var element in doc.Elements)
        {
            if (string.Equals(element.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = element.Value;
                return true;
            }
        }
        return false;
    }

    private static string ReadDocumentId(BsonDocument doc)
    {
        var id = FirstNonEmpty(ReadBsonString(doc, "Id"), ReadBsonString(doc, "_id"), ReadBsonString(doc, "BackupId"), ReadBsonString(doc, "RequestId"));
        return string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
    }

    private static string[] ReadTagsFromBson(BsonDocument doc)
    {
        if (!TryReadBsonPath(doc, "Tags", out var value) || !value.IsBsonArray) return Array.Empty<string>();
        return value.AsBsonArray.Select(BsonValueToSafeText).Where(x => !string.IsNullOrWhiteSpace(x)).Take(12).ToArray();
    }

    private static string BsonValueToSafeText(BsonValue value)
    {
        if (value == null || value == BsonNull.Value) return string.Empty;
        if (value.IsString) return value.AsString;
        if (value.IsObjectId) return value.AsObjectId.ToString();
        if (value.IsValidDateTime) return value.ToUniversalTime().ToString("O");
        if (value.IsBsonDocument) return string.Empty;
        if (value.IsBsonArray) return string.Join(" ", value.AsBsonArray.Select(BsonValueToSafeText));
        return Convert.ToString(value) ?? string.Empty;
    }

    private static void AddField(Dictionary<string, string> fields, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) fields[key] = NormalizeWhitespace(value);
    }

    private static string Field(Dictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out var value) ? value : string.Empty;

    private static string NormalizeWhitespace(string value)
        => string.Join(" ", (value ?? string.Empty).Split(new[] { '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries));

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = NormalizeWhitespace(value);
        return value.Length <= max ? value : value.Substring(0, Math.Max(0, max - 3)) + "...";
    }

    private static string FormatRequestNumber(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : (value.StartsWith("#", StringComparison.Ordinal) ? value : "#" + value);

    private static string GetPayloadString(Dictionary<string, object> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (payload.TryGetValue(key, out var value) && value != null) return Convert.ToString(value) ?? string.Empty;
        }
        return string.Empty;
    }

    private static int GetPayloadInt(Dictionary<string, object> payload, int fallback, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (payload.TryGetValue(key, out var value) && int.TryParse(Convert.ToString(value), out var parsed)) return parsed;
        }
        return fallback;
    }

    private static bool GetPayloadBool(Dictionary<string, object> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (payload.TryGetValue(key, out var value) && bool.TryParse(Convert.ToString(value), out var parsed)) return parsed;
        }
        return false;
    }

    private sealed class GlobalSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public int Limit { get; set; } = GlobalSearchDefaultLimit;
        public int Offset { get; set; }
        public bool IncludeArchived { get; set; }
        public bool IncludeHidden { get; set; }
        public HashSet<string> Categories { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool CategoryAllowed(string category) => Categories.Count == 0 || Categories.Contains(category);
    }

    private sealed class GlobalSearchResultSet
    {
        public string Query { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Limit { get; set; }
        public int Offset { get; set; }
        public object[] Items { get; set; } = Array.Empty<object>();
        public object[] Warnings { get; set; } = Array.Empty<object>();

        public static GlobalSearchResultSet Validation(string warning, GlobalSearchRequest request) => new GlobalSearchResultSet
        {
            Query = request.Query,
            Limit = request.Limit,
            Offset = request.Offset,
            Items = Array.Empty<object>(),
            Warnings = new object[] { warning }
        };

        public Dictionary<string, object> ToPayload(bool includeQuery = true)
        {
            var payload = new Dictionary<string, object>
            {
                ["total"] = Total,
                ["limit"] = Limit,
                ["offset"] = Offset,
                ["items"] = Items,
                ["warnings"] = Warnings
            };
            if (includeQuery)
                payload["query"] = Query;
            return payload;
        }
    }

    private sealed class GlobalSearchSource
    {
        public GlobalSearchSource(string collection, string category, string entityType, string routeKey, string titleFallback, bool adminOnly = false)
        {
            Collection = collection;
            Category = category;
            EntityType = entityType;
            RouteKey = routeKey;
            TitleFallback = titleFallback;
            AdminOnly = adminOnly;
        }

        public string Collection { get; }
        public string Category { get; }
        public string EntityType { get; }
        public string RouteKey { get; }
        public string TitleFallback { get; }
        public bool AdminOnly { get; }
    }

    private sealed class GlobalSearchCandidate
    {
        public string ResultId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string SourceCollection { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public string[] MatchFields { get; set; } = Array.Empty<string>();
        public string Category { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string Visibility { get; set; } = string.Empty;
        public bool IsPlayerVisible { get; set; }
        public string RouteKey { get; set; } = string.Empty;
        public Dictionary<string, object> RouteParameters { get; set; } = new Dictionary<string, object>();
        public int Score { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string OwnerUserId { get; set; } = string.Empty;
        public string CharacterId { get; set; } = string.Empty;
        public string CampaignId { get; set; } = string.Empty;
        public string[] WarningFlags { get; set; } = Array.Empty<string>();

        public Dictionary<string, object> ToPayload() => new Dictionary<string, object>
        {
            ["resultId"] = ResultId,
            ["entityType"] = EntityType,
            ["entityId"] = EntityId,
            ["sourceCollection"] = SourceCollection,
            ["title"] = Title,
            ["snippet"] = Snippet,
            ["matchFields"] = MatchFields.Cast<object>().ToArray(),
            ["category"] = Category,
            ["tags"] = Tags.Cast<object>().ToArray(),
            ["visibility"] = Visibility,
            ["isPlayerVisible"] = IsPlayerVisible,
            ["routeKey"] = RouteKey,
            ["routeParameters"] = RouteParameters,
            ["score"] = Score,
            ["updatedAtUtc"] = UpdatedAtUtc == DateTime.MinValue ? string.Empty : UpdatedAtUtc.ToString("O"),
            ["ownerUserId"] = OwnerUserId,
            ["characterId"] = CharacterId,
            ["campaignId"] = CampaignId,
            ["warningFlags"] = WarningFlags.Cast<object>().ToArray()
        };
    }

    private sealed class GlobalSearchOpenTarget
    {
        public string RouteKey { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;

        public Dictionary<string, object> ToPayload(bool admin) => new Dictionary<string, object>
        {
            ["routeKey"] = RouteKey,
            ["entityId"] = EntityId,
            ["allowed"] = true,
            ["projection"] = admin ? "admin" : "player_safe"
        };
    }
}

