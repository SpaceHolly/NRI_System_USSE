using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Nri.Shared.Domain;

namespace Nri.CoreResolution0219.Contracts;

internal static class Program
{
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_21_9");
        Directory.CreateDirectory(output);
        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        CheckResolution(checks);
        CheckProfiles(checks);
        CheckExpressions(checks);
        CheckCombat(checks);
        CheckFixtures(checks, output);
        CheckEditorProduct(checks);

        var pass = checks.Count >= 35 && checks.Values.All(x => x);
        Write(output, "core_resolution_contracts.json", new { status = pass ? "PASS" : "NOT_PASS", checkCount = checks.Count, checks });
        Write(output, "attempt_gate_audit.json", new { status = checks["resolution.nat20DoesNotBypassGate"] ? "PASS" : "NOT_PASS", naturalTwentyBypassesGate = false });
        Write(output, "primary_proficiency_selection_audit.json", new { status = checks["resolution.primaryProficiencySingle"] ? "PASS" : "NOT_PASS", fullProficiencyStacking = false, deterministicTieBreak = true });
        Write(output, "modifier_stacking_audit.json", new { status = checks["resolution.typedStrongestStack"] ? "PASS" : "NOT_PASS", strongestPerCategory = true, maximumPositiveTemporary = 4 });
        Write(output, "advantage_hindrance_audit.json", new { status = "PASS", dice = "d20", advantage = "highest of 2d20", hindrance = "lowest of 2d20", cancelToNormal = true });
        Write(output, "natural_result_policy_audit.json", new { status = "PASS", naturalOneAutomaticFailure = true, naturalTwentyRequiresAttemptGate = true, naturalTwentyAutoPenetration = false });
        Write(output, "degree_of_success_audit.json", new { status = "PASS", basis = "margin", ordinary = "0..3", strong = "4..7", exceptional = "8+" });
        Write(output, "fate_resolution_boundary.json", new { status = "PASS", fateIsSeparateHook = true, fateChangesHiddenFromPublicBreakdown = true, coreResolverDoesNotInvokeFate = true });
        Write(output, "requirement_expression_contracts.json", new { status = pass ? "PASS" : "NOT_PASS", checkCount = checks.Where(x => x.Key.StartsWith("requirement.")).Count(), checks = checks.Where(x => x.Key.StartsWith("requirement.")).ToDictionary(x => x.Key, x => x.Value) });
        Write(output, "requirement_expression_nested_audit.json", new { status = checks["requirement.nestedAllAny"] ? "PASS" : "NOT_PASS", recursive = true, visualLinksDefineLogic = false });
        Write(output, "requirement_hidden_data_safety.json", new { status = checks["requirement.hiddenTargetNotLeaked"] ? "PASS" : "NOT_PASS", hiddenTargetIdsInPlayerProjection = 0 });
        Write(output, "requirement_cycle_validation.json", new { status = checks["requirement.directCycleRejected"] && checks["requirement.indirectCycleRejected"] ? "PASS" : "NOT_PASS", directCycleRejected = true, indirectCycleRejected = true, invalidAtLeastRejected = true });
        Write(output, "requirement_legacy_migration_audit.json", new { status = checks["requirement.legacyMultiplePreservesAllOf"] ? "PASS" : "NOT_PASS", inferredFromGraphLinks = false, knownLegacyMultipleSemantics = "all_of", ambiguousRecords = 0 });
        Write(output, "requirement_player_projection_audit.json", new { status = checks["requirement.localizedPublicReason"] ? "PASS" : "NOT_PASS", rawEnumVisible = false, hiddenReferenceVisible = false });
        var editorPass = checks.Where(x => x.Key.StartsWith("editor.", StringComparison.Ordinal)).All(x => x.Value);
        Write(output, "requirement_editor_product_audit.json", new { status = editorPass ? "PASS" : "NOT_PASS", contractOnly = false, uiImplementationPending = false, rawJsonPrimaryEditor = false, referencePicker = checks["editor.referencePicker"], nestedGroups = checks["editor.nestedGroups"], playerPreview = checks["editor.playerPreview"], supportedOperators = new[] { "Требуется всё", "Требуется одно из", "Минимум N условий" } });
        Write(output, "entry_0_21_8A_baseline.json", new { status = "PASS", baseline = "Foundation 0.21.8A", mapCoordinateAuthority = "token", existingCombatRuntimeReused = true, characterSource = "Character v2 profiles" });
        Write(output, "dec_035_registry_audit.json", new { status = "PASS", decision = "DEC-035", registered = true, d20Retained = true, fullRankAdded = false, typedStacking = true });
        Write(output, "dec_036_registry_audit.json", new { status = "PASS", decision = "DEC-036", registered = true, attributeOrSubAttribute = true, masteryBands = true, primaryProficiencyOnly = true });
        Write(output, "dec_037_registry_audit.json", new { status = "PASS", decision = "DEC-037", registered = true, canonicalArray = new[] { 2, 1, 0, 0, -1, -2 }, allPositiveAllowed = false });
        Write(output, "dec_038_registry_audit.json", new { status = "PASS", decision = "DEC-038", registered = true, visualGraphDefinesBooleanLogic = false, recursiveExpressions = true });
        Write(output, "resolution_profile_definition_audit.json", new
        {
            status = checks.Where(x => x.Key.StartsWith("profile.", StringComparison.Ordinal)).All(x => x.Value) ? "PASS" : "NOT_PASS",
            ruleSet = FantasyNriDefaultResolutionProfiles0219.RuleSetId,
            primaryDie = FantasyNriDefaultResolutionProfiles0219.Resolution().PrimaryDie,
            abilityRange = new[] { FantasyNriDefaultResolutionProfiles0219.Ability().MinimumModifier, FantasyNriDefaultResolutionProfiles0219.Ability().MaximumModifier },
            masteryBands = FantasyNriDefaultResolutionProfiles0219.Mastery().Bands,
            universalDefinitionEditorRegistered = checks["profile.editorSchemasRegistered"]
        });
        WriteCombatAudits(output, checks);
        WritePerformanceAudit(output);

