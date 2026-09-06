using System;
using System.Collections;
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
    private const string PlayerRequestNumberCounterKey = "player-request:number";
    private readonly object _playerRequestNumberLock = new object();
    private bool _playerRequestNumbersInitialized;

    public ResponseEnvelope PlayerRequestCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!PlayerRequestPlayerEnabled()) return PlayerRequestsDisabled(context);
        if (actor.Roles.Contains(UserRole.Observer)) throw new UnauthorizedAccessException("Observer cannot create player requests.");

        var now = DateTime.UtcNow;
        var campaignId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "campaignId"), "default"), 1, 128, "campaignId");
        var requestType = NormalizePlayerRequestType(PayloadReader.GetString(context.Request.Payload, "requestType"));
        var title = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "title"), DefaultPlayerRequestTitle(requestType)), 2, 160, "title");
        var details = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "details"), PayloadReader.GetString(context.Request.Payload, "description")), 0, 4096, "details");
        var reason = RequireLength(PayloadReader.GetString(context.Request.Payload, "reason"), 0, 2048, "reason");
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 0, 128, "characterId");
        var characterName = string.Empty;
        if (!string.IsNullOrWhiteSpace(characterId) && PlayerRequestCharacterLinkEnabled())
            characterName = EnsurePlayerRequestCharacterAccess(actor, characterId);

        var submit = PayloadReader.GetBool(context.Request.Payload, "submit");
        var request = new PlayerRequestState
        {
            RequestNumber = NextPlayerRequestNumber(),
            CampaignId = campaignId,
            SessionId = PlayerRequestSessionLinkEnabled() ? RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 0, 128, "sessionId") : string.Empty,
            GroupId = PlayerRequestSessionLinkEnabled() ? RequireLength(PayloadReader.GetString(context.Request.Payload, "groupId"), 0, 128, "groupId") : string.Empty,
            CharacterId = characterId,
            CharacterName = characterName,
            CompanionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "companionId"), 0, 128, "companionId"),
            OwnerUserId = actor.Id,
            CreatedByUserId = actor.Id,
            CreatedByDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            RequestType = requestType,
            Category = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "category"), requestType), 0, 64, "category"),
            Title = title,
            Description = details,
            Details = details,
            Reason = reason,
            Status = submit ? PlayerRequestStatusIds.Submitted : PlayerRequestStatusIds.Draft,
            Priority = NormalizePlayerRequestPriority(PayloadReader.GetString(context.Request.Payload, "priority")),
            VisibilityMode = NormalizePlayerRequestVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode")),
            IsPlayerVisible = true,
            LinkedEntityType = RequireLength(PayloadReader.GetString(context.Request.Payload, "linkedEntityType"), 0, 64, "linkedEntityType"),
            LinkedEntityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "linkedEntityId"), 0, 128, "linkedEntityId"),
            ProposalType = RequireLength(PayloadReader.GetString(context.Request.Payload, "proposalType"), 0, 64, "proposalType"),
            ProposalPayloadSummary = RequireLength(PayloadReader.GetString(context.Request.Payload, "proposalPayloadSummary"), 0, 1024, "proposalPayloadSummary"),
            PlayerVisibleText = details,
            PublicNotes = RequireLength(PayloadReader.GetString(context.Request.Payload, "publicNotes"), 0, 2048, "publicNotes"),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SubmittedAtUtc = submit ? now : null,
            Revision = 1
        };
        request.ProposalPayload = BuildPlayerRequestProposalDraft(context.Request.Payload, request.ProposalType, request.ProposalPayloadSummary);
        AddPlayerRequestAudit(request, actor, submit ? "request.created.submitted" : "request.created.draft", string.Empty, request.Status, "Заявка создана игроком.", true);

        _repositories.PlayerRequests.Insert(request);
        AddPlayerRequestSystemComment(request, actor, submit ? "Заявка отправлена GM." : "Черновик заявки создан.", true);
        WriteAudit("player_request", actor.Id, submit ? "submit" : "create", request.Id);
        TryAppendPlayerRequestJournalEntry(
            request,
            actor,
            submit ? "request_created_submitted" : "request_created_draft",
            $"Заявка #{request.RequestNumber}: {request.Title}",
            submit ? "Игрок отправил заявку GM." : "Игрок создал черновик заявки.",
            string.Empty,
            EventJournalSeverityIds.Notice);
        _logger.Admin($"player.request.create.done actor={actor.Login} requestId={request.Id} type={request.RequestType} status={request.Status}");
        return Ok("Player request created.", new Dictionary<string, object> { { "item", PlayerRequestPayload(request, actor, includeAdminFields: false) } });
    }
    public ResponseEnvelope PlayerRequestSubmit(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!PlayerRequestPlayerEnabled()) return PlayerRequestsDisabled(context);
        var request = RequireOwnPlayerRequest(context, actor);
        if (request.Status != PlayerRequestStatusIds.Draft) throw new InvalidOperationException("Only draft requests can be submitted.");
        var from = request.Status;
        request.Status = PlayerRequestStatusIds.Submitted;
        request.SubmittedAtUtc = DateTime.UtcNow;
        TouchPlayerRequest(request, actor.Id);
        AddPlayerRequestAudit(request, actor, "request.submitted", from, request.Status, "Заявка отправлена GM.", true);
        _repositories.PlayerRequests.Replace(request);
        AddPlayerRequestSystemComment(request, actor, "Заявка отправлена GM.", true);
        WriteAudit("player_request", actor.Id, "submit", request.Id);
        TryAppendPlayerRequestJournalEntry(
            request,
            actor,
            "request_submitted",
            $"Заявка #{request.RequestNumber}: {request.Title}",
            "Игрок отправил заявку GM.",
            string.Empty,
            EventJournalSeverityIds.Notice);
        return Ok("Player request submitted.", new Dictionary<string, object> { { "item", PlayerRequestPayload(request, actor, includeAdminFields: false) } });
    }

    public ResponseEnvelope PlayerRequestResubmit(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!PlayerRequestPlayerEnabled()) return PlayerRequestsDisabled(context);
        var request = RequireOwnPlayerRequest(context, actor);
        if (request.Status != PlayerRequestStatusIds.ChangesRequested)
            throw new InvalidOperationException("Only requests with requested changes can be resubmitted.");
        if (request.ResubmissionCount >= 1)
            throw new InvalidOperationException("Resubmission limit reached.");

        var oldDetails = FirstNonEmpty(request.Details, request.Description);
        var title = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "title"), request.Title);
        var details = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "details"), PayloadReader.GetString(context.Request.Payload, "description"), oldDetails);
        var reason = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "reason"), request.Reason);
        var from = request.Status;
        request.Title = RequireLength(title, 2, 160, "title");
        request.Description = RequireLength(details, 0, 4096, "details");
        request.Details = request.Description;
        request.Reason = RequireLength(reason, 0, 2048, "reason");
        request.PlayerVisibleText = request.Description;
        request.Status = PlayerRequestStatusIds.Submitted;
        request.SubmittedAtUtc = DateTime.UtcNow;
        request.ResubmittedFromRequestId = string.IsNullOrWhiteSpace(request.ResubmittedFromRequestId) ? request.Id : request.ResubmittedFromRequestId;
        request.ResubmissionCount++;
        request.Decision = string.Empty;
        request.DecisionCommentPlayerVisible = string.Empty;
        request.DecisionCommentGMOnly = string.Empty;
        request.GMResponse = string.Empty;
        request.ResolutionReason = string.Empty;
        TouchPlayerRequest(request, actor.Id);
        AddPlayerRequestAudit(request, actor, "request.resubmitted", from, request.Status, $"Заявка отправлена повторно. Было: {TrimForAudit(oldDetails)}", true);
        _repositories.PlayerRequests.Replace(request);
        AddPlayerRequestSystemComment(request, actor, "Заявка отправлена повторно после уточнений.", true);
        WriteAudit("player_request", actor.Id, "resubmit", request.Id);
        TryAppendPlayerRequestJournalEntry(
            request,
            actor,
            "request_resubmitted",
            $"Заявка #{request.RequestNumber}: {request.Title}",
            "Игрок повторно отправил заявку после уточнений.",
            string.Empty,
            EventJournalSeverityIds.Notice);
        return Ok("Player request resubmitted.", new Dictionary<string, object> { { "item", PlayerRequestPayload(request, actor, includeAdminFields: false) } });
    }
    public ResponseEnvelope PlayerRequestListMine(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!PlayerRequestPlayerEnabled()) return PlayerRequestsDisabled(context);
        var items = _repositories.PlayerRequests.Find(Builders<PlayerRequestState>.Filter.Eq(x => x.CreatedByUserId, actor.Id))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(200)
            .Select(x => (object)PlayerRequestPayload(x, actor, includeAdminFields: false))
            .ToArray();
        return Ok("Player requests loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope PlayerRequestGetMine(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!PlayerRequestPlayerEnabled()) return PlayerRequestsDisabled(context);
        var request = RequireOwnPlayerRequest(context, actor);
        return Ok("Player request loaded.", new Dictionary<string, object> { { "item", PlayerRequestPayload(request, actor, includeAdminFields: false) } });
    }

    public ResponseEnvelope PlayerRequestComment(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!PlayerRequestPlayerEnabled()) return PlayerRequestsDisabled(context);
        if (!PlayerRequestCommentsEnabled()) return Error("Player request comments are disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var request = RequireOwnPlayerRequest(context, actor);
        var text = RequireLength(PayloadReader.GetString(context.Request.Payload, "text"), 1, 2000, "text");
        AddPlayerRequestComment(request, actor, PlayerRequestCommentAuthorRoleIds.Player, text, true, string.Empty);
        TouchPlayerRequest(request, actor.Id);
        _repositories.PlayerRequests.Replace(request);
        WriteAudit("player_request", actor.Id, "comment", request.Id);
        return Ok("Player request comment added.", new Dictionary<string, object> { { "item", PlayerRequestPayload(request, actor, includeAdminFields: false) } });
    }

    public ResponseEnvelope PlayerRequestCancel(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!PlayerRequestPlayerEnabled()) return PlayerRequestsDisabled(context);
        var request = RequireOwnPlayerRequest(context, actor);
        if (IsPlayerRequestTerminal(request.Status) || request.Status == PlayerRequestStatusIds.Approved)
            throw new InvalidOperationException("This request can no longer be cancelled.");
        var from = request.Status;
        request.Status = PlayerRequestStatusIds.Cancelled;
        request.CancelledAtUtc = DateTime.UtcNow;
        request.CancelledByUserId = actor.Id;
        request.CancelledByDisplayName = FirstNonEmpty(actor.Login, actor.Id);
        TouchPlayerRequest(request, actor.Id);
        AddPlayerRequestAudit(request, actor, "request.cancelled", from, request.Status, "Игрок отменил заявку.", true);
        _repositories.PlayerRequests.Replace(request);
        AddPlayerRequestSystemComment(request, actor, "Игрок отменил заявку.", true);
        WriteAudit("player_request", actor.Id, "cancel", request.Id);
        TryAppendPlayerRequestJournalEntry(
            request,
            actor,
            "request_cancelled",
            $"Заявка #{request.RequestNumber}: {request.Title}",
            "Игрок отменил заявку.",
            string.Empty,
            EventJournalSeverityIds.Notice);
        return Ok("Player request cancelled.", new Dictionary<string, object> { { "item", PlayerRequestPayload(request, actor, includeAdminFields: false) } });
    }

    public ResponseEnvelope AdminPlayerRequestList(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!PlayerRequestAdminReviewEnabled()) return PlayerRequestsDisabled(context);
        var status = NormalizePlayerRequestStatusFilter(PayloadReader.GetString(context.Request.Payload, "status"));
        var campaignId = RequireLength(PayloadReader.GetString(context.Request.Payload, "campaignId"), 0, 128, "campaignId");
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");

        var filter = FilterDefinition<PlayerRequestState>.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId))
            filter &= Builders<PlayerRequestState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(status))
            filter &= Builders<PlayerRequestState>.Filter.Eq(x => x.Status, status);
        if (!includeArchived)
            filter &= Builders<PlayerRequestState>.Filter.Ne(x => x.Status, PlayerRequestStatusIds.Archived);

        var items = _repositories.PlayerRequests.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(300)
            .Select(x => (object)PlayerRequestPayload(x, actor, includeAdminFields: true))
            .ToArray();
        return Ok("Admin player requests loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope AdminPlayerRequestGet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!PlayerRequestAdminReviewEnabled()) return PlayerRequestsDisabled(context);
        var request = RequirePlayerRequest(context);
        return Ok("Admin player request loaded.", new Dictionary<string, object> { { "item", PlayerRequestPayload(request, actor, includeAdminFields: true) } });
    }

    public ResponseEnvelope AdminPlayerRequestSetInReview(CommandContext context)
    {
        return AdminPlayerRequestTransition(context, PlayerRequestStatusIds.InReview, "set_in_review");
    }

    public ResponseEnvelope AdminPlayerRequestApprove(CommandContext context)
    {
        return AdminPlayerRequestTransition(context, PlayerRequestStatusIds.Approved, "approve");
    }

    public ResponseEnvelope AdminPlayerRequestReject(CommandContext context)
    {
        return AdminPlayerRequestTransition(context, PlayerRequestStatusIds.Rejected, "reject");
    }

    public ResponseEnvelope AdminPlayerRequestRequestChanges(CommandContext context)
    {
        return AdminPlayerRequestTransition(context, PlayerRequestStatusIds.ChangesRequested, "request_changes");
    }

    public ResponseEnvelope AdminPlayerRequestMarkFulfilled(CommandContext context)
    {
        return AdminPlayerRequestTransition(context, PlayerRequestStatusIds.Fulfilled, "mark_fulfilled");
    }

    public ResponseEnvelope AdminPlayerRequestArchive(CommandContext context)
    {
        return AdminPlayerRequestTransition(context, PlayerRequestStatusIds.Archived, "archive");
    }

    public ResponseEnvelope AdminPlayerRequestComment(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!PlayerRequestAdminReviewEnabled()) return PlayerRequestsDisabled(context);
        if (!PlayerRequestCommentsEnabled()) return Error("Player request comments are disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var request = RequirePlayerRequest(context);
        var text = RequireLength(PayloadReader.GetString(context.Request.Payload, "text"), 1, 2000, "text");
        var isPlayerVisible = !context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible");
        var gmNotes = RequireLength(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 0, 2000, "gmNotes");
        AddPlayerRequestComment(request, actor, PlayerRequestCommentAuthorRoleIds.GM, text, isPlayerVisible, gmNotes);
        TouchPlayerRequest(request, actor.Id);
        _repositories.PlayerRequests.Replace(request);
        WriteAudit("player_request", actor.Id, "comment", request.Id);
        return Ok("Admin player request comment added.", new Dictionary<string, object> { { "item", PlayerRequestPayload(request, actor, includeAdminFields: true) } });
    }

    public ResponseEnvelope RequestStatusGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!PlayerRequestsBaseEnabled()) return PlayerRequestsDisabled(context);
        var request = RequirePlayerRequest(context);
        if (request.CreatedByUserId != actor.Id && !IsAdmin(actor)) throw new UnauthorizedAccessException("Request is not visible for current user.");
        return Ok("Player request status loaded.", new Dictionary<string, object>
        {
            { "requestId", request.Id },
            { "status", request.Status },
            { "updatedAtUtc", request.UpdatedAtUtc },
            { "gmResponse", request.IsPlayerVisible || IsAdmin(actor) ? request.GMResponse : string.Empty }
        });
    }

    private ResponseEnvelope AdminPlayerRequestTransition(CommandContext context, string nextStatus, string action)
    {
        var actor = RequireAdmin(context);
        if (!PlayerRequestAdminReviewEnabled()) return PlayerRequestsDisabled(context);
        var request = RequirePlayerRequest(context);
        var from = request.Status;
        ValidatePlayerRequestTransition(from, nextStatus);
        var playerComment = RequireLength(FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "decisionCommentPlayerVisible"),
            PayloadReader.GetString(context.Request.Payload, "playerComment"),
            PayloadReader.GetString(context.Request.Payload, "gmResponse"),
            PayloadReader.GetString(context.Request.Payload, "comment")), 0, 4096, "decisionCommentPlayerVisible");
        var gmOnlyComment = RequireLength(FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "decisionCommentGMOnly"),
            PayloadReader.GetString(context.Request.Payload, "gmNotes")), 0, 4096, "decisionCommentGMOnly");
        var adminOnlyNotes = RequireLength(FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "adminOnlyNotes"),
            gmOnlyComment), 0, 4096, "adminOnlyNotes");
        var reason = RequireLength(FirstNonEmpty(
            PayloadReader.GetString(context.Request.Payload, "resolutionReason"),
            PayloadReader.GetString(context.Request.Payload, "reason")), 0, 2048, "resolutionReason");

        request.Status = nextStatus;
        request.Decision = action;
        request.GMResponse = playerComment;
        request.DecisionCommentPlayerVisible = playerComment;
        request.DecisionCommentGMOnly = gmOnlyComment;
        request.AdminOnlyNotes = adminOnlyNotes;
        request.ResolutionReason = reason;
        request.ReviewedByUserId = actor.Id;
        request.ReviewedByDisplayName = FirstNonEmpty(actor.Login, actor.Id);
        request.AssignedAdminUserId = actor.Id;
        if (nextStatus == PlayerRequestStatusIds.InReview
            || nextStatus == PlayerRequestStatusIds.ChangesRequested
            || nextStatus == PlayerRequestStatusIds.Approved
            || nextStatus == PlayerRequestStatusIds.Rejected)
        {
            request.ReviewedAtUtc = DateTime.UtcNow;
        }
        if (nextStatus == PlayerRequestStatusIds.Approved || nextStatus == PlayerRequestStatusIds.Rejected)
        {
            request.DecidedByUserId = actor.Id;
            request.DecidedByDisplayName = FirstNonEmpty(actor.Login, actor.Id);
            request.DecidedAtUtc = DateTime.UtcNow;
            request.ResolvedAtUtc = request.DecidedAtUtc;
        }
        if (nextStatus == PlayerRequestStatusIds.Fulfilled || nextStatus == PlayerRequestStatusIds.Archived)
            request.ResolvedAtUtc = DateTime.UtcNow;
        if (nextStatus == PlayerRequestStatusIds.Archived)
            request.IsArchived = true;
        TouchPlayerRequest(request, actor.Id);
        AddPlayerRequestAudit(request, actor, $"request.{action}", from, request.Status, playerComment, true);
        _repositories.PlayerRequests.Replace(request);

        if (!string.IsNullOrWhiteSpace(playerComment))
            AddPlayerRequestComment(request, actor, PlayerRequestCommentAuthorRoleIds.GM, playerComment, true, string.Empty);
        if (!string.IsNullOrWhiteSpace(gmOnlyComment))
            AddPlayerRequestComment(request, actor, PlayerRequestCommentAuthorRoleIds.GM, "GM-only decision", false, gmOnlyComment);
        if (!string.IsNullOrWhiteSpace(adminOnlyNotes) && !string.Equals(adminOnlyNotes, gmOnlyComment, StringComparison.Ordinal))
            AddPlayerRequestComment(request, actor, PlayerRequestCommentAuthorRoleIds.GM, "GM-only note", false, adminOnlyNotes);
        WriteAudit("player_request", actor.Id, action, request.Id);
        TryAppendPlayerRequestJournalEntry(
            request,
            actor,
            $"request_{action}",
            $"Заявка #{request.RequestNumber}: {request.Title}",
            FirstNonEmpty(playerComment, $"Статус заявки изменён: {PlayerRequestStatusLabel(request.Status)}."),
            FirstNonEmpty(gmOnlyComment, adminOnlyNotes),
            nextStatus == PlayerRequestStatusIds.Approved ? EventJournalSeverityIds.Important : EventJournalSeverityIds.Notice);
        _logger.Admin($"player.request.{action}.done actor={actor.Login} requestId={request.Id} status={request.Status}");
        return Ok("Player request updated.", new Dictionary<string, object> { { "item", PlayerRequestPayload(request, actor, includeAdminFields: true) } });
    }
    private PlayerRequestState RequireOwnPlayerRequest(CommandContext context, UserAccount actor)
    {
        var request = RequirePlayerRequest(context);
        if (request.CreatedByUserId != actor.Id) throw new UnauthorizedAccessException("Cannot access another player's request.");
        return request;
    }

    private PlayerRequestState RequirePlayerRequest(CommandContext context)
    {
        var requestId = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "requestId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "requestId");
        return _repositories.PlayerRequests.GetById(requestId) ?? throw new KeyNotFoundException("Player request not found.");
    }

    private Dictionary<string, object> PlayerRequestPayload(PlayerRequestState request, UserAccount viewer, bool includeAdminFields)
    {
        var comments = _repositories.PlayerRequestComments.Find(Builders<PlayerRequestCommentState>.Filter.Eq(x => x.RequestId, request.Id))
            .Where(x => !x.IsArchived && (includeAdminFields || x.IsPlayerVisible))
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => (object)PlayerRequestCommentPayload(x, includeAdminFields))
            .ToArray();

        var details = FirstNonEmpty(request.Details, request.Description);
        var playerComment = FirstNonEmpty(request.DecisionCommentPlayerVisible, request.GMResponse);
        var submittedBy = ResolvePlayerRequestActorDisplayName(request.CreatedByDisplayName, request.CreatedByUserId);
        var reviewedBy = ResolvePlayerRequestActorDisplayName(request.ReviewedByDisplayName, request.ReviewedByUserId);
        var decidedBy = ResolvePlayerRequestActorDisplayName(request.DecidedByDisplayName, request.DecidedByUserId);
        var cancelledBy = ResolvePlayerRequestActorDisplayName(request.CancelledByDisplayName, request.CancelledByUserId);
        var lastActor = PlayerRequestLastActorDisplayName(request, submittedBy, reviewedBy, decidedBy, cancelledBy);
        var lastAction = PlayerRequestLastActionText(request, submittedBy, reviewedBy, decidedBy, cancelledBy);
        var requestNumber = EnsurePlayerRequestNumber(request);
        var item = new Dictionary<string, object>
        {
            { "requestId", request.Id },
            { "id", request.Id },
            { "requestNumber", requestNumber },
            { "displayRequestId", requestNumber },
            { "requestNumberLabel", "№ " + requestNumber },
            { "characterId", request.CharacterId },
            { "characterDisplayName", FirstNonEmpty(request.CharacterName, request.CharacterId) },
            { "requestType", request.RequestType },
            { "type", request.RequestType },
            { "category", request.Category },
            { "title", request.Title },
            { "name", request.Title },
            { "details", details },
            { "description", details },
            { "reason", request.Reason },
            { "status", request.Status },
            { "priority", request.Priority },
            { "playerVisibleStatusText", PlayerRequestStatusLabel(request.Status) },
            { "decisionCommentPlayerVisible", playerComment },
            { "gmResponse", playerComment },
            { "canCancel", request.Status == PlayerRequestStatusIds.Submitted || request.Status == PlayerRequestStatusIds.InReview || request.Status == PlayerRequestStatusIds.ChangesRequested || request.Status == PlayerRequestStatusIds.Draft },
            { "canResubmit", request.Status == PlayerRequestStatusIds.ChangesRequested && request.ResubmissionCount < 1 },
            { "canEditDraft", request.Status == PlayerRequestStatusIds.Draft },
            { "campaignId", request.CampaignId },
            { "sessionId", request.SessionId },
            { "groupId", request.GroupId },
            { "createdByDisplayName", submittedBy },
            { "submittedByDisplayName", submittedBy },
            { "submittedByLogin", submittedBy },
            { "reviewedByDisplayName", reviewedBy },
            { "decisionByDisplayName", decidedBy },
            { "decidedByDisplayName", decidedBy },
            { "cancelledByDisplayName", cancelledBy },
            { "lastActorDisplayName", lastActor },
            { "lastActionDisplayText", lastAction },
            { "createdAtUtc", request.CreatedAtUtc },
            { "updatedAtUtc", request.UpdatedAtUtc },
            { "submittedAtUtc", request.SubmittedAtUtc.HasValue ? (object)request.SubmittedAtUtc.Value : string.Empty },
            { "reviewedAtUtc", request.ReviewedAtUtc.HasValue ? (object)request.ReviewedAtUtc.Value : string.Empty },
            { "decidedAtUtc", request.DecidedAtUtc.HasValue ? (object)request.DecidedAtUtc.Value : string.Empty },
            { "resolvedAtUtc", request.ResolvedAtUtc.HasValue ? (object)request.ResolvedAtUtc.Value : string.Empty },
            { "resubmissionCount", request.ResubmissionCount },
            { "linkedEntityType", SafeLinkedEntityType(request, includeAdminFields) },
            { "linkedEntityDisplayName", SafeLinkedEntityType(request, includeAdminFields) },
            { "linkedEntityId", includeAdminFields ? request.LinkedEntityId : string.Empty },
            { "proposalType", request.ProposalType },
            { "proposalPayloadSummary", request.ProposalPayloadSummary },
            { "formula", PlayerRequestSummary(request) },
            { "extra", PlayerRequestSummary(request) },
            { "comments", comments }
        };

        if (includeAdminFields)
        {
            item["ownerUserId"] = FirstNonEmpty(request.OwnerUserId, request.CreatedByUserId);
            item["createdByUserId"] = request.CreatedByUserId;
            item["submittedByUserId"] = request.CreatedByUserId;
            item["creatorUserId"] = request.CreatedByUserId;
            item["creatorLogin"] = submittedBy;
            item["companionId"] = request.CompanionId;
            item["visibilityMode"] = request.VisibilityMode;
            item["isPlayerVisible"] = request.IsPlayerVisible;
            item["reviewedByUserId"] = request.ReviewedByUserId;
            item["reviewedByDisplayName"] = reviewedBy;
            item["decidedByUserId"] = request.DecidedByUserId;
            item["decidedByDisplayName"] = decidedBy;
            item["cancelledByUserId"] = request.CancelledByUserId;
            item["cancelledByDisplayName"] = cancelledBy;
            item["assignedAdminUserId"] = request.AssignedAdminUserId;
            item["decision"] = request.Decision;
            item["decisionCommentGMOnly"] = request.DecisionCommentGMOnly;
            item["adminOnlyNotes"] = FirstNonEmpty(request.AdminOnlyNotes, request.GMNotes);
            item["resolutionReason"] = request.ResolutionReason;
            item["publicNotes"] = request.PublicNotes;
            item["gmNotes"] = request.GMNotes;
            item["proposalPayload"] = PlayerRequestProposalPayload(request.ProposalPayload);
            item["revision"] = request.Revision;
            item["isArchived"] = request.IsArchived || request.Status == PlayerRequestStatusIds.Archived;
            item["isDeleted"] = request.IsDeleted;
            item["auditTrail"] = request.AuditTrail.Select(x => (object)PlayerRequestAuditPayload(x, includeAdminFields)).ToArray();
            item["tags"] = request.Tags.Cast<object>().ToArray();
        }

        return item;
    }

    private string NextPlayerRequestNumber()
    {
        EnsurePlayerRequestNumbersInitialized();
        var now = DateTime.UtcNow;
        var counters = _mongo.Database.GetCollection<BsonDocument>("sync_counters");
        var update = Builders<BsonDocument>.Update
            .SetOnInsert("CounterKey", PlayerRequestNumberCounterKey)
            .SetOnInsert("CreatedUtc", now)
            .Inc("Value", 1L)
            .Set("UpdatedUtc", now);
        var options = new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
        var item = counters.FindOneAndUpdate(Builders<BsonDocument>.Filter.Eq("CounterKey", PlayerRequestNumberCounterKey), update, options);
        var next = Math.Max(1, BsonLong(item, "Value", 1));
        return next.ToString(CultureInfo.InvariantCulture);
    }

    private string EnsurePlayerRequestNumber(PlayerRequestState request)
    {
        if (TryParsePlayerRequestNumber(request.RequestNumber, out var existing))
            return existing.ToString(CultureInfo.InvariantCulture);

        EnsurePlayerRequestNumbersInitialized();
        if (TryParsePlayerRequestNumber(request.RequestNumber, out existing))
            return existing.ToString(CultureInfo.InvariantCulture);

        lock (_playerRequestNumberLock)
        {
            if (TryParsePlayerRequestNumber(request.RequestNumber, out existing))
                return existing.ToString(CultureInfo.InvariantCulture);

            request.RequestNumber = NextPlayerRequestNumber();
            _repositories.PlayerRequests.Replace(request);
            return request.RequestNumber;
        }
    }

    private void EnsurePlayerRequestNumbersInitialized()
    {
        if (_playerRequestNumbersInitialized) return;
        lock (_playerRequestNumberLock)
        {
            if (_playerRequestNumbersInitialized) return;
            var collection = _mongo.Database.GetCollection<BsonDocument>("player_requests");
            var filter = Builders<BsonDocument>.Filter.Ne("Deleted", true) & Builders<BsonDocument>.Filter.Ne("IsDeleted", true);
            var sort = Builders<BsonDocument>.Sort.Ascending("CreatedAtUtc").Ascending("CreatedUtc").Ascending("_id");
            var requests = collection.Find(filter).Sort(sort).ToList();

            long maxNumber = 0;
            foreach (var request in requests)
            {
                if (TryGetPlayerRequestNumber(request, out var number))
                    maxNumber = Math.Max(maxNumber, number);
            }

            foreach (var request in requests)
            {
                if (TryGetPlayerRequestNumber(request, out _)) continue;
                maxNumber++;
                var id = request.GetValue("_id", BsonNull.Value);
                if (id == BsonNull.Value) continue;
                var updateNumber = Builders<BsonDocument>.Update.Set("RequestNumber", maxNumber.ToString(CultureInfo.InvariantCulture));
                collection.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", id), updateNumber);
            }

            var now = DateTime.UtcNow;
            var counters = _mongo.Database.GetCollection<BsonDocument>("sync_counters");
            var update = Builders<BsonDocument>.Update
                .SetOnInsert("CounterKey", PlayerRequestNumberCounterKey)
                .SetOnInsert("CreatedUtc", now)
                .Max("Value", maxNumber)
                .Set("UpdatedUtc", now);
            counters.UpdateOne(
                Builders<BsonDocument>.Filter.Eq("CounterKey", PlayerRequestNumberCounterKey),
                update,
                new UpdateOptions { IsUpsert = true });
            _playerRequestNumbersInitialized = true;
        }
    }

    private static bool TryParsePlayerRequestNumber(string value, out long number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return long.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;
    }

    private static bool TryGetPlayerRequestNumber(BsonDocument document, out long number)
    {
        number = 0;
        if (!document.TryGetValue("RequestNumber", out var value) && !document.TryGetValue("requestNumber", out value))
            return false;
        if (value == null || value.IsBsonNull) return false;
        if (value.IsInt32)
        {
            number = value.AsInt32;
            return number > 0;
        }
        if (value.IsInt64)
        {
            number = value.AsInt64;
            return number > 0;
        }

        return TryParsePlayerRequestNumber(value.ToString(), out number);
    }

    private static long BsonLong(BsonDocument? document, string key, long fallback)
    {
        if (document == null || !document.TryGetValue(key, out var value) || value == null || value.IsBsonNull)
            return fallback;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        if (long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return fallback;
    }

    private static Dictionary<string, object> PlayerRequestCommentPayload(PlayerRequestCommentState comment, bool includeAdminFields)
    {
        var item = new Dictionary<string, object>
        {
            { "commentId", comment.Id },
            { "authorDisplayName", comment.AuthorDisplayName },
            { "authorRole", comment.AuthorRole },
            { "text", comment.Text },
            { "isPlayerVisible", comment.IsPlayerVisible },
            { "createdAtUtc", comment.CreatedAtUtc }
        };
        if (includeAdminFields)
        {
            item["authorUserId"] = comment.AuthorUserId;
            item["gmNotes"] = comment.ServerOnlyData.ContainsKey("gmNotes") ? Convert.ToString(comment.ServerOnlyData["gmNotes"]) ?? string.Empty : string.Empty;
        }
        return item;
    }
    private static Dictionary<string, object> PlayerRequestProposalPayload(PlayerRequestProposalDraft proposal)
    {
        return new Dictionary<string, object>
        {
            { "proposalType", proposal.ProposalType },
            { "schemaVersion", proposal.SchemaVersion },
            { "displaySummary", proposal.DisplaySummary },
            { "parameters", proposal.Parameters },
            { "estimatedResult", proposal.EstimatedResult },
            { "warnings", proposal.Warnings.Cast<object>().ToArray() },
            { "requiresGMApproval", proposal.RequiresGMApproval }
        };
    }

    private void AddPlayerRequestSystemComment(PlayerRequestState request, UserAccount actor, string text, bool isPlayerVisible)
    {
        if (!PlayerRequestCommentsEnabled()) return;
        AddPlayerRequestComment(request, actor, PlayerRequestCommentAuthorRoleIds.System, text, isPlayerVisible, string.Empty);
    }

    private void AddPlayerRequestComment(PlayerRequestState request, UserAccount actor, string role, string text, bool isPlayerVisible, string gmNotes)
    {
        var comment = new PlayerRequestCommentState
        {
            RequestId = request.Id,
            CampaignId = request.CampaignId,
            AuthorUserId = actor.Id,
            AuthorDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            AuthorRole = role,
            Text = text,
            IsPlayerVisible = isPlayerVisible,
            CreatedAtUtc = DateTime.UtcNow
        };
        if (!string.IsNullOrWhiteSpace(gmNotes)) comment.ServerOnlyData["gmNotes"] = gmNotes;
        _repositories.PlayerRequestComments.Insert(comment);
    }

    private static void AddPlayerRequestAudit(PlayerRequestState request, UserAccount actor, string action, string fromStatus, string toStatus, string summary, bool playerVisible)
    {
        request.AuditTrail.Add(new PlayerRequestAuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            ActorUserId = actor.Id,
            ActorDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            Action = action,
            FromStatus = fromStatus ?? string.Empty,
            ToStatus = toStatus ?? string.Empty,
            PlayerVisibleComment = playerVisible ? summary ?? string.Empty : string.Empty,
            Summary = summary ?? string.Empty
        });
    }

    private static Dictionary<string, object> PlayerRequestAuditPayload(PlayerRequestAuditEntry entry, bool includeAdminFields)
    {
        var item = new Dictionary<string, object>
        {
            { "timestampUtc", entry.TimestampUtc },
            { "action", entry.Action },
            { "fromStatus", entry.FromStatus },
            { "toStatus", entry.ToStatus },
            { "actorDisplayName", entry.ActorDisplayName },
            { "summary", includeAdminFields ? entry.Summary : entry.PlayerVisibleComment }
        };
        if (includeAdminFields)
        {
            item["actorUserId"] = entry.ActorUserId;
        }
        return item;
    }

    private string ResolvePlayerRequestActorDisplayName(string displayName, string userId)
    {
        if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
        if (string.IsNullOrWhiteSpace(userId)) return string.Empty;
        var account = _repositories.Accounts.GetById(userId);
        return FirstNonEmpty(account?.Login ?? string.Empty, userId);
    }

    private string PlayerRequestLastActorDisplayName(PlayerRequestState request, string submittedBy, string reviewedBy, string decidedBy, string cancelledBy)
    {
        if (request.Status == PlayerRequestStatusIds.Cancelled) return FirstNonEmpty(cancelledBy, LastAuditActor(request), submittedBy);
        if (request.Status == PlayerRequestStatusIds.Approved || request.Status == PlayerRequestStatusIds.Rejected)
            return FirstNonEmpty(decidedBy, reviewedBy, LastAuditActor(request), submittedBy);
        if (request.Status == PlayerRequestStatusIds.InReview
            || request.Status == PlayerRequestStatusIds.ChangesRequested
            || request.Status == PlayerRequestStatusIds.Fulfilled
            || request.Status == PlayerRequestStatusIds.Archived)
            return FirstNonEmpty(reviewedBy, LastAuditActor(request), submittedBy);
        return FirstNonEmpty(LastAuditActor(request), submittedBy);
    }

    private static string PlayerRequestLastActionText(PlayerRequestState request, string submittedBy, string reviewedBy, string decidedBy, string cancelledBy)
    {
        if (request.Status == PlayerRequestStatusIds.Approved) return "Одобрил: " + FirstNonEmpty(decidedBy, reviewedBy, "—");
        if (request.Status == PlayerRequestStatusIds.Rejected) return "Отклонил: " + FirstNonEmpty(decidedBy, reviewedBy, "—");
        if (request.Status == PlayerRequestStatusIds.Cancelled) return "Отменил: " + FirstNonEmpty(cancelledBy, submittedBy, "—");
        if (request.Status == PlayerRequestStatusIds.ChangesRequested) return "Запросил уточнения: " + FirstNonEmpty(reviewedBy, "—");
        if (request.Status == PlayerRequestStatusIds.InReview) return "Взял в рассмотрение: " + FirstNonEmpty(reviewedBy, "—");
        if (request.Status == PlayerRequestStatusIds.Fulfilled) return "Выполнил: " + FirstNonEmpty(reviewedBy, "—");
        if (request.Status == PlayerRequestStatusIds.Archived) return "Архивировал: " + FirstNonEmpty(reviewedBy, "—");
        return "Отправил: " + FirstNonEmpty(submittedBy, "—");
    }

    private static string LastAuditActor(PlayerRequestState request)
    {
        return request.AuditTrail
            .OrderByDescending(x => x.TimestampUtc)
            .Select(x => x.ActorDisplayName)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    }

    private PlayerRequestProposalDraft BuildPlayerRequestProposalDraft(IDictionary<string, object> payload, string proposalType, string summary)
    {
        var draft = new PlayerRequestProposalDraft
        {
            ProposalType = proposalType,
            DisplaySummary = summary,
            RequiresGMApproval = true
        };
        if (!PlayerRequestProposalPayloadEnabled()) return draft;

        var parameters = PayloadReader.GetDictionary(payload, "proposalPayload");
        if (parameters == null) return draft;
        foreach (var pair in parameters.Take(64))
        {
            if (IsUnsafePlayerRequestPayloadKey(pair.Key)) continue;
            draft.Parameters[pair.Key] = SanitizePlayerRequestPayloadValue(pair.Value);
        }
        return draft;
    }

    private static object SanitizePlayerRequestPayloadValue(object value)
    {
        if (value == null) return string.Empty;
        if (value is string s) return s.Length > 512 ? s.Substring(0, 512) : s;
        if (value is int || value is long || value is double || value is float || value is decimal || value is bool || value is DateTime) return value;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>();
            var count = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (++count > 32) break;
                var key = Convert.ToString(entry.Key) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key) || IsUnsafePlayerRequestPayloadKey(key)) continue;
                result[key] = SanitizePlayerRequestPayloadValue(entry.Value!);
            }
            return result;
        }
        return Convert.ToString(value) ?? string.Empty;
    }

    private static bool IsUnsafePlayerRequestPayloadKey(string key)
    {
        return key.IndexOf("serverOnly", StringComparison.OrdinalIgnoreCase) >= 0
            || key.IndexOf("gmNotes", StringComparison.OrdinalIgnoreCase) >= 0
            || key.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
            || key.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string EnsurePlayerRequestCharacterAccess(UserAccount actor, string characterId)
    {
        var character = _repositories.Characters.GetById(characterId);
        if (character == null) throw new KeyNotFoundException("Character not found.");
        if (character.OwnerUserId == actor.Id) return FirstNonEmpty(character.Name, characterId);
        var ownership = _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        if (ownership != null && (ownership.OwnerUserId == actor.Id || ownership.ControlledByUserId == actor.Id)) return FirstNonEmpty(character.Name, characterId);
        throw new UnauthorizedAccessException("Character unavailable for player request.");
    }

    private static void ValidatePlayerRequestTransition(string currentStatus, string nextStatus)
    {
        if (currentStatus == nextStatus)
            throw new InvalidOperationException("Request already has the requested status.");
        if (currentStatus == PlayerRequestStatusIds.Archived) throw new InvalidOperationException("Archived request cannot be changed.");
        if (currentStatus == PlayerRequestStatusIds.Approved || currentStatus == PlayerRequestStatusIds.Rejected || currentStatus == PlayerRequestStatusIds.Cancelled || currentStatus == PlayerRequestStatusIds.Fulfilled)
            throw new InvalidOperationException("Terminal request cannot be changed.");
        if (nextStatus == PlayerRequestStatusIds.InReview && currentStatus != PlayerRequestStatusIds.Submitted) throw new InvalidOperationException("Only submitted requests can be moved to review.");
        if (nextStatus == PlayerRequestStatusIds.ChangesRequested
            && currentStatus != PlayerRequestStatusIds.Submitted
            && currentStatus != PlayerRequestStatusIds.InReview)
            throw new InvalidOperationException("Only submitted or in-review requests can request changes.");
        if ((nextStatus == PlayerRequestStatusIds.Approved || nextStatus == PlayerRequestStatusIds.Rejected)
            && currentStatus != PlayerRequestStatusIds.Submitted
            && currentStatus != PlayerRequestStatusIds.InReview)
            throw new InvalidOperationException("Only submitted or in-review requests can be resolved.");
        if (nextStatus == PlayerRequestStatusIds.Fulfilled && currentStatus != PlayerRequestStatusIds.Approved) throw new InvalidOperationException("Only approved requests can be marked fulfilled.");
    }

    private static bool IsPlayerRequestTerminal(string status)
    {
        return status == PlayerRequestStatusIds.Rejected
            || status == PlayerRequestStatusIds.Cancelled
            || status == PlayerRequestStatusIds.Fulfilled
            || status == PlayerRequestStatusIds.Archived;
    }

    private static void TouchPlayerRequest(PlayerRequestState request, string actorUserId)
    {
        request.UpdatedAtUtc = DateTime.UtcNow;
        request.UpdatedUtc = request.UpdatedAtUtc;
        request.Revision++;
        request.ExtraData["lastTouchedByUserId"] = actorUserId;
    }

    private static string NormalizePlayerRequestType(string? value)
    {
        var type = (value ?? string.Empty).Trim().ToLowerInvariant();
        switch (type)
        {
            case PlayerRequestTypeIds.GenericAction:
            case PlayerRequestTypeIds.DevelopmentUnlock:
            case PlayerRequestTypeIds.ItemRequest:
            case PlayerRequestTypeIds.RulesQuestion:
            case PlayerRequestTypeIds.General:
            case PlayerRequestTypeIds.Action:
            case PlayerRequestTypeIds.Question:
            case PlayerRequestTypeIds.CharacterChange:
            case PlayerRequestTypeIds.CharacterAssignment:
            case PlayerRequestTypeIds.OwnershipTransfer:
            case PlayerRequestTypeIds.Purchase:
            case PlayerRequestTypeIds.Research:
            case PlayerRequestTypeIds.Crafting:
            case PlayerRequestTypeIds.EngineeringDesign:
            case PlayerRequestTypeIds.FactoryQuote:
            case PlayerRequestTypeIds.FactoryOrder:
            case PlayerRequestTypeIds.ItemCreation:
            case PlayerRequestTypeIds.EquipmentChange:
            case PlayerRequestTypeIds.SceneAction:
            case PlayerRequestTypeIds.InformationRequest:
            case PlayerRequestTypeIds.MapRequest:
                return type;
            default:
                return PlayerRequestTypeIds.General;
        }
    }

    private static string PlayerRequestStatusLabel(string status)
    {
        return status switch
        {
            PlayerRequestStatusIds.Draft => "Черновик",
            PlayerRequestStatusIds.Submitted => "Отправлена",
            PlayerRequestStatusIds.InReview => "На рассмотрении",
            PlayerRequestStatusIds.ChangesRequested => "Требуются уточнения",
            PlayerRequestStatusIds.Approved => "Одобрено",
            PlayerRequestStatusIds.Rejected => "Отклонено",
            PlayerRequestStatusIds.Cancelled => "Отменено",
            PlayerRequestStatusIds.Fulfilled => "Выполнено",
            PlayerRequestStatusIds.Archived => "В архиве",
            _ => status ?? string.Empty
        };
    }

    private static string TrimForAudit(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Length <= 160 ? value : value.Substring(0, 160);
    }

    private static string NormalizePlayerRequestPriority(string? value)
    {
        var priority = (value ?? string.Empty).Trim().ToLowerInvariant();
        switch (priority)
        {
            case PlayerRequestPriorityIds.Low:
            case PlayerRequestPriorityIds.High:
            case PlayerRequestPriorityIds.Urgent:
                return priority;
            default:
                return PlayerRequestPriorityIds.Normal;
        }
    }

    private static string NormalizePlayerRequestVisibility(string? value)
    {
        var visibility = (value ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(visibility) ? "party" : visibility;
    }

    private static string NormalizePlayerRequestStatusFilter(string? value)
    {
        var status = (value ?? string.Empty).Trim().ToLowerInvariant();
        switch (status)
        {
            case PlayerRequestStatusIds.Draft:
            case PlayerRequestStatusIds.Submitted:
            case PlayerRequestStatusIds.InReview:
            case PlayerRequestStatusIds.ChangesRequested:
            case PlayerRequestStatusIds.Approved:
            case PlayerRequestStatusIds.Rejected:
            case PlayerRequestStatusIds.Cancelled:
            case PlayerRequestStatusIds.Fulfilled:
            case PlayerRequestStatusIds.Archived:
                return status;
            default:
                return string.Empty;
        }
    }

    private static string DefaultPlayerRequestTitle(string requestType)
    {
        switch (requestType)
        {
            case PlayerRequestTypeIds.Research:
                return "Заявка на исследование";
            case PlayerRequestTypeIds.Crafting:
                return "Заявка на крафт";
            case PlayerRequestTypeIds.EngineeringDesign:
                return "Заявка на инженерный проект";
            case PlayerRequestTypeIds.SceneAction:
                return "Действие в сцене";
            case PlayerRequestTypeIds.Question:
                return "Вопрос GM";
            default:
                return "Заявка GM";
        }
    }

    private static string PlayerRequestSummary(PlayerRequestState request)
    {
        return FirstNonEmpty(request.ProposalPayloadSummary, request.Description, request.Title);
    }

    private void TryAppendPlayerRequestJournalEntry(
        PlayerRequestState request,
        UserAccount actor,
        string sourceEventType,
        string title,
        string playerSummary,
        string gmDetails,
        string severity)
    {
        if (!PlayerRequestJournalIntegrationEnabled()) return;

        try
        {
            var now = DateTime.UtcNow;
            var subjectDisplay = $"Заявка #{request.RequestNumber}: {request.Title}";
            var entry = new EventJournalEntryState
            {
                CampaignId = FirstNonEmpty(request.CampaignId, "default"),
                SessionId = request.SessionId ?? string.Empty,
                GroupId = request.GroupId ?? string.Empty,
                CharacterId = request.CharacterId ?? string.Empty,
                SourceModule = "player_requests",
                SourceEventType = sourceEventType,
                SourceEventId = $"{request.Id}:{sourceEventType}:{request.Revision}",
                CorrelationId = request.Id,
                EntryType = EventJournalEntryTypeIds.Automatic,
                Category = EventJournalCategoryIds.Request,
                Severity = severity,
                Title = RequireLength(FirstNonEmpty(title, subjectDisplay), 1, 240, "journal.title"),
                Summary = RequireLength(FirstNonEmpty(playerSummary, PlayerRequestSummary(request), request.Title), 1, 2048, "journal.summary"),
                PlayerSummary = RequireLength(FirstNonEmpty(playerSummary, request.GMResponse, PlayerRequestSummary(request)), 0, 2048, "journal.playerSummary"),
                GMDetails = RequireLength(gmDetails ?? string.Empty, 0, 8192, "journal.gmDetails"),
                VisibilityMode = EventJournalVisibilityModeIds.PlayerVisible,
                IsPlayerVisible = true,
                IsAutomatic = true,
                ActorUserId = actor.Id,
                ActorDisplayName = FirstNonEmpty(actor.Login, actor.Id),
                SubjectEntityType = EventJournalEntityTypeIds.PlayerRequest,
                SubjectEntityId = request.Id,
                SubjectDisplayName = subjectDisplay,
                OccurredAtUtc = now,
                CreatedAtUtc = now,
                CreatedByUserId = actor.Id,
                UpdatedAtUtc = now,
                Tags = new List<string> { "player_request", sourceEventType, request.Status }
            };

            InsertJournalEntry(actor, entry, sourceEventType);
            _logger.Admin($"player.request.journal.appended requestId={request.Id} requestNumber={request.RequestNumber} sourceEventType={sourceEventType} entryId={entry.Id}");
        }
        catch (Exception ex)
        {
            _logger.Admin($"player.request.journal.warning requestId={request.Id} sourceEventType={sourceEventType} message={ex.Message}");
        }
    }

    private static string SafeLinkedEntityType(PlayerRequestState request, bool includeAdminFields)
    {
        if (includeAdminFields) return request.LinkedEntityType;
        if (string.IsNullOrWhiteSpace(request.LinkedEntityType)) return string.Empty;
        return request.IsPlayerVisible ? request.LinkedEntityType : string.Empty;
    }

    private bool PlayerRequestsBaseEnabled()
    {
        return _featureFlags.IsEnabled(nameof(PlayerRequestFeatureFlags.UsePlayerRequestsMvp));
    }

    private bool PlayerRequestJournalIntegrationEnabled()
    {
        return _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp))
            && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion))
            && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalRequestIntegration));
    }

    private bool PlayerRequestPlayerEnabled()
    {
        return PlayerRequestsBaseEnabled() && _featureFlags.IsEnabled(nameof(PlayerRequestFeatureFlags.UsePlayerRequestPlayerView));
    }

    private bool PlayerRequestAdminReviewEnabled()
    {
        return PlayerRequestsBaseEnabled() && _featureFlags.IsEnabled(nameof(PlayerRequestFeatureFlags.UsePlayerRequestAdminReview));
    }

    private bool PlayerRequestCommentsEnabled()
    {
        return PlayerRequestsBaseEnabled() && _featureFlags.IsEnabled(nameof(PlayerRequestFeatureFlags.UsePlayerRequestComments));
    }

    private bool PlayerRequestSessionLinkEnabled()
    {
        return PlayerRequestsBaseEnabled() && _featureFlags.IsEnabled(nameof(PlayerRequestFeatureFlags.UsePlayerRequestSessionLink));
    }

    private bool PlayerRequestCharacterLinkEnabled()
    {
        return PlayerRequestsBaseEnabled() && _featureFlags.IsEnabled(nameof(PlayerRequestFeatureFlags.UsePlayerRequestCharacterLink));
    }

    private bool PlayerRequestProposalPayloadEnabled()
    {
        return PlayerRequestsBaseEnabled() && _featureFlags.IsEnabled(nameof(PlayerRequestFeatureFlags.UsePlayerRequestProposalPayload));
    }

    private ResponseEnvelope PlayerRequestsDisabled(CommandContext context)
    {
        _logger.Admin($"player.request.disabled command={context.Request.Command}");
        return Error("Player Requests MVP is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private static bool IsAdmin(UserAccount actor)
    {
        return actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
    }
}
