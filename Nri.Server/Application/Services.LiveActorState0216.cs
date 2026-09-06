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
    public ResponseEnvelope CharacterPlayerLiveStateGet(CommandContext context)
    {
        if (!LiveActorPlayerEnabled()) return LiveActorDisabled(context);
        var actor = GetCurrentAccount(context);
        var characterId = ResolvePlayerLiveCharacterId(context, actor);
        var view = BuildPlayerLiveView(characterId);
        _logger.Admin($"live_actor.player.get subjectType=character subjectId={characterId} revision={view.Revision}");
        return Ok("Текущее состояние загружено.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(view) });
    }

    public ResponseEnvelope CharacterAdminLiveStateGet(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context);
        RequireAdmin(context);
        var subject = ResolveSubject(context.Request.Payload);
        var view = BuildPlayerLiveView(subject.SubjectId, includeGm: true, subject.SubjectType);
        return Ok("Текущее состояние загружено для мастера.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(view, true) });
    }

    public ResponseEnvelope CharacterAdminLiveStateGetPlayerPreview(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context);
        RequireAdmin(context);
        var subject = ResolveSubject(context.Request.Payload);
        return Ok("Предпросмотр игрока загружен.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(subject.SubjectId, false, subject.SubjectType)) });
    }

    public ResponseEnvelope ActorAdminPartyBoardGet(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context);
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var requestedCampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty;
        var requestedSessionId = PayloadReader.GetString(payload, "sessionId") ?? string.Empty;
        var requestedGroupId = PayloadReader.GetString(payload, "activeGroupId") ?? string.Empty;
        var sessionFilter = Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)
            & Builders<CurrentSessionState>.Filter.Eq(x => x.Archived, false)
            & Builders<CurrentSessionState>.Filter.Eq(x => x.Status, CurrentSessionStatusIds.Active)
            & Builders<CurrentSessionState>.Filter.Ne(x => x.ActiveGroupId, string.Empty);
        if (!string.IsNullOrWhiteSpace(requestedCampaignId)) sessionFilter &= Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, requestedCampaignId);
        if (!string.IsNullOrWhiteSpace(requestedSessionId)) sessionFilter &= Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, requestedSessionId);
        var sessions = _mongo.CurrentSessions.Find(sessionFilter).SortByDescending(x => x.UpdatedAtUtc).Limit(1).ToList();
        if (sessions.Count == 0) return Error("Активная сессия не выбрана.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var session = sessions[0];
        var groupId = FirstNonEmpty(requestedGroupId, session.ActiveGroupId);
        if (string.IsNullOrWhiteSpace(groupId)) return Error("В активной сессии не выбрана группа.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var group = _mongo.CharacterGroups.Find(x => x.Id == groupId && !x.IsArchived && !x.Archived && x.IsActive).FirstOrDefault();
        if (group == null || !string.Equals(group.CampaignId, session.CampaignId, StringComparison.OrdinalIgnoreCase))
            return Error("Активная группа не найдена в выбранной сессии.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var members = _mongo.CharacterGroupMembers.Find(x => x.GroupId == group.Id && x.RemovedAtUtc == null && !x.Archived).SortBy(x => x.SortOrder).ToList();
        var duplicateIds = members.GroupBy(x => $"{x.EntityType}:{x.EntityId}", StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
        if (duplicateIds.Length > 0) return Error("В активной группе найдены повторяющиеся участники.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var rows = members.Select(member =>
        {
            var type = RuntimeSubjectTypeFromGroupMember(member.EntityType);
            return (object)LiveViewPayload(BuildPlayerLiveView(member.EntityId, true, type, member.DisplayName), true);
        }).ToArray();
        return Ok("Состояние активной группы загружено.", new Dictionary<string, object>
        {
            ["actors"] = rows,
            ["scope"] = new Dictionary<string, object>
            {
                ["campaignId"] = session.CampaignId,
                ["campaignName"] = "Текущая кампания",
                ["sessionId"] = session.SessionId,
                ["sessionName"] = session.Name,
                ["sceneId"] = session.CurrentSceneId,
                ["sceneName"] = session.CurrentSceneName,
                ["activeGroupId"] = group.Id,
                ["activeGroupName"] = group.Name,
                ["selectionMode"] = string.IsNullOrWhiteSpace(requestedCampaignId) && string.IsNullOrWhiteSpace(requestedSessionId)
                    ? "latest_active_group_session"
                    : "explicit_scope"
            }
        });
    }

    public ResponseEnvelope ActorAdminCapacityProfileSet(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context);
        var actor = RequireAdmin(context);
        var subject = ResolveSubject(context.Request.Payload);
        if (subject.SubjectType == RuntimeSubjectTypes.Character)
            return Error("Максимумы персонажа задаются в Character v2 BodyProfile.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var maximums = PayloadReader.GetDictionary(context.Request.Payload, "resourceMaximums")
            ?? throw new ArgumentException("Укажите максимумы ресурсов.");
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in maximums)
        {
            if (!decimal.TryParse(Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value) || value < 0)
                return Error($"Некорректный максимум ресурса «{ReadableResource(pair.Key)}».", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
            values[pair.Key] = value;
        }
        var profile = _mongo.RuntimeSubjectCapacityProfiles.Find(x => x.SubjectType == subject.SubjectType && x.SubjectId == subject.SubjectId).FirstOrDefault();
        if (profile == null)
        {
            profile = new RuntimeSubjectCapacityProfile { SubjectType = subject.SubjectType, SubjectId = subject.SubjectId, ResourceMaximums = values, UpdatedByUserId = actor.Id };
            _mongo.RuntimeSubjectCapacityProfiles.InsertOne(profile);
        }
        else
        {
            profile.ResourceMaximums = values; profile.Revision++; profile.UpdatedByUserId = actor.Id; profile.UpdatedUtc = DateTime.UtcNow;
            _mongo.RuntimeSubjectCapacityProfiles.ReplaceOne(x => x.Id == profile.Id, profile);
        }
        return Ok("Профиль ёмкости участника сохранён.", new Dictionary<string, object> { ["subjectType"] = subject.SubjectType, ["subjectId"] = subject.SubjectId, ["revision"] = profile.Revision });
    }

    public ResponseEnvelope ActorAdminResourceAdjust(CommandContext context) => ActorAdminResourceMutate(context, false);

    public ResponseEnvelope ActorAdminResourceSet(CommandContext context) => ActorAdminResourceMutate(context, true);

    private ResponseEnvelope ActorAdminResourceMutate(CommandContext context, bool forceSet)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context);
        var actor = RequireAdmin(context);
        var subject = ResolveSubject(context.Request.Payload);
        var resourceId = Required(PayloadReader.GetString(context.Request.Payload, "resourceDefinitionId"), "Выберите ресурс.");
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        var value = (decimal)(PayloadReader.GetDouble(context.Request.Payload, "value") ?? 0d);
        var mode = forceSet ? "set" : PayloadReader.GetString(context.Request.Payload, "mode") ?? "adjust";
        var reason = PayloadReader.GetString(context.Request.Payload, "reason") ?? "gm_adjustment";
        var state = GetOrCreateRuntimeState(subject, actor.Id);
        if (expectedRevision.HasValue && state.EntityRevision != expectedRevision.Value)
            return Error("Состояние изменилось. Обновите данные и повторите действие.", ResponseStatus.Conflict, ErrorCode.Conflict);
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any())
            return Ok("Операция уже применена.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(subject.SubjectId, true, subject.SubjectType), true), ["idempotentReplay"] = true });

        var resource = state.ResourceStates.FirstOrDefault(x => string.Equals(x.ResourceDefinitionId, resourceId, StringComparison.OrdinalIgnoreCase));
        if (resource == null) { resource = new RuntimeResourceState { ResourceDefinitionId = resourceId }; state.ResourceStates.Add(resource); }
        var before = resource.CurrentValue;
        resource.CurrentValue = string.Equals(mode, "set", StringComparison.OrdinalIgnoreCase) ? value : resource.CurrentValue + value;
        resource.LastChangeReasonCode = reason;
        resource.LastChangeSourceType = "gm";
        resource.LastChangeSourceId = actor.Id;
        resource.LastChangedAtUtc = DateTime.UtcNow;
        resource.Revision++;
        SaveRuntimeMutation(state, actor.Id, operationId, "resource", $"{ReadableResource(resourceId)}: {before} → {resource.CurrentValue}", before.ToString(), resource.CurrentValue.ToString(), true, resourceId);
        return Ok("Ресурс обновлён.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(subject.SubjectId, true, subject.SubjectType), true) });
    }

    public ResponseEnvelope ActorAdminEffectApply(CommandContext context)
    {
        if (!LiveActorAdminEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseRuntimeEffectsV1))) return LiveActorDisabled(context);
        var actor = RequireAdmin(context);
        var subject = ResolveSubject(context.Request.Payload);
        var runtime = GetOrCreateRuntimeState(subject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != runtime.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите причину применения эффекта.");
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.");
        var effect = new RuntimeEffectInstance
        {
            ConditionDefinitionId = Required(PayloadReader.GetString(context.Request.Payload, "conditionDefinitionId"), "Выберите эффект."),
            TargetSubject = subject,
            SourceSubject = new RuntimeSubjectReference { SubjectType = "user", SubjectId = actor.Id, DisplayNameSnapshot = actor.Login },
            PublicNameSnapshot = Required(PayloadReader.GetString(context.Request.Payload, "displayName"), "Укажите название эффекта."),
            PublicDescriptionSnapshot = PayloadReader.GetString(context.Request.Payload, "description") ?? string.Empty,
            GmNameSnapshot = PayloadReader.GetString(context.Request.Payload, "gmNote") ?? string.Empty,
            StackCount = Math.Max(1, PayloadReader.GetInt(context.Request.Payload, "stackCount") ?? 1),
            RemainingRounds = PayloadReader.GetInt(context.Request.Payload, "remainingRounds"),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            IsModifierReasonPlayerVisible = context.Request.Payload.ContainsKey("isModifierReasonPlayerVisible")
                ? PayloadReader.GetBool(context.Request.Payload, "isModifierReasonPlayerVisible")
                : PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            AppliedByUserId = actor.Id
        };
        effect.DurationMode = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "durationMode"), effect.RemainingRounds.HasValue ? "rounds" : "until_removed");
        var expiresAt = PayloadReader.GetString(context.Request.Payload, "expiresAtUtc");
        if (DateTime.TryParse(expiresAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedExpiry)) effect.ExpiresAtUtc = parsedExpiry.ToUniversalTime();
        var capabilityId = PayloadReader.GetString(context.Request.Payload, "capabilityDefinitionId");
        var capabilityModifier = (decimal)(PayloadReader.GetDouble(context.Request.Payload, "capabilityModifier") ?? 0d);
        if (!string.IsNullOrWhiteSpace(capabilityId) && capabilityModifier != 0) effect.CapabilityModifiers[capabilityId] = capabilityModifier;
        var capabilityModifiers = PayloadReader.GetDictionary(context.Request.Payload, "capabilityModifiers");
        if (capabilityModifiers != null)
            foreach (var pair in capabilityModifiers)
                if (decimal.TryParse(Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var modifier) && modifier != 0)
                    effect.CapabilityModifiers[pair.Key] = modifier;
        var resourceId = PayloadReader.GetString(context.Request.Payload, "resourceMaximumDefinitionId");
        var resourceModifier = (decimal)(PayloadReader.GetDouble(context.Request.Payload, "resourceMaximumModifier") ?? 0d);
        if (!string.IsNullOrWhiteSpace(resourceId) && resourceModifier != 0) effect.ResourceMaximumModifiers[resourceId] = resourceModifier;
        var resourceModifiers = PayloadReader.GetDictionary(context.Request.Payload, "resourceMaximumModifiers");
        if (resourceModifiers != null)
            foreach (var pair in resourceModifiers)
                if (decimal.TryParse(Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var modifier) && modifier != 0)
                    effect.ResourceMaximumModifiers[pair.Key] = modifier;
        effect.StackingPolicySnapshot = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "stackingPolicy"), "independent");
        var existingStack = string.Equals(effect.StackingPolicySnapshot, "stack", StringComparison.OrdinalIgnoreCase)
            ? _mongo.RuntimeEffectInstances.Find(x => x.TargetSubject.SubjectId == subject.SubjectId && x.ConditionDefinitionId == effect.ConditionDefinitionId && x.IsActive).FirstOrDefault()
            : null;
        if (existingStack != null)
        {
            existingStack.StackCount += effect.StackCount;
            if (effect.RemainingRounds.HasValue) existingStack.RemainingRounds = Math.Max(existingStack.RemainingRounds ?? 0, effect.RemainingRounds.Value);
            foreach (var pair in effect.CapabilityModifiers) existingStack.CapabilityModifiers[pair.Key] = pair.Value;
            foreach (var pair in effect.ResourceMaximumModifiers) existingStack.ResourceMaximumModifiers[pair.Key] = pair.Value;
            existingStack.Revision++; existingStack.UpdatedUtc = DateTime.UtcNow;
            _mongo.RuntimeEffectInstances.ReplaceOne(x => x.Id == existingStack.Id, existingStack);
            effect = existingStack;
        }
        else _mongo.RuntimeEffectInstances.InsertOne(effect);
        SaveRuntimeMutation(runtime, actor.Id, operationId, "effect", $"Применён эффект «{effect.PublicNameSnapshot}»", string.Empty, effect.PublicNameSnapshot, effect.IsPlayerVisible, gmOnlyDetail: reason);
        return Ok("Эффект применён.", new Dictionary<string, object> { ["effectId"] = effect.EffectInstanceId, ["liveState"] = LiveViewPayload(BuildPlayerLiveView(subject.SubjectId, true, subject.SubjectType), true) });
    }

    public ResponseEnvelope ActorAdminEffectRemove(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context);
        var actor = RequireAdmin(context);
        var effectId = Required(PayloadReader.GetString(context.Request.Payload, "effectInstanceId"), "Выберите эффект.");
        var effect = _mongo.RuntimeEffectInstances.Find(x => x.EffectInstanceId == effectId && x.IsActive).FirstOrDefault();
        if (effect == null) return Error("Эффект не найден.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var runtime = GetOrCreateRuntimeState(effect.TargetSubject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != runtime.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите причину снятия эффекта.");
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        effect.IsActive = false; effect.Revision++; effect.UpdatedUtc = DateTime.UtcNow;
        _mongo.RuntimeEffectInstances.ReplaceOne(x => x.Id == effect.Id, effect);
        SaveRuntimeMutation(runtime, actor.Id, operationId, "effect", $"Снят эффект «{effect.PublicNameSnapshot}»", effect.PublicNameSnapshot, string.Empty, effect.IsPlayerVisible, gmOnlyDetail: reason);
        return Ok("Эффект снят.");
    }

    public ResponseEnvelope CharacterPlayerActionPreview(CommandContext context)
    {
        if (!LiveActorPlayerEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseActionExecutionV1))) return LiveActorDisabled(context);
        var actor = GetCurrentAccount(context);
        var characterId = ResolvePlayerLiveCharacterId(context, actor);
        var state = GetOrCreateRuntimeState(new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = characterId }, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var actionId = Required(PayloadReader.GetString(context.Request.Payload, "actionDefinitionId"), "Выберите действие.");
        var action = state.ActionStates.FirstOrDefault(x => string.Equals(x.ActionDefinitionId, actionId, StringComparison.OrdinalIgnoreCase));
        if (action == null) return Error("Действие недоступно.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var reasons = ActionUnavailableReasons(state, action);
        return Ok("Предпросмотр действия готов.", new Dictionary<string, object>
        {
            ["preview"] = new Dictionary<string, object>
            {
                ["displayName"] = ReadableAction(action.ActionDefinitionId),
                ["available"] = reasons.Count == 0,
                ["reasons"] = reasons.Cast<object>().ToArray(),
                ["costs"] = action.ResourceCosts.Select(x => (object)$"{ReadableResource(x.Key)}: {x.Value:0.#}").ToArray(),
                ["cooldownRounds"] = action.CooldownRoundsOnUse,
                ["chargesAfterUse"] = action.MaximumCharges > 0 ? Math.Max(0, action.CurrentCharges - 1) : 0
            }
        });
    }

    public ResponseEnvelope CharacterPlayerWeaponReloadPreview(CommandContext context)
    {
        if (!LiveActorPlayerEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseOperationalLoadoutV1))) return LiveActorDisabled(context);
        var actor = GetCurrentAccount(context);
        var characterId = ResolvePlayerLiveCharacterId(context, actor);
        var state = GetOrCreateRuntimeState(new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = characterId }, actor.Id);
        var itemId = Required(PayloadReader.GetString(context.Request.Payload, "itemInstanceId"), "Выберите оружие.");
        var weapon = state.ItemOperationalStates.FirstOrDefault(x => x.ItemInstanceId == itemId);
        if (weapon?.AmmunitionFeed == null) return Error("Для выбранного предмета недоступна перезарядка.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var inventory = _mongo.CharacterInventoryProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()?.Profile;
        var reserve = inventory?.Items.Where(x => string.Equals(FirstNonEmpty(x.DefinitionId, x.ItemDefinitionId), weapon.AmmunitionFeed.LoadedAmmunitionDefinitionId, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity) ?? 0;
        var transfer = LiveActorRules.ReloadTransfer(weapon.AmmunitionFeed.LoadedQuantity, weapon.AmmunitionFeed.Capacity, reserve);
        return Ok("Предпросмотр перезарядки готов.", new Dictionary<string, object> { ["transfer"] = transfer, ["loadedAfter"] = weapon.AmmunitionFeed.LoadedQuantity + transfer, ["reserveAfter"] = reserve - transfer, ["canReload"] = transfer > 0 });
    }

    public ResponseEnvelope CharacterPlayerWeaponConsume(CommandContext context)
    {
        if (!LiveActorPlayerEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseOperationalLoadoutV1))) return LiveActorDisabled(context);
        var actor = GetCurrentAccount(context);
        var characterId = ResolvePlayerLiveCharacterId(context, actor);
        var state = GetOrCreateRuntimeState(new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = characterId }, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var itemId = Required(PayloadReader.GetString(context.Request.Payload, "itemInstanceId"), "Выберите оружие.");
        var quantity = Math.Max(1, PayloadReader.GetInt(context.Request.Payload, "quantity") ?? 1);
        var weapon = state.ItemOperationalStates.FirstOrDefault(x => x.ItemInstanceId == itemId);
        if (weapon?.AmmunitionFeed == null) return Error("Оружие не использует отслеживаемые боеприпасы.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var available = weapon.AmmunitionFeed.ChamberedQuantity + weapon.AmmunitionFeed.LoadedQuantity;
        if (available < quantity) return Error("В оружии недостаточно заряженных боеприпасов.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var fromChamber = Math.Min(quantity, weapon.AmmunitionFeed.ChamberedQuantity);
        weapon.AmmunitionFeed.ChamberedQuantity -= fromChamber;
        weapon.AmmunitionFeed.LoadedQuantity -= quantity - fromChamber;
        weapon.AmmunitionFeed.ReloadRequired = weapon.AmmunitionFeed.ChamberedQuantity + weapon.AmmunitionFeed.LoadedQuantity == 0;
        weapon.AmmunitionFeed.Revision++; weapon.LastOperationId = operationId; weapon.Revision++;
        SaveRuntimeMutation(state, actor.Id, operationId, "loadout", $"Израсходовано боеприпасов: {quantity}", available.ToString(), (available - quantity).ToString(), true, itemId);
        return Ok("Боеприпасы израсходованы.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(characterId)) });
    }

    public ResponseEnvelope CharacterPlayerActionExecute(CommandContext context)
    {
        if (!LiveActorPlayerEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseActionExecutionV1))) return LiveActorDisabled(context);
        var actor = GetCurrentAccount(context);
        var characterId = ResolvePlayerLiveCharacterId(context, actor);
        var subject = new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = characterId };
        var state = GetOrCreateRuntimeState(subject, actor.Id);
        var actionId = Required(PayloadReader.GetString(context.Request.Payload, "actionDefinitionId"), "Выберите действие.");
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var action = state.ActionStates.FirstOrDefault(x => string.Equals(x.ActionDefinitionId, actionId, StringComparison.OrdinalIgnoreCase));
        if (action == null)
            return Error("Действие сейчас недоступно.", ResponseStatus.Conflict, ErrorCode.Conflict);
        if (string.Equals(action.LastOperationId, operationId, StringComparison.Ordinal)) return Ok("Действие уже выполнено.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var unavailableReasons = ActionUnavailableReasons(state, action);
        if (unavailableReasons.Count > 0)
            return Error("Действие сейчас недоступно: " + string.Join("; ", unavailableReasons), ResponseStatus.Conflict, ErrorCode.Conflict);
        foreach (var resourceCost in action.ResourceCosts.Where(x => x.Value > 0))
        {
            var resource = state.ResourceStates.FirstOrDefault(x => string.Equals(x.ResourceDefinitionId, resourceCost.Key, StringComparison.OrdinalIgnoreCase));
            if (resource == null || resource.CurrentValue < resourceCost.Value) return Error("Недостаточно ресурса для действия.", ResponseStatus.Conflict, ErrorCode.Conflict);
        }
        foreach (var resourceCost in action.ResourceCosts.Where(x => x.Value > 0))
        {
            var resource = state.ResourceStates.First(x => string.Equals(x.ResourceDefinitionId, resourceCost.Key, StringComparison.OrdinalIgnoreCase));
            resource.CurrentValue -= resourceCost.Value; resource.Revision++;
        }
        if (action.AmmunitionUnitsOnUse > 0)
        {
            var weaponId = FirstNonEmpty(action.RequiredWeaponItemInstanceId, state.Loadout.ActiveWeaponItemInstanceId);
            var weapon = state.ItemOperationalStates.FirstOrDefault(x => x.ItemInstanceId == weaponId);
            if (weapon?.AmmunitionFeed == null) return Error("Для действия не выбрано заряженное оружие.", ResponseStatus.Conflict, ErrorCode.Conflict);
            var chamber = Math.Min(action.AmmunitionUnitsOnUse, weapon.AmmunitionFeed.ChamberedQuantity);
            weapon.AmmunitionFeed.ChamberedQuantity -= chamber;
            weapon.AmmunitionFeed.LoadedQuantity -= action.AmmunitionUnitsOnUse - chamber;
            weapon.AmmunitionFeed.ReloadRequired = weapon.AmmunitionFeed.ChamberedQuantity + weapon.AmmunitionFeed.LoadedQuantity == 0;
            weapon.AmmunitionFeed.Revision++; weapon.Revision++; weapon.LastOperationId = operationId;
        }
        if (action.MaximumCharges > 0) action.CurrentCharges--;
        action.RemainingRounds = Math.Max(action.RemainingRounds, action.CooldownRoundsOnUse);
        action.RemainingTurns = Math.Max(action.RemainingTurns, action.CooldownTurnsOnUse);
        action.LastUsedAtUtc = DateTime.UtcNow; action.LastOperationId = operationId; action.Revision++;
        SaveRuntimeMutation(state, actor.Id, operationId, "action", $"Выполнено действие «{ReadableAction(actionId)}»", "готово", "перезарядка", true, actionId);
        return Ok("Действие выполнено.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(characterId)) });
    }

    public ResponseEnvelope CharacterPlayerWeaponReload(CommandContext context)
    {
        if (!LiveActorPlayerEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseOperationalLoadoutV1))) return LiveActorDisabled(context);
        var actor = GetCurrentAccount(context);
        var characterId = ResolvePlayerLiveCharacterId(context, actor);
        var state = GetOrCreateRuntimeState(new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = characterId }, actor.Id);
        var itemId = Required(PayloadReader.GetString(context.Request.Payload, "itemInstanceId"), "Выберите оружие.");
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        var weapon = state.ItemOperationalStates.FirstOrDefault(x => x.ItemInstanceId == itemId);
        if (weapon?.AmmunitionFeed == null) return Error("Для выбранного предмета недоступна перезарядка.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (weapon.LastOperationId == operationId) return Ok("Перезарядка уже выполнена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var inventoryDocument = _mongo.CharacterInventoryProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault();
        var inventory = inventoryDocument?.Profile;
        var ammo = inventory?.Items.FirstOrDefault(x => string.Equals(FirstNonEmpty(x.DefinitionId, x.ItemDefinitionId), weapon.AmmunitionFeed.LoadedAmmunitionDefinitionId, StringComparison.OrdinalIgnoreCase) && x.Quantity > 0);
        if (ammo != null && !LiveActorRules.IsAmmunitionCompatible(weapon.AmmunitionFeed.CompatibleAmmunitionTags, ammo.SnapshotTags))
            return Error("Выбранный боеприпас больше не совместим с оружием.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var moved = LiveActorRules.ReloadTransfer(weapon.AmmunitionFeed.LoadedQuantity, weapon.AmmunitionFeed.Capacity, ammo?.Quantity ?? 0);
        if (moved <= 0) return Error("В инвентаре нет совместимых боеприпасов.", ResponseStatus.Conflict, ErrorCode.Conflict);
        weapon.AmmunitionFeed.LoadedQuantity += moved; weapon.AmmunitionFeed.ReloadRequired = weapon.AmmunitionFeed.LoadedQuantity == 0;
        weapon.AmmunitionFeed.Revision++; weapon.LastOperationId = operationId; weapon.Revision++;
        if (ammo != null) ammo.Quantity -= moved;
        if (inventoryDocument != null) { inventoryDocument.UpdatedUtc = DateTime.UtcNow; _mongo.CharacterInventoryProfiles.ReplaceOne(x => x.Id == inventoryDocument.Id, inventoryDocument); }
        SaveRuntimeMutation(state, actor.Id, operationId, "loadout", $"Оружие перезаряжено: +{moved}", string.Empty, moved.ToString(), true);
        return Ok("Оружие перезаряжено.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(characterId)) });
    }

    public ResponseEnvelope CharacterPlayerWeaponUnload(CommandContext context)
    {
        if (!LiveActorPlayerEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseOperationalLoadoutV1))) return LiveActorDisabled(context);
        var actor = GetCurrentAccount(context);
        var characterId = ResolvePlayerLiveCharacterId(context, actor);
        var state = GetOrCreateRuntimeState(new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = characterId }, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var weapon = state.ItemOperationalStates.FirstOrDefault(x => x.ItemInstanceId == Required(PayloadReader.GetString(context.Request.Payload, "itemInstanceId"), "Выберите оружие."));
        if (weapon?.AmmunitionFeed == null) return Error("В оружии нет извлекаемого боеприпаса.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (weapon.LastOperationId == operationId) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var returned = weapon.AmmunitionFeed.LoadedQuantity + weapon.AmmunitionFeed.ChamberedQuantity;
        var inventoryDocument = _mongo.CharacterInventoryProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault();
        var ammo = inventoryDocument?.Profile.Items.FirstOrDefault(x => string.Equals(FirstNonEmpty(x.DefinitionId, x.ItemDefinitionId), weapon.AmmunitionFeed.LoadedAmmunitionDefinitionId, StringComparison.OrdinalIgnoreCase));
        if (returned > 0 && ammo == null) return Error("Не найден стек боеприпасов для безопасного возврата в инвентарь.", ResponseStatus.Conflict, ErrorCode.Conflict);
        if (ammo != null) ammo.Quantity += returned;
        weapon.AmmunitionFeed.LoadedQuantity = 0; weapon.AmmunitionFeed.ChamberedQuantity = 0; weapon.AmmunitionFeed.ReloadRequired = true; weapon.AmmunitionFeed.Revision++; weapon.LastOperationId = operationId; weapon.Revision++;
        if (inventoryDocument != null) { inventoryDocument.UpdatedUtc = DateTime.UtcNow; _mongo.CharacterInventoryProfiles.ReplaceOne(x => x.Id == inventoryDocument.Id, inventoryDocument); }
        SaveRuntimeMutation(state, actor.Id, operationId, "loadout", $"Оружие разряжено: {returned}", returned.ToString(), "0", true);
        return Ok("Оружие разряжено.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(characterId)) });
    }

    public ResponseEnvelope CharacterPlayerLoadoutAdjust(CommandContext context)
    {
        if (!LiveActorPlayerEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseOperationalLoadoutV1))) return LiveActorDisabled(context);
        var actor = GetCurrentAccount(context);
        var characterId = ResolvePlayerLiveCharacterId(context, actor);
        var state = GetOrCreateRuntimeState(new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = characterId }, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var weaponId = PayloadReader.GetString(context.Request.Payload, "itemInstanceId") ?? PayloadReader.GetString(context.Request.Payload, "activeWeaponItemInstanceId");
        var attackProfileId = PayloadReader.GetString(context.Request.Payload, "attackProfileId");
        var ammunitionId = PayloadReader.GetString(context.Request.Payload, "ammunitionDefinitionId");
        var inventory = _mongo.CharacterInventoryProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()?.Profile;
        if (!string.IsNullOrWhiteSpace(weaponId))
        {
            if (inventory?.Items.Any(x => string.Equals(x.ItemId, weaponId, StringComparison.OrdinalIgnoreCase)) != true || state.ItemOperationalStates.All(x => x.ItemInstanceId != weaponId)) return Error("Выбранное оружие недоступно персонажу.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
            state.Loadout.ActiveWeaponItemInstanceId = weaponId;
        }
        if (!string.IsNullOrWhiteSpace(attackProfileId)) state.Loadout.ActiveAttackProfileId = attackProfileId;
        if (!string.IsNullOrWhiteSpace(ammunitionId))
        {
            var weapon = state.ItemOperationalStates.FirstOrDefault(x => x.ItemInstanceId == FirstNonEmpty(weaponId, state.Loadout.ActiveWeaponItemInstanceId));
            var ammo = inventory?.Items.FirstOrDefault(x => string.Equals(FirstNonEmpty(x.DefinitionId, x.ItemDefinitionId), ammunitionId, StringComparison.OrdinalIgnoreCase));
            if (weapon?.AmmunitionFeed == null || ammo == null || ammo.Quantity <= 0) return Error("Боеприпас отсутствует в инвентаре или несовместим с оружием.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
            if (!LiveActorRules.IsAmmunitionCompatible(weapon.AmmunitionFeed.CompatibleAmmunitionTags, ammo.SnapshotTags))
                return Error("Боеприпас несовместим с оружием.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
            if (weapon?.AmmunitionFeed != null) weapon.AmmunitionFeed.LoadedAmmunitionDefinitionId = ammunitionId;
        }
        var fireMode = PayloadReader.GetString(context.Request.Payload, "fireMode");
        if (!string.IsNullOrWhiteSpace(fireMode))
        {
            var weapon = state.ItemOperationalStates.FirstOrDefault(x => x.ItemInstanceId == FirstNonEmpty(weaponId, state.Loadout.ActiveWeaponItemInstanceId));
            if (weapon?.AmmunitionFeed == null) return Error("Режим огня недоступен.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
            weapon.AmmunitionFeed.FireMode = fireMode; state.Loadout.SelectedFireMode = fireMode; weapon.AmmunitionFeed.Revision++; weapon.Revision++;
        }
        state.Loadout.Revision++;
        SaveRuntimeMutation(state, actor.Id, operationId, "loadout", "Активное снаряжение изменено", string.Empty, "выбрано", true);
        return Ok("Активное снаряжение обновлено.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(characterId)) });
    }

    public ResponseEnvelope CharacterPlayerExecutionAdjust(CommandContext context)
    {
        if (!LiveActorPlayerEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseActionExecutionV1))) return LiveActorDisabled(context);
        var actor = GetCurrentAccount(context); var characterId = ResolvePlayerLiveCharacterId(context, actor);
        var state = GetOrCreateRuntimeState(new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = characterId }, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.ActionExecutionStates.Find(x => x.LastOperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var actionId = Required(PayloadReader.GetString(context.Request.Payload, "actionDefinitionId"), "Выберите действие.");
        if (state.ActionStates.All(x => !string.Equals(x.ActionDefinitionId, actionId, StringComparison.OrdinalIgnoreCase))) return Error("Действие недоступно персонажу.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var execution = _mongo.ActionExecutionStates.Find(x => x.ActorSubject.SubjectId == characterId && x.ActionDefinitionId == actionId && x.State != "completed").FirstOrDefault();
        var command = context.Request.Command;
        if (execution == null && command == CommandNames.CharacterPlayerActionPrepare)
        {
            execution = new ActionExecutionState { ActionDefinitionId = actionId, ActorSubject = new RuntimeSubjectReference { SubjectType = RuntimeSubjectTypes.Character, SubjectId = characterId }, State = "prepared", CurrentStage = 0, TotalStages = Math.Max(1, PayloadReader.GetInt(context.Request.Payload, "totalStages") ?? 1), LastOperationId = operationId, Revision = 1 };
            _mongo.ActionExecutionStates.InsertOne(execution);
        }
        else if (execution != null)
        {
            execution.State = command == CommandNames.CharacterPlayerActionInterrupt ? "interrupted" : command == CommandNames.CharacterPlayerActionStopSustain ? "completed" : "prepared";
            execution.LastOperationId = operationId; execution.Revision++; execution.UpdatedUtc = DateTime.UtcNow;
            _mongo.ActionExecutionStates.ReplaceOne(x => x.Id == execution.Id, execution);
        }
        else return Error("Выполнение действия не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        SaveRuntimeMutation(state, actor.Id, operationId, "execution", $"Состояние действия «{actionId}»: {execution.State}", string.Empty, execution.State, true);
        return Ok("Состояние выполнения обновлено.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(characterId)) });
    }

    public ResponseEnvelope ActorAdminLifeStateTransition(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context);
        var actor = RequireAdmin(context); var subject = ResolveSubject(context.Request.Payload); var state = GetOrCreateRuntimeState(subject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var target = Required(PayloadReader.GetString(context.Request.Payload, "stateCode"), "Выберите новое состояние.").ToLowerInvariant();
        if (!new[] { "active", "healthy", "impaired", "incapacitated", "unconscious", "dying", "stable", "dead", "destroyed", "custom" }.Contains(target)) return Error("Неизвестное жизненное состояние.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите причину перехода.");
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var before = state.LifeState.StateCode; var permissions = LiveActorRules.LifePermissions(target); state.LifeState.PreviousStateCode = before; state.LifeState.StateCode = target; state.LifeState.TransitionReasonCode = reason; state.LifeState.TransitionSourceType = "gm"; state.LifeState.TransitionSourceId = actor.Id; state.LifeState.SinceUtc = DateTime.UtcNow; state.LifeState.CanAct = permissions.CanAct; state.LifeState.CanReact = permissions.CanReact; state.LifeState.CanCommunicate = target is "active" or "healthy" or "impaired" or "stable"; state.LifeState.RequiresGmResolution = target is "dying" or "dead" or "destroyed" or "custom"; state.LifeState.Revision++;
        SaveRuntimeMutation(state, actor.Id, operationId, "life_state", $"Состояние: {ReadableLifeState(target)}", ReadableLifeState(before), ReadableLifeState(target), true, gmOnlyDetail: reason);
        return Ok("Состояние участника обновлено.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(subject.SubjectId, true, subject.SubjectType), true) });
    }

    public ResponseEnvelope ActorAdminActionStateAdjust(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context); var actor = RequireAdmin(context); var subject = ResolveSubject(context.Request.Payload); var state = GetOrCreateRuntimeState(subject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите причину корректировки действия.");
        var actionId = Required(PayloadReader.GetString(context.Request.Payload, "actionDefinitionId"), "Выберите действие."); var action = state.ActionStates.FirstOrDefault(x => x.ActionDefinitionId == actionId);
        if (action == null) { action = new ActionRuntimeState { ActionDefinitionId = actionId, IsEnabled = true }; state.ActionStates.Add(action); }
        action.RemainingRounds = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "remainingRounds") ?? action.RemainingRounds); action.RemainingTurns = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "remainingTurns") ?? action.RemainingTurns); action.MaximumCharges = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "maximumCharges") ?? action.MaximumCharges); action.CurrentCharges = Math.Max(0, Math.Min(action.MaximumCharges > 0 ? action.MaximumCharges : int.MaxValue, PayloadReader.GetInt(context.Request.Payload, "currentCharges") ?? action.CurrentCharges)); action.CooldownMode = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "cooldownMode"), action.CooldownMode); action.CooldownRoundsOnUse = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "cooldownRoundsOnUse") ?? action.CooldownRoundsOnUse); action.CooldownTurnsOnUse = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "cooldownTurnsOnUse") ?? action.CooldownTurnsOnUse); var costResourceId = PayloadReader.GetString(context.Request.Payload, "costResourceDefinitionId"); var costAmount = (decimal)(PayloadReader.GetDouble(context.Request.Payload, "costAmount") ?? 0d); if (!string.IsNullOrWhiteSpace(costResourceId)) { if (costAmount > 0) action.ResourceCosts[costResourceId] = costAmount; else action.ResourceCosts.Remove(costResourceId); } action.RestResetPolicy = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "restResetPolicy"), action.RestResetPolicy); action.AmmunitionUnitsOnUse = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "ammunitionUnitsOnUse") ?? action.AmmunitionUnitsOnUse); action.RequiredWeaponItemInstanceId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "requiredWeaponItemInstanceId"), action.RequiredWeaponItemInstanceId); action.SourceType = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "sourceType"), action.SourceType); action.SourceDefinitionId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "sourceDefinitionId"), action.SourceDefinitionId); action.IsPrepared = context.Request.Payload.ContainsKey("isPrepared") ? PayloadReader.GetBool(context.Request.Payload, "isPrepared") : action.IsPrepared; action.IsEnabled = context.Request.Payload.ContainsKey("isEnabled") ? PayloadReader.GetBool(context.Request.Payload, "isEnabled") : action.IsEnabled; action.Revision++;
        SaveRuntimeMutation(state, actor.Id, operationId, "action", $"Действие «{actionId}» скорректировано", string.Empty, "обновлено", true, gmOnlyDetail: reason);
        return Ok("Состояние действия обновлено.");
    }

    public ResponseEnvelope ActorAdminWeaponStateAdjust(CommandContext context)
    {
        if (!LiveActorAdminEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseOperationalLoadoutV1))) return LiveActorDisabled(context);
        var actor = RequireAdmin(context);
        var subject = ResolveSubject(context.Request.Payload);
        var state = GetOrCreateRuntimeState(subject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var itemId = Required(PayloadReader.GetString(context.Request.Payload, "itemInstanceId"), "Выберите предмет снаряжения.");
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите причину корректировки.");
        var weapon = state.ItemOperationalStates.FirstOrDefault(x => x.ItemInstanceId == itemId);
        if (weapon == null)
        {
            weapon = new ItemOperationalState { ItemInstanceId = itemId };
            state.ItemOperationalStates.Add(weapon);
        }
        weapon.IsEquipped = context.Request.Payload.ContainsKey("isEquipped") ? PayloadReader.GetBool(context.Request.Payload, "isEquipped") : weapon.IsEquipped;
        weapon.IsActive = context.Request.Payload.ContainsKey("isActive") ? PayloadReader.GetBool(context.Request.Payload, "isActive") : weapon.IsActive;
        weapon.IsJammed = context.Request.Payload.ContainsKey("isJammed") ? PayloadReader.GetBool(context.Request.Payload, "isJammed") : weapon.IsJammed;
        weapon.IsBroken = context.Request.Payload.ContainsKey("isBroken") ? PayloadReader.GetBool(context.Request.Payload, "isBroken") : weapon.IsBroken;
        weapon.DurabilityCurrent = (decimal)(PayloadReader.GetDouble(context.Request.Payload, "durabilityCurrent") ?? (double)weapon.DurabilityCurrent);
        weapon.DurabilityMaximum = (decimal)(PayloadReader.GetDouble(context.Request.Payload, "durabilityMaximum") ?? (double)weapon.DurabilityMaximum);
        if (context.Request.Payload.ContainsKey("capacity") || context.Request.Payload.ContainsKey("loadedQuantity"))
        {
            weapon.AmmunitionFeed ??= new AmmunitionFeedState();
            weapon.AmmunitionFeed.Capacity = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "capacity") ?? weapon.AmmunitionFeed.Capacity);
            weapon.AmmunitionFeed.LoadedQuantity = Math.Max(0, Math.Min(weapon.AmmunitionFeed.Capacity, PayloadReader.GetInt(context.Request.Payload, "loadedQuantity") ?? weapon.AmmunitionFeed.LoadedQuantity));
            weapon.AmmunitionFeed.ChamberedQuantity = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "chamberedQuantity") ?? weapon.AmmunitionFeed.ChamberedQuantity);
            weapon.AmmunitionFeed.LoadedAmmunitionDefinitionId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "ammunitionDefinitionId"), weapon.AmmunitionFeed.LoadedAmmunitionDefinitionId);
            weapon.AmmunitionFeed.FireMode = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "fireMode"), weapon.AmmunitionFeed.FireMode);
            weapon.AmmunitionFeed.CompatibleAmmunitionTags = GetStringList(context.Request.Payload, "compatibleAmmunitionTags");
            var sourceItemInstanceIds = GetStringList(context.Request.Payload, "sourceItemInstanceIds");
            if (sourceItemInstanceIds.Count > 0) weapon.AmmunitionFeed.SourceItemInstanceIds = sourceItemInstanceIds;
            weapon.AmmunitionFeed.Revision++;
        }
        weapon.LastOperationId = operationId;
        weapon.Revision++;
        SaveRuntimeMutation(state, actor.Id, operationId, "loadout", $"Состояние снаряжения скорректировано: {reason}", string.Empty, "обновлено", true);
        return Ok("Состояние снаряжения обновлено.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(subject.SubjectId, true, subject.SubjectType), true) });
    }

    public ResponseEnvelope ActorAdminExecutionAdjust(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context); var actor = RequireAdmin(context); var subject = ResolveSubject(context.Request.Payload);
        var state = GetOrCreateRuntimeState(subject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите причину корректировки выполнения.");
        var executionId = PayloadReader.GetString(context.Request.Payload, "executionId") ?? string.Empty; var execution = _mongo.ActionExecutionStates.Find(x => x.ExecutionId == executionId).FirstOrDefault();
        if (execution == null) return Error("Выполнение не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!string.Equals(execution.ActorSubject.SubjectId, subject.SubjectId, StringComparison.OrdinalIgnoreCase)) return Error("Выполнение не принадлежит выбранному участнику.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        execution.State = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "state"), execution.State); execution.CurrentStage = PayloadReader.GetInt(context.Request.Payload, "currentStage") ?? execution.CurrentStage; execution.RemainingRounds = PayloadReader.GetInt(context.Request.Payload, "remainingRounds") ?? execution.RemainingRounds; execution.Revision++; execution.UpdatedUtc = DateTime.UtcNow; _mongo.ActionExecutionStates.ReplaceOne(x => x.Id == execution.Id, execution);
        SaveRuntimeMutation(state, actor.Id, operationId, "execution", "Выполнение действия скорректировано", string.Empty, execution.State, true, gmOnlyDetail: reason); return Ok("Выполнение обновлено.");
    }

    public ResponseEnvelope ActorAdminLoadoutAdjust(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context); var actor = RequireAdmin(context); var subject = ResolveSubject(context.Request.Payload); var state = GetOrCreateRuntimeState(subject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision"); if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите причину корректировки снаряжения.");
        state.Loadout.ActiveWeaponItemInstanceId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "activeWeaponItemInstanceId"), state.Loadout.ActiveWeaponItemInstanceId); state.Loadout.ActiveAttackProfileId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "activeAttackProfileId"), state.Loadout.ActiveAttackProfileId); state.Loadout.GripMode = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "gripMode"), state.Loadout.GripMode); state.Loadout.SelectedFireMode = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "fireMode"), state.Loadout.SelectedFireMode); state.Loadout.SafetyState = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "safetyState"), state.Loadout.SafetyState); state.Loadout.IsReadied = context.Request.Payload.ContainsKey("isReadied") ? PayloadReader.GetBool(context.Request.Payload, "isReadied") : state.Loadout.IsReadied; state.Loadout.Revision++;
        SaveRuntimeMutation(state, actor.Id, operationId, "loadout", "Снаряжение скорректировано мастером", string.Empty, "обновлено", true, gmOnlyDetail: reason); return Ok("Снаряжение обновлено.");
    }

    public ResponseEnvelope ActorAdminRuntimeAdvanceRound(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context);
        var actor = RequireAdmin(context); var subject = ResolveSubject(context.Request.Payload); var state = GetOrCreateRuntimeState(subject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите причину продвижения раунда.");
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        foreach (var action in state.ActionStates) { if (action.RemainingRounds > 0) action.RemainingRounds--; action.Revision++; }
        var effects = _mongo.RuntimeEffectInstances.Find(x => x.TargetSubject.SubjectId == subject.SubjectId && x.IsActive).ToList();
        foreach (var effect in effects.Where(x => x.RemainingRounds.HasValue))
        {
            effect.RemainingRounds = Math.Max(0, effect.RemainingRounds!.Value - 1);
            if (effect.RemainingRounds == 0) { effect.IsActive = false; effect.IsExpired = true; }
            effect.Revision++; effect.UpdatedUtc = DateTime.UtcNow; _mongo.RuntimeEffectInstances.ReplaceOne(x => x.Id == effect.Id, effect);
        }
        var executions = _mongo.ActionExecutionStates.Find(x => x.ActorSubject.SubjectId == subject.SubjectId && x.State != "completed" && x.State != "cancelled" && x.State != "interrupted").ToList();
        foreach (var execution in executions)
        {
            if (execution.RemainingRounds > 0) execution.RemainingRounds--;
            execution.Revision++; execution.UpdatedUtc = DateTime.UtcNow; _mongo.ActionExecutionStates.ReplaceOne(x => x.Id == execution.Id, execution);
        }
        SaveRuntimeMutation(state, actor.Id, operationId, "round", $"Продвинут игровой раунд: {reason}", string.Empty, "следующий раунд", true);
        return Ok("Раунд продвинут.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(subject.SubjectId, true, subject.SubjectType), true) });
    }

    public ResponseEnvelope ActorAdminRuntimeApplyRest(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context);
        var actor = RequireAdmin(context); var subject = ResolveSubject(context.Request.Payload); var state = GetOrCreateRuntimeState(subject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var restType = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "restType"), "short").ToLowerInvariant();
        if (restType != "short" && restType != "long") return Error("Неизвестный тип отдыха.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите основание завершения отдыха.");
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        foreach (var action in state.ActionStates)
        {
            var resets = restType == "long" || string.Equals(action.RestResetPolicy, "short_rest", StringComparison.OrdinalIgnoreCase);
            if (!resets) continue;
            action.CurrentCharges = action.MaximumCharges; action.RemainingRounds = 0; action.RemainingTurns = 0;
            if (restType == "short") action.UsesSinceShortRest = 0; else { action.UsesSinceShortRest = 0; action.UsesSinceLongRest = 0; }
            action.Revision++;
        }
        var resourceValues = PayloadReader.GetDictionary(context.Request.Payload, "resourceValues");
        if (resourceValues != null)
        {
            foreach (var pair in resourceValues)
            {
                var resource = state.ResourceStates.FirstOrDefault(x => string.Equals(x.ResourceDefinitionId, pair.Key, StringComparison.OrdinalIgnoreCase));
                if (resource == null || !decimal.TryParse(Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var target)) continue;
                resource.CurrentValue = Math.Max(0, target); resource.LastChangeReasonCode = reason; resource.LastChangeSourceType = "rest"; resource.Revision++;
            }
        }
        SaveRuntimeMutation(state, actor.Id, operationId, "rest", $"Завершён отдых: {(restType == "long" ? "длительный" : "короткий")}", string.Empty, reason, true);
        return Ok("Восстановление после отдыха применено.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(subject.SubjectId, true, subject.SubjectType), true) });
    }

    public ResponseEnvelope ActorAdminReservationAdjust(CommandContext context)
    {
        if (!LiveActorAdminEnabled() || !_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseActionExecutionV1))) return LiveActorDisabled(context);
        var actor = RequireAdmin(context); var subject = ResolveSubject(context.Request.Payload); var state = GetOrCreateRuntimeState(subject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Операция уже применена.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var executionId = Required(PayloadReader.GetString(context.Request.Payload, "executionId"), "Выберите выполнение действия.");
        var resourceId = Required(PayloadReader.GetString(context.Request.Payload, "resourceDefinitionId"), "Выберите ресурс.");
        var mode = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "mode"), "reserve").ToLowerInvariant();
        var amount = Math.Max(0, (decimal)(PayloadReader.GetDouble(context.Request.Payload, "amount") ?? 0));
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите причину изменения резерва.");
        var reservation = _mongo.ResourceReservationStates.Find(x => x.SubjectId == subject.SubjectId && x.ExecutionId == executionId && x.ResourceDefinitionId == resourceId && x.State == "active").FirstOrDefault();
        if (mode == "reserve")
        {
            var resource = state.ResourceStates.FirstOrDefault(x => string.Equals(x.ResourceDefinitionId, resourceId, StringComparison.OrdinalIgnoreCase));
            var alreadyReserved = _mongo.ResourceReservationStates.Find(x => x.SubjectId == subject.SubjectId && x.ResourceDefinitionId == resourceId && x.State == "active").ToList().Sum(x => x.ReservedAmount);
            if (resource == null || resource.CurrentValue - alreadyReserved < amount) return Error("Недостаточно свободного ресурса для резервирования.", ResponseStatus.Conflict, ErrorCode.Conflict);
            reservation = new ResourceReservationState { SubjectId = subject.SubjectId, ResourceDefinitionId = resourceId, ExecutionId = executionId, ReservedAmount = amount, ReleasePolicy = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "releasePolicy"), "release"), State = "active", Revision = 1 };
            _mongo.ResourceReservationStates.InsertOne(reservation);
        }
        else
        {
            if (reservation == null) return Error("Активный резерв не найден.", ResponseStatus.NotFound, ErrorCode.NotFound);
            if (mode == "commit")
            {
                var resource = state.ResourceStates.FirstOrDefault(x => string.Equals(x.ResourceDefinitionId, resourceId, StringComparison.OrdinalIgnoreCase));
                if (resource == null || resource.CurrentValue < reservation.ReservedAmount) return Error("Ресурс для списания недоступен.", ResponseStatus.Conflict, ErrorCode.Conflict);
                resource.CurrentValue -= reservation.ReservedAmount; resource.Revision++; reservation.CommittedAmount = reservation.ReservedAmount;
            }
            reservation.State = mode == "commit" ? "committed" : "released"; reservation.Revision++; reservation.UpdatedUtc = DateTime.UtcNow;
            _mongo.ResourceReservationStates.ReplaceOne(x => x.Id == reservation.Id, reservation);
        }
        SaveRuntimeMutation(state, actor.Id, operationId, "reservation", $"Резерв ресурса: {reason}", string.Empty, mode, true, resourceId);
        return Ok("Резерв ресурса обновлён.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(subject.SubjectId, true, subject.SubjectType), true) });
    }

    public ResponseEnvelope ActorAdminLiveStateCompensate(CommandContext context)
    {
        if (!LiveActorAdminEnabled()) return LiveActorDisabled(context);
        var actor = RequireAdmin(context); var subject = ResolveSubject(context.Request.Payload); var state = GetOrCreateRuntimeState(subject, actor.Id);
        var expectedRevision = PayloadReader.GetLong(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != state.EntityRevision) return Error("Состояние изменилось. Обновите данные.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var originalEventId = Required(PayloadReader.GetString(context.Request.Payload, "eventId"), "Выберите ошибочное изменение.");
        var reason = Required(PayloadReader.GetString(context.Request.Payload, "reason"), "Укажите причину исправления.");
        var operationId = Required(FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "operationId"), context.Request.RequestId ?? string.Empty), "Не указан идентификатор операции.");
        if (_mongo.LiveStateEvents.Find(x => x.OperationId == operationId).Any()) return Ok("Исправление уже применено.", new Dictionary<string, object> { ["idempotentReplay"] = true });
        var original = _mongo.LiveStateEvents.Find(x => x.Id == originalEventId && x.SubjectId == subject.SubjectId).FirstOrDefault();
        if (original == null) return Error("Исходное событие не найдено.", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!string.Equals(original.Category, "resource", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(original.TargetKey)) return Error("Для этого события автоматическая компенсация недоступна.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (!decimal.TryParse(original.OldSummary, out var oldValue) || !decimal.TryParse(original.NewSummary, out var newValue)) return Error("Исходное изменение нельзя безопасно компенсировать.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var resource = state.ResourceStates.FirstOrDefault(x => string.Equals(x.ResourceDefinitionId, original.TargetKey, StringComparison.OrdinalIgnoreCase));
        if (resource == null) return Error("Изменённый ресурс не найден.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var before = resource.CurrentValue; resource.CurrentValue += oldValue - newValue; resource.Revision++;
        SaveRuntimeMutation(state, actor.Id, operationId, "compensation", $"Исправление: {reason}", before.ToString(), resource.CurrentValue.ToString(), true, original.TargetKey, original.Id);
        return Ok("Компенсирующее изменение применено; исходное событие сохранено.", new Dictionary<string, object> { ["liveState"] = LiveViewPayload(BuildPlayerLiveView(subject.SubjectId, true, subject.SubjectType), true), ["originalEventPreserved"] = true });
    }

    public ResponseEnvelope ActorLiveHistoryGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = IsAdmin(actor);
        if (admin ? !LiveActorAdminEnabled() : !LiveActorPlayerEnabled()) return LiveActorDisabled(context);
        var subjectId = admin ? ResolveSubject(context.Request.Payload).SubjectId : ResolvePlayerLiveCharacterId(context, actor);
        var filter = Builders<LiveStateEventRecord>.Filter.Eq(x => x.SubjectId, subjectId);
        if (!admin) filter &= Builders<LiveStateEventRecord>.Filter.Eq(x => x.IsPlayerVisible, true);
        var rows = _mongo.LiveStateEvents.Find(filter).SortByDescending(x => x.CreatedUtc).Limit(100).ToList().Select(x => EventPayload(x, admin)).Cast<object>().ToArray();
        return Ok("История состояния загружена.", new Dictionary<string, object> { ["items"] = rows });
    }

    public ResponseEnvelope CharacterPlayerCompanionsLiveSummaryGet(CommandContext context)
    {
        if (!LiveActorPlayerEnabled()) return LiveActorDisabled(context);
        var actor = GetCurrentAccount(context);
        var characterId = ResolvePlayerLiveCharacterId(context, actor);
        var companions = _mongo.CharacterCompanionProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()?.Profile?.Companions
            ?? new List<CharacterCompanionProfileValue>();
        var rows = companions.Where(x => x.IsPlayerVisible && !x.IsArchived).Select(x =>
        {
            var subject = new RuntimeSubjectReference
            {
                SubjectType = RuntimeSubjectTypes.Companion,
                SubjectId = x.CompanionId,
                DisplayNameSnapshot = x.Name
            };
            var view = BuildPlayerLiveView(subject.SubjectId, false, subject.SubjectType, subject.DisplayNameSnapshot);
            var health = view.Resources.FirstOrDefault(x => x.ResourceId == "health");
            var weapon = view.Weapons.FirstOrDefault(x => x.IsActive) ?? view.Weapons.FirstOrDefault();
            return (object)new Dictionary<string, object>
            {
                ["displayName"] = view.DisplayName,
                ["lifeState"] = view.LifeState,
                ["canAct"] = view.CanAct,
                ["canReact"] = view.CanReact,
                ["resources"] = view.Resources.Select(ResourcePayload).Cast<object>().ToArray(),
                ["resourceSummary"] = health == null ? string.Empty : $"Здоровье {health.Current:0.#}/{health.EffectiveMaximum:0.#}",
                ["weaponSummary"] = weapon == null ? string.Empty : $"{weapon.DisplayName}: {weapon.LoadedQuantity + weapon.ChamberedQuantity} заряжено"
            };
        }).ToArray();
        return Ok("Состояния спутников загружены.", new Dictionary<string, object> { ["companions"] = rows });
    }

    private PlayerLiveActorView BuildPlayerLiveView(string subjectId, bool includeGm = false, string subjectType = RuntimeSubjectTypes.Character, string displayName = "")
    {
        var subject = new RuntimeSubjectReference { SubjectType = subjectType, SubjectId = subjectId, DisplayNameSnapshot = displayName };
        var state = GetOrCreateRuntimeState(subject, "system");
        var reservations = _mongo.ResourceReservationStates.Find(x => x.SubjectId == subjectId && x.State == "active").ToList();
        var body = subjectType == RuntimeSubjectTypes.Character ? _mongo.CharacterBodyProfiles.Find(x => x.CharacterId == subjectId).FirstOrDefault()?.Profile : null;
        var effects = _mongo.RuntimeEffectInstances.Find(x => x.TargetSubject.SubjectId == subjectId && x.IsActive).ToList();
        var visibleEffects = includeGm ? effects : effects.Where(x => x.IsPlayerVisible).ToList();
        var view = new PlayerLiveActorView
        {
            SubjectType = subjectType, SubjectId = subjectId, DisplayName = FirstNonEmpty(displayName, state.DisplayNameSnapshot),
            LifeState = state.LifeState.StateCode, CanAct = state.LifeState.CanAct, CanReact = state.LifeState.CanReact,
            Revision = state.EntityRevision, Loadout = state.Loadout, Actions = state.ActionStates.Select(x => ActionProjection(state, x)).ToList(),
            Executions = _mongo.ActionExecutionStates.Find(x => x.ActorSubject.SubjectId == subjectId && x.State != "completed").ToList()
        };
        foreach (var resource in state.ResourceStates)
        {
            var capacity = ResolveBaseMaximum(subject, body, resource.ResourceDefinitionId, Math.Max(resource.CurrentValue, 0));
            var baseMax = capacity.Maximum;
            var effectMaximum = effects.Where(x => includeGm || x.IsPlayerVisible || x.IsModifierReasonPlayerVisible).Sum(x => x.ResourceMaximumModifiers.TryGetValue(resource.ResourceDefinitionId, out var modifier) ? modifier : 0);
            var effectiveMaximum = LiveActorRules.EffectiveMaximum(baseMax, resource.TemporaryMaximumModifier + effectMaximum);
            view.Resources.Add(new PlayerLiveResourceView { ResourceId = resource.ResourceDefinitionId, DisplayName = ReadableResource(resource.ResourceDefinitionId), Current = resource.CurrentValue, BaseMaximum = baseMax, EffectiveMaximum = effectiveMaximum, Reserved = reservations.Where(x => x.ResourceDefinitionId == resource.ResourceDefinitionId).Sum(x => x.ReservedAmount), Overcap = Math.Max(0, resource.CurrentValue - effectiveMaximum), CapacitySource = capacity.Source, CalculationVersion = "0.21.6A" });
            if (resource.CurrentValue > effectiveMaximum) view.ReconciliationWarnings.Add($"{ReadableResource(resource.ResourceDefinitionId)} превышает действующий максимум; требуется решение мастера.");
        }
        view.Effects = includeGm ? effects : effects.Where(x => x.IsPlayerVisible).Select(PlayerSafeEffect).ToList();
        if (subjectType == RuntimeSubjectTypes.Character) view.Capabilities = BuildCapabilities(subjectId, effects, includeGm);
        if (subjectType == RuntimeSubjectTypes.Character)
        {
            var inventory = _mongo.CharacterInventoryProfiles.Find(x => x.CharacterId == subjectId).FirstOrDefault()?.Profile;
            view.Weapons = state.ItemOperationalStates.Select(x => BuildWeaponView(x, inventory)).ToList();
            foreach (var weapon in state.ItemOperationalStates.Where(x => x.AmmunitionFeed != null && (x.AmmunitionFeed.LoadedQuantity > x.AmmunitionFeed.Capacity || x.AmmunitionFeed.ChamberedQuantity < 0)))
                view.ReconciliationWarnings.Add(includeGm ? $"Состояние оружия «{BuildWeaponView(weapon, inventory).DisplayName}» требует проверки мастером." : "Состояние активного оружия требует проверки мастером.");
            var combat = _mongo.CombatParticipants.Find(x => x.CharacterId == subjectId && x.IsActive && !x.IsDefeated).SortByDescending(x => x.UpdatedUtc).FirstOrDefault();
            if (combat != null)
            {
                view.Combat = new LiveCombatContextView { IsInCombat = true, ActionPoints = combat.ActionPoints, MinorActionPoints = combat.MinorActionPoints, ReactionCount = combat.ReactionCount, ReactionLimit = combat.ReactionLimit, HasActedThisRound = combat.HasActedThisRound };
                view.CanAct &= combat.ActionPoints > 0 || combat.MinorActionPoints > 0;
                view.CanReact &= combat.ReactionCount < combat.ReactionLimit;
            }
        }
        else view.Weapons = state.ItemOperationalStates.Select(x => BuildWeaponView(x, null)).ToList();
        var historyFilter = Builders<LiveStateEventRecord>.Filter.Eq(x => x.SubjectId, subjectId);
        if (!includeGm) historyFilter &= Builders<LiveStateEventRecord>.Filter.Eq(x => x.IsPlayerVisible, true);
        view.History = _mongo.LiveStateEvents.Find(historyFilter).SortByDescending(x => x.CreatedUtc).Limit(30).ToList();
        return view;
    }

    private List<LiveCapabilitySnapshot> BuildCapabilities(string characterId, List<RuntimeEffectInstance> effects, bool includeGm)
    {
        var result = new List<LiveCapabilitySnapshot>();
        var attributes = _mongo.CharacterAttributeProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()?.Profile;
        foreach (var value in attributes?.Values ?? new List<CharacterAttributeValue>())
        {
            var modifiers = effects.Where(x => (includeGm || x.IsPlayerVisible || x.IsModifierReasonPlayerVisible) && x.CapabilityModifiers.ContainsKey(value.AttributeId)).ToList();
            var temp = modifiers.Sum(x => x.CapabilityModifiers[value.AttributeId]);
            result.Add(new LiveCapabilitySnapshot { CapabilityType = "attribute", DefinitionId = value.AttributeId, DisplayName = ReadableCapability(value.AttributeId), BaseValue = value.BaseValue, PermanentModifier = value.ManualModifier, TemporaryModifier = temp, EffectiveValue = LiveActorRules.EffectiveCapability(value.BaseValue, value.ManualModifier, temp), PublicModifierReasons = modifiers.Where(x => x.IsPlayerVisible || x.IsModifierReasonPlayerVisible).Select(x => $"{x.PublicNameSnapshot}: {x.CapabilityModifiers[value.AttributeId]:+0.#;-0.#;0}").ToList(), GmModifierReasons = includeGm ? modifiers.Select(x => $"{FirstNonEmpty(x.GmNameSnapshot, x.PublicNameSnapshot)}: {x.CapabilityModifiers[value.AttributeId]:+0.#;-0.#;0}").ToList() : new List<string>(), CalculatedAtRevision = 0 });
        }
        var skills = _mongo.CharacterSkillProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault()?.Profile;
        foreach (var value in skills?.Skills.Where(x => includeGm || x.IsPlayerVisible) ?? Enumerable.Empty<CharacterSkillProfileValue>())
        {
            var modifiers = effects.Where(x => (includeGm || x.IsPlayerVisible || x.IsModifierReasonPlayerVisible) && x.CapabilityModifiers.ContainsKey(value.SkillId)).ToList();
            var temp = modifiers.Sum(x => x.CapabilityModifiers[value.SkillId]);
            result.Add(new LiveCapabilitySnapshot { CapabilityType = "skill", DefinitionId = value.SkillId, DisplayName = ReadableCapability(value.SkillId), BaseValue = value.Rank, PermanentModifier = value.ManualBonus, TemporaryModifier = temp, EffectiveValue = LiveActorRules.EffectiveCapability(value.Rank, value.ManualBonus, temp), PublicModifierReasons = modifiers.Where(x => x.IsPlayerVisible || x.IsModifierReasonPlayerVisible).Select(x => $"{x.PublicNameSnapshot}: {x.CapabilityModifiers[value.SkillId]:+0.#;-0.#;0}").ToList(), GmModifierReasons = includeGm ? modifiers.Select(x => $"{FirstNonEmpty(x.GmNameSnapshot, x.PublicNameSnapshot)}: {x.CapabilityModifiers[value.SkillId]:+0.#;-0.#;0}").ToList() : new List<string>() });
        }
        return result;
    }

    private ActorRuntimeStateDocument GetOrCreateRuntimeState(RuntimeSubjectReference subject, string actorUserId)
    {
        var state = _mongo.ActorRuntimeStates.Find(x => x.SubjectType == subject.SubjectType && x.SubjectId == subject.SubjectId).FirstOrDefault();
        if (state != null) return state;
        var ownership = subject.SubjectType == RuntimeSubjectTypes.Character ? _mongo.CharacterOwnerships.Find(x => x.CharacterId == subject.SubjectId).FirstOrDefault() : null;
        state = new ActorRuntimeStateDocument
        {
            SubjectType = subject.SubjectType, SubjectId = subject.SubjectId, CharacterId = subject.SubjectType == RuntimeSubjectTypes.Character ? subject.SubjectId : null,
            CampaignId = FirstNonEmpty(subject.CampaignId, ownership?.CampaignId ?? string.Empty), WorldId = subject.WorldId,
            DisplayNameSnapshot = FirstNonEmpty(subject.DisplayNameSnapshot, ownership?.CharacterDisplayName ?? "Участник"), UpdatedBy = actorUserId,
            Loadout = new ActiveLoadoutState { SubjectId = subject.SubjectId }, EntityRevision = 1
        };
        var body = subject.SubjectType == RuntimeSubjectTypes.Character ? _mongo.CharacterBodyProfiles.Find(x => x.CharacterId == subject.SubjectId).FirstOrDefault()?.Profile : null;
        foreach (var id in new[] { "health", "mana", "stamina", "shield" })
        {
            var maximum = BodyStat(body, id, 0);
            if (maximum > 0) state.ResourceStates.Add(new RuntimeResourceState { ResourceDefinitionId = id, CurrentValue = maximum, Revision = 1 });
        }
        _mongo.ActorRuntimeStates.InsertOne(state);
        return state;
    }

    private string ResolvePlayerLiveCharacterId(CommandContext context, UserAccount actor)
    {
        var requested = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "characterId"), _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, actor.Id)).FirstOrDefault()?.ActiveCharacterId ?? string.Empty);
        if (string.IsNullOrWhiteSpace(requested)) throw new KeyNotFoundException("Активный персонаж не выбран.");
        var ownership = _mongo.CharacterOwnerships.Find(x => x.CharacterId == requested && !x.IsArchived).FirstOrDefault();
        if (ownership == null || (!string.Equals(ownership.OwnerUserId, actor.Id, StringComparison.Ordinal) && !string.Equals(ownership.ControlledByUserId, actor.Id, StringComparison.Ordinal))) throw new UnauthorizedAccessException("Персонаж недоступен.");
        return requested;
    }

    private static RuntimeSubjectReference ResolveSubject(IDictionary<string, object> payload) => new()
    {
        SubjectType = FirstNonEmpty(PayloadReader.GetString(payload, "subjectType"), RuntimeSubjectTypes.Character),
        SubjectId = Required(PayloadReader.GetString(payload, "subjectId") ?? PayloadReader.GetString(payload, "characterId"), "Выберите участника."),
        CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
        WorldId = PayloadReader.GetString(payload, "worldId") ?? string.Empty
    };

    private void SaveRuntimeMutation(ActorRuntimeStateDocument state, string actorUserId, string operationId, string category, string display, string oldSummary, string newSummary, bool playerVisible, string targetKey = "", string compensationForEventId = "", string gmOnlyDetail = "")
    {
        state.EntityRevision++; state.UpdatedBy = actorUserId; state.UpdatedUtc = DateTime.UtcNow;
        _mongo.ActorRuntimeStates.ReplaceOne(x => x.Id == state.Id, state);
        _mongo.LiveStateEvents.InsertOne(new LiveStateEventRecord { SubjectId = state.SubjectId, Category = category, TargetKey = targetKey, DisplayText = display, GmOnlyDetail = gmOnlyDetail, OldSummary = oldSummary, NewSummary = newSummary, OperationId = operationId, ActorUserId = actorUserId, CompensationForEventId = compensationForEventId, IsPlayerVisible = playerVisible, Revision = state.EntityRevision });
        if (_featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseLiveActorSyncEvents)))
        {
            _syncEvents.PublishCharacter(state.SubjectId, $"actor.runtime.{category}.changed", "actor_runtime", state.SubjectId, "changed", actorUserId,
                new Dictionary<string, object> { ["subjectType"] = state.SubjectType, ["subjectId"] = state.SubjectId, ["revision"] = state.EntityRevision, ["category"] = category }, operationId);
            _syncEvents.PublishCharacter(state.SubjectId, "character.liveState.invalidated", "actor_runtime", state.SubjectId, "invalidate", actorUserId,
                new Dictionary<string, object> { ["subjectId"] = state.SubjectId, ["revision"] = state.EntityRevision }, operationId);
        }
        _logger.Admin($"live_actor.mutation category={category} subjectType={state.SubjectType} subjectId={state.SubjectId} revision={state.EntityRevision}");
    }

    private bool LiveActorBaseEnabled() => _featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseLiveActorStateV1));
    private bool LiveActorPlayerEnabled() => LiveActorBaseEnabled() && _featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseLiveActorPlayerView));
    private bool LiveActorAdminEnabled() => LiveActorBaseEnabled() && _featureFlags.IsEnabled(nameof(LiveActorFeatureFlags.UseLiveActorAdminView));
    private ResponseEnvelope LiveActorDisabled(CommandContext context) { _logger.Admin($"live_actor.disabled command={context.Request.Command}"); return Error("Текущее состояние персонажа выключено в настройках функций.", ResponseStatus.Forbidden, ErrorCode.Forbidden); }
    private static string Required(string? value, string message) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(message); return value.Trim(); }
    private static decimal BodyStat(BodyProfile? body, string id, decimal fallback)
    {
        if (body?.BodyStats == null) return fallback;
        foreach (var pair in body.BodyStats)
            if (string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        return fallback;
    }
    private (decimal Maximum, string Source) ResolveBaseMaximum(RuntimeSubjectReference subject, BodyProfile? body, string resourceId, decimal fallback)
    {
        if (subject.SubjectType == RuntimeSubjectTypes.Character && body?.BodyStats != null)
        {
            foreach (var pair in body.BodyStats)
                if (string.Equals(pair.Key, resourceId, StringComparison.OrdinalIgnoreCase)) return (pair.Value, "character_v2.body_profile");
        }
        if (subject.SubjectType == RuntimeSubjectTypes.Companion)
        {
            var companion = _mongo.CharacterCompanionProfiles.Find(FilterDefinition<CharacterCompanionProfileDocument>.Empty).ToList()
                .SelectMany(x => x.Profile?.Companions ?? new List<CharacterCompanionProfileValue>())
                .FirstOrDefault(x => string.Equals(x.CompanionId, subject.SubjectId, StringComparison.OrdinalIgnoreCase));
            if (companion?.ResourceMaximums != null)
                foreach (var pair in companion.ResourceMaximums)
                    if (string.Equals(pair.Key, resourceId, StringComparison.OrdinalIgnoreCase)) return (pair.Value, "character_v2.companion_profile");
        }
        var actorProfile = _mongo.RuntimeSubjectCapacityProfiles.Find(x => x.SubjectType == subject.SubjectType && x.SubjectId == subject.SubjectId).FirstOrDefault();
        if (actorProfile?.ResourceMaximums != null)
            foreach (var pair in actorProfile.ResourceMaximums)
                if (string.Equals(pair.Key, resourceId, StringComparison.OrdinalIgnoreCase)) return (pair.Value, "runtime_subject.profile");
        if (string.Equals(resourceId, "health", StringComparison.OrdinalIgnoreCase))
        {
            var combat = _mongo.CombatParticipants.Find(x => x.CharacterId == subject.SubjectId && x.IsActive).SortByDescending(x => x.UpdatedUtc).FirstOrDefault();
            if (combat?.MaxHealth > 0) return (combat.MaxHealth, "combat_derived_profile");
        }
        return (Math.Max(0, fallback), "runtime_initialization_fallback");
    }
    private static string RuntimeSubjectTypeFromGroupMember(string entityType) => entityType.ToLowerInvariant() switch
    {
        CharacterGroupEntityTypeIds.PlayerCharacter => RuntimeSubjectTypes.Character,
        CharacterGroupEntityTypeIds.Companion => RuntimeSubjectTypes.Companion,
        CharacterGroupEntityTypeIds.Npc or CharacterGroupEntityTypeIds.Enemy or CharacterGroupEntityTypeIds.Neutral => RuntimeSubjectTypes.Npc,
        CharacterGroupEntityTypeIds.TemporaryAlly => RuntimeSubjectTypes.Companion,
        _ => RuntimeSubjectTypes.Custom
    };
    private List<string> ActionUnavailableReasons(ActorRuntimeStateDocument state, ActionRuntimeState action)
    {
        var reasons = new List<string>();
        if (!state.LifeState.CanAct) reasons.Add("Текущее состояние не позволяет действовать");
        if (!action.IsEnabled) reasons.Add("Действие отключено");
        if (action.RemainingRounds > 0) reasons.Add($"Перезарядка: {action.RemainingRounds} раунд(а)");
        if (action.RemainingTurns > 0) reasons.Add($"Перезарядка: {action.RemainingTurns} ход(а)");
        if (action.MaximumCharges > 0 && action.CurrentCharges <= 0) reasons.Add("Нет доступных зарядов");
        foreach (var cost in action.ResourceCosts.Where(x => x.Value > 0))
        {
            var current = state.ResourceStates.FirstOrDefault(x => string.Equals(x.ResourceDefinitionId, cost.Key, StringComparison.OrdinalIgnoreCase))?.CurrentValue ?? 0;
            var reserved = _mongo.ResourceReservationStates.Find(x => x.SubjectId == state.SubjectId && x.ResourceDefinitionId == cost.Key && x.State == "active").ToList().Sum(x => x.ReservedAmount);
            if (current - reserved < cost.Value) reasons.Add($"Недостаточно ресурса «{ReadableResource(cost.Key)}»: требуется {cost.Value:0.#}, доступно {Math.Max(0, current - reserved):0.#}");
        }
        if (action.AmmunitionUnitsOnUse > 0)
        {
            var weaponId = FirstNonEmpty(action.RequiredWeaponItemInstanceId, state.Loadout.ActiveWeaponItemInstanceId);
            var weapon = state.ItemOperationalStates.FirstOrDefault(x => x.ItemInstanceId == weaponId);
            var loaded = (weapon?.AmmunitionFeed?.LoadedQuantity ?? 0) + (weapon?.AmmunitionFeed?.ChamberedQuantity ?? 0);
            if (loaded < action.AmmunitionUnitsOnUse) reasons.Add("В активном оружии недостаточно боеприпасов");
            if (weapon?.IsJammed == true) reasons.Add("Оружие заклинило");
            if (weapon?.IsBroken == true) reasons.Add("Оружие неисправно");
        }
        return reasons;
    }
    private ActionRuntimeState ActionProjection(ActorRuntimeStateDocument state, ActionRuntimeState source) => new()
    {
        ActionDefinitionId = source.ActionDefinitionId, SourceType = source.SourceType, SourceDefinitionId = source.SourceDefinitionId,
        CooldownMode = source.CooldownMode, RemainingTurns = source.RemainingTurns, RemainingRounds = source.RemainingRounds,
        ReadyAtWorldTime = source.ReadyAtWorldTime, ReadyAtSceneTime = source.ReadyAtSceneTime, ReadyAtUtc = source.ReadyAtUtc,
        CurrentCharges = source.CurrentCharges, MaximumCharges = source.MaximumCharges, ResourceCosts = new Dictionary<string, decimal>(source.ResourceCosts),
        CooldownRoundsOnUse = source.CooldownRoundsOnUse, CooldownTurnsOnUse = source.CooldownTurnsOnUse, RestResetPolicy = source.RestResetPolicy,
        AmmunitionUnitsOnUse = source.AmmunitionUnitsOnUse, RequiredWeaponItemInstanceId = source.RequiredWeaponItemInstanceId,
        UsesSinceShortRest = source.UsesSinceShortRest, UsesSinceLongRest = source.UsesSinceLongRest, IsPrepared = source.IsPrepared,
        IsEnabled = source.IsEnabled, UnavailableReasonCodes = ActionUnavailableReasons(state, source), LastUsedAtUtc = source.LastUsedAtUtc, Revision = source.Revision
    };
    private static string ReadableResource(string id) => id.ToLowerInvariant() switch { "health" or "hp" => "Здоровье", "mana" => "Мана", "stamina" => "Выносливость", "shield" => "Щит", _ => ReadableCapability(id) };
    private static string ReadableAction(string id) => id.ToLowerInvariant() switch { "combat_dash" or "combat_rush" or "combat_rush_0216" => "Боевой рывок", "field_aid" => "Полевая помощь", "magic_burst" or "magic_shield" or "arcane_burst_0216" => "Магический импульс", "banishment_ritual" => "Ритуал изгнания", _ => "Неизвестное действие" };
    private static string ReadableCapability(string id) => id.ToLowerInvariant() switch { "strength" => "Сила", "dexterity" => "Ловкость", "constitution" or "endurance" => "Выносливость", "intelligence" => "Интеллект", "wisdom" => "Мудрость", "charisma" => "Харизма", "medicine" => "Медицина", _ => id.Replace('_', ' ') };
    private static string ReadableLifeState(string id) => id.ToLowerInvariant() switch { "healthy" => "В норме", "impaired" => "Ослаблен", "incapacitated" => "Недееспособен", "unconscious" => "Без сознания", "dying" => "При смерти", "stable" => "Стабилен", "dead" => "Мёртв", "destroyed" => "Уничтожен", _ => id.Replace('_', ' ') };

    private static RuntimeEffectInstance PlayerSafeEffect(RuntimeEffectInstance source) => new() { EffectInstanceId = source.EffectInstanceId, ConditionDefinitionId = source.ConditionDefinitionId, PublicNameSnapshot = source.PublicNameSnapshot, PublicDescriptionSnapshot = source.PublicDescriptionSnapshot, TargetSubject = new RuntimeSubjectReference { SubjectType = source.TargetSubject.SubjectType, SubjectId = source.TargetSubject.SubjectId, DisplayNameSnapshot = source.TargetSubject.DisplayNameSnapshot }, StackCount = source.StackCount, DurationMode = source.DurationMode, RemainingTurns = source.RemainingTurns, RemainingRounds = source.RemainingRounds, ExpiresAtUtc = source.ExpiresAtUtc, StackingPolicySnapshot = source.StackingPolicySnapshot, ConcentrationExecutionId = source.ConcentrationExecutionId, IsPlayerVisible = true, IsModifierReasonPlayerVisible = source.IsModifierReasonPlayerVisible, IsActive = source.IsActive, IsExpired = source.IsExpired, CapabilityModifiers = new Dictionary<string, decimal>(source.CapabilityModifiers), ResourceMaximumModifiers = new Dictionary<string, decimal>(source.ResourceMaximumModifiers), Revision = source.Revision };

    private static PlayerLiveWeaponView BuildWeaponView(ItemOperationalState state, InventoryProfile? inventory)
    {
        var item = inventory?.Items.FirstOrDefault(x => string.Equals(x.ItemId, state.ItemInstanceId, StringComparison.OrdinalIgnoreCase));
        var ammoId = state.AmmunitionFeed?.LoadedAmmunitionDefinitionId ?? string.Empty;
        var reserve = inventory?.Items.Where(x => string.Equals(FirstNonEmpty(x.DefinitionId, x.ItemDefinitionId), ammoId, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity) ?? 0;
        return new PlayerLiveWeaponView
        {
            ItemInstanceId = state.ItemInstanceId,
            DisplayName = FirstNonEmpty(item?.SnapshotDisplayName ?? string.Empty, item?.DisplayName ?? string.Empty, item?.Name ?? string.Empty, "Оружие"),
            OperationalMode = state.OperationalMode,
            IsEquipped = state.IsEquipped,
            IsActive = state.IsActive,
            IsJammed = state.IsJammed,
            IsBroken = state.IsBroken,
            DurabilityCurrent = state.DurabilityCurrent,
            DurabilityMaximum = state.DurabilityMaximum,
            LoadedQuantity = state.AmmunitionFeed?.LoadedQuantity ?? 0,
            ReserveQuantity = reserve,
            Capacity = state.AmmunitionFeed?.Capacity ?? 0,
            ChamberedQuantity = state.AmmunitionFeed?.ChamberedQuantity ?? 0,
            FireMode = state.AmmunitionFeed?.FireMode ?? string.Empty,
            Revision = state.Revision
        };
    }

    private static Dictionary<string, object> LiveViewPayload(PlayerLiveActorView view, bool admin = false) => new()
    {
        ["subjectType"] = view.SubjectType, ["subjectId"] = view.SubjectId, ["displayName"] = view.DisplayName, ["lifeState"] = view.LifeState,
        ["canAct"] = view.CanAct, ["canReact"] = view.CanReact, ["revision"] = view.Revision, ["builtAtUtc"] = view.BuiltAtUtc,
        ["resources"] = view.Resources.Select(ResourcePayload).Cast<object>().ToArray(),
        ["capabilities"] = view.Capabilities.Select(x => new Dictionary<string, object> { ["capabilityType"] = x.CapabilityType, ["definitionId"] = x.DefinitionId, ["displayName"] = x.DisplayName, ["baseValue"] = x.BaseValue, ["permanentModifier"] = x.PermanentModifier, ["temporaryModifier"] = x.TemporaryModifier, ["effectiveValue"] = x.EffectiveValue, ["modifierReasons"] = (admin ? x.GmModifierReasons : x.PublicModifierReasons).Cast<object>().ToArray() }).Cast<object>().ToArray(),
        ["effects"] = view.Effects.Select(x => EffectPayload(x, admin)).Cast<object>().ToArray(),
        ["actions"] = view.Actions.Select(ActionPayload).Cast<object>().ToArray(), ["weapons"] = view.Weapons.Select(WeaponPayload).Cast<object>().ToArray(),
        ["loadout"] = new Dictionary<string, object> { ["activeWeaponItemInstanceId"] = view.Loadout.ActiveWeaponItemInstanceId, ["activeAttackProfileId"] = view.Loadout.ActiveAttackProfileId, ["gripMode"] = view.Loadout.GripMode, ["selectedFireMode"] = view.Loadout.SelectedFireMode, ["safetyState"] = view.Loadout.SafetyState, ["isReadied"] = view.Loadout.IsReadied, ["attunedCount"] = view.Loadout.AttunedItemInstanceIds.Count, ["attunementLimit"] = view.Loadout.AttunementLimit },
        ["executions"] = view.Executions.Select(x => new Dictionary<string, object> { ["executionId"] = x.ExecutionId, ["actionDefinitionId"] = x.ActionDefinitionId, ["displayName"] = ReadableAction(x.ActionDefinitionId), ["state"] = x.State, ["currentStage"] = x.CurrentStage, ["totalStages"] = x.TotalStages, ["remainingRounds"] = x.RemainingRounds }).Cast<object>().ToArray(),
        ["combat"] = new Dictionary<string, object> { ["isInCombat"] = view.Combat.IsInCombat, ["actionPoints"] = view.Combat.ActionPoints, ["minorActionPoints"] = view.Combat.MinorActionPoints, ["reactionCount"] = view.Combat.ReactionCount, ["reactionLimit"] = view.Combat.ReactionLimit, ["hasActedThisRound"] = view.Combat.HasActedThisRound },
        ["history"] = view.History.Select(x => EventPayload(x, admin)).Cast<object>().ToArray(),
        ["warnings"] = view.ReconciliationWarnings.Cast<object>().ToArray(),
        ["reconciliationWarnings"] = view.ReconciliationWarnings.Cast<object>().ToArray()
    };
    private static Dictionary<string, object> ResourcePayload(PlayerLiveResourceView x) => new() { ["resourceId"] = x.ResourceId, ["displayName"] = x.DisplayName, ["current"] = x.Current, ["baseMaximum"] = x.BaseMaximum, ["effectiveMaximum"] = x.EffectiveMaximum, ["reserved"] = x.Reserved, ["overcap"] = x.Overcap, ["capacitySource"] = x.CapacitySource, ["calculationVersion"] = x.CalculationVersion };
    private static Dictionary<string, object> EffectPayload(RuntimeEffectInstance x, bool admin)
    {
        var payload = new Dictionary<string, object> { ["effectInstanceId"] = x.EffectInstanceId, ["displayName"] = x.PublicNameSnapshot, ["description"] = x.PublicDescriptionSnapshot, ["stackCount"] = x.StackCount, ["durationMode"] = x.DurationMode, ["remainingTurns"] = x.RemainingTurns ?? 0, ["remainingRounds"] = x.RemainingRounds ?? 0, ["expiresAtUtc"] = x.ExpiresAtUtc.HasValue ? (object)x.ExpiresAtUtc.Value : string.Empty };
        if (admin) payload["gmDetail"] = x.GmNameSnapshot;
        return payload;
    }
    private static Dictionary<string, object> ActionPayload(ActionRuntimeState x) => new() { ["actionDefinitionId"] = x.ActionDefinitionId, ["displayName"] = ReadableAction(x.ActionDefinitionId), ["sourceType"] = x.SourceType, ["cooldownMode"] = x.CooldownMode, ["remainingTurns"] = x.RemainingTurns, ["remainingRounds"] = x.RemainingRounds, ["currentCharges"] = x.CurrentCharges, ["maximumCharges"] = x.MaximumCharges, ["isPrepared"] = x.IsPrepared, ["isEnabled"] = x.IsEnabled, ["resourceCosts"] = x.ResourceCosts.Select(c => (object)new Dictionary<string, object> { ["displayName"] = ReadableResource(c.Key), ["amount"] = c.Value }).ToArray(), ["unavailableReasons"] = x.UnavailableReasonCodes.Cast<object>().ToArray(), ["revision"] = x.Revision };
    private static Dictionary<string, object> WeaponPayload(PlayerLiveWeaponView x) => new() { ["itemInstanceId"] = x.ItemInstanceId, ["displayName"] = x.DisplayName, ["operationalMode"] = x.OperationalMode, ["isEquipped"] = x.IsEquipped, ["isActive"] = x.IsActive, ["isJammed"] = x.IsJammed, ["isBroken"] = x.IsBroken, ["durabilityCurrent"] = x.DurabilityCurrent, ["durabilityMaximum"] = x.DurabilityMaximum, ["loadedQuantity"] = x.LoadedQuantity, ["reserveQuantity"] = x.ReserveQuantity, ["capacity"] = x.Capacity, ["chamberedQuantity"] = x.ChamberedQuantity, ["fireMode"] = x.FireMode, ["revision"] = x.Revision };
    private static Dictionary<string, object> EventPayload(LiveStateEventRecord x, bool admin)
    {
        var payload = new Dictionary<string, object> { ["eventId"] = x.Id, ["category"] = x.Category, ["displayText"] = x.DisplayText, ["oldSummary"] = x.OldSummary, ["newSummary"] = x.NewSummary, ["createdAtUtc"] = x.CreatedUtc, ["revision"] = x.Revision };
        if (admin) { payload["gmDetail"] = x.GmOnlyDetail; payload["operationId"] = x.OperationId; payload["compensationForEventId"] = x.CompensationForEventId; }
        return payload;
    }
}
