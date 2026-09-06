using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerGameplayViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "dev-campaign-core";
    private string _statusMessage = "Игровой цикл не загружен.";
    private GameplayRow? _selectedRow;

    public PlayerGameplayViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
        ClearSelectionCommand = new RelayCommand(() => SelectedRow = null);
    }

    public ObservableCollection<GameplayRow> ActiveQuests { get; } = new();
    public ObservableCollection<GameplayRow> PendingPurchases { get; } = new();
    public ObservableCollection<GameplayRow> PendingSales { get; } = new();
    public ObservableCollection<GameplayRow> RestStatus { get; } = new();
    public ObservableCollection<GameplayRow> DowntimeActions { get; } = new();
    public ObservableCollection<GameplayRow> RewardSummary { get; } = new();
    public ObservableCollection<GameplayRow> RecentReceipts { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public string CampaignId { get => _campaignId; set { _campaignId = value; Notify(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; Notify(); } }
    public string VisibilityHint => "Показаны только доступные вам задачи, решения и результаты.";
    public GameplayRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (_selectedRow == value) return;
            _selectedRow = value;
            Notify();
            Notify(nameof(SelectedTitle));
            Notify(nameof(SelectedStatus));
            Notify(nameof(SelectedSummary));
        }
    }
    public string SelectedTitle => SelectedRow?.Title ?? "Выберите запись";
    public string SelectedStatus => SelectedRow?.StatusLabel ?? "Подробности появятся здесь.";
    public string SelectedSummary => SelectedRow?.Summary ?? "Откройте задачу, покупку, отдых, награду или квитанцию.";

    public void Refresh()
    {
        Run("load", () =>
        {
            var response = _api.GameplayPlayerGetMyGameplayStatus(new Dictionary<string, object> { ["campaignId"] = CampaignId });
            if (response.Status != ResponseStatus.Ok)
            {
                StatusMessage = response.Message;
                return;
            }

            ReplaceCollection(ActiveQuests, List(response.Payload, "activeQuests").Select(x => GameplayRow.From(Map(x), "questId", "status", "title", "Задача")));
            ReplaceCollection(PendingPurchases, List(response.Payload, "pendingPurchases").Select(x => GameplayRow.From(Map(x), "requestId", "status", "displayName", "Покупка")));
            ReplaceCollection(PendingSales, List(response.Payload, "pendingSales").Select(x => GameplayRow.From(Map(x), "requestId", "status", "summary", "Продажа")));
            ReplaceCollection(RestStatus, List(response.Payload, "restStatus").Select(x => GameplayRow.From(Map(x), "restId", "status", "playerVisibleSummary", "Отдых")));
            ReplaceCollection(DowntimeActions, List(response.Payload, "downtimeActions").Select(x => GameplayRow.From(Map(x), "actionId", "status", "playerText", "Действие отдыха")));
            ReplaceCollection(RewardSummary, List(response.Payload, "rewardSummary").Select(x => GameplayRow.FromSummary(Map(x), "grantId", "status", FirstNonEmpty(S(Map(x), "playerVisibleSummary"), S(Map(x), "recoverySummaryPlayer")), "Награда")));
            ReplaceCollection(RecentReceipts, List(response.Payload, "recentReceipts").Select(x => GameplayRow.From(Map(x), "receiptId", "status", "displayName", "Квитанция")));
            SelectedRow = ActiveQuests.FirstOrDefault()
                ?? PendingPurchases.FirstOrDefault()
                ?? PendingSales.FirstOrDefault()
                ?? RestStatus.FirstOrDefault()
                ?? RewardSummary.FirstOrDefault()
                ?? RecentReceipts.FirstOrDefault();
            StatusMessage = $"Игровой цикл: задач {ActiveQuests.Count}; покупок {PendingPurchases.Count}; продаж {PendingSales.Count}; отдых {RestStatus.Count}; наград {RewardSummary.Count}.";
        });
    }

    private void Run(string operation, Action action)
    {
        try
        {
            ClientLogService.Instance.Info($"player.gameplay.{operation}.start");
            action();
            ClientLogService.Instance.Info($"player.gameplay.{operation}.done");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ClientLogService.Instance.Error($"player.gameplay.{operation}.error", ex);
        }
    }

    private static Dictionary<string, object> Map(object? raw) => raw as Dictionary<string, object> ?? new Dictionary<string, object>();
    private static IEnumerable<object> List(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) && raw is IEnumerable list ? list.Cast<object>() : Array.Empty<object>();
    private static string S(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items) collection.Add(item);
    }

    public sealed class GameplayRow
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string StatusLabel => ReadableStatus(Status);
        public string Display => $"{Title} | {StatusLabel}";
        public static GameplayRow From(Dictionary<string, object> map, string idKey, string statusKey, string summaryKey, string fallbackTitle) => new()
        {
            Id = S(map, idKey),
            Title = FirstNonEmpty(S(map, summaryKey), fallbackTitle),
            Status = S(map, statusKey),
            Summary = S(map, summaryKey)
        };

        public static GameplayRow FromSummary(Dictionary<string, object> map, string idKey, string statusKey, string summary, string fallbackTitle) => new()
        {
            Id = S(map, idKey),
            Title = fallbackTitle,
            Status = S(map, statusKey),
            Summary = summary
        };

        private static string ReadableStatus(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "active": return "Активно";
                case "available": return "Доступно";
                case "submitted": return "Отправлено";
                case "pending":
                case "pending_gm":
                case "pending_review": return "Ожидает решения";
                case "approved": return "Одобрено";
                case "rejected": return "Отклонено";
                case "completed":
                case "applied": return "Завершено";
                case "cancelled":
                case "canceled": return "Отменено";
                default: return string.IsNullOrWhiteSpace(value) ? "Статус не указан" : value;
            }
        }
    }
}
