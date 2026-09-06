using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private readonly object _developmentProductMutationSync0215 = new object();
    private const int DevelopmentProductOverviewLimit0215 = DevelopmentProductProjectionPolicy0215.OverviewLimit;

    public ResponseEnvelope DevelopmentHexagonPlayerGetProductProjection(CommandContext context)
    {
        if (!DevelopmentPlayerEnabled()) return DevelopmentDisabled();
        var actor = GetCurrentAccount(context);
        var character = ResolveCharacterForClassSkill(context, actor);
        return Ok("Development map loaded.", BuildDevelopmentProductProjection0215(context, character));
    }

    public ResponseEnvelope DevelopmentHexagonAdminGetProductPreview(CommandContext context)
    {
        if (!DevelopmentAdminEnabled()) return DevelopmentDisabled();
        RequireAdmin(context);
        var character = ResolveCharacterForClassSkill(context, GetCurrentAccount(context));
        var projection = BuildDevelopmentProductProjection0215(context, character);
        projection["previewMode"] = "player_safe";
        return Ok("Player-safe development preview loaded.", projection);
    }

    public ResponseEnvelope DevelopmentHexagonPlayerAdvanceProductPath(CommandContext context)
    {
        if (!DevelopmentPlayerEnabled()) return DevelopmentDisabled();
        if (!DevelopmentNodePurchaseEnabled()) return DevelopmentDisabled("Покупка развития выключена feature flags.");

        var actor = GetCurrentAccount(context);
        var character = ResolveCharacterForClassSkill(context, actor);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        var presentationKey = RequireLength(PayloadReader.GetString(context.Request.Payload, "presentationKey"), 1, 320, "presentationKey");
        var operationId = RequireLength(PayloadReader.GetString(context.Request.Payload, "operationId"), 8, 160, "operationId");
        var expectedRevision = PayloadReader.GetInt(context.Request.Payload, "expectedRevision")
            ?? throw new ArgumentException("expectedRevision is required.");

        lock (_developmentProductMutationSync0215)
        {
            EnsureDefinitionsLoaded(false);
            if (IsDevelopmentAdminOnlyHexagon(hexagonId)) throw new KeyNotFoundException("Development map not found.");
            if (!IsHexagonEnabled(hexagonId)) return DevelopmentDisabled("Запрошенный шестиугольник развития выключен feature flags.");

            var profileDocument = LoadDevelopmentProfileDocument0215(character.Id);
            var profile = profileDocument?.Profile;
            var mutationDecision = DevelopmentProductMutationGuard0215.Evaluate(profile?.Revision ?? 0, expectedRevision, profile?.RecentOperationIds ?? new List<string>(), operationId);
            if (mutationDecision == DevelopmentProductMutationDecision0215.Replay)
            {
                var replay = BuildDevelopmentProductProjection0215(context, character);
                replay["alreadyApplied"] = true;
                replay["operationId"] = operationId;
                return Ok("Development already updated.", replay);
            }

            var currentRevision = profile?.Revision ?? 0;
            if (mutationDecision == DevelopmentProductMutationDecision0215.Conflict)
                return Error($"Развитие персонажа изменилось. Обновите карту и повторите действие. Текущая редакция: {currentRevision}.", ResponseStatus.Conflict, ErrorCode.Conflict);

            var groups = BuildDevelopmentProductGroups0215(hexagonId, character);
            var group = groups.FirstOrDefault(item => string.Equals(item.PresentationKey, presentationKey, StringComparison.Ordinal));
            if (group == null) throw new KeyNotFoundException("Development path not found.");
            var next = group.NextNode ?? throw new InvalidOperationException("Путь уже завершён или пока не имеет доступного продолжения.");
            if (ShouldHideNodeFromPlayer(next) || IsDevelopmentDiagnosticLayoutNode(next))
                throw new KeyNotFoundException("Development path not found.");
            if (FindNodeState(character, next.NodeId) != null)
                throw new InvalidOperationException("Следующий этап пути уже приобретён.");
            if (DevelopmentApprovalPolicy.RequiresGMApproval(next) || DevelopmentApprovalPolicy.RequiresPlayerRequest(next))
                throw new InvalidOperationException("Для этого шага требуется решение мастера.");
            if (!IsDevelopmentCostResolved0215(next))
                throw new InvalidOperationException("Стоимость развития пока не утверждена.");

            var snapshot = RecalculateProgress(character);
            var reasons = EvaluateNodeAvailability(character, next, snapshot);
            if (reasons.Count > 0)
                throw new InvalidOperationException("Пока недоступно: " + string.Join("; ", reasons));

            var nextCost = Math.Max(0, next.CostExperienceCoins);
            if (DevelopmentExperienceCoinsEnabled() && !DevelopmentProductProjectionPolicy0215.CanAfford(character.XpCoins, nextCost))
                return Error($"Недостаточно монет опыта. Доступно: {character.XpCoins}, требуется: {nextCost}.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

            ValidateMagicPrimaryPurchase(character, next);
            SpendExperienceCoinsForNode(character, actor, next, operationId);
            UpsertDevelopmentProfileNode(character, next, actor.Id, "player_product_path_purchase", operationId);
            _repositories.Characters.Replace(character);
            WriteAudit("developmentProduct", actor.Id, "advancePath", character.Id + ":" + presentationKey);
            TryPublishDevelopmentSync(character, "development.path.advanced", actor.Id, operationId);
            TryWriteDevelopmentJournal(character, actor.Id, "development.path.advanced", $"Продвижение по пути: {SafeNodeName(next, includeAdmin: false)}");

            var result = BuildDevelopmentProductProjection0215(context, character);
            result["alreadyApplied"] = false;
            result["operationId"] = operationId;
            result["advancedPresentationKey"] = presentationKey;
            return Ok("Путь развития обновлён.", result);
        }
    }

    private Dictionary<string, object> BuildDevelopmentProductProjection0215(CommandContext context, Character character)
    {
        EnsureDefinitionsLoaded(false);
        EnsureProgressInitialized(character);
        var hexagonId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "hexagonId"), DevelopmentHexagonIds.Main);
        if (IsDevelopmentAdminOnlyHexagon(hexagonId)) throw new KeyNotFoundException("Development map not found.");
        if (!IsHexagonEnabled(hexagonId)) throw new InvalidOperationException("Development map is disabled.");

        var mode = NormalizeDevelopmentProductMode0215(PayloadReader.GetString(context.Request.Payload, "viewMode"));
        var directionKey = PayloadReader.GetString(context.Request.Payload, "directionKey") ?? string.Empty;
        var pathKey = PayloadReader.GetString(context.Request.Payload, "pathKey") ?? string.Empty;
        var groups = BuildDevelopmentProductGroups0215(hexagonId, character);
        var profile = LoadDevelopmentProfileDocument0215(character.Id)?.Profile;
        var profileRevision = profile?.Revision ?? 0;
        var acquiredIds = GetPurchasedDevelopmentNodeIds(character);

        var items = new List<Dictionary<string, object>>();
        items.Add(BuildDevelopmentProductRoot0215(hexagonId, acquiredIds));
        var directions = BuildDevelopmentProductDirections0215(hexagonId, groups, acquiredIds).ToList();

        IEnumerable<DevelopmentProductGroup0215> selectedGroups;
        if (mode == "direction")
            selectedGroups = groups.Where(group => string.Equals(group.DirectionKey, directionKey, StringComparison.OrdinalIgnoreCase));
        else if (mode == "path" || mode == "mixed_path")
            selectedGroups = SelectDevelopmentProductPathContext0215(groups, pathKey);
        else if (mode == "my_route")
            selectedGroups = groups.Where(group => group.CurrentTier > 0 || group.IsAcquired).OrderByDescending(group => group.CurrentTier).ThenBy(group => group.SortOrder);
        else if (mode == "available_now")
            selectedGroups = groups.Where(group => group.CanAdvance).OrderBy(group => group.SortOrder);
        else
        {
            selectedGroups = string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase)
                ? groups.Where(IsCanonicalMagicOverviewGroup0215)
                : groups.Where(IsCanonicalBaseClassGroup0215);
        }

        if (mode == "overview") items.AddRange(directions);
        else if (!string.IsNullOrWhiteSpace(directionKey))
            items.AddRange(directions.Where(direction => string.Equals(Convert.ToString(direction["directionId"]), directionKey, StringComparison.OrdinalIgnoreCase)));
        else items.AddRange(directions.Where(direction => GetBool0215(direction, "acquired")));

        var pathOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in selectedGroups.OrderBy(group => group.DirectionOrder).ThenBy(group => group.SortOrder).Take(DevelopmentProductOverviewLimit0215 - items.Count))
        {
            pathOrdinals.TryGetValue(group.DirectionKey, out var pathOrdinal);
            items.Add(BuildDevelopmentProductPathPayload0215(group, pathOrdinal, groups));
            pathOrdinals[group.DirectionKey] = pathOrdinal + 1;
        }

        var warnings = new List<object>();
        if (groups.Count == 0) warnings.Add("Для этой карты развития пока не настроены доступные игроку пути.");
        items = DevelopmentProductProjectionPolicy0215.BoundOverview(items).ToList();

        var hexagon = DevelopmentHexagonPayload(hexagonId, includeAdmin: false);
        hexagon["name"] = GetHexagonDisplayName(hexagonId);
        hexagon["displayName"] = GetHexagonDisplayName(hexagonId);
        hexagon["centerNodeId"] = "product_root:" + hexagonId;
        hexagon["directions"] = directions.Select(direction => new Dictionary<string, object>
        {
            { "directionId", Convert.ToString(direction["directionId"]) ?? string.Empty },
            { "name", Convert.ToString(direction["name"]) ?? string.Empty },
            { "atmosphericName", string.Empty },
            { "displayOrder", Convert.ToInt32(direction["sector"]) },
            { "angleDegrees", (Convert.ToInt32(direction["sector"]) - 1) * 60 - 90 }
        }).Cast<object>().ToArray();

        return new Dictionary<string, object>
        {
            { "projectionKind", "development_product_v1" },
            { "viewMode", mode },
            { "hexagonId", hexagonId },
            { "graphTitle", GetHexagonDisplayName(hexagonId) },
            { "hexagon", hexagon },
            { "hexagons", DevelopmentHexagonsPayload(includeAdmin: false).Cast<object>().ToArray() },
            { "rootLabel", DevelopmentProductRootLabel0215(hexagonId) },
            { "profileRevision", profileRevision },
            { "xpCoins", character.XpCoins },
            { "items", items.Cast<object>().ToArray() },
            { "nodes", items.Cast<object>().ToArray() },
            { "directions", directions.Cast<object>().ToArray() },
            { "visibleItemCount", items.Count },
            { "overviewLimit", DevelopmentProductOverviewLimit0215 },
            { "isBounded", items.Count <= DevelopmentProductOverviewLimit0215 },
            { "isPlayerSafe", true },
            { "sourceOfTruth", "class_tree_definitions" },
            { "characterStateSource", "character_development_profiles" },
            { "warnings", warnings.ToArray() },
            { "builtAtUtc", DateTime.UtcNow }
        };
    }

    private List<DevelopmentProductGroup0215> BuildDevelopmentProductGroups0215(string hexagonId, Character character)
    {
        var nodes = _nodesById.Values
            .Where(node => string.Equals(EffectiveHexagonId(node), hexagonId, StringComparison.OrdinalIgnoreCase))
            .Where(node => !ShouldHideNodeFromPlayer(node) && !IsDevelopmentDiagnosticLayoutNode(node) && !IsDevelopmentRootNode(node))
            .OrderBy(node => node.SortOrder).ThenBy(node => node.Tier).ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var acquired = GetPurchasedDevelopmentNodeIds(character);
        var snapshot = RecalculateProgress(character);

        return nodes
            .GroupBy(node => DevelopmentProductPathKey0215(hexagonId, node), StringComparer.Ordinal)
            .Select(group =>
            {
                var ordered = group.OrderBy(node => Math.Max(1, node.Tier)).ThenBy(node => node.SortOrder).ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase).ToList();
                var acquiredNodes = ordered.Where(node => acquired.Contains(node.NodeId)).ToList();
                var next = ordered.FirstOrDefault(node => !acquired.Contains(node.NodeId));
                var representative = acquiredNodes.LastOrDefault() ?? next ?? ordered[0];
                var currentTier = acquiredNodes.Count == 0 ? 0 : acquiredNodes.Max(node => Math.Max(1, node.Tier));
                var maxTier = Math.Max(ordered.Max(node => Math.Max(1, node.MaxTier)), ordered.Max(node => Math.Max(1, node.Tier)));
                var isCompleted = next == null && currentTier >= maxTier;
                var reasons = next == null ? new List<string>() : EvaluateNodeAvailability(character, next, snapshot);
                var requiresGmApproval = next != null && DevelopmentApprovalPolicy.RequiresGMApproval(next);
                var requiresPlayerRequest = next != null && DevelopmentApprovalPolicy.RequiresPlayerRequest(next);
                var costResolved = IsDevelopmentCostResolved0215(next ?? representative);
                var nextCost = next == null ? 0 : Math.Max(0, next.CostExperienceCoins);
                var canAfford = next == null || !DevelopmentExperienceCoinsEnabled() || DevelopmentProductProjectionPolicy0215.CanAfford(character.XpCoins, nextCost);
                return new DevelopmentProductGroup0215
                {
                    PresentationKey = group.Key,
                    HexagonId = hexagonId,
                    DirectionKey = CanonicalDevelopmentDirectionId(hexagonId, representative),
                    DirectionOrder = DevelopmentProductDirectionOrder0215(hexagonId, CanonicalDevelopmentDirectionId(hexagonId, representative)),
                    Title = DevelopmentProductReadableName0215(FirstNonEmpty(representative.PublicName, representative.Name), CanonicalDevelopmentDirectionId(hexagonId, representative), representative.BranchId),
                    Description = DevelopmentProductReadableDescription0215(FirstNonEmpty(representative.PublicDescription, representative.Description)),
                    PresentationKind = DevelopmentProductPresentationKind0215(representative, ordered.Count),
                    CurrentTier = currentTier,
                    MaxTier = maxTier,
                    IsAcquired = acquiredNodes.Count > 0,
                    IsCompleted = isCompleted,
                    CanAdvance = next != null && costResolved && !requiresGmApproval && !requiresPlayerRequest && reasons.Count == 0 && canAfford,
                    RequiresGMApproval = requiresGmApproval,
                    RequiresPlayerRequest = requiresPlayerRequest,
                    IsCostResolved = costResolved,
                    NextNode = next,
                    NextCost = nextCost,
                    VisibleRankMin = ordered.Min(node => Math.Max(1, node.Tier)),
                    RequirementSummary = !costResolved
                            ? "Стоимость развития пока не утверждена."
                        : isCompleted
                            ? "Путь завершён."
                        : next == null
                            ? "Следующая ступень пути пока не определена."
                        : !canAfford
                            ? $"Недостаточно монет опыта: доступно {character.XpCoins}, требуется {nextCost}."
                            : FirstNonEmpty(next.RequirementSummary, reasons.Count == 0 ? "Все требования выполнены." : string.Join("; ", reasons)),
                    RewardSummary = !costResolved
                        ? "Награда за следующую ступень пока не утверждена."
                        : isCompleted
                        ? "Все этапы пути освоены."
                        : next == null
                            ? "Следующая награда пути пока не определена."
                            : FirstNonEmpty(next.RewardSummary, FormatRewards(next), "Открывает следующий этап пути."),
                    KnownDecisionSummary = DevelopmentProductKnownDecisionSummary0215(representative),
                    SortOrder = ordered.Min(node => node.SortOrder),
                    GroupedNodeCount = ordered.Count,
                    MilestoneTiers = ordered.Select(node => Math.Max(1, node.Tier)).Where(tier => tier % 5 == 0 || tier == 20).Distinct().OrderBy(tier => tier).ToArray()
                    ,RepresentativeNode = representative
                };
            })
            .OrderBy(group => group.DirectionOrder).ThenBy(group => group.SortOrder).ThenBy(group => group.PresentationKey, StringComparer.Ordinal)
            .ToList();
    }

    private Dictionary<string, object> BuildDevelopmentProductRoot0215(string hexagonId, HashSet<string> acquiredIds)
    {
        var rootId = string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase) ? "magic_awakened" : "novice";
        return new Dictionary<string, object>
        {
            { "nodeId", "product_root:" + hexagonId }, { "presentationKey", "product_root:" + hexagonId },
            { "presentationKind", "Root" }, { "name", DevelopmentProductRootLabel0215(hexagonId) },
            { "description", string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase) ? "Начало магического развития." : "Начало пути персонажа." },
            { "hexagonId", hexagonId }, { "canonicalDirectionId", "root" }, { "directionId", "root" }, { "branchId", "root" },
            { "tier", 0 }, { "currentTier", acquiredIds.Contains(rootId) ? 1 : 0 }, { "maxTier", 1 },
            { "acquired", acquiredIds.Contains(rootId) }, { "isPurchased", acquiredIds.Contains(rootId) }, { "available", false }, { "canPurchase", false },
            { "state", acquiredIds.Contains(rootId) ? "completed" : "start" }, { "cost", 0 }, { "costExperienceCoins", 0 },
            { "requirementSummary", "Начальная точка развития." }, { "rewardSummary", "Открывает направления развития." },
            { "nodeType", "root" }, { "nodeTypeLabel", "Начало" }, { "sortOrder", 0 }, { "ring", 0 }, { "sector", 0 },
            { "positionX", 5890 }, { "positionY", 5946 }, { "gridX", 5890 }, { "gridY", 5946 }
        };
    }

    private IEnumerable<Dictionary<string, object>> BuildDevelopmentProductDirections0215(string hexagonId, IEnumerable<DevelopmentProductGroup0215> groups, HashSet<string> acquiredIds)
    {
        var keys = CanonicalDevelopmentDirectionIds(hexagonId);
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            var directionGroups = groups.Where(group => string.Equals(group.DirectionKey, key, StringComparison.OrdinalIgnoreCase)).ToList();
            var acquired = directionGroups.Any(group => group.IsAcquired);
            yield return new Dictionary<string, object>
            {
                { "nodeId", "product_direction:" + hexagonId + ":" + key }, { "presentationKey", "product_direction:" + hexagonId + ":" + key },
                { "presentationKind", "Direction" }, { "name", DevelopmentProductDirectionLabel0215(hexagonId, key) },
                { "description", DevelopmentProductDirectionSummary0215(hexagonId, key) }, { "hexagonId", hexagonId },
                { "canonicalDirectionId", key }, { "directionId", key }, { "branchId", key }, { "tier", 0 }, { "currentTier", directionGroups.Select(group => group.CurrentTier).DefaultIfEmpty(0).Max() },
                { "maxTier", directionGroups.Select(group => group.MaxTier).DefaultIfEmpty(20).Max() }, { "acquired", acquired }, { "isPurchased", acquired },
                { "available", directionGroups.Any(group => group.CanAdvance) }, { "canPurchase", false }, { "state", acquired ? "active" : "available" },
                { "cost", 0 }, { "costExperienceCoins", 0 }, { "requirementSummary", "Выберите путь в этом направлении." },
                { "rewardSummary", directionGroups.Count == 0 ? "Пути пока не настроены." : $"Доступно путей: {directionGroups.Count}." },
                { "nodeType", "direction" }, { "nodeTypeLabel", "Направление" }, { "sortOrder", 10 + index }, { "ring", 1 }, { "sector", index + 1 }
            };
        }
    }

    private Dictionary<string, object> BuildDevelopmentProductPathPayload0215(DevelopmentProductGroup0215 group, int pathOrdinal, IReadOnlyList<DevelopmentProductGroup0215> allGroups)
    {
        var state = group.IsCompleted ? "completed" : group.CanAdvance ? "available" : group.IsAcquired ? "active" : "locked";
        var angleRadians = (group.DirectionOrder * 60d - 90d) * Math.PI / 180d;
        // Product projection is deliberately compact. The canonical persisted graph
        // remains available to Admin diagnostic mode, while players get a readable
        // root/direction/path overview without zooming a 12k workspace.
        var radius = 240d + Math.Max(0, pathOrdinal) * 60d;
        var positionX = (int)Math.Round(6000d + Math.Cos(angleRadians) * radius - 90d);
        var positionY = (int)Math.Round(6000d + Math.Sin(angleRadians) * radius - 62d);
        var requiredNodeIds = ResolveDevelopmentProductRequiredPresentationKeys0215(group, allGroups).Cast<object>().ToArray();
        return new Dictionary<string, object>
        {
            { "nodeId", group.PresentationKey }, { "presentationKey", group.PresentationKey }, { "presentationKind", group.PresentationKind },
            { "name", group.Title }, { "description", group.Description }, { "hexagonId", group.HexagonId }, { "canonicalDirectionId", group.DirectionKey },
            { "directionId", group.DirectionKey }, { "branchId", group.PresentationKey }, { "tier", group.CurrentTier }, { "currentTier", group.CurrentTier }, { "maxTier", group.MaxTier },
            { "nextTier", group.IsCompleted ? group.CurrentTier : Math.Max(group.CurrentTier + 1, group.NextNode?.Tier ?? 1) },
            { "acquired", group.IsAcquired }, { "isPurchased", group.IsAcquired }, { "available", group.CanAdvance }, { "canPurchase", group.CanAdvance },
            { "requiresGMApproval", group.RequiresGMApproval }, { "requiresPlayerRequest", group.RequiresPlayerRequest }, { "state", state },
            { "cost", group.NextCost }, { "costExperienceCoins", group.NextCost }, { "currencyId", group.NextNode?.CurrencyId ?? CharacterCurrencyIds.XpCoin },
            { "costResolved", group.IsCostResolved },
            { "costDisplay", group.IsCostResolved ? FormatExperienceCoinCost0215(group.NextCost) : "Стоимость развития пока не утверждена." },
            { "visibleRankMin", group.VisibleRankMin }, { "visibleRankMax", group.MaxTier },
            { "knownDecisionSummary", group.KnownDecisionSummary },
            { "requirementSummary", group.RequirementSummary }, { "rewardSummary", group.RewardSummary }, { "nodeType", group.PresentationKind.ToLowerInvariant() },
            { "nodeTypeLabel", DevelopmentProductKindLabel0215(group.PresentationKind) }, { "sortOrder", group.SortOrder }, { "ring", 2 }, { "sector", group.DirectionOrder + 1 },
            { "positionX", positionX }, { "positionY", positionY }, { "gridX", positionX }, { "gridY", positionY },
            { "groupedProgressionCount", group.GroupedNodeCount }, { "milestoneTiers", group.MilestoneTiers.Cast<object>().ToArray() },
            { "canonicalNodeId", group.RepresentativeNode?.NodeId ?? string.Empty },
            { "canonicalDefinitionId", ResolveCanonicalDevelopmentDefinitionId0215(group.RepresentativeNode) },
            { "requiredNodeIds", requiredNodeIds },
            { "requiredCanonicalNodeIds", RequirementTargets0215(group.RepresentativeNode?.RequirementExpression).Distinct(StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray() },
            { "isPlayerVisible", true }, { "isVisibleToPlayer", true }
        };
    }

    private static IEnumerable<DevelopmentProductGroup0215> SelectDevelopmentProductPathContext0215(
        IReadOnlyList<DevelopmentProductGroup0215> groups,
        string pathKey)
    {
        var selected = groups.FirstOrDefault(group => string.Equals(group.PresentationKey, pathKey, StringComparison.Ordinal));
        if (selected?.RepresentativeNode == null) return Enumerable.Empty<DevelopmentProductGroup0215>();
        var selectedNodeId = selected.RepresentativeNode.NodeId;
        var selectedRequirements = new HashSet<string>(RequirementTargets0215(selected.RepresentativeNode.RequirementExpression), StringComparer.OrdinalIgnoreCase);
        return groups.Where(group => string.Equals(group.PresentationKey, selected.PresentationKey, StringComparison.Ordinal)
            || string.Equals(group.RepresentativeNode?.BranchId, selectedNodeId, StringComparison.OrdinalIgnoreCase)
            || RequirementTargets0215(group.RepresentativeNode?.RequirementExpression).Contains(selectedNodeId, StringComparer.OrdinalIgnoreCase)
            || selectedRequirements.Contains(group.RepresentativeNode?.NodeId ?? string.Empty));
    }

    private static bool IsCanonicalBaseClassGroup0215(DevelopmentProductGroup0215 group)
        => group.RepresentativeNode != null
           && group.RepresentativeNode.Tier == 1
           && group.RepresentativeNode.NodeId.StartsWith("class_", StringComparison.OrdinalIgnoreCase)
           && !group.RepresentativeNode.NodeId.Equals("class_assassin", StringComparison.OrdinalIgnoreCase)
           && !group.RepresentativeNode.NodeId.Equals("class_paladin", StringComparison.OrdinalIgnoreCase)
           && !group.RepresentativeNode.NodeId.Equals("class_wallborn", StringComparison.OrdinalIgnoreCase);

    private static bool IsCanonicalMagicOverviewGroup0215(DevelopmentProductGroup0215 group)
    {
        var nodeId = group.RepresentativeNode?.NodeId ?? string.Empty;
        return nodeId is "magic_method_mana" or "magic_method_spells" or "magic_method_seals" or "magic_method_arcana"
            or "magic_element_water" or "magic_element_earth" or "magic_element_fire" or "magic_element_air"
            or "magic_enchantment" or "magic_runes" or "magic_antimagic" or "magic_spiritual";
    }

    private static IReadOnlyList<string> ResolveDevelopmentProductRequiredPresentationKeys0215(
        DevelopmentProductGroup0215 group,
        IReadOnlyList<DevelopmentProductGroup0215> allGroups)
    {
        var requiredCanonicalIds = RequirementTargets0215(group.RepresentativeNode?.RequirementExpression).ToList();
        var branchId = group.RepresentativeNode?.BranchId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(branchId) && !string.Equals(branchId, group.RepresentativeNode?.NodeId, StringComparison.OrdinalIgnoreCase))
            requiredCanonicalIds.Add(branchId);
        return requiredCanonicalIds
            .Select(id => allGroups.FirstOrDefault(candidate => string.Equals(candidate.RepresentativeNode?.NodeId, id, StringComparison.OrdinalIgnoreCase))?.PresentationKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> RequirementTargets0215(RequirementExpression? expression)
    {
        if (expression == null) yield break;
        if (string.Equals(expression.Kind, RequirementExpressionKinds.Leaf, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(expression.TargetId))
            yield return expression.TargetId;
        foreach (var child in expression.Children ?? new List<RequirementExpression>())
            foreach (var target in RequirementTargets0215(child))
                yield return target;
    }

    private static bool IsDevelopmentCostResolved0215(ClassNodeDefinition? node)
    {
        if (node == null) return true;
        if (string.Equals(node.PurchasePolicy, DevelopmentPurchasePolicyIds.UnavailableUntilDefined, StringComparison.OrdinalIgnoreCase)) return false;
        if (RequirementTargets0215(node.RequirementExpression).Contains("cost_policy_finalized", StringComparer.OrdinalIgnoreCase)) return false;
        if (node.CostExperienceCoins <= 0 && (node.RequiresGMApproval || node.RequiresPlayerRequest)) return false;
        return (node.RequirementSummary ?? string.Empty).IndexOf("Стоимость развития", StringComparison.OrdinalIgnoreCase) < 0
            || (node.RequirementSummary ?? string.Empty).IndexOf("не утвержд", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static string FormatExperienceCoinCost0215(int cost)
        => $"{Math.Max(0, cost)} МО";

    private static string DevelopmentProductKnownDecisionSummary0215(ClassNodeDefinition node)
        => node.NodeId switch
        {
            "class_shooter" => "6 ранг — выбрать специализацию: Лучник, Арбалетчик или Стрелец.",
            "class_archer" or "class_crossbowman" or "class_firearms" => "Специализация продолжает путь Стрелка с 6 по 20 ранг без сброса прогресса.",
            "class_rogue" or "class_assassin" => "10 ранг: Плут открывает путь Убийцы.",
            "class_knight" or "class_priest" or "class_paladin" => "Рыцарь 10 + Жрец 10 открывают путь Паладина.",
            "class_defender" or "class_wallborn" => "Защитник 15 + одна стрелковая специализация 15 открывают путь Стенорождённого.",
            _ => string.Empty
        };

    private string ResolveCanonicalDevelopmentDefinitionId0215(ClassNodeDefinition? node)
    {
        if (node == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(node.LinkedDefinitionId)) return node.LinkedDefinitionId;
        return _mongo.ContentDefinitionRecords
            .Find(record => record.Category == "development_node_definition" && !record.IsArchived && record.ShortCode == node.NodeId)
            .Project(record => record.Id)
            .FirstOrDefault() ?? string.Empty;
    }

    private CharacterDevelopmentProfileDocument? LoadDevelopmentProfileDocument0215(string characterId)
        => _mongo.CharacterDevelopmentProfiles.Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();

    private static string NormalizeDevelopmentProductMode0215(string? mode)
    {
        var value = (mode ?? string.Empty).Trim().ToLowerInvariant();
        return value == "direction" || value == "path" || value == "mixed_path" || value == "my_route" || value == "available_now" ? value : "overview";
    }

    private static string DevelopmentProductPathKey0215(string hexagonId, ClassNodeDefinition node)
    {
        return DevelopmentProductProjectionPolicy0215.StablePathKey(hexagonId, node, CanonicalDevelopmentDirectionId(hexagonId, node));
    }

    private static string DevelopmentProductPresentationKind0215(ClassNodeDefinition node, int groupedCount)
    {
        var kind = DevelopmentProductProjectionPolicy0215.Classify(node, groupedCount);
        return kind == DevelopmentPresentationKinds0215.InternalProgression ? DevelopmentPresentationKinds0215.Path : kind;
    }

    private static string DevelopmentProductKindLabel0215(string kind) => kind switch
    {
        "Root" => "Начало", "Direction" => "Направление", "Path" => "Путь", "Specialization" => "Специализация",
        "Milestone" => "Рубеж", "Support" => "Поддержка", "MixedPath" => "Смешанный путь", "InternalProgression" => "Прогресс", _ => "Путь"
    };

    private static string DevelopmentProductReadableName0215(string value, string directionKey, string stableKey)
    {
        if (!DevelopmentProductContainsTechnicalText0215(value)) return FirstNonEmpty(value, "Путь развития");
        var alternatives = directionKey switch
        {
            DevelopmentDirectionIds.StrengthAssault => new[] { "Воин", "Берсерк" },
            DevelopmentDirectionIds.DexterityManeuver => new[] { "Следопыт", "Дуэлянт" },
            DevelopmentDirectionIds.EnduranceResilience => new[] { "Страж", "Защитник" },
            DevelopmentDirectionIds.IntellectReason => new[] { "Исследователь", "Инженер" },
            DevelopmentDirectionIds.WisdomPath => new[] { "Хранитель", "Проводник" },
            DevelopmentDirectionIds.CharismaInfluence => new[] { "Дипломат", "Лидер" },
            _ => new[] { "Путь развития", "Особый путь" }
        };
        var hash = (stableKey ?? string.Empty).Aggregate(17, (current, character) => unchecked(current * 31 + character));
        return alternatives[Math.Abs(hash % alternatives.Length)];
    }

    private static string DevelopmentProductReadableDescription0215(string value)
        => DevelopmentProductContainsTechnicalText0215(value)
            ? "Путь развития персонажа с последовательными ступенями и наградами."
            : FirstNonEmpty(value, "Путь развития персонажа.");

    private static bool DevelopmentProductContainsTechnicalText0215(string value)
    {
        var text = value ?? string.Empty;
        return text.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("dev ", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("тест", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("провер", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("Foundation", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("0.14", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("0.15", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int DevelopmentProductDirectionOrder0215(string hexagonId, string directionKey)
    {
        var directions = CanonicalDevelopmentDirectionIds(hexagonId);
        for (var index = 0; index < directions.Length; index++)
            if (string.Equals(directions[index], directionKey, StringComparison.OrdinalIgnoreCase)) return index;
        return directions.Length;
    }

    private static string DevelopmentProductRootLabel0215(string hexagonId)
        => DevelopmentProductProjectionPolicy0215.RootLabel(hexagonId);

    private static string DevelopmentProductDirectionLabel0215(string hexagonId, string key)
    {
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
            return key switch { "magic_methods" => "Методы магии", "magic_element_water" => "Вода", "magic_element_earth" => "Земля", "magic_element_fire" => "Огонь", "magic_element_air" => "Воздух", "magic_special" => "Особые направления", _ => "Магическое направление" };
        return key switch
        {
            DevelopmentDirectionIds.StrengthAssault => "Сила — Натиск",
            DevelopmentDirectionIds.DexterityManeuver => "Ловкость — Манёвр",
            DevelopmentDirectionIds.EnduranceResilience => "Выносливость — Стойкость",
            DevelopmentDirectionIds.IntellectReason => "Интеллект — Разум",
            DevelopmentDirectionIds.WisdomPath => "Мудрость — Путь",
            DevelopmentDirectionIds.CharismaInfluence => "Харизма — Влияние",
            _ => "Направление развития"
        };
    }

    private static string DevelopmentProductDirectionSummary0215(string hexagonId, string key)
    {
        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase)) return "Выберите осмысленный магический путь.";
        return key switch
        {
            DevelopmentDirectionIds.StrengthAssault => "Прямое давление, мощь и решительный натиск.",
            DevelopmentDirectionIds.DexterityManeuver => "Подвижность, точность и контроль позиции.",
            DevelopmentDirectionIds.EnduranceResilience => "Защита, выживание и удержание рубежа.",
            DevelopmentDirectionIds.IntellectReason => "Исследование, расчёт и практическое знание.",
            DevelopmentDirectionIds.WisdomPath => "Интуиция, внутренний путь и тайные силы.",
            DevelopmentDirectionIds.CharismaInfluence => "Лидерство, переговоры и влияние.",
            _ => "Направление развития персонажа."
        };
    }

    private static bool GetBool0215(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value is bool flag && flag;

    private sealed class DevelopmentProductGroup0215
    {
        public string PresentationKey { get; set; } = string.Empty;
        public string HexagonId { get; set; } = string.Empty;
        public string DirectionKey { get; set; } = string.Empty;
        public int DirectionOrder { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PresentationKind { get; set; } = "Path";
        public int CurrentTier { get; set; }
        public int MaxTier { get; set; }
        public bool IsAcquired { get; set; }
        public bool IsCompleted { get; set; }
        public bool CanAdvance { get; set; }
        public bool RequiresGMApproval { get; set; }
        public bool RequiresPlayerRequest { get; set; }
        public bool IsCostResolved { get; set; }
        public ClassNodeDefinition? NextNode { get; set; }
        public int NextCost { get; set; }
        public int VisibleRankMin { get; set; } = 1;
        public string RequirementSummary { get; set; } = string.Empty;
        public string RewardSummary { get; set; } = string.Empty;
        public string KnownDecisionSummary { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int GroupedNodeCount { get; set; }
        public int[] MilestoneTiers { get; set; } = Array.Empty<int>();
        public ClassNodeDefinition? RepresentativeNode { get; set; }
    }
}
