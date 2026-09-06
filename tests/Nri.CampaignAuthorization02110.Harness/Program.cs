using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Application;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Configuration;
using Nri.Shared.Domain;

internal static class Program
{
    private const string Prefix = "audit_02110_";
    private const string CampaignA = Prefix + "campaign_a";
    private const string CampaignB = Prefix + "campaign_b";
    private const string SessionA = Prefix + "session_a";
    private const string SessionB = Prefix + "session_b";
    private const string PerformancePrefix = "perf_02110_";
    private static readonly string[] Collections =
    {
        "campaigns", "campaign_memberships", "current_sessions", "session_participations", "characters", "character_ownerships",
        "map_states", "map_token_instances", "combat_encounters", "player_requests", "project_base_states",
        "gm_notes", "audio_session_states", "chat_messages", "automation_policy_definitions", "automation_execution_records",
        "action_requests", "runtime_effect_instances",
        "character_groups", "character_group_members", "travel_sessions", "scene_map_active_links", "sync_events", "audit_logs",
        "active_game_context_preferences"
    };

    private static int Main(string[] args)
    {
        var mode = args.FirstOrDefault() ?? "seed";
        var client = new MongoClient("mongodb://localhost:27017");
        var database = client.GetDatabase("nri_system");
        if (string.Equals(mode, "performance-seed", StringComparison.OrdinalIgnoreCase))
            return SeedPerformance(database);
        if (string.Equals(mode, "performance-capability", StringComparison.OrdinalIgnoreCase))
            return BenchmarkCapability(database);
        if (string.Equals(mode, "performance-cleanup", StringComparison.OrdinalIgnoreCase))
        {
            CleanupPerformance(database);
            Console.WriteLine("Foundation 0.21.10 performance fixture cleanup: PASS");
            return 0;
        }
        if (string.Equals(mode, "inspect-automation", StringComparison.OrdinalIgnoreCase))
        {
            var executions = database.GetCollection<BsonDocument>("automation_execution_records")
                .Find(Builders<BsonDocument>.Filter.Regex("CampaignId", new BsonRegularExpression("^" + Prefix)))
                .Sort(Builders<BsonDocument>.Sort.Ascending("CreatedUtc")).ToList();
            var requests = database.GetCollection<BsonDocument>("action_requests")
                .Find(Builders<BsonDocument>.Filter.Regex("Fingerprint", new BsonRegularExpression("^weather-exposure:" + Prefix)))
                .ToList();
            var effects = database.GetCollection<BsonDocument>("runtime_effect_instances")
                .Find(Builders<BsonDocument>.Filter.Eq("TargetSubject.SubjectId", Prefix + "character_a")
                      & Builders<BsonDocument>.Filter.Eq("ConditionDefinitionId", "condition_cold_wet_0217"))
                .ToList();
            var session = database.GetCollection<BsonDocument>("current_sessions")
                .Find(Builders<BsonDocument>.Filter.Eq("SessionId", SessionA)).FirstOrDefault();
            Console.WriteLine(new BsonDocument
            {
                ["status"] = "PASS",
                ["executions"] = new BsonArray(executions.Select(x => new BsonDocument
                {
                    ["id"] = ReadString(x, "_id"),
                    ["campaignId"] = ReadString(x, "CampaignId"),
                    ["sessionId"] = ReadString(x, "SessionId"),
                    ["trigger"] = ReadString(x, "Trigger"),
                    ["action"] = ReadString(x, "TargetAction"),
                    ["executionStatus"] = ReadString(x, "Status"),
                    ["operationId"] = ReadString(x, "OperationId"),
                    ["correlationId"] = ReadString(x, "CorrelationId"),
                    ["failureCategory"] = ReadString(x, "FailureCategory")
                })),
                ["actionRequests"] = new BsonArray(requests.Select(x => new BsonDocument
                {
                    ["id"] = ReadString(x, "_id"),
                    ["requestStatus"] = ReadRequestStatus(x),
                    ["fingerprint"] = ReadString(x, "Fingerprint")
                })),
                ["runtimeEffects"] = new BsonArray(effects.Select(x => new BsonDocument
                {
                    ["id"] = ReadString(x, "_id"),
                    ["isActive"] = x.TryGetValue("IsActive", out var active) && active.ToBoolean(),
                    ["operationId"] = ReadString(x, "OperationId")
                })),
                ["sessionA"] = session == null ? BsonNull.Value : new BsonDocument
                {
                    ["mode"] = ReadString(session, "Mode"),
                    ["activeCombatId"] = ReadString(session, "ActiveCombatEncounterId"),
                    ["activeTravelId"] = ReadString(session, "ActiveTravelSessionId"),
                    ["currentSceneId"] = ReadString(session, "CurrentSceneId")
                }
            }.ToJson());
            return 0;
        }
        if (string.Equals(mode, "inspect-context", StringComparison.OrdinalIgnoreCase))
        {
            var login = args.Skip(1).FirstOrDefault() ?? "dev_player";
            var account = Account(database.GetCollection<BsonDocument>("accounts"), login)
                ?? throw new InvalidOperationException("Account not found: " + login);
            var userId = Id(account);
            var preference = database.GetCollection<BsonDocument>("active_game_context_preferences")
                .Find(Builders<BsonDocument>.Filter.Eq("UserId", userId)).FirstOrDefault();
            var campaignId = preference != null && preference.TryGetValue("CampaignId", out var campaign) ? campaign.AsString : string.Empty;
            var characterId = preference != null && preference.TryGetValue("CharacterId", out var character) ? character.AsString : string.Empty;
            var membership = database.GetCollection<BsonDocument>("campaign_memberships").Find(
                Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
                & Builders<BsonDocument>.Filter.Eq("UserId", userId)).FirstOrDefault();
            var ownership = database.GetCollection<BsonDocument>("character_ownerships").Find(
                Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
                & Builders<BsonDocument>.Filter.Eq("CharacterId", characterId)).FirstOrDefault();
            Console.WriteLine(new BsonDocument
            {
                ["status"] = "PASS", ["userId"] = userId,
                ["preference"] = new BsonDocument
                {
                    ["found"] = preference != null,
                    ["CampaignId"] = campaignId,
                    ["SessionId"] = ReadString(preference, "SessionId"),
                    ["CharacterId"] = characterId
                },
                ["membership"] = new BsonDocument
                {
                    ["found"] = membership != null,
                    ["CampaignId"] = ReadString(membership, "CampaignId"),
                    ["UserId"] = ReadString(membership, "UserId"),
                    ["PrimaryRoleId"] = ReadString(membership, "PrimaryRoleId"),
                    ["Status"] = ReadString(membership, "Status")
                },
                ["ownership"] = new BsonDocument
                {
                    ["found"] = ownership != null,
                    ["CampaignId"] = ReadString(ownership, "CampaignId"),
                    ["CharacterId"] = ReadString(ownership, "CharacterId"),
                    ["OwnerUserId"] = ReadString(ownership, "OwnerUserId"),
                    ["ControlledByUserId"] = ReadString(ownership, "ControlledByUserId")
                }
            }.ToJson());
            return 0;
        }
        if (string.Equals(mode, "cleanup", StringComparison.OrdinalIgnoreCase))
        {
            Cleanup(database);
            Console.WriteLine("Foundation 0.21.10 fixture cleanup: PASS");
            return 0;
        }

        Cleanup(database);
        var accounts = database.GetCollection<BsonDocument>("accounts");
        var admin = Account(accounts, "dev_admin");
        var player = Account(accounts, "dev_player");
        var alt = Account(accounts, "dev_player_alt");
        if (admin == null || player == null || alt == null) throw new InvalidOperationException("Dev accounts must be seeded before the authorization fixture.");
        var adminId = Id(admin); var playerId = Id(player); var altId = Id(alt);
        var now = DateTime.UtcNow;

        Insert(database, "campaigns", Doc(CampaignA, "CampaignId", CampaignA, "Name", "Authorization Campaign A", "OwnerUserId", adminId, "IsArchived", false));
        Insert(database, "campaigns", Doc(CampaignB, "CampaignId", CampaignB, "Name", "Authorization Campaign B Foreign", "OwnerUserId", altId, "IsArchived", false));
        InsertMembership(database, CampaignA, adminId, "owner_gm");
        InsertMembership(database, CampaignA, playerId, "player");
        InsertMembership(database, CampaignB, altId, "owner_gm");
        InsertSession(database, SessionA, CampaignA, adminId, "Authorization Session A", now);
        InsertSession(database, SessionB, CampaignB, altId, "Authorization Session B Foreign", now);
        Insert(database, "session_participations", Doc(Prefix + "participation_a", "CampaignId", CampaignA, "SessionId", SessionA, "UserId", playerId, "ParticipationRoleId", "player", "Status", "active", "AllowedCharacterIds", new BsonArray { Prefix + "character_a" }, "ActiveCharacterId", Prefix + "character_a"));
        InsertCharacter(database, Prefix + "character_a", SessionA, playerId, "Authorization Character A");
        InsertCharacter(database, Prefix + "character_b", SessionB, altId, "FOREIGN_CHARACTER_02110_DO_NOT_LEAK");
        InsertOwnership(database, Prefix + "ownership_a", CampaignA, Prefix + "character_a", playerId, "Authorization Character A");
        InsertOwnership(database, Prefix + "ownership_b", CampaignB, Prefix + "character_b", altId, "FOREIGN_CHARACTER_02110_DO_NOT_LEAK");
        Insert(database, "map_states", Doc(Prefix + "map_a", "CampaignId", CampaignA, "Name", "Authorization Scene Map A", "MapType", "scene", "IsArchived", false));
        Insert(database, "map_states", Doc(Prefix + "map_b", "CampaignId", CampaignB, "Name", "FOREIGN_MAP_02110_DO_NOT_LEAK", "MapType", "scene", "IsArchived", false));
        Insert(database, "map_token_instances", Doc(Prefix + "token_b", "MapId", Prefix + "map_b", "MapKind", "scene", "DisplayName", "FOREIGN_TOKEN_02110_DO_NOT_LEAK", "IsArchived", false));
        Insert(database, "combat_encounters", Doc(Prefix + "combat_a", "CampaignId", CampaignA, "SessionId", SessionA, "Name", "Authorization Combat A", "Status", "setup", "IsArchived", false));
        Insert(database, "combat_encounters", Doc(Prefix + "combat_b", "CampaignId", CampaignB, "SessionId", SessionB, "Name", "FOREIGN_COMBAT_02110_DO_NOT_LEAK", "Status", "setup", "IsArchived", false));
        InsertTravel(database, Prefix + "travel_a", CampaignA, Prefix + "character_a", playerId, "Authorization Travel A");
        InsertTravel(database, Prefix + "travel_b", CampaignB, Prefix + "character_b", altId, "FOREIGN_TRAVEL_02110_DO_NOT_LEAK");
        Insert(database, "player_requests", Doc(Prefix + "request_b", "CampaignId", CampaignB, "SessionId", SessionB, "CharacterId", Prefix + "character_b", "OwnerUserId", altId, "Title", "FOREIGN_REQUEST_02110_DO_NOT_LEAK", "Status", "submitted", "IsArchived", false));
        Insert(database, "project_base_states", Doc(Prefix + "project_b", "CampaignId", CampaignB, "SessionId", SessionB, "OwnerCharacterId", Prefix + "character_b", "Name", "FOREIGN_PROJECT_02110_DO_NOT_LEAK", "Status", "draft", "IsArchived", false));
        Insert(database, "gm_notes", Doc(Prefix + "note_b", "CampaignId", CampaignB, "SessionId", SessionB, "Title", "FOREIGN_NOTE_02110_DO_NOT_LEAK", "Content", "GM_ONLY_FOREIGN_02110", "IsArchived", false));
        Insert(database, "audio_session_states", Doc(Prefix + "audio_b", "CampaignId", CampaignB, "SessionId", SessionB, "Status", "stopped"));
        Insert(database, "chat_messages", Doc(Prefix + "chat_b", "CampaignId", CampaignB, "SessionId", SessionB, "Message", "FOREIGN_CHAT_02110_DO_NOT_LEAK", "IsPlayerVisible", true));
        Insert(database, "automation_policy_definitions", Doc(Prefix + "automation_b", "CampaignId", CampaignB, "Name", "FOREIGN_AUTOMATION_02110_DO_NOT_LEAK", "Trigger", "combat.ended", "Action", "session.attention.notify", "Enabled", true));

        var result = new BsonDocument
        {
            ["status"] = "PASS", ["database"] = "nri_system", ["fixturePrefix"] = Prefix,
            ["campaignA"] = CampaignA, ["campaignB"] = CampaignB, ["sessionA"] = SessionA, ["sessionB"] = SessionB,
            ["characterA"] = Prefix + "character_a", ["characterB"] = Prefix + "character_b",
            ["mapA"] = Prefix + "map_a", ["mapB"] = Prefix + "map_b", ["tokenB"] = Prefix + "token_b",
            ["combatA"] = Prefix + "combat_a", ["combatB"] = Prefix + "combat_b",
            ["travelA"] = Prefix + "travel_a", ["travelB"] = Prefix + "travel_b",
            ["requestB"] = Prefix + "request_b", ["projectB"] = Prefix + "project_b", ["noteB"] = Prefix + "note_b",
            ["adminUserId"] = adminId, ["playerUserId"] = playerId, ["altUserId"] = altId
        };
        Console.WriteLine(result.ToJson());
        return 0;
    }

