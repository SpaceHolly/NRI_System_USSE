using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nri.Server.FateEngine;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatFateHookService
{
    Task<CombatFateHookResult> ApplyFateToAttackRollAsync(CombatFateHookRequest request, UserAccount actor);
    Task<CombatFateHookResult> ApplyFateToDamageRollAsync(CombatFateHookRequest request, UserAccount actor);
    CombatFateHookResult BuildFateBreakdown(FateEngineResult result, string rollContext);
    Dictionary<string, object> BuildFateLogSummary(CombatFateHookResult result);
}

public sealed class CombatFateHookService : ICombatFateHookService
{
    private static readonly Regex DiceSidesRegex = new Regex(@"d(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly FateEngineStateService _fateState;
    private readonly IServerLogger _logger;

    public CombatFateHookService(FateEngineStateService fateState, IServerLogger logger)
    {
        _fateState = fateState;
        _logger = logger;
    }

    public Task<CombatFateHookResult> ApplyFateToAttackRollAsync(CombatFateHookRequest request, UserAccount actor)
    {
        return Task.FromResult(ApplyFate(request, "attack_roll", CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateAttackModifier))));
    }

    public Task<CombatFateHookResult> ApplyFateToDamageRollAsync(CombatFateHookRequest request, UserAccount actor)
    {
        return Task.FromResult(ApplyFate(request, "damage_roll", CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateDamageModifier))));
    }

    public CombatFateHookResult BuildFateBreakdown(FateEngineResult result, string rollContext)
    {
        var baseRoll = result?.BaseRoll ?? 0;
        var fateValue = result?.FateValue ?? baseRoll;
        var hook = new CombatFateHookResult
        {
            Applied = result?.Applied ?? false,
            RollContext = rollContext ?? string.Empty,
            BaseRoll = baseRoll,
            FateModifiedRoll = fateValue,
            FateModifier = fateValue - baseRoll,
            FateSummary = result == null
                ? "Fate unavailable."
                : result.Applied
                    ? $"Fate modifier: {FormatSigned(fateValue - baseRoll)}."
                    : $"Fate not applied: {result.SkippedReason}"
        };

        if (result != null && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateBreakdownInResponse)))
        {
            hook.FateLayerSummaries = result.Layers
                .OrderBy(x => x.LayerNumber)
                .Select(x => new CombatFateLayerSummary
                {
                    LayerIndex = x.LayerNumber,
                    LayerName = SafeText(x.LayerName, 80),
                    LayerType = SafeText(x.InfluenceType, 80),
                    Modifier = x.Modifier,
                    IsEnabled = x.Enabled,
                    Summary = SafeText(string.IsNullOrWhiteSpace(x.Reason) ? x.EffectDisplayName : x.Reason, 160)
                })
                .ToList();
        }

        return hook;
    }

    public Dictionary<string, object> BuildFateLogSummary(CombatFateHookResult result)
    {
        return new Dictionary<string, object>
        {
            { "fateApplied", result?.Applied ?? false },
            { "fateModifier", result?.FateModifier ?? 0 },
            { "fateSummary", SafeText(result?.FateSummary, 200) },
            { "rollContext", result?.RollContext ?? string.Empty }
        };
    }

    private CombatFateHookResult ApplyFate(CombatFateHookRequest request, string expectedContext, bool contextFlagEnabled)
    {
        request ??= new CombatFateHookRequest();
        _logger.Debug($"combat.fate.hook.start encounterId={request.EncounterId} context={request.RollContext}");

        var result = new CombatFateHookResult
        {
            Applied = false,
            RollContext = string.IsNullOrWhiteSpace(request.RollContext) ? expectedContext : request.RollContext,
            BaseRoll = request.BaseRoll,
            FateModifiedRoll = request.BaseRoll,
            FateModifier = 0
        };

        if (!CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1)) || !CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatFateHookMvp)))
            return WithWarning(result, "fate_hook_disabled");
        if (!request.UseFateEngine)
            return WithWarning(result, "fate_request_disabled");
        if (!contextFlagEnabled)
            return WithWarning(result, "fate_context_modifier_disabled");

        try
        {
            var settings = _fateState.GetSnapshot();
            if (settings == null || !settings.Enabled)
                return WithWarning(result, "fate_engine_unavailable_or_disabled");

            var dieSides = ResolveDieSides(request.DiceExpression, expectedContext);
            var fateResult = new FateEnginePipeline().Process(new FateEngineRequest
            {
                BaseRoll = Math.Max(0, request.BaseRoll),
                DieSides = dieSides,
                RollType = $"combat_{expectedContext}",
                ActorId = request.ActorParticipantId ?? string.Empty,
                SceneId = string.IsNullOrWhiteSpace(request.EncounterId) ? "combat" : request.EncounterId
            }, settings);

            result = BuildFateBreakdown(fateResult, expectedContext);
            if (!fateResult.Applied)
                result.Warnings.Add("fate_engine_unavailable_or_disabled");
            _logger.Debug($"combat.fate.hook.done applied={result.Applied} modifier={result.FateModifier}");
            return result;
        }
        catch
        {
            return WithWarning(result, "fate_engine_unavailable_or_disabled");
        }
    }

    private CombatFateHookResult WithWarning(CombatFateHookResult result, string warning)
    {
        result.Warnings.Add(warning);
        result.FateSummary = warning;
        _logger.Debug($"combat.fate.hook.warning code={warning}");
        return result;
    }

    private static int ResolveDieSides(string diceExpression, string rollContext)
    {
        if (string.Equals(rollContext, "attack_roll", StringComparison.OrdinalIgnoreCase)) return 20;
        var match = DiceSidesRegex.Match(diceExpression ?? string.Empty);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var sides) && sides > 1) return sides;
        return 20;
    }

    private static string FormatSigned(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    private static string SafeText(string? value, int maxLength)
    {
        var text = value ?? string.Empty;
        return text.Length <= maxLength ? text : text.Substring(0, maxLength);
    }
}
