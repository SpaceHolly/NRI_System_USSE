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
    public ResponseEnvelope LegalAdminDashboardGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!LegalAdminEnabled()) return LegalDisabled(context.Request.Command);

        var applications = _mongo.LegalLicenseApplications.Find(Builders<LicenseApplicationState>.Filter.Empty).ToList();
        var checks = _mongo.LegalCheckRecords.Find(Builders<LegalCheckRecordState>.Filter.Empty).ToList();
        var production = _mongo.LegalProductionLegalityStates.Find(Builders<ProductionLegalityState>.Filter.Empty).ToList();
        return Ok("Legal dashboard loaded.", new Dictionary<string, object>
        {
            ["isEnabled"] = true,
            ["metrics"] = new object[]
            {
                LegalMetric("Юрисдикции", _mongo.LegalJurisdictions.CountDocuments(Builders<JurisdictionDefinition>.Filter.Eq(x => x.IsArchived, false)), "Профили законов по странам, регионам и городам."),
                LegalMetric("Правила", _mongo.LegalRules.CountDocuments(Builders<LegalRuleDefinition>.Filter.Eq(x => x.IsArchived, false)), "Action-specific правила: владение, ношение, производство и другие действия."),
                LegalMetric("Заявки", applications.Count(x => x.Status == LicenseApplicationStatusIds.Submitted || x.Status == LicenseApplicationStatusIds.InReview), "Ожидающие заявки на лицензии и разрешения."),
                LegalMetric("GM review", checks.Count(x => x.RequiresGMReview), "Проверки законности, требующие решения GM.")
            },
            ["applications"] = applications.OrderByDescending(x => x.UpdatedAtUtc).Take(20).Select(x => (object)LegalApplicationPayload(x, includeAdmin: true)).ToArray(),
            ["checks"] = checks.OrderByDescending(x => x.CheckedAtUtc).Take(20).Select(x => (object)LegalCheckRecordPayload(x, includeAdmin: true)).ToArray(),
            ["productionModes"] = production.OrderByDescending(x => x.UpdatedAtUtc).Take(20).Select(x => (object)ProductionLegalityPayload(x, includeAdmin: true)).ToArray(),
            ["builtAtUtc"] = DateTime.UtcNow
        });
    }

    public ResponseEnvelope LegalJurisdictionList(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseJurisdictionProfiles))) return LegalDisabled(context.Request.Command);
        var campaignId = LegalCampaignId(context);
        var items = _mongo.LegalJurisdictions.Find(Builders<JurisdictionDefinition>.Filter.Eq(x => x.CampaignId, campaignId))
            .ToList()
            .Where(x => !x.IsArchived || PayloadReader.GetBool(context.Request.Payload, "includeArchived"))
            .OrderBy(x => x.Name)
            .Select(x => (object)JurisdictionPayload(x, includeAdmin: true))
            .ToArray();
        _logger.Admin($"legal.jurisdiction.list actor={actor.Login} campaign={campaignId} count={items.Length}");
        return Ok("Legal jurisdictions loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope LegalJurisdictionCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseJurisdictionProfiles))) return LegalDisabled(context.Request.Command);
        var now = DateTime.UtcNow;
        var item = new JurisdictionDefinition
        {
            CampaignId = LegalCampaignId(context),
            RuleSetId = LegalString(context, "ruleSetId", 0, 128),
            Name = LegalRequired(context, "name", 2, 160),
            JurisdictionType = FirstNonEmpty(LegalString(context, "jurisdictionType", 0, 64), "country"),
            ParentJurisdictionId = LegalString(context, "parentJurisdictionId", 0, 128),
            LinkedEntityType = LegalString(context, "linkedEntityType", 0, 64),
            LinkedEntityId = LegalString(context, "linkedEntityId", 0, 128),
            Description = LegalString(context, "description", 0, 4096),
            PublicSummary = LegalString(context, "publicSummary", 0, 2048),
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            VisibilityMode = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible") ? "player_visible" : "gm_only",
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _mongo.LegalJurisdictions.InsertOne(item);
        WriteAudit("legal", actor.Id, "jurisdiction.create", item.Id);
        LegalPublish("legal.jurisdiction.created", item.CampaignId, item.Id, item.Name, item.IsPlayerVisible);
        return Ok("Legal jurisdiction created.", new Dictionary<string, object> { ["item"] = JurisdictionPayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalJurisdictionUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseJurisdictionProfiles))) return LegalDisabled(context.Request.Command);
        var item = RequireLegalDocument(_mongo.LegalJurisdictions, LegalRequired(context, "jurisdictionId", 1, 128), "jurisdiction");
        item.Name = FirstNonEmpty(LegalString(context, "name", 0, 160), item.Name);
        item.JurisdictionType = FirstNonEmpty(LegalString(context, "jurisdictionType", 0, 64), item.JurisdictionType);
        item.ParentJurisdictionId = LegalStringOrExisting(context, "parentJurisdictionId", item.ParentJurisdictionId, 128);
        item.LinkedEntityType = LegalStringOrExisting(context, "linkedEntityType", item.LinkedEntityType, 64);
        item.LinkedEntityId = LegalStringOrExisting(context, "linkedEntityId", item.LinkedEntityId, 128);
        item.Description = LegalStringOrExisting(context, "description", item.Description, 4096);
        item.PublicSummary = LegalStringOrExisting(context, "publicSummary", item.PublicSummary, 2048);
        if (context.Request.Payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible");
        item.VisibilityMode = item.IsPlayerVisible ? "player_visible" : "gm_only";
        item.UpdatedByUserId = actor.Id;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.LegalJurisdictions.ReplaceOne(x => x.Id == item.Id, item);
        WriteAudit("legal", actor.Id, "jurisdiction.update", item.Id);
        LegalPublish("legal.jurisdiction.changed", item.CampaignId, item.Id, item.Name, item.IsPlayerVisible);
        return Ok("Legal jurisdiction updated.", new Dictionary<string, object> { ["item"] = JurisdictionPayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalJurisdictionArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseJurisdictionProfiles))) return LegalDisabled(context.Request.Command);
        var item = RequireLegalDocument(_mongo.LegalJurisdictions, LegalRequired(context, "jurisdictionId", 1, 128), "jurisdiction");
        item.IsArchived = true;
        item.UpdatedByUserId = actor.Id;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.LegalJurisdictions.ReplaceOne(x => x.Id == item.Id, item);
        WriteAudit("legal", actor.Id, "jurisdiction.archive", item.Id);
        return Ok("Legal jurisdiction archived.", new Dictionary<string, object> { ["item"] = JurisdictionPayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalProfileList(CommandContext context)
    {
        RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseJurisdictionProfiles))) return LegalDisabled(context.Request.Command);
        var campaignId = LegalCampaignId(context);
        var jurisdictionId = LegalString(context, "jurisdictionId", 0, 128);
        var filter = Builders<LegalProfileState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(jurisdictionId)) filter &= Builders<LegalProfileState>.Filter.Eq(x => x.JurisdictionId, jurisdictionId);
        var items = _mongo.LegalProfiles.Find(filter).ToList()
            .Where(x => !x.IsArchived || PayloadReader.GetBool(context.Request.Payload, "includeArchived"))
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.Name)
            .Select(x => (object)LegalProfilePayload(x))
            .ToArray();
        return Ok("Legal profiles loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope LegalProfileCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseJurisdictionProfiles))) return LegalDisabled(context.Request.Command);
        var now = DateTime.UtcNow;
        var item = new LegalProfileState
        {
            CampaignId = LegalCampaignId(context),
            JurisdictionId = LegalRequired(context, "jurisdictionId", 1, 128),
            Name = LegalRequired(context, "name", 2, 160),
            Description = LegalString(context, "description", 0, 4096),
            IsActive = !context.Request.Payload.ContainsKey("isActive") || PayloadReader.GetBool(context.Request.Payload, "isActive"),
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        if (item.IsActive) DeactivateLegalProfiles(item.CampaignId, item.JurisdictionId, actor.Id);
        _mongo.LegalProfiles.InsertOne(item);
        WriteAudit("legal", actor.Id, "profile.create", item.Id);
        return Ok("Legal profile created.", new Dictionary<string, object> { ["item"] = LegalProfilePayload(item) });
    }

    public ResponseEnvelope LegalProfileSetActive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseJurisdictionProfiles))) return LegalDisabled(context.Request.Command);
        var item = RequireLegalDocument(_mongo.LegalProfiles, LegalRequired(context, "profileId", 1, 128), "legal profile");
        DeactivateLegalProfiles(item.CampaignId, item.JurisdictionId, actor.Id);
        item.IsActive = true;
        item.UpdatedByUserId = actor.Id;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.LegalProfiles.ReplaceOne(x => x.Id == item.Id, item);
        WriteAudit("legal", actor.Id, "profile.setActive", item.Id);
        return Ok("Legal profile activated.", new Dictionary<string, object> { ["item"] = LegalProfilePayload(item) });
    }

    public ResponseEnvelope LegalRuleList(CommandContext context)
    {
        RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLegalActionChecks))) return LegalDisabled(context.Request.Command);
        var campaignId = LegalCampaignId(context);
        var filter = Builders<LegalRuleDefinition>.Filter.Eq(x => x.CampaignId, campaignId);
        var jurisdictionId = LegalString(context, "jurisdictionId", 0, 128);
        if (!string.IsNullOrWhiteSpace(jurisdictionId)) filter &= Builders<LegalRuleDefinition>.Filter.Eq(x => x.JurisdictionId, jurisdictionId);
        var items = _mongo.LegalRules.Find(filter).ToList()
            .Where(x => !x.IsArchived || PayloadReader.GetBool(context.Request.Payload, "includeArchived"))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Select(x => (object)LegalRulePayload(x, includeAdmin: true))
            .ToArray();
        return Ok("Legal rules loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope LegalRuleCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLegalActionChecks))) return LegalDisabled(context.Request.Command);
        var now = DateTime.UtcNow;
        var item = new LegalRuleDefinition
        {
            CampaignId = LegalCampaignId(context),
            JurisdictionId = LegalString(context, "jurisdictionId", 0, 128),
            LegalProfileId = LegalString(context, "legalProfileId", 0, 128),
            Name = LegalRequired(context, "name", 2, 180),
            ActionType = NormalizeLegalAction(LegalString(context, "actionType", 0, 64)),
            SubjectKind = FirstNonEmpty(LegalString(context, "subjectKind", 0, 64), LegalSubjectKindIds.Any),
            SubjectStatus = LegalString(context, "subjectStatus", 0, 64),
            ObjectType = FirstNonEmpty(LegalString(context, "objectType", 0, 64), "any"),
            ObjectCategory = LegalString(context, "objectCategory", 0, 128),
            LegalStatus = NormalizeLegalStatus(FirstNonEmpty(LegalString(context, "legalStatus", 0, 64), LegalStatusIds.Unknown)),
            DeJureStatus = NormalizeLegalStatus(FirstNonEmpty(LegalString(context, "deJureStatus", 0, 64), LegalString(context, "legalStatus", 0, 64))),
            DeFactoStatus = NormalizeLegalStatus(FirstNonEmpty(LegalString(context, "deFactoStatus", 0, 64), LegalString(context, "legalStatus", 0, 64))),
            RequiredLicenseDefinitionId = LegalString(context, "requiredLicenseDefinitionId", 0, 128),
            RequiredPermitType = LegalString(context, "requiredPermitType", 0, 128),
            RequiresGMReview = PayloadReader.GetBool(context.Request.Payload, "requiresGMReview"),
            IsBlocked = PayloadReader.GetBool(context.Request.Payload, "isBlocked"),
            RiskLevel = NormalizeLegalRisk(LegalString(context, "riskLevel", 0, 64)),
            Priority = PayloadReader.GetInt(context.Request.Payload, "priority") ?? 0,
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            PublicWarning = LegalString(context, "publicWarning", 0, 2048),
            AdminNotes = LegalString(context, "adminNotes", 0, 4096),
            GMHiddenLegalTerms = LegalString(context, "gmHiddenLegalTerms", 0, 4096),
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ObjectTags = LegalStringList(context.Request.Payload, "objectTags")
        };
        _mongo.LegalRules.InsertOne(item);
        WriteAudit("legal", actor.Id, "rule.create", item.Id);
        LegalPublish("legal.rule.created", item.CampaignId, item.Id, item.Name, item.IsPlayerVisible);
        return Ok("Legal rule created.", new Dictionary<string, object> { ["item"] = LegalRulePayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalRuleUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLegalActionChecks))) return LegalDisabled(context.Request.Command);
        var item = RequireLegalDocument(_mongo.LegalRules, LegalRequired(context, "ruleId", 1, 128), "legal rule");
        item.Name = FirstNonEmpty(LegalString(context, "name", 0, 180), item.Name);
        item.ActionType = NormalizeLegalAction(FirstNonEmpty(LegalString(context, "actionType", 0, 64), item.ActionType));
        item.SubjectKind = FirstNonEmpty(LegalString(context, "subjectKind", 0, 64), item.SubjectKind);
        item.SubjectStatus = LegalStringOrExisting(context, "subjectStatus", item.SubjectStatus, 64);
        item.ObjectType = FirstNonEmpty(LegalString(context, "objectType", 0, 64), item.ObjectType);
        item.ObjectCategory = LegalStringOrExisting(context, "objectCategory", item.ObjectCategory, 128);
        item.LegalStatus = NormalizeLegalStatus(FirstNonEmpty(LegalString(context, "legalStatus", 0, 64), item.LegalStatus));
        item.DeJureStatus = NormalizeLegalStatus(FirstNonEmpty(LegalString(context, "deJureStatus", 0, 64), item.DeJureStatus));
        item.DeFactoStatus = NormalizeLegalStatus(FirstNonEmpty(LegalString(context, "deFactoStatus", 0, 64), item.DeFactoStatus));
        item.RequiredLicenseDefinitionId = LegalStringOrExisting(context, "requiredLicenseDefinitionId", item.RequiredLicenseDefinitionId, 128);
        item.RequiredPermitType = LegalStringOrExisting(context, "requiredPermitType", item.RequiredPermitType, 128);
        if (context.Request.Payload.ContainsKey("requiresGMReview")) item.RequiresGMReview = PayloadReader.GetBool(context.Request.Payload, "requiresGMReview");
        if (context.Request.Payload.ContainsKey("isBlocked")) item.IsBlocked = PayloadReader.GetBool(context.Request.Payload, "isBlocked");
        item.RiskLevel = NormalizeLegalRisk(FirstNonEmpty(LegalString(context, "riskLevel", 0, 64), item.RiskLevel));
        item.Priority = PayloadReader.GetInt(context.Request.Payload, "priority") ?? item.Priority;
        if (context.Request.Payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible");
        item.PublicWarning = LegalStringOrExisting(context, "publicWarning", item.PublicWarning, 2048);
        item.AdminNotes = LegalStringOrExisting(context, "adminNotes", item.AdminNotes, 4096);
        item.GMHiddenLegalTerms = LegalStringOrExisting(context, "gmHiddenLegalTerms", item.GMHiddenLegalTerms, 4096);
        var tags = LegalStringList(context.Request.Payload, "objectTags");
        if (tags.Count > 0) item.ObjectTags = tags;
        item.UpdatedByUserId = actor.Id;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.LegalRules.ReplaceOne(x => x.Id == item.Id, item);
        WriteAudit("legal", actor.Id, "rule.update", item.Id);
        LegalPublish("legal.rule.changed", item.CampaignId, item.Id, item.Name, item.IsPlayerVisible);
        return Ok("Legal rule updated.", new Dictionary<string, object> { ["item"] = LegalRulePayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalRuleArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLegalActionChecks))) return LegalDisabled(context.Request.Command);
        var item = RequireLegalDocument(_mongo.LegalRules, LegalRequired(context, "ruleId", 1, 128), "legal rule");
        item.IsArchived = true;
        item.UpdatedByUserId = actor.Id;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.LegalRules.ReplaceOne(x => x.Id == item.Id, item);
        WriteAudit("legal", actor.Id, "rule.archive", item.Id);
        return Ok("Legal rule archived.", new Dictionary<string, object> { ["item"] = LegalRulePayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalLicenseDefinitionList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var admin = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
        if (admin)
        {
            if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLicenseDefinitions))) return LegalDisabled(context.Request.Command);
        }
        else if (!LegalPlayerEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLicenseDefinitions))) return LegalDisabled(context.Request.Command);

        var campaignId = LegalCampaignId(context);
        var items = _mongo.LegalLicenseDefinitions.Find(Builders<LicenseDefinition>.Filter.Eq(x => x.CampaignId, campaignId)).ToList()
            .Where(x => !x.IsArchived && (admin || x.IsPlayerVisible))
            .OrderBy(x => x.Name)
            .Select(x => (object)LicenseDefinitionPayload(x, includeAdmin: admin))
            .ToArray();
        return Ok("Legal license definitions loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope LegalLicenseDefinitionCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLicenseDefinitions))) return LegalDisabled(context.Request.Command);
        var now = DateTime.UtcNow;
        var item = new LicenseDefinition
        {
            CampaignId = LegalCampaignId(context),
            JurisdictionId = LegalString(context, "jurisdictionId", 0, 128),
            Name = LegalRequired(context, "name", 2, 180),
            LicenseType = FirstNonEmpty(LegalString(context, "licenseType", 0, 64), "general"),
            AppliesToActionType = NormalizeLegalAction(LegalString(context, "appliesToActionType", 0, 64)),
            AppliesToObjectType = LegalString(context, "appliesToObjectType", 0, 64),
            AppliesToObjectCategory = LegalString(context, "appliesToObjectCategory", 0, 128),
            PublicSummary = LegalString(context, "publicSummary", 0, 2048),
            AdminNotes = LegalString(context, "adminNotes", 0, 4096),
            RequiresGMApproval = !context.Request.Payload.ContainsKey("requiresGMApproval") || PayloadReader.GetBool(context.Request.Payload, "requiresGMApproval"),
            IsPlayerVisible = !context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _mongo.LegalLicenseDefinitions.InsertOne(item);
        WriteAudit("legal", actor.Id, "licenseDefinition.create", item.Id);
        LegalPublish("legal.licenseDefinition.changed", item.CampaignId, item.Id, item.Name, item.IsPlayerVisible);
        return Ok("Legal license definition created.", new Dictionary<string, object> { ["item"] = LicenseDefinitionPayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalLicenseDefinitionUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLicenseDefinitions))) return LegalDisabled(context.Request.Command);
        var item = RequireLegalDocument(_mongo.LegalLicenseDefinitions, LegalRequired(context, "licenseDefinitionId", 1, 128), "license definition");
        item.Name = FirstNonEmpty(LegalString(context, "name", 0, 180), item.Name);
        item.LicenseType = FirstNonEmpty(LegalString(context, "licenseType", 0, 64), item.LicenseType);
        item.AppliesToActionType = NormalizeLegalAction(FirstNonEmpty(LegalString(context, "appliesToActionType", 0, 64), item.AppliesToActionType));
        item.AppliesToObjectType = LegalStringOrExisting(context, "appliesToObjectType", item.AppliesToObjectType, 64);
        item.AppliesToObjectCategory = LegalStringOrExisting(context, "appliesToObjectCategory", item.AppliesToObjectCategory, 128);
        item.PublicSummary = LegalStringOrExisting(context, "publicSummary", item.PublicSummary, 2048);
        item.AdminNotes = LegalStringOrExisting(context, "adminNotes", item.AdminNotes, 4096);
        if (context.Request.Payload.ContainsKey("requiresGMApproval")) item.RequiresGMApproval = PayloadReader.GetBool(context.Request.Payload, "requiresGMApproval");
        if (context.Request.Payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible");
        item.UpdatedByUserId = actor.Id;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.LegalLicenseDefinitions.ReplaceOne(x => x.Id == item.Id, item);
        WriteAudit("legal", actor.Id, "licenseDefinition.update", item.Id);
        return Ok("Legal license definition updated.", new Dictionary<string, object> { ["item"] = LicenseDefinitionPayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalLicenseDefinitionArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLicenseDefinitions))) return LegalDisabled(context.Request.Command);
        var item = RequireLegalDocument(_mongo.LegalLicenseDefinitions, LegalRequired(context, "licenseDefinitionId", 1, 128), "license definition");
        item.IsArchived = true;
        item.UpdatedByUserId = actor.Id;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.LegalLicenseDefinitions.ReplaceOne(x => x.Id == item.Id, item);
        WriteAudit("legal", actor.Id, "licenseDefinition.archive", item.Id);
        return Ok("Legal license definition archived.", new Dictionary<string, object> { ["item"] = LicenseDefinitionPayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalEntityLicenseList(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseEntityLicenses))) return LegalDisabled(context.Request.Command);
        var campaignId = LegalCampaignId(context);
        var holderEntityType = LegalString(context, "holderEntityType", 0, 64);
        var holderEntityId = LegalString(context, "holderEntityId", 0, 128);
        var filter = Builders<EntityLicenseState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(holderEntityType)) filter &= Builders<EntityLicenseState>.Filter.Eq(x => x.HolderEntityType, holderEntityType);
        if (!string.IsNullOrWhiteSpace(holderEntityId)) filter &= Builders<EntityLicenseState>.Filter.Eq(x => x.HolderEntityId, holderEntityId);
        var items = _mongo.LegalEntityLicenses.Find(filter).ToList()
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)EntityLicensePayload(x, includeAdmin: true))
            .ToArray();
        _logger.Admin($"legal.entityLicense.list actor={actor.Login} count={items.Length}");
        return Ok("Entity licenses loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope LegalEntityLicenseIssue(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseEntityLicenses))) return LegalDisabled(context.Request.Command);
        var licenseDefinitionId = LegalRequired(context, "licenseDefinitionId", 1, 128);
        var definition = RequireLegalDocument(_mongo.LegalLicenseDefinitions, licenseDefinitionId, "license definition");
        var now = DateTime.UtcNow;
        var item = new EntityLicenseState
        {
            CampaignId = LegalCampaignId(context),
            LicenseDefinitionId = licenseDefinitionId,
            JurisdictionId = FirstNonEmpty(LegalString(context, "jurisdictionId", 0, 128), definition.JurisdictionId),
            HolderEntityType = FirstNonEmpty(LegalString(context, "holderEntityType", 0, 64), "character"),
            HolderEntityId = LegalRequired(context, "holderEntityId", 1, 128),
            HolderUserId = LegalString(context, "holderUserId", 0, 128),
            DisplayName = FirstNonEmpty(LegalString(context, "displayName", 0, 180), definition.Name),
            Status = LicenseStatusIds.Active,
            IssuedAtUtc = now,
            IssuedByUserId = actor.Id,
            PublicNotes = LegalString(context, "publicNotes", 0, 2048),
            GMNotes = LegalString(context, "gmNotes", 0, 4096),
            IsPlayerVisible = !context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            UpdatedAtUtc = now
        };
        _mongo.LegalEntityLicenses.InsertOne(item);
        WriteAudit("legal", actor.Id, "license.issue", item.Id);
        LegalPublish("legal.entityLicense.changed", item.CampaignId, item.Id, item.DisplayName, item.IsPlayerVisible);
        return Ok("Entity license issued.", new Dictionary<string, object> { ["item"] = EntityLicensePayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalEntityLicenseSuspend(CommandContext context) => LegalEntityLicenseSetStatus(context, LicenseStatusIds.Suspended, "license.suspend");
    public ResponseEnvelope LegalEntityLicenseRevoke(CommandContext context) => LegalEntityLicenseSetStatus(context, LicenseStatusIds.Revoked, "license.revoke");

    public ResponseEnvelope LegalApplicationList(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLicenseApplications))) return LegalDisabled(context.Request.Command);
        var campaignId = LegalCampaignId(context);
        var status = LegalString(context, "status", 0, 64);
        var filter = Builders<LicenseApplicationState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(status)) filter &= Builders<LicenseApplicationState>.Filter.Eq(x => x.Status, status);
        var items = _mongo.LegalLicenseApplications.Find(filter).ToList()
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)LegalApplicationPayload(x, includeAdmin: true))
            .ToArray();
        _logger.Admin($"legal.application.list actor={actor.Login} count={items.Length}");
        return Ok("Legal applications loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope LegalApplicationReview(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLicenseApplications))) return LegalDisabled(context.Request.Command);
        var application = RequireLegalDocument(_mongo.LegalLicenseApplications, LegalRequired(context, "applicationId", 1, 128), "license application");
        var decision = FirstNonEmpty(LegalString(context, "decision", 0, 64), "in_review").ToLowerInvariant();
        application.Status = decision switch
        {
            "approve" or "approved" => LicenseApplicationStatusIds.Approved,
            "reject" or "rejected" => LicenseApplicationStatusIds.Rejected,
            "issue" or "issued" => LicenseApplicationStatusIds.Issued,
            _ => LicenseApplicationStatusIds.InReview
        };
        application.GMResponse = LegalStringOrExisting(context, "gmResponse", application.GMResponse, 2048);
        application.GMNotes = LegalStringOrExisting(context, "gmNotes", application.GMNotes, 4096);
        application.ReviewedByUserId = actor.Id;
        application.ReviewedAtUtc = DateTime.UtcNow;
        application.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.LegalLicenseApplications.ReplaceOne(x => x.Id == application.Id, application);

        EntityLicenseState? issued = null;
        if (application.Status == LicenseApplicationStatusIds.Issued || PayloadReader.GetBool(context.Request.Payload, "issueLicense"))
        {
            var definition = RequireLegalDocument(_mongo.LegalLicenseDefinitions, application.LicenseDefinitionId, "license definition");
            issued = new EntityLicenseState
            {
                CampaignId = application.CampaignId,
                LicenseDefinitionId = application.LicenseDefinitionId,
                JurisdictionId = application.JurisdictionId,
                HolderEntityType = application.ApplicantEntityType,
                HolderEntityId = application.ApplicantEntityId,
                HolderUserId = application.ApplicantUserId,
                DisplayName = FirstNonEmpty(definition.Name, application.Title),
                Status = LicenseStatusIds.Active,
                IssuedByUserId = actor.Id,
                IssuedAtUtc = DateTime.UtcNow,
                PublicNotes = application.GMResponse,
                IsPlayerVisible = true
            };
            _mongo.LegalEntityLicenses.InsertOne(issued);
            application.Status = LicenseApplicationStatusIds.Issued;
            _mongo.LegalLicenseApplications.ReplaceOne(x => x.Id == application.Id, application);
        }

        WriteAudit("legal", actor.Id, "application.review", application.Id);
        LegalPublish("legal.application.changed", application.CampaignId, application.Id, application.Title, application.IsPlayerVisible);
        return Ok("Legal application reviewed.", new Dictionary<string, object>
        {
            ["item"] = LegalApplicationPayload(application, includeAdmin: true),
            ["issuedLicense"] = issued == null ? null! : EntityLicensePayload(issued, includeAdmin: true)
        });
    }

    public ResponseEnvelope LegalCheckRun(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLegalActionChecks))) return LegalDisabled(context.Request.Command);
        var request = BuildLegalCheckRequest(context, actor, adminContext: true);
        var result = EvaluateLegalCheck(request, includeAdmin: true, actor.Id);
        return Ok("Legal check completed.", new Dictionary<string, object> { ["result"] = LegalCheckResultPayload(result, includeAdmin: true) });
    }

    public ResponseEnvelope LegalProductionModeSet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseWhiteGrayShadowProduction))) return LegalDisabled(context.Request.Command);
        var mode = NormalizeProductionMode(LegalString(context, "productionMode", 0, 64));
        var reason = LegalString(context, "reason", 0, 2048);
        if ((mode == ProductionLegalityModeIds.Gray || mode == ProductionLegalityModeIds.Shadow) && string.IsNullOrWhiteSpace(reason))
            return Error("Для серого или теневого режима нужно указать причину/обоснование.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var sourceType = FirstNonEmpty(LegalString(context, "sourceEntityType", 0, 64), "factory_order");
        var sourceId = LegalRequired(context, "sourceEntityId", 1, 128);
        var campaignId = LegalCampaignId(context);
        var filter = Builders<ProductionLegalityState>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<ProductionLegalityState>.Filter.Eq(x => x.SourceEntityType, sourceType)
            & Builders<ProductionLegalityState>.Filter.Eq(x => x.SourceEntityId, sourceId);
        var item = _mongo.LegalProductionLegalityStates.Find(filter).FirstOrDefault() ?? new ProductionLegalityState
        {
            CampaignId = campaignId,
            SourceEntityType = sourceType,
            SourceEntityId = sourceId
        };
        item.JurisdictionId = LegalStringOrExisting(context, "jurisdictionId", item.JurisdictionId, 128);
        item.ProductionMode = mode;
        item.LegalStatus = mode == ProductionLegalityModeIds.White ? LegalStatusIds.Legal : LegalStatusIds.GMReviewRequired;
        item.PublicSummary = mode == ProductionLegalityModeIds.White
            ? "Белый режим производства."
            : "Производство требует проверки GM.";
        item.GMNotes = FirstNonEmpty(reason, item.GMNotes);
        item.ApprovedByUserId = actor.Id;
        item.ApprovedAtUtc = DateTime.UtcNow;
        item.IsPlayerVisible = mode != ProductionLegalityModeIds.Shadow && (!context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"));
        item.UpdatedAtUtc = DateTime.UtcNow;
        if (_mongo.LegalProductionLegalityStates.Find(x => x.Id == item.Id).Any())
            _mongo.LegalProductionLegalityStates.ReplaceOne(x => x.Id == item.Id, item);
        else _mongo.LegalProductionLegalityStates.InsertOne(item);

        WriteAudit("legal", actor.Id, "productionMode.set", item.Id);
        LegalPublish("legal.productionMode.changed", item.CampaignId, item.Id, item.ProductionMode, item.IsPlayerVisible);
        return Ok("Production legality mode set.", new Dictionary<string, object> { ["item"] = ProductionLegalityPayload(item, includeAdmin: true) });
    }

    public ResponseEnvelope LegalPlayerSummary(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!LegalPlayerEnabled()) return LegalDisabled(context.Request.Command);
        var licenses = PlayerVisibleLicenses(actor).Take(20).Select(x => (object)EntityLicensePayload(x, includeAdmin: false)).ToArray();
        var applications = PlayerVisibleApplications(actor).Take(20).Select(x => (object)LegalApplicationPayload(x, includeAdmin: false)).ToArray();
        var checks = _mongo.LegalCheckRecords.Find(Builders<LegalCheckRecordState>.Filter.Eq(x => x.ActorUserId, actor.Id)).ToList()
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.CheckedAtUtc)
            .Take(20)
            .Select(x => (object)LegalCheckRecordPayload(x, includeAdmin: false))
            .ToArray();
        return Ok("Player legal summary loaded.", new Dictionary<string, object>
        {
            ["isEnabled"] = true,
            ["licenses"] = licenses,
            ["applications"] = applications,
            ["checks"] = checks,
            ["warnings"] = BuildPlayerLegalWarnings(licenses.Length, applications.Length, checks.Length),
            ["builtAtUtc"] = DateTime.UtcNow
        });
    }

    public ResponseEnvelope LegalPlayerLicenseList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!LegalPlayerEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseEntityLicenses))) return LegalDisabled(context.Request.Command);
        return Ok("Player licenses loaded.", new Dictionary<string, object> { ["items"] = PlayerVisibleLicenses(actor).Select(x => (object)EntityLicensePayload(x, includeAdmin: false)).ToArray() });
    }

    public ResponseEnvelope LegalPlayerApplicationList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!LegalPlayerEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLicenseApplications))) return LegalDisabled(context.Request.Command);
        return Ok("Player legal applications loaded.", new Dictionary<string, object> { ["items"] = PlayerVisibleApplications(actor).Select(x => (object)LegalApplicationPayload(x, includeAdmin: false)).ToArray() });
    }

    public ResponseEnvelope LegalPlayerApplicationSubmit(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!LegalPlayerEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLicenseApplications))) return LegalDisabled(context.Request.Command);
        var definition = RequireLegalDocument(_mongo.LegalLicenseDefinitions, LegalRequired(context, "licenseDefinitionId", 1, 128), "license definition");
        if (!definition.IsPlayerVisible) throw new UnauthorizedAccessException("License definition is not visible to player.");

        var now = DateTime.UtcNow;
        var application = new LicenseApplicationState
        {
            CampaignId = LegalCampaignId(context),
            LicenseDefinitionId = definition.Id,
            JurisdictionId = FirstNonEmpty(LegalString(context, "jurisdictionId", 0, 128), definition.JurisdictionId),
            ApplicantUserId = actor.Id,
            ApplicantEntityType = FirstNonEmpty(LegalString(context, "applicantEntityType", 0, 64), "character"),
            ApplicantEntityId = LegalString(context, "applicantEntityId", 0, 128),
            Title = FirstNonEmpty(LegalString(context, "title", 0, 180), $"Заявка на лицензию: {definition.Name}"),
            Reason = LegalString(context, "reason", 0, 4096),
            Status = LicenseApplicationStatusIds.Submitted,
            IsPlayerVisible = true,
            SubmittedAtUtc = now,
            UpdatedAtUtc = now
        };
        _mongo.LegalLicenseApplications.InsertOne(application);

        if (LegalFlag(nameof(LegalFeatureFlags.UseLegalRequestIntegration)) && PlayerRequestsBaseEnabled())
        {
            var request = new PlayerRequestState
            {
                RequestNumber = NextPlayerRequestNumber(),
                CampaignId = application.CampaignId,
                CreatedByUserId = actor.Id,
                CreatedByDisplayName = FirstNonEmpty(actor.Login, actor.Id),
                CharacterId = application.ApplicantEntityType == "character" ? application.ApplicantEntityId : string.Empty,
                RequestType = "license_application",
                Title = application.Title,
                Description = application.Reason,
                Status = PlayerRequestStatusIds.Submitted,
                Priority = PlayerRequestPriorityIds.Normal,
                IsPlayerVisible = true,
                LinkedEntityType = "license_application",
                LinkedEntityId = application.Id,
                ProposalType = "license_application",
                ProposalPayloadSummary = definition.Name,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                SubmittedAtUtc = now
            };
            _repositories.PlayerRequests.Insert(request);
            application.LinkedRequestId = request.Id;
            _mongo.LegalLicenseApplications.ReplaceOne(x => x.Id == application.Id, application);
        }

        WriteAudit("legal", actor.Id, "application.submit", application.Id);
        LegalPublish("legal.license.application.submitted", application.CampaignId, application.Id, application.Title, true);
        return Ok("Legal application submitted.", new Dictionary<string, object> { ["item"] = LegalApplicationPayload(application, includeAdmin: false) });
    }

    public ResponseEnvelope LegalPlayerCheckRequest(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!LegalPlayerEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseLegalActionChecks))) return LegalDisabled(context.Request.Command);
        var request = BuildLegalCheckRequest(context, actor, adminContext: false);
        var result = EvaluateLegalCheck(request, includeAdmin: false, actor.Id);
        return Ok("Player legal check completed.", new Dictionary<string, object> { ["result"] = LegalCheckResultPayload(result, includeAdmin: false) });
    }

    private ResponseEnvelope LegalEntityLicenseSetStatus(CommandContext context, string status, string auditAction)
    {
        var actor = RequireAdmin(context);
        if (!LegalAdminEnabled() || !LegalFlag(nameof(LegalFeatureFlags.UseEntityLicenses))) return LegalDisabled(context.Request.Command);
        var item = RequireLegalDocument(_mongo.LegalEntityLicenses, LegalRequired(context, "entityLicenseId", 1, 128), "entity license");
        item.Status = status;
        item.GMNotes = LegalStringOrExisting(context, "gmNotes", item.GMNotes, 4096);
        item.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.LegalEntityLicenses.ReplaceOne(x => x.Id == item.Id, item);
        WriteAudit("legal", actor.Id, auditAction, item.Id);
        LegalPublish("legal.entityLicense.changed", item.CampaignId, item.Id, item.DisplayName, item.IsPlayerVisible);
        return Ok("Entity license status updated.", new Dictionary<string, object> { ["item"] = EntityLicensePayload(item, includeAdmin: true) });
    }

    private LegalCheckResult EvaluateLegalCheck(LegalCheckRequest request, bool includeAdmin, string checkedByUserId)
    {
        var jurisdictionId = ResolveLegalJurisdiction(request);
        var profile = _mongo.LegalProfiles.Find(Builders<LegalProfileState>.Filter.Eq(x => x.CampaignId, request.CampaignId)
            & Builders<LegalProfileState>.Filter.Eq(x => x.JurisdictionId, jurisdictionId)
            & Builders<LegalProfileState>.Filter.Eq(x => x.IsActive, true)
            & Builders<LegalProfileState>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();

        var rules = _mongo.LegalRules.Find(Builders<LegalRuleDefinition>.Filter.Eq(x => x.CampaignId, request.CampaignId)
            & Builders<LegalRuleDefinition>.Filter.Eq(x => x.IsArchived, false)).ToList()
            .Where(rule => LegalRuleApplies(rule, request, jurisdictionId, profile?.Id ?? string.Empty))
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(LegalRestrictionRank)
            .ToArray();

        var rule = rules.FirstOrDefault();
        var result = new LegalCheckResult
        {
            CampaignId = request.CampaignId,
            JurisdictionId = jurisdictionId,
            LegalProfileId = profile?.Id ?? string.Empty,
            ActionType = request.ActionType,
            ObjectType = request.ObjectType,
            ObjectCategory = request.ObjectCategory
        };

        if (profile == null)
        {
            result.LegalStatus = LegalStatusIds.Unknown;
            result.DeJureStatus = LegalStatusIds.Unknown;
            result.DeFactoStatus = LegalStatusIds.Unknown;
            result.RequiresGMReview = true;
            result.CanProceedWithWarning = true;
            result.RiskLevel = LegalRiskLevelIds.Unknown;
            result.PlayerSafeMessage = "Законность неизвестна. Требуется проверка GM.";
            result.AdminSummary = "Нет активного legal profile для юрисдикции.";
            result.Warnings.Add("no_active_legal_profile");
        }
        else if (rule == null)
        {
            result.LegalStatus = LegalStatusIds.Unknown;
            result.DeJureStatus = LegalStatusIds.Unknown;
            result.DeFactoStatus = LegalStatusIds.Unknown;
            result.RequiresGMReview = true;
            result.CanProceedWithWarning = true;
            result.RiskLevel = LegalRiskLevelIds.Medium;
            result.PlayerSafeMessage = "Законность неизвестна. Требуется проверка GM.";
            result.AdminSummary = "Не найдено подходящее правило. По умолчанию не считаем действие законным.";
            result.Warnings.Add("no_matching_rule");
        }
        else
        {
            result.MatchedRuleId = rule.Id;
            result.LegalStatus = rule.LegalStatus;
            result.DeJureStatus = rule.DeJureStatus;
            result.DeFactoStatus = rule.DeFactoStatus;
            result.IsBlocked = rule.IsBlocked || rule.LegalStatus == LegalStatusIds.Illegal;
            result.RequiresGMReview = rule.RequiresGMReview || rule.LegalStatus == LegalStatusIds.GMReviewRequired || rule.LegalStatus == LegalStatusIds.Unknown;
            result.RiskLevel = rule.RiskLevel;
            result.RequiredLicenseDefinitionId = rule.RequiredLicenseDefinitionId;
            result.HasRequiredLicense = string.IsNullOrWhiteSpace(rule.RequiredLicenseDefinitionId) || HasActiveLegalLicense(request, rule.RequiredLicenseDefinitionId, jurisdictionId);

            if (!result.HasRequiredLicense)
            {
                result.LegalStatus = LegalStatusIds.LicenseRequired;
                result.IsBlocked = true;
                result.CanProceedWithWarning = false;
                result.RequiresGMReview = true;
                result.Warnings.Add("missing_required_license");
            }
            else
            {
                result.CanProceedWithWarning = !result.IsBlocked && (result.RequiresGMReview || result.LegalStatus == LegalStatusIds.Restricted);
            }

            result.PlayerSafeMessage = BuildPlayerLegalMessage(rule, result);
            result.AdminSummary = BuildAdminLegalSummary(rule, result);
        }

        var record = LegalCheckRecordFromRequest(request, result, includeAdmin, checkedByUserId);
        _mongo.LegalCheckRecords.InsertOne(record);
        WriteAudit("legal", checkedByUserId, "check.run", record.Id);
        LegalPublish(result.IsBlocked ? "legal.check.blocked" : "legal.check.ran", request.CampaignId, record.Id, result.LegalStatus, record.IsPlayerVisible);
        return result;
    }

    private LegalCheckRequest BuildLegalCheckRequest(CommandContext context, UserAccount actor, bool adminContext)
    {
        var actorEntityId = LegalString(context, "actorEntityId", 0, 128);
        var actorEntityType = LegalString(context, "actorEntityType", 0, 64);
        return new LegalCheckRequest
        {
            CampaignId = LegalCampaignId(context),
            JurisdictionId = LegalString(context, "jurisdictionId", 0, 128),
            ActorUserId = adminContext ? LegalStringOrDefault(context, "actorUserId", actor.Id, 128) : actor.Id,
            ActorEntityType = FirstNonEmpty(actorEntityType, "character"),
            ActorEntityId = actorEntityId,
            SubjectKind = FirstNonEmpty(LegalString(context, "subjectKind", 0, 64), LegalSubjectKindIds.Any),
            SubjectStatus = LegalString(context, "subjectStatus", 0, 64),
            ActionType = NormalizeLegalAction(LegalString(context, "actionType", 0, 64)),
            ObjectType = FirstNonEmpty(LegalString(context, "objectType", 0, 64), "item"),
            ObjectCategory = LegalString(context, "objectCategory", 0, 128),
            ObjectEntityId = LegalString(context, "objectEntityId", 0, 128),
            ObjectDisplayName = LegalString(context, "objectDisplayName", 0, 180),
            ProductionMode = NormalizeProductionMode(LegalString(context, "productionMode", 0, 64)),
            ObjectTags = LegalStringList(context.Request.Payload, "objectTags")
        };
    }

    private string ResolveLegalJurisdiction(LegalCheckRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.JurisdictionId)) return request.JurisdictionId;
        var fallback = _mongo.LegalJurisdictions.Find(Builders<JurisdictionDefinition>.Filter.Eq(x => x.CampaignId, request.CampaignId)
            & Builders<JurisdictionDefinition>.Filter.Eq(x => x.IsArchived, false)).ToList()
            .OrderBy(x => x.Name)
            .FirstOrDefault();
        return fallback?.Id ?? string.Empty;
    }

    private bool LegalRuleApplies(LegalRuleDefinition rule, LegalCheckRequest request, string jurisdictionId, string profileId)
    {
        if (!string.IsNullOrWhiteSpace(rule.JurisdictionId) && !string.Equals(rule.JurisdictionId, jurisdictionId, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(rule.LegalProfileId) && !string.Equals(rule.LegalProfileId, profileId, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(rule.ActionType, request.ActionType, StringComparison.OrdinalIgnoreCase) && !string.Equals(rule.ActionType, "any", StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(rule.SubjectKind) && !string.Equals(rule.SubjectKind, LegalSubjectKindIds.Any, StringComparison.OrdinalIgnoreCase) && !string.Equals(rule.SubjectKind, request.SubjectKind, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(rule.SubjectStatus) && !string.Equals(rule.SubjectStatus, request.SubjectStatus, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(rule.ObjectType) && !string.Equals(rule.ObjectType, "any", StringComparison.OrdinalIgnoreCase) && !string.Equals(rule.ObjectType, request.ObjectType, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(rule.ObjectCategory) && !string.Equals(rule.ObjectCategory, request.ObjectCategory, StringComparison.OrdinalIgnoreCase)) return false;
        if (rule.ObjectTags.Count > 0 && !rule.ObjectTags.Any(tag => request.ObjectTags.Any(x => string.Equals(x, tag, StringComparison.OrdinalIgnoreCase)))) return false;
        return true;
    }

    private bool HasActiveLegalLicense(LegalCheckRequest request, string licenseDefinitionId, string jurisdictionId)
    {
        var filter = Builders<EntityLicenseState>.Filter.Eq(x => x.LicenseDefinitionId, licenseDefinitionId)
            & Builders<EntityLicenseState>.Filter.Eq(x => x.Status, LicenseStatusIds.Active);
        if (!string.IsNullOrWhiteSpace(jurisdictionId)) filter &= Builders<EntityLicenseState>.Filter.Eq(x => x.JurisdictionId, jurisdictionId);
        var licenses = _mongo.LegalEntityLicenses.Find(filter).ToList();
        return licenses.Any(x =>
            (!string.IsNullOrWhiteSpace(request.ActorEntityId) && string.Equals(x.HolderEntityType, request.ActorEntityType, StringComparison.OrdinalIgnoreCase) && string.Equals(x.HolderEntityId, request.ActorEntityId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(request.ActorUserId) && string.Equals(x.HolderUserId, request.ActorUserId, StringComparison.OrdinalIgnoreCase)));
    }

    private LegalCheckRecordState LegalCheckRecordFromRequest(LegalCheckRequest request, LegalCheckResult result, bool includeAdmin, string checkedByUserId)
        => new()
        {
            CampaignId = request.CampaignId,
            JurisdictionId = result.JurisdictionId,
            ActorUserId = request.ActorUserId,
            ActorEntityType = request.ActorEntityType,
            ActorEntityId = request.ActorEntityId,
            ActionType = request.ActionType,
            ObjectType = request.ObjectType,
            ObjectCategory = request.ObjectCategory,
            ObjectEntityId = request.ObjectEntityId,
            ObjectDisplayName = request.ObjectDisplayName,
            LegalStatus = result.LegalStatus,
            DeJureStatus = result.DeJureStatus,
            DeFactoStatus = result.DeFactoStatus,
            IsBlocked = result.IsBlocked,
            CanProceedWithWarning = result.CanProceedWithWarning,
            RequiresGMReview = result.RequiresGMReview,
            RiskLevel = result.RiskLevel,
            RequiredLicenseDefinitionId = result.RequiredLicenseDefinitionId,
            MatchedRuleId = includeAdmin ? result.MatchedRuleId : string.Empty,
            PublicSummary = result.PlayerSafeMessage,
            AdminSummary = includeAdmin ? result.AdminSummary : string.Empty,
            IsPlayerVisible = true,
            CheckedByUserId = checkedByUserId,
            CheckedAtUtc = DateTime.UtcNow
        };

    private Dictionary<string, object> LegalMetric(string title, long value, string description)
        => new() { ["title"] = title, ["value"] = value, ["description"] = description };

    private Dictionary<string, object> JurisdictionPayload(JurisdictionDefinition item, bool includeAdmin)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = item.Id,
            ["campaignId"] = item.CampaignId,
            ["name"] = item.Name,
            ["jurisdictionType"] = item.JurisdictionType,
            ["parentJurisdictionId"] = includeAdmin ? item.ParentJurisdictionId : string.Empty,
            ["description"] = includeAdmin ? item.Description : item.PublicSummary,
            ["publicSummary"] = item.PublicSummary,
            ["isPlayerVisible"] = item.IsPlayerVisible,
            ["isArchived"] = item.IsArchived,
            ["updatedAtUtc"] = item.UpdatedAtUtc
        };
        if (includeAdmin)
        {
            result["linkedEntityType"] = item.LinkedEntityType;
            result["linkedEntityId"] = item.LinkedEntityId;
            result["visibilityMode"] = item.VisibilityMode;
            result["tags"] = item.Tags.ToArray();
        }
        return result;
    }

    private Dictionary<string, object> LegalProfilePayload(LegalProfileState item)
        => new()
        {
            ["id"] = item.Id,
            ["campaignId"] = item.CampaignId,
            ["jurisdictionId"] = item.JurisdictionId,
            ["name"] = item.Name,
            ["description"] = item.Description,
            ["isActive"] = item.IsActive,
            ["isArchived"] = item.IsArchived,
            ["updatedAtUtc"] = item.UpdatedAtUtc
        };

    private Dictionary<string, object> LegalRulePayload(LegalRuleDefinition item, bool includeAdmin)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = item.Id,
            ["campaignId"] = item.CampaignId,
            ["jurisdictionId"] = item.JurisdictionId,
            ["name"] = item.Name,
            ["actionType"] = item.ActionType,
            ["subjectKind"] = item.SubjectKind,
            ["objectType"] = item.ObjectType,
            ["objectCategory"] = item.ObjectCategory,
            ["legalStatus"] = item.LegalStatus,
            ["deJureStatus"] = item.DeJureStatus,
            ["deFactoStatus"] = item.DeFactoStatus,
            ["requiredLicenseDefinitionId"] = includeAdmin || item.IsPlayerVisible ? item.RequiredLicenseDefinitionId : string.Empty,
            ["requiresGMReview"] = item.RequiresGMReview,
            ["isBlocked"] = item.IsBlocked,
            ["riskLevel"] = item.IsPlayerVisible || includeAdmin ? item.RiskLevel : LegalRiskLevelIds.Unknown,
            ["priority"] = item.Priority,
            ["isPlayerVisible"] = item.IsPlayerVisible,
            ["publicWarning"] = item.PublicWarning,
            ["isArchived"] = item.IsArchived,
            ["updatedAtUtc"] = item.UpdatedAtUtc
        };
        if (includeAdmin)
        {
            result["legalProfileId"] = item.LegalProfileId;
            result["subjectStatus"] = item.SubjectStatus;
            result["requiredPermitType"] = item.RequiredPermitType;
            result["adminNotes"] = item.AdminNotes;
            result["objectTags"] = item.ObjectTags.ToArray();
        }
        return result;
    }

    private Dictionary<string, object> LicenseDefinitionPayload(LicenseDefinition item, bool includeAdmin)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = item.Id,
            ["campaignId"] = item.CampaignId,
            ["jurisdictionId"] = item.JurisdictionId,
            ["name"] = item.Name,
            ["licenseType"] = item.LicenseType,
            ["appliesToActionType"] = item.AppliesToActionType,
            ["appliesToObjectType"] = item.AppliesToObjectType,
            ["appliesToObjectCategory"] = item.AppliesToObjectCategory,
            ["publicSummary"] = item.PublicSummary,
            ["requiresGMApproval"] = item.RequiresGMApproval,
            ["isPlayerVisible"] = item.IsPlayerVisible,
            ["isArchived"] = item.IsArchived,
            ["updatedAtUtc"] = item.UpdatedAtUtc
        };
        if (includeAdmin) result["adminNotes"] = item.AdminNotes;
        return result;
    }

    private Dictionary<string, object> EntityLicensePayload(EntityLicenseState item, bool includeAdmin)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = item.Id,
            ["campaignId"] = item.CampaignId,
            ["licenseDefinitionId"] = item.LicenseDefinitionId,
            ["jurisdictionId"] = item.JurisdictionId,
            ["holderEntityType"] = item.HolderEntityType,
            ["holderEntityId"] = includeAdmin ? item.HolderEntityId : string.Empty,
            ["displayName"] = item.DisplayName,
            ["status"] = item.Status,
            ["issuedAtUtc"] = item.IssuedAtUtc,
            ["expiresAtUtc"] = item.ExpiresAtUtc,
            ["publicNotes"] = item.PublicNotes,
            ["isPlayerVisible"] = item.IsPlayerVisible,
            ["updatedAtUtc"] = item.UpdatedAtUtc
        };
        if (includeAdmin)
        {
            result["holderUserId"] = item.HolderUserId;
            result["issuedByUserId"] = item.IssuedByUserId;
            result["gmNotes"] = item.GMNotes;
        }
        return result;
    }

    private Dictionary<string, object> LegalApplicationPayload(LicenseApplicationState item, bool includeAdmin)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = item.Id,
            ["campaignId"] = item.CampaignId,
            ["licenseDefinitionId"] = item.LicenseDefinitionId,
            ["jurisdictionId"] = item.JurisdictionId,
            ["applicantEntityType"] = item.ApplicantEntityType,
            ["applicantEntityId"] = includeAdmin ? item.ApplicantEntityId : string.Empty,
            ["title"] = item.Title,
            ["reason"] = item.Reason,
            ["status"] = item.Status,
            ["linkedRequestId"] = item.LinkedRequestId,
            ["gmResponse"] = item.GMResponse,
            ["isPlayerVisible"] = item.IsPlayerVisible,
            ["submittedAtUtc"] = item.SubmittedAtUtc,
            ["updatedAtUtc"] = item.UpdatedAtUtc
        };
        if (includeAdmin)
        {
            result["applicantUserId"] = item.ApplicantUserId;
            result["gmNotes"] = item.GMNotes;
            result["reviewedByUserId"] = item.ReviewedByUserId;
            result["reviewedAtUtc"] = item.ReviewedAtUtc;
        }
        return result;
    }

    private Dictionary<string, object> LegalCheckResultPayload(LegalCheckResult item, bool includeAdmin)
    {
        var result = new Dictionary<string, object>
        {
            ["campaignId"] = item.CampaignId,
            ["jurisdictionId"] = item.JurisdictionId,
            ["actionType"] = item.ActionType,
            ["objectType"] = item.ObjectType,
            ["objectCategory"] = item.ObjectCategory,
            ["legalStatus"] = item.LegalStatus,
            ["deJureStatus"] = item.DeJureStatus,
            ["deFactoStatus"] = item.DeFactoStatus,
            ["isBlocked"] = item.IsBlocked,
            ["canProceedWithWarning"] = item.CanProceedWithWarning,
            ["requiresGMReview"] = item.RequiresGMReview,
            ["riskLevel"] = item.RiskLevel,
            ["requiredLicenseDefinitionId"] = item.RequiredLicenseDefinitionId,
            ["hasRequiredLicense"] = item.HasRequiredLicense,
            ["playerSafeMessage"] = item.PlayerSafeMessage,
            ["warnings"] = item.Warnings.ToArray(),
            ["builtAtUtc"] = item.BuiltAtUtc
        };
        if (includeAdmin)
        {
            result["legalProfileId"] = item.LegalProfileId;
            result["matchedRuleId"] = item.MatchedRuleId;
            result["adminSummary"] = item.AdminSummary;
        }
        return result;
    }

    private Dictionary<string, object> LegalCheckRecordPayload(LegalCheckRecordState item, bool includeAdmin)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = item.Id,
            ["campaignId"] = item.CampaignId,
            ["jurisdictionId"] = item.JurisdictionId,
            ["actionType"] = item.ActionType,
            ["objectType"] = item.ObjectType,
            ["objectCategory"] = item.ObjectCategory,
            ["objectDisplayName"] = item.ObjectDisplayName,
            ["legalStatus"] = item.LegalStatus,
            ["deJureStatus"] = item.DeJureStatus,
            ["deFactoStatus"] = item.DeFactoStatus,
            ["isBlocked"] = item.IsBlocked,
            ["canProceedWithWarning"] = item.CanProceedWithWarning,
            ["requiresGMReview"] = item.RequiresGMReview,
            ["riskLevel"] = item.RiskLevel,
            ["requiredLicenseDefinitionId"] = item.RequiredLicenseDefinitionId,
            ["publicSummary"] = item.PublicSummary,
            ["checkedAtUtc"] = item.CheckedAtUtc
        };
        if (includeAdmin)
        {
            result["actorUserId"] = item.ActorUserId;
            result["actorEntityType"] = item.ActorEntityType;
            result["actorEntityId"] = item.ActorEntityId;
            result["objectEntityId"] = item.ObjectEntityId;
            result["matchedRuleId"] = item.MatchedRuleId;
            result["adminSummary"] = item.AdminSummary;
            result["checkedByUserId"] = item.CheckedByUserId;
        }
        return result;
    }

    private Dictionary<string, object> ProductionLegalityPayload(ProductionLegalityState item, bool includeAdmin)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = item.Id,
            ["campaignId"] = item.CampaignId,
            ["sourceEntityType"] = item.SourceEntityType,
            ["sourceEntityId"] = includeAdmin ? item.SourceEntityId : string.Empty,
            ["jurisdictionId"] = item.JurisdictionId,
            ["productionMode"] = item.ProductionMode,
            ["legalStatus"] = item.LegalStatus,
            ["publicSummary"] = item.PublicSummary,
            ["isPlayerVisible"] = item.IsPlayerVisible,
            ["updatedAtUtc"] = item.UpdatedAtUtc
        };
        if (includeAdmin)
        {
            result["gmNotes"] = item.GMNotes;
            result["approvedByUserId"] = item.ApprovedByUserId;
            result["approvedAtUtc"] = item.ApprovedAtUtc;
        }
        return result;
    }

    private IReadOnlyCollection<EntityLicenseState> PlayerVisibleLicenses(UserAccount actor)
        => _mongo.LegalEntityLicenses.Find(Builders<EntityLicenseState>.Filter.Eq(x => x.HolderUserId, actor.Id)).ToList()
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToArray();

    private IReadOnlyCollection<LicenseApplicationState> PlayerVisibleApplications(UserAccount actor)
        => _mongo.LegalLicenseApplications.Find(Builders<LicenseApplicationState>.Filter.Eq(x => x.ApplicantUserId, actor.Id)).ToList()
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToArray();

    private object[] BuildPlayerLegalWarnings(int licenseCount, int applicationCount, int checkCount)
    {
        var warnings = new List<object>();
        if (licenseCount == 0) warnings.Add("Активных лицензий пока нет.");
        if (applicationCount > 0) warnings.Add("Есть заявки на лицензии в работе.");
        if (checkCount > 0) warnings.Add("Есть последние проверки законности.");
        return warnings.ToArray();
    }

    private void DeactivateLegalProfiles(string campaignId, string jurisdictionId, string actorId)
    {
        var profiles = _mongo.LegalProfiles.Find(Builders<LegalProfileState>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<LegalProfileState>.Filter.Eq(x => x.JurisdictionId, jurisdictionId)
            & Builders<LegalProfileState>.Filter.Eq(x => x.IsActive, true)).ToList();
        foreach (var profile in profiles)
        {
            profile.IsActive = false;
            profile.UpdatedByUserId = actorId;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            _mongo.LegalProfiles.ReplaceOne(x => x.Id == profile.Id, profile);
        }
    }

    private static int LegalRestrictionRank(LegalRuleDefinition rule)
        => rule.LegalStatus switch
        {
            LegalStatusIds.Illegal => 100,
            LegalStatusIds.LicenseRequired => 90,
            LegalStatusIds.PermitRequired => 80,
            LegalStatusIds.Restricted => 70,
            LegalStatusIds.GMReviewRequired => 60,
            LegalStatusIds.Unknown => 50,
            LegalStatusIds.Legal => 10,
            _ => 0
        };

    private static string BuildPlayerLegalMessage(LegalRuleDefinition rule, LegalCheckResult result)
    {
        if (!result.HasRequiredLicense && !string.IsNullOrWhiteSpace(result.RequiredLicenseDefinitionId)) return "Требуется лицензия.";
        if (result.IsBlocked) return string.IsNullOrWhiteSpace(rule.PublicWarning) ? "Действие заблокировано законом." : rule.PublicWarning;
        if (result.RequiresGMReview) return "Требуется проверка GM.";
        if (result.LegalStatus == LegalStatusIds.Restricted) return string.IsNullOrWhiteSpace(rule.PublicWarning) ? "Есть ограничения в этой юрисдикции." : rule.PublicWarning;
        if (result.LegalStatus == LegalStatusIds.Legal) return "Действие выглядит законным.";
        return "Законность неизвестна. Требуется проверка GM.";
    }

    private static string BuildAdminLegalSummary(LegalRuleDefinition rule, LegalCheckResult result)
        => $"Rule: {rule.Name}; status={result.LegalStatus}; deJure={result.DeJureStatus}; deFacto={result.DeFactoStatus}; risk={result.RiskLevel}";

    private bool LegalFlag(string flagName) => _featureFlags.IsEnabled(flagName);
    private bool LegalBaseEnabled() => LegalFlag(nameof(LegalFeatureFlags.UseLegalMvp));
    private bool LegalAdminEnabled() => LegalBaseEnabled() && LegalFlag(nameof(LegalFeatureFlags.UseLegalAdminView));
    private bool LegalPlayerEnabled() => LegalBaseEnabled() && LegalFlag(nameof(LegalFeatureFlags.UseLegalPlayerView));

    private ResponseEnvelope LegalDisabled(string command)
        => Ok("Legal system is disabled by feature flags.", new Dictionary<string, object>
        {
            ["isEnabled"] = false,
            ["command"] = command,
            ["message"] = "Законы и лицензии выключены feature flags.",
            ["warnings"] = new object[] { "Включите LegalFeatureFlags для работы модуля законов." },
            ["builtAtUtc"] = DateTime.UtcNow
        });

    private string LegalCampaignId(CommandContext context) => FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "campaignId"), "default");
    private string LegalString(CommandContext context, string key, int min, int max) => RequireLength(PayloadReader.GetString(context.Request.Payload, key), min, max, key);
    private string LegalRequired(CommandContext context, string key, int min, int max) => RequireLength(PayloadReader.GetString(context.Request.Payload, key), min, max, key);
    private string LegalStringOrExisting(CommandContext context, string key, string existing, int max) => context.Request.Payload.ContainsKey(key) ? RequireLength(PayloadReader.GetString(context.Request.Payload, key), 0, max, key) : existing;
    private string LegalStringOrDefault(CommandContext context, string key, string fallback, int max) => context.Request.Payload.ContainsKey(key) ? RequireLength(PayloadReader.GetString(context.Request.Payload, key), 0, max, key) : fallback;

    private static List<string> LegalStringList(IDictionary<string, object> payload, string key)
    {
        var list = PayloadReader.GetList(payload, key);
        if (list == null) return new List<string>();
        return list.Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizeLegalAction(string value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? LegalActionTypeIds.Own : value.Trim().ToLowerInvariant();
        var allowed = new[]
        {
            LegalActionTypeIds.Research, LegalActionTypeIds.Design, LegalActionTypeIds.Craft, LegalActionTypeIds.Manufacture,
            LegalActionTypeIds.Buy, LegalActionTypeIds.Sell, LegalActionTypeIds.Own, LegalActionTypeIds.CarryPublic,
            LegalActionTypeIds.Store, LegalActionTypeIds.Transport, LegalActionTypeIds.Operate, LegalActionTypeIds.Use,
            LegalActionTypeIds.Import, LegalActionTypeIds.Export, LegalActionTypeIds.Transfer, LegalActionTypeIds.FactoryOrder
        };
        return allowed.Contains(candidate, StringComparer.OrdinalIgnoreCase) ? candidate : LegalActionTypeIds.Own;
    }

    private static string NormalizeLegalStatus(string value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? LegalStatusIds.Unknown : value.Trim().ToLowerInvariant();
        var allowed = new[] { LegalStatusIds.Legal, LegalStatusIds.Restricted, LegalStatusIds.LicenseRequired, LegalStatusIds.PermitRequired, LegalStatusIds.GMReviewRequired, LegalStatusIds.Illegal, LegalStatusIds.Unknown };
        return allowed.Contains(candidate, StringComparer.OrdinalIgnoreCase) ? candidate : LegalStatusIds.Unknown;
    }

    private static string NormalizeLegalRisk(string value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? LegalRiskLevelIds.None : value.Trim().ToLowerInvariant();
        var allowed = new[] { LegalRiskLevelIds.None, LegalRiskLevelIds.Low, LegalRiskLevelIds.Medium, LegalRiskLevelIds.High, LegalRiskLevelIds.Severe, LegalRiskLevelIds.Unknown };
        return allowed.Contains(candidate, StringComparer.OrdinalIgnoreCase) ? candidate : LegalRiskLevelIds.Unknown;
    }

    private static string NormalizeProductionMode(string value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? ProductionLegalityModeIds.White : value.Trim().ToLowerInvariant();
        var allowed = new[] { ProductionLegalityModeIds.White, ProductionLegalityModeIds.Gray, ProductionLegalityModeIds.Shadow };
        return allowed.Contains(candidate, StringComparer.OrdinalIgnoreCase) ? candidate : ProductionLegalityModeIds.White;
    }

    private T RequireLegalDocument<T>(IMongoCollection<T> collection, string id, string label) where T : EntityBase
    {
        var item = collection.Find(Builders<T>.Filter.Eq(x => x.Id, id)).FirstOrDefault();
        if (item == null) throw new KeyNotFoundException($"{label} not found.");
        return item;
    }

    private void LegalPublish(string eventType, string campaignId, string entityId, string summary, bool playerVisible)
    {
        if (LegalFlag(nameof(LegalFeatureFlags.UseLegalSyncEvents)))
        {
            _syncEvents.Publish(eventType, SyncScopes.Global, "legal", entityId, "changed", string.Empty, new Dictionary<string, object>
            {
                ["campaignId"] = campaignId,
                ["summary"] = summary,
                ["isPlayerVisible"] = playerVisible
            }, string.Empty);
        }
        if (LegalFlag(nameof(LegalFeatureFlags.UseLegalJournalIntegration)) &&
            _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp)) &&
            _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion)))
        {
            try
            {
                var entry = new EventJournalEntryState
                {
                    CampaignId = campaignId,
                    Category = "legal",
                    SourceModule = "legal",
                    SourceEventType = eventType,
                    SourceEventId = entityId,
                    Title = summary,
                    Summary = summary,
                    PlayerSummary = playerVisible ? summary : "Юридическое событие скрыто GM.",
                    IsPlayerVisible = playerVisible,
                    VisibilityMode = playerVisible ? "player_visible" : "gm_only",
                    IsAutomatic = true,
                    OccurredAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _repositories.EventJournalEntries.Insert(entry);
            }
            catch (Exception ex)
            {
                _logger.Admin($"legal.journal.hook.failed event={eventType} error={ex.Message}");
            }
        }
    }
}
