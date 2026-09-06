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
    private static readonly object ProjectResearchRuntimeLock0192 = new();
    private const string ResearchTheoryRuntimeKind0192 = "research_theory_0192";

    public ResponseEnvelope ProjectResearchTechnologyList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectResearchViewEnabled0192(admin)) return ProjectResearchDisabled0192(context.Request.Command);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var ownership = RequireCraftCharacter0191(characterId, actor, admin);
        var known = KnownTopics0192(characterId);
        var items = LoadResearchTechnologies0192()
            .Where(x => admin || IsDefinitionPlayerVisible0191(x))
            .Select(x => (object)ResearchTechnologyCard0192(x, known))
            .ToArray();
        return Ok("Research technologies loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectResearchPreview(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectResearchViewEnabled0192(admin)) return ProjectResearchDisabled0192(context.Request.Command);
        var technology = RequireResearchTechnology0192(context.Request.Payload);
        if (!admin && !IsDefinitionPlayerVisible0191(technology))
            throw new UnauthorizedAccessException("Technology is not available to this player.");
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var ownership = RequireCraftCharacter0191(characterId, actor, admin);
        var snapshot = BuildResearchSnapshot0192(technology, requirePlayerVisible: !admin);
        var evaluation = EvaluateResearchRequirements0192(snapshot, ownership);
        return Ok("Research requirements evaluated.", new Dictionary<string, object>
        {
            ["preview"] = ResearchPreviewPayload0192(snapshot, evaluation)
        });
    }

    public ResponseEnvelope ProjectResearchCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectResearchViewEnabled0192(admin)) return ProjectResearchDisabled0192(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (ProjectResearchRuntimeLock0192)
        {
            var existing = _repositories.Projects.Find(
                    Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedOperationId, operationId)
                    & Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedByUserId, actor.Id))
                .FirstOrDefault();
            if (existing != null)
                return Ok("Research project already created.", ProjectResearchResponse0192(existing, admin, alreadyApplied: true));

            var technology = RequireResearchTechnology0192(context.Request.Payload);
            if (!admin && !IsDefinitionPlayerVisible0191(technology))
                throw new UnauthorizedAccessException("Technology is not available to this player.");
            var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
            var ownership = RequireCraftCharacter0191(characterId, actor, admin);
            var snapshot = BuildResearchSnapshot0192(technology, requirePlayerVisible: !admin);
            var evaluation = EvaluateResearchRequirements0192(snapshot, ownership);
            if (evaluation.AlreadyKnown)
                throw new InvalidOperationException("Персонаж уже владеет этим знанием.");
            var project = new ProjectBaseState
            {
                CampaignId = FirstNonEmpty(ownership.CampaignId, PayloadReader.GetString(context.Request.Payload, "campaignId"), "default"),
                RuleSetId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "ruleSetId"), technology.RuleSetId, RuleSetIds.FantasyNriDefault),
                ProjectType = ProjectTypeIds.ResearchTheory,
                RuntimeKind = ResearchTheoryRuntimeKind0192,
                Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), "Исследование: " + snapshot.TechnologyName), 2, 180, "name"),
                PublicSummary = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicSummary"), snapshot.TechnologyPublicDescription, snapshot.TechnologyName),
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
                LastOperationCommand = CommandNames.ProjectResearchCreate,
                DefinitionSnapshot = snapshot,
                WorkPointsRequired = Math.Max(1, snapshot.Stages.Count),
                Revision = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _repositories.Projects.Insert(project);
            CreateResearchChildren0192(project, evaluation, actor.Id);
            AddResearchAudit0192(project, actor.Id, operationId, "project.created", "Создан проект исследования теории.", "Проект исследования создан.", true);
            TryPublishProjectSync(project, "research.created", actor.Id, context.Request.RequestId ?? string.Empty);
            return Ok("Research project created.", ProjectResearchResponse0192(project, admin));
        }
    }

    public ResponseEnvelope ProjectResearchSubmit(CommandContext context)
        => MutateResearchProject0192(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status != ProjectStatusIds.Draft && project.Status != ProjectStatusIds.RequirementsReview)
                throw new InvalidOperationException("Only a draft research project can be submitted.");
            project.Status = ProjectStatusIds.AwaitingApproval;
            project.ApprovalStatus = ProjectApprovalStatusIds.PendingGmReview;
            project.SubmittedAtUtc = DateTime.UtcNow;
            if (_repositories.ProjectApprovals.Find(Builders<ProjectApprovalState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault() == null)
            {
                _repositories.ProjectApprovals.Insert(new ProjectApprovalState
                {
                    ProjectId = project.Id,
                    CampaignId = project.CampaignId,
                    ApprovalType = "gm_research_start",
                    Status = ProjectApprovalStatusIds.PendingGmReview,
                    RequestedByUserId = actor.Id,
                    PublicSummary = "Исследование ожидает решения GM.",
                    GMSummary = "Проверьте автоматические и ручные требования.",
                    IsPlayerVisible = true
                });
            }
            AddResearchAudit0192(project, actor.Id, operationId, "project.submitted", "Исследование отправлено на согласование.", "Исследование отправлено GM.", true);
        }, "Research project submitted.");

    public ResponseEnvelope ProjectResearchList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectResearchViewEnabled0192(admin)) return ProjectResearchDisabled0192(context.Request.Command);
        var filter = Builders<ProjectBaseState>.Filter.Eq(x => x.RuntimeKind, ResearchTheoryRuntimeKind0192)
                     & Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false);
        if (!admin) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.OwnerUserId, actor.Id);
        var items = _repositories.Projects.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)ResearchProjectPayload0192(x, admin, false))
            .ToArray();
        return Ok("Research projects loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectResearchGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectResearchViewEnabled0192(admin)) return ProjectResearchDisabled0192(context.Request.Command);
        var project = RequireResearchProject0192(context.Request.Payload);
        RequireOwnerOrAdmin0191(project, actor, admin);
        return Ok("Research project loaded.", ProjectResearchResponse0192(project, admin));
    }

    public ResponseEnvelope ProjectResearchRequirementConfirm(CommandContext context)
        => MutateResearchProject0192(context, true, (project, actor, _, operationId) =>
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
            AddResearchAudit0192(project, actor.Id, operationId, "requirement.confirmed", "Подтверждено требование: " + requirement.Name, requirement.PublicNotes, true);
        }, "Research requirement confirmed.");

    public ResponseEnvelope ProjectResearchApprove(CommandContext context)
        => MutateResearchProject0192(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Research project is not awaiting approval.");
            var open = RequiredOpenRequirements0191(project.Id).Where(x => !IsApprovalRequirement0191(x)).ToArray();
            if (open.Length > 0)
                throw new InvalidOperationException("Не выполнены обязательные условия: " + string.Join(", ", open.Select(x => x.Name)));
            project.Status = ProjectStatusIds.Approved;
            project.ApprovalStatus = ProjectApprovalStatusIds.Approved;
            project.ApprovedAtUtc = DateTime.UtcNow;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Approved, "Исследование одобрено.");
            AddResearchAudit0192(project, actor.Id, operationId, "project.approved", "Исследование одобрено GM.", "GM одобрил исследование.", true);
        }, "Research project approved.");

    public ResponseEnvelope ProjectResearchReject(CommandContext context)
        => MutateResearchProject0192(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Research project is not awaiting approval.");
            project.Status = ProjectStatusIds.Failed;
            project.ApprovalStatus = ProjectApprovalStatusIds.Rejected;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            var reason = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicReason"), "Исследование отклонено GM."), 1, 512, "publicReason");
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Rejected, reason);
            AddResearchAudit0192(project, actor.Id, operationId, "project.rejected", reason, reason, true);
        }, "Research project rejected.");

    public ResponseEnvelope ProjectResearchReserve(CommandContext context)
        => MutateResearchProject0192(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.Approved)
                throw new InvalidOperationException("Only an approved research project can reserve resources.");
            ReserveCraftResources0191(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.ResourcesReserved;
            AddResearchAudit0192(project, actor.Id, operationId, "resources.reserved", "Исследовательские ресурсы зарезервированы.", "Ресурсы исследования зарезервированы.", true);
        }, "Research resources reserved.");

    public ResponseEnvelope ProjectResearchStart(CommandContext context)
        => MutateResearchProject0192(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.ResourcesReserved)
                throw new InvalidOperationException("Resources must be reserved before research starts.");
            var stages = LoadResearchStages0192(project.Id);
            if (stages.Count == 0) throw new InvalidOperationException("Research project has no stages.");
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
            AddResearchAudit0192(project, actor.Id, operationId, "project.started", "Исследование начато.", "Работа над исследованием началась.", true);
        }, "Research project started.");

    public ResponseEnvelope ProjectResearchStageComplete(CommandContext context)
        => MutateResearchProject0192(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Research project is not in progress.");
            var stages = LoadResearchStages0192(project.Id);
            var current = stages.FirstOrDefault(x => x.Id == project.CurrentStageId)
                          ?? stages.FirstOrDefault(x => x.Status == ProjectStageStatusIds.Active)
                          ?? throw new InvalidOperationException("Current research stage is not available.");
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
            AddResearchAudit0192(project, actor.Id, operationId, "stage.completed", "Завершена стадия: " + current.Name, "Завершена стадия «" + current.Name + "».", true);
        }, "Research stage completed.");

    public ResponseEnvelope ProjectResearchComplete(CommandContext context)
        => MutateResearchProject0192(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.Completed) return;
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Research project is not in progress.");
            if (LoadResearchStages0192(project.Id).Any(x => x.Status != ProjectStageStatusIds.Completed))
                throw new InvalidOperationException("All research stages must be completed first.");
            var alreadyKnown = CompleteResearchProject0192(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.Completed;
            project.ResultStatus = ProjectResultStatusIds.Applied;
            project.ResultApplicationMode = ProjectResultApplicationModeIds.CreateKnowledgeLater;
            project.ProgressPercent = 100;
            project.CompletedAtUtc = DateTime.UtcNow;
            AddResearchAudit0192(
                project,
                actor.Id,
                operationId,
                "project.completed",
                alreadyKnown ? "Исследование завершено; знание уже было получено ранее." : "Исследование завершено; знание открыто персонажу.",
                alreadyKnown ? "Исследование завершено. Знание уже было известно." : "Исследование завершено. Получено новое знание.",
                true);
        }, "Research project completed.");

    public ResponseEnvelope ProjectResearchCancel(CommandContext context)
        => MutateResearchProject0192(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status is ProjectStatusIds.InProgress or ProjectStatusIds.Completed or ProjectStatusIds.Failed or ProjectStatusIds.Cancelled)
                throw new InvalidOperationException("Research project cannot be cancelled in its current state.");
            ReleaseCraftReservations0191(project.Id, actor.Id, "research project cancelled");
            project.Status = ProjectStatusIds.Cancelled;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            AddResearchAudit0192(project, actor.Id, operationId, "project.cancelled", "Исследование отменено.", "Исследование отменено, резерв освобождён.", true);
        }, "Research project cancelled.");

    public ResponseEnvelope ProjectResearchFail(CommandContext context)
        => MutateResearchProject0192(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status is ProjectStatusIds.Completed or ProjectStatusIds.Cancelled)
                throw new InvalidOperationException("Completed or cancelled research cannot fail.");
            var snapshot = project.DefinitionSnapshot ?? new ProjectDefinitionSnapshot0191();
            var policy = snapshot.CancellationRefundPolicy;
            if (policy.IndexOf("consume", StringComparison.OrdinalIgnoreCase) >= 0)
                ConsumeResearchReservations0192(project, actor.Id);
            else
                ReleaseCraftReservations0191(project.Id, actor.Id, "research failed");
            project.Status = ProjectStatusIds.Failed;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            AddResearchAudit0192(project, actor.Id, operationId, "project.failed", "Исследование завершено неудачей.", "Исследование завершено неудачей.", true);
        }, "Research project failed.");

    public ResponseEnvelope ProjectResearchAudit(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectResearchAdminEnabled0192()) return ProjectResearchDisabled0192(context.Request.Command);
        var project = RequireResearchProject0192(context.Request.Payload);
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
        return Ok("Research project audit loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    private ResponseEnvelope MutateResearchProject0192(
        CommandContext context,
        bool adminOnly,
        Action<ProjectBaseState, UserAccount, bool, string> mutation,
        string successMessage)
    {
        var actor = adminOnly ? RequireAdmin(context) : GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (adminOnly && !admin) throw new UnauthorizedAccessException("Admin role is required.");
        if (!ProjectResearchViewEnabled0192(admin)) return ProjectResearchDisabled0192(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (ProjectResearchRuntimeLock0192)
        {
            var project = RequireResearchProject0192(context.Request.Payload);
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (string.Equals(project.LastOperationId, operationId, StringComparison.Ordinal))
            {
                if (!string.Equals(project.LastOperationCommand, context.Request.Command, StringComparison.Ordinal))
                    throw new InvalidOperationException("OperationId was already used for another command.");
                return Ok(successMessage, ProjectResearchResponse0192(project, admin, alreadyApplied: true));
            }
            var expected = PayloadReader.GetInt(context.Request.Payload, "expectedRevision")
                           ?? throw new ArgumentException("expectedRevision is required.");
            if (expected != project.Revision)
                throw new InvalidOperationException($"Project revision conflict. Reload project. current={project.Revision}; expected={expected}");
            mutation(project, actor, admin, operationId);
            SaveResearchProject0192(project, actor.Id, operationId, context.Request.Command, expected);
            TryPublishProjectSync(project, context.Request.Command, actor.Id, context.Request.RequestId ?? string.Empty);
            if (project.Status == ProjectStatusIds.Completed)
                TryWriteProjectJournal(project, operationId, "Завершено исследование: " + project.Name, actor.Id);
            return Ok(successMessage, ProjectResearchResponse0192(project, admin));
        }
    }

    private void SaveResearchProject0192(ProjectBaseState project, string actorId, string operationId, string command, int expectedRevision)
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

    private ProjectDefinitionSnapshot0191 BuildResearchSnapshot0192(ContentDefinitionRecord technology, bool requirePlayerVisible)
    {
        var template = LoadProjectTemplates0191()
            .FirstOrDefault(x =>
                string.Equals(ContentField0191(x, "projectType"), "ResearchTheory", StringComparison.OrdinalIgnoreCase)
                && SplitDefinitionRefs0191(ContentField0191(x, "technologies"))
                    .Any(id => DefinitionMatches0192(technology, id)))
            ?? throw new KeyNotFoundException("ResearchTheory project template for technology was not found.");
        if (requirePlayerVisible && !IsDefinitionPlayerVisible0191(template))
            throw new UnauthorizedAccessException("Research template is not available to this player.");
        var methodId = SplitDefinitionRefs0191(ContentField0191(template, "methods")).FirstOrDefault() ?? string.Empty;
        var method = string.IsNullOrWhiteSpace(methodId) ? null : FindContentDefinition0191(methodId, TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod);
        if (requirePlayerVisible && method != null && !IsDefinitionPlayerVisible0191(method))
            throw new UnauthorizedAccessException("Research method is not available to this player.");
        var requirements = ParseResearchRequirements0192(technology, template, method);
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
        var snapshot = new ProjectDefinitionSnapshot0191
        {
            TechnologyDefinitionId = technology.Id,
            TechnologyStableKey = technology.StableKey,
            TechnologyVersion = FirstNonEmpty(technology.RecordVersion, technology.DefinitionPackVersion),
            TechnologyRevision = technology.Revision,
            TechnologyName = FirstNonEmpty(technology.DisplayName, technology.Name),
            TechnologyPublicDescription = technology.PublicDescription,
            ResearchMethod = FirstNonEmpty(method == null ? string.Empty : FirstNonEmpty(method.DisplayName, method.Name), "Теоретический анализ"),
            ExpectedKnowledgeTopic = FirstNonEmpty(technology.StableKey, technology.Id),
            ExpectedKnowledgeLevel = KnowledgeLevelIds.Partial,
            ExpectedKnowledgeType = KnowledgeTypeIds.Technology,
            MethodDefinitionId = method?.Id ?? string.Empty,
            MethodStableKey = method?.StableKey ?? string.Empty,
            MethodVersion = FirstNonEmpty(method?.RecordVersion, method?.DefinitionPackVersion),
            MethodRevision = method?.Revision ?? 0,
            MethodName = FirstNonEmpty(method?.DisplayName, method?.Name, "Теоретический анализ"),
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
        if (snapshot.Stages.Count == 0) throw new InvalidOperationException("Research project template has no stages.");
        foreach (var input in snapshot.Inputs)
            if (!IsDefinitionAvailable0191(input.DefinitionId, requirePlayerVisible))
                throw new InvalidOperationException("Research references an unavailable resource: " + input.DisplayName);
        snapshot.SnapshotChecksum = ComputeSnapshotChecksum0191(snapshot);
        return snapshot;
    }

    private List<ProjectRequirementSnapshot0191> ParseResearchRequirements0192(
        ContentDefinitionRecord technology,
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
        AddResearchReferences0192(result, technology, "prerequisiteTechnology", "Предшествующая технология", "prerequisiteTechnologies", autoCheck: true);
        AddResearchReferences0192(result, technology, "knowledge", "Базовое знание", "requiredKnowledgeTypes", autoCheck: true);
        AddResearchReferences0192(result, technology, "knowledge", "Лор", "requiredLore", autoCheck: true);
        AddResearchReferences0192(result, technology, "skill", "Навык", "requiredSkills", autoCheck: true);
        AddResearchReferences0192(result, technology, "developmentNode", "Узел развития", "requiredDevelopmentNodes", autoCheck: true);
        AddResearchReferences0192(result, technology, "facility", "Исследовательская площадка", "requiredFacilities", autoCheck: false);
        AddResearchReferences0192(result, technology, "tool", "Инструмент", "requiredTools", autoCheck: false);
        AddResearchReferences0192(result, technology, "license", "Разрешение", "requiredLicenses", autoCheck: false);
        if (method != null)
        {
            AddResearchReferences0192(result, method, "skill", "Навык метода", "requiredSkills", autoCheck: true);
            AddResearchReferences0192(result, method, "facility", "Площадка метода", "requiredFacilities", autoCheck: false);
            AddResearchReferences0192(result, method, "tool", "Инструмент метода", "requiredTools", autoCheck: false);
            AddResearchReferences0192(result, method, "license", "Разрешение метода", "requiredLicenses", autoCheck: false);
        }
        return result.GroupBy(x => x.Kind + "|" + x.DefinitionId, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
    }

    private void AddResearchReferences0192(
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

    private ResearchRequirementEvaluation0192 EvaluateResearchRequirements0192(
        ProjectDefinitionSnapshot0191 snapshot,
        CharacterOwnershipState ownership)
    {
        var result = new ResearchRequirementEvaluation0192();
        var known = KnownTopics0192(ownership.CharacterId);
        result.AlreadyKnown = TopicKnown0192(known, snapshot.TechnologyDefinitionId, snapshot.TechnologyStableKey, snapshot.TechnologyName);
        result.Requirements.Add(ResearchRequirementLine0192.Create(
            "ownership", "Активный персонаж", ownership.IsActive && !ownership.IsArchived,
            "Персонаж активен и принадлежит владельцу.", "Персонаж должен быть активен.", true, true, false));
        result.Requirements.Add(ResearchRequirementLine0192.Create(
            "technology", "Доступная технология", true,
            "Технология опубликована и доступна для исследования.", string.Empty, true, true, false));
        result.Requirements.Add(ResearchRequirementLine0192.Create(
            "notKnown", "Новое знание", !result.AlreadyKnown,
            "Технология ещё не изучена.", "Персонаж уже владеет этим знанием.", true, true, false));

        var skillProfile = _mongo.CharacterSkillProfiles.Find(
            Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, ownership.CharacterId)).FirstOrDefault()?.Profile;
        var developmentProfile = _mongo.CharacterDevelopmentProfiles.Find(
            Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, ownership.CharacterId)).FirstOrDefault()?.Profile;
        foreach (var requirement in snapshot.Requirements)
        {
            var manual = requirement.Kind is "facility" or "tool" or "license" or "custom_manual";
            var satisfied = !requirement.Required;
            if (requirement.Kind is "prerequisiteTechnology" or "knowledge")
                satisfied = TopicKnown0192(known, requirement.DefinitionId, FindDefinitionStableKey0191(requirement.DefinitionId), DefinitionDisplayName0191(requirement.DefinitionId, string.Empty));
            else if (requirement.Kind == "skill")
                satisfied = skillProfile?.Skills?.Any(x =>
                    (string.Equals(x.SkillId, requirement.DefinitionId, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(x.SkillId, FindDefinitionStableKey0191(requirement.DefinitionId), StringComparison.OrdinalIgnoreCase))
                    && (x.IsLearned || x.IsUnlocked || x.Rank > 0)) == true;
            else if (requirement.Kind == "developmentNode")
                satisfied = developmentProfile?.Nodes?.Any(x =>
                    (string.Equals(x.DevelopmentNodeId, requirement.DefinitionId, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(x.DevelopmentNodeId, FindDefinitionStableKey0191(requirement.DefinitionId), StringComparison.OrdinalIgnoreCase))
                    && (x.IsPurchased || x.IsUnlocked || x.IsCompleted)) == true;
            result.Requirements.Add(new ResearchRequirementLine0192
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

    private void CreateResearchChildren0192(ProjectBaseState project, ResearchRequirementEvaluation0192 evaluation, string actorId)
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
                ResourceType = "research_input",
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

    private bool CompleteResearchProject0192(ProjectBaseState project, string actorId, string operationId)
    {
        var snapshot = project.DefinitionSnapshot ?? throw new InvalidOperationException("Project snapshot is missing.");
        var existingResult = _repositories.ResearchResults.Find(
            Builders<ResearchResultState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        if (existingResult?.Status == ResearchResultStatusIds.Applied)
            return existingResult.KnowledgeAlreadyKnown;

        var knownTopics = KnownTopics0192(project.OwnerCharacterId);
        var alreadyKnown = TopicKnown0192(
            knownTopics,
            snapshot.TechnologyDefinitionId,
            snapshot.TechnologyStableKey,
            snapshot.TechnologyName);

        if (existingResult == null)
        {
            existingResult = new ResearchResultState
            {
                Id = "research_result_0192_" + project.Id,
                CampaignId = project.CampaignId,
                ProjectId = project.Id,
                SourceProjectId = project.Id,
                ResultType = ResearchResultTypeIds.KnowledgeGrant,
                Status = ResearchResultStatusIds.Prepared,
                Title = "Изучено: " + snapshot.TechnologyName,
                PublicSummary = "Персонаж получил знание «" + snapshot.TechnologyName + "».",
                KnowledgeDefinitionId = snapshot.TechnologyDefinitionId,
                KnowledgeTopic = snapshot.ExpectedKnowledgeTopic,
                KnowledgeLevel = snapshot.ExpectedKnowledgeLevel,
                KnowledgeType = snapshot.ExpectedKnowledgeType,
                TargetEntityType = KnowledgeEntityTypeIds.Character,
                TargetEntityId = project.OwnerCharacterId,
                IsPlayerVisible = true,
                VisibilityMode = ProjectVisibilityModeIds.OwnerOnly,
                PreparedByUserId = actorId,
                CompletionOperationId = operationId,
                KnowledgeAlreadyKnown = alreadyKnown,
                ResultPayload = new Dictionary<string, object>
                {
                    ["technologyName"] = snapshot.TechnologyName,
                    ["knowledgeTopic"] = snapshot.ExpectedKnowledgeTopic,
                    ["resourcesConsumed"] = false,
                    ["profileWriteApplied"] = false
                }
            };
            _repositories.ResearchResults.Insert(existingResult);
        }

        if (!ReadBool0192(existingResult.ResultPayload, "resourcesConsumed"))
        {
            ConsumeResearchReservations0192(project, actorId);
            existingResult.ResultPayload["resourcesConsumed"] = true;
            _repositories.ResearchResults.Replace(existingResult);
        }

        if (!ReadBool0192(existingResult.ResultPayload, "profileWriteApplied"))
        {
            var profileWrite = _profileNativeWriteService.UnlockKnowledgeTopicProfileNativeAsync(
                    project.OwnerCharacterId,
                    snapshot.ExpectedKnowledgeTopic,
                    actorId,
                    operationId)
                .GetAwaiter()
                .GetResult();
            if (!profileWrite.ProfileWritten || !profileWrite.UsedProfileNative)
                throw new InvalidOperationException("Character v2 knowledge profile write failed: " + profileWrite.ErrorMessage);
            alreadyKnown = profileWrite.AlreadyKnown;
            existingResult.ResultPayload["profileWriteApplied"] = true;
            existingResult.ResultPayload["knowledgeSource"] = "character_knowledge_profiles";
            _repositories.ResearchResults.Replace(existingResult);
        }
        existingResult.Status = ResearchResultStatusIds.Applied;
        existingResult.AppliedAtUtc = DateTime.UtcNow;
        existingResult.AppliedByUserId = actorId;
        existingResult.KnowledgeAlreadyKnown = alreadyKnown;
        _repositories.ResearchResults.Replace(existingResult);
        return alreadyKnown;
    }

    private void ConsumeResearchReservations0192(ProjectBaseState project, string actorId)
    {
        var reservations = ActiveCraftReservations0191(project.Id).ToList();
        var requirements = _repositories.ProjectResourceRequirements.Find(
            Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id)).ToList();
        if (requirements.Count > 0 && reservations.Count == 0)
            throw new InvalidOperationException("Active research resource reservations were not found.");
        if (reservations.Count == 0) return;
        var document = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, project.OwnerCharacterId))
            .FirstOrDefault() ?? throw new KeyNotFoundException("Character inventory profile not found.");
        document.Profile ??= new InventoryProfile { CharacterId = project.OwnerCharacterId, RuleSetId = project.RuleSetId };
        document.Profile.Items ??= new List<CharacterInventoryItemProfileValue>();
        foreach (var group in reservations.GroupBy(x => x.ItemInstanceId, StringComparer.OrdinalIgnoreCase))
        {
            var item = document.Profile.Items.FirstOrDefault(x => string.Equals(x.ItemId, group.Key, StringComparison.OrdinalIgnoreCase))
                       ?? throw new KeyNotFoundException("Reserved research resource not found.");
            var units = group.Sum(ReservationInventoryUnits0191);
            if (item.Quantity < units) throw new InvalidOperationException("Reserved research resource is no longer available.");
            item.Quantity -= units;
            item.UpdatedAtUtc = DateTime.UtcNow;
            item.Source = "project_research_consumption_0192";
            if (item.Quantity <= 0) document.Profile.Items.Remove(item);
        }
        var originalUpdated = document.UpdatedUtc;
        document.UpdatedUtc = DateTime.UtcNow;
        var write = _mongo.CharacterInventoryProfiles.ReplaceOne(
            Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, document.Id)
            & Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.UpdatedUtc, originalUpdated),
            document);
        if (write.MatchedCount != 1)
            throw new InvalidOperationException("Inventory changed while completing research. Reload and retry.");
        foreach (var reservation in reservations)
        {
            reservation.Status = CraftingReservationStatusIds.Consumed;
            reservation.QuantityConsumed = reservation.QuantityReserved;
            reservation.ConsumedAtUtc = DateTime.UtcNow;
            reservation.UpdatedByUserId = actorId;
            reservation.ExtraData["runtimeKind"] = ResearchTheoryRuntimeKind0192;
            _repositories.CraftingReservations.Replace(reservation);
            var requirement = _repositories.ProjectResourceRequirements.GetById(reservation.RequirementId);
            if (requirement == null) continue;
            requirement.QuantityProvided = requirement.QuantityRequired;
            requirement.Status = ProjectResourceRequirementStatusIds.ConsumedManually;
            requirement.UpdatedByUserId = actorId;
            requirement.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ProjectResourceRequirements.Replace(requirement);
        }
    }

    private Dictionary<string, object> ProjectResearchResponse0192(ProjectBaseState project, bool admin, bool alreadyApplied = false)
        => new()
        {
            ["item"] = ResearchProjectPayload0192(project, admin, true),
            ["alreadyApplied"] = alreadyApplied
        };

    private Dictionary<string, object> ResearchProjectPayload0192(ProjectBaseState project, bool admin, bool details)
    {
        var snapshot = project.DefinitionSnapshot ?? new ProjectDefinitionSnapshot0191();
        var payload = new Dictionary<string, object>
        {
            ["projectId"] = project.Id,
            ["projectType"] = ProjectTypeIds.ResearchTheory,
            ["projectTypeLabel"] = "Исследование теории",
            ["name"] = project.Name,
            ["publicSummary"] = project.PublicSummary,
            ["status"] = project.Status,
            ["statusLabel"] = CraftProjectStatusLabel0191(project.Status),
            ["approvalStatus"] = project.ApprovalStatus,
            ["revision"] = project.Revision,
            ["progressPercent"] = project.ProgressPercent,
            ["currentStageName"] = project.CurrentStageName,
            ["ownerDisplayName"] = project.OwnerDisplayName,
            ["technologyName"] = snapshot.TechnologyName,
            ["technologyDescription"] = snapshot.TechnologyPublicDescription,
            ["methodName"] = snapshot.MethodName,
            ["templateName"] = snapshot.ProjectTemplateName,
            ["knowledgeStatus"] = KnowledgeStatus0192(project.OwnerCharacterId, snapshot),
            ["expectedKnowledge"] = snapshot.TechnologyName,
            ["createdAtUtc"] = project.CreatedAtUtc,
            ["updatedAtUtc"] = project.UpdatedAtUtc,
            ["completedAtUtc"] = project.CompletedAtUtc.HasValue ? project.CompletedAtUtc.Value : string.Empty
        };
        if (!details) return payload;
        payload["requirements"] = _repositories.ProjectRequirements.Find(Builders<ProjectRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
            .Where(x => admin || (x.IsPlayerVisible && x.VisibilityMode != ProjectVisibilityModeIds.GmOnly && x.VisibilityMode != ProjectVisibilityModeIds.Hidden))
            .Select(x => (object)ResearchRequirementPayload0192(x, admin)).ToArray();
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
        payload["stages"] = LoadResearchStages0192(project.Id)
            .Where(x => admin || x.IsPlayerVisible)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.Name,
                ["status"] = x.Status,
                ["statusLabel"] = StageStatusLabel0191(x.Status),
                ["progressPercent"] = x.ProgressPercent,
                ["isCurrent"] = x.Id == project.CurrentStageId
            }).ToArray();
        var result = _repositories.ResearchResults.Find(Builders<ResearchResultState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        payload["result"] = result == null ? new Dictionary<string, object>() : new Dictionary<string, object>
        {
            ["name"] = result.Title,
            ["summary"] = result.PublicSummary,
            ["status"] = result.Status,
            ["knowledgeStatus"] = result.KnowledgeAlreadyKnown ? "Уже было известно" : "Новое знание",
            ["appliedAtUtc"] = result.AppliedAtUtc.HasValue ? result.AppliedAtUtc.Value : string.Empty
        };
        if (admin)
        {
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

    private static Dictionary<string, object> ResearchRequirementPayload0192(ProjectRequirementState requirement, bool admin)
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

    private Dictionary<string, object> ResearchPreviewPayload0192(ProjectDefinitionSnapshot0191 snapshot, ResearchRequirementEvaluation0192 evaluation)
        => new()
        {
            ["technologyName"] = snapshot.TechnologyName,
            ["technologyDescription"] = snapshot.TechnologyPublicDescription,
            ["methodName"] = snapshot.MethodName,
            ["templateName"] = snapshot.ProjectTemplateName,
            ["knowledgeStatus"] = evaluation.AlreadyKnown ? "Уже изучено" : "Не изучено",
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

    private Dictionary<string, object> ResearchTechnologyCard0192(ContentDefinitionRecord technology, IReadOnlyCollection<string> known)
        => new()
        {
            ["technologyId"] = technology.Id,
            ["name"] = FirstNonEmpty(technology.DisplayName, technology.Name),
            ["description"] = technology.PublicDescription,
            ["field"] = ContentField0191(technology, "fieldCategory"),
            ["tier"] = Math.Max(0, ParseInt0191(ContentField0191(technology, "tier"))),
            ["isKnown"] = TopicKnown0192(known, technology.Id, technology.StableKey, FirstNonEmpty(technology.DisplayName, technology.Name)),
            ["knowledgeStatus"] = TopicKnown0192(known, technology.Id, technology.StableKey, FirstNonEmpty(technology.DisplayName, technology.Name))
                ? "Изучено"
                : "Доступно для исследования"
        };

    private void AddResearchAudit0192(
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
        _logger.Audit($"project.research action={action} projectId={project.Id} actor={actorId} operationId={operationId}");
    }

    private ContentDefinitionRecord RequireResearchTechnology0192(IDictionary<string, object> payload)
    {
        var id = RequireLength(PayloadReader.GetString(payload, "technologyId"), 1, 128, "technologyId");
        var record = FindContentDefinition0191(id, TechnologyRecipeBlueprintProjectDefinitionCategories.Technology)
                     ?? throw new KeyNotFoundException("Research technology not found.");
        if (record.IsArchived) throw new KeyNotFoundException("Research technology not found.");
        return record;
    }

    private ProjectBaseState RequireResearchProject0192(IDictionary<string, object> payload)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "projectId"), PayloadReader.GetString(payload, "id")), 1, 128, "projectId");
        var project = _repositories.Projects.GetById(id) ?? throw new KeyNotFoundException("Research project not found.");
        if (!string.Equals(project.RuntimeKind, ResearchTheoryRuntimeKind0192, StringComparison.Ordinal))
            throw new KeyNotFoundException("Research project not found.");
        return project;
    }

    private IEnumerable<ContentDefinitionRecord> LoadResearchTechnologies0192()
        => _mongo.ContentDefinitionRecords.Find(
            Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, TechnologyRecipeBlueprintProjectDefinitionCategories.Technology)
            & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.IsArchived, false)).ToList();

    private IReadOnlyCollection<string> KnownTopics0192(string characterId)
        => _mongo.CharacterKnowledgeProfiles.Find(
                Builders<CharacterKnowledgeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId))
            .FirstOrDefault()?.Profile?.KnownTopics ?? new List<string>();

    private static bool TopicKnown0192(IEnumerable<string> topics, params string[] candidates)
        => topics.Any(topic => candidates.Any(candidate =>
            !string.IsNullOrWhiteSpace(candidate) && string.Equals(topic, candidate, StringComparison.OrdinalIgnoreCase)));

    private string KnowledgeStatus0192(string characterId, ProjectDefinitionSnapshot0191 snapshot)
        => TopicKnown0192(KnownTopics0192(characterId), snapshot.TechnologyDefinitionId, snapshot.TechnologyStableKey, snapshot.TechnologyName)
            ? "Изучено"
            : "Не изучено";

    private static bool DefinitionMatches0192(ContentDefinitionRecord definition, string id)
        => string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase)
           || string.Equals(definition.StableKey, id, StringComparison.OrdinalIgnoreCase);

    private List<ProjectStageState> LoadResearchStages0192(string projectId)
        => _repositories.ProjectStages.Find(Builders<ProjectStageState>.Filter.Eq(x => x.ProjectId, projectId))
            .OrderBy(x => x.SortOrder).ToList();

    private bool ProjectResearchViewEnabled0192(bool admin)
        => ProjectResearchBaseEnabled0192()
           && (admin
               ? _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedResearchAdminView))
               : _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedResearchPlayerView)));

    private bool ProjectResearchBaseEnabled0192()
        => _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectRuntimeV1))
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseResearchTheoryProjectV1));

    private bool ProjectResearchAdminEnabled0192()
        => ProjectResearchBaseEnabled0192()
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedResearchAdminView));

    private ResponseEnvelope ProjectResearchDisabled0192(string command)
    {
        _logger.Admin($"project.research.disabled command={command}");
        return Error("Unified ResearchTheory project runtime is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private static bool ReadBool0192(IDictionary<string, object> values, string key)
        => values.TryGetValue(key, out var raw) && raw != null && bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var value) && value;

    private sealed class ResearchRequirementEvaluation0192
    {
        public List<ResearchRequirementLine0192> Requirements { get; } = new();
        public List<Dictionary<string, object>> Resources { get; } = new();
        public bool CanSubmit { get; set; }
        public bool AlreadyKnown { get; set; }
    }

    private sealed class ResearchRequirementLine0192
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

        public static ResearchRequirementLine0192 Create(
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
