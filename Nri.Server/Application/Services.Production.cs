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
    public ResponseEnvelope ProductionFacilityDefinitionList(CommandContext context)
    {
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var filter = ProductionCampaignFilter<ProductionFacilityDefinition>(context.Request.Payload);
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<ProductionFacilityDefinition>.Filter.Eq(x => x.IsArchived, false);
        var items = _repositories.ProductionFacilityDefinitions.Find(filter).OrderBy(x => x.Name).Take(300).Select(x => (object)FacilityDefinitionPayload(x, true)).ToArray();
        return Ok("Production facility definitions loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProductionFacilityDefinitionCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseProductionFacilityDefinitions))) return ProductionDisabled(context.Request.Command);
        var item = BuildFacilityDefinition(context.Request.Payload, actor, null);
        _repositories.ProductionFacilityDefinitions.Insert(item);
        TryWriteProductionJournal(item.CampaignId, "production.facilityDefinition.created", "Создан тип производственной мощности", item.Name, actor.Id, false);
        return Ok("Production facility definition created.", new Dictionary<string, object> { { "item", FacilityDefinitionPayload(item, true) } });
    }

    public ResponseEnvelope ProductionFacilityDefinitionUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseProductionFacilityDefinitions))) return ProductionDisabled(context.Request.Command);
        var item = RequireFacilityDefinition(context);
        BuildFacilityDefinition(context.Request.Payload, actor, item);
        _repositories.ProductionFacilityDefinitions.Replace(item);
        return Ok("Production facility definition updated.", new Dictionary<string, object> { { "item", FacilityDefinitionPayload(item, true) } });
    }

    public ResponseEnvelope ProductionFacilityDefinitionArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var item = RequireFacilityDefinition(context);
        item.IsArchived = true;
        item.Archived = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedUtc = item.UpdatedAtUtc;
        _repositories.ProductionFacilityDefinitions.Replace(item);
        TryPublishProductionSync("production.facilityDefinition.changed", item.CampaignId, "production_facility_definition", item.Id, "archive", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Production facility definition archived.", new Dictionary<string, object> { { "item", FacilityDefinitionPayload(item, true) } });
    }

    public ResponseEnvelope ProductionFacilityList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        }
        else if (!ProductionPlayerEnabled())
        {
            return ProductionDisabled(context.Request.Command);
        }

        var filter = ProductionCampaignFilter<ProductionFacilityState>(context.Request.Payload);
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<ProductionFacilityState>.Filter.Eq(x => x.IsArchived, false);
        if (!IsAdmin(actor)) filter &= PlayerVisibleFilter<ProductionFacilityState>();
        var items = _repositories.ProductionFacilities.Find(filter).OrderBy(x => x.Name).Take(300).Select(x => (object)FacilityPayload(x, IsAdmin(actor))).ToArray();
        return Ok("Production facilities loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProductionPlayerFacilityList(CommandContext context) => ProductionFacilityList(context);

    public ResponseEnvelope ProductionFacilityGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var facility = RequireFacility(context);
        if (!IsAdmin(actor) && !CanPlayerSeeFacility(facility)) throw new UnauthorizedAccessException("Production facility is hidden.");
        var payload = FacilityPayload(facility, IsAdmin(actor));
        if (IsAdmin(actor))
        {
            payload["capabilities"] = _repositories.ProductionCapabilities.Find(Builders<ProductionFacilityCapabilityState>.Filter.Eq(x => x.FacilityId, facility.Id)).Select(x => (object)CapabilityPayload(x, true)).ToArray();
            payload["capacity"] = _repositories.ProductionCapacities.Find(Builders<ProductionFacilityCapacityState>.Filter.Eq(x => x.FacilityId, facility.Id)).Select(x => (object)CapacityPayload(x, true)).FirstOrDefault() ?? new Dictionary<string, object>();
        }
        return Ok("Production facility loaded.", new Dictionary<string, object> { { "item", payload } });
    }

    public ResponseEnvelope ProductionFacilityCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var item = BuildFacility(context.Request.Payload, actor, null);
        _repositories.ProductionFacilities.Insert(item);
        var capacity = EnsureCapacity(item, actor.Id);
        TryWriteProductionJournal(item.CampaignId, "production.facility.created", "Создана производственная мощность", item.Name, actor.Id, item.IsPlayerVisible);
        TryPublishProductionSync("production.facility.changed", item.CampaignId, "production_facility", item.Id, "create", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Production facility created.", new Dictionary<string, object> { { "item", FacilityPayload(item, true) }, { "capacity", CapacityPayload(capacity, true) } });
    }

    public ResponseEnvelope ProductionFacilityUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var item = RequireFacility(context);
        BuildFacility(context.Request.Payload, actor, item);
        _repositories.ProductionFacilities.Replace(item);
        TryPublishProductionSync("production.facility.changed", item.CampaignId, "production_facility", item.Id, "update", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Production facility updated.", new Dictionary<string, object> { { "item", FacilityPayload(item, true) } });
    }

    public ResponseEnvelope ProductionFacilityArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var item = RequireFacility(context);
        item.IsArchived = true;
        item.Archived = true;
        item.OperationalStatus = ProductionFacilityStatusIds.Archived;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedUtc = item.UpdatedAtUtc;
        item.UpdatedByUserId = actor.Id;
        _repositories.ProductionFacilities.Replace(item);
        return Ok("Production facility archived.", new Dictionary<string, object> { { "item", FacilityPayload(item, true) } });
    }

    public ResponseEnvelope ProductionCapabilityAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseProductionFacilityCapabilities))) return ProductionDisabled(context.Request.Command);
        var facility = RequireFacility(context);
        var item = BuildCapability(context.Request.Payload, facility, null);
        _repositories.ProductionCapabilities.Insert(item);
        TryPublishProductionSync("production.capability.changed", facility.CampaignId, "production_facility", facility.Id, "capability_add", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Production capability added.", new Dictionary<string, object> { { "item", CapabilityPayload(item, true) } });
    }

    public ResponseEnvelope ProductionCapabilityUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseProductionFacilityCapabilities))) return ProductionDisabled(context.Request.Command);
        var item = RequireCapability(context);
        var facility = _repositories.ProductionFacilities.GetById(item.FacilityId) ?? throw new InvalidOperationException("Production facility not found.");
        BuildCapability(context.Request.Payload, facility, item);
        _repositories.ProductionCapabilities.Replace(item);
        TryPublishProductionSync("production.capability.changed", item.CampaignId, "production_capability", item.Id, "capability_update", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Production capability updated.", new Dictionary<string, object> { { "item", CapabilityPayload(item, true) } });
    }

    public ResponseEnvelope ProductionCapabilityRemove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var item = RequireCapability(context);
        item.Archived = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.ProductionCapabilities.Replace(item);
        TryPublishProductionSync("production.capability.changed", item.CampaignId, "production_capability", item.Id, "capability_remove", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Production capability archived.", new Dictionary<string, object> { { "item", CapabilityPayload(item, true) } });
    }

    public ResponseEnvelope ProductionCapacityUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseProductionFacilityCapacity))) return ProductionDisabled(context.Request.Command);
        var facility = RequireFacility(context);
        var capacity = EnsureCapacity(facility, actor.Id);
        capacity.CapacityRating = PositiveInt(context.Request.Payload, "capacityRating", capacity.CapacityRating);
        capacity.MaxQueueSlots = Math.Max(1, PositiveInt(context.Request.Payload, "maxQueueSlots", capacity.MaxQueueSlots));
        capacity.ReservedQueueSlots = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "reservedQueueSlots") ?? capacity.ReservedQueueSlots);
        capacity.CurrentLoadPercent = ClampProduction(PayloadReader.GetInt(context.Request.Payload, "currentLoadPercent") ?? capacity.CurrentLoadPercent, 0, 200);
        capacity.CapacityNotes = PayloadReader.GetString(context.Request.Payload, "capacityNotes") ?? capacity.CapacityNotes;
        capacity.UpdatedAtUtc = DateTime.UtcNow;
        capacity.UpdatedByUserId = actor.Id;
        _repositories.ProductionCapacities.Replace(capacity);
        facility.CapacityRating = capacity.CapacityRating;
        facility.QueueLength = capacity.ReservedQueueSlots;
        facility.CurrentLoadPercent = capacity.CurrentLoadPercent;
        facility.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.ProductionFacilities.Replace(facility);
        return Ok("Production capacity updated.", new Dictionary<string, object> { { "item", CapacityPayload(capacity, true) }, { "facility", FacilityPayload(facility, true) } });
    }

    public ResponseEnvelope ProductionProcessList(CommandContext context)
    {
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var filter = ProductionCampaignFilter<ProductionProcessDefinition>(context.Request.Payload);
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<ProductionProcessDefinition>.Filter.Eq(x => x.IsArchived, false);
        var items = _repositories.ProductionProcesses.Find(filter).OrderBy(x => x.Name).Take(300).Select(x => (object)ProcessPayload(x, true)).ToArray();
        return Ok("Production processes loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProductionProcessCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var item = BuildProcess(context.Request.Payload, actor, null);
        _repositories.ProductionProcesses.Insert(item);
        return Ok("Production process created.", new Dictionary<string, object> { { "item", ProcessPayload(item, true) } });
    }

    public ResponseEnvelope ProductionProcessUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var item = RequireProcess(context);
        BuildProcess(context.Request.Payload, actor, item);
        _repositories.ProductionProcesses.Replace(item);
        return Ok("Production process updated.", new Dictionary<string, object> { { "item", ProcessPayload(item, true) } });
    }

    public ResponseEnvelope ProductionProcessArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var item = RequireProcess(context);
        item.IsArchived = true;
        item.Archived = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.ProductionProcesses.Replace(item);
        TryPublishProductionSync("production.process.changed", item.CampaignId, "production_process", item.Id, "archive", actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok("Production process archived.", new Dictionary<string, object> { { "item", ProcessPayload(item, true) } });
    }

    public ResponseEnvelope FactoryQuoteList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryQuotes))) return ProductionDisabled(context.Request.Command);
        }
        else if (!ProductionPlayerEnabled())
        {
            return ProductionDisabled(context.Request.Command);
        }

        var filter = ProductionCampaignFilter<FactoryQuoteState>(context.Request.Payload);
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<FactoryQuoteState>.Filter.Ne(x => x.Status, FactoryQuoteStatusIds.Archived);
        if (!IsAdmin(actor)) filter &= PlayerQuoteFilter(actor);
        var items = _repositories.FactoryQuotes.Find(filter).OrderByDescending(x => x.UpdatedAtUtc).Take(300).Select(x => (object)QuotePayload(x, IsAdmin(actor))).ToArray();
        return Ok("Factory quotes loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope FactoryPlayerQuoteList(CommandContext context) => FactoryQuoteList(context);

    public ResponseEnvelope FactoryQuoteGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var quote = RequireQuote(context);
        if (!IsAdmin(actor) && !CanPlayerSeeQuote(quote, actor)) throw new UnauthorizedAccessException("Factory quote is hidden.");
        return Ok("Factory quote loaded.", new Dictionary<string, object> { { "item", QuotePayload(quote, IsAdmin(actor)) } });
    }

    public ResponseEnvelope FactoryQuoteGenerate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryQuotes))) return ProductionDisabled(context.Request.Command);
        var quote = BuildQuote(context.Request.Payload, actor, null);
        _repositories.FactoryQuotes.Insert(quote);
        TryWriteProductionJournal(quote.CampaignId, "factory.quote.created", "Создана оценка производства", quote.Name, actor.Id, quote.IsPlayerVisible);
        return Ok("Factory quote generated.", new Dictionary<string, object> { { "item", QuotePayload(quote, true) } });
    }

    public ResponseEnvelope FactoryQuoteUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryQuotes))) return ProductionDisabled(context.Request.Command);
        var quote = RequireQuote(context);
        BuildQuote(context.Request.Payload, actor, quote);
        _repositories.FactoryQuotes.Replace(quote);
        return Ok("Factory quote updated.", new Dictionary<string, object> { { "item", QuotePayload(quote, true) } });
    }

    public ResponseEnvelope FactoryQuoteOffer(CommandContext context) => SetQuoteStatus(context, FactoryQuoteStatusIds.Offered, "Factory quote offered.");
    public ResponseEnvelope FactoryQuoteAccept(CommandContext context) => SetQuoteStatus(context, FactoryQuoteStatusIds.Accepted, "Factory quote accepted.");
    public ResponseEnvelope FactoryQuoteReject(CommandContext context) => SetQuoteStatus(context, FactoryQuoteStatusIds.Rejected, "Factory quote rejected.");
    public ResponseEnvelope FactoryQuoteExpire(CommandContext context) => SetQuoteStatus(context, FactoryQuoteStatusIds.Expired, "Factory quote expired.");
    public ResponseEnvelope FactoryQuoteArchive(CommandContext context) => SetQuoteStatus(context, FactoryQuoteStatusIds.Archived, "Factory quote archived.");

    public ResponseEnvelope FactoryPlayerQuoteAccept(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProductionPlayerEnabled()) return ProductionDisabled(context.Request.Command);
        var quote = RequireQuote(context);
        if (!CanPlayerSeeQuote(quote, actor) || !string.Equals(quote.Status, FactoryQuoteStatusIds.Offered, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Factory quote is not offered to this player.");
        quote.Status = FactoryQuoteStatusIds.Accepted;
        quote.UpdatedAtUtc = DateTime.UtcNow;
        quote.UpdatedByUserId = actor.Id;
        _repositories.FactoryQuotes.Replace(quote);
        return Ok("Factory quote accepted by player.", new Dictionary<string, object> { { "item", QuotePayload(quote, false) } });
    }

    public ResponseEnvelope FactoryPlayerQuoteReject(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProductionPlayerEnabled()) return ProductionDisabled(context.Request.Command);
        var quote = RequireQuote(context);
        if (!CanPlayerSeeQuote(quote, actor)) throw new UnauthorizedAccessException("Factory quote is hidden.");
        quote.Status = FactoryQuoteStatusIds.Rejected;
        quote.UpdatedAtUtc = DateTime.UtcNow;
        quote.UpdatedByUserId = actor.Id;
        _repositories.FactoryQuotes.Replace(quote);
        return Ok("Factory quote rejected by player.", new Dictionary<string, object> { { "item", QuotePayload(quote, false) } });
    }

    public ResponseEnvelope FactoryQuoteConvertToOrder(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrders))) return ProductionDisabled(context.Request.Command);
        var quote = RequireQuote(context);
        if (!string.Equals(quote.Status, FactoryQuoteStatusIds.Accepted, StringComparison.OrdinalIgnoreCase) && !PayloadReader.GetBool(context.Request.Payload, "gmOverride"))
            throw new InvalidOperationException("Quote must be accepted before conversion to order.");

        var order = BuildOrderFromQuote(quote, actor);
        _repositories.FactoryOrders.Insert(order);
        quote.Status = FactoryQuoteStatusIds.ConvertedToOrder;
        quote.UpdatedAtUtc = DateTime.UtcNow;
        quote.UpdatedByUserId = actor.Id;
        _repositories.FactoryQuotes.Replace(quote);
        var slot = ReserveQueueSlot(order, quote, actor.Id);
        EnsureProjectFoundationForFactoryOrder(order, actor);
        TryWriteProductionJournal(order.CampaignId, "factory.order.created", "Создан производственный заказ", order.Name, actor.Id, order.IsPlayerVisible);
        return Ok("Factory quote converted to order. Manufacturing was not started.", new Dictionary<string, object> { { "item", OrderPayload(order, true) }, { "queueSlot", QueuePayload(slot, true) } });
    }

    public ResponseEnvelope FactoryOrderList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (IsAdmin(actor))
        {
            if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrders))) return ProductionDisabled(context.Request.Command);
        }
        else if (!ProductionPlayerEnabled())
        {
            return ProductionDisabled(context.Request.Command);
        }

        var filter = ProductionCampaignFilter<FactoryOrderState>(context.Request.Payload);
        if (!PayloadReader.GetBool(context.Request.Payload, "includeArchived")) filter &= Builders<FactoryOrderState>.Filter.Ne(x => x.Status, FactoryOrderStatusIds.Archived);
        if (!IsAdmin(actor)) filter &= PlayerOrderFilter(actor);
        var items = _repositories.FactoryOrders.Find(filter).OrderByDescending(x => x.UpdatedAtUtc).Take(300).Select(x => (object)OrderPayload(x, IsAdmin(actor))).ToArray();
        return Ok("Factory orders loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope FactoryPlayerOrderList(CommandContext context) => FactoryOrderList(context);

    public ResponseEnvelope FactoryOrderGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var order = RequireOrder(context);
        if (!IsAdmin(actor) && !CanPlayerSeeOrder(order, actor)) throw new UnauthorizedAccessException("Factory order is hidden.");
        return Ok("Factory order loaded.", new Dictionary<string, object> { { "item", OrderPayload(order, IsAdmin(actor)) } });
    }

    public ResponseEnvelope FactoryOrderCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrders))) return ProductionDisabled(context.Request.Command);
        var quote = BuildQuote(context.Request.Payload, actor, null);
        quote.Status = FactoryQuoteStatusIds.Accepted;
        _repositories.FactoryQuotes.Insert(quote);
        var order = BuildOrderFromQuote(quote, actor);
        _repositories.FactoryOrders.Insert(order);
        var slot = ReserveQueueSlot(order, quote, actor.Id);
        EnsureProjectFoundationForFactoryOrder(order, actor);
        return Ok("Factory order created. Manufacturing was not started.", new Dictionary<string, object> { { "quote", QuotePayload(quote, true) }, { "item", OrderPayload(order, true) }, { "queueSlot", QueuePayload(slot, true) } });
    }

    public ResponseEnvelope FactoryPlayerOrderRequest(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProductionPlayerEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrderRequestIntegration))) return ProductionDisabled(context.Request.Command);
        var request = BuildProductionRequest(context, actor, PlayerRequestTypeIds.FactoryOrder, "Заявка на производственный заказ");
        _repositories.PlayerRequests.Insert(request);
        return Ok("Factory order request submitted.", new Dictionary<string, object> { { "requestId", request.Id }, { "requestNumber", request.RequestNumber }, { "item", PlayerRequestPayload(request, actor, includeAdminFields: false) } });
    }

    public ResponseEnvelope FactoryPlayerQuoteRequest(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ProductionPlayerEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrderRequestIntegration))) return ProductionDisabled(context.Request.Command);
        var request = BuildProductionRequest(context, actor, PlayerRequestTypeIds.FactoryQuote, "Заявка на оценку производства");
        _repositories.PlayerRequests.Insert(request);
        return Ok("Factory quote request submitted.", new Dictionary<string, object> { { "requestId", request.Id }, { "requestNumber", request.RequestNumber }, { "item", PlayerRequestPayload(request, actor, includeAdminFields: false) } });
    }

    public ResponseEnvelope FactoryOrderApprove(CommandContext context) => SetOrderStatus(context, FactoryOrderStatusIds.Approved, "Factory order approved.");
    public ResponseEnvelope FactoryOrderReject(CommandContext context) => SetOrderStatus(context, FactoryOrderStatusIds.Cancelled, "Factory order rejected.");
    public ResponseEnvelope FactoryOrderCancel(CommandContext context) => SetOrderStatus(context, FactoryOrderStatusIds.Cancelled, "Factory order cancelled.");
    public ResponseEnvelope FactoryOrderArchive(CommandContext context) => SetOrderStatus(context, FactoryOrderStatusIds.Archived, "Factory order archived.");

    public ResponseEnvelope FactoryOrderSchedule(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrderQueue))) return ProductionDisabled(context.Request.Command);
        var order = RequireOrder(context);
        order.Status = FactoryOrderStatusIds.WaitingManufacturing;
        order.ScheduledAtUtc = DateTime.UtcNow;
        order.UpdatedAtUtc = DateTime.UtcNow;
        order.UpdatedByUserId = actor.Id;
        _repositories.FactoryOrders.Replace(order);
        var quote = string.IsNullOrWhiteSpace(order.QuoteId) ? null : _repositories.FactoryQuotes.GetById(order.QuoteId);
        var slot = ReserveQueueSlot(order, quote, actor.Id);
        return Ok("Factory order scheduled. Manufacturing is waiting for 0.14.17.", new Dictionary<string, object> { { "item", OrderPayload(order, true) }, { "queueSlot", QueuePayload(slot, true) } });
    }

    public ResponseEnvelope FactoryQueueReserve(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrderQueue))) return ProductionDisabled(context.Request.Command);
        var order = RequireOrder(context);
        var quote = string.IsNullOrWhiteSpace(order.QuoteId) ? null : _repositories.FactoryQuotes.GetById(order.QuoteId);
        var slot = ReserveQueueSlot(order, quote, actor.Id);
        return Ok("Factory queue slot reserved.", new Dictionary<string, object> { { "item", QueuePayload(slot, true) } });
    }

    public ResponseEnvelope FactoryQueueList(CommandContext context)
    {
        if (!ProductionAdminEnabled()) return ProductionDisabled(context.Request.Command);
        var filter = ProductionCampaignFilter<ProductionQueueSlotState>(context.Request.Payload);
        var facilityId = PayloadReader.GetString(context.Request.Payload, "facilityId");
        if (!string.IsNullOrWhiteSpace(facilityId)) filter &= Builders<ProductionQueueSlotState>.Filter.Eq(x => x.FacilityId, facilityId);
        var items = _repositories.ProductionQueueSlots.Find(filter).OrderBy(x => x.QueuePosition).ThenByDescending(x => x.UpdatedAtUtc).Take(300).Select(x => (object)QueuePayload(x, true)).ToArray();
        return Ok("Factory queue loaded.", new Dictionary<string, object> { { "items", items } });
    }

    private ResponseEnvelope SetQuoteStatus(CommandContext context, string status, string message)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryQuotes))) return ProductionDisabled(context.Request.Command);
        var quote = RequireQuote(context);
        quote.Status = status;
        quote.UpdatedAtUtc = DateTime.UtcNow;
        quote.UpdatedByUserId = actor.Id;
        if (status == FactoryQuoteStatusIds.Offered) quote.OfferedAtUtc = DateTime.UtcNow;
        _repositories.FactoryQuotes.Replace(quote);
        TryPublishProductionSync("factory.quote.changed", quote.CampaignId, "factory_quote", quote.Id, status, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok(message, new Dictionary<string, object> { { "item", QuotePayload(quote, true) } });
    }

    private ResponseEnvelope SetOrderStatus(CommandContext context, string status, string message)
    {
        var actor = RequireAdmin(context);
        if (!ProductionAdminEnabled() || !_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrders))) return ProductionDisabled(context.Request.Command);
        var order = RequireOrder(context);
        order.Status = status;
        order.UpdatedAtUtc = DateTime.UtcNow;
        order.UpdatedByUserId = actor.Id;
        _repositories.FactoryOrders.Replace(order);
        TryPublishProductionSync("factory.order.changed", order.CampaignId, "factory_order", order.Id, status, actor.Id, context.Request.RequestId ?? string.Empty);
        return Ok(message, new Dictionary<string, object> { { "item", OrderPayload(order, true) } });
    }

    private ProductionFacilityDefinition BuildFacilityDefinition(IDictionary<string, object> payload, UserAccount actor, ProductionFacilityDefinition? item)
    {
        item ??= new ProductionFacilityDefinition();
        item.CampaignId = PayloadReader.GetString(payload, "campaignId") ?? item.CampaignId;
        item.RuleSetId = PayloadReader.GetString(payload, "ruleSetId") ?? item.RuleSetId;
        item.FacilityDefinitionId = FirstNonEmptyProduction(PayloadReader.GetString(payload, "facilityDefinitionId"), item.FacilityDefinitionId, item.Id);
        item.Name = RequiredText(payload, "name", item.Name, "Facility definition name is required.");
        item.Description = PayloadReader.GetString(payload, "description") ?? item.Description;
        item.FacilityCategory = NormalizeFacilityCategory(PayloadReader.GetString(payload, "facilityCategory") ?? item.FacilityCategory);
        item.FacilityType = NormalizeFacilityType(PayloadReader.GetString(payload, "facilityType") ?? item.FacilityType);
        item.SupportedProductionDomains = ProductionStringList(payload, "supportedProductionDomains", item.SupportedProductionDomains);
        item.SupportedPlatformCategories = ProductionStringList(payload, "supportedPlatformCategories", item.SupportedPlatformCategories);
        item.SupportedSizeClassIds = ProductionStringList(payload, "supportedSizeClassIds", item.SupportedSizeClassIds);
        item.SupportedModuleCategories = ProductionStringList(payload, "supportedModuleCategories", item.SupportedModuleCategories);
        item.SupportedProcessIds = ProductionStringList(payload, "supportedProcessIds", item.SupportedProcessIds);
        item.BaseQualityTier = PositiveInt(payload, "baseQualityTier", item.BaseQualityTier);
        item.BaseCapacityRating = PositiveInt(payload, "baseCapacityRating", item.BaseCapacityRating);
        item.BaseComplexityHandling = PositiveInt(payload, "baseComplexityHandling", item.BaseComplexityHandling);
        item.BaseSpecializationTags = ProductionStringList(payload, "baseSpecializationTags", item.BaseSpecializationTags);
        item.RequiredStaffSummary = PayloadReader.GetString(payload, "requiredStaffSummary") ?? item.RequiredStaffSummary;
        item.RequiredEquipmentSummary = PayloadReader.GetString(payload, "requiredEquipmentSummary") ?? item.RequiredEquipmentSummary;
        item.RequiredInfrastructureSummary = PayloadReader.GetString(payload, "requiredInfrastructureSummary") ?? item.RequiredInfrastructureSummary;
        item.BaseCostMultiplier = PositiveDecimal(payload, "baseCostMultiplier", item.BaseCostMultiplier);
        item.BaseTimeMultiplier = PositiveDecimal(payload, "baseTimeMultiplier", item.BaseTimeMultiplier);
        item.BaseRiskMultiplier = PositiveDecimal(payload, "baseRiskMultiplier", item.BaseRiskMultiplier);
        item.LegalCategoryHint = PayloadReader.GetString(payload, "legalCategoryHint") ?? item.LegalCategoryHint;
        if (payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        item.VisibilityMode = NormalizeProductionVisibility(PayloadReader.GetString(payload, "visibilityMode") ?? item.VisibilityMode);
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedUtc = item.UpdatedAtUtc;
        return item;
    }

    private ProductionFacilityState BuildFacility(IDictionary<string, object> payload, UserAccount actor, ProductionFacilityState? item)
    {
        item ??= new ProductionFacilityState { CreatedByUserId = actor.Id };
        item.CampaignId = PayloadReader.GetString(payload, "campaignId") ?? item.CampaignId;
        item.FacilityDefinitionId = PayloadReader.GetString(payload, "facilityDefinitionId") ?? item.FacilityDefinitionId;
        item.FacilityId = FirstNonEmptyProduction(PayloadReader.GetString(payload, "facilityId"), item.FacilityId, item.Id);
        item.Name = RequiredText(payload, "name", item.Name, "Facility name is required.");
        item.Description = PayloadReader.GetString(payload, "description") ?? item.Description;
        item.OwnerEntityType = PayloadReader.GetString(payload, "ownerEntityType") ?? item.OwnerEntityType;
        item.OwnerEntityId = PayloadReader.GetString(payload, "ownerEntityId") ?? item.OwnerEntityId;
        item.OperatorEntityType = PayloadReader.GetString(payload, "operatorEntityType") ?? item.OperatorEntityType;
        item.OperatorEntityId = PayloadReader.GetString(payload, "operatorEntityId") ?? item.OperatorEntityId;
        item.LocationEntityType = PayloadReader.GetString(payload, "locationEntityType") ?? item.LocationEntityType;
        item.LocationEntityId = PayloadReader.GetString(payload, "locationEntityId") ?? item.LocationEntityId;
        item.CountryId = PayloadReader.GetString(payload, "countryId") ?? item.CountryId;
        item.RegionId = PayloadReader.GetString(payload, "regionId") ?? item.RegionId;
        item.CityId = PayloadReader.GetString(payload, "cityId") ?? item.CityId;
        item.JurisdictionId = PayloadReader.GetString(payload, "jurisdictionId") ?? item.JurisdictionId;
        item.FacilityCategory = NormalizeFacilityCategory(PayloadReader.GetString(payload, "facilityCategory") ?? item.FacilityCategory);
        item.FacilityType = NormalizeFacilityType(PayloadReader.GetString(payload, "facilityType") ?? item.FacilityType);
        item.OperationalStatus = NormalizeFacilityStatus(PayloadReader.GetString(payload, "operationalStatus") ?? item.OperationalStatus);
        item.SupportedProductionDomains = ProductionStringList(payload, "supportedProductionDomains", item.SupportedProductionDomains);
        item.SupportedPlatformCategories = ProductionStringList(payload, "supportedPlatformCategories", item.SupportedPlatformCategories);
        item.SupportedSizeClassIds = ProductionStringList(payload, "supportedSizeClassIds", item.SupportedSizeClassIds);
        item.SupportedModuleCategories = ProductionStringList(payload, "supportedModuleCategories", item.SupportedModuleCategories);
        item.SupportedProcessIds = ProductionStringList(payload, "supportedProcessIds", item.SupportedProcessIds);
        item.QualityTier = PositiveInt(payload, "qualityTier", item.QualityTier);
        item.CapacityRating = PositiveInt(payload, "capacityRating", item.CapacityRating);
        item.ComplexityHandling = PositiveInt(payload, "complexityHandling", item.ComplexityHandling);
        item.SpecializationTags = ProductionStringList(payload, "specializationTags", item.SpecializationTags);
        item.CurrentLoadPercent = ClampProduction(PayloadReader.GetInt(payload, "currentLoadPercent") ?? item.CurrentLoadPercent, 0, 200);
        item.QueueLength = Math.Max(0, PayloadReader.GetInt(payload, "queueLength") ?? item.QueueLength);
        item.MaintenanceStatus = PayloadReader.GetString(payload, "maintenanceStatus") ?? item.MaintenanceStatus;
        item.StaffStatus = PayloadReader.GetString(payload, "staffStatus") ?? item.StaffStatus;
        item.EquipmentStatus = PayloadReader.GetString(payload, "equipmentStatus") ?? item.EquipmentStatus;
        item.ResourceAccessSummary = PayloadReader.GetString(payload, "resourceAccessSummary") ?? item.ResourceAccessSummary;
        item.LegalStatusHint = PayloadReader.GetString(payload, "legalStatusHint") ?? item.LegalStatusHint;
        item.DeFactoStatusHint = PayloadReader.GetString(payload, "deFactoStatusHint") ?? item.DeFactoStatusHint;
        item.FacilityLegalityModeHint = PayloadReader.GetString(payload, "facilityLegalityModeHint") ?? item.FacilityLegalityModeHint;
        item.RiskSummary = PayloadReader.GetString(payload, "riskSummary") ?? item.RiskSummary;
        item.GMHiddenTermsSummary = PayloadReader.GetString(payload, "gmHiddenTermsSummary") ?? item.GMHiddenTermsSummary;
        if (payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        item.VisibilityMode = NormalizeProductionVisibility(PayloadReader.GetString(payload, "visibilityMode") ?? item.VisibilityMode);
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedUtc = item.UpdatedAtUtc;
        item.UpdatedByUserId = actor.Id;
        return item;
    }

    private ProductionFacilityCapabilityState BuildCapability(IDictionary<string, object> payload, ProductionFacilityState facility, ProductionFacilityCapabilityState? item)
    {
        item ??= new ProductionFacilityCapabilityState();
        item.CampaignId = facility.CampaignId;
        item.FacilityId = facility.Id;
        item.CapabilityId = FirstNonEmptyProduction(PayloadReader.GetString(payload, "capabilityId"), item.CapabilityId, item.Id);
        item.ProductionDomain = NormalizeProductionDomain(PayloadReader.GetString(payload, "productionDomain") ?? item.ProductionDomain);
        item.SupportedPlatformCategories = ProductionStringList(payload, "supportedPlatformCategories", item.SupportedPlatformCategories);
        item.SupportedSizeClassIds = ProductionStringList(payload, "supportedSizeClassIds", item.SupportedSizeClassIds);
        item.SupportedModuleCategories = ProductionStringList(payload, "supportedModuleCategories", item.SupportedModuleCategories);
        item.SupportedProcessIds = ProductionStringList(payload, "supportedProcessIds", item.SupportedProcessIds);
        item.QualityTier = PositiveInt(payload, "qualityTier", item.QualityTier);
        item.CapacityRating = PositiveInt(payload, "capacityRating", item.CapacityRating);
        item.ComplexityHandling = PositiveInt(payload, "complexityHandling", item.ComplexityHandling);
        item.CostMultiplier = PositiveDecimal(payload, "costMultiplier", item.CostMultiplier);
        item.TimeMultiplier = PositiveDecimal(payload, "timeMultiplier", item.TimeMultiplier);
        item.RiskMultiplier = PositiveDecimal(payload, "riskMultiplier", item.RiskMultiplier);
        if (payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        item.PublicSummary = PayloadReader.GetString(payload, "publicSummary") ?? item.PublicSummary;
        item.GMSummary = PayloadReader.GetString(payload, "gmSummary") ?? item.GMSummary;
        item.UpdatedAtUtc = DateTime.UtcNow;
        return item;
    }

    private ProductionProcessDefinition BuildProcess(IDictionary<string, object> payload, UserAccount actor, ProductionProcessDefinition? item)
    {
        item ??= new ProductionProcessDefinition();
        item.CampaignId = PayloadReader.GetString(payload, "campaignId") ?? item.CampaignId;
        item.RuleSetId = PayloadReader.GetString(payload, "ruleSetId") ?? item.RuleSetId;
        item.ProcessId = FirstNonEmptyProduction(PayloadReader.GetString(payload, "processId"), item.ProcessId, item.Id);
        item.Name = RequiredText(payload, "name", item.Name, "Process name is required.");
        item.Description = PayloadReader.GetString(payload, "description") ?? item.Description;
        item.ProductionDomain = NormalizeProductionDomain(PayloadReader.GetString(payload, "productionDomain") ?? item.ProductionDomain);
        item.ComplexityTier = PositiveInt(payload, "complexityTier", item.ComplexityTier);
        item.BaseWorkPoints = PositiveInt(payload, "baseWorkPoints", item.BaseWorkPoints);
        item.BaseCostMultiplier = PositiveDecimal(payload, "baseCostMultiplier", item.BaseCostMultiplier);
        item.BaseTimeMultiplier = PositiveDecimal(payload, "baseTimeMultiplier", item.BaseTimeMultiplier);
        if (payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedUtc = item.UpdatedAtUtc;
        return item;
    }

    private FactoryQuoteState BuildQuote(IDictionary<string, object> payload, UserAccount actor, FactoryQuoteState? quote)
    {
        quote ??= new FactoryQuoteState { CreatedByUserId = actor.Id };
        quote.CampaignId = PayloadReader.GetString(payload, "campaignId") ?? quote.CampaignId;
        quote.FacilityId = PayloadReader.GetString(payload, "facilityId") ?? quote.FacilityId;
        quote.BlueprintId = PayloadReader.GetString(payload, "blueprintId") ?? quote.BlueprintId;
        quote.PresetId = PayloadReader.GetString(payload, "presetId") ?? quote.PresetId;
        quote.DraftId = PayloadReader.GetString(payload, "draftId") ?? quote.DraftId;
        quote.SourceType = NormalizeOrderSource(PayloadReader.GetString(payload, "sourceType") ?? InferOrderSource(quote));
        quote.RequestId = PayloadReader.GetString(payload, "requestId") ?? quote.RequestId;
        quote.OwnerUserId = PayloadReader.GetString(payload, "ownerUserId") ?? quote.OwnerUserId;
        quote.OwnerCharacterId = PayloadReader.GetString(payload, "ownerCharacterId") ?? quote.OwnerCharacterId;
        quote.QuoteId = FirstNonEmptyProduction(PayloadReader.GetString(payload, "quoteId"), quote.QuoteId, quote.Id);
        quote.Name = RequiredText(payload, "name", quote.Name, "Quote name is required.");
        var estimate = EstimateFactoryQuote(quote, payload);
        quote.EstimatedCost = PositiveDecimal(payload, "estimatedCost", estimate.cost);
        quote.EstimatedWorkPoints = PositiveInt(payload, "estimatedWorkPoints", estimate.workPoints);
        quote.EstimatedDays = PositiveInt(payload, "estimatedDays", estimate.days);
        quote.QueuePosition = Math.Max(0, PayloadReader.GetInt(payload, "queuePosition") ?? estimate.queuePosition);
        quote.RiskSummary = PayloadReader.GetString(payload, "riskSummary") ?? estimate.riskSummary;
        quote.PublicTermsSummary = PayloadReader.GetString(payload, "publicTermsSummary") ?? estimate.publicTerms;
        quote.GMTermsSummary = PayloadReader.GetString(payload, "gmTermsSummary") ?? quote.GMTermsSummary;
        quote.RequiredResourcesSummary = PayloadReader.GetString(payload, "requiredResourcesSummary") ?? estimate.resources;
        quote.RequiredPermitsSummary = PayloadReader.GetString(payload, "requiredPermitsSummary") ?? quote.RequiredPermitsSummary;
        quote.LegalStatusHint = PayloadReader.GetString(payload, "legalStatusHint") ?? quote.LegalStatusHint;
        quote.FacilityValidationStatus = estimate.validationStatus;
        quote.Warnings = estimate.warnings;
        if (payload.ContainsKey("isPlayerVisible")) quote.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        quote.VisibilityMode = NormalizeProductionVisibility(PayloadReader.GetString(payload, "visibilityMode") ?? quote.VisibilityMode);
        if (quote.Status == FactoryQuoteStatusIds.Draft) quote.Status = FactoryQuoteStatusIds.Generated;
        quote.UpdatedAtUtc = DateTime.UtcNow;
        quote.UpdatedUtc = quote.UpdatedAtUtc;
        quote.UpdatedByUserId = actor.Id;
        return quote;
    }

    private FactoryOrderState BuildOrderFromQuote(FactoryQuoteState quote, UserAccount actor)
    {
        var order = new FactoryOrderState
        {
            CampaignId = quote.CampaignId,
            QuoteId = quote.Id,
            FacilityId = quote.FacilityId,
            BlueprintId = quote.BlueprintId,
            PresetId = quote.PresetId,
            DraftId = quote.DraftId,
            SourceType = quote.SourceType,
            RequestId = quote.RequestId,
            OwnerUserId = quote.OwnerUserId,
            OwnerCharacterId = quote.OwnerCharacterId,
            Name = quote.Name,
            Status = FactoryOrderStatusIds.Scheduled,
            EstimatedCost = quote.EstimatedCost,
            EstimatedWorkPoints = quote.EstimatedWorkPoints,
            EstimatedDays = quote.EstimatedDays,
            PublicStatusSummary = "Заказ запланирован. Производственные стадии будут подключены в следующем этапе.",
            RequiredResourcesSummary = quote.RequiredResourcesSummary,
            LegalStatusHint = quote.LegalStatusHint,
            RiskSummary = quote.RiskSummary,
            IsPlayerVisible = quote.IsPlayerVisible,
            VisibilityMode = quote.VisibilityMode,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            ScheduledAtUtc = DateTime.UtcNow,
            EstimatedReadyUtc = DateTime.UtcNow.AddDays(Math.Max(1, quote.EstimatedDays))
        };
        order.OrderId = order.Id;
        return order;
    }

    private ProductionFacilityCapacityState EnsureCapacity(ProductionFacilityState facility, string actorId)
    {
        var existing = _repositories.ProductionCapacities.Find(Builders<ProductionFacilityCapacityState>.Filter.Eq(x => x.FacilityId, facility.Id)).FirstOrDefault();
        if (existing != null) return existing;
        var capacity = new ProductionFacilityCapacityState
        {
            CampaignId = facility.CampaignId,
            FacilityId = facility.Id,
            CapacityId = Guid.NewGuid().ToString("N"),
            CapacityRating = Math.Max(1, facility.CapacityRating),
            MaxQueueSlots = Math.Max(1, facility.CapacityRating),
            ReservedQueueSlots = Math.Max(0, facility.QueueLength),
            CurrentLoadPercent = ClampProduction(facility.CurrentLoadPercent, 0, 200),
            UpdatedByUserId = actorId
        };
        _repositories.ProductionCapacities.Insert(capacity);
        return capacity;
    }

    private ProductionQueueSlotState ReserveQueueSlot(FactoryOrderState order, FactoryQuoteState? quote, string actorId)
    {
        var existing = _repositories.ProductionQueueSlots.Find(Builders<ProductionQueueSlotState>.Filter.Eq(x => x.OrderId, order.Id)).FirstOrDefault();
        if (existing != null) return existing;
        var count = _repositories.ProductionQueueSlots.Find(Builders<ProductionQueueSlotState>.Filter.Eq(x => x.FacilityId, order.FacilityId)).Count;
        var slot = new ProductionQueueSlotState
        {
            CampaignId = order.CampaignId,
            FacilityId = order.FacilityId,
            QuoteId = quote?.Id ?? order.QuoteId,
            OrderId = order.Id,
            QueueSlotId = Guid.NewGuid().ToString("N"),
            Status = ProductionQueueSlotStatusIds.Reserved,
            QueuePosition = count + 1,
            EstimatedStartUtc = DateTime.UtcNow,
            EstimatedReadyUtc = order.EstimatedReadyUtc,
            PublicSummary = "Очередь зарезервирована. Производство начнётся после этапа manufacturing.",
            CreatedByUserId = actorId
        };
        _repositories.ProductionQueueSlots.Insert(slot);
        order.QueueSlotId = slot.Id;
        order.Status = FactoryOrderStatusIds.WaitingManufacturing;
        order.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.FactoryOrders.Replace(order);
        var facility = _repositories.ProductionFacilities.GetById(order.FacilityId);
        if (facility != null)
        {
            facility.QueueLength = count + 1;
            facility.CurrentLoadPercent = Math.Min(200, Math.Max(facility.CurrentLoadPercent, (count + 1) * 20));
            facility.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.ProductionFacilities.Replace(facility);
        }
        return slot;
    }

    private void EnsureProjectFoundationForFactoryOrder(FactoryOrderState order, UserAccount actor)
    {
        if (!_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrderProjectFoundationIntegration))) return;
        if (!_featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectFoundationMvp)) || !_featureFlags.IsEnabled(nameof(ProjectFoundationFeatureFlags.UseProjectBaseV1))) return;
        var project = new ProjectBaseState
        {
            CampaignId = order.CampaignId,
            ProjectType = ProjectTypeIds.FactoryOrder,
            Name = order.Name,
            PublicSummary = order.PublicStatusSummary,
            GMSummary = order.GMHiddenTermsSummary,
            Status = ProjectStatusIds.WaitingResources,
            ProgressMode = ProjectProgressModeIds.WorkPoints,
            ResultApplicationMode = ProjectResultApplicationModeIds.None,
            WorkPointsRequired = order.EstimatedWorkPoints,
            OwnerUserId = order.OwnerUserId,
            OwnerCharacterId = order.OwnerCharacterId,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            IsPlayerVisible = order.IsPlayerVisible,
            VisibilityMode = order.VisibilityMode,
            ProposalPayload = new Dictionary<string, object> { { "factoryOrderId", order.Id }, { "quoteId", order.QuoteId }, { "blueprintId", order.BlueprintId }, { "boundary", "0.14.16 order only; manufacturing stages disabled" } }
        };
        _repositories.Projects.Insert(project);
        order.ProjectBaseId = project.Id;
        _repositories.FactoryOrders.Replace(order);
    }

    private PlayerRequestState BuildProductionRequest(CommandContext context, UserAccount actor, string type, string title)
    {
        var payload = context.Request.Payload;
        var summary = PayloadReader.GetString(payload, "summary") ?? PayloadReader.GetString(payload, "description") ?? title;
        return new PlayerRequestState
        {
            RequestNumber = NextPlayerRequestNumber(),
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
            SessionId = PayloadReader.GetString(payload, "sessionId") ?? string.Empty,
            GroupId = PayloadReader.GetString(payload, "groupId") ?? string.Empty,
            CharacterId = PayloadReader.GetString(payload, "characterId") ?? string.Empty,
            CreatedByUserId = actor.Id,
            CreatedByDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            RequestType = type,
            Title = PayloadReader.GetString(payload, "title") ?? title,
            Description = summary,
            Status = PlayerRequestStatusIds.Submitted,
            VisibilityMode = "party",
            IsPlayerVisible = true,
            LinkedEntityType = "factory_request",
            LinkedEntityId = PayloadReader.GetString(payload, "blueprintId") ?? PayloadReader.GetString(payload, "presetId") ?? string.Empty,
            ProposalType = type,
            ProposalPayloadSummary = summary,
            SubmittedAtUtc = DateTime.UtcNow,
            ProposalPayload = new PlayerRequestProposalDraft
            {
                ProposalType = type,
                DisplaySummary = summary,
                EstimatedResult = "GM может подготовить quote/order. Производство и выдача техники не выполняются в 0.14.16.",
                RequiresGMApproval = true,
                Parameters = new Dictionary<string, object>
                {
                    { "facilityId", PayloadReader.GetString(payload, "facilityId") ?? string.Empty },
                    { "blueprintId", PayloadReader.GetString(payload, "blueprintId") ?? string.Empty },
                    { "presetId", PayloadReader.GetString(payload, "presetId") ?? string.Empty },
                    { "sourceType", PayloadReader.GetString(payload, "sourceType") ?? string.Empty }
                }
            }
        };
    }

    private (decimal cost, int workPoints, int days, int queuePosition, string validationStatus, string publicTerms, string riskSummary, string resources, List<string> warnings) EstimateFactoryQuote(FactoryQuoteState quote, IDictionary<string, object> payload)
    {
        var warnings = new List<string>();
        var facility = string.IsNullOrWhiteSpace(quote.FacilityId) ? null : _repositories.ProductionFacilities.GetById(quote.FacilityId);
        var blueprint = string.IsNullOrWhiteSpace(quote.BlueprintId) ? null : _repositories.EngineeringBlueprints.GetById(quote.BlueprintId);
        if (facility == null) warnings.Add("Производственная мощность не выбрана или не найдена.");
        if (!string.IsNullOrWhiteSpace(quote.BlueprintId) && blueprint == null) warnings.Add("Engineering Blueprint не найден.");

        var baseCost = 1000m;
        if (blueprint != null)
        {
            baseCost += Math.Max(0, blueprint.ModuleIds.Count) * 250m;
            if (!string.IsNullOrWhiteSpace(blueprint.PlatformId)) baseCost += 1500m;
        }
        if (!string.IsNullOrWhiteSpace(quote.PresetId)) baseCost *= 0.9m;
        if (string.Equals(quote.SourceType, FactoryOrderSourceTypeIds.Custom, StringComparison.OrdinalIgnoreCase)) baseCost *= 1.35m;

        var costMultiplier = facility == null ? 1.25m : Math.Max(0.25m, 1.25m - facility.QualityTier * 0.05m);
        var complexity = facility?.ComplexityHandling ?? 1;
        var workPoints = Math.Max(50, 100 + (blueprint?.ModuleIds.Count ?? 0) * 25);
        var days = Math.Max(1, (int)Math.Ceiling(workPoints / (decimal)Math.Max(1, facility?.CapacityRating ?? 1) / 20m));
        var validation = warnings.Count == 0 ? FactoryValidationStatusIds.Valid : FactoryValidationStatusIds.GmReview;
        if (facility != null && !string.Equals(facility.OperationalStatus, ProductionFacilityStatusIds.Active, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Мощность не активна; требуется решение GM.");
            validation = FactoryValidationStatusIds.GmReview;
        }

        var publicTerms = "Предварительная оценка. Quote не запускает производство.";
        var risk = warnings.Count == 0 ? "Базовые риски не выявлены." : string.Join("; ", warnings.Take(3));
        var resources = "Материалы и стоимость являются оценкой; автоматического списания нет.";
        return (Math.Round(baseCost * costMultiplier, 2), workPoints * Math.Max(1, complexity), days, facility?.QueueLength + 1 ?? 0, validation, publicTerms, risk, resources, warnings);
    }

    private static Dictionary<string, object> FacilityDefinitionPayload(ProductionFacilityDefinition x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "facilityDefinitionId", x.FacilityDefinitionId }, { "campaignId", x.CampaignId }, { "ruleSetId", x.RuleSetId },
            { "name", x.Name }, { "description", x.Description }, { "facilityCategory", x.FacilityCategory }, { "facilityType", x.FacilityType },
            { "supportedProductionDomains", x.SupportedProductionDomains.ToArray() }, { "supportedPlatformCategories", x.SupportedPlatformCategories.ToArray() },
            { "supportedSizeClassIds", x.SupportedSizeClassIds.ToArray() }, { "supportedModuleCategories", x.SupportedModuleCategories.ToArray() },
            { "supportedProcessIds", x.SupportedProcessIds.ToArray() }, { "baseQualityTier", x.BaseQualityTier }, { "baseCapacityRating", x.BaseCapacityRating },
            { "baseComplexityHandling", x.BaseComplexityHandling }, { "baseCostMultiplier", x.BaseCostMultiplier }, { "baseTimeMultiplier", x.BaseTimeMultiplier },
            { "baseRiskMultiplier", x.BaseRiskMultiplier }, { "legalCategoryHint", x.LegalCategoryHint }, { "isPlayerVisible", x.IsPlayerVisible },
            { "visibilityMode", x.VisibilityMode }, { "isArchived", x.IsArchived }
        };
        if (admin)
        {
            result["requiredStaffSummary"] = x.RequiredStaffSummary;
            result["requiredEquipmentSummary"] = x.RequiredEquipmentSummary;
            result["requiredInfrastructureSummary"] = x.RequiredInfrastructureSummary;
        }
        return result;
    }

    private static Dictionary<string, object> FacilityPayload(ProductionFacilityState x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "facilityId", x.FacilityId }, { "campaignId", x.CampaignId }, { "facilityDefinitionId", x.FacilityDefinitionId },
            { "name", x.Name }, { "description", x.Description }, { "facilityCategory", x.FacilityCategory }, { "facilityType", x.FacilityType },
            { "operationalStatus", x.OperationalStatus }, { "locationEntityType", x.LocationEntityType }, { "locationEntityId", x.LocationEntityId },
            { "supportedProductionDomains", x.SupportedProductionDomains.ToArray() }, { "supportedPlatformCategories", x.SupportedPlatformCategories.ToArray() },
            { "supportedSizeClassIds", x.SupportedSizeClassIds.ToArray() }, { "supportedModuleCategories", x.SupportedModuleCategories.ToArray() },
            { "supportedProcessIds", x.SupportedProcessIds.ToArray() }, { "qualityTier", x.QualityTier }, { "capacityRating", x.CapacityRating },
            { "complexityHandling", x.ComplexityHandling }, { "currentLoadPercent", x.CurrentLoadPercent }, { "queueLength", x.QueueLength },
            { "maintenanceStatus", x.MaintenanceStatus }, { "staffStatus", x.StaffStatus }, { "equipmentStatus", x.EquipmentStatus },
            { "resourceAccessSummary", x.ResourceAccessSummary }, { "legalStatusHint", x.LegalStatusHint }, { "facilityLegalityModeHint", x.FacilityLegalityModeHint },
            { "riskSummary", x.RiskSummary }, { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", x.VisibilityMode }, { "isArchived", x.IsArchived }
        };
        if (admin)
        {
            result["ownerEntityType"] = x.OwnerEntityType;
            result["ownerEntityId"] = x.OwnerEntityId;
            result["operatorEntityType"] = x.OperatorEntityType;
            result["operatorEntityId"] = x.OperatorEntityId;
            result["jurisdictionId"] = x.JurisdictionId;
            result["deFactoStatusHint"] = x.DeFactoStatusHint;
            result["gmHiddenTermsSummary"] = x.GMHiddenTermsSummary;
        }
        return result;
    }

    private static Dictionary<string, object> CapabilityPayload(ProductionFacilityCapabilityState x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "capabilityId", x.CapabilityId }, { "campaignId", x.CampaignId }, { "facilityId", x.FacilityId },
            { "productionDomain", x.ProductionDomain }, { "supportedPlatformCategories", x.SupportedPlatformCategories.ToArray() },
            { "supportedSizeClassIds", x.SupportedSizeClassIds.ToArray() }, { "supportedModuleCategories", x.SupportedModuleCategories.ToArray() },
            { "supportedProcessIds", x.SupportedProcessIds.ToArray() }, { "qualityTier", x.QualityTier }, { "capacityRating", x.CapacityRating },
            { "complexityHandling", x.ComplexityHandling }, { "costMultiplier", x.CostMultiplier }, { "timeMultiplier", x.TimeMultiplier },
            { "riskMultiplier", x.RiskMultiplier }, { "isPlayerVisible", x.IsPlayerVisible }, { "publicSummary", x.PublicSummary }
        };
        if (admin) result["gmSummary"] = x.GMSummary;
        return result;
    }

    private static Dictionary<string, object> CapacityPayload(ProductionFacilityCapacityState x, bool admin) => new()
    {
        { "id", x.Id }, { "capacityId", x.CapacityId }, { "campaignId", x.CampaignId }, { "facilityId", x.FacilityId },
        { "capacityRating", x.CapacityRating }, { "maxQueueSlots", x.MaxQueueSlots }, { "reservedQueueSlots", x.ReservedQueueSlots },
        { "currentLoadPercent", x.CurrentLoadPercent }, { "nextAvailableWorldDateTime", x.NextAvailableWorldDateTime?.ToString("O") ?? string.Empty },
        { "capacityNotes", x.CapacityNotes }
    };

    private static Dictionary<string, object> ProcessPayload(ProductionProcessDefinition x, bool admin) => new()
    {
        { "id", x.Id }, { "processId", x.ProcessId }, { "campaignId", x.CampaignId }, { "ruleSetId", x.RuleSetId },
        { "name", x.Name }, { "productionDomain", x.ProductionDomain }, { "description", x.Description }, { "complexityTier", x.ComplexityTier },
        { "baseWorkPoints", x.BaseWorkPoints }, { "baseCostMultiplier", x.BaseCostMultiplier }, { "baseTimeMultiplier", x.BaseTimeMultiplier },
        { "isPlayerVisible", x.IsPlayerVisible }, { "isArchived", x.IsArchived }
    };

    private static Dictionary<string, object> QuotePayload(FactoryQuoteState x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "quoteId", x.QuoteId }, { "campaignId", x.CampaignId }, { "facilityId", x.FacilityId },
            { "blueprintId", x.BlueprintId }, { "presetId", x.PresetId }, { "draftId", x.DraftId }, { "sourceType", x.SourceType },
            { "requestId", x.RequestId }, { "ownerUserId", x.OwnerUserId }, { "ownerCharacterId", x.OwnerCharacterId }, { "status", x.Status },
            { "name", x.Name }, { "estimatedCost", x.EstimatedCost }, { "estimatedWorkPoints", x.EstimatedWorkPoints }, { "estimatedDays", x.EstimatedDays },
            { "queuePosition", x.QueuePosition }, { "riskSummary", x.RiskSummary }, { "publicTermsSummary", x.PublicTermsSummary },
            { "requiredResourcesSummary", x.RequiredResourcesSummary }, { "requiredPermitsSummary", x.RequiredPermitsSummary }, { "legalStatusHint", x.LegalStatusHint },
            { "facilityValidationStatus", x.FacilityValidationStatus }, { "warnings", x.Warnings.ToArray() }, { "isPlayerVisible", x.IsPlayerVisible },
            { "visibilityMode", x.VisibilityMode }, { "offeredAtUtc", x.OfferedAtUtc?.ToString("O") ?? string.Empty }, { "validUntilUtc", x.ValidUntilUtc?.ToString("O") ?? string.Empty },
            { "boundary", "Quote is an estimate only; it does not start production." }
        };
        if (admin) result["gmTermsSummary"] = x.GMTermsSummary;
        return result;
    }

    private static Dictionary<string, object> OrderPayload(FactoryOrderState x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "orderId", x.OrderId }, { "campaignId", x.CampaignId }, { "quoteId", x.QuoteId }, { "facilityId", x.FacilityId },
            { "queueSlotId", x.QueueSlotId }, { "blueprintId", x.BlueprintId }, { "presetId", x.PresetId }, { "draftId", x.DraftId },
            { "sourceType", x.SourceType }, { "projectBaseId", x.ProjectBaseId }, { "requestId", x.RequestId }, { "ownerUserId", x.OwnerUserId },
            { "ownerCharacterId", x.OwnerCharacterId }, { "name", x.Name }, { "status", x.Status }, { "estimatedCost", x.EstimatedCost },
            { "estimatedWorkPoints", x.EstimatedWorkPoints }, { "estimatedDays", x.EstimatedDays }, { "publicStatusSummary", x.PublicStatusSummary },
            { "requiredResourcesSummary", x.RequiredResourcesSummary }, { "legalStatusHint", x.LegalStatusHint }, { "riskSummary", x.RiskSummary },
            { "isPlayerVisible", x.IsPlayerVisible }, { "visibilityMode", x.VisibilityMode }, { "scheduledAtUtc", x.ScheduledAtUtc?.ToString("O") ?? string.Empty },
            { "estimatedReadyUtc", x.EstimatedReadyUtc?.ToString("O") ?? string.Empty },
            { "boundary", "Order stops at scheduled/waiting_manufacturing; no asset or resource consumption is created." }
        };
        if (admin) result["gmHiddenTermsSummary"] = x.GMHiddenTermsSummary;
        return result;
    }

    private static Dictionary<string, object> QueuePayload(ProductionQueueSlotState x, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            { "id", x.Id }, { "queueSlotId", x.QueueSlotId }, { "campaignId", x.CampaignId }, { "facilityId", x.FacilityId },
            { "quoteId", x.QuoteId }, { "orderId", x.OrderId }, { "status", x.Status }, { "queuePosition", x.QueuePosition },
            { "estimatedStartUtc", x.EstimatedStartUtc?.ToString("O") ?? string.Empty }, { "estimatedReadyUtc", x.EstimatedReadyUtc?.ToString("O") ?? string.Empty },
            { "publicSummary", x.PublicSummary }
        };
        if (admin) result["gmNotes"] = x.GMNotes;
        return result;
    }

    private ProductionFacilityDefinition RequireFacilityDefinition(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "id") ?? PayloadReader.GetString(context.Request.Payload, "facilityDefinitionId") ?? string.Empty;
        return _repositories.ProductionFacilityDefinitions.GetById(id) ?? throw new InvalidOperationException("Production facility definition not found.");
    }

    private ProductionFacilityState RequireFacility(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "id") ?? PayloadReader.GetString(context.Request.Payload, "facilityId") ?? string.Empty;
        return _repositories.ProductionFacilities.GetById(id) ?? throw new InvalidOperationException("Production facility not found.");
    }

    private ProductionFacilityCapabilityState RequireCapability(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "id") ?? PayloadReader.GetString(context.Request.Payload, "capabilityId") ?? string.Empty;
        return _repositories.ProductionCapabilities.GetById(id) ?? throw new InvalidOperationException("Production capability not found.");
    }

    private ProductionProcessDefinition RequireProcess(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "id") ?? PayloadReader.GetString(context.Request.Payload, "processId") ?? string.Empty;
        return _repositories.ProductionProcesses.GetById(id) ?? throw new InvalidOperationException("Production process not found.");
    }

    private FactoryQuoteState RequireQuote(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "id") ?? PayloadReader.GetString(context.Request.Payload, "quoteId") ?? string.Empty;
        return _repositories.FactoryQuotes.GetById(id) ?? throw new InvalidOperationException("Factory quote not found.");
    }

    private FactoryOrderState RequireOrder(CommandContext context)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "id") ?? PayloadReader.GetString(context.Request.Payload, "orderId") ?? string.Empty;
        return _repositories.FactoryOrders.GetById(id) ?? throw new InvalidOperationException("Factory order not found.");
    }

    private ResponseEnvelope ProductionDisabled(string command)
    {
        _logger.Admin($"production.command.disabled command={command}");
        return Error("Production/factory MVP is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool ProductionBaseEnabled() => _featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseProductionFacilitiesMvp));
    private bool ProductionAdminEnabled() => ProductionBaseEnabled() && _featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrderAdminView));
    private bool ProductionPlayerEnabled() => ProductionBaseEnabled() && _featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrderPlayerView));

    private static FilterDefinition<T> ProductionCampaignFilter<T>(IDictionary<string, object> payload)
    {
        var filter = FilterDefinition<T>.Empty;
        var campaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(campaignId)) filter &= Builders<T>.Filter.Eq("CampaignId", campaignId);
        return filter;
    }

    private static bool CanPlayerSeeFacility(ProductionFacilityState item)
        => item.IsPlayerVisible && !item.IsArchived && !string.Equals(item.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase) && !string.Equals(item.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase);

    private static bool CanPlayerSeeQuote(FactoryQuoteState quote, UserAccount actor)
        => quote.IsPlayerVisible && !string.Equals(quote.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase) && !string.Equals(quote.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(quote.OwnerUserId) || quote.OwnerUserId == actor.Id);

    private static bool CanPlayerSeeOrder(FactoryOrderState order, UserAccount actor)
        => order.IsPlayerVisible && !string.Equals(order.VisibilityMode, ProjectVisibilityModeIds.GmOnly, StringComparison.OrdinalIgnoreCase) && !string.Equals(order.VisibilityMode, ProjectVisibilityModeIds.Hidden, StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(order.OwnerUserId) || order.OwnerUserId == actor.Id);

    private static FilterDefinition<FactoryQuoteState> PlayerQuoteFilter(UserAccount actor)
        => Builders<FactoryQuoteState>.Filter.Eq(x => x.IsPlayerVisible, true)
           & Builders<FactoryQuoteState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.GmOnly)
           & Builders<FactoryQuoteState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.Hidden)
           & Builders<FactoryQuoteState>.Filter.Or(Builders<FactoryQuoteState>.Filter.Eq(x => x.OwnerUserId, actor.Id), Builders<FactoryQuoteState>.Filter.Eq(x => x.OwnerUserId, string.Empty));

    private static FilterDefinition<FactoryOrderState> PlayerOrderFilter(UserAccount actor)
        => Builders<FactoryOrderState>.Filter.Eq(x => x.IsPlayerVisible, true)
           & Builders<FactoryOrderState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.GmOnly)
           & Builders<FactoryOrderState>.Filter.Ne(x => x.VisibilityMode, ProjectVisibilityModeIds.Hidden)
           & Builders<FactoryOrderState>.Filter.Or(Builders<FactoryOrderState>.Filter.Eq(x => x.OwnerUserId, actor.Id), Builders<FactoryOrderState>.Filter.Eq(x => x.OwnerUserId, string.Empty));

    private static string NormalizeFacilityCategory(string? value) => ProductionAllow(value, ProductionFacilityCategoryIds.Custom,
        ProductionFacilityCategoryIds.Workshop, ProductionFacilityCategoryIds.Laboratory, ProductionFacilityCategoryIds.Forge, ProductionFacilityCategoryIds.AlchemyLab,
        ProductionFacilityCategoryIds.EngineeringWorkshop, ProductionFacilityCategoryIds.VehicleGarage, ProductionFacilityCategoryIds.SmallShipyard, ProductionFacilityCategoryIds.Shipyard,
        ProductionFacilityCategoryIds.Drydock, ProductionFacilityCategoryIds.OrbitalDock, ProductionFacilityCategoryIds.SpaceShipyard, ProductionFacilityCategoryIds.AssemblyLine,
        ProductionFacilityCategoryIds.Factory, ProductionFacilityCategoryIds.MilitaryFactory, ProductionFacilityCategoryIds.ResearchFactory, ProductionFacilityCategoryIds.Custom);

    private static string NormalizeFacilityType(string? value) => ProductionAllow(value, ProductionFacilityTypeIds.Custom,
        ProductionFacilityTypeIds.SmallPrivate, ProductionFacilityTypeIds.Guild, ProductionFacilityTypeIds.StateOwned, ProductionFacilityTypeIds.Corporate,
        ProductionFacilityTypeIds.Military, ProductionFacilityTypeIds.BlackMarketFuture, ProductionFacilityTypeIds.Mobile, ProductionFacilityTypeIds.Temporary, ProductionFacilityTypeIds.Custom);

    private static string NormalizeProductionDomain(string? value) => ProductionAllow(value, ProductionDomainIds.Custom,
        ProductionDomainIds.Crafting, ProductionDomainIds.EngineeringDesignSupport, ProductionDomainIds.ComponentManufacturing, ProductionDomainIds.VehicleManufacturing,
        ProductionDomainIds.Shipbuilding, ProductionDomainIds.SpaceshipConstruction, ProductionDomainIds.Repair, ProductionDomainIds.Modification,
        ProductionDomainIds.Prototype, ProductionDomainIds.BatchProduction, ProductionDomainIds.Custom);

    private static string NormalizeFacilityStatus(string? value) => ProductionAllow(value, ProductionFacilityStatusIds.Planned,
        ProductionFacilityStatusIds.Planned, ProductionFacilityStatusIds.Active, ProductionFacilityStatusIds.Overloaded, ProductionFacilityStatusIds.Maintenance,
        ProductionFacilityStatusIds.Damaged, ProductionFacilityStatusIds.Inactive, ProductionFacilityStatusIds.Closed, ProductionFacilityStatusIds.Hidden, ProductionFacilityStatusIds.Archived);

    private static string NormalizeOrderSource(string? value) => ProductionAllow(value, FactoryOrderSourceTypeIds.Custom, FactoryOrderSourceTypeIds.Blueprint, FactoryOrderSourceTypeIds.Preset, FactoryOrderSourceTypeIds.Custom);

    private static string NormalizeProductionVisibility(string? value)
        => ProductionAllow(value, ProjectVisibilityModeIds.PlayerVisible, ProjectVisibilityModeIds.GmOnly, ProjectVisibilityModeIds.PlayerVisible, ProjectVisibilityModeIds.Party, ProjectVisibilityModeIds.OwnerOnly, ProjectVisibilityModeIds.Hidden);

    private static string ProductionAllow(string? value, string fallback, params string[] allowed)
    {
        var text = (value ?? string.Empty).Trim();
        return allowed.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : fallback;
    }

    private static string InferOrderSource(FactoryQuoteState quote)
    {
        if (!string.IsNullOrWhiteSpace(quote.BlueprintId)) return FactoryOrderSourceTypeIds.Blueprint;
        if (!string.IsNullOrWhiteSpace(quote.PresetId)) return FactoryOrderSourceTypeIds.Preset;
        return FactoryOrderSourceTypeIds.Custom;
    }

    private static string RequiredText(IDictionary<string, object> payload, string key, string current, string message)
    {
        var value = PayloadReader.GetString(payload, key);
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        if (!string.IsNullOrWhiteSpace(current)) return current;
        throw new InvalidOperationException(message);
    }

    private static string FirstNonEmptyProduction(params string?[] values)
        => values.Select(x => (x ?? string.Empty).Trim()).FirstOrDefault(x => x.Length > 0) ?? string.Empty;

    private static int PositiveInt(IDictionary<string, object> payload, string key, int fallback)
        => Math.Max(1, PayloadReader.GetInt(payload, key) ?? fallback);

    private static decimal PositiveDecimal(IDictionary<string, object> payload, string key, decimal fallback)
    {
        var raw = PayloadReader.GetString(payload, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback <= 0 ? 1m : fallback;
        decimal value;
        return decimal.TryParse(raw, out value) ? Math.Max(0.01m, value) : Math.Max(0.01m, fallback);
    }

    private static int ClampProduction(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

    private static List<string> ProductionStringList(IDictionary<string, object> payload, string key, List<string> fallback)
    {
        var list = PayloadReader.GetList(payload, key);
        if (list != null) return list.Select(x => Convert.ToString(x) ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).Take(100).ToList();
        var csv = PayloadReader.GetString(payload, key);
        if (string.IsNullOrWhiteSpace(csv)) return fallback;
        return csv.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Take(100).ToList();
    }

    private void TryPublishProductionSync(string eventType, string campaignId, string entityType, string entityId, string operation, string actorId, string requestId)
    {
        if (!_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrderSyncEvents))) return;
        TryPublishSyncEvent(eventType, campaignId, entityType, entityId, operation, actorId, new Dictionary<string, object> { { "entityType", entityType }, { "entityId", entityId } }, requestId);
    }

    private void TryWriteProductionJournal(string campaignId, string sourceEventId, string title, string subjectName, string actorId, bool playerVisible)
    {
        if (!_featureFlags.IsEnabled(nameof(ProductionFeatureFlags.UseFactoryOrderJournalIntegration))) return;
        if (!_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp)) || !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion))) return;
        _repositories.EventJournalEntries.Insert(new EventJournalEntryState
        {
            CampaignId = campaignId,
            EntryType = EventJournalEntryTypeIds.Automatic,
            Category = EventJournalCategoryIds.Custom,
            Severity = EventJournalSeverityIds.Information,
            Title = title,
            Summary = subjectName,
            PlayerSummary = playerVisible ? subjectName : string.Empty,
            SourceModule = "production",
            SourceEventId = sourceEventId + ":" + subjectName,
            SourceEventType = sourceEventId,
            VisibilityMode = playerVisible ? EventJournalVisibilityModeIds.PlayerVisible : EventJournalVisibilityModeIds.GMOnly,
            IsPlayerVisible = playerVisible,
            IsAutomatic = true,
            ActorUserId = actorId,
            SubjectEntityType = "production",
            SubjectDisplayName = subjectName,
            CreatedByUserId = actorId,
            OccurredAtUtc = DateTime.UtcNow
        });
    }
}
