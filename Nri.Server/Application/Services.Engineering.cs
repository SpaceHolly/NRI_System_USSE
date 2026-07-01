using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope EngineeringPlatformList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringPlatformDefinitions))) return EngineeringDisabled(context.Request.Command);
        }
        else if (!EngineeringPlayerEnabled())
        {
            return EngineeringDisabled(context.Request.Command);
        }

        var filter = EngineeringCampaignFilter<EngineeringPlatformDefinition>(context.Request.Payload);
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<EngineeringPlatformDefinition>.Filter.Eq(x => x.IsArchived, false);
        if (!IsAdmin(actor)) filter &= PlayerVisibleFilter<EngineeringPlatformDefinition>();
        var items = _repositories.EngineeringPlatforms.Find(filter)
            .OrderBy(x => x.Name)
            .Take(300)
            .Select(x => (object)EngineeringPlatformPayload(x, IsAdmin(actor)))
            .ToArray();
        return Ok("Engineering platforms loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope EngineeringPlayerPlatformList(CommandContext context) => EngineeringPlatformList(context);

    public ResponseEnvelope EngineeringPlatformGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var item = RequireEngineeringPlatform(context);
        if (!IsAdmin(actor) && !CanPlayerSeePlatform(item)) throw new UnauthorizedAccessException("Engineering platform is not visible.");
        return Ok("Engineering platform loaded.", new Dictionary<string, object> { { "item", EngineeringPlatformPayload(item, IsAdmin(actor)) } });
    }

    public ResponseEnvelope EngineeringPlatformCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringPlatformDefinitions))) return EngineeringDisabled(context.Request.Command);
        var item = BuildEngineeringPlatform(context.Request.Payload, actor, null);
        _repositories.EngineeringPlatforms.Insert(item);
        _logger.Admin($"engineering.platform.create.done actor={actor.Login} platformId={item.Id}");
        return Ok("Engineering platform created.", new Dictionary<string, object> { { "item", EngineeringPlatformPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringPlatformUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringPlatformDefinitions))) return EngineeringDisabled(context.Request.Command);
        var item = RequireEngineeringPlatform(context);
        BuildEngineeringPlatform(context.Request.Payload, actor, item);
        _repositories.EngineeringPlatforms.Replace(item);
        return Ok("Engineering platform updated.", new Dictionary<string, object> { { "item", EngineeringPlatformPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringPlatformArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled()) return EngineeringDisabled(context.Request.Command);
        var item = RequireEngineeringPlatform(context);
        item.IsArchived = true;
        item.Archived = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.EngineeringPlatforms.Replace(item);
        return Ok("Engineering platform archived.", new Dictionary<string, object> { { "item", EngineeringPlatformPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringSizeClassList(CommandContext context)
    {
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringSizeClasses))) return EngineeringDisabled(context.Request.Command);
        var filter = EngineeringCampaignFilter<EngineeringPlatformSizeClassDefinition>(context.Request.Payload);
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<EngineeringPlatformSizeClassDefinition>.Filter.Eq(x => x.IsArchived, false);
        var items = _repositories.EngineeringSizeClasses.Find(filter).OrderBy(x => x.Name).Take(300).Select(x => (object)SizeClassPayload(x, true)).ToArray();
        return Ok("Engineering size classes loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope EngineeringSizeClassCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringSizeClasses))) return EngineeringDisabled(context.Request.Command);
        var item = BuildSizeClass(context.Request.Payload, actor, null);
        _repositories.EngineeringSizeClasses.Insert(item);
        return Ok("Engineering size class created.", new Dictionary<string, object> { { "item", SizeClassPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringSizeClassUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringSizeClasses))) return EngineeringDisabled(context.Request.Command);
        var item = RequireSizeClass(context);
        BuildSizeClass(context.Request.Payload, actor, item);
        _repositories.EngineeringSizeClasses.Replace(item);
        return Ok("Engineering size class updated.", new Dictionary<string, object> { { "item", SizeClassPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringSizeClassArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled()) return EngineeringDisabled(context.Request.Command);
        var item = RequireSizeClass(context);
        item.IsArchived = true;
        item.Archived = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.EngineeringSizeClasses.Replace(item);
        return Ok("Engineering size class archived.", new Dictionary<string, object> { { "item", SizeClassPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringModuleList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringModules))) return EngineeringDisabled(context.Request.Command);
        }
        else if (!EngineeringPlayerEnabled())
        {
            return EngineeringDisabled(context.Request.Command);
        }

        var filter = EngineeringCampaignFilter<EngineeringModuleDefinition>(context.Request.Payload);
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<EngineeringModuleDefinition>.Filter.Eq(x => x.IsArchived, false);
        if (!IsAdmin(actor)) filter &= PlayerVisibleFilter<EngineeringModuleDefinition>();
        var items = _repositories.EngineeringModules.Find(filter)
            .OrderBy(x => x.ModuleCategory)
            .ThenBy(x => x.Name)
            .Take(500)
            .Select(x => (object)EngineeringModulePayload(x, IsAdmin(actor)))
            .ToArray();
        return Ok("Engineering modules loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope EngineeringPlayerModuleList(CommandContext context) => EngineeringModuleList(context);

    public ResponseEnvelope EngineeringModuleGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var item = RequireEngineeringModule(context);
        if (!IsAdmin(actor) && !CanPlayerSeeModule(item)) throw new UnauthorizedAccessException("Engineering module is not visible.");
        return Ok("Engineering module loaded.", new Dictionary<string, object> { { "item", EngineeringModulePayload(item, IsAdmin(actor)) } });
    }

    public ResponseEnvelope EngineeringModuleCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringModules))) return EngineeringDisabled(context.Request.Command);
        var item = BuildEngineeringModule(context.Request.Payload, actor, null);
        _repositories.EngineeringModules.Insert(item);
        EnsureWeaponProfileForModule(item, actor.Id);
        return Ok("Engineering module created.", new Dictionary<string, object> { { "item", EngineeringModulePayload(item, true) } });
    }

    public ResponseEnvelope EngineeringModuleUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringModules))) return EngineeringDisabled(context.Request.Command);
        var item = RequireEngineeringModule(context);
        BuildEngineeringModule(context.Request.Payload, actor, item);
        _repositories.EngineeringModules.Replace(item);
        EnsureWeaponProfileForModule(item, actor.Id);
        return Ok("Engineering module updated.", new Dictionary<string, object> { { "item", EngineeringModulePayload(item, true) } });
    }

    public ResponseEnvelope EngineeringModuleArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled()) return EngineeringDisabled(context.Request.Command);
        var item = RequireEngineeringModule(context);
        item.IsArchived = true;
        item.Archived = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.EngineeringModules.Replace(item);
        return Ok("Engineering module archived.", new Dictionary<string, object> { { "item", EngineeringModulePayload(item, true) } });
    }

    public ResponseEnvelope EngineeringPresetList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringPresetDesigns))) return EngineeringDisabled(context.Request.Command);
        }
        else if (!EngineeringPlayerEnabled())
        {
            return EngineeringDisabled(context.Request.Command);
        }

        var filter = EngineeringCampaignFilter<PresetVehicleDesignDefinition>(context.Request.Payload);
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<PresetVehicleDesignDefinition>.Filter.Eq(x => x.IsArchived, false);
        if (!IsAdmin(actor)) filter &= PlayerVisibleFilter<PresetVehicleDesignDefinition>();
        var items = _repositories.EngineeringPresets.Find(filter).OrderBy(x => x.Name).Take(300).Select(x => (object)PresetPayload(x, IsAdmin(actor))).ToArray();
        return Ok("Engineering presets loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope EngineeringPlayerPresetList(CommandContext context) => EngineeringPresetList(context);

    public ResponseEnvelope EngineeringPresetCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringPresetDesigns))) return EngineeringDisabled(context.Request.Command);
        var item = BuildPreset(context.Request.Payload, actor, null);
        _repositories.EngineeringPresets.Insert(item);
        return Ok("Engineering preset created.", new Dictionary<string, object> { { "item", PresetPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringPresetUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringPresetDesigns))) return EngineeringDisabled(context.Request.Command);
        var item = RequirePreset(context);
        BuildPreset(context.Request.Payload, actor, item);
        _repositories.EngineeringPresets.Replace(item);
        return Ok("Engineering preset updated.", new Dictionary<string, object> { { "item", PresetPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringPresetArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled()) return EngineeringDisabled(context.Request.Command);
        var item = RequirePreset(context);
        item.IsArchived = true;
        item.Archived = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.EngineeringPresets.Replace(item);
        return Ok("Engineering preset archived.", new Dictionary<string, object> { { "item", PresetPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringDesignValidate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringDesignValidation))) return EngineeringDisabled(context.Request.Command);
        }
        else if (!EngineeringPlayerEnabled())
        {
            return EngineeringDisabled(context.Request.Command);
        }

        var validation = BuildValidation(context.Request.Payload, actor);
        _repositories.EngineeringValidationResults.Insert(validation.Result);
        _repositories.EngineeringCostEstimates.Insert(validation.Cost);
        return Ok("Engineering design validated.", new Dictionary<string, object>
        {
            { "validation", ValidationPayload(validation.Result, IsAdmin(actor)) },
            { "costEstimate", CostPayload(validation.Cost, IsAdmin(actor)) }
        });
    }

    public ResponseEnvelope EngineeringProjectList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!EngineeringAdminEnabled()) return EngineeringDisabled(context.Request.Command);
        }
        else if (!EngineeringPlayerEnabled())
        {
            return EngineeringDisabled(context.Request.Command);
        }

        var filter = EngineeringCampaignFilter<EngineeringDesignProjectState>(context.Request.Payload);
        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId") ?? string.Empty;
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<EngineeringDesignProjectState>.Filter.Ne(x => x.Status, EngineeringDesignStatusIds.Archived);
        if (!string.IsNullOrWhiteSpace(characterId)) filter &= Builders<EngineeringDesignProjectState>.Filter.Eq(x => x.ActorEntityId, characterId);
        if (!IsAdmin(actor))
        {
            filter &= PlayerVisibleFilter<EngineeringDesignProjectState>();
            filter &= Builders<EngineeringDesignProjectState>.Filter.Or(
                Builders<EngineeringDesignProjectState>.Filter.Eq(x => x.OwnerUserId, actor.Id),
                Builders<EngineeringDesignProjectState>.Filter.Eq(x => x.ActorEntityId, characterId));
        }

        var items = _repositories.EngineeringProjects.Find(filter).OrderByDescending(x => x.UpdatedAtUtc).Take(300).Select(x => (object)EngineeringProjectPayload(x, IsAdmin(actor))).ToArray();
        return Ok("Engineering projects loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope EngineeringPlayerProjectList(CommandContext context) => EngineeringProjectList(context);

    public ResponseEnvelope EngineeringProjectGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var item = RequireEngineeringProject(context);
        if (!IsAdmin(actor) && !CanPlayerSeeEngineeringProject(item, actor)) throw new UnauthorizedAccessException("Engineering project is not visible.");
        return Ok("Engineering project loaded.", new Dictionary<string, object> { { "item", EngineeringProjectPayload(item, IsAdmin(actor)) } });
    }

    public ResponseEnvelope EngineeringPlayerProjectGet(CommandContext context) => EngineeringProjectGet(context);

    public ResponseEnvelope EngineeringProjectCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled()) return EngineeringDisabled(context.Request.Command);
        var item = BuildEngineeringProject(context.Request.Payload, actor, false);
        _repositories.EngineeringProjects.Insert(item);
        EnsureProjectFoundationForEngineering(item, actor);
        TryPublishEngineeringSync(item, "created", actor.Id, context.Request.RequestId ?? string.Empty);
        TryWriteEngineeringJournal(item, "created", "Создан инженерный проект", actor.Id);
        return Ok("Engineering project created.", new Dictionary<string, object> { { "item", EngineeringProjectPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringProjectStart(CommandContext context) => SetEngineeringProjectStatus(context, EngineeringDesignStatusIds.Active, "started");
    public ResponseEnvelope EngineeringProjectCancel(CommandContext context) => SetEngineeringProjectStatus(context, EngineeringDesignStatusIds.Cancelled, "cancelled");
    public ResponseEnvelope EngineeringProjectFail(CommandContext context) => SetEngineeringProjectStatus(context, EngineeringDesignStatusIds.Failed, "failed");
    public ResponseEnvelope EngineeringProjectComplete(CommandContext context) => SetEngineeringProjectStatus(context, EngineeringDesignStatusIds.Completed, "completed");

    public ResponseEnvelope EngineeringProjectProgressAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled()) return EngineeringDisabled(context.Request.Command);
        var item = RequireEngineeringProject(context);
        var delta = Math.Max(1, Math.Min(100, PayloadReader.GetInt(context.Request.Payload, "progressDelta") ?? PayloadReader.GetInt(context.Request.Payload, "deltaPercent") ?? 10));
        item.ProgressPercent = Math.Max(0, Math.Min(100, item.ProgressPercent + delta));
        item.WorkPointsDone += Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "workPointsDelta") ?? delta);
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.EngineeringProjects.Replace(item);
        UpdateEngineeringProjectBase(item, actor.Id);
        return Ok("Engineering project progress updated.", new Dictionary<string, object> { { "item", EngineeringProjectPayload(item, true) } });
    }

    public ResponseEnvelope EngineeringBlueprintPrepare(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringBlueprintResult))) return EngineeringDisabled(context.Request.Command);
        var project = RequireEngineeringProject(context);
        var blueprint = EnsureBlueprint(project, actor.Id, EngineeringBlueprintStatusIds.Prepared);
        project.BlueprintStatus = EngineeringBlueprintStatusIds.Prepared;
        project.Status = EngineeringDesignStatusIds.AwaitingAcceptance;
        project.UpdatedAtUtc = DateTime.UtcNow;
        project.UpdatedByUserId = actor.Id;
        _repositories.EngineeringProjects.Replace(project);
        return Ok("Engineering blueprint prepared.", new Dictionary<string, object> { { "item", BlueprintPayload(blueprint, true) } });
    }

    public ResponseEnvelope EngineeringBlueprintAccept(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringBlueprintResult))) return EngineeringDisabled(context.Request.Command);
        var project = RequireEngineeringProject(context);
        var blueprint = EnsureBlueprint(project, actor.Id, EngineeringBlueprintStatusIds.Created);
        blueprint.AcceptedAtUtc = DateTime.UtcNow;
        blueprint.AcceptedByUserId = actor.Id;
        _repositories.EngineeringBlueprints.Replace(blueprint);
        var reference = _repositories.EngineeringBlueprintReferences.Find(Builders<EngineeringBlueprintReference>.Filter.Eq(x => x.BlueprintId, blueprint.Id)).FirstOrDefault()
            ?? new EngineeringBlueprintReference { BlueprintId = blueprint.Id, CampaignId = blueprint.CampaignId, ProjectId = project.Id, CreatedByUserId = actor.Id };
        reference.DisplayName = blueprint.Name;
        reference.IsPlayerVisible = blueprint.IsPlayerVisible;
        if (string.IsNullOrWhiteSpace(reference.Id) || _repositories.EngineeringBlueprintReferences.GetById(reference.Id) == null) _repositories.EngineeringBlueprintReferences.Insert(reference); else _repositories.EngineeringBlueprintReferences.Replace(reference);
        project.BlueprintStatus = EngineeringBlueprintStatusIds.Created;
        project.Status = EngineeringDesignStatusIds.Completed;
        project.CompletedAtUtc = DateTime.UtcNow;
        project.UpdatedAtUtc = DateTime.UtcNow;
        project.UpdatedByUserId = actor.Id;
        _repositories.EngineeringProjects.Replace(project);
        TryPublishEngineeringSync(project, "blueprint_created", actor.Id, context.Request.RequestId ?? string.Empty);
        TryWriteEngineeringJournal(project, "blueprint_created", "Инженерный чертёж принят", actor.Id);
        return Ok("Engineering blueprint accepted. No vehicle instance was created.", new Dictionary<string, object> { { "item", BlueprintPayload(blueprint, true) }, { "reference", BlueprintReferencePayload(reference) } });
    }

    public ResponseEnvelope EngineeringBlueprintArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled()) return EngineeringDisabled(context.Request.Command);
        var blueprint = RequireBlueprint(context);
        blueprint.Status = EngineeringBlueprintStatusIds.Archived;
        blueprint.Archived = true;
        blueprint.AcceptedByUserId = actor.Id;
        _repositories.EngineeringBlueprints.Replace(blueprint);
        return Ok("Engineering blueprint archived.", new Dictionary<string, object> { { "item", BlueprintPayload(blueprint, true) } });
    }

    public ResponseEnvelope EngineeringPlayerDraftList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!EngineeringPlayerEnabled()) return EngineeringDisabled(context.Request.Command);
        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId") ?? string.Empty;
        var filter = Builders<VehicleDesignDraft>.Filter.Eq(x => x.OwnerUserId, actor.Id);
        if (!string.IsNullOrWhiteSpace(characterId)) filter &= Builders<VehicleDesignDraft>.Filter.Eq(x => x.OwnerCharacterId, characterId);
        var items = _repositories.EngineeringDesignDrafts.Find(filter).OrderByDescending(x => x.UpdatedAtUtc).Take(200).Select(x => (object)DraftPayload(x, false)).ToArray();
        return Ok("Engineering drafts loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope EngineeringPlayerDraftGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!EngineeringPlayerEnabled()) return EngineeringDisabled(context.Request.Command);
        var item = RequireDraft(context);
        if (!string.Equals(item.OwnerUserId, actor.Id, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Draft is not yours.");
        return Ok("Engineering draft loaded.", new Dictionary<string, object> { { "item", DraftPayload(item, false) } });
    }

    public ResponseEnvelope EngineeringPlayerDraftCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!EngineeringPlayerEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringCustomDesigns))) return EngineeringDisabled(context.Request.Command);
        var item = BuildDraft(context.Request.Payload, actor, null);
        _repositories.EngineeringDesignDrafts.Insert(item);
        return Ok("Engineering draft created.", new Dictionary<string, object> { { "item", DraftPayload(item, false) } });
    }

    public ResponseEnvelope EngineeringPlayerDraftUpdate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!EngineeringPlayerEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringCustomDesigns))) return EngineeringDisabled(context.Request.Command);
        var item = RequireDraft(context);
        if (!string.Equals(item.OwnerUserId, actor.Id, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Draft is not yours.");
        BuildDraft(context.Request.Payload, actor, item);
        _repositories.EngineeringDesignDrafts.Replace(item);
        return Ok("Engineering draft updated.", new Dictionary<string, object> { { "item", DraftPayload(item, false) } });
    }

    public ResponseEnvelope EngineeringPlayerDraftValidate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!EngineeringPlayerEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringDesignValidation))) return EngineeringDisabled(context.Request.Command);
        var draft = RequireDraft(context);
        if (!string.Equals(draft.OwnerUserId, actor.Id, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Draft is not yours.");
        var payload = new Dictionary<string, object>(context.Request.Payload)
        {
            ["platformId"] = draft.PlatformId,
            ["moduleIds"] = draft.ModuleIds.ToArray(),
            ["draftId"] = draft.Id,
            ["campaignId"] = draft.CampaignId
        };
        var validation = BuildValidation(payload, actor);
        _repositories.EngineeringValidationResults.Insert(validation.Result);
        _repositories.EngineeringCostEstimates.Insert(validation.Cost);
        draft.ValidationSummary = ValidationSummary(validation.Result);
        draft.CostSummary = $"Стоимость: {validation.Cost.TotalCost:0.##}";
        draft.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.EngineeringDesignDrafts.Replace(draft);
        return Ok("Engineering draft validated.", new Dictionary<string, object> { { "validation", ValidationPayload(validation.Result, false) }, { "costEstimate", CostPayload(validation.Cost, false) } });
    }

    public ResponseEnvelope EngineeringPlayerDraftSubmit(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!EngineeringPlayerEnabled() || !_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringRequestIntegration))) return EngineeringDisabled(context.Request.Command);
        var draft = RequireDraft(context);
        if (!string.Equals(draft.OwnerUserId, actor.Id, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Draft is not yours.");
        draft.Status = EngineeringDesignStatusIds.Submitted;
        draft.UpdatedAtUtc = DateTime.UtcNow;
        draft.UpdatedByUserId = actor.Id;
        _repositories.EngineeringDesignDrafts.Replace(draft);
        var request = new PlayerRequestState
        {
            RequestNumber = NextPlayerRequestNumber(),
            CampaignId = draft.CampaignId,
            CharacterId = draft.OwnerCharacterId,
            CreatedByUserId = actor.Id,
            CreatedByDisplayName = actor.Login,
            RequestType = PlayerRequestTypeIds.EngineeringDesign,
            Title = "Заявка на инженерный проект",
            Description = string.IsNullOrWhiteSpace(draft.PlayerNotes) ? draft.Name : draft.PlayerNotes,
            Status = PlayerRequestStatusIds.Submitted,
            LinkedEntityType = "engineering_draft",
            LinkedEntityId = draft.Id,
            ProposalType = ProjectTypeIds.EngineeringDesign,
            ProposalPayloadSummary = draft.Name,
            SubmittedAtUtc = DateTime.UtcNow,
            ProposalPayload = new PlayerRequestProposalDraft
            {
                ProposalType = ProjectTypeIds.EngineeringDesign,
                DisplaySummary = draft.Name,
                EstimatedResult = "Инженерный чертёж после проверки GM",
                Parameters = new Dictionary<string, object>
                {
                    { "draftId", draft.Id },
                    { "platformId", draft.PlatformId },
                    { "moduleIds", draft.ModuleIds.ToArray() },
                    { "intendedRole", draft.IntendedRole }
                },
                RequiresGMApproval = true
            }
        };
        _repositories.PlayerRequests.Insert(request);
        return Ok("Engineering draft submitted.", new Dictionary<string, object> { { "item", DraftPayload(draft, false) }, { "requestId", request.Id }, { "requestNumber", request.RequestNumber } });
    }

    public ResponseEnvelope EngineeringPlayerBlueprintList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!EngineeringPlayerEnabled()) return EngineeringDisabled(context.Request.Command);
        var filter = PlayerVisibleFilter<VehicleDesignBlueprint>();
        var items = _repositories.EngineeringBlueprints.Find(filter).OrderByDescending(x => x.PreparedAtUtc).Take(200).Select(x => (object)BlueprintPayload(x, false)).ToArray();
        return Ok("Engineering blueprints loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope EngineeringPlayerBlueprintGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!EngineeringPlayerEnabled()) return EngineeringDisabled(context.Request.Command);
        var item = RequireBlueprint(context);
        if (!CanPlayerSeeBlueprint(item)) throw new UnauthorizedAccessException("Blueprint is not visible.");
        return Ok("Engineering blueprint loaded.", new Dictionary<string, object> { { "item", BlueprintPayload(item, false) } });
    }

    private EngineeringPlatformDefinition BuildEngineeringPlatform(IDictionary<string, object> payload, UserAccount actor, EngineeringPlatformDefinition? existing)
    {
        var item = existing ?? new EngineeringPlatformDefinition();
        item.CampaignId = RequireLength(PayloadReader.GetString(payload, "campaignId") ?? item.CampaignId, 0, 128, "campaignId");
        item.RuleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId") ?? item.RuleSetId, 0, 128, "ruleSetId");
        item.PlatformId = RequireLength(PayloadReader.GetString(payload, "platformId") ?? item.PlatformId, 0, 128, "platformId");
        item.Name = RequireLength(PayloadReader.GetString(payload, "name") ?? item.Name, 1, 160, "name");
        item.ShortName = RequireLength(PayloadReader.GetString(payload, "shortName") ?? item.ShortName, 0, 80, "shortName");
        item.Description = RequireLength(PayloadReader.GetString(payload, "description") ?? item.Description, 0, 2000, "description");
        item.PublicDescription = RequireLength(PayloadReader.GetString(payload, "publicDescription") ?? item.PublicDescription, 0, 2000, "publicDescription");
        item.GMDescription = RequireLength(PayloadReader.GetString(payload, "gmDescription") ?? item.GMDescription, 0, 4000, "gmDescription");
        item.PlatformKind = EngineeringAllow(PayloadReader.GetString(payload, "platformKind") ?? item.PlatformKind, EngineeringPlatformKindIds.Custom, EngineeringPlatformKindIds.GroundVehicle, EngineeringPlatformKindIds.Aircraft, EngineeringPlatformKindIds.Spacecraft, EngineeringPlatformKindIds.Watercraft, EngineeringPlatformKindIds.Walker, EngineeringPlatformKindIds.Drone, EngineeringPlatformKindIds.Building, EngineeringPlatformKindIds.Custom);
        item.SizeClassId = EngineeringAllow(PayloadReader.GetString(payload, "sizeClassId") ?? item.SizeClassId, EngineeringSizeClassIds.Medium, EngineeringSizeClassIds.Tiny, EngineeringSizeClassIds.Small, EngineeringSizeClassIds.Medium, EngineeringSizeClassIds.Large, EngineeringSizeClassIds.Huge, EngineeringSizeClassIds.Capital, EngineeringSizeClassIds.Custom);
        item.BaseMassKg = NonNegativeDecimal(payload, "baseMassKg", item.BaseMassKg);
        item.BaseVolumeM3 = NonNegativeDecimal(payload, "baseVolumeM3", item.BaseVolumeM3);
        item.BaseSlots = NonNegativeInt(payload, "baseSlots", item.BaseSlots);
        item.BaseHardpoints = NonNegativeInt(payload, "baseHardpoints", item.BaseHardpoints);
        item.BasePowerOutput = NonNegativeDecimal(payload, "basePowerOutput", item.BasePowerOutput);
        item.BasePowerLoad = NonNegativeDecimal(payload, "basePowerLoad", item.BasePowerLoad);
        item.BaseCrewMin = NonNegativeInt(payload, "baseCrewMin", item.BaseCrewMin);
        item.BaseCrewMax = NonNegativeInt(payload, "baseCrewMax", item.BaseCrewMax);
        item.BaseCost = NonNegativeDecimal(payload, "baseCost", item.BaseCost);
        item.DifficultyTier = NonNegativeInt(payload, "difficultyTier", item.DifficultyTier);
        item.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        item.VisibilityMode = NormalizeEngineeringVisibility(PayloadReader.GetString(payload, "visibilityMode") ?? item.VisibilityMode);
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        if (existing == null) item.CreatedByUserId = actor.Id;
        return item;
    }

    private EngineeringModuleDefinition BuildEngineeringModule(IDictionary<string, object> payload, UserAccount actor, EngineeringModuleDefinition? existing)
    {
        var item = existing ?? new EngineeringModuleDefinition();
        item.CampaignId = RequireLength(PayloadReader.GetString(payload, "campaignId") ?? item.CampaignId, 0, 128, "campaignId");
        item.RuleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId") ?? item.RuleSetId, 0, 128, "ruleSetId");
        item.ModuleId = RequireLength(PayloadReader.GetString(payload, "moduleId") ?? item.ModuleId, 0, 128, "moduleId");
        item.Name = RequireLength(PayloadReader.GetString(payload, "name") ?? item.Name, 1, 160, "name");
        item.Description = RequireLength(PayloadReader.GetString(payload, "description") ?? item.Description, 0, 2000, "description");
        item.PublicDescription = RequireLength(PayloadReader.GetString(payload, "publicDescription") ?? item.PublicDescription, 0, 2000, "publicDescription");
        item.GMDescription = RequireLength(PayloadReader.GetString(payload, "gmDescription") ?? item.GMDescription, 0, 4000, "gmDescription");
        item.ModuleCategory = EngineeringAllow(PayloadReader.GetString(payload, "moduleCategory") ?? item.ModuleCategory, EngineeringModuleCategoryIds.Custom, EngineeringModuleCategoryIds.Frame, EngineeringModuleCategoryIds.Engine, EngineeringModuleCategoryIds.PowerCore, EngineeringModuleCategoryIds.Armor, EngineeringModuleCategoryIds.Cargo, EngineeringModuleCategoryIds.Crew, EngineeringModuleCategoryIds.Sensor, EngineeringModuleCategoryIds.Weapon, EngineeringModuleCategoryIds.Medical, EngineeringModuleCategoryIds.Utility, EngineeringModuleCategoryIds.Mobility, EngineeringModuleCategoryIds.Shield, EngineeringModuleCategoryIds.Custom);
        item.SlotType = EngineeringAllow(PayloadReader.GetString(payload, "slotType") ?? item.SlotType, EngineeringModuleSlotTypeIds.Internal, EngineeringModuleSlotTypeIds.Internal, EngineeringModuleSlotTypeIds.External, EngineeringModuleSlotTypeIds.Hardpoint, EngineeringModuleSlotTypeIds.Crew, EngineeringModuleSlotTypeIds.Cargo, EngineeringModuleSlotTypeIds.Power, EngineeringModuleSlotTypeIds.Custom);
        item.SlotCost = NonNegativeInt(payload, "slotCost", item.SlotCost);
        item.HardpointCost = NonNegativeInt(payload, "hardpointCost", item.HardpointCost);
        item.MassKg = NonNegativeDecimal(payload, "massKg", item.MassKg);
        item.VolumeM3 = NonNegativeDecimal(payload, "volumeM3", item.VolumeM3);
        item.PowerOutput = NonNegativeDecimal(payload, "powerOutput", item.PowerOutput);
        item.PowerLoad = NonNegativeDecimal(payload, "powerLoad", item.PowerLoad);
        item.CrewRequired = NonNegativeInt(payload, "crewRequired", item.CrewRequired);
        item.Cost = NonNegativeDecimal(payload, "cost", item.Cost);
        item.DifficultyTier = NonNegativeInt(payload, "difficultyTier", item.DifficultyTier);
        item.DiceExpression = NormalizeDiceExpression(PayloadReader.GetString(payload, "diceExpression") ?? item.DiceExpression);
        item.IsRestricted = PayloadReader.GetBool(payload, "isRestricted");
        item.IsMilitary = PayloadReader.GetBool(payload, "isMilitary");
        item.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        item.VisibilityMode = NormalizeEngineeringVisibility(PayloadReader.GetString(payload, "visibilityMode") ?? item.VisibilityMode);
        item.CompatiblePlatformKinds = EngineeringStringList(payload, "compatiblePlatformKinds");
        item.IncompatibleModuleIds = EngineeringStringList(payload, "incompatibleModuleIds");
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        if (existing == null) item.CreatedByUserId = actor.Id;
        return item;
    }

    private EngineeringPlatformSizeClassDefinition BuildSizeClass(IDictionary<string, object> payload, UserAccount actor, EngineeringPlatformSizeClassDefinition? existing)
    {
        var item = existing ?? new EngineeringPlatformSizeClassDefinition();
        item.CampaignId = RequireLength(PayloadReader.GetString(payload, "campaignId") ?? item.CampaignId, 0, 128, "campaignId");
        item.RuleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId") ?? item.RuleSetId, 0, 128, "ruleSetId");
        item.SizeClassId = RequireLength(PayloadReader.GetString(payload, "sizeClassId") ?? item.SizeClassId, 1, 128, "sizeClassId");
        item.Name = RequireLength(PayloadReader.GetString(payload, "name") ?? item.Name, 1, 160, "name");
        item.Description = RequireLength(PayloadReader.GetString(payload, "description") ?? item.Description, 0, 2000, "description");
        item.MaxSlots = NonNegativeInt(payload, "maxSlots", item.MaxSlots);
        item.MaxHardpoints = NonNegativeInt(payload, "maxHardpoints", item.MaxHardpoints);
        item.MaxMassKg = NonNegativeDecimal(payload, "maxMassKg", item.MaxMassKg);
        item.MaxVolumeM3 = NonNegativeDecimal(payload, "maxVolumeM3", item.MaxVolumeM3);
        item.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        item.VisibilityMode = NormalizeEngineeringVisibility(PayloadReader.GetString(payload, "visibilityMode") ?? item.VisibilityMode);
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        if (existing == null) item.CreatedByUserId = actor.Id;
        return item;
    }

    private PresetVehicleDesignDefinition BuildPreset(IDictionary<string, object> payload, UserAccount actor, PresetVehicleDesignDefinition? existing)
    {
        var item = existing ?? new PresetVehicleDesignDefinition();
        item.CampaignId = RequireLength(PayloadReader.GetString(payload, "campaignId") ?? item.CampaignId, 0, 128, "campaignId");
        item.RuleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId") ?? item.RuleSetId, 0, 128, "ruleSetId");
        item.PresetId = RequireLength(PayloadReader.GetString(payload, "presetId") ?? item.PresetId, 0, 128, "presetId");
        item.Name = RequireLength(PayloadReader.GetString(payload, "name") ?? item.Name, 1, 160, "name");
        item.PlatformId = RequireLength(PayloadReader.GetString(payload, "platformId") ?? item.PlatformId, 1, 128, "platformId");
        item.SizeClassId = PayloadReader.GetString(payload, "sizeClassId") ?? item.SizeClassId;
        item.ModuleIds = EngineeringStringList(payload, "moduleIds");
        item.RoleSummary = RequireLength(PayloadReader.GetString(payload, "roleSummary") ?? item.RoleSummary, 0, 1000, "roleSummary");
        item.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        item.VisibilityMode = NormalizeEngineeringVisibility(PayloadReader.GetString(payload, "visibilityMode") ?? item.VisibilityMode);
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        if (existing == null) item.CreatedByUserId = actor.Id;
        return item;
    }

    private VehicleDesignDraft BuildDraft(IDictionary<string, object> payload, UserAccount actor, VehicleDesignDraft? existing)
    {
        var item = existing ?? new VehicleDesignDraft();
        item.CampaignId = RequireLength(PayloadReader.GetString(payload, "campaignId") ?? item.CampaignId, 0, 128, "campaignId");
        item.RuleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId") ?? item.RuleSetId, 0, 128, "ruleSetId");
        item.Name = RequireLength(PayloadReader.GetString(payload, "name") ?? item.Name, 1, 160, "name");
        item.PresetId = PayloadReader.GetString(payload, "presetId") ?? item.PresetId;
        item.OwnerUserId = actor.Id;
        item.OwnerCharacterId = PayloadReader.GetString(payload, "characterId") ?? PayloadReader.GetString(payload, "ownerCharacterId") ?? item.OwnerCharacterId;
        item.ActorEntityId = item.OwnerCharacterId;
        item.PlatformId = RequireLength(PayloadReader.GetString(payload, "platformId") ?? item.PlatformId, 1, 128, "platformId");
        item.SizeClassId = PayloadReader.GetString(payload, "sizeClassId") ?? item.SizeClassId;
        item.ModuleIds = EngineeringStringList(payload, "moduleIds");
        item.IntendedRole = RequireLength(PayloadReader.GetString(payload, "intendedRole") ?? item.IntendedRole, 0, 1000, "intendedRole");
        item.PlayerNotes = RequireLength(PayloadReader.GetString(payload, "playerNotes") ?? item.PlayerNotes, 0, 2000, "playerNotes");
        item.IsPlayerVisible = true;
        item.VisibilityMode = ProjectVisibilityModeIds.OwnerOnly;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        if (existing == null) item.CreatedByUserId = actor.Id;
        return item;
    }

    private EngineeringDesignProjectState BuildEngineeringProject(IDictionary<string, object> payload, UserAccount actor, bool fromPlayer)
    {
        var platformId = RequireLength(PayloadReader.GetString(payload, "platformId"), 1, 128, "platformId");
        var platform = _repositories.EngineeringPlatforms.GetById(platformId)
            ?? _repositories.EngineeringPlatforms.Find(Builders<EngineeringPlatformDefinition>.Filter.Eq(x => x.PlatformId, platformId)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Engineering platform not found.");
        return new EngineeringDesignProjectState
        {
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? platform.CampaignId,
            RuleSetId = PayloadReader.GetString(payload, "ruleSetId") ?? platform.RuleSetId,
            DraftId = PayloadReader.GetString(payload, "draftId") ?? string.Empty,
            PresetId = PayloadReader.GetString(payload, "presetId") ?? string.Empty,
            PlatformId = platform.Id,
            SizeClassId = PayloadReader.GetString(payload, "sizeClassId") ?? platform.SizeClassId,
            ModuleIds = EngineeringStringList(payload, "moduleIds"),
            OwnerUserId = PayloadReader.GetString(payload, "ownerUserId") ?? actor.Id,
            ActorEntityType = PayloadReader.GetString(payload, "actorEntityType") ?? ProjectParticipantEntityTypeIds.PlayerCharacter,
            ActorEntityId = PayloadReader.GetString(payload, "actorEntityId") ?? PayloadReader.GetString(payload, "characterId") ?? string.Empty,
            Name = RequireLength(PayloadReader.GetString(payload, "name") ?? "Инженерный проект", 1, 160, "name"),
            IntendedRole = PayloadReader.GetString(payload, "intendedRole") ?? string.Empty,
            Status = fromPlayer ? EngineeringDesignStatusIds.Submitted : EngineeringAllow(PayloadReader.GetString(payload, "status"), EngineeringDesignStatusIds.Draft, EngineeringDesignStatusIds.Draft, EngineeringDesignStatusIds.Submitted, EngineeringDesignStatusIds.GmReview, EngineeringDesignStatusIds.Approved, EngineeringDesignStatusIds.Active),
            WorkPointsRequired = Math.Max(1, PayloadReader.GetInt(payload, "workPointsRequired") ?? 100),
            IsPlayerVisible = true,
            VisibilityMode = ProjectVisibilityModeIds.PlayerVisible,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            PublicNotes = PayloadReader.GetString(payload, "publicNotes") ?? string.Empty,
            GMNotes = fromPlayer ? string.Empty : (PayloadReader.GetString(payload, "gmNotes") ?? string.Empty)
        };
    }

    private EngineeringValidationBundle BuildValidation(IDictionary<string, object> payload, UserAccount actor)
    {
        var platformId = RequireLength(PayloadReader.GetString(payload, "platformId"), 1, 128, "platformId");
        var platform = _repositories.EngineeringPlatforms.GetById(platformId)
            ?? _repositories.EngineeringPlatforms.Find(Builders<EngineeringPlatformDefinition>.Filter.Eq(x => x.PlatformId, platformId)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Engineering platform not found.");
        var moduleIds = EngineeringStringList(payload, "moduleIds");
        var modules = moduleIds
            .Select(id => _repositories.EngineeringModules.GetById(id) ?? _repositories.EngineeringModules.Find(Builders<EngineeringModuleDefinition>.Filter.Eq(x => x.ModuleId, id)).FirstOrDefault())
            .Where(x => x != null)
            .Cast<EngineeringModuleDefinition>()
            .ToArray();
        var issues = new List<EngineeringValidationIssueValue>();
        foreach (var missing in moduleIds.Where(id => modules.All(m => !string.Equals(m.Id, id, StringComparison.Ordinal) && !string.Equals(m.ModuleId, id, StringComparison.OrdinalIgnoreCase))))
            issues.Add(new EngineeringValidationIssueValue { Severity = EngineeringValidationSeverityIds.Warning, Code = "missing_module", Message = "Модуль не найден: " + missing });

        foreach (var module in modules)
        {
            if (module.CompatiblePlatformKinds.Count > 0 && !module.CompatiblePlatformKinds.Contains(platform.PlatformKind, StringComparer.OrdinalIgnoreCase))
                issues.Add(new EngineeringValidationIssueValue { Severity = EngineeringValidationSeverityIds.GmReview, Code = "platform_kind_review", Message = $"Модуль {module.Name} требует проверки совместимости с платформой {platform.PlatformKind}." });
            foreach (var blockedId in module.IncompatibleModuleIds)
            {
                if (modules.Any(m => string.Equals(m.Id, blockedId, StringComparison.Ordinal) || string.Equals(m.ModuleId, blockedId, StringComparison.OrdinalIgnoreCase)))
                    issues.Add(new EngineeringValidationIssueValue { Severity = EngineeringValidationSeverityIds.HardBlock, Code = "incompatible_modules", Message = $"Модуль {module.Name} несовместим с {blockedId}." });
            }
        }

        var totalSlots = modules.Sum(x => x.SlotCost);
        var totalHardpoints = modules.Sum(x => x.HardpointCost);
        var powerOutput = platform.BasePowerOutput + modules.Sum(x => x.PowerOutput);
        var powerLoad = platform.BasePowerLoad + modules.Sum(x => x.PowerLoad);
        if (totalSlots > platform.BaseSlots)
            issues.Add(new EngineeringValidationIssueValue { Severity = EngineeringValidationSeverityIds.HardBlock, Code = "slots_over_capacity", Message = $"Слоты перегружены: {totalSlots}/{platform.BaseSlots}." });
        if (totalHardpoints > platform.BaseHardpoints)
            issues.Add(new EngineeringValidationIssueValue { Severity = EngineeringValidationSeverityIds.HardBlock, Code = "hardpoints_over_capacity", Message = $"Точки крепления перегружены: {totalHardpoints}/{platform.BaseHardpoints}." });
        if (powerLoad > powerOutput)
        {
            var overloadAllowed = PayloadReader.GetBool(payload, "allowPowerOverload") || _featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringPowerProfiles));
            issues.Add(new EngineeringValidationIssueValue
            {
                Severity = overloadAllowed ? EngineeringValidationSeverityIds.GmReview : EngineeringValidationSeverityIds.HardBlock,
                Code = "power_overload",
                Message = overloadAllowed
                    ? $"Питание перегружено: {powerLoad:0.##}/{powerOutput:0.##}. Требуется профиль перегрузки/решение GM."
                    : $"Питание перегружено: {powerLoad:0.##}/{powerOutput:0.##}."
            });
        }

        var totalCost = platform.BaseCost + modules.Sum(x => x.Cost);
        var result = new EngineeringDesignValidationResult
        {
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? platform.CampaignId,
            DraftId = PayloadReader.GetString(payload, "draftId") ?? string.Empty,
            ProjectId = PayloadReader.GetString(payload, "projectId") ?? string.Empty,
            PlatformId = platform.Id,
            ModuleIds = modules.Select(x => x.Id).ToList(),
            TotalMassKg = platform.BaseMassKg + modules.Sum(x => x.MassKg),
            TotalVolumeM3 = platform.BaseVolumeM3 + modules.Sum(x => x.VolumeM3),
            TotalSlots = totalSlots,
            TotalHardpoints = totalHardpoints,
            TotalPowerOutput = powerOutput,
            TotalPowerLoad = powerLoad,
            TotalCrewRequired = platform.BaseCrewMin + modules.Sum(x => x.CrewRequired),
            TotalCost = totalCost,
            ComplexityScore = platform.DifficultyTier + modules.Sum(x => x.DifficultyTier),
            Issues = issues,
            BuiltByUserId = actor.Id
        };
        result.ValidationId = result.Id;
        result.Status = issues.Any(x => x.Severity == EngineeringValidationSeverityIds.HardBlock) ? EngineeringValidationStatusIds.Blocked
            : issues.Any(x => x.Severity == EngineeringValidationSeverityIds.GmReview) ? EngineeringValidationStatusIds.GmReview
            : issues.Any(x => x.Severity == EngineeringValidationSeverityIds.Warning) ? EngineeringValidationStatusIds.Warnings
            : EngineeringValidationStatusIds.Valid;
        var cost = new EngineeringDesignCostEstimate
        {
            CampaignId = result.CampaignId,
            DraftId = result.DraftId,
            ProjectId = result.ProjectId,
            BaseCost = platform.BaseCost,
            ModuleCost = modules.Sum(x => x.Cost),
            ComplexityCost = result.ComplexityScore * 10m,
            TotalCost = totalCost + result.ComplexityScore * 10m,
            EstimatedWorkDays = Math.Max(1, result.ComplexityScore),
            MaterialSummary = "Оценка проекта; ресурсы и производство будут отдельным этапом."
        };
        cost.EstimateId = cost.Id;
        return new EngineeringValidationBundle(result, cost);
    }

    private void EnsureWeaponProfileForModule(EngineeringModuleDefinition module, string actorId)
    {
        if (!_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringDiceExpressions))) return;
        if (!string.Equals(module.ModuleCategory, EngineeringModuleCategoryIds.Weapon, StringComparison.OrdinalIgnoreCase)) return;
        if (string.IsNullOrWhiteSpace(module.DiceExpression)) return;
        var existing = _repositories.EngineeringWeaponProfiles.Find(Builders<EngineeringWeaponProfileDefinition>.Filter.Eq(x => x.ModuleId, module.Id)).FirstOrDefault();
        var profile = existing ?? new EngineeringWeaponProfileDefinition { CampaignId = module.CampaignId, ModuleId = module.Id, Name = module.Name };
        profile.DiceExpression = module.DiceExpression;
        profile.IsPlayerVisible = module.IsPlayerVisible;
        profile.PublicNotes = module.PublicDescription;
        profile.GMNotes = module.GMDescription;
        if (existing == null) _repositories.EngineeringWeaponProfiles.Insert(profile); else _repositories.EngineeringWeaponProfiles.Replace(profile);
        module.WeaponProfileId = profile.Id;
        _repositories.EngineeringModules.Replace(module);
    }

    private void EnsureProjectFoundationForEngineering(EngineeringDesignProjectState project, UserAccount actor)
    {
        if (!_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringProjectFoundationIntegration))) return;
        var baseProject = new ProjectBaseState
        {
            CampaignId = project.CampaignId,
            RuleSetId = project.RuleSetId,
            ProjectType = ProjectTypeIds.EngineeringDesign,
            Name = project.Name,
            PublicSummary = project.PublicNotes,
            GMSummary = project.GMNotes,
            Status = MapEngineeringStatusToProjectStatus(project.Status),
            ProgressMode = ProjectProgressModeIds.WorkPoints,
            ProgressPercent = project.ProgressPercent,
            WorkPointsDone = project.WorkPointsDone,
            WorkPointsRequired = project.WorkPointsRequired,
            OwnerUserId = project.OwnerUserId,
            OwnerCharacterId = project.ActorEntityId,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            VisibilityMode = project.VisibilityMode,
            IsPlayerVisible = project.IsPlayerVisible,
            ProposalPayload = new Dictionary<string, object>
            {
                { "engineeringProjectId", project.Id },
                { "platformId", project.PlatformId },
                { "moduleIds", project.ModuleIds.ToArray() }
            }
        };
        _repositories.Projects.Insert(baseProject);
        project.ProjectBaseId = baseProject.Id;
        project.ProjectId = baseProject.Id;
        _repositories.EngineeringProjects.Replace(project);
    }

    private void UpdateEngineeringProjectBase(EngineeringDesignProjectState project, string actorId)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectBaseId)) return;
        var baseProject = _repositories.Projects.GetById(project.ProjectBaseId);
        if (baseProject == null) return;
        baseProject.Status = MapEngineeringStatusToProjectStatus(project.Status);
        baseProject.ProgressPercent = project.ProgressPercent;
        baseProject.WorkPointsDone = project.WorkPointsDone;
        baseProject.UpdatedAtUtc = DateTime.UtcNow;
        baseProject.UpdatedByUserId = actorId;
        _repositories.Projects.Replace(baseProject);
    }

    private VehicleDesignBlueprint EnsureBlueprint(EngineeringDesignProjectState project, string actorId, string status)
    {
        var existing = _repositories.EngineeringBlueprints.Find(Builders<VehicleDesignBlueprint>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        var blueprint = existing ?? new VehicleDesignBlueprint
        {
            CampaignId = project.CampaignId,
            RuleSetId = project.RuleSetId,
            ProjectId = project.Id,
            DraftId = project.DraftId,
            PreparedByUserId = actorId
        };
        blueprint.Name = project.Name;
        blueprint.PlatformId = project.PlatformId;
        blueprint.SizeClassId = project.SizeClassId;
        blueprint.ModuleIds = project.ModuleIds;
        blueprint.PublicSummary = project.PublicNotes;
        blueprint.GMNotes = project.GMNotes;
        blueprint.Status = status;
        blueprint.IsPlayerVisible = project.IsPlayerVisible;
        blueprint.VisibilityMode = project.VisibilityMode;
        if (existing == null) _repositories.EngineeringBlueprints.Insert(blueprint); else _repositories.EngineeringBlueprints.Replace(blueprint);
        return blueprint;
    }

    private ResponseEnvelope SetEngineeringProjectStatus(CommandContext context, string status, string operation)
    {
        var actor = RequireAdmin(context);
        if (!EngineeringAdminEnabled()) return EngineeringDisabled(context.Request.Command);
        var item = RequireEngineeringProject(context);
        item.Status = status;
        if (status == EngineeringDesignStatusIds.Active && item.StartedAtUtc == null) item.StartedAtUtc = DateTime.UtcNow;
        if (status == EngineeringDesignStatusIds.Completed) item.CompletedAtUtc = DateTime.UtcNow;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.EngineeringProjects.Replace(item);
        UpdateEngineeringProjectBase(item, actor.Id);
        TryPublishEngineeringSync(item, operation, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Engineering project status updated.", new Dictionary<string, object> { { "item", EngineeringProjectPayload(item, true) } });
    }

    private EngineeringPlatformDefinition RequireEngineeringPlatform(CommandContext context)
    {
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "platformId") ?? PayloadReader.GetString(context.Request.Payload, "id"), 1, 128, "platformId");
        return _repositories.EngineeringPlatforms.GetById(id)
            ?? _repositories.EngineeringPlatforms.Find(Builders<EngineeringPlatformDefinition>.Filter.Eq(x => x.PlatformId, id)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Engineering platform not found.");
    }

    private EngineeringModuleDefinition RequireEngineeringModule(CommandContext context)
    {
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "moduleId") ?? PayloadReader.GetString(context.Request.Payload, "id"), 1, 128, "moduleId");
        return _repositories.EngineeringModules.GetById(id)
            ?? _repositories.EngineeringModules.Find(Builders<EngineeringModuleDefinition>.Filter.Eq(x => x.ModuleId, id)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Engineering module not found.");
    }

    private EngineeringPlatformSizeClassDefinition RequireSizeClass(CommandContext context)
    {
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "sizeClassId") ?? PayloadReader.GetString(context.Request.Payload, "id"), 1, 128, "sizeClassId");
        return _repositories.EngineeringSizeClasses.GetById(id)
            ?? _repositories.EngineeringSizeClasses.Find(Builders<EngineeringPlatformSizeClassDefinition>.Filter.Eq(x => x.SizeClassId, id)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Engineering size class not found.");
    }

    private PresetVehicleDesignDefinition RequirePreset(CommandContext context)
    {
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "presetId") ?? PayloadReader.GetString(context.Request.Payload, "id"), 1, 128, "presetId");
        return _repositories.EngineeringPresets.GetById(id)
            ?? _repositories.EngineeringPresets.Find(Builders<PresetVehicleDesignDefinition>.Filter.Eq(x => x.PresetId, id)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Engineering preset not found.");
    }

    private EngineeringDesignProjectState RequireEngineeringProject(CommandContext context)
    {
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "engineeringProjectId") ?? PayloadReader.GetString(context.Request.Payload, "projectId") ?? PayloadReader.GetString(context.Request.Payload, "id"), 1, 128, "engineeringProjectId");
        return _repositories.EngineeringProjects.GetById(id)
            ?? _repositories.EngineeringProjects.Find(Builders<EngineeringDesignProjectState>.Filter.Eq(x => x.ProjectId, id)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Engineering project not found.");
    }

    private VehicleDesignDraft RequireDraft(CommandContext context)
    {
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "draftId") ?? PayloadReader.GetString(context.Request.Payload, "id"), 1, 128, "draftId");
        return _repositories.EngineeringDesignDrafts.GetById(id)
            ?? _repositories.EngineeringDesignDrafts.Find(Builders<VehicleDesignDraft>.Filter.Eq(x => x.DraftId, id)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Engineering draft not found.");
    }

    private VehicleDesignBlueprint RequireBlueprint(CommandContext context)
    {
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "blueprintId") ?? PayloadReader.GetString(context.Request.Payload, "id"), 1, 128, "blueprintId");
        return _repositories.EngineeringBlueprints.GetById(id)
            ?? _repositories.EngineeringBlueprints.Find(Builders<VehicleDesignBlueprint>.Filter.Eq(x => x.BlueprintId, id)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Engineering blueprint not found.");
    }

    private static Dictionary<string, object> EngineeringPlatformPayload(EngineeringPlatformDefinition x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "platformId", string.IsNullOrWhiteSpace(x.PlatformId) ? x.Id : x.PlatformId }, { "campaignId", x.CampaignId }, { "ruleSetId", x.RuleSetId },
            { "name", x.Name }, { "description", SafeDescription(x.PublicDescription, x.Description) }, { "platformKind", x.PlatformKind }, { "sizeClassId", x.SizeClassId },
            { "baseMassKg", x.BaseMassKg }, { "baseVolumeM3", x.BaseVolumeM3 }, { "baseSlots", x.BaseSlots }, { "baseHardpoints", x.BaseHardpoints },
            { "basePowerOutput", x.BasePowerOutput }, { "basePowerLoad", x.BasePowerLoad }, { "baseCrewMin", x.BaseCrewMin }, { "baseCrewMax", x.BaseCrewMax },
            { "baseCost", x.BaseCost }, { "difficultyTier", x.DifficultyTier }, { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", x.VisibilityMode }, { "isArchived", x.IsArchived }
        };
        if (admin) result["gmDescription"] = x.GMDescription;
        return result;
    }

    private static Dictionary<string, object> EngineeringModulePayload(EngineeringModuleDefinition x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "moduleId", string.IsNullOrWhiteSpace(x.ModuleId) ? x.Id : x.ModuleId }, { "campaignId", x.CampaignId }, { "ruleSetId", x.RuleSetId },
            { "name", x.Name }, { "description", SafeDescription(x.PublicDescription, x.Description) }, { "moduleCategory", x.ModuleCategory }, { "slotType", x.SlotType },
            { "slotCost", x.SlotCost }, { "hardpointCost", x.HardpointCost }, { "massKg", x.MassKg }, { "volumeM3", x.VolumeM3 },
            { "powerOutput", x.PowerOutput }, { "powerLoad", x.PowerLoad }, { "crewRequired", x.CrewRequired }, { "cost", x.Cost },
            { "difficultyTier", x.DifficultyTier }, { "diceExpression", x.DiceExpression }, { "isRestricted", x.IsRestricted }, { "isMilitary", x.IsMilitary },
            { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", x.VisibilityMode }, { "isArchived", x.IsArchived }
        };
        if (admin)
        {
            result["gmDescription"] = x.GMDescription;
            result["compatiblePlatformKinds"] = x.CompatiblePlatformKinds.ToArray();
            result["incompatibleModuleIds"] = x.IncompatibleModuleIds.ToArray();
        }
        return result;
    }

    private static Dictionary<string, object> SizeClassPayload(EngineeringPlatformSizeClassDefinition x, bool admin) => new()
    {
        { "id", x.Id }, { "sizeClassId", x.SizeClassId }, { "campaignId", x.CampaignId }, { "ruleSetId", x.RuleSetId },
        { "name", x.Name }, { "description", x.Description }, { "maxSlots", x.MaxSlots }, { "maxHardpoints", x.MaxHardpoints },
        { "maxMassKg", x.MaxMassKg }, { "maxVolumeM3", x.MaxVolumeM3 }, { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", x.VisibilityMode }, { "isArchived", x.IsArchived }
    };

    private static Dictionary<string, object> PresetPayload(PresetVehicleDesignDefinition x, bool admin) => new()
    {
        { "id", x.Id }, { "presetId", string.IsNullOrWhiteSpace(x.PresetId) ? x.Id : x.PresetId }, { "campaignId", x.CampaignId }, { "ruleSetId", x.RuleSetId },
        { "name", x.Name }, { "platformId", x.PlatformId }, { "sizeClassId", x.SizeClassId }, { "moduleIds", x.ModuleIds.ToArray() },
        { "roleSummary", x.RoleSummary }, { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", x.VisibilityMode }, { "isArchived", x.IsArchived }
    };

    private static Dictionary<string, object> DraftPayload(VehicleDesignDraft x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "draftId", string.IsNullOrWhiteSpace(x.DraftId) ? x.Id : x.DraftId }, { "campaignId", x.CampaignId }, { "ruleSetId", x.RuleSetId },
            { "name", x.Name }, { "platformId", x.PlatformId }, { "sizeClassId", x.SizeClassId }, { "moduleIds", x.ModuleIds.ToArray() },
            { "intendedRole", x.IntendedRole }, { "status", x.Status }, { "validationSummary", x.ValidationSummary }, { "costSummary", x.CostSummary },
            { "playerNotes", x.PlayerNotes }, { "ownerCharacterId", x.OwnerCharacterId }, { "updatedAtUtc", x.UpdatedAtUtc }
        };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> EngineeringProjectPayload(EngineeringDesignProjectState x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "engineeringProjectId", x.Id }, { "projectId", x.ProjectId }, { "projectBaseId", x.ProjectBaseId },
            { "campaignId", x.CampaignId }, { "ruleSetId", x.RuleSetId }, { "name", x.Name }, { "platformId", x.PlatformId },
            { "sizeClassId", x.SizeClassId }, { "moduleIds", x.ModuleIds.ToArray() }, { "ownerUserId", x.OwnerUserId },
            { "actorEntityType", x.ActorEntityType }, { "actorEntityId", x.ActorEntityId }, { "intendedRole", x.IntendedRole },
            { "status", x.Status }, { "progressPercent", x.ProgressPercent }, { "workPointsDone", x.WorkPointsDone }, { "workPointsRequired", x.WorkPointsRequired },
            { "validationStatus", x.ValidationStatus }, { "blueprintStatus", x.BlueprintStatus }, { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", x.VisibilityMode },
            { "publicNotes", x.PublicNotes }, { "updatedAtUtc", x.UpdatedAtUtc }
        };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> ValidationPayload(EngineeringDesignValidationResult x, bool admin) => new()
    {
        { "id", x.Id }, { "validationId", x.Id }, { "status", x.Status }, { "totalMassKg", x.TotalMassKg }, { "totalVolumeM3", x.TotalVolumeM3 },
        { "totalSlots", x.TotalSlots }, { "totalHardpoints", x.TotalHardpoints }, { "totalPowerOutput", x.TotalPowerOutput }, { "totalPowerLoad", x.TotalPowerLoad },
        { "totalCrewRequired", x.TotalCrewRequired }, { "totalCost", x.TotalCost }, { "complexityScore", x.ComplexityScore },
        { "issues", x.Issues.Where(i => admin || i.IsPlayerVisible).Select(i => (object)new Dictionary<string, object> { { "severity", i.Severity }, { "code", i.Code }, { "message", i.Message } }).ToArray() },
        { "summary", ValidationSummary(x) }
    };

    private static Dictionary<string, object> CostPayload(EngineeringDesignCostEstimate x, bool admin) => new()
    {
        { "id", x.Id }, { "estimateId", x.Id }, { "baseCost", x.BaseCost }, { "moduleCost", x.ModuleCost }, { "complexityCost", x.ComplexityCost },
        { "totalCost", x.TotalCost }, { "estimatedWorkDays", x.EstimatedWorkDays }, { "materialSummary", x.MaterialSummary }
    };

    private static Dictionary<string, object> BlueprintPayload(VehicleDesignBlueprint x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "blueprintId", string.IsNullOrWhiteSpace(x.BlueprintId) ? x.Id : x.BlueprintId }, { "campaignId", x.CampaignId }, { "ruleSetId", x.RuleSetId },
            { "projectId", x.ProjectId }, { "draftId", x.DraftId }, { "name", x.Name }, { "platformId", x.PlatformId }, { "sizeClassId", x.SizeClassId },
            { "moduleIds", x.ModuleIds.ToArray() }, { "publicSummary", x.PublicSummary }, { "status", x.Status }, { "isPlayerVisible", x.IsPlayerVisible },
            { "visibilityMode", x.VisibilityMode }, { "preparedAtUtc", x.PreparedAtUtc }, { "acceptedAtUtc", x.AcceptedAtUtc?.ToString("O") ?? string.Empty },
            { "note", "Blueprint reference only; no vehicle instance was created." }
        };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> BlueprintReferencePayload(EngineeringBlueprintReference x) => new()
    {
        { "id", x.Id }, { "blueprintId", x.BlueprintId }, { "projectId", x.ProjectId }, { "displayName", x.DisplayName }, { "referenceType", x.ReferenceType }, { "isPlayerVisible", x.IsPlayerVisible }
    };

    private static string ValidationSummary(EngineeringDesignValidationResult x)
        => $"{x.Status}; масса {x.TotalMassKg:0.##} кг; слоты {x.TotalSlots}; питание {x.TotalPowerLoad:0.##}/{x.TotalPowerOutput:0.##}; замечаний {x.Issues.Count}.";

    private static string SafeDescription(string publicDescription, string description) => string.IsNullOrWhiteSpace(publicDescription) ? description : publicDescription;

    private ResponseEnvelope EngineeringDisabled(string command)
    {
        _logger.Admin($"engineering.command.disabled command={command}");
        return Error("Engineering design MVP is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool EngineeringBaseEnabled() => _featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringDesignMvp)) && _featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseVehicleConstructorV1));
    private bool EngineeringAdminEnabled() => EngineeringBaseEnabled() && _featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringAdminView));
    private bool EngineeringPlayerEnabled() => EngineeringBaseEnabled() && _featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringPlayerView));

    private static bool CanPlayerSeePlatform(EngineeringPlatformDefinition item)
        => item.IsPlayerVisible
           && !string.Equals(item.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(item.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase)
           && !item.IsArchived;

    private static bool CanPlayerSeeModule(EngineeringModuleDefinition item)
        => item.IsPlayerVisible
           && !string.Equals(item.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(item.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase)
           && !item.IsArchived;

    private static bool CanPlayerSeeBlueprint(VehicleDesignBlueprint item)
        => item.IsPlayerVisible
           && !string.Equals(item.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(item.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(item.Status, EngineeringBlueprintStatusIds.Archived, StringComparison.OrdinalIgnoreCase)
           && !item.Archived;

    private static bool CanPlayerSeeEngineeringProject(EngineeringDesignProjectState project, UserAccount actor)
        => project.IsPlayerVisible
           && !string.Equals(project.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(project.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(project.Status, EngineeringDesignStatusIds.Archived, StringComparison.OrdinalIgnoreCase)
           && (string.IsNullOrWhiteSpace(project.OwnerUserId) || string.Equals(project.OwnerUserId, actor.Id, StringComparison.Ordinal));

    private static FilterDefinition<T> EngineeringCampaignFilter<T>(IDictionary<string, object> payload)
    {
        var filter = FilterDefinition<T>.Empty;
        var campaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<T>.Filter.Eq("CampaignId", campaignId);
        return filter;
    }

    private static FilterDefinition<T> PlayerVisibleFilter<T>()
        => Builders<T>.Filter.Eq("IsPlayerVisible", true)
           & Builders<T>.Filter.Ne("VisibilityMode", ProjectVisibilityModeIds.GmOnly)
           & Builders<T>.Filter.Ne("VisibilityMode", ProjectVisibilityModeIds.Hidden);

    private static string NormalizeEngineeringVisibility(string? value)
        => EngineeringAllow(value, ProjectVisibilityModeIds.PlayerVisible, ProjectVisibilityModeIds.GmOnly, ProjectVisibilityModeIds.PlayerVisible, ProjectVisibilityModeIds.Party, ProjectVisibilityModeIds.OwnerOnly, ProjectVisibilityModeIds.Hidden);

    private static string NormalizeDiceExpression(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (text.Length > 80) throw new InvalidOperationException("Dice expression is too long.");
        foreach (var ch in text)
        {
            if (!(char.IsDigit(ch) || ch == 'd' || ch == 'D' || ch == '+' || ch == '-' || ch == '*' || ch == '/' || ch == ' ' || ch == '(' || ch == ')'))
                throw new InvalidOperationException("Dice expression contains unsupported characters.");
        }
        return text;
    }

    private static string MapEngineeringStatusToProjectStatus(string status)
        => status switch
        {
            EngineeringDesignStatusIds.Submitted => ProjectStatusIds.Submitted,
            EngineeringDesignStatusIds.GmReview => ProjectStatusIds.InReview,
            EngineeringDesignStatusIds.Approved => ProjectStatusIds.Approved,
            EngineeringDesignStatusIds.Active => ProjectStatusIds.Active,
            EngineeringDesignStatusIds.AwaitingAcceptance => ProjectStatusIds.AwaitingAcceptance,
            EngineeringDesignStatusIds.Completed => ProjectStatusIds.Completed,
            EngineeringDesignStatusIds.Failed => ProjectStatusIds.Failed,
            EngineeringDesignStatusIds.Cancelled => ProjectStatusIds.Cancelled,
            EngineeringDesignStatusIds.Archived => ProjectStatusIds.Archived,
            _ => ProjectStatusIds.Draft
        };

    private void TryPublishEngineeringSync(EngineeringDesignProjectState project, string operation, string actorId, string requestId)
    {
        if (!_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringSyncEvents))) return;
        TryPublishSyncEvent("engineering.project.changed", project.CampaignId, "engineering_project", project.Id, operation, actorId, new Dictionary<string, object> { { "engineeringProjectId", project.Id }, { "status", project.Status } }, requestId);
    }

    private void TryWriteEngineeringJournal(EngineeringDesignProjectState project, string sourceEventId, string title, string actorId)
    {
        if (!_featureFlags.IsEnabled(nameof(EngineeringFeatureFlags.UseEngineeringJournalIntegration))) return;
        if (!_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp)) || !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion))) return;
        _repositories.EventJournalEntries.Insert(new EventJournalEntryState
        {
            CampaignId = project.CampaignId,
            EntryType = EventJournalEntryTypeIds.Automatic,
            Category = EventJournalCategoryIds.Custom,
            Severity = EventJournalSeverityIds.Information,
            Title = title,
            Summary = project.PublicNotes,
            PlayerSummary = project.PublicNotes,
            GMDetails = project.GMNotes,
            SourceModule = "engineering",
            SourceEventId = sourceEventId + ":" + project.Id,
            SourceEventType = "engineering_project",
            VisibilityMode = project.IsPlayerVisible ? EventJournalVisibilityModeIds.PlayerVisible : EventJournalVisibilityModeIds.GMOnly,
            IsPlayerVisible = project.IsPlayerVisible,
            IsAutomatic = true,
            ActorUserId = actorId,
            SubjectEntityType = "engineering_project",
            SubjectEntityId = project.Id,
            SubjectDisplayName = project.Name,
            CreatedByUserId = actorId,
            CreatedAtUtc = DateTime.UtcNow,
            OccurredAtUtc = DateTime.UtcNow
        });
    }

    private static string EngineeringAllow(string? value, string fallback, params string[] allowed)
    {
        var text = (value ?? string.Empty).Trim();
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : fallback;
    }

    private static int NonNegativeInt(IDictionary<string, object> payload, string key, int fallback) => Math.Max(0, PayloadReader.GetInt(payload, key) ?? fallback);
    private static decimal NonNegativeDecimal(IDictionary<string, object> payload, string key, decimal fallback)
    {
        var raw = PayloadReader.GetString(payload, key);
        if (string.IsNullOrWhiteSpace(raw)) return Math.Max(0, fallback);
        decimal value;
        return decimal.TryParse(raw, out value) ? Math.Max(0, value) : Math.Max(0, fallback);
    }

    private static List<string> EngineeringStringList(IDictionary<string, object> payload, string key)
    {
        var list = PayloadReader.GetList(payload, key);
        if (list == null)
        {
            var csv = PayloadReader.GetString(payload, key) ?? string.Empty;
            return csv.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Take(100).ToList();
        }
        return list.Select(x => Convert.ToString(x) ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).Take(100).ToList();
    }

    private sealed class EngineeringValidationBundle
    {
        public EngineeringValidationBundle(EngineeringDesignValidationResult result, EngineeringDesignCostEstimate cost)
        {
            Result = result;
            Cost = cost;
        }

        public EngineeringDesignValidationResult Result { get; }
        public EngineeringDesignCostEstimate Cost { get; }
    }
}
