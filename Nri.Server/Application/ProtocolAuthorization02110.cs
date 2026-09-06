using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public enum ProtocolAuthorizationClass02110
{
    PublicUnauthenticated,
    AuthenticatedGlobal,
    SystemAdmin,
    CampaignScopedRead,
    CampaignScopedMutation,
    SessionScopedRead,
    SessionScopedMutation,
    PlayerOwnedCharacter,
    SuperAdminOnly
}

public enum ProtocolScopeKind02110
{
    Global,
    ActiveCampaign,
    Campaign,
    Session,
    Character,
    Map,
    Combat,
    Request,
    Project,
    Entity
}

public sealed class ProtocolAuthorizationDescriptor02110
{
    public string CommandName { get; set; } = string.Empty;
    public string HandlerIdentity { get; set; } = string.Empty;
    public ProtocolAuthorizationClass02110 AuthorizationClass { get; set; }
    public ProtocolScopeKind02110 ScopeKind { get; set; }
    public string ScopeResolverId { get; set; } = string.Empty;
    public string RequiredCapability { get; set; } = string.Empty;
    public string PlayerOwnershipPolicy { get; set; } = "none";
    public string ContextPolicy { get; set; } = "none";
    public string VisibilityPolicy { get; set; } = "server_projection";
    public string SuperAdminPolicy { get; set; } = "explicit_override_only";
    public string SecurityTestGroup { get; set; } = string.Empty;
    public string AliasOf { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public interface IIdentifiedCommandHandler02110
{
    string HandlerIdentity { get; }
}

public sealed class ProtocolAuthorizationCatalog02110
{
    private static readonly HashSet<string> GlobalSystemRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "auth", "backup", "backups", "dataPortability", "devAccess", "featureFlags", "lock", "system", "update"
    };

    private static readonly HashSet<string> GlobalDefinitionRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "catalog", "contentDefinition", "definitionPack", "definitions", "reference"
    };

    private readonly Dictionary<string, ProtocolAuthorizationDescriptor02110> _items =
        new Dictionary<string, ProtocolAuthorizationDescriptor02110>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _canonicalByHandler =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyCollection<ProtocolAuthorizationDescriptor02110> Items => _items.Values.ToArray();

    public ProtocolAuthorizationDescriptor02110 Register(string command, string handlerIdentity)
    {
        if (string.IsNullOrWhiteSpace(command)) throw new InvalidOperationException("A protocol command cannot be registered without a name.");
        if (string.IsNullOrWhiteSpace(handlerIdentity)) throw new InvalidOperationException($"Command '{command}' has no stable handler identity.");

        var descriptor = Classify(command, handlerIdentity);
        if (_canonicalByHandler.TryGetValue(handlerIdentity, out var canonical)
            && _items.TryGetValue(canonical, out var canonicalDescriptor)
            && HasSameSecuritySemantics(canonicalDescriptor, descriptor))
            descriptor.AliasOf = canonical;
        else
            _canonicalByHandler[handlerIdentity] = command;

        _items[command] = descriptor;
        return descriptor;
    }

    public ProtocolAuthorizationDescriptor02110 GetRequired(string command)
    {
        if (!_items.TryGetValue(command ?? string.Empty, out var descriptor))
            throw new InvalidOperationException($"Protocol command '{command}' has no authorization descriptor.");
        return descriptor;
    }

    public void ValidateComplete(IEnumerable<string> registeredCommands)
    {
        var missing = registeredCommands.Where(x => !_items.ContainsKey(x)).OrderBy(x => x).ToArray();
        var invalid = _items.Values.Where(x => string.IsNullOrWhiteSpace(x.SecurityTestGroup)
                                               || string.IsNullOrWhiteSpace(x.ScopeResolverId)
                                               || string.IsNullOrWhiteSpace(x.HandlerIdentity)).ToArray();
        if (missing.Length > 0 || invalid.Length > 0)
            throw new InvalidOperationException($"Protocol authorization catalog is incomplete. Missing={missing.Length}; invalid={invalid.Length}.");
    }