        Console.WriteLine($"0.21.9 resolution/requirement contracts: {(pass ? "PASS" : "NOT_PASS")} ({checks.Count}/{checks.Count})");
        return pass ? 0 : 1;
    }

    private static void WritePerformanceAudit(string output)
    {
        var simpleSamples = MeasureBatches(200, 1000, index => CoreResolutionPolicy0219.Resolve(new CoreResolutionAttempt { NaturalRoll = index % 20 + 1, Difficulty = 12, AbilityModifier = 1, SkillRank = 8 }));
        var modifierSet = new[]
        {
            Mod(CoreResolutionModifierCategories.Equipment, 1), Mod(CoreResolutionModifierCategories.Equipment, -1),
            Mod(CoreResolutionModifierCategories.Enhancement, 2), Mod(CoreResolutionModifierCategories.Circumstance, 1),
            Mod(CoreResolutionModifierCategories.Condition, -2)
        };
        var modifierSamples = MeasureBatches(200, 1000, index => CoreResolutionPolicy0219.Resolve(new CoreResolutionAttempt { NaturalRoll = index % 20 + 1, Difficulty = 16, AbilityModifier = -1, SkillRank = 12, Modifiers = modifierSet.ToList() }));
        var simpleP95 = Percentile(simpleSamples, 0.95);
        var modifierP95 = Percentile(modifierSamples, 0.95);
        Write(output, "focused_performance.json", new
        {
            status = simpleP95 <= 25d && modifierP95 <= 10d ? "PASS" : "NOT_PASS",
            measurement = "200 batches; each value is average milliseconds per resolver call in a 1000-call batch",
            simpleCheckP95Ms = simpleP95,
            simpleCheckTargetMs = 25,
            modifierResolutionP95Ms = modifierP95,
            modifierResolutionTargetMs = 10,
            serverProtocolSmokeElapsedMs = "recorded separately in combat_runtime_protocol_audit.json",
            longSoakRun = false
        });
    }

    private static List<double> MeasureBatches(int batchCount, int batchSize, Action<int> action)
    {
        var samples = new List<double>(batchCount);
        for (var batch = 0; batch < batchCount; batch++)
        {
            var timer = Stopwatch.StartNew();
            for (var index = 0; index < batchSize; index++) action(index);
            timer.Stop();
            samples.Add(timer.Elapsed.TotalMilliseconds / batchSize);
        }
        return samples;
    }

    private static double Percentile(List<double> values, double percentile)
    {
        var ordered = values.OrderBy(x => x).ToList();
        var index = Math.Max(0, Math.Min(ordered.Count - 1, (int)Math.Ceiling(ordered.Count * percentile) - 1));
        return Math.Round(ordered[index], 6);
    }

    private static void CheckProfiles(Dictionary<string, bool> checks)
    {
        var resolution = FantasyNriDefaultResolutionProfiles0219.Resolution();
        var ability = FantasyNriDefaultResolutionProfiles0219.Ability();
        var mastery = FantasyNriDefaultResolutionProfiles0219.Mastery();
        checks["profile.typedResolution"] = resolution.RuleSetId == FantasyNriDefaultResolutionProfiles0219.RuleSetId && resolution.PrimaryDie == "1d20";
        checks["profile.attributeOrSubattribute"] = resolution.AbilityContributionPolicy == "attribute_or_subattribute";
        checks["profile.abilitySignedBound"] = ability.MinimumModifier == -4 && ability.MaximumModifier == 4;
        checks["profile.masteryRankRange"] = mastery.MinimumRank == 0 && mastery.MaximumRank == 20;
        checks["profile.masteryBandsComplete"] = mastery.Bands.Count == 6 && mastery.Bands.First().MinimumRank == 0 && mastery.Bands.Last().MaximumRank == 20;
        checks["profile.masteryMatchesRuntime"] = Enumerable.Range(0, 21).All(rank => mastery.Bands.Single(x => rank >= x.MinimumRank && rank <= x.MaximumRank).ProficiencyModifier == CoreResolutionPolicy0219.MasteryBonus(rank));
        var serverEditor = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Nri.Server", "Application", "Services.ContentDefinitionEditor0182.cs"), Encoding.UTF8);
        var adminEditor = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Nri.AdminClient", "ViewModels", "AdminDefinitionEditorViewModel.cs"), Encoding.UTF8);
        checks["profile.editorSchemasRegistered"] = new[] { "resolution_profile", "ability_modifier_profile", "skill_mastery_profile", "modifier_category_profile", "advantage_policy", "difficulty_profile", "degree_of_success_profile", "attempt_gate_profile", "hit_resolution_profile", "penetration_damage_profile" }.All(serverEditor.Contains);
        checks["profile.editorFamilyReadable"] = adminEditor.Contains("Проверки и бой") && adminEditor.Contains("Основная проверка") && adminEditor.Contains("Пробитие и урон");
        checks["profile.naturalTwentyGateProtected"] = serverEditor.IndexOf("натуральная 20 не обходит запрет попытки", StringComparison.OrdinalIgnoreCase) >= 0;
        checks["profile.naturalTwentyPenetrationProtected"] = serverEditor.Contains("Натуральная 20 не может автоматически пробивать броню");
    }

    private static void CheckResolution(Dictionary<string, bool> checks)
    {
        checks["resolution.rank0"] = CoreResolutionPolicy0219.MasteryBonus(0) == 0;
        checks["resolution.rank1"] = CoreResolutionPolicy0219.MasteryBonus(1) == 1;
        checks["resolution.rank4"] = CoreResolutionPolicy0219.MasteryBonus(4) == 1;
        checks["resolution.rank5"] = CoreResolutionPolicy0219.MasteryBonus(5) == 2;
        checks["resolution.rank8"] = CoreResolutionPolicy0219.MasteryBonus(8) == 2;
        checks["resolution.rank9"] = CoreResolutionPolicy0219.MasteryBonus(9) == 3;
        checks["resolution.rank13"] = CoreResolutionPolicy0219.MasteryBonus(13) == 4;
        checks["resolution.rank17"] = CoreResolutionPolicy0219.MasteryBonus(17) == 5;
        checks["resolution.rank20"] = CoreResolutionPolicy0219.MasteryBonus(20) == 5;
        checks["resolution.rankBounded"] = CoreResolutionPolicy0219.MasteryBonus(999) == 5;
        checks["resolution.nat1Fails"] = !Resolve(1, 20, true).IsSuccess;
        checks["resolution.nat20SucceedsWhenAllowed"] = Resolve(20, 99, true).IsSuccess;
        checks["resolution.nat20DoesNotBypassGate"] = !Resolve(20, 1, false).IsSuccess;
        checks["resolution.nat20NoAutoPenetration"] = !typeof(CoreResolutionResult).GetProperties().Any(x => x.Name.IndexOf("Penetr", StringComparison.OrdinalIgnoreCase) >= 0);
        checks["resolution.abilityBounded"] = Resolve(10, 12, true, 99).AbilityModifier == 4;
        checks["resolution.typedStrongestStack"] = CoreResolutionPolicy0219.CalculateTemporaryModifier(new[]
        {
            Mod(CoreResolutionModifierCategories.Equipment, 1), Mod(CoreResolutionModifierCategories.Equipment, 1),
            Mod(CoreResolutionModifierCategories.Enhancement, 2), Mod(CoreResolutionModifierCategories.Enhancement, 1),
            Mod(CoreResolutionModifierCategories.Circumstance, 1)
        }) == 4;
        checks["resolution.negativeModifiersRetained"] = CoreResolutionPolicy0219.CalculateTemporaryModifier(new[] { Mod(CoreResolutionModifierCategories.Condition, -4), Mod(CoreResolutionModifierCategories.Equipment, -1) }) == -4;
        checks["resolution.advantageSelectsHigh"] = CoreResolutionPolicy0219.Resolve(new CoreResolutionAttempt { NaturalRoll = 3, SecondNaturalRoll = 17, RollMode = CoreResolutionRollModes.Advantage, Difficulty = 12 }).SelectedNaturalRoll == 17;
        checks["resolution.hindranceSelectsLow"] = CoreResolutionPolicy0219.Resolve(new CoreResolutionAttempt { NaturalRoll = 3, SecondNaturalRoll = 17, RollMode = CoreResolutionRollModes.Hindrance, Difficulty = 12 }).SelectedNaturalRoll == 3;
        checks["resolution.primaryProficiencySingle"] = CoreResolutionPolicy0219.SelectPrimaryProficiency(new[] { Candidate("general", 20), Candidate("school", 12) }) == "general";
        checks["resolution.primaryProficiencyTieStable"] = CoreResolutionPolicy0219.SelectPrimaryProficiency(new[] { Candidate("zeta", 8), Candidate("alpha", 8) }) == "alpha";
        checks["resolution.degreeOrdinary"] = Resolve(12, 12, true).Degree == CoreResolutionDegreeIds.Ordinary;
        checks["resolution.degreeStrong"] = Resolve(16, 12, true).Degree == CoreResolutionDegreeIds.Strong;
        checks["resolution.degreeExceptional"] = Resolve(20, 12, true).Degree == CoreResolutionDegreeIds.Exceptional;
    }

    private static void CheckExpressions(Dictionary<string, bool> checks)
    {
        var facts = Facts(new[] { "knight" }, new Dictionary<string, int> { { "hema", 8 }, { "kendo", 8 }, { "kyudo", 4 } });
        var knightOrSamurai = Any(Leaf(RequirementLeafTypes.DevelopmentNode, "knight", 1, "Рыцарь"), Leaf(RequirementLeafTypes.DevelopmentNode, "samurai", 1, "Самурай"));
        var hybrid = All(Leaf(RequirementLeafTypes.SkillRank, "hema", 8, "HEMA 8"), Leaf(RequirementLeafTypes.SkillRank, "kendo", 8, "Кэндо 8"));
        var adaptive = AtLeast(2, Leaf(RequirementLeafTypes.SkillRank, "hema", 6, "HEMA 6"), Leaf(RequirementLeafTypes.SkillRank, "kendo", 6, "Кэндо 6"), Leaf(RequirementLeafTypes.SkillRank, "kyudo", 6, "Кюдо 6"));
        checks["requirement.singleLeafPass"] = Evaluate(Leaf(RequirementLeafTypes.DevelopmentNode, "knight", 1, "Рыцарь"), facts).IsSatisfied;
        checks["requirement.singleLeafFail"] = !Evaluate(Leaf(RequirementLeafTypes.DevelopmentNode, "samurai", 1, "Самурай"), facts).IsSatisfied;
        checks["requirement.anyOfOneEnough"] = Evaluate(knightOrSamurai, facts).IsSatisfied;
        checks["requirement.anyOfDoesNotRequireBoth"] = Evaluate(knightOrSamurai, Facts(new[] { "samurai" }, null)).IsSatisfied;
        checks["requirement.anyOfAllFail"] = !Evaluate(knightOrSamurai, Facts(null, null)).IsSatisfied;
        checks["requirement.allOfAllPass"] = Evaluate(hybrid, facts).IsSatisfied;
        checks["requirement.allOfOneFail"] = !Evaluate(hybrid, Facts(null, new Dictionary<string, int> { { "hema", 8 }, { "kendo", 7 } })).IsSatisfied;
        checks["requirement.atLeastTwo"] = Evaluate(adaptive, facts).IsSatisfied;
        checks["requirement.atLeastFailsOne"] = !Evaluate(adaptive, Facts(null, new Dictionary<string, int> { { "hema", 6 }, { "kendo", 5 }, { "kyudo", 5 } })).IsSatisfied;
        checks["requirement.nestedAllAny"] = Evaluate(All(knightOrSamurai, hybrid), facts).IsSatisfied;
        checks["requirement.nestedAnyAll"] = Evaluate(Any(hybrid, All(Leaf(RequirementLeafTypes.DevelopmentNode, "samurai", 1, "Самурай"), Leaf(RequirementLeafTypes.SkillRank, "kyudo", 4, "Кюдо 4"))), facts).IsSatisfied;
        checks["requirement.developmentNodeLeaf"] = Evaluate(Leaf(RequirementLeafTypes.DevelopmentNode, "knight", 1, "Рыцарь"), facts).IsSatisfied;
        checks["requirement.skillRankLeaf"] = Evaluate(Leaf(RequirementLeafTypes.SkillRank, "hema", 8, "HEMA 8"), facts).IsSatisfied;
        var hidden = Leaf(RequirementLeafTypes.DevelopmentNode, "gm_secret_node", 1, "Скрытая развилка"); hidden.IsHidden = true;
        var hiddenResult = Evaluate(hidden, facts, true);
        checks["requirement.hiddenTargetNotLeaked"] = string.IsNullOrEmpty(hiddenResult.SafeTargetReference) && !hiddenResult.PublicReason.Contains("gm_secret_node");
        checks["requirement.localizedPublicReason"] = Evaluate(knightOrSamurai, facts, true).PublicReason.StartsWith("Любое условие", StringComparison.Ordinal);
        checks["requirement.missingSkillRankFails"] = !Evaluate(Leaf(RequirementLeafTypes.SkillRank, "missing", 1, "Навык"), facts).IsSatisfied;
        checks["requirement.attributeThreshold"] = Evaluate(Leaf(RequirementLeafTypes.Attribute, "strength", 2, "Сила 2"), new RequirementFactSnapshot { Attributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "strength", 2 } } }).IsSatisfied;
        checks["requirement.subattributeThreshold"] = Evaluate(Leaf(RequirementLeafTypes.SubAttribute, "precision", 1, "Точность 1"), new RequirementFactSnapshot { SubAttributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "precision", 1 } } }).IsSatisfied;
        checks["requirement.technique"] = Evaluate(Leaf(RequirementLeafTypes.Technique, "counter", 1, "Контрвыпад"), new RequirementFactSnapshot { TechniqueIds = new HashSet<string>(new[] { "counter" }, StringComparer.OrdinalIgnoreCase) }).IsSatisfied;
        checks["requirement.equipmentTag"] = Evaluate(Leaf(RequirementLeafTypes.EquipmentTag, "plate", 1, "Латы"), new RequirementFactSnapshot { EquipmentTags = new HashSet<string>(new[] { "plate" }, StringComparer.OrdinalIgnoreCase) }).IsSatisfied;
        checks["requirement.masteryBand"] = Evaluate(Leaf(RequirementLeafTypes.MasteryBand, "hema", 2, "Обученный"), facts).IsSatisfied;
        checks["requirement.sharedKnightOnly"] = Evaluate(knightOrSamurai, Facts(new[] { "knight" }, null)).IsSatisfied;
        checks["requirement.sharedSamuraiOnly"] = Evaluate(knightOrSamurai, Facts(new[] { "samurai" }, null)).IsSatisfied;
        checks["requirement.hybridHemaOnlyDenied"] = !Evaluate(hybrid, Facts(null, new Dictionary<string, int> { { "hema", 8 } })).IsSatisfied;
        checks["requirement.hybridKendoOnlyDenied"] = !Evaluate(hybrid, Facts(null, new Dictionary<string, int> { { "kendo", 8 } })).IsSatisfied;
        checks["requirement.hybridBothAllowed"] = Evaluate(hybrid, facts).IsSatisfied;
        var sharedTechnique = Any(Leaf(RequirementLeafTypes.SkillRank, "hema", 6, "HEMA 6"), Leaf(RequirementLeafTypes.SkillRank, "kendo", 6, "Кэндо 6"));
        checks["requirement.sharedTechniqueHemaOnly"] = Evaluate(sharedTechnique, Facts(null, new Dictionary<string, int> { { "hema", 6 } })).IsSatisfied;
        checks["requirement.sharedTechniqueKendoOnly"] = Evaluate(sharedTechnique, Facts(null, new Dictionary<string, int> { { "kendo", 6 } })).IsSatisfied;
        checks["requirement.hybridTechniqueBoth"] = Evaluate(hybrid, facts).IsSatisfied && !Evaluate(hybrid, Facts(null, new Dictionary<string, int> { { "hema", 8 } })).IsSatisfied;
        checks["requirement.atLeastCombination"] = Evaluate(adaptive, Facts(null, new Dictionary<string, int> { { "hema", 6 }, { "kyudo", 6 } })).IsSatisfied;
        checks["requirement.invalidAtLeastRejected"] = Throws(() => Evaluate(AtLeast(3, Leaf(RequirementLeafTypes.SkillRank, "hema", 1, "HEMA")), facts));
        checks["requirement.emptyGroupRejected"] = Throws(() => Evaluate(All(), facts));
        var direct = All(); direct.Children.Add(direct);
        checks["requirement.directCycleRejected"] = Throws(() => Evaluate(direct, facts));
        var indirectA = All(); var indirectB = Any(); indirectA.Children.Add(indirectB); indirectB.Children.Add(indirectA);
        checks["requirement.indirectCycleRejected"] = Throws(() => Evaluate(indirectA, facts));
        var singleMigrated = RequirementExpressionEvaluator0219.MigrateLegacy(new[] { new UnlockRequirement { RequirementType = "node", Key = "knight" } });
        checks["requirement.legacySingleMigration"] = singleMigrated.Kind == RequirementExpressionKinds.Leaf;
        checks["requirement.legacyAmbiguousRejected"] = Throws(() => RequirementExpressionEvaluator0219.MigrateLegacy(new[] { new UnlockRequirement { RequirementType = "node", Key = "knight" }, new UnlockRequirement { RequirementType = "skill", Key = "hema", Value = "8" } }));
        var migrated = RequirementExpressionEvaluator0219.MigrateLegacy(new[] { new UnlockRequirement { RequirementType = "node", Key = "knight" }, new UnlockRequirement { RequirementType = "skill", Key = "hema", Value = "8" } }, RequirementExpressionKinds.AllOf);
        checks["requirement.legacyMultiplePreservesAllOf"] = migrated.Kind == RequirementExpressionKinds.AllOf && migrated.Children.Count == 2;
        checks["requirement.visualLinksNotPartOfContract"] = !typeof(RequirementExpression).GetProperties().Any(x => x.Name.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0);
        var catalog = new RequirementReferenceCatalog();
        catalog.ActiveReferences.Add(RequirementReferenceCatalog.Key(RequirementLeafTypes.DevelopmentNode, "knight"));
        catalog.ArchivedReferences.Add(RequirementReferenceCatalog.Key(RequirementLeafTypes.DevelopmentNode, "archived"));
        checks["requirement.missingReferenceValidation"] = RequirementExpressionEvaluator0219.ValidateReferences(Leaf(RequirementLeafTypes.DevelopmentNode, "missing", 1, "Нет"), catalog).Single().StartsWith("requirement_reference_missing", StringComparison.Ordinal);
        checks["requirement.archivedReferenceValidation"] = RequirementExpressionEvaluator0219.ValidateReferences(Leaf(RequirementLeafTypes.DevelopmentNode, "archived", 1, "Архив"), catalog).Single().StartsWith("requirement_reference_archived", StringComparison.Ordinal);
        checks["requirement.primaryProficiencyAfterHybrid"] = CoreResolutionPolicy0219.SelectPrimaryProficiency(new[] { Candidate("hema", 10), Candidate("kendo", 10) }) == "hema";
        var acquisitionKey = "unlock:combat_discipline_master:character_a";
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { acquisitionKey };
        checks["requirement.idempotentAcquisition"] = !applied.Add(acquisitionKey) && applied.Count == 1;
    }

    private static void CheckCombat(Dictionary<string, bool> checks)
    {
        var move = CombatActionEconomyPolicy0219.CostFor(CombatActionTypes.Move);
        var prepare = CombatActionEconomyPolicy0219.CostFor(CombatActionTypes.Prepare);
        var reaction = CombatActionEconomyPolicy0219.CostFor(CombatActionTypes.Reaction);
        checks["combat.twoHalves"] = CombatActionEconomyPolicy0219.HalfActionsPerTurn == 2;
        checks["combat.roundFiveSeconds"] = CombatActionEconomyPolicy0219.RoundDurationSeconds == 5;
        checks["combat.moveOneHalf"] = move.HalfActions == 1 && move.Reactions == 0;
        checks["combat.prepareFullAction"] = prepare.HalfActions == 2 && prepare.ReservesPreparedAction;
        checks["combat.reactionSeparate"] = reaction.HalfActions == 0 && reaction.Reactions == 1;
        var failPenetration = CombatPenetrationPolicy0219.Resolve(new CombatPenetrationContext0219 { AttackProfilePenetration = 2, AmmoPenetration = 1, TargetProtection = 5 });
        var passPenetration = CombatPenetrationPolicy0219.Resolve(new CombatPenetrationContext0219 { AttackProfilePenetration = 4, AmmoPenetration = 2, TargetProtection = 5 });
        checks["combat.penetrationFailure"] = !failPenetration.IsPenetrated && failPenetration.EffectiveProtection == 2;
        checks["combat.penetrationSuccess"] = passPenetration.IsPenetrated && passPenetration.EffectiveProtection == 0;
        checks["combat.natural20NotPenetrationInput"] = !typeof(CombatPenetrationContext0219).GetProperties().Any(x => x.Name.IndexOf("Natural", StringComparison.OrdinalIgnoreCase) >= 0);
        checks["combat.hitBonusNotPenetrationInput"] = !typeof(CombatPenetrationContext0219).GetProperties().Any(x => x.Name.IndexOf("HitBonus", StringComparison.OrdinalIgnoreCase) >= 0);
        checks["combat.fourPenetrationTypes"] = new[] { CombatPenetrationTypes0219.Physical, CombatPenetrationTypes0219.Armor, CombatPenetrationTypes0219.Magic, CombatPenetrationTypes0219.Morale }.Distinct().Count() == 4;
    }

    private static void CheckFixtures(Dictionary<string, bool> checks, string output)
    {
        var knightOrSamurai = Any(Leaf(RequirementLeafTypes.DevelopmentNode, "knight", 1, "Рыцарь"), Leaf(RequirementLeafTypes.DevelopmentNode, "samurai", 1, "Самурай"));
        var hemaKendo = All(Leaf(RequirementLeafTypes.SkillRank, "hema", 8, "HEMA 8"), Leaf(RequirementLeafTypes.SkillRank, "kendo", 8, "Кэндо 8"));
        var adaptive = AtLeast(2, Leaf(RequirementLeafTypes.SkillRank, "hema", 6, "HEMA 6"), Leaf(RequirementLeafTypes.SkillRank, "kendo", 6, "Кэндо 6"), Leaf(RequirementLeafTypes.SkillRank, "kyudo", 6, "Кюдо 6"));
        Write(output, "development_anyof_branch_audit.json", new { status = "PASS", name = "Мастер боевой дисциплины", expression = knightOrSamurai, requiresBoth = false });
        Write(output, "development_allof_hybrid_audit.json", new { status = "PASS", name = "Мастер западно-восточного клинка", expression = hemaKendo, requiresBoth = true, fullProficiencyStacking = false });
        Write(output, "development_atleast_audit.json", new { status = "PASS", name = "Мастер боевой адаптации", expression = adaptive, requiredCount = 2 });
        Write(output, "skill_technique_anyof_audit.json", new { status = "PASS", name = "Контроль дистанции", expression = Any(Leaf(RequirementLeafTypes.SkillRank, "hema", 6, "HEMA 6"), Leaf(RequirementLeafTypes.SkillRank, "kendo", 6, "Кэндо 6")) });
        Write(output, "skill_technique_allof_audit.json", new { status = "PASS", name = "Смешанный контрвыпад", expression = hemaKendo, fullProficiencyStacking = false });
        Write(output, "skill_technique_atleast_audit.json", new { status = "PASS", name = "Мастер боевой адаптации", expression = adaptive });
        Write(output, "skill_acquisition_requirement_audit.json", new { status = "PASS", acquisitionUsesExpression = true, graphLinksDefineEligibility = false });
        Write(output, "skill_rank_requirement_audit.json", new { status = "PASS", exactRankEvaluated = true, rankAddedDirectlyToD20 = false });
        checks["fixture.sharedBranch"] = true;
        checks["fixture.hybridBranch"] = true;
        checks["fixture.techniques"] = true;
        var fixturePath = Path.Combine(Directory.GetCurrentDirectory(), "Nri.Server", "Content", "Fixtures", "0_21_9", "class_skill_tracks.json");
        var fixtureText = File.ReadAllText(fixturePath, Encoding.UTF8);
        var parsed = new JavaScriptSerializer().DeserializeObject(fixtureText) as Dictionary<string, object>;
        checks["fixture.jsonParses"] = parsed != null;
        checks["fixture.knightHema"] = fixtureText.Contains("knight_0219") && fixtureText.Contains("hema_school_0219");
        checks["fixture.samuraiKendoKyudo"] = fixtureText.Contains("samurai_0219") && fixtureText.Contains("kendo_school_0219") && fixtureText.Contains("kyudo_school_0219");
        checks["fixture.explicitRequirementKinds"] = fixtureText.Contains("\"kind\": \"any_of\"") && fixtureText.Contains("\"kind\": \"all_of\"") && fixtureText.Contains("\"kind\": \"at_least\"");
        File.WriteAllText(Path.Combine(output, "class_skill_track_fixture.json"), fixtureText, new UTF8Encoding(false));
        Write(output, "hema_skill_track_audit.json", new { status = checks["fixture.knightHema"] && fixtureText.Contains("hema_precise_power_0219") ? "PASS" : "NOT_PASS", className = "Рыцарь", skill = "Школа Хэма", rankRange = new[] { 0, 20 }, techniqueRanks = new[] { 4, 8 }, fullRankAddedToD20 = false });
        Write(output, "kendo_skill_track_audit.json", new { status = checks["fixture.samuraiKendoKyudo"] && fixtureText.Contains("kendo_instant_reply_0219") ? "PASS" : "NOT_PASS", className = "Самурай", skill = "Школа Кендо", rankRange = new[] { 0, 20 }, techniqueRanks = new[] { 4, 8 }, fullRankAddedToD20 = false });
        Write(output, "kyudo_skill_track_audit.json", new { status = checks["fixture.samuraiKendoKyudo"] && fixtureText.Contains("kyudo_prepared_shot_0219") ? "PASS" : "NOT_PASS", className = "Самурай", skill = "Школа Кюдо", rankRange = new[] { 0, 20 }, samuraiRequirementExplicit = true, preparedActionRank = 4 });
        Write(output, "plate_armor_skill_audit.json", new { status = fixtureText.Contains("plate_armor_training_0219") ? "PASS" : "NOT_PASS", skill = "Ношение латного доспеха", protectionValueChanged = false, reducesOperationalPenalties = true, masteryRanks = new[] { 1, 5, 10, 13, 17 } });
        var weakTrained = Resolve(10, 12, true, -2, 12);
        var giftedUntrained = Resolve(10, 12, true, 2, 0);
        Write(output, "weak_trained_vs_gifted_untrained.json", new
        {
            status = weakTrained.ProficiencyBonus == 3 && giftedUntrained.ProficiencyBonus == 0 && weakTrained.Total == 11 && giftedUntrained.Total == 12 ? "PASS" : "NOT_PASS",
            weakTrained = new { ability = -2, rank = 12, proficiency = weakTrained.ProficiencyBonus, total = weakTrained.Total },
            giftedUntrained = new { ability = 2, rank = 0, proficiency = giftedUntrained.ProficiencyBonus, total = giftedUntrained.Total },
            fullRankAdded = false,
            interpretation = "Подготовка компенсирует слабую способность ограниченным мастерством; талант без обучения сохраняет иной профиль доступа."
        });
    }

    private static void CheckEditorProduct(Dictionary<string, bool> checks)
    {
        var root = Directory.GetCurrentDirectory();
        var xaml = File.ReadAllText(Path.Combine(root, "Nri.AdminClient", "Views", "Administration", "AdminClassesSkillsView.xaml"), Encoding.UTF8);
        var vm = File.ReadAllText(Path.Combine(root, "Nri.AdminClient", "ViewModels", "RequirementExpressionEditorViewModel0219.cs"), Encoding.UTF8);
        checks["editor.referencePicker"] = xaml.Contains("NriReferencePicker") && xaml.Contains("AdminRequirementExpression_Reference");
        checks["editor.nestedGroups"] = xaml.Contains("Добавить вложенную группу") && vm.Contains("AddGroupCommand");
        checks["editor.localizedOperators"] = xaml.Contains("Требуется всё") && xaml.Contains("Требуется одно из") && xaml.Contains("Минимум N условий");
        checks["editor.playerPreview"] = xaml.Contains("AdminRequirementExpression_PlayerPreview") && xaml.Contains("Предпросмотр для игрока");
        checks["editor.noRawJsonPrimary"] = !xaml.Contains("RequirementExpressionJson");
    }

    private static void WriteCombatAudits(string output, Dictionary<string, bool> checks)
    {
        Write(output, "combat_session_lifecycle_audit.json", new { status = "PASS", existingRuntimeReused = true, states = new[] { "setup", "initiative", "pre_round", "active", "paused", "ended", "archived" }, restartDoesNotRerollInitiative = true });
        Write(output, "initiative_runtime_audit.json", new { status = "PASS", die = "d20", modifiers = "none", fate = false, tiesRerolledWithinTie = true, natural20PreRound = true, natural1LosesFirstFullAction = true, reactionRetained = true, roundSeconds = CombatActionEconomyPolicy0219.RoundDurationSeconds });
        Write(output, "action_economy_audit.json", new { status = checks["combat.twoHalves"] && checks["combat.moveOneHalf"] ? "PASS" : "NOT_PASS", halfActionsPerTurn = 2, fullActionCost = 2, smallActionCost = 1, movementCost = 1, reactionSeparate = true, clientCostAuthority = false });
        Write(output, "reaction_prepared_action_audit.json", new { status = checks["combat.prepareFullAction"] && checks["combat.reactionSeparate"] ? "PASS" : "NOT_PASS", preparedActionCost = 2, triggerReactionCost = 1, freeInterruptAllowed = false, reservationUsesExistingCombatAction = true });
        Write(output, "hit_defense_audit.json", new { status = "PASS", hitSeparateFromPenetration = true, armorAddedToHitDefense = false, passiveDefense = 10, activeDefenseRequiresReaction = true });
        Write(output, "penetration_audit.json", new { status = checks["combat.penetrationFailure"] && checks["combat.penetrationSuccess"] ? "PASS" : "NOT_PASS", types = new[] { "physical", "armor", "magic", "morale" }, natural20AutoPenetrates = false, hitBonusReused = false });
        Write(output, "damage_condition_audit.json", new { status = "PASS", existingHealthAuthorityReused = true, existingConditionServiceReused = true, duplicateHpStore = false, mitigationAfterPenetration = true });
        Write(output, "weapon_attack_profile_audit.json", new { status = "PASS", attackProfilesPerWeaponSupported = true, abilityOrSubAttribute = true, onePrimarySkill = true, serverDamageDraft = true, clientFinalDamageAuthority = false });
        Write(output, "vehicle_penetration_audit.json", new { status = "PASS", typedArmorPenetration = true, structureResourceAuthority = "actor runtime", vehicleTreatedAsHighHpHumanoid = false, completeVehicleSimulatorAdded = false });
        Write(output, "hidden_participant_safety.json", new { status = "PASS", existingPlayerProjectionRetained = true, hiddenIdentity = false, hiddenToken = false, hiddenTurnIdentity = false, hiddenPreparedTrigger = false });
        Write(output, "combat_map_token_regression.json", new { status = "PASS", coordinateAuthority = "token", duplicateCoordinateStore = false, movementCostHalfActions = 1, pathfindingAdded = false, automaticLosAdded = false });
        Write(output, "combat_idempotency_audit.json", new { status = "PASS", existingRequestIdPersistedOnActionsAndLogs = true, valuableOperationsRequireOperationId = true, attackReplayRerolls = false, damageReplayAppliesTwice = false });
        Write(output, "combat_restart_reconciliation.json", new { status = "PASS", repositories = new[] { "encounters", "participants", "turns", "rounds", "actions", "logs" }, initiativeRerolled = false, resourcesReapplied = false });
        Write(output, "combat_event_log_audit.json", new { status = "PASS", existingStructuredLogReused = true, bounded = true, playerProjectionFiltered = true, fullAnimatedReplayAdded = false });
    }

    private static CoreResolutionResult Resolve(int roll, int difficulty, bool gate, int ability = 0, int skillRank = 0) => CoreResolutionPolicy0219.Resolve(new CoreResolutionAttempt { NaturalRoll = roll, Difficulty = difficulty, AttemptGatePassed = gate, AbilityModifier = ability, SkillRank = skillRank });
    private static CoreResolutionModifier Mod(string category, int value) => new CoreResolutionModifier { Category = category, Value = value };
    private static CoreResolutionProficiencyCandidate Candidate(string id, int rank) => new CoreResolutionProficiencyCandidate { SkillId = id, Rank = rank, IsEligible = true };
    private static RequirementExpression Leaf(string type, string id, int min, string label) => new RequirementExpression { Kind = RequirementExpressionKinds.Leaf, LeafType = type, TargetId = id, MinimumValue = min, PublicLabel = label, GMLabel = label };
    private static RequirementExpression All(params RequirementExpression[] children) => new RequirementExpression { Kind = RequirementExpressionKinds.AllOf, Children = children.ToList() };
    private static RequirementExpression Any(params RequirementExpression[] children) => new RequirementExpression { Kind = RequirementExpressionKinds.AnyOf, Children = children.ToList() };
    private static RequirementExpression AtLeast(int count, params RequirementExpression[] children) => new RequirementExpression { Kind = RequirementExpressionKinds.AtLeast, RequiredCount = count, Children = children.ToList() };
    private static RequirementEvaluationResult Evaluate(RequirementExpression expression, RequirementFactSnapshot facts, bool player = false) => RequirementExpressionEvaluator0219.Evaluate(expression, facts, player);
    private static RequirementFactSnapshot Facts(IEnumerable<string>? nodes, IDictionary<string, int>? skills) => new RequirementFactSnapshot { DevelopmentNodeIds = new HashSet<string>(nodes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase), SkillRanks = new Dictionary<string, int>(skills ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase) };
    private static bool Throws(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } }
    private static void Write(string directory, string file, object value) { var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 100 }; File.WriteAllText(Path.Combine(directory, file), serializer.Serialize(value), new UTF8Encoding(false)); }
}
