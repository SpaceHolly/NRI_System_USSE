using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope MapSpaceNodeList(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapSpaceHierarchyEnabled())
            return MapSpaceHierarchyDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var parentId = RequireLength(PayloadReader.GetString(payload, "parentId"), 0, 128, "parentId");
        var nodeType = RequireLength(PayloadReader.GetString(payload, "nodeType"), 0, 64, "nodeType");
        var limit = Math.Max(1, Math.Min(PayloadReader.GetInt(payload, "limit") ?? 200, 500));
        var nodes = (string.IsNullOrWhiteSpace(parentId)
                ? _repositories.MapSpaceNodes.ListByCampaignAsync(campaignId, limit).GetAwaiter().GetResult()
                : _repositories.MapSpaceNodes.ListByParentAsync(campaignId, parentId, limit).GetAwaiter().GetResult())
            .Where(x => string.IsNullOrWhiteSpace(nodeType)
                        || string.Equals(x.NodeType, nodeType, StringComparison.OrdinalIgnoreCase))
            .Select(x => (object)MapSpaceNodePayload(x))
            .ToArray();

        return Ok("Space nodes loaded.", new Dictionary<string, object>
        {
            ["items"] = nodes,
            ["count"] = nodes.Length
        });
    }

    public ResponseEnvelope MapSpaceNodeCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSpaceHierarchyEnabled())
            return MapSpaceHierarchyDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var ruleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId"), 1, 128, "ruleSetId");
        var parentId = RequireLength(PayloadReader.GetString(payload, "parentId"), 0, 128, "parentId");
        var nodeType = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "nodeType"), MapSpaceNodeTypeIds.Location), 1, 64, "nodeType");
        var name = RequireLength(PayloadReader.GetString(payload, "name"), 2, 160, "name");
        var description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 2048, "description");
        var visibility = FirstNonEmpty(PayloadReader.GetString(payload, "visibility"), MapVisibilityModes.Party);
        var allowedTypes = new[]
        {
            MapSpaceNodeTypeIds.Dimension, MapSpaceNodeTypeIds.World, MapSpaceNodeTypeIds.StarSystem,
            MapSpaceNodeTypeIds.Star, MapSpaceNodeTypeIds.Planet, MapSpaceNodeTypeIds.Region,
            MapSpaceNodeTypeIds.Country, MapSpaceNodeTypeIds.City, MapSpaceNodeTypeIds.Location,
            MapSpaceNodeTypeIds.Room, MapSpaceNodeTypeIds.Interior, MapSpaceNodeTypeIds.Custom
        };
        if (!allowedTypes.Contains(nodeType, StringComparer.OrdinalIgnoreCase))
            return Error("Unknown space node type.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (!new[] { MapVisibilityModes.Public, MapVisibilityModes.Party, MapVisibilityModes.GmOnly, MapVisibilityModes.Hidden }
                .Contains(visibility, StringComparer.OrdinalIgnoreCase))
            return Error("Unknown space node visibility.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        if (!string.IsNullOrWhiteSpace(parentId))
        {
            var parent = _repositories.MapSpaceNodes.GetByIdAsync(parentId).GetAwaiter().GetResult();
            if (parent == null || parent.IsArchived || parent.Deleted
                || !string.Equals(parent.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase))
                return Error("Parent space node not found in this campaign.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        }

        var existing = _repositories.MapSpaceNodes.ListByCampaignAsync(campaignId, 2000).GetAwaiter().GetResult()
            .FirstOrDefault(x => string.Equals(x.ParentId, parentId, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals(x.NodeType, nodeType, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (existing != null)
        {
            return Ok("Space node already exists.", new Dictionary<string, object>
            {
                ["item"] = MapSpaceNodePayload(existing),
                ["alreadyExists"] = true
            });
        }

        var now = DateTime.UtcNow;
        var node = new MapSpaceNodeState
        {
            CampaignId = campaignId,
            RuleSetId = ruleSetId,
            ParentId = parentId,
            NodeType = nodeType,
            Name = name,
            Description = description,
            Visibility = visibility,
            IsArchived = false,
            Archived = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            Tags = new List<string> { "construction_location", "foundation_0_19_7" }
        };
        var saved = _repositories.MapSpaceNodes.UpsertAsync(node).GetAwaiter().GetResult();
        _logger.Admin($"map.spaceNode.create actor={actor.Login} campaignId={campaignId} nodeId={saved.Id} type={saved.NodeType}");
        return Ok("Space node created.", new Dictionary<string, object>
        {
            ["item"] = MapSpaceNodePayload(saved),
            ["alreadyExists"] = false
        });
    }

    private bool MapSpaceHierarchyEnabled()
        => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1));

    private ResponseEnvelope MapSpaceHierarchyDisabled(string command)
    {
        _logger.Admin($"map.spaceNode.disabled command={command}");
        return Error("Space hierarchy endpoints disabled.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private static Dictionary<string, object> MapSpaceNodePayload(MapSpaceNodeState node)
        => new()
        {
            ["spaceNodeId"] = node.Id,
            ["campaignId"] = node.CampaignId,
            ["ruleSetId"] = node.RuleSetId,
            ["parentId"] = node.ParentId,
            ["nodeType"] = node.NodeType,
            ["name"] = node.Name,
            ["description"] = node.Description,
            ["visibility"] = node.Visibility,
            ["isArchived"] = node.IsArchived || node.Archived,
            ["updatedAtUtc"] = node.UpdatedAtUtc
        };
}
