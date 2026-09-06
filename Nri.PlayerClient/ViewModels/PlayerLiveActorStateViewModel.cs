using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed class LiveResourceRowVm
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Current { get; set; }
    public decimal Maximum { get; set; }
    public decimal BaseMaximum { get; set; }
    public decimal Reserved { get; set; }
    public string ValueText => $"{Current:0.#} / {Maximum:0.#}";
    public string DetailText => Reserved > 0 ? $"Зарезервировано: {Reserved:0.#}" : Maximum != BaseMaximum ? $"Базовый максимум: {BaseMaximum:0.#}" : "Доступно полностью";
}

public sealed class LiveCapabilityRowVm
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Base { get; set; }
    public decimal Permanent { get; set; }
    public decimal Temporary { get; set; }
    public decimal Effective { get; set; }
    public string Reasons { get; set; } = string.Empty;
    public string Breakdown => string.IsNullOrWhiteSpace(Reasons) ? $"Основа {Base:+0;-0;0}  •  постоянное {Permanent:+0;-0;0}  •  временное {Temporary:+0;-0;0}" : Reasons;
}

public sealed class LiveEffectRowVm
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Stacks { get; set; }
    public int Rounds { get; set; }
    public string DurationMode { get; set; } = string.Empty;
    public string DurationText => Rounds > 0 ? $"Осталось раундов: {Rounds}  •  уровней: {Math.Max(1, Stacks)}" : $"До снятия  •  уровней: {Math.Max(1, Stacks)}";
}

public sealed class LiveActionRowVm
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Charges { get; set; }
    public int MaximumCharges { get; set; }
    public int CooldownRounds { get; set; }
    public bool IsEnabled { get; set; }
    public string UnavailableReasons { get; set; } = string.Empty;
    public string AvailabilityText => !string.IsNullOrWhiteSpace(UnavailableReasons) ? UnavailableReasons : !IsEnabled ? "Недоступно" : CooldownRounds > 0 ? $"Перезарядка: {CooldownRounds} р." : MaximumCharges > 0 && Charges <= 0 ? "Нет зарядов" : "Готово";
}

public sealed class LiveExecutionRowVm
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Stage { get; set; }
    public int StageCount { get; set; }
    public int RemainingRounds { get; set; }
    public string Summary => $"{State}  •  этап {Stage}/{Math.Max(1, StageCount)}" + (RemainingRounds > 0 ? $"  •  {RemainingRounds} р." : string.Empty);
}

public sealed class LiveWeaponRowVm
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Loaded { get; set; }
    public int Capacity { get; set; }
    public int Chambered { get; set; }
    public int Reserve { get; set; }
    public string FireMode { get; set; } = string.Empty;
    public decimal Durability { get; set; }
    public decimal DurabilityMaximum { get; set; }
    public bool IsActive { get; set; }
    public string AmmoText => $"Магазин {Loaded}/{Capacity}  •  патронник {Chambered}  •  запас {Reserve}";
    public string ConditionText => $"Состояние {Durability:0.#}/{DurabilityMaximum:0.#}";
}

