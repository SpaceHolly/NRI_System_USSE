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
    public int? Age { get; set; }
    public string Race { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Backstory { get; set; } = string.Empty;
    public CharacterVisibilitySettings Visibility { get; set; } = new CharacterVisibilitySettings();
    public CharacterStats Stats { get; set; } = new CharacterStats();
    public List<CharacterClassProgress> ClassProgress { get; set; } = new List<CharacterClassProgress>();
    public List<SkillState> Skills { get; set; } = new List<SkillState>();
    public List<CharacterClassDirectionState> ClassDirections { get; set; } = new List<CharacterClassDirectionState>();
    public List<CharacterSkillState> CharacterSkillStates { get; set; } = new List<CharacterSkillState>();
    public CharacterProgressSnapshot? ClassSkillSnapshot { get; set; }
    public string ClassSkillDefinitionVersion { get; set; } = "1.0.0";
    public Wallet Wallet { get; set; } = new Wallet();
    public List<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
    public List<Companion> Companions { get; set; } = new List<Companion>();
    public List<HoldingRef> Holdings { get; set; } = new List<HoldingRef>();
    public List<ReputationRef> Reputation { get; set; } = new List<ReputationRef>();
}

public class Companion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
}

public class InventoryItem
{
    public string ItemCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public int? Durability { get; set; }
    public int? ConsumptionPerUse { get; set; }
    public bool Equipped { get; set; }
}

public class Holding : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> OwnerCharacterIds { get; set; } = new List<string>();
}

