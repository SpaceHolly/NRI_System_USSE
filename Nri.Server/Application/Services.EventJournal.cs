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
    private const int EventJournalMaxList = 500;

    public ResponseEnvelope JournalEventList(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = JournalCampaignId(payload);
        var items = QueryAdminJournalEntries(actor, payload, campaignId)
            .Select(x => (object)EventJournalAdminPayload(x, includeDetails: false))
            .ToArray();
        _logger.Admin($"journal.event.list actor={actor.Login} campaignId={campaignId} count={items.Length}");
        return Ok("Event journal loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope JournalEventSearch(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        if (!EventJournalFiltersEnabled()) return EventJournalFeatureDisabled("Поиск и фильтры журнала событий выключены feature flags.");
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = JournalCampaignId(payload);
        var query = (PayloadReader.GetString(payload, "query") ?? string.Empty).Trim();
        var items = QueryAdminJournalEntries(actor, payload, campaignId)
            .Where(x => query.Length < 2 || JournalContains(x, query) || VisibleLinksForEntry(x, playerSafe: false).Any(link => Contains(link.DisplayName, query)))
            .Select(x => (object)EventJournalAdminPayload(x, includeDetails: false))
            .ToArray();
        return Ok("Event journal search completed.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope JournalEventGet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        var entry = RequireJournalEntry(context, actor, playerSafe: false);
        return Ok("Event journal entry loaded.", EventJournalEntryEnvelope(entry, playerSafe: false));
    }

    public ResponseEnvelope JournalEventIngest(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        if (!EventJournalAutomaticIngestionEnabled()) return EventJournalFeatureDisabled("Автоматическое добавление событий в журнал выключено feature flags.");
        var entry = CreateJournalEntryFromPayload(context.Request.Payload ?? new Dictionary<string, object>(), actor, EventJournalEntryTypeIds.Automatic, isAutomatic: true, isCorrection: false);
        var existing = FindDuplicateJournalEntry(entry);
        if (existing != null)
        {
            _logger.Admin($"journal.event.duplicate_ignored source={entry.SourceModule}:{entry.SourceEventId} correlation={entry.CorrelationId}");
            return Ok("Duplicate journal event ignored.", new Dictionary<string, object>
            {
                { "item", EventJournalAdminPayload(existing, includeDetails: false) },
                { "duplicateIgnored", true }
            });
        }

        InsertJournalEntry(actor, entry, "ingested");
        return Ok("Event journal entry ingested.", new Dictionary<string, object> { { "item", EventJournalAdminPayload(entry, includeDetails: false) } });
    }

    public ResponseEnvelope JournalEventManualCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        if (!EventJournalManualEntriesEnabled()) return EventJournalFeatureDisabled("Ручные записи журнала событий выключены feature flags.");
        var entry = CreateJournalEntryFromPayload(context.Request.Payload ?? new Dictionary<string, object>(), actor, EventJournalEntryTypeIds.Manual, isAutomatic: false, isCorrection: false);
        InsertJournalEntry(actor, entry, "manual.created");
        return Ok("Manual journal entry created.", new Dictionary<string, object> { { "item", EventJournalAdminPayload(entry, includeDetails: false) } });
    }

    public ResponseEnvelope JournalEventManualUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        if (!EventJournalManualEntriesEnabled()) return EventJournalFeatureDisabled("Ручные записи журнала событий выключены feature flags.");
        var entry = RequireJournalEntry(context, actor, playerSafe: false);
        if (entry.IsArchived) return Error("Archived journal entry cannot be edited.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (entry.IsAutomatic || entry.IsCorrection || !string.Equals(entry.EntryType, EventJournalEntryTypeIds.Manual, StringComparison.OrdinalIgnoreCase))
            return Error("Automatic journal entries are append-only. Create a correction entry instead.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (payload.ContainsKey("title")) entry.Title = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "title"), entry.Title), 1, 240, "title");
        if (payload.ContainsKey("summary")) entry.Summary = RequireLength(PayloadReader.GetString(payload, "summary"), 1, 2048, "summary");
        if (payload.ContainsKey("playerSummary") || payload.ContainsKey("playerVisibleText"))
            entry.PlayerSummary = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "playerSummary"), PayloadReader.GetString(payload, "playerVisibleText")), 0, 2048, "playerSummary");
        if (payload.ContainsKey("gmDetails") || payload.ContainsKey("gmOnlyDetails"))
            entry.GMDetails = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "gmDetails"), PayloadReader.GetString(payload, "gmOnlyDetails")), 0, 8192, "gmDetails");
        if (payload.ContainsKey("category")) entry.Category = NormalizeJournalCategory(PayloadReader.GetString(payload, "category"));
        if (payload.ContainsKey("severity")) entry.Severity = NormalizeJournalSeverity(PayloadReader.GetString(payload, "severity"));
        if (payload.ContainsKey("visibilityMode")) ApplyJournalVisibility(entry, NormalizeJournalVisibility(PayloadReader.GetString(payload, "visibilityMode")));
        if (payload.ContainsKey("tags") || payload.ContainsKey("tagsText")) entry.Tags = JournalTags(payload);
        entry.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.EventJournalEntries.Replace(entry);
        WriteJournalAudit(actor, entry, "manual.updated", "Manual journal entry updated.");
        return Ok("Manual journal entry updated.", new Dictionary<string, object> { { "item", EventJournalAdminPayload(entry, includeDetails: false) } });
    }

    public ResponseEnvelope JournalEventCorrectionCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        if (!EventJournalCorrectionsEnabled()) return EventJournalFeatureDisabled("Коррекции журнала событий выключены feature flags.");
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var correctsId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "correctsEntryId"), PayloadReader.GetString(payload, "entryId")), 1, 128, "correctsEntryId");
        var original = _repositories.EventJournalEntries.GetById(correctsId) ?? throw new KeyNotFoundException("Journal entry not found.");
        if (!CanViewJournalEntry(actor, original)) throw new UnauthorizedAccessException("Journal correction target is forbidden.");
        var entry = CreateJournalEntryFromPayload(payload, actor, EventJournalEntryTypeIds.Correction, isAutomatic: false, isCorrection: true);
        entry.CampaignId = original.CampaignId;
        entry.SessionId = FirstNonEmpty(entry.SessionId, original.SessionId);
        entry.GroupId = FirstNonEmpty(entry.GroupId, original.GroupId);
        entry.Category = FirstNonEmpty(entry.Category, original.Category, EventJournalCategoryIds.Custom);
        entry.CorrectsEntryId = original.Id;
        entry.RelatedEntryId = original.Id;
        InsertJournalEntry(actor, entry, "corrected");
        AddJournalLink(entry, EventJournalEntityTypeIds.Custom, original.Id, "Исходная запись", EventJournalLinkRoleIds.CorrectionOf, false);
        return Ok("Journal correction created.", new Dictionary<string, object> { { "item", EventJournalAdminPayload(entry, includeDetails: false) } });
    }

    public ResponseEnvelope JournalEventAnnotationAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        var entry = RequireJournalEntry(context, actor, playerSafe: false);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var annotation = new EventJournalAnnotationState
        {
            EntryId = entry.Id,
            CampaignId = entry.CampaignId,
            AuthorUserId = actor.Id,
            AuthorDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            Text = RequireLength(PayloadReader.GetString(payload, "text"), 1, 4096, "text"),
            IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible"),
            CreatedAtUtc = DateTime.UtcNow
        };
        if (annotation.IsPlayerVisible && !entry.IsPlayerVisible) annotation.IsPlayerVisible = false;
        _repositories.EventJournalAnnotations.Insert(annotation);
        WriteJournalAudit(actor, entry, "annotation.added", "Journal annotation added.");
        return Ok("Journal annotation added.", new Dictionary<string, object> { { "item", EventJournalAnnotationPayload(annotation, playerSafe: false) } });
    }

    public ResponseEnvelope JournalEventVisibilitySet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        var entry = RequireJournalEntry(context, actor, playerSafe: false);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        ApplyJournalVisibility(entry, NormalizeJournalVisibility(PayloadReader.GetString(payload, "visibilityMode")));
        entry.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.EventJournalEntries.Replace(entry);
        WriteJournalAudit(actor, entry, "visibility.changed", $"Journal visibility changed to {entry.VisibilityMode}.");
        return Ok("Journal visibility changed.", new Dictionary<string, object> { { "item", EventJournalAdminPayload(entry, includeDetails: false) } });
    }

    public ResponseEnvelope JournalEventArchive(CommandContext context) => SetJournalArchived(context, archived: true);
    public ResponseEnvelope JournalEventRestore(CommandContext context) => SetJournalArchived(context, archived: false);

    public ResponseEnvelope JournalEventLinkList(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        var entry = RequireJournalEntry(context, actor, playerSafe: false);
        return Ok("Journal links loaded.", new Dictionary<string, object>
        {
            { "items", VisibleLinksForEntry(entry, playerSafe: false).Select(x => (object)EventJournalLinkPayload(x, playerSafe: false)).ToArray() }
        });
    }

    public ResponseEnvelope JournalEventLinkAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        var entry = RequireJournalEntry(context, actor, playerSafe: false);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var link = AddJournalLink(
            entry,
            NormalizeJournalEntityType(PayloadReader.GetString(payload, "entityType")),
            RequireLength(PayloadReader.GetString(payload, "entityId"), 1, 128, "entityId"),
            RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "entityId")), 1, 256, "displayName"),
            NormalizeJournalLinkRole(PayloadReader.GetString(payload, "linkRole")),
            PayloadReader.GetBool(payload, "isPlayerVisible") && entry.IsPlayerVisible);
        WriteJournalAudit(actor, entry, "link.added", $"Journal link added: {link.EntityType}:{link.EntityId}");
        return Ok("Journal link added.", new Dictionary<string, object>
        {
            { "item", EventJournalLinkPayload(link, playerSafe: false) },
            { "warning", "Привязка сохранена без проверки существования справочника." }
        });
    }

    public ResponseEnvelope JournalEventLinkRemove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var linkId = RequireLength(PayloadReader.GetString(payload, "linkId"), 1, 128, "linkId");
        var link = _repositories.EventJournalLinks.GetById(linkId) ?? throw new KeyNotFoundException("Journal link not found.");
        var entry = _repositories.EventJournalEntries.GetById(link.EntryId) ?? throw new KeyNotFoundException("Journal entry not found.");
        if (!CanViewJournalEntry(actor, entry)) throw new UnauthorizedAccessException("Journal entry is forbidden.");
        link.IsArchived = true;
        _repositories.EventJournalLinks.Replace(link);
        WriteJournalAudit(actor, entry, "link.removed", $"Journal link removed: {link.EntityType}:{link.EntityId}");
        return Ok("Journal link removed.", new Dictionary<string, object> { { "linkId", link.Id } });
    }

    public ResponseEnvelope JournalEventPlayerList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        if (!EventJournalPlayerViewEnabled()) return EventJournalFeatureDisabled("Журнал событий для игроков выключен feature flags.");
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = JournalCampaignId(payload);
        var items = QueryPlayerJournalEntries(payload, campaignId)
            .Select(x => (object)EventJournalPlayerPayload(x, includeDetails: false))
            .ToArray();
        _logger.Admin($"journal.player.event.list actor={actor.Login} campaignId={campaignId} count={items.Length}");
        return Ok("Player event journal loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope JournalEventPlayerGet(CommandContext context)
    {
        GetCurrentAccount(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        if (!EventJournalPlayerViewEnabled()) return EventJournalFeatureDisabled("Журнал событий для игроков выключен feature flags.");
        var entry = RequireJournalEntry(context, actor: null, playerSafe: true);
        return Ok("Player event journal entry loaded.", EventJournalEntryEnvelope(entry, playerSafe: true));
    }

    private ResponseEnvelope SetJournalArchived(CommandContext context, bool archived)
    {
        var actor = RequireAdmin(context);
        if (!EventJournalEnabled()) return EventJournalDisabled();
        var entry = RequireJournalEntry(context, actor, playerSafe: false);
        entry.IsArchived = archived;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.EventJournalEntries.Replace(entry);
        WriteJournalAudit(actor, entry, archived ? "archived" : "restored", archived ? "Journal entry archived." : "Journal entry restored.");
        return Ok(archived ? "Journal entry archived." : "Journal entry restored.", new Dictionary<string, object> { { "item", EventJournalAdminPayload(entry, includeDetails: false) } });
    }

    private EventJournalEntryState CreateJournalEntryFromPayload(IDictionary<string, object> payload, UserAccount actor, string entryType, bool isAutomatic, bool isCorrection)
    {
        var visibility = NormalizeJournalVisibility(PayloadReader.GetString(payload, "visibilityMode"));
        var now = DateTime.UtcNow;
        var entry = new EventJournalEntryState
        {
            CampaignId = JournalCampaignId(payload),
            SessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId"),
            GroupId = RequireLength(PayloadReader.GetString(payload, "groupId"), 0, 128, "groupId"),
            CharacterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 0, 128, "characterId"),
            SourceModule = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "sourceModule"), isAutomatic ? "manual_ingest" : "gm_manual"), 0, 128, "sourceModule"),
            SourceEventType = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "sourceEventType"), entryType), 0, 128, "sourceEventType"),
            SourceEventId = RequireLength(PayloadReader.GetString(payload, "sourceEventId"), 0, 128, "sourceEventId"),
            CorrelationId = RequireLength(PayloadReader.GetString(payload, "correlationId"), 0, 128, "correlationId"),
            EntryType = entryType,
            Category = NormalizeJournalCategory(PayloadReader.GetString(payload, "category")),
            Severity = NormalizeJournalSeverity(PayloadReader.GetString(payload, "severity")),
            Title = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "title"), "Событие журнала"), 1, 240, "title"),
            Summary = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "summary"), PayloadReader.GetString(payload, "playerSummary"), PayloadReader.GetString(payload, "playerVisibleText"), "Событие записано в журнал."), 1, 2048, "summary"),
            PlayerSummary = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "playerSummary"), PayloadReader.GetString(payload, "playerVisibleText")), 0, 2048, "playerSummary"),
            GMDetails = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "gmDetails"), PayloadReader.GetString(payload, "gmOnlyDetails")), 0, 8192, "gmDetails"),
            VisibilityMode = visibility,
            IsPlayerVisible = visibility == EventJournalVisibilityModeIds.PlayerVisible,
            IsAutomatic = isAutomatic,
            IsCorrection = isCorrection,
            CorrectsEntryId = RequireLength(PayloadReader.GetString(payload, "correctsEntryId"), 0, 128, "correctsEntryId"),
            RelatedEntryId = RequireLength(PayloadReader.GetString(payload, "relatedEntryId"), 0, 128, "relatedEntryId"),
            ActorUserId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "actorUserId"), actor.Id), 0, 128, "actorUserId"),
            ActorDisplayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "actorDisplayName"), actor.Login, actor.Id), 0, 160, "actorDisplayName"),
            SubjectEntityType = RequireLength(PayloadReader.GetString(payload, "subjectEntityType"), 0, 128, "subjectEntityType"),
            SubjectEntityId = RequireLength(PayloadReader.GetString(payload, "subjectEntityId"), 0, 128, "subjectEntityId"),
            SubjectDisplayName = RequireLength(PayloadReader.GetString(payload, "subjectDisplayName"), 0, 256, "subjectDisplayName"),
            OccurredAtUtc = ReadDateTime(payload, "occurredAtUtc") ?? now,
            WorldDateTimeSnapshot = RequireLength(PayloadReader.GetString(payload, "worldDateTimeSnapshot"), 0, 256, "worldDateTimeSnapshot"),
            CreatedAtUtc = now,
            CreatedByUserId = actor.Id,
            UpdatedAtUtc = now,
            Tags = JournalTags(payload),
            MetadataSummary = RequireLength(PayloadReader.GetString(payload, "metadataSummary"), 0, 2048, "metadataSummary")
        };
        return entry;
    }

    private void InsertJournalEntry(UserAccount actor, EventJournalEntryState entry, string auditAction)
    {
        entry.SequenceNumber = NextJournalSequence(entry.CampaignId);
        _repositories.EventJournalEntries.Insert(entry);
        WriteAudit("event_journal", actor.Id, $"journal.event.{auditAction}", entry.Id);
        WriteJournalAudit(actor, entry, auditAction, $"Journal entry {auditAction}.");
        if (!string.IsNullOrWhiteSpace(entry.SubjectEntityId))
        {
            AddJournalLink(entry, NormalizeJournalEntityType(entry.SubjectEntityType), entry.SubjectEntityId, FirstNonEmpty(entry.SubjectDisplayName, entry.SubjectEntityId), EventJournalLinkRoleIds.Subject, entry.IsPlayerVisible);
        }
        _logger.Admin($"journal.event.{auditAction} entryId={entry.Id} campaignId={entry.CampaignId} sequence={entry.SequenceNumber} category={entry.Category}");
    }

    private EventJournalEntityLinkState AddJournalLink(EventJournalEntryState entry, string entityType, string entityId, string displayName, string linkRole, bool isPlayerVisible)
    {
        var link = new EventJournalEntityLinkState
        {
            EntryId = entry.Id,
            CampaignId = entry.CampaignId,
            EntityType = NormalizeJournalEntityType(entityType),
            EntityId = entityId,
            DisplayName = displayName,
            LinkRole = NormalizeJournalLinkRole(linkRole),
            IsPlayerVisible = isPlayerVisible && entry.IsPlayerVisible,
            CreatedAtUtc = DateTime.UtcNow
        };
        _repositories.EventJournalLinks.Insert(link);
        return link;
    }

    private EventJournalEntryState? FindDuplicateJournalEntry(EventJournalEntryState candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.SourceModule) && !string.IsNullOrWhiteSpace(candidate.SourceEventId))
        {
            var filter = Builders<EventJournalEntryState>.Filter.Eq(x => x.SourceModule, candidate.SourceModule)
                & Builders<EventJournalEntryState>.Filter.Eq(x => x.SourceEventId, candidate.SourceEventId);
            var existing = _repositories.EventJournalEntries.Find(filter).FirstOrDefault();
            if (existing != null) return existing;
        }
        if (!string.IsNullOrWhiteSpace(candidate.CorrelationId) && !string.IsNullOrWhiteSpace(candidate.SourceEventType))
        {
            var filter = Builders<EventJournalEntryState>.Filter.Eq(x => x.CorrelationId, candidate.CorrelationId)
                & Builders<EventJournalEntryState>.Filter.Eq(x => x.SourceEventType, candidate.SourceEventType);
            return _repositories.EventJournalEntries.Find(filter).FirstOrDefault();
        }
        return null;
    }

    private List<EventJournalEntryState> QueryAdminJournalEntries(UserAccount actor, IDictionary<string, object> payload, string campaignId)
    {
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var filter = Builders<EventJournalEntryState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!includeArchived) filter &= Builders<EventJournalEntryState>.Filter.Eq(x => x.IsArchived, false);
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var groupId = RequireLength(PayloadReader.GetString(payload, "groupId"), 0, 128, "groupId");
        var category = RequireLength(PayloadReader.GetString(payload, "category"), 0, 64, "category");
        var sourceModule = RequireLength(PayloadReader.GetString(payload, "sourceModule"), 0, 128, "sourceModule");
        return _repositories.EventJournalEntries.Find(filter)
            .Where(x => CanViewJournalEntry(actor, x))
            .Where(x => string.IsNullOrWhiteSpace(sessionId) || x.SessionId == sessionId)
            .Where(x => string.IsNullOrWhiteSpace(groupId) || x.GroupId == groupId)
            .Where(x => string.IsNullOrWhiteSpace(category) || string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(sourceModule) || string.Equals(x.SourceModule, sourceModule, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.SequenceNumber)
            .Take(EventJournalMaxList)
            .ToList();
    }

    private List<EventJournalEntryState> QueryPlayerJournalEntries(IDictionary<string, object> payload, string campaignId)
    {
        var filter = Builders<EventJournalEntryState>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<EventJournalEntryState>.Filter.Eq(x => x.IsArchived, false)
            & Builders<EventJournalEntryState>.Filter.Eq(x => x.IsPlayerVisible, true)
            & Builders<EventJournalEntryState>.Filter.Eq(x => x.VisibilityMode, EventJournalVisibilityModeIds.PlayerVisible);
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var category = RequireLength(PayloadReader.GetString(payload, "category"), 0, 64, "category");
        var query = (PayloadReader.GetString(payload, "query") ?? string.Empty).Trim();
        return _repositories.EventJournalEntries.Find(filter)
            .Where(x => string.IsNullOrWhiteSpace(sessionId) || x.SessionId == sessionId)
            .Where(x => string.IsNullOrWhiteSpace(category) || string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))
            .Where(x => query.Length < 2 || Contains(x.Title, query) || Contains(x.PlayerSummary, query) || Contains(x.Summary, query) || Contains(x.ActorDisplayName, query) || Contains(x.SubjectDisplayName, query) || x.Tags.Any(tag => Contains(tag, query)) || VisibleLinksForEntry(x, playerSafe: true).Any(link => Contains(link.DisplayName, query)))
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(EventJournalMaxList)
            .ToList();
    }

    private EventJournalEntryState RequireJournalEntry(CommandContext context, UserAccount? actor, bool playerSafe)
    {
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var entryId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "entryId"), PayloadReader.GetString(payload, "id")), 1, 128, "entryId");
        var entry = _repositories.EventJournalEntries.GetById(entryId) ?? throw new KeyNotFoundException("Journal entry not found.");
        if (playerSafe)
        {
            if (!entry.IsPlayerVisible || entry.VisibilityMode != EventJournalVisibilityModeIds.PlayerVisible || entry.IsArchived)
                throw new KeyNotFoundException("Journal entry not found.");
        }
        else if (actor == null || !CanViewJournalEntry(actor, entry))
        {
            throw new UnauthorizedAccessException("Journal entry is forbidden.");
        }
        return entry;
    }

    private bool CanViewJournalEntry(UserAccount actor, EventJournalEntryState entry)
    {
        if (actor.Roles.Contains(UserRole.SuperAdmin)) return true;
        if (!actor.Roles.Contains(UserRole.Admin)) return entry.IsPlayerVisible && entry.VisibilityMode == EventJournalVisibilityModeIds.PlayerVisible;
        if (entry.VisibilityMode == EventJournalVisibilityModeIds.SuperAdminOnly) return false;
        return true;
    }

    private Dictionary<string, object> EventJournalEntryEnvelope(EventJournalEntryState entry, bool playerSafe)
    {
        var payload = playerSafe ? EventJournalPlayerPayload(entry, includeDetails: true) : EventJournalAdminPayload(entry, includeDetails: true);
        var annotations = LoadJournalAnnotations(entry.Id, playerSafe).Select(x => (object)EventJournalAnnotationPayload(x, playerSafe)).ToArray();
        var links = VisibleLinksForEntry(entry, playerSafe).Select(x => (object)EventJournalLinkPayload(x, playerSafe)).ToArray();
        return new Dictionary<string, object> { { "item", payload }, { "annotations", annotations }, { "links", links } };
    }

    private Dictionary<string, object> EventJournalAdminPayload(EventJournalEntryState entry, bool includeDetails) => new Dictionary<string, object>
    {
        { "entryId", entry.Id },
        { "campaignId", entry.CampaignId },
        { "sessionId", entry.SessionId },
        { "groupId", entry.GroupId },
        { "characterId", entry.CharacterId },
        { "sourceModule", entry.SourceModule },
        { "sourceEventType", entry.SourceEventType },
        { "sourceEventId", entry.SourceEventId },
        { "correlationId", entry.CorrelationId },
        { "entryType", entry.EntryType },
        { "category", entry.Category },
        { "severity", entry.Severity },
        { "title", entry.Title },
        { "summary", entry.Summary },
        { "playerSummary", entry.PlayerSummary },
        { "gmDetails", includeDetails ? entry.GMDetails : string.Empty },
        { "visibilityMode", entry.VisibilityMode },
        { "isPlayerVisible", entry.IsPlayerVisible },
        { "isAutomatic", entry.IsAutomatic },
        { "isCorrection", entry.IsCorrection },
        { "correctsEntryId", entry.CorrectsEntryId },
        { "relatedEntryId", entry.RelatedEntryId },
        { "actorUserId", entry.ActorUserId },
        { "actorDisplayName", entry.ActorDisplayName },
        { "subjectEntityType", entry.SubjectEntityType },
        { "subjectEntityId", entry.SubjectEntityId },
        { "subjectDisplayName", entry.SubjectDisplayName },
        { "occurredAtUtc", entry.OccurredAtUtc },
        { "worldDateTimeSnapshot", entry.WorldDateTimeSnapshot },
        { "sequenceNumber", entry.SequenceNumber },
        { "isArchived", entry.IsArchived },
        { "createdAtUtc", entry.CreatedAtUtc },
        { "updatedAtUtc", entry.UpdatedAtUtc },
        { "tags", entry.Tags.Cast<object>().ToArray() },
        { "metadataSummary", entry.MetadataSummary }
    };

    private Dictionary<string, object> EventJournalPlayerPayload(EventJournalEntryState entry, bool includeDetails) => new Dictionary<string, object>
    {
        { "entryId", entry.Id },
        { "category", entry.Category },
        { "severity", entry.Severity },
        { "title", entry.Title },
        { "summary", FirstNonEmpty(entry.PlayerSummary, entry.Summary) },
        { "actorDisplayName", entry.ActorDisplayName },
        { "subjectDisplayName", entry.SubjectDisplayName },
        { "occurredAtUtc", entry.OccurredAtUtc },
        { "worldDateTimeSnapshot", entry.WorldDateTimeSnapshot },
        { "tags", entry.Tags.Cast<object>().ToArray() }
    };

    private Dictionary<string, object> EventJournalLinkPayload(EventJournalEntityLinkState link, bool playerSafe)
    {
        var payload = new Dictionary<string, object>
        {
            { "entityType", link.EntityType },
            { "displayName", link.DisplayName },
            { "linkRole", link.LinkRole },
            { "isPlayerVisible", link.IsPlayerVisible },
            { "createdAtUtc", link.CreatedAtUtc }
        };
        if (!playerSafe)
        {
            payload["linkId"] = link.Id;
            payload["entryId"] = link.EntryId;
            payload["entityId"] = link.EntityId;
        }
        return payload;
    }

    private Dictionary<string, object> EventJournalAnnotationPayload(EventJournalAnnotationState annotation, bool playerSafe)
    {
        var payload = new Dictionary<string, object>
        {
            { "authorDisplayName", annotation.AuthorDisplayName },
            { "text", annotation.Text },
            { "isPlayerVisible", annotation.IsPlayerVisible },
            { "createdAtUtc", annotation.CreatedAtUtc },
            { "updatedAtUtc", annotation.UpdatedAtUtc.HasValue ? (object)annotation.UpdatedAtUtc.Value : string.Empty }
        };
        if (!playerSafe)
        {
            payload["annotationId"] = annotation.Id;
            payload["entryId"] = annotation.EntryId;
        }
        return payload;
    }

    private List<EventJournalEntityLinkState> VisibleLinksForEntry(EventJournalEntryState entry, bool playerSafe)
    {
        var filter = Builders<EventJournalEntityLinkState>.Filter.Eq(x => x.EntryId, entry.Id)
            & Builders<EventJournalEntityLinkState>.Filter.Eq(x => x.IsArchived, false);
        return _repositories.EventJournalLinks.Find(filter)
            .Where(x => !playerSafe || x.IsPlayerVisible)
            .OrderBy(x => x.LinkRole)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<EventJournalAnnotationState> LoadJournalAnnotations(string entryId, bool playerSafe)
    {
        var filter = Builders<EventJournalAnnotationState>.Filter.Eq(x => x.EntryId, entryId)
            & Builders<EventJournalAnnotationState>.Filter.Eq(x => x.IsArchived, false);
        return _repositories.EventJournalAnnotations.Find(filter)
            .Where(x => !playerSafe || x.IsPlayerVisible)
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();
    }

    private void WriteJournalAudit(UserAccount actor, EventJournalEntryState entry, string action, string summary)
    {
        _repositories.EventJournalAudit.Insert(new EventJournalAuditEntry
        {
            CampaignId = entry.CampaignId,
            EntryId = entry.Id,
            ActionType = action,
            PerformedByUserId = actor.Id,
            Summary = RequireLength(summary, 0, 512, "summary"),
            PerformedAtUtc = DateTime.UtcNow
        });
    }

    private long NextJournalSequence(string campaignId)
    {
        var filter = Builders<EventJournalEntryState>.Filter.Eq(x => x.CampaignId, campaignId);
        var latest = _repositories.EventJournalEntries.Find(filter).OrderByDescending(x => x.SequenceNumber).FirstOrDefault();
        return (latest?.SequenceNumber ?? 0L) + 1L;
    }

    private static void ApplyJournalVisibility(EventJournalEntryState entry, string visibility)
    {
        entry.VisibilityMode = visibility;
        entry.IsPlayerVisible = visibility == EventJournalVisibilityModeIds.PlayerVisible;
    }

    private static DateTime? ReadDateTime(IDictionary<string, object> payload, string key)
    {
        var raw = PayloadReader.GetString(payload, key);
        return DateTime.TryParse(raw, out var parsed) ? parsed.ToUniversalTime() : (DateTime?)null;
    }

    private static string JournalCampaignId(IDictionary<string, object> payload)
        => RequireLength(PayloadReader.GetString(payload, "campaignId") ?? "default", 1, 128, "campaignId");

    private static string NormalizeJournalCategory(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            EventJournalCategoryIds.Session, EventJournalCategoryIds.Character, EventJournalCategoryIds.Ownership,
            EventJournalCategoryIds.Group, EventJournalCategoryIds.Request, EventJournalCategoryIds.Combat,
            EventJournalCategoryIds.Map, EventJournalCategoryIds.WorldCalendar, EventJournalCategoryIds.RealSchedule,
            EventJournalCategoryIds.GMNote, EventJournalCategoryIds.Inventory, EventJournalCategoryIds.System,
            EventJournalCategoryIds.Custom
        };
        return allowed.Contains(text) ? text : EventJournalCategoryIds.Custom;
    }

    private static string NormalizeJournalSeverity(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            EventJournalSeverityIds.Information, EventJournalSeverityIds.Notice, EventJournalSeverityIds.Important,
            EventJournalSeverityIds.Warning, EventJournalSeverityIds.Critical
        };
        return allowed.Contains(text) ? text : EventJournalSeverityIds.Information;
    }

    private static string NormalizeJournalVisibility(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw)) return EventJournalVisibilityModeIds.GMOnly;
        var text = raw.ToLowerInvariant();
        if (text == EventJournalVisibilityModeIds.PlayerVisible) return EventJournalVisibilityModeIds.PlayerVisible;
        if (text == EventJournalVisibilityModeIds.GMTeam) return EventJournalVisibilityModeIds.GMTeam;
        if (text == EventJournalVisibilityModeIds.SuperAdminOnly) return EventJournalVisibilityModeIds.SuperAdminOnly;
        if (text == EventJournalVisibilityModeIds.GMOnly) return EventJournalVisibilityModeIds.GMOnly;
        throw new ArgumentException("Invalid journal visibilityMode.");
    }

    private static string NormalizeJournalEntityType(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            EventJournalEntityTypeIds.CurrentSession, EventJournalEntityTypeIds.Session, EventJournalEntityTypeIds.Character,
            EventJournalEntityTypeIds.Npc, EventJournalEntityTypeIds.Companion, EventJournalEntityTypeIds.CharacterGroup,
            EventJournalEntityTypeIds.PlayerRequest, EventJournalEntityTypeIds.WorldCalendarEvent,
            EventJournalEntityTypeIds.RealScheduleEvent, EventJournalEntityTypeIds.SceneMap, EventJournalEntityTypeIds.WorldMap,
            EventJournalEntityTypeIds.Room, EventJournalEntityTypeIds.MapMarker, EventJournalEntityTypeIds.CombatEncounter,
            EventJournalEntityTypeIds.Location, EventJournalEntityTypeIds.Country, EventJournalEntityTypeIds.Region,
            EventJournalEntityTypeIds.Faction, EventJournalEntityTypeIds.Organization, EventJournalEntityTypeIds.GMNote,
            EventJournalEntityTypeIds.Custom
        };
        return allowed.Contains(text) ? text : EventJournalEntityTypeIds.Custom;
    }

    private static string NormalizeJournalLinkRole(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            EventJournalLinkRoleIds.Actor, EventJournalLinkRoleIds.Subject, EventJournalLinkRoleIds.Source,
            EventJournalLinkRoleIds.Target, EventJournalLinkRoleIds.Related, EventJournalLinkRoleIds.Location,
            EventJournalLinkRoleIds.Result, EventJournalLinkRoleIds.CorrectionOf, EventJournalLinkRoleIds.Custom
        };
        return allowed.Contains(text) ? text : EventJournalLinkRoleIds.Related;
    }

    private static List<string> JournalTags(IDictionary<string, object> payload)
    {
        var result = new List<string>();
        var list = PayloadReader.GetList(payload, "tags");
        if (list != null)
        {
            foreach (var item in list) AddJournalTag(result, Convert.ToString(item));
        }
        var text = PayloadReader.GetString(payload, "tagsText");
        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var part in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                AddJournalTag(result, part);
        }
        return result.Take(32).ToList();
    }

    private static void AddJournalTag(List<string> result, string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(value) && !result.Contains(value, StringComparer.OrdinalIgnoreCase))
            result.Add(value);
    }

    private static bool JournalContains(EventJournalEntryState entry, string query)
        => Contains(entry.Title, query) || Contains(entry.Summary, query) || Contains(entry.PlayerSummary, query)
            || Contains(entry.ActorDisplayName, query) || Contains(entry.SubjectDisplayName, query)
            || entry.Tags.Any(tag => Contains(tag, query));

    private bool EventJournalEnabled() => _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp));
    private bool EventJournalAutomaticIngestionEnabled() => EventJournalEnabled() && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion));
    private bool EventJournalManualEntriesEnabled() => EventJournalEnabled() && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalManualEntries));
    private bool EventJournalPlayerViewEnabled() => EventJournalEnabled() && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalPlayerView));
    private bool EventJournalFiltersEnabled() => EventJournalEnabled() && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalFilters));
    private bool EventJournalCorrectionsEnabled() => EventJournalEnabled() && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalCorrections));
    private static ResponseEnvelope EventJournalDisabled() => EventJournalFeatureDisabled("Журнал событий пока недоступен.");
    private static ResponseEnvelope EventJournalFeatureDisabled(string message) => Error(message, ResponseStatus.Forbidden, ErrorCode.Forbidden);
}
