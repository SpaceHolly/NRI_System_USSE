using System;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Linq;

namespace Nri.Shared.Domain;

public static class ApplicationContextStates
{
    public const string Ready = "ready";
    public const string Changing = "changing";
    public const string NoCharacter = "no_character";
    public const string ProfileMissing = "profile_missing";
    public const string ProfileMigrationRequired = "profile_migration_required";
    public const string ProfileRepairRequired = "profile_repair_required";
}

public sealed class ApplicationContextReference
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ApplicationModuleAvailability
{
    public string ModuleKey { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class ApplicationContextSnapshot
{
    public ApplicationContextReference Account { get; set; } = new ApplicationContextReference();
    public string Role { get; set; } = string.Empty;
    public List<string> EffectiveCapabilities { get; set; } = new List<string>();
    public bool SuperAdminOverrideActive { get; set; }
    public string SuperAdminOverrideWarning { get; set; } = string.Empty;
    public ApplicationContextReference Campaign { get; set; } = new ApplicationContextReference();
    public ApplicationContextReference Session { get; set; } = new ApplicationContextReference();
    public ApplicationContextReference World { get; set; } = new ApplicationContextReference();
    public ApplicationContextReference ActiveCharacter { get; set; } = new ApplicationContextReference();
    public ApplicationContextReference ActiveScene { get; set; } = new ApplicationContextReference();
    public ApplicationContextReference ActiveMap { get; set; } = new ApplicationContextReference();
    public ApplicationContextReference ActiveCombat { get; set; } = new ApplicationContextReference();
    public long ContextRevision { get; set; }
    public DateTime ServerUtc { get; set; } = DateTime.UtcNow;
    public string State { get; set; } = ApplicationContextStates.Ready;
    public string StateMessage { get; set; } = string.Empty;
    public List<string> MissingProfileSections { get; set; } = new List<string>();
    public List<ApplicationModuleAvailability> Modules { get; set; } = new List<ApplicationModuleAvailability>();

    public bool HasActiveCharacter => !string.IsNullOrWhiteSpace(ActiveCharacter.Id);
    public string CampaignSessionSummary => BuildSummary(Campaign.DisplayName, Session.DisplayName, "Кампания и сессия не выбраны");
    public string CharacterSummary => HasActiveCharacter ? ActiveCharacter.DisplayName : "Персонаж не выбран";
    public bool HasCapability(string capabilityId) => EffectiveCapabilities.Contains(capabilityId, StringComparer.Ordinal);

    private static string BuildSummary(string first, string second, string empty)
    {
        var values = new[] { first, second }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return values.Length == 0 ? empty : string.Join(" / ", values);
    }
}

public sealed class ApplicationContextChangedEventArgs : EventArgs
{
    public ApplicationContextChangedEventArgs(ApplicationContextSnapshot previous, ApplicationContextSnapshot current)
    {
        Previous = previous;
        Current = current;
    }

    public ApplicationContextSnapshot Previous { get; }
    public ApplicationContextSnapshot Current { get; }
    public bool CharacterChanged => !string.Equals(Previous.ActiveCharacter.Id, Current.ActiveCharacter.Id, StringComparison.Ordinal);
    public bool SessionChanged => !string.Equals(Previous.Session.Id, Current.Session.Id, StringComparison.Ordinal)
        || !string.Equals(Previous.Campaign.Id, Current.Campaign.Id, StringComparison.Ordinal);
}

public interface IApplicationContextProvider
{
    ApplicationContextSnapshot Current { get; }
    bool IsLoading { get; }
    long LastAcceptedRevision { get; }
    event EventHandler<ApplicationContextChangedEventArgs>? ContextChanged;
    void BeginReplacement();
    bool TryAccept(ApplicationContextSnapshot snapshot);
    bool IsCurrent(long contextRevision, string campaignId = "", string sessionId = "", string characterId = "");
    void Clear();
}

public sealed class ApplicationContextProvider0212 : IApplicationContextProvider
{
    private readonly object _sync = new object();
    private ApplicationContextSnapshot _current = new ApplicationContextSnapshot { State = ApplicationContextStates.Changing };
    private bool _isLoading;

    public ApplicationContextSnapshot Current { get { lock (_sync) return _current; } }
    public bool IsLoading { get { lock (_sync) return _isLoading; } }
    public long LastAcceptedRevision { get { lock (_sync) return _current.ContextRevision; } }
    public event EventHandler<ApplicationContextChangedEventArgs>? ContextChanged;

    public void BeginReplacement()
    {
        lock (_sync) _isLoading = true;
    }

    public bool TryAccept(ApplicationContextSnapshot snapshot)
    {
        if (snapshot == null) return false;
        ApplicationContextSnapshot previous;
        lock (_sync)
        {
            if (snapshot.ContextRevision < _current.ContextRevision) return false;
            previous = _current;
            _current = snapshot;
            _isLoading = false;
        }

        ContextChanged?.Invoke(this, new ApplicationContextChangedEventArgs(previous, snapshot));
        return true;
    }

    public bool IsCurrent(long contextRevision, string campaignId = "", string sessionId = "", string characterId = "")
    {
        lock (_sync)
        {
            if (_isLoading || contextRevision != _current.ContextRevision) return false;
            if (!Matches(campaignId, _current.Campaign.Id)) return false;
            if (!Matches(sessionId, _current.Session.Id)) return false;
            return Matches(characterId, _current.ActiveCharacter.Id);
        }
    }

    public void Clear()
    {
        ApplicationContextSnapshot previous;
        var cleared = new ApplicationContextSnapshot { State = ApplicationContextStates.Changing };
        lock (_sync)
        {
            previous = _current;
            _current = cleared;
            _isLoading = false;
        }
        ContextChanged?.Invoke(this, new ApplicationContextChangedEventArgs(previous, cleared));
    }

    private static bool Matches(string expected, string actual)
        => string.IsNullOrWhiteSpace(expected) || string.Equals(expected, actual, StringComparison.Ordinal);
}

public static class ApplicationClientKinds
{
    public const string Admin = "admin";
    public const string Player = "player";
}

public static class RouteAvailabilityStates
{
    public const string Available = "available";
    public const string Disabled = "disabled";
    public const string Hidden = "hidden";
    public const string NoContext = "no_context";
    public const string NoPermission = "no_permission";
    public const string FeatureDisabled = "feature_disabled";
}

public sealed class RouteDescriptor
{
    public string RouteKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ClientKind { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string RequiredRole { get; set; } = string.Empty;
    public bool RequiresCharacter { get; set; }
    public bool RequiresSession { get; set; }
    public List<string> RequiredFeatureFlags { get; set; } = new List<string>();
    public string Target { get; set; } = string.Empty;
    public string AutomationId { get; set; } = string.Empty;
    public bool SupportsDeepLink { get; set; }
}

public sealed class RouteAvailability
{
    public string RouteKey { get; set; } = string.Empty;
    public string State { get; set; } = RouteAvailabilityStates.Available;
    public string Reason { get; set; } = string.Empty;
    public bool CanNavigate => string.Equals(State, RouteAvailabilityStates.Available, StringComparison.Ordinal);
}

public static class RouteAvailabilityEvaluator
{
    public static RouteAvailability Evaluate(RouteDescriptor route, ApplicationContextSnapshot context, ISet<string> enabledFlags)
    {
        if (route == null) return Unavailable(string.Empty, RouteAvailabilityStates.Hidden, "Маршрут не зарегистрирован.");
        if (route.RequiresSession && string.IsNullOrWhiteSpace(context?.Session.Id))
            return Unavailable(route.RouteKey, RouteAvailabilityStates.NoContext, "Сначала выберите активную сессию.");
        if (route.RequiresCharacter && string.IsNullOrWhiteSpace(context?.ActiveCharacter.Id))
            return Unavailable(route.RouteKey, RouteAvailabilityStates.NoContext, "Сначала выберите активного персонажа.");
        var missingFlag = route.RequiredFeatureFlags.FirstOrDefault(x => enabledFlags == null || !enabledFlags.Contains(x));
        if (!string.IsNullOrWhiteSpace(missingFlag))
            return Unavailable(route.RouteKey, RouteAvailabilityStates.FeatureDisabled, "Раздел временно недоступен в текущем профиле функций.");
        return new RouteAvailability { RouteKey = route.RouteKey };
    }

    private static RouteAvailability Unavailable(string routeKey, string state, string reason)
        => new RouteAvailability { RouteKey = routeKey, State = state, Reason = reason };
}

public sealed class ApplicationRouteRegistry0212
{
    private readonly Dictionary<string, RouteDescriptor> _routes = new Dictionary<string, RouteDescriptor>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<RouteDescriptor> Routes => _routes.Values.OrderBy(x => x.ClientKind).ThenBy(x => x.Area).ThenBy(x => x.DisplayName).ToArray();

    public void Register(RouteDescriptor descriptor)
    {
        if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.RouteKey))
            throw new ArgumentException("RouteKey is required.");
        if (_routes.ContainsKey(descriptor.RouteKey))
            throw new InvalidOperationException($"Route is already registered: {descriptor.RouteKey}");
        _routes[descriptor.RouteKey] = descriptor;
    }

    public RouteDescriptor? Find(string routeKey)
        => string.IsNullOrWhiteSpace(routeKey) || !_routes.TryGetValue(routeKey, out var route) ? null : route;

    public RouteAvailability Evaluate(string routeKey, ApplicationContextSnapshot context, ISet<string> enabledFlags)
        => RouteAvailabilityEvaluator.Evaluate(Find(routeKey)!, context, enabledFlags);
}

public sealed class FeatureFlagDescriptor
{
    public string CanonicalKey { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool DefaultValue { get; set; }
    public List<string> Dependencies { get; set; } = new List<string>();
    public List<string> ConflictingFlags { get; set; } = new List<string>();
    public List<string> RouteDependencies { get; set; } = new List<string>();
    public bool IsAdminVisible { get; set; } = true;
    public bool IsPlayerSafeVisible { get; set; }
    public string IntendedPreReleaseState { get; set; } = "intentionally_disabled";
    public List<string> Aliases { get; set; } = new List<string>();
    public bool AliasesDeprecated { get; set; } = true;
}

public static class FeatureProfiles
{
    public const string MinimalSafe = "MinimalSafe";
    public const string DevelopmentIntegrated = "DevelopmentIntegrated";
    public const string ReleaseCandidate = "ReleaseCandidate";
}

public static class ApplicationContextPayloadReader
{
    public static ApplicationContextSnapshot Read(IDictionary<string, object> payload)
    {
        payload = payload ?? new Dictionary<string, object>();
        return new ApplicationContextSnapshot
        {
            Account = ReadReference(payload, "account"),
            Role = ReadString(payload, "role"),
            EffectiveCapabilities = ReadList(payload, "capabilities").Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList(),
            SuperAdminOverrideActive = ReadBool(payload, "superAdminOverrideActive"),
            SuperAdminOverrideWarning = ReadString(payload, "superAdminOverrideWarning"),
            Campaign = ReadReference(payload, "campaign"),
            Session = ReadReference(payload, "session"),
            World = ReadReference(payload, "world"),
            ActiveCharacter = ReadReference(payload, "activeCharacter"),
            ActiveScene = ReadReference(payload, "activeScene"),
            ActiveMap = ReadReference(payload, "activeMap"),
            ActiveCombat = ReadReference(payload, "activeCombat"),
            ContextRevision = ReadLong(payload, "contextRevision"),
            ServerUtc = ReadDateTime(payload, "serverUtc"),
            State = ReadString(payload, "state", ApplicationContextStates.Ready),
            StateMessage = ReadString(payload, "stateMessage"),
            MissingProfileSections = ReadList(payload, "missingProfileSections").Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList(),
            Modules = ReadModules(payload)
        };
    }

    private static ApplicationContextReference ReadReference(IDictionary<string, object> payload, string key)
    {
        var map = ReadMap(payload, key);
        return new ApplicationContextReference { Id = ReadString(map, "id"), DisplayName = ReadString(map, "displayName") };
    }

    private static List<ApplicationModuleAvailability> ReadModules(IDictionary<string, object> payload)
    {
        return ReadList(payload, "modules")
            .Select(AsMap)
            .Where(x => x != null)
            .Select(x => new ApplicationModuleAvailability
            {
                ModuleKey = ReadString(x!, "moduleKey"),
                IsAvailable = ReadBool(x!, "isAvailable"),
                Reason = ReadString(x!, "reason")
            }).ToList();
    }

    private static IDictionary<string, object> ReadMap(IDictionary<string, object> payload, string key)
        => payload.TryGetValue(key, out var value) ? AsMap(value) ?? new Dictionary<string, object>() : new Dictionary<string, object>();

    private static IDictionary<string, object>? AsMap(object value)
    {
        if (value is IDictionary<string, object> typed) return typed;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry item in dictionary)
                result[Convert.ToString(item.Key) ?? string.Empty] = item.Value!;
            return result;
        }
        return null;
    }

    private static IEnumerable<object> ReadList(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null || value is string) return Enumerable.Empty<object>();
        if (value is IEnumerable enumerable) return enumerable.Cast<object>();
        return Enumerable.Empty<object>();
    }

    private static string ReadString(IDictionary<string, object> payload, string key, string fallback = "")
        => payload.TryGetValue(key, out var value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback : fallback;

    private static bool ReadBool(IDictionary<string, object> payload, string key)
        => payload.TryGetValue(key, out var value) && value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);

    private static long ReadLong(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null) return 0;
        try { return Convert.ToInt64(value, CultureInfo.InvariantCulture); }
        catch { return 0; }
    }

    private static DateTime ReadDateTime(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null) return DateTime.UtcNow;
        if (value is DateTime dateTime) return dateTime.ToUniversalTime();
        return DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.UtcNow;
    }
}
