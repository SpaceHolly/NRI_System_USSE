using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;

namespace Nri.PlayerClient.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommand(Action execute) : this(_ => execute()) { }
    public RelayCommand(Action<object?> execute) { _execute = execute; }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}

public static class PlayerDevelopmentLayoutVisualRules
{
    public const double WorkspaceWidth = 12000;
    public const double WorkspaceHeight = 12000;
    public const double DefaultNodeWidth = 172;
    public const double DefaultNodeHeight = 86;
    public const double RootNodeWidth = 220;
    public const double RootNodeHeight = 108;
    public const double AnchorNodeWidth = 192;
    public const double AnchorNodeHeight = 96;
    public const double CompactNodeWidth = 154;
    public const double CompactNodeHeight = 74;

    public static bool IsDiagnosticToken(params string[] values)
    {
        var text = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Contains("perf_")
            || text.Contains("performance")
            || text.Contains("gui acceptance token")
            || text.Contains("diagnostic")
            || text.Contains("dev_hex_hidden_node_0152")
            || text.Contains("dev_hex_missing_0152")
            || text.Contains("dev_missing_requirement")
            || text.Contains("0153");
    }

    public static void ApplyNodeSize(ClassNodeVisualVm node)
    {
        if (node == null) return;
        var type = (node.NodeTypeLabel ?? string.Empty).Trim().ToLowerInvariant();
        var id = (node.NodeId ?? string.Empty).Trim().ToLowerInvariant();
        if (id == "novice" || id == "magic_awakened")
        {
            node.NodeWidth = RootNodeWidth;
            node.NodeHeight = RootNodeHeight;
        }
        else if (type == "class" || type == "magic" || node.Ring <= 1)
        {
            node.NodeWidth = AnchorNodeWidth;
            node.NodeHeight = AnchorNodeHeight;
        }
        else if (type == "skill" || type == "passive")
        {
            node.NodeWidth = CompactNodeWidth;
            node.NodeHeight = CompactNodeHeight;
        }
        else
        {
            node.NodeWidth = DefaultNodeWidth;
            node.NodeHeight = DefaultNodeHeight;
        }
    }
}

public class CharacterListItemVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Age { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
    public string HealthText { get; set; } = "—";
    public string ArmorText { get; set; } = "—";
    public string ExperienceCoinsText { get; set; } = "—";
    public string BackstoryPreview { get; set; } = "—";
    public string OwnerDisplay { get; set; } = "—";
    public string GroupDisplay { get; set; } = "—";
    public string CharacterKindDisplay { get; set; } = "—";
    public string CharacterStatusDisplay { get; set; } = "—";
    public bool Archived { get; set; }
    public bool IsActive { get; set; }
    public string ActivityText => IsActive ? "Активен" : "Не активен";
    public string ArchiveText => Archived ? "В архиве" : string.Empty;
}

public sealed class GlobalSearchResultVm : ViewModelBase
{
    public string ResultId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string SourceCollection { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RouteKey { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? EntityId : Title;
    public string DisplayType => $"{Category} / {EntityType}";
    public string RouteSummary => $"{RouteKey} :: {EntityId}";
}

public class CurrencyRowVm
{
    public string CurrencyId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Abbrev { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFF";
    public long Amount { get; set; }
    public string AmountDisplay => IsEmptyState ? "нет данных" : Amount.ToString(CultureInfo.InvariantCulture);
    public string Kind { get; set; } = "money";
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsEmptyState { get; set; }
    public bool IsExperience => string.Equals(Kind, "experience", StringComparison.OrdinalIgnoreCase)
        || string.Equals(CurrencyId, CharacterCurrencyIds.XpCoin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(Code, "xp_coin", StringComparison.OrdinalIgnoreCase);
    public string AutomationId => $"PlayerCharacter_Currency_{NormalizeAutomationCode(Code)}_Amount";

    private static string NormalizeAutomationCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (string.Equals(normalized, "xpcoins", StringComparison.OrdinalIgnoreCase)) normalized = "xp_coin";
        normalized = Regex.Replace(normalized, @"[^a-z0-9_]+", "_");
        normalized = Regex.Replace(normalized, @"_+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }
}

public class StatRowVm
{
    public string AttributeId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string RangeText { get; set; } = string.Empty;
    public string AutomationScope { get; set; } = "Attribute";
    public ObservableCollection<StatRowVm> SubAttributes { get; } = new ObservableCollection<StatRowVm>();
    public string AttributeGroupAutomationId => $"PlayerCharacter_AttributeGroup_{NormalizeAutomationCode(Code)}";
    public string AutomationId => $"PlayerCharacter_{AutomationScope}_{NormalizeAutomationCode(Code)}_Value";

    private static string NormalizeAutomationCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (string.Equals(normalized, "intellect", StringComparison.OrdinalIgnoreCase)) normalized = "intelligence";
        normalized = Regex.Replace(normalized, @"[^a-z0-9_]+", "_");
        normalized = Regex.Replace(normalized, @"_+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }
}

public class SkillDisplayRowVm : ViewModelBase
{
    public string SkillCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Attribute { get; set; } = string.Empty;
    public int Rank { get; set; }
    public int ManualBonus { get; set; }
    public int AttributeBonus { get; set; }
    public string SubAttributeId { get; set; } = string.Empty;
    public string SubAttributeDisplayName { get; set; } = string.Empty;
    public int SubAttributeBonus { get; set; }
    public int TotalBonus { get; set; }
    public string Breakdown { get; set; } = string.Empty;
    public string TrainingState { get; set; } = string.Empty;
    public string Summary => $"{DisplayName} ({SkillCode})";
}

public class InventoryDisplayItemVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string QuantityDisplay { get; set; } = "нет данных";
    public bool IsEquipped { get; set; }
    public string Durability { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AutomationKey => NormalizeAutomationCode(string.IsNullOrWhiteSpace(Code) ? Id : Code);
    public string ItemAutomationId => $"PlayerCharacter_Inventory_Item_{AutomationKey}";
    public string QuantityAutomationId => $"PlayerCharacter_Inventory_Item_{AutomationKey}_Quantity";
    public string EquippedAutomationId => $"PlayerCharacter_Inventory_Item_{AutomationKey}_Equipped";

    private static string NormalizeAutomationCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9_]+", "_");
        normalized = Regex.Replace(normalized, @"_+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }
}

public class HoldingDisplayItemVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string OwnersDisplay { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public string StatusLabel => IsArchived ? "Архив" : "Активно";
    public string Preview => FirstNonEmpty(Description, Notes, "Нет описания");
    public string AutomationId => $"PlayerCharacter_Holding_Item_{Id}";
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public class ReputationRowVm
{
    public string Id { get; set; } = string.Empty;
    public string ScopeType { get; set; } = "Character";
    public string TargetType { get; set; } = "Other";
    public string TargetName { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public string Label => string.IsNullOrWhiteSpace(TargetName) ? "Без названия" : TargetName;
    public string StatusLabel => IsArchived ? "Архив" : "Активно";
    public string AutomationId => $"PlayerCharacter_Reputation_Item_{Id}";
    public string ScopeTypeLabel => ScopeType switch
    {
        "Character" => "Персонаж",
        "Session" => "Сессия",
        "Campaign" => "Кампания",
        _ => string.IsNullOrWhiteSpace(ScopeType) ? "—" : ScopeType
    };
    public string TargetTypeLabel => TargetType switch
    {
        "Other" => "Другое",
        "Character" => "Персонаж",
        "Faction" => "Фракция",
        "Organization" => "Организация",
        _ => string.IsNullOrWhiteSpace(TargetType) ? "—" : TargetType
    };
    public string ValueText => $"{Value} / 100";
    public System.Windows.Media.Brush BarBrush
    {
        get
        {
            var stops = Value switch
            {
                < 0 => new[] { ("#FF4A1D2A", 0.0), ("#FFFF6B6B", 1.0) },
                > 0 => new[] { ("#FF143826", 0.0), ("#FF43D397", 1.0) },
                _ => new[] { ("#FF172A46", 0.0), ("#FF61B1FF", 1.0) }
            };
            var brush = new System.Windows.Media.LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 0)
            };
            foreach (var stop in stops)
                brush.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(stop.Item1), stop.Item2));
            brush.Freeze();
            return brush;
        }
    }
    public string RelationshipLabel => Value switch
    {
        <= -75 => "Враждебно",
        <= -35 => "Недоверие",
        <= -1 => "Напряжённо",
        0 => "Нейтрально",
        <= 34 => "Знакомы",
        <= 74 => "Дружелюбно",
        _ => "Союзники"
    };
    public string NotesPreview => string.IsNullOrWhiteSpace(Notes) ? "—" : Notes;
    public double Percent => (Value + 100) / 200.0 * 100.0;
}

public class CompanionVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public string NameDisplay => string.IsNullOrWhiteSpace(Name) ? "Безымянный компаньон" : Name;
    public string SpeciesDisplay => string.IsNullOrWhiteSpace(Type) ? (string.IsNullOrWhiteSpace(Species) ? "Не указано" : Species) : Type;
    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? "Заметок нет." : Notes;
    public string StatusLabel => IsArchived ? "Архив" : "Активно";
    public string AutomationId => $"PlayerCharacter_Companion_Item_{Id}";
    public ObservableCollection<StatRowVm> StatsRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<StatRowVm> CoreStatRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<StatRowVm> AttributeStatRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<StatRowVm> DerivedStatRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<string> InventoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> HoldingsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SkillsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ClassRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> KnowledgeRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ResearchRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> CraftingRows { get; } = new ObservableCollection<string>();
}

public class PlayerLocalNoteVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string UserKey { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string UpdatedAtUtc { get; set; } = string.Empty;
    public string UpdatedAtLocalText { get; set; } = string.Empty;
    public string Preview => string.IsNullOrWhiteSpace(Text) ? "—" : Text;
}


public class ClassDirectionVm
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string AutomationId => string.IsNullOrWhiteSpace(Key) ? "PlayerDevelopment_Direction_Empty" : "PlayerDevelopment_Direction_" + Key;
}

public class DevelopmentHexagonVm
{
    public string HexagonId { get; set; } = string.Empty;
    public string HexagonType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CenterNodeId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string AutomationId => string.IsNullOrWhiteSpace(HexagonId) ? "PlayerDevelopment_Hexagon_Empty" : "PlayerDevelopment_Hexagon_" + HexagonId;
}

public class ClassBranchVm
{
    public string Key { get; set; } = string.Empty;
    public string DirectionKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DisplayStatus => PlayerDevelopmentGraphDisplay.ToReadableState(Status);
    public string AutomationId => string.IsNullOrWhiteSpace(Key) ? "PlayerDevelopment_Branch_Empty" : "PlayerDevelopment_Branch_" + Key;
}

public static class PlayerDevelopmentGraphDisplay
{
    private static readonly Dictionary<string, string> KnownTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DEV_HEX_NODE_0152_A"] = "служебный узел проверки A",
        ["DEV_HEX_NODE_0152_B"] = "служебный узел проверки B",
        ["DEV_HEX_HIDDEN_NODE_0152"] = "скрытый служебный узел",
        ["DEV_HEX_MISSING_0152"] = "недостающее служебное требование",
        ["dev_missing_requirement_01446"] = "недостающее служебное требование",
        ["dev_hex_purchase_node_01446"] = "узел покупки развития",
        ["dev_hex_node_01445_1"] = "направление развития 1",
        ["dev_hex_node_01445_2"] = "направление развития 2",
        ["dev_hex_branch_01445"] = "ветка развития",
        ["dev_hex_layout_branch_01447"] = "ветка раскладки развития",
        ["dev_hex_01446_branch"] = "ветка развития",
        ["dev_locked_node_01446"] = "закрепленный узел развития",
        ["visible_by_default"] = "видно игрокам",
        ["xp_coin"] = "монеты опыта",
        ["gold_coin"] = "золотая монета",
        ["silver_coin"] = "серебряная монета",
        ["bronze_coin"] = "бронзовая монета",
        ["iron_coin"] = "железная монета",
        ["platinum_coin"] = "платиновая монета",
        ["main_development_hexagon"] = "основной шестиугольник развития",
        ["magic_development_hexagon"] = "магический шестиугольник развития",
        ["large_development_hexagon_0154"] = "большое тестовое дерево развития",
        ["large0154_root"] = "корень большого дерева",
        ["development_hexagon"] = "шестиугольник развития"
    };

    public static string ToReadableNodeTitle(string title, string nodeId)
    {
        var raw = FirstNonEmpty(title, nodeId, "Узел развития");
        var cleaned = ToReadableText(raw);
        return IsTechnicalToken(cleaned) ? "Узел развития" : cleaned;
    }

    public static string ToReadableText(string value)
    {
        var text = value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        foreach (var pair in KnownTokens)
            text = Regex.Replace(text, Regex.Escape(pair.Key), pair.Value, RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"DEV_HEX_LINK[_A-Z0-9]*", "связь требования", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"perf_0153_node_(\d+)", "служебный узел нагрузки $1", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"dev\s+hex\s+direction\s+\d+", "направление развития", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"dev_hex_purchase_node_\d+", "узел покупки развития", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"dev_hex_node_\d+_\d+", "направление развития", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"dev_hex_(?:layout_)?branch_\d+", "ветка развития", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"dev_hex_\d+_branch", "ветка развития", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"dev_locked_node_\d+", "закрепленный узел развития", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"large0154_b(\d+)_l(\d+)_n(\d+)", "узел большого дерева $1-$2-$3", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"large0154_branch_(\d+)", "ветка большого дерева $1", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"large0154_root", "корень большого дерева", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"dev_[a-z0-9_]*", "узел развития", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"GUI acceptance token", "проверочный узел", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"Requires", "Требуется", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"development_hexagon[\w\.-]*", "шестиугольник развития", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"PLAYER_VISIBLE_AUDIO[\w\.-]*", "музыкальный трек", RegexOptions.IgnoreCase);
        return text.Trim();
    }

    public static string ToReadableType(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "class" => "Класс",
            "branch" => "Ветка",
            "skill" => "Навык",
            "specialization" => "Специализация",
            "profession" => "Профессия",
            "license" => "Лицензия",
            "combat_doctrine" => "Боевая доктрина",
            "passive" => "Пассивное развитие",
            "magic" => "Магия",
            "custom" => "Другое",
            "" => "Узел развития",
            _ => ToReadableText(value)
        };
    }

