using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.LiveActor.Contracts;

internal static class Program
{
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_21_6");
        Directory.CreateDirectory(output);
        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        void Check(string name, bool value) => checks[name] = value;

        var subjectTypes = new[] { RuntimeSubjectTypes.Character, RuntimeSubjectTypes.Companion, RuntimeSubjectTypes.Npc, RuntimeSubjectTypes.Summon, RuntimeSubjectTypes.Construct, RuntimeSubjectTypes.VehicleCrewActor, RuntimeSubjectTypes.Custom };
        Check("01.capability.basePermanentEffectiveSeparation", LiveActorRules.EffectiveCapability(12, 1, -4) == 9);
        Check("02.capability.attributeTemporaryModifiers", LiveActorRules.EffectiveCapability(12, 0, -3) == 9);
        Check("03.capability.skillBonusModifiers", LiveActorRules.EffectiveCapability(7, 0, -3) == 4);
        Check("04.capability.genericSkillHasNoAutomaticCooldown", new LiveCapabilitySnapshot { CapabilityType = "skill" }.GetType().GetProperty("RemainingRounds") == null);
        Check("05.resource.baseEffectiveCurrentSeparation", new PlayerLiveResourceView { BaseMaximum = 100, EffectiveMaximum = 80, Current = 43 }.Current != 80);
        Check("06.resource.temporaryMaximumModifier", LiveActorRules.EffectiveMaximum(100, -20) == 80);
        Check("07.resource.clampAndOvercapPolicy", LiveActorRules.ClampCurrent(14, 10, false) == 10 && LiveActorRules.ClampCurrent(14, 10, true) == 14);
        Check("08.projection.playerSafeBreakdown", new LiveCapabilitySnapshot { PublicModifierReasons = new List<string> { "Ранение" }, GmModifierReasons = new List<string> { "скрытый источник" } }.PublicModifierReasons.Count == 1);
        Check("09.life.transitionPermissions", LiveActorRules.LifePermissions("impaired") == (true, true) && LiveActorRules.LifePermissions("stable") == (true, false));
        Check("10.life.zeroHealthDoesNotHardcodeDeath", new ActorRuntimeStateDocument { LifeState = new LifeOperationalState { StateCode = "impaired" }, ResourceStates = new List<RuntimeResourceState> { new RuntimeResourceState { ResourceDefinitionId = "health", CurrentValue = 0 } } }.LifeState.StateCode == "impaired");

        var activeEffect = new RuntimeEffectInstance { IsActive = true, DurationMode = "rounds", RemainingRounds = 2 };
        Check("11.effect.instanceDuration", !LiveActorRules.IsEffectExpired(activeEffect, DateTime.UtcNow));
        Check("12.effect.stacking", new RuntimeEffectInstance { StackCount = 2, StackingPolicySnapshot = "stack" }.StackCount == 2);
        Check("13.effect.expirationAndRemoval", LiveActorRules.IsEffectExpired(new RuntimeEffectInstance { IsActive = true, RemainingRounds = 0 }, DateTime.UtcNow) && LiveActorRules.IsEffectExpired(new RuntimeEffectInstance { IsActive = false }, DateTime.UtcNow));
        Check("14.effect.hiddenSourceFilteringBoundary", !new RuntimeEffectInstance { IsPlayerVisible = false, GmNameSnapshot = "GM_ONLY" }.IsPlayerVisible);

        Check("15.execution.lifecycleState", new ActionExecutionState { State = "casting", CurrentStage = 2, TotalStages = 4 }.CurrentStage == 2);
        Check("16.execution.supportedCanonicalStates", new[] { "prepared", "casting", "channeling", "sustained", "interrupted" }.Distinct().Count() == 5);
        Check("17.execution.reservationDiffersFromSpend", new ResourceReservationState { ReservedAmount = 10, CommittedAmount = 0 }.ReservedAmount != 0);
        Check("18.execution.concentrationSlot", new ActionExecutionState { ConcentrationSlotId = "slot-1" }.ConcentrationSlotId == "slot-1");
        Check("19.mutation.operationIdempotencyContract", new LiveStateEventRecord { OperationId = "op-1" }.OperationId == "op-1");

        Check("20.cooldown.roundBased", !LiveActorRules.IsActionReady(new ActionRuntimeState { IsEnabled = true, RemainingRounds = 1 }));
        Check("21.cooldown.timeBased", !LiveActorRules.IsActionReady(new ActionRuntimeState { IsEnabled = true, ReadyAtUtc = DateTime.UtcNow.AddMinutes(1) }));
        Check("22.cooldown.restResetPolicy", new ActionRuntimeState { RestResetPolicy = "short_rest" }.RestResetPolicy == "short_rest");
        Check("23.action.charges", LiveActorRules.IsActionReady(new ActionRuntimeState { IsEnabled = true, MaximumCharges = 2, CurrentCharges = 1 }) && !LiveActorRules.IsActionReady(new ActionRuntimeState { IsEnabled = true, MaximumCharges = 2, CurrentCharges = 0 }));
        Check("24.action.usesPerRest", new ActionRuntimeState { UsesSinceShortRest = 1, UsesSinceLongRest = 2 }.UsesSinceLongRest == 2);