    private static void InsertMembership(IMongoDatabase db, string campaignId, string userId, string role)
        => Insert(db, "campaign_memberships", Doc(Prefix + campaignId + "_" + userId, "CampaignId", campaignId, "UserId", userId, "PrimaryRoleId", role, "Status", "active", "EntityRevision", 1L, "IsArchived", false, "Archived", false));

    private static void InsertSession(IMongoDatabase db, string id, string campaignId, string gmId, string name, DateTime now)
        => Insert(db, "current_sessions", Doc(id, "SessionId", id, "CampaignId", campaignId, "GMUserId", gmId, "Name", name, "Status", "planned", "Mode", "narrative", "CreatedAtUtc", now, "UpdatedAtUtc", now, "EntityRevision", 1L, "IsArchived", false));

    private static void InsertCharacter(IMongoDatabase db, string id, string sessionId, string ownerId, string name)
        => Insert(db, "characters", Doc(id, "SessionId", sessionId, "OwnerUserId", ownerId, "Name", name, "IsActive", true, "IsArchived", false, "Revision", 1L));

    private static void InsertOwnership(IMongoDatabase db, string id, string campaignId, string characterId, string ownerId, string name)
        => Insert(db, "character_ownerships", Doc(id, "CampaignId", campaignId, "CharacterId", characterId, "CharacterDisplayName", name, "CharacterRole", "player_character", "OwnerUserId", ownerId, "ControlledByUserId", ownerId, "CharacterKind", "player_character", "CharacterStatus", "active", "IsActive", true, "IsArchived", false, "IsPlayerVisible", true, "AssignmentStatus", "assigned"));

