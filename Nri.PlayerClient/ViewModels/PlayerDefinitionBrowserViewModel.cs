using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerDefinitionBrowserViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _searchText = string.Empty;
    private string _statusMessage = "Справочник ещё не загружен.";
    private string _selectedCategory = "Все";
    private string _selectedFamily = "Все";
    private PlayerDefinitionRow? _selectedDefinition;
    private PlayerDefinitionDetailVm? _selectedDetail;
    private bool _isLoading;
    private bool _hasLoadError;
    private string _summaryText = "Доступны только открытые для игроков записи.";

    public PlayerDefinitionBrowserViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
        SearchCommand = new RelayCommand(Search);
    }

    public ObservableCollection<PlayerDefinitionRow> Definitions { get; } = new();
    public ObservableCollection<PlayerDefinitionRow> FilteredDefinitions { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<PlayerDefinitionFilterOption> CategoryOptions { get; } = new();
    public ObservableCollection<string> FamilyFilters { get; } = new();
    public ObservableCollection<PlayerDefinitionFactVm> DetailFacts { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }

    public string SearchText { get => _searchText; set { if (_searchText != value) { _searchText = value; Notify(); ApplyFilter(); } } }
    public string SelectedCategory { get => _selectedCategory; set { if (_selectedCategory != value) { _selectedCategory = value; Notify(); ApplyFilter(); } } }
    public string SelectedFamily { get => _selectedFamily; set { if (_selectedFamily != value) { _selectedFamily = value; Notify(); ApplyFilter(); } } }
    public PlayerDefinitionDetailVm? SelectedDetail { get => _selectedDetail; private set { if (_selectedDetail != value) { _selectedDetail = value; Notify(); NotifyPlayerState(); } } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); NotifyPlayerState(); } } }
    public bool HasLoadError { get => _hasLoadError; private set { if (_hasLoadError != value) { _hasLoadError = value; Notify(); NotifyPlayerState(); } } }
    public bool HasResults => FilteredDefinitions.Count > 0;
    public bool HasDefinitions => Definitions.Count > 0;
    public bool HasSelectedDetail => SelectedDetail != null;
    public bool HasNoSelectedDetail => SelectedDetail == null;
    public bool IsEmptyState => !IsLoading && !HasLoadError && !HasDefinitions;
    public bool IsNoResultsState => !IsLoading && !HasLoadError && HasDefinitions && !HasResults;
    public bool IsUnavailableState => !IsLoading && HasLoadError;

    private void NotifyPlayerState()
    {
        Notify(nameof(HasResults));
        Notify(nameof(HasDefinitions));
        Notify(nameof(HasSelectedDetail));
        Notify(nameof(HasNoSelectedDetail));
        Notify(nameof(IsEmptyState));
        Notify(nameof(IsNoResultsState));
        Notify(nameof(IsUnavailableState));
    }

    public PlayerDefinitionRow? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (_selectedDefinition == value) return;
            _selectedDefinition = value;
            Notify();
            LoadDetails();
        }
    }

    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string SummaryText { get => _summaryText; private set { if (_summaryText != value) { _summaryText = value; Notify(); } } }

    public void Refresh()
    {
        ClientLogService.Instance.Info("player.definitionBrowser.load.start");
        IsLoading = true;
        HasLoadError = false;
        Definitions.Clear();
        var contentResponse = _api.ContentDefinitionPlayerListVisible();
        if (contentResponse.Status == ResponseStatus.Ok)
        {
            foreach (var item in ReadArray(contentResponse.Payload, "definitions"))
            {
                var map = AsMap(item);
                var category = Get(map, "category");
                if (CoreEquipmentDefinitionFamilies.IsSupported(category)
                    || MagicEffectConditionDefinitionFamilies.IsSupported(category)
                    || WorldLoreCalendarDefinitionCategories.IsSupported(category)
                    || FactionOrganizationEconomyDefinitionCategories.IsSupported(category)
                    || TechnologyRecipeBlueprintProjectDefinitionCategories.IsSupported(category))
                    continue;
                Definitions.Add(new PlayerDefinitionRow(map));
            }
        }

        var equipmentResponse = _api.CoreEquipmentPlayerList(new Dictionary<string, object>());
        if (equipmentResponse.Status == ResponseStatus.Ok)
        {
            foreach (var item in ReadArray(equipmentResponse.Payload, "items"))
            {
                Definitions.Add(new PlayerDefinitionRow(AsMap(item), isCoreEquipment: true));
            }
        }

        var magicResponse = _api.MagicDefinitionsPlayerList(new Dictionary<string, object>());
        if (magicResponse.Status == ResponseStatus.Ok)
        {
            foreach (var item in ReadArray(magicResponse.Payload, "items"))
            {
                Definitions.Add(new PlayerDefinitionRow(AsMap(item), isMagicDefinition: true));
            }
        }

        var worldLoreResponse = _api.WorldLoreCalendarPlayerList(new Dictionary<string, object>());
        if (worldLoreResponse.Status == ResponseStatus.Ok)
        {
            foreach (var item in ReadArray(worldLoreResponse.Payload, "items"))
            {
                Definitions.Add(new PlayerDefinitionRow(AsMap(item), isWorldLoreDefinition: true));
            }
        }

        var factionEconomyResponse = _api.FactionOrganizationEconomyPlayerList(new Dictionary<string, object>());
        if (factionEconomyResponse.Status == ResponseStatus.Ok)
        {
            foreach (var item in ReadArray(factionEconomyResponse.Payload, "items"))
            {
                Definitions.Add(new PlayerDefinitionRow(AsMap(item), isFactionEconomyDefinition: true));
            }
        }

        var technologyResponse = _api.TechnologyRecipeBlueprintProjectPlayerList(new Dictionary<string, object>());
        if (technologyResponse.Status == ResponseStatus.Ok)
        {
            foreach (var item in ReadArray(technologyResponse.Payload, "items"))
            {
                Definitions.Add(new PlayerDefinitionRow(AsMap(item), isTechnologyDefinition: true));
            }
        }

        if (contentResponse.Status != ResponseStatus.Ok
            && equipmentResponse.Status != ResponseStatus.Ok
            && magicResponse.Status != ResponseStatus.Ok
            && worldLoreResponse.Status != ResponseStatus.Ok
            && factionEconomyResponse.Status != ResponseStatus.Ok
            && technologyResponse.Status != ResponseStatus.Ok)
        {
            StatusMessage = "Не удалось загрузить справочник.";
            HasLoadError = true;
            IsLoading = false;
            NotifyPlayerState();
            return;
        }

        RebuildCategories();
        ApplyFilter();
        StatusMessage = $"Видимых записей: {Definitions.Count}.";
        SummaryText = "Выберите запись, чтобы открыть общедоступное описание.";
        ClientLogService.Instance.Info($"player.definitionBrowser.load.done count={Definitions.Count}");
        IsLoading = false;
        NotifyPlayerState();
    }

    private void Search()
    {
        ApplyFilter();
        StatusMessage = FilteredDefinitions.Count == 0
            ? "Ничего не найдено."
            : $"Найдено: {FilteredDefinitions.Count}.";
    }

    private void ApplyFilter()
    {
        FilteredDefinitions.Clear();
        var query = SearchText ?? string.Empty;
        foreach (var item in Definitions.Where(x =>
                     (SelectedCategory == "Все" || string.Equals(x.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase)) &&
                     FamilyMatches(x.Category) &&
                     (string.IsNullOrWhiteSpace(query) || Contains(x.DisplayName, query) || Contains(x.PublicDescription, query) || Contains(x.CategoryLabel, query) || Contains(x.Tags, query))))
        {
            FilteredDefinitions.Add(item);
        }
        if (_selectedDefinition != null && !FilteredDefinitions.Contains(_selectedDefinition))
        {
            SelectedDefinition = null;
        }
        if (FilteredDefinitions.Count == 0 && Definitions.Count > 0) SummaryText = "По текущим фильтрам ничего не найдено.";
    }

    private void RebuildCategories()
    {
        Categories.Clear();
        CategoryOptions.Clear();
        FamilyFilters.Clear();
        CategoryOptions.Add(new PlayerDefinitionFilterOption("Все", "Все"));
        foreach (var family in new[] { "Все", "Мир, языки и знания", "Фракции, организации и рынки", "Технологии, рецепты и чертежи", "Магия и состояния", "Снаряжение", "Расы", "Характеристики", "Навыки", "Развитие" }) FamilyFilters.Add(family);
        Categories.Add("Все");
        foreach (var category in Definitions.Select(x => x.Category).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
        {
            Categories.Add(category);
            CategoryOptions.Add(new PlayerDefinitionFilterOption(category, DefinitionLabels.Category(category)));
        }
        if (!Categories.Contains(SelectedCategory)) SelectedCategory = "Все";
        if (!FamilyFilters.Contains(SelectedFamily)) SelectedFamily = "Все";
    }

    private void LoadDetails()
    {
        if (SelectedDefinition == null)
        {
            SelectedDetail = null;
            DetailFacts.Clear();
            SummaryText = "Справочник работает только на чтение.";
            return;
        }
        if (SelectedDefinition.IsCoreEquipment || SelectedDefinition.IsMagicDefinition || SelectedDefinition.IsWorldLoreDefinition || SelectedDefinition.IsFactionEconomyDefinition || SelectedDefinition.IsTechnologyDefinition)
        {
            SelectedDetail = PlayerDefinitionDetailVm.From(SelectedDefinition.EmbeddedDetail, SelectedDefinition);
            DetailFacts.Clear();
            foreach (var fact in SelectedDetail.Facts) DetailFacts.Add(fact);
            SummaryText = SelectedDefinition.PublicDescription;
            StatusMessage = SelectedDefinition.IsTechnologyDefinition
                ? "Публичная карточка технологии, рецепта или чертежа открыта."
                : SelectedDefinition.IsFactionEconomyDefinition
                ? "Публичная карточка фракции или экономики открыта."
                : SelectedDefinition.IsWorldLoreDefinition
                ? "Запись о мире открыта."
                : SelectedDefinition.IsMagicDefinition
                    ? "Карточка магии или состояния открыта."
                    : "Карточка снаряжения открыта.";
            return;
        }

        var response = _api.ContentDefinitionPlayerGetVisible(new Dictionary<string, object> { ["definitionId"] = SelectedDefinition.DefinitionId });
        if (response.Status != ResponseStatus.Ok)
        {
            SelectedDetail = null;
            DetailFacts.Clear();
            StatusMessage = "Не удалось открыть запись.";
            return;
        }
        var map = AsMap(response.Payload.TryGetValue("definition", out var definition) ? definition : null);
        SelectedDetail = PlayerDefinitionDetailVm.From(map, SelectedDefinition);
        DetailFacts.Clear();
        foreach (var fact in SelectedDetail.Facts) DetailFacts.Add(fact);
        SummaryText = SelectedDefinition.PublicDescription;
        StatusMessage = "Запись открыта.";
    }

    private bool FamilyMatches(string category)
    {
        if (SelectedFamily == "Все") return true;
        if (SelectedFamily == "Мир, языки и знания") return WorldLoreCalendarDefinitionCategories.IsSupported(category);
        if (SelectedFamily == "Фракции, организации и рынки") return FactionOrganizationEconomyDefinitionCategories.IsSupported(category);
        if (SelectedFamily == "Технологии, рецепты и чертежи") return TechnologyRecipeBlueprintProjectDefinitionCategories.IsSupported(category);
        if (SelectedFamily == "Магия и состояния") return MagicEffectConditionDefinitionFamilies.IsSupported(category);
        if (SelectedFamily == "Снаряжение") return CoreEquipmentDefinitionFamilies.IsSupported(category);
        if (SelectedFamily == "Расы") return IsIn(category, "race_definition", "subspecies_definition", "hybrid_definition", "hybrid_subtype_definition", "race_trait_definition");
        if (SelectedFamily == "Характеристики") return IsIn(category, "attribute_definition", "subattribute_definition", "derived_stat_definition");
        if (SelectedFamily == "Навыки") return IsIn(category, "skill_definition", "skill_group_definition");
        if (SelectedFamily == "Развитие") return IsIn(category, "development_node_definition", "development_direction_definition");
        return true;
    }

    private static bool Contains(string value, string query) => !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    private static bool IsIn(string value, params string[] values) => values.Contains(value, StringComparer.OrdinalIgnoreCase);
    private static string Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static object[] ReadArray(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return Array.Empty<object>();
        if (value is object[] array) return array;
        if (value is IEnumerable enumerable && value is not string) return enumerable.Cast<object>().ToArray();
        return Array.Empty<object>();
    }
    private static string Join(IEnumerable<object> values) => string.Join(", ", values.Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)));

    private static Dictionary<string, object> AsMap(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }
        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                if (!TryReadPair(item, out var key, out var mappedValue)) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = mappedValue ?? string.Empty;
            }
        }
        return result;
    }

    private static bool TryReadPair(object? value, out string key, out object? mappedValue)
    {
        key = string.Empty;
        mappedValue = null;
        if (value is DictionaryEntry entry)
        {
            key = Convert.ToString(entry.Key) ?? string.Empty;
            mappedValue = entry.Value;
            return !string.IsNullOrWhiteSpace(key);
        }
        if (value is IDictionary pair)
        {
            object? keyValue = null;
            object? contentValue = null;
            var hasKey = false;
            var hasValue = false;
            foreach (DictionaryEntry pairEntry in pair)
            {
                var pairKey = Convert.ToString(pairEntry.Key);
                if (string.Equals(pairKey, "Key", StringComparison.OrdinalIgnoreCase)) { keyValue = pairEntry.Value; hasKey = true; }
                else if (string.Equals(pairKey, "Value", StringComparison.OrdinalIgnoreCase)) { contentValue = pairEntry.Value; hasValue = true; }
            }
            if (hasKey && hasValue)
            {
                key = Convert.ToString(keyValue) ?? string.Empty;
                mappedValue = contentValue;
                return !string.IsNullOrWhiteSpace(key);
            }
        }
        return false;
    }
}

