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
    private static readonly object PrototypeRepairRuntimeLock0195 = new();
    private const string PrototypeRepairRuntimeKind0195 = "prototype_repair_0195";

    public ResponseEnvelope ProjectPrototypeRepairAvailableList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectPrototypeRepairViewEnabled0195(admin))
            return ProjectPrototypeRepairDisabled0195(context.Request.Command);

        var filter = Builders<PrototypeRuntimeState>.Filter.Eq(x => x.IsPlayerVisible, true);
        if (!admin)
        {
            var activeCharacterId = _repositories.Presence.Find(
                    Builders<SessionUserState>.Filter.Eq(x => x.UserId, actor.Id))
                .FirstOrDefault()?.ActiveCharacterId ?? string.Empty;
            var activeCharacterIds = string.IsNullOrWhiteSpace(activeCharacterId)
                ? Array.Empty<string>()
                : _repositories.CharacterOwnerships.Find(
                        Builders<CharacterOwnershipState>.Filter.Eq(x => x.OwnerUserId, actor.Id)
                        & Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, activeCharacterId)
                        & Builders<CharacterOwnershipState>.Filter.Eq(x => x.IsArchived, false))
                    .Select(x => x.CharacterId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            filter &= Builders<PrototypeRuntimeState>.Filter.Eq(x => x.OwnerUserId, actor.Id)
                      & Builders<PrototypeRuntimeState>.Filter.In(
                          x => x.OwnerCharacterId, activeCharacterIds);
        }
        var items = new List<object>();
        foreach (var prototype in _repositories.PrototypeRuntimeStates.Find(filter)
                     .Where(x => x.ProductionApprovalStatus == PrototypeProductionApprovalStatusIds.NotProductionApproved))
        {
            var defects = _repositories.PrototypeDefectInstances.Find(
                Builders<PrototypeDefectInstanceState>.Filter.Eq(x => x.PrototypeId, prototype.Id)
                & Builders<PrototypeDefectInstanceState>.Filter.Eq(x => x.Status, PrototypeDefectStatusIds.Open));
            foreach (var defect in defects.Where(x => admin || x.IsPlayerVisible))
                items.Add(PrototypeRepairCandidatePayload0195(prototype, defect, admin));
        }
        return Ok("Repairable prototypes loaded.", new Dictionary<string, object> { ["items"] = items.ToArray() });
    }

    public ResponseEnvelope ProjectPrototypeRepairPreview(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectPrototypeRepairViewEnabled0195(admin))
            return ProjectPrototypeRepairDisabled0195(context.Request.Command);
        var candidate = RequirePrototypeRepairCandidate0195(context.Request.Payload, actor, admin, true);
        var snapshot = BuildPrototypeRepairSnapshot0195(candidate.Prototype, candidate.Defect, !admin);
        var evaluation = EvaluatePrototypeRepairRequirements0195(snapshot, candidate.Ownership);
        return Ok("Prototype repair requirements evaluated.", new Dictionary<string, object>
        {
            ["preview"] = PrototypeRepairPreviewPayload0195(snapshot, evaluation, admin)
        });
    }

    public ResponseEnvelope ProjectPrototypeRepairCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectPrototypeRepairViewEnabled0195(admin))
            return ProjectPrototypeRepairDisabled0195(context.Request.Command);
        var operationId = RequireOperationId0191(context);

        lock (PrototypeRepairRuntimeLock0195)
        {
            var replay = _repositories.Projects.Find(
                    Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedOperationId, operationId)
                    & Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedByUserId, actor.Id))
                .FirstOrDefault();
            if (replay != null)
                return Ok("Prototype repair project already created.",
                    PrototypeRepairResponse0195(replay, admin, true));

            var candidate = RequirePrototypeRepairCandidate0195(context.Request.Payload, actor, admin, true);
            var snapshot = BuildPrototypeRepairSnapshot0195(candidate.Prototype, candidate.Defect, !admin);
            var evaluation = EvaluatePrototypeRepairRequirements0195(snapshot, candidate.Ownership);
            if (!evaluation.CanSubmit)
                throw new InvalidOperationException("Требования ремонта не выполнены.");

            var project = new ProjectBaseState
            {
                CampaignId = FirstNonEmpty(candidate.Ownership.CampaignId, candidate.Prototype.CampaignId, "default"),
                RuleSetId = FirstNonEmpty(
                    PayloadReader.GetString(context.Request.Payload, "ruleSetId"),
                    RuleSetIds.FantasyNriDefault),
                ProjectType = ProjectTypeIds.Repair,
                RuntimeKind = PrototypeRepairRuntimeKind0195,
                Name = RequireLength(
                    FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"),
                        "Ремонт: " + candidate.Prototype.DisplayName), 2, 180, "name"),
                PublicSummary = "Устранение дефекта «" + candidate.Defect.Name + "» с обязательным повторным испытанием.",
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
                LastOperationCommand = CommandNames.ProjectPrototypeRepairCreate,
                DefinitionSnapshot = snapshot,
                WorkPointsRequired = Math.Max(1, snapshot.Stages.Count),
                Revision = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                ExpectedResultSummary = new Dictionary<string, object>
                {
                    ["kind"] = "prototype_repair",
                    ["label"] = "Прототип отремонтирован и проверен",
                    ["outcome"] = PrototypeProjectOutcomeIds.PrototypeRepairedAndApproved
                }
            };
            _repositories.Projects.Insert(project);
            CreatePrototypeRepairChildren0195(project, evaluation, actor.Id);
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.repair.created",
                "Создан проект ремонта прототипа.", "Подготовлен проект ремонта прототипа.", true);
            TryPublishProjectSync(project, "prototype.repair.created", actor.Id,
                context.Request.RequestId ?? string.Empty);
            return Ok("Prototype repair project created.", PrototypeRepairResponse0195(project, admin));
        }
    }

    public ResponseEnvelope ProjectPrototypeRepairSubmit(CommandContext context)
        => MutatePrototypeRepairProject0195(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status != ProjectStatusIds.Draft && project.Status != ProjectStatusIds.RequirementsReview)
                throw new InvalidOperationException("Отправить можно только черновик ремонта.");
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
                    ApprovalType = "gm_prototype_repair",
                    Status = ProjectApprovalStatusIds.PendingGmReview,
                    RequestedByUserId = actor.Id,
                    PublicSummary = "Ремонт прототипа ожидает решения GM.",
                    GMSummary = "Проверьте дефект, repair snapshot, ресурсы и повторное испытание.",
                    IsPlayerVisible = true
                });
            }
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.repair.submitted",
                "Проект ремонта отправлен GM.", "Проект ремонта отправлен GM.", true);
        }, "Prototype repair project submitted.");

    public ResponseEnvelope ProjectPrototypeRepairList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectPrototypeRepairViewEnabled0195(admin))
            return ProjectPrototypeRepairDisabled0195(context.Request.Command);
        var filter = Builders<ProjectBaseState>.Filter.Eq(x => x.RuntimeKind, PrototypeRepairRuntimeKind0195)
                     & Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false);
        if (!admin) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.OwnerUserId, actor.Id);
        var items = _repositories.Projects.Find(filter).OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)PrototypeRepairProjectPayload0195(x, admin, false)).ToArray();
        return Ok("Prototype repair projects loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectPrototypeRepairGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!ProjectPrototypeRepairViewEnabled0195(admin))
            return ProjectPrototypeRepairDisabled0195(context.Request.Command);
        var project = RequirePrototypeRepairProject0195(context.Request.Payload);
        RequireOwnerOrAdmin0191(project, actor, admin);
        return Ok("Prototype repair project loaded.", PrototypeRepairResponse0195(project, admin));
    }

    public ResponseEnvelope ProjectPrototypeRepairRequirementConfirm(CommandContext context)
        => MutatePrototypeRepairProject0195(context, true, (project, actor, _, operationId) =>
        {
            var requirementId = RequireLength(
                PayloadReader.GetString(context.Request.Payload, "requirementId"), 1, 128, "requirementId");
            var requirement = _repositories.ProjectRequirements.GetById(requirementId)
                              ?? throw new KeyNotFoundException("Условие проекта не найдено.");
            if (!string.Equals(requirement.ProjectId, project.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("Условие относится к другому проекту.");
            requirement.Status = ProjectRequirementStatusIds.Satisfied;
            requirement.VerifiedByUserId = actor.Id;
            requirement.VerifiedAtUtc = DateTime.UtcNow;
            requirement.PublicNotes = FirstNonEmpty(
                PayloadReader.GetString(context.Request.Payload, "publicNote"), "Условие подтверждено GM.");
            requirement.GMNotes = RequireLength(
                PayloadReader.GetString(context.Request.Payload, "gmNote"), 0, 1024, "gmNote");
            _repositories.ProjectRequirements.Replace(requirement);
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.repair.requirement.confirmed",
                "Подтверждено условие: " + requirement.Name, requirement.PublicNotes, true);
        }, "Prototype repair requirement confirmed.");

    public ResponseEnvelope ProjectPrototypeRepairApprove(CommandContext context)
        => MutatePrototypeRepairProject0195(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Проект не ожидает решения GM.");
            var open = RequiredOpenRequirements0191(project.Id).Where(x => !IsApprovalRequirement0191(x)).ToArray();
            if (open.Length > 0)
                throw new InvalidOperationException("Не выполнены обязательные условия: "
                                                    + string.Join(", ", open.Select(x => x.Name)));
            project.Status = ProjectStatusIds.Approved;
            project.ApprovalStatus = ProjectApprovalStatusIds.Approved;
            project.ApprovedAtUtc = DateTime.UtcNow;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Approved,
                "Ремонт прототипа одобрен.");
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.repair.approved",
                "Ремонт прототипа одобрен GM.", "GM одобрил ремонт прототипа.", true);
        }, "Prototype repair project approved.");

    public ResponseEnvelope ProjectPrototypeRepairReject(CommandContext context)
        => MutatePrototypeRepairProject0195(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Проект не ожидает решения GM.");
            var reason = RequireLength(FirstNonEmpty(
                PayloadReader.GetString(context.Request.Payload, "publicReason"),
                "Ремонт прототипа отклонён GM."), 1, 512, "publicReason");
            project.Status = ProjectStatusIds.Failed;
            project.ApprovalStatus = ProjectApprovalStatusIds.Rejected;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Rejected, reason);
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.repair.rejected",
                reason, reason, true);
        }, "Prototype repair project rejected.");

    public ResponseEnvelope ProjectPrototypeRepairReserve(CommandContext context)
        => MutatePrototypeRepairProject0195(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.Approved)
                throw new InvalidOperationException("Резерв доступен только одобренному проекту.");
            var state = RequirePrototypeRepairState0195(project);
            if (state.Prototype.IsRepairReserved || state.Prototype.IsTestReserved)
                throw new InvalidOperationException("Прототип уже зарезервирован другим процессом.");
            if (state.Defect.Status != PrototypeDefectStatusIds.Open
                || (!string.IsNullOrWhiteSpace(state.Defect.RepairProjectId)
                    && state.Defect.RepairProjectId != project.Id))
                throw new InvalidOperationException("Дефект уже ремонтируется или больше не открыт.");

            ReserveCraftResources0191(project, actor.Id, operationId);
            state.Prototype.IsRepairReserved = true;
            state.Prototype.RepairReservationOperationId = operationId;
            state.Prototype.ActiveRepairProjectId = project.Id;
            state.Prototype.LifecycleStatus = PrototypeLifecycleStatusIds.RepairInProgress;
            state.Prototype.UpdatedAtUtc = DateTime.UtcNow;
            state.Prototype.Revision++;
            _repositories.PrototypeRuntimeStates.Replace(state.Prototype);

            state.Defect.Status = PrototypeDefectStatusIds.RepairInProgress;
            state.Defect.RepairProjectId = project.Id;
            state.Defect.Revision++;
            _repositories.PrototypeDefectInstances.Replace(state.Defect);
            UpdatePrototypeInventoryRepairState0195(state.Prototype, "На ремонте", "repair_in_progress");

            project.Status = ProjectStatusIds.ResourcesReserved;
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.repair.reserved",
                "Прототип и ремонтные ресурсы зарезервированы.",
                "Прототип передан в ремонт; ресурсы зарезервированы.", true);
        }, "Prototype and repair resources reserved.");

    public ResponseEnvelope ProjectPrototypeRepairStart(CommandContext context)
        => MutatePrototypeRepairProject0195(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.ResourcesReserved)
                throw new InvalidOperationException("Сначала зарезервируйте прототип и ресурсы.");
            var stages = LoadCraftStages0191(project.Id);
            if (stages.Count == 0) throw new InvalidOperationException("У проекта нет стадий.");
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
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.repair.started",
                "Ремонт прототипа начался.", "Ремонт прототипа начался.", true);
        }, "Prototype repair started.");

    public ResponseEnvelope ProjectPrototypeRepairStageComplete(CommandContext context)
        => MutatePrototypeRepairProject0195(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.Testing || project.Status == ProjectStatusIds.AwaitingAcceptance)
                return;
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Проект ремонта не выполняется.");
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

            var next = stages.FirstOrDefault(x =>
                x.SortOrder > current.SortOrder && x.Status != ProjectStageStatusIds.Completed);
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
                project.ProgressPercent = Math.Min(80,
                    (int)Math.Round(80d * project.WorkPointsDone / Math.Max(1, stages.Count)));
            }
            else
            {
                ApplyPrototypeRepair0195(project, actor.Id, operationId);
                project.Status = ProjectStatusIds.Testing;
                project.CurrentStageId = string.Empty;
                project.CurrentStageName = "Ожидает повторного испытания";
                project.WorkPointsDone = stages.Count;
                project.ProgressPercent = 90;
                project.ResultStatus = ProjectResultStatusIds.ReadyForAcceptance;
            }
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.repair.stage.completed",
                "Завершена стадия: " + current.Name,
                next == null
                    ? "Ремонт применён; прототип ожидает повторного испытания."
                    : "Завершена стадия «" + current.Name + "».", true);
        }, "Prototype repair stage completed.",
            project => project.Status == ProjectStatusIds.Testing
                       || project.Status == ProjectStatusIds.AwaitingAcceptance
                       || project.Status == ProjectStatusIds.Completed);

    public ResponseEnvelope ProjectPrototypeRetestExecute(CommandContext context)
        => MutatePrototypeRepairProject0195(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.AwaitingAcceptance || project.Status == ProjectStatusIds.Completed)
                return;
            if (project.Status != ProjectStatusIds.Testing)
                throw new InvalidOperationException("Прототип не ожидает повторного испытания.");
            ExecutePrototypeRetest0195(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.AwaitingAcceptance;
            project.CurrentStageName = "Повторное испытание пройдено";
            project.ProgressPercent = 95;
            project.ResultStatus = ProjectResultStatusIds.ReadyForAcceptance;
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.retest.passed",
                "Повторное испытание завершено: Attempt 2 — Pass.",
                "Повторное испытание пройдено; дефект устранён.", true);
        }, "Prototype retest executed.",
            project => project.Status == ProjectStatusIds.AwaitingAcceptance
                       || project.Status == ProjectStatusIds.Completed);

    public ResponseEnvelope ProjectPrototypeProductionApprove(CommandContext context)
        => MutatePrototypeRepairProject0195(context, true, (project, actor, _, operationId) =>
        {
            var state = RequirePrototypeRepairState0195(project);
            if (state.Prototype.ProductionApprovalStatus
                == PrototypeProductionApprovalStatusIds.ApprovedForLimitedProduction)
                return;
            if (project.Status != ProjectStatusIds.AwaitingAcceptance)
                throw new InvalidOperationException("Сначала завершите повторное испытание.");
            var latest = _repositories.PrototypeTestResults.GetById(state.Prototype.LatestTestResultId)
                         ?? throw new InvalidOperationException("Последний TestResult не найден.");
            if (latest.ResultCategory != PrototypeTestResultCategoryIds.Pass)
                throw new InvalidOperationException("Ограниченное производство требует успешного TestResult.");
            var blocking = _repositories.PrototypeDefectInstances.Find(
                    Builders<PrototypeDefectInstanceState>.Filter.Eq(x => x.PrototypeId, state.Prototype.Id))
                .Any(x => x.Status == PrototypeDefectStatusIds.Open
                          || x.Status == PrototypeDefectStatusIds.RepairInProgress
                          || x.Status == PrototypeDefectStatusIds.ResolvedPendingRetest
                          || x.Status == PrototypeDefectStatusIds.Reopened);
            if (blocking) throw new InvalidOperationException("У прототипа остаются блокирующие дефекты.");

            state.Prototype.ProductionApprovalStatus =
                PrototypeProductionApprovalStatusIds.ApprovedForLimitedProduction;
            state.Prototype.ProductionApprovedByUserId = actor.Id;
            state.Prototype.ProductionApprovedAtUtc = DateTime.UtcNow;
            state.Prototype.ProductionApprovalSourceTestResultId = latest.Id;
            state.Prototype.UpdatedAtUtc = DateTime.UtcNow;
            state.Prototype.Revision++;
            _repositories.PrototypeRuntimeStates.Replace(state.Prototype);
            UpdatePrototypeInventoryAfterProductionApproval0195(state.Prototype);

            project.Status = ProjectStatusIds.Completed;
            project.ResultStatus = ProjectResultStatusIds.Applied;
            project.ProgressPercent = 100;
            project.CompletedAtUtc = DateTime.UtcNow;
            project.CurrentStageName = "Допущено к ограниченному производству";
            project.ExtraData["prototypeOutcome"] = PrototypeProjectOutcomeIds.PrototypeRepairedAndApproved;
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.production.approved",
                "Прототип допущен к ограниченному производству.",
                "GM допустил прототип к ограниченному производству.", true);
        }, "Prototype approved for limited production.",
            project => project.Status == ProjectStatusIds.Completed);

    public ResponseEnvelope ProjectPrototypeRepairCancel(CommandContext context)
        => MutatePrototypeRepairProject0195(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status == ProjectStatusIds.Testing
                || project.Status == ProjectStatusIds.AwaitingAcceptance
                || project.Status == ProjectStatusIds.Completed)
                throw new InvalidOperationException("После применения ремонта отмена запрещена; требуется повторное испытание.");
            ReleasePrototypeRepairReservation0195(project, actor.Id, "prototype repair cancelled");
            project.Status = ProjectStatusIds.Cancelled;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.repair.cancelled",
                "Проект ремонта отменён; резерв освобождён.",
                "Проект ремонта отменён; прототип снова доступен.", true);
        }, "Prototype repair project cancelled.");

    public ResponseEnvelope ProjectPrototypeRepairFail(CommandContext context)
        => MutatePrototypeRepairProject0195(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.Testing
                || project.Status == ProjectStatusIds.AwaitingAcceptance
                || project.Status == ProjectStatusIds.Completed)
                throw new InvalidOperationException("После применения ремонта требуется явный повторный тест.");
            ReleasePrototypeRepairReservation0195(project, actor.Id, "prototype repair failed");
            project.Status = ProjectStatusIds.Failed;
            project.ResultStatus = ProjectResultStatusIds.Failed;
            AddPrototypeAudit0194(project, actor.Id, operationId, "prototype.repair.failed",
                "Проект ремонта завершён неудачей.", "Ремонт не завершён; резерв освобождён.", true);
        }, "Prototype repair project failed.");

    public ResponseEnvelope ProjectPrototypeRepairAudit(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectPrototypeRepairViewEnabled0195(true))
            return ProjectPrototypeRepairDisabled0195(context.Request.Command);
        var project = RequirePrototypeRepairProject0195(context.Request.Payload);
        var state = RequirePrototypeRepairState0195(project);
        var tests = _repositories.PrototypeTestResults.Find(
            Builders<PrototypeTestResultState>.Filter.Eq(x => x.PrototypeId, state.Prototype.Id));
        return Ok("Prototype repair audit loaded.", new Dictionary<string, object>
        {
            ["item"] = PrototypeRepairProjectPayload0195(project, true, true),
            ["persistenceCounts"] = new Dictionary<string, object>
            {
                ["physicalItemInstances"] = CountPrototypePhysicalItems0195(state.Prototype),
                ["prototypeRuntimeStates"] = _repositories.PrototypeRuntimeStates.Find(
                    Builders<PrototypeRuntimeState>.Filter.Eq(x => x.Id, state.Prototype.Id)).Count,
                ["defectInstances"] = _repositories.PrototypeDefectInstances.Find(
                    Builders<PrototypeDefectInstanceState>.Filter.Eq(x => x.PrototypeId, state.Prototype.Id)).Count,
                ["testResults"] = tests.Count,
                ["repairProjects"] = _repositories.Projects.Find(
                    Builders<ProjectBaseState>.Filter.Eq(x => x.RuntimeKind, PrototypeRepairRuntimeKind0195)
                    & Builders<ProjectBaseState>.Filter.Eq(
                        "DefinitionSnapshot.PrototypeRepair.PrototypeId", state.Prototype.Id)).Count
            },
            ["actor"] = actor.Login
        });
    }

    private ResponseEnvelope MutatePrototypeRepairProject0195(
        CommandContext context,
        bool adminOnly,
        Action<ProjectBaseState, UserAccount, bool, string> mutation,
        string successMessage,
        Func<ProjectBaseState, bool>? alreadyApplied = null)
    {
        var actor = adminOnly ? RequireAdmin(context) : GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (adminOnly && !admin) throw new UnauthorizedAccessException("Admin role is required.");
        if (!ProjectPrototypeRepairViewEnabled0195(admin))
            return ProjectPrototypeRepairDisabled0195(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (PrototypeRepairRuntimeLock0195)
        {
            var project = RequirePrototypeRepairProject0195(context.Request.Payload);
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (alreadyApplied?.Invoke(project) == true)
                return Ok(successMessage, PrototypeRepairResponse0195(project, admin, true));
            if (string.Equals(project.LastOperationId, operationId, StringComparison.Ordinal))
            {
                if (!string.Equals(project.LastOperationCommand, context.Request.Command, StringComparison.Ordinal))
                    throw new InvalidOperationException("OperationId was already used for another command.");
                return Ok(successMessage, PrototypeRepairResponse0195(project, admin, true));
            }
            var expected = PayloadReader.GetInt(context.Request.Payload, "expectedRevision")
                           ?? throw new ArgumentException("expectedRevision is required.");
            if (expected != project.Revision)
                throw new InvalidOperationException(
                    $"Project revision conflict. Reload project. current={project.Revision}; expected={expected}");
            mutation(project, actor, admin, operationId);
            SavePrototypeProject0194(project, actor.Id, operationId, context.Request.Command, expected);
            TryPublishProjectSync(project, context.Request.Command, actor.Id,
                context.Request.RequestId ?? string.Empty);
            if (project.Status == ProjectStatusIds.Completed)
                TryWriteProjectJournal(project, operationId,
                    "Завершён ремонт прототипа: " + project.Name, actor.Id);
            return Ok(successMessage, PrototypeRepairResponse0195(project, admin));
        }
    }

    private PrototypeRepairCandidate0195 RequirePrototypeRepairCandidate0195(
        IDictionary<string, object> payload,
        UserAccount actor,
        bool admin,
        bool requireOpen)
    {
        var prototypeId = RequireLength(PayloadReader.GetString(payload, "prototypeId"), 1, 160, "prototypeId");
        var defectId = RequireLength(PayloadReader.GetString(payload, "defectId"), 1, 160, "defectId");
        var prototype = _repositories.PrototypeRuntimeStates.GetById(prototypeId)
                        ?? throw new KeyNotFoundException("Прототип не найден.");
        var ownership = RequireCraftCharacter0191(prototype.OwnerCharacterId, actor, admin);
        if (!admin && !string.Equals(prototype.OwnerUserId, actor.Id, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Этот прототип принадлежит другому игроку.");
        if (!admin)
        {
            var activeCharacterId = _repositories.Presence.Find(
                    Builders<SessionUserState>.Filter.Eq(x => x.UserId, actor.Id))
                .FirstOrDefault()?.ActiveCharacterId ?? string.Empty;
            if (ownership.IsArchived
                || !string.Equals(activeCharacterId, ownership.CharacterId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Ремонт доступен только прототипу текущего неархивного персонажа.");
        }
        if (prototype.ProductionApprovalStatus
            != PrototypeProductionApprovalStatusIds.NotProductionApproved)
            throw new InvalidOperationException("Прототип уже допущен к производству.");
        var defect = _repositories.PrototypeDefectInstances.GetById(defectId)
                     ?? throw new KeyNotFoundException("Дефект не найден.");
        if (!string.Equals(defect.PrototypeId, prototype.Id, StringComparison.Ordinal))
            throw new InvalidOperationException("Дефект относится к другому прототипу.");
        if (requireOpen && defect.Status != PrototypeDefectStatusIds.Open)
            throw new InvalidOperationException("Для ремонта требуется открытый дефект.");
        return new PrototypeRepairCandidate0195(prototype, defect, ownership);
    }

    private ProjectDefinitionSnapshot0191 BuildPrototypeRepairSnapshot0195(
        PrototypeRuntimeState prototype,
        PrototypeDefectInstanceState defect,
        bool requirePlayerVisible)
    {
        var createProject = _repositories.Projects.GetById(prototype.ProjectId)
                            ?? throw new KeyNotFoundException("Исходный проект прототипа не найден.");
        var source = createProject.DefinitionSnapshot
                     ?? throw new InvalidOperationException("Исходный snapshot прототипа отсутствует.");
        var defectDefinition = FindContentDefinition0191(
                                   defect.DefectDefinitionId,
                                   TechnologyRecipeBlueprintProjectDefinitionCategories.Defect)
                               ?? throw new KeyNotFoundException("DefectDefinition недоступен.");
        var template = LoadProjectTemplates0191().FirstOrDefault(x =>
                           string.Equals(ContentField0191(x, "projectType"), "RepairItem",
                               StringComparison.OrdinalIgnoreCase))
                       ?? throw new KeyNotFoundException("Шаблон ремонта прототипа не найден.");
        if (requirePlayerVisible
            && (!IsDefinitionPlayerVisible0191(defectDefinition)
                || !IsDefinitionPlayerVisible0191(template)))
            throw new UnauthorizedAccessException("Ремонт недоступен игроку.");

        var snapshot = new ProjectDefinitionSnapshot0191
        {
            BlueprintDefinitionId = source.BlueprintDefinitionId,
            BlueprintStableKey = source.BlueprintStableKey,
            BlueprintVersion = source.BlueprintVersion,
            BlueprintRevision = source.BlueprintRevision,
            BlueprintName = source.BlueprintName,
            BlueprintPublicDescription = source.BlueprintPublicDescription,
            BlueprintKind = source.BlueprintKind,
            TargetItemDefinitionId = source.TargetItemDefinitionId,
            TargetItemStableKey = source.TargetItemStableKey,
            TargetItemName = source.TargetItemName,
            TargetItemPublicDescription = source.TargetItemPublicDescription,
            TechnologyDefinitionId = source.TechnologyDefinitionId,
            TechnologyStableKey = source.TechnologyStableKey,
            TechnologyVersion = source.TechnologyVersion,
            TechnologyRevision = source.TechnologyRevision,
            TechnologyName = source.TechnologyName,
            TechnologyPublicDescription = source.TechnologyPublicDescription,
            MethodDefinitionId = source.MethodDefinitionId,
            MethodStableKey = source.MethodStableKey,
            MethodVersion = source.MethodVersion,
            MethodRevision = source.MethodRevision,
            MethodName = FirstNonEmpty(source.MethodName, "Контролируемый ремонт"),
            RecipeDefinitionId = source.RecipeDefinitionId,
            RecipeStableKey = source.RecipeStableKey,
            RecipeVersion = source.RecipeVersion,
            RecipeRevision = source.RecipeRevision,
            RecipeName = source.RecipeName,
            ProjectTemplateDefinitionId = template.Id,
            ProjectTemplateStableKey = template.StableKey,
            ProjectTemplateVersion = FirstNonEmpty(template.RecordVersion, template.DefinitionPackVersion),
            ProjectTemplateRevision = template.Revision,
            ProjectTemplateName = FirstNonEmpty(template.DisplayName, template.Name),
            ApprovalPolicy = ContentField0191(template, "approvalPolicy"),
            ResourceReservationPolicy = ContentField0191(template, "resourceReservationPolicy"),
            CancellationRefundPolicy = ContentField0191(template, "cancellationRefundPolicy"),
            EstimatedDurationMinutes = Math.Max(0,
                ParseInt0191(ContentField0191(template, "estimatedDurationMinutes"))),
            Stages = ParseStageRows0191(ContentField0191(template, "stageRows")),
            PrototypeTestProtocol = source.PrototypeTestProtocol,
            PrototypeDefects = source.PrototypeDefects.Select(CloneDefectSnapshot0195).ToList(),
            PrototypeRepair = new PrototypeRepairSnapshot0195
            {
                PrototypeId = prototype.Id,
                ItemInstanceId = prototype.ItemInstanceId,
                PrototypeLifecycleStatus = prototype.LifecycleStatus,
                DefectInstanceId = defect.Id,
                DefectDefinitionId = defect.DefectDefinitionId,
                DefectStableKey = defect.DefectStableKey,
                DefectVersion = defect.DefectVersion,
                DefectRevision = defect.DefectRevision,
                DefectName = defect.Name,
                DefectSeverity = defect.Severity,
                PublicSymptoms = defect.PublicSymptoms.ToList(),
                GMCauseDetails = defect.GMCauseDetails,
                LimitationTags = defect.LimitationTags.ToList(),
                SourceTestResultId = defect.SourceTestResultId,
                SourceTestAttemptNumber = _repositories.PrototypeTestResults.GetById(defect.SourceTestResultId)
                                                  ?.AttemptNumber ?? 1,
                RepairMethod = FirstNonEmpty(source.MethodName, "Контролируемый ремонт"),
                ResolutionSummary = "Резонансный контур откалиброван; требуется повторное испытание."
            }
        };
        ParsePrototypeRepairRequirements0195(
            ContentField0191(defectDefinition, "repairRequirements"), snapshot);
        foreach (var requirement in ParsePrototypeTemplateRequirements0195(template))
            snapshot.Requirements.Add(requirement);
        if (snapshot.Stages.Count == 0)
            throw new InvalidOperationException("Шаблон ремонта не содержит стадий.");
        if (snapshot.PrototypeTestProtocol == null)
            throw new InvalidOperationException("Snapshot обязательного TestProtocol отсутствует.");
        snapshot.SnapshotChecksum = ComputeSnapshotChecksum0191(snapshot);
        return snapshot;
    }

    private void ParsePrototypeRepairRequirements0195(string raw, ProjectDefinitionSnapshot0191 snapshot)
    {
        foreach (var row in ParseRows0191(raw))
        {
            var kind = Cell0191(row, 0, "requirement");
            var definitionId = Cell0191(row, 1);
            var quantity = decimal.TryParse(Cell0191(row, 2, "1"), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var parsed) ? Math.Max(0, parsed) : 1;
            var minimum = Cell0191(row, 3);
            var required = !string.Equals(Cell0191(row, 4, "true"), "false",
                StringComparison.OrdinalIgnoreCase);
            var mode = Cell0191(row, 5, "confirm");
            var publicText = Cell0191(row, 6, "Требование ремонта.");
            var gmText = Cell0191(row, 7);
            var consumable = string.Equals(kind, "resource", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(mode, "consume", StringComparison.OrdinalIgnoreCase);
            if (consumable)
            {
                snapshot.Inputs.Add(new ProjectMaterialSnapshot0191
                {
                    DefinitionId = definitionId,
                    StableKey = FindDefinitionStableKey0191(definitionId),
                    DisplayName = DefinitionDisplayName0191(definitionId, "Ремонтный материал"),
                    Quantity = quantity,
                    Unit = "шт.",
                    MinimumQuality = minimum,
                    UsageMode = "repair_consumption",
                    Optional = !required
                });
            }
            else
            {
                snapshot.Requirements.Add(new ProjectRequirementSnapshot0191
                {
                    Kind = kind,
                    DefinitionId = definitionId,
                    DisplayName = DefinitionDisplayName0191(definitionId, publicText),
                    Quantity = quantity,
                    MinimumQualityOrRank = minimum,
                    Required = required,
                    IsPlayerVisible = true,
                    ConsumptionMode = mode,
                    PublicExplanation = publicText,
                    GMExplanation = gmText
                });
            }
        }
    }

    private IEnumerable<ProjectRequirementSnapshot0191> ParsePrototypeTemplateRequirements0195(
        ContentDefinitionRecord template)
    {
        foreach (var row in ParseRows0191(ContentField0191(template, "requirementRows")))
        {
            yield return new ProjectRequirementSnapshot0191
            {
                Kind = Cell0191(row, 0, "gm_confirmation"),
                DefinitionId = Cell0191(row, 1),
                DisplayName = Cell0191(row, 6, "Условие ремонта"),
                Quantity = decimal.TryParse(Cell0191(row, 2, "1"), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var quantity) ? quantity : 1,
                MinimumQualityOrRank = Cell0191(row, 3),
                Required = !string.Equals(Cell0191(row, 4, "true"), "false",
                    StringComparison.OrdinalIgnoreCase),
                IsPlayerVisible = true,
                ConsumptionMode = Cell0191(row, 5, "confirm"),
                PublicExplanation = Cell0191(row, 6, "Условие ремонта."),
                GMExplanation = Cell0191(row, 7)
            };
        }
    }

    private PrototypeRepairEvaluation0195 EvaluatePrototypeRepairRequirements0195(
        ProjectDefinitionSnapshot0191 snapshot,
        CharacterOwnershipState ownership)
    {
        var repair = snapshot.PrototypeRepair
                     ?? throw new InvalidOperationException("Repair snapshot отсутствует.");
        var result = new PrototypeRepairEvaluation0195();
        result.Requirements.Add(PrototypeRepairRequirement0195.Server(
            "Активный персонаж", ownership.IsActive && !ownership.IsArchived,
            "Персонаж активен и принадлежит владельцу."));
        result.Requirements.Add(PrototypeRepairRequirement0195.Server(
            "Открытый дефект", !string.IsNullOrWhiteSpace(repair.DefectInstanceId),
            "Выбран дефект «" + repair.DefectName + "»."));
        result.Requirements.Add(PrototypeRepairRequirement0195.Server(
            "Повторное испытание", snapshot.PrototypeTestProtocol != null,
            "После ремонта обязателен протокол «" + (snapshot.PrototypeTestProtocol?.Name ?? string.Empty) + "»."));
        foreach (var requirement in snapshot.Requirements)
        {
            var manual = !string.Equals(requirement.Kind, "resource", StringComparison.OrdinalIgnoreCase);
            result.Requirements.Add(new PrototypeRepairRequirement0195
            {
                Kind = requirement.Kind,
                DefinitionId = requirement.DefinitionId,
                Name = requirement.DisplayName,
                PublicSummary = requirement.PublicExplanation,
                GMSummary = requirement.GMExplanation,
                Required = requirement.Required,
                Satisfied = !requirement.Required,
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
        result.CanSubmit = result.Requirements.Where(x => x.Required && !x.ManualGmConfirmation)
                               .All(x => x.Satisfied)
                           && result.Resources.All(x =>
                               string.Equals(Convert.ToString(x["status"], CultureInfo.InvariantCulture),
                                   "available", StringComparison.Ordinal));
        return result;
    }

    private void CreatePrototypeRepairChildren0195(
        ProjectBaseState project,
        PrototypeRepairEvaluation0195 evaluation,
        string actorId)
    {
        var snapshot = project.DefinitionSnapshot
                       ?? throw new InvalidOperationException("Project snapshot is missing.");
        foreach (var stage in snapshot.Stages.OrderBy(x => x.Order))
        {
            _repositories.ProjectStages.Insert(new ProjectStageState
            {
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                StageType = ProjectStageTypeIds.Revision,
                Name = stage.DisplayName,
                PublicSummary = stage.PublicSummary,
                Status = ProjectStageStatusIds.Locked,
                SortOrder = stage.Order * 10,
                IsPlayerVisible = stage.IsPlayerVisible,
                VisibilityMode = stage.IsPlayerVisible
                    ? ProjectVisibilityModeIds.PlayerVisible
                    : ProjectVisibilityModeIds.GmOnly,
                UpdatedByUserId = actorId,
                ExtraData = new Dictionary<string, object>
                {
                    ["stageKey"] = stage.Key,
                    ["runtimeKind"] = PrototypeRepairRuntimeKind0195
                }
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
                VisibilityMode = line.PlayerVisible
                    ? ProjectVisibilityModeIds.PlayerVisible
                    : ProjectVisibilityModeIds.GmOnly,
                VerifiedByUserId = line.Satisfied ? "server" : string.Empty,
                VerifiedAtUtc = line.Satisfied ? DateTime.UtcNow : null,
                ExtraData = new Dictionary<string, object>
                {
                    ["manualGmConfirmation"] = line.ManualGmConfirmation,
                    ["definitionId"] = line.DefinitionId,
                    ["runtimeKind"] = PrototypeRepairRuntimeKind0195
                }
            });
        }
        foreach (var input in snapshot.Inputs)
        {
            _repositories.ProjectResourceRequirements.Insert(new ProjectResourceRequirementState
            {
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                ResourceType = "prototype_repair_material",
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
                    ["runtimeKind"] = PrototypeRepairRuntimeKind0195
                }
            });
        }
    }

    private void ApplyPrototypeRepair0195(ProjectBaseState project, string actorId, string operationId)
    {
        var state = RequirePrototypeRepairState0195(project);
        if (state.Defect.RepairAppliedAtUtc.HasValue) return;
        if (!state.Prototype.IsRepairReserved
            || state.Prototype.ActiveRepairProjectId != project.Id
            || state.Defect.Status != PrototypeDefectStatusIds.RepairInProgress)
            throw new InvalidOperationException("Прототип и дефект не зарезервированы для этого ремонта.");

        var document = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(
                    x => x.CharacterId, project.OwnerCharacterId))
            .FirstOrDefault() ?? throw new KeyNotFoundException("Character inventory profile not found.");
        var prototypeItem = document.Profile?.Items?.FirstOrDefault(x =>
                                string.Equals(x.ItemId, state.Prototype.ItemInstanceId, StringComparison.Ordinal))
                            ?? throw new KeyNotFoundException("Physical prototype ItemInstance not found.");
        var reservations = ActiveCraftReservations0191(project.Id).ToList();
        if (reservations.Count == 0 && (project.DefinitionSnapshot?.Inputs.Count ?? 0) > 0)
            throw new InvalidOperationException("Ремонтные ресурсы не зарезервированы.");
        foreach (var group in reservations.GroupBy(x => x.ItemInstanceId, StringComparer.OrdinalIgnoreCase))
        {
            var item = document.Profile!.Items.FirstOrDefault(x =>
                           string.Equals(x.ItemId, group.Key, StringComparison.OrdinalIgnoreCase))
                       ?? throw new KeyNotFoundException("Зарезервированный ремонтный ресурс не найден.");
            if (string.Equals(item.ItemId, prototypeItem.ItemId, StringComparison.Ordinal))
                throw new InvalidOperationException("Физический прототип не может быть ремонтным расходником.");
            var units = group.Sum(ReservationInventoryUnits0191);
            if (item.Quantity < units)
                throw new InvalidOperationException("Зарезервированного количества ресурса больше нет.");
            item.Quantity -= units;
            item.UpdatedAtUtc = DateTime.UtcNow;
            item.Source = "prototype_repair_consumption_0195";
            if (item.Quantity <= 0) document.Profile.Items.Remove(item);
        }
        prototypeItem.Durability = Math.Max(prototypeItem.Durability, prototypeItem.MaxDurability);
        prototypeItem.Condition = "Отремонтирован: ожидает повторного испытания";
        prototypeItem.Tags = (prototypeItem.Tags ?? new List<string>())
            .Where(x => !string.Equals(x, "repair_in_progress", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x, "has_open_defects", StringComparison.OrdinalIgnoreCase))
            .Concat(new[] { "prototype", "repaired", "awaiting_retest", "not_production_approved" })
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        prototypeItem.UpdatedAtUtc = DateTime.UtcNow;
        var originalUpdated = document.UpdatedUtc;
        document.UpdatedUtc = DateTime.UtcNow;
        var write = _mongo.CharacterInventoryProfiles.ReplaceOne(
            Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, document.Id)
            & Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.UpdatedUtc, originalUpdated),
            document);
        if (write.MatchedCount != 1)
            throw new InvalidOperationException("Инвентарь изменился во время ремонта. Перезагрузите проект.");

        foreach (var reservation in reservations)
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

        state.Defect.Status = PrototypeDefectStatusIds.ResolvedPendingRetest;
        state.Defect.RepairProjectId = project.Id;
        state.Defect.RepairAppliedAtUtc = DateTime.UtcNow;
        state.Defect.ResolutionSummary = project.DefinitionSnapshot?.PrototypeRepair?.ResolutionSummary
                                         ?? "Ремонт применён; требуется повторное испытание.";
        state.Defect.Revision++;
        _repositories.PrototypeDefectInstances.Replace(state.Defect);

        state.Prototype.LifecycleStatus = PrototypeLifecycleStatusIds.AwaitingRetest;
        state.Prototype.TestStatus = PrototypeTestStatusIds.AwaitingRetest;
        state.Prototype.IsRepairReserved = false;
        state.Prototype.IsTestReserved = true;
        state.Prototype.TestReservationOperationId = operationId;
        state.Prototype.UpdatedAtUtc = DateTime.UtcNow;
        state.Prototype.Revision++;
        _repositories.PrototypeRuntimeStates.Replace(state.Prototype);
    }

    private void ExecutePrototypeRetest0195(ProjectBaseState project, string actorId, string operationId)
    {
        var state = RequirePrototypeRepairState0195(project);
        var resultId = "prototype_retest_result_0195_" + project.Id;
        if (_repositories.PrototypeTestResults.GetById(resultId) != null) return;
        if (!state.Prototype.IsTestReserved
            || state.Prototype.LifecycleStatus != PrototypeLifecycleStatusIds.AwaitingRetest
            || state.Defect.Status != PrototypeDefectStatusIds.ResolvedPendingRetest)
            throw new InvalidOperationException("Прототип не готов к повторному испытанию.");
        var protocol = project.DefinitionSnapshot?.PrototypeTestProtocol
                       ?? throw new InvalidOperationException("TestProtocol snapshot отсутствует.");
        var previous = _repositories.PrototypeTestResults.GetById(
                           project.DefinitionSnapshot?.PrototypeRepair?.SourceTestResultId ?? string.Empty)
                       ?? _repositories.PrototypeTestResults.Find(
                               Builders<PrototypeTestResultState>.Filter.Eq(
                                   x => x.PrototypeId, state.Prototype.Id))
                           .OrderByDescending(x => x.AttemptNumber).FirstOrDefault()
                       ?? throw new InvalidOperationException("Исходный TestResult не найден.");
        var result = new PrototypeTestResultState
        {
            Id = resultId,
            CampaignId = project.CampaignId,
            ProjectId = project.Id,
            RepairProjectId = project.Id,
            PrototypeId = state.Prototype.Id,
            ItemInstanceId = state.Prototype.ItemInstanceId,
            OwnerCharacterId = project.OwnerCharacterId,
            TestProtocolDefinitionId = protocol.DefinitionId,
            TestProtocolStableKey = protocol.StableKey,
            TestProtocolVersion = protocol.Version,
            TestProtocolRevision = protocol.Revision,
            TestProtocolName = protocol.Name,
            AttemptNumber = Math.Max(1, previous.AttemptNumber) + 1,
            PreviousTestResultId = previous.Id,
            ExecutedSteps = protocol.PublicSteps.ToList(),
            ObservedMetrics = new Dictionary<string, decimal>
            {
                ["stability"] = 96m,
                ["thermal_margin"] = 88m
            },
            ResultCategory = PrototypeTestResultCategoryIds.Pass,
            PublicSummary = "Повторное испытание пройдено: ремонт подтверждён.",
            GMSummary = "Детерминированный ReferenceDemo retest outcome: Pass.",
            ResolvedDefectInstanceIds = new List<string> { state.Defect.Id },
            ExecutedByUserId = actorId,
            ExecutionOperationId = operationId,
            IsPlayerVisible = true,
            ServerOnlyData = new Dictionary<string, object>
            {
                ["outcomePolicy"] = "ReferenceDemoDeterministicPass"
            }
        };
        _repositories.PrototypeTestResults.Insert(result);

        state.Defect.Status = PrototypeDefectStatusIds.Resolved;
        state.Defect.RetestResultId = result.Id;
        state.Defect.ResolvedAtUtc = DateTime.UtcNow;
        state.Defect.Revision++;
        _repositories.PrototypeDefectInstances.Replace(state.Defect);

        state.Prototype.LifecycleStatus = PrototypeLifecycleStatusIds.TestedPassed;
        state.Prototype.TestStatus = PrototypeTestStatusIds.RetestCompleted;
        state.Prototype.ActiveDefectInstanceIds.RemoveAll(x =>
            string.Equals(x, state.Defect.Id, StringComparison.Ordinal));
        state.Prototype.LatestTestResultId = result.Id;
        state.Prototype.IsTestReserved = false;
        state.Prototype.ActiveRepairProjectId = project.Id;
        state.Prototype.UpdatedAtUtc = DateTime.UtcNow;
        state.Prototype.Revision++;
        _repositories.PrototypeRuntimeStates.Replace(state.Prototype);
        UpdatePrototypeInventoryAfterRetest0195(state.Prototype);
    }

    private void ReleasePrototypeRepairReservation0195(
        ProjectBaseState project,
        string actorId,
        string reason)
    {
        ReleaseCraftReservations0191(project.Id, actorId, reason);
        var repair = project.DefinitionSnapshot?.PrototypeRepair;
        if (repair == null) return;
        var prototype = _repositories.PrototypeRuntimeStates.GetById(repair.PrototypeId);
        var defect = _repositories.PrototypeDefectInstances.GetById(repair.DefectInstanceId);
        if (prototype != null && prototype.ActiveRepairProjectId == project.Id && prototype.IsRepairReserved)
        {
            prototype.IsRepairReserved = false;
            prototype.RepairReservationOperationId = string.Empty;
            prototype.ActiveRepairProjectId = string.Empty;
            prototype.LifecycleStatus = PrototypeLifecycleStatusIds.TestedWithDefects;
            prototype.UpdatedAtUtc = DateTime.UtcNow;
            prototype.Revision++;
            _repositories.PrototypeRuntimeStates.Replace(prototype);
            UpdatePrototypeInventoryRepairState0195(prototype,
                "Испытан: открытый дефект", "has_open_defects");
        }
        if (defect != null
            && defect.RepairProjectId == project.Id
            && defect.Status == PrototypeDefectStatusIds.RepairInProgress)
        {
            defect.Status = PrototypeDefectStatusIds.Open;
            defect.RepairProjectId = string.Empty;
            defect.Revision++;
            _repositories.PrototypeDefectInstances.Replace(defect);
        }
    }

    private void UpdatePrototypeInventoryRepairState0195(
        PrototypeRuntimeState prototype,
        string condition,
        string stateTag)
    {
        UpdatePrototypeInventoryItem0195(prototype, item =>
        {
            item.Condition = condition;
            item.Tags = (item.Tags ?? new List<string>())
                .Where(x => !string.Equals(x, "repair_in_progress", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(x, "awaiting_retest", StringComparison.OrdinalIgnoreCase))
                .Concat(new[] { "prototype", stateTag, "not_production_approved" })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        });
    }

    private void UpdatePrototypeInventoryAfterRetest0195(PrototypeRuntimeState prototype)
    {
        UpdatePrototypeInventoryItem0195(prototype, item =>
        {
            item.Condition = "Повторное испытание пройдено";
            item.Tags = (item.Tags ?? new List<string>())
                .Where(x => !string.Equals(x, "repair_in_progress", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(x, "awaiting_retest", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(x, "has_open_defects", StringComparison.OrdinalIgnoreCase))
                .Concat(new[] { "prototype", "repaired", "tested_passed", "not_production_approved" })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        });
    }

    private void UpdatePrototypeInventoryAfterProductionApproval0195(PrototypeRuntimeState prototype)
    {
        UpdatePrototypeInventoryItem0195(prototype, item =>
        {
            item.Condition = "Испытан и допущен к ограниченному производству";
            item.Tags = (item.Tags ?? new List<string>())
                .Where(x => !string.Equals(x, "not_production_approved", StringComparison.OrdinalIgnoreCase))
                .Concat(new[] { "prototype", "approved_for_limited_production" })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        });
    }

    private void UpdatePrototypeInventoryItem0195(
        PrototypeRuntimeState prototype,
        Action<CharacterInventoryItemProfileValue> mutation)
    {
        var document = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(
                    x => x.CharacterId, prototype.OwnerCharacterId))
            .FirstOrDefault() ?? throw new KeyNotFoundException("Character inventory profile not found.");
        var item = document.Profile?.Items?.FirstOrDefault(x =>
                       string.Equals(x.ItemId, prototype.ItemInstanceId, StringComparison.Ordinal))
                   ?? throw new KeyNotFoundException("Physical prototype ItemInstance not found.");
        mutation(item);
        item.UpdatedAtUtc = DateTime.UtcNow;
        var originalUpdated = document.UpdatedUtc;
        document.UpdatedUtc = DateTime.UtcNow;
        var write = _mongo.CharacterInventoryProfiles.ReplaceOne(
            Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, document.Id)
            & Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.UpdatedUtc, originalUpdated),
            document);
        if (write.MatchedCount != 1)
            throw new InvalidOperationException("Inventory changed while updating prototype. Reload and retry.");
    }

    private PrototypeRepairState0195 RequirePrototypeRepairState0195(ProjectBaseState project)
    {
        var repair = project.DefinitionSnapshot?.PrototypeRepair
                     ?? throw new InvalidOperationException("Repair snapshot отсутствует.");
        var prototype = _repositories.PrototypeRuntimeStates.GetById(repair.PrototypeId)
                        ?? throw new KeyNotFoundException("Прототип не найден.");
        var defect = _repositories.PrototypeDefectInstances.GetById(repair.DefectInstanceId)
                     ?? throw new KeyNotFoundException("Дефект не найден.");
        return new PrototypeRepairState0195(prototype, defect);
    }

    private ProjectBaseState RequirePrototypeRepairProject0195(IDictionary<string, object> payload)
    {
        var id = RequireLength(FirstNonEmpty(
            PayloadReader.GetString(payload, "projectId"),
            PayloadReader.GetString(payload, "id")), 1, 160, "projectId");
        var project = _repositories.Projects.GetById(id)
                      ?? throw new KeyNotFoundException("Проект ремонта прототипа не найден.");
        if (!string.Equals(project.RuntimeKind, PrototypeRepairRuntimeKind0195, StringComparison.Ordinal))
            throw new KeyNotFoundException("Проект ремонта прототипа не найден.");
        return project;
    }

    private Dictionary<string, object> PrototypeRepairResponse0195(
        ProjectBaseState project,
        bool admin,
        bool alreadyApplied = false)
        => new()
        {
            ["item"] = PrototypeRepairProjectPayload0195(project, admin, true),
            ["alreadyApplied"] = alreadyApplied,
            ["revision"] = project.Revision
        };

    private Dictionary<string, object> PrototypeRepairProjectPayload0195(
        ProjectBaseState project,
        bool admin,
        bool details)
    {
        var snapshot = project.DefinitionSnapshot ?? new ProjectDefinitionSnapshot0191();
        var repair = snapshot.PrototypeRepair ?? new PrototypeRepairSnapshot0195();
        var prototype = _repositories.PrototypeRuntimeStates.GetById(repair.PrototypeId);
        var defect = _repositories.PrototypeDefectInstances.GetById(repair.DefectInstanceId);
        var tests = prototype == null
            ? new List<PrototypeTestResultState>()
            : _repositories.PrototypeTestResults.Find(
                    Builders<PrototypeTestResultState>.Filter.Eq(x => x.PrototypeId, prototype.Id))
                .OrderBy(x => x.AttemptNumber).ThenBy(x => x.CompletedAtUtc).ToList();
        var payload = new Dictionary<string, object>
        {
            ["projectId"] = project.Id,
            ["name"] = project.Name,
            ["projectType"] = ProjectTypeIds.Repair,
            ["projectTypeLabel"] = "Ремонт прототипа",
            ["status"] = project.Status,
            ["statusLabel"] = PrototypeRepairProjectStatusLabel0195(project.Status),
            ["approvalStatus"] = project.ApprovalStatus,
            ["progressPercent"] = project.ProgressPercent,
            ["currentStageName"] = project.CurrentStageName,
            ["ownerDisplayName"] = project.OwnerDisplayName,
            ["ownerCharacterDisplayName"] = _repositories.CharacterOwnerships.Find(
                    Builders<CharacterOwnershipState>.Filter.Eq(
                        x => x.CharacterId, project.OwnerCharacterId))
                .FirstOrDefault()?.CharacterDisplayName ?? "Персонаж не найден",
            ["blueprintName"] = snapshot.BlueprintName,
            ["targetItemName"] = prototype?.DisplayName ?? snapshot.TargetItemName,
            ["prototypeName"] = prototype?.DisplayName ?? snapshot.TargetItemName,
            ["technologyName"] = snapshot.TechnologyName,
            ["methodName"] = snapshot.MethodName,
            ["recipeName"] = snapshot.RecipeName,
            ["templateName"] = snapshot.ProjectTemplateName,
            ["testProtocolName"] = snapshot.PrototypeTestProtocol?.Name ?? string.Empty,
            ["prototypeWarning"] = "После ремонта обязателен повторный TestProtocol; допуск к производству выдаёт GM отдельно.",
            ["prototypeStatus"] = PrototypeLifecycleLabel0195(prototype?.LifecycleStatus),
            ["testStatus"] = PrototypeTestLabel0195(prototype?.TestStatus),
            ["testResultCategory"] = PrototypeTestHistorySummary0195(tests),
            ["testPublicSummary"] = tests.LastOrDefault()?.PublicSummary ?? string.Empty,
            ["defectName"] = defect?.IsPlayerVisible == true ? defect.Name : string.Empty,
            ["defectSeverity"] = defect?.IsPlayerVisible == true ? defect.Severity : string.Empty,
            ["defectStatus"] = defect?.IsPlayerVisible == true
                ? PrototypeDefectStatusLabel0195(defect.Status)
                : string.Empty,
            ["defectSymptoms"] = defect?.IsPlayerVisible == true
                ? string.Join(", ", defect.PublicSymptoms)
                : string.Empty,
            ["defectLimitations"] = defect?.IsPlayerVisible == true
                ? string.Join(", ", defect.LimitationTags)
                : string.Empty,
            ["resolutionSummary"] = defect?.IsPlayerVisible == true ? defect.ResolutionSummary : string.Empty,
            ["productionApprovalLabel"] = PrototypeProductionApprovalLabel0195(
                prototype?.ProductionApprovalStatus),
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
                    var row = new Dictionary<string, object>
                    {
                        ["name"] = x.Name,
                        ["summary"] = x.PublicSummary,
                        ["status"] = x.Status,
                        ["statusLabel"] = RequirementStatusLabel0191(x.Status)
                    };
                    if (admin) row["requirementId"] = x.Id;
                    return (object)row;
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
            payload["testHistory"] = tests.Where(x => admin || x.IsPlayerVisible)
                .Select(x => (object)new Dictionary<string, object>
                {
                    ["attemptNumber"] = x.AttemptNumber,
                    ["result"] = PrototypeTestResultLabel0195(x.ResultCategory),
                    ["summary"] = x.PublicSummary,
                    ["completedAtUtc"] = x.CompletedAtUtc
                }).ToArray();
            payload["testSteps"] = (snapshot.PrototypeTestProtocol?.PublicSteps ?? new List<string>())
                .Cast<object>().ToArray();
        }
        if (admin)
        {
            payload["ownerCharacterId"] = project.OwnerCharacterId;
            payload["defectGmCause"] = defect?.GMCauseDetails ?? string.Empty;
            payload["testGmSummary"] = tests.LastOrDefault()?.GMSummary ?? string.Empty;
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

    private Dictionary<string, object> PrototypeRepairCandidatePayload0195(
        PrototypeRuntimeState prototype,
        PrototypeDefectInstanceState defect,
        bool admin)
    {
        var payload = new Dictionary<string, object>
        {
            ["prototypeId"] = prototype.Id,
            ["defectId"] = defect.Id,
            ["name"] = prototype.DisplayName,
            ["blueprintName"] = prototype.BlueprintName,
            ["lifecycleStatus"] = PrototypeLifecycleLabel0195(prototype.LifecycleStatus),
            ["defectName"] = defect.Name,
            ["defectSeverity"] = defect.Severity,
            ["defectSymptoms"] = string.Join(", ", defect.PublicSymptoms),
            ["summary"] = prototype.DisplayName + " · " + defect.Name
        };
        if (admin) payload["defectGmCause"] = defect.GMCauseDetails;
        return payload;
    }

    private Dictionary<string, object> PrototypeRepairPreviewPayload0195(
        ProjectDefinitionSnapshot0191 snapshot,
        PrototypeRepairEvaluation0195 evaluation,
        bool admin)
    {
        var repair = snapshot.PrototypeRepair ?? new PrototypeRepairSnapshot0195();
        var payload = new Dictionary<string, object>
        {
            ["prototypeName"] = snapshot.TargetItemName + " (прототип)",
            ["blueprintName"] = snapshot.BlueprintName,
            ["defectName"] = repair.DefectName,
            ["defectSeverity"] = repair.DefectSeverity,
            ["defectSymptoms"] = string.Join(", ", repair.PublicSymptoms),
            ["defectLimitations"] = string.Join(", ", repair.LimitationTags),
            ["methodName"] = snapshot.MethodName,
            ["testProtocolName"] = snapshot.PrototypeTestProtocol?.Name ?? string.Empty,
            ["warning"] = "Ремонт не означает допуск к производству: после него обязателен повторный тест и отдельное решение GM.",
            ["requirements"] = evaluation.Requirements.Where(x => x.PlayerVisible)
                .Select(x => (object)new Dictionary<string, object>
                {
                    ["name"] = x.Name,
                    ["summary"] = x.PublicSummary,
                    ["status"] = x.Satisfied
                        ? "satisfied"
                        : x.ManualGmConfirmation ? "gm_confirmation" : "missing",
                    ["statusLabel"] = x.Satisfied
                        ? "Выполнено"
                        : x.ManualGmConfirmation ? "Подтверждает GM" : "Не выполнено"
                }).ToArray(),
            ["resources"] = evaluation.Resources.Cast<object>().ToArray(),
            ["canSubmit"] = evaluation.CanSubmit
        };
        if (admin) payload["defectGmCause"] = repair.GMCauseDetails;
        return payload;
    }

    private int CountPrototypePhysicalItems0195(PrototypeRuntimeState prototype)
    {
        var document = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(
                    x => x.CharacterId, prototype.OwnerCharacterId))
            .FirstOrDefault();
        return document?.Profile?.Items?.Count(x =>
                   string.Equals(x.ItemId, prototype.ItemInstanceId, StringComparison.Ordinal)) ?? 0;
    }

    private bool ProjectPrototypeRepairViewEnabled0195(bool admin)
        => ProjectPrototypeRepairBaseEnabled0195()
           && (admin
               ? _featureFlags.IsEnabled(nameof(
                   UnifiedProjectRuntimeFeatureFlags.UseUnifiedPrototypeRepairAdminView))
               : _featureFlags.IsEnabled(nameof(
                   UnifiedProjectRuntimeFeatureFlags.UseUnifiedPrototypeRepairPlayerView)));

    private bool ProjectPrototypeRepairBaseEnabled0195()
        => _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectRuntimeV1))
           && _featureFlags.IsEnabled(nameof(
               UnifiedProjectRuntimeFeatureFlags.UsePrototypeRepairProjectV1));

    private ResponseEnvelope ProjectPrototypeRepairDisabled0195(string command)
    {
        _logger.Admin($"project.prototype.repair.disabled command={command}");
        return Error("Unified prototype repair runtime is disabled by feature flags.",
            ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private static PrototypeDefectSnapshot0194 CloneDefectSnapshot0195(
        PrototypeDefectSnapshot0194 source)
        => new()
        {
            DefinitionId = source.DefinitionId,
            StableKey = source.StableKey,
            Version = source.Version,
            Revision = source.Revision,
            Name = source.Name,
            Severity = source.Severity,
            PublicSymptoms = source.PublicSymptoms.ToList(),
            GMCauseDetails = source.GMCauseDetails,
            LimitationTags = source.LimitationTags.ToList()
        };

    private static string PrototypeRepairProjectStatusLabel0195(string status) => status switch
    {
        ProjectStatusIds.Draft => "Черновик ремонта",
        ProjectStatusIds.AwaitingApproval => "Ожидает решения GM",
        ProjectStatusIds.Approved => "Ремонт одобрен",
        ProjectStatusIds.ResourcesReserved => "Прототип и ресурсы зарезервированы",
        ProjectStatusIds.InProgress => "На ремонте",
        ProjectStatusIds.Testing => "Ожидает повторного испытания",
        ProjectStatusIds.AwaitingAcceptance => "Повторное испытание пройдено",
        ProjectStatusIds.Completed => "Допущено к ограниченному производству",
        ProjectStatusIds.Cancelled => "Ремонт отменён",
        ProjectStatusIds.Failed => "Ремонт не выполнен",
        _ => status
    };

    private static string PrototypeLifecycleLabel0195(string? status) => status switch
    {
        PrototypeLifecycleStatusIds.TestedWithDefects => "Испытан: есть дефект",
        PrototypeLifecycleStatusIds.RepairInProgress => "На ремонте",
        PrototypeLifecycleStatusIds.AwaitingRetest => "Ожидает повторного испытания",
        PrototypeLifecycleStatusIds.TestedPassed => "Испытан успешно",
        PrototypeLifecycleStatusIds.AwaitingTest => "Ожидает испытания",
        PrototypeLifecycleStatusIds.TestFailed => "Испытание не пройдено",
        _ => "Состояние прототипа не определено"
    };

    private static string PrototypeTestLabel0195(string? status) => status switch
    {
        PrototypeTestStatusIds.Completed => "Attempt 1 завершён",
        PrototypeTestStatusIds.AwaitingRetest => "Ожидает Attempt 2",
        PrototypeTestStatusIds.RetestCompleted => "Attempt 2 завершён: Pass",
        PrototypeTestStatusIds.AwaitingTest => "Ожидает первого испытания",
        _ => "Испытание не назначено"
    };

    private static string PrototypeDefectStatusLabel0195(string status) => status switch
    {
        PrototypeDefectStatusIds.Open => "Открыт",
        PrototypeDefectStatusIds.RepairInProgress => "Устраняется",
        PrototypeDefectStatusIds.ResolvedPendingRetest => "Устранён, ожидает проверки",
        PrototypeDefectStatusIds.Resolved => "Устранён",
        PrototypeDefectStatusIds.Reopened => "Открыт повторно",
        _ => status
    };

    private static string PrototypeProductionApprovalLabel0195(string? status)
        => status == PrototypeProductionApprovalStatusIds.ApprovedForLimitedProduction
            ? "Допущено к ограниченному производству"
            : "Не допущено к производству";

    private static string PrototypeTestResultLabel0195(string status) => status switch
    {
        PrototypeTestResultCategoryIds.Pass => "Pass",
        PrototypeTestResultCategoryIds.PartialPass => "PartialPassWithDefect",
        PrototypeTestResultCategoryIds.Fail => "Fail",
        _ => status
    };

    private static string PrototypeTestHistorySummary0195(
        IReadOnlyCollection<PrototypeTestResultState> tests)
        => tests.Count == 0
            ? "Испытаний нет"
            : string.Join(" → ", tests.OrderBy(x => x.AttemptNumber)
                .Select(x => $"Attempt {x.AttemptNumber}: {PrototypeTestResultLabel0195(x.ResultCategory)}"));

    private sealed class PrototypeRepairEvaluation0195
    {
        public List<PrototypeRepairRequirement0195> Requirements { get; } = new();
        public List<Dictionary<string, object>> Resources { get; } = new();
        public bool CanSubmit { get; set; }
    }

    private sealed class PrototypeRepairRequirement0195
    {
        public string Kind { get; set; } = string.Empty;
        public string DefinitionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PublicSummary { get; set; } = string.Empty;
        public string GMSummary { get; set; } = string.Empty;
        public bool Required { get; set; }
        public bool Satisfied { get; set; }
        public bool ManualGmConfirmation { get; set; }
        public bool PlayerVisible { get; set; } = true;

        public static PrototypeRepairRequirement0195 Server(
            string name,
            bool satisfied,
            string summary)
            => new()
            {
                Kind = "server_validation",
                Name = name,
                PublicSummary = summary,
                GMSummary = summary,
                Required = true,
                Satisfied = satisfied,
                PlayerVisible = true
            };
    }

    private sealed class PrototypeRepairCandidate0195
    {
        public PrototypeRepairCandidate0195(
            PrototypeRuntimeState prototype,
            PrototypeDefectInstanceState defect,
            CharacterOwnershipState ownership)
        {
            Prototype = prototype;
            Defect = defect;
            Ownership = ownership;
        }

        public PrototypeRuntimeState Prototype { get; }
        public PrototypeDefectInstanceState Defect { get; }
        public CharacterOwnershipState Ownership { get; }
    }

    private sealed class PrototypeRepairState0195
    {
        public PrototypeRepairState0195(
            PrototypeRuntimeState prototype,
            PrototypeDefectInstanceState defect)
        {
            Prototype = prototype;
            Defect = defect;
        }

        public PrototypeRuntimeState Prototype { get; }
        public PrototypeDefectInstanceState Defect { get; }
    }
}