    private static ProtocolAuthorizationDescriptor02110 Classify(string command, string handlerIdentity)
    {
        var root = command.Split('.')[0];
        var lower = command.ToLowerInvariant();
        var isRead = IsReadCommand(lower);
        var isPlayerCommand = lower.StartsWith("player.", StringComparison.Ordinal)
                              || lower.Contains(".player.")
                              || lower.StartsWith("character.self", StringComparison.Ordinal)
                              || lower.StartsWith("character.player", StringComparison.Ordinal);
        var isCharacterFamily = root.Equals("character", StringComparison.OrdinalIgnoreCase)
                                         || root.Equals("actor", StringComparison.OrdinalIgnoreCase)
                                         || root.Equals("inventory", StringComparison.OrdinalIgnoreCase)
                                         || root.Equals("skills", StringComparison.OrdinalIgnoreCase)
                                         || root.Equals("classTree", StringComparison.OrdinalIgnoreCase)
                                         || root.Equals("development", StringComparison.OrdinalIgnoreCase)
                                         || root.Equals("progression", StringComparison.OrdinalIgnoreCase)
                                         || root.Equals("rest", StringComparison.OrdinalIgnoreCase);
        var isPlayerOwnedCharacter = isCharacterFamily
                                     && !lower.Contains(".admin.")
                                     && !lower.StartsWith("admin.", StringComparison.Ordinal)
                                     && !lower.Contains("definition");

        if (string.Equals(command, "auth.register", StringComparison.Ordinal)
            || string.Equals(command, "auth.login", StringComparison.Ordinal))
            return Create(command, handlerIdentity, ProtocolAuthorizationClass02110.PublicUnauthenticated,
                ProtocolScopeKind02110.Global, "global", string.Empty, "Auth/System");

        if (lower.Contains("superadmin"))
            return Create(command, handlerIdentity, ProtocolAuthorizationClass02110.SuperAdminOnly,
                ProtocolScopeKind02110.Global, "superadmin", string.Empty, "Campaign Administration");

        if (lower == "campaign.membership.migratelegacy")
            return Create(command, handlerIdentity, ProtocolAuthorizationClass02110.SystemAdmin,
                ProtocolScopeKind02110.Global, "global_migration", string.Empty, "Campaign Administration");

        if (lower is "admin.dashboard.get" or "admin.activeprocesses.list" or "admin.nextactions.list"
            or "admin.character.progress.recalculate" or "admin.classtree.setstate" or "admin.skills.setstate")
        {
            var adminScope = lower.Contains("character") || lower.Contains("classtree") || lower.Contains("skills")
                ? ProtocolScopeKind02110.Character : ProtocolScopeKind02110.ActiveCampaign;
            var read = IsReadCommand(lower);
            return Create(command, handlerIdentity,
                read ? ProtocolAuthorizationClass02110.CampaignScopedRead : ProtocolAuthorizationClass02110.CampaignScopedMutation,
                adminScope, ResolverId(adminScope), read ? CampaignCapabilityIds.CampaignViewGMData : CampaignCapabilityIds.CharacterManageAnyInCampaign,
                lower.Contains("character") || lower.Contains("classtree") || lower.Contains("skills") ? "Character Admin" : "Dashboard");
        }

        if (lower is "system.featureflags.snapshot" or "session.validate"
            || root.Equals("profile", StringComparison.OrdinalIgnoreCase)
            || root.Equals("context", StringComparison.OrdinalIgnoreCase)
            || root.Equals("presence", StringComparison.OrdinalIgnoreCase))
            return Create(command, handlerIdentity, ProtocolAuthorizationClass02110.AuthenticatedGlobal,
                ProtocolScopeKind02110.Global, "global", string.Empty, "Auth/System");

        if (lower == "search.admin.diagnostics")
            return Create(command, handlerIdentity, ProtocolAuthorizationClass02110.SystemAdmin,
                ProtocolScopeKind02110.Global, "global", string.Empty, "Diagnostics/System");

        if (lower == "character.player.assigned.list")
        {
            var assignedListDescriptor = Create(command, handlerIdentity,
                ProtocolAuthorizationClass02110.CampaignScopedRead,
                ProtocolScopeKind02110.Campaign, "campaign_payload",
                CampaignCapabilityIds.CharacterViewPlayerSafe, "Character Player");
            assignedListDescriptor.PlayerOwnershipPolicy = "server_filtered_owner_or_controller_list";
            assignedListDescriptor.ContextPolicy = "selected_campaign_must_match_payload";
            assignedListDescriptor.Notes = "Campaign-scoped list; the handler filters ownership, visibility and archive state server-side.";
            return assignedListDescriptor;
        }

        if (root.Equals("characterCreation", StringComparison.OrdinalIgnoreCase))
        {
            var isAdminOperation = lower.IndexOf(".admin.", StringComparison.Ordinal) >= 0
                                   || lower.EndsWith(".policy.update", StringComparison.Ordinal)
                                   || lower.EndsWith(".finalize", StringComparison.Ordinal);
            var creationRead = IsReadCommand(lower);
            var creationDescriptor = Create(command, handlerIdentity,
                creationRead ? ProtocolAuthorizationClass02110.CampaignScopedRead : ProtocolAuthorizationClass02110.CampaignScopedMutation,
                ProtocolScopeKind02110.ActiveCampaign, "active_context",
                isAdminOperation
                    ? CampaignCapabilityIds.CharacterManageAnyInCampaign
                    : creationRead
                        ? CampaignCapabilityIds.CharacterViewPlayerSafe
                        : CampaignCapabilityIds.CharacterManageOwned,
                isAdminOperation ? "Character Admin" : "Character Player");
            creationDescriptor.PlayerOwnershipPolicy = isAdminOperation ? "gm_campaign_scope" : "draft_owner_enforced_by_handler";
            creationDescriptor.ContextPolicy = "selected_campaign_must_match_payload";
            creationDescriptor.Notes = "Character creation drafts are campaign-scoped; draft ownership and finalization authority are enforced server-side.";
            return creationDescriptor;
        }

        if (lower == "language.comprehension.evaluate")
        {
            var languageDescriptor = Create(command, handlerIdentity,
                ProtocolAuthorizationClass02110.PlayerOwnedCharacter,
                ProtocolScopeKind02110.Character, "character_payload",
                CampaignCapabilityIds.CharacterViewPlayerSafe, "Character Player");
            languageDescriptor.PlayerOwnershipPolicy = "owned_or_session_eligible_character";
            languageDescriptor.ContextPolicy = "selected_campaign_must_match_character";
            languageDescriptor.Notes = "Player-safe comprehension evaluation reads the authoritative Character v2 language level.";
            return languageDescriptor;
        }

        if (GlobalSystemRoots.Contains(root))
        {
            var authClass = lower is "auth.logout" or "auth.changepassword" or "session.validate"
                ? ProtocolAuthorizationClass02110.AuthenticatedGlobal
                : ProtocolAuthorizationClass02110.SystemAdmin;
            var systemGroup = lower.StartsWith("admin.account", StringComparison.Ordinal)
                              || lower.StartsWith("admin.accounts", StringComparison.Ordinal)
                              || lower.StartsWith("devaccess.admin.resetknownaccounts", StringComparison.Ordinal)
                ? "Account Admin"
                : root.Equals("auth", StringComparison.OrdinalIgnoreCase) ? "Auth/System" : "Diagnostics/System";
            return Create(command, handlerIdentity, authClass, ProtocolScopeKind02110.Global,
                "global", string.Empty, systemGroup);
        }

        if (GlobalDefinitionRoots.Contains(root))
            return Create(command, handlerIdentity,
                isRead && !lower.Contains("admin") ? ProtocolAuthorizationClass02110.AuthenticatedGlobal : ProtocolAuthorizationClass02110.SystemAdmin,
                ProtocolScopeKind02110.Global, "global_definition", string.Empty, "Definitions/reference data");

        if (root.Equals("gameContext", StringComparison.OrdinalIgnoreCase))
        {
            var direct = lower.Contains("selectcampaign") ? ProtocolScopeKind02110.Campaign : ProtocolScopeKind02110.ActiveCampaign;
            return Create(command, handlerIdentity,
                lower.EndsWith(".get") || lower.EndsWith(".list") || lower.Contains("restore")
                    ? ProtocolAuthorizationClass02110.AuthenticatedGlobal
                    : ProtocolAuthorizationClass02110.CampaignScopedMutation,
                direct, direct == ProtocolScopeKind02110.Campaign ? "campaign_payload" : "active_context",
                CampaignCapabilityIds.CampaignView, "GameContext");
        }

        if (lower.StartsWith("gm.note", StringComparison.Ordinal)
            || lower.StartsWith("gm.notes", StringComparison.Ordinal))
        {
            return Create(command, handlerIdentity,
                isRead ? ProtocolAuthorizationClass02110.CampaignScopedRead : ProtocolAuthorizationClass02110.CampaignScopedMutation,
                ProtocolScopeKind02110.ActiveCampaign, "active_context",
                isRead ? CampaignCapabilityIds.CampaignViewGMData : CampaignCapabilityIds.CampaignEditContent,
                "GM Notes");
        }

        if (lower == "gameplay.admin.getresolutionqueue" || lower == "gameplay.admin.resolvequeueitem")
        {
            var queueRead = lower.EndsWith("getresolutionqueue", StringComparison.Ordinal);
            return Create(command, handlerIdentity,
                queueRead ? ProtocolAuthorizationClass02110.CampaignScopedRead : ProtocolAuthorizationClass02110.CampaignScopedMutation,
                ProtocolScopeKind02110.ActiveCampaign, "active_context",
                queueRead ? CampaignCapabilityIds.CampaignViewGMData : CampaignCapabilityIds.AutomationApprove,
                "Automation");
        }

        var scope = ResolveScope(root, lower);
        var group = ResolveSecurityGroup(root, lower);
        var capability = ResolveCapability(scope, lower, isPlayerOwnedCharacter || isPlayerCommand, isRead);
        var auth = isPlayerOwnedCharacter
            ? ProtocolAuthorizationClass02110.PlayerOwnedCharacter
            : scope == ProtocolScopeKind02110.Session
                ? (isRead ? ProtocolAuthorizationClass02110.SessionScopedRead : ProtocolAuthorizationClass02110.SessionScopedMutation)
                : (isRead ? ProtocolAuthorizationClass02110.CampaignScopedRead : ProtocolAuthorizationClass02110.CampaignScopedMutation);
        var descriptor = Create(command, handlerIdentity, auth, scope, ResolverId(scope), capability, group);
        descriptor.PlayerOwnershipPolicy = isPlayerOwnedCharacter ? "owned_or_session_eligible_character" : "none";
        descriptor.ContextPolicy = scope == ProtocolScopeKind02110.Global ? "none" : "selected_context_revision_when_supplied";
        descriptor.Notes = "Family-classified; authoritative scope resolver executes before the handler.";
        return descriptor;
    }

