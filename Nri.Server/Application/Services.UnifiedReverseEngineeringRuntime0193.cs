using System;
using System.Collections;
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
    private static readonly object ProjectReverseEngineeringRuntimeLock0193 = new();
    private const string ReverseEngineeringRuntimeKind0193 = "reverse_engineering_0193";
    private const string ReverseEngineeringReservationRole0193 = "reverse_engineering_source";
    private const string ReverseEngineeringResourceRole0193 = "reverse_engineering_resource";

    public ResponseEnvelope ProjectReverseEngineeringSourceList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectReverseEngineeringViewEnabled0193(admin))
            return ProjectReverseEngineeringDisabled0193(context.Request.Command);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var ownership = RequireCraftCharacter0191(characterId, actor, admin);
        var profile = LoadInventoryProfile0193(ownership.CharacterId);
        var items = profile.Items
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ItemId) && FindReverseEngineeringTemplate0193(x) != null)
            .Select(x => (object)ReverseEngineeringSourceCard0193(ownership.CharacterId, x))
            .ToArray();
        return Ok("Reverse engineering source items loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectReverseEngineeringPreview(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectReverseEngineeringViewEnabled0193(admin))
            return ProjectReverseEngineeringDisabled0193(context.Request.Command);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var ownership = RequireCraftCharacter0191(characterId, actor, admin);
        var source = RequireReverseEngineeringSource0193(ownership.CharacterId, context.Request.Payload);
        var snapshot = BuildReverseEngineeringSnapshot0193(source, requirePlayerVisible: !admin);
        var evaluation = EvaluateReverseEngineeringRequirements0193(snapshot, ownership, source);
        return Ok("Reverse engineering requirements evaluated.", new Dictionary<string, object>
        {
            ["preview"] = ReverseEngineeringPreviewPayload0193(snapshot, evaluation)
        });
    }

    public ResponseEnvelope ProjectReverseEngineeringCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectReverseEngineeringViewEnabled0193(admin))
            return ProjectReverseEngineeringDisabled0193(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (ProjectReverseEngineeringRuntimeLock0193)
        {
            var existing = _repositories.Projects.Find(
                    Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedOperationId, operationId)
                    & Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedByUserId, actor.Id))
                .FirstOrDefault();
            if (existing != null)
                return Ok("Reverse engineering project already created.",
                    ProjectReverseEngineeringResponse0193(existing, admin, alreadyApplied: true));

            var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
            var ownership = RequireCraftCharacter0191(characterId, actor, admin);
            var source = RequireReverseEngineeringSource0193(ownership.CharacterId, context.Request.Payload);
            var snapshot = BuildReverseEngineeringSnapshot0193(source, requirePlayerVisible: !admin);
            var evaluation = EvaluateReverseEngineeringRequirements0193(snapshot, ownership, source);
            if (evaluation.AlreadyKnown)
                throw new InvalidOperationException("Персонаж уже получил полное открытие из этого предмета.");
            var project = new ProjectBaseState
            {
                CampaignId = FirstNonEmpty(ownership.CampaignId, PayloadReader.GetString(context.Request.Payload, "campaignId"), "default"),
                RuleSetId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "ruleSetId"), RuleSetIds.FantasyNriDefault),
                ProjectType = ProjectTypeIds.ReverseEngineering,
                RuntimeKind = ReverseEngineeringRuntimeKind0193,
                Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), "Анализ: " + snapshot.SourceItemName), 2, 180, "name"),
                PublicSummary = "Обратная инженерия предмета «" + snapshot.SourceItemName + "».",
                Status = ProjectStatusIds.Draft,
                ApprovalStatus = ProjectApprovalStatusIds.Draft,
                ProgressMode = ProjectProgressModeIds.StageBased,
                ResultStatus = ProjectResultStatusIds.Expected,
                ResultApplicationMode = ProjectResultApplicationModeIds.CreateKnowledgeLater,
                OwnerUserId = ownership.OwnerUserId,
                OwnerDisplayName = FirstNonEmpty(ownership.OwnerDisplayName, actor.Login),
                OwnerCharacterId = ownership.CharacterId,
                CreatedByUserId = actor.Id,
                UpdatedByUserId = actor.Id,
                VisibilityMode = ProjectVisibilityModeIds.OwnerOnly,
                IsPlayerVisible = true,
                CreatedOperationId = operationId,
                LastOperationId = operationId,
                LastOperationCommand = CommandNames.ProjectReverseEngineeringCreate,
                DefinitionSnapshot = snapshot,
                WorkPointsRequired = Math.Max(1, snapshot.Stages.Count),
                Revision = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _repositories.Projects.Insert(project);
            CreateReverseEngineeringChildren0193(project, evaluation, actor.Id);
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "project.created",
                "Создан проект обратной инженерии.", "Проект анализа создан.", true);
            TryPublishProjectSync(project, "reverseEngineering.created", actor.Id, context.Request.RequestId ?? string.Empty);
            return Ok("Reverse engineering project created.", ProjectReverseEngineeringResponse0193(project, admin));
        }
    }

    public ResponseEnvelope ProjectReverseEngineeringSubmit(CommandContext context)
        => MutateReverseEngineeringProject0193(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status != ProjectStatusIds.Draft && project.Status != ProjectStatusIds.RequirementsReview)
                throw new InvalidOperationException("Only a draft reverse engineering project can be submitted.");
            project.Status = ProjectStatusIds.AwaitingApproval;
            project.ApprovalStatus = ProjectApprovalStatusIds.PendingGmReview;
            project.SubmittedAtUtc = DateTime.UtcNow;
            if (_repositories.ProjectApprovals.Find(Builders<ProjectApprovalState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault() == null)
            {
                _repositories.ProjectApprovals.Insert(new ProjectApprovalState
                {
                    ProjectId = project.Id,
                    CampaignId = project.CampaignId,
                    ApprovalType = "gm_reverse_engineering_start",
                    Status = ProjectApprovalStatusIds.PendingGmReview,
                    RequestedByUserId = actor.Id,
                    PublicSummary = "Анализ ожидает решения GM.",
                    GMSummary = "Проверьте исходный предмет, риски и ручные условия.",
                    IsPlayerVisible = true
                });
            }
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "project.submitted",
                "Проект обратной инженерии отправлен GM.", "Проект отправлен GM.", true);
        }, "Reverse engineering project submitted.");

    public ResponseEnvelope ProjectReverseEngineeringList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectReverseEngineeringViewEnabled0193(admin))
            return ProjectReverseEngineeringDisabled0193(context.Request.Command);
        var filter = Builders<ProjectBaseState>.Filter.Eq(x => x.RuntimeKind, ReverseEngineeringRuntimeKind0193)
                     & Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false);
        if (!admin) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.OwnerUserId, actor.Id);
        var items = _repositories.Projects.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)ReverseEngineeringProjectPayload0193(x, admin, false))
            .ToArray();
        return Ok("Reverse engineering projects loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectReverseEngineeringGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectReverseEngineeringViewEnabled0193(admin))
            return ProjectReverseEngineeringDisabled0193(context.Request.Command);
        var project = RequireReverseEngineeringProject0193(context.Request.Payload);
        RequireOwnerOrAdmin0191(project, actor, admin);
        return Ok("Reverse engineering project loaded.", ProjectReverseEngineeringResponse0193(project, admin));
    }

    public ResponseEnvelope ProjectReverseEngineeringRequirementConfirm(CommandContext context)
        => MutateReverseEngineeringProject0193(context, true, (project, actor, _, operationId) =>
        {
            var requirementId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requirementId"), 1, 128, "requirementId");
            var requirement = _repositories.ProjectRequirements.GetById(requirementId)
                              ?? throw new KeyNotFoundException("Project requirement not found.");
            if (!string.Equals(requirement.ProjectId, project.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("Requirement belongs to another project.");
            requirement.Status = ProjectRequirementStatusIds.Satisfied;
            requirement.VerifiedByUserId = actor.Id;
            requirement.VerifiedAtUtc = DateTime.UtcNow;
            requirement.PublicNotes = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicNote"), "Условие подтверждено GM.");
            requirement.GMNotes = RequireLength(PayloadReader.GetString(context.Request.Payload, "gmNote"), 0, 1024, "gmNote");
            _repositories.ProjectRequirements.Replace(requirement);
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "requirement.confirmed",
                "Подтверждено требование: " + requirement.Name, requirement.PublicNotes, true);
        }, "Reverse engineering requirement confirmed.");

    public ResponseEnvelope ProjectReverseEngineeringApprove(CommandContext context)
        => MutateReverseEngineeringProject0193(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Reverse engineering project is not awaiting approval.");
            var open = RequiredOpenRequirements0191(project.Id).Where(x => !IsApprovalRequirement0191(x)).ToArray();
            if (open.Length > 0)
                throw new InvalidOperationException("Не выполнены обязательные условия: " + string.Join(", ", open.Select(x => x.Name)));
            project.Status = ProjectStatusIds.Approved;
            project.ApprovalStatus = ProjectApprovalStatusIds.Approved;
            project.ApprovedAtUtc = DateTime.UtcNow;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Approved, "Обратная инженерия одобрена.");
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "project.approved",
                "Проект обратной инженерии одобрен GM.", "GM одобрил анализ.", true);
        }, "Reverse engineering project approved.");

    public ResponseEnvelope ProjectReverseEngineeringReject(CommandContext context)
        => MutateReverseEngineeringProject0193(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Reverse engineering project is not awaiting approval.");
            project.Status = ProjectStatusIds.Failed;
            project.ApprovalStatus = ProjectApprovalStatusIds.Rejected;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            var reason = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicReason"), "Анализ отклонён GM."), 1, 512, "publicReason");
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Rejected, reason);
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "project.rejected", reason, reason, true);
        }, "Reverse engineering project rejected.");

    public ResponseEnvelope ProjectReverseEngineeringReserve(CommandContext context)
        => MutateReverseEngineeringProject0193(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.Approved)
                throw new InvalidOperationException("Only an approved reverse engineering project can reserve its source item.");
            ReserveReverseEngineeringInputs0193(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.ResourcesReserved;
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "resources.reserved",
                "Исходный предмет и ресурсы зарезервированы.", "Предмет зарезервирован для анализа.", true);
        }, "Reverse engineering inputs reserved.");

    public ResponseEnvelope ProjectReverseEngineeringStart(CommandContext context)
        => MutateReverseEngineeringProject0193(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.ResourcesReserved)
                throw new InvalidOperationException("Source item and resources must be reserved before analysis starts.");
            var stages = LoadReverseEngineeringStages0193(project.Id);
            if (stages.Count == 0) throw new InvalidOperationException("Reverse engineering project has no stages.");
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
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "project.started",
                "Обратная инженерия начата.", "Анализ предмета начат.", true);
        }, "Reverse engineering project started.");

    public ResponseEnvelope ProjectReverseEngineeringStageComplete(CommandContext context)
        => MutateReverseEngineeringProject0193(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Reverse engineering project is not in progress.");
            var stages = LoadReverseEngineeringStages0193(project.Id);
            var current = stages.FirstOrDefault(x => x.Id == project.CurrentStageId)
                          ?? stages.FirstOrDefault(x => x.Status == ProjectStageStatusIds.Active)
                          ?? throw new InvalidOperationException("Current analysis stage is not available.");
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
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "stage.completed",
                "Завершена стадия: " + current.Name, "Завершена стадия «" + current.Name + "».", true);
        }, "Reverse engineering stage completed.");

    public ResponseEnvelope ProjectReverseEngineeringComplete(CommandContext context)
        => MutateReverseEngineeringProject0193(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.Completed) return;
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Reverse engineering project is not in progress.");
            if (LoadReverseEngineeringStages0193(project.Id).Any(x => x.Status != ProjectStageStatusIds.Completed))
                throw new InvalidOperationException("All analysis stages must be completed first.");
            var alreadyKnown = CompleteReverseEngineeringProject0193(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.Completed;
            project.ResultStatus = ProjectResultStatusIds.Applied;
            project.ProgressPercent = 100;
            project.CompletedAtUtc = DateTime.UtcNow;
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "project.completed",
                alreadyKnown
                    ? "Анализ завершён; открытие уже было известно персонажу."
                    : "Анализ завершён; персонаж получил частное открытие.",
                alreadyKnown ? "Анализ завершён. Сведения уже были известны." : "Анализ завершён. Получено новое открытие.",
                true);
        }, "Reverse engineering project completed.");

    public ResponseEnvelope ProjectReverseEngineeringCancel(CommandContext context)
        => MutateReverseEngineeringProject0193(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status is ProjectStatusIds.InProgress or ProjectStatusIds.Completed or ProjectStatusIds.Failed or ProjectStatusIds.Cancelled)
                throw new InvalidOperationException("Reverse engineering project cannot be cancelled in its current state.");
            ReleaseReverseEngineeringReservations0193(project.Id, actor.Id, "project cancelled");
            project.Status = ProjectStatusIds.Cancelled;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "project.cancelled",
                "Проект обратной инженерии отменён.", "Предмет и ресурсы разблокированы.", true);
        }, "Reverse engineering project cancelled.");

    public ResponseEnvelope ProjectReverseEngineeringFail(CommandContext context)
        => MutateReverseEngineeringProject0193(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status is ProjectStatusIds.Completed or ProjectStatusIds.Cancelled or ProjectStatusIds.Failed)
                throw new InvalidOperationException("Completed, cancelled or failed analysis cannot fail again.");
            if (project.Status != ProjectStatusIds.InProgress)
                ReleaseReverseEngineeringReservations0193(project.Id, actor.Id, "analysis failed before start");
            else
                ApplyReverseEngineeringFailure0193(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.Failed;
            project.ResultStatus = ProjectResultStatusIds.Failed;
            AddReverseEngineeringAudit0193(project, actor.Id, operationId, "project.failed",
                "Обратная инженерия завершена неудачей.", "Предмет уничтожен, открытие не получено.", true);
        }, "Reverse engineering project failed.");

    public ResponseEnvelope ProjectReverseEngineeringAudit(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectReverseEngineeringAdminEnabled0193())
            return ProjectReverseEngineeringDisabled0193(context.Request.Command);
        var project = RequireReverseEngineeringProject0193(context.Request.Payload);
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
        return Ok("Reverse engineering audit loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    private ResponseEnvelope MutateReverseEngineeringProject0193(
        CommandContext context,
        bool adminOnly,
        Action<ProjectBaseState, UserAccount, bool, string> mutation,
        string successMessage)
    {
        var actor = adminOnly ? RequireAdmin(context) : GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectReverseEngineeringViewEnabled0193(admin))
            return ProjectReverseEngineeringDisabled0193(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (ProjectReverseEngineeringRuntimeLock0193)
        {
            var project = RequireReverseEngineeringProject0193(context.Request.Payload);
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (string.Equals(project.LastOperationId, operationId, StringComparison.Ordinal))
            {
                if (!string.Equals(project.LastOperationCommand, context.Request.Command, StringComparison.Ordinal))
                    throw new InvalidOperationException("OperationId was already used for another command.");
                return Ok(successMessage, ProjectReverseEngineeringResponse0193(project, admin, alreadyApplied: true));
            }
            var expected = PayloadReader.GetInt(context.Request.Payload, "expectedRevision")
                           ?? throw new ArgumentException("expectedRevision is required.");
            if (expected != project.Revision)
                throw new InvalidOperationException($"Project revision conflict. Reload project. current={project.Revision}; expected={expected}");
            mutation(project, actor, admin, operationId);
            SaveReverseEngineeringProject0193(project, actor.Id, operationId, context.Request.Command, expected);
            TryPublishProjectSync(project, context.Request.Command, actor.Id, context.Request.RequestId ?? string.Empty);
            if (project.Status is ProjectStatusIds.Completed or ProjectStatusIds.Failed)
                TryWriteProjectJournal(project, operationId, "Обратная инженерия: " + project.Name, actor.Id);
            return Ok(successMessage, ProjectReverseEngineeringResponse0193(project, admin));
        }
    }

    private void SaveReverseEngineeringProject0193(ProjectBaseState project, string actorId, string operationId, string command, int expectedRevision)
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
        if (result.MatchedCount == 1) return;
        project.Revision = expectedRevision;
        throw new InvalidOperationException("Project was changed by another operation. Reload and retry.");
    }

    private ProjectDefinitionSnapshot0191 BuildReverseEngineeringSnapshot0193(
        CharacterInventoryItemProfileValue source,
        bool requirePlayerVisible)
    {
        var template = FindReverseEngineeringTemplate0193(source)
                       ?? throw new KeyNotFoundException("Reverse engineering template for this item was not found.");
        if (requirePlayerVisible && !IsDefinitionPlayerVisible0191(template))
            throw new UnauthorizedAccessException("Reverse engineering template is not available to this player.");
        var sourceDefinitionId = FirstNonEmpty(source.DefinitionId, source.ItemDefinitionId);
        if (!IsDefinitionAvailable0191(sourceDefinitionId, requirePlayerVisible))
            throw new KeyNotFoundException("Source item definition is unavailable.");
        var methodId = SplitDefinitionRefs0191(ContentField0191(template, "methods")).FirstOrDefault() ?? string.Empty;
        var method = string.IsNullOrWhiteSpace(methodId)
            ? null
            : FindContentDefinition0191(methodId, TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod);
        var requirements = ParseReverseEngineeringRequirements0193(template, method);
        var inputs = requirements
            .Where(x => string.Equals(x.Kind, "resource", StringComparison.OrdinalIgnoreCase) && x.Required && x.Quantity > 0)
            .Select(x => new ProjectMaterialSnapshot0191
            {
                DefinitionId = x.DefinitionId,
                StableKey = FindDefinitionStableKey0191(x.DefinitionId),
                DisplayName = x.DisplayName,
                Quantity = x.Quantity,
                Unit = FirstNonEmpty(x.ConsumptionMode, "ед."),
                MinimumQuality = x.MinimumQualityOrRank,
                UsageMode = "consumed",
                Optional = !x.Required
            }).ToList();
        var discoveredTechnologies = SplitDefinitionRefs0191(ContentField0191(template, "discoveredTechnologies")).ToList();
        var discoveredRecipes = SplitDefinitionRefs0191(ContentField0191(template, "discoveredRecipes")).ToList();
        var discoveredBlueprints = SplitDefinitionRefs0191(ContentField0191(template, "discoveredBlueprints")).ToList();
        var sourceDefinition = FindContentDefinition0191(sourceDefinitionId);
        var snapshot = new ProjectDefinitionSnapshot0191
        {
            SourceItemInstanceId = source.ItemId,
            SourceItemDefinitionId = sourceDefinitionId,
            SourceItemStableKey = FirstNonEmpty(sourceDefinition?.StableKey, FindDefinitionStableKey0191(sourceDefinitionId)),
            SourceItemDefinitionVersion = FirstNonEmpty(sourceDefinition?.RecordVersion, sourceDefinition?.DefinitionPackVersion),
            SourceItemDefinitionRevision = sourceDefinition?.Revision ?? 0,
            SourceItemName = FirstNonEmpty(source.DisplayName, source.Name, source.SnapshotDisplayName, DefinitionDisplayName0191(sourceDefinitionId, "Предмет")),
            SourceItemQuantity = source.Quantity,
            SourceItemQuality = InventoryQuality0193(source),
            SourceItemCondition = source.Condition,
            SourceItemDurability = source.Durability,
            SourceItemMaxDurability = source.MaxDurability,
            SourceItemTags = (source.Tags ?? new List<string>()).Concat(source.SnapshotTags ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            AnalysisMethod = FirstNonEmpty(method?.DisplayName, method?.Name, ContentField0191(template, "analysisMethod"), "Лабораторный разбор"),
            SourceItemDisposition = NormalizeDisposition0193(ContentField0191(template, "sourceItemDisposition")),
            ExpectedKnowledgeTopic = FirstNonEmpty(ContentField0191(template, "knowledgeTopic"),
                discoveredTechnologies.Select(FindDefinitionStableKey0191).FirstOrDefault(),
                "reverse:" + FirstNonEmpty(sourceDefinition?.StableKey, sourceDefinitionId)),
            ExpectedKnowledgeLevel = KnowledgeLevelIds.Partial,
            ExpectedKnowledgeType = KnowledgeTypeIds.EngineeringKnowledge,
            DiscoveredTechnologyDefinitionIds = discoveredTechnologies,
            DiscoveredRecipeDefinitionIds = discoveredRecipes,
            DiscoveredBlueprintDefinitionIds = discoveredBlueprints,
            MethodDefinitionId = method?.Id ?? string.Empty,
            MethodStableKey = method?.StableKey ?? string.Empty,
            MethodVersion = FirstNonEmpty(method?.RecordVersion, method?.DefinitionPackVersion),
            MethodRevision = method?.Revision ?? 0,
            MethodName = FirstNonEmpty(method?.DisplayName, method?.Name, "Лабораторный разбор"),
            ProjectTemplateDefinitionId = template.Id,
            ProjectTemplateStableKey = template.StableKey,
            ProjectTemplateVersion = FirstNonEmpty(template.RecordVersion, template.DefinitionPackVersion),
            ProjectTemplateRevision = template.Revision,
            ProjectTemplateName = FirstNonEmpty(template.DisplayName, template.Name),
            ApprovalPolicy = ContentField0191(template, "approvalPolicy"),
            ResourceReservationPolicy = ContentField0191(template, "resourceReservationPolicy"),
            CancellationRefundPolicy = ContentField0191(template, "cancellationRefundPolicy"),
            Inputs = inputs,
            Stages = ParseStageRows0191(ContentField0191(template, "stageRows")),
            Requirements = requirements
        };
        if (snapshot.Stages.Count == 0)
            throw new InvalidOperationException("Reverse engineering template has no stages.");
        if (snapshot.SourceItemDisposition == ReverseEngineeringDispositionIds.GmDetermined)
            throw new InvalidOperationException("GMDetermined disposition requires a resolved template policy before project creation.");
        foreach (var input in snapshot.Inputs)
            if (!IsDefinitionAvailable0191(input.DefinitionId, requirePlayerVisible))
                throw new InvalidOperationException("Reverse engineering references an unavailable resource: " + input.DisplayName);
        snapshot.SnapshotChecksum = ComputeSnapshotChecksum0191(snapshot);
        return snapshot;
    }

    private List<ProjectRequirementSnapshot0191> ParseReverseEngineeringRequirements0193(
        ContentDefinitionRecord template,
        ContentDefinitionRecord? method)
    {
        var result = new List<ProjectRequirementSnapshot0191>();
        foreach (var row in ParseRows0191(ContentField0191(template, "requirementRows")))
        {
            var kind = Cell0191(row, 0, "custom_manual");
            var id = Cell0191(row, 1);
            var visible = string.IsNullOrWhiteSpace(id) || IsDefinitionAvailable0191(id, true);
            result.Add(new ProjectRequirementSnapshot0191
            {
                Kind = kind,
                DefinitionId = id,
                DisplayName = Cell0191(row, 2, DefinitionDisplayName0191(id, "Ручное условие")),
                Quantity = Math.Max(0, ParseDecimal0191(Cell0191(row, 3))),
                MinimumQualityOrRank = Cell0191(row, 4),
                Required = ParseBool0191(Cell0191(row, 5), true),
                ConsumptionMode = Cell0191(row, 6, "ед."),
                PublicExplanation = visible ? Cell0191(row, 7, "Требуется подтверждение.") : "Дополнительное условие проверит GM.",
                GMExplanation = Cell0191(row, 8),
                IsPlayerVisible = visible
            });
        }
        if (method != null)
        {
            AddReverseEngineeringReferences0193(result, method, "skill", "Навык метода", "requiredSkills", true);
            AddReverseEngineeringReferences0193(result, method, "facility", "Площадка метода", "requiredFacilities", false);
            AddReverseEngineeringReferences0193(result, method, "tool", "Инструмент метода", "requiredTools", false);
            AddReverseEngineeringReferences0193(result, method, "license", "Разрешение метода", "requiredLicenses", false);
        }
        return result.GroupBy(x => x.Kind + "|" + x.DefinitionId, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
    }

    private void AddReverseEngineeringReferences0193(
        ICollection<ProjectRequirementSnapshot0191> target,
        ContentDefinitionRecord source,
        string kind,
        string prefix,
        string field,
        bool autoCheck)
    {
        foreach (var id in SplitDefinitionRefs0191(ContentField0191(source, field)))
        {
            var visible = IsDefinitionAvailable0191(id, true);
            target.Add(new ProjectRequirementSnapshot0191
            {
                Kind = kind,
                DefinitionId = id,
                DisplayName = visible ? prefix + ": " + DefinitionDisplayName0191(id, "неизвестно") : "Дополнительное условие GM",
                Required = true,
                IsPlayerVisible = visible,
                PublicExplanation = visible
                    ? autoCheck ? "Проверяется сервером по профилю персонажа." : "Требуется подтверждение GM."
                    : "Скрытое условие проверит GM.",
                GMExplanation = prefix + ": " + DefinitionDisplayName0191(id, id)
            });
        }
    }

    private ReverseEngineeringEvaluation0193 EvaluateReverseEngineeringRequirements0193(
        ProjectDefinitionSnapshot0191 snapshot,
        CharacterOwnershipState ownership,
        CharacterInventoryItemProfileValue source)
    {
        var result = new ReverseEngineeringEvaluation0193();
        var known = KnownTopics0192(ownership.CharacterId);
        result.AlreadyKnown = TopicKnown0192(known, snapshot.ExpectedKnowledgeTopic);
        result.Requirements.Add(ReverseEngineeringRequirementLine0193.Create(
            "ownership", "Активный персонаж", ownership.IsActive && !ownership.IsArchived,
            "Персонаж активен и владеет предметом.", "Предмет должен принадлежать активному персонажу.", true, true, false));
        result.Requirements.Add(ReverseEngineeringRequirementLine0193.Create(
            "source_item", "Исходный предмет", source.Quantity > 0 && !source.IsEquipped,
            "Предмет доступен для анализа.", source.IsEquipped ? "Сначала снимите предмет." : "Предмет недоступен.", true, true, false));
        result.Requirements.Add(ReverseEngineeringRequirementLine0193.Create(
            "not_reserved", "Свободный предмет", !IsInventoryItemReserved0193(ownership.CharacterId, source.ItemId),
            "Предмет не зарезервирован.", "Предмет уже зарезервирован другим процессом.", true, true, false));
        result.Requirements.Add(ReverseEngineeringRequirementLine0193.Create(
            "not_known", "Новое открытие", !result.AlreadyKnown,
            "Открытие ещё не получено.", "Персонаж уже получил это открытие.", true, true, false));
        var skillProfile = _mongo.CharacterSkillProfiles.Find(
            Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, ownership.CharacterId)).FirstOrDefault()?.Profile;
        foreach (var requirement in snapshot.Requirements)
        {
            var manual = requirement.Kind is "facility" or "tool" or "license" or "custom_manual";
            var satisfied = !requirement.Required;
            if (requirement.Kind == "knowledge")
                satisfied = TopicKnown0192(known, requirement.DefinitionId, FindDefinitionStableKey0191(requirement.DefinitionId));
            else if (requirement.Kind == "skill")
                satisfied = skillProfile?.Skills?.Any(x =>
                    (string.Equals(x.SkillId, requirement.DefinitionId, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(x.SkillId, FindDefinitionStableKey0191(requirement.DefinitionId), StringComparison.OrdinalIgnoreCase))
                    && (x.IsLearned || x.IsUnlocked || x.Rank > 0)) == true;
            result.Requirements.Add(new ReverseEngineeringRequirementLine0193
            {
                Kind = requirement.Kind,
                DefinitionId = requirement.DefinitionId,
                Name = requirement.DisplayName,
                PublicSummary = manual ? "Требуется подтверждение GM." : satisfied ? "Требование выполнено." : "Требование пока не выполнено.",
                GMSummary = FirstNonEmpty(requirement.GMExplanation, requirement.PublicExplanation),
                Required = requirement.Required,
                Satisfied = satisfied,
                ManualGmConfirmation = manual,
                PlayerVisible = requirement.IsPlayerVisible
            });
        }
        foreach (var input in snapshot.Inputs.Where(x => !x.Optional))
        {
            var item = FindAvailableInventoryItem0191(ownership.CharacterId, input.DefinitionId, input.Quantity, input.MinimumQuality);
            result.Resources.Add(new Dictionary<string, object>
            {
                ["name"] = input.DisplayName,
                ["quantity"] = input.Quantity,
                ["unit"] = input.Unit,
                ["status"] = item == null ? "missing" : "available",
                ["statusLabel"] = item == null ? "Не хватает" : "Будет зарезервировано"
            });
        }
        result.CanSubmit = !result.AlreadyKnown
                           && result.Requirements.Where(x => x.Required && !x.ManualGmConfirmation).All(x => x.Satisfied)
                           && result.Resources.All(x => string.Equals(Convert.ToString(x["status"]), "available", StringComparison.Ordinal));
        return result;
    }

    private void CreateReverseEngineeringChildren0193(ProjectBaseState project, ReverseEngineeringEvaluation0193 evaluation, string actorId)
    {
        var snapshot = project.DefinitionSnapshot ?? throw new InvalidOperationException("Project snapshot is missing.");
        foreach (var stage in snapshot.Stages.OrderBy(x => x.Order))
        {
            _repositories.ProjectStages.Insert(new ProjectStageState
            {
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                StageType = ProjectStageTypeIds.Research,
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
                ResourceType = "reverse_engineering_input",
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

    private void ReserveReverseEngineeringInputs0193(ProjectBaseState project, string actorId, string operationId)
    {
        var snapshot = project.DefinitionSnapshot ?? throw new InvalidOperationException("Project snapshot is missing.");
        var existing = ActiveCraftReservations0191(project.Id).ToList();
        if (existing.Count > 0)
        {
            if (existing.All(x => string.Equals(x.OperationId, operationId, StringComparison.Ordinal))) return;
            throw new InvalidOperationException("Project inputs are already reserved.");
        }
        var source = FindInventoryProfileItem0193(project.OwnerCharacterId, snapshot.SourceItemInstanceId)
                     ?? throw new KeyNotFoundException("Source item not found.");
        if (source.IsEquipped) throw new InvalidOperationException("Снимите исходный предмет перед резервированием.");
        if (source.Quantity < 1) throw new InvalidOperationException("Source item is not available.");
        var competing = ActiveReverseEngineeringSourceReservation0193(project.OwnerCharacterId, source.ItemId);
        if (competing != null && !string.Equals(competing.ProjectId, project.Id, StringComparison.Ordinal))
            throw new InvalidOperationException("Предмет уже зарезервирован другим проектом.");
        if (ReservedInventoryUnits0191(project.OwnerCharacterId, source.ItemId) > 0)
            throw new InvalidOperationException("Предмет уже зарезервирован другим процессом.");

        var requirements = _repositories.ProjectResourceRequirements.Find(
            Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id)).ToList();
        var prepared = new List<(ProjectResourceRequirementState Requirement, CharacterInventoryItemProfileValue Item, int Units)>();
        foreach (var requirement in requirements)
        {
            var minimumQuality = requirement.ExtraData.TryGetValue("minimumQuality", out var quality)
                ? Convert.ToString(quality, CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
            var item = FindAvailableInventoryItem0191(project.OwnerCharacterId, requirement.ResourceId, requirement.QuantityRequired, minimumQuality)
                       ?? throw new InvalidOperationException("Not enough resource: " + requirement.DisplayName);
            prepared.Add((requirement, item, InventoryUnitsForRequirement0191(requirement.QuantityRequired)));
        }
        var written = new List<CraftingResourceReservationState>();
        try
        {
            var sourceReservation = new CraftingResourceReservationState
            {
                Id = "reverse_source_reservation_0193_" + project.Id,
                CampaignId = project.CampaignId,
                ProjectId = project.Id,
                CraftingProjectId = project.Id,
                RequirementId = string.Empty,
                CharacterId = project.OwnerCharacterId,
                ItemInstanceId = source.ItemId,
                DefinitionId = FirstNonEmpty(source.DefinitionId, source.ItemDefinitionId),
                DisplayName = snapshot.SourceItemName,
                QuantityReserved = 1,
                Unit = "предмет",
                Status = CraftingReservationStatusIds.Reserved,
                IsConsumedOnCompletion = snapshot.SourceItemDisposition != ReverseEngineeringDispositionIds.Preserved,
                IsPlayerVisible = true,
                ReservedByUserId = actorId,
                UpdatedByUserId = actorId,
                OperationId = operationId,
                ProjectRevision = project.Revision,
                PublicNotes = "Зарезервировано для анализа.",
                ExtraData = new Dictionary<string, object>
                {
                    ["inventoryUnitsReserved"] = 1,
                    ["runtimeKind"] = ReverseEngineeringRuntimeKind0193,
                    ["reservationRole"] = ReverseEngineeringReservationRole0193,
                    ["disposition"] = snapshot.SourceItemDisposition
                }
            };
            _repositories.CraftingReservations.Insert(sourceReservation);
            written.Add(sourceReservation);
            foreach (var entry in prepared)
            {
                var reservation = new CraftingResourceReservationState
                {
                    Id = "reverse_resource_reservation_0193_" + project.Id + "_" + entry.Requirement.Id,
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
                    ExtraData = new Dictionary<string, object>
                    {
                        ["inventoryUnitsReserved"] = entry.Units,
                        ["runtimeKind"] = ReverseEngineeringRuntimeKind0193,
                        ["reservationRole"] = ReverseEngineeringResourceRole0193
                    }
                };
                _repositories.CraftingReservations.Insert(reservation);
                written.Add(reservation);
                entry.Requirement.QuantityReserved = entry.Requirement.QuantityRequired;
                entry.Requirement.Status = ProjectResourceRequirementStatusIds.Reserved;
                entry.Requirement.UpdatedByUserId = actorId;
                entry.Requirement.UpdatedAtUtc = DateTime.UtcNow;
                _repositories.ProjectResourceRequirements.Replace(entry.Requirement);
            }
        }
        catch
        {
            foreach (var reservation in written)
            {
                reservation.Status = CraftingReservationStatusIds.Released;
                reservation.ReleasedAtUtc = DateTime.UtcNow;
                reservation.PublicNotes = "Резерв освобождён после ошибки операции.";
                _repositories.CraftingReservations.Replace(reservation);
            }
            foreach (var requirement in requirements)
            {
                requirement.QuantityReserved = 0;
                requirement.Status = ProjectResourceRequirementStatusIds.Needed;
                requirement.UpdatedAtUtc = DateTime.UtcNow;
                _repositories.ProjectResourceRequirements.Replace(requirement);
            }
            throw;
        }
    }

    private bool CompleteReverseEngineeringProject0193(ProjectBaseState project, string actorId, string operationId)
    {
        var snapshot = project.DefinitionSnapshot ?? throw new InvalidOperationException("Project snapshot is missing.");
        var result = _repositories.ReverseEngineeringResults.Find(
            Builders<ReverseEngineeringResultState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        if (result?.Status == ReverseEngineeringResultStatusIds.Applied) return result.KnowledgeAlreadyKnown;
        if (result == null)
        {
            result = new ReverseEngineeringResultState
            {
                Id = "reverse_result_0193_" + project.Id,
                CampaignId = project.CampaignId,
                ProjectId = project.Id,
                OwnerCharacterId = project.OwnerCharacterId,
                SourceItemDisplayName = snapshot.SourceItemName,
                SourceItemDefinitionStableKey = snapshot.SourceItemStableKey,
                SourceItemDisposition = snapshot.SourceItemDisposition,
                AnalysisOutcome = "Recovered design principles and technology evidence.",
                DiscoveredTechnologyDefinitionIds = snapshot.DiscoveredTechnologyDefinitionIds.ToList(),
                DiscoveredRecipeDefinitionIds = snapshot.DiscoveredRecipeDefinitionIds.ToList(),
                DiscoveredBlueprintDefinitionIds = snapshot.DiscoveredBlueprintDefinitionIds.ToList(),
                KnowledgeTopic = snapshot.ExpectedKnowledgeTopic,
                Confidence = ReverseEngineeringConfidenceIds.Substantial,
                UnresolvedFindings = new List<string> { "Для канонического чертежа требуется отдельное исследование." },
                PublicSummary = "Анализ «" + snapshot.SourceItemName + "» открыл сведения о технологии «"
                                + ReverseEngineeringDiscoveryName0193(snapshot) + "».",
                GMSummary = "Исходный предмет обработан по policy " + snapshot.SourceItemDisposition + ".",
                Status = ReverseEngineeringResultStatusIds.Prepared,
                CompletionOperationId = operationId,
                IsPlayerVisible = true,
                VisibilityMode = ProjectVisibilityModeIds.OwnerOnly
            };
            _repositories.ReverseEngineeringResults.Insert(result);
        }
        if (!result.SourceDispositionApplied || !result.ResourcesConsumed)
        {
            ApplyReverseEngineeringInventoryOutcome0193(project, result, actorId, operationId);
            result.SourceDispositionApplied = true;
            result.ResourcesConsumed = true;
            _repositories.ReverseEngineeringResults.Replace(result);
        }
        if (!result.KnowledgeApplied)
        {
            var write = _profileNativeWriteService.UnlockKnowledgeTopicProfileNativeAsync(
                    project.OwnerCharacterId, snapshot.ExpectedKnowledgeTopic, actorId, operationId)
                .GetAwaiter().GetResult();
            if (!write.ProfileWritten || !write.UsedProfileNative)
                throw new InvalidOperationException("Character v2 knowledge profile write failed: " + write.ErrorMessage);
            result.KnowledgeApplied = true;
            result.KnowledgeAlreadyKnown = write.AlreadyKnown;
            _repositories.ReverseEngineeringResults.Replace(result);
        }
        result.Status = ReverseEngineeringResultStatusIds.Applied;
        result.AppliedAtUtc = DateTime.UtcNow;
        result.AppliedByUserId = actorId;
        _repositories.ReverseEngineeringResults.Replace(result);
        return result.KnowledgeAlreadyKnown;
    }

    private void ApplyReverseEngineeringInventoryOutcome0193(
        ProjectBaseState project,
        ReverseEngineeringResultState result,
        string actorId,
        string operationId)
    {
        var snapshot = project.DefinitionSnapshot ?? throw new InvalidOperationException("Project snapshot is missing.");
        var reservations = ActiveCraftReservations0191(project.Id).ToList();
        var sourceReservation = reservations.FirstOrDefault(IsReverseEngineeringSourceReservation0193)
                                ?? throw new InvalidOperationException("Active source item reservation was not found.");
        var document = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, project.OwnerCharacterId))
            .FirstOrDefault() ?? throw new KeyNotFoundException("Character inventory profile not found.");
        document.Profile ??= new InventoryProfile { CharacterId = project.OwnerCharacterId, RuleSetId = project.RuleSetId };
        document.Profile.Items ??= new List<CharacterInventoryItemProfileValue>();
        var source = document.Profile.Items.FirstOrDefault(x =>
            string.Equals(x.ItemId, snapshot.SourceItemInstanceId, StringComparison.OrdinalIgnoreCase));
        if (source == null && snapshot.SourceItemDisposition != ReverseEngineeringDispositionIds.Consumed)
            throw new KeyNotFoundException("Reserved source item not found.");
        if (source != null)
        {
            switch (snapshot.SourceItemDisposition)
            {
                case ReverseEngineeringDispositionIds.Preserved:
                    source.Source = "reverse_engineering_preserved_0193";
                    source.UpdatedAtUtc = DateTime.UtcNow;
                    break;
                case ReverseEngineeringDispositionIds.Damaged:
                    source.Durability = Math.Max(0, source.Durability - Math.Max(1, source.MaxDurability / 4));
                    source.Condition = "Повреждён анализом";
                    source.Source = "reverse_engineering_damaged_0193";
                    source.UpdatedAtUtc = DateTime.UtcNow;
                    break;
                case ReverseEngineeringDispositionIds.Consumed:
                    source.Quantity -= 1;
                    if (source.Quantity <= 0) document.Profile.Items.Remove(source);
                    else
                    {
                        source.Source = "reverse_engineering_consumed_0193";
                        source.UpdatedAtUtc = DateTime.UtcNow;
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unsupported source item disposition.");
            }
        }
        foreach (var reservation in reservations.Where(x => !IsReverseEngineeringSourceReservation0193(x)))
        {
            var item = document.Profile.Items.FirstOrDefault(x =>
                string.Equals(x.ItemId, reservation.ItemInstanceId, StringComparison.OrdinalIgnoreCase))
                       ?? throw new KeyNotFoundException("Reserved analysis resource not found.");
            var units = ReservationInventoryUnits0191(reservation);
            if (item.Quantity < units) throw new InvalidOperationException("Reserved analysis resource is no longer available.");
            item.Quantity -= units;
            item.Source = "reverse_engineering_resource_consumption_0193";
            item.UpdatedAtUtc = DateTime.UtcNow;
            if (item.Quantity <= 0) document.Profile.Items.Remove(item);
        }
        var originalUpdated = document.UpdatedUtc;
        document.UpdatedUtc = DateTime.UtcNow;
        var write = _mongo.CharacterInventoryProfiles.ReplaceOne(
            Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, document.Id)
            & Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.UpdatedUtc, originalUpdated),
            document);
        if (write.MatchedCount != 1)
            throw new InvalidOperationException("Inventory changed while completing reverse engineering. Reload and retry.");
        var sync = _profileNativeWriteService.SyncLegacyInventoryFacadeAsync(
            project.OwnerCharacterId, document.Profile, actorId, operationId).GetAwaiter().GetResult();
        if (!sync.LegacyFacadeSynced)
            throw new InvalidOperationException("Inventory compatibility facade sync failed.");
        foreach (var reservation in reservations)
        {
            reservation.Status = CraftingReservationStatusIds.Consumed;
            reservation.QuantityConsumed = reservation.QuantityReserved;
            reservation.ConsumedAtUtc = DateTime.UtcNow;
            reservation.UpdatedByUserId = actorId;
            _repositories.CraftingReservations.Replace(reservation);
            if (string.IsNullOrWhiteSpace(reservation.RequirementId)) continue;
            var requirement = _repositories.ProjectResourceRequirements.GetById(reservation.RequirementId);
            if (requirement == null) continue;
            requirement.QuantityProvided = requirement.QuantityRequired;
            requirement.Status = ProjectResourceRequirementStatusIds.ConsumedManually;
            requirement.UpdatedByUserId = actorId;
            requirement.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ProjectResourceRequirements.Replace(requirement);
        }
        result.ResultPayload["inventorySource"] = "character_inventory_profiles";
        result.ResultPayload["sourceDispositionApplied"] = true;
        result.ResultPayload["resourcesConsumed"] = true;
    }

    private void ApplyReverseEngineeringFailure0193(ProjectBaseState project, string actorId, string operationId)
    {
        var snapshot = project.DefinitionSnapshot ?? throw new InvalidOperationException("Project snapshot is missing.");
        if (snapshot.SourceItemDisposition != ReverseEngineeringDispositionIds.Consumed)
            snapshot.SourceItemDisposition = ReverseEngineeringDispositionIds.Consumed;
        var transient = new ReverseEngineeringResultState
        {
            ProjectId = project.Id,
            OwnerCharacterId = project.OwnerCharacterId,
            SourceItemDisposition = ReverseEngineeringDispositionIds.Consumed
        };
        ApplyReverseEngineeringInventoryOutcome0193(project, transient, actorId, operationId);
    }

    private void ReleaseReverseEngineeringReservations0193(string projectId, string actorId, string reason)
    {
        foreach (var reservation in ActiveCraftReservations0191(projectId))
        {
            reservation.Status = CraftingReservationStatusIds.Released;
            reservation.ReleasedAtUtc = DateTime.UtcNow;
            reservation.UpdatedByUserId = actorId;
            reservation.PublicNotes = "Резерв освобождён: " + reason;
            _repositories.CraftingReservations.Replace(reservation);
            if (string.IsNullOrWhiteSpace(reservation.RequirementId)) continue;
            var requirement = _repositories.ProjectResourceRequirements.GetById(reservation.RequirementId);
            if (requirement == null) continue;
            requirement.QuantityReserved = 0;
            requirement.Status = ProjectResourceRequirementStatusIds.Needed;
            requirement.UpdatedByUserId = actorId;
            requirement.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ProjectResourceRequirements.Replace(requirement);
        }
    }

    private Dictionary<string, object> ProjectReverseEngineeringResponse0193(ProjectBaseState project, bool admin, bool alreadyApplied = false)
        => new()
        {
            ["item"] = ReverseEngineeringProjectPayload0193(project, admin, true),
            ["alreadyApplied"] = alreadyApplied
        };

    private Dictionary<string, object> ReverseEngineeringProjectPayload0193(ProjectBaseState project, bool admin, bool details)
    {
        var snapshot = project.DefinitionSnapshot ?? new ProjectDefinitionSnapshot0191();
        var sourceReservation = ActiveReverseEngineeringSourceReservation0193(project.OwnerCharacterId, snapshot.SourceItemInstanceId);
        var sourcePresent = FindInventoryProfileItem0193(project.OwnerCharacterId, snapshot.SourceItemInstanceId) != null;
        var payload = new Dictionary<string, object>
        {
            ["projectId"] = project.Id,
            ["projectType"] = ProjectTypeIds.ReverseEngineering,
            ["projectTypeLabel"] = "Обратная инженерия",
            ["name"] = project.Name,
            ["publicSummary"] = project.PublicSummary,
            ["status"] = project.Status,
            ["statusLabel"] = CraftProjectStatusLabel0191(project.Status),
            ["approvalStatus"] = project.ApprovalStatus,
            ["revision"] = project.Revision,
            ["progressPercent"] = project.ProgressPercent,
            ["currentStageName"] = project.CurrentStageName,
            ["ownerDisplayName"] = project.OwnerDisplayName,
            ["sourceItemName"] = snapshot.SourceItemName,
            ["sourceItemCondition"] = snapshot.SourceItemCondition,
            ["sourceItemDisposition"] = DispositionLabel0193(snapshot.SourceItemDisposition),
            ["sourceItemStatus"] = SourceItemStatus0193(project, sourcePresent, sourceReservation != null),
            ["methodName"] = snapshot.MethodName,
            ["templateName"] = snapshot.ProjectTemplateName,
            ["expectedDiscovery"] = ReverseEngineeringDiscoveryName0193(snapshot),
            ["knowledgeStatus"] = TopicKnown0192(KnownTopics0192(project.OwnerCharacterId), snapshot.ExpectedKnowledgeTopic) ? "Открытие получено" : "Открытие не получено",
            ["createdAtUtc"] = project.CreatedAtUtc,
            ["updatedAtUtc"] = project.UpdatedAtUtc,
            ["completedAtUtc"] = project.CompletedAtUtc.HasValue ? project.CompletedAtUtc.Value : string.Empty
        };
        if (!details) return payload;
        payload["requirements"] = _repositories.ProjectRequirements.Find(Builders<ProjectRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
            .Where(x => admin || (x.IsPlayerVisible && x.VisibilityMode != ProjectVisibilityModeIds.GmOnly && x.VisibilityMode != ProjectVisibilityModeIds.Hidden))
            .Select(x => (object)ReverseEngineeringRequirementPayload0193(x, admin)).ToArray();
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
        payload["stages"] = LoadReverseEngineeringStages0193(project.Id)
            .Where(x => admin || x.IsPlayerVisible)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.Name,
                ["status"] = x.Status,
                ["statusLabel"] = StageStatusLabel0191(x.Status),
                ["progressPercent"] = x.ProgressPercent,
                ["isCurrent"] = x.Id == project.CurrentStageId
            }).ToArray();
        var result = _repositories.ReverseEngineeringResults.Find(
            Builders<ReverseEngineeringResultState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        payload["result"] = result == null
            ? new Dictionary<string, object>()
            : ReverseEngineeringResultPayload0193(result, admin);
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
            payload["sourceItemDefinitionId"] = snapshot.SourceItemDefinitionId;
            payload["sourceItemInstanceId"] = snapshot.SourceItemInstanceId;
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

    private Dictionary<string, object> ReverseEngineeringPreviewPayload0193(
        ProjectDefinitionSnapshot0191 snapshot,
        ReverseEngineeringEvaluation0193 evaluation)
        => new()
        {
            ["sourceItemName"] = snapshot.SourceItemName,
            ["sourceItemCondition"] = snapshot.SourceItemCondition,
            ["disposition"] = DispositionLabel0193(snapshot.SourceItemDisposition),
            ["dispositionWarning"] = snapshot.SourceItemDisposition == ReverseEngineeringDispositionIds.Consumed
                ? "Внимание: исходный предмет будет уничтожен при завершении анализа."
                : "Состояние предмета изменится согласно policy анализа.",
            ["methodName"] = snapshot.MethodName,
            ["templateName"] = snapshot.ProjectTemplateName,
            ["expectedDiscovery"] = ReverseEngineeringDiscoveryName0193(snapshot),
            ["knowledgeStatus"] = evaluation.AlreadyKnown ? "Открытие уже получено" : "Открытие не получено",
            ["requirements"] = evaluation.Requirements.Where(x => x.PlayerVisible).Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.Name,
                ["status"] = x.Satisfied ? "satisfied" : x.ManualGmConfirmation ? "requires_gm" : "missing",
                ["statusLabel"] = x.Satisfied ? "Выполнено" : x.ManualGmConfirmation ? "Требует GM" : "Не выполнено",
                ["summary"] = x.PublicSummary,
                ["required"] = x.Required
            }).ToArray(),
            ["resources"] = evaluation.Resources.Cast<object>().ToArray(),
            ["canSubmit"] = evaluation.CanSubmit,
            ["alreadyKnown"] = evaluation.AlreadyKnown
        };

    private Dictionary<string, object> ReverseEngineeringResultPayload0193(ReverseEngineeringResultState result, bool admin)
    {
        var payload = new Dictionary<string, object>
        {
            ["name"] = "Открытие: " + FirstNonEmpty(
                result.DiscoveredTechnologyDefinitionIds.Select(x => DefinitionDisplayName0191(x, string.Empty)).FirstOrDefault(),
                result.KnowledgeTopic,
                "результат анализа"),
            ["summary"] = result.PublicSummary,
            ["confidence"] = ConfidenceLabel0193(result.Confidence),
            ["sourceDisposition"] = DispositionLabel0193(result.SourceItemDisposition),
            ["status"] = result.Status,
            ["createdAtUtc"] = result.CreatedAtUtc
        };
        if (admin)
        {
            payload["gmSummary"] = result.GMSummary;
            payload["technologyDefinitionIds"] = result.DiscoveredTechnologyDefinitionIds.Cast<object>().ToArray();
            payload["recipeDefinitionIds"] = result.DiscoveredRecipeDefinitionIds.Cast<object>().ToArray();
            payload["blueprintDefinitionIds"] = result.DiscoveredBlueprintDefinitionIds.Cast<object>().ToArray();
        }
        return payload;
    }

    private static Dictionary<string, object> ReverseEngineeringRequirementPayload0193(ProjectRequirementState requirement, bool admin)
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

    private Dictionary<string, object> ReverseEngineeringSourceCard0193(
        string characterId,
        CharacterInventoryItemProfileValue source)
    {
        var definitionId = FirstNonEmpty(source.DefinitionId, source.ItemDefinitionId);
        var reserved = IsInventoryItemReserved0193(characterId, source.ItemId);
        return new Dictionary<string, object>
        {
            ["itemInstanceId"] = source.ItemId,
            ["name"] = FirstNonEmpty(source.DisplayName, source.Name, source.SnapshotDisplayName, DefinitionDisplayName0191(definitionId, "Предмет")),
            ["description"] = FirstNonEmpty(source.Description, source.SnapshotDescription),
            ["condition"] = FirstNonEmpty(source.Condition, "Состояние не указано"),
            ["quality"] = InventoryQuality0193(source),
            ["durability"] = source.Durability,
            ["maxDurability"] = source.MaxDurability,
            ["isEquipped"] = source.IsEquipped,
            ["isReserved"] = reserved,
            ["availability"] = reserved ? "Зарезервировано для анализа" : source.IsEquipped ? "Сначала снимите предмет" : "Доступно для анализа"
        };
    }

    private CharacterInventoryItemProfileValue RequireReverseEngineeringSource0193(
        string characterId,
        IDictionary<string, object> payload)
    {
        var itemId = RequireLength(PayloadReader.GetString(payload, "itemInstanceId"), 1, 128, "itemInstanceId");
        var item = FindInventoryProfileItem0193(characterId, itemId)
                   ?? throw new KeyNotFoundException("Source item not found in the active character inventory.");
        if (FindReverseEngineeringTemplate0193(item) == null)
            throw new KeyNotFoundException("Reverse engineering template for this item was not found.");
        return item;
    }

    private ContentDefinitionRecord? FindReverseEngineeringTemplate0193(CharacterInventoryItemProfileValue source)
    {
        var definitionId = FirstNonEmpty(source.DefinitionId, source.ItemDefinitionId);
        var stableKey = FindDefinitionStableKey0191(definitionId);
        return LoadProjectTemplates0191().FirstOrDefault(x =>
            string.Equals(ContentField0191(x, "projectType"), "ReverseEngineering", StringComparison.OrdinalIgnoreCase)
            && SplitDefinitionRefs0191(ContentField0191(x, "sourceDefinitions")).Any(id =>
                string.Equals(id, definitionId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, stableKey, StringComparison.OrdinalIgnoreCase)));
    }

    private ProjectBaseState RequireReverseEngineeringProject0193(IDictionary<string, object> payload)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "projectId"), PayloadReader.GetString(payload, "id")), 1, 128, "projectId");
        var project = _repositories.Projects.GetById(id) ?? throw new KeyNotFoundException("Reverse engineering project not found.");
        if (!string.Equals(project.RuntimeKind, ReverseEngineeringRuntimeKind0193, StringComparison.Ordinal))
            throw new KeyNotFoundException("Reverse engineering project not found.");
        return project;
    }

    private InventoryProfile LoadInventoryProfile0193(string characterId)
        => _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId))
            .FirstOrDefault()?.Profile ?? new InventoryProfile { CharacterId = characterId };

    private CharacterInventoryItemProfileValue? FindInventoryProfileItem0193(string characterId, string itemId)
        => (LoadInventoryProfile0193(characterId).Items ?? new List<CharacterInventoryItemProfileValue>())
            .FirstOrDefault(x => string.Equals(x.ItemId, itemId, StringComparison.OrdinalIgnoreCase));

    private CraftingResourceReservationState? ActiveReverseEngineeringSourceReservation0193(string characterId, string itemId)
        => _repositories.CraftingReservations.Find(
                Builders<CraftingResourceReservationState>.Filter.Eq(x => x.CharacterId, characterId)
                & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.ItemInstanceId, itemId)
                & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.Status, CraftingReservationStatusIds.Reserved)
                & Builders<CraftingResourceReservationState>.Filter.Eq("ExtraData.runtimeKind", ReverseEngineeringRuntimeKind0193)
                & Builders<CraftingResourceReservationState>.Filter.Eq("ExtraData.reservationRole", ReverseEngineeringReservationRole0193))
            .FirstOrDefault();

    private bool IsInventoryItemReserved0193(string characterId, string itemId)
        => _repositories.CraftingReservations.Find(
                Builders<CraftingResourceReservationState>.Filter.Eq(x => x.CharacterId, characterId)
                & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.ItemInstanceId, itemId)
                & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.Status, CraftingReservationStatusIds.Reserved))
            .Any();

    private static bool IsReverseEngineeringSourceReservation0193(CraftingResourceReservationState reservation)
        => reservation.ExtraData.TryGetValue("reservationRole", out var role)
           && string.Equals(Convert.ToString(role, CultureInfo.InvariantCulture), ReverseEngineeringReservationRole0193, StringComparison.Ordinal);

    private List<ProjectStageState> LoadReverseEngineeringStages0193(string projectId)
        => _repositories.ProjectStages.Find(Builders<ProjectStageState>.Filter.Eq(x => x.ProjectId, projectId))
            .OrderBy(x => x.SortOrder).ToList();

    private void AddReverseEngineeringAudit0193(
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
        _logger.Audit($"project.reverseEngineering action={action} projectId={project.Id} actor={actorId} operationId={operationId}");
    }

    internal void EnsureInventoryItemNotReservedForReverseEngineering0193(string characterId, string itemId)
    {
        var active = _repositories.CraftingReservations.Find(
                Builders<CraftingResourceReservationState>.Filter.Eq(x => x.CharacterId, characterId)
                & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.ItemInstanceId, itemId)
                & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.Status, CraftingReservationStatusIds.Reserved)
                & Builders<CraftingResourceReservationState>.Filter.Eq("ExtraData.runtimeKind", ReverseEngineeringRuntimeKind0193))
            .Any();
        if (active)
            throw new InvalidOperationException("Предмет зарезервирован для обратной инженерии и временно недоступен для изменения.");
    }

    internal void EnsureInventoryProfileUpdateDoesNotModifyReverseEngineeringReservations0193(
        string characterId,
        IDictionary<string, object> payload)
    {
        var active = _repositories.CraftingReservations.Find(
            Builders<CraftingResourceReservationState>.Filter.Eq(x => x.CharacterId, characterId)
            & Builders<CraftingResourceReservationState>.Filter.Eq(x => x.Status, CraftingReservationStatusIds.Reserved)
            & Builders<CraftingResourceReservationState>.Filter.Eq("ExtraData.runtimeKind", ReverseEngineeringRuntimeKind0193));
        if (active.Count > 0)
            throw new InvalidOperationException("Инвентарь содержит предметы, зарезервированные для обратной инженерии. Сначала завершите или отмените проект.");
    }

    private bool ProjectReverseEngineeringViewEnabled0193(bool admin)
        => ProjectReverseEngineeringBaseEnabled0193()
           && (admin
               ? _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedReverseEngineeringAdminView))
               : _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedReverseEngineeringPlayerView)));

    private bool ProjectReverseEngineeringBaseEnabled0193()
        => _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectRuntimeV1))
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseReverseEngineeringProjectV1));

    private bool ProjectReverseEngineeringAdminEnabled0193()
        => ProjectReverseEngineeringBaseEnabled0193()
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedReverseEngineeringAdminView));

    private ResponseEnvelope ProjectReverseEngineeringDisabled0193(string command)
    {
        _logger.Admin($"project.reverseEngineering.disabled command={command}");
        return Error("Unified ReverseEngineering project runtime is disabled by feature flags.",
            ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private static string NormalizeDisposition0193(string value)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case ReverseEngineeringDispositionIds.Preserved:
                return ReverseEngineeringDispositionIds.Preserved;
            case ReverseEngineeringDispositionIds.Damaged:
                return ReverseEngineeringDispositionIds.Damaged;
            case ReverseEngineeringDispositionIds.Consumed:
            case "":
                return ReverseEngineeringDispositionIds.Consumed;
            case ReverseEngineeringDispositionIds.GmDetermined:
                return ReverseEngineeringDispositionIds.GmDetermined;
            default:
                throw new InvalidOperationException("Unknown source item disposition policy.");
        }
    }

    private static string DispositionLabel0193(string value)
    {
        switch (value)
        {
            case ReverseEngineeringDispositionIds.Preserved: return "Предмет будет сохранён";
            case ReverseEngineeringDispositionIds.Damaged: return "Предмет будет повреждён";
            case ReverseEngineeringDispositionIds.Consumed: return "Предмет будет уничтожен";
            case ReverseEngineeringDispositionIds.GmDetermined: return "Судьбу предмета определит GM";
            default: return "Судьба предмета не указана";
        }
    }

    private static string ConfidenceLabel0193(string value)
    {
        switch (value)
        {
            case ReverseEngineeringConfidenceIds.Complete: return "Полное";
            case ReverseEngineeringConfidenceIds.Substantial: return "Существенное";
            default: return "Частичное";
        }
    }

    private static string SourceItemStatus0193(ProjectBaseState project, bool present, bool reserved)
    {
        if (project.Status == ProjectStatusIds.Completed && !present) return "Уничтожен при анализе";
        if (project.Status == ProjectStatusIds.Failed && !present) return "Уничтожен при неудачном анализе";
        if (reserved) return "Зарезервировано для анализа";
        if (present) return "Доступно владельцу";
        return "Предмет отсутствует";
    }

    private string ReverseEngineeringDiscoveryName0193(ProjectDefinitionSnapshot0191 snapshot)
        => FirstNonEmpty(
            snapshot.DiscoveredTechnologyDefinitionIds.Select(x => DefinitionDisplayName0191(x, string.Empty)).FirstOrDefault(),
            snapshot.DiscoveredRecipeDefinitionIds.Select(x => DefinitionDisplayName0191(x, string.Empty)).FirstOrDefault(),
            snapshot.DiscoveredBlueprintDefinitionIds.Select(x => DefinitionDisplayName0191(x, string.Empty)).FirstOrDefault(),
            "частное инженерное открытие");

    private static string InventoryQuality0193(CharacterInventoryItemProfileValue item)
        => FirstNonEmpty(
            (item.Tags ?? new List<string>()).FirstOrDefault(x => x.StartsWith("quality:", StringComparison.OrdinalIgnoreCase))?.Substring("quality:".Length),
            (item.SnapshotTags ?? new List<string>()).FirstOrDefault(x => x.StartsWith("quality:", StringComparison.OrdinalIgnoreCase))?.Substring("quality:".Length),
            "standard");

    private sealed class ReverseEngineeringEvaluation0193
    {
        public List<ReverseEngineeringRequirementLine0193> Requirements { get; } = new();
        public List<Dictionary<string, object>> Resources { get; } = new();
        public bool AlreadyKnown { get; set; }
        public bool CanSubmit { get; set; }
    }

    private sealed class ReverseEngineeringRequirementLine0193
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

        public static ReverseEngineeringRequirementLine0193 Create(
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
                PublicSummary = satisfied ? success : failure,
                GMSummary = satisfied ? success : failure,
                Required = required,
                Satisfied = satisfied,
                ManualGmConfirmation = manual,
                PlayerVisible = playerVisible
            };
    }
}
