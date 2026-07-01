using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class DefinitionPackManifest
{
    public string PackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public List<DefinitionPackFile> Files { get; set; } = new List<DefinitionPackFile>();
    public int SchemaVersion { get; set; } = 1;
}

public sealed class DefinitionPackFile
{
    public string Category { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Required { get; set; }
    public int ExpectedMinCount { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class DefinitionPackLoadResult
{
    public string PackId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public List<string> LoadedFiles { get; set; } = new List<string>();
    public int LoadedDefinitions { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<string> CrossReferenceErrors { get; set; } = new List<string>();
    public List<string> CrossReferenceWarnings { get; set; } = new List<string>();
    public List<DefinitionPackFileValidationResult> FileResults { get; set; } = new List<DefinitionPackFileValidationResult>();
    public DateTime LoadedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DefinitionPackValidationResult
{
    public bool IsValid { get; set; }
    public int DefinitionCount { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<string> CrossReferenceErrors { get; set; } = new List<string>();
    public List<string> CrossReferenceWarnings { get; set; } = new List<string>();
    public List<DefinitionPackFileValidationResult> FileResults { get; set; } = new List<DefinitionPackFileValidationResult>();
}

public sealed class DefinitionPackFileValidationResult
{
    public string Category { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int DefinitionCount { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class DefinitionPackImportOptions
{
    public bool DryRun { get; set; } = true;
    public bool AllowOverwrite { get; set; }
    public bool IncludeArchived { get; set; }
    public bool ValidateOnly { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}
