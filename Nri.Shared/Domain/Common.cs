using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public abstract class EntityBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int SchemaVersion { get; set; } = 1;
    public bool Deleted { get; set; }
    public bool Archived { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public enum UserRole
{
    Player,
    Observer,
    Admin,
    SuperAdmin
}

public enum AccountStatus
{
    PendingApproval,
    Active,
    Blocked,
    Archived
}

public class Role : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new List<string>();
}

public class UserAccount : EntityBase
{
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public List<UserRole> Roles { get; set; } = new List<UserRole> { UserRole.Player };
    public string ProfileId { get; set; } = string.Empty;
    public AccountStatus Status { get; set; } = AccountStatus.PendingApproval;
    public DateTime? LastLoginUtc { get; set; }
}

public class UserProfile : EntityBase
{
    public string UserAccountId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Backstory { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "UTC";
}

public class SessionUserState : EntityBase
{
    public string UserId { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public string? CurrentGameSessionId { get; set; }
    public string? ActiveCharacterId { get; set; }
}

public class World : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class Campaign : EntityBase
{
    public string WorldId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NarrativeSummary { get; set; } = string.Empty;
}

public class GameSession : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? PlannedStartUtc { get; set; }
    public bool IsActive { get; set; }
}

public class Character : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CharacterVisibilitySettings Visibility { get; set; } = new CharacterVisibilitySettings();
    public CharacterStats Stats { get; set; } = new CharacterStats();
    public List<CharacterClassProgress> ClassProgress { get; set; } = new List<CharacterClassProgress>();
    public List<SkillState> Skills { get; set; } = new List<SkillState>();
    public Wallet Wallet { get; set; } = new Wallet();
    public List<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
    public List<Companion> Companions { get; set; } = new List<Companion>();
}

public class Companion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
}

public class InventoryItem
{
    public string ItemCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public class Holding : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> OwnerCharacterIds { get; set; } = new List<string>();
}

public class ReputationEntry : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public string? CharacterId { get; set; }
    public string GroupKey { get; set; } = string.Empty;
    public int Value { get; set; }
}

public enum CurrencyDenomination
{
    Iron,
    Bronze,
    Silver,
    Gold,
    Platinum,
    Orichalcum,
    Adamant,
    Sovereign
}

public class MoneyBreakdown
{
    public Dictionary<CurrencyDenomination, long> Amounts { get; set; } = new Dictionary<CurrencyDenomination, long>();
}

public class Wallet
{
    public MoneyBreakdown Balance { get; set; } = new MoneyBreakdown();

    public void NormalizeUpward(IReadOnlyDictionary<CurrencyDenomination, int> factors)
    {
        foreach (var factor in factors)
        {
            if (!Balance.Amounts.ContainsKey(factor.Key))
            {
                Balance.Amounts[factor.Key] = 0;
            }
        }
    }
}

public class CharacterVisibilitySettings
{
    public bool HideExactStats { get; set; }
    public bool HideInventory { get; set; }
    public bool HidePrivateNotes { get; set; }
}

public class CharacterStats
{
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Willpower { get; set; }
    public int Endurance { get; set; }
}

public class CharacterClassProgress
{
    public string ClassCode { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Experience { get; set; }
}

public class SkillDefinition : EntityBase
{
    public string SkillCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class SkillState
{
    public string SkillCode { get; set; } = string.Empty;
    public int Rank { get; set; }
    public bool IsUnlocked { get; set; }
}

public enum ChatMessageType
{
    Public,
    HiddenToAdmins,
    AdminOnly,
    System
}

public enum ChatModerationState
{
    Active,
    Muted,
    Banned,
    Removed
}

public class ChatMessage : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public string SenderUserId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public ChatMessageType MessageType { get; set; }
    public ChatModerationState ModerationState { get; set; } = ChatModerationState.Active;
    public List<string> ReadByUserIds { get; set; } = new List<string>();
}

public class CombatState : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Round { get; set; }
    public List<InitiativeSlot> Slots { get; set; } = new List<InitiativeSlot>();
}

public class InitiativeSlot
{
    public int Order { get; set; }
    public InitiativeParticipant Participant { get; set; } = new InitiativeParticipant();
}

public class InitiativeParticipant
{
    public string CharacterId { get; set; } = string.Empty;
    public int Initiative { get; set; }
}

public enum AudioCategory
{
    Ambient,
    Combat,
    Narrative,
    Event
}

public class AudioTrackDefinition : EntityBase
{
    public AudioCategory Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}

public class SessionAudioState : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public string? CurrentTrackId { get; set; }
    public bool IsPaused { get; set; }
    public TimeSpan Position { get; set; }
}

public enum LockOwnerLevel
{
    Admin = 1,
    SuperAdmin = 2,
    Server = 3
}

public class EntityLock : EntityBase
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string LockedByUserId { get; set; } = string.Empty;
    public LockOwnerLevel OwnerLevel { get; set; } = LockOwnerLevel.Admin;
    public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow.AddHours(1);
}

public class AuditLogEntry : EntityBase
{
    public string Category { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
}
