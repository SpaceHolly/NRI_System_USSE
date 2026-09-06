using System;
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
    private const string ShopDefinitions0172Collection = "shop_definitions";
    private const string ShopInstances0172Collection = "shop_instances";
    private const string ShopOffers0172Collection = "shop_offers";
    private const string PurchaseRequests0172Collection = "purchase_requests";
    private const string PurchaseReceipts0172Collection = "purchase_receipts";
    private const string PurchaseGrants0172Collection = "purchase_grants";
    private const string ShopAuditEvents0172Collection = "shop_audit_events";
    private bool _shop0172IndexesEnsured;

    public ResponseEnvelope ShopAdminList0172(CommandContext context)
    {
        RequireAdmin(context);
        EnsureShop0172Indexes();
        var campaignId = Shop0172CampaignId(context.Request.Payload);
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId);
        if (!includeArchived) filter &= Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var shops = ShopInstances0172().Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("Name")).ToList();
        return Ok("Shops loaded.", new Dictionary<string, object>
        {
            ["items"] = shops.Select(x => (object)Shop0172ShopPayload(x, admin: true)).ToArray()
        });
    }

    public ResponseEnvelope ShopAdminCreateShop0172(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureShop0172Indexes();
        var payload = context.Request.Payload;
        var now = DateTime.UtcNow;
        var shopId = Shop0172NewId("shop");
        var shop = new BsonDocument
        {
            ["Id"] = shopId,
            ["CampaignId"] = Shop0172CampaignId(payload),
            ["RuleSetId"] = Shop0172Text(payload, "ruleSetId", "default", 128),
            ["DefinitionId"] = Shop0172Text(payload, "definitionId", string.Empty, 128),
            ["Name"] = Shop0172Text(payload, "name", "Новый магазин", 160, true),
            ["Description"] = Shop0172Text(payload, "description", string.Empty, 2048),
            ["MarketType"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "marketType", "White", 64), Shop0172MarketTypes, "White"),
            ["LocationId"] = Shop0172Text(payload, "locationId", string.Empty, 128),
            ["FactionId"] = Shop0172Text(payload, "factionId", string.Empty, 128),
            ["OwnerContactId"] = Shop0172Text(payload, "ownerContactId", string.Empty, 128),
            ["Visibility"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "visibility", "Public", 64), Shop0172VisibilityModes, "Public"),
            ["IsPlayerVisible"] = payload.ContainsKey("isPlayerVisible") ? PayloadReader.GetBool(payload, "isPlayerVisible") : true,
            ["IsArchived"] = false,
            ["Tags"] = Shop0172Array(payload, "tags"),
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id,
            ["Revision"] = 1,
            ["ExtraData"] = new BsonDocument(),
            ["ServerOnlyData"] = new BsonDocument()
        };
        ShopInstances0172().InsertOne(shop);
        Shop0172Audit(actor, CommandNames.ShopAdminCreateShop, shopId, "shop.created", null, shop, "Shop created.");
        Shop0172Sync("shop.changed", "shop", shopId, "created", actor.Id, context.Request.RequestId);
        return Ok("Shop created.", new Dictionary<string, object> { ["shopId"] = shopId, ["item"] = Shop0172ShopPayload(shop, admin: true) });
    }

    public ResponseEnvelope ShopAdminGet0172(CommandContext context)
    {
        RequireAdmin(context);
        EnsureShop0172Indexes();
        var shop = Shop0172RequireShop(context.Request.Payload);
        return Ok("Shop loaded.", Shop0172AdminShopEnvelope(shop));
    }

    public ResponseEnvelope ShopAdminUpdateShop0172(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureShop0172Indexes();
        var payload = context.Request.Payload;
        var shop = Shop0172RequireShop(payload);
        var before = shop.DeepClone().AsBsonDocument;
        if (payload.ContainsKey("name")) shop["Name"] = Shop0172Text(payload, "name", Shop0172String(shop, "Name"), 160, true);
        if (payload.ContainsKey("description")) shop["Description"] = Shop0172Text(payload, "description", Shop0172String(shop, "Description"), 2048);
        if (payload.ContainsKey("marketType")) shop["MarketType"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "marketType", Shop0172String(shop, "MarketType"), 64), Shop0172MarketTypes, "White");
        if (payload.ContainsKey("visibility")) shop["Visibility"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "visibility", Shop0172String(shop, "Visibility"), 64), Shop0172VisibilityModes, "Public");
        if (payload.ContainsKey("isPlayerVisible")) shop["IsPlayerVisible"] = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("locationId")) shop["LocationId"] = Shop0172Text(payload, "locationId", Shop0172String(shop, "LocationId"), 128);
        if (payload.ContainsKey("factionId")) shop["FactionId"] = Shop0172Text(payload, "factionId", Shop0172String(shop, "FactionId"), 128);
        if (payload.ContainsKey("ownerContactId")) shop["OwnerContactId"] = Shop0172Text(payload, "ownerContactId", Shop0172String(shop, "OwnerContactId"), 128);
        Shop0172Touch(shop, actor.Id);
        ShopInstances0172().ReplaceOne(Shop0172IdFilter(Shop0172String(shop, "Id")), shop);
        Shop0172Audit(actor, CommandNames.ShopAdminUpdateShop, Shop0172String(shop, "Id"), "shop.updated", before, shop, "Shop updated.");
        Shop0172Sync("shop.changed", "shop", Shop0172String(shop, "Id"), "updated", actor.Id, context.Request.RequestId);
        return Ok("Shop updated.", new Dictionary<string, object> { ["item"] = Shop0172ShopPayload(shop, admin: true) });
    }

    public ResponseEnvelope ShopAdminArchiveShop0172(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureShop0172Indexes();
        var shop = Shop0172RequireShop(context.Request.Payload, includeArchived: true);
        var archived = context.Request.Payload.ContainsKey("isArchived") ? PayloadReader.GetBool(context.Request.Payload, "isArchived") : true;
        var before = shop.DeepClone().AsBsonDocument;
        shop["IsArchived"] = archived;
        Shop0172Touch(shop, actor.Id);
        ShopInstances0172().ReplaceOne(Shop0172IdFilter(Shop0172String(shop, "Id")), shop);
        Shop0172Audit(actor, CommandNames.ShopAdminArchiveShop, Shop0172String(shop, "Id"), archived ? "shop.archived" : "shop.restored", before, shop, archived ? "Shop archived." : "Shop restored.");
        Shop0172Sync("shop.changed", "shop", Shop0172String(shop, "Id"), archived ? "archived" : "restored", actor.Id, context.Request.RequestId);
        return Ok(archived ? "Shop archived." : "Shop restored.", new Dictionary<string, object> { ["item"] = Shop0172ShopPayload(shop, admin: true) });
    }

    public ResponseEnvelope ShopAdminCreateOffer0172(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureShop0172Indexes();
        var payload = context.Request.Payload;
        var shop = Shop0172RequireShop(payload);
        var now = DateTime.UtcNow;
        var offerId = Shop0172NewId("offer");
        var basePrice = Math.Max(0, Shop0172PayloadDecimal(payload, "basePrice", 10m));
        var offer = new BsonDocument
        {
            ["Id"] = offerId,
            ["CampaignId"] = Shop0172String(shop, "CampaignId"),
            ["ShopId"] = Shop0172String(shop, "Id"),
            ["ItemDefinitionId"] = Shop0172Text(payload, "itemDefinitionId", string.Empty, 128),
            ["DisplayName"] = Shop0172Text(payload, "displayName", "Товар", 160, true),
            ["PublicDescription"] = Shop0172Text(payload, "publicDescription", string.Empty, 2048),
            ["GMDescription"] = Shop0172Text(payload, "gmDescription", string.Empty, 2048),
            ["OfferType"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "offerType", "Item", 64), Shop0172OfferTypes, "Item"),
            ["BasePrice"] = Decimal128.Parse(basePrice.ToString(CultureInfo.InvariantCulture)),
            ["CurrencyCode"] = Shop0172Text(payload, "currencyCode", "credits", 64),
            ["Rarity"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "rarity", "Common", 64), Shop0172Rarities, "Common"),
            ["Availability"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "availability", "Available", 64), Shop0172Availabilities, "Available"),
            ["Stock"] = Math.Max(0, PayloadReader.GetInt(payload, "stock") ?? 1),
            ["LegalStatus"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "legalStatus", "Free", 64), Shop0172LegalStatuses, "Free"),
            ["ControlLevel"] = Math.Max(0, PayloadReader.GetInt(payload, "controlLevel") ?? 0),
            ["RequiresLicense"] = payload.ContainsKey("requiresLicense") && PayloadReader.GetBool(payload, "requiresLicense"),
            ["RequiresQuestOrContact"] = payload.ContainsKey("requiresQuestOrContact") && PayloadReader.GetBool(payload, "requiresQuestOrContact"),
            ["RequiresGmApproval"] = payload.ContainsKey("requiresGmApproval") && PayloadReader.GetBool(payload, "requiresGmApproval"),
            ["Reliability"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "reliability", "Normal", 64), Shop0172Reliabilities, "Normal"),
            ["DocumentQuality"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "documentQuality", "Clean", 64), Shop0172DocumentQualities, "Clean"),
            ["Visibility"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "visibility", "Public", 64), Shop0172VisibilityModes, "Public"),
            ["IsPlayerVisible"] = payload.ContainsKey("isPlayerVisible") ? PayloadReader.GetBool(payload, "isPlayerVisible") : true,
            ["IsArchived"] = false,
            ["LinkedEntityType"] = Shop0172Text(payload, "linkedEntityType", string.Empty, 64),
            ["LinkedEntityId"] = Shop0172Text(payload, "linkedEntityId", string.Empty, 128),
            ["LinkedEntityDisplayName"] = Shop0172Text(payload, "linkedEntityDisplayName", string.Empty, 160),
            ["Tags"] = Shop0172Array(payload, "tags"),
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id,
            ["Revision"] = 1,
            ["ExtraData"] = new BsonDocument(),
            ["ServerOnlyData"] = new BsonDocument()
        };
        ShopOffers0172().InsertOne(offer);
        Shop0172Audit(actor, CommandNames.ShopAdminCreateOffer, offerId, "offer.created", null, offer, "Offer created.");
        Shop0172Sync("shop.changed", "shop_offer", offerId, "created", actor.Id, context.Request.RequestId);
        return Ok("Offer created.", new Dictionary<string, object> { ["offerId"] = offerId, ["item"] = Shop0172OfferPayload(offer, shop, admin: true) });
    }

    public ResponseEnvelope ShopAdminUpdateOffer0172(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureShop0172Indexes();
        var payload = context.Request.Payload;
        var offer = Shop0172RequireOffer(payload, includeArchived: true);
        var shop = Shop0172FindShop(Shop0172String(offer, "ShopId"));
        if (shop == null) return Error("Shop not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var before = offer.DeepClone().AsBsonDocument;
        if (payload.ContainsKey("displayName")) offer["DisplayName"] = Shop0172Text(payload, "displayName", Shop0172String(offer, "DisplayName"), 160, true);
        if (payload.ContainsKey("publicDescription")) offer["PublicDescription"] = Shop0172Text(payload, "publicDescription", Shop0172String(offer, "PublicDescription"), 2048);
        if (payload.ContainsKey("gmDescription")) offer["GMDescription"] = Shop0172Text(payload, "gmDescription", Shop0172String(offer, "GMDescription"), 2048);
        if (payload.ContainsKey("offerType")) offer["OfferType"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "offerType", Shop0172String(offer, "OfferType"), 64), Shop0172OfferTypes, "Item");
        if (payload.ContainsKey("basePrice")) offer["BasePrice"] = Decimal128.Parse(Math.Max(0, Shop0172PayloadDecimal(payload, "basePrice", Shop0172Decimal(offer, "BasePrice"))).ToString(CultureInfo.InvariantCulture));
        if (payload.ContainsKey("currencyCode")) offer["CurrencyCode"] = Shop0172Text(payload, "currencyCode", Shop0172String(offer, "CurrencyCode"), 64);
        if (payload.ContainsKey("rarity")) offer["Rarity"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "rarity", Shop0172String(offer, "Rarity"), 64), Shop0172Rarities, "Common");
        if (payload.ContainsKey("availability")) offer["Availability"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "availability", Shop0172String(offer, "Availability"), 64), Shop0172Availabilities, "Available");
        if (payload.ContainsKey("stock")) offer["Stock"] = Math.Max(0, PayloadReader.GetInt(payload, "stock") ?? Shop0172Int(offer, "Stock"));
        if (payload.ContainsKey("legalStatus")) offer["LegalStatus"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "legalStatus", Shop0172String(offer, "LegalStatus"), 64), Shop0172LegalStatuses, "Free");
        if (payload.ContainsKey("controlLevel")) offer["ControlLevel"] = Math.Max(0, PayloadReader.GetInt(payload, "controlLevel") ?? Shop0172Int(offer, "ControlLevel"));
        if (payload.ContainsKey("requiresLicense")) offer["RequiresLicense"] = PayloadReader.GetBool(payload, "requiresLicense");
        if (payload.ContainsKey("requiresQuestOrContact")) offer["RequiresQuestOrContact"] = PayloadReader.GetBool(payload, "requiresQuestOrContact");
        if (payload.ContainsKey("requiresGmApproval")) offer["RequiresGmApproval"] = PayloadReader.GetBool(payload, "requiresGmApproval");
        if (payload.ContainsKey("reliability")) offer["Reliability"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "reliability", Shop0172String(offer, "Reliability"), 64), Shop0172Reliabilities, "Normal");
        if (payload.ContainsKey("documentQuality")) offer["DocumentQuality"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "documentQuality", Shop0172String(offer, "DocumentQuality"), 64), Shop0172DocumentQualities, "Clean");
        if (payload.ContainsKey("visibility")) offer["Visibility"] = Shop0172NormalizeOneOf(Shop0172Text(payload, "visibility", Shop0172String(offer, "Visibility"), 64), Shop0172VisibilityModes, "Public");
        if (payload.ContainsKey("isPlayerVisible")) offer["IsPlayerVisible"] = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("linkedEntityType")) offer["LinkedEntityType"] = Shop0172Text(payload, "linkedEntityType", Shop0172String(offer, "LinkedEntityType"), 64);
        if (payload.ContainsKey("linkedEntityId")) offer["LinkedEntityId"] = Shop0172Text(payload, "linkedEntityId", Shop0172String(offer, "LinkedEntityId"), 128);
        if (payload.ContainsKey("linkedEntityDisplayName")) offer["LinkedEntityDisplayName"] = Shop0172Text(payload, "linkedEntityDisplayName", Shop0172String(offer, "LinkedEntityDisplayName"), 160);
        Shop0172Touch(offer, actor.Id);
        ShopOffers0172().ReplaceOne(Shop0172IdFilter(Shop0172String(offer, "Id")), offer);
        Shop0172Audit(actor, CommandNames.ShopAdminUpdateOffer, Shop0172String(offer, "Id"), "offer.updated", before, offer, "Offer updated.");
        Shop0172Sync("shop.changed", "shop_offer", Shop0172String(offer, "Id"), "updated", actor.Id, context.Request.RequestId);
        return Ok("Offer updated.", new Dictionary<string, object> { ["item"] = Shop0172OfferPayload(offer, shop, admin: true) });
    }

    public ResponseEnvelope ShopAdminAdjustStock0172(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureShop0172Indexes();
        var offer = Shop0172RequireOffer(context.Request.Payload, includeArchived: true);
        var before = offer.DeepClone().AsBsonDocument;
        var mode = Shop0172Text(context.Request.Payload, "mode", "set", 32);
        var value = PayloadReader.GetInt(context.Request.Payload, "stock") ?? PayloadReader.GetInt(context.Request.Payload, "delta") ?? 0;
        var current = Shop0172Int(offer, "Stock");
        offer["Stock"] = Math.Max(0, mode.Equals("delta", StringComparison.OrdinalIgnoreCase) ? current + value : value);
        Shop0172Touch(offer, actor.Id);
        ShopOffers0172().ReplaceOne(Shop0172IdFilter(Shop0172String(offer, "Id")), offer);
        Shop0172Audit(actor, CommandNames.ShopAdminAdjustStock, Shop0172String(offer, "Id"), "offer.stock_adjusted", before, offer, "Offer stock adjusted.");
        Shop0172Sync("shop.changed", "shop_offer", Shop0172String(offer, "Id"), "stock_adjusted", actor.Id, context.Request.RequestId);
        return Ok("Stock adjusted.", new Dictionary<string, object> { ["stock"] = Shop0172Int(offer, "Stock") });
    }

    public ResponseEnvelope ShopAdminListPurchaseRequests0172(CommandContext context)
    {
        RequireAdmin(context);
        EnsureShop0172Indexes();
        var campaignId = Shop0172CampaignId(context.Request.Payload);
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId);
        if (!includeArchived) filter &= Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var items = PurchaseRequests0172().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(200).ToList()
            .Select(x => (object)Shop0172PurchaseRequestPayload(x, admin: true)).ToArray();
        return Ok("Purchase requests loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ShopAdminGetPurchaseRequest0172(CommandContext context)
    {
        RequireAdmin(context);
        EnsureShop0172Indexes();
        var request = Shop0172RequirePurchaseRequest(context.Request.Payload);
        return Ok("Purchase request loaded.", Shop0172AdminPurchaseEnvelope(request));
    }

    public ResponseEnvelope ShopAdminApprovePurchase0172(CommandContext context)
    {
        var actor = RequireAdmin(context);
        return Shop0172AdminSetPurchaseStatus(context, actor, "Approved", CommandNames.ShopAdminApprovePurchase, "purchase.approved");
    }

    public ResponseEnvelope ShopAdminRejectPurchase0172(CommandContext context)
    {
        var actor = RequireAdmin(context);
        return Shop0172AdminSetPurchaseStatus(context, actor, "Rejected", CommandNames.ShopAdminRejectPurchase, "purchase.rejected");
    }

    public ResponseEnvelope ShopAdminMarkRequiresProject0172(CommandContext context)
    {
        var actor = RequireAdmin(context);
        return Shop0172AdminSetPurchaseStatus(context, actor, "RequiresProjectOrLicense", CommandNames.ShopAdminMarkRequiresProject, "purchase.requires_project");
    }

    public ResponseEnvelope ShopAdminCompletePurchase0172(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureShop0172Indexes();
        var request = Shop0172RequirePurchaseRequest(context.Request.Payload);
        var status = Shop0172String(request, "Status");
        if (!status.Equals("Approved", StringComparison.OrdinalIgnoreCase) && !status.Equals("PendingGmReview", StringComparison.OrdinalIgnoreCase))
            return Error("Purchase request must be approved or pending GM review before completion.", ResponseStatus.Conflict, ErrorCode.Conflict);
        if (status.Equals("RequiresProjectOrLicense", StringComparison.OrdinalIgnoreCase))
            return Error("Large or controlled assets require a project/license flow.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var offer = Shop0172FindOffer(Shop0172String(request, "OfferId"));
        if (offer == null || Shop0172Bool(offer, "IsArchived")) return Error("Offer not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var quantity = Math.Max(1, Shop0172Int(request, "Quantity", 1));
        if (Shop0172Int(offer, "Stock") < quantity) return Error("Offer is out of stock.", ResponseStatus.Conflict, ErrorCode.Conflict);
        return Shop0172CompletePurchase(context, actor, request, offer, manual: true);
    }

    public ResponseEnvelope ShopAdminGetAudit0172(CommandContext context)
    {
        RequireAdmin(context);
        EnsureShop0172Indexes();
        var entityId = Shop0172Text(context.Request.Payload, "entityId", string.Empty, 128);
        var filter = string.IsNullOrWhiteSpace(entityId)
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.Eq("EntityId", entityId);
        var items = ShopAuditEvents0172().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("CreatedAtUtc")).Limit(200).ToList()
            .Select(x => (object)Shop0172AuditPayload(x)).ToArray();
        return Ok("Shop audit loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope ShopPlayerListShops0172(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureShop0172Indexes();
        var campaignId = Shop0172CampaignId(context.Request.Payload);
        var shops = ShopInstances0172().Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Ascending("Name")).ToList()
            .Where(Shop0172CanPlayerSeeShop)
            .Select(x => (object)Shop0172ShopPayload(x, admin: false)).ToArray();
        _logger.Admin($"shop.player.list actor={actor.Login} count={shops.Length}");
        return Ok("Player shops loaded.", new Dictionary<string, object> { ["items"] = shops });
    }

    public ResponseEnvelope ShopPlayerListOffers0172(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureShop0172Indexes();
        var shopId = Shop0172RequiredId(context.Request.Payload, "shopId");
        var shop = Shop0172FindShop(shopId);
        if (shop == null || !Shop0172CanPlayerSeeShop(shop)) return Error("Shop not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var offers = ShopOffers0172().Find(Builders<BsonDocument>.Filter.Eq("ShopId", shopId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Ascending("DisplayName")).ToList()
            .Where(offer => Shop0172CanPlayerSeeOffer(offer, shop))
            .Select(offer => (object)Shop0172OfferPayload(offer, shop, admin: false)).ToArray();
        _logger.Admin($"shop.player.offers actor={actor.Login} shopId={shopId} count={offers.Length}");
        return Ok("Player shop offers loaded.", new Dictionary<string, object> { ["items"] = offers, ["shop"] = Shop0172ShopPayload(shop, admin: false) });
    }

    public ResponseEnvelope ShopPlayerGetOffer0172(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureShop0172Indexes();
        var offer = Shop0172FindOffer(Shop0172RequiredId(context.Request.Payload, "offerId"));
        if (offer == null || Shop0172Bool(offer, "IsArchived")) return Error("Offer not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var shop = Shop0172FindShop(Shop0172String(offer, "ShopId"));
        if (shop == null || !Shop0172CanPlayerSeeOffer(offer, shop)) return Error("Offer not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Player offer loaded.", new Dictionary<string, object> { ["item"] = Shop0172OfferPayload(offer, shop, admin: false) });
    }

    public ResponseEnvelope ShopPlayerRequestPurchase0172(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureShop0172Indexes();
        var payload = context.Request.Payload;
        var offer = Shop0172FindOffer(Shop0172RequiredId(payload, "offerId"));
        if (offer == null || Shop0172Bool(offer, "IsArchived")) return Error("Offer not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var shop = Shop0172FindShop(Shop0172String(offer, "ShopId"));
        if (shop == null || !Shop0172CanPlayerSeeOffer(offer, shop)) return Error("Offer not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var characterId = Shop0172Text(payload, "characterId", string.Empty, 128);
        if (!string.IsNullOrWhiteSpace(characterId) && !Shop0172IsOwnedCharacter(actor.Id, characterId))
            return Error("Character is not available for this player.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var quantity = Math.Max(1, PayloadReader.GetInt(payload, "quantity") ?? 1);
        if (Shop0172Int(offer, "Stock") < quantity) return Error("Offer is out of stock.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var pricing = Shop0172Price(offer, shop, quantity);
        var status = pricing.RequiresProjectOrLicense ? "RequiresProjectOrLicense" : pricing.InstantAllowed ? "Completed" : "PendingGmReview";
        var now = DateTime.UtcNow;
        var requestId = Shop0172NewId("purchase_request");
        var request = new BsonDocument
        {
            ["Id"] = requestId,
            ["CampaignId"] = Shop0172String(shop, "CampaignId"),
            ["ShopId"] = Shop0172String(shop, "Id"),
            ["OfferId"] = Shop0172String(offer, "Id"),
            ["BuyerUserId"] = actor.Id,
            ["BuyerLogin"] = actor.Login,
            ["CharacterId"] = characterId,
            ["Quantity"] = quantity,
            ["Status"] = status,
            ["BaseUnitPrice"] = Decimal128.Parse(Shop0172Decimal(offer, "BasePrice").ToString(CultureInfo.InvariantCulture)),
            ["FinalUnitPrice"] = Decimal128.Parse(pricing.UnitPrice.ToString(CultureInfo.InvariantCulture)),
            ["FinalTotalPrice"] = Decimal128.Parse(pricing.TotalPrice.ToString(CultureInfo.InvariantCulture)),
            ["CurrencyCode"] = Shop0172String(offer, "CurrencyCode", "credits"),
            ["Availability"] = pricing.Availability,
            ["PricingSummary"] = pricing.PublicSummary,
            ["LegalSummary"] = pricing.LegalSummary,
            ["RiskSummary"] = pricing.RiskSummary,
            ["PlayerComment"] = Shop0172Text(payload, "comment", string.Empty, 1024),
            ["GmComment"] = string.Empty,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["ResolvedAtUtc"] = BsonNull.Value,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id,
            ["Revision"] = 1,
            ["IsArchived"] = false,
            ["ExtraData"] = new BsonDocument(),
            ["ServerOnlyData"] = new BsonDocument { ["suspicion"] = pricing.Suspicion }
        };
        PurchaseRequests0172().InsertOne(request);
        Shop0172Audit(actor, CommandNames.ShopPlayerRequestPurchase, requestId, "purchase.requested", null, request, "Purchase requested.");
        Shop0172Sync("shop.purchase.changed", "purchase_request", requestId, "created", actor.Id, context.Request.RequestId);
        if (pricing.InstantAllowed)
        {
            return Shop0172CompletePurchase(context, actor, request, offer, manual: false);
        }
        return Ok("Purchase request submitted for GM review.", new Dictionary<string, object> { ["item"] = Shop0172PurchaseRequestPayload(request, admin: false) });
    }

    public ResponseEnvelope ShopPlayerRequestSale0172(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureShop0172Indexes();
        var payload = context.Request.Payload;
        var itemGrantId = Shop0172RequiredId(payload, "itemGrantId");
        var grant = PurchaseGrants0172().Find(Shop0172IdFilter(itemGrantId)).FirstOrDefault();
        if (grant == null || Shop0172Bool(grant, "IsArchived") || !Shop0172String(grant, "BuyerUserId").Equals(actor.Id, StringComparison.OrdinalIgnoreCase))
            return Error("Owned item was not found.", ResponseStatus.NotFound, ErrorCode.NotFound);

        var existingSale = PurchaseRequests0172()
            .Find(Builders<BsonDocument>.Filter.Eq("TransactionType", "Sell")
                  & Builders<BsonDocument>.Filter.Eq("ItemGrantId", itemGrantId)
                  & Builders<BsonDocument>.Filter.Eq("BuyerUserId", actor.Id)
                  & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .FirstOrDefault();
        if (existingSale != null)
        {
            var receipt = PurchaseReceipts0172().Find(Builders<BsonDocument>.Filter.Eq("PurchaseRequestId", Shop0172String(existingSale, "Id"))).FirstOrDefault();
            return Ok("Sale request already exists.", new Dictionary<string, object>
            {
                ["request"] = Shop0172PurchaseRequestPayload(existingSale, admin: false),
                ["receipt"] = receipt == null ? new Dictionary<string, object>() : Shop0172ReceiptPayload(receipt, admin: false),
                ["idempotent"] = true
            });
        }

        var sourceRequest = PurchaseRequests0172().Find(Shop0172IdFilter(Shop0172String(grant, "PurchaseRequestId"))).FirstOrDefault();
        var offer = sourceRequest == null ? null : Shop0172FindOffer(Shop0172String(sourceRequest, "OfferId"));
        var shopId = sourceRequest == null ? Shop0172String(grant, "ShopId") : Shop0172String(sourceRequest, "ShopId");
        var shop = string.IsNullOrWhiteSpace(shopId) ? null : Shop0172FindShop(shopId);
        var requiresGmReview = Shop0172RequiresSaleReview(offer, shop);
        var confirmed = PayloadReader.GetBool(payload, "confirmSale") || PayloadReader.GetBool(payload, "confirmed");
        if (!requiresGmReview && !confirmed)
            return Error("Sale confirmation is required.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var quantity = Math.Max(1, PayloadReader.GetInt(payload, "quantity") ?? Math.Max(1, Shop0172Int(grant, "Quantity", 1)));
        var originalPrice = sourceRequest == null ? 0m : Shop0172Decimal(sourceRequest, "FinalTotalPrice");
        var saleTotal = Math.Max(0m, Shop0172PayloadDecimal(payload, "sellPrice", Math.Round(originalPrice * 0.5m, 2, MidpointRounding.AwayFromZero)));
        var now = DateTime.UtcNow;
        var requestId = Shop0172NewId("sale_request");
        var request = new BsonDocument
        {
            ["Id"] = requestId,
            ["TransactionType"] = "Sell",
            ["CampaignId"] = Shop0172String(grant, "CampaignId", sourceRequest == null ? "dev-campaign-core" : Shop0172String(sourceRequest, "CampaignId")),
            ["ShopId"] = shopId,
            ["OfferId"] = sourceRequest == null ? string.Empty : Shop0172String(sourceRequest, "OfferId"),
            ["ItemGrantId"] = itemGrantId,
            ["BuyerUserId"] = actor.Id,
            ["BuyerLogin"] = actor.Login,
            ["SellerUserId"] = actor.Id,
            ["SellerLogin"] = actor.Login,
            ["CharacterId"] = Shop0172String(grant, "CharacterId"),
            ["Quantity"] = quantity,
            ["Status"] = requiresGmReview ? "PendingGmReview" : "Completed",
            ["BaseUnitPrice"] = Decimal128.Parse(saleTotal.ToString(CultureInfo.InvariantCulture)),
            ["FinalUnitPrice"] = Decimal128.Parse(saleTotal.ToString(CultureInfo.InvariantCulture)),
            ["FinalTotalPrice"] = Decimal128.Parse(saleTotal.ToString(CultureInfo.InvariantCulture)),
            ["CurrencyCode"] = sourceRequest == null ? "credits" : Shop0172String(sourceRequest, "CurrencyCode", "credits"),
            ["Availability"] = requiresGmReview ? "PendingGmReview" : "Available",
            ["PricingSummary"] = $"Sell value {saleTotal:0.##}",
            ["LegalSummary"] = requiresGmReview ? "Продажа требует проверки GM." : "Обычная безопасная продажа.",
            ["RiskSummary"] = requiresGmReview ? "Предмет требует ручного решения GM." : "Без явных рисков.",
            ["PlayerComment"] = Shop0172Text(payload, "comment", string.Empty, 1024),
            ["GmComment"] = string.Empty,
            ["InventoryDebitApplied"] = false,
            ["WalletCreditApplied"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["ResolvedAtUtc"] = requiresGmReview ? BsonNull.Value : now,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id,
            ["ResolvedByUserId"] = requiresGmReview ? string.Empty : actor.Id,
            ["ResolvedByLogin"] = requiresGmReview ? string.Empty : actor.Login,
            ["Revision"] = 1,
            ["IsArchived"] = false,
            ["ExtraData"] = new BsonDocument(),
            ["ServerOnlyData"] = new BsonDocument { ["saleReviewReason"] = requiresGmReview ? "controlled_or_non_common_item" : "safe_common_item" }
        };

        PurchaseRequests0172().InsertOne(request);
        Shop0172Audit(actor, CommandNames.ShopPlayerRequestSale, requestId, "sale.requested", null, request, "Sale requested.");
        Shop0172Sync("shop.sale.changed", "sale_request", requestId, "created", actor.Id, context.Request.RequestId);
        if (requiresGmReview)
            return Ok("Sale request submitted for GM review.", new Dictionary<string, object> { ["item"] = Shop0172PurchaseRequestPayload(request, admin: false) });

        return Shop0172CompleteSale(context, actor, request, grant);
    }

    public ResponseEnvelope ShopPlayerPurchaseHistory0172(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureShop0172Indexes();
        var campaignId = Shop0172CampaignId(context.Request.Payload);
        var requestFilter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId) & Builders<BsonDocument>.Filter.Eq("BuyerUserId", actor.Id);
        var requests = PurchaseRequests0172().Find(requestFilter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(100).ToList()
            .Select(x => (object)Shop0172PurchaseRequestPayload(x, admin: false)).ToArray();
        var receiptFilter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId) & Builders<BsonDocument>.Filter.Eq("BuyerUserId", actor.Id);
        var receipts = PurchaseReceipts0172().Find(receiptFilter).Sort(Builders<BsonDocument>.Sort.Descending("CreatedAtUtc")).Limit(100).ToList()
            .Select(x => (object)Shop0172ReceiptPayload(x, admin: false)).ToArray();
        return Ok("Purchase history loaded.", new Dictionary<string, object> { ["requests"] = requests, ["receipts"] = receipts });
    }

    private ResponseEnvelope Shop0172AdminSetPurchaseStatus(CommandContext context, UserAccount actor, string newStatus, string command, string auditAction)
    {
        EnsureShop0172Indexes();
        var request = Shop0172RequirePurchaseRequest(context.Request.Payload);
        var before = request.DeepClone().AsBsonDocument;
        var current = Shop0172String(request, "Status");
        if (current is "Completed" or "Rejected" or "Cancelled")
            return Error("Purchase request cannot be changed from its current status.", ResponseStatus.Conflict, ErrorCode.Conflict);
        request["Status"] = newStatus;
        request["GmComment"] = Shop0172Text(context.Request.Payload, "gmComment", Shop0172String(request, "GmComment"), 1024);
        request["UpdatedByUserId"] = actor.Id;
        request["ResolvedByUserId"] = actor.Id;
        request["ResolvedByLogin"] = actor.Login;
        request["ResolvedAtUtc"] = DateTime.UtcNow;
        if (context.Request.Payload.ContainsKey("finalUnitPrice"))
        {
            var unit = Math.Max(0, Shop0172PayloadDecimal(context.Request.Payload, "finalUnitPrice", Shop0172Decimal(request, "FinalUnitPrice")));
            request["FinalUnitPrice"] = Decimal128.Parse(unit.ToString(CultureInfo.InvariantCulture));
            request["FinalTotalPrice"] = Decimal128.Parse((unit * Math.Max(1, Shop0172Int(request, "Quantity", 1))).ToString(CultureInfo.InvariantCulture));
        }
        Shop0172Touch(request, actor.Id);
        PurchaseRequests0172().ReplaceOne(Shop0172IdFilter(Shop0172String(request, "Id")), request);
        Shop0172Audit(actor, command, Shop0172String(request, "Id"), auditAction, before, request, $"Purchase request status set to {newStatus}.");
        Shop0172Sync("shop.purchase.changed", "purchase_request", Shop0172String(request, "Id"), newStatus.ToLowerInvariant(), actor.Id, context.Request.RequestId);
        return Ok("Purchase request updated.", Shop0172AdminPurchaseEnvelope(request));
    }

    private ResponseEnvelope Shop0172CompletePurchase(CommandContext context, UserAccount actor, BsonDocument request, BsonDocument offer, bool manual)
    {
        var beforeRequest = request.DeepClone().AsBsonDocument;
        var beforeOffer = offer.DeepClone().AsBsonDocument;
        var quantity = Math.Max(1, Shop0172Int(request, "Quantity", 1));
        var stock = Shop0172Int(offer, "Stock");
        if (stock < quantity) return Error("Offer is out of stock.", ResponseStatus.Conflict, ErrorCode.Conflict);
        offer["Stock"] = stock - quantity;
        Shop0172Touch(offer, actor.Id);
        ShopOffers0172().ReplaceOne(Shop0172IdFilter(Shop0172String(offer, "Id")), offer);

        request["Status"] = "Completed";
        request["ResolvedAtUtc"] = DateTime.UtcNow;
        request["ResolvedByUserId"] = actor.Id;
        request["ResolvedByLogin"] = actor.Login;
        Shop0172Touch(request, actor.Id);
        PurchaseRequests0172().ReplaceOne(Shop0172IdFilter(Shop0172String(request, "Id")), request);

        var receipt = Shop0172CreateReceipt(actor, request, offer, manual);
        var grant = Shop0172CreateGrant(actor, request, offer, receipt);
        Shop0172Audit(actor, manual ? CommandNames.ShopAdminCompletePurchase : CommandNames.ShopPlayerRequestPurchase, Shop0172String(request, "Id"), "purchase.completed", beforeRequest, request, "Purchase completed.");
        Shop0172Audit(actor, "shop.stock.decrement", Shop0172String(offer, "Id"), "offer.stock_decremented", beforeOffer, offer, "Stock decreased after completed purchase.");
        Shop0172Sync("shop.purchase.changed", "purchase_request", Shop0172String(request, "Id"), "completed", actor.Id, context.Request.RequestId);
        Shop0172Sync("shop.changed", "shop_offer", Shop0172String(offer, "Id"), "stock_decremented", actor.Id, context.Request.RequestId);
        return Ok(manual ? "Purchase completed." : "Purchase completed instantly.", new Dictionary<string, object>
        {
            ["request"] = Shop0172PurchaseRequestPayload(request, admin: IsAdminActor(actor)),
            ["receipt"] = Shop0172ReceiptPayload(receipt, admin: IsAdminActor(actor)),
            ["grant"] = Shop0172GrantPayload(grant, admin: IsAdminActor(actor))
        });
    }

    private ResponseEnvelope Shop0172CompleteSale(CommandContext context, UserAccount actor, BsonDocument request, BsonDocument grant)
    {
        var beforeRequest = request.DeepClone().AsBsonDocument;
        var beforeGrant = grant.DeepClone().AsBsonDocument;
        request["Status"] = "Completed";
        request["InventoryDebitApplied"] = true;
        request["WalletCreditApplied"] = true;
        request["ResolvedAtUtc"] = DateTime.UtcNow;
        request["ResolvedByUserId"] = actor.Id;
        request["ResolvedByLogin"] = actor.Login;
        Shop0172Touch(request, actor.Id);
        PurchaseRequests0172().ReplaceOne(Shop0172IdFilter(Shop0172String(request, "Id")), request);

        grant["Status"] = "Sold";
        grant["IsSold"] = true;
        grant["SoldAtUtc"] = DateTime.UtcNow;
        grant["SoldBySaleRequestId"] = Shop0172String(request, "Id");
        grant["SoldByUserId"] = actor.Id;
        grant["InventoryDebitApplied"] = true;
        Shop0172Touch(grant, actor.Id);
        PurchaseGrants0172().ReplaceOne(Shop0172IdFilter(Shop0172String(grant, "Id")), grant);

        var receipt = Shop0172CreateSaleReceipt(actor, request, grant);
        Shop0172Audit(actor, CommandNames.ShopPlayerRequestSale, Shop0172String(request, "Id"), "sale.completed", beforeRequest, request, "Sale completed.");
        Shop0172Audit(actor, "shop.sale.inventory_debit", Shop0172String(grant, "Id"), "sale.inventory_debit_applied", beforeGrant, grant, "Sold item grant marked as sold.");
        Shop0172Sync("shop.sale.changed", "sale_request", Shop0172String(request, "Id"), "completed", actor.Id, context.Request.RequestId);
        Shop0172Sync("shop.sale.changed", "purchase_grant", Shop0172String(grant, "Id"), "sold", actor.Id, context.Request.RequestId);
        return Ok("Sale completed.", new Dictionary<string, object>
        {
            ["request"] = Shop0172PurchaseRequestPayload(request, admin: false),
            ["receipt"] = Shop0172ReceiptPayload(receipt, admin: false),
            ["grant"] = Shop0172GrantPayload(grant, admin: false)
        });
    }

    private BsonDocument Shop0172CreateReceipt(UserAccount actor, BsonDocument request, BsonDocument offer, bool manual)
    {
        var now = DateTime.UtcNow;
        var receipt = new BsonDocument
        {
            ["Id"] = Shop0172NewId("purchase_receipt"),
            ["CampaignId"] = Shop0172String(request, "CampaignId"),
            ["PurchaseRequestId"] = Shop0172String(request, "Id"),
            ["ShopId"] = Shop0172String(request, "ShopId"),
            ["OfferId"] = Shop0172String(request, "OfferId"),
            ["BuyerUserId"] = Shop0172String(request, "BuyerUserId"),
            ["BuyerLogin"] = Shop0172String(request, "BuyerLogin"),
            ["CharacterId"] = Shop0172String(request, "CharacterId"),
            ["ItemDefinitionId"] = Shop0172String(offer, "ItemDefinitionId"),
            ["DisplayName"] = Shop0172String(offer, "DisplayName"),
            ["Quantity"] = Math.Max(1, Shop0172Int(request, "Quantity", 1)),
            ["FinalTotalPrice"] = request.GetValue("FinalTotalPrice", Decimal128.Zero),
            ["CurrencyCode"] = Shop0172String(request, "CurrencyCode", "credits"),
            ["GrantMode"] = "PendingGmApply",
            ["WalletApplyMode"] = "PendingGmApply",
            ["CreatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["ManualCompletion"] = manual
        };
        PurchaseReceipts0172().InsertOne(receipt);
        return receipt;
    }

    private BsonDocument Shop0172CreateSaleReceipt(UserAccount actor, BsonDocument request, BsonDocument grant)
    {
        var now = DateTime.UtcNow;
        var receipt = new BsonDocument
        {
            ["Id"] = Shop0172NewId("sale_receipt"),
            ["TransactionType"] = "Sell",
            ["CampaignId"] = Shop0172String(request, "CampaignId"),
            ["PurchaseRequestId"] = Shop0172String(request, "Id"),
            ["SaleRequestId"] = Shop0172String(request, "Id"),
            ["ShopId"] = Shop0172String(request, "ShopId"),
            ["OfferId"] = Shop0172String(request, "OfferId"),
            ["ItemGrantId"] = Shop0172String(grant, "Id"),
            ["BuyerUserId"] = Shop0172String(request, "BuyerUserId"),
            ["BuyerLogin"] = Shop0172String(request, "BuyerLogin"),
            ["SellerUserId"] = Shop0172String(request, "SellerUserId"),
            ["SellerLogin"] = Shop0172String(request, "SellerLogin"),
            ["CharacterId"] = Shop0172String(request, "CharacterId"),
            ["ItemDefinitionId"] = Shop0172String(grant, "ItemDefinitionId"),
            ["DisplayName"] = Shop0172String(grant, "DisplayName"),
            ["Quantity"] = Math.Max(1, Shop0172Int(request, "Quantity", 1)),
            ["FinalTotalPrice"] = request.GetValue("FinalTotalPrice", Decimal128.Zero),
            ["CurrencyCode"] = Shop0172String(request, "CurrencyCode", "credits"),
            ["GrantMode"] = "InventoryDebitApplied",
            ["WalletApplyMode"] = "WalletCreditRecorded",
            ["InventoryDebitApplied"] = true,
            ["WalletCreditApplied"] = true,
            ["CreatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["ManualCompletion"] = false
        };
        PurchaseReceipts0172().InsertOne(receipt);
        return receipt;
    }

    private BsonDocument Shop0172CreateGrant(UserAccount actor, BsonDocument request, BsonDocument offer, BsonDocument receipt)
    {
        var grant = new BsonDocument
        {
            ["Id"] = Shop0172NewId("purchase_grant"),
            ["CampaignId"] = Shop0172String(request, "CampaignId"),
            ["PurchaseRequestId"] = Shop0172String(request, "Id"),
            ["PurchaseReceiptId"] = Shop0172String(receipt, "Id"),
            ["ShopId"] = Shop0172String(request, "ShopId"),
            ["OfferId"] = Shop0172String(request, "OfferId"),
            ["BuyerUserId"] = Shop0172String(request, "BuyerUserId"),
            ["CharacterId"] = Shop0172String(request, "CharacterId"),
            ["ItemDefinitionId"] = Shop0172String(offer, "ItemDefinitionId"),
            ["DisplayName"] = Shop0172String(offer, "DisplayName"),
            ["Quantity"] = Math.Max(1, Shop0172Int(request, "Quantity", 1)),
            ["Status"] = "PendingGmApply",
            ["IsSold"] = false,
            ["InventoryDebitApplied"] = false,
            ["CreatedAtUtc"] = DateTime.UtcNow,
            ["CreatedByUserId"] = actor.Id,
            ["ServerOnlyData"] = new BsonDocument()
        };
        PurchaseGrants0172().InsertOne(grant);
        return grant;
    }

    private Dictionary<string, object> Shop0172AdminShopEnvelope(BsonDocument shop)
    {
        var shopId = Shop0172String(shop, "Id");
        var offers = ShopOffers0172().Find(Builders<BsonDocument>.Filter.Eq("ShopId", shopId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true)).Sort(Builders<BsonDocument>.Sort.Ascending("DisplayName")).ToList();
        var requests = PurchaseRequests0172().Find(Builders<BsonDocument>.Filter.Eq("ShopId", shopId)).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(100).ToList();
        return new Dictionary<string, object>
        {
            ["shop"] = Shop0172ShopPayload(shop, admin: true),
            ["offers"] = offers.Select(x => (object)Shop0172OfferPayload(x, shop, admin: true)).ToArray(),
            ["purchaseRequests"] = requests.Select(x => (object)Shop0172PurchaseRequestPayload(x, admin: true)).ToArray()
        };
    }

    private Dictionary<string, object> Shop0172AdminPurchaseEnvelope(BsonDocument request)
    {
        var receiptFilter = Builders<BsonDocument>.Filter.Eq("PurchaseRequestId", Shop0172String(request, "Id"));
        return new Dictionary<string, object>
        {
            ["request"] = Shop0172PurchaseRequestPayload(request, admin: true),
            ["receipts"] = PurchaseReceipts0172().Find(receiptFilter).ToList().Select(x => (object)Shop0172ReceiptPayload(x, admin: true)).ToArray(),
            ["grants"] = PurchaseGrants0172().Find(receiptFilter).ToList().Select(x => (object)Shop0172GrantPayload(x, admin: true)).ToArray()
        };
    }

    private Dictionary<string, object> Shop0172ShopPayload(BsonDocument doc, bool admin)
    {
        var map = new Dictionary<string, object>
        {
            ["shopId"] = Shop0172String(doc, "Id"),
            ["campaignId"] = Shop0172String(doc, "CampaignId"),
            ["name"] = Shop0172String(doc, "Name"),
            ["description"] = Shop0172String(doc, "Description"),
            ["marketType"] = Shop0172String(doc, "MarketType", "White"),
            ["visibility"] = admin ? Shop0172String(doc, "Visibility", "Public") : "PlayerVisible",
            ["isPlayerVisible"] = Shop0172Bool(doc, "IsPlayerVisible", true),
            ["isArchived"] = admin && Shop0172Bool(doc, "IsArchived"),
            ["updatedAtUtc"] = Shop0172Date(doc, "UpdatedAtUtc")
        };
        if (admin)
        {
            map["locationId"] = Shop0172String(doc, "LocationId");
            map["factionId"] = Shop0172String(doc, "FactionId");
            map["ownerContactId"] = Shop0172String(doc, "OwnerContactId");
            map["revision"] = Shop0172Int(doc, "Revision", 1);
        }
        return map;
    }

    private Dictionary<string, object> Shop0172OfferPayload(BsonDocument offer, BsonDocument shop, bool admin)
    {
        var pricing = Shop0172Price(offer, shop, 1);
        var map = new Dictionary<string, object>
        {
            ["offerId"] = Shop0172String(offer, "Id"),
            ["shopId"] = Shop0172String(offer, "ShopId"),
            ["displayName"] = Shop0172String(offer, "DisplayName"),
            ["publicDescription"] = Shop0172String(offer, "PublicDescription"),
            ["offerType"] = Shop0172String(offer, "OfferType", "Item"),
            ["currencyCode"] = Shop0172String(offer, "CurrencyCode", "credits"),
            ["rarity"] = Shop0172String(offer, "Rarity", "Common"),
            ["availability"] = pricing.Availability,
            ["stock"] = admin ? Shop0172Int(offer, "Stock") : Math.Min(Shop0172Int(offer, "Stock"), 10),
            ["stockDisplay"] = Shop0172StockDisplay(offer),
            ["legalStatus"] = Shop0172String(offer, "LegalStatus", "Free"),
            ["controlLevel"] = admin ? Shop0172Int(offer, "ControlLevel") : Math.Min(Shop0172Int(offer, "ControlLevel"), 3),
            ["requiresGmApproval"] = pricing.RequiresApproval,
            ["requiresProjectOrLicense"] = pricing.RequiresProjectOrLicense,
            ["finalUnitPrice"] = pricing.UnitPrice,
            ["priceSummary"] = pricing.PublicSummary,
            ["legalSummary"] = pricing.LegalSummary,
            ["riskSummary"] = pricing.RiskSummary,
            ["isPlayerVisible"] = admin ? Shop0172Bool(offer, "IsPlayerVisible", true) : true,
            ["linkedEntityType"] = Shop0172String(offer, "LinkedEntityType"),
            ["linkedEntityDisplayName"] = Shop0172String(offer, "LinkedEntityDisplayName")
        };
        if (admin)
        {
            map["itemDefinitionId"] = Shop0172String(offer, "ItemDefinitionId");
            map["gmDescription"] = Shop0172String(offer, "GMDescription");
            map["basePrice"] = Shop0172Decimal(offer, "BasePrice");
            map["reliability"] = Shop0172String(offer, "Reliability", "Normal");
            map["documentQuality"] = Shop0172String(offer, "DocumentQuality", "Clean");
            map["visibility"] = Shop0172String(offer, "Visibility", "Public");
            map["linkedEntityId"] = Shop0172String(offer, "LinkedEntityId");
            map["revision"] = Shop0172Int(offer, "Revision", 1);
        }
        return map;
    }

    private Dictionary<string, object> Shop0172PurchaseRequestPayload(BsonDocument doc, bool admin)
    {
        var map = new Dictionary<string, object>
        {
            ["requestId"] = Shop0172String(doc, "Id"),
            ["transactionType"] = Shop0172String(doc, "TransactionType", "Purchase"),
            ["campaignId"] = Shop0172String(doc, "CampaignId"),
            ["shopId"] = Shop0172String(doc, "ShopId"),
            ["offerId"] = Shop0172String(doc, "OfferId"),
            ["itemGrantId"] = Shop0172String(doc, "ItemGrantId"),
            ["buyerLogin"] = Shop0172String(doc, "BuyerLogin"),
            ["sellerLogin"] = Shop0172String(doc, "SellerLogin"),
            ["characterId"] = Shop0172String(doc, "CharacterId"),
            ["quantity"] = Shop0172Int(doc, "Quantity", 1),
            ["status"] = Shop0172String(doc, "Status"),
            ["finalUnitPrice"] = Shop0172Decimal(doc, "FinalUnitPrice"),
            ["finalTotalPrice"] = Shop0172Decimal(doc, "FinalTotalPrice"),
            ["currencyCode"] = Shop0172String(doc, "CurrencyCode", "credits"),
            ["availability"] = Shop0172String(doc, "Availability"),
            ["pricingSummary"] = Shop0172String(doc, "PricingSummary"),
            ["legalSummary"] = Shop0172String(doc, "LegalSummary"),
            ["riskSummary"] = Shop0172String(doc, "RiskSummary"),
            ["playerComment"] = Shop0172String(doc, "PlayerComment"),
            ["resolvedByLogin"] = Shop0172String(doc, "ResolvedByLogin"),
            ["inventoryDebitApplied"] = Shop0172Bool(doc, "InventoryDebitApplied"),
            ["walletCreditApplied"] = Shop0172Bool(doc, "WalletCreditApplied"),
            ["updatedAtUtc"] = Shop0172Date(doc, "UpdatedAtUtc")
        };
        if (admin)
        {
            map["buyerUserId"] = Shop0172String(doc, "BuyerUserId");
            map["gmComment"] = Shop0172String(doc, "GmComment");
            map["revision"] = Shop0172Int(doc, "Revision", 1);
        }
        return map;
    }

    private Dictionary<string, object> Shop0172ReceiptPayload(BsonDocument doc, bool admin)
    {
        var map = new Dictionary<string, object>
        {
            ["receiptId"] = Shop0172String(doc, "Id"),
            ["transactionType"] = Shop0172String(doc, "TransactionType", "Purchase"),
            ["purchaseRequestId"] = Shop0172String(doc, "PurchaseRequestId"),
            ["saleRequestId"] = Shop0172String(doc, "SaleRequestId"),
            ["itemGrantId"] = Shop0172String(doc, "ItemGrantId"),
            ["displayName"] = Shop0172String(doc, "DisplayName"),
            ["quantity"] = Shop0172Int(doc, "Quantity", 1),
            ["finalTotalPrice"] = Shop0172Decimal(doc, "FinalTotalPrice"),
            ["currencyCode"] = Shop0172String(doc, "CurrencyCode", "credits"),
            ["grantMode"] = Shop0172String(doc, "GrantMode"),
            ["walletApplyMode"] = Shop0172String(doc, "WalletApplyMode"),
            ["inventoryDebitApplied"] = Shop0172Bool(doc, "InventoryDebitApplied"),
            ["walletCreditApplied"] = Shop0172Bool(doc, "WalletCreditApplied"),
            ["createdAtUtc"] = Shop0172Date(doc, "CreatedAtUtc")
        };
        if (admin)
        {
            map["buyerUserId"] = Shop0172String(doc, "BuyerUserId");
            map["characterId"] = Shop0172String(doc, "CharacterId");
        }
        return map;
    }

    private Dictionary<string, object> Shop0172GrantPayload(BsonDocument doc, bool admin)
    {
        var map = new Dictionary<string, object>
        {
            ["grantId"] = Shop0172String(doc, "Id"),
            ["purchaseRequestId"] = Shop0172String(doc, "PurchaseRequestId"),
            ["purchaseReceiptId"] = Shop0172String(doc, "PurchaseReceiptId"),
            ["offerId"] = Shop0172String(doc, "OfferId"),
            ["displayName"] = Shop0172String(doc, "DisplayName"),
            ["quantity"] = Shop0172Int(doc, "Quantity", 1),
            ["status"] = Shop0172String(doc, "Status"),
            ["isSold"] = Shop0172Bool(doc, "IsSold"),
            ["soldBySaleRequestId"] = Shop0172String(doc, "SoldBySaleRequestId"),
            ["createdAtUtc"] = Shop0172Date(doc, "CreatedAtUtc")
        };
        if (admin)
        {
            map["buyerUserId"] = Shop0172String(doc, "BuyerUserId");
            map["characterId"] = Shop0172String(doc, "CharacterId");
            map["itemDefinitionId"] = Shop0172String(doc, "ItemDefinitionId");
        }
        return map;
    }

    private Dictionary<string, object> Shop0172AuditPayload(BsonDocument doc) => new()
    {
        ["auditId"] = Shop0172String(doc, "Id"),
        ["entityId"] = Shop0172String(doc, "EntityId"),
        ["action"] = Shop0172String(doc, "Action"),
        ["actorLogin"] = Shop0172String(doc, "ActorLogin"),
        ["summary"] = Shop0172String(doc, "Summary"),
        ["createdAtUtc"] = Shop0172Date(doc, "CreatedAtUtc")
    };

    private Shop0172PricingResult Shop0172Price(BsonDocument offer, BsonDocument shop, int quantity)
    {
        var basePrice = Shop0172Decimal(offer, "BasePrice");
        var rarity = Shop0172String(offer, "Rarity", "Common");
        var market = Shop0172String(shop, "MarketType", "White");
        var legal = Shop0172String(offer, "LegalStatus", "Free");
        var reliability = Shop0172String(offer, "Reliability", "Normal");
        var documents = Shop0172String(offer, "DocumentQuality", "Clean");
        var multiplier = Shop0172Multiplier(rarity, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Common"] = 1m, ["Ordinary"] = 1m, ["Specialized"] = 1.25m, ["Rare"] = 1.75m, ["VeryRare"] = 2.5m,
            ["Military"] = 3m, ["Unique"] = 5m, ["Anomalous"] = 8m
        });
        multiplier *= Shop0172Multiplier(market, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["White"] = 1m, ["Gray"] = 1.12m, ["Black"] = 1.35m });
        multiplier *= Shop0172Multiplier(reliability, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["Broken"] = 0.45m, ["Worn"] = 0.75m, ["Normal"] = 1m, ["Reliable"] = 1.15m, ["Prototype"] = 1.4m });
        multiplier *= Shop0172Multiplier(documents, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["Clean"] = 1m, ["Questionable"] = 1.08m, ["Fake"] = 1.2m, ["None"] = 1.3m });
        var unit = Math.Round(basePrice * multiplier, 2, MidpointRounding.AwayFromZero);
        var control = Shop0172Int(offer, "ControlLevel");
        var availability = Shop0172String(offer, "Availability", "Available");
        var offerType = Shop0172String(offer, "OfferType", "Item");
        var requiresProject = offerType.Equals("AssetRequestOnly", StringComparison.OrdinalIgnoreCase)
            || offerType.Equals("Asset", StringComparison.OrdinalIgnoreCase)
            || offerType.Equals("Project", StringComparison.OrdinalIgnoreCase)
            || availability.Equals("RequiresProject", StringComparison.OrdinalIgnoreCase)
            || rarity.Equals("Unique", StringComparison.OrdinalIgnoreCase)
            || rarity.Equals("Anomalous", StringComparison.OrdinalIgnoreCase);
        var requiresPersonnelReview = offerType.Equals("Slave", StringComparison.OrdinalIgnoreCase)
            || offerType.Equals("Companion", StringComparison.OrdinalIgnoreCase)
            || offerType.Equals("Personnel", StringComparison.OrdinalIgnoreCase)
            || offerType.Equals("Contract", StringComparison.OrdinalIgnoreCase);
        var requiresApproval = requiresProject
            || requiresPersonnelReview
            || Shop0172Bool(offer, "RequiresGmApproval")
            || Shop0172Bool(offer, "RequiresLicense")
            || Shop0172Bool(offer, "RequiresQuestOrContact")
            || control > 1
            || !market.Equals("White", StringComparison.OrdinalIgnoreCase)
            || !(legal.Equals("Free", StringComparison.OrdinalIgnoreCase) || legal.Equals("Registered", StringComparison.OrdinalIgnoreCase))
            || availability is "AskGm" or "RequiresLicense";
        var instant = !requiresApproval
            && !requiresProject
            && (availability.Equals("Available", StringComparison.OrdinalIgnoreCase) || availability.Equals("Limited", StringComparison.OrdinalIgnoreCase))
            && Shop0172Int(offer, "Stock") >= quantity;
        return new Shop0172PricingResult(unit, unit * quantity, availability, requiresApproval, requiresProject, instant,
            $"{basePrice:0.##} x {multiplier:0.##}",
            legal is "Free" or "Registered" ? "Обычная покупка." : "Требуется проверка GM.",
            requiresApproval ? "Покупка требует проверки GM." : "Без явных рисков.",
            control + (market.Equals("Black", StringComparison.OrdinalIgnoreCase) ? 3 : market.Equals("Gray", StringComparison.OrdinalIgnoreCase) ? 1 : 0));
    }

    private static decimal Shop0172Multiplier(string key, Dictionary<string, decimal> values) => values.TryGetValue(key, out var value) ? value : 1m;
    private static string Shop0172StockDisplay(BsonDocument offer) => Shop0172Int(offer, "Stock") <= 0 ? "Нет в наличии" : Shop0172Int(offer, "Stock") <= 3 ? "Ограничено" : "В наличии";

    private bool Shop0172RequiresSaleReview(BsonDocument? offer, BsonDocument? shop)
    {
        if (offer == null) return false;
        var rarity = Shop0172String(offer, "Rarity", "Common");
        var legal = Shop0172String(offer, "LegalStatus", "Free");
        var availability = Shop0172String(offer, "Availability", "Available");
        var offerType = Shop0172String(offer, "OfferType", "Item");
        var visibility = Shop0172String(offer, "Visibility", "Public");
        var market = shop == null ? "White" : Shop0172String(shop, "MarketType", "White");
        if (Shop0172Bool(offer, "RequiresGmApproval") || Shop0172Bool(offer, "RequiresLicense") || Shop0172Bool(offer, "RequiresQuestOrContact")) return true;
        if (Shop0172Int(offer, "ControlLevel") > 1) return true;
        if (rarity is "Rare" or "VeryRare" or "Military" or "Unique" or "Anomalous") return true;
        if (offerType is "AssetRequestOnly" or "Asset" or "Project" or "Slave" or "Companion" or "Personnel" or "Contract") return true;
        if (availability is "AskGm" or "RequiresLicense" or "RequiresProject" or "Hidden") return true;
        if (visibility is "Hidden" or "GmOnly") return true;
        if (!(legal.Equals("Free", StringComparison.OrdinalIgnoreCase) || legal.Equals("Registered", StringComparison.OrdinalIgnoreCase))) return true;
        return !market.Equals("White", StringComparison.OrdinalIgnoreCase);
    }

    private bool Shop0172CanPlayerSeeShop(BsonDocument shop)
    {
        if (Shop0172Bool(shop, "IsArchived")) return false;
        if (!Shop0172Bool(shop, "IsPlayerVisible", true)) return false;
        var visibility = Shop0172String(shop, "Visibility", "Public");
        return visibility is "Public" or "PlayerVisible" or "PartyKnown" or "CharacterKnown";
    }

    private bool Shop0172CanPlayerSeeOffer(BsonDocument offer, BsonDocument shop)
    {
        if (!Shop0172CanPlayerSeeShop(shop)) return false;
        if (Shop0172Bool(offer, "IsArchived")) return false;
        if (!Shop0172Bool(offer, "IsPlayerVisible", true)) return false;
        var visibility = Shop0172String(offer, "Visibility", "Public");
        if (visibility is "Hidden" or "GmOnly") return false;
        var availability = Shop0172String(offer, "Availability", "Available");
        return !availability.Equals("Hidden", StringComparison.OrdinalIgnoreCase);
    }

    private bool Shop0172IsOwnedCharacter(string userId, string characterId)
    {
        return _repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId) & Builders<Character>.Filter.Eq(x => x.OwnerUserId, userId)).Any();
    }

    private void Shop0172Audit(UserAccount actor, string command, string entityId, string action, BsonDocument? before, BsonDocument? after, string summary)
    {
        var audit = new BsonDocument
        {
            ["Id"] = Shop0172NewId("shop_audit"),
            ["EntityId"] = entityId,
            ["ActorUserId"] = actor.Id,
            ["ActorLogin"] = actor.Login,
            ["Command"] = command,
            ["Action"] = action,
            ["Summary"] = summary,
            ["Before"] = before == null ? BsonNull.Value : Shop0172SafeAuditDoc(before),
            ["After"] = after == null ? BsonNull.Value : Shop0172SafeAuditDoc(after),
            ["CreatedAtUtc"] = DateTime.UtcNow
        };
        ShopAuditEvents0172().InsertOne(audit);
        WriteAudit("shops", actor.Id, action, entityId);
    }

    private void Shop0172Sync(string type, string entityType, string entityId, string operation, string actorUserId, string? requestId)
    {
        TryPublishSyncEvent(type, "shops", entityType, entityId, operation, actorUserId, new Dictionary<string, object>
        {
            ["entityType"] = entityType,
            ["entityId"] = entityId,
            ["operation"] = operation
        }, requestId ?? string.Empty);
    }

    private void EnsureShop0172Indexes()
    {
        if (_shop0172IndexesEnsured) return;
        ShopDefinitions0172().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId").Ascending("IsArchived")));
        ShopInstances0172().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId").Ascending("IsArchived")));
        ShopInstances0172().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Visibility").Ascending("IsPlayerVisible")));
        ShopOffers0172().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("ShopId").Ascending("IsArchived")));
        ShopOffers0172().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId").Ascending("IsPlayerVisible")));
        PurchaseRequests0172().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId").Ascending("Status")));
        PurchaseRequests0172().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("BuyerUserId").Descending("UpdatedAtUtc")));
        PurchaseReceipts0172().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("PurchaseRequestId")));
        PurchaseGrants0172().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("PurchaseRequestId")));
        ShopAuditEvents0172().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("EntityId").Descending("CreatedAtUtc")));
        _shop0172IndexesEnsured = true;
    }

    private BsonDocument Shop0172RequireShop(IDictionary<string, object> payload, bool includeArchived = false)
    {
        var id = Shop0172RequiredId(payload, "shopId");
        var shop = Shop0172FindShop(id);
        if (shop == null || (!includeArchived && Shop0172Bool(shop, "IsArchived"))) throw new KeyNotFoundException("Shop not found.");
        return shop;
    }

    private BsonDocument Shop0172RequireOffer(IDictionary<string, object> payload, bool includeArchived = false)
    {
        var id = Shop0172RequiredId(payload, "offerId");
        var offer = Shop0172FindOffer(id);
        if (offer == null || (!includeArchived && Shop0172Bool(offer, "IsArchived"))) throw new KeyNotFoundException("Offer not found.");
        return offer;
    }

    private BsonDocument Shop0172RequirePurchaseRequest(IDictionary<string, object> payload)
    {
        var id = Shop0172RequiredId(payload, "requestId");
        var request = PurchaseRequests0172().Find(Shop0172IdFilter(id)).FirstOrDefault();
        if (request == null || Shop0172Bool(request, "IsArchived")) throw new KeyNotFoundException("Purchase request not found.");
        return request;
    }

    private BsonDocument? Shop0172FindShop(string shopId) => ShopInstances0172().Find(Shop0172IdFilter(shopId)).FirstOrDefault();
    private BsonDocument? Shop0172FindOffer(string offerId) => ShopOffers0172().Find(Shop0172IdFilter(offerId)).FirstOrDefault();

    private IMongoCollection<BsonDocument> ShopDefinitions0172() => _mongo.Database.GetCollection<BsonDocument>(ShopDefinitions0172Collection);
    private IMongoCollection<BsonDocument> ShopInstances0172() => _mongo.Database.GetCollection<BsonDocument>(ShopInstances0172Collection);
    private IMongoCollection<BsonDocument> ShopOffers0172() => _mongo.Database.GetCollection<BsonDocument>(ShopOffers0172Collection);
    private IMongoCollection<BsonDocument> PurchaseRequests0172() => _mongo.Database.GetCollection<BsonDocument>(PurchaseRequests0172Collection);
    private IMongoCollection<BsonDocument> PurchaseReceipts0172() => _mongo.Database.GetCollection<BsonDocument>(PurchaseReceipts0172Collection);
    private IMongoCollection<BsonDocument> PurchaseGrants0172() => _mongo.Database.GetCollection<BsonDocument>(PurchaseGrants0172Collection);
    private IMongoCollection<BsonDocument> ShopAuditEvents0172() => _mongo.Database.GetCollection<BsonDocument>(ShopAuditEvents0172Collection);

    private static FilterDefinition<BsonDocument> Shop0172IdFilter(string id) => Builders<BsonDocument>.Filter.Eq("Id", id);
    private static string Shop0172NewId(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N");
    private static string Shop0172RequiredId(IDictionary<string, object> payload, string key) => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, key), PayloadReader.GetString(payload, "id")), 1, 128, key);
    private static string Shop0172CampaignId(IDictionary<string, object> payload) => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), "dev-campaign-core"), 1, 128, "campaignId");
    private static string Shop0172Text(IDictionary<string, object> payload, string key, string fallback, int max, bool required = false) => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, key), fallback), required ? 1 : 0, max, key);

    private static string Shop0172String(BsonDocument doc, string key, string fallback = "")
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return fallback;
        if (doc[key].IsString) return doc[key].AsString;
        return Convert.ToString(BsonTypeMapper.MapToDotNetValue(doc[key]), CultureInfo.InvariantCulture) ?? fallback;
    }

    private static int Shop0172Int(BsonDocument doc, string key, int fallback = 0)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return fallback;
        if (doc[key].IsInt32) return doc[key].AsInt32;
        if (doc[key].IsInt64) return (int)doc[key].AsInt64;
        return int.TryParse(Shop0172String(doc, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static decimal Shop0172Decimal(BsonDocument doc, string key, decimal fallback = 0m)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return fallback;
        var value = doc[key];
        if (value.IsDecimal128) return (decimal)value.AsDecimal128;
        if (value.IsDouble) return (decimal)value.AsDouble;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        return decimal.TryParse(Shop0172String(doc, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static decimal Shop0172PayloadDecimal(IDictionary<string, object> payload, string key, decimal fallback = 0m)
    {
        if (!payload.TryGetValue(key, out var raw) || raw == null) return fallback;
        return decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static bool Shop0172Bool(BsonDocument doc, string key, bool fallback = false)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return fallback;
        if (doc[key].IsBoolean) return doc[key].AsBoolean;
        return bool.TryParse(Shop0172String(doc, key), out var value) ? value : fallback;
    }

    private static string Shop0172Date(BsonDocument doc, string key)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return string.Empty;
        if (doc[key].IsValidDateTime) return doc[key].ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        return Shop0172String(doc, key);
    }

    private static BsonArray Shop0172Array(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var raw) || raw == null) return new BsonArray();
        if (raw is IEnumerable<object> list) return new BsonArray(list.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture) ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)));
        return new BsonArray((Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string Shop0172NormalizeOneOf(string value, string[] allowed, string fallback) => allowed.FirstOrDefault(x => x.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? fallback;
    private static void Shop0172Touch(BsonDocument doc, string actorId)
    {
        doc["UpdatedAtUtc"] = DateTime.UtcNow;
        doc["UpdatedByUserId"] = actorId;
        doc["Revision"] = Shop0172Int(doc, "Revision") + 1;
    }

    private static BsonDocument Shop0172SafeAuditDoc(BsonDocument source)
    {
        var clone = source.DeepClone().AsBsonDocument;
        clone.Remove("ServerOnlyData");
        return clone;
    }

    private static readonly string[] Shop0172MarketTypes = { "White", "Gray", "Black" };
    private static readonly string[] Shop0172VisibilityModes = { "Public", "PlayerVisible", "PartyKnown", "CharacterKnown", "GmOnly", "Hidden" };
    private static readonly string[] Shop0172OfferTypes = { "Item", "Service", "Consumable", "Equipment", "AssetRequestOnly", "Asset", "Project", "Information", "Personnel", "Companion", "Slave", "Contract", "Custom" };
    private static readonly string[] Shop0172Rarities = { "Common", "Ordinary", "Specialized", "Rare", "VeryRare", "Military", "Unique", "Anomalous" };
    private static readonly string[] Shop0172Availabilities = { "Available", "Limited", "AskGm", "RequiresLicense", "RequiresProject", "Hidden" };
    private static readonly string[] Shop0172LegalStatuses = { "Free", "Registered", "Licensed", "Restricted", "MilitaryOnly", "Forbidden", "ExistentialThreat" };
    private static readonly string[] Shop0172Reliabilities = { "Broken", "Worn", "Normal", "Reliable", "Prototype" };
    private static readonly string[] Shop0172DocumentQualities = { "Clean", "Questionable", "Fake", "None" };

    private sealed class Shop0172PricingResult
    {
        public Shop0172PricingResult(decimal unitPrice, decimal totalPrice, string availability, bool requiresApproval, bool requiresProjectOrLicense, bool instantAllowed, string publicSummary, string legalSummary, string riskSummary, int suspicion)
        {
            UnitPrice = unitPrice;
            TotalPrice = totalPrice;
            Availability = availability;
            RequiresApproval = requiresApproval;
            RequiresProjectOrLicense = requiresProjectOrLicense;
            InstantAllowed = instantAllowed;
            PublicSummary = publicSummary;
            LegalSummary = legalSummary;
            RiskSummary = riskSummary;
            Suspicion = suspicion;
        }

        public decimal UnitPrice { get; }
        public decimal TotalPrice { get; }
        public string Availability { get; }
        public bool RequiresApproval { get; }
        public bool RequiresProjectOrLicense { get; }
        public bool InstantAllowed { get; }
        public string PublicSummary { get; }
        public string LegalSummary { get; }
        public string RiskSummary { get; }
        public int Suspicion { get; }
    }
}
