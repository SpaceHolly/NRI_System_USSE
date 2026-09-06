using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private static readonly object PrototypeRuntimeLock0194 = new();
    private const string PrototypeRuntimeKind0194 = "create_prototype_0194";

    public ResponseEnvelope ProjectPrototypeBlueprintList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectPrototypeViewEnabled0194(admin)) return ProjectPrototypeDisabled0194(context.Request.Command);

        var items = LoadPrototypeBlueprints0194()
            .Where(x => admin || IsDefinitionPlayerVisible0191(x))
            .Select(x => (object)PrototypeBlueprintCard0194(x))
            .ToArray();
        return Ok("Prototype blueprints loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectPrototypePreview(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectPrototypeViewEnabled0194(admin)) return ProjectPrototypeDisabled0194(context.Request.Command);
        var blueprint = RequirePrototypeBlueprint0194(context.Request.Payload);
        if (!admin && !IsDefinitionPlayerVisible0191(blueprint))
            throw new UnauthorizedAccessException("Blueprint is not available to this player.");
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var ownership = RequireCraftCharacter0191(characterId, actor, admin);
        var snapshot = BuildPrototypeSnapshot0194(blueprint, !admin);
        var evaluation = EvaluatePrototypeRequirements0194(snapshot, ownership);
        return Ok("Prototype requirements evaluated.", new Dictionary<string, object>
        {
            ["preview"] = PrototypePreviewPayload0194(snapshot, evaluation)
        });
    }

    public ResponseEnvelope ProjectPrototypeCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectPrototypeViewEnabled0194(admin)) return ProjectPrototypeDisabled0194(context.Request.Command);
        var operationId = RequireOperationId0191(context);

        lock (PrototypeRuntimeLock0194)
        {
            var existing = _repositories.Projects.Find(
                    Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedOperationId, operationId)
                    & Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedByUserId, actor.Id))
                .FirstOrDefault();
            if (existing != null)
                return Ok("Prototype project already created.", ProjectPrototypeResponse0194(existing, admin, true));

            var blueprint = RequirePrototypeBlueprint0194(context.Request.Payload);
            if (!admin && !IsDefinitionPlayerVisible0191(blueprint))
                throw new UnauthorizedAccessException("Blueprint is not available to this player.");
            var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
            var ownership = RequireCraftCharacter0191(characterId, actor, admin);
            var snapshot = BuildPrototypeSnapshot0194(blueprint, !admin);
            var evaluation = EvaluatePrototypeRequirements0194(snapshot, ownership);
            if (!evaluation.CanSubmit)
                throw new InvalidOperationException("Prototype requirements are not satisfied.");

            var project = new ProjectBaseState
            {
                CampaignId = FirstNonEmpty(ownership.CampaignId, PayloadReader.GetString(context.Request.Payload, "campaignId"), "default"),
                RuleSetId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "ruleSetId"), blueprint.RuleSetId, RuleSetIds.FantasyNriDefault),
                ProjectType = ProjectTypeIds.CreatePrototype,
                RuntimeKind = PrototypeRuntimeKind0194,
                Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), snapshot.BlueprintName + " - прототип"), 2, 180, "name"),
                PublicSummary = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicSummary"), snapshot.BlueprintPublicDescription, snapshot.BlueprintName),
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
                LastOperationCommand = CommandNames.ProjectPrototypeCreate,
                DefinitionSnapshot = snapshot,
                WorkPointsRequired = Math.Max(1, snapshot.Stages.Count),
                Revision = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                ExpectedResultSummary = new Dictionary<string, object>
                {
                    ["kind"] = "prototype",
                    ["label"] = snapshot.TargetItemName + " (прототип)",
                    ["productionApproval"] = PrototypeProductionApprovalStatusIds.NotProductionApproved
                }
            };
            _repositories.Projects.Insert(project);
            CreatePrototypeChildren0194(project, evaluation, actor.Id);
            AddPrototypeAudit0194(project, actor.Id, operationId, "project.created",
                "Создан проект прототипирования.", "Проект прототипирования создан.", true);
            TryPublishProjectSync(project, "prototype.created", actor.Id, context.Request.RequestId ?? string.Empty);
            return Ok("Prototype project created.", ProjectPrototypeResponse0194(project, admin));
        }
    }

    public ResponseEnvelope ProjectPrototypeSubmit(CommandContext context)
        => MutatePrototypeProject0194(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status != ProjectStatusIds.Draft && project.Status != ProjectStatusIds.RequirementsReview)
                throw new InvalidOperationException("Only a draft project can be submitted.");
            project.Status = ProjectStatusIds.AwaitingApproval;
            project.ApprovalStatus = ProjectApprovalStatusIds.PendingGmReview;
            project.SubmittedAtUtc = DateTime.UtcNow;
            if (!_repositories.ProjectApprovals.Find(Builders<ProjectApprovalState>.Filter.Eq(x => x.ProjectId, project.Id)).Any())
            {
                _repositories.ProjectApprovals.Insert(new ProjectApprovalState
                {
                    ProjectId = project.Id,
                    CampaignId = project.CampaignId,
                    ApprovalType = "gm_prototype_start",
                    Status = ProjectApprovalStatusIds.PendingGmReview,
                    RequestedByUserId = actor.Id,
                    PublicSummary = "Проект прототипа ожидает решения GM.",
                    GMSummary = "Проверьте требования, ресурсы и обязательный TestProtocol.",
                    IsPlayerVisible = true
                });
            }
            AddPrototypeAudit0194(project, actor.Id, operationId, "project.submitted",
                "Проект прототипа отправлен на согласование.", "Проект отправлен GM.", true);
        }, "Prototype project submitted.");

    public ResponseEnvelope ProjectPrototypeList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectPrototypeViewEnabled0194(admin)) return ProjectPrototypeDisabled0194(context.Request.Command);
        var filter = Builders<ProjectBaseState>.Filter.Eq(x => x.RuntimeKind, PrototypeRuntimeKind0194)
                     & Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false);
        if (!admin) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.OwnerUserId, actor.Id);
        var items = _repositories.Projects.Find(filter).OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)PrototypeProjectPayload0194(x, admin, false)).ToArray();
        return Ok("Prototype projects loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectPrototypeGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectPrototypeViewEnabled0194(admin)) return ProjectPrototypeDisabled0194(context.Request.Command);
        var project = RequirePrototypeProject0194(context.Request.Payload);
        RequireOwnerOrAdmin0191(project, actor, admin);
        return Ok("Prototype project loaded.", ProjectPrototypeResponse0194(project, admin));
    }

    public ResponseEnvelope ProjectPrototypeRequirementConfirm(CommandContext context)
        => MutatePrototypeProject0194(context, true, (project, actor, _, operationId) =>
        {
            var requirementId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requirementId"), 1, 128, "requirementId");
            var requirement = _repositories.ProjectRequirements.GetById(requirementId)
                              ?? throw new KeyNotFoundException("Project requirement not found.");
            if (!string.Equals(requirement.ProjectId, project.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("Requirement belongs to another project.");
            requirement.Status = ProjectRequirementStatusIds.Satisfied;
            requirement.VerifiedByUserId = actor.Id;
            requirement.VerifiedAtUtc = DateTime.UtcNow;
            requirement.PublicNotes = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicNote"), "Требование подтверждено GM.");
            requirement.GMNotes = RequireLength(PayloadReader.GetString(context.Request.Payload, "gmNote"), 0, 1024, "gmNote");
            _repositories.ProjectRequirements.Replace(requirement);
            AddPrototypeAudit0194(project, actor.Id, operationId, "requirement.confirmed",
                "Подтверждено требование: " + requirement.Name, requirement.PublicNotes, true);
        }, "Prototype requirement confirmed.");

    public ResponseEnvelope ProjectPrototypeApprove(CommandContext context)
        => MutatePrototypeProject0194(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Project is not awaiting approval.");
            var open = RequiredOpenRequirements0191(project.Id).Where(x => !IsApprovalRequirement0191(x)).ToArray();
            if (open.Length > 0)
                throw new InvalidOperationException("Required conditions are not satisfied: " + string.Join(", ", open.Select(x => x.Name)));
            project.Status = ProjectStatusIds.Approved;
            project.ApprovalStatus = ProjectApprovalStatusIds.Approved;
            project.ApprovedAtUtc = DateTime.UtcNow;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Approved, "Проект прототипа одобрен.");
            AddPrototypeAudit0194(project, actor.Id, operationId, "project.approved",
                "Проект прототипа одобрен GM.", "GM одобрил создание прототипа.", true);
        }, "Prototype project approved.");

    public ResponseEnvelope ProjectPrototypeReject(CommandContext context)
        => MutatePrototypeProject0194(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Project is not awaiting approval.");
            var reason = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicReason"), "Проект прототипа отклонён GM."), 1, 512, "publicReason");
            project.Status = ProjectStatusIds.Failed;
            project.ApprovalStatus = ProjectApprovalStatusIds.Rejected;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Rejected, reason);
            AddPrototypeAudit0194(project, actor.Id, operationId, "project.rejected", reason, reason, true);
        }, "Prototype project rejected.");

    public ResponseEnvelope ProjectPrototypeReserve(CommandContext context)
        => MutatePrototypeProject0194(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.Approved)
                throw new InvalidOperationException("Only an approved project can reserve resources.");
            ReserveCraftResources0191(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.ResourcesReserved;
            AddPrototypeAudit0194(project, actor.Id, operationId, "resources.reserved",
                "Ресурсы прототипа зарезервированы.", "Ресурсы проекта зарезервированы.", true);
        }, "Prototype resources reserved.");

    public ResponseEnvelope ProjectPrototypeStart(CommandContext context)
        => MutatePrototypeProject0194(context, true, (project, actor, _, operationId) =>
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
            AddPrototypeAudit0194(project, actor.Id, operationId, "project.started",
                "Создание прототипа началось.", "Работа над прототипом началась.", true);
        }, "Prototype project started.");

    public ResponseEnvelope ProjectPrototypeStageComplete(CommandContext context)
        => MutatePrototypeProject0194(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Prototype project is not in progress.");
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
                project.WorkPointsDone = stages.Count(x => x.Status == ProjectStageStatusIds.Completed);
                project.ProgressPercent = Math.Min(80, (int)Math.Round(80d * project.WorkPointsDone / Math.Max(1, stages.Count)));
            }
            else
            {
                CreatePhysicalPrototype0194(project, actor.Id, operationId);
                project.Status = ProjectStatusIds.Testing;
                project.CurrentStageId = string.Empty;
                project.CurrentStageName = "Ожидает испытания";
                project.WorkPointsDone = stages.Count;
                project.ProgressPercent = 90;
                project.ResultStatus = ProjectResultStatusIds.ReadyForAcceptance;
            }
            AddPrototypeAudit0194(project, actor.Id, operationId, "stage.completed",
                "Завершена стадия: " + current.Name,
                next == null ? "Прототип создан и ожидает испытания." : "Завершена стадия «" + current.Name + "».", true);
        }, "Prototype stage completed.");

    public ResponseEnvelope ProjectPrototypeTestExecute(CommandContext context)
        => MutatePrototypeProject0194(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.Testing)
                throw new InvalidOperationException("Prototype is not awaiting a test.");
            ExecutePrototypeTest0194(project, actor.Id, operationId);
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.test.executed",
                "Выполнен обязательный TestProtocol; создан частичный результат с дефектом.",
                "Испытание завершено частично: обнаружен открытый дефект.", true);
        }, "Prototype test executed.", project => _repositories.PrototypeTestResults.Find(
            Builders<PrototypeTestResultState>.Filter.Eq(x => x.ProjectId, project.Id)).Any());

    public ResponseEnvelope ProjectPrototypeComplete(CommandContext context)
        => MutatePrototypeProject0194(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.Completed) return;
            if (project.Status != ProjectStatusIds.Testing)
                throw new InvalidOperationException("Prototype project is not ready for completion.");
            var result = _repositories.PrototypeTestResults.Find(
                    Builders<PrototypeTestResultState>.Filter.Eq(x => x.ProjectId, project.Id))
                .FirstOrDefault() ?? throw new InvalidOperationException("Mandatory TestProtocol has not been executed.");
            project.Status = ProjectStatusIds.Completed;
            project.ResultStatus = ProjectResultStatusIds.Applied;
            project.ProgressPercent = 100;
            project.CompletedAtUtc = DateTime.UtcNow;
            project.ExpectedResultSummary["projectOutcome"] = result.ResultCategory == PrototypeTestResultCategoryIds.Pass
                ? PrototypeProjectOutcomeIds.PrototypePassed
                : result.ResultCategory == PrototypeTestResultCategoryIds.PartialPass
                    ? PrototypeProjectOutcomeIds.PrototypeCompletedWithDefects
                    : PrototypeProjectOutcomeIds.PrototypeFailed;
            project.ExpectedResultSummary["productionApproval"] = PrototypeProductionApprovalStatusIds.NotProductionApproved;
            AddPrototypeAudit0194(project, actor.Id, operationId, "project.completed",
                "Проект завершён: прототип испытан, серийное производство не разрешено.",
                "Проект завершён. Прототип не допущен к производству.", true);
        }, "Prototype project completed.", project => project.Status == ProjectStatusIds.Completed);

    public ResponseEnvelope ProjectPrototypeCancel(CommandContext context)
        => MutatePrototypeProject0194(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (_repositories.PrototypeRuntimeStates.Find(
                    Builders<PrototypeRuntimeState>.Filter.Eq(x => x.ProjectId, project.Id)).Any())
                throw new InvalidOperationException("A created physical prototype cannot be removed by ordinary cancellation.");
            if (project.Status is ProjectStatusIds.InProgress or ProjectStatusIds.Completed or ProjectStatusIds.Failed or ProjectStatusIds.Cancelled)
                throw new InvalidOperationException("Project cannot be cancelled in its current state.");
            ReleaseCraftReservations0191(project.Id, actor.Id, "prototype project cancelled");
            project.Status = ProjectStatusIds.Cancelled;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            AddPrototypeAudit0194(project, actor.Id, operationId, "project.cancelled",
                "Проект прототипа отменён.", "Проект отменён, резерв освобождён.", true);
        }, "Prototype project cancelled.");

    public ResponseEnvelope ProjectPrototypeFail(CommandContext context)
        => MutatePrototypeProject0194(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status is ProjectStatusIds.Completed or ProjectStatusIds.Cancelled)
                throw new InvalidOperationException("Completed or cancelled project cannot fail.");
            if (!_repositories.PrototypeRuntimeStates.Find(Builders<PrototypeRuntimeState>.Filter.Eq(x => x.ProjectId, project.Id)).Any())
                ReleaseCraftReservations0191(project.Id, actor.Id, "prototype project failed");
            project.Status = ProjectStatusIds.Failed;
            project.ResultStatus = ProjectResultStatusIds.Failed;
            AddPrototypeAudit0194(project, actor.Id, operationId, "project.failed",
                FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "gmReason"), "Проект прототипа завершён неудачей."),
                FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicReason"), "Проект прототипа завершён неудачей."), true);
        }, "Prototype project failed.");

    public ResponseEnvelope ProjectPrototypeAudit(CommandContext context)
    {
        RequireAdmin(context);
        if (!ProjectPrototypeAdminEnabled0194()) return ProjectPrototypeDisabled0194(context.Request.Command);
        var project = RequirePrototypeProject0194(context.Request.Payload);
        var prototypeItemId = "prototype_item_0194_" + project.Id;
        var inventoryDocument = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, project.OwnerCharacterId))
            .FirstOrDefault();
        var items = _repositories.ProjectAuditEntries.Find(
                Builders<ProjectAuditEntryState>.Filter.Eq(x => x.ProjectId, project.Id))
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["action"] = x.ActionType,
                ["summary"] = x.Summary,
                ["publicSummary"] = x.PublicSummary,
                ["actorDisplayName"] = AccountDisplayName0191(x.ActorUserId),
                ["createdAtUtc"] = x.CreatedAtUtc
            }).ToArray();
        var persistenceCounts = new Dictionary<string, object>
        {
            ["physicalItemInstances"] = inventoryDocument?.Profile?.Items?.Count(x =>
                string.Equals(x.ItemId, prototypeItemId, StringComparison.Ordinal)) ?? 0,
            ["prototypeRuntimeStates"] = _repositories.PrototypeRuntimeStates.Find(
                Builders<PrototypeRuntimeState>.Filter.Eq(x => x.ProjectId, project.Id)).Count,
            ["testResults"] = _repositories.PrototypeTestResults.Find(
                Builders<PrototypeTestResultState>.Filter.Eq(x => x.ProjectId, project.Id)).Count,
            ["defectInstances"] = _repositories.PrototypeDefectInstances.Find(
                Builders<PrototypeDefectInstanceState>.Filter.Eq(x => x.ProjectId, project.Id)).Count,
            ["completionJournalEntries"] = _repositories.EventJournalEntries.Find(
                Builders<EventJournalEntryState>.Filter.Eq(x => x.SubjectEntityType, "project")
                & Builders<EventJournalEntryState>.Filter.Eq(x => x.SubjectEntityId, project.Id)
                & Builders<EventJournalEntryState>.Filter.Eq(x => x.SourceModule, "projects")).Count
        };
        return Ok("Prototype project audit loaded.", new Dictionary<string, object>
        {
            ["items"] = items,
            ["persistenceCounts"] = persistenceCounts
        });
    }

    private ResponseEnvelope MutatePrototypeProject0194(
        CommandContext context,
        bool adminOnly,
        Action<ProjectBaseState, UserAccount, bool, string> mutation,
        string successMessage,
        Func<ProjectBaseState, bool>? alreadyApplied = null)
    {
        var actor = adminOnly ? RequireAdmin(context) : GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (adminOnly && !admin) throw new UnauthorizedAccessException("Admin role is required.");
        if (!ProjectPrototypeViewEnabled0194(admin)) return ProjectPrototypeDisabled0194(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (PrototypeRuntimeLock0194)
        {
            var project = RequirePrototypeProject0194(context.Request.Payload);
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (alreadyApplied?.Invoke(project) == true)
                return Ok(successMessage, ProjectPrototypeResponse0194(project, admin, true));
            if (string.Equals(project.LastOperationId, operationId, StringComparison.Ordinal))
            {
                if (!string.Equals(project.LastOperationCommand, context.Request.Command, StringComparison.Ordinal))
                    throw new InvalidOperationException("OperationId was already used for another command.");
                return Ok(successMessage, ProjectPrototypeResponse0194(project, admin, true));
            }
            var expected = PayloadReader.GetInt(context.Request.Payload, "expectedRevision")
                           ?? throw new ArgumentException("expectedRevision is required.");
            if (expected != project.Revision)
                throw new InvalidOperationException($"Project revision conflict. Reload project. current={project.Revision}; expected={expected}");
            mutation(project, actor, admin, operationId);
            SavePrototypeProject0194(project, actor.Id, operationId, context.Request.Command, expected);
            TryPublishProjectSync(project, context.Request.Command, actor.Id, context.Request.RequestId ?? string.Empty);
            if (project.Status == ProjectStatusIds.Completed)
                TryWriteProjectJournal(project, operationId, "Завершён проект прототипа: " + project.Name, actor.Id);
            return Ok(successMessage, ProjectPrototypeResponse0194(project, admin));
        }
    }

    private void SavePrototypeProject0194(ProjectBaseState project, string actorId, string operationId, string command, int expectedRevision)
    {
        project.UpdatedAtUtc = DateTime.UtcNow;
        project.UpdatedByUserId = actorId;
        project.LastOperationId = operationId;
        project.LastOperationCommand = command;
        project.Revision = expectedRevision + 1;
        var result = _mongo.Projects.ReplaceOne(
            Builders<ProjectBaseState>.Filter.Eq(x => x.Id, project.Id)
            & Builders<ProjectBaseState>.Filter.Eq(x => x.Revision, expectedRevision), project);
        if (result.MatchedCount == 1) return;
        project.Revision = expectedRevision;
        throw new InvalidOperationException("Project was changed by another operation. Reload and retry.");
    }

    private ProjectDefinitionSnapshot0191 BuildPrototypeSnapshot0194(ContentDefinitionRecord blueprint, bool requirePlayerVisible)
    {
        var targetId = SplitDefinitionRefs0191(ContentField0191(blueprint, "targetDefinition")).FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetId) || !IsDefinitionAvailable0191(targetId, requirePlayerVisible))
            throw new InvalidOperationException("Blueprint target ItemDefinition is unavailable.");
        var methodId = SplitDefinitionRefs0191(ContentField0191(blueprint, "methods")).FirstOrDefault() ?? string.Empty;
        var recipeId = SplitDefinitionRefs0191(ContentField0191(blueprint, "recipes")).FirstOrDefault() ?? string.Empty;
        var technologyId = SplitDefinitionRefs0191(ContentField0191(blueprint, "technologies")).FirstOrDefault() ?? string.Empty;
        var testProtocolId = SplitDefinitionRefs0191(ContentField0191(blueprint, "testProtocols")).FirstOrDefault()
                             ?? throw new InvalidOperationException("Blueprint has no mandatory TestProtocol.");
        var testProtocol = FindContentDefinition0191(testProtocolId, TechnologyRecipeBlueprintProjectDefinitionCategories.TestProtocol)
                           ?? throw new InvalidOperationException("Blueprint TestProtocol is unavailable.");
        var template = LoadProjectTemplates0191().FirstOrDefault(x =>
                           string.Equals(ContentField0191(x, "projectType"), "CreatePrototype", StringComparison.OrdinalIgnoreCase)
                           && SplitDefinitionRefs0191(ContentField0191(x, "blueprints"))
                               .Any(id => string.Equals(id, blueprint.Id, StringComparison.OrdinalIgnoreCase)
                                          || string.Equals(id, blueprint.StableKey, StringComparison.OrdinalIgnoreCase)))
                       ?? throw new KeyNotFoundException("CreatePrototype project template was not found.");
        if (requirePlayerVisible && (!IsDefinitionPlayerVisible0191(testProtocol) || !IsDefinitionPlayerVisible0191(template)))
            throw new UnauthorizedAccessException("Prototype runtime is not available to this player.");

        var target = FindAnyDefinition0191(targetId);
        var method = FindContentDefinition0191(methodId, TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod);
        var recipe = FindContentDefinition0191(recipeId, TechnologyRecipeBlueprintProjectDefinitionCategories.Recipe);
        var technology = FindContentDefinition0191(technologyId, TechnologyRecipeBlueprintProjectDefinitionCategories.Technology);
        if (method == null || recipe == null || technology == null)
            throw new InvalidOperationException("Blueprint Technology, ProductionMethod or Recipe is unavailable.");

        var inputs = ParsePrototypeComponentRows0194(ContentField0191(blueprint, "componentRows"));
        if (inputs.Count == 0) throw new InvalidOperationException("Blueprint has no resolved required components.");
        var defectSnapshots = SplitDefinitionRefs0191(ContentField0191(blueprint, "knownDefects"))
            .Select(id => FindContentDefinition0191(id, TechnologyRecipeBlueprintProjectDefinitionCategories.Defect))
            .Where(x => x != null && !x.IsArchived)
            .Select(x => BuildPrototypeDefectSnapshot0194(x!))
            .ToList();
        if (defectSnapshots.Count == 0) throw new InvalidOperationException("Reference prototype flow requires an applicable DefectDefinition.");

        var snapshot = new ProjectDefinitionSnapshot0191
        {
            BlueprintDefinitionId = blueprint.Id,
            BlueprintStableKey = blueprint.StableKey,
            BlueprintVersion = FirstNonEmpty(blueprint.RecordVersion, blueprint.DefinitionPackVersion),
            BlueprintRevision = blueprint.Revision,
            BlueprintName = FirstNonEmpty(blueprint.DisplayName, blueprint.Name),
            BlueprintPublicDescription = blueprint.PublicDescription,
            BlueprintKind = ContentField0191(blueprint, "blueprintKind"),
            TargetItemDefinitionId = targetId,
            TargetItemStableKey = FindDefinitionStableKey0191(targetId),
            TargetItemName = FirstNonEmpty(target.Name, "Прототип"),
            TargetItemPublicDescription = target.Description,
            PrototypeOutputQuantity = 1,
            TechnologyDefinitionId = technology.Id,
            TechnologyStableKey = technology.StableKey,
            TechnologyVersion = FirstNonEmpty(technology.RecordVersion, technology.DefinitionPackVersion),
            TechnologyRevision = technology.Revision,
            TechnologyName = FirstNonEmpty(technology.DisplayName, technology.Name),
            TechnologyPublicDescription = technology.PublicDescription,
            RecipeDefinitionId = recipe.Id,
            RecipeStableKey = recipe.StableKey,
            RecipeVersion = FirstNonEmpty(recipe.RecordVersion, recipe.DefinitionPackVersion),
            RecipeRevision = recipe.Revision,
            RecipeName = FirstNonEmpty(recipe.DisplayName, recipe.Name),
            RecipePublicDescription = recipe.PublicDescription,
            MethodDefinitionId = method.Id,
            MethodStableKey = method.StableKey,
            MethodVersion = FirstNonEmpty(method.RecordVersion, method.DefinitionPackVersion),
            MethodRevision = method.Revision,
            MethodName = FirstNonEmpty(method.DisplayName, method.Name),
            ProjectTemplateDefinitionId = template.Id,
            ProjectTemplateStableKey = template.StableKey,
            ProjectTemplateVersion = FirstNonEmpty(template.RecordVersion, template.DefinitionPackVersion),
            ProjectTemplateRevision = template.Revision,
            ProjectTemplateName = FirstNonEmpty(template.DisplayName, template.Name),
            ApprovalPolicy = ContentField0191(template, "approvalPolicy"),
            ResourceReservationPolicy = ContentField0191(template, "resourceReservationPolicy"),
            CancellationRefundPolicy = ContentField0191(template, "cancellationRefundPolicy"),
            EstimatedDurationMinutes = Math.Max(0, ParseInt0191(ContentField0191(blueprint, "estimatedDurationMinutes"))),
            Inputs = inputs,
            Outputs = new List<ProjectMaterialSnapshot0191>
            {
                new()
                {
                    DefinitionId = targetId,
                    StableKey = FindDefinitionStableKey0191(targetId),
                    DisplayName = FirstNonEmpty(target.Name, "Прототип"),
                    Quantity = 1,
                    Unit = "шт.",
                    MinimumQuality = "prototype",
                    UsageMode = "prototype_output"
                }
            },
            Stages = ParseStageRows0191(ContentField0191(template, "stageRows")),
            Requirements = ParsePrototypeRequirements0194(template, blueprint),
            PrototypeTestProtocol = BuildPrototypeTestSnapshot0194(testProtocol),
            PrototypeDefects = defectSnapshots
        };
        if (snapshot.Stages.Count == 0) throw new InvalidOperationException("Prototype ProjectTemplate has no construction stages.");
        foreach (var input in snapshot.Inputs)
            if (!IsDefinitionAvailable0191(input.DefinitionId, requirePlayerVisible))
                throw new InvalidOperationException("Blueprint references an unavailable component: " + input.DisplayName);
        snapshot.SnapshotChecksum = ComputeSnapshotChecksum0191(snapshot);
        return snapshot;
    }

    private List<ProjectMaterialSnapshot0191> ParsePrototypeComponentRows0194(string value)
        => ParseRows0191(value).Select(row =>
        {
            var id = Cell0191(row, 0);
            var definition = FindAnyDefinition0191(id);
            return new ProjectMaterialSnapshot0191
            {
                DefinitionId = id,
                StableKey = FindDefinitionStableKey0191(id),
                DisplayName = FirstNonEmpty(definition.Name, "Компонент"),
                Quantity = Math.Max(0, ParseDecimal0191(Cell0191(row, 1))),
                Unit = Cell0191(row, 2, "шт."),
                MinimumQuality = "standard",
                UsageMode = "consumed",
                Optional = !ParseBool0191(Cell0191(row, 3), true)
            };
        }).Where(x => !string.IsNullOrWhiteSpace(x.DefinitionId) && x.Quantity > 0 && !x.Optional).ToList();

    private List<ProjectRequirementSnapshot0191> ParsePrototypeRequirements0194(
        ContentDefinitionRecord template,
        ContentDefinitionRecord blueprint)
    {
        var result = new List<ProjectRequirementSnapshot0191>();
        foreach (var row in ParseRows0191(ContentField0191(template, "requirementRows")))
        {
            if (row.Length == 0) continue;
            var id = Cell0191(row, 1);
            result.Add(new ProjectRequirementSnapshot0191
            {
                Kind = Cell0191(row, 0, "custom_manual"),
                DefinitionId = id,
                DisplayName = Cell0191(row, 2, DefinitionDisplayName0191(id, "Требование")),
                Quantity = Math.Max(0, ParseDecimal0191(Cell0191(row, 3))),
                MinimumQualityOrRank = Cell0191(row, 4),
                Required = ParseBool0191(Cell0191(row, 5), true),
                ConsumptionMode = Cell0191(row, 6),
                PublicExplanation = Cell0191(row, 7, "Требуется подтверждение."),
                GMExplanation = Cell0191(row, 8)
            });
        }
        AddPrototypeReferenceRequirements0194(result, "knowledge", "Знание технологии", ContentField0191(blueprint, "technologies"));
        AddPrototypeReferenceRequirements0194(result, "facility", "Площадка", ContentField0191(blueprint, "requiredFacilities"));
        AddPrototypeReferenceRequirements0194(result, "tool", "Инструмент", ContentField0191(blueprint, "requiredTools"));
        AddPrototypeReferenceRequirements0194(result, "license", "Лицензия", ContentField0191(blueprint, "requiredLicenses"));
        AddPrototypeReferenceRequirements0194(result, "test_protocol", "Обязательное испытание", ContentField0191(blueprint, "testProtocols"));
        return result.GroupBy(x => x.Kind + "|" + x.DefinitionId, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
    }

    private void AddPrototypeReferenceRequirements0194(
        ICollection<ProjectRequirementSnapshot0191> target,
        string kind,
        string prefix,
        string value)
    {
        foreach (var id in SplitDefinitionRefs0191(value))
        {
            target.Add(new ProjectRequirementSnapshot0191
            {
                Kind = kind,
                DefinitionId = id,
                DisplayName = prefix + ": " + DefinitionDisplayName0191(id, "определение"),
                Required = true,
                IsPlayerVisible = true,
                PublicExplanation = kind == "knowledge"
                    ? "Персонаж должен знать связанную технологию."
                    : kind == "test_protocol"
                        ? "Прототип обязан пройти указанное испытание."
                        : "Наличие подтверждает GM."
            });
        }
    }

    private PrototypeTestProtocolSnapshot0194 BuildPrototypeTestSnapshot0194(ContentDefinitionRecord definition)
    {
        var rows = ParseRows0191(ContentField0191(definition, "testSteps"));
        return new PrototypeTestProtocolSnapshot0194
        {
            DefinitionId = definition.Id,
            StableKey = definition.StableKey,
            Version = FirstNonEmpty(definition.RecordVersion, definition.DefinitionPackVersion),
            Revision = definition.Revision,
            Name = FirstNonEmpty(definition.DisplayName, definition.Name),
            PublicDescription = definition.PublicDescription,
            RequiredStageKey = ContentField0191(definition, "requiredStageKey"),
            PublicSteps = rows.Select(x => Cell0191(x, 1, "Шаг") + ": " + Cell0191(x, 2)).ToList(),
            GMSteps = rows.Select(x => Cell0191(x, 1, "Шаг") + ": " + Cell0191(x, 3)).ToList(),
            MetricLabels = SplitTokens0191(ContentField0191(definition, "metricLabels")).ToList(),
            PassCriteria = ContentField0191(definition, "passCriteria"),
            PartialPassCriteria = ContentField0191(definition, "partialPassCriteria"),
            FailureCriteria = ContentField0191(definition, "failureCriteria"),
            PublicResultTemplate = ContentField0191(definition, "publicResultTemplate"),
            GMResultTemplate = ContentField0191(definition, "gmResultTemplate")
        };
    }

    private PrototypeDefectSnapshot0194 BuildPrototypeDefectSnapshot0194(ContentDefinitionRecord definition)
        => new()
        {
            DefinitionId = definition.Id,
            StableKey = definition.StableKey,
            Version = FirstNonEmpty(definition.RecordVersion, definition.DefinitionPackVersion),
            Revision = definition.Revision,
            Name = FirstNonEmpty(definition.DisplayName, definition.Name),
            Severity = ContentField0191(definition, "severity"),
            PublicSymptoms = SplitTokens0191(ContentField0191(definition, "publicSymptoms")).ToList(),
            GMCauseDetails = ContentField0191(definition, "gmCauseDetails"),
            LimitationTags = SplitTokens0191(ContentField0191(definition, "limitationTags")).ToList()
        };

    private PrototypeRequirementEvaluation0194 EvaluatePrototypeRequirements0194(
        ProjectDefinitionSnapshot0191 snapshot,
        CharacterOwnershipState ownership)
    {
        var result = new PrototypeRequirementEvaluation0194();
        result.Requirements.Add(PrototypeRequirementLine0194.Create(
            "ownership", "Активный персонаж", ownership.IsActive && !ownership.IsArchived,
            "Персонаж активен и доступен владельцу.", "Персонаж неактивен или находится в архиве.", true, true, false));
        result.Requirements.Add(PrototypeRequirementLine0194.Create(
            "blueprint", "Canonical Blueprint", !string.IsNullOrWhiteSpace(snapshot.BlueprintDefinitionId),
            "Чертёж опубликован и доступен.", "Чертёж недоступен.", true, true, false));
        result.Requirements.Add(PrototypeRequirementLine0194.Create(
            "target", "Целевой ItemDefinition", !string.IsNullOrWhiteSpace(snapshot.TargetItemDefinitionId),
            "Целевой тип предмета доступен.", "Целевой тип предмета недоступен.", true, true, false));
        result.Requirements.Add(PrototypeRequirementLine0194.Create(
            "test_protocol", "Обязательный TestProtocol", snapshot.PrototypeTestProtocol != null,
            "Протокол испытания зафиксирован в snapshot.", "Протокол испытания отсутствует.", true, true, false));

        var known = KnownTopics0192(ownership.CharacterId);
        foreach (var requirement in snapshot.Requirements)
        {
            var knowledge = string.Equals(requirement.Kind, "knowledge", StringComparison.OrdinalIgnoreCase);
            var test = string.Equals(requirement.Kind, "test_protocol", StringComparison.OrdinalIgnoreCase);
            var manual = !knowledge && !test && !string.Equals(requirement.Kind, "resource", StringComparison.OrdinalIgnoreCase);
            var satisfied = knowledge
                ? TopicKnown0192(known, requirement.DefinitionId, FindDefinitionStableKey0191(requirement.DefinitionId), DefinitionDisplayName0191(requirement.DefinitionId, string.Empty))
                : test;
            result.Requirements.Add(new PrototypeRequirementLine0194
            {
                Kind = requirement.Kind,
                DefinitionId = requirement.DefinitionId,
                Name = requirement.DisplayName,
                PublicSummary = satisfied
                    ? (knowledge ? "Требуемая технология известна персонажу." : requirement.PublicExplanation)
                    : manual ? "Требуется подтверждение GM." : requirement.PublicExplanation,
                GMSummary = FirstNonEmpty(requirement.GMExplanation, requirement.PublicExplanation),
                Required = requirement.Required,
                Satisfied = satisfied || !requirement.Required,
                ManualGmConfirmation = manual,
                PlayerVisible = requirement.IsPlayerVisible
            });
        }
        foreach (var input in snapshot.Inputs)
        {
            var available = FindAvailableInventoryItem0191(
                ownership.CharacterId, input.DefinitionId, input.Quantity, input.MinimumQuality);
            result.Resources.Add(new Dictionary<string, object>
            {
                ["name"] = input.DisplayName,
                ["quantity"] = input.Quantity,
                ["unit"] = input.Unit,
                ["minimumQuality"] = input.MinimumQuality,
                ["status"] = available == null ? "missing" : "available",
                ["statusLabel"] = available == null ? "Не хватает" : "Будет зарезервировано"
            });
        }
        result.CanSubmit = result.Requirements.Where(x => x.Required && !x.ManualGmConfirmation).All(x => x.Satisfied)
                           && result.Resources.All(x => string.Equals(Convert.ToString(x["status"]), "available", StringComparison.Ordinal));
        return result;
    }

    private void CreatePrototypeChildren0194(ProjectBaseState project, PrototypeRequirementEvaluation0194 evaluation, string actorId)
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
                ExtraData = new Dictionary<string, object> { ["stageKey"] = stage.Key, ["runtimeKind"] = PrototypeRuntimeKind0194 }
            });
        }
        foreach (var line in evaluation.Requirements)
        {
            _repositories.ProjectRequirements.Insert(new ProjectRequirementState
            {
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                RequirementType = line.Kind,
                Name = line.Name,
                PublicSummary = line.PublicSummary,
                GMSummary = line.GMSummary,
                Status = line.Satisfied ? ProjectRequirementStatusIds.Satisfied : ProjectRequirementStatusIds.Open,
                IsRequired = line.Required,
                IsPlayerVisible = line.PlayerVisible,
                VisibilityMode = line.PlayerVisible ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly,
                VerifiedByUserId = line.Satisfied ? "server" : string.Empty,
                VerifiedAtUtc = line.Satisfied ? DateTime.UtcNow : null,
                ExtraData = new Dictionary<string, object>
                {
                    ["manualGmConfirmation"] = line.ManualGmConfirmation,
                    ["definitionId"] = line.DefinitionId,
                    ["runtimeKind"] = PrototypeRuntimeKind0194
                }
            });
        }
        foreach (var input in snapshot.Inputs)
        {
            _repositories.ProjectResourceRequirements.Insert(new ProjectResourceRequirementState
            {
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                ResourceType = "prototype_component",
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
                    ["stableKey"] = input.StableKey,
                    ["runtimeKind"] = PrototypeRuntimeKind0194
                }
            });
        }
    }

    private void CreatePhysicalPrototype0194(ProjectBaseState project, string actorId, string operationId)
    {
        var snapshot = project.DefinitionSnapshot ?? throw new InvalidOperationException("Project snapshot is missing.");
        var itemId = "prototype_item_0194_" + project.Id;
        var prototypeId = "prototype_0194_" + project.Id;
        var existingPrototype = _repositories.PrototypeRuntimeStates.GetById(prototypeId);
        var document = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, project.OwnerCharacterId))
            .FirstOrDefault() ?? throw new KeyNotFoundException("Character inventory profile not found.");
        document.Profile ??= new InventoryProfile { CharacterId = project.OwnerCharacterId, RuleSetId = project.RuleSetId };
        document.Profile.Items ??= new List<CharacterInventoryItemProfileValue>();

        if (!document.Profile.Items.Any(x => string.Equals(x.ItemId, itemId, StringComparison.Ordinal)))
        {
            var reservations = ActiveCraftReservations0191(project.Id).ToList();
            if (reservations.Count == 0) throw new InvalidOperationException("Active resource reservations were not found.");
            foreach (var group in reservations.GroupBy(x => x.ItemInstanceId, StringComparer.OrdinalIgnoreCase))
            {
                var item = document.Profile.Items.FirstOrDefault(x => string.Equals(x.ItemId, group.Key, StringComparison.OrdinalIgnoreCase))
                           ?? throw new KeyNotFoundException("Reserved inventory resource was not found.");
                var units = group.Sum(ReservationInventoryUnits0191);
                if (item.Quantity < units) throw new InvalidOperationException("Reserved inventory quantity is no longer available.");
                item.Quantity -= units;
                item.UpdatedAtUtc = DateTime.UtcNow;
                item.Source = "prototype_resource_consumption_0194";
                if (item.Quantity <= 0) document.Profile.Items.Remove(item);
            }
            var target = FindAnyDefinition0191(snapshot.TargetItemDefinitionId);
            document.Profile.Items.Add(new CharacterInventoryItemProfileValue
            {
                ItemId = itemId,
                DefinitionId = snapshot.TargetItemDefinitionId,
                ItemDefinitionId = snapshot.TargetItemDefinitionId,
                DefinitionCategory = target.Category,
                SnapshotDisplayName = snapshot.TargetItemName + " (прототип)",
                SnapshotCategory = target.Category,
                SnapshotDescription = snapshot.TargetItemPublicDescription,
                SnapshotTags = target.Tags,
                Name = snapshot.TargetItemName + " (прототип)",
                DisplayName = snapshot.TargetItemName + " (прототип)",
                Category = target.Category,
                Description = snapshot.TargetItemPublicDescription,
                Quantity = 1,
                Durability = 80,
                MaxDurability = 100,
                Condition = "Ожидает испытания",
                IsPlayerVisible = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Source = "prototype_result_0194",
                Tags = new List<string>
                {
                    "prototype",
                    "awaiting_test",
                    "not_production_approved",
                    "project:" + project.Id
                }
            });
            var originalUpdated = document.UpdatedUtc;
            document.UpdatedUtc = DateTime.UtcNow;
            var write = _mongo.CharacterInventoryProfiles.ReplaceOne(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, document.Id)
                & Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.UpdatedUtc, originalUpdated), document);
            if (write.MatchedCount != 1)
                throw new InvalidOperationException("Inventory changed while creating prototype. Reload and retry.");
        }

        foreach (var reservation in ActiveCraftReservations0191(project.Id))
        {
            reservation.Status = CraftingReservationStatusIds.Consumed;
            reservation.QuantityConsumed = reservation.QuantityReserved;
            reservation.ConsumedAtUtc = DateTime.UtcNow;
            reservation.UpdatedByUserId = actorId;
            _repositories.CraftingReservations.Replace(reservation);
            var requirement = _repositories.ProjectResourceRequirements.GetById(reservation.RequirementId);
            if (requirement == null) continue;
            requirement.QuantityProvided = requirement.QuantityRequired;
            requirement.Status = ProjectResourceRequirementStatusIds.ConsumedManually;
            requirement.UpdatedByUserId = actorId;
            requirement.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ProjectResourceRequirements.Replace(requirement);
        }

        if (existingPrototype == null)
        {
            _repositories.PrototypeRuntimeStates.Insert(new PrototypeRuntimeState
            {
                Id = prototypeId,
                CampaignId = project.CampaignId,
                ProjectId = project.Id,
                ItemInstanceId = itemId,
                OwnerCharacterId = project.OwnerCharacterId,
                OwnerUserId = project.OwnerUserId,
                BlueprintDefinitionId = snapshot.BlueprintDefinitionId,
                BlueprintStableKey = snapshot.BlueprintStableKey,
                BlueprintName = snapshot.BlueprintName,
                TargetItemDefinitionId = snapshot.TargetItemDefinitionId,
                DisplayName = snapshot.TargetItemName + " (прототип)",
                LifecycleStatus = PrototypeLifecycleStatusIds.AwaitingTest,
                TestStatus = PrototypeTestStatusIds.AwaitingTest,
                ProductionApprovalStatus = PrototypeProductionApprovalStatusIds.NotProductionApproved,
                CreatedByUserId = actorId,
                CreationOperationId = operationId,
                TestReservationOperationId = operationId,
                IsTestReserved = true,
                IsPlayerVisible = true,
                ExtraData = new Dictionary<string, object>
                {
                    ["snapshotChecksum"] = snapshot.SnapshotChecksum
                }
            });
        }
    }

    private void ExecutePrototypeTest0194(ProjectBaseState project, string actorId, string operationId)
    {
        var snapshot = project.DefinitionSnapshot ?? throw new InvalidOperationException("Project snapshot is missing.");
        var protocol = snapshot.PrototypeTestProtocol ?? throw new InvalidOperationException("TestProtocol snapshot is missing.");
        var prototype = _repositories.PrototypeRuntimeStates.Find(
                Builders<PrototypeRuntimeState>.Filter.Eq(x => x.ProjectId, project.Id))
            .FirstOrDefault() ?? throw new KeyNotFoundException("Physical prototype was not found.");
        var resultId = "prototype_test_result_0194_" + project.Id;
        if (_repositories.PrototypeTestResults.GetById(resultId) != null) return;
        if (!prototype.IsTestReserved || prototype.TestStatus != PrototypeTestStatusIds.AwaitingTest)
            throw new InvalidOperationException("Prototype is not reserved for this test.");

        var defectSnapshot = snapshot.PrototypeDefects.FirstOrDefault()
                             ?? throw new InvalidOperationException("Reference outcome requires a defect snapshot.");
        var defectId = "prototype_defect_0194_" + project.Id;
        var result = new PrototypeTestResultState
        {
            Id = resultId,
            CampaignId = project.CampaignId,
            ProjectId = project.Id,
            PrototypeId = prototype.Id,
            ItemInstanceId = prototype.ItemInstanceId,
            OwnerCharacterId = project.OwnerCharacterId,
            TestProtocolDefinitionId = protocol.DefinitionId,
            TestProtocolStableKey = protocol.StableKey,
            TestProtocolVersion = protocol.Version,
            TestProtocolRevision = protocol.Revision,
            TestProtocolName = protocol.Name,
            ExecutedSteps = protocol.PublicSteps,
            ObservedMetrics = new Dictionary<string, decimal>
            {
                ["stability"] = 72m,
                ["thermal_margin"] = 61m
            },
            ResultCategory = PrototypeTestResultCategoryIds.PartialPass,
            PublicSummary = FirstNonEmpty(protocol.PublicResultTemplate,
                "Испытание завершено частично: прототип работоспособен с ограничениями."),
            GMSummary = FirstNonEmpty(protocol.GMResultTemplate,
                "Детерминированный ReferenceDemo outcome: резонансный дрейф обнаружен на нагрузочном шаге."),
            GeneratedDefectInstanceIds = new List<string> { defectId },
            AttemptNumber = 1,
            ExecutedByUserId = actorId,
            ExecutionOperationId = operationId,
            IsPlayerVisible = true,
            ServerOnlyData = new Dictionary<string, object>
            {
                ["outcomePolicy"] = "PartialPassWithDefect"
            }
        };
        _repositories.PrototypeTestResults.Insert(result);
        if (_repositories.PrototypeDefectInstances.GetById(defectId) == null)
        {
            _repositories.PrototypeDefectInstances.Insert(new PrototypeDefectInstanceState
            {
                Id = defectId,
                CampaignId = project.CampaignId,
                ProjectId = project.Id,
                PrototypeId = prototype.Id,
                ItemInstanceId = prototype.ItemInstanceId,
                OwnerCharacterId = project.OwnerCharacterId,
                SourceTestResultId = result.Id,
                DefectDefinitionId = defectSnapshot.DefinitionId,
                DefectStableKey = defectSnapshot.StableKey,
                DefectVersion = defectSnapshot.Version,
                DefectRevision = defectSnapshot.Revision,
                Name = defectSnapshot.Name,
                Severity = defectSnapshot.Severity,
                Status = PrototypeDefectStatusIds.Open,
                PublicSymptoms = defectSnapshot.PublicSymptoms,
                GMCauseDetails = defectSnapshot.GMCauseDetails,
                LimitationTags = defectSnapshot.LimitationTags,
                DetectionOperationId = operationId,
                IsPlayerVisible = true,
                ServerOnlyData = new Dictionary<string, object>
                {
                    ["definitionSnapshotChecksum"] = snapshot.SnapshotChecksum
                }
            });
        }
        prototype.LifecycleStatus = PrototypeLifecycleStatusIds.TestedWithDefects;
        prototype.TestStatus = PrototypeTestStatusIds.Completed;
        prototype.ActiveDefectInstanceIds = new List<string> { defectId };
        prototype.LatestTestResultId = result.Id;
        prototype.IsTestReserved = false;
        prototype.UpdatedAtUtc = DateTime.UtcNow;
        prototype.Revision++;
        _repositories.PrototypeRuntimeStates.Replace(prototype);
        UpdatePrototypeInventoryAfterTest0194(prototype.ItemInstanceId, project.OwnerCharacterId);
    }

    private void UpdatePrototypeInventoryAfterTest0194(string itemId, string characterId)
    {
        var document = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId))
            .FirstOrDefault() ?? throw new KeyNotFoundException("Character inventory profile not found.");
        var item = document.Profile?.Items?.FirstOrDefault(x => string.Equals(x.ItemId, itemId, StringComparison.Ordinal))
                   ?? throw new KeyNotFoundException("Prototype ItemInstance not found.");
        item.Condition = "Испытан: открытый дефект";
        item.Tags = (item.Tags ?? new List<string>())
            .Where(x => !string.Equals(x, "awaiting_test", StringComparison.OrdinalIgnoreCase))
            .Concat(new[] { "tested", "has_open_defects", "not_production_approved" })
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        item.UpdatedAtUtc = DateTime.UtcNow;
        var originalUpdated = document.UpdatedUtc;
        document.UpdatedUtc = DateTime.UtcNow;
        var write = _mongo.CharacterInventoryProfiles.ReplaceOne(
            Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, document.Id)
            & Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.UpdatedUtc, originalUpdated), document);
        if (write.MatchedCount != 1)
            throw new InvalidOperationException("Inventory changed while applying test result. Reload and retry.");
    }

    private Dictionary<string, object> ProjectPrototypeResponse0194(ProjectBaseState project, bool admin, bool alreadyApplied = false)
        => new()
        {
            ["item"] = PrototypeProjectPayload0194(project, admin, true),
            ["alreadyApplied"] = alreadyApplied,
            ["revision"] = project.Revision
        };

    private Dictionary<string, object> PrototypeProjectPayload0194(ProjectBaseState project, bool admin, bool details)
    {
        var snapshot = project.DefinitionSnapshot ?? new ProjectDefinitionSnapshot0191();
        var prototype = _repositories.PrototypeRuntimeStates.Find(
            Builders<PrototypeRuntimeState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        var testResult = _repositories.PrototypeTestResults.Find(
            Builders<PrototypeTestResultState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        var defect = _repositories.PrototypeDefectInstances.Find(
            Builders<PrototypeDefectInstanceState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        var payload = new Dictionary<string, object>
        {
            ["projectId"] = project.Id,
            ["name"] = project.Name,
            ["projectType"] = ProjectTypeIds.CreatePrototype,
            ["projectTypeLabel"] = "Создание прототипа",
            ["status"] = project.Status,
            ["statusLabel"] = PrototypeProjectStatusLabel0194(project.Status),
            ["approvalStatus"] = project.ApprovalStatus,
            ["progressPercent"] = project.ProgressPercent,
            ["currentStageName"] = project.CurrentStageName,
            ["ownerDisplayName"] = project.OwnerDisplayName,
            ["ownerCharacterDisplayName"] = _repositories.CharacterOwnerships.Find(
                    Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, project.OwnerCharacterId))
                .FirstOrDefault()?.CharacterDisplayName ?? "Персонаж не найден",
            ["blueprintName"] = snapshot.BlueprintName,
            ["targetItemName"] = snapshot.TargetItemName,
            ["technologyName"] = snapshot.TechnologyName,
            ["methodName"] = snapshot.MethodName,
            ["recipeName"] = snapshot.RecipeName,
            ["templateName"] = snapshot.ProjectTemplateName,
            ["testProtocolName"] = snapshot.PrototypeTestProtocol?.Name ?? string.Empty,
            ["prototypeWarning"] = "Это прототип, а не серийный предмет.",
            ["productionApprovalLabel"] = "Не допущено к производству",
            ["prototypeStatus"] = PrototypeLifecycleLabel0194(prototype?.LifecycleStatus),
            ["testStatus"] = PrototypeTestLabel0194(prototype?.TestStatus),
            ["testResultCategory"] = PrototypeTestResultLabel0194(testResult?.ResultCategory),
            ["testPublicSummary"] = testResult?.PublicSummary ?? string.Empty,
            ["defectName"] = defect?.IsPlayerVisible == true ? defect.Name : string.Empty,
            ["defectSeverity"] = defect?.IsPlayerVisible == true ? defect.Severity : string.Empty,
            ["defectSymptoms"] = defect?.IsPlayerVisible == true ? string.Join(", ", defect.PublicSymptoms) : string.Empty,
            ["defectLimitations"] = defect?.IsPlayerVisible == true ? string.Join(", ", defect.LimitationTags) : string.Empty,
            ["updatedAtUtc"] = project.UpdatedAtUtc,
            ["revision"] = project.Revision
        };
        if (details)
        {
            payload["requirements"] = _repositories.ProjectRequirements.Find(
                    Builders<ProjectRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
                .Where(x => admin || IsProjectItemPlayerVisible(x.IsPlayerVisible, x.VisibilityMode))
                .Select(x =>
                {
                    var item = new Dictionary<string, object>
                    {
                        ["name"] = x.Name,
                        ["summary"] = x.PublicSummary,
                        ["status"] = x.Status,
                        ["statusLabel"] = RequirementStatusLabel0191(x.Status)
                    };
                    if (admin) item["requirementId"] = x.Id;
                    return (object)item;
                }).ToArray();
            payload["resources"] = _repositories.ProjectResourceRequirements.Find(
                    Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
                .Where(x => admin || x.IsPlayerVisible)
                .Select(x => (object)new Dictionary<string, object>
                {
                    ["name"] = x.DisplayName,
                    ["quantity"] = x.QuantityRequired,
                    ["unit"] = x.Unit,
                    ["status"] = x.Status,
                    ["statusLabel"] = ResourceStatusLabel0191(x.Status)
                }).ToArray();
            payload["stages"] = LoadCraftStages0191(project.Id).Where(x => admin || x.IsPlayerVisible)
                .Select(x => (object)new Dictionary<string, object>
                {
                    ["name"] = x.Name,
                    ["summary"] = x.PublicSummary,
                    ["status"] = x.Status,
                    ["statusLabel"] = StageStatusLabel0191(x.Status),
                    ["progressPercent"] = x.ProgressPercent
                }).ToArray();
            payload["testSteps"] = (snapshot.PrototypeTestProtocol?.PublicSteps ?? new List<string>()).Cast<object>().ToArray();
        }
        if (admin)
        {
            payload["ownerCharacterId"] = project.OwnerCharacterId;
            payload["testGmSummary"] = testResult?.GMSummary ?? string.Empty;
            payload["defectGmCause"] = defect?.GMCauseDetails ?? string.Empty;
            payload["snapshotChecksum"] = snapshot.SnapshotChecksum;
            payload["audit"] = _repositories.ProjectAuditEntries.Find(
                    Builders<ProjectAuditEntryState>.Filter.Eq(x => x.ProjectId, project.Id))
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

    private Dictionary<string, object> PrototypePreviewPayload0194(
        ProjectDefinitionSnapshot0191 snapshot,
        PrototypeRequirementEvaluation0194 evaluation)
        => new()
        {
            ["blueprintName"] = snapshot.BlueprintName,
            ["blueprintDescription"] = snapshot.BlueprintPublicDescription,
            ["targetItemName"] = snapshot.TargetItemName,
            ["technologyName"] = snapshot.TechnologyName,
            ["methodName"] = snapshot.MethodName,
            ["recipeName"] = snapshot.RecipeName,
            ["testProtocolName"] = snapshot.PrototypeTestProtocol?.Name ?? string.Empty,
            ["testSteps"] = (snapshot.PrototypeTestProtocol?.PublicSteps ?? new List<string>()).Cast<object>().ToArray(),
            ["publicRisks"] = snapshot.PrototypeDefects.Select(x => (object)(x.Name + ": " + string.Join(", ", x.PublicSymptoms))).ToArray(),
            ["prototypeWarning"] = "Результат является опытным образцом и не допускается к серийному производству.",
            ["requirements"] = evaluation.Requirements.Where(x => x.PlayerVisible).Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.Name,
                ["summary"] = x.PublicSummary,
                ["status"] = x.Satisfied ? "satisfied" : x.ManualGmConfirmation ? "gm_confirmation" : "missing",
                ["statusLabel"] = x.Satisfied ? "Выполнено" : x.ManualGmConfirmation ? "Подтверждает GM" : "Не выполнено"
            }).ToArray(),
            ["resources"] = evaluation.Resources.Cast<object>().ToArray(),
            ["canSubmit"] = evaluation.CanSubmit
        };

    private Dictionary<string, object> PrototypeBlueprintCard0194(ContentDefinitionRecord blueprint)
        => new()
        {
            ["blueprintId"] = blueprint.Id,
            ["name"] = FirstNonEmpty(blueprint.DisplayName, blueprint.Name),
            ["description"] = blueprint.PublicDescription,
            ["kind"] = ContentField0191(blueprint, "blueprintKind"),
            ["targetItemName"] = DefinitionDisplayName0191(
                SplitDefinitionRefs0191(ContentField0191(blueprint, "targetDefinition")).FirstOrDefault() ?? string.Empty,
                "Прототип"),
            ["testProtocolName"] = DefinitionDisplayName0191(
                SplitDefinitionRefs0191(ContentField0191(blueprint, "testProtocols")).FirstOrDefault() ?? string.Empty,
                "Обязательное испытание"),
            ["summary"] = FirstNonEmpty(blueprint.PublicDescription, blueprint.Name)
        };

    private ContentDefinitionRecord RequirePrototypeBlueprint0194(IDictionary<string, object> payload)
    {
        var id = RequireLength(PayloadReader.GetString(payload, "blueprintId"), 1, 128, "blueprintId");
        var record = FindContentDefinition0191(id, TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint)
                     ?? throw new KeyNotFoundException("Prototype blueprint not found.");
        if (record.IsArchived) throw new KeyNotFoundException("Prototype blueprint not found.");
        return record;
    }

    private ProjectBaseState RequirePrototypeProject0194(IDictionary<string, object> payload)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "projectId"), PayloadReader.GetString(payload, "id")), 1, 128, "projectId");
        var project = _repositories.Projects.GetById(id) ?? throw new KeyNotFoundException("Prototype project not found.");
        if (!string.Equals(project.RuntimeKind, PrototypeRuntimeKind0194, StringComparison.Ordinal))
            throw new KeyNotFoundException("Prototype project not found.");
        return project;
    }

    private IEnumerable<ContentDefinitionRecord> LoadPrototypeBlueprints0194()
        => _mongo.ContentDefinitionRecords.Find(
            Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint)
            & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.IsArchived, false)).ToList()
            .Where(x => LoadProjectTemplates0191().Any(t =>
                string.Equals(ContentField0191(t, "projectType"), "CreatePrototype", StringComparison.OrdinalIgnoreCase)
                && SplitDefinitionRefs0191(ContentField0191(t, "blueprints"))
                    .Any(id => string.Equals(id, x.Id, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(id, x.StableKey, StringComparison.OrdinalIgnoreCase))));

    private void AddPrototypeAudit0194(
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
        _logger.Audit($"project.prototype action={action} projectId={project.Id} actor={actorId} operationId={operationId}");
    }

    internal void EnsureInventoryProfileUpdateDoesNotModifyPrototypeTestReservations0194(string characterId)
    {
        var active = _repositories.PrototypeRuntimeStates.Find(
            Builders<PrototypeRuntimeState>.Filter.Eq(x => x.OwnerCharacterId, characterId)
            & (Builders<PrototypeRuntimeState>.Filter.Eq(x => x.IsTestReserved, true)
               | Builders<PrototypeRuntimeState>.Filter.Eq(x => x.IsRepairReserved, true)));
        if (active.Count > 0)
            throw new InvalidOperationException(
                "Инвентарь содержит прототип, зарезервированный для испытания или ремонта. Сначала завершите активный проект.");
    }

    internal void EnsurePrototypeInventoryItemActionAllowed0194(
        string characterId,
        string itemId,
        string action)
    {
        var prototype = _repositories.PrototypeRuntimeStates.Find(
                Builders<PrototypeRuntimeState>.Filter.Eq(x => x.OwnerCharacterId, characterId)
                & Builders<PrototypeRuntimeState>.Filter.Eq(x => x.ItemInstanceId, itemId))
            .FirstOrDefault();
        if (prototype == null) return;

        if (string.Equals(action, "remove", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "update", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Физический прототип связан с испытанием и дефектами. Изменение или удаление доступно только будущему специальному workflow.");

        var blocksEquip = prototype.IsTestReserved || prototype.IsRepairReserved;
        if (!blocksEquip)
        {
            blocksEquip = _repositories.PrototypeDefectInstances.Find(
                    Builders<PrototypeDefectInstanceState>.Filter.Eq(x => x.PrototypeId, prototype.Id)
                    & Builders<PrototypeDefectInstanceState>.Filter.In(
                        x => x.Status,
                        new[]
                        {
                            PrototypeDefectStatusIds.Open,
                            PrototypeDefectStatusIds.RepairInProgress,
                            PrototypeDefectStatusIds.ResolvedPendingRetest
                        }))
                .Any(x => x.LimitationTags.Any(tag =>
                    string.Equals(tag, "no_equip", StringComparison.OrdinalIgnoreCase)));
        }
        if (blocksEquip && string.Equals(action, "equip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Прототип нельзя экипировать до завершения ремонта, повторного испытания и устранения запрещающего дефекта.");
    }

    private bool ProjectPrototypeViewEnabled0194(bool admin)
        => ProjectPrototypeBaseEnabled0194()
           && (admin
               ? _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedPrototypeAdminView))
               : _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedPrototypePlayerView)));

    private bool ProjectPrototypeBaseEnabled0194()
        => _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectRuntimeV1))
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseCreatePrototypeProjectV1));

    private bool ProjectPrototypeAdminEnabled0194()
        => ProjectPrototypeBaseEnabled0194()
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedPrototypeAdminView));

    private ResponseEnvelope ProjectPrototypeDisabled0194(string command)
    {
        _logger.Admin($"project.prototype.disabled command={command}");
        return Error("Unified CreatePrototype project runtime is disabled by feature flags.",
            ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private static string PrototypeProjectStatusLabel0194(string status) => status switch
    {
        ProjectStatusIds.Draft => "Черновик",
        ProjectStatusIds.AwaitingApproval => "Ожидает решения GM",
        ProjectStatusIds.Approved => "Одобрено",
        ProjectStatusIds.ResourcesReserved => "Ресурсы зарезервированы",
        ProjectStatusIds.InProgress => "Создание прототипа",
        ProjectStatusIds.Testing => "Ожидает испытания",
        ProjectStatusIds.Completed => "Завершено",
        ProjectStatusIds.Cancelled => "Отменено",
        ProjectStatusIds.Failed => "Неудача",
        _ => status
    };

    private static string PrototypeLifecycleLabel0194(string? status) => status switch
    {
        PrototypeLifecycleStatusIds.AwaitingTest => "Ожидает испытания",
        PrototypeLifecycleStatusIds.TestedWithDefects => "Испытан: есть открытый дефект",
        PrototypeLifecycleStatusIds.TestedPassed => "Испытан успешно",
        PrototypeLifecycleStatusIds.TestFailed => "Испытание не пройдено",
        _ => "Прототип ещё не создан"
    };

    private static string PrototypeTestLabel0194(string? status) => status switch
    {
        PrototypeTestStatusIds.AwaitingTest => "Ожидает обязательного испытания",
        PrototypeTestStatusIds.Completed => "Испытание завершено",
        _ => "Испытание ещё недоступно"
    };

    private static string PrototypeTestResultLabel0194(string? result) => result switch
    {
        PrototypeTestResultCategoryIds.Pass => "Пройдено",
        PrototypeTestResultCategoryIds.PartialPass => "Частично пройдено",
        PrototypeTestResultCategoryIds.Fail => "Не пройдено",
        _ => "Результата пока нет"
    };

    private sealed class PrototypeRequirementEvaluation0194
    {
        public List<PrototypeRequirementLine0194> Requirements { get; } = new();
        public List<Dictionary<string, object>> Resources { get; } = new();
        public bool CanSubmit { get; set; }
    }

    private sealed class PrototypeRequirementLine0194
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

        public static PrototypeRequirementLine0194 Create(
            string kind,
            string name,
            bool satisfied,
            string success,
            string failure,
            bool required,
            bool playerVisible,
            bool manual)
            => new()
            {
                Kind = kind,
                Name = name,
                Satisfied = satisfied,
                PublicSummary = satisfied ? success : failure,
                GMSummary = satisfied ? success : failure,
                Required = required,
                PlayerVisible = playerVisible,
                ManualGmConfirmation = manual
            };
    }
}
