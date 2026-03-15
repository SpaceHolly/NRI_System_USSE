using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Security;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public class ServiceHub
{
    private readonly INriRepositoryFactory _repositories;
    private readonly SessionManager _sessionManager;
    private readonly IServerLogger _logger;

    public ServiceHub(INriRepositoryFactory repositories, SessionManager sessionManager, IServerLogger logger)
    {
        _repositories = repositories;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public ResponseEnvelope Register(CommandContext context)
    {
        var login = RequireLength(PayloadReader.GetString(context.Request.Payload, "login"), 3, 64, "login");
        var password = RequireLength(PayloadReader.GetString(context.Request.Payload, "password"), 6, 128, "password");

        var existing = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, login)).FirstOrDefault();
        if (existing != null) throw new InvalidOperationException("Login already exists.");
        var login = PayloadReader.GetString(context.Request.Payload, "login");
        var password = PayloadReader.GetString(context.Request.Payload, "password");
        if (string.IsNullOrWhiteSpace(login) || login.Length < 3 || string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ArgumentException("Invalid login/password.");
        }

        var existing = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, login)).FirstOrDefault();
        if (existing != null)
        {
            throw new InvalidOperationException("Login already exists.");
        }

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
            Roles = new List<UserRole> { UserRole.Player },
            Status = AccountStatus.PendingApproval
        };
        _repositories.Accounts.Insert(account);

        profile.UserAccountId = account.Id;
        _repositories.Profiles.Replace(profile);

        WriteAudit("auth", account.Id, "register", account.Id);
        return Ok("Registration completed.", new Dictionary<string, object> { { "accountId", account.Id }, { "status", account.Status.ToString() } });
        _logger.Session($"Registered account {login}");

        return Ok("Registration completed.", new Dictionary<string, object>
        {
            { "accountId", account.Id },
            { "status", account.Status.ToString() }
        });
    }

    public ResponseEnvelope Login(CommandContext context)
    {
        var login = RequireLength(PayloadReader.GetString(context.Request.Payload, "login"), 3, 64, "login");
        var password = RequireLength(PayloadReader.GetString(context.Request.Payload, "password"), 6, 128, "password");

        var account = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, login)).FirstOrDefault();
        if (account == null || PasswordHasher.Hash(password, account.PasswordSalt) != account.PasswordHash)
            throw new UnauthorizedAccessException("Invalid credentials.");
        if (account.Status == AccountStatus.Blocked || account.Status == AccountStatus.Archived)
            throw new UnauthorizedAccessException($"Account status '{account.Status}' disallows login.");
        var login = PayloadReader.GetString(context.Request.Payload, "login");
        var password = PayloadReader.GetString(context.Request.Payload, "password");
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Login/password are required.");
        }

        var account = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, login)).FirstOrDefault();
        if (account == null)
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var hash = PasswordHasher.Hash(password, account.PasswordSalt);
        if (!string.Equals(hash, account.PasswordHash, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (account.Status == AccountStatus.Blocked || account.Status == AccountStatus.Archived)
        {
            throw new UnauthorizedAccessException($"Account status '{account.Status}' disallows login.");
        }

        account.LastLoginUtc = DateTime.UtcNow;
        _repositories.Accounts.Replace(account);

        var token = _sessionManager.CreateSession(account.Id, context.ConnectionId);
        WriteAudit("auth", account.Id, "login", account.Id);
        _logger.Session($"Login success for {account.Login}");
        WriteAudit("auth", account.Id, "login", account.Id);

        return Ok("Login success.", new Dictionary<string, object>
        {
            { "authToken", token },
            { "accountId", account.Id },
            { "status", account.Status.ToString() },
            { "roles", account.Roles.Select(x => x.ToString()).ToArray() }
        });
    }

    public ResponseEnvelope Logout(CommandContext context)
    {
        _sessionManager.Logout(context.Request.AuthToken);
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
            { "status", account.Status.ToString() }
        });
    }

    public ResponseEnvelope ProfileGet(CommandContext context)
    {
        var account = GetCurrentAccount(context);
        return Ok("Profile loaded.", ProfilePayload(GetProfile(account.ProfileId)));
        var profile = GetProfile(account.ProfileId);
        return Ok("Profile loaded.", ProfilePayload(profile));
    }

    public ResponseEnvelope ProfileUpdate(CommandContext context)
    {
        var account = GetCurrentAccount(context);
        if (account.Status == AccountStatus.Blocked || account.Status == AccountStatus.Archived)
            throw new UnauthorizedAccessException("Account is not allowed to update profile.");
        {
            throw new UnauthorizedAccessException("Account is not allowed to update profile.");
        }

        var profile = GetProfile(account.ProfileId);
        profile.DisplayName = RequireLength(PayloadReader.GetString(context.Request.Payload, "displayName"), 2, 64, "displayName");
        profile.Race = RequireLength(PayloadReader.GetString(context.Request.Payload, "race"), 2, 64, "race");
        profile.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
        profile.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
        var age = PayloadReader.GetInt(context.Request.Payload, "age");
        if (age.HasValue && (age.Value < 1 || age.Value > 1000)) throw new ArgumentException("age must be in range 1..1000");
        if (age.HasValue && (age.Value < 1 || age.Value > 1000))
        {
            throw new ArgumentException("age must be in range 1..1000");
        }

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
        return Ok("Pending accounts loaded.", new Dictionary<string, object> { { "items", items } });
        var account = GetCurrentAccount(context);
        RoleGuard.EnsureRole(account, UserRole.Admin, UserRole.SuperAdmin);

        var pending = _repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Status, AccountStatus.PendingApproval));
        var list = pending.Select(x => new Dictionary<string, object>
        {
            { "accountId", x.Id },
            { "login", x.Login },
            { "status", x.Status.ToString() },
            { "createdUtc", x.CreatedUtc }
        }).Cast<object>().ToArray();

        return Ok("Pending accounts loaded.", new Dictionary<string, object> { { "items", list } });
    }

    public ResponseEnvelope AdminApproveAccount(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var target = GetAccount(RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId"));
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var accountId = RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId");
        var target = GetAccount(accountId);
        target.Status = AccountStatus.Active;
        _repositories.Accounts.Replace(target);
        _logger.Admin($"Account approved {target.Login} by {actor.Login}");
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
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var accountId = RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId");
        var target = GetAccount(accountId);
        target.Status = AccountStatus.Archived;
        target.Archived = true;
        _repositories.Accounts.Replace(target);
        _logger.Admin($"Account archived {target.Login} by {actor.Login}");
        WriteAudit("admin", actor.Id, "archiveAccount", target.Id);
        return Ok("Account archived.");
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
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var accountId = RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId");
        var target = GetAccount(accountId);
        var profile = GetProfile(target.ProfileId);
        return Ok("Account profile loaded.", ProfilePayload(profile));
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
        var actor = RequireAdmin(context);
        var ownerId = RequireLength(PayloadReader.GetString(context.Request.Payload, "ownerUserId"), 8, 128, "ownerUserId");
        var character = new Character
        {
            OwnerUserId = ownerId,
            Name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name"),
            Race = PayloadReader.GetString(context.Request.Payload, "race") ?? string.Empty,
            Height = PayloadReader.GetString(context.Request.Payload, "height") ?? string.Empty,
            Age = PayloadReader.GetInt(context.Request.Payload, "age")
        };
        character.Wallet.EnsureAllDenominations();
        _repositories.Characters.Insert(character);
        WriteAudit("character", actor.Id, "create", character.Id);
        return Ok("Character created.", CharacterDetailsPayload(character, GetAccount(ownerId), actor));
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
        var account = GetCurrentAccount(context);
        var items = _repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.OwnerUserId, account.Id))
            .Where(x => !x.Deleted)
            .Select(CharacterPayload)
            .Cast<object>()
            .ToArray();

        return Ok("Characters loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CharacterListByOwner(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var ownerId = RequireLength(PayloadReader.GetString(context.Request.Payload, "ownerUserId"), 8, 128, "ownerUserId");
        var items = _repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.OwnerUserId, ownerId))
            .Select(CharacterPayload)
            .Cast<object>()
            .ToArray();

        return Ok("Characters loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CharacterGetActive(CommandContext context)
    {
        var account = GetCurrentAccount(context);
        var presence = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, account.Id)).FirstOrDefault();
        if (presence == null || string.IsNullOrWhiteSpace(presence.ActiveCharacterId))
        {
            return Ok("No active character.");
        }

        var character = _repositories.Characters.GetById(presence.ActiveCharacterId);
        if (character == null)
        {
            return Ok("No active character.");
        }

        return Ok("Active character loaded.", CharacterPayload(character));
    }

    public ResponseEnvelope CharacterCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var ownerId = RequireLength(PayloadReader.GetString(context.Request.Payload, "ownerUserId"), 8, 128, "ownerUserId");
        var name = RequireLength(PayloadReader.GetString(context.Request.Payload, "name"), 2, 80, "name");

        GetAccount(ownerId);
        var character = new Character
        {
            OwnerUserId = ownerId,
            Name = name,
            SchemaVersion = 1
        };

        _repositories.Characters.Insert(character);
        _logger.Admin($"Character created {character.Name} by {actor.Login}");
        WriteAudit("character", actor.Id, "create", character.Id);
        return Ok("Character created.", CharacterPayload(character));
    }

    public ResponseEnvelope CharacterArchive(CommandContext context)
    {
        return SetCharacterArchiveState(context, true);
    }

    public ResponseEnvelope CharacterRestore(CommandContext context)
    {
        return SetCharacterArchiveState(context, false);
    }

    public ResponseEnvelope CharacterTransfer(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var targetUserId = RequireLength(PayloadReader.GetString(context.Request.Payload, "targetUserId"), 8, 128, "targetUserId");

        GetAccount(targetUserId);
        var character = GetCharacter(characterId);
        character.OwnerUserId = targetUserId;
        _repositories.Characters.Replace(character);
        WriteAudit("character", actor.Id, "transfer", character.Id);
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
            if (value.HasValue && value.Value >= 0) c.Wallet.Balance.Amounts[d] = value.Value;
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
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var userId = RequireLength(PayloadReader.GetString(context.Request.Payload, "userId"), 8, 128, "userId");
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");

        var character = GetCharacter(characterId);
        if (character.OwnerUserId != userId)
        {
            throw new InvalidOperationException("Character does not belong to specified user.");
        }

        var presence = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, userId)).FirstOrDefault();
        if (presence == null)
        {
            presence = new SessionUserState { UserId = userId, IsOnline = false };
            _repositories.Presence.Insert(presence);
        }

        presence.ActiveCharacterId = characterId;
        _repositories.Presence.Replace(presence);
        WriteAudit("character", actor.Id, "assignActive", character.Id);
        return Ok("Active character assigned.");
    }

    public ResponseEnvelope PresenceList(CommandContext context)
    {
        RequireAdmin(context);
        var items = _repositories.Presence.Find(FilterDefinition<SessionUserState>.Empty)
            .Select(x => new Dictionary<string, object> { { "userId", x.UserId }, { "isOnline", x.IsOnline }, { "lastSeenUtc", x.LastSeenUtc }, { "activeCharacterId", x.ActiveCharacterId ?? string.Empty } })
            .Cast<object>().ToArray();
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var items = _repositories.Presence.Find(FilterDefinition<SessionUserState>.Empty)
            .Select(x => new Dictionary<string, object>
            {
                { "userId", x.UserId },
                { "isOnline", x.IsOnline },
                { "lastSeenUtc", x.LastSeenUtc },
                { "activeCharacterId", x.ActiveCharacterId ?? string.Empty }
            })
            .Cast<object>()
            .ToArray();

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
        return Ok(existing == null ? "Lock acquired." : "Lock refreshed.", LockPayload(lockItem));
    }

    public ResponseEnvelope LockRelease(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var lockItem = RequireLockByEntity(context);
        if (lockItem.LockedByUserId != actor.Id && !actor.Roles.Contains(UserRole.SuperAdmin)) throw new UnauthorizedAccessException("Cannot release lock owned by another admin.");
        lockItem.Deleted = true; lockItem.Archived = true;
        _repositories.Locks.Replace(lockItem);
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
        return Ok("Lock force released.");
    }

    public ResponseEnvelope LockStatus(CommandContext context)
    {
        RequireAdmin(context);
        var entityType = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityType"), 2, 128, "entityType");
        var entityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityId"), 4, 128, "entityId");
        var lockItem = FindActiveLock(entityType, entityId);
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
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var entityType = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityType"), 2, 128, "entityType");
        var entityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityId"), 4, 128, "entityId");

        var existing = _repositories.Locks.Find(Builders<EntityLock>.Filter.Eq(x => x.EntityType, entityType) & Builders<EntityLock>.Filter.Eq(x => x.EntityId, entityId)).FirstOrDefault();
        var now = DateTime.UtcNow;
        if (existing != null)
        {
            if (existing.ExpiresUtc <= now)
            {
                existing.Deleted = true;
                _repositories.Locks.Replace(existing);
            }
            else if (existing.LockedByUserId != actor.Id)
            {
                throw new InvalidOperationException("Entity is already locked.");
            }
            else
            {
                existing.ExpiresUtc = now.AddHours(1);
                _repositories.Locks.Replace(existing);
                return Ok("Lock refreshed.", LockPayload(existing));
            }
        }

        var lockItem = new EntityLock
        {
            EntityType = entityType,
            EntityId = entityId,
            LockedByUserId = actor.Id,
            OwnerLevel = actor.Roles.Contains(UserRole.SuperAdmin) ? LockOwnerLevel.SuperAdmin : LockOwnerLevel.Admin,
            IssuedUtc = now,
            ExpiresUtc = now.AddHours(1)
        };

        _repositories.Locks.Insert(lockItem);
        return Ok("Lock acquired.", LockPayload(lockItem));
    }

    public ResponseEnvelope LockRelease(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var entityType = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityType"), 2, 128, "entityType");
        var entityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityId"), 4, 128, "entityId");

        var existing = _repositories.Locks.Find(Builders<EntityLock>.Filter.Eq(x => x.EntityType, entityType) & Builders<EntityLock>.Filter.Eq(x => x.EntityId, entityId)).FirstOrDefault();
        if (existing == null)
        {
            return Ok("Lock not found.");
        }

        if (existing.LockedByUserId != actor.Id && !actor.Roles.Contains(UserRole.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Cannot release lock owned by another admin.");
        }

        existing.Deleted = true;
        existing.Archived = true;
        _repositories.Locks.Replace(existing);
        return Ok("Lock released.");
    }

    public ResponseEnvelope LockStatus(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var entityType = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityType"), 2, 128, "entityType");
        var entityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityId"), 4, 128, "entityId");

        var existing = _repositories.Locks.Find(Builders<EntityLock>.Filter.Eq(x => x.EntityType, entityType) & Builders<EntityLock>.Filter.Eq(x => x.EntityId, entityId) & Builders<EntityLock>.Filter.Eq(x => x.Deleted, false)).FirstOrDefault();
        if (existing == null || existing.ExpiresUtc <= DateTime.UtcNow)
        {
            return Ok("Lock is free.", new Dictionary<string, object> { { "isLocked", false } });
        }

        return Ok("Lock is active.", new Dictionary<string, object>
        {
            { "isLocked", true },
            { "lock", LockPayload(existing) }
        });
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
        var isPrivileged = viewer.Id == owner.Id || viewer.Roles.Contains(UserRole.Admin) || viewer.Roles.Contains(UserRole.SuperAdmin);
        var details = CharacterSummaryPayload(c, owner, viewer);
        details["age"] = c.Age.HasValue ? (object)c.Age.Value : string.Empty;
        details["backstory"] = (!isPrivileged && c.Visibility.HideBackstoryForOthers) ? "[hidden]" : c.Backstory;
        details["stats"] = (!isPrivileged && c.Visibility.HideStatsForOthers) ? "[hidden]" : (object)StatsPayload(c.Stats);
        details["money"] = WalletPayload(c.Wallet);
        details["inventory"] = c.Inventory.Select(InventoryPayload).Cast<object>().ToArray();
        details["companions"] = c.Companions.Select(CompanionPayload).Cast<object>().ToArray();
        details["holdings"] = c.Holdings.Select(x => new Dictionary<string, object> { { "name", x.Name }, { "description", x.Description } }).Cast<object>().ToArray();
        details["reputation"] = (!isPrivileged && c.Visibility.HideReputationForOthers) ? "[hidden]" : (object)c.Reputation.Select(x => new Dictionary<string, object> { { "scope", x.Scope }, { "groupKey", x.GroupKey }, { "value", x.Value } }).Cast<object>().ToArray();
        details["classProgress"] = c.ClassProgress.Select(x => new Dictionary<string, object> { { "classCode", x.ClassCode }, { "level", x.Level }, { "experience", x.Experience } }).Cast<object>().ToArray();
        details["skills"] = c.Skills.Select(x => new Dictionary<string, object> { { "skillCode", x.SkillCode }, { "name", x.Name }, { "description", x.Description }, { "type", x.Type.ToString() }, { "available", x.IsAvailable }, { "reason", x.UnavailableReason } }).Cast<object>().ToArray();
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
            .ToDictionary(k => k.ToString(), k => (object)(w.Balance.Amounts.ContainsKey(k) ? w.Balance.Amounts[k] : 0L));
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
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var character = GetCharacter(characterId);
        character.Archived = archive;
        character.Deleted = archive;
        _repositories.Characters.Replace(character);
        WriteAudit("character", actor.Id, archive ? "archive" : "restore", character.Id);
        return Ok(archive ? "Character archived." : "Character restored.");
    }

    private UserAccount GetCurrentAccount(CommandContext context)
    {
        if (context.Session == null)
        {
            throw new UnauthorizedAccessException("Session is required.");
        }

        return GetAccount(context.Session.UserId);
    }

    private UserAccount GetAccount(string id)
    {
        var account = _repositories.Accounts.GetById(id);
        if (account == null)
        {
            throw new KeyNotFoundException("Account not found.");
        }

        return account;
    }

    private Character GetCharacter(string id)
    {
        var character = _repositories.Characters.GetById(id);
        if (character == null)
        {
            throw new KeyNotFoundException("Character not found.");
        }

        return character;
    }

    private UserProfile GetProfile(string profileId)
    {
        var profile = _repositories.Profiles.GetById(profileId);
        if (profile == null)
        {
            throw new KeyNotFoundException("Profile not found.");
        }

        return profile;
    }

    private void WriteAudit(string category, string actorUserId, string action, string target)
    {
        _repositories.AuditLogs.Insert(new AuditLogEntry
        {
            Category = category,
            ActorUserId = actorUserId,
            Action = action,
            Target = target
        });

        _logger.Audit($"{category}:{action} actor={actorUserId} target={target}");
    }

    private static ResponseEnvelope Ok(string message, Dictionary<string, object>? payload = null)
    {
        return new ResponseEnvelope
        {
            Status = ResponseStatus.Ok,
            Message = message,
            Payload = payload ?? new Dictionary<string, object>()
        };
    }

    private static Dictionary<string, object> ProfilePayload(UserProfile profile)
    {
        return new Dictionary<string, object>
        {
            { "profileId", profile.Id },
            { "displayName", profile.DisplayName },
            { "race", profile.Race },
            { "age", profile.Age.HasValue ? (object)profile.Age.Value : string.Empty },
            { "description", profile.Description },
            { "backstory", profile.Backstory }
        };
    }

    private static Dictionary<string, object> CharacterPayload(Character character)
    {
        return new Dictionary<string, object>
        {
            { "characterId", character.Id },
            { "ownerUserId", character.OwnerUserId },
            { "name", character.Name },
            { "archived", character.Archived },
            { "schemaVersion", character.SchemaVersion }
        };
    }

    private static Dictionary<string, object> LockPayload(EntityLock lockItem)
    {
        return new Dictionary<string, object>
        {
            { "entityType", lockItem.EntityType },
            { "entityId", lockItem.EntityId },
            { "lockedByUserId", lockItem.LockedByUserId },
            { "ownerLevel", lockItem.OwnerLevel.ToString() },
            { "issuedUtc", lockItem.IssuedUtc },
            { "expiresUtc", lockItem.ExpiresUtc }
        };
    }

    private static string RequireLength(string? value, int min, int max, string field)
    {
        var actual = value ?? string.Empty;
        if (actual.Length < min || actual.Length > max)
        {
            throw new ArgumentException($"{field} length must be between {min} and {max}");
        }

        return actual;
    }
}

public sealed class DelegateCommandHandler : ICommandHandler
{
    private readonly Func<CommandContext, ResponseEnvelope> _handler;

    public DelegateCommandHandler(Func<CommandContext, ResponseEnvelope> handler)
    {
        _handler = handler;
    }

    public ResponseEnvelope Handle(CommandContext context)
    {
        return _handler(context);
    }
using Nri.Shared.Contracts;

namespace Nri.Server.Application;

public interface IStubService
{
    ResponseEnvelope Handle(RequestEnvelope request);
}

public class AccountService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "AccountService");
}

public class CharacterService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "CharacterService");
}

public class RequestService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "RequestService");
}

public class CombatService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "CombatService");
}

public class ChatService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "ChatService");
}

public class AudioService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "AudioService");
}

public static class StubResponses
{
    public static ResponseEnvelope NotImplemented(RequestEnvelope request, string source)
    {
        return new ResponseEnvelope
        {
            RequestId = request.RequestId,
            Status = ResponseStatus.Error,
            ErrorCode = ErrorCode.Unknown,
            Message = $"{source} is a stub and will be implemented in next stages."
        };
    }
}
