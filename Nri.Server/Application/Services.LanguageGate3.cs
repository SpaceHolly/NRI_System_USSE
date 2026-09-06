using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Content;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string LanguageTrainingRuntimeKind022Gate3 = "language_training_022_gate3";
    private static readonly object LanguageMutationSync022Gate3 = new object();

    public ResponseEnvelope ContentDefinitionAdminLanguageSeedApply022Gate3(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var force = PayloadReader.GetBool(context.Request.Payload, "force");
        var created = 0;
        var updated = 0;
        foreach (var candidate in LanguageGate3SeedCatalog.BuildAll())
        {
            var existing = _mongo.ContentDefinitionRecords.Find(x => x.Id == candidate.Id).FirstOrDefault();
            if (existing == null)
            {
                candidate.CreatedByUserId = actor.Id;
                candidate.UpdatedByUserId = actor.Id;
                _mongo.ContentDefinitionRecords.InsertOne(candidate);
                created++;
                continue;
            }
            if (!string.Equals(existing.DefinitionPackId, LanguageGate3SeedCatalog.PackId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Canonical language identity collision: {candidate.Id}.");
            if (!force) continue;
            candidate.CreatedAtUtc = existing.CreatedAtUtc;
            candidate.CreatedByUserId = existing.CreatedByUserId;
            candidate.UpdatedAtUtc = DateTime.UtcNow;
            candidate.UpdatedByUserId = actor.Id;
            candidate.Revision = Math.Max(existing.Revision + 1, candidate.Revision);
            _mongo.ContentDefinitionRecords.ReplaceOne(x => x.Id == candidate.Id, candidate);
            updated++;
        }

        EnsureInitialDefinitionEditorProfiles0181();
        WriteAudit("language_gate3", actor.Id, "canonical_seed", $"created={created};updated={updated}");
        return Ok("Канонический языковой справочник применён.", new Dictionary<string, object>
        {
            ["languageCount"] = LanguageGate3SeedCatalog.BuildLanguages().Count,
            ["scriptCount"] = LanguageGate3SeedCatalog.BuildScripts().Count,
            ["familyCount"] = LanguageGate3SeedCatalog.BuildFamilies().Count,
            ["originTraditionCount"] = LanguageGate3SeedCatalog.BuildTraditions().Count,
            ["created"] = created,
            ["updated"] = updated
        });
    }

    public ResponseEnvelope ContentDefinitionPlayerLanguagesList022Gate3(CommandContext context)
    {
        GetCurrentAccount(context);
        var records = PlayerVisibleLanguageRecords022Gate3()
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(LanguageReferenceSummary022Gate3)
            .Cast<object>()
            .ToArray();
        return Ok("Доступные языки загружены.", new Dictionary<string, object> { ["languages"] = records, ["count"] = records.Length });
    }

    public ResponseEnvelope ContentDefinitionPlayerLanguageGet022Gate3(CommandContext context)
    {
        GetCurrentAccount(context);
        var languageId = RequireLength(PayloadReader.GetString(context.Request.Payload, "languageId"), 3, 160, "languageId");
        var language = PlayerVisibleLanguageRecords022Gate3().FirstOrDefault(x => string.Equals(x.Id, languageId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException("Язык недоступен.");
        return Ok("Сведения о языке загружены.", LanguagePlayerDetail022Gate3(language));
    }

    public ResponseEnvelope CharacterLanguageSummaryGet022Gate3(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var ownership = RequireLanguageCharacterOwnership022Gate3(context, actor, false);
        var profile = LoadKnowledgeProfile022Gate3(ownership.CharacterId);
        var definitions = PlayerVisibleLanguageRecords022Gate3().ToDictionary(x => x.Id, StringComparer.Ordinal);
        var rows = profile.LanguageProficiencies
            .Where(x => x.Level > 0 && definitions.ContainsKey(x.LanguageId))
            .OrderByDescending(x => x.Level)
            .ThenBy(x => definitions[x.LanguageId].DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => LanguageProficiencyRow022Gate3(x, definitions[x.LanguageId]))
            .Cast<object>()
            .ToArray();
        var activeTraining = _mongo.Projects.Find(x => x.OwnerCharacterId == ownership.CharacterId
                                                       && x.RuntimeKind == LanguageTrainingRuntimeKind022Gate3
                                                       && !x.IsArchived
                                                       && (x.Status == ProjectStatusIds.InProgress || x.Status == ProjectStatusIds.Blocked || x.Status == ProjectStatusIds.Paused))
            .ToList()
            .Select(x => LanguageTrainingPlayerProjection022Gate3(x, definitions))
            .Where(x => x != null)
            .Cast<object>()
            .ToArray();
        return Ok("Языки персонажа загружены.", new Dictionary<string, object>
        {
            ["characterName"] = ownership.CharacterDisplayName,
            ["languages"] = rows,
            ["activeTraining"] = activeTraining,
            ["revision"] = profile.Revision,
            ["emptyMessage"] = rows.Length == 0 ? "У персонажа пока нет известных языков." : string.Empty
        });
    }

    public ResponseEnvelope CharacterLanguageTrainingRequirementsGet022Gate3(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var ownership = RequireLanguageCharacterOwnership022Gate3(context, actor, false);
        var language = RequirePlayerVisibleLanguage022Gate3(context);
        var profile = LoadKnowledgeProfile022Gate3(ownership.CharacterId);
        var level = profile.LanguageProficiencies.FirstOrDefault(x => x.LanguageId == language.Id)?.Level ?? 0;
        return Ok("Требования обучения рассчитаны.", LanguageTrainingRequirementsPayload022Gate3(language, level));
    }

    public ResponseEnvelope LanguageComprehensionEvaluate022Gate3(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var ownership = RequireLanguageCharacterOwnership022Gate3(context, actor, false);
        var language = RequirePlayerVisibleLanguage022Gate3(context);
        var required = PayloadReader.GetInt(context.Request.Payload, "requiredLanguageLevel") ?? 1;
        var profile = LoadKnowledgeProfile022Gate3(ownership.CharacterId);
        var level = profile.LanguageProficiencies.FirstOrDefault(x => x.LanguageId == language.Id)?.Level ?? 0;
        var result = LanguageTrainingRules022Gate3.ResolveComprehension(level, required);
        var domain = PayloadReader.GetString(context.Request.Payload, "domain") ?? string.Empty;
        var limitations = LanguageFieldString022Gate3(language, "usageLimitations");
        var limitationApplies = !string.IsNullOrWhiteSpace(domain) && LanguageLimitationApplies022Gate3(limitations, domain);
        return Ok("Понимание определено.", new Dictionary<string, object>
        {
            ["result"] = result,
            ["resultLabel"] = LanguageComprehensionLabel022Gate3(result),
            ["characterLevel"] = level,
            ["requiredLevel"] = required,
            ["usageLimitationApplies"] = limitationApplies,
            ["publicLimitation"] = limitationApplies ? limitations : string.Empty
        });
    }

    public ResponseEnvelope CharacterLanguageTrainingStart022Gate3(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var ownership = RequireLanguageCharacterOwnership022Gate3(context, actor, false);
        var language = RequirePlayerVisibleLanguage022Gate3(context);
        var operationId = RequireLength(PayloadReader.GetString(context.Request.Payload, "operationId"), 8, 160, "operationId");
        var sourceType = RequireLength(PayloadReader.GetString(context.Request.Payload, "sourceType"), 3, 80, "sourceType");
        var sourceId = PayloadReader.GetString(context.Request.Payload, "sourceId") ?? string.Empty;
        var sourceLabel = RequireLength(PayloadReader.GetString(context.Request.Payload, "sourceLabel"), 2, 160, "sourceLabel");
        lock (LanguageMutationSync022Gate3)
        {
            var replay = _mongo.Projects.Find(x => x.CreatedOperationId == operationId && x.RuntimeKind == LanguageTrainingRuntimeKind022Gate3).FirstOrDefault();
            if (replay != null) return Ok("Проект обучения уже создан.", LanguageTrainingPlayerProjection022Gate3(replay, PlayerVisibleLanguageRecords022Gate3().ToDictionary(x => x.Id, StringComparer.Ordinal))!);
            var profile = LoadKnowledgeProfile022Gate3(ownership.CharacterId);
            var fromLevel = profile.LanguageProficiencies.FirstOrDefault(x => x.LanguageId == language.Id)?.Level ?? 0;
            if (fromLevel >= 5) return Error("Достигнут максимальный уровень владения.", ResponseStatus.Conflict, ErrorCode.Conflict);
            var existing = _mongo.Projects.Find(x => x.OwnerCharacterId == ownership.CharacterId && x.RuntimeKind == LanguageTrainingRuntimeKind022Gate3
                                                    && x.Status != ProjectStatusIds.Completed && x.Status != ProjectStatusIds.Cancelled && x.Status != ProjectStatusIds.Archived).ToList()
                .FirstOrDefault(x => LanguageProjectString022Gate3(x, "languageId") == language.Id);
            if (existing != null) return Error("Для этого языка уже есть активный проект обучения.", ResponseStatus.Conflict, ErrorCode.Conflict);
            var costClass = LanguageFieldString022Gate3(language, "costClass", LanguageCostClassIds.Modern);
            var targetLevel = fromLevel + 1;
            var sourceValid = LanguageTrainingRules022Gate3.IsSourceSufficient(costClass, targetLevel, sourceType, false);
            var project = new ProjectBaseState
            {
                Id = Guid.NewGuid().ToString("N"), CampaignId = ownership.CampaignId, RuleSetId = RuleSetIds.FantasyNriDefault,
                ProjectType = ProjectTypeIds.LanguageTraining, RuntimeKind = LanguageTrainingRuntimeKind022Gate3,
                Name = $"Изучение языка: {language.DisplayName}", PublicSummary = $"{language.DisplayName}: уровень {fromLevel} → {targetLevel}",
                Status = sourceValid ? ProjectStatusIds.InProgress : ProjectStatusIds.Blocked,
                ApprovalStatus = ProjectApprovalStatusIds.NotRequired, ProgressMode = ProjectProgressModeIds.WorkPoints,
                OwnerUserId = ownership.OwnerUserId, OwnerDisplayName = ownership.OwnerDisplayName, OwnerCharacterId = ownership.CharacterId,
                CreatedByUserId = actor.Id, UpdatedByUserId = actor.Id, WorkPointsRequired = LanguageTrainingRules022Gate3.RequiredStudyHoursFor(fromLevel),
                CurrentStageName = "Обучение", CreatedOperationId = operationId, LastOperationId = operationId,
                LastOperationCommand = CommandNames.CharacterLanguageTrainingStart,
                ExtraData = new Dictionary<string, object>
                {
                    ["languageId"] = language.Id, ["languageName"] = language.DisplayName, ["fromLevel"] = fromLevel,
                    ["targetLevel"] = targetLevel, ["costClass"] = costClass,
                    ["requiredMo"] = LanguageTrainingRules022Gate3.RequiredMoFor(costClass, fromLevel),
                    ["sourceType"] = sourceType, ["sourceId"] = sourceId, ["sourceLabel"] = sourceLabel,
                    ["sourceStatus"] = sourceValid ? LanguageTrainingSourceStatusIds022Gate3.Valid : LanguageTrainingSourceStatusIds022Gate3.Pending
                }
            };
            _mongo.Projects.InsertOne(project);
            LanguageProjectAudit022Gate3(project, actor.Id, "training_started", "Проект обучения создан.");
            TryPublishSyncEvent("language.training.changed", ownership.CampaignId, "language_training", project.Id, "created", actor.Id,
                new Dictionary<string, object> { ["characterId"] = ownership.CharacterId, ["status"] = project.Status }, operationId);
            return Ok(sourceValid ? "Обучение начато." : "Проект создан, но источник требует подтверждения GM.", LanguageTrainingPlayerProjection022Gate3(project, new Dictionary<string, ContentDefinitionRecord> { [language.Id] = language })!);
        }
    }

    public ResponseEnvelope CharacterAdminLanguageTrainingCredit022Gate3(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var project = RequireLanguageProject022Gate3(context);
        var operationId = RequireLength(PayloadReader.GetString(context.Request.Payload, "operationId"), 8, 160, "operationId");
        var expectedRevision = PayloadReader.GetInt(context.Request.Payload, "expectedRevision") ?? throw new ArgumentException("expectedRevision is required.");
        var hours = PayloadReader.GetInt(context.Request.Payload, "studyHours") ?? 0;
        var worldTimeReference = RequireLength(PayloadReader.GetString(context.Request.Payload, "worldTimeReference"), 3, 200, "worldTimeReference");
        if (hours <= 0 || hours > 10000) throw new ArgumentException("Учебные часы должны быть в диапазоне 1..10000.");
        lock (LanguageMutationSync022Gate3)
        {
            project = _mongo.Projects.Find(x => x.Id == project.Id).First();
            if (project.LastOperationId == operationId) return Ok("Учебное время уже учтено.", LanguageTrainingAdminProjection022Gate3(project));
            if (project.Revision != expectedRevision) return Error("Проект обучения был изменён.", ResponseStatus.Conflict, ErrorCode.Conflict);
            if (project.Status == ProjectStatusIds.Completed || project.Status == ProjectStatusIds.Cancelled) return Error("Проект уже завершён.", ResponseStatus.Conflict, ErrorCode.Conflict);
            project.WorkPointsDone = Math.Min(project.WorkPointsRequired, project.WorkPointsDone + hours);
            project.ProgressPercent = project.WorkPointsRequired == 0 ? 0 : project.WorkPointsDone * 100 / project.WorkPointsRequired;
            project.ExtraData["worldTimeReference"] = worldTimeReference;
            project.LastOperationId = operationId;
            project.LastOperationCommand = CommandNames.CharacterAdminLanguageTrainingCredit;
            project.UpdatedByUserId = actor.Id;
            project.UpdatedAtUtc = DateTime.UtcNow;
            project.Revision++;
            _mongo.Projects.ReplaceOne(x => x.Id == project.Id, project);
            LanguageProjectAudit022Gate3(project, actor.Id, "study_credited", $"Учтено часов: {hours}.");
            return Ok("Учебное время учтено. Глобальное время не изменялось.", LanguageTrainingAdminProjection022Gate3(project));
        }
    }

    public ResponseEnvelope CharacterAdminLanguageTrainingSourceApprove022Gate3(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var project = RequireLanguageProject022Gate3(context);
        var operationId = RequireLength(PayloadReader.GetString(context.Request.Payload, "operationId"), 8, 160, "operationId");
        var expectedRevision = PayloadReader.GetInt(context.Request.Payload, "expectedRevision") ?? throw new ArgumentException("expectedRevision is required.");
        var approved = PayloadReader.GetBool(context.Request.Payload, "approved");
        var reason = RequireLength(PayloadReader.GetString(context.Request.Payload, "reason"), 3, 500, "reason");
        lock (LanguageMutationSync022Gate3)
        {
            project = _mongo.Projects.Find(x => x.Id == project.Id).First();
            if (project.LastOperationId == operationId && project.LastOperationCommand == CommandNames.CharacterAdminLanguageTrainingSourceApprove)
                return Ok(approved ? "Источник уже подтверждён." : "Источник уже отклонён.", LanguageTrainingAdminProjection022Gate3(project));
            if (project.Revision != expectedRevision) return Error("Проект обучения был изменён.", ResponseStatus.Conflict, ErrorCode.Conflict);
            project.ExtraData["sourceStatus"] = approved ? LanguageTrainingSourceStatusIds022Gate3.Valid : LanguageTrainingSourceStatusIds022Gate3.Rejected;
            project.Status = approved ? ProjectStatusIds.InProgress : ProjectStatusIds.Blocked;
            project.GMNotes = reason;
            project.LastOperationId = operationId;
            project.LastOperationCommand = CommandNames.CharacterAdminLanguageTrainingSourceApprove;
            project.UpdatedByUserId = actor.Id;
            project.UpdatedAtUtc = DateTime.UtcNow;
            project.Revision++;
            _mongo.Projects.ReplaceOne(x => x.Id == project.Id, project);
            LanguageProjectAudit022Gate3(project, actor.Id, approved ? "source_approved" : "source_rejected", approved ? "Источник подтверждён." : "Источник отклонён.");
            return Ok(approved ? "Источник подтверждён." : "Источник отклонён.", LanguageTrainingAdminProjection022Gate3(project));
        }
    }

    public ResponseEnvelope CharacterLanguageTrainingComplete022Gate3(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var ownership = RequireLanguageCharacterOwnership022Gate3(context, actor, false);
        var project = RequireLanguageProject022Gate3(context);
        if (!string.Equals(project.OwnerCharacterId, ownership.CharacterId, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Проект обучения недоступен.");
        var operationId = RequireLength(PayloadReader.GetString(context.Request.Payload, "operationId"), 8, 160, "operationId");
        var expectedRevision = PayloadReader.GetInt(context.Request.Payload, "expectedRevision") ?? throw new ArgumentException("expectedRevision is required.");
        lock (LanguageMutationSync022Gate3)
        {
            project = _mongo.Projects.Find(x => x.Id == project.Id).First();
            if (project.Status == ProjectStatusIds.Completed && project.LastOperationId == operationId)
                return Ok("Уровень языка уже повышен.", new Dictionary<string, object> { ["completed"] = true, ["alreadyApplied"] = true });
            if (project.Revision != expectedRevision) return Error("Проект обучения был изменён.", ResponseStatus.Conflict, ErrorCode.Conflict);
            if (project.WorkPointsDone < project.WorkPointsRequired) return Error("Недостаточно учебного времени.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
            if (LanguageProjectString022Gate3(project, "sourceStatus") != LanguageTrainingSourceStatusIds022Gate3.Valid)
                return Error("Источник обучения не подтверждён.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

            var languageId = LanguageProjectString022Gate3(project, "languageId");
            var fromLevel = LanguageProjectInt022Gate3(project, "fromLevel");
            var targetLevel = LanguageProjectInt022Gate3(project, "targetLevel");
            var requiredMo = LanguageProjectInt022Gate3(project, "requiredMo");
            var knowledgeDocument = _mongo.CharacterKnowledgeProfiles.Find(x => x.CharacterId == ownership.CharacterId).FirstOrDefault()
                ?? throw new KeyNotFoundException("Профиль знаний Character v2 не найден.");
            knowledgeDocument.Profile ??= new KnowledgeProfile();
            knowledgeDocument.Profile.LanguageProficiencies ??= new List<CharacterLanguageProficiency>();
            var proficiency = knowledgeDocument.Profile.LanguageProficiencies.FirstOrDefault(x => x.LanguageId == languageId);
            var currentLevel = proficiency?.Level ?? 0;
            if (currentLevel != fromLevel) return Error("Текущий уровень языка изменился.", ResponseStatus.Conflict, ErrorCode.Conflict);
            var walletDocument = _mongo.CharacterWalletProfiles.Find(x => x.CharacterId == ownership.CharacterId).FirstOrDefault()
                ?? throw new KeyNotFoundException("Кошелёк Character v2 не найден.");
            walletDocument.Profile ??= new WalletProfile { CharacterId = ownership.CharacterId };
            walletDocument.Profile.Wallets ??= new List<CharacterWalletValue>();
            var mo = walletDocument.Profile.Wallets.FirstOrDefault(x => x.CurrencyId == CharacterCurrencyIds.XpCoin);
            var balance = mo?.Amount ?? 0;
            if (balance < requiredMo) return Error("Недостаточно MO для завершения обучения.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

            var walletBefore = _mongo.CharacterWalletProfiles.Find(x => x.CharacterId == ownership.CharacterId).First();
            var knowledgeBefore = _mongo.CharacterKnowledgeProfiles.Find(x => x.CharacterId == ownership.CharacterId).First();
            var projectBefore = _mongo.Projects.Find(x => x.Id == project.Id).First();
            try
            {
                if (mo == null)
                {
                    mo = new CharacterWalletValue { CurrencyId = CharacterCurrencyIds.XpCoin, Amount = balance, Source = "profile_native" };
                    walletDocument.Profile.Wallets.Add(mo);
                }
                mo.Amount -= requiredMo;
                mo.Source = "language_training";
                mo.Notes = $"Списано {requiredMo} MO за уровень языка.";
                if (proficiency == null)
                {
                    proficiency = new CharacterLanguageProficiency { LanguageId = languageId };
                    knowledgeDocument.Profile.LanguageProficiencies.Add(proficiency);
                }
                proficiency.Level = targetLevel;
                proficiency.SourceType = LanguageProficiencySourceTypeIds.Training;
                proficiency.SourceId = project.Id;
                proficiency.UpdatedAtUtc = DateTime.UtcNow;
                knowledgeDocument.Profile.Revision = Math.Max(1, knowledgeDocument.Profile.Revision + 1);
                knowledgeDocument.Profile.SchemaVersion = Math.Max(2, knowledgeDocument.Profile.SchemaVersion);
                project.Status = ProjectStatusIds.Completed;
                project.ResultStatus = ProjectResultStatusIds.Applied;
                project.ProgressPercent = 100;
                project.CompletedAtUtc = DateTime.UtcNow;
                project.LastOperationId = operationId;
                project.LastOperationCommand = CommandNames.CharacterLanguageTrainingComplete;
                project.UpdatedByUserId = actor.Id;
                project.UpdatedAtUtc = DateTime.UtcNow;
                project.Revision++;

                _mongo.CharacterWalletProfiles.ReplaceOne(x => x.CharacterId == ownership.CharacterId, walletDocument);
                _mongo.CharacterKnowledgeProfiles.ReplaceOne(x => x.CharacterId == ownership.CharacterId, knowledgeDocument);
                _mongo.Projects.ReplaceOne(x => x.Id == project.Id, project);
                _mongo.ExperienceCoinLedger.InsertOne(new ExperienceCoinLedgerEntry
                {
                    CampaignId = ownership.CampaignId, CharacterId = ownership.CharacterId, CharacterNameSnapshot = ownership.CharacterDisplayName,
                    ActorUserId = actor.Id, EntryType = ExperienceCoinLedgerEntryTypeIds.Spend, Amount = -requiredMo,
                    BalanceAfter = checked((int)mo.Amount), Reason = $"Обучение языку: {LanguageProjectString022Gate3(project, "languageName")}",
                    SourceType = "language_training", SourceId = project.Id, IsPlayerVisible = true
                });
            }
            catch
            {
                _mongo.CharacterWalletProfiles.ReplaceOne(x => x.CharacterId == ownership.CharacterId, walletBefore, new ReplaceOptions { IsUpsert = true });
                _mongo.CharacterKnowledgeProfiles.ReplaceOne(x => x.CharacterId == ownership.CharacterId, knowledgeBefore, new ReplaceOptions { IsUpsert = true });
                _mongo.Projects.ReplaceOne(x => x.Id == project.Id, projectBefore, new ReplaceOptions { IsUpsert = true });
                throw;
            }

            LanguageProjectAudit022Gate3(project, actor.Id, "training_completed", $"Уровень повышен до {targetLevel}; списано {requiredMo} MO.");
            TryPublishSyncEvent("language.training.completed", ownership.CampaignId, "character_knowledge", ownership.CharacterId, "updated", actor.Id,
                new Dictionary<string, object> { ["characterId"] = ownership.CharacterId, ["languageId"] = languageId, ["level"] = targetLevel }, operationId);
            return Ok("Уровень языка повышен.", new Dictionary<string, object>
            {
                ["completed"] = true, ["alreadyApplied"] = false, ["newLevel"] = targetLevel,
                ["moCharged"] = requiredMo, ["moBalance"] = mo!.Amount, ["knowledgeRevision"] = knowledgeDocument.Profile.Revision
            });
        }
    }

    public ResponseEnvelope CharacterAdminLanguageGrant022Gate3(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var ownership = RequireLanguageCharacterOwnership022Gate3(context, actor, true);
        var languageId = RequireLength(PayloadReader.GetString(context.Request.Payload, "languageId"), 3, 160, "languageId");
        var level = PayloadReader.GetInt(context.Request.Payload, "level") ?? -1;
        var expectedRevision = PayloadReader.GetInt(context.Request.Payload, "expectedRevision") ?? throw new ArgumentException("expectedRevision is required.");
        var operationId = RequireLength(PayloadReader.GetString(context.Request.Payload, "operationId"), 8, 160, "operationId");
        var reason = RequireLength(PayloadReader.GetString(context.Request.Payload, "reason"), 3, 500, "reason");
        if (level < 0 || level > 5) throw new ArgumentException("Уровень языка должен быть в диапазоне 0..5.");
        var definition = _mongo.ContentDefinitionRecords.Find(x => x.Id == languageId && x.Category == WorldLoreCalendarDefinitionCategories.Language && !x.IsArchived).FirstOrDefault()
            ?? throw new KeyNotFoundException("Язык не найден.");
        lock (LanguageMutationSync022Gate3)
        {
            var doc = _mongo.CharacterKnowledgeProfiles.Find(x => x.CharacterId == ownership.CharacterId).FirstOrDefault()
                ?? throw new KeyNotFoundException("Профиль знаний Character v2 не найден.");
            doc.Profile ??= new KnowledgeProfile();
            doc.Profile.LanguageProficiencies ??= new List<CharacterLanguageProficiency>();
            if (doc.Profile.Revision != expectedRevision) return Error("Профиль языков был изменён.", ResponseStatus.Conflict, ErrorCode.Conflict);
            var existing = doc.Profile.LanguageProficiencies.FirstOrDefault(x => x.LanguageId == languageId);
            if (existing?.SourceId == operationId) return Ok("Изменение уже применено.", LanguageProficiencyRow022Gate3(existing, definition));
            if (existing == null)
            {
                existing = new CharacterLanguageProficiency { LanguageId = languageId };
                doc.Profile.LanguageProficiencies.Add(existing);
            }
            var previous = existing.Level;
            existing.Level = level;
            existing.SourceType = LanguageProficiencySourceTypeIds.GmOverride;
            existing.SourceId = operationId;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            doc.Profile.Revision++;
            doc.Profile.SchemaVersion = Math.Max(2, doc.Profile.SchemaVersion);
            _mongo.CharacterKnowledgeProfiles.ReplaceOne(x => x.CharacterId == ownership.CharacterId, doc);
            WriteAudit("language_gate3", actor.Id, "gm_override", $"character={ownership.CharacterId};language={languageId};from={previous};to={level};reason={reason}");
            TryPublishSyncEvent("character.languages.changed", ownership.CampaignId, "character_knowledge", ownership.CharacterId, "updated", actor.Id,
                new Dictionary<string, object> { ["characterId"] = ownership.CharacterId, ["languageId"] = languageId, ["level"] = level }, operationId);
            return Ok("Владение языком обновлено.", LanguageProficiencyRow022Gate3(existing, definition));
        }
    }

    private CharacterOwnershipState RequireLanguageCharacterOwnership022Gate3(CommandContext context, UserAccount actor, bool adminOnly)
    {
        if (adminOnly && !IsAdminActor(actor)) throw new UnauthorizedAccessException("Требуются права GM.");
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var ownership = _mongo.CharacterOwnerships.Find(x => x.CharacterId == characterId && !x.IsArchived).FirstOrDefault()
            ?? throw new KeyNotFoundException("Персонаж Character v2 не найден.");
        if (!IsAdminActor(actor) && ownership.OwnerUserId != actor.Id && ownership.ControlledByUserId != actor.Id)
            throw new UnauthorizedAccessException("Персонаж недоступен.");
        return ownership;
    }

    private KnowledgeProfile LoadKnowledgeProfile022Gate3(string characterId)
    {
        var document = _mongo.CharacterKnowledgeProfiles.Find(x => x.CharacterId == characterId).FirstOrDefault();
        var profile = document?.Profile ?? new KnowledgeProfile();
        profile.LanguageProficiencies ??= new List<CharacterLanguageProficiency>();
        profile.Revision = Math.Max(1, profile.Revision);
        return profile;
    }

    private List<ContentDefinitionRecord> PlayerVisibleLanguageRecords022Gate3() => _mongo.ContentDefinitionRecords.Find(
        x => x.Category == WorldLoreCalendarDefinitionCategories.Language && !x.IsArchived
             && (x.VisibilityRule == ContentDefinitionVisibilityRules.PlayerVisible || x.VisibilityRule == ContentDefinitionVisibilityRules.Public)).ToList();

    private ContentDefinitionRecord RequirePlayerVisibleLanguage022Gate3(CommandContext context)
    {
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "languageId"), 3, 160, "languageId");
        return PlayerVisibleLanguageRecords022Gate3().FirstOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("Язык недоступен.");
    }

    private ProjectBaseState RequireLanguageProject022Gate3(CommandContext context)
    {
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "projectId"), 8, 128, "projectId");
        return _mongo.Projects.Find(x => x.Id == id && x.RuntimeKind == LanguageTrainingRuntimeKind022Gate3 && !x.IsArchived).FirstOrDefault()
            ?? throw new KeyNotFoundException("Проект обучения не найден.");
    }

    private Dictionary<string, object> LanguageReferenceSummary022Gate3(ContentDefinitionRecord language)
        => new Dictionary<string, object>
        {
            ["languageId"] = language.Id, ["name"] = language.DisplayName,
            ["roles"] = LanguageFieldList022Gate3(language, "roles").Select(LanguageRoleLabel022Gate3).ToArray(),
            ["costClass"] = LanguageCostLabel022Gate3(LanguageFieldString022Gate3(language, "costClass", LanguageCostClassIds.Modern)),
            ["summary"] = language.PublicDescription
        };

    private Dictionary<string, object> LanguagePlayerDetail022Gate3(ContentDefinitionRecord language)
    {
        var script = ResolvePlayerVisibleReference022Gate3(LanguageFieldString022Gate3(language, "primaryScript"));
        var family = ResolvePlayerVisibleReference022Gate3(LanguageFieldString022Gate3(language, "languageFamily"));
        var ancestors = LanguageFieldList022Gate3(language, "ancestorLanguages").Select(ResolvePlayerVisibleReference022Gate3).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var contacts = LanguageFieldList022Gate3(language, "contactInfluences").Select(ResolvePlayerVisibleReference022Gate3).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var traditions = _mongo.ContentDefinitionRecords.Find(x => x.Category == WorldLoreCalendarDefinitionCategories.LanguageOriginTradition && !x.IsArchived
                                                                   && (x.VisibilityRule == ContentDefinitionVisibilityRules.Public || x.VisibilityRule == ContentDefinitionVisibilityRules.PlayerVisible)).ToList()
            .Where(x => LanguageFieldString022Gate3(x, "language") == language.Id)
            .Select(x => new Dictionary<string, object> { ["name"] = x.DisplayName, ["description"] = x.PublicDescription })
            .Cast<object>().ToArray();
        return new Dictionary<string, object>
        {
            ["name"] = language.DisplayName, ["description"] = language.PublicDescription,
            ["roles"] = LanguageFieldList022Gate3(language, "roles").Select(LanguageRoleLabel022Gate3).ToArray(),
            ["script"] = script, ["family"] = family, ["ancestors"] = ancestors,
            ["contactInfluences"] = contacts,
            ["cultures"] = LanguageFieldList022Gate3(language, "cultures").ToArray(),
            ["originTraditions"] = traditions,
            ["levelDescriptions"] = LanguageFieldList022Gate3(language, "levelDescriptions").ToArray(),
            ["translationRules"] = LanguageFieldString022Gate3(language, "translationRules"),
            ["usageLimitations"] = LanguageFieldString022Gate3(language, "usageLimitations")
        };
    }

    private Dictionary<string, object> LanguageProficiencyRow022Gate3(CharacterLanguageProficiency value, ContentDefinitionRecord language)
    {
        var payload = LanguageTrainingRequirementsPayload022Gate3(language, value.Level);
        payload["languageId"] = value.LanguageId;
        payload["name"] = language.DisplayName;
        payload["level"] = value.Level;
        payload["levelLabel"] = LanguageLevelLabel022Gate3(value.Level);
        payload["sourceLabel"] = LanguageSourceLabel022Gate3(value.SourceType);
        return payload;
    }

    private Dictionary<string, object> LanguageTrainingRequirementsPayload022Gate3(ContentDefinitionRecord language, int currentLevel)
    {
        var result = new Dictionary<string, object> { ["languageId"] = language.Id, ["name"] = language.DisplayName, ["currentLevel"] = currentLevel };
        if (currentLevel >= 5)
        {
            result["canTrain"] = false;
            result["blockReason"] = "Достигнут максимальный уровень владения.";
            return result;
        }
        var costClass = LanguageFieldString022Gate3(language, "costClass", LanguageCostClassIds.Modern);
        result["canTrain"] = true;
        result["targetLevel"] = currentLevel + 1;
        result["requiredMo"] = LanguageTrainingRules022Gate3.RequiredMoFor(costClass, currentLevel);
        result["requiredStudyHours"] = LanguageTrainingRules022Gate3.RequiredStudyHoursFor(currentLevel);
        result["costClassLabel"] = LanguageCostLabel022Gate3(costClass);
        result["sourceRequirement"] = LanguageSourceRequirementLabel022Gate3(costClass, currentLevel + 1);
        result["blockReason"] = string.Empty;
        return result;
    }

    private Dictionary<string, object>? LanguageTrainingPlayerProjection022Gate3(ProjectBaseState project, IReadOnlyDictionary<string, ContentDefinitionRecord> languages)
    {
        var id = LanguageProjectString022Gate3(project, "languageId");
        if (!languages.TryGetValue(id, out var language)) return null;
        return new Dictionary<string, object>
        {
            ["projectId"] = project.Id, ["languageId"] = language.Id, ["languageName"] = language.DisplayName,
            ["fromLevel"] = LanguageProjectInt022Gate3(project, "fromLevel"), ["targetLevel"] = LanguageProjectInt022Gate3(project, "targetLevel"),
            ["requiredStudyHours"] = project.WorkPointsRequired, ["accumulatedStudyHours"] = project.WorkPointsDone,
            ["remainingStudyHours"] = Math.Max(0, project.WorkPointsRequired - project.WorkPointsDone), ["requiredMo"] = LanguageProjectInt022Gate3(project, "requiredMo"),
            ["sourceLabel"] = LanguageProjectString022Gate3(project, "sourceLabel"), ["sourceStatusLabel"] = LanguageSourceStatusLabel022Gate3(LanguageProjectString022Gate3(project, "sourceStatus")),
            ["statusLabel"] = LanguageProjectStatusLabel022Gate3(project.Status), ["revision"] = project.Revision
        };
    }

    private Dictionary<string, object> LanguageTrainingAdminProjection022Gate3(ProjectBaseState project)
    {
        var result = LanguageTrainingPlayerProjection022Gate3(project, _mongo.ContentDefinitionRecords.Find(x => x.Category == WorldLoreCalendarDefinitionCategories.Language).ToList().ToDictionary(x => x.Id, StringComparer.Ordinal))
                     ?? new Dictionary<string, object>();
        result["sourceType"] = LanguageProjectString022Gate3(project, "sourceType");
        result["worldTimeReference"] = LanguageProjectString022Gate3(project, "worldTimeReference");
        result["gmNotes"] = project.GMNotes;
        return result;
    }

    private void LanguageProjectAudit022Gate3(ProjectBaseState project, string actorId, string action, string summary)
        => _mongo.ProjectAuditEntries.InsertOne(new ProjectAuditEntryState
        {
            ProjectId = project.Id, CampaignId = project.CampaignId, ActionType = action, ActorUserId = actorId,
            Summary = summary, PublicSummary = summary, IsPlayerVisible = true, VisibilityMode = ProjectVisibilityModeIds.PlayerVisible
        });

    private string ResolvePlayerVisibleReference022Gate3(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        var record = _mongo.ContentDefinitionRecords.Find(x => x.Id == id && !x.IsArchived
                                                               && (x.VisibilityRule == ContentDefinitionVisibilityRules.Public || x.VisibilityRule == ContentDefinitionVisibilityRules.PlayerVisible)).FirstOrDefault();
        return record?.DisplayName ?? string.Empty;
    }

    private static string LanguageFieldString022Gate3(ContentDefinitionRecord record, string key, string fallback = "")
    {
        if (record.CustomFields == null || !record.CustomFields.TryGetValue(key, out var raw) || raw == null) return fallback;
        if (raw is BsonValue bson) return bson.IsBsonNull ? fallback : bson.ToString();
        var value = Convert.ToString(raw)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static List<string> LanguageFieldList022Gate3(ContentDefinitionRecord record, string key)
    {
        if (record.CustomFields == null || !record.CustomFields.TryGetValue(key, out var raw) || raw == null) return new List<string>();
        if (raw is BsonArray bsonArray) return bsonArray.Where(x => !x.IsBsonNull).Select(x => x.ToString()).Where(x => x.Length > 0).ToList();
        if (raw is IEnumerable values && raw is not string) return values.Cast<object>().Select(x => Convert.ToString(x)?.Trim() ?? string.Empty).Where(x => x.Length > 0).ToList();
        return Convert.ToString(raw)?.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList() ?? new List<string>();
    }

    private static string LanguageProjectString022Gate3(ProjectBaseState project, string key)
        => project.ExtraData != null && project.ExtraData.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private static int LanguageProjectInt022Gate3(ProjectBaseState project, string key)
    {
        var raw = LanguageProjectString022Gate3(project, key);
        return int.TryParse(raw, out var value) ? value : 0;
    }

    private static bool LanguageLimitationApplies022Gate3(string limitations, string domain)
        => limitations.IndexOf(domain.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;

    private static string LanguageLevelLabel022Gate3(int level) => level switch
    {
        1 => "Начальные знания", 2 => "Бытовое владение", 3 => "Свободное владение",
        4 => "Высокое владение", 5 => "Глубокое владение", _ => "Неизвестен"
    };

    private static string LanguageRoleLabel022Gate3(string role) => role switch
    {
        "continental" => "Континентальный", "state" => "Государственный", "political_cultural" => "Политико-культурный",
        "racial" => "Культурное наследие", "religious" => "Религиозный", "ancient" => "Древний", "contact" => "Контактный", _ => role
    };

    private static string LanguageCostLabel022Gate3(string value) => value switch
    {
        LanguageCostClassIds.Religious => "Религиозный", LanguageCostClassIds.Ancient => "Древний", _ => "Современный"
    };

    private static string LanguageSourceLabel022Gate3(string source) => source switch
    {
        LanguageProficiencySourceTypeIds.Native => "Родной", LanguageProficiencySourceTypeIds.Heritage => "Наследие",
        LanguageProficiencySourceTypeIds.Education => "Образование", LanguageProficiencySourceTypeIds.InitialKnowledge => "Начальные знания",
        LanguageProficiencySourceTypeIds.Training => "Обучение", LanguageProficiencySourceTypeIds.GmOverride => "Назначено GM", _ => "Источник не указан"
    };

    private static string LanguageSourceRequirementLabel022Gate3(string costClass, int targetLevel)
    {
        if (costClass == LanguageCostClassIds.Ancient) return targetLevel >= 4 ? "Редкий источник, подтверждённый GM" : "Архив, словарь или сравнительное исследование";
        if (costClass == LanguageCostClassIds.Religious) return targetLevel >= 4 ? "Канонический религиозный источник или школа" : "Учитель, материалы или религиозный корпус";
        return targetLevel >= 4 ? "Учитель, сильный корпус или активное погружение" : "Учитель, материалы, самообучение или активное погружение";
    }

    private static string LanguageSourceStatusLabel022Gate3(string status) => status switch
    {
        LanguageTrainingSourceStatusIds022Gate3.Valid => "Источник подтверждён", LanguageTrainingSourceStatusIds022Gate3.Rejected => "Источник отклонён", _ => "Ожидает подтверждения"
    };

    private static string LanguageProjectStatusLabel022Gate3(string status) => status switch
    {
        ProjectStatusIds.InProgress => "В процессе", ProjectStatusIds.Blocked => "Заблокировано", ProjectStatusIds.Paused => "Приостановлено",
        ProjectStatusIds.Completed => "Завершено", ProjectStatusIds.Cancelled => "Отменено", _ => status
    };

    private static string LanguageComprehensionLabel022Gate3(string result) => result switch
    {
        LanguageComprehensionResultIds022Gate3.Full => "Полное понимание", LanguageComprehensionResultIds022Gate3.Partial => "Общий смысл",
        LanguageComprehensionResultIds022Gate3.Fragments => "Отдельные фрагменты", _ => "Недоступно"
    };
}
