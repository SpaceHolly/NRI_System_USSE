using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IInventoryDiagnosticsService
{
    Task<InventoryDiagnosticsResponse> RunFullDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor);
    Task<InventoryDiagnosticsResponse> RunSlotDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor);
    Task<InventoryDiagnosticsResponse> RunItemStateDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor);
    Task<InventoryDiagnosticsResponse> RunCompatibilityDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor);
    Task<InventoryDiagnosticsResponse> RunLegalityDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor);
}

public sealed class InventoryDiagnosticsService : IInventoryDiagnosticsService
{
    private readonly ICharacterProfileService _profiles;
    private readonly IEquipmentSlotValidator? _slotValidator;
    private readonly IInventoryItemStateValidator? _itemStateValidator;
    private readonly IWeaponArmorAmmoCompatibilityValidator? _compatibilityValidator;
    private readonly IInventoryLegalityReadModelService? _legalityService;
    private readonly IItemEquipmentDefinitionResolver? _definitionResolver;
    private readonly IServerLogger? _logger;

    public InventoryDiagnosticsService(
        ICharacterProfileService profiles,
        IEquipmentSlotValidator? slotValidator,
        IInventoryItemStateValidator? itemStateValidator,
        IWeaponArmorAmmoCompatibilityValidator? compatibilityValidator,
        IInventoryLegalityReadModelService? legalityService,
        IItemEquipmentDefinitionResolver? definitionResolver,
        IServerLogger? logger = null)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _slotValidator = slotValidator;
        _itemStateValidator = itemStateValidator;
        _compatibilityValidator = compatibilityValidator;
        _legalityService = legalityService;
        _definitionResolver = definitionResolver;
        _logger = logger;
    }

    public async Task<InventoryDiagnosticsResponse> RunFullDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor)
    {
        var normalized = NormalizeRequest(request);
        return await RunDiagnosticsAsync(normalized, actor, normalized.IncludeSlotValidation, normalized.IncludeItemStateValidation, normalized.IncludeCompatibilityValidation, normalized.IncludeLegalityValidation);
    }

    public async Task<InventoryDiagnosticsResponse> RunSlotDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor)
    {
        var normalized = NormalizeRequest(request);
        return await RunDiagnosticsAsync(normalized, actor, includeSlots: true, includeItems: false, includeCompatibility: false, includeLegality: false);
    }

    public async Task<InventoryDiagnosticsResponse> RunItemStateDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor)
    {
        var normalized = NormalizeRequest(request);
        return await RunDiagnosticsAsync(normalized, actor, includeSlots: false, includeItems: true, includeCompatibility: false, includeLegality: false);
    }

    public async Task<InventoryDiagnosticsResponse> RunCompatibilityDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor)
    {
        var normalized = NormalizeRequest(request);
        return await RunDiagnosticsAsync(normalized, actor, includeSlots: false, includeItems: false, includeCompatibility: true, includeLegality: false);
    }

    public async Task<InventoryDiagnosticsResponse> RunLegalityDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor)
    {
        var normalized = NormalizeRequest(request);
        return await RunDiagnosticsAsync(normalized, actor, includeSlots: false, includeItems: false, includeCompatibility: false, includeLegality: true);
    }

    private async Task<InventoryDiagnosticsResponse> RunDiagnosticsAsync(InventoryDiagnosticsRequest request, UserAccount actor, bool includeSlots, bool includeItems, bool includeCompatibility, bool includeLegality)
    {
        _ = actor;
        if (string.IsNullOrWhiteSpace(request.CharacterId)) throw new ArgumentException("characterId is required");

        var sections = string.Join(",", new[]
        {
            includeSlots ? "slots" : string.Empty,
            includeItems ? "items" : string.Empty,
            includeCompatibility ? "compatibility" : string.Empty,
            includeLegality ? "legality" : string.Empty
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
        _logger?.Debug($"inventory.diagnostics.start characterId={request.CharacterId} sections={sections}");

        var profile = _profiles.GetInventoryProfile(request.CharacterId);
        var ruleSetId = FirstNonEmpty(request.RuleSetId, profile.RuleSetId, RuleSetIds.FantasyNriDefault);
        var items = (profile.Items ?? new List<CharacterInventoryItemProfileValue>())
            .Where(x => x != null)
            .Select(InventoryDomainMapper.ToItemInstanceState)
            .ToList();
        var loadout = InventoryDomainMapper.BuildLoadoutFromInventoryProfile(profile);
        var response = CreateResponse(request, ruleSetId, items);

        if (includeSlots) AddSection(response, await RunSlotSectionAsync(request, ruleSetId, items, loadout), request.IncludeWarnings);
        if (includeItems) AddSection(response, await RunItemStateSectionAsync(request, ruleSetId, items), request.IncludeWarnings);
        if (includeCompatibility) AddSection(response, await RunCompatibilitySectionAsync(request, ruleSetId, items, loadout), request.IncludeWarnings);
        if (includeLegality) AddSection(response, await RunLegalitySectionAsync(request, ruleSetId, items), request.IncludeWarnings);

        FinalizeResponse(response);
        _logger?.Debug($"inventory.diagnostics.done characterId={request.CharacterId} errors={response.Errors.Count} warnings={response.Warnings.Count}");
        return response;
    }

    private async Task<InventoryDiagnosticsSection> RunSlotSectionAsync(InventoryDiagnosticsRequest request, string ruleSetId, List<InventoryItemInstanceState> items, EquipmentLoadoutState loadout)
    {
        if (_slotValidator == null) return UnavailableSection("slots", "slot_validator_unavailable", "Equipment slot validator is unavailable.");

        var result = await _slotValidator.ValidateLoadoutAsync(new EquipmentValidationRequest
        {
            CharacterId = request.CharacterId,
            RuleSetId = ruleSetId,
            Items = items,
            Loadout = loadout,
            StrictMode = request.StrictMode,
            RequestId = request.RequestId
        });

        return new InventoryDiagnosticsSection
        {
            Section = "slots",
            IsValid = result.IsValid,
            Errors = result.Errors ?? new List<InventoryValidationIssue>(),
            Warnings = result.Warnings ?? new List<InventoryValidationIssue>()
        };
    }

    private async Task<InventoryDiagnosticsSection> RunItemStateSectionAsync(InventoryDiagnosticsRequest request, string ruleSetId, List<InventoryItemInstanceState> items)
    {
        if (_itemStateValidator == null) return UnavailableSection("items", "item_state_validator_unavailable", "Inventory item state validator is unavailable.");
        return FromValidationResult("items", await _itemStateValidator.ValidateItemsAsync(items, ruleSetId, request.StrictMode));
    }

    private async Task<InventoryDiagnosticsSection> RunCompatibilitySectionAsync(InventoryDiagnosticsRequest request, string ruleSetId, List<InventoryItemInstanceState> items, EquipmentLoadoutState loadout)
    {
        if (_compatibilityValidator == null) return UnavailableSection("compatibility", "compatibility_validator_unavailable", "Weapon/armor/ammo compatibility validator is unavailable.");

        var section = new InventoryDiagnosticsSection { Section = "compatibility" };
        Merge(section, await _compatibilityValidator.ValidateEquippedWeaponAmmoSetAsync(items, loadout, ruleSetId, request.StrictMode));

        if (_definitionResolver == null)
        {
            section.Warnings.Add(Issue("definition_resolver_unavailable", "warning", "Item/equipment definition resolver is unavailable.", string.Empty, string.Empty));
            return section;
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.DefinitionId)) continue;

            var weapon = await _definitionResolver.ResolveWeaponAsync(item.DefinitionId, ruleSetId);
            if (weapon.Success && weapon.Value != null)
            {
                Merge(section, await _compatibilityValidator.ValidateWeaponAsync(item, ruleSetId, request.StrictMode));
                continue;
            }

            var armor = await _definitionResolver.ResolveArmorAsync(item.DefinitionId, ruleSetId);
            if (armor.Success && armor.Value != null)
            {
                Merge(section, await _compatibilityValidator.ValidateArmorAsync(item, ruleSetId, request.StrictMode));
                continue;
            }

            var ammo = await _definitionResolver.ResolveAmmoAsync(item.DefinitionId, ruleSetId);
            if (ammo.Success && ammo.Value != null)
            {
                Merge(section, await _compatibilityValidator.ValidateAmmoAsync(item, ruleSetId, request.StrictMode));
            }
        }

        section.IsValid = section.Errors.Count == 0;
        return section;
    }

    private async Task<InventoryDiagnosticsSection> RunLegalitySectionAsync(InventoryDiagnosticsRequest request, string ruleSetId, List<InventoryItemInstanceState> items)
    {
        if (_legalityService == null) return UnavailableSection("legality", "legality_service_unavailable", "Inventory legality read model service is unavailable.");

        var result = await _legalityService.EvaluateInventoryLegalityAsync(new InventoryLegalityBatchRequest
        {
            Items = items,
            Context = new InventoryLegalityContext
            {
                RuleSetId = ruleSetId,
                CampaignId = request.CampaignId,
                CountryId = request.CountryId,
                CityStateId = request.CityStateId,
                LocationId = request.LocationId,
                ActorCharacterId = request.CharacterId,
                IncludeRuntimeStates = InventoryFeatureFlags.UseRuntimeLawRestrictionLookup,
                StrictMode = request.StrictMode,
                RequestId = request.RequestId
            }
        });

        return new InventoryDiagnosticsSection
        {
            Section = "legality",
            IsValid = result.IsValid,
            Errors = result.Errors ?? new List<InventoryValidationIssue>(),
            Warnings = result.Warnings ?? new List<InventoryValidationIssue>()
        };
    }

    private static InventoryDiagnosticsRequest NormalizeRequest(InventoryDiagnosticsRequest request)
    {
        return request ?? new InventoryDiagnosticsRequest();
    }

    private static InventoryDiagnosticsResponse CreateResponse(InventoryDiagnosticsRequest request, string ruleSetId, List<InventoryItemInstanceState> items)
    {
        return new InventoryDiagnosticsResponse
        {
            CharacterId = request.CharacterId ?? string.Empty,
            RuleSetId = ruleSetId ?? string.Empty,
            CampaignId = request.CampaignId ?? string.Empty,
            CheckedAtUtc = DateTime.UtcNow,
            Summary = new InventoryDiagnosticsSummary
            {
                ItemCount = items.Count,
                EquippedItemCount = items.Count(x => x.IsEquipped)
            }
        };
    }

    private static void AddSection(InventoryDiagnosticsResponse response, InventoryDiagnosticsSection section, bool includeWarnings)
    {
        if (section == null) return;
        if (!includeWarnings) section.Warnings = new List<InventoryValidationIssue>();

        section.IsValid = (section.Errors ?? new List<InventoryValidationIssue>()).Count == 0;
        response.Sections.Add(section);
        response.Errors.AddRange(section.Errors ?? new List<InventoryValidationIssue>());
        response.Warnings.AddRange(section.Warnings ?? new List<InventoryValidationIssue>());
    }

    private static void FinalizeResponse(InventoryDiagnosticsResponse response)
    {
        response.IsValid = response.Errors.Count == 0;
        response.Summary.ErrorCount = response.Errors.Count;
        response.Summary.WarningCount = response.Warnings.Count;
        response.Summary.SlotErrorCount = CountErrors(response, "slots");
        response.Summary.ItemStateErrorCount = CountErrors(response, "items");
        response.Summary.CompatibilityErrorCount = CountErrors(response, "compatibility");
        response.Summary.LegalityWarningCount = response.Sections.FirstOrDefault(x => string.Equals(x.Section, "legality", StringComparison.OrdinalIgnoreCase))?.Warnings.Count ?? 0;
    }

    private static int CountErrors(InventoryDiagnosticsResponse response, string section)
    {
        return response.Sections.FirstOrDefault(x => string.Equals(x.Section, section, StringComparison.OrdinalIgnoreCase))?.Errors.Count ?? 0;
    }

    private static InventoryDiagnosticsSection FromValidationResult(string section, InventoryValidationResult result)
    {
        var output = new InventoryDiagnosticsSection
        {
            Section = section,
            IsValid = result == null || result.IsValid,
            Errors = result?.Issues?.Where(x => string.Equals(x.Severity, "error", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<InventoryValidationIssue>(),
            Warnings = result?.Issues?.Where(x => !string.Equals(x.Severity, "error", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<InventoryValidationIssue>()
        };

        AddStringIssues(output.Errors, result?.Errors, "error");
        AddStringIssues(output.Warnings, result?.Warnings, "warning");
        output.IsValid = output.Errors.Count == 0;
        return output;
    }

    private static InventoryDiagnosticsSection UnavailableSection(string section, string code, string message)
    {
        return new InventoryDiagnosticsSection
        {
            Section = section,
            IsValid = true,
            Warnings = new List<InventoryValidationIssue> { Issue(code, "warning", message, string.Empty, string.Empty) }
        };
    }

    private static void Merge(InventoryDiagnosticsSection target, InventoryValidationResult source)
    {
        if (target == null || source == null) return;
        var converted = FromValidationResult(target.Section, source);
        target.Errors.AddRange(converted.Errors);
        target.Warnings.AddRange(converted.Warnings);
        target.IsValid = target.Errors.Count == 0;
    }

    private static void AddStringIssues(List<InventoryValidationIssue> target, IEnumerable<string>? codes, string severity)
    {
        foreach (var code in codes ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(code)) continue;
            if (target.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) continue;
            target.Add(Issue(code, severity, code, string.Empty, string.Empty));
        }
    }

    private static InventoryValidationIssue Issue(string code, string severity, string message, string itemInstanceId, string definitionId)
    {
        return new InventoryValidationIssue
        {
            Code = code ?? string.Empty,
            Severity = severity ?? string.Empty,
            Message = message ?? string.Empty,
            ItemInstanceId = itemInstanceId ?? string.Empty,
            DefinitionId = definitionId ?? string.Empty
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return string.Empty;
    }
}
