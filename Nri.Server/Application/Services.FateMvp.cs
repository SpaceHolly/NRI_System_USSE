using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string FateAcceptanceProfileId = "fate_acceptance_profile_01457";
    private const string FateDefaultStateId = "fate_state_default";
    private const string FateDefaultLayoutId = "default";
    private const string FateHiddenGmToken = "GM_ONLY_FATE_LAYER_01457_DO_NOT_LEAK";

    public ResponseEnvelope FateAdminSeedAcceptanceData(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureFateMvpEnabledOrThrow();
        var characterId = FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "characterId"),
            _repositories.Characters.Find(FilterDefinition<Character>.Empty).FirstOrDefault()?.Id ?? string.Empty);

        var heightCm = PayloadReader.GetInt(context.Request.Payload, "heightCm");
        var focusedCondition = PayloadReader.GetBool(context.Request.Payload, "focusedCondition");
        var hasFocusedConditionOverride = context.Request.Payload.ContainsKey("focusedCondition");
        SeedFateAcceptanceData(actor, characterId, heightCm, hasFocusedConditionOverride ? focusedCondition : (bool?)null);
        return Ok("Fate acceptance data seeded.", new Dictionary<string, object>
        {
            ["profileId"] = FateAcceptanceProfileId,
            ["characterId"] = characterId,
            ["modifierRules"] = FateAcceptanceRuleTokens().Cast<object>().ToArray()
        });
    }

    public ResponseEnvelope FateAdminStateGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!FateMvpCommandsEnabled()) return FateDisabled(context.Request.Command);
        var state = GetOrCreateFateState();
        return Ok("Fate state loaded.", new Dictionary<string, object> { ["state"] = FateDocumentPayload(state, admin: true) });
    }

    public ResponseEnvelope FateAdminStateUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!FateMvpCommandsEnabled()) return FateDisabled(context.Request.Command);

        var state = GetOrCreateFateState();
        var terrain = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "terrainProfile"), ReadString(state, "TerrainProfile"), FateTerrainProfiles.Calm);
        ValidateTerrainProfile(terrain);
        var profileId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "activeProfileId"), ReadString(state, "ActiveProfileId"), FateAcceptanceProfileId);
        if (!FateProfiles().Find(Builders<BsonDocument>.Filter.Eq("ProfileId", profileId)).Any())
            throw new KeyNotFoundException("Fate profile not found.");

        var now = DateTime.UtcNow;
        state["ActiveProfileId"] = profileId;
        state["IsEnabled"] = PayloadReader.GetBool(context.Request.Payload, "isEnabled") || PayloadReader.GetBool(context.Request.Payload, "enabled");
        if (!context.Request.Payload.ContainsKey("isEnabled") && !context.Request.Payload.ContainsKey("enabled"))
            state["IsEnabled"] = ReadBool(state, "IsEnabled");
        state["TerrainProfile"] = terrain;
        state["DramaLevel"] = PayloadReader.GetInt(context.Request.Payload, "dramaLevel") ?? ReadInt(state, "DramaLevel", 0);
        state["ChaosLevel"] = PayloadReader.GetInt(context.Request.Payload, "chaosLevel") ?? ReadInt(state, "ChaosLevel", 0);
        state["AnomalyLevel"] = PayloadReader.GetInt(context.Request.Payload, "anomalyLevel") ?? ReadInt(state, "AnomalyLevel", 0);
        state["ConfidenceMode"] = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "confidenceMode"), ReadString(state, "ConfidenceMode"), "enabled");
        state["UpdatedByUserId"] = actor.Id;
        state["UpdatedByDisplayName"] = actor.Login ?? actor.Id;
        state["UpdatedAtUtc"] = now;
        state["Revision"] = ReadInt(state, "Revision", 0) + 1;
        state["SchemaVersion"] = 1;
        FateStates().ReplaceOne(Builders<BsonDocument>.Filter.Eq("Id", FateDefaultStateId), state, new ReplaceOptions { IsUpsert = true });
        WriteAudit("fate", actor.Id, "fate.engine.state.updated", FateDefaultStateId);
        IngestFateJournalEvent("fate.engine.state.updated", actor, $"Fate Engine state updated: profile={profileId}, terrain={terrain}");
        TryPublishSyncEvent("fate.settings.updated", SyncScopes.Fate, "fateState", FateDefaultStateId, "updated", actor.Id, new Dictionary<string, object> { ["updatedUtc"] = now }, context.Request.RequestId ?? string.Empty);
        _logger.Admin($"fate.engine.state.updated actor={actor.Login} enabled={ReadBool(state, "IsEnabled")} profile={profileId} terrain={terrain} revision={ReadInt(state, "Revision", 0)}");
        return Ok("Fate state updated.", new Dictionary<string, object> { ["state"] = FateDocumentPayload(state, admin: true) });
    }

    public ResponseEnvelope FateAdminProfileList(CommandContext context)
    {
        RequireAdmin(context);
        if (!FateMvpCommandsEnabled()) return FateDisabled(context.Request.Command);
        var items = FateProfiles().Find(Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Ascending("DisplayName"))
            .Limit(100)
            .ToList()
            .Select(x => (object)FateDocumentPayload(x, admin: true))
            .ToArray();
        return Ok("Fate profiles loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope FateAdminProfileGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!FateMvpCommandsEnabled()) return FateDisabled(context.Request.Command);
        var profileId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "profileId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "profileId");
        var doc = FateProfiles().Find(Builders<BsonDocument>.Filter.Eq("ProfileId", profileId)).FirstOrDefault() ?? throw new KeyNotFoundException("Fate profile not found.");
        return Ok("Fate profile loaded.", new Dictionary<string, object> { ["profile"] = FateDocumentPayload(doc, admin: true) });
    }

    public ResponseEnvelope FateAdminProfileSetActive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!FateMvpCommandsEnabled()) return FateDisabled(context.Request.Command);
        var profileId = RequireLength(PayloadReader.GetString(context.Request.Payload, "profileId"), 1, 128, "profileId");
        if (!FateProfiles().Find(Builders<BsonDocument>.Filter.Eq("ProfileId", profileId)).Any())
            throw new KeyNotFoundException("Fate profile not found.");
        var payload = new Dictionary<string, object>(context.Request.Payload, StringComparer.OrdinalIgnoreCase)
        {
            ["activeProfileId"] = profileId,
            ["isEnabled"] = true
        };
        context.Request.Payload.Clear();
        foreach (var item in payload) context.Request.Payload[item.Key] = item.Value;
        var response = FateAdminStateUpdate(context);
        WriteAudit("fate", actor.Id, "fate.profile.changed", profileId);
        IngestFateJournalEvent("fate.profile.changed", actor, $"Fate profile changed: activeProfileId={profileId}");
        return response;
    }

    public ResponseEnvelope FateAdminLayerRulesList(CommandContext context)
    {
        RequireAdmin(context);
        if (!FateMvpCommandsEnabled()) return FateDisabled(context.Request.Command);
        var items = FateLayerRules().Find(FilterDefinition<BsonDocument>.Empty).ToList().Select(x => (object)FateDocumentPayload(x, admin: true)).ToArray();
        return Ok("Fate layer rules loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope FateAdminModifierRulesList(CommandContext context)
    {
        RequireAdmin(context);
        if (!FateMvpCommandsEnabled()) return FateDisabled(context.Request.Command);
        var items = FateModifierRules().Find(FilterDefinition<BsonDocument>.Empty).ToList().Select(x => (object)FateDocumentPayload(x, admin: true)).ToArray();
        return Ok("Fate modifier rules loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope FateAdminRollLogsList(CommandContext context)
    {
        RequireAdmin(context);
        if (!FateMvpCommandsEnabled() || !_featureFlags.IsEnabled(nameof(FateFeatureFlags.UseFateRollLogs))) return FateDisabled(context.Request.Command);
        var limit = ClampInt(PayloadReader.GetInt(context.Request.Payload, "limit") ?? 50, 1, 200);
        var items = FateRollLogs().Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Descending("CreatedAtUtc"))
            .Limit(limit)
            .ToList()
            .Select(x => (object)FateDocumentPayload(x, admin: true))
            .ToArray();
        return Ok("Fate roll logs loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope FateAdminRollLogsGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!FateMvpCommandsEnabled() || !_featureFlags.IsEnabled(nameof(FateFeatureFlags.UseFateRollLogs))) return FateDisabled(context.Request.Command);
        var rollId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "rollId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "rollId");
        var doc = FateRollLogs().Find(Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("RollId", rollId),
            Builders<BsonDocument>.Filter.Eq("Id", rollId))).FirstOrDefault() ?? throw new KeyNotFoundException("Fate roll log not found.");
        return Ok("Fate roll log loaded.", new Dictionary<string, object> { ["item"] = FateDocumentPayload(doc, admin: true) });
    }

    public ResponseEnvelope FateAdminSimulateRoll(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!FateMvpPipelineEnabled()) return FateDisabled(context.Request.Command);
        var baseRoll = PayloadReader.GetInt(context.Request.Payload, "baseRoll") ?? 10;
        var dieSides = PayloadReader.GetInt(context.Request.Payload, "dieSides") ?? 20;
        var skillId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "skillId"), "dev_acceptance_skill_01451");
        var subAttributeId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "subAttributeId"), "dev_acceptance_subattribute_01451");
        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId") ?? string.Empty;
        var seed = PayloadReader.GetInt(context.Request.Payload, "seed");

        var fakeResult = new DiceRollResult
        {
            NormalizedFormula = $"1d{dieSides}",
            Rolls = new List<int> { baseRoll },
            BaseRolls = new List<int> { baseRoll },
            Modifier = 0,
            Total = baseRoll,
            Visibility = RequestVisibility.AdminOnly,
            ApprovedByUserId = actor.Id
        };
        var request = new DiceRollRequest
        {
            Id = $"fate-sim-{Guid.NewGuid():N}",
            RequestType = "FateSimulation",
            CreatorUserId = actor.Id,
            RelatedUserId = actor.Id,
            CharacterId = characterId,
            RawFormula = $"1d{dieSides}",
            Formula = new DiceFormulaSpec { DiceCount = 1, DiceSides = dieSides, Modifier = 0, Normalized = $"1d{dieSides}" },
            Visibility = RequestVisibility.AdminOnly,
            Status = RequestStatus.Approved,
            Result = fakeResult
        };
        var result = ApplyFateMvpToDiceRequest(context, actor, request, FateRollTypes.SkillCheck, skillId, subAttributeId, new[] { "skill_check", "strength", "physical" }, seed, persistLog: true);
        IngestFateJournalEvent("fate.simulation.run", actor, $"Fate simulation run: base={baseRoll}, final={fakeResult.Total}");
        return Ok("Fate simulation completed.", new Dictionary<string, object>
        {
            ["roll"] = DiceRequestPayload(request, actor),
            ["fate"] = result.ToAdminPayload()
        });
    }

    public ResponseEnvelope FateAdminConfidenceGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!FateMvpCommandsEnabled()) return FateDisabled(context.Request.Command);
        var logs = FateRollLogs().Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Descending("CreatedAtUtc"))
            .Limit(20)
            .ToList();
        var avgDelta = logs.Count == 0 ? 0 : logs.Average(x => ReadInt(x, "FinalVisibleResult", 0) - ReadInt(x, "VisibleBaseTotal", 0));
        return Ok("Fate confidence loaded.", new Dictionary<string, object>
        {
            ["mode"] = ReadString(GetOrCreateFateState(), "ConfidenceMode", "enabled"),
            ["recentRollCount"] = logs.Count,
            ["averageDelta"] = avgDelta,
            ["summary"] = logs.Count == 0 ? "Истории бросков пока нет." : $"Средняя коррекция Fate: {avgDelta:0.##}"
        });
    }

    public ResponseEnvelope FateControlPanelsList(CommandContext context)
    {
        RequireAdmin(context);
        if (!FateLayoutEnabled()) return FateDisabled(context.Request.Command);
        return Ok("Fate control panels loaded.", new Dictionary<string, object> { ["items"] = DefaultFatePanelDescriptors().Select(x => (object)BsonToDictionary(x, admin: true)).ToArray() });
    }

    public ResponseEnvelope FateControlLayoutGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!FateLayoutEnabled()) return FateDisabled(context.Request.Command);
        var userId = PayloadReader.GetString(context.Request.Payload, "userId") ?? context.Session?.UserId ?? string.Empty;
        var client = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "client"), "AdminClient");
        var layoutId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "layoutId"), FateDefaultLayoutId);
        var doc = GetOrCreateFateLayout(userId, client, layoutId);
        return Ok("Fate control layout loaded.", new Dictionary<string, object> { ["layout"] = FateDocumentPayload(doc, admin: true), ["panels"] = DefaultFatePanelDescriptors().Select(x => (object)BsonToDictionary(x, admin: true)).ToArray() });
    }

    public ResponseEnvelope FateControlLayoutSave(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!FateLayoutEnabled()) return FateDisabled(context.Request.Command);
        var client = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "client"), "AdminClient");
        var layoutId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "layoutId"), FateDefaultLayoutId);
        var panelValues = PayloadReader.GetList(context.Request.Payload, "panels") ?? new List<object>();
        var panelsJson = PayloadReader.GetString(context.Request.Payload, "panelsJson");
        if (!string.IsNullOrWhiteSpace(panelsJson))
            panelValues = FatePanelsFromJson(panelsJson);
        var panels = ValidateAndNormalizePanels(panelValues);
        var now = DateTime.UtcNow;
        var filter = Builders<BsonDocument>.Filter.Eq("UserId", actor.Id)
                     & Builders<BsonDocument>.Filter.Eq("Client", client)
                     & Builders<BsonDocument>.Filter.Eq("LayoutId", layoutId);
        var existing = FateLayouts().Find(filter).FirstOrDefault();
        var doc = existing ?? CreateDefaultFateLayout(actor.Id, client, layoutId);
        doc["Panels"] = panels;
        doc["UpdatedAtUtc"] = now;
        doc["Revision"] = ReadInt(doc, "Revision", 0) + 1;
        FateLayouts().ReplaceOne(filter, doc, new ReplaceOptions { IsUpsert = true });
        WriteAudit("fate", actor.Id, "fate.control.layout.saved", layoutId);
        IngestFateJournalEvent("fate.control.layout.saved", actor, $"Fate Control layout saved: {layoutId}");
        return Ok("Fate control layout saved.", new Dictionary<string, object> { ["layout"] = FateDocumentPayload(doc, admin: true) });
    }

    public ResponseEnvelope FateControlLayoutReset(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!FateLayoutEnabled()) return FateDisabled(context.Request.Command);
        var client = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "client"), "AdminClient");
        var layoutId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "layoutId"), FateDefaultLayoutId);
        var doc = CreateDefaultFateLayout(actor.Id, client, layoutId);
        doc["Revision"] = 1;
        FateLayouts().ReplaceOne(
            Builders<BsonDocument>.Filter.Eq("UserId", actor.Id) & Builders<BsonDocument>.Filter.Eq("Client", client) & Builders<BsonDocument>.Filter.Eq("LayoutId", layoutId),
            doc,
            new ReplaceOptions { IsUpsert = true });
        return Ok("Fate control layout reset.", new Dictionary<string, object> { ["layout"] = FateDocumentPayload(doc, admin: true) });
    }

    private FateMvpProcessResult ApplyFateMvpToDiceRequest(CommandContext context, UserAccount actor, DiceRollRequest request, string rollType, string skillId, string subAttributeId, IReadOnlyCollection<string> rollTags, int? deterministicSeed = null, bool persistLog = true)
    {
        var result = request.Result;
        if (result == null || result.Rolls.Count == 0 || !FateMvpPipelineEnabled())
            return FateMvpProcessResult.NotApplied("fate_pipeline_disabled");

        var state = GetOrCreateFateState();
        if (!ReadBool(state, "IsEnabled"))
            return FateMvpProcessResult.NotApplied("fate_state_disabled");

        var baseRolls = result.BaseRolls != null && result.BaseRolls.Count == result.Rolls.Count
            ? new List<int>(result.BaseRolls)
            : new List<int>(result.Rolls);
        result.BaseRolls = baseRolls;
        var baseTotal = result.Total;
        var contextSummary = BuildFateRollContext(request, rollType, skillId, subAttributeId, rollTags);
        var modifiers = ResolveFateAutomatedModifiers(request.CharacterId ?? string.Empty, skillId, subAttributeId, rollTags, state);
        var layers = ResolveFateLayers(state, baseTotal, modifiers.Sum(x => x.Value), deterministicSeed);
        var fateDelta = modifiers.Sum(x => x.Value) + layers.Sum(x => x.Modifier);
        if (fateDelta == 0)
        {
            layers.Add(new FateLayerContribution { LayerId = "layer_0", LayerName = "Обычный randomizer", Modifier = 0, Reason = "base randomizer only", IsHiddenFromPlayer = false });
        }

        if (fateDelta != 0)
        {
            result.Rolls[0] += fateDelta;
            if (result.FateRolls == null || result.FateRolls.Count != result.Rolls.Count)
                result.FateRolls = result.Rolls.Select(_ => (int?)null).ToList();
            if (result.FateAppliedByDie == null || result.FateAppliedByDie.Count != result.Rolls.Count)
                result.FateAppliedByDie = result.Rolls.Select(_ => false).ToList();
            result.FateRolls[0] = result.Rolls[0];
            result.FateAppliedByDie[0] = true;
            result.Total += fateDelta;
        }

        var processResult = new FateMvpProcessResult
        {
            Applied = true,
            RollId = request.Id,
            RollType = rollType,
            BaseResult = baseTotal,
            FinalResult = result.Total,
            TotalModifier = fateDelta,
            ContextSummary = contextSummary,
            Modifiers = modifiers,
            Layers = layers,
            PlayerSafeSummary = "Fate Engine применил серверный контекст броска."
        };

        if (persistLog && _featureFlags.IsEnabled(nameof(FateFeatureFlags.UseFateRollLogs)))
        {
            SaveFateRollLog(actor, request, processResult, state);
            IngestFateJournalEvent("fate.roll.processed", actor, $"Fate roll processed: rollId={request.Id}, type={rollType}, delta={fateDelta}");
        }

        _logger.Admin($"fate.roll.processed actor={actor.Login} rollId={request.Id} type={rollType} base={baseTotal} final={result.Total} delta={fateDelta} modifiers={modifiers.Count} layers={layers.Count}");
        return processResult;
    }

    private FateMvpProcessResult ApplyFateMvpToDiceRequestIfEnabled(CommandContext context, UserAccount actor, DiceRollRequest request, string rollType, string skillId = "", string subAttributeId = "", IReadOnlyCollection<string>? rollTags = null)
    {
        if (!FateMvpPipelineEnabled())
            return FateMvpProcessResult.NotApplied("fate_pipeline_disabled");
        return ApplyFateMvpToDiceRequest(context, actor, request, rollType, skillId, subAttributeId, rollTags ?? Array.Empty<string>());
    }

    private string BuildFateRollContext(DiceRollRequest request, string rollType, string skillId, string subAttributeId, IReadOnlyCollection<string> rollTags)
    {
        return $"character={request.CharacterId ?? string.Empty}; skill={skillId}; subattribute={subAttributeId}; tags={string.Join(",", rollTags ?? Array.Empty<string>())}; type={rollType}";
    }

    private List<FateAppliedModifier> ResolveFateAutomatedModifiers(string characterId, string skillId, string subAttributeId, IReadOnlyCollection<string> rollTags, BsonDocument state)
    {
        var result = new List<FateAppliedModifier>();
        if (!_featureFlags.IsEnabled(nameof(FateFeatureFlags.UseFateAutomatedModifiers))) return result;

        var ruleTokens = new HashSet<string>(FateModifierRules()
            .Find(Builders<BsonDocument>.Filter.Eq("IsEnabled", true))
            .ToList()
            .Select(x => ReadString(x, "ReasonToken"))
            .Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);

        if (ruleTokens.Contains("FATE_SKILL_MOD_01457") && string.Equals(skillId, "dev_acceptance_skill_01451", StringComparison.OrdinalIgnoreCase))
        {
            var skillProfile = _mongo.CharacterSkillProfiles.Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
            var skill = (skillProfile?.Skills ?? new List<CharacterSkillProfileValue>()).FirstOrDefault(x => string.Equals(x.SkillId, skillId, StringComparison.OrdinalIgnoreCase));
            if (skill != null)
                result.Add(Modifier("skill", skill.SkillId, "Навык dev_acceptance_skill_01451", 2, "layer_2", true, "FATE_SKILL_MOD_01457"));
        }

        if (ruleTokens.Contains("FATE_SUBATTRIBUTE_MOD_01457"))
        {
            var values = string.IsNullOrWhiteSpace(characterId)
                ? new Dictionary<string, CharacterSubAttributeValue>(StringComparer.OrdinalIgnoreCase)
                : CharacterSubAttributeRuntime.BuildValueMap(_mongo, characterId, RuleSetIds.FantasyNriDefault);
            var selected = values.TryGetValue(FirstNonEmpty(subAttributeId, "dev_acceptance_subattribute_01451"), out var sub) ? sub : null;
            if (selected != null && selected.CurrentValue >= 9)
                result.Add(Modifier("subattribute", selected.SubAttributeId, "Подхарактеристика", 1, "layer_2", true, "FATE_SUBATTRIBUTE_MOD_01457"));
        }

        if (ruleTokens.Contains("FATE_RACE_MOD_01457"))
        {
            var race = _mongo.CharacterRaceOrSpeciesProfiles.Find(Builders<CharacterRaceOrSpeciesProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
            var tags = race?.Tags ?? new List<string>();
            if (string.Equals(race?.RaceId, "dev_fate_race_01457", StringComparison.OrdinalIgnoreCase)
                || string.Equals(race?.RaceCode, "dev_fate_race_01457", StringComparison.OrdinalIgnoreCase)
                || tags.Any(x => string.Equals(x, "dev_fate_race_01457", StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(Modifier("race_trait", FirstNonEmpty(race?.RaceId, race?.RaceCode, "dev_fate_race_01457"), "Раса / происхождение", 3, "layer_2", true, "FATE_RACE_MOD_01457"));
            }
        }

        if (ruleTokens.Contains("FATE_HEIGHT_MOD_01457"))
        {
            var body = _mongo.CharacterBodyProfiles.Find(Builders<CharacterBodyProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
            var tags = rollTags ?? Array.Empty<string>();
            if (body != null && body.HeightCm >= 190 && tags.Any(x => string.Equals(x, "strength", StringComparison.OrdinalIgnoreCase) || string.Equals(x, "physical", StringComparison.OrdinalIgnoreCase) || string.Equals(x, "lifting", StringComparison.OrdinalIgnoreCase)))
                result.Add(Modifier("height", characterId, $"Рост {body.HeightCm} см", 1, "layer_2", true, "FATE_HEIGHT_MOD_01457"));
        }

        if (ruleTokens.Contains("FATE_CONDITION_MOD_01457"))
        {
            var conditions = _mongo.CharacterConditionProfiles.Find(Builders<CharacterConditionProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile?.Conditions ?? new List<string>();
            if (conditions.Any(x => string.Equals(x, "focused_01457", StringComparison.OrdinalIgnoreCase)))
                result.Add(Modifier("condition", "focused_01457", "Состояние: сосредоточенность", 1, "layer_4", true, "FATE_CONDITION_MOD_01457"));
        }

        return result;
    }

    private List<FateLayerContribution> ResolveFateLayers(BsonDocument state, int baseTotal, int modifierTotal, int? seed)
    {
        var terrain = ReadString(state, "TerrainProfile", FateTerrainProfiles.Calm);
        var rnd = seed.HasValue ? new Random(seed.Value) : new Random(unchecked(Environment.TickCount * 31 + baseTotal));
        var result = new List<FateLayerContribution>();
        var terrainDelta = terrain switch
        {
            FateTerrainProfiles.BlessedLand => 2,
            FateTerrainProfiles.CursedLand => -2,
            FateTerrainProfiles.Battle => 1,
            FateTerrainProfiles.Hell => -3,
            FateTerrainProfiles.Chaos => rnd.Next(-2, 3),
            FateTerrainProfiles.Drama => 1,
            FateTerrainProfiles.KeyMoment => 2,
            FateTerrainProfiles.AnomalousSpace => rnd.Next(-4, 7),
            _ => 0
        };
        result.Add(new FateLayerContribution { LayerId = "layer_1", LayerName = "Местность", Modifier = terrainDelta, Reason = $"terrain={terrain}; {FateHiddenGmToken}", IsHiddenFromPlayer = true });
        result.Add(new FateLayerContribution { LayerId = "layer_2", LayerName = "Эффекты персонажа", Modifier = modifierTotal, Reason = "automated profile modifiers", IsHiddenFromPlayer = true });
        result.Add(new FateLayerContribution { LayerId = "layer_3", LayerName = "Предметы", Modifier = 0, Reason = "no item modifiers resolved", IsHiddenFromPlayer = true });
        result.Add(new FateLayerContribution { LayerId = "layer_4", LayerName = "Психология", Modifier = 0, Reason = "condition modifiers are logged as applied modifiers", IsHiddenFromPlayer = true });

        var confidenceMode = ReadString(state, "ConfidenceMode", "enabled");
        var confidenceDelta = 0;
        if (string.Equals(confidenceMode, "enabled", StringComparison.OrdinalIgnoreCase))
        {
            var recent = FateRollLogs().Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Descending("CreatedAtUtc")).Limit(5).ToList();
            if (recent.Count >= 3)
            {
                var avg = recent.Average(x => ReadInt(x, "FinalVisibleResult", 0) - ReadInt(x, "VisibleBaseTotal", 0));
                if (avg > 3) confidenceDelta = -1;
                else if (avg < -3) confidenceDelta = 1;
            }
        }
        result.Add(new FateLayerContribution { LayerId = "layer_5", LayerName = "Шкала уверенности / история бросков", Modifier = confidenceDelta, Reason = "confidence_history", IsHiddenFromPlayer = true });
        return result;
    }

    private void SaveFateRollLog(UserAccount actor, DiceRollRequest request, FateMvpProcessResult result, BsonDocument state)
    {
        var now = DateTime.UtcNow;
        var doc = new BsonDocument
        {
            ["Id"] = $"fate_roll_{Guid.NewGuid():N}",
            ["RollId"] = request.Id,
            ["SessionId"] = string.Empty,
            ["CampaignId"] = string.Empty,
            ["CharacterId"] = request.CharacterId ?? string.Empty,
            ["UserId"] = actor.Id,
            ["ActorDisplayName"] = actor.Login ?? actor.Id,
            ["RollType"] = result.RollType,
            ["DiceFormula"] = request.Formula?.Normalized ?? request.RawFormula,
            ["BaseRandomResult"] = result.BaseResult,
            ["VisibleBaseTotal"] = result.BaseResult,
            ["FinalVisibleResult"] = result.FinalResult,
            ["IsFateApplied"] = result.Applied,
            ["ActiveProfileId"] = ReadString(state, "ActiveProfileId", FateAcceptanceProfileId),
            ["TerrainProfile"] = ReadString(state, "TerrainProfile", FateTerrainProfiles.Calm),
            ["ContextSummary"] = result.ContextSummary,
            ["AppliedLayers"] = new BsonArray(result.Layers.Select(x => x.ToBsonDocument())),
            ["AppliedModifiers"] = new BsonArray(result.Modifiers.Select(x => x.ToBsonDocument())),
            ["PlayerSafeSummary"] = result.PlayerSafeSummary,
            ["AdminOnlyDetails"] = new BsonDocument
            {
                ["HiddenReasonTokens"] = new BsonArray(result.Modifiers.Select(x => x.Reason).Where(x => !string.IsNullOrWhiteSpace(x))),
                ["LayerToken"] = FateHiddenGmToken
            },
            ["ServerOnlyData"] = new BsonDocument
            {
                ["RawContext"] = result.ContextSummary,
                ["CreatedByServer"] = true
            },
            ["CreatedAtUtc"] = now,
            ["SchemaVersion"] = 1
        };
        FateRollLogs().InsertOne(doc);
    }

    private void SeedFateAcceptanceData(UserAccount actor, string characterId, int? heightCm = null, bool? focusedCondition = null)
    {
        var now = DateTime.UtcNow;
        var profile = new BsonDocument
        {
            ["Id"] = FateAcceptanceProfileId,
            ["ProfileId"] = FateAcceptanceProfileId,
            ["DisplayName"] = "Fate acceptance profile 0.14.57",
            ["Description"] = "Acceptance profile for server-embedded Fate MVP.",
            ["IsEnabled"] = true,
            ["AppliesToSessionIds"] = new BsonArray(),
            ["DefaultTerrainProfile"] = FateTerrainProfiles.BlessedLand,
            ["LayerSettings"] = new BsonArray(DefaultFateLayerSettings()),
            ["Visibility"] = "admin_only",
            ["CreatedByUserId"] = actor.Id,
            ["CreatedByDisplayName"] = actor.Login ?? actor.Id,
            ["UpdatedByUserId"] = actor.Id,
            ["UpdatedByDisplayName"] = actor.Login ?? actor.Id,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["Revision"] = 1,
            ["IsArchived"] = false,
            ["SchemaVersion"] = 1
        };
        FateProfiles().ReplaceOne(Builders<BsonDocument>.Filter.Eq("ProfileId", FateAcceptanceProfileId), profile, new ReplaceOptions { IsUpsert = true });

        foreach (var layer in DefaultFateLayerSettings())
        {
            var doc = new BsonDocument(layer)
            {
                ["Id"] = $"fate_layer_{ReadString(layer, "LayerId")}",
                ["ProfileId"] = FateAcceptanceProfileId,
                ["UpdatedAtUtc"] = now
            };
            FateLayerRules().ReplaceOne(Builders<BsonDocument>.Filter.Eq("Id", ReadString(doc, "Id")), doc, new ReplaceOptions { IsUpsert = true });
        }

        foreach (var rule in DefaultFateModifierRules())
        {
            FateModifierRules().ReplaceOne(Builders<BsonDocument>.Filter.Eq("Id", ReadString(rule, "Id")), rule, new ReplaceOptions { IsUpsert = true });
        }

        var state = GetOrCreateFateState();
        state["ActiveProfileId"] = FateAcceptanceProfileId;
        state["IsEnabled"] = true;
        state["TerrainProfile"] = FateTerrainProfiles.BlessedLand;
        state["ConfidenceMode"] = "enabled";
        state["UpdatedByUserId"] = actor.Id;
        state["UpdatedByDisplayName"] = actor.Login ?? actor.Id;
        state["UpdatedAtUtc"] = now;
        state["Revision"] = ReadInt(state, "Revision", 0) + 1;
        FateStates().ReplaceOne(Builders<BsonDocument>.Filter.Eq("Id", FateDefaultStateId), state, new ReplaceOptions { IsUpsert = true });

        if (!string.IsNullOrWhiteSpace(characterId))
            SeedCharacterFateAcceptanceProfiles(characterId, heightCm, focusedCondition);

        GetOrCreateFateLayout(actor.Id, "AdminClient", FateDefaultLayoutId);
        WriteAudit("fate", actor.Id, "fate.acceptance.seeded", FateAcceptanceProfileId);
    }

    private void SeedCharacterFateAcceptanceProfiles(string characterId, int? heightCm = null, bool? focusedCondition = null)
    {
        var skillDoc = _mongo.CharacterSkillProfiles.Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()
            ?? new CharacterSkillProfileDocument { CharacterId = characterId, Profile = new SkillProfile { CharacterId = characterId, RuleSetId = RuleSetIds.FantasyNriDefault } };
        skillDoc.Profile ??= new SkillProfile { CharacterId = characterId, RuleSetId = RuleSetIds.FantasyNriDefault };
        skillDoc.Profile.Skills ??= new List<CharacterSkillProfileValue>();
        var skill = skillDoc.Profile.Skills.FirstOrDefault(x => string.Equals(x.SkillId, "dev_acceptance_skill_01451", StringComparison.OrdinalIgnoreCase));
        if (skill == null)
        {
            skill = new CharacterSkillProfileValue { SkillId = "dev_acceptance_skill_01451", Rank = 1, IsLearned = true, IsUnlocked = true, IsPlayerVisible = true, Source = "fate_acceptance_01457" };
            skillDoc.Profile.Skills.Add(skill);
        }
        skill.Rank = Math.Max(1, skill.Rank);
        _mongo.CharacterSkillProfiles.ReplaceOne(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, characterId), skillDoc, new ReplaceOptions { IsUpsert = true });

        var subDoc = CharacterSubAttributeRuntime.EnsureProfile(_mongo, characterId, RuleSetIds.FantasyNriDefault);
        var sub = subDoc.Profile.SubAttributes.FirstOrDefault(x => string.Equals(x.SubAttributeId, "dev_acceptance_subattribute_01451", StringComparison.OrdinalIgnoreCase));
        if (sub == null)
        {
            sub = new CharacterSubAttributeValue { SubAttributeId = "dev_acceptance_subattribute_01451", ParentAttributeId = "strength", BaseValue = 10, CurrentValue = 10, Source = "fate_acceptance_01457", IsVisibleToPlayer = true };
            subDoc.Profile.SubAttributes.Add(sub);
        }
        sub.ParentAttributeId = "strength";
        sub.CurrentValue = Math.Max(10, sub.CurrentValue);
        subDoc.Profile.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.CharacterSubAttributeProfiles.ReplaceOne(Builders<CharacterSubAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId), subDoc, new ReplaceOptions { IsUpsert = true });

        var raceDoc = _mongo.CharacterRaceOrSpeciesProfiles.Find(Builders<CharacterRaceOrSpeciesProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()
            ?? new CharacterRaceOrSpeciesProfileDocument { CharacterId = characterId };
        raceDoc.Profile = new RaceOrSpeciesProfile
        {
            CharacterId = characterId,
            RuleSetId = RuleSetIds.FantasyNriDefault,
            RaceId = "dev_fate_race_01457",
            RaceCode = "dev_fate_race_01457",
            RaceName = "Fate acceptance race",
            DisplayName = "Fate acceptance race",
            Source = "fate_acceptance_01457",
            Tags = new List<string> { "dev_fate_race_01457" }
        };
        _mongo.CharacterRaceOrSpeciesProfiles.ReplaceOne(
            Builders<CharacterRaceOrSpeciesProfileDocument>.Filter.Eq(x => x.CharacterId, characterId),
            raceDoc,
            new ReplaceOptions { IsUpsert = true });

        var bodyDoc = _mongo.CharacterBodyProfiles.Find(Builders<CharacterBodyProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()
            ?? new CharacterBodyProfileDocument { CharacterId = characterId };
        bodyDoc.Profile = new BodyProfile
        {
            CharacterId = characterId,
            RuleSetId = RuleSetIds.FantasyNriDefault,
            HeightCm = heightCm ?? 195,
            HeightText = $"{heightCm ?? 195} см",
            SizeCategory = "tall",
            Source = "fate_acceptance_01457",
            BodyTags = new List<string> { "physical", "strength" }
        };
        _mongo.CharacterBodyProfiles.ReplaceOne(
            Builders<CharacterBodyProfileDocument>.Filter.Eq(x => x.CharacterId, characterId),
            bodyDoc,
            new ReplaceOptions { IsUpsert = true });

        var conditionDoc = _mongo.CharacterConditionProfiles.Find(Builders<CharacterConditionProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()
            ?? new CharacterConditionProfileDocument { CharacterId = characterId };
        conditionDoc.Profile = new ConditionProfile
        {
            Conditions = (focusedCondition ?? true) ? new List<string> { "focused_01457" } : new List<string>(),
            UpdatedUtc = DateTime.UtcNow
        };
        _mongo.CharacterConditionProfiles.ReplaceOne(
            Builders<CharacterConditionProfileDocument>.Filter.Eq(x => x.CharacterId, characterId),
            conditionDoc,
            new ReplaceOptions { IsUpsert = true });
    }

    private BsonDocument GetOrCreateFateState()
    {
        var doc = FateStates().Find(Builders<BsonDocument>.Filter.Eq("Id", FateDefaultStateId)).FirstOrDefault();
        if (doc != null) return doc;
        doc = new BsonDocument
        {
            ["Id"] = FateDefaultStateId,
            ["SessionId"] = "default",
            ["CampaignId"] = "dev-campaign",
            ["ActiveProfileId"] = FateAcceptanceProfileId,
            ["IsEnabled"] = false,
            ["TerrainProfile"] = FateTerrainProfiles.Calm,
            ["DramaLevel"] = 0,
            ["ChaosLevel"] = 0,
            ["AnomalyLevel"] = 0,
            ["ConfidenceMode"] = "enabled",
            ["LastRevision"] = 0,
            ["UpdatedByUserId"] = string.Empty,
            ["UpdatedByDisplayName"] = string.Empty,
            ["UpdatedAtUtc"] = DateTime.UtcNow,
            ["Revision"] = 0,
            ["SchemaVersion"] = 1
        };
        FateStates().InsertOne(doc);
        return doc;
    }

    private BsonDocument GetOrCreateFateLayout(string userId, string client, string layoutId)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("UserId", userId)
                     & Builders<BsonDocument>.Filter.Eq("Client", client)
                     & Builders<BsonDocument>.Filter.Eq("LayoutId", layoutId);
        var doc = FateLayouts().Find(filter).FirstOrDefault();
        if (doc != null) return doc;
        doc = CreateDefaultFateLayout(userId, client, layoutId);
        FateLayouts().InsertOne(doc);
        return doc;
    }

    private BsonDocument CreateDefaultFateLayout(string userId, string client, string layoutId)
    {
        var now = DateTime.UtcNow;
        return new BsonDocument
        {
            ["Id"] = $"fate_layout_{client}_{layoutId}_{userId}",
            ["UserId"] = userId ?? string.Empty,
            ["Client"] = string.IsNullOrWhiteSpace(client) ? "AdminClient" : client,
            ["LayoutId"] = string.IsNullOrWhiteSpace(layoutId) ? FateDefaultLayoutId : layoutId,
            ["DisplayName"] = "Default Fate Control layout",
            ["Panels"] = new BsonArray(DefaultFatePanelDescriptors().Select((x, i) => new BsonDocument
            {
                ["PanelId"] = ReadString(x, "PanelId"),
                ["DisplayName"] = ReadString(x, "DisplayName"),
                ["IsVisible"] = ReadBool(x, "IsVisibleByDefault", true),
                ["DockArea"] = ReadString(x, "DefaultDockArea", "center"),
                ["Order"] = i + 1,
                ["Width"] = 320,
                ["Height"] = 220,
                ["IsCollapsed"] = false,
                ["Column"] = 0,
                ["Row"] = i,
                ["TabGroup"] = string.Empty
            })),
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["Revision"] = 0,
            ["SchemaVersion"] = 1
        };
    }

    private BsonArray ValidateAndNormalizePanels(IList<object> rawPanels)
    {
        var known = new HashSet<string>(DefaultFatePanelDescriptors().Select(x => ReadString(x, "PanelId")), StringComparer.OrdinalIgnoreCase);
        var result = new BsonArray();
        var order = 1;
        foreach (var raw in rawPanels)
        {
            var map = FateToObjectDictionary(raw);
            var panelId = FirstNonEmpty(ReadMapString(map, "panelId"), ReadMapString(map, "PanelId"));
            if (!known.Contains(panelId))
                throw new ArgumentException($"Invalid Fate panel id: {panelId}; type={raw?.GetType().FullName ?? "null"} raw={RequireLength(Convert.ToString(raw) ?? string.Empty, 0, 180, "rawPanel")}");
            var dock = FirstNonEmpty(ReadMapString(map, "dockArea"), ReadMapString(map, "DockArea"), "center").ToLowerInvariant();
            if (!new[] { "left", "right", "center", "bottom", "floating" }.Contains(dock))
                throw new ArgumentException($"Invalid Fate dock area: {dock}");
            result.Add(new BsonDocument
            {
                ["PanelId"] = panelId,
                ["DisplayName"] = FirstNonEmpty(ReadMapString(map, "displayName"), ReadMapString(map, "DisplayName"), panelId),
                ["IsVisible"] = ReadMapBool(map, "isVisible", ReadMapBool(map, "IsVisible", true)),
                ["DockArea"] = dock,
                ["Order"] = ReadMapInt(map, "order", ReadMapInt(map, "Order", order++)),
                ["Width"] = ClampInt(ReadMapInt(map, "width", ReadMapInt(map, "Width", 320)), 160, 1200),
                ["Height"] = ClampInt(ReadMapInt(map, "height", ReadMapInt(map, "Height", 220)), 120, 900),
                ["IsCollapsed"] = ReadMapBool(map, "isCollapsed", ReadMapBool(map, "IsCollapsed", false)),
                ["Column"] = ClampInt(ReadMapInt(map, "column", ReadMapInt(map, "Column", 0)), 0, 8),
                ["Row"] = ClampInt(ReadMapInt(map, "row", ReadMapInt(map, "Row", 0)), 0, 50),
                ["TabGroup"] = FirstNonEmpty(ReadMapString(map, "tabGroup"), ReadMapString(map, "TabGroup"))
            });
        }

        if (result.Count == 0) throw new ArgumentException("Layout must contain at least one panel.");
        return result;
    }

    private static List<object> FatePanelsFromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Fate layout panelsJson must be an array.");
        return document.RootElement.EnumerateArray()
            .Select(x => FateConvertJsonValue(x))
            .ToList();
    }

    private List<BsonDocument> DefaultFatePanelDescriptors() => new List<BsonDocument>
    {
        Panel("engine_state", "Engine State", "left", 10, "FateControl_Panel_EngineState"),
        Panel("active_profile", "Active Profile", "left", 20, "FateControl_Panel_ActiveProfile"),
        Panel("layer_settings", "Layer Settings", "center", 30, "FateControl_Panel_LayerSettings"),
        Panel("automated_modifiers", "Automated Modifiers", "center", 40, "FateControl_Panel_AutomatedModifiers"),
        Panel("roll_simulator", "Roll Simulator", "right", 50, "FateControl_Panel_RollSimulator"),
        Panel("recent_roll_logs", "Recent Roll Logs", "right", 60, "FateControl_Panel_RecentRolls"),
        Panel("roll_detail", "Roll Detail", "right", 70, "FateControl_Panel_RollDetail"),
        Panel("confidence_state", "Confidence State", "bottom", 80, "FateControl_Panel_ConfidenceState"),
        Panel("layout_editor", "Layout Editor", "bottom", 90, "FateControl_LayoutEditor_Tab"),
        Panel("diagnostics", "Diagnostics", "bottom", 100, "FateControl_Panel_Diagnostics")
    };

    private static BsonDocument Panel(string id, string displayName, string dock, int order, string automationId) => new BsonDocument
    {
        ["PanelId"] = id,
        ["DisplayName"] = displayName,
        ["DefaultDockArea"] = dock,
        ["DefaultOrder"] = order,
        ["RequiredRole"] = "Admin",
        ["FeatureFlagId"] = string.Empty,
        ["ViewModelFactoryKey"] = id,
        ["IsLazyLoaded"] = true,
        ["IsVisibleByDefault"] = true,
        ["AutomationId"] = automationId
    };

    private List<BsonDocument> DefaultFateLayerSettings() => new List<BsonDocument>
    {
        Layer("layer_0", "Обычный randomizer", 0, 0, "none", false, true, 0),
        Layer("layer_1", "Местность", -4, 6, "additive", true, true, 1),
        Layer("layer_2", "Эффекты персонажа", -10, 10, "additive", true, true, 2),
        Layer("layer_3", "Предметы", -5, 5, "additive", true, true, 3),
        Layer("layer_4", "Психология", -5, 5, "additive", true, true, 4),
        Layer("layer_5", "Шкала уверенности / история бросков", -2, 2, "confidence_correction", true, true, 5)
    };

    private static BsonDocument Layer(string id, string name, int min, int max, string mode, bool hidden, bool adminVisible, int order) => new BsonDocument
    {
        ["LayerId"] = id,
        ["LayerName"] = name,
        ["IsEnabled"] = true,
        ["Strength"] = 1.0,
        ["MinDelta"] = min,
        ["MaxDelta"] = max,
        ["DistributionMode"] = mode,
        ["IsHiddenFromPlayer"] = hidden,
        ["IsVisibleToAdmin"] = adminVisible,
        ["SortOrder"] = order
    };

    private List<BsonDocument> DefaultFateModifierRules() => new List<BsonDocument>
    {
        Rule("fate_skill_modifier_01457", "skill", "dev_acceptance_skill_01451", 2, "FATE_SKILL_MOD_01457"),
        Rule("fate_subattribute_modifier_01457", "subattribute", "dev_acceptance_subattribute_01451", 1, "FATE_SUBATTRIBUTE_MOD_01457"),
        Rule("fate_race_modifier_01457", "race_trait", "dev_fate_race_01457", 3, "FATE_RACE_MOD_01457"),
        Rule("fate_height_modifier_01457", "height", "height_cm>=190", 1, "FATE_HEIGHT_MOD_01457"),
        Rule("fate_condition_modifier_01457", "condition", "focused_01457", 1, "FATE_CONDITION_MOD_01457")
    };

    private static BsonDocument Rule(string id, string sourceType, string appliesTo, int value, string reasonToken) => new BsonDocument
    {
        ["Id"] = id,
        ["RuleId"] = id,
        ["SourceType"] = sourceType,
        ["AppliesTo"] = appliesTo,
        ["Value"] = value,
        ["LayerId"] = sourceType == "condition" ? "layer_4" : "layer_2",
        ["IsHiddenFromPlayer"] = true,
        ["IsEnabled"] = true,
        ["ReasonToken"] = reasonToken,
        ["Reason"] = reasonToken,
        ["UpdatedAtUtc"] = DateTime.UtcNow,
        ["SchemaVersion"] = 1
    };

    private static FateAppliedModifier Modifier(string sourceType, string sourceId, string displayName, int value, string layerId, bool hidden, string reason)
        => new FateAppliedModifier { SourceType = sourceType, SourceId = sourceId, DisplayName = displayName, Value = value, LayerId = layerId, IsHiddenFromPlayer = hidden, Reason = reason };

    private IEnumerable<string> FateAcceptanceRuleTokens() => new[]
    {
        "FATE_SKILL_MOD_01457",
        "FATE_SUBATTRIBUTE_MOD_01457",
        "FATE_RACE_MOD_01457",
        "FATE_HEIGHT_MOD_01457",
        "FATE_CONDITION_MOD_01457"
    };

    private void ValidateTerrainProfile(string terrain)
    {
        var allowed = new[]
        {
            FateTerrainProfiles.Calm, FateTerrainProfiles.Battle, FateTerrainProfiles.CursedLand, FateTerrainProfiles.BlessedLand,
            FateTerrainProfiles.Hell, FateTerrainProfiles.Chaos, FateTerrainProfiles.Drama, FateTerrainProfiles.KeyMoment, FateTerrainProfiles.AnomalousSpace
        };
        if (!allowed.Contains(terrain, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid Fate terrain profile.");
    }

    private bool FateMvpCommandsEnabled()
        => _featureFlags.IsEnabled(nameof(FateFeatureFlags.UseFateEngineMvp));

    private bool FateMvpPipelineEnabled()
        => FateMvpCommandsEnabled()
           && _featureFlags.IsEnabled(nameof(FateFeatureFlags.UseFateServerPipeline))
           && _featureFlags.IsEnabled(nameof(FateFeatureFlags.UseFatePlayerSafeFiltering));

    private bool FateLayoutEnabled()
        => FateMvpCommandsEnabled()
           && _featureFlags.IsEnabled(nameof(FateFeatureFlags.UseFateControlLayout));

    private void EnsureFateMvpEnabledOrThrow()
    {
        if (!FateMvpCommandsEnabled()) throw new InvalidOperationException("Fate Engine MVP feature flags are disabled.");
    }

    private ResponseEnvelope FateDisabled(string command)
    {
        _logger.Admin($"fate.command.disabled command={command}");
        return Error("Fate Engine выключен feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private IMongoCollection<BsonDocument> FateProfiles() => _mongo.Database.GetCollection<BsonDocument>("fate_engine_profiles");
    private IMongoCollection<BsonDocument> FateLayerRules() => _mongo.Database.GetCollection<BsonDocument>("fate_layer_rules");
    private IMongoCollection<BsonDocument> FateStates() => _mongo.Database.GetCollection<BsonDocument>("fate_engine_states");
    private IMongoCollection<BsonDocument> FateRollLogs() => _mongo.Database.GetCollection<BsonDocument>("fate_roll_logs");
    private IMongoCollection<BsonDocument> FateModifierRules() => _mongo.Database.GetCollection<BsonDocument>("fate_modifier_rules");
    private IMongoCollection<BsonDocument> FateLayouts() => _mongo.Database.GetCollection<BsonDocument>("fate_control_layouts");

    private Dictionary<string, object> FateDocumentPayload(BsonDocument doc, bool admin) => BsonToDictionary(doc, admin);

    private Dictionary<string, object> BsonToDictionary(BsonDocument doc, bool admin)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in doc)
        {
            if (!admin && (string.Equals(item.Name, "ServerOnlyData", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Name, "AdminOnlyDetails", StringComparison.OrdinalIgnoreCase)))
                continue;
            result[item.Name] = BsonValueToObject(item.Value, admin);
        }
        return result;
    }

    private object BsonValueToObject(BsonValue value, bool admin)
    {
        if (value == null || value == BsonNull.Value) return string.Empty;
        if (value.IsString) return value.AsString;
        if (value.IsBoolean) return value.AsBoolean;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsDouble) return value.AsDouble;
        if (value.IsValidDateTime) return value.ToUniversalTime();
        if (value.IsBsonArray) return value.AsBsonArray.Select(x => BsonValueToObject(x, admin)).ToArray();
        if (value.IsBsonDocument) return BsonToDictionary(value.AsBsonDocument, admin);
        return value.ToString();
    }

    private static string ReadString(BsonDocument doc, string key, string fallback = "")
    {
        if (doc == null || !doc.TryGetValue(key, out var value) || value == BsonNull.Value) return fallback;
        return value.IsString ? value.AsString : value.ToString();
    }

    private static bool ReadBool(BsonDocument doc, string key, bool fallback = false)
    {
        if (doc == null || !doc.TryGetValue(key, out var value) || value == BsonNull.Value) return fallback;
        if (value.IsBoolean) return value.AsBoolean;
        if (value.IsString && bool.TryParse(value.AsString, out var parsed)) return parsed;
        return fallback;
    }

    private static int ReadInt(BsonDocument doc, string key, int fallback)
    {
        if (doc == null || !doc.TryGetValue(key, out var value) || value == BsonNull.Value) return fallback;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return (int)value.AsInt64;
        if (value.IsDouble) return (int)value.AsDouble;
        if (value.IsString && int.TryParse(value.AsString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return fallback;
    }

    private static int ClampInt(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static Dictionary<string, object> FateToObjectDictionary(object raw)
    {
        raw = FateConvertJsonValue(raw);
        if (raw is Dictionary<string, object> converted) return new Dictionary<string, object>(converted, StringComparer.OrdinalIgnoreCase);
        if (raw is Dictionary<string, object> d) return d;
        if (raw is IDictionary<string, object> id) return id.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        if (raw is IDictionary generic)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry item in generic) result[Convert.ToString(item.Key) ?? string.Empty] = item.Value ?? string.Empty;
            return result;
        }
        if (raw is IEnumerable enumerable && raw is not string)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in enumerable)
            {
                if (item == null) continue;
                if (item is DictionaryEntry entry)
                {
                    var key = Convert.ToString(entry.Key);
                    if (!string.IsNullOrWhiteSpace(key)) result[key] = FateConvertJsonValue(entry.Value);
                    continue;
                }
                if (item is object[] arrayPair && arrayPair.Length == 2)
                {
                    var key = Convert.ToString(arrayPair[0]);
                    if (!string.IsNullOrWhiteSpace(key)) result[key] = FateConvertJsonValue(arrayPair[1]);
                    continue;
                }
                if (item is IList listPair && listPair.Count == 2)
                {
                    var key = Convert.ToString(listPair[0]);
                    if (!string.IsNullOrWhiteSpace(key)) result[key] = FateConvertJsonValue(listPair[1]);
                    continue;
                }
                var itemType = item.GetType();
                var keyProperty = itemType.GetProperty("Key") ?? itemType.GetProperty("Name");
                var valueProperty = itemType.GetProperty("Value");
                if (keyProperty == null || valueProperty == null) continue;
                var reflectedKey = Convert.ToString(keyProperty.GetValue(item));
                if (!string.IsNullOrWhiteSpace(reflectedKey)) result[reflectedKey] = FateConvertJsonValue(valueProperty.GetValue(item));
            }
            return result;
        }
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static object FateConvertJsonValue(object? value)
    {
        if (value is JsonElement json)
        {
            return json.ValueKind switch
            {
                JsonValueKind.String => json.GetString() ?? string.Empty,
                JsonValueKind.Number => json.TryGetInt32(out var i) ? i : json.TryGetInt64(out var l) ? l : json.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => json.EnumerateArray().Select(x => FateConvertJsonValue(x)).ToArray(),
                JsonValueKind.Object => json.EnumerateObject().ToDictionary(p => p.Name, p => FateConvertJsonValue(p.Value), StringComparer.OrdinalIgnoreCase),
                _ => string.Empty
            };
        }
        return value ?? string.Empty;
    }

    private static string ReadMapString(Dictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return string.Empty;
        return Convert.ToString(value) ?? string.Empty;
    }

    private static bool ReadMapBool(Dictionary<string, object> map, string key, bool fallback)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        if (value is bool b) return b;
        return bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    }

    private static int ReadMapInt(Dictionary<string, object> map, string key, int fallback)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        return int.TryParse(Convert.ToString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private void IngestFateJournalEvent(string eventType, UserAccount actor, string title)
    {
        try
        {
            if (!_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp))
                || !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion)))
                return;
            var entry = new EventJournalEntryState
            {
                CampaignId = "dev-campaign",
                SourceModule = "fate_engine",
                SourceEventType = eventType,
                SourceEventId = eventType,
                EntryType = EventJournalEntryTypeIds.System,
                Category = EventJournalCategoryIds.System,
                Title = title,
                Summary = title,
                GMDetails = title,
                VisibilityMode = EventJournalVisibilityModeIds.GMOnly,
                IsPlayerVisible = false,
                CreatedByUserId = actor.Id,
                ActorUserId = actor.Id,
                ActorDisplayName = actor.Login ?? actor.Id,
                OccurredAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                IsAutomatic = true,
                SubjectEntityType = "fate",
                SubjectEntityId = eventType
            };
            entry.SequenceNumber = _repositories.EventJournalEntries.Find(Builders<EventJournalEntryState>.Filter.Eq(x => x.CampaignId, entry.CampaignId)).OrderByDescending(x => x.SequenceNumber).FirstOrDefault()?.SequenceNumber + 1 ?? 1;
            _repositories.EventJournalEntries.Insert(entry);
        }
        catch (Exception ex)
        {
            _logger.Debug($"fate.journal.ingest.skipped reason={ex.GetType().Name}:{ex.Message}");
        }
    }

    private sealed class FateAppliedModifier
    {
        public string SourceType { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Value { get; set; }
        public string LayerId { get; set; } = string.Empty;
        public bool IsHiddenFromPlayer { get; set; } = true;
        public string Reason { get; set; } = string.Empty;

        public BsonDocument ToBsonDocument() => new BsonDocument
        {
            ["SourceType"] = SourceType,
            ["SourceId"] = SourceId,
            ["DisplayName"] = DisplayName,
            ["Value"] = Value,
            ["LayerId"] = LayerId,
            ["IsHiddenFromPlayer"] = IsHiddenFromPlayer,
            ["Reason"] = Reason
        };
    }

    private sealed class FateLayerContribution
    {
        public string LayerId { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public int Modifier { get; set; }
        public bool IsHiddenFromPlayer { get; set; } = true;
        public string Reason { get; set; } = string.Empty;

        public BsonDocument ToBsonDocument() => new BsonDocument
        {
            ["LayerId"] = LayerId,
            ["LayerName"] = LayerName,
            ["Modifier"] = Modifier,
            ["IsHiddenFromPlayer"] = IsHiddenFromPlayer,
            ["Reason"] = Reason
        };
    }

    private sealed class FateMvpProcessResult
    {
        public bool Applied { get; set; }
        public string RollId { get; set; } = string.Empty;
        public string RollType { get; set; } = string.Empty;
        public int BaseResult { get; set; }
        public int FinalResult { get; set; }
        public int TotalModifier { get; set; }
        public string ContextSummary { get; set; } = string.Empty;
        public string PlayerSafeSummary { get; set; } = string.Empty;
        public string SkippedReason { get; set; } = string.Empty;
        public List<FateAppliedModifier> Modifiers { get; set; } = new List<FateAppliedModifier>();
        public List<FateLayerContribution> Layers { get; set; } = new List<FateLayerContribution>();

        public static FateMvpProcessResult NotApplied(string reason) => new FateMvpProcessResult { Applied = false, SkippedReason = reason };

        public Dictionary<string, object> ToAdminPayload() => new Dictionary<string, object>
        {
            ["applied"] = Applied,
            ["rollId"] = RollId,
            ["rollType"] = RollType,
            ["baseResult"] = BaseResult,
            ["finalResult"] = FinalResult,
            ["totalModifier"] = TotalModifier,
            ["contextSummary"] = ContextSummary,
            ["skippedReason"] = SkippedReason,
            ["playerSafeSummary"] = PlayerSafeSummary,
            ["modifiers"] = Modifiers.Select(x => new Dictionary<string, object> { ["sourceType"] = x.SourceType, ["sourceId"] = x.SourceId, ["displayName"] = x.DisplayName, ["value"] = x.Value, ["layerId"] = x.LayerId, ["isHiddenFromPlayer"] = x.IsHiddenFromPlayer, ["reason"] = x.Reason }).Cast<object>().ToArray(),
            ["layers"] = Layers.Select(x => new Dictionary<string, object> { ["layerId"] = x.LayerId, ["layerName"] = x.LayerName, ["modifier"] = x.Modifier, ["isHiddenFromPlayer"] = x.IsHiddenFromPlayer, ["reason"] = x.Reason }).Cast<object>().ToArray()
        };
    }
}
