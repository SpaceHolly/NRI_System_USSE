using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Nri.AdminClient.ViewModels;

public sealed class CharacterCardViewModel : ViewModelBase
{
    private static readonly (string Id, string Title)[] EquipmentSlots =
    {
        ("head", "Голова"),
        ("torso", "Торс"),
        ("hands", "Руки"),
        ("main_hand", "Основная рука"),
        ("off_hand", "Вторая рука"),
        ("two_handed", "Двуручное"),
        ("legs", "Ноги"),
        ("feet", "Ступни"),
        ("back", "Спина"),
        ("belt", "Пояс"),
        ("accessory", "Аксессуары"),
        ("backpack", "Рюкзак")
    };

    private Action? _loadAction;
    private Action? _inventoryDiagnosticsAction;
    private Dictionary<string, object>? _lastPayload;
    private bool _hideDescription;
    private bool _hideBackstory;
    private bool _hideStats;
    private bool _hideReputation;
    private string _mode = "gm";
    private string _errorMessage = string.Empty;
    private string _warningMessage = "Данные отсутствуют. Выберите персонажа и нажмите \"Открыть\".";

    public CharacterCardViewModel()
    {
        LoadCharacterCommand = new RelayCommand(() => _loadAction?.Invoke());
        RefreshCommand = new RelayCommand(() => _loadAction?.Invoke());
        RunInventoryDiagnosticsCommand = new RelayCommand(() => _inventoryDiagnosticsAction?.Invoke());
        SwitchToGmEditorModeCommand = new RelayCommand(() => SetMode("gm"));
        SwitchToPlayerPreviewModeCommand = new RelayCommand(() => SetMode("player"));
        ClearErrorCommand = new RelayCommand(() =>
        {
            ErrorMessage = string.Empty;
            WarningMessage = string.Empty;
        });
    }

    public string CharacterId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = "Персонаж не выбран";
    public string PlayerOwner { get; private set; } = "Владелец не выбран";
    public string RaceSummary { get; private set; } = "Раса не указана";
    public string ClassSummary { get; private set; } = "Развитие не загружено";
    public string SelectedTitleDisplay { get; private set; } = "Без титула";
    public string PortraitPlaceholderText { get; } = "Портрет персонажа";
    public string UserAvatarPlaceholderText { get; } = "Аватар пользователя";
    public string CombatTokenPlaceholderText { get; } = "Иконка боя";
    public string MapTokenPlaceholderText { get; } = "Токен карты";
    public string ImageUploadHint { get; } = "Загрузка изображений будет добавлена ближе к 1.0-minimal.";
    public string Mode { get => _mode; private set { _mode = value; Notify(); Notify(nameof(IsGmEditorMode)); Notify(nameof(IsPlayerPreviewMode)); Notify(nameof(ModeTitle)); Notify(nameof(ShowGmInventoryDetails)); } }
    public bool IsGmEditorMode => string.Equals(Mode, "gm", StringComparison.OrdinalIgnoreCase);
    public bool IsPlayerPreviewMode => string.Equals(Mode, "player", StringComparison.OrdinalIgnoreCase);
    public bool ShowGmInventoryDetails => IsGmEditorMode;
    public string ModeTitle => IsPlayerPreviewMode ? "Вид игрока" : "Редактор GM";
    public string ErrorMessage { get => _errorMessage; set { _errorMessage = value; Notify(); } }
    public string WarningMessage { get => _warningMessage; set { _warningMessage = value; Notify(); } }
    public DateTime LastLoadedAtUtc { get; private set; }
    public string LastLoadedText => LastLoadedAtUtc == default ? "Ещё не загружено" : LastLoadedAtUtc.ToString("u");
    public string BiographyText { get; private set; } = "Данные отсутствуют";
    public string NotesText { get; private set; } = "GM-заметки недоступны в этом режиме";
    public string CombatSummaryText { get; private set; } = "Боевой статус будет связан с Combat MVP позже";
    public string ConditionsSummaryText { get; private set; } = "Состояния не загружены";
    public string InventoryDiagnosticsStatus { get; private set; } = "Диагностика инвентаря ещё не запускалась.";
    public int InventoryDiagnosticsErrorCount { get; private set; }
    public int InventoryDiagnosticsWarningCount { get; private set; }

