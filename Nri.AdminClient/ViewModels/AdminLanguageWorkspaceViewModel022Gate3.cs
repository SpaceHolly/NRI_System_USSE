using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminLanguageChoiceVm022Gate3
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
    public int Level { get; set; }
    public string LevelLabel { get; set; } = "Не изучен";
    public string SourceLabel { get; set; } = string.Empty;
    public string Summary => Level > 0 ? $"Уровень {Level}/5 · {LevelLabel} · {SourceLabel}" : "Не изучен";
    public override string ToString() => Name;
}

public sealed class AdminLanguageLevelChoiceVm022Gate3
{
    public int Value { get; set; }
    public string Label { get; set; } = string.Empty;
    public override string ToString() => Label;
}

public sealed class AdminLanguageTrainingVm022Gate3
{
    public string ProjectId { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public int Revision { get; set; }
    public int Done { get; set; }
    public int Required { get; set; }
    public int RequiredMo { get; set; }
    public string SourceLabel { get; set; } = string.Empty;
    public string SourceStatusLabel { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string Summary => $"{Done} из {Required} ч · {RequiredMo} MO · {SourceStatusLabel}";
}

public sealed class AdminLanguageWorkspaceViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _characterId = string.Empty;
    private int _revision = 1;
    private AdminLanguageChoiceVm022Gate3? _selectedLanguage;
    private AdminLanguageLevelChoiceVm022Gate3? _selectedLevel;
    private AdminLanguageTrainingVm022Gate3? _selectedTraining;
    private string _reason = "Изменение GM с указанием причины";
    private string _worldTimeReference = "Подтверждённый период игрового времени";
    private int _studyHours = 4;
    private string _sourceDecisionReason = "Источник проверен GM";
    private string _status = "Выберите персонажа.";

    public AdminLanguageWorkspaceViewModel(CommandApi api)
    {
        _api = api;
        for (var i = 0; i <= 5; i++) Levels.Add(new AdminLanguageLevelChoiceVm022Gate3 { Value = i, Label = $"{i}/5 · {LevelLabel(i)}" });
        SelectedLevel = Levels[0];
        RefreshCommand = new RelayCommand(() => Refresh(_characterId));
        GrantCommand = new RelayCommand(Grant);
        CreditCommand = new RelayCommand(Credit);
        ApproveSourceCommand = new RelayCommand(() => DecideSource(true));
        RejectSourceCommand = new RelayCommand(() => DecideSource(false));
    }

    public ObservableCollection<AdminLanguageChoiceVm022Gate3> Languages { get; } = new();
    public ObservableCollection<AdminLanguageLevelChoiceVm022Gate3> Levels { get; } = new();
    public ObservableCollection<AdminLanguageTrainingVm022Gate3> ActiveTraining { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand GrantCommand { get; }
    public ICommand CreditCommand { get; }
    public ICommand ApproveSourceCommand { get; }
    public ICommand RejectSourceCommand { get; }
    public AdminLanguageChoiceVm022Gate3? SelectedLanguage
    {
        get => _selectedLanguage;
        set { _selectedLanguage = value; Notify(); Notify(nameof(SelectedLanguageId)); if (value != null) SelectedLevel = Levels.First(x => x.Value == value.Level); }
    }
    public string SelectedLanguageId
    {
        get => SelectedLanguage?.Id ?? string.Empty;
        set { var match = Languages.FirstOrDefault(x => x.Id == value); if (match != null && !ReferenceEquals(match, SelectedLanguage)) SelectedLanguage = match; }
    }
    public AdminLanguageLevelChoiceVm022Gate3? SelectedLevel { get => _selectedLevel; set { _selectedLevel = value; Notify(); } }
    public AdminLanguageTrainingVm022Gate3? SelectedTraining { get => _selectedTraining; set { _selectedTraining = value; Notify(); } }
    public string Reason { get => _reason; set { _reason = value ?? string.Empty; Notify(); } }
    public string WorldTimeReference { get => _worldTimeReference; set { _worldTimeReference = value ?? string.Empty; Notify(); } }
    public int StudyHours { get => _studyHours; set { _studyHours = value; Notify(); } }
    public string SourceDecisionReason { get => _sourceDecisionReason; set { _sourceDecisionReason = value ?? string.Empty; Notify(); } }
    public string Status { get => _status; private set { _status = value; Notify(); } }

    public void Refresh(string characterId)
    {
        _characterId = characterId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_characterId)) { Languages.Clear(); ActiveTraining.Clear(); Status = "Выберите персонажа."; return; }
        Status = "Загрузка языкового профиля...";
        var catalog = _api.ContentDefinitionPlayerLanguagesList();
        var summary = _api.CharacterLanguageSummaryGet(_characterId);
        if (catalog.Status != ResponseStatus.Ok || summary.Status != ResponseStatus.Ok) { Status = First(summary.Message, catalog.Message, "Не удалось загрузить языковой профиль."); return; }
        _revision = Number(summary.Payload, "revision");
        var known = Items(summary.Payload, "languages").Select(Map).Where(x => x != null).ToDictionary(x => Text(x, "languageId"), x => x!, StringComparer.Ordinal);
        var selectedId = SelectedLanguage?.Id;
        Languages.Clear();
        foreach (var raw in Items(catalog.Payload, "languages"))
        {
            var map = Map(raw); if (map == null) continue;
            var id = Text(map, "languageId"); known.TryGetValue(id, out var value);
            Languages.Add(new AdminLanguageChoiceVm022Gate3
            {
                Id=id, Name=Text(map,"name"), Roles=string.Join(", ", Items(map,"roles").Select(x => LanguageRoleLabel(Convert.ToString(x) ?? string.Empty))),
                Level=Number(value,"level"), LevelLabel=First(Text(value,"levelLabel"),"Не изучен"), SourceLabel=Text(value,"sourceLabel")
            });
        }
        ActiveTraining.Clear();
        foreach (var raw in Items(summary.Payload, "activeTraining"))
        {
            var map=Map(raw); if(map==null) continue;
            ActiveTraining.Add(new AdminLanguageTrainingVm022Gate3
            {
                ProjectId=Text(map,"projectId"),LanguageName=Text(map,"languageName"),Revision=Number(map,"revision"),Done=Number(map,"accumulatedStudyHours"),
                Required=Number(map,"requiredStudyHours"),RequiredMo=Number(map,"requiredMo"),SourceLabel=Text(map,"sourceLabel"),SourceStatusLabel=Text(map,"sourceStatusLabel"),StatusLabel=Text(map,"statusLabel")
            });
        }
        SelectedLanguage = Languages.FirstOrDefault(x => x.Id == selectedId) ?? Languages.FirstOrDefault(x => x.Level > 0) ?? Languages.FirstOrDefault();
        SelectedTraining = ActiveTraining.FirstOrDefault();
        Status = $"Языков: {Languages.Count}; активных проектов: {ActiveTraining.Count}.";
    }

