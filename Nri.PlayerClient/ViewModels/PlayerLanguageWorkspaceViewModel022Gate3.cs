using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerLanguageRowVm022Gate3
{
    public string LanguageId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string LevelLabel { get; set; } = "Не изучен";
    public string RolesText { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string LevelText => Level > 0 ? $"Уровень {Level}/5 · {LevelLabel}" : "Не изучен";
    public string SourceText => Level > 0 && !string.IsNullOrWhiteSpace(SourceLabel) ? $"Источник: {SourceLabel}" : string.Empty;
}

public sealed class PlayerLanguageTrainingSourceVm022Gate3
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public override string ToString() => Label;
}

public sealed class PlayerLanguageTrainingVm022Gate3
{
    public string ProjectId { get; set; } = string.Empty;
    public string LanguageId { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public int Revision { get; set; }
    public int AccumulatedHours { get; set; }
    public int RequiredHours { get; set; }
    public int RequiredMo { get; set; }
    public string SourceLabel { get; set; } = string.Empty;
    public string SourceStatusLabel { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string ProgressText => $"{AccumulatedHours} из {RequiredHours} ч · {RequiredMo} MO при завершении";
}

public sealed class PlayerLanguageWorkspaceViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _characterId = string.Empty;
    private PlayerLanguageRowVm022Gate3? _selectedLanguage;
    private PlayerLanguageTrainingSourceVm022Gate3? _selectedSource;
    private string _sourceLabel = "Самостоятельные занятия";
    private string _statusText = "Выберите активного персонажа.";
    private string _detailText = "Выберите язык, чтобы увидеть его описание.";
    private string _metadataText = string.Empty;
    private string _levelDescriptionsText = string.Empty;
    private string _originTraditionsText = string.Empty;
    private string _limitationsText = string.Empty;
    private string _requirementsText = "Требования появятся после выбора языка.";
    private PlayerLanguageTrainingVm022Gate3? _activeTraining;
    private readonly Dictionary<string, PlayerLanguageTrainingVm022Gate3> _activeTrainingByLanguageId = new(StringComparer.Ordinal);

    public PlayerLanguageWorkspaceViewModel(CommandApi api)
    {
        _api = api;
        Sources.Add(new PlayerLanguageTrainingSourceVm022Gate3 { Value = LanguageTrainingSourceTypeIds022Gate3.SelfStudy, Label = "Самостоятельное изучение" });
        Sources.Add(new PlayerLanguageTrainingSourceVm022Gate3 { Value = LanguageTrainingSourceTypeIds022Gate3.Teacher, Label = "Преподаватель" });
        Sources.Add(new PlayerLanguageTrainingSourceVm022Gate3 { Value = LanguageTrainingSourceTypeIds022Gate3.ActiveImmersion, Label = "Живая языковая среда" });
        Sources.Add(new PlayerLanguageTrainingSourceVm022Gate3 { Value = LanguageTrainingSourceTypeIds022Gate3.TeachingMaterials, Label = "Учебные материалы" });
        Sources.Add(new PlayerLanguageTrainingSourceVm022Gate3 { Value = LanguageTrainingSourceTypeIds022Gate3.ReligiousCorpus, Label = "Религиозный корпус текстов" });
        Sources.Add(new PlayerLanguageTrainingSourceVm022Gate3 { Value = LanguageTrainingSourceTypeIds022Gate3.ArchiveResearch, Label = "Архивное исследование" });
        SelectedSource = Sources[0];
        RefreshCommand = new RelayCommand(() => Refresh(_characterId));
        StartTrainingCommand = new RelayCommand(StartTraining);
        CompleteTrainingCommand = new RelayCommand(CompleteTraining);
    }

    public ObservableCollection<PlayerLanguageRowVm022Gate3> Languages { get; } = new();
    public ObservableCollection<PlayerLanguageTrainingSourceVm022Gate3> Sources { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand StartTrainingCommand { get; }
    public ICommand CompleteTrainingCommand { get; }
    public PlayerLanguageRowVm022Gate3? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            _selectedLanguage = value;
            Notify();
            ActiveTraining = value != null && _activeTrainingByLanguageId.TryGetValue(value.LanguageId, out var training)
                ? training
                : null;
            LoadSelectedLanguage();
        }
    }
    public PlayerLanguageTrainingSourceVm022Gate3? SelectedSource { get => _selectedSource; set { _selectedSource = value; Notify(); } }
    public string SourceLabel { get => _sourceLabel; set { _sourceLabel = value ?? string.Empty; Notify(); } }
    public string StatusText { get => _statusText; private set { _statusText = value; Notify(); } }
    public string DetailText { get => _detailText; private set { _detailText = value; Notify(); } }
    public string MetadataText { get => _metadataText; private set { _metadataText = value; Notify(); } }
    public string LevelDescriptionsText { get => _levelDescriptionsText; private set { _levelDescriptionsText = value; Notify(); } }
    public string OriginTraditionsText { get => _originTraditionsText; private set { _originTraditionsText = value; Notify(); } }
    public string LimitationsText { get => _limitationsText; private set { _limitationsText = value; Notify(); } }
    public string RequirementsText { get => _requirementsText; private set { _requirementsText = value; Notify(); } }
    public PlayerLanguageTrainingVm022Gate3? ActiveTraining { get => _activeTraining; private set { _activeTraining = value; Notify(); Notify(nameof(HasActiveTraining)); } }
    public bool HasActiveTraining => ActiveTraining != null;