public class HoldingRef
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ReputationEntry : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public string? CharacterId { get; set; }
    public string GroupKey { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class ReputationRef
{
    public string Scope { get; set; } = "Personal";
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
    private static readonly CurrencyDenomination[] Order =
    {
        CurrencyDenomination.Iron,
        CurrencyDenomination.Bronze,
        CurrencyDenomination.Silver,
        CurrencyDenomination.Gold,
        CurrencyDenomination.Platinum,
        CurrencyDenomination.Orichalcum,
        CurrencyDenomination.Adamant,
        CurrencyDenomination.Sovereign
    };

    public MoneyBreakdown Balance { get; set; } = new MoneyBreakdown();

    public void EnsureAllDenominations()
    {
        foreach (var denomination in Order)
        {
            if (!Balance.Amounts.ContainsKey(denomination))
            {
                Balance.Amounts[denomination] = 0;
            }
        }
    }

    public void NormalizeUpward(long factor = 100)
    {
        EnsureAllDenominations();
        for (var index = 0; index < Order.Length - 1; index++)
        {
            var current = Order[index];
            var next = Order[index + 1];
            var amount = Balance.Amounts[current];
            if (amount < factor)
            {
                continue;
            }

            var carry = amount / factor;
            Balance.Amounts[current] = amount % factor;
            Balance.Amounts[next] += carry;
        }
    }

    public bool Spend(long amountInLowest, long factor = 100)
    {
        EnsureAllDenominations();
        var total = 0L;
        var multiplier = 1L;
        foreach (var denomination in Order)
        {
            total += Balance.Amounts[denomination] * multiplier;
            multiplier *= factor;
        }

        if (total < amountInLowest)
        {
            return false;
        }

        total -= amountInLowest;
        foreach (var denomination in Order)
        {
            Balance.Amounts[denomination] = total % factor;
            total /= factor;
        }

        return true;
    }
}

public class CharacterVisibilitySettings
{
    public bool HideDescriptionForOthers { get; set; }
    public bool HideBackstoryForOthers { get; set; }
    public bool HideStatsForOthers { get; set; }
    public bool HideReputationForOthers { get; set; }
}

public class CharacterStats
{
    public int Health { get; set; }
    public int PhysicalArmor { get; set; }
    public int MagicalArmor { get; set; }
    public int Morale { get; set; }
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Endurance { get; set; }
    public int Wisdom { get; set; }
    public int Intellect { get; set; }
    public int Charisma { get; set; }
}

public class CharacterClassProgress
{
    public string ClassCode { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Experience { get; set; }
}

public enum SkillType
{
    Passive,
    Activatable
}

public class SkillDefinition : EntityBase
{
    public string SkillCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SkillType Type { get; set; }
}

public class SkillState
{
    public string SkillCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SkillType Type { get; set; }
    public int Rank { get; set; }
    public bool IsAvailable { get; set; }
    public string UnavailableReason { get; set; } = string.Empty;
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

public class ChatReadState : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime? LastReadMessageUtc { get; set; }
    public string LastReadMessageId { get; set; } = string.Empty;
}

public class ChatRestrictionEntry
{
    public string UserId { get; set; } = string.Empty;
    public bool Muted { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ChatSlowModeSettings
{
    public int PublicSeconds { get; set; }
    public int HiddenToAdminsSeconds { get; set; }
    public int AdminOnlySeconds { get; set; }
}

public class SessionChatSettings : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public bool LockPlayers { get; set; }
    public ChatSlowModeSettings SlowMode { get; set; } = new ChatSlowModeSettings();
    public List<ChatRestrictionEntry> Restrictions { get; set; } = new List<ChatRestrictionEntry>();
}

public class ChatUserThrottleState : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public ChatMessageType MessageType { get; set; }
    public DateTime LastSentUtc { get; set; }
}

public class ChatMessage : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public string SenderUserId { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public ChatMessageType MessageType { get; set; }
    public string VisibilityChannel { get; set; } = string.Empty;
    public ChatModerationState ModerationState { get; set; } = ChatModerationState.Active;
    public List<string> ReadByUserIds { get; set; } = new List<string>();
}

public enum CombatStatus
{
    Lobby,
    Active,
    Paused,
    Ended,
    Archived
}

public enum ParticipantKind
{
    PlayerCharacter,
    Companion,
    Enemy,
    Npc,
    Other
}

public enum TurnStatus
{
    Waiting,
    Acted,
    Skipped,
    Eliminated,
    ExtraTurnPending
}

public class CombatRoundState
{
    public int RoundNumber { get; set; } = 1;
    public int CurrentTurnIndex { get; set; }
    public string? ActiveSlotId { get; set; }
}

public class InitiativeParticipant
{
    public string ParticipantId { get; set; } = Guid.NewGuid().ToString("N");
    public ParticipantKind Kind { get; set; }
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? OwnerUserId { get; set; }
    public string? CompanionOwnerEntityId { get; set; }
    public bool DetachedCompanion { get; set; }
    public int BaseRoll { get; set; }
    public List<int> TieBreakRolls { get; set; } = new List<int>();
    public bool ExtraTurnFirstRound { get; set; }
    public bool SkipFirstTurnRoundOne { get; set; }
    public TurnStatus Status { get; set; } = TurnStatus.Waiting;
}

public class InitiativeSlot
{
    public string SlotId { get; set; } = Guid.NewGuid().ToString("N");
    public int Order { get; set; }
    public bool IsGroup { get; set; }
    public List<string> ParticipantIds { get; set; } = new List<string>();
    public List<string> InternalOrder { get; set; } = new List<string>();
}

public class CombatState : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public CombatStatus Status { get; set; } = CombatStatus.Lobby;
    public CombatRoundState RoundState { get; set; } = new CombatRoundState();
    public List<InitiativeParticipant> Participants { get; set; } = new List<InitiativeParticipant>();
    public List<InitiativeSlot> Slots { get; set; } = new List<InitiativeSlot>();
    public string? ExtraFirstRoundParticipantId { get; set; }
    public bool ExtraFirstRoundConsumed { get; set; }
}

public class CombatLogEntry : EntityBase
{
    public string CombatId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
}

public class SessionCombatSnapshot
{
    public string CombatId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Round { get; set; }
    public string ActiveSlotId { get; set; } = string.Empty;
    public List<Dictionary<string, object>> Slots { get; set; } = new List<Dictionary<string, object>>();
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
