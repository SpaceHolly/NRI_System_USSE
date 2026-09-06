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
    private static readonly object AssetConstructionRuntimeLock0197 = new();

    public ResponseEnvelope ProjectAssetConstructionAvailableList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetConstructionViewEnabled0197(admin))
            return AssetConstructionDisabled0197(context.Request.Command);

        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId") ?? string.Empty;
        var ownership = RequireConstructionOwnership0197(characterId, actor, admin);
        var blueprints = _repositories.AssetConfigurationBlueprints.Find(
                Builders<AssetConfigurationBlueprintState>.Filter.Eq(x => x.ConfiguratorKind, AssetConfiguratorKindIds.Building)
                & Builders<AssetConfigurationBlueprintState>.Filter.Eq(x => x.Status, AssetBlueprintStatusIds.Ready)
                & Builders<AssetConfigurationBlueprintState>.Filter.Eq(x => x.Archived, false))
            .Where(x => admin || string.Equals(x.Visibility, AssetBlueprintVisibilityIds.Shared, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.ServerCalculation != null && x.ServerCalculation.IsValid && x.Configuration?.Building != null)
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => (object)AssetConstructionBlueprintCandidatePayload0197(x))
            .ToArray();
        var locations = _repositories.MapSpaceNodes.ListByCampaignAsync(ownership.CampaignId, 500).GetAwaiter().GetResult()
            .Where(x => admin || IsPlayerSafeConstructionLocation0197(x))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["reference"] = x.Id,
                ["name"] = x.Name,
                ["type"] = x.NodeType,
                ["summary"] = FirstNonEmpty(x.Description, "Доступное место строительства")
            })
            .ToArray();
        return Ok("Asset-construction candidates loaded.", new Dictionary<string, object>
        {
            ["blueprints"] = blueprints,
            ["locations"] = locations,
            ["ownerDisplayName"] = ownership.CharacterDisplayName,
            ["warning"] = "Это крупный актив, а не предмет инвентаря."
        });
    }

    public ResponseEnvelope ProjectAssetConstructionPreview(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetConstructionViewEnabled0197(admin))
            return AssetConstructionDisabled0197(context.Request.Command);
        var snapshot = BuildAssetConstructionSnapshot0197(context.Request.Payload, actor, admin);
        return Ok("Asset-construction preview prepared.", new Dictionary<string, object>
        {
            ["preview"] = AssetConstructionPreviewPayload0197(snapshot)
        });
    }

    public ResponseEnvelope ProjectAssetConstructionCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetConstructionViewEnabled0197(admin))
            return AssetConstructionDisabled0197(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (AssetConstructionRuntimeLock0197)
        {
            var replay = _repositories.Projects.Find(
                    Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedOperationId, operationId)
                    & Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedByUserId, actor.Id))
                .FirstOrDefault();
            if (replay != null)
                return Ok("Asset-construction project already created.", AssetConstructionResponse0197(replay, admin, true));

            var snapshot = BuildAssetConstructionSnapshot0197(context.Request.Payload, actor, admin);
            var now = DateTime.UtcNow;
            var definitionSnapshot = new ProjectDefinitionSnapshot0191
            {
                BlueprintDefinitionId = snapshot.BlueprintId,
                BlueprintStableKey = snapshot.BlueprintStableKey,
                BlueprintRevision = snapshot.BlueprintRevision,
                BlueprintName = snapshot.BlueprintName,
                BlueprintKind = snapshot.AssetKind,
                ProjectTemplateStableKey = snapshot.ProjectTemplateKey,
                ProjectTemplateName = snapshot.ProjectTemplateName,
                Inputs = snapshot.Materials.Select(CloneConstructionMaterial0197).ToList(),
                Stages = snapshot.Stages.Select(x => new ProjectStageSnapshot0191
                {
                    Key = x.Key,
                    DisplayName = x.DisplayName,
                    Order = x.Order,
                    RequiresGMDecision = true,
                    IsPlayerVisible = true,
                    PublicSummary = x.PublicSummary
                }).ToList(),
                Requirements = snapshot.Requirements.Select(CloneConstructionRequirement0197).ToList(),
                AssetConstruction = snapshot
            };
            definitionSnapshot.SnapshotChecksum = ComputeSnapshotChecksum0191(definitionSnapshot);
            snapshot.SnapshotChecksum = definitionSnapshot.SnapshotChecksum;
            var projectOwnership = RequireConstructionOwnership0197(snapshot.TargetOwnerId, actor, admin);

            var project = new ProjectBaseState
            {
                CampaignId = RequireLength(projectOwnership.CampaignId, 1, 128, "campaignId"),
                RuleSetId = snapshot.RuleSetId,
                ProjectType = ProjectTypeIds.Construction,
                RuntimeKind = AssetConstructionRuntimeIds0197.RuntimeKind,
                Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"),
                    "Строительство: " + snapshot.BlueprintName), 2, 180, "name"),
                PublicSummary = $"Строительство здания «{snapshot.BlueprintName}» в месте «{snapshot.LocationName}».",
                GMSummary = "Server-authoritative крупный актив; итог не является предметом инвентаря.",
                Status = ProjectStatusIds.Draft,
                ApprovalStatus = ProjectApprovalStatusIds.Draft,
                ProgressMode = ProjectProgressModeIds.StageBased,
                ResultStatus = ProjectResultStatusIds.Expected,
            ResultApplicationMode = ProjectResultApplicationModeIds.CreateAssetLater,
                OwnerUserId = projectOwnership.OwnerUserId,
                OwnerDisplayName = snapshot.TargetOwnerDisplayName,
                OwnerCharacterId = snapshot.TargetOwnerId,
                CreatedByUserId = actor.Id,
                UpdatedByUserId = actor.Id,
                VisibilityMode = ProjectVisibilityModeIds.OwnerOnly,
                IsPlayerVisible = true,
                CreatedOperationId = operationId,
                LastOperationId = operationId,
                LastOperationCommand = CommandNames.ProjectAssetConstructionCreate,
                DefinitionSnapshot = definitionSnapshot,
                WorkPointsRequired = snapshot.Stages.Count,
                Revision = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ExpectedResultSummary = new Dictionary<string, object>
                {
                    ["kind"] = snapshot.ExpectedAssetKind,
                    ["label"] = snapshot.BlueprintName,
                    ["locationName"] = snapshot.LocationName
                }
            };
            _repositories.Projects.Insert(project);
            CreateAssetConstructionChildren0197(project, snapshot, actor.Id);
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.created",
                "Создан черновик строительства крупного актива.", true);
            TryPublishProjectSync(project, "asset.construction.created", actor.Id, context.Request.RequestId ?? string.Empty);
            return Ok("Asset-construction project created.", AssetConstructionResponse0197(project, admin));
        }
    }

    public ResponseEnvelope ProjectAssetConstructionSubmit(CommandContext context)
        => MutateAssetConstruction0197(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status != ProjectStatusIds.Draft && project.Status != ProjectStatusIds.RequirementsReview)
                throw new InvalidOperationException("Отправить можно только черновик строительства.");
            project.Status = ProjectStatusIds.AwaitingApproval;
            project.ApprovalStatus = ProjectApprovalStatusIds.PendingGmReview;
            project.SubmittedAtUtc = DateTime.UtcNow;
            if (!_repositories.ProjectApprovals.Find(Builders<ProjectApprovalState>.Filter.Eq(x => x.ProjectId, project.Id)).Any())
            {
                _repositories.ProjectApprovals.Insert(new ProjectApprovalState
                {
                    Id = "asset_construction_approval_" + project.Id,
                    ProjectId = project.Id,
                    CampaignId = project.CampaignId,
                    ApprovalType = "gm_asset_construction",
                    Status = ProjectApprovalStatusIds.PendingGmReview,
                    RequestedByUserId = actor.Id,
                    PublicSummary = "Строительство ожидает решения GM.",
                    GMSummary = "Проверьте место, метод, специалистов, оборудование, лицензию и ресурсы.",
                    IsPlayerVisible = true
                });
            }
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.submitted",
                "Проект строительства отправлен GM.", true);
        }, "Asset-construction project submitted.");

    public ResponseEnvelope ProjectAssetConstructionList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetConstructionViewEnabled0197(admin))
            return AssetConstructionDisabled0197(context.Request.Command);
        var filter = Builders<ProjectBaseState>.Filter.Eq(x => x.RuntimeKind, AssetConstructionRuntimeIds0197.RuntimeKind)
                     & Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false);
        if (!admin) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.OwnerUserId, actor.Id);
        var items = _repositories.Projects.Find(filter).OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)AssetConstructionProjectPayload0197(x, admin, false)).ToArray();
        return Ok("Asset-construction projects loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectAssetConstructionGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetConstructionViewEnabled0197(admin))
            return AssetConstructionDisabled0197(context.Request.Command);
        var project = RequireAssetConstructionProject0197(context.Request.Payload);
        RequireOwnerOrAdmin0191(project, actor, admin);
        return Ok("Asset-construction project loaded.", AssetConstructionResponse0197(project, admin));
    }

    public ResponseEnvelope ProjectAssetConstructionRequirementConfirm(CommandContext context)
        => MutateAssetConstruction0197(context, true, (project, actor, _, operationId) =>
        {
            var requirementId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requirementId"), 1, 160, "requirementId");
            var requirement = _repositories.ProjectRequirements.GetById(requirementId)
                ?? throw new KeyNotFoundException("Условие строительства не найдено.");
            if (!string.Equals(requirement.ProjectId, project.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("Условие относится к другому проекту.");
            requirement.Status = ProjectRequirementStatusIds.Satisfied;
            requirement.VerifiedByUserId = actor.Id;
            requirement.VerifiedAtUtc = DateTime.UtcNow;
            requirement.PublicNotes = "Условие подтверждено GM.";
            requirement.GMNotes = RequireLength(PayloadReader.GetString(context.Request.Payload, "gmNote"), 0, 1024, "gmNote");
            _repositories.ProjectRequirements.Replace(requirement);
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.requirement.confirmed",
                "Подтверждено условие: " + requirement.Name, true);
        }, "Asset-construction requirement confirmed.");

    public ResponseEnvelope ProjectAssetConstructionApprove(CommandContext context)
        => MutateAssetConstruction0197(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Проект не ожидает решения GM.");
            var open = _repositories.ProjectRequirements.Find(Builders<ProjectRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
                .Where(x => x.IsRequired && x.Status != ProjectRequirementStatusIds.Satisfied).ToArray();
            if (open.Length > 0)
                throw new InvalidOperationException("Не подтверждены обязательные условия: " + string.Join(", ", open.Select(x => x.Name)));
            project.Status = ProjectStatusIds.Approved;
            project.ApprovalStatus = ProjectApprovalStatusIds.Approved;
            project.ApprovedAtUtc = DateTime.UtcNow;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Approved, "Строительство актива одобрено.");
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.approved",
                "GM одобрил строительство актива.", true);
        }, "Asset-construction project approved.");

    public ResponseEnvelope ProjectAssetConstructionReject(CommandContext context)
        => MutateAssetConstruction0197(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval)
                throw new InvalidOperationException("Проект не ожидает решения GM.");
            var reason = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "publicReason"),
                "Строительство отклонено GM."), 1, 512, "publicReason");
            project.Status = ProjectStatusIds.Failed;
            project.ApprovalStatus = ProjectApprovalStatusIds.Rejected;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Rejected, reason);
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.rejected", reason, true);
        }, "Asset-construction project rejected.");

    public ResponseEnvelope ProjectAssetConstructionReserve(CommandContext context)
        => MutateAssetConstruction0197(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.Approved)
                throw new InvalidOperationException("Ресурсы можно резервировать только после одобрения.");
            ReserveAssetConstructionResources0197(project, actor.Id, operationId);
            var site = EnsureConstructionSite0197(project, actor.Id);
            site.Status = ConstructionSiteStatusIds0197.ResourcesReserved;
            site.UpdatedAtUtc = DateTime.UtcNow;
            site.UpdatedByUserId = actor.Id;
            site.Revision++;
            _repositories.ConstructionSites0197.Replace(site);
            project.Status = ProjectStatusIds.ResourcesReserved;
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.resources.reserved",
                "Материалы зарезервированы, строительная площадка создана.", true);
        }, "Asset-construction resources reserved.");

    public ResponseEnvelope ProjectAssetConstructionStart(CommandContext context)
        => MutateAssetConstruction0197(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.ResourcesReserved)
                throw new InvalidOperationException("Строительство можно начать только после резервирования ресурсов.");
            var site = RequireConstructionSite0197(project.Id);
            var stages = LoadAssetConstructionStages0197(project.Id);
            var first = stages.FirstOrDefault() ?? throw new InvalidOperationException("Стадии строительства отсутствуют.");
        first.Status = ProjectStageStatusIds.Active;
            first.StartedAtUtc = DateTime.UtcNow;
            first.UpdatedAtUtc = DateTime.UtcNow;
            first.UpdatedByUserId = actor.Id;
            _repositories.ProjectStages.Replace(first);
            project.Status = ProjectStatusIds.InProgress;
            project.StartedAtUtc = DateTime.UtcNow;
            project.CurrentStageId = first.Id;
            project.CurrentStageName = first.Name;
            site.Status = ConstructionSiteStatusIds0197.InConstruction;
            site.StartedAtUtc = DateTime.UtcNow;
            site.CurrentStageKey = StageKey0197(first);
            site.CurrentStageName = first.Name;
            site.UpdatedAtUtc = DateTime.UtcNow;
            site.UpdatedByUserId = actor.Id;
            site.Revision++;
            _repositories.ConstructionSites0197.Replace(site);
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.started",
                "Строительство начато: " + first.Name, true);
        }, "Asset construction started.");

    public ResponseEnvelope ProjectAssetConstructionStageComplete(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!AssetConstructionViewEnabled0197(true)) return AssetConstructionDisabled0197(context.Request.Command);
        RequireAdmin(context);
        var project = RequireAssetConstructionProject0197(context.Request.Payload);
        var operationId = RequireOperationId0191(context);
        var stageKey = RequireLength(PayloadReader.GetString(context.Request.Payload, "stageKey"), 1, 128, "stageKey");
        lock (AssetConstructionRuntimeLock0197)
        {
            var consumed = FindStageConsumption0197(project.Id, stageKey);
            if (consumed != null)
                return Ok("Construction stage already applied.", AssetConstructionResponse0197(project, true, true));
            RequireExpectedRevision0197(context.Request.Payload, project);
            if (project.Status != ProjectStatusIds.InProgress)
                throw new InvalidOperationException("Проект не находится в активном строительстве.");
            var stages = LoadAssetConstructionStages0197(project.Id);
            var stage = stages.FirstOrDefault(x => string.Equals(StageKey0197(x), stageKey, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException("Стадия строительства не найдена.");
        if (stage.Status != ProjectStageStatusIds.Active || project.CurrentStageId != stage.Id)
                throw new InvalidOperationException("Завершить можно только текущую стадию.");
            ConsumeConstructionStage0197(project, stage, actor.Id, operationId);
            stage.Status = ProjectStageStatusIds.Completed;
            stage.ProgressPercent = 100;
            stage.CompletedAtUtc = DateTime.UtcNow;
            stage.UpdatedAtUtc = DateTime.UtcNow;
            stage.UpdatedByUserId = actor.Id;
            _repositories.ProjectStages.Replace(stage);
            var next = stages.Where(x => x.SortOrder > stage.SortOrder).OrderBy(x => x.SortOrder).FirstOrDefault();
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
                project.CurrentStageName = "Все стадии завершены";
                project.Status = ProjectStatusIds.AwaitingAcceptance;
            }
            project.WorkPointsDone = stages.Count(x => x.Status == ProjectStageStatusIds.Completed);
            project.ProgressPercent = Math.Min(100, project.WorkPointsDone * 100 / Math.Max(1, project.WorkPointsRequired));
            TouchAssetConstructionProject0197(project, actor.Id, operationId, context.Request.Command);
            var site = RequireConstructionSite0197(project.Id);
            if (!site.CompletedStageKeys.Contains(stageKey, StringComparer.OrdinalIgnoreCase)) site.CompletedStageKeys.Add(stageKey);
            site.ProgressPercent = project.ProgressPercent;
            site.CurrentStageKey = next == null ? string.Empty : StageKey0197(next);
            site.CurrentStageName = next?.Name ?? "Все стадии завершены";
            site.ConsumedResources = _repositories.ConstructionStageConsumptions0197.Find(
                    Builders<ConstructionStageConsumptionState0197>.Filter.Eq(x => x.ProjectId, project.Id))
                .SelectMany(x => x.Resources).Select(CloneConstructionMaterial0197).ToList();
            site.UpdatedAtUtc = DateTime.UtcNow;
            site.UpdatedByUserId = actor.Id;
            site.Revision++;
            _repositories.ConstructionSites0197.Replace(site);
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.stage.completed",
                "Завершена стадия: " + stage.Name, true);
            TryPublishProjectSync(project, "asset.construction.stage.completed", actor.Id,
                context.Request.RequestId ?? string.Empty);
            return Ok("Construction stage completed.", AssetConstructionResponse0197(project, true));
        }
    }

    public ResponseEnvelope ProjectAssetConstructionComplete(CommandContext context)
        => MutateAssetConstruction0197(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.Completed)
            {
                EnsureAssetInstance0197(project, actor.Id);
                return;
            }
            if (project.Status != ProjectStatusIds.AwaitingAcceptance)
                throw new InvalidOperationException("Сначала завершите все стадии строительства.");
            if (LoadAssetConstructionStages0197(project.Id).Any(x => x.Status != ProjectStageStatusIds.Completed))
                throw new InvalidOperationException("Не все стадии строительства завершены.");
            var asset = EnsureAssetInstance0197(project, actor.Id);
            var site = RequireConstructionSite0197(project.Id);
            site.Status = ConstructionSiteStatusIds0197.Completed;
            site.ProgressPercent = 100;
            site.AssetInstanceId = asset.Id;
            site.CompletedAtUtc = DateTime.UtcNow;
            site.UpdatedAtUtc = DateTime.UtcNow;
            site.UpdatedByUserId = actor.Id;
            site.Revision++;
            _repositories.ConstructionSites0197.Replace(site);
            project.Status = ProjectStatusIds.Completed;
            project.ResultStatus = ProjectResultStatusIds.Applied;
            project.ProgressPercent = 100;
            project.CompletedAtUtc = DateTime.UtcNow;
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.completed",
                "Строительство завершено, крупный актив введён в эксплуатацию.", true);
            TryWriteProjectJournal(project, operationId, "Завершено строительство: " + project.Name, actor.Id);
        }, "Asset construction completed.");

    public ResponseEnvelope ProjectAssetConstructionCancel(CommandContext context)
        => MutateAssetConstruction0197(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.Status == ProjectStatusIds.Completed || project.Status == ProjectStatusIds.Cancelled)
                throw new InvalidOperationException("Завершённый или уже отменённый проект нельзя отменить.");
            ReleaseUnusedConstructionReservations0197(project.Id);
            var site = FindConstructionSite0197(project.Id);
            if (site != null)
            {
                site.Status = ConstructionSiteStatusIds0197.Cancelled;
                site.UpdatedAtUtc = DateTime.UtcNow;
                site.UpdatedByUserId = actor.Id;
                site.Revision++;
                _repositories.ConstructionSites0197.Replace(site);
            }
            project.Status = ProjectStatusIds.Cancelled;
        project.ResultStatus = ProjectResultStatusIds.Rejected;
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.cancelled",
                "Строительство отменено; неиспользованные резервы освобождены.", true);
        }, "Asset construction cancelled.");

    public ResponseEnvelope ProjectAssetConstructionFail(CommandContext context)
        => MutateAssetConstruction0197(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.Completed)
                throw new InvalidOperationException("Завершённое строительство нельзя пометить неудачным.");
            ReleaseUnusedConstructionReservations0197(project.Id);
            var site = FindConstructionSite0197(project.Id);
            if (site != null)
            {
                site.Status = ConstructionSiteStatusIds0197.Failed;
                site.UpdatedAtUtc = DateTime.UtcNow;
                site.UpdatedByUserId = actor.Id;
                site.Revision++;
                _repositories.ConstructionSites0197.Replace(site);
            }
            project.Status = ProjectStatusIds.Failed;
            project.ResultStatus = ProjectResultStatusIds.Failed;
            AddAssetConstructionAudit0197(project, actor.Id, operationId, "asset.construction.failed",
                "Строительство остановлено как неудачное.", true);
        }, "Asset construction failed.");

    public ResponseEnvelope ProjectAssetConstructionAudit(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!AssetConstructionViewEnabled0197(true)) return AssetConstructionDisabled0197(context.Request.Command);
        var project = RequireAssetConstructionProject0197(context.Request.Payload);
        var auditEntries = _repositories.ProjectAuditEntries.Find(Builders<ProjectAuditEntryState>.Filter.Eq(x => x.ProjectId, project.Id))
            .OrderBy(x => x.CreatedAtUtc)
            .ToArray();
        var items = auditEntries
            .OrderBy(x => x.CreatedAtUtc).Select(x => (object)new Dictionary<string, object>
            {
                ["action"] = x.ActionType,
                ["summary"] = x.Summary,
                ["actorDisplayName"] = AccountDisplayName0191(x.ActorUserId),
                ["createdAtUtc"] = x.CreatedAtUtc
            }).ToArray();
        var siteCount = _repositories.ConstructionSites0197.Find(
                Builders<ConstructionSiteState0197>.Filter.Eq(x => x.ProjectId, project.Id)).Count;
        var reservations = _repositories.ConstructionReservations0197.Find(
                Builders<ConstructionResourceReservationState0197>.Filter.Eq(x => x.ProjectId, project.Id)).ToArray();
        var reservationCount = reservations.Length;
        var consumptionCount = _repositories.ConstructionStageConsumptions0197.Find(
                Builders<ConstructionStageConsumptionState0197>.Filter.Eq(x => x.ProjectId, project.Id)).Count;
        var assetCount = _repositories.AssetStates.ListByCampaignAsync(project.CampaignId, 500, true)
            .GetAwaiter().GetResult().Count(x =>
                string.Equals(x.ConstructionProjectId, project.Id, StringComparison.Ordinal));
        var maintenanceCount = _repositories.LargeAssetMaintenanceProfiles0197.Find(
                Builders<LargeAssetMaintenanceProfileState0197>.Filter.Eq(x => x.ProjectId, project.Id)).Count;
        return Ok("Asset-construction audit loaded.", new Dictionary<string, object>
        {
            ["projectName"] = project.Name,
            ["items"] = items,
            ["requestedBy"] = actor.Login,
            ["siteCount"] = siteCount,
            ["reservationCount"] = reservationCount,
            ["activeReservationCount"] = reservations.Count(x =>
                x.Status == ConstructionReservationStatusIds0197.Reserved
                || x.Status == ConstructionReservationStatusIds0197.PartiallyConsumed),
            ["releasedReservationCount"] = reservations.Count(x =>
                x.Status == ConstructionReservationStatusIds0197.Released),
            ["consumedReservationCount"] = reservations.Count(x =>
                x.Status == ConstructionReservationStatusIds0197.Consumed),
            ["consumptionCount"] = consumptionCount,
            ["assetCount"] = assetCount,
            ["maintenanceCount"] = maintenanceCount,
            ["completionAuditCount"] = auditEntries.Count(x =>
                string.Equals(x.ActionType, "asset.construction.completed", StringComparison.OrdinalIgnoreCase))
        });
    }

    private ResponseEnvelope MutateAssetConstruction0197(
        CommandContext context,
        bool adminOnly,
        Action<ProjectBaseState, UserAccount, bool, string> action,
        string message)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetConstructionViewEnabled0197(admin)) return AssetConstructionDisabled0197(context.Request.Command);
        if (adminOnly && !admin) throw new UnauthorizedAccessException("Admin or SuperAdmin role is required.");
        var operationId = RequireOperationId0191(context);
        lock (AssetConstructionRuntimeLock0197)
        {
            var project = RequireAssetConstructionProject0197(context.Request.Payload);
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (string.Equals(project.LastOperationId, operationId, StringComparison.Ordinal))
                return Ok(message, AssetConstructionResponse0197(project, admin, true));
            RequireExpectedRevision0197(context.Request.Payload, project);
            action(project, actor, admin, operationId);
            TouchAssetConstructionProject0197(project, actor.Id, operationId, context.Request.Command);
            TryPublishProjectSync(project, "asset.construction.changed", actor.Id, context.Request.RequestId ?? string.Empty);
            return Ok(message, AssetConstructionResponse0197(project, admin));
        }
    }

    private AssetConstructionSnapshot0197 BuildAssetConstructionSnapshot0197(
        IDictionary<string, object> payload,
        UserAccount actor,
        bool admin)
    {
        var characterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 1, 128, "characterId");
        var ownership = RequireConstructionOwnership0197(characterId, actor, admin);
        var blueprintId = RequireLength(PayloadReader.GetString(payload, "blueprintId"), 1, 128, "blueprintId");
        var blueprint = _repositories.AssetConfigurationBlueprints.GetById(blueprintId)
            ?? throw new KeyNotFoundException("Чертёж здания не найден.");
        if (blueprint.Archived || blueprint.Status != AssetBlueprintStatusIds.Ready)
            throw new InvalidOperationException("Чертёж должен быть готов и не находиться в архиве.");
        if (!admin && blueprint.Visibility != AssetBlueprintVisibilityIds.Shared)
            throw new UnauthorizedAccessException("Чертёж не опубликован для игроков.");
        if (blueprint.ConfiguratorKind != AssetConfiguratorKindIds.Building || blueprint.Configuration?.Building == null)
            throw new InvalidOperationException("Для строительства требуется Building Blueprint.");
        if (blueprint.ServerCalculation == null || !blueprint.ServerCalculation.IsValid)
            throw new InvalidOperationException("Конфигурация здания не прошла server-side validation.");
        var campaignId = RequireLength(ownership.CampaignId, 1, 128, "campaignId");
        var locationId = RequireLength(PayloadReader.GetString(payload, "locationId"), 1, 160, "locationId");
        var location = _repositories.MapSpaceNodes.GetByIdAsync(locationId).GetAwaiter().GetResult()
            ?? throw new KeyNotFoundException("Место строительства не найдено.");
        if (location.IsArchived || location.Deleted || !string.Equals(location.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Место строительства недоступно в этой кампании.");
        if (!admin && !IsPlayerSafeConstructionLocation0197(location))
            throw new UnauthorizedAccessException("Место строительства не открыто игроку.");

        var building = blueprint.Configuration.Building;
        var floors = Math.Max(1, building.FloorCount);
        var stage1 = ConstructionMaterial0197("reference.resource.ember_salt", "Подготовительные материалы", Math.Max(1, floors), "кг");
        var stage2 = ConstructionMaterial0197("reference.resource.resonant_alloy", "Резонансный сплав", Math.Max(4, floors * 2), "кг");
        var stage3 = ConstructionMaterial0197("reference.resource.lumen_crystal", "Люмен-кристалл", Math.Max(1, building.Components.Sum(x => Math.Max(1, x.Quantity))), "шт.");
        var metrics = blueprint.ServerCalculation.Metrics ?? new List<AssetBlueprintMetricState>();
        var totalArea = MetricValue0197(metrics, "totalArea");
        var integrity = (int)Math.Max(1, MetricValue0197(metrics, "structuralIntegrity"));
        var storage = metrics.FirstOrDefault(x => x.Key.IndexOf("storage", StringComparison.OrdinalIgnoreCase) >= 0);
        var snapshot = new AssetConstructionSnapshot0197
        {
            BlueprintId = blueprint.Id,
            BlueprintStableKey = "asset.configuration." + blueprint.Id,
            BlueprintRevision = blueprint.Revision,
            BlueprintName = blueprint.Name,
            ConfigurationSummary = FirstNonEmpty(blueprint.ReadableSummary, blueprint.ServerCalculation.Summary),
            CalculationVersion = FirstNonEmpty(blueprint.CatalogVersion, "asset-configurator-current"),
            BuildingType = building.BuildingTypeKey,
            FloorCount = floors,
            TotalArea = totalArea,
            ConstructionMethod = building.ConstructionMethodKey,
            Quality = building.QualityKey,
            StructuralIntegrity = integrity,
            EnergyProfileSummary = $"Производство {blueprint.ServerCalculation.EnergyProduced}; потребление {blueprint.ServerCalculation.EnergyConsumed}",
            StorageCapacitySummary = storage == null ? "Согласно конфигурации здания" : $"{storage.Label}: {storage.Value} {storage.Unit}",
            ModuleReferences = building.Components.Select(x => x.ComponentKey).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList(),
            TargetOwnerId = ownership.CharacterId,
            TargetOwnerDisplayName = ownership.CharacterDisplayName,
            LocationId = location.Id,
            LocationName = location.Name,
            FacilitySummary = "Строительная площадка и специализированный инструмент",
            PersonnelSummary = "Строительная бригада и специалист по системам",
            LicenseSummary = "Право строительства подтверждается GM для выбранного места",
            RuleSetId = FirstNonEmpty(PayloadReader.GetString(payload, "ruleSetId"), location.RuleSetId, RuleSetIds.FantasyNriDefault),
            Materials = new List<ProjectMaterialSnapshot0191> { stage1, stage2, stage3 },
            Requirements = BuildAssetConstructionRequirements0197(building, location),
            Stages = new List<AssetConstructionStageSnapshot0197>
            {
                new AssetConstructionStageSnapshot0197 { Key = "site_preparation", DisplayName = "Подготовка площадки", Order = 1, PublicSummary = "Подготовить место и основание.", Resources = new List<ProjectMaterialSnapshot0191> { CloneConstructionMaterial0197(stage1) } },
                new AssetConstructionStageSnapshot0197 { Key = "structure_erection", DisplayName = "Возведение конструкции", Order = 2, PublicSummary = "Возвести несущую конструкцию.", Resources = new List<ProjectMaterialSnapshot0191> { CloneConstructionMaterial0197(stage2) } },
                new AssetConstructionStageSnapshot0197 { Key = "systems_commissioning", DisplayName = "Монтаж систем и ввод в эксплуатацию", Order = 3, PublicSummary = "Смонтировать системы и подготовить здание к эксплуатации.", Resources = new List<ProjectMaterialSnapshot0191> { CloneConstructionMaterial0197(stage3) } }
            }
        };
        return snapshot;
    }

    private CharacterOwnershipState RequireConstructionOwnership0197(string characterId, UserAccount actor, bool admin)
    {
        var ownership = _repositories.CharacterOwnerships.Find(
                Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId))
            .FirstOrDefault() ?? throw new KeyNotFoundException("Character v2 ownership profile не найден.");
        if (ownership.IsArchived || !ownership.IsActive || ownership.CharacterStatus == CharacterStatusIds.Archived)
            throw new InvalidOperationException("Для строительства нужен активный неархивный персонаж.");
        if (!admin && !string.Equals(ownership.OwnerUserId, actor.Id, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Нельзя строить актив для чужого персонажа.");
        return ownership;
    }

    private static bool IsPlayerSafeConstructionLocation0197(MapSpaceNodeState location)
        => !location.IsArchived && !location.Deleted
           && !string.Equals(location.Visibility, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(location.Visibility, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase);

    private List<ProjectRequirementSnapshot0191> BuildAssetConstructionRequirements0197(
        BuildingBlueprintConfigurationState building,
        MapSpaceNodeState location)
        => new()
        {
            ConstructionRequirement0197("location", location.Id, "Место: " + location.Name, "GM подтверждает пригодность места."),
            ConstructionRequirement0197("method", building.ConstructionMethodKey, "Метод строительства", "GM подтверждает доступность метода."),
            ConstructionRequirement0197("facility", "construction_site", "Площадка и инструмент", "GM подтверждает оборудование."),
            ConstructionRequirement0197("personnel", "construction_team", "Строительная бригада", "GM подтверждает специалистов."),
            ConstructionRequirement0197("license", "construction_permit", "Право строительства", "GM подтверждает лицензию и законность.")
        };

    private static ProjectRequirementSnapshot0191 ConstructionRequirement0197(string kind, string id, string name, string explanation)
        => new()
        {
            Kind = kind,
            DefinitionId = id ?? string.Empty,
            DisplayName = name,
            Required = true,
            IsPlayerVisible = true,
            PublicExplanation = explanation,
            GMExplanation = "Проверить вручную перед одобрением."
        };

    private ProjectMaterialSnapshot0191 ConstructionMaterial0197(string stableKey, string fallback, decimal quantity, string unit)
        => new()
        {
            DefinitionId = stableKey,
            StableKey = stableKey,
            DisplayName = DefinitionDisplayName0191(stableKey, fallback),
            Quantity = quantity,
            Unit = unit,
            MinimumQuality = "standard",
            UsageMode = "consumed"
        };

    private void CreateAssetConstructionChildren0197(ProjectBaseState project, AssetConstructionSnapshot0197 snapshot, string actorId)
    {
        foreach (var requirement in snapshot.Requirements.Select((value, index) => new { value, index }))
        {
            _repositories.ProjectRequirements.Insert(new ProjectRequirementState
            {
                Id = $"asset_construction_requirement_{project.Id}_{requirement.index + 1}",
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                RequirementType = requirement.value.Kind,
                Name = requirement.value.DisplayName,
                PublicSummary = requirement.value.PublicExplanation,
                GMSummary = requirement.value.GMExplanation,
                Status = ProjectRequirementStatusIds.Open,
                IsRequired = requirement.value.Required,
                IsPlayerVisible = requirement.value.IsPlayerVisible,
                ExtraData = new Dictionary<string, object> { ["definitionId"] = requirement.value.DefinitionId }
            });
        }
        foreach (var stage in snapshot.Stages)
        {
            _repositories.ProjectStages.Insert(new ProjectStageState
            {
                Id = $"asset_construction_stage_{project.Id}_{stage.Key}",
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                StageType = ProjectStageTypeIds.Construction,
                Name = stage.DisplayName,
                PublicSummary = stage.PublicSummary,
                Status = stage.Order == 1 ? ProjectStageStatusIds.Available : ProjectStageStatusIds.Locked,
                SortOrder = stage.Order,
                IsPlayerVisible = true,
                UpdatedByUserId = actorId,
                ExtraData = new Dictionary<string, object> { ["stageKey"] = stage.Key }
            });
            foreach (var resource in stage.Resources.Select((value, index) => new { value, index }))
            {
                _repositories.ProjectResourceRequirements.Insert(new ProjectResourceRequirementState
                {
                    Id = $"asset_construction_resource_{project.Id}_{stage.Key}_{resource.index + 1}",
                    ProjectId = project.Id,
                    CampaignId = project.CampaignId,
                    ResourceType = "material",
                    ResourceId = resource.value.DefinitionId,
                    DisplayName = resource.value.DisplayName,
                    QuantityRequired = resource.value.Quantity,
                    Unit = resource.value.Unit,
                    Status = ProjectResourceRequirementStatusIds.Needed,
                    IsReservationOnly = true,
                    IsPlayerVisible = true,
                    UpdatedByUserId = actorId,
                    ExtraData = new Dictionary<string, object>
                    {
                        ["stageKey"] = stage.Key,
                        ["minimumQuality"] = resource.value.MinimumQuality
                    }
                });
            }
        }
    }

    private void ReserveAssetConstructionResources0197(ProjectBaseState project, string actorId, string operationId)
    {
        var existing = ActiveConstructionReservations0197(project.Id).ToList();
        if (existing.Count > 0)
        {
            if (existing.All(x => x.ReservationOperationId == operationId)) return;
            throw new InvalidOperationException("Материалы проекта уже зарезервированы.");
        }
        var requirements = _repositories.ProjectResourceRequirements.Find(
                Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id)).ToList();
        var prepared = new Dictionary<string, CharacterInventoryItemProfileValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in requirements.GroupBy(x => x.ResourceId, StringComparer.OrdinalIgnoreCase))
        {
            var total = group.Sum(x => x.QuantityRequired);
            var item = FindAvailableInventoryItem0191(project.OwnerCharacterId, group.Key, total)
                ?? throw new InvalidOperationException("Недостаточно ресурса: " + group.First().DisplayName);
            var alreadyReserved = _repositories.ConstructionReservations0197.Find(
                    Builders<ConstructionResourceReservationState0197>.Filter.Eq(x => x.CharacterId, project.OwnerCharacterId)
                    & Builders<ConstructionResourceReservationState0197>.Filter.Eq(x => x.InventoryItemId, item.ItemId))
                .Where(x => x.Status == ConstructionReservationStatusIds0197.Reserved
                            || x.Status == ConstructionReservationStatusIds0197.PartiallyConsumed)
                .Sum(x => Math.Max(0, x.QuantityReserved - x.QuantityConsumed));
            if (item.Quantity - alreadyReserved < total)
                throw new InvalidOperationException("Ресурс уже зарезервирован другим строительным проектом: " + group.First().DisplayName);
            prepared[group.Key] = item;
        }
        var site = EnsureConstructionSite0197(project, actorId);
        foreach (var requirement in requirements)
        {
            var stageKey = Convert.ToString(requirement.ExtraData["stageKey"], CultureInfo.InvariantCulture) ?? string.Empty;
            var reservation = new ConstructionResourceReservationState0197
            {
                Id = $"asset_construction_reservation_{project.Id}_{stageKey}_{requirement.Id}",
                ProjectId = project.Id,
                ConstructionSiteId = site.Id,
                CampaignId = project.CampaignId,
                CharacterId = project.OwnerCharacterId,
                StageKey = stageKey,
                ResourceDefinitionId = requirement.ResourceId,
                ResourceDisplayName = requirement.DisplayName,
                InventoryItemId = prepared[requirement.ResourceId].ItemId,
                QuantityReserved = requirement.QuantityRequired,
                Unit = requirement.Unit,
                ReservationOperationId = operationId
            };
            _repositories.ConstructionReservations0197.Insert(reservation);
            requirement.QuantityReserved = requirement.QuantityRequired;
            requirement.Status = ProjectResourceRequirementStatusIds.Reserved;
            requirement.UpdatedByUserId = actorId;
            requirement.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ProjectResourceRequirements.Replace(requirement);
        }
    }

    private void ConsumeConstructionStage0197(ProjectBaseState project, ProjectStageState stage, string actorId, string operationId)
    {
        var stageKey = StageKey0197(stage);
        if (FindStageConsumption0197(project.Id, stageKey) != null) return;
        var reservations = ActiveConstructionReservations0197(project.Id)
            .Where(x => string.Equals(x.StageKey, stageKey, StringComparison.OrdinalIgnoreCase)).ToList();
        if (reservations.Count == 0) throw new InvalidOperationException("Для стадии не найдены активные резервы.");
        var document = _mongo.CharacterInventoryProfiles.Find(
                Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, project.OwnerCharacterId))
            .FirstOrDefault() ?? throw new KeyNotFoundException("Character v2 inventory profile не найден.");
        document.Profile ??= new InventoryProfile { CharacterId = project.OwnerCharacterId, RuleSetId = project.RuleSetId };
        document.Profile.Items ??= new List<CharacterInventoryItemProfileValue>();
        foreach (var group in reservations.GroupBy(x => x.InventoryItemId, StringComparer.OrdinalIgnoreCase))
        {
            var item = document.Profile.Items.FirstOrDefault(x => string.Equals(x.ItemId, group.Key, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException("Зарезервированный материал не найден в Character v2 inventory profile.");
            var units = group.Sum(x => InventoryUnitsForRequirement0191(x.QuantityReserved));
            if (item.Quantity < units) throw new InvalidOperationException("Количество зарезервированного материала изменилось.");
            item.Quantity -= units;
            item.UpdatedAtUtc = DateTime.UtcNow;
            item.Source = "asset_construction_stage_consumption_0197";
            if (item.Quantity <= 0) document.Profile.Items.Remove(item);
        }
        var previousUpdated = document.UpdatedUtc;
        document.UpdatedUtc = DateTime.UtcNow;
        var write = _mongo.CharacterInventoryProfiles.ReplaceOne(
            Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, document.Id)
            & Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.UpdatedUtc, previousUpdated), document);
        if (write.MatchedCount != 1) throw new InvalidOperationException("Инвентарь изменился во время списания. Обновите проект.");
        foreach (var reservation in reservations)
        {
            reservation.QuantityConsumed = reservation.QuantityReserved;
            reservation.Status = ConstructionReservationStatusIds0197.Consumed;
            reservation.ConsumedAtUtc = DateTime.UtcNow;
            reservation.Revision++;
            _repositories.ConstructionReservations0197.Replace(reservation);
            var requirement = _repositories.ProjectResourceRequirements.Find(
                    Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id)
                    & Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ResourceId, reservation.ResourceDefinitionId))
                .FirstOrDefault(x => string.Equals(Convert.ToString(x.ExtraData["stageKey"], CultureInfo.InvariantCulture), stageKey, StringComparison.OrdinalIgnoreCase));
            if (requirement != null)
            {
                requirement.QuantityProvided = requirement.QuantityRequired;
                requirement.QuantityReserved = 0;
                requirement.Status = ProjectResourceRequirementStatusIds.ConsumedManually;
                requirement.UpdatedAtUtc = DateTime.UtcNow;
                requirement.UpdatedByUserId = actorId;
                _repositories.ProjectResourceRequirements.Replace(requirement);
            }
        }
        _repositories.ConstructionStageConsumptions0197.Insert(new ConstructionStageConsumptionState0197
        {
            Id = $"asset_construction_consumption_{project.Id}_{stageKey}",
            ProjectId = project.Id,
            ConstructionSiteId = RequireConstructionSite0197(project.Id).Id,
            StageKey = stageKey,
            StageName = stage.Name,
            Resources = reservations.Select(x => new ProjectMaterialSnapshot0191
            {
                DefinitionId = x.ResourceDefinitionId,
                StableKey = x.ResourceDefinitionId,
                DisplayName = x.ResourceDisplayName,
                Quantity = x.QuantityConsumed,
                Unit = x.Unit,
                UsageMode = "consumed"
            }).ToList(),
            OperationId = operationId,
            ConsumedByUserId = actorId
        });
    }

    private AssetState EnsureAssetInstance0197(ProjectBaseState project, string actorId)
    {
        var id = "asset_construction_" + project.Id;
        var existing = _repositories.AssetStates.GetByIdAsync(id).GetAwaiter().GetResult();
        if (existing != null)
        {
            EnsureLargeAssetMaintenanceProfile0197(existing, project);
            return existing;
        }
        var snapshot = RequireAssetConstructionSnapshot0197(project);
        var site = RequireConstructionSite0197(project.Id);
        var maintenanceId = "asset_maintenance_" + project.Id;
        var asset = new AssetState
        {
            Id = id,
            DefinitionId = snapshot.BlueprintId,
            Name = snapshot.BlueprintName,
            RuleSetId = project.RuleSetId,
            CampaignId = project.CampaignId,
            AssetType = snapshot.AssetKind,
            LocationId = snapshot.LocationId,
            LocationDisplayName = snapshot.LocationName,
            OwnerCharacterIds = new List<string> { project.OwnerCharacterId },
            OwnerKind = snapshot.TargetOwnerKind,
            OwnerId = project.OwnerCharacterId,
            LegalStatus = "approved",
            ActualStatus = "operational",
            LifecycleStatus = LargeAssetLifecycleStatusIds0197.Operational,
            IsActive = true,
            BlueprintStableKey = snapshot.BlueprintStableKey,
            BlueprintRevision = snapshot.BlueprintRevision,
            ConstructionProjectId = project.Id,
            ConstructionSiteId = site.Id,
            ConfigurationSummary = snapshot.ConfigurationSummary,
            StructuralIntegrity = snapshot.StructuralIntegrity,
            ArmorIntegrity = snapshot.StructuralIntegrity,
            ShieldIntegrity = 0,
            EnergyProfileSummary = snapshot.EnergyProfileSummary,
            StorageCapacitySummary = snapshot.StorageCapacitySummary,
            ModuleReferences = snapshot.ModuleReferences.ToList(),
            LicenseSummary = snapshot.LicenseSummary,
            MaintenanceProfileId = maintenanceId,
            Provenance = "asset_construction_project_0197:" + project.Id,
            EstimatedValueAmount = Math.Max(0, project.DefinitionSnapshot?.AssetConstruction?.Materials.Sum(x => (long)Math.Ceiling(x.Quantity)) ?? 0),
            Tags = new List<string> { "large_asset", "building", "constructed", "project:" + project.Id },
            Notes = snapshot.PublicWarning
        };
        _repositories.AssetStates.UpsertAsync(asset).GetAwaiter().GetResult();
        EnsureLargeAssetMaintenanceProfile0197(asset, project);
        return asset;
    }

    private void EnsureLargeAssetMaintenanceProfile0197(AssetState asset, ProjectBaseState project)
    {
        if (_repositories.LargeAssetMaintenanceProfiles0197.GetById(asset.MaintenanceProfileId) != null) return;
        var snapshot = RequireAssetConstructionSnapshot0197(project);
        _repositories.LargeAssetMaintenanceProfiles0197.Insert(new LargeAssetMaintenanceProfileState0197
        {
            Id = asset.MaintenanceProfileId,
            AssetInstanceId = asset.Id,
            ProjectId = project.Id,
            Status = LargeAssetMaintenanceStatusIds0197.NotScheduled,
            PersonnelRequirementsSummary = snapshot.PersonnelSummary,
            ResourceFuelCategories = new List<string> { "construction_materials", "energy" },
            StorageSecurityRequirements = snapshot.StorageCapacitySummary,
            LicenseDocumentRequirements = snapshot.LicenseSummary,
            MaintenanceIntervalDefinitionReference = "future.large_asset_maintenance_interval"
        });
    }

    private ConstructionSiteState0197 EnsureConstructionSite0197(ProjectBaseState project, string actorId)
    {
        var existing = FindConstructionSite0197(project.Id);
        if (existing != null) return existing;
        var snapshot = RequireAssetConstructionSnapshot0197(project);
        var site = new ConstructionSiteState0197
        {
            Id = "construction_site_" + project.Id,
            ProjectId = project.Id,
            CampaignId = project.CampaignId,
            BlueprintId = snapshot.BlueprintId,
            BlueprintStableKey = snapshot.BlueprintStableKey,
            BlueprintRevision = snapshot.BlueprintRevision,
            BlueprintName = snapshot.BlueprintName,
            OwnerId = project.OwnerCharacterId,
            OwnerUserId = project.OwnerUserId,
            LocationId = snapshot.LocationId,
            LocationName = snapshot.LocationName,
            Status = ConstructionSiteStatusIds0197.Planned,
            UpdatedByUserId = actorId
        };
        _repositories.ConstructionSites0197.Insert(site);
        return site;
    }

    private void ReleaseUnusedConstructionReservations0197(string projectId)
    {
        foreach (var reservation in ActiveConstructionReservations0197(projectId).Where(x => x.QuantityConsumed <= 0))
        {
            reservation.Status = ConstructionReservationStatusIds0197.Released;
            reservation.ReleasedAtUtc = DateTime.UtcNow;
            reservation.Revision++;
            _repositories.ConstructionReservations0197.Replace(reservation);
            var requirement = _repositories.ProjectResourceRequirements.Find(
                    Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, projectId)
                    & Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ResourceId, reservation.ResourceDefinitionId))
                .FirstOrDefault(x => string.Equals(Convert.ToString(x.ExtraData["stageKey"], CultureInfo.InvariantCulture), reservation.StageKey, StringComparison.OrdinalIgnoreCase));
            if (requirement == null) continue;
            requirement.QuantityReserved = 0;
            requirement.Status = ProjectResourceRequirementStatusIds.Needed;
            requirement.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ProjectResourceRequirements.Replace(requirement);
        }
    }

    private Dictionary<string, object> AssetConstructionResponse0197(ProjectBaseState project, bool admin, bool alreadyApplied = false)
        => new()
        {
            ["item"] = AssetConstructionProjectPayload0197(project, admin, true),
            ["alreadyApplied"] = alreadyApplied
        };

    private Dictionary<string, object> AssetConstructionProjectPayload0197(ProjectBaseState project, bool admin, bool details)
    {
        var snapshot = RequireAssetConstructionSnapshot0197(project);
        var site = FindConstructionSite0197(project.Id);
        var payload = new Dictionary<string, object>
        {
            ["projectId"] = project.Id,
            ["name"] = project.Name,
            ["publicSummary"] = project.PublicSummary,
            ["projectTypeLabel"] = "Строительство актива",
            ["status"] = project.Status,
            ["statusLabel"] = CraftProjectStatusLabel0191(project.Status),
            ["approvalStatus"] = project.ApprovalStatus,
            ["revision"] = project.Revision,
            ["progressPercent"] = project.ProgressPercent,
            ["currentStageName"] = project.CurrentStageName,
            ["ownerDisplayName"] = project.OwnerDisplayName,
            ["ownerCharacterDisplayName"] = snapshot.TargetOwnerDisplayName,
            ["blueprintName"] = snapshot.BlueprintName,
            ["assetKindLabel"] = "Здание",
            ["configurationSummary"] = snapshot.ConfigurationSummary,
            ["buildingType"] = snapshot.BuildingType,
            ["floorCount"] = snapshot.FloorCount,
            ["totalArea"] = snapshot.TotalArea,
            ["constructionMethod"] = snapshot.ConstructionMethod,
            ["locationName"] = snapshot.LocationName,
            ["warning"] = snapshot.PublicWarning,
            ["siteStatus"] = site?.Status ?? ConstructionSiteStatusIds0197.Planned,
            ["siteStatusLabel"] = ConstructionSiteStatusLabel0197(site?.Status),
            ["siteProgressPercent"] = site?.ProgressPercent ?? 0,
            ["completedAtUtc"] = project.CompletedAtUtc ?? (object)string.Empty
        };
        if (!details) return payload;
        payload["requirements"] = _repositories.ProjectRequirements.Find(Builders<ProjectRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
            .Where(x => admin || x.IsPlayerVisible).Select(x =>
            {
                var row = new Dictionary<string, object>
                {
                    ["name"] = x.Name,
                    ["summary"] = x.PublicSummary,
                    ["status"] = x.Status,
                    ["statusLabel"] = x.Status == ProjectRequirementStatusIds.Satisfied ? "Подтверждено" : "Ожидает GM",
                    ["required"] = x.IsRequired
                };
                if (admin) row["requirementId"] = x.Id;
                return (object)row;
            }).ToArray();
        payload["resources"] = _repositories.ProjectResourceRequirements.Find(Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id))
            .Where(x => admin || x.IsPlayerVisible).OrderBy(x => Convert.ToString(x.ExtraData["stageKey"], CultureInfo.InvariantCulture))
            .Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.DisplayName,
                ["stageName"] = StageNameFromKey0197(snapshot, Convert.ToString(x.ExtraData["stageKey"], CultureInfo.InvariantCulture)),
                ["quantityRequired"] = x.QuantityRequired,
                ["quantityReserved"] = x.QuantityReserved,
                ["quantityConsumed"] = x.QuantityProvided,
                ["unit"] = x.Unit,
                ["status"] = x.Status,
                ["statusLabel"] = ResourceStatusLabel0191(x.Status)
            }).ToArray();
        payload["stages"] = LoadAssetConstructionStages0197(project.Id).Select(x =>
        {
            var row = new Dictionary<string, object>
            {
                ["name"] = x.Name,
                ["summary"] = x.PublicSummary,
                ["status"] = x.Status,
                ["statusLabel"] = StageStatusLabel0191(x.Status),
                ["progressPercent"] = x.ProgressPercent,
                ["isCurrent"] = x.Id == project.CurrentStageId
            };
            if (admin) row["stageKey"] = StageKey0197(x);
            return (object)row;
        }).ToArray();
        var asset = _repositories.AssetStates.GetByIdAsync("asset_construction_" + project.Id).GetAwaiter().GetResult();
        payload["asset"] = asset == null ? new Dictionary<string, object>() : AssetConstructionResultPayload0197(asset);
        payload["maintenance"] = asset == null ? new Dictionary<string, object>() : MaintenancePayload0197(asset.MaintenanceProfileId);
        if (admin)
        {
            payload["campaignId"] = project.CampaignId;
            payload["currentStageKey"] = site?.CurrentStageKey ?? string.Empty;
            payload["ownerCharacterId"] = project.OwnerCharacterId;
            payload["blueprintId"] = snapshot.BlueprintId;
            payload["locationId"] = snapshot.LocationId;
            payload["snapshotChecksum"] = snapshot.SnapshotChecksum;
            payload["gmSummary"] = project.GMSummary;
            payload["gmNotes"] = project.GMNotes;
            payload["audit"] = _repositories.ProjectAuditEntries.Find(Builders<ProjectAuditEntryState>.Filter.Eq(x => x.ProjectId, project.Id))
                .OrderBy(x => x.CreatedAtUtc).Select(x => (object)new Dictionary<string, object>
                {
                    ["action"] = x.ActionType,
                    ["summary"] = x.Summary,
                    ["actorDisplayName"] = AccountDisplayName0191(x.ActorUserId),
                    ["createdAtUtc"] = x.CreatedAtUtc
                }).ToArray();
        }
        return payload;
    }

    private Dictionary<string, object> AssetConstructionPreviewPayload0197(AssetConstructionSnapshot0197 snapshot)
        => new()
        {
            ["blueprintName"] = snapshot.BlueprintName,
            ["assetKindLabel"] = "Здание",
            ["configurationSummary"] = snapshot.ConfigurationSummary,
            ["floorCount"] = snapshot.FloorCount,
            ["totalArea"] = snapshot.TotalArea,
            ["constructionMethod"] = snapshot.ConstructionMethod,
            ["locationName"] = snapshot.LocationName,
            ["ownerDisplayName"] = snapshot.TargetOwnerDisplayName,
            ["warning"] = snapshot.PublicWarning,
            ["facilitySummary"] = snapshot.FacilitySummary,
            ["personnelSummary"] = snapshot.PersonnelSummary,
            ["licenseSummary"] = snapshot.LicenseSummary,
            ["stages"] = snapshot.Stages.Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.DisplayName,
                ["summary"] = x.PublicSummary,
                ["resources"] = x.Resources.Select(r => (object)new Dictionary<string, object>
                {
                    ["name"] = r.DisplayName,
                    ["quantity"] = r.Quantity,
                    ["unit"] = r.Unit
                }).ToArray()
            }).ToArray(),
            ["requirements"] = snapshot.Requirements.Where(x => x.IsPlayerVisible).Select(x => (object)new Dictionary<string, object>
            {
                ["name"] = x.DisplayName,
                ["summary"] = x.PublicExplanation
            }).ToArray()
        };

    private static Dictionary<string, object> AssetConstructionBlueprintCandidatePayload0197(AssetConfigurationBlueprintState blueprint)
        => new()
        {
            ["reference"] = blueprint.Id,
            ["name"] = blueprint.Name,
            ["summary"] = FirstNonEmpty(blueprint.ReadableSummary, blueprint.ServerCalculation?.Summary),
            ["typeLabel"] = "Здание",
            ["floorCount"] = blueprint.Configuration?.Building?.FloorCount ?? 0,
            ["statusLabel"] = "Готов к строительству"
        };

    private static Dictionary<string, object> AssetConstructionResultPayload0197(AssetState asset)
        => new()
        {
            ["name"] = asset.Name,
            ["kindLabel"] = "Здание",
            ["status"] = asset.LifecycleStatus,
            ["statusLabel"] = "В эксплуатации",
            ["ownerStatus"] = "Принадлежит персонажу",
            ["locationName"] = asset.LocationDisplayName,
            ["configurationSummary"] = asset.ConfigurationSummary,
            ["structuralIntegrity"] = asset.StructuralIntegrity,
            ["energyProfile"] = asset.EnergyProfileSummary,
            ["storageCapacity"] = asset.StorageCapacitySummary
        };

    private Dictionary<string, object> MaintenancePayload0197(string maintenanceId)
    {
        var profile = _repositories.LargeAssetMaintenanceProfiles0197.GetById(maintenanceId);
        return profile == null ? new Dictionary<string, object>() : new Dictionary<string, object>
        {
            ["status"] = profile.Status,
            ["statusLabel"] = "Обслуживание пока не запланировано",
            ["personnelSummary"] = profile.PersonnelRequirementsSummary,
            ["storageSecurityRequirements"] = profile.StorageSecurityRequirements,
            ["licenseRequirements"] = profile.LicenseDocumentRequirements
        };
    }

    private ProjectBaseState RequireAssetConstructionProject0197(IDictionary<string, object> payload)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "projectId"), PayloadReader.GetString(payload, "id")), 1, 128, "projectId");
        var project = _repositories.Projects.GetById(id) ?? throw new KeyNotFoundException("Проект строительства актива не найден.");
        if (project.RuntimeKind != AssetConstructionRuntimeIds0197.RuntimeKind)
            throw new KeyNotFoundException("Проект строительства актива не найден.");
        return project;
    }

    private static AssetConstructionSnapshot0197 RequireAssetConstructionSnapshot0197(ProjectBaseState project)
        => project.DefinitionSnapshot?.AssetConstruction ?? throw new InvalidOperationException("Asset-construction snapshot отсутствует.");

    private ConstructionSiteState0197? FindConstructionSite0197(string projectId)
        => _repositories.ConstructionSites0197.Find(Builders<ConstructionSiteState0197>.Filter.Eq(x => x.ProjectId, projectId)).FirstOrDefault();

    private ConstructionSiteState0197 RequireConstructionSite0197(string projectId)
        => FindConstructionSite0197(projectId) ?? throw new KeyNotFoundException("Строительная площадка не найдена.");

    private ConstructionStageConsumptionState0197? FindStageConsumption0197(string projectId, string stageKey)
        => _repositories.ConstructionStageConsumptions0197.Find(
            Builders<ConstructionStageConsumptionState0197>.Filter.Eq(x => x.ProjectId, projectId)
            & Builders<ConstructionStageConsumptionState0197>.Filter.Eq(x => x.StageKey, stageKey)).FirstOrDefault();

    private IEnumerable<ConstructionResourceReservationState0197> ActiveConstructionReservations0197(string projectId)
        => _repositories.ConstructionReservations0197.Find(Builders<ConstructionResourceReservationState0197>.Filter.Eq(x => x.ProjectId, projectId))
            .Where(x => x.Status == ConstructionReservationStatusIds0197.Reserved || x.Status == ConstructionReservationStatusIds0197.PartiallyConsumed);

    private List<ProjectStageState> LoadAssetConstructionStages0197(string projectId)
        => _repositories.ProjectStages.Find(Builders<ProjectStageState>.Filter.Eq(x => x.ProjectId, projectId)).OrderBy(x => x.SortOrder).ToList();

    private static string StageKey0197(ProjectStageState stage)
        => stage.ExtraData.TryGetValue("stageKey", out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    private static string StageNameFromKey0197(AssetConstructionSnapshot0197 snapshot, string? key)
        => snapshot.Stages.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? "Стадия строительства";

    private static decimal MetricValue0197(IEnumerable<AssetBlueprintMetricState> metrics, string key)
        => metrics.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.Value ?? 0;

    private static ProjectMaterialSnapshot0191 CloneConstructionMaterial0197(ProjectMaterialSnapshot0191 source)
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

    private static ProjectRequirementSnapshot0191 CloneConstructionRequirement0197(ProjectRequirementSnapshot0191 source)
        => new()
        {
            Kind = source.Kind,
            DefinitionId = source.DefinitionId,
            DisplayName = source.DisplayName,
            Quantity = source.Quantity,
            MinimumQualityOrRank = source.MinimumQualityOrRank,
            Required = source.Required,
            IsPlayerVisible = source.IsPlayerVisible,
            ConsumptionMode = source.ConsumptionMode,
            PublicExplanation = source.PublicExplanation,
            GMExplanation = source.GMExplanation
        };

    private static void RequireExpectedRevision0197(IDictionary<string, object> payload, ProjectBaseState project)
    {
        var expected = PayloadReader.GetInt(payload, "expectedRevision") ?? 0;
        if (expected <= 0) throw new ArgumentException("expectedRevision is required for asset-construction mutation.");
        if (expected != project.Revision) throw new InvalidOperationException("Project revision is stale. Refresh and retry.");
    }

    private void TouchAssetConstructionProject0197(ProjectBaseState project, string actorId, string operationId, string command)
    {
        project.Revision++;
        project.UpdatedAtUtc = DateTime.UtcNow;
        project.UpdatedByUserId = actorId;
        project.LastOperationId = operationId;
        project.LastOperationCommand = command;
        _repositories.Projects.Replace(project);
    }

    private void AddAssetConstructionAudit0197(ProjectBaseState project, string actorId, string operationId, string action, string summary, bool playerVisible)
    {
        AddCraftAudit0191(project, actorId, operationId, action, summary, summary, playerVisible);
        _logger.Audit($"project.assetConstruction action={action} projectId={project.Id} actor={actorId} operationId={operationId}");
    }

    private bool AssetConstructionViewEnabled0197(bool admin)
        => _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectRuntimeV1))
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseAssetConstructionProjectV1))
           && (admin
               ? _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedAssetConstructionAdminView))
               : _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedAssetConstructionPlayerView)));

    private ResponseEnvelope AssetConstructionDisabled0197(string command)
    {
        _logger.Admin($"project.assetConstruction.disabled command={command}");
        return Error("Asset-construction project runtime is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private static string ConstructionSiteStatusLabel0197(string? status)
        => status switch
        {
            ConstructionSiteStatusIds0197.ResourcesReserved => "Материалы зарезервированы",
            ConstructionSiteStatusIds0197.InConstruction => "Строительство идёт",
            ConstructionSiteStatusIds0197.Completed => "Завершено",
            ConstructionSiteStatusIds0197.Cancelled => "Отменено",
            ConstructionSiteStatusIds0197.Failed => "Неудача",
            _ => "Запланировано"
        };
}
