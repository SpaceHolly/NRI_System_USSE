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
    public ResponseEnvelope GroupCharacterList(CommandContext context)
    {
        GetCurrentAccount(context);
        if (!CharacterGroupsReadEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var groups = ListCharacterGroups(campaignId, sessionId, includeArchived);
        var items = groups
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var count = ListGroupMembers(group.Id, includeRemoved: false).Count;
                return CharacterGroupListPayload(group, count);
            })
            .Cast<object>()
            .ToArray();

        _logger.Admin($"group.character.list campaignId={campaignId} sessionId={sessionId} count={items.Length}");
        return Ok("Character groups loaded.", new Dictionary<string, object>
        {
            { "items", items },
            { "count", items.Length }
        });
    }

    public ResponseEnvelope GroupCharacterCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupsWriteEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var now = DateTime.UtcNow;
        var group = new CharacterGroupState
        {
            CampaignId = campaignId,
            SessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId"),
            Name = CharacterGroupFirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "name"), 0, 160, "name"), "Новая группа"),
            Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description"),
            GroupType = NormalizeCharacterGroupType(PayloadReader.GetString(payload, "groupType")),
            Status = NormalizeCharacterGroupStatus(PayloadReader.GetString(payload, "status")),
            VisibilityMode = NormalizeGroupVisibility(PayloadReader.GetString(payload, "visibilityMode")),
            IsPlayerVisible = payload.ContainsKey("isPlayerVisible") && PayloadReader.GetBool(payload, "isPlayerVisible"),
            PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes"),
            GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes"),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };

        _repositories.CharacterGroups.Insert(group);
        _logger.Admin($"group.character.create.done groupId={group.Id} campaignId={campaignId}");
        return CharacterGroupResponse(group, "Character group created.");
    }

    public ResponseEnvelope GroupCharacterGet(CommandContext context)
    {
        GetCurrentAccount(context);
        if (!CharacterGroupsReadEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var group = RequireCharacterGroup(context);
        return CharacterGroupResponse(group, "Character group loaded.");
    }

    public ResponseEnvelope GroupCharacterUpdate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupsWriteEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var group = RequireCharacterGroup(context);
        if (payload.ContainsKey("name"))
            group.Name = CharacterGroupFirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "name"), 0, 160, "name"), group.Name, "Новая группа");
        if (payload.ContainsKey("description"))
            group.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        if (payload.ContainsKey("sessionId"))
            group.SessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        if (payload.ContainsKey("groupType"))
            group.GroupType = NormalizeCharacterGroupType(PayloadReader.GetString(payload, "groupType"));
        if (payload.ContainsKey("status"))
            group.Status = NormalizeCharacterGroupStatus(PayloadReader.GetString(payload, "status"));
        if (payload.ContainsKey("visibilityMode"))
            group.VisibilityMode = NormalizeGroupVisibility(PayloadReader.GetString(payload, "visibilityMode"));
        if (payload.ContainsKey("isPlayerVisible"))
            group.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("publicNotes"))
            group.PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes");
        if (payload.ContainsKey("gmNotes"))
            group.GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");

        TouchCharacterGroup(group, actor.Id);
        _repositories.CharacterGroups.Replace(group);
        _logger.Admin($"group.character.update groupId={group.Id}");
        return CharacterGroupResponse(group, "Character group updated.");
    }

    public ResponseEnvelope GroupCharacterArchive(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupsWriteEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var group = RequireCharacterGroup(context);
        group.IsArchived = true;
        group.Archived = true;
        group.IsActive = false;
        group.Status = CharacterGroupStatusIds.Archived;
        TouchCharacterGroup(group, actor.Id);
        _repositories.CharacterGroups.Replace(group);
        _logger.Admin($"group.character.archive groupId={group.Id}");
        return Ok("Character group archived.", new Dictionary<string, object> { { "groupId", group.Id } });
    }

    public ResponseEnvelope GroupCharacterMemberAdd(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupMembershipEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var group = RequireCharacterGroup(context);
        if (IsArchivedCharacterGroup(group))
            return Error("archived character group cannot be changed", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var entityType = NormalizeCharacterGroupEntityType(PayloadReader.GetString(payload, "entityType"));
        var entityId = RequireLength(PayloadReader.GetString(payload, "entityId"), 1, 128, "entityId");
        var duplicate = ListGroupMembers(group.Id, includeRemoved: false)
            .FirstOrDefault(member => string.Equals(member.EntityType, entityType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(member.EntityId, entityId, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
            return Error("member already exists in group", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var nextSort = ListGroupMembers(group.Id, includeRemoved: false).Select(x => x.SortOrder).DefaultIfEmpty(0).Max() + 10;
        var member = new CharacterGroupMemberState
        {
            GroupId = group.Id,
            CampaignId = group.CampaignId,
            EntityType = entityType,
            EntityId = entityId,
            DisplayName = CharacterGroupFirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "displayName"), 0, 160, "displayName"), entityId),
            RoleInGroup = NormalizeRoleInGroup(PayloadReader.GetString(payload, "roleInGroup")),
            CharacterRole = NormalizeCharacterRole(PayloadReader.GetString(payload, "characterRole")),
            OwnerUserId = RequireLength(PayloadReader.GetString(payload, "ownerUserId"), 0, 128, "ownerUserId"),
            ControlledByUserId = RequireLength(PayloadReader.GetString(payload, "controlledByUserId"), 0, 128, "controlledByUserId"),
            IsLeader = PayloadReader.GetBool(payload, "isLeader"),
            IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
            VisibilityMode = NormalizeGroupVisibility(PayloadReader.GetString(payload, "visibilityMode")),
            PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes"),
            GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes"),
            AddedByUserId = actor.Id,
            JoinedAtUtc = DateTime.UtcNow,
            SortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? nextSort
        };

        _repositories.CharacterGroupMembers.Insert(member);
        TouchCharacterGroup(group, actor.Id);
        _repositories.CharacterGroups.Replace(group);
        _logger.Admin($"group.character.member.add groupId={group.Id} memberId={member.Id} entityType={member.EntityType}");
        return CharacterGroupResponse(group, "Character group member added.");
    }

    public ResponseEnvelope GroupCharacterMemberRemove(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupMembershipEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var member = RequireCharacterGroupMember(context);
        member.RemovedAtUtc = DateTime.UtcNow;
        member.IsPlayerVisible = false;
        member.UpdatedUtc = DateTime.UtcNow;
        _repositories.CharacterGroupMembers.Replace(member);
        var group = _repositories.CharacterGroups.GetById(member.GroupId);
        if (group != null)
        {
            TouchCharacterGroup(group, actor.Id);
            _repositories.CharacterGroups.Replace(group);
        }

        _logger.Admin($"group.character.member.remove memberId={member.Id}");
        return Ok("Character group member removed.", new Dictionary<string, object>
        {
            { "memberId", member.Id },
            { "groupId", member.GroupId }
        });
    }

    public ResponseEnvelope GroupCharacterMemberUpdate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupMembershipEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var member = RequireCharacterGroupMember(context);
        if (payload.ContainsKey("displayName"))
            member.DisplayName = CharacterGroupFirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "displayName"), 0, 160, "displayName"), member.DisplayName);
        if (payload.ContainsKey("roleInGroup"))
            member.RoleInGroup = NormalizeRoleInGroup(PayloadReader.GetString(payload, "roleInGroup"));
        if (payload.ContainsKey("characterRole"))
            member.CharacterRole = NormalizeCharacterRole(PayloadReader.GetString(payload, "characterRole"));
        if (payload.ContainsKey("ownerUserId"))
            member.OwnerUserId = RequireLength(PayloadReader.GetString(payload, "ownerUserId"), 0, 128, "ownerUserId");
        if (payload.ContainsKey("controlledByUserId"))
            member.ControlledByUserId = RequireLength(PayloadReader.GetString(payload, "controlledByUserId"), 0, 128, "controlledByUserId");
        if (payload.ContainsKey("isLeader"))
            member.IsLeader = PayloadReader.GetBool(payload, "isLeader");
        if (payload.ContainsKey("isPlayerVisible"))
            member.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("visibilityMode"))
            member.VisibilityMode = NormalizeGroupVisibility(PayloadReader.GetString(payload, "visibilityMode"));
        if (payload.ContainsKey("publicNotes"))
            member.PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes");
        if (payload.ContainsKey("gmNotes"))
            member.GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");
        if (payload.ContainsKey("sortOrder"))
            member.SortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? member.SortOrder;

        _repositories.CharacterGroupMembers.Replace(member);
        var group = _repositories.CharacterGroups.GetById(member.GroupId);
        if (group != null)
        {
            TouchCharacterGroup(group, actor.Id);
            _repositories.CharacterGroups.Replace(group);
            _logger.Admin($"group.character.member.update memberId={member.Id}");
            return CharacterGroupResponse(group, "Character group member updated.");
        }

        return Ok("Character group member updated.", new Dictionary<string, object> { { "memberId", member.Id } });
    }

    public ResponseEnvelope GroupCharacterMemberMove(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupMembershipEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var member = RequireCharacterGroupMember(context);
        member.SortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? member.SortOrder;
        _repositories.CharacterGroupMembers.Replace(member);
        var group = _repositories.CharacterGroups.GetById(member.GroupId);
        if (group != null)
        {
            TouchCharacterGroup(group, actor.Id);
            _repositories.CharacterGroups.Replace(group);
        }

        _logger.Admin($"group.character.member.move memberId={member.Id} sortOrder={member.SortOrder}");
        return Ok("Character group member moved.", new Dictionary<string, object>
        {
            { "memberId", member.Id },
            { "groupId", member.GroupId },
            { "sortOrder", member.SortOrder }
        });
    }

    public ResponseEnvelope GroupCharacterSetActive(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupSessionLinkEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var group = RequireCharacterGroup(context);
        if (!string.Equals(group.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase))
            return Error("character group belongs to another campaign", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (IsArchivedCharacterGroup(group))
            return Error("archived character group cannot be active", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var session = LoadSession(campaignId, sessionId);
        if (session == null)
            return Error("current session not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        DeactivateCharacterGroups(campaignId, session.SessionId, actor.Id);
        group.SessionId = session.SessionId ?? group.SessionId;
        group.IsActive = true;
        group.Status = CharacterGroupStatusIds.Active;
        TouchCharacterGroup(group, actor.Id);
        _repositories.CharacterGroups.Replace(group);

        session.ActiveGroupId = group.Id;
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"group.character.setActive groupId={group.Id} sessionId={session.SessionId}");
        return CharacterGroupActiveResponse(session, group, "Active character group set.");
    }

    public ResponseEnvelope GroupCharacterClearActive(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupSessionLinkEnabled())
            return CharacterGroupsDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var session = LoadSession(campaignId, sessionId);
        if (session == null)
            return Error("current session not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        DeactivateCharacterGroups(campaignId, session.SessionId, actor.Id);
        session.ActiveGroupId = string.Empty;
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"group.character.clearActive sessionId={session.SessionId}");
        return CharacterGroupActiveResponse(session, null, "Active character group cleared.");
    }

    public ResponseEnvelope GroupPlayerActiveGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupPlayerViewEnabled())
        {
            _logger.Debug($"group.player.active.get.disabled user={actor.Login}");
            return Error("character group player view is disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var session = LoadSession(campaignId, sessionId);
        if (session == null || string.IsNullOrWhiteSpace(session.ActiveGroupId))
        {
            _logger.Debug($"group.player.active.none user={actor.Login} campaignId={campaignId}");
            return Ok("No active character group.", new Dictionary<string, object>
            {
                { "hasActiveGroup", false },
                { "group", new Dictionary<string, object>() },
                { "builtAtUtc", DateTime.UtcNow }
            });
        }

        var group = _repositories.CharacterGroups.GetById(session.ActiveGroupId);
        if (group == null || IsArchivedCharacterGroup(group) || !IsGroupVisibleForPlayer(group))
            return Ok("No active character group.", new Dictionary<string, object>
            {
                { "hasActiveGroup", false },
                { "group", new Dictionary<string, object>() },
                { "builtAtUtc", DateTime.UtcNow }
            });

        return Ok("Active character group loaded.", new Dictionary<string, object>
        {
            { "hasActiveGroup", true },
            { "group", PlayerCharacterGroupPayload(group) },
            { "builtAtUtc", DateTime.UtcNow }
        });
    }

    public ResponseEnvelope GroupPlayerListVisible(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupPlayerViewEnabled())
            return Error("character group player view is disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var groups = ListCharacterGroups(campaignId, sessionId: string.Empty, includeArchived: false)
            .Where(IsGroupVisibleForPlayer)
            .Select(group => CharacterGroupListPayload(group, ListGroupMembers(group.Id, includeRemoved: false).Count(IsMemberVisibleForPlayer)))
            .Cast<object>()
            .ToArray();

        _logger.Debug($"group.player.listVisible user={actor.Login} campaignId={campaignId} count={groups.Length}");
        return Ok("Visible character groups loaded.", new Dictionary<string, object>
        {
            { "items", groups },
            { "count", groups.Length },
            { "builtAtUtc", DateTime.UtcNow }
        });
    }

    public ResponseEnvelope GroupPlayerGetVisible(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CharacterGroupPlayerViewEnabled())
            return Error("character group player view is disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var group = RequireCharacterGroup(context);
        if (!IsGroupVisibleForPlayer(group))
        {
            _logger.Debug($"group.player.getVisible.forbidden user={actor.Login} groupId={group.Id}");
            return Error("character group is not visible for player", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        return Ok("Visible character group loaded.", new Dictionary<string, object>
        {
            { "group", PlayerCharacterGroupPayload(group) },
            { "builtAtUtc", DateTime.UtcNow }
        });
    }

    private ResponseEnvelope CharacterGroupResponse(CharacterGroupState group, string message)
    {
        var members = ListGroupMembers(group.Id, includeRemoved: false);
        return Ok(message, new Dictionary<string, object>
        {
            { "group", AdminCharacterGroupPayload(group, members) },
            { "members", members.Select(AdminCharacterGroupMemberPayload).Cast<object>().ToArray() },
            { "memberCount", members.Count }
        });
    }

    private ResponseEnvelope CharacterGroupActiveResponse(CurrentSessionState session, CharacterGroupState? group, string message)
    {
        return Ok(message, new Dictionary<string, object>
        {
            { "hasActiveGroup", group != null },
            { "sessionId", session.SessionId ?? string.Empty },
            { "campaignId", session.CampaignId ?? string.Empty },
            { "activeGroupId", group?.Id ?? string.Empty },
            { "activeGroupName", group?.Name ?? string.Empty },
            { "group", group == null ? new Dictionary<string, object>() : AdminCharacterGroupPayload(group, ListGroupMembers(group.Id, includeRemoved: false)) }
        });
    }

    private CharacterGroupState RequireCharacterGroup(CommandContext context)
    {
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var groupId = CharacterGroupFirstNonEmpty(PayloadReader.GetString(payload, "groupId"), PayloadReader.GetString(payload, "id"));
        groupId = RequireLength(groupId, 1, 128, "groupId");
        var group = _repositories.CharacterGroups.GetById(groupId);
        if (group == null || group.Deleted || IsArchivedCharacterGroup(group))
            throw new InvalidOperationException("character group not found");
        return group;
    }

    private CharacterGroupMemberState RequireCharacterGroupMember(CommandContext context)
    {
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var memberId = RequireLength(PayloadReader.GetString(payload, "memberId"), 1, 128, "memberId");
        var member = _repositories.CharacterGroupMembers.GetById(memberId);
        if (member == null || member.Deleted || member.RemovedAtUtc != null)
            throw new InvalidOperationException("character group member not found");
        return member;
    }

    private IReadOnlyCollection<CharacterGroupState> ListCharacterGroups(string campaignId, string sessionId, bool includeArchived)
    {
        var filter = Builders<CharacterGroupState>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<CharacterGroupState>.Filter.Eq(x => x.Deleted, false);
        if (!includeArchived)
        {
            filter &= Builders<CharacterGroupState>.Filter.Eq(x => x.Archived, false)
                & Builders<CharacterGroupState>.Filter.Eq(x => x.IsArchived, false);
        }
        if (!string.IsNullOrWhiteSpace(sessionId))
            filter &= Builders<CharacterGroupState>.Filter.Eq(x => x.SessionId, sessionId);
        return _repositories.CharacterGroups.Find(filter);
    }

    private IReadOnlyCollection<CharacterGroupMemberState> ListGroupMembers(string groupId, bool includeRemoved)
    {
        var filter = Builders<CharacterGroupMemberState>.Filter.Eq(x => x.GroupId, groupId)
            & Builders<CharacterGroupMemberState>.Filter.Eq(x => x.Deleted, false);
        if (!includeRemoved)
            filter &= Builders<CharacterGroupMemberState>.Filter.Eq(x => x.RemovedAtUtc, null);
        return _repositories.CharacterGroupMembers.Find(filter)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void DeactivateCharacterGroups(string campaignId, string sessionId, string userId)
    {
        var groups = ListCharacterGroups(campaignId, string.Empty, includeArchived: false)
            .Where(group => group.IsActive
                && (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(group.SessionId) || string.Equals(group.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        foreach (var group in groups)
        {
            group.IsActive = false;
            if (string.Equals(group.Status, CharacterGroupStatusIds.Active, StringComparison.OrdinalIgnoreCase))
                group.Status = CharacterGroupStatusIds.Inactive;
            TouchCharacterGroup(group, userId);
            _repositories.CharacterGroups.Replace(group);
        }
    }

    private Dictionary<string, object> CharacterGroupListPayload(CharacterGroupState group, int memberCount)
        => new Dictionary<string, object>
        {
            { "groupId", group.Id },
            { "campaignId", group.CampaignId ?? string.Empty },
            { "sessionId", group.SessionId ?? string.Empty },
            { "name", group.Name ?? string.Empty },
            { "description", group.Description ?? string.Empty },
            { "groupType", group.GroupType ?? CharacterGroupTypeIds.Party },
            { "status", group.Status ?? CharacterGroupStatusIds.Draft },
            { "isActive", group.IsActive },
            { "isPlayerVisible", group.IsPlayerVisible },
            { "visibilityMode", group.VisibilityMode ?? MapVisibilityModes.Party },
            { "memberCount", memberCount },
            { "updatedAtUtc", group.UpdatedAtUtc == default ? group.UpdatedUtc : group.UpdatedAtUtc }
        };

    private Dictionary<string, object> AdminCharacterGroupPayload(CharacterGroupState group, IReadOnlyCollection<CharacterGroupMemberState> members)
        => new Dictionary<string, object>
        {
            { "groupId", group.Id },
            { "campaignId", group.CampaignId ?? string.Empty },
            { "sessionId", group.SessionId ?? string.Empty },
            { "name", group.Name ?? string.Empty },
            { "description", group.Description ?? string.Empty },
            { "groupType", group.GroupType ?? CharacterGroupTypeIds.Party },
            { "status", group.Status ?? CharacterGroupStatusIds.Draft },
            { "isActive", group.IsActive },
            { "isPlayerVisible", group.IsPlayerVisible },
            { "visibilityMode", group.VisibilityMode ?? MapVisibilityModes.Party },
            { "publicNotes", group.PublicNotes ?? string.Empty },
            { "gmNotes", group.GMNotes ?? string.Empty },
            { "memberCount", members.Count },
            { "createdAtUtc", group.CreatedAtUtc == default ? group.CreatedUtc : group.CreatedAtUtc },
            { "updatedAtUtc", group.UpdatedAtUtc == default ? group.UpdatedUtc : group.UpdatedAtUtc },
            { "members", members.Select(AdminCharacterGroupMemberPayload).Cast<object>().ToArray() },
            { "diagnosticsSummary", $"members={members.Count}; visible={members.Count(IsMemberVisibleForPlayer)}; active={group.IsActive}" }
        };

    private Dictionary<string, object> AdminCharacterGroupMemberPayload(CharacterGroupMemberState member)
        => new Dictionary<string, object>
        {
            { "memberId", member.Id },
            { "groupId", member.GroupId ?? string.Empty },
            { "campaignId", member.CampaignId ?? string.Empty },
            { "entityType", member.EntityType ?? CharacterGroupEntityTypeIds.PlayerCharacter },
            { "entityId", member.EntityId ?? string.Empty },
            { "displayName", member.DisplayName ?? string.Empty },
            { "roleInGroup", member.RoleInGroup ?? CharacterGroupRoleInGroupIds.Member },
            { "characterRole", member.CharacterRole ?? CharacterGroupCharacterRoleIds.PlayerCharacter },
            { "ownerUserId", member.OwnerUserId ?? string.Empty },
            { "controlledByUserId", member.ControlledByUserId ?? string.Empty },
            { "isLeader", member.IsLeader },
            { "isPlayerVisible", member.IsPlayerVisible },
            { "visibilityMode", member.VisibilityMode ?? MapVisibilityModes.Party },
            { "sortOrder", member.SortOrder },
            { "publicNotes", member.PublicNotes ?? string.Empty },
            { "gmNotes", member.GMNotes ?? string.Empty },
            { "joinedAtUtc", member.JoinedAtUtc == default ? member.CreatedUtc : member.JoinedAtUtc }
        };

    private Dictionary<string, object> PlayerCharacterGroupPayload(CharacterGroupState group)
    {
        var members = ListGroupMembers(group.Id, includeRemoved: false).Where(IsMemberVisibleForPlayer).ToArray();
        return new Dictionary<string, object>
        {
            { "groupId", group.Id },
            { "name", group.Name ?? string.Empty },
            { "description", group.Description ?? string.Empty },
            { "groupType", group.GroupType ?? CharacterGroupTypeIds.Party },
            { "status", group.Status ?? CharacterGroupStatusIds.Draft },
            { "publicNotes", group.PublicNotes ?? string.Empty },
            { "memberCount", members.Length },
            { "members", members.Select(PlayerCharacterGroupMemberPayload).Cast<object>().ToArray() },
            { "updatedAtUtc", group.UpdatedAtUtc == default ? group.UpdatedUtc : group.UpdatedAtUtc }
        };
    }

    private Dictionary<string, object> PlayerCharacterGroupMemberPayload(CharacterGroupMemberState member)
        => new Dictionary<string, object>
        {
            { "memberId", member.Id },
            { "displayName", member.DisplayName ?? string.Empty },
            { "entityType", member.EntityType ?? CharacterGroupEntityTypeIds.PlayerCharacter },
            { "roleInGroup", member.RoleInGroup ?? CharacterGroupRoleInGroupIds.Member },
            { "characterRole", member.CharacterRole ?? CharacterGroupCharacterRoleIds.PlayerCharacter },
            { "isLeader", member.IsLeader },
            { "publicNotes", member.PublicNotes ?? string.Empty },
            { "sortOrder", member.SortOrder }
        };

    private static bool IsArchivedCharacterGroup(CharacterGroupState group)
        => group.Archived || group.IsArchived || string.Equals(group.Status, CharacterGroupStatusIds.Archived, StringComparison.OrdinalIgnoreCase);

    private CharacterGroupState? LoadCharacterGroupById(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId)) return null;
        var group = _repositories.CharacterGroups.GetById(groupId);
        return group == null || group.Deleted || IsArchivedCharacterGroup(group) ? null : group;
    }

    private string ResolveActiveGroupName(string groupId)
        => LoadCharacterGroupById(groupId)?.Name ?? string.Empty;

    private int CountVisibleGroupMembersForSession(string groupId, bool playerSafe)
    {
        if (string.IsNullOrWhiteSpace(groupId)) return 0;
        var members = ListGroupMembers(groupId, includeRemoved: false);
        return playerSafe ? members.Count(IsMemberVisibleForPlayer) : members.Count;
    }

    private static bool IsGroupVisibleForPlayer(CharacterGroupState group)
        => group.IsPlayerVisible
            && !string.Equals(group.VisibilityMode, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(group.VisibilityMode, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase);

    private static bool IsMemberVisibleForPlayer(CharacterGroupMemberState member)
        => member.RemovedAtUtc == null
            && member.IsPlayerVisible
            && !string.Equals(member.VisibilityMode, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(member.VisibilityMode, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase);

    private void TouchCharacterGroup(CharacterGroupState group, string userId)
    {
        group.UpdatedAtUtc = DateTime.UtcNow;
        group.UpdatedByUserId = userId ?? string.Empty;
    }

    private ResponseEnvelope CharacterGroupsDisabled(string commandName)
    {
        _logger.Admin($"group.character.disabled command={commandName}");
        return Error("character groups are disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool CharacterGroupsReadEnabled()
        => _featureFlags.IsEnabled(nameof(CharacterGroupFeatureFlags.UseCharacterGroupsMvp));

    private bool CharacterGroupsWriteEnabled() => CharacterGroupsReadEnabled();

    private bool CharacterGroupMembershipEnabled()
        => CharacterGroupsReadEnabled() && _featureFlags.IsEnabled(nameof(CharacterGroupFeatureFlags.UseGroupMembershipV1));

    private bool CharacterGroupSessionLinkEnabled()
        => CharacterGroupMembershipEnabled()
            && _featureFlags.IsEnabled(nameof(CharacterGroupFeatureFlags.UseActiveGroupMvp))
            && _featureFlags.IsEnabled(nameof(CharacterGroupFeatureFlags.UseGroupSessionLink));

    private bool CharacterGroupPlayerViewEnabled()
        => CharacterGroupsReadEnabled() && _featureFlags.IsEnabled(nameof(CharacterGroupFeatureFlags.UseGroupPlayerView));

    private static string CharacterGroupFirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return string.Empty;
    }

    private static string NormalizeCharacterGroupType(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            CharacterGroupTypeIds.Party => CharacterGroupTypeIds.Party,
            CharacterGroupTypeIds.NpcGroup => CharacterGroupTypeIds.NpcGroup,
            CharacterGroupTypeIds.CompanionGroup => CharacterGroupTypeIds.CompanionGroup,
            CharacterGroupTypeIds.EnemyGroup => CharacterGroupTypeIds.EnemyGroup,
            CharacterGroupTypeIds.NeutralGroup => CharacterGroupTypeIds.NeutralGroup,
            CharacterGroupTypeIds.EscortGroup => CharacterGroupTypeIds.EscortGroup,
            CharacterGroupTypeIds.TemporaryGroup => CharacterGroupTypeIds.TemporaryGroup,
            CharacterGroupTypeIds.Custom => CharacterGroupTypeIds.Custom,
            _ => CharacterGroupTypeIds.Party
        };
    }

    private static string NormalizeCharacterGroupStatus(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            CharacterGroupStatusIds.Draft => CharacterGroupStatusIds.Draft,
            CharacterGroupStatusIds.Active => CharacterGroupStatusIds.Active,
            CharacterGroupStatusIds.Inactive => CharacterGroupStatusIds.Inactive,
            CharacterGroupStatusIds.Disbanded => CharacterGroupStatusIds.Disbanded,
            CharacterGroupStatusIds.Archived => CharacterGroupStatusIds.Archived,
            _ => CharacterGroupStatusIds.Draft
        };
    }

    private static string NormalizeCharacterGroupEntityType(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            CharacterGroupEntityTypeIds.PlayerCharacter => CharacterGroupEntityTypeIds.PlayerCharacter,
            CharacterGroupEntityTypeIds.Npc => CharacterGroupEntityTypeIds.Npc,
            CharacterGroupEntityTypeIds.Companion => CharacterGroupEntityTypeIds.Companion,
            CharacterGroupEntityTypeIds.TemporaryAlly => CharacterGroupEntityTypeIds.TemporaryAlly,
            CharacterGroupEntityTypeIds.Enemy => CharacterGroupEntityTypeIds.Enemy,
            CharacterGroupEntityTypeIds.Neutral => CharacterGroupEntityTypeIds.Neutral,
            CharacterGroupEntityTypeIds.Custom => CharacterGroupEntityTypeIds.Custom,
            _ => CharacterGroupEntityTypeIds.PlayerCharacter
        };
    }

    private static string NormalizeRoleInGroup(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            CharacterGroupRoleInGroupIds.Leader => CharacterGroupRoleInGroupIds.Leader,
            CharacterGroupRoleInGroupIds.Member => CharacterGroupRoleInGroupIds.Member,
            CharacterGroupRoleInGroupIds.Companion => CharacterGroupRoleInGroupIds.Companion,
            CharacterGroupRoleInGroupIds.Guide => CharacterGroupRoleInGroupIds.Guide,
            CharacterGroupRoleInGroupIds.Guard => CharacterGroupRoleInGroupIds.Guard,
            CharacterGroupRoleInGroupIds.Prisoner => CharacterGroupRoleInGroupIds.Prisoner,
            CharacterGroupRoleInGroupIds.Escort => CharacterGroupRoleInGroupIds.Escort,
            CharacterGroupRoleInGroupIds.Enemy => CharacterGroupRoleInGroupIds.Enemy,
            CharacterGroupRoleInGroupIds.Observer => CharacterGroupRoleInGroupIds.Observer,
            CharacterGroupRoleInGroupIds.Custom => CharacterGroupRoleInGroupIds.Custom,
            _ => CharacterGroupRoleInGroupIds.Member
        };
    }

    private static string NormalizeCharacterRole(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "playercharacter" => CharacterGroupCharacterRoleIds.PlayerCharacter,
            "player_character" => CharacterGroupCharacterRoleIds.PlayerCharacter,
            "npc" => CharacterGroupCharacterRoleIds.NPC,
            "companion" => CharacterGroupCharacterRoleIds.Companion,
            "temporaryally" => CharacterGroupCharacterRoleIds.TemporaryAlly,
            "temporary_ally" => CharacterGroupCharacterRoleIds.TemporaryAlly,
            "enemy" => CharacterGroupCharacterRoleIds.Enemy,
            "inactive" => CharacterGroupCharacterRoleIds.Inactive,
            "custom" => CharacterGroupCharacterRoleIds.Custom,
            _ => CharacterGroupCharacterRoleIds.PlayerCharacter
        };
    }

    private static string NormalizeGroupVisibility(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            MapVisibilityModes.Public => MapVisibilityModes.Public,
            MapVisibilityModes.Party => MapVisibilityModes.Party,
            MapVisibilityModes.Hidden => MapVisibilityModes.Hidden,
            MapVisibilityModes.GmOnly => MapVisibilityModes.GmOnly,
            _ => MapVisibilityModes.Party
        };
    }
}
