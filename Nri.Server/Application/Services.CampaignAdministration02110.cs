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
    public ResponseEnvelope CampaignSuperAdminCreate02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.SuperAdmin);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var name = RequireLength(PayloadReader.GetString(payload, "name"), 1, 160, "name");
        var ownerUserId = RequireLength(PayloadReader.GetString(payload, "ownerUserId"), 1, 128, "ownerUserId");
        if (_repositories.Accounts.GetById(ownerUserId) == null) throw new KeyNotFoundException("Account not found.");
        var existingCampaign = _repositories.Campaigns.GetById(campaignId);
        if (existingCampaign != null)
        {
            if (!string.Equals(existingCampaign.OwnerUserId, ownerUserId, StringComparison.Ordinal))
                throw new InvalidOperationException("Кампания уже принадлежит другому владельцу.");
            existingCampaign.Name = name;
            existingCampaign.EntityRevision++;
            _repositories.Campaigns.Replace(existingCampaign);
            WriteAudit("campaign", actor.Id, "ensure", $"{campaignId}:{ownerUserId}");
            return Ok("Кампания обновлена.", new Dictionary<string, object>
            {
                ["campaignId"] = existingCampaign.Id,
                ["name"] = existingCampaign.Name,
                ["ownerUserId"] = ownerUserId,
                ["revision"] = existingCampaign.EntityRevision
            });
        }

        var campaign = new Campaign { Id = campaignId, Name = name, OwnerUserId = ownerUserId };
        var membership = new CampaignMembership
        {
            CampaignId = campaignId,
            UserId = ownerUserId,
            PrimaryRoleId = CampaignRoleIds.OwnerGM,
            Status = CampaignMembershipStatusIds.Active,
            JoinedAtUtc = DateTime.UtcNow,
            AcceptedAtUtc = DateTime.UtcNow,
            InvitedByUserId = actor.Id
        };

        // Both identities are validated before the paired bootstrap writes.
        _repositories.Campaigns.Insert(campaign);
        _repositories.CampaignMemberships.Insert(membership);

        WriteAudit("campaign", actor.Id, "create", $"{campaignId}:{ownerUserId}");
        return Ok("Кампания создана.", new Dictionary<string, object>
        {
            ["campaignId"] = campaign.Id,
            ["name"] = campaign.Name,
            ["ownerUserId"] = ownerUserId,
            ["membershipId"] = membership.Id
        });
    }

    public ResponseEnvelope CampaignMembershipMigrateLegacy02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.SuperAdmin);
        var dryRun = !context.Request.Payload.ContainsKey("dryRun") || PayloadReader.GetBool(context.Request.Payload, "dryRun");
        var sessionGroups = _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x.CampaignId)).GroupBy(x => x.CampaignId, StringComparer.Ordinal).ToArray();
        var createdCampaigns = 0;
        var plannedMemberships = new List<Dictionary<string, object>>();
        var unresolved = new List<string>();
        foreach (var group in sessionGroups)
        {
            var campaign = _repositories.Campaigns.GetById(group.Key);
            var candidates = group.SelectMany(x => new[] { x.LeadGMUserId, x.GMUserId, x.CreatedByUserId }).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
            var ownerId = FirstNonEmpty(campaign?.OwnerUserId, candidates.Length == 1 ? candidates[0] : string.Empty);
            if (string.IsNullOrWhiteSpace(ownerId)) { unresolved.Add(group.Key); continue; }
            if (campaign == null)
            {
                createdCampaigns++;
                if (!dryRun) _repositories.Campaigns.Insert(new Campaign { Id = group.Key, Name = FirstNonEmpty(group.Select(x => x.Name).FirstOrDefault(), "Кампания"), OwnerUserId = ownerId });
            }
            foreach (var userId in candidates.Append(ownerId).Distinct(StringComparer.Ordinal))
            {
                var role = string.Equals(userId, ownerId, StringComparison.Ordinal) ? CampaignRoleIds.OwnerGM : CampaignRoleIds.CoGM;
                if (_repositories.CampaignMemberships.Find(Builders<CampaignMembership>.Filter.Eq(x => x.CampaignId, group.Key) & Builders<CampaignMembership>.Filter.Eq(x => x.UserId, userId)).Any()) continue;
                plannedMemberships.Add(new Dictionary<string, object> { ["campaignId"] = group.Key, ["userId"] = userId, ["role"] = role });
                if (!dryRun) _repositories.CampaignMemberships.Insert(new CampaignMembership { CampaignId = group.Key, UserId = userId, PrimaryRoleId = role, Status = CampaignMembershipStatusIds.Active, AcceptedAtUtc = DateTime.UtcNow, InvitedByUserId = actor.Id });
            }
        }
        WriteAudit("campaign_membership", actor.Id, dryRun ? "migration.dryRun" : "migration.apply", $"campaigns:{createdCampaigns}:memberships:{plannedMemberships.Count}:unresolved:{unresolved.Count}");
        return Ok(dryRun ? "Проверка миграции завершена без изменений." : "Миграция членства завершена.", new Dictionary<string, object>
        {
            ["dryRun"] = dryRun, ["campaignsToCreate"] = createdCampaigns,
            ["memberships"] = plannedMemberships.Cast<object>().ToArray(), ["unresolvedCampaignIds"] = unresolved.Cast<object>().ToArray(),
            ["safeToApply"] = unresolved.Count == 0
        });
    }

    public ResponseEnvelope CampaignMembershipList02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignManageMemberships);
        var items = _repositories.CampaignMemberships.Find(Builders<CampaignMembership>.Filter.Eq(x => x.CampaignId, campaignId))
            .Where(x => !x.Archived && !x.IsArchived && x.Status == CampaignMembershipStatusIds.Active)
            .OrderBy(x => x.PrimaryRoleId switch
            {
                CampaignRoleIds.OwnerGM => 0,
                CampaignRoleIds.CoGM => 1,
                CampaignRoleIds.Editor => 2,
                CampaignRoleIds.Player => 3,
                CampaignRoleIds.Observer => 4,
                _ => 5
            })
            .Select(x =>
            {
                var account = _repositories.Accounts.GetById(x.UserId);
                var profile = account == null ? null : _repositories.Profiles.GetById(account.ProfileId);
                return (object)new Dictionary<string, object>
                {
                    ["membershipId"] = x.Id,
                    ["userId"] = x.UserId,
                    ["accountName"] = FirstNonEmpty(profile?.DisplayName, account?.Login, "Неизвестный пользователь"),
                    ["login"] = account?.Login ?? string.Empty,
                    ["role"] = CampaignRoleDisplay02110(x.PrimaryRoleId),
                    ["status"] = MembershipStatusDisplay02110(x.Status),
                    ["capabilitySummary"] = CampaignRoleCapabilitySummary02110(x.PrimaryRoleId),
                    ["revision"] = x.EntityRevision
                };
            }).ToArray();
        return Ok("Участники кампании загружены.", new Dictionary<string, object> { ["members"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope CampaignMembershipUpsert02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignManageMemberships);
        var userId = RequireLength(PayloadReader.GetString(payload, "userId"), 1, 128, "userId");
        if (_repositories.Accounts.GetById(userId) == null) throw new KeyNotFoundException("Account not found.");
        var role = NormalizeCampaignRole02110(PayloadReader.GetString(payload, "role"));
        if (role == CampaignRoleIds.OwnerGM)
            _campaignAuthorization.RequireCampaignCapability(context.Session, campaignId, CampaignCapabilityIds.CampaignTransferOwnership);
        var item = _repositories.CampaignMemberships.Find(
            Builders<CampaignMembership>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<CampaignMembership>.Filter.Eq(x => x.UserId, userId)).FirstOrDefault();
        if (item == null)
        {
            item = new CampaignMembership
            {
                CampaignId = campaignId,
                UserId = userId,
                PrimaryRoleId = role,
                Status = CampaignMembershipStatusIds.Active,
                JoinedAtUtc = DateTime.UtcNow,
                AcceptedAtUtc = DateTime.UtcNow,
                InvitedByUserId = actor.Id
            };
            _repositories.CampaignMemberships.Insert(item);
        }
        else
        {
            item.PrimaryRoleId = role;
            item.Status = CampaignMembershipStatusIds.Active;
            item.IsArchived = false;
            item.Archived = false;
            item.EntityRevision++;
            _repositories.CampaignMemberships.Replace(item);
        }
        WriteAudit("campaign_membership", actor.Id, "upsert", $"{campaignId}:{userId}:{role}:{item.EntityRevision}");
        return Ok("Участник кампании сохранён.", new Dictionary<string, object> { ["membershipId"] = item.Id, ["role"] = CampaignRoleDisplay02110(item.PrimaryRoleId), ["status"] = MembershipStatusDisplay02110(item.Status) });
    }

    public ResponseEnvelope CampaignMembershipSetStatus02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignManageMemberships);
        var membershipId = RequireLength(PayloadReader.GetString(payload, "membershipId"), 1, 128, "membershipId");
        var status = NormalizeMembershipStatus02110(PayloadReader.GetString(payload, "status"));
        var item = _repositories.CampaignMemberships.GetById(membershipId);
        if (item == null || !string.Equals(item.CampaignId, campaignId, StringComparison.Ordinal)) throw new KeyNotFoundException("Membership not found.");
        if (item.PrimaryRoleId == CampaignRoleIds.OwnerGM && status != CampaignMembershipStatusIds.Active)
            throw new InvalidOperationException("Сначала передайте владение кампанией другому участнику.");
        item.Status = status;
        item.EntityRevision++;
        _repositories.CampaignMemberships.Replace(item);
        if (status != CampaignMembershipStatusIds.Active) _sessionManager.InvalidateCampaignContexts(campaignId, item.UserId);
        WriteAudit("campaign_membership", actor.Id, "status", $"{campaignId}:{item.UserId}:{status}:{item.EntityRevision}");
        return Ok("Статус участника изменён.", new Dictionary<string, object> { ["status"] = MembershipStatusDisplay02110(status), ["revision"] = item.EntityRevision });
    }

    public ResponseEnvelope CampaignOwnershipTransfer02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignTransferOwnership);
        var newOwnerUserId = RequireLength(PayloadReader.GetString(context.Request.Payload, "newOwnerUserId"), 1, 128, "newOwnerUserId");
        var reason = RequireLength(PayloadReader.GetString(context.Request.Payload, "reason"), 3, 500, "reason");
        var newOwner = _campaignAuthorization.GetMembership(newOwnerUserId, campaignId);
        if (newOwner == null) throw new InvalidOperationException("Новый владелец должен быть активным участником кампании.");
        var oldOwner = _repositories.CampaignMemberships.Find(
            Builders<CampaignMembership>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<CampaignMembership>.Filter.Eq(x => x.PrimaryRoleId, CampaignRoleIds.OwnerGM)
            & Builders<CampaignMembership>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active)).FirstOrDefault();
        if (oldOwner == null) throw new InvalidOperationException("В кампании нет активного владельца.");
        if (string.Equals(oldOwner.UserId, newOwnerUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("Новый владелец уже владеет кампанией.");
        var campaign = _repositories.Campaigns.GetById(campaignId) ?? throw new KeyNotFoundException("Campaign not found.");
        var previousNewOwnerRole = newOwner.PrimaryRoleId;
        var previousNewOwnerRevision = newOwner.EntityRevision;
        var previousOldOwnerRole = oldOwner.PrimaryRoleId;
        var previousOldOwnerRevision = oldOwner.EntityRevision;
        var previousCampaignOwner = campaign.OwnerUserId;
        var previousCampaignRevision = campaign.EntityRevision;
        try
        {
            newOwner.PrimaryRoleId = CampaignRoleIds.OwnerGM;
            newOwner.EntityRevision++;
            _repositories.CampaignMemberships.Replace(newOwner);
            oldOwner.PrimaryRoleId = CampaignRoleIds.CoGM;
            oldOwner.EntityRevision++;
            _repositories.CampaignMemberships.Replace(oldOwner);
            campaign.OwnerUserId = newOwnerUserId;
            campaign.EntityRevision++;
            _repositories.Campaigns.Replace(campaign);
        }
        catch
        {
            newOwner.PrimaryRoleId = previousNewOwnerRole;
            newOwner.EntityRevision = previousNewOwnerRevision;
            oldOwner.PrimaryRoleId = previousOldOwnerRole;
            oldOwner.EntityRevision = previousOldOwnerRevision;
            campaign.OwnerUserId = previousCampaignOwner;
            campaign.EntityRevision = previousCampaignRevision;
            try { _repositories.CampaignMemberships.Replace(newOwner); } catch { }
            try { _repositories.CampaignMemberships.Replace(oldOwner); } catch { }
            try { _repositories.Campaigns.Replace(campaign); } catch { }
            throw;
        }
        WriteAudit("campaign_ownership", actor.Id, "transfer", $"{campaignId}:{oldOwner.UserId}:{newOwnerUserId}:{reason}");
        return Ok("Владение кампанией передано.", new Dictionary<string, object> { ["newOwner"] = GetAccountLogin(newOwnerUserId), ["previousOwner"] = GetAccountLogin(oldOwner.UserId) });
    }

    public ResponseEnvelope SessionParticipationList02110(CommandContext context)
    {
        var session = RequireScopedSession02110(context, CampaignCapabilityIds.SessionManageParticipants);
        var items = _repositories.SessionParticipations.Find(Builders<SessionParticipation>.Filter.Eq(x => x.SessionId, session.SessionId))
            .Select(x => (object)new Dictionary<string, object>
            {
                ["participationId"] = x.Id,
                ["accountName"] = GetAccountLogin(x.UserId),
                ["role"] = ParticipationRoleDisplay02110(x.ParticipationRoleId),
                ["status"] = MembershipStatusDisplay02110(x.Status),
                ["characterCount"] = x.AllowedCharacterIds.Count,
                ["revision"] = x.EntityRevision
            }).ToArray();
        return Ok("Участники сессии загружены.", new Dictionary<string, object> { ["participants"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope SessionParticipationUpsert02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var session = RequireScopedSession02110(context, CampaignCapabilityIds.SessionManageParticipants);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var userId = RequireLength(PayloadReader.GetString(payload, "userId"), 1, 128, "userId");
        if (_campaignAuthorization.GetMembership(userId, session.CampaignId) == null) throw new InvalidOperationException("Пользователь не состоит в кампании.");
        var role = NormalizeParticipationRole02110(PayloadReader.GetString(payload, "role"));
        var allowedCharacterIds = (PayloadReader.GetList(payload, "allowedCharacterIds") ?? new List<object>())
            .Select(x => Convert.ToString(x)?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var characterId in allowedCharacterIds)
        {
            var ownership = _repositories.CharacterOwnerships.Find(
                Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)
                & Builders<CharacterOwnershipState>.Filter.Eq(x => x.CampaignId, session.CampaignId)
                & Builders<CharacterOwnershipState>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();
            if (ownership == null) throw new ArgumentException("Персонаж не принадлежит выбранной кампании или недоступен.");
        }
        var item = _repositories.SessionParticipations.Find(
            Builders<SessionParticipation>.Filter.Eq(x => x.SessionId, session.SessionId)
            & Builders<SessionParticipation>.Filter.Eq(x => x.UserId, userId)).FirstOrDefault();
        if (item == null)
        {
            item = new SessionParticipation
            {
                CampaignId = session.CampaignId,
                SessionId = session.SessionId,
                UserId = userId,
                ParticipationRoleId = role,
                AllowedCharacterIds = allowedCharacterIds
            };
            _repositories.SessionParticipations.Insert(item);
        }
        else
        {
            item.ParticipationRoleId = role;
            item.AllowedCharacterIds = allowedCharacterIds;
            if (allowedCharacterIds.Count > 0 && !allowedCharacterIds.Contains(item.ActiveCharacterId))
                item.ActiveCharacterId = string.Empty;
            item.Status = CampaignMembershipStatusIds.Active;
            item.EntityRevision++;
            _repositories.SessionParticipations.Replace(item);
        }
        WriteAudit("session_participation", actor.Id, "upsert", $"{session.SessionId}:{userId}:{role}");
        return Ok("Участник сессии сохранён.", new Dictionary<string, object>
        {
            ["role"] = ParticipationRoleDisplay02110(role),
            ["allowedCharacterCount"] = item.AllowedCharacterIds.Count,
            ["revision"] = item.EntityRevision
        });
    }

    private CurrentSessionState RequireScopedSession02110(CommandContext context, string capability)
    {
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var session = _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault();
        if (session == null) throw new KeyNotFoundException("Session not found.");
        if (!string.Equals(context.Session!.GameContext.CampaignId, session.CampaignId, StringComparison.Ordinal)) throw new KeyNotFoundException("Session not found.");
        _campaignAuthorization.RequireSessionCapability(context.Session, session, capability);
        return session;
    }

    private static string NormalizeCampaignRole02110(string role) => role?.Trim().ToLowerInvariant() switch
    {
        "owner_gm" => CampaignRoleIds.OwnerGM, "co_gm" => CampaignRoleIds.CoGM,
        "editor" => CampaignRoleIds.Editor, "player" => CampaignRoleIds.Player,
        "observer" => CampaignRoleIds.Observer, _ => throw new ArgumentException("Неизвестная роль кампании.")
    };

    private static string NormalizeMembershipStatus02110(string status) => status?.Trim().ToLowerInvariant() switch
    {
        "active" => CampaignMembershipStatusIds.Active, "suspended" => CampaignMembershipStatusIds.Suspended,
        "left" => CampaignMembershipStatusIds.Left, "removed" => CampaignMembershipStatusIds.Removed,
        _ => throw new ArgumentException("Неизвестный статус участника.")
    };

    private static string NormalizeParticipationRole02110(string role) => role?.Trim().ToLowerInvariant() switch
    {
        "lead_gm" => SessionParticipationRoleIds.LeadGM, "assistant_gm" => SessionParticipationRoleIds.AssistantGM,
        "player" => SessionParticipationRoleIds.Player, "observer" => SessionParticipationRoleIds.Observer,
        _ => throw new ArgumentException("Неизвестная роль участника сессии.")
    };

    private static string MembershipStatusDisplay02110(string status) => status switch
    {
        CampaignMembershipStatusIds.Active => "Активен", CampaignMembershipStatusIds.Suspended => "Приостановлен",
        CampaignMembershipStatusIds.Left => "Покинул кампанию", CampaignMembershipStatusIds.Removed => "Удалён",
        CampaignMembershipStatusIds.Invited => "Приглашён", _ => "В архиве"
    };

    private static string ParticipationRoleDisplay02110(string role) => role switch
    {
        SessionParticipationRoleIds.LeadGM => "Ведущий GM", SessionParticipationRoleIds.AssistantGM => "Помощник GM",
        SessionParticipationRoleIds.Player => "Игрок", SessionParticipationRoleIds.Observer => "Наблюдатель", _ => "Участник"
    };

    private static string CampaignRoleCapabilitySummary02110(string role) => role switch
    {
        CampaignRoleIds.OwnerGM => "Проводить сессии · видеть мастерские данные · редактировать контент · управлять участниками",
        CampaignRoleIds.CoGM => "Проводить сессии · видеть мастерские данные · редактировать разрешённый контент",
        CampaignRoleIds.Editor => "Редактировать контент кампании · просматривать доступные материалы",
        CampaignRoleIds.Player => "Участвовать в сессиях · управлять своими персонажами · видеть открытые материалы",
        CampaignRoleIds.Observer => "Наблюдать за открытыми материалами и доступными сессиями",
        _ => "Права определяются ролью кампании"
    };
}
