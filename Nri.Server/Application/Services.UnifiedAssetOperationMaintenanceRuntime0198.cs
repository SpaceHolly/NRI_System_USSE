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
    private static readonly object AssetMaintenanceRuntimeLock0198 = new();

    public ResponseEnvelope AssetOperationList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetMaintenanceViewEnabled0198(admin)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId") ?? string.Empty;
        if (!admin) characterId = RequirePlayerCharacter0198(actor, characterId).CharacterId;
        var assets = _repositories.AssetStates.ListByCampaignAsync(
                PayloadReader.GetString(context.Request.Payload, "campaignId") ?? "default", 500, true)
            .GetAwaiter().GetResult()
            .Where(x => !x.IsArchived && x.IsActive && string.Equals(x.AssetType, AssetConstructionRuntimeIds0197.AssetKindBuilding, StringComparison.OrdinalIgnoreCase))
            .Where(x => admin || string.Equals(x.OwnerId, characterId, StringComparison.Ordinal))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => (object)AssetOperationPayload0198(x, admin, false))
            .ToArray();
        return Ok("Large assets loaded.", new Dictionary<string, object> { ["items"] = assets });
    }

    public ResponseEnvelope AssetOperationGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetMaintenanceViewEnabled0198(admin)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var asset = RequireOperationAsset0198(context.Request.Payload, actor, admin);
        return Ok("Asset operation state loaded.", new Dictionary<string, object> { ["item"] = AssetOperationPayload0198(asset, admin, true) });
    }

    public ResponseEnvelope AssetOperationActivationRequest(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!AssetMaintenanceViewEnabled0198(false)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (AssetMaintenanceRuntimeLock0198)
        {
            var asset = RequireOperationAsset0198(context.Request.Payload, actor, false);
            var state = EnsureAssetOperationState0198(asset, actor.Id);
            if (string.Equals(state.ActivationRequestOperationId, operationId, StringComparison.Ordinal))
                return Ok("Activation request already applied.", OperationResponse0198(asset, false, true));
            RequireExpectedRevision0198(context.Request.Payload, state.Revision, "asset operation");
            state.ActivationRequestOperationId = operationId;
            state.ActivationRequestedAtUtc = DateTime.UtcNow;
            TouchOperation0198(state, actor.Id, operationId, context.Request.Command);
            AddOperationAudit0198(asset, actor.Id, operationId, "asset.operation.activation.requested", "Запрошен ввод актива в эксплуатацию.", true);
            return Ok("Activation requested.", OperationResponse0198(asset, false));
        }
    }

    public ResponseEnvelope AssetOperationRequirementConfirm(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!AssetMaintenanceViewEnabled0198(true)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (AssetMaintenanceRuntimeLock0198)
        {
            var asset = RequireOperationAsset0198(context.Request.Payload, actor, true);
            var state = EnsureAssetOperationState0198(asset, actor.Id);
            if (string.Equals(state.LastOperationId, operationId, StringComparison.Ordinal))
                return Ok("Requirement confirmation already applied.", OperationResponse0198(asset, true, true));
            RequireExpectedRevision0198(context.Request.Payload, state.Revision, "asset operation");
            var kind = RequireLength(PayloadReader.GetString(context.Request.Payload, "requirementKind"), 1, 96, "requirementKind");
            var profile = EnsureMaintenanceProfile0198(asset, actor.Id);
            var requirement = profile.Requirements.FirstOrDefault(x => string.Equals(x.RequirementKind, kind, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException("Условие эксплуатации не найдено.");
            if (requirement.ResolutionKind != AssetRequirementResolutionKindIds0198.ManualGm)
                throw new InvalidOperationException("Это условие подтверждается канонической ссылкой или ресурсом, а не вручную.");
            requirement.PublicStatus = ProjectRequirementStatusIds.Satisfied;
            requirement.GMStatus = "confirmed";
            requirement.GMDetails = RequireLength(PayloadReader.GetString(context.Request.Payload, "gmNote"), 0, 1024, "gmNote");
            requirement.Revision++;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            profile.Revision++;
            _repositories.LargeAssetMaintenanceProfiles0197.Replace(profile);
            EvaluateReadiness0198(asset, profile, state);
            TouchOperation0198(state, actor.Id, operationId, context.Request.Command);
            AddOperationAudit0198(asset, actor.Id, operationId, "asset.operation.requirement.confirmed", "Подтверждено условие: " + requirement.Name, true);
            return Ok("Operation requirement confirmed.", OperationResponse0198(asset, true));
        }
    }

    public ResponseEnvelope AssetOperationReferenceOptions(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!AssetMaintenanceViewEnabled0198(true)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var asset = RequireOperationAsset0198(context.Request.Payload, actor, true);
        var specialists = _repositories.CharacterOwnerships
            .Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CampaignId, asset.CampaignId))
            .Where(x => x.IsActive && !x.IsArchived && x.CharacterStatus == CharacterStatusIds.Active && x.CharacterKind == CharacterKindIds.Npc)
            .OrderBy(x => x.CharacterDisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["id"] = x.CharacterId,
                ["displayName"] = x.CharacterDisplayName,
                ["description"] = "Активный NPC"
            }).ToArray();
        var licenses = _mongo.LegalEntityLicenses
            .Find(Builders<EntityLicenseState>.Filter.Eq(x => x.CampaignId, asset.CampaignId)
                & Builders<EntityLicenseState>.Filter.Eq(x => x.HolderEntityId, asset.OwnerId)
                & Builders<EntityLicenseState>.Filter.Eq(x => x.Status, "active"))
            .SortBy(x => x.DisplayName)
            .ToList()
            .Select(x => (object)new Dictionary<string, object>
            {
                ["id"] = x.Id,
                ["displayName"] = x.DisplayName,
                ["description"] = "Действующий документ владельца"
            }).ToArray();
        return Ok("Asset operation reference options loaded.", new Dictionary<string, object>
        {
            ["specialists"] = specialists,
            ["licenses"] = licenses
        });
    }

    public ResponseEnvelope AssetOperationReferencesUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!AssetMaintenanceViewEnabled0198(true)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (AssetMaintenanceRuntimeLock0198)
        {
            var asset = RequireOperationAsset0198(context.Request.Payload, actor, true);
            var state = EnsureAssetOperationState0198(asset, actor.Id);
            if (string.Equals(state.LastOperationId, operationId, StringComparison.Ordinal))
                return Ok("Asset operation references already applied.", OperationResponse0198(asset, true, true));
            RequireExpectedRevision0198(context.Request.Payload, state.Revision, "asset operation");
            var specialistId = RequireLength(PayloadReader.GetString(context.Request.Payload, "specialistCharacterId"), 1, 128, "specialistCharacterId");
            var licenseId = RequireLength(PayloadReader.GetString(context.Request.Payload, "licenseId"), 1, 128, "licenseId");
            var specialist = _repositories.CharacterOwnerships
                .Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, specialistId))
                .FirstOrDefault(x => x.CampaignId == asset.CampaignId && x.IsActive && !x.IsArchived && x.CharacterStatus == CharacterStatusIds.Active && x.CharacterKind == CharacterKindIds.Npc)
                ?? throw new InvalidOperationException("Выбранный специалист должен быть активным NPC этой кампании.");
            var license = _mongo.LegalEntityLicenses.Find(Builders<EntityLicenseState>.Filter.Eq(x => x.Id, licenseId)).FirstOrDefault();
            if (license == null || license.CampaignId != asset.CampaignId || license.HolderEntityId != asset.OwnerId || license.Status != "active")
                throw new InvalidOperationException("Выбранный документ должен быть действующим и принадлежать владельцу актива.");
            var profile = EnsureMaintenanceProfile0198(asset, actor.Id);
            profile.KeySpecialistCharacterId = specialist.CharacterId;
            profile.KeySpecialistDisplayName = specialist.CharacterDisplayName;
            profile.LicenseDocumentReferences = new List<string> { license.Id };
            asset.LicenseSummary = license.DisplayName;
            asset.UpdatedAtUtc = DateTime.UtcNow;
            asset.Revision++;
            _repositories.AssetStates.UpsertAsync(asset).GetAwaiter().GetResult();
            ApplyReferenceRequirement0198(profile, AssetMaintenanceRequirementKindIds0198.Personnel, specialist.CharacterId, specialist.CharacterDisplayName);
            ApplyReferenceRequirement0198(profile, AssetMaintenanceRequirementKindIds0198.LicensesAndDocuments, license.Id, license.DisplayName);
            profile.UpdatedAtUtc = DateTime.UtcNow;
            profile.Revision++;
            _repositories.LargeAssetMaintenanceProfiles0197.Replace(profile);
            EvaluateReadiness0198(asset, profile, state);
            TouchOperation0198(state, actor.Id, operationId, context.Request.Command);
            AddOperationAudit0198(asset, actor.Id, operationId, "asset.operation.references.updated", "Назначены специалист и действующий документ.", true);
            return Ok("Asset operation references updated.", OperationResponse0198(asset, true));
        }
    }

    private static void ApplyReferenceRequirement0198(LargeAssetMaintenanceProfileState0197 profile, string kind, string referenceId, string displayName)
    {
        var requirement = profile.Requirements.First(x => x.RequirementKind == kind);
        requirement.ReferenceId = referenceId;
        requirement.ReferenceDisplayName = displayName;
        requirement.PublicStatus = ProjectRequirementStatusIds.Satisfied;
        requirement.GMStatus = "validated";
        requirement.Revision++;
    }

    public ResponseEnvelope AssetOperationActivate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!AssetMaintenanceViewEnabled0198(true)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (AssetMaintenanceRuntimeLock0198)
        {
            var asset = RequireOperationAsset0198(context.Request.Payload, actor, true);
            var state = EnsureAssetOperationState0198(asset, actor.Id);
            if (string.Equals(state.LastOperationId, operationId, StringComparison.Ordinal))
                return Ok("Activation already applied.", OperationResponse0198(asset, true, true));
            RequireExpectedRevision0198(context.Request.Payload, state.Revision, "asset operation");
            var profile = EnsureMaintenanceProfile0198(asset, actor.Id);
            EvaluateReadiness0198(asset, profile, state);
            if (state.ReadinessStatus == AssetReadinessStatusIds0198.Blocked)
                throw new InvalidOperationException("Актив не готов к эксплуатации: " + string.Join("; ", state.PublicBlockerSummaries));
            state.OperationStatus = AssetOperationStatusIds0198.Operational;
            state.ActivatedAtUtc ??= DateTime.UtcNow;
            profile.Status = AssetMaintenanceStatusIds0198.Current;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            profile.Revision++;
            _repositories.LargeAssetMaintenanceProfiles0197.Replace(profile);
            TouchOperation0198(state, actor.Id, operationId, context.Request.Command);
            AddOperationAudit0198(asset, actor.Id, operationId, "asset.operation.activated", "Актив введён в эксплуатацию.", true);
            return Ok("Asset activated.", OperationResponse0198(asset, true));
        }
    }

    public ResponseEnvelope AssetMaintenanceMarkDue(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!AssetMaintenanceViewEnabled0198(true)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (AssetMaintenanceRuntimeLock0198)
        {
            var asset = RequireOperationAsset0198(context.Request.Payload, actor, true);
            var state = EnsureAssetOperationState0198(asset, actor.Id);
            if (string.Equals(state.LastOperationId, operationId, StringComparison.Ordinal))
                return Ok("Maintenance due transition already applied.", OperationResponse0198(asset, true, true));
            RequireExpectedRevision0198(context.Request.Payload, state.Revision, "asset operation");
            if (state.OperationStatus != AssetOperationStatusIds0198.Operational)
                throw new InvalidOperationException("Отметить срок обслуживания можно только для эксплуатируемого актива.");
            var profile = EnsureMaintenanceProfile0198(asset, actor.Id);
            if (profile.Status != AssetMaintenanceStatusIds0198.Current && profile.Status != LargeAssetMaintenanceStatusIds0197.Current)
                throw new InvalidOperationException("Профиль обслуживания не находится в актуальном состоянии.");
            profile.Status = AssetMaintenanceStatusIds0198.Due;
            profile.LastOperationId = operationId;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            profile.Revision++;
            _repositories.LargeAssetMaintenanceProfiles0197.Replace(profile);
            state.OperationStatus = AssetOperationStatusIds0198.Restricted;
            state.RestrictedAtUtc = DateTime.UtcNow;
            TouchOperation0198(state, actor.Id, operationId, context.Request.Command);
            AddOperationAudit0198(asset, actor.Id, operationId, "asset.maintenance.due", "Наступил срок обслуживания; эксплуатация ограничена.", true);
            return Ok("Maintenance marked due.", OperationResponse0198(asset, true));
        }
    }

    public ResponseEnvelope ProjectAssetMaintenanceCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetMaintenanceViewEnabled0198(admin)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var operationId = RequireOperationId0191(context);
        lock (AssetMaintenanceRuntimeLock0198)
        {
            var replay = _repositories.Projects.Find(Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedOperationId, operationId)
                & Builders<ProjectBaseState>.Filter.Eq(x => x.CreatedByUserId, actor.Id)).FirstOrDefault();
            if (replay != null) return Ok("Maintenance project already created.", MaintenanceResponse0198(replay, admin, true));
            var asset = RequireOperationAsset0198(context.Request.Payload, actor, admin);
            var profile = EnsureMaintenanceProfile0198(asset, actor.Id);
            var operation = EnsureAssetOperationState0198(asset, actor.Id);
            if (profile.Status != AssetMaintenanceStatusIds0198.Due && profile.Status != AssetMaintenanceStatusIds0198.Overdue)
                throw new InvalidOperationException("Обслуживание можно подготовить только для актива со статусом «Срок наступил».");
            if (ActiveMaintenanceProject0198(asset.Id) != null)
                throw new InvalidOperationException("Для этого актива уже существует активный проект обслуживания.");
            var ownership = RequireConstructionOwnership0197(asset.OwnerId, actor, admin);
            var snapshot = BuildMaintenanceSnapshot0198(asset, profile, operation, ownership);
            var definition = new ProjectDefinitionSnapshot0191
            {
                BlueprintStableKey = snapshot.BlueprintStableKey,
                BlueprintRevision = snapshot.BlueprintRevision,
                BlueprintName = snapshot.AssetName,
                BlueprintKind = snapshot.AssetKind,
                ProjectTemplateStableKey = snapshot.ProjectTemplateKey,
                ProjectTemplateName = snapshot.ProjectTemplateName,
                Inputs = snapshot.Materials.Select(CloneMaintenanceMaterial0198).ToList(),
                Requirements = snapshot.Requirements.Select(CloneMaintenanceRequirement0198).ToList(),
                Stages = snapshot.Stages.Select(CloneMaintenanceStage0198).ToList(),
                AssetMaintenance = snapshot
            };
            definition.SnapshotChecksum = ComputeSnapshotChecksum0191(definition);
            snapshot.SnapshotChecksum = definition.SnapshotChecksum;
            var project = new ProjectBaseState
            {
                CampaignId = asset.CampaignId,
                RuleSetId = asset.RuleSetId,
                ProjectType = ProjectTypeIds.AssetMaintenance,
                RuntimeKind = AssetMaintenanceRuntimeIds0198.RuntimeKind,
                Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), "Обслуживание: " + asset.Name), 2, 180, "name"),
                PublicSummary = "Плановое обслуживание актива «" + asset.Name + "».",
                GMSummary = "Typed maintenance runtime; состояние актива и ресурсы определяет сервер.",
                Status = ProjectStatusIds.Draft,
                ApprovalStatus = ProjectApprovalStatusIds.Draft,
                ProgressMode = ProjectProgressModeIds.StageBased,
                ResultStatus = ProjectResultStatusIds.Expected,
                ResultApplicationMode = ProjectResultApplicationModeIds.GmManual,
                OwnerUserId = ownership.OwnerUserId,
                OwnerDisplayName = ownership.CharacterDisplayName,
                OwnerCharacterId = ownership.CharacterId,
                CreatedByUserId = actor.Id,
                UpdatedByUserId = actor.Id,
                VisibilityMode = ProjectVisibilityModeIds.OwnerOnly,
                IsPlayerVisible = true,
                CreatedOperationId = operationId,
                LastOperationId = operationId,
                LastOperationCommand = context.Request.Command,
                DefinitionSnapshot = definition,
                WorkPointsRequired = snapshot.Stages.Count,
                ExpectedResultSummary = new Dictionary<string, object> { ["assetName"] = asset.Name, ["result"] = "Operational / Current" }
            };
            _repositories.Projects.Insert(project);
            CreateMaintenanceChildren0198(project, snapshot, actor.Id);
            AddOperationAudit0198(asset, actor.Id, operationId, "asset.maintenance.project.created", "Создан черновик обслуживания.", true, project);
            return Ok("Maintenance project created.", MaintenanceResponse0198(project, admin));
        }
    }

    public ResponseEnvelope ProjectAssetMaintenanceSubmit(CommandContext context)
        => MutateMaintenanceProject0198(context, false, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.Draft && project.Status != ProjectStatusIds.RequirementsReview)
                throw new InvalidOperationException("Отправить можно только черновик обслуживания.");
            project.Status = ProjectStatusIds.AwaitingApproval;
            project.ApprovalStatus = ProjectApprovalStatusIds.PendingGmReview;
            project.SubmittedAtUtc = DateTime.UtcNow;
            _repositories.ProjectApprovals.Insert(new ProjectApprovalState
            {
                Id = "asset_maintenance_approval_" + project.Id,
                ProjectId = project.Id,
                CampaignId = project.CampaignId,
                ApprovalType = "gm_asset_maintenance",
                Status = ProjectApprovalStatusIds.PendingGmReview,
                RequestedByUserId = actor.Id,
                PublicSummary = "Обслуживание ожидает решения GM.",
                GMSummary = "Проверьте специалиста, документы, ресурсы и ручные условия.",
                IsPlayerVisible = true
            });
            AddMaintenanceAudit0198(project, actor.Id, operationId, "asset.maintenance.submitted", "Проект обслуживания отправлен GM.", true);
        }, "Maintenance project submitted.");

    public ResponseEnvelope ProjectAssetMaintenanceList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetMaintenanceViewEnabled0198(admin)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var filter = Builders<ProjectBaseState>.Filter.Eq(x => x.RuntimeKind, AssetMaintenanceRuntimeIds0198.RuntimeKind)
            & Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false);
        if (!admin) filter &= Builders<ProjectBaseState>.Filter.Eq(x => x.OwnerUserId, actor.Id);
        var items = _repositories.Projects.Find(filter).OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)MaintenanceProjectPayload0198(x, admin, false)).ToArray();
        return Ok("Maintenance projects loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ProjectAssetMaintenanceGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetMaintenanceViewEnabled0198(admin)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var project = RequireMaintenanceProject0198(context.Request.Payload);
        RequireOwnerOrAdmin0191(project, actor, admin);
        return Ok("Maintenance project loaded.", MaintenanceResponse0198(project, admin));
    }

    public ResponseEnvelope ProjectAssetMaintenanceRequirementConfirm(CommandContext context)
        => MutateMaintenanceProject0198(context, true, (project, actor, _, operationId) =>
        {
            var requirementId = RequireLength(PayloadReader.GetString(context.Request.Payload, "requirementId"), 1, 160, "requirementId");
            var requirement = _repositories.ProjectRequirements.GetById(requirementId) ?? throw new KeyNotFoundException("Условие обслуживания не найдено.");
            if (requirement.ProjectId != project.Id) throw new InvalidOperationException("Условие относится к другому проекту.");
            requirement.Status = ProjectRequirementStatusIds.Satisfied;
            requirement.VerifiedByUserId = actor.Id;
            requirement.VerifiedAtUtc = DateTime.UtcNow;
            requirement.PublicNotes = "Условие подтверждено GM.";
            requirement.GMNotes = RequireLength(PayloadReader.GetString(context.Request.Payload, "gmNote"), 0, 1024, "gmNote");
            _repositories.ProjectRequirements.Replace(requirement);
            AddMaintenanceAudit0198(project, actor.Id, operationId, "asset.maintenance.requirement.confirmed", "Подтверждено условие: " + requirement.Name, true);
        }, "Maintenance requirement confirmed.");

    public ResponseEnvelope ProjectAssetMaintenanceApprove(CommandContext context)
        => MutateMaintenanceProject0198(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval) throw new InvalidOperationException("Проект не ожидает решения GM.");
            var open = RequiredOpenRequirements0191(project.Id).ToArray();
            if (open.Length > 0) throw new InvalidOperationException("Не подтверждены обязательные условия: " + string.Join(", ", open.Select(x => x.Name)));
            project.Status = ProjectStatusIds.Approved;
            project.ApprovalStatus = ProjectApprovalStatusIds.Approved;
            project.ApprovedAtUtc = DateTime.UtcNow;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Approved, "Обслуживание одобрено.");
            AddMaintenanceAudit0198(project, actor.Id, operationId, "asset.maintenance.approved", "GM одобрил обслуживание.", true);
        }, "Maintenance project approved.");

    public ResponseEnvelope ProjectAssetMaintenanceReject(CommandContext context)
        => MutateMaintenanceProject0198(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.AwaitingApproval) throw new InvalidOperationException("Проект не ожидает решения GM.");
            project.Status = ProjectStatusIds.Failed;
            project.ApprovalStatus = ProjectApprovalStatusIds.Rejected;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            ResolveApproval0191(project.Id, actor.Id, ProjectApprovalStatusIds.Rejected, "Обслуживание отклонено.");
            AddMaintenanceAudit0198(project, actor.Id, operationId, "asset.maintenance.rejected", "GM отклонил обслуживание.", true);
        }, "Maintenance project rejected.");

    public ResponseEnvelope ProjectAssetMaintenanceReserve(CommandContext context)
        => MutateMaintenanceProject0198(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.Approved) throw new InvalidOperationException("Ресурсы резервируются после одобрения.");
            ReserveMaintenanceResources0198(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.ResourcesReserved;
            AddMaintenanceAudit0198(project, actor.Id, operationId, "asset.maintenance.resources.reserved", "Ресурсы и актив зарезервированы для обслуживания.", true);
        }, "Maintenance resources reserved.");

    public ResponseEnvelope ProjectAssetMaintenanceStart(CommandContext context)
        => MutateMaintenanceProject0198(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status != ProjectStatusIds.ResourcesReserved) throw new InvalidOperationException("Обслуживание начинается после резервирования.");
            var stages = LoadMaintenanceStages0198(project.Id);
            var first = stages.FirstOrDefault() ?? throw new InvalidOperationException("Стадии обслуживания отсутствуют.");
            first.Status = ProjectStageStatusIds.Active;
            first.StartedAtUtc = DateTime.UtcNow;
            first.UpdatedAtUtc = DateTime.UtcNow;
            first.UpdatedByUserId = actor.Id;
            _repositories.ProjectStages.Replace(first);
            project.Status = ProjectStatusIds.InProgress;
            project.StartedAtUtc = DateTime.UtcNow;
            project.CurrentStageId = first.Id;
            project.CurrentStageName = first.Name;
            var snapshot = RequireMaintenanceSnapshot0198(project);
            var asset = RequireAssetById0198(snapshot.AssetId);
            var profile = EnsureMaintenanceProfile0198(asset, actor.Id);
            var operation = EnsureAssetOperationState0198(asset, actor.Id);
            profile.Status = AssetMaintenanceStatusIds0198.InMaintenance;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            profile.Revision++;
            _repositories.LargeAssetMaintenanceProfiles0197.Replace(profile);
            operation.OperationStatus = AssetOperationStatusIds0198.Restricted;
            TouchOperation0198(operation, actor.Id, operationId, context.Request.Command);
            AddMaintenanceAudit0198(project, actor.Id, operationId, "asset.maintenance.started", "Обслуживание начато: " + first.Name, true);
        }, "Maintenance started.");

    public ResponseEnvelope ProjectAssetMaintenanceStageComplete(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!AssetMaintenanceViewEnabled0198(true)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var project = RequireMaintenanceProject0198(context.Request.Payload);
        var operationId = RequireOperationId0191(context);
        var stageKey = RequireLength(PayloadReader.GetString(context.Request.Payload, "stageKey"), 1, 128, "stageKey");
        lock (AssetMaintenanceRuntimeLock0198)
        {
            if (FindMaintenanceConsumption0198(project.Id, stageKey) != null)
                return Ok("Maintenance stage already applied.", MaintenanceResponse0198(project, true, true));
            RequireExpectedRevision0198(context.Request.Payload, project.Revision, "maintenance project");
            if (project.Status != ProjectStatusIds.InProgress) throw new InvalidOperationException("Проект не находится в активном обслуживании.");
            var stages = LoadMaintenanceStages0198(project.Id);
            var stage = stages.FirstOrDefault(x => MaintenanceStageKey0198(x) == stageKey) ?? throw new KeyNotFoundException("Стадия обслуживания не найдена.");
            if (stage.Status != ProjectStageStatusIds.Active || stage.Id != project.CurrentStageId) throw new InvalidOperationException("Завершить можно только текущую стадию.");
            ConsumeMaintenanceStage0198(project, stage, actor.Id, operationId);
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
            TouchMaintenanceProject0198(project, actor.Id, operationId, context.Request.Command);
            AddMaintenanceAudit0198(project, actor.Id, operationId, "asset.maintenance.stage.completed", "Завершена стадия: " + stage.Name, true);
            return Ok("Maintenance stage completed.", MaintenanceResponse0198(project, true));
        }
    }

    public ResponseEnvelope ProjectAssetMaintenanceComplete(CommandContext context)
        => MutateMaintenanceProject0198(context, true, (project, actor, _, operationId) =>
        {
            var snapshot = RequireMaintenanceSnapshot0198(project);
            if (project.Status == ProjectStatusIds.Completed)
            {
                EnsureServiceRecord0198(project, actor.Id);
                return;
            }
            if (project.Status != ProjectStatusIds.AwaitingAcceptance || LoadMaintenanceStages0198(project.Id).Any(x => x.Status != ProjectStageStatusIds.Completed))
                throw new InvalidOperationException("Сначала завершите все стадии обслуживания.");
            var asset = RequireAssetById0198(snapshot.AssetId);
            var profile = EnsureMaintenanceProfile0198(asset, actor.Id);
            var operation = EnsureAssetOperationState0198(asset, actor.Id);
            profile.Status = AssetMaintenanceStatusIds0198.Current;
            profile.LastMaintenanceCompletedAtUtc = DateTime.UtcNow;
            profile.NextMaintenanceDueAtUtc = DateTime.UtcNow.AddDays(30);
            profile.LastOperationId = operationId;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            profile.Revision++;
            _repositories.LargeAssetMaintenanceProfiles0197.Replace(profile);
            operation.OperationStatus = AssetOperationStatusIds0198.Operational;
            operation.RestrictedAtUtc = null;
            TouchOperation0198(operation, actor.Id, operationId, context.Request.Command);
            CloseMaintenanceReservations0198(project.Id);
            EnsureServiceRecord0198(project, actor.Id);
            project.Status = ProjectStatusIds.Completed;
            project.ResultStatus = ProjectResultStatusIds.Applied;
            project.ProgressPercent = 100;
            project.CompletedAtUtc = DateTime.UtcNow;
            AddMaintenanceAudit0198(project, actor.Id, operationId, "asset.maintenance.completed", "Обслуживание завершено; актив возвращён в эксплуатацию.", true);
        }, "Maintenance completed.");

    public ResponseEnvelope ProjectAssetMaintenanceCancel(CommandContext context)
        => MutateMaintenanceProject0198(context, false, (project, actor, admin, operationId) =>
        {
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (_repositories.AssetMaintenanceStageConsumptions0198.Find(Builders<AssetMaintenanceStageConsumptionState0198>.Filter.Eq(x => x.ProjectId, project.Id)).Any())
                throw new InvalidOperationException("После первого списания обычная отмена недоступна.");
            ReleaseMaintenanceReservations0198(project.Id);
            RestoreDueRestricted0198(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.Cancelled;
            project.ResultStatus = ProjectResultStatusIds.Rejected;
            AddMaintenanceAudit0198(project, actor.Id, operationId, "asset.maintenance.cancelled", "Обслуживание отменено до списания; резервы освобождены.", true);
        }, "Maintenance cancelled.");

    public ResponseEnvelope ProjectAssetMaintenanceFail(CommandContext context)
        => MutateMaintenanceProject0198(context, true, (project, actor, _, operationId) =>
        {
            if (project.Status == ProjectStatusIds.Completed) throw new InvalidOperationException("Завершённое обслуживание нельзя пометить неудачным.");
            ReleaseMaintenanceReservations0198(project.Id);
            RestoreDueRestricted0198(project, actor.Id, operationId);
            project.Status = ProjectStatusIds.Failed;
            project.ResultStatus = ProjectResultStatusIds.Failed;
            AddMaintenanceAudit0198(project, actor.Id, operationId, "asset.maintenance.failed", "Обслуживание остановлено как неудачное.", true);
        }, "Maintenance failed.");

    public ResponseEnvelope ProjectAssetMaintenanceAudit(CommandContext context)
    {
        RequireAdmin(context);
        if (!AssetMaintenanceViewEnabled0198(true)) return AssetMaintenanceDisabled0198(context.Request.Command);
        var project = RequireMaintenanceProject0198(context.Request.Payload);
        var snapshot = RequireMaintenanceSnapshot0198(project);
        var audits = _repositories.ProjectAuditEntries.Find(Builders<ProjectAuditEntryState>.Filter.Eq(x => x.ProjectId, project.Id)).OrderBy(x => x.CreatedAtUtc).ToArray();
        return Ok("Maintenance audit loaded.", new Dictionary<string, object>
        {
            ["projectName"] = project.Name,
            ["assetName"] = snapshot.AssetName,
            ["assetIdUnchanged"] = RequireAssetById0198(snapshot.AssetId).Id == snapshot.AssetId,
            ["reservationCount"] = _repositories.AssetMaintenanceReservations0198.Find(Builders<AssetMaintenanceReservationState0198>.Filter.Eq(x => x.ProjectId, project.Id)).Count,
            ["consumptionCount"] = _repositories.AssetMaintenanceStageConsumptions0198.Find(Builders<AssetMaintenanceStageConsumptionState0198>.Filter.Eq(x => x.ProjectId, project.Id)).Count,
            ["serviceRecordCount"] = _repositories.MaintenanceServiceRecords0198.Find(Builders<MaintenanceServiceRecordState0198>.Filter.Eq(x => x.ProjectId, project.Id)).Count,
            ["items"] = audits.Select(x => (object)new Dictionary<string, object> { ["summary"] = x.Summary, ["actorDisplayName"] = AccountDisplayName0191(x.ActorUserId), ["createdAtUtc"] = x.CreatedAtUtc }).ToArray()
        });
    }

    private ResponseEnvelope MutateMaintenanceProject0198(CommandContext context, bool adminOnly, Action<ProjectBaseState, UserAccount, bool, string> action, string message)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsProjectCraftAdmin0191(actor);
        if (!AssetMaintenanceViewEnabled0198(admin)) return AssetMaintenanceDisabled0198(context.Request.Command);
        if (adminOnly && !admin) throw new UnauthorizedAccessException("Admin or SuperAdmin role is required.");
        var operationId = RequireOperationId0191(context);
        lock (AssetMaintenanceRuntimeLock0198)
        {
            var project = RequireMaintenanceProject0198(context.Request.Payload);
            RequireOwnerOrAdmin0191(project, actor, admin);
            if (project.LastOperationId == operationId) return Ok(message, MaintenanceResponse0198(project, admin, true));
            RequireExpectedRevision0198(context.Request.Payload, project.Revision, "maintenance project");
            action(project, actor, admin, operationId);
            TouchMaintenanceProject0198(project, actor.Id, operationId, context.Request.Command);
            TryPublishProjectSync(project, "asset.maintenance.changed", actor.Id, context.Request.RequestId ?? string.Empty);
            return Ok(message, MaintenanceResponse0198(project, admin));
        }
    }

    private AssetState RequireOperationAsset0198(IDictionary<string, object> payload, UserAccount actor, bool admin)
    {
        var id = RequireLength(PayloadReader.GetString(payload, "assetId"), 1, 160, "assetId");
        var asset = RequireAssetById0198(id);
        if (asset.IsArchived || !asset.IsActive || !string.Equals(asset.AssetType, AssetConstructionRuntimeIds0197.AssetKindBuilding, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Актив недоступен для эксплуатации.");
        var ownership = RequireConstructionOwnership0197(asset.OwnerId, actor, admin);
        if (!admin && ownership.OwnerUserId != actor.Id) throw new UnauthorizedAccessException("Asset belongs to another player.");
        return asset;
    }

    private AssetState RequireAssetById0198(string id)
        => _repositories.AssetStates.GetByIdAsync(id).GetAwaiter().GetResult() ?? throw new KeyNotFoundException("Крупный актив не найден.");

    private CharacterOwnershipState RequirePlayerCharacter0198(UserAccount actor, string requestedCharacterId)
    {
        var owned = _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.OwnerUserId, actor.Id))
            .Where(x => x.IsActive && !x.IsArchived && x.CharacterStatus == CharacterStatusIds.Active).ToArray();
        if (!string.IsNullOrWhiteSpace(requestedCharacterId))
            return owned.FirstOrDefault(x => x.CharacterId == requestedCharacterId) ?? throw new UnauthorizedAccessException("Character belongs to another player.");
        return owned.FirstOrDefault() ?? throw new KeyNotFoundException("У игрока нет активного Character v2 персонажа.");
    }

    private LargeAssetMaintenanceProfileState0197 EnsureMaintenanceProfile0198(AssetState asset, string actorId)
    {
        var profile = !string.IsNullOrWhiteSpace(asset.MaintenanceProfileId) ? _repositories.LargeAssetMaintenanceProfiles0197.GetById(asset.MaintenanceProfileId) : null;
        profile ??= _repositories.LargeAssetMaintenanceProfiles0197.Find(Builders<LargeAssetMaintenanceProfileState0197>.Filter.Eq(x => x.AssetInstanceId, asset.Id)).FirstOrDefault();
        if (profile == null)
        {
            profile = new LargeAssetMaintenanceProfileState0197 { Id = "asset_maintenance_profile_" + asset.Id, AssetInstanceId = asset.Id, ProjectId = asset.ConstructionProjectId, Status = LargeAssetMaintenanceStatusIds0197.NotScheduled };
            _repositories.LargeAssetMaintenanceProfiles0197.Insert(profile);
        }
        if (asset.MaintenanceProfileId != profile.Id)
        {
            asset.MaintenanceProfileId = profile.Id;
            asset.Revision++;
            asset.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.AssetStates.UpsertAsync(asset).GetAwaiter().GetResult();
        }
        if (profile.Requirements.Count == 0)
        {
            var specialist = _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CampaignId, asset.CampaignId))
                .FirstOrDefault(x => x.IsActive && !x.IsArchived && x.CharacterKind == CharacterKindIds.Npc);
            var license = _mongo.LegalEntityLicenses.Find(Builders<EntityLicenseState>.Filter.Eq(x => x.CampaignId, asset.CampaignId)
                & Builders<EntityLicenseState>.Filter.Eq(x => x.HolderEntityId, asset.OwnerId)
                & Builders<EntityLicenseState>.Filter.Eq(x => x.Status, "active")).FirstOrDefault();
            profile.KeySpecialistCharacterId = specialist?.CharacterId ?? string.Empty;
            profile.KeySpecialistDisplayName = specialist?.CharacterDisplayName ?? string.Empty;
            if (license != null) profile.LicenseDocumentReferences.Add(license.Id);
            profile.Requirements = DefaultOperationRequirements0198(profile, license);
            profile.UpdatedAtUtc = DateTime.UtcNow;
            profile.Revision++;
            _repositories.LargeAssetMaintenanceProfiles0197.Replace(profile);
        }
        return profile;
    }

    private static List<AssetMaintenanceRequirementState0198> DefaultOperationRequirements0198(LargeAssetMaintenanceProfileState0197 profile, EntityLicenseState? license)
        => new()
        {
            RefRequirement0198(AssetMaintenanceRequirementKindIds0198.Personnel, "Ключевой специалист", profile.KeySpecialistCharacterId, profile.KeySpecialistDisplayName),
            ManualRequirement0198(AssetMaintenanceRequirementKindIds0198.FuelAndResources, "Операционный запас обеспечен"),
            new AssetMaintenanceRequirementState0198 { RequirementKind = AssetMaintenanceRequirementKindIds0198.RepairAndService, Name = "Расходники обслуживания", ResolutionKind = AssetRequirementResolutionKindIds0198.Resource, PublicStatus = ProjectRequirementStatusIds.Satisfied, IsBlocking = false },
            ManualRequirement0198(AssetMaintenanceRequirementKindIds0198.Storage, "Хранение подготовлено"),
            ManualRequirement0198(AssetMaintenanceRequirementKindIds0198.Security, "Безопасность обеспечена"),
            new AssetMaintenanceRequirementState0198 { RequirementKind = AssetMaintenanceRequirementKindIds0198.TaxesOrRent, Name = "Налоги или аренда", ResolutionKind = AssetRequirementResolutionKindIds0198.NotApplicable, PublicStatus = ProjectRequirementStatusIds.Satisfied, IsBlocking = false },
            RefRequirement0198(AssetMaintenanceRequirementKindIds0198.LicensesAndDocuments, "Лицензия и документы", license?.Id ?? string.Empty, license?.DisplayName ?? string.Empty),
            ManualRequirement0198(AssetMaintenanceRequirementKindIds0198.MagicOrAnomalyService, "Магическая и аномальная безопасность"),
            new AssetMaintenanceRequirementState0198 { RequirementKind = AssetMaintenanceRequirementKindIds0198.Interval, Name = "Интервал обслуживания", ResolutionKind = AssetRequirementResolutionKindIds0198.ManualGm, PublicStatus = ProjectRequirementStatusIds.Satisfied, IsBlocking = false }
        };

    private static AssetMaintenanceRequirementState0198 ManualRequirement0198(string kind, string name)
        => new() { RequirementKind = kind, Name = name, ResolutionKind = AssetRequirementResolutionKindIds0198.ManualGm, PublicStatus = ProjectRequirementStatusIds.Open, IsBlocking = true };

    private static AssetMaintenanceRequirementState0198 RefRequirement0198(string kind, string name, string id, string display)
        => new() { RequirementKind = kind, Name = name, ResolutionKind = AssetRequirementResolutionKindIds0198.Reference, ReferenceId = id, ReferenceDisplayName = display, PublicStatus = string.IsNullOrWhiteSpace(id) ? ProjectRequirementStatusIds.Open : ProjectRequirementStatusIds.Satisfied, IsBlocking = true };

    private AssetOperationState0198 EnsureAssetOperationState0198(AssetState asset, string actorId)
    {
        var state = _repositories.AssetOperationStates0198.Find(Builders<AssetOperationState0198>.Filter.Eq(x => x.AssetId, asset.Id)).FirstOrDefault();
        if (state != null) return state;
        var ownership = _repositories.CharacterOwnerships.GetById(asset.OwnerId) ?? _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, asset.OwnerId)).FirstOrDefault();
        state = new AssetOperationState0198
        {
            Id = "asset_operation_" + asset.Id,
            CampaignId = asset.CampaignId,
            AssetId = asset.Id,
            OwnerKind = asset.OwnerKind,
            OwnerId = asset.OwnerId,
            OwnerUserId = ownership?.OwnerUserId ?? string.Empty,
            UpdatedByUserId = actorId
        };
        var profile = EnsureMaintenanceProfile0198(asset, actorId);
        EvaluateReadiness0198(asset, profile, state);
        _repositories.AssetOperationStates0198.Insert(state);
        return state;
    }

    private void EvaluateReadiness0198(AssetState asset, LargeAssetMaintenanceProfileState0197 profile, AssetOperationState0198 state)
    {
        var blockers = new List<string>();
        if (asset.IsArchived || !asset.IsActive) blockers.Add("Актив недоступен.");
        var owner = _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, asset.OwnerId)).FirstOrDefault();
        if (owner == null || !owner.IsActive || owner.IsArchived || owner.CharacterStatus != CharacterStatusIds.Active) blockers.Add("Владелец актива неактивен.");
        foreach (var row in profile.Requirements.Where(x => x.IsBlocking))
        {
            var satisfied = row.PublicStatus == ProjectRequirementStatusIds.Satisfied || row.ResolutionKind == AssetRequirementResolutionKindIds0198.NotApplicable;
            if (!satisfied) blockers.Add(row.Name + ": требуется подтверждение.");
        }
        if (profile.Status == AssetMaintenanceStatusIds0198.Due || profile.Status == AssetMaintenanceStatusIds0198.Overdue || profile.Status == AssetMaintenanceStatusIds0198.InMaintenance)
            blockers.Add("Актив требует обслуживания.");
        state.PublicBlockerSummaries = blockers;
        state.GMBlockerSummaries = blockers.ToList();
        state.ReadinessStatus = blockers.Count == 0 ? AssetReadinessStatusIds0198.Ready : AssetReadinessStatusIds0198.Blocked;
        state.ActivePersonnelReferences = string.IsNullOrWhiteSpace(profile.KeySpecialistCharacterId) ? new List<string>() : new List<string> { profile.KeySpecialistCharacterId };
        state.LicenseDocumentReferences = profile.LicenseDocumentReferences.ToList();
        state.OperationalRequirementSnapshot = profile.Requirements.Select(CloneOperationRequirement0198).ToList();
    }

    private static AssetMaintenanceRequirementState0198 CloneOperationRequirement0198(AssetMaintenanceRequirementState0198 x)
        => new() { RequirementKind = x.RequirementKind, Name = x.Name, ResolutionKind = x.ResolutionKind, ReferenceId = x.ReferenceId, ReferenceDisplayName = x.ReferenceDisplayName, RequiredQuantity = x.RequiredQuantity, Unit = x.Unit, PublicStatus = x.PublicStatus, GMStatus = x.GMStatus, GMDetails = x.GMDetails, IsBlocking = x.IsBlocking, IsPlayerVisible = x.IsPlayerVisible, Revision = x.Revision };

    private AssetMaintenanceSnapshot0198 BuildMaintenanceSnapshot0198(AssetState asset, LargeAssetMaintenanceProfileState0197 profile, AssetOperationState0198 operation, CharacterOwnershipState ownership)
    {
        var materials = new List<ProjectMaterialSnapshot0191>
        {
            MaintenanceMaterial0198("reference.resource.structural_composite", "Конструкционный композит", 1, "ед."),
            MaintenanceMaterial0198("reference.resource.arcane_conduit", "Арканный проводник", 1, "ед."),
            MaintenanceMaterial0198("reference.resource.lumen_crystal", "Люмен-кристалл", 1, "шт.")
        };
        var stages = new List<ProjectStageSnapshot0191>
        {
            MaintenanceStage0198("diagnostics", "Диагностика и подготовка", 1),
            MaintenanceStage0198("service", "Техническое и магическое обслуживание", 2),
            MaintenanceStage0198("verification", "Проверка систем и возврат в эксплуатацию", 3)
        };
        return new AssetMaintenanceSnapshot0198
        {
            AssetId = asset.Id, AssetName = asset.Name, AssetKind = asset.AssetType,
            BlueprintStableKey = asset.BlueprintStableKey, BlueprintRevision = asset.BlueprintRevision,
            LocationId = asset.LocationId, LocationName = asset.LocationDisplayName,
            OwnerKind = asset.OwnerKind, OwnerId = asset.OwnerId, OwnerDisplayName = ownership.CharacterDisplayName,
            PreviousOperationStatus = operation.OperationStatus, PreviousMaintenanceStatus = profile.Status,
            MaintenanceInterval = FirstNonEmpty(profile.MaintenanceIntervalDefinitionReference, "30 development days"),
            SpecialistReferenceId = profile.KeySpecialistCharacterId, SpecialistDisplayName = profile.KeySpecialistDisplayName,
            LicenseDocumentReferences = profile.LicenseDocumentReferences.ToList(), Materials = materials,
            Requirements = profile.Requirements.Where(x => x.IsBlocking).Select(x => new ProjectRequirementSnapshot0191 { Kind = x.RequirementKind, DefinitionId = x.ReferenceId, DisplayName = x.Name, Required = true, IsPlayerVisible = x.IsPlayerVisible, PublicExplanation = "Условие обслуживания должно быть подтверждено.", GMExplanation = x.GMDetails }).ToList(),
            Stages = stages, RuleSetId = asset.RuleSetId, ExpectedResultSummary = "Актив Operational, обслуживание Current"
        };
    }

    private void CreateMaintenanceChildren0198(ProjectBaseState project, AssetMaintenanceSnapshot0198 snapshot, string actorId)
    {
        foreach (var row in snapshot.Requirements.Select((value, index) => new { value, index }))
            _repositories.ProjectRequirements.Insert(new ProjectRequirementState { Id = $"asset_maintenance_requirement_{project.Id}_{row.index + 1}", ProjectId = project.Id, CampaignId = project.CampaignId, RequirementType = row.value.Kind, Name = row.value.DisplayName, PublicSummary = row.value.PublicExplanation, GMSummary = row.value.GMExplanation, Status = ProjectRequirementStatusIds.Open, IsRequired = row.value.Required, IsPlayerVisible = row.value.IsPlayerVisible });
        foreach (var stage in snapshot.Stages)
        {
            _repositories.ProjectStages.Insert(new ProjectStageState { Id = $"asset_maintenance_stage_{project.Id}_{stage.Key}", ProjectId = project.Id, CampaignId = project.CampaignId, StageType = ProjectStageTypeIds.Custom, Name = stage.DisplayName, PublicSummary = stage.PublicSummary, Status = stage.Order == 1 ? ProjectStageStatusIds.Available : ProjectStageStatusIds.Locked, SortOrder = stage.Order, IsPlayerVisible = true, UpdatedByUserId = actorId, ExtraData = new Dictionary<string, object> { ["stageKey"] = stage.Key } });
            var material = snapshot.Materials[Math.Min(snapshot.Materials.Count - 1, stage.Order - 1)];
            _repositories.ProjectResourceRequirements.Insert(new ProjectResourceRequirementState { Id = $"asset_maintenance_resource_{project.Id}_{stage.Key}", ProjectId = project.Id, CampaignId = project.CampaignId, ResourceType = "material", ResourceId = material.DefinitionId, DisplayName = material.DisplayName, QuantityRequired = material.Quantity, Unit = material.Unit, Status = ProjectResourceRequirementStatusIds.Needed, IsReservationOnly = true, IsPlayerVisible = true, UpdatedByUserId = actorId, ExtraData = new Dictionary<string, object> { ["stageKey"] = stage.Key } });
        }
    }

    private void ReserveMaintenanceResources0198(ProjectBaseState project, string actorId, string operationId)
    {
        var snapshot = RequireMaintenanceSnapshot0198(project);
        var competing = ActiveMaintenanceProject0198(snapshot.AssetId);
        if (competing != null && competing.Id != project.Id) throw new InvalidOperationException("Актив уже зарезервирован другим проектом обслуживания.");
        var existing = ActiveMaintenanceReservations0198(project.Id).ToArray();
        if (existing.Length > 0)
        {
            if (existing.All(x => x.OperationId == operationId)) return;
            throw new InvalidOperationException("Ресурсы проекта уже зарезервированы.");
        }
        var requirements = _repositories.ProjectResourceRequirements.Find(Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id)).ToArray();
        foreach (var requirement in requirements)
        {
            var item = FindAvailableInventoryItem0191(project.OwnerCharacterId, requirement.ResourceId, requirement.QuantityRequired)
                ?? throw new InvalidOperationException("Недостаточно ресурса: " + requirement.DisplayName);
            var stageKey = Convert.ToString(requirement.ExtraData["stageKey"], CultureInfo.InvariantCulture) ?? string.Empty;
            _repositories.AssetMaintenanceReservations0198.Insert(new AssetMaintenanceReservationState0198 { Id = $"asset_maintenance_reservation_{project.Id}_{stageKey}", ProjectId = project.Id, AssetId = snapshot.AssetId, CampaignId = project.CampaignId, OwnerCharacterId = project.OwnerCharacterId, ResourceDefinitionId = requirement.ResourceId, ResourceDisplayName = requirement.DisplayName, InventoryItemId = item.ItemId, QuantityReserved = requirement.QuantityRequired, Unit = requirement.Unit, StageKey = stageKey, OperationId = operationId });
            requirement.QuantityReserved = requirement.QuantityRequired;
            requirement.Status = ProjectResourceRequirementStatusIds.Reserved;
            requirement.UpdatedByUserId = actorId;
            requirement.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ProjectResourceRequirements.Replace(requirement);
        }
    }

    private void ConsumeMaintenanceStage0198(ProjectBaseState project, ProjectStageState stage, string actorId, string operationId)
    {
        var stageKey = MaintenanceStageKey0198(stage);
        if (FindMaintenanceConsumption0198(project.Id, stageKey) != null) return;
        var reservations = ActiveMaintenanceReservations0198(project.Id).Where(x => x.StageKey == stageKey).ToArray();
        if (reservations.Length == 0) throw new InvalidOperationException("Для стадии отсутствует активный резерв.");
        var document = _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, project.OwnerCharacterId)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Character v2 inventory profile не найден.");
        document.Profile ??= new InventoryProfile { CharacterId = project.OwnerCharacterId, RuleSetId = project.RuleSetId };
        foreach (var reservation in reservations)
        {
            var item = document.Profile.Items.FirstOrDefault(x => x.ItemId == reservation.InventoryItemId) ?? throw new KeyNotFoundException("Зарезервированный материал отсутствует в Character v2 inventory.");
            var units = InventoryUnitsForRequirement0191(reservation.QuantityReserved);
            if (item.Quantity < units) throw new InvalidOperationException("Количество материала изменилось после резервирования.");
            item.Quantity -= units;
            item.UpdatedAtUtc = DateTime.UtcNow;
            item.Source = "asset_maintenance_stage_consumption_0198";
            if (item.Quantity <= 0) document.Profile.Items.Remove(item);
        }
        var previousUpdated = document.UpdatedUtc;
        document.UpdatedUtc = DateTime.UtcNow;
        var write = _mongo.CharacterInventoryProfiles.ReplaceOne(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.Id, document.Id) & Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.UpdatedUtc, previousUpdated), document);
        if (write.MatchedCount != 1) throw new InvalidOperationException("Инвентарь изменился во время списания. Обновите проект.");
        foreach (var reservation in reservations)
        {
            reservation.QuantityConsumed = reservation.QuantityReserved;
            reservation.Status = AssetMaintenanceReservationStatusIds0198.Consumed;
            reservation.ConsumedAtUtc = DateTime.UtcNow;
            reservation.Revision++;
            _repositories.AssetMaintenanceReservations0198.Replace(reservation);
            var requirement = _repositories.ProjectResourceRequirements.Find(Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id) & Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ResourceId, reservation.ResourceDefinitionId)).FirstOrDefault(x => Convert.ToString(x.ExtraData["stageKey"], CultureInfo.InvariantCulture) == stageKey);
            if (requirement != null) { requirement.QuantityProvided = requirement.QuantityRequired; requirement.QuantityReserved = 0; requirement.Status = ProjectResourceRequirementStatusIds.ConsumedManually; requirement.UpdatedAtUtc = DateTime.UtcNow; requirement.UpdatedByUserId = actorId; _repositories.ProjectResourceRequirements.Replace(requirement); }
        }
        var snapshot = RequireMaintenanceSnapshot0198(project);
        _repositories.AssetMaintenanceStageConsumptions0198.Insert(new AssetMaintenanceStageConsumptionState0198 { Id = $"asset_maintenance_consumption_{project.Id}_{stageKey}", ProjectId = project.Id, AssetId = snapshot.AssetId, CampaignId = project.CampaignId, StageKey = stageKey, OperationId = operationId, ConsumedByUserId = actorId, Resources = reservations.Select(x => new ProjectMaterialSnapshot0191 { DefinitionId = x.ResourceDefinitionId, StableKey = x.ResourceDefinitionId, DisplayName = x.ResourceDisplayName, Quantity = x.QuantityConsumed, Unit = x.Unit, UsageMode = "consumed" }).ToList() });
    }

    private MaintenanceServiceRecordState0198 EnsureServiceRecord0198(ProjectBaseState project, string actorId)
    {
        var existing = _repositories.MaintenanceServiceRecords0198.Find(Builders<MaintenanceServiceRecordState0198>.Filter.Eq(x => x.ProjectId, project.Id)).FirstOrDefault();
        if (existing != null) return existing;
        var snapshot = RequireMaintenanceSnapshot0198(project);
        var profile = EnsureMaintenanceProfile0198(RequireAssetById0198(snapshot.AssetId), actorId);
        var record = new MaintenanceServiceRecordState0198 { Id = "maintenance_service_record_" + project.Id, CampaignId = project.CampaignId, AssetId = snapshot.AssetId, ProjectId = project.Id, PreviousMaintenanceStatus = snapshot.PreviousMaintenanceStatus, ResultingMaintenanceStatus = AssetMaintenanceStatusIds0198.Current, SpecialistReferenceId = snapshot.SpecialistReferenceId, SpecialistDisplayName = snapshot.SpecialistDisplayName, ConsumedResources = _repositories.AssetMaintenanceStageConsumptions0198.Find(Builders<AssetMaintenanceStageConsumptionState0198>.Filter.Eq(x => x.ProjectId, project.Id)).SelectMany(x => x.Resources).Select(CloneMaintenanceMaterial0198).ToList(), CompletedStages = LoadMaintenanceStages0198(project.Id).Where(x => x.Status == ProjectStageStatusIds.Completed).Select(x => x.Name).ToList(), PublicSummary = "Плановое обслуживание завершено.", GMSummary = "Все три стадии завершены; ресурсы списаны сервером.", CompletedAtUtc = DateTime.UtcNow, NextDueAtUtc = profile.NextMaintenanceDueAtUtc, NextDueIntervalSnapshot = snapshot.MaintenanceInterval };
        _repositories.MaintenanceServiceRecords0198.Insert(record);
        return record;
    }

    private void RestoreDueRestricted0198(ProjectBaseState project, string actorId, string operationId)
    {
        var asset = RequireAssetById0198(RequireMaintenanceSnapshot0198(project).AssetId);
        var profile = EnsureMaintenanceProfile0198(asset, actorId);
        var operation = EnsureAssetOperationState0198(asset, actorId);
        profile.Status = AssetMaintenanceStatusIds0198.Due;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        profile.Revision++;
        _repositories.LargeAssetMaintenanceProfiles0197.Replace(profile);
        operation.OperationStatus = AssetOperationStatusIds0198.Restricted;
        TouchOperation0198(operation, actorId, operationId, "asset.maintenance.restoreDue");
    }

    private void ReleaseMaintenanceReservations0198(string projectId)
    {
        foreach (var reservation in ActiveMaintenanceReservations0198(projectId).Where(x => x.QuantityConsumed <= 0))
        {
            reservation.Status = AssetMaintenanceReservationStatusIds0198.Released;
            reservation.ReleasedAtUtc = DateTime.UtcNow;
            reservation.Revision++;
            _repositories.AssetMaintenanceReservations0198.Replace(reservation);
            var requirement = _repositories.ProjectResourceRequirements.Find(Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, projectId) & Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ResourceId, reservation.ResourceDefinitionId)).FirstOrDefault();
            if (requirement != null) { requirement.QuantityReserved = 0; requirement.Status = ProjectResourceRequirementStatusIds.Needed; requirement.UpdatedAtUtc = DateTime.UtcNow; _repositories.ProjectResourceRequirements.Replace(requirement); }
        }
    }

    private void CloseMaintenanceReservations0198(string projectId)
    {
        foreach (var reservation in ActiveMaintenanceReservations0198(projectId).Where(x => x.QuantityConsumed <= 0))
        {
            reservation.Status = AssetMaintenanceReservationStatusIds0198.Released;
            reservation.ReleasedAtUtc = DateTime.UtcNow;
            reservation.Revision++;
            _repositories.AssetMaintenanceReservations0198.Replace(reservation);
        }
    }

    private ProjectBaseState? ActiveMaintenanceProject0198(string assetId)
        => _repositories.Projects.Find(Builders<ProjectBaseState>.Filter.Eq(x => x.RuntimeKind, AssetMaintenanceRuntimeIds0198.RuntimeKind) & Builders<ProjectBaseState>.Filter.Ne(x => x.Status, ProjectStatusIds.Completed) & Builders<ProjectBaseState>.Filter.Ne(x => x.Status, ProjectStatusIds.Cancelled) & Builders<ProjectBaseState>.Filter.Ne(x => x.Status, ProjectStatusIds.Failed)).FirstOrDefault(x => x.DefinitionSnapshot?.AssetMaintenance?.AssetId == assetId);

    private IEnumerable<AssetMaintenanceReservationState0198> ActiveMaintenanceReservations0198(string projectId)
        => _repositories.AssetMaintenanceReservations0198.Find(Builders<AssetMaintenanceReservationState0198>.Filter.Eq(x => x.ProjectId, projectId)).Where(x => x.Status == AssetMaintenanceReservationStatusIds0198.Reserved || x.Status == AssetMaintenanceReservationStatusIds0198.PartiallyConsumed);

    private AssetMaintenanceStageConsumptionState0198? FindMaintenanceConsumption0198(string projectId, string stageKey)
        => _repositories.AssetMaintenanceStageConsumptions0198.Find(Builders<AssetMaintenanceStageConsumptionState0198>.Filter.Eq(x => x.ProjectId, projectId) & Builders<AssetMaintenanceStageConsumptionState0198>.Filter.Eq(x => x.StageKey, stageKey)).FirstOrDefault();

    private List<ProjectStageState> LoadMaintenanceStages0198(string projectId)
        => _repositories.ProjectStages.Find(Builders<ProjectStageState>.Filter.Eq(x => x.ProjectId, projectId)).OrderBy(x => x.SortOrder).ToList();

    private static string MaintenanceStageKey0198(ProjectStageState stage)
        => Convert.ToString(stage.ExtraData.TryGetValue("stageKey", out var value) ? value : string.Empty, CultureInfo.InvariantCulture) ?? string.Empty;

    private ProjectBaseState RequireMaintenanceProject0198(IDictionary<string, object> payload)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "projectId"), PayloadReader.GetString(payload, "id")), 1, 160, "projectId");
        var project = _repositories.Projects.GetById(id) ?? throw new KeyNotFoundException("Проект обслуживания не найден.");
        if (project.RuntimeKind != AssetMaintenanceRuntimeIds0198.RuntimeKind) throw new KeyNotFoundException("Проект обслуживания не найден.");
        return project;
    }

    private static AssetMaintenanceSnapshot0198 RequireMaintenanceSnapshot0198(ProjectBaseState project)
        => project.DefinitionSnapshot?.AssetMaintenance ?? throw new InvalidOperationException("Maintenance snapshot отсутствует.");

    private Dictionary<string, object> OperationResponse0198(AssetState asset, bool admin, bool alreadyApplied = false)
        => new() { ["item"] = AssetOperationPayload0198(asset, admin, true), ["alreadyApplied"] = alreadyApplied };

    private Dictionary<string, object> AssetOperationPayload0198(AssetState asset, bool admin, bool details)
    {
        var profile = EnsureMaintenanceProfile0198(asset, string.Empty);
        var state = EnsureAssetOperationState0198(asset, string.Empty);
        EvaluateReadiness0198(asset, profile, state);
        var payload = new Dictionary<string, object>
        {
            ["assetId"] = asset.Id, ["name"] = asset.Name, ["assetKindLabel"] = "Здание", ["locationName"] = asset.LocationDisplayName,
            ["operationStatus"] = state.OperationStatus, ["operationStatusLabel"] = OperationStatusLabel0198(state.OperationStatus),
            ["maintenanceStatus"] = profile.Status, ["maintenanceStatusLabel"] = MaintenanceStatusLabel0198(profile.Status),
            ["readinessStatus"] = state.ReadinessStatus, ["readinessStatusLabel"] = ReadinessStatusLabel0198(state.ReadinessStatus),
            ["blockers"] = state.PublicBlockerSummaries.ToArray(), ["revision"] = state.Revision,
            ["specialistName"] = profile.KeySpecialistDisplayName, ["licenseSummary"] = string.IsNullOrWhiteSpace(asset.LicenseSummary) ? "Документы не указаны" : asset.LicenseSummary,
            ["storageSummary"] = asset.StorageCapacitySummary, ["nextMaintenanceDueAtUtc"] = profile.NextMaintenanceDueAtUtc ?? (object)string.Empty,
            ["activationRequested"] = state.ActivationRequestedAtUtc.HasValue
        };
        if (details)
        {
            payload["requirements"] = profile.Requirements.Where(x => admin || x.IsPlayerVisible).Select(x =>
            {
                var row = new Dictionary<string, object> { ["kind"] = x.RequirementKind, ["name"] = x.Name, ["status"] = x.PublicStatus, ["statusLabel"] = x.PublicStatus == ProjectRequirementStatusIds.Satisfied ? "Выполнено" : "Требуется", ["resolutionKind"] = x.ResolutionKind, ["resolutionLabel"] = ResolutionLabel0198(x.ResolutionKind), ["referenceDisplayName"] = x.ReferenceDisplayName, ["blocking"] = x.IsBlocking };
                if (admin) { row["gmStatus"] = x.GMStatus; row["gmDetails"] = x.GMDetails; }
                return (object)row;
            }).ToArray();
            payload["serviceHistory"] = _repositories.MaintenanceServiceRecords0198.Find(Builders<MaintenanceServiceRecordState0198>.Filter.Eq(x => x.AssetId, asset.Id)).OrderByDescending(x => x.CompletedAtUtc).Select(x => (object)new Dictionary<string, object> { ["summary"] = x.PublicSummary, ["completedAtUtc"] = x.CompletedAtUtc, ["specialistName"] = x.SpecialistDisplayName, ["nextDueAtUtc"] = x.NextDueAtUtc ?? (object)string.Empty }).ToArray();
        }
        if (admin)
        {
            payload["ownerDisplayName"] = AccountDisplayName0191(state.OwnerUserId);
            payload["ownerId"] = asset.OwnerId;
            payload["campaignId"] = asset.CampaignId;
            payload["gmBlockers"] = state.GMBlockerSummaries.ToArray();
        }
        return payload;
    }

    private Dictionary<string, object> MaintenanceResponse0198(ProjectBaseState project, bool admin, bool alreadyApplied = false)
        => new() { ["item"] = MaintenanceProjectPayload0198(project, admin, true), ["alreadyApplied"] = alreadyApplied };

    private Dictionary<string, object> MaintenanceProjectPayload0198(ProjectBaseState project, bool admin, bool details)
    {
        var snapshot = RequireMaintenanceSnapshot0198(project);
        var payload = new Dictionary<string, object> { ["projectId"] = project.Id, ["name"] = project.Name, ["publicSummary"] = project.PublicSummary, ["projectTypeLabel"] = "Обслуживание актива", ["status"] = project.Status, ["statusLabel"] = CraftProjectStatusLabel0191(project.Status), ["approvalStatus"] = project.ApprovalStatus, ["revision"] = project.Revision, ["progressPercent"] = project.ProgressPercent, ["currentStageName"] = project.CurrentStageName, ["assetName"] = snapshot.AssetName, ["assetKindLabel"] = "Здание", ["locationName"] = snapshot.LocationName, ["ownerDisplayName"] = snapshot.OwnerDisplayName, ["specialistName"] = snapshot.SpecialistDisplayName };
        if (!details) return payload;
        payload["requirements"] = _repositories.ProjectRequirements.Find(Builders<ProjectRequirementState>.Filter.Eq(x => x.ProjectId, project.Id)).Where(x => admin || x.IsPlayerVisible).Select(x => { var row = new Dictionary<string, object> { ["name"] = x.Name, ["summary"] = x.PublicSummary, ["status"] = x.Status, ["statusLabel"] = x.Status == ProjectRequirementStatusIds.Satisfied ? "Подтверждено" : "Ожидает GM" }; if (admin) row["requirementId"] = x.Id; return (object)row; }).ToArray();
        payload["resources"] = _repositories.ProjectResourceRequirements.Find(Builders<ProjectResourceRequirementState>.Filter.Eq(x => x.ProjectId, project.Id)).Select(x => (object)new Dictionary<string, object> { ["name"] = x.DisplayName, ["quantityRequired"] = x.QuantityRequired, ["quantityReserved"] = x.QuantityReserved, ["quantityConsumed"] = x.QuantityProvided, ["unit"] = x.Unit, ["statusLabel"] = ResourceStatusLabel0191(x.Status) }).ToArray();
        payload["stages"] = LoadMaintenanceStages0198(project.Id).Select(x => { var row = new Dictionary<string, object> { ["name"] = x.Name, ["summary"] = x.PublicSummary, ["status"] = x.Status, ["statusLabel"] = StageStatusLabel0191(x.Status), ["isCurrent"] = x.Id == project.CurrentStageId }; if (admin) row["stageKey"] = MaintenanceStageKey0198(x); return (object)row; }).ToArray();
        payload["serviceHistory"] = _repositories.MaintenanceServiceRecords0198.Find(Builders<MaintenanceServiceRecordState0198>.Filter.Eq(x => x.AssetId, snapshot.AssetId)).Select(x => (object)new Dictionary<string, object> { ["summary"] = x.PublicSummary, ["completedAtUtc"] = x.CompletedAtUtc, ["specialistName"] = x.SpecialistDisplayName }).ToArray();
        if (admin) { payload["currentStageKey"] = LoadMaintenanceStages0198(project.Id).FirstOrDefault(x => x.Id == project.CurrentStageId) is { } current ? MaintenanceStageKey0198(current) : string.Empty; payload["gmSummary"] = project.GMSummary; payload["audit"] = _repositories.ProjectAuditEntries.Find(Builders<ProjectAuditEntryState>.Filter.Eq(x => x.ProjectId, project.Id)).OrderBy(x => x.CreatedAtUtc).Select(x => (object)new Dictionary<string, object> { ["summary"] = x.Summary, ["actorDisplayName"] = AccountDisplayName0191(x.ActorUserId), ["createdAtUtc"] = x.CreatedAtUtc }).ToArray(); }
        return payload;
    }

    private void TouchOperation0198(AssetOperationState0198 state, string actorId, string operationId, string command)
    { state.Revision++; state.UpdatedAtUtc = DateTime.UtcNow; state.UpdatedByUserId = actorId; state.LastOperationId = operationId; state.LastOperationCommand = command; _repositories.AssetOperationStates0198.Replace(state); }

    private void TouchMaintenanceProject0198(ProjectBaseState project, string actorId, string operationId, string command)
    { project.Revision++; project.UpdatedAtUtc = DateTime.UtcNow; project.UpdatedByUserId = actorId; project.LastOperationId = operationId; project.LastOperationCommand = command; _repositories.Projects.Replace(project); }

    private void AddOperationAudit0198(AssetState asset, string actorId, string operationId, string action, string summary, bool playerVisible, ProjectBaseState? project = null)
    {
        var auditProject = project ?? _repositories.Projects.GetById(asset.ConstructionProjectId);
        if (auditProject != null) AddCraftAudit0191(auditProject, actorId, operationId, action, summary, summary, playerVisible);
    }

    private void AddMaintenanceAudit0198(ProjectBaseState project, string actorId, string operationId, string action, string summary, bool playerVisible)
        => AddCraftAudit0191(project, actorId, operationId, action, summary, summary, playerVisible);

    private static void RequireExpectedRevision0198(IDictionary<string, object> payload, int actual, string entity)
    { var expected = PayloadReader.GetInt(payload, "expectedRevision") ?? 0; if (expected <= 0) throw new ArgumentException("expectedRevision is required for " + entity + "."); if (expected != actual) throw new InvalidOperationException("Revision is stale. Refresh and retry."); }

    private bool AssetMaintenanceViewEnabled0198(bool admin)
        => _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedProjectRuntimeV1))
           && _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseAssetMaintenanceProjectV1))
           && (admin ? _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedAssetMaintenanceAdminView)) : _featureFlags.IsEnabled(nameof(UnifiedProjectRuntimeFeatureFlags.UseUnifiedAssetMaintenancePlayerView)));

    private ResponseEnvelope AssetMaintenanceDisabled0198(string command)
    { _logger.Admin("asset.maintenance.disabled command=" + command); return Error("Asset operation and maintenance runtime is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden); }

    private static ProjectMaterialSnapshot0191 MaintenanceMaterial0198(string id, string name, decimal quantity, string unit) => new() { DefinitionId = id, StableKey = id, DisplayName = name, Quantity = quantity, Unit = unit, UsageMode = "consumed" };
    private static ProjectMaterialSnapshot0191 CloneMaintenanceMaterial0198(ProjectMaterialSnapshot0191 x) => new() { DefinitionId = x.DefinitionId, StableKey = x.StableKey, DisplayName = x.DisplayName, Quantity = x.Quantity, Unit = x.Unit, MinimumQuality = x.MinimumQuality, UsageMode = x.UsageMode, Optional = x.Optional };
    private static ProjectRequirementSnapshot0191 CloneMaintenanceRequirement0198(ProjectRequirementSnapshot0191 x) => new() { Kind = x.Kind, DefinitionId = x.DefinitionId, DisplayName = x.DisplayName, Quantity = x.Quantity, MinimumQualityOrRank = x.MinimumQualityOrRank, Required = x.Required, IsPlayerVisible = x.IsPlayerVisible, ConsumptionMode = x.ConsumptionMode, PublicExplanation = x.PublicExplanation, GMExplanation = x.GMExplanation };
    private static ProjectStageSnapshot0191 CloneMaintenanceStage0198(ProjectStageSnapshot0191 x) => new() { Key = x.Key, DisplayName = x.DisplayName, Order = x.Order, AllowedPreviousStageKeys = x.AllowedPreviousStageKeys.ToList(), AllowedNextStageKeys = x.AllowedNextStageKeys.ToList(), RequiredConditions = x.RequiredConditions, RequiresGMDecision = x.RequiresGMDecision, IsPlayerVisible = x.IsPlayerVisible, PublicSummary = x.PublicSummary };
    private static ProjectStageSnapshot0191 MaintenanceStage0198(string key, string name, int order) => new() { Key = key, DisplayName = name, Order = order, RequiresGMDecision = true, IsPlayerVisible = true, PublicSummary = name + "." };
    private static string OperationStatusLabel0198(string status) => status switch { AssetOperationStatusIds0198.Operational => "Эксплуатируется", AssetOperationStatusIds0198.Restricted => "Эксплуатация ограничена", AssetOperationStatusIds0198.Suspended => "Эксплуатация приостановлена", _ => "Не введён в эксплуатацию" };
    private static string MaintenanceStatusLabel0198(string status) => status switch { AssetMaintenanceStatusIds0198.Current => "Обслуживание актуально", AssetMaintenanceStatusIds0198.Due => "Срок обслуживания наступил", AssetMaintenanceStatusIds0198.InMaintenance => "На обслуживании", AssetMaintenanceStatusIds0198.Overdue => "Обслуживание просрочено", _ => "Не настроено" };
    private static string ReadinessStatusLabel0198(string status) => status switch { AssetReadinessStatusIds0198.Ready => "Готов", AssetReadinessStatusIds0198.ReadyWithWarnings => "Готов с предупреждениями", _ => "Заблокирован" };
    private static string ResolutionLabel0198(string kind) => kind switch { AssetRequirementResolutionKindIds0198.Reference => "Связанный объект", AssetRequirementResolutionKindIds0198.Resource => "Ресурс", AssetRequirementResolutionKindIds0198.NotApplicable => "Не требуется", _ => "Подтверждает GM" };
}
