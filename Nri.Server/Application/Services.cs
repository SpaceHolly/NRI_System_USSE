using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private readonly INriRepositoryFactory _repositories;
    private readonly SessionManager _sessionManager;
    private readonly IServerLogger _logger;
    private readonly string _audioFolderPath;

    public ServiceHub(INriRepositoryFactory repositories, SessionManager sessionManager, IServerLogger logger, string audioFolderPath)
    {
        _repositories = repositories;
        _sessionManager = sessionManager;
        _logger = logger;
        _audioFolderPath = string.IsNullOrWhiteSpace(audioFolderPath) ? "./audio" : audioFolderPath;
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
        var actor = GetCurrentAccount(context);
        var items = _repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.OwnerUserId, actor.Id)).Select(c => CharacterSummaryPayload(c, actor, actor)).Cast<object>().ToArray();
        return Ok("Characters loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CharacterListByOwner(CommandContext context)
    {
        RequireAdmin(context);
        var ownerId = RequireLength(PayloadReader.GetString(context.Request.Payload, "ownerUserId"), 8, 128, "ownerUserId");
        var owner = GetAccount(ownerId);
        var actor = GetCurrentAccount(context);
        var items = _repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.OwnerUserId, ownerId)).Select(c => CharacterSummaryPayload(c, owner, actor)).Cast<object>().ToArray();
        return Ok("Characters loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CharacterGetActive(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var p = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, actor.Id)).FirstOrDefault();
        if (p == null || string.IsNullOrWhiteSpace(p.ActiveCharacterId)) return Ok("No active character.");
        var c = _repositories.Characters.GetById(p.ActiveCharacterId);
        if (c == null || c.Deleted) return Ok("No active character.");
        return Ok("Active character loaded.", CharacterDetailsPayload(c, actor, actor));
    }

    public ResponseEnvelope CharacterGetDetails(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character details unavailable.");
        return Ok("Character details loaded.", CharacterDetailsPayload(c, owner, actor));
    }

    public ResponseEnvelope CharacterGetSummary(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character summary unavailable.");
        return Ok("Character summary loaded.", CharacterSummaryPayload(c, owner, actor));
    }

    public ResponseEnvelope CharacterGetCompanions(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character companions unavailable.");
        var payload = CharacterDetailsPayload(c, owner, actor);
        return Ok("Companions loaded.", new Dictionary<string, object> { { "companions", payload["companions"] } });
    }

    public ResponseEnvelope CharacterGetInventory(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character inventory unavailable.");
        var payload = CharacterDetailsPayload(c, owner, actor);
        return Ok("Inventory loaded.", new Dictionary<string, object> { { "inventory", payload["inventory"] } });
    }

    public ResponseEnvelope CharacterGetReputation(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character reputation unavailable.");
        var payload = CharacterDetailsPayload(c, owner, actor);
        return Ok("Reputation loaded.", new Dictionary<string, object> { { "reputation", payload["reputation"] } });
    }

    public ResponseEnvelope CharacterGetHoldings(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(c.OwnerUserId);
        if (!CanViewCharacter(actor, owner, c)) throw new UnauthorizedAccessException("Character holdings unavailable.");
        var payload = CharacterDetailsPayload(c, owner, actor);
        return Ok("Holdings loaded.", new Dictionary<string, object> { { "holdings", payload["holdings"] } });
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
            _logger.Admin($"{flow}.success actor={actor.Login} owner={ownerId} characterId={character.Id}");
            return Ok("Character created.", CharacterDetailsPayload(character, GetAccount(ownerId), actor));
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
        if (c.OwnerUserId != userId || c.Deleted) throw new InvalidOperationException("Character does not belong to user or archived.");

        var p = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, userId)).FirstOrDefault() ?? new SessionUserState { UserId = userId };
        if (string.IsNullOrWhiteSpace(p.Id)) _repositories.Presence.Insert(p);
        p.ActiveCharacterId = characterId;
        _repositories.Presence.Replace(p);
        WriteAudit("character", actor.Id, "assignActive", c.Id);
        return Ok("Active character assigned.");
    }

    public ResponseEnvelope CharacterUpdateBasicInfo(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        c.Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
        c.Race = RequireLength(PayloadReader.GetString(context.Request.Payload, "race"), 2, 64, "race");
        c.Height = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 1, 64, "height");
        c.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
        c.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
        c.Age = PayloadReader.GetInt(context.Request.Payload, "age");
        _repositories.Characters.Replace(c);
        WriteAudit("character", actor.Id, "updateBasic", c.Id);
        return Ok("Character basic info updated.");
    }

    public ResponseEnvelope CharacterUpdateStats(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
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
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var moneyRaw = PayloadReader.GetDictionary(context.Request.Payload, "money") ?? new Dictionary<string, object>();
        c.Wallet.EnsureAllDenominations();
        foreach (CurrencyDenomination d in Enum.GetValues(typeof(CurrencyDenomination)))
        {
            var value = PayloadReader.GetLong(moneyRaw, d.ToString());
            if (value.HasValue && value.Value >= 0) c.Wallet.Balance.Amounts[d.ToString()] = value.Value;
        }
        c.Wallet.NormalizeUpward();
        _repositories.Characters.Replace(c);
        WriteAudit("character", actor.Id, "updateMoney", c.Id);
        return Ok("Character money updated.", new Dictionary<string, object> { { "money", WalletPayload(c.Wallet) } });
    }

    public ResponseEnvelope CharacterUpdateInventory(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        c.Inventory = ParseInventoryList(PayloadReader.GetList(context.Request.Payload, "inventory"));
        _repositories.Characters.Replace(c);
        WriteAudit("character", actor.Id, "updateInventory", c.Id);
        return Ok("Character inventory updated.");
    }

    public ResponseEnvelope CharacterUpdateReputation(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        c.Reputation = ParseReputationList(PayloadReader.GetList(context.Request.Payload, "reputation"));
        _repositories.Characters.Replace(c);
        WriteAudit("character", actor.Id, "updateReputation", c.Id);
        return Ok("Character reputation updated.");
    }

    public ResponseEnvelope CharacterUpdateHoldings(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        c.Holdings = ParseHoldingsList(PayloadReader.GetList(context.Request.Payload, "holdings"));
        _repositories.Characters.Replace(c);
        WriteAudit("character", actor.Id, "updateHoldings", c.Id);
        return Ok("Character holdings updated.");
    }

    public ResponseEnvelope CharacterAdminList(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var items = _repositories.Characters.Find(FilterDefinition<Character>.Empty)
            .Where(c => includeArchived || !c.Archived)
            .Select(c =>
            {
                EnsureCharacterDefaults(c);
                return CharacterSummaryPayload(c, GetAccount(c.OwnerUserId), actor);
            })
            .Cast<object>()
            .ToArray();
        _logger.Admin($"character.admin.list actor={actor.Login} count={items.Length} includeArchived={includeArchived}");
        return Ok("Character admin list loaded.", new Dictionary<string, object> { { "items", items } });
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

        var items = _repositories.Characters.Find(FilterDefinition<Character>.Empty)
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
            .Select(c => CharacterSummaryPayload(c, GetAccount(c.OwnerUserId), actor))
            .Cast<object>()
            .ToArray();

        _logger.Admin($"character.admin.search actor={actor.Login} query={query} count={items.Length}");
        return Ok("Character admin search loaded.", new Dictionary<string, object> { { "items", items } });
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
        character.Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
        character.Height = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 0, 64, "height");
        character.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
        character.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
        character.Age = PayloadReader.GetInt(context.Request.Payload, "age");
        var raceCode = (PayloadReader.GetString(context.Request.Payload, "raceCode") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(raceCode))
        {
            var race = _repositories.RaceDefinitions.GetByCode(raceCode) ?? throw new ArgumentException("Race definition not found.");
            character.RaceCode = race.Code;
            character.Race = race.Name;
        }

        _repositories.Characters.Replace(character);
        _logger.Admin($"character.admin.save.basic actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character basic saved.", BuildCharacterAggregatePayload(character, actor, includeNotesContext: false));
    }

    public ResponseEnvelope CharacterAdminSaveStats(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        EnsureCharacterEditAllowed(actor, character.Id);
        EnsureCharacterDefaults(character);
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
        ApplyMoneyFromPayload(character, context.Request.Payload);
        _repositories.Characters.Replace(character);
        _logger.Admin($"character.admin.save.money actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character money saved.", BuildMoneyPayload(character));
    }

    public ResponseEnvelope CharacterAdminSaveProgression(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        EnsureCharacterEditAllowed(actor, character.Id);
        EnsureCharacterDefaults(character);

        var raceCode = PayloadReader.GetString(context.Request.Payload, "raceCode");
        if (!string.IsNullOrWhiteSpace(raceCode))
        {
            var race = _repositories.RaceDefinitions.GetByCode(raceCode) ?? throw new ArgumentException("Race definition not found.");
            character.RaceCode = race.Code;
            character.Race = race.Name;
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

        _repositories.Characters.Replace(character);
        _logger.Admin($"character.admin.save.progression actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character progression saved.", BuildProgressionPayload(character));
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
        character.Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");
        character.Height = RequireLength(PayloadReader.GetString(context.Request.Payload, "height"), 0, 64, "height");
        character.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
        character.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
        character.Age = PayloadReader.GetInt(context.Request.Payload, "age");
        _repositories.Characters.Replace(character);
        _logger.Admin($"character.self.save.basic actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character self basic saved.", BuildCharacterAggregatePayload(character, actor, includeNotesContext: false));
    }

    public ResponseEnvelope CharacterSelfSaveStats(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveOwnedCharacter(context, actor);
        EnsureCharacterDefaults(character);
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
        ApplyMoneyFromPayload(character, context.Request.Payload);
        _repositories.Characters.Replace(character);
        _logger.Admin($"character.self.save.money actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character self money saved.", BuildMoneyPayload(character));
    }

    public ResponseEnvelope CharacterSelfGetProgression(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveOwnedCharacter(context, actor);
        EnsureCharacterDefaults(character);
        _logger.Admin($"character.self.get.progression actor={actor.Login} characterId={character.Id} result=ok");
        return Ok("Character self progression loaded.", BuildProgressionPayload(character));
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

        var existing = FindActiveLock(entityType, entityId);
        if (existing != null && existing.LockedByUserId != actor.Id) throw new InvalidOperationException("Entity is already locked.");

        var lockItem = existing ?? new EntityLock { EntityType = entityType, EntityId = entityId, LockedByUserId = actor.Id };
        lockItem.OwnerLevel = actor.Roles.Contains(UserRole.SuperAdmin) ? LockOwnerLevel.SuperAdmin : LockOwnerLevel.Admin;
        lockItem.IssuedUtc = DateTime.UtcNow;
        lockItem.ExpiresUtc = DateTime.UtcNow.AddHours(1);
        lockItem.Deleted = false;
        if (existing == null) _repositories.Locks.Insert(lockItem); else _repositories.Locks.Replace(lockItem);
        _logger.Admin($"lock.acquire actor={actor.Login} entityType={entityType} entityId={entityId} result={(existing == null ? "new" : "refresh")}");
        return Ok(existing == null ? "Lock acquired." : "Lock refreshed.", LockPayload(lockItem));
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

    private bool CanViewCharacter(UserAccount actor, UserAccount owner, Character character)
    {
        if (actor.Id == owner.Id) return true;
        if (actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin)) return true;
        if (character.Deleted) return false;
        return true;
    }

    private Dictionary<string, object> CharacterSummaryPayload(Character c, UserAccount owner, UserAccount viewer)
    {
        EnsureCharacterDefaults(c);
        var dto = new Dictionary<string, object>
        {
            { "characterId", c.Id },
            { "ownerUserId", c.OwnerUserId },
            { "name", c.Name },
            { "race", c.Race },
            { "height", c.Height },
            { "archived", c.Archived },
            { "deleted", c.Deleted },
            { "schemaVersion", c.SchemaVersion }
        };

        if ((viewer.Id != owner.Id) && !viewer.Roles.Contains(UserRole.Admin) && !viewer.Roles.Contains(UserRole.SuperAdmin) && c.Visibility.HideDescriptionForOthers)
            dto["description"] = "[hidden]";
        else
            dto["description"] = c.Description;

        return dto;
    }

    private Dictionary<string, object> CharacterDetailsPayload(Character c, UserAccount owner, UserAccount viewer)
    {
        EnsureCharacterDefaults(c);
        var isPrivileged = viewer.Id == owner.Id || viewer.Roles.Contains(UserRole.Admin) || viewer.Roles.Contains(UserRole.SuperAdmin);
        var details = CharacterSummaryPayload(c, owner, viewer);
        details["age"] = c.Age.HasValue ? (object)c.Age.Value : string.Empty;
        details["backstory"] = (!isPrivileged && c.Visibility.HideBackstoryForOthers) ? "[hidden]" : c.Backstory;
        details["stats"] = (!isPrivileged && c.Visibility.HideStatsForOthers) ? "[hidden]" : (object)StatsPayload(c.Stats);
        details["money"] = WalletPayload(c.Wallet);
        details["currencies"] = CurrencyListPayload(c);
        details["inventory"] = c.Inventory.Select(InventoryPayload).Cast<object>().ToArray();
        details["companions"] = c.Companions.Select(CompanionPayload).Cast<object>().ToArray();
        details["holdings"] = c.Holdings.Select(x => new Dictionary<string, object> { { "name", x.Name }, { "description", x.Description } }).Cast<object>().ToArray();
        details["reputation"] = (!isPrivileged && c.Visibility.HideReputationForOthers) ? "[hidden]" : (object)c.Reputation.Select(x => new Dictionary<string, object> { { "scope", x.Scope }, { "groupKey", x.GroupKey }, { "value", x.Value } }).Cast<object>().ToArray();
        details["classProgress"] = c.ClassProgress.Select(x => new Dictionary<string, object> { { "classCode", x.ClassCode }, { "level", x.Level }, { "experience", x.Experience } }).Cast<object>().ToArray();
        details["skills"] = c.Skills.Select(x => new Dictionary<string, object> { { "skillCode", x.SkillCode }, { "name", x.Name }, { "description", x.Description }, { "type", x.Type.ToString() }, { "available", x.IsAvailable }, { "reason", x.UnavailableReason } }).Cast<object>().ToArray();
        details["raceCode"] = c.RaceCode;
        details["xpCoins"] = c.XpCoins;
        details["characterClasses"] = c.CharacterClasses.Select(x => new Dictionary<string, object> { { "classCode", x.ClassCode }, { "level", x.Level }, { "learnedUtc", x.LearnedUtc } }).Cast<object>().ToArray();
        details["characterSkills"] = c.CharacterSkills.Select(x => new Dictionary<string, object> { { "skillCode", x.SkillCode }, { "tier", x.Tier }, { "level", x.Level }, { "learnedUtc", x.LearnedUtc } }).Cast<object>().ToArray();
        details["visibility"] = VisibilityPayload(c.Visibility);
        details["notesContext"] = BuildNotesContextPayload(c.Id);
        return details;
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
        { "itemCode", x.ItemCode }, { "label", x.Label }, { "description", x.Description }, { "quantity", x.Quantity }, { "durability", x.Durability ?? 0 }, { "consumptionPerUse", x.ConsumptionPerUse ?? 0 }, { "equipped", x.Equipped }
    };

    private static Dictionary<string, object> CompanionPayload(Companion c) => new Dictionary<string, object>
    {
        { "id", c.Id }, { "name", c.Name }, { "species", c.Species }, { "notes", c.Notes }, { "inventory", c.Inventory.Select(InventoryPayload).Cast<object>().ToArray() }
    };

    private List<InventoryItem> ParseInventoryList(IList<object>? list)
    {
        if (list == null) return new List<InventoryItem>();
        return list.OfType<Dictionary<string, object>>().Select(item => new InventoryItem
        {
            ItemCode = PayloadReader.GetString(item, "itemCode") ?? string.Empty,
            Label = PayloadReader.GetString(item, "label") ?? string.Empty,
            Description = PayloadReader.GetString(item, "description") ?? string.Empty,
            Quantity = RequireRange(PayloadReader.GetInt(item, "quantity"), 0, 100000, "quantity"),
            Durability = PayloadReader.GetInt(item, "durability"),
            ConsumptionPerUse = PayloadReader.GetInt(item, "consumptionPerUse"),
            Equipped = PayloadReader.GetBool(item, "equipped")
        }).ToList();
    }

    private List<ReputationRef> ParseReputationList(IList<object>? list)
    {
        if (list == null) return new List<ReputationRef>();
        return list.OfType<Dictionary<string, object>>().Select(item => new ReputationRef
        {
            Scope = RequireLength(PayloadReader.GetString(item, "scope"), 3, 32, "scope"),
            GroupKey = RequireLength(PayloadReader.GetString(item, "groupKey"), 0, 128, "groupKey"),
            Value = RequireRange(PayloadReader.GetInt(item, "value"), -9999, 9999, "value")
        }).ToList();
    }

    private List<HoldingRef> ParseHoldingsList(IList<object>? list)
    {
        if (list == null) return new List<HoldingRef>();
        return list.OfType<Dictionary<string, object>>().Select(item => new HoldingRef
        {
            Name = RequireLength(PayloadReader.GetString(item, "name"), 1, 128, "name"),
            Description = RequireLength(PayloadReader.GetString(item, "description"), 0, 512, "description")
        }).ToList();
    }

    private Dictionary<string, object> BuildCharacterAggregatePayload(Character character, UserAccount viewer, bool includeNotesContext)
    {
        var owner = GetAccount(character.OwnerUserId);
        var payload = CharacterDetailsPayload(character, owner, viewer);
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
            _logger.Admin($"dice.roll.standard actor={actor.Login} requestId={roll.Id} total={roll.Result?.Total ?? 0}");
            return Ok("Standard dice roll created.", DiceRequestPayload(roll, actor));
        }
        catch (Exception ex)
        {
            _logger.Admin($"dice.roll.standard.fail actor={actor.Login} reason={ex.GetType().Name}:{ex.Message}");
            throw;
        }
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
                _logger.Admin($"dice.roll.test actor={actor.Login} action=create requestId={roll.Id} total={roll.Result?.Total ?? 0}");
                return Ok("Test dice roll created.", DiceRequestPayload(roll, actor));
            }

            existing.RawFormula = roll.RawFormula;
            existing.Formula = roll.Formula;
            existing.Visibility = roll.Visibility;
            existing.Description = roll.Description;
            existing.Result = roll.Result;
            existing.Status = RequestStatus.Approved;
            existing.History.Add(new RequestHistoryEntry { ActorUserId = actor.Id, Action = "TestReplaced", Comment = roll.Formula.Normalized });
            _repositories.DiceRequests.Replace(existing);
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

        var description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 1024, "description");
        var formulaInput = RequireLength(PayloadReader.GetString(context.Request.Payload, "formula"), 3, 64, "formula");
        var visibilityRaw = (PayloadReader.GetString(context.Request.Payload, "visibility") ?? RequestVisibility.Public.ToString());
        if (!Enum.TryParse(visibilityRaw, true, out RequestVisibility visibility)) visibility = RequestVisibility.Public;
        var formula = DiceFormulaParser.Parse(formulaInput);
        var result = DiceRollExecutor.Execute(formula, visibility, actor.Id);
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
        return request;
    }

    public ResponseEnvelope RequestCancel(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var requestId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requestId"), 8, 128, "requestId");

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
        return Ok("My requests loaded.", new Dictionary<string, object> { { "items", actions.Concat(dice).ToArray() } });
    }

    public ResponseEnvelope RequestListPending(CommandContext context)
    {
        RequireAdmin(context);
        var actions = _repositories.ActionRequests.Find(Builders<ActionRequest>.Filter.Eq(x => x.Status, RequestStatus.Pending)).Select(RequestPayload).Cast<object>();
        var dice = _repositories.DiceRequests.Find(
                Builders<DiceRollRequest>.Filter.Eq(x => x.Status, RequestStatus.Pending) &
                Builders<DiceRollRequest>.Filter.Eq(x => x.IsTestRoll, false))
            .Select(x => (object)DiceRequestPayload(x, GetCurrentAccount(context))).Cast<object>();
        return Ok("Pending requests loaded.", new Dictionary<string, object> { { "items", actions.Concat(dice).ToArray() } });
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

        var dice = _repositories.DiceRequests.GetById(requestId) ?? throw new KeyNotFoundException("Request not found.");
        if (dice.Status != RequestStatus.Pending) throw new InvalidOperationException("Request is not pending.");
        dice.Status = RequestStatus.Approved;
        dice.Result = DiceRollExecutor.Execute(dice.Formula, dice.Visibility, actor.Id);
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
        payload.AddRange(dice.Where(x => !x.IsTestRoll && (includeAll || CanViewDice(actor, x))).Select(x => (object)DiceRequestPayload(x, actor)));
        return Ok("Request history loaded.", new Dictionary<string, object> { { "items", payload.ToArray() } });
    }

    public ResponseEnvelope DiceHistory(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var items = _repositories.DiceRequests.Find(FilterDefinition<DiceRollRequest>.Empty)
            .Where(x => !x.IsTestRoll && CanViewDice(actor, x))
            .Select(x => (object)DiceRequestPayload(x, actor)).ToArray();
        _logger.Admin($"dice.history.get actor={actor.Login} count={items.Length}");
        return Ok("Dice history loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope DiceVisibleFeed(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var items = _repositories.DiceRequests.Find(Builders<DiceRollRequest>.Filter.Eq(x => x.Status, RequestStatus.Approved))
            .Where(x => !x.IsTestRoll && CanViewDice(actor, x))
            .OrderByDescending(x => x.UpdatedUtc)
            .Take(100)
            .Select(x => (object)DiceRequestPayload(x, actor)).ToArray();
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
        if (actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin)) return true;
        if (request.Visibility == RequestVisibility.Public) return true;
        if (request.Visibility == RequestVisibility.PlayerShadow) return request.CreatorUserId == actor.Id;
        return false;
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
        var basePayload = new Dictionary<string, object>
        {
            { "requestId", request.Id },
            { "requestType", request.RequestType },
            { "creatorUserId", request.CreatorUserId },
            { "characterId", request.CharacterId ?? string.Empty },
            { "status", request.Status.ToString() },
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
            basePayload["result"] = new Dictionary<string, object>
            {
                { "normalizedFormula", request.Result.NormalizedFormula },
                { "rolls", request.Result.Rolls.Cast<object>().ToArray() },
                { "modifier", request.Result.Modifier },
                { "total", request.Result.Total },
                { "visibility", request.Result.Visibility.ToString() },
                { "approvedBy", request.Result.ApprovedByUserId },
                { "approvedAt", request.Result.ApprovedAtUtc }
            };
        }

        return basePayload;
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

public sealed class DelegateCommandHandler : ICommandHandler
{
    private readonly Func<CommandContext, ResponseEnvelope> _handler;
    public DelegateCommandHandler(Func<CommandContext, ResponseEnvelope> handler) { _handler = handler; }
    public ResponseEnvelope Handle(CommandContext context) => _handler(context);
}
