using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Application.Services;
using Nri.Server.Content;
using Nri.Server.FateEngine;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private readonly INriRepositoryFactory _repositories;
    private readonly MongoContext _mongo;
    private readonly Nri.Shared.Configuration.ServerConfig _serverConfig;
    private readonly SessionManager _sessionManager;
    private readonly IServerLogger _logger;
    private readonly FateEngineStateService _fateState;
    private readonly FateEngineSettingsStore _fateSettingsStore;
    private readonly GameContentService _contentService;
    private readonly string _audioFolderPath;
    private readonly SyncEventService _syncEvents;
    private readonly IVisibilityService _visibilityService;
    private readonly ICharacterProfileShadowWriteService _profileShadowWriteService;
    private readonly ICharacterProfileConsistencyService _profileConsistencyService;
    private readonly ICharacterDetailsProfileBuilder _characterDetailsProfileBuilder;
    private readonly ICharacterProfileCreationService _characterProfileCreationService;
    private readonly ICharacterProfileNativeWriteService _profileNativeWriteService;
    private readonly IInventoryDiagnosticsService? _inventoryDiagnosticsService;
    private readonly ICombatEncounterManagementService? _combatEncounterManagementService;
    private readonly ICombatTurnEngineService? _combatTurnEngineService;
    private readonly ICombatLogReadService? _combatLogReadService;
    private readonly ICombatSnapshotService? _combatSnapshotService;
    private readonly ICombatDiagnosticsService? _combatDiagnosticsService;
    private readonly ICombatActionEconomyService? _combatActionEconomyService;
    private readonly ICombatAttackRollService? _combatAttackRollService;
    private readonly ICombatDefenseCalculationService? _combatDefenseCalculationService;
    private readonly ICombatDamageApplicationService? _combatDamageApplicationService;
    private readonly ICombatConditionService? _combatConditionService;
    private readonly ICombatWeaponIntegrationService? _combatWeaponIntegrationService;
    private readonly ICombatFateHookService? _combatFateHookService;
    private readonly ICombatMvpSmokeService? _combatMvpSmokeService;
    private readonly ICombatConditionPresentationResolver? _combatConditionPresentationResolver;
    private readonly IFeatureFlagProvider _featureFlags;
    private readonly IMapIdentityResolver _mapIdentityResolver;
    private readonly IMapEditorMutationService _mapEditorMutationService;
    private readonly IPlayerMapProjectionService _playerMapProjectionService;
    private readonly IMapGenerationService _mapGenerationService;
    private readonly ICampaignAuthorizationService _campaignAuthorization;

    public ServiceHub(INriRepositoryFactory repositories, MongoContext mongo, Nri.Shared.Configuration.ServerConfig serverConfig, SessionManager sessionManager, IServerLogger logger, FateEngineStateService fateState, FateEngineSettingsStore fateSettingsStore, GameContentService contentService, string audioFolderPath, SyncEventService syncEvents, IVisibilityService visibilityService, ICharacterProfileShadowWriteService profileShadowWriteService, ICharacterProfileConsistencyService profileConsistencyService, ICharacterDetailsProfileBuilder characterDetailsProfileBuilder, ICharacterProfileCreationService characterProfileCreationService, ICharacterProfileNativeWriteService profileNativeWriteService, IInventoryDiagnosticsService? inventoryDiagnosticsService = null, ICombatEncounterManagementService? combatEncounterManagementService = null, ICombatTurnEngineService? combatTurnEngineService = null, ICombatLogReadService? combatLogReadService = null, ICombatSnapshotService? combatSnapshotService = null, ICombatDiagnosticsService? combatDiagnosticsService = null, ICombatActionEconomyService? combatActionEconomyService = null, ICombatAttackRollService? combatAttackRollService = null, ICombatDefenseCalculationService? combatDefenseCalculationService = null, ICombatDamageApplicationService? combatDamageApplicationService = null, ICombatConditionService? combatConditionService = null, ICombatWeaponIntegrationService? combatWeaponIntegrationService = null, ICombatFateHookService? combatFateHookService = null, ICombatMvpSmokeService? combatMvpSmokeService = null, IFeatureFlagProvider? featureFlags = null, ICombatConditionPresentationResolver? combatConditionPresentationResolver = null)
    {
        _repositories = repositories;
        _mongo = mongo;
        _serverConfig = serverConfig;
        _sessionManager = sessionManager;
        _logger = logger;
        _fateState = fateState;
        _fateSettingsStore = fateSettingsStore;
        _contentService = contentService;
        _audioFolderPath = string.IsNullOrWhiteSpace(audioFolderPath) ? "./audio" : audioFolderPath;
        _syncEvents = syncEvents;
        _visibilityService = visibilityService;
        _profileShadowWriteService = profileShadowWriteService;
        _profileConsistencyService = profileConsistencyService;
        _characterDetailsProfileBuilder = characterDetailsProfileBuilder;
        _characterProfileCreationService = characterProfileCreationService;
        _profileNativeWriteService = profileNativeWriteService;
        _inventoryDiagnosticsService = inventoryDiagnosticsService;
        _combatEncounterManagementService = combatEncounterManagementService;
        _combatTurnEngineService = combatTurnEngineService;
        _combatLogReadService = combatLogReadService;
        _combatSnapshotService = combatSnapshotService;
        _combatDiagnosticsService = combatDiagnosticsService;
        _combatActionEconomyService = combatActionEconomyService;
        _combatAttackRollService = combatAttackRollService;
        _combatDefenseCalculationService = combatDefenseCalculationService;
        _combatDamageApplicationService = combatDamageApplicationService;
        _combatConditionService = combatConditionService;
        _combatWeaponIntegrationService = combatWeaponIntegrationService;
        _combatFateHookService = combatFateHookService;
        _combatMvpSmokeService = combatMvpSmokeService;
        _combatConditionPresentationResolver = combatConditionPresentationResolver;
        _featureFlags = featureFlags ?? new RuntimeFeatureFlagProvider(new Nri.Shared.Configuration.ServerConfig(), logger);
        _mapIdentityResolver = new MapIdentityAdapter0202(mongo);
        _mapEditorMutationService = new MapEditorMutationService0203(mongo, _mapIdentityResolver);
        _playerMapProjectionService = new PlayerMapProjectionService0204(repositories, mongo, _mapIdentityResolver);
        _mapGenerationService = new MapGenerationService0205(mongo, _mapIdentityResolver);
        _campaignAuthorization = new CampaignAuthorizationService02110(repositories);
    }

    public ResponseEnvelope Register(CommandContext context)
    {
        var login = RequireLength(PayloadReader.GetString(context.Request.Payload, "login"), 3, 64, "login");
        var password = RequireLength(PayloadReader.GetString(context.Request.Payload, "password"), 6, 128, "password");
        _logger.Admin($"auth.register.requested login={login}");

        var existing = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, login)).FirstOrDefault();
        if (existing != null) throw new InvalidOperationException("Login already exists.");

        var profile = new UserProfile();
        _repositories.Profiles.Insert(profile);

        var salt = PasswordHasher.CreateSalt();
        var account = new UserAccount
        {
            Login = login,
            PasswordSalt = salt,
            PasswordHash = PasswordHasher.Hash(password, salt),
            ProfileId = profile.Id,
            Status = AccountStatus.PendingApproval
        };
        _repositories.Accounts.Insert(account);
        profile.UserAccountId = account.Id;
        _repositories.Profiles.Replace(profile);

        WriteAudit("auth", account.Id, "register", account.Id);
        _logger.Admin($"auth.register.createdPending login={login} accountId={account.Id}");
        return Ok("Registration submitted. Account is pending admin approval.", new Dictionary<string, object> { { "accountId", account.Id }, { "status", account.Status.ToString() } });
    }

    public ResponseEnvelope Login(CommandContext context)
    {
        var login = RequireLength(PayloadReader.GetString(context.Request.Payload, "login"), 3, 64, "login");
        var password = RequireLength(PayloadReader.GetString(context.Request.Payload, "password"), 6, 128, "password");

        var account = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, login)).FirstOrDefault();
        if (account == null || PasswordHasher.Hash(password, account.PasswordSalt) != account.PasswordHash)
        {
            _logger.Admin($"auth.login.denied login={login} reason=invalid_credentials");
            throw new UnauthorizedAccessException("Invalid credentials.");
        }
        if (account.Status == AccountStatus.PendingApproval)
        {
            _logger.Admin($"auth.login.denied login={login} reason=pending_approval");
            throw new UnauthorizedAccessException("Account is pending admin approval.");
        }
        if (account.Status == AccountStatus.Blocked || account.Status == AccountStatus.Archived)
        {
            _logger.Admin($"auth.login.denied login={login} reason=status_{account.Status}");
            throw new UnauthorizedAccessException($"Account status '{account.Status}' disallows login.");
        }

        account.LastLoginUtc = DateTime.UtcNow;
        _repositories.Accounts.Replace(account);

        var token = _sessionManager.CreateSession(account.Id, context.ConnectionId);
        WriteAudit("auth", account.Id, "login", account.Id);
        PublishSystemMessage("default", $"{account.Login} connected.");
        return Ok("Login success.", new Dictionary<string, object>
        {
            { "authToken", token },
            { "accountId", account.Id },
            { "status", account.Status.ToString() },
            { "roles", account.Roles.Select(x => x.ToString()).ToArray() }
        });
    }

    public ResponseEnvelope AuthChangePassword(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var oldPassword = RequireLength(PayloadReader.GetString(context.Request.Payload, "oldPassword"), 6, 128, "oldPassword");
        var newPassword = RequireLength(PayloadReader.GetString(context.Request.Payload, "newPassword"), 8, 128, "newPassword");
        if (string.Equals(oldPassword, newPassword, StringComparison.Ordinal))
            throw new ArgumentException("New password must be different from old password.");
        if (PasswordHasher.Hash(oldPassword, actor.PasswordSalt) != actor.PasswordHash)
            throw new UnauthorizedAccessException("Old password is invalid.");

        var salt = PasswordHasher.CreateSalt();
        actor.PasswordSalt = salt;
        actor.PasswordHash = PasswordHasher.Hash(newPassword, salt);
        _repositories.Accounts.Replace(actor);
        WriteAudit("auth", actor.Id, "changePassword", actor.Id);
        _logger.Admin($"auth.changePassword actor={actor.Login} result=ok");
        return Ok("Password changed.");
    }

    public ResponseEnvelope Logout(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        _sessionManager.Logout(context.Request.AuthToken);
        PublishSystemMessage("default", $"{actor.Login} disconnected.");
        return Ok("Logout success.");
    }

    public ResponseEnvelope SessionValidate(CommandContext context)
    {
        var account = GetCurrentAccount(context);
        return Ok("Session is valid.", new Dictionary<string, object>
        {
            { "userId", account.Id },
            { "status", account.Status.ToString() },
            { "roles", account.Roles.Select(x => x.ToString()).ToArray() }
        });
    }

    public ResponseEnvelope ProfileGet(CommandContext context)
    {
        var account = GetCurrentAccount(context);
        return Ok("Profile loaded.", ProfilePayload(GetProfile(account.ProfileId)));
    }

    public ResponseEnvelope ProfileUpdate(CommandContext context)
    {
        var account = GetCurrentAccount(context);
        if (account.Status == AccountStatus.Blocked || account.Status == AccountStatus.Archived)
            throw new UnauthorizedAccessException("Account is not allowed to update profile.");

        var profile = GetProfile(account.ProfileId);
        profile.DisplayName = RequireLength(PayloadReader.GetString(context.Request.Payload, "displayName"), 2, 64, "displayName");
        profile.Race = RequireLength(PayloadReader.GetString(context.Request.Payload, "race"), 2, 64, "race");
        profile.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
        profile.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
        var age = PayloadReader.GetInt(context.Request.Payload, "age");
        if (age.HasValue && (age.Value < 1 || age.Value > 1000)) throw new ArgumentException("age must be in range 1..1000");
        profile.Age = age;
        _repositories.Profiles.Replace(profile);
        WriteAudit("profile", account.Id, "update", profile.Id);
        return Ok("Profile updated.", ProfilePayload(profile));
    }

    public ResponseEnvelope AdminPendingAccounts(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var items = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Status, AccountStatus.PendingApproval))
            .Select(AccountPayload).Cast<object>().ToArray();
        _logger.Admin($"admin.accounts.pending actor={actor.Login} count={items.Length}");
        return Ok("Pending accounts loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope AdminApproveAccount(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var target = GetAccount(RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId"));
        target.Status = AccountStatus.Active;
        _repositories.Accounts.Replace(target);
        _logger.Admin($"admin.account.approve actor={actor.Login} target={target.Login} result=ok");
        WriteAudit("admin", actor.Id, "approveAccount", target.Id);
        return Ok("Account approved.");
    }

    public ResponseEnvelope AdminArchiveAccount(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var target = GetAccount(RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId"));
        target.Status = AccountStatus.Archived;
        target.Archived = true;
        _repositories.Accounts.Replace(target);
        _logger.Admin($"admin.account.archive actor={actor.Login} target={target.Login} result=ok");
        WriteAudit("admin", actor.Id, "archiveAccount", target.Id);
        return Ok("Account archived.");
    }

    public ResponseEnvelope AdminRejectAccount(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var target = GetAccount(RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId"));
        target.Status = AccountStatus.Archived;
        target.Archived = true;
        _repositories.Accounts.Replace(target);
        _logger.Admin($"admin.account.reject actor={actor.Login} target={target.Login} result=ok");
        WriteAudit("admin", actor.Id, "rejectAccount", target.Id);
        return Ok("Account rejected.");
    }

    public ResponseEnvelope AdminBlockAccount(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var target = GetAccount(RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId"));
        target.Status = AccountStatus.Blocked;
        _repositories.Accounts.Replace(target);
        _logger.Admin($"admin.account.block actor={actor.Login} target={target.Login} result=ok");
        WriteAudit("admin", actor.Id, "blockAccount", target.Id);
        return Ok("Account blocked.");
    }

    public ResponseEnvelope AdminUnblockAccount(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var target = GetAccount(RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId"));
        if (target.Status == AccountStatus.Archived) throw new InvalidOperationException("Archived account cannot be unblocked.");
        target.Status = AccountStatus.Active;
        _repositories.Accounts.Replace(target);
        _logger.Admin($"admin.account.unblock actor={actor.Login} target={target.Login} result=ok");
        WriteAudit("admin", actor.Id, "unblockAccount", target.Id);
        return Ok("Account unblocked.");
    }

    public ResponseEnvelope AdminResetAccountPassword(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var accountId = RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId");
        var newPassword = RequireLength(PayloadReader.GetString(context.Request.Payload, "newPassword"), 8, 128, "newPassword");
        var target = GetAccount(accountId);
        var salt = PasswordHasher.CreateSalt();
        target.PasswordSalt = salt;
        target.PasswordHash = PasswordHasher.Hash(newPassword, salt);
        _repositories.Accounts.Replace(target);
        _logger.Admin($"admin.account.resetPassword actor={actor.Login} target={target.Login} result=ok");
        WriteAudit("admin", actor.Id, "resetPassword", target.Id);
        return Ok("Password reset.");
    }

    public ResponseEnvelope AdminAccountProfile(CommandContext context)
    {
        RequireAdmin(context);
        var target = GetAccount(RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId"));
        return Ok("Account profile loaded.", ProfilePayload(GetProfile(target.ProfileId)));
    }

    public ResponseEnvelope AdminPlayersList(CommandContext context)
    {
        RequireAdmin(context);
        var accounts = _repositories.Accounts.Find(FilterDefinition<UserAccount>.Empty);
        var presence = _repositories.Presence.Find(FilterDefinition<SessionUserState>.Empty).ToDictionary(x => x.UserId, x => x);

        var items = accounts.Select(a =>
        {
            presence.TryGetValue(a.Id, out var p);
            return new Dictionary<string, object>
            {
                { "accountId", a.Id }, { "login", a.Login }, { "status", a.Status.ToString() },
                { "roles", a.Roles.Select(r => r.ToString()).ToArray() },
                { "isOnline", p != null && p.IsOnline },
                { "lastSeenUtc", p != null ? (object)p.LastSeenUtc : string.Empty }
            };
        }).Cast<object>().ToArray();

        return Ok("Players loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CharacterListMine(CommandContext context)
    {
        if (CharacterOwnershipPlayerViewEnabled())
            return CharacterPlayerAssignedList(context);

        var actor = GetCurrentAccount(context);
        var items = _repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.OwnerUserId, actor.Id))
            .Select(c => CharacterDetailsPayloadWithProfileFirst(c, actor, actor, context.Request.RequestId ?? string.Empty))
            .Cast<object>()
            .ToArray();
        return Ok("Characters loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CharacterListByOwner(CommandContext context)
    {
        RequireAdmin(context);
        var ownerId = RequireLength(PayloadReader.GetString(context.Request.Payload, "ownerUserId"), 8, 128, "ownerUserId");
        GetAccount(ownerId);
        var actor = GetCurrentAccount(context);
        var items = _repositories.CharacterOwnerships.Find(FilterDefinition<CharacterOwnershipState>.Empty)
            .Where(x => string.Equals(x.OwnerUserId, ownerId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ControlledByUserId, ownerId, StringComparison.OrdinalIgnoreCase))
            .Select(x => PlayerAssignedCharacterPayload(x, TryGetCharacter(x.CharacterId), actor, context.Request.RequestId ?? string.Empty))
            .Cast<object>()
            .ToArray();
        return Ok("Characters loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CharacterGetActive(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var p = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, actor.Id)).FirstOrDefault();
        if (p == null || string.IsNullOrWhiteSpace(p.ActiveCharacterId)) return Ok("No active character.");
        var c = _repositories.Characters.GetById(p.ActiveCharacterId);
        if (c == null || c.Deleted) return Ok("No active character.");
        return Ok("Active character loaded.", CharacterDetailsPayloadWithProfileFirst(c, actor, actor, context.Request.RequestId ?? string.Empty));
    }

    public ResponseEnvelope CharacterGetSummary(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character summary unavailable.");
        return Ok("Character summary loaded.", CharacterDetailsPayloadWithProfileFirst(c, owner, actor, context.Request.RequestId ?? string.Empty));
    }

    public ResponseEnvelope CharacterGetCompanions(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character companions unavailable.");
        var payload = CharacterDetailsPayloadWithProfileFirst(c, owner, actor, context.Request.RequestId ?? string.Empty);
        var companions = payload.ContainsKey("companions") ? payload["companions"] : Array.Empty<object>();
        if (!IsAdminActor(actor)) companions = PlayerSafeCharacterCollectionPayload(companions);
        _logger.Admin($"character.companions.get count={CountPayloadItems(companions)} actor={actor.Login} admin={IsAdminActor(actor)}");
        return Ok("Companions loaded.", new Dictionary<string, object> { { "companions", companions } });
    }

    public ResponseEnvelope CharacterGetInventory(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character inventory unavailable.");
        var payload = CharacterDetailsPayloadWithProfileFirst(c, owner, actor, context.Request.RequestId ?? string.Empty);
        var inventory = payload.ContainsKey("inventory") ? payload["inventory"] : Array.Empty<object>();
        if (!actor.Roles.Contains(UserRole.Admin) && !actor.Roles.Contains(UserRole.SuperAdmin))
            inventory = PlayerSafeInventoryPayload(inventory);
        _logger.Admin($"character.inventory.get count={CountPayloadItems(inventory)}");
        return Ok("Inventory loaded.", new Dictionary<string, object> { { "inventory", inventory } });
    }

    public ResponseEnvelope CharacterGetReputation(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character reputation unavailable.");
        var payload = CharacterDetailsPayloadWithProfileFirst(c, owner, actor, context.Request.RequestId ?? string.Empty);
        var reputation = payload.ContainsKey("reputation") ? payload["reputation"] : Array.Empty<object>();
        if (!IsAdminActor(actor)) reputation = PlayerSafeCharacterCollectionPayload(reputation);
        _logger.Admin($"character.reputation.get count={CountPayloadItems(reputation)} actor={actor.Login} admin={IsAdminActor(actor)}");
        return Ok("Reputation loaded.", new Dictionary<string, object> { { "reputation", reputation } });
    }

    public ResponseEnvelope CharacterGetHoldings(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character holdings unavailable.");
        var payload = CharacterDetailsPayloadWithProfileFirst(c, owner, actor, context.Request.RequestId ?? string.Empty);
        var holdings = payload.ContainsKey("holdings") ? payload["holdings"] : Array.Empty<object>();
        if (!IsAdminActor(actor)) holdings = PlayerSafeCharacterCollectionPayload(holdings);
        _logger.Admin($"character.holdings.get count={CountPayloadItems(holdings)} actor={actor.Login} admin={IsAdminActor(actor)}");
        return Ok("Holdings loaded.", new Dictionary<string, object> { { "holdings", holdings } });
    }

    public ResponseEnvelope CharacterCreate(CommandContext context)
    {
        return CharacterCreateCore(context, isAdminFlow: false);
    }

    public ResponseEnvelope CharacterAdminCreate(CommandContext context)
    {
        RequireAdmin(context);
        return CharacterCreateCore(context, isAdminFlow: true);
    }

    private ResponseEnvelope CharacterCreateCore(CommandContext context, bool isAdminFlow)
    {
        var actor = GetCurrentAccount(context);
        var flow = isAdminFlow ? "character.admin.create" : "character.create";
        var ownerRaw = PayloadReader.GetString(context.Request.Payload, "ownerUserId");
        var ownerId = isAdminFlow && !string.IsNullOrWhiteSpace(ownerRaw) ? RequireLength(ownerRaw, 8, 128, "ownerUserId") : actor.Id;
        _logger.Admin($"{flow}.start actor={actor.Login} owner={ownerId}");

        try
        {
            if (!string.IsNullOrWhiteSpace(ownerId)) _ = GetAccount(ownerId);
            var character = new Character
            {
                OwnerUserId = ownerId,
                Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name"),
                Race = PayloadReader.GetString(context.Request.Payload, "race") ?? string.Empty,
                Backstory = PayloadReader.GetString(context.Request.Payload, "backstory") ?? string.Empty,
                Description = string.Empty
            };

            character.Stats ??= new CharacterStats();
            character.Wallet ??= new Wallet();
            character.Wallet.EnsureAllDenominations();
            character.Wallet.NormalizeUpward();

            _repositories.Characters.Insert(character);
            WriteAudit("character", actor.Id, "create", character.Id);

            if (IsProfileFirstCharacterCreationEnabled())
            {
                var creation = _characterProfileCreationService.CreateProfileBundleForNewCharacterAsync(character, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
                if (!creation.Success)
                {
                    _logger.Admin($"{flow}.profileFirst.fail actor={actor.Login} owner={ownerId} characterId={character.Id} reason={creation.ErrorMessage}");
                    return Error("Character profile creation failed.", ResponseStatus.Error, ErrorCode.InternalError);
                }
            }
            else
            {
                TryWriteRaceBodyProfileShadowsAsync(character, actor.Id, context.Request.RequestId ?? string.Empty);
            }

            var campaignId = PayloadReader.GetString(context.Request.Payload, "campaignId");
            if (isAdminFlow && !string.IsNullOrWhiteSpace(campaignId))
                _ = GetOrCreateCharacterOwnership(character, actor, campaignId);

            _logger.Admin($"{flow}.success actor={actor.Login} owner={ownerId} characterId={character.Id}");
            return Ok("Character created.", CharacterDetailsPayloadWithProfileFirst(character, GetAccount(ownerId), actor, context.Request.RequestId ?? string.Empty));
        }
        catch (Exception ex)
        {
            _logger.Admin($"{flow}.fail actor={actor.Login} owner={ownerId} reason={ex.GetType().Name}:{ex.Message}");
            throw;
        }
    }

    public ResponseEnvelope CharacterAssignOwner(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var ownerId = RequireLength(PayloadReader.GetString(context.Request.Payload, "ownerUserId"), 8, 128, "ownerUserId");
        _ = GetAccount(ownerId);
        c.OwnerUserId = ownerId;
        _repositories.Characters.Replace(c);
        _logger.Admin($"character.assignOwner actor={actor.Login} characterId={c.Id} owner={ownerId} result=ok");
        WriteAudit("character", actor.Id, "assignOwner", c.Id);
        return Ok("Character owner assigned.");
    }

    public ResponseEnvelope CharacterArchive(CommandContext context) => SetCharacterArchiveState(context, true);
    public ResponseEnvelope CharacterRestore(CommandContext context) => SetCharacterArchiveState(context, false);

    public ResponseEnvelope CharacterTransfer(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        c.OwnerUserId = RequireLength(PayloadReader.GetString(context.Request.Payload, "targetUserId"), 8, 128, "targetUserId");
        _repositories.Characters.Replace(c);
        WriteAudit("character", actor.Id, "transfer", c.Id);
        return Ok("Character transferred.");
    }

    public ResponseEnvelope CharacterAssignActive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var userId = RequireLength(PayloadReader.GetString(context.Request.Payload, "userId"), 8, 128, "userId");
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var c = GetCharacter(characterId);
        var ownership = _repositories.CharacterOwnerships.Find(
            Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        var controlsCharacter = ownership != null
            && (string.Equals(ownership.OwnerUserId, userId, StringComparison.Ordinal)
                || string.Equals(ownership.ControlledByUserId, userId, StringComparison.Ordinal));
        if (!controlsCharacter
            || c.Deleted
            || ownership!.IsArchived
            || !ownership.IsActive
            || string.Equals(ownership.CharacterStatus, CharacterStatusIds.Archived, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ownership.CharacterStatus, CharacterStatusIds.Inactive, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Персонаж недоступен этому пользователю или находится в архиве.");
        if (!_characterDetailsProfileBuilder.CanBuildFromProfilesAsync(characterId).GetAwaiter().GetResult())
            throw new InvalidOperationException("Профили персонажа требуют явной миграции или восстановления.");

        var p = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, userId)).FirstOrDefault() ?? new SessionUserState { UserId = userId };
        if (string.IsNullOrWhiteSpace(p.Id)) _repositories.Presence.Insert(p);
        if (string.Equals(p.ActiveCharacterId, characterId, StringComparison.Ordinal))
            return Ok("Этот персонаж уже назначен активным.", new Dictionary<string, object>
            {
                ["contextRevision"] = p.ContextRevision,
                ["activeCharacterDisplayName"] = string.IsNullOrWhiteSpace(ownership.CharacterDisplayName) ? c.Name : ownership.CharacterDisplayName
            });
        p.ActiveCharacterId = characterId;
        p.ContextRevision++;
        _repositories.Presence.Replace(p);
        WriteAudit("context", actor.Id, "character.assignActive", $"{c.Id}:revision={p.ContextRevision}");
        return Ok("Активный персонаж назначен.", new Dictionary<string, object>
        {
            ["contextRevision"] = p.ContextRevision,
            ["activeCharacterDisplayName"] = string.IsNullOrWhiteSpace(ownership.CharacterDisplayName) ? c.Name : ownership.CharacterDisplayName
        });
    }

    public ResponseEnvelope CharacterSetActive(CommandContext context)
        => ContextCharacterSwitch(context);

    public ResponseEnvelope CharacterUpdateBasicInfo(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        if (IsProfileNativeRaceBodyWriteEnabled())
        {
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "race"), 2, 64, "race");
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 1, 64, "height");
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
            var native = UpdateRaceBodyProfilesNativeForEnabledSections(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty);
            if (native.LegacyFacadeSynced)
            {
                var updated = GetCharacter(characterId);
                updated.Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
                if (!IsProfileNativeRaceOrSpeciesWriteEnabled()) updated.Race = RequireLength(PayloadReader.GetString(context.Request.Payload, "race"), 2, 64, "race");
                if (!IsProfileNativeBodyWriteEnabled())
                {
                    updated.Height = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 1, 64, "height");
                    updated.Age = PayloadReader.GetInt(context.Request.Payload, "age");
                }
                updated.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
                updated.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
                _repositories.Characters.Replace(updated);
                WriteAudit("character", actor.Id, "updateBasic", updated.Id);
                return Ok("Character basic info updated.");
            }

            return Error("Character race/body profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var c = GetCharacter(characterId);
        c.Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
        c.Race = RequireLength(PayloadReader.GetString(context.Request.Payload, "race"), 2, 64, "race");
        c.Height = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 1, 64, "height");
        c.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
        c.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
        c.Age = PayloadReader.GetInt(context.Request.Payload, "age");
        _repositories.Characters.Replace(c);
        TryWriteRaceBodyProfileShadowsAsync(c, actor.Id, context.Request.RequestId ?? string.Empty);
        WriteAudit("character", actor.Id, "updateBasic", c.Id);
        return Ok("Character basic info updated.");
    }

    public ResponseEnvelope CharacterUpdateStats(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        if (IsProfileNativeStatsWriteEnabled())
        {
            var native = _profileNativeWriteService.UpdateAttributeProfileAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                WriteAudit("character", actor.Id, "updateStats", characterId);
                return Ok("Character stats updated.");
            }

            return Error("Character stats profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var c = GetCharacter(characterId);
        c.Stats.Health = RequireRange(PayloadReader.GetInt(context.Request.Payload, "health"), 0, 999, "health");
        c.Stats.PhysicalArmor = RequireRange(PayloadReader.GetInt(context.Request.Payload, "physicalArmor"), 0, 999, "physicalArmor");
        c.Stats.MagicalArmor = RequireRange(PayloadReader.GetInt(context.Request.Payload, "magicalArmor"), 0, 999, "magicalArmor");
        c.Stats.Morale = RequireRange(PayloadReader.GetInt(context.Request.Payload, "morale"), 0, 999, "morale");
        c.Stats.Strength = RequireRange(PayloadReader.GetInt(context.Request.Payload, "strength"), 0, 999, "strength");
        c.Stats.Dexterity = RequireRange(PayloadReader.GetInt(context.Request.Payload, "dexterity"), 0, 999, "dexterity");
        c.Stats.Endurance = RequireRange(PayloadReader.GetInt(context.Request.Payload, "endurance"), 0, 999, "endurance");
        c.Stats.Wisdom = RequireRange(PayloadReader.GetInt(context.Request.Payload, "wisdom"), 0, 999, "wisdom");
        c.Stats.Intellect = RequireRange(PayloadReader.GetInt(context.Request.Payload, "intellect"), 0, 999, "intellect");
        c.Stats.Charisma = RequireRange(PayloadReader.GetInt(context.Request.Payload, "charisma"), 0, 999, "charisma");
        _repositories.Characters.Replace(c);
        TryShadowWrite(() => _profileShadowWriteService.WriteAttributeProfileShadowAsync(c, actor.Id, context.Request.RequestId ?? string.Empty));
        WriteAudit("character", actor.Id, "updateStats", c.Id);
        return Ok("Character stats updated.");
    }

    public ResponseEnvelope CharacterUpdateVisibility(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        c.Visibility.HideDescriptionForOthers = PayloadReader.GetBool(context.Request.Payload, "hideDescriptionForOthers");
        c.Visibility.HideBackstoryForOthers = PayloadReader.GetBool(context.Request.Payload, "hideBackstoryForOthers");
        c.Visibility.HideStatsForOthers = PayloadReader.GetBool(context.Request.Payload, "hideStatsForOthers");
        c.Visibility.HideReputationForOthers = PayloadReader.GetBool(context.Request.Payload, "hideReputationForOthers");
        _repositories.Characters.Replace(c);
        WriteAudit("character", actor.Id, "updateVisibility", c.Id);
        return Ok("Character visibility updated.");
    }

    public ResponseEnvelope CharacterUpdateMoney(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        if (IsProfileNativeWalletWriteEnabled())
        {
            var native = _profileNativeWriteService.UpdateWalletProfileAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                var updated = GetCharacter(characterId);
                _logger.Admin($"character.money.save response=ok profileNative=true");
                WriteAudit("character", actor.Id, "updateMoney", characterId);
                return Ok("Character money updated.", new Dictionary<string, object> { { "money", WalletPayload(updated.Wallet) } });
            }

            return Error("Character wallet profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var c = GetCharacter(characterId);
        var moneyRawRuntimeType = context.Request.Payload.ContainsKey("money") && context.Request.Payload["money"] != null
            ? context.Request.Payload["money"]!.GetType().FullName ?? context.Request.Payload["money"]!.GetType().Name
            : "null";
        _logger.Admin($"character.update.money payloadKeys={string.Join(",", context.Request.Payload.Keys.OrderBy(key => key, StringComparer.Ordinal))}");
        _logger.Admin($"character.money.save runtimeType={moneyRawRuntimeType}");
        var moneyRaw = PayloadReader.GetDictionary(context.Request.Payload, "money") ?? new Dictionary<string, object>();
        var requestCurrencies = string.Join(",", moneyRaw.Keys.OrderBy(key => key, StringComparer.Ordinal));
        _logger.Admin("character.money.save request currencies=" + requestCurrencies);
        c.Wallet.EnsureAllDenominations();
        var updatedCurrencies = 0;
        var acceptedCurrencies = new List<string>();
        var rejectedCurrencies = new List<string>();
        foreach (CurrencyDenomination d in Enum.GetValues(typeof(CurrencyDenomination)))
        {
            var value = PayloadReader.GetLong(moneyRaw, d.ToString());
            if (!value.HasValue)
            {
                if (moneyRaw.ContainsKey(d.ToString())) rejectedCurrencies.Add(d + "=<unparsed>");
                continue;
            }
            if (value.Value < 0)
            {
                rejectedCurrencies.Add(d + "=" + value.Value);
                continue;
            }
            c.Wallet.Balance.Amounts[d.ToString()] = value.Value;
            updatedCurrencies++;
            acceptedCurrencies.Add(d + "=" + value.Value);
        }
        _logger.Admin($"character.money.save accepted keys={string.Join(",", acceptedCurrencies)}");
        _logger.Admin($"character.money.save rejected keys={string.Join(",", rejectedCurrencies)}");
        if (updatedCurrencies == 0)
        {
            _logger.Admin("character.money.save validator rejected keys=none-valid");
            throw new ArgumentException("money payload does not contain any valid currencies.");
        }
        if (updatedCurrencies == 0)
            throw new ArgumentException("money payload does not contain any valid currencies.");
        c.Wallet.NormalizeUpward();
        _repositories.Characters.Replace(c);
        TryShadowWrite(() => _profileShadowWriteService.WriteWalletProfileShadowAsync(c, actor.Id, context.Request.RequestId ?? string.Empty));
        _logger.Admin($"character.money.save response=ok currenciesSaved={updatedCurrencies}");
        WriteAudit("character", actor.Id, "updateMoney", c.Id);
        return Ok("Character money updated.", new Dictionary<string, object> { { "money", WalletPayload(c.Wallet) } });
    }

    public ResponseEnvelope CharacterUpdateXpCoins(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var xpCoins = PayloadReader.GetInt(context.Request.Payload, "xpCoins");
        if (!xpCoins.HasValue)
            throw new ArgumentException("xpCoins is required.");
        if (xpCoins.Value < 0)
            throw new ArgumentException("xpCoins must be >= 0.");

        if (IsProfileNativeWalletWriteEnabled())
        {
            var native = _profileNativeWriteService.UpdateWalletProfileAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                var updated = GetCharacter(characterId);
                WriteAudit("character", actor.Id, "updateXpCoins", characterId);
                return Ok("Character xp coins updated.", new Dictionary<string, object> { { "xpCoins", updated.XpCoins } });
            }

            return Error("Character xp profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        return Error("Character xp profile write is disabled.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    public ResponseEnvelope CharacterUpdateInventory(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        EnsureInventoryProfileUpdateDoesNotModifyReverseEngineeringReservations0193(characterId, context.Request.Payload);
        EnsureInventoryProfileUpdateDoesNotModifyPrototypeTestReservations0194(characterId);
        if (IsProfileNativeInventoryWriteEnabled())
        {
            var native = _profileNativeWriteService.UpdateInventoryProfileNativeAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                WriteAudit("character", actor.Id, "updateInventory", characterId);
                return Ok("Character inventory updated.");
            }

            return Error("Character inventory profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var c = GetCharacter(characterId);
        c.Inventory = ParseInventoryList(PayloadReader.GetList(context.Request.Payload, "inventory"));
        _repositories.Characters.Replace(c);
        TryShadowWrite(() => _profileShadowWriteService.WriteInventoryProfileShadowAsync(c, actor.Id, context.Request.RequestId ?? string.Empty));
        WriteAudit("character", actor.Id, "updateInventory", c.Id);
        return Ok("Character inventory updated.");
    }

    public ResponseEnvelope CharacterUpdateReputation(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        _ = GetCharacter(characterId);
        var values = (PayloadReader.GetList(context.Request.Payload, "reputation") ?? new List<object>())
            .Select(ToStringObjectDictionary)
            .Where(x => x.Count > 0)
            .Select(ParseReputationProfileValue)
            .ToList();
        SaveReputationProfile(characterId, new ReputationProfile { CharacterId = characterId, RuleSetId = RuleSetIds.FantasyNriDefault, Entries = values, SchemaVersion = 1 });
        WriteAudit("character", actor.Id, "updateReputationProfile", characterId);
        return Ok("Character reputation updated.");
    }

    public ResponseEnvelope CharacterUpdateHoldings(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        _ = GetCharacter(characterId);
        var values = (PayloadReader.GetList(context.Request.Payload, "holdings") ?? new List<object>())
            .Select(ToStringObjectDictionary)
            .Where(x => x.Count > 0)
            .Select(x => ParseHoldingProfileValue(x, characterId))
            .ToList();
        SaveHoldingsProfile(characterId, new HoldingsProfile { CharacterId = characterId, RuleSetId = RuleSetIds.FantasyNriDefault, Holdings = values, SchemaVersion = 1 });
        WriteAudit("character", actor.Id, "updateHoldingsProfile", characterId);
        return Ok("Character holdings updated.");
    }

    public ResponseEnvelope CharacterInventoryGet(CommandContext context) => CharacterGetInventory(context);
    public ResponseEnvelope CharacterCompanionsGet(CommandContext context) => CharacterGetCompanions(context);
    public ResponseEnvelope CharacterHoldingsGet(CommandContext context) => CharacterGetHoldings(context);
    public ResponseEnvelope CharacterReputationGet(CommandContext context) => CharacterGetReputation(context);
    public ResponseEnvelope CharacterSkillsGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var requestedCharacterId = PayloadReader.GetString(context.Request.Payload, "characterId");
        var character = string.IsNullOrWhiteSpace(requestedCharacterId)
            ? ResolveOwnedCharacter(context, actor)
            : GetCharacter(RequireLength(requestedCharacterId, 8, 128, "characterId"));
        EnsureCharacterDefaults(character);
        var owner = GetAccount(character.OwnerUserId);
        var ownerCheck = string.Equals(owner.Id, actor.Id, StringComparison.OrdinalIgnoreCase);
        var isAdmin = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
        var allowed = (ownerCheck || isAdmin) && CanViewCharacter(actor, owner, character);
        _logger.Admin($"character.skills.get auth actor={actor.Login} characterId={character.Id} ownerCheck={ownerCheck} allowed={allowed}");
        if (!allowed) throw new UnauthorizedAccessException("Character skills unavailable.");

        var skillRows = BuildCharacterSkillProfileRows(character, actor, isAdmin);
        _logger.Admin($"character.skills.get actor={actor.Login} characterId={character.Id} count={skillRows.Count} profileFirst=true");
        return Ok("Character skills loaded.", new Dictionary<string, object>
        {
            { "items", skillRows.Cast<object>().ToArray() },
            { "sourceOfTruth", "character_skill_profiles" }
        });
    }

    public ResponseEnvelope CharacterSkillAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        if (IsProfileNativeSkillWriteEnabled())
        {
            var native = _profileNativeWriteService.AddSkillProfileNativeAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                var updated = GetCharacter(characterId);
                var item = BuildCharacterSkillProfileRows(updated, actor, true).FirstOrDefault(x => string.Equals(Convert.ToString(x["skillCode"]), native.SkillId, StringComparison.OrdinalIgnoreCase));
                if (item == null) return Error("Character skill profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
                _logger.Admin($"character.skill.add actor={actor.Login} response=ok profileNative=true characterId={characterId} skillCode={native.SkillId}");
                return Ok("Character skill added.", new Dictionary<string, object> { { "item", item }, { "sourceOfTruth", "character_skill_profiles" } });
            }

            return Error("Character skill profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var character = GetCharacter(characterId);
        EnsureCharacterDefaults(character);
        var skillCode = RequireLength(PayloadReader.GetString(context.Request.Payload, "skillCode"), 1, 128, "skillCode");
        if (character.CharacterSkills.Any(x => string.Equals(x.SkillCode, skillCode, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Skill '{skillCode}' already exists.");
        var definition = _repositories.DefinitionSkills.GetByCode(skillCode)
            ?? throw new KeyNotFoundException($"Skill '{skillCode}' not found.");
        var level = RequireRange(PayloadReader.GetInt(context.Request.Payload, "level"), 1, 999, "level");
        var skill = new CharacterSkillState
        {
            SkillCode = definition.Code,
            Tier = definition.Tier,
            Level = Math.Min(level, Math.Max(1, definition.MaxLevel)),
            Acquired = true,
            LearnedUtc = DateTime.UtcNow
        };
        character.CharacterSkills.Add(skill);
        _repositories.Characters.Replace(character);
        TryShadowWrite(() => _profileShadowWriteService.WriteSkillProfileShadowAsync(character, actor.Id, context.Request.RequestId ?? string.Empty));
        _logger.Admin($"character.skill.add actor={actor.Login} response=ok characterId={character.Id} skillCode={skill.SkillCode} level={skill.Level}");
        return Ok("Character skill added.", new Dictionary<string, object> { { "item", CharacterSkillPayload(skill) } });
    }

    public ResponseEnvelope CharacterSkillUpdateLevel(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        if (IsProfileNativeSkillWriteEnabled())
        {
            var native = _profileNativeWriteService.UpdateSkillProfileNativeAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                var updated = GetCharacter(characterId);
                var item = BuildCharacterSkillProfileRows(updated, actor, true).FirstOrDefault(x => string.Equals(Convert.ToString(x["skillCode"]), native.SkillId, StringComparison.OrdinalIgnoreCase));
                if (item == null) return Error("Character skill profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
                _logger.Admin($"character.skill.updateLevel actor={actor.Login} response=ok profileNative=true characterId={characterId} skillCode={native.SkillId}");
                return Ok("Character skill level updated.", new Dictionary<string, object> { { "item", item }, { "sourceOfTruth", "character_skill_profiles" } });
            }

            return Error("Character skill profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var character = GetCharacter(characterId);
        EnsureCharacterDefaults(character);
        var skillCode = RequireLength(PayloadReader.GetString(context.Request.Payload, "skillCode"), 1, 128, "skillCode");
        var skill = character.CharacterSkills.FirstOrDefault(x => string.Equals(x.SkillCode, skillCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Skill '{skillCode}' not found on character.");
        var definition = _repositories.DefinitionSkills.GetByCode(skill.SkillCode)
            ?? throw new KeyNotFoundException($"Skill '{skill.SkillCode}' definition not found.");
        var level = RequireRange(PayloadReader.GetInt(context.Request.Payload, "level"), 1, 999, "level");
        skill.Level = Math.Min(level, Math.Max(1, definition.MaxLevel));
        _repositories.Characters.Replace(character);
        TryShadowWrite(() => _profileShadowWriteService.WriteSkillProfileShadowAsync(character, actor.Id, context.Request.RequestId ?? string.Empty));
        _logger.Admin($"character.skill.updateLevel actor={actor.Login} response=ok characterId={character.Id} skillCode={skill.SkillCode} level={skill.Level}");
        return Ok("Character skill level updated.", new Dictionary<string, object> { { "item", CharacterSkillPayload(skill) } });
    }

    public ResponseEnvelope CharacterSkillRemove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        if (IsProfileNativeSkillWriteEnabled())
        {
            var native = _profileNativeWriteService.RemoveSkillProfileNativeAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                _logger.Admin($"character.skill.remove actor={actor.Login} response=ok profileNative=true characterId={characterId} skillCode={native.SkillId}");
                return Ok("Character skill removed.");
            }

            return Error("Character skill profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var character = GetCharacter(characterId);
        EnsureCharacterDefaults(character);
        var skillCode = RequireLength(PayloadReader.GetString(context.Request.Payload, "skillCode"), 1, 128, "skillCode");
        character.CharacterSkills.RemoveAll(x => string.Equals(x.SkillCode, skillCode, StringComparison.OrdinalIgnoreCase));
        _repositories.Characters.Replace(character);
        TryShadowWrite(() => _profileShadowWriteService.WriteSkillProfileShadowAsync(character, actor.Id, context.Request.RequestId ?? string.Empty));
        _logger.Admin($"character.skill.remove actor={actor.Login} response=ok characterId={character.Id} skillCode={skillCode}");
        return Ok("Character skill removed.");
    }

    public ResponseEnvelope CharacterInventoryItemAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        if (IsProfileNativeInventoryWriteEnabled())
        {
            var native = _profileNativeWriteService.AddInventoryItemProfileNativeAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                var updated = GetCharacter(characterId);
                var updatedItem = updated.Inventory.FirstOrDefault(x => string.Equals(x.Id, native.ItemId, StringComparison.OrdinalIgnoreCase));
                if (updatedItem == null) return Error("Character inventory profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
                _logger.Admin($"character.inventory.item.add response=ok profileNative=true characterId={characterId} itemId={updatedItem.Id}");
                return Ok("Character inventory item added.", new Dictionary<string, object> { { "item", InventoryPayload(updatedItem) } });
            }

            return Error("Character inventory profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var character = GetCharacter(characterId);
        var item = ParseInventoryItem(PayloadReader.GetDictionary(context.Request.Payload, "item") ?? context.Request.Payload);
        character.Inventory.Add(item);
        _repositories.Characters.Replace(character);
        var updatedCharacter = GetCharacter(character.Id);
        TryWriteInventoryProfileShadowAsync(updatedCharacter, actor.Id, context.Request.RequestId ?? string.Empty, CommandNames.CharacterInventoryItemAdd);
        _logger.Admin($"character.inventory.item.add response=ok characterId={character.Id} itemId={item.Id}");
        return Ok("Character inventory item added.", new Dictionary<string, object> { { "item", InventoryPayload(item) } });
    }

    public ResponseEnvelope CharacterInventoryItemUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var requestedItemId = RequireLength(PayloadReader.GetString(context.Request.Payload, "itemId"), 1, 128, "itemId");
        EnsureInventoryItemNotReservedForReverseEngineering0193(characterId, requestedItemId);
        EnsurePrototypeInventoryItemActionAllowed0194(characterId, requestedItemId, "update");
        if (IsProfileNativeInventoryWriteEnabled())
        {
            var native = _profileNativeWriteService.UpdateInventoryItemProfileNativeAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                var updated = GetCharacter(characterId);
                var updatedItem = updated.Inventory.FirstOrDefault(x => string.Equals(x.Id, native.ItemId, StringComparison.OrdinalIgnoreCase));
                if (updatedItem == null) return Error("Character inventory profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
                _logger.Admin($"character.inventory.item.update response=ok profileNative=true characterId={characterId} itemId={updatedItem.Id}");
                return Ok("Character inventory item updated.", new Dictionary<string, object> { { "item", InventoryPayload(updatedItem) } });
            }

            return Error("Character inventory profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var character = GetCharacter(characterId);
        var itemId = requestedItemId;
        var incoming = ParseInventoryItem(PayloadReader.GetDictionary(context.Request.Payload, "item") ?? context.Request.Payload);
        var existing = character.Inventory.FirstOrDefault(x => string.Equals(x.Id, itemId, StringComparison.OrdinalIgnoreCase));
        if (existing == null) throw new KeyNotFoundException("Inventory item not found.");
        incoming.Id = existing.Id;
        character.Inventory[character.Inventory.IndexOf(existing)] = incoming;
        _repositories.Characters.Replace(character);
        var updatedCharacter = GetCharacter(character.Id);
        TryWriteInventoryProfileShadowAsync(updatedCharacter, actor.Id, context.Request.RequestId ?? string.Empty, CommandNames.CharacterInventoryItemUpdate);
        _logger.Admin($"character.inventory.item.update response=ok characterId={character.Id} itemId={incoming.Id}");
        return Ok("Character inventory item updated.", new Dictionary<string, object> { { "item", InventoryPayload(incoming) } });
    }

    public ResponseEnvelope CharacterInventoryItemRemove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var requestedItemId = RequireLength(PayloadReader.GetString(context.Request.Payload, "itemId"), 1, 128, "itemId");
        EnsureInventoryItemNotReservedForReverseEngineering0193(characterId, requestedItemId);
        EnsurePrototypeInventoryItemActionAllowed0194(characterId, requestedItemId, "remove");
        if (IsProfileNativeInventoryWriteEnabled())
        {
            var native = _profileNativeWriteService.RemoveInventoryItemProfileNativeAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                _logger.Admin($"character.inventory.item.remove response=ok profileNative=true characterId={characterId} itemId={native.ItemId}");
                return Ok("Character inventory item removed.");
            }

            return Error("Character inventory profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var character = GetCharacter(characterId);
        var itemId = requestedItemId;
        character.Inventory.RemoveAll(x => string.Equals(x.Id, itemId, StringComparison.OrdinalIgnoreCase));
        _repositories.Characters.Replace(character);
        var updatedCharacter = GetCharacter(character.Id);
        TryWriteInventoryProfileShadowAsync(updatedCharacter, actor.Id, context.Request.RequestId ?? string.Empty, CommandNames.CharacterInventoryItemRemove);
        _logger.Admin($"character.inventory.item.remove response=ok characterId={character.Id} itemId={itemId}");
        return Ok("Character inventory item removed.");
    }

    public ResponseEnvelope CharacterInventoryItemToggleEquip(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var requestedItemId = RequireLength(PayloadReader.GetString(context.Request.Payload, "itemId"), 1, 128, "itemId");
        EnsureInventoryItemNotReservedForReverseEngineering0193(characterId, requestedItemId);
        EnsurePrototypeInventoryItemActionAllowed0194(characterId, requestedItemId, "equip");
        if (IsProfileNativeInventoryWriteEnabled())
        {
            var native = _profileNativeWriteService.ToggleEquipInventoryItemProfileNativeAsync(characterId, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                var updated = GetCharacter(characterId);
                var updatedItem = updated.Inventory.FirstOrDefault(x => string.Equals(x.Id, native.ItemId, StringComparison.OrdinalIgnoreCase));
                if (updatedItem == null) return Error("Character inventory profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
                _logger.Admin($"character.inventory.item.toggleEquip response=ok profileNative=true characterId={characterId} itemId={updatedItem.Id} equipped={updatedItem.IsEquipped}");
                return Ok("Character inventory item equip status updated.", new Dictionary<string, object> { { "item", InventoryPayload(updatedItem) } });
            }

            return Error("Character inventory profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        var character = GetCharacter(characterId);
        var itemId = requestedItemId;
        var existing = character.Inventory.FirstOrDefault(x => string.Equals(x.Id, itemId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("Inventory item not found.");
        existing.IsEquipped = !existing.IsEquipped;
        existing.Equipped = existing.IsEquipped;
        _repositories.Characters.Replace(character);
        var updatedCharacter = GetCharacter(character.Id);
        TryWriteInventoryProfileShadowAsync(updatedCharacter, actor.Id, context.Request.RequestId ?? string.Empty, CommandNames.CharacterInventoryItemToggleEquip);
        _logger.Admin($"character.inventory.item.toggleEquip response=ok characterId={character.Id} itemId={itemId} equipped={existing.IsEquipped}");
        return Ok("Character inventory item equip status updated.", new Dictionary<string, object> { { "item", InventoryPayload(existing) } });
    }

    public ResponseEnvelope CharacterCompanionAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var profile = LoadCompanionProfile(character.Id);
        var value = ParseCompanionProfileValue(PayloadReader.GetDictionary(context.Request.Payload, "companion") ?? context.Request.Payload, character.Id);
        profile.Companions.RemoveAll(x => string.Equals(x.CompanionId, value.CompanionId, StringComparison.OrdinalIgnoreCase));
        profile.Companions.Add(value);
        SaveCompanionProfile(character.Id, profile);
        WriteAudit("character", actor.Id, "addCompanionProfile", character.Id);
        _logger.Admin($"character.companion.add response=ok profileNative=true characterId={character.Id} companionId={value.CompanionId}");
        return Ok("Character companion added.", new Dictionary<string, object> { { "companion", CompanionProfilePayload(value) } });
    }

    public ResponseEnvelope CharacterCompanionUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var companionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "companionId"), 1, 128, "companionId");
        var profile = LoadCompanionProfile(character.Id);
        var existing = profile.Companions.FirstOrDefault(x => string.Equals(x.CompanionId, companionId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("Companion not found.");
        var incoming = ParseCompanionProfileValue(PayloadReader.GetDictionary(context.Request.Payload, "companion") ?? context.Request.Payload, character.Id);
        incoming.CompanionId = existing.CompanionId;
        if (string.IsNullOrWhiteSpace(incoming.OwnerCharacterId)) incoming.OwnerCharacterId = existing.OwnerCharacterId;
        profile.Companions[profile.Companions.IndexOf(existing)] = incoming;
        SaveCompanionProfile(character.Id, profile);
        WriteAudit("character", actor.Id, "updateCompanionProfile", character.Id);
        _logger.Admin($"character.companion.update response=ok profileNative=true characterId={character.Id} companionId={incoming.CompanionId}");
        return Ok("Character companion updated.", new Dictionary<string, object> { { "companion", CompanionProfilePayload(incoming) } });
    }

    public ResponseEnvelope CharacterCompanionRemove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var companionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "companionId"), 1, 128, "companionId");
        var profile = LoadCompanionProfile(character.Id);
        profile.Companions.RemoveAll(x => string.Equals(x.CompanionId, companionId, StringComparison.OrdinalIgnoreCase));
        SaveCompanionProfile(character.Id, profile);
        WriteAudit("character", actor.Id, "removeCompanionProfile", character.Id);
        _logger.Admin($"character.companion.remove response=ok profileNative=true characterId={character.Id} companionId={companionId}");
        return Ok("Character companion removed.");
    }

    public ResponseEnvelope CharacterHoldingAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var profile = LoadHoldingsProfile(character.Id);
        var value = ParseHoldingProfileValue(PayloadReader.GetDictionary(context.Request.Payload, "holding") ?? context.Request.Payload, character.Id);
        profile.Holdings.RemoveAll(x => string.Equals(x.HoldingId, value.HoldingId, StringComparison.OrdinalIgnoreCase));
        profile.Holdings.Add(value);
        SaveHoldingsProfile(character.Id, profile);
        WriteAudit("character", actor.Id, "addHoldingProfile", character.Id);
        _logger.Admin($"character.holding.add response=ok profileNative=true characterId={character.Id} holdingId={value.HoldingId}");
        return Ok("Character holding added.", new Dictionary<string, object> { { "holding", HoldingProfilePayload(value) } });
    }

    public ResponseEnvelope CharacterHoldingUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var holdingId = RequireLength(PayloadReader.GetString(context.Request.Payload, "holdingId"), 1, 128, "holdingId");
        var profile = LoadHoldingsProfile(character.Id);
        var existing = profile.Holdings.FirstOrDefault(x => string.Equals(x.HoldingId, holdingId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("Holding not found.");
        var incoming = ParseHoldingProfileValue(PayloadReader.GetDictionary(context.Request.Payload, "holding") ?? context.Request.Payload, character.Id);
        incoming.HoldingId = existing.HoldingId;
        profile.Holdings[profile.Holdings.IndexOf(existing)] = incoming;
        SaveHoldingsProfile(character.Id, profile);
        WriteAudit("character", actor.Id, "updateHoldingProfile", character.Id);
        _logger.Admin($"character.holding.update response=ok profileNative=true characterId={character.Id} holdingId={incoming.HoldingId}");
        return Ok("Character holding updated.", new Dictionary<string, object> { { "holding", HoldingProfilePayload(incoming) } });
    }

    public ResponseEnvelope CharacterHoldingRemove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var holdingId = RequireLength(PayloadReader.GetString(context.Request.Payload, "holdingId"), 1, 128, "holdingId");
        var profile = LoadHoldingsProfile(character.Id);
        profile.Holdings.RemoveAll(x => string.Equals(x.HoldingId, holdingId, StringComparison.OrdinalIgnoreCase));
        SaveHoldingsProfile(character.Id, profile);
        WriteAudit("character", actor.Id, "removeHoldingProfile", character.Id);
        _logger.Admin($"character.holding.remove response=ok profileNative=true characterId={character.Id} holdingId={holdingId}");
        return Ok("Character holding removed.");
    }

    public ResponseEnvelope CharacterReputationEntryAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var profile = LoadReputationProfile(character.Id);
        var value = ParseReputationProfileValue(PayloadReader.GetDictionary(context.Request.Payload, "entry") ?? context.Request.Payload);
        profile.Entries.RemoveAll(x => string.Equals(x.EntryId, value.EntryId, StringComparison.OrdinalIgnoreCase));
        profile.Entries.Add(value);
        SaveReputationProfile(character.Id, profile);
        WriteAudit("character", actor.Id, "addReputationProfile", character.Id);
        _logger.Admin($"character.reputation.entry.add response=ok profileNative=true characterId={character.Id} reputationId={value.EntryId}");
        return Ok("Character reputation entry added.", new Dictionary<string, object> { { "entry", ReputationProfilePayload(value) } });
    }

    public ResponseEnvelope CharacterReputationEntryUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var entryId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entryId"), 1, 128, "entryId");
        var profile = LoadReputationProfile(character.Id);
        var existing = profile.Entries.FirstOrDefault(x => string.Equals(x.EntryId, entryId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("Reputation entry not found.");
        var incoming = ParseReputationProfileValue(PayloadReader.GetDictionary(context.Request.Payload, "entry") ?? context.Request.Payload);
        incoming.EntryId = existing.EntryId;
        profile.Entries[profile.Entries.IndexOf(existing)] = incoming;
        SaveReputationProfile(character.Id, profile);
        WriteAudit("character", actor.Id, "updateReputationProfile", character.Id);
        _logger.Admin($"character.reputation.entry.update response=ok profileNative=true characterId={character.Id} reputationId={incoming.EntryId}");
        return Ok("Character reputation entry updated.", new Dictionary<string, object> { { "entry", ReputationProfilePayload(incoming) } });
    }

    public ResponseEnvelope CharacterReputationEntryRemove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var entryId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entryId"), 1, 128, "entryId");
        var profile = LoadReputationProfile(character.Id);
        profile.Entries.RemoveAll(x => string.Equals(x.EntryId, entryId, StringComparison.OrdinalIgnoreCase));
        SaveReputationProfile(character.Id, profile);
        WriteAudit("character", actor.Id, "removeReputationProfile", character.Id);
        _logger.Admin($"character.reputation.entry.remove response=ok profileNative=true characterId={character.Id} reputationId={entryId}");
        return Ok("Character reputation entry removed.");
    }

    public ResponseEnvelope CharacterAdminList(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var items = new List<object>();
        var skipped = 0;
        foreach (var character in _repositories.Characters.Find(FilterDefinition<Character>.Empty).Where(c => includeArchived || !c.Archived))
        {
            try
            {
                EnsureCharacterDefaults(character);
                items.Add(CharacterDetailsPayloadWithProfileFirst(character, GetAccount(character.OwnerUserId), actor, context.Request.RequestId ?? string.Empty));
            }
            catch (Exception ex)
            {
                skipped++;
                _logger.Admin($"character.admin.list.profile_skipped characterId={character.Id} reason={ex.Message}");
            }
        }
        _logger.Admin($"character.admin.list actor={actor.Login} count={items.Count} skipped={skipped} includeArchived={includeArchived}");
        return Ok("Character admin list loaded.", new Dictionary<string, object>
        {
            { "items", items.ToArray() },
            { "warnings", skipped == 0 ? Array.Empty<object>() : new object[] { $"Пропущено временно недоступных профилей: {skipped}." } }
        });
    }

    public ResponseEnvelope CharacterAdminSearch(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var query = (PayloadReader.GetString(context.Request.Payload, "query") ?? string.Empty).Trim();
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var ownerUserId = (PayloadReader.GetString(context.Request.Payload, "ownerUserId") ?? string.Empty).Trim();
        var raceCode = (PayloadReader.GetString(context.Request.Payload, "raceCode") ?? string.Empty).Trim();
        var classCode = (PayloadReader.GetString(context.Request.Payload, "classCode") ?? string.Empty).Trim();
        var lowered = query.ToLowerInvariant();

        var candidates = _repositories.Characters.Find(FilterDefinition<Character>.Empty)
            .Where(c =>
            {
                EnsureCharacterDefaults(c);
                var queryMatch = string.IsNullOrWhiteSpace(lowered)
                    || c.Id.ToLowerInvariant().Contains(lowered)
                    || c.Name.ToLowerInvariant().Contains(lowered)
                    || c.OwnerUserId.ToLowerInvariant().Contains(lowered);
                var ownerMatch = string.IsNullOrWhiteSpace(ownerUserId) || string.Equals(c.OwnerUserId, ownerUserId, StringComparison.OrdinalIgnoreCase);
                var raceMatch = string.IsNullOrWhiteSpace(raceCode) || string.Equals(c.RaceCode, raceCode, StringComparison.OrdinalIgnoreCase);
                var classMatch = string.IsNullOrWhiteSpace(classCode) || c.CharacterClasses.Any(x => string.Equals(x.ClassCode, classCode, StringComparison.OrdinalIgnoreCase));
                var archiveMatch = includeArchived || !c.Archived;
                return queryMatch && ownerMatch && raceMatch && classMatch && archiveMatch;
            })
            .ToArray();

        var items = new List<object>();
        var skipped = 0;
        foreach (var character in candidates)
        {
            try
            {
                items.Add(CharacterDetailsPayloadWithProfileFirst(character, GetAccount(character.OwnerUserId), actor, context.Request.RequestId ?? string.Empty));
            }
            catch (Exception ex)
            {
                skipped++;
                _logger.Admin($"character.admin.search.profile_skipped characterId={character.Id} reason={ex.Message}");
            }
        }

        _logger.Admin($"character.admin.search actor={actor.Login} query={query} count={items.Count} skipped={skipped}");
        return Ok("Character admin search loaded.", new Dictionary<string, object>
        {
            { "items", items.ToArray() },
            { "warnings", skipped == 0 ? Array.Empty<object>() : new object[] { $"Пропущено временно недоступных профилей: {skipped}." } }
        });
    }

    public ResponseEnvelope CharacterAdminGet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        EnsureCharacterDefaults(character);
        _logger.Admin($"character.admin.get actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character admin aggregate loaded.", BuildCharacterAggregatePayload(character, actor, includeNotesContext: true));
    }

    public ResponseEnvelope CharacterAdminSaveBasic(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        EnsureCharacterEditAllowed(actor, character.Id);
        EnsureCharacterDefaults(character);
        var raceCode = (PayloadReader.GetString(context.Request.Payload, "raceCode") ?? string.Empty).Trim();
        RaceDefinition? raceDefinition = null;
        if (!string.IsNullOrWhiteSpace(raceCode))
        {
            raceDefinition = _repositories.RaceDefinitions.GetByCode(raceCode) ?? throw new ArgumentException("Race definition not found.");
        }

        if (IsProfileNativeRaceBodyWriteEnabled())
        {
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 0, 64, "height");
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
            var nativePayload = new Dictionary<string, object>(context.Request.Payload);
            if (raceDefinition != null)
            {
                nativePayload["raceCode"] = raceDefinition.Code;
                nativePayload["race"] = raceDefinition.Name;
                nativePayload["raceName"] = raceDefinition.Name;
            }

            var native = UpdateRaceBodyProfilesNativeForEnabledSections(character.Id, nativePayload, actor.Id, context.Request.RequestId ?? string.Empty);
            if (native.LegacyFacadeSynced)
            {
                character = GetCharacter(character.Id);
                character.Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
                if (!IsProfileNativeBodyWriteEnabled())
                {
                    character.Height = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 0, 64, "height");
                    character.Age = PayloadReader.GetInt(context.Request.Payload, "age");
                }
                if (!IsProfileNativeRaceOrSpeciesWriteEnabled() && raceDefinition != null)
                {
                    character.RaceCode = raceDefinition.Code;
                    character.Race = raceDefinition.Name;
                }
                character.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
                character.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
                _repositories.Characters.Replace(character);
                _logger.Admin($"character.admin.save.basic actor={actor.Login} characterId={character.Id} result=ok profileNativeRaceBody=true");
                return Ok("Character basic saved.", BuildCharacterAggregatePayload(character, actor, includeNotesContext: false));
            }

            return Error("Character race/body profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        character.Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
        character.Height = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 0, 64, "height");
        character.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
        character.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
        character.Age = PayloadReader.GetInt(context.Request.Payload, "age");
        if (raceDefinition != null)
        {
            character.RaceCode = raceDefinition.Code;
            character.Race = raceDefinition.Name;
        }

        _repositories.Characters.Replace(character);
        TryWriteRaceBodyProfileShadowsAsync(character, actor.Id, context.Request.RequestId ?? string.Empty);
        _logger.Admin($"character.admin.save.basic actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character basic saved.", BuildCharacterAggregatePayload(character, actor, includeNotesContext: false));
    }

    public ResponseEnvelope CharacterAdminSaveBiography(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        EnsureCharacterEditAllowed(actor, character.Id);
        EnsureCharacterDefaults(character);

        var description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description") ?? character.Description ?? string.Empty, 0, 2048, "description");
        var backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
        var nativePayload = new Dictionary<string, object>(context.Request.Payload)
        {
            ["description"] = description,
            ["backstory"] = backstory
        };

        _logger.Admin($"character.admin.biography.save.start actor={actor.Login} characterId={character.Id} length={backstory.Length}");
        var native = _profileNativeWriteService.UpdateBiographyProfileNativeAsync(character.Id, nativePayload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
        if (native.BodyProfileWritten && native.LegacyFacadeSynced && !native.UsedFallback)
        {
            character = GetCharacter(character.Id);
            _logger.Admin($"character.admin.biography.save.done actor={actor.Login} characterId={character.Id} length={backstory.Length} profileNative=true fallback=false");
            return Ok("Character biography saved.", new Dictionary<string, object>
            {
                { "characterId", character.Id },
                { "description", character.Description ?? string.Empty },
                { "backstory", character.Backstory ?? string.Empty },
                { "profileNative", true },
                { "fallback", false }
            });
        }

        _logger.Admin($"character.admin.biography.save.failed actor={actor.Login} characterId={character.Id} reason={native.ErrorMessage}");
        return Error("Character biography profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
    }

    public ResponseEnvelope CharacterAdminSaveStats(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        EnsureCharacterEditAllowed(actor, character.Id);
        EnsureCharacterDefaults(character);
        if (IsProfileNativeStatsWriteEnabled())
        {
            var native = _profileNativeWriteService.UpdateAttributeProfileAsync(character.Id, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                character = GetCharacter(character.Id);
                _logger.Admin($"character.admin.save.stats actor={actor.Login} characterId={character.Id} result=ok profileNative=true");
                return Ok("Character stats saved.", new Dictionary<string, object> { { "stats", StatsPayload(character.Stats) } });
            }

            return Error("Character stats profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        ApplyStatsFromPayload(character, context.Request.Payload);
        _repositories.Characters.Replace(character);
        _logger.Admin($"character.admin.save.stats actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character stats saved.", new Dictionary<string, object> { { "stats", StatsPayload(character.Stats) } });
    }

    public ResponseEnvelope CharacterAdminSaveMoney(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        EnsureCharacterEditAllowed(actor, character.Id);
        EnsureCharacterDefaults(character);
        if (IsProfileNativeWalletWriteEnabled())
        {
            var native = _profileNativeWriteService.UpdateWalletProfileAsync(character.Id, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                character = GetCharacter(character.Id);
                _logger.Admin($"character.admin.save.money actor={actor.Login} characterId={character.Id} result=ok profileNative=true");
                return Ok("Character money saved.", BuildMoneyPayload(character));
            }

            return Error("Character wallet profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        return Error("Character wallet profile write is disabled.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    public ResponseEnvelope CharacterAdminSaveProgression(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        EnsureCharacterEditAllowed(actor, character.Id);
        EnsureCharacterDefaults(character);

        var raceCode = (PayloadReader.GetString(context.Request.Payload, "raceCode") ?? string.Empty).Trim();
        RaceDefinition? raceDefinition = null;
        if (!string.IsNullOrWhiteSpace(raceCode))
        {
            raceDefinition = _repositories.RaceDefinitions.GetByCode(raceCode) ?? throw new ArgumentException("Race definition not found.");
            character.RaceCode = raceDefinition.Code;
            character.Race = raceDefinition.Name;
        }

        var xpCoins = PayloadReader.GetInt(context.Request.Payload, "xpCoins");
        if (xpCoins.HasValue)
        {
            if (xpCoins.Value < 0) throw new ArgumentException("xpCoins must be >= 0.");
            character.XpCoins = xpCoins.Value;
        }

        var classList = PayloadReader.GetList(context.Request.Payload, "characterClasses");
        if (classList != null) character.CharacterClasses = ParseCharacterClasses(classList);
        var skillList = PayloadReader.GetList(context.Request.Payload, "characterSkills");
        if (skillList != null) character.CharacterSkills = ParseCharacterSkills(skillList);
        ValidateProgressionState(character);

        if (raceDefinition != null && IsProfileNativeRaceOrSpeciesWriteEnabled())
        {
            var nativePayload = new Dictionary<string, object>(context.Request.Payload)
            {
                ["raceCode"] = raceDefinition.Code,
                ["race"] = raceDefinition.Name,
                ["raceName"] = raceDefinition.Name
            };
            var native = _profileNativeWriteService.UpdateRaceOrSpeciesProfileNativeAsync(character.Id, nativePayload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (!native.LegacyFacadeSynced)
            {
                return Error("Character race profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
            }

            var synced = GetCharacter(character.Id);
            synced.XpCoins = character.XpCoins;
            synced.CharacterClasses = character.CharacterClasses;
            synced.CharacterSkills = character.CharacterSkills;
            character = synced;
        }

        _repositories.Characters.Replace(character);
        TryWriteRaceBodyProfileShadowsAsync(character, actor.Id, context.Request.RequestId ?? string.Empty);
        _logger.Admin($"character.admin.save.progression actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character progression saved.", BuildProgressionPayload(character, includeAdmin: true));
    }

    public ResponseEnvelope CharacterAdminSaveVisibility(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        EnsureCharacterDefaults(character);
        EnsureCharacterEditAllowed(actor, character.Id);
        character.Visibility.HideDescriptionForOthers = PayloadReader.GetBool(context.Request.Payload, "hideDescriptionForOthers");
        character.Visibility.HideBackstoryForOthers = PayloadReader.GetBool(context.Request.Payload, "hideBackstoryForOthers");
        character.Visibility.HideStatsForOthers = PayloadReader.GetBool(context.Request.Payload, "hideStatsForOthers");
        character.Visibility.HideReputationForOthers = PayloadReader.GetBool(context.Request.Payload, "hideReputationForOthers");
        character.Visibility.HideRaceForOthers = PayloadReader.GetBool(context.Request.Payload, "hideRaceForOthers");
        character.Visibility.HideHeightForOthers = PayloadReader.GetBool(context.Request.Payload, "hideHeightForOthers");
        character.Visibility.HideInventoryForOthers = PayloadReader.GetBool(context.Request.Payload, "hideInventoryForOthers");
        _repositories.Characters.Replace(character);
        _logger.Admin($"character.admin.save.visibility actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character visibility saved.", new Dictionary<string, object> { { "visibility", VisibilityPayload(character.Visibility) } });
    }

    public ResponseEnvelope CharacterAdminGetNotesContext(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var notes = BuildNotesContextPayload(characterId);
        _logger.Admin($"character.admin.get.notesContext actor={actor.Login} characterId={characterId} notesCount={((object[])notes["noteLinks"]).Length}");
        return Ok("Character notes context loaded.", notes);
    }

    public ResponseEnvelope CharacterSelfGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveOwnedCharacter(context, actor);
        EnsureCharacterDefaults(character);
        _logger.Admin($"character.self.get actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character self aggregate loaded.", BuildCharacterAggregatePayload(character, actor, includeNotesContext: false));
    }

    public ResponseEnvelope CharacterSelfSaveBasic(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveOwnedCharacter(context, actor);
        EnsureCharacterDefaults(character);
        if (IsProfileNativeBodyWriteEnabled())
        {
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 0, 64, "height");
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
            _ = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
            var native = _profileNativeWriteService.UpdateBodyProfileNativeAsync(character.Id, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.LegacyFacadeSynced)
            {
                character = GetCharacter(character.Id);
                character.Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
                character.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
                character.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
                _repositories.Characters.Replace(character);
                _logger.Admin($"character.self.save.basic actor={actor.Login} characterId={character.Id} result=ok profileNativeBody=true");
                return Ok("Character self basic saved.", BuildCharacterAggregatePayload(character, actor, includeNotesContext: false));
            }

            return Error("Character body profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        character.Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
        character.Height = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 0, 64, "height");
        character.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
        character.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
        character.Age = PayloadReader.GetInt(context.Request.Payload, "age");
        _repositories.Characters.Replace(character);
        TryWriteRaceBodyProfileShadowsAsync(character, actor.Id, context.Request.RequestId ?? string.Empty);
        _logger.Admin($"character.self.save.basic actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character self basic saved.", BuildCharacterAggregatePayload(character, actor, includeNotesContext: false));
    }

    public ResponseEnvelope CharacterSelfSaveStats(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveOwnedCharacter(context, actor);
        EnsureCharacterDefaults(character);
        if (IsProfileNativeStatsWriteEnabled())
        {
            var native = _profileNativeWriteService.UpdateAttributeProfileAsync(character.Id, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                character = GetCharacter(character.Id);
                _logger.Admin($"character.self.save.stats actor={actor.Login} characterId={character.Id} result=ok profileNative=true");
                return Ok("Character self stats saved.", new Dictionary<string, object> { { "stats", StatsPayload(character.Stats) } });
            }

            return Error("Character stats profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        ApplyStatsFromPayload(character, context.Request.Payload);
        _repositories.Characters.Replace(character);
        _logger.Admin($"character.self.save.stats actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character self stats saved.", new Dictionary<string, object> { { "stats", StatsPayload(character.Stats) } });
    }

    public ResponseEnvelope CharacterSelfSaveMoney(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveOwnedCharacter(context, actor);
        EnsureCharacterDefaults(character);
        if (IsProfileNativeWalletWriteEnabled())
        {
            var native = _profileNativeWriteService.UpdateWalletProfileAsync(character.Id, context.Request.Payload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                character = GetCharacter(character.Id);
                _logger.Admin($"character.self.save.money actor={actor.Login} characterId={character.Id} result=ok profileNative=true");
                return Ok("Character self money saved.", BuildMoneyPayload(character));
            }

            return Error("Character wallet profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        return Error("Character wallet profile write is disabled.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    public ResponseEnvelope CharacterSelfGetProgression(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveOwnedCharacter(context, actor);
        EnsureCharacterDefaults(character);
        _logger.Admin($"character.self.get.progression actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character self progression loaded.", BuildProgressionPayload(character, includeAdmin: false));
    }

    public ResponseEnvelope CharacterLockAcquire(CommandContext context) => CharacterLockExecute(context, CommandNames.LockAcquire);
    public ResponseEnvelope CharacterLockRelease(CommandContext context) => CharacterLockExecute(context, CommandNames.LockRelease);
    public ResponseEnvelope CharacterLockForceRelease(CommandContext context) => CharacterLockExecute(context, CommandNames.LockForceRelease, allowAdminForceRelease: true);
    public ResponseEnvelope CharacterLockGet(CommandContext context) => CharacterLockExecute(context, CommandNames.LockStatus);

    private ResponseEnvelope CharacterLockExecute(CommandContext context, string lockCommand, bool allowAdminForceRelease = false)
    {
        var payload = new Dictionary<string, object>(context.Request.Payload)
        {
            ["entityType"] = "character",
            ["entityId"] = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId")
        };
        var cloned = new CommandContext
        {
            ConnectionId = context.ConnectionId,
            Request = new RequestEnvelope
            {
                Command = lockCommand,
                RequestId = context.Request.RequestId,
                AuthToken = context.Request.AuthToken,
                SessionId = context.Request.SessionId,
                TimestampUtc = context.Request.TimestampUtc,
                Version = context.Request.Version,
                Payload = payload
            },
            Session = context.Session
        };

        if (allowAdminForceRelease)
        {
            var actor = RequireAdmin(context);
            var lockItem = FindActiveLock("character", (string)payload["entityId"]);
            if (lockItem == null) return Ok("Lock not found.");
            lockItem.Deleted = true;
            lockItem.Archived = true;
            _repositories.Locks.Replace(lockItem);
            _logger.Admin($"character.lock.forceRelease actor={actor.Login} characterId={payload["entityId"]} result=ok");
            return Ok("Character lock force released.");
        }

        var response = lockCommand switch
        {
            var x when x == CommandNames.LockAcquire => LockAcquire(cloned),
            var x when x == CommandNames.LockRelease => LockRelease(cloned),
            var x when x == CommandNames.LockStatus => LockStatus(cloned),
            _ => throw new ArgumentException("Unsupported lock command.")
        };
        var actorAccount = GetCurrentAccount(context);
        _logger.Admin($"character.lock.{lockCommand.Split('.').Last()} actor={actorAccount.Login} characterId={payload["entityId"]} result={response.Status}");
        return response;
    }

    public ResponseEnvelope PresenceList(CommandContext context)
    {
        RequireAdmin(context);
        var items = _repositories.Presence.Find(FilterDefinition<SessionUserState>.Empty)
            .Select(x => new Dictionary<string, object> { { "userId", x.UserId }, { "isOnline", x.IsOnline }, { "lastSeenUtc", x.LastSeenUtc }, { "activeCharacterId", x.ActiveCharacterId ?? string.Empty } })
            .Cast<object>().ToArray();
        return Ok("Presence loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope LockAcquire(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var entityType = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityType"), 2, 128, "entityType");
        var entityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityId"), 4, 128, "entityId");

        var existing = FindLockByEntityKey(entityType, entityId);
        var existingIsActive = existing != null && !existing.Deleted && !existing.Archived && existing.ExpiresUtc > DateTime.UtcNow;
        if (existingIsActive && existing!.LockedByUserId != actor.Id) throw new InvalidOperationException("Entity is already locked.");

        var lockItem = existing ?? new EntityLock { EntityType = entityType, EntityId = entityId, LockedByUserId = actor.Id };
        lockItem.LockedByUserId = actor.Id;
        lockItem.OwnerLevel = actor.Roles.Contains(UserRole.SuperAdmin) ? LockOwnerLevel.SuperAdmin : LockOwnerLevel.Admin;
        lockItem.IssuedUtc = DateTime.UtcNow;
        lockItem.ExpiresUtc = DateTime.UtcNow.AddHours(1);
        lockItem.Deleted = false;
        lockItem.Archived = false;
        if (existing == null) _repositories.Locks.Insert(lockItem); else _repositories.Locks.Replace(lockItem);
        _logger.Admin($"lock.acquire actor={actor.Login} entityType={entityType} entityId={entityId} result={(existing == null ? "new" : existingIsActive ? "refresh" : "reactivated")}");
        return Ok(existing == null || !existingIsActive ? "Lock acquired." : "Lock refreshed.", LockPayload(lockItem));
    }

    public ResponseEnvelope LockRelease(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var lockItem = RequireLockByEntity(context);
        if (lockItem.LockedByUserId != actor.Id && !actor.Roles.Contains(UserRole.SuperAdmin)) throw new UnauthorizedAccessException("Cannot release lock owned by another admin.");
        lockItem.Deleted = true; lockItem.Archived = true;
        _repositories.Locks.Replace(lockItem);
        _logger.Admin($"lock.release actor={actor.Login} entityType={lockItem.EntityType} entityId={lockItem.EntityId} result=ok");
        return Ok("Lock released.");
    }

    public ResponseEnvelope LockForceRelease(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.SuperAdmin);
        var lockItem = RequireLockByEntity(context);
        lockItem.Deleted = true;
        lockItem.Archived = true;
        _repositories.Locks.Replace(lockItem);
        WriteAudit("lock", actor.Id, "forceRelease", lockItem.Id);
        _logger.Admin($"lock.forceRelease actor={actor.Login} entityType={lockItem.EntityType} entityId={lockItem.EntityId} result=ok");
        return Ok("Lock force released.");
    }

    public ResponseEnvelope LockStatus(CommandContext context)
    {
        RequireAdmin(context);
        var entityType = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityType"), 2, 128, "entityType");
        var entityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityId"), 4, 128, "entityId");
        var lockItem = FindActiveLock(entityType, entityId);
        _logger.Admin($"lock.get actor={GetCurrentAccount(context).Login} entityType={entityType} entityId={entityId} result={(lockItem == null ? "free" : "locked")}");
        if (lockItem == null) return Ok("Lock is free.", new Dictionary<string, object> { { "isLocked", false } });
        return Ok("Lock is active.", new Dictionary<string, object> { { "isLocked", true }, { "lock", LockPayload(lockItem) } });
    }

    private EntityLock? FindActiveLock(string entityType, string entityId)
    {
        var lockItem = _repositories.Locks.Find(Builders<EntityLock>.Filter.Eq(x => x.EntityType, entityType) & Builders<EntityLock>.Filter.Eq(x => x.EntityId, entityId) & Builders<EntityLock>.Filter.Eq(x => x.Deleted, false)).FirstOrDefault();
        if (lockItem == null) return null;
        if (lockItem.ExpiresUtc <= DateTime.UtcNow)
        {
            lockItem.Deleted = true;
            _repositories.Locks.Replace(lockItem);
            return null;
        }

        return lockItem;
    }

    private EntityLock? FindLockByEntityKey(string entityType, string entityId)
    {
        return _repositories.Locks.Find(
            Builders<EntityLock>.Filter.Eq(x => x.EntityType, entityType) &
            Builders<EntityLock>.Filter.Eq(x => x.EntityId, entityId)).FirstOrDefault();
    }

    private EntityLock RequireLockByEntity(CommandContext context)
    {
        var entityType = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityType"), 2, 128, "entityType");
        var entityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityId"), 4, 128, "entityId");
        return FindActiveLock(entityType, entityId) ?? throw new KeyNotFoundException("Lock not found.");
    }

    private ResponseEnvelope SetCharacterArchiveState(CommandContext context, bool archive)
    {
        var actor = RequireAdmin(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        c.Archived = archive; c.Deleted = archive;
        _repositories.Characters.Replace(c);
        WriteAudit("character", actor.Id, archive ? "archive" : "restore", c.Id);
        return Ok(archive ? "Character archived." : "Character restored.");
    }

    private void TryShadowWrite(Func<System.Threading.Tasks.Task<ShadowWriteResult>> write)
    {
        try
        {
            _ = write().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.shadow.write.error profile=unknown characterId=unknown message={ex.Message}");
        }
    }

    private void TryWriteRaceBodyProfileShadowsAsync(Character character, string actorUserId, string requestId)
    {
        try
        {
            TryShadowWrite(() => _profileShadowWriteService.WriteRaceOrSpeciesProfileShadowAsync(character, actorUserId, requestId));
            TryShadowWrite(() => _profileShadowWriteService.WriteBodyProfileShadowAsync(character, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.shadow.write.error profile=raceBody characterId={character?.Id ?? string.Empty} message={ex.Message}");
        }
        // TODO(F0.5.15): publish character.profile.shadow_written sync events for race/body after profile events stabilize.
        // TODO(F0.5.15): add read-path-safe audit for race/body profile.shadow.write without duplicating legacy command audit.
    }

    private void TryWriteInventoryProfileShadowAsync(Character character, string actorUserId, string requestId, string reason)
    {
        var characterId = character?.Id ?? string.Empty;
        if (!IsInventoryProfileShadowWriteEnabled())
        {
            _logger.Debug($"inventory.shadow.write.skipped characterId={characterId} reason=flag_disabled command={reason}");
            return;
        }

        if (character == null)
        {
            _logger.Debug($"inventory.shadow.write.error characterId={characterId} command={reason} message=legacy_character_missing");
            return;
        }

        try
        {
            _logger.Debug($"inventory.shadow.write.start characterId={characterId} command={reason}");
            var result = _profileShadowWriteService.WriteInventoryProfileShadowAsync(character, actorUserId, requestId).GetAwaiter().GetResult();
            if (result.Success)
            {
                _logger.Debug($"inventory.shadow.write.done characterId={characterId} command={reason}");
                return;
            }

            _logger.Debug($"inventory.shadow.write.error characterId={characterId} command={reason} message={result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            _logger.Debug($"inventory.shadow.write.error characterId={characterId} command={reason} message={ex.Message}");
        }
        // TODO(F0.5.18): publish character.inventory.updated / character.profile.inventory.shadow_written after profile events stabilize.
    }

    private static bool IsInventoryProfileShadowWriteEnabled()
    {
        return ProfileFeatureFlags.UseRuleSetProfilesWriteShadow && ProfileFeatureFlags.UseInventoryProfileShadowWrite;
    }

    private static bool IsProfileFirstCharacterCreationEnabled()
    {
        return ProfileFeatureFlags.UseProfileFirstCharacterCreation;
    }

    private static bool IsProfileNativeStatsWriteEnabled()
    {
        return ProfileFeatureFlags.UseProfileNativeCharacterWrites && ProfileFeatureFlags.UseProfileNativeStatsWrite;
    }

    private static bool IsProfileNativeWalletWriteEnabled()
    {
        return ProfileFeatureFlags.UseProfileNativeCharacterWrites && ProfileFeatureFlags.UseProfileNativeWalletWrite;
    }

    private static bool IsProfileNativeSkillWriteEnabled()
    {
        return ProfileFeatureFlags.UseProfileNativeCharacterWrites && ProfileFeatureFlags.UseProfileNativeSkillWrite;
    }

    private static bool IsProfileNativeDevelopmentWriteEnabled()
    {
        return ProfileFeatureFlags.UseProfileNativeCharacterWrites && ProfileFeatureFlags.UseProfileNativeDevelopmentWrite;
    }

    private static bool IsProfileNativeInventoryWriteEnabled()
    {
        return ProfileFeatureFlags.UseProfileNativeCharacterWrites && ProfileFeatureFlags.UseProfileNativeInventoryWrite;
    }

    private static bool IsProfileNativeRaceBodyWriteEnabled()
    {
        return ProfileFeatureFlags.UseProfileNativeCharacterWrites && ProfileFeatureFlags.UseProfileNativeRaceBodyWrite;
    }

    private static bool IsProfileNativeRaceOrSpeciesWriteEnabled()
    {
        return IsProfileNativeRaceBodyWriteEnabled() && ProfileFeatureFlags.UseProfileNativeRaceOrSpeciesWrite;
    }

    private static bool IsProfileNativeBodyWriteEnabled()
    {
        return IsProfileNativeRaceBodyWriteEnabled() && ProfileFeatureFlags.UseProfileNativeBodyWrite;
    }

    private ProfileNativeRaceBodyWriteResult UpdateRaceBodyProfilesNativeForEnabledSections(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        var raceEnabled = IsProfileNativeRaceOrSpeciesWriteEnabled();
        var bodyEnabled = IsProfileNativeBodyWriteEnabled();
        if (raceEnabled && bodyEnabled)
        {
            return _profileNativeWriteService.UpdateRaceBodyProfilesNativeAsync(characterId, payload, actorUserId, requestId).GetAwaiter().GetResult();
        }

        if (raceEnabled)
        {
            return _profileNativeWriteService.UpdateRaceOrSpeciesProfileNativeAsync(characterId, payload, actorUserId, requestId).GetAwaiter().GetResult();
        }

        if (bodyEnabled)
        {
            return _profileNativeWriteService.UpdateBodyProfileNativeAsync(characterId, payload, actorUserId, requestId).GetAwaiter().GetResult();
        }

        return new ProfileNativeRaceBodyWriteResult { CharacterId = characterId ?? string.Empty, UsedFallback = true, ErrorMessage = "flag_disabled", WrittenAtUtc = DateTime.UtcNow };
    }

    private bool CanViewCharacter(UserAccount actor, UserAccount owner, Character character)
    {
        if (actor.Id == owner.Id) return true;
        if (actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin)) return true;
        if (character.Deleted) return false;
        return true;
    }

    private static bool IsAdminActor(UserAccount actor)
    {
        return actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
    }

    private Dictionary<string, object> CharacterDetailsPayloadWithProfileFirst(Character c, UserAccount owner, UserAccount viewer, string requestId)
    {
        if (c == null) throw new InvalidOperationException("Character not found.");

        var identityShell = _characterDetailsProfileBuilder.BuildProfileIdentityShell(c);

        _logger.Debug($"profile.details.profile_path.start characterId={c.Id} requestId={requestId}");
        try
        {
            var result = _characterDetailsProfileBuilder
                .BuildFromProfilesAsync(c, viewer.Id, requestId, identityShell)
                .GetAwaiter()
                .GetResult();

            if (result == null || result.Payload == null)
                throw new InvalidOperationException("Профиль персонажа недоступен. Выполните восстановление профиля.");

            if (result.UsedFallback || !result.UsedProfileFirst || result.MissingSections.Count > 0)
            {
                _logger.Debug($"profile.details.profile_required characterId={c.Id} requestId={requestId} missing={string.Join(",", result.MissingSections)} error={result.ErrorMessage}");
                throw new InvalidOperationException("Профиль персонажа требует явной миграции или восстановления.");
            }

            _logger.Debug($"profile.details.profile_path.done characterId={c.Id} requestId={requestId} profileFirst={result.UsedProfileFirst} fallback={result.UsedFallback}");
            ApplyPlayerSafeCharacterPayload(result.Payload, viewer);
            result.Payload["selectedTitle"] = CharacterSelectedTitleDisplay02111(c.Id, playerSafe: !IsAdmin(viewer));
            result.Payload["publicProfileRevision"] = _mongo.CharacterBodyProfiles.Find(x => x.CharacterId == c.Id).FirstOrDefault()?.EntityRevision ?? 0;
            return result.Payload;
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.details.error characterId={c.Id} requestId={requestId} message={ex.Message}");
            _logger.Debug($"profile.details.profile_path.done characterId={c.Id} requestId={requestId} profileFirst=false fallback=false reason=profile_required");
            throw new InvalidOperationException("Профиль персонажа временно недоступен. Обратитесь к мастеру.", ex);
        }
    }

    private string CharacterSelectedTitleDisplay02111(string characterId, bool playerSafe)
    {
        var profile = _mongo.CharacterTitleProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault();
        if (profile == null || string.IsNullOrWhiteSpace(profile.SelectedTitleId)
            || !profile.Entitlements.Any(x => !x.IsRevoked && string.Equals(x.TitleId, profile.SelectedTitleId, StringComparison.Ordinal)))
            return string.Empty;
        var definition = CharacterTitleDefinitions02111(profile.RuleSetId)
            .FirstOrDefault(x => string.Equals(x.DefinitionId, profile.SelectedTitleId, StringComparison.Ordinal)
                                 && !x.IsArchived && (!playerSafe || x.IsPlayerVisible));
        return definition?.DisplayName ?? string.Empty;
    }

    private void ApplyPlayerSafeCharacterPayload(Dictionary<string, object> payload, UserAccount viewer)
    {
        if (payload == null || IsAdmin(viewer)) return;
        if (!payload.ContainsKey("inventory")) return;

        var visibleItems = ToInventoryObjectList(payload["inventory"])
            .Select(AsDictionary)
            .Where(x => x != null && IsInventoryPayloadVisibleToPlayer(x))
            .Select(x => (object)x!)
            .ToArray();

        payload["inventory"] = visibleItems;
        _logger.Debug($"character.inventory.player_safe.filtered viewer={viewer?.Login ?? string.Empty} visibleCount={visibleItems.Length}");
    }

    private static bool IsInventoryPayloadVisibleToPlayer(Dictionary<string, object> item)
    {
        if (item == null) return false;
        if (item.ContainsKey("isPlayerVisible") && !PayloadReader.GetBool(item, "isPlayerVisible")) return false;
        var visibilityMode = (PayloadReader.GetString(item, "visibilityMode") ?? string.Empty).Trim();
        if (string.Equals(visibilityMode, "gm_only", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(visibilityMode, "hidden", StringComparison.OrdinalIgnoreCase)) return false;
        if (PayloadReader.GetBool(item, "gmOnly")) return false;
        if (PayloadReader.GetBool(item, "isHidden")) return false;
        if (PayloadReader.GetBool(item, "archived")) return false;
        if (PayloadReader.GetBool(item, "deleted")) return false;
        return true;
    }

    private static IEnumerable<object> ToInventoryObjectList(object? value)
    {
        if (value == null) return Array.Empty<object>();
        if (value is IEnumerable enumerable && value is not string) return enumerable.Cast<object>();
        return new[] { value };
    }

    private static Dictionary<string, object>? AsDictionary(object value)
    {
        if (value is Dictionary<string, object> dictionary) return dictionary;
        if (value is IDictionary raw)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in raw)
            {
                if (entry.Key == null) continue;
                result[Convert.ToString(entry.Key) ?? string.Empty] = entry.Value ?? string.Empty;
            }
            return result;
        }

        return null;
    }

    private static Dictionary<string, object> StatsPayload(CharacterStats s) => new Dictionary<string, object>
    {
        { "health", s.Health }, { "physicalArmor", s.PhysicalArmor }, { "magicalArmor", s.MagicalArmor }, { "morale", s.Morale },
        { "strength", s.Strength }, { "dexterity", s.Dexterity }, { "endurance", s.Endurance }, { "wisdom", s.Wisdom }, { "intellect", s.Intellect }, { "charisma", s.Charisma }
    };

    private static Dictionary<string, object> WalletPayload(Wallet w)
    {
        w.EnsureAllDenominations();
        return Enum.GetValues(typeof(CurrencyDenomination)).Cast<CurrencyDenomination>()
            .ToDictionary(k => k.ToString(), k => (object)(w.Balance.Amounts.ContainsKey(k.ToString()) ? w.Balance.Amounts[k.ToString()] : 0L));
    }

    private static Dictionary<string, object> InventoryPayload(InventoryItem x) => new Dictionary<string, object>
    {
        { "id", x.Id },
        { "itemCode", x.ItemCode },
        { "definitionId", x.ItemCode },
        { "itemDefinitionId", x.ItemCode },
        { "definitionCode", x.ItemCode },
        { "name", string.IsNullOrWhiteSpace(x.Name) ? x.Label : x.Name },
        { "label", string.IsNullOrWhiteSpace(x.Label) ? x.Name : x.Label },
        { "displayName", string.IsNullOrWhiteSpace(x.Name) ? x.Label : x.Name },
        { "description", x.Description },
        { "category", x.Category },
        { "quantity", x.Quantity },
        { "durabilityOrHealth", x.DurabilityOrHealth ?? x.Durability ?? 0 },
        { "durability", x.Durability ?? x.DurabilityOrHealth ?? 0 },
        { "isEquipped", x.IsEquipped || x.Equipped },
        { "equipped", x.Equipped || x.IsEquipped },
        { "usesAmmoOrConsumable", x.UsesAmmoOrConsumable },
        { "consumptionPerUse", x.ConsumptionPerUse ?? 0 },
        { "properties", x.Properties },
        { "notes", x.Notes },
        { "archived", x.Archived },
        { "deleted", x.Deleted }
    };

    private static Dictionary<string, object> CompanionPayload(Companion c) => new Dictionary<string, object>
    {
        { "id", c.Id },
        { "name", c.Name },
        { "type", c.Type },
        { "species", c.Species },
        { "description", c.Description },
        { "notes", c.Notes },
        { "ownerCharacterId", c.OwnerCharacterId },
        { "statsSummary", c.StatsSummary },
        { "isArchived", c.IsArchived },
        { "inventory", c.Inventory.Select(InventoryPayload).Cast<object>().ToArray() },
        { "holdings", c.Holdings.Select(HoldingPayload).Cast<object>().ToArray() },
        { "reputation", c.Reputation.Select(ReputationPayload).Cast<object>().ToArray() }
    };

    private ReputationProfile LoadReputationProfile(string characterId)
    {
        var doc = _mongo.CharacterReputationProfiles.Find(Builders<CharacterReputationProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? new ReputationProfile { CharacterId = characterId, RuleSetId = RuleSetIds.FantasyNriDefault, Entries = new List<CharacterReputationProfileValue>(), SchemaVersion = 1 };
    }

    private HoldingsProfile LoadHoldingsProfile(string characterId)
    {
        var doc = _mongo.CharacterHoldingsProfiles.Find(Builders<CharacterHoldingsProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? new HoldingsProfile { CharacterId = characterId, RuleSetId = RuleSetIds.FantasyNriDefault, Holdings = new List<CharacterHoldingProfileValue>(), SchemaVersion = 1 };
    }

    private CompanionProfile LoadCompanionProfile(string characterId)
    {
        var doc = _mongo.CharacterCompanionProfiles.Find(Builders<CharacterCompanionProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? new CompanionProfile { CharacterId = characterId, RuleSetId = RuleSetIds.FantasyNriDefault, Companions = new List<CharacterCompanionProfileValue>(), SchemaVersion = 1 };
    }

    private void SaveReputationProfile(string characterId, ReputationProfile profile)
    {
        profile.CharacterId = characterId;
        profile.RuleSetId = FirstNonEmpty(profile.RuleSetId, RuleSetIds.FantasyNriDefault);
        profile.Entries ??= new List<CharacterReputationProfileValue>();
        UpsertProfile(_mongo.CharacterReputationProfiles, characterId, new CharacterReputationProfileDocument { CharacterId = characterId, Profile = profile });
    }

    private void SaveHoldingsProfile(string characterId, HoldingsProfile profile)
    {
        profile.CharacterId = characterId;
        profile.RuleSetId = FirstNonEmpty(profile.RuleSetId, RuleSetIds.FantasyNriDefault);
        profile.Holdings ??= new List<CharacterHoldingProfileValue>();
        UpsertProfile(_mongo.CharacterHoldingsProfiles, characterId, new CharacterHoldingsProfileDocument { CharacterId = characterId, Profile = profile });
    }

    private void SaveCompanionProfile(string characterId, CompanionProfile profile)
    {
        profile.CharacterId = characterId;
        profile.RuleSetId = FirstNonEmpty(profile.RuleSetId, RuleSetIds.FantasyNriDefault);
        profile.Companions ??= new List<CharacterCompanionProfileValue>();
        UpsertProfile(_mongo.CharacterCompanionProfiles, characterId, new CharacterCompanionProfileDocument { CharacterId = characterId, Profile = profile });
    }

    private static void UpsertProfile<TDoc>(IMongoCollection<TDoc> collection, string characterId, TDoc doc) where TDoc : EntityBase
    {
        var existing = collection.Find(Builders<TDoc>.Filter.Eq("CharacterId", characterId)).FirstOrDefault();
        if (existing != null)
        {
            doc.Id = existing.Id;
            doc.CreatedUtc = existing.CreatedUtc;
        }

        doc.UpdatedUtc = DateTime.UtcNow;
        collection.ReplaceOne(Builders<TDoc>.Filter.Eq("CharacterId", characterId), doc, new ReplaceOptions { IsUpsert = true });
    }

    private CharacterReputationProfileValue ParseReputationProfileValue(Dictionary<string, object> source)
    {
        var targetName = RequireLength(FirstNonEmpty(PayloadReader.GetString(source, "targetName"), PayloadReader.GetString(source, "name"), PayloadReader.GetString(source, "groupKey")), 1, 128, "targetName");
        var targetId = RequireLength(PayloadReader.GetString(source, "targetId"), 0, 128, "targetId");
        var id = FirstNonEmpty(PayloadReader.GetString(source, "id"), PayloadReader.GetString(source, "entryId"), targetId, Guid.NewGuid().ToString("N"));
        return new CharacterReputationProfileValue
        {
            EntryId = id,
            Scope = FirstNonEmpty(PayloadReader.GetString(source, "scope"), "Personal"),
            ScopeType = FirstNonEmpty(PayloadReader.GetString(source, "scopeType"), "Character"),
            TargetType = FirstNonEmpty(PayloadReader.GetString(source, "targetType"), "Other"),
            TargetId = targetId,
            Name = targetName,
            Value = PayloadReader.GetInt(source, "value") ?? 0,
            GroupValue = PayloadReader.GetInt(source, "groupValue") ?? 0,
            Status = RequireLength(PayloadReader.GetString(source, "status"), 0, 64, "status"),
            Notes = RequireLength(PayloadReader.GetString(source, "notes"), 0, 1024, "notes"),
            IsPlayerVisible = GetBoolDefault(source, "isPlayerVisible", !PayloadReader.GetBool(source, "isHiddenForOthers")),
            IsArchived = PayloadReader.GetBool(source, "archived") || PayloadReader.GetBool(source, "isArchived"),
            Source = "character_v2_profile_native"
        };
    }

    private CharacterHoldingProfileValue ParseHoldingProfileValue(Dictionary<string, object> source, string characterId)
    {
        var owners = (PayloadReader.GetList(source, "owners") ?? new List<object>()).Select(x => x?.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var ownerCharacters = owners.Count == 0 ? new List<string> { characterId } : owners;
        return new CharacterHoldingProfileValue
        {
            HoldingId = FirstNonEmpty(PayloadReader.GetString(source, "id"), PayloadReader.GetString(source, "holdingId"), Guid.NewGuid().ToString("N")),
            Name = RequireLength(PayloadReader.GetString(source, "name"), 1, 128, "name"),
            HoldingType = RequireLength(FirstNonEmpty(PayloadReader.GetString(source, "type"), PayloadReader.GetString(source, "holdingType")), 0, 64, "type"),
            Description = RequireLength(PayloadReader.GetString(source, "description"), 0, 1024, "description"),
            LocationId = RequireLength(PayloadReader.GetString(source, "locationId"), 0, 128, "locationId"),
            LocationName = RequireLength(FirstNonEmpty(PayloadReader.GetString(source, "locationName"), PayloadReader.GetString(source, "location")), 0, 128, "locationName"),
            OwnerCharacterIds = ownerCharacters,
            OwnerDisplayName = RequireLength(PayloadReader.GetString(source, "ownerDisplayName"), 0, 128, "ownerDisplayName"),
            LegalStatus = RequireLength(PayloadReader.GetString(source, "legalStatus"), 0, 64, "legalStatus"),
            ActualStatus = RequireLength(FirstNonEmpty(PayloadReader.GetString(source, "actualStatus"), PayloadReader.GetString(source, "status")), 0, 64, "status"),
            Notes = RequireLength(PayloadReader.GetString(source, "notes"), 0, 1024, "notes"),
            IsPlayerVisible = GetBoolDefault(source, "isPlayerVisible", true),
            IsArchived = PayloadReader.GetBool(source, "archived") || PayloadReader.GetBool(source, "isArchived"),
            Source = "character_v2_profile_native"
        };
    }

    private CharacterCompanionProfileValue ParseCompanionProfileValue(Dictionary<string, object> source, string characterId)
    {
        var type = RequireLength(FirstNonEmpty(PayloadReader.GetString(source, "type"), PayloadReader.GetString(source, "species"), PayloadReader.GetString(source, "companionType")), 0, 64, "type");
        var resourceMaximums = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var resourceMaximumPayload = PayloadReader.GetDictionary(source, "resourceMaximums");
        if (resourceMaximumPayload != null)
        {
            foreach (var pair in resourceMaximumPayload)
                if (int.TryParse(Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture), out var maximum) && maximum >= 0)
                    resourceMaximums[pair.Key] = maximum;
        }
        return new CharacterCompanionProfileValue
        {
            CompanionId = FirstNonEmpty(PayloadReader.GetString(source, "id"), PayloadReader.GetString(source, "companionId"), Guid.NewGuid().ToString("N")),
            Name = RequireLength(PayloadReader.GetString(source, "name"), 1, 128, "name"),
            CompanionType = type,
            RaceOrSpeciesId = FirstNonEmpty(PayloadReader.GetString(source, "raceOrSpeciesId"), type),
            Description = RequireLength(PayloadReader.GetString(source, "description"), 0, 1024, "description"),
            Notes = RequireLength(PayloadReader.GetString(source, "notes"), 0, 1024, "notes"),
            OwnerCharacterId = RequireLength(FirstNonEmpty(PayloadReader.GetString(source, "ownerCharacterId"), characterId), 0, 128, "ownerCharacterId"),
            OwnerDisplayName = RequireLength(PayloadReader.GetString(source, "ownerDisplayName"), 0, 128, "ownerDisplayName"),
            Status = RequireLength(PayloadReader.GetString(source, "status"), 0, 64, "status"),
            ResourceMaximums = resourceMaximums,
            InitiativeMode = RequireLength(PayloadReader.GetString(source, "initiativeMode"), 0, 64, "initiativeMode"),
            HasSeparateInventory = PayloadReader.GetBool(source, "hasSeparateInventory"),
            IsPlayerVisible = GetBoolDefault(source, "isPlayerVisible", true),
            IsArchived = PayloadReader.GetBool(source, "archived") || PayloadReader.GetBool(source, "isArchived"),
            Source = "character_v2_profile_native"
        };
    }

    private static Dictionary<string, object> ReputationProfilePayload(CharacterReputationProfileValue x) => new Dictionary<string, object>
    {
        { "id", x.EntryId },
        { "scope", x.Scope },
        { "scopeType", x.ScopeType },
        { "groupKey", string.Equals(x.ScopeType, "Group", StringComparison.OrdinalIgnoreCase) ? x.Name : string.Empty },
        { "targetType", x.TargetType },
        { "targetId", x.TargetId },
        { "targetName", x.Name },
        { "value", x.Value },
        { "groupValue", x.GroupValue },
        { "status", x.Status },
        { "notes", x.Notes },
        { "isPlayerVisible", x.IsPlayerVisible },
        { "isHiddenForOthers", !x.IsPlayerVisible },
        { "archived", x.IsArchived },
        { "isArchived", x.IsArchived },
        { "source", x.Source }
    };

    private static Dictionary<string, object> HoldingProfilePayload(CharacterHoldingProfileValue x) => new Dictionary<string, object>
    {
        { "id", x.HoldingId },
        { "name", x.Name },
        { "type", x.HoldingType },
        { "holdingType", x.HoldingType },
        { "description", x.Description },
        { "locationId", x.LocationId },
        { "locationName", x.LocationName },
        { "owners", (x.OwnerCharacterIds ?? new List<string>()).Concat(x.OwnerUserIds ?? new List<string>()).Cast<object>().ToArray() },
        { "ownerDisplayName", x.OwnerDisplayName },
        { "status", FirstNonEmpty(x.ActualStatus, x.LegalStatus) },
        { "legalStatus", x.LegalStatus },
        { "actualStatus", x.ActualStatus },
        { "notes", x.Notes },
        { "isPlayerVisible", x.IsPlayerVisible },
        { "archived", x.IsArchived },
        { "isArchived", x.IsArchived },
        { "source", x.Source }
    };

    private static Dictionary<string, object> CompanionProfilePayload(CharacterCompanionProfileValue x) => new Dictionary<string, object>
    {
        { "id", x.CompanionId },
        { "name", x.Name },
        { "type", FirstNonEmpty(x.CompanionType, x.RaceOrSpeciesId) },
        { "species", FirstNonEmpty(x.RaceOrSpeciesId, x.CompanionType) },
        { "description", x.Description },
        { "notes", x.Notes },
        { "ownerCharacterId", x.OwnerCharacterId },
        { "ownerDisplayName", x.OwnerDisplayName },
        { "status", x.Status },
        { "isPlayerVisible", x.IsPlayerVisible },
        { "isArchived", x.IsArchived },
        { "archived", x.IsArchived },
        { "inventory", Array.Empty<object>() },
        { "holdings", Array.Empty<object>() },
        { "reputation", Array.Empty<object>() },
        { "source", x.Source }
    };

    private static bool GetBoolDefault(Dictionary<string, object> source, string key, bool defaultValue)
    {
        return source.ContainsKey(key) ? PayloadReader.GetBool(source, key) : defaultValue;
    }

    private static Dictionary<string, object> HoldingPayload(HoldingRef x) => new Dictionary<string, object>
    {
        { "id", x.Id },
        { "name", x.Name },
        { "type", x.Type },
        { "description", x.Description },
        { "owners", x.Owners.Cast<object>().ToArray() },
        { "notes", x.Notes },
        { "archived", x.Archived },
        { "isArchived", x.Archived }
    };

    private static Dictionary<string, object> ReputationPayload(ReputationRef x) => new Dictionary<string, object>
    {
        { "id", x.Id },
        { "scope", x.Scope },
        { "scopeType", x.ScopeType.ToString() },
        { "groupKey", x.GroupKey },
        { "targetType", x.TargetType.ToString() },
        { "targetName", x.TargetName },
        { "value", x.Value },
        { "notes", x.Notes },
        { "isHiddenForOthers", x.IsHiddenForOthers },
        { "archived", x.Archived },
        { "isArchived", x.Archived }
    };

    private List<InventoryItem> ParseInventoryList(IList<object>? list)
    {
        if (list == null) return new List<InventoryItem>();
        return list.OfType<Dictionary<string, object>>().Select(ParseInventoryItem).ToList();
    }

    private List<ReputationRef> ParseReputationList(IList<object>? list)
    {
        if (list == null) return new List<ReputationRef>();
        return list.OfType<Dictionary<string, object>>().Select(item => new ReputationRef
        {
            Id = PayloadReader.GetString(item, "id") ?? Guid.NewGuid().ToString("N"),
            Scope = RequireLength(PayloadReader.GetString(item, "scope"), 3, 32, "scope"),
            ScopeType = ParseScopeType(PayloadReader.GetString(item, "scopeType")),
            GroupKey = RequireLength(PayloadReader.GetString(item, "groupKey"), 0, 128, "groupKey"),
            TargetType = ParseTargetType(PayloadReader.GetString(item, "targetType")),
            TargetName = RequireLength(PayloadReader.GetString(item, "targetName"), 0, 128, "targetName"),
            Value = RequireRange(PayloadReader.GetInt(item, "value"), -9999, 9999, "value"),
            Notes = RequireLength(PayloadReader.GetString(item, "notes"), 0, 1024, "notes"),
            IsHiddenForOthers = PayloadReader.GetBool(item, "isHiddenForOthers"),
            Archived = PayloadReader.GetBool(item, "archived") || PayloadReader.GetBool(item, "isArchived")
        }).ToList();
    }

    private List<HoldingRef> ParseHoldingsList(IList<object>? list)
    {
        if (list == null) return new List<HoldingRef>();
        return list.OfType<Dictionary<string, object>>().Select(item => new HoldingRef
        {
            Id = PayloadReader.GetString(item, "id") ?? Guid.NewGuid().ToString("N"),
            Name = RequireLength(PayloadReader.GetString(item, "name"), 1, 128, "name"),
            Type = RequireLength(PayloadReader.GetString(item, "type"), 0, 64, "type"),
            Description = RequireLength(PayloadReader.GetString(item, "description"), 0, 512, "description"),
            Owners = (PayloadReader.GetList(item, "owners") ?? new List<object>()).Select(x => x?.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
            Notes = RequireLength(PayloadReader.GetString(item, "notes"), 0, 1024, "notes"),
            Archived = PayloadReader.GetBool(item, "archived") || PayloadReader.GetBool(item, "isArchived")
        }).ToList();
    }

    private InventoryItem ParseInventoryItem(Dictionary<string, object> item)
    {
        var durabilityOrHealth = PayloadReader.GetInt(item, "durabilityOrHealth");
        var durability = PayloadReader.GetInt(item, "durability");
        var isEquipped = PayloadReader.GetBool(item, "isEquipped") || PayloadReader.GetBool(item, "equipped");
        var name = PayloadReader.GetString(item, "name") ?? string.Empty;
        var label = PayloadReader.GetString(item, "label") ?? string.Empty;
        return new InventoryItem
        {
            Id = PayloadReader.GetString(item, "id") ?? Guid.NewGuid().ToString("N"),
            ItemCode = PayloadReader.GetString(item, "itemCode") ?? string.Empty,
            Name = RequireLength(name, 0, 128, "name"),
            Label = RequireLength(string.IsNullOrWhiteSpace(label) ? name : label, 0, 128, "label"),
            Description = RequireLength(PayloadReader.GetString(item, "description"), 0, 1024, "description"),
            Category = RequireLength(PayloadReader.GetString(item, "category"), 0, 64, "category"),
            Quantity = RequireRange(PayloadReader.GetInt(item, "quantity"), 0, 100000, "quantity"),
            DurabilityOrHealth = durabilityOrHealth ?? durability,
            Durability = durability ?? durabilityOrHealth,
            IsEquipped = isEquipped,
            Equipped = isEquipped,
            UsesAmmoOrConsumable = PayloadReader.GetBool(item, "usesAmmoOrConsumable"),
            ConsumptionPerUse = PayloadReader.GetInt(item, "consumptionPerUse"),
            Properties = RequireLength(PayloadReader.GetString(item, "properties"), 0, 2048, "properties"),
            Notes = RequireLength(PayloadReader.GetString(item, "notes"), 0, 1024, "notes"),
            Archived = PayloadReader.GetBool(item, "archived"),
            Deleted = PayloadReader.GetBool(item, "deleted")
        };
    }

    private Companion ParseCompanion(Dictionary<string, object> source) => new Companion
    {
        Id = PayloadReader.GetString(source, "id") ?? Guid.NewGuid().ToString("N"),
        Name = RequireLength(PayloadReader.GetString(source, "name"), 1, 128, "name"),
        Type = RequireLength(FirstNonEmpty(PayloadReader.GetString(source, "type"), PayloadReader.GetString(source, "species")), 0, 64, "type"),
        Species = RequireLength(FirstNonEmpty(PayloadReader.GetString(source, "species"), PayloadReader.GetString(source, "type")), 0, 64, "species"),
        Description = RequireLength(PayloadReader.GetString(source, "description"), 0, 1024, "description"),
        Notes = RequireLength(PayloadReader.GetString(source, "notes"), 0, 1024, "notes"),
        OwnerCharacterId = RequireLength(PayloadReader.GetString(source, "ownerCharacterId"), 0, 128, "ownerCharacterId"),
        StatsSummary = RequireLength(PayloadReader.GetString(source, "statsSummary"), 0, 512, "statsSummary"),
        IsArchived = PayloadReader.GetBool(source, "isArchived"),
        Inventory = ParseInventoryList(PayloadReader.GetList(source, "inventory")),
        Holdings = ParseHoldingsList(PayloadReader.GetList(source, "holdings")),
        Reputation = ParseReputationList(PayloadReader.GetList(source, "reputation"))
    };

    private HoldingRef ParseHolding(Dictionary<string, object> source) => ParseHoldingsList(new List<object> { source }).First();
    private ReputationRef ParseReputationEntry(Dictionary<string, object> source) => ParseReputationList(new List<object> { source }).First();
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    private static object[] PlayerSafeInventoryPayload(object? value)
    {
        if (value == null || value is string) return Array.Empty<object>();
        if (value is not IEnumerable enumerable) return Array.Empty<object>();

        var result = new List<object>();
        foreach (var item in enumerable)
        {
            var map = ToStringObjectDictionary(item);
            if (map.Count == 0) continue;

            map.Remove("itemCode");
            map.Remove("definitionId");
            map.Remove("itemDefinitionId");
            map.Remove("definitionCode");
            map.Remove("gmNotes");
            map.Remove("serverOnlyData");
            map.Remove("notes");

            result.Add(map);
        }

        return result.ToArray();
    }

    private static object[] PlayerSafeCharacterCollectionPayload(object? value)
    {
        if (value == null || value is string) return Array.Empty<object>();
        if (value is not IEnumerable enumerable) return Array.Empty<object>();

        var result = new List<object>();
        foreach (var item in enumerable)
        {
            var map = ToStringObjectDictionary(item);
            if (map.Count == 0) continue;

            var isVisible = !map.TryGetValue("isPlayerVisible", out var visibleValue) || ToBool(visibleValue, defaultValue: true);
            var hidden = map.TryGetValue("isHiddenForOthers", out var hiddenValue) && ToBool(hiddenValue, defaultValue: false);
            var archived = (map.TryGetValue("isArchived", out var archivedValue) && ToBool(archivedValue, defaultValue: false))
                || (map.TryGetValue("archived", out var archivedAltValue) && ToBool(archivedAltValue, defaultValue: false));

            if (!isVisible || hidden || archived) continue;

            map.Remove("gmNotes");
            map.Remove("serverOnlyData");
            map.Remove("source");
            result.Add(map);
        }

        return result.ToArray();
    }

    private static bool ToBool(object? value, bool defaultValue)
    {
        if (value == null) return defaultValue;
        if (value is bool b) return b;
        if (value is string s && bool.TryParse(s, out var parsed)) return parsed;
        if (value is int i) return i != 0;
        if (value is long l) return l != 0;
        return defaultValue;
    }

    private static Dictionary<string, object> ToStringObjectDictionary(object? value)
    {
        if (value is Dictionary<string, object> typed)
            return new Dictionary<string, object>(typed, StringComparer.OrdinalIgnoreCase);
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key == null) continue;
                result[Convert.ToString(entry.Key) ?? string.Empty] = entry.Value ?? string.Empty;
            }
            return result;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static int CountPayloadItems(object? value)
    {
        if (value == null || value is string) return 0;
        if (value is IDictionary<string, object> dict) return dict.Count;
        if (value is ICollection collection) return collection.Count;
        return 1;
    }
    private static ReputationScopeType ParseScopeType(string? value) => Enum.TryParse<ReputationScopeType>(value, true, out var parsed) ? parsed : ReputationScopeType.Character;
    private static ReputationTargetType ParseTargetType(string? value) => Enum.TryParse<ReputationTargetType>(value, true, out var parsed) ? parsed : ReputationTargetType.Other;

    private Dictionary<string, object> BuildCharacterAggregatePayload(Character character, UserAccount viewer, bool includeNotesContext)
    {
        var owner = GetAccount(character.OwnerUserId);
        var payload = CharacterDetailsPayloadWithProfileFirst(character, owner, viewer, string.Empty);
        if (!includeNotesContext) payload["notesContext"] = new Dictionary<string, object> { { "scopes", Array.Empty<object>() }, { "noteLinks", Array.Empty<object>() } };
        return payload;
    }

    private void EnsureCharacterDefaults(Character character)
    {
        character.Visibility ??= new CharacterVisibilitySettings();
        character.Stats ??= new CharacterStats();
        character.Wallet ??= new Wallet();
        character.Wallet.EnsureAllDenominations();
        character.Inventory ??= new List<InventoryItem>();
        character.Companions ??= new List<Companion>();
        character.Holdings ??= new List<HoldingRef>();
        character.Reputation ??= new List<ReputationRef>();
        foreach (var item in character.Inventory)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(item.Name)) item.Name = item.Label;
            if (string.IsNullOrWhiteSpace(item.Label)) item.Label = item.Name;
            item.IsEquipped = item.IsEquipped || item.Equipped;
            item.Equipped = item.IsEquipped;
            item.DurabilityOrHealth ??= item.Durability;
            item.Durability ??= item.DurabilityOrHealth;
        }
        foreach (var companion in character.Companions)
        {
            if (string.IsNullOrWhiteSpace(companion.Id)) companion.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(companion.OwnerCharacterId)) companion.OwnerCharacterId = character.Id;
            if (string.IsNullOrWhiteSpace(companion.Type)) companion.Type = companion.Species;
            companion.Inventory ??= new List<InventoryItem>();
            companion.Holdings ??= new List<HoldingRef>();
            companion.Reputation ??= new List<ReputationRef>();
            foreach (var item in companion.Inventory)
            {
                if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(item.Name)) item.Name = item.Label;
                if (string.IsNullOrWhiteSpace(item.Label)) item.Label = item.Name;
            }
            foreach (var holding in companion.Holdings)
            {
                if (string.IsNullOrWhiteSpace(holding.Id)) holding.Id = Guid.NewGuid().ToString("N");
                holding.Owners ??= new List<string>();
            }
            foreach (var reputation in companion.Reputation)
            {
                if (string.IsNullOrWhiteSpace(reputation.Id)) reputation.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(reputation.TargetName)) reputation.TargetName = reputation.GroupKey;
            }
        }
        foreach (var holding in character.Holdings)
        {
            if (string.IsNullOrWhiteSpace(holding.Id)) holding.Id = Guid.NewGuid().ToString("N");
            holding.Owners ??= new List<string>();
        }
        foreach (var reputation in character.Reputation)
        {
            if (string.IsNullOrWhiteSpace(reputation.Id)) reputation.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(reputation.TargetName)) reputation.TargetName = reputation.GroupKey;
        }
        character.ClassProgress ??= new List<CharacterClassProgress>();
        character.Skills ??= new List<SkillState>();
        character.CharacterClasses ??= new List<CharacterClassState>();
        character.CharacterSkills ??= new List<CharacterSkillState>();
        if (string.IsNullOrWhiteSpace(character.RaceCode) && !string.IsNullOrWhiteSpace(character.Race))
        {
            character.RaceCode = character.Race.Trim();
        }

        if (character.XpCoins < 0) character.XpCoins = 0;
    }

    private Character ResolveOwnedCharacter(CommandContext context, UserAccount actor)
    {
        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId");
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            var character = GetCharacter(RequireLength(characterId, 8, 128, "characterId"));
            if (!string.Equals(character.OwnerUserId, actor.Id, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Character unavailable.");
            return character;
        }

        var active = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, actor.Id)).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(active?.ActiveCharacterId))
        {
            var selected = _repositories.Characters.GetById(active.ActiveCharacterId);
            if (selected != null && selected.OwnerUserId == actor.Id && !selected.Deleted) return selected;
        }

        return _repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.OwnerUserId, actor.Id)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Character not found.");
    }

    private void EnsureCharacterEditAllowed(UserAccount actor, string characterId)
    {
        var lockItem = FindActiveLock("character", characterId);
        if (lockItem == null)
        {
            _logger.Admin($"character.validation.denied command=save characterId={characterId} actor={actor.Login} reason=lock-missing");
            throw new UnauthorizedAccessException("Character lock is required for admin save.");
        }

        if (lockItem.LockedByUserId != actor.Id && !actor.Roles.Contains(UserRole.SuperAdmin))
        {
            _logger.Admin($"character.validation.denied command=save characterId={characterId} actor={actor.Login} reason=lock-owner-mismatch");
            throw new UnauthorizedAccessException("Character is locked by another admin.");
        }
    }

    private void ApplyStatsFromPayload(Character character, Dictionary<string, object> payload)
    {
        character.Stats.Health = RequireRange(PayloadReader.GetInt(payload, "health"), 0, 999, "health");
        character.Stats.PhysicalArmor = RequireRange(PayloadReader.GetInt(payload, "physicalArmor"), 0, 999, "physicalArmor");
        character.Stats.MagicalArmor = RequireRange(PayloadReader.GetInt(payload, "magicalArmor"), 0, 999, "magicalArmor");
        character.Stats.Morale = RequireRange(PayloadReader.GetInt(payload, "morale"), 0, 999, "morale");
        character.Stats.Strength = RequireRange(PayloadReader.GetInt(payload, "strength"), 0, 999, "strength");
        character.Stats.Dexterity = RequireRange(PayloadReader.GetInt(payload, "dexterity"), 0, 999, "dexterity");
        character.Stats.Endurance = RequireRange(PayloadReader.GetInt(payload, "endurance"), 0, 999, "endurance");
        character.Stats.Wisdom = RequireRange(PayloadReader.GetInt(payload, "wisdom"), 0, 999, "wisdom");
        character.Stats.Intellect = RequireRange(PayloadReader.GetInt(payload, "intellect"), 0, 999, "intellect");
        character.Stats.Charisma = RequireRange(PayloadReader.GetInt(payload, "charisma"), 0, 999, "charisma");
    }

    private void ApplyMoneyFromPayload(Character character, Dictionary<string, object> payload)
    {
        character.Wallet.EnsureAllDenominations();
        var moneyRaw = PayloadReader.GetDictionary(payload, "money") ?? payload;
        foreach (CurrencyDenomination denomination in Enum.GetValues(typeof(CurrencyDenomination)))
        {
            var value = PayloadReader.GetLong(moneyRaw, denomination.ToString());
            if (!value.HasValue) continue;
            if (value.Value < 0)
            {
                _logger.Admin($"character.validation.denied command=save.money characterId={character.Id} reason=currency-negative currency={denomination}");
                throw new ArgumentException($"currency {denomination} must be >= 0.");
            }

            character.Wallet.Balance.Amounts[denomination.ToString()] = value.Value;
        }

        var xpCoins = PayloadReader.GetInt(payload, "xpCoins");
        if (xpCoins.HasValue)
        {
            if (xpCoins.Value < 0)
            {
                _logger.Admin($"character.validation.denied command=save.money characterId={character.Id} reason=xp-negative");
                throw new ArgumentException("xpCoins must be >= 0.");
            }

            character.XpCoins = xpCoins.Value;
        }
    }

    private void ValidateProgressionState(Character character)
    {
        EnsureCharacterDefaults(character);
        if (character.XpCoins < 0) throw new ArgumentException("xpCoins must be >= 0.");
        var classSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in character.CharacterClasses)
        {
            if (string.IsNullOrWhiteSpace(item.ClassCode)) throw new ArgumentException("classCode is required.");
            if (!classSet.Add(item.ClassCode)) throw new ArgumentException($"Duplicate class '{item.ClassCode}'.");
            if (_repositories.ClassDefinitions.GetByCode(item.ClassCode) == null) throw new ArgumentException($"Class '{item.ClassCode}' not found.");
            if (item.Level <= 0) throw new ArgumentException($"Class '{item.ClassCode}' level must be > 0.");
        }

        var skillSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in character.CharacterSkills)
        {
            if (string.IsNullOrWhiteSpace(item.SkillCode)) throw new ArgumentException("skillCode is required.");
            if (!skillSet.Add(item.SkillCode)) throw new ArgumentException($"Duplicate skill '{item.SkillCode}'.");
            if (_repositories.DefinitionSkills.GetByCode(item.SkillCode) == null) throw new ArgumentException($"Skill '{item.SkillCode}' not found.");
            if (item.Level <= 0) throw new ArgumentException($"Skill '{item.SkillCode}' level must be > 0.");
        }
    }

    private List<CharacterClassState> ParseCharacterClasses(IList<object> list)
    {
        return list.OfType<Dictionary<string, object>>().Select(x => new CharacterClassState
        {
            ClassCode = RequireLength(PayloadReader.GetString(x, "classCode"), 1, 128, "classCode"),
            Level = RequireRange(PayloadReader.GetInt(x, "level"), 1, 999, "level"),
            LearnedUtc = DateTime.UtcNow
        }).ToList();
    }

    private List<CharacterSkillState> ParseCharacterSkills(IList<object> list)
    {
        return list.OfType<Dictionary<string, object>>().Select(x => new CharacterSkillState
        {
            SkillCode = RequireLength(PayloadReader.GetString(x, "skillCode"), 1, 128, "skillCode"),
            Tier = RequireRange(PayloadReader.GetInt(x, "tier"), 0, 999, "tier"),
            Level = RequireRange(PayloadReader.GetInt(x, "level"), 1, 999, "level"),
            Acquired = true,
            LearnedUtc = DateTime.UtcNow
        }).ToList();
    }

    private static Dictionary<string, object> CharacterSkillPayload(CharacterSkillState skill) => new Dictionary<string, object>
    {
        { "skillCode", skill.SkillCode },
        { "tier", skill.Tier },
        { "level", skill.Level },
        { "acquired", skill.Acquired },
        { "learnedUtc", skill.LearnedUtc }
    };

    private List<Dictionary<string, object>> BuildCharacterSkillProfileRows(Character character, UserAccount viewer, bool includeHidden, string requestedSkillCode = "", string requestedSubAttributeId = "")
    {
        var definitions = _repositories.DefinitionSkills.GetAll(false)
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Code) && !x.IsArchived && x.Status != DefinitionStatus.Archived)
            .Select(SkillDefinitionV2Defaults.Normalize)
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.DisplayGroup)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.Code)
            .ToList();
        EnsureStarterSkillSubAttributeBindings(definitions);

        var doc = _mongo.CharacterSkillProfiles.Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault();
        var profile = doc?.Profile ?? new SkillProfile { CharacterId = character.Id, RuleSetId = RuleSetIds.FantasyNriDefault, Skills = new List<CharacterSkillProfileValue>(), SchemaVersion = 1 };
        profile.CharacterId = character.Id;
        if (string.IsNullOrWhiteSpace(profile.RuleSetId)) profile.RuleSetId = RuleSetIds.FantasyNriDefault;
        if (profile.Skills == null) profile.Skills = new List<CharacterSkillProfileValue>();

        var changed = doc == null;
        var byId = profile.Skills
            .Where(x => !string.IsNullOrWhiteSpace(x.SkillId))
            .GroupBy(x => x.SkillId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (byId.ContainsKey(definition.Code)) continue;
            var row = new CharacterSkillProfileValue
            {
                SkillId = definition.Code,
                Rank = Math.Max(0, definition.RankMin),
                ManualBonus = 0,
                TrainingState = "untrained",
                IsPlayerVisible = !IsHiddenSkillDefinition(definition),
                IsUnlocked = true,
                IsLearned = false,
                Source = "profile_default",
                LearnedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            profile.Skills.Add(row);
            byId[definition.Code] = row;
            changed = true;
        }

        if (changed)
        {
            var newDoc = doc ?? new CharacterSkillProfileDocument { CharacterId = character.Id };
            newDoc.CharacterId = character.Id;
            newDoc.Profile = profile;
            _mongo.CharacterSkillProfiles.ReplaceOne(
                Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id),
                newDoc,
                new ReplaceOptions { IsUpsert = true });
        }

        var attributeProfile = _mongo.CharacterAttributeProfiles.Find(Builders<CharacterAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault()?.Profile;
        var ruleSetId = FirstNonEmpty(attributeProfile?.RuleSetId, profile.RuleSetId, RuleSetIds.FantasyNriDefault);
        var attributeMap = (attributeProfile?.Values ?? new List<CharacterAttributeValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.AttributeId))
            .GroupBy(x => x.AttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var subAttributeDefinitions = CharacterSubAttributeRuntime.LoadDefinitions(_mongo, ruleSetId, includeHidden: true)
            .Where(x => x.AppliesToSkillChecks && x.IsRollableModifier)
            .GroupBy(x => x.SubAttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
        var subAttributeValues = CharacterSubAttributeRuntime.BuildValueMap(_mongo, character.Id, ruleSetId);

        var rows = new List<Dictionary<string, object>>();
        foreach (var definition in definitions)
        {
            if (!byId.TryGetValue(definition.Code, out var skill)) continue;
            if (!includeHidden && (!skill.IsPlayerVisible || IsHiddenSkillDefinition(definition))) continue;
            var attributeId = FirstNonEmpty(definition.DefaultAttribute, definition.AllowedAttributes.FirstOrDefault() ?? string.Empty);
            var attributeBonus = ResolveAttributeBonus(attributeMap, attributeId);
            var subAttributeId = ResolveSkillSubAttributeId(definition, requestedSkillCode, requestedSubAttributeId, includeHidden);
            var subAttributeBonus = 0;
            var subAttributeDisplayName = string.Empty;
            var subAttributeParentAttribute = string.Empty;
            if (!string.IsNullOrWhiteSpace(subAttributeId))
            {
                if (!subAttributeDefinitions.TryGetValue(subAttributeId, out var subDefinition))
                    throw new KeyNotFoundException("Subattribute definition not found.");
                if (!includeHidden && !subDefinition.IsPlayerVisible)
                    throw new UnauthorizedAccessException("Subattribute is hidden.");
                subAttributeValues.TryGetValue(subAttributeId, out var subValue);
                if (!includeHidden && subValue != null && !subValue.IsVisibleToPlayer)
                    throw new UnauthorizedAccessException("Subattribute is hidden.");
                subAttributeBonus = ResolveSubAttributeBonus(subValue, subDefinition);
                subAttributeDisplayName = FirstNonEmpty(subDefinition.DisplayName, subDefinition.Code, subDefinition.SubAttributeId);
                subAttributeParentAttribute = subDefinition.ParentAttributeId;
            }
            var total = skill.Rank + skill.ManualBonus + attributeBonus + subAttributeBonus;
            var displayName = FirstNonEmpty(definition.Name, definition.Code);
            var breakdown = $"Ранг {skill.Rank} + атрибут {attributeBonus} + ручной бонус {skill.ManualBonus} = {total}";
            var subAttributeBreakdown = string.IsNullOrWhiteSpace(subAttributeDisplayName) ? string.Empty : $" + подхарактеристика {subAttributeDisplayName} {subAttributeBonus}";
            breakdown = $"Ранг {skill.Rank} + атрибут {attributeBonus}{subAttributeBreakdown} + ручной бонус {skill.ManualBonus} = {total}";
            rows.Add(new Dictionary<string, object>
            {
                { "skillId", definition.Code },
                { "skillCode", definition.Code },
                { "code", definition.Code },
                { "displayName", displayName },
                { "name", displayName },
                { "description", includeHidden ? definition.Description : string.Empty },
                { "category", FirstNonEmpty(definition.DisplayGroup, definition.SkillCategory.ToString()) },
                { "tier", definition.Tier },
                { "rank", skill.Rank },
                { "level", skill.Rank },
                { "manualBonus", skill.ManualBonus },
                { "trainingState", FirstNonEmpty(skill.TrainingState, "trained") },
                { "isPlayerVisible", skill.IsPlayerVisible && !IsHiddenSkillDefinition(definition) },
                { "isUnlocked", skill.IsUnlocked },
                { "isLearned", skill.IsLearned },
                { "acquired", skill.IsLearned },
                { "defaultAttribute", attributeId },
                { "attributeBonus", attributeBonus },
                { "defaultSubAttribute", definition.DefaultSubAttribute },
                { "allowedSubAttributes", definition.AllowedSubAttributes.Cast<object>().ToArray() },
                { "subAttributeId", subAttributeId },
                { "subAttributeDisplayName", subAttributeDisplayName },
                { "subAttributeParentAttribute", subAttributeParentAttribute },
                { "subAttributeBonus", subAttributeBonus },
                { "totalBonus", total },
                { "breakdown", breakdown },
                { "breakdownText", breakdown },
                { "isRollable", definition.IsRollable },
                { "source", skill.Source },
                { "sourceOfTruth", "character_skill_profiles" },
                { "updatedAtUtc", skill.UpdatedAtUtc == default ? skill.LearnedAtUtc : skill.UpdatedAtUtc },
                { "learnedUtc", skill.LearnedAtUtc }
            });
        }

        return rows;
    }

    private static bool IsHiddenSkillDefinition(SkillDefinition definition)
    {
        var visibility = (definition.VisibilityRule ?? string.Empty).Trim().ToLowerInvariant();
        return visibility == "hidden" || visibility == "gm_only" || visibility == "server_only";
    }

    private static int ResolveAttributeBonus(Dictionary<string, CharacterAttributeValue> attributes, string attributeId)
    {
        if (string.IsNullOrWhiteSpace(attributeId) || attributes == null || !attributes.TryGetValue(attributeId, out var attribute)) return 0;
        var value = attribute.CurrentValue != 0 ? attribute.CurrentValue : attribute.BaseValue;
        return (int)Math.Floor((value - 10) / 2.0) + attribute.ManualModifier;
    }

    private void EnsureStarterSkillSubAttributeBindings(List<SkillDefinition> definitions)
    {
        var byCode = definitions
            .Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        ConfigureSkillSubAttribute(byCode, "athletics", "strength_lifting", new[] { "strength_grip", "strength_lifting", "strength_impact" });
        ConfigureSkillSubAttribute(byCode, "stealth", "dexterity_stealth", new[] { "dexterity_stealth", "dexterity_reaction" });
        ConfigureSkillSubAttribute(byCode, "perception", "wisdom_perception", new[] { "wisdom_perception", "wisdom_intuition" });
        ConfigureSkillSubAttribute(byCode, "engineering", "intellect_engineering", new[] { "intellect_engineering", "intellect_analysis" });

        if (!byCode.TryGetValue("dev_acceptance_skill_01451", out var acceptance))
        {
            acceptance = new SkillDefinition
            {
                Id = "dev_acceptance_skill_01451",
                Code = "dev_acceptance_skill_01451",
                Name = "Проверочный навык 0.14.51",
                Description = "Acceptance skill for RuleSet-driven subattribute checks.",
                DisplayGroup = "testing",
                DefaultAttribute = CharacterAttributeIds.Strength,
                AllowedAttributes = new List<string> { CharacterAttributeIds.Strength },
                RankMin = 0,
                RankMax = 20,
                IsRollable = true,
                IsRollableExplicitlySet = true,
                VisibilityRule = "public",
                Status = DefinitionStatus.Active,
                IsActive = true,
                SchemaVersion = 1
            };
            definitions.Add(acceptance);
            byCode[acceptance.Code] = acceptance;
        }

        ConfigureSkillSubAttribute(byCode, "dev_acceptance_skill_01451", "dev_acceptance_subattribute_01451", new[] { "dev_acceptance_subattribute_01451", "strength_grip" });
    }

    private void ConfigureSkillSubAttribute(Dictionary<string, SkillDefinition> byCode, string skillCode, string defaultSubAttribute, IEnumerable<string> allowedSubAttributes)
    {
        if (!byCode.TryGetValue(skillCode, out var definition)) return;
        var allowed = (allowedSubAttributes ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (allowed.Count == 0 && !string.IsNullOrWhiteSpace(defaultSubAttribute)) allowed.Add(defaultSubAttribute);

        var changed = false;
        if (!string.Equals(definition.DefaultSubAttribute, defaultSubAttribute, StringComparison.OrdinalIgnoreCase))
        {
            definition.DefaultSubAttribute = defaultSubAttribute;
            changed = true;
        }

        var current = definition.AllowedSubAttributes ?? new List<string>();
        if (current.Count != allowed.Count || current.Except(allowed, StringComparer.OrdinalIgnoreCase).Any())
        {
            definition.AllowedSubAttributes = allowed;
            changed = true;
        }

        if (!string.Equals(definition.SubAttributeMode, "defaultFromSkill", StringComparison.OrdinalIgnoreCase))
        {
            definition.SubAttributeMode = "defaultFromSkill";
            changed = true;
        }

        if (changed)
        {
            definition.IsRollable = true;
            definition.IsRollableExplicitlySet = true;
            definition.Status = definition.Status == DefinitionStatus.Archived ? DefinitionStatus.Active : definition.Status;
            definition.IsArchived = false;
            definition.Archived = false;
            _repositories.DefinitionSkills.Upsert(definition);
        }
    }

    private static string ResolveSkillSubAttributeId(SkillDefinition definition, string requestedSkillCode, string requestedSubAttributeId, bool includeHidden)
    {
        var allowed = definition.AllowedSubAttributes ?? new List<string>();
        var isRequestedSkill = !string.IsNullOrWhiteSpace(requestedSkillCode)
            && string.Equals(definition.Code, requestedSkillCode, StringComparison.OrdinalIgnoreCase);
        if (isRequestedSkill && !string.IsNullOrWhiteSpace(requestedSubAttributeId))
        {
            if (!allowed.Contains(requestedSubAttributeId, StringComparer.OrdinalIgnoreCase))
            {
                if (!includeHidden) throw new UnauthorizedAccessException("Subattribute is not allowed for this skill.");
                throw new ArgumentException("Subattribute is not allowed for this skill.");
            }

            return requestedSubAttributeId;
        }

        var defaultSubAttribute = FirstNonEmpty(definition.DefaultSubAttribute, allowed.FirstOrDefault() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(defaultSubAttribute)) return string.Empty;
        return allowed.Count == 0 || allowed.Contains(defaultSubAttribute, StringComparer.OrdinalIgnoreCase)
            ? defaultSubAttribute
            : string.Empty;
    }

    private static int ResolveSubAttributeBonus(CharacterSubAttributeValue? value, SubAttributeDefinitionProjection definition)
    {
        if (definition == null) return 0;
        var current = value == null
            ? definition.DefaultValue
            : value.CurrentValue != 0 || value.BaseValue == 0
                ? value.CurrentValue
                : value.BaseValue;
        return current + (value?.ManualBonus ?? 0);
    }

    private Dictionary<string, object> BuildNotesContextPayload(string characterId)
    {
        var links = _repositories.Notes.Find(
                Builders<Note>.Filter.Eq(x => x.TargetType, "character") &
                Builders<Note>.Filter.Eq(x => x.TargetId, characterId) &
                Builders<Note>.Filter.Eq(x => x.Deleted, false))
            .Select(n => new Dictionary<string, object>
            {
                { "noteId", n.Id },
                { "title", n.Title },
                { "visibility", n.Visibility.ToString() },
                { "noteType", n.NoteType.ToString() }
            })
            .Cast<object>()
            .ToArray();

        return new Dictionary<string, object>
        {
            { "scopes", new object[] { "character.personal", "character.admin", "character.session" } },
            { "noteLinks", links }
        };
    }

    private Dictionary<string, object> BuildMoneyPayload(Character character) => new Dictionary<string, object>
    {
        { "money", WalletPayload(character.Wallet) },
        { "currencies", CurrencyListPayload(character) }
    };

    private static object[] CurrencyListPayload(Character character)
    {
        character.Wallet.EnsureAllDenominations();
        var list = Enum.GetValues(typeof(CurrencyDenomination)).Cast<CurrencyDenomination>()
            .Select(x => (object)new Dictionary<string, object>
            {
                { "code", x.ToString() },
                { "amount", character.Wallet.Balance.Amounts.ContainsKey(x.ToString()) ? character.Wallet.Balance.Amounts[x.ToString()] : 0L },
                { "kind", "money" }
            })
            .ToList();
        list.Add(new Dictionary<string, object> { { "code", "XpCoins" }, { "amount", character.XpCoins }, { "kind", "progression" } });
        return list.ToArray();
    }



    public ResponseEnvelope CombatStart(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var list = PayloadReader.GetList(context.Request.Payload, "participants") ?? new List<object>();
        if (list.Count == 0) throw new ArgumentException("participants are required.");

        var participants = ParseCombatParticipants(list, false);
        var combat = _repositories.Combats.Find(Builders<CombatState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault();
        if (combat == null)
        {
            combat = new CombatState { SessionId = sessionId, Status = CombatStatus.Lobby };
            _repositories.Combats.Insert(combat);
        }

        if (combat.Status == CombatStatus.Active) throw new InvalidOperationException("Combat already active.");

        BuildInitialInitiative(combat, participants, isNewSide:false);
        combat.Status = CombatStatus.Active;
        combat.RoundState.RoundNumber = 1;
        combat.RoundState.CurrentTurnIndex = 0;
        combat.RoundState.ActiveSlotId = ResolveCurrentSlot(combat);

        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.start", actor.Id, "Combat started");
        SyncAudioPolicyForSession(sessionId, actor.Id);
        PublishSystemMessage(sessionId, "Combat started.");
        return Ok("Combat started.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatEnd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        combat.Status = CombatStatus.Ended;
        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.end", actor.Id, "Combat ended manually");
        SyncAudioPolicyForSession(combat.SessionId, actor.Id);
        PublishSystemMessage(combat.SessionId, "Combat ended.");
        return Ok("Combat ended.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatGetState(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var combat = GetCombatBySession(context);
        return Ok("Combat state loaded.", CombatSnapshotPayloadForViewer(combat, actor));
    }

    public ResponseEnvelope CombatVisibleState(CommandContext context) => CombatGetState(context);

    public ResponseEnvelope CombatParticipants(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var combat = GetCombatBySession(context);
        return Ok("Combat participants loaded.", new Dictionary<string, object>
        {
            { "participants", combat.Participants.Select(p => ParticipantPayload(p, actor)).Cast<object>().ToArray() }
        });
    }

    public ResponseEnvelope CombatGetHistory(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var combat = GetCombatBySession(context);
        var logs = _repositories.CombatLogs.Find(Builders<CombatLogEntry>.Filter.Eq(x => x.CombatId, combat.Id))
            .OrderBy(x => x.CreatedUtc)
            .Select(x => new Dictionary<string, object>
            {
                { "eventType", x.EventType }, { "message", x.Message }, { "actorUserId", x.ActorUserId }, { "at", x.CreatedUtc }
            }).Cast<object>().ToArray();
        return Ok("Combat history loaded.", new Dictionary<string, object> { { "items", logs } });
    }

    public ResponseEnvelope CombatTimeline(CommandContext context) => CombatGetHistory(context);

    public ResponseEnvelope CombatNextTurn(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        EnsureCombatActive(combat);

        AdvanceTurn(combat, +1);
        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.nextTurn", actor.Id, $"Turn advanced to index {combat.RoundState.CurrentTurnIndex}");
        return Ok("Moved to next turn.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatPreviousTurn(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        EnsureCombatActive(combat);

        AdvanceTurn(combat, -1);
        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.previousTurn", actor.Id, $"Turn moved back to index {combat.RoundState.CurrentTurnIndex}");
        return Ok("Moved to previous turn.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatNextRound(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        EnsureCombatActive(combat);

        combat.RoundState.RoundNumber += 1;
        combat.RoundState.CurrentTurnIndex = 0;
        combat.ExtraFirstRoundConsumed = true;
        foreach (var participant in combat.Participants.Where(p => p.Status != TurnStatus.Eliminated))
            participant.Status = TurnStatus.Waiting;
        combat.RoundState.ActiveSlotId = ResolveCurrentSlot(combat);

        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.nextRound", actor.Id, $"Round {combat.RoundState.RoundNumber} started");
        SyncAudioPolicyForSession(combat.SessionId, actor.Id);
        PublishSystemMessage(combat.SessionId, $"Round {combat.RoundState.RoundNumber} started.");
        return Ok("Moved to next round.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatSkipTurn(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        EnsureCombatActive(combat);

        var activeSlot = GetActiveSlot(combat);
        foreach (var id in activeSlot.InternalOrder)
        {
            var p = combat.Participants.FirstOrDefault(x => x.ParticipantId == id);
            if (p != null && p.Status != TurnStatus.Eliminated)
                p.Status = TurnStatus.Skipped;
        }

        AdvanceTurn(combat, +1);
        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.skipTurn", actor.Id, "Active turn skipped");
        return Ok("Turn skipped.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatSelectActive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        var slotId = RequireLength(PayloadReader.GetString(context.Request.Payload, "slotId"), 8, 128, "slotId");
        var idx = combat.Slots.FindIndex(s => s.SlotId == slotId);
        if (idx < 0) throw new KeyNotFoundException("Slot not found.");

        combat.RoundState.CurrentTurnIndex = idx;
        combat.RoundState.ActiveSlotId = slotId;
        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.selectActive", actor.Id, $"Active slot set to {slotId}");
        return Ok("Active slot selected.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatReorderBeforeStart(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        if (combat.Status == CombatStatus.Active) throw new InvalidOperationException("Cannot reorder slots after combat start.");

        var ids = PayloadReader.GetList(context.Request.Payload, "slotOrder")?.Select(x => Convert.ToString(x) ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
        if (ids.Count != combat.Slots.Count) throw new ArgumentException("slotOrder must contain all slot ids.");

        for (var i = 0; i < ids.Count; i++)
        {
            var slot = combat.Slots.FirstOrDefault(s => s.SlotId == ids[i]) ?? throw new KeyNotFoundException("slot not found in order");
            slot.Order = i;
        }

        combat.Slots = combat.Slots.OrderBy(s => s.Order).ToList();
        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.reorderBeforeStart", actor.Id, "Slots reordered before start");
        return Ok("Slots reordered.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatReorderSlotMembers(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        var slotId = RequireLength(PayloadReader.GetString(context.Request.Payload, "slotId"), 8, 128, "slotId");
        var slot = combat.Slots.FirstOrDefault(s => s.SlotId == slotId) ?? throw new KeyNotFoundException("slot not found");
        var ids = PayloadReader.GetList(context.Request.Payload, "memberOrder")?.Select(x => Convert.ToString(x) ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
        if (ids.Count != slot.ParticipantIds.Count) throw new ArgumentException("memberOrder must include all slot members.");
        slot.InternalOrder = ids;
        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.reorderSlotMembers", actor.Id, $"Internal order changed for slot {slotId}");
        return Ok("Slot members reordered.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatAddParticipant(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        var list = PayloadReader.GetList(context.Request.Payload, "participants") ?? new List<object>();
        if (list.Count == 0) throw new ArgumentException("participants are required.");
        var participants = ParseCombatParticipants(list, true);

        BuildInitialInitiative(combat, participants, isNewSide:true);
        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.addParticipant", actor.Id, $"Added {participants.Count} participant(s)");
        return Ok("Participants added.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatRemoveParticipant(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        var participantId = RequireLength(PayloadReader.GetString(context.Request.Payload, "participantId"), 8, 128, "participantId");
        var participant = combat.Participants.FirstOrDefault(p => p.ParticipantId == participantId) ?? throw new KeyNotFoundException("participant not found");
        participant.Status = TurnStatus.Eliminated;

        foreach (var slot in combat.Slots)
        {
            slot.ParticipantIds.Remove(participantId);
            slot.InternalOrder.Remove(participantId);
        }
        combat.Slots = combat.Slots.Where(s => s.ParticipantIds.Count > 0).OrderBy(s=>s.Order).ToList();
        ReindexSlots(combat);

        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.removeParticipant", actor.Id, $"Removed {participantId}");
        return Ok("Participant removed.", CombatSnapshotPayload(combat));
    }

    public ResponseEnvelope CombatDetachCompanion(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var combat = GetCombatBySession(context);
        var participantId = RequireLength(PayloadReader.GetString(context.Request.Payload, "participantId"), 8, 128, "participantId");
        var participant = combat.Participants.FirstOrDefault(p => p.ParticipantId == participantId) ?? throw new KeyNotFoundException("participant not found");
        if (participant.Kind != ParticipantKind.Companion) throw new InvalidOperationException("Only companion can be detached.");

        participant.DetachedCompanion = true;
        foreach (var slot in combat.Slots)
        {
            if (slot.ParticipantIds.Contains(participantId))
            {
                slot.ParticipantIds.Remove(participantId);
                slot.InternalOrder.Remove(participantId);
            }
        }

        var newSlot = new InitiativeSlot { IsGroup = false, ParticipantIds = new List<string> { participantId }, InternalOrder = new List<string> { participantId }, Order = combat.Slots.Count };
        combat.Slots.Add(newSlot);
        combat.Slots = combat.Slots.Where(s => s.ParticipantIds.Count > 0).OrderBy(s => s.Order).ToList();
        ReindexSlots(combat);
        _repositories.Combats.Replace(combat);
        AddCombatLog(combat, "combat.detachCompanion", actor.Id, $"Detached companion {participantId}");
        return Ok("Companion detached.", CombatSnapshotPayload(combat));
    }

    private CombatState GetCombatBySession(CommandContext context)
    {
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var combat = _repositories.Combats.Find(Builders<CombatState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault();
        if (combat == null) throw new KeyNotFoundException("Combat state not found.");
        return combat;
    }

    private static void EnsureCombatActive(CombatState combat)
    {
        if (combat.Status != CombatStatus.Active) throw new InvalidOperationException("Combat is not active.");
    }

    private void BuildInitialInitiative(CombatState combat, List<InitiativeParticipant> incoming, bool isNewSide)
    {
        var rng = new Random();
        foreach (var p in incoming)
        {
            p.BaseRoll = rng.Next(1, 101);
            p.SkipFirstTurnRoundOne = p.BaseRoll == 1;
            p.ExtraTurnFirstRound = false;
            p.Status = p.SkipFirstTurnRoundOne ? TurnStatus.Skipped : TurnStatus.Waiting;
        }

        if (!isNewSide)
        {
            ResolveTieBreaks(incoming, rng, true);

            var winners100 = incoming.Where(p => p.BaseRoll == 100).ToList();
            if (winners100.Count > 0)
            {
                ResolveTieBreaks(winners100, rng, true);
                var winner = winners100.OrderByDescending(p => p.TieBreakRolls.DefaultIfEmpty(p.BaseRoll).Sum()).First();
                winner.ExtraTurnFirstRound = true;
                combat.ExtraFirstRoundParticipantId = winner.ParticipantId;
                combat.ExtraFirstRoundConsumed = false;
            }
        }
        else
        {
            ResolveTieBreaks(incoming, rng, false);
        }

        var all = combat.Participants.Where(p => p.Status != TurnStatus.Eliminated).ToList();
        all.AddRange(incoming);
        combat.Participants = all;

        var grouped = incoming.GroupBy(p => DetermineGroupKey(p));
        foreach (var group in grouped)
        {
            var members = group.ToList();
            var slot = new InitiativeSlot
            {
                IsGroup = members.Count > 1,
                ParticipantIds = members.Select(m => m.ParticipantId).ToList(),
                InternalOrder = members.Select(m => m.ParticipantId).ToList()
            };

            if (!isNewSide)
            {
                combat.Slots.Add(slot);
            }
            else
            {
                var roll = members.Max(m => m.BaseRoll);
                var insertIndex = combat.Slots.Count;
                for (var i = 0; i < combat.Slots.Count; i++)
                {
                    var existingMax = combat.Slots[i].ParticipantIds
                        .Select(id => combat.Participants.FirstOrDefault(p => p.ParticipantId == id))
                        .Where(p => p != null)
                        .Max(p => p!.BaseRoll);
                    if (roll > existingMax)
                    {
                        insertIndex = i;
                        break;
                    }
                }
                combat.Slots.Insert(insertIndex, slot);
            }
        }

        combat.Slots = combat.Slots
            .OrderByDescending(s => s.ParticipantIds.Select(id => combat.Participants.First(p => p.ParticipantId == id).BaseRoll).Max())
            .ToList();
        ReindexSlots(combat);
    }

    private static void ResolveTieBreaks(List<InitiativeParticipant> items, Random rng, bool strict)
    {
        var grouped = items.GroupBy(x => x.BaseRoll).Where(g => g.Count() > 1).ToList();
        foreach (var group in grouped)
        {
            var tied = group.ToList();
            var unique = false;
            while (!unique)
            {
                unique = true;
                var values = new Dictionary<string, int>();
                foreach (var p in tied)
                {
                    var roll = rng.Next(1, 101);
                    p.TieBreakRolls.Add(roll);
                    values[p.ParticipantId] = roll;
                }
                if (strict)
                    unique = values.Values.Distinct().Count() == values.Count;
                if (!strict) break;
            }

            var ordered = tied.OrderByDescending(x => x.TieBreakRolls.DefaultIfEmpty(0).Sum()).ToList();
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].BaseRoll = group.Key - i;
        }
    }

    private static string DetermineGroupKey(InitiativeParticipant participant)
    {
        if (participant.Kind == ParticipantKind.Companion && !participant.DetachedCompanion && !string.IsNullOrWhiteSpace(participant.CompanionOwnerEntityId))
            return "owner:" + participant.CompanionOwnerEntityId;
        return "self:" + participant.ParticipantId;
    }

    private static void ReindexSlots(CombatState combat)
    {
        for (var i = 0; i < combat.Slots.Count; i++) combat.Slots[i].Order = i;
        if (combat.RoundState.CurrentTurnIndex >= combat.Slots.Count) combat.RoundState.CurrentTurnIndex = 0;
        combat.RoundState.ActiveSlotId = combat.Slots.Count == 0 ? null : combat.Slots[combat.RoundState.CurrentTurnIndex].SlotId;
    }

    private static string? ResolveCurrentSlot(CombatState combat)
    {
        if (!combat.ExtraFirstRoundConsumed && !string.IsNullOrWhiteSpace(combat.ExtraFirstRoundParticipantId))
        {
            var slot = combat.Slots.FirstOrDefault(s => s.ParticipantIds.Contains(combat.ExtraFirstRoundParticipantId));
            if (slot != null) return slot.SlotId;
        }
        return combat.Slots.Count == 0 ? null : combat.Slots[0].SlotId;
    }

    private InitiativeSlot GetActiveSlot(CombatState combat)
    {
        if (!string.IsNullOrWhiteSpace(combat.RoundState.ActiveSlotId))
        {
            var slot = combat.Slots.FirstOrDefault(s => s.SlotId == combat.RoundState.ActiveSlotId);
            if (slot != null) return slot;
        }

        if (combat.Slots.Count == 0) throw new InvalidOperationException("No initiative slots.");
        return combat.Slots[combat.RoundState.CurrentTurnIndex];
    }

    private void AdvanceTurn(CombatState combat, int delta)
    {
        if (!combat.ExtraFirstRoundConsumed && !string.IsNullOrWhiteSpace(combat.ExtraFirstRoundParticipantId))
        {
            combat.ExtraFirstRoundConsumed = true;
            combat.RoundState.CurrentTurnIndex = 0;
            combat.RoundState.ActiveSlotId = combat.Slots.Count == 0 ? null : combat.Slots[0].SlotId;
            return;
        }

        if (combat.Slots.Count == 0) return;
        combat.RoundState.CurrentTurnIndex += delta;
        if (combat.RoundState.CurrentTurnIndex >= combat.Slots.Count)
        {
            combat.RoundState.CurrentTurnIndex = 0;
            combat.RoundState.RoundNumber += 1;
            foreach (var participant in combat.Participants.Where(p => p.Status != TurnStatus.Eliminated)) participant.Status = TurnStatus.Waiting;
        }
        if (combat.RoundState.CurrentTurnIndex < 0)
        {
            combat.RoundState.CurrentTurnIndex = combat.Slots.Count - 1;
            combat.RoundState.RoundNumber = Math.Max(1, combat.RoundState.RoundNumber - 1);
        }

        var slot = combat.Slots[combat.RoundState.CurrentTurnIndex];
        combat.RoundState.ActiveSlotId = slot.SlotId;

        if (combat.RoundState.RoundNumber == 1)
        {
            foreach (var pid in slot.InternalOrder)
            {
                var p = combat.Participants.FirstOrDefault(x => x.ParticipantId == pid);
                if (p != null && p.SkipFirstTurnRoundOne)
                    p.Status = TurnStatus.Skipped;
            }
        }
    }

    private List<InitiativeParticipant> ParseCombatParticipants(IList<object> list, bool allowNewSide)
    {
        var result = new List<InitiativeParticipant>();
        foreach (var item in list)
        {
            var map = item as Dictionary<string, object>;
            if (map == null) continue;

            ParticipantKind kind;
            if (!Enum.TryParse(PayloadReader.GetString(map, "kind"), true, out kind)) kind = ParticipantKind.Other;
            var participant = new InitiativeParticipant
            {
                Kind = kind,
                EntityId = RequireLength(PayloadReader.GetString(map, "entityId"), 1, 128, "entityId"),
                DisplayName = RequireLength(PayloadReader.GetString(map, "displayName"), 1, 128, "displayName"),
                OwnerUserId = PayloadReader.GetString(map, "ownerUserId"),
                CompanionOwnerEntityId = PayloadReader.GetString(map, "companionOwnerEntityId"),
                DetachedCompanion = PayloadReader.GetBool(map, "detachedCompanion")
            };
            if (participant.Kind == ParticipantKind.Companion && string.IsNullOrWhiteSpace(participant.CompanionOwnerEntityId))
                participant.CompanionOwnerEntityId = participant.OwnerUserId;
            result.Add(participant);
        }

        if (result.Count == 0) throw new ArgumentException("No valid participants.");
        return result;
    }

    private Dictionary<string, object> CombatSnapshotPayload(CombatState combat)
    {
        return new Dictionary<string, object>
        {
            { "combatId", combat.Id },
            { "sessionId", combat.SessionId },
            { "status", combat.Status.ToString() },
            { "round", combat.RoundState.RoundNumber },
            { "turnIndex", combat.RoundState.CurrentTurnIndex },
            { "activeSlotId", combat.RoundState.ActiveSlotId ?? string.Empty },
            { "slots", combat.Slots.Select(s => SlotPayload(s, combat)).Cast<object>().ToArray() },
            { "participants", combat.Participants.Select(p => ParticipantPayload(p, null)).Cast<object>().ToArray() }
        };
    }

    private Dictionary<string, object> CombatSnapshotPayloadForViewer(CombatState combat, UserAccount viewer)
    {
        return CombatSnapshotPayload(combat);
    }

    private Dictionary<string, object> SlotPayload(InitiativeSlot slot, CombatState combat)
    {
        return new Dictionary<string, object>
        {
            { "slotId", slot.SlotId },
            { "order", slot.Order },
            { "isGroup", slot.IsGroup },
            { "memberParticipantIds", slot.ParticipantIds.Cast<object>().ToArray() },
            { "internalOrder", slot.InternalOrder.Cast<object>().ToArray() },
            { "maxRoll", slot.ParticipantIds.Select(id => combat.Participants.FirstOrDefault(p => p.ParticipantId == id)?.BaseRoll ?? 0).DefaultIfEmpty(0).Max() }
        };
    }

    private Dictionary<string, object> ParticipantPayload(InitiativeParticipant p, UserAccount? viewer)
    {
        return new Dictionary<string, object>
        {
            { "participantId", p.ParticipantId },
            { "kind", p.Kind.ToString() },
            { "entityId", p.EntityId },
            { "displayName", p.DisplayName },
            { "ownerUserId", p.OwnerUserId ?? string.Empty },
            { "baseRoll", p.BaseRoll },
            { "tieBreakRolls", p.TieBreakRolls.Cast<object>().ToArray() },
            { "extraTurnFirstRound", p.ExtraTurnFirstRound },
            { "skipFirstTurnRoundOne", p.SkipFirstTurnRoundOne },
            { "status", p.Status.ToString() },
            { "detachedCompanion", p.DetachedCompanion }
        };
    }

    private void AddCombatLog(CombatState combat, string eventType, string actorUserId, string message)
    {
        _repositories.CombatLogs.Insert(new CombatLogEntry
        {
            CombatId = combat.Id,
            SessionId = combat.SessionId,
            EventType = eventType,
            ActorUserId = actorUserId,
            Message = message
        });
        _logger.Session($"[combat] {eventType} session={combat.SessionId} {message}");
        _logger.Admin($"[combat-admin] {eventType} actor={actorUserId} session={combat.SessionId}");
        _logger.Audit($"combat:{eventType} actor={actorUserId} session={combat.SessionId}");
    }


    public ResponseEnvelope RequestCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (actor.Roles.Contains(UserRole.Observer)) throw new UnauthorizedAccessException("Observer cannot create requests.");

        var requestType = RequireLength(PayloadReader.GetString(context.Request.Payload, "requestType"), 3, 64, "requestType");
        var actionCode = RequireLength(PayloadReader.GetString(context.Request.Payload, "actionCode"), 3, 128, "actionCode");
        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId");
        var description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 1024, "description");
        var payloadJson = PayloadReader.GetString(context.Request.Payload, "payloadJson") ?? "{}";

        var fingerprint = BuildFingerprint(actionCode, actor.Id, characterId, payloadJson);
        EnsureCanCreateByFingerprint(actor.Id, fingerprint);

        var request = new ActionRequest
        {
            RequestType = requestType,
            ActionCode = actionCode,
            CreatorUserId = actor.Id,
            RelatedUserId = actor.Id,
            CharacterId = characterId,
            Description = description,
            PayloadJson = payloadJson,
            Fingerprint = fingerprint,
            RejectionCountForFingerprint = GetRejectionCount(actor.Id, fingerprint)
        };
        request.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "Created", Comment = description });
        _repositories.ActionRequests.Insert(request);
        WriteAudit("request", actor.Id, "create", request.Id);
        return Ok("Request created.", RequestPayload(request));
    }

    public ResponseEnvelope DiceRequest(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (actor.Roles.Contains(UserRole.Observer)) throw new UnauthorizedAccessException("Observer cannot create dice requests.");

        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId");
        var description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 1024, "description");
        var formulaInput = RequireLength(PayloadReader.GetString(context.Request.Payload, "formula"), 3, 64, "formula");
        var visibilityRaw = (PayloadReader.GetString(context.Request.Payload, "visibility") ?? RequestVisibility.Public.ToString());
        RequestVisibility visibility;
        if (!Enum.TryParse(visibilityRaw, true, out visibility)) visibility = RequestVisibility.Public;

        var formula = DiceFormulaParser.Parse(formulaInput);
        var fingerprint = BuildFingerprint("dice", actor.Id, characterId, formula.Normalized + ":" + visibility);
        EnsureCanCreateByFingerprint(actor.Id, fingerprint);

        var request = new DiceRollRequest
        {
            RequestType = "DiceRoll",
            CreatorUserId = actor.Id,
            RelatedUserId = actor.Id,
            CharacterId = characterId,
            Description = description,
            RawFormula = formulaInput,
            Formula = formula,
            Visibility = visibility,
            PayloadJson = "{}",
            Fingerprint = fingerprint,
            RejectionCountForFingerprint = GetRejectionCount(actor.Id, fingerprint)
        };
        request.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "Created", Comment = $"{formula.Normalized} ({visibility})" });

        _repositories.DiceRequests.Insert(request);
        WriteAudit("request", actor.Id, "createDice", request.Id);
        _logger.Admin($"dice.request.pending actor={actor.Login} characterId={characterId} formula={formula.Normalized} visibility={visibility}");
        return Ok("Dice request created.", DiceRequestPayload(request, actor));
    }

    public ResponseEnvelope DiceRollStandard(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        _logger.Admin($"dice.roll.standard.start actor={actor.Login}");
        try
        {
            var roll = CreateResolvedDiceRoll(context, actor, isTestRoll: false);
            _repositories.DiceRequests.Insert(roll);
            TryPublishSyncEvent(
                type: "dice.roll.created",
                scope: SyncScopes.Dice,
                entityType: "diceRoll",
                entityId: roll.Id,
                operation: "created",
                actorUserId: actor.Id,
                payload: new Dictionary<string, object>
                {
                    { "rollId", roll.Id },
                    { "createdUtc", roll.CreatedUtc },
                    { "visibility", roll.Visibility.ToString() }
                },
                requestId: context.Request.RequestId ?? string.Empty);
            _logger.Admin($"dice.roll.saved commentPresent={!string.IsNullOrWhiteSpace(roll.Description)}");
            _logger.Admin($"dice.roll.standard created actor={actor.Login} requestId={roll.Id}");
            _logger.Admin($"dice.roll.standard actor={actor.Login} requestId={roll.Id} total={roll.Result?.Total ?? 0}");
            return Ok("Standard dice roll created.", DiceRequestPayload(roll, actor));
        }
        catch (Exception ex)
        {
            _logger.Admin($"dice.roll.standard.fail actor={actor.Login} reason={ex.GetType().Name}:{ex.Message}");
            throw;
        }
    }

    public ResponseEnvelope CharacterSkillCheckRoll(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (actor.Roles.Contains(UserRole.Observer)) throw new UnauthorizedAccessException("Observer cannot create skill checks.");
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var skillCode = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "skillCode"), PayloadReader.GetString(context.Request.Payload, "skillId")), 1, 128, "skillCode");
        var requestedSubAttributeId = PayloadReader.GetString(context.Request.Payload, "subAttributeId") ?? string.Empty;
        var character = GetCharacter(characterId);
        var isAdmin = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
        if (character.OwnerUserId != actor.Id && !isAdmin) throw new UnauthorizedAccessException("Character unavailable for skill check.");

        var rows = BuildCharacterSkillProfileRows(character, actor, isAdmin, skillCode, requestedSubAttributeId);
        var row = rows.FirstOrDefault(x => string.Equals(Convert.ToString(x["skillCode"]), skillCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("Skill is unavailable.");
        var visible = row.ContainsKey("isPlayerVisible") && string.Equals(Convert.ToString(row["isPlayerVisible"]), "True", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !visible) throw new UnauthorizedAccessException("Skill is hidden.");

        var totalBonus = Convert.ToInt32(row["totalBonus"]);
        var formulaInput = totalBonus >= 0 ? $"1d20+{totalBonus}" : $"1d20{totalBonus}";
        var formula = DiceFormulaParser.Parse(formulaInput);
        var result = DiceRollExecutor.Execute(formula, RequestVisibility.Public, actor.Id);
        if (!FateMvpPipelineEnabled()) ApplyFateToRealDiceRoll(formula, result);
        var skillName = Convert.ToString(row["displayName"]) ?? skillCode;
        var description = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "description"), $"Проверка навыка: {skillName} ({skillCode}). {row["breakdownText"]}"), 0, 1024, "description");
        var request = new DiceRollRequest
        {
            RequestType = "SkillCheck",
            CreatorUserId = actor.Id,
            RelatedUserId = actor.Id,
            CharacterId = characterId,
            Description = description,
            RawFormula = formulaInput,
            Formula = formula,
            Visibility = RequestVisibility.Public,
            Status = RequestStatus.Approved,
            Result = result,
            PayloadJson = "{}",
            Fingerprint = BuildFingerprint("skill-check", actor.Id, characterId, $"{skillCode}:{formula.Normalized}:{DateTime.UtcNow.Ticks}")
        };
        request.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "CreatedSkillCheck", Comment = formula.Normalized });
        ApplyFateMvpToDiceRequestIfEnabled(context, actor, request, FateRollTypes.SkillCheck, skillCode, requestedSubAttributeId, new[] { "skill_check", "strength", "physical" });
        _repositories.DiceRequests.Insert(request);
        TryPublishSyncEvent(
            type: "dice.roll.created",
            scope: SyncScopes.Dice,
            entityType: "diceRoll",
            entityId: request.Id,
            operation: "created",
            actorUserId: actor.Id,
            payload: new Dictionary<string, object> { { "rollId", request.Id }, { "createdUtc", request.CreatedUtc }, { "visibility", request.Visibility.ToString() } },
            requestId: context.Request.RequestId ?? string.Empty);
        _logger.Admin($"character.skill.check.roll actor={actor.Login} characterId={characterId} skillCode={skillCode} totalBonus={totalBonus} requestId={request.Id}");
        return Ok("Skill check rolled.", new Dictionary<string, object>
        {
            { "skillCode", skillCode },
            { "displayName", skillName },
            { "subAttributeId", row.ContainsKey("subAttributeId") ? row["subAttributeId"] : string.Empty },
            { "subAttributeDisplayName", row.ContainsKey("subAttributeDisplayName") ? row["subAttributeDisplayName"] : string.Empty },
            { "subAttributeBonus", row.ContainsKey("subAttributeBonus") ? row["subAttributeBonus"] : 0 },
            { "totalBonus", totalBonus },
            { "breakdown", row["breakdownText"] },
            { "formula", formula.Normalized },
            { "roll", DiceRequestPayload(request, actor) }
        });
    }

    public ResponseEnvelope DiceRollTest(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        _logger.Admin($"dice.roll.test.start actor={actor.Login}");
        try
        {
            var roll = CreateResolvedDiceRoll(context, actor, isTestRoll: true);
            var existing = _repositories.DiceRequests.Find(
                Builders<DiceRollRequest>.Filter.Eq(x => x.IsTestRoll, true) &
                Builders<DiceRollRequest>.Filter.Eq(x => x.TestRollOwnerUserId, actor.Id) &
                Builders<DiceRollRequest>.Filter.Eq(x => x.Deleted, false))
                .OrderByDescending(x => x.UpdatedUtc)
                .FirstOrDefault();

            if (existing == null)
            {
                _repositories.DiceRequests.Insert(roll);
                TryPublishSyncEvent(
                    type: "dice.roll.created",
                    scope: SyncScopes.Dice,
                    entityType: "diceRoll",
                    entityId: roll.Id,
                    operation: "created",
                    actorUserId: actor.Id,
                    payload: new Dictionary<string, object>
                    {
                        { "rollId", roll.Id },
                        { "createdUtc", roll.CreatedUtc },
                        { "visibility", roll.Visibility.ToString() }
                    },
                    requestId: context.Request.RequestId ?? string.Empty);
                _logger.Admin($"dice.roll.saved commentPresent={!string.IsNullOrWhiteSpace(roll.Description)}");
                _logger.Admin($"dice.roll.test replacedPrevious=false actor={actor.Login} requestId={roll.Id}");
                _logger.Admin($"dice.roll.test actor={actor.Login} action=create requestId={roll.Id} total={roll.Result?.Total ?? 0}");
                return Ok("Test dice roll created.", DiceRequestPayload(roll, actor));
            }

            existing.RawFormula = roll.RawFormula;
            existing.Formula = roll.Formula;
            existing.Visibility = roll.Visibility;
            existing.Description = roll.Description;
            existing.Result = roll.Result;
            existing.Status = RequestStatus.Approved;
            var oldTimestamp = existing.CreatedUtc;
            var newTimestamp = DateTime.UtcNow;
            existing.CreatedUtc = newTimestamp;
            existing.UpdatedUtc = newTimestamp;
            existing.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "TestReplaced", Comment = roll.Formula.Normalized });
            _repositories.DiceRequests.Replace(existing);
            TryPublishSyncEvent(
                type: "dice.roll.created",
                scope: SyncScopes.Dice,
                entityType: "diceRoll",
                entityId: existing.Id,
                operation: "created",
                actorUserId: actor.Id,
                payload: new Dictionary<string, object>
                {
                    { "rollId", existing.Id },
                    { "createdUtc", existing.CreatedUtc },
                    { "visibility", existing.Visibility.ToString() }
                },
                requestId: context.Request.RequestId ?? string.Empty);
            _logger.Admin($"dice.roll.saved commentPresent={!string.IsNullOrWhiteSpace(existing.Description)}");
            _logger.Admin($"dice.roll.test replacement oldTimestamp={oldTimestamp:o}");
            _logger.Admin($"dice.roll.test replacement newTimestamp={newTimestamp:o}");
            _logger.Admin("dice.roll.test replacement updated=true");
            _logger.Admin($"dice.roll.test replacedPrevious=true actor={actor.Login} requestId={existing.Id}");
            _logger.Admin($"dice.roll.test actor={actor.Login} action=replace requestId={existing.Id} total={existing.Result?.Total ?? 0} replacedPrevious=true");
            return Ok("Test dice roll replaced.", DiceRequestPayload(existing, actor));
        }
        catch (Exception ex)
        {
            _logger.Admin($"dice.roll.test.fail actor={actor.Login} reason={ex.GetType().Name}:{ex.Message}");
            throw;
        }
    }

    public ResponseEnvelope DiceTestGetCurrent(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var requestedUserId = PayloadReader.GetString(context.Request.Payload, "userId");
        var userId = (!string.IsNullOrWhiteSpace(requestedUserId) && (actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin)))
            ? requestedUserId
            : actor.Id;
        var existing = _repositories.DiceRequests.Find(
                Builders<DiceRollRequest>.Filter.Eq(x => x.IsTestRoll, true) &
                Builders<DiceRollRequest>.Filter.Eq(x => x.TestRollOwnerUserId, userId) &
                Builders<DiceRollRequest>.Filter.Eq(x => x.Deleted, false))
            .OrderByDescending(x => x.UpdatedUtc)
            .FirstOrDefault();
        _logger.Admin($"dice.test.getCurrent actor={actor.Login} userId={userId} found={(existing != null)}");
        if (existing == null) return Ok("No current test roll.", new Dictionary<string, object> { { "item", new Dictionary<string, object>() } });
        return Ok("Current test roll loaded.", new Dictionary<string, object> { { "item", DiceRequestPayload(existing, actor) } });
    }

    private DiceRollRequest CreateResolvedDiceRoll(CommandContext context, UserAccount actor, bool isTestRoll)
    {
        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId");
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            var character = GetCharacter(RequireLength(characterId, 8, 128, "characterId"));
            if (character.OwnerUserId != actor.Id && !actor.Roles.Contains(UserRole.Admin) && !actor.Roles.Contains(UserRole.SuperAdmin))
                throw new UnauthorizedAccessException("Character unavailable for dice roll.");
        }

        var rawComment = FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "description"),
            PayloadReader.GetString(context.Request.Payload, "comment"),
            PayloadReader.GetString(context.Request.Payload, "note"),
            PayloadReader.GetString(context.Request.Payload, "text"));
        var description = RequireLength(rawComment, 0, 1024, "description");
        _logger.Admin($"dice.roll.comment received={!string.IsNullOrWhiteSpace(description)} length={description.Length}");
        var formulaInput = RequireLength(PayloadReader.GetString(context.Request.Payload, "formula"), 3, 64, "formula");
        var visibilityRaw = (PayloadReader.GetString(context.Request.Payload, "visibility") ?? RequestVisibility.Public.ToString());
        if (!Enum.TryParse(visibilityRaw, true, out RequestVisibility visibility)) visibility = RequestVisibility.Public;
        var formula = DiceFormulaParser.Parse(formulaInput);
        var result = DiceRollExecutor.Execute(formula, visibility, actor.Id);
        if (!FateMvpPipelineEnabled()) ApplyFateToRealDiceRoll(formula, result);
        var audio = DiceSoundResolver.Resolve(formula, result.Rolls);
        result.SoundKey = audio.SoundKey;
        result.SoundEasterTriggered = audio.EasterTriggered;
        _logger.Admin($"dice.audio.soundKey resolved={result.SoundKey}");
        _logger.Admin($"dice.audio.easter triggered={result.SoundEasterTriggered}");
        var request = new DiceRollRequest
        {
            RequestType = isTestRoll ? "DiceRollTest" : "DiceRollStandard",
            CreatorUserId = actor.Id,
            RelatedUserId = actor.Id,
            CharacterId = characterId,
            Description = description,
            RawFormula = formulaInput,
            Formula = formula,
            Visibility = visibility,
            Status = RequestStatus.Approved,
            IsTestRoll = isTestRoll,
            TestRollOwnerUserId = isTestRoll ? actor.Id : string.Empty,
            Result = result,
            PayloadJson = "{}",
            Fingerprint = BuildFingerprint(isTestRoll ? "dice-test" : "dice-standard", actor.Id, characterId, formula.Normalized + ":" + visibility)
        };
        request.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = isTestRoll ? "CreatedTest" : "CreatedStandard", Comment = formula.Normalized });
        ApplyFateMvpToDiceRequestIfEnabled(context, actor, request, FateRollTypes.Dice, string.Empty, string.Empty, new[] { "dice" });
        return request;
    }

    public ResponseEnvelope RequestCancel(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var requestId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requestId"), 8, 128, "requestId");

        if (PlayerRequestPlayerEnabled() && _repositories.PlayerRequests.GetById(requestId) != null)
            return PlayerRequestCancel(context);

        var dice = _repositories.DiceRequests.GetById(requestId);
        if (dice != null)
        {
            if (dice.CreatorUserId != actor.Id) throw new UnauthorizedAccessException("Cannot cancel another user's request.");
            if (dice.Status != RequestStatus.Pending) throw new InvalidOperationException("Only pending requests can be cancelled.");
            dice.Status = RequestStatus.Cancelled;
            dice.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "Cancelled", Comment = string.Empty });
            _repositories.DiceRequests.Replace(dice);
            WriteAudit("request", actor.Id, "cancel", dice.Id);
            return Ok("Request cancelled.");
        }

        var action = _repositories.ActionRequests.GetById(requestId) ?? throw new KeyNotFoundException("Request not found.");
        if (action.CreatorUserId != actor.Id) throw new UnauthorizedAccessException("Cannot cancel another user's request.");
        if (action.Status != RequestStatus.Pending) throw new InvalidOperationException("Only pending requests can be cancelled.");
        action.Status = RequestStatus.Cancelled;
        action.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "Cancelled", Comment = string.Empty });
        _repositories.ActionRequests.Replace(action);
        WriteAudit("request", actor.Id, "cancel", action.Id);
        return Ok("Request cancelled.");
    }

    public ResponseEnvelope RequestListMine(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var actions = _repositories.ActionRequests.Find(Builders<ActionRequest>.Filter.Eq(x => x.CreatorUserId, actor.Id)).Select(RequestPayload).Cast<object>();
        var dice = _repositories.DiceRequests.Find(
                Builders<DiceRollRequest>.Filter.Eq(x => x.CreatorUserId, actor.Id) &
                Builders<DiceRollRequest>.Filter.Eq(x => x.IsTestRoll, false))
            .Select(x => (object)DiceRequestPayload(x, actor));
        var playerRequests = PlayerRequestPlayerEnabled()
            ? _repositories.PlayerRequests.Find(Builders<PlayerRequestState>.Filter.Eq(x => x.CreatedByUserId, actor.Id))
                .Select(x => (object)PlayerRequestPayload(x, actor, includeAdminFields: false))
            : Enumerable.Empty<object>();
        return Ok("My requests loaded.", new Dictionary<string, object> { { "items", actions.Concat(dice).Concat(playerRequests).ToArray() } });
    }

    public ResponseEnvelope RequestListPending(CommandContext context)
    {
        RequireAdmin(context);
        var actions = _repositories.ActionRequests.Find(Builders<ActionRequest>.Filter.Eq(x => x.Status, RequestStatus.Pending)).Select(RequestPayload).Cast<object>();
        var dice = _repositories.DiceRequests.Find(
                Builders<DiceRollRequest>.Filter.Eq(x => x.Status, RequestStatus.Pending) &
                Builders<DiceRollRequest>.Filter.Eq(x => x.IsTestRoll, false))
            .Select(x => (object)DiceRequestPayload(x, GetCurrentAccount(context))).Cast<object>();
        var actor = GetCurrentAccount(context);
        var playerRequests = PlayerRequestAdminReviewEnabled()
            ? _repositories.PlayerRequests.Find(
                    Builders<PlayerRequestState>.Filter.Eq(x => x.Status, PlayerRequestStatusIds.Submitted) |
                    Builders<PlayerRequestState>.Filter.Eq(x => x.Status, PlayerRequestStatusIds.InReview))
                .Select(x => (object)PlayerRequestPayload(x, actor, includeAdminFields: true))
            : Enumerable.Empty<object>();
        return Ok("Pending requests loaded.", new Dictionary<string, object> { { "items", actions.Concat(dice).Concat(playerRequests).ToArray() } });
    }

    public ResponseEnvelope RequestGetDetails(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var requestId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requestId"), 8, 128, "requestId");
        var action = _repositories.ActionRequests.GetById(requestId);
        if (action != null)
        {
            EnsureCanViewRequest(actor, action.CreatorUserId);
            return Ok("Request loaded.", RequestPayload(action));
        }

        var playerRequest = _repositories.PlayerRequests.GetById(requestId);
        if (playerRequest != null)
        {
            if (playerRequest.CreatedByUserId != actor.Id && !IsAdmin(actor)) throw new UnauthorizedAccessException("Request is not visible for current user.");
            return Ok("Request loaded.", PlayerRequestPayload(playerRequest, actor, includeAdminFields: IsAdmin(actor)));
        }

        var dice = _repositories.DiceRequests.GetById(requestId) ?? throw new KeyNotFoundException("Request not found.");
        EnsureCanViewDice(actor, dice);
        return Ok("Request loaded.", DiceRequestPayload(dice, actor));
    }

    public ResponseEnvelope RequestApprove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var requestId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requestId"), 8, 128, "requestId");
        var adminComment = RequireLength(PayloadReader.GetString(context.Request.Payload, "comment"), 0, 2048, "comment");

        var action = _repositories.ActionRequests.GetById(requestId);
        if (action != null)
        {
            if (action.Status != RequestStatus.Pending) throw new InvalidOperationException("Request is not pending.");
            action.Status = RequestStatus.Approved;
            action.Decision = new RequestDecision { DecidedByUserId = actor.Id, DecidedAtUtc = DateTime.UtcNow, AdminComment = adminComment };
            action.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "Approved", Comment = adminComment });
            _repositories.ActionRequests.Replace(action);
            WriteAudit("request", actor.Id, "approve", action.Id);
            return Ok("Request approved.", RequestPayload(action));
        }

        if (PlayerRequestAdminReviewEnabled() && _repositories.PlayerRequests.GetById(requestId) != null)
            return AdminPlayerRequestApprove(context);

        var dice = _repositories.DiceRequests.GetById(requestId) ?? throw new KeyNotFoundException("Request not found.");
        if (dice.Status != RequestStatus.Pending) throw new InvalidOperationException("Request is not pending.");
        dice.Status = RequestStatus.Approved;
        dice.Result = DiceRollExecutor.Execute(dice.Formula, dice.Visibility, actor.Id);
        if (!FateMvpPipelineEnabled()) ApplyFateToRealDiceRoll(dice.Formula, dice.Result);
        ApplyFateMvpToDiceRequestIfEnabled(context, actor, dice, FateRollTypes.Dice, string.Empty, string.Empty, new[] { "dice" });
        dice.Decision = new RequestDecision { DecidedByUserId = actor.Id, DecidedAtUtc = DateTime.UtcNow, AdminComment = adminComment };
        dice.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "Approved", Comment = adminComment });
        _repositories.DiceRequests.Replace(dice);
        WriteAudit("dice", actor.Id, "approve", dice.Id);
        _logger.Session($"Dice roll approved: {dice.Formula.Normalized} => {dice.Result.Total}");
        return Ok("Request approved.", DiceRequestPayload(dice, actor));
    }

    public ResponseEnvelope RequestReject(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var requestId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requestId"), 8, 128, "requestId");
        var adminComment = RequireLength(PayloadReader.GetString(context.Request.Payload, "comment"), 0, 2048, "comment");

        var action = _repositories.ActionRequests.GetById(requestId);
        if (action != null)
        {
            if (action.Status != RequestStatus.Pending) throw new InvalidOperationException("Request is not pending.");
            action.Status = RequestStatus.Rejected;
            action.Decision = new RequestDecision { DecidedByUserId = actor.Id, DecidedAtUtc = DateTime.UtcNow, AdminComment = adminComment };
            action.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "Rejected", Comment = adminComment });
            _repositories.ActionRequests.Replace(action);
            WriteAudit("request", actor.Id, "reject", action.Id);
            return Ok("Request rejected.", RequestPayload(action));
        }

        if (PlayerRequestAdminReviewEnabled() && _repositories.PlayerRequests.GetById(requestId) != null)
            return AdminPlayerRequestReject(context);

        var dice = _repositories.DiceRequests.GetById(requestId) ?? throw new KeyNotFoundException("Request not found.");
        if (dice.Status != RequestStatus.Pending) throw new InvalidOperationException("Request is not pending.");
        dice.Status = RequestStatus.Rejected;
        dice.Decision = new RequestDecision { DecidedByUserId = actor.Id, DecidedAtUtc = DateTime.UtcNow, AdminComment = adminComment };
        dice.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "Rejected", Comment = adminComment });
        _repositories.DiceRequests.Replace(dice);
        WriteAudit("request", actor.Id, "reject", dice.Id);
        return Ok("Request rejected.", DiceRequestPayload(dice, actor));
    }

    public ResponseEnvelope RequestHistory(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var includeAll = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);

        var actions = includeAll
            ? _repositories.ActionRequests.Find(FilterDefinition<ActionRequest>.Empty)
            : _repositories.ActionRequests.Find(Builders<ActionRequest>.Filter.Eq(x => x.CreatorUserId, actor.Id));
        var dice = includeAll
            ? _repositories.DiceRequests.Find(FilterDefinition<DiceRollRequest>.Empty)
            : _repositories.DiceRequests.Find(Builders<DiceRollRequest>.Filter.Eq(x => x.CreatorUserId, actor.Id));

        var payload = new List<object>();
        payload.AddRange(actions.Select(x => (object)RequestPayload(x)));
        payload.AddRange(dice.Where(x => includeAll || CanViewDice(actor, x)).Select(x => (object)DiceRequestPayload(x, actor)));
        if (PlayerRequestsBaseEnabled())
        {
            var playerRequests = includeAll
                ? _repositories.PlayerRequests.Find(FilterDefinition<PlayerRequestState>.Empty)
                : _repositories.PlayerRequests.Find(Builders<PlayerRequestState>.Filter.Eq(x => x.CreatedByUserId, actor.Id));
            payload.AddRange(playerRequests.Select(x => (object)PlayerRequestPayload(x, actor, includeAdminFields: includeAll)));
        }
        return Ok("Request history loaded.", new Dictionary<string, object> { { "items", payload.ToArray() } });
    }

    public ResponseEnvelope DiceHistory(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var items = _repositories.DiceRequests.Find(FilterDefinition<DiceRollRequest>.Empty)
            .Where(x => CanViewDice(actor, x))
            .Select(x => (object)DiceRequestPayload(x, actor)).ToArray();
        _logger.Admin($"dice.history.get actor={actor.Login} count={items.Length}");
        return Ok("Dice history loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope DiceVisibleFeed(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var approvedItems = _repositories.DiceRequests.Find(Builders<DiceRollRequest>.Filter.Eq(x => x.Status, RequestStatus.Approved))
            .ToArray();
        _logger.Admin($"dice.feed.common itemsRaw={approvedItems.Length}");
        var items = approvedItems
            .Where(x => CanViewDice(actor, x))
            .OrderByDescending(x => x.UpdatedUtc)
            .Take(100)
            .Select(x => (object)DiceRequestPayload(x, actor)).ToArray();
        _logger.Admin($"dice.feed.common itemsMapped={items.Length}");
        _logger.Admin($"dice.visibleFeed actor={actor.Login} count={items.Length}");
        return Ok("Dice feed loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope DiceGetDetails(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var requestId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requestId"), 8, 128, "requestId");
        var dice = _repositories.DiceRequests.GetById(requestId) ?? throw new KeyNotFoundException("Dice request not found.");
        EnsureCanViewDice(actor, dice);
        return Ok("Dice details loaded.", DiceRequestPayload(dice, actor));
    }

    private void EnsureCanCreateByFingerprint(string creatorUserId, string fingerprint)
    {
        var pendingSameAction = _repositories.ActionRequests.Find(Builders<ActionRequest>.Filter.Eq(x => x.CreatorUserId, creatorUserId) & Builders<ActionRequest>.Filter.Eq(x => x.Fingerprint, fingerprint) & Builders<ActionRequest>.Filter.Eq(x => x.Status, RequestStatus.Pending)).Any();
        if (pendingSameAction) throw new InvalidOperationException("A pending equivalent request already exists.");

        var pendingDice = _repositories.DiceRequests.Find(Builders<DiceRollRequest>.Filter.Eq(x => x.CreatorUserId, creatorUserId) & Builders<DiceRollRequest>.Filter.Eq(x => x.Fingerprint, fingerprint) & Builders<DiceRollRequest>.Filter.Eq(x => x.Status, RequestStatus.Pending)).Any();
        if (pendingDice) throw new InvalidOperationException("A pending equivalent request already exists.");

        var rejectCount = GetRejectionCount(creatorUserId, fingerprint);
        if (rejectCount >= 2)
        {
            _logger.Admin($"Blocked request by rejection limit user={creatorUserId} fingerprint={fingerprint}");
            throw new UnauthorizedAccessException("Equivalent request was rejected twice and cannot be submitted again.");
        }
    }

    private int GetRejectionCount(string creatorUserId, string fingerprint)
    {
        var actionRejected = _repositories.ActionRequests.Find(Builders<ActionRequest>.Filter.Eq(x => x.CreatorUserId, creatorUserId) & Builders<ActionRequest>.Filter.Eq(x => x.Fingerprint, fingerprint) & Builders<ActionRequest>.Filter.Eq(x => x.Status, RequestStatus.Rejected)).Count;
        var diceRejected = _repositories.DiceRequests.Find(Builders<DiceRollRequest>.Filter.Eq(x => x.CreatorUserId, creatorUserId) & Builders<DiceRollRequest>.Filter.Eq(x => x.Fingerprint, fingerprint) & Builders<DiceRollRequest>.Filter.Eq(x => x.Status, RequestStatus.Rejected)).Count;
        return actionRejected + diceRejected;
    }

    private static string BuildFingerprint(string actionType, string actorUserId, string? characterId, string normalizedPayload)
    {
        var payload = (normalizedPayload ?? string.Empty).Trim().ToLowerInvariant();
        return $"{actionType.Trim().ToLowerInvariant()}|{actorUserId}|{(characterId ?? string.Empty)}|{payload}";
    }

    private void EnsureCanViewRequest(UserAccount actor, string creatorUserId)
    {
        if (actor.Id == creatorUserId) return;
        if (actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin)) return;
        throw new UnauthorizedAccessException("Request is not visible for current user.");
    }

    private bool CanViewDice(UserAccount actor, DiceRollRequest request)
    {
        var isAdmin = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
        if (request.CreatorUserId == actor.Id) return true;
        if (request.Visibility == RequestVisibility.Public) return true;
        if (request.Visibility == RequestVisibility.HiddenToAdmins || request.Visibility == RequestVisibility.PlayerShadow) return !isAdmin;
        if (request.Visibility == RequestVisibility.AdminOnly || request.Visibility == RequestVisibility.AdminOnlyShadow) return isAdmin;
        return false;
    }

    private void ApplyFateToRealDiceRoll(DiceFormulaSpec formula, DiceRollResult result)
    {
        if (result == null || formula == null || result.Rolls.Count == 0)
        {
            return;
        }

        var settings = _fateState.GetSnapshot();
        _logger.Admin($"dice.roll.fate.state instance={_fateState.InstanceId} enabled={settings.Enabled} effects={BuildEffectSummary(settings.Layers)} formula={formula.Normalized}");
        if (!settings.Enabled)
        {
            _logger.Admin($"dice.roll.fate.disabled reason=settings.Enabled=false instance={_fateState.InstanceId} formula={formula.Normalized}");
            return;
        }

        result.BaseRolls = new List<int>(result.Rolls);
        result.FateRolls = result.Rolls.Select(_ => (int?)null).ToList();
        result.FateAppliedByDie = result.Rolls.Select(_ => false).ToList();

        var fatePipeline = new FateEnginePipeline();
        for (var i = 0; i < result.Rolls.Count; i++)
        {
            var baseRoll = result.Rolls[i];
            _logger.Admin($"dice.roll.fate.before dieIndex={i} dieSides={formula.DiceSides} base={baseRoll}");
            var fateRequest = new FateEngineRequest
            {
                BaseRoll = baseRoll,
                DieSides = formula.DiceSides,
                RollType = "real-dice-roll"
            };

            var fateResult = fatePipeline.Process(fateRequest, settings);
            if (!fateResult.Applied)
            {
                _logger.Admin($"dice.roll.fate.after dieIndex={i} applied=false fateValue={baseRoll} skippedReason={fateResult.SkippedReason}");
                if (string.Equals(fateResult.SkippedReason, "d4 or lower", StringComparison.OrdinalIgnoreCase))
                    _logger.Admin("dice.roll.fate.bypass reason=d4 or lower");
                continue;
            }

            result.Rolls[i] = fateResult.FateValue;
            result.FateRolls[i] = fateResult.FateValue;
            result.FateAppliedByDie[i] = true;
            _logger.Admin($"dice.roll.fate applied=true formula={formula.Normalized} dieIndex={i} base={baseRoll} fate={fateResult.FateValue} dieSides={formula.DiceSides} layers={fateResult.Layers.Count}");
            _logger.Admin($"dice.roll.fate.after dieIndex={i} applied=true fateValue={fateResult.FateValue} skippedReason=");
            _logger.Admin($"dice.roll.public dieIndex={i} base={result.BaseRolls[i]} fate={fateResult.FateValue} public={result.Rolls[i]}");
            foreach (var layer in fateResult.Layers.OrderBy(x => x.LayerNumber))
            {
                _logger.Debug($"dice.roll.fate.trace dieIndex={i} layer={layer.LayerNumber} effect={layer.EffectCode} input={layer.InputValue} output={layer.OutputValue} reason={layer.Reason}");
            }
        }

        result.Total = result.Rolls.Sum() + formula.Modifier;
        _logger.Admin($"dice.roll.total publicTotal={result.Total}");
        _logger.Admin($"dice.roll.payload values=[{string.Join(",", result.Rolls)}]");
    }

    private void EnsureCanViewDice(UserAccount actor, DiceRollRequest request)
    {
        if (!CanViewDice(actor, request)) throw new UnauthorizedAccessException("Dice request not visible.");
    }

    private static Dictionary<string, object> RequestPayload(ActionRequest request)
    {
        return new Dictionary<string, object>
        {
            { "requestId", request.Id },
            { "requestType", request.RequestType },
            { "actionCode", request.ActionCode },
            { "creatorUserId", request.CreatorUserId },
            { "characterId", request.CharacterId ?? string.Empty },
            { "status", request.Status.ToString() },
            { "description", request.Description },
            { "fingerprint", request.Fingerprint },
            { "rejections", request.RejectionCountForFingerprint },
            { "adminComment", request.Decision.AdminComment },
            { "history", request.History.Select(h => new Dictionary<string, object>{{"at",h.TimestampUtc},{"actor",h.ActorUserId},{"action",h.Action},{"comment",h.Comment}}).Cast<object>().ToArray() }
        };
    }

    private Dictionary<string, object> DiceRequestPayload(DiceRollRequest request, UserAccount viewer)
    {
        var creatorLogin = GetAccountLogin(request.CreatorUserId);
        var basePayload = new Dictionary<string, object>
        {
            { "requestId", request.Id },
            { "requestType", request.RequestType },
            { "creatorUserId", request.CreatorUserId },
            { "creatorLogin", creatorLogin },
            { "characterId", request.CharacterId ?? string.Empty },
            { "status", request.Status.ToString() },
            { "createdUtc", request.CreatedUtc },
            { "updatedUtc", request.UpdatedUtc },
            { "requestedUtc", request.CreatedUtc },
            { "description", request.Description },
            { "isTestRoll", request.IsTestRoll },
            { "visibility", request.Visibility.ToString() },
            { "formula", request.Formula.Normalized },
            { "rawFormula", request.RawFormula },
            { "fingerprint", request.Fingerprint },
            { "rejections", request.RejectionCountForFingerprint },
            { "adminComment", request.Decision.AdminComment },
            { "history", request.History.Select(h => new Dictionary<string, object>{{"at",h.TimestampUtc},{"actor",h.ActorUserId},{"action",h.Action},{"comment",h.Comment}}).Cast<object>().ToArray() }
        };

        if (request.Result != null && CanViewDice(viewer, request))
        {
            basePayload["resolvedUtc"] = request.Result.ApprovedAtUtc;
            basePayload["result"] = new Dictionary<string, object>
            {
                { "normalizedFormula", request.Result.NormalizedFormula },
                { "rolls", request.Result.Rolls.Cast<object>().ToArray() },
                { "baseRolls", request.Result.BaseRolls.Cast<object>().ToArray() },
                { "fateRolls", request.Result.FateRolls.Select(x => x.HasValue ? (object)x.Value : string.Empty).ToArray() },
                { "fateAppliedByDie", request.Result.FateAppliedByDie.Cast<object>().ToArray() },
                { "modifier", request.Result.Modifier },
                { "total", request.Result.Total },
                { "visibility", request.Result.Visibility.ToString() },
                { "approvedBy", request.Result.ApprovedByUserId },
                { "approvedAt", request.Result.ApprovedAtUtc },
                { "soundKey", request.Result.SoundKey },
                { "soundEasterTriggered", request.Result.SoundEasterTriggered }
            };
        }

        return basePayload;
    }

    private string GetAccountLogin(string accountId)
    {
        var account = _repositories.Accounts.GetById(accountId);
        return string.IsNullOrWhiteSpace(account?.Login) ? accountId : account.Login!;
    }


    public ResponseEnvelope CharacterProfileConsistencyVerify(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var report = _profileConsistencyService.VerifyCharacterAsync(characterId, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
        WriteAudit("profile", actor.Id, "consistency.verify", $"{characterId}:{report.IsConsistent}:{report.TotalDifferenceCount}:{context.Request.RequestId ?? string.Empty}");
        return Ok("Character profile consistency verified.", new Dictionary<string, object>
        {
            { "characterId", report.CharacterId },
            { "isConsistent", report.IsConsistent },
            { "totalDifferenceCount", report.TotalDifferenceCount },
            { "sections", report.SectionReports.Select(x => new Dictionary<string, object>{{"section", x.Section},{"hasPersistedProfile", x.HasPersistedProfile},{"isConsistent", x.IsConsistent},{"differenceCount", x.DifferenceCount},{"severity", x.Severity},{"differences", x.Differences.Cast<object>().ToArray()}}).Cast<object>().ToArray() }
        });
    }

    private UserAccount RequireAdmin(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);
        return actor;
    }

    private UserAccount GetCurrentAccount(CommandContext context)
    {
        if (context.Session == null) throw new UnauthorizedAccessException("Session is required.");
        return GetAccount(context.Session.UserId);
    }

    private UserAccount GetAccount(string id) => _repositories.Accounts.GetById(id) ?? throw new KeyNotFoundException("Account not found.");
    private Character GetCharacter(string id) => _repositories.Characters.GetById(id) ?? throw new KeyNotFoundException("Character not found.");
    private UserProfile GetProfile(string id) => _repositories.Profiles.GetById(id) ?? throw new KeyNotFoundException("Profile not found.");

    private void WriteAudit(string category, string actorUserId, string action, string target)
    {
        _repositories.AuditLogs.Insert(new AuditLogEntry { Category = category, ActorUserId = actorUserId, Action = action, Target = target });
        _logger.Audit($"{category}:{action} actor={actorUserId} target={target}");
    }

    private void TryPublishSyncEvent(string type, string scope, string entityType, string entityId, string operation, string actorUserId, Dictionary<string, object>? payload, string requestId)
    {
        try
        {
            if (string.Equals(scope, SyncScopes.Definitions, StringComparison.OrdinalIgnoreCase)
                || string.Equals(scope, SyncScopes.Fate, StringComparison.OrdinalIgnoreCase) && type.StartsWith("fate.settings", StringComparison.OrdinalIgnoreCase))
            {
                _syncEvents.PublishGlobal(type, entityType, entityId, operation, actorUserId, payload, requestId);
                return;
            }

            var campaignId = payload != null && payload.TryGetValue("campaignId", out var campaignValue)
                ? Convert.ToString(campaignValue) ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(campaignId) && scope.StartsWith("campaign:", StringComparison.OrdinalIgnoreCase))
                campaignId = scope.Substring("campaign:".Length);
            if (string.IsNullOrWhiteSpace(campaignId) && _repositories.Campaigns.GetById(scope) != null)
                campaignId = scope;
            if (!string.IsNullOrWhiteSpace(campaignId))
            {
                _syncEvents.PublishCampaign(campaignId, type, entityType, entityId, operation, actorUserId, payload, requestId);
                return;
            }

            var sessionId = scope.StartsWith("session:", StringComparison.OrdinalIgnoreCase) ? scope.Substring("session:".Length)
                : scope.StartsWith("chat:", StringComparison.OrdinalIgnoreCase) ? scope.Substring("chat:".Length) : string.Empty;
            if (string.IsNullOrWhiteSpace(sessionId) && _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, scope)).Any())
                sessionId = scope;
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                _syncEvents.PublishSessionById(sessionId, type, entityType, entityId, operation, actorUserId, payload, requestId);
                return;
            }

            if (scope.StartsWith("character:", StringComparison.OrdinalIgnoreCase))
            {
                _syncEvents.PublishCharacter(scope.Substring("character:".Length), type, entityType, entityId, operation, actorUserId, payload, requestId);
                return;
            }
            _syncEvents.PublishEntity(type, entityType, entityId, operation, actorUserId, payload, requestId);
        }
        catch (Exception ex)
        {
            _logger.Debug($"sync.publish.error requestId={requestId} type={type} entityId={entityId} message={ex.Message}");
        }
    }

    private static Dictionary<string, object> AccountPayload(UserAccount x) => new Dictionary<string, object>
    {
        { "accountId", x.Id }, { "login", x.Login }, { "status", x.Status.ToString() }, { "roles", x.Roles.Select(r => r.ToString()).ToArray() }, { "lastLoginUtc", x.LastLoginUtc.HasValue ? (object)x.LastLoginUtc.Value : string.Empty }
    };

    private static Dictionary<string, object> ProfilePayload(UserProfile x) => new Dictionary<string, object>
    {
        { "profileId", x.Id }, { "displayName", x.DisplayName }, { "race", x.Race }, { "age", x.Age.HasValue ? (object)x.Age.Value : string.Empty }, { "description", x.Description }, { "backstory", x.Backstory }
    };

    private static Dictionary<string, object> LockPayload(EntityLock x) => new Dictionary<string, object>
    {
        { "entityType", x.EntityType }, { "entityId", x.EntityId }, { "lockedByUserId", x.LockedByUserId }, { "ownerLevel", x.OwnerLevel.ToString() }, { "issuedUtc", x.IssuedUtc }, { "expiresUtc", x.ExpiresUtc }
    };

    private static ResponseEnvelope Ok(string message, Dictionary<string, object>? payload = null) => new ResponseEnvelope { Status = ResponseStatus.Ok, Message = message, Payload = payload ?? new Dictionary<string, object>() };
    private static ResponseEnvelope Error(string message, ResponseStatus status, ErrorCode code) => new ResponseEnvelope { Status = status, ErrorCode = code, Message = message };

    private static string RequireLength(string? value, int min, int max, string field)
    {
        var actual = value ?? string.Empty;
        if (actual.Length < min || actual.Length > max) throw new ArgumentException($"{field} length must be between {min} and {max}");
        return actual;
    }

    private static int RequireRange(int? value, int min, int max, string field)
    {
        if (!value.HasValue || value.Value < min || value.Value > max) throw new ArgumentException($"{field} must be in range {min}..{max}");
        return value.Value;
    }
}

public sealed class DelegateCommandHandler : ICommandHandler, IIdentifiedCommandHandler02110
{
    private readonly Func<CommandContext, ResponseEnvelope> _handler;
    public DelegateCommandHandler(Func<CommandContext, ResponseEnvelope> handler)
    {
        _handler = handler;
        HandlerIdentity = $"{handler.Method.DeclaringType?.FullName ?? "unknown"}.{handler.Method.Name}";
    }
    public string HandlerIdentity { get; }
    public ResponseEnvelope Handle(CommandContext context) => _handler(context);
}
