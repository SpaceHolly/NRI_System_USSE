using System.Collections.Generic;
using System.Linq;
using Nri.Server.Application.Services;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope SystemFeatureFlagsSnapshot(CommandContext context)
    {
        RequireAdmin(context);
        var snapshot = _featureFlags.GetFeatureFlagSnapshot();
        return Ok("Feature flag snapshot loaded.", FeatureFlagSnapshotPayload(snapshot));
    }

    public ResponseEnvelope FeatureFlagsAdminList(CommandContext context)
    {
        RequireAdmin(context);
        var snapshot = _featureFlags.GetFeatureFlagSnapshot();
        return Ok("Feature flags loaded.", FeatureFlagSnapshotPayload(snapshot));
    }

    public ResponseEnvelope FeatureFlagsAdminGet(CommandContext context)
    {
        RequireAdmin(context);
        var name = PayloadReader.GetString(context.Request.Payload, "name") ?? PayloadReader.GetString(context.Request.Payload, "flagName") ?? string.Empty;
        var item = _featureFlags.GetFeatureFlag(name);
        if (item == null) return Error("Feature flag not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Feature flag loaded.", new Dictionary<string, object> { { "flag", FeatureFlagSnapshotItemPayload(item) } });
    }

    public ResponseEnvelope FeatureFlagsAdminSetOverride(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var name = PayloadReader.GetString(context.Request.Payload, "name") ?? PayloadReader.GetString(context.Request.Payload, "flagName") ?? string.Empty;
        var value = PayloadReader.GetBool(context.Request.Payload, "value");
        var reason = PayloadReader.GetString(context.Request.Payload, "reason") ?? string.Empty;
        var item = _featureFlags.SetOverride(name, value, actor.Id, reason);
        return Ok("Feature flag override saved.", new Dictionary<string, object>
        {
            { "flag", FeatureFlagSnapshotItemPayload(item) },
            { "snapshot", FeatureFlagSnapshotPayload(_featureFlags.GetFeatureFlagSnapshot()) }
        });
    }

    public ResponseEnvelope FeatureFlagsAdminClearOverride(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var name = PayloadReader.GetString(context.Request.Payload, "name") ?? PayloadReader.GetString(context.Request.Payload, "flagName") ?? string.Empty;
        var item = _featureFlags.ClearOverride(name, actor.Id);
        return Ok("Feature flag override cleared.", new Dictionary<string, object>
        {
            { "flag", FeatureFlagSnapshotItemPayload(item) },
            { "snapshot", FeatureFlagSnapshotPayload(_featureFlags.GetFeatureFlagSnapshot()) }
        });
    }

    public ResponseEnvelope FeatureFlagsAdminRefresh(CommandContext context)
    {
        RequireAdmin(context);
        return Ok("Feature flags refreshed.", FeatureFlagSnapshotPayload(_featureFlags.GetFeatureFlagSnapshot()));
    }

    public ResponseEnvelope CombatV1SmokeRun(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CombatV1SmokeEnabled())
        {
            _logger.Admin($"combat.v1.smoke.disabled command={context.Request.Command}");
            return Error("combat v1 smoke endpoint disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var request = ParseCombatMvpSmokeRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        var result = CombatV1SmokeService().RunCombatMvpSmokeAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Combat v1 smoke completed.", CombatMvpSmokeResultPayload(result));
    }

    private bool CombatV1SmokeEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatInitiativeOrder))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatTurnEngine))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatLogReadEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSnapshotReadEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDiagnosticsEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatActionEconomySkeleton))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatActionDeclareEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAttackRollMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatHitCalculationMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAttackActionEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDefenseMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDefensePreviewEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDamageMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDamageApplicationEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatParticipantVitals))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionsMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionApplyEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionRemoveEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionReadEndpoint))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatWeaponIntegrationMvp))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatMvpSmokeEndpoint));
    }

    private ICombatMvpSmokeService CombatV1SmokeService()
    {
        if (_combatMvpSmokeService == null)
            throw new System.InvalidOperationException("Combat v1 smoke service unavailable.");
        return _combatMvpSmokeService;
    }

    private static CombatMvpSmokeRequest ParseCombatMvpSmokeRequest(IDictionary<string, object> payload)
    {
        return new CombatMvpSmokeRequest
        {
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
            SessionId = PayloadReader.GetString(payload, "sessionId") ?? string.Empty,
            RuleSetId = PayloadReader.GetString(payload, "ruleSetId") ?? string.Empty,
            RunWriteSmoke = PayloadReader.GetBool(payload, "runWriteSmoke"),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static Dictionary<string, object> FeatureFlagSnapshotPayload(FeatureFlagSnapshot snapshot)
    {
        return new Dictionary<string, object>
        {
            { "environment", snapshot.Environment ?? string.Empty },
            { "overridesAllowed", snapshot.OverridesAllowed },
            { "flags", snapshot.Flags.Select(FeatureFlagSnapshotItemPayload).Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> FeatureFlagSnapshotItemPayload(FeatureFlagSnapshotItem item)
    {
        return new Dictionary<string, object>
        {
            { "name", item.Name ?? string.Empty },
            { "category", item.Category ?? string.Empty },
            { "defaultValue", item.DefaultValue },
            { "effectiveValue", item.EffectiveValue },
            { "source", item.Source ?? "default" },
            { "description", item.Description ?? string.Empty },
            { "updatedAtUtc", item.UpdatedAtUtc.HasValue ? (object)item.UpdatedAtUtc.Value : string.Empty },
            { "updatedByUserId", item.UpdatedByUserId ?? string.Empty }
        };
    }

    private static Dictionary<string, object> CombatMvpSmokeResultPayload(CombatMvpSmokeResult result)
    {
        return new Dictionary<string, object>
        {
            { "success", result.Success },
            { "createdEncounterId", result.CreatedEncounterId ?? string.Empty },
            { "steps", result.Steps.Select(CombatMvpSmokeStepResultPayload).Cast<object>().ToArray() },
            { "errors", result.Errors.Cast<object>().ToArray() },
            { "warnings", result.Warnings.Cast<object>().ToArray() },
            { "checkedAtUtc", result.CheckedAtUtc }
        };
    }

    private static Dictionary<string, object> CombatMvpSmokeStepResultPayload(CombatMvpSmokeStepResult step)
    {
        return new Dictionary<string, object>
        {
            { "stepName", step.StepName ?? string.Empty },
            { "success", step.Success },
            { "message", step.Message ?? string.Empty },
            { "errors", step.Errors.Cast<object>().ToArray() },
            { "warnings", step.Warnings.Cast<object>().ToArray() }
        };
    }
}
