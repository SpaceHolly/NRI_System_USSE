using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private static readonly object LimitedProductionRuntimeLock0196 = new();
    private const string LimitedProductionRuntimeKind0196 = "limited_production_0196";
    private const int LimitedProductionMaxUnits0196 = 3;

    public ResponseEnvelope ProjectLimitedProductionAvailableList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!LimitedProductionViewEnabled0196(admin))
            return LimitedProductionDisabled0196(context.Request.Command);

        var filter = Builders<PrototypeRuntimeState>.Filter.Eq(
                         x => x.ProductionApprovalStatus,
                         PrototypeProductionApprovalStatusIds.ApprovedForLimitedProduction)
                     & Builders<PrototypeRuntimeState>.Filter.Eq(x => x.IsPlayerVisible, true);
        if (!admin)
            filter &= Builders<PrototypeRuntimeState>.Filter.Eq(x => x.OwnerUserId, actor.Id);

        var items = new List<object>();
        lock (LimitedProductionRuntimeLock0196)
        {
            foreach (var prototype in _repositories.PrototypeRuntimeStates.Find(filter)
                         .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                var ownership = _repositories.CharacterOwnerships.Find(
                        Builders<CharacterOwnershipState>.Filter.Eq(
                            x => x.CharacterId,
                            prototype.OwnerCharacterId))
                    .FirstOrDefault();
                if (ownership == null || ownership.IsArchived || !ownership.IsActive)
                    continue;
                var authorization = EnsureLimitedProductionAuthorization0196(prototype);
                if (authorization.Status == LimitedProductionAuthorizationStatusIds.Revoked)
                    continue;
                items.Add(LimitedProductionCandidatePayload0196(prototype, authorization, admin));
            }
        }
        return Ok("Limited-production prototypes loaded.",
            new Dictionary<string, object> { ["items"] = items.ToArray() });
    }

    public ResponseEnvelope ProjectLimitedProductionPreview(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!LimitedProductionViewEnabled0196(admin))
            return LimitedProductionDisabled0196(context.Request.Command);
        var candidate = RequireLimitedProductionCandidate0196(context.Request.Payload, actor, admin);
        var batchSize = RequireBatchSize0196(context.Request.Payload);
        ValidateAuthorizationCapacity0196(candidate.Authorization, batchSize);
        var snapshot = BuildLimitedProductionSnapshot0196(
            candidate.Prototype,
            candidate.Authorization,
            batchSize);
        var evaluation = EvaluateCraftRequirements0191(snapshot, candidate.Ownership);
        return Ok("Limited-production preview prepared.",
            new Dictionary<string, object>
            {
                ["preview"] = LimitedProductionPreviewPayload0196(snapshot, evaluation)
            });
    }

    public ResponseEnvelope ProjectLimitedProductionCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!LimitedProductionViewEnabled0196(admin))
            return LimitedProductionDisabled0196(context.Request.Command);
        var operationId = RequireOperationId0191(context);

        lock (LimitedProductionRuntimeLock0196)
        {
            var replay = _repositories.Projects.Find(
                    Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedOperationId, operationId)
                    & Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedByUserId, actor.Id))
                .FirstOrDefault();
            if (replay != null)
                return Ok("Limited-production project already created.",
                    LimitedProductionResponse0196(replay, admin, true));

            var candidate = RequireLimitedProductionCandidate0196(context.Request.Payload, actor, admin);
            var batchSize = RequireBatchSize0196(context.Request.Payload);
            ValidateAuthorizationCapacity0196(candidate.Authorization, batchSize);
            var snapshot = BuildLimitedProductionSnapshot0196(
                candidate.Prototype,
                candidate.Authorization,
                batchSize);
            var evaluation = EvaluateCraftRequirements0191(snapshot, candidate.Ownership);
            if (!evaluation.CanSubmit)
                throw new InvalidOperationException("Обязательные условия ограниченной партии не выполнены.");

            var now = DateTime.UtcNow;
            var project = new ProjectBaseState
            {
                CampaignId = FirstNonEmpty(candidate.Ownership.CampaignId, candidate.Prototype.CampaignId, "default"),
                RuleSetId = FirstNonEmpty(
                    PayloadReader.GetString(context.Request.Payload, "ruleSetId"),
                    snapshot.LimitedProduction?.RuleSetId,
                    RuleSetIds.FantasyNriDefault),
                ProjectType = ProjectTypeIds.ProductionBatch,
                RuntimeKind = LimitedProductionRuntimeKind0196,
                Name = RequireLength(
                    FirstNonEmpty(
                        PayloadReader.GetString(context.Request.Payload, "name"),
                        "Партия: " + snapshot.BlueprintName),
                    2,
                    180,
                    "name"),
                PublicSummary = $"Ограниченная партия: {batchSize} шт. по чертежу «{snapshot.BlueprintName}».",
                Status = ProjectStatusIds.Draft,
                ApprovalStatus = ProjectApprovalStatusIds.Draft,
                ProgressMode = ProjectProgressModeIds.StageBased,
                ResultStatus = ProjectResultStatusIds.Expected,
                ResultApplicationMode = ProjectResultApplicationModeIds.GmManual,
                OwnerUserId = candidate.Ownership.OwnerUserId,
                OwnerDisplayName = FirstNonEmpty(candidate.Ownership.OwnerDisplayName, actor.Login),
                OwnerCharacterId = candidate.Ownership.CharacterId,
                CreatedByUserId = actor.Id,
                UpdatedByUserId = actor.Id,
                VisibilityMode = ProjectVisibilityModeIds.OwnerOnly,
                IsPlayerVisible = true,
                CreatedOperationId = operationId,
                LastOperationId = operationId,
                LastOperationCommand = CommandNames.ProjectLimitedProductionCreate,
                DefinitionSnapshot = snapshot,
                WorkPointsRequired = Math.Max(1, snapshot.Stages.Count),
                Revision = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ExpectedResultSummary = new Dictionary<string, object>
                {
                    ["kind"] = "limited_production_batch",
                    ["label"] = $"{batchSize} готовых предмета",
                    ["quantity"] = batchSize
                }
            };
            _repositories.Projects.Insert(project);
            CreateCraftChildren0191(project, evaluation, actor.Id);
            AddCraftAudit0191(
                project,
                actor.Id,
                operationId,
                "limited.production.created",
                "Создан проект ограниченной производственной партии.",
                "Проект ограниченной партии создан.",
                true);
            TryPublishProjectSync(project, "limited.production.created", actor.Id,
                context.Request.RequestId ?? string.Empty);
            return Ok("Limited-production project created.",
                LimitedProductionResponse0196(project, admin));
        }
    }

    public ResponseEnvelope ProjectLimitedProductionSubmit(CommandContext context)
        => MutateLimitedProduction0196(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status != ProjectStatusIds.Draft
                && project.Status != ProjectStatusIds.RequirementsReview)
                throw new InvalidOperationException("Отправить можно только черновик партии.");
            project.Status = ProjectStatusIds.AwaitingApproval;
            project.ApprovalStatus = ProjectApprovalStatusIds.PendingGmReview;
            project.SubmittedAtUtc = DateTime.UtcNow;
            if (!_repositories.ProjectApprovals.Find(
                    Builders<ProjectApprovalState>.Filter.Eq(x => x.ProjectId, project.Id)).Any())
            {
                _repositories.ProjectApprovals.Insert(new ProjectApprovalState
                {
                    ProjectId = project.Id,
                    CampaignId = project.CampaignId,
                    ApprovalType = "gm_limited_production",
                    Status = ProjectApprovalStatusIds.PendingGmReview,
                    RequestedByUserId = actor.Id,
                    PublicSummary = "Ограниченная партия ожидает решения GM.",
                    GMSummary = "Проверьте допуск прототипа, лимит партии, требования и ресурсы.",
                    IsPlayerVisible = true
                });
            }
            AddLimitedProductionAudit0196(project, actor.Id, operationId,
                "limited.production.submitted", "Проект партии отправлен GM.", true);
        }, "Limited-production project submitted.");

    public ResponseEnvelope ProjectLimitedProductionList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!LimitedProductionViewEnabled0196(admin))
            return LimitedProductionDisabled0196(context.Request.Command);
        var filter = Builders<ProjectBaseState>.Filter.Eq(x => x.RuntimeKind, LimitedProductionRuntimeKind0196)
                     & Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false);
        if (!admin)
            filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.OwnerUserId, actor.Id);
        var items = _repositories.Projects.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)LimitedProductionProjectPayload0196(x, admin, false))
            .ToArray();
        return Ok("Limited-production projects loaded.",
            new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectLimitedProductionGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!LimitedProductionViewEnabled0196(admin))
            return LimitedProductionDisabled0196(context.Request.Command);
        var project = RequireLimitedProductionProject0196(context.Request.Payload);
        RequireOwnerOrAdmin0191(project, actor, admin);
        return Ok("Limited-production project loaded.",
            LimitedProductionResponse0196(project, admin));
    }

    public ResponseEnvelope ProjectLimitedProductionRequirementConfirm(CommandContext context)
        => MutateLimitedProduction0196(context, true, (project, actor, _, operationId) =>
        {
            var requirementId = RequireLength(
                PayloadReader.GetString(context.Request.Payload, "requirementId"),
                1,
                128,
                "requirementId");
            var requirement = _repositories.ProjectRequirements.GetById(requirementId)
                              ?? throw new KeyNotFoundException("Условие проекта не найдено.");
            if (!string.Equals(requirement.ProjectId, project.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("Условие относится к другому проекту.");
            requirement.Status = ProjectRequirementStatusIds.Satisfied;
            requirement.VerifiedByUserId = actor.Id;
            requirement.VerifiedAtUtc = DateTime.UtcNow;
            requirement.PublicNotes = FirstNonEmpty(
                PayloadReader.GetString(context.Request.Payload, "publicNote"),
                "Условие подтверждено GM.");
            requirement.GMNotes = RequireLength(
                PayloadReader.GetString(context.Request.Payload, "gmNote"),
                0,
                1024,
                "gmNote");
            _repositories.ProjectRequirements.Replace(requirement);
            AddLimitedProductionAudit0196(project, actor.Id, operationId,
                "limited.production.requirement.confirmed",
                "Подтверждено условие: " + requirement.Name,
                true);
        }, "Limited-production requirement confirmed.");

    public ResponseEnvelope ProjectLimitedProductionApprove(CommandContext context)
        => MutateLimitedProduction0196(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Проект не ожидает решения GM.");
            var open = RequiredOpenRequirements0191(project.Id)
                .Where(x => !IsApprovalRequirement0191(x))
                .ToArray();
            if (open.Length > 0)
                throw new InvalidOperationException(
                    "Не выполнены обязательные условия: " + string.Join(", ", open.Select(x => x.Name)));
            var snapshot = RequireLimitedProductionSnapshot0196(project);
            var authorization = RequireAuthorization0196(snapshot.ProductionAuthorizationId);
            ValidateAuthorizationCapacity0196(authorization, snapshot.BatchSize);
            project.Status = ProjectStatusIds.Approved;
            project.ApprovalStatus = ProjectApprovalStatusIds.Approved;
            project.ApprovedAtUtc = DateTime.UtcNow;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Approved,
                "Ограниченная партия одобрена.");
            AddLimitedProductionAudit0196(project, actor.Id, operationId,
                "limited.production.approved", "GM одобрил ограниченную партию.", true);
        }, "Limited-production project approved.");

    public ResponseEnvelope ProjectLimitedProductionReject(CommandContext context)
        => MutateLimitedProduction0196(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Проект не ожидает решения GM.");
            var reason = RequireLength(
                FirstNonEmpty(
                    PayloadReader.GetString(context.Request.Payload, "publicReason"),
                    "Ограниченная партия отклонена GM."),
                1,
                512,
                "publicReason");
            project.Status = ProjectStatusIds.Failed;
            project.ApprovalStatus = ProjectApprovalStatusIds.Rejected;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Rejected, reason);
            AddLimitedProductionAudit0196(project, actor.Id, operationId,
                "limited.production.rejected", reason, true);
        }, "Limited-production project rejected.");

    public ResponseEnvelope ProjectLimitedProductionReserve(CommandContext context)
        => MutateLimitedProduction0196(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.Approved)
                throw new InvalidOperationException("Резерв доступен только одобренному проекту.");
            ReserveLimitedProductionCapacity0196(project, actor.Id, operationId);
            try
            {
                ReserveCraftResources0191(project, actor.Id, operationId);
            }
            catch
            {
                ReleaseLimitedProductionCapacity0196(project, actor.Id);
                throw;
            }
            project.Status = ProjectStatusIds.ResourcesReserved;
            AddLimitedProductionAudit0196(project, actor.Id, operationId,
                "limited.production.reserved",
                "Лимит партии и материальные ресурсы зарезервированы.",
                true);
        }, "Limited-production capacity and resources reserved.");

    public ResponseEnvelope ProjectLimitedProductionStart(CommandContext context)
        => MutateLimitedProduction0196(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.ResourcesReserved)
                throw new InvalidOperationException("Перед запуском нужно зарезервировать лимит и ресурсы.");
            var stages = LoadCraftStages0191(project.Id);
            if (stages.Count == 0)
                throw new InvalidOperationException("У проекта нет стадий.");
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
            AddLimitedProductionAudit0196(project, actor.Id, operationId,
                "limited.production.started", "Производство партии запущено.", true);
        }, "Limited-production project started.");

    public ResponseEnvelope ProjectLimitedProductionStageComplete(CommandContext context)
        => MutateLimitedProduction0196(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Производство партии не запущено.");
            var stages = LoadCraftStages0191(project.Id);
            var current = stages.FirstOrDefault(x => x.Id == project.CurrentStageId)
                          ?? stages.FirstOrDefault(x => x.Status == ProjectStageStatusIds.Active)
                          ?? throw new InvalidOperationException("Текущая стадия недоступна.");
            current.Status = ProjectStageStatusIds.Completed;
            current.ProgressPercent = 100;
            current.CompletedAtUtc = DateTime.UtcNow;
            current.UpdatedAtUtc = DateTime.UtcNow;
            current.UpdatedByUserId = actor.Id;
            _repositories.ProjectStages.Replace(current);
            var next = stages.FirstOrDefault(
                x => x.SortOrder > current.SortOrder && x.Status != ProjectStageStatusIds.Completed);
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
            project.ProgressPercent = (int)Math.Round(
                100d * project.WorkPointsDone / Math.Max(1, stages.Count));
            AddLimitedProductionAudit0196(project, actor.Id, operationId,
                "limited.production.stage.completed",
                "Завершена стадия: " + current.Name,
                true);
        }, "Limited-production stage completed.");

    public ResponseEnvelope ProjectLimitedProductionComplete(CommandContext context)
        => MutateLimitedProduction0196(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.Completed)
                return;
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Производство партии не запущено.");
            if (LoadCraftStages0191(project.Id).Any(x => x.Status != ProjectStageStatusIds.Completed))
                throw new InvalidOperationException("Сначала завершите все стадии партии.");
            CompleteLimitedProduction0196(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.Completed;
            project.ResultStatus = ProjectResultStatusIds.Applied;
            project.ResultApplicationMode = ProjectResultApplicationModeIds.CreateItemLater;
            project.ProgressPercent = 100;
            project.CompletedAtUtc = DateTime.UtcNow;
            AddLimitedProductionAudit0196(project, actor.Id, operationId,
                "limited.production.completed",
                "Партия завершена и добавлена в инвентарь.",
                true);
        }, "Limited-production project completed.",
            project => project.Status == ProjectStatusIds.Completed);

    public ResponseEnvelope ProjectLimitedProductionCancel(CommandContext context)
        => MutateLimitedProduction0196(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status is ProjectStatusIds.InProgress
                or ProjectStatusIds.Completed
                or ProjectStatusIds.Failed
                or ProjectStatusIds.Cancelled)
                throw new InvalidOperationException("Проект нельзя отменить в текущем состоянии.");
            ReleaseCraftReservations0191(project.Id, actor.Id, "limited production cancelled");
            ReleaseLimitedProductionCapacity0196(project, actor.Id);
            project.Status = ProjectStatusIds.Cancelled;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            AddLimitedProductionAudit0196(project, actor.Id, operationId,
                "limited.production.cancelled", "Проект партии отменён, резерв освобождён.", true);
        }, "Limited-production project cancelled.");

    public ResponseEnvelope ProjectLimitedProductionFail(CommandContext context)
        => MutateLimitedProduction0196(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status is ProjectStatusIds.Completed or ProjectStatusIds.Cancelled)
                throw new InvalidOperationException("Завершённый или отменённый проект нельзя завершить неудачей.");
            ReleaseCraftReservations0191(project.Id, actor.Id, "limited production failed");
            ReleaseLimitedProductionCapacity0196(project, actor.Id);
            project.Status = ProjectStatusIds.Failed;
            project.ResultStatus = ProjectResultStatusIds.Failed;
            AddLimitedProductionAudit0196(project, actor.Id, operationId,
                "limited.production.failed",
                FirstNonEmpty(
                    PayloadReader.GetString(context.Request.Payload, "publicReason"),
                    "Производство партии завершилось неудачей."),
                true);
        }, "Limited-production project failed.");

    public ResponseEnvelope ProjectLimitedProductionAudit(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LimitedProductionViewEnabled0196(true))
            return LimitedProductionDisabled0196(context.Request.Command);
        var project = RequireLimitedProductionProject0196(context.Request.Payload);
        var snapshot = RequireLimitedProductionSnapshot0196(project);
        var authorization = RequireAuthorization0196(snapshot.ProductionAuthorizationId);
        var claim = FindCapacityClaim0196(project.Id);
        var result = FindBatchResult0196(project.Id);
        return Ok("Limited-production audit loaded.", new Dictionary<string, object>
        {
            ["item"] = LimitedProductionProjectPayload0196(project, true, true),
            ["authorization"] = LimitedProductionAuthorizationAdminPayload0196(authorization),
            ["claim"] = claim == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>
                {
                    ["units"] = claim.Units,
                    ["status"] = claim.Status,
                    ["revision"] = claim.Revision
                },
            ["batchResult"] = result == null
                ? new Dictionary<string, object>()
                : ManufacturingBatchResultAdminPayload0196(result),
            ["actor"] = actor.Login
        });
    }

    private ResponseEnvelope MutateLimitedProduction0196(
        CommandContext context,
        bool adminOnly,
        Action<ProjectBaseState, UserAccount, bool, string> mutation,
        string successMessage,
        Func<ProjectBaseState, bool>? alreadyApplied = null)
    {
        var actor = adminOnly ? RequireAdmin(context) : GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (adminOnly && !admin)
            throw new UnauthorizedAccessException("Admin role is required.");
        if (!LimitedProductionViewEnabled0196(admin))
            return LimitedProductionDisabled0196(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (LimitedProductionRuntimeLock0196)
        {
            var project = RequireLimitedProductionProject0196(context.Request.Payload);
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (alreadyApplied?.Invoke(project) == true)
                return Ok(successMessage, LimitedProductionResponse0196(project, admin, true));
            if (string.Equals(project.LastOperationId, operationId, StringComparison.Ordinal))
            {
                if (!string.Equals(project.LastOperationCommand, context.Request.Command, StringComparison.Ordinal))
                    throw new InvalidOperationException("OperationId was already used for another command.");
                return Ok(successMessage, LimitedProductionResponse0196(project, admin, true));
            }
            var expected = PayloadReader.GetInt(context.Request.Payload, "expectedRevision")
                           ?? throw new ArgumentException("expectedRevision is required.");
            if (expected != project.Revision)
                throw new InvalidOperationException(
                    $"Project revision conflict. Reload project. current={project.Revision}; expected={expected}");
            mutation(project, actor, admin, operationId);
            SaveCraftProject0191(project, actor.Id, operationId, context.Request.Command, expected);
            TryPublishProjectSync(project, context.Request.Command, actor.Id,
                context.Request.RequestId ?? string.Empty);
            if (project.Status == ProjectStatusIds.Completed)
                TryWriteProjectJournal(project, operationId,
                    "Завершена ограниченная партия: " + project.Name, actor.Id);
            return Ok(successMessage, LimitedProductionResponse0196(project, admin));
        }
    }

    private LimitedProductionCandidate0196 RequireLimitedProductionCandidate0196(
        IDictionary<string, object> payload,
        UserAccount actor,
        bool admin)
    {
        var prototypeId = RequireLength(
            PayloadReader.GetString(payload, "prototypeId"), 1, 128, "prototypeId");
        var prototype = _repositories.PrototypeRuntimeStates.GetById(prototypeId)
                        ?? throw new KeyNotFoundException("Допущенный прототип не найден.");
        if (prototype.ProductionApprovalStatus
            != PrototypeProductionApprovalStatusIds.ApprovedForLimitedProduction)
            throw new InvalidOperationException("Прототип не допущен к ограниченному производству.");
        if (!admin && (!prototype.IsPlayerVisible
                       || !string.Equals(prototype.OwnerUserId, actor.Id, StringComparison.Ordinal)))
            throw new UnauthorizedAccessException("Прототип недоступен этому игроку.");
        var ownership = RequireCraftCharacter0191(prototype.OwnerCharacterId, actor, admin);
        var authorization = EnsureLimitedProductionAuthorization0196(prototype);
        return new LimitedProductionCandidate0196(prototype, ownership, authorization);
    }

    private LimitedProductionAuthorizationState EnsureLimitedProductionAuthorization0196(
        PrototypeRuntimeState prototype)
    {
        var existing = _repositories.LimitedProductionAuthorizations.Find(
                Builders<LimitedProductionAuthorizationState>.Filter.Eq(x => x.PrototypeId, prototype.Id))
            .FirstOrDefault();
        if (existing != null)
            return existing;
        if (prototype.ProductionApprovalStatus
            != PrototypeProductionApprovalStatusIds.ApprovedForLimitedProduction)
            throw new InvalidOperationException("Прототип не допущен к ограниченному производству.");
        var authorization = new LimitedProductionAuthorizationState
        {
            Id = "limited_auth_0196_" + prototype.Id,
            CampaignId = prototype.CampaignId,
            PrototypeId = prototype.Id,
            OwnerCharacterId = prototype.OwnerCharacterId,
            OwnerUserId = prototype.OwnerUserId,
            BlueprintDefinitionId = prototype.BlueprintDefinitionId,
            BlueprintStableKey = prototype.BlueprintStableKey,
            BlueprintName = prototype.BlueprintName,
            ApprovalSourceTestResultId = prototype.ProductionApprovalSourceTestResultId,
            ApprovedByUserId = prototype.ProductionApprovedByUserId,
            ApprovedAtUtc = prototype.ProductionApprovedAtUtc ?? prototype.UpdatedAtUtc,
            MaxUnits = LimitedProductionMaxUnits0196,
            Status = LimitedProductionAuthorizationStatusIds.Active,
            UpdatedByUserId = FirstNonEmpty(prototype.ProductionApprovedByUserId, "server"),
            Revision = 1
        };
        _repositories.LimitedProductionAuthorizations.Insert(authorization);
        return authorization;
    }

    private ProjectDefinitionSnapshot0191 BuildLimitedProductionSnapshot0196(
        PrototypeRuntimeState prototype,
        LimitedProductionAuthorizationState authorization,
        int batchSize)
    {
        var prototypeProject = _repositories.Projects.GetById(prototype.ProjectId)
                               ?? throw new KeyNotFoundException("Исходный проект прототипа не найден.");
        var source = prototypeProject.DefinitionSnapshot
                     ?? throw new InvalidOperationException("У прототипа отсутствует immutable definition snapshot.");
        var targetDefinitionId = FirstNonEmpty(
            source.TargetItemDefinitionId,
            prototype.TargetItemDefinitionId,
            source.Outputs.FirstOrDefault()?.DefinitionId);
        if (string.IsNullOrWhiteSpace(targetDefinitionId))
            throw new InvalidOperationException("В snapshot прототипа не определён выпускаемый предмет.");
        var recipe = FindContentDefinition0191(
            source.RecipeDefinitionId,
            TechnologyRecipeBlueprintProjectDefinitionCategories.Recipe);
        var method = FindContentDefinition0191(
            source.MethodDefinitionId,
            TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod);
        var recipeInputs = recipe == null
            ? new List<ProjectMaterialSnapshot0191>()
            : ParseMaterialRows0191(ContentField0191(recipe, "inputRows"));
        var perUnitInputs = (recipeInputs.Count > 0 ? recipeInputs : source.Inputs)
            .Select(CloneMaterial0196)
            .ToList();
        if (perUnitInputs.Count == 0)
            throw new InvalidOperationException("В snapshot прототипа отсутствуют производственные материалы.");
        var scaledInputs = perUnitInputs.Select(x => ScaleMaterial0196(x, batchSize)).ToList();
        var outputDefinition = FindAnyDefinition0191(targetDefinitionId);
        var template = LoadProjectTemplates0191()
            .Where(x => string.Equals(
                ContentField0191(x, "projectType"),
                "LimitedProduction",
                StringComparison.OrdinalIgnoreCase))
            .Where(x =>
            {
                var blueprints = SplitDefinitionRefs0191(ContentField0191(x, "blueprints"));
                var recipes = SplitDefinitionRefs0191(ContentField0191(x, "recipes"));
                return (!blueprints.Any() && !recipes.Any())
                       || blueprints.Contains(source.BlueprintDefinitionId, StringComparer.OrdinalIgnoreCase)
                       || recipes.Contains(source.RecipeDefinitionId, StringComparer.OrdinalIgnoreCase);
            })
            .FirstOrDefault()
            ?? throw new KeyNotFoundException(
                "ProjectTemplate LimitedProduction для этого чертежа не найден.");
        var stages = ParseStageRows0191(ContentField0191(template, "stageRows"));
        if (stages.Count == 0)
            throw new InvalidOperationException("ProjectTemplate LimitedProduction не содержит стадий.");
        var requirements = recipe == null
            ? source.Requirements.Select(CloneRequirement0196).ToList()
            : ParseTemplateRequirements0191(template, recipe, method);

        var snapshot = new ProjectDefinitionSnapshot0191
        {
            BlueprintDefinitionId = FirstNonEmpty(source.BlueprintDefinitionId, prototype.BlueprintDefinitionId),
            BlueprintStableKey = FirstNonEmpty(source.BlueprintStableKey, prototype.BlueprintStableKey),
            BlueprintVersion = source.BlueprintVersion,
            BlueprintRevision = source.BlueprintRevision,
            BlueprintName = FirstNonEmpty(source.BlueprintName, prototype.BlueprintName, prototype.DisplayName),
            BlueprintPublicDescription = source.BlueprintPublicDescription,
            BlueprintKind = source.BlueprintKind,
            TargetItemDefinitionId = targetDefinitionId,
            TargetItemStableKey = FirstNonEmpty(source.TargetItemStableKey, FindDefinitionStableKey0191(targetDefinitionId)),
            TargetItemName = FirstNonEmpty(source.TargetItemName, outputDefinition.Name),
            TargetItemPublicDescription = FirstNonEmpty(
                source.TargetItemPublicDescription,
                outputDefinition.Description),
            RecipeDefinitionId = source.RecipeDefinitionId,
            RecipeStableKey = source.RecipeStableKey,
            RecipeVersion = source.RecipeVersion,
            RecipeRevision = source.RecipeRevision,
            RecipeName = source.RecipeName,
            RecipePublicDescription = source.RecipePublicDescription,
            MethodDefinitionId = source.MethodDefinitionId,
            MethodStableKey = source.MethodStableKey,
            MethodVersion = source.MethodVersion,
            MethodRevision = source.MethodRevision,
            MethodName = source.MethodName,
            ProjectTemplateDefinitionId = template.Id,
            ProjectTemplateStableKey = template.StableKey,
            ProjectTemplateVersion = FirstNonEmpty(
                template.RecordVersion,
                template.DefinitionPackVersion),
            ProjectTemplateRevision = template.Revision,
            ProjectTemplateName = FirstNonEmpty(template.DisplayName, template.Name),
            ApprovalPolicy = ContentField0191(template, "approvalPolicy"),
            ResourceReservationPolicy = ContentField0191(
                template,
                "resourceReservationPolicy"),
            CancellationRefundPolicy = ContentField0191(
                template,
                "cancellationRefundPolicy"),
            EstimatedDurationMinutes = Math.Max(
                1,
                ParseInt0191(ContentField0191(template, "estimatedDurationMinutes"))),
            Inputs = scaledInputs,
            Outputs = new List<ProjectMaterialSnapshot0191>
            {
                new()
                {
                    DefinitionId = targetDefinitionId,
                    StableKey = FindDefinitionStableKey0191(targetDefinitionId),
                    DisplayName = FirstNonEmpty(source.TargetItemName, outputDefinition.Name, "Готовое изделие"),
                    Quantity = batchSize,
                    Unit = "шт.",
                    UsageMode = "create_inventory_instances"
                }
            },
            Stages = stages,
            Requirements = requirements,
            LimitedProduction = new LimitedProductionSnapshot0196
            {
                PrototypeId = prototype.Id,
                ProductionAuthorizationId = authorization.Id,
                ProductionAuthorizationRevision = authorization.Revision,
                BatchSize = batchSize,
                MaxUnits = authorization.MaxUnits,
                RemainingUnitsAtCreation = authorization.MaxUnits
                                           - authorization.ReservedUnits
                                           - authorization.ProducedUnits,
                OwnerCharacterId = authorization.OwnerCharacterId,
                RuleSetId = prototypeProject.RuleSetId,
                PerUnitInputs = perUnitInputs,
                ScaledInputs = scaledInputs,
                Warning = "Ограниченная партия, не серийное производство."
            }
        };
        snapshot.SnapshotChecksum = ComputeSnapshotChecksum0191(snapshot);
        return snapshot;
    }

    private void ReserveLimitedProductionCapacity0196(
        ProjectBaseState project,
        string actorId,
        string operationId)
    {
        var existing = FindCapacityClaim0196(project.Id);
        if (existing != null)
        {
            if (existing.Status == LimitedProductionClaimStatusIds.Reserved
                && existing.ReservationOperationId == operationId)
                return;
            throw new InvalidOperationException("Лимит этой партии уже был обработан.");
        }
        var snapshot = RequireLimitedProductionSnapshot0196(project);
        var authorization = RequireAuthorization0196(snapshot.ProductionAuthorizationId);
        ValidateAuthorizationCapacity0196(authorization, snapshot.BatchSize);

        var capacityExpression = new BsonDocument("$expr",
            new BsonDocument("$lte", new BsonArray
            {
                new BsonDocument("$add", new BsonArray
                {
                    "$ReservedUnits",
                    "$ProducedUnits",
                    snapshot.BatchSize
                }),
                "$MaxUnits"
            }));
        var filter = Builders<LimitedProductionAuthorizationState>.Filter.Eq(x => x.Id, authorization.Id)
                     & Builders<LimitedProductionAuthorizationState>.Filter.Eq(x => x.Revision, authorization.Revision)
                     & Builders<LimitedProductionAuthorizationState>.Filter.Eq(
                         x => x.Status,
                         LimitedProductionAuthorizationStatusIds.Active)
                     & new BsonDocumentFilterDefinition<LimitedProductionAuthorizationState>(capacityExpression);
        var update = Builders<LimitedProductionAuthorizationState>.Update
            .Inc(x => x.ReservedUnits, snapshot.BatchSize)
            .Inc(x => x.Revision, 1)
            .Set(x => x.UpdatedAtUtc, DateTime.UtcNow)
            .Set(x => x.UpdatedByUserId, actorId);
        var updated = _mongo.LimitedProductionAuthorizations.FindOneAndUpdate(
            filter,
            update,
            new FindOneAndUpdateOptions<LimitedProductionAuthorizationState>
            {
                ReturnDocument = ReturnDocument.After
            });
        if (updated == null)
            throw new InvalidOperationException(
                "Лимит ограниченного производства изменился или исчерпан. Обновите проект.");
        _repositories.LimitedProductionCapacityClaims.Insert(
            new LimitedProductionCapacityClaimState
            {
                Id = "limited_claim_0196_" + project.Id,
                CampaignId = project.CampaignId,
                ProjectId = project.Id,
                AuthorizationId = authorization.Id,
                Units = snapshot.BatchSize,
                Status = LimitedProductionClaimStatusIds.Reserved,
                ReservationOperationId = operationId,
                ReservedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = actorId,
                Revision = 1
            });
    }

    private void ReleaseLimitedProductionCapacity0196(ProjectBaseState project, string actorId)
    {
        var claim = FindCapacityClaim0196(project.Id);
        if (claim == null || claim.Status != LimitedProductionClaimStatusIds.Reserved)
            return;
        var authorization = RequireAuthorization0196(claim.AuthorizationId);
        var result = _mongo.LimitedProductionAuthorizations.UpdateOne(
            Builders<LimitedProductionAuthorizationState>.Filter.Eq(x => x.Id, authorization.Id)
            & Builders<LimitedProductionAuthorizationState>.Filter.Gte(x => x.ReservedUnits, claim.Units),
            Builders<LimitedProductionAuthorizationState>.Update
                .Inc(x => x.ReservedUnits, -claim.Units)
                .Inc(x => x.Revision, 1)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow)
                .Set(x => x.UpdatedByUserId, actorId));
        if (result.MatchedCount != 1)
            throw new InvalidOperationException("Не удалось освободить лимит партии.");
        claim.Status = LimitedProductionClaimStatusIds.Released;
        claim.ReleasedAtUtc = DateTime.UtcNow;
        claim.UpdatedByUserId = actorId;
        claim.Revision++;
        _repositories.LimitedProductionCapacityClaims.Replace(claim);
    }

    private void CompleteLimitedProduction0196(
        ProjectBaseState project,
        string actorId,
        string operationId)
    {
        var existingResult = FindBatchResult0196(project.Id);
        if (existingResult != null)
            return;
        var snapshot = project.DefinitionSnapshot
                       ?? throw new InvalidOperationException("Project snapshot is missing.");
        var limited = RequireLimitedProductionSnapshot0196(project);
        var claim = FindCapacityClaim0196(project.Id)
                    ?? throw new InvalidOperationException("Резерв лимита партии не найден.");
        if (claim.Status != LimitedProductionClaimStatusIds.Reserved)
            throw new InvalidOperationException("Лимит партии не находится в зарезервированном состоянии.");

        var document = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(
                    x => x.CharacterId,
                    project.OwnerCharacterId))
            .FirstOrDefault()
            ?? throw new KeyNotFoundException("Профиль инвентаря персонажа не найден.");
        document.Profile ??= new InventoryProfile
        {
            CharacterId = project.OwnerCharacterId,
            RuleSetId = project.RuleSetId
        };
        document.Profile.Items ??= new List<CharacterInventoryItemProfileValue>();
        var outputDefinition = FindAnyDefinition0191(snapshot.TargetItemDefinitionId);
        var outputIds = Enumerable.Range(1, limited.BatchSize)
            .Select(index => $"limited0196_{project.Id}_{index}")
            .ToList();
        var outputsAlreadyExist = outputIds.All(
            id => document.Profile.Items.Any(x => x.ItemId == id));
        if (!outputsAlreadyExist && outputIds.Any(
                id => document.Profile.Items.Any(x => x.ItemId == id)))
            throw new InvalidOperationException("Обнаружен неполный materialization партии.");

        if (!outputsAlreadyExist)
        {
            var reservations = ActiveCraftReservations0191(project.Id).ToList();
            if (reservations.Count == 0)
                throw new InvalidOperationException("Активные резервы материалов не найдены.");
            foreach (var group in reservations.GroupBy(x => x.ItemInstanceId, StringComparer.OrdinalIgnoreCase))
            {
                var item = document.Profile.Items.FirstOrDefault(
                               x => string.Equals(x.ItemId, group.Key, StringComparison.OrdinalIgnoreCase))
                           ?? throw new KeyNotFoundException("Зарезервированный предмет инвентаря не найден.");
                var units = group.Sum(ReservationInventoryUnits0191);
                if (item.Quantity < units)
                    throw new InvalidOperationException("Зарезервированного количества больше нет.");
                item.Quantity -= units;
                item.UpdatedAtUtc = DateTime.UtcNow;
                item.Source = "limited_production_consumption_0196";
                if (item.Quantity <= 0)
                    document.Profile.Items.Remove(item);
            }
            for (var outputIndex = 0; outputIndex < outputIds.Count; outputIndex++)
            {
                var outputId = outputIds[outputIndex];
                document.Profile.Items.Add(new CharacterInventoryItemProfileValue
                {
                    ItemId = outputId,
                    DefinitionId = snapshot.TargetItemDefinitionId,
                    ItemDefinitionId = snapshot.TargetItemDefinitionId,
                    DefinitionCategory = outputDefinition.Category,
                    SnapshotDisplayName = FirstNonEmpty(snapshot.TargetItemName, outputDefinition.Name),
                    SnapshotCategory = outputDefinition.Category,
                    SnapshotDescription = FirstNonEmpty(
                        snapshot.TargetItemPublicDescription,
                        outputDefinition.Description),
                    SnapshotTags = outputDefinition.Tags.ToList(),
                    Name = FirstNonEmpty(snapshot.TargetItemName, outputDefinition.Name),
                    DisplayName = FirstNonEmpty(snapshot.TargetItemName, outputDefinition.Name),
                    Category = outputDefinition.Category,
                    Description = FirstNonEmpty(
                        snapshot.TargetItemPublicDescription,
                        outputDefinition.Description),
                    Quantity = 1,
                    Durability = 100,
                    MaxDurability = 100,
                    Condition = "Новый",
                    IsPlayerVisible = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    Source = "limited_production_result_0196",
                    Tags = new List<string>
                    {
                        "manufactured",
                        "limited_batch",
                        "project:" + project.Id,
                        "batch-result:manufacturing_batch_0196_" + project.Id,
                        "blueprint:" + snapshot.BlueprintStableKey,
                        "blueprint-revision:" + snapshot.BlueprintRevision,
                        "authorization:" + limited.ProductionAuthorizationId,
                        "unit-index:" + (outputIndex + 1)
                    }
                });
            }
            var originalUpdated = document.UpdatedUtc;
            document.UpdatedUtc = DateTime.UtcNow;
            var inventoryWrite = _mongo.CharacterInventoryProfiles.ReplaceOne(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, document.Id)
                & Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.UpdatedUtc, originalUpdated),
                document);
            if (inventoryWrite.MatchedCount != 1)
                throw new InvalidOperationException(
                    "Инвентарь изменился во время завершения партии. Обновите проект.");
        }

        foreach (var reservation in ActiveCraftReservations0191(project.Id))
        {
            reservation.Status = CraftingReservationStatusIds.Consumed;
            reservation.QuantityConsumed = reservation.QuantityReserved;
            reservation.ConsumedAtUtc = DateTime.UtcNow;
            reservation.UpdatedByUserId = actorId;
            _repositories.CraftingReservations.Replace(reservation);
            var requirement = _repositories.ProjectResourceRequirements.GetById(reservation.RequirementId);
            if (requirement == null)
                continue;
            requirement.QuantityProvided = requirement.QuantityRequired;
            requirement.Status = ProjectResourceRequirementStatusIds.ConsumedManually;
            requirement.UpdatedByUserId = actorId;
            requirement.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ProjectResourceRequirements.Replace(requirement);
        }

        var authorization = RequireAuthorization0196(claim.AuthorizationId);
        var authorizationWrite = _mongo.LimitedProductionAuthorizations.UpdateOne(
            Builders<LimitedProductionAuthorizationState>.Filter.Eq(x => x.Id, authorization.Id)
            & Builders<LimitedProductionAuthorizationState>.Filter.Gte(x => x.ReservedUnits, claim.Units),
            Builders<LimitedProductionAuthorizationState>.Update
                .Inc(x => x.ReservedUnits, -claim.Units)
                .Inc(x => x.ProducedUnits, claim.Units)
                .Inc(x => x.Revision, 1)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow)
                .Set(x => x.UpdatedByUserId, actorId)
                .Set(x => x.Status,
                    authorization.ProducedUnits + claim.Units >= authorization.MaxUnits
                        ? LimitedProductionAuthorizationStatusIds.Exhausted
                        : LimitedProductionAuthorizationStatusIds.Active));
        if (authorizationWrite.MatchedCount != 1)
            throw new InvalidOperationException("Не удалось применить расход лимита партии.");
        claim.Status = LimitedProductionClaimStatusIds.Produced;
        claim.CompletionOperationId = operationId;
        claim.ProducedAtUtc = DateTime.UtcNow;
        claim.UpdatedByUserId = actorId;
        claim.Revision++;
        _repositories.LimitedProductionCapacityClaims.Replace(claim);

        _repositories.ManufacturingBatchResults.Insert(new ManufacturingBatchResultState
        {
            Id = "manufacturing_batch_0196_" + project.Id,
            CampaignId = project.CampaignId,
            ProjectId = project.Id,
            AuthorizationId = claim.AuthorizationId,
            OwnerCharacterId = project.OwnerCharacterId,
            BlueprintStableKey = snapshot.BlueprintStableKey,
            BlueprintVersion = snapshot.BlueprintVersion,
            BlueprintRevision = snapshot.BlueprintRevision,
            BlueprintName = snapshot.BlueprintName,
            BatchSize = limited.BatchSize,
            OutputItemInstanceIds = outputIds,
            ResourcesConsumed = limited.ScaledInputs.Select(CloneMaterial0196).ToList(),
            PublicSummary = $"Изготовлено {limited.BatchSize} шт.: {snapshot.TargetItemName}.",
            GMSummary = "Материалы списаны exactly-once; предметы созданы с deterministic provenance.",
            CompletedAtUtc = DateTime.UtcNow,
            CompletedByUserId = actorId,
            CompletionOperationId = operationId,
            Revision = 1
        });
    }

    private void ValidateAuthorizationCapacity0196(
        LimitedProductionAuthorizationState authorization,
        int batchSize)
    {
        if (authorization.Status != LimitedProductionAuthorizationStatusIds.Active)
            throw new InvalidOperationException("Допуск ограниченного производства не активен.");
        var remaining = authorization.MaxUnits - authorization.ReservedUnits - authorization.ProducedUnits;
        if (batchSize > remaining)
            throw new InvalidOperationException(
                $"Лимит допуска недостаточен: доступно {Math.Max(0, remaining)}, запрошено {batchSize}.");
    }

    private static int RequireBatchSize0196(IDictionary<string, object> payload)
    {
        var batchSize = PayloadReader.GetInt(payload, "batchSize") ?? 0;
        if (batchSize < 1 || batchSize > LimitedProductionMaxUnits0196)
            throw new ArgumentException("Размер ограниченной партии должен быть от 1 до 3.");
        return batchSize;
    }

    private ProjectBaseState RequireLimitedProductionProject0196(
        IDictionary<string, object> payload)
    {
        var id = RequireLength(
            FirstNonEmpty(
                PayloadReader.GetString(payload, "projectId"),
                PayloadReader.GetString(payload, "id")),
            1,
            128,
            "projectId");
        var project = _repositories.Projects.GetById(id)
                      ?? throw new KeyNotFoundException("Проект ограниченной партии не найден.");
        if (!string.Equals(project.RuntimeKind, LimitedProductionRuntimeKind0196, StringComparison.Ordinal))
            throw new KeyNotFoundException("Проект ограниченной партии не найден.");
        return project;
    }

    private static LimitedProductionSnapshot0196 RequireLimitedProductionSnapshot0196(
        ProjectBaseState project)
        => project.DefinitionSnapshot?.LimitedProduction
           ?? throw new InvalidOperationException("Limited-production snapshot is missing.");

    private LimitedProductionAuthorizationState RequireAuthorization0196(string id)
        => _repositories.LimitedProductionAuthorizations.GetById(id)
           ?? throw new KeyNotFoundException("Допуск ограниченного производства не найден.");

    private LimitedProductionCapacityClaimState? FindCapacityClaim0196(string projectId)
        => _repositories.LimitedProductionCapacityClaims.Find(
                Builders<LimitedProductionCapacityClaimState>.Filter.Eq(x => x.ProjectId, projectId))
            .FirstOrDefault();

    private ManufacturingBatchResultState? FindBatchResult0196(string projectId)
        => _repositories.ManufacturingBatchResults.Find(
                Builders<ManufacturingBatchResultState>.Filter.Eq(x => x.ProjectId, projectId))
            .FirstOrDefault();

    private Dictionary<string, object> LimitedProductionResponse0196(
        ProjectBaseState project,
        bool admin,
        bool alreadyApplied = false)
        => new()
        {
            ["item"] = LimitedProductionProjectPayload0196(project, admin, true),
            ["alreadyApplied"] = alreadyApplied
        };

    private Dictionary<string, object> LimitedProductionProjectPayload0196(
        ProjectBaseState project,
        bool admin,
        bool details)
    {
        var snapshot = project.DefinitionSnapshot ?? new ProjectDefinitionSnapshot0191();
        var limited = snapshot.LimitedProduction ?? new LimitedProductionSnapshot0196();
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
            ["ownerCharacterDisplayName"] = _repositories.CharacterOwnerships.Find(
                    Builders<CharacterOwnershipState>.Filter.Eq(
                        x => x.CharacterId,
                        project.OwnerCharacterId))
                .FirstOrDefault()?.CharacterDisplayName ?? "Персонаж не найден",
            ["projectTypeLabel"] = "Ограниченная партия",
            ["blueprintName"] = snapshot.BlueprintName,
            ["targetItemName"] = snapshot.TargetItemName,
            ["recipeName"] = snapshot.RecipeName,
            ["methodName"] = snapshot.MethodName,
            ["batchSize"] = limited.BatchSize,
            ["maxUnits"] = limited.MaxUnits,
            ["warning"] = limited.Warning,
            ["expectedOutput"] = snapshot.Outputs.Select(MaterialPayload0191).Cast<object>().ToArray(),
            ["createdAtUtc"] = project.CreatedAtUtc,
            ["updatedAtUtc"] = project.UpdatedAtUtc,
            ["completedAtUtc"] = project.CompletedAtUtc.HasValue
                ? project.CompletedAtUtc.Value
                : string.Empty
        };
        if (!details)
            return payload;
        payload["requirements"] = _repositories.ProjectRequirements.Find(
                Builders<ProjectRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
            .Where(x => admin || (x.IsPlayerVisible
                                  && x.VisibilityMode != ProjectVisibilityModeIds.GmOnly
                                  && x.VisibilityMode != ProjectVisibilityModeIds.Hidden))
            .Select(x => (object)CraftRequirementPayload0191(x, admin))
            .ToArray();
        payload["resources"] = _repositories.ProjectResourceRequirements.Find(
                Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
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
            })
            .ToArray();
        payload["stages"] = LoadCraftStages0191(project.Id)
            .Where(x => admin || x.IsPlayerVisible)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.Name,
                ["status"] = x.Status,
                ["statusLabel"] = StageStatusLabel0191(x.Status),
                ["progressPercent"] = x.ProgressPercent,
                ["isCurrent"] = x.Id == project.CurrentStageId
            })
            .ToArray();
        var result = FindBatchResult0196(project.Id);
        payload["result"] = result == null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>
            {
                ["name"] = result.BlueprintName,
                ["quantity"] = result.BatchSize,
                ["status"] = result.Status,
                ["summary"] = result.PublicSummary,
                ["completedAtUtc"] = result.CompletedAtUtc
            };
        if (admin)
        {
            var authorization = string.IsNullOrWhiteSpace(limited.ProductionAuthorizationId)
                ? null
                : _repositories.LimitedProductionAuthorizations.GetById(
                    limited.ProductionAuthorizationId);
            payload["campaignId"] = project.CampaignId;
            payload["ownerCharacterId"] = project.OwnerCharacterId;
            payload["gmSummary"] = project.GMSummary;
            payload["gmNotes"] = project.GMNotes;
            payload["snapshotChecksum"] = snapshot.SnapshotChecksum;
            payload["authorization"] = authorization == null
                ? new Dictionary<string, object>()
                : LimitedProductionAuthorizationAdminPayload0196(authorization);
            payload["audit"] = _repositories.ProjectAuditEntries.Find(
                    Builders<ProjectAuditEntryState>.Filter.Eq(x => x.ProjectId, project.Id))
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => (object)new Dictionary<string, object>
                {
                    ["action"] = x.ActionType,
                    ["summary"] = x.Summary,
                    ["actorDisplayName"] = AccountDisplayName0191(x.ActorUserId),
                    ["createdAtUtc"] = x.CreatedAtUtc
                })
                .ToArray();
        }
        return payload;
    }

    private Dictionary<string, object> LimitedProductionPreviewPayload0196(
        ProjectDefinitionSnapshot0191 snapshot,
        CraftRequirementEvaluation0191 evaluation)
    {
        var limited = snapshot.LimitedProduction ?? new LimitedProductionSnapshot0196();
        return new Dictionary<string, object>
        {
            ["blueprintName"] = snapshot.BlueprintName,
            ["recipeName"] = snapshot.RecipeName,
            ["methodName"] = snapshot.MethodName,
            ["batchSize"] = limited.BatchSize,
            ["maxUnits"] = limited.MaxUnits,
            ["remainingUnits"] = limited.RemainingUnitsAtCreation,
            ["warning"] = limited.Warning,
            ["resources"] = evaluation.Resources.Cast<object>().ToArray(),
            ["outputs"] = snapshot.Outputs.Select(MaterialPayload0191).Cast<object>().ToArray(),
            ["requirements"] = evaluation.Requirements.Where(x => x.PlayerVisible)
                .Select(x => (object)new Dictionary<string, object>
                {
                    ["name"] = x.Name,
                    ["status"] = x.Satisfied
                        ? "satisfied"
                        : x.ManualGmConfirmation
                            ? "requires_gm"
                            : "missing",
                    ["summary"] = x.PublicSummary,
                    ["required"] = x.Required
                })
                .ToArray(),
            ["canSubmit"] = evaluation.CanSubmit
        };
    }

    private Dictionary<string, object> LimitedProductionCandidatePayload0196(
        PrototypeRuntimeState prototype,
        LimitedProductionAuthorizationState authorization,
        bool admin)
    {
        var payload = new Dictionary<string, object>
        {
            ["prototypeId"] = prototype.Id,
            ["name"] = prototype.DisplayName,
            ["blueprintName"] = FirstNonEmpty(prototype.BlueprintName, prototype.DisplayName),
            ["remainingUnits"] = Math.Max(
                0,
                authorization.MaxUnits - authorization.ReservedUnits - authorization.ProducedUnits),
            ["maxUnits"] = authorization.MaxUnits,
            ["producedUnits"] = authorization.ProducedUnits,
            ["status"] = authorization.Status,
            ["warning"] = "Ограниченная партия, максимум 3 единицы."
        };
        if (admin)
        {
            payload["ownerDisplayName"] = AccountDisplayName0191(prototype.OwnerUserId);
            payload["reservedUnits"] = authorization.ReservedUnits;
            payload["authorizationRevision"] = authorization.Revision;
        }
        return payload;
    }

    private static Dictionary<string, object> LimitedProductionAuthorizationAdminPayload0196(
        LimitedProductionAuthorizationState authorization)
        => new()
        {
            ["prototypeId"] = authorization.PrototypeId,
            ["blueprintName"] = authorization.BlueprintName,
            ["maxUnits"] = authorization.MaxUnits,
            ["reservedUnits"] = authorization.ReservedUnits,
            ["producedUnits"] = authorization.ProducedUnits,
            ["remainingUnits"] = Math.Max(
                0,
                authorization.MaxUnits - authorization.ReservedUnits - authorization.ProducedUnits),
            ["status"] = authorization.Status,
            ["revision"] = authorization.Revision,
            ["approvedAtUtc"] = authorization.ApprovedAtUtc
        };

    private static Dictionary<string, object> ManufacturingBatchResultAdminPayload0196(
        ManufacturingBatchResultState result)
        => new()
        {
            ["blueprintName"] = result.BlueprintName,
            ["batchSize"] = result.BatchSize,
            ["outputCount"] = result.OutputItemInstanceIds.Count,
            ["status"] = result.Status,
            ["publicSummary"] = result.PublicSummary,
            ["completedAtUtc"] = result.CompletedAtUtc,
            ["revision"] = result.Revision
        };

    private void AddLimitedProductionAudit0196(
        ProjectBaseState project,
        string actorId,
        string operationId,
        string action,
        string summary,
        bool playerVisible)
    {
        AddCraftAudit0191(project, actorId, operationId, action, summary, summary, playerVisible);
        _logger.Audit(
            $"project.production.limited action={action} projectId={project.Id} actor={actorId} operationId={operationId}");
    }

    private bool LimitedProductionViewEnabled0196(bool admin)
        => _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectRuntimeV1))
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseLimitedProductionProjectV1))
           && (admin
               ? _featureFlags.IsEnabled(
                   nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedLimitedProductionAdminView))
               : _featureFlags.IsEnabled(
                   nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedLimitedProductionPlayerView)));

    private ResponseEnvelope LimitedProductionDisabled0196(string command)
    {
        _logger.Admin($"project.production.limited.disabled command={command}");
        return Error(
            "Limited-production project runtime is disabled by feature flags.",
            ResponseStatus.Forbidden,
            ErrorCode.Forbidden);
    }

    private static ProjectMaterialSnapshot0191 CloneMaterial0196(
        ProjectMaterialSnapshot0191 source)
        => new()
        {
            DefinitionId = source.DefinitionId,
            StableKey = source.StableKey,
            DisplayName = source.DisplayName,
            Quantity = source.Quantity,
            Unit = source.Unit,
            MinimumQuality = source.MinimumQuality,
            UsageMode = source.UsageMode,
            Optional = source.Optional
        };

    private static ProjectMaterialSnapshot0191 ScaleMaterial0196(
        ProjectMaterialSnapshot0191 source,
        int batchSize)
    {
        var clone = CloneMaterial0196(source);
        clone.Quantity *= batchSize;
        return clone;
    }

    private static ProjectRequirementSnapshot0191 CloneRequirement0196(
        ProjectRequirementSnapshot0191 source)
        => new()
        {
            Kind = source.Kind,
            DefinitionId = source.DefinitionId,
            DisplayName = source.DisplayName,
            Quantity = source.Quantity,
            MinimumQualityOrRank = source.MinimumQualityOrRank,
            Required = source.Required,
            ConsumptionMode = source.ConsumptionMode,
            PublicExplanation = source.PublicExplanation
        };

    private sealed class LimitedProductionCandidate0196
    {
        public LimitedProductionCandidate0196(
            PrototypeRuntimeState prototype,
            CharacterOwnershipState ownership,
            LimitedProductionAuthorizationState authorization)
        {
            Prototype = prototype;
            Ownership = ownership;
            Authorization = authorization;
        }

        public PrototypeRuntimeState Prototype { get; }
        public CharacterOwnershipState Ownership { get; }
        public LimitedProductionAuthorizationState Authorization { get; }
    }
}
