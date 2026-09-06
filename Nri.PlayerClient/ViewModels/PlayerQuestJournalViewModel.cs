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

public sealed class PlayerQuestJournalViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private string _campaignId = "dev-campaign-core";
    private string _statusMessage = "Журнал задач не загружен.";
    private PlayerQuestRow? _selectedQuest;

    public PlayerQuestJournalViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        RefreshCommand = new RelayCommand(Refresh);
    }

    public ObservableCollection<PlayerQuestRow> ActiveQuests { get; } = new();
    public ObservableCollection<PlayerQuestRow> AvailableQuests { get; } = new();
    public ObservableCollection<PlayerQuestRow> CompletedQuests { get; } = new();
    public ObservableCollection<PlayerQuestObjectiveRow> Objectives { get; } = new();
    public ObservableCollection<string> Rewards { get; } = new();
    public ICommand RefreshCommand { get; }

    public string CampaignId { get => _campaignId; set { _campaignId = value; Notify(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; Notify(); } }
    public string VisibilityHint => "Показаны только известные персонажу цели, изменения и награды.";

    public PlayerQuestRow? SelectedQuest
    {
        get => _selectedQuest;
        set
        {
            _selectedQuest = value;
            Notify();
            if (value != null) LoadQuest(value.QuestId);
        }
    }

    public void Refresh()
    {
        try
        {
            var response = _api.QuestPlayerGetJournal(new Dictionary<string, object>
            {
                ["campaignId"] = CampaignId,
                ["characterId"] = _activeCharacterIdAccessor()
            });
            if (response.Status != ResponseStatus.Ok)
            {
                StatusMessage = response.Message;
                return;
            }
            Fill(ActiveQuests, response.Payload, "active");
            Fill(AvailableQuests, response.Payload, "available");
            Fill(CompletedQuests, response.Payload, "completed");
            StatusMessage = $"Журнал задач обновлён: активных {ActiveQuests.Count}, доступных {AvailableQuests.Count}, завершённых {CompletedQuests.Count}.";
            var first = ActiveQuests.FirstOrDefault() ?? AvailableQuests.FirstOrDefault() ?? CompletedQuests.FirstOrDefault();
            if (first != null) SelectedQuest = first;
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error("player.quest.refresh.error", ex);
            StatusMessage = ex.Message;
        }
    }

    private void LoadQuest(string questId)
    {
        try
        {
            var response = _api.QuestPlayerGet(questId);
            if (response.Status != ResponseStatus.Ok)
            {
                StatusMessage = response.Message;
                return;
            }
            var item = Map(response.Payload.ContainsKey("item") ? response.Payload["item"] : null);
            Objectives.Clear();
            foreach (var objective in ListFrom(item.ContainsKey("objectives") ? item["objectives"] : null).Select(Map))
            {
                Objectives.Add(new PlayerQuestObjectiveRow
                {
                    Title = S(objective, "title"),
                    Text = S(objective, "playerText"),
                    Status = S(objective, "status"),
                    ProgressCurrent = I(objective, "progressCurrent"),
                    ProgressTarget = Math.Max(1, I(objective, "progressTarget", 1))
                });
            }
            Rewards.Clear();
            var reward = Map(item.ContainsKey("rewardBundle") ? item["rewardBundle"] : null);
            if (reward.Count > 0) Rewards.Add(S(reward, "summary", "publicDescription", "customRewardText"));
            foreach (var grant in ListFrom(item.ContainsKey("rewardGrants") ? item["rewardGrants"] : null).Select(Map))
            {
                Rewards.Add($"{ReadableStatus(S(grant, "status"))} | {S(grant, "playerVisibleSummary")}");
            }
            StatusMessage = "Задача загружена.";
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error("player.quest.load.error", ex);
            StatusMessage = ex.Message;
        }
    }

    private static void Fill(ObservableCollection<PlayerQuestRow> target, Dictionary<string, object> payload, string key)
    {
        target.Clear();
        foreach (var item in List(payload, key).Select(Map))
        {
            target.Add(new PlayerQuestRow
            {
                QuestId = S(item, "questId", "id"),
                Title = S(item, "title", "playerTitle"),
                Status = S(item, "status"),
                Summary = S(item, "summary", "playerSummary"),
                ProgressText = string.Join(", ", ListFrom(item.ContainsKey("objectives") ? item["objectives"] : null).Select(Map).Select(o => $"{S(o, "title")} {I(o, "progressCurrent")}/{Math.Max(1, I(o, "progressTarget", 1))}"))
            });
        }
    }

    private static IEnumerable<object> List(Dictionary<string, object> payload, string key)
        => payload.ContainsKey(key) ? ListFrom(payload[key]) : Enumerable.Empty<object>();

    private static IEnumerable<object> ListFrom(object? value)
    {
        if (value is object[] array) return array;
        if (value is IEnumerable enumerable && !(value is string)) return enumerable.Cast<object>();
        return Enumerable.Empty<object>();
    }

    private static Dictionary<string, object> Map(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static string S(Dictionary<string, object> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.ContainsKey(key) && map[key] != null) return Convert.ToString(map[key]) ?? string.Empty;
        }
        return string.Empty;
    }

    private static int I(Dictionary<string, object> map, string key, int fallback = 0)
    {
        int value;
        return map.ContainsKey(key) && int.TryParse(Convert.ToString(map[key]), out value) ? value : fallback;
    }

    internal static string ReadableStatus(string value)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "active": return "Активна";
            case "available": return "Доступна";
            case "completed": return "Завершена";
            case "failed": return "Не выполнена";
            case "hidden": return "Недоступна";
            case "visible":
            case "in_progress": return "Выполняется";
            case "pending":
            case "pending_gm": return "Ожидает решения";
            case "applied":
            case "granted": return "Применено";
            default: return string.IsNullOrWhiteSpace(value) ? "Статус не указан" : value;
        }
    }
}

public sealed class PlayerQuestRow
{
    public string QuestId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ProgressText { get; set; } = string.Empty;
    public string StatusLabel => PlayerQuestJournalViewModel.ReadableStatus(Status);
    public string Display => $"{Title} | {StatusLabel}";
}

public sealed class PlayerQuestObjectiveRow
{
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ProgressCurrent { get; set; }
    public int ProgressTarget { get; set; } = 1;
    public string StatusLabel => PlayerQuestJournalViewModel.ReadableStatus(Status);
    public string Display => $"{StatusLabel} | {ProgressCurrent}/{ProgressTarget}";
}
