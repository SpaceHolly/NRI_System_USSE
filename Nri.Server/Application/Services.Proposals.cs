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
    public ResponseEnvelope ProposalTypesList(CommandContext context)
    {
        GetCurrentAccount(context);
        return Ok("Proposal types loaded.", new Dictionary<string, object>
        {
            { "items", BuiltInProposalTemplates().Select(x => (object)ProposalTemplatePayload(x, admin: false)).ToArray() }
        });
    }

    public ResponseEnvelope ProposalStatusExplain(CommandContext context)
    {
        GetCurrentAccount(context);
        var items = new[]
        {
            Row("draft", "Черновик", "Игрок может редактировать."),
            Row("submitted", "Отправлено", "Ожидает просмотра GM."),
            Row("in_gm_review", "На рассмотрении", "GM открыл заявку."),
            Row("changes_requested", "Нужны правки", "Игрок может исправить и отправить снова."),
            Row("approved", "Одобрено", "GM одобрил, но результат ещё не применён."),
            Row("converted", "Конвертировано", "GM явно создал связанный проект/заказ/проверку."),
            Row("rejected", "Отклонено", "GM отклонил предложение.")
        };
        return Ok("Proposal statuses loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProposalPlayerTemplateList(CommandContext context)
    {
        GetCurrentAccount(context);
        if (!ProposalPlayerCenterEnabled()) return ProposalDisabled(context);
        var campaignId = ProposalCampaignId(context);
        var persisted = _mongo.ProposalTemplateDefinitions.Find(Builders<ProposalTemplateDefinition>.Filter.Eq(x => x.IsPlayerVisible, true)
            & Builders<ProposalTemplateDefinition>.Filter.Eq(x => x.IsArchived, false)
            & (Builders<ProposalTemplateDefinition>.Filter.Eq(x => x.CampaignId, string.Empty) | Builders<ProposalTemplateDefinition>.Filter.Eq(x => x.CampaignId, campaignId)))
            .ToList();
        var items = BuiltInProposalTemplates().Concat(persisted)
            .GroupBy(x => x.ProposalType, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(x => (object)ProposalTemplatePayload(x, admin: false))
            .ToArray();
        return Ok("Player proposal templates loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProposalAdminTemplateList(CommandContext context)
    {
        RequireAdmin(context);
        if (!ProposalAdminReviewEnabled()) return ProposalDisabled(context);
        var campaignId = ProposalCampaignId(context);
        var items = BuiltInProposalTemplates()
            .Concat(_mongo.ProposalTemplateDefinitions.Find(Builders<ProposalTemplateDefinition>.Filter.Empty).ToList())
            .Where(x => string.IsNullOrWhiteSpace(x.CampaignId) || x.CampaignId == campaignId)
            .Select(x => (object)ProposalTemplatePayload(x, admin: true))
            .ToArray();
        return Ok("Admin proposal templates loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProposalAdminTemplateCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProposalAdminReviewEnabled()) return ProposalDisabled(context);
        var template = BuildTemplate(context.Request.Payload);
        template.Id = string.IsNullOrWhiteSpace(template.Id) ? Guid.NewGuid().ToString("N") : template.Id;
        template.ProposalTemplateId = FirstNonEmpty(template.ProposalTemplateId, template.Id);
        _mongo.ProposalTemplateDefinitions.InsertOne(template);
        _logger.Admin($"proposal.template.create actor={actor.Login} templateId={template.Id} type={template.ProposalType}");
        return Ok("Proposal template created.", new Dictionary<string, object> { { "item", ProposalTemplatePayload(template, admin: true) } });
    }

    public ResponseEnvelope ProposalAdminTemplateUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProposalAdminReviewEnabled()) return ProposalDisabled(context);
        var template = RequireTemplate(context);
        UpdateTemplate(template, context.Request.Payload);
        template.UpdatedUtc = DateTime.UtcNow;
        _mongo.ProposalTemplateDefinitions.ReplaceOne(Builders<ProposalTemplateDefinition>.Filter.Eq(x => x.Id, template.Id), template);
        _logger.Admin($"proposal.template.update actor={actor.Login} templateId={template.Id}");
        return Ok("Proposal template updated.", new Dictionary<string, object> { { "item", ProposalTemplatePayload(template, admin: true) } });
    }

    public ResponseEnvelope ProposalAdminTemplateArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProposalAdminReviewEnabled()) return ProposalDisabled(context);
        var template = RequireTemplate(context);
        template.IsArchived = true;
        template.Archived = true;
        template.UpdatedUtc = DateTime.UtcNow;
        _mongo.ProposalTemplateDefinitions.ReplaceOne(Builders<ProposalTemplateDefinition>.Filter.Eq(x => x.Id, template.Id), template);
        _logger.Admin($"proposal.template.archive actor={actor.Login} templateId={template.Id}");
        return Ok("Proposal template archived.", new Dictionary<string, object> { { "item", ProposalTemplatePayload(template, admin: true) } });
    }

    public ResponseEnvelope ProposalPlayerDraftListMine(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalPlayerCenterEnabled()) return ProposalDisabled(context);
        var campaignId = ProposalCampaignId(context);
        var filter = Builders<PlayerProposalDraftState>.Filter.Eq(x => x.CreatedByUserId, actor.Id);
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<PlayerProposalDraftState>.Filter.Eq(x => x.CampaignId, campaignId);
        var items = _mongo.PlayerProposalDrafts.Find(filter)
            .SortByDescending(x => x.UpdatedAtUtc)
            .Limit(200)
            .ToList()
            .Select(x => (object)ProposalDraftPayload(x, admin: false))
            .ToArray();
        return Ok("Player proposals loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProposalPlayerDraftGetMine(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalPlayerCenterEnabled()) return ProposalDisabled(context);
        var draft = RequireOwnProposal(context, actor);
        return Ok("Player proposal loaded.", ProposalSinglePayload(draft, admin: false));
    }

    public ResponseEnvelope ProposalPlayerDraftCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalEditorsEnabled()) return ProposalDisabled(context);
        var draft = BuildProposalDraft(context.Request.Payload, actor);
        EnsureProposalEditorEnabled(draft.ProposalType);
        ValidatePlayerProposalOwnership(actor, draft.CharacterId);
        var validation = ValidateProposalDraft(draft, actor.Id, playerSafe: true);
        draft.ValidationSummary = validation.Summary;
        draft.PublicSummary = BuildProposalPublicSummary(draft);
        _mongo.PlayerProposalDrafts.InsertOne(draft);
        _mongo.PlayerProposalValidations.InsertOne(validation);
        _logger.Admin($"proposal.draft.create actor={actor.Login} proposalId={draft.Id} type={draft.ProposalType}");
        TryPublishProposalSync(draft, "created", actor.Id, context.Request.RequestId);
        return Ok("Proposal draft created.", ProposalSinglePayload(draft, admin: false, validation: validation));
    }

    public ResponseEnvelope ProposalPlayerDraftUpdate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalEditorsEnabled()) return ProposalDisabled(context);
        var draft = RequireOwnEditableProposal(context, actor);
        UpdateProposalDraft(draft, context.Request.Payload, actor.Id);
        ValidatePlayerProposalOwnership(actor, draft.CharacterId);
        var validation = ValidateProposalDraft(draft, actor.Id, playerSafe: true);
        draft.ValidationSummary = validation.Summary;
        _mongo.PlayerProposalDrafts.ReplaceOne(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, draft.Id), draft);
        _mongo.PlayerProposalValidations.InsertOne(validation);
        _logger.Admin($"proposal.draft.update actor={actor.Login} proposalId={draft.Id}");
        TryPublishProposalSync(draft, "updated", actor.Id, context.Request.RequestId);
        return Ok("Proposal draft updated.", ProposalSinglePayload(draft, admin: false, validation: validation));
    }

    public ResponseEnvelope ProposalPlayerDraftValidate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalValidationEnabled()) return ProposalDisabled(context);
        var draft = RequireOwnProposal(context, actor);
        var validation = ValidateProposalDraft(draft, actor.Id, playerSafe: true);
        draft.ValidationSummary = validation.Summary;
        _mongo.PlayerProposalDrafts.ReplaceOne(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, draft.Id), draft);
        _mongo.PlayerProposalValidations.InsertOne(validation);
        _logger.Admin($"proposal.draft.validate actor={actor.Login} proposalId={draft.Id} status={validation.Status}");
        return Ok("Proposal validation complete.", ProposalSinglePayload(draft, admin: false, validation: validation));
    }

    public ResponseEnvelope ProposalPlayerDraftPreview(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalPreviewEnabled()) return ProposalDisabled(context);
        var draft = RequireOwnProposal(context, actor);
        var validation = ValidateProposalDraft(draft, actor.Id, playerSafe: true);
        return Ok("Proposal preview ready.", ProposalSinglePayload(draft, admin: false, validation: validation));
    }

    public ResponseEnvelope ProposalPlayerDraftSubmit(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalSubmitEnabled()) return ProposalDisabled(context);
        var draft = RequireOwnEditableProposal(context, actor);
        var validation = ValidateProposalDraft(draft, actor.Id, playerSafe: true);
        if (!validation.CanSubmit) return Error(validation.Summary, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        draft.ProposalStatus = ProposalStatusIds.Submitted;
        draft.SubmittedAtUtc = DateTime.UtcNow;
        draft.PlayerComment = RequireLength(PayloadReader.GetString(context.Request.Payload, "playerComment"), 0, 2048, "playerComment");
        TouchProposalDraft(draft, actor.Id);

        if (PayloadReader.GetBool(context.Request.Payload, "createPlayerRequest") || ProposalRequestIntegrationEnabled())
            LinkProposalToPlayerRequest(draft, actor);

        _mongo.PlayerProposalDrafts.ReplaceOne(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, draft.Id), draft);
        _mongo.PlayerProposalValidations.InsertOne(validation);
        UpsertProposalReview(draft, ProposalReviewStatusIds.Pending, string.Empty, string.Empty, string.Empty, string.Empty);
        TryWriteProposalJournal(draft, "proposal.submit", "Предложение отправлено GM.", actor.Id);
        TryPublishProposalSync(draft, "submitted", actor.Id, context.Request.RequestId);
        _logger.Admin($"proposal.draft.submit actor={actor.Login} proposalId={draft.Id} requestId={draft.LinkedPlayerRequestId}");
        return Ok("Proposal submitted.", ProposalSinglePayload(draft, admin: false, validation: validation));
    }

    public ResponseEnvelope ProposalPlayerDraftCancel(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalPlayerCenterEnabled()) return ProposalDisabled(context);
        var draft = RequireOwnProposal(context, actor);
        if (draft.ProposalStatus == ProposalStatusIds.Converted || draft.ProposalStatus == ProposalStatusIds.Archived)
            throw new InvalidOperationException("Converted or archived proposals cannot be cancelled.");
        draft.ProposalStatus = ProposalStatusIds.Cancelled;
        draft.CancelledAtUtc = DateTime.UtcNow;
        TouchProposalDraft(draft, actor.Id);
        _mongo.PlayerProposalDrafts.ReplaceOne(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, draft.Id), draft);
        TryPublishProposalSync(draft, "cancelled", actor.Id, context.Request.RequestId);
        return Ok("Proposal cancelled.", ProposalSinglePayload(draft, admin: false));
    }

    public ResponseEnvelope ProposalPlayerDraftArchive(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalPlayerCenterEnabled()) return ProposalDisabled(context);
        var draft = RequireOwnProposal(context, actor);
        draft.ProposalStatus = ProposalStatusIds.Archived;
        draft.Archived = true;
        draft.ArchivedAtUtc = DateTime.UtcNow;
        TouchProposalDraft(draft, actor.Id);
        _mongo.PlayerProposalDrafts.ReplaceOne(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, draft.Id), draft);
        return Ok("Proposal archived.", ProposalSinglePayload(draft, admin: false));
    }

    public ResponseEnvelope ProposalPlayerDraftResubmitAfterChanges(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalSubmitEnabled()) return ProposalDisabled(context);
        var draft = RequireOwnProposal(context, actor);
        if (draft.ProposalStatus != ProposalStatusIds.ChangesRequested)
            throw new InvalidOperationException("Only proposals with requested changes can be resubmitted.");
        draft.ProposalStatus = ProposalStatusIds.Submitted;
        draft.SubmittedAtUtc = DateTime.UtcNow;
        TouchProposalDraft(draft, actor.Id);
        _mongo.PlayerProposalDrafts.ReplaceOne(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, draft.Id), draft);
        UpsertProposalReview(draft, ProposalReviewStatusIds.Pending, string.Empty, "Игрок отправил правки повторно.", string.Empty, string.Empty);
        return Ok("Proposal resubmitted.", ProposalSinglePayload(draft, admin: false));
    }

    public ResponseEnvelope ProposalPlayerLinkedOpen(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProposalPlayerCenterEnabled()) return ProposalDisabled(context);
        var draft = RequireOwnProposal(context, actor);
        return Ok("Proposal links loaded.", new Dictionary<string, object>
        {
            { "proposal", ProposalDraftPayload(draft, admin: false) },
            { "linkedPlayerRequestId", draft.LinkedPlayerRequestId },
            { "linkedProjectId", draft.LinkedProjectId },
            { "linkedSpecializedEntityType", draft.LinkedSpecializedEntityType },
            { "linkedSpecializedEntityId", draft.LinkedSpecializedEntityId },
            { "linkedResultEntityType", draft.LinkedResultEntityType },
            { "linkedResultEntityId", draft.LinkedResultEntityId }
        });
    }

    public ResponseEnvelope ProposalAdminList(CommandContext context)
    {
        RequireAdmin(context);
        if (!ProposalAdminReviewEnabled()) return ProposalDisabled(context);
        var campaignId = ProposalCampaignId(context);
        var status = NormalizeProposalStatus(PayloadReader.GetString(context.Request.Payload, "status"), allowEmpty: true);
        var filter = FilterDefinition<PlayerProposalDraftState>.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<PlayerProposalDraftState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(status)) filter &= Builders<PlayerProposalDraftState>.Filter.Eq(x => x.ProposalStatus, status);
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        if (!includeArchived) filter &= Builders<PlayerProposalDraftState>.Filter.Ne(x => x.ProposalStatus, ProposalStatusIds.Archived);
        var items = _mongo.PlayerProposalDrafts.Find(filter)
            .SortByDescending(x => x.UpdatedAtUtc)
            .Limit(300)
            .ToList()
            .Select(x => (object)ProposalDraftPayload(x, admin: true))
            .ToArray();
        return Ok("Admin proposals loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProposalAdminGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!ProposalAdminReviewEnabled()) return ProposalDisabled(context);
        var draft = RequireProposal(context);
        return Ok("Admin proposal loaded.", ProposalSinglePayload(draft, admin: true));
    }

    public ResponseEnvelope ProposalAdminReviewStart(CommandContext context)
        => AdminProposalReviewTransition(context, ProposalStatusIds.InGmReview, ProposalReviewStatusIds.InReview, "proposal.review.start");

    public ResponseEnvelope ProposalAdminReviewRequestChanges(CommandContext context)
        => AdminProposalReviewTransition(context, ProposalStatusIds.ChangesRequested, ProposalReviewStatusIds.ChangesRequested, "proposal.review.requestChanges");

    public ResponseEnvelope ProposalAdminReviewApprove(CommandContext context)
        => AdminProposalReviewTransition(context, ProposalStatusIds.Approved, ProposalReviewStatusIds.Approved, "proposal.review.approve");

    public ResponseEnvelope ProposalAdminReviewReject(CommandContext context)
        => AdminProposalReviewTransition(context, ProposalStatusIds.Rejected, ProposalReviewStatusIds.Rejected, "proposal.review.reject");

    public ResponseEnvelope ProposalAdminArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProposalAdminReviewEnabled()) return ProposalDisabled(context);
        var draft = RequireProposal(context);
        draft.ProposalStatus = ProposalStatusIds.Archived;
        draft.Archived = true;
        draft.ArchivedAtUtc = DateTime.UtcNow;
        TouchProposalDraft(draft, actor.Id);
        _mongo.PlayerProposalDrafts.ReplaceOne(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, draft.Id), draft);
        return Ok("Proposal archived.", ProposalSinglePayload(draft, admin: true));
    }

    public ResponseEnvelope ProposalAdminValidationRun(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProposalValidationEnabled()) return ProposalDisabled(context);
        var draft = RequireProposal(context);
        var validation = ValidateProposalDraft(draft, actor.Id, playerSafe: false);
        draft.ValidationSummary = validation.Summary;
        _mongo.PlayerProposalDrafts.ReplaceOne(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, draft.Id), draft);
        _mongo.PlayerProposalValidations.InsertOne(validation);
        return Ok("Proposal validation complete.", ProposalSinglePayload(draft, admin: true, validation: validation));
    }

    public ResponseEnvelope ProposalAdminConvertToResearch(CommandContext context) => ConvertProposalToProject(context, ProposalConversionTypeIds.CreateResearchProject, ProjectTypeIds.Research);
    public ResponseEnvelope ProposalAdminConvertToCrafting(CommandContext context) => ConvertProposalToProject(context, ProposalConversionTypeIds.CreateCraftingProject, ProjectTypeIds.Crafting);
    public ResponseEnvelope ProposalAdminConvertToEngineering(CommandContext context) => ConvertProposalToProject(context, ProposalConversionTypeIds.CreateEngineeringProject, ProjectTypeIds.EngineeringDesign);
    public ResponseEnvelope ProposalAdminConvertToFactoryQuote(CommandContext context) => ConvertProposalToProject(context, ProposalConversionTypeIds.CreateFactoryQuote, ProjectTypeIds.FactoryOrder);
    public ResponseEnvelope ProposalAdminConvertToFactoryOrder(CommandContext context) => ConvertProposalToProject(context, ProposalConversionTypeIds.CreateFactoryOrder, ProjectTypeIds.FactoryOrder);
    public ResponseEnvelope ProposalAdminConvertToManufacturing(CommandContext context) => ConvertProposalToProject(context, ProposalConversionTypeIds.CreateManufacturingProject, ProjectTypeIds.Manufacturing);
    public ResponseEnvelope ProposalAdminConvertToDevelopmentPurchase(CommandContext context) => ConvertProposalToProject(context, ProposalConversionTypeIds.CreateDevelopmentPurchaseRequest, ProjectTypeIds.CustomProposal);
    public ResponseEnvelope ProposalAdminConvertToGenericProject(CommandContext context) => ConvertProposalToProject(context, ProposalConversionTypeIds.CreateGenericProject, ProjectTypeIds.Generic);

    public ResponseEnvelope ProposalAdminConvertToLegalCheck(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProposalConversionEnabled()) return ProposalDisabled(context);
        var draft = RequireConvertibleProposal(context);
        var payload = new Dictionary<string, object>(draft.StructuredPayload, StringComparer.OrdinalIgnoreCase)
        {
            ["campaignId"] = draft.CampaignId,
            ["actorUserId"] = draft.CreatedByUserId,
            ["sourceEntityType"] = "proposal",
            ["sourceEntityId"] = draft.Id
        };
        var request = new LegalCheckRequest
        {
            CampaignId = FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), draft.CampaignId),
            JurisdictionId = PayloadReader.GetString(payload, "jurisdictionId") ?? string.Empty,
            ActorUserId = FirstNonEmpty(PayloadReader.GetString(payload, "actorUserId"), draft.CreatedByUserId),
            ActorEntityType = FirstNonEmpty(PayloadReader.GetString(payload, "actorEntityType"), "user"),
            ActorEntityId = FirstNonEmpty(PayloadReader.GetString(payload, "actorEntityId"), draft.CreatedByUserId),
            ActionType = FirstNonEmpty(PayloadReader.GetString(payload, "actionType"), LegalActionTypeIds.Own),
            ObjectType = FirstNonEmpty(PayloadReader.GetString(payload, "objectType"), PayloadReader.GetString(payload, "objectEntityType"), "proposal"),
            ObjectCategory = PayloadReader.GetString(payload, "objectCategory") ?? string.Empty,
            ObjectEntityId = FirstNonEmpty(PayloadReader.GetString(payload, "objectEntityId"), draft.Id),
            ObjectDisplayName = FirstNonEmpty(PayloadReader.GetString(payload, "objectDisplayName"), draft.Title),
            SubjectKind = PayloadReader.GetString(payload, "subjectKind") ?? LegalSubjectKindIds.Any,
            SubjectStatus = PayloadReader.GetString(payload, "subjectStatus") ?? string.Empty,
            ProductionMode = PayloadReader.GetString(payload, "productionMode") ?? string.Empty
        };
        var result = EvaluateLegalCheck(request, includeAdmin: true, actor.Id);
        var record = LegalCheckRecordFromRequest(request, result, includeAdmin: true, checkedByUserId: actor.Id);
        record.ExtraData["proposalDraftId"] = draft.Id;
        _mongo.LegalCheckRecords.InsertOne(record);
        return CompleteProposalConversion(context, draft, actor, ProposalConversionTypeIds.CreateLegalCheck, "legal_check", record.Id, "Юридическая проверка создана.");
    }

    public ResponseEnvelope ProposalAdminConvertToLicenseApplication(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProposalConversionEnabled()) return ProposalDisabled(context);
        var draft = RequireConvertibleProposal(context);
        var now = DateTime.UtcNow;
        var application = new LicenseApplicationState
        {
            CampaignId = draft.CampaignId,
            LicenseDefinitionId = ProposalPayloadString(draft, "licenseDefinitionId"),
            JurisdictionId = ProposalPayloadString(draft, "jurisdictionId"),
            ApplicantUserId = draft.CreatedByUserId,
            ApplicantEntityType = FirstNonEmpty(draft.OwnerEntityType, "character"),
            ApplicantEntityId = FirstNonEmpty(draft.OwnerEntityId, draft.CharacterId),
            Title = FirstNonEmpty(draft.Title, "Заявка на лицензию"),
            Reason = FirstNonEmpty(draft.PlayerComment, draft.Description, ProposalPayloadString(draft, "applicationReason")),
            Status = LicenseApplicationStatusIds.Submitted,
            LinkedRequestId = draft.LinkedPlayerRequestId,
            IsPlayerVisible = true,
            SubmittedAtUtc = now,
            UpdatedAtUtc = now
        };
        application.ExtraData["proposalDraftId"] = draft.Id;
        _mongo.LegalLicenseApplications.InsertOne(application);
        return CompleteProposalConversion(context, draft, actor, ProposalConversionTypeIds.CreateLicenseApplication, "license_application", application.Id, "Заявка на лицензию создана.");
    }

    public ResponseEnvelope ProposalAdminLinkExisting(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProposalConversionEnabled()) return ProposalDisabled(context);
        var draft = RequireConvertibleProposal(context);
        var targetType = RequireLength(PayloadReader.GetString(context.Request.Payload, "targetEntityType"), 1, 96, "targetEntityType");
        var targetId = RequireLength(PayloadReader.GetString(context.Request.Payload, "targetEntityId"), 1, 160, "targetEntityId");
        return CompleteProposalConversion(context, draft, actor, ProposalConversionTypeIds.LinkExistingEntity, targetType, targetId, "Предложение связано с существующей сущностью.");
    }

    private ResponseEnvelope AdminProposalReviewTransition(CommandContext context, string proposalStatus, string reviewStatus, string logAction)
    {
        var actor = RequireAdmin(context);
        if (!ProposalAdminReviewEnabled()) return ProposalDisabled(context);
        var draft = RequireProposal(context);
        draft.ProposalStatus = proposalStatus;
        var playerComment = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "playerVisibleComment"), PayloadReader.GetString(context.Request.Payload, "comment"), PayloadReader.GetString(context.Request.Payload, "playerVisibleReason")), 0, 4096, "playerVisibleComment");
        var gmComment = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "gmComment"), PayloadReader.GetString(context.Request.Payload, "gmNotes"), PayloadReader.GetString(context.Request.Payload, "gmReason")), 0, 4096, "gmComment");
        var requestedChanges = RequireLength(PayloadReader.GetString(context.Request.Payload, "requestedChanges"), 0, 4096, "requestedChanges");
        var reason = RequireLength(PayloadReader.GetString(context.Request.Payload, "decisionReason"), 0, 2048, "decisionReason");
        TouchProposalDraft(draft, actor.Id);
        _mongo.PlayerProposalDrafts.ReplaceOne(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, draft.Id), draft);
        UpsertProposalReview(draft, reviewStatus, actor.Id, playerComment, gmComment, requestedChanges, reason);
        TryWriteProposalJournal(draft, logAction, playerComment, actor.Id);
        TryPublishProposalSync(draft, reviewStatus, actor.Id, context.Request.RequestId);
        _logger.Admin($"{logAction} actor={actor.Login} proposalId={draft.Id} status={draft.ProposalStatus}");
        return Ok("Proposal review updated.", ProposalSinglePayload(draft, admin: true));
    }

    private ResponseEnvelope ConvertProposalToProject(CommandContext context, string conversionType, string projectType)
    {
        var actor = RequireAdmin(context);
        if (!ProposalConversionEnabled()) return ProposalDisabled(context);
        var draft = RequireConvertibleProposal(context);
        if (ProposalProjectIntegrationEnabled())
        {
            var project = new ProjectBaseState
            {
                CampaignId = draft.CampaignId,
                ProjectType = projectType,
                Name = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "projectName"), draft.Title),
                PublicSummary = FirstNonEmpty(draft.PublicSummary, draft.Description),
                GMSummary = draft.GMReviewSummary,
                Status = ProjectStatusIds.InReview,
                ApprovalStatus = ProjectApprovalStatusIds.PendingGmReview,
                OwnerUserId = draft.CreatedByUserId,
                OwnerDisplayName = draft.CreatedByDisplayName,
                OwnerCharacterId = draft.CharacterId,
                CreatedByUserId = actor.Id,
                UpdatedByUserId = actor.Id,
                VisibilityMode = PayloadReader.GetBool(context.Request.Payload, "createPlayerVisibleProject") ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly,
                IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "createPlayerVisibleProject"),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                SubmittedAtUtc = DateTime.UtcNow,
                PublicNotes = FirstNonEmpty(draft.PlayerComment, draft.Description),
                GMNotes = RequireLength(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 0, 4096, "gmNotes"),
                ProposalPayload = new Dictionary<string, object>(draft.StructuredPayload, StringComparer.OrdinalIgnoreCase)
            };
            project.ProposalPayload["proposalDraftId"] = draft.Id;
            project.ProposalPayload["linkedPlayerRequestId"] = draft.LinkedPlayerRequestId;
            _repositories.Projects.Insert(project);
            draft.LinkedProjectId = project.Id;
            return CompleteProposalConversion(context, draft, actor, conversionType, "project", project.Id, "Связанный проект создан.");
        }

        return CompleteProposalConversion(context, draft, actor, conversionType, "project", string.Empty, "Конвертация зафиксирована; Project Foundation выключен feature flags.");
    }

    private ResponseEnvelope CompleteProposalConversion(CommandContext context, PlayerProposalDraftState draft, UserAccount actor, string conversionType, string targetType, string targetId, string summary)
    {
        draft.ProposalStatus = ProposalStatusIds.Converted;
        draft.ConvertedAtUtc = DateTime.UtcNow;
        draft.ConvertedByUserId = actor.Id;
        draft.LinkedSpecializedEntityType = targetType;
        draft.LinkedSpecializedEntityId = targetId;
        TouchProposalDraft(draft, actor.Id);
        _mongo.PlayerProposalDrafts.ReplaceOne(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, draft.Id), draft);

        var conversion = new PlayerProposalConversionState
        {
            ConversionId = Guid.NewGuid().ToString("N"),
            CampaignId = draft.CampaignId,
            ProposalDraftId = draft.Id,
            LinkedPlayerRequestId = draft.LinkedPlayerRequestId,
            ConversionType = conversionType,
            TargetEntityType = targetType,
            TargetEntityId = targetId,
            SourceSummary = draft.PublicSummary,
            ConversionSummary = summary,
            ConvertedByUserId = actor.Id,
            PublicSummary = summary,
            GMSummary = RequireLength(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 0, 4096, "gmNotes"),
            IsPlayerVisible = true,
            VisibilityMode = ProjectVisibilityModeIds.PlayerVisible
        };
        _mongo.PlayerProposalConversions.InsertOne(conversion);
        UpsertProposalReview(draft, ProposalReviewStatusIds.Converted, actor.Id, summary, conversion.GMSummary, string.Empty);
        MarkLinkedRequestFulfilled(draft, actor, summary);
        TryWriteProposalJournal(draft, "proposal.convert", summary, actor.Id);
        TryPublishProposalSync(draft, "converted", actor.Id, context.Request.RequestId);
        _logger.Admin($"proposal.convert.done actor={actor.Login} proposalId={draft.Id} conversion={conversionType} target={targetType}:{targetId}");
        return Ok("Proposal converted.", ProposalSinglePayload(draft, admin: true, conversion: conversion));
    }

    private PlayerProposalDraftState BuildProposalDraft(IDictionary<string, object> payload, UserAccount actor)
    {
        var now = DateTime.UtcNow;
        var proposalType = NormalizeProposalType(PayloadReader.GetString(payload, "proposalType"));
        var category = NormalizeProposalCategory(FirstNonEmpty(PayloadReader.GetString(payload, "proposalCategory"), DefaultProposalCategory(proposalType)));
        var title = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "title"), DefaultProposalTitle(proposalType)), 2, 180, "title");
        var description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        var structuredPayload = SanitizeProposalPayload(PayloadReader.GetDictionary(payload, "structuredPayload") ?? PayloadReader.GetDictionary(payload, "payload") ?? new Dictionary<string, object>());
        var draft = new PlayerProposalDraftState
        {
            ProposalDraftId = Guid.NewGuid().ToString("N"),
            CampaignId = ProposalCampaignId(payload),
            CreatedByUserId = actor.Id,
            CreatedByDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            OwnerEntityType = RequireLength(PayloadReader.GetString(payload, "ownerEntityType"), 0, 64, "ownerEntityType"),
            OwnerEntityId = RequireLength(PayloadReader.GetString(payload, "ownerEntityId"), 0, 128, "ownerEntityId"),
            CharacterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 0, 128, "characterId"),
            CompanionId = RequireLength(PayloadReader.GetString(payload, "companionId"), 0, 128, "companionId"),
            GroupId = RequireLength(PayloadReader.GetString(payload, "groupId"), 0, 128, "groupId"),
            Title = title,
            Description = description,
            ProposalType = proposalType,
            ProposalCategory = category,
            ProposalStatus = ProposalStatusIds.Draft,
            Priority = NormalizePlayerRequestPriority(PayloadReader.GetString(payload, "priority")),
            SourceView = RequireLength(PayloadReader.GetString(payload, "sourceView"), 0, 128, "sourceView"),
            SourceEntityType = RequireLength(PayloadReader.GetString(payload, "sourceEntityType"), 0, 96, "sourceEntityType"),
            SourceEntityId = RequireLength(PayloadReader.GetString(payload, "sourceEntityId"), 0, 160, "sourceEntityId"),
            StructuredPayload = structuredPayload,
            PlayerComment = RequireLength(PayloadReader.GetString(payload, "playerComment"), 0, 2048, "playerComment"),
            VisibilityMode = ProjectVisibilityModeIds.OwnerOnly,
            IsPlayerVisible = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        FillProposalRelatedIds(draft, payload);
        draft.PublicSummary = BuildProposalPublicSummary(draft);
        return draft;
    }

    private void UpdateProposalDraft(PlayerProposalDraftState draft, IDictionary<string, object> payload, string actorId)
    {
        var title = PayloadReader.GetString(payload, "title");
        if (!string.IsNullOrWhiteSpace(title)) draft.Title = RequireLength(title, 2, 180, "title");
        if (payload.ContainsKey("description")) draft.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        if (payload.ContainsKey("playerComment")) draft.PlayerComment = RequireLength(PayloadReader.GetString(payload, "playerComment"), 0, 2048, "playerComment");
        var structured = PayloadReader.GetDictionary(payload, "structuredPayload") ?? PayloadReader.GetDictionary(payload, "payload");
        if (structured != null) draft.StructuredPayload = SanitizeProposalPayload(structured);
        if (payload.ContainsKey("characterId")) draft.CharacterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 0, 128, "characterId");
        if (payload.ContainsKey("companionId")) draft.CompanionId = RequireLength(PayloadReader.GetString(payload, "companionId"), 0, 128, "companionId");
        if (payload.ContainsKey("groupId")) draft.GroupId = RequireLength(PayloadReader.GetString(payload, "groupId"), 0, 128, "groupId");
        if (payload.ContainsKey("priority")) draft.Priority = NormalizePlayerRequestPriority(PayloadReader.GetString(payload, "priority"));
        FillProposalRelatedIds(draft, payload);
        draft.PublicSummary = BuildProposalPublicSummary(draft);
        if (draft.ProposalStatus == ProposalStatusIds.ChangesRequested) draft.ProposalStatus = ProposalStatusIds.Draft;
        TouchProposalDraft(draft, actorId);
    }

    private PlayerProposalValidationResult ValidateProposalDraft(PlayerProposalDraftState draft, string actorUserId, bool playerSafe)
    {
        var result = new PlayerProposalValidationResult
        {
            ValidationId = Guid.NewGuid().ToString("N"),
            ProposalDraftId = draft.Id,
            CampaignId = draft.CampaignId,
            CheckedByUserId = actorUserId,
            CheckedAtUtc = DateTime.UtcNow,
            IsPlayerVisible = true,
            RequiresGMReview = true
        };

        if (string.IsNullOrWhiteSpace(draft.Title)) result.MissingFields.Add("Название");
        if (string.IsNullOrWhiteSpace(draft.ProposalType)) result.MissingFields.Add("Тип предложения");
        foreach (var field in RequiredProposalFields(draft.ProposalType))
        {
            if (!draft.StructuredPayload.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(Convert.ToString(value)))
                result.MissingFields.Add(ProposalFieldLabel(field));
        }

        if ((draft.ProposalType == ProposalTypeIds.Crafting || draft.ProposalType == ProposalTypeIds.FactoryOrder || draft.ProposalType == ProposalTypeIds.FactoryQuote) &&
            draft.StructuredPayload.TryGetValue("quantity", out var quantity) &&
            decimal.TryParse(Convert.ToString(quantity), out var q) && q <= 0)
            result.Errors.Add("Количество должно быть больше нуля.");

        if (string.IsNullOrWhiteSpace(draft.CharacterId) && (draft.ProposalType == ProposalTypeIds.DevelopmentPurchase || draft.ProposalType == ProposalTypeIds.Crafting))
            result.Warnings.Add("Персонаж не выбран. GM сможет уточнить владельца вручную.");

        if (draft.ProposalType == ProposalTypeIds.LegalCheck || draft.ProposalType == ProposalTypeIds.LicenseApplication)
            result.LegalWarnings.Add("Юридическая проверка не является финальным разрешением до решения GM.");

        if (result.Errors.Count > 0)
            result.Status = ProposalValidationStatusIds.Blocked;
        else if (result.MissingFields.Count > 0)
            result.Status = ProposalValidationStatusIds.MissingRequiredFields;
        else if (result.Warnings.Count > 0 || result.LegalWarnings.Count > 0)
            result.Status = ProposalValidationStatusIds.ValidWithWarnings;
        else
            result.Status = ProposalValidationStatusIds.Valid;

        result.CanSubmit = result.Errors.Count == 0 && result.MissingFields.Count == 0;
        result.Summary = result.CanSubmit
            ? (result.Warnings.Count > 0 || result.LegalWarnings.Count > 0 ? "Можно отправить, но GM увидит предупреждения." : "Можно отправить GM.")
            : "Заполните обязательные поля перед отправкой.";
        if (!playerSafe) result.ExtraData["proposalType"] = draft.ProposalType;
        return result;
    }

    private void LinkProposalToPlayerRequest(PlayerProposalDraftState draft, UserAccount actor)
    {
        if (!string.IsNullOrWhiteSpace(draft.LinkedPlayerRequestId)) return;
        if (!PlayerRequestsBaseEnabled()) return;
        var now = DateTime.UtcNow;
        var request = new PlayerRequestState
        {
            RequestNumber = NextPlayerRequestNumber(),
            CampaignId = draft.CampaignId,
            GroupId = draft.GroupId,
            CharacterId = draft.CharacterId,
            CompanionId = draft.CompanionId,
            CreatedByUserId = actor.Id,
            CreatedByDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            RequestType = RequestTypeForProposal(draft.ProposalType),
            Title = draft.Title,
            Description = FirstNonEmpty(draft.Description, draft.PublicSummary),
            Status = PlayerRequestStatusIds.Submitted,
            Priority = draft.Priority,
            VisibilityMode = "party",
            IsPlayerVisible = true,
            LinkedEntityType = "proposal",
            LinkedEntityId = draft.Id,
            ProposalType = draft.ProposalType,
            ProposalPayloadSummary = draft.PublicSummary,
            PublicNotes = draft.PlayerComment,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SubmittedAtUtc = now
        };
        request.ProposalPayload = new PlayerRequestProposalDraft
        {
            ProposalType = draft.ProposalType,
            DisplaySummary = draft.PublicSummary,
            Parameters = new Dictionary<string, object>(draft.StructuredPayload, StringComparer.OrdinalIgnoreCase),
            RequiresGMApproval = true
        };
        _repositories.PlayerRequests.Insert(request);
        draft.LinkedPlayerRequestId = request.Id;
    }

    private void MarkLinkedRequestFulfilled(PlayerProposalDraftState draft, UserAccount actor, string summary)
    {
        if (string.IsNullOrWhiteSpace(draft.LinkedPlayerRequestId)) return;
        var request = _repositories.PlayerRequests.GetById(draft.LinkedPlayerRequestId);
        if (request == null) return;
        request.Status = PlayerRequestStatusIds.Fulfilled;
        request.GMResponse = summary;
        request.ReviewedByUserId = actor.Id;
        request.ReviewedByDisplayName = FirstNonEmpty(actor.Login, actor.Id);
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ResolvedAtUtc = DateTime.UtcNow;
        request.UpdatedAtUtc = DateTime.UtcNow;
        request.UpdatedUtc = request.UpdatedAtUtc;
        request.LinkedEntityType = FirstNonEmpty(draft.LinkedSpecializedEntityType, "proposal");
        request.LinkedEntityId = FirstNonEmpty(draft.LinkedSpecializedEntityId, draft.Id);
        _repositories.PlayerRequests.Replace(request);
    }

    private PlayerProposalDraftState RequireProposal(CommandContext context)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "proposalDraftId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "proposalDraftId");
        var draft = _mongo.PlayerProposalDrafts.Find(Builders<PlayerProposalDraftState>.Filter.Eq(x => x.Id, id)
            | Builders<PlayerProposalDraftState>.Filter.Eq(x => x.ProposalDraftId, id)).FirstOrDefault();
        return draft ?? throw new KeyNotFoundException("Proposal not found.");
    }

    private PlayerProposalDraftState RequireOwnProposal(CommandContext context, UserAccount actor)
    {
        var draft = RequireProposal(context);
        if (draft.CreatedByUserId != actor.Id) throw new UnauthorizedAccessException("Cannot access another player's proposal.");
        return draft;
    }

    private PlayerProposalDraftState RequireOwnEditableProposal(CommandContext context, UserAccount actor)
    {
        var draft = RequireOwnProposal(context, actor);
        if (draft.ProposalStatus != ProposalStatusIds.Draft && draft.ProposalStatus != ProposalStatusIds.ChangesRequested && draft.ProposalStatus != ProposalStatusIds.ReadyToSubmit)
            throw new InvalidOperationException("Proposal is not editable.");
        return draft;
    }

    private PlayerProposalDraftState RequireConvertibleProposal(CommandContext context)
    {
        var draft = RequireProposal(context);
        if (draft.ProposalStatus == ProposalStatusIds.Converted) throw new InvalidOperationException("Proposal is already converted.");
        if (draft.ProposalStatus == ProposalStatusIds.Rejected || draft.ProposalStatus == ProposalStatusIds.Cancelled || draft.ProposalStatus == ProposalStatusIds.Archived)
            throw new InvalidOperationException("Proposal cannot be converted from current state.");
        return draft;
    }

    private void ValidatePlayerProposalOwnership(UserAccount actor, string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) return;
        var character = _repositories.Characters.GetById(characterId);
        if (character == null) throw new KeyNotFoundException("Character not found.");
        if (character.OwnerUserId == actor.Id) return;
        var ownership = _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        if (ownership != null && (ownership.OwnerUserId == actor.Id || ownership.ControlledByUserId == actor.Id)) return;
        throw new UnauthorizedAccessException("Character unavailable for proposal.");
    }

    private Dictionary<string, object> ProposalSinglePayload(PlayerProposalDraftState draft, bool admin, PlayerProposalValidationResult? validation = null, PlayerProposalConversionState? conversion = null)
    {
        validation ??= _mongo.PlayerProposalValidations.Find(Builders<PlayerProposalValidationResult>.Filter.Eq(x => x.ProposalDraftId, draft.Id))
            .SortByDescending(x => x.CheckedAtUtc)
            .FirstOrDefault();
        conversion ??= _mongo.PlayerProposalConversions.Find(Builders<PlayerProposalConversionState>.Filter.Eq(x => x.ProposalDraftId, draft.Id))
            .SortByDescending(x => x.ConvertedAtUtc)
            .FirstOrDefault();
        var reviews = _mongo.PlayerProposalReviews.Find(Builders<PlayerProposalReviewState>.Filter.Eq(x => x.ProposalDraftId, draft.Id))
            .SortByDescending(x => x.UpdatedAtUtc)
            .Limit(10)
            .ToList()
            .Select(x => (object)ProposalReviewPayload(x, admin))
            .ToArray();
        return new Dictionary<string, object>
        {
            { "item", ProposalDraftPayload(draft, admin) },
            { "validation", validation == null ? new Dictionary<string, object>() : ProposalValidationPayload(validation, admin) },
            { "conversion", conversion == null ? new Dictionary<string, object>() : ProposalConversionPayload(conversion, admin) },
            { "reviews", reviews }
        };
    }

    private static Dictionary<string, object> ProposalDraftPayload(PlayerProposalDraftState draft, bool admin)
    {
        var item = new Dictionary<string, object>
        {
            { "id", draft.Id },
            { "proposalDraftId", draft.Id },
            { "campaignId", draft.CampaignId },
            { "title", draft.Title },
            { "name", draft.Title },
            { "description", draft.Description },
            { "proposalType", draft.ProposalType },
            { "proposalTypeLabel", ProposalTypeLabel(draft.ProposalType) },
            { "proposalCategory", draft.ProposalCategory },
            { "proposalStatus", draft.ProposalStatus },
            { "status", draft.ProposalStatus },
            { "statusLabel", ProposalStatusLabel(draft.ProposalStatus) },
            { "priority", draft.Priority },
            { "createdByDisplayName", draft.CreatedByDisplayName },
            { "characterId", draft.CharacterId },
            { "companionId", draft.CompanionId },
            { "groupId", draft.GroupId },
            { "publicSummary", draft.PublicSummary },
            { "playerComment", draft.PlayerComment },
            { "validationSummary", draft.ValidationSummary },
            { "linkedPlayerRequestId", draft.LinkedPlayerRequestId },
            { "linkedProjectId", draft.LinkedProjectId },
            { "linkedSpecializedEntityType", draft.LinkedSpecializedEntityType },
            { "linkedSpecializedEntityId", draft.LinkedSpecializedEntityId },
            { "createdAtUtc", draft.CreatedAtUtc },
            { "updatedAtUtc", draft.UpdatedAtUtc },
            { "submittedAtUtc", draft.SubmittedAtUtc.HasValue ? (object)draft.SubmittedAtUtc.Value : string.Empty },
            { "convertedAtUtc", draft.ConvertedAtUtc.HasValue ? (object)draft.ConvertedAtUtc.Value : string.Empty },
            { "structuredFields", ProposalStructuredFields(draft, admin) },
            { "summary", BuildProposalPublicSummary(draft) }
        };
        if (admin)
        {
            item["createdByUserId"] = draft.CreatedByUserId;
            item["ownerEntityType"] = draft.OwnerEntityType;
            item["ownerEntityId"] = draft.OwnerEntityId;
            item["sourceView"] = draft.SourceView;
            item["sourceEntityType"] = draft.SourceEntityType;
            item["sourceEntityId"] = draft.SourceEntityId;
            item["gmReviewSummary"] = draft.GMReviewSummary;
            item["visibilityMode"] = draft.VisibilityMode;
            item["isPlayerVisible"] = draft.IsPlayerVisible;
            item["tags"] = draft.Tags.Cast<object>().ToArray();
        }
        return item;
    }

    private static object[] ProposalStructuredFields(PlayerProposalDraftState draft, bool admin)
    {
        return draft.StructuredPayload
            .Where(x => admin || !IsUnsafeProposalPayloadKey(x.Key))
            .Take(80)
            .Select(x => (object)new Dictionary<string, object>
            {
                { "key", x.Key },
                { "label", ProposalFieldLabel(x.Key) },
                { "value", SafeDisplayValue(x.Value) }
            })
            .ToArray();
    }

    private static Dictionary<string, object> ProposalValidationPayload(PlayerProposalValidationResult validation, bool admin)
    {
        var item = new Dictionary<string, object>
        {
            { "validationId", validation.ValidationId },
            { "status", validation.Status },
            { "summary", validation.Summary },
            { "errors", validation.Errors.Cast<object>().ToArray() },
            { "warnings", validation.Warnings.Concat(validation.LegalWarnings).Concat(validation.ResourceWarnings).Concat(validation.VisibilityWarnings).Cast<object>().ToArray() },
            { "missingFields", validation.MissingFields.Cast<object>().ToArray() },
            { "canSubmit", validation.CanSubmit },
            { "requiresGMReview", validation.RequiresGMReview },
            { "checkedAtUtc", validation.CheckedAtUtc }
        };
        if (admin) item["missingReferences"] = validation.MissingReferences.Cast<object>().ToArray();
        return item;
    }

    private static Dictionary<string, object> ProposalReviewPayload(PlayerProposalReviewState review, bool admin)
    {
        var item = new Dictionary<string, object>
        {
            { "reviewId", review.ReviewId },
            { "reviewStatus", review.ReviewStatus },
            { "playerVisibleComment", review.PlayerVisibleComment },
            { "requestedChanges", review.RequestedChanges },
            { "decisionReason", review.DecisionReason },
            { "updatedAtUtc", review.UpdatedAtUtc },
            { "reviewedAtUtc", review.ReviewedAtUtc.HasValue ? (object)review.ReviewedAtUtc.Value : string.Empty }
        };
        if (admin)
        {
            item["reviewedByUserId"] = review.ReviewedByUserId;
            item["gmComment"] = review.GMComment;
        }
        return item;
    }

    private static Dictionary<string, object> ProposalConversionPayload(PlayerProposalConversionState conversion, bool admin)
    {
        var item = new Dictionary<string, object>
        {
            { "conversionId", conversion.ConversionId },
            { "conversionType", conversion.ConversionType },
            { "targetEntityType", conversion.TargetEntityType },
            { "targetEntityId", conversion.TargetEntityId },
            { "conversionSummary", conversion.ConversionSummary },
            { "publicSummary", conversion.PublicSummary },
            { "convertedAtUtc", conversion.ConvertedAtUtc },
            { "requiresFollowUp", conversion.RequiresFollowUp },
            { "nextActionKind", conversion.NextActionKind }
        };
        if (admin)
        {
            item["convertedByUserId"] = conversion.ConvertedByUserId;
            item["gmSummary"] = conversion.GMSummary;
        }
        return item;
    }

    private static Dictionary<string, object> ProposalTemplatePayload(ProposalTemplateDefinition template, bool admin)
    {
        var item = new Dictionary<string, object>
        {
            { "id", template.Id },
            { "proposalTemplateId", FirstNonEmpty(template.ProposalTemplateId, template.Id) },
            { "proposalType", template.ProposalType },
            { "proposalTypeLabel", ProposalTypeLabel(template.ProposalType) },
            { "name", template.Name },
            { "description", template.Description },
            { "publicSummary", template.PublicSummary },
            { "requiredFields", template.RequiredFields.Cast<object>().ToArray() },
            { "optionalFields", template.OptionalFields.Cast<object>().ToArray() },
            { "supportedConversionTargets", template.SupportedConversionTargets.Cast<object>().ToArray() },
            { "createsPlayerRequestType", template.CreatesPlayerRequestType },
            { "requiresGMApproval", template.RequiresGMApproval },
            { "isPlayerVisible", template.IsPlayerVisible }
        };
        if (admin)
        {
            item["gmSummary"] = template.GMSummary;
            item["campaignId"] = template.CampaignId;
            item["ruleSetId"] = template.RuleSetId;
            item["visibilityMode"] = template.VisibilityMode;
            item["isArchived"] = template.IsArchived;
        }
        return item;
    }

    private static Dictionary<string, object> Row(string id, string name, string summary)
        => new Dictionary<string, object> { { "id", id }, { "name", name }, { "summary", summary } };

    private ProposalTemplateDefinition BuildTemplate(IDictionary<string, object> payload)
    {
        var template = new ProposalTemplateDefinition
        {
            CampaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 0, 128, "campaignId"),
            RuleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId"), 0, 128, "ruleSetId"),
            ProposalType = NormalizeProposalType(PayloadReader.GetString(payload, "proposalType")),
            Name = RequireLength(PayloadReader.GetString(payload, "name"), 2, 180, "name"),
            Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 2048, "description"),
            PublicSummary = RequireLength(PayloadReader.GetString(payload, "publicSummary"), 0, 1024, "publicSummary"),
            GMSummary = RequireLength(PayloadReader.GetString(payload, "gmSummary"), 0, 2048, "gmSummary"),
            CreatesPlayerRequestType = RequestTypeForProposal(PayloadReader.GetString(payload, "proposalType")),
            RequiresGMApproval = !payload.ContainsKey("requiresGMApproval") || PayloadReader.GetBool(payload, "requiresGMApproval"),
            IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
            VisibilityMode = FirstNonEmpty(PayloadReader.GetString(payload, "visibilityMode"), ProjectVisibilityModeIds.PlayerVisible)
        };
        template.ProposalTemplateId = FirstNonEmpty(PayloadReader.GetString(payload, "proposalTemplateId"), Guid.NewGuid().ToString("N"));
        template.RequiredFields.AddRange(RequiredProposalFields(template.ProposalType));
        template.OptionalFields.AddRange(OptionalProposalFields(template.ProposalType));
        template.SupportedConversionTargets.AddRange(DefaultConversionTargets(template.ProposalType));
        return template;
    }

    private void UpdateTemplate(ProposalTemplateDefinition template, IDictionary<string, object> payload)
    {
        if (payload.ContainsKey("name")) template.Name = RequireLength(PayloadReader.GetString(payload, "name"), 2, 180, "name");
        if (payload.ContainsKey("description")) template.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 2048, "description");
        if (payload.ContainsKey("publicSummary")) template.PublicSummary = RequireLength(PayloadReader.GetString(payload, "publicSummary"), 0, 1024, "publicSummary");
        if (payload.ContainsKey("gmSummary")) template.GMSummary = RequireLength(PayloadReader.GetString(payload, "gmSummary"), 0, 2048, "gmSummary");
        if (payload.ContainsKey("isPlayerVisible")) template.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("visibilityMode")) template.VisibilityMode = FirstNonEmpty(PayloadReader.GetString(payload, "visibilityMode"), template.VisibilityMode);
    }

    private ProposalTemplateDefinition RequireTemplate(CommandContext context)
    {
        var id = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "proposalTemplateId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "proposalTemplateId");
        var template = _mongo.ProposalTemplateDefinitions.Find(Builders<ProposalTemplateDefinition>.Filter.Eq(x => x.Id, id)
            | Builders<ProposalTemplateDefinition>.Filter.Eq(x => x.ProposalTemplateId, id)).FirstOrDefault();
        return template ?? throw new KeyNotFoundException("Proposal template not found.");
    }

    private static IReadOnlyCollection<ProposalTemplateDefinition> BuiltInProposalTemplates()
    {
        ProposalTemplateDefinition Template(string type, string name, string category, string summary)
        {
            var t = new ProposalTemplateDefinition
            {
                Id = $"builtin_{type}",
                ProposalTemplateId = $"builtin_{type}",
                ProposalType = type,
                Name = name,
                Description = summary,
                PublicSummary = summary,
                CreatesPlayerRequestType = RequestTypeForProposal(type),
                RequiresGMApproval = true,
                IsPlayerVisible = true,
                VisibilityMode = ProjectVisibilityModeIds.PlayerVisible
            };
            t.RequiredFields.AddRange(RequiredProposalFields(type));
            t.OptionalFields.AddRange(OptionalProposalFields(type));
            t.SupportedConversionTargets.AddRange(DefaultConversionTargets(type));
            t.Tags.Add(category);
            return t;
        }

        return new[]
        {
            Template(ProposalTypeIds.Research, "Предложить исследование", ProposalCategoryIds.Knowledge, "Тема, вопрос и ожидаемый результат исследования."),
            Template(ProposalTypeIds.Crafting, "Предложить крафт", ProposalCategoryIds.Item, "Рецепт или желаемый предмет, материалы и назначение."),
            Template(ProposalTypeIds.EngineeringDesign, "Предложить инженерный проект", ProposalCategoryIds.Vehicle, "Платформа, роль, модули и желаемые возможности."),
            Template(ProposalTypeIds.FactoryQuote, "Запросить расчёт производства", ProposalCategoryIds.Production, "Blueprint/preset, количество, качество и желаемые сроки."),
            Template(ProposalTypeIds.FactoryOrder, "Запросить заказ производства", ProposalCategoryIds.Production, "Заказ по blueprint/preset или ручному описанию."),
            Template(ProposalTypeIds.Manufacturing, "Запросить manufacturing действие", ProposalCategoryIds.Production, "Приёмка, статус, ресурсы или передача asset."),
            Template(ProposalTypeIds.LicenseApplication, "Запросить лицензию", ProposalCategoryIds.Law, "Юрисдикция, лицензия и причина заявки."),
            Template(ProposalTypeIds.LegalCheck, "Запросить юридическую проверку", ProposalCategoryIds.Law, "Действие, объект и юрисдикция для проверки."),
            Template(ProposalTypeIds.DevelopmentPurchase, "Запросить покупку развития", ProposalCategoryIds.CharacterDevelopment, "Персонаж, узел развития и комментарий игрока."),
            Template(ProposalTypeIds.CustomProject, "Custom proposal", ProposalCategoryIds.Custom, "Свободное структурированное предложение GM.")
        };
    }

    private void UpsertProposalReview(PlayerProposalDraftState draft, string reviewStatus, string reviewedByUserId, string playerComment, string gmComment, string requestedChanges, string decisionReason = "")
    {
        var review = _mongo.PlayerProposalReviews.Find(Builders<PlayerProposalReviewState>.Filter.Eq(x => x.ProposalDraftId, draft.Id)
            & Builders<PlayerProposalReviewState>.Filter.Eq(x => x.ReviewStatus, reviewStatus)).FirstOrDefault();
        review ??= new PlayerProposalReviewState
        {
            ReviewId = Guid.NewGuid().ToString("N"),
            CampaignId = draft.CampaignId,
            ProposalDraftId = draft.Id,
            LinkedPlayerRequestId = draft.LinkedPlayerRequestId,
            CreatedAtUtc = DateTime.UtcNow
        };
        review.ReviewStatus = reviewStatus;
        review.ReviewedByUserId = reviewedByUserId;
        review.PlayerVisibleComment = playerComment;
        review.GMComment = gmComment;
        review.RequestedChanges = requestedChanges;
        review.DecisionReason = decisionReason;
        review.UpdatedAtUtc = DateTime.UtcNow;
        review.ReviewedAtUtc = string.IsNullOrWhiteSpace(reviewedByUserId) ? review.ReviewedAtUtc : DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(review.Id) || _mongo.PlayerProposalReviews.Find(Builders<PlayerProposalReviewState>.Filter.Eq(x => x.Id, review.Id)).FirstOrDefault() == null)
            _mongo.PlayerProposalReviews.InsertOne(review);
        else
            _mongo.PlayerProposalReviews.ReplaceOne(Builders<PlayerProposalReviewState>.Filter.Eq(x => x.Id, review.Id), review);
    }

    private void TryWriteProposalJournal(PlayerProposalDraftState draft, string eventType, string summary, string actorId)
    {
        if (!ProposalJournalEnabled()) return;
        if (!_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp)) ||
            !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion))) return;
        _repositories.EventJournalEntries.Insert(new EventJournalEntryState
        {
            CampaignId = draft.CampaignId,
            CharacterId = draft.CharacterId,
            SourceModule = "proposals",
            SourceEventType = eventType,
            SourceEventId = draft.Id,
            CorrelationId = FirstNonEmpty(draft.LinkedPlayerRequestId, draft.Id),
            EntryType = EventJournalEntryTypeIds.System,
            Category = EventJournalCategoryIds.Custom,
            Title = draft.Title,
            Summary = summary,
            PlayerSummary = summary,
            VisibilityMode = EventJournalVisibilityModeIds.PlayerVisible,
            IsPlayerVisible = true,
            IsAutomatic = true,
            ActorUserId = actorId,
            CreatedByUserId = actorId,
            SubjectEntityType = "proposal",
            SubjectEntityId = draft.Id,
            SubjectDisplayName = draft.Title,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    private void TryPublishProposalSync(PlayerProposalDraftState draft, string operation, string actorId, string requestId)
    {
        if (!ProposalSyncEnabled()) return;
        _syncEvents.PublishCampaign(draft.CampaignId, "proposal.changed", "proposal", draft.Id, operation, actorId,
            new Dictionary<string, object> { { "proposalDraftId", draft.Id }, { "status", draft.ProposalStatus }, { "type", draft.ProposalType } },
            requestId);
    }

    private static void FillProposalRelatedIds(PlayerProposalDraftState draft, IDictionary<string, object> payload)
    {
        draft.RelatedKnowledgeIds = GetProposalStringList(payload, "relatedKnowledgeIds");
        draft.RelatedRecipeIds = GetProposalStringList(payload, "relatedRecipeIds");
        draft.RelatedBlueprintIds = GetProposalStringList(payload, "relatedBlueprintIds");
        draft.RelatedAssetIds = GetProposalStringList(payload, "relatedAssetIds");
        draft.RelatedProjectIds = GetProposalStringList(payload, "relatedProjectIds");
        draft.RelatedInventoryItemIds = GetProposalStringList(payload, "relatedInventoryItemIds");
        draft.RelatedLicenseIds = GetProposalStringList(payload, "relatedLicenseIds");
        draft.RelatedFacilityIds = GetProposalStringList(payload, "relatedFacilityIds");
    }

    private static List<string> GetProposalStringList(IDictionary<string, object> payload, string key)
    {
        var raw = PayloadReader.GetList(payload, key);
        if (raw == null) return new List<string>();
        return raw.Select(x => Convert.ToString(x) ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).Take(64).ToList();
    }

    private static Dictionary<string, object> SanitizeProposalPayload(IDictionary<string, object> source)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source.Take(96))
        {
            if (IsUnsafeProposalPayloadKey(pair.Key)) continue;
            result[pair.Key] = SanitizeProposalPayloadValue(pair.Value);
        }
        return result;
    }

    private static object SanitizeProposalPayloadValue(object value)
    {
        if (value == null) return string.Empty;
        if (value is string s) return s.Length > 2000 ? s.Substring(0, 2000) : s;
        if (value is int || value is long || value is double || value is float || value is decimal || value is bool || value is DateTime) return value;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var count = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (++count > 48) break;
                var key = Convert.ToString(entry.Key) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key) || IsUnsafeProposalPayloadKey(key)) continue;
                result[key] = SanitizeProposalPayloadValue(entry.Value!);
            }
            return result;
        }
        if (value is IEnumerable enumerable && value is not string)
        {
            var list = new List<object>();
            foreach (var item in enumerable)
            {
                if (list.Count >= 64) break;
                list.Add(SanitizeProposalPayloadValue(item!));
            }
            return list;
        }
        return Convert.ToString(value) ?? string.Empty;
    }

    private static bool IsUnsafeProposalPayloadKey(string key)
    {
        return key.IndexOf("serverOnly", StringComparison.OrdinalIgnoreCase) >= 0
            || key.IndexOf("gmNotes", StringComparison.OrdinalIgnoreCase) >= 0
            || key.IndexOf("gmOnly", StringComparison.OrdinalIgnoreCase) >= 0
            || key.IndexOf("hidden", StringComparison.OrdinalIgnoreCase) >= 0
            || key.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
            || key.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BuildProposalPublicSummary(PlayerProposalDraftState draft)
    {
        var payloadPreview = draft.StructuredPayload
            .Where(x => !IsUnsafeProposalPayloadKey(x.Key))
            .Take(5)
            .Select(x => $"{ProposalFieldLabel(x.Key)}: {SafeDisplayValue(x.Value)}");
        return FirstNonEmpty(draft.Description, string.Join("; ", payloadPreview), draft.Title);
    }

    private static string ProposalPayloadString(PlayerProposalDraftState draft, string key)
        => draft.StructuredPayload.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private static string SafeDisplayValue(object value)
    {
        if (value == null) return "—";
        if (value is string s) return string.IsNullOrWhiteSpace(s) ? "—" : (s.Length > 180 ? s.Substring(0, 180) + "..." : s);
        if (value is IEnumerable enumerable && value is not string)
        {
            var parts = new List<string>();
            foreach (var item in enumerable)
            {
                if (parts.Count >= 6) break;
                parts.Add(Convert.ToString(item) ?? string.Empty);
            }
            return parts.Count == 0 ? "—" : string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
        return Convert.ToString(value) ?? "—";
    }

    private static string[] RequiredProposalFields(string proposalType)
    {
        return NormalizeProposalType(proposalType) switch
        {
            ProposalTypeIds.Research => new[] { "researchTopic", "researchQuestion" },
            ProposalTypeIds.Crafting => new[] { "desiredResultTitle" },
            ProposalTypeIds.EngineeringDesign => new[] { "intendedRoleSummary" },
            ProposalTypeIds.FactoryQuote => new[] { "quantity" },
            ProposalTypeIds.FactoryOrder => new[] { "quantity" },
            ProposalTypeIds.Manufacturing => new[] { "requestKind" },
            ProposalTypeIds.LegalCheck => new[] { "actionType", "objectEntityType" },
            ProposalTypeIds.LicenseApplication => new[] { "licenseDefinitionId", "applicationReason" },
            ProposalTypeIds.DevelopmentPurchase => new[] { "characterId", "developmentNodeId" },
            _ => new[] { "summary" }
        };
    }

    private static string[] OptionalProposalFields(string proposalType)
    {
        return NormalizeProposalType(proposalType) switch
        {
            ProposalTypeIds.Research => new[] { "desiredResultType", "suggestedSources", "linkedKnowledgeIds", "suggestedApproach" },
            ProposalTypeIds.Crafting => new[] { "recipeId", "quantity", "targetQuality", "suggestedMaterials", "intendedUse" },
            ProposalTypeIds.EngineeringDesign => new[] { "platformId", "sizeClassId", "selectedModuleIds", "desiredCapabilities", "powerProfileNotes", "targetQuality" },
            ProposalTypeIds.FactoryQuote or ProposalTypeIds.FactoryOrder => new[] { "sourceBlueprintId", "sourcePresetDesignId", "desiredQuality", "preferredFacilityId", "acceptableCostRange", "deliveryTargetSummary" },
            ProposalTypeIds.Manufacturing => new[] { "factoryOrderId", "manufacturingProjectId", "resourceProposal", "acceptanceRequest", "assetTransferTarget" },
            ProposalTypeIds.LegalCheck => new[] { "jurisdictionId", "objectEntityId", "objectCategory" },
            ProposalTypeIds.LicenseApplication => new[] { "jurisdictionId", "applicantEntityType", "applicantEntityId" },
            ProposalTypeIds.DevelopmentPurchase => new[] { "playerComment" },
            _ => new[] { "details", "targetEntityType", "targetEntityId" }
        };
    }

    private static string[] DefaultConversionTargets(string proposalType)
    {
        return NormalizeProposalType(proposalType) switch
        {
            ProposalTypeIds.Research => new[] { ProposalConversionTypeIds.CreateResearchProject, ProposalConversionTypeIds.CreateGenericProject },
            ProposalTypeIds.Crafting => new[] { ProposalConversionTypeIds.CreateCraftingProject, ProposalConversionTypeIds.CreateGenericProject },
            ProposalTypeIds.EngineeringDesign => new[] { ProposalConversionTypeIds.CreateEngineeringProject, ProposalConversionTypeIds.CreateGenericProject },
            ProposalTypeIds.FactoryQuote => new[] { ProposalConversionTypeIds.CreateFactoryQuote, ProposalConversionTypeIds.CreateGenericProject },
            ProposalTypeIds.FactoryOrder => new[] { ProposalConversionTypeIds.CreateFactoryOrder, ProposalConversionTypeIds.CreateGenericProject },
            ProposalTypeIds.Manufacturing => new[] { ProposalConversionTypeIds.CreateManufacturingProject, ProposalConversionTypeIds.CreateGenericProject },
            ProposalTypeIds.LegalCheck => new[] { ProposalConversionTypeIds.CreateLegalCheck, ProposalConversionTypeIds.CreateGenericProject },
            ProposalTypeIds.LicenseApplication => new[] { ProposalConversionTypeIds.CreateLicenseApplication, ProposalConversionTypeIds.CreateGenericProject },
            ProposalTypeIds.DevelopmentPurchase => new[] { ProposalConversionTypeIds.CreateDevelopmentPurchaseRequest, ProposalConversionTypeIds.CreateGenericProject },
            _ => new[] { ProposalConversionTypeIds.CreateGenericProject, ProposalConversionTypeIds.LinkExistingEntity }
        };
    }

    private static string NormalizeProposalType(string? value)
    {
        var type = (value ?? string.Empty).Trim().ToLowerInvariant();
        return type switch
        {
            ProposalTypeIds.Research or ProposalTypeIds.Crafting or ProposalTypeIds.EngineeringDesign or ProposalTypeIds.FactoryQuote
                or ProposalTypeIds.FactoryOrder or ProposalTypeIds.Manufacturing or ProposalTypeIds.LegalCheck or ProposalTypeIds.LicenseApplication
                or ProposalTypeIds.DevelopmentPurchase or ProposalTypeIds.InventoryAction or ProposalTypeIds.AssetTransfer
                or ProposalTypeIds.CustomProject or ProposalTypeIds.GenericGmRequest or ProposalTypeIds.Custom => type,
            _ => ProposalTypeIds.GenericGmRequest
        };
    }

    private static string NormalizeProposalCategory(string? value)
    {
        var category = (value ?? string.Empty).Trim().ToLowerInvariant();
        return category switch
        {
            ProposalCategoryIds.Knowledge or ProposalCategoryIds.Item or ProposalCategoryIds.Technology or ProposalCategoryIds.Vehicle
                or ProposalCategoryIds.Production or ProposalCategoryIds.Law or ProposalCategoryIds.CharacterDevelopment
                or ProposalCategoryIds.Inventory or ProposalCategoryIds.Asset or ProposalCategoryIds.WorldAction or ProposalCategoryIds.Custom => category,
            _ => ProposalCategoryIds.Custom
        };
    }

    private static string NormalizeProposalStatus(string? value, bool allowEmpty)
    {
        var status = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (allowEmpty && string.IsNullOrWhiteSpace(status)) return string.Empty;
        return status switch
        {
            ProposalStatusIds.Draft or ProposalStatusIds.ReadyToSubmit or ProposalStatusIds.Submitted or ProposalStatusIds.LinkedToRequest
                or ProposalStatusIds.InGmReview or ProposalStatusIds.ChangesRequested or ProposalStatusIds.Approved
                or ProposalStatusIds.Rejected or ProposalStatusIds.Converted or ProposalStatusIds.Cancelled or ProposalStatusIds.Archived => status,
            _ => ProposalStatusIds.Draft
        };
    }

    private static string DefaultProposalCategory(string proposalType)
    {
        return NormalizeProposalType(proposalType) switch
        {
            ProposalTypeIds.Research => ProposalCategoryIds.Knowledge,
            ProposalTypeIds.Crafting => ProposalCategoryIds.Item,
            ProposalTypeIds.EngineeringDesign => ProposalCategoryIds.Vehicle,
            ProposalTypeIds.FactoryQuote or ProposalTypeIds.FactoryOrder or ProposalTypeIds.Manufacturing => ProposalCategoryIds.Production,
            ProposalTypeIds.LegalCheck or ProposalTypeIds.LicenseApplication => ProposalCategoryIds.Law,
            ProposalTypeIds.DevelopmentPurchase => ProposalCategoryIds.CharacterDevelopment,
            ProposalTypeIds.InventoryAction => ProposalCategoryIds.Inventory,
            ProposalTypeIds.AssetTransfer => ProposalCategoryIds.Asset,
            _ => ProposalCategoryIds.Custom
        };
    }

    private static string DefaultProposalTitle(string proposalType)
    {
        return NormalizeProposalType(proposalType) switch
        {
            ProposalTypeIds.Research => "Предложение исследования",
            ProposalTypeIds.Crafting => "Предложение крафта",
            ProposalTypeIds.EngineeringDesign => "Инженерное предложение",
            ProposalTypeIds.FactoryQuote => "Запрос расчёта производства",
            ProposalTypeIds.FactoryOrder => "Запрос заказа производства",
            ProposalTypeIds.Manufacturing => "Запрос manufacturing",
            ProposalTypeIds.LegalCheck => "Запрос юридической проверки",
            ProposalTypeIds.LicenseApplication => "Заявка на лицензию",
            ProposalTypeIds.DevelopmentPurchase => "Запрос покупки развития",
            _ => "Предложение GM"
        };
    }

    private static string RequestTypeForProposal(string? proposalType)
    {
        return NormalizeProposalType(proposalType) switch
        {
            ProposalTypeIds.Research => PlayerRequestTypeIds.Research,
            ProposalTypeIds.Crafting => PlayerRequestTypeIds.Crafting,
            ProposalTypeIds.EngineeringDesign => PlayerRequestTypeIds.EngineeringDesign,
            ProposalTypeIds.FactoryQuote => PlayerRequestTypeIds.FactoryQuote,
            ProposalTypeIds.FactoryOrder => PlayerRequestTypeIds.FactoryOrder,
            ProposalTypeIds.DevelopmentPurchase => PlayerRequestTypeIds.Purchase,
            ProposalTypeIds.LegalCheck or ProposalTypeIds.LicenseApplication => PlayerRequestTypeIds.InformationRequest,
            ProposalTypeIds.Manufacturing => PlayerRequestTypeIds.Action,
            _ => PlayerRequestTypeIds.General
        };
    }

    private static string ProposalTypeLabel(string proposalType)
    {
        return NormalizeProposalType(proposalType) switch
        {
            ProposalTypeIds.Research => "Исследование",
            ProposalTypeIds.Crafting => "Крафт",
            ProposalTypeIds.EngineeringDesign => "Инженерный проект",
            ProposalTypeIds.FactoryQuote => "Расчёт производства",
            ProposalTypeIds.FactoryOrder => "Заказ производства",
            ProposalTypeIds.Manufacturing => "Manufacturing",
            ProposalTypeIds.LegalCheck => "Юридическая проверка",
            ProposalTypeIds.LicenseApplication => "Лицензия",
            ProposalTypeIds.DevelopmentPurchase => "Развитие",
            ProposalTypeIds.InventoryAction => "Инвентарь",
            ProposalTypeIds.AssetTransfer => "Передача asset",
            ProposalTypeIds.CustomProject => "Особый проект",
            ProposalTypeIds.GenericGmRequest => "Заявка GM",
            _ => "Другое"
        };
    }

    private static string ProposalStatusLabel(string status)
    {
        return NormalizeProposalStatus(status, allowEmpty: false) switch
        {
            ProposalStatusIds.Draft => "Черновик",
            ProposalStatusIds.ReadyToSubmit => "Готово к отправке",
            ProposalStatusIds.Submitted => "Отправлено",
            ProposalStatusIds.LinkedToRequest => "Связано с заявкой",
            ProposalStatusIds.InGmReview => "На рассмотрении",
            ProposalStatusIds.ChangesRequested => "Нужны правки",
            ProposalStatusIds.Approved => "Одобрено",
            ProposalStatusIds.Rejected => "Отклонено",
            ProposalStatusIds.Converted => "Конвертировано",
            ProposalStatusIds.Cancelled => "Отменено",
            ProposalStatusIds.Archived => "Архив",
            _ => status
        };
    }

    private static string ProposalFieldLabel(string key)
    {
        return key switch
        {
            "researchTopic" => "Тема исследования",
            "researchQuestion" => "Исследовательский вопрос",
            "desiredResultType" => "Ожидаемый результат",
            "suggestedSources" => "Предлагаемые источники",
            "suggestedApproach" => "Подход",
            "recipeId" => "Рецепт",
            "desiredResultTitle" => "Желаемый результат",
            "desiredResultDescription" => "Описание результата",
            "quantity" => "Количество",
            "targetQuality" => "Качество",
            "suggestedMaterials" => "Материалы",
            "intendedUse" => "Назначение",
            "platformId" => "Платформа",
            "intendedRoleSummary" => "Роль",
            "selectedModuleIds" => "Модули",
            "desiredCapabilities" => "Возможности",
            "sourceBlueprintId" => "Blueprint",
            "sourcePresetDesignId" => "Preset",
            "preferredFacilityId" => "Предпочтительная площадка",
            "acceptableCostRange" => "Диапазон стоимости",
            "deliveryTargetSummary" => "Доставка",
            "requestKind" => "Тип запроса",
            "factoryOrderId" => "Factory Order",
            "manufacturingProjectId" => "Manufacturing Project",
            "jurisdictionId" => "Юрисдикция",
            "actionType" => "Действие",
            "objectEntityType" => "Тип объекта",
            "objectEntityId" => "Объект",
            "objectCategory" => "Категория объекта",
            "licenseDefinitionId" => "Лицензия",
            "applicationReason" => "Причина заявки",
            "characterId" => "Персонаж",
            "developmentNodeId" => "Узел развития",
            "summary" => "Краткое описание",
            "details" => "Детали",
            _ => key
        };
    }

    private static string ProposalCampaignId(CommandContext context) => ProposalCampaignId(context.Request.Payload);

    private static string ProposalCampaignId(IDictionary<string, object> payload)
        => FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), "default");

    private static void TouchProposalDraft(PlayerProposalDraftState draft, string actorId)
    {
        draft.UpdatedAtUtc = DateTime.UtcNow;
        draft.UpdatedUtc = draft.UpdatedAtUtc;
        draft.ExtraData["lastTouchedByUserId"] = actorId;
    }

    private bool ProposalPlayerCenterEnabled() => _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UsePlayerProposalCenter));
    private bool ProposalEditorsEnabled() => ProposalPlayerCenterEnabled() && _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseStructuredProposalDrafts)) && _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseProposalEditors));
    private bool ProposalValidationEnabled() => ProposalPlayerCenterEnabled() && _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseProposalValidation));
    private bool ProposalPreviewEnabled() => ProposalPlayerCenterEnabled() && _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseProposalPreview));
    private bool ProposalSubmitEnabled() => ProposalPlayerCenterEnabled() && _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseProposalSubmitFlow));
    private bool ProposalAdminReviewEnabled() => ProposalPlayerCenterEnabled() && _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseProposalReviewWorkspace));
    private bool ProposalConversionEnabled() => ProposalAdminReviewEnabled() && _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseProposalConversionFlow));
    private bool ProposalRequestIntegrationEnabled() => _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseProposalRequestIntegration));
    private bool ProposalProjectIntegrationEnabled() => _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseProposalProjectIntegration)) && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectFoundationMvp)) && _featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectBaseV1));
    private bool ProposalJournalEnabled() => _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseProposalJournalIntegration));
    private bool ProposalSyncEnabled() => _featureFlags.IsEnabled(nameof(ProposalFeatureFlags.UseProposalSyncEvents));

    private void EnsureProposalEditorEnabled(string proposalType)
    {
        var flag = NormalizeProposalType(proposalType) switch
        {
            ProposalTypeIds.Research => nameof(ProposalFeatureFlags.UseResearchProposalEditor),
            ProposalTypeIds.Crafting => nameof(ProposalFeatureFlags.UseCraftingProposalEditor),
            ProposalTypeIds.EngineeringDesign => nameof(ProposalFeatureFlags.UseEngineeringProposalEditor),
            ProposalTypeIds.FactoryQuote or ProposalTypeIds.FactoryOrder => nameof(ProposalFeatureFlags.UseFactoryOrderProposalEditor),
            ProposalTypeIds.Manufacturing => nameof(ProposalFeatureFlags.UseManufacturingProposalEditor),
            ProposalTypeIds.LegalCheck or ProposalTypeIds.LicenseApplication => nameof(ProposalFeatureFlags.UseLegalProposalEditor),
            ProposalTypeIds.DevelopmentPurchase => nameof(ProposalFeatureFlags.UseDevelopmentProposalEditor),
            _ => nameof(ProposalFeatureFlags.UseCustomProposalEditor)
        };
        if (!_featureFlags.IsEnabled(flag)) throw new InvalidOperationException("Этот редактор предложений выключен feature flags.");
    }

    private ResponseEnvelope ProposalDisabled(CommandContext context)
    {
        _logger.Admin($"proposal.disabled command={context.Request.Command}");
        return Error("Proposal Center выключен feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }
}