    private static ProtocolAuthorizationDescriptor02110 Create(
        string command, string handler, ProtocolAuthorizationClass02110 auth,
        ProtocolScopeKind02110 scope, string resolver, string capability, string group)
        => new ProtocolAuthorizationDescriptor02110
        {
            CommandName = command,
            HandlerIdentity = handler,
            AuthorizationClass = auth,
            ScopeKind = scope,
            ScopeResolverId = resolver,
            RequiredCapability = capability,
            SecurityTestGroup = group
        };

    private static ProtocolScopeKind02110 ResolveScope(string root, string lower)
    {
        if (lower.Contains("session") || root.Equals("chat", StringComparison.OrdinalIgnoreCase) || root.Equals("audio", StringComparison.OrdinalIgnoreCase)) return ProtocolScopeKind02110.Session;
        if (root.Equals("character", StringComparison.OrdinalIgnoreCase) || root.Equals("actor", StringComparison.OrdinalIgnoreCase)
            || root.Equals("inventory", StringComparison.OrdinalIgnoreCase) || root.Equals("skills", StringComparison.OrdinalIgnoreCase)
            || root.Equals("classTree", StringComparison.OrdinalIgnoreCase)
            || root.Equals("development", StringComparison.OrdinalIgnoreCase) || root.Equals("progression", StringComparison.OrdinalIgnoreCase)) return ProtocolScopeKind02110.Character;
        if (lower.Contains("map") || root.StartsWith("sceneMap", StringComparison.OrdinalIgnoreCase)) return ProtocolScopeKind02110.Map;
        if (root.Equals("combat", StringComparison.OrdinalIgnoreCase)) return ProtocolScopeKind02110.Combat;
        if (root.Equals("request", StringComparison.OrdinalIgnoreCase) || root.Equals("requests", StringComparison.OrdinalIgnoreCase) || root.Equals("dice", StringComparison.OrdinalIgnoreCase)) return ProtocolScopeKind02110.Request;
        if (root.Equals("project", StringComparison.OrdinalIgnoreCase) || root.Equals("proposal", StringComparison.OrdinalIgnoreCase)
            || root.Equals("research", StringComparison.OrdinalIgnoreCase) || root.Equals("engineering", StringComparison.OrdinalIgnoreCase)
            || root.Equals("production", StringComparison.OrdinalIgnoreCase) || root.Equals("manufacturing", StringComparison.OrdinalIgnoreCase)
            || root.Equals("crafting", StringComparison.OrdinalIgnoreCase) || root.Equals("factory", StringComparison.OrdinalIgnoreCase)) return ProtocolScopeKind02110.Project;
        if (root.Equals("campaign", StringComparison.OrdinalIgnoreCase) || root.Equals("dataPortability", StringComparison.OrdinalIgnoreCase)) return ProtocolScopeKind02110.Campaign;
        return ProtocolScopeKind02110.ActiveCampaign;
    }

