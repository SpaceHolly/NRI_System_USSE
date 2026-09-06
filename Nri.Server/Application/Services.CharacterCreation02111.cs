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
    private static readonly string[] FantasyCreationAttributes02111 =
    {
        CharacterAttributeIds.Strength,
        CharacterAttributeIds.Dexterity,
        CharacterAttributeIds.Endurance,
        CharacterAttributeIds.Intellect,
        CharacterAttributeIds.Wisdom,
        CharacterAttributeIds.Charisma
    };

    public ResponseEnvelope CharacterCreationPolicyGet02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = CharacterCreationCampaign02111(context.Request.Payload);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignView);
        var policy = CharacterCreationPolicy02111(campaignId);
        return Ok("Правила создания персонажа загружены.", CharacterCreationPolicyPayload02111(policy));
    }

    public ResponseEnvelope CharacterCreationPolicyUpdate02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = CharacterCreationCampaign02111(context.Request.Payload);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignManageSettings);
        var expected = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        var policy = CharacterCreationPolicy02111(campaignId);
        if (expected.HasValue && expected.Value != policy.EntityRevision) return Conflict02111("Правила создания уже изменены другим пользователем.");
        var value = PayloadReader.GetString(context.Request.Payload, "policy") ?? string.Empty;
        if (!new[] { CharacterCreationPolicyIds.Free, CharacterCreationPolicyIds.RequireGmApproval, CharacterCreationPolicyIds.GmOnly }.Contains(value, StringComparer.Ordinal))
            return Validation02111("Выберите допустимый режим создания персонажей.");
        policy.Policy = value;
        policy.PlayerMayRenameFinalized = PayloadReader.GetBool(context.Request.Payload, "playerMayRenameFinalized");
        policy.PlayerMayEditFinalizedBackstory = PayloadReader.GetBool(context.Request.Payload, "playerMayEditFinalizedBackstory");
        policy.UpdatedByUserId = actor.Id;
        policy.EntityRevision++;
        policy.UpdatedUtc = DateTime.UtcNow;
        _mongo.CharacterCreationPolicies.ReplaceOne(x => x.Id == policy.Id, policy, new ReplaceOptions { IsUpsert = true });
        WriteAudit("character_creation_policy", actor.Id, "update", campaignId);
        PublishCharacterCreationSync02111(campaignId, "CharacterCreationPolicyUpdated", "character_creation_policy", policy.Id, "updated", actor.Id, context.Request.RequestId);
        return Ok("Правила создания персонажа сохранены.", CharacterCreationPolicyPayload02111(policy));
    }

    public ResponseEnvelope CharacterCreationDefinitionsList02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = CharacterCreationCampaign02111(context.Request.Payload);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignView);
        var ruleSetId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "ruleSetId"), RuleSetIds.FantasyNriDefault);
        var canUsePermissionOrigins = false; // Campaign permission grants can extend this later without leaking definitions.
        var allOrigins = CharacterCreationOriginDefinitions02111(ruleSetId);
        var origins = allOrigins
            .Where(x => x.IsPlayerVisible
                && (string.Equals(x.Availability, CharacterOriginAvailabilityIds.Playable, StringComparison.Ordinal)
                    || (canUsePermissionOrigins && string.Equals(x.Availability, CharacterOriginAvailabilityIds.PlayableWithCampaignPermission, StringComparison.Ordinal))))
            .Select(CharacterOriginListPayload02111).Cast<object>().ToArray();
        var visibleOriginIds = new HashSet<string>(origins.Cast<Dictionary<string, object>>().Select(x => Convert.ToString(x["originId"]) ?? string.Empty), StringComparer.Ordinal);
        var subtypes = CharacterCreationSubtypeDefinitions02111(ruleSetId)
            .Where(x => visibleOriginIds.Contains(x.OriginId) && x.IsPlayerVisible && !x.IsGmOnly
                && (x.Availability == CharacterOriginAvailabilityIds.Playable
                    || (canUsePermissionOrigins && x.Availability == CharacterOriginAvailabilityIds.PlayableWithCampaignPermission)))
            .Select(CharacterSubtypePayload02111).Cast<object>().ToArray();
        var attributeDefinitions = CharacterCreationRuleDefinitions02111(ruleSetId, "attribute_definition", "attributeId");
        var subAttributeDefinitions = CharacterCreationRuleDefinitions02111(ruleSetId, "subattribute_definition", "subAttributeId");
        var eligibleOwners = CharacterCreationCanManageAny02111(actor.Id, campaignId)
            ? _repositories.CampaignMemberships.Find(Builders<CampaignMembership>.Filter.Eq(x => x.CampaignId, campaignId)
                & Builders<CampaignMembership>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active))
                .Select(x =>
                {
                    var account = _repositories.Accounts.GetById(x.UserId);
                    return (object)new Dictionary<string, object> { ["ownerUserId"] = x.UserId, ["displayName"] = account?.Login ?? "Участник кампании" };
                }).ToArray()
            : Array.Empty<object>();
        return Ok("Варианты происхождения загружены.", new Dictionary<string, object>
        {
            ["origins"] = origins,
            ["subtypes"] = subtypes,
            ["attributeDefinitions"] = attributeDefinitions,
            ["subAttributeDefinitions"] = subAttributeDefinitions,
            ["attributeIds"] = ruleSetId == RuleSetIds.FantasyNriDefault ? FantasyCreationAttributes02111 : Array.Empty<string>(),
            ["attributePreset"] = new[] { 2, 1, 0, 0, -1, -2 },
            ["subAttributeMinimum"] = -2,
            ["subAttributeMaximum"] = 2,
            ["eligibleOwners"] = eligibleOwners
        });
    }

    public ResponseEnvelope CharacterCreationDraftList02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = CharacterCreationCampaign02111(context.Request.Payload);
        var canManage = CharacterCreationCanManageAny02111(actor.Id, campaignId);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId,
            canManage ? CampaignCapabilityIds.CharacterManageAnyInCampaign : CampaignCapabilityIds.CharacterManageOwned);
        var filter = Builders<CharacterCreationDraft>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<CharacterCreationDraft>.Filter.Eq(x => x.IsArchived, false);
        if (!canManage) filter &= Builders<CharacterCreationDraft>.Filter.Eq(x => x.OwnerUserId, actor.Id);
        var items = _mongo.CharacterCreationDrafts.Find(filter).SortByDescending(x => x.UpdatedUtc).ToList()
            .Select(x => CharacterCreationDraftPayload02111(x, canManage)).Cast<object>().ToArray();
        return Ok("Черновики загружены.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope CharacterCreationDraftGet02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var draft = CharacterCreationDraft02111(PayloadReader.GetString(context.Request.Payload, "draftId"));
        CharacterCreationRequireDraftAccess02111(context, actor, draft, false);
        return Ok("Черновик загружен.", CharacterCreationDraftPayload02111(draft, CharacterCreationCanManageAny02111(actor.Id, draft.CampaignId)));
    }

    public ResponseEnvelope CharacterCreationDraftSave02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload;
        var campaignId = CharacterCreationCampaign02111(payload);
        var canManage = CharacterCreationCanManageAny02111(actor.Id, campaignId);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId,
            canManage ? CampaignCapabilityIds.CharacterManageAnyInCampaign : CampaignCapabilityIds.CharacterManageOwned);
        var policy = CharacterCreationPolicy02111(campaignId);
        if (!canManage && string.Equals(policy.Policy, CharacterCreationPolicyIds.GmOnly, StringComparison.Ordinal))
            return Error("Создание персонажей в этой кампании выполняет GM.", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var draftId = PayloadReader.GetString(payload, "draftId") ?? string.Empty;
        var draft = string.IsNullOrWhiteSpace(draftId) ? null : _mongo.CharacterCreationDrafts.Find(x => x.Id == draftId).FirstOrDefault();
        var isNew = draft == null;
        draft ??= new CharacterCreationDraft { CampaignId = campaignId, CreatedByUserId = actor.Id };
        if (!isNew) CharacterCreationRequireDraftAccess02111(context, actor, draft, true);
        if (!isNew && !string.Equals(draft.Status, CharacterCreationDraftStatusIds.Draft, StringComparison.Ordinal)
            && !string.Equals(draft.Status, CharacterCreationDraftStatusIds.ReturnedForRevision, StringComparison.Ordinal))
            return Conflict02111("Этот черновик сейчас недоступен для редактирования.");
        var expected = PayloadReader.GetLong(payload, "expectedRevision");
        if (!isNew && (!expected.HasValue || expected.Value != draft.EntityRevision)) return Conflict02111("Черновик уже изменён. Обновите данные.");

        var requestedOwner = PayloadReader.GetString(payload, "ownerUserId") ?? string.Empty;
        if (!canManage && !string.IsNullOrWhiteSpace(requestedOwner) && !string.Equals(requestedOwner, actor.Id, StringComparison.Ordinal))
            return Error("Нельзя создать персонажа для другого пользователя.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        draft.OwnerUserId = canManage && !string.IsNullOrWhiteSpace(requestedOwner) ? requestedOwner : actor.Id;
        _ = GetAccount(draft.OwnerUserId);
        var ownerMembership = _repositories.CampaignMemberships.Find(
            Builders<CampaignMembership>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<CampaignMembership>.Filter.Eq(x => x.UserId, draft.OwnerUserId)
            & Builders<CampaignMembership>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active)).FirstOrDefault();
        if (ownerMembership == null)
            return Error("Владелец должен быть активным участником выбранной кампании.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        draft.RuleSetId = FirstNonEmpty(PayloadReader.GetString(payload, "ruleSetId"), draft.RuleSetId, RuleSetIds.FantasyNriDefault);
        draft.DisplayName = (PayloadReader.GetString(payload, "displayName") ?? string.Empty).Trim();
        draft.PublicBackstory = (PayloadReader.GetString(payload, "backstory") ?? string.Empty).Trim();
        draft.Parent1RaceId = PayloadReader.GetString(payload, "parent1RaceId") ?? string.Empty;
        draft.Parent2RaceId = FirstNonEmpty(PayloadReader.GetString(payload, "parent2RaceId"), draft.Parent1RaceId);
        draft.SubtypeId = PayloadReader.GetString(payload, "subtypeId") ?? string.Empty;
        draft.HeightCm = PayloadReader.GetInt(payload, "heightCm") ?? 0;
        draft.AgeAnchorYears = PayloadReader.GetInt(payload, "ageYears") ?? 0;
        CharacterCreationAnchorAgeToWorldTime02111(draft);
        draft.AttributeAllocation = CharacterCreationIntMap02111(payload, "attributeAllocation");
        draft.SubAttributeAllocation = CharacterCreationIntMap02111(payload, "subAttributeAllocation");
        draft.LanguageAllocation = CharacterCreationIntMap02111(payload, "languageAllocation");
        draft.LanguageGrantProfileId = FirstNonEmpty(PayloadReader.GetString(payload, "languageGrantProfileId"), CharacterLanguageGrantProfileIds022Gate3.Custom);
        var preview = CharacterCreationBuildPreview02111(draft, false);
        ApplyResolvedOrigin02111(draft, preview);
        draft.ValidationRevision++;
        draft.EntityRevision = isNew ? 1 : draft.EntityRevision + 1;
        draft.UpdatedByUserId = actor.Id;
        draft.UpdatedUtc = DateTime.UtcNow;
        if (isNew) _mongo.CharacterCreationDrafts.InsertOne(draft);
        else _mongo.CharacterCreationDrafts.ReplaceOne(x => x.Id == draft.Id, draft);
        WriteAudit("character_creation", actor.Id, isNew ? "draft.create" : "draft.update", draft.Id);
        PublishCharacterCreationSync02111(campaignId, isNew ? "CharacterCreationDraftCreated" : "CharacterCreationDraftUpdated", "character_creation_draft", draft.Id, isNew ? "created" : "updated", actor.Id, context.Request.RequestId);
        return Ok(isNew ? "Черновик создан." : "Черновик сохранён.", new Dictionary<string, object>
        {
            ["draft"] = CharacterCreationDraftPayload02111(draft, canManage),
            ["preview"] = CharacterCreationPreviewPayload02111(preview, false)
        });
    }

    public ResponseEnvelope CharacterCreationPreview02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var draft = CharacterCreationDraft02111(PayloadReader.GetString(context.Request.Payload, "draftId"));
        CharacterCreationRequireDraftAccess02111(context, actor, draft, false);
        var admin = CharacterCreationCanManageAny02111(actor.Id, draft.CampaignId);
        return Ok("Предпросмотр готов.", CharacterCreationPreviewPayload02111(CharacterCreationBuildPreview02111(draft, admin), admin));
    }

    public ResponseEnvelope CharacterCreationSubmit02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var draft = CharacterCreationDraft02111(PayloadReader.GetString(context.Request.Payload, "draftId"));
        CharacterCreationRequireDraftAccess02111(context, actor, draft, true);
        if (!string.Equals(draft.OwnerUserId, actor.Id, StringComparison.Ordinal)) return Error("Отправить можно только свой черновик.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        if (!new[] { CharacterCreationDraftStatusIds.Draft, CharacterCreationDraftStatusIds.ReturnedForRevision }.Contains(draft.Status, StringComparer.Ordinal))
            return Conflict02111("Черновик нельзя отправить в текущем состоянии.");
        var preview = CharacterCreationBuildPreview02111(draft, false);
        if (!preview.IsValid) return Validation02111(string.Join(" ", preview.Errors));
        var policy = CharacterCreationPolicy02111(draft.CampaignId);
        if (string.Equals(policy.Policy, CharacterCreationPolicyIds.Free, StringComparison.Ordinal))
            return CharacterCreationFinalizeCore02111(context, actor, draft, false);
        if (string.Equals(policy.Policy, CharacterCreationPolicyIds.GmOnly, StringComparison.Ordinal))
            return Error("Создание персонажей в этой кампании выполняет GM.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        draft.Status = CharacterCreationDraftStatusIds.Submitted;
        draft.ReturnComment = string.Empty;
        draft.EntityRevision++;
        draft.UpdatedByUserId = actor.Id;
        draft.UpdatedUtc = DateTime.UtcNow;
        _mongo.CharacterCreationDrafts.ReplaceOne(x => x.Id == draft.Id, draft);
        WriteAudit("character_creation", actor.Id, "submit", draft.Id);
        PublishCharacterCreationSync02111(draft.CampaignId, "CharacterCreationSubmitted", "character_creation_draft", draft.Id, "submitted", actor.Id, context.Request.RequestId);
        return Ok("Черновик отправлен GM.", CharacterCreationDraftPayload02111(draft, false));
    }

    public ResponseEnvelope CharacterCreationCancel02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var draft = CharacterCreationDraft02111(PayloadReader.GetString(context.Request.Payload, "draftId"));
        CharacterCreationRequireDraftAccess02111(context, actor, draft, true);
        if (string.Equals(draft.Status, CharacterCreationDraftStatusIds.Finalized, StringComparison.Ordinal)) return Conflict02111("Созданного персонажа нельзя отменить как черновик.");
        draft.Status = CharacterCreationDraftStatusIds.Cancelled;
        draft.IsArchived = true;
        draft.EntityRevision++;
        draft.UpdatedByUserId = actor.Id;
        _mongo.CharacterCreationDrafts.ReplaceOne(x => x.Id == draft.Id, draft);
        WriteAudit("character_creation", actor.Id, "cancel", draft.Id);
        PublishCharacterCreationSync02111(draft.CampaignId, "CharacterCreationCancelled", "character_creation_draft", draft.Id, "cancelled", actor.Id, context.Request.RequestId);
        return Ok("Черновик отменён.");
    }

    public ResponseEnvelope CharacterCreationAdminPending02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = CharacterCreationCampaign02111(context.Request.Payload);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CharacterManageAnyInCampaign);
        var items = _mongo.CharacterCreationDrafts.Find(x => x.CampaignId == campaignId && x.Status == CharacterCreationDraftStatusIds.Submitted && !x.IsArchived).ToList()
            .Select(x => CharacterCreationDraftPayload02111(x, true)).Cast<object>().ToArray();
        return Ok("Заявки на создание загружены.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope CharacterCreationAdminReturn02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var draft = CharacterCreationDraft02111(PayloadReader.GetString(context.Request.Payload, "draftId"));
        _campaignAuthorization.RequireCampaignCapability(context.Session!, draft.CampaignId, CampaignCapabilityIds.CharacterManageAnyInCampaign);
        if (!string.Equals(draft.Status, CharacterCreationDraftStatusIds.Submitted, StringComparison.Ordinal)) return Conflict02111("Вернуть можно только отправленный черновик.");
        var comment = (PayloadReader.GetString(context.Request.Payload, "comment") ?? string.Empty).Trim();
        if (comment.Length < 3) return Validation02111("Укажите комментарий для игрока.");
        draft.Status = CharacterCreationDraftStatusIds.ReturnedForRevision;
        draft.ReturnComment = comment;
        draft.EntityRevision++;
        draft.UpdatedByUserId = actor.Id;
        draft.UpdatedUtc = DateTime.UtcNow;
        _mongo.CharacterCreationDrafts.ReplaceOne(x => x.Id == draft.Id, draft);
        WriteAudit("character_creation", actor.Id, "return", draft.Id);
        PublishCharacterCreationSync02111(draft.CampaignId, "CharacterCreationReturned", "character_creation_draft", draft.Id, "returned", actor.Id, context.Request.RequestId);
        return Ok("Черновик возвращён игроку.", CharacterCreationDraftPayload02111(draft, true));
    }

    public ResponseEnvelope CharacterCreationFinalize02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var draft = CharacterCreationDraft02111(PayloadReader.GetString(context.Request.Payload, "draftId"));
        var canManage = CharacterCreationCanManageAny02111(actor.Id, draft.CampaignId);
        if (!canManage)
        {
            var policy = CharacterCreationPolicy02111(draft.CampaignId);
            if (!string.Equals(policy.Policy, CharacterCreationPolicyIds.Free, StringComparison.Ordinal) || !string.Equals(draft.OwnerUserId, actor.Id, StringComparison.Ordinal))
                return Error("Финализация требует решения GM.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }
        else _campaignAuthorization.RequireCampaignCapability(context.Session!, draft.CampaignId, CampaignCapabilityIds.CharacterManageAnyInCampaign);
        return CharacterCreationFinalizeCore02111(context, actor, draft, canManage);
    }

    public ResponseEnvelope CharacterStructuralEditPreview02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var draft = CharacterStructuralDraft02111(context, actor);
        var preview = CharacterCreationBuildPreview02111(draft, true);
        var race = _mongo.CharacterRaceOrSpeciesProfiles.Find(x => x.CharacterId == draft.Id).FirstOrDefault();
        var payload = CharacterCreationPreviewPayload02111(preview, true);
        payload["characterId"] = draft.Id;
        payload["parent1RaceId"] = draft.Parent1RaceId;
        payload["parent2RaceId"] = draft.Parent2RaceId;
        payload["subtypeId"] = draft.SubtypeId;
        payload["heightCm"] = draft.HeightCm;
        payload["ageYears"] = draft.AgeAnchorYears;
        payload["entityRevision"] = race?.EntityRevision ?? 0;
        var currentRace = race?.Profile.DisplayName ?? race?.Profile.RaceName ?? "Не указано";
        var nextRace = preview.Origin?.DisplayName ?? "Не определено";
        var inventory = _mongo.CharacterInventoryProfiles.Find(x => x.CharacterId == draft.Id).FirstOrDefault();
        var equipped = inventory?.Profile.Items.Where(x => x.IsEquipped).ToList() ?? new List<CharacterInventoryItemProfileValue>();
        var fitTags = preview.Origin?.EquipmentCompatibilityTags ?? new List<string>();
        var incompatible = equipped.Where(x => fitTags.Count > 0 && x.SnapshotTags.Count > 0 && !x.SnapshotTags.Intersect(fitTags, StringComparer.OrdinalIgnoreCase).Any()).ToList();
        var development = _mongo.CharacterDevelopmentProfiles.Find(x => x.CharacterId == draft.Id).FirstOrDefault();
        var purchasedNodes = development?.Profile.Nodes.Count(x => x.IsPurchased || x.IsUnlocked) ?? 0;
        var titleProfile = _mongo.CharacterTitleProfiles.Find(x => x.CharacterId == draft.Id).FirstOrDefault();
        var activeTitles = titleProfile?.Entitlements.Count(x => !x.IsRevoked) ?? 0;
        var impactItems = new List<string>
        {
            $"Происхождение: {currentRace} → {nextRace}.",
            $"Бонусы характеристик будут пересчитаны: {preview.AttributeBreakdown.Count}; подхарактеристик: {preview.SubAttributeBreakdown.Count}.",
            $"Языки происхождения будут добавлены без удаления уже изученных: {preview.Origin?.Languages.Count ?? 0}.",
            $"Знания происхождения будут добавлены без удаления уже изученных: {preview.Origin?.KnowledgeGrants.Count ?? 0}.",
            incompatible.Count == 0 ? $"Экипировка: проверено надетых предметов {equipped.Count}, явных конфликтов не найдено." : $"Экипировка: потенциально несовместимых надетых предметов {incompatible.Count}; предметы не будут сняты или удалены автоматически.",
            purchasedNodes == 0 ? "Развитие: открытых узлов нет." : $"Развитие: {purchasedNodes} открытых/приобретённых узлов требуют повторной проверки требований; прогресс не удаляется.",
            activeTitles == 0 ? "Титулы: открытых титулов нет." : $"Титулы: {activeTitles} открытых титулов требуют повторной проверки условий; титулы не отзываются автоматически."
        };
        payload["impactItems"] = impactItems.Cast<object>().ToArray();
        payload["impactSummary"] = incompatible.Count > 0 || purchasedNodes > 0 || activeTitles > 0
            ? "Есть последствия, требующие внимания GM. Автоматическое удаление данных отключено."
            : "Критические последствия не обнаружены. Автоматическое удаление данных отключено.";
        return Ok("Предпросмотр структурных изменений готов.", payload);
    }

    public ResponseEnvelope CharacterStructuralEditApply02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var campaignId = CharacterCampaign02111(characterId);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CharacterManageAnyInCampaign);
        var reason = RequireLength(PayloadReader.GetString(context.Request.Payload, "reason"), 5, 500, "reason");
        var raceDocument = _mongo.CharacterRaceOrSpeciesProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()
            ?? throw new KeyNotFoundException("Профиль происхождения персонажа не найден.");
        var bodyDocument = _mongo.CharacterBodyProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()
            ?? throw new KeyNotFoundException("Профиль тела персонажа не найден.");
        var attributeDocument = _mongo.CharacterAttributeProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()
            ?? throw new KeyNotFoundException("Профиль характеристик персонажа не найден.");
        var subAttributeDocument = _mongo.CharacterSubAttributeProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()
            ?? throw new KeyNotFoundException("Профиль подхарактеристик персонажа не найден.");
        var knowledgeDocument = _mongo.CharacterKnowledgeProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault();
        var expected = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (!expected.HasValue || expected.Value != raceDocument.EntityRevision)
            return Conflict02111("Структурный профиль уже изменён. Обновите данные и повторите попытку.");

        var draft = CharacterStructuralDraft02111(context, actor);
        var preview = CharacterCreationBuildPreview02111(draft, true);
        if (!preview.IsValid) return Validation02111(string.Join(" ", preview.Errors));
        var origin = preview.Origin!;
        var subtype = preview.Subtype;
        // Independent Mongo materializations preserve a usable rollback snapshot.
        var raceBefore = _mongo.CharacterRaceOrSpeciesProfiles.Find(x => x.CharacterId == characterId).First();
        var bodyBefore = _mongo.CharacterBodyProfiles.Find(x => x.CharacterId == characterId).First();
        var attributesBefore = _mongo.CharacterAttributeProfiles.Find(x => x.CharacterId == characterId).First();
        var subAttributesBefore = _mongo.CharacterSubAttributeProfiles.Find(x => x.CharacterId == characterId).First();
        var knowledgeBefore = knowledgeDocument == null ? null : _mongo.CharacterKnowledgeProfiles.Find(x => x.CharacterId == characterId).First();
        try
        {
            raceDocument.Profile.RaceId = origin.OriginKind == CharacterOriginKinds.Race ? origin.DefinitionId : string.Empty;
            raceDocument.Profile.HybridId = origin.OriginKind == CharacterOriginKinds.Hybrid ? origin.DefinitionId : string.Empty;
            raceDocument.Profile.RaceCode = origin.DefinitionId;
            raceDocument.Profile.RaceName = origin.DisplayName;
            raceDocument.Profile.SubspeciesId = origin.OriginKind == CharacterOriginKinds.Race ? draft.SubtypeId : string.Empty;
            raceDocument.Profile.HybridSubtypeId = origin.OriginKind == CharacterOriginKinds.Hybrid ? draft.SubtypeId : string.Empty;
            raceDocument.Profile.Parent1RaceId = draft.Parent1RaceId;
            raceDocument.Profile.Parent2RaceId = draft.Parent2RaceId;
            raceDocument.Profile.OriginKind = origin.OriginKind;
            raceDocument.Profile.DisplayName = FirstNonEmpty(subtype?.DisplayName, origin.DisplayName);
            raceDocument.Profile.Source = "gm_structural_edit";
            raceDocument.EntityRevision++;
            bodyDocument.Profile.HeightCm = draft.HeightCm;
            bodyDocument.Profile.HeightText = $"{draft.HeightCm} см";
            bodyDocument.Profile.AgeYears = draft.AgeAnchorYears;
            bodyDocument.Profile.AgeText = draft.AgeAnchorYears.ToString();
            bodyDocument.Profile.AgeAnchorYears = draft.AgeAnchorYears;
            bodyDocument.Profile.AgeAnchorWorldDate = draft.AgeAnchorWorldDate;
            bodyDocument.Profile.AgeAnchorWorldAbsoluteDay = draft.AgeAnchorWorldAbsoluteDay;
            bodyDocument.Profile.AgeAnchorWorldYearLengthDays = draft.AgeAnchorWorldYearLengthDays;
            bodyDocument.Profile.EquipmentCompatibilityTags = origin.EquipmentCompatibilityTags.ToList();
            bodyDocument.Profile.Source = "gm_structural_edit";
            bodyDocument.EntityRevision++;
            foreach (var value in attributeDocument.Profile.Values)
            {
                var bonus = CharacterCreationOriginBonus02111(origin.AttributeBonuses, subtype?.AttributeBonuses, value.AttributeId);
                value.ManualModifier = bonus;
                value.CurrentValue = value.BaseValue + bonus;
                value.Source = "gm_structural_edit";
            }
            attributeDocument.EntityRevision++;
            foreach (var value in subAttributeDocument.Profile.SubAttributes)
            {
                var bonus = CharacterCreationOriginBonus02111(origin.SubAttributeBonuses, subtype?.SubAttributeBonuses, value.SubAttributeId);
                value.ManualBonus = bonus;
                value.CurrentValue = value.BaseValue + bonus;
                value.Source = "gm_structural_edit";
            }
            subAttributeDocument.EntityRevision++;
            if (knowledgeDocument != null)
            {
                var languageAllocation = CharacterCreationLanguageAllocation022Gate3(draft, origin);
                knowledgeDocument.Profile.LanguageProficiencies ??= new List<CharacterLanguageProficiency>();
                foreach (var pair in languageAllocation.Where(x => x.Value > 0))
                {
                    var proficiency = knowledgeDocument.Profile.LanguageProficiencies.FirstOrDefault(x => x.LanguageId == pair.Key);
                    if (proficiency == null)
                    {
                        proficiency = new CharacterLanguageProficiency { LanguageId = pair.Key };
                        knowledgeDocument.Profile.LanguageProficiencies.Add(proficiency);
                    }
                    proficiency.Level = Math.Max(proficiency.Level, pair.Value);
                    proficiency.SourceType = LanguageProficiencySourceTypeIds.InitialKnowledge;
                    proficiency.SourceId = origin.DefinitionId;
                    proficiency.UpdatedAtUtc = DateTime.UtcNow;
                }
                knowledgeDocument.Profile.Languages = knowledgeDocument.Profile.LanguageProficiencies
                    .Where(x => x.Level > 0)
                    .Select(x => x.LanguageId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                knowledgeDocument.Profile.SchemaVersion = Math.Max(2, knowledgeDocument.Profile.SchemaVersion);
                knowledgeDocument.Profile.Revision = Math.Max(1, knowledgeDocument.Profile.Revision + 1);
                knowledgeDocument.Profile.KnownTopics = knowledgeDocument.Profile.KnownTopics.Concat(origin.KnowledgeGrants).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            _mongo.CharacterRaceOrSpeciesProfiles.ReplaceOne(x => x.CharacterId == characterId, raceDocument);
            _mongo.CharacterBodyProfiles.ReplaceOne(x => x.CharacterId == characterId, bodyDocument);
            _mongo.CharacterAttributeProfiles.ReplaceOne(x => x.CharacterId == characterId, attributeDocument);
            _mongo.CharacterSubAttributeProfiles.ReplaceOne(x => x.CharacterId == characterId, subAttributeDocument);
            if (knowledgeDocument != null) _mongo.CharacterKnowledgeProfiles.ReplaceOne(x => x.CharacterId == characterId, knowledgeDocument);
            WriteAudit("character_structural_edit", actor.Id, "apply", $"{characterId}:{reason}");
            PublishCharacterCreationSync02111(campaignId, "CharacterStructureChanged", "character", characterId, "structural_edit", actor.Id, context.Request.RequestId);
            return Ok("Структурные изменения персонажа сохранены.", new Dictionary<string, object>
            {
                ["characterId"] = characterId,
                ["entityRevision"] = raceDocument.EntityRevision,
                ["preview"] = CharacterCreationPreviewPayload02111(preview, true)
            });
        }
        catch
        {
            _mongo.CharacterRaceOrSpeciesProfiles.ReplaceOne(x => x.CharacterId == characterId, raceBefore, new ReplaceOptions { IsUpsert = true });
            _mongo.CharacterBodyProfiles.ReplaceOne(x => x.CharacterId == characterId, bodyBefore, new ReplaceOptions { IsUpsert = true });
            _mongo.CharacterAttributeProfiles.ReplaceOne(x => x.CharacterId == characterId, attributesBefore, new ReplaceOptions { IsUpsert = true });
            _mongo.CharacterSubAttributeProfiles.ReplaceOne(x => x.CharacterId == characterId, subAttributesBefore, new ReplaceOptions { IsUpsert = true });
            if (knowledgeBefore != null) _mongo.CharacterKnowledgeProfiles.ReplaceOne(x => x.CharacterId == characterId, knowledgeBefore, new ReplaceOptions { IsUpsert = true });
            throw;
        }
    }

    public ResponseEnvelope CharacterFinalizedUpdatePublic02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var ownership = _mongo.CharacterOwnerships.Find(x => x.CharacterId == characterId && !x.IsArchived).FirstOrDefault()
            ?? throw new KeyNotFoundException("Персонаж не найден в текущей кампании.");
        _campaignAuthorization.RequireCampaignCapability(context.Session!, ownership.CampaignId, CampaignCapabilityIds.CharacterManageOwned);
        if (!string.Equals(ownership.OwnerUserId, actor.Id, StringComparison.Ordinal)
            && !string.Equals(ownership.ControlledByUserId, actor.Id, StringComparison.Ordinal))
            return Error("Изменять публичные данные может только владелец персонажа.", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var body = _mongo.CharacterBodyProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()
            ?? throw new KeyNotFoundException("Профиль персонажа не найден.");
        var expected = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (!expected.HasValue || expected.Value != body.EntityRevision)
            return Conflict02111("Карточка персонажа уже изменилась. Обновите данные.");

        var policy = CharacterCreationPolicy02111(ownership.CampaignId);
        var requestedName = (PayloadReader.GetString(context.Request.Payload, "displayName") ?? ownership.CharacterDisplayName).Trim();
        var requestedBackstory = (PayloadReader.GetString(context.Request.Payload, "backstory") ?? body.Profile.Backstory).Trim();
        if (!policy.PlayerMayRenameFinalized && !string.Equals(requestedName, ownership.CharacterDisplayName, StringComparison.Ordinal))
            return Error("Переименование завершённого персонажа запрещено правилами кампании.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        if (!policy.PlayerMayEditFinalizedBackstory && !string.Equals(requestedBackstory, body.Profile.Backstory, StringComparison.Ordinal))
            return Error("Редактирование предыстории запрещено правилами кампании.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        requestedName = RequireLength(requestedName, 2, 120, "displayName");
        requestedBackstory = RequireLength(requestedBackstory, 0, 4096, "backstory");

        var previousName = ownership.CharacterDisplayName;
        var previousBackstory = body.Profile.Backstory;
        var previousBodyRevision = body.EntityRevision;
        var previousBodyUpdatedUtc = body.UpdatedUtc;
        try
        {
            ownership.CharacterDisplayName = requestedName;
            ownership.UpdatedAtUtc = DateTime.UtcNow;
            ownership.UpdatedByUserId = actor.Id;
            body.Profile.Backstory = requestedBackstory;
            body.Profile.Source = "character_v2_owner_edit";
            body.EntityRevision++;
            body.UpdatedUtc = DateTime.UtcNow;
            _repositories.CharacterOwnerships.Replace(ownership);
            _mongo.CharacterBodyProfiles.ReplaceOne(x => x.CharacterId == characterId, body);
        }
        catch
        {
            ownership.CharacterDisplayName = previousName;
            body.Profile.Backstory = previousBackstory;
            body.EntityRevision = previousBodyRevision;
            body.UpdatedUtc = previousBodyUpdatedUtc;
            _repositories.CharacterOwnerships.Replace(ownership);
            _mongo.CharacterBodyProfiles.ReplaceOne(x => x.CharacterId == characterId, body, new ReplaceOptions { IsUpsert = true });
            throw;
        }

        WriteAudit("character_v2_public_profile", actor.Id, "update", characterId);
        PublishCharacterCreationSync02111(ownership.CampaignId, "CharacterPublicProfileChanged", "character", characterId, "public_profile_updated", actor.Id, context.Request.RequestId);
        return Ok("Имя и предыстория сохранены.", new Dictionary<string, object>
        {
            ["characterId"] = characterId,
            ["displayName"] = ownership.CharacterDisplayName,
            ["backstory"] = body.Profile.Backstory,
            ["entityRevision"] = body.EntityRevision
        });
    }

    private CharacterCreationDraft CharacterStructuralDraft02111(CommandContext context, UserAccount actor)
    {
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var character = GetCharacter(characterId);
        var campaignId = CharacterCampaign02111(characterId);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CharacterManageAnyInCampaign);
        var race = _mongo.CharacterRaceOrSpeciesProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()
            ?? throw new KeyNotFoundException("Профиль происхождения персонажа не найден.");
        var body = _mongo.CharacterBodyProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()
            ?? throw new KeyNotFoundException("Профиль тела персонажа не найден.");
        var attributes = _mongo.CharacterAttributeProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault();
        var subAttributes = _mongo.CharacterSubAttributeProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault();
        return new CharacterCreationDraft
        {
            Id = characterId,
            CampaignId = campaignId,
            OwnerUserId = character.OwnerUserId,
            CreatedByUserId = actor.Id,
            RuleSetId = race.Profile.RuleSetId,
            DisplayName = character.Name,
            PublicBackstory = body.Profile.Backstory,
            Parent1RaceId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "parent1RaceId"), race.Profile.Parent1RaceId),
            Parent2RaceId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "parent2RaceId"), race.Profile.Parent2RaceId),
            SubtypeId = context.Request.Payload.ContainsKey("subtypeId") ? PayloadReader.GetString(context.Request.Payload, "subtypeId") ?? string.Empty : FirstNonEmpty(race.Profile.SubspeciesId, race.Profile.HybridSubtypeId),
            HeightCm = PayloadReader.GetInt(context.Request.Payload, "heightCm") ?? body.Profile.HeightCm,
            AgeAnchorYears = PayloadReader.GetInt(context.Request.Payload, "ageYears") ?? body.Profile.AgeYears,
            AttributeAllocation = attributes?.Profile.Values.ToDictionary(x => x.AttributeId, x => x.BaseValue, StringComparer.Ordinal) ?? new Dictionary<string, int>(),
            SubAttributeAllocation = subAttributes?.Profile.SubAttributes.ToDictionary(x => x.SubAttributeId, x => x.BaseValue, StringComparer.Ordinal) ?? new Dictionary<string, int>()
        };
    }

    public ResponseEnvelope CharacterTitleList02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var character = GetCharacter(characterId);
        if (!string.Equals(character.OwnerUserId, actor.Id, StringComparison.Ordinal) && !IsAdminActor(actor)) return Error("Титулы персонажа недоступны.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var profile = _mongo.CharacterTitleProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault() ?? new CharacterTitleProfileDocument { CharacterId = characterId };
        var ids = profile.Entitlements.Where(x => !x.IsRevoked).Select(x => x.TitleId).ToHashSet(StringComparer.Ordinal);
        var definitions = CharacterTitleDefinitions02111(profile.RuleSetId).Where(x => ids.Contains(x.DefinitionId) && x.IsPlayerVisible && !x.IsArchived).OrderBy(x => x.SortOrder).ToList();
        return Ok("Титулы загружены.", new Dictionary<string, object>
        {
            ["selectedTitleId"] = profile.SelectedTitleId,
            ["selectedTitle"] = definitions.FirstOrDefault(x => x.DefinitionId == profile.SelectedTitleId)?.DisplayName ?? string.Empty,
            ["entityRevision"] = profile.EntityRevision,
            ["items"] = definitions.Select(x => new Dictionary<string, object> { ["titleId"] = x.DefinitionId, ["displayName"] = x.DisplayName, ["description"] = x.PublicDescription }).Cast<object>().ToArray()
        });
    }

    public ResponseEnvelope CharacterTitleSelect02111(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var character = GetCharacter(characterId);
        if (!string.Equals(character.OwnerUserId, actor.Id, StringComparison.Ordinal)) return Error("Изменить титул может только владелец персонажа.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var titleId = PayloadReader.GetString(context.Request.Payload, "titleId") ?? string.Empty;
        var profile = _mongo.CharacterTitleProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault() ?? new CharacterTitleProfileDocument { CharacterId = characterId };
        var expected = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expected.HasValue && expected.Value != profile.EntityRevision) return Conflict02111("Список титулов уже изменён.");
        if (!string.IsNullOrWhiteSpace(titleId))
        {
            var entitled = profile.Entitlements.Any(x => !x.IsRevoked && string.Equals(x.TitleId, titleId, StringComparison.Ordinal));
            var definition = CharacterTitleDefinitions02111(profile.RuleSetId).FirstOrDefault(x => x.DefinitionId == titleId && x.IsPlayerVisible && !x.IsArchived);
            if (!entitled || definition == null) return Error("Этот титул не открыт для персонажа.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }
        profile.SelectedTitleId = titleId;
        profile.EntityRevision++;
        profile.UpdatedUtc = DateTime.UtcNow;
        _mongo.CharacterTitleProfiles.ReplaceOne(x => x.CharacterId == characterId, profile, new ReplaceOptions { IsUpsert = true });
        WriteAudit("character_title", actor.Id, "select", characterId);
        PublishCharacterCreationSync02111(CharacterCampaign02111(characterId), "CharacterTitleSelected", "character_title_profile", characterId, "selected", actor.Id, context.Request.RequestId);
        return Ok("Титул выбран.");
    }

    public ResponseEnvelope CharacterTitleAdminGrant02111(CommandContext context) => CharacterTitleGrantState02111(context, false);
    public ResponseEnvelope CharacterTitleAdminRevoke02111(CommandContext context) => CharacterTitleGrantState02111(context, true);

    private ResponseEnvelope CharacterTitleGrantState02111(CommandContext context, bool revoke)
    {
        var actor = GetCurrentAccount(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var campaignId = CharacterCampaign02111(characterId);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CharacterManageAnyInCampaign);
        var titleId = RequireLength(PayloadReader.GetString(context.Request.Payload, "titleId"), 2, 128, "titleId");
        var profile = _mongo.CharacterTitleProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault() ?? new CharacterTitleProfileDocument { CharacterId = characterId };
        var definition = CharacterTitleDefinitions02111(profile.RuleSetId).FirstOrDefault(x => x.DefinitionId == titleId && !x.IsArchived);
        if (definition == null) return Error("Титул не найден.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var grant = profile.Entitlements.FirstOrDefault(x => string.Equals(x.TitleId, titleId, StringComparison.Ordinal));
        if (grant == null)
        {
            grant = new CharacterTitleEntitlement { TitleId = titleId, GrantSourceType = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "sourceType"), "gm"), GrantSourceId = PayloadReader.GetString(context.Request.Payload, "sourceId") ?? string.Empty, GrantedByUserId = actor.Id };
            profile.Entitlements.Add(grant);
        }
        grant.IsRevoked = revoke;
        if (revoke && string.Equals(profile.SelectedTitleId, titleId, StringComparison.Ordinal)) profile.SelectedTitleId = string.Empty;
        profile.EntityRevision++;
        _mongo.CharacterTitleProfiles.ReplaceOne(x => x.CharacterId == characterId, profile, new ReplaceOptions { IsUpsert = true });
        WriteAudit("character_title", actor.Id, revoke ? "revoke" : "grant", $"{characterId}:{titleId}");
        PublishCharacterCreationSync02111(campaignId, revoke ? "CharacterTitleRevoked" : "CharacterTitleUnlocked", "character_title_profile", characterId, revoke ? "revoked" : "unlocked", actor.Id, context.Request.RequestId);
        return Ok(revoke ? "Титул отозван." : "Титул открыт.");
    }

    private ResponseEnvelope CharacterCreationFinalizeCore02111(CommandContext context, UserAccount actor, CharacterCreationDraft draft, bool isGm)
    {
        var operationId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId);
        if (string.IsNullOrWhiteSpace(operationId)) return Validation02111("Для финализации требуется идентификатор операции.");
        if (string.Equals(draft.Status, CharacterCreationDraftStatusIds.Finalized, StringComparison.Ordinal))
        {
            if (string.Equals(draft.FinalizationOperationId, operationId, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(draft.FinalCharacterId))
                return Ok("Персонаж уже создан.", new Dictionary<string, object> { ["characterId"] = draft.FinalCharacterId, ["replayed"] = true });
            return Conflict02111("Черновик уже финализирован.");
        }
        if (isGm && !string.Equals(draft.Status, CharacterCreationDraftStatusIds.Submitted, StringComparison.Ordinal)
            && !string.Equals(draft.Status, CharacterCreationDraftStatusIds.Draft, StringComparison.Ordinal)) return Conflict02111("Черновик нельзя финализировать в текущем состоянии.");
        var expected = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (!expected.HasValue || expected.Value != draft.EntityRevision) return Conflict02111("Черновик уже изменён. Обновите данные.");
        var preview = CharacterCreationBuildPreview02111(draft, isGm);
        if (!preview.IsValid) return Validation02111(string.Join(" ", preview.Errors));

        var character = new Character
        {
            OwnerUserId = string.Empty, // Staging remains invisible to owner queries until commit.
            Name = draft.DisplayName,
            Race = preview.Origin!.DisplayName,
            RaceCode = preview.Origin.DefinitionId,
            Age = draft.AgeAnchorYears,
            Height = draft.HeightCm.ToString(),
            Backstory = draft.PublicBackstory,
            Archived = true,
            Deleted = true
        };
        _mongo.Characters.InsertOne(character);
        try
        {
            CharacterCreationWriteProfiles02111(character.Id, draft, preview);
            if (PayloadReader.GetBool(context.Request.Payload, "injectFailureAfterProfiles")) throw new InvalidOperationException("Controlled finalization failure.");
            character.OwnerUserId = draft.OwnerUserId;
            character.Archived = false;
            character.Deleted = false;
            character.UpdatedUtc = DateTime.UtcNow;
            _mongo.Characters.ReplaceOne(x => x.Id == character.Id, character);
            _ = GetOrCreateCharacterOwnership(character, actor, draft.CampaignId);
            draft.Status = CharacterCreationDraftStatusIds.Finalized;
            draft.FinalCharacterId = character.Id;
            draft.FinalizationOperationId = operationId;
            draft.EntityRevision++;
            draft.UpdatedByUserId = actor.Id;
            draft.UpdatedUtc = DateTime.UtcNow;
            _mongo.CharacterCreationDrafts.ReplaceOne(x => x.Id == draft.Id, draft);
            WriteAudit("character_creation", actor.Id, "finalize", $"{draft.Id}:{character.Id}");
            PublishCharacterCreationSync02111(draft.CampaignId, "CharacterCreationFinalized", "character", character.Id, "finalized", actor.Id, context.Request.RequestId);
            return Ok("Персонаж создан.", new Dictionary<string, object> { ["characterId"] = character.Id, ["draftId"] = draft.Id, ["replayed"] = false });
        }
        catch
        {
            CharacterCreationCleanupStaging02111(character.Id);
            WriteAudit("character_creation", actor.Id, "finalize.failed_recovered", draft.Id);
            throw;
        }
    }

    private void CharacterCreationWriteProfiles02111(string characterId, CharacterCreationDraft draft, CharacterCreationPreview02111 preview)
    {
        var origin = preview.Origin!;
        var subtype = preview.Subtype;
        var attributeValues = FantasyCreationAttributes02111.Select(id =>
        {
            var allocated = draft.AttributeAllocation.TryGetValue(id, out var value) ? value : 0;
            var bonus = CharacterCreationOriginBonus02111(origin.AttributeBonuses, subtype?.AttributeBonuses, id);
            return new CharacterAttributeValue { AttributeId = id, BaseValue = allocated, CurrentValue = allocated + bonus, ManualModifier = bonus, Source = "character_creation" };
        }).ToList();
        var subValues = draft.SubAttributeAllocation.Select(pair =>
        {
            var parent = pair.Key.Contains(":") ? pair.Key.Split(':')[0] : string.Empty;
            var bonus = CharacterCreationOriginBonus02111(origin.SubAttributeBonuses, subtype?.SubAttributeBonuses, pair.Key);
            return new CharacterSubAttributeValue { SubAttributeId = pair.Key, ParentAttributeId = parent, BaseValue = pair.Value, CurrentValue = pair.Value + bonus, ManualBonus = bonus, Source = "character_creation" };
        }).ToList();
        _mongo.CharacterAttributeProfiles.InsertOne(new CharacterAttributeProfileDocument { CharacterId = characterId, Profile = new AttributeProfile { CharacterId = characterId, RuleSetId = draft.RuleSetId, Values = attributeValues } });
        _mongo.CharacterSubAttributeProfiles.InsertOne(new CharacterSubAttributeProfileDocument { CharacterId = characterId, Profile = new SubAttributeProfile { CharacterId = characterId, RuleSetId = draft.RuleSetId, SubAttributes = subValues } });
        _mongo.CharacterSkillProfiles.InsertOne(new CharacterSkillProfileDocument { CharacterId = characterId, Profile = new SkillProfile { CharacterId = characterId, RuleSetId = draft.RuleSetId } });
        _mongo.CharacterDevelopmentProfiles.InsertOne(new CharacterDevelopmentProfileDocument
        {
            CharacterId = characterId,
            Profile = new DevelopmentProfile
            {
                CharacterId = characterId,
                RuleSetId = draft.RuleSetId,
                SchemaVersion = 2,
                InitialDevelopment = new InitialDevelopmentState
                {
                    Status = InitialDevelopmentStatusIds.Pending,
                    PolicyId = $"{draft.RuleSetId}:initial_development",
                    PolicyRevision = 1,
                    EntityRevision = 1
                }
            }
        });
        _mongo.CharacterWalletProfiles.InsertOne(new CharacterWalletProfileDocument { CharacterId = characterId, Profile = new WalletProfile { CharacterId = characterId, RuleSetId = draft.RuleSetId } });
        _mongo.CharacterInventoryProfiles.InsertOne(new CharacterInventoryProfileDocument { CharacterId = characterId, Profile = new InventoryProfile { CharacterId = characterId, RuleSetId = draft.RuleSetId } });
        _mongo.CharacterRaceOrSpeciesProfiles.InsertOne(new CharacterRaceOrSpeciesProfileDocument { CharacterId = characterId, Profile = new RaceOrSpeciesProfile
        {
            CharacterId = characterId, RuleSetId = draft.RuleSetId, RaceId = origin.OriginKind == CharacterOriginKinds.Race ? origin.DefinitionId : string.Empty,
            HybridId = origin.OriginKind == CharacterOriginKinds.Hybrid ? origin.DefinitionId : string.Empty, RaceCode = origin.DefinitionId, RaceName = origin.DisplayName,
            SubspeciesId = origin.OriginKind == CharacterOriginKinds.Race ? draft.SubtypeId : string.Empty, HybridSubtypeId = origin.OriginKind == CharacterOriginKinds.Hybrid ? draft.SubtypeId : string.Empty,
            Parent1RaceId = draft.Parent1RaceId, Parent2RaceId = draft.Parent2RaceId, OriginKind = origin.OriginKind, DisplayName = FirstNonEmpty(subtype?.DisplayName, origin.DisplayName),
            Parent1SubtypeId = subtype?.Parent1SubtypeId ?? string.Empty, Parent2SubtypeId = subtype?.Parent2SubtypeId ?? string.Empty,
            ElementalLineageId = subtype?.ElementalLineageId ?? string.Empty,
            InheritedAspectId = subtype?.InheritedAspectId ?? string.Empty,
            FlightInheritancePermissionId = subtype?.FlightInheritancePermissionId ?? string.Empty,
            ResolvedTraitIds = preview.ResolvedPhysiology?.TraitDefinitionIds.ToList() ?? new List<string>(), Source = "character_creation", SchemaVersion = 2
        }});
        var physiology = preview.ResolvedPhysiology ?? RacePhysiologyRules022Gate2.Resolve(origin, subtype);
        _mongo.CharacterBodyProfiles.InsertOne(new CharacterBodyProfileDocument { CharacterId = characterId, Profile = new BodyProfile
        {
            CharacterId = characterId, RuleSetId = draft.RuleSetId, HeightCm = draft.HeightCm, HeightText = $"{draft.HeightCm} см", AgeYears = draft.AgeAnchorYears,
            AgeText = draft.AgeAnchorYears.ToString(), AgeAnchorYears = draft.AgeAnchorYears, AgeAnchorWorldDate = draft.AgeAnchorWorldDate,
            AgeAnchorWorldAbsoluteDay = draft.AgeAnchorWorldAbsoluteDay, AgeAnchorWorldYearLengthDays = draft.AgeAnchorWorldYearLengthDays,
            Description = origin.PublicDescription, Backstory = draft.PublicBackstory,
            EquipmentCompatibilityTags = origin.EquipmentCompatibilityTags.Concat(physiology.EquipmentFit.RequiredFitTags).Distinct(StringComparer.Ordinal).ToList(),
            BaseHealth = physiology.BaseHealth, NaturalArmorRating = physiology.NaturalArmorRating,
            NaturalPenetrationResistance = physiology.NaturalPenetrationResistance, AdultAgeYears = physiology.AdultAgeYears,
            AverageLifespanYears = physiology.AverageLifespanYears, MaximumLifespanYears = physiology.MaximumLifespanYears,
            BodyZones = physiology.BodyZones, EquipmentFit = physiology.EquipmentFit, RacialSenses = physiology.Senses,
            MovementAbilities = physiology.MovementAbilities, NaturalAttacks = physiology.NaturalAttacks,
            ElementalResistances = physiology.ElementalResistances, EnvironmentalToleranceModifiers = physiology.EnvironmentalToleranceModifiers,
            Source = "character_creation", SchemaVersion = 2
        }});
        var languageAllocation = CharacterCreationLanguageAllocation022Gate3(draft, origin);
        _mongo.CharacterKnowledgeProfiles.InsertOne(new CharacterKnowledgeProfileDocument
        {
            CharacterId = characterId,
            Profile = new KnowledgeProfile
            {
                Languages = languageAllocation.Where(x => x.Value > 0).Select(x => x.Key).ToList(),
                LanguageProficiencies = languageAllocation.Where(x => x.Value > 0).Select(x => new CharacterLanguageProficiency
                {
                    LanguageId = x.Key,
                    Level = x.Value,
                    SourceType = LanguageProficiencySourceTypeIds.InitialKnowledge,
                    SourceId = draft.Id,
                    UpdatedAtUtc = DateTime.UtcNow
                }).ToList(),
                KnownTopics = origin.KnowledgeGrants.ToList(),
                Revision = 1,
                SchemaVersion = 2
            }
        });
        _mongo.CharacterConditionProfiles.InsertOne(new CharacterConditionProfileDocument { CharacterId = characterId, Profile = new ConditionProfile() });
        _mongo.CharacterTitleProfiles.InsertOne(new CharacterTitleProfileDocument { CharacterId = characterId, RuleSetId = draft.RuleSetId });
    }

    private void CharacterCreationCleanupStaging02111(string characterId)
    {
        _mongo.CharacterAttributeProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.CharacterSubAttributeProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.CharacterSkillProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.CharacterDevelopmentProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.CharacterWalletProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.CharacterInventoryProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.CharacterRaceOrSpeciesProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.CharacterBodyProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.CharacterKnowledgeProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.CharacterConditionProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.CharacterTitleProfiles.DeleteMany(x => x.CharacterId == characterId);
        _mongo.Characters.DeleteOne(x => x.Id == characterId);
    }

    private CharacterCreationPreview02111 CharacterCreationBuildPreview02111(CharacterCreationDraft draft, bool admin)
    {
        var result = new CharacterCreationPreview02111();
        if (draft.DisplayName.Trim().Length < 2 || draft.DisplayName.Trim().Length > 80) result.Errors.Add("Имя персонажа должно содержать от 2 до 80 символов.");
        var parent1 = CharacterCreationVisibleRace02111(draft.RuleSetId, draft.Parent1RaceId, admin);
        var parent2 = CharacterCreationVisibleRace02111(draft.RuleSetId, draft.Parent2RaceId, admin);
        if (parent1 == null || parent2 == null) result.Errors.Add("Выберите доступные линии обоих родителей.");
        if (parent1 != null && parent2 != null)
        {
            if (string.Equals(parent1.DefinitionId, parent2.DefinitionId, StringComparison.Ordinal)) result.Origin = parent1;
            else result.Origin = CharacterCreationOriginDefinitions02111(draft.RuleSetId).FirstOrDefault(x => x.OriginKind == CharacterOriginKinds.Hybrid
                && ((x.Parent1RaceId == parent1.DefinitionId && x.Parent2RaceId == parent2.DefinitionId)
                    || (!x.ParentOrderMatters && x.Parent1RaceId == parent2.DefinitionId && x.Parent2RaceId == parent1.DefinitionId)));
            if (result.Origin == null) result.Errors.Add("Для выбранных линий нет допустимого гибридного происхождения.");
        }
        if (result.Origin != null)
        {
            result.Subtype = string.IsNullOrWhiteSpace(draft.SubtypeId) ? null : CharacterCreationSubtypeDefinitions02111(draft.RuleSetId)
                .FirstOrDefault(x => x.DefinitionId == draft.SubtypeId && x.OriginId == result.Origin.DefinitionId
                    && (admin || (x.IsPlayerVisible && !x.IsGmOnly && x.Availability == CharacterOriginAvailabilityIds.Playable)));
            if (!string.IsNullOrWhiteSpace(draft.SubtypeId) && result.Subtype == null) result.Errors.Add("Выбранный подвид не относится к текущему происхождению.");
            var availableSubtypes = CharacterCreationSubtypeDefinitions02111(draft.RuleSetId)
                .Any(x => x.OriginId == result.Origin.DefinitionId
                    && (admin || (x.IsPlayerVisible && !x.IsGmOnly && x.Availability == CharacterOriginAvailabilityIds.Playable)));
            if (availableSubtypes && result.Subtype == null) result.Errors.Add("Выберите подвид происхождения.");
            result.MinimumHeightCm = result.Subtype?.MinimumHeightCm ?? result.Origin.MinimumHeightCm;
            result.MaximumHeightCm = result.Subtype?.MaximumHeightCm ?? result.Origin.MaximumHeightCm;
            result.MinimumAgeYears = result.Subtype?.MinimumAgeYears ?? result.Origin.MinimumAgeYears;
            result.MaximumAgeYears = result.Subtype?.MaximumAgeYears ?? result.Origin.MaximumAgeYears;
            result.ResolvedPhysiology = RacePhysiologyRules022Gate2.Resolve(result.Origin, result.Subtype);
            foreach (var code in RacePhysiologyRules022Gate2.Validate(result.ResolvedPhysiology, result.Origin.Availability == CharacterOriginAvailabilityIds.Playable))
                result.Errors.Add(CharacterCreationPhysiologyValidationMessage022Gate2(code));
            if (draft.HeightCm < result.MinimumHeightCm || draft.HeightCm > result.MaximumHeightCm) result.Errors.Add($"Допустимый рост: {result.MinimumHeightCm}–{result.MaximumHeightCm} см.");
            if (draft.AgeAnchorYears < result.MinimumAgeYears || draft.AgeAnchorYears > result.MaximumAgeYears) result.Errors.Add($"Допустимый возраст: {result.MinimumAgeYears}–{result.MaximumAgeYears}.");
            foreach (var pair in draft.AttributeAllocation)
            {
                var originBonus = CharacterCreationOriginBonus02111(result.Origin.AttributeBonuses, result.Subtype?.AttributeBonuses, pair.Key);
                result.AttributeBreakdown[pair.Key] = new CharacterCreationValueBreakdown02111 { Allocated = pair.Value, Origin = originBonus, Effective = pair.Value + originBonus };
            }
            foreach (var pair in draft.SubAttributeAllocation)
            {
                var originBonus = CharacterCreationOriginBonus02111(result.Origin.SubAttributeBonuses, result.Subtype?.SubAttributeBonuses, pair.Key);
                result.SubAttributeBreakdown[pair.Key] = new CharacterCreationValueBreakdown02111 { Allocated = pair.Value, Origin = originBonus, Effective = pair.Value + originBonus };
            }
            foreach (var error in CharacterCreationValidateLanguages022Gate3(draft, result.Origin)) result.Errors.Add(error);
        }
        var values = draft.AttributeAllocation.Values.OrderBy(x => x).ToArray();
        if (draft.RuleSetId == RuleSetIds.FantasyNriDefault && (draft.AttributeAllocation.Count != 6 || !values.SequenceEqual(new[] { -2, -1, 0, 0, 1, 2 })))
            result.Errors.Add("Распределите характеристики по набору +2, +1, 0, 0, -1, -2.");
        if (draft.SubAttributeAllocation.Values.Any(x => x < -2 || x > 2)) result.Errors.Add("Специализация подхарактеристики должна быть от -2 до +2.");
        var positiveGroup = draft.SubAttributeAllocation.GroupBy(x => x.Key.Contains(":") ? x.Key.Split(':')[0] : x.Key).FirstOrDefault(x => x.Sum(v => v.Value) > 0);
        if (positiveGroup != null) result.Errors.Add("Сумма специализаций под одной характеристикой не может быть положительной.");
        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    private void CharacterCreationAnchorAgeToWorldTime02111(CharacterCreationDraft draft)
    {
        if (!string.IsNullOrWhiteSpace(draft.AgeAnchorWorldDate)) return;
        var worldTime = _repositories.CampaignWorldTimes.Find(
                Builders<CampaignWorldTimeState>.Filter.Eq(x => x.CampaignId, draft.CampaignId))
            .OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
        if (worldTime == null) return;
        var calendar = _repositories.WorldCalendarDefinitions.GetById(worldTime.CalendarId);
        draft.AgeAnchorWorldDate = WorldCalendarMath.Format(worldTime.CurrentDateTime, calendar, false);
        draft.AgeAnchorWorldAbsoluteDay = worldTime.CurrentDateTime.AbsoluteDayIndex;
        draft.AgeAnchorWorldYearLengthDays = Math.Max(1, calendar?.DaysPerYear ?? WorldCalendarDefaults.DaysPerYear);
    }

    private CharacterOriginDefinition? CharacterCreationVisibleRace02111(string ruleSetId, string id, bool admin)
        => CharacterCreationOriginDefinitions02111(ruleSetId).FirstOrDefault(x => x.DefinitionId == id && x.OriginKind == CharacterOriginKinds.Race
            && (admin || (x.IsPlayerVisible && x.Availability == CharacterOriginAvailabilityIds.Playable)));

    private static int CharacterCreationOriginBonus02111(Dictionary<string, int> origin, Dictionary<string, int>? subtype, string id)
        => (origin.TryGetValue(id, out var first) ? first : 0) + (subtype != null && subtype.TryGetValue(id, out var second) ? second : 0);

    private static Dictionary<string, int> CharacterCreationIntMap02111(IDictionary<string, object> payload, string key)
    {
        var source = PayloadReader.GetDictionary(payload, key) ?? new Dictionary<string, object>();
        return source.Where(x => !string.IsNullOrWhiteSpace(x.Key) && int.TryParse(Convert.ToString(x.Value), out _))
            .ToDictionary(x => x.Key, x => Convert.ToInt32(x.Value), StringComparer.Ordinal);
    }

    private Dictionary<string, int> CharacterCreationLanguageAllocation022Gate3(CharacterCreationDraft draft, CharacterOriginDefinition origin)
    {
        var source = draft.LanguageAllocation != null && draft.LanguageAllocation.Count > 0
            ? draft.LanguageAllocation
            : origin.Languages.ToDictionary(x => x, x => x.StartsWith("lang.race.", StringComparison.Ordinal) ? 3 : 5, StringComparer.Ordinal);
        var visible = _mongo.ContentDefinitionRecords.Find(x => x.Category == WorldLoreCalendarDefinitionCategories.Language && !x.IsArchived).ToList();
        var byName = visible.Where(x => !string.IsNullOrWhiteSpace(x.DisplayName)).GroupBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToDictionary(x => x.Key, x => x.First().Id, StringComparer.CurrentCultureIgnoreCase);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            var id = visible.Any(x => x.Id == pair.Key) ? pair.Key : byName.TryGetValue(pair.Key, out var resolved) ? resolved : pair.Key;
            result[id] = pair.Value;
        }
        return result;
    }

    private IEnumerable<string> CharacterCreationValidateLanguages022Gate3(CharacterCreationDraft draft, CharacterOriginDefinition origin)
    {
        var allocation = CharacterCreationLanguageAllocation022Gate3(draft, origin);
        if (allocation.Any(x => x.Value < 0 || x.Value > 5)) yield return "Уровень языка должен быть в диапазоне 0..5.";
        var languageIds = _mongo.ContentDefinitionRecords.Find(x => x.Category == WorldLoreCalendarDefinitionCategories.Language && !x.IsArchived).ToList().Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (allocation.Keys.Any(x => !languageIds.Contains(x))) yield return "Один из выбранных языков отсутствует в справочнике.";

        int Level(string id) => allocation.TryGetValue(id, out var value) ? value : 0;
        bool Pair(string first, string second) => (Level(first) == 5 && Level(second) == 3) || (Level(first) == 3 && Level(second) == 5);
        var profileError = draft.LanguageGrantProfileId switch
        {
            CharacterLanguageGrantProfileIds022Gate3.Lutwein when Level("lang.state.lutwein") != 5 || Level("lang.continental.vestar") < 1 || Level("lang.continental.vestar") > 3 => "Для лютвейнской среды нужны: Лютвейнский 5 и Вестар 1–3.",
            CharacterLanguageGrantProfileIds022Gate3.Rashid when Level("lang.state.rashid") != 5 || Level("lang.state.tarad") < 2 || Level("lang.continental.vestar") < 1 || Level("lang.continental.vestar") > 3 => "Для рашидской среды нужны: Рашидский 5, Тарадский не ниже 2 и Вестар 1–3.",
            CharacterLanguageGrantProfileIds022Gate3.Tarad when Level("lang.state.tarad") != 5 || Level("lang.state.rashid") < 2 || Level("lang.continental.vestar") < 1 || Level("lang.continental.vestar") > 3 => "Для тарадской среды нужны: Тарадский 5, Рашидский не ниже 2 и Вестар 1–3.",
            CharacterLanguageGrantProfileIds022Gate3.Lichtenburg when !Pair("lang.state.lutwein", "lang.state.kelreno") || Level("lang.continental.vestar") < 1 || Level("lang.continental.vestar") > 3 => "Для Лихтенбурга нужны Лютвейнский и Кёльренский на уровнях 5/3 в любом порядке, а также Вестар 1–3.",
            CharacterLanguageGrantProfileIds022Gate3.Bergenby when !Pair("lang.state.kolymin", "lang.state.lutwein") || Level("lang.continental.vestar") < 1 || Level("lang.continental.vestar") > 3 => "Для Бергенби нужны Колыминьский и Лютвейнский на уровнях 5/3 в любом порядке, а также Вестар 1–3.",
            CharacterLanguageGrantProfileIds022Gate3.Launtown when !Pair("lang.state.ostfront", "lang.state.kelreno") || Level("lang.continental.vestar") < 1 || Level("lang.continental.vestar") > 3 => "Для Лаунтауна нужны Остфронтский и Кёльренский на уровнях 5/3 в любом порядке, а также Вестар 1–3.",
            CharacterLanguageGrantProfileIds022Gate3.Fugu when Level("lang.culture.fugu") != 5 || Level("lang.continental.teyro") > 3 => "Для среды Фугу нужны: Фугу 5 и Тэйро 0–3.",
            CharacterLanguageGrantProfileIds022Gate3.Dzhau when Level("lang.continental.dzhau") != 5 => "Для местной среды Танаджау нужен Джау 5.",
            CharacterLanguageGrantProfileIds022Gate3.Istal when Level("lang.continental.istal") != 5 => "Для местной среды Истактлалли нужен Исталь 5.",
            CharacterLanguageGrantProfileIds022Gate3.Nalpa when Level("lang.continental.nalpa") != 5 => "Для местной среды Ухунинальпа нужен Нальпа 5.",
            CharacterLanguageGrantProfileIds022Gate3.Paven when Level("lang.continental.paven") != 5 => "Для местной среды Мотупавенуа нужен Павен 5.",
            CharacterLanguageGrantProfileIds022Gate3.Taura when Level("lang.continental.taura") != 5 => "Для местной среды Фенуатаура нужен Таура 5.",
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(profileError)) yield return profileError;

        var definitions = _mongo.ContentDefinitionRecords.Find(x => x.Category == WorldLoreCalendarDefinitionCategories.Language && !x.IsArchived).ToList().ToDictionary(x => x.Id, StringComparer.Ordinal);
        if (allocation.Any(x => x.Value > 3 && definitions.TryGetValue(x.Key, out var definition) && LanguageFieldList022Gate3(definition, "roles").Contains(LanguageRoleIds022Gate3.Religious) && x.Key != "lang.state.rashid" && x.Key != "lang.state.tarad"))
            yield return "Религиозный язык при обычном создании не может быть выше уровня 3.";
        if (allocation.Any(x => x.Value > 1 && definitions.TryGetValue(x.Key, out var definition) && LanguageFieldList022Gate3(definition, "roles").Contains(LanguageRoleIds022Gate3.Ancient)))
            yield return "Древний язык при обычном создании не может быть выше уровня 1.";
        if (origin.OriginKind == CharacterOriginKinds.Hybrid)
        {
            var parentRaceIds = new[] { origin.Parent1RaceId, origin.Parent2RaceId }.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
            var parentLanguages = definitions.Values
                .Where(x => LanguageFieldList022Gate3(x, "heritageRaceIds").Any(parentRaceIds.Contains))
                .Select(x => x.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (parentLanguages.Length > 0 && !parentLanguages.Any(x => allocation.TryGetValue(x, out var level) && level >= 1))
                yield return "Для гибрида хотя бы один язык родительского наследия должен иметь уровень не ниже 1.";
        }
    }

    private CharacterCreationPolicyState CharacterCreationPolicy02111(string campaignId)
        => _mongo.CharacterCreationPolicies.Find(x => x.CampaignId == campaignId).FirstOrDefault()
            ?? new CharacterCreationPolicyState { CampaignId = campaignId, Policy = CharacterCreationPolicyIds.RequireGmApproval };

    private string CharacterCreationCampaign02111(IDictionary<string, object> payload)
    {
        var campaignId = FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"));
        if (string.IsNullOrWhiteSpace(campaignId)) throw new ArgumentException("Выберите кампанию.");
        return campaignId;
    }

    private CharacterCreationDraft CharacterCreationDraft02111(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Черновик не выбран.");
        return _mongo.CharacterCreationDrafts.Find(x => x.Id == id).FirstOrDefault() ?? throw new KeyNotFoundException("Черновик не найден.");
    }

    private bool CharacterCreationCanManageAny02111(string actorId, string campaignId)
        => _campaignAuthorization.GetEffectiveCapabilities(actorId, campaignId).Contains(CampaignCapabilityIds.CharacterManageAnyInCampaign);

    private void CharacterCreationRequireDraftAccess02111(CommandContext context, UserAccount actor, CharacterCreationDraft draft, bool write)
    {
        var canManage = CharacterCreationCanManageAny02111(actor.Id, draft.CampaignId);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, draft.CampaignId,
            canManage ? CampaignCapabilityIds.CharacterManageAnyInCampaign : write ? CampaignCapabilityIds.CharacterManageOwned : CampaignCapabilityIds.CharacterViewPlayerSafe);
        if (!canManage && !string.Equals(draft.OwnerUserId, actor.Id, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Черновик недоступен.");
    }

    private string CharacterCampaign02111(string characterId)
        => _mongo.CharacterOwnerships.Find(x => x.CharacterId == characterId && !x.IsArchived).FirstOrDefault()?.CampaignId
            ?? throw new KeyNotFoundException("Кампания персонажа не найдена.");

    private void ApplyResolvedOrigin02111(CharacterCreationDraft draft, CharacterCreationPreview02111 preview)
    {
        draft.ResolvedOriginId = preview.Origin?.DefinitionId ?? string.Empty;
        draft.ResolvedOriginKind = preview.Origin?.OriginKind ?? string.Empty;
        draft.ResolvedOriginName = preview.Origin?.DisplayName ?? string.Empty;
        if (preview.Subtype == null && !string.IsNullOrWhiteSpace(draft.SubtypeId)) draft.SubtypeId = string.Empty;
    }

    private static Dictionary<string, object> CharacterCreationPolicyPayload02111(CharacterCreationPolicyState x) => new Dictionary<string, object>
    {
        ["campaignId"] = x.CampaignId, ["policy"] = x.Policy, ["policyDisplay"] = x.Policy == CharacterCreationPolicyIds.Free ? "Свободное создание" : x.Policy == CharacterCreationPolicyIds.GmOnly ? "Создание выполняет GM" : "Требуется одобрение GM",
        ["playerMayRenameFinalized"] = x.PlayerMayRenameFinalized, ["playerMayEditFinalizedBackstory"] = x.PlayerMayEditFinalizedBackstory, ["entityRevision"] = x.EntityRevision
    };

    private static Dictionary<string, object> CharacterOriginListPayload02111(CharacterOriginDefinition x) => new Dictionary<string, object>
    {
        ["originId"] = x.DefinitionId, ["originKind"] = x.OriginKind, ["displayName"] = x.DisplayName, ["description"] = x.PublicDescription,
        ["parent1RaceId"] = x.Parent1RaceId, ["parent2RaceId"] = x.Parent2RaceId, ["parentOrderMatters"] = x.ParentOrderMatters
    };

    private static Dictionary<string, object> CharacterSubtypePayload02111(CharacterOriginSubtypeDefinition x) => new Dictionary<string, object>
    {
        ["subtypeId"] = x.DefinitionId, ["originId"] = x.OriginId, ["originKind"] = x.OriginKind, ["displayName"] = x.DisplayName, ["description"] = x.PublicDescription
    };

    private static Dictionary<string, object> CharacterCreationDraftPayload02111(CharacterCreationDraft x, bool admin) => new Dictionary<string, object>
    {
        ["draftId"] = x.Id, ["campaignId"] = x.CampaignId, ["ownerUserId"] = admin ? x.OwnerUserId : string.Empty, ["status"] = x.Status,
        ["statusDisplay"] = CharacterCreationStatusDisplay02111(x.Status), ["displayName"] = x.DisplayName, ["backstory"] = x.PublicBackstory,
        ["parent1RaceId"] = x.Parent1RaceId, ["parent2RaceId"] = x.Parent2RaceId, ["resolvedOriginKind"] = x.ResolvedOriginKind,
        ["resolvedOriginId"] = x.ResolvedOriginId, ["resolvedOriginName"] = x.ResolvedOriginName, ["subtypeId"] = x.SubtypeId,
        ["heightCm"] = x.HeightCm, ["ageYears"] = x.AgeAnchorYears, ["ageAnchorWorldDate"] = x.AgeAnchorWorldDate,
        ["attributeAllocation"] = x.AttributeAllocation, ["subAttributeAllocation"] = x.SubAttributeAllocation,
        ["languageAllocation"] = x.LanguageAllocation, ["languageGrantProfileId"] = x.LanguageGrantProfileId, ["returnComment"] = x.ReturnComment,
        ["finalCharacterId"] = x.FinalCharacterId, ["entityRevision"] = x.EntityRevision, ["validationRevision"] = x.ValidationRevision,
        ["isReadOnly"] = x.Status == CharacterCreationDraftStatusIds.Submitted || x.Status == CharacterCreationDraftStatusIds.Finalized
    };

    private static Dictionary<string, object> CharacterCreationPreviewPayload02111(CharacterCreationPreview02111 x, bool admin)
    {
        var origin = x.Origin;
        return new Dictionary<string, object>
        {
            ["isValid"] = x.IsValid, ["errors"] = x.Errors.Cast<object>().ToArray(), ["warnings"] = x.Warnings.Cast<object>().ToArray(),
            ["resolvedOriginKind"] = origin?.OriginKind ?? string.Empty, ["resolvedOriginId"] = origin?.DefinitionId ?? string.Empty,
            ["resolvedOriginName"] = origin?.DisplayName ?? string.Empty, ["subtypeId"] = x.Subtype?.DefinitionId ?? string.Empty,
            ["subtypeName"] = x.Subtype?.DisplayName ?? string.Empty, ["description"] = origin?.PublicDescription ?? string.Empty,
            ["subtypeDescription"] = x.Subtype?.PublicDescription ?? string.Empty,
            ["strongSides"] = origin?.StrongSides.Cast<object>().ToArray() ?? Array.Empty<object>(), ["weakSides"] = origin?.WeakSides.Cast<object>().ToArray() ?? Array.Empty<object>(),
            ["traits"] = (x.ResolvedPhysiology?.PublicTraits ?? origin?.PublicTraits ?? new List<string>()).Cast<object>().ToArray(), ["languages"] = origin?.Languages.Cast<object>().ToArray() ?? Array.Empty<object>(),
            ["knowledge"] = origin?.KnowledgeGrants.Cast<object>().ToArray() ?? Array.Empty<object>(), ["attributeBonuses"] = origin?.AttributeBonuses ?? new Dictionary<string, int>(),
            ["subAttributeBonuses"] = origin?.SubAttributeBonuses ?? new Dictionary<string, int>(), ["minimumHeightCm"] = x.MinimumHeightCm,
            ["maximumHeightCm"] = x.MaximumHeightCm, ["minimumAgeYears"] = x.MinimumAgeYears, ["maximumAgeYears"] = x.MaximumAgeYears,
            ["adultAgeYears"] = x.ResolvedPhysiology?.AdultAgeYears ?? 0,
            ["averageLifespanYears"] = x.ResolvedPhysiology?.AverageLifespanYears ?? 0,
            ["maximumLifespanYears"] = x.ResolvedPhysiology?.MaximumLifespanYears ?? 0,
            ["baseHealth"] = x.ResolvedPhysiology?.BaseHealth ?? 0,
            ["naturalArmorRating"] = x.ResolvedPhysiology?.NaturalArmorRating ?? 0,
            ["naturalPenetrationResistance"] = x.ResolvedPhysiology?.NaturalPenetrationResistance ?? 0,
            ["bodyZones"] = (x.ResolvedPhysiology?.BodyZones ?? new List<BodyZoneDefinition>()).Select(v => (object)new Dictionary<string, object> { ["name"] = v.DisplayName, ["calledShotModifier"] = v.CalledShotAccuracyModifier }).ToArray(),
            ["equipmentFitWarning"] = x.ResolvedPhysiology?.EquipmentFit.PublicWarning ?? string.Empty,
            ["senses"] = (x.ResolvedPhysiology?.Senses ?? new List<RacialSenseDefinition>()).Select(v => (object)new Dictionary<string, object> { ["name"] = v.DisplayName, ["limitations"] = v.PublicLimitations }).ToArray(),
            ["movementAbilities"] = (x.ResolvedPhysiology?.MovementAbilities ?? new List<RacialMovementAbilityDefinition>()).Select(v => (object)new Dictionary<string, object> { ["name"] = v.DisplayName, ["mode"] = v.MovementMode }).ToArray(),
            ["naturalAttacks"] = (x.ResolvedPhysiology?.NaturalAttacks ?? new List<NaturalAttackDefinition>()).Select(v => (object)new Dictionary<string, object> { ["name"] = v.DisplayName, ["damage"] = v.Damage.Display, ["range"] = $"{v.RangeMeters:0.##} м", ["cooldown"] = v.CooldownRounds > 0 ? $"Перезарядка: {v.CooldownRounds} раунд." : string.Empty }).ToArray(),
            ["elementalResistances"] = (x.ResolvedPhysiology?.ElementalResistances ?? new List<ElementalResistanceTier>()).Select(v => (object)new Dictionary<string, object> { ["damageType"] = v.DamageTypeId, ["tier"] = v.Tier }).ToArray(),
            ["attributeBreakdown"] = x.AttributeBreakdown.ToDictionary(v => v.Key, v => (object)new Dictionary<string, object> { ["allocated"] = v.Value.Allocated, ["origin"] = v.Value.Origin, ["effective"] = v.Value.Effective }),
            ["subAttributeBreakdown"] = x.SubAttributeBreakdown.ToDictionary(v => v.Key, v => (object)new Dictionary<string, object> { ["allocated"] = v.Value.Allocated, ["origin"] = v.Value.Origin, ["effective"] = v.Value.Effective }),
            ["gmValidation"] = admin ? string.Join(" ", x.Errors) : string.Empty
        };
    }

    private static string CharacterCreationStatusDisplay02111(string status)
        => status == CharacterCreationDraftStatusIds.Submitted ? "Отправлен GM"
            : status == CharacterCreationDraftStatusIds.ReturnedForRevision ? "Возвращён на доработку"
            : status == CharacterCreationDraftStatusIds.Finalized ? "Персонаж создан"
            : status == CharacterCreationDraftStatusIds.Cancelled ? "Отменён" : "Черновик";

    private ResponseEnvelope Validation02111(string message) => Error(message, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
    private ResponseEnvelope Conflict02111(string message) => Error(message, ResponseStatus.Conflict, ErrorCode.Conflict);

    private void PublishCharacterCreationSync02111(string campaignId, string type, string entityType, string entityId, string operation, string actorId, string? requestId)
        => TryPublishSyncEvent(type, campaignId, entityType, entityId, operation, actorId, new Dictionary<string, object> { ["entityId"] = entityId }, requestId ?? string.Empty);

    private List<CharacterOriginDefinition> CharacterCreationOriginDefinitions02111(string ruleSetId)
    {
        var categories = new[] { "race_definition", "hybrid_definition" };
        return _mongo.ContentDefinitionRecords.Find(x => categories.Contains(x.Category) && !x.IsArchived
                && (x.RuleSetId == ruleSetId || x.RuleSetId == string.Empty || x.AllowedRuleSetIds.Contains(ruleSetId)))
            .ToList().Select(CharacterCreationOriginFromRecord02111).ToList();
    }

    private List<CharacterOriginSubtypeDefinition> CharacterCreationSubtypeDefinitions02111(string ruleSetId)
    {
        var categories = new[] { "subspecies_definition", "hybrid_subtype_definition" };
        return _mongo.ContentDefinitionRecords.Find(x => categories.Contains(x.Category) && !x.IsArchived
                && (x.RuleSetId == ruleSetId || x.RuleSetId == string.Empty || x.AllowedRuleSetIds.Contains(ruleSetId)))
            .ToList().Select(CharacterCreationSubtypeFromRecord02111).ToList();
    }

    private List<TitleDefinition> CharacterTitleDefinitions02111(string ruleSetId)
        => _mongo.ContentDefinitionRecords.Find(x => x.Category == "title_definition" && !x.IsArchived
                && (x.RuleSetId == ruleSetId || x.RuleSetId == string.Empty || x.AllowedRuleSetIds.Contains(ruleSetId)))
            .ToList().Select(x => new TitleDefinition
            {
                DefinitionId = CharacterCreationRecordKey02111(x),
                RuleSetId = FirstNonEmpty(x.RuleSetId, ruleSetId),
                DisplayName = FirstNonEmpty(x.DisplayName, x.Name),
                PublicDescription = x.PublicDescription,
                GmDescription = x.GMDescription,
                Category = CharacterCreationFieldString02111(x, "titleCategory"),
                IsPlayerVisible = CharacterCreationRecordPlayerVisible02111(x),
                SortOrder = CharacterCreationFieldInt02111(x, "sortOrder", 0)
            }).ToList();

    private CharacterOriginDefinition CharacterCreationOriginFromRecord02111(ContentDefinitionRecord x)
    {
        var parents = CharacterCreationFieldStrings02111(x, "parentLineages");
        return new CharacterOriginDefinition
        {
            DefinitionId = CharacterCreationRecordKey02111(x),
            RuleSetId = x.RuleSetId,
            OriginKind = x.Category == "hybrid_definition" ? CharacterOriginKinds.Hybrid : CharacterOriginKinds.Race,
            DisplayName = FirstNonEmpty(x.DisplayName, x.Name),
            PublicDescription = FirstNonEmpty(x.PublicDescription, CharacterCreationFieldString02111(x, "fullPlayerDescription")),
            GmDescription = x.GMDescription,
            Availability = CharacterCreationAvailability02111(CharacterCreationFieldString02111(x, "availabilityType")),
            IsPlayerVisible = CharacterCreationRecordPlayerVisible02111(x),
            Parent1RaceId = parents.ElementAtOrDefault(0) ?? string.Empty,
            Parent2RaceId = parents.ElementAtOrDefault(1) ?? string.Empty,
            ParentOrderMatters = CharacterCreationFieldBool02111(x, "parentOrderMatters"),
            MinimumHeightCm = CharacterCreationFieldInt02111(x, "minHeightCm", 50),
            MaximumHeightCm = CharacterCreationFieldInt02111(x, "maxHeightCm", 350),
            MinimumAgeYears = CharacterCreationFieldInt02111(x, "minAgeYears", 1),
            MaximumAgeYears = CharacterCreationFieldInt02111(x, "maxAgeYears", 120),
            AdultAgeYears = CharacterCreationFieldInt02111(x, "adultAgeYears", 18),
            AverageLifespanYears = CharacterCreationFieldInt02111(x, "averageLifespanYears", 75),
            MaximumLifespanYears = CharacterCreationFieldInt02111(x, "maximumLifespanYears", 120),
            BaseHealth = CharacterCreationFieldInt02111(x, "baseHealth", 100),
            NaturalArmorRating = CharacterCreationFieldInt02111(x, "naturalArmorRating", 1),
            NaturalPenetrationResistance = CharacterCreationFieldInt02111(x, "naturalPenetrationResistance", 1),
            StrongSides = CharacterCreationFieldStrings02111(x, "strongSides"),
            WeakSides = CharacterCreationFieldStrings02111(x, "weakSides"),
            PublicTraits = CharacterCreationFieldStrings02111(x, "publicTraits").Concat(CharacterCreationTraitNames02111(x)).Distinct(StringComparer.Ordinal).ToList(),
            TraitDefinitionIds = CharacterCreationFieldStrings02111(x, "traitIds"),
            GmOnlyTraits = CharacterCreationFieldStrings02111(x, "gmOnlyTraits"),
            Languages = CharacterCreationFieldStrings02111(x, "startingLanguages").Concat(CharacterCreationFieldStrings02111(x, "languageGrants")).Distinct(StringComparer.Ordinal).ToList(),
            KnowledgeGrants = CharacterCreationFieldStrings02111(x, "knowledgeGrants"),
            EquipmentCompatibilityTags = CharacterCreationFieldStrings02111(x, "equipmentFitTags"),
            BodyZones = CharacterCreationBodyZones02111(x),
            EquipmentFit = CharacterCreationEquipmentFit02111(x),
            Senses = CharacterCreationSenses02111(x),
            MovementAbilities = CharacterCreationMovementAbilities02111(x),
            NaturalAttacks = CharacterCreationNaturalAttacks02111(x),
            ElementalResistances = CharacterCreationElementalResistances02111(x),
            EnvironmentalToleranceModifiers = CharacterCreationEnvironmentalModifiers02111(x),
            AttributeBonuses = CharacterCreationFieldIntMap02111(x, "attributeBonuses", "defaultModifiers"),
            SubAttributeBonuses = CharacterCreationFieldIntMap02111(x, "subAttributeBonuses")
        };
    }

    private CharacterOriginSubtypeDefinition CharacterCreationSubtypeFromRecord02111(ContentDefinitionRecord x) => new CharacterOriginSubtypeDefinition
    {
        DefinitionId = CharacterCreationRecordKey02111(x),
        RuleSetId = x.RuleSetId,
        OriginId = FirstNonEmpty(CharacterCreationFieldString02111(x, "raceId"), CharacterCreationFieldString02111(x, "hybridId"), CharacterCreationFieldString02111(x, "originId")),
        OriginKind = x.Category == "hybrid_subtype_definition" ? CharacterOriginKinds.Hybrid : CharacterOriginKinds.Race,
        DisplayName = FirstNonEmpty(x.DisplayName, x.Name),
        PublicDescription = x.PublicDescription,
        IsPlayerVisible = CharacterCreationRecordPlayerVisible02111(x),
        IsGmOnly = string.Equals(x.VisibilityRule, ContentDefinitionVisibilityRules.GmOnly, StringComparison.OrdinalIgnoreCase),
        Availability = CharacterCreationAvailability02111(CharacterCreationFieldString02111(x, "availabilityType")),
        MinimumHeightCm = CharacterCreationFieldNullableInt02111(x, "minHeightCm"),
        MaximumHeightCm = CharacterCreationFieldNullableInt02111(x, "maxHeightCm"),
        MinimumAgeYears = CharacterCreationFieldNullableInt02111(x, "minAgeYears"),
        MaximumAgeYears = CharacterCreationFieldNullableInt02111(x, "maxAgeYears"),
        AdultAgeYears = CharacterCreationFieldNullableInt02111(x, "adultAgeYears"),
        AverageLifespanYears = CharacterCreationFieldNullableInt02111(x, "averageLifespanYears"),
        MaximumLifespanYears = CharacterCreationFieldNullableInt02111(x, "maximumLifespanYears"),
        BaseHealth = CharacterCreationFieldNullableInt02111(x, "baseHealth"),
        NaturalArmorRating = CharacterCreationFieldNullableInt02111(x, "naturalArmorRating"),
        NaturalPenetrationResistance = CharacterCreationFieldNullableInt02111(x, "naturalPenetrationResistance"),
        Parent1SubtypeId = CharacterCreationFieldString02111(x, "parent1SubtypeId"),
        Parent2SubtypeId = CharacterCreationFieldString02111(x, "parent2SubtypeId"),
        ElementalLineageId = CharacterCreationFieldString02111(x, "elementalLineageId"),
        InheritedAspectId = CharacterCreationFieldString02111(x, "inheritedAspectId"),
        FlightInheritancePermissionId = CharacterCreationFieldString02111(x, "flightInheritancePermissionId"),
        PublicTraits = CharacterCreationFieldStrings02111(x, "publicTraits").Concat(CharacterCreationTraitNames02111(x)).Distinct(StringComparer.Ordinal).ToList(),
        TraitDefinitionIds = CharacterCreationFieldStrings02111(x, "traitIds"),
        BodyZones = CharacterCreationFieldStrings02111(x, "bodyZoneIds").Count == 0 ? new List<BodyZoneDefinition>() : CharacterCreationBodyZones02111(x),
        EquipmentFit = string.IsNullOrWhiteSpace(CharacterCreationFieldString02111(x, "equipmentFitProfileId")) ? null : CharacterCreationEquipmentFit02111(x),
        Senses = CharacterCreationSenses02111(x),
        MovementAbilities = CharacterCreationMovementAbilities02111(x),
        NaturalAttacks = CharacterCreationNaturalAttacks02111(x),
        ElementalResistances = CharacterCreationElementalResistances02111(x),
        EnvironmentalToleranceModifiers = CharacterCreationEnvironmentalModifiers02111(x),
        AttributeBonuses = CharacterCreationFieldIntMap02111(x, "attributeBonuses", "defaultModifiers"),
        SubAttributeBonuses = CharacterCreationFieldIntMap02111(x, "subAttributeBonuses")
    };

    private List<string> CharacterCreationTraitNames02111(ContentDefinitionRecord owner)
        => CharacterCreationReferencedRecords02111(owner, "traitIds", "race_trait_definition")
            .Where(CharacterCreationRecordPlayerVisible02111)
            .Select(v => FirstNonEmpty(v.DisplayName, v.Name)).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

    private List<BodyZoneDefinition> CharacterCreationBodyZones02111(ContentDefinitionRecord owner)
    {
        var records = CharacterCreationReferencedRecords02111(owner, "bodyZoneIds", "body_zone_definition");
        if (records.Count == 0) return RacePhysiologyRules022Gate2.HumanoidZones();
        return records.Select(v => new BodyZoneDefinition
        {
            ZoneId = CharacterCreationRecordKey02111(v), DisplayName = FirstNonEmpty(v.DisplayName, v.Name),
            RandomWeight = CharacterCreationFieldDecimal02111(v, "randomWeight", 0m),
            CalledShotAccuracyModifier = CharacterCreationFieldInt02111(v, "calledShotAccuracyModifier", 0),
            NaturalPenetrationResistanceModifier = CharacterCreationFieldInt02111(v, "naturalPenetrationResistanceModifier", 0),
            CapabilityTags = CharacterCreationFieldStrings02111(v, "capabilityTags")
        }).ToList();
    }

    private RaceEquipmentFitProfile CharacterCreationEquipmentFit02111(ContentDefinitionRecord owner)
    {
        var record = CharacterCreationReferencedRecords02111(owner, "equipmentFitProfileId", "race_equipment_fit_profile").FirstOrDefault();
        return new RaceEquipmentFitProfile
        {
            SizeClass = record == null ? FirstNonEmpty(CharacterCreationFieldString02111(owner, "sizeClass"), "medium") : FirstNonEmpty(CharacterCreationFieldString02111(record, "sizeClass"), "medium"),
            MinimumEquipmentHeightCm = CharacterCreationFieldInt02111(record ?? owner, "minimumEquipmentHeightCm", CharacterCreationFieldInt02111(owner, "minHeightCm", 0)),
            MaximumEquipmentHeightCm = CharacterCreationFieldInt02111(record ?? owner, "maximumEquipmentHeightCm", CharacterCreationFieldInt02111(owner, "maxHeightCm", 0)),
            RequiredFitTags = CharacterCreationFieldStrings02111(record ?? owner, record == null ? "equipmentFitTags" : "fitTags"),
            BodyShapeTags = CharacterCreationFieldStrings02111(record ?? owner, "bodyShapeTags"),
            PublicWarning = FirstNonEmpty(CharacterCreationFieldString02111(record ?? owner, "publicWarning"), CharacterCreationFieldString02111(record ?? owner, "rules"))
        };
    }

    private List<RacialSenseDefinition> CharacterCreationSenses02111(ContentDefinitionRecord owner)
        => CharacterCreationReferencedRecords02111(owner, "racialSenseIds", "racial_sense_definition").Select(v => new RacialSenseDefinition
        {
            SenseId = CharacterCreationRecordKey02111(v), DisplayName = FirstNonEmpty(v.DisplayName, v.Name), Modality = CharacterCreationFieldString02111(v, "modality"),
            PassiveRangeMeters = CharacterCreationFieldDecimal02111(v, "passiveRangeMeters", 0m), FocusedRangeMeters = CharacterCreationFieldDecimal02111(v, "focusedRangeMeters", 0m),
            RangeMultiplier = CharacterCreationFieldDecimal02111(v, "rangeMultiplier", 1m), RequiresConnectedSurface = CharacterCreationFieldBool02111(v, "requiresConnectedSurface"),
            BlockedBySealedBarrier = CharacterCreationFieldBool02111(v, "blockedBySealedBarrier"), PenetratesWalls = CharacterCreationFieldBool02111(v, "penetratesWalls"),
            WorksInAbsoluteDarkness = CharacterCreationFieldBool02111(v, "worksInAbsoluteDarkness"), PublicLimitations = FirstNonEmpty(v.PublicDescription, CharacterCreationFieldString02111(v, "publicLimitations"))
        }).ToList();

    private List<RacialMovementAbilityDefinition> CharacterCreationMovementAbilities02111(ContentDefinitionRecord owner)
        => CharacterCreationReferencedRecords02111(owner, "movementAbilityIds", "racial_movement_ability_definition").Select(v => new RacialMovementAbilityDefinition
        {
            AbilityId = CharacterCreationRecordKey02111(v), DisplayName = FirstNonEmpty(v.DisplayName, v.Name), MovementMode = CharacterCreationFieldString02111(v, "movementMode"),
            ActionCostHalfActions = CharacterCreationFieldInt02111(v, "actionCostHalfActions", 1), SpeedMeters = CharacterCreationFieldDecimal02111(v, "speedMeters", 0m),
            MaximumLoadFraction = CharacterCreationFieldDecimal02111(v, "maximumLoadFraction", 0m), ReducedSpeedLoadFraction = CharacterCreationFieldDecimal02111(v, "reducedSpeedLoadFraction", 0m),
            ReducedSpeedMultiplier = CharacterCreationFieldDecimal02111(v, "reducedSpeedMultiplier", 1m), RequiredClearanceMeters = CharacterCreationFieldDecimal02111(v, "requiredClearanceMeters", 0m),
            MaximumIndependentTakeoffWindMetersPerSecond = CharacterCreationFieldDecimal02111(v, "maximumIndependentTakeoffWindMetersPerSecond", 0m), GlideRatio = CharacterCreationFieldDecimal02111(v, "glideRatio", 0m),
            CanHover = CharacterCreationFieldBool02111(v, "canHover"), RequiredBodyZoneIds = CharacterCreationFieldStrings02111(v, "requiredBodyZoneIds"), RequiredEquipmentFitTags = CharacterCreationFieldStrings02111(v, "requiredEquipmentFitTags")
        }).ToList();

    private List<NaturalAttackDefinition> CharacterCreationNaturalAttacks02111(ContentDefinitionRecord owner)
        => CharacterCreationReferencedRecords02111(owner, "naturalAttackIds", "natural_attack_definition").Select(v => new NaturalAttackDefinition
        {
            DefinitionId = CharacterCreationRecordKey02111(v), DisplayName = FirstNonEmpty(v.DisplayName, v.Name), AttackType = CharacterCreationFieldString02111(v, "attackType"),
            ActionCostHalfActions = CharacterCreationFieldInt02111(v, "actionCostHalfActions", 1), AccuracyModifier = CharacterCreationFieldInt02111(v, "accuracyModifier", 0), RangeMeters = CharacterCreationFieldDecimal02111(v, "rangeMeters", 0m),
            Damage = new DamageExpressionDefinition { DiceCount = CharacterCreationFieldInt02111(v, "diceCount", 1), DieSides = CharacterCreationFieldInt02111(v, "dieSides", 2), PerDieModifier = CharacterCreationFieldInt02111(v, "perDieModifier", 0), TotalModifier = CharacterCreationFieldInt02111(v, "totalModifier", 0) },
            DamageTypeIds = CharacterCreationFieldStrings02111(v, "damageTypeIds"), PhysicalPenetration = CharacterCreationFieldInt02111(v, "physicalPenetration", 0),
            FailedPenetrationDamageTransfer = CharacterCreationFieldDecimal02111(v, "failedPenetrationDamageTransfer", 0m), AreaShape = FirstNonEmpty(CharacterCreationFieldString02111(v, "areaShape"), "single"),
            AreaAngleDegrees = CharacterCreationFieldDecimal02111(v, "areaAngleDegrees", 0m), AreaWidthMeters = CharacterCreationFieldDecimal02111(v, "areaWidthMeters", 0m),
            CooldownRounds = CharacterCreationFieldInt02111(v, "cooldownRounds", 0), FriendlyFire = CharacterCreationFieldBool02111(v, "friendlyFire"),
            FateEligibleForHitCheck = CharacterCreationFieldBool02111(v, "fateEligibleForHitCheck"), FateEligibleForDamage = false,
            RequiredBodyZoneIds = CharacterCreationFieldStrings02111(v, "requiredBodyZoneIds"), AppliedConditionId = CharacterCreationFieldString02111(v, "appliedConditionId"), AppliedConditionRounds = CharacterCreationFieldInt02111(v, "appliedConditionRounds", 0)
        }).ToList();

    private List<ElementalResistanceTier> CharacterCreationElementalResistances02111(ContentDefinitionRecord owner)
        => CharacterCreationReferencedRecords02111(owner, "elementalResistanceIds", "elemental_resistance_definition").Select(v => new ElementalResistanceTier
        { DamageTypeId = CharacterCreationFieldString02111(v, "damageTypeId"), Tier = CharacterCreationFieldInt02111(v, "tier", 0) }).ToList();

    private List<EnvironmentalToleranceModifier> CharacterCreationEnvironmentalModifiers02111(ContentDefinitionRecord owner)
        => CharacterCreationReferencedRecords02111(owner, "environmentalToleranceModifierIds", "environmental_tolerance_modifier_definition").Select(v => new EnvironmentalToleranceModifier
        {
            SourceType = "origin", SourceDisplayName = FirstNonEmpty(v.DisplayName, v.Name), ComfortMinDeltaC = CharacterCreationFieldDecimal02111(v, "comfortMinDeltaC", 0m), ComfortMaxDeltaC = CharacterCreationFieldDecimal02111(v, "comfortMaxDeltaC", 0m),
            ColdSensitivityMultiplier = CharacterCreationFieldDecimal02111(v, "coldSensitivityMultiplier", 1m), HeatSensitivityMultiplier = CharacterCreationFieldDecimal02111(v, "heatSensitivityMultiplier", 1m),
            WetSensitivityMultiplier = CharacterCreationFieldDecimal02111(v, "wetSensitivityMultiplier", 1m), WindSensitivityMultiplier = CharacterCreationFieldDecimal02111(v, "windSensitivityMultiplier", 1m),
            HumiditySensitivityMultiplier = CharacterCreationFieldDecimal02111(v, "humiditySensitivityMultiplier", 1m), HypoxiaSensitivityMultiplier = CharacterCreationFieldDecimal02111(v, "hypoxiaSensitivityMultiplier", 1m),
            HydrationConsumptionMultiplier = CharacterCreationFieldDecimal02111(v, "hydrationConsumptionMultiplier", 1m), IsPlayerVisible = CharacterCreationRecordPlayerVisible02111(v)
        }).ToList();

    private List<ContentDefinitionRecord> CharacterCreationReferencedRecords02111(ContentDefinitionRecord owner, string fieldName, string category)
    {
        var ids = CharacterCreationFieldStrings02111(owner, fieldName);
        if (ids.Count == 0) return new List<ContentDefinitionRecord>();
        return _mongo.ContentDefinitionRecords.Find(v => v.Category == category && !v.IsArchived && (ids.Contains(v.StableKey) || ids.Contains(v.ShortCode) || ids.Contains(v.Id))).ToList()
            .OrderBy(v => ids.IndexOf(CharacterCreationRecordKey02111(v))).ToList();
    }

    private static string CharacterCreationRecordKey02111(ContentDefinitionRecord x) => FirstNonEmpty(x.StableKey, x.ShortCode, x.Id);
    private static bool CharacterCreationRecordPlayerVisible02111(ContentDefinitionRecord x)
        => string.Equals(x.VisibilityRule, ContentDefinitionVisibilityRules.Public, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.VisibilityRule, ContentDefinitionVisibilityRules.PlayerVisible, StringComparison.OrdinalIgnoreCase);
    private static string CharacterCreationFieldString02111(ContentDefinitionRecord x, string name)
        => x.CustomFields.TryGetValue(name, out var value) ? Convert.ToString(value)?.Trim() ?? string.Empty : string.Empty;
    private static int CharacterCreationFieldInt02111(ContentDefinitionRecord x, string name, int fallback)
        => int.TryParse(CharacterCreationFieldString02111(x, name), out var value) ? value : fallback;
    private static int? CharacterCreationFieldNullableInt02111(ContentDefinitionRecord x, string name)
        => int.TryParse(CharacterCreationFieldString02111(x, name), out var value) ? value : null;
    private static decimal CharacterCreationFieldDecimal02111(ContentDefinitionRecord x, string name, decimal fallback)
    {
        if (!x.CustomFields.TryGetValue(name, out var raw) || raw == null) return fallback;
        if (raw is decimal decimalValue) return decimalValue;
        if (raw is double doubleValue) return Convert.ToDecimal(doubleValue, System.Globalization.CultureInfo.InvariantCulture);
        if (raw is float floatValue) return Convert.ToDecimal(floatValue, System.Globalization.CultureInfo.InvariantCulture);
        if (raw is byte || raw is short || raw is int || raw is long || raw is sbyte || raw is ushort || raw is uint || raw is ulong)
            return Convert.ToDecimal(raw, System.Globalization.CultureInfo.InvariantCulture);
        var text = Convert.ToString(raw)?.Trim() ?? string.Empty;
        const System.Globalization.NumberStyles style = System.Globalization.NumberStyles.Float;
        if (decimal.TryParse(text, style, System.Globalization.CultureInfo.CurrentCulture, out var localValue)) return localValue;
        return decimal.TryParse(text, style, System.Globalization.CultureInfo.InvariantCulture, out var invariantValue) ? invariantValue : fallback;
    }
    private static bool CharacterCreationFieldBool02111(ContentDefinitionRecord x, string name)
        => bool.TryParse(CharacterCreationFieldString02111(x, name), out var value) && value;
    private static List<string> CharacterCreationFieldStrings02111(ContentDefinitionRecord x, string name)
    {
        if (!x.CustomFields.TryGetValue(name, out var raw) || raw == null) return new List<string>();
        if (raw is IEnumerable<object> objects) return objects.Select(Convert.ToString).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).ToList();
        return (Convert.ToString(raw) ?? string.Empty).Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
    }
    private static Dictionary<string, int> CharacterCreationFieldIntMap02111(ContentDefinitionRecord x, params string[] names)
    {
        foreach (var name in names)
        {
            if (!x.CustomFields.TryGetValue(name, out var raw) || raw == null) continue;
            if (raw is IDictionary<string, object> map) return map.Where(v => int.TryParse(Convert.ToString(v.Value), out _)).ToDictionary(v => v.Key, v => Convert.ToInt32(v.Value), StringComparer.Ordinal);
            var parsed = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in (Convert.ToString(raw) ?? string.Empty).Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split(new[] { '=', ':' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var value)) parsed[parts[0].Trim()] = value;
            }
            if (parsed.Count > 0) return parsed;
        }
        return new Dictionary<string, int>(StringComparer.Ordinal);
    }
    private static string CharacterCreationAvailability02111(string value)
    {
        var normalized = (value ?? string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        if (normalized == "playablewithcampaignpermission") return CharacterOriginAvailabilityIds.PlayableWithCampaignPermission;
        if (normalized == "npconly") return CharacterOriginAvailabilityIds.NpcOnly;
        if (normalized == "monsteronly") return CharacterOriginAvailabilityIds.MonsterOnly;
        if (normalized == "wildonly") return CharacterOriginAvailabilityIds.WildOnly;
        if (normalized == "hidden" || normalized == "gmonly" || normalized == "archived") return CharacterOriginAvailabilityIds.Hidden;
        return CharacterOriginAvailabilityIds.Playable;
    }

    private static string CharacterCreationPhysiologyValidationMessage022Gate2(string code)
        => code == "height_range_invalid" ? "Диапазон роста происхождения задан неверно."
            : code == "lifespan_order_invalid" ? "Возраст взросления и продолжительность жизни заданы неверно."
            : code == "base_health_required" ? "Для игрового происхождения требуется положительное базовое здоровье."
            : code == "natural_armor_rating_required" ? "Для игрового происхождения требуется естественная броня не ниже 1."
            : code == "natural_penetration_resistance_required" ? "Для игрового происхождения требуется естественная стойкость к пробитию не ниже 1."
            : code == "body_zone_weights_invalid" ? "Для происхождения не настроено распределение зон тела."
            : code == "natural_attack_dice_invalid" ? "Формула урона естественной атаки задана неверно."
            : code == "natural_attack_transfer_invalid" ? "Перенос урона естественной атаки должен быть от 0 до 1."
            : code == "natural_attack_cooldown_invalid" ? "Перезарядка естественной атаки не может быть отрицательной."
            : code == "movement_body_zone_requirement_missing" ? "Способ передвижения ссылается на отсутствующую зону тела."
            : "Механика физиологии происхождения настроена неверно.";

    private object[] CharacterCreationRuleDefinitions02111(string ruleSetId, string category, string idField)
    {
        var records = _mongo.ContentDefinitionRecords.Find(x => x.Category == category && !x.IsArchived
                && (x.RuleSetId == ruleSetId || x.RuleSetId == string.Empty || x.AllowedRuleSetIds.Contains(ruleSetId)))
            .ToList()
            .Where(CharacterCreationRecordPlayerVisible02111);

        if (string.Equals(ruleSetId, RuleSetIds.FantasyNriDefault, StringComparison.Ordinal))
        {
            records = category == "attribute_definition"
                ? records.Where(x => FantasyCreationAttributes02111.Contains(CharacterCreationRecordKey02111(x), StringComparer.Ordinal))
                : records.Where(x => FantasyCreationAttributes02111.Contains(CharacterCreationFieldString02111(x, "parentAttributeId"), StringComparer.Ordinal));
        }

        return records
            .GroupBy(CharacterCreationRecordKey02111, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(x => string.Equals(x.RuleSetId, ruleSetId, StringComparison.Ordinal))
                .ThenByDescending(x => x.Revision)
                .First())
            .OrderBy(x => CharacterCreationFieldInt02111(x, "displayOrder", 0))
            .ThenBy(x => Array.IndexOf(FantasyCreationAttributes02111, CharacterCreationRecordKey02111(x)))
            .Select(x => (object)new Dictionary<string, object>
            {
                [idField] = CharacterCreationRecordKey02111(x),
                ["displayName"] = FirstNonEmpty(x.DisplayName, x.Name),
                ["parentAttributeId"] = CharacterCreationFieldString02111(x, "parentAttributeId")
            }).ToArray();
    }
}

internal sealed class CharacterCreationPreview02111
{
    public bool IsValid { get; set; }
    public CharacterOriginDefinition? Origin { get; set; }
    public CharacterOriginSubtypeDefinition? Subtype { get; set; }
    public int MinimumHeightCm { get; set; }
    public int MaximumHeightCm { get; set; }
    public int MinimumAgeYears { get; set; }
    public int MaximumAgeYears { get; set; }
    public ResolvedOriginPhysiology? ResolvedPhysiology { get; set; }
    public List<string> Errors { get; } = new List<string>();
    public List<string> Warnings { get; } = new List<string>();
    public Dictionary<string, CharacterCreationValueBreakdown02111> AttributeBreakdown { get; } = new Dictionary<string, CharacterCreationValueBreakdown02111>(StringComparer.Ordinal);
    public Dictionary<string, CharacterCreationValueBreakdown02111> SubAttributeBreakdown { get; } = new Dictionary<string, CharacterCreationValueBreakdown02111>(StringComparer.Ordinal);
}

internal sealed class CharacterCreationValueBreakdown02111
{
    public int Allocated { get; set; }
    public int Origin { get; set; }
    public int Effective { get; set; }
}
