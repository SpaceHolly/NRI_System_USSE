using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nri.Shared.Domain;

internal static class Program
{
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var root = FindRoot();
        var checks = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["policy.alternatives.typed"] = TestPolicyAlternatives(),
            ["scope.arcana.self.allowed"] = Evaluate(new[] { MagicTargetScopeIds.Self }, null, MagicTargetScopeIds.Self).IsAllowed,
            ["scope.arcana.other.denied"] = !Evaluate(new[] { MagicTargetScopeIds.Self }, null, MagicTargetScopeIds.OtherActor).IsAllowed,
            ["scope.arcana.object.denied"] = !Evaluate(new[] { MagicTargetScopeIds.Self }, null, MagicTargetScopeIds.Object).IsAllowed,
            ["scope.arcana.position.denied"] = !Evaluate(new[] { MagicTargetScopeIds.Self }, null, MagicTargetScopeIds.Position).IsAllowed,
            ["scope.arcana.area.denied"] = !Evaluate(new[] { MagicTargetScopeIds.Self }, null, MagicTargetScopeIds.Area).IsAllowed,
            ["scope.nonArcana.notGloballySelfOnly"] = Evaluate(MagicTargetScopeIds.All, null, MagicTargetScopeIds.OtherActor).IsAllowed,
            ["scope.technique.intersection"] = Evaluate(MagicTargetScopeIds.All, new[] { MagicTargetScopeIds.Object }, MagicTargetScopeIds.Object).IsAllowed
                                                   && !Evaluate(MagicTargetScopeIds.All, new[] { MagicTargetScopeIds.Object }, MagicTargetScopeIds.Area).IsAllowed,
            ["runtime.noArcanaNameBranch"] = NoArcanaNameBranch(root),
            ["state.profileNative"] = ProfileNative(root),
            ["ui.noManualIds"] = NoManualIds(root),
            ["ui.readableTargetScopes"] = ReadableTargetScopes(root),
            ["ui.initialDevelopmentRouteDoesNotRequireActiveCharacter"] = InitialDevelopmentRouteUsesSelectedCharacter(root)
        };

        foreach (var check in checks) Console.WriteLine($"{check.Key}={(check.Value ? "PASS" : "FAIL")}");
        return checks.Values.All(x => x) ? 0 : 1;
    }

    private static bool TestPolicyAlternatives()
    {
        var policy = new InitialDevelopmentPolicy
        {
            ClassSelectionOptions = new List<InitialDevelopmentClassSelectionOption>
            {
                new() { ClassCount = 1, RankPerClass = 2, RequireDistinctClasses = true },
                new() { ClassCount = 2, RankPerClass = 1, RequireDistinctClasses = true }
            }
        };
        return policy.ClassSelectionOptions.Any(x => x.ClassCount == 1 && x.RankPerClass == 2)
               && policy.ClassSelectionOptions.Any(x => x.ClassCount == 2 && x.RankPerClass == 1 && x.RequireDistinctClasses)
               && policy.ClassSelectionOptions.All(x => !(x.ClassCount == 3 || x.RankPerClass > 2));
    }

    private static MagicTargetScopeEvaluation Evaluate(IEnumerable<string> method, IEnumerable<string>? technique, string requested) =>
        MagicTargetScopeEvaluator.Evaluate(method, technique, requested, "Метод");

    private static bool NoArcanaNameBranch(string root)
    {
        var source = File.ReadAllText(Path.Combine(root, "Nri.Server", "Application", "Services.InitialDevelopmentMagicTargeting02112.cs"), Encoding.UTF8);
        return source.IndexOf("method == Arcana", StringComparison.OrdinalIgnoreCase) < 0
               && source.IndexOf("method.Name ==", StringComparison.OrdinalIgnoreCase) < 0
               && source.IndexOf("Аркана", StringComparison.OrdinalIgnoreCase) < 0
               && source.IndexOf("MagicTargetScopeEvaluator.Evaluate", StringComparison.Ordinal) >= 0;
    }

    private static bool ProfileNative(string root)
    {
        var source = File.ReadAllText(Path.Combine(root, "Nri.Server", "Application", "Services.InitialDevelopmentMagicTargeting02112.cs"), Encoding.UTF8);
        return source.IndexOf("CharacterDevelopmentProfiles.ReplaceOne", StringComparison.Ordinal) >= 0
               && source.IndexOf("InitialDevelopmentGrantSources.InitialDevelopment", StringComparison.Ordinal) >= 0
               && source.IndexOf("LegacyCharacter", StringComparison.Ordinal) < 0
               && source.IndexOf("CharacterDetailsPayload", StringComparison.Ordinal) < 0
               && source.IndexOf("ResolveCharacterForClassSkill", StringComparison.Ordinal) < 0;
    }

    private static bool NoManualIds(string root)
    {
        var player = File.ReadAllText(Path.Combine(root, "Nri.PlayerClient", "Views", "Pages", "PlayerDevelopmentView.xaml"), Encoding.UTF8);
        var admin = File.ReadAllText(Path.Combine(root, "Nri.AdminClient", "Views", "Administration", "AdminMagicDefinitionsView.xaml"), Encoding.UTF8);
        return player.IndexOf("DevelopmentNodeId", StringComparison.Ordinal) < 0
               && player.IndexOf("MagicMethodNodeId", StringComparison.Ordinal) < 0
               && admin.IndexOf("AdminMagicDefinitions_TargetSelf", StringComparison.Ordinal) >= 0;
    }

    private static bool ReadableTargetScopes(string root)
    {
        var xaml = File.ReadAllText(Path.Combine(root, "Nri.AdminClient", "Views", "Administration", "AdminMagicDefinitionsView.xaml"), Encoding.UTF8);
        var seed = File.ReadAllText(Path.Combine(root, "scripts", "dev_canonical_core_references_seed_0_22_gate1.ps1"), Encoding.UTF8);
        return new[] { "На себя", "Другой персонаж", "Объект", "Точка", "Область", "AdminMagicDefinitions_TargetScopeSummary" }.All(xaml.Contains)
               && seed.IndexOf("Аркана может применяться только на самого использующего.", StringComparison.Ordinal) >= 0
               && seed.IndexOf("TargetScope = SelfOnly", StringComparison.Ordinal) < 0;
    }

    private static bool InitialDevelopmentRouteUsesSelectedCharacter(string root)
    {
        var source = File.ReadAllText(Path.Combine(root, "Nri.PlayerClient", "ViewModels", "ViewModels.cs"), Encoding.UTF8);
        return source.IndexOf("!string.Equals(routeKey, \"development\", StringComparison.OrdinalIgnoreCase)", StringComparison.Ordinal) >= 0;
    }

    private static string FindRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "NriSystem.sln"))) return current;
            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }
        throw new DirectoryNotFoundException("NriSystem.sln not found.");
    }
}
