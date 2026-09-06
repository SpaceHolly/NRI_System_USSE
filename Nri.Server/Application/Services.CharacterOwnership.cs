using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope CharacterOwnershipGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!CharacterOwnershipReadEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var character = GetCharacter(characterId);
        var ownership = GetOrCreateCharacterOwnership(character, GetCurrentAccount(context), PayloadReader.GetString(context.Request.Payload, "campaignId"));
        return Ok("Character ownership loaded.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipList(CommandContext context)
    {
        RequireAdmin(context);
        if (!CharacterOwnershipReadEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var actor = GetCurrentAccount(context);
        var campaignId = (PayloadReader.GetString(payload, "campaignId") ?? string.Empty).Trim();
        var role = NormalizeOwnershipRole(PayloadReader.GetString(payload, "characterRole"), allowEmpty: true);
        var ownerUserId = (PayloadReader.GetString(payload, "ownerUserId") ?? string.Empty).Trim();
        var includeUnassigned = PayloadReader.GetBool(payload, "includeUnassigned");
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");

        EnsureOwnershipsForKnownCharacters(actor, campaignId);

        var items = _repositories.CharacterOwnerships.Find(FilterDefinition<CharacterOwnershipState>.Empty)
            .Where(x => string.IsNullOrWhiteSpace(campaignId) || string.Equals(x.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(role) || string.Equals(x.CharacterRole, role, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(ownerUserId) || string.Equals(x.OwnerUserId, ownerUserId, StringComparison.OrdinalIgnoreCase))
            .Where(x => includeUnassigned || !string.IsNullOrWhiteSpace(x.OwnerUserId))
            .Where(x => includeArchived || !string.Equals(x.AssignmentStatus, CharacterOwnershipAssignmentStatusIds.Archived, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.CharacterDisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(x => AdminCharacterOwnershipPayload(x, TryGetCharacter(x.CharacterId)))
            .Cast<object>()
            .ToArray();

        _logger.Admin($"character.ownership.list count={items.Length} campaignId={campaignId}");
        return Ok("Character ownership list loaded.", new Dictionary<string, object> { { "items", items }, { "count", items.Length } });
    }

    public ResponseEnvelope CharacterOwnershipAssignOwner(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CharacterAssignmentEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var character = GetCharacter(RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId"));
        var owner = RequireExistingAccount(RequireLength(PayloadReader.GetString(payload, "ownerUserId"), 8, 128, "ownerUserId"));
        var controllerId = RequireLength(PayloadReader.GetString(payload, "controlledByUserId"), 0, 128, "controlledByUserId");
        if (!string.IsNullOrWhiteSpace(controllerId)) _ = RequireExistingAccount(controllerId);
        var visible = payload.ContainsKey("isPlayerVisible") ? PayloadReader.GetBool(payload, "isPlayerVisible") : true;

        var ownership = GetOrCreateCharacterOwnership(character, actor, PayloadReader.GetString(payload, "campaignId"));
        ApplyOwnershipChange(ownership, character, actor, CharacterOwnershipAuditActionIds.AssignOwner, CharacterOwnershipRoleIds.PlayerCharacter, owner.Id, controllerId, visible, ownership.VisibilityMode, PayloadReader.GetString(payload, "reason"));
        return Ok("Character owner assigned.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipReassignOwner(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CharacterAssignmentEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var character = GetCharacter(RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId"));
        var owner = RequireExistingAccount(RequireLength(PayloadReader.GetString(payload, "newOwnerUserId"), 8, 128, "newOwnerUserId"));
        var controllerId = RequireLength(PayloadReader.GetString(payload, "newControlledByUserId"), 0, 128, "newControlledByUserId");
        if (!string.IsNullOrWhiteSpace(controllerId)) _ = RequireExistingAccount(controllerId);
        var ownership = GetOrCreateCharacterOwnership(character, actor, PayloadReader.GetString(payload, "campaignId"));

        ApplyOwnershipChange(ownership, character, actor, CharacterOwnershipAuditActionIds.ReassignOwner, CharacterOwnershipRoleIds.PlayerCharacter, owner.Id, controllerId, true, ownership.VisibilityMode, PayloadReader.GetString(payload, "reason"));
        return Ok("Character owner reassigned.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipClearOwner(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CharacterAssignmentEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var character = GetCharacter(RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId"));
        var ownership = GetOrCreateCharacterOwnership(character, actor, PayloadReader.GetString(payload, "campaignId"));
        ApplyOwnershipChange(ownership, character, actor, CharacterOwnershipAuditActionIds.ClearOwner, ownership.CharacterRole, string.Empty, string.Empty, false, ownership.VisibilityMode, PayloadReader.GetString(payload, "reason"));
        return Ok("Character owner cleared.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipSetController(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CharacterAssignmentEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var character = GetCharacter(RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId"));
        var controller = RequireExistingAccount(RequireLength(PayloadReader.GetString(payload, "controlledByUserId"), 8, 128, "controlledByUserId"));
        var ownership = GetOrCreateCharacterOwnership(character, actor, PayloadReader.GetString(payload, "campaignId"));
        ApplyOwnershipChange(ownership, character, actor, CharacterOwnershipAuditActionIds.SetController, ownership.CharacterRole, ownership.OwnerUserId, controller.Id, ownership.IsPlayerVisible, ownership.VisibilityMode, PayloadReader.GetString(payload, "reason"));
        return Ok("Character controller assigned.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipClearController(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CharacterAssignmentEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var character = GetCharacter(RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId"));
        var ownership = GetOrCreateCharacterOwnership(character, actor, PayloadReader.GetString(payload, "campaignId"));
        ApplyOwnershipChange(ownership, character, actor, CharacterOwnershipAuditActionIds.ClearController, ownership.CharacterRole, ownership.OwnerUserId, string.Empty, ownership.IsPlayerVisible, ownership.VisibilityMode, PayloadReader.GetString(payload, "reason"));
        return Ok("Character controller cleared.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipSetRole(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CharacterRoleConversionEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var character = GetCharacter(RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId"));
        var role = NormalizeOwnershipRole(PayloadReader.GetString(payload, "characterRole"), allowEmpty: false);
        var ownership = GetOrCreateCharacterOwnership(character, actor, PayloadReader.GetString(payload, "campaignId"));
        var action = string.Equals(role, CharacterOwnershipRoleIds.Custom, StringComparison.OrdinalIgnoreCase)
            ? CharacterOwnershipAuditActionIds.ConvertToCustomRole
            : CharacterOwnershipAuditActionIds.VisibilityChanged;
        ApplyOwnershipChange(ownership, character, actor, action, role, ownership.OwnerUserId, ownership.ControlledByUserId, ownership.IsPlayerVisible, ownership.VisibilityMode, PayloadReader.GetString(payload, "reason"));
        return Ok("Character role updated.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipConvertToPlayerCharacter(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CharacterRoleConversionEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var character = GetCharacter(RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId"));
        var owner = RequireExistingAccount(RequireLength(PayloadReader.GetString(payload, "ownerUserId"), 8, 128, "ownerUserId"));
        var controllerId = RequireLength(PayloadReader.GetString(payload, "controlledByUserId"), 0, 128, "controlledByUserId");
        if (!string.IsNullOrWhiteSpace(controllerId)) _ = RequireExistingAccount(controllerId);
        var ownership = GetOrCreateCharacterOwnership(character, actor, PayloadReader.GetString(payload, "campaignId"));
        var action = string.Equals(ownership.CharacterRole, CharacterOwnershipRoleIds.Companion, StringComparison.OrdinalIgnoreCase)
            ? CharacterOwnershipAuditActionIds.ConvertCompanionToPc
            : CharacterOwnershipAuditActionIds.ConvertNpcToPc;
        ApplyOwnershipChange(ownership, character, actor, action, CharacterOwnershipRoleIds.PlayerCharacter, owner.Id, controllerId, PayloadReader.GetBool(payload, "isPlayerVisible"), ownership.VisibilityMode, PayloadReader.GetString(payload, "reason"));
        return Ok("Character converted to player character.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipConvertToNpc(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CharacterRoleConversionEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var character = GetCharacter(RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId"));
        var ownership = GetOrCreateCharacterOwnership(character, actor, PayloadReader.GetString(payload, "campaignId"));
        var clearOwner = !payload.ContainsKey("clearOwner") || PayloadReader.GetBool(payload, "clearOwner");
        var visible = payload.ContainsKey("isPlayerVisible") ? PayloadReader.GetBool(payload, "isPlayerVisible") : false;
        ApplyOwnershipChange(ownership, character, actor, CharacterOwnershipAuditActionIds.ConvertPcToNpc, CharacterOwnershipRoleIds.NPC, clearOwner ? string.Empty : ownership.OwnerUserId, clearOwner ? string.Empty : ownership.ControlledByUserId, visible, ownership.VisibilityMode, PayloadReader.GetString(payload, "reason"));
        return Ok("Character converted to NPC.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipConvertToCompanion(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CharacterRoleConversionEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var character = GetCharacter(RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId"));
        var ownerId = RequireLength(PayloadReader.GetString(payload, "ownerUserId"), 0, 128, "ownerUserId");
        if (!string.IsNullOrWhiteSpace(ownerId)) _ = RequireExistingAccount(ownerId);
        var controllerId = RequireLength(PayloadReader.GetString(payload, "controlledByUserId"), 0, 128, "controlledByUserId");
        if (!string.IsNullOrWhiteSpace(controllerId)) _ = RequireExistingAccount(controllerId);
        var ownership = GetOrCreateCharacterOwnership(character, actor, PayloadReader.GetString(payload, "campaignId"));
        var visible = payload.ContainsKey("isPlayerVisible") ? PayloadReader.GetBool(payload, "isPlayerVisible") : ownership.IsPlayerVisible;
        ApplyOwnershipChange(ownership, character, actor, CharacterOwnershipAuditActionIds.ConvertPcToCompanion, CharacterOwnershipRoleIds.Companion, ownerId, controllerId, visible, ownership.VisibilityMode, PayloadReader.GetString(payload, "reason"));
        return Ok("Character converted to companion.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipSetVisibility(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CharacterAssignmentEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var character = GetCharacter(RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId"));
        var ownership = GetOrCreateCharacterOwnership(character, actor, PayloadReader.GetString(payload, "campaignId"));
        var visibility = payload.ContainsKey("visibilityMode") ? NormalizeOwnershipVisibility(PayloadReader.GetString(payload, "visibilityMode")) : ownership.VisibilityMode;
        ApplyOwnershipChange(ownership, character, actor, CharacterOwnershipAuditActionIds.VisibilityChanged, ownership.CharacterRole, ownership.OwnerUserId, ownership.ControlledByUserId, PayloadReader.GetBool(payload, "isPlayerVisible"), visibility, PayloadReader.GetString(payload, "reason"));
        ApplyOwnershipStatusPayload(ownership, character, actor.Id, payload);
        return Ok("Character ownership visibility updated.", new Dictionary<string, object> { { "ownership", AdminCharacterOwnershipPayload(ownership, character) } });
    }

    public ResponseEnvelope CharacterOwnershipAuditList(CommandContext context)
    {
        RequireAdmin(context);
        if (!CharacterOwnershipAuditEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = (PayloadReader.GetString(payload, "campaignId") ?? string.Empty).Trim();
        var characterId = (PayloadReader.GetString(payload, "characterId") ?? string.Empty).Trim();
        var ownerUserId = (PayloadReader.GetString(payload, "ownerUserId") ?? string.Empty).Trim();
        var actionType = (PayloadReader.GetString(payload, "actionType") ?? string.Empty).Trim();
        var limit = ClampOwnershipLimit(PayloadReader.GetInt(payload, "limit") ?? 100, 1, 500);

        var items = _repositories.CharacterOwnershipAudit.Find(FilterDefinition<CharacterOwnershipAuditEntry>.Empty)
            .Where(x => string.IsNullOrWhiteSpace(campaignId) || string.Equals(x.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(characterId) || string.Equals(x.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(ownerUserId) || string.Equals(x.ToOwnerUserId, ownerUserId, StringComparison.OrdinalIgnoreCase) || string.Equals(x.FromOwnerUserId, ownerUserId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(actionType) || string.Equals(x.ActionType, actionType, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.PerformedAtUtc)
            .Take(limit)
            .Select(AdminCharacterOwnershipAuditPayload)
            .Cast<object>()
            .ToArray();

        return Ok("Character ownership audit loaded.", new Dictionary<string, object> { { "items", items }, { "count", items.Length } });
    }

    public ResponseEnvelope CharacterPlayerAssignedList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterOwnershipPlayerViewEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = (PayloadReader.GetString(payload, "campaignId") ?? string.Empty).Trim();
        var includeCompanions = !payload.ContainsKey("includeCompanions") || PayloadReader.GetBool(payload, "includeCompanions");
        var roleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CharacterOwnershipRoleIds.PlayerCharacter };
        if (includeCompanions) roleSet.Add(CharacterOwnershipRoleIds.Companion);

        var items = _repositories.CharacterOwnerships.Find(FilterDefinition<CharacterOwnershipState>.Empty)
            .Where(x => string.Equals(x.OwnerUserId, actor.Id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.ControlledByUserId, actor.Id, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.IsPlayerVisible)
            .Where(x => !IsArchivedForPlayer(x))
            .Where(x => roleSet.Contains(x.CharacterRole))
            .Where(x => string.IsNullOrWhiteSpace(campaignId) || string.Equals(x.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase))
            .Select(x => PlayerAssignedCharacterPayload(x, TryGetCharacter(x.CharacterId), actor, context.Request.RequestId ?? string.Empty))
            .Cast<object>()
            .ToArray();

        return Ok("Assigned characters loaded.", new Dictionary<string, object> { { "items", items }, { "count", items.Length } });
    }

    public ResponseEnvelope CharacterPlayerAssignedGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterOwnershipPlayerViewEnabled())
            return CharacterOwnershipDisabled(context.Request.Command);

        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var ownership = GetCharacterOwnershipByCharacterId(characterId);
        if (ownership == null || !ownership.IsPlayerVisible
            || IsArchivedForPlayer(ownership)
            || (!string.Equals(ownership.OwnerUserId, actor.Id, StringComparison.OrdinalIgnoreCase) && !string.Equals(ownership.ControlledByUserId, actor.Id, StringComparison.OrdinalIgnoreCase)))
            return Error("assigned character not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        return Ok("Assigned character loaded.", new Dictionary<string, object> { { "character", PlayerAssignedCharacterPayload(ownership, TryGetCharacter(ownership.CharacterId), actor, context.Request.RequestId ?? string.Empty) } });
    }

    private CharacterOwnershipState GetOrCreateCharacterOwnership(Character character, UserAccount actor, string? campaignId)
    {
        var existing = GetCharacterOwnershipByCharacterId(character.Id);
        if (existing != null) return existing;

        var owner = TryGetAccount(character.OwnerUserId);
        var now = DateTime.UtcNow;
        var ownership = new CharacterOwnershipState
        {
            CampaignId = ResolveCharacterOwnershipCampaignId(character, campaignId),
            CharacterId = character.Id,
            CharacterDisplayName = character.Name ?? string.Empty,
            CharacterRole = string.IsNullOrWhiteSpace(character.OwnerUserId) ? CharacterOwnershipRoleIds.NPC : CharacterOwnershipRoleIds.PlayerCharacter,
            CharacterKind = string.IsNullOrWhiteSpace(character.OwnerUserId) ? CharacterKindIds.Npc : CharacterKindIds.PlayerCharacter,
            CharacterStatus = character.Archived || character.Deleted ? CharacterStatusIds.Archived : CharacterStatusIds.Active,
            IsActive = !character.Archived && !character.Deleted,
            IsArchived = character.Archived || character.Deleted,
            OwnerUserId = character.OwnerUserId ?? string.Empty,
            OwnerDisplayName = AccountDisplay(owner),
            IsPlayerVisible = !string.IsNullOrWhiteSpace(character.OwnerUserId),
            VisibilityMode = MapVisibilityModes.Party,
            AssignmentStatus = string.IsNullOrWhiteSpace(character.OwnerUserId) ? CharacterOwnershipAssignmentStatusIds.Unassigned : CharacterOwnershipAssignmentStatusIds.Assigned,
            AssignedAtUtc = string.IsNullOrWhiteSpace(character.OwnerUserId) ? null : now,
            AssignedByUserId = actor.Id,
            UpdatedAtUtc = now,
            UpdatedByUserId = actor.Id
        };
        _repositories.CharacterOwnerships.Insert(ownership);
        return ownership;
    }

    private void ApplyOwnershipChange(CharacterOwnershipState ownership, Character character, UserAccount actor, string action, string role, string ownerUserId, string controlledByUserId, bool isPlayerVisible, string visibilityMode, string? reason)
    {
        var previousRole = ownership.CharacterRole ?? string.Empty;
        var previousOwner = ownership.OwnerUserId ?? string.Empty;
        var previousController = ownership.ControlledByUserId ?? string.Empty;
        var owner = TryGetAccount(ownerUserId);
        var controller = TryGetAccount(controlledByUserId);
        var now = DateTime.UtcNow;

        ownership.PreviousCharacterRole = previousRole;
        ownership.PreviousOwnerUserId = previousOwner;
        ownership.CharacterDisplayName = character.Name ?? ownership.CharacterDisplayName ?? string.Empty;
        ownership.CharacterRole = NormalizeOwnershipRole(role, allowEmpty: false);
        ownership.CharacterKind = MapOwnershipRoleToCharacterKind(ownership.CharacterRole);
        ownership.OwnerUserId = ownerUserId ?? string.Empty;
        ownership.OwnerDisplayName = AccountDisplay(owner);
        ownership.ControlledByUserId = controlledByUserId ?? string.Empty;
        ownership.ControlledByDisplayName = AccountDisplay(controller);
        ownership.IsPlayerVisible = isPlayerVisible;
        ownership.VisibilityMode = NormalizeOwnershipVisibility(visibilityMode);
        ownership.AssignmentStatus = ResolveAssignmentStatus(action, ownership.OwnerUserId);
        ownership.AssignedAtUtc = string.IsNullOrWhiteSpace(ownership.OwnerUserId) ? null : (ownership.AssignedAtUtc ?? now);
        ownership.AssignedByUserId = string.IsNullOrWhiteSpace(ownership.OwnerUserId) ? string.Empty : actor.Id;
        ownership.UpdatedAtUtc = now;
        ownership.UpdatedByUserId = actor.Id;

        character.OwnerUserId = ownership.OwnerUserId;
        _repositories.Characters.Replace(character);
        _repositories.CharacterOwnerships.Replace(ownership);
        WriteCharacterOwnershipAudit(ownership, actor, action, previousRole, previousOwner, previousController, reason);
        SyncCharacterGroupOwnershipSummary(ownership, actor.Id);

        _logger.Admin($"character.ownership.{action}.done characterId={character.Id} owner={ownership.OwnerUserId} role={ownership.CharacterRole}");
    }

    private void WriteCharacterOwnershipAudit(CharacterOwnershipState ownership, UserAccount actor, string action, string previousRole, string previousOwner, string previousController, string? reason)
    {
        if (!CharacterOwnershipAuditEnabled()) return;
        var entry = new CharacterOwnershipAuditEntry
        {
            CampaignId = ownership.CampaignId,
            CharacterId = ownership.CharacterId,
            ActionType = action,
            FromRole = previousRole ?? string.Empty,
            ToRole = ownership.CharacterRole ?? string.Empty,
            FromOwnerUserId = previousOwner ?? string.Empty,
            ToOwnerUserId = ownership.OwnerUserId ?? string.Empty,
            FromControlledByUserId = previousController ?? string.Empty,
            ToControlledByUserId = ownership.ControlledByUserId ?? string.Empty,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Не указана" : reason!.Trim(),
            PerformedByUserId = actor.Id,
            PerformedAtUtc = DateTime.UtcNow,
            Summary = $"{action}: {ownership.CharacterDisplayName}",
            PublicSummary = $"{ownership.CharacterDisplayName}: роль/владелец обновлены"
        };
        _repositories.CharacterOwnershipAudit.Insert(entry);
    }

    private void ApplyOwnershipStatusPayload(CharacterOwnershipState ownership, Character character, string actorUserId, Dictionary<string, object> payload)
    {
        var changed = false;
        if (payload.ContainsKey("characterStatus"))
        {
            ownership.CharacterStatus = NormalizeCharacterStatus(PayloadReader.GetString(payload, "characterStatus"));
            changed = true;
        }

        if (payload.ContainsKey("isActive"))
        {
            ownership.IsActive = PayloadReader.GetBool(payload, "isActive");
            ownership.CharacterStatus = ownership.IsActive ? CharacterStatusIds.Active : CharacterStatusIds.Inactive;
            changed = true;
        }

        if (payload.ContainsKey("isArchived"))
        {
            ownership.IsArchived = PayloadReader.GetBool(payload, "isArchived");
            if (ownership.IsArchived)
            {
                ownership.IsActive = false;
                ownership.CharacterStatus = CharacterStatusIds.Archived;
            }
            else if (string.Equals(ownership.CharacterStatus, CharacterStatusIds.Archived, StringComparison.OrdinalIgnoreCase))
            {
                ownership.CharacterStatus = ownership.IsActive ? CharacterStatusIds.Active : CharacterStatusIds.Inactive;
            }
            changed = true;
        }

        if (!changed) return;

        if (ownership.IsArchived || string.Equals(ownership.CharacterStatus, CharacterStatusIds.Archived, StringComparison.OrdinalIgnoreCase))
        {
            ownership.AssignmentStatus = CharacterOwnershipAssignmentStatusIds.Archived;
        }
        else if (string.IsNullOrWhiteSpace(ownership.OwnerUserId))
        {
            ownership.AssignmentStatus = CharacterOwnershipAssignmentStatusIds.Unassigned;
        }
        else if (string.Equals(ownership.AssignmentStatus, CharacterOwnershipAssignmentStatusIds.Archived, StringComparison.OrdinalIgnoreCase))
        {
            ownership.AssignmentStatus = CharacterOwnershipAssignmentStatusIds.Assigned;
        }

        ownership.UpdatedAtUtc = DateTime.UtcNow;
        ownership.UpdatedByUserId = actorUserId;

        // Compatibility facade for older read paths. 0.14.42 GUI reads/writes character_ownerships.
        character.Archived = ownership.IsArchived;
        _repositories.Characters.Replace(character);
        _repositories.CharacterOwnerships.Replace(ownership);
        _logger.Admin($"character.ownership.status.done characterId={character.Id} status={ownership.CharacterStatus} active={ownership.IsActive} archived={ownership.IsArchived}");
    }

    private void SyncCharacterGroupOwnershipSummary(CharacterOwnershipState ownership, string userId)
    {
        if (!_featureFlags.IsEnabled(nameof(CharacterOwnershipFeatureFlags.UseCharacterGroupOwnershipSync))) return;
        var members = _repositories.CharacterGroupMembers.Find(Builders<CharacterGroupMemberState>.Filter.Eq(x => x.EntityId, ownership.CharacterId));
        foreach (var member in members.Where(x => x.RemovedAtUtc == null && !x.Deleted))
        {
            member.CharacterRole = MapOwnershipRoleToGroupRole(ownership.CharacterRole);
            member.OwnerUserId = ownership.OwnerUserId ?? string.Empty;
            member.ControlledByUserId = ownership.ControlledByUserId ?? string.Empty;
            member.DisplayName = string.IsNullOrWhiteSpace(ownership.CharacterDisplayName) ? member.DisplayName : ownership.CharacterDisplayName;
            member.IsPlayerVisible = ownership.IsPlayerVisible;
            member.VisibilityMode = ownership.VisibilityMode;
            _repositories.CharacterGroupMembers.Replace(member);
        }
    }

    private void EnsureOwnershipsForKnownCharacters(UserAccount actor, string campaignId)
    {
        foreach (var character in _repositories.Characters.Find(FilterDefinition<Character>.Empty).Where(x => !x.Deleted))
        {
            if (!string.IsNullOrWhiteSpace(campaignId) && !string.Equals(ResolveCharacterOwnershipCampaignId(character, campaignId), campaignId, StringComparison.OrdinalIgnoreCase))
                continue;
            _ = GetOrCreateCharacterOwnership(character, actor, campaignId);
        }
    }

    private CharacterOwnershipState? GetCharacterOwnershipByCharacterId(string characterId)
        => _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();

    private Character? TryGetCharacter(string characterId)
        => string.IsNullOrWhiteSpace(characterId) ? null : _repositories.Characters.GetById(characterId);

    private UserAccount? TryGetAccount(string userId)
        => string.IsNullOrWhiteSpace(userId) ? null : _repositories.Accounts.GetById(userId);

    private static bool IsArchivedForPlayer(CharacterOwnershipState ownership)
        => ownership.IsArchived || string.Equals(ownership.CharacterStatus, CharacterStatusIds.Archived, StringComparison.OrdinalIgnoreCase);

    private UserAccount RequireExistingAccount(string userId)
        => _repositories.Accounts.GetById(userId) ?? throw new KeyNotFoundException("Account not found.");

    private static string AccountDisplay(UserAccount? account)
        => account == null ? string.Empty : FirstOwnershipNonEmpty(account.Login, account.Id);

    private static string ResolveCharacterOwnershipCampaignId(Character character, string? requestedCampaignId)
        => FirstOwnershipNonEmpty(requestedCampaignId, character.SessionId, "default");

    private Dictionary<string, object> AdminCharacterOwnershipPayload(CharacterOwnershipState ownership, Character? character)
    {
        var groupMembers = _repositories.CharacterGroupMembers.Find(Builders<CharacterGroupMemberState>.Filter.Eq(x => x.EntityId, ownership.CharacterId))
            .Where(x => x.RemovedAtUtc == null && !x.Deleted)
            .Select(x => new Dictionary<string, object>
            {
                { "groupId", x.GroupId },
                { "displayName", x.DisplayName ?? string.Empty },
                { "roleInGroup", x.RoleInGroup ?? string.Empty },
                { "characterRole", x.CharacterRole ?? string.Empty },
                { "isPlayerVisible", x.IsPlayerVisible }
            })
            .Cast<object>()
            .ToArray();

        return new Dictionary<string, object>
        {
            { "id", ownership.Id },
            { "campaignId", ownership.CampaignId ?? string.Empty },
            { "characterId", ownership.CharacterId ?? string.Empty },
            { "characterDisplayName", FirstOwnershipNonEmpty(ownership.CharacterDisplayName, character?.Name, ownership.CharacterId) },
            { "characterRole", ownership.CharacterRole ?? CharacterOwnershipRoleIds.PlayerCharacter },
            { "characterKind", FirstOwnershipNonEmpty(ownership.CharacterKind, MapOwnershipRoleToCharacterKind(ownership.CharacterRole)) },
            { "characterKindDisplayName", CharacterKindDisplayName(FirstOwnershipNonEmpty(ownership.CharacterKind, MapOwnershipRoleToCharacterKind(ownership.CharacterRole))) },
            { "characterStatus", FirstOwnershipNonEmpty(ownership.CharacterStatus, ownership.IsArchived ? CharacterStatusIds.Archived : ownership.IsActive ? CharacterStatusIds.Active : CharacterStatusIds.Inactive) },
            { "characterStatusDisplayName", CharacterStatusDisplayName(FirstOwnershipNonEmpty(ownership.CharacterStatus, ownership.IsArchived ? CharacterStatusIds.Archived : ownership.IsActive ? CharacterStatusIds.Active : CharacterStatusIds.Inactive)) },
            { "isActive", ownership.IsActive },
            { "isArchived", ownership.IsArchived },
            { "ownerUserId", ownership.OwnerUserId ?? string.Empty },
            { "ownerDisplayName", ownership.OwnerDisplayName ?? string.Empty },
            { "controlledByUserId", ownership.ControlledByUserId ?? string.Empty },
            { "controlledByDisplayName", ownership.ControlledByDisplayName ?? string.Empty },
            { "previousOwnerUserId", ownership.PreviousOwnerUserId ?? string.Empty },
            { "previousCharacterRole", ownership.PreviousCharacterRole ?? string.Empty },
            { "isPlayerVisible", ownership.IsPlayerVisible },
            { "visibilityMode", ownership.VisibilityMode ?? MapVisibilityModes.Party },
            { "assignmentStatus", ownership.AssignmentStatus ?? CharacterOwnershipAssignmentStatusIds.Unassigned },
            { "assignedAtUtc", ownership.AssignedAtUtc.HasValue ? (object)ownership.AssignedAtUtc.Value : string.Empty },
            { "assignedByUserId", ownership.AssignedByUserId ?? string.Empty },
            { "updatedAtUtc", ownership.UpdatedAtUtc },
            { "updatedByUserId", ownership.UpdatedByUserId ?? string.Empty },
            { "publicNotes", ownership.PublicNotes ?? string.Empty },
            { "gmNotes", ownership.GMNotes ?? string.Empty },
            { "archived", ownership.IsArchived },
            { "deleted", character?.Deleted ?? false },
            { "groupMembership", groupMembers }
        };
    }

    private Dictionary<string, object> PlayerAssignedCharacterPayload(CharacterOwnershipState ownership, Character? character, UserAccount actor, string requestId)
    {
        var archivedForPlayer = ownership.IsArchived || character?.Archived == true || character?.Deleted == true;
        var profileReady = character != null
            && !archivedForPlayer
            && _characterDetailsProfileBuilder.CanBuildFromProfilesAsync(character.Id).GetAwaiter().GetResult();
        var card = profileReady
            ? CharacterDetailsPayloadWithProfileFirst(character!, TryGetAccount(character!.OwnerUserId) ?? actor, actor, requestId)
            : new Dictionary<string, object>();
        var stats = ClientMap(card.TryGetValue("stats", out var statsRaw) ? statsRaw : null);

        return new Dictionary<string, object>
        {
            { "campaignId", ownership.CampaignId ?? string.Empty },
            { "characterId", ownership.CharacterId ?? string.Empty },
            { "displayName", FirstOwnershipNonEmpty(ClientString(card, "name"), ownership.CharacterDisplayName, "Без имени") },
            { "name", FirstOwnershipNonEmpty(ClientString(card, "name"), ownership.CharacterDisplayName, "Без имени") },
            { "characterRole", ownership.CharacterRole ?? CharacterOwnershipRoleIds.PlayerCharacter },
            { "characterKind", FirstOwnershipNonEmpty(ownership.CharacterKind, MapOwnershipRoleToCharacterKind(ownership.CharacterRole)) },
            { "characterKindDisplayName", CharacterKindDisplayName(FirstOwnershipNonEmpty(ownership.CharacterKind, MapOwnershipRoleToCharacterKind(ownership.CharacterRole))) },
            { "characterStatus", FirstOwnershipNonEmpty(ownership.CharacterStatus, ownership.IsArchived ? CharacterStatusIds.Archived : ownership.IsActive ? CharacterStatusIds.Active : CharacterStatusIds.Inactive) },
            { "characterStatusDisplayName", CharacterStatusDisplayName(FirstOwnershipNonEmpty(ownership.CharacterStatus, ownership.IsArchived ? CharacterStatusIds.Archived : ownership.IsActive ? CharacterStatusIds.Active : CharacterStatusIds.Inactive)) },
            { "ownerUserId", ownership.OwnerUserId ?? string.Empty },
            { "ownerDisplayName", ownership.OwnerDisplayName ?? string.Empty },
            { "controlledByUserId", ownership.ControlledByUserId ?? string.Empty },
            { "controlledByDisplayName", ownership.ControlledByDisplayName ?? string.Empty },
            { "isAssigned", !string.IsNullOrWhiteSpace(ownership.OwnerUserId) },
            { "isActive", ownership.IsActive },
            { "isArchived", archivedForPlayer },
            { "archived", archivedForPlayer },
            { "isPlayerVisible", ownership.IsPlayerVisible },
            { "isSelectable", profileReady && ownership.IsActive && !archivedForPlayer },
            { "profileState", archivedForPlayer ? CharacterStatusIds.Archived : profileReady ? ApplicationContextStates.Ready : ApplicationContextStates.ProfileMigrationRequired },
            { "availabilityMessage", archivedForPlayer ? "Персонаж находится в архиве." : profileReady ? string.Empty : "Данные персонажа временно недоступны. Обратитесь к мастеру." },
            { "race", FirstOwnershipNonEmpty(ClientString(card, "race"), "—") },
            { "height", FirstOwnershipNonEmpty(ClientString(card, "height"), "—") },
            { "description", ClientString(card, "description") },
            { "selectedTitle", ClientString(card, "selectedTitle") },
            { "xpCoins", card.TryGetValue("xpCoins", out var xpCoins) ? xpCoins : 0 },
            { "stats", stats },
            { "profileSource", ClientString(card, "profileSource") },
            { "groupMembership", PlayerVisibleGroupMembershipPayload(ownership.CharacterId) }
        };
    }

    private object[] PlayerVisibleGroupMembershipPayload(string characterId)
        => _repositories.CharacterGroupMembers.Find(Builders<CharacterGroupMemberState>.Filter.Eq(x => x.EntityId, characterId))
            .Where(IsMemberVisibleForPlayer)
            .Select(x => new Dictionary<string, object>
            {
                { "groupId", x.GroupId },
                { "displayName", x.DisplayName ?? string.Empty },
                { "roleInGroup", x.RoleInGroup ?? string.Empty },
                { "characterRole", x.CharacterRole ?? string.Empty }
            })
            .Cast<object>()
            .ToArray();

    private static Dictionary<string, object> AdminCharacterOwnershipAuditPayload(CharacterOwnershipAuditEntry entry)
        => new Dictionary<string, object>
        {
            { "id", entry.Id },
            { "campaignId", entry.CampaignId ?? string.Empty },
            { "characterId", entry.CharacterId ?? string.Empty },
            { "actionType", entry.ActionType ?? string.Empty },
            { "fromRole", entry.FromRole ?? string.Empty },
            { "toRole", entry.ToRole ?? string.Empty },
            { "fromOwnerUserId", entry.FromOwnerUserId ?? string.Empty },
            { "toOwnerUserId", entry.ToOwnerUserId ?? string.Empty },
            { "reason", entry.Reason ?? string.Empty },
            { "performedByUserId", entry.PerformedByUserId ?? string.Empty },
            { "performedAtUtc", entry.PerformedAtUtc },
            { "summary", entry.Summary ?? string.Empty },
            { "publicSummary", entry.PublicSummary ?? string.Empty },
            { "gmNotes", entry.GMNotes ?? string.Empty }
        };

    private static string NormalizeOwnershipRole(string? value, bool allowEmpty)
    {
        var key = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(key)) return string.Empty;
        return key.ToLowerInvariant().Replace("_", string.Empty) switch
        {
            "playercharacter" or "pc" => CharacterOwnershipRoleIds.PlayerCharacter,
            "npc" => CharacterOwnershipRoleIds.NPC,
            "companion" => CharacterOwnershipRoleIds.Companion,
            "temporaryally" => CharacterOwnershipRoleIds.TemporaryAlly,
            "enemy" => CharacterOwnershipRoleIds.Enemy,
            "neutral" => CharacterOwnershipRoleIds.Neutral,
            "inactive" => CharacterOwnershipRoleIds.Inactive,
            "custom" => CharacterOwnershipRoleIds.Custom,
            _ => CharacterOwnershipRoleIds.PlayerCharacter
        };
    }

    private static string MapOwnershipRoleToGroupRole(string? role)
    {
        return NormalizeOwnershipRole(role, allowEmpty: false) switch
        {
            CharacterOwnershipRoleIds.NPC => CharacterGroupCharacterRoleIds.NPC,
            CharacterOwnershipRoleIds.Companion => CharacterGroupCharacterRoleIds.Companion,
            CharacterOwnershipRoleIds.TemporaryAlly => CharacterGroupCharacterRoleIds.TemporaryAlly,
            CharacterOwnershipRoleIds.Enemy => CharacterGroupCharacterRoleIds.Enemy,
            CharacterOwnershipRoleIds.Inactive => CharacterGroupCharacterRoleIds.Inactive,
            CharacterOwnershipRoleIds.Custom => CharacterGroupCharacterRoleIds.Custom,
            _ => CharacterGroupCharacterRoleIds.PlayerCharacter
        };
    }

    private static string MapOwnershipRoleToCharacterKind(string? role)
    {
        return NormalizeOwnershipRole(role, allowEmpty: false) switch
        {
            CharacterOwnershipRoleIds.NPC => CharacterKindIds.Npc,
            CharacterOwnershipRoleIds.Companion => CharacterKindIds.Companion,
            CharacterOwnershipRoleIds.TemporaryAlly => CharacterKindIds.TemporaryAlly,
            CharacterOwnershipRoleIds.Enemy => CharacterKindIds.Enemy,
            CharacterOwnershipRoleIds.Neutral => CharacterKindIds.Neutral,
            CharacterOwnershipRoleIds.Custom => CharacterKindIds.Custom,
            _ => CharacterKindIds.PlayerCharacter
        };
    }

    private static string NormalizeCharacterStatus(string? value)
    {
        var key = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("_", string.Empty);
        return key switch
        {
            "inactive" or "disabled" => CharacterStatusIds.Inactive,
            "archived" or "archive" => CharacterStatusIds.Archived,
            _ => CharacterStatusIds.Active
        };
    }

    private static string CharacterKindDisplayName(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            CharacterKindIds.Npc => "NPC",
            CharacterKindIds.Companion => "Компаньон",
            CharacterKindIds.TemporaryAlly => "Временный союзник",
            CharacterKindIds.Enemy => "Враг",
            CharacterKindIds.Neutral => "Нейтральный",
            CharacterKindIds.Custom => "Другое",
            _ => "Персонаж игрока"
        };
    }

    private static string CharacterStatusDisplayName(string? value)
    {
        return NormalizeCharacterStatus(value) switch
        {
            CharacterStatusIds.Inactive => "Неактивен",
            CharacterStatusIds.Archived => "В архиве",
            _ => "Активен"
        };
    }

    private static string NormalizeOwnershipVisibility(string? value)
    {
        var key = (value ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "public" => MapVisibilityModes.Public,
            "party" => MapVisibilityModes.Party,
            "players" => MapVisibilityModes.Party,
            "gm_only" or "gmonly" => MapVisibilityModes.GmOnly,
            "hidden" => MapVisibilityModes.Hidden,
            _ => MapVisibilityModes.Party
        };
    }

    private static string ResolveAssignmentStatus(string action, string ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId)) return CharacterOwnershipAssignmentStatusIds.Unassigned;
        return action switch
        {
            CharacterOwnershipAuditActionIds.ReassignOwner => CharacterOwnershipAssignmentStatusIds.Transferred,
            CharacterOwnershipAuditActionIds.ConvertNpcToPc or CharacterOwnershipAuditActionIds.ConvertCompanionToPc or CharacterOwnershipAuditActionIds.ConvertPcToNpc or CharacterOwnershipAuditActionIds.ConvertPcToCompanion or CharacterOwnershipAuditActionIds.ConvertToCustomRole => CharacterOwnershipAssignmentStatusIds.Converted,
            _ => CharacterOwnershipAssignmentStatusIds.Assigned
        };
    }

    private static int ClampOwnershipLimit(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private ResponseEnvelope CharacterOwnershipDisabled(string commandName)
    {
        _logger.Admin($"character.ownership.disabled command={commandName}");
        return Error("character ownership is disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool CharacterOwnershipReadEnabled()
        => _featureFlags.IsEnabled(nameof(CharacterOwnershipFeatureFlags.UseCharacterOwnershipMvp));

    private bool CharacterAssignmentEnabled()
        => CharacterOwnershipReadEnabled() && _featureFlags.IsEnabled(nameof(CharacterOwnershipFeatureFlags.UseCharacterAssignmentMvp));

    private bool CharacterRoleConversionEnabled()
        => CharacterOwnershipReadEnabled() && _featureFlags.IsEnabled(nameof(CharacterOwnershipFeatureFlags.UseCharacterRoleConversionMvp));

    private bool CharacterOwnershipPlayerViewEnabled()
        => CharacterOwnershipReadEnabled() && _featureFlags.IsEnabled(nameof(CharacterOwnershipFeatureFlags.UseCharacterOwnerPlayerView));

    private bool CharacterOwnershipAuditEnabled()
        => CharacterOwnershipReadEnabled() && _featureFlags.IsEnabled(nameof(CharacterOwnershipFeatureFlags.UseCharacterOwnershipAudit));

    private static string FirstOwnershipNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return string.Empty;
    }
}
