using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminLiveActorRowVm
{
    public string SubjectId { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string SubjectTypeLabel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LifeState { get; set; } = string.Empty;
    public string Readiness { get; set; } = string.Empty;
    public string ResourceSummary { get; set; } = string.Empty;
    public long Revision { get; set; }
}

public sealed class AdminLiveResourceRowVm
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Current { get; set; }
    public decimal Maximum { get; set; }
    public decimal BaseMaximum { get; set; }
    public decimal Reserved { get; set; }
    public string Summary => $"{Current:0.#} / {Maximum:0.#}";
    public string PreviewSummary => BaseMaximum == Maximum ? Summary : $"{Summary} (базовый максимум {BaseMaximum:0.#})";
}

public sealed class AdminLivePreviewTextRowVm
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class AdminLiveEffectRowVm
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Rounds { get; set; }
}

public sealed class AdminLiveHistoryRowVm
{
    public string Text { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}

public partial class AdminMainViewModel
{
    private AdminLiveActorRowVm? _selectedAdminLiveActor;
    private AdminLiveResourceRowVm? _selectedAdminLiveResource;
    private AdminLiveEffectRowVm? _selectedAdminLiveEffect;
    private string _adminLiveStateStatus = "Обновите состояние активной группы.";
    private string _adminLiveAdjustmentValue = "0";
    private string _adminLiveAdjustmentReason = string.Empty;
    private string _adminLiveAdjustmentValidation = string.Empty;
    private string _adminLivePreviewSummary = "Предпросмотр игрока ещё не загружен.";
    private string _adminLiveScopeSummary = "Кампания и активная сессия не определены.";
    private string _adminLivePreviewSubject = "Участник не выбран";
    private string _adminLivePreviewLifeState = string.Empty;
    private ICommand? _refreshAdminLiveStateCommand;
    private ICommand? _loadAdminLiveActorCommand;
    private ICommand? _adjustAdminLiveResourceCommand;
    private ICommand? _previewAdminLiveStateCommand;

    public ObservableCollection<AdminLiveActorRowVm> AdminLiveActors { get; } = new();
    public ObservableCollection<AdminLiveResourceRowVm> AdminLiveResources { get; } = new();
    public ObservableCollection<AdminLiveEffectRowVm> AdminLiveEffects { get; } = new();
    public ObservableCollection<AdminLiveHistoryRowVm> AdminLiveHistory { get; } = new();
    public ObservableCollection<AdminLiveResourceRowVm> AdminLivePreviewResources { get; } = new();
    public ObservableCollection<AdminLivePreviewTextRowVm> AdminLivePreviewEffects { get; } = new();
    public ObservableCollection<AdminLivePreviewTextRowVm> AdminLivePreviewActions { get; } = new();
    public ObservableCollection<AdminLivePreviewTextRowVm> AdminLivePreviewWeapons { get; } = new();
    public AdminLiveActorRowVm? SelectedAdminLiveActor { get => _selectedAdminLiveActor; set { _selectedAdminLiveActor=value; Notify(); if(value!=null) LoadSelectedAdminLiveActor(); } }
    public AdminLiveResourceRowVm? SelectedAdminLiveResource { get => _selectedAdminLiveResource; set { _selectedAdminLiveResource=value; Notify(); } }
    public AdminLiveEffectRowVm? SelectedAdminLiveEffect { get => _selectedAdminLiveEffect; set { _selectedAdminLiveEffect=value; Notify(); } }
    public string AdminLiveStateStatus { get=>_adminLiveStateStatus; private set { _adminLiveStateStatus=value; Notify(); } }
    public string AdminLiveAdjustmentValue { get=>_adminLiveAdjustmentValue; set { _adminLiveAdjustmentValue=value; Notify(); } }
    public string AdminLiveAdjustmentReason { get=>_adminLiveAdjustmentReason; set { _adminLiveAdjustmentReason=value; Notify(); } }
    public string AdminLiveAdjustmentValidation { get=>_adminLiveAdjustmentValidation; private set { _adminLiveAdjustmentValidation=value; Notify(); } }
    public string AdminLivePreviewSummary { get=>_adminLivePreviewSummary; private set { _adminLivePreviewSummary=value; Notify(); } }
    public string AdminLiveScopeSummary { get=>_adminLiveScopeSummary; private set { _adminLiveScopeSummary=value; Notify(); } }
    public string AdminLivePreviewSubject { get=>_adminLivePreviewSubject; private set { _adminLivePreviewSubject=value; Notify(); } }
    public string AdminLivePreviewLifeState { get=>_adminLivePreviewLifeState; private set { _adminLivePreviewLifeState=value; Notify(); } }
    public ICommand RefreshAdminLiveStateCommand => _refreshAdminLiveStateCommand ??= new RelayCommand(LoadAdminLivePartyBoard);
    public ICommand LoadAdminLiveActorCommand => _loadAdminLiveActorCommand ??= new RelayCommand(LoadSelectedAdminLiveActor);
    public ICommand AdjustAdminLiveResourceCommand => _adjustAdminLiveResourceCommand ??= new RelayCommand(AdjustSelectedAdminLiveResource);
    public ICommand PreviewAdminLiveStateCommand => _previewAdminLiveStateCommand ??= new RelayCommand(LoadAdminLivePlayerPreview);

