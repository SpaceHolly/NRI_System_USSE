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
    public ResponseEnvelope ManufacturingProjectList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!ManufacturingAdminEnabled()) return ManufacturingDisabled(context.Request.Command);
        }
        else if (!ManufacturingPlayerEnabled())
        {
            return ManufacturingDisabled(context.Request.Command);
        }

        var filter = ManufacturingCampaignFilter<ManufacturingProjectState>(context.Request.Payload);
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<ManufacturingProjectState>.Filter.Eq(x => x.IsArchived, false);
        if (!IsAdmin(actor)) filter &= PlayerManufacturingProjectFilter(actor);
        var items = _repositories.ManufacturingProjects.Find(filter)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(300)
            .Select(x => (object)ManufacturingProjectPayload(x, IsAdmin(actor), includeDetails: false))
            .ToArray();
        return Ok("Manufacturing projects loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ManufacturingPlayerProjectList(CommandContext context) => ManufacturingProjectList(context);

    public ResponseEnvelope ManufacturingProjectGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!ManufacturingAdminEnabled()) return ManufacturingDisabled(context.Request.Command);
        }
        else if (!ManufacturingPlayerEnabled())
        {
            return ManufacturingDisabled(context.Request.Command);
        }

        var project = RequireManufacturingProject(context);
        if (!IsAdmin(actor) && !CanPlayerSeeManufacturingProject(project, actor)) throw new UnauthorizedAccessException("Manufacturing project is hidden.");
        return Ok("Manufacturing project loaded.", new Dictionary<string, object> { ["item"] = ManufacturingProjectPayload(project, IsAdmin(actor), includeDetails: true) });
    }

    public ResponseEnvelope ManufacturingPlayerProjectGet(CommandContext context) => ManufacturingProjectGet(context);

    public ResponseEnvelope ManufacturingProjectCreateFromOrder(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingFactoryOrderIntegration))) return ManufacturingDisabled(context.Request.Command);
        var orderId = PayloadReader.GetString(context.Request.Payload, "orderId") ?? string.Empty;
        var order = _repositories.FactoryOrders.GetById(orderId) ?? throw new InvalidOperationException("Factory order not found.");
        var existing = _repositories.ManufacturingProjects.Find(Builders<ManufacturingProjectState>.Filter.Eq(x => x.FactoryOrderId, order.Id)).FirstOrDefault();
        if (existing != null) return Ok("Manufacturing project already exists for this order.", new Dictionary<string, object> { ["item"] = ManufacturingProjectPayload(existing, true, true) });

        var project = BuildManufacturingProjectFromOrder(order, actor, context.Request.Payload);
        _repositories.ManufacturingProjects.Insert(project);
        CreateDefaultManufacturingStages(project, actor.Id);
        order.Status = FactoryOrderStatusIds.WaitingManufacturing;
        order.UpdatedAtUtc = DateTime.UtcNow;
        order.UpdatedByUserId = actor.Id;
        _repositories.FactoryOrders.Replace(order);
        EnsureManufacturingProjectBase(project, actor);
        TryWriteManufacturingJournal(project, "manufacturing.project.created", "Создан производственный проект", actor.Id, project.IsPlayerVisible);
        TryPublishManufacturingSync("manufacturing.project.changed", project, "create", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Manufacturing project created from factory order.", new Dictionary<string, object> { ["item"] = ManufacturingProjectPayload(project, true, true) });
    }

    public ResponseEnvelope ManufacturingProjectCreateManual(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingProjects))) return ManufacturingDisabled(context.Request.Command);
        var project = BuildManualManufacturingProject(context.Request.Payload, actor);
        _repositories.ManufacturingProjects.Insert(project);
        CreateDefaultManufacturingStages(project, actor.Id);
        EnsureManufacturingProjectBase(project, actor);
        TryWriteManufacturingJournal(project, "manufacturing.project.created", "Создан производственный проект", actor.Id, project.IsPlayerVisible);
        return Ok("Manufacturing project created.", new Dictionary<string, object> { ["item"] = ManufacturingProjectPayload(project, true, true) });
    }

    public ResponseEnvelope ManufacturingProjectStart(CommandContext context) => SetManufacturingStatus(context, ManufacturingStatusIds.Active, "Manufacturing project started.");
    public ResponseEnvelope ManufacturingProjectPause(CommandContext context) => SetManufacturingStatus(context, ManufacturingStatusIds.Paused, "Manufacturing project paused.");
    public ResponseEnvelope ManufacturingProjectResume(CommandContext context) => SetManufacturingStatus(context, ManufacturingStatusIds.Active, "Manufacturing project resumed.");
    public ResponseEnvelope ManufacturingProjectCancel(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled()) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        project.ManufacturingStatus = ManufacturingStatusIds.Cancelled;
        project.UpdatedAtUtc = DateTime.UtcNow;
        project.UpdatedByUserId = actor.Id;
        _repositories.ManufacturingProjects.Replace(project);
        ReleaseOpenReservations(project, actor.Id);
        TryWriteManufacturingJournal(project, "manufacturing.project.cancelled", "Производственный проект отменён", actor.Id, project.IsPlayerVisible);
        return Ok("Manufacturing project cancelled. Unconsumed reservations were released.", new Dictionary<string, object> { ["item"] = ManufacturingProjectPayload(project, true, true) });
    }

    public ResponseEnvelope ManufacturingProjectComplete(CommandContext context)
        => SetManufacturingStatus(context, ManufacturingStatusIds.Completed, "Manufacturing project completed.");

    public ResponseEnvelope ManufacturingStageAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingStages))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        var stage = BuildManufacturingStage(project, context.Request.Payload, actor.Id);
        _repositories.ManufacturingStages.Insert(stage);
        RecalculateManufacturingProject(project, actor.Id);
        return Ok("Manufacturing stage added.", new Dictionary<string, object> { ["item"] = StagePayload(stage, true) });
    }

    public ResponseEnvelope ManufacturingStageUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingStages))) return ManufacturingDisabled(context.Request.Command);
        var stage = RequireManufacturingStage(context);
        UpdateManufacturingStage(stage, context.Request.Payload, actor.Id);
        _repositories.ManufacturingStages.Replace(stage);
        var project = _repositories.ManufacturingProjects.GetById(stage.ManufacturingProjectId);
        if (project != null) RecalculateManufacturingProject(project, actor.Id);
        return Ok("Manufacturing stage updated.", new Dictionary<string, object> { ["item"] = StagePayload(stage, true) });
    }

    public ResponseEnvelope ManufacturingStageStart(CommandContext context) => SetStageStatus(context, ManufacturingStageStatusIds.Active, "Manufacturing stage started.");
    public ResponseEnvelope ManufacturingStageComplete(CommandContext context) => SetStageStatus(context, ManufacturingStageStatusIds.Completed, "Manufacturing stage completed.");

    public ResponseEnvelope ManufacturingResourcePlanAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingResourcePlan))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        var plan = new ManufacturingResourcePlanState
        {
            CampaignId = project.CampaignId,
            ManufacturingProjectId = project.Id,
            StageId = PayloadReader.GetString(context.Request.Payload, "stageId") ?? string.Empty,
            ResourceType = PayloadReader.GetString(context.Request.Payload, "resourceType") ?? "material",
            ResourceId = PayloadReader.GetString(context.Request.Payload, "resourceId") ?? string.Empty,
            ResourceName = RequiredManufacturingText(context.Request.Payload, "resourceName", "Resource name required."),
            RequiredQuantity = PositiveManufacturingDecimal(context.Request.Payload, "requiredQuantity", 1m),
            Unit = PayloadReader.GetString(context.Request.Payload, "unit") ?? "pcs",
            SourceType = PayloadReader.GetString(context.Request.Payload, "sourceType") ?? string.Empty,
            SourceId = PayloadReader.GetString(context.Request.Payload, "sourceId") ?? string.Empty,
            PublicSummary = PayloadReader.GetString(context.Request.Payload, "publicSummary") ?? string.Empty,
            GMNotes = PayloadReader.GetString(context.Request.Payload, "gmNotes") ?? string.Empty,
            IsPlayerVisible = !context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            UpdatedByUserId = actor.Id
        };
        _repositories.ManufacturingResourcePlans.Insert(plan);
        RecalculateManufacturingProject(project, actor.Id);
        return Ok("Manufacturing resource plan added.", new Dictionary<string, object> { ["item"] = ResourcePlanPayload(plan, true) });
    }

    public ResponseEnvelope ManufacturingResourceReserve(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingResourceReservation))) return ManufacturingDisabled(context.Request.Command);
        var plan = RequireResourcePlan(context);
        var quantity = PositiveManufacturingDecimal(context.Request.Payload, "quantity", Math.Max(0.01m, plan.RequiredQuantity - plan.ReservedQuantity));
        if (plan.ReservedQuantity + quantity > plan.RequiredQuantity) throw new InvalidOperationException("Reserved quantity cannot exceed required quantity.");
        var reservation = new ManufacturingResourceReservationState
        {
            ManufacturingProjectId = plan.ManufacturingProjectId,
            ResourcePlanId = plan.Id,
            CampaignId = plan.CampaignId,
            ReservedQuantity = quantity,
            Unit = plan.Unit,
            InventoryItemId = PayloadReader.GetString(context.Request.Payload, "inventoryItemId") ?? string.Empty,
            SourceEntityType = PayloadReader.GetString(context.Request.Payload, "sourceEntityType") ?? plan.SourceType,
            SourceEntityId = PayloadReader.GetString(context.Request.Payload, "sourceEntityId") ?? plan.SourceId,
            IsPlayerVisible = plan.IsPlayerVisible,
            UpdatedByUserId = actor.Id
        };
        _repositories.ManufacturingResourceReservations.Insert(reservation);
        plan.ReservedQuantity += quantity;
        plan.Status = plan.ReservedQuantity >= plan.RequiredQuantity ? ManufacturingResourceStatusIds.Reserved : ManufacturingResourceStatusIds.PartiallyReserved;
        plan.UpdatedAtUtc = DateTime.UtcNow;
        plan.UpdatedByUserId = actor.Id;
        _repositories.ManufacturingResourcePlans.Replace(plan);
        RecalculateManufacturingProject(_repositories.ManufacturingProjects.GetById(plan.ManufacturingProjectId), actor.Id);
        return Ok("Manufacturing resources reserved.", new Dictionary<string, object> { ["item"] = ReservationPayload(reservation, true), ["resourcePlan"] = ResourcePlanPayload(plan, true) });
    }

    public ResponseEnvelope ManufacturingResourceRelease(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingResourceReservation))) return ManufacturingDisabled(context.Request.Command);
        var reservation = RequireReservation(context);
        if (reservation.ConsumedQuantity > 0) throw new InvalidOperationException("Consumed resources cannot be released automatically.");
        reservation.Status = ManufacturingReservationStatusIds.Released;
        reservation.UpdatedAtUtc = DateTime.UtcNow;
        reservation.UpdatedByUserId = actor.Id;
        _repositories.ManufacturingResourceReservations.Replace(reservation);
        var plan = _repositories.ManufacturingResourcePlans.GetById(reservation.ResourcePlanId);
        if (plan != null)
        {
            plan.ReservedQuantity = Math.Max(0, plan.ReservedQuantity - reservation.ReservedQuantity);
            plan.Status = plan.ReservedQuantity <= 0 ? ManufacturingResourceStatusIds.Planned : ManufacturingResourceStatusIds.PartiallyReserved;
            plan.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ManufacturingResourcePlans.Replace(plan);
            RecalculateManufacturingProject(_repositories.ManufacturingProjects.GetById(plan.ManufacturingProjectId), actor.Id);
        }
        return Ok("Manufacturing reservation released.", new Dictionary<string, object> { ["item"] = ReservationPayload(reservation, true) });
    }

    public ResponseEnvelope ManufacturingResourceConsume(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingResourceConsumption))) return ManufacturingDisabled(context.Request.Command);
        var reservation = RequireReservation(context);
        if (reservation.Status == ManufacturingReservationStatusIds.Released || reservation.Status == ManufacturingReservationStatusIds.Cancelled) throw new InvalidOperationException("Reservation is not consumable.");
        var quantity = PositiveManufacturingDecimal(context.Request.Payload, "quantity", reservation.ReservedQuantity - reservation.ConsumedQuantity);
        if (reservation.ConsumedQuantity + quantity > reservation.ReservedQuantity) throw new InvalidOperationException("Consumed quantity cannot exceed reserved quantity.");
        reservation.ConsumedQuantity += quantity;
        reservation.Status = reservation.ConsumedQuantity >= reservation.ReservedQuantity ? ManufacturingReservationStatusIds.Consumed : ManufacturingReservationStatusIds.PartiallyConsumed;
        reservation.UpdatedAtUtc = DateTime.UtcNow;
        reservation.UpdatedByUserId = actor.Id;
        _repositories.ManufacturingResourceReservations.Replace(reservation);
        var plan = _repositories.ManufacturingResourcePlans.GetById(reservation.ResourcePlanId);
        if (plan != null)
        {
            plan.ConsumedQuantity += quantity;
            plan.Status = plan.ConsumedQuantity >= plan.RequiredQuantity ? ManufacturingResourceStatusIds.Consumed : ManufacturingResourceStatusIds.PartiallyConsumed;
            plan.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ManufacturingResourcePlans.Replace(plan);
            RecalculateManufacturingProject(_repositories.ManufacturingProjects.GetById(plan.ManufacturingProjectId), actor.Id);
        }
        return Ok("Manufacturing resources consumed. Consumption is manual and not auto-restored.", new Dictionary<string, object> { ["item"] = ReservationPayload(reservation, true) });
    }

    public ResponseEnvelope ManufacturingCostAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingCostTracking))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        var item = new ManufacturingCostLedgerEntry
        {
            CampaignId = project.CampaignId,
            ManufacturingProjectId = project.Id,
            CostType = PayloadReader.GetString(context.Request.Payload, "costType") ?? "manual",
            Amount = PositiveManufacturingDecimal(context.Request.Payload, "amount", 0m),
            CurrencyCode = PayloadReader.GetString(context.Request.Payload, "currencyCode") ?? project.CurrencyCode,
            IsEstimated = !context.Request.Payload.ContainsKey("isEstimated") || PayloadReader.GetBool(context.Request.Payload, "isEstimated"),
            IsPlayerVisible = !context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            PublicSummary = PayloadReader.GetString(context.Request.Payload, "publicSummary") ?? string.Empty,
            GMNotes = PayloadReader.GetString(context.Request.Payload, "gmNotes") ?? string.Empty,
            CreatedByUserId = actor.Id
        };
        _repositories.ManufacturingCostLedger.Insert(item);
        RecalculateManufacturingProject(project, actor.Id);
        return Ok("Manufacturing cost added.", new Dictionary<string, object> { ["item"] = CostPayload(item, true) });
    }

    public ResponseEnvelope ManufacturingPaymentAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingPaymentTracking))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        var item = new ManufacturingPaymentState
        {
            CampaignId = project.CampaignId,
            ManufacturingProjectId = project.Id,
            PaymentKind = PayloadReader.GetString(context.Request.Payload, "paymentKind") ?? ManufacturingPaymentKindIds.Deposit,
            Amount = PositiveManufacturingDecimal(context.Request.Payload, "amount", 0m),
            CurrencyCode = PayloadReader.GetString(context.Request.Payload, "currencyCode") ?? project.CurrencyCode,
            Status = ManufacturingPaymentStatusIds.Planned,
            IsPlayerVisible = !context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            PublicSummary = PayloadReader.GetString(context.Request.Payload, "publicSummary") ?? string.Empty,
            GMNotes = PayloadReader.GetString(context.Request.Payload, "gmNotes") ?? string.Empty
        };
        _repositories.ManufacturingPayments.Insert(item);
        RecalculateManufacturingProject(project, actor.Id);
        return Ok("Manufacturing payment added.", new Dictionary<string, object> { ["item"] = PaymentPayload(item, true) });
    }

    public ResponseEnvelope ManufacturingPaymentMarkPaid(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingPaymentTracking))) return ManufacturingDisabled(context.Request.Command);
        var payment = RequirePayment(context);
        payment.Status = ManufacturingPaymentStatusIds.Paid;
        payment.PaidAtUtc = DateTime.UtcNow;
        payment.ConfirmedByUserId = actor.Id;
        payment.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.ManufacturingPayments.Replace(payment);
        RecalculateManufacturingProject(_repositories.ManufacturingProjects.GetById(payment.ManufacturingProjectId), actor.Id);
        return Ok("Manufacturing payment marked paid manually.", new Dictionary<string, object> { ["item"] = PaymentPayload(payment, true) });
    }

    public ResponseEnvelope ManufacturingProgressAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingProgress))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        var entry = new ManufacturingProgressEntry
        {
            CampaignId = project.CampaignId,
            ManufacturingProjectId = project.Id,
            StageId = PayloadReader.GetString(context.Request.Payload, "stageId") ?? project.CurrentStageId,
            ProgressDelta = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "progressDelta") ?? 0),
            PublicSummary = PayloadReader.GetString(context.Request.Payload, "publicSummary") ?? string.Empty,
            GMNotes = PayloadReader.GetString(context.Request.Payload, "gmNotes") ?? string.Empty,
            IsPlayerVisible = !context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            CreatedByUserId = actor.Id
        };
        _repositories.ManufacturingProgressEntries.Insert(entry);
        project.CurrentManufacturingProgress = Math.Max(0, project.CurrentManufacturingProgress + entry.ProgressDelta);
        RecalculateManufacturingProject(project, actor.Id);
        return Ok("Manufacturing progress added.", new Dictionary<string, object> { ["item"] = ProgressPayload(entry, true), ["project"] = ManufacturingProjectPayload(project, true, false) });
    }

    public ResponseEnvelope ManufacturingTestPlanCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingTesting))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        var plan = new ManufacturingTestPlanState
        {
            CampaignId = project.CampaignId,
            ManufacturingProjectId = project.Id,
            Name = RequiredManufacturingText(context.Request.Payload, "name", "Test plan name required."),
            PublicSummary = PayloadReader.GetString(context.Request.Payload, "publicSummary") ?? string.Empty,
            GMNotes = PayloadReader.GetString(context.Request.Payload, "gmNotes") ?? string.Empty,
            IsPlayerVisible = !context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible")
        };
        _repositories.ManufacturingTestPlans.Insert(plan);
        project.TestingStatus = ManufacturingTestingStatusIds.Planned;
        RecalculateManufacturingProject(project, actor.Id);
        return Ok("Manufacturing test plan created.", new Dictionary<string, object> { ["item"] = TestPlanPayload(plan, true) });
    }

    public ResponseEnvelope ManufacturingTestResultAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingTesting))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        var result = new ManufacturingTestResultState
        {
            CampaignId = project.CampaignId,
            ManufacturingProjectId = project.Id,
            TestPlanId = PayloadReader.GetString(context.Request.Payload, "testPlanId") ?? string.Empty,
            Result = PayloadReader.GetString(context.Request.Payload, "result") ?? ManufacturingTestResultIds.Passed,
            PublicSummary = PayloadReader.GetString(context.Request.Payload, "publicSummary") ?? string.Empty,
            GMNotes = PayloadReader.GetString(context.Request.Payload, "gmNotes") ?? string.Empty,
            IsPlayerVisible = !context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            CreatedByUserId = actor.Id
        };
        _repositories.ManufacturingTestResults.Insert(result);
        project.TestingStatus = result.Result == ManufacturingTestResultIds.Failed ? ManufacturingTestingStatusIds.Failed : result.Result;
        if (project.TestingStatus == ManufacturingTestingStatusIds.Failed) project.ManufacturingStatus = ManufacturingStatusIds.Rework;
        RecalculateManufacturingProject(project, actor.Id);
        return Ok("Manufacturing test result added.", new Dictionary<string, object> { ["item"] = TestResultPayload(result, true) });
    }

    public ResponseEnvelope ManufacturingDefectCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingDefects))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        var defect = new ManufacturingDefectState
        {
            CampaignId = project.CampaignId,
            ManufacturingProjectId = project.Id,
            Severity = PayloadReader.GetString(context.Request.Payload, "severity") ?? "minor",
            IsCritical = PayloadReader.GetBool(context.Request.Payload, "isCritical"),
            PublicSummary = PayloadReader.GetString(context.Request.Payload, "publicSummary") ?? "Замечание по результату производства.",
            GMNotes = PayloadReader.GetString(context.Request.Payload, "gmNotes") ?? string.Empty,
            IsPlayerVisible = PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            UpdatedByUserId = actor.Id
        };
        _repositories.ManufacturingDefects.Insert(defect);
        project.DefectSummary = BuildDefectSummary(project.Id, admin: true);
        project.ManufacturingStatus = defect.IsCritical ? ManufacturingStatusIds.Rework : project.ManufacturingStatus;
        RecalculateManufacturingProject(project, actor.Id);
        return Ok("Manufacturing defect created.", new Dictionary<string, object> { ["item"] = DefectPayload(defect, true) });
    }

    public ResponseEnvelope ManufacturingDefectResolve(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingDefects))) return ManufacturingDisabled(context.Request.Command);
        var defect = RequireDefect(context);
        defect.Status = PayloadReader.GetBool(context.Request.Payload, "acceptedAsIs") ? ManufacturingDefectStatusIds.AcceptedAsIs : ManufacturingDefectStatusIds.Resolved;
        defect.UpdatedAtUtc = DateTime.UtcNow;
        defect.UpdatedByUserId = actor.Id;
        _repositories.ManufacturingDefects.Replace(defect);
        RecalculateManufacturingProject(_repositories.ManufacturingProjects.GetById(defect.ManufacturingProjectId), actor.Id);
        return Ok("Manufacturing defect resolved.", new Dictionary<string, object> { ["item"] = DefectPayload(defect, true) });
    }

    public ResponseEnvelope ManufacturingAcceptancePrepare(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingAcceptance))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        var acceptance = new ManufacturingAcceptanceState
        {
            CampaignId = project.CampaignId,
            ManufacturingProjectId = project.Id,
            Status = ManufacturingAcceptanceStatusIds.ReadyForReview,
            PublicSummary = PayloadReader.GetString(context.Request.Payload, "publicSummary") ?? "Результат готов к приёмке GM.",
            GMNotes = PayloadReader.GetString(context.Request.Payload, "gmNotes") ?? string.Empty,
            IsPlayerVisible = !context.Request.Payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(context.Request.Payload, "isPlayerVisible"),
            ReviewedByUserId = actor.Id
        };
        _repositories.ManufacturingAcceptances.Insert(acceptance);
        project.AcceptanceStatus = ManufacturingAcceptanceStatusIds.ReadyForReview;
        project.ManufacturingStatus = ManufacturingStatusIds.AwaitingAcceptance;
        RecalculateManufacturingProject(project, actor.Id);
        return Ok("Manufacturing acceptance prepared.", new Dictionary<string, object> { ["item"] = AcceptancePayload(acceptance, true) });
    }

    public ResponseEnvelope ManufacturingAcceptanceAccept(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingAcceptance))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        var hasCriticalOpenDefects = _repositories.ManufacturingDefects.Find(Builders<ManufacturingDefectState>.Filter.Eq(x => x.ManufacturingProjectId, project.Id)
            & Builders<ManufacturingDefectState>.Filter.Eq(x => x.IsCritical, true)
            & Builders<ManufacturingDefectState>.Filter.Ne(x => x.Status, ManufacturingDefectStatusIds.Resolved)
            & Builders<ManufacturingDefectState>.Filter.Ne(x => x.Status, ManufacturingDefectStatusIds.AcceptedAsIs)).Any();
        var gmOverride = PayloadReader.GetBool(context.Request.Payload, "gmOverride");
        if (hasCriticalOpenDefects && !gmOverride) throw new InvalidOperationException("Critical defects block acceptance unless GM override is provided.");
        var acceptance = _repositories.ManufacturingAcceptances.Find(Builders<ManufacturingAcceptanceState>.Filter.Eq(x => x.ManufacturingProjectId, project.Id)).OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefault()
            ?? new ManufacturingAcceptanceState { CampaignId = project.CampaignId, ManufacturingProjectId = project.Id };
        acceptance.Status = hasCriticalOpenDefects ? ManufacturingAcceptanceStatusIds.AcceptedWithDefects : ManufacturingAcceptanceStatusIds.Accepted;
        acceptance.AcceptedWithDefects = hasCriticalOpenDefects;
        acceptance.GMOverride = gmOverride;
        acceptance.PublicSummary = PayloadReader.GetString(context.Request.Payload, "publicSummary") ?? acceptance.PublicSummary;
        acceptance.GMNotes = PayloadReader.GetString(context.Request.Payload, "gmNotes") ?? acceptance.GMNotes;
        acceptance.UpdatedAtUtc = DateTime.UtcNow;
        acceptance.ReviewedByUserId = actor.Id;
        if (string.IsNullOrWhiteSpace(acceptance.Id)) _repositories.ManufacturingAcceptances.Insert(acceptance); else _repositories.ManufacturingAcceptances.Replace(acceptance);
        project.AcceptanceStatus = acceptance.Status;
        project.ManufacturingStatus = ManufacturingStatusIds.Accepted;
        project.AssetCreationStatus = ManufacturingAssetCreationStatusIds.Ready;
        RecalculateManufacturingProject(project, actor.Id);
        TryWriteManufacturingJournal(project, "manufacturing.acceptance.accepted", "Производственный результат принят GM", actor.Id, project.IsPlayerVisible);
        return Ok("Manufacturing accepted. Asset creation is now allowed.", new Dictionary<string, object> { ["item"] = AcceptancePayload(acceptance, true), ["project"] = ManufacturingProjectPayload(project, true, true) });
    }

    public ResponseEnvelope ManufacturingAcceptanceReject(CommandContext context)
        => SetManufacturingStatus(context, ManufacturingStatusIds.Rework, "Manufacturing acceptance rejected; project moved to rework.");

    public ResponseEnvelope ManufacturingAssetCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturedAssets))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        if (project.AcceptanceStatus != ManufacturingAcceptanceStatusIds.Accepted && project.AcceptanceStatus != ManufacturingAcceptanceStatusIds.AcceptedWithDefects)
            throw new InvalidOperationException("Manufacturing result must be accepted before asset creation.");
        if (project.AssetCreationStatus == ManufacturingAssetCreationStatusIds.Created && project.CreatedAssetIds.Count > 0)
            throw new InvalidOperationException("Asset already created for this manufacturing project.");

        var manufactured = new ManufacturedAssetState
        {
            CampaignId = project.CampaignId,
            ManufacturingProjectId = project.Id,
            Name = PayloadReader.GetString(context.Request.Payload, "name") ?? project.Name,
            AssetType = PayloadReader.GetString(context.Request.Payload, "assetType") ?? "vehicle_asset",
            BlueprintId = project.SourceBlueprintId,
            OwnerEntityType = project.OwnerEntityType,
            OwnerEntityId = project.OwnerEntityId,
            OperatorEntityType = project.OperatorEntityType,
            OperatorEntityId = project.OperatorEntityId,
            IsPlayerVisible = project.IsPlayerVisible,
            VisibilityMode = project.VisibilityMode,
            PublicSummary = PayloadReader.GetString(context.Request.Payload, "publicSummary") ?? project.ActualResultSummary,
            GMNotes = PayloadReader.GetString(context.Request.Payload, "gmNotes") ?? string.Empty,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };
        var economyAsset = new AssetState
        {
            Name = manufactured.Name,
            CampaignId = manufactured.CampaignId,
            AssetType = manufactured.AssetType,
            DefinitionId = manufactured.BlueprintId,
            LegalStatus = project.LegalBoundarySummary,
            ActualStatus = ManufacturedAssetStatusIds.Created,
            Notes = manufactured.PublicSummary,
            IsActive = true
        };
        if (string.Equals(project.OwnerEntityType, "character", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(project.OwnerEntityId))
            economyAsset.OwnerCharacterIds.Add(project.OwnerEntityId);
        if (string.Equals(project.OwnerEntityType, "organization", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(project.OwnerEntityId))
            economyAsset.OwnerOrganizationIds.Add(project.OwnerEntityId);
        if (string.Equals(project.OwnerEntityType, "faction", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(project.OwnerEntityId))
            economyAsset.OwnerFactionIds.Add(project.OwnerEntityId);
        _repositories.AssetStates.UpsertAsync(economyAsset).GetAwaiter().GetResult();
        manufactured.AssetStateId = economyAsset.Id;
        _repositories.ManufacturedAssets.Insert(manufactured);
        project.CreatedAssetIds.Add(manufactured.Id);
        project.AssetCreationStatus = ManufacturingAssetCreationStatusIds.Created;
        project.ManufacturingStatus = ManufacturingStatusIds.AssetCreated;
        project.ActualResultSummary = string.IsNullOrWhiteSpace(project.ActualResultSummary) ? manufactured.PublicSummary : project.ActualResultSummary;
        RecalculateManufacturingProject(project, actor.Id);
        TryWriteManufacturingJournal(project, "manufacturing.asset.created", "Создан произведённый актив", actor.Id, manufactured.IsPlayerVisible);
        return Ok("Manufactured asset created after GM acceptance.", new Dictionary<string, object> { ["item"] = ManufacturedAssetPayload(manufactured, true), ["project"] = ManufacturingProjectPayload(project, true, true) });
    }

    public ResponseEnvelope ManufacturingAssetTransfer(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingOwnershipTransfer))) return ManufacturingDisabled(context.Request.Command);
        var asset = RequireManufacturedAsset(context);
        asset.OwnerEntityType = PayloadReader.GetString(context.Request.Payload, "ownerEntityType") ?? asset.OwnerEntityType;
        asset.OwnerEntityId = PayloadReader.GetString(context.Request.Payload, "ownerEntityId") ?? asset.OwnerEntityId;
        asset.Status = ManufacturedAssetStatusIds.Transferred;
        asset.UpdatedAtUtc = DateTime.UtcNow;
        asset.UpdatedByUserId = actor.Id;
        _repositories.ManufacturedAssets.Replace(asset);
        var project = _repositories.ManufacturingProjects.GetById(asset.ManufacturingProjectId);
        if (project != null)
        {
            project.OwnerEntityType = asset.OwnerEntityType;
            project.OwnerEntityId = asset.OwnerEntityId;
            project.ManufacturingStatus = ManufacturingStatusIds.Delivered;
            RecalculateManufacturingProject(project, actor.Id);
        }
        return Ok("Manufactured asset transferred.", new Dictionary<string, object> { ["item"] = ManufacturedAssetPayload(asset, true) });
    }

    public ResponseEnvelope ManufacturingAssetCommission(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingOperationStart))) return ManufacturingDisabled(context.Request.Command);
        var asset = RequireManufacturedAsset(context);
        asset.Status = ManufacturedAssetStatusIds.Commissioned;
        asset.UpdatedAtUtc = DateTime.UtcNow;
        asset.UpdatedByUserId = actor.Id;
        _repositories.ManufacturedAssets.Replace(asset);
        var project = _repositories.ManufacturingProjects.GetById(asset.ManufacturingProjectId);
        if (project != null)
        {
            project.ManufacturingStatus = ManufacturingStatusIds.Commissioned;
            RecalculateManufacturingProject(project, actor.Id);
        }
        return Ok("Manufactured asset commissioned.", new Dictionary<string, object> { ["item"] = ManufacturedAssetPayload(asset, true) });
    }

    public ResponseEnvelope ManufacturingAssetList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!ManufacturingAdminEnabled()) return ManufacturingDisabled(context.Request.Command);
        }
        else if (!ManufacturingPlayerEnabled())
        {
            return ManufacturingDisabled(context.Request.Command);
        }
        var filter = ManufacturingCampaignFilter<ManufacturedAssetState>(context.Request.Payload);
        if (!IsAdmin(actor)) filter &= PlayerManufacturedAssetFilter(actor);
        var items = _repositories.ManufacturedAssets.Find(filter).OrderByDescending(x => x.UpdatedAtUtc).Take(300).Select(x => (object)ManufacturedAssetPayload(x, IsAdmin(actor))).ToArray();
        return Ok("Manufactured assets loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ManufacturingPlayerAssetList(CommandContext context) => ManufacturingAssetList(context);

    public ResponseEnvelope ManufacturingPlayerContributionSubmit(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ManufacturingPlayerEnabled()) return ManufacturingDisabled(context.Request.Command);
        var request = BuildProductionRequest(context, actor, PlayerRequestTypeIds.FactoryOrder, "Заявка по производству");
        request.Title = PayloadReader.GetString(context.Request.Payload, "summary") ?? "Комментарий к производству";
        request.Description = PayloadReader.GetString(context.Request.Payload, "description") ?? PayloadReader.GetString(context.Request.Payload, "comment") ?? string.Empty;
        _repositories.PlayerRequests.Insert(request);
        return Ok("Manufacturing contribution request submitted.", new Dictionary<string, object> { ["requestId"] = request.Id, ["item"] = PlayerRequestPayload(request, actor, includeAdminFields: false) });
    }

    private ResponseEnvelope SetManufacturingStatus(CommandContext context, string status, string message)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingProjects))) return ManufacturingDisabled(context.Request.Command);
        var project = RequireManufacturingProject(context);
        project.ManufacturingStatus = status;
        if (status == ManufacturingStatusIds.Active && !project.ActualStartWorldDateTime.HasValue) project.ActualStartWorldDateTime = DateTime.UtcNow;
        if (status == ManufacturingStatusIds.Completed || status == ManufacturingStatusIds.Failed || status == ManufacturingStatusIds.Cancelled) project.ActualEndWorldDateTime = DateTime.UtcNow;
        RecalculateManufacturingProject(project, actor.Id);
        TryPublishManufacturingSync("manufacturing.project.changed", project, status, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok(message, new Dictionary<string, object> { ["item"] = ManufacturingProjectPayload(project, true, true) });
    }

    private ResponseEnvelope SetStageStatus(CommandContext context, string status, string message)
    {
        var actor = RequireAdmin(context);
        if (!ManufacturingAdminEnabled() || !_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingStages))) return ManufacturingDisabled(context.Request.Command);
        var stage = RequireManufacturingStage(context);
        stage.Status = status;
        stage.UpdatedAtUtc = DateTime.UtcNow;
        stage.UpdatedByUserId = actor.Id;
        if (status == ManufacturingStageStatusIds.Completed) stage.CurrentProgress = Math.Max(stage.CurrentProgress, stage.RequiredProgress);
        _repositories.ManufacturingStages.Replace(stage);
        var project = _repositories.ManufacturingProjects.GetById(stage.ManufacturingProjectId);
        if (project != null)
        {
            project.CurrentStageId = stage.Id;
            if (status == ManufacturingStageStatusIds.Active) project.ManufacturingStatus = ManufacturingStatusIds.Active;
            RecalculateManufacturingProject(project, actor.Id);
        }
        return Ok(message, new Dictionary<string, object> { ["item"] = StagePayload(stage, true) });
    }

    private ManufacturingProjectState BuildManufacturingProjectFromOrder(FactoryOrderState order, UserAccount actor, IDictionary<string, object> payload)
    {
        return new ManufacturingProjectState
        {
            CampaignId = order.CampaignId,
            FactoryOrderId = order.Id,
            ProjectId = order.ProjectBaseId,
            FacilityId = order.FacilityId,
            SourceBlueprintId = order.BlueprintId,
            SourcePresetDesignId = order.PresetId,
            Name = PayloadReader.GetString(payload, "name") ?? order.Name,
            Description = order.PublicStatusSummary,
            ManufacturingProjectId = Guid.NewGuid().ToString("N"),
            ManufacturingType = InferManufacturingType(order),
            ProductionDomain = InferManufacturingDomain(order),
            OrderKind = InferManufacturingOrderKind(order),
            Quantity = Math.Max(1, PayloadReader.GetInt(payload, "quantity") ?? 1),
            OwnerEntityType = string.IsNullOrWhiteSpace(order.OwnerCharacterId) ? "user" : "character",
            OwnerEntityId = string.IsNullOrWhiteSpace(order.OwnerCharacterId) ? order.OwnerUserId : order.OwnerCharacterId,
            CustomerEntityType = "user",
            CustomerEntityId = order.OwnerUserId,
            ManufacturingStatus = ManufacturingStatusIds.Planning,
            RequiredProgressTotal = Math.Max(1, order.EstimatedWorkPoints),
            EstimatedTotalCost = order.EstimatedCost,
            CurrencyCode = "MO",
            CostBreakdownSummary = $"Оценка заказа: {order.EstimatedCost} MO.",
            ResourceRequirementSummary = order.RequiredResourcesSummary,
            ExpectedResultSummary = order.Name,
            LegalBoundarySummary = order.LegalStatusHint,
            ManufacturingRiskRating = order.RiskSummary,
            GMHiddenRiskSummary = order.GMHiddenTermsSummary,
            IsPlayerVisible = order.IsPlayerVisible,
            VisibilityMode = order.VisibilityMode,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };
    }

    private ManufacturingProjectState BuildManualManufacturingProject(IDictionary<string, object> payload, UserAccount actor)
    {
        var name = RequiredManufacturingText(payload, "name", "Manufacturing project name required.");
        return new ManufacturingProjectState
        {
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? "default",
            ManufacturingProjectId = Guid.NewGuid().ToString("N"),
            Name = name,
            Description = PayloadReader.GetString(payload, "description") ?? string.Empty,
            FacilityId = PayloadReader.GetString(payload, "facilityId") ?? string.Empty,
            SourceBlueprintId = PayloadReader.GetString(payload, "blueprintId") ?? string.Empty,
            SourcePresetDesignId = PayloadReader.GetString(payload, "presetId") ?? string.Empty,
            ManufacturingType = PayloadReader.GetString(payload, "manufacturingType") ?? ManufacturingTypeIds.Custom,
            ProductionDomain = PayloadReader.GetString(payload, "productionDomain") ?? ProductionDomainIds.Custom,
            OrderKind = PayloadReader.GetString(payload, "orderKind") ?? ManufacturingOrderKindIds.Custom,
            Quantity = Math.Max(1, PayloadReader.GetInt(payload, "quantity") ?? 1),
            OwnerEntityType = PayloadReader.GetString(payload, "ownerEntityType") ?? string.Empty,
            OwnerEntityId = PayloadReader.GetString(payload, "ownerEntityId") ?? string.Empty,
            RequiredProgressTotal = Math.Max(1, PayloadReader.GetInt(payload, "requiredProgressTotal") ?? 100),
            EstimatedTotalCost = PositiveManufacturingDecimal(payload, "estimatedTotalCost", 0m),
            CurrencyCode = PayloadReader.GetString(payload, "currencyCode") ?? "MO",
            ExpectedResultSummary = PayloadReader.GetString(payload, "expectedResultSummary") ?? name,
            LegalBoundarySummary = PayloadReader.GetString(payload, "legalBoundarySummary") ?? string.Empty,
            IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
            VisibilityMode = PayloadReader.GetString(payload, "visibilityMode") ?? ProjectVisibilityModeIds.OwnerOnly,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };
    }

    private void CreateDefaultManufacturingStages(ManufacturingProjectState project, string actorId)
    {
        var defaults = new[]
        {
            (ManufacturingStageTypeIds.ResourcePreparation, "Ресурсы", 10, true, false, false),
            (ManufacturingStageTypeIds.Fabrication, "Изготовление", 35, true, true, false),
            (ManufacturingStageTypeIds.Assembly, "Сборка", 30, true, false, false),
            (ManufacturingStageTypeIds.Testing, "Испытания", 15, false, false, true),
            (ManufacturingStageTypeIds.Acceptance, "Приёмка", 10, false, false, false)
        };
        for (var i = 0; i < defaults.Length; i++)
        {
            var item = defaults[i];
            _repositories.ManufacturingStages.Insert(new ManufacturingStageState
            {
                CampaignId = project.CampaignId,
                ManufacturingProjectId = project.Id,
                StageType = item.Item1,
                Name = item.Item2,
                SortOrder = i + 1,
                RequiredProgress = item.Item3,
                RequiresResources = item.Item4,
                RequiresPayment = item.Item5,
                RequiresTesting = item.Item6,
                PublicSummary = item.Item2,
                UpdatedByUserId = actorId
            });
        }
        RecalculateManufacturingProject(project, actorId);
    }

    private ManufacturingStageState BuildManufacturingStage(ManufacturingProjectState project, IDictionary<string, object> payload, string actorId)
    {
        return new ManufacturingStageState
        {
            CampaignId = project.CampaignId,
            ManufacturingProjectId = project.Id,
            StageType = PayloadReader.GetString(payload, "stageType") ?? ManufacturingStageTypeIds.Custom,
            Name = RequiredManufacturingText(payload, "name", "Stage name required."),
            SortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? NextManufacturingStageSort(project.Id),
            RequiredProgress = Math.Max(0, PayloadReader.GetInt(payload, "requiredProgress") ?? 20),
            RequiresResources = !payload.ContainsKey("requiresResources") || PayloadReader.GetBool(payload, "requiresResources"),
            RequiresPayment = PayloadReader.GetBool(payload, "requiresPayment"),
            RequiresTesting = PayloadReader.GetBool(payload, "requiresTesting"),
            IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
            PublicSummary = PayloadReader.GetString(payload, "publicSummary") ?? string.Empty,
            GMNotes = PayloadReader.GetString(payload, "gmNotes") ?? string.Empty,
            UpdatedByUserId = actorId
        };
    }

    private void UpdateManufacturingStage(ManufacturingStageState stage, IDictionary<string, object> payload, string actorId)
    {
        stage.Name = PayloadReader.GetString(payload, "name") ?? stage.Name;
        stage.StageType = PayloadReader.GetString(payload, "stageType") ?? stage.StageType;
        stage.Status = PayloadReader.GetString(payload, "status") ?? stage.Status;
        stage.SortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? stage.SortOrder;
        stage.RequiredProgress = Math.Max(0, PayloadReader.GetInt(payload, "requiredProgress") ?? stage.RequiredProgress);
        stage.CurrentProgress = Math.Max(0, PayloadReader.GetInt(payload, "currentProgress") ?? stage.CurrentProgress);
        if (payload.ContainsKey("isPlayerVisible")) stage.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        stage.PublicSummary = PayloadReader.GetString(payload, "publicSummary") ?? stage.PublicSummary;
        stage.GMNotes = PayloadReader.GetString(payload, "gmNotes") ?? stage.GMNotes;
        stage.UpdatedAtUtc = DateTime.UtcNow;
        stage.UpdatedByUserId = actorId;
    }

    private void RecalculateManufacturingProject(ManufacturingProjectState? project, string actorId)
    {
        if (project == null) return;
        var stages = _repositories.ManufacturingStages.Find(Builders<ManufacturingStageState>.Filter.Eq(x => x.ManufacturingProjectId, project.Id)).ToList();
        var plannedTotal = stages.Sum(x => Math.Max(0, x.RequiredProgress));
        var stageDone = stages.Sum(x => Math.Min(Math.Max(0, x.CurrentProgress), Math.Max(0, x.RequiredProgress)));
        project.RequiredProgressTotal = Math.Max(project.RequiredProgressTotal, plannedTotal > 0 ? plannedTotal : project.RequiredProgressTotal);
        project.CurrentManufacturingProgress = Math.Max(project.CurrentManufacturingProgress, stageDone);
        project.ProgressPercent = project.RequiredProgressTotal <= 0 ? 0 : Math.Round(Math.Min(100m, project.CurrentManufacturingProgress * 100m / project.RequiredProgressTotal), 2);

        var resourcePlans = _repositories.ManufacturingResourcePlans.Find(Builders<ManufacturingResourcePlanState>.Filter.Eq(x => x.ManufacturingProjectId, project.Id)).ToList();
        if (resourcePlans.Count == 0) project.ResourceStatus = ManufacturingResourceStatusIds.NotRequired;
        else if (resourcePlans.All(x => x.ConsumedQuantity >= x.RequiredQuantity)) project.ResourceStatus = ManufacturingResourceStatusIds.Consumed;
        else if (resourcePlans.All(x => x.ReservedQuantity >= x.RequiredQuantity)) project.ResourceStatus = ManufacturingResourceStatusIds.Reserved;
        else if (resourcePlans.Any(x => x.ReservedQuantity > 0)) project.ResourceStatus = ManufacturingResourceStatusIds.PartiallyReserved;
        else project.ResourceStatus = ManufacturingResourceStatusIds.Planned;
        project.ResourceRequirementSummary = resourcePlans.Count == 0 ? project.ResourceRequirementSummary : string.Join("; ", resourcePlans.Select(x => $"{x.ResourceName}: {x.RequiredQuantity} {x.Unit}").Take(10));
        project.ReservedResourceSummary = string.Join("; ", resourcePlans.Where(x => x.ReservedQuantity > 0).Select(x => $"{x.ResourceName}: {x.ReservedQuantity}/{x.RequiredQuantity} {x.Unit}").Take(10));
        project.ConsumedResourceSummary = string.Join("; ", resourcePlans.Where(x => x.ConsumedQuantity > 0).Select(x => $"{x.ResourceName}: {x.ConsumedQuantity}/{x.RequiredQuantity} {x.Unit}").Take(10));

        var costs = _repositories.ManufacturingCostLedger.Find(Builders<ManufacturingCostLedgerEntry>.Filter.Eq(x => x.ManufacturingProjectId, project.Id)).ToList();
        project.EstimatedTotalCost = costs.Where(x => x.IsEstimated).Sum(x => x.Amount) > 0 ? costs.Where(x => x.IsEstimated).Sum(x => x.Amount) : project.EstimatedTotalCost;
        project.ActualTotalCost = costs.Where(x => !x.IsEstimated).Sum(x => x.Amount);
        var payments = _repositories.ManufacturingPayments.Find(Builders<ManufacturingPaymentState>.Filter.Eq(x => x.ManufacturingProjectId, project.Id)).ToList();
        if (payments.Count == 0) project.PaymentStatus = ManufacturingPaymentStatusIds.NotRequired;
        else if (payments.All(x => x.Status == ManufacturingPaymentStatusIds.Paid || x.Status == ManufacturingPaymentStatusIds.WaivedByGm)) project.PaymentStatus = ManufacturingPaymentStatusIds.Paid;
        else if (payments.Any(x => x.Status == ManufacturingPaymentStatusIds.Paid)) project.PaymentStatus = ManufacturingPaymentStatusIds.PartiallyPaid;
        else project.PaymentStatus = ManufacturingPaymentStatusIds.Planned;
        project.PaymentPlanSummary = payments.Count == 0 ? project.PaymentPlanSummary : string.Join("; ", payments.Select(x => $"{x.PaymentKind}: {x.Amount} {x.CurrencyCode} {x.Status}").Take(10));

        project.DefectSummary = BuildDefectSummary(project.Id, admin: true);
        project.UpdatedAtUtc = DateTime.UtcNow;
        project.UpdatedByUserId = actorId;
        _repositories.ManufacturingProjects.Replace(project);
    }

    private void EnsureManufacturingProjectBase(ManufacturingProjectState project, UserAccount actor)
    {
        if (!_featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectFoundationMvp))) return;
        if (!string.IsNullOrWhiteSpace(project.ProjectId) && _repositories.Projects.GetById(project.ProjectId) != null) return;
        var baseProject = new ProjectBaseState
        {
            CampaignId = project.CampaignId,
            ProjectType = ProjectTypeIds.Manufacturing,
            Name = project.Name,
            PublicSummary = project.Description,
            Status = ProjectStatusIds.Preparation,
            OwnerUserId = actor.Id,
            VisibilityMode = project.VisibilityMode,
            IsPlayerVisible = project.IsPlayerVisible,
            WorkPointsRequired = project.RequiredProgressTotal,
            WorkPointsDone = project.CurrentManufacturingProgress,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };
        _repositories.Projects.Insert(baseProject);
        project.ProjectId = baseProject.Id;
        _repositories.ManufacturingProjects.Replace(project);
    }

    private void ReleaseOpenReservations(ManufacturingProjectState project, string actorId)
    {
        var reservations = _repositories.ManufacturingResourceReservations.Find(Builders<ManufacturingResourceReservationState>.Filter.Eq(x => x.ManufacturingProjectId, project.Id)).ToList();
        foreach (var reservation in reservations.Where(x => x.ConsumedQuantity <= 0 && x.Status == ManufacturingReservationStatusIds.Reserved))
        {
            reservation.Status = ManufacturingReservationStatusIds.Released;
            reservation.UpdatedAtUtc = DateTime.UtcNow;
            reservation.UpdatedByUserId = actorId;
            _repositories.ManufacturingResourceReservations.Replace(reservation);
        }
    }

    private int NextManufacturingStageSort(string projectId)
    {
        var existing = _repositories.ManufacturingStages.Find(Builders<ManufacturingStageState>.Filter.Eq(x => x.ManufacturingProjectId, projectId));
        return existing.Count == 0 ? 1 : existing.Max(x => x.SortOrder) + 1;
    }

    private ManufacturingProjectState RequireManufacturingProject(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "id")
            ?? PayloadReader.GetString(context.Request.Payload, "manufacturingProjectId")
            ?? PayloadReader.GetString(context.Request.Payload, "projectId")
            ?? string.Empty;
        return _repositories.ManufacturingProjects.GetById(id) ?? throw new InvalidOperationException("Manufacturing project not found.");
    }

    private ManufacturingStageState RequireManufacturingStage(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "stageId") ?? PayloadReader.GetString(context.Request.Payload, "id") ?? string.Empty;
        return _repositories.ManufacturingStages.GetById(id) ?? throw new InvalidOperationException("Manufacturing stage not found.");
    }

    private ManufacturingResourcePlanState RequireResourcePlan(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "resourcePlanId") ?? PayloadReader.GetString(context.Request.Payload, "id") ?? string.Empty;
        return _repositories.ManufacturingResourcePlans.GetById(id) ?? throw new InvalidOperationException("Manufacturing resource plan not found.");
    }

    private ManufacturingResourceReservationState RequireReservation(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "reservationId") ?? PayloadReader.GetString(context.Request.Payload, "id") ?? string.Empty;
        return _repositories.ManufacturingResourceReservations.GetById(id) ?? throw new InvalidOperationException("Manufacturing reservation not found.");
    }

    private ManufacturingPaymentState RequirePayment(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "paymentId") ?? PayloadReader.GetString(context.Request.Payload, "id") ?? string.Empty;
        return _repositories.ManufacturingPayments.GetById(id) ?? throw new InvalidOperationException("Manufacturing payment not found.");
    }

    private ManufacturingDefectState RequireDefect(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "defectId") ?? PayloadReader.GetString(context.Request.Payload, "id") ?? string.Empty;
        return _repositories.ManufacturingDefects.GetById(id) ?? throw new InvalidOperationException("Manufacturing defect not found.");
    }

    private ManufacturedAssetState RequireManufacturedAsset(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "assetId") ?? PayloadReader.GetString(context.Request.Payload, "id") ?? string.Empty;
        return _repositories.ManufacturedAssets.GetById(id) ?? throw new InvalidOperationException("Manufactured asset not found.");
    }

    private Dictionary<string, object> ManufacturingProjectPayload(ManufacturingProjectState x, bool admin, bool includeDetails)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = x.Id,
            ["manufacturingProjectId"] = x.ManufacturingProjectId,
            ["projectId"] = x.ProjectId,
            ["campaignId"] = x.CampaignId,
            ["factoryOrderId"] = x.FactoryOrderId,
            ["facilityId"] = x.FacilityId,
            ["sourceBlueprintId"] = x.SourceBlueprintId,
            ["sourcePresetDesignId"] = x.SourcePresetDesignId,
            ["name"] = x.Name,
            ["description"] = x.Description,
            ["manufacturingType"] = x.ManufacturingType,
            ["productionDomain"] = x.ProductionDomain,
            ["orderKind"] = x.OrderKind,
            ["quantity"] = x.Quantity,
            ["manufacturingStatus"] = x.ManufacturingStatus,
            ["status"] = x.ManufacturingStatus,
            ["resourceStatus"] = x.ResourceStatus,
            ["paymentStatus"] = x.PaymentStatus,
            ["testingStatus"] = x.TestingStatus,
            ["acceptanceStatus"] = x.AcceptanceStatus,
            ["assetCreationStatus"] = x.AssetCreationStatus,
            ["progressPercent"] = x.ProgressPercent,
            ["currentManufacturingProgress"] = x.CurrentManufacturingProgress,
            ["requiredProgressTotal"] = x.RequiredProgressTotal,
            ["estimatedTotalCost"] = x.EstimatedTotalCost,
            ["actualTotalCost"] = x.ActualTotalCost,
            ["currencyCode"] = x.CurrencyCode,
            ["resourceRequirementSummary"] = x.ResourceRequirementSummary,
            ["reservedResourceSummary"] = x.ReservedResourceSummary,
            ["consumedResourceSummary"] = x.ConsumedResourceSummary,
            ["paymentPlanSummary"] = x.PaymentPlanSummary,
            ["expectedResultSummary"] = x.ExpectedResultSummary,
            ["actualResultSummary"] = x.ActualResultSummary,
            ["createdAssetIds"] = x.CreatedAssetIds.ToArray(),
            ["defectSummary"] = admin ? x.DefectSummary : BuildDefectSummary(x.Id, admin: false),
            ["legalBoundarySummary"] = x.LegalBoundarySummary,
            ["isPlayerVisible"] = x.IsPlayerVisible,
            ["visibilityMode"] = x.VisibilityMode,
            ["updatedAtUtc"] = x.UpdatedAtUtc.ToString("O")
        };
        if (admin)
        {
            result["ownerEntityType"] = x.OwnerEntityType;
            result["ownerEntityId"] = x.OwnerEntityId;
            result["operatorEntityType"] = x.OperatorEntityType;
            result["operatorEntityId"] = x.OperatorEntityId;
            result["manufacturingRiskRating"] = x.ManufacturingRiskRating;
            result["gmHiddenRiskSummary"] = x.GMHiddenRiskSummary;
            result["costBreakdownSummary"] = x.CostBreakdownSummary;
        }
        if (includeDetails)
        {
            result["stages"] = _repositories.ManufacturingStages.Find(Builders<ManufacturingStageState>.Filter.Eq(s => s.ManufacturingProjectId, x.Id)).OrderBy(s => s.SortOrder).Where(s => admin || s.IsPlayerVisible).Select(s => (object)StagePayload(s, admin)).ToArray();
            result["resourcePlans"] = _repositories.ManufacturingResourcePlans.Find(Builders<ManufacturingResourcePlanState>.Filter.Eq(s => s.ManufacturingProjectId, x.Id)).Where(s => admin || s.IsPlayerVisible).Select(s => (object)ResourcePlanPayload(s, admin)).ToArray();
            result["reservations"] = _repositories.ManufacturingResourceReservations.Find(Builders<ManufacturingResourceReservationState>.Filter.Eq(s => s.ManufacturingProjectId, x.Id)).Where(s => admin || s.IsPlayerVisible).Select(s => (object)ReservationPayload(s, admin)).ToArray();
            result["payments"] = _repositories.ManufacturingPayments.Find(Builders<ManufacturingPaymentState>.Filter.Eq(s => s.ManufacturingProjectId, x.Id)).Where(s => admin || s.IsPlayerVisible).Select(s => (object)PaymentPayload(s, admin)).ToArray();
            result["testResults"] = _repositories.ManufacturingTestResults.Find(Builders<ManufacturingTestResultState>.Filter.Eq(s => s.ManufacturingProjectId, x.Id)).Where(s => admin || s.IsPlayerVisible).Select(s => (object)TestResultPayload(s, admin)).ToArray();
            result["defects"] = _repositories.ManufacturingDefects.Find(Builders<ManufacturingDefectState>.Filter.Eq(s => s.ManufacturingProjectId, x.Id)).Where(s => admin || s.IsPlayerVisible).Select(s => (object)DefectPayload(s, admin)).ToArray();
            result["assets"] = _repositories.ManufacturedAssets.Find(Builders<ManufacturedAssetState>.Filter.Eq(s => s.ManufacturingProjectId, x.Id)).Where(s => admin || CanPlayerSeeManufacturedAsset(s)).Select(s => (object)ManufacturedAssetPayload(s, admin)).ToArray();
        }
        return result;
    }

    private static Dictionary<string, object> StagePayload(ManufacturingStageState x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["stageType"] = x.StageType, ["name"] = x.Name,
            ["status"] = x.Status, ["sortOrder"] = x.SortOrder, ["requiredProgress"] = x.RequiredProgress, ["currentProgress"] = x.CurrentProgress,
            ["requiresResources"] = x.RequiresResources, ["requiresPayment"] = x.RequiresPayment, ["requiresTesting"] = x.RequiresTesting,
            ["isPlayerVisible"] = x.IsPlayerVisible, ["publicSummary"] = x.PublicSummary
        };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> ResourcePlanPayload(ManufacturingResourcePlanState x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["stageId"] = x.StageId, ["resourceType"] = x.ResourceType,
            ["resourceId"] = admin ? x.ResourceId : string.Empty, ["resourceName"] = x.ResourceName, ["requiredQuantity"] = x.RequiredQuantity,
            ["reservedQuantity"] = x.ReservedQuantity, ["consumedQuantity"] = x.ConsumedQuantity, ["unit"] = x.Unit, ["status"] = x.Status,
            ["isPlayerVisible"] = x.IsPlayerVisible, ["publicSummary"] = x.PublicSummary
        };
        if (admin) { result["sourceType"] = x.SourceType; result["sourceId"] = x.SourceId; result["gmNotes"] = x.GMNotes; }
        return result;
    }

    private static Dictionary<string, object> ReservationPayload(ManufacturingResourceReservationState x, bool admin) => new()
    {
        ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["resourcePlanId"] = x.ResourcePlanId,
        ["reservedQuantity"] = x.ReservedQuantity, ["consumedQuantity"] = x.ConsumedQuantity, ["unit"] = x.Unit,
        ["inventoryItemId"] = admin ? x.InventoryItemId : string.Empty, ["status"] = x.Status, ["isPlayerVisible"] = x.IsPlayerVisible
    };

    private static Dictionary<string, object> CostPayload(ManufacturingCostLedgerEntry x, bool admin)
    {
        var result = new Dictionary<string, object> { ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["costType"] = x.CostType, ["amount"] = x.Amount, ["currencyCode"] = x.CurrencyCode, ["isEstimated"] = x.IsEstimated, ["isPlayerVisible"] = x.IsPlayerVisible, ["publicSummary"] = x.PublicSummary };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> PaymentPayload(ManufacturingPaymentState x, bool admin)
    {
        var result = new Dictionary<string, object> { ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["paymentKind"] = x.PaymentKind, ["amount"] = x.Amount, ["currencyCode"] = x.CurrencyCode, ["status"] = x.Status, ["paidAtUtc"] = x.PaidAtUtc?.ToString("O") ?? string.Empty, ["isPlayerVisible"] = x.IsPlayerVisible, ["publicSummary"] = x.PublicSummary };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> ProgressPayload(ManufacturingProgressEntry x, bool admin) => new()
    {
        ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["stageId"] = x.StageId, ["progressDelta"] = x.ProgressDelta,
        ["publicSummary"] = x.PublicSummary, ["isPlayerVisible"] = x.IsPlayerVisible, ["createdAtUtc"] = x.CreatedAtUtc.ToString("O")
    };

    private static Dictionary<string, object> TestPlanPayload(ManufacturingTestPlanState x, bool admin)
    {
        var result = new Dictionary<string, object> { ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["name"] = x.Name, ["status"] = x.Status, ["publicSummary"] = x.PublicSummary, ["isPlayerVisible"] = x.IsPlayerVisible };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> TestResultPayload(ManufacturingTestResultState x, bool admin)
    {
        var result = new Dictionary<string, object> { ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["testPlanId"] = x.TestPlanId, ["result"] = x.Result, ["publicSummary"] = x.PublicSummary, ["isPlayerVisible"] = x.IsPlayerVisible };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> DefectPayload(ManufacturingDefectState x, bool admin)
    {
        var result = new Dictionary<string, object> { ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["severity"] = x.Severity, ["status"] = x.Status, ["isCritical"] = admin && x.IsCritical, ["publicSummary"] = x.PublicSummary, ["isPlayerVisible"] = x.IsPlayerVisible };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private static Dictionary<string, object> AcceptancePayload(ManufacturingAcceptanceState x, bool admin)
    {
        var result = new Dictionary<string, object> { ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["status"] = x.Status, ["acceptedWithDefects"] = x.AcceptedWithDefects, ["publicSummary"] = x.PublicSummary, ["isPlayerVisible"] = x.IsPlayerVisible, ["updatedAtUtc"] = x.UpdatedAtUtc.ToString("O") };
        if (admin) { result["gmOverride"] = x.GMOverride; result["gmNotes"] = x.GMNotes; }
        return result;
    }

    private static Dictionary<string, object> ManufacturedAssetPayload(ManufacturedAssetState x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = x.Id, ["manufacturingProjectId"] = x.ManufacturingProjectId, ["assetStateId"] = admin ? x.AssetStateId : string.Empty,
            ["name"] = x.Name, ["assetType"] = x.AssetType, ["blueprintId"] = x.BlueprintId, ["ownerEntityType"] = x.OwnerEntityType,
            ["ownerEntityId"] = x.OwnerEntityId, ["status"] = x.Status, ["isPlayerVisible"] = x.IsPlayerVisible,
            ["visibilityMode"] = x.VisibilityMode, ["publicSummary"] = x.PublicSummary, ["updatedAtUtc"] = x.UpdatedAtUtc.ToString("O")
        };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private string BuildDefectSummary(string projectId, bool admin)
    {
        var defects = _repositories.ManufacturingDefects.Find(Builders<ManufacturingDefectState>.Filter.Eq(x => x.ManufacturingProjectId, projectId)).Where(x => admin || x.IsPlayerVisible).ToList();
        if (defects.Count == 0) return string.Empty;
        var open = defects.Count(x => x.Status == ManufacturingDefectStatusIds.Open || x.Status == ManufacturingDefectStatusIds.ReworkRequired);
        var critical = admin ? defects.Count(x => x.IsCritical) : 0;
        return admin ? $"Дефекты: {defects.Count}, открыто: {open}, критичных: {critical}" : $"Замечания: {defects.Count}, открыто: {open}";
    }

    private bool ManufacturingBaseEnabled() => _featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingMvp));
    private bool ManufacturingAdminEnabled() => ManufacturingBaseEnabled() && _featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingAdminView));
    private bool ManufacturingPlayerEnabled() => ManufacturingBaseEnabled() && _featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingPlayerView));

    private ResponseEnvelope ManufacturingDisabled(string command)
    {
        _logger.Admin($"manufacturing.command.disabled command={command}");
        return Error("Manufacturing MVP is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private static FilterDefinition<T> ManufacturingCampaignFilter<T>(IDictionary<string, object> payload)
    {
        var filter = FilterDefinition<T>.Empty;
        var campaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<T>.Filter.Eq("CampaignId", campaignId);
        return filter;
    }

    private static FilterDefinition<ManufacturingProjectState> PlayerManufacturingProjectFilter(UserAccount actor)
        => Builders<ManufacturingProjectState>.Filter.Eq(x => x.IsPlayerVisible, true)
           & Builders<ManufacturingProjectState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.GmOnly)
           & Builders<ManufacturingProjectState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.Hidden)
           & Builders<ManufacturingProjectState>.Filter.Or(
               Builders<ManufacturingProjectState>.Filter.Eq(x => x.CustomerEntityId, actor.Id),
               Builders<ManufacturingProjectState>.Filter.Eq(x => x.OwnerEntityId, actor.Id),
               Builders<ManufacturingProjectState>.Filter.Eq(x => x.OwnerEntityId, string.Empty));

    private static FilterDefinition<ManufacturedAssetState> PlayerManufacturedAssetFilter(UserAccount actor)
        => Builders<ManufacturedAssetState>.Filter.Eq(x => x.IsPlayerVisible, true)
           & Builders<ManufacturedAssetState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.GmOnly)
           & Builders<ManufacturedAssetState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.Hidden)
           & Builders<ManufacturedAssetState>.Filter.Or(
               Builders<ManufacturedAssetState>.Filter.Eq(x => x.OwnerEntityId, actor.Id),
               Builders<ManufacturedAssetState>.Filter.Eq(x => x.OwnerEntityId, string.Empty));

    private static bool CanPlayerSeeManufacturingProject(ManufacturingProjectState project, UserAccount actor)
        => project.IsPlayerVisible
           && !project.IsArchived
           && !string.Equals(project.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(project.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase)
           && (string.IsNullOrWhiteSpace(project.CustomerEntityId) || project.CustomerEntityId == actor.Id || project.OwnerEntityId == actor.Id);

    private static bool CanPlayerSeeManufacturedAsset(ManufacturedAssetState asset)
        => asset.IsPlayerVisible
           && !string.Equals(asset.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(asset.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase);

    private static string InferManufacturingType(FactoryOrderState order)
    {
        if (!string.IsNullOrWhiteSpace(order.BlueprintId)) return ManufacturingTypeIds.VehicleBuild;
        if (!string.IsNullOrWhiteSpace(order.PresetId)) return ManufacturingTypeIds.BatchProduction;
        return ManufacturingTypeIds.Custom;
    }

    private static string InferManufacturingDomain(FactoryOrderState order)
    {
        if (string.Equals(order.SourceType, FactoryOrderSourceTypeIds.Blueprint, StringComparison.OrdinalIgnoreCase)) return ProductionDomainIds.VehicleManufacturing;
        return ProductionDomainIds.Custom;
    }

    private static string InferManufacturingOrderKind(FactoryOrderState order)
    {
        if (string.Equals(order.SourceType, FactoryOrderSourceTypeIds.Blueprint, StringComparison.OrdinalIgnoreCase)) return ManufacturingOrderKindIds.CustomBlueprintProduction;
        if (string.Equals(order.SourceType, FactoryOrderSourceTypeIds.Preset, StringComparison.OrdinalIgnoreCase)) return ManufacturingOrderKindIds.PresetProduction;
        return ManufacturingOrderKindIds.Custom;
    }

    private static string RequiredManufacturingText(IDictionary<string, object> payload, string key, string message)
    {
        var value = PayloadReader.GetString(payload, key);
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        throw new InvalidOperationException(message);
    }

    private static decimal PositiveManufacturingDecimal(IDictionary<string, object> payload, string key, decimal fallback)
    {
        var raw = PayloadReader.GetString(payload, key);
        if (string.IsNullOrWhiteSpace(raw)) return Math.Max(0m, fallback);
        return decimal.TryParse(raw, out var value) ? Math.Max(0m, value) : Math.Max(0m, fallback);
    }

    private void TryPublishManufacturingSync(string eventType, ManufacturingProjectState project, string operation, string actorId, string requestId)
    {
        if (!_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingSyncEvents))) return;
        TryPublishSyncEvent(eventType, project.CampaignId, "manufacturing_project", project.Id, operation, actorId, new Dictionary<string, object> { ["projectId"] = project.Id, ["status"] = project.ManufacturingStatus }, requestId);
    }

    private void TryWriteManufacturingJournal(ManufacturingProjectState project, string sourceEventId, string title, string actorId, bool playerVisible)
    {
        if (!_featureFlags.IsEnabled(nameof(ManufacturingFeatureFlags.UseManufacturingJournalIntegration))) return;
        if (!_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp)) || !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion))) return;
        _repositories.EventJournalEntries.Insert(new EventJournalEntryState
        {
            CampaignId = project.CampaignId,
            EntryType = EventJournalEntryTypeIds.Automatic,
            Category = EventJournalCategoryIds.Custom,
            Severity = EventJournalSeverityIds.Information,
            Title = title,
            Summary = project.Name,
            PlayerSummary = playerVisible ? project.Name : string.Empty,
            SourceModule = "manufacturing",
            SourceEventId = sourceEventId + ":" + project.Id,
            SourceEventType = sourceEventId,
            VisibilityMode = playerVisible ? EventJournalVisibilityModeIds.PlayerVisible : EventJournalVisibilityModeIds.GMOnly,
            IsPlayerVisible = playerVisible,
            IsAutomatic = true,
            ActorUserId = actorId,
            SubjectEntityType = "manufacturing_project",
            SubjectEntityId = project.Id,
            SubjectDisplayName = project.Name,
            CreatedByUserId = actorId,
            OccurredAtUtc = DateTime.UtcNow
        });
    }
}