public sealed class LiveHistoryRowVm
{
    public string Text { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}

public sealed class LiveCompanionRowVm
{
    public string Name { get; set; } = string.Empty;
    public string LifeState { get; set; } = string.Empty;
    public bool CanAct { get; set; }
    public string ResourceSummary { get; set; } = string.Empty;
    public string WeaponSummary { get; set; } = string.Empty;
    public string Summary => string.Join("  •  ", new[] { LifeState, CanAct ? "может действовать" : "не может действовать", ResourceSummary, WeaponSummary }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public partial class PlayerMainViewModel
{
    private string _liveActorStatusText = "Откройте состояние активного персонажа.";
    private string _liveActorName = "Персонаж не выбран";
    private string _liveActorLifeState = "Состояние неизвестно";
    private bool _liveActorCanAct;
    private bool _liveActorCanReact;
    private string _liveActorCombatText = string.Empty;
    private string _liveActorWarningText = string.Empty;
    private long _liveActorRevision;
    private LiveActionRowVm? _selectedLiveAction;
    private LiveWeaponRowVm? _selectedLiveWeapon;
    private ICommand? _refreshLiveActorStateCommand;
    private ICommand? _executeLiveActionCommand;
    private ICommand? _reloadLiveWeaponCommand;

    public ObservableCollection<LiveResourceRowVm> LiveActorResources { get; } = new();
    public ObservableCollection<LiveCapabilityRowVm> LiveActorCapabilities { get; } = new();
    public ObservableCollection<LiveEffectRowVm> LiveActorEffects { get; } = new();
    public ObservableCollection<LiveActionRowVm> LiveActorActions { get; } = new();
    public ObservableCollection<LiveExecutionRowVm> LiveActorExecutions { get; } = new();
    public ObservableCollection<LiveWeaponRowVm> LiveActorWeapons { get; } = new();
    public ObservableCollection<LiveHistoryRowVm> LiveActorHistory { get; } = new();
    public ObservableCollection<LiveCompanionRowVm> LiveActorCompanions { get; } = new();
    public string LiveActorStatusText { get => _liveActorStatusText; private set { _liveActorStatusText = value; Notify(); } }
    public string LiveActorName { get => _liveActorName; private set { _liveActorName = value; Notify(); } }
    public string LiveActorLifeState { get => _liveActorLifeState; private set { _liveActorLifeState = value; Notify(); } }
    public string LiveActorWarningText { get => _liveActorWarningText; private set { _liveActorWarningText = value; Notify(); } }
    public string LiveActorActionEconomyText => string.IsNullOrWhiteSpace(_liveActorCombatText) ? $"Действие: {(LiveActorCanAct ? "доступно" : "недоступно")}  •  реакция: {(LiveActorCanReact ? "готова" : "потрачена")}" : _liveActorCombatText;
    public bool LiveActorCanAct { get => _liveActorCanAct; private set { _liveActorCanAct = value; Notify(); Notify(nameof(LiveActorActionEconomyText)); } }
    public bool LiveActorCanReact { get => _liveActorCanReact; private set { _liveActorCanReact = value; Notify(); Notify(nameof(LiveActorActionEconomyText)); } }
    public LiveActionRowVm? SelectedLiveAction { get => _selectedLiveAction; set { _selectedLiveAction = value; Notify(); } }
    public LiveWeaponRowVm? SelectedLiveWeapon { get => _selectedLiveWeapon; set { _selectedLiveWeapon = value; Notify(); } }
    public ICommand RefreshLiveActorStateCommand => _refreshLiveActorStateCommand ??= new RelayCommand(LoadLiveActorState);
    public ICommand ExecuteLiveActionCommand => _executeLiveActionCommand ??= new RelayCommand(ExecuteSelectedLiveAction);
    public ICommand ReloadLiveWeaponCommand => _reloadLiveWeaponCommand ??= new RelayCommand(ReloadSelectedLiveWeapon);

    private void LoadLiveActorState()
    {
        var characterId = FirstNonEmpty(ActiveCharacterId, SelectedCharacterId);
        if (string.IsNullOrWhiteSpace(characterId)) { LiveActorStatusText = "Сначала выберите активного персонажа."; ClearLiveActorState(); return; }
        LiveActorStatusText = "Загружаю текущее состояние...";
        var response = _api.CharacterPlayerLiveStateGet(characterId);
        if (response.Status != ResponseStatus.Ok) { LiveActorStatusText = response.Message; ClearLiveActorState(); return; }
        var state = response.Payload.TryGetValue("liveState", out var raw) ? AsMap(raw, CommandNames.CharacterPlayerLiveStateGet) : null;
        if (state == null) { LiveActorStatusText = "Сервер не вернул состояние персонажа."; ClearLiveActorState(); return; }
        _liveActorRevision = ParseLong0216(GetString(state, "revision"));
        LiveActorName = FirstNonEmpty(GetString(state, "displayName"), ActiveCharacterShellTitle);
        LiveActorLifeState = ReadableLifeState0216(GetString(state, "lifeState"));
        LiveActorCanAct = ParseBool0216(state, "canAct"); LiveActorCanReact = ParseBool0216(state, "canReact");
        var combat = state.TryGetValue("combat", out var combatRaw) ? AsMap(combatRaw, "live combat") : null;
        _liveActorCombatText = combat != null && ParseBool0216(combat, "isInCombat")
            ? $"Бой: действий {Dec0216(combat, "actionPoints"):0} + малых {Dec0216(combat, "minorActionPoints"):0}  •  реакции {Dec0216(combat, "reactionCount"):0}/{Dec0216(combat, "reactionLimit"):0}"
            : string.Empty;
        Notify(nameof(LiveActorActionEconomyText));
        LiveActorWarningText = JoinText0216(state, "reconciliationWarnings");
        BindResources0216(state); BindCapabilities0216(state); BindEffects0216(state); BindActions0216(state); BindExecutions0216(state); BindWeapons0216(state); BindHistory0216(state); BindCompanions0216(characterId);
        LiveActorStatusText = $"Обновлено {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
    }

    private void ExecuteSelectedLiveAction()
    {
        if (SelectedLiveAction == null) { LiveActorStatusText = "Выберите действие."; return; }
        var response = _api.CharacterPlayerActionExecute(new Dictionary<string, object> { ["characterId"] = FirstNonEmpty(ActiveCharacterId, SelectedCharacterId), ["actionDefinitionId"] = SelectedLiveAction.Id, ["expectedRevision"] = _liveActorRevision, ["operationId"] = Guid.NewGuid().ToString("N") });
        LiveActorStatusText = response.Message; if (response.Status == ResponseStatus.Ok) LoadLiveActorState();
    }

    private void ReloadSelectedLiveWeapon()
    {
        if (SelectedLiveWeapon == null) { LiveActorStatusText = "Выберите оружие."; return; }
        var response = _api.CharacterPlayerWeaponReload(new Dictionary<string, object> { ["characterId"] = FirstNonEmpty(ActiveCharacterId, SelectedCharacterId), ["itemInstanceId"] = SelectedLiveWeapon.ItemId, ["expectedRevision"] = _liveActorRevision, ["operationId"] = Guid.NewGuid().ToString("N") });
        LiveActorStatusText = response.Message; if (response.Status == ResponseStatus.Ok) LoadLiveActorState();
    }

    private void ClearLiveActorState() { LiveActorWarningText = string.Empty; LiveActorResources.Clear(); LiveActorCapabilities.Clear(); LiveActorEffects.Clear(); LiveActorActions.Clear(); LiveActorExecutions.Clear(); LiveActorWeapons.Clear(); LiveActorHistory.Clear(); LiveActorCompanions.Clear(); }
    private void BindResources0216(Dictionary<string, object> state) { LiveActorResources.Clear(); foreach (var raw in ToObjectList(state.TryGetValue("resources", out var v) ? v : new ArrayList())) { var m = AsMap(raw, "live resources"); if (m == null) continue; LiveActorResources.Add(new LiveResourceRowVm { Id=GetString(m,"resourceId"), Name=GetString(m,"displayName"), Current=Dec0216(m,"current"), Maximum=Dec0216(m,"effectiveMaximum"), BaseMaximum=Dec0216(m,"baseMaximum"), Reserved=Dec0216(m,"reserved") }); } }
    private void BindCapabilities0216(Dictionary<string, object> state)
    {
        LiveActorCapabilities.Clear();
        var rows = new List<(string Id, LiveCapabilityRowVm Row)>();
        foreach (var raw in ToObjectList(state.TryGetValue("capabilities", out var value) ? value : new ArrayList()))
        {
            var map = AsMap(raw, "live capabilities");
            if (map == null) continue;
            var definitionId = GetString(map, "definitionId");
            var row = new LiveCapabilityRowVm
            {
                Name = ReadableCapability0216(definitionId, GetString(map, "displayName")),
                Type = GetString(map, "capabilityType"),
                Base = Dec0216(map, "baseValue"),
                Permanent = Dec0216(map, "permanentModifier"),
                Temporary = Dec0216(map, "temporaryModifier"),
                Effective = Dec0216(map, "effectiveValue"),
                Reasons = JoinText0216(map, "modifierReasons")
            };
            if (row.Base == 0 && row.Permanent == 0 && row.Temporary == 0 && row.Effective == 0 && string.IsNullOrWhiteSpace(row.Reasons)) continue;
            rows.Add((definitionId, row));
        }
        foreach (var item in rows.OrderBy(x => CapabilityDisplayOrder0216(x.Id)).ThenBy(x => x.Row.Name, StringComparer.CurrentCultureIgnoreCase))
            LiveActorCapabilities.Add(item.Row);
    }

    private static int CapabilityDisplayOrder0216(string definitionId) => definitionId.ToLowerInvariant() switch
    {
        "strength" => 0,
        "medicine" => 1,
        _ => 10
    };
    private void BindEffects0216(Dictionary<string, object> state) { LiveActorEffects.Clear(); foreach(var raw in ToObjectList(state.TryGetValue("effects",out var v)?v:new ArrayList())){var m=AsMap(raw,"live effects");if(m==null)continue;LiveActorEffects.Add(new LiveEffectRowVm{Name=GetString(m,"displayName"),Description=GetString(m,"description"),Stacks=(int)Dec0216(m,"stackCount"),Rounds=(int)Dec0216(m,"remainingRounds"),DurationMode=GetString(m,"durationMode")});} }
    private void BindActions0216(Dictionary<string, object> state) { LiveActorActions.Clear(); foreach(var raw in ToObjectList(state.TryGetValue("actions",out var v)?v:new ArrayList())){var m=AsMap(raw,"live actions");if(m==null)continue;var id=GetString(m,"actionDefinitionId");LiveActorActions.Add(new LiveActionRowVm{Id=id,Name=FirstNonEmpty(GetString(m,"displayName"),ReadableAction0216(id)),Charges=(int)Dec0216(m,"currentCharges"),MaximumCharges=(int)Dec0216(m,"maximumCharges"),CooldownRounds=(int)Dec0216(m,"remainingRounds"),IsEnabled=ParseBool0216(m,"isEnabled"),UnavailableReasons=JoinText0216(m,"unavailableReasons")});} SelectedLiveAction=LiveActorActions.FirstOrDefault(); }
    private void BindExecutions0216(Dictionary<string, object> state) { LiveActorExecutions.Clear(); foreach(var raw in ToObjectList(state.TryGetValue("executions",out var v)?v:new ArrayList())){var m=AsMap(raw,"live executions");if(m==null)continue;LiveActorExecutions.Add(new LiveExecutionRowVm{Name=FirstNonEmpty(GetString(m,"displayName"),"Неизвестное действие"),State=ReadableExecutionState0216(GetString(m,"state")),Stage=(int)Dec0216(m,"currentStage"),StageCount=(int)Dec0216(m,"totalStages"),RemainingRounds=(int)Dec0216(m,"remainingRounds")});} }
    private void BindWeapons0216(Dictionary<string, object> state) { LiveActorWeapons.Clear(); foreach(var raw in ToObjectList(state.TryGetValue("weapons",out var v)?v:new ArrayList())){var m=AsMap(raw,"live weapons");if(m==null)continue;var id=GetString(m,"itemInstanceId");LiveActorWeapons.Add(new LiveWeaponRowVm{ItemId=id,Name=FirstNonEmpty(GetString(m,"displayName"),"Оружие"),Loaded=(int)Dec0216(m,"loadedQuantity"),Reserve=(int)Dec0216(m,"reserveQuantity"),Capacity=(int)Dec0216(m,"capacity"),Chambered=(int)Dec0216(m,"chamberedQuantity"),FireMode=GetString(m,"fireMode"),Durability=Dec0216(m,"durabilityCurrent"),DurabilityMaximum=Dec0216(m,"durabilityMaximum"),IsActive=ParseBool0216(m,"isActive")});} SelectedLiveWeapon=LiveActorWeapons.FirstOrDefault(); }
    private void BindHistory0216(Dictionary<string, object> state) { LiveActorHistory.Clear(); foreach(var raw in ToObjectList(state.TryGetValue("history",out var v)?v:new ArrayList())){var m=AsMap(raw,"live history");if(m==null)continue;LiveActorHistory.Add(new LiveHistoryRowVm{Text=GetString(m,"displayText"),Change=$"{GetString(m,"oldSummary")} → {GetString(m,"newSummary")}",Time=FormatLocalTime0216(GetString(m,"createdAtUtc"))});} }
    private void BindCompanions0216(string characterId) { LiveActorCompanions.Clear(); var response=_api.CharacterPlayerCompanionsLiveSummaryGet(characterId); if(response.Status!=ResponseStatus.Ok)return; foreach(var raw in ToObjectList(response.Payload.TryGetValue("companions",out var value)?value:new ArrayList())){var map=AsMap(raw,"live companions");if(map==null)continue;LiveActorCompanions.Add(new LiveCompanionRowVm{Name=GetString(map,"displayName"),LifeState=ReadableLifeState0216(GetString(map,"lifeState")),CanAct=ParseBool0216(map,"canAct"),ResourceSummary=GetString(map,"resourceSummary"),WeaponSummary=GetString(map,"weaponSummary")});} }
    private static string JoinText0216(Dictionary<string,object> map,string key)=>map.TryGetValue(key,out var value)?string.Join("; ",ToObjectList(value).Cast<object>().Select(Convert.ToString).Where(x=>!string.IsNullOrWhiteSpace(x))):string.Empty;
    private static decimal Dec0216(Dictionary<string,object> m,string key)=>decimal.TryParse(GetString(m,key),NumberStyles.Any,CultureInfo.InvariantCulture,out var v)?v:decimal.TryParse(GetString(m,key),out v)?v:0;
    private static long ParseLong0216(string value)=>long.TryParse(value,out var result)?result:0;
    private static bool ParseBool0216(Dictionary<string,object> m,string key)=>m.TryGetValue(key,out var value)&&bool.TryParse(Convert.ToString(value),out var result)&&result;
    private static string FormatLocalTime0216(string value)=>DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var date)?date.ToLocalTime().ToString("dd.MM.yyyy HH:mm"):string.Empty;
    private static string ReadableLifeState0216(string value)=>value.ToLowerInvariant() switch{"active" or "healthy"=>"В норме","impaired"=>"Ослаблен","incapacitated"=>"Недееспособен","unconscious"=>"Без сознания","dying"=>"При смерти","stable"=>"Стабилен","dead"=>"Мёртв","destroyed"=>"Уничтожен",_=>"Состояние не определено"};
    private static string ReadableExecutionState0216(string value)=>value.ToLowerInvariant() switch{"prepared"=>"Подготовлено","casting"=>"Применяется","channeling"=>"Поддерживается","inprogress" or "in_progress"=>"Выполняется","sustained"=>"Поддерживается","interrupted"=>"Прервано","completed"=>"Завершено",_=>value.Replace('_',' ')};
    private static string ReadableAction0216(string value)=>"Неизвестное действие";
    private static string ReadableCapability0216(string definitionId, string displayName)
    {
        var key = FirstNonEmpty(definitionId, displayName).Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        var readable = key switch
        {
            "health" => "Здоровье",
            "health_current" => "Текущее здоровье",
            "health_max" => "Максимальное здоровье",
            "physical_armor" => "Физическая броня",
            "magic_armor" => "Магическая броня",
            "physical_defense" => "Физическая защита",
            "magical_defense" or "magic_defense" => "Магическая защита",
            "morale" => "Мораль",
            "strength" => "Сила",
            "dexterity" or "agility" => "Ловкость",
            "endurance" or "constitution" => "Выносливость",
            "intellect" or "intelligence" => "Интеллект",
            "wisdom" => "Мудрость",
            "charisma" => "Харизма",
            "initiative" => "Инициатива",
            "movement" => "Скорость перемещения",
            "carrying_capacity" => "Грузоподъёмность",
            "athletics" => "Атлетика",
            "stealth" => "Скрытность",
            "perception" => "Восприятие",
            "persuasion" => "Убеждение",
            "arcana" => "Магические знания",
            "survival" => "Выживание",
            "medicine" => "Медицина",
            "history" => "История",
            "investigation" => "Расследование",
            "acrobatics" => "Акробатика",
            "dev_acceptance_attribute" => "Тестовый показатель",
            "dev_acceptance_derived" => "Тестовый производный показатель",
            "dev_acceptance_skill" => "Тестовый навык",
            "dev_acceptance_skill_01451" => "Тестовый навык 01451",
            "magic_resonance_0184" => "Магический резонанс 0184",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(readable)) return readable;
        if (!LooksTechnical0216(displayName)) return displayName.Trim();
        return "Пользовательский показатель";
    }

    private static bool LooksTechnical0216(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Any(ch => ch == '_' || ch == '-') || value.All(ch => ch <= 127);
    }
}