    private static string ResolverId(ProtocolScopeKind02110 scope)
        => scope switch
        {
            ProtocolScopeKind02110.Campaign => "campaign_payload_or_entity",
            ProtocolScopeKind02110.Session => "session_entity",
            ProtocolScopeKind02110.Character => "character_v2_entity",
            ProtocolScopeKind02110.Map => "map_entity",
            ProtocolScopeKind02110.Combat => "combat_entity",
            ProtocolScopeKind02110.Request => "request_entity",
            ProtocolScopeKind02110.Project => "project_entity",
            ProtocolScopeKind02110.ActiveCampaign => "active_context",
            _ => "entity_parent"
        };

    private static string ResolveCapability(ProtocolScopeKind02110 scope, string lower, bool isPlayer, bool isRead)
    {
        if (isPlayer) return isRead ? CampaignCapabilityIds.CharacterViewPlayerSafe : CampaignCapabilityIds.CharacterManageOwned;
        if (lower == "chat.send") return CampaignCapabilityIds.SessionView;
        if (lower.StartsWith("campaign.membership", StringComparison.Ordinal)) return CampaignCapabilityIds.CampaignManageMemberships;
        if (lower.StartsWith("campaign.ownership", StringComparison.Ordinal)) return CampaignCapabilityIds.CampaignTransferOwnership;
        if (lower.StartsWith("session.participation", StringComparison.Ordinal)) return CampaignCapabilityIds.SessionManageParticipants;
        if (lower.StartsWith("automation.", StringComparison.Ordinal)) return isRead ? CampaignCapabilityIds.AutomationView : CampaignCapabilityIds.AutomationManage;
        if (lower.Contains("travel")) return isRead ? CampaignCapabilityIds.SessionView : CampaignCapabilityIds.TravelRun;
        if (lower.Contains("weather") || lower.Contains("environment")) return isRead ? CampaignCapabilityIds.SessionView : CampaignCapabilityIds.WeatherRun;
        return scope switch
        {
            ProtocolScopeKind02110.Session => isRead ? CampaignCapabilityIds.SessionView : CampaignCapabilityIds.SessionEdit,
            ProtocolScopeKind02110.Character => isRead ? CampaignCapabilityIds.CharacterViewPlayerSafe : CampaignCapabilityIds.CharacterManageAnyInCampaign,
            ProtocolScopeKind02110.Map => isRead ? CampaignCapabilityIds.MapViewGM : CampaignCapabilityIds.MapEdit,
            ProtocolScopeKind02110.Combat => CampaignCapabilityIds.CombatRun,
            ProtocolScopeKind02110.Campaign => isRead ? CampaignCapabilityIds.CampaignView : CampaignCapabilityIds.CampaignEditContent,
            _ => isRead ? CampaignCapabilityIds.CampaignView : CampaignCapabilityIds.CampaignEditContent
        };
    }

