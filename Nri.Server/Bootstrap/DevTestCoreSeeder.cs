using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Application;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Bootstrap;

public static class DevTestCoreSeeder
{
    private const string EnableEnv = "NRI_DEV_BOOTSTRAP_TEST_ACCOUNTS";
    private const string AdminPasswordEnv = "NRI_DEV_ADMIN_PASSWORD";
    private const string PlayerPasswordEnv = "NRI_DEV_PLAYER_PASSWORD";
    private const string DevAdminLogin = "dev_admin";
    private const string DevPlayerLogin = "dev_player";
    private const string DevCharacterName = "Dev Test Character";
    private const string DevCampaignId = "dev-campaign-core";
    private const string DevSessionId = "dev-session-core";

    public static string Run(string configPath)
    {
        if (!IsTruthy(Environment.GetEnvironmentVariable(EnableEnv)))
        {
            throw new InvalidOperationException($"{EnableEnv}=true is required for dev/test account seeding.");
        }

        var adminPassword = RequireSecret(AdminPasswordEnv);
        var playerPassword = RequireSecret(PlayerPasswordEnv);
        var config = ServerConfigProvider.Load(configPath);
        var logger = new CompositeLogger(config.Logging);
        var mongo = new MongoContext(config, logger);
        var repositories = new MongoRepositoryFactory(mongo, logger);

        var admin = EnsureAccount(
            repositories,
            DevAdminLogin,
            "Dev Admin",
            adminPassword,
            new[] { UserRole.Admin, UserRole.SuperAdmin });
        var player = EnsureAccount(
            repositories,
            DevPlayerLogin,
            "Dev Player",
            playerPassword,
            new[] { UserRole.Player });
        var character = EnsurePlayerCharacter(repositories, player);
        EnsureOwnership(repositories, admin, player, character);
        EnsurePresence(repositories, player, character);

        repositories.AuditLogs.Insert(new AuditLogEntry
        {
            Category = "dev-test-core-seed",
            ActorUserId = admin.Id,
            Action = "ensure",
            Target = character.Id,
            DetailsJson = "dev_admin/dev_player core rescue seed"
        });

        logger.Admin($"Dev/test core seed completed admin={admin.Login} player={player.Login} characterId={character.Id}.");
        return $"Dev/test core seed completed: admin={admin.Login}; player={player.Login}; characterId={character.Id}";
    }

