using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerProposalTemplateVm
{
    public string ProposalType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Display => string.IsNullOrWhiteSpace(Description) ? Name : $"{Name} - {Description}";
}

public sealed class PlayerProposalDraftVm
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ProposalType { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public string Display => $"{FirstNonEmpty(Title, "Без названия")} | {FirstNonEmpty(TypeLabel, ProposalType)} | {FirstNonEmpty(StatusLabel, Status)}";
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

public sealed class PlayerProposalFieldVm : ViewModelBase
{
    private string _value = string.Empty;

    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public bool IsRequired { get; set; }

    public string Value
    {
        get => _value;
        set { if (_value != value) { _value = value ?? string.Empty; Notify(); } }
    }

    public string DisplayLabel => IsRequired ? $"{Label} *" : Label;
}

public sealed class PlayerProposalCenterViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdProvider;
    private PlayerProposalTemplateVm? _selectedTemplate;
    private PlayerProposalDraftVm? _selectedDraft;
    private string _titleInput = string.Empty;
    private string _descriptionInput = string.Empty;
    private string _priorityInput = "normal";
    private string _playerCommentInput = string.Empty;
    private string _statusMessage = "Центр предложений готов к загрузке.";
    private string _errorMessage = string.Empty;
    private string _previewText = string.Empty;

    public PlayerProposalCenterViewModel(CommandApi api, Func<string> activeCharacterIdProvider)
    {
        _api = api;
        _activeCharacterIdProvider = activeCharacterIdProvider;
        RefreshCommand = new RelayCommand(Refresh);
        NewDraftCommand = new RelayCommand(NewDraftFromTemplate);
        SaveDraftCommand = new RelayCommand(SaveDraft);
        ValidateDraftCommand = new RelayCommand(ValidateDraft);
        PreviewDraftCommand = new RelayCommand(PreviewDraft);
        SubmitDraftCommand = new RelayCommand(SubmitDraft);
        CancelDraftCommand = new RelayCommand(CancelDraft);
        LoadDraftCommand = new RelayCommand(LoadSelectedDraft);
        LoadDefaultFields("generic_gm_request");
    }

    public ObservableCollection<PlayerProposalTemplateVm> Templates { get; } = new();
    public ObservableCollection<PlayerProposalDraftVm> Drafts { get; } = new();
    public ObservableCollection<PlayerProposalFieldVm> Fields { get; } = new();
    public ObservableCollection<string> ValidationRows { get; } = new();
    public string[] PriorityOptions { get; } = { "low", "normal", "high", "urgent" };

    public PlayerProposalTemplateVm? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (_selectedTemplate != value)
            {
                _selectedTemplate = value;
                Notify();
                if (value != null)
                {
                    TitleInput = value.Name;
                    DescriptionInput = value.Description;
                    LoadDefaultFields(value.ProposalType);
                }
            }
        }
    }

    public PlayerProposalDraftVm? SelectedDraft
    {
        get => _selectedDraft;
        set { if (_selectedDraft != value) { _selectedDraft = value; Notify(); } }
    }

    public string TitleInput { get => _titleInput; set { if (_titleInput != value) { _titleInput = value ?? string.Empty; Notify(); } } }
    public string DescriptionInput { get => _descriptionInput; set { if (_descriptionInput != value) { _descriptionInput = value ?? string.Empty; Notify(); } } }
    public string PriorityInput { get => _priorityInput; set { if (_priorityInput != value) { _priorityInput = value ?? "normal"; Notify(); } } }
    public string PlayerCommentInput { get => _playerCommentInput; set { if (_playerCommentInput != value) { _playerCommentInput = value ?? string.Empty; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value ?? string.Empty; Notify(); } }
    public string ErrorMessage { get => _errorMessage; private set { _errorMessage = value ?? string.Empty; Notify(); } }
    public string PreviewText { get => _previewText; private set { _previewText = value ?? string.Empty; Notify(); } }

    public ICommand RefreshCommand { get; }
    public ICommand NewDraftCommand { get; }
    public ICommand SaveDraftCommand { get; }
    public ICommand ValidateDraftCommand { get; }
    public ICommand PreviewDraftCommand { get; }
    public ICommand SubmitDraftCommand { get; }
    public ICommand CancelDraftCommand { get; }
    public ICommand LoadDraftCommand { get; }

    public void Refresh()
    {
        ErrorMessage = string.Empty;
        LoadTemplates();
        LoadDrafts();
    }

    private void LoadTemplates()
    {
        Templates.Clear();
        var response = _api.ProposalPlayerTemplateList();
        if (!IsOk(response))
        {
            StatusMessage = DisabledOrError(response, "Предложения пока недоступны.");
            AddBuiltInTemplatesFallback();
            return;
        }

        foreach (var item in GetItems(response))
        {
            var map = AsMap(item);
            Templates.Add(new PlayerProposalTemplateVm
            {
                ProposalType = S(map, "proposalType"),
                Name = FirstNonEmpty(S(map, "name"), S(map, "title"), S(map, "proposalTypeLabel")),
                Description = FirstNonEmpty(S(map, "description"), S(map, "publicSummary"))
            });
        }

        if (Templates.Count == 0) AddBuiltInTemplatesFallback();
        SelectedTemplate ??= Templates.FirstOrDefault();
        StatusMessage = $"Шаблонов: {Templates.Count}. Черновиков: {Drafts.Count}.";
    }

    private void LoadDrafts()
    {
        Drafts.Clear();
        var response = _api.ProposalPlayerDraftListMine();
        if (!IsOk(response))
        {
            StatusMessage = DisabledOrError(response, "Черновики предложений недоступны.");
            return;
        }

        foreach (var item in GetItems(response))
            Drafts.Add(ToDraftVm(AsMap(item)));

        StatusMessage = $"Шаблонов: {Templates.Count}. Черновиков: {Drafts.Count}.";
    }

    private void NewDraftFromTemplate()
    {
        SelectedDraft = null;
        var type = SelectedTemplate?.ProposalType ?? "generic_gm_request";
        TitleInput = SelectedTemplate?.Name ?? DefaultTitle(type);
        DescriptionInput = SelectedTemplate?.Description ?? string.Empty;
        PlayerCommentInput = string.Empty;
        PreviewText = string.Empty;
        ValidationRows.Clear();
        LoadDefaultFields(type);
        StatusMessage = "Новый черновик подготовлен. Заполните поля и нажмите 'Сохранить черновик'.";
    }

    private void SaveDraft()
    {
        RunProposalAction(() =>
        {
            var payload = BuildDraftPayload();
            var response = string.IsNullOrWhiteSpace(SelectedDraft?.Id)
                ? _api.ProposalPlayerDraftCreate(payload)
                : _api.ProposalPlayerDraftUpdate(new Dictionary<string, object>(payload) { ["proposalDraftId"] = SelectedDraft!.Id });
            ApplySingleResponse(response, "Черновик сохранён.");
            LoadDrafts();
        });
    }

    private void ValidateDraft()
    {
        if (!EnsureSelectedDraft()) return;
        RunProposalAction(() => ApplySingleResponse(_api.ProposalPlayerDraftValidate(new Dictionary<string, object> { ["proposalDraftId"] = SelectedDraft!.Id }), "Проверка завершена."));
    }

    private void PreviewDraft()
    {
        if (!EnsureSelectedDraft()) return;
        RunProposalAction(() => ApplySingleResponse(_api.ProposalPlayerDraftPreview(new Dictionary<string, object> { ["proposalDraftId"] = SelectedDraft!.Id }), "Предпросмотр обновлён."));
    }

    private void SubmitDraft()
    {
        if (!EnsureSelectedDraft()) return;
        RunProposalAction(() =>
        {
            ApplySingleResponse(_api.ProposalPlayerDraftSubmit(new Dictionary<string, object> { ["proposalDraftId"] = SelectedDraft!.Id, ["createPlayerRequest"] = true }), "Предложение отправлено GM.");
            LoadDrafts();
        });
    }

    private void CancelDraft()
    {
        if (!EnsureSelectedDraft()) return;
        RunProposalAction(() =>
        {
            ApplySingleResponse(_api.ProposalPlayerDraftCancel(new Dictionary<string, object> { ["proposalDraftId"] = SelectedDraft!.Id }), "Предложение отменено.");
            LoadDrafts();
        });
    }

    private void LoadSelectedDraft()
    {
        if (!EnsureSelectedDraft()) return;
        RunProposalAction(() => ApplySingleResponse(_api.ProposalPlayerDraftGetMine(new Dictionary<string, object> { ["proposalDraftId"] = SelectedDraft!.Id }), "Черновик открыт."));
    }

    private Dictionary<string, object> BuildDraftPayload()
    {
        var type = SelectedTemplate?.ProposalType ?? SelectedDraft?.ProposalType ?? "generic_gm_request";
        return new Dictionary<string, object>
        {
            ["proposalType"] = type,
            ["title"] = FirstNonEmpty(TitleInput, DefaultTitle(type)),
            ["description"] = DescriptionInput,
            ["priority"] = PriorityInput,
            ["characterId"] = _activeCharacterIdProvider() ?? string.Empty,
            ["playerComment"] = PlayerCommentInput,
            ["sourceView"] = "player_proposal_center",
            ["structuredPayload"] = Fields.ToDictionary(x => x.Key, x => (object)(x.Value ?? string.Empty), StringComparer.OrdinalIgnoreCase)
        };
    }

    private void ApplySingleResponse(ResponseEnvelope response, string okMessage)
    {
        if (!IsOk(response))
        {
            ErrorMessage = DisabledOrError(response, "Операция с предложением не выполнена.");
            return;
        }

        var item = AsMap(GetValue(response.Payload, "item"));
        if (item.Count > 0)
        {
            SelectedDraft = ToDraftVm(item);
            TitleInput = S(item, "title");
            DescriptionInput = S(item, "description");
            PlayerCommentInput = S(item, "playerComment");
            LoadFieldsFromPayload(item);
            PreviewText = FirstNonEmpty(S(item, "summary"), S(item, "publicSummary"), S(item, "description"));
        }

        ValidationRows.Clear();
        var validation = AsMap(GetValue(response.Payload, "validation"));
        AddValidationRows(validation);
        StatusMessage = okMessage;
    }

    private void LoadFieldsFromPayload(IDictionary<string, object> item)
    {
        var type = FirstNonEmpty(S(item, "proposalType"), SelectedTemplate?.ProposalType ?? "generic_gm_request");
        LoadDefaultFields(type);
        foreach (var field in GetList(item, "structuredFields"))
        {
            var map = AsMap(field);
            var key = S(map, "key");
            var existing = Fields.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (existing != null) existing.Value = S(map, "value");
        }
    }

    private void AddValidationRows(IDictionary<string, object> validation)
    {
        if (validation.Count == 0) return;
        AddRow("Статус", FirstNonEmpty(S(validation, "summary"), S(validation, "status")));
        foreach (var value in GetList(validation, "missingFields")) AddRow("Заполнить", Convert.ToString(value) ?? string.Empty);
        foreach (var value in GetList(validation, "errors")) AddRow("Ошибка", Convert.ToString(value) ?? string.Empty);
        foreach (var value in GetList(validation, "warnings")) AddRow("Предупреждение", Convert.ToString(value) ?? string.Empty);
    }

    private void AddRow(string kind, string text)
    {
        if (!string.IsNullOrWhiteSpace(text)) ValidationRows.Add($"{kind}: {text}");
    }

    private bool EnsureSelectedDraft()
    {
        if (!string.IsNullOrWhiteSpace(SelectedDraft?.Id)) return true;
        ErrorMessage = "Выберите или сохраните черновик предложения.";
        return false;
    }

    private void RunProposalAction(Action action)
    {
        try
        {
            ErrorMessage = string.Empty;
            action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void LoadDefaultFields(string proposalType)
    {
        Fields.Clear();
        foreach (var field in FieldsFor(proposalType))
            Fields.Add(field);
    }

    private void AddBuiltInTemplatesFallback()
    {
        if (Templates.Count > 0) return;
        Templates.Add(new PlayerProposalTemplateVm { ProposalType = "research", Name = "Предложить исследование", Description = "Тема, вопрос и ожидаемый результат." });
        Templates.Add(new PlayerProposalTemplateVm { ProposalType = "crafting", Name = "Предложить крафт", Description = "Предмет, материалы и назначение." });
        Templates.Add(new PlayerProposalTemplateVm { ProposalType = "engineering_design", Name = "Инженерный проект", Description = "Роль, платформа и нужные возможности." });
        Templates.Add(new PlayerProposalTemplateVm { ProposalType = "factory_order", Name = "Заказ производства", Description = "Blueprint/preset, количество и сроки." });
        Templates.Add(new PlayerProposalTemplateVm { ProposalType = "manufacturing", Name = "Производство", Description = "Приёмка, прогресс или передача asset." });
        Templates.Add(new PlayerProposalTemplateVm { ProposalType = "legal_check", Name = "Юридическая проверка", Description = "Действие, объект и юрисдикция." });
        Templates.Add(new PlayerProposalTemplateVm { ProposalType = "license_application", Name = "Заявка на лицензию", Description = "Лицензия, юрисдикция и причина." });
        Templates.Add(new PlayerProposalTemplateVm { ProposalType = "development_purchase", Name = "Покупка развития", Description = "Персонаж и узел развития." });
        Templates.Add(new PlayerProposalTemplateVm { ProposalType = "custom_project", Name = "Свободное предложение", Description = "Структурированная заявка GM." });
    }

    private static IEnumerable<PlayerProposalFieldVm> FieldsFor(string proposalType)
    {
        return (proposalType ?? string.Empty).ToLowerInvariant() switch
        {
            "research" => new[]
            {
                Field("researchTopic", "Тема исследования", true, "Что изучаем"),
                Field("researchQuestion", "Главный вопрос", true, "Какой ответ нужен"),
                Field("desiredResultType", "Ожидаемый результат", false, "Знание, технология, рецепт..."),
                Field("suggestedApproach", "Предложенный подход", false, "Как персонаж предлагает исследовать")
            },
            "crafting" => new[]
            {
                Field("desiredResultTitle", "Что создать", true, "Название предмета или результата"),
                Field("recipeId", "Рецепт", false, "RecipeId, если известен"),
                Field("quantity", "Количество", false, "Например: 1"),
                Field("suggestedMaterials", "Материалы", false, "Что игрок готов вложить"),
                Field("intendedUse", "Назначение", false, "Для чего нужен результат")
            },
            "engineering_design" => new[]
            {
                Field("intendedRoleSummary", "Роль конструкции", true, "Разведка, груз, бой, поддержка..."),
                Field("platformId", "Платформа", false, "PlatformId, если известен"),
                Field("selectedModuleIds", "Модули", false, "Через запятую"),
                Field("desiredCapabilities", "Желаемые возможности", false, "Что конструкция должна уметь")
            },
            "factory_quote" or "factory_order" => new[]
            {
                Field("sourceBlueprintId", "Blueprint", false, "BlueprintId, если есть"),
                Field("sourcePresetDesignId", "Preset", false, "PresetDesignId, если есть"),
                Field("quantity", "Количество", true, "Сколько произвести"),
                Field("desiredQuality", "Качество", false, "Минимальный уровень"),
                Field("deliveryTargetSummary", "Срок / доставка", false, "Когда и куда")
            },
            "manufacturing" => new[]
            {
                Field("requestKind", "Тип запроса", true, "progress / acceptance / transfer"),
                Field("factoryOrderId", "FactoryOrderId", false, "Если запрос связан с заказом"),
                Field("manufacturingProjectId", "ManufacturingProjectId", false, "Если запрос связан с проектом"),
                Field("resourceProposal", "Ресурсы", false, "Что предлагает игрок")
            },
            "legal_check" => new[]
            {
                Field("actionType", "Действие", true, "own / sell / craft / move..."),
                Field("objectEntityType", "Тип объекта", true, "item / vehicle / license..."),
                Field("objectEntityId", "Id объекта", false, "Если известен"),
                Field("jurisdictionId", "Юрисдикция", false, "Где проверять")
            },
            "license_application" => new[]
            {
                Field("licenseDefinitionId", "Лицензия", true, "Какую лицензию запросить"),
                Field("jurisdictionId", "Юрисдикция", false, "Где действует"),
                Field("applicationReason", "Причина", true, "Зачем нужна лицензия")
            },
            "development_purchase" => new[]
            {
                Field("characterId", "CharacterId", true, "Обычно активный персонаж"),
                Field("developmentNodeId", "Узел развития", true, "NodeId"),
                Field("playerComment", "Комментарий", false, "Почему это развитие логично")
            },
            _ => new[]
            {
                Field("goal", "Цель", true, "Что нужно получить"),
                Field("context", "Контекст", false, "Почему это важно"),
                Field("expectedOutcome", "Ожидаемый результат", false, "Как понять, что задача выполнена")
            }
        };
    }

    private static PlayerProposalFieldVm Field(string key, string label, bool required, string hint)
        => new() { Key = key, Label = label, IsRequired = required, Hint = hint };
    private static PlayerProposalDraftVm ToDraftVm(IDictionary<string, object> map)
        => new()
        {
            Id = FirstNonEmpty(S(map, "proposalDraftId"), S(map, "id")),
            Title = S(map, "title"),
            ProposalType = S(map, "proposalType"),
            TypeLabel = S(map, "proposalTypeLabel"),
            Status = S(map, "status"),
            StatusLabel = S(map, "statusLabel"),
            Summary = FirstNonEmpty(S(map, "summary"), S(map, "publicSummary"), S(map, "description")),
            UpdatedAt = S(map, "updatedAtUtc")
        };

    private static bool IsOk(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;
    private static string DisabledOrError(ResponseEnvelope response, string fallback)
    {
        var message = response.Message;
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        if (message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0)
            return fallback;
        return message;
    }
    private static IEnumerable<object> GetItems(ResponseEnvelope response) => GetList(response.Payload, "items");
    private static object GetValue(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : new Dictionary<string, object>();
    private static List<object> GetList(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? ToList(value) : new List<object>();
    private static List<object> ToList(object value) => value is IEnumerable enumerable && value is not string ? enumerable.Cast<object>().ToList() : new List<object>();
    private static Dictionary<string, object> AsMap(object value)
        => value is Dictionary<string, object> dictionary
            ? dictionary
            : value is IDictionary<string, object> map
                ? new Dictionary<string, object>(map, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    private static string S(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string DefaultTitle(string proposalType) => FieldsFor(proposalType).FirstOrDefault()?.Label ?? "Предложение GM";
}

