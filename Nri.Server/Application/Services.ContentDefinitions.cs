using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.Content;
using Nri.Shared.Contracts;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope DefinitionsSkillsGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var category = PayloadReader.GetString(context.Request.Payload, "category");
        var search = PayloadReader.GetString(context.Request.Payload, "search");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");

        var items = FilterRecords(_contentService.GetSnapshot().Skills.Values, search, includeArchived,
            x => string.IsNullOrWhiteSpace(category) || FieldEquals(x, "category", category))
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(ToDefinitionPayload)
            .Cast<object>()
            .ToArray();

        _logger.Debug($"definitions.skills.get count={items.Length} category={category ?? ""} search={search ?? ""} includeArchived={includeArchived}");
        return Ok("Skill definitions loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope DefinitionsClassesGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var branchCode = PayloadReader.GetString(context.Request.Payload, "branchCode");
        var parentClassCode = PayloadReader.GetString(context.Request.Payload, "parentClassCode");
        var search = PayloadReader.GetString(context.Request.Payload, "search");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");

        var items = FilterRecords(_contentService.GetSnapshot().Classes.Values, search, includeArchived,
            x => (string.IsNullOrWhiteSpace(branchCode) || FieldEquals(x, "branchCode", branchCode))
                 && (string.IsNullOrWhiteSpace(parentClassCode) || FieldEquals(x, "parentClassCode", parentClassCode)))
            .OrderBy(x => GetFieldString(x, "branchCode"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(ToDefinitionPayload)
            .Cast<object>()
            .ToArray();

        _logger.Debug($"definitions.classes.get count={items.Length} branchCode={branchCode ?? ""} parentClassCode={parentClassCode ?? ""} search={search ?? ""} includeArchived={includeArchived}");
        return Ok("Class definitions loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope DefinitionsRacesGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var search = PayloadReader.GetString(context.Request.Payload, "search");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");

        var items = FilterRecords(_contentService.GetSnapshot().Races.Values, search, includeArchived, _ => true)
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(ToDefinitionPayload)
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
            .OrderBy(x => GetFieldString(x, "itemType"), StringComparer.OrdinalIgnoreCase)
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
                var description = GetFieldString(item, "description");
                if (!item.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
                    && !item.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    && !description.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            yield return item;
        }
    }

    private static bool IsArchived(GameContentRecord record)
    {
        if (!record.ExtraFields.TryGetValue("archived", out var value)) return false;
        return value.ValueKind == System.Text.Json.JsonValueKind.True;
    }

    private static bool FieldEquals(GameContentRecord record, string field, string expected)
        => string.Equals(GetFieldString(record, field), expected, StringComparison.OrdinalIgnoreCase);

    private static string GetFieldString(GameContentRecord record, string field)
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
            { "description", GetFieldString(record, "description") }
        };

        foreach (var key in new[] { "category", "itemType", "branchCode", "parentClassCode" })
        {
            var value = GetFieldString(record, key);
            if (!string.IsNullOrWhiteSpace(value)) payload[key] = value;
        }

        payload["additionalData"] = record.ExtraFields.ToDictionary(x => x.Key, x => (object)x.Value.ToString());
        return payload;
    }
}
