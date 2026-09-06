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
    private static readonly HashSet<string> CharacterScopedCampaignCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "characters",
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
        "character_title_profiles"
    };

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
        "definition_editor_profiles",
        "content_definition_records",
        "content_definition_audit_events",
        "content_definition_validation_results",
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
        "definition_editor_profiles",
        "content_definition_records",
        "content_definition_audit_events",
        "content_definition_validation_results",
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
        "character_creation_policies",
        "character_creation_drafts",
        "character_title_profiles",
        "player_requests",
        "player_request_comments",
        "gm_notes",
        "gm_note_folders",
        "event_journal_entries",
        "event_journal_links",
        "current_sessions",
        "campaigns",
        "campaign_memberships",
        "campaign_capability_definitions",
        "session_participations",
        "automation_policy_definitions",
        "automation_execution_records",
        "campaign_world_times",
        "world_calendar_events",
        "real_schedule_events",
        "audio_states",
        "audio_client_settings",
        "quest_definitions",
        "quest_instances",
        "quest_objectives",
        "quest_reward_bundles",
        "quest_reward_grants",
        "quest_audit_events",
        "shop_definitions",
        "shop_instances",
        "shop_offers",
        "purchase_requests",
        "purchase_receipts",
        "purchase_grants",
        "shop_audit_events",
        "rest_sessions",
        "rest_participants",
        "downtime_actions",
        "recovery_grants",
        "rest_audit_events",
        "asset_configuration_blueprints",
        "project_base_states",
        "project_stages",
        "project_requirements",
        "project_resource_requirements",
        "project_approvals",
        "project_audit_entries",
        "construction_sites",
        "construction_resource_reservations",
        "construction_stage_consumptions",
        "asset_states",
        "large_asset_maintenance_profiles",
        "asset_operation_states",
        "asset_maintenance_reservations",
        "asset_maintenance_stage_consumptions",
        "maintenance_service_records",
        "actor_runtime_states",
        "runtime_subject_capacity_profiles",
        "runtime_effect_instances",
        "action_execution_states",
        "resource_reservation_states",
        "live_state_events",
        "weather_states",
        "travel_sessions",
        "environmental_tolerance_profiles",
        "measurement_instrument_profiles",
        "environmental_protection_profiles",
        "environment_observations",
        "map_space_nodes",
        "map_states",
        "map_coordinate_profiles",
        "map_scale_profiles",
        "map_semantic_layers",
        "map_semantic_features",
        "map_portals",
        "map_generator_recipes",
        "map_generation_jobs",
        "map_identity_mappings",
        "map_room_interiors",
        "map_markers",
        "map_marker_bindings",
        "map_fog_layers",
        "map_scene_active_links",
        "world_map_states",
        "world_map_layers",
        "world_map_legends",
        "world_map_profiles",
        "world_map_regions",
        "world_map_locations",
        "world_map_labels",
        "world_map_definitions",
        "world_map_markers",
        "session_world_map_states",
        "scene_map_definitions",
        "scene_map_markers",
        "session_scene_map_states",
        "map_token_instances",
        "map_token_move_operations",
        "legal_entity_licenses",
        "fate_engine_profiles",
        "fate_engine_states",
        "fate_roll_logs",
        "scene_map_layers",
        "scene_map_shapes",
        "scene_map_tile_layers",
        "scene_map_tile_patches",
        "scene_map_asset_instances",
        "scene_map_generation_presets",
        "scene_map_templates",
        "scene_map_generation_runs",
        "combat_encounters",
        "combat_participants",
        "combat_turns",
        "combat_rounds",
        "combat_actions",
        "combat_runtime_logs",
        "combat_replay_events",
        "data_portability_acceptance_markers"
    };

    private static readonly HashSet<string> CampaignImportCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "data_portability_acceptance_markers",
        "campaigns",
        "campaign_memberships",
        "campaign_capability_definitions",
        "current_sessions",
        "session_participations",
        "character_ownerships",
        "characters",
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
        "character_creation_policies",
        "character_creation_drafts",
        "character_title_profiles",
        "automation_policy_definitions",
        "automation_execution_records",
        "quest_definitions",
        "quest_instances",
        "quest_objectives",
        "quest_reward_bundles",
        "quest_reward_grants",
        "quest_audit_events",
        "shop_definitions",
        "shop_instances",
        "shop_offers",
        "purchase_requests",
        "purchase_receipts",
        "purchase_grants",
        "shop_audit_events",
        "rest_sessions",
        "rest_participants",
        "downtime_actions",
        "recovery_grants",
        "rest_audit_events",
        "asset_configuration_blueprints",
        "project_base_states",
        "project_stages",
        "project_requirements",
        "project_resource_requirements",
        "project_approvals",
        "project_audit_entries",
        "construction_sites",
        "construction_resource_reservations",
        "construction_stage_consumptions",
        "asset_states",
        "large_asset_maintenance_profiles"
        ,"asset_operation_states"
        ,"asset_maintenance_reservations"
        ,"asset_maintenance_stage_consumptions"
        ,"maintenance_service_records"
        ,"actor_runtime_states"
        ,"runtime_subject_capacity_profiles"
        ,"runtime_effect_instances"
        ,"action_execution_states"
        ,"resource_reservation_states"
        ,"live_state_events"
        ,"weather_states"
        ,"travel_sessions"
        ,"environmental_tolerance_profiles"
        ,"measurement_instrument_profiles"
        ,"environmental_protection_profiles"
        ,"environment_observations"
        ,"map_space_nodes"
        ,"map_states"
        ,"map_coordinate_profiles"
        ,"map_scale_profiles"
        ,"map_semantic_layers"
        ,"map_semantic_features"
        ,"map_portals"
        ,"map_generator_recipes"
        ,"map_generation_jobs"
        ,"map_identity_mappings"
        ,"map_room_interiors"
        ,"map_markers"
        ,"map_marker_bindings"
        ,"map_fog_layers"
        ,"map_scene_active_links"
        ,"world_map_states"
        ,"world_map_layers"
        ,"world_map_legends"
        ,"world_map_profiles"
        ,"world_map_regions"
        ,"world_map_locations"
        ,"world_map_labels"
        ,"world_map_definitions"
        ,"world_map_markers"
        ,"session_world_map_states"
        ,"scene_map_definitions"
        ,"scene_map_markers"
        ,"session_scene_map_states"
        ,"map_token_instances"
        ,"map_token_move_operations"
        ,"scene_map_layers"
        ,"scene_map_shapes"
        ,"scene_map_tile_layers"
        ,"scene_map_tile_patches"
        ,"scene_map_asset_instances"
        ,"scene_map_generation_presets"
        ,"scene_map_templates"
        ,"scene_map_generation_runs"
        ,"combat_encounters"
        ,"combat_participants"
        ,"combat_turns"
        ,"combat_rounds"
        ,"combat_actions"
        ,"combat_runtime_logs"
        ,"combat_replay_events"
        ,"legal_entity_licenses"
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
        var actor = GetCurrentAccount(context);
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignViewAudit);
        EnsureDataPortabilityIndexes();
        var packageName = SafePackageName(DataPortabilityFirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "packageName"), "campaign_export"));
        var includeSensitive = PayloadReader.GetBool(context.Request.Payload, "includeSensitive");
        var result = CreateExportPackage(actor, "campaign_data", packageName, CampaignExportCollections, includeSensitive, sanitizeAccounts: !includeSensitive, campaignId);
        WriteDataPortabilityAudit(actor, "data_export.campaign.created", $"Campaign data export created: {packageName}", "data_portability", false);
        return Ok("Campaign data export created.", result);
    }

    public ResponseEnvelope DataPortabilityAdminImportCampaignDataDryRun(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignManageSettings);
        var validation = ValidatePackageFromPayload(context.Request.Payload, actor, writeRecord: false);
        if (!validation.IsValid) throw new ArgumentException("Package validation failed: " + string.Join("; ", validation.Errors));
        if (!string.Equals(validation.PackageType, "campaign_data", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Package is not a campaign data package.");
        var plan = BuildCampaignImportDryRunPlan(validation, context.Request.Payload);
        return Ok("Campaign data import dry-run completed.", plan);
    }

    public ResponseEnvelope DataPortabilityAdminImportCampaignData(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignManageSettings);
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

    private Dictionary<string, object> CreateExportPackage(UserAccount actor, string packageType, string packageName, IEnumerable<string> collections, bool includeSensitive, bool sanitizeAccounts, string campaignId = "")
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
            var filter = string.IsNullOrWhiteSpace(campaignId)
                ? FilterDefinition<BsonDocument>.Empty
                : CampaignExportFilter02110(collectionName, campaignId);
            var docs = collection.Find(filter).ToList();
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

    private FilterDefinition<BsonDocument> CampaignExportFilter02110(string collectionName, string campaignId)
    {
        var filter = Builders<BsonDocument>.Filter;
        if (string.Equals(collectionName, "campaigns", StringComparison.OrdinalIgnoreCase))
            return filter.Eq("_id", campaignId) | filter.Eq("Id", campaignId);
        if (string.Equals(collectionName, "campaign_capability_definitions", StringComparison.OrdinalIgnoreCase))
            return FilterDefinition<BsonDocument>.Empty;
        if (CharacterScopedCampaignCollections.Contains(collectionName))
        {
            var characterIds = _mongo.CharacterOwnerships.Find(x => x.CampaignId == campaignId)
                .ToList().Select(x => x.CharacterId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
            if (characterIds.Length == 0) return filter.In("_id", Array.Empty<string>());
            return string.Equals(collectionName, "characters", StringComparison.OrdinalIgnoreCase)
                ? filter.In("_id", characterIds) | filter.In("Id", characterIds)
                : filter.In("CharacterId", characterIds) | filter.In("characterId", characterIds);
        }
        return filter.Eq("CampaignId", campaignId) | filter.Eq("campaignId", campaignId);
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

    private Dictionary<string, object> BuildCampaignImportDryRunPlan(
        DataPackageValidationResult validation,
        IDictionary<string, object> payload)
    {
        var plan = BuildImportPlan(validation, CampaignImportCollections);
        plan["dryRun"] = true;
        plan["liveDatabaseWrites"] = 0;

        var focusMapId = PayloadReader.GetString(payload, "focusMapId") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(focusMapId))
            return BuildMapImportDryRunPlan0201(plan, validation, focusMapId);

        var focusWeatherId = PayloadReader.GetString(payload, "focusWeatherId") ?? string.Empty;
        var focusTravelId = PayloadReader.GetString(payload, "focusTravelId") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(focusWeatherId) || !string.IsNullOrWhiteSpace(focusTravelId))
            return BuildWeatherTravelImportDryRunPlan0217(plan, validation, focusWeatherId, focusTravelId);

        var focusProjectId = PayloadReader.GetString(payload, "focusProjectId") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(focusProjectId))
        {
            var allowedManifestCollections = ManifestCollections(validation.Manifest)
                .Select(x => x.GetValue("collectionName", "").ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x) && CampaignImportCollections.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var duplicateIdentityConflicts = new List<string>();
            var genericPlannedCounts = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var collectionName in allowedManifestCollections)
            {
                var rows = ReadPackageDocuments(validation, collectionName);
                genericPlannedCounts[collectionName] = rows.Count;
                duplicateIdentityConflicts.AddRange(rows
                    .GroupBy(DocumentId, StringComparer.OrdinalIgnoreCase)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
                    .Select(x => collectionName + ":" + x.Key));
            }

            if (duplicateIdentityConflicts.Count > 0)
                throw new ArgumentException("Campaign import dry-run found duplicate identities: " + string.Join("; ", duplicateIdentityConflicts));

            plan["validatedCollections"] = allowedManifestCollections;
            plan["resolvedReferenceCount"] = 0;
            plan["unresolvedReferences"] = Array.Empty<string>();
            plan["duplicateIdentityConflicts"] = duplicateIdentityConflicts.ToArray();
            plan["plannedCounts"] = genericPlannedCounts;
            plan["competingCollections"] = Array.Empty<string>();
            return BuildCampaignContextImportDryRunPlan02110(plan, validation);
        }

        var projects = ReadPackageDocuments(validation, "project_base_states");
        var sites = ReadPackageDocuments(validation, "construction_sites");
        var reservations = ReadPackageDocuments(validation, "construction_resource_reservations");
        var consumptions = ReadPackageDocuments(validation, "construction_stage_consumptions");
        var assets = ReadPackageDocuments(validation, "asset_states");
        var maintenanceProfiles = ReadPackageDocuments(validation, "large_asset_maintenance_profiles");
        var blueprints = ReadPackageDocuments(validation, "asset_configuration_blueprints");
        var ownerships = ReadPackageDocuments(validation, "character_ownerships");
        var inventoryProfiles = ReadPackageDocuments(validation, "character_inventory_profiles");

        var projectRows = projects.Where(x => DocumentId(x) == focusProjectId).ToList();
        if (projectRows.Count == 1 && string.Equals(DocumentString(projectRows[0], "RuntimeKind"), AssetMaintenanceRuntimeIds0198.RuntimeKind, StringComparison.OrdinalIgnoreCase))
            return BuildAssetMaintenanceImportDryRunPlan0198(plan, validation, focusProjectId, projectRows[0]);
        var siteRows = sites.Where(x => DocumentString(x, "ProjectId") == focusProjectId).ToList();
        var reservationRows = reservations.Where(x => DocumentString(x, "ProjectId") == focusProjectId).ToList();
        var consumptionRows = consumptions.Where(x => DocumentString(x, "ProjectId") == focusProjectId).ToList();
        var assetRows = assets.Where(x => DocumentString(x, "ConstructionProjectId") == focusProjectId).ToList();
        var maintenanceRows = maintenanceProfiles.Where(x => DocumentString(x, "ProjectId") == focusProjectId).ToList();

        var expectedCounts = new Dictionary<string, object>
        {
            ["project_base_states"] = 1,
            ["construction_sites"] = 1,
            ["construction_resource_reservations"] = 3,
            ["construction_stage_consumptions"] = 3,
            ["asset_states"] = 1,
            ["large_asset_maintenance_profiles"] = 1
        };
        var plannedCounts = new Dictionary<string, object>
        {
            ["project_base_states"] = projectRows.Count,
            ["construction_sites"] = siteRows.Count,
            ["construction_resource_reservations"] = reservationRows.Count,
            ["construction_stage_consumptions"] = consumptionRows.Count,
            ["asset_states"] = assetRows.Count,
            ["large_asset_maintenance_profiles"] = maintenanceRows.Count
        };

        var unresolved = new List<string>();
        var resolvedReferenceCount = 0;
        var duplicates = new List<string>();
        foreach (var pair in plannedCounts)
        {
            var expected = Convert.ToInt32(expectedCounts[pair.Key]);
            var actual = Convert.ToInt32(pair.Value);
            if (actual != expected)
                unresolved.Add($"{pair.Key}: expected {expected}, found {actual}");
        }

        var focusedRows = new Dictionary<string, List<BsonDocument>>(StringComparer.OrdinalIgnoreCase)
        {
            ["project_base_states"] = projectRows,
            ["construction_sites"] = siteRows,
            ["construction_resource_reservations"] = reservationRows,
            ["construction_stage_consumptions"] = consumptionRows,
            ["asset_states"] = assetRows,
            ["large_asset_maintenance_profiles"] = maintenanceRows
        };
        foreach (var pair in focusedRows)
        {
            duplicates.AddRange(pair.Value
                .GroupBy(DocumentId, StringComparer.OrdinalIgnoreCase)
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
                .Select(x => pair.Key + ":" + x.Key));
        }

        var project = projectRows.SingleOrDefault();
        var site = siteRows.SingleOrDefault();
        var asset = assetRows.SingleOrDefault();
        var maintenance = maintenanceRows.SingleOrDefault();
        if (project != null)
        {
            ResolveReference(
                string.Equals(DocumentString(project, "RuntimeKind"), "asset_construction_0197", StringComparison.OrdinalIgnoreCase),
                "project runtime kind", unresolved, ref resolvedReferenceCount);
            ResolveReference(
                string.Equals(DocumentString(project, "Status"), "completed", StringComparison.OrdinalIgnoreCase),
                "project completed status", unresolved, ref resolvedReferenceCount);
        }

        var siteId = site == null ? string.Empty : DocumentId(site);
        var assetId = asset == null ? string.Empty : DocumentId(asset);
        if (site != null)
        {
            ResolveReference(DocumentString(site, "ProjectId") == focusProjectId,
                "site -> project", unresolved, ref resolvedReferenceCount);
            ResolveReference(DocumentString(site, "AssetInstanceId") == assetId,
                "site -> asset", unresolved, ref resolvedReferenceCount);
        }
        foreach (var row in reservationRows)
        {
            ResolveReference(DocumentString(row, "ProjectId") == focusProjectId,
                "reservation -> project", unresolved, ref resolvedReferenceCount);
            ResolveReference(DocumentString(row, "ConstructionSiteId") == siteId,
                "reservation -> site", unresolved, ref resolvedReferenceCount);
        }
        foreach (var row in consumptionRows)
        {
            ResolveReference(DocumentString(row, "ProjectId") == focusProjectId,
                "consumption -> project", unresolved, ref resolvedReferenceCount);
            ResolveReference(DocumentString(row, "ConstructionSiteId") == siteId,
                "consumption -> site", unresolved, ref resolvedReferenceCount);
        }
        if (asset != null)
        {
            ResolveReference(DocumentString(asset, "ConstructionProjectId") == focusProjectId,
                "asset -> project", unresolved, ref resolvedReferenceCount);
            ResolveReference(DocumentString(asset, "ConstructionSiteId") == siteId,
                "asset -> site", unresolved, ref resolvedReferenceCount);
            ResolveReference(!string.Equals(DocumentString(asset, "AssetType"), "inventory_item", StringComparison.OrdinalIgnoreCase),
                "asset is not an inventory item", unresolved, ref resolvedReferenceCount);
            ResolveReference(!PackageDocumentsContainId(inventoryProfiles, assetId),
                "asset absent from character inventory profiles", unresolved, ref resolvedReferenceCount);
        }
        if (maintenance != null)
        {
            ResolveReference(DocumentString(maintenance, "ProjectId") == focusProjectId,
                "maintenance -> project", unresolved, ref resolvedReferenceCount);
            ResolveReference(DocumentString(maintenance, "AssetInstanceId") == assetId,
                "maintenance -> asset", unresolved, ref resolvedReferenceCount);
            ResolveReference(asset != null && DocumentString(asset, "MaintenanceProfileId") == DocumentId(maintenance),
                "asset -> maintenance", unresolved, ref resolvedReferenceCount);
        }

        var blueprintId = site == null ? string.Empty : DocumentString(site, "BlueprintId");
        ResolveReference(blueprints.Any(x => DocumentId(x) == blueprintId),
            "blueprint reference", unresolved, ref resolvedReferenceCount);

        var ownerCharacterId = site == null ? string.Empty : DocumentString(site, "OwnerId");
        var ownerUserId = site == null ? string.Empty : DocumentString(site, "OwnerUserId");
        ResolveReference(ownerships.Any(x =>
                DocumentString(x, "CharacterId") == ownerCharacterId &&
                DocumentString(x, "OwnerUserId") == ownerUserId),
            "owner reference", unresolved, ref resolvedReferenceCount);

        var locationId = site == null ? string.Empty : DocumentString(site, "LocationId");
        var targetLocationExists = !string.IsNullOrWhiteSpace(locationId) &&
            _mongo.Database.GetCollection<BsonDocument>("map_space_nodes")
                .Find(Builders<BsonDocument>.Filter.Eq("_id", locationId))
                .Limit(1)
                .Any();
        ResolveReference(targetLocationExists, "location reference", unresolved, ref resolvedReferenceCount);

        if (duplicates.Count > 0 || unresolved.Count > 0)
        {
            var details = duplicates.Concat(unresolved).ToArray();
            throw new ArgumentException("Campaign construction import dry-run failed: " + string.Join("; ", details));
        }

        plan["focusProjectId"] = focusProjectId;
        plan["validatedCollections"] = focusedRows.Keys.ToArray();
        plan["resolvedReferenceCount"] = resolvedReferenceCount;
        plan["unresolvedReferences"] = unresolved.ToArray();
        plan["duplicateIdentityConflicts"] = duplicates.ToArray();
        plan["expectedCounts"] = expectedCounts;
        plan["plannedCounts"] = plannedCounts;
        plan["assetClassifiedAsInventoryItem"] = false;
        plan["referenceSources"] = new[]
        {
            "package:asset_configuration_blueprints",
            "package:character_ownerships",
            "target-readonly:map_space_nodes"
        };
        plan["competingCollections"] = Array.Empty<string>();
        return plan;
    }

    private Dictionary<string, object> BuildCampaignContextImportDryRunPlan02110(
        Dictionary<string, object> plan,
        DataPackageValidationResult validation)
    {
        var campaigns = ReadPackageDocuments(validation, "campaigns");
        var memberships = ReadPackageDocuments(validation, "campaign_memberships");
        var sessions = ReadPackageDocuments(validation, "current_sessions");
        var participations = ReadPackageDocuments(validation, "session_participations");
        var ownerships = ReadPackageDocuments(validation, "character_ownerships");
        var policies = ReadPackageDocuments(validation, "automation_policy_definitions");
        var executions = ReadPackageDocuments(validation, "automation_execution_records");
        var unresolved = new List<string>();
        var resolved = 0;

        var campaignIds = new HashSet<string>(campaigns.Select(DocumentId), StringComparer.OrdinalIgnoreCase);
        var sessionIds = new HashSet<string>(sessions.Select(x => DataPortabilityFirstNonEmpty(DocumentString(x, "SessionId"), DocumentId(x))), StringComparer.OrdinalIgnoreCase);
        var policyIds = new HashSet<string>(policies.Select(DocumentId), StringComparer.OrdinalIgnoreCase);
        var membershipKeys = new HashSet<string>(memberships.Select(x => DocumentString(x, "CampaignId") + ":" + DocumentString(x, "UserId")), StringComparer.OrdinalIgnoreCase);
        var ownershipByCampaignCharacter = new HashSet<string>(ownerships.Select(x => DocumentString(x, "CampaignId") + ":" + DocumentString(x, "CharacterId")), StringComparer.OrdinalIgnoreCase);

        foreach (var membership in memberships)
            ResolveReference(campaignIds.Contains(DocumentString(membership, "CampaignId")), "membership -> campaign: " + DocumentId(membership), unresolved, ref resolved);
        foreach (var session in sessions)
            ResolveReference(campaignIds.Contains(DocumentString(session, "CampaignId")), "session -> campaign: " + DocumentId(session), unresolved, ref resolved);
        foreach (var participation in participations)
        {
            var campaignId = DocumentString(participation, "CampaignId");
            var userId = DocumentString(participation, "UserId");
            ResolveReference(sessionIds.Contains(DocumentString(participation, "SessionId")), "participation -> session: " + DocumentId(participation), unresolved, ref resolved);
            ResolveReference(membershipKeys.Contains(campaignId + ":" + userId), "participation -> membership: " + DocumentId(participation), unresolved, ref resolved);
            foreach (var characterId in DocumentStringArray(participation, "AllowedCharacterIds"))
                ResolveReference(ownershipByCampaignCharacter.Contains(campaignId + ":" + characterId), "participation -> character ownership: " + characterId, unresolved, ref resolved);
        }
        foreach (var ownership in ownerships)
        {
            var campaignId = DocumentString(ownership, "CampaignId");
            foreach (var userId in new[] { DocumentString(ownership, "OwnerUserId"), DocumentString(ownership, "ControlledByUserId") }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                ResolveReference(membershipKeys.Contains(campaignId + ":" + userId), "character ownership -> membership: " + DocumentId(ownership) + ":" + userId, unresolved, ref resolved);
        }
        foreach (var policy in policies)
            ResolveReference(campaignIds.Contains(DocumentString(policy, "CampaignId")), "automation policy -> campaign: " + DocumentId(policy), unresolved, ref resolved);
        foreach (var execution in executions)
        {
            ResolveReference(policyIds.Contains(DocumentString(execution, "PolicyId")), "automation execution -> policy: " + DocumentId(execution), unresolved, ref resolved);
            ResolveReference(sessionIds.Contains(DocumentString(execution, "SessionId")), "automation execution -> session: " + DocumentId(execution), unresolved, ref resolved);
        }

        if (unresolved.Count > 0)
            throw new ArgumentException("Campaign context import dry-run failed: " + string.Join("; ", unresolved));

        plan["resolvedReferenceCount"] = resolved;
        plan["unresolvedReferences"] = unresolved.ToArray();
        plan["membershipOwnershipMappings"] = memberships.Select(membership =>
        {
            var campaignId = DocumentString(membership, "CampaignId");
            var userId = DocumentString(membership, "UserId");
            return (object)new Dictionary<string, object>
            {
                ["campaignId"] = campaignId,
                ["userId"] = userId,
                ["roleId"] = DocumentString(membership, "PrimaryRoleId"),
                ["ownedCharacterIds"] = ownerships
                    .Where(x => DocumentString(x, "CampaignId") == campaignId && DocumentString(x, "OwnerUserId") == userId)
                    .Select(x => DocumentString(x, "CharacterId")).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
            };
        }).ToArray();
        plan["sessionParticipationCount"] = participations.Count;
        plan["automationPolicyCount"] = policies.Count;
        plan["automationExecutionCount"] = executions.Count;
        plan["activeGameContextExported"] = false;
        plan["activeGameContextContract"] = "Excluded: connection/user preference state is reconstructed after authentication.";
        return plan;
    }

    private Dictionary<string, object> BuildWeatherTravelImportDryRunPlan0217(
        Dictionary<string, object> plan,
        DataPackageValidationResult validation,
        string focusWeatherId,
        string focusTravelId)
    {
        var weatherRows = ReadPackageDocuments(validation, "weather_states");
        var travelRows = ReadPackageDocuments(validation, "travel_sessions");
        var toleranceRows = ReadPackageDocuments(validation, "environmental_tolerance_profiles");
        var instrumentRows = ReadPackageDocuments(validation, "measurement_instrument_profiles");
        var protectionRows = ReadPackageDocuments(validation, "environmental_protection_profiles");
        var observationRows = ReadPackageDocuments(validation, "environment_observations");

        var selectedWeather = string.IsNullOrWhiteSpace(focusWeatherId)
            ? weatherRows
            : weatherRows.Where(x => DocumentId(x) == focusWeatherId).ToList();
        var selectedTravel = string.IsNullOrWhiteSpace(focusTravelId)
            ? travelRows
            : travelRows.Where(x => DocumentId(x) == focusTravelId).ToList();

        var unresolved = new List<string>();
        var duplicates = new List<string>();
        var resolvedReferenceCount = 0;

        duplicates.AddRange(selectedWeather.GroupBy(DocumentId, StringComparer.OrdinalIgnoreCase)
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
            .Select(x => "weather_states:" + x.Key));
        duplicates.AddRange(selectedTravel.GroupBy(DocumentId, StringComparer.OrdinalIgnoreCase)
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
            .Select(x => "travel_sessions:" + x.Key));
        duplicates.AddRange(observationRows.GroupBy(DocumentId, StringComparer.OrdinalIgnoreCase)
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
            .Select(x => "environment_observations:" + x.Key));

        foreach (var weather in selectedWeather)
        {
            var schemaVersion = int.TryParse(DocumentString(weather, "WindUnitSchemaVersion"), out var parsed) ? parsed : 1;
            if (schemaVersion < 2 && string.IsNullOrWhiteSpace(DocumentString(weather, "TrueWindKmh")))
                unresolved.Add("legacy weather wind unit cannot be normalized");
        }
        foreach (var observation in observationRows)
        {
            ResolveReference(toleranceRows.Count > 0, "environment tolerance profiles", unresolved, ref resolvedReferenceCount);
            var instrumentProfileId = DocumentString(observation, "InstrumentProfileId");
            if (!string.IsNullOrWhiteSpace(instrumentProfileId))
                ResolveReference(instrumentRows.Any(x => DocumentId(x) == instrumentProfileId), "measurement instrument profile", unresolved, ref resolvedReferenceCount);
        }

        if (!string.IsNullOrWhiteSpace(focusWeatherId))
            ResolveReference(selectedWeather.Count == 1, "focused weather state", unresolved, ref resolvedReferenceCount);
        if (!string.IsNullOrWhiteSpace(focusTravelId))
            ResolveReference(selectedTravel.Count == 1, "focused travel session", unresolved, ref resolvedReferenceCount);

        bool DefinitionExists(string id) => !string.IsNullOrWhiteSpace(id) &&
            _mongo.Database.GetCollection<BsonDocument>("unified_definitions")
                .Find(Builders<BsonDocument>.Filter.Eq("_id", id)).Limit(1).Any();

        foreach (var weather in selectedWeather)
        {
            ResolveReference(DocumentString(weather, "RandomAlgorithmId") == WeatherDeterministicRandom.AlgorithmId,
                "supported weather RNG algorithm", unresolved, ref resolvedReferenceCount);
            ResolveReference(DocumentString(weather, "RandomAlgorithmVersion") == WeatherDeterministicRandom.AlgorithmVersion.ToString(),
                "supported weather RNG version", unresolved, ref resolvedReferenceCount);
            ResolveReference(DefinitionExists(DocumentString(weather, "ClimateProfileId")),
                "weather climate profile", unresolved, ref resolvedReferenceCount);
            ResolveReference(DefinitionExists(DocumentString(weather, "CurrentPatternId")),
                "weather pattern", unresolved, ref resolvedReferenceCount);
        }

        foreach (var travel in selectedTravel)
        {
            ResolveReference(DefinitionExists(DocumentString(travel, "ModeDefinitionId")),
                "travel mode", unresolved, ref resolvedReferenceCount);
            var segments = travel.GetValue("Segments", new BsonArray()).AsBsonArray;
            ResolveReference(segments.Count > 0, "travel segments", unresolved, ref resolvedReferenceCount);
            foreach (var segmentValue in segments)
            {
                if (!segmentValue.IsBsonDocument) continue;
                var segment = segmentValue.AsBsonDocument;
                var terrainId = DocumentString(segment, "TerrainProfileId");
                ResolveReference(DefinitionExists(terrainId), "segment terrain profile", unresolved, ref resolvedReferenceCount);
                ResolveReference(!string.IsNullOrWhiteSpace(DocumentString(segment, "FromLocationId")),
                    "segment origin", unresolved, ref resolvedReferenceCount);
                ResolveReference(!string.IsNullOrWhiteSpace(DocumentString(segment, "ToLocationId")),
                    "segment destination", unresolved, ref resolvedReferenceCount);
            }
        }

        if (duplicates.Count > 0 || unresolved.Count > 0)
            throw new ArgumentException("Weather/travel import dry-run failed: " + string.Join("; ", duplicates.Concat(unresolved)));

        plan["focusWeatherId"] = focusWeatherId;
        plan["focusTravelId"] = focusTravelId;
        plan["validatedCollections"] = new[] { "weather_states", "travel_sessions", "environmental_tolerance_profiles", "measurement_instrument_profiles", "environmental_protection_profiles", "environment_observations" };
        plan["resolvedReferenceCount"] = resolvedReferenceCount;
        plan["unresolvedReferences"] = unresolved.ToArray();
        plan["duplicateIdentityConflicts"] = duplicates.ToArray();
        plan["expectedCounts"] = new Dictionary<string, object>
        {
            ["weather_states"] = selectedWeather.Count,
            ["travel_sessions"] = selectedTravel.Count,
            ["environmental_tolerance_profiles"] = toleranceRows.Count,
            ["measurement_instrument_profiles"] = instrumentRows.Count,
            ["environmental_protection_profiles"] = protectionRows.Count,
            ["environment_observations"] = observationRows.Count
        };
        plan["plannedCounts"] = new Dictionary<string, object>
        {
            ["weather_states"] = selectedWeather.Count,
            ["travel_sessions"] = selectedTravel.Count,
            ["environmental_tolerance_profiles"] = toleranceRows.Count,
            ["measurement_instrument_profiles"] = instrumentRows.Count,
            ["environmental_protection_profiles"] = protectionRows.Count,
            ["environment_observations"] = observationRows.Count
        };
        plan["supportedRandomAlgorithm"] = WeatherDeterministicRandom.AlgorithmId;
        plan["supportedRandomAlgorithmVersion"] = WeatherDeterministicRandom.AlgorithmVersion;
        plan["windUnitSchemaVersion"] = 2;
        plan["legacyWindKmhConversionPlanned"] = selectedWeather.Any(x => (int.TryParse(DocumentString(x, "WindUnitSchemaVersion"), out var version) ? version : 1) < 2);
        plan["competingCollections"] = Array.Empty<string>();
        return plan;
    }

    private Dictionary<string, object> BuildMapImportDryRunPlan0201(
        Dictionary<string, object> plan,
        DataPackageValidationResult validation,
        string focusMapId)
    {
        var rootCollections = new[]
        {
            "map_states",
            "map_coordinate_profiles",
            "map_scale_profiles",
            "world_map_states",
            "scene_map_definitions",
            "world_map_definitions"
        };
        var dependentCollections = new[]
        {
            "map_identity_mappings",
            "map_semantic_layers",
            "map_semantic_features",
            "map_portals",
            "map_generator_recipes",
            "map_generation_jobs",
            "map_markers",
            "map_marker_bindings",
            "map_fog_layers",
            "map_scene_active_links",
            "world_map_layers",
            "world_map_legends",
            "world_map_profiles",
            "world_map_regions",
            "world_map_locations",
            "world_map_labels",
            "world_map_markers",
            "session_world_map_states",
            "scene_map_markers",
            "session_scene_map_states",
            "map_token_instances",
            "map_token_move_operations",
            "scene_map_layers",
            "scene_map_shapes",
            "scene_map_tile_layers",
            "scene_map_tile_patches",
            "scene_map_asset_instances",
            "scene_map_generation_runs"
        };

        var roots = rootCollections.ToDictionary(
            name => name,
            name => ReadPackageDocuments(validation, name)
                .Where(x => DocumentId(x) == focusMapId || DocumentString(x, "MapId") == focusMapId)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
        var related = dependentCollections.ToDictionary(
            name => name,
            name => ReadPackageDocuments(validation, name)
                .Where(x => MapImportDocumentReferences0201(x, focusMapId))
                .ToList(),
            StringComparer.OrdinalIgnoreCase);

        var rootCount = roots.Sum(x => x.Value.Count);
        var unresolved = new List<string>();
        var duplicates = new List<string>();
        if (rootCount == 0)
            unresolved.Add("canonical map root not found in export package");

        foreach (var pair in roots.Concat(related))
        {
            duplicates.AddRange(pair.Value
                .GroupBy(DocumentId, StringComparer.OrdinalIgnoreCase)
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
                .Select(x => pair.Key + ":" + x.Key));
        }

        if (duplicates.Count > 0 || unresolved.Count > 0)
            throw new ArgumentException("Campaign map import dry-run failed: " + string.Join("; ", duplicates.Concat(unresolved)));

        var plannedCounts = roots.Concat(related)
            .Where(x => x.Value.Count > 0)
            .ToDictionary(x => x.Key, x => (object)x.Value.Count, StringComparer.OrdinalIgnoreCase);
        plan["focusMapId"] = focusMapId;
        plan["validatedCollections"] = plannedCounts.Keys.ToArray();
        plan["resolvedReferenceCount"] = related.Sum(x => x.Value.Count);
        plan["unresolvedReferences"] = unresolved.ToArray();
        plan["duplicateIdentityConflicts"] = duplicates.ToArray();
        plan["plannedCounts"] = plannedCounts;
        plan["canonicalRootCount"] = rootCount;
        plan["competingCollections"] = roots.Where(x => x.Value.Count > 0).Select(x => x.Key).ToArray();
        plan["referenceRule"] = "Every dependent row must reference focusMapId through a canonical map reference field.";
        return plan;
    }

    private static bool MapImportDocumentReferences0201(BsonDocument document, string mapId)
    {
        var fields = new[]
        {
            "MapId", "CanonicalMapId", "LegacyMapId", "WorldMapId", "SceneMapId", "ParentSceneMapId", "ActiveSceneMapId", "ActiveWorldMapId"
        };
        return fields.Any(field => string.Equals(DocumentString(document, field), mapId, StringComparison.OrdinalIgnoreCase));
    }

    private Dictionary<string, object> BuildAssetMaintenanceImportDryRunPlan0198(
        Dictionary<string, object> plan,
        DataPackageValidationResult validation,
        string focusProjectId,
        BsonDocument project)
    {
        var operations = ReadPackageDocuments(validation, "asset_operation_states");
        var reservations = ReadPackageDocuments(validation, "asset_maintenance_reservations").Where(x => DocumentString(x, "ProjectId") == focusProjectId).ToList();
        var consumptions = ReadPackageDocuments(validation, "asset_maintenance_stage_consumptions").Where(x => DocumentString(x, "ProjectId") == focusProjectId).ToList();
        var records = ReadPackageDocuments(validation, "maintenance_service_records").Where(x => DocumentString(x, "ProjectId") == focusProjectId).ToList();
        var assets = ReadPackageDocuments(validation, "asset_states");
        var profiles = ReadPackageDocuments(validation, "large_asset_maintenance_profiles");
        var ownerships = ReadPackageDocuments(validation, "character_ownerships");
        var locations = ReadPackageDocuments(validation, "map_space_nodes");
        var licenses = ReadPackageDocuments(validation, "legal_entity_licenses");
        var inventoryProfiles = ReadPackageDocuments(validation, "character_inventory_profiles");

        var snapshot = project.GetValue("DefinitionSnapshot", new BsonDocument()).AsBsonDocument
            .GetValue("AssetMaintenance", new BsonDocument()).AsBsonDocument;
        var assetId = DocumentString(snapshot, "AssetId");
        var assetRows = assets.Where(x => DocumentId(x) == assetId).ToList();
        var operationRows = operations.Where(x => DocumentString(x, "AssetId") == assetId).ToList();
        var profileRows = profiles.Where(x => DocumentString(x, "AssetInstanceId") == assetId).ToList();
        var serviceRows = records.Where(x => DocumentString(x, "AssetId") == assetId).ToList();

        var expectedCounts = new Dictionary<string, object>
        {
            ["project_base_states"] = 1,
            ["asset_states"] = 1,
            ["large_asset_maintenance_profiles"] = 1,
            ["asset_operation_states"] = 1,
            ["asset_maintenance_reservations"] = 3,
            ["asset_maintenance_stage_consumptions"] = 3,
            ["maintenance_service_records"] = 1
        };
        var focusedRows = new Dictionary<string, List<BsonDocument>>(StringComparer.OrdinalIgnoreCase)
        {
            ["project_base_states"] = new List<BsonDocument> { project },
            ["asset_states"] = assetRows,
            ["large_asset_maintenance_profiles"] = profileRows,
            ["asset_operation_states"] = operationRows,
            ["asset_maintenance_reservations"] = reservations,
            ["asset_maintenance_stage_consumptions"] = consumptions,
            ["maintenance_service_records"] = serviceRows
        };
        var plannedCounts = focusedRows.ToDictionary(x => x.Key, x => (object)x.Value.Count, StringComparer.OrdinalIgnoreCase);
        var unresolved = new List<string>();
        var duplicates = new List<string>();
        var resolved = 0;
        foreach (var pair in focusedRows)
        {
            if (pair.Value.Count != Convert.ToInt32(expectedCounts[pair.Key])) unresolved.Add($"{pair.Key}: expected {expectedCounts[pair.Key]}, found {pair.Value.Count}");
            duplicates.AddRange(pair.Value.GroupBy(DocumentId, StringComparer.OrdinalIgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1).Select(x => pair.Key + ":" + x.Key));
        }
        var asset = assetRows.SingleOrDefault();
        var profile = profileRows.SingleOrDefault();
        var operation = operationRows.SingleOrDefault();
        var record = serviceRows.SingleOrDefault();
        ResolveReference(string.Equals(DocumentString(project, "Status"), ProjectStatusIds.Completed, StringComparison.OrdinalIgnoreCase), "maintenance project completed", unresolved, ref resolved);
        ResolveReference(asset != null && !string.Equals(DocumentString(asset, "AssetType"), "inventory_item", StringComparison.OrdinalIgnoreCase), "asset is not inventory item", unresolved, ref resolved);
        ResolveReference(asset != null && !PackageDocumentsContainId(inventoryProfiles, assetId), "asset absent from inventory", unresolved, ref resolved);
        ResolveReference(profile != null && DocumentString(profile, "AssetInstanceId") == assetId, "profile -> asset", unresolved, ref resolved);
        ResolveReference(operation != null && DocumentString(operation, "AssetId") == assetId, "operation -> asset", unresolved, ref resolved);
        ResolveReference(record != null && DocumentString(record, "AssetId") == assetId && DocumentString(record, "ProjectId") == focusProjectId, "service record references", unresolved, ref resolved);
        ResolveReference(reservations.All(x => DocumentString(x, "AssetId") == assetId), "reservations -> asset", unresolved, ref resolved);
        ResolveReference(consumptions.All(x => DocumentString(x, "AssetId") == assetId), "consumptions -> asset", unresolved, ref resolved);
        var ownerId = DocumentString(snapshot, "OwnerId");
        ResolveReference(ownerships.Any(x => DocumentString(x, "CharacterId") == ownerId), "owner reference", unresolved, ref resolved);
        ResolveReference(locations.Any(x => DocumentId(x) == DocumentString(snapshot, "LocationId")), "location reference", unresolved, ref resolved);
        var specialistId = DocumentString(snapshot, "SpecialistReferenceId");
        ResolveReference(ownerships.Any(x => DocumentString(x, "CharacterId") == specialistId), "specialist NPC reference", unresolved, ref resolved);
        var licenseIds = snapshot.GetValue("LicenseDocumentReferences", new BsonArray()).AsBsonArray.Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        ResolveReference(licenseIds.Length > 0 && licenseIds.All(id => licenses.Any(x => DocumentId(x) == id)), "issued-license references", unresolved, ref resolved);
        if (duplicates.Count > 0 || unresolved.Count > 0)
            throw new ArgumentException("Campaign maintenance import dry-run failed: " + string.Join("; ", duplicates.Concat(unresolved)));

        plan["focusProjectId"] = focusProjectId;
        plan["validatedCollections"] = focusedRows.Keys.ToArray();
        plan["resolvedReferenceCount"] = resolved;
        plan["unresolvedReferences"] = unresolved.ToArray();
        plan["duplicateIdentityConflicts"] = duplicates.ToArray();
        plan["expectedCounts"] = expectedCounts;
        plan["plannedCounts"] = plannedCounts;
        plan["assetIdUnchanged"] = true;
        plan["assetClassifiedAsInventoryItem"] = false;
        plan["competingCollections"] = Array.Empty<string>();
        return plan;
    }

    private List<BsonDocument> ReadPackageDocuments(DataPackageValidationResult validation, string collectionName)
    {
        var manifestEntry = ManifestCollections(validation.Manifest).FirstOrDefault(x =>
            string.Equals(x.GetValue("collectionName", "").ToString(), collectionName, StringComparison.OrdinalIgnoreCase));
        if (manifestEntry == null)
            throw new ArgumentException("Campaign package is missing required collection: " + collectionName);

        var fileName = manifestEntry.GetValue("fileName", "").ToString();
        var filePath = Path.GetFullPath(Path.Combine(validation.PackagePath, fileName));
        return File.ReadLines(filePath, Encoding.UTF8)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(BsonDocument.Parse)
            .ToList();
    }

    private static string DocumentId(BsonDocument document)
        => document.GetValue("_id", document.GetValue("Id", document.GetValue("id", ""))).ToString();

    private static string DocumentString(BsonDocument document, string field)
        => document.GetValue(field, "").ToString();

    private static string[] DocumentStringArray(BsonDocument document, string field)
        => document.TryGetValue(field, out var value) && value.IsBsonArray
            ? value.AsBsonArray.Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
            : Array.Empty<string>();

    private static bool PackageDocumentsContainId(IEnumerable<BsonDocument> documents, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return documents.Any(x => x.ToJson().IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void ResolveReference(
        bool resolved,
        string label,
        ICollection<string> unresolved,
        ref int resolvedReferenceCount)
    {
        if (resolved)
        {
            resolvedReferenceCount++;
            return;
        }
        unresolved.Add(label);
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