    private void LoadAdminLivePartyBoard()
    {
        AdminLiveStateStatus="Загружаю состояния участников...";
        var response=_api.ActorAdminPartyBoardGet();
        if(response.Status!=ResponseStatus.Ok){AdminLiveStateStatus=response.Message;return;}
        AdminLiveActors.Clear();
        var scope=response.Payload.TryGetValue("scope",out var scopeRaw)?AsMap(scopeRaw,CommandNames.ActorAdminPartyBoardGet):null;
        AdminLiveScopeSummary=scope==null?"Контекст активной группы не определён.":$"Кампания: {FirstNonEmpty(GetString(scope,"campaignName"),"Текущая кампания")}  •  Сессия: {FirstNonEmpty(GetString(scope,"sessionName"),"Без названия")}  •  Группа: {FirstNonEmpty(GetString(scope,"activeGroupName"),"Без названия")}";
        foreach(var raw in ToObjectList(response.Payload.TryGetValue("actors",out var value)?value:new ArrayList()))
        {
            var map=AsMap(raw,CommandNames.ActorAdminPartyBoardGet); if(map==null)continue;
            var resources=ToObjectList(map.TryGetValue("resources",out var resourceRaw)?resourceRaw:new ArrayList()).Cast<object>().Select(x=>AsMap(x,"admin live resource")).Where(x=>x!=null).Select(x=>$"{GetString(x!,"displayName")}: {GetString(x!,"current")}/{GetString(x!,"effectiveMaximum")}");
            var subjectType=GetString(map,"subjectType");
            AdminLiveActors.Add(new AdminLiveActorRowVm{SubjectId=GetString(map,"subjectId"),SubjectType=subjectType,SubjectTypeLabel=ReadableAdminSubjectType0216(subjectType),Name=GetString(map,"displayName"),LifeState=ReadableAdminLife0216(GetString(map,"lifeState")),Readiness=$"Действие: {(Bool0216(map,"canAct")?"да":"нет")}; реакция: {(Bool0216(map,"canReact")?"готова":"нет")}",ResourceSummary=string.Join("  •  ",resources),Revision=Long0216(GetString(map,"revision"))});
        }
        SelectedAdminLiveActor=AdminLiveActors.FirstOrDefault();
        AdminLiveStateStatus=AdminLiveActors.Count==0?"Активные runtime-состояния пока не созданы.":$"Участников: {AdminLiveActors.Count}. Обновлено {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
    }

    private static string ReadableAdminSubjectType0216(string value) => value.ToLowerInvariant() switch
    {
        "character" => "Персонаж",
        "companion" => "Компаньон",
        "npc" => "NPC",
        "summon" => "Призванное существо",
        "construct" => "Конструкт",
        "vehiclecrewactor" => "Экипаж",
        _ => "Другой участник"
    };