    public static string ToReadableState(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "available" => "Доступно",
            "unlocked" => "Доступно",
            "locked" => "Недоступно",
            "taken" => "Изучено",
            "completed" => "Изучено",
            "start" => "Старт",
            "" => "Статус не указан",
            _ => ToReadableText(value)
        };
    }

    public static string ToReadableCost(int cost, string currencyId)
    {
        var currency = (currencyId ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "xp_coin" => "монеты опыта",
            "gold_coin" => "золотая монета",
            "silver_coin" => "серебряная монета",
            "bronze_coin" => "бронзовая монета",
            "iron_coin" => "железная монета",
            "platinum_coin" => "платиновая монета",
            "mo" => "монеты опыта",
            "" => "монеты опыта",
            _ => ToReadableText(currencyId)
        };
        return $"{cost} {currency}";
    }

    public static string ToReadableRequirementList(string requiredNodeIds, Func<string, string> resolveNode)
    {
        var ids = (requiredNodeIds ?? string.Empty)
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(id => id.Trim())
            .Where(id => id.Length > 0)
            .Select(id => resolveNode(id))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        return ids.Count == 0 ? "Требуется: нет" : "Требуется: " + string.Join(", ", ids);
    }

    public static bool IsTechnicalToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("dev_", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("dev hex", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("PLAYER_VISIBLE_", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("development_hexagon", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("xp_coin", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
public class ClassEntryVm
{
    public string NodeId { get; set; } = string.Empty;
    public string HexagonId { get; set; } = string.Empty;
    public string HexagonName { get; set; } = string.Empty;
    public string NodeTypeLabel { get; set; } = string.Empty;
    public string DirectionKey { get; set; } = string.Empty;
    public string BranchKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequirementSummary { get; set; } = string.Empty;
    public string RewardSummary { get; set; } = string.Empty;
    public string RequiredNodeIds { get; set; } = string.Empty;
    public string LinkedClassId { get; set; } = string.Empty;
    public string CurrencyId { get; set; } = CharacterCurrencyIds.XpCoin;
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Ring { get; set; }
    public int Sector { get; set; }
    public int SortOrder { get; set; }
    public int LayoutVersion { get; set; }
    public int CostExperienceCoins { get; set; }
    public string CostText { get; set; } = "0 монеты опыта";
    public bool CanPurchase { get; set; }
    public bool RequiresRequest { get; set; }
    public string PositionText => $"Позиция: X {PositionX}, Y {PositionY}; кольцо {Ring}; сектор {Sector}";
    public string DirectionText => $"Направление: {FormatDirection(DirectionKey)}; ветка: {PlayerDevelopmentGraphDisplay.ToReadableText(BranchKey)}";
    public string RequirementsText => PlayerDevelopmentGraphDisplay.ToReadableRequirementList(
        RequiredNodeIds,
        id => PlayerDevelopmentGraphDisplay.ToReadableNodeTitle(string.Empty, id));
    public string DisplayTitle => PlayerDevelopmentGraphDisplay.ToReadableNodeTitle(Title, NodeId);
    public string DisplayStatus => PlayerDevelopmentGraphDisplay.ToReadableState(Status);
    public string FriendlyMetaText => $"{PlayerDevelopmentGraphDisplay.ToReadableType(NodeTypeLabel)} / {DisplayStatus} / {CostText}";
    public string FriendlyRequirementsText => string.IsNullOrWhiteSpace(RequirementSummary) ? "Требований нет." : PlayerDevelopmentGraphDisplay.ToReadableText(RequirementSummary);
    public string FriendlyUnlockText => string.IsNullOrWhiteSpace(LinkedClassId)
        ? PlayerDevelopmentGraphDisplay.ToReadableText(RewardSummary)
        : "Открывает класс: " + PlayerDevelopmentGraphDisplay.ToReadableText(LinkedClassId);
    public string AutomationId => string.IsNullOrWhiteSpace(NodeId) ? "PlayerDevelopment_Node_Empty" : "PlayerDevelopment_Node_" + NodeId;

    private static string FormatDirection(string key)
    {
        var readable = PlayerDevelopmentGraphDisplay.ToReadableText(key);
        return string.IsNullOrWhiteSpace(readable) ? "Направление" : readable;
    }
}

public class PublicProfileFieldVm
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class GameFeedItemVm
{
    public string Kind { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
}

public class ChatMessageRowVm
{
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public long SortTicks { get; set; }
}

public class ClassNodeVisualVm : ViewModelBase
{
    private bool _isSearchMatch;
    private bool _isFilteredOut;

    public string NodeId { get; set; } = string.Empty;
    public string HexagonId { get; set; } = string.Empty;
    public string HexagonName { get; set; } = string.Empty;
    public string NodeTypeLabel { get; set; } = string.Empty;
    public string DirectionKey { get; set; } = string.Empty;
    public string BranchKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = "Locked";
    public int CostExperienceCoins { get; set; }
    public string CostText { get; set; } = "0 монеты опыта";
    public string Summary { get; set; } = string.Empty;
    public string RequirementSummary { get; set; } = string.Empty;
    public string RewardSummary { get; set; } = string.Empty;
    public string RequiredNodeIds { get; set; } = string.Empty;
    public string LinkedClassId { get; set; } = string.Empty;
    public string CurrencyId { get; set; } = CharacterCurrencyIds.XpCoin;
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Ring { get; set; }
    public int Sector { get; set; }
    public int SortOrder { get; set; }
    public int LayoutVersion { get; set; }
    public bool CanPurchase { get; set; }
    public bool RequiresRequest { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double NodeWidth { get; set; } = PlayerDevelopmentLayoutVisualRules.DefaultNodeWidth;
    public double NodeHeight { get; set; } = PlayerDevelopmentLayoutVisualRules.DefaultNodeHeight;
    public string DisplayTitle => PlayerDevelopmentGraphDisplay.ToReadableNodeTitle(Title, NodeId);
    public string DisplayTypeLabel => PlayerDevelopmentGraphDisplay.ToReadableType(NodeTypeLabel);
    public string DisplayState => PlayerDevelopmentGraphDisplay.ToReadableState(State);
    public string AccessibleTitle => $"{DisplayTitle} ({DisplayState})";
    public string SearchText => string.Join(" ", new[] { NodeId, Title, NodeTypeLabel, State, DirectionKey, BranchKey, LinkedClassId }).Trim();
    public string LockReason => CanPurchase
        ? "Статус: доступно"
        : (State.IndexOf("Locked", StringComparison.OrdinalIgnoreCase) >= 0 || DisplayState.IndexOf("Недоступно", StringComparison.OrdinalIgnoreCase) >= 0)
            ? FirstNonEmpty(PlayerDevelopmentGraphDisplay.ToReadableText(RequirementSummary), "Статус: недоступно — не выполнено требование.")
            : $"Статус: {DisplayState}";
    public double VisualOpacity => IsFilteredOut ? 0.24 : 1.0;
    public System.Windows.Visibility VisualVisibility => IsFilteredOut ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    public string SearchBadgeText => IsSearchMatch ? "Найдено" : string.Empty;
    public bool IsSearchMatch
    {
        get => _isSearchMatch;
        set
        {
            if (_isSearchMatch == value) return;
            _isSearchMatch = value;
            Notify();
            Notify(nameof(SearchBadgeText));
        }
    }
    public bool IsFilteredOut
    {
        get => _isFilteredOut;
        set
        {
            if (_isFilteredOut == value) return;
            _isFilteredOut = value;
            Notify();
            Notify(nameof(VisualOpacity));
            Notify(nameof(VisualVisibility));
        }
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public class ClassNodeVisualLinkVm
{
    public string LinkId { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string SourceTitle { get; set; } = string.Empty;
    public string TargetTitle { get; set; } = string.Empty;
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string SourceDisplay => PlayerDevelopmentGraphDisplay.ToReadableNodeTitle(SourceTitle, SourceNodeId);
    public string TargetDisplay => PlayerDevelopmentGraphDisplay.ToReadableNodeTitle(TargetTitle, TargetNodeId);
    public string Label => $"{SourceDisplay} → {TargetDisplay}";
    public string DirectionText => $"{SourceDisplay} требуется для {TargetDisplay}";
    public string ArrowPoints => BuildArrowPoints(X1, Y1, X2, Y2);

    private static string BuildArrowPoints(double x1, double y1, double x2, double y2)
    {
        var angle = Math.Atan2(y2 - y1, x2 - x1);
        const double length = 13;
        const double spread = 0.55;
        var p1x = x2 - Math.Cos(angle - spread) * length;
        var p1y = y2 - Math.Sin(angle - spread) * length;
        var p2x = x2 - Math.Cos(angle + spread) * length;
        var p2y = y2 - Math.Sin(angle + spread) * length;
        return string.Format(CultureInfo.InvariantCulture, "{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1}", x2, y2, p1x, p1y, p2x, p2y);
    }
}

public sealed class PlayerDevelopmentCanonicalRootVm
{
    public string Label { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 260;
    public double Height { get; set; } = 150;
    public string AutomationId { get; set; } = "PlayerDevelopmentHexagonViewer_CenterRootHexagon";
    public string HexPoints => BuildHexPoints(Width, Height);

    internal static string BuildHexPoints(double width, double height)
    {
        var shoulder = Math.Max(18, width * 0.24);
        return string.Format(CultureInfo.InvariantCulture,
            "{0:F1},0 {1:F1},0 {2:F1},{3:F1} {1:F1},{4:F1} {0:F1},{4:F1} 0,{3:F1}",
            shoulder, width - shoulder, width, height / 2.0, height);
    }
}

public sealed class PlayerDevelopmentCanonicalDirectionVm : ViewModelBase
{
    private bool _isFocused;
    public string DirectionId { get; set; } = string.Empty;
    public int SideIndex { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AtmosphericName { get; set; } = string.Empty;
    public string FullDisplayName => string.IsNullOrWhiteSpace(AtmosphericName) ? DisplayName : $"{DisplayName} — {AtmosphericName}";
    public double AnchorX { get; set; }
    public double AnchorY { get; set; }
    public double AnchorWidth { get; set; } = 190;
    public double AnchorHeight { get; set; } = 86;
    public string AnchorHexPoints => PlayerDevelopmentCanonicalRootVm.BuildHexPoints(AnchorWidth, AnchorHeight);
    public string PlayerAnchorAutomationId => $"PlayerDevelopmentHexagonViewer_DirectionAnchor_{SideIndex}";
    public double VisualOpacity => IsFocused ? 1.0 : 0.82;
    public bool IsFocused { get => _isFocused; set { if (_isFocused == value) return; _isFocused = value; Notify(); Notify(nameof(VisualOpacity)); } }
}

public sealed class PlayerDevelopmentCanonicalLaneVm
{
    public string DirectionId { get; set; } = string.Empty;
    public int SideIndex { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string StrokeBrush { get; set; } = "#668BC5FF";
    public double StrokeThickness { get; set; } = 5;
    public double Opacity { get; set; } = 0.72;
}

public class PlayerCombatConditionVm : ViewModelBase
{
    public string ConditionDefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int StackCount { get; set; }
    public int RemainingRounds { get; set; }
    public bool IsPositive { get; set; }
    public bool IsNegative { get; set; }
    public string Summary => $"{DisplayName} x{StackCount} {Severity}";
}

public class PlayerCombatParticipantVm : ViewModelBase
{
    public string ParticipantId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public bool IsCurrentTurn { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefeated { get; set; }
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public int TemporaryHealth { get; set; }
    public int CurrentMorale { get; set; }
    public int MaxMorale { get; set; }
    public string VisibilityState { get; set; } = string.Empty;
    public ObservableCollection<PlayerCombatConditionVm> KnownConditions { get; } = new ObservableCollection<PlayerCombatConditionVm>();
    public string HealthText => MaxHealth > 0 ? $"{CurrentHealth}/{MaxHealth} (+{TemporaryHealth})" : "-";
    public string MoraleText => MaxMorale > 0 ? $"{CurrentMorale}/{MaxMorale}" : "-";
    public string TurnText => IsCurrentTurn ? "Текущий ход" : string.Empty;
}

public class PlayerCombatLogVm : ViewModelBase
{
    public string CreatedAtText { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RoundTurnText => $"R{RoundNumber}/T{TurnIndex}";
}

public class PlayerMainViewModel : ViewModelBase
{
    private readonly ClientSessionState _session = new ClientSessionState();
    private readonly ClientConfig _clientConfig;
    private readonly JsonTcpClient _client;
    private readonly CommandApi _api;
    private readonly DispatcherTimer _poller;
    private readonly IClientSyncEventDispatcher _syncDispatcher;
    private long _syncRevision;
    private bool _definitionsDirty;


    private string _connectionState = "Оффлайн";
    private bool _isAuthPopupOpen;
    private bool _isConnectionPopupOpen;
    private string _selectedMainTab = "MyCharacters";
    private string _activeCharacterId = string.Empty;
    private string _activeCharacterStatusText = "Активный персонаж не выбран.";
    private CharacterListItemVm? _selectedMyCharacter;
    private CompanionVm? _selectedCompanion;
    private long _experienceCoins;
    private bool _experienceCoinsLoaded;
    private string _selectedClassNodeId = string.Empty;
    private string _developmentStatusText = string.Empty;
    private string _selectedDevelopmentHexagonId = DevelopmentHexagonIds.Main;
    private string _developmentViewerFocusedDirectionKey = string.Empty;
    private double _developmentViewerZoom = 0.62;
    private double _developmentViewerViewportTranslateX;
    private double _developmentViewerViewportTranslateY;
    private readonly Dictionary<string, Dictionary<string, object>> _developmentViewerHexagonPayloads = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
    private string _developmentViewerSearchText = string.Empty;
    private int _developmentViewerSearchIndex = -1;
    private bool _developmentViewerShowLegend = true;
    private string _selectedClassDirectionKey = "strength_assault";
    private ClassBranchVm? _selectedClassBranch;
    private ClassEntryVm? _selectedClassEntry;
    private int _chatScrollRequestVersion;
    private int _lastInventoryRenderCount = -1;
    private bool? _lastInventoryPlaceholderHidden;
    private string _lastInventoryPayloadSignature = string.Empty;
    private InventoryDisplayItemVm? _selectedInventoryItem;
    private int _lastHoldingsRenderCount = -1;
    private bool? _lastHoldingsPlaceholderHidden;
    private int _lastHoldingsLoadedCount = -1;
    private HoldingDisplayItemVm? _selectedHoldingItem;
    private int _lastReputationRenderCount = -1;
    private bool? _lastReputationPlaceholderHidden;
    private int _lastReputationLoadedCount = -1;
    private int _lastCompanionsRenderCount = -1;
    private bool? _lastCompanionsPlaceholderHidden;
    private int _lastCompanionsLoadedCount = -1;
    private int _lastSkillsRenderCount = -1;
    private bool? _lastSkillsPlaceholderHidden;
    private PlayerCombatParticipantVm? _combatMyParticipant;
    private bool _combatIsLoading;
    private bool _combatIsMyTurn;
    private string _combatErrorMessage = string.Empty;
    private string _combatWarningMessage = string.Empty;
    private string _combatEncounterName = "Столкновение не выбрано";
    private string _combatEncounterStatus = string.Empty;
    private string _combatCurrentTurnText = string.Empty;
    private string _combatLastRefreshText = string.Empty;
    private string _selectedRequestRow = string.Empty;
    private readonly Dictionary<string, string> _requestRowIds = new Dictionary<string, string>(StringComparer.Ordinal);
    private string _selectedRequestRawTitle = string.Empty;
    private PlayerLocalNoteVm? _selectedLocalNote;
    private readonly List<PlayerLocalNoteVm> _localNotesStore = new List<PlayerLocalNoteVm>();
    private string _globalSearchQuery = string.Empty;
    private string _globalSearchCategoryFilter = "all";
    private string _globalSearchStatusText = "Поиск готов.";
    private GlobalSearchResultVm? _selectedGlobalSearchResult;
    public PlayerSceneMapViewModel SceneMap { get; }
    public PlayerWorldMapViewModel WorldMap { get; }
    public PlayerEngineeringViewModel Engineering { get; }
    public PlayerProductionViewModel Production { get; }
    public PlayerWorldCalendarViewModel WorldCalendar { get; }
    public PlayerRealScheduleViewModel RealSchedule { get; }
    public PlayerRoomInteriorViewModel RoomInterior { get; }
    public PlayerCurrentSessionViewModel CurrentSession { get; }
    public PlayerActiveGroupViewModel ActiveGroup { get; }
    public PlayerEventJournalViewModel EventJournal { get; }
    public PlayerFunctionalDashboardViewModel FunctionalDashboard { get; }
    public PlayerProposalCenterViewModel ProposalCenter { get; }

    public PlayerMainViewModel()
    {
        _clientConfig = App.ClientConfig;
        _client = new JsonTcpClient(_clientConfig, _session);
        _api = new CommandApi(_client);
        FunctionalDashboard = new PlayerFunctionalDashboardViewModel(_api);
        CurrentSession = new PlayerCurrentSessionViewModel(_api, () => ActiveCharacterId);
        ActiveGroup = new PlayerActiveGroupViewModel(_api, () => ActiveCharacterId);
        SceneMap = new PlayerSceneMapViewModel(_api, () => ActiveCharacterId);
        WorldMap = new PlayerWorldMapViewModel(_api, () => ActiveCharacterId);
        Engineering = new PlayerEngineeringViewModel(_api, () => ActiveCharacterId);
        Production = new PlayerProductionViewModel(_api, () => ActiveCharacterId);
        WorldCalendar = new PlayerWorldCalendarViewModel(_api, () => ActiveCharacterId);
        RealSchedule = new PlayerRealScheduleViewModel(_api);
        RoomInterior = new PlayerRoomInteriorViewModel(_api, () => ActiveCharacterId);
        EventJournal = new PlayerEventJournalViewModel(_api, () => ActiveCharacterId);
        ProposalCenter = new PlayerProposalCenterViewModel(_api, () => ActiveCharacterId);
        ClientLogService.Instance.Info("PlayerMainViewModel initialized");

        ToggleAuthPopupCommand = new RelayCommand(() =>
        {
            IsAuthPopupOpen = !IsAuthPopupOpen;
            if (IsAuthPopupOpen)
            {
                ClientLogService.Instance.Info("ui.password.change.opened");
            }
        });
        ToggleConnectionPopupCommand = new RelayCommand(() => IsConnectionPopupOpen = !IsConnectionPopupOpen);
        LoginCommand = new RelayCommand(Login);
        RegisterCommand = new RelayCommand(Register);
        ChangePasswordCommand = new RelayCommand(ChangePassword);
        RefreshCommand = new RelayCommand(RefreshAll);
        GlobalSearchCommand = new RelayCommand(RunGlobalSearch);
        GlobalSearchOpenCommand = new RelayCommand(OpenGlobalSearchResult);

        LoadCharacterHubCommand = new RelayCommand(LoadSelectedCharacterHub);
        SetActiveCharacterCommand = new RelayCommand(SetSelectedCharacterActive);
        CreateCharacterCommand = new RelayCommand(CreateCharacter);
        CreateDiceRequestCommand = new RelayCommand(CreateDiceRequest);
        CreatePlayerRequestCommand = new RelayCommand(CreatePlayerRequest);
        CancelRequestCommand = new RelayCommand(CancelRequest);
        ResubmitRequestCommand = new RelayCommand(ResubmitRequest);
        RefreshRequestsCommand = new RelayCommand(RefreshDiceAndRequests);

        ChatSendCommand = new RelayCommand(SendChat);
        BottomRefreshCommand = new RelayCommand(RefreshBottomPanel);

        AudioRefreshCommand = new RelayCommand(RefreshAudioState);
        AudioApplyLocalSettingsCommand = new RelayCommand(ApplyAudioLocalSettings);

        VisibilityLoadCommand = new RelayCommand(LoadVisibility);
        VisibilitySaveCommand = new RelayCommand(SaveVisibility);
        PublicCharacterLoadCommand = new RelayCommand(LoadPublicCharacter);

        NotesRefreshCommand = new RelayCommand(RefreshNotes);
        NotesCreateCommand = new RelayCommand(CreateNote);
        NotesArchiveCommand = new RelayCommand(ArchiveNote);
        LocalNoteAddCommand = new RelayCommand(AddLocalNote);
        LocalNoteSaveCommand = new RelayCommand(SaveSelectedLocalNote);
        LocalNoteDeleteCommand = new RelayCommand(DeleteSelectedLocalNote);
        LocalNoteClearCommand = new RelayCommand(ClearLocalNoteEditor);
        CombatRefreshSnapshotCommand = new RelayCommand(RefreshCombatSnapshot);
        CombatRefreshFeedCommand = new RelayCommand(RefreshCombatFeed);
        CombatClearErrorCommand = new RelayCommand(() =>
        {
            CombatErrorMessage = string.Empty;
            CombatWarningMessage = string.Empty;
        });

        AcquireClassNodeCommand = new RelayCommand(AcquireClassNode);
        BuySelectedClassNodeCommand = new RelayCommand(BuySelectedClassNode);
        RequestUnlockNodeCommand = new RelayCommand(RequestUnlockNode);
        FitToViewDevelopmentHexagonCommand = new RelayCommand(FitToViewDevelopmentHexagon);
        DevelopmentViewerSearchClearCommand = new RelayCommand(ClearDevelopmentViewerSearch);
        DevelopmentViewerSearchNextCommand = new RelayCommand(() => SelectDevelopmentViewerSearchResult(1));
        DevelopmentViewerSearchPreviousCommand = new RelayCommand(() => SelectDevelopmentViewerSearchResult(-1));
        AcquireSkillCommand = new RelayCommand(AcquireSkill);
        SkillCheckRollCommand = new RelayCommand(RollSelectedSkillCheck);
        ConnectToServerCommand = new RelayCommand(ConnectToServer);
        ApplyConnectionSettingsCommand = new RelayCommand(ApplyConnectionSettings);
        ResetConnectionDefaultsCommand = new RelayCommand(ResetConnectionDefaults);
        UseLastConnectionCommand = new RelayCommand(UseSavedConnectionSettings);

        _poller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _poller.Tick += (_, _) => PollRefresh();
        _syncDispatcher = new ClientSyncEventDispatcher(this);

        LoadConnectionSettings();
        InitializeClassVisualLayout();
        InitializeDefaultPublicProfile();
        InitializeDefaultCharacterScaffolding();
        LoadLocalAudioSettings();
        LoadLocalNotesStore();
        RefreshLocalNotesForCurrentCharacter();
        RefreshConnectionSummary();
        ClientLogService.Instance.Info("chat.input.wrap enabled=true");
        ClientLogService.Instance.Info("dice.layout.movedUnderChat=true");
    }

    public string LoginText { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string OldPasswordText { get; set; } = string.Empty;
    public string NewPasswordText { get; set; } = string.Empty;
    public string PlayerDisplayName { get; set; } = "Гость";
    public string SessionSummary { get; set; } = "Сессия не подключена";

    public bool IsAuthPopupOpen { get => _isAuthPopupOpen; set { _isAuthPopupOpen = value; Notify(); } }
    public bool IsConnectionPopupOpen { get => _isConnectionPopupOpen; set { _isConnectionPopupOpen = value; Notify(); } }
    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); Notify(nameof(IsOnline)); Notify(nameof(IsAuthenticated)); } }
    public bool IsOnline => string.Equals(ConnectionState, "Онлайн", StringComparison.OrdinalIgnoreCase);
    public bool IsAuthenticated => IsOnline && !string.Equals(PlayerDisplayName, "Гость", StringComparison.OrdinalIgnoreCase);

    public string SelectedMainTab { get => _selectedMainTab; set { _selectedMainTab = value; Notify(); } }
    public string SelectedCharacterId { get; set; } = string.Empty;
    public string ActiveCharacterId { get => _activeCharacterId; set { _activeCharacterId = value; Notify(); Notify(nameof(HasActiveCharacter)); } }
    public bool HasActiveCharacter => !string.IsNullOrWhiteSpace(ActiveCharacterId);
    public string ActiveCharacterStatusText { get => _activeCharacterStatusText; set { _activeCharacterStatusText = value; Notify(); } }
    public CharacterListItemVm? SelectedMyCharacter
    {
        get => _selectedMyCharacter;
        set
        {
            _selectedMyCharacter = value;
            if (value != null)
            {
                SelectedCharacterId = value.Id;
                Notify(nameof(SelectedCharacterId));
                RefreshLocalNotesForCurrentCharacter();
            }
            Notify();
            Notify(nameof(CanSetActiveCharacter));
        }
    }
    public bool HasMyCharacters => MyCharacters.Count > 0;
    public bool CanSetActiveCharacter => IsAuthenticated && SelectedMyCharacter != null;
    public int ChatScrollRequestVersion { get => _chatScrollRequestVersion; private set { _chatScrollRequestVersion = value; Notify(); } }
    public string PublicViewCharacterId { get; set; } = string.Empty;
    public string ServerHostInput { get; set; } = "127.0.0.1";
    public string ServerPortInput { get; set; } = "4600";
    public string LastServerHost { get; set; } = "127.0.0.1";
    public int LastServerPort { get; set; } = 4600;
    public string ConnectionStatusDetail { get; set; } = "Не подключено";
    public string ConnectedEndpointDisplay => $"{ServerHostInput}:{ServerPortInput}";
    public string SelectedClassDirectionKey
    {
        get => _selectedClassDirectionKey;
        set
        {
            if (_selectedClassDirectionKey == value) return;
            _selectedClassDirectionKey = value;
            Notify();
            RebuildClassNavigation();
        }
    }

    public string CharacterName { get; set; } = string.Empty;
    public string CharacterRace { get; set; } = string.Empty;
    public string CharacterAge { get; set; } = string.Empty;
    public string CharacterHeight { get; set; } = string.Empty;
    public string CharacterDescription { get; set; } = string.Empty;
    public string CharacterBackstory { get; set; } = string.Empty;
    public long ExperienceCoins
    {
        get => _experienceCoins;
        set
        {
            _experienceCoins = value;
            _experienceCoinsLoaded = true;
            Notify();
            Notify(nameof(ExperienceCoinsDisplay));
        }
    }

    public string ExperienceCoinsDisplay => _experienceCoinsLoaded ? ExperienceCoins.ToString(CultureInfo.InvariantCulture) : "нет данных";
    public string CreateCharacterName { get; set; } = string.Empty;
    public string CreateCharacterRace { get; set; } = string.Empty;
    public string CreateCharacterBackstory { get; set; } = string.Empty;

    public string CharacterNameDisplay => string.IsNullOrWhiteSpace(CharacterName) ? "Без имени" : CharacterName;
    public string CharacterRaceDisplay => string.IsNullOrWhiteSpace(CharacterRace) ? "Не указано" : CharacterRace;
    public string CharacterAgeDisplay => string.IsNullOrWhiteSpace(CharacterAge) ? "нет данных" : CharacterAge;
    public string CharacterHeightDisplay => string.IsNullOrWhiteSpace(CharacterHeight) ? "нет данных" : CharacterHeight;
    public string CharacterBackstoryDisplay => string.IsNullOrWhiteSpace(CharacterBackstory) ? "Предыстория не указана." : CharacterBackstory;
    public string CharacterVisibilityDisplay => $"Видимость: описание скрыто={VisHideDescription}; предыстория скрыта={VisHideBackstory}; характеристики скрыты={VisHideStats}; репутация скрыта={VisHideReputation}";
    public string CharacterOwnerDisplay { get; set; } = "—";
    public string CharacterControllerDisplay { get; set; } = "—";
    public string CharacterGroupDisplay { get; set; } = "—";
    public string CharacterKindDisplay { get; set; } = "—";
    public string CharacterStatusDisplay { get; set; } = "—";

    public string CharacterOwnershipReadOnlySummary =>
        $"Владелец: {FirstNonEmpty(CharacterOwnerDisplay, "—")}; группа: {FirstNonEmpty(CharacterGroupDisplay, "—")}; тип: {FirstNonEmpty(CharacterKindDisplay, "—")}; статус: {FirstNonEmpty(CharacterStatusDisplay, "—")}";

    public bool VisHideDescription { get; set; }
    public bool VisHideBackstory { get; set; }
    public bool VisHideStats { get; set; }
    public bool VisHideReputation { get; set; }

    public int DiceCount { get; set; } = 1;
    public int DiceFaces { get; set; } = 20;
    public int DiceModifier { get; set; }
    public string DiceVisibilityInput { get; set; } = "Общее";
    public string DiceModeInput { get; set; } = "Обычный";
    public string DiceDescriptionInput { get; set; } = string.Empty;
    public string SelectedRequestId { get; set; } = string.Empty;
    public string PlayerRequestTypeInput { get; set; } = "general";
    public string PlayerRequestTitleInput { get; set; } = string.Empty;
    public string PlayerRequestDescriptionInput { get; set; } = string.Empty;
    public string PlayerRequestReasonInput { get; set; } = string.Empty;
    public string PlayerRequestPriorityInput { get; set; } = "normal";
    public string PlayerRequestStatusFilterInput { get; set; } = "all";
    public string[] PlayerRequestTypeOptions { get; } = { "generic_action", "development_unlock", "character_change", "item_request", "rules_question", "scene_action", "research", "crafting", "purchase" };
    public string[] PlayerRequestPriorityOptions { get; } = { "normal", "low", "high", "urgent" };
    public string[] PlayerRequestStatusFilterOptions { get; } = { "all", "submitted", "in_review", "changes_requested", "approved", "rejected", "cancelled" };
    public string SelectedRequestTitle { get; set; } = "Заявка не выбрана.";
    public string SelectedRequestStatus { get; set; } = string.Empty;
    public string SelectedRequestActors { get; set; } = "Участники: —";
    public string SelectedRequestDecision { get; set; } = string.Empty;
    public string SelectedRequestDetails { get; set; } = string.Empty;
    public string SelectedRequestRow
    {
        get => _selectedRequestRow;
        set
        {
            _selectedRequestRow = value ?? string.Empty;
            SelectedRequestId = ParseRequestIdFromRow(_selectedRequestRow);
            ApplySelectedRequestDetailsFromRow(_selectedRequestRow);
            Notify(nameof(SelectedRequestRow));
            Notify(nameof(SelectedRequestId));
        }
    }

    public string ChatSessionId { get; set; } = "default";
    public string ChatTypeInput { get; set; } = "Общее";
    public string ChatTextInput { get; set; } = string.Empty;

    public string AudioSessionId { get; set; } = "default";
    public string AudioStateText { get; set; } = string.Empty;
    public string AudioCurrentTrackTitle { get; set; } = "Трек не выбран";
    public string AudioCurrentCategory { get; set; } = "—";
    public string AudioPlaybackStateText { get; set; } = "—";
    public string AudioStatusText { get; set; } = "Музыка готова к обновлению.";
    public ObservableCollection<string> AudioVisibleTrackRows { get; } = new ObservableCollection<string>();
    public double LocalVolume { get; set; } = 0.7;
    public bool LocalMuted { get; set; }

    public string NoteSessionId { get; set; } = "default";
    public string NoteTargetType { get; set; } = "character";
    public string NoteTargetId { get; set; } = string.Empty;
    public string NoteTitle { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public string NoteVisibility { get; set; } = "Personal";
    public string SelectedNoteId { get; set; } = string.Empty;
    public string LocalNoteTitle { get; set; } = string.Empty;
    public string LocalNoteText { get; set; } = string.Empty;
    public string LocalNoteStatusText { get; set; } = "Локальных заметок пока нет.";

    public string CombatEncounterId { get; set; } = string.Empty;
    public string CombatParticipantId { get; set; } = string.Empty;
    public string CombatCharacterIdInput { get; set; } = string.Empty;
    public string CombatEncounterName { get => _combatEncounterName; set { _combatEncounterName = value; Notify(); } }
    public string CombatEncounterStatus { get => _combatEncounterStatus; set { _combatEncounterStatus = value; Notify(); } }
    public string CombatCurrentTurnText { get => _combatCurrentTurnText; set { _combatCurrentTurnText = value; Notify(); } }
    public string CombatLastRefreshText { get => _combatLastRefreshText; set { _combatLastRefreshText = value; Notify(); } }
    public string CombatErrorMessage { get => _combatErrorMessage; set { _combatErrorMessage = value; Notify(); Notify(nameof(HasCombatError)); } }
    public string CombatWarningMessage { get => _combatWarningMessage; set { _combatWarningMessage = value; Notify(); Notify(nameof(HasCombatWarning)); } }
    public bool CombatIsLoading { get => _combatIsLoading; set { _combatIsLoading = value; Notify(); } }
    public bool CombatIsMyTurn { get => _combatIsMyTurn; set { _combatIsMyTurn = value; Notify(); Notify(nameof(CombatTurnBanner)); } }
    public bool HasCombatError => !string.IsNullOrWhiteSpace(CombatErrorMessage);
    public bool HasCombatWarning => !string.IsNullOrWhiteSpace(CombatWarningMessage);
    public string CombatTurnBanner => CombatIsMyTurn ? " " : " ";
    public PlayerCombatParticipantVm? CombatMyParticipant { get => _combatMyParticipant; set { _combatMyParticipant = value; Notify(); Notify(nameof(HasCombatMyParticipant)); } }
    public bool HasCombatMyParticipant => CombatMyParticipant != null;

    public string SelectedClassNodeId
    {
        get => _selectedClassNodeId;
        set { _selectedClassNodeId = value ?? string.Empty; Notify(); }
    }
    public string SelectedSkillId { get; set; } = string.Empty;

    public ObservableCollection<CharacterListItemVm> MyCharacters { get; } = new ObservableCollection<CharacterListItemVm>();
    public ObservableCollection<StatRowVm> StatsRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<StatRowVm> CoreStatRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<StatRowVm> AttributeStatRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<StatRowVm> DerivedStatRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<CurrencyRowVm> MoneyRows { get; } = new ObservableCollection<CurrencyRowVm>();
    public ObservableCollection<string> CharacterKnowledgeRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> CharacterResearchRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> CharacterCraftingRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> InventoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<InventoryDisplayItemVm> InventoryItems { get; } = new ObservableCollection<InventoryDisplayItemVm>();
    public InventoryDisplayItemVm? SelectedInventoryItem
    {
        get => _selectedInventoryItem;
        set { _selectedInventoryItem = value; Notify(); }
    }
    public ObservableCollection<string> HoldingsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<HoldingDisplayItemVm> HoldingsItems { get; } = new ObservableCollection<HoldingDisplayItemVm>();
    public HoldingDisplayItemVm? SelectedHoldingItem
    {
        get => _selectedHoldingItem;
        set { _selectedHoldingItem = value; Notify(); }
    }
    public ObservableCollection<ReputationRowVm> ReputationRows { get; } = new ObservableCollection<ReputationRowVm>();
    public ObservableCollection<CompanionVm> Companions { get; } = new ObservableCollection<CompanionVm>();
    public CompanionVm? SelectedCompanion
    {
        get => _selectedCompanion;
        set { _selectedCompanion = value; Notify(); }
    }

    public ObservableCollection<SkillDisplayRowVm> SkillRows { get; } = new ObservableCollection<SkillDisplayRowVm>();
    public ObservableCollection<string> SkillCatalogRows { get; } = new ObservableCollection<string>();
    private SkillDisplayRowVm? _selectedSkillRow;
    public SkillDisplayRowVm? SelectedSkillRow
    {
        get => _selectedSkillRow;
        set
        {
            _selectedSkillRow = value;
            if (value != null) SelectedSkillId = value.SkillCode;
            Notify();
            Notify(nameof(SelectedSkillId));
            Notify(nameof(SelectedSkillSummary));
        }
    }
    public string SelectedSkillSummary => SelectedSkillRow == null ? "Навык не выбран." : $"{SelectedSkillRow.DisplayName}: бонус {SelectedSkillRow.TotalBonus}; {SelectedSkillRow.Breakdown}";
    public ObservableCollection<DevelopmentHexagonVm> DevelopmentHexagons { get; } = new ObservableCollection<DevelopmentHexagonVm>();
    public string SelectedDevelopmentHexagonId
    {
        get => _selectedDevelopmentHexagonId;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? DevelopmentHexagonIds.Main : value;
            if (string.Equals(_selectedDevelopmentHexagonId, normalized, StringComparison.OrdinalIgnoreCase)) return;
            _selectedDevelopmentHexagonId = normalized;
            Notify();
            _developmentViewerSearchIndex = -1;
            RebuildClassNavigation();
            Notify(nameof(SelectedDevelopmentHexagonDisplay));
            Notify(nameof(DevelopmentTreeModeOverlayText));
            Notify(nameof(VisibleDevelopmentCanvasNodes));
            RebuildDevelopmentCanvasLinks();
            ApplyDevelopmentViewerSearch();
            RebuildDevelopmentViewerCanonicalOverlay();
        }
    }
    public string SelectedDevelopmentHexagonDisplay => DevelopmentHexagons.FirstOrDefault(h => string.Equals(h.HexagonId, SelectedDevelopmentHexagonId, StringComparison.OrdinalIgnoreCase))?.Name ?? "Основной шестиугольник развития";
    public string DevelopmentTreeModeOverlayText
    {
        get
        {
            var mode = "Канонический шестиугольник";
            var nodes = VisibleDevelopmentCanvasNodes.ToList();
            var visibleWorking = nodes.Count(node => !node.IsFilteredOut && !PlayerDevelopmentLayoutVisualRules.IsDiagnosticToken(node.NodeId, node.Title, node.NodeTypeLabel, node.BranchKey, node.DirectionKey));
            var diagnostic = nodes.Count(node => PlayerDevelopmentLayoutVisualRules.IsDiagnosticToken(node.NodeId, node.Title, node.NodeTypeLabel, node.BranchKey, node.DirectionKey));
            var rootLabel = ResolvePlayerDevelopmentCanonicalRootLabel(
                SelectedDevelopmentHexagonId,
                FindPlayerDevelopmentCanonicalRootNode(SelectedDevelopmentHexagonId));
            return $"Дерево: {PlayerDevelopmentGraphDisplay.ToReadableText(SelectedDevelopmentHexagonDisplay)}; Ключ: {SelectedDevelopmentHexagonId}; Режим: {mode}; Корень: {rootLabel}; Узлов: {visibleWorking}; Связей: {DevelopmentCanvasLinks.Count}; Направлений: {DevelopmentViewerCanonicalDirections.Count}; Линий: {DevelopmentViewerCanonicalLanes.Count}; Диагностических узлов: {diagnostic}";
        }
    }
    public double DevelopmentViewerWorkspaceWidth => PlayerDevelopmentLayoutVisualRules.WorkspaceWidth;
    public double DevelopmentViewerWorkspaceHeight => PlayerDevelopmentLayoutVisualRules.WorkspaceHeight;
    public double DevelopmentViewerViewportTranslateX
    {
        get => _developmentViewerViewportTranslateX;
        private set
        {
            if (Math.Abs(_developmentViewerViewportTranslateX - value) < 0.01) return;
            _developmentViewerViewportTranslateX = value;
            Notify();
        }
    }
    public double DevelopmentViewerViewportTranslateY
    {
        get => _developmentViewerViewportTranslateY;
        private set
        {
            if (Math.Abs(_developmentViewerViewportTranslateY - value) < 0.01) return;
            _developmentViewerViewportTranslateY = value;
            Notify();
        }
    }
    public double DevelopmentViewerZoom
    {
        get => _developmentViewerZoom;
        set
        {
            var normalized = Math.Max(0.05, Math.Min(1.35, value));
            if (Math.Abs(_developmentViewerZoom - normalized) < 0.01) return;
            _developmentViewerZoom = normalized;
            Notify();
            Notify(nameof(DevelopmentViewerZoomText));
        }
    }
    public string DevelopmentViewerZoomText => $"{DevelopmentViewerZoom:P0}";
    public bool DevelopmentViewerShowLegend
    {
        get => _developmentViewerShowLegend;
        set
        {
            if (_developmentViewerShowLegend == value) return;
            _developmentViewerShowLegend = value;
            Notify();
            Notify(nameof(DevelopmentViewerLegendVisibility));
        }
    }
    public System.Windows.Visibility DevelopmentViewerLegendVisibility => DevelopmentViewerShowLegend ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public System.Windows.Visibility DevelopmentViewerCanonicalLayerVisibility => System.Windows.Visibility.Visible;
    public string DevelopmentViewerModeText => "Канонический шестиугольник";
    public string DevelopmentViewerFocusedDirectionKey
    {
        get => _developmentViewerFocusedDirectionKey;
        set
        {
            var normalized = value ?? string.Empty;
            if (_developmentViewerFocusedDirectionKey == normalized) return;
            _developmentViewerFocusedDirectionKey = normalized;
            Notify();
            RebuildDevelopmentViewerCanonicalOverlay();
        }
    }
    public string DevelopmentViewerSearchText
    {
        get => _developmentViewerSearchText;
        set
        {
            if (_developmentViewerSearchText == (value ?? string.Empty)) return;
            _developmentViewerSearchText = value ?? string.Empty;
            _developmentViewerSearchIndex = -1;
            Notify();
            ApplyDevelopmentViewerSearch();
        }
    }
    public string DevelopmentViewerSearchResultCountText
    {
        get
        {
            var visible = VisibleDevelopmentCanvasNodes.ToList();
            var matches = string.IsNullOrWhiteSpace(DevelopmentViewerSearchText)
                ? visible.Count
                : visible.Count(node => node.IsSearchMatch && !node.IsFilteredOut);
            return string.IsNullOrWhiteSpace(DevelopmentViewerSearchText)
                ? $"Узлов: {visible.Count}"
                : $"Найдено: {matches} / {visible.Count}";
        }
    }
    public string DevelopmentViewerLinkDirectionText => DevelopmentCanvasLinks.Count == 0
        ? "Связи не показаны"
        : "Направление связей: требование → открываемый узел.";
    public string DevelopmentViewerPurchaseExplanation => SelectedClassEntry == null
        ? "Выберите узел, чтобы увидеть условия покупки."
        : SelectedClassEntry.CanPurchase
            ? $"Узел можно купить за {SelectedClassEntry.CostText}."
            : SelectedClassEntry.RequiresRequest
                ? "Для открытия нужен запрос GM."
                : $"Покупка недоступна: {SelectedClassEntry.FriendlyRequirementsText}";
    public ObservableCollection<ClassNodeVisualVm> ClassNodes { get; } = new ObservableCollection<ClassNodeVisualVm>();
    public ObservableCollection<ClassNodeVisualLinkVm> DevelopmentCanvasLinks { get; } = new ObservableCollection<ClassNodeVisualLinkVm>();
    public ObservableCollection<PlayerDevelopmentCanonicalRootVm> DevelopmentViewerCanonicalRoots { get; } = new ObservableCollection<PlayerDevelopmentCanonicalRootVm>();
    public ObservableCollection<PlayerDevelopmentCanonicalDirectionVm> DevelopmentViewerCanonicalDirections { get; } = new ObservableCollection<PlayerDevelopmentCanonicalDirectionVm>();
    public ObservableCollection<PlayerDevelopmentCanonicalLaneVm> DevelopmentViewerCanonicalLanes { get; } = new ObservableCollection<PlayerDevelopmentCanonicalLaneVm>();
    public IEnumerable<ClassNodeVisualVm> VisibleDevelopmentCanvasNodes => ClassNodes
        .Where(node => string.Equals(string.IsNullOrWhiteSpace(node.HexagonId) ? DevelopmentHexagonIds.Main : node.HexagonId, SelectedDevelopmentHexagonId, StringComparison.OrdinalIgnoreCase))
        .OrderBy(node => node.SortOrder)
        .ThenBy(node => node.PositionY)
        .ThenBy(node => node.PositionX);
    public ObservableCollection<ClassDirectionVm> ClassDirections { get; } = new ObservableCollection<ClassDirectionVm>();
    public ObservableCollection<ClassBranchVm> ClassBranches { get; } = new ObservableCollection<ClassBranchVm>();
    public ObservableCollection<ClassEntryVm> ClassEntries { get; } = new ObservableCollection<ClassEntryVm>();
    public ClassBranchVm? SelectedClassBranch
    {
        get => _selectedClassBranch;
        set
        {
            _selectedClassBranch = value;
            Notify();
            RebuildClassEntries();
        }
    }
    public ClassEntryVm? SelectedClassEntry
    {
        get => _selectedClassEntry;
        set
        {
            _selectedClassEntry = value;
            if (value != null)
            {
                SelectedClassNodeId = value.NodeId;
            }
            Notify();
            NotifyClassDetail();
        }
    }

    public ObservableCollection<string> ChatRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<ChatMessageRowVm> ChatMessageRows { get; } = new ObservableCollection<ChatMessageRowVm>();
    public ObservableCollection<ChatMessageRowVm> MergedChatRows { get; } = new ObservableCollection<ChatMessageRowVm>();
    public ObservableCollection<ChatMessageRowVm> DiceMessageRows { get; } = new ObservableCollection<ChatMessageRowVm>();
    public ObservableCollection<string> EventRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> DiceFeedRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> RequestRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SessionStateRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<GameFeedItemVm> GameFeedRows { get; } = new ObservableCollection<GameFeedItemVm>();

    public ObservableCollection<string> PublicCharacterRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<PublicProfileFieldVm> PublicProfileIdentityRows { get; } = new ObservableCollection<PublicProfileFieldVm>();
    public ObservableCollection<PublicProfileFieldVm> PublicProfileSummaryRows { get; } = new ObservableCollection<PublicProfileFieldVm>();
    public ObservableCollection<string> PublicProfileHiddenRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> NoteRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> AdminNoteRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<PlayerLocalNoteVm> LocalNotes { get; } = new ObservableCollection<PlayerLocalNoteVm>();
    public PlayerLocalNoteVm? SelectedLocalNote
    {
        get => _selectedLocalNote;
        set
        {
            _selectedLocalNote = value;
            if (value != null)
            {
                LocalNoteTitle = value.Title;
                LocalNoteText = value.Text;
                Notify(nameof(LocalNoteTitle));
                Notify(nameof(LocalNoteText));
            }
            Notify();
        }
    }
    public ObservableCollection<PlayerCombatParticipantVm> CombatParticipants { get; } = new ObservableCollection<PlayerCombatParticipantVm>();
    public ObservableCollection<PlayerCombatLogVm> CombatPublicLog { get; } = new ObservableCollection<PlayerCombatLogVm>();
    public ObservableCollection<PlayerCombatConditionVm> CombatKnownConditions { get; } = new ObservableCollection<PlayerCombatConditionVm>();

    public ObservableCollection<string> DiceVisibilityOptions { get; } = new ObservableCollection<string> { "Публично", "Только GM", "Скрыто" };
    public ObservableCollection<string> DiceModeOptions { get; } = new ObservableCollection<string> { "Обычный", "Проверочный" };
    public ObservableCollection<string> ChatTypeOptions { get; } = new ObservableCollection<string> { "Обычное", "Действие", "Вопрос GM" };
    public ObservableCollection<string> NoteTargetTypeOptions { get; } = new ObservableCollection<string> { "character", "session", "campaign" };
    public ObservableCollection<string> NoteVisibilityOptions { get; } = new ObservableCollection<string> { "Personal", "SharedWithOwner", "SessionShared" };

    public string SelectedClassDirectionDisplay => FormatDevelopmentDirectionLabel(SelectedClassDirectionKey);
    public string SelectedClassBranchTitle => SelectedClassBranch?.Title ?? "Ветка не выбрана";
    public string SelectedClassBranchSummary => SelectedClassBranch?.Summary ?? "Выберите направление развития персонажа.";
    public string SelectedClassEntryTitle => SelectedClassEntry?.DisplayTitle ?? "Узел не выбран";
    public string SelectedClassEntrySummary => SelectedClassEntry?.Summary ?? "Выберите узел развития, чтобы увидеть описание.";
    public string SelectedClassEntryState => SelectedClassEntry?.Status ?? "Не выбрано";
    public string SelectedClassEntryRequirements => SelectedClassEntry == null ? "Требования появятся после выбора узла." : SelectedClassEntry.FriendlyRequirementsText;
    public string SelectedClassEntryReward => SelectedClassEntry == null ? string.Empty : SelectedClassEntry.RewardSummary;
    public string SelectedClassEntryCost => SelectedClassEntry?.CostText ?? "—";
    public string SelectedClassEntryPosition => SelectedClassEntry?.PositionText ?? "X:— Y:— | ring:— sector:—";
    public string SelectedClassEntryDirection => SelectedClassEntry?.DirectionText ?? "direction:— | branch:—";
    public string SelectedClassEntryRequiredNodeIds => SelectedClassEntry?.RequirementsText ?? "Требуется: —";
    public string SelectedClassEntryLayoutVersion => SelectedClassEntry == null ? "layout: —" : $"layout: v{SelectedClassEntry.LayoutVersion}; sort:{SelectedClassEntry.SortOrder}";
    public string SelectedClassEntryMeta => SelectedClassEntry?.FriendlyMetaText ?? "Узел не выбран.";
    public string SelectedClassEntryUnlock => SelectedClassEntry?.FriendlyUnlockText ?? string.Empty;
    public bool CanBuySelectedClassNode => SelectedClassEntry?.CanPurchase == true && !string.IsNullOrWhiteSpace(SelectedCharacterId);
    public bool CanRequestSelectedClassNode => SelectedClassEntry?.RequiresRequest == true && !string.IsNullOrWhiteSpace(SelectedCharacterId);
    public string DevelopmentStatusText { get => _developmentStatusText; set { _developmentStatusText = value ?? string.Empty; Notify(); } }
    public bool HasClassBranches => ClassBranches.Count > 0;
    public bool HasClassEntries => ClassEntries.Count > 0;
    public bool HasSelectedClassEntry => SelectedClassEntry != null;

    public string PublicProfileName { get; set; } = "Персонаж не выбран";
    public string PublicProfileSubtitle { get; set; } = "Нет данных";
    public string PublicProfileStatusText { get; set; } = "Публичный профиль не загружен";
    public string PublicProfileHintText { get; set; } = "Введите CharacterId и откройте профиль";
    public string PublicProfileDescription { get; set; } = "Публичные данные персонажа пока не загружены.";
    public bool HasPublicProfileData => PublicProfileIdentityRows.Count > 0 || PublicProfileSummaryRows.Count > 0 || PublicProfileHiddenRows.Count > 0;

    public ObservableCollection<GlobalSearchResultVm> GlobalSearchResults { get; } = new ObservableCollection<GlobalSearchResultVm>();
    public ObservableCollection<string> GlobalSearchCategories { get; } = new ObservableCollection<string>
    {
        "all",
        "characters",
        "inventory",
        "definitions",
        "development",
        "requests",
        "journal",
        "calendar"
    };
    public string GlobalSearchQuery { get => _globalSearchQuery; set { _globalSearchQuery = value ?? string.Empty; Notify(); } }
    public string GlobalSearchCategoryFilter { get => _globalSearchCategoryFilter; set { _globalSearchCategoryFilter = string.IsNullOrWhiteSpace(value) ? "all" : value; Notify(); } }
    public string GlobalSearchStatusText { get => _globalSearchStatusText; set { _globalSearchStatusText = value ?? string.Empty; Notify(); } }
    public GlobalSearchResultVm? SelectedGlobalSearchResult
    {
        get => _selectedGlobalSearchResult;
        set
        {
            _selectedGlobalSearchResult = value;
            Notify();
            Notify(nameof(SelectedGlobalSearchTitle));
            Notify(nameof(SelectedGlobalSearchSnippet));
            Notify(nameof(SelectedGlobalSearchType));
            Notify(nameof(SelectedGlobalSearchRoute));
        }
    }
    public string SelectedGlobalSearchTitle => SelectedGlobalSearchResult?.DisplayTitle ?? "Результат не выбран";
    public string SelectedGlobalSearchSnippet => SelectedGlobalSearchResult?.Snippet ?? "Выберите результат поиска.";
    public string SelectedGlobalSearchType => SelectedGlobalSearchResult?.DisplayType ?? "—";
    public string SelectedGlobalSearchRoute => SelectedGlobalSearchResult?.RouteSummary ?? "—";

    public ICommand ToggleAuthPopupCommand { get; }
    public ICommand ToggleConnectionPopupCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand GlobalSearchCommand { get; }
    public ICommand GlobalSearchOpenCommand { get; }
    public ICommand LoadCharacterHubCommand { get; }
    public ICommand SetActiveCharacterCommand { get; }
    public ICommand CreateCharacterCommand { get; }
    public ICommand CreateDiceRequestCommand { get; }
    public ICommand CreatePlayerRequestCommand { get; }
    public ICommand CancelRequestCommand { get; }
    public ICommand ResubmitRequestCommand { get; }
    public ICommand RefreshRequestsCommand { get; }
    public ICommand ChatSendCommand { get; }
    public ICommand BottomRefreshCommand { get; }
    public ICommand AudioRefreshCommand { get; }
    public ICommand AudioApplyLocalSettingsCommand { get; }
    public ICommand VisibilityLoadCommand { get; }
    public ICommand VisibilitySaveCommand { get; }
    public ICommand PublicCharacterLoadCommand { get; }
    public ICommand NotesRefreshCommand { get; }
    public ICommand NotesCreateCommand { get; }
    public ICommand NotesArchiveCommand { get; }
    public ICommand LocalNoteAddCommand { get; }
    public ICommand LocalNoteSaveCommand { get; }
    public ICommand LocalNoteDeleteCommand { get; }
    public ICommand LocalNoteClearCommand { get; }
    public ICommand CombatRefreshSnapshotCommand { get; }
    public ICommand CombatRefreshFeedCommand { get; }
    public ICommand CombatClearErrorCommand { get; }
    public ICommand AcquireClassNodeCommand { get; }
    public ICommand BuySelectedClassNodeCommand { get; }
    public ICommand RequestUnlockNodeCommand { get; }
    public ICommand FitToViewDevelopmentHexagonCommand { get; }
    public ICommand DevelopmentViewerSearchClearCommand { get; }
    public ICommand DevelopmentViewerSearchNextCommand { get; }
    public ICommand DevelopmentViewerSearchPreviousCommand { get; }
    public ICommand AcquireSkillCommand { get; }
    public ICommand SkillCheckRollCommand { get; }
    public ICommand ConnectToServerCommand { get; }
    public ICommand ApplyConnectionSettingsCommand { get; }
    public ICommand ResetConnectionDefaultsCommand { get; }
    public ICommand UseLastConnectionCommand { get; }

    private void RunGlobalSearch()
    {
        try
        {
            GlobalSearchResults.Clear();
            SelectedGlobalSearchResult = null;
            var payload = new Dictionary<string, object>
            {
                { "query", GlobalSearchQuery },
                { "limit", 50 },
                { "offset", 0 },
                { "characterId", ActiveCharacterId }
            };

            if (!string.Equals(GlobalSearchCategoryFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                payload["categories"] = new object[] { GlobalSearchCategoryFilter };
            }

            var response = _api.SearchPlayerQuery(payload);
            if (response.Status != ResponseStatus.Ok)
            {
                GlobalSearchStatusText = string.IsNullOrWhiteSpace(response.Message)
                    ? "Глобальный поиск недоступен."
                    : response.Message;
                ClientLogService.Instance.Warn($"player.global_search.query.failed status={response.Status} message={response.Message}");
                return;
            }

            var items = response.Payload.TryGetValue("items", out var rawItems)
                ? ToObjectList(rawItems)
                : new ArrayList();

            foreach (var item in items)
            {
                var map = AsMap(item, CommandNames.SearchPlayerQuery);
                if (map == null) continue;
                GlobalSearchResults.Add(MapGlobalSearchResult(map));
            }

            SelectedGlobalSearchResult = GlobalSearchResults.FirstOrDefault();
            var total = response.Payload.TryGetValue("total", out var rawTotal)
                ? Convert.ToString(rawTotal)
                : GlobalSearchResults.Count.ToString(CultureInfo.InvariantCulture);
            GlobalSearchStatusText = $"Найдено: {total}. Показано: {GlobalSearchResults.Count}.";
            ClientLogService.Instance.Info($"player.global_search.query.done count={GlobalSearchResults.Count} total={total}");
        }
        catch (Exception ex)
        {
            GlobalSearchStatusText = $"Ошибка поиска: {ex.Message}";
            ClientLogService.Instance.Warn($"player.global_search.query.error reason={ex.Message}");
        }
    }

    private void OpenGlobalSearchResult()
    {
        var selected = SelectedGlobalSearchResult;
        if (selected == null)
        {
            GlobalSearchStatusText = "Выберите результат поиска.";
            return;
        }

        try
        {
            var response = _api.SearchPlayerOpenTarget(new Dictionary<string, object>
            {
                { "routeKey", selected.RouteKey },
                { "entityId", selected.EntityId }
            });

            if (response.Status != ResponseStatus.Ok)
            {
                GlobalSearchStatusText = string.IsNullOrWhiteSpace(response.Message)
                    ? "Не удалось открыть результат."
                    : response.Message;
                ClientLogService.Instance.Warn($"player.global_search.open.failed route={selected.RouteKey} entityId={selected.EntityId} status={response.Status}");
                return;
            }

            ApplyGlobalSearchRoute(selected);
            GlobalSearchStatusText = $"Открыт результат: {selected.DisplayTitle}";
            ClientLogService.Instance.Info($"player.global_search.open.done route={selected.RouteKey} entityId={selected.EntityId}");
        }
        catch (Exception ex)
        {
            GlobalSearchStatusText = $"Ошибка открытия: {ex.Message}";
            ClientLogService.Instance.Warn($"player.global_search.open.error reason={ex.Message}");
        }
    }

    private void ApplyGlobalSearchRoute(GlobalSearchResultVm result)
    {
        switch (result.RouteKey)
        {
            case "character.details":
                if (!string.IsNullOrWhiteSpace(result.EntityId))
                {
                    ActiveCharacterId = result.EntityId;
                    LoadSelectedCharacterHub();
                }
                SelectedMainTab = "character";
                break;
            case "playerRequest.details":
                RefreshDiceAndRequests();
                SelectedRequestRow = result.EntityId;
                SelectedMainTab = "requests";
                break;
            case "eventJournal.details":
                EventJournal.RefreshFlags();
                SelectedMainTab = "journal";
                break;
            case "worldCalendarEvent.details":
                WorldCalendar.RefreshFlags();
                SelectedMainTab = "calendar";
                break;
            case "realScheduleEvent.details":
                RealSchedule.RefreshFlags();
                SelectedMainTab = "calendar";
                break;
            case "worldMap.details":
            case "worldMap.region":
            case "worldMap.location":
            case "worldMap.label":
                SelectedMainTab = "worldMap";
                if (WorldMap.RefreshMapsCommand.CanExecute(null))
                    WorldMap.RefreshMapsCommand.Execute(null);
                break;
            case "definition.details":
                SelectedMainTab = "character";
                break;
            default:
                SelectedMainTab = "search";
                break;
        }
    }

    private static GlobalSearchResultVm MapGlobalSearchResult(Dictionary<string, object> map)
        => new GlobalSearchResultVm
        {
            ResultId = GetString(map, "resultId"),
            EntityType = GetString(map, "entityType"),
            EntityId = GetString(map, "entityId"),
            SourceCollection = GetString(map, "sourceCollection"),
            Title = GetString(map, "title"),
            Snippet = GetString(map, "snippet"),
            Category = GetString(map, "category"),
            RouteKey = GetString(map, "routeKey"),
            Visibility = GetString(map, "visibility")
        };

    private void Login()
    {
        try
        {
            EnsureConnected();
            ClientLogService.Instance.Info($"Login attempt: user={LoginText}");
            var result = _api.Login(LoginText, PasswordText);
            if (result.Status != ResponseStatus.Ok)
            {
                ConnectionState = "Оффлайн";
                ClientLogService.Instance.Warn($"Login failed: user={LoginText}; message={result.Message}");
                return;
            }

            SetConnectedState();
            IsAuthPopupOpen = false;
            PlayerDisplayName = LoginText;
            NotifyHeader();
            RefreshLocalNotesForCurrentCharacter();
            SessionSummary = "Сессия: default";
            RefreshAll();
            ClientLogService.Instance.Info($"player.auth.login.success user={LoginText}");
            _poller.Start();
        }
        catch (Exception ex)
        {
            if (IsConnectionLevelException(ex))
            {
                SetConnectionError(ex);
                return;
            }

            if (LooksLikeUnauthorized(ex.Message))
            {
                HandleUnauthorizedState("refresh.all", ex.Message);
                return;
            }

            ConnectionStatusDetail = "Сервер не настроен, проверьте параметры подключения.";
            Notify(nameof(ConnectionStatusDetail));
            ClientLogService.Instance.Warn($"player.refresh.warning message={ex.Message}");
        }
    }

    private void Register()
    {
        try
        {
            _api.Register(LoginText, PasswordText);
            ClientLogService.Instance.Info($"register requested login={LoginText} result=pending");
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void ChangePassword()
    {
        try
        {
            EnsureConnected();
            ClientLogService.Instance.Info("changePassword.send");
            var result = _api.ChangePassword(OldPasswordText, NewPasswordText);
            ClientLogService.Instance.Info($"changePassword.response status={result.Status}");
            if (result.Status != ResponseStatus.Ok) throw new InvalidOperationException(result.Message);
            ConnectionStatusDetail = "Подключение выполнено.";
            Notify(nameof(ConnectionStatusDetail));
            OldPasswordText = string.Empty;
            NewPasswordText = string.Empty;
            Notify(nameof(OldPasswordText));
            Notify(nameof(NewPasswordText));
        }
        catch (Exception ex)
        {
            ConnectionStatusDetail = $"Ошибка подключения: {ex.Message}";
            Notify(nameof(ConnectionStatusDetail));
            ClientLogService.Instance.Info("changePassword.response status=Failed");
            ClientLogService.Instance.Warn($"changePassword.error reason={ex.Message}");
        }
    }

    private void RefreshAll()
    {
        try
        {
            LoadCharacters();
            LoadActiveCharacter();
            LoadClassAndSkills();
            RefreshBottomPanel();
            RefreshNotes();
            FunctionalDashboard.Refresh();
            CurrentSession.RefreshFlags();
            ActiveGroup.RefreshFlags();
            EventJournal.RefreshFlags();
            if (!SceneMap.ManualMapIdMode || !string.IsNullOrWhiteSpace(SceneMap.MapId))
                SceneMap.Refresh();
            if (!string.IsNullOrWhiteSpace(WorldMap.MapId))
                WorldMap.RefreshCurrentMap();
            WorldCalendar.RefreshFlags();
            RealSchedule.RefreshFlags();
            NotifyHeader();
            SetConnectedState();
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void PollRefresh()
    {
        try
        {
            RefreshBottomPanel();
            PollPassiveSync();
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void PollPassiveSync()
    {
        if (!SyncFeatureFlags.UsePassiveSyncPoller) return;
        var scopes = new[] { "chat:default", "dice", "fate", "definitions", "character", "combat", "audio" };
        var response = _api.SyncChangesGet(_syncRevision, scopes, 100);
        if (response.Status != ResponseStatus.Ok || !response.Payload.ContainsKey("events")) return;
        foreach (var raw in ToObjectList(response.Payload["events"]))
        {
            var evt = ClientSyncEvent.FromMap(AsMap(raw, CommandNames.SyncChangesGet));
            ClientLogService.Instance.Info($"sync.event.received eventId={evt.EventId} revision={evt.Revision} type={evt.Type} scope={evt.Scope}");
            if (!SyncFeatureFlags.UseEventDispatcher) continue;
            try { _syncDispatcher.DispatchAsync(evt).GetAwaiter().GetResult(); }
            catch (Exception ex) { ClientLogService.Instance.Error($"sync.dispatch.error eventId={evt.EventId} type={evt.Type} message={ex.Message}", ex); }
            _syncRevision = Math.Max(_syncRevision, evt.Revision);
        }
    }

    private void LoadCharacters()
    {
        MyCharacters.Clear();
        ClientLogService.Instance.Info("player.characters.load.start");
        var mine = _api.GetMyCharacters();
        if (mine.Status == ResponseStatus.Unauthorized)
        {
            HandleUnauthorizedState(CommandNames.CharacterListMine, mine.Message);
            return;
        }

        if (mine.Status != ResponseStatus.Ok)
        {
            ClientLogService.Instance.Warn($"player.characters.load.error status={mine.Status} message={mine.Message}");
            Notify(nameof(HasMyCharacters));
            return;
        }
        foreach (var item in ToObjectList(mine.Payload.ContainsKey("items") ? mine.Payload["items"] : new ArrayList()))
        {
            var map = AsMap(item, CommandNames.CharacterListMine);
            if (map == null) continue;
            var rowStats = map.ContainsKey("stats") ? AsMap(map["stats"], CommandNames.CharacterListMine) : null;
            var backstory = FirstNonEmpty(GetString(map, "backstory"), GetString(map, "description"));
            var healthText = FirstNonEmpty(
                GetString(map, "health"),
                GetString(map, "currentHealth"),
                GetString(rowStats, "health"),
                "—");
            var physicalArmor = FirstNonEmpty(GetString(map, "physicalArmor"), GetString(rowStats, "physicalArmor"), "—");
            var magicalArmor = FirstNonEmpty(GetString(map, "magicalArmor"), GetString(rowStats, "magicalArmor"), "—");
            var xpCoinsText = FirstNonEmpty(GetString(map, "xpCoins"), GetString(map, "experienceCoins"), "—");
            MyCharacters.Add(new CharacterListItemVm
            {
                Id = FirstNonEmpty(GetString(map, "characterId"), GetString(map, "id"), Guid.NewGuid().ToString("N")),
                Name = FirstNonEmpty(GetString(map, "name"), "Без имени"),
                Race = FirstNonEmpty(GetString(map, "race"), "—"),
                Age = FirstNonEmpty(GetString(map, "age"), "—"),
                Height = FirstNonEmpty(GetString(map, "height"), "—"),
                Description = backstory,
                BackstoryPreview = BuildPreviewText(backstory, 140),
                HealthText = string.IsNullOrWhiteSpace(healthText) ? "—" : healthText,
                ArmorText = $"Физ: {physicalArmor} / Маг: {magicalArmor}",
                ExperienceCoinsText = xpCoinsText,
                OwnerDisplay = FirstNonEmpty(GetString(map, "ownerDisplayName"), GetString(map, "ownerUserId"), "—"),
                GroupDisplay = BuildGroupDisplay(map),
                CharacterKindDisplay = FirstNonEmpty(GetString(map, "characterKindDisplayName"), GetString(map, "characterKind"), GetString(map, "characterRole"), "—"),
                CharacterStatusDisplay = FirstNonEmpty(GetString(map, "characterStatusDisplayName"), GetString(map, "characterStatus"), "—"),
                Archived = GetBool(map, "archived")
            });

            var optionalMissing = new List<string>();
            if (string.IsNullOrWhiteSpace(GetString(map, "health")) && string.IsNullOrWhiteSpace(GetString(map, "currentHealth")) && string.IsNullOrWhiteSpace(GetString(rowStats, "health"))) optionalMissing.Add("health");
            if (string.IsNullOrWhiteSpace(GetString(map, "physicalArmor")) && string.IsNullOrWhiteSpace(GetString(rowStats, "physicalArmor"))) optionalMissing.Add("physicalArmor");
            if (string.IsNullOrWhiteSpace(GetString(map, "magicalArmor")) && string.IsNullOrWhiteSpace(GetString(rowStats, "magicalArmor"))) optionalMissing.Add("magicalArmor");
            if (string.IsNullOrWhiteSpace(GetString(map, "xpCoins")) && string.IsNullOrWhiteSpace(GetString(map, "experienceCoins"))) optionalMissing.Add("xpCoins");
            if (optionalMissing.Count > 0)
            {
                ClientLogService.Instance.Warn($"player.characters.load.warning characterId={FirstNonEmpty(GetString(map, "characterId"), GetString(map, "id"), "unknown")} missingOptional={string.Join(",", optionalMissing)}");
            }
        }
        ClientLogService.Instance.Info($"player.characters.load.done count={MyCharacters.Count}");
        ClientLogService.Instance.Info($"myCharacters.render count={MyCharacters.Count}");
        Notify(nameof(HasMyCharacters));

        if (string.IsNullOrWhiteSpace(SelectedCharacterId) && MyCharacters.Count > 0)
            SelectedCharacterId = MyCharacters[0].Id;
        SelectedMyCharacter = MyCharacters.FirstOrDefault(item => item.Id == SelectedCharacterId) ?? MyCharacters.FirstOrDefault();
        UpdateCharacterActiveFlags();
    }

    private void LoadSelectedCharacterHub()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        var response = _api.CharacterPlayerHubGet(SelectedCharacterId);
        if (response.Status != ResponseStatus.Ok)
        {
            ConnectionStatusDetail = string.IsNullOrWhiteSpace(response.Message)
                ? "Активный персонаж не выбран."
                : response.Message;
            Notify(nameof(ConnectionStatusDetail));
            return;
        }

        var cards = response.Payload.ContainsKey("characters")
            ? ToObjectList(response.Payload["characters"])
            : new ArrayList();
        var first = cards.Cast<object>().Select(item => AsMap(item, CommandNames.CharacterPlayerHubGet)).FirstOrDefault(map => map != null);
        if (first == null) return;

        ApplyCharacterPayload(first);
        SelectedCharacterId = FirstNonEmpty(GetString(first, "characterId"), SelectedCharacterId);
        ActiveCharacterId = FirstNonEmpty(ActiveCharacterId, SelectedCharacterId);
        ActiveCharacterStatusText = $"Активный персонаж: {CharacterNameDisplay}";
        NotifyCharacter();
    }

    private string BuildGroupDisplay(Dictionary<string, object> payload)
    {
        var groups = ToObjectList(payload.TryGetValue("groupMembership", out var raw) ? raw : new ArrayList())
            .Cast<object>()
            .Select(item => AsMap(item, CommandNames.CharacterListMine))
            .Where(map => map != null)
            .Select(map => FirstNonEmpty(GetString(map, "displayName"), GetString(map, "groupId")))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        return groups.Length == 0 ? "—" : string.Join(", ", groups);
    }

    public void NotifyChatWindowOpened()
    {
        RequestChatScrollToLatest(isInitial: true);
    }

    private void SetSelectedCharacterActive()
    {
        if (SelectedMyCharacter == null) return;
        try
        {
            ClientLogService.Instance.Info($"character.set.active requested characterId={SelectedMyCharacter.Id}");
            var result = _api.SetActiveCharacter(SelectedMyCharacter.Id);
            if (result.Status != ResponseStatus.Ok) throw new InvalidOperationException(result.Message);
            ApplyCharacterPayload(result.Payload);
            ActiveCharacterId = SelectedMyCharacter.Id;
            ActiveCharacterStatusText = $"Активный персонаж: {SelectedMyCharacter.Name}";
            ClientLogService.Instance.Info($"character.set.active success characterId={SelectedMyCharacter.Id}");
            LoadCharacters();
            LoadActiveCharacter();
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Warn($"character.set.active failed reason={ex.Message}");
            ConnectionStatusDetail = $"Ошибка подключения: {ex.Message}";
            Notify(nameof(ConnectionStatusDetail));
        }
    }

    private void UpdateCharacterActiveFlags()
    {
        foreach (var character in MyCharacters)
        {
            character.IsActive = !string.IsNullOrWhiteSpace(ActiveCharacterId) && string.Equals(character.Id, ActiveCharacterId, StringComparison.Ordinal);
        }
        Notify(nameof(MyCharacters));
    }

    private void RequestChatScrollToLatest(bool isInitial)
    {
        ClientLogService.Instance.Debug($"chat.autoScroll initial={isInitial.ToString().ToLowerInvariant()}");
        ClientLogService.Instance.Debug("chat.scroll target=latest");
        ChatScrollRequestVersion++;
    }

    private void LoadActiveCharacter()
    {
        var requestedCharacterId = FirstNonEmpty(ActiveCharacterId, SelectedCharacterId);
        var active = _api.CharacterPlayerHubGet(requestedCharacterId);
        if (active.Status == ResponseStatus.Ok && active.Payload.Count > 0)
        {
            var cards = active.Payload.ContainsKey("characters")
                ? ToObjectList(active.Payload["characters"])
                : new ArrayList();
            var first = cards.Cast<object>()
                .Select(item => AsMap(item, CommandNames.CharacterPlayerHubGet))
                .FirstOrDefault(map => map != null);
            if (first == null)
            {
                ActiveCharacterId = string.Empty;
                ActiveCharacterStatusText = "Активный персонаж не выбран.";
                NotifyCharacter();
                return;
            }

            ApplyCharacterPayload(first);
            ActiveCharacterId = GetString(first, "characterId");
            ActiveCharacterStatusText = string.IsNullOrWhiteSpace(ActiveCharacterId) ? "Активный персонаж не выбран." : $"Активный персонаж: {CharacterNameDisplay}";
            ClientLogService.Instance.Info($"activeCharacter.load id={ActiveCharacterId}");
            UpdateCharacterActiveFlags();
            return;
        }

        ActiveCharacterId = string.Empty;
        ActiveCharacterStatusText = "Активный персонаж не выбран.";
        CharacterName = string.Empty;
        CharacterRace = string.Empty;
        CharacterBackstory = string.Empty;
        ExperienceCoins = 0;
        StatsRows.Clear();
        CoreStatRows.Clear();
        AttributeStatRows.Clear();
        DerivedStatRows.Clear();
        RebuildStatGroups();
        MoneyRows.Clear();
        RefreshLocalNotesForCurrentCharacter();
        NotifyCharacter();
        ClientLogService.Instance.Info("activeCharacter.load id=null");
        UpdateCharacterActiveFlags();
    }
    private void ApplyCharacterPayload(Dictionary<string, object> payload)
    {
        CharacterName = GetString(payload, "name");
        CharacterRace = GetString(payload, "race");
        CharacterAge = GetString(payload, "age");
        CharacterHeight = GetString(payload, "height");
        CharacterBackstory = GetString(payload, "backstory");
        ExperienceCoins = ParseLongValue(payload, "xpCoins");
        SelectedCharacterId = GetString(payload, "characterId");
        CharacterOwnerDisplay = FirstNonEmpty(GetString(payload, "ownerDisplayName"), GetString(payload, "ownerUserId"), "—");
        CharacterControllerDisplay = FirstNonEmpty(GetString(payload, "controlledByDisplayName"), GetString(payload, "controlledByUserId"), "—");
        CharacterKindDisplay = FirstNonEmpty(GetString(payload, "characterKindDisplayName"), GetString(payload, "characterKind"), GetString(payload, "characterRole"), "—");
        CharacterStatusDisplay = FirstNonEmpty(GetString(payload, "characterStatusDisplayName"), GetString(payload, "characterStatus"), GetString(payload, "assignmentStatus"), "—");
        var groupMaps = ToObjectList(payload.TryGetValue("groupMembership", out var groupRaw) ? groupRaw : new ArrayList())
            .Cast<object>()
            .Select(item => AsMap(item, CommandNames.CharacterGetActive))
            .Where(map => map != null)
            .Select(map => FirstNonEmpty(GetString(map, "displayName"), GetString(map, "groupId")))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        CharacterGroupDisplay = groupMaps.Length == 0 ? "—" : string.Join(", ", groupMaps);

        StatsRows.Clear();
        CoreStatRows.Clear();
        DerivedStatRows.Clear();
        var hasVitals = payload.TryGetValue("vitals", out var vitalsRaw) && ToObjectList(vitalsRaw).Count > 0;
        if (hasVitals)
        {
            BindCharacterStatRows(vitalsRaw!, CoreStatRows, CommandNames.CharacterGetActive, "vitals");
        }
        else
        {
            var stats = payload.TryGetValue("stats", out var statsRaw)
                ? AsMap(statsRaw, CommandNames.CharacterGetActive)
                : null;
            stats ??= new Dictionary<string, object>();
            AddStat("HP", stats, "health");
            AddStat("Физ. броня", stats, "physicalArmor");
            AddStat("Маг. броня", stats, "magicalArmor");
            AddStat("Мораль", stats, "morale");
            RebuildStatGroups();
        }

        if (payload.TryGetValue("derivedStats", out var derivedRaw))
        {
            BindCharacterStatRows(derivedRaw, DerivedStatRows, CommandNames.CharacterGetActive, "derivedStats");
        }
        BindAttributeRows(payload.ContainsKey("attributes") ? payload["attributes"] : new ArrayList(), CommandNames.CharacterGetActive);

        BindCurrencyRows(payload);

        BindInventoryRows(payload.ContainsKey("inventory") ? payload["inventory"] : new ArrayList());

        BindHoldingsRows(payload.ContainsKey("holdings") ? payload["holdings"] : new ArrayList(), CommandNames.CharacterGetActive);

        BindReputationRows(payload.ContainsKey("reputation") ? payload["reputation"] : new ArrayList(), CommandNames.CharacterGetActive);

        BindCompanions(payload.ContainsKey("companions") ? payload["companions"] : new ArrayList(), CommandNames.CharacterGetActive);
        BindCharacterKnowledge(payload);
        BindCharacterResearch(payload);
        BindCharacterCrafting(payload);
        RefreshCharacterCrafting();

        EnsureCollectionPlaceholder(InventoryRows, "Инвентарь пока не загружен.");
        if (HoldingsItems.Count == 0) EnsureCollectionPlaceholder(HoldingsRows, "   ");
        if (ReputationRows.Count == 0) EnsureReputationPlaceholder();
        if (Companions.Count == 0) EnsureCompanionsPlaceholder();
        if (SelectedCompanion == null || !Companions.Contains(SelectedCompanion))
            SelectedCompanion = Companions.FirstOrDefault();

        NotifyCharacter();
    }

    private void LoadActiveCharacterInventory()
    {
        if (string.IsNullOrWhiteSpace(ActiveCharacterId)) return;
        try
        {
            var response = _api.CharacterInventoryGet(ActiveCharacterId);
            if (response.Status != ResponseStatus.Ok) return;
            var payloadKeys = string.Join(",", response.Payload.Keys.OrderBy(x => x, StringComparer.Ordinal));
            var rawInventory = response.Payload.ContainsKey("inventory") ? response.Payload["inventory"] : null;
            var rawType = rawInventory?.GetType().FullName ?? "null";
            var rawItems = NormalizeInventoryRaw(rawInventory);
            var signature = payloadKeys + "|" + rawType + "|" + rawItems.Count;
            if (!string.Equals(_lastInventoryPayloadSignature, signature, StringComparison.Ordinal))
            {
                ClientLogService.Instance.Info($"inventory.player.payload.keys={payloadKeys}");
                ClientLogService.Instance.Info($"inventory.player.raw.type={rawType}");
                ClientLogService.Instance.Info($"inventory.player.raw.count={rawItems.Count}");
                _lastInventoryPayloadSignature = signature;
            }
            BindInventoryRows(rawItems, CommandNames.CharacterInventoryGet);
            ClientLogService.Instance.Info($"activeCharacter.inventory loaded={InventoryItems.Count}");
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void BindInventoryRows(object rawInventory)
    {
        BindInventoryRows(NormalizeInventoryRaw(rawInventory), CommandNames.CharacterGetActive);
    }

    private void BindInventoryRows(IList rawItems, string context)
    {
        InventoryItems.Clear();
        InventoryRows.Clear();
        foreach (var item in rawItems.Cast<object>())
        {
            var map = AsMap(item, context);
            if (map == null) continue;
            var quantityText = GetString(map, "quantity");
            int.TryParse(quantityText, out var quantity);
            var durability = FirstNonEmpty(GetString(map, "durabilityOrHealth"), GetString(map, "durability"), "-");
            var equippedText = FirstNonEmpty(GetString(map, "isEquipped"), GetString(map, "equipped"), "False");
            var vm = new InventoryDisplayItemVm
            {
                Id = GetString(map, "id"),
                Code = FirstNonEmpty(GetString(map, "itemCode"), GetString(map, "definitionId"), GetString(map, "id")),
                Name = FirstNonEmpty(GetString(map, "displayName"), GetString(map, "name"), GetString(map, "label"), " "),
                Quantity = quantity,
                QuantityDisplay = string.IsNullOrWhiteSpace(quantityText) ? MissingDataText : quantity.ToString(CultureInfo.InvariantCulture),
                IsEquipped = string.Equals(equippedText, "True", StringComparison.OrdinalIgnoreCase),
                Durability = durability,
                Slot = FirstNonEmpty(GetString(map, "slot"), GetString(map, "slotId"), GetString(map, "properties"), "-"),
                Category = GetString(map, "category"),
                Description = GetString(map, "description")
            };
            InventoryItems.Add(vm);
            InventoryRows.Add($"{vm.Name} x{vm.QuantityDisplay} | экипировано: {vm.IsEquipped} | прочность: {vm.Durability} | категория={vm.Category}");
        }
        var placeholderHidden = InventoryItems.Count > 0;
        if (!placeholderHidden) EnsureCollectionPlaceholder(InventoryRows, "Инвентарь пока не загружен.");
        if (SelectedInventoryItem == null || !InventoryItems.Contains(SelectedInventoryItem))
            SelectedInventoryItem = InventoryItems.FirstOrDefault();

        if (_lastInventoryRenderCount != InventoryItems.Count)
        {
            ClientLogService.Instance.Info($"activeCharacter.inventory.bind count={InventoryItems.Count}");
            ClientLogService.Instance.Info($"inventory.player.mapped.count={InventoryItems.Count}");
            ClientLogService.Instance.Info($"inventory.player.bound.count={InventoryItems.Count}");
            ClientLogService.Instance.Info($"activeCharacter.inventory.render count={InventoryItems.Count}");
            ClientLogService.Instance.Info($"inventory.player.render.count={InventoryItems.Count}");
            _lastInventoryRenderCount = InventoryItems.Count;
        }
        if (_lastInventoryPlaceholderHidden != placeholderHidden)
        {
            ClientLogService.Instance.Info($"activeCharacter.inventory.placeholder hidden={placeholderHidden.ToString().ToLowerInvariant()}");
            _lastInventoryPlaceholderHidden = placeholderHidden;
        }

        Notify(nameof(InventoryItems));
        Notify(nameof(SelectedInventoryItem));
    }

    private static IList NormalizeInventoryRaw(object? rawInventory)
    {
        if (rawInventory == null) return new ArrayList();
        if (rawInventory is IList list) return list;
        if (rawInventory is IDictionary) return new object[] { rawInventory };
        if (rawInventory is IEnumerable enumerable && rawInventory is not string) return enumerable.Cast<object>().ToArray();
        return new ArrayList();
    }

    private void LoadActiveCharacterHoldings()
    {
        if (string.IsNullOrWhiteSpace(ActiveCharacterId)) return;
        try
        {
            var response = _api.CharacterHoldingsGet(ActiveCharacterId);
            if (response.Status != ResponseStatus.Ok) return;
            BindHoldingsRows(response.Payload.ContainsKey("holdings") ? response.Payload["holdings"] : new ArrayList(), CommandNames.CharacterHoldingsGet);
            if (_lastHoldingsLoadedCount != HoldingsItems.Count)
            {
                ClientLogService.Instance.Info($"activeCharacter.holdings loaded={HoldingsItems.Count}");
                _lastHoldingsLoadedCount = HoldingsItems.Count;
            }
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void BindHoldingsRows(object rawHoldings, string context)
    {
        HoldingsRows.Clear();
        HoldingsItems.Clear();
        foreach (var item in ToObjectList(rawHoldings))
        {
            var map = AsMap(item, context);
            if (map == null) continue;
            var owners = ToObjectList(map.ContainsKey("owners") ? map["owners"] : new ArrayList()).Cast<object>().Select(x => x?.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            var vm = new HoldingDisplayItemVm
            {
                Id = GetString(map, "id"),
                Name = FirstNonEmpty(GetString(map, "name"), " "),
                Type = GetString(map, "type"),
                Description = GetString(map, "description"),
                Notes = GetString(map, "notes"),
                OwnersDisplay = owners.Length == 0 ? "—" : string.Join(", ", owners),
                IsArchived = string.Equals(FirstNonEmpty(GetString(map, "isArchived"), GetString(map, "archived")), "True", StringComparison.OrdinalIgnoreCase)
            };
            HoldingsItems.Add(vm);
            HoldingsRows.Add($"{vm.Name} ({vm.Type}) [{vm.StatusLabel}] — {vm.Preview}");
        }

        var placeholderHidden = HoldingsItems.Count > 0;
        if (!placeholderHidden) EnsureCollectionPlaceholder(HoldingsRows, "   ");
        if (SelectedHoldingItem == null || !HoldingsItems.Contains(SelectedHoldingItem))
            SelectedHoldingItem = HoldingsItems.FirstOrDefault();
        if (_lastHoldingsRenderCount != HoldingsItems.Count)
        {
            ClientLogService.Instance.Info($"activeCharacter.holdings.bind count={HoldingsItems.Count}");
            ClientLogService.Instance.Info($"activeCharacter.holdings.render count={HoldingsItems.Count}");
            _lastHoldingsRenderCount = HoldingsItems.Count;
        }
        if (_lastHoldingsPlaceholderHidden != placeholderHidden)
        {
            ClientLogService.Instance.Info($"activeCharacter.holdings.placeholder hidden={placeholderHidden.ToString().ToLowerInvariant()}");
            _lastHoldingsPlaceholderHidden = placeholderHidden;
        }
        Notify(nameof(HoldingsItems));
        Notify(nameof(SelectedHoldingItem));
    }

    private void LoadActiveCharacterReputation()
    {
        if (string.IsNullOrWhiteSpace(ActiveCharacterId)) return;
        try
        {
            var response = _api.CharacterReputationGet(ActiveCharacterId);
            if (response.Status != ResponseStatus.Ok) return;
            BindReputationRows(response.Payload.ContainsKey("reputation") ? response.Payload["reputation"] : new ArrayList(), CommandNames.CharacterReputationGet);
            if (_lastReputationLoadedCount != ReputationRows.Count)
            {
                ClientLogService.Instance.Info($"activeCharacter.reputation loaded={ReputationRows.Count}");
                _lastReputationLoadedCount = ReputationRows.Count;
            }
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void BindReputationRows(object rawReputation, string context)
    {
        ReputationRows.Clear();
        foreach (var item in ToObjectList(rawReputation))
        {
            var map = AsMap(item, context);
            if (map == null) continue;
            int.TryParse(GetString(map, "value"), out var value);
            ReputationRows.Add(new ReputationRowVm
            {
                Id = GetString(map, "id"),
                ScopeType = FirstNonEmpty(GetString(map, "scopeType"), "Character"),
                TargetType = FirstNonEmpty(GetString(map, "targetType"), "Other"),
                TargetName = FirstNonEmpty(GetString(map, "targetName"), GetString(map, "groupKey"), "Без названия"),
                Value = value,
                Notes = GetString(map, "notes"),
                IsArchived = string.Equals(FirstNonEmpty(GetString(map, "isArchived"), GetString(map, "archived")), "True", StringComparison.OrdinalIgnoreCase)
            });
        }
        var placeholderHidden = ReputationRows.Count > 0;
        if (!placeholderHidden) EnsureReputationPlaceholder();
        if (_lastReputationRenderCount != ReputationRows.Count)
        {
            ClientLogService.Instance.Info($"activeCharacter.reputation.bind count={ReputationRows.Count}");
            ClientLogService.Instance.Info($"activeCharacter.reputation.render count={ReputationRows.Count}");
            _lastReputationRenderCount = ReputationRows.Count;
        }
        if (_lastReputationPlaceholderHidden != placeholderHidden)
        {
            ClientLogService.Instance.Info($"activeCharacter.reputation.placeholder hidden={placeholderHidden.ToString().ToLowerInvariant()}");
            _lastReputationPlaceholderHidden = placeholderHidden;
        }
        Notify(nameof(ReputationRows));
    }

    private void LoadActiveCharacterCompanions()
    {
        if (string.IsNullOrWhiteSpace(ActiveCharacterId)) return;
        try
        {
            var response = _api.CharacterCompanionsGet(ActiveCharacterId);
            if (response.Status != ResponseStatus.Ok) return;
            BindCompanions(response.Payload.ContainsKey("companions") ? response.Payload["companions"] : new ArrayList(), CommandNames.CharacterCompanionsGet);
            if (_lastCompanionsLoadedCount != Companions.Count)
            {
                ClientLogService.Instance.Info($"activeCharacter.companions loaded={Companions.Count}");
                _lastCompanionsLoadedCount = Companions.Count;
            }
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void BindCompanions(object rawCompanions, string context)
    {
        Companions.Clear();
        foreach (var item in ToObjectList(rawCompanions))
        {
            var map = AsMap(item, context);
            if (map == null) continue;
            var vm = new CompanionVm
            {
                Id = GetString(map, "id"),
                Name = string.IsNullOrWhiteSpace(GetString(map, "name")) ? "Безымянный компаньон" : GetString(map, "name"),
                Type = FirstNonEmpty(GetString(map, "type"), GetString(map, "species")),
                Species = GetString(map, "species"),
                Description = GetString(map, "description"),
                Notes = GetString(map, "notes"),
                OwnerCharacterId = FirstNonEmpty(GetString(map, "ownerCharacterId"), ActiveCharacterId),
                IsArchived = string.Equals(FirstNonEmpty(GetString(map, "isArchived"), GetString(map, "archived")), "True", StringComparison.OrdinalIgnoreCase)
            };
            AddCompanionStatScaffold(vm);
            if (map.ContainsKey("stats") && map["stats"] is Dictionary<string, object> companionStats)
                ApplyCompanionStats(vm, companionStats);

            foreach (var inv in ToObjectList(map.ContainsKey("inventory") ? map["inventory"] : new ArrayList()))
                if (inv is Dictionary<string, object> im)
                    vm.InventoryRows.Add($"{GetString(im, "label")} x{GetString(im, "quantity")}");

            foreach (var hold in ToObjectList(map.ContainsKey("holdings") ? map["holdings"] : new ArrayList()))
                if (hold is Dictionary<string, object> hm)
                    vm.HoldingsRows.Add($"{GetString(hm, "name")} — {GetString(hm, "description")}");

            EnsureCollectionPlaceholder(vm.InventoryRows, "Инвентарь компаньона пока не загружен.");
            EnsureCollectionPlaceholder(vm.HoldingsRows, "   ");
            EnsureCollectionPlaceholder(vm.SkillsRows, "   ");
            EnsureCollectionPlaceholder(vm.ClassRows, "Развитие компаньона пока не загружено.");
            BindCompanionKnowledge(vm, map);
            BindCompanionResearch(vm, map);
            BindCompanionCrafting(vm, map);

            Companions.Add(vm);
        }

        var placeholderHidden = Companions.Count > 0;
        if (!placeholderHidden) EnsureCompanionsPlaceholder();
        if (SelectedCompanion == null || !Companions.Contains(SelectedCompanion))
            SelectedCompanion = Companions.FirstOrDefault();
        if (_lastCompanionsRenderCount != Companions.Count)
        {
            ClientLogService.Instance.Info($"activeCharacter.companions.bind count={Companions.Count}");
            ClientLogService.Instance.Info($"activeCharacter.companions.render count={Companions.Count}");
            _lastCompanionsRenderCount = Companions.Count;
        }
        if (_lastCompanionsPlaceholderHidden != placeholderHidden)
        {
            ClientLogService.Instance.Info($"activeCharacter.companions.placeholder hidden={placeholderHidden.ToString().ToLowerInvariant()}");
            _lastCompanionsPlaceholderHidden = placeholderHidden;
        }
        Notify(nameof(Companions));
        Notify(nameof(SelectedCompanion));
    }

    private void CreateDiceRequest()
    {
        try
        {
            var formula = DiceCount + "d" + DiceFaces + (DiceModifier == 0 ? string.Empty : DiceModifier > 0 ? "+" + DiceModifier : DiceModifier.ToString());
            var visibility = ToServerDiceVisibility(DiceVisibilityInput);
            var comment = DiceDescriptionInput;
            ClientLogService.Instance.Info("dice.actor.mode=account");
            ClientLogService.Instance.Info($"dice.roll.actor login={PlayerDisplayName} userId=unknown");
            ClientLogService.Instance.Info($"dice.roll.comment input={comment}");
            ClientLogService.Instance.Info("dice.roll.payload.keys=formula,visibility,description");
            ClientLogService.Instance.Info($"dice.roll.payload.commentPresent={!string.IsNullOrWhiteSpace(comment)}");
            if (string.Equals(DiceModeInput, "Проверочный", StringComparison.OrdinalIgnoreCase))
            {
                ClientLogService.Instance.Info($"dice.roll.test.send actor={PlayerDisplayName} formula={formula}");
                _api.DiceRollTest(formula, visibility, comment);
                var currentTest = _api.DiceTestGetCurrent();
                ClientLogService.Instance.Info($"dice.test.getCurrent.status={currentTest.Status}");
            }
            else
            {
                ClientLogService.Instance.Info($"dice.roll.standard.send actor={PlayerDisplayName} formula={formula}");
                _api.DiceRollStandard(formula, visibility, comment);
            }
            RefreshBottomPanel();
        }
        catch (Exception ex) { SetConnectionError(ex); }
    }

    private void CreatePlayerRequest()
    {
        try
        {
            EnsureConnected();
            var title = string.IsNullOrWhiteSpace(PlayerRequestTitleInput) ? DefaultPlayerRequestTitle(PlayerRequestTypeInput) : PlayerRequestTitleInput.Trim();
            var description = PlayerRequestDescriptionInput ?? string.Empty;
            var reason = PlayerRequestReasonInput ?? string.Empty;
            var payload = new Dictionary<string, object>
            {
                { "campaignId", "default" },
                { "sessionId", ChatSessionId ?? string.Empty },
                { "groupId", string.Empty },
                { "characterId", ActiveCharacterId ?? string.Empty },
                { "requestType", PlayerRequestTypeInput ?? "general" },
                { "category", PlayerRequestTypeInput ?? "general" },
                { "title", title },
                { "description", description },
                { "details", description },
                { "reason", reason },
                { "priority", PlayerRequestPriorityInput ?? "normal" },
                { "proposalType", PlayerRequestTypeInput ?? "general" },
                { "proposalPayloadSummary", description },
                { "submit", true }
            };
            var response = _api.CreatePlayerRequest(payload);
            if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(response.Message);
            PlayerRequestTitleInput = string.Empty;
            PlayerRequestDescriptionInput = string.Empty;
            PlayerRequestReasonInput = string.Empty;
            Notify(nameof(PlayerRequestTitleInput));
            Notify(nameof(PlayerRequestDescriptionInput));
            Notify(nameof(PlayerRequestReasonInput));
            RefreshDiceAndRequests();
        }
        catch (Exception ex)
        {
            ConnectionStatusDetail = "Ошибка входа: " + ex.Message;
            Notify(nameof(ConnectionStatusDetail));
            ClientLogService.Instance.Warn($"player.request.create.fail reason={ex.Message}");
            RefreshConnectionSummary();
        }
    }

    private void CreateCharacter()
    {
        try
        {
            EnsureConnected();
            var payload = new Dictionary<string, object>
            {
                { "name", CreateCharacterName },
                { "race", CreateCharacterRace },
                { "backstory", CreateCharacterBackstory }
            };
            ClientLogService.Instance.Info($"character.create.send name={CreateCharacterName}");
            var result = _api.CreateCharacter(payload);
            ClientLogService.Instance.Info($"character.create.response status={result.Status} message={result.Message}");
            if (result.Status != ResponseStatus.Ok) throw new InvalidOperationException(result.Message);
            LoadCharacters();
            if (MyCharacters.Count > 0 && string.IsNullOrWhiteSpace(ActiveCharacterId))
            {
                SelectedMyCharacter = MyCharacters.OrderByDescending(x => x.Id).FirstOrDefault() ?? MyCharacters[0];
                SetSelectedCharacterActive();
            }
            LoadActiveCharacter();
            ClientLogService.Instance.Info($"character.create.success count={MyCharacters.Count}");
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Warn($"character.create.result=fail reason={ex.Message}");
            ConnectionStatusDetail = $"Ошибка подключения: {ex.Message}";
            Notify(nameof(ConnectionStatusDetail));
            ClientLogService.Instance.Info("character.create.handled-error session-preserved=true");
            RefreshConnectionSummary();
        }
    }

    private void CancelRequest()
    {
        if (string.IsNullOrWhiteSpace(SelectedRequestId)) return;
        try
        {
            _api.CancelRequest(SelectedRequestId);
            RefreshBottomPanel();
        }
        catch (Exception ex) { SetConnectionError(ex); }
    }

    private void ResubmitRequest()
    {
        if (string.IsNullOrWhiteSpace(SelectedRequestId)) return;
        try
        {
            EnsureConnected();
            var details = string.IsNullOrWhiteSpace(PlayerRequestDescriptionInput)
                ? FirstNonEmpty(SelectedRequestDetails, "Уточнённая версия заявки.")
                : PlayerRequestDescriptionInput;
            var payload = new Dictionary<string, object>
            {
                { "requestId", SelectedRequestId },
                { "title", FirstNonEmpty(PlayerRequestTitleInput, _selectedRequestRawTitle, "Заявка GM") },
                { "details", details },
                { "description", details },
                { "reason", PlayerRequestReasonInput ?? string.Empty }
            };
            var response = _api.ResubmitPlayerRequest(payload);
            if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(response.Message);
            PlayerRequestTitleInput = string.Empty;
            PlayerRequestDescriptionInput = string.Empty;
            PlayerRequestReasonInput = string.Empty;
            Notify(nameof(PlayerRequestTitleInput));
            Notify(nameof(PlayerRequestDescriptionInput));
            Notify(nameof(PlayerRequestReasonInput));
            RefreshBottomPanel();
        }
        catch (Exception ex) { SetConnectionError(ex); }
    }

    private void RefreshBottomPanel()
    {
        RefreshChat();
        RefreshDiceAndRequests();
        RefreshCombatEvents();
        RefreshAudioState();
        BuildMergedChatRows();
        BuildGameFeed();
    }

    private void SendChat()
    {
        if (string.IsNullOrWhiteSpace(ChatTextInput)) return;
        var sessionId = ResolveChatSessionId();
        var serverType = ToServerChatType(ChatTypeInput);
        if (string.Equals(serverType, "System", StringComparison.OrdinalIgnoreCase))
        {
            TraceChatDiagnostic("blocked client-side system message send");
            return;
        }
        ClientLogService.Instance.Info($"Chat send requested: sessionId={sessionId}; command={CommandNames.ChatSend}");
        _api.ChatSend(sessionId, serverType, ChatTextInput);
        ChatTextInput = string.Empty;
        Notify(nameof(ChatTextInput));
        RefreshChat();
        BuildMergedChatRows();
        BuildGameFeed();
    }

    private void RefreshChat()
    {
        var sessionId = ResolveChatSessionId();
        TraceChatDiagnostic($"request command={CommandNames.ChatVisibleFeed} session={sessionId}");
        ChatRows.Clear();
        ChatMessageRows.Clear();
        var chat = _api.ChatVisibleFeed(sessionId, 80);
        var chatItems = ExtractChatItems(chat.Payload, out var sourceKey, out var payloadKeys, out var rawItemsType);
        TraceChatDiagnostic($"response command={CommandNames.ChatVisibleFeed} status={chat.Status} success={(chat.Status == ResponseStatus.Ok)} payloadKeys=[{payloadKeys}] sourceKey={sourceKey} rawItems={chatItems.Count} rawType={rawItemsType}");
        LogFirstChatItemShape(chatItems, CommandNames.ChatVisibleFeed);
        var mappedCount = 0;
        var filteredCount = 0;
        foreach (var item in chatItems)
        {
            var map = AsMap(item, CommandNames.ChatVisibleFeed);
            if (map == null) continue;
            mappedCount++;
            var row = BuildChatMessageRow(map);
            if (row == null)
            {
                filteredCount++;
                continue;
            }

            ChatRows.Add($"{row.Sender}: {row.Text}");
            ChatMessageRows.Add(row);
        }
        TraceChatDiagnostic($"mapped command={CommandNames.ChatVisibleFeed} mappedItems={mappedCount} filteredOut={filteredCount} displayItems={ChatMessageRows.Count}");

        BuildGameFeed();
        BuildMergedChatRows();
        TraceChatDiagnostic($"collection command={CommandNames.ChatVisibleFeed} chatRows={ChatRows.Count} chatMessageRows={ChatMessageRows.Count} uiCollection=GameFeedRows uiCount={GameFeedRows.Count}");
    }

    private void RefreshDiceAndRequests()
    {
        ClientLogService.Instance.Debug("dice.feed.refresh requested");
        DiceFeedRows.Clear();
        DiceMessageRows.Clear();
        var feed = _api.DiceVisibleFeed();
        var feedItems = ToObjectList(feed.Payload.ContainsKey("items") ? feed.Payload["items"] : new ArrayList());
        ClientLogService.Instance.Debug($"dice.feed.refresh itemsRaw={feedItems.Count}");
        var mappedDice = 0;
        var ownDiceCount = 0;
        var commentMapped = false;
        var firstDiceTimestampRaw = string.Empty;
        var firstDiceTimestampMapped = string.Empty;
        var firstDiceComment = string.Empty;
        foreach (var item in feedItems)
        {
            var map = AsMap(item, CommandNames.DiceVisibleFeed);
            if (map == null) continue;
            mappedDice++;
            if (!IsOwnDiceRoll(map))
                continue;

            ownDiceCount++;
            var total = ExtractDiceTotal(map);
            var creator = FirstNonEmpty(GetString(map, "creatorLogin"), GetString(map, "creatorUserId"));
            var isTest = string.Equals(GetString(map, "isTestRoll"), "True", StringComparison.OrdinalIgnoreCase);
            var label = isTest ? "[тест] " : string.Empty;
            var details = BuildDiceRollDetails(map, CommandNames.DiceVisibleFeed);
            var comment = FirstNonEmpty(GetString(map, "description"), GetString(map, "comment"), GetString(map, "note"), GetString(map, "text"));
            var diceText = $"{label}{GetString(map, "formula")} = {total}{details} | {GetString(map, "visibility")}";
            if (!string.IsNullOrWhiteSpace(comment) && diceText.IndexOf(comment, StringComparison.OrdinalIgnoreCase) < 0)
            {
                diceText += $" — {comment}";
                commentMapped = true;
            }
            var createdRaw = FirstNonEmpty(
                GetString(map, "createdUtc"),
                GetString(map, "createdAtUtc"),
                GetString(map, "requestedUtc"),
                GetString(map, "resolvedUtc"),
                GetString(map, "at"));
            var timestampMapped = FormatChatTimestamp(createdRaw);
            var sortTicks = ParseTimelineTicks(createdRaw);
            DiceFeedRows.Add($"{creator}: {diceText}");
            DiceMessageRows.Add(new ChatMessageRowVm
            {
                Sender = creator,
                Text = diceText,
                Timestamp = timestampMapped,
                IsSystem = true,
                SortTicks = sortTicks
            });
            if (string.IsNullOrWhiteSpace(firstDiceTimestampRaw))
            {
                firstDiceTimestampRaw = createdRaw;
                firstDiceTimestampMapped = timestampMapped;
                firstDiceComment = comment;
            }
        }
        if (mappedDice > 0)
        {
            ClientLogService.Instance.Debug($"dice.timeline timestampRaw={firstDiceTimestampRaw}");
            ClientLogService.Instance.Debug($"dice.timeline timestampMapped={firstDiceTimestampMapped}");
            ClientLogService.Instance.Debug($"dice.feed.firstComment={firstDiceComment}");
        }
        ClientLogService.Instance.Debug($"dice.feed.refresh itemsMapped={mappedDice}");
        ClientLogService.Instance.Debug($"dice.feed.refresh ownItems={ownDiceCount}");
        ClientLogService.Instance.Debug($"dice.feed.commentMapped={commentMapped}");

        var previousRequestId = SelectedRequestId;
        var requestRowToRestore = string.Empty;
        var firstRequestRow = string.Empty;
        RequestRows.Clear();
        _requestRowIds.Clear();
        var req = _api.ListMyRequests();
        foreach (var item in ToObjectList(req.Payload.ContainsKey("items") ? req.Payload["items"] : new ArrayList()))
        {
            var map = AsMap(item, CommandNames.PlayerRequestListMine);
            if (map == null) continue;
            var requestId = GetString(map, "requestId");
            var requestNumber = FormatRequestNumberForDisplay(GetString(map, "requestNumber"), GetString(map, "displayRequestId"), GetString(map, "requestNumberLabel"));
            var status = GetString(map, "status");
            if (!string.IsNullOrWhiteSpace(PlayerRequestStatusFilterInput)
                && !string.Equals(PlayerRequestStatusFilterInput, "all", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(PlayerRequestStatusFilterInput, status, StringComparison.OrdinalIgnoreCase))
                continue;
            var title = FirstNonEmpty(GetString(map, "title"), GetString(map, "name"), GetString(map, "description"), "");
            var details = FirstNonEmpty(GetString(map, "details"), GetString(map, "description"));
            var response = FirstNonEmpty(GetString(map, "decisionCommentPlayerVisible"), GetString(map, "gmResponse"));
            var statusText = FirstNonEmpty(GetString(map, "playerVisibleStatusText"), status);
            var submittedBy = FirstNonEmpty(GetString(map, "submittedByDisplayName"), GetString(map, "createdByDisplayName"), "—");
            var lastAction = FirstNonEmpty(GetString(map, "lastActionDisplayText"), "Действие: —");
            var actorText = $"Отправил: {submittedBy}; {lastAction}";
            var suffix = string.IsNullOrWhiteSpace(response) ? string.Empty : $" | Решение GM: {response}";
            var rowText = $"{requestNumber} | {status} | {statusText} | {title} | {actorText} | {details}{suffix}";
            if (string.IsNullOrWhiteSpace(firstRequestRow)) firstRequestRow = rowText;
            if (!string.IsNullOrWhiteSpace(requestId)) _requestRowIds[rowText] = requestId;
            if (!string.IsNullOrWhiteSpace(previousRequestId) && string.Equals(previousRequestId, requestId, StringComparison.OrdinalIgnoreCase))
                requestRowToRestore = rowText;
            RequestRows.Add(rowText);
        }

        var currentTest = _api.DiceTestGetCurrent();
        ClientLogService.Instance.Info($"dice.test.getCurrent.status={currentTest.Status}");
        ClientLogService.Instance.Debug($"dice.feed.render visibleRows={DiceFeedRows.Count}");
        ClientLogService.Instance.Debug($"dice.view.counts feed={DiceFeedRows.Count} requests={RequestRows.Count}");

        EnsureCollectionPlaceholder(DiceFeedRows, "У вас пока нет бросков.");
        EnsureCollectionPlaceholder(RequestRows, "Заявок пока нет.");
        if (!IsPlaceholderText(RequestRows.FirstOrDefault() ?? string.Empty))
        {
            var rowToSelect = FirstNonEmpty(requestRowToRestore, RequestRows.Count == 1 ? firstRequestRow : string.Empty);
            if (!string.IsNullOrWhiteSpace(rowToSelect))
                SelectedRequestRow = rowToSelect;
        }
    }

    private void RefreshCombatEvents()
    {
        EventRows.Clear();
        var timeline = _api.CombatTimeline(ChatSessionId);
        foreach (var item in ToObjectList(timeline.Payload.ContainsKey("items") ? timeline.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            EventRows.Add($"{GetString(map, "at")} | {GetString(map, "eventType")} | {GetString(map, "message")}");
        }

        SessionStateRows.Clear();
        var state = _api.CombatVisibleState(ChatSessionId);
        SessionStateRows.Add("Статус сессии: " + GetString(state.Payload, "status"));
        SessionStateRows.Add("Раунд: " + GetString(state.Payload, "round"));
        SessionStateRows.Add("Активный слот: " + GetString(state.Payload, "activeSlotId"));

        EnsureCollectionPlaceholder(EventRows, "Событий пока нет.");
    }

    private void RefreshCombatSnapshot()
    {
        if (string.IsNullOrWhiteSpace(CombatEncounterId))
        {
            CombatWarningMessage = "Укажите идентификатор боя.";
            return;
        }

        CombatIsLoading = true;
        CombatErrorMessage = string.Empty;
        CombatWarningMessage = string.Empty;
        ClientLogService.Instance.Info("player.combat.snapshot.refresh.start");
        try
        {
            var payload = new Dictionary<string, object>
            {
                { "encounterId", CombatEncounterId.Trim() },
                { "characterId", FirstNonEmpty(CombatCharacterIdInput, ActiveCharacterId, SelectedCharacterId) },
                { "participantId", CombatParticipantId },
                { "includePublicParticipants", true },
                { "includePublicLog", true },
                { "limitLog", 100 }
            };
            var response = _api.CombatV1PlayerSnapshot(payload);
            if (response.Status != ResponseStatus.Ok)
            {
                CombatErrorMessage = response.Message;
                ClientLogService.Instance.Warn($"player.combat.snapshot.refresh.error status={response.Status} message={response.Message}");
                return;
            }

            ApplyCombatSnapshot(response.Payload);
            ClientLogService.Instance.Info("player.combat.snapshot.refresh.done");
        }
        catch (Exception ex)
        {
            CombatErrorMessage = ex.Message;
            ClientLogService.Instance.Error("player.combat.snapshot.refresh.error", ex);
        }
        finally
        {
            CombatIsLoading = false;
        }
    }

    private void RefreshCombatFeed()
    {
        if (string.IsNullOrWhiteSpace(CombatEncounterId))
        {
            CombatWarningMessage = "Укажите идентификатор боя.";
            return;
        }

        CombatIsLoading = true;
        CombatErrorMessage = string.Empty;
        ClientLogService.Instance.Info("player.combat.feed.refresh.start");
        try
        {
            var response = _api.CombatV1PlayerFeed(new Dictionary<string, object>
            {
                { "encounterId", CombatEncounterId.Trim() },
                { "characterId", FirstNonEmpty(CombatCharacterIdInput, ActiveCharacterId, SelectedCharacterId) },
                { "participantId", CombatParticipantId },
                { "limit", 100 }
            });
            if (response.Status != ResponseStatus.Ok)
            {
                CombatWarningMessage = response.Message;
                ClientLogService.Instance.Warn($"player.combat.feed.refresh.error status={response.Status} message={response.Message}");
                return;
            }

            CombatPublicLog.Clear();
            foreach (var raw in ToObjectList(response.Payload.ContainsKey("items") ? response.Payload["items"] : new ArrayList()))
            {
                var map = AsMap(raw, CommandNames.CombatV1PlayerFeed);
                if (map == null) continue;
                CombatPublicLog.Add(BuildCombatLog(map));
            }

            CombatLastRefreshText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            ClientLogService.Instance.Info("player.combat.feed.refresh.done");
        }
        catch (Exception ex)
        {
            CombatErrorMessage = ex.Message;
            ClientLogService.Instance.Error("player.combat.feed.refresh.error", ex);
        }
        finally
        {
            CombatIsLoading = false;
        }
    }

    private void ApplyCombatSnapshot(Dictionary<string, object> payload)
    {
        var encounter = payload.ContainsKey("encounter") ? AsMap(payload["encounter"], CommandNames.CombatV1PlayerSnapshot) : null;
        if (encounter != null)
        {
            CombatEncounterName = FirstNonEmpty(GetString(encounter, "name"), GetString(encounter, "encounterId"), "Combat");
            CombatEncounterStatus = $"{GetString(encounter, "status")} | раунд {GetInt(encounter, "roundNumber")} | ход {GetInt(encounter, "activeTurnIndex")}";
        }

        var currentTurn = payload.ContainsKey("currentTurn") ? AsMap(payload["currentTurn"], CommandNames.CombatV1PlayerSnapshot) : null;
        if (currentTurn != null)
        {
            CombatIsMyTurn = GetBool(currentTurn, "isMyTurn");
            CombatCurrentTurnText = $"Раунд {GetInt(currentTurn, "roundNumber")}, ход {GetInt(currentTurn, "turnIndex")}: {GetString(currentTurn, "activeParticipantName")}";
        }

        var myParticipantMap = payload.ContainsKey("myParticipant") ? AsMap(payload["myParticipant"], CommandNames.CombatV1PlayerSnapshot) : null;
        CombatMyParticipant = myParticipantMap == null ? null : BuildCombatParticipant(myParticipantMap);

        CombatKnownConditions.Clear();
        if (CombatMyParticipant != null)
        {
            foreach (var condition in CombatMyParticipant.KnownConditions)
                CombatKnownConditions.Add(condition);
        }

        CombatParticipants.Clear();
        foreach (var raw in ToObjectList(payload.ContainsKey("participants") ? payload["participants"] : new ArrayList()))
        {
            var map = AsMap(raw, CommandNames.CombatV1PlayerSnapshot);
            if (map == null) continue;
            CombatParticipants.Add(BuildCombatParticipant(map));
        }

        CombatPublicLog.Clear();
        foreach (var raw in ToObjectList(payload.ContainsKey("publicLog") ? payload["publicLog"] : new ArrayList()))
        {
            var map = AsMap(raw, CommandNames.CombatV1PlayerSnapshot);
            if (map == null) continue;
            CombatPublicLog.Add(BuildCombatLog(map));
        }

        var warnings = ToObjectList(payload.ContainsKey("warnings") ? payload["warnings"] : new ArrayList()).Cast<object?>()
            .Select(x => x?.ToString() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        CombatWarningMessage = warnings.Length > 0 ? string.Join("; ", warnings) : string.Empty;
        CombatLastRefreshText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private PlayerCombatParticipantVm BuildCombatParticipant(Dictionary<string, object> map)
    {
        var participant = new PlayerCombatParticipantVm
        {
            ParticipantId = GetString(map, "participantId"),
            CharacterId = GetString(map, "characterId"),
            DisplayName = FirstNonEmpty(GetString(map, "displayName"), GetString(map, "participantId")),
            TeamId = GetString(map, "teamId"),
            ParticipantType = GetString(map, "participantType"),
            IsCurrentTurn = GetBool(map, "isCurrentTurn"),
            IsActive = GetBool(map, "isActive"),
            IsDefeated = GetBool(map, "isDefeated"),
            CurrentHealth = GetInt(map, "currentHealth"),
            MaxHealth = GetInt(map, "maxHealth"),
            TemporaryHealth = GetInt(map, "temporaryHealth"),
            CurrentMorale = GetInt(map, "currentMorale"),
            MaxMorale = GetInt(map, "maxMorale"),
            VisibilityState = GetString(map, "visibilityState")
        };

        foreach (var raw in ToObjectList(map.ContainsKey("knownConditions") ? map["knownConditions"] : new ArrayList()))
        {
            var condition = AsMap(raw, CommandNames.CombatV1PlayerSnapshot);
            if (condition == null) continue;
            participant.KnownConditions.Add(new PlayerCombatConditionVm
            {
                ConditionDefinitionId = GetString(condition, "conditionDefinitionId"),
                DisplayName = FirstNonEmpty(GetString(condition, "displayName"), GetString(condition, "conditionDefinitionId")),
                Severity = GetString(condition, "severity"),
                StackCount = GetInt(condition, "stackCount"),
                RemainingRounds = GetInt(condition, "remainingRounds"),
                IsPositive = GetBool(condition, "isPositive"),
                IsNegative = GetBool(condition, "isNegative")
            });
        }

        return participant;
    }

    private PlayerCombatLogVm BuildCombatLog(Dictionary<string, object> map)
    {
        var createdRaw = FirstNonEmpty(GetString(map, "createdAtUtc"), GetString(map, "createdUtc"));
        return new PlayerCombatLogVm
        {
            CreatedAtText = FormatChatTimestamp(createdRaw),
            RoundNumber = GetInt(map, "roundNumber"),
            TurnIndex = GetInt(map, "turnIndex"),
            EventType = GetString(map, "eventType"),
            Message = GetString(map, "message")
        };
    }

    private string ConnectionSettingsPath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nri.PlayerClient");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "connection.settings.json");
        }
    }

    private string AudioSettingsPath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nri.PlayerClient");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "audio.settings.json");
        }
    }

    private string LocalNotesPath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nri.PlayerClient");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "local.notes.json");
        }
    }


    private void LoadConnectionSettings()
    {
        try
        {
            if (File.Exists(ConnectionSettingsPath))
            {
                var map = JsonProtocolSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(ConnectionSettingsPath));
                if (map != null)
                {
                    ServerHostInput = GetStringOrFallback(map, "serverHost", "127.0.0.1");
                    ServerPortInput = GetStringOrFallback(map, "serverPort", "4600");
                    LastServerHost = ServerHostInput;
                    int.TryParse(ServerPortInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var loadedPort);
                    LastServerPort = loadedPort <= 0 ? 4600 : loadedPort;
                }
            }
            else
            {
                ServerHostInput = _clientConfig.ServerHost;
                ServerPortInput = _clientConfig.ServerPort.ToString(CultureInfo.InvariantCulture);
                LastServerHost = ServerHostInput;
                LastServerPort = _clientConfig.ServerPort;
                Notify(nameof(LastServerHost));
                Notify(nameof(LastServerPort));
            }

            _client.UpdateEndpoint(ServerHostInput, LastServerPort);
            Notify(nameof(ServerHostInput));
            Notify(nameof(ServerPortInput));
            Notify(nameof(ConnectedEndpointDisplay));
        }
        catch
        {
            ServerHostInput = "127.0.0.1";
            ServerPortInput = "4600";
            LastServerHost = ServerHostInput;
            LastServerPort = 4600;
        }
    }

    private void SaveConnectionSettings()
    {
        File.WriteAllText(ConnectionSettingsPath, JsonProtocolSerializer.Serialize(new Dictionary<string, object>
        {
            { "serverHost", ServerHostInput },
            { "serverPort", ServerPortInput }
        }));
    }

    private void ConnectToServer()
    {
        ApplyConnectionSettings();
    }

    private void ApplyConnectionSettings()
    {
        if (!TryValidateConnectionSettings(out var host, out var port, out var message))
        {
            SetDisconnectedState(message);
            return;
        }

        try
        {
            _client.UpdateEndpoint(host, port);
            _client.Connect();
            ServerHostInput = host;
            ServerPortInput = port.ToString(CultureInfo.InvariantCulture);
            LastServerHost = host;
            LastServerPort = port;
            Notify(nameof(LastServerHost));
            Notify(nameof(LastServerPort));
            SaveConnectionSettings();
            SetConnectedState();
            IsConnectionPopupOpen = false;
            Notify(nameof(ServerHostInput));
            Notify(nameof(ServerPortInput));
            RefreshConnectionSummary();
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void ResetConnectionDefaults()
    {
        ServerHostInput = "127.0.0.1";
        ServerPortInput = "4600";
        Notify(nameof(ServerHostInput));
        Notify(nameof(ServerPortInput));
        Notify(nameof(ConnectedEndpointDisplay));
    }

    private void UseSavedConnectionSettings()
    {
        ServerHostInput = LastServerHost;
        ServerPortInput = LastServerPort.ToString(CultureInfo.InvariantCulture);
        Notify(nameof(ServerHostInput));
        Notify(nameof(ServerPortInput));
        Notify(nameof(ConnectedEndpointDisplay));
    }

    private bool TryValidateConnectionSettings(out string host, out int port, out string error)
    {
        host = (ServerHostInput ?? string.Empty).Trim();
        error = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(host))
        {
            error = "Нет текста сообщения";
            return false;
        }

        if (!string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) && !IPAddress.TryParse(host, out _))
        {
            error = "Нет активного персонажа";
            return false;
        }

        if (!int.TryParse(ServerPortInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
        {
            error = "Нет подключения";
            return false;
        }

        return true;
    }

    private void EnsureConnected()
    {
        if (_client.ServerHost != ServerHostInput || _client.ServerPort.ToString(CultureInfo.InvariantCulture) != ServerPortInput)
            ApplyConnectionSettings();
        else
            _client.Connect();
    }

    private void SetConnectedState()
    {
        ConnectionState = "Онлайн";
        ConnectionStatusDetail = $"Подключено к {ServerHostInput}:{ServerPortInput}";
        ClientLogService.Instance.Info("ui.player.dice.button state=enabled reason=ready");
        RefreshConnectionSummary();
        Notify(nameof(ConnectionStatusDetail));
    }

    private void SetDisconnectedState(string message)
    {
        _client.Disconnect();
        ConnectionState = "Оффлайн";
        ConnectionStatusDetail = message;
        ClientLogService.Instance.Info("ui.player.dice.button state=disabled reason=not_authenticated");
        RefreshConnectionSummary();
        Notify(nameof(ConnectionStatusDetail));
    }

    private void SetConnectionError(Exception ex)
    {
        var message = ex switch
        {
            SocketException => "Нет соединения",
            TimeoutException => "Сервер не ответил вовремя: timeout",
            InvalidOperationException => "Операция сейчас недоступна",
            _ => string.IsNullOrWhiteSpace(ex.Message) ? "Неизвестная ошибка подключения" : ex.Message
        };
        ClientLogService.Instance.Error("Connection error", ex);
        SetDisconnectedState(message);
    }

    private static bool IsConnectionLevelException(Exception ex)
    {
        return ex is SocketException
               || ex is TimeoutException
               || ex is IOException;
    }

    private static bool LooksLikeUnauthorized(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return message.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("auth token is invalid", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("invalid token", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0
                  && message.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void HandleUnauthorizedState(string source, string? message)
    {
        _poller.Stop();
        _session.AuthToken = null;
        PlayerDisplayName = "Гость";
        Notify(nameof(PlayerDisplayName));
        Notify(nameof(IsAuthenticated));
        ClientLogService.Instance.Warn($"player.auth.unauthorized source={source} message={message}");
        SetDisconnectedState("Сессия истекла. Войдите снова.");
    }

    private void RefreshConnectionSummary()
    {
        SessionSummary = $"Сервер: {ServerHostInput}:{ServerPortInput}";
        Notify(nameof(SessionSummary));
        Notify(nameof(ConnectedEndpointDisplay));
    }

    private void RefreshAudioState()
    {
        var state = _api.AudioPlayerStateGet(AudioSessionId);
        if (state.Status != ResponseStatus.Ok)
        {
            AudioStatusText = state.Message;
            AudioStateText = state.Message;
            NotifyAudioStateProperties();
            return;
        }

        var category = PlayerDevelopmentGraphDisplay.ToReadableText(FirstNonEmpty(GetString(state.Payload, "currentCategory"), GetString(state.Payload, "category"), "—"));
        var track = PlayerDevelopmentGraphDisplay.ToReadableText(FirstNonEmpty(GetString(state.Payload, "trackDisplayName"), GetString(state.Payload, "trackName"), "Трек не выбран"));
        var playback = PlayerDevelopmentGraphDisplay.ToReadableText(FirstNonEmpty(GetString(state.Payload, "playbackStateText"), GetString(state.Payload, "playbackState"), "—"));
        AudioCurrentCategory = category;
        AudioCurrentTrackTitle = track;
        AudioPlaybackStateText = playback;
        AudioStateText = $"Категория: {category}; трек: {track}; состояние: {playback}";

        AudioVisibleTrackRows.Clear();
        var visibleTracks = _api.AudioPlayerTracksVisible(AudioSessionId);
        if (visibleTracks.Status == ResponseStatus.Ok && visibleTracks.Payload.ContainsKey("items"))
        {
            foreach (var item in ToObjectList(visibleTracks.Payload["items"]))
                if (AsMap(item, CommandNames.AudioPlayerTracksVisible) is { } m)
                    AudioVisibleTrackRows.Add($"{PlayerDevelopmentGraphDisplay.ToReadableText(GetString(m, "category"))} | {PlayerDevelopmentGraphDisplay.ToReadableText(GetString(m, "displayName"))}");
        }

        AudioStatusText = "Музыка обновлена.";
        NotifyAudioStateProperties();
    }

    private void LoadLocalAudioSettings()
    {
        try
        {
            if (!File.Exists(AudioSettingsPath))
                return;

            var map = JsonProtocolSerializer.Deserialize<Dictionary<string, object>>(
             File.ReadAllText(AudioSettingsPath));

            if (map == null)
                return;

            double volume = 0.7;
            bool muted = false;

            if (map.ContainsKey("volume"))
                double.TryParse(Convert.ToString(map["volume"]), out volume);

            if (map.ContainsKey("muted"))
                bool.TryParse(Convert.ToString(map["muted"]), out muted);

            LocalVolume = Math.Max(0, Math.Min(1, volume));
            LocalMuted = muted;
            Notify(nameof(LocalVolume));
            Notify(nameof(LocalMuted));
        }
        catch
        {

        }
    }

    private void ApplyAudioLocalSettings()
    {
        var volume = Math.Max(0, Math.Min(1, LocalVolume));
        LocalVolume = volume;
        var response = _api.AudioPlayerClientSettingsUpdate(AudioSessionId, volume, LocalMuted);
        AudioStatusText = response.Status == ResponseStatus.Ok ? "Локальные настройки музыки сохранены." : response.Message;
        try
        {
            File.WriteAllText(AudioSettingsPath, JsonProtocolSerializer.Serialize(new Dictionary<string, object>
            {
                { "volume", LocalVolume },
                { "muted", LocalMuted }
            }));
        }
        catch { }

        RefreshAudioState();
    }

    private void NotifyAudioStateProperties()
    {
        Notify(nameof(AudioStateText));
        Notify(nameof(AudioCurrentTrackTitle));
        Notify(nameof(AudioCurrentCategory));
        Notify(nameof(AudioPlaybackStateText));
        Notify(nameof(AudioStatusText));
        Notify(nameof(AudioVisibleTrackRows));
        Notify(nameof(LocalVolume));
        Notify(nameof(LocalMuted));
    }

    private void LoadVisibility()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        var r = _api.VisibilityGet(SelectedCharacterId);
        VisHideDescription = GetString(r.Payload, "hideDescriptionForOthers") == "True";
        VisHideBackstory = GetString(r.Payload, "hideBackstoryForOthers") == "True";
        VisHideStats = GetString(r.Payload, "hideStatsForOthers") == "True";
        VisHideReputation = GetString(r.Payload, "hideReputationForOthers") == "True";
        Notify(nameof(VisHideDescription));
        Notify(nameof(VisHideBackstory));
        Notify(nameof(VisHideStats));
        Notify(nameof(VisHideReputation));
    }

    private void SaveVisibility()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        _api.VisibilityUpdate(new Dictionary<string, object>
        {
            { "characterId", SelectedCharacterId },
            { "hideDescriptionForOthers", VisHideDescription },
            { "hideBackstoryForOthers", VisHideBackstory },
            { "hideStatsForOthers", VisHideStats },
            { "hideReputationForOthers", VisHideReputation }
        });
    }

    private void LoadPublicCharacter()
    {
        PublicCharacterRows.Clear();
        InitializeDefaultPublicProfile();
        var characterId = ResolvePublicProfileCharacterId();
        if (string.IsNullOrWhiteSpace(characterId)) return;
        PublicViewCharacterId = characterId;
        Notify(nameof(PublicViewCharacterId));
        ClientLogService.Instance.Info($"publicProfile.request characterId={characterId}");
        var r = _api.CharacterPublicViewGet(characterId);
        ClientLogService.Instance.Info($"publicProfile.result status={r.Status} message={r.Message}");
        if (r.Status != ResponseStatus.Ok) return;
        foreach (var kv in r.Payload)
            PublicCharacterRows.Add(kv.Key + " = " + Convert.ToString(kv.Value));

        PublicProfileName = string.IsNullOrWhiteSpace(GetString(r.Payload, "name")) ? "Персонаж без имени" : GetString(r.Payload, "name");
        PublicProfileSubtitle = string.IsNullOrWhiteSpace(GetString(r.Payload, "race")) ? "Раса не указана" : $"Раса: {GetString(r.Payload, "race")}";
        PublicProfileStatusText = "Публичный профиль загружен";
        PublicProfileHintText = string.IsNullOrWhiteSpace(PublicViewCharacterId) ? "Введите CharacterId и откройте профиль" : $"CharacterId: {PublicViewCharacterId}";
        PublicProfileDescription = GetStringOrFallback(r.Payload, "backstory", "Предыстория не раскрыта.");

        AddPublicProfileField(PublicProfileIdentityRows, "Имя", PublicProfileName);
        AddPublicProfileField(PublicProfileIdentityRows, "Раса", GetStringOrFallback(r.Payload, "race", "Не указана"));
        AddPublicProfileField(PublicProfileIdentityRows, "Возраст", GetStringOrFallback(r.Payload, "age", "Не указан"));
        AddPublicProfileField(PublicProfileIdentityRows, "Рост", GetStringOrFallback(r.Payload, "height", "Не указан"));
        AddPublicProfileField(PublicProfileSummaryRows, "Описание", PublicProfileDescription);
        AddPublicProfileField(PublicProfileSummaryRows, "Монеты опыта", GetStringOrFallback(r.Payload, "xpCoins", "нет данных"));
        var publicStats = r.Payload.TryGetValue("stats", out var statsRaw)
            ? AsMap(statsRaw, CommandNames.CharacterPublicViewGet)
            : null;
        if (publicStats != null)
        {
            var statsText = $"HP {GetMapValueOrDefault(publicStats, "health")}, сила {GetMapValueOrDefault(publicStats, "strength")}, ловкость {GetMapValueOrDefault(publicStats, "dexterity")}";
            AddPublicProfileField(PublicProfileSummaryRows, "Характеристики", statsText);
        }
        else
        {
            AddPublicProfileField(PublicProfileSummaryRows, "Характеристики", GetStringOrFallback(r.Payload, "statsSummary", "Не раскрыты"));
        }

        foreach (var hiddenKey in new[] { "hiddenFields", "hidden", "blockedFields" })
        {
            if (!r.Payload.ContainsKey(hiddenKey)) continue;
            foreach (var item in ToObjectList(r.Payload[hiddenKey]))
                PublicProfileHiddenRows.Add(Convert.ToString(item) ?? string.Empty);
        }

        if (PublicProfileHiddenRows.Count == 0)
            PublicProfileHiddenRows.Add("Часть данных скрыта настройками видимости.");

        NotifyPublicProfile();
        var fieldsLoaded = PublicProfileIdentityRows.Count + PublicProfileSummaryRows.Count;
        ClientLogService.Instance.Info($"publicProfile.render fieldsLoaded={fieldsLoaded}");
    }

    private string ResolvePublicProfileCharacterId()
    {
        var input = (PublicViewCharacterId ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(input))
        {
            var byId = MyCharacters.FirstOrDefault(character => string.Equals(character.Id, input, StringComparison.Ordinal));
            if (byId != null) return byId.Id;
            if (input.Length >= 8 && input.Length <= 128) return input;
            var byName = MyCharacters.FirstOrDefault(character => string.Equals(character.Name, input, StringComparison.OrdinalIgnoreCase));
            if (byName != null) return byName.Id;
        }

        if (SelectedMyCharacter != null && !string.IsNullOrWhiteSpace(SelectedMyCharacter.Id))
            return SelectedMyCharacter.Id;
        if (!string.IsNullOrWhiteSpace(SelectedCharacterId))
            return SelectedCharacterId;
        return string.Empty;
    }

    private void RefreshNotes()
    {
        // Player notes in 0.11.7 are local-only (per user + character).
        RefreshLocalNotesForCurrentCharacter();

        NoteRows.Clear();
        AdminNoteRows.Clear();
        foreach (var note in LocalNotes)
        {
            NoteRows.Add($"{note.UpdatedAtLocalText} | {note.Title} | {note.Preview}");
        }

        EnsureCollectionPlaceholder(NoteRows, "Локальных заметок пока нет.");
    }

    private void LoadLocalNotesStore()
    {
        _localNotesStore.Clear();
        try
        {
            if (!File.Exists(LocalNotesPath))
                return;

            var data = JsonProtocolSerializer.Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(LocalNotesPath));
            if (data == null) return;
            foreach (var map in data)
            {
                _localNotesStore.Add(new PlayerLocalNoteVm
                {
                    Id = FirstNonEmpty(GetString(map, "id"), Guid.NewGuid().ToString("N")),
                    UserKey = FirstNonEmpty(GetString(map, "userKey"), "guest"),
                    CharacterId = FirstNonEmpty(GetString(map, "characterId"), string.Empty),
                    Title = FirstNonEmpty(GetString(map, "title"), "Заметка"),
                    Text = GetString(map, "text"),
                    UpdatedAtUtc = FirstNonEmpty(GetString(map, "updatedAtUtc"), DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
                    UpdatedAtLocalText = FirstNonEmpty(GetString(map, "updatedAtLocalText"), DateTime.Now.ToString("g", CultureInfo.CurrentCulture))
                });
            }
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Warn($"local.notes.load.error message={ex.Message}");
        }
    }

    private void SaveLocalNotesStore()
    {
        try
        {
            var payload = _localNotesStore.Select(note => new Dictionary<string, object>
            {
                { "id", note.Id },
                { "userKey", note.UserKey },
                { "characterId", note.CharacterId },
                { "title", note.Title },
                { "text", note.Text },
                { "updatedAtUtc", note.UpdatedAtUtc },
                { "updatedAtLocalText", note.UpdatedAtLocalText }
            }).ToList();
            File.WriteAllText(LocalNotesPath, JsonProtocolSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Warn($"local.notes.save.error message={ex.Message}");
        }
    }

    private string BuildLocalNotesUserKey()
    {
        return FirstNonEmpty(PlayerDisplayName, LoginText, "guest").Trim().ToLowerInvariant();
    }

    private string ResolveLocalNotesCharacterId()
    {
        return FirstNonEmpty(SelectedCharacterId, ActiveCharacterId, SelectedMyCharacter?.Id, string.Empty);
    }

    private void RefreshLocalNotesForCurrentCharacter()
    {
        LocalNotes.Clear();
        var userKey = BuildLocalNotesUserKey();
        var characterId = ResolveLocalNotesCharacterId();

        var filtered = _localNotesStore
            .Where(note => string.Equals(note.UserKey, userKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(note.CharacterId, characterId, StringComparison.Ordinal))
            .OrderByDescending(note => note.UpdatedAtUtc, StringComparer.Ordinal)
            .ToList();

        foreach (var note in filtered)
            LocalNotes.Add(note);

        if (LocalNotes.Count == 0)
        {
            LocalNoteStatusText = "Локальных заметок пока нет.";
            SelectedLocalNote = null;
            Notify(nameof(LocalNoteStatusText));
            return;
        }

        LocalNoteStatusText = $"Локальных заметок: {LocalNotes.Count}";
        Notify(nameof(LocalNoteStatusText));
        if (SelectedLocalNote == null || !LocalNotes.Any(note => note.Id == SelectedLocalNote.Id))
            SelectedLocalNote = LocalNotes[0];
    }

    private void AddLocalNote()
    {
        var characterId = ResolveLocalNotesCharacterId();
        if (string.IsNullOrWhiteSpace(characterId))
        {
            LocalNoteStatusText = "Локальных заметок пока нет.";
            Notify(nameof(LocalNoteStatusText));
            return;
        }

        var text = (LocalNoteText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            LocalNoteStatusText = "Заметки загружены.";
            Notify(nameof(LocalNoteStatusText));
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var note = new PlayerLocalNoteVm
        {
            Id = Guid.NewGuid().ToString("N"),
            UserKey = BuildLocalNotesUserKey(),
            CharacterId = characterId,
            Title = FirstNonEmpty((LocalNoteTitle ?? string.Empty).Trim(), "Заметка"),
            Text = text,
            UpdatedAtUtc = nowUtc.ToString("O", CultureInfo.InvariantCulture),
            UpdatedAtLocalText = DateTime.Now.ToString("g", CultureInfo.CurrentCulture)
        };
        _localNotesStore.Add(note);
        SaveLocalNotesStore();
        RefreshLocalNotesForCurrentCharacter();
        SelectedLocalNote = LocalNotes.FirstOrDefault(local => local.Id == note.Id) ?? LocalNotes.FirstOrDefault();
        LocalNoteStatusText = " .";
        Notify(nameof(LocalNoteStatusText));
    }

    private void SaveSelectedLocalNote()
    {
        if (SelectedLocalNote == null)
        {
            AddLocalNote();
            return;
        }

        var note = _localNotesStore.FirstOrDefault(local => local.Id == SelectedLocalNote.Id);
        if (note == null)
        {
            AddLocalNote();
            return;
        }

        note.Title = FirstNonEmpty((LocalNoteTitle ?? string.Empty).Trim(), "Заметка");
        note.Text = (LocalNoteText ?? string.Empty).Trim();
        note.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        note.UpdatedAtLocalText = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);
        SaveLocalNotesStore();
        RefreshLocalNotesForCurrentCharacter();
        SelectedLocalNote = LocalNotes.FirstOrDefault(local => local.Id == note.Id) ?? LocalNotes.FirstOrDefault();
        LocalNoteStatusText = "Заметка сохранена.";
        Notify(nameof(LocalNoteStatusText));
    }

    private void DeleteSelectedLocalNote()
    {
        if (SelectedLocalNote == null) return;
        var removed = _localNotesStore.RemoveAll(local => local.Id == SelectedLocalNote.Id);
        if (removed <= 0) return;
        SaveLocalNotesStore();
        SelectedLocalNote = null;
        LocalNoteTitle = string.Empty;
        LocalNoteText = string.Empty;
        Notify(nameof(LocalNoteTitle));
        Notify(nameof(LocalNoteText));
        RefreshLocalNotesForCurrentCharacter();
        LocalNoteStatusText = "Заметка удалена.";
        Notify(nameof(LocalNoteStatusText));
    }

    private void ClearLocalNoteEditor()
    {
        SelectedLocalNote = null;
        LocalNoteTitle = string.Empty;
        LocalNoteText = string.Empty;
        Notify(nameof(LocalNoteTitle));
        Notify(nameof(LocalNoteText));
        LocalNoteStatusText = "Заметка удалена.";
        Notify(nameof(LocalNoteStatusText));
    }

    private void CreateNote()
    {
        AddLocalNote();
    }

    private void ArchiveNote()
    {
        DeleteSelectedLocalNote();
    }

    private void LoadClassAndSkills()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;

        var tree = _api.DevelopmentPlayerHexagonGet(SelectedCharacterId);
        var available = new HashSet<string>();
        var acquired = new HashSet<string>();
        var serverNodes = new List<ClassNodeVisualVm>();
        _developmentViewerHexagonPayloads.Clear();
        StoreDevelopmentViewerHexagonPayloads(tree.Payload, DevelopmentHexagonIds.Main);
        var serverHexagons = ExtractDevelopmentHexagons(tree.Payload);
        DevelopmentHexagons.Clear();
        foreach (var hexagon in serverHexagons.OrderBy(h => h.SortOrder).ThenBy(h => h.Name))
            DevelopmentHexagons.Add(hexagon);
        if (DevelopmentHexagons.Count == 0)
        {
            DevelopmentHexagons.Add(new DevelopmentHexagonVm
            {
                HexagonId = DevelopmentHexagonIds.Main,
                HexagonType = DevelopmentHexagonTypes.Main,
                Name = "Основной шестиугольник развития",
                CenterNodeId = "novice",
                SortOrder = 1
            });
        }
        if (!DevelopmentHexagons.Any(h => string.Equals(h.HexagonId, SelectedDevelopmentHexagonId, StringComparison.OrdinalIgnoreCase)))
            SelectedDevelopmentHexagonId = DevelopmentHexagons.First().HexagonId;

        var hexagonItems = ExtractDevelopmentHexagonItems(tree.Payload, out var hexagonRawCollectionKey, out var hexagonRawItemsType);
        foreach (var item in hexagonItems)
        {
            var map = AsMap(item, CommandNames.DevelopmentPlayerHexagonGet);
            if (map == null) continue;
            var nodeId = GetString(map, "nodeId");
            if (string.IsNullOrWhiteSpace(nodeId)) continue;
            if (GetBool(map, "acquired") || GetBool(map, "isPurchased")) acquired.Add(nodeId);
            else if (GetBool(map, "available") || GetBool(map, "canPurchase")) available.Add(nodeId);
            var title = FirstNonEmpty(GetString(map, "name"), nodeId);
            var stateRaw = FirstNonEmpty(GetString(map, "state"), GetString(map, "status"));
            var state = nodeId == "novice" ? "Start" : FormatDevelopmentNodeState(stateRaw, acquired.Contains(nodeId), available.Contains(nodeId));
            var cost = ParseInt(FirstNonEmpty(GetString(map, "cost"), GetString(map, "costExperienceCoins")), 0);
            var requirements = FirstNonEmpty(GetString(map, "requirementSummary"), JoinObjectList(map, "reasons"), "Нет требований.");
            var reward = FirstNonEmpty(GetString(map, "rewardSummary"), GetString(map, "classDisplayName"));
            var currencyId = FirstNonEmpty(GetString(map, "currencyId"), CharacterCurrencyIds.XpCoin);
            var positionX = ParseInt(FirstNonEmpty(GetString(map, "positionX"), GetString(map, "gridX")), 190);
            var positionY = ParseInt(FirstNonEmpty(GetString(map, "positionY"), GetString(map, "gridY")), 120);
            var requiredNodeIds = JoinObjectList(map, "requiredNodeIds");
            if (string.IsNullOrWhiteSpace(requiredNodeIds)) requiredNodeIds = JoinObjectList(map, "linkedNodeIds");
            var visualNode = new ClassNodeVisualVm
            {
                NodeId = nodeId,
                HexagonId = FirstNonEmpty(GetString(map, "hexagonId"), DevelopmentHexagonIds.Main),
                HexagonName = PlayerDevelopmentGraphDisplay.ToReadableText(FirstNonEmpty(GetString(map, "hexagonName"), "Основной шестиугольник развития")),
                NodeTypeLabel = PlayerDevelopmentGraphDisplay.ToReadableType(FirstNonEmpty(GetString(map, "nodeTypeLabel"), GetString(map, "nodeType"), "Узел развития")),
                DirectionKey = FirstNonEmpty(GetString(map, "canonicalDirectionId"), GetString(map, "directionCode"), GetString(map, "directionId")),
                BranchKey = FirstNonEmpty(GetString(map, "canonicalBranchId"), GetString(map, "branchCode"), GetString(map, "branchId")),
                Title = PlayerDevelopmentGraphDisplay.ToReadableNodeTitle(title, nodeId),
                State = PlayerDevelopmentGraphDisplay.ToReadableState(state),
                CostExperienceCoins = cost,
                CostText = PlayerDevelopmentGraphDisplay.ToReadableCost(cost, currencyId),
                Summary = PlayerDevelopmentGraphDisplay.ToReadableText(FirstNonEmpty(GetString(map, "description"), reward)),
                RequirementSummary = PlayerDevelopmentGraphDisplay.ToReadableText(requirements),
                RewardSummary = PlayerDevelopmentGraphDisplay.ToReadableText(reward),
                RequiredNodeIds = requiredNodeIds,
                LinkedClassId = PlayerDevelopmentGraphDisplay.ToReadableText(FirstNonEmpty(GetString(map, "linkedClassId"), GetString(map, "classId"))),
                CurrencyId = currencyId,
                PositionX = positionX,
                PositionY = positionY,
                Ring = ParseInt(GetString(map, "ring"), 0),
                Sector = ParseInt(GetString(map, "sector"), 0),
                SortOrder = ParseInt(GetString(map, "sortOrder"), 0),
                LayoutVersion = ParseInt(GetString(map, "layoutVersion"), 0),
                CanPurchase = GetBool(map, "canPurchase") || available.Contains(nodeId),
                RequiresRequest = GetBool(map, "requiresPlayerRequest") || GetBool(map, "requiresGMApproval"),
                X = positionX,
                Y = positionY
            };
            PlayerDevelopmentLayoutVisualRules.ApplyNodeSize(visualNode);
            if (PlayerDevelopmentLayoutVisualRules.IsDiagnosticToken(visualNode.NodeId, visualNode.Title, visualNode.NodeTypeLabel, visualNode.BranchKey, visualNode.DirectionKey))
                continue;
            serverNodes.Add(visualNode);
        }

        var hexagonPayloadKeys = string.Join(",", tree.Payload.Keys.OrderBy(key => key, StringComparer.Ordinal));
        ClientLogService.Instance.Info($"player.development.hexagon.response status={tree.Status}; payloadKeys={hexagonPayloadKeys}; rawCollectionKey={hexagonRawCollectionKey}; rawItemsType={hexagonRawItemsType}; rawCount={hexagonItems.Count}; mappedCount={serverNodes.Count}");

        if (serverNodes.Count > 0)
        {
            ClassNodes.Clear();
            foreach (var node in serverNodes.OrderBy(n => n.NodeId == "novice" ? 0 : 1).ThenBy(n => n.Y).ThenBy(n => n.X))
                ClassNodes.Add(node);
        }

        foreach (var node in ClassNodes)
        {
            if (node.NodeId == "novice") node.State = "Старт";
            else if (acquired.Contains(node.NodeId)) node.State = "Изучено";
            else if (available.Contains(node.NodeId)) node.State = "Доступно";
            else if (string.IsNullOrWhiteSpace(node.State)) node.State = "Недоступно";
            else node.State = PlayerDevelopmentGraphDisplay.ToReadableState(node.State);
        }
        Notify(nameof(ClassNodes));
        Notify(nameof(VisibleDevelopmentCanvasNodes));
        RebuildDevelopmentCanvasLinks();
        ApplyDevelopmentViewerSearch();
        Notify(nameof(DevelopmentHexagons));
        Notify(nameof(SelectedDevelopmentHexagonDisplay));
        Notify(nameof(DevelopmentTreeModeOverlayText));
        RebuildClassNavigation();
        if (!string.IsNullOrWhiteSpace(SelectedClassNodeId))
        {
            TrySelectClassNodeById(SelectedClassNodeId, updateStatus: false);
        }

        SkillRows.Clear();
        SkillCatalogRows.Clear();

        var catalog = _api.ProgressionAvailableSkills(SelectedCharacterId);
        var catalogPayloadKeys = string.Join(",", catalog.Payload.Keys.OrderBy(key => key, StringComparer.Ordinal));
        var catalogMappedCount = 0;
        var catalogRawCount = 0;
        if (catalog.Status == ResponseStatus.Ok)
        {
            var catalogItems = ExtractCharacterSkillsItems(catalog.Payload, out _);
            catalogRawCount = catalogItems.Count;
            foreach (var item in catalogItems)
            {
                var map = AsMap(item, CommandNames.ProgressionAvailableSkills);
                if (map == null) continue;
                var code = GetString(map, "code");
                var name = FirstNonEmpty(GetString(map, "name"), code);
                SkillCatalogRows.Add($"{code} | {name} | available={available}");
                catalogMappedCount++;
            }
        }
        ClientLogService.Instance.Info($"player.skillCatalog.response.keys={catalogPayloadKeys}");
        ClientLogService.Instance.Info($"player.skillCatalog.rawCount={catalogRawCount}");
        ClientLogService.Instance.Info($"player.skillCatalog.mappedCount={catalogMappedCount}");

        var skills = _api.CharacterSkillsGet(SelectedCharacterId);
        ClientLogService.Instance.Info($"player.characterSkills.response status={skills.Status}");
        var mappedCount = 0;
        var characterRawCount = 0;
        if (skills.Status == ResponseStatus.Ok)
        {
            var payloadKeys = string.Join(",", skills.Payload.Keys.OrderBy(key => key, StringComparer.Ordinal));
            var items = ExtractCharacterSkillsItems(skills.Payload, out var rawCollectionKey);
            characterRawCount = items.Count;
            string firstSkillCode = string.Empty;
            foreach (var item in items)
            {
                var map = AsMap(item, CommandNames.CharacterSkillsGet);
                if (map == null) continue;
                var skillCode = FirstNonEmpty(GetString(map, "skillCode"), GetString(map, "skillId"), GetString(map, "code"));
                if (string.IsNullOrWhiteSpace(skillCode)) continue;
                SkillRows.Add(new SkillDisplayRowVm
                {
                    SkillCode = skillCode,
                    DisplayName = FirstNonEmpty(GetString(map, "displayName"), GetString(map, "name"), skillCode),
                    Category = FirstNonEmpty(GetString(map, "category"), "other"),
                    Attribute = GetString(map, "defaultAttribute"),
                    Rank = ParseInt(GetString(map, "rank"), ParseInt(GetString(map, "level"), 0)),
                    ManualBonus = ParseInt(GetString(map, "manualBonus"), 0),
                    AttributeBonus = ParseInt(GetString(map, "attributeBonus"), 0),
                    SubAttributeId = GetString(map, "subAttributeId"),
                    SubAttributeDisplayName = GetString(map, "subAttributeDisplayName"),
                    SubAttributeBonus = ParseInt(GetString(map, "subAttributeBonus"), 0),
                    TotalBonus = ParseInt(GetString(map, "totalBonus"), 0),
                    Breakdown = FirstNonEmpty(GetString(map, "breakdownText"), GetString(map, "breakdown")),
                    TrainingState = FirstNonEmpty(GetString(map, "trainingState"), "trained")
                });
                mappedCount++;
                if (string.IsNullOrWhiteSpace(firstSkillCode)) firstSkillCode = skillCode;
            }
            ClientLogService.Instance.Info($"character.skills.response.keys={payloadKeys}");
            ClientLogService.Instance.Info($"character.skills.rawCollectionKey={rawCollectionKey}");
            ClientLogService.Instance.Info($"character.skills.rawCount={items.Count}");
            ClientLogService.Instance.Info($"character.skills.mappedCount={mappedCount}");
            ClientLogService.Instance.Info($"character.skills.firstSkillCode={FirstNonEmpty(firstSkillCode, "<none>")}");
        }
        ClientLogService.Instance.Info($"player.characterSkills.rawCount={characterRawCount}");
        ClientLogService.Instance.Info($"player.characterSkills.mappedCount={mappedCount}");

        ClientLogService.Instance.Info($"player.skillCatalog.count={catalogMappedCount}");
        ClientLogService.Instance.Info($"player.characterSkills.count={mappedCount}");
        ClientLogService.Instance.Info($"activeCharacter.skills loaded={mappedCount}");

        var placeholderHidden = mappedCount > 0;
        if (catalogMappedCount == 0) EnsureCollectionPlaceholder(SkillCatalogRows, "Каталог навыков пока не загружен.");
        if (!placeholderHidden) ClientLogService.Instance.Info("player.skills.empty no_visible_skills");
        ClientLogService.Instance.Info($"player.skills.placeholder hidden={placeholderHidden.ToString().ToLowerInvariant()}");

        ClientLogService.Instance.Info($"player.skills.character.bind count={mappedCount}");
        if (_lastSkillsRenderCount != mappedCount)
        {
            ClientLogService.Instance.Info($"activeCharacter.skills.render count={mappedCount}");
            _lastSkillsRenderCount = mappedCount;
        }
        if (_lastSkillsPlaceholderHidden != placeholderHidden)
        {
            ClientLogService.Instance.Info($"activeCharacter.skills.placeholder hidden={placeholderHidden.ToString().ToLowerInvariant()}");
            _lastSkillsPlaceholderHidden = placeholderHidden;
        }
    }

    private void RebuildDevelopmentCanvasLinks()
    {
        DevelopmentCanvasLinks.Clear();
        var visibleNodes = VisibleDevelopmentCanvasNodes
            .Where(node => !node.IsFilteredOut)
            .ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
        foreach (var target in visibleNodes.Values)
        {
            var requiredIds = (target.RequiredNodeIds ?? string.Empty)
                .Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var sourceId in requiredIds)
            {
                if (!visibleNodes.TryGetValue(sourceId, out var source)) continue;
                DevelopmentCanvasLinks.Add(new ClassNodeVisualLinkVm
                {
                    LinkId = sourceId + "->" + target.NodeId,
                    SourceNodeId = sourceId,
                    TargetNodeId = target.NodeId,
                    SourceTitle = source.DisplayTitle,
                    TargetTitle = target.DisplayTitle,
                    X1 = source.X + source.NodeWidth / 2,
                    Y1 = source.Y + source.NodeHeight / 2,
                    X2 = target.X + target.NodeWidth / 2,
                    Y2 = target.Y + target.NodeHeight / 2
                });
            }
        }

        Notify(nameof(DevelopmentCanvasLinks));
        Notify(nameof(DevelopmentViewerLinkDirectionText));
        Notify(nameof(DevelopmentTreeModeOverlayText));
        RebuildDevelopmentViewerCanonicalOverlay();
        ClientLogService.Instance.Info($"player.development.hexagon.links.rendered count={DevelopmentCanvasLinks.Count}");
    }

    private void StoreDevelopmentViewerHexagonPayloads(Dictionary<string, object> payload, string fallbackHexagonId)
    {
        if (payload.TryGetValue("hexagon", out var rawHexagon) && TryAsMap(rawHexagon, out var hexagon))
        {
            var hexagonId = FirstNonEmpty(GetString(hexagon, "hexagonId"), fallbackHexagonId, DevelopmentHexagonIds.Main);
            _developmentViewerHexagonPayloads[hexagonId] = hexagon;
        }

        foreach (var item in NormalizePayloadList(payload.TryGetValue("hexagons", out var rawHexagons) ? rawHexagons : new object[0], out _))
        {
            if (!TryAsMap(item, out var map)) continue;
            var hexagonId = FirstNonEmpty(GetString(map, "hexagonId"), fallbackHexagonId, DevelopmentHexagonIds.Main);
            _developmentViewerHexagonPayloads[hexagonId] = map;
        }
    }

    private void ApplyDevelopmentViewerSearch()
    {
        var query = (DevelopmentViewerSearchText ?? string.Empty).Trim();
        foreach (var node in ClassNodes)
        {
            var inHexagon = string.Equals(string.IsNullOrWhiteSpace(node.HexagonId) ? DevelopmentHexagonIds.Main : node.HexagonId, SelectedDevelopmentHexagonId, StringComparison.OrdinalIgnoreCase);
            var searchOk = string.IsNullOrWhiteSpace(query) || node.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            node.IsSearchMatch = inHexagon && !string.IsNullOrWhiteSpace(query) && searchOk;
            var canonicalRootSuppressed = inHexagon && IsPlayerDevelopmentCanonicalRootNode(node.NodeId, SelectedDevelopmentHexagonId);
            node.IsFilteredOut = inHexagon && (canonicalRootSuppressed || !searchOk);
        }

        Notify(nameof(DevelopmentViewerSearchResultCountText));
        Notify(nameof(VisibleDevelopmentCanvasNodes));
        RebuildDevelopmentCanvasLinks();
        Notify(nameof(DevelopmentTreeModeOverlayText));
        RebuildDevelopmentViewerCanonicalOverlay();
    }

    private void RebuildDevelopmentViewerCanonicalOverlay()
    {
        DevelopmentViewerCanonicalRoots.Clear();
        DevelopmentViewerCanonicalDirections.Clear();
        DevelopmentViewerCanonicalLanes.Clear();

        const double rootWidth = 320;
        const double rootHeight = 184;

        var rootNode = FindPlayerDevelopmentCanonicalRootNode(SelectedDevelopmentHexagonId);
        var centerX = rootNode == null
            ? PlayerDevelopmentLayoutVisualRules.WorkspaceWidth / 2.0
            : rootNode.X + rootNode.NodeWidth / 2.0;
        var centerY = rootNode == null
            ? PlayerDevelopmentLayoutVisualRules.WorkspaceHeight / 2.0
            : rootNode.Y + rootNode.NodeHeight / 2.0;
        var rootLabel = ResolvePlayerDevelopmentCanonicalRootLabel(SelectedDevelopmentHexagonId, rootNode);
        DevelopmentViewerCanonicalRoots.Add(new PlayerDevelopmentCanonicalRootVm
        {
            Label = rootLabel,
            X = centerX - rootWidth / 2.0,
            Y = centerY - rootHeight / 2.0,
            Width = rootWidth,
            Height = rootHeight
        });

        var visible = VisibleDevelopmentCanvasNodes
            .Where(node => !node.IsFilteredOut)
            .Where(node => !IsPlayerDevelopmentCanonicalRootNode(node.NodeId, SelectedDevelopmentHexagonId))
            .Select(node => new
            {
                Node = node,
                X = node.X + node.NodeWidth / 2.0,
                Y = node.Y + node.NodeHeight / 2.0
            })
            .ToList();

        foreach (var direction in BuildPlayerCanonicalDirections(SelectedDevelopmentHexagonId))
        {
            var radians = direction.AngleDegrees * Math.PI / 180.0;
            var normalX = Math.Cos(radians);
            var normalY = Math.Sin(radians);
            var matching = visible
                .Where(item => PlayerNodeMatchesCanonicalDirection(item.Node, direction.DirectionId))
                .ToList();
            var farthest = matching.Count == 0
                ? 680
                : Math.Max(680, matching.Max(item => (item.X - centerX) * normalX + (item.Y - centerY) * normalY) + 160);
            farthest = Math.Min(farthest, 940);

            var isFocused = string.IsNullOrWhiteSpace(DevelopmentViewerFocusedDirectionKey) ||
                            string.Equals(DevelopmentViewerFocusedDirectionKey, direction.DirectionId, StringComparison.OrdinalIgnoreCase);
            var anchorWidth = 190.0;
            var anchorHeight = 86.0;
            DevelopmentViewerCanonicalLanes.Add(new PlayerDevelopmentCanonicalLaneVm
            {
                DirectionId = direction.DirectionId,
                SideIndex = direction.SideIndex,
                X1 = centerX + normalX * 178,
                Y1 = centerY + normalY * 178,
                X2 = centerX + normalX * farthest,
                Y2 = centerY + normalY * farthest,
                Opacity = isFocused ? 0.76 : 0.18,
                StrokeThickness = isFocused ? 5.2 : 2.4
            });
            DevelopmentViewerCanonicalDirections.Add(new PlayerDevelopmentCanonicalDirectionVm
            {
                DirectionId = direction.DirectionId,
                SideIndex = direction.SideIndex,
                DisplayName = direction.DisplayName,
                AtmosphericName = direction.AtmosphericName,
                AnchorX = centerX + normalX * 360 - anchorWidth / 2.0,
                AnchorY = centerY + normalY * 360 - anchorHeight / 2.0,
                AnchorWidth = anchorWidth,
                AnchorHeight = anchorHeight,
                IsFocused = isFocused
            });
        }
    }

    private ClassNodeVisualVm? FindPlayerDevelopmentCanonicalRootNode(string hexagonId)
    {
        var centerNodeId = GetPlayerDevelopmentCanonicalCenterNodeId(hexagonId);
        if (!string.IsNullOrWhiteSpace(centerNodeId))
        {
            var explicitRoot = ClassNodes.FirstOrDefault(node => string.Equals(node.NodeId, centerNodeId, StringComparison.OrdinalIgnoreCase));
            if (explicitRoot != null) return explicitRoot;
        }

        return ClassNodes.FirstOrDefault(node => string.Equals(node.NodeId, ExpectedPlayerDevelopmentCanonicalRootNodeId(hexagonId), StringComparison.OrdinalIgnoreCase));
    }

    private static string ExpectedPlayerDevelopmentCanonicalRootNodeId(string hexagonId)
        => string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase)
            ? "magic_awakened"
            : string.Equals(hexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase)
                ? "large0154_root"
                : "novice";

    private bool IsPlayerDevelopmentCanonicalRootNode(string nodeId, string hexagonId)
    {
        var centerNodeId = GetPlayerDevelopmentCanonicalCenterNodeId(hexagonId);
        return (!string.IsNullOrWhiteSpace(centerNodeId) && string.Equals(nodeId, centerNodeId, StringComparison.OrdinalIgnoreCase))
               || string.Equals(nodeId, "novice", StringComparison.OrdinalIgnoreCase)
               || string.Equals(nodeId, "magic_awakened", StringComparison.OrdinalIgnoreCase)
               || string.Equals(nodeId, "large0154_root", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolvePlayerDevelopmentCanonicalRootLabel(string hexagonId, ClassNodeVisualVm? rootNode)
    {
        var centerName = string.Empty;
        if (_developmentViewerHexagonPayloads.TryGetValue(hexagonId, out var payload))
            centerName = GetString(payload, "centerNodeName");

        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
            return FirstNonEmpty(rootNode?.DisplayTitle ?? string.Empty, centerName, "Источник магии");
        if (string.Equals(hexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase))
            return FirstNonEmpty(rootNode?.DisplayTitle ?? string.Empty, centerName, "Большое дерево");
        return FirstNonEmpty(rootNode?.DisplayTitle ?? string.Empty, centerName, "Новичок");
    }

    private string GetPlayerDevelopmentCanonicalCenterNodeId(string hexagonId)
    {
        if (_developmentViewerHexagonPayloads.TryGetValue(hexagonId, out var payload))
            return FirstNonEmpty(GetString(payload, "centerNodeId"), GetString(payload, "rootNodeId"));
        return ExpectedPlayerDevelopmentCanonicalRootNodeId(hexagonId);
    }

    private static bool PlayerNodeMatchesCanonicalDirection(ClassNodeVisualVm node, string directionId)
        => string.Equals(node.DirectionKey, directionId, StringComparison.OrdinalIgnoreCase)
           || string.Equals(node.BranchKey, directionId, StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<PlayerCanonicalDirectionDefinition> BuildPlayerCanonicalDirections(string hexagonId)
    {
        if (_developmentViewerHexagonPayloads.TryGetValue(hexagonId, out var payload))
        {
            var serverDirections = BuildPlayerCanonicalDirectionsFromPayload(payload);
            if (serverDirections.Count > 0)
            {
                if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase) &&
                    serverDirections.Count != 6)
                {
                    return BuildDefaultPlayerMagicCanonicalDirections();
                }

                return serverDirections.Count <= 6
                    ? serverDirections
                    : serverDirections.Take(6).ToList();
            }
        }

        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
            return BuildDefaultPlayerMagicCanonicalDirections();

        return new[]
        {
            new PlayerCanonicalDirectionDefinition(DevelopmentDirectionIds.StrengthAssault, "Сила", "Натиск", 0, -90),
            new PlayerCanonicalDirectionDefinition(DevelopmentDirectionIds.DexterityManeuver, "Ловкость", "Манёвр", 1, -30),
            new PlayerCanonicalDirectionDefinition(DevelopmentDirectionIds.EnduranceResilience, "Выносливость", "Стойкость", 2, 30),
            new PlayerCanonicalDirectionDefinition(DevelopmentDirectionIds.IntellectReason, "Интеллект", "Разум", 3, 90),
            new PlayerCanonicalDirectionDefinition(DevelopmentDirectionIds.WisdomPath, "Мудрость", "Путь", 4, 150),
            new PlayerCanonicalDirectionDefinition(DevelopmentDirectionIds.CharismaInfluence, "Харизма", "Влияние", 5, -150)
        };
    }

    private static IReadOnlyList<PlayerCanonicalDirectionDefinition> BuildDefaultPlayerMagicCanonicalDirections()
        => new[]
        {
            new PlayerCanonicalDirectionDefinition("magic_mana", "Мана", "Поток", 0, -90),
            new PlayerCanonicalDirectionDefinition("magic_spell", "Заклинания", "Форма", 1, -30),
            new PlayerCanonicalDirectionDefinition("magic_seal", "Печати", "Знак", 2, 30),
            new PlayerCanonicalDirectionDefinition("magic_arcana", "Аркана", "Глубина", 3, 90),
            new PlayerCanonicalDirectionDefinition("magic_element_fire", "Стихия огня", "Пламя", 4, 150),
            new PlayerCanonicalDirectionDefinition("magic_direction_light", "Направление света", "Свет", 5, -150)
        };

    private static IReadOnlyList<PlayerCanonicalDirectionDefinition> BuildPlayerCanonicalDirectionsFromPayload(Dictionary<string, object> payload)
    {
        var result = new List<PlayerCanonicalDirectionDefinition>();
        foreach (var raw in NormalizePayloadList(payload.TryGetValue("directions", out var rawDirections) ? rawDirections : new object[0], out _))
        {
            if (!TryAsMap(raw, out var map)) continue;
            var directionId = FirstNonEmpty(GetString(map, "directionId"), GetString(map, "id"));
            if (string.IsNullOrWhiteSpace(directionId)) continue;
            var displayOrder = ParseInt(FirstNonEmpty(GetString(map, "displayOrder"), GetString(map, "sortOrder")), result.Count + 1);
            var sideIndex = string.IsNullOrWhiteSpace(GetString(map, "sideIndex"))
                ? Math.Max(0, displayOrder - 1)
                : Math.Max(0, ParseInt(GetString(map, "sideIndex"), result.Count));
            result.Add(new PlayerCanonicalDirectionDefinition(
                directionId,
                FirstNonEmpty(GetString(map, "name"), GetString(map, "displayName"), directionId),
                FirstNonEmpty(GetString(map, "atmosphericName"), GetString(map, "subtitle"), GetString(map, "secondaryName")),
                sideIndex,
                ParseDouble(FirstNonEmpty(GetString(map, "angleDegrees"), GetString(map, "angle")), PlayerCanonicalAngleForSide(sideIndex))));
        }

        return result
            .OrderBy(direction => direction.SideIndex)
            .ThenBy(direction => direction.DirectionId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static double PlayerCanonicalAngleForSide(int sideIndex)
        => sideIndex switch
        {
            0 => -90,
            1 => -30,
            2 => 30,
            3 => 90,
            4 => 150,
            _ => -150
        };

    private sealed class PlayerCanonicalDirectionDefinition
    {
        public PlayerCanonicalDirectionDefinition(string directionId, string displayName, string atmosphericName, int sideIndex, double angleDegrees)
        {
            DirectionId = directionId;
            DisplayName = displayName;
            AtmosphericName = atmosphericName;
            SideIndex = sideIndex;
            AngleDegrees = angleDegrees;
        }

        public string DirectionId { get; }
        public string DisplayName { get; }
        public string AtmosphericName { get; }
        public int SideIndex { get; }
        public double AngleDegrees { get; }
    }

    private void ClearDevelopmentViewerSearch()
    {
        DevelopmentViewerSearchText = string.Empty;
    }

    private void SelectDevelopmentViewerSearchResult(int direction)
    {
        var matches = VisibleDevelopmentCanvasNodes
            .Where(node => !node.IsFilteredOut && (string.IsNullOrWhiteSpace(DevelopmentViewerSearchText) || node.IsSearchMatch))
            .OrderBy(node => node.PositionY)
            .ThenBy(node => node.PositionX)
            .ToList();
        if (matches.Count == 0)
        {
            DevelopmentStatusText = "Нет узлов в области поиска.";
            return;
        }

        _developmentViewerSearchIndex += direction;
        if (_developmentViewerSearchIndex < 0) _developmentViewerSearchIndex = matches.Count - 1;
        if (_developmentViewerSearchIndex >= matches.Count) _developmentViewerSearchIndex = 0;
        TrySelectClassNodeById(matches[_developmentViewerSearchIndex].NodeId, updateStatus: true);
        DevelopmentStatusText = $"Найден узел {_developmentViewerSearchIndex + 1}/{matches.Count}: {matches[_developmentViewerSearchIndex].DisplayTitle}";
    }

    private void FitToViewDevelopmentHexagon()
    {
        if (DevelopmentViewerCanonicalRoots.Count > 0 && DevelopmentViewerCanonicalDirections.Count > 0)
        {
            var minXValues = new List<double>();
            var minYValues = new List<double>();
            var maxXValues = new List<double>();
            var maxYValues = new List<double>();
            void IncludeBounds(double x, double y, double width, double height)
            {
                minXValues.Add(x);
                minYValues.Add(y);
                maxXValues.Add(x + Math.Max(0, width));
                maxYValues.Add(y + Math.Max(0, height));
            }

            foreach (var root in DevelopmentViewerCanonicalRoots)
                IncludeBounds(root.X, root.Y, root.Width, root.Height);
            foreach (var direction in DevelopmentViewerCanonicalDirections)
                IncludeBounds(direction.AnchorX, direction.AnchorY, direction.AnchorWidth, direction.AnchorHeight);
            foreach (var lane in DevelopmentViewerCanonicalLanes)
                IncludeBounds(Math.Min(lane.X1, lane.X2) - 24, Math.Min(lane.Y1, lane.Y2) - 24, Math.Abs(lane.X2 - lane.X1) + 48, Math.Abs(lane.Y2 - lane.Y1) + 48);
            var rootCenterX = DevelopmentViewerCanonicalRoots.Average(root => root.X + root.Width / 2.0);
            var rootCenterY = DevelopmentViewerCanonicalRoots.Average(root => root.Y + root.Height / 2.0);
            var canonicalDirectionIds = new HashSet<string>(DevelopmentViewerCanonicalDirections.Select(direction => direction.DirectionId), StringComparer.OrdinalIgnoreCase);
            foreach (var node in VisibleDevelopmentCanvasNodes.Where(node =>
                         !node.IsFilteredOut
                         && !PlayerDevelopmentLayoutVisualRules.IsDiagnosticToken(node.NodeId, node.Title, node.NodeTypeLabel, node.BranchKey, node.DirectionKey)
                         && !IsPlayerDevelopmentCanonicalRootNode(node.NodeId, SelectedDevelopmentHexagonId)
                         && canonicalDirectionIds.Any(directionId => PlayerNodeMatchesCanonicalDirection(node, directionId))))
            {
                var nodeCenterX = node.X + node.NodeWidth / 2.0;
                var nodeCenterY = node.Y + node.NodeHeight / 2.0;
                var distance = Math.Sqrt(Math.Pow(nodeCenterX - rootCenterX, 2) + Math.Pow(nodeCenterY - rootCenterY, 2));
                if (distance <= 1120)
                    IncludeBounds(node.X, node.Y, node.NodeWidth, node.NodeHeight);
            }

            var minXCanonical = minXValues.Min();
            var minYCanonical = minYValues.Min();
            var maxXCanonical = maxXValues.Max();
            var maxYCanonical = maxYValues.Max();
            var canonicalWidth = Math.Max(1, maxXCanonical - minXCanonical);
            var canonicalHeight = Math.Max(1, maxYCanonical - minYCanonical);
            const double canonicalViewportWidth = 1900;
            const double canonicalViewportHeight = 560;
            const double canonicalMargin = 96;
            var canonicalAvailableWidth = Math.Max(1, canonicalViewportWidth - canonicalMargin * 2);
            var canonicalAvailableHeight = Math.Max(1, canonicalViewportHeight - canonicalMargin * 2);
            var canonicalScale = Math.Min(1.34, Math.Max(0.08, Math.Min(canonicalAvailableWidth / canonicalWidth, canonicalAvailableHeight / canonicalHeight)));
            var canonicalCenterX = minXCanonical + canonicalWidth / 2;
            var canonicalCenterY = minYCanonical + canonicalHeight / 2;
            DevelopmentViewerZoom = canonicalScale;
            DevelopmentViewerViewportTranslateX = canonicalViewportWidth / 2 - canonicalCenterX * canonicalScale;
            DevelopmentViewerViewportTranslateY = canonicalViewportHeight / 2 - canonicalCenterY * canonicalScale;
            DevelopmentStatusText = $"Канонический шестиугольник вписан: {DevelopmentViewerCanonicalDirections.Count} направлений; масштаб {DevelopmentViewerZoomText}.";
            return;
        }

        var visible = VisibleDevelopmentCanvasNodes.Where(node => !node.IsFilteredOut).ToList();
        if (visible.Count == 0) visible = VisibleDevelopmentCanvasNodes.ToList();
        if (visible.Count == 0)
        {
            DevelopmentViewerZoom = 0.62;
            DevelopmentViewerViewportTranslateX = 0;
            DevelopmentViewerViewportTranslateY = 0;
            DevelopmentStatusText = "Нет узлов для fit-to-view.";
            return;
        }

        var minX = visible.Min(node => node.X);
        var minY = visible.Min(node => node.Y);
        var maxX = visible.Max(node => node.X + node.NodeWidth);
        var maxY = visible.Max(node => node.Y + node.NodeHeight);
        var width = Math.Max(1, maxX - minX);
        var height = Math.Max(1, maxY - minY);
        const double viewportWidth = 1900;
        const double viewportHeight = 560;
        const double margin = 72;
        var availableWidth = Math.Max(1, viewportWidth - margin * 2);
        var availableHeight = Math.Max(1, viewportHeight - margin * 2);
        var scale = Math.Min(1.34, Math.Max(0.08, Math.Min(availableWidth / width, availableHeight / height)));
        var contentCenterX = minX + width / 2;
        var contentCenterY = minY + height / 2;
        DevelopmentViewerZoom = scale;
        DevelopmentViewerViewportTranslateX = viewportWidth / 2 - contentCenterX * scale;
        DevelopmentViewerViewportTranslateY = viewportHeight / 2 - contentCenterY * scale;
        DevelopmentStatusText = $"Канва развития вписана: {visible.Count} узл.; масштаб {DevelopmentViewerZoomText}.";
    }

    private void AcquireClassNode(object? parameter)
    {
        if (parameter is string key && !string.IsNullOrWhiteSpace(key))
        {
            if (TrySelectClassNodeById(key, updateStatus: true))
            {
                NotifyClassDetail();
                return;
            }

            if (ClassDirections.Any(d => d.Key == key))
            {
                SelectedClassDirectionKey = key;
                return;
            }

            if (ClassBranches.Any(b => b.Key == key))
            {
                SelectedClassBranch = ClassBranches.First(b => b.Key == key);
                return;
            }

            SelectedClassNodeId = key;
            DevelopmentStatusText = $"Узел развития не найден: {key}";
        }
        else if (!string.IsNullOrWhiteSpace(SelectedClassNodeId))
        {
            TrySelectClassNodeById(SelectedClassNodeId, updateStatus: true);
        }
        NotifyClassDetail();
    }

    private bool TrySelectClassNodeById(string nodeId, bool updateStatus)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return false;

        var visual = ClassNodes.FirstOrDefault(n => string.Equals(n.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        if (visual == null) return false;

        SelectedClassNodeId = visual.NodeId;

        if (!string.IsNullOrWhiteSpace(visual.DirectionKey) && !string.Equals(SelectedClassDirectionKey, visual.DirectionKey, StringComparison.OrdinalIgnoreCase))
        {
            _selectedClassDirectionKey = visual.DirectionKey;
            Notify(nameof(SelectedClassDirectionKey));
            RebuildClassNavigation();
        }

        var branchKey = FirstNonEmpty(visual.BranchKey, visual.NodeId);
        var branch = ClassBranches.FirstOrDefault(b => string.Equals(b.Key, branchKey, StringComparison.OrdinalIgnoreCase))
            ?? ClassBranches.FirstOrDefault(b => string.Equals(b.Key, visual.NodeId, StringComparison.OrdinalIgnoreCase));
        if (branch != null && !ReferenceEquals(SelectedClassBranch, branch))
        {
            SelectedClassBranch = branch;
        }

        var entry = ClassEntries.FirstOrDefault(e => string.Equals(e.NodeId, visual.NodeId, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            RebuildClassEntries();
            entry = ClassEntries.FirstOrDefault(e => string.Equals(e.NodeId, visual.NodeId, StringComparison.OrdinalIgnoreCase));
        }

        if (entry == null) return false;

        SelectedClassEntry = entry;
        if (updateStatus)
        {
            DevelopmentStatusText = $"Выбран узел развития: {entry.Title}";
        }

        return true;
    }

    private void BuySelectedClassNode()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedClassNodeId))
        {
            DevelopmentStatusText = "Выберите узел развития.";
            return;
        }

        if (!CanBuySelectedClassNode)
        {
            DevelopmentStatusText = "Этот узел сейчас недоступен для покупки.";
            return;
        }

        var response = _api.DevelopmentPlayerPurchase(SelectedCharacterId, SelectedClassNodeId, FirstNonEmpty(SelectedClassEntry?.HexagonId ?? string.Empty, SelectedDevelopmentHexagonId));
        DevelopmentStatusText = response.Status == ResponseStatus.Ok
            ? "Узел развития куплен."
            : FirstNonEmpty(response.Message, "Не удалось купить узел развития.");
        LoadClassAndSkills();
    }

    private void RequestUnlockNode()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedClassNodeId))
        {
            DevelopmentStatusText = "Выберите узел развития.";
            return;
        }

        var response = _api.DevelopmentPlayerRequestPurchase(SelectedCharacterId, SelectedClassNodeId, string.Empty, FirstNonEmpty(SelectedClassEntry?.HexagonId ?? string.Empty, SelectedDevelopmentHexagonId));
        DevelopmentStatusText = response.Status == ResponseStatus.Ok
            ? "Заявка на развитие отправлена GM."
            : FirstNonEmpty(response.Message, "Заявка на развитие недоступна.");
        LoadClassAndSkills();
    }

    private void AcquireSkill()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedSkillId)) return;
        _api.SkillsAcquire(SelectedCharacterId, SelectedSkillId);
        LoadClassAndSkills();
    }

    private void RollSelectedSkillCheck()
    {
        var skillCode = FirstNonEmpty(SelectedSkillRow?.SkillCode ?? string.Empty, SelectedSkillId);
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(skillCode)) return;
        var subAttributeId = FirstNonEmpty(SelectedSkillRow?.SubAttributeId ?? string.Empty);
        var response = _api.CharacterSkillCheckRoll(SelectedCharacterId, skillCode, subAttributeId);
        if (response.Status != ResponseStatus.Ok)
        {
            ClientLogService.Instance.Warn($"player.skillCheck.error skill={skillCode} status={response.Status} message={response.Message}");
            return;
        }
        var total = response.Payload.ContainsKey("totalBonus") ? Convert.ToString(response.Payload["totalBonus"]) : string.Empty;
        var breakdown = response.Payload.ContainsKey("breakdown") ? Convert.ToString(response.Payload["breakdown"]) : string.Empty;
        DiceFeedRows.Insert(0, $"Проверка навыка {skillCode}: бонус {total}. {breakdown}");
        ClientLogService.Instance.Info($"player.skillCheck.done skill={skillCode} subAttribute={subAttributeId} totalBonus={total}");
        RefreshDiceAndRequests();
    }

    private void InitializeClassVisualLayout()
    {
        ClassDirections.Clear();
        ClassDirections.Add(new ClassDirectionVm { Key = "strength_assault", Label = "Сила - Натиск", Summary = "Силовое направление для ударов, удержания и прорыва." });
        ClassDirections.Add(new ClassDirectionVm { Key = "dexterity_maneuver", Label = "Ловкость - Манёвр", Summary = "Уклонение, мобильность и точные действия." });
        ClassDirections.Add(new ClassDirectionVm { Key = "endurance_resilience", Label = "Выносливость - Стойкость", Summary = "Защита, живучесть и сопротивление давлению." });
        ClassDirections.Add(new ClassDirectionVm { Key = "intellect_reason", Label = "Интеллект - Разум", Summary = "Анализ, ремесло, техника и сложные решения." });
        ClassDirections.Add(new ClassDirectionVm { Key = "wisdom_path", Label = "Мудрость - Путь", Summary = "Внимание, интуиция и устойчивые практики." });
        ClassDirections.Add(new ClassDirectionVm { Key = "charisma_influence", Label = "Харизма - Влияние", Summary = "Переговоры, лидерство и социальное давление." });

        ClassNodes.Clear();
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "novice", Title = "", State = "Start", X = 190, Y = 120 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "strength_assault", DirectionKey = "strength_assault", BranchKey = "strength_assault", Title = "Натиск", State = "Locked", X = 190, Y = 12 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "dexterity_maneuver", DirectionKey = "dexterity_maneuver", BranchKey = "dexterity_maneuver", Title = "", State = "Locked", X = 322, Y = 68 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "endurance_resilience", DirectionKey = "endurance_resilience", BranchKey = "endurance_resilience", Title = "Стойкость", State = "Locked", X = 322, Y = 186 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "intellect_reason", DirectionKey = "intellect_reason", BranchKey = "intellect_reason", Title = "Разум", State = "Locked", X = 190, Y = 242 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "wisdom_path", DirectionKey = "wisdom_path", BranchKey = "wisdom_path", Title = "Путь", State = "Locked", X = 58, Y = 186 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "charisma_influence", DirectionKey = "charisma_influence", BranchKey = "charisma_influence", Title = "", State = "Locked", X = 58, Y = 68 });
        RebuildClassNavigation();
    }

    private const string MissingDataText = "нет данных";

    private void AddStat(string label, Dictionary<string, object> map, string key)
        => StatsRows.Add(new StatRowVm { AttributeId = key, Code = key, Label = label, Value = FirstNonEmpty(GetString(map, key), MissingDataText), AutomationScope = "Vital" });

    private void RebuildStatGroups()
    {
        CoreStatRows.Clear();
        AttributeStatRows.Clear();

        foreach (var row in StatsRows)
        {
            if (row.Code == "health"
                || row.Code == "physicalArmor"
                || row.Code == "magicalArmor"
                || row.Code == "morale"
                || row.Code == "health_current"
                || row.Code == "health_max"
                || row.Code == "physical_defense"
                || row.Code == "magical_defense")
                CoreStatRows.Add(row);
            else
                AttributeStatRows.Add(row);
        }
    }

    private void BindCharacterStatRows(object rawStats, ObservableCollection<StatRowVm> target, string context, string logName)
    {
        target.Clear();
        var automationScope = ReferenceEquals(target, CoreStatRows) ? "Vital" : "Derived";
        foreach (var item in ToObjectList(rawStats).Cast<object>())
        {
            var map = AsMap(item, context);
            if (map == null) continue;
            var statId = FirstNonEmpty(GetString(map, "definitionId"), GetString(map, "attributeId"), GetString(map, "id"), GetString(map, "code"));
            if (string.IsNullOrWhiteSpace(statId)) continue;
            var code = FirstNonEmpty(GetString(map, "code"), statId);
            var value = FirstNonEmpty(GetString(map, "value"), GetString(map, "currentValue"), GetString(map, "baseValue"), MissingDataText);
            var min = GetInt(map, "minValue");
            var max = GetInt(map, "maxValue");
            var row = new StatRowVm
            {
                AttributeId = statId,
                Code = code,
                Label = FirstNonEmpty(GetString(map, "displayName"), GetString(map, "label"), statId),
                Value = value,
                Description = GetString(map, "description"),
                SortOrder = GetInt(map, "sortOrder"),
                RangeText = max > min ? $"{min}..{max}" : string.Empty,
                AutomationScope = automationScope
            };
            target.Add(row);
            StatsRows.Add(row);
        }

        var ordered = target.OrderBy(x => x.SortOrder).ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToList();
        target.Clear();
        foreach (var row in ordered) target.Add(row);
        ClientLogService.Instance.Info($"player.character.{logName}.bind count={target.Count}");
    }

    private void BindAttributeRows(object rawAttributes, string context)
    {
        AttributeStatRows.Clear();
        foreach (var item in ToObjectList(rawAttributes).Cast<object>())
        {
            var map = AsMap(item, context);
            if (map == null) continue;
            var attributeId = FirstNonEmpty(GetString(map, "attributeId"), GetString(map, "id"), GetString(map, "code"));
            if (string.IsNullOrWhiteSpace(attributeId)) continue;
            var code = FirstNonEmpty(GetString(map, "code"), attributeId);
            var value = FirstNonEmpty(GetString(map, "value"), GetString(map, "currentValue"), MissingDataText);
            var min = GetInt(map, "minValue");
            var max = GetInt(map, "maxValue");
            var row = new StatRowVm
            {
                AttributeId = attributeId,
                Code = code,
                Label = FirstNonEmpty(GetString(map, "displayName"), GetString(map, "label"), attributeId),
                Value = value,
                Description = GetString(map, "description"),
                SortOrder = GetInt(map, "sortOrder"),
                RangeText = max > min ? $"{min}..{max}" : string.Empty,
                AutomationScope = "Attribute"
            };
            foreach (var subItem in ToObjectList(map.ContainsKey("subAttributes") ? map["subAttributes"] : null).Cast<object>())
            {
                var subMap = AsMap(subItem, $"{context}.subAttributes");
                if (subMap == null) continue;
                var subAttributeId = FirstNonEmpty(GetString(subMap, "subAttributeId"), GetString(subMap, "id"), GetString(subMap, "code"));
                if (string.IsNullOrWhiteSpace(subAttributeId)) continue;
                var subCode = FirstNonEmpty(GetString(subMap, "code"), subAttributeId);
                var subMin = GetInt(subMap, "minValue");
                var subMax = GetInt(subMap, "maxValue");
                row.SubAttributes.Add(new StatRowVm
                {
                    AttributeId = subAttributeId,
                    Code = subCode,
                    Label = FirstNonEmpty(GetString(subMap, "displayName"), GetString(subMap, "label"), subAttributeId),
                    Value = FirstNonEmpty(GetString(subMap, "value"), GetString(subMap, "currentValue"), MissingDataText),
                    Description = GetString(subMap, "description"),
                    SortOrder = GetInt(subMap, "sortOrder"),
                    RangeText = subMax > subMin ? $"{subMin}..{subMax}" : string.Empty,
                    AutomationScope = "SubAttribute"
                });
            }
            AttributeStatRows.Add(row);
        }

        var ordered = AttributeStatRows.OrderBy(x => x.SortOrder).ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToList();
        AttributeStatRows.Clear();
        foreach (var row in ordered) AttributeStatRows.Add(row);
        ClientLogService.Instance.Info($"player.character.attributes.bind count={AttributeStatRows.Count}");
    }

    private void InitializeDefaultCharacterScaffolding()
    {
        if (StatsRows.Count == 0)
        {
            var empty = new Dictionary<string, object>();
            AddStat("HP", empty, "health");
            AddStat("Физ. броня", empty, "physicalArmor");
            AddStat("Маг. броня", empty, "magicalArmor");
            AddStat("Мораль", empty, "morale");
        }

        if (MoneyRows.Count == 0)
            MoneyRows.Add(new CurrencyRowVm { CurrencyId = "empty", Code = "empty", Name = "Валюты", Color = "#9AA7C7", Kind = "empty", IsEmptyState = true });

        EnsureCollectionPlaceholder(InventoryRows, "Инвентарь пока не загружен.");
        EnsureCollectionPlaceholder(HoldingsRows, "Владения пока не раскрыты.");
        EnsureReputationPlaceholder();
        EnsureCompanionsPlaceholder();
        SelectedCompanion = Companions.FirstOrDefault();
        EnsureCollectionPlaceholder(NoteRows, "Нет заметок");
        EnsureCollectionPlaceholder(CharacterKnowledgeRows, "Статус: Недоступно / Отложено • Причина: знания персонажа ещё не раскрыты сервером • Target: 0.15");
        EnsureCollectionPlaceholder(CharacterResearchRows, "Статус: Недоступно / Отложено • Причина: исследования персонажа ещё не подключены к рабочему player-safe потоку • Target: 0.15");
        EnsureCollectionPlaceholder(CharacterCraftingRows, "Статус: Недоступно / Отложено • Причина: крафт персонажа ещё не подключён к рабочему player-safe потоку • Target: 0.15");
        RefreshLocalNotesForCurrentCharacter();
        RebuildStatGroups();
        RebuildClassNavigation();
        BuildGameFeed();
    }


    private void InitializeDefaultPublicProfile()
    {
        PublicProfileIdentityRows.Clear();
        PublicProfileSummaryRows.Clear();
        PublicProfileHiddenRows.Clear();
        PublicProfileName = "Персонаж не выбран";
        PublicProfileSubtitle = "Нет данных";
        PublicProfileStatusText = "Публичный профиль не загружен";
        PublicProfileHintText = "Введите CharacterId и откройте профиль";
        PublicProfileDescription = "Публичные данные персонажа пока не загружены.";
        NotifyPublicProfile();
    }

    private void RebuildClassDirectionsForSelectedHexagon()
    {
        var activeHexagonId = string.IsNullOrWhiteSpace(SelectedDevelopmentHexagonId) ? DevelopmentHexagonIds.Main : SelectedDevelopmentHexagonId;
        var previousDirection = SelectedClassDirectionKey;
        ClassDirections.Clear();

        var nodes = ClassNodes
            .Where(n => string.Equals(string.IsNullOrWhiteSpace(n.HexagonId) ? DevelopmentHexagonIds.Main : n.HexagonId, activeHexagonId, StringComparison.OrdinalIgnoreCase))
            .Where(n => !string.IsNullOrWhiteSpace(n.DirectionKey))
            .Where(n => !string.Equals(n.DirectionKey, "root", StringComparison.OrdinalIgnoreCase))
            .GroupBy(n => n.DirectionKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Min(n => n.SortOrder <= 0 ? 9999 : n.SortOrder))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in nodes)
        {
            var first = group.OrderBy(n => n.SortOrder).ThenBy(n => n.Title).First();
            ClassDirections.Add(new ClassDirectionVm
            {
                Key = group.Key,
                Label = FormatDevelopmentDirectionLabel(group.Key),
                Summary = $"{first.HexagonName}: {group.Count()} узл."
            });
        }

        if (ClassDirections.Count == 0)
        {
            ClassDirections.Add(new ClassDirectionVm
            {
                Key = string.Equals(activeHexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase) ? "magic_root" : "strength_assault",
                Label = string.Equals(activeHexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase) ? "Пробуждение" : "Сила - Натиск",
                Summary = "Направления развития пока не загружены."
            });
        }

        if (!ClassDirections.Any(d => string.Equals(d.Key, previousDirection, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedClassDirectionKey = ClassDirections.First().Key;
            Notify(nameof(SelectedClassDirectionKey));
        }
    }

    private static string FormatDevelopmentDirectionLabel(string key)
    {
        return (key ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "strength_assault" => "Сила - Натиск",
            "dexterity_maneuver" => "Ловкость - Манёвр",
            "endurance_resilience" => "Выносливость - Стойкость",
            "intellect_reason" => "Интеллект - Разум",
            "wisdom_path" => "Мудрость - Путь",
            "charisma_influence" => "Харизма - Влияние",
            "magic_root" => "Пробуждение",
            "magic_mana" => "Мана",
            "magic_spell" => "Заклинания",
            "magic_seal" => "Печати",
            "magic_arcana" => "Аркана",
            "magic_element_fire" => "Стихия огня",
            "magic_direction_light" => "Направление света",
            _ => string.IsNullOrWhiteSpace(key) ? "Направление" : PlayerDevelopmentGraphDisplay.ToReadableText(key)
        };
    }

    private void RebuildClassNavigation()
    {
        RebuildClassDirectionsForSelectedHexagon();
        ClassBranches.Clear();
        var direction = ClassDirections.FirstOrDefault(d => d.Key == SelectedClassDirectionKey) ?? ClassDirections.FirstOrDefault();
        if (direction == null)
            return;

        var activeHexagonId = string.IsNullOrWhiteSpace(SelectedDevelopmentHexagonId) ? DevelopmentHexagonIds.Main : SelectedDevelopmentHexagonId;
        var directionNodes = ClassNodes
            .Where(n => !string.Equals(n.NodeId, "novice", StringComparison.OrdinalIgnoreCase))
            .Where(n => string.Equals(string.IsNullOrWhiteSpace(n.HexagonId) ? DevelopmentHexagonIds.Main : n.HexagonId, activeHexagonId, StringComparison.OrdinalIgnoreCase))
            .Where(n => string.Equals(n.DirectionKey, direction.Key, StringComparison.OrdinalIgnoreCase) || n.NodeId.StartsWith(direction.Key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var group in directionNodes.GroupBy(n => string.IsNullOrWhiteSpace(n.BranchKey) ? n.NodeId : n.BranchKey))
        {
            var first = group.OrderBy(n => n.Y).ThenBy(n => n.X).First();
            ClassBranches.Add(new ClassBranchVm
            {
                Key = group.Key,
                DirectionKey = direction.Key,
                Title = PlayerDevelopmentGraphDisplay.ToReadableText(FirstNonEmpty(first.Title, group.Key)),
                Summary = $"Ветка направления {direction.Label}. Узлов: {group.Count()}.",
                Status = first.State
            });
        }

        foreach (var node in Enumerable.Empty<ClassNodeVisualVm>())
        {
            ClassBranches.Add(new ClassBranchVm
            {
                Key = node.NodeId,
                DirectionKey = direction.Key,
                Title = PlayerDevelopmentGraphDisplay.ToReadableText(node.Title),
                Summary = $"Ветка направления {direction.Label}. Подробное развитие будет раскрыто позже.",
                Status = node.State
            });
        }

        if (ClassBranches.Count == 0)
        {
            ClassBranches.Add(new ClassBranchVm
            {
                Key = direction.Key + "_placeholder_branch",
                DirectionKey = direction.Key,
                Title = "Ветка пока недоступна",
                Summary = "Для выбранного направления пока нет раскрытых веток.",
                Status = ""
            });
        }

        SelectedClassBranch = ClassBranches.FirstOrDefault();
        Notify(nameof(HasClassBranches));
        Notify(nameof(SelectedClassDirectionDisplay));
    }

    private void RebuildClassEntries()
    {
        ClassEntries.Clear();
        if (SelectedClassBranch == null)
        {
            SelectedClassEntry = null;
            NotifyClassDetail();
            return;
        }

        if (!SelectedClassBranch.Key.EndsWith("_placeholder_branch", StringComparison.OrdinalIgnoreCase))
        {
            var activeHexagonId = string.IsNullOrWhiteSpace(SelectedDevelopmentHexagonId) ? DevelopmentHexagonIds.Main : SelectedDevelopmentHexagonId;
            foreach (var node in ClassNodes
                .Where(n => string.Equals(string.IsNullOrWhiteSpace(n.HexagonId) ? DevelopmentHexagonIds.Main : n.HexagonId, activeHexagonId, StringComparison.OrdinalIgnoreCase))
                .Where(n => string.Equals(n.BranchKey, SelectedClassBranch.Key, StringComparison.OrdinalIgnoreCase) || string.Equals(n.NodeId, SelectedClassBranch.Key, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.Y)
                .ThenBy(n => n.X))
            {
                ClassEntries.Add(new ClassEntryVm
                {
                    NodeId = node.NodeId,
                    HexagonId = node.HexagonId,
                    HexagonName = node.HexagonName,
                    NodeTypeLabel = node.NodeTypeLabel,
                    DirectionKey = node.DirectionKey,
                    BranchKey = SelectedClassBranch.Key,
                    Title = FirstNonEmpty(node.Title, node.NodeId),
                    Summary = FirstNonEmpty(node.Summary, node.RewardSummary),
                    Status = node.State,
                    RequirementSummary = node.RequirementSummary,
                    RewardSummary = node.RewardSummary,
                    RequiredNodeIds = node.RequiredNodeIds,
                    LinkedClassId = node.LinkedClassId,
                    CurrencyId = node.CurrencyId,
                    PositionX = node.PositionX,
                    PositionY = node.PositionY,
                    Ring = node.Ring,
                    Sector = node.Sector,
                    SortOrder = node.SortOrder,
                    LayoutVersion = node.LayoutVersion,
                    CostExperienceCoins = node.CostExperienceCoins,
                    CostText = node.CostText,
                    CanPurchase = node.CanPurchase,
                    RequiresRequest = node.RequiresRequest
                });
            }

            SelectedClassEntry = ClassEntries.FirstOrDefault();
            Notify(nameof(HasClassEntries));
            Notify(nameof(SelectedClassBranchTitle));
            Notify(nameof(SelectedClassBranchSummary));
            return;
        }

        if (SelectedClassBranch.Key.EndsWith("_placeholder_branch", StringComparison.OrdinalIgnoreCase))
        {
            ClassEntries.Add(new ClassEntryVm
            {
                NodeId = SelectedClassBranch.Key + "_class",
                BranchKey = SelectedClassBranch.Key,
                Title = "Узел пока недоступен",
                Summary = "Для этой ветки пока нет раскрытых узлов развития.",
                Status = ""
            });
        }
        else
        {
            var branchTitle = SelectedClassBranch.Title;
            ClassEntries.Add(new ClassEntryVm
            {
                NodeId = SelectedClassBranch.Key,
                BranchKey = SelectedClassBranch.Key,
                Title = branchTitle,
                Summary = "Базовый узел ветки. Подробности развития появятся позже.",
                Status = SelectedClassBranch.Status
            });
            ClassEntries.Add(new ClassEntryVm
            {
                NodeId = SelectedClassBranch.Key + "_advanced",
                BranchKey = SelectedClassBranch.Key,
                Title = branchTitle + " — следующий шаг",
                Summary = "Продвинутое развитие будет добавлено позже.",
                Status = ""
            });
        }

        SelectedClassEntry = ClassEntries.FirstOrDefault();
        Notify(nameof(HasClassEntries));
        Notify(nameof(SelectedClassBranchTitle));
        Notify(nameof(SelectedClassBranchSummary));
    }

    private void NotifyClassDetail()
    {
        Notify(nameof(SelectedClassDirectionDisplay));
        Notify(nameof(SelectedClassBranchTitle));
        Notify(nameof(SelectedClassBranchSummary));
        Notify(nameof(SelectedClassEntryTitle));
        Notify(nameof(SelectedClassEntrySummary));
        Notify(nameof(SelectedClassEntryState));
        Notify(nameof(SelectedClassEntryRequirements));
        Notify(nameof(SelectedClassEntryReward));
        Notify(nameof(SelectedClassEntryCost));
        Notify(nameof(SelectedClassEntryMeta));
        Notify(nameof(SelectedClassEntryUnlock));
        Notify(nameof(SelectedClassEntryPosition));
        Notify(nameof(SelectedClassEntryDirection));
        Notify(nameof(SelectedClassEntryRequiredNodeIds));
        Notify(nameof(SelectedClassEntryLayoutVersion));
        Notify(nameof(DevelopmentViewerPurchaseExplanation));
        Notify(nameof(CanBuySelectedClassNode));
        Notify(nameof(CanRequestSelectedClassNode));
        Notify(nameof(HasSelectedClassEntry));
    }

    private void AddPublicProfileField(ObservableCollection<PublicProfileFieldVm> target, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target.Add(new PublicProfileFieldVm { Label = label, Value = value });
    }

    private void NotifyPublicProfile()
    {
        Notify(nameof(PublicProfileName));
        Notify(nameof(PublicProfileSubtitle));
        Notify(nameof(PublicProfileStatusText));
        Notify(nameof(PublicProfileHintText));
        Notify(nameof(PublicProfileDescription));
        Notify(nameof(HasPublicProfileData));
    }

    private static string GetStringOrFallback(Dictionary<string, object> map, string key, string fallback)
    {
        var value = GetString(map, key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string GetDirectionLabel(string key)
    {
        return key switch
        {
            "defender" => "Защитник",
            "vanguard" => "Авангард",
            "ranger" => "Рейджер",
            "samurai" => "Самурай",
            "mage" => "Маг",
            "inventor" => "Изобретатель",
            _ => "Без направления"
        };
    }

    private static string FormatDevelopmentNodeState(string state, bool acquired, bool available)
    {
        if (acquired) return "Куплен";
        if (available) return "Доступен";
        return (state ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "purchased" or "taken" or "start" => "Куплен",
            "available" or "unlocked" => "Доступен",
            "locked" => "Закрыт",
            _ => "Закрыт"
        };
    }

    private static string JoinObjectList(Dictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return string.Empty;
        var items = ToObjectList(value).Cast<object>()
            .Select(x => Convert.ToString(x, CultureInfo.InvariantCulture) ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(5)
            .ToArray();
        return items.Length == 0 ? string.Empty : string.Join("; ", items);
    }

    private void EnsureCollectionPlaceholder(ObservableCollection<string> collection, string placeholder)
    {
        if (collection.Count == 0)
            collection.Add(placeholder);
    }

    private void EnsureReputationPlaceholder()
    {
        if (ReputationRows.Count == 0)
            ReputationRows.Add(new ReputationRowVm { TargetName = "Репутация не раскрыта", TargetType = "Other", ScopeType = "Character", Value = 0, Notes = "Репутация пока не раскрыта или отсутствует." });
    }

    private void EnsureCompanionsPlaceholder()
    {
        if (Companions.Count == 0)
        {
            var vm = new CompanionVm { Name = "Компаньон не выбран", Type = "Не указано", Species = "Не указано", Notes = "Заметок нет.", Description = "Данные компаньона пока не раскрыты.", OwnerCharacterId = ActiveCharacterId };
            AddCompanionStatScaffold(vm);
            vm.InventoryRows.Add("Нет данных");
            vm.HoldingsRows.Add("Нет данных");
            vm.SkillsRows.Add("Нет данных");
            vm.ClassRows.Add("Развитие компаньона пока не загружено.");
            vm.KnowledgeRows.Add("Знания компаньона пока не раскрыты.");
            vm.ResearchRows.Add("Статус: Недоступно / Отложено • Причина: исследования компаньона ещё не раскрыты сервером • Target: 0.15");
            Companions.Add(vm);
        }
    }

    private void AddCompanionStatScaffold(CompanionVm vm)
    {
        if (vm.StatsRows.Count > 0) return;
        AddCompanionStat(vm, "HP", MissingDataText, true);
        AddCompanionStat(vm, "Физ. защита", MissingDataText, true);
        AddCompanionStat(vm, "Маг. защита", MissingDataText, true);
        AddCompanionStat(vm, "Мораль", MissingDataText, true);
        AddCompanionStat(vm, "Сила", MissingDataText, false);
        AddCompanionStat(vm, "Ловкость", MissingDataText, false);
        AddCompanionStat(vm, "Выносливость", MissingDataText, false);
        AddCompanionStat(vm, "Мудрость", MissingDataText, false);
        AddCompanionStat(vm, "Интеллект", MissingDataText, false);
        AddCompanionStat(vm, "Харизма", MissingDataText, false);
    }

    private void ApplyCompanionStats(CompanionVm vm, Dictionary<string, object> stats)
    {
        vm.StatsRows.Clear();
        vm.CoreStatRows.Clear();
        vm.AttributeStatRows.Clear();
        AddCompanionStat(vm, "HP", GetMapValueOrDefault(stats, "health"), true);
        AddCompanionStat(vm, "Физ. защита", GetMapValueOrDefault(stats, "physicalArmor", "physicalDefense"), true);
        AddCompanionStat(vm, "Маг. защита", GetMapValueOrDefault(stats, "magicalArmor", "magicalDefense"), true);
        AddCompanionStat(vm, "Мораль", GetMapValueOrDefault(stats, "morale"), true);
        AddCompanionStat(vm, "Сила", GetMapValueOrDefault(stats, "strength"), false);
        AddCompanionStat(vm, "Ловкость", GetMapValueOrDefault(stats, "dexterity"), false);
        AddCompanionStat(vm, "Выносливость", GetMapValueOrDefault(stats, "endurance"), false);
        AddCompanionStat(vm, "Мудрость", GetMapValueOrDefault(stats, "wisdom"), false);
        AddCompanionStat(vm, "Интеллект", GetMapValueOrDefault(stats, "intellect"), false);
        AddCompanionStat(vm, "Харизма", GetMapValueOrDefault(stats, "charisma"), false);
    }

    private void AddCompanionStat(CompanionVm vm, string label, string value, bool isCore)
    {
        var row = new StatRowVm { Label = label, Value = string.IsNullOrWhiteSpace(value) ? MissingDataText : value };
        vm.StatsRows.Add(row);
        if (isCore)
            vm.CoreStatRows.Add(row);
        else
            vm.AttributeStatRows.Add(row);
    }

    private void BindCharacterKnowledge(Dictionary<string, object> payload)
    {
        CharacterKnowledgeRows.Clear();
        AppendLabeledValues(CharacterKnowledgeRows, payload, "Языки", "languages", "knownLanguages");
        AppendLabeledValues(CharacterKnowledgeRows, payload, "Локации", "discoveredLocations", "knownLocations");
        AppendLabeledValues(CharacterKnowledgeRows, payload, "Технологии", "knownTechnologies", "knownMethods");
        AppendLabeledValues(CharacterKnowledgeRows, payload, "Рецепты", "knownRecipes", "recipes");
        AppendLabeledValues(CharacterKnowledgeRows, payload, "Сведения", "knownFacts", "rumors", "intel");
        EnsureCollectionPlaceholder(CharacterKnowledgeRows, "Статус: Недоступно / Отложено • Причина: знания персонажа ещё не раскрыты сервером • Target: 0.15");
    }

    private void BindCharacterResearch(Dictionary<string, object> payload)
    {
        CharacterResearchRows.Clear();
        AppendLabeledValues(CharacterResearchRows, payload, "Активные исследования", "activeResearch");
        AppendLabeledValues(CharacterResearchRows, payload, "Изученные технологии", "researchedTechnologies");
        AppendLabeledValues(CharacterResearchRows, payload, "Чертежи", "blueprints");
        AppendLabeledValues(CharacterResearchRows, payload, "Методики", "researchMethods");
        AppendLabeledValues(CharacterResearchRows, payload, "Незавершённые проекты", "pendingProjects");
        EnsureCollectionPlaceholder(CharacterResearchRows, "Статус: Недоступно / Отложено • Причина: исследования персонажа ещё не подключены к рабочему player-safe потоку • Target: 0.15");
    }

    private void BindCharacterCrafting(Dictionary<string, object> payload)
    {
        CharacterCraftingRows.Clear();
        AppendLabeledValues(CharacterCraftingRows, payload, "Рецепты", "craftRecipes");
        AppendLabeledValues(CharacterCraftingRows, payload, "Материалы", "craftMaterials");
        AppendLabeledValues(CharacterCraftingRows, payload, "Активные работы", "craftJobs");
        AppendLabeledValues(CharacterCraftingRows, payload, "Результаты", "craftResults");
        EnsureCollectionPlaceholder(CharacterCraftingRows, "Статус: Недоступно / Отложено • Причина: крафт персонажа ещё не подключён к рабочему player-safe потоку • Target: 0.15");
    }

    private void RefreshCharacterCrafting()
    {
        var characterId = FirstNonEmpty(ActiveCharacterId, SelectedCharacterId).Trim();
        if (string.IsNullOrWhiteSpace(characterId)) return;

        var rows = new List<string>();
        try
        {
            var payload = new Dictionary<string, object>
            {
                { "campaignId", "default" },
                { "characterId", characterId },
                { "includeArchived", false }
            };

            var recipes = _api.CraftingPlayerRecipeList(payload);
            if (recipes.Status == ResponseStatus.Forbidden)
            {
                CharacterCraftingRows.Clear();
                CharacterCraftingRows.Add("Крафт пока недоступен.");
                return;
            }

            if (recipes.Status == ResponseStatus.Ok)
            {
                foreach (var item in ExtractCraftingItems(recipes.Payload))
                {
                    var recipe = AsMap(item, CommandNames.CraftingPlayerRecipeList);
                    if (recipe == null) continue;
                    var name = FirstNonEmpty(GetString(recipe, "name"), GetString(recipe, "recipeId"), "Без названия");
                    var output = FirstNonEmpty(GetString(recipe, "outputName"), GetString(recipe, "outputItemDefinitionId"), "Результат не указан");
                    var quantity = FirstNonEmpty(GetString(recipe, "outputQuantity"), "1");
                    var difficulty = FirstNonEmpty(GetString(recipe, "difficultyTier"), GetString(recipe, "recipeType"), "—");
                    rows.Add($"Рецепт: {name} > {output} x{quantity} | сложность: {difficulty}");
                }
            }

            var projects = _api.CraftingPlayerProjectList(payload);
            if (projects.Status == ResponseStatus.Forbidden && rows.Count == 0)
            {
                CharacterCraftingRows.Clear();
                CharacterCraftingRows.Add("Крафтовые проекты пока недоступны.");
                return;
            }

            if (projects.Status == ResponseStatus.Ok)
            {
                foreach (var item in ExtractCraftingItems(projects.Payload))
                {
                    var project = AsMap(item, CommandNames.CraftingPlayerProjectList);
                    if (project == null) continue;
                    var name = FirstNonEmpty(GetString(project, "recipeName"), GetString(project, "title"), GetString(project, "projectId"), "Проект без названия");
                    var status = FirstNonEmpty(GetString(project, "status"), "draft");
                    var progress = FirstNonEmpty(GetString(project, "progressPercent"), MissingDataText);
                    var progressText = string.Equals(progress, MissingDataText, StringComparison.Ordinal) ? progress : $"{progress}%";
                    var result = FirstNonEmpty(GetString(project, "resultStatus"), "—");
                    rows.Add($"Проект: {name} | статус: {status} | прогресс: {progressText} | результат: {result}");
                }
            }

            CharacterCraftingRows.Clear();
            foreach (var row in rows.Distinct(StringComparer.OrdinalIgnoreCase))
                CharacterCraftingRows.Add(row);
            EnsureCollectionPlaceholder(CharacterCraftingRows, "Активных крафтовых проектов пока нет.");
            ClientLogService.Instance.Info($"player.crafting.refresh.done characterId={characterId} rows={CharacterCraftingRows.Count}");
        }
        catch (Exception ex)
        {
            CharacterCraftingRows.Clear();
            CharacterCraftingRows.Add("Крафт пока недоступен.");
            ClientLogService.Instance.Warn($"player.crafting.refresh.warning characterId={characterId} message={ex.Message}");
        }
    }

    private void BindCompanionKnowledge(CompanionVm vm, Dictionary<string, object> payload)
    {
        vm.KnowledgeRows.Clear();
        AppendLabeledValues(vm.KnowledgeRows, payload, "Языки", "languages", "knownLanguages");
        AppendLabeledValues(vm.KnowledgeRows, payload, "Сведения", "knownFacts", "rumors");
        AppendLabeledValues(vm.KnowledgeRows, payload, "Локации", "discoveredLocations", "knownLocations");
        EnsureCollectionPlaceholder(vm.KnowledgeRows, "Знания компаньона пока не раскрыты.");
    }

    private void BindCompanionResearch(CompanionVm vm, Dictionary<string, object> payload)
    {
        vm.ResearchRows.Clear();
        AppendLabeledValues(vm.ResearchRows, payload, "Активные исследования", "activeResearch");
        AppendLabeledValues(vm.ResearchRows, payload, "Изученное", "researchedTechnologies", "researchMethods");
        EnsureCollectionPlaceholder(vm.ResearchRows, "Статус: Недоступно / Отложено • Причина: исследования компаньона ещё не раскрыты сервером • Target: 0.15");
    }

    private void BindCompanionCrafting(CompanionVm vm, Dictionary<string, object> payload)
    {
        vm.CraftingRows.Clear();
        AppendLabeledValues(vm.CraftingRows, payload, "Рецепты", "craftRecipes", "knownRecipes");
        AppendLabeledValues(vm.CraftingRows, payload, "Материалы", "craftMaterials");
        AppendLabeledValues(vm.CraftingRows, payload, "Активные работы", "craftJobs", "craftingProjects");
        EnsureCollectionPlaceholder(vm.CraftingRows, "Статус: Недоступно / Отложено • Причина: крафт компаньона ещё не подключён к рабочему player-safe потоку • Target: 0.15");
    }

    private static IList ExtractCraftingItems(Dictionary<string, object> payload)
    {
        foreach (var key in new[] { "items", "recipes", "projects" })
        {
            if (payload.TryGetValue(key, out var raw))
                return ToObjectList(raw);
        }

        return new ArrayList();
    }

    private void AppendLabeledValues(ObservableCollection<string> target, Dictionary<string, object> payload, string label, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.ContainsKey(key)) continue;
            var values = ToObjectList(payload[key]).Cast<object>()
                .Select(item => Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (values.Length == 0) continue;
            foreach (var value in values)
                target.Add($"{label}: {value}");
        }
    }

    private bool IsOwnDiceRoll(Dictionary<string, object> map)
    {
        var currentUser = FirstNonEmpty(PlayerDisplayName, LoginText).Trim();
        var currentUserLower = currentUser.ToLowerInvariant();
        var activeCharacter = FirstNonEmpty(ActiveCharacterId, SelectedCharacterId).Trim();

        var loginOwner = FirstNonEmpty(GetString(map, "creatorLogin"), GetString(map, "sender"), GetString(map, "ownerLogin")).Trim().ToLowerInvariant();
        var userIdOwner = FirstNonEmpty(GetString(map, "creatorUserId"), GetString(map, "userId"), GetString(map, "actorUserId"), GetString(map, "ownerUserId")).Trim();
        var characterOwner = FirstNonEmpty(GetString(map, "characterId"), GetString(map, "actorCharacterId"), GetString(map, "ownerCharacterId")).Trim();

        if (!string.IsNullOrWhiteSpace(currentUserLower) && !string.IsNullOrWhiteSpace(loginOwner) && string.Equals(currentUserLower, loginOwner, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(currentUser) && !string.IsNullOrWhiteSpace(userIdOwner) && string.Equals(currentUser, userIdOwner, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(activeCharacter) && !string.IsNullOrWhiteSpace(characterOwner) && string.Equals(activeCharacter, characterOwner, StringComparison.Ordinal))
            return true;

        return false;
    }

    private static string BuildPreviewText(string? source, int maxLength)
    {
        var text = (source ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text)) return "—";
        if (text.Length <= maxLength) return text;
        return text.Substring(0, Math.Max(0, maxLength - 1)) + "…";
    }


    public void Shutdown()
    {
        ClientLogService.Instance.Info("Logout / shutdown requested from Player client");
        _poller.Stop();
        _client.Disconnect();
    }

    private string ToServerChatType(string uiType)
    {
        return uiType switch
        {
            "Обычное" => "Public",
            "Действие" => "Public",
            "Вопрос GM" => "AdminOnly",
            "Общее" => "Public",
            "Скрыто от админов" => "HiddenToAdmins",
            "Только админам" => "AdminOnly",
            _ => "Public"
        };
    }

    private string ToServerDiceVisibility(string uiValue)
    {
        return uiValue switch
        {
            "Публично" => "Public",
            "Скрыто" => "HiddenToAdmins",
            "Общее" => "Public",
            "Только GM" => "AdminOnly",
            "" => "HiddenToAdmins",
            _ => "Public"
        };
    }

    private static string DefaultPlayerRequestTitle(string requestType)
    {
        switch ((requestType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "generic_action":
                return "Заявить действие";
            case "development_unlock":
                return "Запрос развития";
            case "item_request":
                return "Запрос предмета";
            case "rules_question":
                return "Вопрос по правилам";
            case "research":
                return "Запросить исследование";
            case "crafting":
                return "Запросить крафт";
            case "scene_action":
                return "Заявить действие";
            case "question":
                return "Вопрос GM";
            case "purchase":
                return "Запрос покупки";
            case "character_change":
                return "Изменение персонажа";
            default:
                return "Заявка GM";
        }
    }
    private string ParseRequestIdFromRow(string row)
    {
        if (!string.IsNullOrWhiteSpace(row) && _requestRowIds.TryGetValue(row, out var mappedId))
            return mappedId;
        if (string.IsNullOrWhiteSpace(row) || row.IndexOf("|", StringComparison.Ordinal) < 0) return string.Empty;
        var first = row.Split('|')[0].Trim();
        return first.StartsWith("№", StringComparison.Ordinal) ? string.Empty : first;
    }

    private void ApplySelectedRequestDetailsFromRow(string row)
    {
        var parts = (row ?? string.Empty).Split('|').Select(part => part.Trim()).ToArray();
        SelectedRequestStatus = parts.Length > 2 ? parts[2] : string.Empty;
        _selectedRequestRawTitle = parts.Length > 3 ? parts[3] : string.Empty;
        SelectedRequestTitle = parts.Length > 3 ? $"{parts[0]} — {parts[3]}" : "Заявка не выбрана.";
        SelectedRequestActors = parts.Length > 4 ? parts[4] : "Участники: —";
        SelectedRequestDetails = parts.Length > 5 ? parts[5] : (parts.Length > 4 ? parts[4] : string.Empty);
        var decisionIndex = row?.IndexOf("Решение GM:", StringComparison.OrdinalIgnoreCase) ?? -1;
        SelectedRequestDecision = decisionIndex >= 0 ? row!.Substring(decisionIndex).Trim() : string.Empty;
        Notify(nameof(SelectedRequestStatus));
        Notify(nameof(SelectedRequestTitle));
        Notify(nameof(SelectedRequestActors));
        Notify(nameof(SelectedRequestDetails));
        Notify(nameof(SelectedRequestDecision));
    }

    private static string FormatRequestNumberForDisplay(params string[] values)
    {
        foreach (var value in values)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (text.StartsWith("№", StringComparison.Ordinal)) return text;
            if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0)
                return "№ " + number.ToString(CultureInfo.InvariantCulture);
        }

        return "№ —";
    }

    private string ExtractDiceTotal(Dictionary<string, object> map)
    {
        if (!map.TryGetValue("result", out var rawResult)) return "?";
        var resultMap = AsMap(rawResult, CommandNames.DiceVisibleFeed);
        if (resultMap == null) return "?";
        return FirstNonEmpty(GetString(resultMap, "total"), "?");
    }

    private static long ParseLongValue(Dictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var rawValue) || rawValue == null) return 0;
        return long.TryParse(Convert.ToString(rawValue, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static double ParseDoubleValue(Dictionary<string, object> payload, string key, double fallback)
    {
        if (!payload.TryGetValue(key, out var rawValue) || rawValue == null) return fallback;
        return double.TryParse(Convert.ToString(rawValue, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private string BuildDiceRollDetails(Dictionary<string, object> map, string context)
    {
        if (!map.TryGetValue("result", out var rawResult)) return string.Empty;
        var resultMap = AsMap(rawResult, context);
        if (resultMap == null || !resultMap.TryGetValue("rolls", out var rawRolls)) return string.Empty;
        var values = new List<string>();
        foreach (var item in ToObjectList(rawRolls))
        {
            var value = Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
                values.Add(value);
        }
        if (values.Count == 0) return string.Empty;
        var rolled = string.Join(",", values);
        var modifier = 0;
        if (resultMap.TryGetValue("modifier", out var rawModifier))
            int.TryParse(Convert.ToString(rawModifier, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out modifier);
        if (modifier == 0) return $" ({rolled})";
        return modifier > 0 ? $" ({rolled}+{modifier})" : $" ({rolled}{modifier})";
    }

    private void BuildGameFeed()
    {
        GameFeedRows.Clear();
        var filteredPlaceholders = 0;

        foreach (var item in ChatMessageRows)
        {
            GameFeedRows.Add(new GameFeedItemVm
            {
                Kind = item.IsSystem ? "System" : "Chat",
                Text = $"{item.Sender}: {item.Text}",
                IsMuted = item.IsSystem
            });
        }

        foreach (var item in EventRows)
        {
            if (IsPlaceholderText(item))
            {
                filteredPlaceholders++;
                continue;
            }

            GameFeedRows.Add(new GameFeedItemVm { Kind = "System", Text = item, IsMuted = true });
        }

        foreach (var item in DiceFeedRows)
        {
            if (IsPlaceholderText(item))
            {
                filteredPlaceholders++;
                continue;
            }

            GameFeedRows.Add(new GameFeedItemVm { Kind = "Dice", Text = item, IsMuted = true });
        }

        foreach (var item in RequestRows)
        {
            if (IsPlaceholderText(item))
            {
                filteredPlaceholders++;
                continue;
            }

            GameFeedRows.Add(new GameFeedItemVm { Kind = "Request", Text = item, IsMuted = true });
        }

        if (GameFeedRows.Count == 0)
            GameFeedRows.Add(new GameFeedItemVm { Kind = "Hint", Text = "Лента пока пуста.", IsMuted = true });

        var mergedDiceCount = 0;
        foreach (var row in GameFeedRows)
        {
            if (string.Equals(row.Kind, "Dice", StringComparison.Ordinal))
                mergedDiceCount++;
        }
        ClientLogService.Instance.Info($"gameFeed diceMerged={mergedDiceCount}");
        TraceChatDiagnostic($"game-feed build chat={ChatMessageRows.Count} event={EventRows.Count} dice={DiceFeedRows.Count} request={RequestRows.Count} filteredPlaceholders={filteredPlaceholders} final={GameFeedRows.Count}");
    }

    private void BuildMergedChatRows()
    {
        MergedChatRows.Clear();
        var timeline = new List<ChatMessageRowVm>();
        timeline.AddRange(ChatMessageRows);
        timeline.AddRange(DiceMessageRows.Where(item => !IsPlaceholderText(item.Text)));

        foreach (var item in EventRows)
        {
            if (IsPlaceholderText(item)) continue;
            timeline.Add(new ChatMessageRowVm
            {
                Sender = "System",
                Text = item,
                Timestamp = string.Empty,
                IsSystem = true
            });
        }

        var sorted = timeline
            .OrderBy(item => item.SortTicks == 0 ? long.MaxValue : item.SortTicks)
            .ThenBy(item => item.Timestamp, StringComparer.Ordinal)
            .ToList();
        foreach (var row in sorted)
            MergedChatRows.Add(row);

        ClientLogService.Instance.Info($"chat.window.timeline mergedCount={MergedChatRows.Count}");
        var first = MergedChatRows.Count > 0 ? $"{MergedChatRows[0].Sender}:{MergedChatRows[0].Timestamp}" : "<empty>";
        var last = MergedChatRows.Count > 0 ? $"{MergedChatRows[MergedChatRows.Count - 1].Sender}:{MergedChatRows[MergedChatRows.Count - 1].Timestamp}" : "<empty>";
        ClientLogService.Instance.Debug($"merged.timeline first={first}");
        ClientLogService.Instance.Debug($"merged.timeline last={last}");
        ClientLogService.Instance.Info("chat.window.timeline sorted=true");
        ClientLogService.Instance.Debug("merged.timeline sorted=true");
    }
    private void BindCurrencyRows(Dictionary<string, object> payload)
    {
        MoneyRows.Clear();
        ClearExperienceCoins();
        var rawCurrencies = payload.ContainsKey("currencies") ? payload["currencies"] : new ArrayList();
        foreach (var item in EnumeratePayloadItems(rawCurrencies))
        {
            var map = AsMap(item, CommandNames.CharacterGetActive);
            if (map == null) continue;
            var currencyId = FirstNonEmpty(GetString(map, "currencyId"), GetString(map, "id"), GetString(map, "code"));
            if (string.IsNullOrWhiteSpace(currencyId)) continue;
            var code = FirstNonEmpty(GetString(map, "code"), currencyId);
            var kind = FirstNonEmpty(GetString(map, "kind"), string.Equals(currencyId, CharacterCurrencyIds.XpCoin, StringComparison.OrdinalIgnoreCase) ? "experience" : "money");
            long.TryParse(FirstNonEmpty(GetString(map, "amount"), GetString(map, "value")), out var amount);
            var row = new CurrencyRowVm
            {
                CurrencyId = currencyId,
                Code = code,
                Name = FirstNonEmpty(GetString(map, "displayName"), GetString(map, "label"), code),
                Abbrev = FirstNonEmpty(GetString(map, "unit"), code),
                Color = CurrencyColor(currencyId, code),
                Amount = amount,
                Kind = kind,
                Description = GetString(map, "description"),
                SortOrder = ParseInt(GetString(map, "sortOrder"), 1000)
            };
            MoneyRows.Add(row);
            if (row.IsExperience) ExperienceCoins = row.Amount;
        }

        if (MoneyRows.Count > 0)
        {
            Notify(nameof(MoneyRows));
            return;
        }

        var money = payload.TryGetValue("money", out var moneyRaw)
            ? AsMap(moneyRaw, CommandNames.CharacterGetActive)
            : null;
        if (money != null && money.Count > 0)
        {
            AddLegacyMoneyIfPresent("iron_coin", "iron", "Железная монета", "Fe", "#B0BEC5", money, "Iron");
            AddLegacyMoneyIfPresent("bronze_coin", "bronze", "Бронзовая монета", "Br", "#B87333", money, "Bronze");
            AddLegacyMoneyIfPresent("silver_coin", "silver", "Серебряная монета", "Ag", "#C0C0C0", money, "Silver");
            AddLegacyMoneyIfPresent("gold_coin", "gold", "Золотая монета", "Au", "#FFD700", money, "Gold");
            AddLegacyMoneyIfPresent("platinum_coin", "platinum", "Платиновая монета", "Pt", "#E5E4E2", money, "Platinum");
        }

        if (MoneyRows.Count == 0)
        {
            MoneyRows.Add(new CurrencyRowVm { CurrencyId = "empty", Code = "empty", Name = "Валюты", Abbrev = string.Empty, Color = "#9AA7C7", Kind = "empty", IsEmptyState = true });
        }

        Notify(nameof(MoneyRows));
    }

    private void AddLegacyMoneyIfPresent(string currencyId, string code, string name, string abbr, string color, Dictionary<string, object> money, string key)
    {
        var raw = GetString(money, key);
        if (string.IsNullOrWhiteSpace(raw)) return;
        long.TryParse(raw, out var amount);
        MoneyRows.Add(new CurrencyRowVm { CurrencyId = currencyId, Code = code, Name = name, Abbrev = abbr, Color = color, Amount = amount, Kind = "money" });
    }

    private void ClearExperienceCoins()
    {
        _experienceCoins = 0;
        _experienceCoinsLoaded = false;
        Notify(nameof(ExperienceCoins));
        Notify(nameof(ExperienceCoinsDisplay));
    }

    private static string CurrencyColor(string currencyId, string code)
    {
        var normalized = FirstNonEmpty(currencyId, code).Replace("_coin", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "iron" => "#B0BEC5",
            "bronze" => "#B87333",
            "silver" => "#C0C0C0",
            "gold" => "#FFD700",
            "platinum" => "#E5E4E2",
            "orichalcum" => "#39FF14",
            "adamant" => "#5F9EA0",
            "sovereign" => "#B05CFF",
            "xp" => "#8AB4F8",
            _ => "#FFFFFF"
        };
    }

    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static double ParseDouble(string value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static IEnumerable<object> EnumeratePayloadItems(object value)
    {
        if (value is string || value == null) yield break;
        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item != null) yield return item;
            }
        }
    }

    private void NotifyHeader()
    {
        Notify(nameof(PlayerDisplayName));
        Notify(nameof(SessionSummary));
        Notify(nameof(IsAuthenticated));
    }

    private void NotifyCharacter()
    {
        Notify(nameof(CharacterName));
        Notify(nameof(CharacterRace));
        Notify(nameof(CharacterAge));
        Notify(nameof(CharacterHeight));
        Notify(nameof(CharacterDescription));
        Notify(nameof(CharacterBackstory));
        Notify(nameof(CharacterNameDisplay));
        Notify(nameof(CharacterRaceDisplay));
        Notify(nameof(CharacterAgeDisplay));
        Notify(nameof(CharacterHeightDisplay));
        Notify(nameof(CharacterBackstoryDisplay));
        Notify(nameof(CharacterVisibilityDisplay));
        Notify(nameof(CharacterOwnerDisplay));
        Notify(nameof(CharacterControllerDisplay));
        Notify(nameof(CharacterGroupDisplay));
        Notify(nameof(CharacterKindDisplay));
        Notify(nameof(CharacterStatusDisplay));
        Notify(nameof(CharacterOwnershipReadOnlySummary));
        Notify(nameof(ActiveCharacterStatusText));
        Notify(nameof(HasActiveCharacter));
    }

    private ChatMessageRowVm? BuildChatMessageRow(Dictionary<string, object> map)
    {
        var sender = FirstNonEmpty(GetString(map, "senderDisplayName"), GetString(map, "senderUserId"), "Система");
        var text = FirstNonEmpty(GetString(map, "text"), GetString(map, "message"), GetString(map, "body"));
        var type = FirstNonEmpty(GetString(map, "type"), "Public");
        var createdRaw = FirstNonEmpty(GetString(map, "createdUtc"), GetString(map, "createdAt"), GetString(map, "at"));
        var timestamp = FormatChatTimestamp(createdRaw);

        if (string.IsNullOrWhiteSpace(text))
        {
            TraceChatDiagnostic("chat-filter reason=empty-text");
            return null;
        }

        if (IsPlaceholderText(text))
        {
            TraceChatDiagnostic($"chat-filter reason=placeholder-text value={text}");
            return null;
        }

        return new ChatMessageRowVm
        {
            Sender = sender,
            Text = text,
            Timestamp = timestamp,
            IsSystem = string.Equals(type, "System", StringComparison.OrdinalIgnoreCase),
            SortTicks = ParseTimelineTicks(createdRaw)
        };
    }

    private static bool IsPlaceholderText(string text)
    {
        return string.Equals(text, "Нет данных", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "Лента пока пуста.", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "У вас пока нет бросков.", StringComparison.OrdinalIgnoreCase)
               || string.IsNullOrWhiteSpace(text);
    }

    private static string FormatChatTimestamp(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        if (TryParseServerTimestamp(rawValue, out var parsed))
        {
            var local = parsed.ToLocalTime();
            return local.Date == DateTime.Now.Date
                ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
                : local.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        return rawValue;
    }

    private static long ParseTimelineTicks(string rawValue)
    {
        if (TryParseServerTimestamp(rawValue, out var parsed))
            return parsed.Ticks;
        return 0;
    }

    private static bool TryParseServerTimestamp(string rawValue, out DateTime utcValue)
    {
        utcValue = default;
        var dateMatch = Regex.Match(rawValue, @"^/Date\(([-+]?\d+)");
        if (dateMatch.Success && long.TryParse(dateMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
        {
            utcValue = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
            return true;
        }

        if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            utcValue = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        return false;
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string GetString(Dictionary<string, object>? map, string key)
    {
        if (map == null || string.IsNullOrWhiteSpace(key)) return string.Empty;
        if (!map.TryGetValue(key, out var value) || value == null) return string.Empty;

        try
        {
            if (value is string s) return s;
            if (value is IDictionary || value is IList)
                return string.Empty;

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int GetInt(Dictionary<string, object>? map, string key)
    {
        if (map == null || !map.ContainsKey(key) || map[key] == null) return 0;
        if (map[key] is int i) return i;
        if (map[key] is long l) return l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l;
        return int.TryParse(Convert.ToString(map[key], CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static bool GetBool(Dictionary<string, object>? map, string key)
    {
        if (map == null || !map.ContainsKey(key) || map[key] == null) return false;
        if (map[key] is bool b) return b;
        return bool.TryParse(Convert.ToString(map[key], CultureInfo.InvariantCulture), out var parsed) && parsed;
    }

    private static string GetMapValueOrDefault(Dictionary<string, object>? map, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = GetString(map, key);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return MissingDataText;
    }

    private string ResolveChatSessionId()
    {
        var sessionId = string.IsNullOrWhiteSpace(ChatSessionId) ? "default" : ChatSessionId.Trim();
        if (!string.Equals(ChatSessionId, sessionId, StringComparison.Ordinal))
        {
            ChatSessionId = sessionId;
            Notify(nameof(ChatSessionId));
        }

        return sessionId;
    }

    private Dictionary<string, object>? AsMap(object? value, string context)
    {
        if (value is Dictionary<string, object> typedMap)
        {
            TraceChatDiagnostic($"map-shape command={context} branch=Dictionary<string,object> count={typedMap.Count}");
            return typedMap;
        }

        if (value is IDictionary dictionary)
        {
            var map = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                map[key] = entry.Value;
            }

            TraceChatDiagnostic($"map-shape command={context} branch=IDictionary count={map.Count}");
            return map.Count > 0 ? map : null;
        }

        if (value is object[] objectArray)
        {
            if (TryConvertObjectArrayToMap(objectArray, out var objectArrayMap))
            {
                TraceChatDiagnostic($"map-shape command={context} branch=object[] count={objectArrayMap.Count}");
                return objectArrayMap;
            }

            TraceChatDiagnostic($"map-shape command={context} branch=object[] fallback=failed length={objectArray.Length}");
            return null;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            if (TryConvertEnumerableToMap(enumerable, out var enumerableMap))
            {
                TraceChatDiagnostic($"map-shape command={context} branch=IEnumerable count={enumerableMap.Count}");
                return enumerableMap;
            }

            TraceChatDiagnostic($"map-shape command={context} branch=IEnumerable fallback=failed type={value.GetType().FullName}");
            return null;
        }

        TraceChatDiagnostic($"map-shape command={context} branch=unsupported type={value?.GetType().FullName ?? "null"}");
        return null;
    }

    private void LogFirstChatItemShape(IList items, string command)
    {
        if (items.Count == 0)
        {
            TraceChatDiagnostic($"first-item command={command} type=<none>");
            return;
        }

        var firstItem = items[0];
        var firstType = firstItem?.GetType().FullName ?? "null";
        TraceChatDiagnostic($"first-item command={command} type={firstType}");

        if (firstItem is IEnumerable enumerable && firstItem is not string)
        {
            var innerTypes = enumerable
                .Cast<object?>()
                .Take(6)
                .Select(item => item?.GetType().FullName ?? "null")
                .ToArray();
            TraceChatDiagnostic($"first-item-inner command={command} sampleTypes=[{string.Join(",", innerTypes)}]");
        }

        if (TryConvertPairLike(firstItem, out var key, out _, out var pairShape))
        {
            TraceChatDiagnostic($"first-item-pair command={command} shape={pairShape} key={key}");
        }
    }

    private static bool TryConvertObjectArrayToMap(object[] source, out Dictionary<string, object> map)
    {
        map = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source.Length == 0)
        {
            return false;
        }

        var asPairs = true;
        foreach (var item in source)
        {
            if (!TryConvertPairLike(item, out var key, out var value, out _))
            {
                asPairs = false;
                break;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                map[key] = value;
            }
        }

        if (asPairs && map.Count > 0)
        {
            return true;
        }

        if (source.Length % 2 != 0)
        {
            return false;
        }

        map.Clear();
        for (var i = 0; i < source.Length; i += 2)
        {
            var key = Convert.ToString(source[i]);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            map[key] = source[i + 1];
        }

        return map.Count > 0;
    }

    private static bool TryConvertEnumerableToMap(IEnumerable source, out Dictionary<string, object> map)
    {
        map = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var item in source)
        {
            if (!TryConvertPairLike(item, out var key, out var value, out _))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                map[key] = value;
            }
        }

        return map.Count > 0;
    }

    private static bool TryConvertPairLike(object? value, out string key, out object? mappedValue, out string shape)
    {
        key = string.Empty;
        mappedValue = null;
        shape = "unknown";

        if (value is DictionaryEntry dictionaryEntry)
        {
            key = Convert.ToString(dictionaryEntry.Key) ?? string.Empty;
            mappedValue = dictionaryEntry.Value;
            shape = "DictionaryEntry";
            return !string.IsNullOrWhiteSpace(key);
        }

        if (value is IDictionary pairDictionary)
        {
            object? keyValue = null;
            object? contentValue = null;
            var hasKey = false;
            var hasValue = false;
            foreach (DictionaryEntry entry in pairDictionary)
            {
                var entryKey = Convert.ToString(entry.Key);
                if (string.Equals(entryKey, "Key", StringComparison.OrdinalIgnoreCase))
                {
                    keyValue = entry.Value;
                    hasKey = true;
                }
                else if (string.Equals(entryKey, "Value", StringComparison.OrdinalIgnoreCase))
                {
                    contentValue = entry.Value;
                    hasValue = true;
                }
            }

            if (hasKey && hasValue)
            {
                key = Convert.ToString(keyValue) ?? string.Empty;
                mappedValue = contentValue;
                shape = "IDictionary[key/value]";
                return !string.IsNullOrWhiteSpace(key);
            }
        }

        if (value is object[] objectPair && objectPair.Length == 2)
        {
            key = Convert.ToString(objectPair[0]) ?? string.Empty;
            mappedValue = objectPair[1];
            shape = "object[2]";
            return !string.IsNullOrWhiteSpace(key);
        }

        if (value is IList listPair && listPair.Count == 2)
        {
            key = Convert.ToString(listPair[0]) ?? string.Empty;
            mappedValue = listPair[1];
            shape = "IList[2]";
            return !string.IsNullOrWhiteSpace(key);
        }

        if (value != null)
        {
            var valueType = value.GetType();
            var keyProperty = valueType.GetProperty("Key");
            var valueProperty = valueType.GetProperty("Value");
            if (keyProperty != null && valueProperty != null)
            {
                key = Convert.ToString(keyProperty.GetValue(value)) ?? string.Empty;
                mappedValue = valueProperty.GetValue(value);
                shape = valueType.FullName ?? valueType.Name;
                return !string.IsNullOrWhiteSpace(key);
            }
        }

        return false;
    }

    private static IList ExtractChatItems(Dictionary<string, object> payload, out string sourceKey, out string payloadKeys, out string rawItemsType)
    {
        payloadKeys = string.Join(",", payload.Keys.OrderBy(x => x, StringComparer.Ordinal));
        foreach (var key in new[] { "items", "messages", "feed", "history" })
        {
            if (!payload.ContainsKey(key))
            {
                continue;
            }

            var normalized = NormalizePayloadList(payload[key], out rawItemsType);
            sourceKey = key;
            return normalized;
        }

        foreach (var entry in payload)
        {
            if (entry.Value is string)
            {
                continue;
            }

            if (entry.Value is IEnumerable)
            {
                var normalized = NormalizePayloadList(entry.Value, out rawItemsType);
                sourceKey = entry.Key;
                return normalized;
            }
        }

        sourceKey = "<none>";
        rawItemsType = "<none>";
        return new ArrayList();
    }

    private static IList ExtractCharacterSkillsItems(Dictionary<string, object> payload, out string rawCollectionKey)
    {
        foreach (var key in new[] { "items", "skills", "characterSkills" })
        {
            if (!payload.ContainsKey(key))
            {
                continue;
            }

            rawCollectionKey = key;
            return NormalizePayloadList(payload[key], out _);
        }

        foreach (var entry in payload)
        {
            if (entry.Value is string)
            {
                continue;
            }

            if (entry.Value is IEnumerable)
            {
                rawCollectionKey = entry.Key;
                return NormalizePayloadList(entry.Value, out _);
            }
        }

        rawCollectionKey = "<none>";
        return new ArrayList();
    }

    private static List<DevelopmentHexagonVm> ExtractDevelopmentHexagons(Dictionary<string, object> payload)
    {
        var result = new List<DevelopmentHexagonVm>();
        foreach (var item in NormalizePayloadList(payload.TryGetValue("hexagons", out var rawHexagons) ? rawHexagons : new object[0], out _))
        {
            if (!TryAsMap(item, out var map)) continue;
            var hexagonId = FirstNonEmpty(GetString(map, "hexagonId"), DevelopmentHexagonIds.Main);
            if (result.Any(h => string.Equals(h.HexagonId, hexagonId, StringComparison.OrdinalIgnoreCase))) continue;
            result.Add(new DevelopmentHexagonVm
            {
                HexagonId = hexagonId,
                HexagonType = FirstNonEmpty(GetString(map, "hexagonType"), DevelopmentHexagonTypes.Main),
                Name = FirstNonEmpty(GetString(map, "name"), hexagonId),
                CenterNodeId = GetString(map, "centerNodeId"),
                SortOrder = ParseInt(GetString(map, "sortOrder"), result.Count + 1),
                Summary = FirstNonEmpty(GetString(map, "description"), GetString(map, "centerNodeName"))
            });
        }

        if (result.Count == 0 && payload.TryGetValue("hexagon", out var rawHexagon) && TryAsMap(rawHexagon, out var single))
        {
            result.Add(new DevelopmentHexagonVm
            {
                HexagonId = FirstNonEmpty(GetString(single, "hexagonId"), DevelopmentHexagonIds.Main),
                HexagonType = FirstNonEmpty(GetString(single, "hexagonType"), DevelopmentHexagonTypes.Main),
                Name = FirstNonEmpty(GetString(single, "name"), "Основной шестиугольник развития"),
                CenterNodeId = FirstNonEmpty(GetString(single, "centerNodeId"), "novice"),
                SortOrder = 1,
                Summary = GetString(single, "centerNodeName")
            });
        }

        return result;
    }

    private static IList ExtractDevelopmentHexagonItems(Dictionary<string, object> payload, out string rawCollectionKey, out string rawItemsType)
    {
        foreach (var key in new[] { "items", "nodes", "availableNodes", "hexagonNodes" })
        {
            if (!payload.TryGetValue(key, out var raw))
            {
                continue;
            }

            var normalized = NormalizePayloadList(raw, out rawItemsType);
            if (normalized.Count > 0)
            {
                rawCollectionKey = key;
                return normalized;
            }
        }

        foreach (var key in new[] { "data", "result", "response", "payload" })
        {
            if (!payload.TryGetValue(key, out var nestedRaw) || !TryAsMap(nestedRaw, out var nestedMap))
            {
                continue;
            }

            foreach (var nestedKey in new[] { "items", "nodes", "availableNodes", "hexagonNodes" })
            {
                if (!nestedMap.TryGetValue(nestedKey, out var raw))
                {
                    continue;
                }

                var normalized = NormalizePayloadList(raw, out rawItemsType);
                if (normalized.Count > 0)
                {
                    rawCollectionKey = key + "." + nestedKey;
                    return normalized;
                }
            }
        }

        foreach (var entry in payload)
        {
            if (entry.Value is string)
            {
                continue;
            }

            if (TryAsMap(entry.Value, out var nestedMap))
            {
                foreach (var nestedKey in new[] { "items", "nodes", "availableNodes", "hexagonNodes" })
                {
                    if (!nestedMap.TryGetValue(nestedKey, out var raw))
                    {
                        continue;
                    }

                    var normalized = NormalizePayloadList(raw, out rawItemsType);
                    if (normalized.Count > 0)
                    {
                        rawCollectionKey = entry.Key + "." + nestedKey;
                        return normalized;
                    }
                }
            }

            if (entry.Value is IEnumerable enumerable && entry.Value is not string)
            {
                var normalized = NormalizePayloadList(enumerable, out rawItemsType);
                if (normalized.Count > 0)
                {
                    rawCollectionKey = entry.Key;
                    return normalized;
                }
            }
        }

        rawCollectionKey = "<none>";
        rawItemsType = "<none>";
        return new ArrayList();
    }

    private static bool TryAsMap(object? value, out Dictionary<string, object> map)
    {
        map = new Dictionary<string, object>(StringComparer.Ordinal);
        if (value is Dictionary<string, object> typed)
        {
            map = typed;
            return map.Count > 0;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                map[key] = entry.Value;
            }

            return map.Count > 0;
        }

        if (value is object[] objectArray && TryConvertObjectArrayToMap(objectArray, out var objectArrayMap))
        {
            map = objectArrayMap;
            return true;
        }

        if (value is IEnumerable enumerable && value is not string && TryConvertEnumerableToMap(enumerable, out var enumerableMap))
        {
            map = enumerableMap;
            return true;
        }

        return false;
    }

    private static IList NormalizePayloadList(object? payloadValue, out string rawItemsType)
    {
        rawItemsType = payloadValue?.GetType().Name ?? "null";
        if (payloadValue is IList list) return list;
        if (payloadValue is IEnumerable enumerable && payloadValue is not string) return enumerable.Cast<object>().ToArray();
        return new ArrayList();
    }

    private void TraceChatDiagnostic(string message)
    {
        const bool enableChatDiagnostics = false;
        if (!enableChatDiagnostics) return;
        var line = "[CHAT-DIAG][Player] " + message;
        ClientLogService.Instance.Debug(line);
    }

    private static IList ToObjectList(object payload) => payload as IList ?? new ArrayList();

    internal void ChatVisibleFeedRefreshFromSync() => RefreshChat();
    internal void DiceFeedRefreshFromSync() => RefreshDiceAndRequests();
    internal void SetDefinitionsDirty(long revision) { _definitionsDirty = true; ClientLogService.Instance.Warn($"sync.definitions.dirty revision={revision}"); }
}

public static class SyncFeatureFlags
{
    public const bool UsePassiveSyncPoller = false;
    public const bool UseEventDispatcher = false;
}

public interface IClientSyncEventDispatcher
{
    System.Threading.Tasks.Task DispatchAsync(ClientSyncEvent evt);
}

public sealed class ClientSyncEventDispatcher : IClientSyncEventDispatcher
{
    private readonly PlayerMainViewModel _vm;
    private bool _chatRefreshInProgress;
    public ClientSyncEventDispatcher(PlayerMainViewModel vm) { _vm = vm; }
    public System.Threading.Tasks.Task DispatchAsync(ClientSyncEvent evt)
    {
        ClientLogService.Instance.Info($"sync.dispatch.start eventId={evt.EventId} revision={evt.Revision} type={evt.Type}");
        if (evt.Type.StartsWith("character.") || evt.Type.StartsWith("combat.")) { ClientLogService.Instance.Warn($"sync.dispatch.deferred eventId={evt.EventId} type={evt.Type} reason=profile_migration_pending"); return System.Threading.Tasks.Task.CompletedTask; }
        if (evt.Type == "audio.state.changed") { ClientLogService.Instance.Warn($"sync.dispatch.deferred eventId={evt.EventId} type={evt.Type} reason=audio_not_ready"); return System.Threading.Tasks.Task.CompletedTask; }
        switch (evt.Type)
        {
            case "chat.message.created": if (_chatRefreshInProgress) { ClientLogService.Instance.Warn("sync.dispatch.deferred eventId="+evt.EventId+" type=chat.message.created reason=chat_refresh_in_progress"); break; } _chatRefreshInProgress=true; _vm.ChatVisibleFeedRefreshFromSync(); _chatRefreshInProgress=false; ClientLogService.Instance.Info($"sync.dispatch.done eventId={evt.EventId} type={evt.Type} action=chat.refresh"); break;
            case "dice.roll.created": _vm.DiceFeedRefreshFromSync(); ClientLogService.Instance.Info($"sync.dispatch.done eventId={evt.EventId} type={evt.Type} action=dice.refresh"); break;
            case "fate.settings.updated": ClientLogService.Instance.Warn($"sync.dispatch.deferred eventId={evt.EventId} type={evt.Type} reason=player_visible_only"); break;
            case "definitions.updated": _vm.SetDefinitionsDirty(evt.Revision); ClientLogService.Instance.Info($"sync.dispatch.done eventId={evt.EventId} type={evt.Type} action=definitions.dirty"); break;
            default: ClientLogService.Instance.Warn($"sync.event.unhandled type={evt.Type} scope={evt.Scope} revision={evt.Revision}"); break;
        }
        return System.Threading.Tasks.Task.CompletedTask;
    }
}

public sealed class ClientSyncEvent { public string EventId=""; public long Revision; public string Type=""; public string Scope=""; public static ClientSyncEvent FromMap(Dictionary<string,object>? map){ map ??= new Dictionary<string,object>(); return new ClientSyncEvent{ EventId=map.ContainsKey("eventId")?Convert.ToString(map["eventId"])??"":"", Revision=map.ContainsKey("revision")?Convert.ToInt64(map["revision"]):0, Type=map.ContainsKey("type")?Convert.ToString(map["type"])??"":"", Scope=map.ContainsKey("scope")?Convert.ToString(map["scope"])??"":""}; } }


