using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string DataPortabilityVersion = "0.14.59";
    private const string ImportConfirmation = "IMPORT";
    private const string DevAccessLeakToken = "DEV_ACCESS_01459_DO_NOT_LEAK_TO_PLAYER";

    private static readonly DevAccessKnownAccount[] KnownDevAccounts =
    {
        new DevAccessKnownAccount("dev_superadmin", "Dev SuperAdmin", "DevSuper_01459!", new[] { UserRole.SuperAdmin, UserRole.Admin }),
        new DevAccessKnownAccount("dev_admin", "Dev Admin", "DevAdmin_01459!", new[] { UserRole.Admin }),
        new DevAccessKnownAccount("dev_player", "Dev Player", "DevPlayer_01459!", new[] { UserRole.Player }),
        new DevAccessKnownAccount("dev_player_alt", "Dev Player Alt", "DevPlayerAlt_01459!", new[] { UserRole.Player })
    };

    private static readonly string[] DefinitionExportCollections =
    {
        "unified_definitions",
        "class_definitions",
        "race_definitions",
        "skill_definition_documents",
        "class_trees",
        "class_tree_definitions",
        "audio_tracks",
        "world_calendar_definitions",
        "world_calendar_months",
        "world_calendar_seasons",
        "feature_flag_overrides",
        "module_runtime_matrix"
    };

    private static readonly HashSet<string> DefinitionImportCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "unified_definitions",
        "class_definitions",
        "race_definitions",
        "skill_definition_documents",
        "class_trees",
        "class_tree_definitions",
        "audio_tracks",
        "world_calendar_definitions",
        "world_calendar_months",
        "world_calendar_seasons",
        "module_runtime_matrix"
    };

    private static readonly string[] CampaignExportCollections =
    {
        "accounts",
        "profiles",
        "characters",
        "character_ownerships",
        "character_module_states",
        "character_attribute_profiles",
        "character_subattribute_profiles",
        "character_skill_profiles",
        "character_development_profiles",
        "character_wallet_profiles",
        "character_inventory_profiles",
        "character_reputation_profiles",
        "character_holdings_profiles",
        "character_companion_profiles",
        "character_race_or_species_profiles",
        "character_body_profiles",
        "character_knowledge_profiles",
        "character_condition_profiles",
        "player_requests",
        "player_request_comments",
        "gm_notes",
        "gm_note_folders",
        "event_journal_entries",
        "event_journal_links",
        "current_sessions",
        "campaign_world_times",
        "world_calendar_events",
        "real_schedule_events",
        "audio_states",
        "audio_client_settings",
        "fate_engine_profiles",
        "fate_engine_states",
        "fate_roll_logs",
        "data_portability_acceptance_markers"
    };

    private static readonly HashSet<string> CampaignImportCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "data_portability_acceptance_markers"
    };

    public ResponseEnvelope DevAccessAdminStatus(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDataPortabilityIndexes();
        var items = KnownDevAccounts.Select(x => KnownAccountStatusPayload(x)).Cast<object>().ToArray();
        _logger.Admin($"dev_access.status actor={actor.Login} environment={_serverConfig.Environment} count={items.Length}");
        return Ok("Dev access status loaded.", new Dictionary<string, object>
        {
            ["environment"] = _serverConfig.Environment ?? string.Empty,
            ["isDevEnvironment"] = IsDevelopmentOrTest(),
            ["accounts"] = items
        });
    }

    public ResponseEnvelope DevAccessAdminResetKnownAccounts(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDevelopmentOrTest("Known dev account reset is disabled outside Development/Test.");
        EnsureDataPortabilityIndexes();

        var rows = new List<object>();
        foreach (var known in KnownDevAccounts)
        {
            var account = EnsureKnownDevAccount(known);
            rows.Add(KnownAccountPayload(known, account, includePassword: false));
        }

        EnsureDevPlayerCharacter(actor);
        WriteDataPortabilityAudit(actor, "dev_access.known_accounts_reset", "Known dev accounts reset.", "dev_access", playerVisible: false);
        _logger.Admin($"dev_access.known_accounts_reset actor={actor.Login} count={rows.Count}");
        return Ok("Known dev accounts reset.", new Dictionary<string, object>
        {
            ["accounts"] = rows.ToArray(),
            ["credentialsPrinted"] = false,
            ["environment"] = _serverConfig.Environment ?? string.Empty
        });
    }

    public ResponseEnvelope DevAccessAdminPrintKnownCredentials(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDevelopmentOrTest("Known dev credentials are disabled outside Development/Test.");
        var rows = KnownDevAccounts.Select(x =>
        {
            var account = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(a => a.Login, x.Login)).FirstOrDefault();
            return KnownAccountPayload(x, account, includePassword: true);
        }).Cast<object>().ToArray();
        WriteAudit("dev_access", actor.Id, "dev_access.known_credentials_printed", "transient");
        _logger.Admin($"dev_access.credentials_printed actor={actor.Login} token={DevAccessLeakToken}");
        return Ok("Known dev credentials prepared for local dev operator.", new Dictionary<string, object>
        {
            ["accounts"] = rows,
            ["warning"] = "Development/Test only. Passwords are not stored in MongoDB plaintext.",
            ["leakToken"] = DevAccessLeakToken
        });
    }

    public ResponseEnvelope DevAccessAdminVerifyKnownLogin(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDataPortabilityIndexes();
        var rows = KnownDevAccounts.Select(x =>
        {
            var account = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(a => a.Login, x.Login)).FirstOrDefault();
            var ok = account != null
                && account.Status == AccountStatus.Active
                && PasswordHasher.Hash(x.Password, account.PasswordSalt) == account.PasswordHash
                && x.Roles.All(role => account.Roles.Contains(role));
            return new Dictionary<string, object>
            {
                ["login"] = x.Login,
                ["status"] = account?.Status.ToString() ?? "missing",
                ["roles"] = account?.Roles.Select(r => r.ToString()).ToArray() ?? Array.Empty<string>(),
                ["passwordValid"] = ok,
                ["accountId"] = account?.Id ?? string.Empty
            };
        }).Cast<object>().ToArray();
        _logger.Admin($"dev_access.verify actor={actor.Login}");
        return Ok("Known dev account verification completed.", new Dictionary<string, object> { ["items"] = rows });
    }

    public ResponseEnvelope DevAccessAdminDisableKnownCredentials(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDevelopmentOrTest("Known dev credential disabling is disabled outside Development/Test.");
        foreach (var known in KnownDevAccounts)
        {
            var account = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(a => a.Login, known.Login)).FirstOrDefault();
            if (account == null) continue;
            account.Status = AccountStatus.Blocked;
            account.UpdatedUtc = DateTime.UtcNow;
            _repositories.Accounts.Replace(account);
        }

        WriteAudit("dev_access", actor.Id, "dev_access.known_accounts_disabled", "known");
        return Ok("Known dev accounts disabled.");
    }

    public ResponseEnvelope DataPortabilityAdminExportDefinitions(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDataPortabilityIndexes();
        var packageName = SafePackageName(DataPortabilityFirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "packageName"), "definitions_export"));
        var result = CreateExportPackage(actor, "definitions", packageName, DefinitionExportCollections, includeSensitive: false, sanitizeAccounts: false);
        WriteDataPortabilityAudit(actor, "data_export.definitions.created", $"Definitions export created: {packageName}", "data_portability", false);
        return Ok("Definitions export created.", result);
    }

    public ResponseEnvelope DataPortabilityAdminValidatePackage(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDataPortabilityIndexes();
        var validation = ValidatePackageFromPayload(context.Request.Payload, actor, writeRecord: true);
        var validationEventType = string.Equals(validation.PackageType, "campaign_data", StringComparison.OrdinalIgnoreCase)
            ? "data_import.campaign.validated"
            : "data_import.definitions.validated";
        WriteDataPortabilityAudit(actor, validationEventType, $"Package validated: {validation.PackageName}", "data_portability", false);
        return Ok(validation.IsValid ? "Package validated." : "Package validation failed.", validation.ToPayload());
    }

    public ResponseEnvelope DataPortabilityAdminImportDefinitionsDryRun(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDataPortabilityIndexes();
        var validation = ValidatePackageFromPayload(context.Request.Payload, actor, writeRecord: false);
        if (!validation.IsValid) throw new ArgumentException("Package validation failed: " + string.Join("; ", validation.Errors));
        if (!string.Equals(validation.PackageType, "definitions", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Package is not a definitions package.");
        var plan = BuildImportPlan(validation, DefinitionImportCollections);
        UpsertImportRecord(actor, validation, "definitions", "dry_run", "dry_run_passed", plan, Array.Empty<string>());
        WriteDataPortabilityAudit(actor, "data_import.definitions.dry_run", $"Definitions dry-run: {validation.PackageName}", "data_portability", false);
        return Ok("Definitions import dry-run completed.", plan);
    }

    public ResponseEnvelope DataPortabilityAdminImportDefinitions(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureNonProductionImport("Definitions import is disabled in Production.");
        EnsureDataPortabilityIndexes();
        RequireConfirmation(context.Request.Payload);
        var validation = ValidatePackageFromPayload(context.Request.Payload, actor, writeRecord: false);
        if (!validation.IsValid) throw new ArgumentException("Package validation failed: " + string.Join("; ", validation.Errors));
        if (!string.Equals(validation.PackageType, "definitions", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Package is not a definitions package.");
        EnsureDefinitionsPackageSafe(validation);
        var plan = BuildImportPlan(validation, DefinitionImportCollections);
        var applied = ApplyImportPackage(validation, DefinitionImportCollections);
        UpsertImportRecord(actor, validation, "definitions", "merge", "completed", plan, applied);
        WriteDataPortabilityAudit(actor, "data_import.definitions.completed", $"Definitions import completed: {validation.PackageName}", "data_portability", false);
        return Ok("Definitions import completed.", new Dictionary<string, object>(plan) { ["applied"] = applied });
    }

    public ResponseEnvelope DataPortabilityAdminExportCampaignData(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDataPortabilityIndexes();
        var packageName = SafePackageName(DataPortabilityFirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "packageName"), "campaign_export"));
        var includeSensitive = PayloadReader.GetBool(context.Request.Payload, "includeSensitive");
        var result = CreateExportPackage(actor, "campaign_data", packageName, CampaignExportCollections, includeSensitive, sanitizeAccounts: !includeSensitive);
        WriteDataPortabilityAudit(actor, "data_export.campaign.created", $"Campaign data export created: {packageName}", "data_portability", false);
        return Ok("Campaign data export created.", result);
    }

    public ResponseEnvelope DataPortabilityAdminImportCampaignDataDryRun(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDataPortabilityIndexes();
        var validation = ValidatePackageFromPayload(context.Request.Payload, actor, writeRecord: false);
        if (!validation.IsValid) throw new ArgumentException("Package validation failed: " + string.Join("; ", validation.Errors));
        if (!string.Equals(validation.PackageType, "campaign_data", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Package is not a campaign data package.");
        var plan = BuildImportPlan(validation, CampaignImportCollections);
        UpsertImportRecord(actor, validation, "campaign_data", "dry_run", "dry_run_passed", plan, Array.Empty<string>());
        WriteDataPortabilityAudit(actor, "data_import.campaign.dry_run", $"Campaign import dry-run: {validation.PackageName}", "data_portability", false);
        return Ok("Campaign data import dry-run completed.", plan);
    }

    public ResponseEnvelope DataPortabilityAdminImportCampaignData(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureNonProductionImport("Campaign data import is disabled in Production.");
        EnsureDataPortabilityIndexes();
        RequireConfirmation(context.Request.Payload);
        var mode = DataPortabilityFirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "mode"), "merge");
        if (string.Equals(mode, "replace", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Replace import mode requires a safety backup and is blocked in this MVP.");
        var validation = ValidatePackageFromPayload(context.Request.Payload, actor, writeRecord: false);
        if (!validation.IsValid) throw new ArgumentException("Package validation failed: " + string.Join("; ", validation.Errors));
        if (!string.Equals(validation.PackageType, "campaign_data", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Package is not a campaign data package.");
        var plan = BuildImportPlan(validation, CampaignImportCollections);
        var applied = ApplyImportPackage(validation, CampaignImportCollections);
        UpsertImportRecord(actor, validation, "campaign_data", mode, "completed", plan, applied);
        WriteDataPortabilityAudit(actor, "data_import.campaign.completed", $"Campaign data import completed: {validation.PackageName}", "data_portability", false);
        return Ok("Campaign data import completed.", new Dictionary<string, object>(plan) { ["applied"] = applied });
    }

    public ResponseEnvelope DataPortabilityAdminImportPreview(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDataPortabilityIndexes();
        var validation = ValidatePackageFromPayload(context.Request.Payload, actor, writeRecord: false);
        var allow = string.Equals(validation.PackageType, "definitions", StringComparison.OrdinalIgnoreCase) ? DefinitionImportCollections : CampaignImportCollections;
        var plan = validation.IsValid ? BuildImportPlan(validation, allow) : new Dictionary<string, object> { ["validationErrors"] = validation.Errors.ToArray() };
        return Ok("Import preview loaded.", plan);
    }

    public ResponseEnvelope DataPortabilityAdminExportList(CommandContext context)
    {
        RequireAdmin(context);
        EnsureDataPortabilityIndexes();
        var docs = ExportRecords().Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Descending("createdAtUtc")).Limit(50).ToList();
        return Ok("Export records loaded.", new Dictionary<string, object> { ["items"] = docs.Select(DocumentPayload).Cast<object>().ToArray() });
    }

    public ResponseEnvelope DataPortabilityAdminImportList(CommandContext context)
    {
        RequireAdmin(context);
        EnsureDataPortabilityIndexes();
        var docs = ImportRecords().Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Descending("createdAtUtc")).Limit(50).ToList();
        return Ok("Import records loaded.", new Dictionary<string, object> { ["items"] = docs.Select(DocumentPayload).Cast<object>().ToArray() });
    }

    private UserAccount EnsureKnownDevAccount(DevAccessKnownAccount known)
    {
        var account = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, known.Login)).FirstOrDefault();
        var now = DateTime.UtcNow;
        var salt = PasswordHasher.CreateSalt();
        if (account == null)
        {
            var profile = new UserProfile { DisplayName = known.DisplayName, TimeZoneId = "Europe/Moscow" };
            _repositories.Profiles.Insert(profile);
            account = new UserAccount
            {
                Login = known.Login,
                PasswordSalt = salt,
                PasswordHash = PasswordHasher.Hash(known.Password, salt),
                Roles = known.Roles.Distinct().ToList(),
                ProfileId = profile.Id,
                Status = AccountStatus.Active
            };
            _repositories.Accounts.Insert(account);
            profile.UserAccountId = account.Id;
            _repositories.Profiles.Replace(profile);
            return account;
        }

        account.PasswordSalt = salt;
        account.PasswordHash = PasswordHasher.Hash(known.Password, salt);
        account.Roles = known.Roles.Distinct().ToList();
        account.Status = AccountStatus.Active;
        account.Archived = false;
        account.Deleted = false;
        account.UpdatedUtc = now;
        if (string.IsNullOrWhiteSpace(account.ProfileId) || _repositories.Profiles.GetById(account.ProfileId) == null)
        {
            var profile = new UserProfile { UserAccountId = account.Id, DisplayName = known.DisplayName, TimeZoneId = "Europe/Moscow" };
            _repositories.Profiles.Insert(profile);
            account.ProfileId = profile.Id;
        }
        else
        {
            var profile = _repositories.Profiles.GetById(account.ProfileId);
            if (profile != null)
            {
                profile.UserAccountId = account.Id;
                profile.DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? known.DisplayName : profile.DisplayName;
                profile.TimeZoneId = string.IsNullOrWhiteSpace(profile.TimeZoneId) ? "Europe/Moscow" : profile.TimeZoneId;
                profile.UpdatedUtc = now;
                _repositories.Profiles.Replace(profile);
            }
        }

        _repositories.Accounts.Replace(account);
        return account;
    }

    private void EnsureDevPlayerCharacter(UserAccount actor)
    {
        var player = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, "dev_player")).FirstOrDefault();
        if (player == null) return;
        var character = _repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.OwnerUserId, player.Id) & Builders<Character>.Filter.Eq(x => x.Name, "Dev Player 0.14.59 Character")).FirstOrDefault();
        var created = false;
        if (character == null)
        {
            character = new Character();
            created = true;
        }

        character.OwnerUserId = player.Id;
        character.SessionId = "dev-session-01459";
        character.Name = "Dev Player 0.14.59 Character";
        character.Race = "Human";
        character.RaceCode = "human";
        character.Age = 30;
        character.Height = "175 cm";
        character.XpCoins = 10;
        character.Description = "Playable dev character for Foundation 0.14.59.";
        character.Backstory = "Created by guarded Dev Access reset.";
        character.Stats = character.Stats ?? new CharacterStats();
        character.Stats.Health = Math.Max(character.Stats.Health, 12);
        character.Stats.Strength = Math.Max(character.Stats.Strength, 3);
        character.Stats.Wisdom = Math.Max(character.Stats.Wisdom, 2);
        character.Archived = false;
        character.Deleted = false;
        character.UpdatedUtc = DateTime.UtcNow;

        if (created) _repositories.Characters.Insert(character); else _repositories.Characters.Replace(character);

        var ownership = _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault() ?? new CharacterOwnershipState();
        var ownershipCreated = string.IsNullOrWhiteSpace(ownership.CharacterId);
        ownership.CampaignId = "dev-campaign-01459";
        ownership.CharacterId = character.Id;
        ownership.CharacterDisplayName = character.Name;
        ownership.CharacterRole = CharacterOwnershipRoleIds.PlayerCharacter;
        ownership.CharacterKind = "player_character";
        ownership.OwnerUserId = player.Id;
        ownership.OwnerDisplayName = player.Login;
        ownership.ControlledByUserId = player.Id;
        ownership.ControlledByDisplayName = player.Login;
        ownership.IsPlayerVisible = true;
        ownership.VisibilityMode = MapVisibilityModes.Party;
        ownership.AssignmentStatus = CharacterOwnershipAssignmentStatusIds.Assigned;
        ownership.AssignedAtUtc = ownership.AssignedAtUtc ?? DateTime.UtcNow;
        ownership.AssignedByUserId = actor.Id;
        ownership.UpdatedAtUtc = DateTime.UtcNow;
        ownership.UpdatedByUserId = actor.Id;
        if (ownershipCreated) _repositories.CharacterOwnerships.Insert(ownership); else _repositories.CharacterOwnerships.Replace(ownership);

        var presence = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, player.Id)).FirstOrDefault() ?? new SessionUserState { UserId = player.Id };
        var presenceCreated = string.IsNullOrWhiteSpace(presence.Id);
        presence.UserId = player.Id;
        presence.CurrentGameSessionId = "dev-session-01459";
        presence.ActiveCharacterId = character.Id;
        presence.IsOnline = false;
        presence.LastSeenUtc = DateTime.UtcNow;
        if (presenceCreated) _repositories.Presence.Insert(presence); else _repositories.Presence.Replace(presence);
    }

    private Dictionary<string, object> CreateExportPackage(UserAccount actor, string packageType, string packageName, IEnumerable<string> collections, bool includeSensitive, bool sanitizeAccounts)
    {
        var packageId = $"{packageType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}".Substring(0, 58);
        var root = DataPortabilityPackageRoot();
        var packagePath = Path.Combine(root, packageId);
        var collectionsPath = Path.Combine(packagePath, "collections");
        Directory.CreateDirectory(collectionsPath);

        var manifestCollections = new BsonArray();
        var totalDocuments = 0L;
        var totalBytes = 0L;
        foreach (var collectionName in collections.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var collection = _mongo.Database.GetCollection<BsonDocument>(collectionName);
            var docs = collection.Find(FilterDefinition<BsonDocument>.Empty).ToList();
            if (sanitizeAccounts && string.Equals(collectionName, "accounts", StringComparison.OrdinalIgnoreCase))
                docs = docs.Select(RedactAccountDocument).ToList();

            var fileName = collectionName + ".ndjson";
            var filePath = Path.Combine(collectionsPath, fileName);
            using (var writer = new StreamWriter(filePath, false, new UTF8Encoding(false)))
            {
                foreach (var doc in docs)
                {
                    writer.WriteLine(doc.ToJson());
                }
            }

            var bytes = new FileInfo(filePath).Length;
            totalDocuments += docs.Count;
            totalBytes += bytes;
            manifestCollections.Add(new BsonDocument
            {
                ["collectionName"] = collectionName,
                ["documentCount"] = docs.Count,
                ["fileName"] = "collections/" + fileName,
                ["checksumSha256"] = DataPortabilitySha256File(filePath),
                ["schemaVersion"] = 1,
                ["category"] = packageType == "definitions" ? "definitions" : "runtime"
            });
        }

        var packageChecksum = DataPortabilitySha256Text(string.Join("|", manifestCollections.Select(x => x.AsBsonDocument.GetValue("checksumSha256", "").AsString)));
        var manifest = new BsonDocument
        {
            ["packageId"] = packageId,
            ["packageType"] = packageType,
            ["packageName"] = packageName,
            ["createdAtUtc"] = DateTime.UtcNow,
            ["createdByUserId"] = actor.Id,
            ["createdByDisplayName"] = actor.Login,
            ["sourceDatabase"] = _serverConfig.Mongo.DatabaseName ?? string.Empty,
            ["schemaVersions"] = new BsonDocument { ["dataPortability"] = DataPortabilityVersion },
            ["collections"] = manifestCollections,
            ["totalDocuments"] = totalDocuments,
            ["totalBytes"] = totalBytes,
            ["checksumAlgorithm"] = "sha256",
            ["packageChecksumSha256"] = packageChecksum,
            ["isSensitive"] = includeSensitive,
            ["compatibility"] = "nri-system foundation 0.14.59",
            ["warnings"] = new BsonArray(includeSensitive ? new[] { "Sensitive export requested. Password hashes may be present; plaintext passwords are never exported." } : new[] { "Sensitive fields redacted where applicable." }),
            ["notes"] = packageType == "definitions" ? "Definitions export package." : "Campaign/runtime data export package."
        };
        var manifestPath = Path.Combine(packagePath, "manifest.json");
        File.WriteAllText(manifestPath, manifest.ToJson(new MongoDB.Bson.IO.JsonWriterSettings { Indent = true }), new UTF8Encoding(false));

        var exportRecord = new BsonDocument
        {
            ["_id"] = packageId,
            ["exportId"] = packageId,
            ["exportType"] = packageType,
            ["packageName"] = packageName,
            ["packagePath"] = packagePath,
            ["manifestPath"] = manifestPath,
            ["createdAtUtc"] = DateTime.UtcNow,
            ["createdByUserId"] = actor.Id,
            ["createdByDisplayName"] = actor.Login,
            ["status"] = "completed",
            ["includedCollections"] = new BsonArray(collections),
            ["totalDocuments"] = totalDocuments,
            ["totalBytes"] = totalBytes,
            ["checksumSha256"] = packageChecksum,
            ["isSensitive"] = includeSensitive
        };
        ExportRecords().ReplaceOne(Builders<BsonDocument>.Filter.Eq("_id", packageId), exportRecord, new ReplaceOptions { IsUpsert = true });

        return new Dictionary<string, object>
        {
            ["exportId"] = packageId,
            ["packageName"] = packageName,
            ["packageType"] = packageType,
            ["packagePath"] = packagePath,
            ["manifestPath"] = manifestPath,
            ["totalDocuments"] = totalDocuments,
            ["totalBytes"] = totalBytes,
            ["checksumSha256"] = packageChecksum,
            ["collections"] = manifestCollections.Select(x => DocumentPayload(x.AsBsonDocument)).Cast<object>().ToArray()
        };
    }

    private DataPackageValidationResult ValidatePackageFromPayload(IDictionary<string, object> payload, UserAccount actor, bool writeRecord)
    {
        var packagePath = ResolvePackagePath(DataPortabilityFirstNonEmpty(PayloadReader.GetString(payload, "packagePath"), PayloadReader.GetString(payload, "path")));
        var manifestPath = Path.Combine(packagePath, "manifest.json");
        var result = new DataPackageValidationResult { PackagePath = packagePath, ManifestPath = manifestPath };
        if (!Directory.Exists(packagePath))
        {
            result.Errors.Add("Package directory not found.");
            WriteBlockedValidation(actor, result, writeRecord);
            return result;
        }
        if (!File.Exists(manifestPath))
        {
            result.Errors.Add("manifest.json not found.");
            WriteBlockedValidation(actor, result, writeRecord);
            return result;
        }

        BsonDocument manifest;
        try
        {
            manifest = BsonDocument.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            result.Errors.Add("Manifest parse failed: " + ex.GetType().Name);
            WriteBlockedValidation(actor, result, writeRecord);
            return result;
        }

        result.Manifest = manifest;
        result.PackageId = manifest.GetValue("packageId", "").ToString();
        result.PackageName = manifest.GetValue("packageName", result.PackageId).ToString();
        result.PackageType = manifest.GetValue("packageType", "").ToString();
        var collections = manifest.GetValue("collections", new BsonArray()).AsBsonArray;
        var recomputed = new List<string>();
        foreach (var item in collections.OfType<BsonDocument>())
        {
            var fileName = item.GetValue("fileName", "").ToString();
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || Path.IsPathRooted(fileName))
            {
                result.Errors.Add("Invalid collection file path in manifest.");
                continue;
            }
            var filePath = Path.GetFullPath(Path.Combine(packagePath, fileName));
            if (!filePath.StartsWith(packagePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("Collection file path traversal rejected.");
                continue;
            }
            if (!File.Exists(filePath))
            {
                result.Errors.Add("Collection file missing: " + fileName);
                continue;
            }
            var actual = DataPortabilitySha256File(filePath);
            var expected = item.GetValue("checksumSha256", "").ToString();
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                result.Errors.Add("Checksum mismatch: " + item.GetValue("collectionName", "").ToString());
            recomputed.Add(actual);
        }

        var expectedPackageChecksum = manifest.GetValue("packageChecksumSha256", "").ToString();
        var actualPackageChecksum = DataPortabilitySha256Text(string.Join("|", recomputed));
        if (!string.Equals(expectedPackageChecksum, actualPackageChecksum, StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Package checksum mismatch.");

        result.IsValid = result.Errors.Count == 0;
        if (writeRecord)
        {
            UpsertImportRecord(actor, result, result.PackageType, "validate_only", result.IsValid ? "validated" : "validation_failed", new Dictionary<string, object>(), Array.Empty<string>());
            if (!result.IsValid) WriteBlockedValidation(actor, result, writeRecord: false);
        }
        return result;
    }

    private Dictionary<string, object> BuildImportPlan(DataPackageValidationResult validation, ISet<string> allowedCollections)
    {
        var items = new List<object>();
        var skipped = new List<string>();
        foreach (var collection in ManifestCollections(validation.Manifest))
        {
            var collectionName = collection.GetValue("collectionName", "").ToString();
            var count = collection.GetValue("documentCount", 0).ToInt64();
            if (!allowedCollections.Contains(collectionName))
            {
                skipped.Add(collectionName);
                continue;
            }
            items.Add(new Dictionary<string, object>
            {
                ["collectionName"] = collectionName,
                ["documentCount"] = count,
                ["operation"] = "merge/upsert"
            });
        }

        return new Dictionary<string, object>
        {
            ["packageId"] = validation.PackageId,
            ["packageName"] = validation.PackageName,
            ["packageType"] = validation.PackageType,
            ["planned"] = items.ToArray(),
            ["skippedCollections"] = skipped.ToArray(),
            ["plannedCount"] = items.Count,
            ["warnings"] = skipped.Select(x => "Skipped non-allowed collection: " + x).ToArray()
        };
    }

    private string[] ApplyImportPackage(DataPackageValidationResult validation, ISet<string> allowedCollections)
    {
        var applied = new List<string>();
        foreach (var collection in ManifestCollections(validation.Manifest))
        {
            var collectionName = collection.GetValue("collectionName", "").ToString();
            if (!allowedCollections.Contains(collectionName)) continue;
            var fileName = collection.GetValue("fileName", "").ToString();
            var filePath = Path.GetFullPath(Path.Combine(validation.PackagePath, fileName));
            var target = _mongo.Database.GetCollection<BsonDocument>(collectionName);
            var changed = 0;
            var lineNumber = 0;
            foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var doc = BsonDocument.Parse(line);
                    var filter = ImportDocumentFilter(collectionName, doc);
                    AlignReplacementIdForLogicalMerge(target, filter, doc);
                    target.ReplaceOne(filter, doc, new ReplaceOptions { IsUpsert = true });
                    changed++;
                }
                catch (Exception ex) when (ex is not ArgumentException)
                {
                    throw new ArgumentException($"Import failed for collection '{collectionName}' at line {lineNumber}: {ex.Message}", ex);
                }
            }
            applied.Add(collectionName + ":" + changed);
        }
        return applied.ToArray();
    }

    private static void AlignReplacementIdForLogicalMerge(IMongoCollection<BsonDocument> target, FilterDefinition<BsonDocument> filter, BsonDocument doc)
    {
        if (!doc.TryGetValue("_id", out var incomingId)) return;
        var existing = target.Find(filter).Project(Builders<BsonDocument>.Projection.Include("_id")).FirstOrDefault();
        if (existing == null || !existing.TryGetValue("_id", out var existingId)) return;
        if (!existingId.Equals(incomingId)) doc["_id"] = existingId;
    }

    private void EnsureDefinitionsPackageSafe(DataPackageValidationResult validation)
    {
        foreach (var collection in ManifestCollections(validation.Manifest))
        {
            var name = collection.GetValue("collectionName", "").ToString();
            if (string.Equals(name, "accounts", StringComparison.OrdinalIgnoreCase) || name.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new UnauthorizedAccessException("Definitions package cannot import accounts or password collections.");
        }
    }

    private FilterDefinition<BsonDocument> ImportDocumentFilter(string collectionName, BsonDocument doc)
    {
        if (string.Equals(collectionName, "unified_definitions", StringComparison.OrdinalIgnoreCase))
        {
            if (doc.TryGetValue("Id", out var stableId))
            {
                var category = doc.GetValue("Category", doc.GetValue("category", "")).ToString();
                return Builders<BsonDocument>.Filter.Eq("Category", category) & Builders<BsonDocument>.Filter.Eq("Id", stableId.ToString());
            }
            if (doc.TryGetValue("id", out var lowerStableId))
            {
                var category = doc.GetValue("Category", doc.GetValue("category", "")).ToString();
                return Builders<BsonDocument>.Filter.Eq("Category", category) & Builders<BsonDocument>.Filter.Eq("id", lowerStableId.ToString());
            }
            if (doc.TryGetValue("_id", out var unifiedMongoId)) return Builders<BsonDocument>.Filter.Eq("_id", unifiedMongoId);
        }
        if (doc.TryGetValue("_id", out var mongoId)) return Builders<BsonDocument>.Filter.Eq("_id", mongoId);
        if (doc.TryGetValue("Id", out var idValue)) return Builders<BsonDocument>.Filter.Eq("Id", idValue);
        if (doc.TryGetValue("id", out var lowerIdValue)) return Builders<BsonDocument>.Filter.Eq("id", lowerIdValue);
        doc["_id"] = Guid.NewGuid().ToString("N");
        return Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
    }

    private IEnumerable<BsonDocument> ManifestCollections(BsonDocument manifest)
        => manifest.GetValue("collections", new BsonArray()).AsBsonArray.OfType<BsonDocument>();

    private void UpsertImportRecord(UserAccount actor, DataPackageValidationResult validation, string importType, string mode, string status, Dictionary<string, object> plan, IReadOnlyCollection<string> applied)
    {
        var importId = string.IsNullOrWhiteSpace(validation.PackageId) ? Guid.NewGuid().ToString("N") : "import_" + validation.PackageId;
        var doc = new BsonDocument
        {
            ["_id"] = importId,
            ["importId"] = importId,
            ["packageName"] = validation.PackageName ?? string.Empty,
            ["packagePath"] = validation.PackagePath ?? string.Empty,
            ["manifestPath"] = validation.ManifestPath ?? string.Empty,
            ["importType"] = importType ?? string.Empty,
            ["mode"] = mode,
            ["status"] = status,
            ["createdAtUtc"] = DateTime.UtcNow,
            ["createdByUserId"] = actor.Id,
            ["createdByDisplayName"] = actor.Login,
            ["safetyBackupId"] = string.Empty,
            ["validationErrors"] = new BsonArray(validation.Errors),
            ["warnings"] = new BsonArray(plan.TryGetValue("warnings", out var warnings) && warnings is IEnumerable<string> strings ? strings : Array.Empty<string>()),
            ["plannedSummary"] = plan.TryGetValue("plannedCount", out var count) ? Convert.ToString(count) : string.Empty,
            ["applied"] = new BsonArray(applied)
        };
        ImportRecords().ReplaceOne(Builders<BsonDocument>.Filter.Eq("_id", importId), doc, new ReplaceOptions { IsUpsert = true });
    }

    private void WriteBlockedValidation(UserAccount actor, DataPackageValidationResult validation, bool writeRecord)
    {
        if (writeRecord)
            UpsertImportRecord(actor, validation, validation.PackageType, "validate_only", "validation_failed", new Dictionary<string, object>(), Array.Empty<string>());
        WriteDataPortabilityAudit(actor, "data_import.package.blocked", "Package validation blocked: " + string.Join("; ", validation.Errors), "data_portability", false);
    }

    private void WriteDataPortabilityAudit(UserAccount actor, string eventType, string summary, string sourceModule, bool playerVisible)
    {
        WriteAudit(sourceModule, actor.Id, eventType, "data_portability");
        try
        {
            _repositories.EventJournalEntries.Insert(new EventJournalEntryState
            {
                CampaignId = "system",
                SourceModule = sourceModule,
                SourceEventType = eventType,
                SourceEventId = Guid.NewGuid().ToString("N"),
                EntryType = "system",
                Category = "system",
                Severity = "info",
                Title = eventType,
                Summary = summary,
                PlayerSummary = playerVisible ? summary : string.Empty,
                GMDetails = summary,
                VisibilityMode = playerVisible ? EventJournalVisibilityModeIds.PlayerVisible : EventJournalVisibilityModeIds.GMOnly,
                IsPlayerVisible = playerVisible,
                IsAutomatic = true,
                ActorUserId = actor.Id,
                ActorDisplayName = actor.Login,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                OccurredAtUtc = DateTime.UtcNow,
                Tags = new List<string> { "foundation_0_14_59", "data_portability" }
            });
        }
        catch (Exception ex)
        {
            _logger.Debug($"data_portability.event_journal.write_failed type={ex.GetType().Name}");
        }
    }

    private Dictionary<string, object> KnownAccountStatusPayload(DevAccessKnownAccount known)
    {
        var account = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, known.Login)).FirstOrDefault();
        return KnownAccountPayload(known, account, includePassword: false);
    }

    private Dictionary<string, object> KnownAccountPayload(DevAccessKnownAccount known, UserAccount? account, bool includePassword)
    {
        return new Dictionary<string, object>
        {
            ["login"] = known.Login,
            ["displayName"] = known.DisplayName,
            ["accountId"] = account?.Id ?? string.Empty,
            ["exists"] = account != null,
            ["status"] = account?.Status.ToString() ?? "missing",
            ["roles"] = (account?.Roles ?? known.Roles.ToList()).Select(x => x.ToString()).ToArray(),
            ["password"] = includePassword ? known.Password : string.Empty,
            ["passwordStoredPlaintext"] = false
        };
    }

    private void EnsureDataPortabilityIndexes()
    {
        ExportRecords().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("exportType").Descending("createdAtUtc")));
        ExportRecords().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("packageName")));
        ImportRecords().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("importType").Descending("createdAtUtc")));
        ImportRecords().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("packageName")));
    }

    private IMongoCollection<BsonDocument> ExportRecords() => _mongo.Database.GetCollection<BsonDocument>("export_records");
    private IMongoCollection<BsonDocument> ImportRecords() => _mongo.Database.GetCollection<BsonDocument>("import_records");

    private string DataPortabilityPackageRoot()
    {
        var root = Path.GetFullPath(Path.Combine(_serverConfig.BackupStorage.BackupRootDirectory ?? "./backups", "data_portability_packages"));
        Directory.CreateDirectory(root);
        return root;
    }

    private string ResolvePackagePath(string? value)
    {
        var raw = RequireLength(value, 1, 512, "packagePath");
        if (raw.Contains("..")) throw new ArgumentException("Package path traversal rejected.");
        var root = DataPortabilityPackageRoot().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var combined = Path.IsPathRooted(raw) ? raw : Path.Combine(root, raw);
        var full = Path.GetFullPath(combined);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Package path is outside data portability package root.");
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private void EnsureDevelopmentOrTest(string message)
    {
        if (!IsDevelopmentOrTest()) throw new InvalidOperationException(message);
    }

    private void EnsureNonProductionImport(string message)
    {
        if (string.Equals(_serverConfig.Environment, "Production", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(message);
    }

    private bool IsDevelopmentOrTest()
        => string.Equals(_serverConfig.Environment, "Development", StringComparison.OrdinalIgnoreCase)
           || string.Equals(_serverConfig.Environment, "Test", StringComparison.OrdinalIgnoreCase)
           || string.Equals(_serverConfig.Environment, "Local", StringComparison.OrdinalIgnoreCase);

    private static void RequireConfirmation(IDictionary<string, object> payload)
    {
        var confirmation = PayloadReader.GetString(payload, "confirmation") ?? string.Empty;
        if (!string.Equals(confirmation, ImportConfirmation, StringComparison.Ordinal))
            throw new InvalidOperationException("Exact confirmation IMPORT is required.");
    }

    private static string SafePackageName(string value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "data_portability_package" : value.Trim();
        var chars = raw.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray();
        var safe = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(safe) ? "data_portability_package" : safe.Length > 80 ? safe.Substring(0, 80) : safe;
    }

    private static BsonDocument RedactAccountDocument(BsonDocument source)
    {
        var clone = new BsonDocument(source);
        foreach (var key in clone.Names.ToList())
        {
            if (key.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("salt", StringComparison.OrdinalIgnoreCase) >= 0)
                clone[key] = "[REDACTED]";
        }
        return clone;
    }

    private static string DataPortabilitySha256File(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string DataPortabilitySha256Text(string text)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string DataPortabilityFirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static Dictionary<string, object> DocumentPayload(BsonDocument doc)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in doc)
            result[element.Name] = BsonPayloadValue(element.Value);
        return result;
    }

    private static object BsonPayloadValue(BsonValue value)
    {
        if (value == null || value.IsBsonNull) return string.Empty;
        if (value.IsString) return value.AsString;
        if (value.IsBoolean) return value.AsBoolean;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsDouble) return value.AsDouble;
        if (value.IsBsonDateTime) return value.ToUniversalTime().ToString("O");
        if (value.IsBsonArray) return value.AsBsonArray.Select(BsonPayloadValue).ToArray();
        if (value.IsBsonDocument) return DocumentPayload(value.AsBsonDocument);
        return value.ToString();
    }

    private sealed class DevAccessKnownAccount
    {
        public DevAccessKnownAccount(string login, string displayName, string password, IReadOnlyCollection<UserRole> roles)
        {
            Login = login;
            DisplayName = displayName;
            Password = password;
            Roles = roles;
        }

        public string Login { get; }
        public string DisplayName { get; }
        public string Password { get; }
        public IReadOnlyCollection<UserRole> Roles { get; }
    }

    private sealed class DataPackageValidationResult
    {
        public string PackagePath { get; set; } = string.Empty;
        public string ManifestPath { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string PackageType { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public BsonDocument Manifest { get; set; } = new BsonDocument();
        public List<string> Errors { get; } = new List<string>();

        public Dictionary<string, object> ToPayload() => new Dictionary<string, object>
        {
            ["isValid"] = IsValid,
            ["packageId"] = PackageId,
            ["packageName"] = PackageName,
            ["packageType"] = PackageType,
            ["packagePath"] = PackagePath,
            ["manifestPath"] = ManifestPath,
            ["errors"] = Errors.ToArray(),
            ["manifest"] = DocumentPayload(Manifest)
        };
    }
}
