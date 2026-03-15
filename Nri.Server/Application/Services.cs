using System;
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
            Roles = new List<UserRole> { UserRole.Player },
            Status = AccountStatus.PendingApproval
        };
        _repositories.Accounts.Insert(account);

        profile.UserAccountId = account.Id;
        _repositories.Profiles.Replace(profile);

        WriteAudit("auth", account.Id, "register", account.Id);
        _logger.Session($"Registered account {login}");

        return Ok("Registration completed.", new Dictionary<string, object>
        {
            { "accountId", account.Id },
            { "status", account.Status.ToString() }
        });
    }

    public ResponseEnvelope Login(CommandContext context)
    {
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
            { "status", account.Status.ToString() }
        });
    }

    public ResponseEnvelope ProfileGet(CommandContext context)
    {
        var account = GetCurrentAccount(context);
        var profile = GetProfile(account.ProfileId);
        return Ok("Profile loaded.", ProfilePayload(profile));
    }

    public ResponseEnvelope ProfileUpdate(CommandContext context)
    {
        var account = GetCurrentAccount(context);
        if (account.Status == AccountStatus.Blocked || account.Status == AccountStatus.Archived)
        {
            throw new UnauthorizedAccessException("Account is not allowed to update profile.");
        }

        var profile = GetProfile(account.ProfileId);
        profile.DisplayName = RequireLength(PayloadReader.GetString(context.Request.Payload, "displayName"), 2, 64, "displayName");
        profile.Race = RequireLength(PayloadReader.GetString(context.Request.Payload, "race"), 2, 64, "race");
        profile.Description = RequireLength(PayloadReader.GetString(context.Request.Payload, "description"), 0, 2048, "description");
        profile.Backstory = RequireLength(PayloadReader.GetString(context.Request.Payload, "backstory"), 0, 4096, "backstory");
        var age = PayloadReader.GetInt(context.Request.Payload, "age");
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
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);

        var accountId = RequireLength(PayloadReader.GetString(context.Request.Payload, "accountId"), 8, 128, "accountId");
        var target = GetAccount(accountId);
        var profile = GetProfile(target.ProfileId);
        return Ok("Account profile loaded.", ProfilePayload(profile));
    }

    public ResponseEnvelope CharacterListMine(CommandContext context)
    {
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
}
