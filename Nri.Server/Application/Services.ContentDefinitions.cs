using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nri.Server.Application.Services;
using Nri.Server.Content;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope DefinitionsSkillsGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var visibilityContext = _visibilityService.BuildContextFromCommand(context, actor);
        var category = PayloadReader.GetString(context.Request.Payload, "category");
        var search = PayloadReader.GetString(context.Request.Payload, "search");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");

        var items = FilterRecords(_contentService.GetSnapshot().Skills.Values, search, includeArchived,
            x => string.IsNullOrWhiteSpace(category) || FieldEquals(x, "category", category))
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(ToDefinitionPayload)
            .Select(x => VisibilityFeatureFlags.UseDefinitionVisibilityFilter ? _visibilityService.FilterDefinitionPayload(x, visibilityContext, "skill", Convert.ToString(x.ContainsKey("code") ? x["code"] : string.Empty) ?? string.Empty) : x)
            .Where(x => x != null)
            .Cast<object>()
            .ToArray();

        _logger.Debug($"definitions.skills.get count={items.Length} category={category ?? ""} search={search ?? ""} includeArchived={includeArchived}");
        return Ok("Skill definitions loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope DefinitionsClassesGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var visibilityContext = _visibilityService.BuildContextFromCommand(context, actor);
        var branchCode = PayloadReader.GetString(context.Request.Payload, "branchCode");
        var parentClassCode = PayloadReader.GetString(context.Request.Payload, "parentClassCode");
        var search = PayloadReader.GetString(context.Request.Payload, "search");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");

        var items = FilterRecords(_contentService.GetSnapshot().Classes.Values, search, includeArchived,
            x => (string.IsNullOrWhiteSpace(branchCode) || FieldEquals(x, "branchCode", branchCode))
                 && (string.IsNullOrWhiteSpace(parentClassCode) || FieldEquals(x, "parentClassCode", parentClassCode)))
            .OrderBy(x => GetContentFieldString(x, "branchCode"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(ToDefinitionPayload)
            .Select(x => VisibilityFeatureFlags.UseDefinitionVisibilityFilter ? _visibilityService.FilterDefinitionPayload(x, visibilityContext, "class", Convert.ToString(x.ContainsKey("code") ? x["code"] : string.Empty) ?? string.Empty) : x)
            .Where(x => x != null)
            .Cast<object>()
            .ToArray();

        _logger.Debug($"definitions.classes.get count={items.Length} branchCode={branchCode ?? ""} parentClassCode={parentClassCode ?? ""} search={search ?? ""} includeArchived={includeArchived}");
        return Ok("Class definitions loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope DefinitionsRacesGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var visibilityContext = _visibilityService.BuildContextFromCommand(context, actor);
        var search = PayloadReader.GetString(context.Request.Payload, "search");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");

        var items = FilterRecords(_contentService.GetSnapshot().Races.Values, search, includeArchived, _ => true)
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(ToDefinitionPayload)
            .Select(x => VisibilityFeatureFlags.UseDefinitionVisibilityFilter ? _visibilityService.FilterDefinitionPayload(x, visibilityContext, "race", Convert.ToString(x.ContainsKey("code") ? x["code"] : string.Empty) ?? string.Empty) : x)
            .Where(x => x != null)
            .Cast<object>()
            .ToArray();

        _logger.Debug($"definitions.races.get count={items.Length} search={search ?? ""} includeArchived={includeArchived}");
        return Ok("Race definitions loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope DefinitionsItemsGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var itemType = PayloadReader.GetString(context.Request.Payload, "itemType");
        var search = PayloadReader.GetString(context.Request.Payload, "search");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");

        var items = FilterRecords(_contentService.GetSnapshot().Items.Values, search, includeArchived,
            x => string.IsNullOrWhiteSpace(itemType) || FieldEquals(x, "itemType", itemType))
            .OrderBy(x => GetContentFieldString(x, "itemType"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(ToDefinitionPayload)
            .Cast<object>()
            .ToArray();

        _logger.Debug($"definitions.items.get count={items.Length} itemType={itemType ?? ""} search={search ?? ""} includeArchived={includeArchived}");
        return Ok("Item definitions loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope DefinitionsContentStatus(CommandContext context)
    {
        GetCurrentAccount(context);
        var report = _contentService.GetLastReport();
        if (report == null)
        {
            return Ok("Content has not been loaded yet.", new Dictionary<string, object>
            {
                { "loaded", false },
                { "items", Array.Empty<object>() },
                { "errors", Array.Empty<object>() }
            });
        }

        var recordsByCategory = report.RecordsByCategory.ToDictionary(x => x.Key, x => (object)x.Value);
        return Ok("Content status loaded.", new Dictionary<string, object>
        {
            { "loaded", true },
            { "loadedAtUtc", report.LoadedAtUtc },
            { "success", report.Success },
            { "filesFound", report.FilesFound },
            { "filesRead", report.FilesRead },
            { "errorCount", report.ErrorCount },
            { "recordsByCategory", recordsByCategory },
            { "errors", report.Errors.Cast<object>().ToArray() }
        });
    }

    public ResponseEnvelope DefinitionsPackDryRun(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var requestedPath = PayloadReader.GetString(context.Request.Payload, "packPath");
        var packDirectory = ResolveDefinitionPackDirectory(requestedPath);
        var loader = new DefinitionPackLoader(_logger);
        var result = loader.DryRunImportAsync(packDirectory).GetAwaiter().GetResult();
        _logger.Admin($"definition.pack.dryrun.actor actor={actor.Id} packId={result.PackId} success={result.Success} definitions={result.LoadedDefinitions}");

        return Ok("Definition pack dry-run completed.", new Dictionary<string, object>
        {
            { "packId", result.PackId },
            { "success", result.Success },
            { "loadedDefinitions", result.LoadedDefinitions },
            { "errors", result.Errors.Cast<object>().ToArray() },
            { "warnings", result.Warnings.Cast<object>().ToArray() },
            { "crossReferenceErrors", result.CrossReferenceErrors.Cast<object>().ToArray() },
            { "crossReferenceWarnings", result.CrossReferenceWarnings.Cast<object>().ToArray() },
            { "files", result.FileResults.Select(DefinitionPackFileResultPayload).Cast<object>().ToArray() }
        });
    }

    public ResponseEnvelope EconomyRuntimeSeedApply(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EconomyFeatureFlags.UseEconomyRuntimeSeedWrite)
        {
            return Error("economy runtime seed write disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var requestedPath = PayloadReader.GetString(context.Request.Payload, "packPath");
        var packDirectory = ResolveDefinitionPackDirectory(requestedPath);
        var loader = new DefinitionPackLoader(_logger);
        var planner = new EconomyRuntimeSeedPlanner(loader, new DefinitionPackCrossReferenceValidator(), _logger);
        var service = new EconomyRuntimeSeedService(planner, _repositories, _logger);
        var request = new EconomyRuntimeSeedRequest
        {
            RuleSetId = PayloadReader.GetString(context.Request.Payload, "ruleSetId") ?? "fantasy_nri_default",
            CampaignId = PayloadReader.GetString(context.Request.Payload, "campaignId") ?? string.Empty,
            PackId = PayloadReader.GetString(context.Request.Payload, "packId") ?? string.Empty,
            PackPath = packDirectory,
            IncludeFactions = GetBoolDefault(context.Request.Payload, "includeFactions", true),
            IncludeOrganizations = GetBoolDefault(context.Request.Payload, "includeOrganizations", true),
            IncludeLaws = GetBoolDefault(context.Request.Payload, "includeLaws", true),
            IncludeRestrictions = GetBoolDefault(context.Request.Payload, "includeRestrictions", true),
            IncludeMarkets = GetBoolDefault(context.Request.Payload, "includeMarkets", true),
            IncludeEconomyScopes = GetBoolDefault(context.Request.Payload, "includeEconomyScopes", true),
            RequireDryRunSuccess = GetBoolDefault(context.Request.Payload, "requireDryRunSuccess", true),
            AllowOverwrite = PayloadReader.GetBool(context.Request.Payload, "allowOverwrite"),
            ValidateOnly = PayloadReader.GetBool(context.Request.Payload, "validateOnly"),
            ActorUserId = actor.Id,
            RequestId = context.Request.RequestId ?? string.Empty
        };

        var result = service.SeedFromDefinitionsAsync(request).GetAwaiter().GetResult();
        _logger.Admin($"economy.seed.apply.actor actor={actor.Id} campaignId={result.CampaignId} success={result.Success} created={result.CreatedStates.Count} skipped={result.SkippedStates.Count}");

        return Ok("Economy runtime seed apply completed.", EconomyRuntimeSeedResultPayload(result));
    }

    private static IEnumerable<GameContentRecord> FilterRecords(IEnumerable<GameContentRecord> source, string? search, bool includeArchived, Func<GameContentRecord, bool> extraFilter)
    {
        foreach (var item in source)
        {
            if (!includeArchived && IsArchived(item))
            {
                continue;
            }

            if (!extraFilter(item))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var description = GetContentFieldString(item, "description");
                if (IndexOfIgnoreCase(item.Code, search) < 0
                    && IndexOfIgnoreCase(item.DisplayName, search) < 0
                    && IndexOfIgnoreCase(description, search) < 0)
                {
                    continue;
                }
            }

            yield return item;
        }
    }


    private static int IndexOfIgnoreCase(string value, string search)
    {
        return (value ?? string.Empty).IndexOf(search ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArchived(GameContentRecord record)
    {
        if (!record.ExtraFields.TryGetValue("archived", out var value)) return false;
        return value.ValueKind == System.Text.Json.JsonValueKind.True;
    }

    private static bool FieldEquals(GameContentRecord record, string field, string expected)
        => string.Equals(GetContentFieldString(record, field), expected, StringComparison.OrdinalIgnoreCase);

    private static string GetContentFieldString(GameContentRecord record, string field)
    {
        if (!record.ExtraFields.TryGetValue(field, out var value)) return string.Empty;
        return value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => value.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Null => string.Empty,
            _ => value.ToString()
        };
    }

    private static Dictionary<string, object> ToDefinitionPayload(GameContentRecord record)
    {
        var payload = new Dictionary<string, object>
        {
            { "code", record.Code },
            { "displayName", record.DisplayName },
            { "description", GetContentFieldString(record, "description") }
        };

        foreach (var key in new[] { "category", "itemType", "branchCode", "parentClassCode" })
        {
            var value = GetContentFieldString(record, key);
            if (!string.IsNullOrWhiteSpace(value)) payload[key] = value;
        }

        payload["additionalData"] = record.ExtraFields.ToDictionary(x => x.Key, x => (object)x.Value.ToString());
        return payload;
    }

    private static Dictionary<string, object> DefinitionPackFileResultPayload(DefinitionPackFileValidationResult result)
    {
        return new Dictionary<string, object>
        {
            { "category", result.Category },
            { "path", result.Path },
            { "definitionCount", result.DefinitionCount },
            { "errors", result.Errors.Cast<object>().ToArray() },
            { "warnings", result.Warnings.Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> EconomyRuntimeSeedResultPayload(EconomyRuntimeSeedResult result)
    {
        return new Dictionary<string, object>
        {
            { "success", result.Success },
            { "ruleSetId", result.RuleSetId },
            { "campaignId", result.CampaignId },
            { "packId", result.PackId },
            { "createdStates", result.CreatedStates.Select(x => new Dictionary<string, object>{{"runtimeType", x.RuntimeType},{"id", x.Id},{"definitionId", x.DefinitionId},{"name", x.Name},{"collectionName", x.CollectionName}}).Cast<object>().ToArray() },
            { "skippedStates", result.SkippedStates.Select(x => new Dictionary<string, object>{{"runtimeType", x.RuntimeType},{"proposedId", x.ProposedId},{"definitionId", x.DefinitionId},{"reason", x.Reason}}).Cast<object>().ToArray() },
            { "errors", result.Errors.Cast<object>().ToArray() },
            { "warnings", result.Warnings.Cast<object>().ToArray() },
            { "summary", new Dictionary<string, object>
                {
                    { "createdFactions", result.Summary.CreatedFactions },
                    { "createdOrganizations", result.Summary.CreatedOrganizations },
                    { "createdLaws", result.Summary.CreatedLaws },
                    { "createdRestrictions", result.Summary.CreatedRestrictions },
                    { "createdMarkets", result.Summary.CreatedMarkets },
                    { "createdEconomyScopes", result.Summary.CreatedEconomyScopes },
                    { "skippedExisting", result.Summary.SkippedExisting },
                    { "errorCount", result.Summary.ErrorCount },
                    { "warningCount", result.Summary.WarningCount }
                }
            },
            { "seededAtUtc", result.SeededAtUtc }
        };
    }

    private static bool GetBoolDefault(IDictionary<string, object> payload, string key, bool defaultValue)
    {
        return payload.ContainsKey(key) && payload[key] != null
            ? PayloadReader.GetBool(payload, key)
            : defaultValue;
    }

    private static string ResolveDefinitionPackDirectory(string? requestedPath)
    {
        var raw = string.IsNullOrWhiteSpace(requestedPath)
            ? Path.Combine("Content", "DefinitionPacks", "fantasy_nri_default_starter")
            : requestedPath.Trim();
        var current = Directory.GetCurrentDirectory();
        var candidate = Path.IsPathRooted(raw)
            ? Path.GetFullPath(raw)
            : Path.GetFullPath(Path.Combine(current, raw));

        if (!Directory.Exists(candidate) && !Path.IsPathRooted(raw))
        {
            var serverCandidate = Path.GetFullPath(Path.Combine(current, "Nri.Server", raw));
            if (Directory.Exists(serverCandidate))
            {
                candidate = serverCandidate;
            }
        }

        var allowedRoots = new[]
        {
            Path.GetFullPath(Path.Combine(current, "Content", "DefinitionPacks")),
            Path.GetFullPath(Path.Combine(current, "Nri.Server", "Content", "DefinitionPacks"))
        };

        if (!allowedRoots.Any(root => IsSameOrChildPath(root, candidate)))
        {
            throw new UnauthorizedAccessException("Definition pack dry-run path must be inside Content/DefinitionPacks.");
        }

        if (!Directory.Exists(candidate))
        {
            throw new DirectoryNotFoundException("Definition pack directory not found.");
        }

        return candidate;
    }

    private static bool IsSameOrChildPath(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(normalizedRoot, normalizedCandidate, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