public sealed class PlayerDefinitionFilterOption
{
    public PlayerDefinitionFilterOption(string id, string label) { Id = id; Label = label; }
    public string Id { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

public sealed class PlayerDefinitionDetailVm
{
    public string DisplayName { get; set; } = string.Empty;
    public string CategoryLabel { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public ObservableCollection<PlayerDefinitionFactVm> Facts { get; } = new();

    public static PlayerDefinitionDetailVm From(IDictionary<string, object> map, PlayerDefinitionRow row)
    {
        var detail = new PlayerDefinitionDetailVm
        {
            DisplayName = FirstNonEmpty(Get(map, "displayName"), row.DisplayName),
            CategoryLabel = FirstNonEmpty(Get(map, "categoryLabel"), DefinitionLabels.Category(Get(map, "category"))),
            PublicDescription = Get(map, "publicDescription")
        };
        var playerFacts = ReadArray(map, "playerFacts");
        foreach (var item in playerFacts)
        {
            var fact = AsMap(item);
            var label = Get(fact, "label");
            var factValue = Get(fact, "value");
            if (!string.IsNullOrWhiteSpace(label)) detail.Facts.Add(new PlayerDefinitionFactVm(label, factValue));
        }

        foreach (var item in ReadArray(map, "attackProfiles"))
        {
            var profile = AsMap(item);
            var name = FirstNonEmpty(Get(profile, "name"), "Атака");
            var summary = string.Join("; ", new[]
            {
                TextPart("навык", Get(profile, "skill")),
                TextPart("урон", Get(profile, "damageExpression")),
                TextPart("тип", Join(ReadArray(profile, "damageTypes"))),
                TextPart("дальность", Get(profile, "range")),
                TextPart("боеприпасы", Get(profile, "ammoCost"))
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
            detail.Facts.Add(new PlayerDefinitionFactVm($"Атака: {name}", summary));
        }

        AddReadableList(detail, map, "compatibleAmmo", "Совместимые боеприпасы");
        AddReadableList(detail, map, "allowedWeapons", "Совместимое оружие");

        return detail;
    }

    private static void AddReadableList(PlayerDefinitionDetailVm detail, IDictionary<string, object> map, string key, string label)
    {
        var value = Join(ReadArray(map, key));
        if (!string.IsNullOrWhiteSpace(value)) detail.Facts.Add(new PlayerDefinitionFactVm(label, value));
    }

    private static string TextPart(string label, string value)
        => string.IsNullOrWhiteSpace(value) || value == "0" ? string.Empty : $"{label}: {value}";

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static object[] ReadArray(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return Array.Empty<object>();
        if (value is object[] array) return array;
        if (value is IEnumerable enumerable && value is not string) return enumerable.Cast<object>().ToArray();
        return Array.Empty<object>();
    }
    private static Dictionary<string, object> AsMap(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                result[Convert.ToString(entry.Key) ?? string.Empty] = entry.Value ?? string.Empty;
            return result;
        }
        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                if (!TryReadPair(item, out var key, out var mappedValue))
                    return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(key))
                    result[key] = mappedValue ?? string.Empty;
            }
        }
        return result;
    }

    private static bool TryReadPair(object? value, out string key, out object? mappedValue)
    {
        key = string.Empty;
        mappedValue = null;
        if (value is DictionaryEntry entry)
        {
            key = Convert.ToString(entry.Key) ?? string.Empty;
            mappedValue = entry.Value;
            return !string.IsNullOrWhiteSpace(key);
        }
        if (value is IDictionary pair)
        {
            object? keyValue = null;
            object? contentValue = null;
            var hasKey = false;
            var hasValue = false;
            foreach (DictionaryEntry pairEntry in pair)
            {
                var pairKey = Convert.ToString(pairEntry.Key);
                if (string.Equals(pairKey, "Key", StringComparison.OrdinalIgnoreCase))
                {
                    keyValue = pairEntry.Value;
                    hasKey = true;
                }
                else if (string.Equals(pairKey, "Value", StringComparison.OrdinalIgnoreCase))
                {
                    contentValue = pairEntry.Value;
                    hasValue = true;
                }
            }
            if (hasKey && hasValue)
            {
                key = Convert.ToString(keyValue) ?? string.Empty;
                mappedValue = contentValue;
                return !string.IsNullOrWhiteSpace(key);
            }
        }
        return false;
    }
    private static string Join(IEnumerable<object> values) => string.Join(", ", values.Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)));
    private static string SafeValue(object value) => value is IEnumerable enumerable && value is not string
        ? string.Join(", ", enumerable.Cast<object>().Select(x => Convert.ToString(x)))
        : Convert.ToString(value) ?? string.Empty;
}

