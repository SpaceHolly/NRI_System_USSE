using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope ProjectList(CommandContext context)
    {
        RequireAdmin(context);
        if (!ProjectAdminEnabled()) return ProjectDisabled(context.Request.Command);
        var campaignId = RequireLength(PayloadReader.GetString(context.Request.Payload, "campaignId"), 0, 128, "campaignId");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var status = NormalizeProjectStatus(PayloadReader.GetString(context.Request.Payload, "status"), allowEmpty: true);
        var type = NormalizeProjectType(PayloadReader.GetString(context.Request.Payload, "projectType"));

        var filter = FilterDefinition<ProjectBaseState>.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(status)) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.Status, status);
        if (!string.IsNullOrWhiteSpace(type)) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.ProjectType, type);
        if (!includeArchived) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false);

        var items = _repositories.Projects.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(300)
            .Select(x => (object)ProjectPayload(x, includeAdminFields: true, includeDetails: false))
            .ToArray();
        return Ok("Projects loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProjectGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!ProjectAdminEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        return Ok("Project loaded.", new Dictionary<string, object> { { "item", ProjectPayload(project, includeAdminFields: true, includeDetails: true) } });
    }

    public ResponseEnvelope ProjectCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectAdminEnabled()) return ProjectDisabled(context.Request.Command);
        var project = BuildProjectFromPayload(context.Request.Payload, actor, isPlayerDraft: false);
        project.Status = NormalizeProjectStatus(PayloadReader.GetString(context.Request.Payload, "status"), allowEmpty: true);
        if (string.IsNullOrWhiteSpace(project.Status)) project.Status = ProjectStatusIds.Draft;
        project.ApprovalStatus = NormalizeProjectApprovalStatus(PayloadReader.GetString(context.Request.Payload, "approvalStatus"), allowEmpty: true);
        if (string.IsNullOrWhiteSpace(project.ApprovalStatus)) project.ApprovalStatus = ProjectApprovalStatusIds.Draft;
        _repositories.Projects.Insert(project);
        AddProjectAudit(project, actor.Id, "created", "Project created.", project.PublicSummary, isPlayerVisible: false);
        TryPublishProjectSync(project, "created", actor.Id, context.Request.RequestId ?? string.Empty);
        TryWriteProjectJournal(project, "project.created", "Project created", actor.Id);
        _logger.Admin($"project.create.done actor={actor.Login} projectId={project.Id} type={project.ProjectType}");
        return Ok("Project created.", new Dictionary<string, object> { { "item", ProjectPayload(project, includeAdminFields: true, includeDetails: true) } });
    }

    public ResponseEnvelope ProjectUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectAdminEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        UpdateProjectFromPayload(project, context.Request.Payload, actor.Id, adminUpdate: true);
        _repositories.Projects.Replace(project);
        AddProjectAudit(project, actor.Id, "updated", "Project updated.", "Project updated.", isPlayerVisible: false);
        TryPublishProjectSync(project, "updated", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Project updated.", new Dictionary<string, object> { { "item", ProjectPayload(project, includeAdminFields: true, includeDetails: true) } });
    }

    public ResponseEnvelope ProjectStatusSet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectAdminEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        var status = NormalizeProjectStatus(PayloadReader.GetString(context.Request.Payload, "status"), allowEmpty: false);
        project.Status = status;
        if (status == ProjectStatusIds.Active && project.StartedAtUtc == null) project.StartedAtUtc = DateTime.UtcNow;
        if (status == ProjectStatusIds.Completed) project.CompletedAtUtc = DateTime.UtcNow;
        TouchProject(project, actor.Id);
        _repositories.Projects.Replace(project);
        AddProjectAudit(project, actor.Id, "status.set", "Project status changed to " + status + ".", "Status changed.", isPlayerVisible: true);
        TryPublishProjectSync(project, "status.set", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Project status updated.", new Dictionary<string, object> { { "item", ProjectPayload(project, includeAdminFields: true, includeDetails: true) } });
    }

    public ResponseEnvelope ProjectStageAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectStagesEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        var stage = new ProjectStageState
        {
            ProjectId = project.Id,
            CampaignId = project.CampaignId,
            StageType = NormalizeProjectStageType(PayloadReader.GetString(context.Request.Payload, "stageType")),
            Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), "Stage"), 1, 160, "name"),
            PublicSummary = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicSummary"), 1024),
            GMSummary = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmSummary"), 2048),
            Status = NormalizeProjectStageStatus(PayloadReader.GetString(context.Request.Payload, "status")),
            SortOrder = PayloadReader.GetInt(context.Request.Payload, "sortOrder") ?? NextStageSortOrder(project.Id),
            WorkPointsRequired = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "workPointsRequired") ?? 0),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible") || !context.Request.Payload.ContainsKey("isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode")),
            UpdatedByUserId = actor.Id,
            PublicNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicNotes"), 2048),
            GMNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 4096)
        };
        _repositories.ProjectStages.Insert(stage);
        project.CurrentStageId = FirstNonEmpty(project.CurrentStageId, stage.Id);
        project.CurrentStageName = FirstNonEmpty(project.CurrentStageName, stage.Name);
        TouchProject(project, actor.Id);
        _repositories.Projects.Replace(project);
        AddProjectAudit(project, actor.Id, "stage.add", $"Stage added: {stage.Name}", $"Stage added: {stage.Name}", stage.IsPlayerVisible);
        return Ok("Project stage added.", new Dictionary<string, object> { { "item", ProjectPayload(project, includeAdminFields: true, includeDetails: true) }, { "stage", ProjectStagePayload(stage, true) } });
    }

    public ResponseEnvelope ProjectStageUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectStagesEnabled()) return ProjectDisabled(context.Request.Command);
        var stage = RequireProjectStage(context);
        stage.Name = FirstNonEmpty(SafeProjectText(PayloadReader.GetString(context.Request.Payload, "name"), 160), stage.Name);
        stage.PublicSummary = FirstNonEmpty(SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicSummary"), 1024), stage.PublicSummary);
        stage.GMSummary = FirstNonEmpty(SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmSummary"), 2048), stage.GMSummary);
        var status = NormalizeProjectStageStatus(PayloadReader.GetString(context.Request.Payload, "status"), allowEmpty: true);
        if (!string.IsNullOrWhiteSpace(status)) stage.Status = status;
        stage.ProgressPercent = Math.Max(0, Math.Min(100, PayloadReader.GetInt(context.Request.Payload, "progressPercent") ?? stage.ProgressPercent));
        stage.WorkPointsDone = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "workPointsDone") ?? stage.WorkPointsDone);
        stage.WorkPointsRequired = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "workPointsRequired") ?? stage.WorkPointsRequired);
        if (context.Request.Payload.ContainsKey("isPlayerVisible")) stage.IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible");
        stage.VisibilityMode = FirstNonEmpty(NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode"), allowEmpty: true), stage.VisibilityMode);
        stage.UpdatedAtUtc = DateTime.UtcNow;
        stage.UpdatedByUserId = actor.Id;
        _repositories.ProjectStages.Replace(stage);
        var project = _repositories.Projects.GetById(stage.ProjectId);
        if (project != null) AddProjectAudit(project, actor.Id, "stage.update", $"Stage updated: {stage.Name}", $"Stage updated: {stage.Name}", stage.IsPlayerVisible);
        return Ok("Project stage updated.", new Dictionary<string, object> { { "stage", ProjectStagePayload(stage, true) } });
    }

    public ResponseEnvelope ProjectStageComplete(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectStagesEnabled()) return ProjectDisabled(context.Request.Command);
        var stage = RequireProjectStage(context);
        stage.Status = ProjectStageStatusIds.Completed;
        stage.ProgressPercent = 100;
        stage.CompletedAtUtc = DateTime.UtcNow;
        stage.UpdatedAtUtc = DateTime.UtcNow;
        stage.UpdatedByUserId = actor.Id;
        _repositories.ProjectStages.Replace(stage);
        var project = _repositories.Projects.GetById(stage.ProjectId);
        if (project != null)
        {
            RecalculateProjectProgress(project, actor.Id);
            AddProjectAudit(project, actor.Id, "stage.complete", $"Stage completed: {stage.Name}", $"Stage completed: {stage.Name}", stage.IsPlayerVisible);
        }
        return Ok("Project stage completed.", new Dictionary<string, object> { { "stage", ProjectStagePayload(stage, true) } });
    }

    public ResponseEnvelope ProjectParticipantAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectParticipantsEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        var participant = new ProjectParticipantState
        {
            ProjectId = project.Id,
            CampaignId = project.CampaignId,
            EntityType = NormalizeParticipantEntityType(PayloadReader.GetString(context.Request.Payload, "entityType")),
            EntityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityId"), 0, 128, "entityId"),
            DisplayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "displayName"), "Participant"), 1, 160, "displayName"),
            ParticipantRole = NormalizeParticipantRole(PayloadReader.GetString(context.Request.Payload, "participantRole")),
            ContributionMode = NormalizeContributionMode(PayloadReader.GetString(context.Request.Payload, "contributionMode")),
            OwnerUserId = RequireLength(PayloadReader.GetString(context.Request.Payload, "ownerUserId"), 0, 128, "ownerUserId"),
            IsPrimary = PayloadReader.GetBool(context.Request.Payload, "isPrimary"),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible") || !context.Request.Payload.ContainsKey("isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode")),
            AddedByUserId = actor.Id,
            PublicNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicNotes"), 2048),
            GMNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 4096)
        };
        _repositories.ProjectParticipants.Insert(participant);
        AddProjectAudit(project, actor.Id, "participant.add", $"Participant added: {participant.DisplayName}", $"Participant added: {participant.DisplayName}", participant.IsPlayerVisible);
        return Ok("Project participant added.", new Dictionary<string, object> { { "participant", ProjectParticipantPayload(participant, true) } });
    }

    public ResponseEnvelope ProjectParticipantUpdate(CommandContext context)
        => UpdateProjectChild<ProjectParticipantState>(context, _repositories.ProjectParticipants, "participant", ProjectParticipantPayload, item =>
        {
            item.DisplayName = FirstNonEmpty(SafeProjectText(PayloadReader.GetString(context.Request.Payload, "displayName"), 160), item.DisplayName);
            item.ParticipantRole = FirstNonEmpty(NormalizeParticipantRole(PayloadReader.GetString(context.Request.Payload, "participantRole"), true), item.ParticipantRole);
            item.ContributionMode = FirstNonEmpty(NormalizeContributionMode(PayloadReader.GetString(context.Request.Payload, "contributionMode"), true), item.ContributionMode);
            if (context.Request.Payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible");
        }, ProjectParticipantsEnabled);

    public ResponseEnvelope ProjectParticipantRemove(CommandContext context)
        => UpdateProjectChild<ProjectParticipantState>(context, _repositories.ProjectParticipants, "participant", ProjectParticipantPayload, item => item.RemovedAtUtc = DateTime.UtcNow, ProjectParticipantsEnabled);

    public ResponseEnvelope ProjectRequirementAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectRequirementsEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        var requirement = new ProjectRequirementState
        {
            ProjectId = project.Id,
            CampaignId = project.CampaignId,
            RequirementType = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "requirementType"), "generic"), 1, 64, "requirementType"),
            Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), "Requirement"), 1, 160, "name"),
            PublicSummary = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicSummary"), 1024),
            GMSummary = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmSummary"), 2048),
            IsRequired = !context.Request.Payload.ContainsKey("isRequired") || PayloadReader.GetBool(context.Request.Payload, "isRequired"),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible") || !context.Request.Payload.ContainsKey("isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode")),
            PublicNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicNotes"), 2048),
            GMNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 4096)
        };
        _repositories.ProjectRequirements.Insert(requirement);
        AddProjectAudit(project, actor.Id, "requirement.add", $"Requirement added: {requirement.Name}", $"Requirement added: {requirement.Name}", requirement.IsPlayerVisible);
        return Ok("Project requirement added.", new Dictionary<string, object> { { "requirement", ProjectRequirementPayload(requirement, true) } });
    }

    public ResponseEnvelope ProjectRequirementVerify(CommandContext context) => SetRequirementStatus(context, ProjectRequirementStatusIds.Satisfied, "requirement.verify");
    public ResponseEnvelope ProjectRequirementWaive(CommandContext context) => SetRequirementStatus(context, ProjectRequirementStatusIds.Waived, "requirement.waive");

    public ResponseEnvelope ProjectResourceAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectResourceRequirementsEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        var resource = new ProjectResourceRequirementState
        {
            ProjectId = project.Id,
            CampaignId = project.CampaignId,
            ResourceType = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "resourceType"), "generic"), 1, 64, "resourceType"),
            ResourceId = RequireLength(PayloadReader.GetString(context.Request.Payload, "resourceId"), 0, 128, "resourceId"),
            DisplayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "displayName"), "Resource"), 1, 160, "displayName"),
            QuantityRequired = Math.Max(0m, (decimal)(PayloadReader.GetDouble(context.Request.Payload, "quantityRequired") ?? 0d)),
            Unit = RequireLength(PayloadReader.GetString(context.Request.Payload, "unit"), 0, 32, "unit"),
            IsReservationOnly = true,
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible") || !context.Request.Payload.ContainsKey("isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode")),
            UpdatedByUserId = actor.Id,
            PublicNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicNotes"), 2048),
            GMNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 4096)
        };
        _repositories.ProjectResourceRequirements.Insert(resource);
        AddProjectAudit(project, actor.Id, "resource.add", $"Resource requirement added: {resource.DisplayName}", $"Resource requirement added: {resource.DisplayName}", resource.IsPlayerVisible);
        return Ok("Project resource requirement added.", new Dictionary<string, object> { { "resource", ProjectResourcePayload(resource, true) } });
    }

    public ResponseEnvelope ProjectResourceMarkReserved(CommandContext context) => SetResourceStatus(context, ProjectResourceRequirementStatusIds.Reserved, "resource.reserve");
    public ResponseEnvelope ProjectResourceMarkConsumed(CommandContext context) => SetResourceStatus(context, ProjectResourceRequirementStatusIds.ConsumedManually, "resource.consume.manual_boundary");

    public ResponseEnvelope ProjectProgressAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectProgressEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        var deltaPercent = PayloadReader.GetInt(context.Request.Payload, "progressDeltaPercent") ?? 0;
        var deltaWork = PayloadReader.GetInt(context.Request.Payload, "workPointsDelta") ?? 0;
        project.ProgressPercent = Math.Max(0, Math.Min(100, project.ProgressPercent + deltaPercent));
        project.WorkPointsDone = Math.Max(0, project.WorkPointsDone + deltaWork);
        TouchProject(project, actor.Id);
        _repositories.Projects.Replace(project);
        var entry = new ProjectProgressEntryState
        {
            ProjectId = project.Id,
            CampaignId = project.CampaignId,
            StageId = RequireLength(PayloadReader.GetString(context.Request.Payload, "stageId"), 0, 128, "stageId"),
            EntryType = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "entryType"), "manual"), 1, 64, "entryType"),
            Summary = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "summary"), 2048),
            PublicSummary = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicSummary"), 1024),
            ProgressDeltaPercent = deltaPercent,
            WorkPointsDelta = deltaWork,
            ResultProgressPercent = project.ProgressPercent,
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible") || !context.Request.Payload.ContainsKey("isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode")),
            CreatedByUserId = actor.Id,
            GMNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 4096)
        };
        _repositories.ProjectProgressEntries.Insert(entry);
        AddProjectAudit(project, actor.Id, "progress.add", "Project progress updated.", FirstNonEmpty(entry.PublicSummary, "Project progress updated."), entry.IsPlayerVisible);
        TryPublishProjectSync(project, "progress.add", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Project progress added.", new Dictionary<string, object> { { "item", ProjectPayload(project, true, true) }, { "progress", ProjectProgressPayload(entry, true) } });
    }

    public ResponseEnvelope ProjectApprovalCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectApprovalsEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        var approval = new ProjectApprovalState
        {
            ProjectId = project.Id,
            CampaignId = project.CampaignId,
            ApprovalType = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "approvalType"), "gm_review"), 1, 64, "approvalType"),
            Status = ProjectApprovalStatusIds.PendingGmReview,
            RequestedByUserId = actor.Id,
            PublicSummary = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicSummary"), 1024),
            GMSummary = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmSummary"), 2048),
            PublicNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicNotes"), 2048),
            GMNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 4096),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible") || !context.Request.Payload.ContainsKey("isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode"))
        };
        project.ApprovalStatus = ProjectApprovalStatusIds.PendingGmReview;
        TouchProject(project, actor.Id);
        _repositories.ProjectApprovals.Insert(approval);
        _repositories.Projects.Replace(project);
        AddProjectAudit(project, actor.Id, "approval.create", "Approval requested.", "Approval requested.", approval.IsPlayerVisible);
        return Ok("Project approval created.", new Dictionary<string, object> { { "approval", ProjectApprovalPayload(approval, true) }, { "item", ProjectPayload(project, true, true) } });
    }

    public ResponseEnvelope ProjectApprovalResolve(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectApprovalsEnabled()) return ProjectDisabled(context.Request.Command);
        var approvalId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "approvalId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "approvalId");
        var approval = _repositories.ProjectApprovals.GetById(approvalId) ?? throw new KeyNotFoundException("Project approval not found.");
        var status = NormalizeProjectApprovalStatus(PayloadReader.GetString(context.Request.Payload, "status"), allowEmpty: false);
        approval.Status = status;
        approval.ReviewedByUserId = actor.Id;
        approval.ReviewedAtUtc = DateTime.UtcNow;
        approval.PublicNotes = FirstNonEmpty(SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicNotes"), 2048), approval.PublicNotes);
        approval.GMNotes = FirstNonEmpty(SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 4096), approval.GMNotes);
        _repositories.ProjectApprovals.Replace(approval);
        var project = _repositories.Projects.GetById(approval.ProjectId);
        if (project != null)
        {
            project.ApprovalStatus = status;
            if (status == ProjectApprovalStatusIds.Approved)
            {
                project.Status = ProjectStatusIds.Approved;
                project.ApprovedAtUtc = DateTime.UtcNow;
            }
            TouchProject(project, actor.Id);
            _repositories.Projects.Replace(project);
            AddProjectAudit(project, actor.Id, "approval.resolve", "Approval resolved: " + status, "Approval resolved.", approval.IsPlayerVisible);
        }
        return Ok("Project approval resolved.", new Dictionary<string, object> { { "approval", ProjectApprovalPayload(approval, true) } });
    }

    public ResponseEnvelope ProjectLinkAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProjectBaseEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        var link = new ProjectEntityLinkState
        {
            ProjectId = project.Id,
            CampaignId = project.CampaignId,
            LinkType = NormalizeProjectLinkType(PayloadReader.GetString(context.Request.Payload, "linkType")),
            EntityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityId"), 0, 128, "entityId"),
            DisplayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "displayName"), "Link"), 1, 160, "displayName"),
            LinkRole = RequireLength(PayloadReader.GetString(context.Request.Payload, "linkRole"), 0, 64, "linkRole"),
            IsPrimary = PayloadReader.GetBool(context.Request.Payload, "isPrimary"),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible") || !context.Request.Payload.ContainsKey("isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode")),
            CreatedByUserId = actor.Id,
            PublicNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "publicNotes"), 2048),
            GMNotes = SafeProjectText(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 4096)
        };
        _repositories.ProjectEntityLinks.Insert(link);
        AddProjectAudit(project, actor.Id, "link.add", $"Link added: {link.LinkType}:{link.EntityId}", $"Link added: {link.DisplayName}", link.IsPlayerVisible);
        return Ok("Project link added.", new Dictionary<string, object> { { "link", ProjectLinkPayload(link, true) } });
    }

    public ResponseEnvelope ProjectAuditList(CommandContext context)
    {
        RequireAdmin(context);
        if (!ProjectAuditEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        var items = _repositories.ProjectAuditEntries.Find(Builders<ProjectAuditEntryState>.Filter.Eq(x => x.ProjectId, project.Id))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .Select(x => (object)ProjectAuditPayload(x, includeAdminFields: true))
            .ToArray();
        return Ok("Project audit loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProjectPlayerList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProjectPlayerEnabled()) return ProjectDisabled(context.Request.Command);
        var campaignId = RequireLength(PayloadReader.GetString(context.Request.Payload, "campaignId"), 0, 128, "campaignId");
        var filter = Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false)
            & Builders<ProjectBaseState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.Hidden)
            & (Builders<ProjectBaseState>.Filter.Eq(x => x.IsPlayerVisible, true) | Builders<ProjectBaseState>.Filter.Eq(x => x.OwnerUserId, actor.Id));
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.CampaignId, campaignId);
        var items = _repositories.Projects.Find(filter)
            .Where(x => CanPlayerSeeProject(x, actor))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(200)
            .Select(x => (object)ProjectPayload(x, includeAdminFields: false, includeDetails: false))
            .ToArray();
        return Ok("Player projects loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProjectPlayerGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProjectPlayerEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireProject(context);
        if (!CanPlayerSeeProject(project, actor)) return Error("Project is not available to this player.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        return Ok("Player project loaded.", new Dictionary<string, object> { { "item", ProjectPayload(project, includeAdminFields: false, includeDetails: true) } });
    }

    public ResponseEnvelope ProjectPlayerDraftCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProjectPlayerEnabled()) return ProjectDisabled(context.Request.Command);
        var project = BuildProjectFromPayload(context.Request.Payload, actor, isPlayerDraft: true);
        project.Status = ProjectStatusIds.Draft;
        project.ApprovalStatus = ProjectApprovalStatusIds.Draft;
        _repositories.Projects.Insert(project);
        var proposal = new ProjectProposalBoundaryState
        {
            CampaignId = project.CampaignId,
            ProjectId = project.Id,
            ProposalType = project.ProjectType,
            Title = project.Name,
            PublicSummary = project.PublicSummary,
            Status = ProjectApprovalStatusIds.Draft,
            CreatedByUserId = actor.Id,
            DraftPayload = SanitizeProjectDraftPayload(context.Request.Payload)
        };
        _repositories.ProjectProposals.Insert(proposal);
        AddProjectAudit(project, actor.Id, "player.draft.create", "Player project draft created.", "Draft created.", isPlayerVisible: true);
        return Ok("Project draft created.", new Dictionary<string, object> { { "item", ProjectPayload(project, false, true) } });
    }

    public ResponseEnvelope ProjectPlayerDraftUpdate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProjectPlayerEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireOwnProjectDraft(context, actor);
        UpdateProjectFromPayload(project, context.Request.Payload, actor.Id, adminUpdate: false);
        _repositories.Projects.Replace(project);
        var proposal = _repositories.ProjectProposals.Find(Builders<ProjectProposalBoundaryState>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        if (proposal != null)
        {
            proposal.Title = project.Name;
            proposal.PublicSummary = project.PublicSummary;
            proposal.UpdatedAtUtc = DateTime.UtcNow;
            proposal.DraftPayload = SanitizeProjectDraftPayload(context.Request.Payload);
            _repositories.ProjectProposals.Replace(proposal);
        }
        AddProjectAudit(project, actor.Id, "player.draft.update", "Player project draft updated.", "Draft updated.", isPlayerVisible: true);
        return Ok("Project draft updated.", new Dictionary<string, object> { { "item", ProjectPayload(project, false, true) } });
    }

    public ResponseEnvelope ProjectPlayerDraftSubmit(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProjectPlayerEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireOwnProjectDraft(context, actor);
        project.Status = ProjectStatusIds.Submitted;
        project.ApprovalStatus = ProjectApprovalStatusIds.PendingGmReview;
        project.SubmittedAtUtc = DateTime.UtcNow;
        TouchProject(project, actor.Id);
        _repositories.Projects.Replace(project);
        AddProjectAudit(project, actor.Id, "player.draft.submit", "Player project draft submitted.", "Draft submitted to GM.", isPlayerVisible: true);
        TryPublishProjectSync(project, "player.draft.submit", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Project draft submitted.", new Dictionary<string, object> { { "item", ProjectPayload(project, false, true) } });
    }

    public ResponseEnvelope ProjectPlayerDraftCancel(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProjectPlayerEnabled()) return ProjectDisabled(context.Request.Command);
        var project = RequireOwnPlayerProject(context, actor);
        if (project.Status != ProjectStatusIds.Draft && project.Status != ProjectStatusIds.Submitted)
            return Error("Only draft or submitted projects can be cancelled by player.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        project.Status = ProjectStatusIds.Cancelled;
        TouchProject(project, actor.Id);
        _repositories.Projects.Replace(project);
        AddProjectAudit(project, actor.Id, "player.draft.cancel", "Player project draft cancelled.", "Draft cancelled.", isPlayerVisible: true);
        return Ok("Project draft cancelled.", new Dictionary<string, object> { { "item", ProjectPayload(project, false, true) } });
    }

    private ResponseEnvelope SetRequirementStatus(CommandContext context, string status, string auditAction)
    {
        var actor = RequireAdmin(context);
        if (!ProjectRequirementsEnabled()) return ProjectDisabled(context.Request.Command);
        var requirementId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "requirementId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "requirementId");
        var item = _repositories.ProjectRequirements.GetById(requirementId) ?? throw new KeyNotFoundException("Project requirement not found.");
        item.Status = status;
        item.VerifiedByUserId = actor.Id;
        item.VerifiedAtUtc = DateTime.UtcNow;
        _repositories.ProjectRequirements.Replace(item);
        var project = _repositories.Projects.GetById(item.ProjectId);
        if (project != null) AddProjectAudit(project, actor.Id, auditAction, $"Requirement status: {item.Name} -> {status}", $"Requirement updated: {item.Name}", item.IsPlayerVisible);
        return Ok("Project requirement updated.", new Dictionary<string, object> { { "requirement", ProjectRequirementPayload(item, true) } });
    }

    private ResponseEnvelope SetResourceStatus(CommandContext context, string status, string auditAction)
    {
        var actor = RequireAdmin(context);
        if (!ProjectResourceRequirementsEnabled()) return ProjectDisabled(context.Request.Command);
        var resourceId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "resourceRequirementId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "resourceRequirementId");
        var item = _repositories.ProjectResourceRequirements.GetById(resourceId) ?? throw new KeyNotFoundException("Project resource requirement not found.");
        item.Status = status;
        item.QuantityReserved = Math.Max(item.QuantityReserved, (decimal)(PayloadReader.GetDouble(context.Request.Payload, "quantityReserved") ?? (double)item.QuantityReserved));
        item.QuantityProvided = Math.Max(item.QuantityProvided, (decimal)(PayloadReader.GetDouble(context.Request.Payload, "quantityProvided") ?? (double)item.QuantityProvided));
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.ProjectResourceRequirements.Replace(item);
        var project = _repositories.Projects.GetById(item.ProjectId);
        if (project != null) AddProjectAudit(project, actor.Id, auditAction, $"Resource status: {item.DisplayName} -> {status}", $"Resource updated: {item.DisplayName}", item.IsPlayerVisible);
        return Ok("Project resource requirement updated.", new Dictionary<string, object> { { "resource", ProjectResourcePayload(item, true) } });
    }

    private ResponseEnvelope UpdateProjectChild<T>(CommandContext context, IRepository<T> repository, string payloadName, Func<T, bool, Dictionary<string, object>> payload, Action<T> update, Func<bool> enabled) where T : EntityBase
    {
        var actor = RequireAdmin(context);
        if (!enabled()) return ProjectDisabled(context.Request.Command);
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, payloadName + "Id"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, payloadName + "Id");
        var item = repository.GetById(id) ?? throw new KeyNotFoundException("Project " + payloadName + " not found.");
        update(item);
        repository.Replace(item);
        var projectId = GetProjectIdFromChild(item);
        var project = string.IsNullOrWhiteSpace(projectId) ? null : _repositories.Projects.GetById(projectId);
        if (project != null) AddProjectAudit(project, actor.Id, payloadName + ".update", "Project " + payloadName + " updated.", "Project item updated.", false);
        return Ok("Project " + payloadName + " updated.", new Dictionary<string, object> { { payloadName, payload(item, true) } });
    }

    private ProjectBaseState BuildProjectFromPayload(IDictionary<string, object> payload, UserAccount actor, bool isPlayerDraft)
    {
        var now = DateTime.UtcNow;
        var name = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "name"), PayloadReader.GetString(payload, "title"), "Project draft"), 2, 160, "name");
        return new ProjectBaseState
        {
            CampaignId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), "default"), 1, 128, "campaignId"),
            RuleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId"), 0, 128, "ruleSetId"),
            SessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId"),
            ActiveGroupId = RequireLength(PayloadReader.GetString(payload, "activeGroupId"), 0, 128, "activeGroupId"),
            ProjectType = NormalizeProjectType(PayloadReader.GetString(payload, "projectType")),
            Name = name,
            PublicSummary = SafeProjectText(FirstNonEmpty(PayloadReader.GetString(payload, "publicSummary"), PayloadReader.GetString(payload, "description")), 2048),
            GMSummary = isPlayerDraft ? string.Empty : SafeProjectText(PayloadReader.GetString(payload, "gmSummary"), 4096),
            ProgressMode = NormalizeProjectProgressMode(PayloadReader.GetString(payload, "progressMode")),
            ResultStatus = ProjectResultStatusIds.Expected,
            ResultApplicationMode = NormalizeProjectResultApplicationMode(PayloadReader.GetString(payload, "resultApplicationMode")),
            WorkPointsRequired = Math.Max(0, PayloadReader.GetInt(payload, "workPointsRequired") ?? 0),
            OwnerUserId = isPlayerDraft ? actor.Id : RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "ownerUserId"), actor.Id), 0, 128, "ownerUserId"),
            OwnerDisplayName = FirstNonEmpty(PayloadReader.GetString(payload, "ownerDisplayName"), actor.Login, actor.Id),
            OwnerCharacterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 0, 128, "characterId"),
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            AssignedGmUserId = isPlayerDraft ? string.Empty : RequireLength(PayloadReader.GetString(payload, "assignedGmUserId"), 0, 128, "assignedGmUserId"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(payload, "visibilityMode")),
            IsPlayerVisible = isPlayerDraft || PayloadReader.GetBool(payload, "isPlayerVisible") || !payload.ContainsKey("isPlayerVisible"),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            PublicNotes = SafeProjectText(PayloadReader.GetString(payload, "publicNotes"), 2048),
            GMNotes = isPlayerDraft ? string.Empty : SafeProjectText(PayloadReader.GetString(payload, "gmNotes"), 4096),
            ProposalPayload = SanitizeProjectDraftPayload(payload)
        };
    }

    private void UpdateProjectFromPayload(ProjectBaseState project, IDictionary<string, object> payload, string actorId, bool adminUpdate)
    {
        project.Name = FirstNonEmpty(SafeProjectText(PayloadReader.GetString(payload, "name"), 160), project.Name);
        project.PublicSummary = FirstNonEmpty(SafeProjectText(FirstNonEmpty(PayloadReader.GetString(payload, "publicSummary"), PayloadReader.GetString(payload, "description")), 2048), project.PublicSummary);
        if (adminUpdate)
        {
            project.GMSummary = FirstNonEmpty(SafeProjectText(PayloadReader.GetString(payload, "gmSummary"), 4096), project.GMSummary);
            project.GMNotes = FirstNonEmpty(SafeProjectText(PayloadReader.GetString(payload, "gmNotes"), 4096), project.GMNotes);
            if (payload.ContainsKey("isPlayerVisible")) project.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
            project.VisibilityMode = FirstNonEmpty(NormalizeProjectVisibility(PayloadReader.GetString(payload, "visibilityMode"), allowEmpty: true), project.VisibilityMode);
        }
        project.ProjectType = FirstNonEmpty(NormalizeProjectType(PayloadReader.GetString(payload, "projectType"), true), project.ProjectType);
        project.ProgressMode = FirstNonEmpty(NormalizeProjectProgressMode(PayloadReader.GetString(payload, "progressMode"), true), project.ProgressMode);
        project.ResultApplicationMode = FirstNonEmpty(NormalizeProjectResultApplicationMode(PayloadReader.GetString(payload, "resultApplicationMode"), true), project.ResultApplicationMode);
        project.WorkPointsRequired = Math.Max(0, PayloadReader.GetInt(payload, "workPointsRequired") ?? project.WorkPointsRequired);
        project.PublicNotes = FirstNonEmpty(SafeProjectText(PayloadReader.GetString(payload, "publicNotes"), 2048), project.PublicNotes);
        TouchProject(project, actorId);
    }

    private ProjectBaseState RequireProject(CommandContext context)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "projectId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "projectId");
        return _repositories.Projects.GetById(id) ?? throw new KeyNotFoundException("Project not found.");
    }

    private ProjectStageState RequireProjectStage(CommandContext context)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "stageId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "stageId");
        return _repositories.ProjectStages.GetById(id) ?? throw new KeyNotFoundException("Project stage not found.");
    }

    private ProjectBaseState RequireOwnProjectDraft(CommandContext context, UserAccount actor)
    {
        var project = RequireOwnPlayerProject(context, actor);
        if (project.Status != ProjectStatusIds.Draft) throw new InvalidOperationException("Only draft project can be updated or submitted.");
        return project;
    }

    private ProjectBaseState RequireOwnPlayerProject(CommandContext context, UserAccount actor)
    {
        var project = RequireProject(context);
        if (!string.Equals(project.OwnerUserId, actor.Id, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only project owner can change this draft.");
        return project;
    }

    private Dictionary<string, object> ProjectPayload(ProjectBaseState project, bool includeAdminFields, bool includeDetails)
    {
        var result = new Dictionary<string, object>
        {
            { "projectId", project.Id },
            { "campaignId", project.CampaignId },
            { "ruleSetId", project.RuleSetId },
            { "sessionId", project.SessionId },
            { "activeGroupId", project.ActiveGroupId },
            { "projectType", project.ProjectType },
            { "projectTypeLabel", ProjectTypeLabel(project.ProjectType) },
            { "name", project.Name },
            { "publicSummary", project.PublicSummary },
            { "status", project.Status },
            { "statusLabel", ProjectStatusLabel(project.Status) },
            { "approvalStatus", project.ApprovalStatus },
            { "progressMode", project.ProgressMode },
            { "progressPercent", project.ProgressPercent },
            { "workPointsDone", project.WorkPointsDone },
            { "workPointsRequired", project.WorkPointsRequired },
            { "currentStageId", project.CurrentStageId },
            { "currentStageName", project.CurrentStageName },
            { "resultStatus", project.ResultStatus },
            { "resultApplicationMode", project.ResultApplicationMode },
            { "ownerUserId", includeAdminFields ? project.OwnerUserId : string.Empty },
            { "ownerDisplayName", project.OwnerDisplayName },
            { "ownerCharacterId", project.OwnerCharacterId },
            { "isPlayerVisible", project.IsPlayerVisible },
            { "visibilityMode", includeAdminFields ? project.VisibilityMode : SafePlayerVisibility(project.VisibilityMode) },
            { "publicNotes", project.PublicNotes },
            { "createdAtUtc", project.CreatedAtUtc },
            { "updatedAtUtc", project.UpdatedAtUtc },
            { "submittedAtUtc", project.SubmittedAtUtc.HasValue ? (object)project.SubmittedAtUtc.Value : string.Empty },
            { "approvedAtUtc", project.ApprovedAtUtc.HasValue ? (object)project.ApprovedAtUtc.Value : string.Empty },
            { "completedAtUtc", project.CompletedAtUtc.HasValue ? (object)project.CompletedAtUtc.Value : string.Empty },
            { "isArchived", project.IsArchived }
        };
        if (includeAdminFields)
        {
            result["gmSummary"] = project.GMSummary;
            result["gmNotes"] = project.GMNotes;
            result["assignedGmUserId"] = project.AssignedGmUserId;
            result["createdByUserId"] = project.CreatedByUserId;
            result["updatedByUserId"] = project.UpdatedByUserId;
            result["expectedResultSummary"] = project.ExpectedResultSummary;
        }
        if (includeDetails)
        {
            result["stages"] = LoadProjectStages(project.Id, includeAdminFields).Cast<object>().ToArray();
            result["participants"] = LoadProjectParticipants(project.Id, includeAdminFields).Cast<object>().ToArray();
            result["requirements"] = LoadProjectRequirements(project.Id, includeAdminFields).Cast<object>().ToArray();
            result["resources"] = LoadProjectResources(project.Id, includeAdminFields).Cast<object>().ToArray();
            result["progress"] = LoadProjectProgress(project.Id, includeAdminFields).Cast<object>().ToArray();
            result["approvals"] = LoadProjectApprovals(project.Id, includeAdminFields).Cast<object>().ToArray();
            result["links"] = LoadProjectLinks(project.Id, includeAdminFields).Cast<object>().ToArray();
        }
        return result;
    }

    private IEnumerable<Dictionary<string, object>> LoadProjectStages(string projectId, bool includeAdminFields) => _repositories.ProjectStages.Find(Builders<ProjectStageState>.Filter.Eq(x => x.ProjectId, projectId)).OrderBy(x => x.SortOrder).Where(x => includeAdminFields || IsProjectItemPlayerVisible(x.IsPlayerVisible, x.VisibilityMode)).Select(x => ProjectStagePayload(x, includeAdminFields));
    private IEnumerable<Dictionary<string, object>> LoadProjectParticipants(string projectId, bool includeAdminFields) => _repositories.ProjectParticipants.Find(Builders<ProjectParticipantState>.Filter.Eq(x => x.ProjectId, projectId)).Where(x => x.RemovedAtUtc == null).Where(x => includeAdminFields || IsProjectItemPlayerVisible(x.IsPlayerVisible, x.VisibilityMode)).Select(x => ProjectParticipantPayload(x, includeAdminFields));
    private IEnumerable<Dictionary<string, object>> LoadProjectRequirements(string projectId, bool includeAdminFields) => _repositories.ProjectRequirements.Find(Builders<ProjectRequirementState>.Filter.Eq(x => x.ProjectId, projectId)).Where(x => includeAdminFields || IsProjectItemPlayerVisible(x.IsPlayerVisible, x.VisibilityMode)).Select(x => ProjectRequirementPayload(x, includeAdminFields));
    private IEnumerable<Dictionary<string, object>> LoadProjectResources(string projectId, bool includeAdminFields) => _repositories.ProjectResourceRequirements.Find(Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, projectId)).Where(x => includeAdminFields || IsProjectItemPlayerVisible(x.IsPlayerVisible, x.VisibilityMode)).Select(x => ProjectResourcePayload(x, includeAdminFields));
    private IEnumerable<Dictionary<string, object>> LoadProjectProgress(string projectId, bool includeAdminFields) => _repositories.ProjectProgressEntries.Find(Builders<ProjectProgressEntryState>.Filter.Eq(x => x.ProjectId, projectId)).OrderByDescending(x => x.CreatedAtUtc).Take(100).Where(x => includeAdminFields || IsProjectItemPlayerVisible(x.IsPlayerVisible, x.VisibilityMode)).Select(x => ProjectProgressPayload(x, includeAdminFields));
    private IEnumerable<Dictionary<string, object>> LoadProjectApprovals(string projectId, bool includeAdminFields) => _repositories.ProjectApprovals.Find(Builders<ProjectApprovalState>.Filter.Eq(x => x.ProjectId, projectId)).OrderByDescending(x => x.RequestedAtUtc).Where(x => includeAdminFields || IsProjectItemPlayerVisible(x.IsPlayerVisible, x.VisibilityMode)).Select(x => ProjectApprovalPayload(x, includeAdminFields));
    private IEnumerable<Dictionary<string, object>> LoadProjectLinks(string projectId, bool includeAdminFields) => _repositories.ProjectEntityLinks.Find(Builders<ProjectEntityLinkState>.Filter.Eq(x => x.ProjectId, projectId)).Where(x => includeAdminFields || IsProjectItemPlayerVisible(x.IsPlayerVisible, x.VisibilityMode)).Select(x => ProjectLinkPayload(x, includeAdminFields));

    private static Dictionary<string, object> ProjectStagePayload(ProjectStageState x, bool includeAdminFields)
    {
        var result = new Dictionary<string, object> { { "stageId", x.Id }, { "projectId", x.ProjectId }, { "stageType", x.StageType }, { "name", x.Name }, { "publicSummary", x.PublicSummary }, { "status", x.Status }, { "sortOrder", x.SortOrder }, { "progressPercent", x.ProgressPercent }, { "workPointsDone", x.WorkPointsDone }, { "workPointsRequired", x.WorkPointsRequired }, { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", includeAdminFields ? x.VisibilityMode : SafePlayerVisibility(x.VisibilityMode) }, { "publicNotes", x.PublicNotes } };
        if (includeAdminFields) { result["gmSummary"] = x.GMSummary; result["gmNotes"] = x.GMNotes; }
        return result;
    }

    private static Dictionary<string, object> ProjectParticipantPayload(ProjectParticipantState x, bool includeAdminFields)
    {
        var result = new Dictionary<string, object> { { "participantId", x.Id }, { "projectId", x.ProjectId }, { "entityType", x.EntityType }, { "entityId", includeAdminFields ? x.EntityId : string.Empty }, { "displayName", x.DisplayName }, { "participantRole", x.ParticipantRole }, { "contributionMode", x.ContributionMode }, { "isPrimary", x.IsPrimary }, { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", includeAdminFields ? x.VisibilityMode : SafePlayerVisibility(x.VisibilityMode) }, { "publicNotes", x.PublicNotes } };
        if (includeAdminFields) { result["ownerUserId"] = x.OwnerUserId; result["gmNotes"] = x.GMNotes; }
        return result;
    }

    private static Dictionary<string, object> ProjectRequirementPayload(ProjectRequirementState x, bool includeAdminFields)
    {
        var result = new Dictionary<string, object> { { "requirementId", x.Id }, { "projectId", x.ProjectId }, { "requirementType", x.RequirementType }, { "name", x.Name }, { "publicSummary", x.PublicSummary }, { "status", x.Status }, { "isRequired", x.IsRequired }, { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", includeAdminFields ? x.VisibilityMode : SafePlayerVisibility(x.VisibilityMode) }, { "publicNotes", x.PublicNotes } };
        if (includeAdminFields) { result["gmSummary"] = x.GMSummary; result["gmNotes"] = x.GMNotes; result["verifiedByUserId"] = x.VerifiedByUserId; }
        return result;
    }

    private static Dictionary<string, object> ProjectResourcePayload(ProjectResourceRequirementState x, bool includeAdminFields)
    {
        var result = new Dictionary<string, object> { { "resourceRequirementId", x.Id }, { "projectId", x.ProjectId }, { "resourceType", x.ResourceType }, { "resourceId", includeAdminFields ? x.ResourceId : string.Empty }, { "displayName", x.DisplayName }, { "quantityRequired", x.QuantityRequired }, { "quantityReserved", x.QuantityReserved }, { "quantityProvided", x.QuantityProvided }, { "unit", x.Unit }, { "status", x.Status }, { "isReservationOnly", x.IsReservationOnly }, { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", includeAdminFields ? x.VisibilityMode : SafePlayerVisibility(x.VisibilityMode) }, { "publicNotes", x.PublicNotes } };
        if (includeAdminFields) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> ProjectProgressPayload(ProjectProgressEntryState x, bool includeAdminFields)
    {
        var result = new Dictionary<string, object> { { "progressId", x.Id }, { "projectId", x.ProjectId }, { "stageId", x.StageId }, { "entryType", x.EntryType }, { "summary", includeAdminFields ? x.Summary : FirstNonEmpty(x.PublicSummary, x.Summary) }, { "publicSummary", x.PublicSummary }, { "progressDeltaPercent", x.ProgressDeltaPercent }, { "workPointsDelta", x.WorkPointsDelta }, { "resultProgressPercent", x.ResultProgressPercent }, { "createdAtUtc", x.CreatedAtUtc }, { "isPlayerVisible", x.IsPlayerVisible } };
        if (includeAdminFields) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> ProjectApprovalPayload(ProjectApprovalState x, bool includeAdminFields)
    {
        var result = new Dictionary<string, object> { { "approvalId", x.Id }, { "projectId", x.ProjectId }, { "approvalType", x.ApprovalType }, { "status", x.Status }, { "publicSummary", x.PublicSummary }, { "publicNotes", x.PublicNotes }, { "requestedAtUtc", x.RequestedAtUtc }, { "reviewedAtUtc", x.ReviewedAtUtc.HasValue ? (object)x.ReviewedAtUtc.Value : string.Empty }, { "isPlayerVisible", x.IsPlayerVisible } };
        if (includeAdminFields) { result["gmSummary"] = x.GMSummary; result["gmNotes"] = x.GMNotes; result["requestedByUserId"] = x.RequestedByUserId; result["reviewedByUserId"] = x.ReviewedByUserId; }
        return result;
    }

    private static Dictionary<string, object> ProjectLinkPayload(ProjectEntityLinkState x, bool includeAdminFields)
    {
        var result = new Dictionary<string, object> { { "linkId", x.Id }, { "projectId", x.ProjectId }, { "linkType", x.LinkType }, { "entityId", includeAdminFields ? x.EntityId : string.Empty }, { "displayName", x.DisplayName }, { "linkRole", x.LinkRole }, { "isPrimary", x.IsPrimary }, { "publicNotes", x.PublicNotes }, { "isPlayerVisible", x.IsPlayerVisible } };
        if (includeAdminFields) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> ProjectAuditPayload(ProjectAuditEntryState x, bool includeAdminFields)
    {
        var result = new Dictionary<string, object> { { "auditId", x.Id }, { "projectId", x.ProjectId }, { "actionType", x.ActionType }, { "createdAtUtc", x.CreatedAtUtc }, { "publicSummary", x.PublicSummary }, { "isPlayerVisible", x.IsPlayerVisible } };
        if (includeAdminFields) { result["actorUserId"] = x.ActorUserId; result["summary"] = x.Summary; }
        return result;
    }

    private void AddProjectAudit(ProjectBaseState project, string actorUserId, string action, string summary, string publicSummary, bool isPlayerVisible)
    {
        if (!ProjectAuditEnabled()) return;
        _repositories.ProjectAuditEntries.Insert(new ProjectAuditEntryState
        {
            ProjectId = project.Id,
            CampaignId = project.CampaignId,
            ActionType = action,
            ActorUserId = actorUserId,
            Summary = SafeProjectText(summary, 1024),
            PublicSummary = SafeProjectText(publicSummary, 512),
            IsPlayerVisible = isPlayerVisible,
            VisibilityMode = isPlayerVisible ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly
        });
    }

    private void RecalculateProjectProgress(ProjectBaseState project, string actorUserId)
    {
        var stages = _repositories.ProjectStages.Find(Builders<ProjectStageState>.Filter.Eq(x => x.ProjectId, project.Id)).ToList();
        if (stages.Count > 0)
        {
            project.ProgressPercent = (int)Math.Round(stages.Average(x => Math.Max(0, Math.Min(100, x.ProgressPercent))));
            var current = stages.OrderBy(x => x.SortOrder).FirstOrDefault(x => x.Status == ProjectStageStatusIds.Active || x.Status == ProjectStageStatusIds.Available);
            if (current != null)
            {
                project.CurrentStageId = current.Id;
                project.CurrentStageName = current.Name;
            }
        }
        TouchProject(project, actorUserId);
        _repositories.Projects.Replace(project);
    }

    private void TouchProject(ProjectBaseState project, string actorId)
    {
        project.UpdatedAtUtc = DateTime.UtcNow;
        project.UpdatedByUserId = actorId;
    }

    private void TryPublishProjectSync(ProjectBaseState project, string operation, string actorId, string requestId)
        => TryPublishSyncEvent("project.changed", project.CampaignId, "project", project.Id, operation, actorId, new Dictionary<string, object> { { "projectId", project.Id }, { "status", project.Status }, { "projectType", project.ProjectType } }, requestId);

    private void TryWriteProjectJournal(ProjectBaseState project, string sourceEventId, string title, string actorId)
    {
        try
        {
            if (!_featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectJournalIntegration)) ||
                !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp)) ||
                !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion)))
                return;

            _repositories.EventJournalEntries.Insert(new EventJournalEntryState
            {
                CampaignId = project.CampaignId,
                EntryType = EventJournalEntryTypeIds.Automatic,
                Category = EventJournalCategoryIds.Custom,
                Severity = EventJournalSeverityIds.Information,
                Title = title,
                Summary = FirstNonEmpty(project.PublicSummary, project.PublicNotes, project.Name),
                PlayerSummary = project.PublicSummary,
                GMDetails = project.GMSummary,
                SourceModule = "projects",
                SourceEventId = sourceEventId + ":" + project.Id,
                SourceEventType = "project",
                VisibilityMode = project.IsPlayerVisible ? EventJournalVisibilityModeIds.PlayerVisible : EventJournalVisibilityModeIds.GMOnly,
                IsPlayerVisible = project.IsPlayerVisible,
                IsAutomatic = true,
                ActorUserId = actorId,
                SubjectEntityType = "project",
                SubjectEntityId = project.Id,
                SubjectDisplayName = project.Name,
                CreatedByUserId = actorId,
                CreatedAtUtc = DateTime.UtcNow,
                OccurredAtUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.Debug($"project.journal.write.error projectId={project.Id} message={ex.Message}");
        }
    }

    private bool CanPlayerSeeProject(ProjectBaseState project, UserAccount actor)
    {
        if (project.IsArchived) return false;
        if (string.Equals(project.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(project.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase)) return string.Equals(project.OwnerUserId, actor.Id, StringComparison.Ordinal);
        if (string.Equals(project.VisibilityMode, ProjectVisibilityModeIds.OwnerOnly, StringComparison.OrdinalIgnoreCase)) return string.Equals(project.OwnerUserId, actor.Id, StringComparison.Ordinal);
        return project.IsPlayerVisible || string.Equals(project.OwnerUserId, actor.Id, StringComparison.Ordinal);
    }

    private static bool IsProjectItemPlayerVisible(bool isPlayerVisible, string visibilityMode)
        => isPlayerVisible
           && !string.Equals(visibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(visibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase);

    private int NextStageSortOrder(string projectId)
    {
        var stages = _repositories.ProjectStages.Find(Builders<ProjectStageState>.Filter.Eq(x => x.ProjectId, projectId));
        return stages.Count == 0 ? 10 : stages.Max(x => x.SortOrder) + 10;
    }

    private static Dictionary<string, object> SanitizeProjectDraftPayload(IDictionary<string, object> payload)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var pair in payload)
        {
            if (string.Equals(pair.Key, "gmNotes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "gmSummary", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "serverOnlyData", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "token", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "password", StringComparison.OrdinalIgnoreCase))
                continue;
            result[pair.Key] = pair.Value;
        }
        return result;
    }

    private string GetProjectIdFromChild(object item)
    {
        var prop = item.GetType().GetProperty("ProjectId");
        return Convert.ToString(prop?.GetValue(item)) ?? string.Empty;
    }

    private ResponseEnvelope ProjectDisabled(string command)
    {
        _logger.Admin($"project.command.disabled command={command}");
        return Error("Project foundation is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool ProjectFoundationEnabled() => _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectFoundationMvp));
    private bool ProjectBaseEnabled() => ProjectFoundationEnabled() && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectBaseV1));
    private bool ProjectAdminEnabled() => ProjectBaseEnabled() && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectAdminView));
    private bool ProjectPlayerEnabled() => ProjectBaseEnabled() && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectPlayerView));
    private bool ProjectStagesEnabled() => ProjectAdminEnabled() && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectStagesV1));
    private bool ProjectProgressEnabled() => ProjectAdminEnabled() && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectProgressV1));
    private bool ProjectParticipantsEnabled() => ProjectAdminEnabled() && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectParticipantsV1));
    private bool ProjectRequirementsEnabled() => ProjectAdminEnabled() && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectRequirementsV1));
    private bool ProjectResourceRequirementsEnabled() => ProjectAdminEnabled() && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectResourceRequirementsV1));
    private bool ProjectApprovalsEnabled() => ProjectAdminEnabled() && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectApprovalsV1));
    private bool ProjectAuditEnabled() => ProjectFoundationEnabled() && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectAuditV1));

    private static string NormalizeProjectType(string? value, bool allowEmpty = false)
    {
        var text = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(text)) return string.Empty;
        var allowed = new[] { ProjectTypeIds.Research, ProjectTypeIds.Crafting, ProjectTypeIds.EngineeringDesign, ProjectTypeIds.Manufacturing, ProjectTypeIds.FactoryOrder, ProjectTypeIds.Construction, ProjectTypeIds.Repair, ProjectTypeIds.Modification, ProjectTypeIds.ReverseEngineering, ProjectTypeIds.ProductionBatch, ProjectTypeIds.CustomProposal, ProjectTypeIds.Generic };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectTypeIds.Generic;
    }

    private static string NormalizeProjectStatus(string? value, bool allowEmpty = false)
    {
        var text = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(text)) return string.Empty;
        var allowed = new[] { ProjectStatusIds.Draft, ProjectStatusIds.Submitted, ProjectStatusIds.InReview, ProjectStatusIds.Approved, ProjectStatusIds.Preparation, ProjectStatusIds.WaitingResources, ProjectStatusIds.Active, ProjectStatusIds.Paused, ProjectStatusIds.Blocked, ProjectStatusIds.Testing, ProjectStatusIds.AwaitingAcceptance, ProjectStatusIds.Completed, ProjectStatusIds.Failed, ProjectStatusIds.Cancelled, ProjectStatusIds.Archived };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectStatusIds.Draft;
    }

    private static string NormalizeProjectApprovalStatus(string? value, bool allowEmpty = false)
    {
        var text = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(text)) return string.Empty;
        var allowed = new[] { ProjectApprovalStatusIds.NotRequired, ProjectApprovalStatusIds.Draft, ProjectApprovalStatusIds.PendingGmReview, ProjectApprovalStatusIds.ChangesRequested, ProjectApprovalStatusIds.Approved, ProjectApprovalStatusIds.Rejected, ProjectApprovalStatusIds.Revoked, ProjectApprovalStatusIds.Superseded };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectApprovalStatusIds.PendingGmReview;
    }

    private static string NormalizeProjectProgressMode(string? value, bool allowEmpty = false)
    {
        var text = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(text)) return string.Empty;
        var allowed = new[] { ProjectProgressModeIds.Manual, ProjectProgressModeIds.WorkPoints, ProjectProgressModeIds.StageBased, ProjectProgressModeIds.CalendarDuration, ProjectProgressModeIds.Hybrid };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectProgressModeIds.Manual;
    }

    private static string NormalizeProjectResultApplicationMode(string? value, bool allowEmpty = false)
    {
        var text = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(text)) return string.Empty;
        var allowed = new[] { ProjectResultApplicationModeIds.None, ProjectResultApplicationModeIds.GmManual, ProjectResultApplicationModeIds.CreateKnowledgeLater, ProjectResultApplicationModeIds.CreateRecipeLater, ProjectResultApplicationModeIds.CreateItemLater, ProjectResultApplicationModeIds.CreateAssetLater, ProjectResultApplicationModeIds.CreateBlueprintLater, ProjectResultApplicationModeIds.CreateProjectLater, ProjectResultApplicationModeIds.CustomLater };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectResultApplicationModeIds.None;
    }

    private static string NormalizeProjectStageType(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        var allowed = new[] { ProjectStageTypeIds.Concept, ProjectStageTypeIds.GmReview, ProjectStageTypeIds.Preparation, ProjectStageTypeIds.Research, ProjectStageTypeIds.Design, ProjectStageTypeIds.ResourceGathering, ProjectStageTypeIds.ResourceReservation, ProjectStageTypeIds.Crafting, ProjectStageTypeIds.Construction, ProjectStageTypeIds.Manufacturing, ProjectStageTypeIds.Prototype, ProjectStageTypeIds.Testing, ProjectStageTypeIds.Revision, ProjectStageTypeIds.Acceptance, ProjectStageTypeIds.Delivery, ProjectStageTypeIds.OperationStart, ProjectStageTypeIds.Custom };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectStageTypeIds.Custom;
    }

    private static string NormalizeProjectStageStatus(string? value, bool allowEmpty = false)
    {
        var text = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(text)) return string.Empty;
        var allowed = new[] { ProjectStageStatusIds.Locked, ProjectStageStatusIds.Available, ProjectStageStatusIds.Active, ProjectStageStatusIds.Completed, ProjectStageStatusIds.Skipped, ProjectStageStatusIds.Failed, ProjectStageStatusIds.Blocked, ProjectStageStatusIds.Cancelled };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectStageStatusIds.Available;
    }

    private static string NormalizeParticipantEntityType(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        var allowed = new[] { ProjectParticipantEntityTypeIds.PlayerCharacter, ProjectParticipantEntityTypeIds.Npc, ProjectParticipantEntityTypeIds.Companion, ProjectParticipantEntityTypeIds.CharacterGroup, ProjectParticipantEntityTypeIds.Organization, ProjectParticipantEntityTypeIds.Facility, ProjectParticipantEntityTypeIds.Specialist, ProjectParticipantEntityTypeIds.Custom };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectParticipantEntityTypeIds.Custom;
    }

    private static string NormalizeParticipantRole(string? value, bool allowEmpty = false)
    {
        var text = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(text)) return string.Empty;
        var allowed = new[] { ProjectParticipantRoleIds.ProjectOwner, ProjectParticipantRoleIds.Requester, ProjectParticipantRoleIds.LeadResearcher, ProjectParticipantRoleIds.LeadCrafter, ProjectParticipantRoleIds.LeadEngineer, ProjectParticipantRoleIds.Worker, ProjectParticipantRoleIds.Assistant, ProjectParticipantRoleIds.Consultant, ProjectParticipantRoleIds.Sponsor, ProjectParticipantRoleIds.Supplier, ProjectParticipantRoleIds.Inspector, ProjectParticipantRoleIds.GmReviewer, ProjectParticipantRoleIds.Custom };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectParticipantRoleIds.Custom;
    }

    private static string NormalizeContributionMode(string? value, bool allowEmpty = false)
    {
        var text = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(text)) return string.Empty;
        var allowed = new[] { ProjectContributionModeIds.ActiveWork, ProjectContributionModeIds.PassiveSupport, ProjectContributionModeIds.Funding, ProjectContributionModeIds.Supervision, ProjectContributionModeIds.KnowledgeSource, ProjectContributionModeIds.EquipmentProvider, ProjectContributionModeIds.FacilityProvider, ProjectContributionModeIds.LegalCover, ProjectContributionModeIds.Custom };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectContributionModeIds.Custom;
    }

    private static string NormalizeProjectLinkType(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        var allowed = new[] { ProjectLinkTypeIds.PlayerRequest, ProjectLinkTypeIds.Character, ProjectLinkTypeIds.Companion, ProjectLinkTypeIds.Organization, ProjectLinkTypeIds.Faction, ProjectLinkTypeIds.Location, ProjectLinkTypeIds.WorldCalendarEvent, ProjectLinkTypeIds.RealScheduleEvent, ProjectLinkTypeIds.InventoryItem, ProjectLinkTypeIds.Knowledge, ProjectLinkTypeIds.Blueprint, ProjectLinkTypeIds.SceneMap, ProjectLinkTypeIds.WorldMap, ProjectLinkTypeIds.Custom };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectLinkTypeIds.Custom;
    }

    private static string NormalizeProjectVisibility(string? value, bool allowEmpty = false)
    {
        var text = (value ?? string.Empty).Trim();
        if (allowEmpty && string.IsNullOrWhiteSpace(text)) return string.Empty;
        var allowed = new[] { ProjectVisibilityModeIds.GmOnly, ProjectVisibilityModeIds.PlayerVisible, ProjectVisibilityModeIds.Party, ProjectVisibilityModeIds.OwnerOnly, ProjectVisibilityModeIds.Hidden };
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : ProjectVisibilityModeIds.PlayerVisible;
    }

    private static string SafePlayerVisibility(string visibility)
        => string.Equals(visibility, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase) || string.Equals(visibility, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : visibility;

    private static string SafeProjectText(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text.Substring(0, max);
    }

    private static string ProjectTypeLabel(string type)
        => type switch
        {
            ProjectTypeIds.Research => "Исследование",
            ProjectTypeIds.Crafting => "Крафт",
            ProjectTypeIds.EngineeringDesign => "Инженерный проект",
            ProjectTypeIds.Manufacturing => "Производство",
            ProjectTypeIds.FactoryOrder => "Заказ фабрики",
            ProjectTypeIds.Construction => "Строительство",
            ProjectTypeIds.Repair => "Ремонт",
            ProjectTypeIds.Modification => "Модификация",
            ProjectTypeIds.ReverseEngineering => "Обратная разработка",
            ProjectTypeIds.ProductionBatch => "Партия производства",
            ProjectTypeIds.CustomProposal => "Особое предложение",
            _ => "Проект"
        };

    private static string ProjectStatusLabel(string status)
        => status switch
        {
            ProjectStatusIds.Draft => "Черновик",
            ProjectStatusIds.Submitted => "Отправлено GM",
            ProjectStatusIds.InReview => "На рассмотрении",
            ProjectStatusIds.Approved => "Одобрено",
            ProjectStatusIds.Preparation => "Подготовка",
            ProjectStatusIds.WaitingResources => "Ожидает ресурсы",
            ProjectStatusIds.Active => "В работе",
            ProjectStatusIds.Paused => "Пауза",
            ProjectStatusIds.Blocked => "Заблокировано",
            ProjectStatusIds.Testing => "Проверка",
            ProjectStatusIds.AwaitingAcceptance => "Ожидает принятия",
            ProjectStatusIds.Completed => "Завершено",
            ProjectStatusIds.Failed => "Провалено",
            ProjectStatusIds.Cancelled => "Отменено",
            ProjectStatusIds.Archived => "Архив",
            _ => status
        };
}