    private void LoadSelectedAdminLiveActor()
    {
        if(SelectedAdminLiveActor==null)return;
        var response=_api.CharacterAdminLiveStateGet(SelectedAdminLiveActor.SubjectId,SelectedAdminLiveActor.SubjectType);
        if(response.Status!=ResponseStatus.Ok){AdminLiveStateStatus=response.Message;return;}
        var map=response.Payload.TryGetValue("liveState",out var raw)?AsMap(raw,CommandNames.CharacterAdminLiveStateGet):null;if(map==null)return;
        BindAdminLiveDetails0216(map);
        AdminLiveStateStatus=$"Открыто состояние «{SelectedAdminLiveActor.Name}».";
    }

    private void AdjustSelectedAdminLiveResource()
    {
        if(SelectedAdminLiveActor==null||SelectedAdminLiveResource==null){AdminLiveStateStatus="Выберите участника и ресурс.";return;}
        if(!decimal.TryParse(AdminLiveAdjustmentValue,NumberStyles.Any,CultureInfo.CurrentCulture,out var amount)){AdminLiveAdjustmentValidation="Введите числовое изменение.";return;}
        if(string.IsNullOrWhiteSpace(AdminLiveAdjustmentReason)){AdminLiveAdjustmentValidation="Укажите причину изменения.";return;}
        AdminLiveAdjustmentValidation=string.Empty;
        var response=_api.CharacterAdminResourceAdjust(new Dictionary<string,object>{["subjectType"]=SelectedAdminLiveActor.SubjectType,["subjectId"]=SelectedAdminLiveActor.SubjectId,["resourceDefinitionId"]=SelectedAdminLiveResource.Id,["value"]=amount,["mode"]="adjust",["reason"]=AdminLiveAdjustmentReason,["expectedRevision"]=SelectedAdminLiveActor.Revision,["operationId"]=Guid.NewGuid().ToString("N")});
        AdminLiveStateStatus=response.Message;if(response.Status==ResponseStatus.Ok){LoadAdminLivePartyBoard();}
    }

    private void LoadAdminLivePlayerPreview()
    {
        if(SelectedAdminLiveActor==null)SelectedAdminLiveActor=AdminLiveActors.FirstOrDefault();
        if(SelectedAdminLiveActor==null){AdminLivePreviewSummary="Выберите участника.";return;}
        var response=_api.CharacterAdminLiveStateGetPlayerPreview(SelectedAdminLiveActor.SubjectId,SelectedAdminLiveActor.SubjectType);
        if(response.Status!=ResponseStatus.Ok){AdminLivePreviewSummary=response.Message;return;}
        var map=response.Payload.TryGetValue("liveState",out var raw)?AsMap(raw,CommandNames.CharacterAdminLiveStateGetPlayerPreview):null;
        if(map==null){AdminLivePreviewSummary="Предпросмотр недоступен.";return;}
        AdminLivePreviewSubject=FirstNonEmpty(GetString(map,"displayName"),"Участник");
        AdminLivePreviewLifeState=$"Состояние: {ReadableAdminLife0216(GetString(map,"lifeState"))}; действие: {(Bool0216(map,"canAct")?"доступно":"недоступно")}; реакция: {(Bool0216(map,"canReact")?"готова":"недоступна")}";
        AdminLivePreviewResources.Clear();
        foreach(var item in ToObjectList(map.TryGetValue("resources",out var resources)?resources:new ArrayList())){var m=AsMap(item,"admin player preview resource");if(m==null)continue;AdminLivePreviewResources.Add(new AdminLiveResourceRowVm{Id=GetString(m,"resourceId"),Name=GetString(m,"displayName"),Current=Decimal0216(m,"current"),Maximum=Decimal0216(m,"effectiveMaximum"),BaseMaximum=Decimal0216(m,"baseMaximum"),Reserved=Decimal0216(m,"reserved")});}
        AdminLivePreviewEffects.Clear();
        foreach(var item in ToObjectList(map.TryGetValue("effects",out var effects)?effects:new ArrayList())){var m=AsMap(item,"admin player preview effect");if(m==null)continue;var stacks=(int)Decimal0216(m,"stackCount");var rounds=(int)Decimal0216(m,"remainingRounds");AdminLivePreviewEffects.Add(new AdminLivePreviewTextRowVm{Title=GetString(m,"displayName")+(stacks>1?$" ×{stacks}":string.Empty),Detail=rounds>0?$"Осталось раундов: {rounds}":GetString(m,"description")});}
        AdminLivePreviewActions.Clear();
        foreach(var item in ToObjectList(map.TryGetValue("actions",out var actions)?actions:new ArrayList())){var m=AsMap(item,"admin player preview action");if(m==null)continue;AdminLivePreviewActions.Add(new AdminLivePreviewTextRowVm{Title=FirstNonEmpty(GetString(m,"displayName"),"Неизвестное действие"),Detail=Bool0216(m,"isEnabled")?"Готово":"Недоступно"});}
        AdminLivePreviewWeapons.Clear();
        foreach(var item in ToObjectList(map.TryGetValue("weapons",out var weapons)?weapons:new ArrayList())){var m=AsMap(item,"admin player preview weapon");if(m==null)continue;AdminLivePreviewWeapons.Add(new AdminLivePreviewTextRowVm{Title=FirstNonEmpty(GetString(m,"displayName"),"Оружие"),Detail=$"Заряжено: {GetString(m,"loadedQuantity")}; запас: {GetString(m,"reserveQuantity")}"});}
        AdminLivePreviewSummary="Показана серверная player-safe проекция. Скрытые сведения мастера исключены.";
    }

