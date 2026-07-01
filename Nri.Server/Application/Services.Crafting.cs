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
    public ResponseEnvelope CraftingRecipeList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!CraftingAdminEnabled()) return CraftingDisabled(context.Request.Command);
        }
        else
        {
            if (!CraftingPlayerEnabled()) return CraftingDisabled(context.Request.Command);
        }

        var campaignId = RequireLength(PayloadReader.GetString(context.Request.Payload, "campaignId"), 0, 128, "campaignId");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived") && IsAdmin(actor);
        var filter = FilterDefinition<CraftingRecipeDefinition>.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<CraftingRecipeDefinition>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!includeArchived) filter &= Builders<CraftingRecipeDefinition>.Filter.Eq(x => x.IsArchived, false);
        if (!IsAdmin(actor))
        {
            filter &= Builders<CraftingRecipeDefinition>.Filter.Eq(x => x.IsPlayerVisible, true);
            filter &= Builders<CraftingRecipeDefinition>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.GmOnly);
            filter &= Builders<CraftingRecipeDefinition>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.Hidden);
        }

        var recipes = _repositories.CraftingRecipes.Find(filter)
            .OrderBy(x => x.Name)
            .Take(300);
        if (!IsAdmin(actor))
        {
            var characterId = PayloadReader.GetString(context.Request.Payload, "characterId");
            recipes = recipes.Where(x => CanPlayerSeeCraftingRecipe(x, characterId));
        }

        var items = recipes
            .Select(x => (object)CraftingRecipePayload(x, includeAdminFields: IsAdmin(actor), includeDetails: false))
            .ToArray();
        return Ok("Crafting recipes loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CraftingPlayerRecipeList(CommandContext context) => CraftingRecipeList(context);

    public ResponseEnvelope CraftingRecipeGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var recipe = RequireCraftingRecipe(context);
        if (!IsAdmin(actor) && !CanPlayerSeeCraftingRecipe(recipe, PayloadReader.GetString(context.Request.Payload, "characterId"))) throw new UnauthorizedAccessException("Recipe is not visible.");
        return Ok("Crafting recipe loaded.", new Dictionary<string, object> { { "item", CraftingRecipePayload(recipe, IsAdmin(actor), includeDetails: true) } });
    }

    public ResponseEnvelope CraftingRecipeCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !_featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingRecipesV1))) return CraftingDisabled(context.Request.Command);
        var recipe = BuildCraftingRecipe(context.Request.Payload, actor, existing: null);
        _repositories.CraftingRecipes.Insert(recipe);
        UpsertRecipeRequirements(recipe, context.Request.Payload, actor.Id);
        _logger.Admin($"crafting.recipe.create.done actor={actor.Login} recipeId={recipe.Id}");
        return Ok("Crafting recipe created.", new Dictionary<string, object> { { "item", CraftingRecipePayload(recipe, true, includeDetails: true) } });
    }

    public ResponseEnvelope CraftingRecipeUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !_featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingRecipesV1))) return CraftingDisabled(context.Request.Command);
        var recipe = RequireCraftingRecipe(context);
        BuildCraftingRecipe(context.Request.Payload, actor, recipe);
        _repositories.CraftingRecipes.Replace(recipe);
        UpsertRecipeRequirements(recipe, context.Request.Payload, actor.Id);
        _logger.Admin($"crafting.recipe.update.done actor={actor.Login} recipeId={recipe.Id}");
        return Ok("Crafting recipe updated.", new Dictionary<string, object> { { "item", CraftingRecipePayload(recipe, true, includeDetails: true) } });
    }

    public ResponseEnvelope CraftingRecipeArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled()) return CraftingDisabled(context.Request.Command);
        var recipe = RequireCraftingRecipe(context);
        recipe.IsArchived = true;
        recipe.Archived = true;
        recipe.UpdatedAtUtc = DateTime.UtcNow;
        recipe.UpdatedByUserId = actor.Id;
        _repositories.CraftingRecipes.Replace(recipe);
        return Ok("Crafting recipe archived.", new Dictionary<string, object> { { "item", CraftingRecipePayload(recipe, true, false) } });
    }

    public ResponseEnvelope CraftingProjectList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!CraftingAdminEnabled()) return CraftingDisabled(context.Request.Command);
        }
        else
        {
            if (!CraftingPlayerEnabled()) return CraftingDisabled(context.Request.Command);
        }

        var campaignId = RequireLength(PayloadReader.GetString(context.Request.Payload, "campaignId"), 0, 128, "campaignId");
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 0, 128, "characterId");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived") && IsAdmin(actor);
        var filter = FilterDefinition<CraftingProjectState>.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<CraftingProjectState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(characterId)) filter &= Builders<CraftingProjectState>.Filter.Eq(x => x.ActorEntityId, characterId);
        if (!includeArchived) filter &= Builders<CraftingProjectState>.Filter.Ne(x => x.Status, CraftingProjectStatusIds.Archived);
        if (!IsAdmin(actor))
        {
            filter &= Builders<CraftingProjectState>.Filter.Eq(x => x.IsPlayerVisible, true);
            filter &= Builders<CraftingProjectState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.GmOnly);
            filter &= Builders<CraftingProjectState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.Hidden);
            filter &= Builders<CraftingProjectState>.Filter.Or(
                Builders<CraftingProjectState>.Filter.Eq(x => x.OwnerUserId, actor.Id),
                Builders<CraftingProjectState>.Filter.Eq(x => x.ActorEntityId, characterId));
        }

        var items = _repositories.CraftingProjects.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(300)
            .Select(x => (object)CraftingProjectPayload(x, includeAdminFields: IsAdmin(actor), includeDetails: false))
            .ToArray();
        return Ok("Crafting projects loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CraftingPlayerProjectList(CommandContext context) => CraftingProjectList(context);

    public ResponseEnvelope CraftingProjectGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var project = RequireCraftingProject(context);
        if (!IsAdmin(actor) && !CanPlayerSeeCraftingProject(project, actor)) throw new UnauthorizedAccessException("Crafting project is not visible.");
        return Ok("Crafting project loaded.", new Dictionary<string, object> { { "item", CraftingProjectPayload(project, IsAdmin(actor), includeDetails: true) } });
    }

    public ResponseEnvelope CraftingPlayerProjectGet(CommandContext context) => CraftingProjectGet(context);

    public ResponseEnvelope CraftingProjectCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !CraftingProjectsEnabled()) return CraftingDisabled(context.Request.Command);
        var project = BuildCraftingProject(context.Request.Payload, actor, isPlayerDraft: false);
        _repositories.CraftingProjects.Insert(project);
        EnsureProjectFoundationForCrafting(project, actor);
        TryPublishCraftingSync(project, "created", actor.Id, context.Request.RequestId ?? string.Empty);
        TryWriteCraftingJournal(project, "crafting.project.created", "Crafting project created", actor.Id);
        _logger.Admin($"crafting.project.create.done actor={actor.Login} craftingProjectId={project.Id} recipeId={project.RecipeId}");
        return Ok("Crafting project created.", new Dictionary<string, object> { { "item", CraftingProjectPayload(project, true, includeDetails: true) } });
    }

    public ResponseEnvelope CraftingPlayerDraftCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CraftingPlayerEnabled() || !_featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingRequestIntegration))) return CraftingDisabled(context.Request.Command);
        var submit = PayloadReader.GetBool(context.Request.Payload, "submit");
        var request = BuildCraftingPlayerRequest(context.Request.Payload, actor, submit);
        _repositories.PlayerRequests.Insert(request);
        _logger.Admin($"crafting.player.draft.create.done actor={actor.Login} requestId={request.Id} status={request.Status}");
        return Ok("Crafting request draft created.", new Dictionary<string, object>
        {
            { "requestId", request.Id },
            { "status", request.Status },
            { "title", request.Title },
            { "description", request.Description }
        });
    }

    public ResponseEnvelope CraftingPlayerDraftSubmit(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CraftingPlayerEnabled() || !_featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingRequestIntegration))) return CraftingDisabled(context.Request.Command);
        var requestId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "requestId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "requestId");
        var request = _repositories.PlayerRequests.GetById(requestId) ?? throw new KeyNotFoundException("Crafting request not found.");
        if (!string.Equals(request.CreatedByUserId, actor.Id, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Request belongs to another player.");
        if (request.Status != PlayerRequestStatusIds.Draft) throw new InvalidOperationException("Only draft crafting requests can be submitted.");
        request.Status = PlayerRequestStatusIds.Submitted;
        request.SubmittedAtUtc = DateTime.UtcNow;
        request.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.PlayerRequests.Replace(request);
        return Ok("Crafting request submitted.", new Dictionary<string, object> { { "requestId", request.Id }, { "status", request.Status } });
    }

    public ResponseEnvelope CraftingProjectStart(CommandContext context) => SetCraftingProjectStatus(context, CraftingProjectStatusIds.Active, "started");
    public ResponseEnvelope CraftingProjectCancel(CommandContext context) => SetCraftingProjectStatus(context, CraftingProjectStatusIds.Cancelled, "cancelled", releaseReservations: true);
    public ResponseEnvelope CraftingProjectFail(CommandContext context) => SetCraftingProjectStatus(context, CraftingProjectStatusIds.Failed, "failed", releaseReservations: true);

    public ResponseEnvelope CraftingProjectProgressAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !CraftingProjectsEnabled()) return CraftingDisabled(context.Request.Command);
        var project = RequireCraftingProject(context);
        var deltaPercent = PayloadReader.GetInt(context.Request.Payload, "progressDeltaPercent") ?? PayloadReader.GetInt(context.Request.Payload, "deltaPercent") ?? 0;
        var workDelta = PayloadReader.GetInt(context.Request.Payload, "workPointsDelta") ?? 0;
        project.ProgressPercent = Math.Max(0, Math.Min(100, project.ProgressPercent + deltaPercent));
        project.WorkPointsDone = Math.Max(0, project.WorkPointsDone + workDelta);
        if (project.WorkPointsRequired > 0)
        {
            project.ProgressPercent = Math.Max(project.ProgressPercent, Math.Min(100, (int)Math.Round(project.WorkPointsDone * 100d / project.WorkPointsRequired)));
        }
        if (project.ProgressPercent >= 100) project.Status = CraftingProjectStatusIds.AwaitingAcceptance;
        TouchCraftingProject(project, actor.Id);
        _repositories.CraftingProjects.Replace(project);
        UpdateProjectFoundationProgress(project, actor.Id, deltaPercent, workDelta);
        TryPublishCraftingSync(project, "progress.add", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Crafting project progress updated.", new Dictionary<string, object> { { "item", CraftingProjectPayload(project, true, true) } });
    }

    public ResponseEnvelope CraftingProjectComplete(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !CraftingProjectsEnabled()) return CraftingDisabled(context.Request.Command);
        var project = RequireCraftingProject(context);
        project.Status = CraftingProjectStatusIds.AwaitingAcceptance;
        project.ProgressPercent = 100;
        project.ResultStatus = CraftingResultStatusIds.Prepared;
        TouchCraftingProject(project, actor.Id);
        _repositories.CraftingProjects.Replace(project);
        var result = EnsureCraftingResult(project, actor.Id, context.Request.Payload);
        return Ok("Crafting project is ready for GM acceptance.", new Dictionary<string, object>
        {
            { "item", CraftingProjectPayload(project, true, true) },
            { "result", CraftingResultPayload(result, true) }
        });
    }

    public ResponseEnvelope CraftingReservationPreview(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!CraftingAdminEnabled()) return CraftingDisabled(context.Request.Command);
        }
        else if (!CraftingPlayerEnabled()) return CraftingDisabled(context.Request.Command);

        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var items = BuildCraftingInventoryAvailability(characterId)
            .Select(x => (object)x)
            .ToArray();
        return Ok("Crafting reservation preview loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CraftingReservationReserve(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !CraftingReservationEnabled()) return CraftingDisabled(context.Request.Command);
        var project = RequireCraftingProject(context);
        var characterId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "characterId"), project.TargetInventoryCharacterId, project.ActorEntityId), 1, 128, "characterId");
        var itemId = RequireLength(PayloadReader.GetString(context.Request.Payload, "itemInstanceId"), 1, 128, "itemInstanceId");
        var quantity = (decimal)(PayloadReader.GetDouble(context.Request.Payload, "quantity") ?? 1d);
        if (quantity <= 0) throw new ArgumentException("quantity must be greater than zero.");
        var item = FindInventoryProfileItem(characterId, itemId) ?? throw new KeyNotFoundException("Inventory item not found.");
        var reserved = GetReservedQuantity(characterId, itemId);
        if ((decimal)item.Quantity - reserved < quantity) throw new InvalidOperationException("Not enough unreserved inventory quantity.");

        var reservation = new CraftingResourceReservationState
        {
            CampaignId = project.CampaignId,
            ProjectId = project.ProjectId,
            CraftingProjectId = project.Id,
            RequirementId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requirementId"), 0, 128, "requirementId"),
            CharacterId = characterId,
            ItemInstanceId = item.ItemId,
            DefinitionId = item.DefinitionId,
            DisplayName = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "displayName"), item.Name, item.DefinitionId, item.ItemId),
            QuantityReserved = quantity,
            Unit = RequireLength(PayloadReader.GetString(context.Request.Payload, "unit"), 0, 32, "unit"),
            IsConsumedOnCompletion = !context.Request.Payload.ContainsKey("isConsumedOnCompletion") || PayloadReader.GetBool(context.Request.Payload, "isConsumedOnCompletion"),
            ReservedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            PublicNotes = RequireLength(PayloadReader.GetString(context.Request.Payload, "publicNotes"), 0, 2048, "publicNotes"),
            GMNotes = RequireLength(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 0, 4096, "gmNotes")
        };
        _repositories.CraftingReservations.Insert(reservation);
        project.Status = CraftingProjectStatusIds.WaitingResources;
        TouchCraftingProject(project, actor.Id);
        _repositories.CraftingProjects.Replace(project);
        _logger.Admin($"crafting.reservation.reserve.done actor={actor.Login} craftingProjectId={project.Id} itemId={item.ItemId} quantity={quantity}");
        return Ok("Crafting resource reserved.", new Dictionary<string, object> { { "reservation", CraftingReservationPayload(reservation, true) } });
    }

    public ResponseEnvelope CraftingReservationRelease(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !CraftingReservationEnabled()) return CraftingDisabled(context.Request.Command);
        var reservation = RequireCraftingReservation(context);
        if (reservation.Status == CraftingReservationStatusIds.Consumed) throw new InvalidOperationException("Consumed reservation cannot be released.");
        reservation.Status = CraftingReservationStatusIds.Released;
        reservation.ReleasedAtUtc = DateTime.UtcNow;
        reservation.UpdatedByUserId = actor.Id;
        _repositories.CraftingReservations.Replace(reservation);
        return Ok("Crafting reservation released.", new Dictionary<string, object> { { "reservation", CraftingReservationPayload(reservation, true) } });
    }

    public ResponseEnvelope CraftingReservationConsume(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !CraftingReservationEnabled() || !_featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingResourceConsumption))) return CraftingDisabled(context.Request.Command);
        var project = RequireCraftingProject(context);
        var reservations = ActiveReservations(project.Id).ToList();
        foreach (var reservation in reservations.Where(x => x.IsConsumedOnCompletion))
        {
            ConsumeReservationFromInventory(reservation, actor.Id, context.Request.RequestId ?? string.Empty);
            reservation.Status = CraftingReservationStatusIds.Consumed;
            reservation.QuantityConsumed = reservation.QuantityReserved;
            reservation.ConsumedAtUtc = DateTime.UtcNow;
            reservation.UpdatedByUserId = actor.Id;
            _repositories.CraftingReservations.Replace(reservation);
        }
        return Ok("Crafting reservations consumed.", new Dictionary<string, object> { { "items", reservations.Select(x => (object)CraftingReservationPayload(x, true)).ToArray() } });
    }

    public ResponseEnvelope CraftingResultPrepare(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !CraftingProjectsEnabled()) return CraftingDisabled(context.Request.Command);
        var project = RequireCraftingProject(context);
        var result = EnsureCraftingResult(project, actor.Id, context.Request.Payload);
        project.ResultStatus = CraftingResultStatusIds.Prepared;
        project.Status = CraftingProjectStatusIds.AwaitingAcceptance;
        TouchCraftingProject(project, actor.Id);
        _repositories.CraftingProjects.Replace(project);
        return Ok("Crafting result prepared.", new Dictionary<string, object> { { "result", CraftingResultPayload(result, true) } });
    }

    public ResponseEnvelope CraftingResultAccept(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !CraftingProjectsEnabled() || !_featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingResultCreation))) return CraftingDisabled(context.Request.Command);
        var project = RequireCraftingProject(context);
        var result = EnsureCraftingResult(project, actor.Id, context.Request.Payload);
        if (PayloadReader.GetBool(context.Request.Payload, "consumeResources")) CraftingReservationConsume(context);
        var itemPayload = new Dictionary<string, object>
        {
            { "itemId", string.IsNullOrWhiteSpace(result.CreatedItemInstanceId) ? Guid.NewGuid().ToString("N") : result.CreatedItemInstanceId },
            { "definitionId", result.DefinitionId },
            { "name", result.DisplayName },
            { "quantity", Math.Max(1, result.Quantity) },
            { "notes", FirstNonEmpty(result.PublicNotes, "Создано через крафт.") },
            { "source", "crafting" }
        };
        var native = _profileNativeWriteService.AddInventoryItemProfileNativeAsync(result.TargetCharacterId, new Dictionary<string, object> { { "item", itemPayload } }, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
        if (!native.ProfileWritten && !native.LegacyFacadeSynced) return Error("Crafting result inventory write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        result.Status = CraftingResultStatusIds.Created;
        result.AcceptedAtUtc = DateTime.UtcNow;
        result.CreatedAtInventoryUtc = DateTime.UtcNow;
        result.AcceptedByUserId = actor.Id;
        result.CreatedItemInstanceId = Convert.ToString(itemPayload["itemId"]) ?? string.Empty;
        _repositories.CraftingResults.Replace(result);

        project.Status = CraftingProjectStatusIds.Completed;
        project.ResultStatus = CraftingResultStatusIds.Created;
        project.CompletedAtUtc = DateTime.UtcNow;
        project.ProgressPercent = 100;
        TouchCraftingProject(project, actor.Id);
        _repositories.CraftingProjects.Replace(project);
        TryPublishCraftingSync(project, "completed", actor.Id, context.Request.RequestId ?? string.Empty);
        TryWriteCraftingJournal(project, "crafting.project.completed", "Crafting project completed", actor.Id);
        return Ok("Crafting result accepted and inventory item created.", new Dictionary<string, object>
        {
            { "item", CraftingProjectPayload(project, true, true) },
            { "result", CraftingResultPayload(result, true) }
        });
    }

    private CraftingRecipeDefinition BuildCraftingRecipe(IDictionary<string, object> payload, UserAccount actor, CraftingRecipeDefinition? existing)
    {
        var recipe = existing ?? new CraftingRecipeDefinition();
        recipe.CampaignId = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "campaignId"), 0, 128, "campaignId"), recipe.CampaignId);
        recipe.RuleSetId = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "ruleSetId"), 0, 128, "ruleSetId"), recipe.RuleSetId);
        recipe.RecipeId = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "recipeId"), 0, 128, "recipeId"), recipe.RecipeId, recipe.Id);
        recipe.Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "name"), recipe.Name, "Recipe"), 1, 180, "name");
        recipe.ShortName = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "shortName"), 0, 80, "shortName"), recipe.ShortName);
        recipe.Description = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description"), recipe.Description);
        recipe.PublicDescription = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "publicDescription"), 0, 4096, "publicDescription"), recipe.PublicDescription, recipe.Description);
        recipe.GMDescription = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "gmDescription"), 0, 4096, "gmDescription"), recipe.GMDescription);
        recipe.RecipeCategory = NormalizeCraftingRecipeCategory(PayloadReader.GetString(payload, "recipeCategory") ?? recipe.RecipeCategory);
        recipe.RecipeType = NormalizeCraftingRecipeType(PayloadReader.GetString(payload, "recipeType") ?? recipe.RecipeType);
        recipe.OutputType = NormalizeCraftingOutputType(PayloadReader.GetString(payload, "outputType") ?? recipe.OutputType);
        recipe.OutputDefinitionId = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "outputDefinitionId"), 0, 128, "outputDefinitionId"), recipe.OutputDefinitionId);
        recipe.OutputName = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "outputName"), 0, 180, "outputName"), recipe.OutputName, recipe.Name);
        recipe.OutputQuantity = Math.Max(1, PayloadReader.GetInt(payload, "outputQuantity") ?? recipe.OutputQuantity);
        recipe.OutputQualityRange = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "outputQualityRange"), 0, 128, "outputQualityRange"), recipe.OutputQualityRange);
        recipe.BaseDifficulty = Math.Max(0, PayloadReader.GetInt(payload, "baseDifficulty") ?? recipe.BaseDifficulty);
        recipe.BaseComplexity = Math.Max(0, PayloadReader.GetInt(payload, "baseComplexity") ?? recipe.BaseComplexity);
        recipe.BaseRequiredProgress = Math.Max(1, PayloadReader.GetInt(payload, "baseRequiredProgress") ?? recipe.BaseRequiredProgress);
        recipe.BaseCostSummary = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "baseCostSummary"), 0, 1024, "baseCostSummary"), recipe.BaseCostSummary);
        recipe.IsKnownByDefault = payload.ContainsKey("isKnownByDefault") ? PayloadReader.GetBool(payload, "isKnownByDefault") : recipe.IsKnownByDefault;
        recipe.IsPlayerDiscoverable = payload.ContainsKey("isPlayerDiscoverable") ? PayloadReader.GetBool(payload, "isPlayerDiscoverable") : recipe.IsPlayerDiscoverable;
        recipe.IsPlayerVisible = payload.ContainsKey("isPlayerVisible") ? PayloadReader.GetBool(payload, "isPlayerVisible") : recipe.IsPlayerVisible;
        recipe.VisibilityMode = FirstNonEmpty(NormalizeCraftingVisibility(PayloadReader.GetString(payload, "visibilityMode"), allowEmpty: true), recipe.VisibilityMode);
        recipe.RequiresGMApproval = payload.ContainsKey("requiresGMApproval") ? PayloadReader.GetBool(payload, "requiresGMApproval") : recipe.RequiresGMApproval;
        recipe.IsCustomAllowed = payload.ContainsKey("isCustomAllowed") ? PayloadReader.GetBool(payload, "isCustomAllowed") : recipe.IsCustomAllowed;
        recipe.UpdatedAtUtc = DateTime.UtcNow;
        recipe.UpdatedByUserId = actor.Id;
        if (existing == null) recipe.CreatedByUserId = actor.Id;
        recipe.Tags = PayloadStringList(payload, "tags");
        return recipe;
    }

    private void UpsertRecipeRequirements(CraftingRecipeDefinition recipe, IDictionary<string, object> payload, string actorId)
    {
        if (!payload.ContainsKey("ingredients")) return;
        var items = PayloadReader.GetList(payload, "ingredients") ?? new List<object>();
        foreach (var raw in items)
        {
            var map = ToDictionary(raw);
            if (map.Count == 0) continue;
            var ingredient = new RecipeIngredientRequirement
            {
                RecipeId = recipe.Id,
                CampaignId = recipe.CampaignId,
                IngredientType = NormalizeCraftingIngredientType(PayloadReader.GetString(map, "ingredientType")),
                IngredientDefinitionId = RequireLength(PayloadReader.GetString(map, "ingredientDefinitionId"), 0, 128, "ingredientDefinitionId"),
                IngredientName = RequireLength(FirstNonEmpty(PayloadReader.GetString(map, "ingredientName"), PayloadReader.GetString(map, "name"), "Материал"), 1, 180, "ingredientName"),
                RequiredQuantity = Math.Max(0m, (decimal)(PayloadReader.GetDouble(map, "requiredQuantity") ?? PayloadReader.GetDouble(map, "quantity") ?? 1d)),
                Unit = RequireLength(PayloadReader.GetString(map, "unit"), 0, 32, "unit"),
                IsConsumed = !map.ContainsKey("isConsumed") || PayloadReader.GetBool(map, "isConsumed"),
                IsSubstitutable = PayloadReader.GetBool(map, "isSubstitutable"),
                IsMandatory = !map.ContainsKey("isMandatory") || PayloadReader.GetBool(map, "isMandatory"),
                IsPlayerVisible = !map.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(map, "isPlayerVisible"),
                PublicSummary = RequireLength(PayloadReader.GetString(map, "publicSummary"), 0, 1024, "publicSummary"),
                GMSummary = RequireLength(PayloadReader.GetString(map, "gmSummary"), 0, 2048, "gmSummary")
            };
            _repositories.CraftingRecipeIngredients.Insert(ingredient);
        }
    }

    private CraftingProjectState BuildCraftingProject(IDictionary<string, object> payload, UserAccount actor, bool isPlayerDraft)
    {
        var recipeId = RequireLength(PayloadReader.GetString(payload, "recipeId"), 0, 128, "recipeId");
        var recipe = string.IsNullOrWhiteSpace(recipeId) ? null : _repositories.CraftingRecipes.GetById(recipeId);
        var actorEntityId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "characterId"), PayloadReader.GetString(payload, "actorEntityId")), 0, 128, "actorEntityId");
        return new CraftingProjectState
        {
            CampaignId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), recipe?.CampaignId ?? "default"), 1, 128, "campaignId"),
            RuleSetId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "ruleSetId"), recipe?.RuleSetId ?? string.Empty), 0, 128, "ruleSetId"),
            RecipeId = recipe?.Id ?? recipeId,
            RecipeName = FirstNonEmpty(recipe?.Name ?? string.Empty, PayloadReader.GetString(payload, "recipeName"), PayloadReader.GetString(payload, "name"), "Крафт"),
            OwnerUserId = isPlayerDraft ? actor.Id : RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "ownerUserId"), actor.Id), 0, 128, "ownerUserId"),
            ActorEntityType = NormalizeParticipantEntityType(PayloadReader.GetString(payload, "actorEntityType")),
            ActorEntityId = actorEntityId,
            ActorDisplayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "actorDisplayName"), PayloadReader.GetString(payload, "characterName"), "Исполнитель"), 0, 180, "actorDisplayName"),
            TargetInventoryCharacterId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "targetInventoryCharacterId"), actorEntityId), 0, 128, "targetInventoryCharacterId"),
            Status = isPlayerDraft ? CraftingProjectStatusIds.Submitted : NormalizeCraftingProjectStatus(PayloadReader.GetString(payload, "status")),
            WorkPointsRequired = Math.Max(0, PayloadReader.GetInt(payload, "workPointsRequired") ?? recipe?.BaseRequiredProgress ?? 100),
            IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
            VisibilityMode = NormalizeCraftingVisibility(PayloadReader.GetString(payload, "visibilityMode")),
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            PublicNotes = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "publicNotes"), PayloadReader.GetString(payload, "description")), 0, 2048, "publicNotes"),
            GMNotes = isPlayerDraft ? string.Empty : RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes"),
            CustomProposalPayload = SanitizeCraftingPayload(payload)
        };
    }

    private void EnsureProjectFoundationForCrafting(CraftingProjectState crafting, UserAccount actor)
    {
        if (!_featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingProjectFoundationIntegration))) return;
        if (!ProjectAdminEnabled()) return;
        if (!string.IsNullOrWhiteSpace(crafting.ProjectId) && _repositories.Projects.GetById(crafting.ProjectId) != null) return;
        var project = new ProjectBaseState
        {
            CampaignId = crafting.CampaignId,
            RuleSetId = crafting.RuleSetId,
            ProjectType = ProjectTypeIds.Crafting,
            Name = crafting.RecipeName,
            PublicSummary = crafting.PublicNotes,
            GMSummary = crafting.GMNotes,
            Status = MapCraftingStatusToProjectStatus(crafting.Status),
            ApprovalStatus = ProjectApprovalStatusIds.Approved,
            ProgressMode = ProjectProgressModeIds.WorkPoints,
            ResultApplicationMode = ProjectResultApplicationModeIds.CreateItemLater,
            ProgressPercent = crafting.ProgressPercent,
            WorkPointsDone = crafting.WorkPointsDone,
            WorkPointsRequired = crafting.WorkPointsRequired,
            OwnerUserId = crafting.OwnerUserId,
            OwnerCharacterId = crafting.ActorEntityId,
            CreatedByUserId = crafting.CreatedByUserId,
            UpdatedByUserId = actor.Id,
            VisibilityMode = crafting.VisibilityMode,
            IsPlayerVisible = crafting.IsPlayerVisible,
            PublicNotes = crafting.PublicNotes,
            GMNotes = crafting.GMNotes,
            ProposalPayload = new Dictionary<string, object>(crafting.CustomProposalPayload),
            ExpectedResultSummary = new Dictionary<string, object> { { "recipeId", crafting.RecipeId }, { "recipeName", crafting.RecipeName } }
        };
        _repositories.Projects.Insert(project);
        crafting.ProjectId = project.Id;
        _repositories.CraftingProjects.Replace(crafting);
    }

    private void UpdateProjectFoundationProgress(CraftingProjectState crafting, string actorId, int deltaPercent, int workDelta)
    {
        if (string.IsNullOrWhiteSpace(crafting.ProjectId)) return;
        var project = _repositories.Projects.GetById(crafting.ProjectId);
        if (project == null) return;
        project.ProgressPercent = crafting.ProgressPercent;
        project.WorkPointsDone = crafting.WorkPointsDone;
        project.WorkPointsRequired = crafting.WorkPointsRequired;
        project.Status = MapCraftingStatusToProjectStatus(crafting.Status);
        TouchProject(project, actorId);
        _repositories.Projects.Replace(project);
        AddProjectAudit(project, actorId, "crafting.progress", "Crafting progress updated.", "Крафт продвинулся.", isPlayerVisible: crafting.IsPlayerVisible);
        if (ProjectProgressEnabled() && (deltaPercent != 0 || workDelta != 0))
        {
            _repositories.ProjectProgressEntries.Insert(new ProjectProgressEntryState
            {
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                EntryType = "crafting",
                Summary = "Crafting progress updated.",
                PublicSummary = "Крафт продвинулся.",
                ProgressDeltaPercent = deltaPercent,
                WorkPointsDelta = workDelta,
                ResultProgressPercent = crafting.ProgressPercent,
                IsPlayerVisible = crafting.IsPlayerVisible,
                VisibilityMode = crafting.VisibilityMode,
                CreatedByUserId = actorId
            });
        }
    }

    private ResponseEnvelope SetCraftingProjectStatus(CommandContext context, string status, string operation, bool releaseReservations = false)
    {
        var actor = RequireAdmin(context);
        if (!CraftingAdminEnabled() || !CraftingProjectsEnabled()) return CraftingDisabled(context.Request.Command);
        var project = RequireCraftingProject(context);
        project.Status = status;
        if (status == CraftingProjectStatusIds.Active && project.StartedAtUtc == null) project.StartedAtUtc = DateTime.UtcNow;
        if (status == CraftingProjectStatusIds.Completed) project.CompletedAtUtc = DateTime.UtcNow;
        TouchCraftingProject(project, actor.Id);
        _repositories.CraftingProjects.Replace(project);
        if (releaseReservations) ReleaseProjectReservations(project.Id, actor.Id, operation);
        TryPublishCraftingSync(project, operation, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Crafting project status updated.", new Dictionary<string, object> { { "item", CraftingProjectPayload(project, true, true) } });
    }

    private CraftingProjectItemResult EnsureCraftingResult(CraftingProjectState project, string actorId, IDictionary<string, object> payload)
    {
        var existing = _repositories.CraftingResults.Find(Builders<CraftingProjectItemResult>.Filter.Eq(x => x.CraftingProjectId, project.Id)).FirstOrDefault();
        var recipe = string.IsNullOrWhiteSpace(project.RecipeId) ? null : _repositories.CraftingRecipes.GetById(project.RecipeId);
        var result = existing ?? new CraftingProjectItemResult { CraftingProjectId = project.Id, ProjectId = project.ProjectId, CampaignId = project.CampaignId, PreparedByUserId = actorId };
        result.TargetCharacterId = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "targetCharacterId"), 0, 128, "targetCharacterId"), result.TargetCharacterId, project.TargetInventoryCharacterId, project.ActorEntityId);
        result.ResultType = NormalizeCraftingOutputType(FirstNonEmpty(PayloadReader.GetString(payload, "resultType"), recipe?.OutputType ?? result.ResultType));
        result.DefinitionId = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "definitionId"), 0, 128, "definitionId"), result.DefinitionId, recipe?.OutputDefinitionId ?? string.Empty);
        result.DisplayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "displayName"), result.DisplayName, recipe?.OutputName ?? string.Empty, project.RecipeName), 1, 180, "displayName");
        result.Quantity = Math.Max(1, PayloadReader.GetInt(payload, "quantity") ?? result.Quantity);
        result.QualitySummary = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "qualitySummary"), 0, 512, "qualitySummary"), result.QualitySummary, project.QualitySummary);
        result.PublicNotes = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 2048, "publicNotes"), result.PublicNotes);
        result.GMNotes = FirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes"), result.GMNotes);
        result.Status = CraftingResultStatusIds.Prepared;
        if (existing == null) _repositories.CraftingResults.Insert(result); else _repositories.CraftingResults.Replace(result);
        return result;
    }

    private PlayerRequestState BuildCraftingPlayerRequest(IDictionary<string, object> payload, UserAccount actor, bool submit)
    {
        var now = DateTime.UtcNow;
        var title = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "title"), "Заявка на крафт"), 2, 160, "title");
        var description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        var summary = FirstNonEmpty(PayloadReader.GetString(payload, "proposalPayloadSummary"), title);
        return new PlayerRequestState
        {
            RequestNumber = NextPlayerRequestNumber(),
            CampaignId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), "default"), 1, 128, "campaignId"),
            CharacterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 0, 128, "characterId"),
            CompanionId = RequireLength(PayloadReader.GetString(payload, "companionId"), 0, 128, "companionId"),
            CreatedByUserId = actor.Id,
            CreatedByDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            RequestType = PlayerRequestTypeIds.Crafting,
            Title = title,
            Description = description,
            Status = submit ? PlayerRequestStatusIds.Submitted : PlayerRequestStatusIds.Draft,
            SubmittedAtUtc = submit ? now : null,
            Priority = PlayerRequestPriorityIds.Normal,
            LinkedEntityType = "crafting",
            ProposalType = "crafting",
            ProposalPayloadSummary = summary,
            ProposalPayload = new PlayerRequestProposalDraft
            {
                ProposalType = "crafting",
                DisplaySummary = summary,
                EstimatedResult = RequireLength(PayloadReader.GetString(payload, "estimatedResult"), 0, 1024, "estimatedResult"),
                RequiresGMApproval = true,
                Parameters = SanitizeCraftingPayload(payload)
            },
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private IEnumerable<Dictionary<string, object>> BuildCraftingInventoryAvailability(string characterId)
    {
        var doc = _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        var items = doc?.Profile?.Items ?? new List<CharacterInventoryItemProfileValue>();
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId)) continue;
            var reserved = GetReservedQuantity(characterId, item.ItemId);
            yield return new Dictionary<string, object>
            {
                { "itemInstanceId", item.ItemId },
                { "definitionId", item.DefinitionId ?? string.Empty },
                { "displayName", item.Name ?? string.Empty },
                { "quantity", item.Quantity },
                { "reservedQuantity", reserved },
                { "availableQuantity", Math.Max(0m, (decimal)item.Quantity - reserved) }
            };
        }
    }

    private CharacterInventoryItemProfileValue? FindInventoryProfileItem(string characterId, string itemId)
    {
        var doc = _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return (doc?.Profile?.Items ?? new List<CharacterInventoryItemProfileValue>())
            .FirstOrDefault(x => string.Equals(x.ItemId, itemId, StringComparison.OrdinalIgnoreCase));
    }

    private decimal GetReservedQuantity(string characterId, string itemId)
    {
        var filter = Builders<CraftingResourceReservationState>.Filter.Eq(x => x.CharacterId, characterId)
            & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.ItemInstanceId, itemId)
            & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.Status, CraftingReservationStatusIds.Reserved);
        return _repositories.CraftingReservations.Find(filter).Sum(x => x.QuantityReserved);
    }

    private IEnumerable<CraftingResourceReservationState> ActiveReservations(string craftingProjectId)
        => _repositories.CraftingReservations.Find(Builders<CraftingResourceReservationState>.Filter.Eq(x => x.CraftingProjectId, craftingProjectId)
            & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.Status, CraftingReservationStatusIds.Reserved));

    private void ConsumeReservationFromInventory(CraftingResourceReservationState reservation, string actorId, string requestId)
    {
        if (reservation.QuantityReserved != decimal.Truncate(reservation.QuantityReserved)) throw new InvalidOperationException("Only whole inventory quantities can be consumed in MVP.");
        var doc = _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, reservation.CharacterId)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Inventory profile not found.");
        var item = (doc.Profile?.Items ?? new List<CharacterInventoryItemProfileValue>()).FirstOrDefault(x => string.Equals(x.ItemId, reservation.ItemInstanceId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("Reserved inventory item not found.");
        var quantity = (int)reservation.QuantityReserved;
        if (item.Quantity < quantity) throw new InvalidOperationException("Reserved inventory quantity is no longer available.");
        item.Quantity -= quantity;
        item.Source = "crafting_consumption";
        if (item.Quantity <= 0) doc.Profile.Items.Remove(item);
        doc.UpdatedUtc = DateTime.UtcNow;
        _mongo.CharacterInventoryProfiles.ReplaceOne(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, doc.Id), doc);
        _profileNativeWriteService.SyncLegacyInventoryFacadeAsync(reservation.CharacterId, doc.Profile, actorId, requestId).GetAwaiter().GetResult();
    }

    private void ReleaseProjectReservations(string craftingProjectId, string actorId, string reason)
    {
        foreach (var reservation in ActiveReservations(craftingProjectId))
        {
            reservation.Status = CraftingReservationStatusIds.Released;
            reservation.ReleasedAtUtc = DateTime.UtcNow;
            reservation.UpdatedByUserId = actorId;
            reservation.PublicNotes = FirstNonEmpty(reservation.PublicNotes, "Резерв освобождён: " + reason);
            _repositories.CraftingReservations.Replace(reservation);
        }
    }

    private CraftingRecipeDefinition RequireCraftingRecipe(CommandContext context)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "recipeId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "recipeId");
        return _repositories.CraftingRecipes.GetById(id) ?? throw new KeyNotFoundException("Crafting recipe not found.");
    }

    private CraftingProjectState RequireCraftingProject(CommandContext context)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "craftingProjectId"), PayloadReader.GetString(context.Request.Payload, "projectId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "craftingProjectId");
        return _repositories.CraftingProjects.GetById(id)
            ?? _repositories.CraftingProjects.Find(Builders<CraftingProjectState>.Filter.Eq(x => x.ProjectId, id)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Crafting project not found.");
    }

    private CraftingResourceReservationState RequireCraftingReservation(CommandContext context)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "reservationId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "reservationId");
        return _repositories.CraftingReservations.GetById(id) ?? throw new KeyNotFoundException("Crafting reservation not found.");
    }

    private static Dictionary<string, object> CraftingRecipePayload(CraftingRecipeDefinition recipe, bool includeAdminFields, bool includeDetails)
    {
        var result = new Dictionary<string, object>
        {
            { "recipeId", recipe.Id },
            { "recipeCode", recipe.RecipeId },
            { "campaignId", recipe.CampaignId },
            { "ruleSetId", recipe.RuleSetId },
            { "name", recipe.Name },
            { "shortName", recipe.ShortName },
            { "description", includeAdminFields ? FirstNonEmpty(recipe.PublicDescription, recipe.Description) : recipe.PublicDescription },
            { "recipeCategory", recipe.RecipeCategory },
            { "recipeType", includeAdminFields ? recipe.RecipeType : SafeRecipeType(recipe.RecipeType) },
            { "outputType", recipe.OutputType },
            { "outputName", recipe.OutputName },
            { "outputQuantity", recipe.OutputQuantity },
            { "baseDifficulty", recipe.BaseDifficulty },
            { "baseComplexity", recipe.BaseComplexity },
            { "baseRequiredProgress", recipe.BaseRequiredProgress },
            { "isKnownByDefault", recipe.IsKnownByDefault },
            { "isPlayerDiscoverable", recipe.IsPlayerDiscoverable },
            { "isPlayerVisible", recipe.IsPlayerVisible },
            { "visibilityMode", includeAdminFields ? recipe.VisibilityMode : SafePlayerVisibility(recipe.VisibilityMode) },
            { "requiresGMApproval", recipe.RequiresGMApproval },
            { "isCustomAllowed", recipe.IsCustomAllowed },
            { "isArchived", recipe.IsArchived },
            { "updatedAtUtc", recipe.UpdatedAtUtc }
        };
        if (includeAdminFields)
        {
            result["gmDescription"] = recipe.GMDescription;
            result["outputDefinitionId"] = recipe.OutputDefinitionId;
            result["baseCostSummary"] = recipe.BaseCostSummary;
            result["requiredKnowledgeDefinitionIds"] = recipe.RequiredKnowledgeDefinitionIds.Cast<object>().ToArray();
            result["requiredAppliedKnowledgeIds"] = recipe.RequiredAppliedKnowledgeIds.Cast<object>().ToArray();
        }
        return result;
    }

    private Dictionary<string, object> CraftingProjectPayload(CraftingProjectState project, bool includeAdminFields, bool includeDetails)
    {
        var result = new Dictionary<string, object>
        {
            { "craftingProjectId", project.Id },
            { "projectId", project.ProjectId },
            { "campaignId", project.CampaignId },
            { "ruleSetId", project.RuleSetId },
            { "recipeId", project.RecipeId },
            { "recipeName", project.RecipeName },
            { "actorEntityType", project.ActorEntityType },
            { "actorEntityId", includeAdminFields ? project.ActorEntityId : string.Empty },
            { "actorDisplayName", project.ActorDisplayName },
            { "status", project.Status },
            { "progressPercent", project.ProgressPercent },
            { "workPointsDone", project.WorkPointsDone },
            { "workPointsRequired", project.WorkPointsRequired },
            { "qualitySummary", project.QualitySummary },
            { "resultStatus", project.ResultStatus },
            { "isPlayerVisible", project.IsPlayerVisible },
            { "visibilityMode", includeAdminFields ? project.VisibilityMode : SafePlayerVisibility(project.VisibilityMode) },
            { "publicNotes", project.PublicNotes },
            { "updatedAtUtc", project.UpdatedAtUtc }
        };
        if (includeAdminFields)
        {
            result["ownerUserId"] = project.OwnerUserId;
            result["targetInventoryCharacterId"] = project.TargetInventoryCharacterId;
            result["gmNotes"] = project.GMNotes;
        }
        if (includeDetails)
        {
            result["reservations"] = _repositories.CraftingReservations.Find(Builders<CraftingResourceReservationState>.Filter.Eq(x => x.CraftingProjectId, project.Id)).Select(x => (object)CraftingReservationPayload(x, includeAdminFields)).ToArray();
            result["results"] = _repositories.CraftingResults.Find(Builders<CraftingProjectItemResult>.Filter.Eq(x => x.CraftingProjectId, project.Id)).Select(x => (object)CraftingResultPayload(x, includeAdminFields)).ToArray();
        }
        return result;
    }

    private static Dictionary<string, object> CraftingReservationPayload(CraftingResourceReservationState x, bool includeAdminFields)
    {
        var result = new Dictionary<string, object>
        {
            { "reservationId", x.Id },
            { "craftingProjectId", x.CraftingProjectId },
            { "displayName", x.DisplayName },
            { "quantityReserved", x.QuantityReserved },
            { "quantityConsumed", x.QuantityConsumed },
            { "unit", x.Unit },
            { "status", x.Status },
            { "isConsumedOnCompletion", x.IsConsumedOnCompletion },
            { "isPlayerVisible", x.IsPlayerVisible },
            { "reservedAtUtc", x.ReservedAtUtc },
            { "publicNotes", x.PublicNotes }
        };
        if (includeAdminFields)
        {
            result["characterId"] = x.CharacterId;
            result["itemInstanceId"] = x.ItemInstanceId;
            result["definitionId"] = x.DefinitionId;
            result["gmNotes"] = x.GMNotes;
        }
        return result;
    }

    private static Dictionary<string, object> CraftingResultPayload(CraftingProjectItemResult x, bool includeAdminFields)
    {
        var result = new Dictionary<string, object>
        {
            { "resultId", x.Id },
            { "craftingProjectId", x.CraftingProjectId },
            { "resultType", x.ResultType },
            { "displayName", x.DisplayName },
            { "quantity", x.Quantity },
            { "qualitySummary", x.QualitySummary },
            { "status", x.Status },
            { "isPlayerVisible", x.IsPlayerVisible },
            { "publicNotes", x.PublicNotes }
        };
        if (includeAdminFields)
        {
            result["targetCharacterId"] = x.TargetCharacterId;
            result["definitionId"] = x.DefinitionId;
            result["createdItemInstanceId"] = x.CreatedItemInstanceId;
            result["gmNotes"] = x.GMNotes;
        }
        return result;
    }

    private bool CanPlayerSeeCraftingRecipe(CraftingRecipeDefinition recipe, string? characterId)
    {
        if (!recipe.IsPlayerVisible
            || recipe.IsArchived
            || string.Equals(recipe.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase)
            || string.Equals(recipe.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!_featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingKnowledgeIntegration)))
            return true;

        var requirements = _repositories.CraftingRecipeKnowledgeRequirements
            .Find(Builders<RecipeKnowledgeRequirement>.Filter.Eq(x => x.RecipeId, recipe.Id)
                  & Builders<RecipeKnowledgeRequirement>.Filter.Eq(x => x.IsMandatory, true))
            .ToArray();
        if (requirements.Length == 0) return true;
        if (string.IsNullOrWhiteSpace(characterId)) return false;

        foreach (var requirement in requirements)
        {
            if (string.IsNullOrWhiteSpace(requirement.KnowledgeDefinitionId)) continue;
            var hasKnowledge = _repositories.EntityKnowledgeStates
                .Find(Builders<EntityKnowledgeState>.Filter.Eq(x => x.CampaignId, recipe.CampaignId)
                      & Builders<EntityKnowledgeState>.Filter.Eq(x => x.EntityId, characterId)
                      & Builders<EntityKnowledgeState>.Filter.Eq(x => x.KnowledgeDefinitionId, requirement.KnowledgeDefinitionId)
                      & Builders<EntityKnowledgeState>.Filter.Eq(x => x.IsPlayerVisible, true)
                      & Builders<EntityKnowledgeState>.Filter.Eq(x => x.IsArchived, false))
                .Any();
            if (!hasKnowledge) return false;
        }

        return true;
    }

    private static bool CanPlayerSeeCraftingProject(CraftingProjectState project, UserAccount actor)
        => !string.Equals(project.Status, CraftingProjectStatusIds.Archived, StringComparison.OrdinalIgnoreCase)
           && project.IsPlayerVisible
           && !string.Equals(project.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(project.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase)
           && (string.IsNullOrWhiteSpace(project.OwnerUserId) || string.Equals(project.OwnerUserId, actor.Id, StringComparison.Ordinal));

    private void TouchCraftingProject(CraftingProjectState project, string actorId)
    {
        project.UpdatedAtUtc = DateTime.UtcNow;
        project.UpdatedByUserId = actorId;
    }

    private void TryPublishCraftingSync(CraftingProjectState project, string operation, string actorId, string requestId)
    {
        if (!_featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingSyncEvents))) return;
        TryPublishSyncEvent("crafting.project.changed", project.CampaignId, "crafting_project", project.Id, operation, actorId, new Dictionary<string, object> { { "craftingProjectId", project.Id }, { "status", project.Status } }, requestId);
    }

    private void TryWriteCraftingJournal(CraftingProjectState project, string sourceEventId, string title, string actorId)
    {
        if (!_featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingJournalIntegration))) return;
        if (!_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp)) || !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion))) return;
        _repositories.EventJournalEntries.Insert(new EventJournalEntryState
        {
            CampaignId = project.CampaignId,
            EntryType = EventJournalEntryTypeIds.Automatic,
            Category = EventJournalCategoryIds.Inventory,
            Severity = EventJournalSeverityIds.Information,
            Title = title,
            Summary = project.PublicNotes,
            PlayerSummary = project.PublicNotes,
            GMDetails = project.GMNotes,
            SourceModule = "crafting",
            SourceEventId = sourceEventId + ":" + project.Id,
            SourceEventType = "crafting_project",
            VisibilityMode = project.IsPlayerVisible ? EventJournalVisibilityModeIds.PlayerVisible : EventJournalVisibilityModeIds.GMOnly,
            IsPlayerVisible = project.IsPlayerVisible,
            IsAutomatic = true,
            ActorUserId = actorId,
            SubjectEntityType = "crafting_project",
            SubjectEntityId = project.Id,
            SubjectDisplayName = project.RecipeName,
            CreatedByUserId = actorId,
            CreatedAtUtc = DateTime.UtcNow,
            OccurredAtUtc = DateTime.UtcNow
        });
    }

    private ResponseEnvelope CraftingDisabled(string command)
    {
        _logger.Admin($"crafting.command.disabled command={command}");
        return Error("Crafting MVP is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool CraftingBaseEnabled() => _featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingMvp));
    private bool CraftingAdminEnabled() => CraftingBaseEnabled() && _featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingAdminView));
    private bool CraftingPlayerEnabled() => CraftingBaseEnabled() && _featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingPlayerView));
    private bool CraftingProjectsEnabled() => _featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingProjectsV1));
    private bool CraftingReservationEnabled() => _featureFlags.IsEnabled(nameof(CraftingFeatureFlags.UseCraftingInventoryReservation));

    private static string NormalizeCraftingRecipeCategory(string? value) => Allow(value, CraftingRecipeCategoryIds.Custom, CraftingRecipeCategoryIds.Consumable, CraftingRecipeCategoryIds.Weapon, CraftingRecipeCategoryIds.Armor, CraftingRecipeCategoryIds.Tool, CraftingRecipeCategoryIds.Equipment, CraftingRecipeCategoryIds.Ammunition, CraftingRecipeCategoryIds.MagicalItem, CraftingRecipeCategoryIds.AlchemicalItem, CraftingRecipeCategoryIds.Component, CraftingRecipeCategoryIds.MaterialProcessing, CraftingRecipeCategoryIds.Repair, CraftingRecipeCategoryIds.Modification, CraftingRecipeCategoryIds.Document, CraftingRecipeCategoryIds.RitualComponent, CraftingRecipeCategoryIds.Medicine, CraftingRecipeCategoryIds.Food, CraftingRecipeCategoryIds.Custom);
    private static string NormalizeCraftingRecipeType(string? value) => Allow(value, CraftingRecipeTypeIds.Standard, CraftingRecipeTypeIds.Standard, CraftingRecipeTypeIds.Discovered, CraftingRecipeTypeIds.ResearchUnlocked, CraftingRecipeTypeIds.GmDefined, CraftingRecipeTypeIds.FactionSecret, CraftingRecipeTypeIds.Experimental, CraftingRecipeTypeIds.Custom);
    private static string NormalizeCraftingOutputType(string? value) => Allow(value, CraftingOutputTypeIds.InventoryItem, CraftingOutputTypeIds.InventoryItem, CraftingOutputTypeIds.EquipmentItem, CraftingOutputTypeIds.Material, CraftingOutputTypeIds.Component, CraftingOutputTypeIds.Ammo, CraftingOutputTypeIds.Document, CraftingOutputTypeIds.RecipeReference, CraftingOutputTypeIds.BlueprintReference, CraftingOutputTypeIds.ProjectResult, CraftingOutputTypeIds.Custom);
    private static string NormalizeCraftingIngredientType(string? value) => Allow(value, CraftingIngredientTypeIds.Custom, CraftingIngredientTypeIds.Item, CraftingIngredientTypeIds.Material, CraftingIngredientTypeIds.Component, CraftingIngredientTypeIds.Reagent, CraftingIngredientTypeIds.Fuel, CraftingIngredientTypeIds.Ammo, CraftingIngredientTypeIds.MagicCrystal, CraftingIngredientTypeIds.Document, CraftingIngredientTypeIds.Sample, CraftingIngredientTypeIds.Catalyst, CraftingIngredientTypeIds.Currency, CraftingIngredientTypeIds.Custom);
    private static string NormalizeCraftingProjectStatus(string? value) => Allow(value, CraftingProjectStatusIds.Draft, CraftingProjectStatusIds.Draft, CraftingProjectStatusIds.Submitted, CraftingProjectStatusIds.GmReview, CraftingProjectStatusIds.Approved, CraftingProjectStatusIds.WaitingResources, CraftingProjectStatusIds.Active, CraftingProjectStatusIds.AwaitingAcceptance, CraftingProjectStatusIds.Completed, CraftingProjectStatusIds.Failed, CraftingProjectStatusIds.Cancelled, CraftingProjectStatusIds.Archived);
    private static string NormalizeCraftingVisibility(string? value, bool allowEmpty = false)
    {
        var text = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(text)) return string.Empty;
        return Allow(text, ProjectVisibilityModeIds.PlayerVisible, ProjectVisibilityModeIds.GmOnly, ProjectVisibilityModeIds.PlayerVisible, ProjectVisibilityModeIds.Party, ProjectVisibilityModeIds.OwnerOnly, ProjectVisibilityModeIds.Hidden);
    }

    private static string SafeRecipeType(string recipeType)
        => string.Equals(recipeType, CraftingRecipeTypeIds.FactionSecret, StringComparison.OrdinalIgnoreCase) ? CraftingRecipeTypeIds.Discovered : recipeType;

    private static string MapCraftingStatusToProjectStatus(string status)
    {
        return status switch
        {
            CraftingProjectStatusIds.Submitted => ProjectStatusIds.Submitted,
            CraftingProjectStatusIds.GmReview => ProjectStatusIds.InReview,
            CraftingProjectStatusIds.Approved => ProjectStatusIds.Approved,
            CraftingProjectStatusIds.WaitingResources => ProjectStatusIds.WaitingResources,
            CraftingProjectStatusIds.Active => ProjectStatusIds.Active,
            CraftingProjectStatusIds.AwaitingAcceptance => ProjectStatusIds.AwaitingAcceptance,
            CraftingProjectStatusIds.Completed => ProjectStatusIds.Completed,
            CraftingProjectStatusIds.Failed => ProjectStatusIds.Failed,
            CraftingProjectStatusIds.Cancelled => ProjectStatusIds.Cancelled,
            CraftingProjectStatusIds.Archived => ProjectStatusIds.Archived,
            _ => ProjectStatusIds.Draft
        };
    }

    private static string Allow(string? value, string fallback, params string[] allowed)
    {
        var text = (value ?? string.Empty).Trim();
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : fallback;
    }

    private static List<string> PayloadStringList(IDictionary<string, object> payload, string key)
    {
        var list = PayloadReader.GetList(payload, key);
        if (list == null) return new List<string>();
        return list.Select(x => Convert.ToString(x) ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).Take(50).ToList();
    }

    private static Dictionary<string, object> ToDictionary(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value!;
            }
            return result;
        }
        return new Dictionary<string, object>();
    }

    private static Dictionary<string, object> SanitizeCraftingPayload(IDictionary<string, object> payload)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var pair in payload)
        {
            if (string.Equals(pair.Key, "gmNotes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "gmDescription", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "serverOnlyData", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "token", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "password", StringComparison.OrdinalIgnoreCase))
                continue;
            result[pair.Key] = pair.Value;
        }
        return result;
    }
}