    private static string ResolveSecurityGroup(string root, string lower)
    {
        if (lower.Contains("session.participation")) return "SessionParticipation";
        if (root.Equals("dice", StringComparison.OrdinalIgnoreCase)) return "Dice";
        if (lower.Contains("request") || root.Equals("requests", StringComparison.OrdinalIgnoreCase)) return "Requests";
        if (lower.Contains("organization") || lower.Contains("faction")) return "Organizations";
        if (lower.Contains("maptoken")) return "Map tokens";
        if (lower.Contains("map")) return "Maps";
        if (lower.Contains("combat")) return "Combat";
        if (lower.Contains("weather") || lower.Contains("environment")) return "Weather";
        if (lower.Contains("travel")) return "Travel";
        if (lower.Contains("character") || lower.StartsWith("actor.")) return lower.Contains("admin") ? "Character Admin" : "Character Player";
        if (lower.Contains("session")) return "CurrentSession";
        if (lower.Contains("group")) return "ActiveGroup";
        if (lower.Contains("knowledge")) return "Knowledge";
        if (lower.Contains("quest")) return "Quest";
        if (lower.Contains("shop") || lower.Contains("market") || lower.Contains("economy")) return "Economy/Shop/Market";
        if (lower.Contains("project") || lower.Contains("research") || lower.Contains("engineering") || lower.Contains("production") || lower.Contains("manufacturing") || lower.Contains("crafting")) return "Projects";
        if (lower.Contains("dice")) return "Dice";
        if (lower.Contains("chat")) return "Chat";
        if (lower.Contains("audio")) return "Audio";
        if (lower.Contains("note") || lower.Contains("journal")) return "Notes";
        if (lower.Contains("automation")) return "Automation";
        if (lower.Contains("portability")) return "Portability";
        if (root.Equals("development", StringComparison.OrdinalIgnoreCase) || root.Equals("progression", StringComparison.OrdinalIgnoreCase)
            || root.Equals("skills", StringComparison.OrdinalIgnoreCase) || root.Equals("classTree", StringComparison.OrdinalIgnoreCase)
            || root.Equals("inventory", StringComparison.OrdinalIgnoreCase) || root.Equals("rest", StringComparison.OrdinalIgnoreCase)) return "Character v2 Profiles";
        if (root.Equals("proposal", StringComparison.OrdinalIgnoreCase) || root.Equals("factory", StringComparison.OrdinalIgnoreCase)
            || root.Equals("asset", StringComparison.OrdinalIgnoreCase) || root.Equals("asset_blueprint", StringComparison.OrdinalIgnoreCase)) return "Projects";
        if (root.Equals("legal", StringComparison.OrdinalIgnoreCase)) return "Economy/Shop/Market";
        if (root.Equals("world", StringComparison.OrdinalIgnoreCase) || root.Equals("schedule", StringComparison.OrdinalIgnoreCase)) return "Definitions/reference data";
        if (root.Equals("fate", StringComparison.OrdinalIgnoreCase) || root.Equals("gameplay", StringComparison.OrdinalIgnoreCase)) return "CurrentSession";
        if (root.Equals("search", StringComparison.OrdinalIgnoreCase)) return "Search";
        if (root.Equals("sync", StringComparison.OrdinalIgnoreCase)) return "Sync";
        if (root.Equals("visibility", StringComparison.OrdinalIgnoreCase)) return "Campaign Administration";
        if (root.Equals("campaign", StringComparison.OrdinalIgnoreCase)) return "Campaign Administration";
        if (root.Equals("player", StringComparison.OrdinalIgnoreCase)) return "Dashboard";
        return "Campaign Administration";
    }

