using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Threading;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.AdminClient.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public static class DevelopmentLayoutVisualRules
{
    public const double WorkspaceWidth = 12000;
    public const double WorkspaceHeight = 12000;
    public const double DefaultNodeWidth = 172;
    public const double DefaultNodeHeight = 92;
    public const double RootNodeWidth = 224;
    public const double RootNodeHeight = 116;
    public const double AnchorNodeWidth = 198;
    public const double AnchorNodeHeight = 102;
    public const double CompactNodeWidth = 154;
    public const double CompactNodeHeight = 78;

    public static bool IsDiagnosticNode(params string[] values)
    {
        var text = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Contains("perf_")
            || text.Contains("performance")
            || text.Contains("gui acceptance token")
            || text.Contains("test node")
            || text.Contains("diagnostic")
            || text.Contains("dev_hex_hidden_node_0152")
            || text.Contains("dev_hex_missing_0152")
            || text.Contains("dev_missing_requirement")
            || text.Contains("0153");
    }

    public static void ApplyNodeSize(DevelopmentHexagonEditorNodeVm node)
    {
        if (node == null) return;
        var type = (node.NodeTypeLabel ?? string.Empty).Trim().ToLowerInvariant();
        var id = (node.NodeId ?? string.Empty).Trim().ToLowerInvariant();
        var title = (node.Title ?? string.Empty).Trim().ToLowerInvariant();
        var isRoot = id == "novice" || id == "magic_awakened" || title.Contains("root") || title.Contains("пробуждение");
        var isAnchor = type == "class" || type == "magic" || node.RingOrLayer <= 1;
        if (isRoot)
        {
            node.NodeWidth = RootNodeWidth;
            node.NodeHeight = RootNodeHeight;
        }
        else if (isAnchor)
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

public class RowVm : ViewModelBase
{
    private int _rank;
    private int _manualBonus;
    private string _trainingState = string.Empty;
    private bool _isPlayerVisible = true;
    private string _notes = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string DisplayId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Extra { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Attribute { get; set; } = string.Empty;
    public int TotalBonus { get; set; }
    public string Breakdown { get; set; } = string.Empty;
    public int Rank { get => _rank; set { _rank = value; Notify(); } }
    public int ManualBonus { get => _manualBonus; set { _manualBonus = value; Notify(); } }
    public string TrainingState { get => _trainingState; set { _trainingState = value; Notify(); } }
    public bool IsPlayerVisible { get => _isPlayerVisible; set { _isPlayerVisible = value; Notify(); } }
    public string Notes { get => _notes; set { _notes = value; Notify(); } }
}

public sealed class DevelopmentHexagonEditorTreeVm
{
    public string HexagonId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HexagonType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int NodeCount { get; set; }
    public string Summary => $"{DevelopmentGraphDisplay.ToReadableText(Name)} · {NodeCount} узл.";
}

public static class DevelopmentGraphDisplay
{
    private static readonly Dictionary<string, string> KnownTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DEV_HEX_NODE_0152_A"] = "Служебный узел проверки 0.15.2 A",
        ["DEV_HEX_NODE_0152_B"] = "Служебный узел проверки 0.15.2 B",
        ["DEV_HEX_HIDDEN_NODE_0152"] = "Скрытый служебный узел проверки",
        ["DEV_HEX_MISSING_0152"] = "Недостающее служебное требование",
        ["dev_missing_requirement_01446"] = "Недостающее служебное требование",
        ["dev_hex_purchase_node_01446"] = "Узел покупки развития",
        ["dev_hex_node_01445_1"] = "Направление развития 1",
        ["dev_hex_node_01445_2"] = "Направление развития 2",
        ["dev_hex_branch_01445"] = "Ветка развития",
        ["dev_hex_layout_branch_01447"] = "Ветка раскладки развития",
        ["dev_hex_01446_branch"] = "Ветка развития",
        ["dev_locked_node_01446"] = "Закрепленный узел развития",
        ["visible_by_default"] = "Видно игрокам",
        ["hidden_until_gm_reveal"] = "Скрыто до раскрытия GM",
        ["gm_only"] = "Только GM",
        ["public"] = "Открыто",
        ["xp_coin"] = "монеты опыта",
        ["gold_coin"] = "золотая монета",
        ["silver_coin"] = "серебряная монета",
        ["bronze_coin"] = "бронзовая монета",
        ["iron_coin"] = "железная монета",
        ["platinum_coin"] = "платиновая монета",
        ["main_development_hexagon"] = "Основной шестиугольник развития",
        ["magic_development_hexagon"] = "Магический шестиугольник развития",
        ["large_development_hexagon_0154"] = "Большое тестовое дерево развития 0.15.4",
        ["large0154_root"] = "Корень большого тестового дерева",
        ["development_hexagon"] = "Шестиугольник развития"
    };

    public static string ToReadableNodeTitle(string title, string nodeId)
    {
        var raw = FirstNonEmpty(title, nodeId, "Узел развития");
        var cleaned = ToReadableText(raw);
        return IsTechnicalToken(cleaned) ? "Служебный узел" : cleaned;
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
        text = Regex.Replace(text, @"PLAYER_VISIBLE_AUDIO[\w\.-]*", "трек, видимый игрокам", RegexOptions.IgnoreCase);
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
            "layout" => "Раскладка",
            "archived" => "В архиве",
            "hidden" => "Скрыто",
            "start" => "Старт",
            "" => "Статус не указан",
            _ => ToReadableText(value)
        };
    }

    public static string ToReadableCurrency(string currencyId)
    {
        return (currencyId ?? string.Empty).Trim().ToLowerInvariant() switch
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
    }

    public static string ToCurrencyId(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text)) return CharacterCurrencyIds.XpCoin;
        return text.ToLowerInvariant() switch
        {
            "xp_coin" or "mo" or "мо" or "монета опыта" or "монеты опыта" => CharacterCurrencyIds.XpCoin,
            "gold_coin" or "золотая монета" or "золотые монеты" => "gold_coin",
            "silver_coin" or "серебряная монета" or "серебряные монеты" => "silver_coin",
            "bronze_coin" or "бронзовая монета" or "бронзовые монеты" => "bronze_coin",
            "iron_coin" or "железная монета" or "железные монеты" => "iron_coin",
            "platinum_coin" or "платиновая монета" or "платиновые монеты" => "platinum_coin",
            _ => text
        };
    }

    public static bool IsTechnicalToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("dev_", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("dev hex", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("PLAYER_VISIBLE_", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("development_hexagon", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("visible_by_default", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("xp_coin", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
public sealed class DevelopmentHexagonEditorNodeVm : ViewModelBase
{
    private double _positionX;
    private double _positionY;
    private bool _isSelected;
    private bool _isSearchMatch;
    private bool _isFilteredOut;
    private bool _isInvalid;
    private bool _hasWarning;
    private bool _isFocusNeighbor;
    private bool _isFocusDimmed;
    private string _validationMessage = string.Empty;

    public string NodeId { get; set; } = string.Empty;
    public string HexagonId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string NodeTypeLabel { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string LinkedDefinitionKind { get; set; } = string.Empty;
    public string LinkedDefinitionId { get; set; } = string.Empty;
    public string CostText { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsHidden { get; set; }
    public int LayoutVersion { get; set; }
    public int LayoutLayer { get; set; }
    public double OriginalPositionX { get; set; }
    public double OriginalPositionY { get; set; }
    public double NodeWidth { get; set; } = DevelopmentLayoutVisualRules.DefaultNodeWidth;
    public double NodeHeight { get; set; } = DevelopmentLayoutVisualRules.DefaultNodeHeight;

    public double PositionX
    {
        get => _positionX;
        set
        {
            if (Math.Abs(_positionX - value) < 0.01) return;
            _positionX = value;
            Notify();
            Notify(nameof(PositionText));
            Notify(nameof(IsChanged));
        }
    }

    public double PositionY
    {
        get => _positionY;
        set
        {
            if (Math.Abs(_positionY - value) < 0.01) return;
            _positionY = value;
            Notify();
            Notify(nameof(PositionText));
            Notify(nameof(IsChanged));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Notify();
        }
    }

    public bool IsChanged => Math.Abs(PositionX - OriginalPositionX) >= 0.01 || Math.Abs(PositionY - OriginalPositionY) >= 0.01;
    public int RingOrLayer => LayoutLayer;
    public bool IsDiagnosticNode => IsHidden
        || !IsPlayerVisible
        || DevelopmentLayoutVisualRules.IsDiagnosticNode(NodeId, Title, NodeTypeLabel, State, Direction, Branch, LinkedDefinitionId);
    public string DisplayTitle => DevelopmentGraphDisplay.ToReadableNodeTitle(Title, NodeId);
    public string PositionText => $"X:{Math.Round(PositionX)} Y:{Math.Round(PositionY)}";
    public string VisibilityText => IsHidden ? "Только GM" : IsPlayerVisible ? "Видно игрокам" : "Скрыто";
    public string DisplayTypeLabel => DevelopmentGraphDisplay.ToReadableType(NodeTypeLabel);
    public string DisplayState => DevelopmentGraphDisplay.ToReadableState(State);
    public string DisplayCostText => DevelopmentGraphDisplay.ToReadableText(CostText);
    public string SearchText => string.Join(" ", new[]
    {
        NodeId,
        Title,
        DisplayTitle,
        NodeTypeLabel,
        DisplayTypeLabel,
        State,
        DisplayState,
        Direction,
        Branch,
        LinkedDefinitionKind,
        LinkedDefinitionId
    }).Trim();
    public string LinkedDefinitionText => string.IsNullOrWhiteSpace(LinkedDefinitionId)
        ? "Без привязки"
        : string.IsNullOrWhiteSpace(LinkedDefinitionKind)
            ? DevelopmentGraphDisplay.ToReadableText(LinkedDefinitionId)
            : $"{DevelopmentGraphDisplay.ToReadableType(LinkedDefinitionKind)}: {DevelopmentGraphDisplay.ToReadableText(LinkedDefinitionId)}";
    public string StateBadgeText => DisplayState;
    public string FilterBadgeText => IsFilteredOut ? "Скрыто фильтрами" : "В обычной области";
    public string SearchBadgeText => IsSearchMatch ? "Найдено" : string.Empty;
    public string ValidationBadgeText => IsInvalid ? "Ошибка" : HasWarning ? "Предупреждение" : string.Empty;
    public double VisualOpacity => IsFilteredOut ? 0.24 : IsFocusDimmed ? 0.42 : 1.0;
    public System.Windows.Visibility VisualVisibility => IsFilteredOut ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

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
            Notify(nameof(FilterBadgeText));
            Notify(nameof(VisualOpacity));
            Notify(nameof(VisualVisibility));
        }
    }

    public bool IsFocusNeighbor
    {
        get => _isFocusNeighbor;
        set
        {
            if (_isFocusNeighbor == value) return;
            _isFocusNeighbor = value;
            Notify();
        }
    }

    public bool IsFocusDimmed
    {
        get => _isFocusDimmed;
        set
        {
            if (_isFocusDimmed == value) return;
            _isFocusDimmed = value;
            Notify();
            Notify(nameof(VisualOpacity));
        }
    }

    public bool IsInvalid
    {
        get => _isInvalid;
        set
        {
            if (_isInvalid == value) return;
            _isInvalid = value;
            Notify();
            Notify(nameof(ValidationBadgeText));
        }
    }

    public bool HasWarning
    {
        get => _hasWarning;
        set
        {
            if (_hasWarning == value) return;
            _hasWarning = value;
            Notify();
            Notify(nameof(ValidationBadgeText));
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set
        {
            _validationMessage = value ?? string.Empty;
            Notify();
        }
    }

    public string AutomationId => string.IsNullOrWhiteSpace(NodeId) ? "AdminDevelopmentHexagonEditor_Node_Empty" : "AdminDevelopmentHexagonEditor_Node_" + NodeId;
}
public sealed class DevelopmentHexagonEditorLinkVm : ViewModelBase
{
    private bool _isSelected;
    private bool _isInvalid;
    private bool _isFocusRelevant = true;
    private bool _isFilteredOut;
    private double _x1;
    private double _y1;
    private double _x2;
    private double _y2;
    public string LinkId { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string SourceTitle { get; set; } = string.Empty;
    public string TargetTitle { get; set; } = string.Empty;
    public string LinkType { get; set; } = "requirement";
    public double X1 { get => _x1; set { if (Math.Abs(_x1 - value) < 0.01) return; _x1 = value; Notify(); Notify(nameof(ArrowPoints)); Notify(nameof(PathData)); } }
    public double Y1 { get => _y1; set { if (Math.Abs(_y1 - value) < 0.01) return; _y1 = value; Notify(); Notify(nameof(ArrowPoints)); Notify(nameof(PathData)); } }
    public double X2 { get => _x2; set { if (Math.Abs(_x2 - value) < 0.01) return; _x2 = value; Notify(); Notify(nameof(ArrowPoints)); Notify(nameof(PathData)); } }
    public double Y2 { get => _y2; set { if (Math.Abs(_y2 - value) < 0.01) return; _y2 = value; Notify(); Notify(nameof(ArrowPoints)); Notify(nameof(PathData)); } }
    public string SourceDisplay => DevelopmentGraphDisplay.ToReadableNodeTitle(SourceTitle, SourceNodeId);
    public string TargetDisplay => DevelopmentGraphDisplay.ToReadableNodeTitle(TargetTitle, TargetNodeId);
    public string Label => $"{SourceDisplay} → {TargetDisplay}";
    public string DirectionText => $"{SourceDisplay} требуется для {TargetDisplay}";
    public string ArrowPoints => BuildArrowPoints(X1, Y1, X2, Y2);
    public string PathData => BuildPathData(X1, Y1, X2, Y2);
    public double VisualOpacity => IsFilteredOut ? 0.0 : IsFocusRelevant ? 0.82 : 0.08;
    public double VisualStrokeThickness => IsFilteredOut ? 0.0 : IsInvalid ? 5.2 : IsSelected ? 5.0 : IsFocusRelevant ? 3.0 : 0.9;
    public string VisualStrokeBrush => IsInvalid ? "#FFFF6B6B" : IsSelected ? "#FFFFD166" : IsFocusRelevant ? "#FF8BC5FF" : "#FF4B5870";
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Notify();
            Notify(nameof(VisualStrokeThickness));
            Notify(nameof(VisualStrokeBrush));
        }
    }
    public bool IsInvalid
    {
        get => _isInvalid;
        set
        {
            if (_isInvalid == value) return;
            _isInvalid = value;
            Notify();
            Notify(nameof(VisualStrokeThickness));
            Notify(nameof(VisualStrokeBrush));
        }
    }
    public bool IsFocusRelevant
    {
        get => _isFocusRelevant;
        set
        {
            if (_isFocusRelevant == value) return;
            _isFocusRelevant = value;
            Notify();
            Notify(nameof(VisualOpacity));
            Notify(nameof(VisualStrokeThickness));
            Notify(nameof(VisualStrokeBrush));
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
            Notify(nameof(VisualStrokeThickness));
        }
    }
    public string AutomationId => string.IsNullOrWhiteSpace(LinkId) ? "AdminDevelopmentHexagonEditor_Link_Empty" : "AdminDevelopmentHexagonEditor_Link_" + LinkId.Replace("->", "_to_");

    private static string BuildPathData(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var curve = Math.Max(80, Math.Min(360, Math.Sqrt(dx * dx + dy * dy) * 0.22));
        var tangentX = Math.Abs(dx) >= Math.Abs(dy) ? curve : 0;
        var tangentY = Math.Abs(dx) >= Math.Abs(dy) ? 0 : curve;
        var c1x = x1 + Math.Sign(dx == 0 ? 1 : dx) * tangentX;
        var c1y = y1 + Math.Sign(dy == 0 ? 1 : dy) * tangentY;
        var c2x = x2 - Math.Sign(dx == 0 ? 1 : dx) * tangentX;
        var c2y = y2 - Math.Sign(dy == 0 ? 1 : dy) * tangentY;
        return string.Format(CultureInfo.InvariantCulture, "M {0:F1},{1:F1} C {2:F1},{3:F1} {4:F1},{5:F1} {6:F1},{7:F1}", x1, y1, c1x, c1y, c2x, c2y, x2, y2);
    }

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

public sealed class DevelopmentCanonicalRootVm
{
    public string Label { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double ViewportX { get; set; }
    public double ViewportY { get; set; }
    public double Width { get; set; } = 260;
    public double Height { get; set; } = 150;
    public string AutomationId { get; set; } = "AdminDevelopmentHexagonEditor_CenterRootHexagon";
    public string HexPoints => BuildHexPoints(Width, Height);

    internal static string BuildHexPoints(double width, double height)
    {
        var shoulder = Math.Max(18, width * 0.24);
        return string.Format(CultureInfo.InvariantCulture,
            "{0:F1},0 {1:F1},0 {2:F1},{3:F1} {1:F1},{4:F1} {0:F1},{4:F1} 0,{3:F1}",
            shoulder, width - shoulder, width, height / 2.0, height);
    }
}

public sealed class DevelopmentCanonicalDirectionVm : ViewModelBase
{
    private bool _isFocused;
    public string DirectionId { get; set; } = string.Empty;
    public int SideIndex { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AtmosphericName { get; set; } = string.Empty;
    public string FullDisplayName => string.IsNullOrWhiteSpace(AtmosphericName) ? DisplayName : $"{DisplayName} — {AtmosphericName}";
    public double AnchorX { get; set; }
    public double AnchorY { get; set; }
    public double ViewportAnchorX { get; set; }
    public double ViewportAnchorY { get; set; }
    public double LabelX { get; set; }
    public double LabelY { get; set; }
    public double AnchorWidth { get; set; } = 190;
    public double AnchorHeight { get; set; } = 86;
    public string AnchorHexPoints => DevelopmentCanonicalRootVm.BuildHexPoints(AnchorWidth, AnchorHeight);
    public string AdminAnchorAutomationId => $"AdminDevelopmentHexagonEditor_DirectionAnchor_{SideIndex}";
    public string PlayerAnchorAutomationId => $"PlayerDevelopmentHexagonViewer_DirectionAnchor_{SideIndex}";
    public double VisualOpacity => IsFocused ? 1.0 : 0.82;
    public bool IsFocused { get => _isFocused; set { if (_isFocused == value) return; _isFocused = value; Notify(); Notify(nameof(VisualOpacity)); } }
}

public sealed class DevelopmentCanonicalLaneVm
{
    public string DirectionId { get; set; } = string.Empty;
    public int SideIndex { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double ViewportX1 { get; set; }
    public double ViewportY1 { get; set; }
    public double ViewportX2 { get; set; }
    public double ViewportY2 { get; set; }
    public string AutomationId => $"AdminDevelopmentHexagonEditor_CanonicalLane_{SideIndex}";
    public string StrokeBrush { get; set; } = "#668BC5FF";
    public double StrokeThickness { get; set; } = 5;
    public double Opacity { get; set; } = 0.72;
}

public sealed class DevelopmentLayoutMoveEdit
{
    public string NodeId { get; set; } = string.Empty;
    public double FromX { get; set; }
    public double FromY { get; set; }
    public double ToX { get; set; }
    public double ToY { get; set; }
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
    public string Score { get; set; } = string.Empty;
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? EntityId : Title;
    public string DisplayType => $"{Category} / {EntityType}";
    public string RouteSummary => $"{RouteKey} :: {EntityId}";
}

public sealed class AttributeEditorRowVm : ViewModelBase
{
    private int _value;
    public string AttributeId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinValue { get; set; }
    public int MaxValue { get; set; } = 30;
    public int DefaultValue { get; set; } = 10;
    public int SortOrder { get; set; }
    public string AttributeSetId { get; set; } = string.Empty;
    public string SourceRuleSetId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsEditableByGM { get; set; } = true;
    public string AutomationScope { get; set; } = "Attribute";
    public int OriginalValue { get; set; }
    public Action<AttributeEditorRowVm, int, int>? OnValueChanged { get; set; }
    public ObservableCollection<SubAttributeEditorRowVm> SubAttributes { get; } = new ObservableCollection<SubAttributeEditorRowVm>();

    public int Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            var old = _value;
            _value = value;
            Notify();
            Notify(nameof(IsChanged));
            OnValueChanged?.Invoke(this, old, value);
        }
    }

    public bool IsChanged => Value != OriginalValue;
    public string RangeText => $"{MinValue}..{MaxValue}";
    public string AutomationId => $"AdminCharacterEditor_{AutomationScope}_{NormalizeAutomationCode(Code)}_Value";

    public static string NormalizeAutomationCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (string.Equals(normalized, "intellect", StringComparison.OrdinalIgnoreCase)) normalized = "intelligence";
        normalized = Regex.Replace(normalized, @"[^a-z0-9_]+", "_");
        normalized = Regex.Replace(normalized, @"_+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }
}

public sealed class SubAttributeEditorRowVm : ViewModelBase
{
    private int _value;
    private int _manualBonus;
    public string SubAttributeId { get; set; } = string.Empty;
    public string ParentAttributeId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinValue { get; set; }
    public int MaxValue { get; set; } = 30;
    public int DefaultValue { get; set; }
    public int SortOrder { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsEditableByGM { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
    public int OriginalValue { get; set; }

    public int Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            Notify();
            Notify(nameof(IsChanged));
        }
    }

    public int ManualBonus
    {
        get => _manualBonus;
        set
        {
            if (_manualBonus == value) return;
            _manualBonus = value;
            Notify();
        }
    }

    public bool IsChanged => Value != OriginalValue;
    public string RangeText => $"{MinValue}..{MaxValue}";
    public string VisibilityText => IsPlayerVisible ? "Скрыто" : "Скрыто";
    public string AutomationId => $"AdminCharacterEditor_SubAttribute_{AttributeEditorRowVm.NormalizeAutomationCode(Code)}_Value";
}

public sealed class CurrencyEditorRowVm : ViewModelBase
{
    private long _amount;
    public string CurrencyId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Kind { get; set; } = "money";
    public long MinValue { get; set; }
    public long? MaxValue { get; set; }
    public long DefaultValue { get; set; }
    public int SortOrder { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string SourceRuleSetId { get; set; } = string.Empty;
    public string SourceCurrencySetId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsEditableByGM { get; set; } = true;
    public long OriginalAmount { get; set; }
    public Action<CurrencyEditorRowVm, long, long>? OnAmountChanged { get; set; }

    public long Amount
    {
        get => _amount;
        set
        {
            if (_amount == value) return;
            var old = _amount;
            _amount = value;
            Notify();
            Notify(nameof(IsChanged));
            OnAmountChanged?.Invoke(this, old, value);
        }
    }

    public bool IsChanged => Amount != OriginalAmount;
    public bool IsExperience => string.Equals(Kind, "experience", StringComparison.OrdinalIgnoreCase)
        || string.Equals(CurrencyId, CharacterCurrencyIds.XpCoin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(Code, "xp_coin", StringComparison.OrdinalIgnoreCase);
    public string VisibilityText => IsPlayerVisible ? "Скрыто" : "Скрыто";
    public string RangeText => MaxValue.HasValue ? $"{MinValue}..{MaxValue.Value}" : $">= {MinValue}";
    public string AutomationId => $"AdminCharacterEditor_Currency_{NormalizeAutomationCode(Code)}_Amount";

    public static string NormalizeAutomationCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (string.Equals(normalized, "xpcoins", StringComparison.OrdinalIgnoreCase)) normalized = "xp_coin";
        normalized = Regex.Replace(normalized, @"[^a-z0-9_]+", "_");
        normalized = Regex.Replace(normalized, @"_+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }
}

public class InventoryItemEditorVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Без названия" : Name;
    public string ItemDefinitionId { get; set; } = string.Empty;
    public string DefinitionCategory { get; set; } = string.Empty;
    public string DefinitionCode { get; set; } = string.Empty;
    public string SnapshotDisplayName { get; set; } = string.Empty;
    public string SnapshotCategory { get; set; } = string.Empty;
    public string SnapshotDescription { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int? DurabilityOrHealth { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int? Ammo { get; set; }
    public bool IsEquipped { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public bool UsesAmmoOrConsumable { get; set; }
    public int? ConsumptionPerUse { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string ListLabel => $"Без названия";
    public string SlotDisplay => string.IsNullOrWhiteSpace(Slot) ? "-" : Slot;
    public string CategoryDisplay => string.IsNullOrWhiteSpace(Category) ? "-" : Category;
    public string DefinitionDisplay => string.IsNullOrWhiteSpace(ItemDefinitionId) ? "Без справочника" : $"Без справочника";
}
public class HoldingEditorVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public List<string> Owners { get; set; } = new List<string>();
    public string OwnersDisplay => Owners.Count == 0 ? "—" : string.Join(", ", Owners);
    public string Preview => $"{Name} | {Type} | {(IsArchived ? "Archived" : "Active")} | {FirstNonEmpty(Description, Notes)}";
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public class ReputationEditorVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string ScopeType { get; set; } = "Character";
    public string TargetType { get; set; } = "Other";
    public string TargetName { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public string StatusLabel => IsArchived ? "Archived" : "Active";
    public string Preview => $"{TargetName} [{TargetType}/{ScopeType}] = {Value} | {FirstNonEmpty(Notes, "—")}";
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public class CompanionEditorVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public int OwnInventoryCount { get; set; }
    public int OwnHoldingsCount { get; set; }
    public int OwnReputationCount { get; set; }
    public object[] OwnInventoryPayload { get; set; } = Array.Empty<object>();
    public object[] OwnHoldingsPayload { get; set; } = Array.Empty<object>();
    public object[] OwnReputationPayload { get; set; } = Array.Empty<object>();
    public string StatusLabel => IsArchived ? "Archived" : "Active";
    public string Preview => $"{Name} [{Type}] {StatusLabel} | {FirstNonEmpty(Description, Notes)}";
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class ChatMessageRowVm : ViewModelBase
{
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public long SortTicks { get; set; }
}

public sealed class SkillLevelEditorRowVm : ViewModelBase
{
    private int _level;
    private string _description = string.Empty;

    public int Level
    {
        get => _level;
        set { if (_level != value) { _level = value; Notify(); } }
    }

    public string Description
    {
        get => _description;
        set { if (_description != value) { _description = value; Notify(); } }
    }
}

public sealed class WorkspacePanelDescriptor : ViewModelBase
{
    private bool _isDetached;
    private bool _isVisible = true;
    private double _windowLeft = 120;
    private double _windowTop = 120;
    private double _windowWidth = 920;
    private double _windowHeight = 720;

    public WorkspacePanelDescriptor(string panelId, string title, bool canDetach)
    {
        PanelId = panelId;
        Title = title;
        CanDetach = canDetach;
    }

    public string PanelId { get; }
    public string Title { get; }
    public bool CanDetach { get; }

    public bool IsDetached
    {
        get => _isDetached;
        set
        {
            if (_isDetached != value)
            {
                _isDetached = value;
                Notify();
                Notify(nameof(PanelState));
                Notify(nameof(StateBadge));
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                Notify();
                Notify(nameof(PanelState));
                Notify(nameof(StateBadge));
            }
        }
    }

    public double WindowLeft
    {
        get => _windowLeft;
        set { if (Math.Abs(_windowLeft - value) > 0.1) { _windowLeft = value; Notify(); } }
    }

    public double WindowTop
    {
        get => _windowTop;
        set { if (Math.Abs(_windowTop - value) > 0.1) { _windowTop = value; Notify(); } }
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set { if (Math.Abs(_windowWidth - value) > 0.1) { _windowWidth = value; Notify(); } }
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set { if (Math.Abs(_windowHeight - value) > 0.1) { _windowHeight = value; Notify(); } }
    }

    public string PanelState => !IsVisible ? "Скрыта" : IsDetached ? "Скрыта" : "Скрыта";
    public string StateBadge => !IsVisible ? "Скрыта" : IsDetached ? "Скрыта" : "Скрыта";
}

public sealed class AdminNavigationItem : ViewModelBase
{
    private bool _isSelected;
    private bool _isSearchVisible = true;

    public AdminNavigationItem(
        string id,
        string title,
        string icon,
        string groupId,
        string targetViewKey,
        bool isPlaceholder,
        int sortOrder,
        string description = "",
        string badgeText = "",
        bool isEnabled = true)
    {
        Id = id;
        Title = title;
        Icon = icon;
        GroupId = groupId;
        TargetViewKey = targetViewKey;
        IsPlaceholder = isPlaceholder;
        SortOrder = sortOrder;
        Description = description;
        BadgeText = badgeText;
        IsEnabled = isEnabled;
    }

    public string Id { get; }
    public string Title { get; }
    public string Icon { get; }
    public string GroupId { get; }
    public string TargetViewKey { get; }
    public string AutomationId => "AdminNav_" + Id.Replace('.', '_').Replace('-', '_');
    public bool IsPlaceholder { get; }
    public string BadgeText { get; }
    public bool IsEnabled { get; }
    public int SortOrder { get; }
    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                Notify();
            }
        }
    }

    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set
        {
            if (_isSearchVisible != value)
            {
                _isSearchVisible = value;
                Notify();
            }
        }
    }
}

public sealed class AdminNavigationGroup
{
    public AdminNavigationGroup(string id, string title, IEnumerable<AdminNavigationItem> items)
    {
        Id = id;
        Title = title;
        Items = new ObservableCollection<AdminNavigationItem>(items.OrderBy(item => item.SortOrder));
    }

    public string Id { get; }
    public string Title { get; }
    public ObservableCollection<AdminNavigationItem> Items { get; }
}

[DataContract]
public sealed class ConnectionSettingsModel
{
    [DataMember(Order = 1)] public string ServerHost { get; set; } = "127.0.0.1";
    [DataMember(Order = 2)] public int ServerPort { get; set; } = 4600;
    [DataMember(Order = 3)] public string LastServerHost { get; set; } = "127.0.0.1";
    [DataMember(Order = 4)] public int LastServerPort { get; set; } = 4600;
}

[DataContract]
public sealed class WorkspaceLayoutModel
{
    [DataMember(Order = 1)] public List<WorkspacePanelLayoutItem> Panels { get; set; } = new List<WorkspacePanelLayoutItem>();
}

[DataContract]
public sealed class WorkspacePanelLayoutItem
{
    [DataMember(Order = 1)] public string PanelId { get; set; } = string.Empty;
    [DataMember(Order = 2)] public bool IsDetached { get; set; }
    [DataMember(Order = 3)] public bool IsVisible { get; set; } = true;
    [DataMember(Order = 4)] public double Left { get; set; }
    [DataMember(Order = 5)] public double Top { get; set; }
    [DataMember(Order = 6)] public double Width { get; set; } = 920;
    [DataMember(Order = 7)] public double Height { get; set; } = 720;
}

public class AdminMainViewModel : ViewModelBase
{
    private readonly ClientSessionState _session = new ClientSessionState();
    private readonly JsonTcpClient _client;
    private readonly CommandApi _api;
    private readonly DispatcherTimer _poller;
    private readonly EntityRevisionStore _entityRevisions = new EntityRevisionStore();
    private readonly string _appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nri.AdminClient");
    private string _connectionState = "Оффлайн";
    private string _connectionStatusDetail = "Сервер не подключён.";
    private string _sessionSummary = "Сессия не выбрана";
    private string _serverHostInput = "127.0.0.1";
    private string _serverPortInput = "4600";
    private string _lastServerHost = "127.0.0.1";
    private int _lastServerPort = 4600;
    private bool _isConnectionPopupOpen;
    private bool _isAuthPopupOpen;
    private bool _isOnline;
    private bool _isConnectedToServer;
    private bool _isAuthenticated;
    private string _lastErrorMessage = string.Empty;
    private string _lastStatusMessage = "Ожидание подключения";
    private string _statusMessage = string.Empty;
    private int _locksCount;
    private bool _isBusy;
    private string _busyMessage = string.Empty;
    private static readonly string[] MainSectionOrder =
    {
        "admin.dashboard",
        "admin.users",
        "admin.characters",
        "system.settings",
        "session.overview",
        "session.chat",
        "event.log",
        "requests.formal",
        "proposals.review",
        "quick.notes",
        "locks.active",
        "combat.readonly",
        "definitions.browser",
        "global.search",
        "system.tools",
        "admin.items",
        "admin.classes",
        "admin.races",
        "admin.world",
        "admin.factions",
        "admin.economy",
        "admin.chronicle",
        "session.group",
        "fate.engine",
        "scene.map",
        "placeholder.combat_replay",
        "placeholder.audio"
    };

    private string _selectedSection = "admin.dashboard";
    private int _selectedSectionIndex;
    private string _selectedCharacterWorkspaceTab = "Editor";
    private string _selectedLockId = string.Empty;
    private string _selectedPendingAccountId = string.Empty;
    private string _selectedOwnerUserId = string.Empty;
    private string _selectedCharacterId = string.Empty;
    private string _selectedPendingRequestId = string.Empty;
    private string _selectedRequestDetailsId = string.Empty;
    private string _selectedCombatParticipantId = string.Empty;
    private string _selectedClassNodeId = string.Empty;
    private string _selectedSkillId = string.Empty;
    private string _characterSkillSelectedSkillIdInput = string.Empty;
    private int _characterSkillLevelInput = 1;
    private int _characterSkillManualBonusInput;
    private string _characterSkillLevelText = "0";
    private string _characterSkillManualBonusText = "0";
    private string _characterSkillTrainingStateInput = "trained";
    private bool _characterSkillIsPlayerVisibleInput = true;
    private string _skillSaveStatus = string.Empty;
    private string _selectedReferenceId = string.Empty;
    private string _selectedClassDefinitionCode = string.Empty;
    private string _selectedSkillDefinitionCode = string.Empty;
    private string _selectedRaceDefinitionCode = string.Empty;
    private string _selectedItemDefinitionCode = string.Empty;
    private string _selectedBackupId = string.Empty;
    private string _selectedDiagnosticsId = string.Empty;
    private string _editSkillCode = string.Empty;
    private string _editSkillName = string.Empty;
    private string _skillDefinitionsContentButtonsSignature = string.Empty;
    private int _selectedContentTabIndex;
    private int _selectedSystemTabIndex;
    private int _selectedSessionTabIndex;
    private string _charactersSearchText = string.Empty;
    private string _locksSearchText = string.Empty;
    private string _classSearchText = string.Empty;
    private string _skillSearchText = string.Empty;
    private string _raceSearchText = string.Empty;
    private string _itemSearchText = string.Empty;
    private string _skillCategoryFilter = string.Empty;
    private string _classBranchFilter = string.Empty;
    private string _itemTypeFilter = string.Empty;
    private string _globalSearchQuery = string.Empty;
    private string _globalSearchCategoryFilter = "all";
    private string _globalSearchStatusText = "Глобальный поиск готов.";
    private bool _globalSearchIncludeArchived;
    private bool _globalSearchIncludeHidden = true;
    private GlobalSearchResultVm? _selectedGlobalSearchResult;
    private RowVm? _selectedAudioTrackRow;
    private string _selectedNavigationItemId = string.Empty;
    private string _shellSearchText = string.Empty;
    private string _assignClassCode = string.Empty;
    private string _assignClassNodeId = string.Empty;
    private string _assignClassLevel = "1";
    private readonly Dictionary<string, Dictionary<string, object>> _classNodeLayoutPayloads = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, object>> _developmentLayoutHexagonPayloads = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
    private string _selectedDevelopmentLayoutHexagonId = DevelopmentHexagonIds.Main;
    private bool _isRefreshingDevelopmentLayoutEditor;
    private DevelopmentHexagonEditorNodeVm? _selectedDevelopmentLayoutNode;
    private string _developmentLayoutStatusText = "Визуальная раскладка не загружена.";
    private bool _developmentLayoutHasUnsavedChanges;
    private double _developmentLayoutZoom = 0.72;
    private double _developmentLayoutViewportTranslateX;
    private double _developmentLayoutViewportTranslateY;
    private bool _developmentLayoutShowGrid = true;
    private bool _developmentLayoutSnapToGrid = true;
    private bool _developmentLayoutShowLegend = true;
    private bool _developmentLayoutFocusSelectedNodeLinks = true;
    private bool _developmentLayoutShowDiagnostics;
    private bool _developmentLayoutShowDirectionLabels = true;
    private string _developmentLayoutFocusedDirectionKey = string.Empty;
    private string _developmentLayoutQualityText = "Оценка читаемости не выполнена.";
    private string _developmentLayoutSnapshotText = "Снимок раскладки не создан.";
    private bool _developmentLayoutPreviewActive;
    private string _developmentLayoutSearchText = string.Empty;
    private string _developmentLayoutTypeFilter = "all";
    private string _developmentLayoutVisibilityFilter = "all";
    private string _developmentLayoutStateFilter = "all";
    private string _developmentLayoutLinkedKindFilter = "all";
    private int _developmentLayoutSearchIndex = -1;
    private string _developmentLayoutDragNodeId = string.Empty;
    private double _developmentLayoutDragStartX;
    private double _developmentLayoutDragStartY;
    private bool _isApplyingDevelopmentLayoutHistory;
    private int _developmentLayoutRevision = 1;
    private string _nodePositionX = string.Empty;
    private string _nodePositionY = string.Empty;
    private string _nodeHexagonId = DevelopmentHexagonIds.Main;
    private string _nodeHexagonType = DevelopmentHexagonTypes.Main;
    private string _nodeName = string.Empty;
    private string _nodeDescription = string.Empty;
    private string _nodeType = DevelopmentNodeTypes.Class;
    private string _nodeRole = DevelopmentNodeRoleIds.MainBranchLevel;
    private string _nodeVisibilityRule = DevelopmentUnlockPolicyIds.VisibleByDefault;
    private string _nodeRing = string.Empty;
    private string _nodeSector = string.Empty;
    private string _nodeDirectionCode = string.Empty;
    private string _nodeBranchCode = string.Empty;
    private string _nodeSortOrder = string.Empty;
    private string _nodeRequiredNodes = string.Empty;
    private string _nodeLinkedClassId = string.Empty;
    private string _nodeLinkedDefinitionKind = string.Empty;
    private string _nodeLinkedDefinitionId = string.Empty;
    private string _nodeCost = string.Empty;
    private string _nodeCurrencyId = CharacterCurrencyIds.XpCoin;
    private string _nodePrimaryMagicGroupId = string.Empty;
    private string _nodeLayoutVersion = string.Empty;
    private string _nodeLayoutUpdatedAt = string.Empty;
    private string _requirementSourceNodeId = string.Empty;
    private string _requirementTargetNodeId = string.Empty;
    private DevelopmentHexagonEditorLinkVm? _selectedDevelopmentLayoutLink;
    private bool _developmentLinkModeEnabled;
    private string _nodeLayoutSaveStatus = "Раскладка узла не сохранена.";
    private bool _nodeIsPlayerVisible = true;
    private bool _nodeIsHidden;
    private bool _nodeIsArchived;
    private bool _nodeIsPrimaryMagicClass;
    private bool _nodeLayoutLockedManualPosition;
    private bool _isClassNodeLayoutDirty;
    private bool _isLoadingClassNodeLayoutEditor;
    private readonly Stack<DevelopmentLayoutMoveEdit> _developmentLayoutUndoStack = new Stack<DevelopmentLayoutMoveEdit>();
    private readonly Stack<DevelopmentLayoutMoveEdit> _developmentLayoutRedoStack = new Stack<DevelopmentLayoutMoveEdit>();
    private int _diceCount = 1;
    private int _diceFaces = 20;
    private int _diceModifier;
    private string _diceModeInput = "Обычный";
    private string _diceVisibilityInput = "Public";
    private string _diceDescriptionInput = "Admin quick roll";
    private string _lastDiceAvailabilityReason = string.Empty;
    private readonly IClientSyncEventDispatcher _syncDispatcher;
    private long _syncRevision;
    private bool _definitionsDirty;
    private int _strength;
    private string _characterStatsSaveStatus = "Характеристики не сохранены.";
    private string _editBackstory = string.Empty;
    private string _biographySaveStatus = "Биография не сохранена.";
    private string _ownershipOwnerUserId = string.Empty;
    private string _ownershipControlledByUserId = string.Empty;
    private string _ownershipReason = "owner/group/status acceptance 0.14.42";
    private string _ownershipGroupId = string.Empty;
    private string _ownershipGroupName = string.Empty;
    private string _ownershipKind = CharacterKindIds.PlayerCharacter;
    private string _ownershipStatus = CharacterStatusIds.Active;
    private string _ownershipMessage = "Владелец и статус не загружены.";
    private bool _ownershipIsActive = true;
    private bool _ownershipIsArchived;
    private bool _ownershipIsPlayerVisible = true;

    public AdminMainViewModel()
    {
        Directory.CreateDirectory(_appDataDirectory);

        _client = new JsonTcpClient(App.ClientConfig, _session);
        ClientLogService.Instance.Info("AdminMainViewModel initialized");
        ClientLogService.Instance.Info("chat.window.layout fixedHeaderFooter=true");
        _api = new CommandApi(_client);
        FunctionalDashboard = new AdminFunctionalDashboardViewModel(_api);
        CombatReadOnly = new AdminCombatReadOnlyViewModel(_api);
        CurrentSession = new AdminCurrentSessionViewModel(_api);
        CharacterGroups = new AdminCharacterGroupsViewModel(_api);
        FateControl = new AdminFateControlViewModel(_api);
        SceneMap = new AdminSceneMapViewModel(_api);
        WorldMap = new AdminWorldMapViewModel(_api);
        ProposalReview = new AdminProposalReviewViewModel(_api);
        ItemsEquipmentCatalog = new AdminItemsEquipmentCatalogViewModel(_api);
        Crafting = new AdminCraftingViewModel(_api);
        Engineering = new AdminEngineeringViewModel(_api);
        Production = new AdminProductionViewModel(_api);
        WorldCalendar = new AdminWorldCalendarViewModel(_api);
        RealSchedule = new AdminRealScheduleViewModel(_api);
        GMNotes = new AdminGMNotesViewModel(_api);
        EventJournal = new AdminEventJournalViewModel(_api);
        RoomInterior = new AdminRoomInteriorViewModel(_api);
        DefinitionsBrowser = new AdminDefinitionsBrowserViewModel(_api);
        SystemTools = new SystemToolsViewModel(_api);
        CharacterCard.SetLoadAction(OpenCharacter);
        CharacterCard.SetInventoryDiagnosticsAction(RunInventoryDiagnosticsForCard);

        LoginCommand = new RelayCommand(() => RunUiAction("Вход", Login));
        RefreshCommand = new RelayCommand(() => RunUiAction("  ", RefreshAll));
        OpenConnectionPopupCommand = new RelayCommand(() =>
        {
            IsAuthPopupOpen = false;
            IsConnectionPopupOpen = !IsConnectionPopupOpen;
        });
        ToggleAuthPopupCommand = new RelayCommand(() =>
        {
            IsConnectionPopupOpen = false;
            IsAuthPopupOpen = !IsAuthPopupOpen;
        });
        ConnectToServerCommand = new RelayCommand(() => RunUiAction("Подключение к серверу", ConnectToServer));
        ApplyConnectionSettingsCommand = new RelayCommand(ApplyConnectionSettings);
        ResetConnectionDefaultsCommand = new RelayCommand(ResetConnectionDefaults);
        UseSavedConnectionSettingsCommand = new RelayCommand(UseSavedConnectionSettings);
        ApproveCommand = new RelayCommand(ApproveSelected);
        ArchiveCommand = new RelayCommand(ArchiveSelected);
        RejectAccountCommand = new RelayCommand(RejectSelectedAccount);
        BlockAccountCommand = new RelayCommand(BlockSelectedAccount);
        UnblockAccountCommand = new RelayCommand(UnblockSelectedAccount);
        ChangePasswordCommand = new RelayCommand(ChangePassword);
        ResetPasswordCommand = new RelayCommand(ResetSelectedPassword);
        CreateCharacterCommand = new RelayCommand(CreateCharacterForOwner);
        RollDiceCommand = new RelayCommand(RollCharacterDice);
        LoadOwnerCharactersCommand = new RelayCommand(LoadOwnerCharacters);
        OpenCharacterCommand = new RelayCommand(OpenCharacter);
        OpenPlayerCharactersCommand = new RelayCommand(OpenPlayerCharacters);
        FocusSelectedCharacterCommand = new RelayCommand(FocusSelectedCharacter);
        FocusSelectedRequestCommand = new RelayCommand(FocusSelectedRequest);
        FocusCharacterEditorCommand = new RelayCommand(FocusCharacterEditor);
        FocusCharacterNotesCommand = new RelayCommand(FocusCharacterNotes);
        FocusCharacterVisibilityCommand = new RelayCommand(FocusCharacterVisibility);
        RefreshSelectedCharacterCommand = new RelayCommand(RefreshSelectedCharacter);
        RefreshPeopleSectionCommand = new RelayCommand(RefreshPeopleSection);
        RefreshModerationSectionCommand = new RelayCommand(RefreshModerationSection);
        RefreshSessionSectionCommand = new RelayCommand(RefreshSessionSection);
        RefreshContentSectionCommand = new RelayCommand(RefreshContentSection);
        RefreshSystemSectionCommand = new RelayCommand(RefreshSystemSection);
        AcquireLockCommand = new RelayCommand(AcquireLock);
        ReleaseLockCommand = new RelayCommand(ReleaseLock);
        ForceUnlockCommand = new RelayCommand(ForceUnlock);
        SaveBasicInfoCommand = new RelayCommand(SaveBasicInfo);
        SaveBiographyCommand = new RelayCommand(SaveBiography);
        SaveStatsCommand = new RelayCommand(SaveStats);
        SaveMoneyCommand = new RelayCommand(SaveMoney);
        SaveXpCoinsCommand = new RelayCommand(SaveXpCoins);
        InventoryReloadCommand = new RelayCommand(LoadCharacterInventory);
        InventoryAddItemCommand = new RelayCommand(AddInventoryItem);
        InventoryLoadCatalogCommand = new RelayCommand(LoadInventoryCatalogDefinitions);
        InventoryAddFromCatalogCommand = new RelayCommand(AddInventoryItemFromCatalog);
        InventoryUpdateItemCommand = new RelayCommand(UpdateInventoryItem);
        InventoryRemoveItemCommand = new RelayCommand(RemoveInventoryItem);
        InventoryToggleEquipCommand = new RelayCommand(ToggleInventoryItemEquip);
        HoldingsReloadCommand = new RelayCommand(LoadCharacterHoldings);
        HoldingAddCommand = new RelayCommand(AddHolding);
        HoldingUpdateCommand = new RelayCommand(UpdateHolding);
        HoldingRemoveCommand = new RelayCommand(RemoveHolding);
        ReputationReloadCommand = new RelayCommand(LoadCharacterReputation);
        ReputationAddCommand = new RelayCommand(AddReputationEntry);
        ReputationUpdateCommand = new RelayCommand(UpdateReputationEntry);
        ReputationRemoveCommand = new RelayCommand(RemoveReputationEntry);
        CompanionsReloadCommand = new RelayCommand(LoadCharacterCompanions);
        CompanionAddCommand = new RelayCommand(AddCompanion);
        CompanionUpdateCommand = new RelayCommand(UpdateCompanion);
        CompanionRemoveCommand = new RelayCommand(RemoveCompanion);
        OwnershipSaveOwnerCommand = new RelayCommand(SaveOwnershipOwner);
        OwnershipSaveKindStatusCommand = new RelayCommand(SaveOwnershipKindStatus);
        OwnershipAssignGroupCommand = new RelayCommand(AssignOwnershipGroup);
        OwnershipArchiveCommand = new RelayCommand(() => SetOwnershipArchived(true));
        OwnershipUnarchiveCommand = new RelayCommand(() => SetOwnershipArchived(false));
        ApproveRequestCommand = new RelayCommand(ApproveRequest);
        RejectRequestCommand = new RelayCommand(RejectRequest);
        MarkInReviewRequestCommand = new RelayCommand(MarkInReviewRequest);
        RequestChangesCommand = new RelayCommand(RequestChangesForSelectedRequest);
        ArchiveRequestCommand = new RelayCommand(ArchiveSelectedRequest);
        RefreshRequestsCommand = new RelayCommand(LoadPendingRequests);
        CombatStartCommand = new RelayCommand(() => RunUiAction("Начать бой", CombatStart));
        CombatEndCommand = new RelayCommand(() => RunUiAction("Завершить бой", CombatEnd));
        CombatRefreshCommand = new RelayCommand(() => RunUiAction(" ", CombatRefresh));
        CombatNextTurnCommand = new RelayCommand(() => RunUiAction("Следующий ход боя", CombatNextTurn));
        CombatPrevTurnCommand = new RelayCommand(() => RunUiAction("Предыдущий ход боя", CombatPrevTurn));
        CombatNextRoundCommand = new RelayCommand(() => RunUiAction("Следующий раунд боя", CombatNextRound));
        CombatSkipTurnCommand = new RelayCommand(() => RunUiAction("Пропустить ход", CombatSkipTurn));
        CombatAddParticipantCommand = new RelayCommand(() => RunUiAction("Добавить участника боя", CombatAddParticipant));
        CombatRemoveParticipantCommand = new RelayCommand(() => RunUiAction("Удалить участника боя", CombatRemoveParticipant));
        CombatDetachCompanionCommand = new RelayCommand(() => RunUiAction("Отвязать компаньона", CombatDetachCompanion));
        DefinitionsReloadCommand = new RelayCommand(() => RunUiAction("Обновить справочники", DefinitionsReload));
        RefreshDefinitionClassesCommand = new RelayCommand(() => RunUiAction("Обновить классы", RefreshDefinitionClasses));
        NewClassDefinitionCommand = new RelayCommand(NewClassDefinition);
        OpenSelectedClassDefinitionCommand = new RelayCommand(() => RunUiAction("Открыть класс", OpenSelectedClassDefinition));
        SaveClassDefinitionCommand = new RelayCommand(() => RunUiAction("Сохранить класс", SaveClassDefinition));
        ArchiveClassDefinitionCommand = new RelayCommand(() => RunUiAction("Архивировать класс", ArchiveClassDefinition));
        RefreshDefinitionSkillsCommand = new RelayCommand(() => RunUiAction("Обновить навыки", RefreshDefinitionSkills));
        RefreshDefinitionRacesCommand = new RelayCommand(() => RunUiAction("Обновить расы", RefreshDefinitionRaces));
        RefreshDefinitionItemsCommand = new RelayCommand(() => RunUiAction("Обновить предметы", RefreshDefinitionItems));
        RefreshContentStatusCommand = new RelayCommand(() => RunUiAction("Обновить статус контента", RefreshDefinitionsContentStatus));
        AssignCharacterClassCommand = new RelayCommand(() => RunUiAction("Назначить класс", AssignCharacterClass));
        NewSkillDefinitionCommand = new RelayCommand(() => RunUiAction("Новый навык", NewSkillDefinition));
        OpenSelectedSkillDefinitionCommand = new RelayCommand(() => RunUiAction("Открыть навык", OpenSelectedSkillDefinition));
        SaveSkillDefinitionCommand = new RelayCommand(() => RunUiAction("Сохранить навык", SaveSkillDefinition));
        ArchiveSkillDefinitionCommand = new RelayCommand(() => RunUiAction("Архивировать навык", ArchiveSkillDefinition));
        AddSkillLevelCommand = new RelayCommand(AddSkillLevel);
        RemoveSkillLevelCommand = new RelayCommand(RemoveSkillLevel);
        LoadClassTreeCommand = new RelayCommand(() => RunUiAction("Загрузить дерево классов", LoadClassTree));
        SelectDevelopmentLayoutHexagonCommand = new RelayCommand<string>(hexagonId => RunUiAction("Выбрать шестиугольник развития", () => SelectDevelopmentLayoutHexagon(hexagonId)));
        AcquireClassNodeCommand = new RelayCommand(() => RunUiAction("Разблокировать узел развития", AcquireClassNode));
        RevokeClassNodeCommand = new RelayCommand(() => RunUiAction("Заблокировать узел развития", RevokeClassNode));
        SelectClassNodeCommand = new RelayCommand<string>(nodeId => RunUiAction("Выполнить действие", () => { SelectedClassNodeId = nodeId ?? string.Empty; }));
        SaveClassNodeLayoutCommand = new RelayCommand(() => RunUiAction("Сохранить раскладку узла", SaveClassNodeLayout));
        SaveDevelopmentHexagonLayoutCommand = new RelayCommand(() => RunUiAction("Сохранить визуальную раскладку", SaveDevelopmentHexagonLayout));
        CancelDevelopmentHexagonLayoutCommand = new RelayCommand(() => RunUiAction("Отменить изменения раскладки", CancelDevelopmentHexagonLayout));
        ResetDevelopmentHexagonLayoutCommand = new RelayCommand(() => RunUiAction("Сбросить раскладку шестиугольника", ResetDevelopmentHexagonLayout));
        ValidateDevelopmentHexagonLayoutCommand = new RelayCommand(() => RunUiAction("Проверить раскладку шестиугольника", ValidateDevelopmentHexagonLayout));
        PreviewBaselineDevelopmentHexagonLayoutCommand = new RelayCommand(() => RunUiAction("Предпросмотр базовой раскладки", PreviewBaselineDevelopmentHexagonLayout));
        ApplyBaselineDevelopmentHexagonLayoutCommand = new RelayCommand(() => RunUiAction("Применить базовую раскладку", ApplyBaselineDevelopmentHexagonLayout));
        CreateDevelopmentLayoutSnapshotCommand = new RelayCommand(() => RunUiAction("Создать снимок раскладки", CreateDevelopmentLayoutSnapshot));
        RestoreDevelopmentLayoutSnapshotCommand = new RelayCommand(() => RunUiAction("Восстановить снимок раскладки", RestoreDevelopmentLayoutSnapshot));
        GetDevelopmentLayoutQualityReportCommand = new RelayCommand(() => RunUiAction("Оценить читаемость раскладки", GetDevelopmentLayoutQualityReport));
        LockSelectedDevelopmentLayoutNodeCommand = new RelayCommand(() => RunUiAction("Зафиксировать позицию узла", () => SetSelectedDevelopmentLayoutNodeLock(true)));
        UnlockSelectedDevelopmentLayoutNodeCommand = new RelayCommand(() => RunUiAction("Снять фиксацию позиции узла", () => SetSelectedDevelopmentLayoutNodeLock(false)));
        CreateDevelopmentNodeCommand = new RelayCommand(() => RunUiAction("Создать узел развития", CreateDevelopmentNode));
        ArchiveDevelopmentNodeCommand = new RelayCommand(() => RunUiAction("Архивировать узел развития", ArchiveDevelopmentNode));
        RestoreDevelopmentNodeCommand = new RelayCommand(() => RunUiAction("Вернуть узел развития", RestoreDevelopmentNode));
        SaveDevelopmentNodeCommand = new RelayCommand(() => RunUiAction("Сохранить узел развития", SaveClassNodeLayout));
        CancelDevelopmentNodeEditCommand = new RelayCommand(() => RunUiAction("Отменить правку узла", LoadSelectedClassNodeLayoutEditor));
        AddRequirementLinkCommand = new RelayCommand(() => RunUiAction("Добавить требование узла", AddRequirementLink));
        RemoveRequirementLinkCommand = new RelayCommand(() => RunUiAction("Удалить требование узла", RemoveRequirementLink));
        ValidateDevelopmentGraphCommand = new RelayCommand(() => RunUiAction("Проверить граф развития", ValidateDevelopmentGraph));
        ToggleDevelopmentLinkModeCommand = new RelayCommand(() => DevelopmentLinkModeEnabled = !DevelopmentLinkModeEnabled);
        ZoomInDevelopmentHexagonLayoutCommand = new RelayCommand(() => DevelopmentLayoutZoom = Math.Min(1.5, DevelopmentLayoutZoom + 0.1));
        ZoomOutDevelopmentHexagonLayoutCommand = new RelayCommand(() => DevelopmentLayoutZoom = Math.Max(0.35, DevelopmentLayoutZoom - 0.1));
        ResetViewDevelopmentHexagonLayoutCommand = new RelayCommand(() => DevelopmentLayoutZoom = 0.72);
        FitToViewDevelopmentHexagonLayoutCommand = new RelayCommand(FitToViewDevelopmentHexagonLayout);
        SearchDevelopmentHexagonLayoutClearCommand = new RelayCommand(ClearDevelopmentLayoutSearch);
        SearchDevelopmentHexagonLayoutNextCommand = new RelayCommand(() => SelectDevelopmentLayoutSearchResult(1));
        SearchDevelopmentHexagonLayoutPreviousCommand = new RelayCommand(() => SelectDevelopmentLayoutSearchResult(-1));
        ClearDevelopmentHexagonLayoutFiltersCommand = new RelayCommand(ClearDevelopmentLayoutFilters);
        UndoDevelopmentHexagonLayoutCommand = new RelayCommand(UndoDevelopmentLayoutEdit);
        RedoDevelopmentHexagonLayoutCommand = new RelayCommand(RedoDevelopmentLayoutEdit);
        SaveAllDevelopmentHexagonChangesCommand = new RelayCommand(() => RunUiAction("Сохранить все изменения графа развития", SaveAllDevelopmentHexagonChanges));
        DiscardAllDevelopmentHexagonChangesCommand = new RelayCommand(DiscardAllDevelopmentHexagonChanges);
        ShowAllDevelopmentLayoutLinksCommand = new RelayCommand(ShowAllDevelopmentLayoutLinks);
        FocusDevelopmentHexagonValidationAffectedCommand = new RelayCommand(FocusFirstDevelopmentValidationAffected);
        CompleteClassNodeCommand = new RelayCommand(() => RunUiAction("Завершить узел развития", CompleteClassNode));
        LoadSkillsCommand = new RelayCommand(() => RunUiAction("Загрузить навыки", LoadSkills));
        AcquireSkillCommand = new RelayCommand(() => RunUiAction("Выдать навык", AcquireSkill));
        UpdateSkillLevelCommand = new RelayCommand(() => RunUiAction("Обновить уровень навыка", UpdateSkillLevel));
        RemoveSkillCommand = new RelayCommand(() => RunUiAction("Удалить навык", RemoveSkill));
        ChatSendCommand = new RelayCommand(() => RunUiAction("Отправить сообщение", ChatSend));
        ChatRefreshCommand = new RelayCommand(() => RunUiAction(" ", ChatRefresh));
        ChatMuteUserCommand = new RelayCommand(ChatMuteUser);
        ChatUnmuteUserCommand = new RelayCommand(ChatUnmuteUser);
        ChatLockPlayersCommand = new RelayCommand(ChatLockPlayers);
        ChatUnlockPlayersCommand = new RelayCommand(ChatUnlockPlayers);
        ChatSetSlowModeCommand = new RelayCommand(ChatSetSlowMode);
        AudioRefreshCommand = new RelayCommand(() => RunUiAction("Обновить аудио", AudioRefresh));
        AudioSetModeCommand = new RelayCommand(() => RunUiAction("Сохранить настройки аудио", AudioSetMode));
        AudioClearOverrideCommand = new RelayCommand(() => RunUiAction("Синхронизировать аудио", AudioClearOverride));
        AudioNextTrackCommand = new RelayCommand(() => RunUiAction("Следующий трек", AudioNextTrack));
        AudioSelectTrackCommand = new RelayCommand(() => RunUiAction("Воспроизвести трек", AudioSelectTrack));
        AudioReloadLibraryCommand = new RelayCommand(() => RunUiAction("Обновить библиотеку", AudioReloadLibrary));
        AudioPauseCommand = new RelayCommand(() => RunUiAction("Пауза аудио", AudioPause));
        AudioStopCommand = new RelayCommand(() => RunUiAction("Остановить аудио", AudioStop));
        AudioResyncCommand = new RelayCommand(() => RunUiAction("Синхронизировать аудио", AudioResync));
        VisibilityLoadCommand = new RelayCommand(VisibilityLoad);
        VisibilitySaveCommand = new RelayCommand(VisibilitySave);
        NotesRefreshCommand = new RelayCommand(NotesRefresh);
        NotesCreateCommand = new RelayCommand(NotesCreate);
        NotesArchiveCommand = new RelayCommand(NotesArchive);
        ReferenceRefreshCommand = new RelayCommand(() => RunUiAction(" reference data", ReferenceRefresh));
        ReferenceCreateCommand = new RelayCommand(() => RunUiAction("Создать справочную запись", ReferenceCreate));
        ReferenceUpdateCommand = new RelayCommand(() => RunUiAction(" reference data", ReferenceUpdate));
        ReferenceArchiveCommand = new RelayCommand(() => RunUiAction("Архивировать справочную запись", ReferenceArchive));
        BackupRefreshCommand = new RelayCommand(() => RunUiAction("Список резервных копий", BackupRefresh));
        BackupCreateCommand = new RelayCommand(() => RunUiAction("Создать резервную копию", BackupCreate));
        BackupRestoreCommand = new RelayCommand(() => RunUiAction("Восстановить резервную копию", BackupRestore));
        BackupExportCommand = new RelayCommand(() => RunUiAction("Экспортировать резервную копию", BackupExport));
        DiagnosticsRefreshCommand = new RelayCommand(() => RunUiAction(" diagnostics", DiagnosticsRefresh));
        FocusContentClassesCommand = new RelayCommand(FocusContentClasses);
        FocusContentReferenceCommand = new RelayCommand(FocusContentReference);
        FocusSystemReferenceCommand = new RelayCommand(FocusSystemReference);
        FocusSystemBackupsCommand = new RelayCommand(FocusSystemBackups);
        FocusSystemDiagnosticsCommand = new RelayCommand(FocusSystemDiagnostics);
        SelectSectionCommand = new RelayCommand<string>(SelectSection);
        SelectNavigationItemCommand = new RelayCommand<AdminNavigationItem>(SelectNavigationItem);
        OpenFirstShellSearchResultCommand = new RelayCommand(OpenFirstShellSearchResult);
        GlobalSearchCommand = new RelayCommand(() => RunUiAction("Глобальный поиск", RunGlobalSearch));
        GlobalSearchOpenCommand = new RelayCommand(() => RunUiAction("Открыть результат поиска", OpenGlobalSearchResult));
        DetachWorkspacePanelCommand = new RelayCommand<string>(DetachWorkspacePanel);
        AttachWorkspacePanelCommand = new RelayCommand<string>(AttachWorkspacePanel);
        ToggleWorkspacePanelVisibilityCommand = new RelayCommand<string>(ToggleWorkspacePanelVisibility);
        ShowWorkspacePanelCommand = new RelayCommand<string>(ShowWorkspacePanel);
        HideWorkspacePanelCommand = new RelayCommand<string>(HideWorkspacePanel);

        InitializeNavigationGroups();
        InitializeWorkspacePanels();
        LoadConnectionSettings();
        LoadWorkspaceLayout();
        RefreshConnectionSummary();
        ClientLogService.Instance.Info("ui.admin.dice.panel.loaded");
        ClientLogService.Instance.Info("people.grid.template fixed=true");
        ClientLogService.Instance.Info("dice.actor.mode=account");
        TraceDiceAvailability();

        _poller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _poller.Tick += (_, _) => PollSyncAndRefresh();
        _syncDispatcher = new ClientSyncEventDispatcher(this);
    }

    public string LoginText { get; set; } = string.Empty;
    public AdminFunctionalDashboardViewModel FunctionalDashboard { get; }
    public AdminCombatReadOnlyViewModel CombatReadOnly { get; }
    public AdminCurrentSessionViewModel CurrentSession { get; }
    public AdminCharacterGroupsViewModel CharacterGroups { get; }
    public AdminFateControlViewModel FateControl { get; }
    public AdminSceneMapViewModel SceneMap { get; }
    public AdminWorldMapViewModel WorldMap { get; }
    public AdminProposalReviewViewModel ProposalReview { get; }
    public AdminItemsEquipmentCatalogViewModel ItemsEquipmentCatalog { get; }
    public AdminCraftingViewModel Crafting { get; }
    public AdminEngineeringViewModel Engineering { get; }
    public AdminProductionViewModel Production { get; }
    public AdminWorldCalendarViewModel WorldCalendar { get; }
    public AdminRealScheduleViewModel RealSchedule { get; }
    public AdminGMNotesViewModel GMNotes { get; }
    public AdminEventJournalViewModel EventJournal { get; }
    public AdminRoomInteriorViewModel RoomInterior { get; }
    public AdminDefinitionsBrowserViewModel DefinitionsBrowser { get; }
    public SystemToolsViewModel SystemTools { get; }
    public CharacterCardViewModel CharacterCard { get; } = new CharacterCardViewModel();
    public string PasswordText { get; set; } = string.Empty;
    public string OldPasswordText { get; set; } = string.Empty;
    public string NewPasswordText { get; set; } = string.Empty;
    public string ResetPasswordText { get; set; } = string.Empty;
    public string CreateCharacterName { get; set; } = string.Empty;
    public string CreateCharacterRace { get; set; } = string.Empty;
    public string CreateCharacterBackstory { get; set; } = string.Empty;

    public void SetResetPasswordTextFromUi(string value)
    {
        ResetPasswordText = value ?? string.Empty;
        Notify(nameof(ResetPasswordText));
        Notify(nameof(CanResetSelectedAccountPassword));
    }
    public int DiceCount { get => _diceCount; set { if (_diceCount != value) { _diceCount = value; Notify(); Notify(nameof(CanRollCharacterDice)); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } } }
    public int DiceFaces { get => _diceFaces; set { if (_diceFaces != value) { _diceFaces = value; Notify(); Notify(nameof(CanRollCharacterDice)); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } } }
    public int DiceModifier { get => _diceModifier; set { if (_diceModifier != value) { _diceModifier = value; Notify(); } } }
    public string DiceModeInput { get => _diceModeInput; set { if (_diceModeInput != value) { _diceModeInput = value; Notify(); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } } }
    public string DiceVisibilityInput { get => _diceVisibilityInput; set { if (_diceVisibilityInput != value) { _diceVisibilityInput = value; Notify(); } } }
    public string DiceDescriptionInput { get => _diceDescriptionInput; set { if (_diceDescriptionInput != value) { _diceDescriptionInput = value; Notify(); } } }
    public ObservableCollection<string> DiceModeOptions { get; } = new ObservableCollection<string> { "Проверочный", "Проверочный" };
    public ObservableCollection<string> DiceVisibilityOptions { get; } = new ObservableCollection<string> { "Public", "HiddenToAdmins", "AdminOnly" };
    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); } }
    public string ConnectionStatusDetail { get => _connectionStatusDetail; set { _connectionStatusDetail = value; Notify(); } }
    public string SessionSummary { get => _sessionSummary; set { _sessionSummary = value; Notify(); } }
    public bool IsOnline { get => _isOnline; set { _isOnline = value; Notify(); } }
    public bool IsConnectedToServer { get => _isConnectedToServer; set { _isConnectedToServer = value; Notify(); Notify(nameof(ConnectionStage)); Notify(nameof(LoginState)); Notify(nameof(ArePrivilegedSectionsEnabled)); Notify(nameof(SectionAccessHint)); Notify(nameof(CanRollCharacterDice)); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } }
    public bool IsAuthenticated { get => _isAuthenticated; set { _isAuthenticated = value; Notify(); Notify(nameof(ConnectionStage)); Notify(nameof(LoginState)); Notify(nameof(ArePrivilegedSectionsEnabled)); Notify(nameof(SectionAccessHint)); Notify(nameof(CanRollCharacterDice)); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } }
    public string LastErrorMessage { get => _lastErrorMessage; set { _lastErrorMessage = value; Notify(); Notify(nameof(HasConnectionError)); Notify(nameof(ConnectionStage)); } }
    public string LastStatusMessage { get => _lastStatusMessage; set { _lastStatusMessage = value; Notify(); } }
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                Notify();
            }
        }
    }
    public int LocksCount { get => _locksCount; set { _locksCount = value; Notify(); } }
    public bool HasConnectionError => !string.IsNullOrWhiteSpace(LastErrorMessage);
    public bool ArePrivilegedSectionsEnabled => IsConnectedToServer && IsAuthenticated;
    public string ConnectionStage => HasConnectionError ? "Ошибка подключения" : IsAuthenticated ? "Авторизован" : IsConnectedToServer ? "Подключён, требуется вход" : "Нет подключения";
    public string LoginState => IsAuthenticated ? $"Вошли как: {LoginSummary}" : IsConnectedToServer ? "Сервер подключён, выполните вход" : "Нет подключения";
    public string SectionAccessHint => ArePrivilegedSectionsEnabled ? "Разделы доступны" : IsConnectedToServer ? "Войдите администратором, чтобы открыть разделы" : "Подключитесь к серверу";
    public bool IsConnectionPopupOpen { get => _isConnectionPopupOpen; set { _isConnectionPopupOpen = value; Notify(); } }
    public bool IsAuthPopupOpen { get => _isAuthPopupOpen; set { _isAuthPopupOpen = value; Notify(); } }
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            Notify();
            Notify(nameof(IsIdle));
            Notify(nameof(CanManagePendingAccount));
            Notify(nameof(CanResetSelectedAccountPassword));
            Notify(nameof(CanLoadPlayerCharacters));
            Notify(nameof(CanOpenSelectedCharacter));
            Notify(nameof(CanModerateSelectedRequest));
            Notify(nameof(CanManageSelectedLock));
            Notify(nameof(CanManageSelectedCharacter));
            Notify(nameof(CanCreateCharacterForOwner));
            Notify(nameof(CanManageCharacterVisibility));
            Notify(nameof(CanRefreshNotes));
            Notify(nameof(CanCreateNote));
            Notify(nameof(CanArchiveNote));
            Notify(nameof(CanModerateChatUser));
            Notify(nameof(CanManageChatControls));
            Notify(nameof(CanRollCharacterDice));
            Notify(nameof(DiceRollAvailabilityHint));
            TraceDiceAvailability();
        }
    }
    public bool IsIdle => !IsBusy;
    public string BusyMessage { get => _busyMessage; set { _busyMessage = value; Notify(); } }
    public string ServerHostInput { get => _serverHostInput; set { _serverHostInput = value; Notify(); } }
    public string ServerPortInput { get => _serverPortInput; set { _serverPortInput = value; Notify(); } }
    public string LastServerHost { get => _lastServerHost; set { _lastServerHost = value; Notify(); } }
    public int LastServerPort { get => _lastServerPort; set { _lastServerPort = value; Notify(); } }
    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            var normalized = NormalizeMainSection(value);
            if (_selectedSection != normalized)
            {
                _selectedSection = normalized;
                Notify();
                SyncMainSectionIndex(normalized);
                SyncSelectedNavigationItemForSection(normalized);
            }
        }
    }
    public int SelectedSectionIndex
    {
        get => _selectedSectionIndex;
        set
        {
            if (value < 0 || value >= MainSectionOrder.Length) return;
            if (_selectedSectionIndex != value)
            {
                _selectedSectionIndex = value;
                Notify();
                var section = MainSectionOrder[value];
                if (_selectedSection != section)
                {
                    _selectedSection = section;
                    Notify(nameof(SelectedSection));
                    SyncSelectedNavigationItemForSection(section);
                }
            }
        }
    }
    public string SelectedNavigationItemId
    {
        get => _selectedNavigationItemId;
        set
        {
            if (_selectedNavigationItemId != value)
            {
                _selectedNavigationItemId = value;
                Notify();
                Notify(nameof(SelectedNavigationItem));
                Notify(nameof(SelectedNavigationTitle));
                Notify(nameof(SelectedNavigationSubtitle));
                Notify(nameof(SelectedNavigationGroupTitle));
                Notify(nameof(SelectedNavigationBreadcrumb));
            }
        }
    }
    public AdminNavigationItem? SelectedNavigationItem => NavigationGroups.SelectMany(group => group.Items).FirstOrDefault(item => item.Id == SelectedNavigationItemId);
    public string SelectedNavigationTitle => SelectedNavigationItem?.Title ?? "Обзор";
    public string SelectedNavigationSubtitle => SelectedNavigationItem?.Description ?? "Выберите раздел GM-панели.";
    public string SelectedNavigationGroupTitle => NavigationGroups.FirstOrDefault(group => group.Id == SelectedNavigationItem?.GroupId)?.Title ?? "Разделы";
    public string SelectedNavigationBreadcrumb => $"{SelectedNavigationGroupTitle} / {SelectedNavigationTitle}";
    public string ActiveCampaignSummary => "Кампания по умолчанию";
    public string ActiveSessionTopBarSummary => IsSessionActive ? SessionStateSummary : "Активная сессия не выбрана";
    public string NotificationSummary => PendingRequestsCount > 0 ? $"Заявки: {PendingRequestsCount}" : "Нет новых заявок";
    public string ShellSearchText
    {
        get => _shellSearchText;
        set
        {
            if (_shellSearchText != value)
            {
                _shellSearchText = value;
                Notify();
                ApplyShellNavigationSearch();
                Notify(nameof(ShellSearchHint));
            }
        }
    }
    public string ShellSearchHint => string.IsNullOrWhiteSpace(ShellSearchText)
        ? "Нет данных"
        : NavigationGroups.SelectMany(group => group.Items).Any(item => item.IsSearchVisible)
            ? $"Найдено разделов: {NavigationGroups.SelectMany(group => group.Items).Count(item => item.IsSearchVisible)}"
            : "Ничего не найдено";
    public ObservableCollection<GlobalSearchResultVm> GlobalSearchResults { get; } = new ObservableCollection<GlobalSearchResultVm>();
    public ObservableCollection<string> GlobalSearchCategories { get; } = new ObservableCollection<string>
    {
        "all",
        "characters",
        "inventory",
        "definitions",
        "development",
        "requests",
        "gm_notes",
        "journal",
        "calendar",
        "backups"
    };
    public string GlobalSearchQuery { get => _globalSearchQuery; set { _globalSearchQuery = value ?? string.Empty; Notify(); } }
    public string GlobalSearchCategoryFilter { get => _globalSearchCategoryFilter; set { _globalSearchCategoryFilter = string.IsNullOrWhiteSpace(value) ? "all" : value; Notify(); } }
    public bool GlobalSearchIncludeArchived { get => _globalSearchIncludeArchived; set { _globalSearchIncludeArchived = value; Notify(); } }
    public bool GlobalSearchIncludeHidden { get => _globalSearchIncludeHidden; set { _globalSearchIncludeHidden = value; Notify(); } }
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
    public string CharactersSearchText { get => _charactersSearchText; set { _charactersSearchText = value; Notify(); Notify(nameof(FilteredCharacters)); var filtered = FilteredCharacters.Count(); ClientLogService.Instance.Info($"ui-filter section=... block=... query={_charactersSearchText} loaded={Characters.Count} filtered={filtered} visible={filtered}"); } }
    public string LocksSearchText { get => _locksSearchText; set { _locksSearchText = value; Notify(); Notify(nameof(FilteredLockRows)); var filtered = FilteredLockRows.Count(); ClientLogService.Instance.Info($"ui-filter section=... block=... query={_locksSearchText} loaded={LockRows.Count} filtered={filtered} visible={filtered}"); } }
    public string SelectedCharacterWorkspaceTab { get => _selectedCharacterWorkspaceTab; set { _selectedCharacterWorkspaceTab = value; Notify(); } }
    public string ClassSearchText { get => _classSearchText; set { _classSearchText = value; Notify(); Notify(nameof(FilteredClassDefinitionRows)); ClientLogService.Instance.Info($"ui-filter section=... block=... query={_classSearchText} loaded={ClassDefinitionRows.Count} visible={FilteredClassDefinitionRows.Count()}"); } }
    public string SkillSearchText { get => _skillSearchText; set { _skillSearchText = value; Notify(); Notify(nameof(FilteredSkillDefinitionRows)); ClientLogService.Instance.Info($"ui-filter section= block= query={_skillSearchText} loaded={SkillDefinitionRows.Count} visible={FilteredSkillDefinitionRows.Count()}"); } }
    public string RaceSearchText { get => _raceSearchText; set { _raceSearchText = value; Notify(); } }
    public string ItemSearchText { get => _itemSearchText; set { _itemSearchText = value; Notify(); } }
    public string SkillCategoryFilter { get => _skillCategoryFilter; set { _skillCategoryFilter = value; Notify(); } }
    public string ClassBranchFilter { get => _classBranchFilter; set { _classBranchFilter = value; Notify(); } }
    public string ItemTypeFilter { get => _itemTypeFilter; set { _itemTypeFilter = value; Notify(); } }
    public string AssignClassCode { get => _assignClassCode; set { _assignClassCode = value; Notify(); } }
    public string AssignClassNodeId { get => _assignClassNodeId; set { _assignClassNodeId = value; Notify(); } }
    public string AssignClassLevel { get => _assignClassLevel; set { _assignClassLevel = value; Notify(); } }
    public string NodePositionX { get => _nodePositionX; set => SetClassNodeLayoutText(ref _nodePositionX, value, nameof(NodePositionX)); }
    public string NodePositionY { get => _nodePositionY; set => SetClassNodeLayoutText(ref _nodePositionY, value, nameof(NodePositionY)); }
    public string NodeHexagonId { get => _nodeHexagonId; set => SetClassNodeLayoutText(ref _nodeHexagonId, string.IsNullOrWhiteSpace(value) ? DevelopmentHexagonIds.Main : value, nameof(NodeHexagonId)); }
    public string NodeHexagonType { get => _nodeHexagonType; set => SetClassNodeLayoutText(ref _nodeHexagonType, string.IsNullOrWhiteSpace(value) ? DevelopmentHexagonTypes.Main : value, nameof(NodeHexagonType)); }
    public string NodeName { get => _nodeName; set => SetClassNodeLayoutText(ref _nodeName, value, nameof(NodeName)); }
    public string NodeDescription { get => _nodeDescription; set => SetClassNodeLayoutText(ref _nodeDescription, value, nameof(NodeDescription)); }
    public string NodeType { get => _nodeType; set => SetClassNodeLayoutText(ref _nodeType, string.IsNullOrWhiteSpace(value) ? DevelopmentNodeTypes.Class : value, nameof(NodeType)); }
    public string NodeRole { get => _nodeRole; set => SetClassNodeLayoutText(ref _nodeRole, string.IsNullOrWhiteSpace(value) ? DevelopmentNodeRoleIds.MainBranchLevel : value, nameof(NodeRole)); }
    public string NodeVisibilityRule { get => _nodeVisibilityRule; set => SetClassNodeLayoutText(ref _nodeVisibilityRule, string.IsNullOrWhiteSpace(value) ? DevelopmentUnlockPolicyIds.VisibleByDefault : value, nameof(NodeVisibilityRule)); }
    public string NodeRing { get => _nodeRing; set => SetClassNodeLayoutText(ref _nodeRing, value, nameof(NodeRing)); }
    public string NodeSector { get => _nodeSector; set => SetClassNodeLayoutText(ref _nodeSector, value, nameof(NodeSector)); }
    public string NodeDirectionCode { get => _nodeDirectionCode; set => SetClassNodeLayoutText(ref _nodeDirectionCode, value, nameof(NodeDirectionCode)); }
    public string NodeBranchCode { get => _nodeBranchCode; set => SetClassNodeLayoutText(ref _nodeBranchCode, value, nameof(NodeBranchCode)); }
    public string NodeSortOrder { get => _nodeSortOrder; set => SetClassNodeLayoutText(ref _nodeSortOrder, value, nameof(NodeSortOrder)); }
    public string NodeRequiredNodes { get => _nodeRequiredNodes; set => SetClassNodeLayoutText(ref _nodeRequiredNodes, value, nameof(NodeRequiredNodes)); }
    public string NodeLinkedClassId { get => _nodeLinkedClassId; set => SetClassNodeLayoutText(ref _nodeLinkedClassId, value, nameof(NodeLinkedClassId)); }
    public string NodeLinkedDefinitionKind { get => _nodeLinkedDefinitionKind; set => SetClassNodeLayoutText(ref _nodeLinkedDefinitionKind, value, nameof(NodeLinkedDefinitionKind)); }
    public string NodeLinkedDefinitionId { get => _nodeLinkedDefinitionId; set => SetClassNodeLayoutText(ref _nodeLinkedDefinitionId, value, nameof(NodeLinkedDefinitionId)); }
    public string NodeCost { get => _nodeCost; set => SetClassNodeLayoutText(ref _nodeCost, value, nameof(NodeCost)); }
    public string NodeCurrencyId
    {
        get => _nodeCurrencyId;
        set
        {
            SetClassNodeLayoutText(ref _nodeCurrencyId, string.IsNullOrWhiteSpace(value) ? CharacterCurrencyIds.XpCoin : value, nameof(NodeCurrencyId));
            Notify(nameof(NodeCurrencyDisplayInput));
            Notify(nameof(NodeCurrencyDisplayName));
        }
    }
    public string NodeCurrencyDisplayInput
    {
        get => DevelopmentGraphDisplay.ToReadableCurrency(NodeCurrencyId);
        set => NodeCurrencyId = DevelopmentGraphDisplay.ToCurrencyId(value);
    }
    public string NodeCurrencyDisplayName => DevelopmentGraphDisplay.ToReadableCurrency(NodeCurrencyId);
    public string NodePrimaryMagicGroupId { get => _nodePrimaryMagicGroupId; set => SetClassNodeLayoutText(ref _nodePrimaryMagicGroupId, value, nameof(NodePrimaryMagicGroupId)); }
    public string NodeLayoutVersion { get => _nodeLayoutVersion; set { _nodeLayoutVersion = value ?? string.Empty; Notify(); } }
    public string NodeLayoutUpdatedAt { get => _nodeLayoutUpdatedAt; set { _nodeLayoutUpdatedAt = value ?? string.Empty; Notify(); } }
    public string NodeLayoutSaveStatus { get => _nodeLayoutSaveStatus; set { _nodeLayoutSaveStatus = value ?? string.Empty; Notify(); } }
    public string RequirementSourceNodeId { get => _requirementSourceNodeId; set { _requirementSourceNodeId = value ?? string.Empty; Notify(); } }
    public string RequirementTargetNodeId { get => _requirementTargetNodeId; set { _requirementTargetNodeId = value ?? string.Empty; Notify(); } }
    public string SelectedDevelopmentLayoutHexagonId
    {
        get => _selectedDevelopmentLayoutHexagonId;
        set
        {
            if (_isRefreshingDevelopmentLayoutEditor) return;
            var normalized = string.IsNullOrWhiteSpace(value) ? DevelopmentHexagonIds.Main : value;
            if (string.Equals(_selectedDevelopmentLayoutHexagonId, normalized, StringComparison.OrdinalIgnoreCase)) return;
            _selectedDevelopmentLayoutHexagonId = normalized;
            Notify();
            Notify(nameof(DevelopmentLayoutTreeModeOverlayText));
            RefreshDevelopmentLayoutEditorFromPayloads();
            RebuildDevelopmentCanonicalOverlay();
        }
    }
    public DevelopmentHexagonEditorNodeVm? SelectedDevelopmentLayoutNode
    {
        get => _selectedDevelopmentLayoutNode;
        set
        {
            if (_selectedDevelopmentLayoutNode == value) return;
            if (_selectedDevelopmentLayoutNode != null) _selectedDevelopmentLayoutNode.IsSelected = false;
            _selectedDevelopmentLayoutNode = value;
            if (_selectedDevelopmentLayoutNode != null)
            {
                _selectedDevelopmentLayoutNode.IsSelected = true;
                SelectedClassNodeId = _selectedDevelopmentLayoutNode.NodeId;
            }
            ApplyDevelopmentLayoutFocusState();
            Notify();
            Notify(nameof(SelectedDevelopmentLayoutNodeSummary));
            Notify(nameof(SelectedDevelopmentLayoutIncomingLinksText));
            Notify(nameof(SelectedDevelopmentLayoutOutgoingLinksText));
        }
    }
    public DevelopmentHexagonEditorLinkVm? SelectedDevelopmentLayoutLink
    {
        get => _selectedDevelopmentLayoutLink;
        set
        {
            if (_selectedDevelopmentLayoutLink == value) return;
            if (_selectedDevelopmentLayoutLink != null) _selectedDevelopmentLayoutLink.IsSelected = false;
            _selectedDevelopmentLayoutLink = value;
            if (_selectedDevelopmentLayoutLink != null)
            {
                _selectedDevelopmentLayoutLink.IsSelected = true;
                RequirementSourceNodeId = _selectedDevelopmentLayoutLink.SourceNodeId;
                RequirementTargetNodeId = _selectedDevelopmentLayoutLink.TargetNodeId;
            }
            Notify();
            Notify(nameof(SelectedDevelopmentLayoutLinkSummary));
            Notify(nameof(SelectedDevelopmentLayoutLinkDirectionText));
        }
    }
    public string SelectedDevelopmentLayoutLinkSummary => SelectedDevelopmentLayoutLink == null ? "Связь не выбрана" : SelectedDevelopmentLayoutLink.Label;
    public string SelectedDevelopmentLayoutNodeSummary => SelectedDevelopmentLayoutNode == null
        ? "Нет данных"
        : $"{SelectedDevelopmentLayoutNode.DisplayTitle} · {SelectedDevelopmentLayoutNode.PositionText} · {SelectedDevelopmentLayoutNode.VisibilityText}";
    public string DevelopmentLayoutTreeModeOverlayText
    {
        get
        {
            var tree = DevelopmentLayoutHexagons.FirstOrDefault(h => string.Equals(h.HexagonId, SelectedDevelopmentLayoutHexagonId, StringComparison.OrdinalIgnoreCase));
            var treeName = DevelopmentGraphDisplay.ToReadableText(tree?.Name ?? SelectedDevelopmentLayoutHexagonId);
            var isMagic = string.Equals(SelectedDevelopmentLayoutHexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase);
            var isLarge = string.Equals(SelectedDevelopmentLayoutHexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase);
            var treeKind = DevelopmentLayoutShowDiagnostics
                ? "Диагностическое дерево"
                : isLarge
                    ? "Большое тестовое дерево"
                    : isMagic
                        ? "Магический шестиугольник"
                        : "Основной шестиугольник";
            var mode = DevelopmentLayoutShowDiagnostics
                ? "Диагностика"
                : isLarge
                    ? "Большое тестовое дерево"
                    : "Канонический шестиугольник";
            var visibleWorking = isLarge
                ? DevelopmentLayoutNodes.Count(node => !node.IsFilteredOut)
                : DevelopmentLayoutNodes.Count(node => !node.IsFilteredOut && !node.IsDiagnosticNode);
            var diagnostic = DevelopmentLayoutNodes.Count(node => node.IsDiagnosticNode);
            var rootLabel = ResolveDevelopmentCanonicalRootLabel(
                SelectedDevelopmentLayoutHexagonId,
                FindDevelopmentCanonicalRootNode(SelectedDevelopmentLayoutHexagonId));
            var laneCount = DevelopmentLayoutCanonicalLanes.Count;
            var directionCount = DevelopmentLayoutCanonicalDirections.Count;
            return $"Дерево: {treeName}; Ключ: {SelectedDevelopmentLayoutHexagonId}; Режим: {mode}; Тип: {treeKind}; Корень: {rootLabel}; Узлов: {visibleWorking}; Связей: {DevelopmentLayoutLinks.Count}; Направлений: {directionCount}; Линий: {laneCount}; Диагностических узлов: {diagnostic}";
        }
    }
    public string DevelopmentLayoutStatusText { get => _developmentLayoutStatusText; set { _developmentLayoutStatusText = value ?? string.Empty; Notify(); } }
    public bool DevelopmentLayoutHasUnsavedChanges
    {
        get => _developmentLayoutHasUnsavedChanges;
        set
        {
            if (_developmentLayoutHasUnsavedChanges == value) return;
            _developmentLayoutHasUnsavedChanges = value;
            Notify();
            Notify(nameof(DevelopmentLayoutDirtyText));
        }
    }
    public string DevelopmentLayoutDirtyText => DevelopmentLayoutHasUnsavedChanges ? "Есть несохранённые изменения" : "Изменений нет";
    public string DevelopmentLayoutQualityText { get => _developmentLayoutQualityText; set { _developmentLayoutQualityText = value ?? string.Empty; Notify(); } }
    public string DevelopmentLayoutSnapshotText { get => _developmentLayoutSnapshotText; set { _developmentLayoutSnapshotText = value ?? string.Empty; Notify(); } }
    public bool DevelopmentLayoutPreviewActive { get => _developmentLayoutPreviewActive; set { if (_developmentLayoutPreviewActive == value) return; _developmentLayoutPreviewActive = value; Notify(); Notify(nameof(DevelopmentLayoutPreviewText)); } }
    public string DevelopmentLayoutPreviewText => DevelopmentLayoutPreviewActive ? "Предпросмотр активен: изменения ещё не сохранены" : "Предпросмотр выключен";
    public double DevelopmentLayoutWorkspaceWidth => DevelopmentLayoutVisualRules.WorkspaceWidth;
    public double DevelopmentLayoutWorkspaceHeight => DevelopmentLayoutVisualRules.WorkspaceHeight;
    public double DevelopmentLayoutViewportTranslateX
    {
        get => _developmentLayoutViewportTranslateX;
        private set
        {
            if (Math.Abs(_developmentLayoutViewportTranslateX - value) < 0.01) return;
            _developmentLayoutViewportTranslateX = value;
            Notify();
        }
    }
    public double DevelopmentLayoutViewportTranslateY
    {
        get => _developmentLayoutViewportTranslateY;
        private set
        {
            if (Math.Abs(_developmentLayoutViewportTranslateY - value) < 0.01) return;
            _developmentLayoutViewportTranslateY = value;
            Notify();
        }
    }
    public double DevelopmentLayoutZoom
    {
        get => _developmentLayoutZoom;
        set
        {
            var normalized = Math.Max(0.05, Math.Min(1.5, value));
            if (Math.Abs(_developmentLayoutZoom - normalized) < 0.01) return;
            _developmentLayoutZoom = normalized;
            Notify();
            Notify(nameof(DevelopmentLayoutZoomText));
        }
    }
    public string DevelopmentLayoutZoomText => $"{DevelopmentLayoutZoom:P0}";
    public bool DevelopmentLayoutShowGrid
    {
        get => _developmentLayoutShowGrid;
        set
        {
            if (_developmentLayoutShowGrid == value) return;
            _developmentLayoutShowGrid = value;
            Notify();
            Notify(nameof(DevelopmentLayoutGridOpacity));
        }
    }
    public double DevelopmentLayoutGridOpacity => DevelopmentLayoutShowGrid ? 1.0 : 0.0;
    public bool DevelopmentLayoutSnapToGrid { get => _developmentLayoutSnapToGrid; set { _developmentLayoutSnapToGrid = value; Notify(); } }
    public bool DevelopmentLayoutShowLegend
    {
        get => _developmentLayoutShowLegend;
        set
        {
            if (_developmentLayoutShowLegend == value) return;
            _developmentLayoutShowLegend = value;
            Notify();
            Notify(nameof(DevelopmentLayoutLegendVisibility));
        }
    }
    public System.Windows.Visibility DevelopmentLayoutLegendVisibility => DevelopmentLayoutShowLegend ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public bool DevelopmentLayoutShowDiagnostics
    {
        get => _developmentLayoutShowDiagnostics;
        set
        {
            if (_developmentLayoutShowDiagnostics == value) return;
            _developmentLayoutShowDiagnostics = value;
            Notify();
            Notify(nameof(DevelopmentLayoutCanonicalModeSelected));
            Notify(nameof(DevelopmentLayoutDiagnosticGraphModeSelected));
            Notify(nameof(DevelopmentLayoutViewModeText));
            Notify(nameof(DevelopmentLayoutCanonicalLayerVisibility));
            ApplyDevelopmentLayoutSearchAndFilters();
            ApplyDevelopmentLayoutFocusState();
            RebuildDevelopmentCanonicalOverlay();
            Notify(nameof(DevelopmentLayoutTreeModeOverlayText));
            DevelopmentLayoutStatusText = value
                ? "Показано диагностическое дерево; обычная рабочая раскладка отделена."
                : "Диагностическое дерево скрыто из обычной рабочей раскладки.";
        }
    }
    public bool DevelopmentLayoutCanonicalModeSelected
    {
        get => !DevelopmentLayoutShowDiagnostics;
        set { if (value) DevelopmentLayoutShowDiagnostics = false; }
    }
    public bool DevelopmentLayoutDiagnosticGraphModeSelected
    {
        get => DevelopmentLayoutShowDiagnostics;
        set { if (value) DevelopmentLayoutShowDiagnostics = true; }
    }
    public string DevelopmentLayoutViewModeText => DevelopmentLayoutShowDiagnostics ? "Диагностический граф" : "Канонический шестиугольник";
    public System.Windows.Visibility DevelopmentLayoutCanonicalLayerVisibility => DevelopmentLayoutShowDiagnostics ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    public bool DevelopmentLayoutShowDirectionLabels
    {
        get => _developmentLayoutShowDirectionLabels;
        set
        {
            if (_developmentLayoutShowDirectionLabels == value) return;
            _developmentLayoutShowDirectionLabels = value;
            Notify();
            Notify(nameof(DevelopmentLayoutDirectionLabelsVisibility));
        }
    }
    public System.Windows.Visibility DevelopmentLayoutDirectionLabelsVisibility => DevelopmentLayoutShowDirectionLabels ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public string DevelopmentLayoutFocusedDirectionKey
    {
        get => _developmentLayoutFocusedDirectionKey;
        set
        {
            var normalized = value ?? string.Empty;
            if (_developmentLayoutFocusedDirectionKey == normalized) return;
            _developmentLayoutFocusedDirectionKey = normalized;
            Notify();
            RebuildDevelopmentCanonicalOverlay();
            ApplyDevelopmentLayoutFocusState();
        }
    }
    public bool DevelopmentLayoutFocusSelectedNodeLinks
    {
        get => _developmentLayoutFocusSelectedNodeLinks;
        set
        {
            if (_developmentLayoutFocusSelectedNodeLinks == value) return;
            _developmentLayoutFocusSelectedNodeLinks = value;
            Notify();
            ApplyDevelopmentLayoutFocusState();
            DevelopmentLayoutStatusText = value
                ? "Показаны связи выбранного узла; остальные связи приглушены."
                : "Показаны все связи графа.";
        }
    }
    public string DevelopmentLayoutSearchText
    {
        get => _developmentLayoutSearchText;
        set
        {
            if (_developmentLayoutSearchText == (value ?? string.Empty)) return;
            _developmentLayoutSearchText = value ?? string.Empty;
            _developmentLayoutSearchIndex = -1;
            Notify();
            ApplyDevelopmentLayoutSearchAndFilters();
        }
    }
    public string DevelopmentLayoutTypeFilter { get => _developmentLayoutTypeFilter; set { _developmentLayoutTypeFilter = string.IsNullOrWhiteSpace(value) ? "all" : value; Notify(); ApplyDevelopmentLayoutSearchAndFilters(); } }
    public string DevelopmentLayoutVisibilityFilter { get => _developmentLayoutVisibilityFilter; set { _developmentLayoutVisibilityFilter = string.IsNullOrWhiteSpace(value) ? "all" : value; Notify(); ApplyDevelopmentLayoutSearchAndFilters(); } }
    public string DevelopmentLayoutStateFilter { get => _developmentLayoutStateFilter; set { _developmentLayoutStateFilter = string.IsNullOrWhiteSpace(value) ? "all" : value; Notify(); ApplyDevelopmentLayoutSearchAndFilters(); } }
    public string DevelopmentLayoutLinkedKindFilter { get => _developmentLayoutLinkedKindFilter; set { _developmentLayoutLinkedKindFilter = string.IsNullOrWhiteSpace(value) ? "all" : value; Notify(); ApplyDevelopmentLayoutSearchAndFilters(); } }
    public string DevelopmentLayoutSearchResultCountText
    {
        get
        {
            var filtered = DevelopmentLayoutNodes.Count(node => !node.IsFilteredOut);
            var matches = string.IsNullOrWhiteSpace(DevelopmentLayoutSearchText)
                ? filtered
                : DevelopmentLayoutNodes.Count(node => !node.IsFilteredOut && node.IsSearchMatch);
            return string.IsNullOrWhiteSpace(DevelopmentLayoutSearchText)
                ? $"В области фильтра: {filtered}"
                : $"Найдено: {matches} / {filtered}";
        }
    }
    public string SelectedDevelopmentLayoutLinkDirectionText => SelectedDevelopmentLayoutLink?.DirectionText ?? "Связь не выбрана";
    public string SelectedDevelopmentLayoutIncomingLinksText => SelectedDevelopmentLayoutNode == null
        ? "Входящие связи: узел не выбран"
        : $"Входящие связи: {DevelopmentLayoutLinks.Count(link => string.Equals(link.TargetNodeId, SelectedDevelopmentLayoutNode.NodeId, StringComparison.OrdinalIgnoreCase))}";
    public string SelectedDevelopmentLayoutOutgoingLinksText => SelectedDevelopmentLayoutNode == null
        ? "Исходящие связи: узел не выбран"
        : $"Исходящие связи: {DevelopmentLayoutLinks.Count(link => string.Equals(link.SourceNodeId, SelectedDevelopmentLayoutNode.NodeId, StringComparison.OrdinalIgnoreCase))}";
    public string DevelopmentLayoutValidationSummary => $"Ошибки: {DevelopmentLayoutValidationErrors.Count}; предупреждения: {DevelopmentLayoutValidationWarnings.Count}";
    public ObservableCollection<string> DevelopmentLayoutChangedObjects { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> DevelopmentLayoutValidationErrors { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> DevelopmentLayoutValidationWarnings { get; } = new ObservableCollection<string>();
    public bool NodeIsPlayerVisible { get => _nodeIsPlayerVisible; set => SetClassNodeLayoutBool(ref _nodeIsPlayerVisible, value, nameof(NodeIsPlayerVisible)); }
    public bool NodeIsHidden { get => _nodeIsHidden; set => SetClassNodeLayoutBool(ref _nodeIsHidden, value, nameof(NodeIsHidden)); }
    public bool NodeIsArchived { get => _nodeIsArchived; set => SetClassNodeLayoutBool(ref _nodeIsArchived, value, nameof(NodeIsArchived)); }
    public bool NodeIsPrimaryMagicClass { get => _nodeIsPrimaryMagicClass; set => SetClassNodeLayoutBool(ref _nodeIsPrimaryMagicClass, value, nameof(NodeIsPrimaryMagicClass)); }
    public bool NodeLayoutLockedManualPosition { get => _nodeLayoutLockedManualPosition; set => SetClassNodeLayoutBool(ref _nodeLayoutLockedManualPosition, value, nameof(NodeLayoutLockedManualPosition)); }
    public bool DevelopmentLinkModeEnabled { get => _developmentLinkModeEnabled; set { _developmentLinkModeEnabled = value; Notify(); } }
    public string CurrentEndpoint => $"{_client.ServerHost}:{_client.ServerPort}";
    public string LoginSummary => string.IsNullOrWhiteSpace(LoginText) ? "Не вошли" : LoginText;
    public int PendingAccountsCount => PendingAccounts.Count;
    public int PlayersCount => Players.Count;
    public int CharactersCount => Characters.Count;
    public int PendingRequestsCount => PendingRequests.Count;
    public int ActivePlayersCount => Players.Count(player => string.Equals(player.State, "В сети", StringComparison.OrdinalIgnoreCase));
    public bool HasActiveCombat => CombatRows.Any(row => row.IndexOf("Status:", StringComparison.OrdinalIgnoreCase) >= 0 && row.IndexOf("Ended", StringComparison.OrdinalIgnoreCase) < 0);
    public string ChatSummary => ChatRows.Count == 0 ? "Чат: сообщений нет" : $"Чат: {ChatRows.Count} сообщений";
    public string AudioSummary => string.IsNullOrWhiteSpace(AudioStateText) ? "Музыка: нет данных" : $"Музыка: {AudioStateText}";
    public string DiagnosticsSummary => DiagnosticsItems.Count == 0 ? "Диагностика: данных нет" : $"{DiagnosticsItems[0].Name} / {DiagnosticsItems[0].State} / {DiagnosticsItems[0].Extra}";
    public string SessionStateSummary => CombatRows.FirstOrDefault(row => row.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))?.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? (ArePrivilegedSectionsEnabled ? "Сессия готова" : "Сессия готова");
    public int ActiveCombatParticipantsCount => CombatRows.Count(row => row.StartsWith("P:", StringComparison.OrdinalIgnoreCase));
    public string CombatTrackerSummary => HasActiveCombat ? $"Активный бой: участников {ActiveCombatParticipantsCount}" : ActiveCombatParticipantsCount > 0 ? $"Активный бой: участников {ActiveCombatParticipantsCount}" : "Бой не активен.";
    public bool IsSessionActive => ArePrivilegedSectionsEnabled && (HasActiveCombat || ChatRows.Count > 0 || AudioLibraryRows.Count > 0 || !string.IsNullOrWhiteSpace(AudioStateText));
    public int CombatOpponentsCount => CombatParticipantRows.Count(row => row.State.IndexOf("Npc", StringComparison.OrdinalIgnoreCase) >= 0 || row.State.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0);
    public RowVm? SelectedCombatParticipant => CombatParticipantRows.FirstOrDefault(row => row.Id == SelectedCombatParticipantId);
    public string SelectedCombatParticipantSummary => SelectedCombatParticipant == null ? "Участник боя не выбран." : $"{SelectedCombatParticipant.Name} / {SelectedCombatParticipant.State} / {SelectedCombatParticipant.Extra}";
    public string SessionAttentionSummary => HasActiveCombat ? "Сессия ожидает действий." : ChatRows.Count > 0 ? "Сессия ожидает действий." : !string.IsNullOrWhiteSpace(AudioStateText) ? "Сессия ожидает действий." : "Сессия ожидает действий.";
    public string ChatActivitySummary => ChatRows.Count == 0 ? "Сообщений нет" : $"Сообщений нет";
    public string AudioTrackSummary => string.IsNullOrWhiteSpace(AudioStateText) ? "  " : AudioStateText;
    public bool CanManageCombatSelection => ArePrivilegedSectionsEnabled && SelectedCombatParticipant != null && !IsBusy;
    public bool CanControlCombat => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanSendChat => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(ChatMessageText);
    public bool CanControlAudio => ArePrivilegedSectionsEnabled && !IsBusy;
    public string ContentSummary => $"Классы: {ClassDefinitionRows.Count}; навыки: {SkillDefinitionRows.Count}";
    public string ContentReadinessSummary => !ArePrivilegedSectionsEnabled ? "Справочники готовы к проверке." : "Справочники готовы к проверке.";
    public string SelectedClassSummary => SelectedClassDefinition == null ? "Класс не выбран." : $"{SelectedClassDefinition.Name} / {SelectedClassDefinition.State} / {SelectedClassDefinition.Extra}";
    public string SelectedSkillSummary => SelectedSkillDefinition == null ? "Навык не выбран." : $"{SelectedSkillDefinition.Name} / {SelectedSkillDefinition.State} / {SelectedSkillDefinition.Extra}";
    public string SelectedReferenceSummary => SelectedReference == null ? "Справочник не выбран." : $"{SelectedReference.Name} / {SelectedReference.State} / {SelectedReference.Extra}";
    public string SelectedContentSummary => SelectedClassDefinition != null ? SelectedClassSummary : SelectedSkillDefinition != null ? SelectedSkillSummary : SelectedReferenceSummary;
    public string ReferenceSummary => ReferenceItems.Count == 0 ? "Справочники: данных нет" : $"Справочники: данных нет";
    public string BackupSummary => BackupItems.Count == 0 ? "Резервные копии: данных нет" : $"Резервные копии: данных нет";
    public string DiagnosticsStatusSummary => DiagnosticsItems.Count == 0 ? "Диагностика не загружена" : DiagnosticsItems[0].Name;
    public string SelectedBackupSummary => SelectedBackup == null ? "Резервные копии: данных нет" : $"{SelectedBackup.Name} / {SelectedBackup.State} / {SelectedBackup.Extra}";
    public string SelectedDiagnosticsSummary => SelectedDiagnostics == null ? "Диагностика" : $"{SelectedDiagnostics.Name} / {SelectedDiagnostics.State} / {SelectedDiagnostics.Extra}";
    public string SystemHealthSummary => DiagnosticsItems.Count == 0 ? "Резервная копия" : $"Резервная копия";
    public bool CanControlContent => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanRefreshContent => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanManageClassDefinition => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanArchiveClassDefinition => ArePrivilegedSectionsEnabled && !IsBusy && SelectedClassDefinition != null;
    public bool CanManageSkillDefinition => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanCreateSkillDefinition => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(EditSkillCode) && !string.IsNullOrWhiteSpace(EditSkillName);
    public bool CanRefreshSkillDefinitions => ArePrivilegedSectionsEnabled;
    public bool CanSaveSkillDefinition => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedSkillDefinitionCode) && !string.IsNullOrWhiteSpace(EditSkillCode) && !string.IsNullOrWhiteSpace(EditSkillName);
    public bool CanArchiveSkillDefinition => ArePrivilegedSectionsEnabled && !IsBusy && (!string.IsNullOrWhiteSpace(SelectedSkillDefinitionCode) || !string.IsNullOrWhiteSpace(EditSkillCode));
    public bool CanAcquireClassNode => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedCharacterId) && SelectedClassNode != null;
    public bool CanAcquireSkill => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedCharacterId) && !string.IsNullOrWhiteSpace(SelectedSkillDefinitionCode);
    public bool CanUpdateCharacterSkillLevel => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedCharacterId) && SelectedSkill != null;
    public bool CanSaveCharacterSkillProfile => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedCharacterId) && !string.IsNullOrWhiteSpace(CharacterSkillSelectedSkillIdInput);
    public bool CanRemoveCharacterSkill => CanUpdateCharacterSkillLevel;
    public bool CanManageReferenceRecord => ArePrivilegedSectionsEnabled && !IsBusy && SelectedReference != null;
    public bool CanManageSelectedBackup => ArePrivilegedSectionsEnabled && !IsBusy && SelectedBackup != null;
    public bool CanRefreshSystem => ArePrivilegedSectionsEnabled && !IsBusy;
    public string WorkspaceSummary => $"Панели: открыто {WorkspacePanels.Count(panel => panel.IsVisible && !panel.IsDetached)}, отдельно {WorkspacePanels.Count(panel => panel.IsVisible && panel.IsDetached)}, скрыто {WorkspacePanels.Count(panel => !panel.IsVisible)}";
    public RowVm? SelectedLock => LockRows.FirstOrDefault(row => row.Id == SelectedLockId);
    public RowVm? SelectedPendingAccount => PendingAccounts.FirstOrDefault(row => row.Id == SelectedPendingAccountId);
    public RowVm? SelectedPlayer => Players.FirstOrDefault(row => row.Id == SelectedOwnerUserId);
    public RowVm? SelectedCharacter => Characters.FirstOrDefault(row => row.Id == SelectedCharacterId);
    public RowVm? SelectedRequest => PendingRequests.FirstOrDefault(row => row.Id == SelectedPendingRequestId);
    public RowVm? SelectedClassDefinition => ClassDefinitionRows.FirstOrDefault(row => row.Id == SelectedClassDefinitionCode);
    public RowVm? SelectedSkillDefinition => SkillDefinitionRows.FirstOrDefault(row => row.Id == SelectedSkillDefinitionCode);
    public RowVm? SelectedRaceDefinition => RaceDefinitionRows.FirstOrDefault(row => row.Id == SelectedRaceDefinitionCode);
    public RowVm? SelectedItemDefinition => ItemDefinitionRows.FirstOrDefault(row => row.Id == SelectedItemDefinitionCode);
    public RowVm? SelectedClassNode => ClassTreeItems.FirstOrDefault(row => row.Id == SelectedClassNodeId);
    public RowVm? SelectedSkill => SkillRows.FirstOrDefault(row => row.Id == SelectedSkillId);
    public RowVm? SelectedReference => ReferenceItems.FirstOrDefault(row => row.Id == ReferenceId);
    public RowVm? SelectedBackup => BackupItems.FirstOrDefault(row => row.Id == SelectedBackupId);
    public RowVm? SelectedDiagnostics => DiagnosticsItems.FirstOrDefault(row => row.Id == SelectedDiagnosticsId);
    public string SelectedPendingAccountSummary => SelectedPendingAccount == null ? "Нет данных" : $"{SelectedPendingAccount.Name} / {SelectedPendingAccount.State} / {SelectedPendingAccount.Extra}";
    public string SelectedPlayerSummary => SelectedPlayer == null ? "Нет данных" : $"{SelectedPlayer.Name} / {SelectedPlayer.State} / {SelectedPlayer.Extra}";
    public string SelectedCharacterSummary => SelectedCharacter == null ? "Нет данных" : $"Нет данных";
    public string SelectedRequestSummary => SelectedRequest == null ? "Нет данных" : $"{SelectedRequest.Name} / {SelectedRequest.State} / {SelectedRequest.Extra}";
    public string SelectedLockSummary => SelectedLock == null ? "Нет данных" : $"{SelectedLock.Name} / {SelectedLock.State} / {SelectedLock.Extra}";
    public string CharacterActionSummary => !ArePrivilegedSectionsEnabled ? "Нет данных" : SelectedCharacter == null ? "Нет данных" : IsBusy ? $"Нет данных" : "Нет данных";
    public string ChatModerationSummary => !ArePrivilegedSectionsEnabled ? "Нет данных" : IsBusy ? $"Нет данных" : string.IsNullOrWhiteSpace(ChatModerationUserId) ? "Нет данных" : $"Нет данных";
    public string SystemActionSummary => !ArePrivilegedSectionsEnabled ? "Нет данных" : IsBusy ? $"Нет данных" : "Нет данных";
    public string HeaderStatusSummary => HasConnectionError ? LastErrorMessage : $"{ConnectionStage} / {LoginState}";
    public bool CanManagePendingAccount => ArePrivilegedSectionsEnabled && SelectedPendingAccount != null && !IsBusy;
    public bool CanResetSelectedAccountPassword => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(ResetPasswordText) && (!string.IsNullOrWhiteSpace(SelectedPendingAccountId) || !string.IsNullOrWhiteSpace(SelectedOwnerUserId));
    public bool CanLoadPlayerCharacters => ArePrivilegedSectionsEnabled && SelectedPlayer != null && !IsBusy;
    public bool CanOpenSelectedCharacter => ArePrivilegedSectionsEnabled && SelectedCharacter != null && !IsBusy;
    public bool CanModerateSelectedRequest => ArePrivilegedSectionsEnabled && !string.IsNullOrWhiteSpace(CurrentSelectedRequestId()) && !IsBusy;
    public bool CanManageSelectedLock => ArePrivilegedSectionsEnabled && SelectedLock != null && !IsBusy;
    public bool CanManageSelectedCharacter => ArePrivilegedSectionsEnabled && SelectedCharacter != null && !IsBusy;
    public bool CanCreateCharacterForOwner => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedOwnerUserId);
    public bool CanRollCharacterDice => string.IsNullOrWhiteSpace(DiceRollAvailabilityHint);
    public string DiceRollAvailabilityHint => GetDiceRollAvailabilityReason();
    public bool CanManageCharacterVisibility => ArePrivilegedSectionsEnabled && SelectedCharacter != null && !IsBusy;
    public bool CanRefreshNotes => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanCreateNote => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanArchiveNote => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedNoteId);
    public bool CanModerateChatUser => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(ChatModerationUserId);
    public bool CanManageChatControls => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanManageWorkspace => !IsBusy;
    public bool CanInitiateConnection => !IsBusy;
    public int SelectedContentTabIndex { get => _selectedContentTabIndex; set { if (_selectedContentTabIndex != value) { _selectedContentTabIndex = value; Notify(); } } }
    public int SelectedSystemTabIndex { get => _selectedSystemTabIndex; set { if (_selectedSystemTabIndex != value) { _selectedSystemTabIndex = value; Notify(); } } }
    public int SelectedSessionTabIndex { get => _selectedSessionTabIndex; set { if (_selectedSessionTabIndex != value) { _selectedSessionTabIndex = value; Notify(); } } }
    public string WorkspaceLayoutPath => Path.Combine(_appDataDirectory, "workspace.layout.json");
    public string ConnectionSettingsPath => Path.Combine(_appDataDirectory, "connection.settings.json");

    public string OwnershipOwnerUserId { get => _ownershipOwnerUserId; set { _ownershipOwnerUserId = value; Notify(); } }
    public string OwnershipControlledByUserId { get => _ownershipControlledByUserId; set { _ownershipControlledByUserId = value; Notify(); } }
    public string OwnershipReason { get => _ownershipReason; set { _ownershipReason = value; Notify(); } }
    public string OwnershipGroupId { get => _ownershipGroupId; set { _ownershipGroupId = value; Notify(); } }
    public string OwnershipGroupName { get => _ownershipGroupName; set { _ownershipGroupName = value; Notify(); } }
    public string OwnershipKind { get => _ownershipKind; set { _ownershipKind = value; Notify(); } }
    public string OwnershipStatus { get => _ownershipStatus; set { _ownershipStatus = value; Notify(); } }
    public bool OwnershipIsActive { get => _ownershipIsActive; set { _ownershipIsActive = value; Notify(); } }
    public bool OwnershipIsArchived { get => _ownershipIsArchived; set { _ownershipIsArchived = value; Notify(); } }
    public bool OwnershipIsPlayerVisible { get => _ownershipIsPlayerVisible; set { _ownershipIsPlayerVisible = value; Notify(); } }
    public string OwnershipMessage { get => _ownershipMessage; set { _ownershipMessage = value; Notify(); } }
    public string OwnershipSummary =>
        $"Владелец: {FirstNonEmpty(OwnershipOwnerUserId, "—")} / группа: {FirstNonEmpty(OwnershipGroupName, OwnershipGroupId, "—")} / тип: {OwnershipKind} / статус: {OwnershipStatus}";

    public string SelectedPendingAccountId
    {
        get => _selectedPendingAccountId;
        set
        {
            if (_selectedPendingAccountId != value)
            {
                _selectedPendingAccountId = value;
                Notify();
                Notify(nameof(SelectedPendingAccount));
                Notify(nameof(SelectedPendingAccountSummary));
                Notify(nameof(CanManagePendingAccount));
                Notify(nameof(CanResetSelectedAccountPassword));
            }
        }
    }

    public string SelectedOwnerUserId
    {
        get => _selectedOwnerUserId;
        set
        {
            if (_selectedOwnerUserId != value)
            {
                _selectedOwnerUserId = value;
                Notify();
                Notify(nameof(SelectedPlayer));
                Notify(nameof(SelectedPlayerSummary));
                Notify(nameof(CanLoadPlayerCharacters));
                Notify(nameof(CanCreateCharacterForOwner));
                Notify(nameof(CanResetSelectedAccountPassword));
                ClientLogService.Instance.Info($"ui.people.owner.selected ownerUserId={_selectedOwnerUserId}");
                if (!string.IsNullOrWhiteSpace(_selectedOwnerUserId) && ArePrivilegedSectionsEnabled)
                {
                    LoadOwnerCharacters();
                }
            }
        }
    }

    public string SelectedCharacterId
    {
        get => _selectedCharacterId;
        set
        {
            if (_selectedCharacterId != value)
            {
                _selectedCharacterId = value;
                if (string.Equals(NoteTargetType, "character", StringComparison.OrdinalIgnoreCase))
                {
                    NoteTargetId = value;
                    Notify(nameof(NoteTargetId));
                }
                Notify();
                Notify(nameof(SelectedCharacter));
                Notify(nameof(SelectedCharacterSummary));
                Notify(nameof(CanOpenSelectedCharacter));
                Notify(nameof(CanRollCharacterDice));
                Notify(nameof(DiceRollAvailabilityHint));
                Notify(nameof(CanAcquireSkill));
                Notify(nameof(CanUpdateCharacterSkillLevel));
                Notify(nameof(CanRemoveCharacterSkill));
                TraceDiceAvailability();
                ClientLogService.Instance.Info($"ui.people.character.selected characterId={_selectedCharacterId}");
                if (ArePrivilegedSectionsEnabled && !string.IsNullOrWhiteSpace(_selectedCharacterId) && !IsBusy)
                {
                    RunUiAction("Открыть персонажа", OpenCharacter);
                }
            }
        }
    }

    public string SelectedPendingRequestId
    {
        get => _selectedPendingRequestId;
        set
        {
            if (_selectedPendingRequestId != value)
            {
                _selectedPendingRequestId = value;
                Notify();
                Notify(nameof(SelectedRequest));
                Notify(nameof(SelectedRequestSummary));
                Notify(nameof(CanModerateSelectedRequest));
                if (ArePrivilegedSectionsEnabled && !string.IsNullOrWhiteSpace(_selectedPendingRequestId) && !IsBusy)
                    LoadSelectedRequestDetails();
            }
        }
    }

    public string SelectedLockId
    {
        get => _selectedLockId;
        set
        {
            if (_selectedLockId != value)
            {
                _selectedLockId = value;
                Notify();
                Notify(nameof(SelectedLock));
                Notify(nameof(SelectedLockSummary));
                Notify(nameof(CanManageSelectedLock));
            }
        }
    }
    public string RequestComment { get; set; } = string.Empty;
    public string RequestGMOnlyComment { get; set; } = string.Empty;
    public string RequestStatusFilter { get; set; } = string.Empty;
    public string RequestTypeFilter { get; set; } = string.Empty;
    public string[] RequestStatusFilters { get; } = { "", "submitted", "in_review", "changes_requested", "approved", "rejected", "cancelled", "archived" };
    public string[] RequestTypeFilters { get; } = { "", "generic_action", "development_unlock", "character_change", "item_request", "rules_question", "scene_action", "research", "crafting", "purchase" };
    public string AdminSelectedRequestTitle { get; set; } = "Заявка без названия";
    public string AdminSelectedRequestPlayer { get; set; } = "Игрок: —";
    public string AdminSelectedRequestCharacter { get; set; } = "Персонаж: —";
    public string AdminSelectedRequestType { get; set; } = "Тип: —";
    public string AdminSelectedRequestActors { get; set; } = "Последнее действие: —";
    public string AdminSelectedRequestDetails { get; set; } = "Данные заявки не загружены.";
    public string CombatSessionId { get; set; } = "default";
    public string NewParticipantName { get; set; } = "New NPC";
    public string NewParticipantKind { get; set; } = "Npc";
    public string SelectedCombatParticipantId
    {
        get => _selectedCombatParticipantId;
        set
        {
            if (_selectedCombatParticipantId != value)
            {
                _selectedCombatParticipantId = value;
                Notify();
                Notify(nameof(SelectedCombatParticipant));
                Notify(nameof(SelectedCombatParticipantSummary));
                Notify(nameof(CanManageCombatSelection));
            }
        }
    }
    public string LockStateText { get; set; } = string.Empty;
    public string SelectedClassNodeId
    {
        get => _selectedClassNodeId;
        set
        {
            if (_selectedClassNodeId != value)
            {
                _selectedClassNodeId = value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _isClassNodeLayoutDirty = false;
                    AssignClassNodeId = value;
                    if (string.IsNullOrWhiteSpace(EditClassRequiredNodeId)) EditClassRequiredNodeId = value;
                }
                LoadSelectedClassNodeLayoutEditor();
                SyncDevelopmentLayoutSelectionFromNodeId();
                Notify();
                Notify(nameof(SelectedClassNode));
                Notify(nameof(SelectedClassSummary));
                Notify(nameof(SelectedContentSummary));
                Notify(nameof(CanAcquireClassNode));
            }
        }
    }
    public string SelectedClassDefinitionCode
    {
        get => _selectedClassDefinitionCode;
        set
        {
            if (_selectedClassDefinitionCode != value)
            {
                _selectedClassDefinitionCode = value;
                Notify();
                Notify(nameof(SelectedClassDefinition));
                Notify(nameof(SelectedClassSummary));
                Notify(nameof(SelectedContentSummary));
                Notify(nameof(CanArchiveClassDefinition));
            }
        }
    }

    public string SelectedSkillDefinitionCode
    {
        get => _selectedSkillDefinitionCode;
        set
        {
            if (_selectedSkillDefinitionCode != value)
            {
                _selectedSkillDefinitionCode = value;
                Notify();
                Notify(nameof(SelectedSkillDefinition));
                Notify(nameof(SelectedSkillSummary));
                Notify(nameof(SelectedContentSummary));
                Notify(nameof(CanArchiveSkillDefinition));
                Notify(nameof(CanAcquireSkill));
                TraceSkillDefinitionContentButtons();
            }
        }
    }

    public string SelectedSkillId
    {
        get => _selectedSkillId;
        set
        {
            if (_selectedSkillId != value)
            {
                _selectedSkillId = value;
                Notify();
                Notify(nameof(SelectedSkill));
                Notify(nameof(SelectedSkillSummary));
                Notify(nameof(SelectedContentSummary));
                Notify(nameof(CanAcquireSkill));
                Notify(nameof(CanUpdateCharacterSkillLevel));
                Notify(nameof(CanSaveCharacterSkillProfile));
                Notify(nameof(CanRemoveCharacterSkill));
                RaiseCharacterSkillCommandStates();
                if (SelectedSkill != null)
                {
                    CharacterSkillSelectedSkillIdInput = SelectedSkill.Id;
                    CharacterSkillLevelInput = SelectedSkill.Rank;
                    CharacterSkillManualBonusInput = SelectedSkill.ManualBonus;
                    CharacterSkillLevelText = SelectedSkill.Rank.ToString(CultureInfo.InvariantCulture);
                    CharacterSkillManualBonusText = SelectedSkill.ManualBonus.ToString(CultureInfo.InvariantCulture);
                    CharacterSkillTrainingStateInput = FirstNonEmpty(SelectedSkill.TrainingState, "trained");
                    CharacterSkillIsPlayerVisibleInput = SelectedSkill.IsPlayerVisible;
                }
            }
        }
    }
    public string CharacterSkillSelectedSkillIdInput
    {
        get => _characterSkillSelectedSkillIdInput;
        set
        {
            var normalized = value ?? string.Empty;
            if (_characterSkillSelectedSkillIdInput != normalized)
            {
                _characterSkillSelectedSkillIdInput = normalized;
                Notify();
                Notify(nameof(CanSaveCharacterSkillProfile));
                RaiseCharacterSkillCommandStates();
            }
        }
    }
    public int CharacterSkillLevelInput
    {
        get => _characterSkillLevelInput;
        set
        {
            var normalized = Math.Max(1, value);
            if (_characterSkillLevelInput != normalized)
            {
                _characterSkillLevelInput = normalized;
                Notify();
            }
        }
    }
    public int CharacterSkillManualBonusInput
    {
        get => _characterSkillManualBonusInput;
        set
        {
            if (_characterSkillManualBonusInput != value)
            {
                _characterSkillManualBonusInput = value;
                Notify();
            }
        }
    }
    public string CharacterSkillLevelText
    {
        get => _characterSkillLevelText;
        set
        {
            var normalized = value ?? string.Empty;
            if (_characterSkillLevelText != normalized)
            {
                _characterSkillLevelText = normalized;
                Notify();
            }
        }
    }
    public string CharacterSkillManualBonusText
    {
        get => _characterSkillManualBonusText;
        set
        {
            var normalized = value ?? string.Empty;
            if (_characterSkillManualBonusText != normalized)
            {
                _characterSkillManualBonusText = normalized;
                Notify();
            }
        }
    }
    public string CharacterSkillTrainingStateInput
    {
        get => _characterSkillTrainingStateInput;
        set
        {
            var normalized = FirstNonEmpty(value, "trained");
            if (_characterSkillTrainingStateInput != normalized)
            {
                _characterSkillTrainingStateInput = normalized;
                Notify();
            }
        }
    }
    public bool CharacterSkillIsPlayerVisibleInput
    {
        get => _characterSkillIsPlayerVisibleInput;
        set
        {
            if (_characterSkillIsPlayerVisibleInput != value)
            {
                _characterSkillIsPlayerVisibleInput = value;
                Notify();
            }
        }
    }
    public string SkillSaveStatus
    {
        get => _skillSaveStatus;
        set
        {
            if (_skillSaveStatus != value)
            {
                _skillSaveStatus = value;
                Notify();
            }
        }
    }
    public string DefinitionVersionText { get; set; } = string.Empty;

    public string SelectedRaceDefinitionCode
    {
        get => _selectedRaceDefinitionCode;
        set
        {
            _selectedRaceDefinitionCode = value;
            Notify();
            Notify(nameof(SelectedRaceDefinition));
            Notify(nameof(SelectedContentSummary));
        }
    }

    public string SelectedItemDefinitionCode
    {
        get => _selectedItemDefinitionCode;
        set
        {
            _selectedItemDefinitionCode = value;
            Notify();
            Notify(nameof(SelectedItemDefinition));
            Notify(nameof(SelectedContentSummary));
        }
    }

    public string EditClassCode { get; set; } = string.Empty;
    public string EditClassName { get; set; } = string.Empty;
    public string EditClassDescription { get; set; } = string.Empty;
    public string EditClassDirectionCode { get; set; } = string.Empty;
    public string EditClassBranchCode { get; set; } = string.Empty;
    public string EditClassRootClassCode { get; set; } = string.Empty;
    public string EditClassParentClassCode { get; set; } = string.Empty;
    public string EditClassRequiredHexagonId { get; set; } = "main_development_hexagon";
    public string EditClassRequiredNodeId { get; set; } = string.Empty;
    public string EditClassVisibilityRule { get; set; } = "hexagon-gated";
    public bool EditClassIsPlayerVisible { get; set; }
    public bool EditClassIsLockedOutsideHexagon { get; set; } = true;
    public string EditClassTags { get; set; } = string.Empty;
    public int EditClassSortOrder { get; set; }
    public int EditClassLevel { get; set; } = 1;
    public string EditClassGrantedSkillCodes { get; set; } = string.Empty;
    public string EditClassRequiredClassCodes { get; set; } = string.Empty;
    public bool EditClassIsActive { get; set; } = true;
    public string EditClassStatus { get; set; } = DefinitionStatus.Draft.ToString();
    public string EditSkillCode
    {
        get => _editSkillCode;
        set
        {
            if (_editSkillCode == value) return;
            _editSkillCode = value;
            Notify();
            Notify(nameof(CanCreateSkillDefinition));
            Notify(nameof(CanSaveSkillDefinition));
            Notify(nameof(CanArchiveSkillDefinition));
            TraceSkillDefinitionContentButtons();
        }
    }

    public string EditSkillName
    {
        get => _editSkillName;
        set
        {
            if (_editSkillName == value) return;
            _editSkillName = value;
            Notify();
            Notify(nameof(CanCreateSkillDefinition));
            Notify(nameof(CanSaveSkillDefinition));
            TraceSkillDefinitionContentButtons();
        }
    }
    public string EditSkillDescription { get; set; } = string.Empty;
    public int EditSkillTier { get; set; } = 1;
    public int EditSkillMaxLevel { get; set; } = 1;
    public string EditSkillCategory { get; set; } = SkillCategory.Undefined.ToString();
    public bool EditSkillIsClassSkill { get; set; }
    public string EditSkillRequiredClassCodes { get; set; } = string.Empty;
    public string EditSkillRequiredSkillCodes { get; set; } = string.Empty;
    public bool EditSkillIsActive { get; set; } = true;
    public string EditSkillStatus { get; set; } = DefinitionStatus.Draft.ToString();
    public string DefinitionHintText => string.IsNullOrWhiteSpace(EditClassParentClassCode) ? "Нет данных" : "Нет данных";
    public string SkillEditorHintText => SkillLevelEditorRows.Count == 0 ? "Нет данных" : $"Нет данных";
    public string ChatSessionId { get; set; } = "default";
    public string ChatMessageText { get; set; } = string.Empty;
    public string ChatMessageType { get; set; } = "Обычный";
    public ObservableCollection<string> ChatMessageTypeOptions { get; } = new ObservableCollection<string> { "Нет данных", "Нет данных", "Нет данных" };
    public string ChatModerationUserId { get; set; } = string.Empty;
    public string ChatModerationReason { get; set; } = string.Empty;
    public int ChatSlowPublicSeconds { get; set; }
    public int ChatSlowHiddenSeconds { get; set; }
    public int ChatSlowAdminOnlySeconds { get; set; }
    public string ChatUnreadText { get; set; } = string.Empty;
    public string AudioSessionId { get; set; } = "default";
    public string AudioModeInput { get; set; } = "Auto";
    public string AudioCategoryInput { get; set; } = "calm";
    public ObservableCollection<string> AudioCategoryOptions { get; } = new ObservableCollection<string> { "calm", "battle", "siege", "custom" };
    public ObservableCollection<string> AudioLoopModeOptions { get; } = new ObservableCollection<string> { "none", "track", "category" };
    public string AudioLoopModeInput { get; set; } = "none";
    public string AudioFadeSecondsInput { get; set; } = "1,8";
    public string AudioSelectedTrackId { get; set; } = string.Empty;
    public string AudioCurrentTrackTitle { get; set; } = "Аудио обновлено.";
    public string AudioCurrentCategory { get; set; } = "—";
    public string AudioPlaybackStateText { get; set; } = "—";
    public string AudioStatusText { get; set; } = "Аудио обновлено.";
    public string AudioStateText { get; set; } = string.Empty;
    public string AudioSelectedTrackTitle => SelectedAudioTrackRow != null ? SelectedAudioTrackRow.Name : "Аудио обновлено.";
    public RowVm? SelectedAudioTrackRow
    {
        get => _selectedAudioTrackRow;
        set
        {
            if (_selectedAudioTrackRow == value) return;
            _selectedAudioTrackRow = value;
            AudioSelectedTrackId = value?.Id ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value?.Category)) AudioCategoryInput = value.Category;
            Notify();
            Notify(nameof(AudioSelectedTrackId));
            Notify(nameof(AudioCategoryInput));
            Notify(nameof(AudioSelectedTrackTitle));
        }
    }
    public bool VisHideDescription { get; set; }
    public bool VisHideBackstory { get; set; }
    public bool VisHideStats { get; set; }
    public bool VisHideReputation { get; set; }
    public string NoteSessionId { get; set; } = "default";
    public string NoteTargetType { get; set; } = "character";
    public string NoteTargetId { get; set; } = string.Empty;
    public string NoteTitle { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public string NoteVisibility { get; set; } = "AdminOnly";
    public string SelectedNoteId { get; set; } = string.Empty;
    public string ReferenceWorldId { get; set; } = "default-world";
    public string ReferenceType { get; set; } = "race";
    public string ReferenceId
    {
        get => _selectedReferenceId;
        set
        {
            if (_selectedReferenceId != value)
            {
                _selectedReferenceId = value;
                var selected = ReferenceItems.FirstOrDefault(row => row.Id == value);
                if (selected != null)
                {
                    ReferenceDisplayName = selected.Name;
                    ReferenceKey = selected.Extra.StartsWith("key=", StringComparison.OrdinalIgnoreCase) ? selected.Extra.Substring(4) : ReferenceKey;
                    Notify(nameof(ReferenceDisplayName));
                    Notify(nameof(ReferenceKey));
                }
                Notify();
                Notify(nameof(SelectedReference));
                Notify(nameof(SelectedReferenceSummary));
                Notify(nameof(SelectedContentSummary));
                Notify(nameof(CanManageReferenceRecord));
            }
        }
    }
    public string ReferenceKey { get; set; } = string.Empty;
    public string ReferenceDisplayName { get; set; } = string.Empty;
    public string ReferenceDataJson { get; set; } = "{}";
    public string BackupLabel { get; set; } = string.Empty;
    public string SelectedBackupId
    {
        get => _selectedBackupId;
        set
        {
            if (_selectedBackupId != value)
            {
                _selectedBackupId = value;
                Notify();
                Notify(nameof(SelectedBackup));
                Notify(nameof(SelectedBackupSummary));
                Notify(nameof(CanManageSelectedBackup));
            }
        }
    }
    public string SelectedDiagnosticsId
    {
        get => _selectedDiagnosticsId;
        set
        {
            if (_selectedDiagnosticsId != value)
            {
                _selectedDiagnosticsId = value;
                Notify();
                Notify(nameof(SelectedDiagnostics));
                Notify(nameof(SelectedDiagnosticsSummary));
            }
        }
    }
    public string EditName { get; set; } = string.Empty;
    public string EditRace { get; set; } = string.Empty;
    public string EditHeight { get; set; } = string.Empty;
    public string EditDescription { get; set; } = string.Empty;
    public string EditBackstory
    {
        get => _editBackstory;
        set
        {
            value ??= string.Empty;
            if (_editBackstory == value) return;
            var oldLength = _editBackstory.Length;
            _editBackstory = value;
            BiographySaveStatus = "Биография сохранена.";
            ClientLogService.Instance.Info($"character.admin.biography.input.changed characterId={SelectedCharacterId} oldLength={oldLength} newLength={value.Length}");
            Notify();
        }
    }
    public int EditAge { get; set; }
    public int Health { get; set; }
    public int PhysicalArmor { get; set; }
    public int MagicalArmor { get; set; }
    public int Morale { get; set; }
    public int Strength
    {
        get => _strength;
        set
        {
            if (_strength == value) return;
            var old = _strength;
            _strength = value;
            CharacterStatsSaveStatus = $"Изменено значение: {old} -> {value}.";
            ClientLogService.Instance.Info($"character.admin.stats.input.changed stat=Strength old={old} new={value}");
            Notify();
        }
    }
    public int Dexterity { get; set; }
    public int Endurance { get; set; }
    public int Wisdom { get; set; }
    public int Intellect { get; set; }
    public int Charisma { get; set; }
    public long Iron { get; set; }
    public long Bronze { get; set; }
    public long Silver { get; set; }
    public long Gold { get; set; }
    public long Platinum { get; set; }
    public long Orichalcum { get; set; }
    public long Adamant { get; set; }
    public long Sovereign { get; set; }
    public long ExperienceCoins { get; set; }
    public string CharacterStatsSaveStatus
    {
        get => _characterStatsSaveStatus;
        set
        {
            if (_characterStatsSaveStatus == value) return;
            _characterStatsSaveStatus = value;
            Notify();
        }
    }
    private string _characterMoneySaveStatus = string.Empty;
    public string CharacterMoneySaveStatus
    {
        get => _characterMoneySaveStatus;
        set
        {
            if (_characterMoneySaveStatus == value) return;
            _characterMoneySaveStatus = value;
            Notify();
        }
    }
    public string BiographySaveStatus
    {
        get => _biographySaveStatus;
        set
        {
            if (_biographySaveStatus == value) return;
            _biographySaveStatus = value;
            Notify();
        }
    }
    public string InventoryName { get; set; } = string.Empty;
    public string InventoryDescription { get; set; } = string.Empty;
    public int InventoryQuantity { get; set; } = 1;
    public int? InventoryDurabilityOrHealth { get; set; }
    public string InventoryCondition { get; set; } = string.Empty;
    public int? InventoryAmmo { get; set; }
    public bool InventoryIsEquipped { get; set; }
    public bool InventoryIsPlayerVisible { get; set; } = true;
    public bool InventoryUsesAmmoOrConsumable { get; set; }
    public int? InventoryConsumptionPerUse { get; set; }
    public string InventoryCategory { get; set; } = string.Empty;
    public string InventorySlot { get; set; } = string.Empty;
    public string InventoryNotes { get; set; } = string.Empty;
    public ObservableCollection<CatalogDefinitionUiItem> InventoryCatalogDefinitions { get; } = new ObservableCollection<CatalogDefinitionUiItem>();
    private CatalogDefinitionUiItem? _selectedInventoryCatalogDefinition;
    public CatalogDefinitionUiItem? SelectedInventoryCatalogDefinition
    {
        get => _selectedInventoryCatalogDefinition;
        set
        {
            _selectedInventoryCatalogDefinition = value;
            InventorySelectedCatalogDefinitionSummary = value == null
                ? "Нет данных"
                : $"{value.DisplayName} ({value.Category}/{value.Code})";
            Notify(nameof(SelectedInventoryCatalogDefinition));
            Notify(nameof(InventorySelectedCatalogDefinitionSummary));
        }
    }
    public string InventoryCatalogSearch { get; set; } = string.Empty;
    public string InventoryCatalogCategoryFilter { get; set; } = "item";
    public int InventoryCatalogQuantity { get; set; } = 1;
    public bool InventoryCatalogIsEquipped { get; set; }
    public bool InventoryCatalogIsPlayerVisible { get; set; } = true;
    public string InventorySelectedCatalogDefinitionSummary { get; set; } = "Нет данных";
    private string _inventoryStatus = string.Empty;
    public string InventoryStatus
    {
        get => _inventoryStatus;
        set
        {
            if (_inventoryStatus == value) return;
            _inventoryStatus = value;
            Notify();
        }
    }
    private InventoryItemEditorVm? _selectedInventoryItem;
    public InventoryItemEditorVm? SelectedInventoryItem
    {
        get => _selectedInventoryItem;
        set
        {
            _selectedInventoryItem = value;
            if (value != null)
            {
                InventoryName = value.Name;
                InventoryDescription = value.Description;
                InventoryQuantity = value.Quantity;
                InventoryDurabilityOrHealth = value.DurabilityOrHealth;
                InventoryCondition = value.Condition;
                InventoryAmmo = value.Ammo;
                InventoryIsEquipped = value.IsEquipped;
                InventoryIsPlayerVisible = value.IsPlayerVisible;
                InventoryUsesAmmoOrConsumable = value.UsesAmmoOrConsumable;
                InventoryConsumptionPerUse = value.ConsumptionPerUse;
                InventoryCategory = value.Category;
                InventorySlot = value.Slot;
                InventoryNotes = value.Notes;
                NotifyInventoryEditor();
                ClientLogService.Instance.Info($"inventory.editor.bind selectedItem={value.Id}");
                ClientLogService.Instance.Info("inventory.editor.fields populated=true");
            }
            Notify();
        }
    }

    public ObservableCollection<RowVm> PendingAccounts { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> Players { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> Characters { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<AttributeEditorRowVm> AttributeEditorRows { get; } = new ObservableCollection<AttributeEditorRowVm>();
    public ObservableCollection<AttributeEditorRowVm> VitalsEditorRows { get; } = new ObservableCollection<AttributeEditorRowVm>();
    public ObservableCollection<AttributeEditorRowVm> DerivedStatEditorRows { get; } = new ObservableCollection<AttributeEditorRowVm>();
    public ObservableCollection<CurrencyEditorRowVm> CurrencyEditorRows { get; } = new ObservableCollection<CurrencyEditorRowVm>();
    public ObservableCollection<string> InventoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<InventoryItemEditorVm> InventoryItems { get; } = new ObservableCollection<InventoryItemEditorVm>();
    public ObservableCollection<string> HoldingsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<HoldingEditorVm> HoldingsItems { get; } = new ObservableCollection<HoldingEditorVm>();
    public string HoldingName { get; set; } = string.Empty;
    public string HoldingType { get; set; } = string.Empty;
    public string HoldingLocationName { get; set; } = string.Empty;
    public string HoldingStatus { get; set; } = string.Empty;
    public string HoldingDescription { get; set; } = string.Empty;
    public string HoldingNotes { get; set; } = string.Empty;
    public bool HoldingIsPlayerVisible { get; set; } = true;
    public bool HoldingIsArchived { get; set; }
    public string HoldingOwners { get; set; } = string.Empty;
    private HoldingEditorVm? _selectedHoldingItem;
    public HoldingEditorVm? SelectedHoldingItem
    {
        get => _selectedHoldingItem;
        set
        {
            _selectedHoldingItem = value;
            if (value != null)
            {
                HoldingName = value.Name;
                HoldingType = value.Type;
                HoldingLocationName = value.LocationName;
                HoldingStatus = value.Status;
                HoldingDescription = value.Description;
                HoldingNotes = value.Notes;
                HoldingIsPlayerVisible = value.IsPlayerVisible;
                HoldingIsArchived = value.IsArchived;
                HoldingOwners = value.OwnersDisplay;
                NotifyHoldingEditor();
                ClientLogService.Instance.Info($"holdings.editor.bind selectedHolding={value.Id}");
                ClientLogService.Instance.Info("holdings.editor.fields populated=true");
            }
            Notify();
        }
    }
    public ObservableCollection<string> ReputationRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<ReputationEditorVm> ReputationItems { get; } = new ObservableCollection<ReputationEditorVm>();
    public ObservableCollection<string> ReputationScopeTypeOptions { get; } = new ObservableCollection<string> { "Character", "Group" };
    public ObservableCollection<string> ReputationTargetTypeOptions { get; } = new ObservableCollection<string> { "State", "Settlement", "Faction", "Group", "Other" };
    public string ReputationScopeTypeInput { get; set; } = "Character";
    public string ReputationTargetTypeInput { get; set; } = "Other";
    public string ReputationTargetNameInput { get; set; } = string.Empty;
    public int ReputationValueInput { get; set; }
    public string ReputationStatusInput { get; set; } = string.Empty;
    public string ReputationNotesInput { get; set; } = string.Empty;
    public bool ReputationIsPlayerVisibleInput { get; set; } = true;
    public bool ReputationIsArchivedInput { get; set; }
    private ReputationEditorVm? _selectedReputationItem;
    public ReputationEditorVm? SelectedReputationItem
    {
        get => _selectedReputationItem;
        set
        {
            _selectedReputationItem = value;
            if (value != null)
            {
                ReputationScopeTypeInput = value.ScopeType;
                ReputationTargetTypeInput = value.TargetType;
                ReputationTargetNameInput = value.TargetName;
                ReputationValueInput = value.Value;
                ReputationStatusInput = value.Status;
                ReputationNotesInput = value.Notes;
                ReputationIsPlayerVisibleInput = value.IsPlayerVisible;
                ReputationIsArchivedInput = value.IsArchived;
                NotifyReputationEditor();
                ClientLogService.Instance.Info($"reputation.editor.bind selectedEntry={value.Id}");
                ClientLogService.Instance.Info("reputation.editor.fields populated=true");
            }
            Notify();
        }
    }
    public ObservableCollection<string> CompanionRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<CompanionEditorVm> CompanionItems { get; } = new ObservableCollection<CompanionEditorVm>();
    public string CompanionNameInput { get; set; } = string.Empty;
    public string CompanionTypeInput { get; set; } = string.Empty;
    public string CompanionDescriptionInput { get; set; } = string.Empty;
    public string CompanionNotesInput { get; set; } = string.Empty;
    public string CompanionStatusInput { get; set; } = string.Empty;
    public bool CompanionIsPlayerVisibleInput { get; set; } = true;
    public bool CompanionIsArchivedInput { get; set; }
    public string CompanionOwnerCharacterIdInput { get; set; } = string.Empty;
    public string CompanionOwnerDisplayNameInput { get; set; } = string.Empty;
    public string CompanionOwnCollectionsPreview { get; set; } = "Inventory: 0 | Holdings: 0 | Reputation: 0";
    private CompanionEditorVm? _selectedCompanionItem;
    public CompanionEditorVm? SelectedCompanionItem
    {
        get => _selectedCompanionItem;
        set
        {
            _selectedCompanionItem = value;
            if (value != null)
            {
                CompanionNameInput = value.Name;
                CompanionTypeInput = value.Type;
                CompanionDescriptionInput = value.Description;
                CompanionNotesInput = value.Notes;
                CompanionStatusInput = value.Status;
                CompanionIsPlayerVisibleInput = value.IsPlayerVisible;
                CompanionIsArchivedInput = value.IsArchived;
                CompanionOwnerCharacterIdInput = value.OwnerCharacterId;
                CompanionOwnerDisplayNameInput = value.OwnerDisplayName;
                CompanionOwnCollectionsPreview = $"Inventory: {value.OwnInventoryCount} | Holdings: {value.OwnHoldingsCount} | Reputation: {value.OwnReputationCount}";
                NotifyCompanionEditor();
                ClientLogService.Instance.Info($"companions.editor.bind selectedCompanion={value.Id}");
                ClientLogService.Instance.Info("companions.editor.fields populated=true");
            }
            Notify();
        }
    }
    public ObservableCollection<RowVm> PendingRequests { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<string> RequestHistoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> DiceFeedRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> CombatRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<RowVm> CombatParticipantRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<string> CombatHistoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<RowVm> ClassDefinitionRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> SkillDefinitionRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> RaceDefinitionRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> ItemDefinitionRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> ContentStatusRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> ContentErrorRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<SkillLevelEditorRowVm> SkillLevelEditorRows { get; } = new ObservableCollection<SkillLevelEditorRowVm>();
    public ObservableCollection<RowVm> ClassTreeItems { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<DevelopmentHexagonEditorTreeVm> DevelopmentLayoutHexagons { get; } = new ObservableCollection<DevelopmentHexagonEditorTreeVm>();
    public ObservableCollection<DevelopmentHexagonEditorNodeVm> DevelopmentLayoutNodes { get; } = new ObservableCollection<DevelopmentHexagonEditorNodeVm>();
    public ObservableCollection<DevelopmentHexagonEditorLinkVm> DevelopmentLayoutLinks { get; } = new ObservableCollection<DevelopmentHexagonEditorLinkVm>();
    public ObservableCollection<DevelopmentCanonicalRootVm> DevelopmentLayoutCanonicalRoots { get; } = new ObservableCollection<DevelopmentCanonicalRootVm>();
    public ObservableCollection<DevelopmentCanonicalDirectionVm> DevelopmentLayoutCanonicalDirections { get; } = new ObservableCollection<DevelopmentCanonicalDirectionVm>();
    public ObservableCollection<DevelopmentCanonicalLaneVm> DevelopmentLayoutCanonicalLanes { get; } = new ObservableCollection<DevelopmentCanonicalLaneVm>();
    public ObservableCollection<RowVm> SkillRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> CharacterClassRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<string> ChatRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<ChatMessageRowVm> ChatMessageRows { get; } = new ObservableCollection<ChatMessageRowVm>();
    public ObservableCollection<ChatMessageRowVm> MergedSessionFeedRows { get; } = new ObservableCollection<ChatMessageRowVm>();
    public ObservableCollection<ChatMessageRowVm> DiceMessageRows { get; } = new ObservableCollection<ChatMessageRowVm>();
    public ObservableCollection<string> ChatRestrictionRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<RowVm> AudioTrackRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<string> AudioLibraryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> NotesRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<RowVm> ReferenceItems { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> BackupItems { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> DiagnosticsItems { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> LockRows { get; } = new ObservableCollection<RowVm>();
    public IEnumerable<RowVm> FilteredCharacters => string.IsNullOrWhiteSpace(CharactersSearchText)
        ? Characters
        : Characters.Where(row => row.Name.IndexOf(CharactersSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                  || row.Id.IndexOf(CharactersSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                  || row.Extra.IndexOf(CharactersSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
    public IEnumerable<RowVm> FilteredLockRows => string.IsNullOrWhiteSpace(LocksSearchText)
        ? LockRows
        : LockRows.Where(row => row.Name.IndexOf(LocksSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                || row.Id.IndexOf(LocksSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                || row.Extra.IndexOf(LocksSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
    public IEnumerable<RowVm> FilteredClassDefinitionRows => string.IsNullOrWhiteSpace(ClassSearchText)
        ? ClassDefinitionRows
        : ClassDefinitionRows.Where(row => row.Name.IndexOf(ClassSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                           || row.Id.IndexOf(ClassSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                           || row.Extra.IndexOf(ClassSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
    public IEnumerable<RowVm> FilteredSkillDefinitionRows => string.IsNullOrWhiteSpace(SkillSearchText)
        ? SkillDefinitionRows
        : SkillDefinitionRows.Where(row => row.Name.IndexOf(SkillSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                           || row.Id.IndexOf(SkillSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                           || row.Extra.IndexOf(SkillSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
    public ObservableCollection<string> OverviewActivityRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<AdminNavigationGroup> NavigationGroups { get; } = new ObservableCollection<AdminNavigationGroup>();
    public ObservableCollection<WorkspacePanelDescriptor> WorkspacePanels { get; } = new ObservableCollection<WorkspacePanelDescriptor>();

    public WorkspacePanelDescriptor NotesPanel => GetPanelById("NotesManagement");
    public WorkspacePanelDescriptor RequestsPanel => GetPanelById("Requests");
    public WorkspacePanelDescriptor DiceFeedPanel => GetPanelById("DiceFeed");
    public WorkspacePanelDescriptor CombatTrackerPanel => GetPanelById("CombatTracker");
    public WorkspacePanelDescriptor SessionChatPanel => GetPanelById("SessionChat");
    public WorkspacePanelDescriptor SessionAudioPanel => GetPanelById("SessionAudio");

    public ICommand LoginCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenConnectionPopupCommand { get; }
    public ICommand ToggleAuthPopupCommand { get; }
    public ICommand ConnectToServerCommand { get; }
    public ICommand ApplyConnectionSettingsCommand { get; }
    public ICommand ResetConnectionDefaultsCommand { get; }
    public ICommand UseSavedConnectionSettingsCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand RejectAccountCommand { get; }
    public ICommand BlockAccountCommand { get; }
    public ICommand UnblockAccountCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    public ICommand CreateCharacterCommand { get; }
    public ICommand RollDiceCommand { get; }
    public ICommand LoadOwnerCharactersCommand { get; }
    public ICommand OpenCharacterCommand { get; }
    public ICommand OpenPlayerCharactersCommand { get; }
    public ICommand FocusSelectedCharacterCommand { get; }
    public ICommand FocusSelectedRequestCommand { get; }
    public ICommand FocusCharacterEditorCommand { get; }
    public ICommand FocusCharacterNotesCommand { get; }
    public ICommand FocusCharacterVisibilityCommand { get; }
    public ICommand RefreshSelectedCharacterCommand { get; }
    public ICommand RefreshPeopleSectionCommand { get; }
    public ICommand RefreshModerationSectionCommand { get; }
    public ICommand RefreshSessionSectionCommand { get; }
    public ICommand RefreshContentSectionCommand { get; }
    public ICommand RefreshSystemSectionCommand { get; }
    public ICommand AcquireLockCommand { get; }
    public ICommand ReleaseLockCommand { get; }
    public ICommand ForceUnlockCommand { get; }
    public ICommand SaveBasicInfoCommand { get; }
    public ICommand SaveBiographyCommand { get; }
    public ICommand SaveStatsCommand { get; }
    public ICommand SaveMoneyCommand { get; }
    public ICommand SaveXpCoinsCommand { get; }
    public ICommand InventoryReloadCommand { get; }
    public ICommand InventoryAddItemCommand { get; }
    public ICommand InventoryLoadCatalogCommand { get; }
    public ICommand InventoryAddFromCatalogCommand { get; }
    public ICommand InventoryUpdateItemCommand { get; }
    public ICommand InventoryRemoveItemCommand { get; }
    public ICommand InventoryToggleEquipCommand { get; }
    public ICommand HoldingsReloadCommand { get; }
    public ICommand HoldingAddCommand { get; }
    public ICommand HoldingUpdateCommand { get; }
    public ICommand HoldingRemoveCommand { get; }
    public ICommand ReputationReloadCommand { get; }
    public ICommand ReputationAddCommand { get; }
    public ICommand ReputationUpdateCommand { get; }
    public ICommand ReputationRemoveCommand { get; }
    public ICommand CompanionsReloadCommand { get; }
    public ICommand CompanionAddCommand { get; }
    public ICommand CompanionUpdateCommand { get; }
    public ICommand CompanionRemoveCommand { get; }
    public ICommand OwnershipSaveOwnerCommand { get; }
    public ICommand OwnershipSaveKindStatusCommand { get; }
    public ICommand OwnershipAssignGroupCommand { get; }
    public ICommand OwnershipArchiveCommand { get; }
    public ICommand OwnershipUnarchiveCommand { get; }
    public ICommand ApproveRequestCommand { get; }
    public ICommand RejectRequestCommand { get; }
    public ICommand MarkInReviewRequestCommand { get; }
    public ICommand RequestChangesCommand { get; }
    public ICommand ArchiveRequestCommand { get; }
    public ICommand RefreshRequestsCommand { get; }
    public ICommand CombatStartCommand { get; }
    public ICommand CombatEndCommand { get; }
    public ICommand CombatRefreshCommand { get; }
    public ICommand CombatNextTurnCommand { get; }
    public ICommand CombatPrevTurnCommand { get; }
    public ICommand CombatNextRoundCommand { get; }
    public ICommand CombatSkipTurnCommand { get; }
    public ICommand CombatAddParticipantCommand { get; }
    public ICommand CombatRemoveParticipantCommand { get; }
    public ICommand CombatDetachCompanionCommand { get; }
    public ICommand DefinitionsReloadCommand { get; }
    public ICommand RefreshDefinitionClassesCommand { get; }
    public ICommand NewClassDefinitionCommand { get; }
    public ICommand OpenSelectedClassDefinitionCommand { get; }
    public ICommand SaveClassDefinitionCommand { get; }
    public ICommand ArchiveClassDefinitionCommand { get; }
    public ICommand RefreshDefinitionSkillsCommand { get; }
    public ICommand RefreshDefinitionRacesCommand { get; }
    public ICommand RefreshDefinitionItemsCommand { get; }
    public ICommand RefreshContentStatusCommand { get; }
    public ICommand AssignCharacterClassCommand { get; }
    public ICommand NewSkillDefinitionCommand { get; }
    public ICommand OpenSelectedSkillDefinitionCommand { get; }
    public ICommand SaveSkillDefinitionCommand { get; }
    public ICommand ArchiveSkillDefinitionCommand { get; }
    public ICommand AddSkillLevelCommand { get; }
    public ICommand RemoveSkillLevelCommand { get; }
    public ICommand LoadClassTreeCommand { get; }
    public ICommand SelectDevelopmentLayoutHexagonCommand { get; }
    public ICommand AcquireClassNodeCommand { get; }
    public ICommand RevokeClassNodeCommand { get; }
    public ICommand SelectClassNodeCommand { get; }
    public ICommand SaveClassNodeLayoutCommand { get; }
    public ICommand SaveDevelopmentHexagonLayoutCommand { get; }
    public ICommand CancelDevelopmentHexagonLayoutCommand { get; }
    public ICommand ResetDevelopmentHexagonLayoutCommand { get; }
    public ICommand ValidateDevelopmentHexagonLayoutCommand { get; }
    public ICommand PreviewBaselineDevelopmentHexagonLayoutCommand { get; }
    public ICommand ApplyBaselineDevelopmentHexagonLayoutCommand { get; }
    public ICommand CreateDevelopmentLayoutSnapshotCommand { get; }
    public ICommand RestoreDevelopmentLayoutSnapshotCommand { get; }
    public ICommand GetDevelopmentLayoutQualityReportCommand { get; }
    public ICommand LockSelectedDevelopmentLayoutNodeCommand { get; }
    public ICommand UnlockSelectedDevelopmentLayoutNodeCommand { get; }
    public ICommand CreateDevelopmentNodeCommand { get; }
    public ICommand ArchiveDevelopmentNodeCommand { get; }
    public ICommand RestoreDevelopmentNodeCommand { get; }
    public ICommand SaveDevelopmentNodeCommand { get; }
    public ICommand CancelDevelopmentNodeEditCommand { get; }
    public ICommand AddRequirementLinkCommand { get; }
    public ICommand RemoveRequirementLinkCommand { get; }
    public ICommand ValidateDevelopmentGraphCommand { get; }
    public ICommand ToggleDevelopmentLinkModeCommand { get; }
    public ICommand ZoomInDevelopmentHexagonLayoutCommand { get; }
    public ICommand ZoomOutDevelopmentHexagonLayoutCommand { get; }
    public ICommand ResetViewDevelopmentHexagonLayoutCommand { get; }
    public ICommand FitToViewDevelopmentHexagonLayoutCommand { get; }
    public ICommand SearchDevelopmentHexagonLayoutClearCommand { get; }
    public ICommand SearchDevelopmentHexagonLayoutNextCommand { get; }
    public ICommand SearchDevelopmentHexagonLayoutPreviousCommand { get; }
    public ICommand ClearDevelopmentHexagonLayoutFiltersCommand { get; }
    public ICommand UndoDevelopmentHexagonLayoutCommand { get; }
    public ICommand RedoDevelopmentHexagonLayoutCommand { get; }
    public ICommand SaveAllDevelopmentHexagonChangesCommand { get; }
    public ICommand DiscardAllDevelopmentHexagonChangesCommand { get; }
    public ICommand ShowAllDevelopmentLayoutLinksCommand { get; }
    public ICommand FocusDevelopmentHexagonValidationAffectedCommand { get; }
    public ICommand CompleteClassNodeCommand { get; }
    public ICommand LoadSkillsCommand { get; }
    public ICommand AcquireSkillCommand { get; }
    public ICommand UpdateSkillLevelCommand { get; }
    public ICommand RemoveSkillCommand { get; }
    public ICommand ChatSendCommand { get; }
    public ICommand ChatRefreshCommand { get; }
    public ICommand ChatMuteUserCommand { get; }
    public ICommand ChatUnmuteUserCommand { get; }
    public ICommand ChatLockPlayersCommand { get; }
    public ICommand ChatUnlockPlayersCommand { get; }
    public ICommand ChatSetSlowModeCommand { get; }
    public ICommand AudioRefreshCommand { get; }
    public ICommand AudioSetModeCommand { get; }
    public ICommand AudioClearOverrideCommand { get; }
    public ICommand AudioNextTrackCommand { get; }
    public ICommand AudioSelectTrackCommand { get; }
    public ICommand AudioReloadLibraryCommand { get; }
    public ICommand AudioPauseCommand { get; }
    public ICommand AudioStopCommand { get; }
    public ICommand AudioResyncCommand { get; }
    public ICommand VisibilityLoadCommand { get; }
    public ICommand VisibilitySaveCommand { get; }
    public ICommand NotesRefreshCommand { get; }
    public ICommand NotesCreateCommand { get; }
    public ICommand NotesArchiveCommand { get; }
    public ICommand ReferenceRefreshCommand { get; }
    public ICommand ReferenceCreateCommand { get; }
    public ICommand ReferenceUpdateCommand { get; }
    public ICommand ReferenceArchiveCommand { get; }
    public ICommand BackupRefreshCommand { get; }
    public ICommand BackupCreateCommand { get; }
    public ICommand BackupRestoreCommand { get; }
    public ICommand BackupExportCommand { get; }
    public ICommand DiagnosticsRefreshCommand { get; }
    public ICommand FocusContentClassesCommand { get; }
    public ICommand FocusContentReferenceCommand { get; }
    public ICommand FocusSystemReferenceCommand { get; }
    public ICommand FocusSystemBackupsCommand { get; }
    public ICommand FocusSystemDiagnosticsCommand { get; }
    public ICommand SelectSectionCommand { get; }
    public ICommand SelectNavigationItemCommand { get; }
    public ICommand OpenFirstShellSearchResultCommand { get; }
    public ICommand GlobalSearchCommand { get; }
    public ICommand GlobalSearchOpenCommand { get; }
    public ICommand DetachWorkspacePanelCommand { get; }
    public ICommand AttachWorkspacePanelCommand { get; }
    public ICommand ToggleWorkspacePanelVisibilityCommand { get; }
    public ICommand ShowWorkspacePanelCommand { get; }
    public ICommand HideWorkspacePanelCommand { get; }

    public void LoadConnectionSettings()
    {
        var settings = ReadJson(ConnectionSettingsPath, new ConnectionSettingsModel());
        LastServerHost = settings.LastServerHost;
        LastServerPort = settings.LastServerPort;
        ServerHostInput = settings.ServerHost;
        ServerPortInput = settings.ServerPort.ToString();

        if (TryValidateConnectionSettings(ServerHostInput, ServerPortInput, out var host, out var port, out _))
        {
            _client.UpdateEndpoint(host, port);
        }

        RefreshConnectionSummary();
    }

    public void SaveConnectionSettings()
    {
        int.TryParse(ServerPortInput, out var currentPort);
        WriteJson(ConnectionSettingsPath, new ConnectionSettingsModel
        {
            ServerHost = ServerHostInput,
            ServerPort = currentPort <= 0 ? _client.ServerPort : currentPort,
            LastServerHost = LastServerHost,
            LastServerPort = LastServerPort
        });
    }

    public bool TryValidateConnectionSettings(string hostInput, string portInput, out string normalizedHost, out int port, out string error)
    {
        normalizedHost = hostInput.Trim();
        error = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(normalizedHost))
        {
            error = "Укажите host.";
            return false;
        }

        if (!string.Equals(normalizedHost, "localhost", StringComparison.OrdinalIgnoreCase)
            && !(IPAddress.TryParse(normalizedHost, out var address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
        {
            error = "Укажите localhost или IPv4-адрес.";
            return false;
        }

        if (!int.TryParse(portInput, out port) || port < 1 || port > 65535)
        {
            error = "Порт должен быть в диапазоне 1..65535.";
            return false;
        }

        return true;
    }

    public void ApplyConnectionSettings()
    {
        if (!TryValidateConnectionSettings(ServerHostInput, ServerPortInput, out var host, out var port, out var error))
        {
            SetConnectionError(error);
            return;
        }

        _client.UpdateEndpoint(host, port);
        LastServerHost = host;
        LastServerPort = port;
        SaveConnectionSettings();
        SetDisconnectedState($"Подключение: {host}:{port}");
        RefreshConnectionSummary();
    }

    public void ConnectToServer()
    {
        if (!TryValidateConnectionSettings(ServerHostInput, ServerPortInput, out var host, out var port, out var error))
        {
            SetConnectionError(error);
            return;
        }

        try
        {
            _client.UpdateEndpoint(host, port);
            _client.Connect();
            ClientLogService.Instance.Info($"Server connection established: {host}:{port}");
            LastServerHost = host;
            LastServerPort = port;
            SaveConnectionSettings();
            IsAuthenticated = false;
            SetConnectedState($"Подключено: {host}:{port}");
            IsConnectionPopupOpen = false;
            IsAuthPopupOpen = false;
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error($"Server connection failed: {host}:{port}", ex);
            SetConnectionError($"Не удалось подключиться к {host}:{port}. {ex.Message}");
        }
    }

    public void ResetConnectionDefaults()
    {
        ServerHostInput = "127.0.0.1";
        ServerPortInput = "4600";
        SetDisconnectedState("Нет данных");
    }

    public void UseSavedConnectionSettings()
    {
        ServerHostInput = LastServerHost;
        ServerPortInput = LastServerPort.ToString();
        SetDisconnectedState($"Подключение сброшено: {LastServerHost}:{LastServerPort}");
    }

    public void SetConnectedState(string detail)
    {
        IsConnectedToServer = true;
        IsOnline = true;
        LastErrorMessage = string.Empty;
        LastStatusMessage = detail;
        ConnectionState = IsAuthenticated ? "Онлайн / Admin" : "Подключено";
        ConnectionStatusDetail = detail;
        RefreshConnectionSummary();
    }

    public void SetDisconnectedState(string detail)
    {
        IsConnectedToServer = false;
        IsAuthenticated = false;
        IsOnline = false;
        LastStatusMessage = detail;
        ConnectionState = "Оффлайн";
        ConnectionStatusDetail = detail;
        RefreshConnectionSummary();
    }

    public void SetConnectionError(string detail)
    {
        _client.Disconnect();
        _poller.Stop();
        LastErrorMessage = detail;
        SetDisconnectedState(detail);
    }

    private void HandleUiException(string context, Exception ex)
    {
        ClientLogService.Instance.Error($"ui.error context={context}", ex);

        if (LooksLikeUnauthorized(ex.Message))
        {
            SetConnectionError("Нет данных");
            return;
        }

        if (IsNetworkException(ex))
        {
            SetConnectionError($"Нет данных");
            return;
        }

        LastErrorMessage = ex.Message;
        LastStatusMessage = context + " — ошибка";
        RefreshConnectionSummary();
    }

    private static bool IsNetworkException(Exception ex)
    {
        if (ex is SocketException || ex is IOException || ex is TimeoutException)
        {
            return true;
        }

        var inner = ex.InnerException;
        return inner is SocketException || inner is IOException || inner is TimeoutException;
    }

    private static bool LooksLikeUnauthorized(string? message)
        => !string.IsNullOrWhiteSpace(message)
           && (message.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("auth token is invalid", StringComparison.OrdinalIgnoreCase) >= 0);

    public void RefreshConnectionSummary()
    {
        SessionSummary = $"Сервер подключён, выполните вход";
        RefreshOverviewActivity();
        Notify(nameof(CurrentEndpoint));
        Notify(nameof(LoginSummary));
        Notify(nameof(PendingAccountsCount));
        Notify(nameof(PlayersCount));
        Notify(nameof(CharactersCount));
        Notify(nameof(PendingRequestsCount));
        Notify(nameof(ActivePlayersCount));
        Notify(nameof(HasActiveCombat));
        Notify(nameof(ChatSummary));
        Notify(nameof(AudioSummary));
        Notify(nameof(DiagnosticsSummary));
        Notify(nameof(SessionStateSummary));
        Notify(nameof(ActiveSessionTopBarSummary));
        Notify(nameof(NotificationSummary));
        Notify(nameof(ActiveCombatParticipantsCount));
        Notify(nameof(CombatTrackerSummary));
        Notify(nameof(IsSessionActive));
        Notify(nameof(CombatOpponentsCount));
        Notify(nameof(SelectedCombatParticipant));
        Notify(nameof(SelectedCombatParticipantSummary));
        Notify(nameof(SessionAttentionSummary));
        Notify(nameof(ChatActivitySummary));
        Notify(nameof(AudioTrackSummary));
        Notify(nameof(CanManageCombatSelection));
        Notify(nameof(CanControlCombat));
        Notify(nameof(CanSendChat));
        Notify(nameof(CanControlAudio));
        Notify(nameof(ContentSummary));
        Notify(nameof(ContentReadinessSummary));
        Notify(nameof(SelectedClassNode));
        Notify(nameof(SelectedSkill));
        Notify(nameof(SelectedReference));
        Notify(nameof(SelectedBackup));
        Notify(nameof(SelectedDiagnostics));
        Notify(nameof(SelectedClassSummary));
        Notify(nameof(SelectedSkillSummary));
        Notify(nameof(SelectedReferenceSummary));
        Notify(nameof(SelectedContentSummary));
        Notify(nameof(ReferenceSummary));
        Notify(nameof(BackupSummary));
        Notify(nameof(DiagnosticsStatusSummary));
        Notify(nameof(SelectedBackupSummary));
        Notify(nameof(SelectedDiagnosticsSummary));
        Notify(nameof(SystemHealthSummary));
        Notify(nameof(WorkspaceSummary));
        Notify(nameof(SelectedPendingAccount));
        Notify(nameof(SelectedPlayer));
        Notify(nameof(SelectedCharacter));
        Notify(nameof(SelectedRequest));
        Notify(nameof(SelectedPendingAccountSummary));
        Notify(nameof(SelectedPlayerSummary));
        Notify(nameof(SelectedCharacterSummary));
        Notify(nameof(SelectedRequestSummary));
        Notify(nameof(CharacterActionSummary));
        Notify(nameof(ChatModerationSummary));
        Notify(nameof(SystemActionSummary));
        Notify(nameof(SelectedLock));
        Notify(nameof(SelectedLockSummary));
        Notify(nameof(HeaderStatusSummary));
        Notify(nameof(CanManagePendingAccount));
        Notify(nameof(CanLoadPlayerCharacters));
        Notify(nameof(CanOpenSelectedCharacter));
        Notify(nameof(CanModerateSelectedRequest));
        Notify(nameof(CanManageSelectedLock));
        Notify(nameof(CanManageSelectedCharacter));
        Notify(nameof(CanManageCharacterVisibility));
        Notify(nameof(CanAcquireSkill));
        Notify(nameof(CanUpdateCharacterSkillLevel));
        Notify(nameof(CanRemoveCharacterSkill));
        Notify(nameof(CanRefreshNotes));
        Notify(nameof(CanCreateNote));
        Notify(nameof(CanArchiveNote));
        Notify(nameof(CanModerateChatUser));
        Notify(nameof(CanManageChatControls));
        Notify(nameof(CanManageWorkspace));
        Notify(nameof(CanInitiateConnection));
        Notify(nameof(CanControlContent));
        Notify(nameof(CanCreateSkillDefinition));
        Notify(nameof(CanRefreshSkillDefinitions));
        Notify(nameof(CanSaveSkillDefinition));
        Notify(nameof(CanArchiveSkillDefinition));
        Notify(nameof(CanRefreshContent));
        Notify(nameof(CanAcquireClassNode));
        Notify(nameof(CanAcquireSkill));
        Notify(nameof(CanManageReferenceRecord));
        Notify(nameof(CanManageSelectedBackup));
        Notify(nameof(CanRefreshSystem));
        TraceSkillDefinitionContentButtons();
    }

    public void LoadWorkspaceLayout()
    {
        var layout = ReadJson(WorkspaceLayoutPath, new WorkspaceLayoutModel());
        foreach (var item in layout.Panels)
        {
            var panel = WorkspacePanels.FirstOrDefault(p => p.PanelId == item.PanelId);
            if (panel == null)
            {
                continue;
            }

            panel.IsDetached = item.IsDetached && panel.CanDetach;
            panel.IsVisible = item.IsVisible;
            panel.WindowLeft = item.Left;
            panel.WindowTop = item.Top;
            panel.WindowWidth = item.Width > 200 ? item.Width : 920;
            panel.WindowHeight = item.Height > 200 ? item.Height : 720;
        }
    }

    public void SaveWorkspaceLayout()
    {
        var layout = new WorkspaceLayoutModel
        {
            Panels = WorkspacePanels.Select(panel => new WorkspacePanelLayoutItem
            {
                PanelId = panel.PanelId,
                IsDetached = panel.IsDetached,
                IsVisible = panel.IsVisible,
                Left = panel.WindowLeft,
                Top = panel.WindowTop,
                Width = panel.WindowWidth,
                Height = panel.WindowHeight
            }).ToList()
        };

        WriteJson(WorkspaceLayoutPath, layout);
    }

    public WorkspacePanelDescriptor GetPanelById(string panelId) => WorkspacePanels.First(panel => panel.PanelId == panelId);

    public void UpdatePanelWindowBounds(string panelId, double left, double top, double width, double height)
    {
        var panel = GetPanelById(panelId);
        panel.WindowLeft = left;
        panel.WindowTop = top;
        panel.WindowWidth = width;
        panel.WindowHeight = height;
        SaveWorkspaceLayout();
    }

    private void InitializeNavigationGroups()
    {
        NavigationGroups.Clear();
        NavigationGroups.Add(new AdminNavigationGroup("administration", "Администрирование", new[]
        {
            Nav("admin.dashboard", "Панель Гейм-мастера", "D", "administration", "admin.dashboard", false, 5, "GM-пульт кампании."),
            Nav("admin.users", "Пользователи", "U", "administration", "admin.users", false, 20, "Игроки, доступы и учётные записи."),
            Nav("admin.characters", "Персонажи", "C", "administration", "admin.characters", false, 30, "Карточки персонажей и Character v2 редактор GM."),
            Nav("admin.definitions", "Справочники", "R", "administration", "definitions.browser", false, 40, "Правила, определения и игровые справочники."),
            Nav("admin.items", "Предметы / экипировка", "I", "administration", "admin.items", false, 50, "Каталог предметов и экипировки."),
            Nav("admin.classes", "Классы / навыки", "K", "administration", "admin.classes", false, 60, "Классы, навыки и развитие."),
            Nav("admin.races", "Расы / языки", "A", "administration", "admin.races", false, 70, "Расы, языки и связанные справочники."),
            Nav("admin.world", "Мир / страны / локации", "W", "administration", "admin.world", false, 80, "Мир, страны, регионы и карта мира."),
            Nav("admin.factions", "Фракции / организации", "F", "administration", "admin.factions", false, 90, "Фракции, организации и отношения мира."),
            Nav("admin.economy", "Экономика / рынки", "E", "administration", "admin.economy", false, 100, "Экономика, рынки и торговые данные."),
            Nav("admin.chronicle", "Хроника / события", "H", "administration", "admin.chronicle", false, 110, "Хроника мира, события и GM-заметки.")
        }));
        NavigationGroups.Add(new AdminNavigationGroup("conduct", "Проведение", new[]
        {
            Nav("conduct.session", "Текущая сессия", "S", "conduct", "session.overview", false, 10, "Состояние текущей сессии."),
            Nav("conduct.party", "Активная группа", "G", "conduct", "session.group", false, 20, "Группа персонажей текущей сессии."),
            Nav("conduct.combat", "Бой", "B", "conduct", "combat.readonly", false, 30, "Combat v1: snapshot, участники, ходы и GM controls."),
            Nav("conduct.chat", "Чат / кубики", "Q", "conduct", "session.chat", false, 40, "Чат сессии и лента бросков."),
            Nav("conduct.fate", "Fate Engine", "F", "conduct", "fate.engine", false, 50, "Автоматизированные проверки и судьба сцены."),
            Nav("conduct.scene_map", "Карта сцены", "M", "conduct", "scene.map", false, 60, "Scene Map MVP."),
            Nav("conduct.event_log", "Журнал событий", "J", "conduct", "event.log", false, 70, "Журнал событий кампании."),
            Nav("conduct.requests", "Заявки игроков", "!", "conduct", "requests.formal", false, 80, "Заявки и действия игроков."),
            Nav("conduct.quick_notes", "Заметки GM", "N", "conduct", "quick.notes", false, 90, "Быстрые заметки GM по сессии."),
            Nav("conduct.replay", "Повтор боя", "R", "conduct", "placeholder.combat_replay", true, 100, "Контролируемый placeholder повтора боя."),
            Nav("conduct.audio", "Музыка / звуки", "A", "conduct", "placeholder.audio", false, 110, "Музыка, звуки и аудио сцены.")
        }));
        NavigationGroups.Add(new AdminNavigationGroup("system", "Система", new[]
        {
            Nav("system.settings", "Настройки", "S", "system", "system.settings", false, 10, "Настройки клиента и системы."),
            Nav("system.global_search", "Глобальный поиск", "G", "system", "global.search", false, 15, "Глобальный поиск по доступным данным."),
            Nav("system.tools", "Функции и модули", "F", "system", "system.tools", false, 20, "Feature Flags и системные модули."),
            Nav("system.diagnostics", "Диагностика", "D", "system", "system.tools", false, 30, "Обзор системных инструментов, состояния клиента и безопасного режима."),
            Nav("system.logs", "Логи", "L", "system", "system.tools", false, 40, "Логи и диагностические события."),
            Nav("system.backups", "Резервные копии", "B", "system", "system.tools", false, 50, "Backup / Restore."),
            Nav("system.data", "Импорт / экспорт", "I", "system", "system.tools", false, 60, "Импорт и экспорт данных."),
            Nav("system.server", "Сервер / мониторинг", "C", "system", "system.tools", false, 70, "Мониторинг сервера."),
            Nav("system.definition_check", "Проверка справочников", "V", "system", "system.tools", false, 80, "Проверка справочников."),
            Nav("system.inventory_diagnostics", "Диагностика инвентаря", "N", "system", "system.tools", false, 90, "Диагностика инвентаря."),
            Nav("system.locks", "Активные блокировки", "K", "system", "locks.active", false, 100, "Активные блокировки."),
            Nav("system.smoke", "Проверки", "T", "system", "system.tools", false, 110, "Smoke и regression проверки.")
        }));

        ApplyShellNavigationSearch();
        SetSelectedNavigationItem(NavigationGroups.SelectMany(group => group.Items).First());
    }
    private static AdminNavigationItem Nav(string id, string title, string icon, string groupId, string targetViewKey, bool isPlaceholder, int sortOrder, string description)
        => new AdminNavigationItem(id, title, icon, groupId, targetViewKey, isPlaceholder, sortOrder, description);

    private void InitializeWorkspacePanels()
    {
        WorkspacePanels.Add(new WorkspacePanelDescriptor("NotesManagement", "Заметки GM", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("Requests", "Нет данных", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("DiceFeed", "Лента бросков", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("CombatTracker", "Трекер боя", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("CombatReadOnly", "Бой Combat v1", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("DefinitionsBrowser", "Нет данных", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("SessionChat", "Чат сессии", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("SessionAudio", "Музыка сессии", canDetach: true));
    }
    private void SelectSection(string? section)
    {
        if (!string.IsNullOrWhiteSpace(section))
        {
            SelectedSection = section;
        }
    }

    private static string NormalizeMainSection(string? section)
        => MainSectionOrder.Contains(section ?? string.Empty)
            ? section!
            : "admin.dashboard";

    private void SyncMainSectionIndex(string section)
    {
        var index = Array.IndexOf(MainSectionOrder, section);
        if (index >= 0 && _selectedSectionIndex != index)
        {
            _selectedSectionIndex = index;
            Notify(nameof(SelectedSectionIndex));
        }
    }

    private void SelectNavigationItem(AdminNavigationItem? item)
    {
        if (item == null || !item.IsEnabled) return;
        SetSelectedNavigationItem(item);
    }

    private void OpenFirstShellSearchResult()
    {
        var item = NavigationGroups
            .SelectMany(group => group.Items)
            .Where(candidate => candidate.IsEnabled && candidate.IsSearchVisible)
            .OrderBy(candidate => candidate.SortOrder)
            .FirstOrDefault();

        if (item == null)
        {
            StatusMessage = "Действие выполнено.";
            return;
        }

        SetSelectedNavigationItem(item);
    }

    private void RunGlobalSearch()
    {
        GlobalSearchResults.Clear();
        SelectedGlobalSearchResult = null;

        var payload = new Dictionary<string, object>
        {
            { "query", GlobalSearchQuery },
            { "limit", 50 },
            { "offset", 0 },
            { "includeArchived", GlobalSearchIncludeArchived },
            { "includeHidden", GlobalSearchIncludeHidden }
        };

        if (!string.Equals(GlobalSearchCategoryFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            payload["categories"] = new object[] { GlobalSearchCategoryFilter };
        }

        var response = _api.SearchAdminQuery(payload);
        if (response.Status != ResponseStatus.Ok)
        {
            GlobalSearchStatusText = string.IsNullOrWhiteSpace(response.Message)
                ? "Глобальный поиск недоступен."
                : response.Message;
            ClientLogService.Instance.Warn($"admin.global_search.query.failed status={response.Status} message={response.Message}");
            return;
        }

        var items = response.Payload.TryGetValue("items", out var rawItems)
            ? ToList(rawItems)
            : new ArrayList();

        foreach (var item in items)
        {
            var map = AsMap(item, CommandNames.SearchAdminQuery);
            if (map == null) continue;
            GlobalSearchResults.Add(MapGlobalSearchResult(map));
        }

        SelectedGlobalSearchResult = GlobalSearchResults.FirstOrDefault();
        var total = response.Payload.TryGetValue("total", out var rawTotal)
            ? Convert.ToString(rawTotal)
            : GlobalSearchResults.Count.ToString(CultureInfo.InvariantCulture);
        GlobalSearchStatusText = $"Найдено: {total}. Показано: {GlobalSearchResults.Count}.";
        ClientLogService.Instance.Info($"admin.global_search.query.done count={GlobalSearchResults.Count} total={total}");
    }

    private void OpenGlobalSearchResult()
    {
        var selected = SelectedGlobalSearchResult;
        if (selected == null)
        {
            GlobalSearchStatusText = "Выберите результат поиска.";
            return;
        }

        var response = _api.SearchAdminOpenTarget(new Dictionary<string, object>
        {
            { "routeKey", selected.RouteKey },
            { "entityId", selected.EntityId }
        });

        if (response.Status != ResponseStatus.Ok)
        {
            GlobalSearchStatusText = string.IsNullOrWhiteSpace(response.Message)
                ? "Не удалось открыть результат."
                : response.Message;
            ClientLogService.Instance.Warn($"admin.global_search.open.failed route={selected.RouteKey} entityId={selected.EntityId} status={response.Status}");
            return;
        }

        ApplyGlobalSearchRoute(selected);
        GlobalSearchStatusText = $"Открыт результат: {selected.DisplayTitle}";
        ClientLogService.Instance.Info($"admin.global_search.open.done route={selected.RouteKey} entityId={selected.EntityId}");
    }

    private void ApplyGlobalSearchRoute(GlobalSearchResultVm result)
    {
        switch (result.RouteKey)
        {
            case "character.details":
                SelectedSection = "admin.characters";
                if (!string.IsNullOrWhiteSpace(result.EntityId))
                {
                    SelectedCharacterId = result.EntityId;
                }
                break;
            case "playerRequest.details":
                SelectedSection = "requests.formal";
                if (!string.IsNullOrWhiteSpace(result.EntityId))
                {
                    SelectedPendingRequestId = result.EntityId;
                }
                break;
            case "gmNote.details":
                SelectedSection = "quick.notes";
                break;
            case "eventJournal.details":
                SelectedSection = "event.log";
                break;
            case "worldCalendarEvent.details":
            case "realScheduleEvent.details":
                SelectedSection = "admin.chronicle";
                break;
            case "worldMap.details":
            case "worldMap.region":
            case "worldMap.location":
            case "worldMap.label":
                SelectedSection = "admin.world";
                WorldMap.RefreshFlags();
                if (WorldMap.IsWorldMapEnabled)
                    WorldMap.RefreshMaps();
                break;
            case "backup.details":
                SelectedSection = "system.tools";
                SystemTools.SelectTab("backups");
                break;
            case "definition.details":
                SelectedSection = "definitions.browser";
                break;
            default:
                SelectedSection = "global.search";
                break;
        }
    }

    private static GlobalSearchResultVm MapGlobalSearchResult(Dictionary<string, object> map)
        => new GlobalSearchResultVm
        {
            ResultId = S(map, "resultId"),
            EntityType = S(map, "entityType"),
            EntityId = S(map, "entityId"),
            SourceCollection = S(map, "sourceCollection"),
            Title = S(map, "title"),
            Snippet = S(map, "snippet"),
            Category = S(map, "category"),
            RouteKey = S(map, "routeKey"),
            Visibility = S(map, "visibility"),
            Score = S(map, "score")
        };

    private void SetSelectedNavigationItem(AdminNavigationItem item)
    {
        foreach (var navigationItem in NavigationGroups.SelectMany(group => group.Items))
        {
            navigationItem.IsSelected = navigationItem.Id == item.Id;
        }

        SelectedNavigationItemId = item.Id;
        SelectedSection = item.TargetViewKey;
        ApplyNavigationSideEffects(item.Id);
        ClientLogService.Instance.Info($"ui.navigation.selected item={item.Id} target={item.TargetViewKey}");
    }

    private void SyncSelectedNavigationItemForSection(string? section)
    {
        if (string.IsNullOrWhiteSpace(section) || NavigationGroups.Count == 0) return;
        var current = SelectedNavigationItem;
        if (current != null && current.TargetViewKey == section) return;
        var matchingItem = NavigationGroups
            .SelectMany(group => group.Items)
            .FirstOrDefault(item => item.TargetViewKey == section && !item.IsPlaceholder);
        if (matchingItem == null) return;

        foreach (var navigationItem in NavigationGroups.SelectMany(group => group.Items))
        {
            navigationItem.IsSelected = navigationItem.Id == matchingItem.Id;
        }

        SelectedNavigationItemId = matchingItem.Id;
        ApplyNavigationSideEffects(matchingItem.Id);
        ClientLogService.Instance.Info($"ui.navigation.synced section={section} item={matchingItem.Id}");
    }

    private void ApplyShellNavigationSearch()
    {
        var search = (ShellSearchText ?? string.Empty).Trim();
        foreach (var item in NavigationGroups.SelectMany(group => group.Items))
        {
            item.IsSearchVisible = string.IsNullOrWhiteSpace(search)
                || Contains(item.Title, search)
                || Contains(item.Description, search)
                || Contains(item.GroupId, search)
                || Contains(item.TargetViewKey, search);
        }
    }

    private void ApplyNavigationSideEffects(string itemId)
    {
        if (itemId == "conduct.chat") RefreshDiceFeedForChat();
        else if (itemId == "admin.characters") LoadAllCharactersForAdminSection();
        else if (itemId == "conduct.fate") FateControl.Refresh();
        else if (itemId == "conduct.scene_map") { if (SceneMap.IsSceneMapDisabled) SceneMap.RefreshFlags(); }
        else if (itemId == "conduct.session") { CurrentSession.RefreshFlags(); }
        else if (itemId == "conduct.party") { CharacterGroups.RefreshFlags(); }
        else if (itemId == "admin.chronicle") { WorldCalendar.RefreshFlags(); RealSchedule.RefreshFlags(); }
        else if (itemId == "conduct.event_log") { EventJournal.RefreshFlags(); }
        else if (itemId == "conduct.quick_notes") { GMNotes.RefreshFlags(); }
        else if (itemId == "admin.world")
        {
            WorldMap.RefreshFlags();
            if (WorldMap.IsWorldMapEnabled)
                WorldMap.RefreshMaps();
            if (RoomInterior.IsRoomDisabled)
                RoomInterior.RefreshFlags();
        }
        else if (itemId == "system.tools") SystemTools.SelectTab("flags");
        else if (itemId == "system.diagnostics") SystemTools.SelectTab("diagnostics");
        else if (itemId == "system.logs") SystemTools.SelectTab("logs");
        else if (itemId == "system.backups") SystemTools.SelectTab("backups");
        else if (itemId == "system.data") SystemTools.SelectTab("data");
        else if (itemId == "system.server") { SystemTools.SelectTab("server"); IsConnectionPopupOpen = true; }
        else if (itemId == "system.definition_check") SystemTools.SelectTab("definition_check");
        else if (itemId == "system.inventory_diagnostics") SystemTools.SelectTab("inventory");
        else if (itemId == "system.smoke") SystemTools.SelectTab("smoke");
    }

    private void ToggleWorkspacePanelVisibility(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        if (panel.IsVisible)
        {
            HideWorkspacePanel(panelId);
        }
        else
        {
            ShowWorkspacePanel(panelId);
        }
    }

    private void ShowWorkspacePanel(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        panel.IsVisible = true;
        SaveWorkspaceLayout();
        RefreshConnectionSummary();
    }

    private void HideWorkspacePanel(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        panel.IsDetached = false;
        panel.IsVisible = false;
        SaveWorkspaceLayout();
        RefreshConnectionSummary();
    }

    private void DetachWorkspacePanel(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        if (!panel.CanDetach) return;
        panel.IsVisible = true;
        panel.IsDetached = true;
        ClientLogService.Instance.Info($"ui-panel action=detach panel={panel.PanelId}");
        SaveWorkspaceLayout();
        RefreshConnectionSummary();
    }

    private void AttachWorkspacePanel(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        panel.IsDetached = false;
        panel.IsVisible = true;
        ClientLogService.Instance.Info($"ui-panel action=attach panel={panel.PanelId}");
        SaveWorkspaceLayout();
        RefreshConnectionSummary();
    }


    private void OpenPlayerCharacters()
    {
        if (string.IsNullOrWhiteSpace(SelectedOwnerUserId)) return;
        RunUiAction("Выполнить действие", () =>
        {
            LoadOwnerCharacters();
            SelectedSection = "admin.users";
        });
    }

    private void FocusSelectedCharacter()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        SelectedSection = "admin.characters";
        SelectedCharacterWorkspaceTab = "Card";
        OpenCharacter();
    }

    private void FocusSelectedRequest()
    {
        if (string.IsNullOrWhiteSpace(SelectedPendingRequestId)) return;
        SelectedSection = "requests.formal";
    }

    private void FocusCharacterEditor()
    {
        SelectedSection = "admin.characters";
        SelectedCharacterWorkspaceTab = "Editor";
    }

    private void FocusCharacterNotes()
    {
        SelectedSection = "admin.characters";
        SelectedCharacterWorkspaceTab = "Notes";
        ShowWorkspacePanel("NotesManagement");
    }

    private void FocusCharacterVisibility()
    {
        SelectedSection = "admin.characters";
        SelectedCharacterWorkspaceTab = "Visibility";
    }

    private void RefreshSelectedCharacter()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        RunUiAction(" ", OpenCharacter);
    }

    private void RefreshPeopleSection()
    {
        RunUiAction("Выполнить действие", () =>
        {
            LoadPending();
            LoadPlayers();
            if (!string.IsNullOrWhiteSpace(SelectedOwnerUserId))
            {
                LoadOwnerCharacters();
            }
            LoadLocksSummary();
            ClientLogService.Instance.Debug($"ui-refresh section=Люди final pending={PendingAccounts.Count} players={Players.Count} characters={Characters.Count} locks={LockRows.Count}");
        });
    }

    private void RefreshModerationSection()
    {
        RunUiAction(" ", () =>
        {
            LoadPendingRequests();
            LoadRequestHistory();
            ClientLogService.Instance.Debug($"ui-refresh section=... final requests={PendingRequests.Count} history={RequestHistoryRows.Count} dice={DiceFeedRows.Count}");
        });
    }

    private void RefreshSessionSection()
    {
        RunUiAction(" ", () =>
        {
            CombatRefresh();
            ChatRefresh();
            AudioRefresh();
            ClientLogService.Instance.Debug($"ui-refresh section=... final combatRows={CombatRows.Count} chatRows={ChatRows.Count} audioRows={AudioLibraryRows.Count}");
        });
    }

    private void RefreshContentSection()
    {
        RunUiAction(" ", () =>
        {
            RefreshDefinitionClasses();
            RefreshDefinitionSkills();
            RefreshDefinitionRaces();
            RefreshDefinitionItems();
            RefreshDefinitionsContentStatus();
            ClientLogService.Instance.Debug($"ui-refresh section=Контент final classes={ClassDefinitionRows.Count} skills={SkillDefinitionRows.Count}");
        });
    }

    private void RefreshSystemSection()
    {
        RunUiAction("Выполнить действие", () =>
        {
            BackupRefresh();
            DiagnosticsRefresh();
            ClientLogService.Instance.Debug($"ui-refresh section=... final backups={BackupItems.Count} diagnostics={DiagnosticsItems.Count}");
        });
    }

    private void FocusContentClasses()
    {
        SelectedSection = "admin.classes";
    }

    private void FocusContentReference()
    {
        SelectedSection = "definitions.browser";
    }

    private void FocusSystemReference()
    {
        SelectedSection = "system.settings";
        SelectedSystemTabIndex = 0;
    }

    private void FocusSystemBackups()
    {
        SelectedSection = "system.tools";
        SystemTools.SelectTab("backups");
        SelectedSystemTabIndex = 1;
    }

    private void FocusSystemDiagnostics()
    {
        SelectedSection = "system.tools";
        SystemTools.SelectTab("diagnostics");
        SelectedSystemTabIndex = 2;
    }

    private void RestoreSelection(ObservableCollection<RowVm> source, string selectedId, Action<string> setter)
    {
        if (string.IsNullOrWhiteSpace(selectedId))
        {
            return;
        }

        if (source.Any(row => row.Id == selectedId))
        {
            setter(selectedId);
            return;
        }

        setter(string.Empty);
    }

    private void RunUiAction(string message, Action action)
    {
        try
        {
            IsBusy = true;
            BusyMessage = message;
            LastStatusMessage = message;
            action();
            if (string.IsNullOrWhiteSpace(LastErrorMessage))
            {
                LastStatusMessage = message + "  ";
            }
        }
        catch (Exception ex)
        {
            HandleUiException(message, ex);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
            RefreshConnectionSummary();
        }
    }

    private void Login()
    {
        try
        {
            ConnectToServer();
            ClientLogService.Instance.Info($"Login attempt: user={LoginText}");
            var r = _api.Login(LoginText, PasswordText);
            if (r.Status == ResponseStatus.Ok)
            {
                var roleItems = ToList(r.Payload.ContainsKey("roles") ? r.Payload["roles"] : new ArrayList());
                var resolvedRoles = new List<string>();
                var isAdmin = false;
                foreach (var roleItem in roleItems)
                {
                    var roleValue = Convert.ToString(roleItem) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(roleValue))
                    {
                        continue;
                    }

                    resolvedRoles.Add(roleValue);
                    if (string.Equals(roleValue, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(roleValue, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        isAdmin = true;
                    }
                }
                ClientLogService.Instance.Info($"admin.roleGate rolesResolved={string.Join(", ", resolvedRoles)}");
                ClientLogService.Instance.Info($"admin.roleGate isAdmin={isAdmin}");
                if (!isAdmin)
                {
                    _poller.Stop();
                    IsAuthenticated = false;
                    IsConnectedToServer = false;
                    IsOnline = false;
                    ConnectionState = "Оффлайн";
                    ConnectionStatusDetail = "Нет данных";
                    LastStatusMessage = ConnectionStatusDetail;
                    LastErrorMessage = string.Empty;
                    _client.Disconnect();
                    RefreshConnectionSummary();
                    ClientLogService.Instance.Warn("auth.login.denied client-gate reason=insufficient-admin-role");
                    return;
                }

                IsAuthenticated = true;
                SetConnectedState($"Подключено: {CurrentEndpoint}");
                _poller.Start();
                RefreshAll();
                RefreshCurrentNavigationSideEffectsAfterLogin();
                IsAuthPopupOpen = false;
                Notify(nameof(LoginSummary));
                ClientLogService.Instance.Info($"Login success: user={LoginText}");
            }
            else
            {
                IsAuthenticated = false;
                LastErrorMessage = r.Message;
                IsConnectedToServer = true;
                IsOnline = true;
                ConnectionState = "Подключено";
                ConnectionStatusDetail = string.IsNullOrWhiteSpace(r.Message) ? "  ." : r.Message;
                LastStatusMessage = "  .";
                RefreshConnectionSummary();
                ClientLogService.Instance.Warn($"Login failed: user={LoginText}; message={r.Message}");
            }
        }
        catch (Exception ex)
        {
            HandleUiException(" ", ex);
        }
    }

    private void RefreshCurrentNavigationSideEffectsAfterLogin()
    {
        var itemId = SelectedNavigationItemId;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemId = NavigationGroups
                .SelectMany(group => group.Items)
                .FirstOrDefault(item => item.TargetViewKey == SelectedSection && !item.IsPlaceholder)
                ?.Id ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(itemId))
        {
            ApplyNavigationSideEffects(itemId);
            ClientLogService.Instance.Info($"ui.navigation.side-effects.after-login item={itemId} section={SelectedSection}");
        }
    }

    private void ChangePassword()
    {
        RunUiAction("Выполнить действие", () =>
        {
            ClientLogService.Instance.Info("ui.password.change.opened");
            var response = _api.ChangePassword(OldPasswordText, NewPasswordText);
            EnsureSuccess(response);
            OldPasswordText = string.Empty;
            NewPasswordText = string.Empty;
            Notify(nameof(OldPasswordText));
            Notify(nameof(NewPasswordText));
            ClientLogService.Instance.Info("auth.changePassword result=ok");
        });
    }


    private void PollSyncAndRefresh()
    {
        try
        {
            RefreshAll();
            PollPassiveSync();
        }
        catch (Exception ex)
        {
            HandleUiException("  ", ex);
        }
    }

    private void PollPassiveSync()
    {
        if (!SyncFeatureFlags.UsePassiveSyncPoller) return;
        if (!IsConnectedToServer || !IsAuthenticated) return;
        var scopes = new[] { "chat:default", "dice", "fate", "definitions" };
        var response = _api.SyncChangesGet(_syncRevision, scopes, 100);
        if (response.Status != ResponseStatus.Ok || !response.Payload.ContainsKey("events")) return;
        foreach (var raw in ToList(response.Payload["events"]))
        {
            var evt = ClientSyncEvent.FromMap(AsMap(raw));
            ClientLogService.Instance.Info($"sync.event.received eventId={evt.EventId} revision={evt.Revision} type={evt.Type} scope={evt.Scope}");
            if (SyncFeatureFlags.UseEventDispatcher)
            {
                try { _syncDispatcher.DispatchAsync(evt).GetAwaiter().GetResult(); }
                catch (Exception ex) { ClientLogService.Instance.Error($"sync.dispatch.error eventId={evt.EventId} type={evt.Type} message={ex.Message}", ex); }
            }
            _syncRevision = Math.Max(_syncRevision, evt.Revision);
        }
    }
    private void RefreshAll()
    {
        if (!IsConnectedToServer)
        {
            SetDisconnectedState("Нет данных");
            return;
        }

        try
        {
            ClientLogService.Instance.Debug("ui-refresh section=Люди step=LoadPending");
            LoadPending();
            ClientLogService.Instance.Debug("ui-refresh section=Люди step=LoadPlayers");
            LoadPlayers();
            ClientLogService.Instance.Debug("ui-refresh section=... step=LoadPendingRequests");
            LoadPendingRequests();
            LoadRequestHistory();
            ClientLogService.Instance.Debug("ui-refresh section=... step=CombatRefresh");
            CombatRefresh();
            ClientLogService.Instance.Debug("ui-refresh section=Контент step=RefreshDefinitionClasses");
            RefreshDefinitionClasses();
            RefreshDefinitionSkills();
            RefreshDefinitionRaces();
            RefreshDefinitionItems();
            RefreshDefinitionsContentStatus();
            if (!string.IsNullOrWhiteSpace(SelectedCharacterId))
            {
                ClientLogService.Instance.Debug("ui-refresh section=... step=LoadClassTree+LoadSkills");
                LoadClassTree();
                LoadSkills();
        RefreshCharacterClasses();
                RefreshCharacterClasses();
            }
            ClientLogService.Instance.Debug("ui-refresh section=... step=ChatRefresh");
            ChatRefresh();
            AudioRefresh();
            NotesRefresh();
            ReferenceRefresh();
            BackupRefresh();
            DiagnosticsRefresh();
            LoadLocksSummary();
            FunctionalDashboard.Refresh();
            SetConnectedState($" : {CurrentEndpoint}");
        }
        catch (Exception ex)
        {
            HandleUiException(" ", ex);
        }
    }

    private void LoadPending()
    {
        PendingAccounts.Clear();
        var r = _api.GetPendingAccounts();
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var obj in ToList(r.Payload["items"]))
        {
            var m = AsMap(obj);
            if (m == null) continue;
            PendingAccounts.Add(new RowVm { Id = S(m, "accountId"), Name = S(m, "login"), State = S(m, "status"), Extra = S(m, "createdUtc") });
        }
        ClientLogService.Instance.Debug($"ui-refresh section=Люди block=Ожидающие raw={ToList(r.Payload["items"]).Count} shown={PendingAccounts.Count}");
        ClientLogService.Instance.Info($"people.grid.rows count={PendingAccounts.Count}");
        RestoreSelection(PendingAccounts, SelectedPendingAccountId, value => SelectedPendingAccountId = value);
        RefreshConnectionSummary();
    }

    private void LoadPlayers()
    {
        Players.Clear();
        var r = _api.GetPlayers();
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var obj in ToList(r.Payload["items"]))
        {
            var m = AsMap(obj);
            if (m == null) continue;
            var isOnline = IsTruthy(S(m, "isOnline"));
            Players.Add(new RowVm
            {
                Id = S(m, "accountId"),
                Name = S(m, "login"),
                State = isOnline ? "В сети" : "Не в сети",
                Extra = isOnline ? "Нет данных" : FormatLastSeen(S(m, "lastSeenUtc"))
            });
        }
        ClientLogService.Instance.Debug($"ui-refresh section=... block=... raw={ToList(r.Payload["items"]).Count} shown={Players.Count}");
        ClientLogService.Instance.Info($"people.grid.rows count={Players.Count}");
        RestoreSelection(Players, SelectedOwnerUserId, value => SelectedOwnerUserId = value);
        RefreshConnectionSummary();
    }

    private void LoadOwnerCharacters()
    {
        if (string.IsNullOrWhiteSpace(SelectedOwnerUserId)) return;
        Characters.Clear();
        var r = _api.GetCharactersByOwner(SelectedOwnerUserId);
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var obj in ToList(r.Payload["items"]))
        {
            var m = AsMap(obj);
            if (m == null) continue;
            Characters.Add(new RowVm { Id = S(m, "characterId"), Name = S(m, "name"), State = S(m, "archived"), Extra = S(m, "race") });
        }
        Notify(nameof(FilteredCharacters));
        var visibleCharacters = FilteredCharacters.Count();
        ClientLogService.Instance.Debug($"ui-refresh section=... block=... loaded={Characters.Count} filtered={visibleCharacters} visible={visibleCharacters}");
        ClientLogService.Instance.Info($"people.grid.rows count={visibleCharacters}");
        RestoreSelection(Characters, SelectedCharacterId, value => SelectedCharacterId = value);
        RefreshConnectionSummary();
    }

    private void LoadAllCharactersForAdminSection()
    {
        if (!ArePrivilegedSectionsEnabled || IsBusy) return;

        RunUiAction("Выполнить действие", () =>
        {
            Characters.Clear();
            var response = _api.GetAllCharacters(includeArchived: false);
            if (response.Status != ResponseStatus.Ok || !response.Payload.ContainsKey("items"))
            {
                ClientLogService.Instance.Warn($"character.admin.list.ui error status={response.Status} message={response.Message}");
                CharacterCard.MarkError(string.IsNullOrWhiteSpace(response.Message) ? "Не удалось загрузить карточку персонажа." : response.Message);
                return;
            }

            foreach (var item in ToList(response.Payload["items"]))
            {
                var map = AsMap(item);
                if (map == null) continue;

                var characterId = FirstNonEmpty(S(map, "characterId"), S(map, "id"));
                if (string.IsNullOrWhiteSpace(characterId)) continue;

                Characters.Add(new RowVm
                {
                    Id = characterId,
                    Name = FirstNonEmpty(S(map, "name"), "Без имени"),
                    State = IsTruthy(S(map, "archived")) ? "Нет данных" : "Нет данных",
                    Extra = FirstNonEmpty(S(map, "race"), S(map, "raceName"), "Character v2")
                });
            }

            Notify(nameof(FilteredCharacters));
            var selectedStillAvailable = Characters.Any(row => string.Equals(row.Id, SelectedCharacterId, StringComparison.OrdinalIgnoreCase));
            if (!selectedStillAvailable)
            {
                SelectedCharacterId = Characters.FirstOrDefault()?.Id ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(SelectedCharacterId))
            {
                OpenCharacter();
            }

            ClientLogService.Instance.Info($"character.admin.section.autoload count={Characters.Count} selected={SelectedCharacterId}");
            RefreshConnectionSummary();
        });
    }

    private void OpenCharacter()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        ClientLogService.Instance.Info($"character.card.load.start characterId={SelectedCharacterId}");
        var r = _api.CharacterAdminHubGet(SelectedCharacterId);
        if (r.Status != ResponseStatus.Ok)
        {
            CharacterCard.MarkError(r.Message);
            ClientLogService.Instance.Warn($"character.card.load.error characterId={SelectedCharacterId} status={r.Status} message={r.Message}");
            return;
        }

        var hub = FirstCharacterHubCard(r.Payload);
        if (hub == null)
        {
            CharacterCard.MarkError("Character v2 hub did not return a character card.");
            ClientLogService.Instance.Warn($"character.card.load.empty_hub characterId={SelectedCharacterId}");
            return;
        }

        EditName = S(hub, "name");
        EditRace = S(hub, "race");
        EditHeight = S(hub, "height");
        EditDescription = S(hub, "description");
        _editBackstory = S(hub, "backstory");
        BiographySaveStatus = "Биография загружена.";
        EditAge = ReadInt(hub, "age");

        var stats = MapValue(hub.ContainsKey("stats") ? hub["stats"] : null);
        Health = ReadInt(stats, "health", ParseFirstInt(S(hub, "health")));
        PhysicalArmor = ReadInt(stats, "physicalArmor", ParseFirstInt(S(hub, "armor")));
        MagicalArmor = ReadInt(stats, "magicalArmor");
        Morale = ReadInt(stats, "morale");
        Strength = ReadInt(stats, "strength");
        Dexterity = ReadInt(stats, "dexterity");
        Endurance = ReadInt(stats, "endurance");
        Wisdom = ReadInt(stats, "wisdom");
        Intellect = ReadInt(stats, "intellect");
        Charisma = ReadInt(stats, "charisma");
        BindAttributeEditorRows(hub);
        BindCharacterStatEditorRows(hub);
        ApplyAttributeRowsToLegacyProperties();

        var money = MapValue(hub.ContainsKey("money") ? hub["money"] : null);
        Iron = ReadLong(money, "Iron");
        Bronze = ReadLong(money, "Bronze");
        Silver = ReadLong(money, "Silver");
        Gold = ReadLong(money, "Gold");
        Platinum = ReadLong(money, "Platinum");
        Orichalcum = ReadLong(money, "Orichalcum");
        Adamant = ReadLong(money, "Adamant");
        Sovereign = ReadLong(money, "Sovereign");
        long.TryParse(S(hub, "xpCoins"), out var xpValue);
        ExperienceCoins = xpValue;
        BindCurrencyEditorRows(hub);
        ApplyCurrencyRowsToLegacyProperties();
        CharacterMoneySaveStatus = $"Валюты загружены: {CurrencyEditorRows.Count}.";
        ClientLogService.Instance.Info($"character.reload stats={Health}/{Strength}/{Dexterity} currencies={CurrencyEditorRows.Count} xp={ExperienceCoins}");
        ClientLogService.Instance.Info("character.money.reload values=" + string.Join(",", CurrencyEditorRows.Select(row => $"{row.Code}:{row.Amount}")));

        LoadCharacterInventory();
        LoadCharacterHoldings();
        LoadCharacterReputation();
        LoadCharacterCompanions();
        LoadOwnershipPanel();
        CharacterCard.LoadFromDetails(
            BuildCharacterCardPayloadFromHub(hub),
            SelectedCharacterId,
            SelectedPlayer?.Name ?? SelectedOwnerUserId,
            VisHideDescription,
            VisHideBackstory,
            VisHideStats,
            VisHideReputation);
        ClientLogService.Instance.Info($"character.card.load.done characterId={SelectedCharacterId}");
        if (SkillDefinitionRows.Count == 0)
        {
            RefreshDefinitionSkills();
        }
        ClientLogService.Instance.Info($"skillDefinitions.assignment.load count={SkillDefinitionRows.Count}");
        LoadSkills();

        NotifyAllEditor();
    }

    private void LoadOwnershipPanel()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        var response = _api.CharacterOwnershipGet(new Dictionary<string, object> { { "characterId", SelectedCharacterId } });
        if (response.Status != ResponseStatus.Ok)
        {
            OwnershipMessage = $"Владелец/статус недоступны: {response.Message}";
            ClientLogService.Instance.Warn($"character.ownership.panel.load.failed characterId={SelectedCharacterId} status={response.Status} message={response.Message}");
            return;
        }

        var ownership = response.Payload.TryGetValue("ownership", out var raw) ? MapValue(raw) : new Dictionary<string, object>();
        OwnershipOwnerUserId = S(ownership, "ownerUserId");
        OwnershipControlledByUserId = S(ownership, "controlledByUserId");
        OwnershipKind = FirstNonEmpty(S(ownership, "characterKind"), MapOwnershipRoleToKind(S(ownership, "characterRole")));
        OwnershipStatus = FirstNonEmpty(S(ownership, "characterStatus"), ReadBool(ownership, "isArchived") ? CharacterStatusIds.Archived : ReadBool(ownership, "isActive") ? CharacterStatusIds.Active : CharacterStatusIds.Inactive);
        OwnershipIsActive = ReadBool(ownership, "isActive", true);
        OwnershipIsArchived = ReadBool(ownership, "isArchived", ReadBool(ownership, "archived"));
        OwnershipIsPlayerVisible = ReadBool(ownership, "isPlayerVisible", true);

        var groups = ToList(ownership.TryGetValue("groupMembership", out var groupsRaw) ? groupsRaw : new ArrayList())
            .Cast<object>()
            .Select(item => MapValue(item))
            .FirstOrDefault(map => map.Count > 0);
        OwnershipGroupId = groups == null ? string.Empty : S(groups, "groupId");
        OwnershipGroupName = groups == null ? string.Empty : FirstNonEmpty(S(groups, "displayName"), S(groups, "groupId"));
        OwnershipMessage = "Владелец, группа и статус загружены.";
        Notify(nameof(OwnershipSummary));
        ClientLogService.Instance.Info($"character.ownership.panel.load.done characterId={SelectedCharacterId} owner={OwnershipOwnerUserId} group={OwnershipGroupId} kind={OwnershipKind} status={OwnershipStatus}");
    }

    private void SaveOwnershipOwner()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        var payload = new Dictionary<string, object>
        {
            { "characterId", SelectedCharacterId },
            { "ownerUserId", OwnershipOwnerUserId },
            { "controlledByUserId", OwnershipControlledByUserId },
            { "isPlayerVisible", OwnershipIsPlayerVisible },
            { "reason", OwnershipReason }
        };
        var response = _api.CharacterOwnershipAssignOwner(payload);
        OwnershipMessage = response.Status == ResponseStatus.Ok ? "Нет данных" : $"Нет данных";
        ClientLogService.Instance.Info($"character.ownership.owner.save status={response.Status} characterId={SelectedCharacterId} owner={OwnershipOwnerUserId}");
        if (response.Status == ResponseStatus.Ok) LoadOwnershipPanel();
    }

    private void SaveOwnershipKindStatus()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        var roleResponse = _api.CharacterOwnershipSetRole(new Dictionary<string, object>
        {
            { "characterId", SelectedCharacterId },
            { "characterRole", MapKindToOwnershipRole(OwnershipKind) },
            { "reason", OwnershipReason }
        });
        if (roleResponse.Status != ResponseStatus.Ok)
        {
            OwnershipMessage = $"Ошибка типа: {roleResponse.Message}";
            return;
        }

        var statusResponse = _api.CharacterOwnershipSetVisibility(new Dictionary<string, object>
        {
            { "characterId", SelectedCharacterId },
            { "isPlayerVisible", OwnershipIsPlayerVisible },
            { "characterStatus", OwnershipStatus },
            { "isActive", OwnershipIsActive },
            { "isArchived", OwnershipIsArchived },
            { "reason", OwnershipReason }
        });
        OwnershipMessage = statusResponse.Status == ResponseStatus.Ok ? "Тип и статус сохранены." : $"Ошибка статуса: {statusResponse.Message}";
        ClientLogService.Instance.Info($"character.ownership.kind_status.save status={statusResponse.Status} characterId={SelectedCharacterId} kind={OwnershipKind} characterStatus={OwnershipStatus} active={OwnershipIsActive} archived={OwnershipIsArchived}");
        if (statusResponse.Status == ResponseStatus.Ok) LoadOwnershipPanel();
    }

    private void AssignOwnershipGroup()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(OwnershipGroupId)) return;
        var response = _api.GroupCharacterMemberAdd(new Dictionary<string, object>
        {
            { "groupId", OwnershipGroupId },
            { "entityType", CharacterGroupEntityTypeIds.PlayerCharacter },
            { "entityId", SelectedCharacterId },
            { "displayName", FirstNonEmpty(EditName, SelectedCharacter?.Name, SelectedCharacterId) },
            { "roleInGroup", CharacterGroupRoleInGroupIds.Member },
            { "characterRole", MapKindToGroupRole(OwnershipKind) },
            { "ownerUserId", OwnershipOwnerUserId },
            { "controlledByUserId", OwnershipControlledByUserId },
            { "isPlayerVisible", OwnershipIsPlayerVisible }
        });
        OwnershipMessage = response.Status == ResponseStatus.Ok ? "Группа назначена." : $"Ошибка группы: {response.Message}";
        ClientLogService.Instance.Info($"character.ownership.group.assign status={response.Status} characterId={SelectedCharacterId} groupId={OwnershipGroupId}");
        if (response.Status == ResponseStatus.Ok) LoadOwnershipPanel();
    }

    private void SetOwnershipArchived(bool archived)
    {
        OwnershipIsArchived = archived;
        OwnershipIsActive = !archived;
        OwnershipStatus = archived ? CharacterStatusIds.Archived : CharacterStatusIds.Active;
        SaveOwnershipKindStatus();
    }

    private void BindAttributeEditorRows(Dictionary<string, object> hub)
    {
        AttributeEditorRows.Clear();
        var rawAttributes = hub.ContainsKey("attributes") ? hub["attributes"] : new ArrayList();
        foreach (var item in ToList(rawAttributes))
        {
            var row = BuildAttributeEditorRow(MapValue(item), defaultMaxValue: 30, automationScope: "Attribute");
            if (row != null) AttributeEditorRows.Add(row);
        }

        if (AttributeEditorRows.Count == 0)
        {
            AddFallbackAttributeRow(CharacterAttributeIds.Strength, "strength", "Нет данных", Strength, 10);
            AddFallbackAttributeRow(CharacterAttributeIds.Dexterity, "dexterity", "Нет данных", Dexterity, 20);
            AddFallbackAttributeRow(CharacterAttributeIds.Endurance, "endurance", "Нет данных", Endurance, 30);
            AddFallbackAttributeRow(CharacterAttributeIds.Intellect, "intelligence", "Интеллект", Intellect, 40);
            AddFallbackAttributeRow(CharacterAttributeIds.Wisdom, "wisdom", "Мудрость", Wisdom, 50);
            AddFallbackAttributeRow(CharacterAttributeIds.Charisma, "charisma", "Харизма", Charisma, 60);
        }

        LoadAdminSubAttributesIntoRows();
        ClientLogService.Instance.Info($"character.admin.attributes.bind characterId={SelectedCharacterId} count={AttributeEditorRows.Count} subAttributes={AttributeEditorRows.Sum(x => x.SubAttributes.Count)}");
    }

    private void BindCharacterStatEditorRows(Dictionary<string, object> hub)
    {
        VitalsEditorRows.Clear();
        DerivedStatEditorRows.Clear();

        foreach (var item in ToList(hub.ContainsKey("vitals") ? hub["vitals"] : new ArrayList()))
        {
            var row = BuildAttributeEditorRow(MapValue(item), defaultMaxValue: 999, automationScope: "Vital");
            if (row != null) VitalsEditorRows.Add(row);
        }

        foreach (var item in ToList(hub.ContainsKey("derivedStats") ? hub["derivedStats"] : new ArrayList()))
        {
            var row = BuildAttributeEditorRow(MapValue(item), defaultMaxValue: 999, automationScope: "Derived");
            if (row != null) DerivedStatEditorRows.Add(row);
        }

        if (VitalsEditorRows.Count == 0)
        {
            AddFallbackStatRow(VitalsEditorRows, CharacterVitalStatIds.HealthCurrent, "health_current", "Нет данных", Health, 10);
            AddFallbackStatRow(VitalsEditorRows, CharacterVitalStatIds.HealthMax, "health_max", "Нет данных", Health, 20);
            AddFallbackStatRow(VitalsEditorRows, CharacterVitalStatIds.PhysicalDefense, "physical_defense", "Физическая защита", PhysicalArmor, 30);
            AddFallbackStatRow(VitalsEditorRows, CharacterVitalStatIds.MagicalDefense, "magical_defense", "Магическая защита", MagicalArmor, 40);
            AddFallbackStatRow(VitalsEditorRows, CharacterVitalStatIds.Morale, "morale", "Мораль", Morale, 50);
        }

        if (DerivedStatEditorRows.Count == 0)
        {
            AddFallbackStatRow(DerivedStatEditorRows, CharacterVitalStatIds.Initiative, "initiative", "Нет данных", 0, 110);
            AddFallbackStatRow(DerivedStatEditorRows, CharacterVitalStatIds.Movement, "movement", "Перемещение", 0, 120);
            AddFallbackStatRow(DerivedStatEditorRows, CharacterVitalStatIds.CarryingCapacity, "carrying_capacity", "Грузоподъёмность", 0, 130);
            AddFallbackStatRow(DerivedStatEditorRows, "dev_acceptance_derived", "dev_acceptance_derived", "Нет данных", 5, 990);
        }

        ClientLogService.Instance.Info($"character.admin.vitals.bind characterId={SelectedCharacterId} count={VitalsEditorRows.Count}");
        ClientLogService.Instance.Info($"character.admin.derived.bind characterId={SelectedCharacterId} count={DerivedStatEditorRows.Count}");
    }

    private AttributeEditorRowVm? BuildAttributeEditorRow(Dictionary<string, object> map, int defaultMaxValue, string automationScope = "Attribute")
    {
        var attributeId = FirstNonEmpty(S(map, "attributeId"), S(map, "definitionId"), S(map, "id"), S(map, "code"));
        if (string.IsNullOrWhiteSpace(attributeId)) return null;
        var code = FirstNonEmpty(S(map, "code"), attributeId);
        var value = ReadInt(map, "value", ReadInt(map, "currentValue"));
        var row = new AttributeEditorRowVm
        {
            AttributeId = attributeId,
            Code = code,
            DisplayName = FirstNonEmpty(S(map, "displayName"), S(map, "label"), attributeId),
            Description = FirstNonEmpty(S(map, "description")),
            Value = value,
            OriginalValue = value,
            MinValue = ReadInt(map, "minValue", 0),
            MaxValue = ReadInt(map, "maxValue", defaultMaxValue),
            DefaultValue = ReadInt(map, "defaultValue", value),
            SortOrder = ReadInt(map, "sortOrder", 1000),
            AttributeSetId = FirstNonEmpty(S(map, "attributeSetId"), S(map, "category")),
            SourceRuleSetId = S(map, "sourceRuleSetId"),
            IsPlayerVisible = ReadBool(map, "isPlayerVisible", true),
            IsEditableByGM = ReadBool(map, "isEditableByGM", true),
            AutomationScope = automationScope
        };
        foreach (var subItem in ToList(map.ContainsKey("subAttributes") ? map["subAttributes"] : new ArrayList()))
        {
            var subRow = BuildSubAttributeEditorRow(MapValue(subItem), attributeId);
            if (subRow != null) row.SubAttributes.Add(subRow);
        }
        row.OnValueChanged = OnAttributeEditorValueChanged;
        return row;
    }

    private void LoadAdminSubAttributesIntoRows()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || AttributeEditorRows.Count == 0) return;
        try
        {
            var response = _api.CharacterSubAttributesAdminGet(SelectedCharacterId);
            if (response.Status != ResponseStatus.Ok)
            {
                ClientLogService.Instance.Warn($"character.admin.subattributes.load.failed characterId={SelectedCharacterId} status={response.Status} message={response.Message}");
                return;
            }

            var payload = response.Payload ?? new Dictionary<string, object>();
            foreach (var row in AttributeEditorRows) row.SubAttributes.Clear();
            foreach (var item in ToList(payload.ContainsKey("items") ? payload["items"] : new ArrayList()))
            {
                var subMap = MapValue(item);
                var parentId = FirstNonEmpty(S(subMap, "parentAttributeId"), S(subMap, "attributeId"));
                var parent = AttributeEditorRows.FirstOrDefault(row =>
                    string.Equals(row.AttributeId, parentId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(row.Code, parentId, StringComparison.OrdinalIgnoreCase));
                if (parent == null) continue;
                var subRow = BuildSubAttributeEditorRow(subMap, parent.AttributeId);
                if (subRow != null) parent.SubAttributes.Add(subRow);
            }

            foreach (var row in AttributeEditorRows)
            {
                var ordered = row.SubAttributes.OrderBy(x => x.SortOrder).ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
                row.SubAttributes.Clear();
                foreach (var sub in ordered) row.SubAttributes.Add(sub);
            }

            ClientLogService.Instance.Info($"character.admin.subattributes.load.done characterId={SelectedCharacterId} count={AttributeEditorRows.Sum(x => x.SubAttributes.Count)}");
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Warn($"character.admin.subattributes.load.error characterId={SelectedCharacterId} reason={ex.GetType().Name}:{ex.Message}");
        }
    }

    private SubAttributeEditorRowVm? BuildSubAttributeEditorRow(Dictionary<string, object> map, string fallbackParentAttributeId)
    {
        var subAttributeId = FirstNonEmpty(S(map, "subAttributeId"), S(map, "id"), S(map, "code"));
        if (string.IsNullOrWhiteSpace(subAttributeId)) return null;
        var code = FirstNonEmpty(S(map, "code"), subAttributeId);
        var value = ReadInt(map, "value", ReadInt(map, "currentValue", ReadInt(map, "defaultValue")));
        return new SubAttributeEditorRowVm
        {
            SubAttributeId = subAttributeId,
            ParentAttributeId = FirstNonEmpty(S(map, "parentAttributeId"), fallbackParentAttributeId),
            Code = code,
            DisplayName = FirstNonEmpty(S(map, "displayName"), S(map, "label"), subAttributeId),
            Description = FirstNonEmpty(S(map, "description")),
            Value = value,
            OriginalValue = value,
            ManualBonus = ReadInt(map, "manualBonus"),
            MinValue = ReadInt(map, "minValue", 0),
            MaxValue = ReadInt(map, "maxValue", 30),
            DefaultValue = ReadInt(map, "defaultValue", value),
            SortOrder = ReadInt(map, "sortOrder", 1000),
            IsPlayerVisible = ReadBool(map, "isPlayerVisible", true),
            IsEditableByGM = ReadBool(map, "isEditableByGM", true),
            Notes = S(map, "notes")
        };
    }

    private void BindCurrencyEditorRows(Dictionary<string, object> hub)
    {
        CurrencyEditorRows.Clear();
        var rawCurrencies = hub.ContainsKey("currencies") ? hub["currencies"] : new ArrayList();
        foreach (var item in ToList(rawCurrencies))
        {
            var map = MapValue(item);
            var currencyId = FirstNonEmpty(S(map, "currencyId"), S(map, "id"), S(map, "code"));
            if (string.IsNullOrWhiteSpace(currencyId)) continue;
            var code = FirstNonEmpty(S(map, "code"), currencyId);
            var amount = ReadLong(map, "amount", ReadLong(map, "value"));
            var row = new CurrencyEditorRowVm
            {
                CurrencyId = currencyId,
                Code = code,
                DisplayName = FirstNonEmpty(S(map, "displayName"), S(map, "label"), code),
                Description = S(map, "description"),
                Kind = FirstNonEmpty(S(map, "kind"), string.Equals(currencyId, CharacterCurrencyIds.XpCoin, StringComparison.OrdinalIgnoreCase) ? "experience" : "money"),
                MinValue = ReadLong(map, "minValue"),
                MaxValue = TryReadNullableLong(map, "maxValue"),
                DefaultValue = ReadLong(map, "defaultValue"),
                Amount = amount,
                OriginalAmount = amount,
                Unit = S(map, "unit"),
                SortOrder = ReadInt(map, "sortOrder", 1000),
                SourceRuleSetId = S(map, "sourceRuleSetId"),
                SourceCurrencySetId = S(map, "sourceCurrencySetId"),
                IsPlayerVisible = ReadBool(map, "isPlayerVisible", true),
                IsEditableByGM = ReadBool(map, "isEditableByGM", true)
            };
            row.OnAmountChanged = OnCurrencyEditorAmountChanged;
            CurrencyEditorRows.Add(row);
        }

        if (CurrencyEditorRows.Count == 0)
        {
            AddFallbackCurrencyRow(CharacterCurrencyIds.IronCoin, "iron", "Железная монета", Iron, 10, "money");
            AddFallbackCurrencyRow(CharacterCurrencyIds.BronzeCoin, "bronze", "Нет данных", Bronze, 20, "money");
            AddFallbackCurrencyRow(CharacterCurrencyIds.SilverCoin, "silver", "Нет данных", Silver, 30, "money");
            AddFallbackCurrencyRow(CharacterCurrencyIds.GoldCoin, "gold", "Золотая монета", Gold, 40, "money");
            AddFallbackCurrencyRow(CharacterCurrencyIds.PlatinumCoin, "platinum", " ", Platinum, 50, "money");
            AddFallbackCurrencyRow(CharacterCurrencyIds.XpCoin, "xp_coin", "Монета опыта", ExperienceCoins, 90, "experience");
        }

        ClientLogService.Instance.Info($"character.admin.currencies.bind characterId={SelectedCharacterId} count={CurrencyEditorRows.Count}");
    }

    private void AddFallbackCurrencyRow(string currencyId, string code, string label, long amount, int sortOrder, string kind)
    {
        var row = new CurrencyEditorRowVm
        {
            CurrencyId = currencyId,
            Code = code,
            DisplayName = label,
            Kind = kind,
            MinValue = 0,
            Amount = amount,
            OriginalAmount = amount,
            SortOrder = sortOrder,
            SourceRuleSetId = RuleSetIds.FantasyNriDefault,
            SourceCurrencySetId = "fantasy_default_currencies",
            IsPlayerVisible = true,
            IsEditableByGM = true
        };
        row.OnAmountChanged = OnCurrencyEditorAmountChanged;
        CurrencyEditorRows.Add(row);
    }

    private void OnCurrencyEditorAmountChanged(CurrencyEditorRowVm row, long oldValue, long newValue)
    {
        CharacterMoneySaveStatus = $"{row.DisplayName} изменена: {oldValue} -> {newValue}.";
        ClientLogService.Instance.Info($"character.admin.currency.input.changed currencyId={row.CurrencyId} code={row.Code} old={oldValue} new={newValue}");
        ApplyCurrencyRowsToLegacyProperties();
    }

    private void ApplyCurrencyRowsToLegacyProperties()
    {
        foreach (var row in CurrencyEditorRows)
        {
            var key = FirstNonEmpty(row.CurrencyId, row.Code).Replace("_coin", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            switch (key)
            {
                case "iron": Iron = row.Amount; break;
                case "bronze": Bronze = row.Amount; break;
                case "silver": Silver = row.Amount; break;
                case "gold": Gold = row.Amount; break;
                case "platinum": Platinum = row.Amount; break;
                case "orichalcum": Orichalcum = row.Amount; break;
                case "adamant": Adamant = row.Amount; break;
                case "sovereign": Sovereign = row.Amount; break;
                case "xp": ExperienceCoins = row.Amount; break;
            }
        }
    }

    private void AddFallbackAttributeRow(string attributeId, string code, string label, int value, int sortOrder)
    {
        var row = new AttributeEditorRowVm
        {
            AttributeId = attributeId,
            Code = code,
            DisplayName = label,
            Value = value,
            OriginalValue = value,
            MinValue = 0,
            MaxValue = 30,
            DefaultValue = value,
            SortOrder = sortOrder,
            AttributeSetId = "fantasy_primary",
            SourceRuleSetId = RuleSetIds.FantasyNriDefault,
            IsPlayerVisible = true,
            IsEditableByGM = true
        };
        row.OnValueChanged = OnAttributeEditorValueChanged;
        AttributeEditorRows.Add(row);
    }

    private void AddFallbackStatRow(ObservableCollection<AttributeEditorRowVm> target, string attributeId, string code, string label, int value, int sortOrder)
    {
        var row = new AttributeEditorRowVm
        {
            AttributeId = attributeId,
            Code = code,
            DisplayName = label,
            Value = value,
            OriginalValue = value,
            MinValue = 0,
            MaxValue = 999,
            DefaultValue = value,
            SortOrder = sortOrder,
            AttributeSetId = "character_stats",
            SourceRuleSetId = RuleSetIds.FantasyNriDefault,
            IsPlayerVisible = true,
            IsEditableByGM = true,
            AutomationScope = ReferenceEquals(target, VitalsEditorRows) ? "Vital" : "Derived"
        };
        row.OnValueChanged = OnAttributeEditorValueChanged;
        target.Add(row);
    }

    private void OnAttributeEditorValueChanged(AttributeEditorRowVm row, int oldValue, int newValue)
    {
        CharacterStatsSaveStatus = $"{row.DisplayName} изменено: {oldValue} -> {newValue}.";
        ApplyAttributeRowsToLegacyProperties();
        ClientLogService.Instance.Info($"character.admin.attributes.input.changed attributeId={row.AttributeId} code={row.Code} old={oldValue} new={newValue}");
    }

    private void ApplyAttributeRowsToLegacyProperties()
    {
        Health = AttributeValue(CharacterVitalStatIds.HealthCurrent, AttributeValue(CharacterAttributeIds.Health, Health));
        PhysicalArmor = AttributeValue(CharacterVitalStatIds.PhysicalDefense, AttributeValue(CharacterAttributeIds.PhysicalArmor, PhysicalArmor));
        MagicalArmor = AttributeValue(CharacterVitalStatIds.MagicalDefense, AttributeValue(CharacterAttributeIds.MagicArmor, MagicalArmor));
        Morale = AttributeValue(CharacterVitalStatIds.Morale, AttributeValue(CharacterAttributeIds.Morale, Morale));
        Strength = AttributeValue(CharacterAttributeIds.Strength, Strength);
        Dexterity = AttributeValue(CharacterAttributeIds.Dexterity, Dexterity);
        Endurance = AttributeValue(CharacterAttributeIds.Endurance, Endurance);
        Wisdom = AttributeValue(CharacterAttributeIds.Wisdom, Wisdom);
        Intellect = AttributeValue(CharacterAttributeIds.Intellect, Intellect);
        Charisma = AttributeValue(CharacterAttributeIds.Charisma, Charisma);
    }

    private int AttributeValue(string attributeId, int fallback)
    {
        return AttributeEditorRows
            .Concat(VitalsEditorRows)
            .Concat(DerivedStatEditorRows)
            .FirstOrDefault(x => string.Equals(x.AttributeId, attributeId, StringComparison.OrdinalIgnoreCase))?.Value ?? fallback;
    }
    private void LoadCharacterInventory()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        try
        {
            var response = _api.CharacterInventoryGet(SelectedCharacterId);
            if (response.Status != ResponseStatus.Ok) return;
            ApplyInventoryPayload(response.Payload.ContainsKey("inventory") ? response.Payload["inventory"] : new ArrayList());
        }
        catch (Exception ex)
        {
            SetConnectionError(ex.Message);
        }
    }

    private void LoadInventoryCatalogDefinitions()
    {
        try
        {
            InventoryCatalogDefinitions.Clear();
            var payload = new Dictionary<string, object>
            {
                { "search", InventoryCatalogSearch ?? string.Empty },
                { "includeArchived", false }
            };

            foreach (var response in new[]
            {
                _api.CatalogAdminItemsList(payload),
                _api.CatalogAdminWeaponsList(payload),
                _api.CatalogAdminArmorList(payload),
                _api.CatalogAdminAmmoList(payload)
            })
            {
                if (response.Status != ResponseStatus.Ok) continue;
                foreach (var entry in ToList(response.Payload.ContainsKey("items") ? response.Payload["items"] : new ArrayList()))
                {
                    var map = AsMap(entry);
                    if (map == null) continue;
                    var item = CatalogDefinitionUiItem.FromMap(map);
                    if (!string.IsNullOrWhiteSpace(InventoryCatalogCategoryFilter)
                        && !string.Equals(InventoryCatalogCategoryFilter, "all", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(item.Category, InventoryCatalogCategoryFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    InventoryCatalogDefinitions.Add(item);
                }
            }

            SelectedInventoryCatalogDefinition = InventoryCatalogDefinitions.FirstOrDefault();
            InventoryStatus = InventoryCatalogDefinitions.Count == 0
                ? "Нет данных"
                : $"Каталог загружен: {InventoryCatalogDefinitions.Count}.";
            Notify(nameof(InventoryCatalogDefinitions));
        }
        catch (Exception ex)
        {
            InventoryStatus = ex.Message;
            SetConnectionError(ex.Message);
        }
    }

    private void LoadCharacterHoldings()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        try
        {
            var response = _api.CharacterHoldingsGet(SelectedCharacterId);
            if (response.Status != ResponseStatus.Ok) return;
            ApplyHoldingsPayload(response.Payload.ContainsKey("holdings") ? response.Payload["holdings"] : new ArrayList());
        }
        catch (Exception ex)
        {
            SetConnectionError(ex.Message);
        }
    }

    private void LoadCharacterReputation()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        try
        {
            var response = _api.CharacterReputationGet(SelectedCharacterId);
            if (response.Status != ResponseStatus.Ok) return;
            ApplyReputationPayload(response.Payload.ContainsKey("reputation") ? response.Payload["reputation"] : new ArrayList());
        }
        catch (Exception ex)
        {
            SetConnectionError(ex.Message);
        }
    }

    private void LoadCharacterCompanions()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        try
        {
            var response = _api.CharacterCompanionsGet(SelectedCharacterId);
            if (response.Status != ResponseStatus.Ok) return;
            ApplyCompanionsPayload(response.Payload.ContainsKey("companions") ? response.Payload["companions"] : new ArrayList());
        }
        catch (Exception ex)
        {
            SetConnectionError(ex.Message);
        }
    }

    private void ApplyHoldingsPayload(object? rawHoldings)
    {
        HoldingsRows.Clear();
        HoldingsItems.Clear();
        foreach (var item in ToList(rawHoldings ?? new ArrayList()))
        {
            var map = AsMap(item);
            if (map == null) continue;
            var owners = ToList(map.ContainsKey("owners") ? map["owners"] : new ArrayList()).Cast<object>().Select(x => x?.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var vm = new HoldingEditorVm
            {
                Id = S(map, "id"),
                Name = S(map, "name"),
                Type = S(map, "type"),
                Description = S(map, "description"),
                LocationName = FirstNonEmpty(S(map, "locationName"), S(map, "location")),
                Notes = S(map, "notes"),
                Status = FirstNonEmpty(S(map, "status"), S(map, "actualStatus"), S(map, "legalStatus")),
                IsPlayerVisible = !string.Equals(FirstNonEmpty(S(map, "isPlayerVisible"), "True"), "False", StringComparison.OrdinalIgnoreCase),
                IsArchived = string.Equals(FirstNonEmpty(S(map, "isArchived"), S(map, "archived")), "True", StringComparison.OrdinalIgnoreCase),
                Owners = owners
            };
            HoldingsItems.Add(vm);
            HoldingsRows.Add(vm.Preview);
        }
        if (SelectedHoldingItem == null || !HoldingsItems.Contains(SelectedHoldingItem))
            SelectedHoldingItem = HoldingsItems.FirstOrDefault();
        if (SelectedHoldingItem == null)
        {
            HoldingName = string.Empty;
            HoldingType = string.Empty;
            HoldingLocationName = string.Empty;
            HoldingStatus = string.Empty;
            HoldingDescription = string.Empty;
            HoldingNotes = string.Empty;
            HoldingIsPlayerVisible = true;
            HoldingIsArchived = false;
            HoldingOwners = string.Empty;
            NotifyHoldingEditor();
        }
        ClientLogService.Instance.Info($"selectedCharacter.holdings loaded={HoldingsItems.Count}");
        ClientLogService.Instance.Info($"holdings.list.render count={HoldingsItems.Count}");
        Notify(nameof(HoldingsItems));
    }

    private void ApplyReputationPayload(object? rawReputation)
    {
        ReputationRows.Clear();
        ReputationItems.Clear();
        foreach (var item in ToList(rawReputation ?? new ArrayList()))
        {
            var map = AsMap(item);
            if (map == null) continue;
            int.TryParse(S(map, "value"), out var value);
            var vm = new ReputationEditorVm
            {
                Id = S(map, "id"),
                ScopeType = FirstNonEmpty(S(map, "scopeType"), "Character"),
                TargetType = FirstNonEmpty(S(map, "targetType"), "Other"),
                TargetName = FirstNonEmpty(S(map, "targetName"), S(map, "groupKey")),
                Value = value,
                Notes = S(map, "notes"),
                Status = S(map, "status"),
                IsPlayerVisible = !string.Equals(FirstNonEmpty(S(map, "isPlayerVisible"), "True"), "False", StringComparison.OrdinalIgnoreCase),
                IsArchived = string.Equals(FirstNonEmpty(S(map, "isArchived"), S(map, "archived")), "True", StringComparison.OrdinalIgnoreCase)
            };
            ReputationItems.Add(vm);
            ReputationRows.Add(vm.Preview);
        }
        if (SelectedReputationItem == null || !ReputationItems.Contains(SelectedReputationItem))
            SelectedReputationItem = ReputationItems.FirstOrDefault();
        if (SelectedReputationItem == null)
        {
            ReputationScopeTypeInput = "Character";
            ReputationTargetTypeInput = "Other";
            ReputationTargetNameInput = string.Empty;
            ReputationValueInput = 0;
            ReputationStatusInput = string.Empty;
            ReputationNotesInput = string.Empty;
            ReputationIsPlayerVisibleInput = true;
            ReputationIsArchivedInput = false;
            NotifyReputationEditor();
        }
        ClientLogService.Instance.Info($"selectedCharacter.reputation loaded={ReputationItems.Count}");
        ClientLogService.Instance.Info($"reputation.list.render count={ReputationItems.Count}");
        Notify(nameof(ReputationItems));
    }

    private void ApplyCompanionsPayload(object? rawCompanions)
    {
        CompanionRows.Clear();
        CompanionItems.Clear();
        foreach (var item in ToList(rawCompanions ?? new ArrayList()))
        {
            var map = AsMap(item);
            if (map == null) continue;
            var inventoryCount = ToList(map.ContainsKey("inventory") ? map["inventory"] : new ArrayList()).Count;
            var holdingsCount = ToList(map.ContainsKey("holdings") ? map["holdings"] : new ArrayList()).Count;
            var reputationCount = ToList(map.ContainsKey("reputation") ? map["reputation"] : new ArrayList()).Count;
            var ownInventory = ToList(map.ContainsKey("inventory") ? map["inventory"] : new ArrayList()).Cast<object>().ToArray();
            var ownHoldings = ToList(map.ContainsKey("holdings") ? map["holdings"] : new ArrayList()).Cast<object>().ToArray();
            var ownReputation = ToList(map.ContainsKey("reputation") ? map["reputation"] : new ArrayList()).Cast<object>().ToArray();
            var vm = new CompanionEditorVm
            {
                Id = S(map, "id"),
                Name = S(map, "name"),
                Type = FirstNonEmpty(S(map, "type"), S(map, "species")),
                Description = S(map, "description"),
                Notes = S(map, "notes"),
                OwnerCharacterId = FirstNonEmpty(S(map, "ownerCharacterId"), SelectedCharacterId),
                OwnerDisplayName = S(map, "ownerDisplayName"),
                Status = S(map, "status"),
                IsPlayerVisible = !string.Equals(FirstNonEmpty(S(map, "isPlayerVisible"), "True"), "False", StringComparison.OrdinalIgnoreCase),
                IsArchived = string.Equals(FirstNonEmpty(S(map, "isArchived"), S(map, "archived")), "True", StringComparison.OrdinalIgnoreCase),
                OwnInventoryCount = inventoryCount,
                OwnHoldingsCount = holdingsCount,
                OwnReputationCount = reputationCount,
                OwnInventoryPayload = ownInventory,
                OwnHoldingsPayload = ownHoldings,
                OwnReputationPayload = ownReputation
            };
            CompanionItems.Add(vm);
            CompanionRows.Add(vm.Preview);
        }
        if (SelectedCompanionItem == null || !CompanionItems.Contains(SelectedCompanionItem))
            SelectedCompanionItem = CompanionItems.FirstOrDefault();
        if (SelectedCompanionItem == null)
        {
            CompanionNameInput = string.Empty;
            CompanionTypeInput = string.Empty;
            CompanionDescriptionInput = string.Empty;
            CompanionNotesInput = string.Empty;
            CompanionStatusInput = string.Empty;
            CompanionIsPlayerVisibleInput = true;
            CompanionIsArchivedInput = false;
            CompanionOwnerCharacterIdInput = SelectedCharacterId;
            CompanionOwnerDisplayNameInput = string.Empty;
            CompanionOwnCollectionsPreview = "Inventory: 0 | Holdings: 0 | Reputation: 0";
            NotifyCompanionEditor();
        }
        ClientLogService.Instance.Info($"selectedCharacter.companions loaded={CompanionItems.Count}");
        ClientLogService.Instance.Info($"companions.list.render count={CompanionItems.Count}");
        Notify(nameof(CompanionItems));
    }

    private void ApplyInventoryPayload(object? rawInventory)
    {
        InventoryRows.Clear();
        InventoryItems.Clear();
        foreach (var item in ToList(rawInventory))
        {
            var map = AsMap(item);
            if (map == null) continue;
            int.TryParse(FirstNonEmpty(S(map, "quantity"), "0"), out var quantity);
            int? durability = null;
            if (int.TryParse(FirstNonEmpty(S(map, "durabilityOrHealth"), S(map, "durability")), out var parsedDurability)) durability = parsedDurability;
            int? consumption = null;
            if (int.TryParse(S(map, "consumptionPerUse"), out var parsedConsumption)) consumption = parsedConsumption;
            int? ammo = null;
            if (int.TryParse(S(map, "ammo"), out var parsedAmmo)) ammo = parsedAmmo;
            var vm = new InventoryItemEditorVm
            {
                Id = S(map, "id"),
                Name = FirstNonEmpty(S(map, "displayName"), S(map, "name"), S(map, "label")),
                ItemDefinitionId = FirstNonEmpty(S(map, "itemDefinitionId"), S(map, "definitionId"), S(map, "itemCode")),
                DefinitionCategory = S(map, "definitionCategory"),
                DefinitionCode = FirstNonEmpty(S(map, "definitionCode"), S(map, "itemDefinitionId"), S(map, "definitionId"), S(map, "itemCode")),
                SnapshotDisplayName = S(map, "snapshotDisplayName"),
                SnapshotCategory = S(map, "snapshotCategory"),
                SnapshotDescription = S(map, "snapshotDescription"),
                Source = S(map, "source"),
                Description = S(map, "description"),
                Quantity = quantity,
                DurabilityOrHealth = durability,
                Condition = S(map, "condition"),
                Ammo = ammo ?? consumption,
                IsEquipped = string.Equals(FirstNonEmpty(S(map, "isEquipped"), S(map, "equipped")), "True", StringComparison.OrdinalIgnoreCase),
                IsPlayerVisible = !map.ContainsKey("isPlayerVisible") || string.Equals(S(map, "isPlayerVisible"), "True", StringComparison.OrdinalIgnoreCase),
                UsesAmmoOrConsumable = string.Equals(S(map, "usesAmmoOrConsumable"), "True", StringComparison.OrdinalIgnoreCase),
                ConsumptionPerUse = consumption,
                Category = S(map, "category"),
                Slot = FirstNonEmpty(S(map, "slot"), S(map, "slotId"), S(map, "properties")),
                Notes = FirstNonEmpty(S(map, "notes"), S(map, "properties"))
            };
            InventoryItems.Add(vm);
            InventoryRows.Add(vm.ListLabel);
        }

        if (SelectedInventoryItem == null || !InventoryItems.Contains(SelectedInventoryItem))
            SelectedInventoryItem = InventoryItems.FirstOrDefault();
        if (SelectedInventoryItem == null)
        {
            InventoryName = string.Empty;
            InventoryDescription = string.Empty;
            InventoryQuantity = 1;
            InventoryDurabilityOrHealth = null;
            InventoryCondition = string.Empty;
            InventoryAmmo = null;
            InventoryIsEquipped = false;
            InventoryIsPlayerVisible = true;
            InventoryUsesAmmoOrConsumable = false;
            InventoryConsumptionPerUse = null;
            InventoryCategory = string.Empty;
            InventorySlot = string.Empty;
            InventoryNotes = string.Empty;
            InventoryStatus = InventoryItems.Count == 0 ? "Инвентарь обновлён." : string.Empty;
            NotifyInventoryEditor();
        }
        ClientLogService.Instance.Info($"selectedCharacter.inventory loaded={InventoryItems.Count}");
        ClientLogService.Instance.Info($"inventory.list.render count={InventoryItems.Count}");
        InventoryStatus = $"Инвентарь обновлён.";
        Notify(nameof(InventoryItems));
    }

    private void AddInventoryItem()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        try
        {
            ClientLogService.Instance.Info("inventory.item.add requested");
            var response = _api.CharacterInventoryItemAdd(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "item", BuildInventoryRequestPayload() }
            });
            EnsureSuccess(response);
            ClientLogService.Instance.Info("inventory.item.add success");
            var createdId = InventoryItemIdFromResponse(response);
            LoadCharacterInventory();
            SelectInventoryItemById(createdId);
            RefreshCharacterCardDetails();
            InventoryStatus = "Инвентарь обновлён.";
        }
        catch (Exception ex) { InventoryStatus = ex.Message; SetConnectionError(ex.Message); }
    }

    private void AddInventoryItemFromCatalog()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || SelectedInventoryCatalogDefinition == null) return;
        try
        {
            ClientLogService.Instance.Info("inventory.item.addFromCatalog requested");
            var selected = SelectedInventoryCatalogDefinition;
            var response = _api.CharacterInventoryItemAddFromCatalog(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "itemDefinitionId", selected.Code },
                { "definitionId", selected.Code },
                { "definitionCategory", selected.Category },
                { "quantity", InventoryCatalogQuantity },
                { "isEquipped", InventoryCatalogIsEquipped },
                { "slotId", InventorySlot ?? string.Empty },
                { "isPlayerVisible", InventoryCatalogIsPlayerVisible }
            });
            EnsureSuccess(response);
            ClientLogService.Instance.Info("inventory.item.addFromCatalog success");
            var createdId = InventoryItemIdFromResponse(response);
            LoadCharacterInventory();
            SelectInventoryItemById(createdId);
            RefreshCharacterCardDetails();
            InventoryStatus = "Инвентарь обновлён.";
        }
        catch (Exception ex)
        {
            InventoryStatus = ex.Message;
            SetConnectionError(ex.Message);
        }
    }

    private void UpdateInventoryItem()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || SelectedInventoryItem == null) return;
        try
        {
            ClientLogService.Instance.Info("inventory.item.update requested");
            var selectedItemId = SelectedInventoryItem.Id;
            var response = _api.CharacterInventoryItemUpdate(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "itemId", selectedItemId },
                { "item", BuildInventoryRequestPayload() }
            });
            EnsureSuccess(response);
            ClientLogService.Instance.Info("inventory.item.update success");
            LoadCharacterInventory();
            SelectInventoryItemById(selectedItemId);
            RefreshCharacterCardDetails();
            InventoryStatus = "Предмет сохранён в Character v2 profile.";
        }
        catch (Exception ex) { InventoryStatus = ex.Message; SetConnectionError(ex.Message); }
    }

    private void RemoveInventoryItem()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || SelectedInventoryItem == null) return;
        try
        {
            ClientLogService.Instance.Info("inventory.item.remove requested");
            var response = _api.CharacterInventoryItemRemove(SelectedCharacterId, SelectedInventoryItem.Id);
            EnsureSuccess(response);
            ClientLogService.Instance.Info("inventory.item.remove success");
            LoadCharacterInventory();
            RefreshCharacterCardDetails();
            InventoryStatus = "Предмет удалён из Character v2 profile.";
        }
        catch (Exception ex) { InventoryStatus = ex.Message; SetConnectionError(ex.Message); }
    }

    private void ToggleInventoryItemEquip()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || SelectedInventoryItem == null) return;
        try
        {
            ClientLogService.Instance.Info("inventory.item.toggleEquip requested");
            var response = _api.CharacterInventoryItemToggleEquip(SelectedCharacterId, SelectedInventoryItem.Id);
            EnsureSuccess(response);
            ClientLogService.Instance.Info("inventory.item.toggleEquip success");
            LoadCharacterInventory();
            RefreshCharacterCardDetails();
            InventoryStatus = "Инвентарь обновлён.";
        }
        catch (Exception ex) { InventoryStatus = ex.Message; SetConnectionError(ex.Message); }
    }

    private void RefreshCharacterCardDetails()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        try
        {
            ClientLogService.Instance.Info($"inventory.ui.refresh.start characterId={SelectedCharacterId}");
            var response = _api.CharacterAdminHubGet(SelectedCharacterId);
            if (response.Status != ResponseStatus.Ok)
            {
                CharacterCard.MarkError(response.Message);
                ClientLogService.Instance.Warn($"inventory.ui.error characterId={SelectedCharacterId} status={response.Status} message={response.Message}");
                return;
            }

            var hub = FirstCharacterHubCard(response.Payload);
            if (hub == null)
            {
                CharacterCard.MarkError("Character v2 hub did not return a character card.");
                ClientLogService.Instance.Warn($"inventory.ui.error characterId={SelectedCharacterId} emptyHub=true");
                return;
            }

            CharacterCard.LoadFromDetails(
                BuildCharacterCardPayloadFromHub(hub),
                SelectedCharacterId,
                SelectedPlayer?.Name ?? SelectedOwnerUserId,
                VisHideDescription,
                VisHideBackstory,
                VisHideStats,
                VisHideReputation);
            ClientLogService.Instance.Info($"inventory.ui.refresh.done characterId={SelectedCharacterId}");
        }
        catch (Exception ex)
        {
            CharacterCard.MarkError(ex.Message);
            ClientLogService.Instance.Error($"inventory.ui.error characterId={SelectedCharacterId} message={ex.Message}", ex);
        }
    }

    private void RunInventoryDiagnosticsForCard()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId))
        {
            CharacterCard.MarkError("Не удалось загрузить карточку персонажа.");
            return;
        }

        try
        {
            ClientLogService.Instance.Info($"inventory.ui.diagnostics.start characterId={SelectedCharacterId}");
            var response = _api.InventoryDiagnosticsFull(SelectedCharacterId);
            if (response.Status != ResponseStatus.Ok)
            {
                CharacterCard.MarkError(response.Message);
                ClientLogService.Instance.Warn($"inventory.ui.diagnostics.disabled_or_error characterId={SelectedCharacterId} status={response.Status} message={response.Message}");
                return;
            }

            CharacterCard.ApplyInventoryDiagnostics(response.Payload, response.Message);
            ClientLogService.Instance.Info($"inventory.ui.diagnostics.done characterId={SelectedCharacterId}");
        }
        catch (Exception ex)
        {
            CharacterCard.MarkError(ex.Message);
            ClientLogService.Instance.Error($"inventory.ui.diagnostics.error characterId={SelectedCharacterId} message={ex.Message}", ex);
        }
    }

    private void AddHolding()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        try
        {
            ClientLogService.Instance.Info("holding.add requested");
            var response = _api.CharacterHoldingAdd(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "holding", BuildHoldingRequestPayload() }
            });
            EnsureSuccess(response);
            ClientLogService.Instance.Info("holding.add success");
            LoadCharacterHoldings();
        }
        catch (Exception ex) { SetConnectionError(ex.Message); }
    }

    private void UpdateHolding()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || SelectedHoldingItem == null) return;
        try
        {
            ClientLogService.Instance.Info("holding.update requested");
            var response = _api.CharacterHoldingUpdate(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "holdingId", SelectedHoldingItem.Id },
                { "holding", BuildHoldingRequestPayload() }
            });
            EnsureSuccess(response);
            ClientLogService.Instance.Info("holding.update success");
            LoadCharacterHoldings();
        }
        catch (Exception ex) { SetConnectionError(ex.Message); }
    }

    private void RemoveHolding()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || SelectedHoldingItem == null) return;
        try
        {
            ClientLogService.Instance.Info("holding.remove requested");
            var response = _api.CharacterHoldingRemove(SelectedCharacterId, SelectedHoldingItem.Id);
            EnsureSuccess(response);
            ClientLogService.Instance.Info("holding.remove success");
            LoadCharacterHoldings();
        }
        catch (Exception ex) { SetConnectionError(ex.Message); }
    }

    private void AddReputationEntry()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        try
        {
            ClientLogService.Instance.Info("reputation.add requested");
            var response = _api.CharacterReputationEntryAdd(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "entry", BuildReputationRequestPayload() }
            });
            EnsureSuccess(response);
            ClientLogService.Instance.Info("reputation.add success");
            LoadCharacterReputation();
        }
        catch (Exception ex) { SetConnectionError(ex.Message); }
    }

    private void UpdateReputationEntry()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || SelectedReputationItem == null) return;
        try
        {
            ClientLogService.Instance.Info("reputation.update requested");
            var response = _api.CharacterReputationEntryUpdate(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "entryId", SelectedReputationItem.Id },
                { "entry", BuildReputationRequestPayload() }
            });
            EnsureSuccess(response);
            ClientLogService.Instance.Info("reputation.update success");
            LoadCharacterReputation();
        }
        catch (Exception ex) { SetConnectionError(ex.Message); }
    }

    private void RemoveReputationEntry()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || SelectedReputationItem == null) return;
        try
        {
            ClientLogService.Instance.Info("reputation.remove requested");
            var response = _api.CharacterReputationEntryRemove(SelectedCharacterId, SelectedReputationItem.Id);
            EnsureSuccess(response);
            ClientLogService.Instance.Info("reputation.remove success");
            LoadCharacterReputation();
        }
        catch (Exception ex) { SetConnectionError(ex.Message); }
    }

    private void AddCompanion()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        try
        {
            ClientLogService.Instance.Info("companion.add requested");
            var response = _api.CharacterCompanionAdd(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "companion", BuildCompanionRequestPayload() }
            });
            EnsureSuccess(response);
            ClientLogService.Instance.Info("companion.add success");
            LoadCharacterCompanions();
        }
        catch (Exception ex) { SetConnectionError(ex.Message); }
    }

    private void UpdateCompanion()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || SelectedCompanionItem == null) return;
        try
        {
            ClientLogService.Instance.Info("companion.update requested");
            var response = _api.CharacterCompanionUpdate(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "companionId", SelectedCompanionItem.Id },
                { "companion", BuildCompanionRequestPayload() }
            });
            EnsureSuccess(response);
            ClientLogService.Instance.Info("companion.update success");
            LoadCharacterCompanions();
        }
        catch (Exception ex) { SetConnectionError(ex.Message); }
    }

    private void RemoveCompanion()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || SelectedCompanionItem == null) return;
        try
        {
            ClientLogService.Instance.Info("companion.remove requested");
            var response = _api.CharacterCompanionRemove(SelectedCharacterId, SelectedCompanionItem.Id);
            EnsureSuccess(response);
            ClientLogService.Instance.Info("companion.remove success");
            LoadCharacterCompanions();
        }
        catch (Exception ex) { SetConnectionError(ex.Message); }
    }

    private Dictionary<string, object> BuildInventoryRequestPayload()
    {
        var payload = new Dictionary<string, object>
        {
            { "name", InventoryName },
            { "description", InventoryDescription },
            { "quantity", Math.Max(0, InventoryQuantity) },
            { "isEquipped", InventoryIsEquipped },
            { "isPlayerVisible", InventoryIsPlayerVisible },
            { "usesAmmoOrConsumable", InventoryUsesAmmoOrConsumable },
            { "category", InventoryCategory },
            { "slot", InventorySlot },
            { "slotId", InventorySlot },
            { "condition", InventoryCondition },
            { "notes", InventoryNotes }
        };
        if (InventoryDurabilityOrHealth.HasValue) payload["durabilityOrHealth"] = InventoryDurabilityOrHealth.Value;
        if (InventoryAmmo.HasValue) payload["ammo"] = InventoryAmmo.Value;
        if (InventoryConsumptionPerUse.HasValue) payload["consumptionPerUse"] = InventoryConsumptionPerUse.Value;
        return payload;
    }

    private Dictionary<string, object> BuildHoldingRequestPayload()
    {
        var ownerValues = HoldingOwners.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray();
        return new Dictionary<string, object>
        {
            { "name", HoldingName },
            { "type", HoldingType },
            { "description", HoldingDescription },
            { "locationName", HoldingLocationName },
            { "status", HoldingStatus },
            { "actualStatus", HoldingStatus },
            { "notes", HoldingNotes },
            { "isPlayerVisible", HoldingIsPlayerVisible },
            { "archived", HoldingIsArchived },
            { "isArchived", HoldingIsArchived },
            { "owners", ownerValues }
        };
    }

    private Dictionary<string, object> BuildReputationRequestPayload()
    {
        return new Dictionary<string, object>
        {
            { "scope", ReputationScopeTypeInput == "Group" ? "Group" : "Character" },
            { "scopeType", ReputationScopeTypeInput },
            { "groupKey", ReputationScopeTypeInput == "Group" ? ReputationTargetNameInput : string.Empty },
            { "targetType", ReputationTargetTypeInput },
            { "targetName", ReputationTargetNameInput },
            { "value", ReputationValueInput },
            { "status", ReputationStatusInput },
            { "notes", ReputationNotesInput },
            { "isPlayerVisible", ReputationIsPlayerVisibleInput },
            { "archived", ReputationIsArchivedInput },
            { "isArchived", ReputationIsArchivedInput },
            { "isHiddenForOthers", !ReputationIsPlayerVisibleInput }
        };
    }

    private Dictionary<string, object> BuildCompanionRequestPayload()
    {
        var ownInventory = SelectedCompanionItem?.OwnInventoryPayload ?? Array.Empty<object>();
        var ownHoldings = SelectedCompanionItem?.OwnHoldingsPayload ?? Array.Empty<object>();
        var ownReputation = SelectedCompanionItem?.OwnReputationPayload ?? Array.Empty<object>();
        return new Dictionary<string, object>
        {
            { "name", CompanionNameInput },
            { "type", CompanionTypeInput },
            { "species", CompanionTypeInput },
            { "description", CompanionDescriptionInput },
            { "notes", CompanionNotesInput },
            { "ownerCharacterId", FirstNonEmpty(CompanionOwnerCharacterIdInput, SelectedCharacterId) },
            { "ownerDisplayName", CompanionOwnerDisplayNameInput },
            { "status", CompanionStatusInput },
            { "isPlayerVisible", CompanionIsPlayerVisibleInput },
            { "isArchived", CompanionIsArchivedInput },
            { "inventory", ownInventory },
            { "holdings", ownHoldings },
            { "reputation", ownReputation }
        };
    }

    private void NotifyInventoryEditor()
    {
        Notify(nameof(InventoryName));
        Notify(nameof(InventoryDescription));
        Notify(nameof(InventoryQuantity));
        Notify(nameof(InventoryDurabilityOrHealth));
        Notify(nameof(InventoryCondition));
        Notify(nameof(InventoryAmmo));
        Notify(nameof(InventoryIsEquipped));
        Notify(nameof(InventoryIsPlayerVisible));
        Notify(nameof(InventoryUsesAmmoOrConsumable));
        Notify(nameof(InventoryConsumptionPerUse));
        Notify(nameof(InventoryCategory));
        Notify(nameof(InventorySlot));
        Notify(nameof(InventoryNotes));
        Notify(nameof(InventoryCatalogSearch));
        Notify(nameof(InventoryCatalogCategoryFilter));
        Notify(nameof(InventoryCatalogQuantity));
        Notify(nameof(InventoryCatalogIsEquipped));
        Notify(nameof(InventoryCatalogIsPlayerVisible));
        Notify(nameof(InventorySelectedCatalogDefinitionSummary));
        Notify(nameof(InventoryStatus));
    }

    private string InventoryItemIdFromResponse(ResponseEnvelope response)
    {
        if (response?.Payload == null) return string.Empty;
        if (response.Payload.TryGetValue("itemId", out var directId)) return directId?.ToString() ?? string.Empty;
        if (!response.Payload.ContainsKey("item")) return string.Empty;
        var map = AsMap(response.Payload["item"]);
        return map == null ? string.Empty : FirstNonEmpty(S(map, "id"), S(map, "itemId"));
    }

    private void SelectInventoryItemById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        var item = InventoryItems.FirstOrDefault(x => string.Equals(x.Id, itemId, StringComparison.OrdinalIgnoreCase));
        if (item != null) SelectedInventoryItem = item;
    }

    private void NotifyHoldingEditor()
    {
        Notify(nameof(HoldingName));
        Notify(nameof(HoldingType));
        Notify(nameof(HoldingLocationName));
        Notify(nameof(HoldingStatus));
        Notify(nameof(HoldingDescription));
        Notify(nameof(HoldingNotes));
        Notify(nameof(HoldingIsPlayerVisible));
        Notify(nameof(HoldingIsArchived));
        Notify(nameof(HoldingOwners));
    }

    private void NotifyReputationEditor()
    {
        Notify(nameof(ReputationScopeTypeInput));
        Notify(nameof(ReputationTargetTypeInput));
        Notify(nameof(ReputationTargetNameInput));
        Notify(nameof(ReputationValueInput));
        Notify(nameof(ReputationStatusInput));
        Notify(nameof(ReputationNotesInput));
        Notify(nameof(ReputationIsPlayerVisibleInput));
        Notify(nameof(ReputationIsArchivedInput));
    }

    private void NotifyCompanionEditor()
    {
        Notify(nameof(CompanionNameInput));
        Notify(nameof(CompanionTypeInput));
        Notify(nameof(CompanionDescriptionInput));
        Notify(nameof(CompanionNotesInput));
        Notify(nameof(CompanionStatusInput));
        Notify(nameof(CompanionIsPlayerVisibleInput));
        Notify(nameof(CompanionIsArchivedInput));
        Notify(nameof(CompanionOwnerCharacterIdInput));
        Notify(nameof(CompanionOwnerDisplayNameInput));
        Notify(nameof(CompanionOwnCollectionsPreview));
    }

    private void LoadPendingRequests()
    {
        PendingRequests.Clear();
        var payload = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(RequestStatusFilter)) payload["status"] = RequestStatusFilter;
        payload["includeArchived"] = string.Equals(RequestStatusFilter, "archived", StringComparison.OrdinalIgnoreCase);
        var r = _api.AdminRequestList(payload);
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var obj in ToList(r.Payload["items"]))
        {
            var m = AsMap(obj);
            if (m == null) continue;
            var type = S(m, "requestType");
            if (!string.IsNullOrWhiteSpace(RequestTypeFilter) && !string.Equals(RequestTypeFilter, type, StringComparison.OrdinalIgnoreCase)) continue;
            var player = FirstNonEmptyAdmin(S(m, "submittedByDisplayName"), S(m, "createdByDisplayName"), S(m, "creatorLogin"), "—");
            var lastAction = FirstNonEmptyAdmin(S(m, "lastActionDisplayText"), "Нет данных");
            var character = FirstNonEmptyAdmin(S(m, "characterDisplayName"), S(m, "characterId"));
            var details = FirstNonEmptyAdmin(S(m, "details"), S(m, "description"), S(m, "gmResponse"));
            PendingRequests.Add(new RowVm
            {
                Id = S(m, "requestId"),
                DisplayId = FormatRequestNumberForDisplay(S(m, "requestNumber"), S(m, "displayRequestId"), S(m, "requestNumberLabel")),
                Name = FirstNonEmptyAdmin(S(m, "title"), S(m, "name"), type),
                State = S(m, "status"),
                Extra = $"Нет данных"
            });
        }
        ClientLogService.Instance.Debug($"ui-refresh section=... block=... raw={ToList(r.Payload["items"]).Count} shown={PendingRequests.Count}");
        RestoreSelection(PendingRequests, SelectedPendingRequestId, value => SelectedPendingRequestId = value);
        if (!string.IsNullOrWhiteSpace(SelectedPendingRequestId) && PendingRequests.Any(row => row.Id == SelectedPendingRequestId))
            LoadSelectedRequestDetails();
        RefreshConnectionSummary();
    }

    private void LoadSelectedRequestDetails()
    {
        if (string.IsNullOrWhiteSpace(SelectedPendingRequestId)) return;
        var r = _api.AdminRequestGet(SelectedPendingRequestId);
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("item")) return;
        var m = AsMap(r.Payload["item"]);
        if (m == null) return;
        ApplySelectedRequestDetails(m);
    }

    private void ApplySelectedRequestDetails(Dictionary<string, object> m)
    {
        var requestNumber = FormatRequestNumberForDisplay(S(m, "requestNumber"), S(m, "displayRequestId"), S(m, "requestNumberLabel"));
        _selectedRequestDetailsId = FirstNonEmptyAdmin(S(m, "requestId"), S(m, "id"), SelectedPendingRequestId);
        AdminSelectedRequestTitle = $"{requestNumber} — {FirstNonEmptyAdmin(S(m, "title"), S(m, "name"), "Заявка без названия")}";
        AdminSelectedRequestPlayer = "Без названия" + FirstNonEmptyAdmin(S(m, "submittedByDisplayName"), S(m, "createdByDisplayName"), S(m, "creatorLogin"), "—");
        AdminSelectedRequestCharacter = "Персонаж: " + FirstNonEmptyAdmin(S(m, "characterDisplayName"), S(m, "characterId"), "—");
        AdminSelectedRequestType = "Тип / статус: " + FirstNonEmptyAdmin(S(m, "requestType"), S(m, "type"), "—") + " / " + FirstNonEmptyAdmin(S(m, "status"), "—");
        AdminSelectedRequestActors = FirstNonEmptyAdmin(S(m, "lastActionDisplayText"), "Последнее действие: —");
        AdminSelectedRequestDetails = FirstNonEmptyAdmin(S(m, "details"), S(m, "description"), "Данные заявки не загружены.");
        Notify(nameof(AdminSelectedRequestTitle));
        Notify(nameof(AdminSelectedRequestPlayer));
        Notify(nameof(AdminSelectedRequestCharacter));
        Notify(nameof(AdminSelectedRequestType));
        Notify(nameof(AdminSelectedRequestActors));
        Notify(nameof(AdminSelectedRequestDetails));

        RequestHistoryRows.Clear();
        foreach (var audit in ToList(m.TryGetValue("auditTrail", out var rawAudit) ? rawAudit : new ArrayList()))
        {
            var row = AsMap(audit);
            if (row == null) continue;
            var actor = FirstNonEmptyAdmin(S(row, "actorDisplayName"), "—");
            RequestHistoryRows.Add($"{S(row, "timestampUtc")} | {actor} | {S(row, "action")} | {S(row, "fromStatus")} -> {S(row, "toStatus")} | {S(row, "summary")}");
        }

        if (RequestHistoryRows.Count == 0)
            RequestHistoryRows.Add("История заявки пока пуста.");
    }

    private void ApplySelectedRequestDetails(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok || !response.Payload.ContainsKey("item")) return;
        var map = AsMap(response.Payload["item"]);
        if (map != null) ApplySelectedRequestDetails(map);
    }

    private void LoadRequestHistory()
    {
        RequestHistoryRows.Clear();
        var r = _api.RequestHistory();
        if (r.Status == ResponseStatus.Ok && r.Payload.ContainsKey("items"))
        {
            foreach (var obj in ToList(r.Payload["items"]))
                if (obj is Dictionary<string, object> m)
                    RequestHistoryRows.Add($"{FormatRequestNumberForDisplay(S(m, "requestNumber"), S(m, "displayRequestId"), S(m, "requestNumberLabel"))} | {S(m, "status")} | {S(m, "requestType")} | {FirstNonEmptyAdmin(S(m, "title"), S(m, "formula"), S(m, "description"))}");
        }

        DiceFeedRows.Clear();
        ClientLogService.Instance.Debug("dice.feed.refresh requested");
        var feed = _api.DiceVisibleFeed();
        if (feed.Status == ResponseStatus.Ok && feed.Payload.ContainsKey("items"))
        {
            var rawItems = ToList(feed.Payload["items"]);
            ClientLogService.Instance.Debug($"dice.feed.refresh itemsRaw={rawItems.Count}");
            var mappedItems = 0;
            foreach (var obj in rawItems)
            {
                var m = AsMap(obj);
                if (m == null) continue;
                mappedItems++;
                var total = "?";
                if (m.ContainsKey("result"))
                {
                    var result = AsMap(m["result"]);
                    if (result != null) total = FirstNonEmpty(S(result, "total"), "?");
                }
                var creator = FirstNonEmpty(S(m, "creatorLogin"), S(m, "creatorUserId"));
                var isTest = string.Equals(S(m, "isTestRoll"), "True", StringComparison.OrdinalIgnoreCase);
                var label = isTest ? "[тест] " : string.Empty;
                var rolls = BuildDiceRollDetails(m, CommandNames.DiceVisibleFeed);
                var comment = BuildDiceCommentSuffix(m);
                DiceFeedRows.Add($"{creator} | {label}{S(m, "formula")} = {total}{rolls} | {S(m, "visibility")}{comment}");
            }
            ClientLogService.Instance.Debug($"dice.feed.refresh itemsMapped={mappedItems}");
        }
        ClientLogService.Instance.Debug($"dice.feed.render visibleRows={DiceFeedRows.Count}");
        MergeDiceIntoChatFeed();
        RefreshConnectionSummary();
    }

    private void CombatStart()
    {
        var participants = new[] { new Dictionary<string, object> { { "kind", "Npc" }, { "entityId", "npc-1" }, { "displayName", "NPC-1" }, { "ownerUserId", "" } } };
        _api.CombatStart(CombatSessionId, participants);
        CombatRefresh();
    }

    private void CombatEnd() { _api.CombatEnd(CombatSessionId); CombatRefresh(); }
    private void CombatNextTurn() { _api.CombatNextTurn(CombatSessionId); CombatRefresh(); }
    private void CombatPrevTurn() { _api.CombatPreviousTurn(CombatSessionId); CombatRefresh(); }
    private void CombatNextRound() { _api.CombatNextRound(CombatSessionId); CombatRefresh(); }
    private void CombatSkipTurn() { _api.CombatSkipTurn(CombatSessionId); CombatRefresh(); }

    private void CombatAddParticipant()
    {
        var participants = new[] { new Dictionary<string, object> { { "kind", NewParticipantKind }, { "entityId", Guid.NewGuid().ToString("N") }, { "displayName", NewParticipantName }, { "ownerUserId", "" } } };
        _api.CombatAddParticipant(CombatSessionId, participants);
        CombatRefresh();
    }

    private void CombatRemoveParticipant()
    {
        if (string.IsNullOrWhiteSpace(SelectedCombatParticipantId)) return;
        _api.CombatRemoveParticipant(CombatSessionId, SelectedCombatParticipantId);
        CombatRefresh();
    }

    private void CombatDetachCompanion()
    {
        if (string.IsNullOrWhiteSpace(SelectedCombatParticipantId)) return;
        _api.CombatDetachCompanion(CombatSessionId, SelectedCombatParticipantId);
        CombatRefresh();
    }

    private void CombatRefresh()
    {
        CombatRows.Clear();
        CombatParticipantRows.Clear();
        var state = _api.CombatGetState(CombatSessionId);
        if (state.Status == ResponseStatus.Ok)
        {
            CombatRows.Add($"Status: {S(state.Payload, "status")}");
            CombatRows.Add($"Round: {S(state.Payload, "round")}");
            CombatRows.Add($"TurnIndex: {S(state.Payload, "turnIndex")}");
            CombatRows.Add($"ActiveSlot: {S(state.Payload, "activeSlotId")}");
            foreach (var item in ToList(state.Payload.ContainsKey("participants") ? state.Payload["participants"] : new ArrayList()))
            {
                if (item is Dictionary<string, object> m)
                {
                    var participantId = S(m, "participantId");
                    var displayName = S(m, "displayName");
                    var kind = S(m, "kind");
                    var extra = $"roll={S(m, "baseRoll")} • st={S(m, "status")}";
                    CombatRows.Add($"P:{participantId} {displayName} {kind} {extra}");
                    CombatParticipantRows.Add(new RowVm { Id = participantId, Name = displayName, State = kind, Extra = extra });
                }
            }
        }
        RestoreSelection(CombatParticipantRows, SelectedCombatParticipantId, value => SelectedCombatParticipantId = value);

        CombatHistoryRows.Clear();
        var history = _api.CombatGetHistory(CombatSessionId);
        if (history.Status == ResponseStatus.Ok && history.Payload.ContainsKey("items"))
        {
            foreach (var item in ToList(history.Payload["items"]))
            {
                if (item is Dictionary<string, object> m)
                    CombatHistoryRows.Add($"{S(m, "at")} | {S(m, "eventType")} | {S(m, "message")}");
            }
        }
        RefreshConnectionSummary();
    }

    private void DefinitionsReload()
    {
        var r = EnsureSuccess(_api.DefinitionsReload());
        DefinitionVersionText = FirstNonEmpty(S(r.Payload, "version"), DefinitionVersionText);
        Notify(nameof(DefinitionVersionText));
    }

    private void RefreshDefinitionClasses()
    {
        ClassDefinitionRows.Clear();
        ClientLogService.Instance.Info($"definitions.classes.get requested branch={ClassBranchFilter} search={ClassSearchText} includeArchived=true");
        var response = _api.DefinitionsClassesGetContent(ClassBranchFilter, "", ClassSearchText, true);
        ClientLogService.Instance.Info($"definitions.classes.get response status={response.Status} message={response.Message}");
        var payloadKeys = string.Join(",", response.Payload.Keys.OrderBy(key => key, StringComparer.Ordinal));
        ClientLogService.Instance.Info($"definitions.classes.get payload.keys={payloadKeys}");
        EnsureSuccess(response);

                object rawItemsObject = response.Payload.ContainsKey("items") ? response.Payload["items"] : new ArrayList();
        var rawItemsType = rawItemsObject == null ? "null" : rawItemsObject.GetType().FullName;
        ClientLogService.Instance.Info($"definitions.classes.get payload.items.type={rawItemsType}");

        var rawItems = ExtractSkillDefinitionItems(response.Payload, out var rawCollectionKey);
        var added = 0;

        foreach (var item in rawItems)
        {
            var map = AsMap(item, CommandNames.DefinitionsClassesGet);
            if (map == null)
            {
                ClientLogService.Instance.Warn($"definitions.classes.get skipped item type={item?.GetType().FullName ?? "null"}");
                continue;
            }

            ClassDefinitionRows.Add(new RowVm
            {
                Id = S(map, "code"),
                Name = FirstNonEmpty(S(map, "displayName"), S(map, "name"), S(map, "code")),
                State = $"node={S(map, "requiredNodeId")}",
                Extra = $"hexagon={FirstNonEmpty(S(map, "requiredHexagonId"), "main_development_hexagon")}; lockedOutsideHexagon={FirstNonEmpty(S(map, "isLockedOutsideHexagon"), "True")}; {S(map, "description")}" 
            });

            added++;
        }

        ClientLogService.Instance.Info($"definitions.classes.get rawCollectionKey={rawCollectionKey}");
        ClientLogService.Instance.Info($"definitions.classes.get rawCount={rawItems.Count}");
        ClientLogService.Instance.Info($"definitions.classes.get added={added}");

ClientLogService.Instance.Debug($"ui-refresh section=... block=... loaded={ClassDefinitionRows.Count} visible={FilteredClassDefinitionRows.Count()}");
        RestoreSelection(ClassDefinitionRows, SelectedClassDefinitionCode, value => SelectedClassDefinitionCode = value);
        Notify(nameof(ContentSummary));
        Notify(nameof(SelectedClassDefinition));
        Notify(nameof(SelectedClassSummary));
        Notify(nameof(SelectedContentSummary));
    }

    private void OpenSelectedClassDefinition()
    {
        if (string.IsNullOrWhiteSpace(SelectedClassDefinitionCode)) return;
        var response = EnsureSuccess(_api.DefinitionClassGet(SelectedClassDefinitionCode));
        if (!response.Payload.TryGetValue("item", out var item) || item is not Dictionary<string, object> map) return;
        ApplyClassDefinitionEditor(map);
    }

    private void NewClassDefinition()
    {
        SelectedClassDefinitionCode = string.Empty;
        EditClassCode = string.Empty;
        EditClassName = string.Empty;
        EditClassDescription = string.Empty;
        EditClassDirectionCode = string.Empty;
        EditClassBranchCode = string.Empty;
        EditClassRootClassCode = string.Empty;
        EditClassParentClassCode = string.Empty;
        EditClassRequiredHexagonId = "main_development_hexagon";
        EditClassRequiredNodeId = string.Empty;
        EditClassVisibilityRule = "hexagon-gated";
        EditClassIsPlayerVisible = false;
        EditClassIsLockedOutsideHexagon = true;
        EditClassTags = string.Empty;
        EditClassSortOrder = 0;
        EditClassLevel = 1;
        EditClassGrantedSkillCodes = string.Empty;
        EditClassRequiredClassCodes = string.Empty;
        EditClassIsActive = true;
        EditClassStatus = DefinitionStatus.Draft.ToString();
        NotifyClassDefinitionEditor();
    }

    private void SaveClassDefinition()
    {
        var payload = BuildClassDefinitionPayload();
        AttachExpectedRevision(payload, "definition:class", FirstNonEmpty(EditClassCode, SelectedClassDefinitionCode), CommandNames.DefinitionsClassSave);
        var response = EnsureSuccess(_api.DefinitionClassSave(payload));
        UpdateRevisionAfterDefinitionResponse(response, "definition:class", FirstNonEmpty(EditClassCode, SelectedClassDefinitionCode), CommandNames.DefinitionsClassSave);
        if (response.Payload.TryGetValue("item", out var item) && item is Dictionary<string, object> map)
        {
            ApplyClassDefinitionEditor(map);
        }
        RefreshDefinitionClasses();
    }

    private void ArchiveClassDefinition()
    {
        var code = FirstNonEmpty(SelectedClassDefinitionCode, EditClassCode);
        if (string.IsNullOrWhiteSpace(code)) return;
        var response = EnsureSuccess(SendDefinitionArchiveWithRevision(CommandNames.DefinitionsClassArchive, code));
        UpdateRevisionAfterDefinitionResponse(response, "definition:class", code, CommandNames.DefinitionsClassArchive);
        RefreshDefinitionClasses();
        if (string.Equals(EditClassCode, code, StringComparison.OrdinalIgnoreCase))
        {
            OpenSelectedClassDefinition();
        }
    }

    private void RefreshDefinitionSkills()
    {
        SkillDefinitionRows.Clear();
        ClientLogService.Instance.Info("skillDefinitions.content.load requested");
        var response = EnsureSuccess(_api.DefinitionsSkillsGetContent(SkillCategoryFilter, SkillSearchText, true));
        var payloadKeys = string.Join(",", response.Payload.Keys.OrderBy(key => key, StringComparer.Ordinal));
        var rawItems = ExtractSkillDefinitionItems(response.Payload, out var rawCollectionKey);
        var mappedCount = 0;
        string firstRowCode = string.Empty;
        foreach (var item in rawItems)
        {
            var map = AsMap(item, CommandNames.DefinitionsSkillsGet);
            if (map == null) continue;
            var status = FirstNonEmpty(S(map, "status"), "Draft");
            var isArchived = string.Equals(status, DefinitionStatus.Archived.ToString(), StringComparison.OrdinalIgnoreCase);
            var code = S(map, "code");
            SkillDefinitionRows.Add(new RowVm
            {
                Id = code,
                Name = FirstNonEmpty(S(map, "displayName"), S(map, "name"), code),
                State = FirstNonEmpty(S(map, "category"), S(map, "displayGroup")),
                Extra = S(map, "description")
            });
            mappedCount++;
            if (string.IsNullOrWhiteSpace(firstRowCode)) firstRowCode = code;
        }
        ClientLogService.Instance.Info($"skillDefinitions.content.response.keys={payloadKeys}");
        ClientLogService.Instance.Info($"skillDefinitions.content.rawCollectionKey={rawCollectionKey}");
        ClientLogService.Instance.Info($"skillDefinitions.content.rawCount={rawItems.Count}");
        ClientLogService.Instance.Info($"skillDefinitions.content.mappedCount={mappedCount}");
        ClientLogService.Instance.Info($"skillDefinitions.content.firstRow.code={FirstNonEmpty(firstRowCode, "<none>")}");
        ClientLogService.Instance.Debug($"ui-refresh section= block= loaded={SkillDefinitionRows.Count} visible={FilteredSkillDefinitionRows.Count()}");
        ClientLogService.Instance.Info($"skillDefinitions.content.load count={SkillDefinitionRows.Count}");
        ClientLogService.Instance.Info($"skillDefinitions.render count={SkillDefinitionRows.Count}");
        RestoreSelection(SkillDefinitionRows, SelectedSkillDefinitionCode, value => SelectedSkillDefinitionCode = value);
        TraceSkillDefinitionContentButtons();
        Notify(nameof(ContentSummary));
        Notify(nameof(SelectedSkillDefinition));
        Notify(nameof(SelectedSkillSummary));
        Notify(nameof(SelectedContentSummary));
    }

    private void OpenSelectedSkillDefinition()
    {
        if (string.IsNullOrWhiteSpace(SelectedSkillDefinitionCode)) return;
        var response = EnsureSuccess(_api.DefinitionSkillGet(SelectedSkillDefinitionCode));
        if (!response.Payload.TryGetValue("item", out var item) || item is not Dictionary<string, object> map) return;
        ApplySkillDefinitionEditor(map);
    }

    private void NewSkillDefinition()
    {
        var code = FirstNonEmpty(EditSkillCode);
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Заполните обязательные поля.");
        if (string.IsNullOrWhiteSpace(EditSkillName)) throw new ArgumentException("Заполните обязательные поля.");
        ClientLogService.Instance.Info($"skillDefinition.create begin code={code}");
        var dto = BuildSkillDefinitionPayload();
        var dtoLevelsCount = ToList(dto.ContainsKey("levels") ? dto["levels"] : new ArrayList()).Count;
        ClientLogService.Instance.Info($"skillDefinition.create dtoBuilt code={FirstNonEmpty(S(dto, "code"), code)} name={S(dto, "name")} sourceType={S(dto, "skillCategory")} maxLevel={S(dto, "maxLevel")} levels={dtoLevelsCount}");
        var payload = new Dictionary<string, object> { { "definition", dto } };
        ClientLogService.Instance.Info($"skillDefinition.create payloadHasDefinition={payload.ContainsKey("definition").ToString().ToLowerInvariant()} payloadKeys={string.Join(",", payload.Keys)}");

        SelectedSkillDefinitionCode = string.Empty;
        var response = EnsureSuccess(_api.DefinitionSkillSavePayload(payload));
        ClientLogService.Instance.Info($"skillDefinition.create code={code} response={response.Status}");
        if (response.Payload.TryGetValue("item", out var item) && item is Dictionary<string, object> map)
        {
            ApplySkillDefinitionEditor(map);
        }
        RefreshDefinitionSkills();
        TraceSkillDefinitionContentButtons();
    }

    private void SaveSkillDefinition()
    {
        if (string.IsNullOrWhiteSpace(SelectedSkillDefinitionCode))
            throw new InvalidOperationException("Заполните обязательные поля.");
        var code = FirstNonEmpty(EditSkillCode, SelectedSkillDefinitionCode);
        var dto = BuildSkillDefinitionPayload();
        var dtoLevels = ToList(dto.ContainsKey("levels") ? dto["levels"] : new ArrayList()).OfType<Dictionary<string, object>>().ToList();
        var firstLevel = dtoLevels.FirstOrDefault();
        ClientLogService.Instance.Info(
            $"skillDefinition.save dtoBuilt code={FirstNonEmpty(S(dto, "code"), code)} maxLevel={S(dto, "maxLevel")} levels_count={dtoLevels.Count} levels_item_keys={string.Join(",", firstLevel?.Keys?.ToArray() ?? Array.Empty<string>())}");
        var payload = new Dictionary<string, object> { { "definition", dto } };
        AttachExpectedRevision(payload, "definition:skill", code, CommandNames.DefinitionsSkillSave);
        ClientLogService.Instance.Info($"skillDefinition.save payloadHasDefinition={payload.ContainsKey("definition").ToString().ToLowerInvariant()} payloadKeys={string.Join(",", payload.Keys)}");
        var response = EnsureSuccess(_api.DefinitionSkillSavePayload(payload));
        UpdateRevisionAfterDefinitionResponse(response, "definition:skill", code, CommandNames.DefinitionsSkillSave);
        ClientLogService.Instance.Info($"skillDefinition.save code={code} response={response.Status}");
        if (response.Payload.TryGetValue("item", out var item) && item is Dictionary<string, object> map)
        {
            ApplySkillDefinitionEditor(map);
        }
        RefreshDefinitionSkills();
        TraceSkillDefinitionContentButtons();
    }

    private void ArchiveSkillDefinition()
    {
        var code = FirstNonEmpty(SelectedSkillDefinitionCode, EditSkillCode);
        if (string.IsNullOrWhiteSpace(code)) return;
        var response = EnsureSuccess(SendDefinitionArchiveWithRevision(CommandNames.DefinitionsSkillArchive, code));
        ClientLogService.Instance.Info($"skillDefinition.archive code={code} response={response.Status}");
        UpdateRevisionAfterDefinitionResponse(response, "definition:skill", code, CommandNames.DefinitionsSkillArchive);
        RefreshDefinitionSkills();
        if (string.Equals(EditSkillCode, code, StringComparison.OrdinalIgnoreCase))
        {
            OpenSelectedSkillDefinition();
        }
        TraceSkillDefinitionContentButtons();
    }

    private void AddSkillLevel()
    {
        SkillLevelEditorRows.Add(new SkillLevelEditorRowVm { Level = SkillLevelEditorRows.Count + 1, Description = string.Empty });
        EditSkillMaxLevel = Math.Max(EditSkillMaxLevel, SkillLevelEditorRows.Count);
        Notify(nameof(EditSkillMaxLevel));
        Notify(nameof(SkillEditorHintText));
    }

    private void RemoveSkillLevel()
    {
        if (SkillLevelEditorRows.Count == 0) return;
        SkillLevelEditorRows.RemoveAt(SkillLevelEditorRows.Count - 1);
        for (var index = 0; index < SkillLevelEditorRows.Count; index++) SkillLevelEditorRows[index].Level = index + 1;
        EditSkillMaxLevel = Math.Max(1, SkillLevelEditorRows.Count);
        Notify(nameof(EditSkillMaxLevel));
        Notify(nameof(SkillEditorHintText));
    }

    private void TraceSkillDefinitionContentButtons()
    {
        var signature = $"{CanCreateSkillDefinition}|{CanSaveSkillDefinition}|{CanArchiveSkillDefinition}|{CanRefreshSkillDefinitions}";
        if (string.Equals(signature, _skillDefinitionsContentButtonsSignature, StringComparison.Ordinal))
            return;
        _skillDefinitionsContentButtonsSignature = signature;
        ClientLogService.Instance.Info($"skillDefinitions.content.new enabled={CanCreateSkillDefinition.ToString().ToLowerInvariant()}");
        ClientLogService.Instance.Info($"skillDefinitions.content.save enabled={CanSaveSkillDefinition.ToString().ToLowerInvariant()}");
        ClientLogService.Instance.Info($"skillDefinitions.content.archive enabled={CanArchiveSkillDefinition.ToString().ToLowerInvariant()}");
        ClientLogService.Instance.Info($"skillDefinitions.content.refresh enabled={CanRefreshSkillDefinitions.ToString().ToLowerInvariant()}");
    }

    private void SetClassNodeLayoutText(ref string field, string? value, string propertyName)
    {
        var normalized = value ?? string.Empty;
        if (string.Equals(field, normalized, StringComparison.Ordinal))
        {
            return;
        }

        field = normalized;
        if (!_isLoadingClassNodeLayoutEditor)
        {
            _isClassNodeLayoutDirty = true;
            UpdateDevelopmentLayoutChangedObjects();
        }

        Notify(propertyName);
    }

    private void SetClassNodeLayoutBool(ref bool field, bool value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        if (!_isLoadingClassNodeLayoutEditor)
        {
            _isClassNodeLayoutDirty = true;
            UpdateDevelopmentLayoutChangedObjects();
        }

        Notify(propertyName);
    }

    private void LoadClassTree()
    {
        ClassTreeItems.Clear();
        _classNodeLayoutPayloads.Clear();
        _developmentLayoutHexagonPayloads.Clear();
        LoadDevelopmentLayoutDefinitions();

        if (!string.IsNullOrWhiteSpace(SelectedCharacterId))
        {
            var tree = _api.ClassTreeGet(SelectedCharacterId);
            if (tree.Status == ResponseStatus.Ok)
            {
                DefinitionVersionText = S(tree.Payload, "definitionVersion");
                foreach (var d in ToList(tree.Payload.ContainsKey("directions") ? tree.Payload["directions"] : new ArrayList()))
                {
                    var dm = AsMap(d, CommandNames.ClassTreeGet);
                    if (dm == null) continue;
                    var directionId = S(dm, "directionId");
                    var branchId = S(dm, "selectedBranchId");
                    ClassTreeItems.Add(new RowVm { Id = directionId, Name = $"Direction {directionId}", State = "Branch", Extra = $"selectedBranch={branchId}" });
                    foreach (var n in ToList(dm.ContainsKey("acquiredNodes") ? dm["acquiredNodes"] : new ArrayList()))
                        if (AsMap(n, CommandNames.ClassTreeGet) is { } nm)
                            ClassTreeItems.Add(new RowVm { Id = S(nm, "nodeId"), Name = S(nm, "nodeId"), State = "Acquired", Extra = $"acquiredAt={S(nm, "acquiredAt")}" });
                }
            }

            var available = _api.DevelopmentAdminHexagonGet(new Dictionary<string, object> { { "characterId", SelectedCharacterId } });
            if (available.Status == ResponseStatus.Ok && available.Payload.ContainsKey("items"))
            {
                StoreDevelopmentHexagonPayload(available.Payload, DevelopmentHexagonIds.Main);
                foreach (var d in ToList(available.Payload["items"]))
                {
                    var dm = AsMap(d, CommandNames.ClassTreeAvailableGet);
                    if (dm == null) continue;
                    var nodeId = S(dm, "nodeId");
                    if (string.IsNullOrWhiteSpace(nodeId)) continue;
                    StoreClassNodeLayoutPayload(dm, DevelopmentHexagonIds.Main);
                    if (ClassTreeItems.Any(row => string.Equals(row.Id, nodeId, StringComparison.OrdinalIgnoreCase))) continue;
                    var state = FirstNonEmpty(S(dm, "state"), S(dm, "status"), S(dm, "available") == "True" ? "Available" : "Locked");
                    ClassTreeItems.Add(new RowVm
                    {
                        Id = nodeId,
                        Name = FirstNonEmpty(S(dm, "name"), nodeId),
                        State = state,
                        Extra = $"hexagon={FirstNonEmpty(S(dm, "hexagonId"), "main_development_hexagon")}; type={FirstNonEmpty(S(dm, "nodeTypeLabel"), S(dm, "nodeType"))}; pos=({FirstNonEmpty(S(dm, "positionX"), S(dm, "gridX"))},{FirstNonEmpty(S(dm, "positionY"), S(dm, "gridY"))}); ring={S(dm, "ring")}; sector={S(dm, "sector")}; branch={FirstNonEmpty(S(dm, "branchCode"), S(dm, "branchId"))}; required={S(dm, "requirementSummary")}; class={FirstNonEmpty(S(dm, "linkedClassId"), S(dm, "classId"))}"
                    });
                }
            }
        }

        RestoreSelection(ClassTreeItems, SelectedClassNodeId, value => SelectedClassNodeId = value);
        LoadSelectedClassNodeLayoutEditor();
        RefreshDevelopmentLayoutEditorFromPayloads();
        Notify(nameof(DefinitionVersionText));
    }

    private void SelectDevelopmentLayoutHexagon(string? hexagonId)
    {
        if (string.IsNullOrWhiteSpace(hexagonId)) return;

        if (!DevelopmentLayoutHexagons.Any(hexagon =>
                string.Equals(hexagon.HexagonId, hexagonId, StringComparison.OrdinalIgnoreCase)))
        {
            DevelopmentLayoutStatusText = $"Шестиугольник не загружен: {hexagonId}.";
            Notify(nameof(DevelopmentLayoutStatusText));
            return;
        }

        SelectedDevelopmentLayoutHexagonId = hexagonId;
        Notify(nameof(DevelopmentLayoutTreeModeOverlayText));
    }

    private void LoadDevelopmentLayoutDefinitions()
    {
        var listResponse = _api.DevelopmentHexagonAdminList(new Dictionary<string, object>());
        if (listResponse.Status != ResponseStatus.Ok)
        {
            DevelopmentLayoutStatusText = FirstNonEmpty(listResponse.Message, "Визуальная раскладка готова.");
            return;
        }

        var hexagonIds = ToList(listResponse.Payload.ContainsKey("items") ? listResponse.Payload["items"] : new ArrayList()).Cast<object>()
            .Select(item => AsMap(item, CommandNames.DevelopmentHexagonAdminList))
            .Where(map => map != null)
            .Select(map => FirstNonEmpty(S(map!, "hexagonId"), DevelopmentHexagonIds.Main))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var knownHexagonId in new[]
                 {
                     DevelopmentHexagonIds.Main,
                     DevelopmentHexagonIds.Magic,
                     DevelopmentHexagonIds.LargeTest0154
                 })
        {
            if (!hexagonIds.Contains(knownHexagonId, StringComparer.OrdinalIgnoreCase))
                hexagonIds.Add(knownHexagonId);
        }

        if (hexagonIds.Count == 0) hexagonIds.Add(DevelopmentHexagonIds.Main);

        foreach (var hexagonId in hexagonIds)
        {
            var layoutResponse = _api.DevelopmentHexagonAdminGetLayout(new Dictionary<string, object>
            {
                { "hexagonId", hexagonId }
            });
            if (layoutResponse.Status != ResponseStatus.Ok)
            {
                DevelopmentLayoutStatusText = FirstNonEmpty(layoutResponse.Message, $"Не удалось загрузить раскладку: {hexagonId}.");
                continue;
            }

            StoreDevelopmentHexagonPayload(layoutResponse.Payload, hexagonId);
            foreach (var item in ToList(layoutResponse.Payload.ContainsKey("items") ? layoutResponse.Payload["items"] : new ArrayList()))
            {
                var map = AsMap(item, CommandNames.DevelopmentHexagonAdminGetLayout);
                if (map == null) continue;
                var nodeId = S(map, "nodeId");
                if (string.IsNullOrWhiteSpace(nodeId)) continue;
                if (string.IsNullOrWhiteSpace(S(map, "hexagonId"))) map["hexagonId"] = hexagonId;
                StoreClassNodeLayoutPayload(map, hexagonId);
            }
        }
    }

    private void StoreDevelopmentHexagonPayload(Dictionary<string, object> payload, string fallbackHexagonId)
    {
        if (payload.TryGetValue("hexagon", out var rawHexagon) &&
            AsMap(rawHexagon, CommandNames.DevelopmentHexagonAdminGetLayout) is { } hexagon)
        {
            var hexagonId = FirstNonEmpty(S(hexagon, "hexagonId"), fallbackHexagonId, DevelopmentHexagonIds.Main);
            _developmentLayoutHexagonPayloads[hexagonId] = hexagon;
        }

        foreach (var item in ToList(payload.ContainsKey("hexagons") ? payload["hexagons"] : new ArrayList()))
        {
            var map = AsMap(item, CommandNames.DevelopmentHexagonAdminGetLayout);
            if (map == null) continue;
            var hexagonId = FirstNonEmpty(S(map, "hexagonId"), fallbackHexagonId, DevelopmentHexagonIds.Main);
            _developmentLayoutHexagonPayloads[hexagonId] = map;
        }
    }

    private static string DevelopmentLayoutPayloadKey(string hexagonId, string nodeId)
    {
        return string.Concat(
            FirstNonEmpty(hexagonId, DevelopmentHexagonIds.Main),
            "::",
            FirstNonEmpty(nodeId, string.Empty));
    }

    private void StoreClassNodeLayoutPayload(Dictionary<string, object> map, string fallbackHexagonId)
    {
        var nodeId = S(map, "nodeId");
        if (string.IsNullOrWhiteSpace(nodeId)) return;
        var hexagonId = FirstNonEmpty(S(map, "hexagonId"), fallbackHexagonId, DevelopmentHexagonIds.Main);
        if (string.IsNullOrWhiteSpace(S(map, "hexagonId"))) map["hexagonId"] = hexagonId;
        _classNodeLayoutPayloads[DevelopmentLayoutPayloadKey(hexagonId, nodeId)] = map;
    }

    private bool TryGetClassNodeLayoutPayload(string nodeId, out Dictionary<string, object> map)
    {
        map = null;
        if (string.IsNullOrWhiteSpace(nodeId)) return false;

        var selectedKey = DevelopmentLayoutPayloadKey(SelectedDevelopmentLayoutHexagonId, nodeId);
        if (_classNodeLayoutPayloads.TryGetValue(selectedKey, out map)) return true;

        map = _classNodeLayoutPayloads.Values.FirstOrDefault(payload =>
            string.Equals(S(payload, "nodeId"), nodeId, StringComparison.OrdinalIgnoreCase));
        return map != null;
    }

    private void LoadSelectedClassNodeLayoutEditor()
    {
        if (_isClassNodeLayoutDirty)
        {
            NodeLayoutSaveStatus = "Есть несохранённые изменения раскладки узла.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedClassNodeId) ||
            !TryGetClassNodeLayoutPayload(SelectedClassNodeId, out var map))
        {
            return;
        }

        _isLoadingClassNodeLayoutEditor = true;
        NodeHexagonId = FirstNonEmpty(S(map, "hexagonId"), DevelopmentHexagonIds.Main);
        NodeHexagonType = FirstNonEmpty(S(map, "hexagonType"), DevelopmentHexagonTypes.Main);
        NodeName = FirstNonEmpty(S(map, "name"), S(map, "publicName"), SelectedClassNodeId);
        NodeDescription = FirstNonEmpty(S(map, "description"), S(map, "publicDescription"));
        NodeType = FirstNonEmpty(S(map, "nodeType"), DevelopmentNodeTypes.Class);
        NodeRole = FirstNonEmpty(S(map, "nodeRole"), DevelopmentNodeRoleIds.MainBranchLevel);
        NodeVisibilityRule = FirstNonEmpty(S(map, "visibilityRule"), DevelopmentUnlockPolicyIds.VisibleByDefault);
        NodePositionX = FirstNonEmpty(S(map, "positionX"), S(map, "gridX"));
        NodePositionY = FirstNonEmpty(S(map, "positionY"), S(map, "gridY"));
        NodeRing = S(map, "ring");
        NodeSector = S(map, "sector");
        NodeDirectionCode = FirstNonEmpty(S(map, "directionCode"), S(map, "directionId"));
        NodeBranchCode = FirstNonEmpty(S(map, "branchCode"), S(map, "branchId"));
        NodeSortOrder = S(map, "sortOrder");
        NodeRequiredNodes = string.Join(", ", ReadStringList(map, "requiredNodeIds"));
        if (string.IsNullOrWhiteSpace(NodeRequiredNodes)) NodeRequiredNodes = string.Join(", ", ReadStringList(map, "linkedNodeIds"));
        NodeLinkedClassId = FirstNonEmpty(S(map, "linkedClassId"), S(map, "classId"));
        NodeLinkedDefinitionKind = FirstNonEmpty(S(map, "linkedDefinitionKind"), S(map, "linkedEntityType"));
        NodeLinkedDefinitionId = FirstNonEmpty(S(map, "linkedDefinitionId"), S(map, "linkedEntityId"));
        NodeCost = FirstNonEmpty(S(map, "cost"), S(map, "costExperienceCoins"));
        NodeCurrencyId = FirstNonEmpty(S(map, "currencyId"), S(map, "costCurrencyId"), CharacterCurrencyIds.XpCoin);
        NodePrimaryMagicGroupId = S(map, "primaryMagicGroupId");
        NodeIsPrimaryMagicClass = ParseBool(S(map, "isPrimaryMagicClass"), false);
        NodeIsPlayerVisible = ParseBool(FirstNonEmpty(S(map, "isPlayerVisible"), S(map, "isVisibleToPlayer")), true);
        NodeIsHidden = ParseBool(S(map, "isHidden"), false);
        NodeIsArchived = ParseBool(S(map, "isArchived"), false);
        NodeLayoutLockedManualPosition = ParseBool(S(map, "layoutLockedManualPosition"), false);
        NodeLayoutVersion = S(map, "layoutVersion");
        NodeLayoutUpdatedAt = S(map, "updatedAtUtc");
        RequirementTargetNodeId = SelectedClassNodeId;
        NodeLayoutSaveStatus = $"Загружен узел: {DevelopmentGraphDisplay.ToReadableNodeTitle(NodeName, SelectedClassNodeId)}";
        _isClassNodeLayoutDirty = false;
        _isLoadingClassNodeLayoutEditor = false;
    }

    private void RefreshDevelopmentLayoutEditorFromPayloads()
    {
        if (_isRefreshingDevelopmentLayoutEditor) return;
        _isRefreshingDevelopmentLayoutEditor = true;
        try
        {
        DevelopmentLayoutHexagons.Clear();
        var payloadEntries = _classNodeLayoutPayloads
            .Select(pair =>
            {
                var keyParts = pair.Key.Split(new[] { "::" }, StringSplitOptions.None);
                var keyHexagonId = keyParts.Length > 0 ? keyParts[0] : string.Empty;
                return new
                {
                    Map = pair.Value,
                    HexagonId = FirstNonEmpty(S(pair.Value, "hexagonId"), keyHexagonId, DevelopmentHexagonIds.Main)
                };
            })
            .ToList();

        var hexagonIds = payloadEntries
            .Select(entry => entry.HexagonId)
            .Concat(_developmentLayoutHexagonPayloads.Keys)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => string.Equals(id, DevelopmentHexagonIds.Main, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var hexagonId in hexagonIds)
        {
            _developmentLayoutHexagonPayloads.TryGetValue(hexagonId, out var hexagonPayload);
            var first = payloadEntries.FirstOrDefault(entry => string.Equals(entry.HexagonId, hexagonId, StringComparison.OrdinalIgnoreCase))?.Map;
            var hexagonType = hexagonPayload == null ? string.Empty : S(hexagonPayload, "hexagonType");
            var hexagonName = hexagonPayload == null ? string.Empty : FirstNonEmpty(S(hexagonPayload, "name"), S(hexagonPayload, "displayName"));
            DevelopmentLayoutHexagons.Add(new DevelopmentHexagonEditorTreeVm
            {
                HexagonId = hexagonId,
                HexagonType = FirstNonEmpty(hexagonType, first == null ? string.Empty : S(first, "hexagonType"), DevelopmentHexagonTypes.Main),
                Name = FirstNonEmpty(hexagonName, first == null ? string.Empty : S(first, "hexagonName"), hexagonId),
                SortOrder = string.Equals(hexagonId, DevelopmentHexagonIds.Main, StringComparison.OrdinalIgnoreCase) ? 1 : 2,
                NodeCount = payloadEntries.Count(entry => string.Equals(entry.HexagonId, hexagonId, StringComparison.OrdinalIgnoreCase))
            });
        }

        if (DevelopmentLayoutHexagons.Count == 0)
        {
            DevelopmentLayoutHexagons.Add(new DevelopmentHexagonEditorTreeVm
            {
                HexagonId = DevelopmentHexagonIds.Main,
                HexagonType = DevelopmentHexagonTypes.Main,
                Name = "Нет данных",
                SortOrder = 1,
                NodeCount = 0
            });
        }

        if (!DevelopmentLayoutHexagons.Any(h => string.Equals(h.HexagonId, SelectedDevelopmentLayoutHexagonId, StringComparison.OrdinalIgnoreCase)))
            _selectedDevelopmentLayoutHexagonId = DevelopmentLayoutHexagons.First().HexagonId;

        DevelopmentLayoutNodes.Clear();
        DevelopmentLayoutLinks.Clear();
        _developmentLayoutRevision = 1;
        foreach (var map in payloadEntries
            .Where(entry => string.Equals(entry.HexagonId, SelectedDevelopmentLayoutHexagonId, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Map)
            .OrderBy(map => ParseDevelopmentLayoutInt(S(map, "sortOrder"), 0))
            .ThenBy(map => ParseDevelopmentLayoutInt(FirstNonEmpty(S(map, "positionY"), S(map, "gridY")), 0))
            .ThenBy(map => ParseDevelopmentLayoutInt(FirstNonEmpty(S(map, "positionX"), S(map, "gridX")), 0)))
        {
            var cost = FirstNonEmpty(S(map, "costLabel"), S(map, "cost"), S(map, "costExperienceCoins"));
            var currencyId = FirstNonEmpty(S(map, "currencyId"), S(map, "costCurrencyId"), CharacterCurrencyIds.XpCoin);
            var node = new DevelopmentHexagonEditorNodeVm
            {
                NodeId = S(map, "nodeId"),
                HexagonId = FirstNonEmpty(S(map, "hexagonId"), DevelopmentHexagonIds.Main),
                Title = FirstNonEmpty(S(map, "name"), S(map, "nodeId")),
                NodeTypeLabel = FirstNonEmpty(S(map, "nodeTypeLabel"), S(map, "nodeType"), "Нет данных"),
                State = FirstNonEmpty(S(map, "state"), S(map, "status"), "layout"),
                Direction = FirstNonEmpty(S(map, "canonicalDirectionId"), S(map, "directionCode"), S(map, "directionId")),
                Branch = FirstNonEmpty(S(map, "canonicalBranchId"), S(map, "branchCode"), S(map, "branchId")),
                LinkedDefinitionKind = FirstNonEmpty(S(map, "linkedDefinitionKind"), S(map, "linkedEntityType")),
                LinkedDefinitionId = FirstNonEmpty(S(map, "linkedDefinitionId"), S(map, "linkedEntityId"), S(map, "linkedClassId"), S(map, "classId")),
                CostText = string.IsNullOrWhiteSpace(cost) ? string.Empty : $"{cost} {DevelopmentGraphDisplay.ToReadableCurrency(currencyId)}",
                IsPlayerVisible = ParseBool(FirstNonEmpty(S(map, "isPlayerVisible"), S(map, "isVisibleToPlayer")), true),
                IsHidden = ParseBool(S(map, "isHidden"), false),
                LayoutVersion = ParseDevelopmentLayoutInt(S(map, "layoutVersion"), 1),
                LayoutLayer = ParseDevelopmentLayoutInt(FirstNonEmpty(S(map, "layoutLayer"), S(map, "ring")), 0)
            };
            DevelopmentLayoutVisualRules.ApplyNodeSize(node);
            _developmentLayoutRevision = Math.Max(_developmentLayoutRevision, node.LayoutVersion);
            node.PositionX = ParseDevelopmentLayoutInt(FirstNonEmpty(S(map, "positionX"), S(map, "gridX")), 500);
            node.PositionY = ParseDevelopmentLayoutInt(FirstNonEmpty(S(map, "positionY"), S(map, "gridY")), 500);
            node.OriginalPositionX = node.PositionX;
            node.OriginalPositionY = node.PositionY;
            node.IsSelected = string.Equals(node.NodeId, SelectedClassNodeId, StringComparison.OrdinalIgnoreCase);
            DevelopmentLayoutNodes.Add(node);
        }

        var nodesById = DevelopmentLayoutNodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
        foreach (var targetMap in payloadEntries
            .Where(entry => string.Equals(entry.HexagonId, SelectedDevelopmentLayoutHexagonId, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Map))
        {
            var targetId = S(targetMap, "nodeId");
            if (!nodesById.TryGetValue(targetId, out var targetNode)) continue;
            foreach (var sourceId in ReadStringList(targetMap, "requiredNodeIds").Concat(ReadStringList(targetMap, "linkedNodeIds")).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!nodesById.TryGetValue(sourceId, out var sourceNode)) continue;
                DevelopmentLayoutLinks.Add(new DevelopmentHexagonEditorLinkVm
                {
                    LinkId = sourceId + "->" + targetId,
                    SourceNodeId = sourceId,
                    TargetNodeId = targetId,
                    SourceTitle = sourceNode.DisplayTitle,
                    TargetTitle = targetNode.DisplayTitle,
                    LinkType = "requirement",
                    X1 = sourceNode.PositionX + sourceNode.NodeWidth / 2,
                    Y1 = sourceNode.PositionY + sourceNode.NodeHeight / 2,
                    X2 = targetNode.PositionX + targetNode.NodeWidth / 2,
                    Y2 = targetNode.PositionY + targetNode.NodeHeight / 2,
                    IsSelected = SelectedDevelopmentLayoutLink != null && string.Equals(SelectedDevelopmentLayoutLink.LinkId, sourceId + "->" + targetId, StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        _selectedDevelopmentLayoutNode = DevelopmentLayoutNodes.FirstOrDefault(n => string.Equals(n.NodeId, SelectedClassNodeId, StringComparison.OrdinalIgnoreCase));
        if (_selectedDevelopmentLayoutNode != null) _selectedDevelopmentLayoutNode.IsSelected = true;
        DevelopmentLayoutHasUnsavedChanges = false;
        DevelopmentLayoutStatusText = DevelopmentLayoutNodes.Count == 0
            ? "Нет данных"
            : $"Загружено узлов: {DevelopmentLayoutNodes.Count}; связей: {DevelopmentLayoutLinks.Count}";
        _developmentLayoutUndoStack.Clear();
        _developmentLayoutRedoStack.Clear();
        DevelopmentLayoutValidationErrors.Clear();
        DevelopmentLayoutValidationWarnings.Clear();
        ApplyDevelopmentLayoutSearchAndFilters();
        ApplyDevelopmentLayoutFocusState();
        UpdateDevelopmentLayoutChangedObjects();
        Notify(nameof(DevelopmentLayoutValidationSummary));
        Notify(nameof(SelectedDevelopmentLayoutHexagonId));
        Notify(nameof(SelectedDevelopmentLayoutNode));
        Notify(nameof(SelectedDevelopmentLayoutNodeSummary));
        Notify(nameof(SelectedDevelopmentLayoutIncomingLinksText));
        Notify(nameof(SelectedDevelopmentLayoutOutgoingLinksText));
        Notify(nameof(DevelopmentLayoutTreeModeOverlayText));
        }
        finally
        {
            _isRefreshingDevelopmentLayoutEditor = false;
        }
    }

    private void SyncDevelopmentLayoutSelectionFromNodeId()
    {
        var node = DevelopmentLayoutNodes.FirstOrDefault(n => string.Equals(n.NodeId, SelectedClassNodeId, StringComparison.OrdinalIgnoreCase));
        if (_selectedDevelopmentLayoutNode != null) _selectedDevelopmentLayoutNode.IsSelected = false;
        _selectedDevelopmentLayoutNode = node;
        if (_selectedDevelopmentLayoutNode != null) _selectedDevelopmentLayoutNode.IsSelected = true;
        ApplyDevelopmentLayoutFocusState();
        Notify(nameof(SelectedDevelopmentLayoutNode));
        Notify(nameof(SelectedDevelopmentLayoutNodeSummary));
        Notify(nameof(SelectedDevelopmentLayoutIncomingLinksText));
        Notify(nameof(SelectedDevelopmentLayoutOutgoingLinksText));
    }

    private void ShowAllDevelopmentLayoutLinks()
    {
        DevelopmentLayoutFocusSelectedNodeLinks = false;
        ApplyDevelopmentLayoutFocusState();
        DevelopmentLayoutStatusText = "Показаны все связи графа.";
    }

    private void ApplyDevelopmentLayoutFocusState()
    {
        var selectedId = SelectedDevelopmentLayoutNode?.NodeId ?? string.Empty;
        var focusMode = DevelopmentLayoutFocusSelectedNodeLinks;
        var hasFocus = focusMode && !string.IsNullOrWhiteSpace(selectedId);
        var connectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (hasFocus)
        {
            connectedIds.Add(selectedId);
            foreach (var link in DevelopmentLayoutLinks.Where(link =>
                         string.Equals(link.SourceNodeId, selectedId, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(link.TargetNodeId, selectedId, StringComparison.OrdinalIgnoreCase)))
            {
                connectedIds.Add(link.SourceNodeId);
                connectedIds.Add(link.TargetNodeId);
            }
        }

        foreach (var link in DevelopmentLayoutLinks)
        {
            if (link.IsFilteredOut)
            {
                link.IsFocusRelevant = false;
                continue;
            }

            link.IsFocusRelevant = !focusMode || (hasFocus &&
                (string.Equals(link.SourceNodeId, selectedId, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(link.TargetNodeId, selectedId, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var node in DevelopmentLayoutNodes)
        {
            var directionFocused = !string.IsNullOrWhiteSpace(DevelopmentLayoutFocusedDirectionKey)
                && !IsCanonicalRootNodeId(node.NodeId)
                && !NodeMatchesCanonicalDirection(node, DevelopmentLayoutFocusedDirectionKey);
            node.IsFocusNeighbor = hasFocus && connectedIds.Contains(node.NodeId);
            node.IsFocusDimmed = (hasFocus && !connectedIds.Contains(node.NodeId)) || directionFocused;
        }

        if (hasFocus && (SelectedDevelopmentLayoutLink == null || !SelectedDevelopmentLayoutLink.IsFocusRelevant))
            SelectedDevelopmentLayoutLink = DevelopmentLayoutLinks.FirstOrDefault(link => link.IsFocusRelevant);

        Notify(nameof(SelectedDevelopmentLayoutIncomingLinksText));
        Notify(nameof(SelectedDevelopmentLayoutOutgoingLinksText));
    }

    private void RebuildDevelopmentCanonicalOverlay()
    {
        DevelopmentLayoutCanonicalRoots.Clear();
        DevelopmentLayoutCanonicalDirections.Clear();
        DevelopmentLayoutCanonicalLanes.Clear();

        if (DevelopmentLayoutShowDiagnostics) return;

        const double rootWidth = 320;
        const double rootHeight = 184;

        var rootNode = FindDevelopmentCanonicalRootNode(SelectedDevelopmentLayoutHexagonId);
        var centerX = rootNode == null
            ? DevelopmentLayoutVisualRules.WorkspaceWidth / 2.0
            : rootNode.PositionX + rootNode.NodeWidth / 2.0;
        var centerY = rootNode == null
            ? DevelopmentLayoutVisualRules.WorkspaceHeight / 2.0
            : rootNode.PositionY + rootNode.NodeHeight / 2.0;
        var rootLabel = ResolveDevelopmentCanonicalRootLabel(SelectedDevelopmentLayoutHexagonId, rootNode);

        DevelopmentLayoutCanonicalRoots.Add(new DevelopmentCanonicalRootVm
        {
            Label = rootLabel,
            X = centerX - rootWidth / 2.0,
            Y = centerY - rootHeight / 2.0,
            Width = rootWidth,
            Height = rootHeight,
            AutomationId = "AdminDevelopmentHexagonEditor_CenterRootHexagon"
        });

        var directions = BuildCanonicalDirectionDefinitions(SelectedDevelopmentLayoutHexagonId);
        var nodeCenters = DevelopmentLayoutNodes
            .Where(node => !node.IsFilteredOut && !IsDevelopmentCanonicalRootNode(node.NodeId, SelectedDevelopmentLayoutHexagonId))
            .Select(node => new
            {
                Node = node,
                X = node.PositionX + node.NodeWidth / 2.0,
                Y = node.PositionY + node.NodeHeight / 2.0
            })
            .ToList();

        foreach (var direction in directions)
        {
            var radians = direction.AngleDegrees * Math.PI / 180.0;
            var normalX = Math.Cos(radians);
            var normalY = Math.Sin(radians);
            var matching = nodeCenters
                .Where(item => NodeMatchesCanonicalDirection(item.Node, direction.DirectionId))
                .ToList();
            var isLargeDiagnosticHexagon = string.Equals(SelectedDevelopmentLayoutHexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase);
            var minimumLaneLength = isLargeDiagnosticHexagon ? 4400 : 680;
            var laneEndPadding = isLargeDiagnosticHexagon ? 360 : 160;
            var maximumLaneLength = isLargeDiagnosticHexagon ? 5200 : 940;
            var farthest = matching.Count == 0
                ? minimumLaneLength
                : Math.Max(minimumLaneLength, matching.Max(item => (item.X - centerX) * normalX + (item.Y - centerY) * normalY) + laneEndPadding);
            farthest = Math.Min(farthest, maximumLaneLength);

            var laneStart = 178.0;
            var anchorDistance = 360.0;
            var labelDistance = 520.0;
            var anchorWidth = 190.0;
            var anchorHeight = 86.0;
            var isFocused = string.IsNullOrWhiteSpace(DevelopmentLayoutFocusedDirectionKey) ||
                            string.Equals(DevelopmentLayoutFocusedDirectionKey, direction.DirectionId, StringComparison.OrdinalIgnoreCase);

            DevelopmentLayoutCanonicalLanes.Add(new DevelopmentCanonicalLaneVm
            {
                DirectionId = direction.DirectionId,
                SideIndex = direction.SideIndex,
                X1 = centerX + normalX * laneStart,
                Y1 = centerY + normalY * laneStart,
                X2 = centerX + normalX * farthest,
                Y2 = centerY + normalY * farthest,
                Opacity = isFocused ? 0.76 : 0.18,
                StrokeThickness = isFocused ? 5.2 : 2.4
            });

            DevelopmentLayoutCanonicalDirections.Add(new DevelopmentCanonicalDirectionVm
            {
                DirectionId = direction.DirectionId,
                SideIndex = direction.SideIndex,
                DisplayName = direction.DisplayName,
                AtmosphericName = direction.AtmosphericName,
                AnchorX = centerX + normalX * anchorDistance - anchorWidth / 2.0,
                AnchorY = centerY + normalY * anchorDistance - anchorHeight / 2.0,
                LabelX = centerX + normalX * labelDistance - 110,
                LabelY = centerY + normalY * labelDistance - 22,
                AnchorWidth = anchorWidth,
                AnchorHeight = anchorHeight,
                IsFocused = isFocused
            });
        }

        Notify(nameof(DevelopmentLayoutCanonicalLayerVisibility));
    }

    private DevelopmentHexagonEditorNodeVm? FindDevelopmentCanonicalRootNode(string hexagonId)
    {
        var centerNodeId = GetDevelopmentCanonicalCenterNodeId(hexagonId);
        if (!string.IsNullOrWhiteSpace(centerNodeId))
        {
            var explicitRoot = DevelopmentLayoutNodes.FirstOrDefault(node => string.Equals(node.NodeId, centerNodeId, StringComparison.OrdinalIgnoreCase));
            if (explicitRoot != null) return explicitRoot;
        }

        return DevelopmentLayoutNodes.FirstOrDefault(node => string.Equals(node.NodeId, ExpectedDevelopmentCanonicalRootNodeId(hexagonId), StringComparison.OrdinalIgnoreCase));
    }

    private static string ExpectedDevelopmentCanonicalRootNodeId(string hexagonId)
        => string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase)
            ? "magic_awakened"
            : string.Equals(hexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase)
                ? "large0154_root"
                : "novice";

    private string ResolveDevelopmentCanonicalRootLabel(string hexagonId, DevelopmentHexagonEditorNodeVm? rootNode)
    {
        var centerName = string.Empty;
        if (_developmentLayoutHexagonPayloads.TryGetValue(hexagonId, out var payload))
            centerName = S(payload, "centerNodeName");

        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
            return FirstNonEmpty(rootNode?.DisplayTitle ?? string.Empty, centerName, "Источник магии");
        if (string.Equals(hexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase))
            return FirstNonEmpty(centerName, rootNode?.DisplayTitle ?? string.Empty, "Большое дерево");

        return FirstNonEmpty(rootNode?.DisplayTitle ?? string.Empty, centerName, "Новичок");
    }

    private string GetDevelopmentCanonicalCenterNodeId(string hexagonId)
    {
        if (_developmentLayoutHexagonPayloads.TryGetValue(hexagonId, out var payload))
            return FirstNonEmpty(S(payload, "centerNodeId"), S(payload, "rootNodeId"));
        return ExpectedDevelopmentCanonicalRootNodeId(hexagonId);
    }

    private static bool IsCanonicalRootNodeId(string nodeId)
        => string.Equals(nodeId, "novice", StringComparison.OrdinalIgnoreCase)
           || string.Equals(nodeId, "magic_awakened", StringComparison.OrdinalIgnoreCase)
           || string.Equals(nodeId, "large0154_root", StringComparison.OrdinalIgnoreCase);

    private bool IsDevelopmentCanonicalRootNode(string nodeId, string hexagonId)
    {
        var centerNodeId = GetDevelopmentCanonicalCenterNodeId(hexagonId);
        return (!string.IsNullOrWhiteSpace(centerNodeId) && string.Equals(nodeId, centerNodeId, StringComparison.OrdinalIgnoreCase))
               || IsCanonicalRootNodeId(nodeId);
    }

    private static bool NodeMatchesCanonicalDirection(DevelopmentHexagonEditorNodeVm node, string directionId)
        => string.Equals(node.Direction, directionId, StringComparison.OrdinalIgnoreCase)
           || string.Equals(node.Branch, directionId, StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<CanonicalDirectionDefinition> BuildCanonicalDirectionDefinitions(string hexagonId)
    {
        if (_developmentLayoutHexagonPayloads.TryGetValue(hexagonId, out var payload))
        {
            var serverDirections = BuildCanonicalDirectionDefinitionsFromPayload(payload);
            if (serverDirections.Count > 0)
            {
                if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase) &&
                    serverDirections.Count != 6)
                {
                    return BuildDefaultMagicCanonicalDirectionDefinitions();
                }

                if (string.Equals(hexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase) &&
                    serverDirections.Count != 6)
                {
                    return BuildDefaultLargeCanonicalDirectionDefinitions();
                }

                return serverDirections.Count <= 6
                    ? serverDirections
                    : serverDirections.Take(6).ToList();
            }
        }

        if (string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase))
            return BuildDefaultMagicCanonicalDirectionDefinitions();

        if (string.Equals(hexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase))
            return BuildDefaultLargeCanonicalDirectionDefinitions();

        return new[]
        {
            new CanonicalDirectionDefinition(DevelopmentDirectionIds.StrengthAssault, "Сила", "Натиск", 0, -90),
            new CanonicalDirectionDefinition(DevelopmentDirectionIds.DexterityManeuver, "Ловкость", "Манёвр", 1, -30),
            new CanonicalDirectionDefinition(DevelopmentDirectionIds.EnduranceResilience, "Выносливость", "Стойкость", 2, 30),
            new CanonicalDirectionDefinition(DevelopmentDirectionIds.IntellectReason, "Интеллект", "Разум", 3, 90),
            new CanonicalDirectionDefinition(DevelopmentDirectionIds.WisdomPath, "Мудрость", "Путь", 4, 150),
            new CanonicalDirectionDefinition(DevelopmentDirectionIds.CharismaInfluence, "Харизма", "Влияние", 5, -150)
        };
    }

    private static IReadOnlyList<CanonicalDirectionDefinition> BuildDefaultMagicCanonicalDirectionDefinitions()
        => new[]
        {
            new CanonicalDirectionDefinition("magic_mana", "Мана", "Поток", 0, -90),
            new CanonicalDirectionDefinition("magic_spell", "Заклинания", "Форма", 1, -30),
            new CanonicalDirectionDefinition("magic_seal", "Печати", "Знак", 2, 30),
            new CanonicalDirectionDefinition("magic_arcana", "Аркана", "Глубина", 3, 90),
            new CanonicalDirectionDefinition("magic_element_fire", "Стихия огня", "Пламя", 4, 150),
            new CanonicalDirectionDefinition("magic_direction_light", "Направление света", "Свет", 5, -150)
        };

    private static IReadOnlyList<CanonicalDirectionDefinition> BuildDefaultLargeCanonicalDirectionDefinitions()
        => Enumerable.Range(1, 6)
            .Select(index => new CanonicalDirectionDefinition($"large0154_branch_{index:00}", $"Ветка {index}", "тест", index - 1, CanonicalAngleForSide(index - 1)))
            .ToList();

    private IReadOnlyList<CanonicalDirectionDefinition> BuildCanonicalDirectionDefinitionsFromPayload(Dictionary<string, object> payload)
    {
        var result = new List<CanonicalDirectionDefinition>();
        foreach (var raw in ToList(payload.ContainsKey("directions") ? payload["directions"] : new ArrayList()))
        {
            var map = AsMap(raw, CommandNames.DevelopmentHexagonAdminGetLayout);
            if (map == null) continue;

            var directionId = FirstNonEmpty(S(map, "directionId"), S(map, "id"));
            if (string.IsNullOrWhiteSpace(directionId)) continue;

            var displayOrder = ParseDevelopmentLayoutInt(FirstNonEmpty(S(map, "displayOrder"), S(map, "sortOrder")), result.Count + 1);
            var sideIndex = string.IsNullOrWhiteSpace(S(map, "sideIndex"))
                ? Math.Max(0, displayOrder - 1)
                : Math.Max(0, ParseDevelopmentLayoutInt(S(map, "sideIndex"), result.Count));
            result.Add(new CanonicalDirectionDefinition(
                directionId,
                FirstNonEmpty(S(map, "name"), S(map, "displayName"), directionId),
                FirstNonEmpty(S(map, "atmosphericName"), S(map, "subtitle"), S(map, "secondaryName")),
                sideIndex,
                ParseDevelopmentLayoutDouble(FirstNonEmpty(S(map, "angleDegrees"), S(map, "angle")), CanonicalAngleForSide(sideIndex))));
        }

        return result
            .OrderBy(direction => direction.SideIndex)
            .ThenBy(direction => direction.DirectionId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static double CanonicalAngleForSide(int sideIndex)
        => sideIndex switch
        {
            0 => -90,
            1 => -30,
            2 => 30,
            3 => 90,
            4 => 150,
            _ => -150
        };

    private sealed class CanonicalDirectionDefinition
    {
        public CanonicalDirectionDefinition(string directionId, string displayName, string atmosphericName, int sideIndex, double angleDegrees)
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

    private void ApplyDevelopmentLayoutSearchAndFilters()
    {
        var query = (DevelopmentLayoutSearchText ?? string.Empty).Trim();
        var isLargeDiagnosticHexagon = string.Equals(SelectedDevelopmentLayoutHexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase);
        foreach (var node in DevelopmentLayoutNodes)
        {
            var typeOk = IsDevelopmentLayoutFilterMatch(DevelopmentLayoutTypeFilter, node.NodeTypeLabel);
            var stateOk = IsDevelopmentLayoutFilterMatch(DevelopmentLayoutStateFilter, node.State);
            var linkedOk = IsDevelopmentLayoutFilterMatch(DevelopmentLayoutLinkedKindFilter, node.LinkedDefinitionKind);
            var diagnosticsOk = isLargeDiagnosticHexagon || DevelopmentLayoutShowDiagnostics || !node.IsDiagnosticNode;
            var visibilityOk = DevelopmentLayoutVisibilityFilter switch
            {
                "player" => node.IsPlayerVisible && !node.IsHidden,
                "hidden" => !node.IsPlayerVisible && !node.IsHidden,
                "gm_only" => node.IsHidden,
                _ => true
            };
            var searchOk = string.IsNullOrWhiteSpace(query) || node.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            node.IsSearchMatch = !string.IsNullOrWhiteSpace(query) && searchOk;
            var canonicalRootSuppressed = DevelopmentLayoutCanonicalModeSelected
                && !DevelopmentLayoutShowDiagnostics
                && IsDevelopmentCanonicalRootNode(node.NodeId, SelectedDevelopmentLayoutHexagonId);
            node.IsFilteredOut = canonicalRootSuppressed || !(typeOk && stateOk && linkedOk && visibilityOk && diagnosticsOk && searchOk);
        }

        var visibleNodeIds = new HashSet<string>(
            DevelopmentLayoutNodes.Where(node => !node.IsFilteredOut).Select(node => node.NodeId),
            StringComparer.OrdinalIgnoreCase);
        foreach (var link in DevelopmentLayoutLinks)
            link.IsFilteredOut = !visibleNodeIds.Contains(link.SourceNodeId) || !visibleNodeIds.Contains(link.TargetNodeId);

        RebuildDevelopmentCanonicalOverlay();
        Notify(nameof(DevelopmentLayoutSearchResultCountText));
        Notify(nameof(DevelopmentLayoutTreeModeOverlayText));
    }

    private static bool IsDevelopmentLayoutFilterMatch(string filter, string value)
    {
        if (string.IsNullOrWhiteSpace(filter) || string.Equals(filter, "all", StringComparison.OrdinalIgnoreCase)) return true;
        return (value ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ClearDevelopmentLayoutSearch()
    {
        DevelopmentLayoutSearchText = string.Empty;
    }

    private void ClearDevelopmentLayoutFilters()
    {
        _developmentLayoutTypeFilter = "all";
        _developmentLayoutVisibilityFilter = "all";
        _developmentLayoutStateFilter = "all";
        _developmentLayoutLinkedKindFilter = "all";
        Notify(nameof(DevelopmentLayoutTypeFilter));
        Notify(nameof(DevelopmentLayoutVisibilityFilter));
        Notify(nameof(DevelopmentLayoutStateFilter));
        Notify(nameof(DevelopmentLayoutLinkedKindFilter));
        ApplyDevelopmentLayoutSearchAndFilters();
        DevelopmentLayoutStatusText = "Фильтры графа сброшены.";
    }

    private void SelectDevelopmentLayoutSearchResult(int direction)
    {
        var matches = DevelopmentLayoutNodes
            .Where(node => !node.IsFilteredOut && (string.IsNullOrWhiteSpace(DevelopmentLayoutSearchText) || node.IsSearchMatch))
            .OrderBy(node => node.PositionY)
            .ThenBy(node => node.PositionX)
            .ToList();
        if (matches.Count == 0)
        {
            DevelopmentLayoutStatusText = "Нет узлов в области поиска.";
            return;
        }

        _developmentLayoutSearchIndex += direction;
        if (_developmentLayoutSearchIndex < 0) _developmentLayoutSearchIndex = matches.Count - 1;
        if (_developmentLayoutSearchIndex >= matches.Count) _developmentLayoutSearchIndex = 0;
        SelectedDevelopmentLayoutNode = matches[_developmentLayoutSearchIndex];
        DevelopmentLayoutStatusText = $"Найден узел {_developmentLayoutSearchIndex + 1}/{matches.Count}: {SelectedDevelopmentLayoutNode.DisplayTitle}";
    }

    private void FitToViewDevelopmentHexagonLayout()
    {
        if (DevelopmentLayoutCanonicalModeSelected && DevelopmentLayoutCanonicalRoots.Count > 0 && DevelopmentLayoutCanonicalDirections.Count > 0)
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

            foreach (var root in DevelopmentLayoutCanonicalRoots)
                IncludeBounds(root.X, root.Y, root.Width, root.Height);
            foreach (var direction in DevelopmentLayoutCanonicalDirections)
            {
                IncludeBounds(direction.AnchorX, direction.AnchorY, direction.AnchorWidth, direction.AnchorHeight);
                IncludeBounds(direction.LabelX, direction.LabelY, 220, 44);
            }
            foreach (var lane in DevelopmentLayoutCanonicalLanes)
                IncludeBounds(Math.Min(lane.X1, lane.X2) - 24, Math.Min(lane.Y1, lane.Y2) - 24, Math.Abs(lane.X2 - lane.X1) + 48, Math.Abs(lane.Y2 - lane.Y1) + 48);
            var rootCenterX = DevelopmentLayoutCanonicalRoots.Average(root => root.X + root.Width / 2.0);
            var rootCenterY = DevelopmentLayoutCanonicalRoots.Average(root => root.Y + root.Height / 2.0);
            var canonicalDirectionIds = new HashSet<string>(DevelopmentLayoutCanonicalDirections.Select(direction => direction.DirectionId), StringComparer.OrdinalIgnoreCase);
            var canonicalFitRadius = string.Equals(SelectedDevelopmentLayoutHexagonId, DevelopmentHexagonIds.LargeTest0154, StringComparison.OrdinalIgnoreCase) ? 5600 : 1120;
            foreach (var node in DevelopmentLayoutNodes.Where(node =>
                         !node.IsFilteredOut
                         && !node.IsDiagnosticNode
                         && !IsDevelopmentCanonicalRootNode(node.NodeId, SelectedDevelopmentLayoutHexagonId)
                         && canonicalDirectionIds.Any(directionId => NodeMatchesCanonicalDirection(node, directionId))))
            {
                var nodeCenterX = node.PositionX + node.NodeWidth / 2.0;
                var nodeCenterY = node.PositionY + node.NodeHeight / 2.0;
                var distance = Math.Sqrt(Math.Pow(nodeCenterX - rootCenterX, 2) + Math.Pow(nodeCenterY - rootCenterY, 2));
                if (distance <= canonicalFitRadius)
                    IncludeBounds(node.PositionX, node.PositionY, node.NodeWidth, node.NodeHeight);
            }

            var minXCanonical = minXValues.Min();
            var minYCanonical = minYValues.Min();
            var maxXCanonical = maxXValues.Max();
            var maxYCanonical = maxYValues.Max();
            var canonicalWidth = Math.Max(1, maxXCanonical - minXCanonical);
            var canonicalHeight = Math.Max(1, maxYCanonical - minYCanonical);
            const double canonicalViewportWidth = 1540;
            const double canonicalViewportHeight = 620;
            const double canonicalMargin = 96;
            var canonicalAvailableWidth = Math.Max(1, canonicalViewportWidth - canonicalMargin * 2);
            var canonicalAvailableHeight = Math.Max(1, canonicalViewportHeight - canonicalMargin * 2);
            var canonicalScale = Math.Min(1.34, Math.Max(0.08, Math.Min(canonicalAvailableWidth / canonicalWidth, canonicalAvailableHeight / canonicalHeight)));
            var canonicalCenterX = minXCanonical + canonicalWidth / 2;
            var canonicalCenterY = minYCanonical + canonicalHeight / 2;
            DevelopmentLayoutZoom = canonicalScale;
            DevelopmentLayoutViewportTranslateX = canonicalViewportWidth / 2 - canonicalCenterX * canonicalScale;
            DevelopmentLayoutViewportTranslateY = canonicalViewportHeight / 2 - canonicalCenterY * canonicalScale;
            DevelopmentLayoutStatusText = $"Вписан канонический шестиугольник: центр и {DevelopmentLayoutCanonicalDirections.Count} направлений.";
            return;
        }

        var visible = DevelopmentLayoutNodes.Where(node => !node.IsFilteredOut).ToList();
        if (visible.Count == 0) visible = DevelopmentLayoutNodes.ToList();
        if (visible.Count == 0)
        {
            DevelopmentLayoutZoom = 0.72;
            DevelopmentLayoutViewportTranslateX = 0;
            DevelopmentLayoutViewportTranslateY = 0;
            DevelopmentLayoutStatusText = "Нет узлов для подгонки масштаба.";
            return;
        }

        var minX = visible.Min(node => node.PositionX);
        var minY = visible.Min(node => node.PositionY);
        var maxX = visible.Max(node => node.PositionX + node.NodeWidth);
        var maxY = visible.Max(node => node.PositionY + node.NodeHeight);
        var width = Math.Max(1, maxX - minX);
        var height = Math.Max(1, maxY - minY);
        const double viewportWidth = 1540;
        const double viewportHeight = 620;
        const double margin = 72;
        var availableWidth = Math.Max(1, viewportWidth - margin * 2);
        var availableHeight = Math.Max(1, viewportHeight - margin * 2);
        var scale = Math.Min(1.34, Math.Max(0.08, Math.Min(availableWidth / width, availableHeight / height)));
        var contentCenterX = minX + width / 2;
        var contentCenterY = minY + height / 2;
        DevelopmentLayoutZoom = scale;
        DevelopmentLayoutViewportTranslateX = viewportWidth / 2 - contentCenterX * scale;
        DevelopmentLayoutViewportTranslateY = viewportHeight / 2 - contentCenterY * scale;
        DevelopmentLayoutStatusText = $"Вписано в экран: {visible.Count} узл.; область {Math.Round(width)}x{Math.Round(height)}.";
    }

    public void BeginDevelopmentLayoutNodeDrag(DevelopmentHexagonEditorNodeVm? node)
    {
        if (node == null) return;
        _developmentLayoutDragNodeId = node.NodeId;
        _developmentLayoutDragStartX = node.PositionX;
        _developmentLayoutDragStartY = node.PositionY;
    }

    public void CommitDevelopmentLayoutNodeDrag(DevelopmentHexagonEditorNodeVm? node)
    {
        if (node == null || string.IsNullOrWhiteSpace(_developmentLayoutDragNodeId)) return;
        if (!string.Equals(_developmentLayoutDragNodeId, node.NodeId, StringComparison.OrdinalIgnoreCase)) return;
        if (Math.Abs(node.PositionX - _developmentLayoutDragStartX) >= 0.01 || Math.Abs(node.PositionY - _developmentLayoutDragStartY) >= 0.01)
        {
            _developmentLayoutUndoStack.Push(new DevelopmentLayoutMoveEdit
            {
                NodeId = node.NodeId,
                FromX = _developmentLayoutDragStartX,
                FromY = _developmentLayoutDragStartY,
                ToX = node.PositionX,
                ToY = node.PositionY
            });
            _developmentLayoutRedoStack.Clear();
        }

        _developmentLayoutDragNodeId = string.Empty;
        UpdateDevelopmentLayoutChangedObjects();
    }

    private void UndoDevelopmentLayoutEdit()
    {
        if (_developmentLayoutUndoStack.Count == 0)
        {
            DevelopmentLayoutStatusText = "Нет локальных действий для undo.";
            return;
        }

        var edit = _developmentLayoutUndoStack.Pop();
        ApplyDevelopmentLayoutMoveEdit(edit.NodeId, edit.FromX, edit.FromY);
        _developmentLayoutRedoStack.Push(edit);
        DevelopmentLayoutStatusText = $"Отменено перемещение: {FindDevelopmentLayoutNodeTitle(edit.NodeId)}";
    }

    private void RedoDevelopmentLayoutEdit()
    {
        if (_developmentLayoutRedoStack.Count == 0)
        {
            DevelopmentLayoutStatusText = "Нет локальных действий для redo.";
            return;
        }

        var edit = _developmentLayoutRedoStack.Pop();
        ApplyDevelopmentLayoutMoveEdit(edit.NodeId, edit.ToX, edit.ToY);
        _developmentLayoutUndoStack.Push(edit);
        DevelopmentLayoutStatusText = $"Повторено перемещение: {FindDevelopmentLayoutNodeTitle(edit.NodeId)}";
    }

    private void ApplyDevelopmentLayoutMoveEdit(string nodeId, double x, double y)
    {
        var node = DevelopmentLayoutNodes.FirstOrDefault(n => string.Equals(n.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        if (node == null) return;
        _isApplyingDevelopmentLayoutHistory = true;
        MoveDevelopmentLayoutNode(node, x, y);
        _isApplyingDevelopmentLayoutHistory = false;
        UpdateDevelopmentLayoutChangedObjects();
    }

    private string FindDevelopmentLayoutNodeTitle(string nodeId)
    {
        return DevelopmentLayoutNodes.FirstOrDefault(n => string.Equals(n.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))?.DisplayTitle
            ?? DevelopmentGraphDisplay.ToReadableNodeTitle(string.Empty, nodeId);
    }

    public void SelectDevelopmentLayoutNode(DevelopmentHexagonEditorNodeVm? node)
    {
        if (node == null) return;
        SelectedDevelopmentLayoutNode = node;
    }

    public void MoveDevelopmentLayoutNode(DevelopmentHexagonEditorNodeVm? node, double x, double y)
    {
        if (node == null) return;
        var newX = Math.Max(0, Math.Min(DevelopmentLayoutVisualRules.WorkspaceWidth - node.NodeWidth, DevelopmentLayoutSnapToGrid ? Math.Round(x / 20.0) * 20 : Math.Round(x)));
        var newY = Math.Max(0, Math.Min(DevelopmentLayoutVisualRules.WorkspaceHeight - node.NodeHeight, DevelopmentLayoutSnapToGrid ? Math.Round(y / 20.0) * 20 : Math.Round(y)));
        node.PositionX = newX;
        node.PositionY = newY;
        UpdateDevelopmentLayoutLinksForNode(node);
        DevelopmentLayoutHasUnsavedChanges = DevelopmentLayoutNodes.Any(n => n.IsChanged);
        DevelopmentLayoutStatusText = $"Узел перемещён: {node.DisplayTitle} · {node.PositionText}";
        if (string.Equals(node.NodeId, SelectedClassNodeId, StringComparison.OrdinalIgnoreCase))
        {
            _isLoadingClassNodeLayoutEditor = true;
            NodePositionX = Convert.ToString((int)Math.Round(node.PositionX), CultureInfo.InvariantCulture);
            NodePositionY = Convert.ToString((int)Math.Round(node.PositionY), CultureInfo.InvariantCulture);
            _isLoadingClassNodeLayoutEditor = false;
        }
        if (!_isApplyingDevelopmentLayoutHistory) UpdateDevelopmentLayoutChangedObjects();
    }

    private void UpdateDevelopmentLayoutLinksForNode(DevelopmentHexagonEditorNodeVm node)
    {
        foreach (var link in DevelopmentLayoutLinks.Where(link =>
                     string.Equals(link.SourceNodeId, node.NodeId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(link.TargetNodeId, node.NodeId, StringComparison.OrdinalIgnoreCase)))
        {
            if (string.Equals(link.SourceNodeId, node.NodeId, StringComparison.OrdinalIgnoreCase))
            {
                link.X1 = node.PositionX + node.NodeWidth / 2;
                link.Y1 = node.PositionY + node.NodeHeight / 2;
            }

            if (string.Equals(link.TargetNodeId, node.NodeId, StringComparison.OrdinalIgnoreCase))
            {
                link.X2 = node.PositionX + node.NodeWidth / 2;
                link.Y2 = node.PositionY + node.NodeHeight / 2;
            }
        }
    }

    private void UpdateDevelopmentLayoutChangedObjects()
    {
        DevelopmentLayoutChangedObjects.Clear();
        foreach (var node in DevelopmentLayoutNodes.Where(node => node.IsChanged).OrderBy(node => node.NodeId))
            DevelopmentLayoutChangedObjects.Add($"{node.DisplayTitle}: {Math.Round(node.OriginalPositionX)},{Math.Round(node.OriginalPositionY)} → {Math.Round(node.PositionX)},{Math.Round(node.PositionY)}");
        if (_isClassNodeLayoutDirty)
            DevelopmentLayoutChangedObjects.Add($"Редактор узла: {FirstNonEmpty(SelectedClassNodeId, "новый узел")}");
        DevelopmentLayoutHasUnsavedChanges = DevelopmentLayoutNodes.Any(node => node.IsChanged);
        Notify(nameof(DevelopmentLayoutSearchResultCountText));
    }

    private void SaveAllDevelopmentHexagonChanges()
    {
        var hadNodeEdit = _isClassNodeLayoutDirty;
        var hadLayoutEdit = DevelopmentLayoutNodes.Any(node => node.IsChanged);
        if (hadNodeEdit) SaveClassNodeLayout();
        if (hadLayoutEdit) SaveDevelopmentHexagonLayout();
        if (!hadNodeEdit && !hadLayoutEdit)
            DevelopmentLayoutStatusText = "Нет изменений для сохранения.";
        UpdateDevelopmentLayoutChangedObjects();
    }

    private void DiscardAllDevelopmentHexagonChanges()
    {
        CancelDevelopmentHexagonLayout();
        _isClassNodeLayoutDirty = false;
        LoadSelectedClassNodeLayoutEditor();
        _developmentLayoutUndoStack.Clear();
        _developmentLayoutRedoStack.Clear();
        UpdateDevelopmentLayoutChangedObjects();
        DevelopmentLayoutStatusText = "Все локальные изменения графа отменены.";
    }

    private void SaveDevelopmentHexagonLayout()
    {
        var changedNodes = DevelopmentLayoutNodes.Where(node => node.IsChanged).ToList();
        if (changedNodes.Count == 0)
        {
            DevelopmentLayoutStatusText = "Нет изменений для сохранения.";
            return;
        }

        var response = _api.DevelopmentHexagonAdminSaveLayout(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId },
            { "layoutRevision", _developmentLayoutRevision },
            { "nodes", changedNodes.Select(node => (object)new Dictionary<string, object>
                {
                    { "nodeId", node.NodeId },
                    { "positionX", (int)Math.Round(node.PositionX) },
                    { "positionY", (int)Math.Round(node.PositionY) }
                }).ToArray() }
        });

        if (response.Status != ResponseStatus.Ok)
        {
            DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Визуальная раскладка готова.");
            return;
        }

        var savedNodeCount = changedNodes.Count;
        LoadClassTree();
        DevelopmentLayoutStatusText = $"Визуальная раскладка сохранена: {savedNodeCount} узл.";
    }

    private void CancelDevelopmentHexagonLayout()
    {
        foreach (var node in DevelopmentLayoutNodes)
        {
            node.PositionX = node.OriginalPositionX;
            node.PositionY = node.OriginalPositionY;
            UpdateDevelopmentLayoutLinksForNode(node);
        }

        DevelopmentLayoutHasUnsavedChanges = false;
        _developmentLayoutUndoStack.Clear();
        _developmentLayoutRedoStack.Clear();
        UpdateDevelopmentLayoutChangedObjects();
        DevelopmentLayoutStatusText = "Локальные изменения раскладки отменены.";
    }

    private void ResetDevelopmentHexagonLayout()
    {
        var response = _api.DevelopmentHexagonAdminResetLayout(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId }
        });
        DevelopmentLayoutStatusText = response.Status == ResponseStatus.Ok
            ? "Нет данных"
            : FirstNonEmpty(response.Message, "Не удалось сбросить раскладку.");
        if (response.Status == ResponseStatus.Ok) LoadClassTree();
    }

    private void ValidateDevelopmentHexagonLayout()
    {
        var response = _api.DevelopmentHexagonAdminValidateLayout(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId },
            { "nodes", DevelopmentLayoutNodes.Select(node => (object)new Dictionary<string, object>
                {
                    { "nodeId", node.NodeId },
                    { "positionX", (int)Math.Round(node.PositionX) },
                    { "positionY", (int)Math.Round(node.PositionY) }
                }).ToArray() }
        });
        DevelopmentLayoutStatusText = response.Status == ResponseStatus.Ok
            ? FirstNonEmpty(response.Message, "Нет данных")
            : FirstNonEmpty(response.Message, "Нет данных");
        PopulateDevelopmentLayoutValidation(response);
    }

    private void PreviewBaselineDevelopmentHexagonLayout()
    {
        var response = _api.DevelopmentHexagonAdminPreviewBaselineLayout(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId },
            { "layoutRevision", _developmentLayoutRevision }
        });
        if (response.Status != ResponseStatus.Ok)
        {
            DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Не удалось построить предпросмотр базовой раскладки.");
            return;
        }

        ApplyDevelopmentLayoutPreview(response);
        DevelopmentLayoutPreviewActive = true;
        DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Предпросмотр базовой раскладки готов.");
        UpdateDevelopmentLayoutQualityText(response);
    }

    private void ApplyBaselineDevelopmentHexagonLayout()
    {
        var response = _api.DevelopmentHexagonAdminApplyBaselineLayout(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId },
            { "layoutRevision", _developmentLayoutRevision },
            { "confirm", true }
        });
        if (response.Status != ResponseStatus.Ok)
        {
            DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Не удалось применить базовую раскладку.");
            return;
        }

        DevelopmentLayoutPreviewActive = false;
        UpdateDevelopmentLayoutSnapshotText(response);
        UpdateDevelopmentLayoutQualityText(response);
        LoadClassTree();
        DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Базовая раскладка применена.");
    }

    private void CreateDevelopmentLayoutSnapshot()
    {
        var response = _api.DevelopmentHexagonAdminCreateLayoutSnapshot(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId }
        });
        if (response.Status != ResponseStatus.Ok)
        {
            DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Не удалось создать снимок раскладки.");
            return;
        }

        UpdateDevelopmentLayoutSnapshotText(response);
        DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Снимок раскладки создан.");
    }

    private void RestoreDevelopmentLayoutSnapshot()
    {
        var response = _api.DevelopmentHexagonAdminRestoreLayoutSnapshot(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId },
            { "confirm", true }
        });
        if (response.Status != ResponseStatus.Ok)
        {
            DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Не удалось восстановить снимок раскладки.");
            return;
        }

        DevelopmentLayoutPreviewActive = false;
        UpdateDevelopmentLayoutQualityText(response);
        LoadClassTree();
        DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Раскладка восстановлена из снимка.");
    }

    private void GetDevelopmentLayoutQualityReport()
    {
        var response = _api.DevelopmentHexagonAdminGetLayoutQualityReport(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId }
        });
        if (response.Status != ResponseStatus.Ok)
        {
            DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Не удалось оценить читаемость раскладки.");
            return;
        }

        UpdateDevelopmentLayoutQualityText(response);
        DevelopmentLayoutStatusText = FirstNonEmpty(response.Message, "Оценка читаемости готова.");
    }

    private void SetSelectedDevelopmentLayoutNodeLock(bool locked)
    {
        if (SelectedDevelopmentLayoutNode == null)
        {
            DevelopmentLayoutStatusText = "Выберите узел для фиксации позиции.";
            return;
        }

        SelectedClassNodeId = SelectedDevelopmentLayoutNode.NodeId;
        LoadSelectedClassNodeLayoutEditor();
        NodeLayoutLockedManualPosition = locked;
        SaveClassNodeLayout();
        DevelopmentLayoutStatusText = locked
            ? $"Позиция узла зафиксирована: {SelectedDevelopmentLayoutNode.DisplayTitle}"
            : $"Фиксация позиции снята: {SelectedDevelopmentLayoutNode.DisplayTitle}";
    }

    private void ApplyDevelopmentLayoutPreview(ResponseEnvelope response)
    {
        foreach (var item in ToList(response.Payload.TryGetValue("nodes", out var rawNodes) ? rawNodes : new ArrayList()))
        {
            var map = AsMap(item, CommandNames.DevelopmentHexagonAdminPreviewBaselineLayout);
            if (map == null) continue;
            var nodeId = S(map, "nodeId");
            if (string.IsNullOrWhiteSpace(nodeId)) continue;
            var node = DevelopmentLayoutNodes.FirstOrDefault(n => string.Equals(n.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            if (node == null) continue;
            node.PositionX = ParseDevelopmentLayoutInt(FirstNonEmpty(S(map, "positionX"), S(map, "gridX")), (int)node.PositionX);
            node.PositionY = ParseDevelopmentLayoutInt(FirstNonEmpty(S(map, "positionY"), S(map, "gridY")), (int)node.PositionY);
            UpdateDevelopmentLayoutLinksForNode(node);
        }

        DevelopmentLayoutHasUnsavedChanges = DevelopmentLayoutNodes.Any(node => node.IsChanged);
        UpdateDevelopmentLayoutChangedObjects();
    }

    private void UpdateDevelopmentLayoutSnapshotText(ResponseEnvelope response)
    {
        var snapshotId = response.Payload.TryGetValue("snapshotId", out var raw) ? Convert.ToString(raw, CultureInfo.InvariantCulture) : string.Empty;
        if (!string.IsNullOrWhiteSpace(snapshotId))
            DevelopmentLayoutSnapshotText = "Снимок: " + snapshotId;
    }

    private void UpdateDevelopmentLayoutQualityText(ResponseEnvelope response)
    {
        Dictionary<string, object>? report = null;
        if (response.Payload.TryGetValue("qualityAfter", out var afterRaw))
            report = AsMap(afterRaw);
        if (report == null && response.Payload.TryGetValue("report", out var reportRaw))
            report = AsMap(reportRaw);
        if (report == null && response.Payload.TryGetValue("qualityReport", out var qualityRaw))
            report = AsMap(qualityRaw);
        if (report == null)
            return;

        var score = FirstNonEmpty(S(report, "readabilityScore"), "—");
        var overlap = FirstNonEmpty(S(report, "overlapCount"), "—");
        var offscreen = FirstNonEmpty(S(report, "offscreenNodeCount"), "—");
        var crossing = FirstNonEmpty(S(report, "crossingEstimate"), "—");
        var linkLength = FirstNonEmpty(S(report, "averageLinkLength"), "—");
        DevelopmentLayoutQualityText = $"Читаемость: {score}; пересечения карточек: {overlap}; вне области: {offscreen}; пересечения связей: {crossing}; средняя связь: {linkLength}";
    }

    private void PopulateDevelopmentLayoutValidation(ResponseEnvelope response)
    {
        DevelopmentLayoutValidationErrors.Clear();
        DevelopmentLayoutValidationWarnings.Clear();
        foreach (var node in DevelopmentLayoutNodes)
        {
            node.IsInvalid = false;
            node.HasWarning = false;
            node.ValidationMessage = string.Empty;
        }

        foreach (var link in DevelopmentLayoutLinks)
            link.IsInvalid = false;

        foreach (var message in ExtractDevelopmentValidationMessages(response.Payload, "errors"))
            DevelopmentLayoutValidationErrors.Add(message);
        foreach (var message in ExtractDevelopmentValidationMessages(response.Payload, "warnings"))
            DevelopmentLayoutValidationWarnings.Add(message);

        if (response.Status != ResponseStatus.Ok && DevelopmentLayoutValidationErrors.Count == 0)
            DevelopmentLayoutValidationErrors.Add(FirstNonEmpty(response.Message, "Ошибка проверки графа."));
        if (response.Status == ResponseStatus.Ok && DevelopmentLayoutValidationErrors.Count == 0 && DevelopmentLayoutValidationWarnings.Count == 0)
            DevelopmentLayoutValidationWarnings.Add(FirstNonEmpty(response.Message, "Проверка завершена без ошибок."));

        ApplyDevelopmentLayoutValidationBadges();
        Notify(nameof(DevelopmentLayoutValidationSummary));
    }

    private static IEnumerable<string> ExtractDevelopmentValidationMessages(Dictionary<string, object> payload, string key)
    {
        if (payload == null || !payload.TryGetValue(key, out var raw)) yield break;
        foreach (var item in ToList(raw))
        {
            var text = Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text)) yield return TranslateDevelopmentValidationMessage(text);
        }
    }

    private static string TranslateDevelopmentValidationMessage(string message)
    {
        var text = DevelopmentGraphDisplay.ToReadableText(message);
        text = Regex.Replace(text, @"\bduplicate_link\b", "Дублирующая связь", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bself_link\b", "Узел не может требовать сам себя", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\binvalid_currency\b", "Некорректная валюта стоимости", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bnegative_cost\b", "Стоимость не может быть отрицательной", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bcycle_detected\b", "Обнаружен цикл требований", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bhidden_prerequisite\b", "Узел требует скрытый узел", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\brequirement\b", "требование", RegexOptions.IgnoreCase);
        return text.Trim();
    }

    private void ApplyDevelopmentLayoutValidationBadges()
    {
        foreach (var message in DevelopmentLayoutValidationErrors)
        {
            foreach (var node in DevelopmentLayoutNodes.Where(node =>
                         message.IndexOf(node.NodeId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         message.IndexOf(node.DisplayTitle, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                node.IsInvalid = true;
                node.ValidationMessage = message;
            }

            foreach (var link in DevelopmentLayoutLinks.Where(link =>
                         (message.IndexOf(link.SourceNodeId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                          message.IndexOf(link.SourceDisplay, StringComparison.OrdinalIgnoreCase) >= 0) &&
                         (message.IndexOf(link.TargetNodeId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                          message.IndexOf(link.TargetDisplay, StringComparison.OrdinalIgnoreCase) >= 0)))
                link.IsInvalid = true;
        }

        foreach (var message in DevelopmentLayoutValidationWarnings)
        {
            foreach (var node in DevelopmentLayoutNodes.Where(node =>
                         message.IndexOf(node.NodeId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         message.IndexOf(node.DisplayTitle, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (!node.IsInvalid) node.HasWarning = true;
                node.ValidationMessage = FirstNonEmpty(node.ValidationMessage, message);
            }
        }
    }

    private void FocusFirstDevelopmentValidationAffected()
    {
        var affected = DevelopmentLayoutNodes.FirstOrDefault(node => node.IsInvalid || node.HasWarning);
        if (affected == null)
        {
            DevelopmentLayoutStatusText = "Нет узла, связанного с текущей проверкой.";
            return;
        }

        SelectedDevelopmentLayoutNode = affected;
        DevelopmentLayoutStatusText = $"Фокус на проблемном узле: {affected.DisplayTitle}";
    }

    private static int ParseDevelopmentLayoutInt(string value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static double ParseDevelopmentLayoutDouble(string value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private void CreateDevelopmentNode()
    {
        var requestedNodeId = FirstNonEmpty(SelectedClassNodeId, NodeLinkedDefinitionId);
        var nodeId = string.IsNullOrWhiteSpace(requestedNodeId)
            ? Regex.Replace(FirstNonEmpty(NodeName, string.Empty).Trim().ToLowerInvariant(), "[^a-z0-9_]+", "_").Trim('_')
            : requestedNodeId.Trim();
        if (string.IsNullOrWhiteSpace(nodeId)) nodeId = "dev_node_" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var response = _api.DevelopmentHexagonAdminCreateNode(new Dictionary<string, object>
        {
            { "nodeId", nodeId },
            { "hexagonId", SelectedDevelopmentLayoutHexagonId },
            { "hexagonType", NodeHexagonType },
            { "name", string.IsNullOrWhiteSpace(NodeName) ? "Новый узел" : NodeName },
            { "description", NodeDescription },
            { "nodeType", NodeType },
            { "nodeRole", NodeRole },
            { "positionX", string.IsNullOrWhiteSpace(NodePositionX) ? "500" : NodePositionX },
            { "positionY", string.IsNullOrWhiteSpace(NodePositionY) ? "500" : NodePositionY },
            { "cost", string.IsNullOrWhiteSpace(NodeCost) ? "1" : NodeCost },
            { "currencyId", NodeCurrencyId }
        });

        NodeLayoutSaveStatus = response.Status == ResponseStatus.Ok ? "Узел создан." : FirstNonEmpty(response.Message, "Не удалось создать узел.");
        if (response.Status == ResponseStatus.Ok)
        {
            SelectedClassNodeId = nodeId;
            LoadClassTree();
        }
    }

    private void ArchiveDevelopmentNode()
    {
        if (string.IsNullOrWhiteSpace(SelectedClassNodeId)) return;
        var response = _api.DevelopmentHexagonAdminArchiveNode(new Dictionary<string, object>
        {
            { "nodeId", SelectedClassNodeId }
        });
        NodeLayoutSaveStatus = response.Status == ResponseStatus.Ok ? "Узел архивирован." : FirstNonEmpty(response.Message, "Не удалось архивировать узел.");
        if (response.Status == ResponseStatus.Ok) LoadClassTree();
    }

    private void RestoreDevelopmentNode()
    {
        if (string.IsNullOrWhiteSpace(SelectedClassNodeId)) return;
        var response = _api.DevelopmentHexagonAdminRestoreNode(new Dictionary<string, object>
        {
            { "nodeId", SelectedClassNodeId }
        });
        NodeLayoutSaveStatus = response.Status == ResponseStatus.Ok ? "Узел возвращён из архива." : FirstNonEmpty(response.Message, "Не удалось вернуть узел.");
        if (response.Status == ResponseStatus.Ok) LoadClassTree();
    }

    private void AddRequirementLink()
    {
        var source = FirstNonEmpty(RequirementSourceNodeId, SelectedDevelopmentLayoutLink?.SourceNodeId ?? string.Empty);
        var target = FirstNonEmpty(RequirementTargetNodeId, SelectedClassNodeId, SelectedDevelopmentLayoutLink?.TargetNodeId ?? string.Empty);
        var response = _api.DevelopmentHexagonAdminAddRequirementLink(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId },
            { "sourceNodeId", source },
            { "targetNodeId", target }
        });
        NodeLayoutSaveStatus = response.Status == ResponseStatus.Ok ? "Требование добавлено." : FirstNonEmpty(response.Message, "Не удалось добавить требование.");
        if (response.Status == ResponseStatus.Ok) LoadClassTree();
    }

    private void RemoveRequirementLink()
    {
        var source = FirstNonEmpty(RequirementSourceNodeId, SelectedDevelopmentLayoutLink?.SourceNodeId ?? string.Empty);
        var target = FirstNonEmpty(RequirementTargetNodeId, SelectedClassNodeId, SelectedDevelopmentLayoutLink?.TargetNodeId ?? string.Empty);
        var response = _api.DevelopmentHexagonAdminRemoveRequirementLink(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId },
            { "sourceNodeId", source },
            { "targetNodeId", target }
        });
        NodeLayoutSaveStatus = response.Status == ResponseStatus.Ok ? "Требование удалено." : FirstNonEmpty(response.Message, "Не удалось удалить требование.");
        if (response.Status == ResponseStatus.Ok) LoadClassTree();
    }

    private void ValidateDevelopmentGraph()
    {
        var response = _api.DevelopmentHexagonAdminValidateGraph(new Dictionary<string, object>
        {
            { "hexagonId", SelectedDevelopmentLayoutHexagonId }
        });
        DevelopmentLayoutStatusText = response.Status == ResponseStatus.Ok
            ? FirstNonEmpty(response.Message, "Граф проверен.")
            : FirstNonEmpty(response.Message, "Не удалось проверить граф.");
        PopulateDevelopmentLayoutValidation(response);
    }

    private void SaveClassNodeLayout()
    {
        if (string.IsNullOrWhiteSpace(SelectedClassNodeId))
        {
            NodeLayoutSaveStatus = "Раскладка узла сохранена.";
            return;
        }

        var payload = new Dictionary<string, object>
        {
            { "characterId", SelectedCharacterId },
            { "hexagonId", NodeHexagonId },
            { "hexagonType", NodeHexagonType },
            { "nodeId", SelectedClassNodeId },
            { "name", NodeName },
            { "publicName", NodeName },
            { "description", NodeDescription },
            { "publicDescription", NodeDescription },
            { "nodeType", NodeType },
            { "nodeRole", NodeRole },
            { "visibilityRule", NodeVisibilityRule },
            { "positionX", NodePositionX },
            { "positionY", NodePositionY },
            { "ring", NodeRing },
            { "sector", NodeSector },
            { "directionCode", NodeDirectionCode },
            { "branchCode", NodeBranchCode },
            { "sortOrder", NodeSortOrder },
            { "requiredNodeIds", NodeRequiredNodes },
            { "linkedClassId", NodeLinkedClassId },
            { "linkedDefinitionKind", NodeLinkedDefinitionKind },
            { "linkedDefinitionId", NodeLinkedDefinitionId },
            { "cost", NodeCost },
            { "currencyId", NodeCurrencyId },
            { "primaryMagicGroupId", NodePrimaryMagicGroupId },
            { "isPrimaryMagicClass", NodeIsPrimaryMagicClass },
            { "isPlayerVisible", NodeIsPlayerVisible },
            { "isHidden", NodeIsHidden },
            { "isArchived", NodeIsArchived },
            { "layoutLockedManualPosition", NodeLayoutLockedManualPosition }
        };

        var response = _api.DevelopmentHexagonAdminSaveNodeEdit(payload);
        if (response.Status == ResponseStatus.Ok)
        {
            _isClassNodeLayoutDirty = false;
            NodeLayoutSaveStatus = "Раскладка узла сохранена.";
            LoadClassTree();
            return;
        }

        NodeLayoutSaveStatus = FirstNonEmpty(response.Message, "Не удалось сохранить раскладку узла.");
    }

    private void AcquireClassNode()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedClassNodeId)) return;
        _api.DevelopmentNodeUnlock(SelectedCharacterId, SelectedClassNodeId);
        LoadClassTree();
        LoadSkills();
    }

    private void RevokeClassNode()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedClassNodeId)) return;
        _api.DevelopmentNodeRevoke(SelectedCharacterId, SelectedClassNodeId);
        LoadClassTree();
        LoadSkills();
        RefreshCharacterClasses();
    }

    private void CompleteClassNode()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedClassNodeId))
        {
            NodeLayoutSaveStatus = "Раскладка узла сохранена.";
            return;
        }

        var response = _api.DevelopmentAdminNodeComplete(new Dictionary<string, object>
        {
            { "characterId", SelectedCharacterId },
            { "nodeId", SelectedClassNodeId },
            { "hexagonId", NodeHexagonId }
        });

        NodeLayoutSaveStatus = response.Status == ResponseStatus.Ok
            ? "Нет данных"
            : FirstNonEmpty(response.Message, "Нет данных");
        LoadClassTree();
        LoadSkills();
        RefreshCharacterClasses();
    }

    private void LoadSkills()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        LoadSkillsProfileFirst();
        return;
        SkillRows.Clear();
        var r = _api.CharacterSkillsGet(SelectedCharacterId);
        if (r.Status != ResponseStatus.Ok) return;

        var payloadKeys = string.Join(",", r.Payload.Keys.OrderBy(key => key, StringComparer.Ordinal));
        var rawItems = ExtractCharacterSkillsItems(r.Payload, out var rawCollectionKey);
        var mappedCount = 0;
        string firstSkillCode = string.Empty;
        foreach (var item in rawItems)
        {
            var m = AsMap(item, CommandNames.CharacterSkillsGet);
            if (m == null) continue;
            var skillCode = S(m, "skillCode");
            SkillRows.Add(new RowVm
            {
                Id = skillCode,
                Name = skillCode,
                State = $"уровень={S(m, "level")} ранг={S(m, "tier")}",
                Extra = $"получен={S(m, "acquired")} • изучен={S(m, "learnedUtc")}"
            });
            mappedCount++;
            if (string.IsNullOrWhiteSpace(firstSkillCode)) firstSkillCode = skillCode;
        }
        ClientLogService.Instance.Info($"character.skills.response.keys={payloadKeys}");
        ClientLogService.Instance.Info($"character.skills.rawCollectionKey={rawCollectionKey}");
        ClientLogService.Instance.Info($"character.skills.rawCount={rawItems.Count}");
        ClientLogService.Instance.Info($"character.skills.mappedCount={mappedCount}");
        ClientLogService.Instance.Info($"character.skills.firstSkillCode={FirstNonEmpty(firstSkillCode, "<none>")}");
        RestoreSelection(SkillRows, SelectedSkillId, value => SelectedSkillId = value);
        ClientLogService.Instance.Info($"selectedCharacter.skills loaded={SkillRows.Count}");
    }

    private void LoadSkillsProfileFirst()
    {
        SkillRows.Clear();
        var r = _api.CharacterSkillsGet(SelectedCharacterId);
        if (r.Status != ResponseStatus.Ok) return;
        var rawItems = ExtractCharacterSkillsItems(r.Payload, out var rawCollectionKey);
        var mappedCount = 0;
        string firstSkillCode = string.Empty;
        foreach (var item in rawItems)
        {
            var m = AsMap(item, CommandNames.CharacterSkillsGet);
            if (m == null) continue;
            var skillCode = FirstNonEmpty(S(m, "skillCode"), S(m, "skillId"), S(m, "code"));
            if (string.IsNullOrWhiteSpace(skillCode)) continue;
            var displayName = FirstNonEmpty(S(m, "displayName"), S(m, "name"), skillCode);
            var rank = ParseInt(FirstNonEmpty(S(m, "rank"), S(m, "level")), 0);
            var manualBonus = ParseInt(S(m, "manualBonus"), 0);
            var totalBonus = ParseInt(S(m, "totalBonus"), rank + manualBonus);
            SkillRows.Add(new RowVm
            {
                Id = skillCode,
                Name = displayName,
                Rank = rank,
                ManualBonus = manualBonus,
                TrainingState = FirstNonEmpty(S(m, "trainingState"), "trained"),
                IsPlayerVisible = ParseBool(S(m, "isPlayerVisible"), true),
                Category = FirstNonEmpty(S(m, "category"), "other"),
                Attribute = S(m, "defaultAttribute"),
                TotalBonus = totalBonus,
                Breakdown = S(m, "breakdownText"),
                Notes = S(m, "notes"),
                State = $"Нет данных",
                Extra = $"{FirstNonEmpty(S(m, "category"), "other")} | {S(m, "breakdownText")}"
            });
            mappedCount++;
            if (string.IsNullOrWhiteSpace(firstSkillCode)) firstSkillCode = skillCode;
        }
        ClientLogService.Instance.Info($"character.skills.profileFirst.rawCollectionKey={rawCollectionKey}");
        ClientLogService.Instance.Info($"character.skills.profileFirst.mappedCount={mappedCount}");
        ClientLogService.Instance.Info($"character.skills.profileFirst.firstSkillCode={FirstNonEmpty(firstSkillCode, "<none>")}");
        RestoreSelection(SkillRows, SelectedSkillId, value => SelectedSkillId = value);
        RaiseCharacterSkillCommandStates();
        ClientLogService.Instance.Info($"selectedCharacter.skills loaded={SkillRows.Count} sourceOfTruth=character_skill_profiles");
    }

    private void RaiseCharacterSkillCommandStates()
    {
        (AcquireSkillCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (UpdateSkillLevelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveSkillCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void AcquireSkill()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedSkillDefinitionCode)) return;
        ClientLogService.Instance.Info($"character.skill.add selectedSkillCode={SelectedSkillDefinitionCode}");
        var response = _api.CharacterSkillAdd(SelectedCharacterId, SelectedSkillDefinitionCode, CharacterSkillLevelInput);
        ClientLogService.Instance.Info($"character.skill.add response={response.Status}");
        if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(response.Message);
        SelectedSkillId = SelectedSkillDefinitionCode;
        LoadSkills();
    }

    private void UpdateSkillLevel()
    {
        var skillId = FirstNonEmpty(CharacterSkillSelectedSkillIdInput, SelectedSkillId);
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(skillId)) return;
        var selected = SkillRows.FirstOrDefault(x => string.Equals(x.Id, skillId, StringComparison.OrdinalIgnoreCase)) ?? SelectedSkill;
        SkillSaveStatus = $"Навык сохранён.";
        var rank = ParseInt(CharacterSkillLevelText, CharacterSkillLevelInput);
        var manualBonus = ParseInt(CharacterSkillManualBonusText, CharacterSkillManualBonusInput);
        var response = _api.CharacterSkillUpdate(
            SelectedCharacterId,
            skillId,
            rank,
            manualBonus,
            CharacterSkillTrainingStateInput,
            CharacterSkillIsPlayerVisibleInput,
            selected?.Notes ?? string.Empty);
        ClientLogService.Instance.Info($"character.skill.updateLevel response={response.Status}");
        if (response.Status != ResponseStatus.Ok)
        {
            SkillSaveStatus = $"Навык сохранён.";
            throw new InvalidOperationException(response.Message);
        }
        SkillSaveStatus = $"Навык сохранён.";
        LoadSkills();
    }

    private void RemoveSkill()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedSkillId)) return;
        var response = _api.CharacterSkillRemove(SelectedCharacterId, SelectedSkillId);
        ClientLogService.Instance.Info($"character.skill.remove response={response.Status}");
        if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(response.Message);
        SelectedSkillId = string.Empty;
        LoadSkills();
    }

    private void ApplyClassDefinitionEditor(Dictionary<string, object> map)
    {
        EditClassCode = S(map, "code");
        EditClassName = S(map, "name");
        EditClassDescription = S(map, "description");
        EditClassDirectionCode = S(map, "directionCode");
        EditClassBranchCode = S(map, "branchCode");
        EditClassRootClassCode = S(map, "rootClassCode");
        EditClassParentClassCode = S(map, "parentClassCode");
        EditClassRequiredHexagonId = FirstNonEmpty(S(map, "requiredHexagonId"), "main_development_hexagon");
        EditClassRequiredNodeId = S(map, "requiredNodeId");
        EditClassVisibilityRule = FirstNonEmpty(S(map, "visibilityRule"), "hexagon-gated");
        EditClassIsPlayerVisible = ParseBool(S(map, "isPlayerVisible"), false);
        EditClassIsLockedOutsideHexagon = ParseBool(S(map, "isLockedOutsideHexagon"), true);
        EditClassTags = string.Join(", ", ReadStringList(map, "tags"));
        EditClassSortOrder = ParseInt(S(map, "sortOrder"), 0);
        EditClassLevel = ParseInt(S(map, "level"), 1);
        EditClassGrantedSkillCodes = string.Join(", ", ReadStringList(map, "grantedSkillCodes"));
        EditClassRequiredClassCodes = string.Join(", ", ReadStringList(map, "requiredClassCodes"));
        EditClassIsActive = ParseBool(S(map, "isActive"), true);
        EditClassStatus = FirstNonEmpty(S(map, "status"), DefinitionStatus.Draft.ToString());
        SelectedClassDefinitionCode = EditClassCode;
        NotifyClassDefinitionEditor();
    }

    private void ApplySkillDefinitionEditor(Dictionary<string, object> map)
    {
        EditSkillCode = S(map, "code");
        EditSkillName = S(map, "name");
        EditSkillDescription = S(map, "description");
        EditSkillTier = ParseInt(S(map, "tier"), 1);
        EditSkillMaxLevel = ParseInt(S(map, "maxLevel"), 1);
        EditSkillCategory = FirstNonEmpty(S(map, "skillCategory"), SkillCategory.Undefined.ToString());
        EditSkillIsClassSkill = ParseBool(S(map, "isClassSkill"), false);
        EditSkillRequiredClassCodes = string.Join(", ", ReadStringList(map, "requiredClassCodes"));
        EditSkillRequiredSkillCodes = string.Join(", ", ReadStringList(map, "requiredSkillCodes"));
        EditSkillIsActive = ParseBool(S(map, "isActive"), true);
        EditSkillStatus = FirstNonEmpty(S(map, "status"), DefinitionStatus.Draft.ToString());
        SkillLevelEditorRows.Clear();
        foreach (var level in ReadMapList(map, "levels"))
        {
            SkillLevelEditorRows.Add(new SkillLevelEditorRowVm
            {
                Level = ParseInt(S(level, "level"), SkillLevelEditorRows.Count + 1),
                Description = S(level, "description")
            });
        }
        if (SkillLevelEditorRows.Count == 0) SkillLevelEditorRows.Add(new SkillLevelEditorRowVm { Level = 1, Description = string.Empty });
        SelectedSkillDefinitionCode = EditSkillCode;
        NotifySkillDefinitionEditor();
    }

    private Dictionary<string, object> BuildClassDefinitionPayload()
    {
        return new Dictionary<string, object>
        {
            { "code", EditClassCode },
            { "name", EditClassName },
            { "description", EditClassDescription },
            { "directionCode", EditClassDirectionCode },
            { "branchCode", EditClassBranchCode },
            { "rootClassCode", EditClassRootClassCode },
            { "parentClassCode", EditClassParentClassCode },
            { "requiredHexagonId", FirstNonEmpty(EditClassRequiredHexagonId, "main_development_hexagon") },
            { "requiredNodeId", EditClassRequiredNodeId },
            { "visibilityRule", FirstNonEmpty(EditClassVisibilityRule, "hexagon-gated") },
            { "isPlayerVisible", EditClassIsPlayerVisible },
            { "isLockedOutsideHexagon", true },
            { "tags", SplitCsv(EditClassTags).Cast<object>().ToArray() },
            { "sortOrder", EditClassSortOrder },
            { "level", EditClassLevel },
            { "grantedSkillCodes", SplitCsv(EditClassGrantedSkillCodes).Cast<object>().ToArray() },
            { "requiredClassCodes", SplitCsv(EditClassRequiredClassCodes).Cast<object>().ToArray() },
            { "isActive", EditClassIsActive },
            { "status", EditClassStatus }
        };
    }

    private Dictionary<string, object> BuildSkillDefinitionPayload()
    {
        var effectiveMaxLevel = Math.Max(1, EditSkillMaxLevel);
        var configuredByLevel = SkillLevelEditorRows
            .Where(level => level.Level > 0)
            .GroupBy(level => level.Level)
            .ToDictionary(group => group.Key, group => group.Last().Description, EqualityComparer<int>.Default);

        var configuredLevels = new List<Dictionary<string, object>>();
        for (var level = 1; level <= effectiveMaxLevel; level++)
        {
            configuredLevels.Add(new Dictionary<string, object>
            {
                { "level", level },
                { "description", configuredByLevel.TryGetValue(level, out var description) ? description : string.Empty },
                { "requirements", new object[0] },
                { "effects", new object[0] }
            });
        }

        var firstLevel = configuredLevels.FirstOrDefault();
        ClientLogService.Instance.Debug($"skillDefinition.payload.levels shape count={configuredLevels.Count} itemKeys={string.Join(",", firstLevel?.Keys?.ToArray() ?? Array.Empty<string>())}");

        return new Dictionary<string, object>
        {
            { "code", EditSkillCode },
            { "name", EditSkillName },
            { "description", EditSkillDescription },
            { "tier", EditSkillTier },
            { "maxLevel", effectiveMaxLevel },
            { "skillCategory", EditSkillCategory },
            { "isClassSkill", EditSkillIsClassSkill },
            { "requiredClassCodes", SplitCsv(EditSkillRequiredClassCodes).Cast<object>().ToArray() },
            { "requiredSkillCodes", SplitCsv(EditSkillRequiredSkillCodes).Cast<object>().ToArray() },
            { "levels", configuredLevels.Cast<object>().ToArray() },
            { "isActive", EditSkillIsActive },
            { "status", EditSkillStatus }
        };
    }

    private void NotifyClassDefinitionEditor()
    {
        Notify(nameof(EditClassCode)); Notify(nameof(EditClassName)); Notify(nameof(EditClassDescription)); Notify(nameof(EditClassDirectionCode)); Notify(nameof(EditClassBranchCode)); Notify(nameof(EditClassRootClassCode)); Notify(nameof(EditClassParentClassCode)); Notify(nameof(EditClassRequiredHexagonId)); Notify(nameof(EditClassRequiredNodeId)); Notify(nameof(EditClassVisibilityRule)); Notify(nameof(EditClassIsPlayerVisible)); Notify(nameof(EditClassIsLockedOutsideHexagon)); Notify(nameof(EditClassTags)); Notify(nameof(EditClassSortOrder)); Notify(nameof(EditClassLevel)); Notify(nameof(EditClassGrantedSkillCodes)); Notify(nameof(EditClassRequiredClassCodes)); Notify(nameof(EditClassIsActive)); Notify(nameof(EditClassStatus)); Notify(nameof(DefinitionHintText));
    }

    private void NotifySkillDefinitionEditor()
    {
        Notify(nameof(EditSkillCode)); Notify(nameof(EditSkillName)); Notify(nameof(EditSkillDescription)); Notify(nameof(EditSkillTier)); Notify(nameof(EditSkillMaxLevel)); Notify(nameof(EditSkillCategory)); Notify(nameof(EditSkillIsClassSkill)); Notify(nameof(EditSkillRequiredClassCodes)); Notify(nameof(EditSkillRequiredSkillCodes)); Notify(nameof(EditSkillIsActive)); Notify(nameof(EditSkillStatus)); Notify(nameof(SkillEditorHintText));
    }

    private static int ParseInt(string value, int fallback) => int.TryParse(value, out var parsed) ? parsed : fallback;
    private static int ParseLevelFromSkillState(string state)
    {
        if (string.IsNullOrWhiteSpace(state)) return 1;
        var marker = "level=";
        var index = state.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return 1;
        var start = index + marker.Length;
        var end = state.IndexOf(' ', start);
        var raw = end > start ? state.Substring(start, end - start) : state.Substring(start);
        return int.TryParse(raw, out var parsed) && parsed > 0 ? parsed : 1;
    }
    private static bool ParseBool(string value, bool fallback) => bool.TryParse(value, out var parsed) ? parsed : fallback;
    private static List<string> SplitCsv(string value) => value.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim()).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static List<string> ReadStringList(Dictionary<string, object> map, string key) => ToList(map.ContainsKey(key) ? map[key] : new ArrayList()).Cast<object>().Select(item => Convert.ToString(item) ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
    private static List<Dictionary<string, object>> ReadMapList(Dictionary<string, object> map, string key) => ToList(map.ContainsKey(key) ? map[key] : new ArrayList()).OfType<Dictionary<string, object>>().ToList();
    private ResponseEnvelope EnsureSuccess(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
        {
            if (ConflictResponseParser.TryParseConflict(response, out var conflict))
            {
                _entityRevisions.SetRevision(conflict.EntityType, conflict.EntityId, conflict.CurrentRevision, "conflict");
                ClientLogService.Instance.Warn($"revision.client.set entityType={conflict.EntityType} entityId={conflict.EntityId} revision={conflict.CurrentRevision} source=conflict");
                ClientLogService.Instance.Warn($"conflict.definition_save entityType={conflict.EntityType} entityId={conflict.EntityId}");
            }
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? response.Status.ToString() : response.Message);
        }

        LastErrorMessage = string.Empty;
        return response;
    }

    private ResponseEnvelope SendDefinitionArchiveWithRevision(string command, string code)
    {
        var payload = new Dictionary<string, object> { { "code", code } };
        var entityType = command.IndexOf("skill", StringComparison.OrdinalIgnoreCase) >= 0
            ? "definition:skill"
            : "definition:class";
        AttachExpectedRevision(payload, entityType, code, command);
        return command == CommandNames.DefinitionsSkillArchive ? _api.DefinitionSkillArchivePayload(payload) : _api.DefinitionClassArchivePayload(payload);
    }

    private void AttachExpectedRevision(Dictionary<string, object> payload, string entityType, string entityId, string command)
    {
        if (!RevisionFeatureFlags.UseDefinitionExpectedRevision) return;
        if (_entityRevisions.TryGetExpectedRevision(entityType, entityId, out var expectedRevision))
        {
            payload["expectedRevision"] = expectedRevision;
            ClientLogService.Instance.Info($"revision.client.attach_expected command={command} entityType={entityType} entityId={entityId} expected={expectedRevision}");
        }
        else
        {
            ClientLogService.Instance.Info($"revision.client.missing entityType={entityType} entityId={entityId}");
        }
    }

    private void UpdateRevisionAfterDefinitionResponse(ResponseEnvelope response, string entityType, string entityId, string command)
    {
        if (response.Payload.TryGetValue("currentRevision", out var currentRaw) && long.TryParse(Convert.ToString(currentRaw), out var currentRevision))
        {
            _entityRevisions.SetRevision(entityType, entityId, currentRevision, "response.currentRevision");
            ClientLogService.Instance.Info($"revision.client.set entityType={entityType} entityId={entityId} revision={currentRevision} source=response.currentRevision");
            return;
        }

        _entityRevisions.MarkStale(entityType, entityId);
        ClientLogService.Instance.Info($"revision.client.not_returned command={command} entityType={entityType} entityId={entityId}");
    }

    private void ChatSend()
    {
        if (string.IsNullOrWhiteSpace(ChatMessageText)) return;
        var sessionId = ResolveChatSessionId();
        var serverChatType = MapChatTypeToServer(ChatMessageType);
        ClientLogService.Instance.Info($"Chat send requested: sessionId={sessionId}; command={CommandNames.ChatSend}; uiType={ChatMessageType}; serverType={serverChatType}");
        _api.ChatSend(sessionId, serverChatType, ChatMessageText);
        ChatMessageText = string.Empty;
        Notify(nameof(ChatMessageText));
        ChatRefresh();
    }

    private void ChatRefresh()
    {
        var sessionId = ResolveChatSessionId();
        TraceChatDiagnostic($"request command={CommandNames.ChatVisibleFeed} session={sessionId}");
        ChatRows.Clear();
        ChatMessageRows.Clear();
        var feed = _api.ChatVisibleFeed(sessionId, 80);
        var feedItems = ExtractChatItems(feed.Payload, out var sourceKey, out var payloadKeys, out var rawItemsType);
        TraceChatDiagnostic($"response command={CommandNames.ChatVisibleFeed} status={feed.Status} success={(feed.Status == ResponseStatus.Ok)} payloadKeys=[{payloadKeys}] sourceKey={sourceKey} rawItems={feedItems.Count} rawType={rawItemsType}");
        LogFirstChatItemShape(feedItems, CommandNames.ChatVisibleFeed);
        if (feed.Status == ResponseStatus.Ok)
        {
            var mappedCount = 0;
            var filteredCount = 0;
            foreach (var item in feedItems)
            {
                var m = AsMap(item, CommandNames.ChatVisibleFeed);
                if (m == null) continue;
                mappedCount++;
                var row = BuildChatMessageRow(m);
                if (row == null)
                {
                    filteredCount++;
                    continue;
                }

                ChatRows.Add($"{row.Sender}: {row.Text}");
                ChatMessageRows.Add(row);
            }
            TraceChatDiagnostic($"mapped command={CommandNames.ChatVisibleFeed} mappedItems={mappedCount} filteredOut={filteredCount} displayItems={ChatMessageRows.Count}");
        }
        else
        {
            TraceChatDiagnostic($"response-error command={CommandNames.ChatVisibleFeed} message={feed.Message}");
        }
        TraceChatDiagnostic($"collection command={CommandNames.ChatVisibleFeed} chatRows={ChatRows.Count} uiCollection=ChatMessageRows uiCount={ChatMessageRows.Count}");
        ClientLogService.Instance.Debug($"ui-refresh section=... block=... loaded={ChatRows.Count} visible={ChatMessageRows.Count}");
        RefreshDiceFeedForChat();
        MergeDiceIntoChatFeed();

        var unread = _api.ChatUnreadGet(sessionId);
        ChatUnreadText = "Unread: " + S(unread.Payload, "count");
        Notify(nameof(ChatUnreadText));

        var slow = _api.ChatSlowModeGet(sessionId);
        ChatSlowPublicSeconds = int.TryParse(S(slow.Payload, "publicSeconds"), out var ps) ? ps : 0;
        ChatSlowHiddenSeconds = int.TryParse(S(slow.Payload, "hiddenToAdminsSeconds"), out var hs) ? hs : 0;
        ChatSlowAdminOnlySeconds = int.TryParse(S(slow.Payload, "adminOnlySeconds"), out var a) ? a : 0;
        Notify(nameof(ChatSlowPublicSeconds)); Notify(nameof(ChatSlowHiddenSeconds)); Notify(nameof(ChatSlowAdminOnlySeconds));

        ChatRestrictionRows.Clear();
        var restrictions = _api.ChatRestrictionsGet(sessionId);
        ChatRestrictionRows.Add("LockPlayers=" + S(restrictions.Payload, "lockPlayers"));
        foreach (var item in ToList(restrictions.Payload.ContainsKey("restrictions") ? restrictions.Payload["restrictions"] : new ArrayList()))
            if (AsMap(item) is { } m)
                ChatRestrictionRows.Add($"{S(m, "userId")} muted={S(m, "muted")} reason={S(m, "reason")}");
        TraceChatDiagnostic($"ui chatRows={ChatRows.Count} chatMessageRows={ChatMessageRows.Count} restrictionsRows={ChatRestrictionRows.Count}");
        RefreshConnectionSummary();
    }

    private void ChatMuteUser() { if (!string.IsNullOrWhiteSpace(ChatModerationUserId)) { _api.ChatRestrictionsMuteUser(ResolveChatSessionId(), ChatModerationUserId, ChatModerationReason); ChatRefresh(); } }
    private void ChatUnmuteUser() { if (!string.IsNullOrWhiteSpace(ChatModerationUserId)) { _api.ChatRestrictionsUnmuteUser(ResolveChatSessionId(), ChatModerationUserId); ChatRefresh(); } }
    private void ChatLockPlayers() { _api.ChatRestrictionsLockPlayers(ResolveChatSessionId()); ChatRefresh(); }
    private void ChatUnlockPlayers() { _api.ChatRestrictionsUnlockPlayers(ResolveChatSessionId()); ChatRefresh(); }

    private void ChatSetSlowMode()
    {
        _api.ChatSlowModeSet(ResolveChatSessionId(), ChatSlowPublicSeconds, ChatSlowHiddenSeconds, ChatSlowAdminOnlySeconds);
        ChatRefresh();
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

    private void AudioRefresh()
    {
        AudioRefreshMvp();
        return;

        var state = _api.AudioStateGet(AudioSessionId);
        var mode = S(state.Payload, "mode");
        var category = S(state.Payload, "category");
        var track = FirstNonEmpty(S(state.Payload, "trackName"), "Нет данных");
        var position = FirstNonEmpty(S(state.Payload, "positionSeconds"), "0");
        var playback = FirstNonEmpty(S(state.Payload, "playbackState"), "нет данных");
        AudioStateText = $"режим: {mode}; категория: {category}; трек: {track}; позиция: {position} сек.; состояние: {playback}";
        ClientLogService.Instance.Info($"ui-audio-refresh section=... stateLoaded=true tracksRaw={state.Payload.Count}");
        Notify(nameof(AudioStateText));

        AudioLibraryRows.Clear();
        var lib = _api.AudioLibraryGet();
        if (lib.Status == ResponseStatus.Ok && lib.Payload.ContainsKey("items"))
        {
            foreach (var item in ToList(lib.Payload["items"]))
                if (item is Dictionary<string, object> m)
                    AudioLibraryRows.Add($"{S(m, "trackId")} | {S(m, "category")} | {S(m, "displayName")} | {S(m, "filePath")}");
        }
        RefreshConnectionSummary();
    }

    private void AudioSetMode()
    {
        _api.AudioAdminStateSetCategory(AudioSessionId, AudioCategoryInput);
        _api.AudioAdminStateSetLoopMode(AudioSessionId, AudioLoopModeInput);
        if (double.TryParse(AudioFadeSecondsInput.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var fade))
            _api.AudioAdminStateSetFade(AudioSessionId, fade);
        AudioRefresh();
    }
    private void AudioClearOverride() { _api.AudioAdminStateResync(AudioSessionId); AudioRefresh(); }
    private void AudioNextTrack() { var r = _api.AudioAdminStateNext(AudioSessionId); AudioStatusText = r.Message; AudioRefresh(); }
    private void AudioSelectTrack() { if (!string.IsNullOrWhiteSpace(AudioSelectedTrackId)) { var r = _api.AudioAdminStatePlay(AudioSessionId, AudioSelectedTrackId); AudioStatusText = r.Message; AudioRefresh(); } }
    private void AudioPause() { var r = _api.AudioAdminStatePause(AudioSessionId); AudioStatusText = r.Message; AudioRefresh(); }
    private void AudioStop() { var r = _api.AudioAdminStateStop(AudioSessionId); AudioStatusText = r.Message; AudioRefresh(); }
    private void AudioResync() { var r = _api.AudioAdminStateResync(AudioSessionId); AudioStatusText = r.Message; AudioRefresh(); }
    private void AudioReloadLibrary() { _api.AudioTrackReload(); AudioRefresh(); }

    private void AudioRefreshMvp()
    {
        var state = _api.AudioAdminStateGet(AudioSessionId);
        if (state.Status != ResponseStatus.Ok)
        {
            AudioStatusText = state.Message;
            AudioStateText = state.Message;
            NotifyAudioStateProperties();
            return;
        }

        ApplyAdminAudioState(state.Payload);
        AudioLibraryRows.Clear();
        AudioTrackRows.Clear();
        var lib = _api.AudioAdminTracksList();
        if (lib.Status == ResponseStatus.Ok && lib.Payload.ContainsKey("items"))
        {
            foreach (var item in ToList(lib.Payload["items"]))
                if (item is Dictionary<string, object> m)
                {
                    var row = new RowVm
                    {
                        Id = FirstNonEmpty(S(m, "trackId"), S(m, "id")),
                        Name = S(m, "displayName"),
                        Category = S(m, "category"),
                        State = S(m, "visibility"),
                        Extra = $"виден игрокам={S(m, "isPlayerVisible")}; rev={S(m, "revision")}",
                        IsPlayerVisible = string.Equals(S(m, "isPlayerVisible"), "True", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(S(m, "isPlayerVisible"), "true", StringComparison.OrdinalIgnoreCase)
                    };
                    AudioTrackRows.Add(row);
                    AudioLibraryRows.Add($"{row.Id} | {row.Category} | {row.Name} | {row.State}");
                }
        }

        if (SelectedAudioTrackRow == null && AudioTrackRows.Count > 0)
            SelectedAudioTrackRow = AudioTrackRows[0];
        else if (!string.IsNullOrWhiteSpace(AudioSelectedTrackId))
            SelectedAudioTrackRow = AudioTrackRows.FirstOrDefault(x => string.Equals(x.Id, AudioSelectedTrackId, StringComparison.OrdinalIgnoreCase));

        AudioStatusText = "Аудио обновлено.";
        NotifyAudioStateProperties();
        RefreshConnectionSummary();
    }

    private void ApplyAdminAudioState(Dictionary<string, object> payload)
    {
        var category = FirstNonEmpty(S(payload, "currentCategory"), S(payload, "category"), "—");
        var track = FirstNonEmpty(S(payload, "trackDisplayName"), S(payload, "trackName"), "Без названия");
        var position = FirstNonEmpty(S(payload, "positionSeconds"), "0");
        var playback = FirstNonEmpty(S(payload, "playbackStateText"), S(payload, "playbackState"), "—");
        AudioCurrentTrackTitle = track;
        AudioCurrentCategory = category;
        AudioPlaybackStateText = playback;
        AudioSelectedTrackId = FirstNonEmpty(S(payload, "trackId"), AudioSelectedTrackId);
        AudioStateText = $"Категория: {category}; трек: {track}; позиция: {position} сек.; состояние: {playback}";
        ClientLogService.Instance.Info($"admin.audio.refresh stateLoaded=true track={AudioSelectedTrackId} category={category}");
    }

    private void NotifyAudioStateProperties()
    {
        Notify(nameof(AudioStateText));
        Notify(nameof(AudioStatusText));
        Notify(nameof(AudioCurrentTrackTitle));
        Notify(nameof(AudioCurrentCategory));
        Notify(nameof(AudioPlaybackStateText));
        Notify(nameof(AudioSelectedTrackId));
        Notify(nameof(AudioTrackRows));
        Notify(nameof(AudioSelectedTrackTitle));
    }

    private void VisibilityLoad()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        var r = _api.VisibilityGet(SelectedCharacterId);
        VisHideDescription = S(r.Payload, "hideDescriptionForOthers") == "True";
        VisHideBackstory = S(r.Payload, "hideBackstoryForOthers") == "True";
        VisHideStats = S(r.Payload, "hideStatsForOthers") == "True";
        VisHideReputation = S(r.Payload, "hideReputationForOthers") == "True";
        Notify(nameof(VisHideDescription)); Notify(nameof(VisHideBackstory)); Notify(nameof(VisHideStats)); Notify(nameof(VisHideReputation));
    }

    private void VisibilitySave()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        _api.VisibilityUpdate(new Dictionary<string, object> { { "characterId", SelectedCharacterId }, { "hideDescriptionForOthers", VisHideDescription }, { "hideBackstoryForOthers", VisHideBackstory }, { "hideStatsForOthers", VisHideStats }, { "hideReputationForOthers", VisHideReputation } });
        VisibilityLoad();
    }

    private void NotesRefresh()
    {
        NotesRows.Clear();
        var r = _api.NotesList(new Dictionary<string, object> { { "sessionId", NoteSessionId }, { "targetType", NoteTargetType }, { "targetId", NoteTargetId } });
        foreach (var item in ToList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                NotesRows.Add($"{S(m, "noteId")} | {S(m, "visibility")} | {S(m, "title")} | {S(m, "text")}");
    }

    private void NotesCreate()
    {
        _api.NotesCreate(new Dictionary<string, object> { { "sessionId", NoteSessionId }, { "targetType", NoteTargetType }, { "targetId", NoteTargetId }, { "title", NoteTitle }, { "text", NoteText }, { "visibility", NoteVisibility }, { "noteType", "Session" } });
        NotesRefresh();
    }

    private void NotesArchive() { if (!string.IsNullOrWhiteSpace(SelectedNoteId)) { _api.NotesArchive(SelectedNoteId); NotesRefresh(); } }

    private void ReferenceRefresh()
    {
        ReferenceItems.Clear();
        var r = _api.ReferenceList(ReferenceWorldId, ReferenceType);
        foreach (var item in ToList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                ReferenceItems.Add(new RowVm
                {
                    Id = S(m, "referenceId"),
                    Name = S(m, "displayName"),
                    State = S(m, "referenceType"),
                    Extra = $"key={S(m, "key")}"
                });
        RestoreSelection(ReferenceItems, ReferenceId, value => ReferenceId = value);
    }

    private void ReferenceCreate() { _api.ReferenceCreate(new Dictionary<string, object> { { "worldId", ReferenceWorldId }, { "referenceType", ReferenceType }, { "key", ReferenceKey }, { "displayName", ReferenceDisplayName }, { "dataJson", ReferenceDataJson } }); ReferenceRefresh(); }
    private void ReferenceUpdate() { if (!string.IsNullOrWhiteSpace(ReferenceId)) { _api.ReferenceUpdate(new Dictionary<string, object> { { "referenceId", ReferenceId }, { "displayName", ReferenceDisplayName }, { "dataJson", ReferenceDataJson } }); ReferenceRefresh(); } }
    private void ReferenceArchive() { if (!string.IsNullOrWhiteSpace(ReferenceId)) { _api.ReferenceArchive(ReferenceId); ReferenceRefresh(); } }

    private void BackupRefresh()
    {
        BackupItems.Clear();
        var r = _api.BackupList();
        foreach (var item in ToList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                BackupItems.Add(new RowVm { Id = S(m, "backupId"), Name = S(m, "label"), State = "Резервная копия", Extra = S(m, "createdUtc") });
        RestoreSelection(BackupItems, SelectedBackupId, value => SelectedBackupId = value);
    }

    private void BackupCreate() { _api.BackupCreate(string.IsNullOrWhiteSpace(BackupLabel) ? "manual-backup" : BackupLabel); BackupRefresh(); }
    private void BackupRestore() { if (!string.IsNullOrWhiteSpace(SelectedBackupId)) { _api.BackupRestore(SelectedBackupId); BackupRefresh(); } }
    private void BackupExport() { if (!string.IsNullOrWhiteSpace(SelectedBackupId)) { _api.BackupExport(SelectedBackupId); } }

    private void DiagnosticsRefresh()
    {
        DiagnosticsItems.Clear();
        var s1 = _api.AdminServerStatus();
            DiagnosticsItems.Add(new RowVm { Id = "server-status", Name = "Диагностика", State = $"online={S(s1.Payload, "onlineUsers")}", Extra = $"utc={S(s1.Payload, "utcNow")}" });
        var s2 = _api.AdminSessionsList();
        DiagnosticsItems.Add(new RowVm { Id = "sessions", Name = "Диагностика", State = "Диагностика", Extra = ToList(s2.Payload.ContainsKey("items") ? s2.Payload["items"] : new ArrayList()).Count.ToString() });
        LoadLocksSummary();
        DiagnosticsItems.Add(new RowVm { Id = "locks", Name = "Диагностика", State = "Диагностика", Extra = LocksCount.ToString() });
        RestoreSelection(DiagnosticsItems, SelectedDiagnosticsId, value => SelectedDiagnosticsId = value);
        RefreshConnectionSummary();
    }

    private void LoadLocksSummary()
    {
        LockRows.Clear();
        var locks = _api.AdminLocksList();
        var items = ToList(locks.Payload.ContainsKey("items") ? locks.Payload["items"] : new ArrayList());
        LocksCount = items.Count;
        foreach (var item in items)
        {
            if (item is not Dictionary<string, object> map) continue;
            var resourceId = FirstNonEmpty(S(map, "characterId"), S(map, "entityId"), S(map, "resourceId"), S(map, "lockId"));
            var owner = FirstNonEmpty(S(map, "lockedByUserId"), S(map, "ownerUserId"), S(map, "ownerId"), S(map, "login"), "unknown owner");
            var state = FirstNonEmpty(S(map, "entityType"), S(map, "lockType"), S(map, "scope"), S(map, "resourceType"), "character");
            var extra = string.Join(" • ", new[]
            {
                FirstNonEmpty(S(map, "resourceName"), S(map, "displayName"), resourceId),
                FirstNonEmpty(S(map, "ownerLevel"), S(map, "role")),
                FirstNonEmpty(S(map, "issuedUtc"), S(map, "acquiredUtc"), S(map, "createdUtc")),
                FirstNonEmpty(S(map, "expiresUtc"), " ")
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
            LockRows.Add(new RowVm { Id = resourceId, Name = owner, State = state, Extra = extra });
        }
        Notify(nameof(FilteredLockRows));
        ClientLogService.Instance.Debug($"ui-refresh section=... block=... raw={items.Count} shown={LockRows.Count}");
        ClientLogService.Instance.Info($"people.grid.rows count={LockRows.Count}");
        ClientLogService.Instance.Debug("people.grid.render ok");
        RestoreSelection(LockRows, SelectedLockId, value => SelectedLockId = value);
    }

    private string CurrentSelectedRequestId() => FirstNonEmptyAdmin(SelectedPendingRequestId, _selectedRequestDetailsId);
    private void MarkInReviewRequest() { var requestId = CurrentSelectedRequestId(); if (!string.IsNullOrWhiteSpace(requestId)) { RunUiAction("Взять заявку в рассмотрение", () => { var response = _api.AdminRequestSetInReview(requestId, RequestComment); ApplySelectedRequestDetails(response); RefreshModerationSection(); }); } else { ClientLogService.Instance.Warn("admin.request.markInReview.skipped reason=no-selected-request"); } }
    private void ApproveRequest() { var requestId = CurrentSelectedRequestId(); if (!string.IsNullOrWhiteSpace(requestId)) { RunUiAction("Одобрить заявку", () => { var response = _api.AdminRequestApprove(requestId, RequestComment, RequestGMOnlyComment); ApplySelectedRequestDetails(response); RefreshModerationSection(); }); } else { ClientLogService.Instance.Warn("admin.request.approve.skipped reason=no-selected-request"); } }
    private void RejectRequest() { var requestId = CurrentSelectedRequestId(); if (!string.IsNullOrWhiteSpace(requestId)) { RunUiAction("Отклонить заявку", () => { var response = _api.AdminRequestReject(requestId, RequestComment, RequestGMOnlyComment); ApplySelectedRequestDetails(response); RefreshModerationSection(); }); } else { ClientLogService.Instance.Warn("admin.request.reject.skipped reason=no-selected-request"); } }
    private void RequestChangesForSelectedRequest() { var requestId = CurrentSelectedRequestId(); if (!string.IsNullOrWhiteSpace(requestId)) { RunUiAction("Запросить уточнения по заявке", () => { var response = _api.AdminRequestRequestChanges(requestId, RequestComment, RequestGMOnlyComment); ApplySelectedRequestDetails(response); RefreshModerationSection(); }); } else { ClientLogService.Instance.Warn("admin.request.requestChanges.skipped reason=no-selected-request"); } }
    private void ArchiveSelectedRequest() { var requestId = CurrentSelectedRequestId(); if (!string.IsNullOrWhiteSpace(requestId)) { RunUiAction("Архивировать заявку", () => { var response = _api.AdminRequestArchive(requestId, RequestComment); ApplySelectedRequestDetails(response); RefreshModerationSection(); }); } else { ClientLogService.Instance.Warn("admin.request.archive.skipped reason=no-selected-request"); } }    private void AcquireLock() { if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return; RunUiAction("Архивировать заявку", () => { var r = _api.AcquireCharacterLock(SelectedCharacterId); LockStateText = r.Message; Notify(nameof(LockStateText)); LoadLocksSummary(); }); }
    private void ReleaseLock() { if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return; RunUiAction("Снять блокировку", () => { var r = _api.ReleaseCharacterLock(SelectedCharacterId); LockStateText = r.Message; Notify(nameof(LockStateText)); LoadLocksSummary(); }); }
    private void ForceUnlock()
    {
        var entityId = FirstNonEmpty(SelectedLock?.Id ?? string.Empty, SelectedCharacterId);
        var entityType = FirstNonEmpty(SelectedLock?.State ?? string.Empty, "character");
        if (string.IsNullOrWhiteSpace(entityId)) return;

        var confirmation = System.Windows.MessageBox.Show(
            $"Нет данных",
            "Нет данных",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirmation != System.Windows.MessageBoxResult.Yes) return;

        RunUiAction("Принудительно снять lock", () =>
        {
            var r = _api.AdminLocksForceRelease(entityType, entityId);
            LockStateText = r.Message;
            Notify(nameof(LockStateText));
            LoadLocksSummary();
        });
    }
    private void SaveBasicInfo()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        RunUiAction("Выполнить действие", () =>
        {
            ClientLogService.Instance.Info("ui-action section=... action=SaveBasic");
            var response = _api.CharacterAdminSaveBasic(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "name", EditName },
                { "race", EditRace },
                { "height", EditHeight },
                { "age", EditAge },
                { "description", EditDescription },
                { "backstory", EditBackstory }
            });
            ClientLogService.Instance.Info($"character.admin.save.basic response={response.Status}:{response.Message}");
            EnsureSuccess(response);
            OpenCharacter();
        });
    }

    private void SaveBiography()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        RunUiAction("пїЅпїЅпїЅпїЅпїЅпїЅпїЅпїЅпїЅпїЅ пїЅпїЅпїЅпїЅпїЅпїЅ", () =>
        {
            ClientLogService.Instance.Info($"character.admin.biography.save.start characterId={SelectedCharacterId} length={EditBackstory?.Length ?? 0}");
            BiographySaveStatus = $"Биография сохранена.";
            EnsureSelectedCharacterLockForEdit();
            var response = _api.CharacterAdminSaveBiography(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "description", EditDescription },
                { "backstory", EditBackstory ?? string.Empty }
            });
            ClientLogService.Instance.Info($"character.admin.save.biography response={response.Status}:{response.Message}");
            if (response.Status != ResponseStatus.Ok)
            {
                BiographySaveStatus = $"Ошибка сохранения: {response.Message}";
                ClientLogService.Instance.Warn($"character.admin.biography.save.failed characterId={SelectedCharacterId} status={response.Status} message={response.Message}");
                EnsureSuccess(response);
            }

            BiographySaveStatus = $"Биография сохранена.";
            ClientLogService.Instance.Info($"character.admin.biography.save.done characterId={SelectedCharacterId} length={EditBackstory?.Length ?? 0}");
            OpenCharacter();
            BiographySaveStatus = $"Биография сохранена.";
        });
    }

    private void SaveStats()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        ClientLogService.Instance.Info($"character.admin.attributes.save.canExecute={CanManageSelectedCharacter} characterId={SelectedCharacterId}");
        RunUiAction("Выполнить действие", () =>
        {
            ClientLogService.Instance.Info("ui-action section=... action=SaveAttributes");
            var allStatRows = AttributeEditorRows
                .Concat(VitalsEditorRows)
                .Concat(DerivedStatEditorRows)
                .Where(row => !string.IsNullOrWhiteSpace(row.AttributeId))
                .GroupBy(row => row.AttributeId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            ClientLogService.Instance.Info($"character.admin.attributes.save.start characterId={SelectedCharacterId} attributes={AttributeEditorRows.Count} vitals={VitalsEditorRows.Count} derived={DerivedStatEditorRows.Count}");
            CharacterStatsSaveStatus = $"Показатели сохранены.";
            EnsureSelectedCharacterLockForEdit();
            var attributesPayload = allStatRows
                .Select(row => (object)new Dictionary<string, object>
                {
                    { "attributeId", row.AttributeId },
                    { "code", row.Code },
                    { "value", row.Value }
                })
                .ToArray();
            var response = _api.CharacterAdminSaveStats(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "attributes", attributesPayload }
            });
            ClientLogService.Instance.Info($"character.admin.save.attributes response={response.Status}:{response.Message}");
            if (response.Status != ResponseStatus.Ok)
            {
                CharacterStatsSaveStatus = $"Ошибка сохранения: {response.Message}";
                ClientLogService.Instance.Warn($"character.admin.attributes.save.failed characterId={SelectedCharacterId} status={response.Status} message={response.Message}");
                EnsureSuccess(response);
            }
            var subAttributePayload = AttributeEditorRows
                .SelectMany(row => row.SubAttributes)
                .Where(row => !string.IsNullOrWhiteSpace(row.SubAttributeId))
                .Select(row => (object)new Dictionary<string, object>
                {
                    { "subAttributeId", row.SubAttributeId },
                    { "parentAttributeId", row.ParentAttributeId },
                    { "value", row.Value },
                    { "manualBonus", row.ManualBonus },
                    { "notes", row.Notes ?? string.Empty }
                })
                .ToArray();
            if (subAttributePayload.Length > 0)
            {
                var subResponse = _api.CharacterSubAttributesAdminUpdate(new Dictionary<string, object>
                {
                    { "characterId", SelectedCharacterId },
                    { "subAttributes", subAttributePayload }
                });
                ClientLogService.Instance.Info($"character.admin.subattributes.save response={subResponse.Status}:{subResponse.Message}");
                if (subResponse.Status != ResponseStatus.Ok)
                {
                    CharacterStatsSaveStatus = $"Ошибка сохранения подхарактеристик: {subResponse.Message}";
                    EnsureSuccess(subResponse);
                }
            }
            CharacterStatsSaveStatus = $"Показатели сохранены: {allStatRows.Count}; подхарактеристики: {subAttributePayload.Length}.";
            ClientLogService.Instance.Info($"character.admin.attributes.save.done characterId={SelectedCharacterId} count={allStatRows.Count} subAttributes={subAttributePayload.Length}");
            OpenCharacter();
        });
    }
    private void SaveMoney()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        RunUiAction("Выполнить действие", () =>
        {
            ClientLogService.Instance.Info("ui-action section=... action=SaveCurrencies");
            CharacterMoneySaveStatus = $"Валюты сохранены.";
            EnsureSelectedCharacterLockForEdit();
            var currencyPayload = CurrencyEditorRows
                .OrderBy(row => row.SortOrder)
                .Select(row => (object)new Dictionary<string, object>
                {
                    { "currencyId", row.CurrencyId },
                    { "code", row.Code },
                    { "amount", row.Amount }
                })
                .ToArray();
            var moneyPayload = CurrencyEditorRows
                .Where(row => !row.IsExperience)
                .GroupBy(row => row.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(row => row.Key, row => (object)row.Last().Amount, StringComparer.OrdinalIgnoreCase);
            var xpRow = CurrencyEditorRows.FirstOrDefault(row => row.IsExperience);
            if (xpRow != null)
            {
                ExperienceCoins = xpRow.Amount;
            }
            var payload = new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "money", moneyPayload },
                { "currencies", currencyPayload },
                { "xpCoins", ExperienceCoins }
            };
            ClientLogService.Instance.Info($"character.update.money payloadKeys={string.Join(",", payload.Keys.OrderBy(key => key, StringComparer.Ordinal))}");
            ClientLogService.Instance.Info("character.money.save request currencies=" + string.Join(",", CurrencyEditorRows.Select(row => $"{row.Code}:{row.Amount}")));
            var payloadText = string.Join(
                " | ",
                moneyPayload.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => kv.Key + ":" + kv.Value));
            ClientLogService.Instance.Info("character.money.save payload={" + payloadText + "}");
            var response = _api.CharacterAdminSaveMoney(payload);
            ClientLogService.Instance.Info($"character.admin.save.money response={response.Status}:{response.Message}");
            EnsureSuccess(response);
            CharacterMoneySaveStatus = $"Валюты сохранены: {CurrencyEditorRows.Count}.";
            OpenCharacter();
        });
    }

    private void SaveXpCoins()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        ClientLogService.Instance.Info("ui-action section=... action=SaveXpCoinsViaWallet");
        SaveMoney();
    }
    private void ApproveSelected() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) RunUiAction("Выполнить действие", () => { ClientLogService.Instance.Info($"admin.account.approve.requested accountId={SelectedPendingAccountId}"); _api.ApproveAccount(SelectedPendingAccountId); RefreshPeopleSection(); }); }
    private void ArchiveSelected() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) RunUiAction("Выполнить действие", () => { _api.ArchiveAccount(SelectedPendingAccountId); RefreshPeopleSection(); }); }
    private void RejectSelectedAccount() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) RunUiAction("Отклонение аккаунта", () => { ClientLogService.Instance.Info($"admin.account.reject.requested accountId={SelectedPendingAccountId}"); _api.RejectAccount(SelectedPendingAccountId); RefreshPeopleSection(); }); }
    private void BlockSelectedAccount() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) RunUiAction("Выполнить действие", () => { ClientLogService.Instance.Info($"admin.account.block.requested accountId={SelectedPendingAccountId}"); _api.BlockAccount(SelectedPendingAccountId); RefreshPeopleSection(); }); }
    private void UnblockSelectedAccount() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) RunUiAction("Выполнить действие", () => { ClientLogService.Instance.Info($"admin.account.unblock.requested accountId={SelectedPendingAccountId}"); _api.UnblockAccount(SelectedPendingAccountId); RefreshPeopleSection(); }); }
    private void ResetSelectedPassword()
    {
        var accountId = !string.IsNullOrWhiteSpace(SelectedPendingAccountId) ? SelectedPendingAccountId : SelectedOwnerUserId;
        if (string.IsNullOrWhiteSpace(accountId)) return;
        if (string.IsNullOrWhiteSpace(ResetPasswordText))
        {
            LastErrorMessage = "Нет данных";
            Notify(nameof(LastErrorMessage));
            Notify(nameof(HasConnectionError));
            return;
        }
        RunUiAction("Выполнить действие", () =>
        {
            ClientLogService.Instance.Info($"admin.account.resetPassword.requested accountId={accountId}");
            _api.ResetPassword(accountId, ResetPasswordText);
            ResetPasswordText = string.Empty;
            Notify(nameof(ResetPasswordText));
            Notify(nameof(CanResetSelectedAccountPassword));
            RefreshPeopleSection();
        });
    }

    private void CreateCharacterForOwner()
    {
        if (string.IsNullOrWhiteSpace(SelectedOwnerUserId)) return;
        RunUiAction("Бросок кубиков", () =>
        {
            var payload = new Dictionary<string, object>
            {
                { "ownerUserId", SelectedOwnerUserId },
                { "name", CreateCharacterName },
                { "race", CreateCharacterRace },
                { "backstory", CreateCharacterBackstory }
            };
            ClientLogService.Instance.Info($"character.admin.create.send ownerUserId={SelectedOwnerUserId} name={CreateCharacterName}");
            var response = _api.CreateCharacter(payload);
            ClientLogService.Instance.Info($"character.admin.create.response status={response.Status} message={response.Message}");
            EnsureSuccess(response);
            LoadOwnerCharacters();
            ClientLogService.Instance.Info($"character.admin.create.success ownerUserId={SelectedOwnerUserId} listCount={Characters.Count}");
        });
    }

    private void RollCharacterDice()
    {
        var availabilityReason = GetDiceRollAvailabilityReason();
        if (!string.IsNullOrWhiteSpace(availabilityReason))
        {
            ClientLogService.Instance.Warn($"ui.admin.dice.click.blocked reason={availabilityReason}");
            return;
        }
        RunUiAction("Бросок кубиков", () =>
        {
            var formula = DiceCount + "d" + DiceFaces + (DiceModifier == 0 ? string.Empty : DiceModifier > 0 ? "+" + DiceModifier : DiceModifier.ToString());
            var actorLogin = FirstNonEmpty(LoginText, "unknown");
            ClientLogService.Instance.Info($"dice.roll.actor login={actorLogin} userId=unknown");
            if (string.Equals(DiceModeInput, "Проверочный", StringComparison.OrdinalIgnoreCase))
            {
                ClientLogService.Instance.Info($"dice.roll.test.send actor={actorLogin} formula={formula}");
                var response = _api.DiceRollTest(formula, DiceVisibilityInput, DiceDescriptionInput);
                ClientLogService.Instance.Info($"dice.roll.test.response status={response.Status} message={response.Message}");
                EnsureSuccess(response);
            }
            else
            {
                ClientLogService.Instance.Info($"dice.roll.standard.send actor={actorLogin} formula={formula}");
                var response = _api.DiceRollStandard(formula, DiceVisibilityInput, DiceDescriptionInput);
                ClientLogService.Instance.Info($"dice.roll.standard.response status={response.Status} message={response.Message}");
                EnsureSuccess(response);
            }

            var testState = _api.DiceTestGetCurrent();
            ClientLogService.Instance.Info($"dice.test.getCurrent.status={testState.Status}");
            LoadPendingRequests();
            LoadRequestHistory();
        });
    }

    private string GetDiceRollAvailabilityReason()
    {
        if (!ArePrivilegedSectionsEnabled) return "Нет данных";
        if (IsBusy) return "Нет данных";
        if (DiceCount < 1) return "Нет данных";
        if (DiceFaces < 2) return "Нет данных";
        return string.Empty;
    }

    private void TraceDiceAvailability()
    {
        var reason = GetDiceRollAvailabilityReason();
        if (string.Equals(reason, _lastDiceAvailabilityReason, StringComparison.Ordinal))
        {
            return;
        }

        _lastDiceAvailabilityReason = reason;
        var state = string.IsNullOrWhiteSpace(reason) ? "enabled" : "disabled";
        ClientLogService.Instance.Info("dice.actor.mode=account");
        ClientLogService.Instance.Info($"ui.admin.dice.button state={state} reason={FirstNonEmpty(reason, "ready")}");
    }

    private void RefreshOverviewActivity()
    {
        OverviewActivityRows.Clear();
        OverviewActivityRows.Add(HasConnectionError ? $"Ошибка: {LastErrorMessage}" : LastStatusMessage);
        if (PendingRequests.Count > 0) OverviewActivityRows.Add($"Активность пока отсутствует.");
        if (PendingAccounts.Count > 0) OverviewActivityRows.Add($" : {PendingAccounts[0].Name}");
        if (DiceFeedRows.Count > 0) OverviewActivityRows.Add($"Бросок: {DiceFeedRows[0]}");
        if (ChatRows.Count > 0) OverviewActivityRows.Add($"Чат: {ChatRows[0]}");
        if (DiagnosticsItems.Count > 0) OverviewActivityRows.Add($"Диагностика: {DiagnosticsItems[0].Name} / {DiagnosticsItems[0].Extra}");
        if (OverviewActivityRows.Count == 1 && string.IsNullOrWhiteSpace(OverviewActivityRows[0]))
        {
            OverviewActivityRows[0] = "Активность пока отсутствует.";
        }
    }

    public void Shutdown()
    {
        SaveConnectionSettings();
        SaveWorkspaceLayout();
        ClientLogService.Instance.Info("Logout / shutdown requested from Admin client");
        _client.Disconnect();
    }

    private void NotifyAllEditor()
    {
        Notify(nameof(EditName)); Notify(nameof(EditRace)); Notify(nameof(EditHeight)); Notify(nameof(EditAge)); Notify(nameof(EditDescription)); Notify(nameof(EditBackstory)); Notify(nameof(BiographySaveStatus));
        Notify(nameof(Health)); Notify(nameof(PhysicalArmor)); Notify(nameof(MagicalArmor)); Notify(nameof(Morale)); Notify(nameof(Strength)); Notify(nameof(Dexterity)); Notify(nameof(Endurance)); Notify(nameof(Wisdom)); Notify(nameof(Intellect)); Notify(nameof(Charisma));
        Notify(nameof(Iron)); Notify(nameof(Bronze)); Notify(nameof(Silver)); Notify(nameof(Gold)); Notify(nameof(Platinum)); Notify(nameof(Orichalcum)); Notify(nameof(Adamant)); Notify(nameof(Sovereign)); Notify(nameof(ExperienceCoins)); Notify(nameof(CharacterMoneySaveStatus));
        NotifyInventoryEditor();
        NotifyHoldingEditor();
        NotifyReputationEditor();
        NotifyCompanionEditor();
        Notify(nameof(OwnershipOwnerUserId));
        Notify(nameof(OwnershipControlledByUserId));
        Notify(nameof(OwnershipReason));
        Notify(nameof(OwnershipGroupId));
        Notify(nameof(OwnershipGroupName));
        Notify(nameof(OwnershipKind));
        Notify(nameof(OwnershipStatus));
        Notify(nameof(OwnershipIsActive));
        Notify(nameof(OwnershipIsArchived));
        Notify(nameof(OwnershipIsPlayerVisible));
        Notify(nameof(OwnershipMessage));
        Notify(nameof(OwnershipSummary));
    }

    private static T ReadJson<T>(string path, T fallback) where T : class
    {
        try
        {
            if (!File.Exists(path)) return fallback;
            using var stream = File.OpenRead(path);
            var serializer = new DataContractJsonSerializer(typeof(T));
            return serializer.ReadObject(stream) as T ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void WriteJson<T>(string path, T value) where T : class
    {
        using var stream = File.Create(path);
        var serializer = new DataContractJsonSerializer(typeof(T));
        serializer.WriteObject(stream, value);
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string MapOwnershipRoleToKind(string role)
    {
        return (role ?? string.Empty).Trim().ToLowerInvariant().Replace("_", string.Empty) switch
        {
            "npc" => CharacterKindIds.Npc,
            "companion" => CharacterKindIds.Companion,
            "temporaryally" => CharacterKindIds.TemporaryAlly,
            "enemy" => CharacterKindIds.Enemy,
            "neutral" => CharacterKindIds.Neutral,
            "custom" => CharacterKindIds.Custom,
            _ => CharacterKindIds.PlayerCharacter
        };
    }

    private static string MapKindToOwnershipRole(string kind)
    {
        return (kind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            CharacterKindIds.Npc => CharacterOwnershipRoleIds.NPC,
            CharacterKindIds.Companion => CharacterOwnershipRoleIds.Companion,
            CharacterKindIds.TemporaryAlly => CharacterOwnershipRoleIds.TemporaryAlly,
            CharacterKindIds.Enemy => CharacterOwnershipRoleIds.Enemy,
            CharacterKindIds.Neutral => CharacterOwnershipRoleIds.Neutral,
            CharacterKindIds.Custom => CharacterOwnershipRoleIds.Custom,
            _ => CharacterOwnershipRoleIds.PlayerCharacter
        };
    }

    private static string MapKindToGroupRole(string kind)
    {
        return (kind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            CharacterKindIds.Npc => CharacterGroupCharacterRoleIds.NPC,
            CharacterKindIds.Companion => CharacterGroupCharacterRoleIds.Companion,
            CharacterKindIds.TemporaryAlly => CharacterGroupCharacterRoleIds.TemporaryAlly,
            CharacterKindIds.Enemy => CharacterGroupCharacterRoleIds.Enemy,
            CharacterKindIds.Custom => CharacterGroupCharacterRoleIds.Custom,
            _ => CharacterGroupCharacterRoleIds.PlayerCharacter
        };
    }

    private static IList ToList(object value) => value as IList ?? new ArrayList();
    private Dictionary<string, object>? FirstCharacterHubCard(Dictionary<string, object> payload)
    {
        if (!payload.TryGetValue("characters", out var rawCharacters)) return null;
        foreach (var item in ToList(rawCharacters))
        {
            var map = AsMap(item, CommandNames.CharacterAdminHubGet);
            if (map != null && string.Equals(S(map, "characterId"), SelectedCharacterId, StringComparison.OrdinalIgnoreCase))
                return map;
        }

        foreach (var item in ToList(rawCharacters))
        {
            var map = AsMap(item, CommandNames.CharacterAdminHubGet);
            if (map != null) return map;
        }

        return null;
    }

    private static Dictionary<string, object> BuildCharacterCardPayloadFromHub(Dictionary<string, object> hub)
    {
        var hubStats = MapValue(hub.ContainsKey("stats") ? hub["stats"] : null);
        var stats = new Dictionary<string, object>
        {
            ["health"] = ReadInt(hubStats, "health", ParseFirstInt(S(hub, "health"))),
            ["physicalArmor"] = ReadInt(hubStats, "physicalArmor", ParseFirstInt(S(hub, "armor"))),
            ["magicalArmor"] = ReadInt(hubStats, "magicalArmor"),
            ["morale"] = ReadInt(hubStats, "morale"),
            ["strength"] = ReadInt(hubStats, "strength"),
            ["dexterity"] = ReadInt(hubStats, "dexterity"),
            ["endurance"] = ReadInt(hubStats, "endurance"),
            ["wisdom"] = ReadInt(hubStats, "wisdom"),
            ["intellect"] = ReadInt(hubStats, "intellect"),
            ["charisma"] = ReadInt(hubStats, "charisma")
        };

        return new Dictionary<string, object>
        {
            ["characterId"] = S(hub, "characterId"),
            ["name"] = S(hub, "name"),
            ["race"] = S(hub, "race"),
            ["description"] = S(hub, "description"),
            ["backstory"] = S(hub, "backstory"),
            ["xpCoins"] = S(hub, "xpCoins"),
            ["stats"] = stats,
            ["money"] = hub.ContainsKey("money") ? hub["money"] : new Dictionary<string, object>(),
            ["inventory"] = hub.ContainsKey("inventory") ? hub["inventory"] : new ArrayList(),
            ["holdings"] = hub.ContainsKey("holdings") ? hub["holdings"] : new ArrayList(),
            ["reputation"] = hub.ContainsKey("reputation") ? hub["reputation"] : new ArrayList(),
            ["companions"] = hub.ContainsKey("companions") ? hub["companions"] : new ArrayList()
        };
    }

    private static Dictionary<string, object> MapValue(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                result[key] = entry.Value;
            }
            return result;
        }
        if (value is object[] objectArray && TryConvertObjectArrayToMap(objectArray, out var objectArrayMap))
        {
            return objectArrayMap;
        }
        if (value is IEnumerable enumerable && value is not string && TryConvertEnumerableToMap(enumerable, out var enumerableMap))
        {
            return enumerableMap;
        }
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static int ReadInt(Dictionary<string, object> map, string key, int fallback = 0)
    {
        if (!map.ContainsKey(key) || map[key] == null) return fallback;
        return int.TryParse(Convert.ToString(map[key]), out var value) ? value : fallback;
    }

    private static bool ReadBool(Dictionary<string, object> map, string key, bool fallback = false)
    {
        if (!map.ContainsKey(key) || map[key] == null) return fallback;
        return bool.TryParse(Convert.ToString(map[key]), out var value) ? value : fallback;
    }

    private static long ReadLong(Dictionary<string, object> map, string key, long fallback = 0)
    {
        if (!map.ContainsKey(key) || map[key] == null) return fallback;
        return long.TryParse(Convert.ToString(map[key]), out var value) ? value : fallback;
    }

    private static long? TryReadNullableLong(Dictionary<string, object> map, string key)
    {
        if (!map.ContainsKey(key) || map[key] == null) return null;
        return long.TryParse(Convert.ToString(map[key]), out var value) ? value : null;
    }

    private static int ParseFirstInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var digits = new string(value.SkipWhile(ch => !char.IsDigit(ch) && ch != '-').TakeWhile(ch => char.IsDigit(ch) || ch == '-').ToArray());
        return int.TryParse(digits, out var parsed) ? parsed : 0;
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

    private static IList ExtractSkillDefinitionItems(Dictionary<string, object> payload, out string rawCollectionKey)
    {
        foreach (var key in new[] { "items", "definitions", "skills" })
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
        var line = "[CHAT-DIAG][Admin] " + message;
        ClientLogService.Instance.Debug(line);
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

    private Dictionary<string, object>? AsMap(object? value)
    {
        return AsMap(value, "generic");
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

    private ChatMessageRowVm? BuildChatMessageRow(Dictionary<string, object> map)
    {
        var sender = FirstNonEmpty(S(map, "senderDisplayName"), S(map, "senderUserId"), "Без названия");
        var text = FirstNonEmpty(S(map, "text"), S(map, "message"), S(map, "body"));
        var type = FirstNonEmpty(S(map, "type"), "Public");
        var createdRaw = FirstNonEmpty(S(map, "createdUtc"), S(map, "createdAt"), S(map, "at"));
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

    private void MergeDiceIntoChatFeed()
    {
        MergedSessionFeedRows.Clear();
        var timeline = new List<ChatMessageRowVm>();
        timeline.AddRange(ChatMessageRows);
        timeline.AddRange(DiceMessageRows.Where(row => !IsPlaceholderText(row.Text)));
        var sorted = timeline
            .OrderBy(row => row.SortTicks == 0 ? long.MaxValue : row.SortTicks)
            .ThenBy(row => row.Timestamp, StringComparer.Ordinal)
            .ToList();
        foreach (var row in sorted)
            MergedSessionFeedRows.Add(row);

        var merged = DiceMessageRows.Count(row => !IsPlaceholderText(row.Text));
        ClientLogService.Instance.Info($"gameFeed diceMerged={merged}");
        ClientLogService.Instance.Info($"chat.window.timeline mergedCount={MergedSessionFeedRows.Count}");
        var first = MergedSessionFeedRows.Count > 0 ? $"{MergedSessionFeedRows[0].Sender}:{MergedSessionFeedRows[0].Timestamp}" : "<empty>";
        var last = MergedSessionFeedRows.Count > 0 ? $"{MergedSessionFeedRows[MergedSessionFeedRows.Count - 1].Sender}:{MergedSessionFeedRows[MergedSessionFeedRows.Count - 1].Timestamp}" : "<empty>";
        ClientLogService.Instance.Debug($"merged.timeline first={first}");
        ClientLogService.Instance.Debug($"merged.timeline last={last}");
        ClientLogService.Instance.Info("chat.window.timeline sorted=true");
        ClientLogService.Instance.Debug("merged.timeline sorted=true");
    }

    private void RefreshDiceFeedForChat()
    {
        DiceFeedRows.Clear();
        DiceMessageRows.Clear();
        var feed = _api.DiceVisibleFeed();
        if (feed.Status != ResponseStatus.Ok || !feed.Payload.ContainsKey("items")) return;

        var firstDiceTimestampRaw = string.Empty;
        var firstDiceTimestampMapped = string.Empty;
        foreach (var obj in ToList(feed.Payload["items"]))
        {
            var map = AsMap(obj);
            if (map == null) continue;
            var total = "?";
            if (map.ContainsKey("result"))
            {
                var result = AsMap(map["result"]);
                if (result != null) total = FirstNonEmpty(S(result, "total"), "?");
            }

            var creator = FirstNonEmpty(S(map, "creatorLogin"), S(map, "creatorUserId"));
            var isTest = string.Equals(S(map, "isTestRoll"), "True", StringComparison.OrdinalIgnoreCase);
            var label = isTest ? "[тест] " : string.Empty;
            var rolls = BuildDiceRollDetails(map, CommandNames.DiceVisibleFeed);
            var comment = BuildDiceCommentSuffix(map);
            var diceText = $"{label}{S(map, "formula")} = {total}{rolls} | {S(map, "visibility")}{comment}";
            var createdRaw = FirstNonEmpty(
                S(map, "createdUtc"),
                S(map, "createdAtUtc"),
                S(map, "requestedUtc"),
                S(map, "resolvedUtc"),
                S(map, "at"));
            var timestampMapped = FormatChatTimestamp(createdRaw);
            DiceFeedRows.Add($"{creator}: {diceText}");
            DiceMessageRows.Add(new ChatMessageRowVm
            {
                Sender = creator,
                Text = diceText,
                Timestamp = timestampMapped,
                IsSystem = true,
                SortTicks = ParseTimelineTicks(createdRaw)
            });
            if (string.IsNullOrWhiteSpace(firstDiceTimestampRaw))
            {
                firstDiceTimestampRaw = createdRaw;
                firstDiceTimestampMapped = timestampMapped;
            }
        }
        if (DiceMessageRows.Count > 0)
        {
            ClientLogService.Instance.Debug($"dice.timeline timestampRaw={firstDiceTimestampRaw}");
            ClientLogService.Instance.Debug($"dice.timeline timestampMapped={firstDiceTimestampMapped}");
        }
    }

    private string BuildDiceRollDetails(Dictionary<string, object> map, string context)
    {
        if (!map.TryGetValue("result", out var rawResult)) return string.Empty;
        var result = AsMap(rawResult, context);
        if (result == null || !result.TryGetValue("rolls", out var rawRolls)) return string.Empty;
        var values = ToList(rawRolls)
            .Cast<object>()
            .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (values.Length == 0) return string.Empty;

        var rolled = string.Join(",", values);
        var modifier = 0;
        if (result.TryGetValue("modifier", out var rawModifier))
            int.TryParse(Convert.ToString(rawModifier, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out modifier);
        if (modifier == 0) return $" ({rolled})";
        return modifier > 0 ? $" ({rolled}+{modifier})" : $" ({rolled}{modifier})";
    }

    private static string BuildDiceCommentSuffix(Dictionary<string, object> map)
    {
        var comment = FirstNonEmpty(
            S(map, "description"),
            S(map, "comment"),
            S(map, "note"),
            S(map, "reason"),
            S(map, "message"));
        return string.IsNullOrWhiteSpace(comment) ? string.Empty : $" | комментарий: {comment}";
    }

    private static bool IsPlaceholderText(string text)
    {
        return string.Equals(text, "Нет данных", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "Не указано", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "Нет данных", StringComparison.OrdinalIgnoreCase);
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

    private static string FormatLastSeen(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return "Не указано";
        var formatted = FormatChatTimestamp(rawValue);
        return string.IsNullOrWhiteSpace(formatted) ? "Не указано" : formatted;
    }

    private static bool IsTruthy(string value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string source, string token)
        => (source ?? string.Empty).IndexOf(token ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;

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

    private static string MapChatTypeToServer(string uiType)
    {
        return uiType switch
        {
            "Скрыто" => "HiddenToAdmins",
            "Только для админов" => "AdminOnly",
            _ => "Public"
        };
    }


    private void RefreshDefinitionRaces()
    {
        ClientLogService.Instance.Info("definitions.races.get requested");
        var response = EnsureSuccess(_api.DefinitionsRacesGetContent(RaceSearchText, true));
        RaceDefinitionRows.Clear();
        var rawItems = ExtractSkillDefinitionItems(response.Payload, out var rawCollectionKey);
        foreach (var item in rawItems)
        {
            var map = AsMap(item, CommandNames.DefinitionsRacesGet);
            if (map == null) continue;
            RaceDefinitionRows.Add(new RowVm { Id = S(map, "code"), Name = FirstNonEmpty(S(map, "displayName"), S(map, "name"), S(map, "code")), State = FirstNonEmpty(S(map, "subtypeCode"), S(map, "hybridCode")), Extra = S(map, "description") });
        }
        ClientLogService.Instance.Info($"definitions.races.get rawCollectionKey={rawCollectionKey}");
        ClientLogService.Instance.Info($"definitions.races.get rawCount={rawItems.Count}");
        ClientLogService.Instance.Info($"definitions.races.get count={RaceDefinitionRows.Count}");
    }

    private void RefreshDefinitionItems()
    {
        ClientLogService.Instance.Info("definitions.items.get requested");
        var response = EnsureSuccess(_api.DefinitionsItemsGetContent(ItemTypeFilter, ItemSearchText, true));
        ItemDefinitionRows.Clear();
        var rawItems = ExtractSkillDefinitionItems(response.Payload, out var rawCollectionKey);
        foreach (var item in rawItems)
        {
            var map = AsMap(item, CommandNames.DefinitionsItemsGet);
            if (map == null) continue;
            ItemDefinitionRows.Add(new RowVm { Id = S(map, "code"), Name = FirstNonEmpty(S(map, "displayName"), S(map, "name"), S(map, "code")), State = S(map, "itemType"), Extra = S(map, "description") });
        }
        ClientLogService.Instance.Info($"definitions.items.get rawCollectionKey={rawCollectionKey}");
        ClientLogService.Instance.Info($"definitions.items.get rawCount={rawItems.Count}");
        ClientLogService.Instance.Info($"definitions.items.get count={ItemDefinitionRows.Count}");
    }

    private void RefreshDefinitionsContentStatus()
    {
        var response = EnsureSuccess(_api.DefinitionsContentStatusGet());
        ContentStatusRows.Clear();
        ContentErrorRows.Clear();
        ContentStatusRows.Add(new RowVm { Id = "loadedAt", Name = "Загружено в", State = S(response.Payload, "loadedAtUtc"), Extra = string.Empty });
        ContentStatusRows.Add(new RowVm { Id = "summary", Name = "Сводка", State = $"success={S(response.Payload, "success")}", Extra = $"files={S(response.Payload, "filesRead")}/{S(response.Payload, "filesFound")}, errors={S(response.Payload, "errorCount")}" });
        if (response.Payload.TryGetValue("errors", out var errs))
        {
            foreach (var e in ToList(errs)) ContentErrorRows.Add(new RowVm { Id = Guid.NewGuid().ToString("N"), Name = "Ошибка", State = Convert.ToString(e) ?? string.Empty, Extra = string.Empty });
        }
    }



    private void RefreshCharacterClasses()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        if (ClassDefinitionRows.Count == 0)
        {
            RefreshDefinitionClasses();
        }

        ClientLogService.Instance.Info($"character.classes.get requested characterId={SelectedCharacterId}");
        var response = EnsureSuccess(_api.CharacterClassesGet(SelectedCharacterId));
        var payloadKeys = string.Join(",", response.Payload.Keys.OrderBy(x => x, StringComparer.Ordinal));
        ClientLogService.Instance.Info($"character.classes.get payload.keys={payloadKeys}");
        IList rawItems;
        string rawCollectionKey;
        if (response.Payload.ContainsKey("classes"))
        {
            rawItems = NormalizePayloadList(response.Payload["classes"], out _);
            rawCollectionKey = "classes";
        }
        else
        {
            rawItems = ExtractSkillDefinitionItems(response.Payload, out rawCollectionKey);
        }
        ClientLogService.Instance.Info($"character.classes.get rawCollectionKey={rawCollectionKey}");
        ClientLogService.Instance.Info($"character.classes.get rawCount={rawItems.Count}");

        CharacterClassRows.Clear();
        var mappedCount = 0;
        foreach (var item in rawItems)
        {
            var map = AsMap(item, CommandNames.CharacterClassesGet);
            if (map == null) continue;
            CharacterClassRows.Add(new RowVm { Id = S(map, "classCode"), Name = S(map, "displayName"), State = $"ветка={S(map, "branchCode")}", Extra = $"уровень={S(map, "level")}" });
            mappedCount++;
        }

        ClientLogService.Instance.Info($"character.classes.get mappedCount={mappedCount}");

        if (string.IsNullOrWhiteSpace(AssignClassCode) && ClassDefinitionRows.Count > 0)
        {
            AssignClassCode = ClassDefinitionRows[0].Id;
            ClientLogService.Instance.Info($"character.class.assign defaultClassCode={AssignClassCode}");
        }
        if (string.IsNullOrWhiteSpace(AssignClassNodeId) && !string.IsNullOrWhiteSpace(SelectedClassNodeId))
        {
            AssignClassNodeId = SelectedClassNodeId;
        }
    }



    private void EnsureSelectedCharacterLockForEdit()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId))
        {
            throw new ArgumentException("Заполните обязательные поля.");
        }

        ClientLogService.Instance.Info($"character.lock.acquire before-edit characterId={SelectedCharacterId}");

        var response = EnsureSuccess(_api.AcquireCharacterLock(SelectedCharacterId));

        LockStateText = response.Message;
        Notify(nameof(LockStateText));

        ClientLogService.Instance.Info(
            $"character.lock.acquire before-edit response status={response.Status} message={response.Message}");

        LoadLocksSummary();
    }
    private void AssignCharacterClass()
    {
        ClientLogService.Instance.Info($"character.class.assign requested characterId={SelectedCharacterId} classCode={AssignClassCode} levelText={AssignClassLevel}");

        if (string.IsNullOrWhiteSpace(SelectedCharacterId))
            throw new ArgumentException("Заполните обязательные поля.");

        if (string.IsNullOrWhiteSpace(AssignClassCode))
            throw new ArgumentException("Заполните обязательные поля.");

        if (!int.TryParse(AssignClassLevel, out var level) || level < 1)
            throw new ArgumentException("Заполните обязательные поля.");

        EnsureSelectedCharacterLockForEdit();

        var nodeId = FirstNonEmpty(AssignClassNodeId, EditClassRequiredNodeId, SelectedClassNodeId);
        var response = EnsureSuccess(_api.CharacterClassAssign(SelectedCharacterId, AssignClassCode, nodeId, level));

        ClientLogService.Instance.Info($"character.class.assign response status={response.Status} message={response.Message}");

        RefreshCharacterClasses();

        StatusMessage = "Действие выполнено.";
    }


    private static IEnumerable<Dictionary<string, object>> PayloadItemsAsMaps(
        Dictionary<string, object> payload,
        string key = "items")
    {
        if (payload == null || !payload.TryGetValue(key, out var raw) || raw == null)
        {
            yield break;
        }

        foreach (var item in FlattenPayloadItems(raw))
        {
            if (item is Dictionary<string, object> direct)
            {
                yield return direct;
                continue;
            }

            if (item is IDictionary<string, object> generic)
            {
                yield return generic.ToDictionary(x => x.Key, x => x.Value);
                continue;
            }

            if (item is IDictionary dictionary)
            {
                var map = new Dictionary<string, object>();

                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key == null)
                    {
                        continue;
                    }

                    var mapKey = Convert.ToString(entry.Key) ?? string.Empty;
                    map[mapKey] = entry.Value;
                }

                yield return map;
            }
        }
    }

    private static IEnumerable<object> FlattenPayloadItems(object raw)
    {
        if (raw == null)
        {
            yield break;
        }

        if (raw is string)
        {
            yield return raw;
            yield break;
        }

        if (raw is IDictionary)
        {
            yield return raw;
            yield break;
        }

        if (raw is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item == null)
                {
                    continue;
                }

                if (!(item is string) && !(item is IDictionary) && item is IEnumerable)
                {
                    foreach (var nestedItem in FlattenPayloadItems(item))
                    {
                        yield return nestedItem;
                    }
                }
                else
                {
                    yield return item;
                }
            }

            yield break;
        }

        yield return raw;
    }
    private static string S(Dictionary<string, object> map, string key) => map.ContainsKey(key) && map[key] != null ? Convert.ToString(map[key]) ?? string.Empty : string.Empty;
    private static string FirstNonEmptyAdmin(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
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

    internal void ChatRefreshFromSync() => ChatRefresh();
    internal void RefreshDiceFromSync() => RefreshDiceFeedForChat();
    internal void SetDefinitionsDirty(long revision) { _definitionsDirty = true; ClientLogService.Instance.Warn($"sync.definitions.dirty revision={revision}"); }
}

public static class SyncFeatureFlags
{
    public const bool UsePassiveSyncPoller = false;
    public const bool UseEventDispatcher = false;
}
public interface IClientSyncEventDispatcher { System.Threading.Tasks.Task DispatchAsync(ClientSyncEvent evt); }
public sealed class ClientSyncEventDispatcher : IClientSyncEventDispatcher
{
    private readonly AdminMainViewModel _vm; private bool _chatRefreshInProgress;
    public ClientSyncEventDispatcher(AdminMainViewModel vm){_vm=vm;}
    public System.Threading.Tasks.Task DispatchAsync(ClientSyncEvent evt){ ClientLogService.Instance.Info($"sync.dispatch.start eventId={evt.EventId} revision={evt.Revision} type={evt.Type}"); switch(evt.Type){ case "chat.message.created": if(_chatRefreshInProgress){ClientLogService.Instance.Warn($"sync.dispatch.deferred eventId={evt.EventId} type={evt.Type} reason=chat_refresh_in_progress"); break;} _chatRefreshInProgress=true; _vm.ChatRefreshFromSync(); _chatRefreshInProgress=false; ClientLogService.Instance.Info($"sync.dispatch.done eventId={evt.EventId} type={evt.Type} action=chat.refresh"); break; case "dice.roll.created": _vm.RefreshDiceFromSync(); ClientLogService.Instance.Info($"sync.dispatch.done eventId={evt.EventId} type={evt.Type} action=dice.refresh"); break; case "fate.settings.updated": ClientLogService.Instance.Warn($"sync.dispatch.deferred eventId={evt.EventId} type={evt.Type} reason=fate_refresh_todo"); break; case "definitions.updated": _vm.SetDefinitionsDirty(evt.Revision); ClientLogService.Instance.Info($"sync.dispatch.done eventId={evt.EventId} type={evt.Type} action=definitions.dirty"); break; default: ClientLogService.Instance.Warn($"sync.event.unhandled type={evt.Type} scope={evt.Scope} revision={evt.Revision}"); break;} return System.Threading.Tasks.Task.CompletedTask; }
}
public sealed class ClientSyncEvent { public string EventId=""; public long Revision; public string Type=""; public string Scope=""; public static ClientSyncEvent FromMap(IDictionary<string,object>? map){ map ??= new Dictionary<string,object>(); return new ClientSyncEvent{ EventId=map.ContainsKey("eventId")?Convert.ToString(map["eventId"])??"":"", Revision=map.ContainsKey("revision")?Convert.ToInt64(map["revision"]):0, Type=map.ContainsKey("type")?Convert.ToString(map["type"])??"":"", Scope=map.ContainsKey("scope")?Convert.ToString(map["scope"])??"":""}; } }

