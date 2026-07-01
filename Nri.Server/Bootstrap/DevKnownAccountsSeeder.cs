using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Application;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Bootstrap;

public static class DevKnownAccountsSeeder
{
    private static KnownAccount[] KnownAccounts => new[]
    {
        new KnownAccount("dev_superadmin", "Dev SuperAdmin", EnvOrDefault("NRI_DEV_SUPERADMIN_PASSWORD", "DevSuper_01459!"), new[] { UserRole.SuperAdmin, UserRole.Admin }),
        new KnownAccount("dev_admin", "Dev Admin", EnvOrDefault("NRI_DEV_ADMIN_PASSWORD", "dev_admin_01434"), new[] { UserRole.Admin }),
        new KnownAccount("dev_player", "Dev Player", EnvOrDefault("NRI_DEV_PLAYER_PASSWORD", "dev_player_01434"), new[] { UserRole.Player }),
        new KnownAccount("dev_player_alt", "Dev Player Alt", EnvOrDefault("NRI_DEV_PLAYER_ALT_PASSWORD", "DevPlayerAlt_01459!"), new[] { UserRole.Player })
    };

    public static string Run(string configPath)
    {
        var config = ServerConfigProvider.Load(configPath);
        if (!IsDevelopmentOrTest(config.Environment))
            throw new InvalidOperationException("Known dev account reset is disabled outside Development/Test.");

        var logger = new CompositeLogger(config.Logging);
        var mongo = new MongoContext(config, logger);
        var repositories = new MongoRepositoryFactory(mongo, logger);
        var admin = EnsureAccount(repositories, KnownAccounts[0]);
        UserAccount? devPlayer = null;
        foreach (var known in KnownAccounts)
        {
            var account = EnsureAccount(repositories, known);
            if (known.Login == "dev_player") devPlayer = account;
        }

        if (devPlayer != null)
        {
            EnsureDevPlayerCharacter(repositories, admin, devPlayer);
        }

        repositories.AuditLogs.Insert(new AuditLogEntry
        {
            Category = "dev_access",
            ActorUserId = admin.Id,
            Action = "dev_access.known_accounts_reset",
            Target = "known-dev-accounts",
            DetailsJson = "Foundation 0.14.59 guarded dev reset"
        });

        logger.Admin("Dev known accounts reset completed.");
        return "Dev known accounts reset completed: " + string.Join(", ", KnownAccounts.Select(x => x.Login));
    }

    public static IReadOnlyCollection<object> CredentialsPayload()
        => KnownAccounts.Select(x => (object)new Dictionary<string, object>
        {
            ["login"] = x.Login,
            ["password"] = x.Password,
            ["roles"] = x.Roles.Select(r => r.ToString()).ToArray()
        }).ToArray();

    private static UserAccount EnsureAccount(INriRepositoryFactory repositories, KnownAccount known)
    {
        var account = repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, known.Login)).FirstOrDefault();
        var salt = PasswordHasher.CreateSalt();
        if (account == null)
        {
            var profile = new UserProfile { DisplayName = known.DisplayName, TimeZoneId = "Europe/Moscow" };
            repositories.Profiles.Insert(profile);
            account = new UserAccount
            {
                Login = known.Login,
                PasswordSalt = salt,
                PasswordHash = PasswordHasher.Hash(known.Password, salt),
                ProfileId = profile.Id,
                Roles = known.Roles.Distinct().ToList(),
                Status = AccountStatus.Active
            };
            repositories.Accounts.Insert(account);
            profile.UserAccountId = account.Id;
            repositories.Profiles.Replace(profile);
            return account;
        }

        account.PasswordSalt = salt;
        account.PasswordHash = PasswordHasher.Hash(known.Password, salt);
        account.Roles = known.Roles.Distinct().ToList();
        account.Status = AccountStatus.Active;
        account.Archived = false;
        account.Deleted = false;
        account.UpdatedUtc = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(account.ProfileId) || repositories.Profiles.GetById(account.ProfileId) == null)
        {
            var profile = new UserProfile { UserAccountId = account.Id, DisplayName = known.DisplayName, TimeZoneId = "Europe/Moscow" };
            repositories.Profiles.Insert(profile);
            account.ProfileId = profile.Id;
        }

        repositories.Accounts.Replace(account);
        return account;
    }

    private static void EnsureDevPlayerCharacter(INriRepositoryFactory repositories, UserAccount admin, UserAccount player)
    {
        var character = repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.OwnerUserId, player.Id) & Builders<Character>.Filter.Eq(x => x.Name, "Dev Player 0.14.59 Character")).FirstOrDefault();
        var created = false;
        if (character == null)
        {
            character = new Character();
            created = true;
        }

        character.SessionId = "dev-session-01459";
        character.OwnerUserId = player.Id;
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
        if (created) repositories.Characters.Insert(character); else repositories.Characters.Replace(character);

        var ownership = repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault() ?? new CharacterOwnershipState();
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
        ownership.AssignedByUserId = admin.Id;
        ownership.UpdatedAtUtc = DateTime.UtcNow;
        ownership.UpdatedByUserId = admin.Id;
        if (ownershipCreated) repositories.CharacterOwnerships.Insert(ownership); else repositories.CharacterOwnerships.Replace(ownership);

        var presence = repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, player.Id)).FirstOrDefault();
        var presenceCreated = false;
        if (presence == null)
        {
            presence = new SessionUserState { UserId = player.Id };
            presenceCreated = true;
        }
        presence.CurrentGameSessionId = "dev-session-01459";
        presence.ActiveCharacterId = character.Id;
        presence.LastSeenUtc = DateTime.UtcNow;
        if (presenceCreated) repositories.Presence.Insert(presence); else repositories.Presence.Replace(presence);
    }

    private static bool IsDevelopmentOrTest(string? environment)
        => string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
           || string.Equals(environment, "Test", StringComparison.OrdinalIgnoreCase)
           || string.Equals(environment, "Local", StringComparison.OrdinalIgnoreCase);

    private static string EnvOrDefault(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private sealed class KnownAccount
    {
        public KnownAccount(string login, string displayName, string password, IReadOnlyCollection<UserRole> roles)
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
}