    private static bool IsReadCommand(string lower)
        => lower == "magic.targetscope.evaluate"
           || lower.EndsWith(".get") || lower.EndsWith("get") || lower.Contains(".get") || lower.EndsWith(".list") || lower.EndsWith("list") || lower.Contains(".list") || lower.EndsWith(".search") || lower.EndsWith("search")
           || lower.Contains(".get.") || lower.Contains(".list.") || lower.Contains("preview")
           || lower.Contains("validate") || lower.Contains("diagnostic") || lower.EndsWith(".status")
           || lower.Contains(".status.get") || lower.EndsWith(".explain")
           || lower.EndsWith(".query") || lower.Contains("referenceoptions")
           || lower.EndsWith("feed") || lower.Contains("available") || lower.Contains("history") || lower.Contains("export");

    private static bool HasSameSecuritySemantics(ProtocolAuthorizationDescriptor02110 left, ProtocolAuthorizationDescriptor02110 right)
        => left.AuthorizationClass == right.AuthorizationClass
           && left.ScopeKind == right.ScopeKind
           && string.Equals(left.ScopeResolverId, right.ScopeResolverId, StringComparison.Ordinal)
           && string.Equals(left.RequiredCapability, right.RequiredCapability, StringComparison.Ordinal)
           && string.Equals(left.PlayerOwnershipPolicy, right.PlayerOwnershipPolicy, StringComparison.Ordinal)
           && string.Equals(left.SecurityTestGroup, right.SecurityTestGroup, StringComparison.Ordinal);
}