    public ObservableCollection<RowVm> Attributes { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> DerivedStats { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> Skills { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> Classes { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<InventoryCardItemVm> InventoryItems { get; } = new ObservableCollection<InventoryCardItemVm>();
    public ObservableCollection<EquipmentSlotCardVm> EquipmentSlotsSummary { get; } = new ObservableCollection<EquipmentSlotCardVm>();
    public ObservableCollection<RowVm> Currencies { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> Conditions { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> Languages { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> ReputationItems { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> HoldingsItems { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<InventoryDiagnosticsIssueVm> InventoryDiagnosticsIssues { get; } = new ObservableCollection<InventoryDiagnosticsIssueVm>();
    public ObservableCollection<RowVm> InventoryDiagnosticsSections { get; } = new ObservableCollection<RowVm>();

    public ICommand LoadCharacterCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand RunInventoryDiagnosticsCommand { get; }
    public ICommand SwitchToGmEditorModeCommand { get; }
    public ICommand SwitchToPlayerPreviewModeCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public void SetLoadAction(Action loadAction) => _loadAction = loadAction;
    public void SetInventoryDiagnosticsAction(Action diagnosticsAction) => _inventoryDiagnosticsAction = diagnosticsAction;

    public void LoadFromDetails(Dictionary<string, object> payload, string characterId, string owner, bool hideDescription, bool hideBackstory, bool hideStats, bool hideReputation)
    {
        _lastPayload = payload;
        CharacterId = characterId;
        PlayerOwner = string.IsNullOrWhiteSpace(owner) ? "Владелец не выбран" : owner;
        _hideDescription = hideDescription;
        _hideBackstory = hideBackstory;
        _hideStats = hideStats;
        _hideReputation = hideReputation;
        LastLoadedAtUtc = DateTime.UtcNow;
        RebuildFromPayload();
    }

    public void MarkError(string message)
    {
        ErrorMessage = message;
        WarningMessage = string.Empty;
    }

    public void ApplyInventoryDiagnostics(Dictionary<string, object> payload, string message)
    {
        InventoryDiagnosticsIssues.Clear();
        InventoryDiagnosticsSections.Clear();

        var summary = payload.TryGetValue("summary", out var rawSummary) ? AsMap(rawSummary) : null;
        InventoryDiagnosticsErrorCount = ParseInt(summary == null ? string.Empty : Str(summary, "errorCount"));
        InventoryDiagnosticsWarningCount = ParseInt(summary == null ? string.Empty : Str(summary, "warningCount"));
        InventoryDiagnosticsStatus = FirstNonEmpty(message, $"Ошибки: {InventoryDiagnosticsErrorCount}, предупреждения: {InventoryDiagnosticsWarningCount}");

        foreach (var section in ToList(payload.TryGetValue("sections", out var rawSections) ? rawSections : new ArrayList()))
        {
            var map = AsMap(section);
            if (map == null) continue;
            var errors = ToList(map.TryGetValue("errors", out var rawErrors) ? rawErrors : new ArrayList()).Count;
            var warnings = ToList(map.TryGetValue("warnings", out var rawWarnings) ? rawWarnings : new ArrayList()).Count;
            InventoryDiagnosticsSections.Add(new RowVm
            {
                Id = Str(map, "section"),
                Name = Str(map, "section"),
                State = Str(map, "isValid"),
                Extra = $"errors={errors}; warnings={warnings}"
            });
        }

        AddIssues(payload, "errors");
        AddIssues(payload, "warnings");
        if (InventoryDiagnosticsIssues.Count == 0)
        {
            InventoryDiagnosticsIssues.Add(new InventoryDiagnosticsIssueVm { Severity = "ok", Code = "none", Message = "Проблемы не найдены", ItemInstanceId = "—", DefinitionId = "—" });
        }

        ApplyDiagnosticsToSlots();
        Notify(nameof(InventoryDiagnosticsStatus));
        Notify(nameof(InventoryDiagnosticsErrorCount));
        Notify(nameof(InventoryDiagnosticsWarningCount));
    }

    private void SetMode(string mode)
    {
        Mode = mode;
        RebuildFromPayload();
    }

    private void RebuildFromPayload()
    {
        if (_lastPayload == null)
        {
            NotifyAll();
            return;
        }

        var payload = _lastPayload;
        DisplayName = FirstNonEmpty(Str(payload, "name"), Str(payload, "displayName"), CharacterId, "Персонаж");
        RaceSummary = FirstNonEmpty(Str(payload, "race"), Str(payload, "species"), "Раса не указана");
        SelectedTitleDisplay = FirstNonEmpty(Str(payload, "selectedTitle"), "Без титула");
        BiographyText = BuildBiography(payload);
        NotesText = IsPlayerPreviewMode ? "Скрыто в виде игрока" : FirstNonEmpty(Str(payload, "notes"), Str(payload, "gmNotes"), "GM-заметки отсутствуют");

        FillStats(payload);
        FillMoney(payload);
        FillInventory(payload);
        FillSimpleCollection(Skills, payload, new[] { "skills", "characterSkills" }, "Навыки не загружены");
        FillSimpleCollection(Classes, payload, new[] { "classes", "classProgress", "characterClasses" }, "Классы не загружены");
        FillSimpleCollection(Conditions, payload, new[] { "conditions" }, "Состояния не загружены");
        FillSimpleCollection(Languages, payload, new[] { "languages" }, "Языки не загружены");
        FillRoleplayCollections(payload);

        ClassSummary = Classes.FirstOrDefault(row => row.Id != "placeholder")?.Name ?? "Развитие не загружено";
        ConditionsSummaryText = Conditions.FirstOrDefault(row => row.Id != "placeholder")?.Name ?? "Состояния не загружены";
        CombatSummaryText = "Combat summary появится после привязки active encounter.";
        WarningMessage = string.Empty;
        ErrorMessage = string.Empty;
        NotifyAll();
    }

    private string BuildBiography(Dictionary<string, object> payload)
    {
        if (IsPlayerPreviewMode && _hideBackstory)
        {
            return "Предыстория скрыта настройками видимости.";
        }

        var description = IsPlayerPreviewMode && _hideDescription ? string.Empty : Str(payload, "description");
        var backstory = Str(payload, "backstory");
        return FirstNonEmpty(description, backstory, "Биография отсутствует");
    }

    private void FillStats(Dictionary<string, object> payload)
    {
        Attributes.Clear();
        DerivedStats.Clear();
        if (IsPlayerPreviewMode && _hideStats)
        {
            AddPlaceholder(Attributes, "Характеристики скрыты настройками видимости");
            AddPlaceholder(DerivedStats, "Производные значения скрыты настройками видимости");
            return;
        }

        var stats = payload.TryGetValue("stats", out var rawStats) ? AsMap(rawStats) : null;
        if (stats == null)
        {
            AddPlaceholder(Attributes, "Характеристики не загружены");
            AddPlaceholder(DerivedStats, "Производные значения не загружены");
            return;
        }

        AddRow(DerivedStats, "health", "HP", Str(stats, "health"));
        AddRow(DerivedStats, "physicalArmor", "Физическая броня", Str(stats, "physicalArmor"));
        AddRow(DerivedStats, "magicalArmor", "Магическая броня", Str(stats, "magicalArmor"));
        AddRow(DerivedStats, "morale", "Мораль", Str(stats, "morale"));
        AddRow(Attributes, "strength", "Сила", Str(stats, "strength"));
        AddRow(Attributes, "dexterity", "Ловкость", Str(stats, "dexterity"));
        AddRow(Attributes, "endurance", "Выносливость", Str(stats, "endurance"));
        AddRow(Attributes, "wisdom", "Мудрость", Str(stats, "wisdom"));
        AddRow(Attributes, "intellect", "Интеллект", Str(stats, "intellect"));
        AddRow(Attributes, "charisma", "Харизма", Str(stats, "charisma"));
    }

    private void FillMoney(Dictionary<string, object> payload)
    {
        Currencies.Clear();
        var money = payload.TryGetValue("money", out var rawMoney) ? AsMap(rawMoney) : null;
        if (money != null)
        {
            foreach (var key in new[] { "Iron", "Bronze", "Silver", "Gold", "Platinum", "Orichalcum", "Adamant", "Sovereign" })
            {
                AddRow(Currencies, key, key, EmptyAsDash(Str(money, key)));
            }
        }
        AddRow(Currencies, "xpCoins", "Монеты опыта", EmptyAsDash(Str(payload, "xpCoins")));
        if (Currencies.Count == 0) AddPlaceholder(Currencies, "Деньги не загружены");
    }

    private void FillInventory(Dictionary<string, object> payload)
    {
        InventoryItems.Clear();
        EquipmentSlotsSummary.Clear();
        var items = ToList(payload.TryGetValue("inventory", out var rawInventory) ? rawInventory : new ArrayList())
            .Cast<object>()
            .Select(AsMap)
            .Where(map => map != null)
            .Select(map => BuildInventoryItem(map!))
            .ToList();

        foreach (var item in items)
        {
            InventoryItems.Add(item);
        }

        foreach (var slot in EquipmentSlots)
        {
            var matching = items.Where(item => item.IsEquipped && IsSlotMatch(item.EquipmentSlotId, slot.Id)).ToList();
            if (matching.Count == 0)
            {
                EquipmentSlotsSummary.Add(new EquipmentSlotCardVm { SlotId = slot.Id, SlotName = slot.Title, ItemName = "Пусто", Quantity = "—", Durability = "—", Warning = string.Empty });
                continue;
            }

            var first = matching[0];
            EquipmentSlotsSummary.Add(new EquipmentSlotCardVm
            {
                SlotId = slot.Id,
                SlotName = slot.Title,
                ItemName = first.DisplayName,
                Quantity = first.QuantityDisplay,
                Durability = first.DurabilityDisplay,
                Warning = matching.Count > 1 ? "Конфликт слота: несколько предметов" : string.Empty
            });
        }

        if (InventoryItems.Count == 0)
        {
            InventoryItems.Add(InventoryCardItemVm.Placeholder("Инвентарь не загружен"));
        }
    }

    private InventoryCardItemVm BuildInventoryItem(Dictionary<string, object> map)
    {
        var itemId = FirstNonEmpty(Str(map, "id"), Str(map, "itemId"), Str(map, "itemInstanceId"), "—");
        var definitionId = EmptyAsDash(FirstNonEmpty(Str(map, "definitionId"), Str(map, "itemDefinitionId")));
        var displayName = FirstNonEmpty(Str(map, "displayName"), Str(map, "name"), Str(map, "label"), definitionId, itemId);
        var quantity = EmptyAsDash(FirstNonEmpty(Str(map, "quantity"), "1"));
        var durability = FirstNonEmpty(Str(map, "durability"), Str(map, "durabilityOrHealth"));
        var maxDurability = Str(map, "maxDurability");
        var slotId = EmptyAsDash(FirstNonEmpty(Str(map, "equipmentSlotId"), Str(map, "slotId")));
        var tags = ReadTags(map);
        var type = EmptyAsDash(FirstNonEmpty(Str(map, "category"), Str(map, "itemType"), Str(map, "type")));

        return new InventoryCardItemVm
        {
            ItemInstanceId = itemId,
            DefinitionId = ShowGmInventoryDetails ? definitionId : "—",
            DisplayName = displayName,
            ItemType = type,
            Quantity = quantity,
            Durability = EmptyAsDash(durability),
            MaxDurability = EmptyAsDash(maxDurability),
            IsEquipped = IsTrue(Str(map, "isEquipped")) || IsTrue(Str(map, "equipped")),
            EquipmentSlotId = slotId,
            SlotDisplayName = ResolveSlotDisplayName(slotId),
            ContainerId = EmptyAsDash(Str(map, "containerId")),
            Tags = IsPlayerPreviewMode ? "—" : EmptyAsDash(tags),
            Notes = IsPlayerPreviewMode ? "—" : EmptyAsDash(Str(map, "notes"))
        };
    }

    private void FillRoleplayCollections(Dictionary<string, object> payload)
    {
        if (IsPlayerPreviewMode && _hideReputation)
        {
            ReputationItems.Clear();
            AddPlaceholder(ReputationItems, "Репутация скрыта настройками видимости");
        }
        else
        {
            FillSimpleCollection(ReputationItems, payload, new[] { "reputation" }, "Репутация не загружена");
        }
        FillSimpleCollection(HoldingsItems, payload, new[] { "holdings" }, "Владения не загружены");
    }

    private void FillSimpleCollection(ObservableCollection<RowVm> target, Dictionary<string, object> payload, string[] keys, string placeholder)
    {
        target.Clear();
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var raw)) continue;
            foreach (var item in ToList(raw))
            {
                var map = AsMap(item);
                if (map == null)
                {
                    var text = Convert.ToString(item) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text)) target.Add(new RowVm { Id = text, Name = text });
                    continue;
                }

                if (IsPlayerPreviewMode && HasSensitiveKeys(map)) continue;
                var id = FirstNonEmpty(Str(map, "id"), Str(map, "code"), Str(map, "skillCode"), Str(map, "classCode"), Str(map, "definitionId"));
                var name = FirstNonEmpty(Str(map, "displayName"), Str(map, "name"), Str(map, "targetName"), id);
                var state = FirstNonEmpty(Str(map, "status"), Str(map, "level"), Str(map, "value"), Str(map, "scopeType"));
                var extra = FirstNonEmpty(Str(map, "description"), Str(map, "notes"), Str(map, "extra"), Str(map, "type"));
                target.Add(new RowVm { Id = id, Name = EmptyAsDash(name), State = EmptyAsDash(state), Extra = EmptyAsDash(extra) });
            }
        }
        if (target.Count == 0) AddPlaceholder(target, placeholder);
    }

    private void AddIssues(Dictionary<string, object> payload, string key)
    {
        foreach (var issue in ToList(payload.TryGetValue(key, out var rawIssues) ? rawIssues : new ArrayList()))
        {
            var map = AsMap(issue);
            if (map == null) continue;
            InventoryDiagnosticsIssues.Add(new InventoryDiagnosticsIssueVm
            {
                Severity = EmptyAsDash(Str(map, "severity")),
                Code = EmptyAsDash(Str(map, "code")),
                Message = EmptyAsDash(Str(map, "message")),
                ItemInstanceId = EmptyAsDash(Str(map, "itemInstanceId")),
                DefinitionId = EmptyAsDash(Str(map, "definitionId"))
            });
        }
    }

    private void ApplyDiagnosticsToSlots()
    {
        foreach (var slot in EquipmentSlotsSummary)
        {
            var issue = InventoryDiagnosticsIssues.FirstOrDefault(x =>
                !string.Equals(x.Code, "none", StringComparison.OrdinalIgnoreCase)
                && (ContainsIgnoreCase(x.Code, "slot") || ContainsIgnoreCase(x.Message, slot.SlotId) || ContainsIgnoreCase(x.Message, slot.SlotName)));
            if (issue != null && string.IsNullOrWhiteSpace(slot.Warning))
            {
                slot.Warning = $"{issue.Severity}: {issue.Code}";
            }
        }
    }

    private void NotifyAll()
    {
        Notify(nameof(CharacterId));
        Notify(nameof(DisplayName));
        Notify(nameof(PlayerOwner));
        Notify(nameof(RaceSummary));
        Notify(nameof(ClassSummary));
        Notify(nameof(SelectedTitleDisplay));
        Notify(nameof(BiographyText));
        Notify(nameof(NotesText));
        Notify(nameof(CombatSummaryText));
        Notify(nameof(ConditionsSummaryText));
        Notify(nameof(LastLoadedAtUtc));
        Notify(nameof(LastLoadedText));
        Notify(nameof(ModeTitle));
        Notify(nameof(ShowGmInventoryDetails));
    }

    private static void AddPlaceholder(ObservableCollection<RowVm> target, string text)
    {
        target.Add(new RowVm { Id = "placeholder", Name = text, State = "Данные отсутствуют" });
    }

    private static void AddRow(ObservableCollection<RowVm> target, string id, string name, string value)
    {
        target.Add(new RowVm { Id = id, Name = name, State = string.IsNullOrWhiteSpace(value) ? "0" : value });
    }

    private static bool IsSlotMatch(string actualSlotId, string expectedSlotId)
    {
        if (string.IsNullOrWhiteSpace(actualSlotId)) return false;
        if (string.Equals(actualSlotId, expectedSlotId, StringComparison.OrdinalIgnoreCase)) return true;
        if (expectedSlotId == "accessory" && ContainsIgnoreCase(actualSlotId, "accessory")) return true;
        return false;
    }

    private static bool IsTrue(string value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSensitiveKeys(Dictionary<string, object> map)
    {
        return map.Keys.Any(key =>
            ContainsIgnoreCase(key, "serverOnly")
            || ContainsIgnoreCase(key, "gm")
            || ContainsIgnoreCase(key, "hidden")
            || ContainsIgnoreCase(key, "secret")
            || ContainsIgnoreCase(key, "private")
            || ContainsIgnoreCase(key, "diagnostic"));
    }

    private static bool ContainsIgnoreCase(string value, string fragment)
    {
        return (value ?? string.Empty).IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string ReadTags(Dictionary<string, object> map)
    {
        if (!map.TryGetValue("tags", out var raw) || raw == null) return string.Empty;
        if (raw is string text) return text;
        return string.Join(", ", ToList(raw).Cast<object>().Select(x => Convert.ToString(x)).Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static Dictionary<string, object>? AsMap(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary dictionary)
        {
            var map = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) map[key] = entry.Value;
            }
            return map.Count > 0 ? map : null;
        }
        return null;
    }

    private static IList ToList(object? value)
    {
        return value as IList ?? new ArrayList();
    }

    private static string Str(Dictionary<string, object> map, string key)
    {
        return map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static string EmptyAsDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private static string ResolveSlotDisplayName(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId) || slotId == "—") return "Не назначен";
        var slot = EquipmentSlots.FirstOrDefault(candidate => IsSlotMatch(slotId, candidate.Id));
        return string.IsNullOrWhiteSpace(slot.Title) ? slotId : slot.Title;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}

public sealed class InventoryCardItemVm
{
    public string ItemInstanceId { get; set; } = "—";
    public string DefinitionId { get; set; } = "—";
    public string DisplayName { get; set; } = "—";
    public string ItemType { get; set; } = "—";
    public string Quantity { get; set; } = "—";
    public string Durability { get; set; } = "—";
    public string MaxDurability { get; set; } = "—";
    public bool IsEquipped { get; set; }
    public string EquipmentSlotId { get; set; } = "—";
    public string SlotDisplayName { get; set; } = "Не назначен";
    public string ContainerId { get; set; } = "—";
    public string Tags { get; set; } = "—";
    public string Notes { get; set; } = "—";
    public string QuantityDisplay => Quantity;
    public string DurabilityDisplay => MaxDurability == "—" ? Durability : $"{Durability}/{MaxDurability}";

    public static InventoryCardItemVm Placeholder(string text) => new InventoryCardItemVm { DisplayName = text };
}

public sealed class EquipmentSlotCardVm : ViewModelBase
{
    private string _warning = string.Empty;

    public string SlotId { get; set; } = string.Empty;
    public string SlotName { get; set; } = string.Empty;
    public string ItemName { get; set; } = "Пусто";
    public string Quantity { get; set; } = "—";
    public string Durability { get; set; } = "—";
    public string Warning { get => _warning; set { _warning = value; Notify(); } }
}

public sealed class InventoryDiagnosticsIssueVm
{
    public string Severity { get; set; } = "—";
    public string Code { get; set; } = "—";
    public string Message { get; set; } = "—";
    public string ItemInstanceId { get; set; } = "—";
    public string DefinitionId { get; set; } = "—";
}