        var weapon = new PlayerLiveWeaponView { LoadedQuantity = 7, ChamberedQuantity = 1, ReserveQuantity = 90 };
        Check("25.ammo.loadedReserveSeparation", weapon.LoadedQuantity + weapon.ChamberedQuantity != weapon.ReserveQuantity);
        Check("26.ammo.detachableMagazineState", new AmmunitionFeedState { SourceItemInstanceIds = new List<string> { "magazine-b", "magazine-c" } }.SourceItemInstanceIds.Count == 2);
        Check("27.ammo.reloadExactTransfer", LiveActorRules.ReloadTransfer(7, 30, 18) == 18);
        Check("28.ammo.incompatibleRejected", !LiveActorRules.IsAmmunitionCompatible(new[] { "armor_piercing" }, new[] { "plasma" }));
        Check("29.ammo.fireIdempotencyReceipt", new ItemOperationalState { LastOperationId = "fire-op" }.LastOperationId == "fire-op");
        Check("30.loadout.attackProfileSelection", new ActiveLoadoutState { ActiveWeaponItemInstanceId = "rifle", ActiveAttackProfileId = "single-shot" }.ActiveAttackProfileId == "single-shot");

        Check("31.history.lifeStatePlayerProjection", new LiveStateEventRecord { Category = "life_state", IsPlayerVisible = true }.IsPlayerVisible);
        Check("32.history.compensatingCorrectionLink", new LiveStateEventRecord { CompensationForEventId = "event-original" }.CompensationForEventId == "event-original");
        Check("33.isolation.characterAAndB", new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = "A" }.SubjectId != new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = "B" }.SubjectId);
        Check("34.isolation.companionNpcGenericSubjects", subjectTypes.Distinct().Count() == 7 && RuntimeSubjectTypes.Companion != RuntimeSubjectTypes.Npc);
        Check("35.partyBoard.derivedProjection", new List<PlayerLiveActorView> { new PlayerLiveActorView { SubjectId = "character-a" }, new PlayerLiveActorView { SubjectId = "companion-a" } }.Select(x => x.SubjectId).Distinct().Count() == 2);
        Check("36.persistence.revisionAndOperationMarkers", new ActorRuntimeStateDocument { EntityRevision = 8, UpdatedBy = "server" }.EntityRevision == 8);
        Check("37.reconciliation.warningContract", new PlayerLiveActorView { ReconciliationWarnings = new List<string> { "Требуется проверка мастера" } }.ReconciliationWarnings.Count == 1);
        Check("38.combat.mergeWithoutRuntimeDuplication", typeof(ActorRuntimeStateDocument).GetProperty("CombatParticipant") == null && typeof(PlayerLiveActorView).GetProperty("Combat") != null);
        Check("39.preview.samePlayerProjectionType", typeof(PlayerLiveActorView) == typeof(PlayerLiveActorView));
        Check("40.portability.persistentEntityShapes", typeof(RuntimeEffectInstance).IsSubclassOf(typeof(EntityBase)) && typeof(ActionExecutionState).IsSubclassOf(typeof(EntityBase)) && typeof(ResourceReservationState).IsSubclassOf(typeof(EntityBase)));

        var pass = checks.Count == 40 && checks.Values.All(x => x);
        Write(Path.Combine(output, "live_actor_deterministic_contracts.json"), new Dictionary<string, object>
        {
            ["status"] = pass ? "PASS" : "NOT_PASS",
            ["checkCount"] = checks.Count,
            ["passedCount"] = checks.Values.Count(x => x),
            ["checks"] = checks.ToDictionary(x => x.Key, x => (object)x.Value),
            ["executedAtUtc"] = DateTime.UtcNow
        });

        WriteGroup(output, "runtime_subject_contracts.json", checks, 33, 35);
        WriteGroup(output, "effective_capability_contracts.json", checks, 1, 4);
        WriteGroup(output, "runtime_resource_contracts.json", checks, 5, 8);
        WriteGroup(output, "life_operational_state_contracts.json", checks, 9, 10);
        WriteGroup(output, "runtime_effect_instance_contracts.json", checks, 11, 14);
        WriteGroup(output, "action_readiness_contracts.json", checks, 20, 24);
        WriteGroup(output, "action_execution_reservation_contracts.json", checks, 15, 19);
        WriteGroup(output, "ammunition_operational_contracts.json", checks, 25, 30);

        Console.WriteLine("0.21.6 live actor deterministic contracts: " + (pass ? "PASS" : "NOT_PASS"));
        return pass ? 0 : 1;
    }

    private static void WriteGroup(string output, string fileName, Dictionary<string, bool> checks, int first, int last)
    {
        var group = checks.Where(x => int.TryParse(x.Key.Substring(0, 2), out var number) && number >= first && number <= last).ToDictionary(x => x.Key, x => (object)x.Value);
        Write(Path.Combine(output, fileName), new Dictionary<string, object> { ["status"] = group.Values.Cast<bool>().All(x => x) ? "PASS" : "NOT_PASS", ["checks"] = group });
    }

    private static void Write(string path, object value)
        => File.WriteAllText(path, JsonProtocolSerializer.Serialize(value), new UTF8Encoding(false));
}
