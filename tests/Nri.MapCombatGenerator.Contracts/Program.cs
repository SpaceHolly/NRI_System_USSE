using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Nri.Shared.Domain;

namespace Nri.MapCombatGenerator.Contracts;

internal static class Program
{
    private static readonly Dictionary<string, bool> Checks = new(StringComparer.OrdinalIgnoreCase);

    private static int Main(string[] args)
    {
        var root = FindRoot();
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(root, "obj", "0_20_5"));
        Directory.CreateDirectory(output);

        DeterminismContracts();
        SourceContracts(root);
        UiContracts(root);
        WriteAudits(output, root);

        var pass = Checks.Values.All(value => value);
        Console.WriteLine("Map/combat/generator contracts: " + (pass ? "PASS" : "NOT_PASS"));
        foreach (var failed in Checks.Where(pair => !pair.Value)) Console.WriteLine("FAIL " + failed.Key);
        return pass ? 0 : 1;
    }

    private static void DeterminismContracts()
    {
        string Hash(string seed, int templateRevision = 3, int presetRevision = 5, double width = 1000, double height = 1000, string ruleSet = "ruleset_demo")
            => MapGenerationDeterminism0205.ComputeFingerprint("template_город", templateRevision, "preset_площадь", presetRevision, seed, width, height, ruleSet, "blueprint-hash");

        var first = Hash("seed-0205");
        Check("determinism.sameInput", first == Hash("seed-0205"));
        Check("determinism.differentSeed", first != Hash("seed-0205-other"));
        Check("determinism.templateRevision", first != Hash("seed-0205", templateRevision: 4));
        Check("determinism.presetRevision", first != Hash("seed-0205", presetRevision: 6));
        Check("determinism.ruleSet", first != Hash("seed-0205", ruleSet: "ruleset_other"));
        Check("determinism.bounds250", first != Hash("seed-0205", width: 250, height: 250));
        Check("determinism.bounds4000", first != Hash("seed-0205", width: 4000, height: 4000));
        Check("determinism.sha256", first.Length == 64 && first.All(Uri.IsHexDigit));
    }

    private static void SourceContracts(string root)
    {
        var generationService = Read(root, "Nri.Server/Application/MapGenerationService0205.cs");
        var generator = Read(root, "Nri.Server/Application/Services.SceneMapGenerator0165.cs");
        var tokens = Read(root, "Nri.Server/Application/Services.MapTokens0163.cs");
        var combatMap = Read(root, "Nri.Server/Application/Services.CombatMapIntegration0167.cs");
        var combat = Read(root, "Nri.Server/Application/Services.CombatTracker0166.cs");
        var portability = Read(root, "Nri.Server/Application/Services.DataPortability.cs");

        Check("generator.boundary", generationService.Contains("interface IMapGenerationService") && generationService.Contains("class MapGenerationService0205"));
        Check("generator.previewTransient", generationService.Contains("ConcurrentDictionary<string, MapGenerationPreviewHandle0205>") && !generator.Contains("PersistSceneMapGenerator0165Output"));
        Check("generator.identity", generationService.Contains("IMapIdentityResolver") && generationService.Contains("ResolveSceneMap"));
        Check("generator.exactlyOnce", generationService.Contains("OperationId") && generationService.Contains("AlreadyApplied") && generationService.Contains("Unique = true"));
        Check("generator.replayBeforePreviewLookup", generator.IndexOf("if (gate.AlreadyApplied)", StringComparison.Ordinal) < generator.IndexOf("currentTemplateRevision", StringComparison.Ordinal));
        Check("generator.staleRevision", generationService.Contains("current.EditorRevision != input.ExpectedMapRevision"));
        Check("generator.definitionRevision", generator.Contains("currentTemplateRevision") && generator.Contains("currentPresetRevision"));
        Check("generator.canonicalBoundsScale", generator.Contains("SceneMapGenerator0205ScaleForBounds") && generator.Contains("largestSide <= 500") && generator.Contains("largestSide <= 1000"));
        Check("generator.uniqueGeneratedIdentity", generator.Contains("ValidateSceneMapGenerator0205ApplyPlan") && generator.Contains("SceneMapGenerator0205StableLocalId") && generator.Contains("duplicate != null"));
        Check("generator.mutationBoundary", generator.Contains("_mapEditorMutationService.Mutate") && generator.Contains("MapEditorMutationRequest0203"));
        Check("generator.noPlayerApply", generator.Contains("RequireAdmin(context)"));

        Check("combat.tokenCoordinates", tokens.Contains("doc[\"X\"]") && tokens.Contains("doc[\"Y\"]") && combatMap.Contains("MapTokenId"));
        Check("combat.moveRevision", tokens.Contains("expectedRevision") && tokens.Contains("map_token_move_operations"));
        Check("combat.moveReplay", tokens.Contains("operationId") && tokens.Contains("CombatMap0167PublishTokenProjectionSync"));
        Check("combat.playerProjection", combatMap.Contains("PlayerMapProjectionService0204") || combatMap.Contains("_playerMapProjectionService"));
        Check("combat.canonicalTokenLookup",
            combatMap.Contains("MapToken0163DocsForMap(MapToken0163KindScene, SceneMap0162CanonicalMapId(map), includeHidden: true)")
            && combatMap.Contains("MapToken0163DocsForMap(MapToken0163KindScene, canonicalMapId, includeHidden: false)"));
        Check("combat.canonicalMapBinding",
            combat.Contains("ResolveSceneMap(suppliedSceneMapId)")
            && combat.Contains("doc[\"SceneMapId\"]")
            && combat.Contains("mapIdentity.CanonicalMapId"));
        Check("combat.endCleanup", combat.Contains("MapOverlayState") && combat.Contains("TurnStatus") && combat.Contains("completed"));
        Check("portability.tokens", portability.Contains("map_token_instances") && portability.Contains("map_token_move_operations"));
        Check("portability.generator", portability.Contains("scene_map_generation_runs") && portability.Contains("scene_map_generation_presets") && portability.Contains("scene_map_templates"));
    }

    private static void UiContracts(string root)
    {
        var generator = Read(root, "Nri.AdminClient/Views/Conduct/AdminLocationGeneratorView.xaml");
        var generatorViewModel = Read(root, "Nri.AdminClient/ViewModels/AdminLocationGeneratorViewModel.cs");
        var adminCombat = Read(root, "Nri.AdminClient/Views/Conduct/AdminCombatReadOnlyView.xaml");
        var playerCombat = Read(root, "Nri.PlayerClient/Views/Pages/CombatView.xaml");
        var requiredGeneratorIds = new[]
        {
            "AdminMapGenerator_TemplateList", "AdminMapGenerator_PresetList", "AdminMapGenerator_Seed",
            "AdminMapGenerator_Preview", "AdminMapGenerator_Apply", "AdminMapGenerator_CancelPreview"
        };
        var requiredCombatIds = new[]
        {
            "AdminCombatTracker_AdvancedContext", "AdminCombatTracker_CombatList", "AdminCombatMap_Overlay", "AdminCombatMap_ParticipantList", "AdminCombatMap_ActiveParticipant",
            "PlayerCombatMap_Overlay", "PlayerCombatMap_ActiveParticipant"
        };
        Check("ui.generatorAutomation", requiredGeneratorIds.All(generator.Contains));
        Check("ui.combatAutomation", requiredCombatIds.All(id => adminCombat.Contains(id) || playerCombat.Contains(id)));
        Check("ui.playerReadOnly", playerCombat.Contains("IsReadOnly=\"True\"") && !playerCombat.Contains("MoveMapTokenCommand"));
        Check("ui.noRawGeneratorIds", !generator.Contains("Text=\"MapId\"") && !generator.Contains("Text=\"RunId\"") && !generator.Contains("Raw JSON"));
        Check("ui.previewFingerprintAuthority",
            generatorViewModel.IndexOf("LastHash = GetString(payload, \"previewFingerprint\")", StringComparison.Ordinal)
            < generatorViewModel.IndexOf("LastHash = GetString(payload, \"normalizedHash\")", StringComparison.Ordinal));
        Check("ui.readableGeneratorSelectors", Count(generatorViewModel, "public override string ToString() => DisplayName;") >= 4);
    }

    private static void WriteAudits(string output, string root)
    {
        Write(Path.Combine(output, "map_generator_determinism_audit.json"), new
        {
            status = Status("determinism."),
            inputs = new[] { "templateRevision", "presetRevision", "seed", "mapBounds", "ruleSet", "blueprintFingerprint" },
            sizesMeters = new[] { "250x250", "1000x1000", "4000x4000" },
            unicodeNames = true,
            checks = Group("determinism.")
        });
        Write(Path.Combine(output, "map_generator_preview_apply_audit.json"), new
        {
            status = Status("generator."),
            previewPersistence = "transient in-memory only",
            applyBoundary = "MapEditorMutationService0203",
            checks = Group("generator.")
        });
        Write(Path.Combine(output, "map_combat_coordinate_authority_audit.json"), new
        {
            status = Status("combat."),
            coordinateAuthority = "map_token_instances.WorldX/WorldY",
            participantReference = "CombatParticipant.MapTokenId",
            checks = Group("combat.")
        });
        Write(Path.Combine(output, "map_combat_generator_ui_contract_audit.json"), new
        {
            status = Status("ui."),
            playerReadOnly = Checks["ui.playerReadOnly"],
            checks = Group("ui.")
        });
        Write(Path.Combine(output, "map_combat_generator_source_inventory.json"), new
        {
            status = Status(string.Empty),
            generatedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            paths = new object[]
            {
                Entry("generatorService", "Nri.Server/Application/MapGenerationService0205.cs", "IMapGenerationService", "transient preview; persisted applied run", "MapIdentityAdapter0202", "editor revision + operation id", "canonical", "none"),
                Entry("generatorHandlers", "Nri.Server/Application/Services.SceneMapGenerator0165.cs", "Admin generator commands", "templates/presets/runs + canonical map mutations", "canonical MapId", "template/preset/map revision", "0.16.5 generation retained behind 0.20.5 boundary", "no competing persistence path"),
                Entry("mapTokens", "Nri.Server/Application/Services.MapTokens0163.cs", "canonical token commands", "map_token_instances + move operations", "MapId/MapTokenId", "token revision + operation id", "canonical", "none"),
                Entry("combatOverlay", "Nri.Server/Application/Services.CombatMapIntegration0167.cs", "derived combat projection", "combat state references token; overlay transient", "MapTokenId", "combat/token revisions", "canonical projection", "none"),
                Entry("playerProjection", "Nri.Server/Application/PlayerMapProjectionService0204.cs", "player-safe projection", "read only", "canonical MapId", "projection revision", "canonical", "none"),
                Entry("portability", "Nri.Server/Application/Services.DataPortability.cs", "official export/import", "registered map, token, generator collections", "canonical ids", "dry-run boundary", "canonical", "official dry-run required live")
            }
        });
    }

    private static object Entry(string name, string path, string authority, string persistence, string identity, string revision, string legacy, string migration)
        => new { name, path, canonicalSource = path, readWriteAuthority = authority, identityResolution = identity, persistence, playerProjection = name == "playerProjection" || name == "combatOverlay", revisionBoundary = revision, legacyStatus = legacy, migrationObligation = migration };

    private static string FindRoot()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "NriSystem.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("NriSystem.sln not found.");
    }

    private static string Read(string root, string relative) => File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
    private static int Count(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }
    private static void Check(string name, bool value) => Checks[name] = value;
    private static string Status(string prefix) => Group(prefix).Values.All(value => value) ? "PASS" : "NOT_PASS";
    private static Dictionary<string, bool> Group(string prefix) => Checks.Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToDictionary(pair => pair.Key, pair => pair.Value);
    private static void Write(string path, object value) => File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(value));
}
