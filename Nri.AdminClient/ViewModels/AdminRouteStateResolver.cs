namespace Nri.AdminClient.ViewModels;

public static class AdminRouteStateResolver
{
    public static string ResolveCollection(
        bool hasPermission,
        bool isLoading,
        bool hasError,
        bool hasActiveFilter,
        bool hasVisibleRows,
        bool requiresSelection,
        bool hasSelection)
    {
        if (!hasPermission) return "permission";
        if (isLoading) return "loading";
        if (hasError) return "error";
        if (!hasVisibleRows) return hasActiveFilter ? "no-results" : "empty";
        if (requiresSelection && !hasSelection) return "no-selection";
        return "ready";
    }

    public static string ResolveUnavailable() => "unavailable";
}
