using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerShopViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterId;
    private PlayerShopRow? _selectedShop;
    private PlayerOfferRow? _selectedOffer;
    private string _statusMessage = "Магазины не загружены.";
    private string _campaignId = "dev-campaign-core";
    private int _quantity = 1;
    private string _comment = string.Empty;

    public PlayerShopViewModel(CommandApi api, Func<string> activeCharacterId)
    {
        _api = api;
        _activeCharacterId = activeCharacterId;
        RefreshCommand = new RelayCommand(Refresh);
        RequestPurchaseCommand = new RelayCommand(RequestPurchase);
        RefreshHistoryCommand = new RelayCommand(RefreshHistory);
    }

    public ObservableCollection<PlayerShopRow> Shops { get; } = new();
    public ObservableCollection<PlayerOfferRow> Offers { get; } = new();
    public ObservableCollection<PurchaseHistoryRow> History { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand RequestPurchaseCommand { get; }
    public ICommand RefreshHistoryCommand { get; }

    public string CampaignId { get => _campaignId; set { _campaignId = value; Notify(); } }
    public int Quantity { get => _quantity; set { _quantity = Math.Max(1, value); Notify(); } }
    public string Comment { get => _comment; set { _comment = value; Notify(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; Notify(); } }

    public PlayerShopRow? SelectedShop
    {
        get => _selectedShop;
        set
        {
            _selectedShop = value;
            Notify();
            if (value != null) LoadOffers(value.ShopId);
        }
    }

    public PlayerOfferRow? SelectedOffer
    {
        get => _selectedOffer;
        set { _selectedOffer = value; Notify(); }
    }

    public void Refresh()
    {
        Run("load", () =>
        {
            var response = _api.ShopPlayerListShops(new Dictionary<string, object> { ["campaignId"] = CampaignId });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            Shops.Clear();
            foreach (var item in List(response.Payload, "items").Select(Map))
                Shops.Add(PlayerShopRow.From(item));
            RefreshHistory();
            StatusMessage = Shops.Count == 0 ? "GM ещё не открыл магазины игрокам." : $"Доступно магазинов: {Shops.Count}.";
        });
    }

    private void LoadOffers(string shopId)
    {
        Run("offers", () =>
        {
            var response = _api.ShopPlayerListOffers(shopId);
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            Offers.Clear();
            foreach (var item in List(response.Payload, "items").Select(Map))
                Offers.Add(PlayerOfferRow.From(item));
            StatusMessage = Offers.Count == 0 ? "В этом магазине нет видимых предложений." : $"Предложений: {Offers.Count}.";
        });
    }

    private void RequestPurchase()
    {
        if (SelectedOffer == null) return;
        var confirmation = MessageBox.Show(
            $"Отправить запрос на покупку?\n\n{SelectedOffer.DisplayName}\nКоличество: {Quantity}\nИтого: {SelectedOffer.FinalUnitPrice * Quantity:0.##} {SelectedOffer.CurrencyCode}",
            "Подтверждение покупки",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            StatusMessage = "Запрос на покупку отменён.";
            return;
        }
        Run("request", () =>
        {
            var response = _api.ShopPlayerRequestPurchase(new Dictionary<string, object>
            {
                ["offerId"] = SelectedOffer.OfferId,
                ["characterId"] = _activeCharacterId(),
                ["quantity"] = Quantity,
                ["comment"] = Comment
            });
            StatusMessage = response.Message;
            if (response.Status == ResponseStatus.Ok)
            {
                if (SelectedShop != null) LoadOffers(SelectedShop.ShopId);
                RefreshHistory();
            }
        });
    }

    private void RefreshHistory()
    {
        Run("history", () =>
        {
            var response = _api.ShopPlayerPurchaseHistory(new Dictionary<string, object> { ["campaignId"] = CampaignId });
            if (response.Status != ResponseStatus.Ok) return;
            History.Clear();
            foreach (var item in List(response.Payload, "requests").Select(Map))
                History.Add(PurchaseHistoryRow.From(item));
            foreach (var item in List(response.Payload, "receipts").Select(Map))
                History.Add(PurchaseHistoryRow.FromReceipt(item));
        });
    }

    private void Run(string operation, Action action)
    {
        try
        {
            ClientLogService.Instance.Info($"player.shop.{operation}.start");
            action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ClientLogService.Instance.Error($"player.shop.{operation}.error", ex);
        }
    }

    private static Dictionary<string, object> Map(object? raw) => raw as Dictionary<string, object> ?? new Dictionary<string, object>();
    private static IEnumerable<object> List(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) && raw is IEnumerable list ? list.Cast<object>() : Array.Empty<object>();
    private static string S(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;
    private static int I(Dictionary<string, object> map, string key) => int.TryParse(S(map, key), out var value) ? value : 0;
    private static decimal D(Dictionary<string, object> map, string key) => decimal.TryParse(S(map, key), out var value) ? value : 0m;

    public sealed class PlayerShopRow
    {
        public string ShopId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MarketType { get; set; } = string.Empty;
        public string Summary => $"{Name} | {PlayerDevelopmentGraphDisplay.ToReadableText(MarketType)}";
        public static PlayerShopRow From(Dictionary<string, object> map) => new() { ShopId = S(map, "shopId"), Name = S(map, "name"), Description = S(map, "description"), MarketType = S(map, "marketType") };
    }

    public sealed class PlayerOfferRow
    {
        public string OfferId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PublicDescription { get; set; } = string.Empty;
        public decimal FinalUnitPrice { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string Availability { get; set; } = string.Empty;
        public string StockDisplay { get; set; } = string.Empty;
        public string LegalSummary { get; set; } = string.Empty;
        public string RiskSummary { get; set; } = string.Empty;
        public bool RequiresGmApproval { get; set; }
        public bool RequiresProjectOrLicense { get; set; }
        public string AvailabilityLabel => PlayerDevelopmentGraphDisplay.ToReadableText(Availability);
        public string Summary => $"{DisplayName} | {FinalUnitPrice:0.##} {CurrencyCode} | {AvailabilityLabel}";
        public static PlayerOfferRow From(Dictionary<string, object> map) => new()
        {
            OfferId = S(map, "offerId"),
            DisplayName = S(map, "displayName"),
            PublicDescription = S(map, "publicDescription"),
            FinalUnitPrice = D(map, "finalUnitPrice"),
            CurrencyCode = S(map, "currencyCode"),
            Availability = S(map, "availability"),
            StockDisplay = S(map, "stockDisplay"),
            LegalSummary = S(map, "legalSummary"),
            RiskSummary = S(map, "riskSummary"),
            RequiresGmApproval = S(map, "requiresGmApproval").Equals("True", StringComparison.OrdinalIgnoreCase),
            RequiresProjectOrLicense = S(map, "requiresProjectOrLicense").Equals("True", StringComparison.OrdinalIgnoreCase)
        };
    }

    public sealed class PurchaseHistoryRow
    {
        public string Kind { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string Summary => $"{Kind} | {PlayerDevelopmentGraphDisplay.ToReadableText(Status)} | {Price:0.##} {CurrencyCode}";
        public static PurchaseHistoryRow From(Dictionary<string, object> map) => new() { Kind = "Заявка", Id = S(map, "requestId"), Status = S(map, "status"), Price = D(map, "finalTotalPrice"), CurrencyCode = S(map, "currencyCode") };
        public static PurchaseHistoryRow FromReceipt(Dictionary<string, object> map) => new() { Kind = "Чек", Id = S(map, "receiptId"), Status = S(map, "grantMode"), Price = D(map, "finalTotalPrice"), CurrencyCode = S(map, "currencyCode") };
    }
}