    private static void InsertTravel(IMongoDatabase db, string id, string campaignId, string characterId, string ownerId, string name)
        => Insert(db, "travel_sessions", Doc(id,
            "CampaignId", campaignId,
            "WorldId", Prefix + "world",
            "PartyId", characterId,
            "PartyName", name,
            "PartyActorIds", new BsonArray { characterId },
            "PartyOwnerUserIds", new BsonArray { ownerId },
            "OriginLocationId", Prefix + "location_start",
            "OriginLocationName", "Start",
            "DestinationLocationId", Prefix + "location_end",
            "DestinationLocationName", "End",
            "ModeDefinitionId", "walking",
            "ModeName", "Walking",
            "ModeBaseSpeedKmh", 4m,
            "Segments", new BsonArray
            {
                new BsonDocument
                {
                    ["Order"] = 1,
                    ["FromLocationId"] = Prefix + "location_start",
                    ["FromLocationName"] = "Start",
                    ["ToLocationId"] = Prefix + "location_end",
                    ["ToLocationName"] = "End",
                    ["DistanceKm"] = 4m,
                    ["TerrainProfileId"] = "road",
                    ["TerrainName"] = "Road",
                    ["ModeMultiplier"] = 1m,
                    ["TerrainMultiplier"] = 1m,
                    ["WeatherMultiplier"] = 1m,
                    ["LoadMultiplier"] = 1m,
                    ["HazardTags"] = new BsonArray { "cold_wet" }
                }
            },
            "Status", "prepared",
            "Revision", 1,
            "IsArchived", false));

