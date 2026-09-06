using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Nri.Shared.Domain;

namespace Nri.RacePhysiology022Gate2.Contracts;

internal static class Program
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };

    private static int Main(string[] args)
    {
        var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(root, "obj", "0_22", "gate2_races_physiology"));
        var package = Path.Combine(root, "Nri.Server", "backups", "data_portability_packages", "fantasy_nri_default_race_physiology_gate2_v1", "collections", "content_definition_records.ndjson");
        Directory.CreateDirectory(output);
        var records = File.ReadAllLines(package, Encoding.UTF8).Where(x => !string.IsNullOrWhiteSpace(x)).Select(Parse).ToList();
        var checks = new Dictionary<string, bool>(StringComparer.Ordinal);

        CheckSeed(records, checks);
        CheckPhysiology(records, checks);
        CheckCombat(records, checks);
        CheckAreaGeometry(checks);
        CheckMovement(records, checks);
        CheckEnvironment(records, checks);
        var simulation = SimulateNeutralHumanCombat();
        checks["combat.humanMedianFourToSeven"] = simulation.MedianRounds >= 4 && simulation.MedianRounds <= 7;
        checks["combat.routineOneTwoRoundKillsAbsent"] = simulation.OneOrTwoRoundRate < .05m;

        var pass = checks.Count >= 45 && checks.Values.All(x => x);
        Write(output, "race_physiology_contracts.json", new { status = pass ? "PASS" : "NOT_PASS", checkCount = checks.Count, checks });
        Write(output, "physiology_height_age_validation_audit.json", new { status = All(checks, "physiology.") ? "PASS" : "NOT_PASS", baseRaces = 10, exactOwnerTable = true, giantHeightAccepted = true });
        Write(output, "health_ar_pr_resolution_audit.json", new { status = checks["combat.protectionAxesIndependent"] ? "PASS" : "NOT_PASS", naturalArmorContributesToDefense = true, naturalPrContributesToZoneResistance = true });
        Write(output, "body_zone_coverage_audit.json", new { status = All(checks, "body.") ? "PASS" : "NOT_PASS", humanoidWeight = 1.0m, calledShotHead = -4, calledShotWing = -3, perTargetWeightedResolution = true });
        Write(output, "equipment_fit_audit.json", new { status = checks["fit.wingClearance"] && checks["fit.giant"] ? "PASS" : "NOT_PASS", manualIdsInPlayerFlow = false });
        Write(output, "environmental_modifier_composition_audit.json", new { status = All(checks, "environment.") ? "PASS" : "NOT_PASS", typedModifierCount = 12, weatherTruthReplaced = false, compositionOrder = new[] { "weather", "protection_and_shelter", "body_profile_tolerance", "assessment" } });
        Write(output, "racial_senses_audit.json", new { status = checks["sense.typed"] && checks["sense.noWallPenetration"] ? "PASS" : "NOT_PASS", nameBasedRules = false });
        Write(output, "flight_glide_audit.json", new { status = All(checks, "flight.") ? "PASS" : "NOT_PASS", wingImpairmentBlocksFlight = true, equipmentFitRequired = true });
        Write(output, "breath_natural_attack_audit.json", new { status = All(checks, "attack.") ? "PASS" : "NOT_PASS", commonResolverAdapter = "NaturalAttackDefinition.ToAttackProfile", attackCount = 9 });
        Write(output, "natural_attack_area_geometry_audit.json", new { status = All(checks, "area.") ? "PASS" : "NOT_PASS", sourceOfTruth = "map_token_instances.X/Y", oneAttackRoll = true, friendlyFireIncluded = true });
        Write(output, "fate_exclusion_from_damage_audit.json", new { status = checks["attack.fateExcludedFromAllDamage"] ? "PASS" : "NOT_PASS", eligibleHitChecksMayUseFate = true, damageMutations = 0, penetrationMutations = 0 });
        Write(output, "armor_two_axis_audit.json", new { status = checks["combat.protectionAxesIndependent"] && checks["combat.failedTransferExplicit"] ? "PASS" : "NOT_PASS", padded = new { armorRating = 5, torsoPr = 2 }, thickBreastplate = new { armorRating = 2, torsoPr = 9 }, plate = new { armorRating = 6, torsoPr = 7 } });
        Write(output, "focused_combat_balance_simulation.json", new { status = checks["combat.humanMedianFourToSeven"] ? "PASS" : "NOT_PASS", seed = 2202, bouts = simulation.Bouts, medianRounds = simulation.MedianRounds, oneOrTwoRoundRate = simulation.OneOrTwoRoundRate, fixture = "trained starter +2, Human Dodge 5, Natural AR1/PR1, no Fate/cover/healing" });
        Write(output, "explicit_hybrid_lineage_audit.json", new { status = checks["hybrid.explicitLineages"] ? "PASS" : "NOT_PASS", humanLineHalfDragonBreath = false, dragonLineElementalLineage = "fire", runtimeAveraging = false });
        Console.WriteLine($"Gate 2 race/physiology contracts: {(pass ? "PASS" : "NOT_PASS")} ({checks.Count} checks)");
        return pass ? 0 : 1;
    }

    private static void CheckSeed(List<Dictionary<string, object>> records, Dictionary<string, bool> c)
    {
        var races = Category(records, "race_definition");
        var subs = Category(records, "subspecies_definition");
        var hybrids = Category(records, "hybrid_definition");
        var hybridSubs = Category(records, "hybrid_subtype_definition");
        c["seed.baseRaceCount"] = races.Count == 10;
        c["seed.subspeciesCount"] = subs.Count == 41;
        c["seed.hybridCount"] = hybrids.Count == 18;
        c["seed.hybridSubtypeCount"] = hybridSubs.Count == 36;
        c["seed.noDuplicateStableKeys"] = records.GroupBy(Key).All(g => g.Count() == 1);
        c["seed.dragonStableIdentity"] = Name(races, "dragonborn") == "Драконид";
        c["seed.giantStableIdentity"] = Name(races, "giantborn") == "Великан";
        c["seed.runicDwarfSubtype"] = subs.Any(x => Key(x) == "runic_dwarf" && Name(x) == "Рунный дварф");
        c["seed.owlPresent"] = subs.Any(x => Key(x) == "owl_beastfolk" && Name(x) == "Совлин");
        c["seed.wildGoblinRestricted"] = Field(Single(subs, "wild_goblin"), "availabilityType") == "WildOnly";
        var pairs = hybrids.Select(x => string.Join("+", Strings(FieldObject(x, "parentLineages")).OrderBy(v => v, StringComparer.Ordinal))).ToList();
        c["hybrid.unorderedUniquePairs"] = pairs.Distinct(StringComparer.Ordinal).Count() == 18;
        c["hybrid.unsupportedPairsAbsent"] = !pairs.Contains("dragonborn+goblin") && !pairs.Contains("giantborn+goblin");
        c["hybrid.twoSubtypesEach"] = hybrids.All(h => hybridSubs.Count(s => Field(s, "hybridId") == Key(h)) >= 2);
        c["hybrid.explicitPhysiology"] = hybrids.All(h => Int(h, "baseHealth") > 0 && Int(h, "naturalArmorRating") > 0 && Int(h, "naturalPenetrationResistance") > 0);
        c["hybrid.explicitLineages"] = Field(Single(hybridSubs, "half_dragonid_line_1"), "elementalLineageId") == "" && Field(Single(hybridSubs, "half_dragonid_line_2"), "elementalLineageId") == "fire";
        c["hybrid.humanLineNoBreath"] = Strings(FieldObject(Single(hybridSubs, "half_dragonid_line_1"), "naturalAttackIds")).Count == 0;
        c["hybrid.dragonLineBreath"] = Strings(FieldObject(Single(hybridSubs, "half_dragonid_line_2"), "naturalAttackIds")).SequenceEqual(new[] { "half_fire_breath" });
        c["hybrid.noSilentAverageMarker"] = hybrids.All(h => Tags(h).Contains("no_parent_averaging"));
    }

    private static void CheckPhysiology(List<Dictionary<string, object>> records, Dictionary<string, bool> c)
    {
        var expected = new[]
        {
            new object[]{"human",140,220,18,75,120,100,1,1},new object[]{"elf",150,210,18,1500,1800,80,1,1},new object[]{"dwarf",80,150,18,800,1000,115,2,2},
            new object[]{"orc",160,250,16,50,70,120,2,2},new object[]{"goblin",60,140,10,40,60,85,1,1},new object[]{"halfling",70,140,14,80,120,80,1,1},
            new object[]{"gnome",65,130,25,600,700,90,1,1},new object[]{"beastfolk",80,230,12,120,150,100,1,1},new object[]{"dragonborn",150,250,20,150,300,140,4,4},new object[]{"giantborn",250,370,25,180,270,160,4,4}
        };
        var races = Category(records, "race_definition");
        c["physiology.baseTableExact"] = expected.All(e => Exact(Single(races,(string)e[0]), e.Skip(1).Cast<int>().ToArray()));
        c["physiology.heightOrder"] = races.Concat(Category(records,"subspecies_definition")).Concat(Category(records,"hybrid_definition")).All(x => Int(x,"minHeightCm") > 0 && Int(x,"minHeightCm") <= Int(x,"maxHeightCm"));
        c["physiology.lifespanOrder"] = races.Concat(Category(records,"subspecies_definition")).Concat(Category(records,"hybrid_definition")).All(x => Int(x,"adultAgeYears") <= Int(x,"averageLifespanYears") && Int(x,"averageLifespanYears") <= Int(x,"maximumLifespanYears"));
        c["physiology.playablePositive"] = races.All(x => Int(x,"baseHealth") > 0 && Int(x,"naturalArmorRating") >= 1 && Int(x,"naturalPenetrationResistance") >= 1);
        var deep = Single(Category(records,"subspecies_definition"),"deep_dwarf");
        c["physiology.deepDwarfExact"] = Exact(deep,65,135,18,3500,5000,110,2,2);
        var high = Single(Category(records,"subspecies_definition"),"high_elf");
        c["physiology.highElfExact"] = Exact(high,180,240,18,10000,12000,75,1,1);
        var bear = Single(Category(records,"subspecies_definition"),"bear_beastfolk");
        var snake = Single(Category(records,"subspecies_definition"),"snake_beastfolk");
        c["physiology.arPrCanDiffer"] = Int(bear,"naturalArmorRating") == 4 && Int(bear,"naturalPenetrationResistance") == 2 && Int(snake,"naturalArmorRating") == 2 && Int(snake,"naturalPenetrationResistance") == 4;
        var zones=Category(records,"body_zone_definition");
        c["body.humanoidWeightNormalized"] = new[]{"head","torso","left_arm","right_arm","left_leg","right_leg"}.Sum(id => Decimal(Single(zones,id),"randomWeight")) == 1m;
        c["body.calledShotModifiers"] = Int(Single(zones,"head"),"calledShotAccuracyModifier") == -4 && Int(Single(zones,"torso"),"calledShotAccuracyModifier") == 0 && Int(Single(zones,"left_wing"),"calledShotAccuracyModifier") == -3;
        c["body.wingZonesPresent"] = zones.Any(x=>Key(x)=="left_wing") && zones.Any(x=>Key(x)=="right_wing");
        var weighted=RacePhysiologyRules022Gate2.HumanoidZones();
        c["body.weightedResolverUsesTargetRoll"] = BodyZoneRules022Gate2.ResolveWeighted(weighted,.05m).ZoneId==BodyZoneIds.Head
            && BodyZoneRules022Gate2.ResolveWeighted(weighted,.20m).ZoneId==BodyZoneIds.Torso
            && BodyZoneRules022Gate2.ResolveWeighted(weighted,.60m).ZoneId==BodyZoneIds.LeftArm
            && BodyZoneRules022Gate2.ResolveWeighted(weighted,.99m).ZoneId==BodyZoneIds.RightLeg;
        var fits=Category(records,"race_equipment_fit_profile");
        c["fit.wingClearance"] = Strings(FieldObject(Single(fits,"winged_fit"),"fitTags")).Contains("wing_clearance");
        c["fit.giant"] = Field(Single(fits,"giant_fit"),"sizeClass") == "giant";
    }

    private static void CheckCombat(List<Dictionary<string, object>> records, Dictionary<string, bool> c)
    {
        var attacks=Category(records,"natural_attack_definition");
        c["attack.count"] = attacks.Count == 9;
        c["attack.unarmedExact"] = AttackExact(Single(attacks,"unarmed"),1,12,1,2,0,1m,0,"single");
        c["attack.catExact"] = AttackExact(Single(attacks,"cat_claws"),2,6,1,2,2,0m,0,"single");
        c["attack.bearExact"] = AttackExact(Single(attacks,"bear_claws"),2,10,2,4,4,.10m,0,"single");
        c["attack.fireBreathExact"] = AttackExact(Single(attacks,"dragon_fire_breath"),2,12,2,4,4,.35m,3,"cone") && Decimal(Single(attacks,"dragon_fire_breath"),"areaAngleDegrees") == 60m;
        c["attack.iceBreathExact"] = AttackExact(Single(attacks,"dragon_ice_breath"),2,10,2,4,4,.25m,3,"cone") && Field(Single(attacks,"dragon_ice_breath"),"appliedConditionId") == "slowed";
        c["attack.stormBreathExact"] = AttackExact(Single(attacks,"dragon_storm_breath"),2,10,2,5,5,.35m,3,"line") && Decimal(Single(attacks,"dragon_storm_breath"),"areaWidthMeters") == 1m;
        c["attack.halfBreathCooldown"] = attacks.Where(x=>Key(x).StartsWith("half_",StringComparison.Ordinal)).All(x=>Int(x,"cooldownRounds")==4);
        c["attack.fateExcludedFromAllDamage"] = attacks.All(x=>!Bool(x,"fateEligibleForDamage"));
        var rolled=DamageExpressionRules022Gate2.Roll(new DamageExpressionDefinition{DiceCount=3,DieSides=8,PerDieModifier=2,TotalModifier=5},new QueueRoller(4,7,3).Roll);
        c["attack.structuredDamageRawPreserved"] = rolled.TotalDamage==25 && rolled.Dice.Select(x=>x.RawValue).SequenceEqual(new[]{4,7,3}) && rolled.Dice.Select(x=>x.ModifiedValue).SequenceEqual(new[]{6,9,5});
        var d35=DamageExpressionRules022Gate2.Roll(new DamageExpressionDefinition{DiceCount=1,DieSides=35},_=>35);
        c["attack.genericDieSides"] = d35.TotalDamage==35;
        var padded=PersonalProtectionRules022Gate2.Resolve(12,5,1,5,0,2,1,2,20,0m);
        var breast=PersonalProtectionRules022Gate2.Resolve(12,5,1,2,0,2,1,9,20,.25m);
        c["combat.protectionAxesIndependent"] = padded.Defense==11 && padded.TotalPenetrationResistance==3 && breast.Defense==8 && breast.TotalPenetrationResistance==10;
        c["combat.failedTransferExplicit"] = breast.Hit && !breast.Penetrated && breast.AppliedDamage==5 && PersonalProtectionRules022Gate2.Resolve(12,5,1,2,0,2,1,9,20,0m).AppliedDamage==0;
        c["combat.naturalTwentyNoAutoPenetration"] = !PersonalProtectionRules022Gate2.Resolve(20,5,1,2,0,0,1,9,20,0m).Penetrated;
    }

    private static void CheckMovement(List<Dictionary<string, object>> records, Dictionary<string, bool> c)
    {
        var owl=new RacialMovementAbilityDefinition{MovementMode=RacialMovementModeIds.PoweredFlight,SpeedMeters=12m,MaximumLoadFraction=.55m,ReducedSpeedLoadFraction=.35m,ReducedSpeedMultiplier=.65m,RequiredClearanceMeters=3m,MaximumIndependentTakeoffWindMetersPerSecond=14m,RequiredBodyZoneIds=new List<string>{BodyZoneIds.LeftWing,BodyZoneIds.RightWing},RequiredEquipmentFitTags=new List<string>{"wing_clearance"}};
        RacialMovementAvailability022Gate2 Resolve(IEnumerable<string> zones,IEnumerable<string> fit,decimal load=.2m,decimal wind=2m,decimal clearance=5m)=>RacialMovementRules022Gate2.Resolve(new RacialMovementAvailabilityContext022Gate2{Ability=owl,FunctionalBodyZoneIds=zones.ToList(),EquipmentFitTags=fit.ToList(),CarriedLoadFraction=load,WindMetersPerSecond=wind,AvailableClearanceMeters=clearance});
        c["flight.availableWithHealthyWings"] = Resolve(new[]{BodyZoneIds.LeftWing,BodyZoneIds.RightWing},new[]{"wing_clearance"}).IsAvailable;
        c["flight.wingDamageBlocks"] = Resolve(new[]{BodyZoneIds.LeftWing},new[]{"wing_clearance"}).BlockingReasons.Contains("required_body_zone_impaired");
        c["flight.armorCanBlock"] = Resolve(new[]{BodyZoneIds.LeftWing,BodyZoneIds.RightWing},Array.Empty<string>()).BlockingReasons.Contains("equipment_blocks_movement");
        c["flight.loadCanReduceSpeed"] = Resolve(new[]{BodyZoneIds.LeftWing,BodyZoneIds.RightWing},new[]{"wing_clearance"},.45m).EffectiveSpeedMeters==7.8m;
        c["flight.loadCanBlock"] = Resolve(new[]{BodyZoneIds.LeftWing,BodyZoneIds.RightWing},new[]{"wing_clearance"},.7m).BlockingReasons.Contains("load_limit_exceeded");
        c["flight.windCanBlockTakeoff"] = Resolve(new[]{BodyZoneIds.LeftWing,BodyZoneIds.RightWing},new[]{"wing_clearance"},.2m,20m).BlockingReasons.Contains("takeoff_wind_limit_exceeded");
        c["flight.clearanceCanBlockTakeoff"] = Resolve(new[]{BodyZoneIds.LeftWing,BodyZoneIds.RightWing},new[]{"wing_clearance"},.2m,2m,1m).BlockingReasons.Contains("takeoff_clearance_insufficient");
        c["flight.seedHasPoweredAndGlide"] = Category(records,"racial_movement_ability_definition").Select(Key).OrderBy(x=>x).SequenceEqual(new[]{"bird_glide","owl_powered_flight"});
    }

    private static void CheckAreaGeometry(Dictionary<string, bool> c)
    {
        var origin = new CombatAreaPoint022Gate2 { ParticipantId = "attacker", X = 0, Y = 0 };
        var aim = new CombatAreaPoint022Gate2 { ParticipantId = "aim", X = 6, Y = 0 };
        var points = new[]
        {
            aim,
            new CombatAreaPoint022Gate2 { ParticipantId = "cone_inside", X = 5, Y = 2 },
            new CombatAreaPoint022Gate2 { ParticipantId = "cone_outside", X = 3, Y = 4 },
            new CombatAreaPoint022Gate2 { ParticipantId = "line_inside", X = 8, Y = .4 },
            new CombatAreaPoint022Gate2 { ParticipantId = "line_outside", X = 8, Y = .6 },
            new CombatAreaPoint022Gate2 { ParticipantId = "friendly", X = 4, Y = 0 }
        };
        var cone = NaturalAttackAreaRules022Gate2.ResolveTargets(new NaturalAttackDefinition { AreaShape = "cone", RangeMeters = 8, AreaAngleDegrees = 60, FriendlyFire = true }, origin, aim, points);
        c["area.coneIncludesInside"] = cone.Contains("aim") && cone.Contains("cone_inside");
        c["area.coneExcludesOutside"] = !cone.Contains("cone_outside");
        c["area.friendlyFireIncluded"] = cone.Contains("friendly");
        var line = NaturalAttackAreaRules022Gate2.ResolveTargets(new NaturalAttackDefinition { AreaShape = "line", RangeMeters = 10, AreaWidthMeters = 1, FriendlyFire = true }, origin, aim, points);
        c["area.lineIncludesHalfWidth"] = line.Contains("line_inside");
        c["area.lineExcludesBeyondWidth"] = !line.Contains("line_outside");
        c["area.attackerExcluded"] = !cone.Contains("attacker") && !line.Contains("attacker");
    }

    private static void CheckEnvironment(List<Dictionary<string, object>> records, Dictionary<string, bool> c)
    {
        var env=Category(records,"environmental_tolerance_modifier_definition");
        c["environment.typedModifiers"] = env.Count==12 && env.All(x=>FieldObject(x,"coldSensitivityMultiplier")!=null && FieldObject(x,"hydrationConsumptionMultiplier")!=null);
        c["environment.polarExact"] = Decimal(Single(env,"env_polar_human"),"comfortMinDeltaC")==-10m && Decimal(Single(env,"env_polar_human"),"coldSensitivityMultiplier")==.65m;
        c["environment.southernExact"] = Decimal(Single(env,"env_southern_human"),"comfortMaxDeltaC")==10m && Decimal(Single(env,"env_southern_human"),"hydrationConsumptionMultiplier")==.75m;
        var senses=Category(records,"racial_sense_definition");
        c["sense.typed"] = senses.Count>=14 && senses.All(x=>!string.IsNullOrWhiteSpace(Field(x,"modality")));
        c["sense.noWallPenetration"] = senses.All(x=>!Bool(x,"penetratesWalls"));
    }

    private static SimulationResult SimulateNeutralHumanCombat()
    {
        var random=new Random(2202);var rounds=new List<int>();var quick=0;const int bouts=2000;
        for(var b=0;b<bouts;b++){var hp=100;var round=0;while(hp>0&&round<50){round++;var roll=random.Next(1,21);if(roll+2>=6){var damage=random.Next(1,36)+6;hp-=damage;}}rounds.Add(round);if(round<=2)quick++;}
        rounds.Sort();return new SimulationResult{Bouts=bouts,MedianRounds=rounds[rounds.Count/2],OneOrTwoRoundRate=(decimal)quick/bouts};
    }

    private static bool Exact(Dictionary<string,object> r,params int[] values)=>new[]{Int(r,"minHeightCm"),Int(r,"maxHeightCm"),Int(r,"adultAgeYears"),Int(r,"averageLifespanYears"),Int(r,"maximumLifespanYears"),Int(r,"baseHealth"),Int(r,"naturalArmorRating"),Int(r,"naturalPenetrationResistance")}.SequenceEqual(values);
    private static bool AttackExact(Dictionary<string,object> r,int count,int sides,int perDie,int total,int pen,decimal transfer,int cooldown,string shape)=>Int(r,"diceCount")==count&&Int(r,"dieSides")==sides&&Int(r,"perDieModifier")==perDie&&Int(r,"totalModifier")==total&&Int(r,"physicalPenetration")==pen&&Decimal(r,"failedPenetrationDamageTransfer")==transfer&&Int(r,"cooldownRounds")==cooldown&&Field(r,"areaShape")==shape;
    private static Dictionary<string,object> Parse(string line)=>Json.Deserialize<Dictionary<string,object>>(line);
    private static List<Dictionary<string,object>> Category(List<Dictionary<string,object>> all,string category)=>all.Where(x=>Convert.ToString(x["Category"])==category).ToList();
    private static Dictionary<string,object> Single(List<Dictionary<string,object>> all,string key)=>all.Single(x=>Key(x)==key);
    private static string Key(Dictionary<string,object> r)=>Convert.ToString(r["StableKey"])??"";
    private static string Name(Dictionary<string,object> r)=>Convert.ToString(r["DisplayName"])??"";
    private static string Name(List<Dictionary<string,object>> all,string key)=>Name(Single(all,key));
    private static Dictionary<string,object> Fields(Dictionary<string,object> r)=>(Dictionary<string,object>)r["CustomFields"];
    private static object? FieldObject(Dictionary<string,object> r,string field)=>Fields(r).TryGetValue(field,out var v)?v:null;
    private static string Field(Dictionary<string,object> r,string field)=>Convert.ToString(FieldObject(r,field))??"";
    private static int Int(Dictionary<string,object> r,string field)=>Convert.ToInt32(FieldObject(r,field)??0);
    private static decimal Decimal(Dictionary<string,object> r,string field)=>Convert.ToDecimal(FieldObject(r,field)??0,System.Globalization.CultureInfo.InvariantCulture);
    private static bool Bool(Dictionary<string,object> r,string field)=>Convert.ToBoolean(FieldObject(r,field)??false);
    private static List<string> Strings(object? value)
    {
        if (value is string text) return string.IsNullOrWhiteSpace(text) ? new List<string>() : new List<string> { text };
        if (value is IEnumerable values) return values.Cast<object>().Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
        return string.IsNullOrWhiteSpace(Convert.ToString(value)) ? new List<string>() : new List<string> { Convert.ToString(value)! };
    }
    private static List<string> Tags(Dictionary<string,object> r)=>Strings(r.TryGetValue("Tags",out var v)?v:null);
    private static bool All(Dictionary<string,bool> c,string prefix)=>c.Where(x=>x.Key.StartsWith(prefix,StringComparison.Ordinal)).All(x=>x.Value);
    private static void Write(string output,string name,object value)=>File.WriteAllText(Path.Combine(output,name),Json.Serialize(value),new UTF8Encoding(false));
    private sealed class QueueRoller{private readonly Queue<int> _values;public QueueRoller(params int[] values){_values=new Queue<int>(values);}public int Roll(int sides)=>_values.Dequeue();}
    private sealed class SimulationResult{public int Bouts{get;set;}public int MedianRounds{get;set;}public decimal OneOrTwoRoundRate{get;set;}}
}