public sealed class PlayerDefinitionFactVm
{
    public PlayerDefinitionFactVm(string label, string value) { Label = label; Value = value; }
    public string Label { get; }
    public string Value { get; }
}

public sealed class PlayerDefinitionRow
{
    public PlayerDefinitionRow(
        Dictionary<string, object> map,
        bool isCoreEquipment = false,
        bool isMagicDefinition = false,
        bool isWorldLoreDefinition = false,
        bool isFactionEconomyDefinition = false,
        bool isTechnologyDefinition = false)
    {
        IsCoreEquipment = isCoreEquipment;
        IsMagicDefinition = isMagicDefinition;
        IsWorldLoreDefinition = isWorldLoreDefinition;
        IsFactionEconomyDefinition = isFactionEconomyDefinition;
        IsTechnologyDefinition = isTechnologyDefinition;
        EmbeddedDetail = new Dictionary<string, object>(map, StringComparer.OrdinalIgnoreCase);
        DefinitionId = isCoreEquipment || isMagicDefinition || isWorldLoreDefinition || isFactionEconomyDefinition || isTechnologyDefinition ? string.Empty : Get(map, "definitionId");
        Category = FirstNonEmpty(Get(map, "family"), Get(map, "category"));
        Name = Get(map, "name");
        DisplayName = FirstNonEmpty(Get(map, "displayName"), Get(map, "name"), "Без названия");
        ShortCode = Get(map, "shortCode");
        PublicDescription = Get(map, "publicDescription");
        Tags = Join(ReadArray(map, "tags"));
    }

