using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private static readonly object ProjectCraftRuntimeLock0191 = new();
    private const string CraftItemRuntimeKind0191 = "craft_item_0191";

    public ResponseEnvelope ProjectCraftRecipeList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectCraftViewEnabled0191(admin)) return ProjectCraftDisabled0191(context.Request.Command);

        var recipes = LoadCraftRecipes0191()
            .Where(x => admin || IsDefinitionPlayerVisible0191(x))
            .Select(x => (object)RecipeCard0191(x))
            .ToArray();
        return Ok("Craft recipes loaded.", new Dictionary<string, object> { ["items"] = recipes });
    }

    public ResponseEnvelope ProjectCraftPreview(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectCraftViewEnabled0191(admin)) return ProjectCraftDisabled0191(context.Request.Command);

        var recipe = RequireCraftRecipe0191(context.Request.Payload);
        if (!admin && !IsDefinitionPlayerVisible0191(recipe))
            throw new UnauthorizedAccessException("Recipe is not available to this player.");
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var ownership = RequireCraftCharacter0191(characterId, actor, admin);
        var snapshot = BuildCraftSnapshot0191(recipe, requirePlayerVisible: !admin);
        var evaluation = EvaluateCraftRequirements0191(snapshot, ownership);
        return Ok("Craft requirements evaluated.", new Dictionary<string, object>
        {
            ["preview"] = CraftPreviewPayload0191(snapshot, evaluation)
        });
    }

    public ResponseEnvelope ProjectCraftCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectCraftViewEnabled0191(admin)) return ProjectCraftDisabled0191(context.Request.Command);
        var operationId = RequireOperationId0191(context);

        lock (ProjectCraftRuntimeLock0191)
        {
            var existing = _repositories.Projects.Find(
                    Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedOperationId, operationId)
                    & Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedByUserId, actor.Id))
                .FirstOrDefault();
            if (existing != null)
                return Ok("Craft project already created.", ProjectCraftResponse0191(existing, admin, alreadyApplied: true));

            var recipe = RequireCraftRecipe0191(context.Request.Payload);
            if (!admin && !IsDefinitionPlayerVisible0191(recipe))
                throw new UnauthorizedAccessException("Recipe is not available to this player.");
            var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
            var ownership = RequireCraftCharacter0191(characterId, actor, admin);
            var snapshot = BuildCraftSnapshot0191(recipe, requirePlayerVisible: !admin);
            var evaluation = EvaluateCraftRequirements0191(snapshot, ownership);
            var project = new ProjectBaseState
            {
                CampaignId = FirstNonEmpty(ownership.CampaignId, PayloadReader.GetString(context.Request.Payload, "campaignId"), "default"),
                RuleSetId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "ruleSetId"), recipe.RuleSetId, RuleSetIds.FantasyNriDefault),
                ProjectType = ProjectTypeIds.Crafting,
                RuntimeKind = CraftItemRuntimeKind0191,
                Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), snapshot.RecipeName), 2, 180, "name"),
                PublicSummary = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicSummary"), snapshot.RecipePublicDescription, snapshot.RecipeName),
                Status = ProjectStatusIds.Draft,
                ApprovalStatus = ProjectApprovalStatusIds.Draft,
                ProgressMode = ProjectProgressModeIds.StageBased,
                ResultStatus = ProjectResultStatusIds.Expected,
                ResultApplicationMode = ProjectResultApplicationModeIds.CreateItemLater,
                OwnerUserId = ownership.OwnerUserId,
                OwnerDisplayName = FirstNonEmpty(ownership.OwnerDisplayName, actor.Login),
                OwnerCharacterId = ownership.CharacterId,
                CreatedByUserId = actor.Id,
                UpdatedByUserId = actor.Id,
                VisibilityMode = ProjectVisibilityModeIds.OwnerOnly,
                IsPlayerVisible = true,
                CreatedOperationId = operationId,
                LastOperationId = operationId,
                LastOperationCommand = CommandNames.ProjectCraftCreate,
                DefinitionSnapshot = snapshot,
                WorkPointsRequired = Math.Max(1, snapshot.Stages.Count),
                Revision = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _repositories.Projects.Insert(project);
            CreateCraftChildren0191(project, evaluation, actor.Id);
            AddCraftAudit0191(project, actor.Id, operationId, "project.created", "Создан проект изготовления.", "Проект изготовления создан.", true);
            TryPublishProjectSync(project, "craft.created", actor.Id, context.Request.RequestId ?? string.Empty);
            return Ok("Craft project created.", ProjectCraftResponse0191(project, admin));
        }
    }

    public ResponseEnvelope ProjectCraftSubmit(CommandContext context)
        => MutateCraftProject0191(context, adminOnly: false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status != ProjectStatusIds.Draft && project.Status != ProjectStatusIds.RequirementsReview)
                throw new InvalidOperationException("Only a draft project can be submitted.");
            project.Status = ProjectStatusIds.AwaitingApproval;
            project.ApprovalStatus = ProjectApprovalStatusIds.PendingGmReview;
            project.SubmittedAtUtc = DateTime.UtcNow;
            var existing = _repositories.ProjectApprovals.Find(Builders<ProjectApprovalState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
            if (existing == null)
            {
                _repositories.ProjectApprovals.Insert(new ProjectApprovalState
                {
                    ProjectId = project.Id,
                    CampaignId = project.CampaignId,
                    ApprovalType = "gm_project_start",
                    Status = ProjectApprovalStatusIds.PendingGmReview,
                    RequestedByUserId = actor.Id,
                    PublicSummary = "Проект ожидает решения GM.",
                    GMSummary = "Проверьте требования и подтвердите запуск.",
                    IsPlayerVisible = true
                });
            }
            AddCraftAudit0191(project, actor.Id, operationId, "project.submitted", "Проект отправлен на согласование.", "Проект отправлен GM.", true);
        }, "Craft project submitted.");

    public ResponseEnvelope ProjectCraftList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectCraftViewEnabled0191(admin)) return ProjectCraftDisabled0191(context.Request.Command);
        var filter = Builders<ProjectBaseState>.Filter.Eq(x => x.RuntimeKind, CraftItemRuntimeKind0191)
                     & Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false);
        if (!admin) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.OwnerUserId, actor.Id);
        var items = _repositories.Projects.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)CraftProjectPayload0191(x, admin, details: false))
            .ToArray();
        return Ok("Craft projects loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectCraftGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectCraftViewEnabled0191(admin)) return ProjectCraftDisabled0191(context.Request.Command);
        var project = RequireCraftProject0191(context.Request.Payload);
        RequireOwnerOrAdmin0191(project, actor, admin);
        return Ok("Craft project loaded.", ProjectCraftResponse0191(project, admin));
    }

    public ResponseEnvelope ProjectCraftRequirementConfirm(CommandContext context)
        => MutateCraftProject0191(context, adminOnly: true, (project, actor, _, operationId) =>
        {
            var requirementId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requirementId"), 1, 128, "requirementId");
            var requirement = _repositories.ProjectRequirements.GetById(requirementId)
                              ?? throw new KeyNotFoundException("Project requirement not found.");
            if (!string.Equals(requirement.ProjectId, project.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("Requirement belongs to another project.");
            requirement.Status = ProjectRequirementStatusIds.Satisfied;
            requirement.VerifiedByUserId = actor.Id;
            requirement.VerifiedAtUtc = DateTime.UtcNow;
            requirement.PublicNotes = FirstNonEmpty(
                PayloadReader.GetString(context.Request.Payload, "publicNote"),
                "Требование подтверждено GM.");
            requirement.GMNotes = RequireLength(PayloadReader.GetString(context.Request.Payload, "gmNote"), 0, 1024, "gmNote");
            _repositories.ProjectRequirements.Replace(requirement);
            AddCraftAudit0191(project, actor.Id, operationId, "requirement.confirmed", "Требование подтверждено: " + requirement.Name, requirement.PublicNotes, true);
        }, "Craft requirement confirmed.");

    public ResponseEnvelope ProjectCraftApprove(CommandContext context)
        => MutateCraftProject0191(context, adminOnly: true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Project is not awaiting approval.");
            var open = RequiredOpenRequirements0191(project.Id).Where(x => !IsApprovalRequirement0191(x)).ToArray();
            if (open.Length > 0)
                throw new InvalidOperationException("Required conditions are not satisfied: " + string.Join(", ", open.Select(x => x.Name)));
            project.Status = ProjectStatusIds.Approved;
            project.ApprovalStatus = ProjectApprovalStatusIds.Approved;
            project.ApprovedAtUtc = DateTime.UtcNow;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Approved, "Проект одобрен.");
            AddCraftAudit0191(project, actor.Id, operationId, "project.approved", "Проект одобрен GM.", "GM одобрил проект.", true);
        }, "Craft project approved.");

    public ResponseEnvelope ProjectCraftReject(CommandContext context)
        => MutateCraftProject0191(context, adminOnly: true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Project is not awaiting approval.");
            project.Status = ProjectStatusIds.Failed;
            project.ApprovalStatus = ProjectApprovalStatusIds.Rejected;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            var publicReason = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicReason"), "Проект отклонён GM."), 1, 512, "publicReason");
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Rejected, publicReason);
            AddCraftAudit0191(project, actor.Id, operationId, "project.rejected", publicReason, publicReason, true);
        }, "Craft project rejected.");

    public ResponseEnvelope ProjectCraftReserve(CommandContext context)
        => MutateCraftProject0191(context, adminOnly: true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.Approved)
                throw new InvalidOperationException("Only an approved project can reserve resources.");
            ReserveCraftResources0191(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.ResourcesReserved;
            AddCraftAudit0191(project, actor.Id, operationId, "resources.reserved", "Ресурсы зарезервированы.", "Ресурсы проекта зарезервированы.", true);
        }, "Craft resources reserved.");

    public ResponseEnvelope ProjectCraftStart(CommandContext context)
        => MutateCraftProject0191(context, adminOnly: true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.ResourcesReserved)
                throw new InvalidOperationException("Resources must be reserved before project start.");
            var stages = LoadCraftStages0191(project.Id);
            if (stages.Count == 0) throw new InvalidOperationException("Project has no stages.");
            var first = stages[0];
            first.Status = ProjectStageStatusIds.Active;
            first.StartedAtUtc = DateTime.UtcNow;
            first.UpdatedAtUtc = DateTime.UtcNow;
            first.UpdatedByUserId = actor.Id;
            _repositories.ProjectStages.Replace(first);
            project.Status = ProjectStatusIds.InProgress;
            project.StartedAtUtc = DateTime.UtcNow;
            project.CurrentStageId = first.Id;
            project.CurrentStageName = first.Name;
            AddCraftAudit0191(project, actor.Id, operationId, "project.started", "Проект запущен.", "Работа над проектом началась.", true);
        }, "Craft project started.");

    public ResponseEnvelope ProjectCraftStageComplete(CommandContext context)
        => MutateCraftProject0191(context, adminOnly: true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Project is not in progress.");
            var stages = LoadCraftStages0191(project.Id);
            var current = stages.FirstOrDefault(x => x.Id == project.CurrentStageId)
                          ?? stages.FirstOrDefault(x => x.Status == ProjectStageStatusIds.Active)
                          ?? throw new InvalidOperationException("Current stage is not available.");
            current.Status = ProjectStageStatusIds.Completed;
            current.ProgressPercent = 100;
            current.CompletedAtUtc = DateTime.UtcNow;
            current.UpdatedAtUtc = DateTime.UtcNow;
            current.UpdatedByUserId = actor.Id;
            _repositories.ProjectStages.Replace(current);
            var next = stages.FirstOrDefault(x => x.SortOrder > current.SortOrder && x.Status != ProjectStageStatusIds.Completed);
            if (next != null)
            {
                next.Status = ProjectStageStatusIds.Active;
                next.StartedAtUtc = DateTime.UtcNow;
                next.UpdatedAtUtc = DateTime.UtcNow;
                next.UpdatedByUserId = actor.Id;
                _repositories.ProjectStages.Replace(next);
                project.CurrentStageId = next.Id;
                project.CurrentStageName = next.Name;
            }
            else
            {
                project.CurrentStageId = string.Empty;
                project.CurrentStageName = "Все стадии выполнены";
            }
            project.WorkPointsDone = stages.Count(x => x.Status == ProjectStageStatusIds.Completed);
            project.ProgressPercent = (int)Math.Round(100d * project.WorkPointsDone / Math.Max(1, stages.Count));
            AddCraftAudit0191(project, actor.Id, operationId, "stage.completed", "Завершена стадия: " + current.Name, "Завершена стадия «" + current.Name + "».", true);
        }, "Craft stage completed.");

    public ResponseEnvelope ProjectCraftComplete(CommandContext context)
        => MutateCraftProject0191(context, adminOnly: true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.Completed)
                return;
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Project is not in progress.");
            if (LoadCraftStages0191(project.Id).Any(x => x.Status != ProjectStageStatusIds.Completed))
                throw new InvalidOperationException("All project stages must be completed first.");
            CompleteCraftProject0191(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.Completed;
            project.ResultStatus = ProjectResultStatusIds.Applied;
            project.ResultApplicationMode = ProjectResultApplicationModeIds.CreateItemLater;
            project.ProgressPercent = 100;
            project.CompletedAtUtc = DateTime.UtcNow;
            AddCraftAudit0191(project, actor.Id, operationId, "project.completed", "Проект завершён, предмет создан.", "Проект завершён. Результат добавлен в инвентарь.", true);
        }, "Craft project completed.");

    public ResponseEnvelope ProjectCraftCancel(CommandContext context)
        => MutateCraftProject0191(context, adminOnly: false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status is ProjectStatusIds.InProgress or ProjectStatusIds.Completed or ProjectStatusIds.Failed or ProjectStatusIds.Cancelled)
                throw new InvalidOperationException("Project cannot be cancelled in its current state.");
            ReleaseCraftReservations0191(project.Id, actor.Id, "project cancelled");
            project.Status = ProjectStatusIds.Cancelled;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            AddCraftAudit0191(project, actor.Id, operationId, "project.cancelled", "Проект отменён.", "Проект отменён, резерв освобождён.", true);
        }, "Craft project cancelled.");

    public ResponseEnvelope ProjectCraftFail(CommandContext context)
        => MutateCraftProject0191(context, adminOnly: true, (project, actor, _, operationId) =>
        {
            if (project.Status is ProjectStatusIds.Completed or ProjectStatusIds.Cancelled)
                throw new InvalidOperationException("Completed or cancelled project cannot fail.");
            ReleaseCraftReservations0191(project.Id, actor.Id, "project failed");
            project.Status = ProjectStatusIds.Failed;
            project.ResultStatus = ProjectResultStatusIds.Failed;
            AddCraftAudit0191(project, actor.Id, operationId, "project.failed",
                FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "gmReason"), "Проект завершён неудачей."),
                FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicReason"), "Проект завершён неудачей."),
                true);
        }, "Craft project failed.");

    public ResponseEnvelope ProjectCraftAudit(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectCraftAdminEnabled0191()) return ProjectCraftDisabled0191(context.Request.Command);
        var project = RequireCraftProject0191(context.Request.Payload);
        var items = _repositories.ProjectAuditEntries.Find(Builders<ProjectAuditEntryState>.Filter.Eq(x => x.ProjectId, project.Id))
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["action"] = x.ActionType,
                ["summary"] = x.Summary,
                ["publicSummary"] = x.PublicSummary,
                ["actorDisplayName"] = AccountDisplayName0191(x.ActorUserId),
                ["createdAtUtc"] = x.CreatedAtUtc
            }).ToArray();
        return Ok("Craft project audit loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    private ResponseEnvelope MutateCraftProject0191(
        CommandContext context,
        bool adminOnly,
        Action<ProjectBaseState, UserAccount, bool, string> mutation,
        string successMessage)
    {
        var actor = adminOnly ? RequireAdmin(context) : GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (adminOnly && !admin) throw new UnauthorizedAccessException("Admin role is required.");
        if (!ProjectCraftViewEnabled0191(admin)) return ProjectCraftDisabled0191(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (ProjectCraftRuntimeLock0191)
        {
            var project = RequireCraftProject0191(context.Request.Payload);
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (string.Equals(project.LastOperationId, operationId, StringComparison.Ordinal))
            {
                if (!string.Equals(project.LastOperationCommand, context.Request.Command, StringComparison.Ordinal))
                    throw new InvalidOperationException("OperationId was already used for another command.");
                return Ok(successMessage, ProjectCraftResponse0191(project, admin, alreadyApplied: true));
            }
            var expected = PayloadReader.GetInt(context.Request.Payload, "expectedRevision")
                           ?? throw new ArgumentException("expectedRevision is required.");
            if (expected != project.Revision)
                throw new InvalidOperationException($"Project revision conflict. Reload project. current={project.Revision}; expected={expected}");
            mutation(project, actor, admin, operationId);
            SaveCraftProject0191(project, actor.Id, operationId, context.Request.Command, expected);
            TryPublishProjectSync(project, context.Request.Command, actor.Id, context.Request.RequestId ?? string.Empty);
            if (project.Status == ProjectStatusIds.Completed)
                TryWriteProjectJournal(project, operationId, "Завершён проект: " + project.Name, actor.Id);
            return Ok(successMessage, ProjectCraftResponse0191(project, admin));
        }
    }

    private void SaveCraftProject0191(ProjectBaseState project, string actorId, string operationId, string command, int expectedRevision)
    {
        project.UpdatedAtUtc = DateTime.UtcNow;
        project.UpdatedByUserId = actorId;
        project.LastOperationId = operationId;
        project.LastOperationCommand = command;
        project.Revision = expectedRevision + 1;
        var result = _mongo.Projects.ReplaceOne(
            Builders<ProjectBaseState>.Filter.Eq(x => x.Id, project.Id)
            & Builders<ProjectBaseState>.Filter.Eq(x => x.Revision, expectedRevision),
            project);
        if (result.MatchedCount != 1)
        {
            project.Revision = expectedRevision;
            throw new InvalidOperationException("Project was changed by another operation. Reload and retry.");
        }
    }

    private void CreateCraftChildren0191(ProjectBaseState project, CraftRequirementEvaluation0191 evaluation, string actorId)
    {
        var snapshot = project.DefinitionSnapshot ?? throw new InvalidOperationException("Project snapshot is missing.");
        foreach (var stage in snapshot.Stages.OrderBy(x => x.Order))
        {
            _repositories.ProjectStages.Insert(new ProjectStageState
            {
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                StageType = ProjectStageTypeIds.Crafting,
                Name = stage.DisplayName,
                PublicSummary = stage.PublicSummary,
                Status = ProjectStageStatusIds.Locked,
                SortOrder = stage.Order * 10,
                IsPlayerVisible = stage.IsPlayerVisible,
                VisibilityMode = stage.IsPlayerVisible ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly,
                UpdatedByUserId = actorId,
                ExtraData = new Dictionary<string, object> { ["stageKey"] = stage.Key }
            });
        }
        foreach (var item in evaluation.Requirements)
        {
            _repositories.ProjectRequirements.Insert(new ProjectRequirementState
            {
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                RequirementType = item.Kind,
                Name = item.Name,
                PublicSummary = item.PublicSummary,
                GMSummary = item.GMSummary,
                Status = item.Satisfied ? ProjectRequirementStatusIds.Satisfied : ProjectRequirementStatusIds.Open,
                IsRequired = item.Required,
                IsPlayerVisible = item.PlayerVisible,
                VisibilityMode = item.PlayerVisible ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly,
                VerifiedByUserId = item.Satisfied ? "server" : string.Empty,
                VerifiedAtUtc = item.Satisfied ? DateTime.UtcNow : null,
                ExtraData = new Dictionary<string, object>
                {
                    ["manualGmConfirmation"] = item.ManualGmConfirmation,
                    ["definitionId"] = item.DefinitionId
                }
            });
        }
        foreach (var input in snapshot.Inputs.Where(x => !x.Optional))
        {
            _repositories.ProjectResourceRequirements.Insert(new ProjectResourceRequirementState
            {
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                ResourceType = "recipe_input",
                ResourceId = input.DefinitionId,
                DisplayName = input.DisplayName,
                QuantityRequired = input.Quantity,
                Unit = input.Unit,
                Status = ProjectResourceRequirementStatusIds.Needed,
                IsReservationOnly = true,
                IsPlayerVisible = true,
                VisibilityMode = ProjectVisibilityModeIds.PlayerVisible,
                UpdatedByUserId = actorId,
                ExtraData = new Dictionary<string, object>
                {
                    ["minimumQuality"] = input.MinimumQuality,
                    ["stableKey"] = input.StableKey
                }
            });
        }
    }

    private CraftRequirementEvaluation0191 EvaluateCraftRequirements0191(ProjectDefinitionSnapshot0191 snapshot, CharacterOwnershipState ownership)
    {
        var result = new CraftRequirementEvaluation0191();
        result.Requirements.Add(CraftRequirement0191("ownership", "Активный персонаж",
            ownership.IsActive && !ownership.IsArchived,
            "Персонаж активен и доступен владельцу.",
            "Персонаж неактивен или находится в архиве.", required: true));
        result.Requirements.Add(CraftRequirement0191("recipe", "Доступный рецепт", true,
            "Рецепт опубликован и доступен.", string.Empty, required: true));
        result.Requirements.Add(CraftRequirement0191("method", "Метод производства",
            !string.IsNullOrWhiteSpace(snapshot.MethodDefinitionId),
            "Метод производства выбран.", "Для рецепта не найден метод производства.", required: true));

        foreach (var requirement in snapshot.Requirements)
        {
            var manual = !string.Equals(requirement.Kind, "resource", StringComparison.OrdinalIgnoreCase);
            result.Requirements.Add(new CraftRequirementLine0191
            {
                Kind = requirement.Kind,
                DefinitionId = requirement.DefinitionId,
                Name = requirement.DisplayName,
                PublicSummary = manual ? "Требуется подтверждение GM." : requirement.PublicExplanation,
                GMSummary = requirement.PublicExplanation,
                Required = requirement.Required,
                Satisfied = !requirement.Required,
                ManualGmConfirmation = manual,
                PlayerVisible = true
            });
        }

        foreach (var input in snapshot.Inputs.Where(x => !x.Optional))
        {
            var availability = FindAvailableInventoryItem0191(
                ownership.CharacterId,
                input.DefinitionId,
                input.Quantity,
                input.MinimumQuality);
            result.Resources.Add(new Dictionary<string, object>
            {
                ["name"] = input.DisplayName,
                ["quantity"] = input.Quantity,
                ["unit"] = input.Unit,
                ["minimumQuality"] = input.MinimumQuality,
                ["status"] = availability == null ? "missing" : "available",
                ["statusLabel"] = availability == null ? "Не хватает" : "Будет зарезервировано"
            });
        }
        result.CanSubmit = result.Requirements.Where(x => x.Required && !x.ManualGmConfirmation).All(x => x.Satisfied);
        return result;
    }

    private ProjectDefinitionSnapshot0191 BuildCraftSnapshot0191(
        ContentDefinitionRecord recipe,
        bool requirePlayerVisible)
    {
        var methodId = SplitDefinitionRefs0191(ContentField0191(recipe, "methods")).FirstOrDefault() ?? string.Empty;
        var method = string.IsNullOrWhiteSpace(methodId) ? null : FindContentDefinition0191(methodId, TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod);
        if (method == null || method.IsArchived)
            throw new InvalidOperationException("Для рецепта не найден доступный метод производства.");
        var template = LoadProjectTemplates0191()
            .FirstOrDefault(x => string.Equals(ContentField0191(x, "projectType"), "CraftItem", StringComparison.OrdinalIgnoreCase)
                                 && SplitDefinitionRefs0191(ContentField0191(x, "recipes")).Contains(recipe.Id, StringComparer.OrdinalIgnoreCase))
                       ?? throw new KeyNotFoundException("CraftItem project template for recipe was not found.");
        if (requirePlayerVisible && (!IsDefinitionPlayerVisible0191(method) || !IsDefinitionPlayerVisible0191(template)))
            throw new UnauthorizedAccessException("Recipe runtime is not available to this player.");
        var inputs = ParseMaterialRows0191(ContentField0191(recipe, "inputRows"));
        var outputs = ParseMaterialRows0191(ContentField0191(recipe, "outputRows"));
        if (inputs.Count == 0 || outputs.Count != 1)
            throw new InvalidOperationException("CraftItem recipe must have inputs and exactly one output row.");
        var snapshot = new ProjectDefinitionSnapshot0191
        {
            RecipeDefinitionId = recipe.Id,
            RecipeStableKey = recipe.StableKey,
            RecipeVersion = FirstNonEmpty(recipe.RecordVersion, recipe.DefinitionPackVersion),
            RecipeRevision = recipe.Revision,
            RecipeName = FirstNonEmpty(recipe.DisplayName, recipe.Name),
            RecipePublicDescription = recipe.PublicDescription,
            MethodDefinitionId = method?.Id ?? string.Empty,
            MethodStableKey = method?.StableKey ?? string.Empty,
            MethodVersion = FirstNonEmpty(method?.RecordVersion, method?.DefinitionPackVersion),
            MethodRevision = method?.Revision ?? 0,
            MethodName = FirstNonEmpty(method?.DisplayName, method?.Name, "Метод не выбран"),
            ProjectTemplateDefinitionId = template.Id,
            ProjectTemplateStableKey = template.StableKey,
            ProjectTemplateVersion = FirstNonEmpty(template.RecordVersion, template.DefinitionPackVersion),
            ProjectTemplateRevision = template.Revision,
            ProjectTemplateName = FirstNonEmpty(template.DisplayName, template.Name),
            ApprovalPolicy = ContentField0191(template, "approvalPolicy"),
            ResourceReservationPolicy = ContentField0191(template, "resourceReservationPolicy"),
            CancellationRefundPolicy = ContentField0191(template, "cancellationRefundPolicy"),
            EstimatedDurationMinutes = Math.Max(0, ParseInt0191(ContentField0191(recipe, "estimatedDurationMinutes"))),
            Inputs = inputs,
            Outputs = outputs,
            Stages = ParseStageRows0191(ContentField0191(template, "stageRows")),
            Requirements = ParseTemplateRequirements0191(template, recipe, method)
        };
        if (snapshot.Stages.Count == 0) throw new InvalidOperationException("Project template has no stages.");
        foreach (var material in snapshot.Inputs.Concat(snapshot.Outputs))
        {
            if (!IsDefinitionAvailable0191(material.DefinitionId, requirePlayerVisible))
                throw new InvalidOperationException("Recipe references an unavailable material: " + material.DisplayName);
        }
        foreach (var requirement in snapshot.Requirements.Where(x => !string.IsNullOrWhiteSpace(x.DefinitionId)))
        {
            if (!IsDefinitionAvailable0191(requirement.DefinitionId, requirePlayerVisible))
                throw new InvalidOperationException("Project references an unavailable requirement: " + requirement.DisplayName);
        }
        snapshot.SnapshotChecksum = ComputeSnapshotChecksum0191(snapshot);
        return snapshot;
    }

    private List<ProjectRequirementSnapshot0191> ParseTemplateRequirements0191(
        ContentDefinitionRecord template,
        ContentDefinitionRecord recipe,
        ContentDefinitionRecord? method)
    {
        var result = new List<ProjectRequirementSnapshot0191>();
        foreach (var row in ParseRows0191(ContentField0191(template, "requirementRows")))
        {
            if (row.Length == 0) continue;
            result.Add(new ProjectRequirementSnapshot0191
            {
                Kind = Cell0191(row, 0, "custom_manual"),
                DefinitionId = Cell0191(row, 1),
                DisplayName = DefinitionDisplayName0191(Cell0191(row, 1), Cell0191(row, 0, "Требование")),
                Quantity = ParseDecimal0191(Cell0191(row, 2)),
                MinimumQualityOrRank = Cell0191(row, 3),
                Required = ParseBool0191(Cell0191(row, 4), true),
                ConsumptionMode = Cell0191(row, 5),
                PublicExplanation = Cell0191(row, 6, "Требуется подтверждение.")
            });
        }
        AddReferenceRequirements0191(result, "technology", "Технология", ContentField0191(recipe, "technologies"), true);
        AddReferenceRequirements0191(result, "skill", "Навык", ContentField0191(recipe, "requiredSkills"), true);
        AddReferenceRequirements0191(result, "facility", "Площадка", ContentField0191(recipe, "requiredFacilities"), true);
        AddReferenceRequirements0191(result, "license", "Лицензия", ContentField0191(recipe, "requiredLicenses"), true);
        if (method != null)
        {
            AddReferenceRequirements0191(result, "technology", "Технология", ContentField0191(method, "technologies"), true);
            AddReferenceRequirements0191(result, "skill", "Навык", ContentField0191(method, "requiredSkills"), true);
            AddReferenceRequirements0191(result, "facility", "Площадка", ContentField0191(method, "requiredFacilities"), true);
            AddReferenceRequirements0191(result, "tool", "Инструмент", ContentField0191(method, "requiredTools"), true);
            AddReferenceRequirements0191(result, "license", "Лицензия", ContentField0191(method, "requiredLicenses"), true);
        }
        return result
            .GroupBy(x => x.Kind + "|" + x.DefinitionId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private void AddReferenceRequirements0191(
        ICollection<ProjectRequirementSnapshot0191> target,
        string kind,
        string prefix,
        string values,
        bool required)
    {
        foreach (var id in SplitDefinitionRefs0191(values))
        {
            target.Add(new ProjectRequirementSnapshot0191
            {
                Kind = kind,
                DefinitionId = id,
                DisplayName = prefix + ": " + DefinitionDisplayName0191(id, "неизвестное определение"),
                Required = required,
                PublicExplanation = "Требуется подтверждение GM."
            });
        }
    }

    private void ReserveCraftResources0191(ProjectBaseState project, string actorId, string operationId)
    {
        var existing = ActiveCraftReservations0191(project.Id).ToList();
        if (existing.Count > 0)
        {
            if (existing.All(x => string.Equals(x.OperationId, operationId, StringComparison.Ordinal))) return;
            throw new InvalidOperationException("Project resources are already reserved.");
        }
        var requirements = _repositories.ProjectResourceRequirements.Find(
            Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id)).ToList();
        var prepared = new List<(ProjectResourceRequirementState Requirement, CharacterInventoryItemProfileValue Item, int Units)>();
        foreach (var requirement in requirements)
        {
            var minimumQuality = requirement.ExtraData.TryGetValue("minimumQuality", out var quality)
                ? Convert.ToString(quality, CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
            var item = FindAvailableInventoryItem0191(
                           project.OwnerCharacterId,
                           requirement.ResourceId,
                           requirement.QuantityRequired,
                           minimumQuality)
                       ?? throw new InvalidOperationException("Not enough resource: " + requirement.DisplayName);
            var units = InventoryUnitsForRequirement0191(requirement.QuantityRequired);
            prepared.Add((requirement, item, units));
        }
        var written = new List<(CraftingResourceReservationState Reservation, ProjectResourceRequirementState Requirement)>();
        try
        {
            foreach (var entry in prepared)
            {
                var reservation = new CraftingResourceReservationState
                {
                    CampaignId = project.CampaignId,
                    ProjectId = project.Id,
                    CraftingProjectId = project.Id,
                    RequirementId = entry.Requirement.Id,
                    CharacterId = project.OwnerCharacterId,
                    ItemInstanceId = entry.Item.ItemId,
                    DefinitionId = entry.Requirement.ResourceId,
                    DisplayName = entry.Requirement.DisplayName,
                    QuantityReserved = entry.Requirement.QuantityRequired,
                    Unit = entry.Requirement.Unit,
                    Status = CraftingReservationStatusIds.Reserved,
                    IsConsumedOnCompletion = true,
                    IsPlayerVisible = true,
                    ReservedByUserId = actorId,
                    UpdatedByUserId = actorId,
                    OperationId = operationId,
                    ProjectRevision = project.Revision,
                    ExtraData = new Dictionary<string, object> { ["inventoryUnitsReserved"] = entry.Units }
                };
                _repositories.CraftingReservations.Insert(reservation);
                entry.Requirement.QuantityReserved = entry.Requirement.QuantityRequired;
                entry.Requirement.Status = ProjectResourceRequirementStatusIds.Reserved;
                entry.Requirement.UpdatedByUserId = actorId;
                entry.Requirement.UpdatedAtUtc = DateTime.UtcNow;
                _repositories.ProjectResourceRequirements.Replace(entry.Requirement);
                written.Add((reservation, entry.Requirement));
            }
        }
        catch
        {
            foreach (var entry in written)
            {
                entry.Reservation.Status = CraftingReservationStatusIds.Released;
                entry.Reservation.ReleasedAtUtc = DateTime.UtcNow;
                entry.Reservation.PublicNotes = "Резерв освобождён после ошибки операции.";
                _repositories.CraftingReservations.Replace(entry.Reservation);
                entry.Requirement.QuantityReserved = 0;
                entry.Requirement.Status = ProjectResourceRequirementStatusIds.Needed;
                entry.Requirement.UpdatedAtUtc = DateTime.UtcNow;
                _repositories.ProjectResourceRequirements.Replace(entry.Requirement);
            }
            throw;
        }
    }

    private void CompleteCraftProject0191(ProjectBaseState project, string actorId, string operationId)
    {
        var snapshot = project.DefinitionSnapshot ?? throw new InvalidOperationException("Project snapshot is missing.");
        var output = snapshot.Outputs.Single();
        var outputItemId = "craft0191_" + project.Id;
        var existingResult = _repositories.CraftingResults.Find(
            Builders<CraftingProjectItemResult>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        var document = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, project.OwnerCharacterId))
            .FirstOrDefault() ?? throw new KeyNotFoundException("Character inventory profile not found.");
        document.Profile ??= new InventoryProfile { CharacterId = project.OwnerCharacterId, RuleSetId = project.RuleSetId };
        document.Profile.Items ??= new List<CharacterInventoryItemProfileValue>();
        var outputAlreadyExists = document.Profile.Items.Any(x => string.Equals(x.ItemId, outputItemId, StringComparison.Ordinal));
        if (!outputAlreadyExists)
        {
            var reservations = ActiveCraftReservations0191(project.Id).ToList();
            if (reservations.Count == 0) throw new InvalidOperationException("Active reservations were not found.");
            foreach (var group in reservations.GroupBy(x => x.ItemInstanceId, StringComparer.OrdinalIgnoreCase))
            {
                var item = document.Profile.Items.FirstOrDefault(x => string.Equals(x.ItemId, group.Key, StringComparison.OrdinalIgnoreCase))
                           ?? throw new KeyNotFoundException("Reserved inventory item not found.");
                var units = group.Sum(ReservationInventoryUnits0191);
                if (item.Quantity < units) throw new InvalidOperationException("Reserved inventory quantity is no longer available.");
                item.Quantity -= units;
                item.UpdatedAtUtc = DateTime.UtcNow;
                item.Source = "project_craft_consumption_0191";
                if (item.Quantity <= 0) document.Profile.Items.Remove(item);
            }
            var outputDefinition = FindAnyDefinition0191(output.DefinitionId);
            document.Profile.Items.Add(new CharacterInventoryItemProfileValue
            {
                ItemId = outputItemId,
                DefinitionId = output.DefinitionId,
                ItemDefinitionId = output.DefinitionId,
                DefinitionCategory = outputDefinition.Category,
                SnapshotDisplayName = output.DisplayName,
                SnapshotCategory = outputDefinition.Category,
                SnapshotDescription = outputDefinition.Description,
                SnapshotTags = outputDefinition.Tags,
                Name = output.DisplayName,
                DisplayName = output.DisplayName,
                Category = outputDefinition.Category,
                Description = outputDefinition.Description,
                Quantity = Math.Max(1, (int)Math.Ceiling(output.Quantity)),
                Durability = 100,
                MaxDurability = 100,
                Condition = "Новый",
                IsPlayerVisible = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Source = "project_craft_result_0191",
                Tags = new List<string> { "crafted", "project:" + project.Id }
            });
            var originalUpdated = document.UpdatedUtc;
            document.UpdatedUtc = DateTime.UtcNow;
            var inventoryWrite = _mongo.CharacterInventoryProfiles.ReplaceOne(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, document.Id)
                & Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.UpdatedUtc, originalUpdated),
                document);
            if (inventoryWrite.MatchedCount != 1)
                throw new InvalidOperationException("Inventory changed while completing project. Reload and retry.");
        }

        foreach (var reservation in ActiveCraftReservations0191(project.Id))
        {
            reservation.Status = CraftingReservationStatusIds.Consumed;
            reservation.QuantityConsumed = reservation.QuantityReserved;
            reservation.ConsumedAtUtc = DateTime.UtcNow;
            reservation.UpdatedByUserId = actorId;
            _repositories.CraftingReservations.Replace(reservation);
            var requirement = _repositories.ProjectResourceRequirements.GetById(reservation.RequirementId);
            if (requirement != null)
            {
                requirement.QuantityProvided = requirement.QuantityRequired;
                requirement.Status = ProjectResourceRequirementStatusIds.ConsumedManually;
                requirement.UpdatedByUserId = actorId;
                requirement.UpdatedAtUtc = DateTime.UtcNow;
                _repositories.ProjectResourceRequirements.Replace(requirement);
            }
        }
        var result = existingResult ?? new CraftingProjectItemResult
        {
            Id = "craft_result_0191_" + project.Id,
            CampaignId = project.CampaignId,
            CraftingProjectId = project.Id,
            ProjectId = project.Id,
            TargetCharacterId = project.OwnerCharacterId,
            ResultType = CraftingOutputTypeIds.InventoryItem,
            DefinitionId = output.DefinitionId,
            DisplayName = output.DisplayName,
            Quantity = Math.Max(1, (int)Math.Ceiling(output.Quantity)),
            QualitySummary = output.MinimumQuality,
            IsPlayerVisible = true,
            PreparedByUserId = actorId
        };
        result.Status = CraftingResultStatusIds.Created;
        result.CreatedItemInstanceId = outputItemId;
        result.AcceptedAtUtc ??= DateTime.UtcNow;
        result.CreatedAtInventoryUtc ??= DateTime.UtcNow;
        result.AcceptedByUserId = actorId;
        result.CompletionOperationId = FirstNonEmpty(result.CompletionOperationId, operationId);
        if (existingResult == null) _repositories.CraftingResults.Insert(result); else _repositories.CraftingResults.Replace(result);
    }

    private void ReleaseCraftReservations0191(string projectId, string actorId, string reason)
    {
        foreach (var reservation in ActiveCraftReservations0191(projectId))
        {
            reservation.Status = CraftingReservationStatusIds.Released;
            reservation.ReleasedAtUtc = DateTime.UtcNow;
            reservation.UpdatedByUserId = actorId;
            reservation.PublicNotes = "Резерв освобождён: " + reason;
            _repositories.CraftingReservations.Replace(reservation);
            var requirement = _repositories.ProjectResourceRequirements.GetById(reservation.RequirementId);
            if (requirement == null) continue;
            requirement.QuantityReserved = 0;
            requirement.Status = ProjectResourceRequirementStatusIds.Needed;
            requirement.UpdatedByUserId = actorId;
            requirement.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ProjectResourceRequirements.Replace(requirement);
        }
    }

    private CharacterInventoryItemProfileValue? FindAvailableInventoryItem0191(
        string characterId,
        string definitionId,
        decimal requiredQuantity,
        string minimumQuality = "")
    {
        var document = _mongo.CharacterInventoryProfiles.Find(
            Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        var requiredUnits = InventoryUnitsForRequirement0191(requiredQuantity);
        return (document?.Profile?.Items ?? new List<CharacterInventoryItemProfileValue>())
            .Where(x => string.Equals(x.DefinitionId, definitionId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.ItemDefinitionId, definitionId, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(x =>
                x.Quantity - ReservedInventoryUnits0191(characterId, x.ItemId) >= requiredUnits
                && InventoryQualityMeets0191(x, minimumQuality));
    }

    private static bool InventoryQualityMeets0191(CharacterInventoryItemProfileValue item, string minimumQuality)
    {
        if (string.IsNullOrWhiteSpace(minimumQuality)) return true;
        var actual = item.Tags
                         .FirstOrDefault(x => x.StartsWith("quality:", StringComparison.OrdinalIgnoreCase))
                         ?.Substring("quality:".Length)
                     ?? item.Condition;
        return QualityRank0191(actual) >= QualityRank0191(minimumQuality);
    }

    private static int QualityRank0191(string value)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "poor":
            case "низкое":
            case "плохое":
                return 0;
            case "standard":
            case "обычное":
            case "стандарт":
            case "стандартное":
                return 1;
            case "good":
            case "хорошее":
                return 2;
            case "excellent":
            case "превосходное":
                return 3;
            case "masterwork":
            case "шедевральное":
                return 4;
            default:
                return 1;
        }
    }

    private int ReservedInventoryUnits0191(string characterId, string itemId)
        => _repositories.CraftingReservations.Find(
                Builders<CraftingResourceReservationState>.Filter.Eq(x => x.CharacterId, characterId)
                & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.ItemInstanceId, itemId)
                & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.Status, CraftingReservationStatusIds.Reserved))
            .Sum(ReservationInventoryUnits0191);

    private static int InventoryUnitsForRequirement0191(decimal quantity)
        => Math.Max(1, (int)Math.Ceiling(quantity));

    private static int ReservationInventoryUnits0191(CraftingResourceReservationState reservation)
    {
        if (reservation.ExtraData.TryGetValue("inventoryUnitsReserved", out var value)
            && int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return Math.Max(1, parsed);
        return InventoryUnitsForRequirement0191(reservation.QuantityReserved);
    }

    private IEnumerable<CraftingResourceReservationState> ActiveCraftReservations0191(string projectId)
        => _repositories.CraftingReservations.Find(
            Builders<CraftingResourceReservationState>.Filter.Eq(x => x.ProjectId, projectId)
            & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.Status, CraftingReservationStatusIds.Reserved));

    private List<ProjectStageState> LoadCraftStages0191(string projectId)
        => _repositories.ProjectStages.Find(Builders<ProjectStageState>.Filter.Eq(x => x.ProjectId, projectId))
            .OrderBy(x => x.SortOrder)
            .ToList();

    private IEnumerable<ProjectRequirementState> RequiredOpenRequirements0191(string projectId)
        => _repositories.ProjectRequirements.Find(Builders<ProjectRequirementState>.Filter.Eq(x => x.ProjectId, projectId))
            .Where(x => x.IsRequired && x.Status != ProjectRequirementStatusIds.Satisfied && x.Status != ProjectRequirementStatusIds.Waived);

    private static bool IsApprovalRequirement0191(ProjectRequirementState requirement)
        => string.Equals(requirement.RequirementType, "gm_approval", StringComparison.OrdinalIgnoreCase);

    private void ResolveApproval0191(string projectId, string actorId, string status, string summary)
    {
        var approval = _repositories.ProjectApprovals.Find(Builders<ProjectApprovalState>.Filter.Eq(x => x.ProjectId, projectId))
            .OrderByDescending(x => x.RequestedAtUtc).FirstOrDefault();
        if (approval == null) return;
        approval.Status = status;
        approval.ReviewedByUserId = actorId;
        approval.ReviewedAtUtc = DateTime.UtcNow;
        approval.PublicSummary = summary;
        _repositories.ProjectApprovals.Replace(approval);
    }

    private Dictionary<string, object> ProjectCraftResponse0191(ProjectBaseState project, bool admin, bool alreadyApplied = false)
        => new()
        {
            ["item"] = CraftProjectPayload0191(project, admin, details: true),
            ["alreadyApplied"] = alreadyApplied
        };

    private Dictionary<string, object> CraftProjectPayload0191(ProjectBaseState project, bool admin, bool details)
    {
        var snapshot = project.DefinitionSnapshot ?? new ProjectDefinitionSnapshot0191();
        var payload = new Dictionary<string, object>
        {
            ["projectId"] = project.Id,
            ["name"] = project.Name,
            ["publicSummary"] = project.PublicSummary,
            ["status"] = project.Status,
            ["statusLabel"] = CraftProjectStatusLabel0191(project.Status),
            ["approvalStatus"] = project.ApprovalStatus,
            ["revision"] = project.Revision,
            ["progressPercent"] = project.ProgressPercent,
            ["currentStageName"] = project.CurrentStageName,
            ["ownerDisplayName"] = project.OwnerDisplayName,
            ["recipeName"] = snapshot.RecipeName,
            ["recipeDescription"] = snapshot.RecipePublicDescription,
            ["methodName"] = snapshot.MethodName,
            ["templateName"] = snapshot.ProjectTemplateName,
            ["estimatedDurationMinutes"] = snapshot.EstimatedDurationMinutes,
            ["expectedOutput"] = snapshot.Outputs.Select(MaterialPayload0191).Cast<object>().ToArray(),
            ["createdAtUtc"] = project.CreatedAtUtc,
            ["updatedAtUtc"] = project.UpdatedAtUtc,
            ["completedAtUtc"] = project.CompletedAtUtc.HasValue ? project.CompletedAtUtc.Value : string.Empty
        };
        if (!details) return payload;
        payload["requirements"] = _repositories.ProjectRequirements.Find(Builders<ProjectRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
            .Where(x => admin || (x.IsPlayerVisible && x.VisibilityMode != ProjectVisibilityModeIds.GmOnly && x.VisibilityMode != ProjectVisibilityModeIds.Hidden))
            .Select(x => (object)CraftRequirementPayload0191(x, admin))
            .ToArray();
        payload["resources"] = _repositories.ProjectResourceRequirements.Find(Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
            .Where(x => admin || x.IsPlayerVisible)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.DisplayName,
                ["quantityRequired"] = x.QuantityRequired,
                ["quantityReserved"] = x.QuantityReserved,
                ["quantityProvided"] = x.QuantityProvided,
                ["unit"] = x.Unit,
                ["status"] = x.Status,
                ["statusLabel"] = ResourceStatusLabel0191(x.Status)
            }).ToArray();
        payload["stages"] = LoadCraftStages0191(project.Id)
            .Where(x => admin || x.IsPlayerVisible)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.Name,
                ["status"] = x.Status,
                ["statusLabel"] = StageStatusLabel0191(x.Status),
                ["progressPercent"] = x.ProgressPercent,
                ["isCurrent"] = x.Id == project.CurrentStageId
            }).ToArray();
        var result = _repositories.CraftingResults.Find(Builders<CraftingProjectItemResult>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        payload["result"] = result == null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>
            {
                ["name"] = result.DisplayName,
                ["quantity"] = result.Quantity,
                ["quality"] = result.QualitySummary,
                ["status"] = result.Status,
                ["createdAtUtc"] = result.CreatedAtInventoryUtc.HasValue ? result.CreatedAtInventoryUtc.Value : string.Empty
            };
        if (admin)
        {
            payload["campaignId"] = project.CampaignId;
            payload["ownerCharacterId"] = project.OwnerCharacterId;
            payload["ownerCharacterDisplayName"] = _repositories.CharacterOwnerships.Find(
                    Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, project.OwnerCharacterId))
                .FirstOrDefault()?.CharacterDisplayName ?? "Персонаж не найден";
            payload["gmSummary"] = project.GMSummary;
            payload["gmNotes"] = project.GMNotes;
            payload["snapshotChecksum"] = snapshot.SnapshotChecksum;
            payload["audit"] = _repositories.ProjectAuditEntries.Find(Builders<ProjectAuditEntryState>.Filter.Eq(x => x.ProjectId, project.Id))
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => (object)new Dictionary<string, object>
                {
                    ["action"] = x.ActionType,
                    ["summary"] = x.Summary,
                    ["actorDisplayName"] = AccountDisplayName0191(x.ActorUserId),
                    ["createdAtUtc"] = x.CreatedAtUtc
                }).ToArray();
        }
        return payload;
    }

    private static Dictionary<string, object> CraftRequirementPayload0191(
        ProjectRequirementState requirement,
        bool admin)
    {
        var payload = new Dictionary<string, object>
        {
            ["name"] = requirement.Name,
            ["summary"] = requirement.PublicSummary,
            ["status"] = requirement.Status,
            ["statusLabel"] = RequirementStatusLabel0191(requirement.Status),
            ["required"] = requirement.IsRequired
        };
        if (admin)
        {
            payload["requirementId"] = requirement.Id;
            payload["gmSummary"] = requirement.GMSummary;
        }
        return payload;
    }

    private Dictionary<string, object> CraftPreviewPayload0191(ProjectDefinitionSnapshot0191 snapshot, CraftRequirementEvaluation0191 evaluation)
        => new()
        {
            ["recipeName"] = snapshot.RecipeName,
            ["recipeDescription"] = snapshot.RecipePublicDescription,
            ["methodName"] = snapshot.MethodName,
            ["templateName"] = snapshot.ProjectTemplateName,
            ["estimatedDurationMinutes"] = snapshot.EstimatedDurationMinutes,
            ["requirements"] = evaluation.Requirements.Where(x => x.PlayerVisible).Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.Name,
                ["status"] = x.Satisfied ? "satisfied" : x.ManualGmConfirmation ? "requires_gm" : "missing",
                ["statusLabel"] = x.Satisfied ? "Выполнено" : x.ManualGmConfirmation ? "Требует GM" : "Не выполнено",
                ["summary"] = x.Satisfied ? x.PublicSummary : FirstNonEmpty(x.PublicSummary, "Требование не выполнено."),
                ["required"] = x.Required
            }).ToArray(),
            ["resources"] = evaluation.Resources.Cast<object>().ToArray(),
            ["outputs"] = snapshot.Outputs.Select(MaterialPayload0191).Cast<object>().ToArray(),
            ["canSubmit"] = evaluation.CanSubmit
        };

    private static Dictionary<string, object> MaterialPayload0191(ProjectMaterialSnapshot0191 item)
        => new()
        {
            ["name"] = item.DisplayName,
            ["quantity"] = item.Quantity,
            ["unit"] = item.Unit,
            ["quality"] = item.MinimumQuality
        };

    private Dictionary<string, object> RecipeCard0191(ContentDefinitionRecord recipe)
    {
        var output = ParseMaterialRows0191(ContentField0191(recipe, "outputRows")).FirstOrDefault();
        return new Dictionary<string, object>
        {
            ["recipeId"] = recipe.Id,
            ["name"] = FirstNonEmpty(recipe.DisplayName, recipe.Name),
            ["description"] = recipe.PublicDescription,
            ["estimatedDurationMinutes"] = Math.Max(0, ParseInt0191(ContentField0191(recipe, "estimatedDurationMinutes"))),
            ["outputName"] = output?.DisplayName ?? "Результат"
        };
    }

    private void AddCraftAudit0191(
        ProjectBaseState project,
        string actorId,
        string operationId,
        string action,
        string summary,
        string publicSummary,
        bool playerVisible)
    {
        _repositories.ProjectAuditEntries.Insert(new ProjectAuditEntryState
        {
            ProjectId = project.Id,
            CampaignId = project.CampaignId,
            ActionType = action,
            ActorUserId = actorId,
            Summary = RequireLength(summary, 0, 1024, "summary"),
            PublicSummary = RequireLength(publicSummary, 0, 512, "publicSummary"),
            IsPlayerVisible = playerVisible,
            VisibilityMode = playerVisible ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly,
            ExtraData = new Dictionary<string, object> { ["operationId"] = operationId }
        });
        _logger.Audit($"project.craft action={action} projectId={project.Id} actor={actorId} operationId={operationId}");
    }

    private CharacterOwnershipState RequireCraftCharacter0191(string characterId, UserAccount actor, bool admin)
    {
        var ownership = _repositories.CharacterOwnerships.Find(
            Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()
                        ?? throw new KeyNotFoundException("Character ownership profile not found.");
        if (!admin && !string.Equals(ownership.OwnerUserId, actor.Id, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Character is not owned by this player.");
        if (ownership.IsArchived || !ownership.IsActive)
            throw new InvalidOperationException("Character must be active and not archived.");
        return ownership;
    }

    private ProjectBaseState RequireCraftProject0191(IDictionary<string, object> payload)
    {
        var id = RequireLength(FirstNonEmpty(
            PayloadReader.GetString(payload, "projectId"),
            PayloadReader.GetString(payload, "id")), 1, 128, "projectId");
        var project = _repositories.Projects.GetById(id) ?? throw new KeyNotFoundException("Craft project not found.");
        if (!string.Equals(project.RuntimeKind, CraftItemRuntimeKind0191, StringComparison.Ordinal))
            throw new KeyNotFoundException("Craft project not found.");
        return project;
    }

    private ContentDefinitionRecord RequireCraftRecipe0191(IDictionary<string, object> payload)
    {
        var id = RequireLength(PayloadReader.GetString(payload, "recipeId"), 1, 128, "recipeId");
        var record = FindContentDefinition0191(id, TechnologyRecipeBlueprintProjectDefinitionCategories.Recipe)
                     ?? throw new KeyNotFoundException("Craft recipe not found.");
        if (record.IsArchived) throw new KeyNotFoundException("Craft recipe not found.");
        return record;
    }

    private IEnumerable<ContentDefinitionRecord> LoadCraftRecipes0191()
        => _mongo.ContentDefinitionRecords.Find(
            Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, TechnologyRecipeBlueprintProjectDefinitionCategories.Recipe)
            & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.IsArchived, false)).ToList();

    private IEnumerable<ContentDefinitionRecord> LoadProjectTemplates0191()
        => _mongo.ContentDefinitionRecords.Find(
            Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, TechnologyRecipeBlueprintProjectDefinitionCategories.ProjectTemplate)
            & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.IsArchived, false)).ToList();

    private ContentDefinitionRecord? FindContentDefinition0191(string idOrStableKey, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(idOrStableKey)) return null;
        var filter = Builders<ContentDefinitionRecord>.Filter.Or(
            Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, idOrStableKey),
            Builders<ContentDefinitionRecord>.Filter.Eq(x => x.StableKey, idOrStableKey));
        if (!string.IsNullOrWhiteSpace(category))
            filter &= Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, category);
        return _mongo.ContentDefinitionRecords.Find(filter).FirstOrDefault();
    }

    private bool IsDefinitionAvailable0191(string idOrStableKey, bool requirePlayerVisible)
    {
        var content = FindContentDefinition0191(idOrStableKey);
        if (content != null)
            return !content.IsArchived && (!requirePlayerVisible || IsDefinitionPlayerVisible0191(content));
        var unified = _mongo.UnifiedDefinitions.Find(
            Builders<UnifiedDefinitionDocument>.Filter.Or(
                Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, idOrStableKey),
                Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.StableKey, idOrStableKey))).FirstOrDefault();
        if (unified == null || unified.IsArchived) return false;
        return !requirePlayerVisible
               || string.Equals(unified.VisibilityRule, VisibilityRuleIds.Public, StringComparison.OrdinalIgnoreCase)
               || string.Equals(unified.VisibilityRule, ContentDefinitionVisibilityRules.PlayerVisible, StringComparison.OrdinalIgnoreCase)
               || string.Equals(unified.VisibilityRule, "public", StringComparison.OrdinalIgnoreCase);
    }

    private DefinitionLookup0191 FindAnyDefinition0191(string id)
    {
        var content = FindContentDefinition0191(id);
        if (content != null)
            return new DefinitionLookup0191(content.Category, FirstNonEmpty(content.DisplayName, content.Name), content.PublicDescription, content.Tags);
        var unified = _mongo.UnifiedDefinitions.Find(
            Builders<UnifiedDefinitionDocument>.Filter.Or(
                Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, id),
                Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.StableKey, id))).FirstOrDefault();
        return unified == null
            ? new DefinitionLookup0191("item", "Созданный предмет", string.Empty, new List<string>())
            : new DefinitionLookup0191(unified.Category, unified.Name, unified.PublicDescription, unified.Tags);
    }

    private string DefinitionDisplayName0191(string id, string fallback)
    {
        if (string.IsNullOrWhiteSpace(id)) return fallback;
        var found = FindAnyDefinition0191(id);
        return FirstNonEmpty(found.Name, fallback);
    }

    private static string ContentField0191(ContentDefinitionRecord record, string name)
        => record.CustomFields.TryGetValue(name, out var value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

    private List<ProjectMaterialSnapshot0191> ParseMaterialRows0191(string value)
        => ParseRows0191(value).Select(row =>
        {
            var definitionId = Cell0191(row, 0);
            var definition = FindAnyDefinition0191(definitionId);
            return new ProjectMaterialSnapshot0191
            {
                DefinitionId = definitionId,
                StableKey = FindDefinitionStableKey0191(definitionId),
                DisplayName = FirstNonEmpty(definition.Name, "Материал"),
                Quantity = Math.Max(0, ParseDecimal0191(Cell0191(row, 1))),
                Unit = Cell0191(row, 2),
                MinimumQuality = Cell0191(row, 3),
                UsageMode = Cell0191(row, 4),
                Optional = ParseBool0191(Cell0191(row, 6), false)
            };
        }).Where(x => !string.IsNullOrWhiteSpace(x.DefinitionId) && x.Quantity > 0).ToList();

    private List<ProjectStageSnapshot0191> ParseStageRows0191(string value)
        => ParseRows0191(value).Select((row, index) => new ProjectStageSnapshot0191
        {
            Key = Cell0191(row, 0, "stage_" + (index + 1)),
            DisplayName = Cell0191(row, 1, "Стадия " + (index + 1)),
            Order = Math.Max(1, ParseInt0191(Cell0191(row, 2, (index + 1).ToString(CultureInfo.InvariantCulture)))),
            AllowedPreviousStageKeys = SplitTokens0191(Cell0191(row, 3)).ToList(),
            AllowedNextStageKeys = SplitTokens0191(Cell0191(row, 4)).ToList(),
            RequiredConditions = Cell0191(row, 5),
            RequiresGMDecision = ParseBool0191(Cell0191(row, 6), true) || string.Equals(Cell0191(row, 6), "GM", StringComparison.OrdinalIgnoreCase),
            IsPlayerVisible = ParseBool0191(Cell0191(row, 7), true),
            PublicSummary = Cell0191(row, 8)
        }).OrderBy(x => x.Order).ToList();

    private static List<string[]> ParseRows0191(string value)
        => (value ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('|').Select(y => y.Trim()).ToArray())
            .Where(x => x.Length > 0)
            .ToList();

    private static IEnumerable<string> SplitDefinitionRefs0191(string value)
        => SplitTokens0191(value);

    private static IEnumerable<string> SplitTokens0191(string value)
        => (value ?? string.Empty)
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private string FindDefinitionStableKey0191(string id)
    {
        var content = FindContentDefinition0191(id);
        if (content != null) return content.StableKey;
        return _mongo.UnifiedDefinitions.Find(Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, id)).FirstOrDefault()?.StableKey ?? string.Empty;
    }

    private static string ComputeSnapshotChecksum0191(ProjectDefinitionSnapshot0191 snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot);
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(json)).Select(x => x.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string Cell0191(string[] row, int index, string fallback = "")
        => index >= 0 && index < row.Length && !string.IsNullOrWhiteSpace(row[index]) ? row[index].Trim() : fallback;

    private static decimal ParseDecimal0191(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("ru-RU"), out result) ? result : 0m;

    private static int ParseInt0191(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static bool ParseBool0191(string value, bool fallback)
        => bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static CraftRequirementLine0191 CraftRequirement0191(
        string kind,
        string name,
        bool satisfied,
        string success,
        string failure,
        bool required)
        => new()
        {
            Kind = kind,
            Name = name,
            Satisfied = satisfied,
            PublicSummary = satisfied ? success : failure,
            GMSummary = satisfied ? success : failure,
            Required = required,
            PlayerVisible = true
        };

    private static void RequireOwnerOrAdmin0191(ProjectBaseState project, UserAccount actor, bool admin)
    {
        if (!admin && !string.Equals(project.OwnerUserId, actor.Id, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the project owner can access this project.");
    }

    private string RequireOperationId0191(CommandContext context)
        => RequireLength(FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "operationId"),
            context.Request.RequestId), 8, 128, "operationId");

    private bool ProjectCraftViewEnabled0191(bool admin)
        => ProjectCraftBaseEnabled0191()
           && (admin
               ? _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectAdminView))
               : _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectPlayerView)));

    private bool ProjectCraftBaseEnabled0191()
        => _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectRuntimeV1))
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseCraftItemProjectV1));

    private bool ProjectCraftAdminEnabled0191()
        => ProjectCraftBaseEnabled0191()
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectAdminView));

    private static bool IsProjectCraftAdmin0191(UserAccount actor)
        => actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);

    private static bool IsDefinitionPlayerVisible0191(ContentDefinitionRecord definition)
        => !definition.IsArchived
           && (string.Equals(definition.VisibilityRule, ContentDefinitionVisibilityRules.PlayerVisible, StringComparison.OrdinalIgnoreCase)
               || string.Equals(definition.VisibilityRule, VisibilityRuleIds.Public, StringComparison.OrdinalIgnoreCase)
               || string.Equals(definition.VisibilityRule, "public", StringComparison.OrdinalIgnoreCase));

    private ResponseEnvelope ProjectCraftDisabled0191(string command)
    {
        _logger.Admin($"project.craft.disabled command={command}");
        return Error("Unified CraftItem project runtime is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private string AccountDisplayName0191(string userId)
    {
        var account = _repositories.Accounts.GetById(userId);
        if (account == null) return "Система";
        var profile = string.IsNullOrWhiteSpace(account.ProfileId) ? null : _repositories.Profiles.GetById(account.ProfileId);
        return FirstNonEmpty(profile?.DisplayName, account.Login, "Пользователь");
    }

    private static string CraftProjectStatusLabel0191(string status) => status switch
    {
        ProjectStatusIds.Draft => "Черновик",
        ProjectStatusIds.RequirementsReview => "Проверка требований",
        ProjectStatusIds.AwaitingApproval => "Ожидает решения GM",
        ProjectStatusIds.Approved => "Одобрено",
        ProjectStatusIds.ResourcesReserved => "Ресурсы зарезервированы",
        ProjectStatusIds.InProgress => "В работе",
        ProjectStatusIds.Completed => "Завершено",
        ProjectStatusIds.Cancelled => "Отменено",
        ProjectStatusIds.Failed => "Неудача",
        _ => status
    };

    private static string RequirementStatusLabel0191(string status) => status switch
    {
        ProjectRequirementStatusIds.Satisfied => "Выполнено",
        ProjectRequirementStatusIds.Waived => "Необязательно",
        ProjectRequirementStatusIds.Blocked => "Заблокировано",
        _ => "Требует проверки"
    };

    private static string ResourceStatusLabel0191(string status) => status switch
    {
        ProjectResourceRequirementStatusIds.Reserved => "Зарезервировано",
        ProjectResourceRequirementStatusIds.Provided => "Предоставлено",
        ProjectResourceRequirementStatusIds.ConsumedManually => "Списано",
        ProjectResourceRequirementStatusIds.Waived => "Не требуется",
        _ => "Требуется"
    };

    private static string StageStatusLabel0191(string status) => status switch
    {
        ProjectStageStatusIds.Active => "Текущая",
        ProjectStageStatusIds.Completed => "Выполнена",
        ProjectStageStatusIds.Failed => "Не пройдена",
        ProjectStageStatusIds.Cancelled => "Отменена",
        _ => "Ожидает"
    };

    private sealed class CraftRequirementEvaluation0191
    {
        public List<CraftRequirementLine0191> Requirements { get; } = new();
        public List<Dictionary<string, object>> Resources { get; } = new();
        public bool CanSubmit { get; set; }
    }

    private sealed class CraftRequirementLine0191
    {
        public string Kind { get; set; } = string.Empty;
        public string DefinitionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PublicSummary { get; set; } = string.Empty;
        public string GMSummary { get; set; } = string.Empty;
        public bool Required { get; set; }
        public bool Satisfied { get; set; }
        public bool ManualGmConfirmation { get; set; }
        public bool PlayerVisible { get; set; }
    }

    private sealed class DefinitionLookup0191
    {
        public DefinitionLookup0191(string category, string name, string description, List<string> tags)
        {
            Category = category;
            Name = name;
            Description = description;
            Tags = tags;
        }

        public string Category { get; }
        public string Name { get; }
        public string Description { get; }
        public List<string> Tags { get; }
    }
}
