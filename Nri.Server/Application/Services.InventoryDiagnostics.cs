using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.Application.Services;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope InventoryDiagnosticsFull(CommandContext context) => RunInventoryDiagnosticsCommand(context, "full");
    public ResponseEnvelope InventoryDiagnosticsSlots(CommandContext context) => RunInventoryDiagnosticsCommand(context, "slots");
    public ResponseEnvelope InventoryDiagnosticsItems(CommandContext context) => RunInventoryDiagnosticsCommand(context, "items");
    public ResponseEnvelope InventoryDiagnosticsCompatibility(CommandContext context) => RunInventoryDiagnosticsCommand(context, "compatibility");
    public ResponseEnvelope InventoryDiagnosticsLegality(CommandContext context) => RunInventoryDiagnosticsCommand(context, "legality");

    private ResponseEnvelope RunInventoryDiagnosticsCommand(CommandContext context, string diagnosticsMode)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"inventory.diagnostics.forbidden command={context.Request.Command}");
            throw;
        }

        if (!InventoryFeatureFlags.UseInventoryDiagnosticsEndpoints)
        {
            _logger.Admin($"inventory.diagnostics.disabled command={context.Request.Command}");
            return Error("inventory diagnostics endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var request = ParseInventoryDiagnosticsRequest(context.Request.Payload);
        request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? context.Request.RequestId ?? string.Empty : request.RequestId;
        if (string.IsNullOrWhiteSpace(request.CharacterId))
        {
            return Error("characterId is required", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        }

        _ = GetCharacter(request.CharacterId);

        try
        {
            if (_inventoryDiagnosticsService == null)
            {
                return Error("inventory diagnostics service unavailable", ResponseStatus.Error, ErrorCode.InternalError);
            }

            var result = diagnosticsMode switch
            {
                "slots" => _inventoryDiagnosticsService.RunSlotDiagnosticsAsync(request, actor).GetAwaiter().GetResult(),
                "items" => _inventoryDiagnosticsService.RunItemStateDiagnosticsAsync(request, actor).GetAwaiter().GetResult(),
                "compatibility" => _inventoryDiagnosticsService.RunCompatibilityDiagnosticsAsync(request, actor).GetAwaiter().GetResult(),
                "legality" => _inventoryDiagnosticsService.RunLegalityDiagnosticsAsync(request, actor).GetAwaiter().GetResult(),
                _ => _inventoryDiagnosticsService.RunFullDiagnosticsAsync(request, actor).GetAwaiter().GetResult()
            };

            return Ok("Inventory diagnostics completed.", InventoryDiagnosticsPayload(result));
        }
        catch (Exception ex)
        {
            _logger.Debug($"inventory.diagnostics.error characterId={request.CharacterId} message={ex.Message}");
            throw;
        }
    }

    private static InventoryDiagnosticsRequest ParseInventoryDiagnosticsRequest(IDictionary<string, object> payload)
    {
        return new InventoryDiagnosticsRequest
        {
            CharacterId = PayloadReader.GetString(payload, "characterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            RuleSetId = PayloadReader.GetString(payload, "ruleSetId") ?? string.Empty,
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
            CountryId = PayloadReader.GetString(payload, "countryId") ?? string.Empty,
            CityStateId = PayloadReader.GetString(payload, "cityStateId") ?? string.Empty,
            LocationId = PayloadReader.GetString(payload, "locationId") ?? string.Empty,
            StrictMode = PayloadReader.GetBool(payload, "strictMode"),
            IncludeSlotValidation = GetBoolDefault(payload, "includeSlotValidation", true),
            IncludeItemStateValidation = GetBoolDefault(payload, "includeItemStateValidation", true),
            IncludeCompatibilityValidation = GetBoolDefault(payload, "includeCompatibilityValidation", true),
            IncludeLegalityValidation = GetBoolDefault(payload, "includeLegalityValidation", true),
            IncludeWarnings = GetBoolDefault(payload, "includeWarnings", true),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static Dictionary<string, object> InventoryDiagnosticsPayload(InventoryDiagnosticsResponse response)
    {
        return new Dictionary<string, object>
        {
            { "characterId", response.CharacterId },
            { "ruleSetId", response.RuleSetId },
            { "campaignId", response.CampaignId },
            { "isValid", response.IsValid },
            { "sections", response.Sections.Select(InventoryDiagnosticsSectionPayload).Cast<object>().ToArray() },
            { "errors", response.Errors.Select(InventoryValidationIssuePayload).Cast<object>().ToArray() },
            { "warnings", response.Warnings.Select(InventoryValidationIssuePayload).Cast<object>().ToArray() },
            { "summary", InventoryDiagnosticsSummaryPayload(response.Summary) },
            { "checkedAtUtc", response.CheckedAtUtc }
        };
    }

    private static Dictionary<string, object> InventoryDiagnosticsSectionPayload(InventoryDiagnosticsSection section)
    {
        return new Dictionary<string, object>
        {
            { "section", section.Section },
            { "isValid", section.IsValid },
            { "errors", section.Errors.Select(InventoryValidationIssuePayload).Cast<object>().ToArray() },
            { "warnings", section.Warnings.Select(InventoryValidationIssuePayload).Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> InventoryDiagnosticsSummaryPayload(InventoryDiagnosticsSummary summary)
    {
        return new Dictionary<string, object>
        {
            { "itemCount", summary.ItemCount },
            { "equippedItemCount", summary.EquippedItemCount },
            { "errorCount", summary.ErrorCount },
            { "warningCount", summary.WarningCount },
            { "slotErrorCount", summary.SlotErrorCount },
            { "itemStateErrorCount", summary.ItemStateErrorCount },
            { "compatibilityErrorCount", summary.CompatibilityErrorCount },
            { "legalityWarningCount", summary.LegalityWarningCount }
        };
    }

    private static Dictionary<string, object> InventoryValidationIssuePayload(InventoryValidationIssue issue)
    {
        return new Dictionary<string, object>
        {
            { "code", issue.Code },
            { "severity", issue.Severity },
            { "message", issue.Message },
            { "itemInstanceId", issue.ItemInstanceId },
            { "definitionId", issue.DefinitionId }
        };
    }
}