    private static BsonDocument? Account(IMongoCollection<BsonDocument> accounts, string login)
        => accounts.Find(Builders<BsonDocument>.Filter.Eq("Login", login) | Builders<BsonDocument>.Filter.Eq("login", login)).FirstOrDefault();

    private static string Id(BsonDocument document)
        => document.TryGetValue("Id", out var id) ? id.ToString() : document["_id"].ToString();

    private static string ReadString(BsonDocument? document, string field)
        => document != null && document.TryGetValue(field, out var value) && value.IsString ? value.AsString : string.Empty;

    private static string ReadRequestStatus(BsonDocument? document)
    {
        if (document == null || !document.TryGetValue("Status", out var value)) return string.Empty;
        if (value.IsString) return value.AsString;
        return value.IsInt32 && Enum.IsDefined(typeof(RequestStatus), value.AsInt32)
            ? ((RequestStatus)value.AsInt32).ToString()
            : value.ToString();
    }

    private static int SeedPerformance(IMongoDatabase database)
    {
        CleanupPerformance(database);
        var account = Account(database.GetCollection<BsonDocument>("accounts"), "dev_player")
            ?? throw new InvalidOperationException("dev_player account is required.");
        var normalUserId = Id(account);
        var campaigns = new List<BsonDocument>();
        var memberships = new List<BsonDocument>();
        var sessions = new List<BsonDocument>();
        var participations = new List<BsonDocument>();
        for (var campaignIndex = 0; campaignIndex < 100; campaignIndex++)
        {
            var campaignId = PerformancePrefix + "campaign_" + campaignIndex.ToString("D3");
            campaigns.Add(Doc(campaignId, "Name", "Performance Campaign " + campaignIndex, "OwnerUserId", normalUserId, "IsArchived", false));
            for (var memberIndex = 0; memberIndex < 10; memberIndex++)
            {
                var userId = campaignIndex < 10 && memberIndex == 0 ? normalUserId : PerformancePrefix + "user_" + campaignIndex.ToString("D3") + "_" + memberIndex.ToString("D2");
                memberships.Add(Doc(PerformancePrefix + "membership_" + campaignIndex.ToString("D3") + "_" + memberIndex.ToString("D2"),
                    "CampaignId", campaignId, "UserId", userId, "PrimaryRoleId", "player", "Status", "active",
                    "EntityRevision", 1L, "IsArchived", false, "Archived", false));
            }
            if (campaignIndex == 0)
            {
                for (var sessionIndex = 0; sessionIndex < 20; sessionIndex++)
                {
                    var sessionId = PerformancePrefix + "session_" + sessionIndex.ToString("D2");
                    sessions.Add(Doc(sessionId, "SessionId", sessionId, "CampaignId", campaignId, "GMUserId", normalUserId,
                        "Name", "Performance Session " + sessionIndex, "Status", "planned", "Mode", "narrative",
                        "EntityRevision", 1L, "IsArchived", false, "IsPlayerVisible", true, "VisibilityMode", "public"));
                    participations.Add(Doc(PerformancePrefix + "participation_" + sessionIndex.ToString("D2"),
                        "CampaignId", campaignId, "SessionId", sessionId, "UserId", normalUserId,
                        "ParticipationRoleId", "player", "Status", "active", "EntityRevision", 1L));
                }
            }
        }
        database.GetCollection<BsonDocument>("campaigns").InsertMany(campaigns);
        database.GetCollection<BsonDocument>("campaign_memberships").InsertMany(memberships);
        database.GetCollection<BsonDocument>("current_sessions").InsertMany(sessions);
        database.GetCollection<BsonDocument>("session_participations").InsertMany(participations);
        Console.WriteLine(new BsonDocument
        {
            ["status"] = "PASS", ["normalUserId"] = normalUserId,
            ["campaignCount"] = campaigns.Count, ["membershipCount"] = memberships.Count,
            ["selectedCampaignSessionCount"] = sessions.Count,
            ["sessionParticipationCount"] = participations.Count,
            ["authorizedCampaignCount"] = 10,
            ["selectedCampaignId"] = PerformancePrefix + "campaign_000",
            ["selectedSessionId"] = PerformancePrefix + "session_00"
        }.ToJson());
        return 0;
    }

