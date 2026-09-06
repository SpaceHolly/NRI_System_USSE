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

public sealed class AdminGameplayViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "dev-campaign-core";
    private string _statusMessage = "Игровой цикл не загружен.";
    private GameplayQueueRow? _selectedQueueItem;

    public AdminGameplayViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
        ResolveSelectedCommand = new RelayCommand(ResolveSelected);
    }

    public ObservableCollection<GameplayQueueRow> QueueItems { get; } = new();
    public ObservableCollection<GameplayRow> QuestSummary { get; } = new();
    public ObservableCollection<GameplayRow> PurchaseRequests { get; } = new();
    public ObservableCollection<GameplayRow> SaleRequests { get; } = new();
    public ObservableCollection<GameplayRow> PersonnelRequests { get; } = new();
    public ObservableCollection<GameplayRow> RewardGrants { get; } = new();
    public ObservableCollection<GameplayRow> RestStatus { get; } = new();
    public ObservableCollection<GameplayRow> DowntimeActions { get; } = new();
    public ObservableCollection<string> AuditRows { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ResolveSelectedCommand { get; }

    public string CampaignId { get => _campaignId; set { _campaignId = value; Notify(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; Notify(); } }
    public GameplayQueueRow? SelectedQueueItem { get => _selectedQueueItem; set { _selectedQueueItem = value; Notify(); } }

    public void Refresh()
    {
        Run("load", () =>
        {
            var response = _api.GameplayAdminGetResolutionQueue(new Dictionary<string, object> { ["campaignId"] = CampaignId });
            if (response.Status != ResponseStatus.Ok)
            {
                StatusMessage = response.Message;
                return;
            }

            ReplaceCollection(QueueItems, List(response.Payload, "queueItems").Select(x => GameplayQueueRow.From(Map(x))));
            ReplaceCollection(PurchaseRequests, List(response.Payload, "purchaseRequests").Select(x => GameplayRow.From(Map(x), "requestId", "status", "buyerLogin")));
            ReplaceCollection(SaleRequests, List(response.Payload, "saleRequests").Select(x => GameplayRow.From(Map(x), "requestId", "status", "buyerLogin")));
            ReplaceCollection(PersonnelRequests, List(response.Payload, "personnelRequests").Select(x => GameplayRow.From(Map(x), "requestId", "status", "buyerLogin")));
            ReplaceCollection(RewardGrants, List(response.Payload, "rewardGrants").Select(x => GameplayRow.From(Map(x), "grantId", "status", "playerVisibleSummary")));
            ReplaceCollection(RestStatus, List(response.Payload, "restStatus").Select(x => GameplayRow.From(Map(x), "restId", "status", "playerVisibleSummary")));
            ReplaceCollection(DowntimeActions, List(response.Payload, "downtimeActions").Select(x => GameplayRow.From(Map(x), "actionId", "status", "playerText")));

            QuestSummary.Clear();
            foreach (var pair in Map(response.Payload.TryGetValue("questSummary", out var raw) ? raw : null))
                QuestSummary.Add(new GameplayRow { Id = pair.Key, Status = Convert.ToString(pair.Value) ?? string.Empty, Summary = pair.Key });

            AuditRows.Clear();
            foreach (var row in List(response.Payload, "audit").Select(Map))
                AuditRows.Add($"{S(row, "createdAtUtc")} | {S(row, "actorLogin")} | {S(row, "action")} | {S(row, "summary")}");

            StatusMessage = $"Очередь решений: {QueueItems.Count}; покупки: {PurchaseRequests.Count}; продажи: {SaleRequests.Count}; награды: {RewardGrants.Count}; отдых: {RestStatus.Count}.";
        });
    }

    private void ResolveSelected()
    {
        if (SelectedQueueItem == null)
        {
            StatusMessage = "Выберите пункт очереди.";
            return;
        }

        Run("resolve", () =>
        {
            var response = _api.GameplayAdminResolveQueueItem(new Dictionary<string, object>
            {
                ["itemType"] = SelectedQueueItem.ItemType,
                ["entityId"] = SelectedQueueItem.EntityId
            });
            StatusMessage = response.Message;
            Refresh();
        });
    }

    private void Run(string operation, Action action)
    {
        try
        {
            ClientLogService.Instance.Info($"admin.gameplay.{operation}.start");
            action();
            ClientLogService.Instance.Info($"admin.gameplay.{operation}.done");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ClientLogService.Instance.Error($"admin.gameplay.{operation}.error", ex);
        }
    }

    private static Dictionary<string, object> Map(object? raw) => raw as Dictionary<string, object> ?? new Dictionary<string, object>();
    private static IEnumerable<object> List(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) && raw is IEnumerable list ? list.Cast<object>() : Array.Empty<object>();
    private static string S(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;
    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items) collection.Add(item);
    }

    public sealed class GameplayQueueRow
    {
        public string QueueItemId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Display => $"{Category} | {Status} | {Actor} | {Summary}";
        public static GameplayQueueRow From(Dictionary<string, object> map) => new()
        {
            QueueItemId = S(map, "queueItemId"),
            ItemType = S(map, "itemType"),
            Category = S(map, "category"),
            Status = S(map, "status"),
            Actor = S(map, "actor"),
            EntityId = S(map, "entityId"),
            Summary = S(map, "summary")
        };
    }

    public sealed class GameplayRow
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Display => $"{Id} | {Status} | {Summary}";
        public static GameplayRow From(Dictionary<string, object> map, string idKey, string statusKey, string summaryKey) => new()
        {
            Id = S(map, idKey),
            Status = S(map, statusKey),
            Summary = S(map, summaryKey)
        };
    }
}
