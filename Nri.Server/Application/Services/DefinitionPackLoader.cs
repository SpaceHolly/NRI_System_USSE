using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IDefinitionPackLoader
{
    Task<DefinitionPackManifest> LoadManifestAsync(string packDirectory);
    Task<IReadOnlyCollection<UnifiedDefinitionDocument>> LoadDefinitionsAsync(string packDirectory, DefinitionPackManifest manifest);
    Task<DefinitionPackValidationResult> ValidatePackAsync(string packDirectory);
    Task<DefinitionPackLoadResult> DryRunImportAsync(string packDirectory);
}

public sealed class DefinitionPackLoader : IDefinitionPackLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServerLogger _logger;
    private readonly DefinitionPackCrossReferenceValidator _crossReferenceValidator = new DefinitionPackCrossReferenceValidator();

    public DefinitionPackLoader(IServerLogger logger)
    {
        _logger = logger;
    }

    public Task<DefinitionPackManifest> LoadManifestAsync(string packDirectory)
    {
        _logger.Debug($"definition.pack.load.start path={SafePath(packDirectory)}");
        var manifestPath = Path.Combine(packDirectory ?? string.Empty, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Definition pack manifest not found.", manifestPath);
        }

        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<DefinitionPackManifest>(json, JsonOptions) ?? new DefinitionPackManifest();
        NormalizeManifest(manifest);
        _logger.Debug($"definition.pack.load.manifest packId={manifest.PackId} files={manifest.Files.Count}");
        return Task.FromResult(manifest);
    }

    public Task<IReadOnlyCollection<UnifiedDefinitionDocument>> LoadDefinitionsAsync(string packDirectory, DefinitionPackManifest manifest)
    {
        var definitions = new List<UnifiedDefinitionDocument>();
        var safeManifest = manifest ?? new DefinitionPackManifest();
        NormalizeManifest(safeManifest);

        foreach (var file in safeManifest.Files)
        {
            var fullPath = ResolvePackFilePath(packDirectory, file.Path);
            if (!File.Exists(fullPath))
            {
                if (file.Required)
                {
                    throw new FileNotFoundException("Required definition pack file not found.", fullPath);
                }

                continue;
            }

            var json = File.ReadAllText(fullPath);
            var loaded = JsonSerializer.Deserialize<List<UnifiedDefinitionDocument>>(json, JsonOptions) ?? new List<UnifiedDefinitionDocument>();
            foreach (var definition in loaded)
            {
                NormalizeDefinition(definition, file.Category, safeManifest.RuleSetId);
                definitions.Add(definition);
            }

            _logger.Debug($"definition.pack.load.file path={SafePath(file.Path)} category={file.Category} count={loaded.Count}");
        }

        return Task.FromResult<IReadOnlyCollection<UnifiedDefinitionDocument>>(definitions);
    }

    public Task<DefinitionPackValidationResult> ValidatePackAsync(string packDirectory)
    {
        var result = new DefinitionPackValidationResult();
        var packId = PackIdForLog(packDirectory);
        try
        {
            var manifest = LoadManifestAsync(packDirectory).GetAwaiter().GetResult();
            packId = manifest.PackId;
            ValidateManifest(manifest, result);
            var definitions = LoadDefinitionsForValidation(packDirectory, manifest, result);
            result.DefinitionCount = definitions.Count;

            foreach (var definition in definitions)
            {
                ValidateDefinition(definition, result);
            }

            var index = _crossReferenceValidator.BuildIndex(definitions);
            var crossReferenceResult = _crossReferenceValidator.ValidateReferences(index, definitions, manifest.RuleSetId);
            result.CrossReferenceErrors.AddRange(crossReferenceResult.CrossReferenceErrors);
            result.CrossReferenceWarnings.AddRange(crossReferenceResult.CrossReferenceWarnings);
            result.Errors.AddRange(crossReferenceResult.Errors);
            result.Warnings.AddRange(crossReferenceResult.Warnings);

            if (definitions.Count == 0)
            {
                result.Warnings.Add("definition_count=0");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
        }

        result.IsValid = result.Errors.Count == 0 && result.CrossReferenceErrors.Count == 0;
        _logger.Debug($"definition.pack.validate.done packId={packId} valid={result.IsValid} definitions={result.DefinitionCount} errors={result.Errors.Count + result.CrossReferenceErrors.Count} warnings={result.Warnings.Count + result.CrossReferenceWarnings.Count}");
        return Task.FromResult(result);
    }

    public Task<DefinitionPackLoadResult> DryRunImportAsync(string packDirectory)
    {
        var result = new DefinitionPackLoadResult();
        try
        {
            _logger.Debug($"definition.pack.dryrun.start path={SafePath(packDirectory)}");
            var manifest = LoadManifestAsync(packDirectory).GetAwaiter().GetResult();
            result.PackId = manifest.PackId;
            var validation = ValidatePackAsync(packDirectory).GetAwaiter().GetResult();
            result.Success = validation.IsValid;
            result.LoadedDefinitions = validation.DefinitionCount;
            result.Errors = validation.Errors ?? new List<string>();
            result.Warnings = validation.Warnings ?? new List<string>();
            result.CrossReferenceErrors = validation.CrossReferenceErrors ?? new List<string>();
            result.CrossReferenceWarnings = validation.CrossReferenceWarnings ?? new List<string>();
            result.FileResults = validation.FileResults ?? new List<DefinitionPackFileValidationResult>();
            result.LoadedFiles = manifest.Files
                .Where(x => File.Exists(ResolvePackFilePath(packDirectory, x.Path)))
                .Select(x => x.Path)
                .ToList();
            result.LoadedAtUtc = DateTime.UtcNow;
            foreach (var error in result.CrossReferenceErrors)
            {
                _logger.Debug($"definition.pack.crossref.error packId={result.PackId} message={error}");
            }

            foreach (var warning in result.CrossReferenceWarnings)
            {
                _logger.Debug($"definition.pack.crossref.warning packId={result.PackId} message={warning}");
            }

            _logger.Debug("definition.pack.import.skipped reason=dry_run_only");
            _logger.Debug($"definition.pack.dryrun.done packId={result.PackId} success={result.Success} errors={result.Errors.Count + result.CrossReferenceErrors.Count} warnings={result.Warnings.Count + result.CrossReferenceWarnings.Count}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            result.LoadedAtUtc = DateTime.UtcNow;
            _logger.Debug("definition.pack.import.skipped reason=dry_run_only");
            _logger.Debug($"definition.pack.dryrun.done packId={PackIdForLog(packDirectory)} success=False errors={result.Errors.Count} warnings={result.Warnings.Count}");
        }

        return Task.FromResult(result);
    }

    private List<UnifiedDefinitionDocument> LoadDefinitionsForValidation(string packDirectory, DefinitionPackManifest manifest, DefinitionPackValidationResult result)
    {
        var definitions = new List<UnifiedDefinitionDocument>();
        var safeManifest = manifest ?? new DefinitionPackManifest();
        NormalizeManifest(safeManifest);

        foreach (var file in safeManifest.Files)
        {
            var fileResult = new DefinitionPackFileValidationResult
            {
                Category = file.Category,
                Path = file.Path
            };
            result.FileResults.Add(fileResult);

            string fullPath;
            try
            {
                fullPath = ResolvePackFilePath(packDirectory, file.Path);
            }
            catch (Exception ex)
            {
                var message = $"file_path_invalid:{file.Path}:{ex.Message}";
                fileResult.Errors.Add(message);
                result.Errors.Add(message);
                continue;
            }

            if (!File.Exists(fullPath))
            {
                var message = file.Required ? $"required_file_missing:{file.Path}" : $"optional_file_missing:{file.Path}";
                if (file.Required)
                {
                    fileResult.Errors.Add(message);
                    result.Errors.Add(message);
                }
                else
                {
                    fileResult.Warnings.Add(message);
                    result.Warnings.Add(message);
                }

                continue;
            }

            List<UnifiedDefinitionDocument> loaded;
            try
            {
                var json = File.ReadAllText(fullPath);
                loaded = JsonSerializer.Deserialize<List<UnifiedDefinitionDocument>>(json, JsonOptions) ?? new List<UnifiedDefinitionDocument>();
            }
            catch (JsonException ex)
            {
                var message = $"definition_file_invalid_json:{file.Path}:{ex.Message}";
                fileResult.Errors.Add(message);
                result.Errors.Add(message);
                continue;
            }
            catch (Exception ex)
            {
                var message = $"definition_file_read_failed:{file.Path}:{ex.Message}";
                fileResult.Errors.Add(message);
                result.Errors.Add(message);
                continue;
            }

            foreach (var definition in loaded)
            {
                NormalizeDefinition(definition, file.Category, safeManifest.RuleSetId);
                definitions.Add(definition);
            }

            fileResult.DefinitionCount = loaded.Count;
            if (file.ExpectedMinCount > 0 && loaded.Count < file.ExpectedMinCount)
            {
                var message = $"file_min_count_not_met:{file.Path}:{loaded.Count}/{file.ExpectedMinCount}";
                if (file.Required)
                {
                    fileResult.Errors.Add(message);
                    result.Errors.Add(message);
                }
                else
                {
                    fileResult.Warnings.Add(message);
                    result.Warnings.Add(message);
                }
            }

            if (loaded.Count == 0)
            {
                var message = $"definition_file_empty:{file.Path}";
                fileResult.Warnings.Add(message);
                result.Warnings.Add(message);
            }

            _logger.Debug($"definition.pack.load.file path={SafePath(file.Path)} category={file.Category} count={loaded.Count}");
        }

        return definitions;
    }

    private static void NormalizeManifest(DefinitionPackManifest manifest)
    {
        manifest.PackId = (manifest.PackId ?? string.Empty).Trim();
        manifest.Name = (manifest.Name ?? string.Empty).Trim();
        manifest.RuleSetId = (manifest.RuleSetId ?? string.Empty).Trim();
        manifest.Version = (manifest.Version ?? string.Empty).Trim();
        manifest.Description = manifest.Description ?? string.Empty;
        manifest.Author = manifest.Author ?? string.Empty;
        manifest.Tags = NormalizeStringList(manifest.Tags);
        manifest.Files = manifest.Files ?? new List<DefinitionPackFile>();
        if (manifest.SchemaVersion < 1) manifest.SchemaVersion = 1;

        foreach (var file in manifest.Files)
        {
            file.Category = (file.Category ?? string.Empty).Trim();
            file.Path = (file.Path ?? string.Empty).Trim();
            file.Notes = file.Notes ?? string.Empty;
        }
    }

    private static void ValidateManifest(DefinitionPackManifest manifest, DefinitionPackValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(manifest.PackId)) result.Errors.Add("manifest_pack_id_required");
        if (string.IsNullOrWhiteSpace(manifest.Name)) result.Errors.Add("manifest_name_required");
        if (string.IsNullOrWhiteSpace(manifest.RuleSetId)) result.Errors.Add("manifest_ruleset_required");
        if (manifest.SchemaVersion < 1) result.Errors.Add("manifest_schema_version_invalid");
        foreach (var file in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Category)) result.Errors.Add("manifest_file_category_required");
            if (string.IsNullOrWhiteSpace(file.Path)) result.Errors.Add("manifest_file_path_required");
            if (file.ExpectedMinCount < 0) result.Errors.Add($"manifest_file_expected_min_count_invalid:{file.Path}");
        }
    }

    private static void NormalizeDefinition(UnifiedDefinitionDocument definition, string category, string ruleSetId)
    {
        if (definition == null) return;
        definition.Id = (definition.Id ?? string.Empty).Trim();
        definition.Category = (definition.Category ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(definition.Category)) definition.Category = (category ?? string.Empty).Trim();
        definition.Name = (definition.Name ?? string.Empty).Trim();
        definition.PublicDescription = definition.PublicDescription ?? string.Empty;
        definition.GMDescription = definition.GMDescription ?? string.Empty;
        definition.VisibilityRule = (definition.VisibilityRule ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(definition.VisibilityRule)) definition.VisibilityRule = VisibilityRuleIds.Public;
        if (definition.SchemaVersion < 1) definition.SchemaVersion = 1;
        definition.RuleSetIds = NormalizeStringList(definition.RuleSetIds);
        if (!string.IsNullOrWhiteSpace(ruleSetId) && !definition.RuleSetIds.Contains(ruleSetId, StringComparer.OrdinalIgnoreCase))
        {
            definition.RuleSetIds.Add(ruleSetId);
        }

        definition.Tags = NormalizeStringList(definition.Tags);
        definition.ServerOnlyData = definition.ServerOnlyData ?? new Dictionary<string, object>();
        definition.ExtraData = definition.ExtraData ?? new Dictionary<string, object>();
        if (definition.CreatedAtUtc == default(DateTime)) definition.CreatedAtUtc = DateTime.UtcNow;
        if (definition.UpdatedAtUtc == default(DateTime)) definition.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ValidateDefinition(UnifiedDefinitionDocument definition, DefinitionPackValidationResult result)
    {
        if (definition == null)
        {
            result.Errors.Add("definition_null");
            return;
        }

        if (string.IsNullOrWhiteSpace(definition.Id)) result.Errors.Add("definition_id_required");
        if (string.IsNullOrWhiteSpace(definition.Category)) result.Errors.Add($"definition_category_required:{definition.Id}");
        if (string.IsNullOrWhiteSpace(definition.Name)) result.Errors.Add($"definition_name_required:{definition.Category}:{definition.Id}");
        if (definition.SchemaVersion < 1) result.Errors.Add($"definition_schema_version_invalid:{definition.Category}:{definition.Id}");
        if (string.IsNullOrWhiteSpace(definition.VisibilityRule))
        {
            result.Warnings.Add($"definition_visibility_defaulted:{definition.Category}:{definition.Id}");
            definition.VisibilityRule = VisibilityRuleIds.Public;
        }

        if (definition.RuleSetIds == null) result.Errors.Add($"definition_rulesets_null:{definition.Category}:{definition.Id}");
        if (definition.Tags == null) result.Errors.Add($"definition_tags_null:{definition.Category}:{definition.Id}");
        if (definition.ServerOnlyData == null) result.Errors.Add($"definition_server_only_data_null:{definition.Category}:{definition.Id}");
        if (definition.ExtraData == null) result.Errors.Add($"definition_extra_data_null:{definition.Category}:{definition.Id}");
    }

    private static string ResolvePackFilePath(string packDirectory, string relativePath)
    {
        var basePath = Path.GetFullPath(packDirectory ?? string.Empty);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath ?? string.Empty));
        var baseWithSeparator = basePath.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? basePath
            : basePath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Definition pack file path escapes pack directory.");
        }

        return fullPath;
    }

    private static List<string> NormalizeStringList(List<string> source)
    {
        return (source ?? new List<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string PackIdForLog(string packDirectory)
    {
        return new DirectoryInfo(packDirectory ?? string.Empty).Name;
    }

    private static string SafePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }
}

public static class DefinitionPackDiagnostics
{
    public static Task<DefinitionPackLoadResult> RunDryRun(string packPath, IServerLogger logger)
    {
        var loader = new DefinitionPackLoader(logger);
        return loader.DryRunImportAsync(packPath);
    }
}