    private static int BenchmarkCapability(IMongoDatabase database)
    {
        var account = Account(database.GetCollection<BsonDocument>("accounts"), "dev_player")
            ?? throw new InvalidOperationException("dev_player account is required.");
        var userId = Id(account);
        var logger = new CompositeLogger(new LoggingConfig
        {
            DebugLogPath = "obj/0_21_10/performance_harness_debug.log",
            SessionLogPath = "obj/0_21_10/performance_harness_session.log",
            AdminLogPath = "obj/0_21_10/performance_harness_admin.log",
            AuditLogPath = "obj/0_21_10/performance_harness_audit.log"
        });
        var config = new ServerConfig { Mongo = new MongoConfig { ConnectionString = "mongodb://localhost:27017", DatabaseName = "nri_system" } };
        var context = new MongoContext(config, logger);
        var authorization = new CampaignAuthorizationService02110(new MongoRepositoryFactory(context, logger));
        var campaignId = PerformancePrefix + "campaign_000";
        for (var i = 0; i < 20; i++) authorization.GetEffectiveCapabilities(userId, campaignId);
        var samples = new List<double>();
        for (var i = 0; i < 500; i++)
        {
            var timer = Stopwatch.StartNew();
            var capabilities = authorization.GetEffectiveCapabilities(userId, campaignId);
            timer.Stop();
            if (!capabilities.Contains(CampaignCapabilityIds.CampaignView)) throw new InvalidOperationException("Capability evaluation returned an invalid result.");
            samples.Add(timer.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        var p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
        var membershipIndexes = database.GetCollection<BsonDocument>("campaign_memberships").Indexes.List().ToList();
        var sessionIndexes = database.GetCollection<BsonDocument>("current_sessions").Indexes.List().ToList();
        Console.WriteLine(new BsonDocument
        {
            ["status"] = "PASS", ["sampleCount"] = samples.Count, ["p95Milliseconds"] = p95,
            ["maxMilliseconds"] = samples.Max(),
            ["membershipIndexes"] = new BsonArray(membershipIndexes.Select(x => x.GetValue("name", "").ToString())),
            ["sessionIndexes"] = new BsonArray(sessionIndexes.Select(x => x.GetValue("name", "").ToString()))
        }.ToJson());
        return 0;
    }

    private static void CleanupPerformance(IMongoDatabase database)
    {
        var prefix = new BsonRegularExpression("^" + PerformancePrefix);
        database.GetCollection<BsonDocument>("campaigns").DeleteMany(Builders<BsonDocument>.Filter.Regex("_id", prefix));
        database.GetCollection<BsonDocument>("campaign_memberships").DeleteMany(Builders<BsonDocument>.Filter.Regex("_id", prefix));
        database.GetCollection<BsonDocument>("current_sessions").DeleteMany(Builders<BsonDocument>.Filter.Regex("_id", prefix));
        database.GetCollection<BsonDocument>("session_participations").DeleteMany(Builders<BsonDocument>.Filter.Regex("_id", prefix));
        database.GetCollection<BsonDocument>("active_game_context_preferences").DeleteMany(Builders<BsonDocument>.Filter.Regex("CampaignId", prefix));
    }

    private static BsonDocument Doc(string id, params object[] fields)
    {
        var doc = new BsonDocument { ["_id"] = id, ["Id"] = id, ["CreatedAtUtc"] = DateTime.UtcNow, ["UpdatedAtUtc"] = DateTime.UtcNow, ["Archived"] = false, ["Deleted"] = false };
        for (var i = 0; i < fields.Length; i += 2) doc[Convert.ToString(fields[i]) ?? string.Empty] = BsonValue.Create(fields[i + 1]);
        return doc;
    }

    private static void Insert(IMongoDatabase db, string collection, BsonDocument document)
        => db.GetCollection<BsonDocument>(collection).InsertOne(document);

    private static void Cleanup(IMongoDatabase db)
    {
        foreach (var collectionName in Collections)
        {
            var collection = db.GetCollection<BsonDocument>(collectionName);
            var prefix = new BsonRegularExpression("^" + Prefix);
            var filter = Builders<BsonDocument>.Filter.Regex("_id", prefix)
                | Builders<BsonDocument>.Filter.Regex("CampaignId", prefix)
                | Builders<BsonDocument>.Filter.Regex("SessionId", prefix)
                | Builders<BsonDocument>.Filter.Regex("Target", new BsonRegularExpression(Prefix))
                | Builders<BsonDocument>.Filter.Regex("Fingerprint", new BsonRegularExpression(Prefix))
                | Builders<BsonDocument>.Filter.Regex("TargetSubject.SubjectId", prefix);
            collection.DeleteMany(filter);
        }
    }
}