    public void Clear()
    {
        _characterId = string.Empty;
        Languages.Clear();
        _activeTrainingByLanguageId.Clear();
        SelectedLanguage = null;
        StatusText = "Выберите активного персонажа.";
    }

    public void Refresh(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) { Clear(); return; }
        _characterId = characterId;
        StatusText = "Загрузка языков...";
        var catalog = _api.ContentDefinitionPlayerLanguagesList();
        var summary = _api.CharacterLanguageSummaryGet(characterId);
        if (catalog.Status != ResponseStatus.Ok || summary.Status != ResponseStatus.Ok)
        {
            StatusText = FirstNonEmpty(summary.Message, catalog.Message, "Не удалось загрузить языки.");
            return;
        }

        var known = Items(summary.Payload, "languages").Select(Map).Where(x => x != null).ToDictionary(x => Text(x!, "languageId"), x => x!, StringComparer.Ordinal);
        var previousId = SelectedLanguage?.LanguageId;
        Languages.Clear();
        foreach (var item in Items(catalog.Payload, "languages").Select(Map).Where(x => x != null))
        {
            var id = Text(item!, "languageId");
            known.TryGetValue(id, out var proficiency);
            Languages.Add(new PlayerLanguageRowVm022Gate3
            {
                LanguageId = id,
                Name = Text(item!, "name"),
                Level = Number(proficiency, "level"),
                LevelLabel = FirstNonEmpty(Text(proficiency, "levelLabel"), "Не изучен"),
                SourceLabel = Text(proficiency, "sourceLabel"),
                RolesText = string.Join(", ", Items(item!, "roles").Cast<object>().Select(x => LanguageRoleLabel(Convert.ToString(x) ?? string.Empty))),
                Summary = Text(item!, "summary")
            });
        }
        _activeTrainingByLanguageId.Clear();
        foreach (var training in Items(summary.Payload, "activeTraining").Select(Map).Where(x => x != null).Select(x => Training(x!)))
        {
            if (!string.IsNullOrWhiteSpace(training.LanguageId))
                _activeTrainingByLanguageId[training.LanguageId] = training;
        }
        SelectedLanguage = Languages.FirstOrDefault(x => x.LanguageId == previousId) ?? Languages.FirstOrDefault(x => x.Level > 0) ?? Languages.FirstOrDefault();
        StatusText = Languages.Count == 0 ? "Доступные языки пока не опубликованы GM." : $"Доступно языков: {Languages.Count}.";
    }

    private void LoadSelectedLanguage()
    {
        var row = SelectedLanguage;
        if (row == null || string.IsNullOrWhiteSpace(_characterId))
        {
            DetailText = "Выберите язык, чтобы увидеть его описание.";
            MetadataText = string.Empty;
            LevelDescriptionsText = string.Empty;
            OriginTraditionsText = string.Empty;
            LimitationsText = string.Empty;
            RequirementsText = "Требования появятся после выбора языка.";
            return;
        }
        var detail = _api.ContentDefinitionPlayerLanguageGet(row.LanguageId);
        var requirements = _api.CharacterLanguageTrainingRequirementsGet(_characterId, row.LanguageId);
        DetailText = detail.Status == ResponseStatus.Ok ? FirstNonEmpty(Text(detail.Payload, "description"), row.Summary, "Описание пока не добавлено.") : detail.Message;
        MetadataText = detail.Status == ResponseStatus.Ok
            ? BuildMetadata(detail.Payload)
            : string.Empty;
        LevelDescriptionsText = detail.Status == ResponseStatus.Ok
            ? FormatLevelDescriptions(Items(detail.Payload, "levelDescriptions"))
            : string.Empty;
        OriginTraditionsText = detail.Status == ResponseStatus.Ok
            ? FormatTraditions(Items(detail.Payload, "originTraditions"))
            : string.Empty;
        var limitations = Text(detail.Payload, "usageLimitations");
        LimitationsText = string.IsNullOrWhiteSpace(limitations) ? "Особых ограничений применения нет." : limitations;
        RequirementsText = requirements.Status == ResponseStatus.Ok
            ? Number(requirements.Payload, "canTrain") == 0 && string.Equals(Text(requirements.Payload, "canTrain"), "False", StringComparison.OrdinalIgnoreCase)
                ? FirstNonEmpty(Text(requirements.Payload, "blockReason"), "Обучение недоступно.")
                : $"Следующий уровень: {Number(requirements.Payload, "targetLevel")}/5 · {Number(requirements.Payload, "requiredStudyHours")} ч · {Number(requirements.Payload, "requiredMo")} MO. {Text(requirements.Payload, "sourceRequirement")}"
            : requirements.Message;
    }

    private void StartTraining()
    {
        if (SelectedLanguage == null || SelectedSource == null) { StatusText = "Выберите язык и источник обучения."; return; }
        var response = _api.CharacterLanguageTrainingStart(new Dictionary<string, object>
        {
            ["characterId"] = _characterId, ["languageId"] = SelectedLanguage.LanguageId,
            ["sourceType"] = SelectedSource.Value, ["sourceLabel"] = FirstNonEmpty(SourceLabel, SelectedSource.Label),
            ["operationId"] = "language-start-" + Guid.NewGuid().ToString("N")
        });
        StatusText = response.Message;
        if (response.Status == ResponseStatus.Ok) Refresh(_characterId);
    }

    private void CompleteTraining()
    {
        if (ActiveTraining == null) { StatusText = "Активное обучение не найдено."; return; }
        var response = _api.CharacterLanguageTrainingComplete(new Dictionary<string, object>
        {
            ["characterId"] = _characterId, ["projectId"] = ActiveTraining.ProjectId,
            ["expectedRevision"] = ActiveTraining.Revision, ["operationId"] = "language-complete-" + Guid.NewGuid().ToString("N")
        });
        StatusText = response.Message;
        if (response.Status == ResponseStatus.Ok) Refresh(_characterId);
    }

    private static PlayerLanguageTrainingVm022Gate3 Training(Dictionary<string, object> map) => new()
    {
        ProjectId = Text(map, "projectId"), LanguageId = Text(map, "languageId"), LanguageName = Text(map, "languageName"), Revision = Number(map, "revision"),
        AccumulatedHours = Number(map, "accumulatedStudyHours"), RequiredHours = Number(map, "requiredStudyHours"), RequiredMo = Number(map, "requiredMo"),
        SourceLabel = Text(map, "sourceLabel"), SourceStatusLabel = Text(map, "sourceStatusLabel"), StatusLabel = Text(map, "statusLabel")
    };
    private static IEnumerable<object> Items(IDictionary<string, object>? map, string key) => map != null && map.TryGetValue(key, out var value) && value is IEnumerable items && value is not string ? items.Cast<object>() : Array.Empty<object>();
    private static Dictionary<string, object>? Map(object? value) => value as Dictionary<string, object> ?? (value as IDictionary)?.Cast<DictionaryEntry>().ToDictionary(x => Convert.ToString(x.Key) ?? string.Empty, x => x.Value!);
    private static string Text(IDictionary<string, object>? map, string key) => map != null && map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static int Number(IDictionary<string, object>? map, string key) => int.TryParse(Text(map, key), out var value) ? value : 0;
    private static string FormatLevelDescriptions(IEnumerable<object> values)
    {
        var rows = values.Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return rows.Length == 0 ? "Описание уровней пока не опубликовано." : string.Join(Environment.NewLine, rows.Select((x, i) => $"{i + 1}. {x}"));
    }
    private static string FormatTraditions(IEnumerable<object> values)
    {
        var rows = values.Select(Map).Where(x => x != null)
            .Select(x => $"{Text(x, "name")}: {Text(x, "description")}".Trim().TrimEnd(':')).ToArray();
        return rows.Length == 0 ? "Особая традиция происхождения не указана." : string.Join(Environment.NewLine, rows);
    }
    private static string BuildMetadata(IDictionary<string, object> detail)
    {
        var primary = string.Join(" · ", new[]
        {
            Text(detail, "family"), Text(detail, "script"),
            string.Join(", ", Items(detail, "roles").Select(x => LanguageRoleLabel(Convert.ToString(x) ?? string.Empty)))
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var relations = Items(detail, "ancestors").Concat(Items(detail, "contactInfluences"))
            .Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        var cultures = Items(detail, "cultures").Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return string.Join(Environment.NewLine, new[]
        {
            primary,
            relations.Length == 0 ? string.Empty : "Известные связи: " + string.Join(", ", relations),
            cultures.Length == 0 ? string.Empty : "Культуры и области: " + string.Join(", ", cultures)
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
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
