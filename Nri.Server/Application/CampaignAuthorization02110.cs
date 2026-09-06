using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface ICampaignAuthorizationService
{
    CampaignMembership? GetMembership(string userId, string campaignId);
    IReadOnlyCollection<string> GetEffectiveCapabilities(string userId, string campaignId);
    bool CanAccessCampaign(AuthSession authSession, string campaignId);
    void RequireCampaignCapability(AuthSession authSession, string campaignId, string capabilityId);
    void RequireSessionCapability(AuthSession authSession, CurrentSessionState session, string capabilityId);
    void RequireEntityScope(AuthSession authSession, string authoritativeCampaignId, string capabilityId);
    bool CanViewGMData(AuthSession authSession, string campaignId);
    void RequireSuperAdminOverride(AuthSession authSession, UserAccount actor, string campaignId);
}

public sealed class CampaignAuthorizationService02110 : ICampaignAuthorizationService
{
    private readonly INriRepositoryFactory _repositories;

    private static readonly IReadOnlyDictionary<string, string[]> DefaultRoleCapabilities =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [CampaignRoleIds.OwnerGM] = new[]
            {
                CampaignCapabilityIds.CampaignView, CampaignCapabilityIds.CampaignViewGMData,
                CampaignCapabilityIds.CampaignManageSettings, CampaignCapabilityIds.CampaignManageMemberships,
                CampaignCapabilityIds.CampaignTransferOwnership, CampaignCapabilityIds.CampaignEditContent,
                CampaignCapabilityIds.CampaignViewAudit, CampaignCapabilityIds.SessionView,
                CampaignCapabilityIds.SessionCreate, CampaignCapabilityIds.SessionRun,
                CampaignCapabilityIds.SessionEdit, CampaignCapabilityIds.SessionManageParticipants,
                CampaignCapabilityIds.SessionViewGMData, CampaignCapabilityIds.CharacterViewPlayerSafe,
                CampaignCapabilityIds.CharacterManageOwned, CampaignCapabilityIds.CharacterManageAnyInCampaign,
                CampaignCapabilityIds.MapViewGM, CampaignCapabilityIds.MapEdit, CampaignCapabilityIds.CombatRun,
                CampaignCapabilityIds.TravelRun, CampaignCapabilityIds.WeatherRun,
                CampaignCapabilityIds.AutomationView, CampaignCapabilityIds.AutomationManage,
                CampaignCapabilityIds.AutomationApprove, CampaignCapabilityIds.ReferenceDataEditCampaignBound
            },
            [CampaignRoleIds.CoGM] = new[]
            {
                CampaignCapabilityIds.CampaignView, CampaignCapabilityIds.CampaignViewGMData,
                CampaignCapabilityIds.CampaignEditContent, CampaignCapabilityIds.CampaignViewAudit,
                CampaignCapabilityIds.SessionView, CampaignCapabilityIds.SessionCreate,
                CampaignCapabilityIds.SessionRun, CampaignCapabilityIds.SessionEdit,
                CampaignCapabilityIds.SessionManageParticipants, CampaignCapabilityIds.SessionViewGMData,
                CampaignCapabilityIds.CharacterViewPlayerSafe, CampaignCapabilityIds.CharacterManageAnyInCampaign,
                CampaignCapabilityIds.MapViewGM, CampaignCapabilityIds.MapEdit, CampaignCapabilityIds.CombatRun,
                CampaignCapabilityIds.TravelRun, CampaignCapabilityIds.WeatherRun,
                CampaignCapabilityIds.AutomationView, CampaignCapabilityIds.AutomationManage,
                CampaignCapabilityIds.AutomationApprove, CampaignCapabilityIds.ReferenceDataEditCampaignBound
            },
            [CampaignRoleIds.Editor] = new[]
            {
                CampaignCapabilityIds.CampaignView, CampaignCapabilityIds.CampaignEditContent,
                CampaignCapabilityIds.SessionView, CampaignCapabilityIds.CharacterViewPlayerSafe,
                CampaignCapabilityIds.ReferenceDataEditCampaignBound
            },
            [CampaignRoleIds.Player] = new[]
            {
                CampaignCapabilityIds.CampaignView, CampaignCapabilityIds.SessionView,
                CampaignCapabilityIds.CharacterViewPlayerSafe, CampaignCapabilityIds.CharacterManageOwned
            },
            [CampaignRoleIds.Observer] = new[]
            {
                CampaignCapabilityIds.CampaignView, CampaignCapabilityIds.SessionView,
                CampaignCapabilityIds.CharacterViewPlayerSafe
            }
        };

    public CampaignAuthorizationService02110(INriRepositoryFactory repositories)
    {
        _repositories = repositories;
    }

    public CampaignMembership? GetMembership(string userId, string campaignId)
        => _repositories.CampaignMemberships.Find(
            Builders<CampaignMembership>.Filter.Eq(x => x.UserId, userId)
            & Builders<CampaignMembership>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<CampaignMembership>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active)
            & Builders<CampaignMembership>.Filter.Eq(x => x.IsArchived, false)
            & Builders<CampaignMembership>.Filter.Eq(x => x.Archived, false)).FirstOrDefault();

    public IReadOnlyCollection<string> GetEffectiveCapabilities(string userId, string campaignId)
    {
        var membership = GetMembership(userId, campaignId);
        if (membership == null) return new HashSet<string>(StringComparer.Ordinal);

        var result = new HashSet<string>(StringComparer.Ordinal);
        AddRole(result, membership.PrimaryRoleId);
        foreach (var role in membership.AdditionalRoleIds) AddRole(result, role);
        foreach (var grant in membership.CapabilityGrants) result.Add(grant);
        foreach (var denial in membership.CapabilityDenials) result.Remove(denial);
        return result;
    }

    public bool CanAccessCampaign(AuthSession authSession, string campaignId)
        => IsExplicitOverride(authSession, campaignId)
           || GetEffectiveCapabilities(authSession.UserId, campaignId).Contains(CampaignCapabilityIds.CampaignView);

    public void RequireCampaignCapability(AuthSession authSession, string campaignId, string capabilityId)
    {
        if (authSession == null || string.IsNullOrWhiteSpace(campaignId)) throw new UnauthorizedAccessException("Campaign access is unavailable.");
        if (IsExplicitOverride(authSession, campaignId)) return;
        if (!GetEffectiveCapabilities(authSession.UserId, campaignId).Contains(capabilityId))
            throw new UnauthorizedAccessException("Campaign access is unavailable.");
    }

    public void RequireSessionCapability(AuthSession authSession, CurrentSessionState session, string capabilityId)
    {
        if (session == null) throw new KeyNotFoundException("Session not found.");
        RequireCampaignCapability(authSession, session.CampaignId, capabilityId);
        var participation = _repositories.SessionParticipations.Find(
            Builders<SessionParticipation>.Filter.Eq(x => x.SessionId, session.SessionId)
            & Builders<SessionParticipation>.Filter.Eq(x => x.UserId, authSession.UserId)
            & Builders<SessionParticipation>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active)).FirstOrDefault();
        if (participation == null && !CanViewGMData(authSession, session.CampaignId))
            throw new UnauthorizedAccessException("Session access is unavailable.");
        if (participation?.CapabilityDenials.Contains(capabilityId) == true)
            throw new UnauthorizedAccessException("Session access is unavailable.");
    }

    public void RequireEntityScope(AuthSession authSession, string authoritativeCampaignId, string capabilityId)
        => RequireCampaignCapability(authSession, authoritativeCampaignId, capabilityId);

    public bool CanViewGMData(AuthSession authSession, string campaignId)
        => IsExplicitOverride(authSession, campaignId)
           || GetEffectiveCapabilities(authSession.UserId, campaignId).Contains(CampaignCapabilityIds.CampaignViewGMData);

    public void RequireSuperAdminOverride(AuthSession authSession, UserAccount actor, string campaignId)
    {
        if (!actor.Roles.Contains(UserRole.SuperAdmin)
            || !authSession.GameContext.SuperAdminOverrideActive
            || !string.Equals(authSession.GameContext.CampaignId, campaignId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(authSession.GameContext.SuperAdminOverrideReason))
            throw new UnauthorizedAccessException("Explicit SuperAdmin override is required.");
    }

    private static bool IsExplicitOverride(AuthSession authSession, string campaignId)
        => authSession.GameContext.SuperAdminOverrideActive
           && string.Equals(authSession.GameContext.CampaignId, campaignId, StringComparison.Ordinal)
           && !string.IsNullOrWhiteSpace(authSession.GameContext.SuperAdminOverrideReason);

    private static void AddRole(ISet<string> target, string roleId)
    {
        if (!DefaultRoleCapabilities.TryGetValue(roleId ?? string.Empty, out var capabilities)) return;
        foreach (var capability in capabilities) target.Add(capability);
    }
}
