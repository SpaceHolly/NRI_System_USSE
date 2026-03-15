using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private readonly object _definitionsSync = new object();
    private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
    private bool _definitionsLoaded;
    private string _definitionVersion = "1.0.0";
    private Dictionary<string, ClassNodeDefinition> _nodesById = new Dictionary<string, ClassNodeDefinition>();
    private Dictionary<string, ClassDirectionDefinition> _directionsById = new Dictionary<string, ClassDirectionDefinition>();
    private Dictionary<string, SkillDefinitionRecord> _skillsById = new Dictionary<string, SkillDefinitionRecord>();

    public ResponseEnvelope DefinitionsClassesGet(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureDefinitionsLoaded(false);
        var items = _directionsById.Values.Select(d => new Dictionary<string, object>
        {
            { "directionId", d.DirectionId },
            { "name", d.Name },
            { "branches", d.Branches.Select(b => new Dictionary<string, object>
                {
                    { "branchId", b.BranchId },
                    { "name", b.Name },
                    { "nodeIds", b.NodeIds.Cast<object>().ToArray() }
                }).Cast<object>().ToArray() }
        }).Cast<object>().ToArray();

        var nodes = _nodesById.Values.Select(NodePayload).Cast<object>().ToArray();
        return Ok("Class definitions loaded.", new Dictionary<string, object> { { "directions", items }, { "nodes", nodes }, { "version", _definitionVersion } });
    }

    public ResponseEnvelope DefinitionsSkillsGet(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureDefinitionsLoaded(false);
        return Ok("Skill definitions loaded.", new Dictionary<string, object>
        {
            { "items", _skillsById.Values.Select(SkillDefinitionPayload).Cast<object>().ToArray() },
            { "version", _definitionVersion }
        });
    }

    public ResponseEnvelope DefinitionsVersionGet(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureDefinitionsLoaded(false);
        return Ok("Definition version loaded.", new Dictionary<string, object> { { "version", _definitionVersion } });
    }

    public ResponseEnvelope DefinitionsReload(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(true);
        WriteAudit("definitions", actor.Id, "reload", "class-skill-definitions");
        _logger.Admin($"Definitions reloaded by {actor.Login}. version={_definitionVersion}");
        return Ok("Definitions reloaded.", new Dictionary<string, object> { { "version", _definitionVersion } });
    }

    public ResponseEnvelope ClassTreeGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        return Ok("Class tree state loaded.", CharacterProgressPayload(c, snapshot));
    }

    public ResponseEnvelope ClassTreeNodeGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        EnsureDefinitionsLoaded(false);
        if (!_nodesById.ContainsKey(nodeId)) throw new KeyNotFoundException("Node not found.");
        var snapshot = RecalculateProgress(c);
        var state = FindNodeState(c, nodeId);
        var reasons = EvaluateNodeAvailability(c, _nodesById[nodeId], snapshot);
        return Ok("Node loaded.", new Dictionary<string, object>
        {
            { "node", NodePayload(_nodesById[nodeId]) },
            { "acquired", state != null },
            { "available", reasons.Count == 0 },
            { "reasons", reasons.Cast<object>().ToArray() }
        });
    }

    public ResponseEnvelope ClassTreeAvailableGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        var items = _nodesById.Values.Select(n =>
        {
            var acquired = FindNodeState(c, n.NodeId) != null;
            var reasons = acquired ? new List<string>() : EvaluateNodeAvailability(c, n, snapshot);
            return new Dictionary<string, object>
            {
                { "nodeId", n.NodeId },
                { "name", n.Name },
                { "directionId", n.DirectionId },
                { "branchId", n.BranchId },
                { "acquired", acquired },
                { "available", !acquired && reasons.Count == 0 },
                { "reasons", reasons.Cast<object>().ToArray() }
            };
        }).Cast<object>().ToArray();

        return Ok("Available nodes loaded.", new Dictionary<string, object> { { "items", items }, { "version", _definitionVersion } });
    }

    public ResponseEnvelope ClassTreeAcquireNode(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        EnsureDefinitionsLoaded(false);
        var node = _nodesById.ContainsKey(nodeId) ? _nodesById[nodeId] : throw new KeyNotFoundException("Node not found.");
        if (FindNodeState(c, nodeId) != null) throw new InvalidOperationException("Node already acquired.");

        var snapshot = RecalculateProgress(c);
        var reasons = EvaluateNodeAvailability(c, node, snapshot);
        if (reasons.Count > 0) throw new InvalidOperationException("Node unavailable: " + string.Join(", ", reasons));

        var dir = c.ClassDirections.FirstOrDefault(x => x.DirectionId == node.DirectionId);
        if (dir == null)
        {
            dir = new CharacterClassDirectionState { DirectionId = node.DirectionId, SelectedBranchId = node.BranchId };
            c.ClassDirections.Add(dir);
        }

        dir.SelectedBranchId = string.IsNullOrWhiteSpace(dir.SelectedBranchId) ? node.BranchId : dir.SelectedBranchId;
        dir.AcquiredNodes.Add(new CharacterClassNodeState { NodeId = node.NodeId, AcquiredAtUtc = DateTime.UtcNow });

        snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("classTree", actor.Id, "acquireNode", c.Id + ":" + nodeId);
        return Ok("Node acquired.", CharacterProgressPayload(c, snapshot));
    }

    public ResponseEnvelope ClassTreeRecalculate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("classTree", actor.Id, "recalculate", c.Id);
        return Ok("Character class progress recalculated.", CharacterProgressPayload(c, snapshot));
    }

    public ResponseEnvelope SkillsList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        return Ok("Skills loaded.", new Dictionary<string, object> { { "items", SkillStatePayload(snapshot).ToArray() }, { "version", _definitionVersion } });
    }

    public ResponseEnvelope SkillsAvailable(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        var available = SkillStatePayload(snapshot).Where(x => (bool)x["available"] && !(bool)x["acquired"]).Cast<object>().ToArray();
        return Ok("Available skills loaded.", new Dictionary<string, object> { { "items", available } });
    }

    public ResponseEnvelope SkillsGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var skillId = RequireLength(PayloadReader.GetString(context.Request.Payload, "skillId"), 1, 128, "skillId");
        var snapshot = RecalculateProgress(c);
        var row = SkillStatePayload(snapshot).FirstOrDefault(x => Convert.ToString(x["skillId"]) == skillId);
        if (row == null) throw new KeyNotFoundException("Skill not found.");
        return Ok("Skill loaded.", row);
    }

    public ResponseEnvelope SkillsAcquire(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var skillId = RequireLength(PayloadReader.GetString(context.Request.Payload, "skillId"), 1, 128, "skillId");
        var snapshot = RecalculateProgress(c);
        var row = snapshot.Skills.FirstOrDefault(s => s.SkillId == skillId) ?? throw new KeyNotFoundException("Skill not found.");
        if (row.Acquired) throw new InvalidOperationException("Skill already acquired.");
        if (!row.Available) throw new InvalidOperationException("Skill unavailable: " + row.UnavailableReason);
        row.Acquired = true;
        var existing = c.CharacterSkillStates.FirstOrDefault(s => s.SkillId == skillId);
        if (existing == null) c.CharacterSkillStates.Add(row);
        else existing.Acquired = true;
        snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("skills", actor.Id, "acquire", c.Id + ":" + skillId);
        return Ok("Skill acquired.", new Dictionary<string, object> { { "items", SkillStatePayload(snapshot).ToArray() } });
    }

    public ResponseEnvelope AdminClassTreeSetState(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var directionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "directionId"), 1, 128, "directionId");
        var branchId = PayloadReader.GetString(context.Request.Payload, "branchId") ?? string.Empty;
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");

        var dir = c.ClassDirections.FirstOrDefault(x => x.DirectionId == directionId);
        if (dir == null)
        {
            dir = new CharacterClassDirectionState { DirectionId = directionId };
            c.ClassDirections.Add(dir);
        }

        if (!string.IsNullOrWhiteSpace(branchId)) dir.SelectedBranchId = branchId;
        if (dir.AcquiredNodes.All(n => n.NodeId != nodeId)) dir.AcquiredNodes.Add(new CharacterClassNodeState { NodeId = nodeId, AcquiredAtUtc = DateTime.UtcNow });

        var snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("admin", actor.Id, "classTree.setState", c.Id + ":" + nodeId);
        return Ok("Class state updated.", CharacterProgressPayload(c, snapshot));
    }

    public ResponseEnvelope AdminSkillsSetState(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var skillId = RequireLength(PayloadReader.GetString(context.Request.Payload, "skillId"), 1, 128, "skillId");
        var acquired = PayloadReader.GetBool(context.Request.Payload, "acquired") ?? true;

        var row = c.CharacterSkillStates.FirstOrDefault(s => s.SkillId == skillId);
        if (row == null)
        {
            row = new CharacterSkillState { SkillId = skillId, Acquired = acquired };
            c.CharacterSkillStates.Add(row);
        }
        else row.Acquired = acquired;

        var snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("admin", actor.Id, "skills.setState", c.Id + ":" + skillId);
        return Ok("Skill state updated.", new Dictionary<string, object> { { "items", SkillStatePayload(snapshot).ToArray() } });
    }

    public ResponseEnvelope AdminCharacterProgressRecalculate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("admin", actor.Id, "character.progress.recalculate", c.Id);
        return Ok("Character progress recalculated.", CharacterProgressPayload(c, snapshot));
    }

    private Character ResolveCharacterForClassSkill(CommandContext context, UserAccount actor)
    {
        var requestedId = PayloadReader.GetString(context.Request.Payload, "characterId");
        if (!string.IsNullOrWhiteSpace(requestedId))
        {
            var c = GetCharacter(RequireLength(requestedId, 8, 128, "characterId"));
            if (actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin)) return c;
            if (c.OwnerUserId != actor.Id) throw new UnauthorizedAccessException("Character unavailable.");
            return c;
        }

        var own = _repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.OwnerUserId, actor.Id)).FirstOrDefault();
        if (own != null) return own;
        throw new InvalidOperationException("No character selected.");
    }

    private void EnsureDefinitionsLoaded(bool force)
    {
        lock (_definitionsSync)
        {
            if (_definitionsLoaded && !force) return;
            LoadDefinitions();
            _definitionsLoaded = true;
        }
    }

    private void LoadDefinitions()
    {
        var dbNodes = _repositories.ClassTrees.Find(FilterDefinition<ClassTreeDefinition>.Empty).ToList();
        var dbSkills = _repositories.SkillDefinitions.Find(FilterDefinition<SkillDefinitionRecord>.Empty).ToList();
        if (dbNodes.Count > 0 && dbSkills.Count > 0)
        {
            ApplyDefinitions(dbNodes, dbSkills, "mongo");
            return;
        }

        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var classesPath = Path.Combine(basePath, "definitions", "classes.json");
        var skillsPath = Path.Combine(basePath, "definitions", "skills.json");

        if (!File.Exists(classesPath) || !File.Exists(skillsPath))
        {
            var seeded = SeedDefaultDefinitions();
            ApplyDefinitions(seeded.Item1, seeded.Item2, "seeded");
            return;
        }

        var classItems = _serializer.Deserialize<List<ClassTreeDefinition>>(File.ReadAllText(classesPath)) ?? new List<ClassTreeDefinition>();
        var skillItems = _serializer.Deserialize<List<SkillDefinitionRecord>>(File.ReadAllText(skillsPath)) ?? new List<SkillDefinitionRecord>();
        ApplyDefinitions(classItems, skillItems, "json");
    }

    private Tuple<List<ClassTreeDefinition>, List<SkillDefinitionRecord>> SeedDefaultDefinitions()
    {
        var directions = new[]
        {
            new ClassDirectionDefinition { DirectionId = "defender", Name = "Защитник", Branches = new List<ClassBranchDefinition>{ new ClassBranchDefinition{ BranchId="defender_core", Name="Core", NodeIds = new List<string>{"defender_guard"}}}},
            new ClassDirectionDefinition { DirectionId = "vanguard", Name = "Передовой", Branches = new List<ClassBranchDefinition>{ new ClassBranchDefinition{ BranchId="vanguard_core", Name="Core", NodeIds = new List<string>{"vanguard_breach"}}}},
            new ClassDirectionDefinition { DirectionId = "ranger", Name = "Рейнджер", Branches = new List<ClassBranchDefinition>{ new ClassBranchDefinition{ BranchId="ranger_core", Name="Core", NodeIds = new List<string>{"ranger_hunt"}}}},
            new ClassDirectionDefinition { DirectionId = "samurai", Name = "Самурай", Branches = new List<ClassBranchDefinition>{ new ClassBranchDefinition{ BranchId="samurai_core", Name="Core", NodeIds = new List<string>{"samurai_focus"}}}},
            new ClassDirectionDefinition { DirectionId = "mage", Name = "Маг", Branches = new List<ClassBranchDefinition>{ new ClassBranchDefinition{ BranchId="mage_core", Name="Core", NodeIds = new List<string>{"mage_channel"}}}},
            new ClassDirectionDefinition { DirectionId = "inventor", Name = "Изобретатель", Branches = new List<ClassBranchDefinition>{ new ClassBranchDefinition{ BranchId="inventor_core", Name="Core", NodeIds = new List<string>{"inventor_gear"}}}}
        };

        var nodes = new List<ClassNodeDefinition>
        {
            new ClassNodeDefinition{ NodeId="defender_guard", DirectionId="defender", BranchId="defender_core", Name="Стойка щита", Description="Базовый защитный узел", UnlockSkillIds = new List<string>{"skill_guard_stance"}, StatBonuses = new List<StatBonusDefinition>{ new StatBonusDefinition{ Stat="PhysicalArmor", Bonus=2 } } },
            new ClassNodeDefinition{ NodeId="vanguard_breach", DirectionId="vanguard", BranchId="vanguard_core", Name="Пролом", Description="Базовый штурмовой узел", UnlockSkillIds = new List<string>{"skill_breach"}, StatBonuses = new List<StatBonusDefinition>{ new StatBonusDefinition{ Stat="Strength", Bonus=1 } } },
            new ClassNodeDefinition{ NodeId="ranger_hunt", DirectionId="ranger", BranchId="ranger_core", Name="Меткий выстрел", Description="Базовый рейнджерский узел", UnlockSkillIds = new List<string>{"skill_hunt_mark"}, StatBonuses = new List<StatBonusDefinition>{ new StatBonusDefinition{ Stat="Dexterity", Bonus=1 } } },
            new ClassNodeDefinition{ NodeId="samurai_focus", DirectionId="samurai", BranchId="samurai_core", Name="Фокус клинка", Description="Базовый самурайский узел", UnlockSkillIds = new List<string>{"skill_blade_focus"}, StatBonuses = new List<StatBonusDefinition>{ new StatBonusDefinition{ Stat="Wisdom", Bonus=1 } } },
            new ClassNodeDefinition{ NodeId="mage_channel", DirectionId="mage", BranchId="mage_core", Name="Канал маны", Description="Базовый магический узел", UnlockSkillIds = new List<string>{"skill_mana_channel"}, StatBonuses = new List<StatBonusDefinition>{ new StatBonusDefinition{ Stat="Intellect", Bonus=2 } } },
            new ClassNodeDefinition{ NodeId="inventor_gear", DirectionId="inventor", BranchId="inventor_core", Name="Техномастер", Description="Базовый инженерный узел", UnlockSkillIds = new List<string>{"skill_quick_gadget"}, StatBonuses = new List<StatBonusDefinition>{ new StatBonusDefinition{ Stat="Intellect", Bonus=1 } } }
        };

        var trees = directions.Select(d => new ClassTreeDefinition { DirectionId = d.DirectionId, Nodes = nodes.Where(n => n.DirectionId == d.DirectionId).ToList() }).ToList();
        var skills = new List<SkillDefinitionRecord>
        {
            new SkillDefinitionRecord { SkillId = "skill_guard_stance", Name = "Стойка щита", Description = "Пассивно повышает защиту", Type = SkillType.Passive, UsageDescription = "Постоянно", Activation = new SkillActivationCondition{ Description="Пассивен", RequiresApprovalOnUse=false } },
            new SkillDefinitionRecord { SkillId = "skill_breach", Name = "Пролом", Description = "Активируемый штурм", Type = SkillType.Activatable, UsageDescription = "Заявка на применение", Activation = new SkillActivationCondition{ Description="Требует одобрения", RequiresApprovalOnUse=true } },
            new SkillDefinitionRecord { SkillId = "skill_hunt_mark", Name = "Метка охотника", Description = "Активируемый дебаф", Type = SkillType.Activatable, UsageDescription = "Заявка на применение", Activation = new SkillActivationCondition{ Description="Требует одобрения", RequiresApprovalOnUse=true } },
            new SkillDefinitionRecord { SkillId = "skill_blade_focus", Name = "Фокус клинка", Description = "Пассивная концентрация", Type = SkillType.Passive, UsageDescription = "Постоянно", Activation = new SkillActivationCondition{ Description="Пассивен", RequiresApprovalOnUse=false } },
            new SkillDefinitionRecord { SkillId = "skill_mana_channel", Name = "Канал маны", Description = "Пассивное усиление магии", Type = SkillType.Passive, UsageDescription = "Постоянно", Activation = new SkillActivationCondition{ Description="Пассивен", RequiresApprovalOnUse=false } },
            new SkillDefinitionRecord { SkillId = "skill_quick_gadget", Name = "Быстрый гаджет", Description = "Активируемый инженерный трюк", Type = SkillType.Activatable, UsageDescription = "Заявка на применение", Activation = new SkillActivationCondition{ Description="Требует одобрения", RequiresApprovalOnUse=true } }
        };

        return Tuple.Create(trees, skills);
    }

    private void ApplyDefinitions(List<ClassTreeDefinition> classes, List<SkillDefinitionRecord> skills, string source)
    {
        ValidateDefinitions(classes, skills);
        _nodesById = classes.SelectMany(x => x.Nodes).ToDictionary(x => x.NodeId, x => x);
        _skillsById = skills.ToDictionary(x => x.SkillId, x => x);

        _directionsById = classes.ToDictionary(x => x.DirectionId, x =>
        {
            var grouped = x.Nodes.GroupBy(n => n.BranchId).Select(g => new ClassBranchDefinition { BranchId = g.Key, Name = g.Key, NodeIds = g.Select(n => n.NodeId).ToList() }).ToList();
            return new ClassDirectionDefinition { DirectionId = x.DirectionId, Name = x.DirectionId, Branches = grouped };
        });

        _definitionVersion = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        UpsertDefinitionVersion("classTree", _definitionVersion, source);
        UpsertDefinitionVersion("skills", _definitionVersion, source);
    }

    private void ValidateDefinitions(List<ClassTreeDefinition> classes, List<SkillDefinitionRecord> skills)
    {
        if (classes.Count == 0) throw new InvalidOperationException("No class definitions.");
        if (skills.Count == 0) throw new InvalidOperationException("No skill definitions.");

        var nodeIds = new HashSet<string>();
        foreach (var node in classes.SelectMany(x => x.Nodes))
        {
            if (!nodeIds.Add(node.NodeId)) throw new InvalidOperationException("Duplicate nodeId: " + node.NodeId);
        }

        var skillIds = new HashSet<string>();
        foreach (var skill in skills)
        {
            if (!skillIds.Add(skill.SkillId)) throw new InvalidOperationException("Duplicate skillId: " + skill.SkillId);
        }

        foreach (var node in classes.SelectMany(x => x.Nodes))
        {
            foreach (var next in node.NextNodeIds)
            {
                if (!nodeIds.Contains(next)) throw new InvalidOperationException("Broken nextNode reference: " + node.NodeId + " -> " + next);
            }
            foreach (var sid in node.UnlockSkillIds)
            {
                if (!skillIds.Contains(sid)) throw new InvalidOperationException("Broken skill reference: " + sid);
            }
        }
    }

    private CharacterProgressSnapshot RecalculateProgress(Character c)
    {
        EnsureDefinitionsLoaded(false);
        EnsureProgressInitialized(c);

        var acquiredNodeIds = c.ClassDirections.SelectMany(d => d.AcquiredNodes.Select(n => n.NodeId)).Distinct().ToHashSet();
        var bonuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var effects = new List<CharacterPassiveEffectState>();
        var unlockState = new CharacterUnlockState();
        var unlockedSkillIds = new HashSet<string>();

        foreach (var nodeId in acquiredNodeIds)
        {
            if (!_nodesById.ContainsKey(nodeId)) continue;
            var node = _nodesById[nodeId];
            foreach (var bonus in node.StatBonuses)
            {
                bonuses[bonus.Stat] = bonuses.ContainsKey(bonus.Stat) ? bonuses[bonus.Stat] + bonus.Bonus : bonus.Bonus;
            }
            foreach (var fx in node.PassiveEffects)
            {
                effects.Add(new CharacterPassiveEffectState { EffectId = fx.EffectId, Description = fx.Description });
            }
            foreach (var u in node.EquipmentUnlocks) if (!unlockState.EquipmentUnlocks.Contains(u.UnlockCode)) unlockState.EquipmentUnlocks.Add(u.UnlockCode);
            foreach (var u in node.AbilityUnlocks) if (!unlockState.AbilityUnlocks.Contains(u.UnlockCode)) unlockState.AbilityUnlocks.Add(u.UnlockCode);
            foreach (var skill in node.UnlockSkillIds) unlockedSkillIds.Add(skill);
        }

        var snapshot = new CharacterProgressSnapshot
        {
            CharacterId = c.Id,
            Directions = c.ClassDirections,
            TotalStatBonuses = bonuses.Select(x => new StatBonusDefinition { Stat = x.Key, Bonus = x.Value }).ToList(),
            PassiveEffects = effects,
            Unlocks = unlockState,
            DefinitionVersion = _definitionVersion
        };

        var skillStates = new List<CharacterSkillState>();
        foreach (var skill in _skillsById.Values)
        {
            var existing = c.CharacterSkillStates.FirstOrDefault(x => x.SkillId == skill.SkillId);
            var reasons = EvaluateSkillAvailability(c, skill, unlockedSkillIds);
            skillStates.Add(new CharacterSkillState
            {
                SkillId = skill.SkillId,
                Acquired = existing != null && existing.Acquired,
                Available = reasons.Count == 0,
                UnavailableReason = reasons.Count == 0 ? string.Empty : string.Join("; ", reasons)
            });
        }

        c.CharacterSkillStates = skillStates;
        snapshot.Skills = skillStates;
        c.ClassSkillSnapshot = snapshot;
        c.ClassSkillDefinitionVersion = _definitionVersion;
        return snapshot;
    }

    private void EnsureProgressInitialized(Character c)
    {
        if (c.ClassDirections == null) c.ClassDirections = new List<CharacterClassDirectionState>();
        if (c.CharacterSkillStates == null) c.CharacterSkillStates = new List<CharacterSkillState>();
        if (c.ClassSkillSnapshot == null)
        {
            c.ClassSkillSnapshot = new CharacterProgressSnapshot { CharacterId = c.Id, DefinitionVersion = _definitionVersion };
        }
    }

    private List<string> EvaluateNodeAvailability(Character c, ClassNodeDefinition node, CharacterProgressSnapshot snapshot)
    {
        var reasons = new List<string>();
        var state = c.ClassDirections.FirstOrDefault(d => d.DirectionId == node.DirectionId);
        if (state != null && !string.IsNullOrWhiteSpace(state.SelectedBranchId) && state.SelectedBranchId != node.BranchId)
        {
            reasons.Add("В направлении можно выбрать только одну ветку");
        }

        if (node.Requirements != null)
        {
            foreach (var req in node.Requirements)
            {
                if (req.RequirementType.Equals("node", StringComparison.OrdinalIgnoreCase))
                {
                    var has = c.ClassDirections.SelectMany(d => d.AcquiredNodes).Any(n => n.NodeId == req.Key);
                    if (!has) reasons.Add("Требуется узел: " + req.Key);
                }
                else if (req.RequirementType.Equals("stat", StringComparison.OrdinalIgnoreCase))
                {
                    var val = GetStatValue(c.Stats, req.Key);
                    var threshold = 0;
                    int.TryParse(req.Value, out threshold);
                    if (val < threshold) reasons.Add("Недостаточный параметр " + req.Key + ": требуется " + threshold);
                }
            }
        }

        var anyAcquiredInDirection = c.ClassDirections.Where(d => d.DirectionId == node.DirectionId).SelectMany(d => d.AcquiredNodes).Any();
        if (!anyAcquiredInDirection)
        {
            return reasons;
        }

        var hasParent = _nodesById.Values.Any(n => n.NextNodeIds.Contains(node.NodeId) && c.ClassDirections.SelectMany(d => d.AcquiredNodes).Any(a => a.NodeId == n.NodeId));
        if (!hasParent && _nodesById.Values.Any(n => n.NextNodeIds.Contains(node.NodeId))) reasons.Add("Нет доступа по графу прогрессии");
        return reasons;
    }

    private List<string> EvaluateSkillAvailability(Character c, SkillDefinitionRecord skill, HashSet<string> unlockedSkillIds)
    {
        var reasons = new List<string>();
        if (!unlockedSkillIds.Contains(skill.SkillId)) reasons.Add("Навык не открыт узлом класса");
        foreach (var req in skill.Requirements)
        {
            if (req.RequirementType.Equals("node", StringComparison.OrdinalIgnoreCase))
            {
                var has = c.ClassDirections.SelectMany(d => d.AcquiredNodes).Any(n => n.NodeId == req.Key);
                if (!has) reasons.Add("Требуется узел: " + req.Key);
            }
            if (req.RequirementType.Equals("skill", StringComparison.OrdinalIgnoreCase))
            {
                var has = c.CharacterSkillStates.Any(s => s.SkillId == req.Key && s.Acquired);
                if (!has) reasons.Add("Требуется навык: " + req.Key);
            }
        }
        return reasons;
    }

    private CharacterClassNodeState FindNodeState(Character c, string nodeId)
    {
        return c.ClassDirections.SelectMany(d => d.AcquiredNodes).FirstOrDefault(n => n.NodeId == nodeId);
    }

    private Dictionary<string, object> CharacterProgressPayload(Character c, CharacterProgressSnapshot snapshot)
    {
        return new Dictionary<string, object>
        {
            { "characterId", c.Id },
            { "definitionVersion", snapshot.DefinitionVersion },
            { "directions", snapshot.Directions.Select(d => new Dictionary<string, object>
                {
                    { "directionId", d.DirectionId },
                    { "selectedBranchId", d.SelectedBranchId ?? string.Empty },
                    { "acquiredNodes", d.AcquiredNodes.Select(n => new Dictionary<string, object>{{"nodeId", n.NodeId},{"acquiredAt", n.AcquiredAtUtc}}).Cast<object>().ToArray() }
                }).Cast<object>().ToArray() },
            { "statBonuses", snapshot.TotalStatBonuses.Select(b => new Dictionary<string, object>{{"stat", b.Stat},{"bonus", b.Bonus}}).Cast<object>().ToArray() },
            { "passiveEffects", snapshot.PassiveEffects.Select(x => new Dictionary<string, object>{{"effectId",x.EffectId},{"description",x.Description}}).Cast<object>().ToArray() },
            { "unlocks", new Dictionary<string, object>{{"equipment", snapshot.Unlocks.EquipmentUnlocks.Cast<object>().ToArray()},{"ability", snapshot.Unlocks.AbilityUnlocks.Cast<object>().ToArray()}} },
            { "skills", SkillStatePayload(snapshot).Cast<object>().ToArray() }
        };
    }

    private List<Dictionary<string, object>> SkillStatePayload(CharacterProgressSnapshot snapshot)
    {
        return snapshot.Skills.Select(s =>
        {
            _skillsById.TryGetValue(s.SkillId, out var def);
            return new Dictionary<string, object>
            {
                { "skillId", s.SkillId },
                { "name", def != null ? def.Name : s.SkillId },
                { "description", def != null ? def.Description : string.Empty },
                { "type", def != null ? def.Type.ToString() : string.Empty },
                { "available", s.Available },
                { "acquired", s.Acquired },
                { "reason", s.UnavailableReason },
                { "activationCondition", def != null ? def.Activation.Description : string.Empty },
                { "usage", def != null ? def.UsageDescription : string.Empty },
                { "requiresApprovalOnUse", def != null && def.Activation.RequiresApprovalOnUse },
                { "requirements", def != null ? def.Requirements.Select(r => new Dictionary<string, object>{{"type",r.RequirementType},{"key",r.Key},{"value",r.Value}}).Cast<object>().ToArray() : new object[0] }
            };
        }).ToList();
    }

    private Dictionary<string, object> SkillDefinitionPayload(SkillDefinitionRecord def)
    {
        return new Dictionary<string, object>
        {
            { "skillId", def.SkillId },
            { "name", def.Name },
            { "description", def.Description },
            { "type", def.Type.ToString() },
            { "activationCondition", def.Activation.Description },
            { "requiresApprovalOnUse", def.Activation.RequiresApprovalOnUse },
            { "usage", def.UsageDescription },
            { "tags", def.Tags.Cast<object>().ToArray() },
            { "requirements", def.Requirements.Select(r => new Dictionary<string, object>{{"type",r.RequirementType},{"key",r.Key},{"value",r.Value}}).Cast<object>().ToArray() }
        };
    }

    private Dictionary<string, object> NodePayload(ClassNodeDefinition n)
    {
        return new Dictionary<string, object>
        {
            { "nodeId", n.NodeId },
            { "directionId", n.DirectionId },
            { "branchId", n.BranchId },
            { "name", n.Name },
            { "description", n.Description },
            { "nextNodeIds", n.NextNodeIds.Cast<object>().ToArray() },
            { "unlockSkillIds", n.UnlockSkillIds.Cast<object>().ToArray() },
            { "requirements", n.Requirements.Select(r => new Dictionary<string, object>{{"type",r.RequirementType},{"key",r.Key},{"value",r.Value}}).Cast<object>().ToArray() },
            { "statBonuses", n.StatBonuses.Select(s => new Dictionary<string, object>{{"stat",s.Stat},{"bonus",s.Bonus}}).Cast<object>().ToArray() },
            { "passiveEffects", n.PassiveEffects.Select(p => new Dictionary<string, object>{{"effectId",p.EffectId},{"description",p.Description}}).Cast<object>().ToArray() }
        };
    }

    private int GetStatValue(CharacterStats stats, string key)
    {
        if (key.Equals("Health", StringComparison.OrdinalIgnoreCase)) return stats.Health;
        if (key.Equals("PhysicalArmor", StringComparison.OrdinalIgnoreCase)) return stats.PhysicalArmor;
        if (key.Equals("MagicalArmor", StringComparison.OrdinalIgnoreCase)) return stats.MagicalArmor;
        if (key.Equals("Morale", StringComparison.OrdinalIgnoreCase)) return stats.Morale;
        if (key.Equals("Strength", StringComparison.OrdinalIgnoreCase)) return stats.Strength;
        if (key.Equals("Dexterity", StringComparison.OrdinalIgnoreCase)) return stats.Dexterity;
        if (key.Equals("Endurance", StringComparison.OrdinalIgnoreCase)) return stats.Endurance;
        if (key.Equals("Wisdom", StringComparison.OrdinalIgnoreCase)) return stats.Wisdom;
        if (key.Equals("Intellect", StringComparison.OrdinalIgnoreCase)) return stats.Intellect;
        if (key.Equals("Charisma", StringComparison.OrdinalIgnoreCase)) return stats.Charisma;
        return 0;
    }

    private void UpsertDefinitionVersion(string contentName, string version, string source)
    {
        var existing = _repositories.DefinitionVersions.Find(Builders<DefinitionVersion>.Filter.Eq(x => x.ContentName, contentName)).FirstOrDefault();
        if (existing == null)
        {
            _repositories.DefinitionVersions.Insert(new DefinitionVersion { ContentName = contentName, Version = version, Source = source, LoadedAtUtc = DateTime.UtcNow });
            return;
        }

        existing.Version = version;
        existing.Source = source;
        existing.LoadedAtUtc = DateTime.UtcNow;
        _repositories.DefinitionVersions.Replace(existing);
    }
}
