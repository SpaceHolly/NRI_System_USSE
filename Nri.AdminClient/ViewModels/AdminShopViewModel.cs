using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminShopViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private ShopRow? _selectedShop;
    private ShopOfferRow? _selectedOffer;
    private PurchaseRequestRow? _selectedPurchaseRequest;
    private string _statusMessage = "Магазины не загружены.";
    private string _campaignId = "dev-campaign-core";
    private string _newShopName = "Магазин 0.17.2";
    private string _newShopDescription = "Тестовая витрина для простых покупок.";
    private string _marketType = "White";
    private bool _shopPlayerVisible = true;
    private string _offerName = "Аптечка";
    private string _offerDescription = "Базовый расходник.";
    private string _offerType = "Item";
    private string _basePrice = "25";
    private string _currencyCode = "credits";
    private string _rarity = "Common";
    private string _availability = "Available";
    private int _stock = 5;
    private string _legalStatus = "Free";
    private int _controlLevel;
    private bool _requiresGmApproval;
    private bool _offerPlayerVisible = true;
    private string _gmComment = string.Empty;

    public AdminShopViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
        CreateShopCommand = new RelayCommand(CreateShop);
        SaveShopCommand = new RelayCommand(SaveShop);
        CreateOfferCommand = new RelayCommand(CreateOffer);
        SaveOfferCommand = new RelayCommand(SaveOffer);
        AdjustStockCommand = new RelayCommand(AdjustStock);
        ApprovePurchaseCommand = new RelayCommand(ApprovePurchase);
        RejectPurchaseCommand = new RelayCommand(RejectPurchase);
        CompletePurchaseCommand = new RelayCommand(CompletePurchase);
        MarkRequiresProjectCommand = new RelayCommand(MarkRequiresProject);
    }

    public ObservableCollection<ShopRow> Shops { get; } = new();
    public ObservableCollection<ShopOfferRow> Offers { get; } = new();
    public ObservableCollection<PurchaseRequestRow> PurchaseRequests { get; } = new();
    public ObservableCollection<string> AuditRows { get; } = new();
    public string[] MarketTypes { get; } = { "White", "Gray", "Black" };
    public string[] OfferTypes { get; } = { "Item", "Service", "Consumable", "Equipment", "AssetRequestOnly", "Information", "Custom" };
    public string[] Rarities { get; } = { "Common", "Ordinary", "Specialized", "Rare", "VeryRare", "Military", "Unique", "Anomalous" };
    public string[] Availabilities { get; } = { "Available", "Limited", "AskGm", "RequiresLicense", "RequiresProject", "Hidden" };
    public string[] LegalStatuses { get; } = { "Free", "Registered", "Licensed", "Restricted", "MilitaryOnly", "Forbidden", "ExistentialThreat" };

    public ICommand RefreshCommand { get; }
    public ICommand CreateShopCommand { get; }
    public ICommand SaveShopCommand { get; }
    public ICommand CreateOfferCommand { get; }
    public ICommand SaveOfferCommand { get; }
    public ICommand AdjustStockCommand { get; }
    public ICommand ApprovePurchaseCommand { get; }
    public ICommand RejectPurchaseCommand { get; }
    public ICommand CompletePurchaseCommand { get; }
    public ICommand MarkRequiresProjectCommand { get; }

    public string CampaignId { get => _campaignId; set { _campaignId = value; Notify(); } }
    public string NewShopName { get => _newShopName; set { _newShopName = value; Notify(); } }
    public string NewShopDescription { get => _newShopDescription; set { _newShopDescription = value; Notify(); } }
    public string MarketType { get => _marketType; set { _marketType = value; Notify(); } }
    public bool ShopPlayerVisible { get => _shopPlayerVisible; set { _shopPlayerVisible = value; Notify(); } }
    public string OfferName { get => _offerName; set { _offerName = value; Notify(); } }
    public string OfferDescription { get => _offerDescription; set { _offerDescription = value; Notify(); } }
    public string OfferType { get => _offerType; set { _offerType = value; Notify(); } }
    public string BasePrice { get => _basePrice; set { _basePrice = value; Notify(); } }
    public string CurrencyCode { get => _currencyCode; set { _currencyCode = value; Notify(); } }
    public string Rarity { get => _rarity; set { _rarity = value; Notify(); } }
    public string Availability { get => _availability; set { _availability = value; Notify(); } }
    public int Stock { get => _stock; set { _stock = Math.Max(0, value); Notify(); } }
    public string LegalStatus { get => _legalStatus; set { _legalStatus = value; Notify(); } }
    public int ControlLevel { get => _controlLevel; set { _controlLevel = Math.Max(0, value); Notify(); } }
    public bool RequiresGmApproval { get => _requiresGmApproval; set { _requiresGmApproval = value; Notify(); } }
    public bool OfferPlayerVisible { get => _offerPlayerVisible; set { _offerPlayerVisible = value; Notify(); } }
    public string GmComment { get => _gmComment; set { _gmComment = value; Notify(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; Notify(); } }

    public ShopRow? SelectedShop
    {
        get => _selectedShop;
        set
        {
            _selectedShop = value;
            Notify();
            if (value != null)
            {
                NewShopName = value.Name;
                NewShopDescription = value.Description;
                MarketType = string.IsNullOrWhiteSpace(value.MarketType) ? "White" : value.MarketType;
                ShopPlayerVisible = value.IsPlayerVisible;
                LoadShop(value.ShopId);
            }
        }
    }

    public ShopOfferRow? SelectedOffer
    {
        get => _selectedOffer;
        set
        {
            _selectedOffer = value;
            Notify();
            if (value != null)
            {
                OfferName = value.DisplayName;
                OfferDescription = value.PublicDescription;
                OfferType = string.IsNullOrWhiteSpace(value.OfferType) ? "Item" : value.OfferType;
                BasePrice = value.BasePrice <= 0 ? value.FinalUnitPrice.ToString("0.##") : value.BasePrice.ToString("0.##");
                CurrencyCode = value.CurrencyCode;
                Rarity = value.Rarity;
                Availability = value.Availability;
                Stock = value.Stock;
                LegalStatus = value.LegalStatus;
                ControlLevel = value.ControlLevel;
                RequiresGmApproval = value.RequiresGmApproval;
                OfferPlayerVisible = value.IsPlayerVisible;
            }
        }
    }

    public PurchaseRequestRow? SelectedPurchaseRequest
    {
        get => _selectedPurchaseRequest;
        set { _selectedPurchaseRequest = value; Notify(); }
    }

    public void Refresh()
    {
        Run("Загрузка магазинов", () =>
        {
            var response = _api.ShopAdminList(new Dictionary<string, object> { ["campaignId"] = CampaignId });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            Shops.Clear();
            foreach (var item in List(response.Payload, "items").Select(Map))
                Shops.Add(ShopRow.From(item));
            RefreshRequests();
            StatusMessage = $"Магазинов: {Shops.Count}.";
        });
    }

    private void RefreshRequests()
    {
        var response = _api.ShopAdminListPurchaseRequests(new Dictionary<string, object> { ["campaignId"] = CampaignId });
        if (response.Status != ResponseStatus.Ok) return;
        PurchaseRequests.Clear();
        foreach (var item in List(response.Payload, "items").Select(Map))
            PurchaseRequests.Add(PurchaseRequestRow.From(item));
    }

    private void LoadShop(string shopId)
    {
        Run("Загрузка магазина", () =>
        {
            var response = _api.ShopAdminGet(shopId);
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            Offers.Clear();
            foreach (var item in List(response.Payload, "offers").Select(Map))
                Offers.Add(ShopOfferRow.From(item));
            PurchaseRequests.Clear();
            foreach (var item in List(response.Payload, "purchaseRequests").Select(Map))
                PurchaseRequests.Add(PurchaseRequestRow.From(item));
            LoadAudit(shopId);
            StatusMessage = $"Магазин открыт: {NewShopName}.";
        });
    }

    private void LoadAudit(string entityId)
    {
        var response = _api.ShopAdminGetAudit(entityId);
        if (response.Status != ResponseStatus.Ok) return;
        AuditRows.Clear();
        foreach (var item in List(response.Payload, "items").Select(Map))
            AuditRows.Add($"{S(item, "createdAtUtc")} | {S(item, "actorLogin")} | {S(item, "action")} | {S(item, "summary")}");
    }

    private void CreateShop()
    {
        Run("Создание магазина", () =>
        {
            var response = _api.ShopAdminCreateShop(new Dictionary<string, object>
            {
                ["campaignId"] = CampaignId,
                ["name"] = NewShopName,
                ["description"] = NewShopDescription,
                ["marketType"] = MarketType,
                ["visibility"] = ShopPlayerVisible ? "Public" : "GmOnly",
                ["isPlayerVisible"] = ShopPlayerVisible
            });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            Refresh();
            StatusMessage = "Магазин создан.";
        });
    }

    private void SaveShop()
    {
        if (SelectedShop == null) return;
        Run("Сохранение магазина", () =>
        {
            var response = _api.ShopAdminUpdateShop(new Dictionary<string, object>
            {
                ["shopId"] = SelectedShop.ShopId,
                ["name"] = NewShopName,
                ["description"] = NewShopDescription,
                ["marketType"] = MarketType,
                ["visibility"] = ShopPlayerVisible ? "Public" : "GmOnly",
                ["isPlayerVisible"] = ShopPlayerVisible
            });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadShop(SelectedShop.ShopId);
            StatusMessage = "Магазин сохранён.";
        });
    }

    private void CreateOffer()
    {
        if (SelectedShop == null) return;
        Run("Создание предложения", () =>
        {
            var response = _api.ShopAdminCreateOffer(OfferPayload(SelectedShop.ShopId));
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadShop(SelectedShop.ShopId);
            StatusMessage = "Предложение создано.";
        });
    }

    private void SaveOffer()
    {
        if (SelectedOffer == null || SelectedShop == null) return;
        Run("Сохранение предложения", () =>
        {
            var payload = OfferPayload(SelectedShop.ShopId);
            payload["offerId"] = SelectedOffer.OfferId;
            var response = _api.ShopAdminUpdateOffer(payload);
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadShop(SelectedShop.ShopId);
            StatusMessage = "Предложение сохранено.";
        });
    }

    private void AdjustStock()
    {
        if (SelectedOffer == null || SelectedShop == null) return;
        Run("Обновление остатка", () =>
        {
            var response = _api.ShopAdminAdjustStock(new Dictionary<string, object> { ["offerId"] = SelectedOffer.OfferId, ["stock"] = Stock, ["mode"] = "set" });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadShop(SelectedShop.ShopId);
            StatusMessage = "Остаток обновлён.";
        });
    }

    private void ApprovePurchase()
    {
        if (!ConfirmPurchase("Одобрить покупку", "Покупка будет одобрена и сможет изменить деньги, остаток и инвентарь. Продолжить?")) return;
        PurchaseAction("Одобрение покупки", () => _api.ShopAdminApprovePurchase(SelectedPurchaseRequest!.RequestId, GmComment));
    }
    private void RejectPurchase()
    {
        if (!ConfirmPurchase("Отклонить покупку", "Покупка будет отклонена без изменения денег и инвентаря. Продолжить?")) return;
        PurchaseAction("Отклонение покупки", () => _api.ShopAdminRejectPurchase(SelectedPurchaseRequest!.RequestId, GmComment));
    }
    private void CompletePurchase()
    {
        if (!ConfirmPurchase("Завершить покупку", "Транзакция будет окончательно завершена. Повторное применение не допускается. Продолжить?")) return;
        PurchaseAction("Завершение покупки", () => _api.ShopAdminCompletePurchase(SelectedPurchaseRequest!.RequestId));
    }
    private void MarkRequiresProject()
    {
        if (!ConfirmPurchase("Перевести в проект", "Операция будет направлена в отдельный проект или лицензионное рассмотрение. Продолжить?")) return;
        PurchaseAction("Перевод в проект/лицензию", () => _api.ShopAdminMarkRequiresProject(SelectedPurchaseRequest!.RequestId, GmComment));
    }

    private static bool ConfirmPurchase(string title, string message)
        => System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
           == System.Windows.MessageBoxResult.Yes;

    private void PurchaseAction(string name, Func<ResponseEnvelope> action)
    {
        if (SelectedPurchaseRequest == null) return;
        Run(name, () =>
        {
            var response = action();
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            RefreshRequests();
            if (SelectedShop != null) LoadShop(SelectedShop.ShopId);
            StatusMessage = response.Message;
        });
    }

    private Dictionary<string, object> OfferPayload(string shopId) => new()
    {
        ["shopId"] = shopId,
        ["displayName"] = OfferName,
        ["publicDescription"] = OfferDescription,
        ["offerType"] = OfferType,
        ["basePrice"] = decimal.TryParse(BasePrice, out var price) ? price : 0m,
        ["currencyCode"] = CurrencyCode,
        ["rarity"] = Rarity,
        ["availability"] = Availability,
        ["stock"] = Stock,
        ["legalStatus"] = LegalStatus,
        ["controlLevel"] = ControlLevel,
        ["requiresGmApproval"] = RequiresGmApproval,
        ["visibility"] = OfferPlayerVisible ? "Public" : "GmOnly",
        ["isPlayerVisible"] = OfferPlayerVisible
    };

    private void Run(string operation, Action action)
    {
        try
        {
            ClientLogService.Instance.Info($"admin.shop.{operation}.start");
            action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ClientLogService.Instance.Error($"admin.shop.{operation}.error", ex);
        }
    }

    private static Dictionary<string, object> Map(object? raw) => raw as Dictionary<string, object> ?? new Dictionary<string, object>();
    private static IEnumerable<object> List(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) && raw is IEnumerable list ? list.Cast<object>() : Array.Empty<object>();
    private static string S(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;
    private static int I(Dictionary<string, object> map, string key) => int.TryParse(S(map, key), out var value) ? value : 0;
    private static decimal D(Dictionary<string, object> map, string key) => decimal.TryParse(S(map, key), out var value) ? value : 0m;

    public sealed class ShopRow
    {
        public string ShopId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MarketType { get; set; } = string.Empty;
        public bool IsPlayerVisible { get; set; }
        public string Summary => $"{Name} | {MarketType} | {(IsPlayerVisible ? "виден игрокам" : "GM-only")}";
        public static ShopRow From(Dictionary<string, object> map) => new() { ShopId = S(map, "shopId"), Name = S(map, "name"), Description = S(map, "description"), MarketType = S(map, "marketType"), IsPlayerVisible = S(map, "isPlayerVisible").Equals("True", StringComparison.OrdinalIgnoreCase) };
    }

    public sealed class ShopOfferRow
    {
        public string OfferId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PublicDescription { get; set; } = string.Empty;
        public string OfferType { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public decimal FinalUnitPrice { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string Rarity { get; set; } = string.Empty;
        public string Availability { get; set; } = string.Empty;
        public int Stock { get; set; }
        public string LegalStatus { get; set; } = string.Empty;
        public int ControlLevel { get; set; }
        public bool RequiresGmApproval { get; set; }
        public bool RequiresProjectOrLicense { get; set; }
        public bool IsPlayerVisible { get; set; }
        public string PriceSummary { get; set; } = string.Empty;
        public string Summary => $"{DisplayName} | {FinalUnitPrice:0.##} {CurrencyCode} | {Availability} | остаток {Stock}";
        public static ShopOfferRow From(Dictionary<string, object> map) => new()
        {
            OfferId = S(map, "offerId"),
            DisplayName = S(map, "displayName"),
            PublicDescription = S(map, "publicDescription"),
            OfferType = S(map, "offerType"),
            BasePrice = D(map, "basePrice"),
            FinalUnitPrice = D(map, "finalUnitPrice"),
            CurrencyCode = S(map, "currencyCode"),
            Rarity = S(map, "rarity"),
            Availability = S(map, "availability"),
            Stock = I(map, "stock"),
            LegalStatus = S(map, "legalStatus"),
            ControlLevel = I(map, "controlLevel"),
            RequiresGmApproval = S(map, "requiresGmApproval").Equals("True", StringComparison.OrdinalIgnoreCase),
            RequiresProjectOrLicense = S(map, "requiresProjectOrLicense").Equals("True", StringComparison.OrdinalIgnoreCase),
            IsPlayerVisible = S(map, "isPlayerVisible").Equals("True", StringComparison.OrdinalIgnoreCase),
            PriceSummary = S(map, "priceSummary")
        };
    }

    public sealed class PurchaseRequestRow
    {
        public string RequestId { get; set; } = string.Empty;
        public string BuyerLogin { get; set; } = string.Empty;
        public string OfferId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal FinalTotalPrice { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string Summary => $"{Status} | {BuyerLogin} | x{Quantity} | {FinalTotalPrice:0.##} {CurrencyCode}";
        public static PurchaseRequestRow From(Dictionary<string, object> map) => new() { RequestId = S(map, "requestId"), BuyerLogin = S(map, "buyerLogin"), OfferId = S(map, "offerId"), Quantity = I(map, "quantity"), Status = S(map, "status"), FinalTotalPrice = D(map, "finalTotalPrice"), CurrencyCode = S(map, "currencyCode") };
    }
}