    public string DefinitionId { get; }
    public bool IsCoreEquipment { get; }
    public bool IsMagicDefinition { get; }
    public bool IsWorldLoreDefinition { get; }
    public bool IsFactionEconomyDefinition { get; }
    public bool IsTechnologyDefinition { get; }
    public Dictionary<string, object> EmbeddedDetail { get; }
    public string Category { get; }
    public string CategoryLabel => DefinitionLabels.Category(Category);
    public string Name { get; }
    public string DisplayName { get; }
    public string ShortCode { get; }
    public string PublicDescription { get; }
    public string Tags { get; }
    public override string ToString() => DisplayName;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static object[] ReadArray(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return Array.Empty<object>();
        if (value is object[] array) return array;
        if (value is IEnumerable enumerable && value is not string) return enumerable.Cast<object>().ToArray();
        return Array.Empty<object>();
    }
    private static string Join(IEnumerable<object> values) => string.Join(", ", values.Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)));
}

internal static class DefinitionLabels
{
    private static readonly Dictionary<string, string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["race_definition"] = "Раса",
        ["subspecies_definition"] = "Подвид",
        ["hybrid_definition"] = "Гибрид",
        ["hybrid_subtype_definition"] = "Подтип гибрида",
        ["race_trait_definition"] = "Расовая особенность",
        ["attribute_definition"] = "Характеристика",
        ["subattribute_definition"] = "Подхарактеристика",
        ["derived_stat_definition"] = "Производный параметр",
        ["skill_definition"] = "Навык",
        ["skill_group_definition"] = "Группа навыков",
        ["development_node_definition"] = "Узел развития",
        ["development_direction_definition"] = "Направление развития",
        [DefinitionCategoryIds.Resource] = "Ресурс",
        [DefinitionCategoryIds.Item] = "Предмет",
        [DefinitionCategoryIds.DamageType] = "Тип урона",
        [DefinitionCategoryIds.Weapon] = "Оружие",
        [DefinitionCategoryIds.Ammo] = "Боеприпасы",
        [DefinitionCategoryIds.Armor] = "Броня или щит",
        [DefinitionCategoryIds.MagicMethod] = "Магический метод",
        [DefinitionCategoryIds.MagicDirection] = "Направление магии",
        [DefinitionCategoryIds.Spell] = "Заклинание",
        [DefinitionCategoryIds.Seal] = "Печать",
        [DefinitionCategoryIds.ArcanaForm] = "Форма Арканы",
        [DefinitionCategoryIds.Ritual] = "Ритуал",
        [DefinitionCategoryIds.Effect] = "Эффект",
        [DefinitionCategoryIds.Condition] = "Состояние",
        [WorldLoreCalendarDefinitionCategories.World] = "Мир",
        [WorldLoreCalendarDefinitionCategories.Location] = "Локация",
        [WorldLoreCalendarDefinitionCategories.Language] = "Язык",
        [WorldLoreCalendarDefinitionCategories.KnowledgeType] = "Тип знания",
        [WorldLoreCalendarDefinitionCategories.LoreEntry] = "Знание о мире",
        [WorldLoreCalendarDefinitionCategories.Calendar] = "Календарь",
        [WorldLoreCalendarDefinitionCategories.Era] = "Эпоха",
        [WorldLoreCalendarDefinitionCategories.EventType] = "Тип события",
        [FactionOrganizationEconomyDefinitionCategories.Faction] = "Фракция",
        [FactionOrganizationEconomyDefinitionCategories.Organization] = "Организация",
        [FactionOrganizationEconomyDefinitionCategories.Jurisdiction] = "Юрисдикция",
        [FactionOrganizationEconomyDefinitionCategories.Law] = "Закон",
        [FactionOrganizationEconomyDefinitionCategories.License] = "Лицензия",
        [FactionOrganizationEconomyDefinitionCategories.Currency] = "Валюта",
        [FactionOrganizationEconomyDefinitionCategories.Market] = "Рынок",
        [FactionOrganizationEconomyDefinitionCategories.BusinessProfile] = "Экономический профиль",
        [FactionOrganizationEconomyDefinitionCategories.ControlLevel] = "Уровень контроля",
        [FactionOrganizationEconomyDefinitionCategories.EconomicScale] = "Экономический масштаб",
        [FactionOrganizationEconomyDefinitionCategories.MarketOfferKind] = "Вид предложения",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.Technology] = "Технология",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.ProductionMethod] = "Метод производства",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.Recipe] = "Рецепт",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint] = "Канонический чертёж",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.Facility] = "Тип площадки",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.ProjectTemplate] = "Шаблон проекта",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.TestProtocol] = "Протокол испытаний",
        [TechnologyRecipeBlueprintProjectDefinitionCategories.Defect] = "Тип дефекта"
    };

    private static readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["description"] = "Описание",
        ["publicDescription"] = "Описание",
        ["rulesText"] = "Правила",
        ["category"] = "Категория",
        ["group"] = "Группа",
        ["cost"] = "Стоимость",
        ["level"] = "Уровень",
        ["requirements"] = "Требования",
        ["rewards"] = "Награды"
    };

    public static string Category(string value) => Categories.TryGetValue(value ?? string.Empty, out var label) ? label : value;
    public static string Field(string value)
    {
        if (Fields.TryGetValue(value ?? string.Empty, out var label)) return label;
        return Regex.Replace(value ?? string.Empty, "([a-z])([A-Z])", "$1 $2").Replace("_", " ");
    }
}