    private void BindAdminLiveDetails0216(Dictionary<string,object> map)
    {
        AdminLiveResources.Clear();foreach(var raw in ToObjectList(map.TryGetValue("resources",out var rv)?rv:new ArrayList())){var m=AsMap(raw,"admin live resources");if(m==null)continue;AdminLiveResources.Add(new AdminLiveResourceRowVm{Id=GetString(m,"resourceId"),Name=GetString(m,"displayName"),Current=Decimal0216(m,"current"),Maximum=Decimal0216(m,"effectiveMaximum"),BaseMaximum=Decimal0216(m,"baseMaximum"),Reserved=Decimal0216(m,"reserved")});}SelectedAdminLiveResource=AdminLiveResources.FirstOrDefault();
        AdminLiveEffects.Clear();foreach(var raw in ToObjectList(map.TryGetValue("effects",out var ev)?ev:new ArrayList())){var m=AsMap(raw,"admin live effects");if(m==null)continue;AdminLiveEffects.Add(new AdminLiveEffectRowVm{Id=GetString(m,"effectInstanceId"),Name=GetString(m,"displayName"),Description=GetString(m,"description"),Rounds=(int)Decimal0216(m,"remainingRounds")});}
        AdminLiveHistory.Clear();foreach(var raw in ToObjectList(map.TryGetValue("history",out var hv)?hv:new ArrayList())){var m=AsMap(raw,"admin live history");if(m==null)continue;AdminLiveHistory.Add(new AdminLiveHistoryRowVm{Text=GetString(m,"displayText"),Change=$"{GetString(m,"oldSummary")} → {GetString(m,"newSummary")}",Time=FormatAdminTime0216(GetString(m,"createdAtUtc"))});}
    }
    private static decimal Decimal0216(Dictionary<string,object> map,string key)=>decimal.TryParse(GetString(map,key),NumberStyles.Any,CultureInfo.InvariantCulture,out var value)?value:0;
    private static IList ToObjectList(object? value) => value as IList ?? new ArrayList();
    private static string GetString(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
    private static long Long0216(string value)=>long.TryParse(value,out var parsed)?parsed:0;
    private static bool Bool0216(Dictionary<string,object> map,string key)=>map.TryGetValue(key,out var value)&&bool.TryParse(Convert.ToString(value),out var parsed)&&parsed;
    private static string FormatAdminTime0216(string value)=>DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var parsed)?parsed.ToLocalTime().ToString("dd.MM.yyyy HH:mm"):string.Empty;
    private static string ReadableAdminLife0216(string value)=>value.ToLowerInvariant() switch{"active" or "healthy"=>"В норме","impaired"=>"Ослаблен","incapacitated"=>"Недееспособен","unconscious"=>"Без сознания","dying"=>"При смерти","stable"=>"Стабилен","dead"=>"Мёртв","destroyed"=>"Уничтожен",_=>"Не определено"};
}
