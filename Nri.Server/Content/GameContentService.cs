using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nri.Server.Logging;

namespace Nri.Server.Content;

public sealed class GameContentService
{
    private static readonly string[] CategoryNames =
    {
        "skills", "class_skills", "classes", "races", "items", "item_templates", "holdings", "magic"
    };

    private readonly IServerLogger _logger;
    private readonly string _rootPath;
    private readonly object _sync = new object();
    private GameContentRegistry _registry = new GameContentRegistry();
    private ContentLoadReport? _lastReport;

    public GameContentService(IServerLogger logger, string? rootPath = null)
    {
        _logger = logger;
        var basePath = string.IsNullOrWhiteSpace(rootPath) ? AppContext.BaseDirectory : rootPath;
        _rootPath = Path.Combine(basePath, "ServerContent");
    }

    public string RootPath => _rootPath;

    public ContentLoadReport Reload()
    {
        EnsureFolders();

        var report = new ContentLoadReport();
        foreach (var category in CategoryNames)
        {
            report.RecordsByCategory[category] = 0;
        }

        var candidate = new GameContentRegistry();

        foreach (var category in CategoryNames)
        {
            var categoryPath = Path.Combine(_rootPath, category);
            var files = Directory.GetFiles(categoryPath, "*.json", SearchOption.TopDirectoryOnly);
            report.FilesFound += files.Length;

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        throw new InvalidDataException("Root element must be array.");
                    }

                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            report.ErrorCount++;
                            report.Errors.Add($"{category}/{Path.GetFileName(file)}: record is not object");
                            continue;
                        }

                        if (!TryParseRecord(item, out var record, out var error))
                        {
                            report.ErrorCount++;
                            report.Errors.Add($"{category}/{Path.GetFileName(file)}: {error}");
                            continue;
                        }

                        GetBucket(candidate, category)[record.Code] = record;
                    }

                    report.FilesRead++;
                }
                catch (Exception ex)
                {
                    report.ErrorCount++;
                    var message = $"content.load.failed category={category} file={file} error={ex.GetType().Name} message={ex.Message}";
                    report.Errors.Add(message);
                    _logger.Debug(message);
                }
            }

            report.RecordsByCategory[category] = GetBucket(candidate, category).Count;
        }

        report.Success = report.ErrorCount == 0 || report.FilesRead > 0;

        lock (_sync)
        {
            if (report.Success)
            {
                _registry = candidate;
            }

            _lastReport = report;
        }

        _logger.Debug($"content.load.summary success={report.Success} filesFound={report.FilesFound} filesRead={report.FilesRead} errors={report.ErrorCount}");
        return report;
    }

    public ContentLoadReport? GetLastReport()
    {
        lock (_sync)
        {
            return _lastReport;
        }
    }

    public GameContentRegistry GetSnapshot()
    {
        lock (_sync)
        {
            return _registry;
        }
    }

    public void EnsureFolders()
    {
        Directory.CreateDirectory(_rootPath);
        foreach (var category in CategoryNames)
        {
            Directory.CreateDirectory(Path.Combine(_rootPath, category));
        }
    }

    private static bool TryParseRecord(JsonElement item, out GameContentRecord record, out string error)
    {
        record = new GameContentRecord();
        error = string.Empty;

        if (!item.TryGetProperty("code", out var codeElement) || codeElement.ValueKind != JsonValueKind.String)
        {
            error = "missing required field 'code'";
            return false;
        }

        if (!item.TryGetProperty("displayName", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            error = "missing required field 'displayName'";
            return false;
        }

        record.Code = codeElement.GetString() ?? string.Empty;
        record.DisplayName = nameElement.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(record.Code) || string.IsNullOrWhiteSpace(record.DisplayName))
        {
            error = "'code' and 'displayName' must be non-empty";
            return false;
        }

        foreach (var property in item.EnumerateObject())
        {
            if (property.NameEquals("code") || property.NameEquals("displayName"))
            {
                continue;
            }

            record.ExtraFields[property.Name] = property.Value.Clone();
        }

        return true;
    }

    private static Dictionary<string, GameContentRecord> GetBucket(GameContentRegistry registry, string category)
    {
        return category switch
        {
            "skills" => registry.Skills,
            "class_skills" => registry.ClassSkills,
            "classes" => registry.Classes,
            "races" => registry.Races,
            "items" => registry.Items,
            "item_templates" => registry.ItemTemplates,
            "holdings" => registry.Holdings,
            "magic" => registry.Magic,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown category")
        };
    }
}
