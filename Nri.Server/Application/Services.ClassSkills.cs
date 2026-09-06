using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private readonly object _definitionsSync = new object();
    private bool _definitionsLoaded;
    private string _definitionVersion = "1.0.0";
    private Dictionary<string, ClassNodeDefinition> _nodesById = new Dictionary<string, ClassNodeDefinition>();
    private Dictionary<string, ClassDirectionDefinition> _directionsById = new Dictionary<string, ClassDirectionDefinition>();
    private Dictionary<string, SkillDefinitionRecord> _skillsById = new Dictionary<string, SkillDefinitionRecord>();
    private const string MagicPrimaryGroupId = "primary_magic_class_01448";
    private const int DevelopmentLayoutWorkspaceWidth = 12000;
    private const int DevelopmentLayoutWorkspaceHeight = 12000;
    private const int DevelopmentLayoutCenterX = DevelopmentLayoutWorkspaceWidth / 2;
    private const int DevelopmentLayoutCenterY = DevelopmentLayoutWorkspaceHeight / 2;
    private const int DevelopmentLayoutNodeWidth = 172;
    private const int DevelopmentLayoutNodeHeight = 92;
    private const int DevelopmentLargeTestMinimumWorkingNodes = 600;

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
        if (!DevelopmentPlayerOrAdminEnabled(context)) return DevelopmentDisabled();
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        return Ok("Class tree state loaded.", CharacterProgressPayload(c, snapshot));
    }

    public ResponseEnvelope ClassTreeNodeGet(CommandContext context)
    {
        if (!DevelopmentPlayerOrAdminEnabled(context)) return DevelopmentDisabled();
        var actor = GetCurrentAccount(context);
        var includeAdmin = IsAdmin(actor);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        EnsureDefinitionsLoaded(false);
        if (!_nodesById.ContainsKey(nodeId)) throw new KeyNotFoundException("Node not found.");
        var node = _nodesById[nodeId];
        if (!includeAdmin && IsDevelopmentAdminOnlyHexagon(EffectiveHexagonId(node)))
            throw new KeyNotFoundException("Node not found.");
        if (!IsNodeHexagonEnabled(node)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");
        var snapshot = RecalculateProgress(c);
        var state = FindNodeState(c, nodeId);
        var reasons = EvaluateNodeAvailability(c, node, snapshot);
        return Ok("Node loaded.", new Dictionary<string, object>
        {
            { "node", NodePayload(node) },
            { "acquired", state != null },
            { "available", reasons.Count == 0 },
            { "reasons", reasons.Cast<object>().ToArray() }
        });
    }

    public ResponseEnvelope ClassTreeAvailableGet(CommandContext context)
    {
        if (!DevelopmentPlayerOrAdminEnabled(context)) return DevelopmentDisabled();
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        var includeAdmin = IsAdmin(actor);
        var requestedHexagonId = PayloadReader.GetString(context.Request.Payload, "hexagonId") ?? string.Empty;
        if (!includeAdmin && IsDevelopmentAdminOnlyHexagon(requestedHexagonId))
            throw new KeyNotFoundException("Development hexagon not found.");
        if (!IsHexagonEnabled(requestedHexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");
        var visibleNodes = includeAdmin
            ? _nodesById.Values
            : _nodesById.Values.Where(n => !ShouldHideNodeFromPlayer(n));
        if (!includeAdmin)
            visibleNodes = visibleNodes.Where(n => !IsDevelopmentAdminOnlyHexagon(EffectiveHexagonId(n)));
        visibleNodes = visibleNodes.Where(IsNodeHexagonEnabled);
        if (!string.IsNullOrWhiteSpace(requestedHexagonId))
        {
            visibleNodes = visibleNodes.Where(n => string.Equals(EffectiveHexagonId(n), requestedHexagonId, StringComparison.OrdinalIgnoreCase));
        }

        var items = visibleNodes.Select(n =>
        {
            var acquired = FindNodeState(c, n.NodeId) != null;
            var reasons = acquired ? new List<string>() : EvaluateNodeAvailability(c, n, snapshot);
            var hiddenForPlayer = !includeAdmin && ShouldHideNodeFromPlayer(n);
            var linkedClass = ResolveClassDefinitionForNode(n.NodeId);
            var requiresApproval = n.RequiresGMApproval ||
                n.RequiresPlayerRequest ||
                n.PurchasePolicy == DevelopmentPurchasePolicyIds.RequiresGMApproval ||
                n.PurchasePolicy == DevelopmentPurchasePolicyIds.RequestOnly;
            var canPurchase = !acquired && !hiddenForPlayer && !requiresApproval && reasons.Count == 0;
            var state = acquired ? "purchased" : canPurchase ? "available" : "locked";
            var canonicalDirectionId = CanonicalDevelopmentDirectionId(EffectiveHexagonId(n), n);
            return new Dictionary<string, object>
            {
                { "nodeId", n.NodeId },
                { "hexagonId", string.IsNullOrWhiteSpace(n.HexagonId) ? "main_development_hexagon" : n.HexagonId },
                { "hexagonType", EffectiveHexagonType(n) },
                { "hexagonName", GetHexagonDisplayName(EffectiveHexagonId(n)) },
                { "classId", linkedClass?.Code ?? n.ClassId ?? string.Empty },
                { "classCode", linkedClass?.Code ?? n.ClassId ?? string.Empty },
                { "classDisplayName", hiddenForPlayer ? string.Empty : linkedClass?.Name ?? string.Empty },
                { "name", hiddenForPlayer ? "????" : FirstNonEmpty(n.PublicName, n.Name, n.NodeId) },
                { "description", hiddenForPlayer ? "Скрытое развитие." : FirstNonEmpty(n.PublicDescription, n.Description) },
                { "directionId", n.DirectionId },
                { "directionCode", n.DirectionId },
                { "canonicalDirectionId", canonicalDirectionId },
                { "branchId", n.BranchId },
                { "branchCode", n.BranchId },
                { "canonicalBranchId", FirstNonEmpty(n.BranchId, canonicalDirectionId) },
                { "nodeType", n.NodeType },
                { "nodeRole", n.NodeRole },
                { "nodeTypeLabel", FormatDevelopmentNodeType(n) },
                { "isPrimaryMagicClass", IsPrimaryMagicClassNode(n) },
                { "primaryMagicGroupId", n.PrimaryMagicGroupId ?? string.Empty },
                { "magicRestrictionSummary", FirstNonEmpty(n.MagicRestrictionSummary, MagicPrimaryRestrictionSummary(n)) },
                { "tier", n.Tier },
                { "costExperienceCoins", Math.Max(0, n.CostExperienceCoins) },
                { "cost", Math.Max(0, n.CostExperienceCoins) },
                { "currencyId", FirstNonEmpty(n.CurrencyId, CharacterCurrencyIds.XpCoin) },
                { "requiresGMApproval", n.RequiresGMApproval || n.PurchasePolicy == DevelopmentPurchasePolicyIds.RequiresGMApproval || n.PurchasePolicy == DevelopmentPurchasePolicyIds.RequestOnly },
                { "requiresPlayerRequest", n.RequiresPlayerRequest },
                { "requirementSummary", hiddenForPlayer ? string.Empty : FirstNonEmpty(n.RequirementSummary, FormatRequirements(n.Requirements)) },
                { "rewardSummary", hiddenForPlayer ? string.Empty : FirstNonEmpty(n.RewardSummary, FormatRewards(n)) },
                { "gridX", n.GridX },
                { "gridY", n.GridY },
                { "positionX", n.GridX },
                { "positionY", n.GridY },
                { "angle", n.Angle },
                { "ring", n.Ring },
                { "sector", n.Sector },
                { "sortOrder", n.SortOrder },
                { "requiredNodeIds", GetRequiredNodeIds(n).Cast<object>().ToArray() },
                { "linkedNodeIds", GetRequiredNodeIds(n).Cast<object>().ToArray() },
                { "linkedClassId", linkedClass?.Code ?? n.ClassId ?? string.Empty },
                { "layoutVersion", Math.Max(1, n.LayoutVersion) },
                { "updatedAtUtc", n.UpdatedAtUtc },
                { "visibilityRule", hiddenForPlayer ? DevelopmentUnlockPolicyIds.VisibleAsUnknown : n.VisibilityRule },
                { "acquired", acquired },
                { "available", canPurchase },
                { "canPurchase", canPurchase },
                { "isPurchased", acquired },
                { "isUnlocked", acquired || canPurchase },
                { "state", state },
                { "status", state },
                { "costCurrencyId", FirstNonEmpty(n.CurrencyId, CharacterCurrencyIds.XpCoin) },
                { "costLabel", Math.Max(0, n.CostExperienceCoins) + " МО" },
                { "isPlayerVisible", !hiddenForPlayer },
                { "isVisibleToPlayer", !hiddenForPlayer },
                { "sourceOfTruth", "character_development_profiles" },
                { "hexagonLockedClass", true },
                { "reasons", reasons.Cast<object>().ToArray() }
            };
        }).Cast<object>().ToArray();

        return Ok("Available nodes loaded.", new Dictionary<string, object>
        {
            { "items", items },
            { "xpCoins", c.XpCoins },
            { "version", _definitionVersion },
            { "hexagon", DevelopmentHexagonPayload(DevelopmentHexagonIds.Main, includeAdmin) },
            { "hexagons", DevelopmentHexagonsPayload(includeAdmin).Cast<object>().ToArray() },
            { "activeHexagonId", string.IsNullOrWhiteSpace(requestedHexagonId) ? DevelopmentHexagonIds.Main : requestedHexagonId }
        });
    }

    public ResponseEnvelope ClassTreeAcquireNode(CommandContext context)
    {
        if (!DevelopmentPlayerOrAdminEnabled(context)) return DevelopmentDisabled();
        if (!DevelopmentNodePurchaseEnabled()) return DevelopmentDisabled("Покупка узлов развития выключена feature flags.");
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        EnsureDefinitionsLoaded(false);
        var node = _nodesById.ContainsKey(nodeId) ? _nodesById[nodeId] : throw new KeyNotFoundException("Node not found.");
        if (!IsAdmin(actor) && IsDevelopmentAdminOnlyHexagon(EffectiveHexagonId(node)))
            throw new KeyNotFoundException("Node not found.");
        if (!IsNodeHexagonEnabled(node)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");
        var requestedHexagonId = PayloadReader.GetString(context.Request.Payload, "hexagonId") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(requestedHexagonId) &&
            !string.Equals(requestedHexagonId, EffectiveHexagonId(node), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Node belongs to another development hexagon.");
        }
        if (FindNodeState(c, nodeId) != null) throw new InvalidOperationException("Node already acquired.");
        if (ShouldHideNodeFromPlayer(node) && !IsAdmin(actor)) throw new KeyNotFoundException("Node not found.");
        if ((node.RequiresGMApproval || node.RequiresPlayerRequest || node.PurchasePolicy == DevelopmentPurchasePolicyIds.RequiresGMApproval || node.PurchasePolicy == DevelopmentPurchasePolicyIds.RequestOnly) && !IsAdmin(actor))
            throw new InvalidOperationException("GM approval is required for this development node.");

        var snapshot = RecalculateProgress(c);
        var reasons = EvaluateNodeAvailability(c, node, snapshot);
        if (reasons.Count > 0) throw new InvalidOperationException("Node unavailable: " + string.Join(", ", reasons));
        ValidateMagicPrimaryPurchase(c, node);
        SpendExperienceCoinsForNode(c, actor, node, context.Request.RequestId ?? string.Empty);

        UpsertDevelopmentProfileNode(c, node, actor.Id, IsAdmin(actor) ? "admin_hexagon_gui" : "player_hexagon_purchase");
        snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("classTree", actor.Id, "acquireNode", c.Id + ":" + nodeId);
        TryPublishDevelopmentSync(c, "development.node.purchased", actor.Id, context.Request.RequestId ?? string.Empty);
        TryWriteDevelopmentJournal(c, actor.Id, "development.node.purchased", $"Узел развития приобретён: {SafeNodeName(node, includeAdmin: IsAdmin(actor))}");
        return Ok("Node acquired.", CharacterProgressPayload(c, snapshot));
    }

    public ResponseEnvelope ClassTreeRecalculate(CommandContext context)
    {
        if (!DevelopmentPlayerOrAdminEnabled(context)) return DevelopmentDisabled();
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
        if (!DevelopmentPlayerOrAdminEnabled(context)) return DevelopmentDisabled();
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        return Ok("Skills loaded.", new Dictionary<string, object> { { "items", SkillStatePayload(snapshot).ToArray() }, { "version", _definitionVersion } });
    }

    public ResponseEnvelope SkillsAvailable(CommandContext context)
    {
        if (!DevelopmentPlayerOrAdminEnabled(context)) return DevelopmentDisabled();
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        var available = SkillStatePayload(snapshot).Where(x => (bool)x["available"] && !(bool)x["acquired"]).Cast<object>().ToArray();
        return Ok("Available skills loaded.", new Dictionary<string, object> { { "items", available } });
    }

    public ResponseEnvelope SkillsGet(CommandContext context)
    {
        if (!DevelopmentPlayerOrAdminEnabled(context)) return DevelopmentDisabled();
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
        if (!DevelopmentPlayerOrAdminEnabled(context)) return DevelopmentDisabled();
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
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var requestedDirectionId = PayloadReader.GetString(context.Request.Payload, "directionId") ?? string.Empty;
        var requestedBranchId = PayloadReader.GetString(context.Request.Payload, "branchId") ?? string.Empty;
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");

        EnsureDefinitionsLoaded(false);
        if (!_nodesById.TryGetValue(nodeId, out var developmentNode))
            throw new KeyNotFoundException("Node not found.");

        if (!string.IsNullOrWhiteSpace(requestedDirectionId) &&
            !string.Equals(requestedDirectionId, developmentNode.DirectionId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Node direction mismatch.");

        if (!string.IsNullOrWhiteSpace(requestedBranchId) &&
            !string.Equals(requestedBranchId, developmentNode.BranchId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Node branch mismatch.");

        UpsertDevelopmentProfileNode(c, developmentNode, actor.Id);

        var snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("admin", actor.Id, "classTree.setState", c.Id + ":" + nodeId);
        return Ok("Class state updated.", CharacterProgressPayload(c, snapshot));
    }

    public ResponseEnvelope AdminSkillsSetState(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var skillId = RequireLength(PayloadReader.GetString(context.Request.Payload, "skillId"), 1, 128, "skillId");
        var acquired = PayloadReader.GetBool(context.Request.Payload, "acquired");

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
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        var snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("admin", actor.Id, "character.progress.recalculate", c.Id);
        return Ok("Character progress recalculated.", CharacterProgressPayload(c, snapshot));
    }

    public ResponseEnvelope DevelopmentHexagonGet(CommandContext context) => ClassTreeAvailableGet(context);
    public ResponseEnvelope DevelopmentNodeList(CommandContext context) => ClassTreeAvailableGet(context);
    public ResponseEnvelope DevelopmentCharacterGet(CommandContext context) => ClassTreeGet(context);
    public ResponseEnvelope DevelopmentCharacterInitialize(CommandContext context) => ClassTreeRecalculate(context);
    public ResponseEnvelope DevelopmentNodePurchase(CommandContext context) => ClassTreeAcquireNode(context);
    public ResponseEnvelope DevelopmentPlayerHexagonGet(CommandContext context) => DevelopmentHexagonPlayerGetProductProjection(context);
    public ResponseEnvelope DevelopmentPlayerPurchase(CommandContext context) => ClassTreeAcquireNode(context);

    public ResponseEnvelope DevelopmentXpLedgerList(CommandContext context)
    {
        if (!DevelopmentPlayerOrAdminEnabled(context)) return DevelopmentDisabled();
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        var includeAdmin = IsAdmin(actor);
        var filter = Builders<ExperienceCoinLedgerEntry>.Filter.Eq(x => x.CharacterId, c.Id);
        if (!includeAdmin) filter &= Builders<ExperienceCoinLedgerEntry>.Filter.Eq(x => x.IsPlayerVisible, true);
        var items = _repositories.ExperienceCoinLedger.Find(filter)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .Select(x => (object)ExperienceCoinLedgerPayload(x, includeAdmin))
            .ToArray();
        return Ok("Experience coin ledger loaded.", new Dictionary<string, object> { { "items", items }, { "xpCoins", c.XpCoins } });
    }

    public ResponseEnvelope DevelopmentXpGrant(CommandContext context)
        => DevelopmentXpAdjust(context, ExperienceCoinLedgerEntryTypeIds.Grant, Math.Abs(PayloadReader.GetInt(context.Request.Payload, "amount") ?? 0));

    public ResponseEnvelope DevelopmentXpRefund(CommandContext context)
        => DevelopmentXpAdjust(context, ExperienceCoinLedgerEntryTypeIds.Refund, Math.Abs(PayloadReader.GetInt(context.Request.Payload, "amount") ?? 0));

    public ResponseEnvelope DevelopmentXpCorrect(CommandContext context)
        => DevelopmentXpAdjust(context, ExperienceCoinLedgerEntryTypeIds.Correction, PayloadReader.GetInt(context.Request.Payload, "amount") ?? 0);

    public ResponseEnvelope DevelopmentNodeReveal(CommandContext context) => DevelopmentAdminStateStub(context, "development.node.revealed", "Development node revealed.");
    public ResponseEnvelope DevelopmentNodeHide(CommandContext context) => DevelopmentAdminStateStub(context, "development.node.hidden", "Development node hidden.");
    public ResponseEnvelope DevelopmentNodeUnlock(CommandContext context) => AdminClassTreeSetState(context);
    public ResponseEnvelope DevelopmentAdminHexagonGet(CommandContext context) => ClassTreeAvailableGet(context);

    public ResponseEnvelope DevelopmentHexagonAdminList(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagons = DevelopmentHexagonsPayload(includeAdmin: true)
            .Select(hexagon =>
            {
                var hexagonId = Convert.ToString(hexagon["hexagonId"]) ?? DevelopmentHexagonIds.Main;
                var nodeCount = _nodesById.Values.Count(node => string.Equals(EffectiveHexagonId(node), hexagonId, StringComparison.OrdinalIgnoreCase));
                hexagon["nodeCount"] = nodeCount;
                hexagon["sourceOfTruth"] = "class_tree_definitions";
                return hexagon;
            })
            .Cast<object>()
            .ToArray();
        return Ok("Development hexagons loaded.", new Dictionary<string, object>
        {
            { "items", hexagons },
            { "sourceOfTruth", "class_tree_definitions" }
        });
    }

    public ResponseEnvelope DevelopmentHexagonAdminSeedLargeTestTree(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var requestedNodeCount = PayloadReader.GetInt(context.Request.Payload, "nodeCount") ?? DevelopmentLargeTestMinimumWorkingNodes;
        requestedNodeCount = Math.Max(DevelopmentLargeTestMinimumWorkingNodes, Math.Min(900, requestedNodeCount));
        var result = SeedLargeDevelopmentTestTree(actor.Id, requestedNodeCount);
        EnsureDefinitionsLoaded(true);
        WriteAudit("development", actor.Id, "development_hexagon.large_test.seeded", $"{DevelopmentHexagonIds.LargeTest0154}:nodes={result["workingNodeCount"]}:links={result["linkCount"]}");
        _logger.Admin($"development.hexagon.large_test.seeded actor={actor.Login} nodes={result["workingNodeCount"]} links={result["linkCount"]}");
        return Ok("Большое тестовое дерево развития создано.", result);
    }

    public ResponseEnvelope DevelopmentHexagonAdminGetLayout(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");
        return Ok("Development hexagon layout loaded.", DevelopmentHexagonLayoutPayload(hexagonId, includeAdmin: true));
    }

    public ResponseEnvelope DevelopmentHexagonAdminPreviewLayout(CommandContext context) => DevelopmentHexagonAdminGetLayout(context);

    public ResponseEnvelope DevelopmentHexagonAdminPreviewBaselineLayout(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        var before = CurrentDevelopmentLayoutPositions(hexagonId);
        var planned = BuildDevelopmentBaselineLayout(hexagonId);
        var changed = CountChangedDevelopmentLayoutPositions(before, planned);
        var payload = DevelopmentHexagonLayoutPayloadWithPositions(hexagonId, includeAdmin: true, planned);
        payload["preview"] = true;
        payload["persisted"] = false;
        payload["changedCount"] = changed;
        payload["qualityBefore"] = BuildDevelopmentLayoutQualityReport(hexagonId, before, "before");
        payload["qualityAfter"] = BuildDevelopmentLayoutQualityReport(hexagonId, planned, "after");
        payload["layoutGeneratedBy"] = "baseline_0_15_4";
        WriteAudit("development", actor.Id, "development_hexagon.layout.previewed", $"{hexagonId}:changed={changed}");
        _logger.Admin($"development.hexagon.layout.previewed actor={actor.Login} hexagonId={hexagonId} changed={changed}");
        return Ok("Предпросмотр базовой раскладки построен. Изменения ещё не сохранены.", payload);
    }

    public ResponseEnvelope DevelopmentHexagonAdminApplyBaselineLayout(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        var expectedRevision = PayloadReader.GetInt(context.Request.Payload, "layoutRevision") ?? PayloadReader.GetInt(context.Request.Payload, "revision");
        var currentRevision = CurrentDevelopmentLayoutRevision(hexagonId);
        if (expectedRevision.HasValue && expectedRevision.Value != currentRevision)
            return Error($"Layout revision conflict. Reload layout before applying baseline. current={currentRevision}; expected={expectedRevision.Value}", ResponseStatus.Conflict, ErrorCode.Conflict);

        var before = CurrentDevelopmentLayoutPositions(hexagonId);
        var snapshotId = CreateDevelopmentLayoutSnapshot(hexagonId, actor.Id);
        var planned = BuildDevelopmentBaselineLayout(hexagonId);
        var changed = ApplyDevelopmentLayoutPositions(hexagonId, planned, actor.Id, "baseline_0_15_4", "foundation_0_15_4_baseline");
        if (changed > 0) EnsureDefinitionsLoaded(true);

        var after = CurrentDevelopmentLayoutPositions(hexagonId);
        var payload = DevelopmentHexagonLayoutPayload(hexagonId, includeAdmin: true);
        payload["snapshotId"] = snapshotId;
        payload["changedCount"] = changed;
        payload["qualityBefore"] = BuildDevelopmentLayoutQualityReport(hexagonId, before, "before");
        payload["qualityAfter"] = BuildDevelopmentLayoutQualityReport(hexagonId, after, "after");
        payload["layoutGeneratedBy"] = "baseline_0_15_4";
        WriteAudit("development", actor.Id, "development_hexagon.layout.baseline.applied", $"{hexagonId}:snapshot={snapshotId}:changed={changed}");
        _logger.Admin($"development.hexagon.layout.baseline.applied actor={actor.Login} hexagonId={hexagonId} snapshot={snapshotId} changed={changed}");
        return Ok("Базовая раскладка применена и сохранена.", payload);
    }

    public ResponseEnvelope DevelopmentHexagonAdminCreateLayoutSnapshot(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        var snapshotId = CreateDevelopmentLayoutSnapshot(hexagonId, actor.Id);
        EnsureDefinitionsLoaded(true);
        var payload = DevelopmentHexagonLayoutPayload(hexagonId, includeAdmin: true);
        payload["snapshotId"] = snapshotId;
        WriteAudit("development", actor.Id, "development_hexagon.layout.snapshot.created", $"{hexagonId}:snapshot={snapshotId}");
        _logger.Admin($"development.hexagon.layout.snapshot.created actor={actor.Login} hexagonId={hexagonId} snapshot={snapshotId}");
        return Ok("Снимок раскладки создан.", payload);
    }

    public ResponseEnvelope DevelopmentHexagonAdminRestoreLayoutSnapshot(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        var before = CurrentDevelopmentLayoutPositions(hexagonId);
        var changed = RestoreDevelopmentLayoutSnapshot(hexagonId, actor.Id);
        if (changed > 0) EnsureDefinitionsLoaded(true);
        var after = CurrentDevelopmentLayoutPositions(hexagonId);
        var payload = DevelopmentHexagonLayoutPayload(hexagonId, includeAdmin: true);
        payload["changedCount"] = changed;
        payload["qualityBefore"] = BuildDevelopmentLayoutQualityReport(hexagonId, before, "beforeRestore");
        payload["qualityAfter"] = BuildDevelopmentLayoutQualityReport(hexagonId, after, "afterRestore");
        WriteAudit("development", actor.Id, "development_hexagon.layout.snapshot.restored", $"{hexagonId}:changed={changed}");
        _logger.Admin($"development.hexagon.layout.snapshot.restored actor={actor.Login} hexagonId={hexagonId} changed={changed}");
        return Ok("Раскладка восстановлена из последнего снимка.", payload);
    }

    public ResponseEnvelope DevelopmentHexagonAdminGetLayoutQualityReport(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        var positions = CurrentDevelopmentLayoutPositions(hexagonId);
        var report = BuildDevelopmentLayoutQualityReport(hexagonId, positions, "current");
        WriteAudit("development", actor.Id, "development_hexagon.layout.quality.checked", $"{hexagonId}:score={report["readabilityScore"]}");
        return Ok("Оценка читаемости раскладки готова.", new Dictionary<string, object>
        {
            { "hexagonId", hexagonId },
            { "report", report },
            { "sourceOfTruth", "class_tree_definitions" }
        });
    }

    public ResponseEnvelope DevelopmentHexagonAdminGetEditableGraph(CommandContext context) => DevelopmentHexagonAdminGetLayout(context);

    public ResponseEnvelope DevelopmentHexagonAdminValidateGraph(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");
        var issues = ValidateDevelopmentGraph(hexagonId);
        WriteAudit("development", actor.Id, "development_hexagon.graph.validated", $"{hexagonId}:issues={issues.Count}");
        var payload = DevelopmentHexagonLayoutPayload(hexagonId, includeAdmin: true);
        payload["valid"] = issues.Count == 0;
        payload["issues"] = issues.Cast<object>().ToArray();
        return Ok(issues.Count == 0 ? "Граф развития корректен." : "Граф развития требует внимания.", payload);
    }

    public ResponseEnvelope DevelopmentHexagonAdminSaveNodeEdit(CommandContext context) => DevelopmentAdminNodeUpdate(context);

    public ResponseEnvelope DevelopmentHexagonAdminCreateNode(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        var nodeId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "nodeId"),
            "dev_node_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
        nodeId = RequireLength(nodeId, 1, 128, "nodeId");
        if (_nodesById.ContainsKey(nodeId))
            return Error("Development node already exists.", ResponseStatus.Conflict, ErrorCode.Conflict);

        var node = new ClassNodeDefinition
        {
            NodeId = nodeId,
            HexagonId = hexagonId,
            HexagonType = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonType"), HexagonTypeFromId(hexagonId)),
            DirectionId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "directionCode"), PayloadReader.GetString(context.Request.Payload, "directionId"), "root"),
            BranchId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "branchCode"), PayloadReader.GetString(context.Request.Payload, "branchId"), "root"),
            Name = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), PayloadReader.GetString(context.Request.Payload, "title"), "Новый узел"),
            PublicName = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicName"), PayloadReader.GetString(context.Request.Payload, "name"), "Новый узел"),
            Description = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "description"), "Описание узла развития."),
            PublicDescription = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicDescription"), PayloadReader.GetString(context.Request.Payload, "description"), "Описание узла развития."),
            NodeType = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "nodeType"), DevelopmentNodeTypes.Class),
            NodeRole = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "nodeRole"), DevelopmentNodeRoleIds.MainBranchLevel),
            Tier = ReadOptionalInt(context.Request.Payload, "tier", 1),
            MaxTier = ReadOptionalInt(context.Request.Payload, "maxTier", 20),
            GridX = ReadRequiredInt(context.Request.Payload, "positionX", "gridX", 500),
            GridY = ReadRequiredInt(context.Request.Payload, "positionY", "gridY", 500),
            Ring = ReadOptionalInt(context.Request.Payload, "ring", 1),
            Sector = ReadOptionalInt(context.Request.Payload, "sector", 0),
            SortOrder = ReadOptionalInt(context.Request.Payload, "sortOrder", _nodesById.Count + 1),
            CostExperienceCoins = ReadOptionalInt(context.Request.Payload, "cost", 1),
            CurrencyId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "currencyId"), CharacterCurrencyIds.XpCoin),
            IsPlayerVisible = true,
            VisibilityRule = DevelopmentUnlockPolicyIds.VisibleByDefault,
            UnlockPolicy = DevelopmentUnlockPolicyIds.VisibleByDefault,
            PurchasePolicy = DevelopmentPurchasePolicyIds.AutomaticIfRequirementsMet,
            RequiresGMApproval = PayloadReader.GetBool(context.Request.Payload, "requiresGMApproval"),
            RequiresPlayerRequest = PayloadReader.GetBool(context.Request.Payload, "requiresPlayerRequest"),
            LayoutGroup = PayloadReader.GetString(context.Request.Payload, "layoutGroup") ?? string.Empty,
            LayoutBranch = PayloadReader.GetString(context.Request.Payload, "layoutBranch") ?? string.Empty,
            LayoutVersion = 1,
            Revision = 1,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserId = actor.Id,
            SchemaVersion = 1
        };
        ValidateRange(node.Tier, 1, 20, "Tier");
        ValidateRange(node.MaxTier, node.Tier, 20, "MaxTier");
        if (node.RequiresGMApproval || node.RequiresPlayerRequest)
            node.PurchasePolicy = DevelopmentPurchasePolicyIds.RequiresGMApproval;
        if (!IsAllowedDevelopmentCurrency(node.CurrencyId))
            return Error("Invalid development currency.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        PersistDevelopmentNodeDefinition(node);
        EnsureDefinitionsLoaded(true);
        context.Request.Payload["nodeId"] = nodeId;
        WriteAudit("development", actor.Id, "development_hexagon.node.created", nodeId);
        return DevelopmentAdminNodeUpdate(context);
    }

    public ResponseEnvelope DevelopmentHexagonAdminArchiveNode(CommandContext context)
        => SetDevelopmentNodeArchived(context, archived: true);

    public ResponseEnvelope DevelopmentHexagonAdminRestoreNode(CommandContext context)
        => SetDevelopmentNodeArchived(context, archived: false);

    public ResponseEnvelope DevelopmentHexagonAdminAddRequirementLink(CommandContext context)
        => UpdateDevelopmentRequirementLink(context, add: true);

    public ResponseEnvelope DevelopmentHexagonAdminRemoveRequirementLink(CommandContext context)
        => UpdateDevelopmentRequirementLink(context, add: false);

    public ResponseEnvelope DevelopmentHexagonAdminValidateLayout(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        var updates = ReadDevelopmentLayoutUpdates(context.Request.Payload, hexagonId, requireNodes: false);
        var issues = ValidateDevelopmentLayoutUpdates(hexagonId, updates).Cast<object>().ToArray();
        WriteAudit("development", actor.Id, "development_hexagon.layout.validated", $"{hexagonId}:issues={issues.Length}");
        return Ok(issues.Length == 0 ? "Development hexagon layout is valid." : "Development hexagon layout has warnings.", new Dictionary<string, object>
        {
            { "hexagonId", hexagonId },
            { "valid", issues.Length == 0 },
            { "issues", issues },
            { "sourceOfTruth", "class_tree_definitions" }
        });
    }

    public ResponseEnvelope DevelopmentHexagonAdminSaveLayout(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        var expectedRevision = PayloadReader.GetInt(context.Request.Payload, "layoutRevision") ?? PayloadReader.GetInt(context.Request.Payload, "revision");
        var currentRevision = CurrentDevelopmentLayoutRevision(hexagonId);
        if (expectedRevision.HasValue && expectedRevision.Value != currentRevision)
        {
            return Error($"Layout revision conflict. Reload layout before saving. current={currentRevision}; expected={expectedRevision.Value}", ResponseStatus.Conflict, ErrorCode.Conflict);
        }

        var updates = ReadDevelopmentLayoutUpdates(context.Request.Payload, hexagonId, requireNodes: true);
        var issues = ValidateDevelopmentLayoutUpdates(hexagonId, updates);
        if (issues.Count > 0) throw new InvalidOperationException("Layout validation failed: " + string.Join("; ", issues));

        var changed = 0;
        foreach (var update in updates)
        {
            if (!_nodesById.TryGetValue(update.NodeId, out var node))
                throw new KeyNotFoundException("Development node not found: " + update.NodeId);
            if (!string.Equals(EffectiveHexagonId(node), hexagonId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Node belongs to another development hexagon: " + update.NodeId);
            if (node.GridX == update.PositionX && node.GridY == update.PositionY) continue;

            node.GridX = update.PositionX;
            node.GridY = update.PositionY;
            node.LayoutVersion = Math.Max(1, node.LayoutVersion) + 1;
            node.UpdatedAtUtc = DateTime.UtcNow;
            node.UpdatedByUserId = actor.Id;
            node.SchemaVersion = Math.Max(1, node.SchemaVersion);
            PersistDevelopmentNodeDefinition(node);
            changed++;
        }

        if (changed > 0) EnsureDefinitionsLoaded(true);
        WriteAudit("development", actor.Id, "development_hexagon.layout.saved", $"{hexagonId}:changed={changed}");
        _logger.Admin($"development.hexagon.layout.save actor={actor.Login} hexagonId={hexagonId} changed={changed}");
        var payload = DevelopmentHexagonLayoutPayload(hexagonId, includeAdmin: true);
        payload["changedCount"] = changed;
        return Ok("Development hexagon layout saved.", payload);
    }

    public ResponseEnvelope DevelopmentHexagonAdminResetLayout(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        var changed = 0;
        foreach (var node in _nodesById.Values.Where(n => string.Equals(EffectiveHexagonId(n), hexagonId, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var fallback = DefaultDevelopmentNodePosition(node);
            if (node.GridX == fallback.Item1 && node.GridY == fallback.Item2) continue;
            node.GridX = fallback.Item1;
            node.GridY = fallback.Item2;
            node.LayoutVersion = Math.Max(1, node.LayoutVersion) + 1;
            node.UpdatedAtUtc = DateTime.UtcNow;
            node.UpdatedByUserId = actor.Id;
            node.SchemaVersion = Math.Max(1, node.SchemaVersion);
            PersistDevelopmentNodeDefinition(node);
            changed++;
        }

        if (changed > 0) EnsureDefinitionsLoaded(true);
        WriteAudit("development", actor.Id, "development_hexagon.layout.reset", $"{hexagonId}:changed={changed}");
        var payload = DevelopmentHexagonLayoutPayload(hexagonId, includeAdmin: true);
        payload["changedCount"] = changed;
        return Ok("Development hexagon layout reset.", payload);
    }

    public ResponseEnvelope DevelopmentHexagonPlayerList(CommandContext context)
    {
        if (!DevelopmentPlayerEnabled()) return DevelopmentDisabled();
        GetCurrentAccount(context);
        EnsureDefinitionsLoaded(false);
        var hexagons = DevelopmentHexagonsPayload(includeAdmin: false).Cast<object>().ToArray();
        return Ok("Visible development hexagons loaded.", new Dictionary<string, object> { { "items", hexagons } });
    }

    public ResponseEnvelope DevelopmentHexagonPlayerGetLayout(CommandContext context)
    {
        return DevelopmentHexagonPlayerGetProductProjection(context);
    }

    public ResponseEnvelope DevelopmentHexagonPlayerGetNodeDetails(CommandContext context)
    {
        if (!DevelopmentPlayerEnabled()) return DevelopmentDisabled();
        var actor = GetCurrentAccount(context);
        if (!IsAdmin(actor))
        {
            ResolveCharacterForClassSkill(context, actor);
        }

        EnsureDefinitionsLoaded(false);
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        if (!_nodesById.TryGetValue(nodeId, out var node) || ShouldHideNodeFromPlayer(node))
            throw new KeyNotFoundException("Development node not found.");
        if (!IsAdmin(actor) && IsDevelopmentAdminOnlyHexagon(EffectiveHexagonId(node)))
            throw new KeyNotFoundException("Development node not found.");
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), EffectiveHexagonId(node));
        if (!string.Equals(hexagonId, EffectiveHexagonId(node), StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("Development node not found.");
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");
        return Ok("Visible development node loaded.", new Dictionary<string, object>
        {
            { "node", PlayerDevelopmentNodeLayoutPayload(node) },
            { "sourceOfTruth", "class_tree_definitions" },
            { "isPlayerSafe", true }
        });
    }

    public ResponseEnvelope DevelopmentAdminNodeUpdate(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);

        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        if (!_nodesById.TryGetValue(nodeId, out var node))
            throw new KeyNotFoundException("Development node not found.");

        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), node.HexagonId, "main_development_hexagon");
        if (!string.Equals(hexagonId, FirstNonEmpty(node.HexagonId, "main_development_hexagon"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Hexagon mismatch.");
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        var positionX = ReadRequiredInt(context.Request.Payload, "positionX", "gridX", node.GridX);
        var positionY = ReadRequiredInt(context.Request.Payload, "positionY", "gridY", node.GridY);
        ValidateRange(positionX, 0, DevelopmentLayoutWorkspaceWidth, "PositionX");
        ValidateRange(positionY, 0, DevelopmentLayoutWorkspaceHeight, "PositionY");

        var ring = ReadOptionalInt(context.Request.Payload, "ring", node.Ring <= 0 ? 1 : node.Ring);
        ValidateRange(ring, 0, 20, "Ring");
        var sector = ReadOptionalInt(context.Request.Payload, "sector", node.Sector);
        ValidateRange(sector, 0, 6, "Sector");
        var sortOrder = ReadOptionalInt(context.Request.Payload, "sortOrder", node.SortOrder);
        ValidateRange(sortOrder, 0, 100000, "SortOrder");
        var cost = ReadOptionalInt(context.Request.Payload, "cost", node.CostExperienceCoins);
        cost = ReadOptionalInt(context.Request.Payload, "costExperienceCoins", cost);
        ValidateRange(cost, 0, 100000, "Cost");

        var directionCode = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "directionCode"), PayloadReader.GetString(context.Request.Payload, "directionId"), node.DirectionId);
        if (BuildDevelopmentDirections().All(d => !string.Equals(d.DirectionId, directionCode, StringComparison.OrdinalIgnoreCase)) &&
            !string.Equals(directionCode, "root", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid direction code.");

        var branchCode = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "branchCode"), PayloadReader.GetString(context.Request.Payload, "branchId"), node.BranchId), 0, 128, "branchCode");
        var linkedClassId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "linkedClassId"), PayloadReader.GetString(context.Request.Payload, "classId"), node.ClassId), 0, 128, "linkedClassId");
        var currencyId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "currencyId"), node.CurrencyId, CharacterCurrencyIds.XpCoin), 1, 64, "currencyId");
        if (!IsAllowedDevelopmentCurrency(currencyId))
            return Error("Invalid development currency.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var nodeType = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "nodeType"), node.NodeType, DevelopmentNodeTypes.Class), 1, 128, "nodeType");
        var nodeRole = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "nodeRole"), node.NodeRole, DevelopmentNodeRoleIds.MainBranchLevel), 1, 128, "nodeRole");
        var tier = ReadOptionalInt(context.Request.Payload, "tier", Math.Max(1, node.Tier));
        var maxTier = ReadOptionalInt(context.Request.Payload, "maxTier", Math.Max(tier, node.MaxTier));
        ValidateRange(tier, 1, 20, "Tier");
        ValidateRange(maxTier, tier, 20, "MaxTier");
        var hexagonType = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonType"), node.HexagonType, HexagonTypeFromId(hexagonId)), 1, 64, "hexagonType");
        var primaryMagicGroupId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "primaryMagicGroupId"), node.PrimaryMagicGroupId), 0, 128, "primaryMagicGroupId");
        var name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), PayloadReader.GetString(context.Request.Payload, "title"), node.Name, node.NodeId), 1, 256, "name");
        var publicName = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicName"), name), 1, 256, "publicName");
        var description = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "description"), node.Description), 0, 4000, "description");
        var publicDescription = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicDescription"), description, node.PublicDescription), 0, 4000, "publicDescription");
        var linkedDefinitionKind = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "linkedDefinitionKind"), PayloadReader.GetString(context.Request.Payload, "linkedEntityType"), node.LinkedDefinitionKind), 0, 128, "linkedDefinitionKind");
        var linkedDefinitionId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "linkedDefinitionId"), PayloadReader.GetString(context.Request.Payload, "linkedEntityId"), node.LinkedDefinitionId), 0, 256, "linkedDefinitionId");
        var requiredNodeIds = ReadRequiredNodeIds(context.Request.Payload, node);
        var requirementExpression = ReadRequirementExpression0219(context.Request.Payload, "requirementExpression", node.RequirementExpression);
        var unlockSkillIds = ReadStringList0219(context.Request.Payload, "unlockSkillIds", node.UnlockSkillIds);

        if (requiredNodeIds.Any(id => string.Equals(id, nodeId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Self requirement is not allowed.");

        foreach (var requiredNodeId in requiredNodeIds)
        {
            if (!_nodesById.ContainsKey(requiredNodeId))
                throw new KeyNotFoundException("Required node not found: " + requiredNodeId);
        }

        var isPlayerVisible = context.Request.Payload.ContainsKey("isPlayerVisible")
            ? PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible")
            : node.IsPlayerVisible && !ShouldHideNodeFromPlayer(node);
        var isHidden = context.Request.Payload.ContainsKey("isHidden")
            ? PayloadReader.GetBool(context.Request.Payload, "isHidden")
            : node.IsHidden;
        var isArchived = context.Request.Payload.ContainsKey("isArchived")
            ? PayloadReader.GetBool(context.Request.Payload, "isArchived")
            : node.IsArchived;
        var layoutLockedManualPosition = context.Request.Payload.ContainsKey("layoutLockedManualPosition")
            ? PayloadReader.GetBool(context.Request.Payload, "layoutLockedManualPosition")
            : node.LayoutLockedManualPosition;
        var visibilityRule = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "visibilityRule"), node.VisibilityRule, DevelopmentUnlockPolicyIds.VisibleByDefault);
        if (isArchived)
        {
            isHidden = true;
            isPlayerVisible = false;
            visibilityRule = DevelopmentUnlockPolicyIds.GMOnly;
        }

        node.HexagonId = hexagonId;
        node.HexagonType = hexagonType;
        node.DirectionId = directionCode;
        node.BranchId = branchCode;
        node.ClassId = linkedClassId;
        node.Name = name;
        node.PublicName = publicName;
        node.Description = description;
        node.PublicDescription = publicDescription;
        node.LinkedDefinitionKind = linkedDefinitionKind;
        node.LinkedDefinitionId = linkedDefinitionId;
        node.NodeType = nodeType;
        node.NodeRole = nodeRole;
        node.Tier = tier;
        node.MaxTier = maxTier;
        node.LayoutGroup = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "layoutGroup"), node.LayoutGroup);
        node.LayoutBranch = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "layoutBranch"), node.LayoutBranch);
        node.RequiresGMApproval = context.Request.Payload.ContainsKey("requiresGMApproval")
            ? PayloadReader.GetBool(context.Request.Payload, "requiresGMApproval")
            : node.RequiresGMApproval;
        node.RequiresPlayerRequest = context.Request.Payload.ContainsKey("requiresPlayerRequest")
            ? PayloadReader.GetBool(context.Request.Payload, "requiresPlayerRequest")
            : node.RequiresPlayerRequest;
        node.IsPrimaryMagicClass = context.Request.Payload.ContainsKey("isPrimaryMagicClass")
            ? PayloadReader.GetBool(context.Request.Payload, "isPrimaryMagicClass")
            : IsPrimaryMagicClassNode(node);
        node.PrimaryMagicGroupId = node.IsPrimaryMagicClass
            ? FirstNonEmpty(primaryMagicGroupId, MagicPrimaryGroupId)
            : primaryMagicGroupId;
        node.MagicRestrictionSummary = node.IsPrimaryMagicClass
            ? "Можно выбрать только один первичный магический класс, пока первый магический путь не завершён."
            : node.MagicRestrictionSummary;
        node.GridX = positionX;
        node.GridY = positionY;
        node.Ring = ring;
        node.Sector = sector;
        node.SortOrder = sortOrder;
        node.CostExperienceCoins = cost;
        node.CurrencyId = currencyId;
        node.IsArchived = isArchived;
        node.IsHidden = isHidden;
        node.IsGMOnly = isHidden;
        node.IsPlayerVisible = isPlayerVisible && !isHidden;
        node.LayoutLockedManualPosition = layoutLockedManualPosition;
        node.VisibilityRule = isHidden
            ? DevelopmentUnlockPolicyIds.GMOnly
            : node.IsPlayerVisible ? FirstNonEmpty(visibilityRule, DevelopmentUnlockPolicyIds.VisibleByDefault) : DevelopmentUnlockPolicyIds.HiddenUntilGMReveal;
        node.UnlockPolicy = isHidden ? DevelopmentUnlockPolicyIds.GMOnly : DevelopmentUnlockPolicyIds.VisibleByDefault;
        node.PurchasePolicy = isHidden
            ? DevelopmentPurchasePolicyIds.GMOnly
            : node.RequiresGMApproval || node.RequiresPlayerRequest
                ? DevelopmentPurchasePolicyIds.RequiresGMApproval
                : DevelopmentPurchasePolicyIds.AutomaticIfRequirementsMet;
        node.Requirements = requiredNodeIds
            .Select(id => new UnlockRequirement { RequirementType = "node", Key = id })
            .ToList();
        node.RequirementExpression = requirementExpression;
        node.UnlockSkillIds = unlockSkillIds;
        node.RequirementSummary = requirementExpression != null
            ? RequirementExpressionSummary0219(requirementExpression)
            : requiredNodeIds.Count == 0
                ? "Нет требований."
                : "Требуется: " + string.Join(", ", requiredNodeIds);
        node.LayoutVersion = Math.Max(1, node.LayoutVersion) + 1;
        node.Revision = Math.Max(1, node.Revision) + 1;
        node.UpdatedAtUtc = DateTime.UtcNow;
        node.UpdatedByUserId = actor.Id;
        node.SchemaVersion = Math.Max(1, node.SchemaVersion);

        var graphIssues = ValidateDevelopmentGraph(hexagonId)
            .Where(issue => issue.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            .Where(issue => DevelopmentGraphIssueTouches(issue, nodeId))
            .ToList();
        if (graphIssues.Count > 0)
        {
            EnsureDefinitionsLoaded(true);
            return Error("Development graph validation failed: " + string.Join("; ", graphIssues), ResponseStatus.Conflict, ErrorCode.Conflict);
        }

        PersistDevelopmentNodeDefinition(node);
        EnsureDefinitionsLoaded(true);
        _nodesById.TryGetValue(nodeId, out var updatedNode);
        updatedNode ??= node;

        WriteAudit("development", actor.Id, "development_hexagon.node.updated", nodeId);
        _logger.Admin($"development.node.layout.update actor={actor.Login} nodeId={nodeId} x={positionX} y={positionY} ring={ring} sector={sector} direction={directionCode} branch={branchCode} version={updatedNode.LayoutVersion}");

        return Ok("Development node layout saved.", new Dictionary<string, object>
        {
            { "node", NodePayload(updatedNode) },
            { "hexagon", DevelopmentHexagonPayload(EffectiveHexagonId(updatedNode), includeAdmin: true) },
            { "hexagons", DevelopmentHexagonsPayload(includeAdmin: true).Cast<object>().ToArray() },
            { "sourceOfTruth", "class_tree_definitions" }
        });
    }

    public ResponseEnvelope DevelopmentAdminNodeComplete(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressInitialized(c);
        EnsureDefinitionsLoaded(false);

        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        if (!_nodesById.TryGetValue(nodeId, out var node))
            throw new KeyNotFoundException("Development node not found.");
        if (!IsNodeHexagonEnabled(node)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        UpsertDevelopmentProfileNode(c, node, actor.Id, "admin_hexagon_complete");
        MarkDevelopmentProfileNodeCompleted(c, node, actor.Id);

        var snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("development", actor.Id, "node.complete", c.Id + ":" + nodeId);
        TryPublishDevelopmentSync(c, "development.node.completed", actor.Id, context.Request.RequestId ?? string.Empty);

        return Ok("Development node completed.", CharacterProgressPayload(c, snapshot));
    }

    public ResponseEnvelope DevelopmentNodeRevoke(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        foreach (var direction in c.ClassDirections)
        {
            direction.AcquiredNodes.RemoveAll(x => string.Equals(x.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        }
        RemoveDevelopmentProfileNode(c, nodeId);
        var snapshot = RecalculateProgress(c);
        _repositories.Characters.Replace(c);
        WriteAudit("development", actor.Id, "node.revoke", c.Id + ":" + nodeId);
        TryPublishDevelopmentSync(c, "development.node.revoked", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Development node revoked.", CharacterProgressPayload(c, snapshot));
    }

    public ResponseEnvelope DevelopmentVocationSet(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        var vocation = RequireLength(PayloadReader.GetString(context.Request.Payload, "vocationTitle"), 0, 160, "vocationTitle");
        c.ClassSkillSnapshot ??= new CharacterProgressSnapshot { CharacterId = c.Id };
        c.ClassSkillSnapshot.DefinitionVersion = string.IsNullOrWhiteSpace(vocation) ? c.ClassSkillSnapshot.DefinitionVersion : _definitionVersion;
        c.Description = c.Description;
        _repositories.Characters.Replace(c);
        WriteAudit("development", actor.Id, "vocation.set", c.Id + ":" + vocation);
        TryPublishDevelopmentSync(c, "development.vocation.changed", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Vocation updated.", new Dictionary<string, object> { { "characterId", c.Id }, { "vocationTitle", vocation } });
    }

    public ResponseEnvelope DevelopmentPlayerRequestPurchase(CommandContext context)
    {
        if (!DevelopmentPlayerEnabled()) return DevelopmentDisabled();
        if (!_featureFlags.IsEnabled(nameof(DevelopmentFeatureFlags.UseDevelopmentRequestIntegration))) return DevelopmentDisabled("Заявки на развитие выключены feature flags.");
        var actor = GetCurrentAccount(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        EnsureDefinitionsLoaded(false);
        var node = _nodesById.ContainsKey(nodeId) ? _nodesById[nodeId] : throw new KeyNotFoundException("Node not found.");
        if (!IsAdmin(actor) && IsDevelopmentAdminOnlyHexagon(EffectiveHexagonId(node)))
            throw new KeyNotFoundException("Node not found.");
        var request = new PlayerRequestState
        {
            RequestNumber = NextPlayerRequestNumber(),
            CampaignId = PayloadReader.GetString(context.Request.Payload, "campaignId") ?? string.Empty,
            CharacterId = c.Id,
            CreatedByUserId = actor.Id,
            RequestType = PlayerRequestTypeIds.Custom,
            ProposalType = "development_purchase",
            Title = "Заявка на развитие: " + SafeNodeName(node, includeAdmin: false),
            Description = PayloadReader.GetString(context.Request.Payload, "comment") ?? string.Empty,
            Status = PlayerRequestStatusIds.Submitted,
            SubmittedAtUtc = DateTime.UtcNow,
            ProposalPayloadSummary = nodeId,
            ProposalPayload = new PlayerRequestProposalDraft
            {
                ProposalType = "development_purchase",
                DisplaySummary = "Покупка узла развития: " + SafeNodeName(node, includeAdmin: false),
                Parameters = new Dictionary<string, object> { { "characterId", c.Id }, { "nodeId", nodeId } },
                RequiresGMApproval = true
            }
        };
        _repositories.PlayerRequests.Insert(request);
        TryPublishDevelopmentSync(c, "development.purchaseRequest.changed", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Development purchase request submitted.", new Dictionary<string, object> { { "requestId", request.Id }, { "requestNumber", request.RequestNumber }, { "nodeId", nodeId } });
    }

    public ResponseEnvelope DevelopmentPurchaseRequestApprove(CommandContext context) => DevelopmentPurchaseRequestTransition(context, true);
    public ResponseEnvelope DevelopmentPurchaseRequestReject(CommandContext context) => DevelopmentPurchaseRequestTransition(context, false);

    private ResponseEnvelope DevelopmentXpAdjust(CommandContext context, string entryType, int amount)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        if (!DevelopmentExperienceCoinsEnabled()) return DevelopmentDisabled("Монеты опыта выключены feature flags.");
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressCollections(c);
        var reason = RequireLength(PayloadReader.GetString(context.Request.Payload, "reason"), 0, 500, "reason");
        var delta = entryType == ExperienceCoinLedgerEntryTypeIds.Correction ? amount : Math.Abs(amount);
        if (entryType == ExperienceCoinLedgerEntryTypeIds.Correction && c.XpCoins + delta < 0) throw new InvalidOperationException("XP correction would make balance negative.");
        c.XpCoins += delta;
        if (c.XpCoins < 0) c.XpCoins = 0;
        SyncExperienceCoinsProfile(c, actor.Id, context.Request.RequestId ?? string.Empty);
        _repositories.Characters.Replace(c);
        AddExperienceCoinLedger(c, actor.Id, entryType, delta, reason, string.Empty, true);
        WriteAudit("development", actor.Id, "xp." + entryType, c.Id + ":" + delta);
        TryPublishDevelopmentSync(c, "development.xp.changed", actor.Id, context.Request.RequestId ?? string.Empty);
        TryWriteDevelopmentJournal(c, actor.Id, "development.xp." + entryType, $"МО изменены: {delta}. Баланс: {c.XpCoins}");
        return Ok("Experience coins updated.", new Dictionary<string, object> { { "characterId", c.Id }, { "xpCoins", c.XpCoins } });
    }

    private ResponseEnvelope DevelopmentAdminStateStub(CommandContext context, string eventType, string message)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        var c = ResolveCharacterForClassSkill(context, actor);
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        WriteAudit("development", actor.Id, eventType, c.Id + ":" + nodeId);
        TryPublishDevelopmentSync(c, eventType, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok(message, new Dictionary<string, object> { { "characterId", c.Id }, { "nodeId", nodeId } });
    }

    private ResponseEnvelope DevelopmentPurchaseRequestTransition(CommandContext context, bool approve)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        var requestId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requestId"), 1, 128, "requestId");
        var request = _repositories.PlayerRequests.GetById(requestId) ?? throw new KeyNotFoundException("Request not found.");
        if (!string.Equals(request.ProposalType, "development_purchase", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Request is not a development purchase.");
        request.Status = approve ? PlayerRequestStatusIds.Approved : PlayerRequestStatusIds.Rejected;
        request.ReviewedByUserId = actor.Id;
        request.ReviewedByDisplayName = actor.Login;
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ResolvedAtUtc = DateTime.UtcNow;
        request.GMResponse = PayloadReader.GetString(context.Request.Payload, "gmResponse") ?? string.Empty;
        request.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.PlayerRequests.Replace(request);
        WriteAudit("development", actor.Id, approve ? "purchaseRequest.approve" : "purchaseRequest.reject", request.Id);
        return Ok(approve ? "Development purchase request approved." : "Development purchase request rejected.", new Dictionary<string, object> { { "requestId", request.Id }, { "status", request.Status } });
    }

    private bool DevelopmentBaseEnabled() => _featureFlags.IsEnabled(nameof(DevelopmentFeatureFlags.UseDevelopmentHexagonMvp));
    private bool DevelopmentPlayerEnabled() => DevelopmentBaseEnabled() && _featureFlags.IsEnabled(nameof(DevelopmentFeatureFlags.UseDevelopmentPlayerView));
    private bool DevelopmentAdminEnabled() => DevelopmentBaseEnabled() && _featureFlags.IsEnabled(nameof(DevelopmentFeatureFlags.UseDevelopmentAdminView));
    private bool DevelopmentNodePurchaseEnabled() => DevelopmentBaseEnabled() && _featureFlags.IsEnabled(nameof(DevelopmentFeatureFlags.UseDevelopmentNodePurchase));
    private bool DevelopmentExperienceCoinsEnabled() => DevelopmentBaseEnabled() && _featureFlags.IsEnabled(nameof(DevelopmentFeatureFlags.UseExperienceCoins));
    private bool DevelopmentMultiHexagonsEnabled() => DevelopmentBaseEnabled() && _featureFlags.IsEnabled(nameof(DevelopmentFeatureFlags.UseMultiDevelopmentHexagons));
    private bool DevelopmentMagicHexagonEnabled() => DevelopmentMultiHexagonsEnabled() && _featureFlags.IsEnabled(nameof(DevelopmentFeatureFlags.UseMagicDevelopmentHexagon));

    private bool DevelopmentPlayerOrAdminEnabled(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        return IsAdmin(actor) ? DevelopmentAdminEnabled() : DevelopmentPlayerEnabled();
    }

    private static ResponseEnvelope DevelopmentDisabled(string message = "Development Hexagon is disabled by feature flags.")
        => Error(message, ResponseStatus.Forbidden, ErrorCode.Forbidden);

    private sealed class DevelopmentLayoutUpdate
    {
        public string NodeId { get; set; } = string.Empty;
        public int PositionX { get; set; }
        public int PositionY { get; set; }
    }

    private Dictionary<string, object> DevelopmentHexagonLayoutPayload(string hexagonId, bool includeAdmin)
    {
        var effectiveHexagonId = string.IsNullOrWhiteSpace(hexagonId) ? DevelopmentHexagonIds.Main : hexagonId;
        var nodes = _nodesById.Values
            .Where(node => string.Equals(EffectiveHexagonId(node), effectiveHexagonId, StringComparison.OrdinalIgnoreCase))
            .Where(node => includeAdmin || !ShouldHideNodeFromPlayer(node))
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.GridY)
            .ThenBy(node => node.GridX)
            .ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .Select(node => includeAdmin ? NodePayload(node) : PlayerDevelopmentNodeLayoutPayload(node))
            .Cast<object>()
            .ToArray();

        var revision = CurrentDevelopmentLayoutRevision(effectiveHexagonId);
        var links = DevelopmentRequirementLinks(effectiveHexagonId, includeAdmin)
            .Cast<object>()
            .ToArray();
        var issues = includeAdmin ? ValidateDevelopmentGraph(effectiveHexagonId).Cast<object>().ToArray() : new object[0];

        return new Dictionary<string, object>
        {
            { "hexagonId", effectiveHexagonId },
            { "hexagon", DevelopmentHexagonPayload(effectiveHexagonId, includeAdmin) },
            { "hexagons", DevelopmentHexagonsPayload(includeAdmin).Cast<object>().ToArray() },
            { "items", nodes },
            { "nodes", nodes },
            { "links", links },
            { "requirementLinks", links },
            { "valid", issues.Length == 0 },
            { "issues", issues },
            { "layoutRevision", revision },
            { "layoutVersion", revision },
            { "sourceOfTruth", "class_tree_definitions" },
            { "isPlayerSafe", !includeAdmin },
            { "builtAtUtc", DateTime.UtcNow }
        };
    }

    private int CurrentDevelopmentLayoutRevision(string hexagonId)
    {
        var effectiveHexagonId = string.IsNullOrWhiteSpace(hexagonId) ? DevelopmentHexagonIds.Main : hexagonId;
        return _nodesById.Values
            .Where(node => string.Equals(EffectiveHexagonId(node), effectiveHexagonId, StringComparison.OrdinalIgnoreCase))
            .Select(node => Math.Max(1, node.LayoutVersion))
            .DefaultIfEmpty(1)
            .Max();
    }

    private Dictionary<string, object> PlayerDevelopmentNodeLayoutPayload(ClassNodeDefinition node)
    {
        var linkedClass = ResolveClassDefinitionForNode(node.NodeId);
        var canonicalDirectionId = CanonicalDevelopmentDirectionId(EffectiveHexagonId(node), node);
        return new Dictionary<string, object>
        {
            { "nodeId", node.NodeId },
            { "hexagonId", EffectiveHexagonId(node) },
            { "hexagonType", EffectiveHexagonType(node) },
            { "hexagonName", GetHexagonDisplayName(EffectiveHexagonId(node)) },
            { "name", FirstNonEmpty(node.PublicName, node.Name, node.NodeId) },
            { "description", FirstNonEmpty(node.PublicDescription, node.Description) },
            { "nodeType", node.NodeType },
            { "nodeRole", node.NodeRole },
            { "nodeTypeLabel", FormatDevelopmentNodeType(node) },
            { "linkedDefinitionKind", string.IsNullOrWhiteSpace(node.LinkedDefinitionKind) ? string.Empty : node.LinkedDefinitionKind },
            { "linkedDefinitionDisplayName", FirstNonEmpty(node.PublicName, linkedClass?.Name, node.Name, node.NodeId) },
            { "classId", linkedClass?.Code ?? node.ClassId ?? string.Empty },
            { "classCode", linkedClass?.Code ?? node.ClassId ?? string.Empty },
            { "directionId", node.DirectionId },
            { "directionCode", node.DirectionId },
            { "canonicalDirectionId", canonicalDirectionId },
            { "branchId", node.BranchId },
            { "branchCode", node.BranchId },
            { "canonicalBranchId", FirstNonEmpty(node.BranchId, canonicalDirectionId) },
            { "costExperienceCoins", Math.Max(0, node.CostExperienceCoins) },
            { "cost", Math.Max(0, node.CostExperienceCoins) },
            { "currencyId", FirstNonEmpty(node.CurrencyId, CharacterCurrencyIds.XpCoin) },
            { "costLabel", Math.Max(0, node.CostExperienceCoins) + " МО" },
            { "requirementSummary", FirstNonEmpty(node.RequirementSummary, FormatRequirements(node.Requirements)) },
            { "rewardSummary", FirstNonEmpty(node.RewardSummary, FormatRewards(node)) },
            { "gridX", node.GridX },
            { "gridY", node.GridY },
            { "positionX", node.GridX },
            { "positionY", node.GridY },
            { "ring", node.Ring },
            { "sector", node.Sector },
            { "sortOrder", node.SortOrder },
            { "layoutVersion", Math.Max(1, node.LayoutVersion) },
            { "updatedAtUtc", node.UpdatedAtUtc },
            { "updatedByUserId", string.Empty },
            { "schemaVersion", Math.Max(1, node.SchemaVersion) },
            { "isArchived", false },
            { "isPlayerVisible", true },
            { "isVisibleToPlayer", true },
            { "isGMOnly", false },
            { "requiredNodeIds", GetRequiredNodeIds(node).Cast<object>().ToArray() },
            { "linkedNodeIds", GetRequiredNodeIds(node).Cast<object>().ToArray() },
            { "linkedClassId", linkedClass?.Code ?? node.ClassId ?? string.Empty }
        };
    }

    private List<DevelopmentLayoutUpdate> ReadDevelopmentLayoutUpdates(IDictionary<string, object> payload, string hexagonId, bool requireNodes)
    {
        var rawNodes = PayloadReader.GetList(payload, "nodes") ?? PayloadReader.GetList(payload, "items") ?? new List<object>();
        if (requireNodes && rawNodes.Count == 0) throw new InvalidOperationException("nodes must contain at least one layout item.");
        var result = new List<DevelopmentLayoutUpdate>();
        foreach (var raw in rawNodes)
        {
            var map = ObjectToDictionary(raw);
            if (map == null) continue;
            var nodeId = RequireLength(FirstNonEmpty(PayloadReader.GetString(map, "nodeId"), PayloadReader.GetString(map, "id")), 1, 128, "nodeId");
            var x = ReadRequiredInt(map, "positionX", "gridX", 0);
            var y = ReadRequiredInt(map, "positionY", "gridY", 0);
            result.Add(new DevelopmentLayoutUpdate { NodeId = nodeId, PositionX = x, PositionY = y });
        }

        return result;
    }

    private List<string> ValidateDevelopmentLayoutUpdates(string hexagonId, IEnumerable<DevelopmentLayoutUpdate> updates)
    {
        var issues = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var update in updates)
        {
            if (!seen.Add(update.NodeId)) issues.Add("Duplicate node in layout batch: " + update.NodeId);
            if (!_nodesById.TryGetValue(update.NodeId, out var node))
            {
                issues.Add("Node not found: " + update.NodeId);
                continue;
            }

            if (!string.Equals(EffectiveHexagonId(node), hexagonId, StringComparison.OrdinalIgnoreCase))
                issues.Add("Node belongs to another development hexagon: " + update.NodeId);
            if (update.PositionX < 0 || update.PositionX > DevelopmentLayoutWorkspaceWidth)
                issues.Add("PositionX must be between 0 and " + DevelopmentLayoutWorkspaceWidth + " for " + update.NodeId);
            if (update.PositionY < 0 || update.PositionY > DevelopmentLayoutWorkspaceHeight)
                issues.Add("PositionY must be between 0 and " + DevelopmentLayoutWorkspaceHeight + " for " + update.NodeId);
        }

        return issues;
    }

    private static Dictionary<string, object>? ObjectToDictionary(object? value)
    {
        if (value == null) return null;
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                result[key] = entry.Value!;
            }

            return result;
        }

        return null;
    }

    private static Tuple<int, int> DefaultDevelopmentNodePosition(ClassNodeDefinition node)
    {
        var ring = Math.Max(0, node.Ring);
        var sector = node.Sector > 0 ? node.Sector : SectorFromDirection(node.DirectionId);
        if (string.Equals(node.NodeId, "novice", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.NodeId, "magic_awakened", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.NodeId, "large0154_root", StringComparison.OrdinalIgnoreCase) ||
            ring == 0)
            return Tuple.Create(
                ClampLayoutCoordinate(DevelopmentLayoutCenterX - DevelopmentLayoutNodeWidth / 2, 0, DevelopmentLayoutWorkspaceWidth - DevelopmentLayoutNodeWidth),
                ClampLayoutCoordinate(DevelopmentLayoutCenterY - DevelopmentLayoutNodeHeight / 2, 0, DevelopmentLayoutWorkspaceHeight - DevelopmentLayoutNodeHeight));

        var radius = Math.Min(2200, 320 + ring * 300);
        var angle = node.Angle;
        if (Math.Abs(angle) < 0.01)
            angle = (sector <= 0 ? 0 : (sector - 1) * 60) - 90;
        var radians = Math.PI * angle / 180.0;
        var x = (int)Math.Round(DevelopmentLayoutCenterX + Math.Cos(radians) * radius);
        var y = (int)Math.Round(DevelopmentLayoutCenterY + Math.Sin(radians) * radius);
        return Tuple.Create(
            ClampLayoutCoordinate(x, 0, DevelopmentLayoutWorkspaceWidth - DevelopmentLayoutNodeWidth),
            ClampLayoutCoordinate(y, 0, DevelopmentLayoutWorkspaceHeight - DevelopmentLayoutNodeHeight));
    }

    private Dictionary<string, Tuple<int, int>> CurrentDevelopmentLayoutPositions(string hexagonId)
    {
        var effectiveHexagonId = string.IsNullOrWhiteSpace(hexagonId) ? DevelopmentHexagonIds.Main : hexagonId;
        return _nodesById.Values
            .Where(node => string.Equals(EffectiveHexagonId(node), effectiveHexagonId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(node => node.NodeId, node => Tuple.Create(node.GridX, node.GridY), StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, Tuple<int, int>> BuildDevelopmentBaselineLayout(string hexagonId)
    {
        var effectiveHexagonId = string.IsNullOrWhiteSpace(hexagonId) ? DevelopmentHexagonIds.Main : hexagonId;
        var nodes = _nodesById.Values
            .Where(node => string.Equals(EffectiveHexagonId(node), effectiveHexagonId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new Dictionary<string, Tuple<int, int>>(StringComparer.OrdinalIgnoreCase);
        if (nodes.Count == 0) return result;
        if (IsDevelopmentLargeTestHexagon(effectiveHexagonId) && nodes.Count >= DevelopmentLargeTestMinimumWorkingNodes)
            return BuildDevelopmentLargeBaselineLayout(nodes);

        const int centerX = DevelopmentLayoutCenterX;
        const int centerY = DevelopmentLayoutCenterY;
        const int nodeWidth = DevelopmentLayoutNodeWidth;
        const int nodeHeight = DevelopmentLayoutNodeHeight;
        var layerByNode = ComputeDevelopmentLayoutLayers(nodes);
        var directionIndexByKey = BuildDevelopmentDirectionIndexMap(effectiveHexagonId, nodes);
        var orderedNodes = nodes
            .OrderBy(node => layerByNode.TryGetValue(node.NodeId, out var layer) ? layer : 1)
            .ThenBy(node => DevelopmentBranchKey(node), StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.SortOrder)
            .ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var node in orderedNodes)
        {
            if (node.LayoutLockedManualPosition)
            {
                result[node.NodeId] = Tuple.Create(
                    ClampLayoutCoordinate(node.GridX, 0, DevelopmentLayoutWorkspaceWidth - nodeWidth),
                    ClampLayoutCoordinate(node.GridY, 0, DevelopmentLayoutWorkspaceHeight - nodeHeight));
                continue;
            }

            var layer = Math.Max(0, Math.Min(4, layerByNode.TryGetValue(node.NodeId, out var knownLayer) ? knownLayer : Math.Max(1, node.Ring)));
            var isDiagnosticLayoutNode = IsDevelopmentDiagnosticLayoutNode(node);
            if (isDiagnosticLayoutNode)
                layer = Math.Max(layer, 5);
            if (IsDevelopmentRootNode(node))
            {
                result[node.NodeId] = Tuple.Create(centerX - nodeWidth / 2, centerY - nodeHeight / 2);
                continue;
            }

            var branchKey = CanonicalDevelopmentDirectionId(effectiveHexagonId, node);
            if (!directionIndexByKey.TryGetValue(branchKey, out var directionIndex))
                directionIndex = StableDevelopmentDirectionIndex(branchKey);
            if (isDiagnosticLayoutNode)
                directionIndex = 4;
            var angleDegrees = DevelopmentLayoutAngleDegrees(directionIndex);
            var radians = angleDegrees * Math.PI / 180.0;
            var radius = isDiagnosticLayoutNode ? 4300 + layer * 260 : 640 + Math.Max(0, layer - 1) * 390;
            var sameBranchLayerIndex = orderedNodes
                .Where(other => !string.Equals(other.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase))
                .Where(other => string.Equals(DevelopmentBranchKey(other), branchKey, StringComparison.OrdinalIgnoreCase))
                .Where(other => (layerByNode.TryGetValue(other.NodeId, out var otherLayer) ? Math.Max(0, Math.Min(4, otherLayer)) : Math.Max(1, other.Ring)) == layer)
                .Where(other => string.Compare(other.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase) < 0)
                .Count();
            var sameBranchLayerCount = orderedNodes
                .Where(other => string.Equals(DevelopmentBranchKey(other), branchKey, StringComparison.OrdinalIgnoreCase))
                .Where(other => (layerByNode.TryGetValue(other.NodeId, out var otherLayer) ? Math.Max(0, Math.Min(4, otherLayer)) : Math.Max(1, other.Ring)) == layer)
                .Count();
            var perpendicular = sameBranchLayerCount <= 1 ? 0 : (sameBranchLayerIndex - (sameBranchLayerCount - 1) / 2.0) * (isDiagnosticLayoutNode ? 190 : 130);
            var normalX = Math.Cos(radians);
            var normalY = Math.Sin(radians);
            var tangentX = -normalY;
            var tangentY = normalX;
            var x = centerX + normalX * radius + tangentX * perpendicular - nodeWidth / 2.0;
            var y = centerY + normalY * radius + tangentY * perpendicular - nodeHeight / 2.0;
            result[node.NodeId] = Tuple.Create(
                ClampLayoutCoordinate((int)Math.Round(x), 0, DevelopmentLayoutWorkspaceWidth - nodeWidth),
                ClampLayoutCoordinate((int)Math.Round(y), 0, DevelopmentLayoutWorkspaceHeight - nodeHeight));
        }

        ResolveDevelopmentLayoutCollisions(result, nodes);
        NormalizeDevelopmentWorkingLayoutAspect(result, nodes, effectiveHexagonId);
        ResolveDevelopmentLayoutCollisions(result, nodes);
        return result;
    }

    private static Dictionary<string, Tuple<int, int>> BuildDevelopmentLargeBaselineLayout(List<ClassNodeDefinition> nodes)
    {
        const int nodeWidth = DevelopmentLayoutNodeWidth;
        const int nodeHeight = DevelopmentLayoutNodeHeight;
        const int centerX = DevelopmentLayoutCenterX;
        const int centerY = DevelopmentLayoutCenterY;
        const int siblingsPerColumn = 4;
        const double baseDistance = 820;
        const double levelDistance = 850;
        const double columnDistance = 150;
        const double siblingSpacing = 225;

        var result = new Dictionary<string, Tuple<int, int>>(StringComparer.OrdinalIgnoreCase);
        var root = nodes.FirstOrDefault(node => string.Equals(node.NodeId, "large0154_root", StringComparison.OrdinalIgnoreCase)) ??
                   nodes.FirstOrDefault(IsDevelopmentRootNode);
        if (root != null)
        {
            result[root.NodeId] = Tuple.Create(
                ClampLayoutCoordinate(DevelopmentLayoutCenterX - nodeWidth / 2, 0, DevelopmentLayoutWorkspaceWidth - nodeWidth),
                ClampLayoutCoordinate(DevelopmentLayoutCenterY - nodeHeight / 2, 0, DevelopmentLayoutWorkspaceHeight - nodeHeight));
        }

        var branchOrder = nodes
            .Where(node => node != root)
            .Select(node => FirstNonEmpty(node.BranchId, node.DirectionId, "large0154_branch_01"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (branchOrder.Count == 0) branchOrder.Add("large0154_branch_01");
        var branchIndex = branchOrder
            .Select((branch, index) => new { branch, index })
            .ToDictionary(x => x.branch, x => x.index, StringComparer.OrdinalIgnoreCase);

        var branchLocalCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes
            .Where(node => node != root)
            .OrderBy(node => FirstNonEmpty(node.BranchId, node.DirectionId, "large0154_branch_01"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Ring)
            .ThenBy(node => node.SortOrder)
            .ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase))
        {
            if (node.LayoutLockedManualPosition)
            {
                result[node.NodeId] = Tuple.Create(
                    ClampLayoutCoordinate(node.GridX, 0, DevelopmentLayoutWorkspaceWidth - nodeWidth),
                    ClampLayoutCoordinate(node.GridY, 0, DevelopmentLayoutWorkspaceHeight - nodeHeight));
                continue;
            }

            var branch = FirstNonEmpty(node.BranchId, node.DirectionId, "large0154_branch_01");
            if (!branchIndex.TryGetValue(branch, out var index)) index = 0;
            branchLocalCounters.TryGetValue(branch, out var localIndex);
            branchLocalCounters[branch] = localIndex + 1;

            var layer = Math.Max(1, node.LayoutLayer > 0 ? node.LayoutLayer : node.Ring > 0 ? node.Ring : 1);
            var sameBranchLayerIndex = nodes
                .Where(other => other != root)
                .Where(other => string.Equals(FirstNonEmpty(other.BranchId, other.DirectionId, "large0154_branch_01"), branch, StringComparison.OrdinalIgnoreCase))
                .Where(other => Math.Max(1, other.LayoutLayer > 0 ? other.LayoutLayer : other.Ring > 0 ? other.Ring : 1) == layer)
                .OrderBy(other => other.SortOrder)
                .ThenBy(other => other.NodeId, StringComparer.OrdinalIgnoreCase)
                .Select((other, local) => new { other.NodeId, local })
                .FirstOrDefault(other => string.Equals(other.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase))?.local ?? localIndex;
            var column = sameBranchLayerIndex / siblingsPerColumn;
            var row = sameBranchLayerIndex % siblingsPerColumn;
            var rowCenter = (Math.Min(siblingsPerColumn, Math.Max(1, nodes.Count(other =>
                other != root
                && string.Equals(FirstNonEmpty(other.BranchId, other.DirectionId, "large0154_branch_01"), branch, StringComparison.OrdinalIgnoreCase)
                && Math.Max(1, other.LayoutLayer > 0 ? other.LayoutLayer : other.Ring > 0 ? other.Ring : 1) == layer))) - 1) / 2.0;
            var angle = DevelopmentLayoutAngleDegrees(index % 6);
            var radians = angle * Math.PI / 180.0;
            var normalX = Math.Cos(radians);
            var normalY = Math.Sin(radians);
            var tangentX = -normalY;
            var tangentY = normalX;
            var distance = baseDistance + (layer - 1) * levelDistance + column * columnDistance;
            var perpendicular = (row - rowCenter) * siblingSpacing;
            var x = (int)Math.Round(centerX + normalX * distance + tangentX * perpendicular - nodeWidth / 2.0);
            var y = (int)Math.Round(centerY + normalY * distance + tangentY * perpendicular - nodeHeight / 2.0);
            result[node.NodeId] = Tuple.Create(
                ClampLayoutCoordinate(x, 0, DevelopmentLayoutWorkspaceWidth - nodeWidth),
                ClampLayoutCoordinate(y, 0, DevelopmentLayoutWorkspaceHeight - nodeHeight));
        }

        ResolveDevelopmentLayoutCollisions(result, nodes);
        return result;
    }

    private static Dictionary<string, int> ComputeDevelopmentLayoutLayers(List<ClassNodeDefinition> nodes)
    {
        var byId = nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in nodes.Where(IsDevelopmentRootNode))
            result[root.NodeId] = 0;

        for (var pass = 0; pass < Math.Max(2, nodes.Count); pass++)
        {
            var changed = false;
            foreach (var node in nodes.OrderBy(n => n.SortOrder).ThenBy(n => n.NodeId, StringComparer.OrdinalIgnoreCase))
            {
                if (result.ContainsKey(node.NodeId)) continue;
                var required = GetRequiredNodeIds(node).Where(byId.ContainsKey).ToList();
                var layer = required.Count == 0 ? Math.Max(1, Math.Min(4, node.Ring <= 0 ? node.Tier : node.Ring)) : 0;
                if (required.Count > 0)
                {
                    var known = required.Where(result.ContainsKey).Select(id => result[id]).ToList();
                    if (known.Count == 0) continue;
                    layer = Math.Min(4, known.Max() + 1);
                }

                result[node.NodeId] = Math.Max(0, layer);
                changed = true;
            }

            if (!changed) break;
        }

        foreach (var node in nodes)
        {
            if (!result.ContainsKey(node.NodeId))
                result[node.NodeId] = Math.Max(1, Math.Min(4, node.Ring <= 0 ? node.Tier : node.Ring));
        }

        return result;
    }

    private static Dictionary<string, int> BuildDevelopmentDirectionIndexMap(string hexagonId, List<ClassNodeDefinition> nodes)
    {
        var known = CanonicalDevelopmentDirectionIds(hexagonId).Concat(new[] { "root", "magic_root", "large0154_root" }).ToArray();
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < known.Length; index++)
            map[known[index]] = index % 6;

        foreach (var key in nodes.Select(DevelopmentBranchKey).Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!map.ContainsKey(key))
                map[key] = StableDevelopmentDirectionIndex(key);
        }

        return map;
    }

    private static string DevelopmentBranchKey(ClassNodeDefinition node)
        => IsDevelopmentDiagnosticLayoutNode(node)
            ? FirstNonEmpty(node.BranchId, node.DirectionId, "diagnostic_hidden")
            : FirstNonEmpty(node.DirectionId, node.BranchId, node.NodeRole, node.NodeType, "root");

    private static string CanonicalDevelopmentDirectionId(string hexagonId, ClassNodeDefinition node)
    {
        if (node == null || IsDevelopmentRootNode(node)) return "root";
        var expected = CanonicalDevelopmentDirectionIds(hexagonId);
        if (expected.Length == 0) return DevelopmentBranchKey(node);
        var direction = FirstNonEmpty(node.DirectionId, node.BranchId);
        if (expected.Any(id => string.Equals(id, direction, StringComparison.OrdinalIgnoreCase)))
            return direction;
        if (IsDevelopmentLargeTestHexagon(hexagonId))
        {
            var branch = FirstNonEmpty(node.BranchId, node.DirectionId);
            if (expected.Any(id => string.Equals(id, branch, StringComparison.OrdinalIgnoreCase)))
                return branch;
        }

        var key = FirstNonEmpty(node.NodeId, node.ClassId, node.LinkedDefinitionId, node.BranchId, node.DirectionId, node.NodeRole, node.NodeType);
        return expected[StableDevelopmentDirectionIndex(key) % expected.Length];
    }

    private static string[] CanonicalDevelopmentDirectionIds(string hexagonId)
    {
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
            return new[] { "magic_methods", "magic_element_water", "magic_element_earth", "magic_element_fire", "magic_element_air", "magic_special" };
        if (IsDevelopmentLargeTestHexagon(hexagonId))
            return new[] { "large0154_branch_01", "large0154_branch_02", "large0154_branch_03", "large0154_branch_04", "large0154_branch_05", "large0154_branch_06" };
        return new[] { DevelopmentDirectionIds.StrengthAssault, DevelopmentDirectionIds.DexterityManeuver, DevelopmentDirectionIds.EnduranceResilience, DevelopmentDirectionIds.IntellectReason, DevelopmentDirectionIds.WisdomPath, DevelopmentDirectionIds.CharismaInfluence };
    }

    private static int StableDevelopmentDirectionIndex(string key)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in (key ?? string.Empty).ToLowerInvariant())
                hash = hash * 31 + ch;
            return Math.Abs(hash) % 6;
        }
    }

    private static double DevelopmentLayoutAngleDegrees(int directionIndex)
    {
        return directionIndex switch
        {
            0 => -90,
            1 => -30,
            2 => 30,
            3 => 90,
            4 => 150,
            _ => -150
        };
    }

    private static bool IsDevelopmentRootNode(ClassNodeDefinition node)
        => string.Equals(node.NodeId, "novice", StringComparison.OrdinalIgnoreCase)
           || string.Equals(node.NodeId, "magic_awakened", StringComparison.OrdinalIgnoreCase)
           || string.Equals(node.NodeRole, DevelopmentNodeRoleIds.NoviceRoot, StringComparison.OrdinalIgnoreCase)
           || string.Equals(node.NodeRole, DevelopmentNodeRoleIds.MagicRoot, StringComparison.OrdinalIgnoreCase)
           || node.Ring == 0;

    private static void ResolveDevelopmentLayoutCollisions(Dictionary<string, Tuple<int, int>> positions, List<ClassNodeDefinition> nodes)
    {
        const int nodeWidth = DevelopmentLayoutNodeWidth;
        const int nodeHeight = DevelopmentLayoutNodeHeight;
        const int margin = 32;
        var ordered = nodes.OrderBy(n => IsDevelopmentRootNode(n) ? 0 : 1).ThenBy(n => n.SortOrder).ThenBy(n => n.NodeId, StringComparer.OrdinalIgnoreCase).ToList();
        for (var pass = 0; pass < 96; pass++)
        {
            var changed = false;
            for (var i = 0; i < ordered.Count; i++)
            {
                var a = ordered[i];
                if (!positions.TryGetValue(a.NodeId, out var pa)) continue;
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    var b = ordered[j];
                    if (b.LayoutLockedManualPosition || !positions.TryGetValue(b.NodeId, out var pb)) continue;
                    if (!DevelopmentLayoutRectanglesOverlap(pa, pb, nodeWidth + margin, nodeHeight + margin)) continue;

                    var deltaX = (pb.Item1 + nodeWidth / 2.0) - (pa.Item1 + nodeWidth / 2.0);
                    var deltaY = (pb.Item2 + nodeHeight / 2.0) - (pa.Item2 + nodeHeight / 2.0);
                    if (Math.Abs(deltaX) < 0.01 && Math.Abs(deltaY) < 0.01)
                    {
                        deltaX = ((pass + j) % 2 == 0) ? 1 : -1;
                        deltaY = ((pass + i) % 2 == 0) ? 1 : -1;
                    }

                    var overlapX = nodeWidth + margin - Math.Abs(deltaX);
                    var overlapY = nodeHeight + margin - Math.Abs(deltaY);
                    var pushX = 0;
                    var pushY = 0;
                    if (overlapX <= overlapY)
                        pushX = (int)Math.Ceiling(Math.Max(48, overlapX + margin)) * (deltaX >= 0 ? 1 : -1);
                    else
                        pushY = (int)Math.Ceiling(Math.Max(44, overlapY + margin)) * (deltaY >= 0 ? 1 : -1);

                    positions[b.NodeId] = Tuple.Create(
                        ClampLayoutCoordinate(pb.Item1 + pushX, 0, DevelopmentLayoutWorkspaceWidth - nodeWidth),
                        ClampLayoutCoordinate(pb.Item2 + pushY, 0, DevelopmentLayoutWorkspaceHeight - nodeHeight));
                    changed = true;
                }
            }

            if (!changed) break;
        }

        foreach (var node in ordered.Where(n => !IsDevelopmentRootNode(n) && !n.LayoutLockedManualPosition))
        {
            if (!positions.TryGetValue(node.NodeId, out var current)) continue;
            if (!DevelopmentLayoutPositionOverlapsAny(node.NodeId, current, positions, nodeWidth + margin, nodeHeight + margin))
                continue;

            var best = current;
            var found = false;
            var stepX = nodeWidth + margin;
            var stepY = nodeHeight + margin;
            for (var radius = 1; radius <= 10 && !found; radius++)
            {
                foreach (var candidate in DevelopmentLayoutCollisionFallbackCandidates(current, stepX, stepY, radius))
                {
                    var clamped = Tuple.Create(
                        ClampLayoutCoordinate(candidate.Item1, 0, DevelopmentLayoutWorkspaceWidth - nodeWidth),
                        ClampLayoutCoordinate(candidate.Item2, 0, DevelopmentLayoutWorkspaceHeight - nodeHeight));
                    if (DevelopmentLayoutPositionOverlapsAny(node.NodeId, clamped, positions, nodeWidth + margin, nodeHeight + margin))
                        continue;
                    best = clamped;
                    found = true;
                    break;
                }
            }

            if (found) positions[node.NodeId] = best;
        }
    }

    private static void NormalizeDevelopmentWorkingLayoutAspect(Dictionary<string, Tuple<int, int>> positions, List<ClassNodeDefinition> nodes, string hexagonId)
    {
        const int nodeWidth = DevelopmentLayoutNodeWidth;
        const int nodeHeight = DevelopmentLayoutNodeHeight;
        const int targetMinWidth = 1280;
        const double minimumAspect = 1.75;
        if (IsDevelopmentLargeTestHexagon(hexagonId)) return;

        var visibleWorkingNodes = nodes
            .Where(node => node != null)
            .Where(node => positions.ContainsKey(node.NodeId))
            .Where(node => !IsDevelopmentDiagnosticLayoutNode(node))
            .Where(node => !node.IsArchived && !node.IsHidden && node.IsPlayerVisible && !node.IsGMOnly)
            .OrderBy(node => IsDevelopmentRootNode(node) ? 0 : 1)
            .ThenBy(node => node.LayoutLayer)
            .ThenBy(node => node.Ring)
            .ThenBy(node => node.SortOrder)
            .ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (visibleWorkingNodes.Count < 4) return;

        var minX = visibleWorkingNodes.Min(node => positions[node.NodeId].Item1);
        var maxX = visibleWorkingNodes.Max(node => positions[node.NodeId].Item1);
        var minY = visibleWorkingNodes.Min(node => positions[node.NodeId].Item2);
        var maxY = visibleWorkingNodes.Max(node => positions[node.NodeId].Item2);
        var width = Math.Max(1, maxX - minX);
        var height = Math.Max(1, maxY - minY);
        if (width >= targetMinWidth && width / (double)height >= minimumAspect) return;
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Main, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
            return;

        var movableNodes = visibleWorkingNodes
            .Where(node => !node.LayoutLockedManualPosition && !IsDevelopmentRootNode(node))
            .ToList();
        if (movableNodes.Count == 0) return;

        var root = visibleWorkingNodes.FirstOrDefault(IsDevelopmentRootNode);
        if (root != null && !root.LayoutLockedManualPosition)
        {
            positions[root.NodeId] = Tuple.Create(
                ClampLayoutCoordinate(DevelopmentLayoutCenterX - nodeWidth / 2, 0, DevelopmentLayoutWorkspaceWidth - nodeWidth),
                ClampLayoutCoordinate(DevelopmentLayoutCenterY - nodeHeight / 2, 0, DevelopmentLayoutWorkspaceHeight - nodeHeight));
        }

        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
        {
            var radiusX = Math.Min(1500, Math.Max(920, movableNodes.Count * 170));
            var radiusY = 430;
            for (var index = 0; index < movableNodes.Count; index++)
            {
                var angle = -150.0 + index * (300.0 / Math.Max(1, movableNodes.Count - 1));
                var radians = Math.PI * angle / 180.0;
                var x = (int)Math.Round(DevelopmentLayoutCenterX + Math.Cos(radians) * radiusX - nodeWidth / 2.0);
                var y = (int)Math.Round(DevelopmentLayoutCenterY + Math.Sin(radians) * radiusY - nodeHeight / 2.0);
                positions[movableNodes[index].NodeId] = Tuple.Create(
                    ClampLayoutCoordinate(x, 0, DevelopmentLayoutWorkspaceWidth - nodeWidth),
                    ClampLayoutCoordinate(y, 0, DevelopmentLayoutWorkspaceHeight - nodeHeight));
            }

            return;
        }

        var rows = movableNodes.Count <= 4 ? 1 : 2;
        var columns = Math.Max(1, (int)Math.Ceiling(movableNodes.Count / (double)rows));
        var targetWidth = Math.Min(1900, Math.Max(1400, columns * 430));
        var topY = rows == 1 ? DevelopmentLayoutCenterY - 260 : DevelopmentLayoutCenterY - 260;
        var bottomY = DevelopmentLayoutCenterY + 260;

        for (var index = 0; index < movableNodes.Count; index++)
        {
            var row = rows == 1 ? 0 : index / columns;
            var column = index % columns;
            var xOffset = columns <= 1 ? 0 : -targetWidth / 2.0 + column * (targetWidth / (double)(columns - 1));
            var yCenter = rows == 1 ? topY : (row == 0 ? topY : bottomY);
            var x = (int)Math.Round(DevelopmentLayoutCenterX + xOffset - nodeWidth / 2.0);
            var y = (int)Math.Round(yCenter - nodeHeight / 2.0);
            positions[movableNodes[index].NodeId] = Tuple.Create(
                ClampLayoutCoordinate(x, 0, DevelopmentLayoutWorkspaceWidth - nodeWidth),
                ClampLayoutCoordinate(y, 0, DevelopmentLayoutWorkspaceHeight - nodeHeight));
        }
    }

    private static IEnumerable<Tuple<int, int>> DevelopmentLayoutCollisionFallbackCandidates(Tuple<int, int> origin, int stepX, int stepY, int radius)
    {
        yield return Tuple.Create(origin.Item1 + stepX * radius, origin.Item2);
        yield return Tuple.Create(origin.Item1 - stepX * radius, origin.Item2);
        yield return Tuple.Create(origin.Item1, origin.Item2 + stepY * radius);
        yield return Tuple.Create(origin.Item1, origin.Item2 - stepY * radius);
        yield return Tuple.Create(origin.Item1 + stepX * radius, origin.Item2 + stepY * radius);
        yield return Tuple.Create(origin.Item1 - stepX * radius, origin.Item2 + stepY * radius);
        yield return Tuple.Create(origin.Item1 + stepX * radius, origin.Item2 - stepY * radius);
        yield return Tuple.Create(origin.Item1 - stepX * radius, origin.Item2 - stepY * radius);
    }

    private static bool DevelopmentLayoutPositionOverlapsAny(string nodeId, Tuple<int, int> position, Dictionary<string, Tuple<int, int>> positions, int width, int height)
    {
        foreach (var pair in positions)
        {
            if (string.Equals(pair.Key, nodeId, StringComparison.OrdinalIgnoreCase)) continue;
            if (DevelopmentLayoutRectanglesOverlap(position, pair.Value, width, height))
                return true;
        }

        return false;
    }

    private static bool DevelopmentLayoutRectanglesOverlap(Tuple<int, int> a, Tuple<int, int> b, int width, int height)
        => a.Item1 < b.Item1 + width && a.Item1 + width > b.Item1 && a.Item2 < b.Item2 + height && a.Item2 + height > b.Item2;

    private static int ClampLayoutCoordinate(int value, int min, int max)
        => Math.Max(min, Math.Min(max, value));

    private Dictionary<string, object> DevelopmentHexagonLayoutPayloadWithPositions(string hexagonId, bool includeAdmin, Dictionary<string, Tuple<int, int>> positions)
    {
        var payload = DevelopmentHexagonLayoutPayload(hexagonId, includeAdmin);
        var effectiveHexagonId = string.IsNullOrWhiteSpace(hexagonId) ? DevelopmentHexagonIds.Main : hexagonId;
        var nodes = _nodesById.Values
            .Where(node => string.Equals(EffectiveHexagonId(node), effectiveHexagonId, StringComparison.OrdinalIgnoreCase))
            .Where(node => includeAdmin || !ShouldHideNodeFromPlayer(node))
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => positions.TryGetValue(node.NodeId, out var p) ? p.Item2 : node.GridY)
            .ThenBy(node => positions.TryGetValue(node.NodeId, out var p) ? p.Item1 : node.GridX)
            .ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .Select(node => (object)NodePayloadWithLayoutPosition(node, positions.TryGetValue(node.NodeId, out var p) ? p : Tuple.Create(node.GridX, node.GridY)))
            .ToArray();
        payload["items"] = nodes;
        payload["nodes"] = nodes;
        payload["qualityReport"] = BuildDevelopmentLayoutQualityReport(effectiveHexagonId, positions, "preview");
        return payload;
    }

    private Dictionary<string, object> NodePayloadWithLayoutPosition(ClassNodeDefinition node, Tuple<int, int> position)
    {
        var payload = NodePayload(node);
        payload["gridX"] = position.Item1;
        payload["gridY"] = position.Item2;
        payload["positionX"] = position.Item1;
        payload["positionY"] = position.Item2;
        payload["layoutGroup"] = node.LayoutGroup ?? string.Empty;
        payload["layoutLayer"] = node.LayoutLayer;
        payload["layoutBranch"] = node.LayoutBranch ?? string.Empty;
        payload["layoutWeight"] = node.LayoutWeight;
        payload["layoutGeneratedBy"] = node.LayoutGeneratedBy ?? string.Empty;
        payload["layoutGeneratedAtUtc"] = node.LayoutGeneratedAtUtc;
        payload["layoutLockedManualPosition"] = node.LayoutLockedManualPosition;
        payload["layoutPresetId"] = node.LayoutPresetId ?? string.Empty;
        payload["layoutSnapshotId"] = node.LayoutSnapshotId ?? string.Empty;
        return payload;
    }

    private int CountChangedDevelopmentLayoutPositions(Dictionary<string, Tuple<int, int>> before, Dictionary<string, Tuple<int, int>> after)
    {
        return after.Count(pair => before.TryGetValue(pair.Key, out var oldPosition) && (oldPosition.Item1 != pair.Value.Item1 || oldPosition.Item2 != pair.Value.Item2));
    }

    private int ApplyDevelopmentLayoutPositions(string hexagonId, Dictionary<string, Tuple<int, int>> positions, string actorId, string generatedBy, string presetId)
    {
        var changed = 0;
        foreach (var pair in positions)
        {
            if (!_nodesById.TryGetValue(pair.Key, out var node)) continue;
            if (!string.Equals(EffectiveHexagonId(node), hexagonId, StringComparison.OrdinalIgnoreCase)) continue;
            if (node.LayoutLockedManualPosition) continue;
            if (node.GridX == pair.Value.Item1 && node.GridY == pair.Value.Item2 && string.Equals(node.LayoutGeneratedBy, generatedBy, StringComparison.OrdinalIgnoreCase)) continue;

            node.GridX = pair.Value.Item1;
            node.GridY = pair.Value.Item2;
            node.LayoutGroup = EffectiveHexagonId(node);
            node.LayoutLayer = Math.Max(0, node.Ring);
            node.LayoutBranch = DevelopmentBranchKey(node);
            node.LayoutWeight = Math.Max(0, node.SortOrder);
            node.LayoutGeneratedBy = generatedBy;
            node.LayoutGeneratedAtUtc = DateTime.UtcNow;
            node.LayoutPresetId = presetId;
            node.LayoutVersion = Math.Max(1, node.LayoutVersion) + 1;
            node.Revision = Math.Max(1, node.Revision) + 1;
            node.UpdatedAtUtc = DateTime.UtcNow;
            node.UpdatedByUserId = actorId;
            node.SchemaVersion = Math.Max(1, node.SchemaVersion);
            PersistDevelopmentNodeDefinition(node);
            changed++;
        }

        return changed;
    }

    private string CreateDevelopmentLayoutSnapshot(string hexagonId, string actorId)
    {
        var snapshotId = "layout_snapshot_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        foreach (var node in _nodesById.Values.Where(n => string.Equals(EffectiveHexagonId(n), hexagonId, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            node.LayoutSnapshotId = snapshotId;
            node.LayoutSnapshotPositionX = node.GridX;
            node.LayoutSnapshotPositionY = node.GridY;
            node.LayoutSnapshotCreatedAtUtc = DateTime.UtcNow;
            node.UpdatedAtUtc = DateTime.UtcNow;
            node.UpdatedByUserId = actorId;
            PersistDevelopmentNodeDefinition(node);
        }

        return snapshotId;
    }

    private int RestoreDevelopmentLayoutSnapshot(string hexagonId, string actorId)
    {
        var changed = 0;
        var nodes = _nodesById.Values
            .Where(n => string.Equals(EffectiveHexagonId(n), hexagonId, StringComparison.OrdinalIgnoreCase))
            .Where(n => !string.IsNullOrWhiteSpace(n.LayoutSnapshotId))
            .ToList();
        if (nodes.Count == 0)
            throw new InvalidOperationException("Layout snapshot is not available for this hexagon.");

        foreach (var node in nodes)
        {
            var x = ClampLayoutCoordinate(node.LayoutSnapshotPositionX, 0, DevelopmentLayoutWorkspaceWidth - DevelopmentLayoutNodeWidth);
            var y = ClampLayoutCoordinate(node.LayoutSnapshotPositionY, 0, DevelopmentLayoutWorkspaceHeight - DevelopmentLayoutNodeHeight);
            if (node.GridX == x && node.GridY == y) continue;
            node.GridX = x;
            node.GridY = y;
            node.LayoutGeneratedBy = "snapshot_restore_0_15_4";
            node.LayoutGeneratedAtUtc = DateTime.UtcNow;
            node.LayoutVersion = Math.Max(1, node.LayoutVersion) + 1;
            node.Revision = Math.Max(1, node.Revision) + 1;
            node.UpdatedAtUtc = DateTime.UtcNow;
            node.UpdatedByUserId = actorId;
            PersistDevelopmentNodeDefinition(node);
            changed++;
        }

        return changed;
    }

    private Dictionary<string, object> BuildDevelopmentLayoutQualityReport(string hexagonId, Dictionary<string, Tuple<int, int>> positions, string phase)
    {
        const int nodeWidth = DevelopmentLayoutNodeWidth;
        const int nodeHeight = DevelopmentLayoutNodeHeight;
        var effectiveHexagonId = string.IsNullOrWhiteSpace(hexagonId) ? DevelopmentHexagonIds.Main : hexagonId;
        var allNodes = _nodesById.Values
            .Where(node => string.Equals(EffectiveHexagonId(node), effectiveHexagonId, StringComparison.OrdinalIgnoreCase))
            .Where(node => !node.IsArchived)
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var diagnosticNodes = allNodes.Where(IsDevelopmentDiagnosticLayoutNode).ToList();
        var nodes = allNodes.Where(node => !IsDevelopmentDiagnosticLayoutNode(node)).ToList();
        var normalNodeIds = new HashSet<string>(nodes.Select(node => node.NodeId), StringComparer.OrdinalIgnoreCase);
        var links = DevelopmentRequirementLinks(effectiveHexagonId, includeAdmin: true)
            .Select(link => Tuple.Create(Convert.ToString(link["sourceNodeId"]) ?? string.Empty, Convert.ToString(link["targetNodeId"]) ?? string.Empty))
            .Where(link => positions.ContainsKey(link.Item1) && positions.ContainsKey(link.Item2))
            .Where(link => normalNodeIds.Contains(link.Item1) && normalNodeIds.Contains(link.Item2))
            .ToList();

        var overlapCount = 0;
        var offscreenCount = 0;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (!positions.TryGetValue(nodes[i].NodeId, out var a)) continue;
            if (a.Item1 < 0 || a.Item2 < 0 || a.Item1 + nodeWidth > DevelopmentLayoutWorkspaceWidth || a.Item2 + nodeHeight > DevelopmentLayoutWorkspaceHeight)
                offscreenCount++;
            for (var j = i + 1; j < nodes.Count; j++)
            {
                if (!positions.TryGetValue(nodes[j].NodeId, out var b)) continue;
                if (DevelopmentLayoutRectanglesOverlap(a, b, nodeWidth, nodeHeight))
                    overlapCount++;
            }
        }

        var crossingEstimate = EstimateDevelopmentLinkCrossings(links, positions, nodeWidth, nodeHeight);
        var linkLengths = links.Select(link => DevelopmentLayoutLinkLength(link.Item1, link.Item2, positions, nodeWidth, nodeHeight)).ToList();
        var averageLinkLength = linkLengths.Count == 0 ? 0.0 : linkLengths.Average();
        var maxLinkLength = linkLengths.Count == 0 ? 0.0 : linkLengths.Max();
        var hiddenTestNodes = diagnosticNodes.Count;
        var visualDensityScore = Math.Max(0, Math.Min(100, 100.0 - nodes.Count / 1.8 - crossingEstimate * 0.25));
        var spiderwebScore = Math.Max(0, Math.Min(100, 100.0 - crossingEstimate * 0.9 - Math.Max(0, averageLinkLength - 1200.0) / 35.0));
        var labelClutterScore = Math.Max(0, Math.Min(100, 100.0 - Math.Max(0, nodes.Count - 28) * 2.2));
        var branchSeparationScore = Math.Max(0, Math.Min(100, 100.0 - overlapCount * 16.0 - crossingEstimate * 0.2));
        var score = 100.0
            - overlapCount * 14.0
            - offscreenCount * 12.0
            - crossingEstimate * 0.25
            - Math.Abs(averageLinkLength - 1500.0) / 90.0;
        score = Math.Max(0, Math.Min(100, score));
        var findings = new List<object>();
        if (overlapCount > 0) findings.Add("Есть пересечения карточек: " + overlapCount);
        if (offscreenCount > 0) findings.Add("Есть узлы за пределами рабочей области: " + offscreenCount);
        if (hiddenTestNodes > 0) findings.Add("Диагностические performance-узлы отделены от оценки рабочей раскладки: " + hiddenTestNodes);
        if (findings.Count == 0) findings.Add("Критичных проблем читаемости не найдено.");

        return new Dictionary<string, object>
        {
            { "treeId", effectiveHexagonId },
            { "phase", phase },
            { "nodeCount", nodes.Count },
            { "linkCount", links.Count },
            { "overlapCount", overlapCount },
            { "offscreenNodeCount", offscreenCount },
            { "crossingEstimate", crossingEstimate },
            { "averageLinkLength", Math.Round(averageLinkLength, 2) },
            { "maxLinkLength", Math.Round(maxLinkLength, 2) },
            { "visualDensityScore", Math.Round(visualDensityScore, 2) },
            { "spiderwebScore", Math.Round(spiderwebScore, 2) },
            { "labelClutterScore", Math.Round(labelClutterScore, 2) },
            { "branchSeparationScore", Math.Round(branchSeparationScore, 2) },
            { "anchorNodeCount", nodes.Count(IsDevelopmentRootNode) },
            { "manuallyLockedNodesPreserved", nodes.Count(n => n.LayoutLockedManualPosition) },
            { "hiddenTestNodesInNormalView", 0 },
            { "diagnosticNodesVisibleInNormalAdminView", 0 },
            { "diagnosticNodesVisibleInPlayerView", 0 },
            { "diagnosticTestNodeCount", hiddenTestNodes },
            { "readabilityScore", Math.Round(score, 2) },
            { "manualScreenshotRequired", true },
            { "result", overlapCount == 0 && offscreenCount == 0 ? "PASS" : "WARN" },
            { "findings", findings.ToArray() }
        };
    }

    private static double DevelopmentLayoutLinkLength(string sourceNodeId, string targetNodeId, Dictionary<string, Tuple<int, int>> positions, int nodeWidth, int nodeHeight)
    {
        var a = positions[sourceNodeId];
        var b = positions[targetNodeId];
        var ax = a.Item1 + nodeWidth / 2.0;
        var ay = a.Item2 + nodeHeight / 2.0;
        var bx = b.Item1 + nodeWidth / 2.0;
        var by = b.Item2 + nodeHeight / 2.0;
        var dx = ax - bx;
        var dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static int EstimateDevelopmentLinkCrossings(IEnumerable<Tuple<string, string>> links, Dictionary<string, Tuple<int, int>> positions, int nodeWidth, int nodeHeight)
    {
        var list = links.ToList();
        var count = 0;
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                if (string.Equals(list[i].Item1, list[j].Item1, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(list[i].Item1, list[j].Item2, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(list[i].Item2, list[j].Item1, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(list[i].Item2, list[j].Item2, StringComparison.OrdinalIgnoreCase))
                    continue;
                var a1 = positions[list[i].Item1];
                var a2 = positions[list[i].Item2];
                var b1 = positions[list[j].Item1];
                var b2 = positions[list[j].Item2];
                if (SegmentsIntersect(
                        a1.Item1 + nodeWidth / 2.0, a1.Item2 + nodeHeight / 2.0,
                        a2.Item1 + nodeWidth / 2.0, a2.Item2 + nodeHeight / 2.0,
                        b1.Item1 + nodeWidth / 2.0, b1.Item2 + nodeHeight / 2.0,
                        b2.Item1 + nodeWidth / 2.0, b2.Item2 + nodeHeight / 2.0))
                    count++;
            }
        }

        return count;
    }

    private static bool SegmentsIntersect(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
    {
        double Direction(double x1, double y1, double x2, double y2, double x3, double y3)
            => (x3 - x1) * (y2 - y1) - (x2 - x1) * (y3 - y1);
        var d1 = Direction(cx, cy, dx, dy, ax, ay);
        var d2 = Direction(cx, cy, dx, dy, bx, by);
        var d3 = Direction(ax, ay, bx, by, cx, cy);
        var d4 = Direction(ax, ay, bx, by, dx, dy);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private void SpendExperienceCoinsForNode(Character c, UserAccount actor, ClassNodeDefinition node, string requestId)
    {
        if (!DevelopmentExperienceCoinsEnabled()) return;
        var cost = Math.Max(0, node.CostExperienceCoins);
        if (cost <= 0) return;
        if (c.XpCoins < cost) throw new InvalidOperationException("Insufficient experience coins.");
        c.XpCoins -= cost;
        AddExperienceCoinLedger(c, actor.Id, ExperienceCoinLedgerEntryTypeIds.Purchase, -cost, "Покупка узла развития", node.NodeId, true);
        SyncExperienceCoinsProfile(c, actor.Id, requestId);
        TryPublishDevelopmentSync(c, "development.xp.changed", actor.Id, requestId);
    }

    private void SyncExperienceCoinsProfile(Character c, string actorUserId, string requestId)
    {
        var native = _profileNativeWriteService.UpdateWalletProfileAsync(
            c.Id,
            new Dictionary<string, object> { { "xpCoins", c.XpCoins } },
            actorUserId,
            requestId ?? string.Empty).GetAwaiter().GetResult();

        if (!native.ProfileWritten || !native.LegacyFacadeSynced || native.UsedFallback)
        {
            throw new InvalidOperationException("Experience coin profile write failed.");
        }
    }

    private void AddExperienceCoinLedger(Character c, string actorUserId, string entryType, int amount, string reason, string nodeId, bool playerVisible)
    {
        _repositories.ExperienceCoinLedger.Insert(new ExperienceCoinLedgerEntry
        {
            CampaignId = c.SessionId,
            CharacterId = c.Id,
            CharacterNameSnapshot = c.Name,
            ActorUserId = actorUserId,
            EntryType = entryType,
            Amount = amount,
            BalanceAfter = c.XpCoins,
            Reason = reason ?? string.Empty,
            SourceType = string.IsNullOrWhiteSpace(nodeId) ? "manual" : "development_node",
            SourceId = nodeId ?? string.Empty,
            DevelopmentNodeId = nodeId ?? string.Empty,
            IsPlayerVisible = playerVisible,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static Dictionary<string, object> ExperienceCoinLedgerPayload(ExperienceCoinLedgerEntry entry, bool includeAdmin)
    {
        return new Dictionary<string, object>
        {
            { "id", entry.Id },
            { "characterId", entry.CharacterId },
            { "entryType", entry.EntryType },
            { "amount", entry.Amount },
            { "balanceAfter", entry.BalanceAfter },
            { "reason", entry.Reason },
            { "developmentNodeId", entry.DevelopmentNodeId },
            { "createdAtUtc", entry.CreatedAtUtc },
            { "actorUserId", includeAdmin ? entry.ActorUserId : string.Empty }
        };
    }

    private static bool ShouldHideNodeFromPlayer(ClassNodeDefinition node)
    {
        return node.IsArchived || node.IsHidden || IsDevelopmentPerformanceTestNode(node) || node.VisibilityRule == DevelopmentUnlockPolicyIds.GMOnly || node.VisibilityRule == DevelopmentUnlockPolicyIds.HiddenUntilGMReveal;
    }

    private static bool IsDevelopmentPerformanceTestNode(ClassNodeDefinition node)
    {
        if (node == null) return false;
        var id = node.NodeId ?? string.Empty;
        var name = FirstNonEmpty(node.Name, node.PublicName, node.HiddenName);
        var branch = node.BranchId ?? string.Empty;
        return id.StartsWith("perf_0153_", StringComparison.OrdinalIgnoreCase)
               || id.StartsWith("perf_0154_", StringComparison.OrdinalIgnoreCase)
               || branch.IndexOf("performance", StringComparison.OrdinalIgnoreCase) >= 0
               || name.IndexOf("UX performance node", StringComparison.OrdinalIgnoreCase) >= 0
               || name.IndexOf("DEV_HEX_PERFORMANCE_TREE", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsDevelopmentDiagnosticLayoutNode(ClassNodeDefinition node)
    {
        if (node != null && IsDevelopmentLargeTestHexagon(EffectiveHexagonId(node))) return false;
        return node != null
               && (IsDevelopmentPerformanceTestNode(node)
                   || node.IsHidden
                   || !node.IsPlayerVisible
                   || node.VisibilityRule == DevelopmentUnlockPolicyIds.GMOnly
                   || node.VisibilityRule == DevelopmentUnlockPolicyIds.HiddenUntilGMReveal);
    }

    private bool IsNodeHexagonEnabled(ClassNodeDefinition node)
    {
        var hexagonId = EffectiveHexagonId(node);
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
            return DevelopmentMagicHexagonEnabled();
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Main, StringComparison.OrdinalIgnoreCase))
            return true;
        return DevelopmentMultiHexagonsEnabled();
    }

    private bool IsHexagonEnabled(string hexagonId)
    {
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
            return DevelopmentMagicHexagonEnabled();
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Main, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(hexagonId))
            return true;
        return DevelopmentMultiHexagonsEnabled();
    }

    private static bool IsDevelopmentLargeTestHexagon(string hexagonId)
        => string.Equals(hexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase);

    private static bool IsDevelopmentAdminOnlyHexagon(string hexagonId)
        => IsDevelopmentLargeTestHexagon(hexagonId);

    private static string EffectiveHexagonId(ClassNodeDefinition node)
        => FirstNonEmpty(node?.HexagonId, DevelopmentHexagonIds.Main);

    private static string EffectiveHexagonType(ClassNodeDefinition node)
        => FirstNonEmpty(node?.HexagonType, HexagonTypeFromId(EffectiveHexagonId(node)));

    private static string HexagonTypeFromId(string hexagonId)
    {
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase)) return DevelopmentHexagonTypes.Magic;
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Main, StringComparison.OrdinalIgnoreCase)) return DevelopmentHexagonTypes.Main;
        return DevelopmentHexagonTypes.Custom;
    }

    private static string GetHexagonDisplayName(string hexagonId)
    {
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase)) return "Шестиугольник магии";
        if (IsDevelopmentLargeTestHexagon(hexagonId)) return "Большое тестовое дерево развития 0.15.4";
        return "Основной шестиугольник развития";
    }

    private static bool IsPrimaryMagicClassNode(ClassNodeDefinition node)
    {
        if (node == null) return false;
        return node.IsPrimaryMagicClass ||
            string.Equals(node.NodeRole, DevelopmentNodeRoleIds.PrimaryMagicClass, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(node.PrimaryMagicGroupId) && string.Equals(EffectiveHexagonId(node), DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase));
    }

    private static string MagicPrimaryRestrictionSummary(ClassNodeDefinition node)
        => IsPrimaryMagicClassNode(node)
            ? "Первичный магический класс: можно выбрать один путь, второй откроется после завершения первого."
            : string.Empty;

    private static string FormatDevelopmentNodeType(ClassNodeDefinition node)
    {
        if (IsPrimaryMagicClassNode(node)) return "Первичный магический класс";
        return (node?.NodeType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            DevelopmentNodeTypes.Class => "Класс",
            DevelopmentNodeTypes.MagicPath => "Магическое направление",
            DevelopmentNodeTypes.Training => "Тренировка",
            DevelopmentNodeTypes.Skill => "Навык",
            DevelopmentNodeTypes.Profession => "Профессия",
            DevelopmentNodeTypes.HiddenDevelopment => "Скрытое развитие",
            _ => "Узел развития"
        };
    }

    private static string SafeNodeName(ClassNodeDefinition node, bool includeAdmin)
    {
        if (!includeAdmin && ShouldHideNodeFromPlayer(node)) return "????";
        return FirstNonEmpty(node.PublicName, node.Name, node.NodeId);
    }

    private static string FormatRequirements(IEnumerable<UnlockRequirement> requirements)
    {
        var parts = requirements.Select(x => FirstNonEmpty(x.Key, x.RequirementType)).Where(x => !string.IsNullOrWhiteSpace(x)).Take(4).ToArray();
        return parts.Length == 0 ? "Нет видимых требований." : "Требуется: " + string.Join(", ", parts);
    }

    private static string FormatRewards(ClassNodeDefinition node)
    {
        var parts = new List<string>();
        foreach (var stat in node.StatBonuses) parts.Add($"{stat.Stat} +{stat.Bonus}");
        foreach (var skill in node.UnlockSkillIds) parts.Add("Навык: " + skill);
        foreach (var fx in node.PassiveEffects) parts.Add(fx.Description);
        return parts.Count == 0 ? "Награды будут уточнены GM." : string.Join("; ", parts.Take(5));
    }

    private static List<string> GetRequiredNodeIds(ClassNodeDefinition node)
    {
        return (node.Requirements ?? new List<UnlockRequirement>())
            .Where(r => string.Equals(r.RequirementType, "node", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(r.RequirementType))
            .Select(r => r.Key)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ReadRequiredInt(IDictionary<string, object> payload, string primaryKey, string alternateKey, int fallback)
    {
        if (payload.ContainsKey(primaryKey))
        {
            var parsed = PayloadReader.GetInt(payload, primaryKey);
            if (parsed.HasValue) return parsed.Value;
            throw new InvalidOperationException(primaryKey + " must be a valid integer.");
        }

        if (!string.IsNullOrWhiteSpace(alternateKey) && payload.ContainsKey(alternateKey))
        {
            var parsed = PayloadReader.GetInt(payload, alternateKey);
            if (parsed.HasValue) return parsed.Value;
            throw new InvalidOperationException(alternateKey + " must be a valid integer.");
        }

        return fallback;
    }

    private static int ReadOptionalInt(IDictionary<string, object> payload, string key, int fallback)
    {
        if (!payload.ContainsKey(key)) return fallback;
        var parsed = PayloadReader.GetInt(payload, key);
        if (parsed.HasValue) return parsed.Value;
        throw new InvalidOperationException(key + " must be a valid integer.");
    }

    private static void ValidateRange(int value, int min, int max, string label)
    {
        if (value < min || value > max)
            throw new InvalidOperationException($"{label} must be between {min} and {max}.");
    }

    private static int SectorFromDirection(string directionId)
    {
        return (directionId ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            DevelopmentDirectionIds.StrengthAssault => 1,
            DevelopmentDirectionIds.DexterityManeuver => 2,
            DevelopmentDirectionIds.EnduranceResilience => 3,
            DevelopmentDirectionIds.IntellectReason => 4,
            DevelopmentDirectionIds.WisdomPath => 5,
            DevelopmentDirectionIds.CharismaInfluence => 6,
            _ => 0
        };
    }

    private static List<string> ReadRequiredNodeIds(IDictionary<string, object> payload, ClassNodeDefinition node)
    {
        if (!payload.ContainsKey("requiredNodeIds") && !payload.ContainsKey("requiredNodes"))
            return GetRequiredNodeIds(node);

        var raw = PayloadReader.GetString(payload, payload.ContainsKey("requiredNodeIds") ? "requiredNodeIds" : "requiredNodes") ?? string.Empty;
        if (payload.TryGetValue("requiredNodeIds", out var rawValue) && rawValue is IEnumerable enumerable && rawValue is not string)
        {
            return enumerable.Cast<object?>()
                .Select(item => Convert.ToString(item) ?? string.Empty)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return raw
            .Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RequirementExpression? ReadRequirementExpression0219(
        IDictionary<string, object> payload,
        string key,
        RequirementExpression? fallback)
    {
        if (!payload.TryGetValue(key, out var raw)) return fallback;
        if (raw == null) return null;
        var map = PayloadReader.GetDictionary(payload, key);
        if (map == null || map.Count == 0) return null;
        var expression = ReadRequirementExpressionMap0219(map);
        RequirementExpressionEvaluator0219.Validate(expression);
        return expression;
    }

    private static RequirementExpression ReadRequirementExpressionMap0219(IDictionary<string, object> map)
    {
        var expression = new RequirementExpression
        {
            Kind = FirstNonEmpty(PayloadReader.GetString(map, "kind"), RequirementExpressionKinds.Leaf),
            LeafType = PayloadReader.GetString(map, "leafType") ?? string.Empty,
            TargetId = PayloadReader.GetString(map, "targetId") ?? string.Empty,
            MinimumValue = PayloadReader.GetInt(map, "minimumValue") ?? 0,
            RequiredCount = PayloadReader.GetInt(map, "requiredCount") ?? 0,
            PublicLabel = PayloadReader.GetString(map, "publicLabel") ?? string.Empty,
            GMLabel = PayloadReader.GetString(map, "gmLabel") ?? string.Empty,
            IsHidden = PayloadReader.GetBool(map, "isHidden")
        };
        if (map.TryGetValue("children", out var rawChildren) && rawChildren is IEnumerable children && rawChildren is not string)
        {
            foreach (var child in children.Cast<object?>())
            {
                if (child == null) continue;
                var wrapper = new Dictionary<string, object> { { "child", child } };
                var childMap = PayloadReader.GetDictionary(wrapper, "child");
                if (childMap != null && childMap.Count > 0)
                    expression.Children.Add(ReadRequirementExpressionMap0219(childMap));
            }
        }
        return expression;
    }

    private static List<string> ReadStringList0219(
        IDictionary<string, object> payload,
        string key,
        IEnumerable<string>? fallback)
    {
        if (!payload.TryGetValue(key, out var raw)) return (fallback ?? Enumerable.Empty<string>()).ToList();
        if (raw is IEnumerable enumerable && raw is not string)
        {
            return enumerable.Cast<object?>()
                .Select(value => Convert.ToString(value)?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        return (Convert.ToString(raw) ?? string.Empty)
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string RequirementExpressionSummary0219(RequirementExpression expression)
    {
        if (expression.Kind == RequirementExpressionKinds.Leaf)
            return FirstNonEmpty(expression.PublicLabel, "Условие развития");
        var label = expression.Kind == RequirementExpressionKinds.AllOf ? "Все условия"
            : expression.Kind == RequirementExpressionKinds.AnyOf ? "Любое условие"
            : $"Не менее {expression.RequiredCount} условий";
        return $"{label}: {expression.Children.Count}";
    }

    private ResponseEnvelope SetDevelopmentNodeArchived(CommandContext context, bool archived)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var nodeId = RequireLength(PayloadReader.GetString(context.Request.Payload, "nodeId"), 1, 128, "nodeId");
        if (!_nodesById.TryGetValue(nodeId, out var node))
            throw new KeyNotFoundException("Development node not found.");
        var hexagonId = EffectiveHexagonId(node);
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

        node.IsArchived = archived;
        node.IsHidden = archived ? true : false;
        node.IsGMOnly = archived ? true : false;
        node.IsPlayerVisible = archived ? false : (context.Request.Payload.ContainsKey("isPlayerVisible") ? PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible") : true);
        if (archived)
        {
            node.VisibilityRule = DevelopmentUnlockPolicyIds.GMOnly;
            node.UnlockPolicy = DevelopmentUnlockPolicyIds.GMOnly;
            node.PurchasePolicy = DevelopmentPurchasePolicyIds.GMOnly;
        }
        else if (!node.IsHidden)
        {
            node.VisibilityRule = node.IsPlayerVisible ? DevelopmentUnlockPolicyIds.VisibleByDefault : DevelopmentUnlockPolicyIds.HiddenUntilGMReveal;
            node.UnlockPolicy = DevelopmentUnlockPolicyIds.VisibleByDefault;
            node.PurchasePolicy = DevelopmentPurchasePolicyIds.AutomaticIfRequirementsMet;
        }

        node.LayoutVersion = Math.Max(1, node.LayoutVersion) + 1;
        node.Revision = Math.Max(1, node.Revision) + 1;
        node.UpdatedAtUtc = DateTime.UtcNow;
        node.UpdatedByUserId = actor.Id;
        PersistDevelopmentNodeDefinition(node);
        EnsureDefinitionsLoaded(true);
        WriteAudit("development", actor.Id, archived ? "development_hexagon.node.archived" : "development_hexagon.node.restored", nodeId);
        return Ok(archived ? "Development node archived." : "Development node restored.", new Dictionary<string, object>
        {
            { "node", NodePayload(_nodesById.TryGetValue(nodeId, out var updated) ? updated : node) },
            { "hexagon", DevelopmentHexagonPayload(hexagonId, includeAdmin: true) },
            { "sourceOfTruth", "class_tree_definitions" }
        });
    }

    private ResponseEnvelope UpdateDevelopmentRequirementLink(CommandContext context, bool add)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        var actor = RequireAdmin(context);
        EnsureDefinitionsLoaded(false);
        var sourceNodeId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "sourceNodeId"), PayloadReader.GetString(context.Request.Payload, "requiredNodeId")), 1, 128, "sourceNodeId");
        var targetNodeId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "targetNodeId"), PayloadReader.GetString(context.Request.Payload, "nodeId")), 1, 128, "targetNodeId");
        if (string.Equals(sourceNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase))
            return Error("Self requirement is not allowed.", ResponseStatus.Conflict, ErrorCode.Conflict);
        if (!_nodesById.TryGetValue(sourceNodeId, out var sourceNode))
            throw new KeyNotFoundException("Required node not found.");
        if (!_nodesById.TryGetValue(targetNodeId, out var targetNode))
            throw new KeyNotFoundException("Target node not found.");

        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), EffectiveHexagonId(targetNode));
        if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");
        if (!string.Equals(EffectiveHexagonId(sourceNode), hexagonId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(EffectiveHexagonId(targetNode), hexagonId, StringComparison.OrdinalIgnoreCase))
            return Error("Requirement link must stay inside the selected development hexagon.", ResponseStatus.Conflict, ErrorCode.Conflict);

        targetNode.Requirements ??= new List<UnlockRequirement>();
        sourceNode.NextNodeIds ??= new List<string>();
        var changed = false;
        if (add)
        {
            var requirementExists = targetNode.Requirements.Any(r => string.Equals(r.RequirementType, "node", StringComparison.OrdinalIgnoreCase) && string.Equals(r.Key, sourceNodeId, StringComparison.OrdinalIgnoreCase));
            var nextExists = sourceNode.NextNodeIds.Any(id => string.Equals(id, targetNodeId, StringComparison.OrdinalIgnoreCase));
            if (requirementExists || nextExists)
                return Error("Requirement link already exists.", ResponseStatus.Conflict, ErrorCode.Conflict);

            if (!requirementExists)
            {
                targetNode.Requirements.Add(new UnlockRequirement { RequirementType = "node", Key = sourceNodeId });
                changed = true;
            }
            if (!nextExists)
            {
                sourceNode.NextNodeIds.Add(targetNodeId);
                changed = true;
            }
        }
        else
        {
            changed |= targetNode.Requirements.RemoveAll(r => string.Equals(r.RequirementType, "node", StringComparison.OrdinalIgnoreCase) && string.Equals(r.Key, sourceNodeId, StringComparison.OrdinalIgnoreCase)) > 0;
            changed |= sourceNode.NextNodeIds.RemoveAll(id => string.Equals(id, targetNodeId, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        targetNode.RequirementSummary = GetRequiredNodeIds(targetNode).Count == 0
            ? "Нет требований."
            : "Требуется: " + string.Join(", ", GetRequiredNodeIds(targetNode));
        foreach (var node in new[] { sourceNode, targetNode })
        {
            node.LayoutVersion = Math.Max(1, node.LayoutVersion) + 1;
            node.Revision = Math.Max(1, node.Revision) + 1;
            node.UpdatedAtUtc = DateTime.UtcNow;
            node.UpdatedByUserId = actor.Id;
        }

        var graphIssues = ValidateDevelopmentGraph(hexagonId)
            .Where(issue => issue.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            .Where(issue => DevelopmentGraphIssueTouches(issue, sourceNodeId, targetNodeId))
            .ToList();
        if (graphIssues.Count > 0)
        {
            EnsureDefinitionsLoaded(true);
            return Error("Development graph validation failed: " + string.Join("; ", graphIssues), ResponseStatus.Conflict, ErrorCode.Conflict);
        }

        if (changed)
        {
            PersistDevelopmentNodeDefinition(sourceNode);
            PersistDevelopmentNodeDefinition(targetNode);
            EnsureDefinitionsLoaded(true);
        }

        WriteAudit("development", actor.Id, add ? "development_hexagon.requirement_link.added" : "development_hexagon.requirement_link.removed", $"{sourceNodeId}->{targetNodeId}");
        var payload = DevelopmentHexagonLayoutPayload(hexagonId, includeAdmin: true);
        payload["changed"] = changed;
        return Ok(add ? "Requirement link added." : "Requirement link removed.", payload);
    }

    private List<Dictionary<string, object>> DevelopmentRequirementLinks(string hexagonId, bool includeAdmin)
    {
        var visibleNodeIds = _nodesById.Values
            .Where(node => string.Equals(EffectiveHexagonId(node), hexagonId, StringComparison.OrdinalIgnoreCase))
            .Where(node => includeAdmin || !ShouldHideNodeFromPlayer(node))
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<Dictionary<string, object>>();
        foreach (var target in _nodesById.Values.Where(node => visibleNodeIds.Contains(node.NodeId)))
        {
            foreach (var sourceId in GetRequiredNodeIds(target))
            {
                if (!visibleNodeIds.Contains(sourceId)) continue;
                result.Add(new Dictionary<string, object>
                {
                    { "linkId", sourceId + "->" + target.NodeId },
                    { "sourceNodeId", sourceId },
                    { "targetNodeId", target.NodeId },
                    { "linkType", "requirement" },
                    { "isPlayerSafe", !includeAdmin }
                });
            }
        }

        return result;
    }

    private List<string> ValidateDevelopmentGraph(string hexagonId)
    {
        var issues = new List<string>();
        var nodes = _nodesById.Values
            .Where(node => string.Equals(EffectiveHexagonId(node), hexagonId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes.Values)
        {
            var rawRequiredNodeIds = (node.Requirements ?? new List<UnlockRequirement>())
                .Where(r => string.Equals(r.RequirementType, "node", StringComparison.OrdinalIgnoreCase))
                .Select(r => FirstNonEmpty(r.Key, r.Value))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
            foreach (var duplicate in rawRequiredNodeIds.GroupBy(id => id, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key))
            {
                issues.Add("WARN:duplicate_requirement:" + node.NodeId + ":" + duplicate);
            }

            foreach (var requiredNodeId in GetRequiredNodeIds(node))
            {
                if (string.Equals(requiredNodeId, node.NodeId, StringComparison.OrdinalIgnoreCase))
                    issues.Add("ERROR:self_requirement:" + node.NodeId);
                if (!nodes.ContainsKey(requiredNodeId))
                    issues.Add("ERROR:missing_requirement:" + node.NodeId + ":" + requiredNodeId);
                else if (!node.IsArchived && nodes[requiredNodeId].IsArchived)
                    issues.Add("ERROR:active_node_requires_archived_node:" + node.NodeId + ":" + requiredNodeId);
                else if (node.IsPlayerVisible && ShouldHideNodeFromPlayer(nodes[requiredNodeId]))
                    issues.Add("WARN:player_visible_node_requires_hidden_node:" + node.NodeId + ":" + requiredNodeId);
            }
        }

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var nodeId in nodes.Keys)
        {
            VisitDevelopmentGraph(nodeId, nodes, visiting, visited, issues);
        }

        return issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsAllowedDevelopmentCurrency(string currencyId)
    {
        if (string.IsNullOrWhiteSpace(currencyId)) return false;
        var normalized = currencyId.Trim();
        return string.Equals(normalized, CharacterCurrencyIds.XpCoin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.IronCoin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.BronzeCoin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.SilverCoin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.GoldCoin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.PlatinumCoin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.OrichalcumCoin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.AdamantCoin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.SovereignCoin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.Credit, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.CorporateCredit, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.RationToken, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, CharacterCurrencyIds.LicensePoint, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DevelopmentGraphIssueTouches(string issue, params string[] nodeIds)
    {
        if (string.IsNullOrWhiteSpace(issue)) return false;
        var ids = (nodeIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
        if (ids.Count == 0) return false;
        foreach (var id in ids)
        {
            if (issue.IndexOf(":" + id, StringComparison.OrdinalIgnoreCase) >= 0 ||
                issue.EndsWith(":" + id, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void VisitDevelopmentGraph(string nodeId, Dictionary<string, ClassNodeDefinition> nodes, HashSet<string> visiting, HashSet<string> visited, List<string> issues)
    {
        if (visited.Contains(nodeId)) return;
        if (!visiting.Add(nodeId))
        {
            issues.Add("ERROR:cycle_detected:" + nodeId);
            return;
        }

        if (nodes.TryGetValue(nodeId, out var node))
        {
            foreach (var requiredNodeId in GetRequiredNodeIds(node).Where(nodes.ContainsKey))
            {
                VisitDevelopmentGraph(requiredNodeId, nodes, visiting, visited, issues);
            }
        }

        visiting.Remove(nodeId);
        visited.Add(nodeId);
    }

    private Dictionary<string, object> SeedLargeDevelopmentTestTree(string actorId, int requestedWorkingNodeCount)
    {
        var now = DateTime.UtcNow;
        var directions = new[]
        {
            DevelopmentDirectionIds.StrengthAssault,
            DevelopmentDirectionIds.DexterityManeuver,
            DevelopmentDirectionIds.EnduranceResilience,
            DevelopmentDirectionIds.IntellectReason,
            DevelopmentDirectionIds.WisdomPath,
            DevelopmentDirectionIds.CharismaInfluence
        };
        var branchNames = new[]
        {
            "Боевые классы",
            "Маневренные навыки",
            "Защитные профессии",
            "Технические лицензии",
            "Тактические доктрины",
            "Социальные специализации"
        };
        var nodeTypes = new[]
        {
            DevelopmentNodeTypes.Branch,
            DevelopmentNodeTypes.Class,
            DevelopmentNodeTypes.Skill,
            DevelopmentNodeTypes.Specialization,
            DevelopmentNodeTypes.Profession,
            DevelopmentNodeTypes.License,
            DevelopmentNodeTypes.CombatDoctrine
        };
        var nodeRoles = new[]
        {
            DevelopmentNodeRoleIds.MainBranchLevel,
            DevelopmentNodeRoleIds.UnlockNode,
            DevelopmentNodeRoleIds.ThematicNode,
            DevelopmentNodeRoleIds.Custom
        };

        var targetWorkingNodeCount = Math.Max(DevelopmentLargeTestMinimumWorkingNodes, requestedWorkingNodeCount);
        var branchCount = directions.Length;
        var levels = 5;
        var nodesPerBranch = (int)Math.Ceiling(targetWorkingNodeCount / (double)branchCount);
        var nodesPerLevel = (int)Math.Ceiling(nodesPerBranch / (double)levels);
        var nodes = new List<ClassNodeDefinition>();
        var root = new ClassNodeDefinition
        {
            NodeId = "large0154_root",
            HexagonId = DevelopmentHexagonIds.LargeTest0154,
            HexagonType = DevelopmentHexagonTypes.Custom,
            DirectionId = "root",
            BranchId = "large0154_root",
            Name = "Корень большого тестового дерева",
            PublicName = "Корень большого тестового дерева",
            Description = "Служебный корневой узел большого дерева для проверки читаемости.",
            PublicDescription = "Корневой узел большого дерева развития.",
            NodeType = DevelopmentNodeTypes.Branch,
            NodeRole = DevelopmentNodeRoleIds.NoviceRoot,
            Tier = 0,
            Ring = 0,
            Sector = 0,
            SortOrder = 0,
            CostExperienceCoins = 0,
            CurrencyId = CharacterCurrencyIds.XpCoin,
            IsPlayerVisible = false,
            IsGMOnly = true,
            VisibilityRule = DevelopmentUnlockPolicyIds.GMOnly,
            UnlockPolicy = DevelopmentUnlockPolicyIds.GMOnly,
            PurchasePolicy = DevelopmentPurchasePolicyIds.GMOnly,
            LayoutGroup = DevelopmentHexagonIds.LargeTest0154,
            LayoutLayer = 0,
            LayoutBranch = "large0154_root",
            LayoutWeight = 0,
            LayoutGeneratedBy = "large_test_0_15_4",
            LayoutGeneratedAtUtc = now,
            UpdatedAtUtc = now,
            UpdatedByUserId = actorId,
            SchemaVersion = 1
        };
        nodes.Add(root);

        for (var branch = 0; branch < branchCount; branch++)
        {
            var directionId = directions[branch];
            var branchId = $"large0154_branch_{branch + 1:00}";
            for (var level = 1; level <= levels; level++)
            {
                for (var index = 1; index <= nodesPerLevel; index++)
                {
                    if (nodes.Count - 1 >= targetWorkingNodeCount) break;
                    var nodeNumber = (level - 1) * nodesPerLevel + index;
                    var nodeId = $"large0154_b{branch + 1:00}_l{level:00}_n{index:000}";
                    var previousLevelSameIndex = $"large0154_b{branch + 1:00}_l{level - 1:00}_n{index:000}";
                    var previousLevelNeighbor = $"large0154_b{branch + 1:00}_l{level - 1:00}_n{Math.Max(1, index - 1):000}";
                    var requirements = new List<UnlockRequirement>();
                    if (level == 1)
                    {
                        requirements.Add(new UnlockRequirement { RequirementType = "node", Key = root.NodeId });
                    }
                    else
                    {
                        requirements.Add(new UnlockRequirement { RequirementType = "node", Key = previousLevelSameIndex });
                        if (!string.Equals(previousLevelNeighbor, previousLevelSameIndex, StringComparison.OrdinalIgnoreCase))
                            requirements.Add(new UnlockRequirement { RequirementType = "node", Key = previousLevelNeighbor });
                    }

                    var nodeType = level == 1 && index == 1 ? DevelopmentNodeTypes.Branch : nodeTypes[(branch + level + index) % nodeTypes.Length];
                    var node = new ClassNodeDefinition
                    {
                        NodeId = nodeId,
                        HexagonId = DevelopmentHexagonIds.LargeTest0154,
                        HexagonType = DevelopmentHexagonTypes.Custom,
                        DirectionId = directionId,
                        BranchId = branchId,
                        ClassId = $"large0154_class_{branch + 1:00}_{level:00}_{index:000}",
                        Name = $"{branchNames[branch]} · уровень {level} · узел {index}",
                        PublicName = $"{branchNames[branch]} · уровень {level} · узел {index}",
                        Description = $"Большой тестовый узел 0.15.4: ветка {branch + 1}, уровень {level}, порядковый {index}.",
                        PublicDescription = $"Узел проверки большой раскладки: ветка {branch + 1}, уровень {level}.",
                        NodeType = nodeType,
                        NodeRole = nodeRoles[(level + index) % nodeRoles.Length],
                        Tier = level,
                        Ring = level,
                        Sector = branch + 1,
                        SortOrder = (branch + 1) * 10000 + level * 1000 + index,
                        CostExperienceCoins = Math.Max(1, level + index % 5),
                        CurrencyId = CharacterCurrencyIds.XpCoin,
                        IsPlayerVisible = false,
                        IsGMOnly = true,
                        VisibilityRule = DevelopmentUnlockPolicyIds.GMOnly,
                        UnlockPolicy = DevelopmentUnlockPolicyIds.GMOnly,
                        PurchasePolicy = DevelopmentPurchasePolicyIds.GMOnly,
                        Requirements = requirements,
                        RequirementSummary = "Требуется: " + string.Join(", ", requirements.Select(r => r.Key)),
                        RewardSummary = "Служебная награда для проверки читаемости большого дерева.",
                        LayoutGroup = DevelopmentHexagonIds.LargeTest0154,
                        LayoutLayer = level,
                        LayoutBranch = branchId,
                        LayoutWeight = nodeNumber,
                        LayoutGeneratedBy = "large_test_0_15_4",
                        LayoutGeneratedAtUtc = now,
                        UpdatedAtUtc = now,
                        UpdatedByUserId = actorId,
                        SchemaVersion = 1
                    };
                    nodes.Add(node);
                }
            }
        }

        var nodeById = nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
            node.NextNodeIds.Clear();
        foreach (var node in nodes)
        {
            foreach (var requiredId in GetRequiredNodeIds(node))
            {
                if (nodeById.TryGetValue(requiredId, out var required) &&
                    !required.NextNodeIds.Contains(node.NodeId, StringComparer.OrdinalIgnoreCase))
                    required.NextNodeIds.Add(node.NodeId);
            }
        }

        var positions = BuildDevelopmentLargeBaselineLayout(nodes);
        foreach (var node in nodes)
        {
            if (!positions.TryGetValue(node.NodeId, out var position)) continue;
            node.GridX = position.Item1;
            node.GridY = position.Item2;
        }

        PersistLargeDevelopmentTestDefinitions(nodes);
        var workingNodeCount = nodes.Count(node => !string.Equals(node.NodeId, root.NodeId, StringComparison.OrdinalIgnoreCase));
        var linkCount = nodes.Sum(node => GetRequiredNodeIds(node).Count);
        return new Dictionary<string, object>
        {
            { "status", "PASS" },
            { "hexagonId", DevelopmentHexagonIds.LargeTest0154 },
            { "displayName", GetHexagonDisplayName(DevelopmentHexagonIds.LargeTest0154) },
            { "sourceOfTruth", "class_tree_definitions" },
            { "requestedWorkingNodeCount", requestedWorkingNodeCount },
            { "workingNodeCount", workingNodeCount },
            { "totalNodeCount", nodes.Count },
            { "linkCount", linkCount },
            { "branchCount", branchCount },
            { "levelCount", levels },
            { "adminOnly", true },
            { "playerVisible", false },
            { "rootNodeId", root.NodeId },
            { "seededAtUtc", now }
        };
    }

    private void PersistLargeDevelopmentTestDefinitions(List<ClassNodeDefinition> nodes)
    {
        var now = DateTime.UtcNow;
        var trees = _repositories.ClassTrees.Find(FilterDefinition<ClassTreeDefinition>.Empty).ToList();
        var changed = new Dictionary<string, ClassTreeDefinition>(StringComparer.Ordinal);
        foreach (var tree in trees)
        {
            tree.Nodes ??= new List<ClassNodeDefinition>();
            var removed = tree.Nodes.RemoveAll(node => IsDevelopmentLargeTestHexagon(EffectiveHexagonId(node)) ||
                                                       (node.NodeId ?? string.Empty).StartsWith("large0154_", StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
                changed[tree.Id] = tree;
        }

        var byDirection = trees
            .GroupBy(tree => string.IsNullOrWhiteSpace(tree.DirectionId) ? "root" : tree.DirectionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var direction = string.IsNullOrWhiteSpace(node.DirectionId) ? "root" : node.DirectionId;
            if (!byDirection.TryGetValue(direction, out var tree))
            {
                tree = new ClassTreeDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    DirectionId = direction,
                    Nodes = new List<ClassNodeDefinition>(),
                    CreatedUtc = now
                };
                byDirection[direction] = tree;
            }

            tree.Nodes ??= new List<ClassNodeDefinition>();
            tree.Nodes.RemoveAll(existing => string.Equals(existing.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase));
            tree.Nodes.Add(node);
            changed[tree.Id] = tree;
        }

        var existingIds = trees.Select(tree => tree.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var tree in changed.Values)
        {
            tree.Nodes = tree.Nodes
                .OrderBy(node => node.SortOrder)
                .ThenBy(node => node.GridY)
                .ThenBy(node => node.GridX)
                .ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            tree.UpdatedUtc = now;
            if (existingIds.Contains(tree.Id)) _repositories.ClassTrees.Replace(tree);
            else _repositories.ClassTrees.Insert(tree);
        }
    }

    private void PersistDevelopmentNodeDefinition(ClassNodeDefinition node)
    {
        var trees = _repositories.ClassTrees.Find(FilterDefinition<ClassTreeDefinition>.Empty).ToList();
        var target = trees.FirstOrDefault(t => string.Equals(t.DirectionId, node.DirectionId, StringComparison.OrdinalIgnoreCase));
        var oldTree = trees.FirstOrDefault(t => t.Nodes.Any(n => string.Equals(n.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase)));
        var touched = new List<ClassTreeDefinition>();

        foreach (var tree in trees)
        {
            var removed = tree.Nodes.RemoveAll(n => string.Equals(n.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0 && !touched.Any(t => string.Equals(t.Id, tree.Id, StringComparison.Ordinal)))
                touched.Add(tree);
        }

        if (target == null)
        {
            target = oldTree ?? new ClassTreeDefinition { Id = Guid.NewGuid().ToString("N") };
            target.DirectionId = string.IsNullOrWhiteSpace(node.DirectionId) ? "root" : node.DirectionId;
        }

        target.Nodes.RemoveAll(n => string.Equals(n.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase));
        target.Nodes.Add(node);
        target.UpdatedUtc = DateTime.UtcNow;
        if (!touched.Any(t => string.Equals(t.Id, target.Id, StringComparison.Ordinal)))
            touched.Add(target);

        var existingIds = new HashSet<string>(trees.Select(t => t.Id), StringComparer.Ordinal);
        foreach (var tree in touched)
        {
            tree.Nodes = tree.Nodes.OrderBy(n => n.SortOrder).ThenBy(n => n.GridY).ThenBy(n => n.GridX).ThenBy(n => n.NodeId, StringComparer.OrdinalIgnoreCase).ToList();
            tree.UpdatedUtc = DateTime.UtcNow;
            if (existingIds.Contains(tree.Id)) _repositories.ClassTrees.Replace(tree);
            else _repositories.ClassTrees.Insert(tree);
        }
    }

    private void PersistNormalizedDevelopmentDefinitions(List<ClassTreeDefinition> sourceTrees, IEnumerable<ClassNodeDefinition> normalizedNodes)
    {
        try
        {
            var persistedTrees = _repositories.ClassTrees.Find(FilterDefinition<ClassTreeDefinition>.Empty).ToList();
            var existingNodeIds = persistedTrees
                .SelectMany(t => t.Nodes ?? new List<ClassNodeDefinition>())
                .Select(n => n.NodeId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var treesByDirection = persistedTrees
                .GroupBy(t => string.IsNullOrWhiteSpace(t.DirectionId) ? "root" : t.DirectionId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var changed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in normalizedNodes ?? Enumerable.Empty<ClassNodeDefinition>())
            {
                if (string.IsNullOrWhiteSpace(node.NodeId)) continue;
                if (existingNodeIds.Contains(node.NodeId)) continue;

                var directionId = string.IsNullOrWhiteSpace(node.DirectionId) ? "root" : node.DirectionId;
                if (!treesByDirection.TryGetValue(directionId, out var tree))
                {
                    tree = new ClassTreeDefinition
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        DirectionId = directionId,
                        Nodes = new List<ClassNodeDefinition>(),
                        CreatedUtc = DateTime.UtcNow
                    };
                    treesByDirection[directionId] = tree;
                }

                tree.Nodes ??= new List<ClassNodeDefinition>();
                tree.Nodes.RemoveAll(n => string.Equals(n.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase));
                tree.Nodes.Add(node);
                tree.Nodes = tree.Nodes.OrderBy(n => n.SortOrder).ThenBy(n => n.GridY).ThenBy(n => n.GridX).ThenBy(n => n.NodeId, StringComparer.OrdinalIgnoreCase).ToList();
                tree.UpdatedUtc = DateTime.UtcNow;
                changed.Add(tree.Id);
            }

            var knownTreeIds = persistedTrees.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var tree in treesByDirection.Values.Where(t => changed.Contains(t.Id)))
            {
                if (knownTreeIds.Contains(tree.Id)) _repositories.ClassTrees.Replace(tree);
                else _repositories.ClassTrees.Insert(tree);
            }

        }
        catch (Exception ex)
        {
            _logger.Debug("development.definitions.persist_normalized_skipped " + ex.Message);
        }
    }

    private void EnsureMagicClassDefinitions()
    {
        EnsureMagicClassDefinition("dev_mana_mage_01448", "Маг маны", "Первичный магический класс чистого потока маны.", "magic_mana", "primary_magic_mana", "dev_mana_mage_01448", 10);
        EnsureMagicClassDefinition("dev_spell_mage_01448", "Маг заклинаний", "Первичный магический класс структурированных заклинаний.", "magic_spell", "primary_magic_spell", "dev_spell_mage_01448", 20);
        EnsureMagicClassDefinition("dev_seal_mage_01448", "Маг печатей", "Первичный магический класс печатей и рун.", "magic_seal", "primary_magic_seal", "dev_seal_mage_01448", 30);
        EnsureMagicClassDefinition("dev_arcana_mage_01448", "Маг Арканы", "Первичный магический класс арканных принципов.", "magic_arcana", "primary_magic_arcana", "dev_arcana_mage_01448", 40);
    }

    private void EnsureMagicClassDefinition(string code, string name, string description, string directionCode, string branchCode, string requiredNodeId, int sortOrder)
    {
        var existing = _repositories.ClassDefinitions.GetByCode(code);
        var definition = existing ?? new ClassDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Code = code,
            CreatedUtc = DateTime.UtcNow
        };

        definition.Name = string.IsNullOrWhiteSpace(definition.Name) ? name : definition.Name;
        definition.Description = string.IsNullOrWhiteSpace(definition.Description) ? description : definition.Description;
        definition.DirectionCode = string.IsNullOrWhiteSpace(definition.DirectionCode) ? directionCode : definition.DirectionCode;
        definition.BranchCode = string.IsNullOrWhiteSpace(definition.BranchCode) ? branchCode : definition.BranchCode;
        definition.RequiredHexagonId = DevelopmentHexagonIds.Magic;
        definition.RequiredNodeId = requiredNodeId;
        definition.VisibilityRule = "hexagon-gated";
        definition.IsLockedOutsideHexagon = true;
        definition.IsPlayerVisible = true;
        definition.IsActive = true;
        definition.Status = DefinitionStatus.Active;
        definition.SortOrder = definition.SortOrder <= 0 ? sortOrder : definition.SortOrder;
        definition.Level = definition.Level <= 0 ? 1 : definition.Level;
        definition.UnlockLevel = definition.UnlockLevel <= 0 ? 1 : definition.UnlockLevel;
        definition.MaxLevel = definition.MaxLevel <= 0 ? 3 : definition.MaxLevel;
        definition.XpCoinCost = definition.XpCoinCost <= 0 ? 4 : definition.XpCoinCost;
        if (!definition.Tags.Contains("development_hexagon", StringComparer.OrdinalIgnoreCase)) definition.Tags.Add("development_hexagon");
        if (!definition.Tags.Contains("magic_primary", StringComparer.OrdinalIgnoreCase)) definition.Tags.Add("magic_primary");
        _repositories.ClassDefinitions.Upsert(definition);
    }

    private Dictionary<string, object> DevelopmentHexagonPayload(string hexagonId, bool includeAdmin)
    {
        var effectiveHexagonId = string.IsNullOrWhiteSpace(hexagonId) ? DevelopmentHexagonIds.Main : hexagonId;
        var isMagic = string.Equals(effectiveHexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase);
        var isLargeTest = IsDevelopmentLargeTestHexagon(effectiveHexagonId);
        var directions = BuildDevelopmentDirections(effectiveHexagonId).Select(x => new Dictionary<string, object>
        {
            { "directionId", x.DirectionId },
            { "name", x.Name },
            { "atmosphericName", x.AtmosphericName },
            { "description", x.Description },
            { "angleDegrees", x.AngleDegrees },
            { "displayOrder", x.DisplayOrder }
        }).Cast<object>().ToArray();

        return new Dictionary<string, object>
        {
            { "hexagonId", effectiveHexagonId },
            { "name", GetHexagonDisplayName(effectiveHexagonId) },
            { "hexagonType", HexagonTypeFromId(effectiveHexagonId) },
            { "isMainHexagon", string.Equals(effectiveHexagonId, DevelopmentHexagonIds.Main, StringComparison.OrdinalIgnoreCase) },
            { "isPlayerVisible", !isLargeTest },
            { "isAdminOnly", isLargeTest },
            { "sortOrder", string.Equals(effectiveHexagonId, DevelopmentHexagonIds.Main, StringComparison.OrdinalIgnoreCase) ? 1 : isMagic ? 2 : 50 },
            { "centerNodeId", isLargeTest ? "large0154_root" : isMagic ? "magic_awakened" : "novice" },
            { "centerNodeName", isLargeTest ? "Корень большого дерева" : isMagic ? "Магическое пробуждение" : "Новичок" },
            { "directions", directions }
        };
    }

    private List<Dictionary<string, object>> DevelopmentHexagonsPayload(bool includeAdmin)
    {
        var result = new List<Dictionary<string, object>>
        {
            DevelopmentHexagonPayload(DevelopmentHexagonIds.Main, includeAdmin)
        };
        if (DevelopmentMagicHexagonEnabled())
            result.Add(DevelopmentHexagonPayload(DevelopmentHexagonIds.Magic, includeAdmin));
        if (includeAdmin && _nodesById.Values.Any(node => IsDevelopmentLargeTestHexagon(EffectiveHexagonId(node))))
            result.Add(DevelopmentHexagonPayload(DevelopmentHexagonIds.LargeTest0154, includeAdmin));
        return result;
    }

    private static List<DevelopmentDirectionDefinition> BuildDevelopmentDirections(string hexagonId = "")
    {
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
            return BuildMagicDevelopmentDirections();
        if (IsDevelopmentLargeTestHexagon(hexagonId))
            return BuildLargeDevelopmentDirections();

        if (string.IsNullOrWhiteSpace(hexagonId))
            return BuildDefaultDevelopmentDirections().Concat(BuildMagicDevelopmentDirections()).Concat(BuildLargeDevelopmentDirections()).ToList();

        return BuildDefaultDevelopmentDirections();
    }

    private static List<DevelopmentDirectionDefinition> BuildDefaultDevelopmentDirections()
    {
        return new List<DevelopmentDirectionDefinition>
        {
            new DevelopmentDirectionDefinition { DirectionId = "strength_assault", Name = "Сила", AtmosphericName = "Натиск", AttributeId = "strength", DisplayOrder = 1, AngleDegrees = 270, Description = "Штурм, тяжёлое оружие и прямое давление." },
            new DevelopmentDirectionDefinition { DirectionId = "dexterity_maneuver", Name = "Ловкость", AtmosphericName = "Манёвр", AttributeId = "dexterity", DisplayOrder = 2, AngleDegrees = 330, Description = "Мобильность, уклонение и точные действия." },
            new DevelopmentDirectionDefinition { DirectionId = "endurance_resilience", Name = "Выносливость", AtmosphericName = "Стойкость", AttributeId = "endurance", DisplayOrder = 3, AngleDegrees = 30, Description = "Живучесть, защита и удержание позиции." },
            new DevelopmentDirectionDefinition { DirectionId = "intellect_reason", Name = "Интеллект", AtmosphericName = "Разум", AttributeId = "intellect", DisplayOrder = 4, AngleDegrees = 90, Description = "Анализ, технологии и сложные методы." },
            new DevelopmentDirectionDefinition { DirectionId = "wisdom_path", Name = "Мудрость", AtmosphericName = "Путь", AttributeId = "wisdom", DisplayOrder = 5, AngleDegrees = 150, Description = "Интуиция, дисциплина и духовные практики." },
            new DevelopmentDirectionDefinition { DirectionId = "charisma_influence", Name = "Харизма", AtmosphericName = "Влияние", AttributeId = "charisma", DisplayOrder = 6, AngleDegrees = 210, Description = "Лидерство, дипломатия и социальное давление." }
        };
    }

    private static List<DevelopmentDirectionDefinition> BuildMagicDevelopmentDirections()
    {
        return new List<DevelopmentDirectionDefinition>
        {
            new DevelopmentDirectionDefinition { DirectionId = "magic_methods", HexagonId = DevelopmentHexagonIds.Magic, Name = "Методы магии", AtmosphericName = "Метод", AttributeId = "intellect", DisplayOrder = 1, AngleDegrees = 270, Description = "Первичные способы управления магией." },
            new DevelopmentDirectionDefinition { DirectionId = "magic_element_water", HexagonId = DevelopmentHexagonIds.Magic, Name = "Вода", AtmosphericName = "Течение", AttributeId = "wisdom", DisplayOrder = 2, AngleDegrees = 330, Description = "Базовое стихийное направление воды." },
            new DevelopmentDirectionDefinition { DirectionId = "magic_element_earth", HexagonId = DevelopmentHexagonIds.Magic, Name = "Земля", AtmosphericName = "Основа", AttributeId = "endurance", DisplayOrder = 3, AngleDegrees = 30, Description = "Базовое стихийное направление земли." },
            new DevelopmentDirectionDefinition { DirectionId = "magic_element_fire", HexagonId = DevelopmentHexagonIds.Magic, Name = "Огонь", AtmosphericName = "Пламя", AttributeId = "charisma", DisplayOrder = 4, AngleDegrees = 90, Description = "Базовое стихийное направление огня." },
            new DevelopmentDirectionDefinition { DirectionId = "magic_element_air", HexagonId = DevelopmentHexagonIds.Magic, Name = "Воздух", AtmosphericName = "Поток", AttributeId = "dexterity", DisplayOrder = 5, AngleDegrees = 150, Description = "Базовое стихийное направление воздуха." },
            new DevelopmentDirectionDefinition { DirectionId = "magic_special", HexagonId = DevelopmentHexagonIds.Magic, Name = "Особые направления", AtmosphericName = "Искусство", AttributeId = "intellect", DisplayOrder = 6, AngleDegrees = 210, Description = "Зачарование, руны, антимагия и духовная магия." }
        };
    }

    private static List<DevelopmentDirectionDefinition> BuildLargeDevelopmentDirections()
    {
        return new List<DevelopmentDirectionDefinition>
        {
            new DevelopmentDirectionDefinition { DirectionId = "large0154_branch_01", HexagonId = DevelopmentHexagonIds.LargeTest0154, Name = "Ветка 1", AtmosphericName = "нагрузка", DisplayOrder = 1, AngleDegrees = 270, Description = "Первая каноническая ветка большого тестового дерева." },
            new DevelopmentDirectionDefinition { DirectionId = "large0154_branch_02", HexagonId = DevelopmentHexagonIds.LargeTest0154, Name = "Ветка 2", AtmosphericName = "нагрузка", DisplayOrder = 2, AngleDegrees = 330, Description = "Вторая каноническая ветка большого тестового дерева." },
            new DevelopmentDirectionDefinition { DirectionId = "large0154_branch_03", HexagonId = DevelopmentHexagonIds.LargeTest0154, Name = "Ветка 3", AtmosphericName = "нагрузка", DisplayOrder = 3, AngleDegrees = 30, Description = "Третья каноническая ветка большого тестового дерева." },
            new DevelopmentDirectionDefinition { DirectionId = "large0154_branch_04", HexagonId = DevelopmentHexagonIds.LargeTest0154, Name = "Ветка 4", AtmosphericName = "нагрузка", DisplayOrder = 4, AngleDegrees = 90, Description = "Четвёртая каноническая ветка большого тестового дерева." },
            new DevelopmentDirectionDefinition { DirectionId = "large0154_branch_05", HexagonId = DevelopmentHexagonIds.LargeTest0154, Name = "Ветка 5", AtmosphericName = "нагрузка", DisplayOrder = 5, AngleDegrees = 150, Description = "Пятая каноническая ветка большого тестового дерева." },
            new DevelopmentDirectionDefinition { DirectionId = "large0154_branch_06", HexagonId = DevelopmentHexagonIds.LargeTest0154, Name = "Ветка 6", AtmosphericName = "нагрузка", DisplayOrder = 6, AngleDegrees = 210, Description = "Шестая каноническая ветка большого тестового дерева." }
        };
    }

    private void TryPublishDevelopmentSync(Character c, string eventType, string actorId, string requestId)
        => TryPublishSyncEvent(eventType, c.SessionId, "development", c.Id, "update", actorId, new Dictionary<string, object> { { "characterId", c.Id }, { "xpCoins", c.XpCoins } }, requestId);

    private void TryWriteDevelopmentJournal(Character c, string actorId, string eventType, string summary)
    {
        try
        {
            if (!_featureFlags.IsEnabled(nameof(DevelopmentFeatureFlags.UseDevelopmentJournalIntegration)) ||
                !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp)) ||
                !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion)))
                return;
            _repositories.EventJournalEntries.Insert(new EventJournalEntryState
            {
                CampaignId = c.SessionId,
                EntryType = EventJournalEntryTypeIds.Automatic,
                Category = EventJournalCategoryIds.Character,
                Severity = EventJournalSeverityIds.Information,
                Title = "Развитие персонажа",
                Summary = summary,
                CreatedByUserId = actorId,
                IsPlayerVisible = true,
                VisibilityMode = EventJournalVisibilityModeIds.PlayerVisible
            });
        }
        catch (Exception ex)
        {
            _logger.Debug("development.journal.skip " + ex.Message);
        }
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

        var presence = _repositories.Presence
            .Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, actor.Id))
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(presence?.ActiveCharacterId))
        {
            var active = _repositories.Characters.GetById(presence.ActiveCharacterId);
            if (active != null && (IsAdmin(actor) || string.Equals(active.OwnerUserId, actor.Id, StringComparison.Ordinal)))
                return active;
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
        var dbSkills = LoadSkillDefinitionsSafe();
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

        var classItems = JsonProtocolSerializer.Deserialize<List<ClassTreeDefinition>>(File.ReadAllText(classesPath)) ?? new List<ClassTreeDefinition>();
        var skillItems = JsonProtocolSerializer.Deserialize<List<SkillDefinitionRecord>>(File.ReadAllText(skillsPath)) ?? new List<SkillDefinitionRecord>();
        ApplyDefinitions(classItems, skillItems, "json");
    }

    private List<SkillDefinitionRecord> LoadSkillDefinitionsSafe()
    {
        // Character v2 and the definition editors own skill_definition_documents.
        // Adapt those canonical documents into the development runtime shape first.
        var canonical = _mongo.DefinitionSkills
            .Find(Builders<SkillDefinition>.Filter.Empty)
            .ToList()
            .Where(skill => !string.IsNullOrWhiteSpace(skill.Code) && !skill.IsArchived)
            .Select(skill => new SkillDefinitionRecord
            {
                Id = FirstNonEmpty(skill.Id, skill.Code),
                SkillId = skill.Code,
                Name = FirstNonEmpty(skill.Name, skill.Code),
                Description = skill.Description ?? string.Empty,
                Type = skill.IsRollable ? SkillType.Activatable : SkillType.Passive,
                UsageDescription = skill.IsRollable ? "Проверка навыка" : "Пассивное владение",
                DefaultAttribute = skill.DefaultAttribute ?? string.Empty,
                DefaultSubAttribute = skill.DefaultSubAttribute ?? string.Empty,
                RankMin = Math.Max(0, skill.RankMin),
                RankMax = Math.Max(1, Math.Min(20, skill.RankMax)),
                RequirementExpression = skill.RequirementExpression,
                RankMilestones = skill.RankMilestones ?? new List<SkillRankMilestoneDefinition>(),
                Techniques = skill.Techniques ?? new List<SkillTechniqueDefinition>()
            })
            .ToDictionary(skill => skill.SkillId, skill => skill, StringComparer.OrdinalIgnoreCase);

        var documents = _mongo.Database
            .GetCollection<BsonDocument>("skill_definitions")
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToList();

        var result = new List<SkillDefinitionRecord>();
        foreach (var document in documents)
        {
            try
            {
                var skill = BsonSerializer.Deserialize<SkillDefinitionRecord>(document);
                if (!string.IsNullOrWhiteSpace(skill.SkillId))
                {
                    if (!canonical.ContainsKey(skill.SkillId)) result.Add(skill);
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"development.skill_definition.legacy_shape documentId={GetBsonString(document, "_id")} reason={ex.GetType().Name}");
            }

            var legacyId = FirstNonEmpty(GetBsonString(document, "SkillId"), GetBsonString(document, "Code"), GetBsonString(document, "Id"), GetBsonString(document, "_id"));
            if (string.IsNullOrWhiteSpace(legacyId)) continue;
            if (canonical.ContainsKey(legacyId)) continue;
            result.Add(new SkillDefinitionRecord
            {
                Id = FirstNonEmpty(GetBsonString(document, "Id"), GetBsonString(document, "_id"), legacyId),
                SkillId = legacyId,
                Name = FirstNonEmpty(GetBsonString(document, "Name"), legacyId),
                Description = GetBsonString(document, "Description"),
                Type = ParseSkillType(GetBsonString(document, "Type")),
                UsageDescription = GetBsonString(document, "UsageDescription"),
                Tags = GetBsonStringArray(document, "Tags")
            });
        }

        result.AddRange(canonical.Values);
        return result;
    }

    private static SkillType ParseSkillType(string raw)
        => Enum.TryParse<SkillType>(raw, ignoreCase: true, out var parsed) ? parsed : SkillType.Passive;

    private static string GetBsonString(BsonDocument document, string key)
    {
        if (!document.TryGetValue(key, out var value) || value.IsBsonNull) return string.Empty;
        return value.ToString();
    }

    private static List<string> GetBsonStringArray(BsonDocument document, string key)
    {
        if (!document.TryGetValue(key, out var value) || !value.IsBsonArray) return new List<string>();
        return value.AsBsonArray.Select(x => x.IsBsonNull ? string.Empty : x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
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
        NormalizeDevelopmentNodeMetadata(_nodesById);
        PersistNormalizedDevelopmentDefinitions(classes, _nodesById.Values);
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

    private static void NormalizeDevelopmentNodeMetadata(Dictionary<string, ClassNodeDefinition> nodes)
    {
        if (!nodes.ContainsKey("novice"))
        {
            nodes["novice"] = new ClassNodeDefinition
            {
                NodeId = "novice",
                HexagonId = DevelopmentHexagonIds.Main,
                HexagonType = DevelopmentHexagonTypes.Main,
                DirectionId = "root",
                BranchId = "root",
                Name = "Новичок",
                PublicName = "Новичок",
                Description = "Стартовый технический класс персонажа.",
                PublicDescription = "Стартовый центр развития.",
                NodeType = DevelopmentNodeTypes.Class,
                NodeRole = DevelopmentNodeRoleIds.NoviceRoot,
                Tier = 0,
                MaxTier = 0,
                CostExperienceCoins = 0,
                GridX = 190,
                GridY = 120,
                Ring = 0,
                RewardSummary = "Базовый доступ к развитию персонажа."
            };
        }

        ApplyNodeMetadata(nodes, "defender_guard", DevelopmentDirectionIds.EnduranceResilience, "endurance_resilience_core", "Стойкость I", "Живучесть, защита и удержание позиции.", 2, 322, 186, "Броня +1; открывает базовую стойкость.");
        ApplyNodeMetadata(nodes, "vanguard_breach", DevelopmentDirectionIds.StrengthAssault, "strength_assault_core", "Натиск I", "Штурм, тяжёлое оружие и прямое давление.", 2, 190, 12, "Сила +1; открывает базовый натиск.");
        ApplyNodeMetadata(nodes, "ranger_hunt", DevelopmentDirectionIds.DexterityManeuver, "dexterity_maneuver_core", "Манёвр I", "Мобильность, уклонение и точные действия.", 2, 322, 68, "Ловкость +1; открывает базовый манёвр.");
        ApplyNodeMetadata(nodes, "samurai_focus", DevelopmentDirectionIds.WisdomPath, "wisdom_path_core", "Путь I", "Интуиция, дисциплина и духовные практики.", 2, 58, 186, "Мудрость +1; открывает базовый путь.");
        ApplyNodeMetadata(nodes, "mage_channel", DevelopmentDirectionIds.IntellectReason, "intellect_reason_core", "Разум I", "Анализ, технологии и сложные методы.", 2, 190, 242, "Интеллект +1; открывает базовый анализ.");
        ApplyNodeMetadata(nodes, "inventor_gear", DevelopmentDirectionIds.CharismaInfluence, "charisma_influence_core", "Влияние I", "Лидерство, дипломатия и социальное давление.", 2, 58, 68, "Харизма +1; открывает базовое влияние.");

        EnsureDevelopmentNode(nodes, "magic_awakened", "magic_root", "magic_root", "Магическое пробуждение", "Стартовый центр магического развития.", 0, 190, 120, "Открывает первичные магические пути.", new[] { "novice" }, DevelopmentNodeTypes.MagicPath, DevelopmentNodeRoleIds.MagicRoot, false, string.Empty, DevelopmentHexagonIds.Magic, DevelopmentHexagonTypes.Magic, false);
    }

    private static void EnsureDevelopmentNode(Dictionary<string, ClassNodeDefinition> nodes, string nodeId, string directionId, string branchId, string name, string description, int cost, int gridX, int gridY, string rewardSummary, IEnumerable<string> requiredNodeIds, string nodeType, string nodeRole, bool hidden, string classId, string hexagonId = DevelopmentHexagonIds.Main, string hexagonType = DevelopmentHexagonTypes.Main, bool isPrimaryMagicClass = false)
    {
        var created = false;
        if (!nodes.TryGetValue(nodeId, out var node))
        {
            node = new ClassNodeDefinition { NodeId = nodeId };
            nodes[nodeId] = node;
            created = true;
        }

        node.HexagonId = string.IsNullOrWhiteSpace(node.HexagonId) ? hexagonId : node.HexagonId;
        node.HexagonType = string.IsNullOrWhiteSpace(node.HexagonType) ? hexagonType : node.HexagonType;
        node.DirectionId = string.IsNullOrWhiteSpace(node.DirectionId) ? directionId : node.DirectionId;
        node.BranchId = string.IsNullOrWhiteSpace(node.BranchId) ? branchId : node.BranchId;
        node.ClassId = string.IsNullOrWhiteSpace(node.ClassId) ? classId ?? string.Empty : node.ClassId;
        node.Name = string.IsNullOrWhiteSpace(node.Name) ? name : node.Name;
        node.PublicName = string.IsNullOrWhiteSpace(node.PublicName) && !hidden ? name : node.PublicName;
        node.HiddenName = string.IsNullOrWhiteSpace(node.HiddenName) && hidden ? name : node.HiddenName;
        node.Description = string.IsNullOrWhiteSpace(node.Description) ? description : node.Description;
        node.PublicDescription = string.IsNullOrWhiteSpace(node.PublicDescription) && !hidden ? description : node.PublicDescription;
        node.NodeType = string.IsNullOrWhiteSpace(node.NodeType) ? nodeType : node.NodeType;
        node.NodeRole = string.IsNullOrWhiteSpace(node.NodeRole) ? nodeRole : node.NodeRole;
        node.IsPrimaryMagicClass = node.IsPrimaryMagicClass || isPrimaryMagicClass || string.Equals(node.NodeRole, DevelopmentNodeRoleIds.PrimaryMagicClass, StringComparison.OrdinalIgnoreCase);
        node.PrimaryMagicGroupId = node.IsPrimaryMagicClass ? FirstNonEmpty(node.PrimaryMagicGroupId, MagicPrimaryGroupId) : node.PrimaryMagicGroupId;
        node.MagicRestrictionSummary = node.IsPrimaryMagicClass && string.IsNullOrWhiteSpace(node.MagicRestrictionSummary)
            ? "Можно выбрать только один первичный магический класс, пока первый магический путь не завершён."
            : node.MagicRestrictionSummary;
        node.Tier = node.Tier <= 0 ? 1 : node.Tier;
        node.MaxTier = Math.Max(node.Tier, node.MaxTier <= 0 ? 1 : node.MaxTier);
        node.CostExperienceCoins = node.CostExperienceCoins <= 0 ? cost : node.CostExperienceCoins;
        if (created || node.GridX == 0) node.GridX = gridX;
        if (created || node.GridY == 0) node.GridY = gridY;
        node.Ring = Math.Max(1, node.Ring);
        node.Sector = node.Sector <= 0 ? SectorFromDirection(node.DirectionId) : node.Sector;
        node.SortOrder = node.SortOrder <= 0 ? node.Tier * 100 + node.Sector : node.SortOrder;
        node.CurrencyId = string.IsNullOrWhiteSpace(node.CurrencyId) ? CharacterCurrencyIds.XpCoin : node.CurrencyId;
        node.LayoutVersion = Math.Max(1, node.LayoutVersion);
        if (node.UpdatedAtUtc == default(DateTime)) node.UpdatedAtUtc = DateTime.UtcNow;
        node.VisibilityRule = string.IsNullOrWhiteSpace(node.VisibilityRule) ? (hidden ? DevelopmentUnlockPolicyIds.GMOnly : DevelopmentUnlockPolicyIds.VisibleByDefault) : node.VisibilityRule;
        node.UnlockPolicy = string.IsNullOrWhiteSpace(node.UnlockPolicy) ? (hidden ? DevelopmentUnlockPolicyIds.GMOnly : DevelopmentUnlockPolicyIds.VisibleByDefault) : node.UnlockPolicy;
        node.PurchasePolicy = string.IsNullOrWhiteSpace(node.PurchasePolicy) ? (hidden ? DevelopmentPurchasePolicyIds.GMOnly : DevelopmentPurchasePolicyIds.AutomaticIfRequirementsMet) : node.PurchasePolicy;
        if (created) node.IsHidden = hidden;
        node.IsGMOnly = node.IsGMOnly || node.VisibilityRule == DevelopmentUnlockPolicyIds.GMOnly;
        node.IsPlayerVisible = node.IsPlayerVisible && !ShouldHideNodeFromPlayer(node);
        if (string.IsNullOrWhiteSpace(node.RequirementSummary))
            node.RequirementSummary = requiredNodeIds == null || !requiredNodeIds.Any()
            ? "Нет требований."
            : "Требуется: " + string.Join(", ", requiredNodeIds);
        node.RewardSummary = string.IsNullOrWhiteSpace(node.RewardSummary) ? rewardSummary : node.RewardSummary;
        if (node.Requirements == null || node.Requirements.Count == 0)
        {
            node.Requirements = (requiredNodeIds ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new UnlockRequirement { RequirementType = "node", Key = x })
                .ToList();
        }
    }

    private static void ApplyNodeMetadata(Dictionary<string, ClassNodeDefinition> nodes, string nodeId, string directionId, string branchId, string name, string description, int cost, int gridX, int gridY, string rewardSummary)
    {
        if (!nodes.TryGetValue(nodeId, out var node)) return;
        node.DirectionId = directionId;
        node.HexagonId = string.IsNullOrWhiteSpace(node.HexagonId) ? DevelopmentHexagonIds.Main : node.HexagonId;
        node.HexagonType = string.IsNullOrWhiteSpace(node.HexagonType) ? HexagonTypeFromId(node.HexagonId) : node.HexagonType;
        node.ClassId = string.IsNullOrWhiteSpace(node.ClassId) ? node.NodeId : node.ClassId;
        node.BranchId = branchId;
        node.Name = name;
        node.PublicName = name;
        node.Description = description;
        node.PublicDescription = description;
        node.NodeType = DevelopmentNodeTypes.Class;
        node.NodeRole = DevelopmentNodeRoleIds.MainBranchLevel;
        node.Tier = node.Tier <= 0 ? 1 : node.Tier;
        node.MaxTier = node.MaxTier <= 0 ? 20 : node.MaxTier;
        node.CostExperienceCoins = node.CostExperienceCoins <= 0 ? cost : node.CostExperienceCoins;
        if (node.LayoutVersion <= 1 && string.IsNullOrWhiteSpace(node.LayoutGeneratedBy) && !node.LayoutLockedManualPosition)
        {
            node.GridX = gridX;
            node.GridY = gridY;
        }
        node.Ring = node.Ring <= 0 ? 1 : node.Ring;
        node.VisibilityRule = string.IsNullOrWhiteSpace(node.VisibilityRule) ? "public" : node.VisibilityRule;
        node.RewardSummary = string.IsNullOrWhiteSpace(node.RewardSummary) ? rewardSummary : node.RewardSummary;
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

        var acquiredNodeIds = GetPurchasedDevelopmentNodeIds(c);
        var profileDirections = acquiredNodeIds
            .Where(id => _nodesById.ContainsKey(id))
            .Select(id => _nodesById[id])
            .GroupBy(n => string.IsNullOrWhiteSpace(n.DirectionId) ? "root" : n.DirectionId)
            .Select(g => new CharacterClassDirectionState
            {
                DirectionId = g.Key,
                SelectedBranchId = g.Select(n => n.BranchId).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                AcquiredNodes = g.Select(n => new CharacterClassNodeState { NodeId = n.NodeId, AcquiredAtUtc = FindDevelopmentProfileNodeState(c.Id, n.NodeId)?.PurchasedAtUtc ?? DateTime.UtcNow }).ToList()
            })
            .ToList();
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
            Directions = profileDirections,
            TotalStatBonuses = bonuses.Select(x => new StatBonusDefinition { Stat = x.Key, Bonus = x.Value }).ToList(),
            PassiveEffects = effects,
            Unlocks = unlockState,
            DefinitionVersion = _definitionVersion
        };

        var skillProfile = _mongo.CharacterSkillProfiles
            .Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, c.Id))
            .FirstOrDefault()?.Profile;
        var acquiredProfileSkills = new HashSet<string>(
            skillProfile?.Skills?
                .Where(value => value.IsLearned || value.IsUnlocked)
                .Select(value => value.SkillId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var skillStates = new List<CharacterSkillState>();
        foreach (var skill in _skillsById.Values)
        {
            var reasons = EvaluateSkillAvailability(c, skill, unlockedSkillIds);
            skillStates.Add(new CharacterSkillState
            {
                SkillId = skill.SkillId,
                Acquired = acquiredProfileSkills.Contains(skill.SkillId),
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
        var root = c.ClassDirections.FirstOrDefault(x => x.DirectionId == "root");
        if (root == null)
        {
            root = new CharacterClassDirectionState { DirectionId = "root", SelectedBranchId = "root" };
            c.ClassDirections.Add(root);
        }
        if (root.AcquiredNodes.All(x => !string.Equals(x.NodeId, "novice", StringComparison.OrdinalIgnoreCase)))
        {
            root.AcquiredNodes.Add(new CharacterClassNodeState { NodeId = "novice", AcquiredAtUtc = DateTime.UtcNow });
        }
        var developmentProfile = _mongo.CharacterDevelopmentProfiles
            .Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, c.Id))
            .FirstOrDefault()?.Profile;
        var profileNodeIds = new HashSet<string>(
            developmentProfile?.Nodes?.Select(state => state.DevelopmentNodeId).Where(id => !string.IsNullOrWhiteSpace(id))
                ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        if (!profileNodeIds.Contains("novice") && _nodesById.TryGetValue("novice", out var noviceNode))
        {
            UpsertDevelopmentProfileNode(c, noviceNode, "system", "development_profile_initialize");
        }
        if (!profileNodeIds.Contains("magic_awakened") && _nodesById.TryGetValue("magic_awakened", out var magicRootNode))
        {
            UpsertDevelopmentProfileNode(c, magicRootNode, "system", "development_profile_initialize");
        }
        if (c.ClassSkillSnapshot == null)
        {
            c.ClassSkillSnapshot = new CharacterProgressSnapshot { CharacterId = c.Id, DefinitionVersion = _definitionVersion };
        }
    }

    private List<string> EvaluateNodeAvailability(Character c, ClassNodeDefinition node, CharacterProgressSnapshot snapshot)
    {
        var reasons = new List<string>();
        var acquiredNodeIds = GetPurchasedDevelopmentNodeIds(c);
        var selectedBranchIds = acquiredNodeIds
            .Where(id => _nodesById.ContainsKey(id))
            .Select(id => _nodesById[id])
            .Where(n => string.Equals(n.DirectionId, node.DirectionId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(n.BranchId))
            .Select(n => n.BranchId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var continuesAcquiredPath = !string.IsNullOrWhiteSpace(node.LayoutBranch) && acquiredNodeIds
            .Where(id => _nodesById.ContainsKey(id))
            .Select(id => _nodesById[id])
            .Any(acquired => string.Equals(acquired.DirectionId, node.DirectionId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(acquired.LayoutBranch, node.LayoutBranch, StringComparison.OrdinalIgnoreCase));
        if (selectedBranchIds.Count > 0 && !selectedBranchIds.Contains(node.BranchId, StringComparer.OrdinalIgnoreCase) && !continuesAcquiredPath)
        {
            reasons.Add("В направлении можно выбрать только одну ветку");
        }

        var requirementExpression = node.RequirementExpression
            ?? RequirementExpressionEvaluator0219.MigrateLegacy(node.Requirements, RequirementExpressionKinds.AllOf);
        if (requirementExpression.Children.Count > 0 || requirementExpression.Kind == RequirementExpressionKinds.Leaf)
        {
            var evaluation = RequirementExpressionEvaluator0219.Evaluate(requirementExpression, BuildRequirementFacts0219(c.Id), playerSafe: false);
            if (!evaluation.IsSatisfied)
            {
                reasons.Add(evaluation.PublicReason);
            }
        }

        if (IsPrimaryMagicClassNode(node))
        {
            var blocker = GetBlockingIncompletePrimaryMagicNode(c, node.NodeId);
            if (!string.IsNullOrWhiteSpace(blocker))
            {
                reasons.Add("Требуется завершить первый первичный магический путь: " + blocker);
            }
        }

        var anyAcquiredInDirection = acquiredNodeIds
            .Where(id => _nodesById.ContainsKey(id))
            .Select(id => _nodesById[id])
            .Any(n => string.Equals(n.DirectionId, node.DirectionId, StringComparison.OrdinalIgnoreCase));
        if (!anyAcquiredInDirection)
        {
            return reasons;
        }

        return reasons;
    }

    private List<string> EvaluateSkillAvailability(Character c, SkillDefinitionRecord skill, HashSet<string> unlockedSkillIds)
    {
        var reasons = new List<string>();
        var acquiredNodeIds = GetPurchasedDevelopmentNodeIds(c);
        if (!unlockedSkillIds.Contains(skill.SkillId)) reasons.Add("Навык не открыт узлом класса");
        var requirementExpression = skill.RequirementExpression
            ?? RequirementExpressionEvaluator0219.MigrateLegacy(skill.Requirements, RequirementExpressionKinds.AllOf);
        if (requirementExpression.Children.Count > 0 || requirementExpression.Kind == RequirementExpressionKinds.Leaf)
        {
            var evaluation = RequirementExpressionEvaluator0219.Evaluate(requirementExpression, BuildRequirementFacts0219(c.Id), playerSafe: false);
            if (!evaluation.IsSatisfied) reasons.Add(evaluation.PublicReason);
        }
        return reasons;
    }

    private RequirementFactSnapshot BuildRequirementFacts0219(string characterId)
    {
        var facts = new RequirementFactSnapshot();
        var attributes = _mongo.CharacterAttributeProfiles
            .Find(Builders<CharacterAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId))
            .FirstOrDefault()?.Profile;
        foreach (var value in attributes?.Values ?? new List<CharacterAttributeValue>())
            facts.Attributes[value.AttributeId] = value.CurrentValue + value.ManualModifier;

        var subAttributes = _mongo.CharacterSubAttributeProfiles
            .Find(Builders<CharacterSubAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId))
            .FirstOrDefault()?.Profile;
        foreach (var value in subAttributes?.SubAttributes ?? new List<CharacterSubAttributeValue>())
            facts.SubAttributes[value.SubAttributeId] = value.CurrentValue + value.ManualBonus;

        var skills = _mongo.CharacterSkillProfiles
            .Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, characterId))
            .FirstOrDefault()?.Profile;
        foreach (var value in skills?.Skills ?? new List<CharacterSkillProfileValue>())
        {
            if (!value.IsLearned && !value.IsUnlocked) continue;
            facts.SkillRanks[value.SkillId] = Math.Max(0, Math.Min(20, value.Rank));
            if (_skillsById.TryGetValue(value.SkillId, out var definition))
            {
                foreach (var technique in definition.Techniques.Where(technique =>
                    !technique.IsArchived &&
                    value.Rank >= technique.MinimumRank &&
                    (!technique.MaximumRank.HasValue || value.Rank <= technique.MaximumRank.Value)))
                {
                    facts.TechniqueIds.Add(technique.Id);
                    if (!string.IsNullOrWhiteSpace(technique.ActionDefinitionId))
                        facts.ActionIds.Add(technique.ActionDefinitionId);
                }
            }
        }

        var development = _mongo.CharacterDevelopmentProfiles
            .Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, characterId))
            .FirstOrDefault()?.Profile;
        foreach (var state in development?.Nodes ?? new List<CharacterDevelopmentNodeState>())
            if (state.IsPurchased || state.IsUnlocked || state.CurrentTier > 0)
            {
                facts.DevelopmentNodeIds.Add(state.DevelopmentNodeId);
                facts.DevelopmentNodeRanks[state.DevelopmentNodeId] = Math.Max(
                    facts.DevelopmentNodeRanks.TryGetValue(state.DevelopmentNodeId, out var currentRank) ? currentRank : 0,
                    state.CurrentTier);
                if (_nodesById.TryGetValue(state.DevelopmentNodeId, out var node))
                {
                    if (!string.IsNullOrWhiteSpace(node.DirectionId)) facts.DevelopmentPathIds.Add(node.DirectionId);
                    if (!string.IsNullOrWhiteSpace(node.BranchId)) facts.DevelopmentPathIds.Add(node.BranchId);
                    if (!string.IsNullOrWhiteSpace(node.LayoutBranch)) facts.DevelopmentPathIds.Add(node.LayoutBranch);
                }
            }
        return facts;
    }

    private void ValidateMagicPrimaryPurchase(Character c, ClassNodeDefinition node)
    {
        if (!IsPrimaryMagicClassNode(node)) return;
        var blocker = GetBlockingIncompletePrimaryMagicNode(c, node.NodeId);
        if (!string.IsNullOrWhiteSpace(blocker))
        {
            throw new InvalidOperationException("Primary magic class is locked until the first magic path is completed: " + blocker);
        }
    }

    private string GetBlockingIncompletePrimaryMagicNode(Character c, string candidateNodeId)
    {
        var profile = _mongo.CharacterDevelopmentProfiles
            .Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, c.Id))
            .FirstOrDefault()?.Profile;

        foreach (var state in profile?.Nodes ?? new List<CharacterDevelopmentNodeState>())
        {
            if (string.IsNullOrWhiteSpace(state.DevelopmentNodeId)) continue;
            if (string.Equals(state.DevelopmentNodeId, candidateNodeId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!(state.IsPurchased || state.IsUnlocked || state.CurrentTier > 0)) continue;
            if (!_nodesById.TryGetValue(state.DevelopmentNodeId, out var purchasedNode)) continue;
            if (!IsPrimaryMagicClassNode(purchasedNode)) continue;
            var completed = state.IsCompleted ||
                string.Equals(state.State, "completed", StringComparison.OrdinalIgnoreCase);
            if (!completed) return state.DevelopmentNodeId;
        }

        return string.Empty;
    }

    private CharacterClassNodeState FindNodeState(Character c, string nodeId)
    {
        var profileNode = FindDevelopmentProfileNodeState(c.Id, nodeId);
        if (profileNode == null) return null;
        return new CharacterClassNodeState { NodeId = nodeId, AcquiredAtUtc = profileNode.PurchasedAtUtc };
    }

    private CharacterDevelopmentNodeState? FindDevelopmentProfileNodeState(string characterId, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(nodeId)) return null;
        var profile = _mongo.CharacterDevelopmentProfiles
            .Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, characterId))
            .FirstOrDefault()?.Profile;
        return profile?.Nodes?
            .FirstOrDefault(n => string.Equals(n.DevelopmentNodeId, nodeId, StringComparison.OrdinalIgnoreCase) && (n.IsPurchased || n.IsUnlocked || n.CurrentTier > 0));
    }

    private HashSet<string> GetPurchasedDevelopmentNodeIds(Character c)
    {
        var profile = _mongo.CharacterDevelopmentProfiles
            .Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, c.Id))
            .FirstOrDefault()?.Profile;
        return (profile?.Nodes ?? new List<CharacterDevelopmentNodeState>())
            .Where(n => n.IsPurchased || n.IsUnlocked || n.CurrentTier > 0)
            .Select(n => n.DevelopmentNodeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, object> CharacterProgressPayload(Character c, CharacterProgressSnapshot snapshot)
    {
        var profile = _mongo.CharacterDevelopmentProfiles
            .Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, c.Id))
            .FirstOrDefault()?.Profile;
        var visibleProfileHexagons = (profile?.Hexagons ?? new List<CharacterDevelopmentHexagonState>())
            .Where(h => IsHexagonEnabled(h.HexagonId))
            .ToList();
        var visibleActiveHexagonIds = (profile?.ActiveHexagonIds ?? new List<string>())
            .Where(IsHexagonEnabled)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new Dictionary<string, object>
        {
            { "characterId", c.Id },
            { "xpCoins", c.XpCoins },
            { "definitionVersion", snapshot.DefinitionVersion },
            { "hexagon", DevelopmentHexagonPayload(DevelopmentHexagonIds.Main, includeAdmin: true) },
            { "hexagons", DevelopmentHexagonsPayload(includeAdmin: true).Cast<object>().ToArray() },
            { "activeHexagonIds", visibleActiveHexagonIds.Cast<object>().ToArray() },
            { "profileHexagons", visibleProfileHexagons.Select(h => new Dictionary<string, object>
                {
                    { "hexagonId", h.HexagonId },
                    { "hexagonType", h.HexagonType },
                    { "displayName", h.DisplayName },
                    { "isUnlocked", h.IsUnlocked },
                    { "isPlayerVisible", h.IsPlayerVisible },
                    { "isMain", h.IsMain },
                    { "sortOrder", h.SortOrder },
                    { "nodeCount", h.Nodes?.Count ?? 0 }
                }).Cast<object>().ToArray() },
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
        var profile = _mongo.CharacterSkillProfiles
            .Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, snapshot.CharacterId))
            .FirstOrDefault()?.Profile;
        var ranks = (profile?.Skills ?? new List<CharacterSkillProfileValue>())
            .ToDictionary(value => value.SkillId, value => Math.Max(0, Math.Min(20, value.Rank)), StringComparer.OrdinalIgnoreCase);
        return snapshot.Skills.Select(s =>
        {
            _skillsById.TryGetValue(s.SkillId, out var def);
            var acquiredNodeIds = snapshot.Directions
                .SelectMany(direction => direction.AcquiredNodes)
                .Select(node => node.NodeId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var sourceNode = _nodesById.Values
                .Where(node => acquiredNodeIds.Contains(node.NodeId))
                .Where(node => !ShouldHideNodeFromPlayer(node))
                .Where(node => node.UnlockSkillIds.Any(skillId => string.Equals(skillId, s.SkillId, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(node => node.Tier)
                .FirstOrDefault();
            var sourcePath = sourceNode == null
                ? null
                : _nodesById.Values
                    .Where(node => !ShouldHideNodeFromPlayer(node))
                    .Where(node => string.Equals(node.BranchId, sourceNode.BranchId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(node => node.Tier)
                    .ThenBy(node => node.SortOrder)
                    .FirstOrDefault();
            var rank = ranks.TryGetValue(s.SkillId, out var currentRank) ? currentRank : 0;
            var nextMilestone = def?.RankMilestones
                .Where(milestone => milestone.Rank > rank)
                .OrderBy(milestone => milestone.Rank)
                .FirstOrDefault();
            return new Dictionary<string, object>
            {
                { "skillId", s.SkillId },
                { "name", def != null ? def.Name : s.SkillId },
                { "sourcePathName", sourcePath == null ? string.Empty : FirstNonEmptyWorld(sourcePath.PublicName, sourcePath.Name) },
                { "sourceNodeName", sourceNode == null ? string.Empty : FirstNonEmptyWorld(sourceNode.PublicName, sourceNode.Name) },
                { "description", def != null ? def.Description : string.Empty },
                { "type", def != null ? def.Type.ToString() : string.Empty },
                { "available", s.Available },
                { "acquired", s.Acquired },
                { "reason", s.UnavailableReason },
                { "rank", rank },
                { "rankMax", def?.RankMax ?? 20 },
                { "masteryBand", CoreResolutionPolicy0219.MasteryBand(rank) },
                { "proficiencyBonus", CoreResolutionPolicy0219.MasteryBonus(rank) },
                { "defaultAttribute", def?.DefaultAttribute ?? string.Empty },
                { "defaultSubAttribute", def?.DefaultSubAttribute ?? string.Empty },
                { "nextMilestone", nextMilestone == null ? new Dictionary<string, object>() : new Dictionary<string, object>
                    {
                        { "rank", nextMilestone.Rank },
                        { "name", nextMilestone.DisplayName },
                        { "description", nextMilestone.PublicDescription },
                        { "requirement", RequirementExpressionPayload0219(nextMilestone.RequirementExpression, true) }
                    } },
                { "techniques", def == null ? Array.Empty<object>() : def.Techniques
                    .Where(technique => !technique.IsArchived)
                    .Select(technique => new Dictionary<string, object>
                    {
                        { "name", technique.DisplayName },
                        { "description", technique.PublicDescription },
                        { "minimumRank", technique.MinimumRank },
                        { "availableByRank", rank >= technique.MinimumRank && (!technique.MaximumRank.HasValue || rank <= technique.MaximumRank.Value) },
                        { "halfActionCost", technique.HalfActionCost },
                        { "reactionCost", technique.ReactionCost },
                        { "requirement", RequirementExpressionPayload0219(technique.RequirementExpression, true) }
                    }).Cast<object>().ToArray() },
                { "requirement", RequirementExpressionPayload0219(def?.RequirementExpression, true) },
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
            { "rankMin", def.RankMin },
            { "rankMax", def.RankMax },
            { "rankMilestones", def.RankMilestones.Select(milestone => new Dictionary<string, object>
                {
                    { "rank", milestone.Rank },
                    { "name", milestone.DisplayName },
                    { "publicDescription", milestone.PublicDescription },
                    { "gmDescription", milestone.GMDescription },
                    { "requirement", RequirementExpressionPayload0219(milestone.RequirementExpression, false) }
                }).Cast<object>().ToArray() },
            { "techniques", def.Techniques.Where(technique => !technique.IsArchived).Select(technique => new Dictionary<string, object>
                {
                    { "name", technique.DisplayName },
                    { "minimumRank", technique.MinimumRank },
                    { "maximumRank", technique.MaximumRank ?? 0 },
                    { "actionDefinitionId", technique.ActionDefinitionId },
                    { "halfActionCost", technique.HalfActionCost },
                    { "reactionCost", technique.ReactionCost },
                    { "publicDescription", technique.PublicDescription },
                    { "gmDescription", technique.GMDescription },
                    { "requirement", RequirementExpressionPayload0219(technique.RequirementExpression, false) }
                }).Cast<object>().ToArray() },
            { "requirement", RequirementExpressionPayload0219(def.RequirementExpression, false) },
            { "tags", def.Tags.Cast<object>().ToArray() },
            { "requirements", def.Requirements.Select(r => new Dictionary<string, object>{{"type",r.RequirementType},{"key",r.Key},{"value",r.Value}}).Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> RequirementExpressionPayload0219(RequirementExpression? expression, bool playerSafe)
    {
        if (expression == null) return new Dictionary<string, object>();
        var hidden = playerSafe && expression.IsHidden;
        return new Dictionary<string, object>
        {
            { "kind", expression.Kind },
            { "label", hidden ? "Скрытое условие" : FirstNonEmpty(expression.PublicLabel, "Условие развития") },
            { "leafType", hidden ? string.Empty : expression.LeafType },
            { "target", hidden ? string.Empty : expression.TargetId },
            { "minimumValue", hidden ? 0 : expression.MinimumValue },
            { "requiredCount", expression.RequiredCount },
            { "isHidden", hidden },
            { "children", expression.Children.Select(child => RequirementExpressionPayload0219(child, playerSafe)).Cast<object>().ToArray() }
        };
    }

    private Dictionary<string, object> NodePayload(ClassNodeDefinition n)
    {
        var linkedClass = ResolveClassDefinitionForNode(n.NodeId);
        var canonicalDirectionId = CanonicalDevelopmentDirectionId(EffectiveHexagonId(n), n);
        return new Dictionary<string, object>
        {
            { "nodeId", n.NodeId },
            { "hexagonId", string.IsNullOrWhiteSpace(n.HexagonId) ? "main_development_hexagon" : n.HexagonId },
            { "classId", linkedClass?.Code ?? n.ClassId ?? string.Empty },
            { "classCode", linkedClass?.Code ?? n.ClassId ?? string.Empty },
            { "classDisplayName", linkedClass?.Name ?? string.Empty },
            { "directionId", n.DirectionId },
            { "directionCode", n.DirectionId },
            { "canonicalDirectionId", canonicalDirectionId },
            { "branchId", n.BranchId },
            { "branchCode", n.BranchId },
            { "canonicalBranchId", FirstNonEmpty(n.BranchId, canonicalDirectionId) },
            { "name", n.Name },
            { "publicName", FirstNonEmpty(n.PublicName, n.Name) },
            { "description", n.Description },
            { "publicDescription", FirstNonEmpty(n.PublicDescription, n.Description) },
            { "nodeType", n.NodeType },
            { "nodeRole", n.NodeRole },
            { "nodeTypeLabel", FormatDevelopmentNodeType(n) },
            { "linkedDefinitionKind", n.LinkedDefinitionKind ?? string.Empty },
            { "linkedDefinitionId", n.LinkedDefinitionId ?? string.Empty },
            { "hexagonType", EffectiveHexagonType(n) },
            { "hexagonName", GetHexagonDisplayName(EffectiveHexagonId(n)) },
            { "isPrimaryMagicClass", IsPrimaryMagicClassNode(n) },
            { "primaryMagicGroupId", n.PrimaryMagicGroupId ?? string.Empty },
            { "magicRestrictionSummary", FirstNonEmpty(n.MagicRestrictionSummary, MagicPrimaryRestrictionSummary(n)) },
            { "tier", n.Tier },
            { "maxTier", n.MaxTier },
            { "costExperienceCoins", n.CostExperienceCoins },
            { "cost", n.CostExperienceCoins },
            { "currencyId", FirstNonEmpty(n.CurrencyId, CharacterCurrencyIds.XpCoin) },
            { "requiresGMApproval", n.RequiresGMApproval },
            { "requiresPlayerRequest", n.RequiresPlayerRequest },
            { "visibilityRule", n.VisibilityRule },
            { "isArchived", n.IsArchived },
            { "isHidden", n.IsHidden },
            { "isPlayerVisible", n.IsPlayerVisible && !ShouldHideNodeFromPlayer(n) },
            { "isVisibleToPlayer", n.IsPlayerVisible && !ShouldHideNodeFromPlayer(n) },
            { "isGMOnly", n.IsGMOnly || ShouldHideNodeFromPlayer(n) },
            { "requirementSummary", FirstNonEmpty(n.RequirementSummary, FormatRequirements(n.Requirements)) },
            { "rewardSummary", FirstNonEmpty(n.RewardSummary, FormatRewards(n)) },
            { "gridX", n.GridX },
            { "gridY", n.GridY },
            { "positionX", n.GridX },
            { "positionY", n.GridY },
            { "angle", n.Angle },
            { "ring", n.Ring },
            { "sector", n.Sector },
            { "sortOrder", n.SortOrder },
            { "layoutGroup", n.LayoutGroup ?? string.Empty },
            { "layoutLayer", n.LayoutLayer },
            { "layoutBranch", n.LayoutBranch ?? string.Empty },
            { "layoutWeight", n.LayoutWeight },
            { "layoutGeneratedBy", n.LayoutGeneratedBy ?? string.Empty },
            { "layoutGeneratedAtUtc", n.LayoutGeneratedAtUtc },
            { "layoutLockedManualPosition", n.LayoutLockedManualPosition },
            { "layoutPresetId", n.LayoutPresetId ?? string.Empty },
            { "layoutSnapshotId", n.LayoutSnapshotId ?? string.Empty },
            { "requiredNodeIds", GetRequiredNodeIds(n).Cast<object>().ToArray() },
            { "linkedNodeIds", GetRequiredNodeIds(n).Cast<object>().ToArray() },
            { "linkedClassId", linkedClass?.Code ?? n.ClassId ?? string.Empty },
            { "layoutVersion", Math.Max(1, n.LayoutVersion) },
            { "revision", Math.Max(1, n.Revision) },
            { "updatedAtUtc", n.UpdatedAtUtc },
            { "updatedByUserId", n.UpdatedByUserId ?? string.Empty },
            { "schemaVersion", Math.Max(1, n.SchemaVersion) },
            { "nextNodeIds", n.NextNodeIds.Cast<object>().ToArray() },
            { "unlockSkillIds", n.UnlockSkillIds.Cast<object>().ToArray() },
            { "requirements", n.Requirements.Select(r => new Dictionary<string, object>{{"type",r.RequirementType},{"key",r.Key},{"value",r.Value}}).Cast<object>().ToArray() },
            { "statBonuses", n.StatBonuses.Select(s => new Dictionary<string, object>{{"stat",s.Stat},{"bonus",s.Bonus}}).Cast<object>().ToArray() },
            { "passiveEffects", n.PassiveEffects.Select(p => new Dictionary<string, object>{{"effectId",p.EffectId},{"description",p.Description}}).Cast<object>().ToArray() }
        };
    }

    private ClassDefinition? ResolveClassDefinitionForNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return null;
        return _repositories.ClassDefinitions.GetAll(includeArchived: false)
            .Where(x => x.IsActive && x.Status != DefinitionStatus.Archived)
            .FirstOrDefault(x => string.Equals(x.RequiredNodeId, nodeId, StringComparison.OrdinalIgnoreCase));
    }

    private void UpsertDevelopmentProfileNode(Character character, ClassNodeDefinition node, string actorId, string source = "admin_hexagon_gui", string operationId = "")
    {
        if (character == null || node == null || string.IsNullOrWhiteSpace(node.NodeId)) return;

        var filter = Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id);
        var document = _mongo.CharacterDevelopmentProfiles.Find(filter).FirstOrDefault()
            ?? new CharacterDevelopmentProfileDocument
            {
                Id = Guid.NewGuid().ToString("N"),
                CharacterId = character.Id,
                Profile = new DevelopmentProfile
                {
                    CharacterId = character.Id,
                    RuleSetId = RuleSetIds.FantasyNriDefault,
                    SchemaVersion = 1
                }
            };

        document.Profile ??= new DevelopmentProfile
        {
            CharacterId = character.Id,
            RuleSetId = RuleSetIds.FantasyNriDefault,
            SchemaVersion = 1
        };
        document.Profile.CharacterId = character.Id;
        document.Profile.RuleSetId = string.IsNullOrWhiteSpace(document.Profile.RuleSetId) ? RuleSetIds.FantasyNriDefault : document.Profile.RuleSetId;
        document.Profile.SchemaVersion = Math.Max(1, document.Profile.SchemaVersion);
        document.Profile.ActiveHexagonId = string.IsNullOrWhiteSpace(document.Profile.ActiveHexagonId) ? DevelopmentHexagonIds.Main : document.Profile.ActiveHexagonId;
        document.Profile.UpdatedAtUtc = DateTime.UtcNow;
        document.Profile.Revision = Math.Max(0, document.Profile.Revision) + 1;
        document.Profile.RecentOperationIds ??= new List<string>();
        if (!string.IsNullOrWhiteSpace(operationId) && !document.Profile.RecentOperationIds.Contains(operationId, StringComparer.Ordinal))
        {
            document.Profile.RecentOperationIds.Add(operationId);
            if (document.Profile.RecentOperationIds.Count > 64)
                document.Profile.RecentOperationIds.RemoveRange(0, document.Profile.RecentOperationIds.Count - 64);
        }

        var hexagonId = string.IsNullOrWhiteSpace(node.HexagonId) ? "main_development_hexagon" : node.HexagonId;
        if (!document.Profile.ActiveHexagonIds.Any(x => string.Equals(x, hexagonId, StringComparison.OrdinalIgnoreCase)))
        {
            document.Profile.ActiveHexagonIds.Add(hexagonId);
        }

        var state = document.Profile.Nodes.FirstOrDefault(x => string.Equals(x.DevelopmentNodeId, node.NodeId, StringComparison.OrdinalIgnoreCase));
        if (state == null)
        {
            state = new CharacterDevelopmentNodeState
            {
                Id = Guid.NewGuid().ToString("N"),
                CharacterId = character.Id,
                DevelopmentNodeId = node.NodeId,
                PurchasedAtUtc = DateTime.UtcNow
            };
            document.Profile.Nodes.Add(state);
        }

        state.CharacterId = character.Id;
        state.HexagonId = hexagonId;
        state.DevelopmentNodeId = node.NodeId;
        state.ClassId = string.IsNullOrWhiteSpace(node.ClassId) ? node.NodeId : node.ClassId;
        state.NodeType = string.IsNullOrWhiteSpace(node.NodeType) ? DevelopmentNodeTypes.Class : node.NodeType;
        state.CurrentTier = Math.Max(1, Math.Max(state.CurrentTier, node.Tier));
        state.MaxTier = Math.Max(state.CurrentTier, Math.Max(1, node.MaxTier));
        state.IsUnlocked = true;
        state.IsPurchased = true;
        state.IsAvailable = true;
        state.IsHidden = false;
        state.State = state.IsCompleted ? "completed" : "purchased";
        if (source != null && source.IndexOf("purchase", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            state.CostPaid = Math.Max(state.CostPaid, Math.Max(0, node.CostExperienceCoins));
        }
        state.CurrencyId = string.IsNullOrWhiteSpace(node.CurrencyId) ? CharacterCurrencyIds.XpCoin : node.CurrencyId;
        state.Source = string.IsNullOrWhiteSpace(source) ? "admin_hexagon_gui" : source;
        state.GMApprovalStatus = "approved";
        state.Notes = string.IsNullOrWhiteSpace(state.Notes) ? $"Unlocked by {actorId} through Development Hexagon." : state.Notes;
        state.PurchasedAtUtc = state.PurchasedAtUtc == default ? DateTime.UtcNow : state.PurchasedAtUtc;
        state.UpdatedAtUtc = DateTime.UtcNow;

        SyncDevelopmentProfileHexagons(document.Profile);
        _mongo.CharacterDevelopmentProfiles.ReplaceOne(filter, document, new ReplaceOptions { IsUpsert = true });
        ApplyDevelopmentTitleRewards02111(character, node, actorId);
    }

    private void ApplyDevelopmentTitleRewards02111(Character character, ClassNodeDefinition node, string actorId)
    {
        var titleIds = string.Equals(node.LinkedDefinitionKind, "title_definition", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(node.LinkedDefinitionId)
            ? new[] { node.LinkedDefinitionId }
            : Array.Empty<string>();
        if (titleIds.Length == 0) return;
        var profile = _mongo.CharacterTitleProfiles.Find(x => x.CharacterId == character.Id).FirstOrDefault()
            ?? new CharacterTitleProfileDocument { CharacterId = character.Id, RuleSetId = RuleSetIds.FantasyNriDefault };
        var changed = false;
        foreach (var titleId in titleIds)
        {
            if (CharacterTitleDefinitions02111(profile.RuleSetId).All(x => x.DefinitionId != titleId || x.IsArchived)) continue;
            var entitlement = profile.Entitlements.FirstOrDefault(x => string.Equals(x.TitleId, titleId, StringComparison.Ordinal));
            if (entitlement == null)
            {
                profile.Entitlements.Add(new CharacterTitleEntitlement { TitleId = titleId, GrantSourceType = "development", GrantSourceId = node.NodeId, GrantedByUserId = actorId });
                changed = true;
            }
            else if (entitlement.IsRevoked)
            {
                entitlement.IsRevoked = false;
                entitlement.GrantSourceType = "development";
                entitlement.GrantSourceId = node.NodeId;
                entitlement.GrantedByUserId = actorId;
                entitlement.GrantedAtUtc = DateTime.UtcNow;
                changed = true;
            }
        }
        if (!changed) return;
        profile.EntityRevision++;
        profile.UpdatedUtc = DateTime.UtcNow;
        _mongo.CharacterTitleProfiles.ReplaceOne(x => x.CharacterId == character.Id, profile, new ReplaceOptions { IsUpsert = true });
    }

    private void MarkDevelopmentProfileNodeCompleted(Character character, ClassNodeDefinition node, string actorId)
    {
        if (character == null || node == null || string.IsNullOrWhiteSpace(node.NodeId)) return;
        var filter = Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id);
        var document = _mongo.CharacterDevelopmentProfiles.Find(filter).FirstOrDefault();
        if (document?.Profile?.Nodes == null) return;

        var state = document.Profile.Nodes.FirstOrDefault(x => string.Equals(x.DevelopmentNodeId, node.NodeId, StringComparison.OrdinalIgnoreCase));
        if (state == null) return;

        state.CharacterId = character.Id;
        state.HexagonId = EffectiveHexagonId(node);
        state.NodeType = string.IsNullOrWhiteSpace(node.NodeType) ? DevelopmentNodeTypes.Class : node.NodeType;
        state.CurrentTier = Math.Max(Math.Max(1, node.MaxTier), Math.Max(state.CurrentTier, state.MaxTier));
        state.MaxTier = Math.Max(state.CurrentTier, Math.Max(1, node.MaxTier));
        state.IsUnlocked = true;
        state.IsPurchased = true;
        state.IsCompleted = true;
        state.State = "completed";
        state.Source = "admin_hexagon_complete";
        state.GMApprovalStatus = "completed";
        state.Notes = "Completed by " + actorId + " through Development Hexagon.";
        state.UpdatedAtUtc = DateTime.UtcNow;
        document.Profile.UpdatedAtUtc = DateTime.UtcNow;
        document.Profile.Revision = Math.Max(0, document.Profile.Revision) + 1;
        SyncDevelopmentProfileHexagons(document.Profile);
        _mongo.CharacterDevelopmentProfiles.ReplaceOne(filter, document, new ReplaceOptions { IsUpsert = true });
    }

    private void SyncDevelopmentProfileHexagons(DevelopmentProfile profile)
    {
        if (profile == null) return;
        profile.Hexagons ??= new List<CharacterDevelopmentHexagonState>();
        profile.Nodes ??= new List<CharacterDevelopmentNodeState>();
        profile.ActiveHexagonIds ??= new List<string>();

        EnsureProfileHexagon(profile, DevelopmentHexagonIds.Main, DevelopmentHexagonTypes.Main, GetHexagonDisplayName(DevelopmentHexagonIds.Main), true, 1);
        EnsureProfileHexagon(profile, DevelopmentHexagonIds.Magic, DevelopmentHexagonTypes.Magic, GetHexagonDisplayName(DevelopmentHexagonIds.Magic), false, 2);

        foreach (var node in profile.Nodes)
        {
            var hexagonId = string.IsNullOrWhiteSpace(node.HexagonId) ? DevelopmentHexagonIds.Main : node.HexagonId;
            if (!profile.ActiveHexagonIds.Any(x => string.Equals(x, hexagonId, StringComparison.OrdinalIgnoreCase)))
                profile.ActiveHexagonIds.Add(hexagonId);
        }

        foreach (var hexagon in profile.Hexagons)
        {
            hexagon.Nodes = profile.Nodes
                .Where(n => string.Equals(string.IsNullOrWhiteSpace(n.HexagonId) ? DevelopmentHexagonIds.Main : n.HexagonId, hexagon.HexagonId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.DevelopmentNodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            hexagon.IsUnlocked = hexagon.IsUnlocked || hexagon.Nodes.Any(n => n.IsPurchased || n.IsUnlocked || n.CurrentTier > 0);
        }

        profile.TotalXpSpent = profile.Nodes.Where(n => n.IsPurchased || n.IsUnlocked).Sum(n => Math.Max(0, n.CostPaid));
        profile.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void EnsureProfileHexagon(DevelopmentProfile profile, string hexagonId, string hexagonType, string displayName, bool isMain, int sortOrder)
    {
        var state = profile.Hexagons.FirstOrDefault(x => string.Equals(x.HexagonId, hexagonId, StringComparison.OrdinalIgnoreCase));
        if (state == null)
        {
            state = new CharacterDevelopmentHexagonState { HexagonId = hexagonId };
            profile.Hexagons.Add(state);
        }

        state.HexagonType = string.IsNullOrWhiteSpace(state.HexagonType) ? hexagonType : state.HexagonType;
        state.DisplayName = string.IsNullOrWhiteSpace(state.DisplayName) ? displayName : state.DisplayName;
        state.IsMain = isMain;
        state.IsPlayerVisible = true;
        state.SortOrder = sortOrder;
        if (!profile.ActiveHexagonIds.Any(x => string.Equals(x, hexagonId, StringComparison.OrdinalIgnoreCase)))
            profile.ActiveHexagonIds.Add(hexagonId);
    }

    private void RemoveDevelopmentProfileNode(Character character, string nodeId)
    {
        if (character == null || string.IsNullOrWhiteSpace(nodeId)) return;

        var filter = Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id);
        var document = _mongo.CharacterDevelopmentProfiles.Find(filter).FirstOrDefault();
        if (document?.Profile?.Nodes == null) return;

        var removed = document.Profile.Nodes.RemoveAll(x => string.Equals(x.DevelopmentNodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            document.Profile.UpdatedAtUtc = DateTime.UtcNow;
            document.Profile.Revision = Math.Max(0, document.Profile.Revision) + 1;
            SyncDevelopmentProfileHexagons(document.Profile);
            _mongo.CharacterDevelopmentProfiles.ReplaceOne(filter, document, new ReplaceOptions { IsUpsert = true });
        }
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

