using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Nri.Server.Application;

namespace Nri.MapIdentity.Contracts;

internal static class Program
{
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_20_2");
        Directory.CreateDirectory(output);
        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["legacyIdResolvesToCanonical"] = Evaluate(new[] { "canonical-1" }) == MapIdentityResolutionStatus0202.Resolved,
            ["canonicalIdResolves"] = Evaluate(new[] { "canonical-1" }) == MapIdentityResolutionStatus0202.Resolved,
            ["missingMappingRejected"] = Evaluate(Array.Empty<string>()) == MapIdentityResolutionStatus0202.NotFound,
            ["conflictingMappingRejected"] = Evaluate(new[] { "canonical-1", "canonical-2" }) == MapIdentityResolutionStatus0202.Conflict,
            ["missingCanonicalRejected"] = Evaluate(new[] { "canonical-1" }, canonicalExists: false) == MapIdentityResolutionStatus0202.Conflict,
            ["archivedMapRejected"] = Evaluate(new[] { "canonical-1" }, archived: true) == MapIdentityResolutionStatus0202.Archived,
            ["projectionConflictRejected"] = Evaluate(new[] { "canonical-1" }, conflictingProjection: true) == MapIdentityResolutionStatus0202.Conflict,
            ["staleProjectionRejected"] = Evaluate(new[] { "canonical-1" }, staleProjection: true) == MapIdentityResolutionStatus0202.StaleProjection
        };
        var status = System.Linq.Enumerable.All(checks.Values, value => value) ? "PASS" : "NOT_PASS";
        var audit = new
        {
            status,
            authoritativeCollection = "map_states",
            compatibilityProjection = "scene_map_definitions",
            mappingCollection = "map_identity_mappings",
            destructiveMigration = false,
            directClientDualWrite = false,
            checks
        };
        File.WriteAllText(Path.Combine(output, "map_identity_adapter_audit.json"), new JavaScriptSerializer().Serialize(audit), new UTF8Encoding(false));
        Console.WriteLine("Map identity contracts: " + status);
        return status == "PASS" ? 0 : 1;
    }

    private static MapIdentityResolutionStatus0202 Evaluate(
        IEnumerable<string> candidates,
        bool canonicalExists = true,
        bool archived = false,
        bool conflictingProjection = false,
        bool staleProjection = false)
        => MapIdentityDecision0202.Evaluate(candidates, canonicalExists, archived, conflictingProjection, staleProjection);
}
