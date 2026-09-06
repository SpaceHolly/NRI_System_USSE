using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope KnowledgeDefinitionList(CommandContext context)
    {
        RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var campaignId = SafeText(PayloadReader.GetString(context.Request.Payload, "campaignId"), 128);
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var filter = FilterDefinition<KnowledgeDefinition>.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<KnowledgeDefinition>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!includeArchived) filter &= Builders<KnowledgeDefinition>.Filter.Eq(x => x.IsArchived, false);
        var items = _repositories.KnowledgeDefinitions.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(500)
            .Select(x => (object)KnowledgeDefinitionPayload(x, includeAdmin: true))
            .ToArray();
        return Ok("Knowledge definitions loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope KnowledgeDefinitionGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var item = RequireKnowledgeDefinition(context);
        return Ok("Knowledge definition loaded.", new Dictionary<string, object> { { "item", KnowledgeDefinitionPayload(item, includeAdmin: true) } });
    }

    public ResponseEnvelope KnowledgeDefinitionCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var item = new KnowledgeDefinition
        {
            CampaignId = SafeText(PayloadReader.GetString(context.Request.Payload, "campaignId"), 128),
            RuleSetId = SafeText(PayloadReader.GetString(context.Request.Payload, "ruleSetId"), 128),
            KnowledgeId = SafeText(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "knowledgeId"), Guid.NewGuid().ToString("N")), 128),
            Category = SafeText(PayloadReader.GetString(context.Request.Payload, "category"), 128),
            Subcategory = SafeText(PayloadReader.GetString(context.Request.Payload, "subcategory"), 128),
            Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), "Knowledge"), 1, 180, "name"),
            ShortName = SafeText(PayloadReader.GetString(context.Request.Payload, "shortName"), 80),
            PublicDescription = SafeText(PayloadReader.GetString(context.Request.Payload, "publicDescription"), 4096),
            GMDescription = SafeText(PayloadReader.GetString(context.Request.Payload, "gmDescription"), 8192),
            TruthDescription = SafeText(PayloadReader.GetString(context.Request.Payload, "truthDescription"), 8192),
            OfficialDescription = SafeText(PayloadReader.GetString(context.Request.Payload, "officialDescription"), 4096),
            KnowledgeType = NormalizeKnowledgeType(PayloadReader.GetString(context.Request.Payload, "knowledgeType")),
            KnowledgeDomain = NormalizeKnowledgeDomain(PayloadReader.GetString(context.Request.Payload, "knowledgeDomain")),
            DefaultVisibilityRule = NormalizeKnowledgeVisibilityRule(PayloadReader.GetString(context.Request.Payload, "defaultVisibilityRule")),
            IsAppliedKnowledge = PayloadReader.GetBool(context.Request.Payload, "isAppliedKnowledge"),
            IsSecret = PayloadReader.GetBool(context.Request.Payload, "isSecret"),
            IsPlayerDiscoverable = !context.Request.Payload.ContainsKey("isPlayerDiscoverable") || PayloadReader.GetBool(context.Request.Payload, "isPlayerDiscoverable"),
            SourceDocument = SafeText(PayloadReader.GetString(context.Request.Payload, "sourceDocument"), 512),
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };
        _repositories.KnowledgeDefinitions.Insert(item);
        TryPublishKnowledgeSync(item.CampaignId, "knowledge.definition.created", "knowledge_definition", item.Id, actor.Id, context.Request.RequestId ?? string.Empty);
        TryWriteKnowledgeJournal(item.CampaignId, "knowledge.definition.created:" + item.Id, "Knowledge definition created", item.Name, item.PublicDescription, actor.Id, item.IsPlayerDiscoverable && !item.IsSecret);
        _logger.Admin($"knowledge.definition.create actor={actor.Login} id={item.Id}");
        return Ok("Knowledge definition created.", new Dictionary<string, object> { { "item", KnowledgeDefinitionPayload(item, includeAdmin: true) } });
    }

    public ResponseEnvelope KnowledgeDefinitionUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var item = RequireKnowledgeDefinition(context);
        item.Category = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "category"), 128), item.Category);
        item.Subcategory = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "subcategory"), 128), item.Subcategory);
        item.Name = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "name"), 180), item.Name);
        item.ShortName = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "shortName"), 80), item.ShortName);
        item.PublicDescription = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "publicDescription"), 4096), item.PublicDescription);
        item.GMDescription = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "gmDescription"), 8192), item.GMDescription);
        item.TruthDescription = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "truthDescription"), 8192), item.TruthDescription);
        item.OfficialDescription = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "officialDescription"), 4096), item.OfficialDescription);
        item.KnowledgeType = FirstNonEmpty(NormalizeKnowledgeType(PayloadReader.GetString(context.Request.Payload, "knowledgeType"), true), item.KnowledgeType);
        item.KnowledgeDomain = FirstNonEmpty(NormalizeKnowledgeDomain(PayloadReader.GetString(context.Request.Payload, "knowledgeDomain"), true), item.KnowledgeDomain);
        item.DefaultVisibilityRule = FirstNonEmpty(NormalizeKnowledgeVisibilityRule(PayloadReader.GetString(context.Request.Payload, "defaultVisibilityRule"), true), item.DefaultVisibilityRule);
        if (context.Request.Payload.ContainsKey("isAppliedKnowledge")) item.IsAppliedKnowledge = PayloadReader.GetBool(context.Request.Payload, "isAppliedKnowledge");
        if (context.Request.Payload.ContainsKey("isSecret")) item.IsSecret = PayloadReader.GetBool(context.Request.Payload, "isSecret");
        if (context.Request.Payload.ContainsKey("isPlayerDiscoverable")) item.IsPlayerDiscoverable = PayloadReader.GetBool(context.Request.Payload, "isPlayerDiscoverable");
        item.SourceDocument = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "sourceDocument"), 512), item.SourceDocument);
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.KnowledgeDefinitions.Replace(item);
        TryPublishKnowledgeSync(item.CampaignId, "knowledge.definition.updated", "knowledge_definition", item.Id, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Knowledge definition updated.", new Dictionary<string, object> { { "item", KnowledgeDefinitionPayload(item, includeAdmin: true) } });
    }

    public ResponseEnvelope KnowledgeDefinitionArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var item = RequireKnowledgeDefinition(context);
        item.IsArchived = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.KnowledgeDefinitions.Replace(item);
        TryPublishKnowledgeSync(item.CampaignId, "knowledge.definition.archived", "knowledge_definition", item.Id, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Knowledge definition archived.", new Dictionary<string, object> { { "knowledgeDefinitionId", item.Id } });
    }

    public ResponseEnvelope EntityKnowledgeList(CommandContext context)
    {
        RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var filter = EntityKnowledgeFilter(context.Request.Payload, playerSafe: false, actor: null);
        var items = _repositories.EntityKnowledgeStates.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(500)
            .Select(x => (object)EntityKnowledgePayload(x, includeAdmin: true))
            .ToArray();
        return Ok("Entity knowledge loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope EntityKnowledgeGrant(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var definition = RequireKnowledgeDefinition(context);
        var entityType = NormalizeKnowledgeEntityType(PayloadReader.GetString(context.Request.Payload, "entityType"));
        var entityId = RequireLength(PayloadReader.GetString(context.Request.Payload, "entityId"), 1, 128, "entityId");
        var profileTopic = ResolveCharacterKnowledgeTopicForGrant0194(
            entityType,
            PayloadReader.GetString(context.Request.Payload, "profileTopic"));
        if (!string.IsNullOrWhiteSpace(profileTopic))
        {
            var profileWrite = _profileNativeWriteService.UnlockKnowledgeTopicProfileNativeAsync(
                    entityId,
                    profileTopic,
                    actor.Id,
                    context.Request.RequestId ?? string.Empty)
                .GetAwaiter()
                .GetResult();
            if (!profileWrite.ProfileWritten || !profileWrite.UsedProfileNative)
                throw new InvalidOperationException("Character v2 knowledge profile write failed: " + profileWrite.ErrorMessage);
        }
        var item = new EntityKnowledgeState
        {
            CampaignId = definition.CampaignId,
            KnowledgeDefinitionId = definition.Id,
            KnowledgeId = definition.KnowledgeId,
            EntityType = entityType,
            EntityId = entityId,
            EntityDisplayName = SafeText(PayloadReader.GetString(context.Request.Payload, "entityDisplayName"), 180),
            OwnerUserId = SafeText(PayloadReader.GetString(context.Request.Payload, "ownerUserId"), 128),
            Level = NormalizeKnowledgeLevel(PayloadReader.GetString(context.Request.Payload, "level")),
            TruthRelation = NormalizeTruthRelation(PayloadReader.GetString(context.Request.Payload, "truthRelation")),
            PlayerSummary = SafeText(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "playerSummary"), definition.PublicDescription), 4096),
            GMNotes = SafeText(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 8192),
            FalseOrOutdatedSummary = SafeText(PayloadReader.GetString(context.Request.Payload, "falseOrOutdatedSummary"), 4096),
            IsApplied = PayloadReader.GetBool(context.Request.Payload, "isApplied"),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode"), allowEmpty: true),
            SourceId = SafeText(PayloadReader.GetString(context.Request.Payload, "sourceId"), 128),
            SourceLabel = SafeText(PayloadReader.GetString(context.Request.Payload, "sourceLabel"), 256),
            GrantedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };
        if (!string.IsNullOrWhiteSpace(profileTopic))
            item.ExtraData["characterProfileTopic"] = profileTopic;
        if (string.IsNullOrWhiteSpace(item.VisibilityMode)) item.VisibilityMode = item.IsPlayerVisible ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly;
        _repositories.EntityKnowledgeStates.Insert(item);
        TryPublishKnowledgeSync(item.CampaignId, "knowledge.entity.granted", "entity_knowledge", item.Id, actor.Id, context.Request.RequestId ?? string.Empty);
        TryWriteKnowledgeJournal(item.CampaignId, "knowledge.entity.granted:" + item.Id, "Knowledge granted", definition.Name, item.PlayerSummary, actor.Id, item.IsPlayerVisible);
        return Ok("Knowledge granted.", new Dictionary<string, object> { { "item", EntityKnowledgePayload(item, includeAdmin: true) } });
    }

    private string ResolveCharacterKnowledgeTopicForGrant0194(string entityType, string? requestedTopic)
    {
        requestedTopic = (requestedTopic ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(requestedTopic))
            return string.Empty;
        if (!string.Equals(entityType, KnowledgeEntityTypeIds.Character, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("profileTopic is supported only for Character v2 knowledge grants.");

        var technology = _mongo.ContentDefinitionRecords.Find(
                Builders<ContentDefinitionRecord>.Filter.Eq(x => x.IsArchived, false)
                & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, TechnologyRecipeBlueprintProjectDefinitionCategories.Technology)
                & (Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, requestedTopic)
                   | Builders<ContentDefinitionRecord>.Filter.Eq(x => x.StableKey, requestedTopic)))
            .FirstOrDefault();
        if (technology == null)
            throw new KeyNotFoundException("Canonical technology for Character v2 knowledge grant was not found.");

        return FirstNonEmpty(technology.StableKey, technology.Id);
    }

    public ResponseEnvelope EntityKnowledgeUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var item = RequireEntityKnowledge(context);
        UpdateEntityKnowledgeFromPayload(item, context.Request.Payload, actor.Id);
        _repositories.EntityKnowledgeStates.Replace(item);
        TryPublishKnowledgeSync(item.CampaignId, "knowledge.entity.updated", "entity_knowledge", item.Id, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Entity knowledge updated.", new Dictionary<string, object> { { "item", EntityKnowledgePayload(item, includeAdmin: true) } });
    }

    public ResponseEnvelope EntityKnowledgeReveal(CommandContext context) => SetEntityKnowledgeVisibility(context, true, ProjectVisibilityModeIds.PlayerVisible, "Knowledge revealed.");
    public ResponseEnvelope EntityKnowledgeHide(CommandContext context) => SetEntityKnowledgeVisibility(context, false, ProjectVisibilityModeIds.GmOnly, "Knowledge hidden.");

    public ResponseEnvelope EntityKnowledgeCorrect(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var item = RequireEntityKnowledge(context);
        item.Level = NormalizeKnowledgeLevel(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "level"), KnowledgeLevelIds.Partial));
        item.TruthRelation = NormalizeTruthRelation(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "truthRelation"), KnowledgeTruthRelationIds.Partial));
        item.PlayerSummary = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "playerSummary"), 4096), item.PlayerSummary);
        item.FalseOrOutdatedSummary = SafeText(PayloadReader.GetString(context.Request.Payload, "falseOrOutdatedSummary"), 4096);
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.EntityKnowledgeStates.Replace(item);
        TryPublishKnowledgeSync(item.CampaignId, "knowledge.entity.corrected", "entity_knowledge", item.Id, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Knowledge corrected.", new Dictionary<string, object> { { "item", EntityKnowledgePayload(item, includeAdmin: true) } });
    }

    public ResponseEnvelope EntityKnowledgeArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var item = RequireEntityKnowledge(context);
        item.IsArchived = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.EntityKnowledgeStates.Replace(item);
        return Ok("Entity knowledge archived.", new Dictionary<string, object> { { "entityKnowledgeId", item.Id } });
    }

    public ResponseEnvelope KnowledgeSourceList(CommandContext context)
    {
        RequireAdmin(context);
        if (!KnowledgeSourcesEnabled()) return KnowledgeDisabled();
        var campaignId = SafeText(PayloadReader.GetString(context.Request.Payload, "campaignId"), 128);
        var knowledgeDefinitionId = SafeText(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "knowledgeDefinitionId"), PayloadReader.GetString(context.Request.Payload, "definitionId")), 128);
        var entityKnowledgeId = SafeText(PayloadReader.GetString(context.Request.Payload, "entityKnowledgeId"), 128);
        var filter = FilterDefinition<KnowledgeSourceState>.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<KnowledgeSourceState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(knowledgeDefinitionId)) filter &= Builders<KnowledgeSourceState>.Filter.Eq(x => x.KnowledgeDefinitionId, knowledgeDefinitionId);
        if (!string.IsNullOrWhiteSpace(entityKnowledgeId)) filter &= Builders<KnowledgeSourceState>.Filter.Eq(x => x.EntityKnowledgeId, entityKnowledgeId);
        filter &= Builders<KnowledgeSourceState>.Filter.Eq(x => x.IsArchived, false);
        var items = _repositories.KnowledgeSources.Find(filter).OrderByDescending(x => x.CreatedAtUtc).Take(300).Select(x => (object)KnowledgeSourcePayload(x, true)).ToArray();
        return Ok("Knowledge sources loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope KnowledgeSourceAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!KnowledgeSourcesEnabled()) return KnowledgeDisabled();
        var item = new KnowledgeSourceState
        {
            CampaignId = SafeText(PayloadReader.GetString(context.Request.Payload, "campaignId"), 128),
            KnowledgeDefinitionId = SafeText(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "knowledgeDefinitionId"), PayloadReader.GetString(context.Request.Payload, "definitionId")), 128),
            EntityKnowledgeId = SafeText(PayloadReader.GetString(context.Request.Payload, "entityKnowledgeId"), 128),
            SourceType = NormalizeKnowledgeSourceType(PayloadReader.GetString(context.Request.Payload, "sourceType")),
            Title = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "title"), "Knowledge source"), 1, 180, "title"),
            PublicSummary = SafeText(PayloadReader.GetString(context.Request.Payload, "publicSummary"), 2048),
            GMSummary = SafeText(PayloadReader.GetString(context.Request.Payload, "gmSummary"), 4096),
            LinkedEntityType = SafeText(PayloadReader.GetString(context.Request.Payload, "linkedEntityType"), 128),
            LinkedEntityId = SafeText(PayloadReader.GetString(context.Request.Payload, "linkedEntityId"), 128),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode"), true),
            CreatedByUserId = actor.Id
        };
        if (string.IsNullOrWhiteSpace(item.VisibilityMode)) item.VisibilityMode = item.IsPlayerVisible ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly;
        _repositories.KnowledgeSources.Insert(item);
        return Ok("Knowledge source added.", new Dictionary<string, object> { { "item", KnowledgeSourcePayload(item, true) } });
    }

    public ResponseEnvelope AppliedKnowledgeList(CommandContext context)
    {
        RequireAdmin(context);
        if (!AppliedKnowledgeEnabled()) return KnowledgeDisabled();
        var campaignId = SafeText(PayloadReader.GetString(context.Request.Payload, "campaignId"), 128);
        var filter = FilterDefinition<AppliedKnowledgeDefinition>.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<AppliedKnowledgeDefinition>.Filter.Eq(x => x.CampaignId, campaignId);
        filter &= Builders<AppliedKnowledgeDefinition>.Filter.Eq(x => x.IsArchived, false);
        var items = _repositories.AppliedKnowledgeDefinitions.Find(filter).OrderByDescending(x => x.UpdatedAtUtc).Take(300).Select(x => (object)AppliedKnowledgePayload(x, true)).ToArray();
        return Ok("Applied knowledge loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope AppliedKnowledgeCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!AppliedKnowledgeEnabled()) return KnowledgeDisabled();
        var item = new AppliedKnowledgeDefinition
        {
            CampaignId = SafeText(PayloadReader.GetString(context.Request.Payload, "campaignId"), 128),
            KnowledgeDefinitionId = SafeText(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "knowledgeDefinitionId"), PayloadReader.GetString(context.Request.Payload, "definitionId")), 128),
            AppliedType = NormalizeAppliedKnowledgeType(PayloadReader.GetString(context.Request.Payload, "appliedType")),
            Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "name"), "Applied knowledge"), 1, 180, "name"),
            PublicDescription = SafeText(PayloadReader.GetString(context.Request.Payload, "publicDescription"), 4096),
            GMDescription = SafeText(PayloadReader.GetString(context.Request.Payload, "gmDescription"), 8192),
            RecipeReferenceId = SafeText(PayloadReader.GetString(context.Request.Payload, "recipeReferenceId"), 128),
            BlueprintReferenceId = SafeText(PayloadReader.GetString(context.Request.Payload, "blueprintReferenceId"), 128),
            FutureSystemBoundary = SafeText(PayloadReader.GetString(context.Request.Payload, "futureSystemBoundary"), 256),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode"), true),
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };
        if (string.IsNullOrWhiteSpace(item.VisibilityMode)) item.VisibilityMode = item.IsPlayerVisible ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly;
        _repositories.AppliedKnowledgeDefinitions.Insert(item);
        return Ok("Applied knowledge created.", new Dictionary<string, object> { { "item", AppliedKnowledgePayload(item, true) } });
    }

    public ResponseEnvelope AppliedKnowledgeUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!AppliedKnowledgeEnabled()) return KnowledgeDisabled();
        var item = RequireAppliedKnowledge(context);
        item.Name = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "name"), 180), item.Name);
        item.PublicDescription = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "publicDescription"), 4096), item.PublicDescription);
        item.GMDescription = FirstNonEmpty(SafeText(PayloadReader.GetString(context.Request.Payload, "gmDescription"), 8192), item.GMDescription);
        item.AppliedType = FirstNonEmpty(NormalizeAppliedKnowledgeType(PayloadReader.GetString(context.Request.Payload, "appliedType"), true), item.AppliedType);
        if (context.Request.Payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible");
        item.VisibilityMode = FirstNonEmpty(NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode"), true), item.VisibilityMode);
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.AppliedKnowledgeDefinitions.Replace(item);
        return Ok("Applied knowledge updated.", new Dictionary<string, object> { { "item", AppliedKnowledgePayload(item, true) } });
    }

    public ResponseEnvelope AppliedKnowledgeArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!AppliedKnowledgeEnabled()) return KnowledgeDisabled();
        var item = RequireAppliedKnowledge(context);
        item.IsArchived = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.AppliedKnowledgeDefinitions.Replace(item);
        return Ok("Applied knowledge archived.", new Dictionary<string, object> { { "appliedKnowledgeId", item.Id } });
    }

    public ResponseEnvelope ResearchList(CommandContext context)
    {
        RequireAdmin(context);
        if (!ResearchAdminEnabled()) return KnowledgeDisabled();
        var payload = new Dictionary<string, object>(context.Request.Payload ?? new Dictionary<string, object>())
        {
            ["projectType"] = ProjectTypeIds.Research
        };
        context.Request.Payload = payload;
        return ProjectList(context);
    }

    public ResponseEnvelope ResearchGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!ResearchAdminEnabled()) return KnowledgeDisabled();
        var project = RequireResearchProject(context);
        return Ok("Research loaded.", new Dictionary<string, object>
        {
            { "item", ProjectPayload(project, true, true) },
            { "results", ResearchResultsPayload(project.Id, true) }
        });
    }

    public ResponseEnvelope ResearchCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ResearchAdminEnabled()) return KnowledgeDisabled();
        var project = new ProjectBaseState
        {
            CampaignId = SafeText(PayloadReader.GetString(context.Request.Payload, "campaignId"), 128),
            RuleSetId = SafeText(PayloadReader.GetString(context.Request.Payload, "ruleSetId"), 128),
            SessionId = SafeText(PayloadReader.GetString(context.Request.Payload, "sessionId"), 128),
            ActiveGroupId = SafeText(PayloadReader.GetString(context.Request.Payload, "activeGroupId"), 128),
            ProjectType = ProjectTypeIds.Research,
            Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "topic"), PayloadReader.GetString(context.Request.Payload, "name"), "Research"), 1, 180, "topic"),
            PublicSummary = SafeText(PayloadReader.GetString(context.Request.Payload, "publicSummary"), 2048),
            GMSummary = SafeText(PayloadReader.GetString(context.Request.Payload, "gmSummary"), 4096),
            Status = NormalizeProjectStatus(PayloadReader.GetString(context.Request.Payload, "status"), true),
            ApprovalStatus = ProjectApprovalStatusIds.Approved,
            ProgressMode = ProjectProgressModeIds.Manual,
            OwnerUserId = SafeText(PayloadReader.GetString(context.Request.Payload, "ownerUserId"), 128),
            OwnerCharacterId = SafeText(PayloadReader.GetString(context.Request.Payload, "ownerCharacterId"), 128),
            OwnerDisplayName = SafeText(PayloadReader.GetString(context.Request.Payload, "ownerDisplayName"), 180),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode"), true),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            PublicNotes = SafeText(PayloadReader.GetString(context.Request.Payload, "publicNotes"), 2048),
            GMNotes = SafeText(PayloadReader.GetString(context.Request.Payload, "gmNotes"), 4096)
        };
        if (string.IsNullOrWhiteSpace(project.Status)) project.Status = ProjectStatusIds.Active;
        if (string.IsNullOrWhiteSpace(project.VisibilityMode)) project.VisibilityMode = project.IsPlayerVisible ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly;
        project.ProposalPayload["researchType"] = NormalizeResearchType(PayloadReader.GetString(context.Request.Payload, "researchType"));
        project.ProposalPayload["researchDomain"] = SafeText(PayloadReader.GetString(context.Request.Payload, "researchDomain"), 128);
        project.ProposalPayload["objective"] = SafeText(PayloadReader.GetString(context.Request.Payload, "objective"), 2048);
        _repositories.Projects.Insert(project);
        AddProjectAudit(project, actor.Id, "research.created", "Research created.", project.PublicSummary, project.IsPlayerVisible);
        TryPublishProjectSync(project, "research.created", actor.Id, context.Request.RequestId ?? string.Empty);
        TryWriteResearchJournal(project, "research.created", "Research created", actor.Id);
        return Ok("Research created.", new Dictionary<string, object> { { "item", ProjectPayload(project, true, true) } });
    }

    public ResponseEnvelope ResearchUpdate(CommandContext context)
    {
        if (!ResearchAdminEnabled()) return KnowledgeDisabled();
        return ProjectUpdate(context);
    }

    public ResponseEnvelope ResearchProgressAdd(CommandContext context)
    {
        if (!ResearchAdminEnabled()) return KnowledgeDisabled();
        return ProjectProgressAdd(context);
    }

    public ResponseEnvelope ResearchResultPrepare(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ResearchResultsEnabled()) return KnowledgeDisabled();
        var project = RequireResearchProject(context);
        var result = new ResearchResultState
        {
            CampaignId = project.CampaignId,
            ProjectId = project.Id,
            ResultType = NormalizeResearchResultType(PayloadReader.GetString(context.Request.Payload, "resultType")),
            Status = ResearchResultStatusIds.Prepared,
            Title = RequireLength(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "title"), "Research result"), 1, 180, "title"),
            PublicSummary = SafeText(PayloadReader.GetString(context.Request.Payload, "publicSummary"), 4096),
            GMSummary = SafeText(PayloadReader.GetString(context.Request.Payload, "gmSummary"), 8192),
            KnowledgeDefinitionId = SafeText(PayloadReader.GetString(context.Request.Payload, "knowledgeDefinitionId"), 128),
            AppliedKnowledgeId = SafeText(PayloadReader.GetString(context.Request.Payload, "appliedKnowledgeId"), 128),
            TargetEntityType = NormalizeKnowledgeEntityType(PayloadReader.GetString(context.Request.Payload, "targetEntityType")),
            TargetEntityId = SafeText(PayloadReader.GetString(context.Request.Payload, "targetEntityId"), 128),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            VisibilityMode = NormalizeProjectVisibility(PayloadReader.GetString(context.Request.Payload, "visibilityMode"), true),
            PreparedByUserId = actor.Id
        };
        if (string.IsNullOrWhiteSpace(result.VisibilityMode)) result.VisibilityMode = result.IsPlayerVisible ? ProjectVisibilityModeIds.PlayerVisible : ProjectVisibilityModeIds.GmOnly;
        _repositories.ResearchResults.Insert(result);
        AddProjectAudit(project, actor.Id, "research.result.prepared", "Research result prepared.", result.PublicSummary, result.IsPlayerVisible);
        return Ok("Research result prepared.", new Dictionary<string, object> { { "result", ResearchResultPayload(result, true) } });
    }

    public ResponseEnvelope ResearchResultResolve(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ResearchResultsEnabled()) return KnowledgeDisabled();
        var result = RequireResearchResult(context);
        var status = NormalizeResearchResultStatus(PayloadReader.GetString(context.Request.Payload, "status"));
        if (status != ResearchResultStatusIds.Accepted && status != ResearchResultStatusIds.Rejected) throw new InvalidOperationException("Research result can only be accepted or rejected here.");
        result.Status = status;
        result.ReviewedAtUtc = DateTime.UtcNow;
        result.ReviewedByUserId = actor.Id;
        _repositories.ResearchResults.Replace(result);
        var project = _repositories.Projects.GetById(result.ProjectId);
        if (project != null) AddProjectAudit(project, actor.Id, "research.result." + status, "Research result " + status + ".", result.PublicSummary, result.IsPlayerVisible);
        return Ok("Research result resolved.", new Dictionary<string, object> { { "result", ResearchResultPayload(result, true) } });
    }

    public ResponseEnvelope ResearchResultApply(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ResearchResultsEnabled()) return KnowledgeDisabled();
        var result = RequireResearchResult(context);
        if (result.Status != ResearchResultStatusIds.Accepted) throw new InvalidOperationException("Research result must be accepted by GM before applying.");
        EntityKnowledgeState? granted = null;
        if (!string.IsNullOrWhiteSpace(result.KnowledgeDefinitionId) && !string.IsNullOrWhiteSpace(result.TargetEntityId))
        {
            var definition = _repositories.KnowledgeDefinitions.GetById(result.KnowledgeDefinitionId) ?? throw new InvalidOperationException("Knowledge definition not found.");
            granted = new EntityKnowledgeState
            {
                CampaignId = result.CampaignId,
                KnowledgeDefinitionId = definition.Id,
                KnowledgeId = definition.KnowledgeId,
                EntityType = result.TargetEntityType,
                EntityId = result.TargetEntityId,
                Level = definition.IsAppliedKnowledge ? KnowledgeLevelIds.Applied : KnowledgeLevelIds.Partial,
                TruthRelation = KnowledgeTruthRelationIds.Accurate,
                PlayerSummary = FirstNonEmpty(result.PublicSummary, definition.PublicDescription),
                IsApplied = definition.IsAppliedKnowledge,
                IsPlayerVisible = result.IsPlayerVisible,
                VisibilityMode = result.VisibilityMode,
                SourceLabel = "Research result",
                GrantedByUserId = actor.Id,
                UpdatedByUserId = actor.Id
            };
            _repositories.EntityKnowledgeStates.Insert(granted);
        }
        result.Status = ResearchResultStatusIds.Applied;
        result.AppliedAtUtc = DateTime.UtcNow;
        result.AppliedByUserId = actor.Id;
        _repositories.ResearchResults.Replace(result);
        var project = _repositories.Projects.GetById(result.ProjectId);
        if (project != null)
        {
            project.ResultStatus = ProjectResultStatusIds.Accepted;
            if (project.Status != ProjectStatusIds.Completed)
            {
                project.Status = ProjectStatusIds.Completed;
                project.CompletedAtUtc = DateTime.UtcNow;
            }
            TouchProject(project, actor.Id);
            _repositories.Projects.Replace(project);
            AddProjectAudit(project, actor.Id, "research.result.applied", "Research result applied by GM.", result.PublicSummary, result.IsPlayerVisible);
            TryWriteResearchJournal(project, "research.result.applied", "Research result applied", actor.Id);
        }
        return Ok("Research result applied.", new Dictionary<string, object>
        {
            { "result", ResearchResultPayload(result, true) },
            { "knowledgeGrant", granted == null ? new Dictionary<string, object>() : EntityKnowledgePayload(granted, true) }
        });
    }

    public ResponseEnvelope ResearchPlayerList(CommandContext context)
    {
        var actor = GetCurrentAccount(context) ?? throw new UnauthorizedAccessException("Authentication required.");
        if (!ResearchPlayerEnabled()) return KnowledgeDisabled();
        var payload = new Dictionary<string, object>(context.Request.Payload ?? new Dictionary<string, object>()) { ["projectType"] = ProjectTypeIds.Research };
        context.Request.Payload = payload;
        return ProjectPlayerList(context);
    }

    public ResponseEnvelope ResearchPlayerGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context) ?? throw new UnauthorizedAccessException("Authentication required.");
        if (!ResearchPlayerEnabled()) return KnowledgeDisabled();
        var project = RequireOwnPlayerProject(context, actor);
        if (project.ProjectType != ProjectTypeIds.Research) throw new InvalidOperationException("Research project not found.");
        return Ok("Player research loaded.", new Dictionary<string, object>
        {
            { "item", ProjectPayload(project, false, true) },
            { "results", ResearchResultsPayload(project.Id, false) }
        });
    }

    public ResponseEnvelope ResearchPlayerDraftCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context) ?? throw new UnauthorizedAccessException("Authentication required.");
        if (!ResearchPlayerEnabled()) return KnowledgeDisabled();
        var payload = new Dictionary<string, object>(context.Request.Payload ?? new Dictionary<string, object>())
        {
            ["projectType"] = ProjectTypeIds.Research,
            ["ownerUserId"] = actor.Id,
            ["approvalStatus"] = ProjectApprovalStatusIds.Draft,
            ["status"] = ProjectStatusIds.Draft
        };
        context.Request.Payload = payload;
        return ProjectPlayerDraftCreate(context);
    }

    public ResponseEnvelope ResearchPlayerDraftSubmit(CommandContext context)
    {
        if (!ResearchPlayerEnabled()) return KnowledgeDisabled();
        return ProjectPlayerDraftSubmit(context);
    }

    public ResponseEnvelope KnowledgePlayerEntityList(CommandContext context)
    {
        var actor = GetCurrentAccount(context) ?? throw new UnauthorizedAccessException("Authentication required.");
        if (!KnowledgePlayerEnabled()) return KnowledgeDisabled();
        var filter = EntityKnowledgeFilter(context.Request.Payload, playerSafe: true, actor: actor);
        var items = _repositories.EntityKnowledgeStates.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(300)
            .Where(x => CanPlayerSeeEntityKnowledge(x, actor))
            .Select(x => (object)EntityKnowledgePayload(x, includeAdmin: false))
            .ToArray();
        return Ok("Player knowledge loaded.", new Dictionary<string, object> { { "items", items } });
    }

    private ResponseEnvelope SetEntityKnowledgeVisibility(CommandContext context, bool visible, string visibilityMode, string message)
    {
        var actor = RequireAdmin(context);
        if (!KnowledgeAdminEnabled()) return KnowledgeDisabled();
        var item = RequireEntityKnowledge(context);
        item.IsPlayerVisible = visible;
        item.VisibilityMode = visibilityMode;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actor.Id;
        _repositories.EntityKnowledgeStates.Replace(item);
        TryPublishKnowledgeSync(item.CampaignId, visible ? "knowledge.entity.revealed" : "knowledge.entity.hidden", "entity_knowledge", item.Id, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok(message, new Dictionary<string, object> { { "item", EntityKnowledgePayload(item, includeAdmin: true) } });
    }

    private KnowledgeDefinition RequireKnowledgeDefinition(CommandContext context)
    {
        var id = SafeText(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "knowledgeDefinitionId"), PayloadReader.GetString(context.Request.Payload, "definitionId"), PayloadReader.GetString(context.Request.Payload, "id")), 128);
        var item = string.IsNullOrWhiteSpace(id) ? null : _repositories.KnowledgeDefinitions.GetById(id);
        if (item == null) throw new InvalidOperationException("Knowledge definition not found.");
        return item;
    }

    private EntityKnowledgeState RequireEntityKnowledge(CommandContext context)
    {
        var id = SafeText(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "entityKnowledgeId"), PayloadReader.GetString(context.Request.Payload, "knowledgeStateId"), PayloadReader.GetString(context.Request.Payload, "id")), 128);
        var item = string.IsNullOrWhiteSpace(id) ? null : _repositories.EntityKnowledgeStates.GetById(id);
        if (item == null) throw new InvalidOperationException("Entity knowledge not found.");
        return item;
    }

    private AppliedKnowledgeDefinition RequireAppliedKnowledge(CommandContext context)
    {
        var id = SafeText(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "appliedKnowledgeId"), PayloadReader.GetString(context.Request.Payload, "id")), 128);
        var item = string.IsNullOrWhiteSpace(id) ? null : _repositories.AppliedKnowledgeDefinitions.GetById(id);
        if (item == null) throw new InvalidOperationException("Applied knowledge not found.");
        return item;
    }

    private ProjectBaseState RequireResearchProject(CommandContext context)
    {
        var project = RequireProject(context);
        if (project.ProjectType != ProjectTypeIds.Research) throw new InvalidOperationException("Research project not found.");
        return project;
    }

    private ResearchResultState RequireResearchResult(CommandContext context)
    {
        var id = SafeText(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "researchResultId"), PayloadReader.GetString(context.Request.Payload, "resultId"), PayloadReader.GetString(context.Request.Payload, "id")), 128);
        var item = string.IsNullOrWhiteSpace(id) ? null : _repositories.ResearchResults.GetById(id);
        if (item == null) throw new InvalidOperationException("Research result not found.");
        return item;
    }

    private FilterDefinition<EntityKnowledgeState> EntityKnowledgeFilter(IDictionary<string, object> payload, bool playerSafe, UserAccount? actor)
    {
        var campaignId = SafeText(PayloadReader.GetString(payload, "campaignId"), 128);
        var entityType = NormalizeKnowledgeEntityType(PayloadReader.GetString(payload, "entityType"), true);
        var entityId = SafeText(PayloadReader.GetString(payload, "entityId"), 128);
        var filter = FilterDefinition<EntityKnowledgeState>.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<EntityKnowledgeState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(entityType)) filter &= Builders<EntityKnowledgeState>.Filter.Eq(x => x.EntityType, entityType);
        if (!string.IsNullOrWhiteSpace(entityId)) filter &= Builders<EntityKnowledgeState>.Filter.Eq(x => x.EntityId, entityId);
        filter &= Builders<EntityKnowledgeState>.Filter.Eq(x => x.IsArchived, false);
        if (playerSafe)
        {
            filter &= Builders<EntityKnowledgeState>.Filter.Eq(x => x.IsPlayerVisible, true);
            filter &= Builders<EntityKnowledgeState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.GmOnly);
            filter &= Builders<EntityKnowledgeState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.Hidden);
            if (actor != null) filter &= Builders<EntityKnowledgeState>.Filter.Or(Builders<EntityKnowledgeState>.Filter.Eq(x => x.OwnerUserId, actor.Id), Builders<EntityKnowledgeState>.Filter.Eq(x => x.OwnerUserId, string.Empty));
        }
        return filter;
    }

    private void UpdateEntityKnowledgeFromPayload(EntityKnowledgeState item, IDictionary<string, object> payload, string actorId)
    {
        item.EntityDisplayName = FirstNonEmpty(SafeText(PayloadReader.GetString(payload, "entityDisplayName"), 180), item.EntityDisplayName);
        item.OwnerUserId = FirstNonEmpty(SafeText(PayloadReader.GetString(payload, "ownerUserId"), 128), item.OwnerUserId);
        item.Level = FirstNonEmpty(NormalizeKnowledgeLevel(PayloadReader.GetString(payload, "level"), true), item.Level);
        item.TruthRelation = FirstNonEmpty(NormalizeTruthRelation(PayloadReader.GetString(payload, "truthRelation"), true), item.TruthRelation);
        item.PlayerSummary = FirstNonEmpty(SafeText(PayloadReader.GetString(payload, "playerSummary"), 4096), item.PlayerSummary);
        item.GMNotes = FirstNonEmpty(SafeText(PayloadReader.GetString(payload, "gmNotes"), 8192), item.GMNotes);
        item.FalseOrOutdatedSummary = FirstNonEmpty(SafeText(PayloadReader.GetString(payload, "falseOrOutdatedSummary"), 4096), item.FalseOrOutdatedSummary);
        if (payload.ContainsKey("isApplied")) item.IsApplied = PayloadReader.GetBool(payload, "isApplied");
        if (payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        item.VisibilityMode = FirstNonEmpty(NormalizeProjectVisibility(PayloadReader.GetString(payload, "visibilityMode"), true), item.VisibilityMode);
        item.SourceId = FirstNonEmpty(SafeText(PayloadReader.GetString(payload, "sourceId"), 128), item.SourceId);
        item.SourceLabel = FirstNonEmpty(SafeText(PayloadReader.GetString(payload, "sourceLabel"), 256), item.SourceLabel);
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actorId;
    }

    private Dictionary<string, object> KnowledgeDefinitionPayload(KnowledgeDefinition item, bool includeAdmin)
    {
        var payload = new Dictionary<string, object>
        {
            { "id", item.Id },
            { "knowledgeId", item.KnowledgeId },
            { "campaignId", item.CampaignId },
            { "category", item.Category },
            { "subcategory", item.Subcategory },
            { "name", item.Name },
            { "shortName", item.ShortName },
            { "publicDescription", item.PublicDescription },
            { "knowledgeType", item.KnowledgeType },
            { "knowledgeDomain", item.KnowledgeDomain },
            { "defaultVisibilityRule", item.DefaultVisibilityRule },
            { "isAppliedKnowledge", item.IsAppliedKnowledge },
            { "isSecret", includeAdmin && item.IsSecret },
            { "isPlayerDiscoverable", item.IsPlayerDiscoverable },
            { "isArchived", item.IsArchived },
            { "updatedAtUtc", item.UpdatedAtUtc }
        };
        if (includeAdmin)
        {
            payload["ruleSetId"] = item.RuleSetId;
            payload["gmDescription"] = item.GMDescription;
            payload["truthDescription"] = item.TruthDescription;
            payload["officialDescription"] = item.OfficialDescription;
            payload["sourceDocument"] = item.SourceDocument;
            payload["tags"] = item.Tags.ToArray();
        }
        return payload;
    }

    private Dictionary<string, object> EntityKnowledgePayload(EntityKnowledgeState item, bool includeAdmin)
    {
        var definition = string.IsNullOrWhiteSpace(item.KnowledgeDefinitionId) ? null : _repositories.KnowledgeDefinitions.GetById(item.KnowledgeDefinitionId);
        var payload = new Dictionary<string, object>
        {
            { "id", item.Id },
            { "campaignId", item.CampaignId },
            { "knowledgeDefinitionId", item.KnowledgeDefinitionId },
            { "knowledgeId", item.KnowledgeId },
            { "knowledgeName", definition?.Name ?? string.Empty },
            { "knowledgeType", definition?.KnowledgeType ?? string.Empty },
            { "knowledgeDomain", definition?.KnowledgeDomain ?? string.Empty },
            { "entityType", item.EntityType },
            { "entityId", item.EntityId },
            { "entityDisplayName", item.EntityDisplayName },
            { "level", PlayerSafeKnowledgeLevel(item, includeAdmin) },
            { "playerSummary", item.PlayerSummary },
            { "isApplied", item.IsApplied },
            { "isPlayerVisible", item.IsPlayerVisible },
            { "visibilityMode", includeAdmin ? item.VisibilityMode : ProjectVisibilityModeIds.PlayerVisible },
            { "sourceLabel", item.SourceLabel },
            { "updatedAtUtc", item.UpdatedAtUtc },
            { "tags", item.Tags.ToArray() }
        };
        if (includeAdmin)
        {
            payload["truthRelation"] = item.TruthRelation;
            payload["gmNotes"] = item.GMNotes;
            payload["falseOrOutdatedSummary"] = item.FalseOrOutdatedSummary;
            payload["ownerUserId"] = item.OwnerUserId;
            payload["sourceId"] = item.SourceId;
        }
        return payload;
    }

    private Dictionary<string, object> KnowledgeSourcePayload(KnowledgeSourceState item, bool includeAdmin)
    {
        var payload = new Dictionary<string, object>
        {
            { "id", item.Id },
            { "campaignId", item.CampaignId },
            { "knowledgeDefinitionId", item.KnowledgeDefinitionId },
            { "entityKnowledgeId", item.EntityKnowledgeId },
            { "sourceType", item.SourceType },
            { "title", item.Title },
            { "publicSummary", item.PublicSummary },
            { "isPlayerVisible", item.IsPlayerVisible },
            { "createdAtUtc", item.CreatedAtUtc }
        };
        if (includeAdmin)
        {
            payload["gmSummary"] = item.GMSummary;
            payload["linkedEntityType"] = item.LinkedEntityType;
            payload["linkedEntityId"] = item.LinkedEntityId;
            payload["visibilityMode"] = item.VisibilityMode;
        }
        return payload;
    }

    private Dictionary<string, object> AppliedKnowledgePayload(AppliedKnowledgeDefinition item, bool includeAdmin)
    {
        var payload = new Dictionary<string, object>
        {
            { "id", item.Id },
            { "campaignId", item.CampaignId },
            { "knowledgeDefinitionId", item.KnowledgeDefinitionId },
            { "appliedType", item.AppliedType },
            { "name", item.Name },
            { "publicDescription", item.PublicDescription },
            { "recipeReferenceId", item.RecipeReferenceId },
            { "blueprintReferenceId", item.BlueprintReferenceId },
            { "futureSystemBoundary", item.FutureSystemBoundary },
            { "isPlayerVisible", item.IsPlayerVisible },
            { "updatedAtUtc", item.UpdatedAtUtc }
        };
        if (includeAdmin)
        {
            payload["gmDescription"] = item.GMDescription;
            payload["visibilityMode"] = item.VisibilityMode;
        }
        return payload;
    }

    private object[] ResearchResultsPayload(string projectId, bool includeAdmin)
        => _repositories.ResearchResults.Find(Builders<ResearchResultState>.Filter.Eq(x => x.ProjectId, projectId) & Builders<ResearchResultState>.Filter.Eq(x => x.IsArchived, false))
            .Where(x => includeAdmin || IsProjectItemPlayerVisible(x.IsPlayerVisible, x.VisibilityMode))
            .OrderByDescending(x => x.PreparedAtUtc)
            .Select(x => (object)ResearchResultPayload(x, includeAdmin))
            .ToArray();

    private Dictionary<string, object> ResearchResultPayload(ResearchResultState item, bool includeAdmin)
    {
        var payload = new Dictionary<string, object>
        {
            { "id", item.Id },
            { "projectId", item.ProjectId },
            { "campaignId", item.CampaignId },
            { "resultType", item.ResultType },
            { "status", item.Status },
            { "title", item.Title },
            { "publicSummary", item.PublicSummary },
            { "knowledgeDefinitionId", includeAdmin ? item.KnowledgeDefinitionId : (item.IsPlayerVisible ? item.KnowledgeDefinitionId : string.Empty) },
            { "appliedKnowledgeId", includeAdmin ? item.AppliedKnowledgeId : string.Empty },
            { "isPlayerVisible", item.IsPlayerVisible },
            { "preparedAtUtc", item.PreparedAtUtc },
            { "reviewedAtUtc", item.ReviewedAtUtc?.ToString("O") ?? string.Empty },
            { "appliedAtUtc", item.AppliedAtUtc?.ToString("O") ?? string.Empty }
        };
        if (includeAdmin)
        {
            payload["gmSummary"] = item.GMSummary;
            payload["visibilityMode"] = item.VisibilityMode;
            payload["targetEntityType"] = item.TargetEntityType;
            payload["targetEntityId"] = item.TargetEntityId;
            payload["resultPayload"] = item.ResultPayload;
        }
        return payload;
    }

    private bool CanPlayerSeeEntityKnowledge(EntityKnowledgeState item, UserAccount actor)
    {
        if (item.IsArchived || !item.IsPlayerVisible) return false;
        if (item.VisibilityMode == ProjectVisibilityModeIds.GmOnly || item.VisibilityMode == ProjectVisibilityModeIds.Hidden) return false;
        return string.IsNullOrWhiteSpace(item.OwnerUserId) || item.OwnerUserId == actor.Id;
    }

    private static string PlayerSafeKnowledgeLevel(EntityKnowledgeState item, bool includeAdmin)
    {
        if (includeAdmin) return item.Level;
        if (item.Level == KnowledgeLevelIds.False || item.Level == KnowledgeLevelIds.Outdated) return KnowledgeLevelIds.Partial;
        if (item.Level == KnowledgeLevelIds.Truth) return KnowledgeLevelIds.Partial;
        return item.Level;
    }

    private void TryPublishKnowledgeSync(string campaignId, string operation, string entityType, string entityId, string actorId, string requestId)
        => TryPublishSyncEvent("knowledge.changed", campaignId, entityType, entityId, operation, actorId, new Dictionary<string, object> { { "entityType", entityType }, { "entityId", entityId } }, requestId);

    private void TryWriteKnowledgeJournal(string campaignId, string sourceEventId, string title, string summary, string playerSummary, string actorId, bool playerVisible)
    {
        try
        {
            if (!_featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchJournalIntegration)) ||
                !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp)) ||
                !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion)))
                return;

            _repositories.EventJournalEntries.Insert(new EventJournalEntryState
            {
                CampaignId = campaignId,
                EntryType = EventJournalEntryTypeIds.Automatic,
                Category = EventJournalCategoryIds.Custom,
                Severity = EventJournalSeverityIds.Information,
                Title = title,
                Summary = summary,
                PlayerSummary = playerSummary,
                SourceModule = "knowledge_research",
                SourceEventId = sourceEventId,
                SourceEventType = "knowledge",
                VisibilityMode = playerVisible ? EventJournalVisibilityModeIds.PlayerVisible : EventJournalVisibilityModeIds.GMOnly,
                IsPlayerVisible = playerVisible,
                IsAutomatic = true,
                ActorUserId = actorId,
                CreatedByUserId = actorId,
                CreatedAtUtc = DateTime.UtcNow,
                OccurredAtUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.Debug($"knowledge.journal.write.error message={ex.Message}");
        }
    }

    private void TryWriteResearchJournal(ProjectBaseState project, string sourceEventId, string title, string actorId)
        => TryWriteKnowledgeJournal(project.CampaignId, sourceEventId + ":" + project.Id, title, project.Name, project.PublicSummary, actorId, project.IsPlayerVisible);

    private bool KnowledgeAdminEnabled() => _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseKnowledgeMvp)) && _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchAdminView));
    private bool KnowledgePlayerEnabled() => _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseKnowledgeMvp)) && _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchPlayerView));
    private bool KnowledgeSourcesEnabled() => KnowledgeAdminEnabled() && _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseKnowledgeSourcesV1));
    private bool AppliedKnowledgeEnabled() => KnowledgeAdminEnabled() && _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseAppliedKnowledgeV1));
    private bool ResearchAdminEnabled() => _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchMvp)) && _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchProjectV1)) && _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchAdminView)) && _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchProjectFoundationIntegration));
    private bool ResearchPlayerEnabled() => _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchMvp)) && _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchPlayerView)) && _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchProjectFoundationIntegration));
    private bool ResearchResultsEnabled() => ResearchAdminEnabled() && _featureFlags.IsEnabled(nameof(KnowledgeResearchFeatureFlags.UseResearchResultsV1));
    private static ResponseEnvelope KnowledgeDisabled() => Error("Knowledge / Research MVP is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);

    private static string NormalizeKnowledgeType(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, KnowledgeTypeIds.Fact,
        KnowledgeTypeIds.Fact, KnowledgeTypeIds.Rumor, KnowledgeTypeIds.Theory, KnowledgeTypeIds.Method, KnowledgeTypeIds.Technology, KnowledgeTypeIds.Recipe, KnowledgeTypeIds.Blueprint, KnowledgeTypeIds.Ritual, KnowledgeTypeIds.Doctrine, KnowledgeTypeIds.LanguageKnowledge, KnowledgeTypeIds.LocationKnowledge, KnowledgeTypeIds.FactionKnowledge, KnowledgeTypeIds.CreatureKnowledge, KnowledgeTypeIds.AnomalyKnowledge, KnowledgeTypeIds.MagicKnowledge, KnowledgeTypeIds.EngineeringKnowledge, KnowledgeTypeIds.LegalKnowledge, KnowledgeTypeIds.Custom);
    private static string NormalizeKnowledgeDomain(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, KnowledgeDomainIds.Custom,
        KnowledgeDomainIds.Person, KnowledgeDomainIds.Creature, KnowledgeDomainIds.Faction, KnowledgeDomainIds.Country, KnowledgeDomainIds.City, KnowledgeDomainIds.Location, KnowledgeDomainIds.Region, KnowledgeDomainIds.Technology, KnowledgeDomainIds.Magic, KnowledgeDomainIds.Item, KnowledgeDomainIds.Recipe, KnowledgeDomainIds.Blueprint, KnowledgeDomainIds.Anomaly, KnowledgeDomainIds.Event, KnowledgeDomainIds.Language, KnowledgeDomainIds.Law, KnowledgeDomainIds.Market, KnowledgeDomainIds.Organization, KnowledgeDomainIds.Doctrine, KnowledgeDomainIds.Ritual, KnowledgeDomainIds.Map, KnowledgeDomainIds.Custom);
    private static string NormalizeKnowledgeEntityType(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, KnowledgeEntityTypeIds.Custom,
        KnowledgeEntityTypeIds.Character, KnowledgeEntityTypeIds.Companion, KnowledgeEntityTypeIds.Npc, KnowledgeEntityTypeIds.Group, KnowledgeEntityTypeIds.Organization, KnowledgeEntityTypeIds.Faction, KnowledgeEntityTypeIds.Custom);
    private static string NormalizeKnowledgeLevel(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, KnowledgeLevelIds.Partial,
        KnowledgeLevelIds.Unknown, KnowledgeLevelIds.Rumor, KnowledgeLevelIds.Partial, KnowledgeLevelIds.False, KnowledgeLevelIds.Outdated, KnowledgeLevelIds.Official, KnowledgeLevelIds.Truth, KnowledgeLevelIds.KnownWithoutUnderstanding, KnowledgeLevelIds.Applied);
    private static string NormalizeTruthRelation(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, KnowledgeTruthRelationIds.Unknown,
        KnowledgeTruthRelationIds.Unknown, KnowledgeTruthRelationIds.Accurate, KnowledgeTruthRelationIds.Partial, KnowledgeTruthRelationIds.False, KnowledgeTruthRelationIds.Outdated, KnowledgeTruthRelationIds.OfficialVersion, KnowledgeTruthRelationIds.GmTruth);
    private static string NormalizeKnowledgeVisibilityRule(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, KnowledgeVisibilityRuleIds.RevealManually,
        KnowledgeVisibilityRuleIds.GmOnly, KnowledgeVisibilityRuleIds.PlayerVisible, KnowledgeVisibilityRuleIds.RevealManually, KnowledgeVisibilityRuleIds.OwnerOnly, KnowledgeVisibilityRuleIds.Hidden);
    private static string NormalizeKnowledgeSourceType(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, KnowledgeSourceTypeIds.Custom,
        KnowledgeSourceTypeIds.Observation, KnowledgeSourceTypeIds.Book, KnowledgeSourceTypeIds.Mentor, KnowledgeSourceTypeIds.Research, KnowledgeSourceTypeIds.Rumor, KnowledgeSourceTypeIds.Artifact, KnowledgeSourceTypeIds.Experiment, KnowledgeSourceTypeIds.OfficialRecord, KnowledgeSourceTypeIds.Custom);
    private static string NormalizeAppliedKnowledgeType(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, AppliedKnowledgeTypeIds.Custom,
        AppliedKnowledgeTypeIds.Technology, AppliedKnowledgeTypeIds.Method, AppliedKnowledgeTypeIds.Recipe, AppliedKnowledgeTypeIds.Blueprint, AppliedKnowledgeTypeIds.Ritual, AppliedKnowledgeTypeIds.Doctrine, AppliedKnowledgeTypeIds.ProductionProcess, AppliedKnowledgeTypeIds.Custom);
    private static string NormalizeResearchType(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, ResearchTypeIds.Custom,
        ResearchTypeIds.Investigation, ResearchTypeIds.Experiment, ResearchTypeIds.ReverseEngineering, ResearchTypeIds.Invention, ResearchTypeIds.Adaptation, ResearchTypeIds.FieldStudy, ResearchTypeIds.DoctrineDevelopment, ResearchTypeIds.RitualStudy, ResearchTypeIds.Custom);
    private static string NormalizeResearchResultType(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, ResearchResultTypeIds.ResearchNote,
        ResearchResultTypeIds.KnowledgeGrant, ResearchResultTypeIds.AppliedKnowledgeUnlock, ResearchResultTypeIds.RecipeReference, ResearchResultTypeIds.BlueprintReference, ResearchResultTypeIds.ResearchNote, ResearchResultTypeIds.FutureCraftingBoundary, ResearchResultTypeIds.FutureEngineeringBoundary, ResearchResultTypeIds.Custom);
    private static string NormalizeResearchResultStatus(string? value, bool allowEmpty = false) => NormalizeAllowed(value, allowEmpty, ResearchResultStatusIds.Prepared,
        ResearchResultStatusIds.Draft, ResearchResultStatusIds.Prepared, ResearchResultStatusIds.Accepted, ResearchResultStatusIds.Rejected, ResearchResultStatusIds.Applied);

    private static string NormalizeAllowed(string? value, bool allowEmpty, string fallback, params string[] allowed)
    {
        var text = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return allowEmpty ? string.Empty : fallback;
        return allowed.Any(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase)) ? text : fallback;
    }
}
