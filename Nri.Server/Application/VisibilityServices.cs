using System;
using System.Collections.Generic;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public sealed class VisibilityContext
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ActiveCharacterId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public bool IsSuperAdmin { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsPlayer { get; set; }
    public bool IsObserver { get; set; }
}

public sealed class VisibilityFilterResult
{
    public bool IsVisible { get; set; }
    public string AppliedRule { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public interface IVisibilityService
{
    bool CanSee(string visibilityRule, VisibilityContext context);
    bool CanSeeServerOnly(VisibilityContext context);
    bool CanSeeGmOnly(VisibilityContext context);
    Dictionary<string, object> FilterDefinitionPayload(Dictionary<string, object> payload, VisibilityContext context, string entityType, string entityId);
    VisibilityContext BuildContextFromCommand(CommandContext context, UserAccount actor);
}

public sealed class VisibilityService : IVisibilityService
{
    private readonly IServerLogger _logger;
    public VisibilityService(IServerLogger logger) { _logger = logger; }

    public bool CanSee(string visibilityRule, VisibilityContext context)
    {
        var rule = string.IsNullOrWhiteSpace(visibilityRule) ? VisibilityRuleIds.Public : visibilityRule;
        var normalized = rule.Trim().ToLowerInvariant();
        var result = false;
        switch (normalized)
        {
            case "public": result = true; break;
            case "player_visible": result = context.IsPlayer || context.IsAdmin || context.IsSuperAdmin; break;
            case "character_known":
            case "party_known": result = context.IsAdmin || context.IsSuperAdmin || (!string.IsNullOrWhiteSpace(context.ActiveCharacterId) && context.IsPlayer); break;
            case "faction_known": result = context.IsAdmin || context.IsSuperAdmin; break;
            case "owner_known": result = context.IsAdmin || context.IsSuperAdmin; break;
            case "gm_only": result = CanSeeGmOnly(context); break;
            case "super_admin_only": result = context.IsSuperAdmin; break;
            case "server_only": result = CanSeeServerOnly(context); break;
            case "hidden_until_discovered": result = context.IsAdmin || context.IsSuperAdmin; break;
            default: result = context.IsAdmin || context.IsSuperAdmin; break;
        }

        _logger.Debug($"visibility.check rule={rule} role={context.Role} result={result}");
        return result;
    }

    public bool CanSeeServerOnly(VisibilityContext context) => context.IsSuperAdmin;
    public bool CanSeeGmOnly(VisibilityContext context) => context.IsAdmin || context.IsSuperAdmin;

    public Dictionary<string, object> FilterDefinitionPayload(Dictionary<string, object> payload, VisibilityContext context, string entityType, string entityId)
    {
        if (payload == null) return new Dictionary<string, object>();
        var result = new Dictionary<string, object>(payload, StringComparer.OrdinalIgnoreCase);
        var rule = result.ContainsKey("visibilityRule") ? Convert.ToString(result["visibilityRule"]) ?? VisibilityRuleIds.Public : VisibilityRuleIds.Public;
        if (!CanSee(rule, context))
        {
            _logger.Debug($"visibility.denied entityType={entityType} entityId={entityId} rule={rule} role={context.Role}");
            return null;
        }

        var removed = new List<string>();
        if (!CanSeeGmOnly(context) && result.ContainsKey("gmDescription")) { result.Remove("gmDescription"); removed.Add("GMDescription"); }
        if (!CanSeeServerOnly(context))
        {
            if (result.ContainsKey("serverOnlyData")) { result.Remove("serverOnlyData"); removed.Add("ServerOnlyData"); }
            if (result.ContainsKey("additionalData") && result["additionalData"] is Dictionary<string, object> nested && nested.ContainsKey("serverOnlyData"))
            {
                nested.Remove("serverOnlyData");
                removed.Add("ServerOnlyData");
            }
        }
        if (removed.Count > 0) _logger.Debug($"visibility.filtered entityType={entityType} entityId={entityId} removed={string.Join(",", removed.ToArray())}");
        return result;
    }

    public VisibilityContext BuildContextFromCommand(CommandContext context, UserAccount actor)
    {
        var role = actor.Roles.Contains(UserRole.SuperAdmin) ? "SuperAdmin" : actor.Roles.Contains(UserRole.Admin) ? "Admin" : actor.Roles.Contains(UserRole.Observer) ? "Observer" : "Player";
        return new VisibilityContext
        {
            UserId = actor.Id,
            Role = role,
            // TODO Foundation 0.5: resolve active character from UserProfile/session when profile-based character context is introduced.
            ActiveCharacterId = string.Empty,
            CampaignId = string.Empty,
            SessionId = context.ConnectionId ?? string.Empty,
            IsSuperAdmin = actor.Roles.Contains(UserRole.SuperAdmin),
            IsAdmin = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin),
            IsPlayer = actor.Roles.Contains(UserRole.Player),
            IsObserver = actor.Roles.Contains(UserRole.Observer)
        };
    }
}

public static class VisibilityFeatureFlags
{
    public const bool UseDefinitionVisibilityFilter = false;
}
