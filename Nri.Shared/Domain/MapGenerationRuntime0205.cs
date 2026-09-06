using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Nri.Shared.Domain;

public static class MapGenerationRuntime0205
{
    public const string ServiceVersion = "0.20.5";
    public const string PreviewStatus = "preview";
    public const string ApplyingStatus = "applying";
    public const string AppliedStatus = "applied";
    public const string FailedStatus = "failed";
}

public static class MapGenerationDeterminism0205
{
    public static string ComputeFingerprint(
        string templateId,
        int templateRevision,
        string presetId,
        int presetRevision,
        string seed,
        double widthMeters,
        double heightMeters,
        string ruleSetId,
        string blueprintFingerprint)
    {
        var canonical = string.Join("|", new[]
        {
            (templateId ?? string.Empty).Trim(),
            templateRevision.ToString(CultureInfo.InvariantCulture),
            (presetId ?? string.Empty).Trim(),
            presetRevision.ToString(CultureInfo.InvariantCulture),
            (seed ?? string.Empty).Trim(),
            widthMeters.ToString("0.###", CultureInfo.InvariantCulture),
            heightMeters.ToString("0.###", CultureInfo.InvariantCulture),
            (ruleSetId ?? string.Empty).Trim(),
            (blueprintFingerprint ?? string.Empty).Trim().ToLowerInvariant()
        });
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }
}

public sealed class GenerationTemplateDefinition0205 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LocationKind { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public bool IsArchived { get; set; }
}

public sealed class GenerationPresetDefinition0205 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public bool IsArchived { get; set; }
}

public sealed class MapGenerationPreview0205
{
    public string PreviewId { get; set; } = string.Empty;
    public string CanonicalMapId { get; set; } = string.Empty;
    public long MapRevision { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public int TemplateRevision { get; set; }
    public string PresetId { get; set; } = string.Empty;
    public int PresetRevision { get; set; }
    public string Seed { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(30);
    public Dictionary<string, object> Summary { get; set; } = new Dictionary<string, object>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class MapGenerationRunState0205 : EntityBase
{
    public string OperationId { get; set; } = string.Empty;
    public string PreviewId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public int TemplateRevision { get; set; }
    public string PresetId { get; set; } = string.Empty;
    public int PresetRevision { get; set; }
    public string Seed { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public long ExpectedMapRevision { get; set; }
    public long AppliedMapRevision { get; set; }
    public string Status { get; set; } = MapGenerationRuntime0205.ApplyingStatus;
    public string AppliedByUserId { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ResultSummary { get; set; } = new Dictionary<string, object>();
}
