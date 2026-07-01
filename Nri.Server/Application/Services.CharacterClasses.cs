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
    public ResponseEnvelope CharacterClassAssign(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var useProfileNativeDevelopment = IsProfileNativeDevelopmentWriteEnabled();
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var payloadClassCode = PayloadReader.GetString(context.Request.Payload, "classCode");
        if (useProfileNativeDevelopment && string.IsNullOrWhiteSpace(payloadClassCode))
        {
            payloadClassCode = PayloadReader.GetString(context.Request.Payload, "classId");
        }

        var classCode = RequireLength(payloadClassCode, 1, 128, "classCode");
        var level = PayloadReader.GetInt(context.Request.Payload, "level") ?? 1;
        if (level < 1) throw new ArgumentException("level must be >= 1.");

        var character = GetCharacter(characterId);
        EnsureCharacterEditAllowed(actor, character.Id);
        EnsureCharacterDefaults(character);

        var classDef = _repositories.ClassDefinitions.GetByCode(classCode);
        if (classDef == null) throw new KeyNotFoundException("Class definition not found.");

        var explicitNodeId = PayloadReader.GetString(context.Request.Payload, "nodeId") ?? string.Empty;
        var requiredNodeId = ResolveHexagonNodeForClass(classDef, explicitNodeId);
        if (string.IsNullOrWhiteSpace(requiredNodeId))
        {
            _logger.Admin($"character.class.assign.denied actor={actor.Login} characterId={character.Id} classCode={classCode} reason=class-outside-hexagon");
            return Error("Class is locked outside Development Hexagon.", ResponseStatus.NotFound, ErrorCode.NotFound);
        }

        var levelCap = Math.Max(0, classDef.MaxLevel);
        if (levelCap > 0 && level > levelCap) throw new ArgumentException($"level exceeds levelCap ({levelCap}).");

        if (useProfileNativeDevelopment)
        {
            var nativePayload = new Dictionary<string, object>(context.Request.Payload)
            {
                ["classCode"] = classDef.Code,
                ["classId"] = classDef.Code,
                ["nodeId"] = requiredNodeId,
                ["requiredNodeId"] = requiredNodeId,
                ["hexagonId"] = string.IsNullOrWhiteSpace(classDef.RequiredHexagonId) ? "main_development_hexagon" : classDef.RequiredHexagonId,
                ["level"] = level
            };
            var native = _profileNativeWriteService.AssignClassProfileNativeAsync(character.Id, nativePayload, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
            if (native.ProfileWritten && native.LegacyFacadeSynced)
            {
                WriteAudit("character", actor.Id, "class.assign", $"{character.Id}:{classCode}:{requiredNodeId}:{level}");
                _logger.Admin($"character.class.assign actor={actor.Login} characterId={character.Id} classCode={classCode} nodeId={requiredNodeId} level={level} profileNative=true");
                return Ok("Class assigned through Development Hexagon.", new Dictionary<string, object>
                {
                    { "characterId", character.Id },
                    { "classCode", classCode },
                    { "nodeId", requiredNodeId },
                    { "level", level },
                    { "sourceOfTruth", "character_development_profiles" }
                });
            }

            if (!native.UsedFallback)
            {
                return Error("Character development profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
            }
        }

        return Error("Profile-native Development Hexagon write is required for class assignment.", ResponseStatus.Error, ErrorCode.InternalError);
    }

    public ResponseEnvelope CharacterClassesGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var character = GetCharacter(characterId);
        var owner = GetAccount(character.OwnerUserId);
        if (!CanViewCharacter(actor, owner, character))
        {
            throw new UnauthorizedAccessException("Character classes unavailable.");
        }

        var includeAdmin = IsAdmin(actor);
        var unlockedNodeIds = GetUnlockedDevelopmentNodeIds(character);
        var definitions = _repositories.ClassDefinitions.GetAll(includeArchived: false)
            .Where(x => x.IsActive && x.Status != DefinitionStatus.Archived)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();

        var items = definitions
            .Where(def => includeAdmin || IsClassVisibleThroughUnlockedHexagon(def, unlockedNodeIds))
            .Where(def => includeAdmin || !string.IsNullOrWhiteSpace(ResolveHexagonNodeForClass(def, string.Empty)))
            .Select(def => BuildHexagonClassPayload(def, unlockedNodeIds, includeAdmin))
            .Cast<object>()
            .ToArray();

        return Ok("Character classes loaded through Development Hexagon.", new Dictionary<string, object>
        {
            { "items", items },
            { "total", items.Length },
            { "sourceOfTruth", "character_development_profiles" },
            { "hexagonGated", true }
        });
    }

    private Dictionary<string, object> BuildHexagonClassPayload(ClassDefinition definition, HashSet<string> unlockedNodeIds, bool includeAdmin)
    {
        var nodeId = ResolveHexagonNodeForClass(definition, string.Empty);
        var unlocked = !string.IsNullOrWhiteSpace(nodeId) && unlockedNodeIds.Contains(nodeId);
        return new Dictionary<string, object>
        {
            { "classCode", definition.Code },
            { "classId", definition.Code },
            { "displayName", definition.Name },
            { "name", definition.Name },
            { "level", Math.Max(1, definition.UnlockLevel) },
            { "branchCode", definition.BranchCode },
            { "description", includeAdmin || unlocked ? definition.Description : string.Empty },
            { "requiredHexagonId", string.IsNullOrWhiteSpace(definition.RequiredHexagonId) ? "main_development_hexagon" : definition.RequiredHexagonId },
            { "requiredNodeId", includeAdmin || unlocked ? nodeId : string.Empty },
            { "isUnlockedByHexagon", unlocked },
            { "isLockedOutsideHexagon", definition.IsLockedOutsideHexagon },
            { "visibilityRule", string.IsNullOrWhiteSpace(definition.VisibilityRule) ? "hexagon-gated" : definition.VisibilityRule },
            { "sourceOfTruth", "character_development_profiles" }
        };
    }

    private HashSet<string> GetUnlockedDevelopmentNodeIds(Character character)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var profile = _mongo.CharacterDevelopmentProfiles
                .Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id))
                .FirstOrDefault()?.Profile;
            foreach (var node in profile?.Nodes ?? new List<CharacterDevelopmentNodeState>())
            {
                if (!string.Equals(node.NodeType, DevelopmentNodeTypes.Class, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(node.NodeType)) continue;
                if (!(node.IsUnlocked || node.IsPurchased || node.CurrentTier > 0)) continue;
                if (!string.IsNullOrWhiteSpace(node.DevelopmentNodeId)) ids.Add(node.DevelopmentNodeId.Trim());
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("character.class.hexagon.profile_read_skipped " + ex.Message);
        }

        return ids;
    }

    private string ResolveHexagonNodeForClass(ClassDefinition definition, string explicitNodeId)
    {
        EnsureDefinitionsLoaded(false);
        var nodeId = FirstNonEmpty(explicitNodeId, definition.RequiredNodeId);
        if (string.IsNullOrWhiteSpace(nodeId)) return string.Empty;
        return _nodesById.ContainsKey(nodeId) ? nodeId : string.Empty;
    }

    private bool IsClassVisibleThroughUnlockedHexagon(ClassDefinition definition, HashSet<string> unlockedNodeIds)
    {
        var nodeId = ResolveHexagonNodeForClass(definition, string.Empty);
        if (string.IsNullOrWhiteSpace(nodeId)) return false;
        return unlockedNodeIds.Contains(nodeId);
    }
}