    private void Grant()
    {
        if (SelectedLanguage == null || SelectedLevel == null) { Status = "Выберите язык и уровень."; return; }
        var response = _api.CharacterAdminLanguageGrant(new Dictionary<string, object>
        {
            ["characterId"]=_characterId,["languageId"]=SelectedLanguage.Id,["level"]=SelectedLevel.Value,["expectedRevision"]=_revision,
            ["reason"]=First(Reason,"Причина изменения указана GM"),["operationId"]="language-admin-"+Guid.NewGuid().ToString("N")
        });
        Status=response.Message; if(response.Status==ResponseStatus.Ok) Refresh(_characterId);
    }
    private void Credit()
    {
        if (SelectedTraining == null) { Status="Выберите проект обучения."; return; }
        var response=_api.CharacterAdminLanguageTrainingCredit(new Dictionary<string, object>
        {
            ["projectId"]=SelectedTraining.ProjectId,["expectedRevision"]=SelectedTraining.Revision,["studyHours"]=StudyHours,
            ["worldTimeReference"]=First(WorldTimeReference,"Подтверждённое игровое время"),["operationId"]="language-credit-"+Guid.NewGuid().ToString("N")
        });
        Status=response.Message; if(response.Status==ResponseStatus.Ok) Refresh(_characterId);
    }
    private void DecideSource(bool approved)
    {
        if (SelectedTraining == null) { Status="Выберите проект обучения."; return; }
        var response=_api.CharacterAdminLanguageTrainingSourceApprove(new Dictionary<string, object>
        {
            ["projectId"]=SelectedTraining.ProjectId,["expectedRevision"]=SelectedTraining.Revision,["approved"]=approved,
            ["reason"]=First(SourceDecisionReason,"Решение GM по источнику"),
            ["operationId"]="language-source-"+Guid.NewGuid().ToString("N")
        });
        Status=response.Message; if(response.Status==ResponseStatus.Ok) Refresh(_characterId);
    }
    private static string LevelLabel(int value) => value switch { 0=>"Неизвестен",1=>"Начальные знания",2=>"Бытовое владение",3=>"Свободное владение",4=>"Высокое владение",_=>"Глубокое мастерство" };
    private static IEnumerable<object> Items(IDictionary<string,object>? map,string key)=>map!=null&&map.TryGetValue(key,out var v)&&v is IEnumerable e&&v is not string?e.Cast<object>():Array.Empty<object>();
    private static Dictionary<string,object>? Map(object? value)=>value as Dictionary<string,object>??(value as IDictionary)?.Cast<DictionaryEntry>().ToDictionary(x=>Convert.ToString(x.Key)??string.Empty,x=>x.Value!);
    private static string Text(IDictionary<string,object>? map,string key)=>map!=null&&map.TryGetValue(key,out var v)?Convert.ToString(v)??string.Empty:string.Empty;
    private static int Number(IDictionary<string,object>? map,string key)=>int.TryParse(Text(map,key),out var v)?v:0;
    private static string First(params string[] values)=>values.FirstOrDefault(x=>!string.IsNullOrWhiteSpace(x))??string.Empty;
    private static string LanguageRoleLabel(string value) => value switch
    {
        LanguageRoleIds022Gate3.Continental => "общий язык континента",
        LanguageRoleIds022Gate3.State => "государственный",
        LanguageRoleIds022Gate3.PoliticalCultural => "политический и культурный",
        LanguageRoleIds022Gate3.Racial => "культурное наследие",
        LanguageRoleIds022Gate3.Religious => "религиозный",
        LanguageRoleIds022Gate3.Ancient => "древний",
        LanguageRoleIds022Gate3.Contact => "контактный",
        _ => string.IsNullOrWhiteSpace(value) ? "другое назначение" : value
    };
}
