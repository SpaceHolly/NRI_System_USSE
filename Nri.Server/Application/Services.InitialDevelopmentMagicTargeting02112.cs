using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope InitialDevelopmentAdminPolicyGet02112(CommandContext context)
    {
        RequireAdmin(context);
        var campaignId = RequireLength(PayloadReader.GetString(context.Request.Payload, "campaignId"), 1, 128, "campaignId");
        var creationPolicy = _mongo.CharacterCreationPolicies.Find(x => x.CampaignId == campaignId).FirstOrDefault();
        var ruleSetId = FirstNonEmpty(creationPolicy?.RuleSetId, RuleSetIds.FantasyNriDefault);
        var policy = LoadInitialDevelopmentPolicy02112(ruleSetId);
        if (policy == null) return Error("Правила начального развития не настроены.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Правила начального развития загружены.", new Dictionary<string, object>
        {
            ["policyId"] = policy.PolicyId,
            ["ruleSetId"] = policy.RuleSetId,
            ["classRule"] = "Один базовый класс до 2 ранга или два разных базовых класса по 1 рангу",
            ["magicRule"] = "Один первичный метод магии и одна базовая стихия",
            ["mustCompleteBeforeActiveSession"] = policy.MustCompleteBeforeActiveSession,
            ["baseClassCount"] = policy.AllowedBaseClassNodeIds.Count,
            ["magicMethodCount"] = policy.AllowedPrimaryMagicMethodNodeIds.Count,
            ["basicElementCount"] = policy.AllowedBasicMagicDirectionNodeIds.Count,
            ["entityRevision"] = policy.EntityRevision
        });
    }

    public ResponseEnvelope InitialDevelopmentPlayerGet02112(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var ownership = RequireInitialDevelopmentOwnership02112(context, actor);
        var document = LoadDevelopmentProfileDocument02112(ownership.CharacterId);
        var ruleSetId = FirstNonEmpty(document?.Profile?.RuleSetId, RuleSetIds.FantasyNriDefault);
        var policy = LoadInitialDevelopmentPolicy02112(ruleSetId);
        if (policy == null)
            return Error("Правила начального развития для этого набора правил не настроены.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Начальное развитие загружено.", BuildInitialDevelopmentPayload02112(ownership.CharacterId, document?.Profile, policy));
    }

    public ResponseEnvelope InitialDevelopmentPlayerComplete02112(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var ownership = RequireInitialDevelopmentOwnership02112(context, actor);
        var characterId = ownership.CharacterId;
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var operationId = RequireLength(PayloadReader.GetString(payload, "operationId"), 8, 160, "operationId");
        var expectedRevision = PayloadReader.GetInt(payload, "expectedRevision")
            ?? throw new ArgumentException("expectedRevision is required.");

        lock (_developmentProductMutationSync0215)
        {
            EnsureDefinitionsLoaded(false);
            var filter = Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, characterId);
            var document = _mongo.CharacterDevelopmentProfiles.Find(filter).FirstOrDefault()
                ?? new CharacterDevelopmentProfileDocument
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CharacterId = characterId,
                    Profile = new DevelopmentProfile { CharacterId = characterId, RuleSetId = RuleSetIds.FantasyNriDefault }
                };
            document.Profile ??= new DevelopmentProfile { CharacterId = characterId, RuleSetId = RuleSetIds.FantasyNriDefault };
            var profile = document.Profile;
            var policy = LoadInitialDevelopmentPolicy02112(FirstNonEmpty(profile.RuleSetId, RuleSetIds.FantasyNriDefault))
                ?? throw new InvalidOperationException("Правила начального развития для этого набора правил не настроены.");
            profile.InitialDevelopment ??= PendingInitialDevelopmentState02112(policy);

            if (string.Equals(profile.InitialDevelopment.CompletionOperationId, operationId, StringComparison.Ordinal)
                && string.Equals(profile.InitialDevelopment.Status, InitialDevelopmentStatusIds.Completed, StringComparison.Ordinal))
            {
                var replay = BuildInitialDevelopmentPayload02112(characterId, profile, policy);
                replay["alreadyApplied"] = true;
                return Ok("Начальное развитие уже было завершено.", replay);
            }
            if (profile.Revision != expectedRevision)
                return Error($"Развитие персонажа изменилось. Текущая редакция: {profile.Revision}.", ResponseStatus.Conflict, ErrorCode.Conflict);
            if (string.Equals(profile.InitialDevelopment.Status, InitialDevelopmentStatusIds.Completed, StringComparison.Ordinal))
                return Error("Начальное развитие уже завершено. Для изменения обратитесь к GM.", ResponseStatus.Conflict, ErrorCode.Conflict);

            var classGrants = ReadInitialClassGrants02112(payload);
            var methodNodeIds = Equipment0183ExtraList(payload, "magicMethodNodeIds");
            var scalarMethodNodeId = PayloadReader.GetString(payload, "magicMethodNodeId") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(scalarMethodNodeId)) methodNodeIds.Add(scalarMethodNodeId);
            methodNodeIds = methodNodeIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (methodNodeIds.Count != 1) throw new ArgumentException("Выберите ровно один первичный метод магии.");
            var methodNodeId = (methodNodeIds[0] ?? string.Empty).Trim();
            var elementNodeId = (PayloadReader.GetString(payload, "basicMagicDirectionNodeId") ?? string.Empty).Trim();
            ValidateInitialSelection02112(policy, classGrants, methodNodeId, elementNodeId);
            if (methodNodeId.Length > 160) throw new ArgumentException("Выбранный первичный метод магии указан неверно.");
            if (elementNodeId.Length > 160) throw new ArgumentException("Выбранная базовая стихия указана неверно.");

            var selectedNodes = classGrants.Select(x => x.DevelopmentNodeId)
                .Concat(new[] { methodNodeId, elementNodeId })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => _nodesById.TryGetValue(id, out var node) ? node : null)
                .ToList();
            if (selectedNodes.Any(x => x == null)) throw new KeyNotFoundException("Один из выбранных путей развития не найден.");

            var now = DateTime.UtcNow;
            foreach (var grant in classGrants)
                ApplyInitialDevelopmentGrant02112(profile, characterId, _nodesById[grant.DevelopmentNodeId], grant.Rank, actor.Id, now);
            ApplyInitialDevelopmentGrant02112(profile, characterId, _nodesById[methodNodeId], policy.MagicMethodGrantRank, actor.Id, now);
            ApplyInitialDevelopmentGrant02112(profile, characterId, _nodesById[elementNodeId], policy.BasicMagicDirectionGrantRank, actor.Id, now);

            profile.InitialDevelopment = new InitialDevelopmentState
            {
                Status = InitialDevelopmentStatusIds.Completed,
                PolicyId = policy.PolicyId,
                PolicyRevision = policy.EntityRevision,
                SelectedClassGrants = classGrants,
                SelectedMagicMethodNodeId = methodNodeId,
                SelectedBasicMagicDirectionNodeId = elementNodeId,
                CompletionOperationId = operationId,
                CompletedAtUtc = now,
                CompletedByUserId = actor.Id,
                EntityRevision = Math.Max(1, profile.InitialDevelopment.EntityRevision + 1)
            };
            profile.CharacterId = characterId;
            profile.RuleSetId = policy.RuleSetId;
            profile.Revision = Math.Max(0, profile.Revision) + 1;
            profile.SchemaVersion = Math.Max(2, profile.SchemaVersion);
            profile.UpdatedAtUtc = now;
            profile.RecentOperationIds ??= new List<string>();
            if (!profile.RecentOperationIds.Contains(operationId, StringComparer.Ordinal)) profile.RecentOperationIds.Add(operationId);
            if (profile.RecentOperationIds.Count > 64) profile.RecentOperationIds.RemoveRange(0, profile.RecentOperationIds.Count - 64);
            SyncDevelopmentProfileHexagons(profile);

            _mongo.CharacterDevelopmentProfiles.ReplaceOne(filter, document, new ReplaceOptions { IsUpsert = true });
            WriteAudit("initial_development", actor.Id, "complete", characterId);
            TryPublishSyncEvent("initial_development.completed", ownership.CampaignId, "development", characterId, "update", actor.Id,
                new Dictionary<string, object> { ["characterId"] = characterId, ["status"] = InitialDevelopmentStatusIds.Completed }, operationId);
            var result = BuildInitialDevelopmentPayload02112(characterId, profile, policy);
            result["alreadyApplied"] = false;
            result["moBalanceDelta"] = 0;
            return Ok("Начальное развитие завершено.", result);
        }
    }

    public ResponseEnvelope InitialDevelopmentAdminReset02112(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var characterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 1, 128, "characterId");
        var reason = RequireLength(PayloadReader.GetString(payload, "reason"), 5, 500, "reason");
        lock (_developmentProductMutationSync0215)
        {
            var filter = Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, characterId);
            var document = _mongo.CharacterDevelopmentProfiles.Find(filter).FirstOrDefault();
            if (document?.Profile?.InitialDevelopment == null) throw new KeyNotFoundException("Состояние начального развития не найдено.");
            var profile = document.Profile;
            var laterProgress = profile.Nodes.Any(x => x.IsPurchased
                && !string.Equals(x.Source, InitialDevelopmentGrantSources.InitialDevelopment, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.Source, "development_profile_initialize", StringComparison.OrdinalIgnoreCase));
            if (laterProgress)
                return Error("Сброс недоступен: после начального развития уже приобретены другие узлы.", ResponseStatus.Conflict, ErrorCode.Conflict);

            profile.Nodes.RemoveAll(x => string.Equals(x.Source, InitialDevelopmentGrantSources.InitialDevelopment, StringComparison.OrdinalIgnoreCase));
            profile.InitialDevelopment = new InitialDevelopmentState
            {
                Status = InitialDevelopmentStatusIds.ResetByGm,
                PolicyId = profile.InitialDevelopment.PolicyId,
                PolicyRevision = profile.InitialDevelopment.PolicyRevision,
                ResetAtUtc = DateTime.UtcNow,
                ResetByUserId = actor.Id,
                ResetReason = reason,
                EntityRevision = profile.InitialDevelopment.EntityRevision + 1
            };
            profile.Revision++;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            SyncDevelopmentProfileHexagons(profile);
            _mongo.CharacterDevelopmentProfiles.ReplaceOne(filter, document);
            WriteAudit("initial_development", actor.Id, "reset", characterId + ":" + reason);
            return Ok("Начальное развитие сброшено.", new Dictionary<string, object>
            {
                ["characterId"] = characterId,
                ["status"] = profile.InitialDevelopment.Status,
                ["profileRevision"] = profile.Revision
            });
        }
    }

    public ResponseEnvelope MagicTargetScopeEvaluate02112(CommandContext context)
    {
        GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var methodId = RequireLength(PayloadReader.GetString(payload, "magicMethodId"), 1, 160, "magicMethodId");
        var requestedScope = RequireLength(PayloadReader.GetString(payload, "requestedScope"), 1, 80, "requestedScope");
        if (!MagicTargetScopeIds.IsSupported(requestedScope))
            return Error("Выберите поддерживаемый тип цели.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var method = _mongo.UnifiedDefinitions.Find(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, DefinitionCategoryIds.MagicMethod)
            & Builders<UnifiedDefinitionDocument>.Filter.Or(
                Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, methodId),
                Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.StableKey, methodId))
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();
        if (method == null) throw new KeyNotFoundException("Магический метод не найден.");

        var techniqueScopes = Array.Empty<string>();
        var techniqueId = PayloadReader.GetString(payload, "techniqueId") ?? string.Empty;
        var techniqueFamily = PayloadReader.GetString(payload, "techniqueFamily") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(techniqueId))
        {
            if (!MagicEffectConditionDefinitionFamilies.IsSupported(techniqueFamily))
                return Error("Выберите поддерживаемый вид магической техники.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
            var technique = _mongo.UnifiedDefinitions.Find(
                Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, techniqueFamily)
                & Builders<UnifiedDefinitionDocument>.Filter.Or(
                    Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, techniqueId),
                    Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.StableKey, techniqueId))
                & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();
            if (technique == null) throw new KeyNotFoundException("Магическая техника не найдена.");
            techniqueScopes = Equipment0183ExtraList(technique.ExtraData, "allowedTargetScopes").ToArray();
        }

        var evaluation = MagicTargetScopeEvaluator.Evaluate(
            Equipment0183ExtraList(method.ExtraData, "allowedTargetScopes"),
            techniqueScopes,
            requestedScope,
            FirstNonEmpty(method.Name, "Этот магический метод"));
        var response = new Dictionary<string, object>
        {
            ["allowed"] = evaluation.IsAllowed,
            ["requestedScope"] = evaluation.RequestedScope,
            ["effectiveAllowedScopes"] = evaluation.EffectiveAllowedScopes.Cast<object>().ToArray(),
            ["effectiveAllowedScopeLabels"] = evaluation.EffectiveAllowedScopes.Select(MagicTargetScopeDisplay02112).Cast<object>().ToArray(),
            ["reason"] = evaluation.PublicReason,
            ["wouldCommit"] = evaluation.IsAllowed,
            ["externalEffectCommitted"] = false
        };
        return evaluation.IsAllowed
            ? Ok(evaluation.PublicReason, response)
            : new ResponseEnvelope
            {
                Status = ResponseStatus.ValidationFailed,
                ErrorCode = ErrorCode.ValidationFailed,
                Message = evaluation.PublicReason,
                Payload = response
            };
    }

    private CharacterDevelopmentProfileDocument? LoadDevelopmentProfileDocument02112(string characterId) =>
        _mongo.CharacterDevelopmentProfiles.Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();

    private CharacterOwnershipState RequireInitialDevelopmentOwnership02112(CommandContext context, UserAccount actor)
    {
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var ownership = _mongo.CharacterOwnerships.Find(
            Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)
            & Builders<CharacterOwnershipState>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Персонаж Character v2 не найден.");
        if (!IsAdminActor(actor)
            && !string.Equals(ownership.OwnerUserId, actor.Id, StringComparison.Ordinal)
            && !string.Equals(ownership.ControlledByUserId, actor.Id, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Персонаж недоступен.");
        return ownership;
    }

    private InitialDevelopmentPolicy? LoadInitialDevelopmentPolicy02112(string ruleSetId)
    {
        var record = _mongo.ContentDefinitionRecords.Find(
            Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, DefinitionCategoryIds.InitialDevelopmentPolicy)
            & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.RuleSetId, ruleSetId)
            & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();
        if (record == null) return null;
        var fields = record.CustomFields ?? new Dictionary<string, object>();
        var options = InitialDevelopmentMapList02112(fields.TryGetValue("classSelectionOptions", out var rawOptions) ? rawOptions : null)
            .Select(x => new InitialDevelopmentClassSelectionOption
            {
                ClassCount = Equipment0183MapInt(x, "classCount"),
                RankPerClass = Equipment0183MapInt(x, "rankPerClass"),
                RequireDistinctClasses = !x.ContainsKey("requireDistinctClasses") || Equipment0183MapBool(x, "requireDistinctClasses")
            }).Where(x => x.ClassCount > 0 && x.RankPerClass > 0).ToList();
        if (options.Count == 0)
        {
            options = Equipment0183ExtraList(fields, "classSelectionOptionCodes")
                .Select(InitialDevelopmentOptionCode02112)
                .Where(x => x != null)
                .Cast<InitialDevelopmentClassSelectionOption>()
                .ToList();
        }
        return new InitialDevelopmentPolicy
        {
            PolicyId = record.Id,
            RuleSetId = record.RuleSetId,
            ClassSelectionOptions = options,
            AllowedBaseClassNodeIds = Equipment0183ExtraList(fields, "allowedBaseClassNodeIds"),
            MagicMethodGrantRank = Math.Max(1, Equipment0183ExtraInt(fields, "magicMethodGrantRank")),
            AllowedPrimaryMagicMethodNodeIds = Equipment0183ExtraList(fields, "allowedPrimaryMagicMethodNodeIds"),
            BasicMagicDirectionGrantRank = Math.Max(1, Equipment0183ExtraInt(fields, "basicMagicDirectionGrantRank")),
            AllowedBasicMagicDirectionNodeIds = Equipment0183ExtraList(fields, "allowedBasicMagicDirectionNodeIds"),
            MustCompleteBeforeActiveSession = !fields.ContainsKey("mustCompleteBeforeActiveSession") || Equipment0183MapBool(fields, "mustCompleteBeforeActiveSession"),
            SchemaVersion = Math.Max(1, record.SchemaVersion),
            EntityRevision = Math.Max(1, record.Revision)
        };
    }

    private static List<Dictionary<string, object>> InitialDevelopmentMapList02112(object? raw)
    {
        var result = new List<Dictionary<string, object>>();
        if (raw is not IEnumerable values || raw is string) return result;
        foreach (var value in values)
        {
            if (value is BsonDocument bson)
            {
                result.Add(bson.Elements.ToDictionary(
                    x => x.Name,
                    x => BsonTypeMapper.MapToDotNetValue(x.Value) ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase));
                continue;
            }
            var mapped = Equipment0183Map(value);
            if (mapped.Count > 0) result.Add(mapped);
        }
        return result;
    }

    private static InitialDevelopmentClassSelectionOption? InitialDevelopmentOptionCode02112(string code)
    {
        var parts = (code ?? string.Empty).Split(':');
        var values = parts[0].Split('x');
        if (values.Length != 2 || !int.TryParse(values[0], out var classCount) || !int.TryParse(values[1], out var rankPerClass))
            return null;
        return new InitialDevelopmentClassSelectionOption
        {
            ClassCount = classCount,
            RankPerClass = rankPerClass,
            RequireDistinctClasses = parts.Skip(1).Any(x => string.Equals(x, "distinct", StringComparison.OrdinalIgnoreCase))
        };
    }

    private static InitialDevelopmentState PendingInitialDevelopmentState02112(InitialDevelopmentPolicy policy) => new()
    {
        Status = InitialDevelopmentStatusIds.Pending,
        PolicyId = policy.PolicyId,
        PolicyRevision = policy.EntityRevision,
        EntityRevision = 1
    };

    private static List<InitialDevelopmentClassGrant> ReadInitialClassGrants02112(IDictionary<string, object> payload) =>
        Equipment0183MapList(payload.TryGetValue("classGrants", out var raw) ? raw : null)
            .Select(x => new InitialDevelopmentClassGrant
            {
                DevelopmentNodeId = Equipment0183MapString(x, "developmentNodeId"),
                Rank = Equipment0183MapInt(x, "rank")
            }).ToList();

    private static void ValidateInitialSelection02112(
        InitialDevelopmentPolicy policy,
        List<InitialDevelopmentClassGrant> classGrants,
        string methodNodeId,
        string elementNodeId)
    {
        if (classGrants.Count == 0) throw new ArgumentException("Выберите стартовый класс.");
        if (classGrants.Any(x => string.IsNullOrWhiteSpace(x.DevelopmentNodeId) || x.Rank < 1))
            throw new ArgumentException("Стартовый класс или его ранг указаны неверно.");
        if (classGrants.Select(x => x.DevelopmentNodeId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != classGrants.Count)
            throw new ArgumentException("Для варианта с двумя классами выберите разные классы.");
        if (classGrants.Any(x => !policy.AllowedBaseClassNodeIds.Contains(x.DevelopmentNodeId, StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("В начальном развитии доступны только базовые немагические классы.");
        var optionMatches = policy.ClassSelectionOptions.Any(option =>
            option.ClassCount == classGrants.Count
            && classGrants.All(x => x.Rank == option.RankPerClass)
            && (!option.RequireDistinctClasses || classGrants.Select(x => x.DevelopmentNodeId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == classGrants.Count));
        if (!optionMatches)
            throw new ArgumentException("Выберите один класс второго ранга или два разных класса первого ранга.");
        if (!policy.AllowedPrimaryMagicMethodNodeIds.Contains(methodNodeId, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Выберите один из доступных первичных методов магии.");
        if (!policy.AllowedBasicMagicDirectionNodeIds.Contains(elementNodeId, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Выберите одну из четырёх базовых стихий.");
    }

    private static void ApplyInitialDevelopmentGrant02112(
        DevelopmentProfile profile,
        string characterId,
        ClassNodeDefinition node,
        int rank,
        string actorId,
        DateTime now)
    {
        profile.Nodes ??= new List<CharacterDevelopmentNodeState>();
        var state = profile.Nodes.FirstOrDefault(x => string.Equals(x.DevelopmentNodeId, node.NodeId, StringComparison.OrdinalIgnoreCase));
        if (state == null)
        {
            state = new CharacterDevelopmentNodeState { Id = Guid.NewGuid().ToString("N"), CharacterId = characterId, DevelopmentNodeId = node.NodeId };
            profile.Nodes.Add(state);
        }
        state.CharacterId = characterId;
        state.HexagonId = FirstNonEmpty(node.HexagonId, DevelopmentHexagonIds.Main);
        state.DevelopmentNodeId = node.NodeId;
        state.ClassId = FirstNonEmpty(node.ClassId, node.NodeId);
        state.NodeType = FirstNonEmpty(node.NodeType, DevelopmentNodeTypes.Class);
        state.CurrentTier = rank;
        state.MaxTier = Math.Max(rank, Math.Max(1, node.MaxTier));
        state.IsUnlocked = true;
        state.IsPurchased = true;
        state.IsAvailable = true;
        state.IsHidden = false;
        state.State = "granted";
        state.IsCompleted = false;
        state.PurchasedAtUtc = now;
        state.UpdatedAtUtc = now;
        state.CostPaid = 0;
        state.CurrencyId = CharacterCurrencyIds.XpCoin;
        state.Source = InitialDevelopmentGrantSources.InitialDevelopment;
        state.GMApprovalStatus = "not_required";
        state.Notes = $"Initial Development grant by {actorId}.";
        profile.ActiveHexagonIds ??= new List<string>();
        if (!profile.ActiveHexagonIds.Contains(state.HexagonId, StringComparer.OrdinalIgnoreCase)) profile.ActiveHexagonIds.Add(state.HexagonId);
    }

    private Dictionary<string, object> BuildInitialDevelopmentPayload02112(string characterId, DevelopmentProfile? profile, InitialDevelopmentPolicy policy)
    {
        EnsureDefinitionsLoaded(false);
        var state = profile?.InitialDevelopment ?? PendingInitialDevelopmentState02112(policy);
        var wallet = _mongo.CharacterWalletProfiles.Find(
            Builders<CharacterWalletProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
        var moBalance = (wallet?.Wallets ?? new List<CharacterWalletValue>())
            .Where(x => string.Equals(x.CurrencyId, CharacterCurrencyIds.XpCoin, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Amount);
        Dictionary<string, object> Option(string id)
        {
            if (!_nodesById.TryGetValue(id, out var node))
                throw new InvalidOperationException("Один из разрешённых путей развития не найден в справочнике.");
            var displayName = FirstNonEmpty(node.PublicName, node.Name);
            if (string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException("Для одного из разрешённых путей развития не задано публичное название.");
            return new Dictionary<string, object>
            {
                ["developmentNodeId"] = id,
                ["displayName"] = displayName
            };
        }
        return new Dictionary<string, object>
        {
            ["characterId"] = characterId,
            ["status"] = state.Status,
            ["isPending"] = !string.Equals(state.Status, InitialDevelopmentStatusIds.Completed, StringComparison.Ordinal),
            ["isCompleted"] = string.Equals(state.Status, InitialDevelopmentStatusIds.Completed, StringComparison.Ordinal),
            ["profileRevision"] = profile?.Revision ?? 0,
            ["policyId"] = policy.PolicyId,
            ["policyRevision"] = policy.EntityRevision,
            ["classSelectionOptions"] = policy.ClassSelectionOptions.Select(x => (object)new Dictionary<string, object>
            {
                ["classCount"] = x.ClassCount,
                ["rankPerClass"] = x.RankPerClass,
                ["displayName"] = x.ClassCount == 1 ? "Один класс до 2 ранга" : "Два разных класса по 1 рангу"
            }).ToArray(),
            ["baseClassOptions"] = policy.AllowedBaseClassNodeIds.Select(x => (object)Option(x)).ToArray(),
            ["magicMethodOptions"] = policy.AllowedPrimaryMagicMethodNodeIds.Select(x => (object)Option(x)).ToArray(),
            ["basicMagicDirectionOptions"] = policy.AllowedBasicMagicDirectionNodeIds.Select(x => (object)Option(x)).ToArray(),
            ["selectedClassGrants"] = state.SelectedClassGrants.Select(x => (object)new Dictionary<string, object> { ["developmentNodeId"] = x.DevelopmentNodeId, ["rank"] = x.Rank }).ToArray(),
            ["selectedMagicMethodNodeId"] = state.SelectedMagicMethodNodeId,
            ["selectedBasicMagicDirectionNodeId"] = state.SelectedBasicMagicDirectionNodeId,
            ["mustCompleteBeforeActiveSession"] = policy.MustCompleteBeforeActiveSession,
            ["playerExplanation"] = "Завершите начальное развитие персонажа перед участием в сессии.",
            ["sourceOfTruth"] = "character_development_profiles",
            ["grantProvenance"] = InitialDevelopmentGrantSources.InitialDevelopment,
            ["moCost"] = 0,
            ["moBalance"] = moBalance
        };
    }

    private static string MagicTargetScopeDisplay02112(string scope) => scope switch
    {
        MagicTargetScopeIds.Self => "На себя",
        MagicTargetScopeIds.OtherActor => "На другого персонажа",
        MagicTargetScopeIds.Object => "На объект",
        MagicTargetScopeIds.Position => "На точку",
        MagicTargetScopeIds.Area => "На область",
        _ => "Неизвестная цель"
    };

    private bool IsInitialDevelopmentEligibleForSession02112(string characterId)
    {
        var profile = LoadDevelopmentProfileDocument02112(characterId)?.Profile;
        if (profile?.InitialDevelopment == null) return true;
        if (string.Equals(profile.InitialDevelopment.Status, InitialDevelopmentStatusIds.Completed, StringComparison.Ordinal)) return true;
        var policy = LoadInitialDevelopmentPolicy02112(profile.RuleSetId);
        return policy == null || !policy.MustCompleteBeforeActiveSession;
    }
}
