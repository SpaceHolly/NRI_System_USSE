using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Ui.Wpf.Controls;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminQuestViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private QuestRow? _selectedQuest;
    private QuestObjectiveRow? _selectedObjective;
    private string _statusMessage = "Задачи не загружены.";
    private string _campaignId = "dev-campaign-core";
    private string _sessionId = "dev_session_0171";
    private string _newQuestTitle = "Задача 0.17.1";
    private string _newQuestSummary = "Игроки получают цель, прогресс и награды через журнал задач.";
    private string _assignedCharacterIdsInput = string.Empty;
    private string _assignedPlayerUserIdsInput = string.Empty;
    private string _assignedCharacterDisplayName = "Персонаж не выбран";
    private string _visibilityInput = "PlayerVisible";
    private string _statusInput = "Draft";
    private string _objectiveTitleInput = "Поговорить с NPC";
    private string _objectiveTextInput = "Найдите контакт и получите сведения.";
    private string _objectiveStatusInput = "Visible";
    private int _objectiveProgressCurrent;
    private int _objectiveProgressTarget = 1;
    private string _rewardMoneyInput = "25 серебра";
    private string _rewardXpInput = "1 MO";
    private string _rewardItemInput = "item_ref:test_reward";
    private string _rewardCustomInput = "Репутация и знание выдаются GM вручную.";

    public AdminQuestViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
        CreateQuestCommand = new RelayCommand(CreateQuest);
        SaveQuestCommand = new RelayCommand(SaveQuest);
        AssignQuestCommand = new RelayCommand(AssignQuest);
        SetAvailableCommand = new RelayCommand(() => SetStatus("Available"));
        SetActiveCommand = new RelayCommand(() => SetStatus("Active"));
        CompleteQuestCommand = new RelayCommand(CompleteQuest);
        FailQuestCommand = new RelayCommand(FailQuest);
        AddObjectiveCommand = new RelayCommand(AddObjective);
        SaveObjectiveProgressCommand = new RelayCommand(SaveObjectiveProgress);
        CompleteObjectiveCommand = new RelayCommand(() => SaveObjectiveStatus("Completed"));
        SaveRewardBundleCommand = new RelayCommand(SaveRewardBundle);
        PreviewRewardsCommand = new RelayCommand(PreviewRewards);
        CreateRewardGrantCommand = new RelayCommand(CreateRewardGrant);
    }

    public ObservableCollection<QuestRow> Quests { get; } = new();
    public ObservableCollection<QuestObjectiveRow> Objectives { get; } = new();
    public ObservableCollection<string> RewardGrants { get; } = new();
    public ObservableCollection<string> AuditRows { get; } = new();
    public ObservableCollection<NriReferenceOption> CharacterOptions { get; } = new();
    public QuestOption[] StatusOptions { get; } = { new("Draft", "Черновик"), new("Available", "Доступна"), new("Active", "Активна"), new("Completed", "Завершена"), new("Failed", "Провалена"), new("Cancelled", "Отменена"), new("Archived", "В архиве") };
    public QuestOption[] VisibilityOptions { get; } = { new("PlayerVisible", "Всем игрокам"), new("PartyVisible", "Участникам группы"), new("AssignedCharactersOnly", "Только назначенным персонажам"), new("GmOnly", "Только GM"), new("Hidden", "Скрыто") };
    public QuestOption[] ObjectiveStatusOptions { get; } = { new("Hidden", "Скрыта"), new("Visible", "Видна"), new("Active", "Активна"), new("Completed", "Выполнена"), new("Failed", "Провалена") };

    public ICommand RefreshCommand { get; }
    public ICommand CreateQuestCommand { get; }
    public ICommand SaveQuestCommand { get; }
    public ICommand AssignQuestCommand { get; }
    public ICommand SetAvailableCommand { get; }
    public ICommand SetActiveCommand { get; }
    public ICommand CompleteQuestCommand { get; }
    public ICommand FailQuestCommand { get; }
    public ICommand AddObjectiveCommand { get; }
    public ICommand SaveObjectiveProgressCommand { get; }
    public ICommand CompleteObjectiveCommand { get; }
    public ICommand SaveRewardBundleCommand { get; }
    public ICommand PreviewRewardsCommand { get; }
    public ICommand CreateRewardGrantCommand { get; }

    public string CampaignId { get => _campaignId; set { _campaignId = value; Notify(); } }
    public string SessionId { get => _sessionId; set { _sessionId = value; Notify(); } }
    public string NewQuestTitle { get => _newQuestTitle; set { _newQuestTitle = value; Notify(); } }
    public string NewQuestSummary { get => _newQuestSummary; set { _newQuestSummary = value; Notify(); } }
    public string AssignedCharacterIdsInput { get => _assignedCharacterIdsInput; set { _assignedCharacterIdsInput = value; Notify(); } }
    public string AssignedPlayerUserIdsInput { get => _assignedPlayerUserIdsInput; set { _assignedPlayerUserIdsInput = value; Notify(); } }
    public string AssignedCharacterId
    {
        get => Convert.ToString(Split(_assignedCharacterIdsInput).FirstOrDefault()) ?? string.Empty;
        set
        {
            _assignedCharacterIdsInput = value ?? string.Empty;
            Notify();
            Notify(nameof(AssignedCharacterIdsInput));
        }
    }
    public string AssignedCharacterDisplayName { get => _assignedCharacterDisplayName; set { _assignedCharacterDisplayName = value; Notify(); } }
    public string VisibilityInput { get => _visibilityInput; set { _visibilityInput = value; Notify(); } }
    public string StatusInput { get => _statusInput; set { _statusInput = value; Notify(); } }
    public string ObjectiveTitleInput { get => _objectiveTitleInput; set { _objectiveTitleInput = value; Notify(); } }
    public string ObjectiveTextInput { get => _objectiveTextInput; set { _objectiveTextInput = value; Notify(); } }
    public string ObjectiveStatusInput { get => _objectiveStatusInput; set { _objectiveStatusInput = value; Notify(); } }
    public int ObjectiveProgressCurrent { get => _objectiveProgressCurrent; set { _objectiveProgressCurrent = value; Notify(); } }
    public int ObjectiveProgressTarget { get => _objectiveProgressTarget; set { _objectiveProgressTarget = Math.Max(1, value); Notify(); } }
    public string RewardMoneyInput { get => _rewardMoneyInput; set { _rewardMoneyInput = value; Notify(); } }
    public string RewardXpInput { get => _rewardXpInput; set { _rewardXpInput = value; Notify(); } }
    public string RewardItemInput { get => _rewardItemInput; set { _rewardItemInput = value; Notify(); } }
    public string RewardCustomInput { get => _rewardCustomInput; set { _rewardCustomInput = value; Notify(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; Notify(); } }

    public QuestRow? SelectedQuest
    {
        get => _selectedQuest;
        set
        {
            _selectedQuest = value;
            Notify();
            if (value != null) LoadQuest(value.QuestId);
        }
    }

    public QuestObjectiveRow? SelectedObjective
    {
        get => _selectedObjective;
        set
        {
            _selectedObjective = value;
            Notify();
            if (value == null) return;
            ObjectiveTitleInput = value.Title;
            ObjectiveTextInput = value.PlayerText;
            ObjectiveStatusInput = value.Status;
            ObjectiveProgressCurrent = value.ProgressCurrent;
            ObjectiveProgressTarget = value.ProgressTarget;
        }
    }

    public void Refresh()
    {
        try
        {
            RefreshCharacterOptions();
            var response = _api.QuestAdminListForCampaign(new Dictionary<string, object> { ["campaignId"] = CampaignId, ["includeArchived"] = false });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            Quests.Clear();
            foreach (var item in List(response.Payload, "items").Select(Map))
            {
                Quests.Add(new QuestRow
                {
                    QuestId = S(item, "questId", "id"),
                    Title = S(item, "title", "playerTitle"),
                    Status = S(item, "status"),
                    Visibility = S(item, "visibility"),
                    Summary = S(item, "summary", "playerSummary"),
                    UpdatedAtUtc = S(item, "updatedAtUtc")
                });
            }
            StatusMessage = $"Задачи загружены: {Quests.Count}.";
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error("admin.quest.refresh.error", ex);
            StatusMessage = ex.Message;
        }
    }

    private void RefreshCharacterOptions()
    {
        CharacterOptions.Clear();
        var response = _api.GetAllCharacters(includeArchived: false);
        if (response.Status != ResponseStatus.Ok) return;
        foreach (var item in List(response.Payload, "items").Select(Map))
        {
            var id = S(item, "characterId", "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            CharacterOptions.Add(new NriReferenceOption
            {
                Id = id,
                DisplayName = string.IsNullOrWhiteSpace(S(item, "name")) ? "Персонаж без имени" : S(item, "name"),
                TypeLabel = "Персонаж",
                StatusLabel = "Доступен"
            });
        }
    }

    private void CreateQuest()
    {
        Run("Создание задачи", () =>
        {
            var response = _api.QuestAdminCreate(new Dictionary<string, object>
            {
                ["campaignId"] = CampaignId,
                ["sessionId"] = SessionId,
                ["playerTitle"] = NewQuestTitle,
                ["playerSummary"] = NewQuestSummary,
                ["playerKnownDetails"] = NewQuestSummary,
                ["visibility"] = VisibilityInput,
                ["status"] = "Draft",
                ["assignedCharacterIds"] = Split(AssignedCharacterIdsInput),
                ["assignedPlayerUserIds"] = Split(AssignedPlayerUserIdsInput)
            });
            ApplyQuestResponse(response);
            Refresh();
        });
    }

    private void SaveQuest()
    {
        if (SelectedQuest == null) return;
        Run("Сохранение задачи", () =>
        {
            var response = _api.QuestAdminUpdate(new Dictionary<string, object>
            {
                ["questId"] = SelectedQuest.QuestId,
                ["playerTitle"] = NewQuestTitle,
                ["playerSummary"] = NewQuestSummary,
                ["playerKnownDetails"] = NewQuestSummary,
                ["sessionId"] = SessionId
            });
            ApplyQuestResponse(response);
        });
    }

    private void AssignQuest()
    {
        if (SelectedQuest == null) return;
        Run("Назначение задачи", () =>
        {
            var response = _api.QuestAdminAssign(new Dictionary<string, object>
            {
                ["questId"] = SelectedQuest.QuestId,
                ["assignedCharacterIds"] = Split(AssignedCharacterIdsInput),
                ["assignedPlayerUserIds"] = Split(AssignedPlayerUserIdsInput)
            });
            ApplyQuestResponse(response);
        });
    }

    private void SetStatus(string status)
    {
        if (SelectedQuest == null) return;
        Run("Смена статуса задачи", () => ApplyQuestResponse(_api.QuestAdminSetStatus(SelectedQuest.QuestId, status)));
    }

    private void CompleteQuest()
    {
        if (SelectedQuest == null) return;
        if (!Confirm("Завершить задачу", "Задача будет завершена. Безопасные награды могут быть подготовлены к применению, а неоднозначные останутся на решении GM. Продолжить?")) return;
        Run("Завершение задачи", () => ApplyQuestResponse(_api.QuestAdminComplete(SelectedQuest.QuestId)));
    }

    private void FailQuest()
    {
        if (SelectedQuest == null) return;
        if (!Confirm("Провалить задачу", "Задача будет отмечена как проваленная. Награды не должны применяться. Продолжить?")) return;
        Run("Провал задачи", () => ApplyQuestResponse(_api.QuestAdminFail(SelectedQuest.QuestId)));
    }

    private static bool Confirm(string title, string message)
        => System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
           == System.Windows.MessageBoxResult.Yes;

    private void AddObjective()
    {
        if (SelectedQuest == null) return;
        Run("Добавление цели", () =>
        {
            var response = _api.QuestAdminAddObjective(new Dictionary<string, object>
            {
                ["questId"] = SelectedQuest.QuestId,
                ["title"] = ObjectiveTitleInput,
                ["playerText"] = ObjectiveTextInput,
                ["status"] = ObjectiveStatusInput,
                ["progressCurrent"] = ObjectiveProgressCurrent,
                ["progressTarget"] = ObjectiveProgressTarget,
                ["visibility"] = ObjectiveStatusInput == "Hidden" ? "Hidden" : "PlayerVisible"
            });
            ApplyQuestResponse(response);
        });
    }

    private void SaveObjectiveProgress()
    {
        if (SelectedObjective == null) return;
        Run("Обновление прогресса", () =>
        {
            var response = _api.QuestAdminSetObjectiveProgress(new Dictionary<string, object>
            {
                ["objectiveId"] = SelectedObjective.ObjectiveId,
                ["status"] = ObjectiveStatusInput,
                ["progressCurrent"] = ObjectiveProgressCurrent,
                ["progressTarget"] = ObjectiveProgressTarget,
                ["title"] = ObjectiveTitleInput,
                ["playerText"] = ObjectiveTextInput
            });
            ApplyQuestResponse(response);
        });
    }

    private void SaveObjectiveStatus(string status)
    {
        if (SelectedObjective == null) return;
        ObjectiveStatusInput = status;
        SaveObjectiveProgress();
    }

    private void SaveRewardBundle()
    {
        if (SelectedQuest == null) return;
        Run("Сохранение наград", () =>
        {
            var response = _api.QuestAdminCreateRewardBundle(new Dictionary<string, object>
            {
                ["questId"] = SelectedQuest.QuestId,
                ["name"] = "Награды задачи",
                ["moneyRewards"] = RewardMoneyInput,
                ["experienceCoinRewards"] = RewardXpInput,
                ["itemRewardRefs"] = RewardItemInput,
                ["reputationRewardRefs"] = RewardCustomInput,
                ["knowledgeRewardRefs"] = RewardCustomInput,
                ["customRewardText"] = RewardCustomInput,
                ["requiresGmApply"] = true
            });
            ApplyQuestResponse(response);
        });
    }

    private void PreviewRewards()
    {
        if (SelectedQuest == null) return;
        Run("Предпросмотр наград", () =>
        {
            var response = _api.QuestAdminPreviewRewards(SelectedQuest.QuestId);
            if (response.Status != ResponseStatus.Ok)
            {
                StatusMessage = response.Message;
                return;
            }
            ApplyQuestResponse(response);
            StatusMessage = "Предпросмотр наград обновлен.";
        });
    }

    private void CreateRewardGrant()
    {
        if (SelectedQuest == null) return;
        Run("Создание reward grant", () =>
        {
            var response = _api.QuestAdminCreateRewardGrant(new Dictionary<string, object>
            {
                ["questId"] = SelectedQuest.QuestId,
                ["targetCharacterIds"] = Split(AssignedCharacterIdsInput),
                ["targetPlayerUserIds"] = Split(AssignedPlayerUserIdsInput),
                ["playerVisibleSummary"] = $"{RewardMoneyInput}; {RewardXpInput}; {RewardCustomInput}"
            });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadQuest(SelectedQuest.QuestId);
        });
    }

    private void LoadQuest(string questId)
    {
        Run("Загрузка задачи", () => ApplyQuestResponse(_api.QuestAdminGet(questId)));
    }

    private void ApplyQuestResponse(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
        {
            StatusMessage = response.Message;
            return;
        }
        var item = Map(response.Payload.ContainsKey("item") ? response.Payload["item"] : null);
        var questId = S(item, "questId", "id");
        if (!string.IsNullOrWhiteSpace(questId))
        {
            NewQuestTitle = S(item, "title", "playerTitle");
            NewQuestSummary = S(item, "summary", "playerSummary");
            StatusInput = S(item, "status");
            VisibilityInput = S(item, "visibility");
            SessionId = S(item, "sessionId");
            AssignedCharacterIdsInput = string.Join(", ", ListFrom(item.ContainsKey("assignedCharacterIds") ? item["assignedCharacterIds"] : null).Select(Convert.ToString));
            AssignedPlayerUserIdsInput = string.Join(", ", ListFrom(item.ContainsKey("assignedPlayerUserIds") ? item["assignedPlayerUserIds"] : null).Select(Convert.ToString));
        }
        Objectives.Clear();
        foreach (var obj in List(response.Payload, "objectives").Select(Map))
        {
            Objectives.Add(new QuestObjectiveRow
            {
                ObjectiveId = S(obj, "objectiveId", "id"),
                Title = S(obj, "title"),
                PlayerText = S(obj, "playerText"),
                Status = S(obj, "status"),
                ProgressCurrent = I(obj, "progressCurrent"),
                ProgressTarget = Math.Max(1, I(obj, "progressTarget", 1)),
                Visibility = S(obj, "visibility")
            });
        }
        RewardGrants.Clear();
        foreach (var grant in List(response.Payload, "rewardGrants").Select(Map))
        {
            RewardGrants.Add($"{S(grant, "status")} | {S(grant, "playerVisibleSummary")} | {S(grant, "grantId")}");
        }
        var reward = Map(response.Payload.ContainsKey("rewardBundle") ? response.Payload["rewardBundle"] : null);
        if (reward.Count > 0)
        {
            RewardMoneyInput = S(reward, "moneyRewards");
            RewardXpInput = S(reward, "experienceCoinRewards");
            RewardItemInput = S(reward, "itemRewardRefs");
            RewardCustomInput = S(reward, "customRewardText", "reputationRewardRefs", "knowledgeRewardRefs");
        }
        AuditRows.Clear();
        foreach (var audit in List(response.Payload, "audit").Select(Map))
        {
            AuditRows.Add($"{S(audit, "createdAtUtc")} | {S(audit, "actorLogin")} | {S(audit, "action")} | {S(audit, "summary")}");
        }
        StatusMessage = response.Message;
    }

    private void Run(string action, Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error("admin.quest.action.error " + action, ex);
            StatusMessage = ex.Message;
        }
    }

    private static object[] Split(string text)
        => (text ?? string.Empty).Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => (object)x.Trim()).Where(x => !string.IsNullOrWhiteSpace(Convert.ToString(x))).ToArray();

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
}

public sealed class QuestOption
{
    public QuestOption(string id, string title)
    {
        Id = id;
        Title = title;
    }

    public string Id { get; }
    public string Title { get; }
}

public sealed class QuestRow
{
    public string QuestId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string UpdatedAtUtc { get; set; } = string.Empty;
    public string Display => $"{Title} | {Status} | {Visibility}";
}

public sealed class QuestObjectiveRow
{
    public string ObjectiveId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PlayerText { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ProgressCurrent { get; set; }
    public int ProgressTarget { get; set; } = 1;
    public string Visibility { get; set; } = string.Empty;
    public string Display => $"{Title} | {Status} | {ProgressCurrent}/{ProgressTarget}";
}
