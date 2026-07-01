using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminItemsEquipmentCatalogViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private CatalogDefinitionUiItem? _selectedItem;
    private CatalogDefinitionUiItem? _selectedWeapon;
    private CatalogDefinitionUiItem? _selectedArmor;
    private CatalogDefinitionUiItem? _selectedAmmo;
    private CatalogDefinitionUiItem? _selectedSlot;
    private string _itemSearch = string.Empty;
    private string _weaponSearch = string.Empty;
    private string _armorSearch = string.Empty;
    private string _ammoSearch = string.Empty;
    private string _slotSearch = string.Empty;
    private string _itemStatus = "Загрузите справочник предметов.";
    private string _weaponStatus = "Загрузите справочник оружия.";
    private string _armorStatus = "Загрузите справочник брони.";
    private string _ammoStatus = "Загрузите справочник боеприпасов.";
    private string _slotStatus = "Загрузите справочник слотов экипировки.";

    public AdminItemsEquipmentCatalogViewModel(CommandApi api)
    {
        _api = api;

        ItemEditor = CatalogDefinitionEditorVm.CreateItem();
        WeaponEditor = CatalogDefinitionEditorVm.CreateWeapon();
        ArmorEditor = CatalogDefinitionEditorVm.CreateArmor();
        AmmoEditor = CatalogDefinitionEditorVm.CreateAmmo();
        SlotEditor = CatalogDefinitionEditorVm.CreateSlot();

        RefreshItemsCommand = new RelayCommand(() => RefreshCategory(DefinitionCategoryIds.Item));
        CreateItemCommand = new RelayCommand(() => SaveCategory(DefinitionCategoryIds.Item, true));
        UpdateItemCommand = new RelayCommand(() => SaveCategory(DefinitionCategoryIds.Item, false));
        ArchiveItemCommand = new RelayCommand(() => ArchiveCategory(DefinitionCategoryIds.Item));

        RefreshWeaponsCommand = new RelayCommand(() => RefreshCategory(DefinitionCategoryIds.Weapon));
        CreateWeaponCommand = new RelayCommand(() => SaveCategory(DefinitionCategoryIds.Weapon, true));
        UpdateWeaponCommand = new RelayCommand(() => SaveCategory(DefinitionCategoryIds.Weapon, false));
        ArchiveWeaponCommand = new RelayCommand(() => ArchiveCategory(DefinitionCategoryIds.Weapon));

        RefreshArmorCommand = new RelayCommand(() => RefreshCategory(DefinitionCategoryIds.Armor));
        CreateArmorCommand = new RelayCommand(() => SaveCategory(DefinitionCategoryIds.Armor, true));
        UpdateArmorCommand = new RelayCommand(() => SaveCategory(DefinitionCategoryIds.Armor, false));
        ArchiveArmorCommand = new RelayCommand(() => ArchiveCategory(DefinitionCategoryIds.Armor));

        RefreshAmmoCommand = new RelayCommand(() => RefreshCategory(DefinitionCategoryIds.Ammo));
        CreateAmmoCommand = new RelayCommand(() => SaveCategory(DefinitionCategoryIds.Ammo, true));
        UpdateAmmoCommand = new RelayCommand(() => SaveCategory(DefinitionCategoryIds.Ammo, false));
        ArchiveAmmoCommand = new RelayCommand(() => ArchiveCategory(DefinitionCategoryIds.Ammo));

        RefreshSlotsCommand = new RelayCommand(() => RefreshCategory(DefinitionCategoryIds.EquipmentSlot));
        CreateSlotCommand = new RelayCommand(() => SaveCategory(DefinitionCategoryIds.EquipmentSlot, true));
        UpdateSlotCommand = new RelayCommand(() => SaveCategory(DefinitionCategoryIds.EquipmentSlot, false));
        ArchiveSlotCommand = new RelayCommand(() => ArchiveCategory(DefinitionCategoryIds.EquipmentSlot));
    }

    public ObservableCollection<CatalogDefinitionUiItem> Items { get; } = new();
    public ObservableCollection<CatalogDefinitionUiItem> Weapons { get; } = new();
    public ObservableCollection<CatalogDefinitionUiItem> Armor { get; } = new();
    public ObservableCollection<CatalogDefinitionUiItem> Ammo { get; } = new();
    public ObservableCollection<CatalogDefinitionUiItem> EquipmentSlots { get; } = new();

    public CatalogDefinitionEditorVm ItemEditor { get; }
    public CatalogDefinitionEditorVm WeaponEditor { get; }
    public CatalogDefinitionEditorVm ArmorEditor { get; }
    public CatalogDefinitionEditorVm AmmoEditor { get; }
    public CatalogDefinitionEditorVm SlotEditor { get; }

    public ICommand RefreshItemsCommand { get; }
    public ICommand CreateItemCommand { get; }
    public ICommand UpdateItemCommand { get; }
    public ICommand ArchiveItemCommand { get; }
    public ICommand RefreshWeaponsCommand { get; }
    public ICommand CreateWeaponCommand { get; }
    public ICommand UpdateWeaponCommand { get; }
    public ICommand ArchiveWeaponCommand { get; }
    public ICommand RefreshArmorCommand { get; }
    public ICommand CreateArmorCommand { get; }
    public ICommand UpdateArmorCommand { get; }
    public ICommand ArchiveArmorCommand { get; }
    public ICommand RefreshAmmoCommand { get; }
    public ICommand CreateAmmoCommand { get; }
    public ICommand UpdateAmmoCommand { get; }
    public ICommand ArchiveAmmoCommand { get; }
    public ICommand RefreshSlotsCommand { get; }
    public ICommand CreateSlotCommand { get; }
    public ICommand UpdateSlotCommand { get; }
    public ICommand ArchiveSlotCommand { get; }

    public string ItemSearch { get => _itemSearch; set { if (_itemSearch != value) { _itemSearch = value; Notify(); } } }
    public string WeaponSearch { get => _weaponSearch; set { if (_weaponSearch != value) { _weaponSearch = value; Notify(); } } }
    public string ArmorSearch { get => _armorSearch; set { if (_armorSearch != value) { _armorSearch = value; Notify(); } } }
    public string AmmoSearch { get => _ammoSearch; set { if (_ammoSearch != value) { _ammoSearch = value; Notify(); } } }
    public string SlotSearch { get => _slotSearch; set { if (_slotSearch != value) { _slotSearch = value; Notify(); } } }

    public string ItemStatus { get => _itemStatus; set { if (_itemStatus != value) { _itemStatus = value; Notify(); } } }
    public string WeaponStatus { get => _weaponStatus; set { if (_weaponStatus != value) { _weaponStatus = value; Notify(); } } }
    public string ArmorStatus { get => _armorStatus; set { if (_armorStatus != value) { _armorStatus = value; Notify(); } } }
    public string AmmoStatus { get => _ammoStatus; set { if (_ammoStatus != value) { _ammoStatus = value; Notify(); } } }
    public string SlotStatus { get => _slotStatus; set { if (_slotStatus != value) { _slotStatus = value; Notify(); } } }

    public CatalogDefinitionUiItem? SelectedItem
    {
        get => _selectedItem;
        set { if (_selectedItem != value) { _selectedItem = value; Notify(); CopyToEditor(value, ItemEditor); } }
    }

    public CatalogDefinitionUiItem? SelectedWeapon
    {
        get => _selectedWeapon;
        set { if (_selectedWeapon != value) { _selectedWeapon = value; Notify(); CopyToEditor(value, WeaponEditor); } }
    }

    public CatalogDefinitionUiItem? SelectedArmor
    {
        get => _selectedArmor;
        set { if (_selectedArmor != value) { _selectedArmor = value; Notify(); CopyToEditor(value, ArmorEditor); } }
    }

    public CatalogDefinitionUiItem? SelectedAmmo
    {
        get => _selectedAmmo;
        set { if (_selectedAmmo != value) { _selectedAmmo = value; Notify(); CopyToEditor(value, AmmoEditor); } }
    }

    public CatalogDefinitionUiItem? SelectedSlot
    {
        get => _selectedSlot;
        set { if (_selectedSlot != value) { _selectedSlot = value; Notify(); CopyToEditor(value, SlotEditor); } }
    }

    private void RefreshCategory(string category)
    {
        try
        {
            SetStatus(category, "Загрузка...");
            var response = ListCommand(category)(new Dictionary<string, object>
            {
                ["search"] = SearchText(category),
                ["includeArchived"] = true
            });
            EnsureOk(response);

            var rows = PayloadList(response.Payload, "items").Select(CatalogDefinitionUiItem.FromMap).ToList();
            var target = Collection(category);
            target.Clear();
            foreach (var row in rows.OrderBy(x => x.SortOrder).ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(row);
            }

            SetStatus(category, $"Загружено: {rows.Count}");
            ClientLogService.Instance.Info($"admin.catalog.{category}.load.done count={rows.Count}");
        }
        catch (Exception ex)
        {
            SetStatus(category, $"Ошибка загрузки: {ex.Message}");
            ClientLogService.Instance.Error($"admin.catalog.{category}.load.error", ex);
        }
    }

    private void SaveCategory(string category, bool create)
    {
        var editor = Editor(category);
        try
        {
            var payload = editor.ToPayload(category);
            var response = create ? CreateCommand(category)(payload) : UpdateCommand(category)(payload);
            EnsureOk(response);
            SetStatus(category, create ? "Запись создана." : "Запись сохранена.");
            RefreshCategory(category);
            var saved = PayloadMap(response.Payload, "item");
            if (saved.Count > 0)
            {
                SelectByDefinitionId(category, S(Get(saved, "definitionId")));
            }
            ClientLogService.Instance.Info($"admin.catalog.{category}.save.done create={create} code={editor.Code}");
        }
        catch (Exception ex)
        {
            SetStatus(category, $"Ошибка сохранения: {ex.Message}");
            ClientLogService.Instance.Error($"admin.catalog.{category}.save.error create={create}", ex);
        }
    }

    private void ArchiveCategory(string category)
    {
        var editor = Editor(category);
        try
        {
            var id = FirstNonEmpty(editor.DefinitionId, editor.Code);
            if (string.IsNullOrWhiteSpace(id))
            {
                SetStatus(category, "Выберите или укажите запись для архивации.");
                return;
            }

            var response = ArchiveCommand(category)(new Dictionary<string, object> { ["definitionId"] = id });
            EnsureOk(response);
            SetStatus(category, "Запись архивирована.");
            RefreshCategory(category);
            ClientLogService.Instance.Info($"admin.catalog.{category}.archive.done id={id}");
        }
        catch (Exception ex)
        {
            SetStatus(category, $"Ошибка архивации: {ex.Message}");
            ClientLogService.Instance.Error($"admin.catalog.{category}.archive.error", ex);
        }
    }

    private void SelectByDefinitionId(string category, string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId)) return;
        var row = Collection(category).FirstOrDefault(x => string.Equals(x.DefinitionId, definitionId, StringComparison.OrdinalIgnoreCase));
        switch (category)
        {
            case DefinitionCategoryIds.Item: SelectedItem = row; break;
            case DefinitionCategoryIds.Weapon: SelectedWeapon = row; break;
            case DefinitionCategoryIds.Armor: SelectedArmor = row; break;
            case DefinitionCategoryIds.Ammo: SelectedAmmo = row; break;
            case DefinitionCategoryIds.EquipmentSlot: SelectedSlot = row; break;
        }
    }

    private void CopyToEditor(CatalogDefinitionUiItem? row, CatalogDefinitionEditorVm editor)
    {
        if (row == null) return;
        editor.Load(row);
    }

    private Func<Dictionary<string, object>, ResponseEnvelope> ListCommand(string category) => category switch
    {
        DefinitionCategoryIds.Item => _api.CatalogAdminItemsList,
        DefinitionCategoryIds.Weapon => _api.CatalogAdminWeaponsList,
        DefinitionCategoryIds.Armor => _api.CatalogAdminArmorList,
        DefinitionCategoryIds.Ammo => _api.CatalogAdminAmmoList,
        DefinitionCategoryIds.EquipmentSlot => _api.CatalogAdminEquipmentSlotsList,
        _ => _api.CatalogAdminItemsList
    };

    private Func<Dictionary<string, object>, ResponseEnvelope> CreateCommand(string category) => category switch
    {
        DefinitionCategoryIds.Item => _api.CatalogAdminItemsCreate,
        DefinitionCategoryIds.Weapon => _api.CatalogAdminWeaponsCreate,
        DefinitionCategoryIds.Armor => _api.CatalogAdminArmorCreate,
        DefinitionCategoryIds.Ammo => _api.CatalogAdminAmmoCreate,
        DefinitionCategoryIds.EquipmentSlot => _api.CatalogAdminEquipmentSlotsCreate,
        _ => _api.CatalogAdminItemsCreate
    };

    private Func<Dictionary<string, object>, ResponseEnvelope> UpdateCommand(string category) => category switch
    {
        DefinitionCategoryIds.Item => _api.CatalogAdminItemsUpdate,
        DefinitionCategoryIds.Weapon => _api.CatalogAdminWeaponsUpdate,
        DefinitionCategoryIds.Armor => _api.CatalogAdminArmorUpdate,
        DefinitionCategoryIds.Ammo => _api.CatalogAdminAmmoUpdate,
        DefinitionCategoryIds.EquipmentSlot => _api.CatalogAdminEquipmentSlotsUpdate,
        _ => _api.CatalogAdminItemsUpdate
    };

    private Func<Dictionary<string, object>, ResponseEnvelope> ArchiveCommand(string category) => category switch
    {
        DefinitionCategoryIds.Item => _api.CatalogAdminItemsArchive,
        DefinitionCategoryIds.Weapon => _api.CatalogAdminWeaponsArchive,
        DefinitionCategoryIds.Armor => _api.CatalogAdminArmorArchive,
        DefinitionCategoryIds.Ammo => _api.CatalogAdminAmmoArchive,
        DefinitionCategoryIds.EquipmentSlot => _api.CatalogAdminEquipmentSlotsArchive,
        _ => _api.CatalogAdminItemsArchive
    };

    private ObservableCollection<CatalogDefinitionUiItem> Collection(string category) => category switch
    {
        DefinitionCategoryIds.Item => Items,
        DefinitionCategoryIds.Weapon => Weapons,
        DefinitionCategoryIds.Armor => Armor,
        DefinitionCategoryIds.Ammo => Ammo,
        DefinitionCategoryIds.EquipmentSlot => EquipmentSlots,
        _ => Items
    };

    private CatalogDefinitionEditorVm Editor(string category) => category switch
    {
        DefinitionCategoryIds.Item => ItemEditor,
        DefinitionCategoryIds.Weapon => WeaponEditor,
        DefinitionCategoryIds.Armor => ArmorEditor,
        DefinitionCategoryIds.Ammo => AmmoEditor,
        DefinitionCategoryIds.EquipmentSlot => SlotEditor,
        _ => ItemEditor
    };

    private string SearchText(string category) => category switch
    {
        DefinitionCategoryIds.Item => ItemSearch,
        DefinitionCategoryIds.Weapon => WeaponSearch,
        DefinitionCategoryIds.Armor => ArmorSearch,
        DefinitionCategoryIds.Ammo => AmmoSearch,
        DefinitionCategoryIds.EquipmentSlot => SlotSearch,
        _ => string.Empty
    };

    private void SetStatus(string category, string value)
    {
        switch (category)
        {
            case DefinitionCategoryIds.Item: ItemStatus = value; break;
            case DefinitionCategoryIds.Weapon: WeaponStatus = value; break;
            case DefinitionCategoryIds.Armor: ArmorStatus = value; break;
            case DefinitionCategoryIds.Ammo: AmmoStatus = value; break;
            case DefinitionCategoryIds.EquipmentSlot: SlotStatus = value; break;
        }
    }

    private static void EnsureOk(ResponseEnvelope response)
    {
        if (response.Status == ResponseStatus.Ok) return;
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? response.Status.ToString() : response.Message);
    }

    private static IReadOnlyList<Dictionary<string, object>> PayloadList(Dictionary<string, object> payload, string key)
    {
        var value = Get(payload, key);
        if (value is IEnumerable enumerable && value is not string)
        {
            return enumerable.OfType<object>().Select(ToMap).Where(x => x.Count > 0).ToList();
        }

        return Array.Empty<Dictionary<string, object>>();
    }

    private static Dictionary<string, object> PayloadMap(Dictionary<string, object> payload, string key) => ToMap(Get(payload, key));

    private static Dictionary<string, object> ToMap(object? value)
    {
        if (value is Dictionary<string, object> map) return map;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key == null) continue;
                result[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] = entry.Value ?? string.Empty;
            }
            return result;
        }
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    internal static object? Get(Dictionary<string, object> map, string key)
    {
        if (map.TryGetValue(key, out var value)) return value;
        var pair = map.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(pair.Key) ? null : pair.Value;
    }

    internal static string S(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    internal static int I(object? value)
    {
        if (value is int i) return i;
        if (value is long l) return (int)l;
        return int.TryParse(S(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    internal static decimal D(object? value)
    {
        if (value is decimal d) return d;
        if (value is double db) return (decimal)db;
        if (value is float f) return (decimal)f;
        return decimal.TryParse(S(value), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }

    internal static bool B(object? value)
    {
        if (value is bool b) return b;
        var text = S(value);
        return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase);
    }

    internal static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

public sealed class CatalogDefinitionEditorVm : ViewModelBase
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string TagsText { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public string WeaponKind { get; set; } = string.Empty;
    public string WeaponType { get; set; } = string.Empty;
    public string DamageDraft { get; set; } = string.Empty;
    public string DamageType { get; set; } = string.Empty;
    public string Range { get; set; } = string.Empty;
    public string RangeType { get; set; } = string.Empty;
    public int Hands { get; set; } = 1;
    public string AmmoType { get; set; } = string.Empty;
    public string ArmorKind { get; set; } = string.Empty;
    public string ArmorType { get; set; } = string.Empty;
    public string Coverage { get; set; } = string.Empty;
    public string Caliber { get; set; } = string.Empty;
    public string DamageModifier { get; set; } = string.Empty;
    public string SlotId { get; set; } = string.Empty;
    public string SlotGroup { get; set; } = string.Empty;
    public string AllowedItemCategories { get; set; } = string.Empty;
    public string AllowedTags { get; set; } = string.Empty;
    public string BodyCompatibilityTags { get; set; } = string.Empty;
    public string CompatibleSlots { get; set; } = string.Empty;
    public string CompatibleAmmoTags { get; set; } = string.Empty;
    public string CompatibilityTags { get; set; } = string.Empty;
    public string LinkedSkillIds { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
    public int StackSize { get; set; } = 1;
    public int Value { get; set; }
    public decimal Weight { get; set; }
    public int PhysicalArmor { get; set; }
    public int MagicalArmor { get; set; }
    public int Durability { get; set; }
    public int Ammo { get; set; }
    public int SortOrder { get; set; }
    public bool IsConsumable { get; set; }
    public bool IsEquipment { get; set; }
    public bool IsTwoHanded { get; set; }
    public bool IsExclusive { get; set; }
    public bool IsPlayerVisible { get; set; } = true;

    public static CatalogDefinitionEditorVm CreateItem() => new()
    {
        Code = $"item_{DateTime.UtcNow:yyyyMMddHHmmss}",
        DisplayName = "Новый предмет",
        ItemKind = "generic",
        StackSize = 1,
        IsPlayerVisible = true
    };

    public static CatalogDefinitionEditorVm CreateWeapon() => new()
    {
        Code = $"weapon_{DateTime.UtcNow:yyyyMMddHHmmss}",
        DisplayName = "Новое оружие",
        WeaponKind = "melee",
        DamageDraft = "1d6",
        Hands = 1,
        IsEquipment = true,
        IsPlayerVisible = true
    };

    public static CatalogDefinitionEditorVm CreateArmor() => new()
    {
        Code = $"armor_{DateTime.UtcNow:yyyyMMddHHmmss}",
        DisplayName = "Новая броня",
        ArmorKind = "light",
        Coverage = "torso",
        IsEquipment = true,
        IsPlayerVisible = true
    };

    public static CatalogDefinitionEditorVm CreateAmmo() => new()
    {
        Code = $"ammo_{DateTime.UtcNow:yyyyMMddHHmmss}",
        DisplayName = "Новые боеприпасы",
        AmmoType = "generic",
        StackSize = 20,
        IsPlayerVisible = true
    };

    public static CatalogDefinitionEditorVm CreateSlot() => new()
    {
        Code = $"slot_{DateTime.UtcNow:yyyyMMddHHmmss}",
        DisplayName = "Новый слот",
        SlotGroup = "body",
        IsPlayerVisible = true
    };

    public void Load(CatalogDefinitionUiItem row)
    {
        DefinitionId = row.DefinitionId;
        Code = row.Code;
        DisplayName = row.DisplayName;
        Description = row.Description;
        RuleSetId = row.RuleSetId;
        TagsText = row.TagsText;
        ItemKind = row.ItemKind;
        WeaponKind = row.WeaponKind;
        WeaponType = row.WeaponType;
        DamageDraft = row.DamageDraft;
        DamageType = row.DamageType;
        Range = row.Range;
        RangeType = row.RangeType;
        Hands = row.Hands;
        AmmoType = row.AmmoType;
        ArmorKind = row.ArmorKind;
        ArmorType = row.ArmorType;
        Coverage = row.Coverage;
        Caliber = row.Caliber;
        DamageModifier = row.DamageModifier;
        SlotId = row.SlotId;
        SlotGroup = row.SlotGroup;
        AllowedItemCategories = row.AllowedItemCategories;
        AllowedTags = row.AllowedTags;
        BodyCompatibilityTags = row.BodyCompatibilityTags;
        CompatibleSlots = row.CompatibleSlots;
        CompatibleAmmoTags = row.CompatibleAmmoTags;
        CompatibilityTags = row.CompatibilityTags;
        LinkedSkillIds = row.LinkedSkillIds;
        Rarity = row.Rarity;
        StackSize = row.StackSize;
        Value = row.Value;
        Weight = row.Weight;
        PhysicalArmor = row.PhysicalArmor;
        MagicalArmor = row.MagicalArmor;
        Durability = row.Durability;
        Ammo = row.Ammo;
        SortOrder = row.SortOrder;
        IsConsumable = row.IsConsumable;
        IsEquipment = row.IsEquipment;
        IsTwoHanded = row.IsTwoHanded;
        IsExclusive = row.IsExclusive;
        IsPlayerVisible = row.IsPlayerVisible;
        NotifyAll();
    }

    public Dictionary<string, object> ToPayload(string category)
    {
        var payload = new Dictionary<string, object>
        {
            ["definitionId"] = AdminItemsEquipmentCatalogViewModel.FirstNonEmpty(DefinitionId, Code),
            ["code"] = Code,
            ["displayName"] = DisplayName,
            ["name"] = DisplayName,
            ["description"] = Description,
            ["ruleSetId"] = RuleSetId,
            ["isPlayerVisible"] = IsPlayerVisible,
            ["tags"] = Split(TagsText),
            ["tagsText"] = TagsText,
            ["sortOrder"] = SortOrder,
            ["stackSize"] = StackSize,
            ["weight"] = Weight,
            ["value"] = Value,
            ["rarity"] = Rarity,
            ["isConsumable"] = IsConsumable,
            ["isEquipment"] = IsEquipment,
            ["isTwoHanded"] = IsTwoHanded,
            ["isExclusive"] = IsExclusive,
            ["physicalArmor"] = PhysicalArmor,
            ["magicalArmor"] = MagicalArmor,
            ["durability"] = Durability,
            ["ammo"] = Ammo,
            ["itemKind"] = ItemKind,
            ["weaponKind"] = WeaponKind,
            ["weaponType"] = WeaponType,
            ["damageDraft"] = DamageDraft,
            ["damageType"] = DamageType,
            ["range"] = Range,
            ["rangeType"] = RangeType,
            ["hands"] = Hands,
            ["ammoType"] = AmmoType,
            ["armorKind"] = ArmorKind,
            ["armorType"] = ArmorType,
            ["coverage"] = Coverage,
            ["caliber"] = Caliber,
            ["damageModifier"] = DamageModifier,
            ["slotId"] = AdminItemsEquipmentCatalogViewModel.FirstNonEmpty(SlotId, Code),
            ["slotGroup"] = SlotGroup,
            ["allowedItemCategories"] = Split(AllowedItemCategories),
            ["allowedTags"] = Split(AllowedTags),
            ["bodyCompatibilityTags"] = Split(BodyCompatibilityTags),
            ["compatibleSlots"] = Split(CompatibleSlots),
            ["compatibleAmmoTags"] = Split(CompatibleAmmoTags),
            ["compatibilityTags"] = Split(CompatibilityTags),
            ["linkedSkillIds"] = Split(LinkedSkillIds)
        };

        if (category == DefinitionCategoryIds.Item)
        {
            payload["category"] = ItemKind;
        }

        return payload;
    }

    private void NotifyAll()
    {
        foreach (var property in GetType().GetProperties())
        {
            Notify(property.Name);
        }
    }

    private static string[] Split(string value)
        => (value ?? string.Empty).Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public sealed class CatalogDefinitionUiItem
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }
    public string TagsText { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public string WeaponKind { get; set; } = string.Empty;
    public string WeaponType { get; set; } = string.Empty;
    public string DamageDraft { get; set; } = string.Empty;
    public string DamageType { get; set; } = string.Empty;
    public string Range { get; set; } = string.Empty;
    public string RangeType { get; set; } = string.Empty;
    public int Hands { get; set; }
    public string AmmoType { get; set; } = string.Empty;
    public string ArmorKind { get; set; } = string.Empty;
    public string ArmorType { get; set; } = string.Empty;
    public string Coverage { get; set; } = string.Empty;
    public string Caliber { get; set; } = string.Empty;
    public string DamageModifier { get; set; } = string.Empty;
    public string SlotId { get; set; } = string.Empty;
    public string SlotGroup { get; set; } = string.Empty;
    public string AllowedItemCategories { get; set; } = string.Empty;
    public string AllowedTags { get; set; } = string.Empty;
    public string BodyCompatibilityTags { get; set; } = string.Empty;
    public string CompatibleSlots { get; set; } = string.Empty;
    public string CompatibleAmmoTags { get; set; } = string.Empty;
    public string CompatibilityTags { get; set; } = string.Empty;
    public string LinkedSkillIds { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
    public int StackSize { get; set; }
    public int Value { get; set; }
    public decimal Weight { get; set; }
    public int PhysicalArmor { get; set; }
    public int MagicalArmor { get; set; }
    public int Durability { get; set; }
    public int Ammo { get; set; }
    public int SortOrder { get; set; }
    public bool IsConsumable { get; set; }
    public bool IsEquipment { get; set; }
    public bool IsTwoHanded { get; set; }
    public bool IsExclusive { get; set; }

    public string VisibilityDisplay => IsPlayerVisible ? "видно игрокам" : "скрыто";
    public string ArchivedDisplay => IsArchived ? "архив" : "активно";
    public string MainDetail => AdminItemsEquipmentCatalogViewModel.FirstNonEmpty(ItemKind, WeaponKind, ArmorKind, AmmoType, SlotGroup, Category, "—");
    public string NumericSummary => $"Стек {StackSize}; вес {Weight}; цена {Value}";

    public static CatalogDefinitionUiItem FromMap(Dictionary<string, object> map) => new()
    {
        DefinitionId = S(map, "definitionId"),
        Code = AdminItemsEquipmentCatalogViewModel.FirstNonEmpty(S(map, "code"), S(map, "id")),
        Category = S(map, "category"),
        DisplayName = AdminItemsEquipmentCatalogViewModel.FirstNonEmpty(S(map, "displayName"), S(map, "name"), S(map, "code")),
        Description = S(map, "description"),
        RuleSetId = S(map, "ruleSetId"),
        IsPlayerVisible = AdminItemsEquipmentCatalogViewModel.B(AdminItemsEquipmentCatalogViewModel.Get(map, "isPlayerVisible")),
        IsArchived = AdminItemsEquipmentCatalogViewModel.B(AdminItemsEquipmentCatalogViewModel.Get(map, "isArchived")),
        TagsText = S(map, "tagsText"),
        ItemKind = AdminItemsEquipmentCatalogViewModel.FirstNonEmpty(S(map, "itemKind"), S(map, "itemType")),
        WeaponKind = S(map, "weaponKind"),
        WeaponType = S(map, "weaponType"),
        DamageDraft = S(map, "damageDraft"),
        DamageType = S(map, "damageType"),
        Range = S(map, "range"),
        RangeType = S(map, "rangeType"),
        Hands = AdminItemsEquipmentCatalogViewModel.I(AdminItemsEquipmentCatalogViewModel.Get(map, "hands")),
        AmmoType = S(map, "ammoType"),
        ArmorKind = S(map, "armorKind"),
        ArmorType = S(map, "armorType"),
        Coverage = S(map, "coverage"),
        Caliber = S(map, "caliber"),
        DamageModifier = S(map, "damageModifier"),
        SlotId = S(map, "slotId"),
        SlotGroup = S(map, "slotGroup"),
        AllowedItemCategories = Join(map, "allowedItemCategories"),
        AllowedTags = Join(map, "allowedTags"),
        BodyCompatibilityTags = Join(map, "bodyCompatibilityTags"),
        CompatibleSlots = Join(map, "compatibleSlots"),
        CompatibleAmmoTags = Join(map, "compatibleAmmoTags"),
        CompatibilityTags = Join(map, "compatibilityTags"),
        LinkedSkillIds = Join(map, "linkedSkillIds"),
        Rarity = S(map, "rarity"),
        StackSize = AdminItemsEquipmentCatalogViewModel.I(AdminItemsEquipmentCatalogViewModel.Get(map, "stackSize")),
        Value = AdminItemsEquipmentCatalogViewModel.I(AdminItemsEquipmentCatalogViewModel.Get(map, "value")),
        Weight = AdminItemsEquipmentCatalogViewModel.D(AdminItemsEquipmentCatalogViewModel.Get(map, "weight")),
        PhysicalArmor = AdminItemsEquipmentCatalogViewModel.I(AdminItemsEquipmentCatalogViewModel.Get(map, "physicalArmor")),
        MagicalArmor = AdminItemsEquipmentCatalogViewModel.I(AdminItemsEquipmentCatalogViewModel.Get(map, "magicalArmor")),
        Durability = AdminItemsEquipmentCatalogViewModel.I(AdminItemsEquipmentCatalogViewModel.Get(map, "durability")),
        Ammo = AdminItemsEquipmentCatalogViewModel.I(AdminItemsEquipmentCatalogViewModel.Get(map, "ammo")),
        SortOrder = AdminItemsEquipmentCatalogViewModel.I(AdminItemsEquipmentCatalogViewModel.Get(map, "sortOrder")),
        IsConsumable = AdminItemsEquipmentCatalogViewModel.B(AdminItemsEquipmentCatalogViewModel.Get(map, "isConsumable")),
        IsEquipment = AdminItemsEquipmentCatalogViewModel.B(AdminItemsEquipmentCatalogViewModel.Get(map, "isEquipment")),
        IsTwoHanded = AdminItemsEquipmentCatalogViewModel.B(AdminItemsEquipmentCatalogViewModel.Get(map, "isTwoHanded")),
        IsExclusive = AdminItemsEquipmentCatalogViewModel.B(AdminItemsEquipmentCatalogViewModel.Get(map, "isExclusive"))
    };

    private static string S(Dictionary<string, object> map, string key) => AdminItemsEquipmentCatalogViewModel.S(AdminItemsEquipmentCatalogViewModel.Get(map, key));

    private static string Join(Dictionary<string, object> map, string key)
    {
        var value = AdminItemsEquipmentCatalogViewModel.Get(map, key);
        if (value is IEnumerable enumerable && value is not string)
        {
            return string.Join(", ", enumerable.OfType<object>().Select(AdminItemsEquipmentCatalogViewModel.S).Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        return AdminItemsEquipmentCatalogViewModel.S(value);
    }
}