    private static UserAccount EnsureAccount(INriRepositoryFactory repositories, string login, string displayName, string password, IReadOnlyCollection<UserRole> roles)
    {
        var account = repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, login)).FirstOrDefault();
        var salt = PasswordHasher.CreateSalt();
        if (account == null)
        {
            var profile = new UserProfile { DisplayName = displayName };
            repositories.Profiles.Insert(profile);
            account = new UserAccount
            {
                Login = login,
                PasswordSalt = salt,
                PasswordHash = PasswordHasher.Hash(password, salt),
                ProfileId = profile.Id,
                Status = AccountStatus.Active,
                Roles = roles.Distinct().ToList()
            };
            repositories.Accounts.Insert(account);
            profile.UserAccountId = account.Id;
            repositories.Profiles.Replace(profile);
            return account;
        }

        account.PasswordSalt = salt;
        account.PasswordHash = PasswordHasher.Hash(password, salt);
        account.Status = AccountStatus.Active;
        account.Archived = false;
        account.Deleted = false;
        account.Roles = roles.Distinct().ToList();
        if (string.IsNullOrWhiteSpace(account.ProfileId) || repositories.Profiles.GetById(account.ProfileId) == null)
        {
            var profile = new UserProfile { UserAccountId = account.Id, DisplayName = displayName };
            repositories.Profiles.Insert(profile);
            account.ProfileId = profile.Id;
        }
        else
        {
            var profile = repositories.Profiles.GetById(account.ProfileId);
            if (profile != null)
            {
                profile.UserAccountId = account.Id;
                if (string.IsNullOrWhiteSpace(profile.DisplayName))
                {
                    profile.DisplayName = displayName;
                }

                repositories.Profiles.Replace(profile);
            }
        }

        repositories.Accounts.Replace(account);
        return account;
    }

    private static Character EnsurePlayerCharacter(INriRepositoryFactory repositories, UserAccount player)
    {
        var character = repositories.Characters.Find(
                Builders<Character>.Filter.Eq(x => x.OwnerUserId, player.Id) &
                Builders<Character>.Filter.Eq(x => x.Name, DevCharacterName))
            .FirstOrDefault();
        var created = false;
        if (character == null)
        {
            character = new Character();
            created = true;
        }

        character.SessionId = DevSessionId;
        character.OwnerUserId = player.Id;
        character.Name = DevCharacterName;
        character.Age = 28;
        character.Race = "Human";
        character.RaceCode = "human";
        character.Height = "175 cm";
        character.XpCoins = 7;
        character.Description = "Player-owned dev/test character for core verification.";
        character.Backstory = "Prepared by explicit local dev/test seed for rescue verification.";
        character.Visibility = new CharacterVisibilitySettings();
        character.Stats = new CharacterStats
        {
            Health = 12,
            PhysicalArmor = 2,
            MagicalArmor = 1,
            Morale = 5,
            Strength = 3,
            Dexterity = 3,
            Endurance = 3,
            Wisdom = 2,
            Intellect = 2,
            Charisma = 2
        };
        character.Wallet = character.Wallet ?? new Wallet();
        character.Wallet.EnsureAllDenominations();
        character.Wallet.Balance.Amounts["Gold"] = 5;
        character.Wallet.Balance.Amounts["Silver"] = 20;
        character.Inventory = new List<InventoryItem>
        {
            new InventoryItem
            {
                Name = "Verification Kit",
                Label = "Verification Kit",
                Category = "tool",
                Quantity = 1,
                Durability = 10,
                IsEquipped = false,
                Notes = "Seeded local dev/test item."
            }
        };

        if (created)
        {
            repositories.Characters.Insert(character);
        }
        else
        {
            repositories.Characters.Replace(character);
        }

        return character;
    }

    private static void EnsureOwnership(INriRepositoryFactory repositories, UserAccount admin, UserAccount player, Character character)
    {
        var ownership = repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault();
        var created = false;
        if (ownership == null)
        {
            ownership = new CharacterOwnershipState();
            created = true;
        }

        ownership.CampaignId = DevCampaignId;
        ownership.CharacterId = character.Id;
        ownership.CharacterDisplayName = character.Name;
        ownership.CharacterRole = CharacterOwnershipRoleIds.PlayerCharacter;
        ownership.OwnerUserId = player.Id;
        ownership.OwnerDisplayName = DevPlayerLogin;
        ownership.ControlledByUserId = player.Id;
        ownership.ControlledByDisplayName = DevPlayerLogin;
        ownership.IsPlayerVisible = true;
        ownership.VisibilityMode = MapVisibilityModes.Party;
        ownership.AssignmentStatus = CharacterOwnershipAssignmentStatusIds.Assigned;
        ownership.AssignedAtUtc = ownership.AssignedAtUtc ?? DateTime.UtcNow;
        ownership.AssignedByUserId = admin.Id;
        ownership.UpdatedAtUtc = DateTime.UtcNow;
        ownership.UpdatedByUserId = admin.Id;
        ownership.PublicNotes = "Seeded dev/test ownership for player verification.";

        if (created)
        {
            repositories.CharacterOwnerships.Insert(ownership);
        }
        else
        {
            repositories.CharacterOwnerships.Replace(ownership);
        }
    }

    private static void EnsurePresence(INriRepositoryFactory repositories, UserAccount player, Character character)
    {
        var presence = repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, player.Id)).FirstOrDefault();
        var created = false;
        if (presence == null)
        {
            presence = new SessionUserState { UserId = player.Id };
            created = true;
        }

        presence.CurrentGameSessionId = DevSessionId;
        presence.ActiveCharacterId = character.Id;
        presence.IsOnline = false;
        presence.LastSeenUtc = DateTime.UtcNow;

        if (created)
        {
            repositories.Presence.Insert(presence);
        }
        else
        {
            repositories.Presence.Replace(presence);
        }
    }

    private static string RequireSecret(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} must be set for dev/test account seeding.");
        }

        return value;
    }

    private static bool IsTruthy(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
