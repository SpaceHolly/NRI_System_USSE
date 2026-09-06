using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Nri.AdminClient.ViewModels;

public sealed class RequirementExpressionEditorNodeVm0219 : ViewModelBase
{
    private string _kind = "all_of";
    private string _leafType = "development_node";
    private string _targetId = string.Empty;
    private string _publicLabel = string.Empty;
    private int _minimumValue = 1;
    private int _requiredCount = 1;

    public RequirementExpressionEditorNodeVm0219(RequirementExpressionEditorNodeVm0219? parent = null)
    {
        Parent = parent;
        AddConditionCommand = new RelayCommand(AddCondition, () => !IsLeaf);
        AddGroupCommand = new RelayCommand(AddGroup, () => !IsLeaf);
        RemoveCommand = new RelayCommand(Remove, () => Parent != null);
    }

    public RequirementExpressionEditorNodeVm0219? Parent { get; }
    public ObservableCollection<RequirementExpressionEditorNodeVm0219> Children { get; } = new();
    public ICommand AddConditionCommand { get; }
    public ICommand AddGroupCommand { get; }
    public ICommand RemoveCommand { get; }

    public string Kind
    {
        get => _kind;
        set
        {
            _kind = string.IsNullOrWhiteSpace(value) ? "all_of" : value;
            Notify(); Notify(nameof(IsLeaf)); Notify(nameof(IsGroup)); Notify(nameof(IsAtLeast)); Notify(nameof(OperatorLabel)); Notify(nameof(PreviewLine));
            ((RelayCommand)AddConditionCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddGroupCommand).RaiseCanExecuteChanged();
        }
    }

    public string LeafType { get => _leafType; set { _leafType = value ?? string.Empty; Notify(); Notify(nameof(PreviewLine)); } }
    public string TargetId { get => _targetId; set { _targetId = value ?? string.Empty; Notify(); Notify(nameof(PreviewLine)); } }
    public string PublicLabel { get => _publicLabel; set { _publicLabel = value ?? string.Empty; Notify(); Notify(nameof(PreviewLine)); } }
    public int MinimumValue { get => _minimumValue; set { _minimumValue = Math.Max(0, value); Notify(); Notify(nameof(PreviewLine)); } }
    public int RequiredCount { get => _requiredCount; set { _requiredCount = Math.Max(1, value); Notify(); Notify(nameof(OperatorLabel)); Notify(nameof(PreviewLine)); } }
    public bool IsLeaf => string.Equals(Kind, "leaf", StringComparison.OrdinalIgnoreCase);
    public bool IsGroup => !IsLeaf;
    public bool IsAtLeast => string.Equals(Kind, "at_least", StringComparison.OrdinalIgnoreCase);
    public string OperatorLabel => Kind == "all_of" ? "Требуется всё" : Kind == "any_of" ? "Требуется одно из" : Kind == "at_least" ? $"Нужно выполнить {RequiredCount} условий" : "Условие";
    public string PreviewLine => IsLeaf ? First(PublicLabel, "Выберите условие") + ThresholdSuffix() : OperatorLabel;

    public Dictionary<string, object>? ToPayload()
    {
        if (IsLeaf)
        {
            if (string.IsNullOrWhiteSpace(LeafType) || string.IsNullOrWhiteSpace(TargetId)) return null;
            return new Dictionary<string, object>
            {
                ["kind"] = "leaf", ["leafType"] = LeafType, ["targetId"] = TargetId,
                ["minimumValue"] = MinimumValue, ["publicLabel"] = First(PublicLabel, TargetId), ["gmLabel"] = First(PublicLabel, TargetId), ["isHidden"] = false,
                ["children"] = Array.Empty<object>()
            };
        }

        var children = Children.Select(x => x.ToPayload()).Where(x => x != null).Cast<object>().ToArray();
        if (children.Length == 0) return null;
        return new Dictionary<string, object>
        {
            ["kind"] = Kind, ["requiredCount"] = IsAtLeast ? Math.Min(Math.Max(1, RequiredCount), children.Length) : 0,
            ["children"] = children
        };
    }

    public static RequirementExpressionEditorNodeVm0219 FromPayload(object? value, RequirementExpressionEditorNodeVm0219? parent = null)
    {
        var map = value as IDictionary<string, object>;
        var node = new RequirementExpressionEditorNodeVm0219(parent);
        if (map == null) return node;
        node.Kind = Text(map, "kind", "all_of");
        node.LeafType = Text(map, "leafType", "development_node");
        node.TargetId = Text(map, "targetId");
        node.PublicLabel = Text(map, "publicLabel");
        node.MinimumValue = Number(map, "minimumValue", 1);
        node.RequiredCount = Number(map, "requiredCount", 1);
        if (map.TryGetValue("children", out var children) && children is IEnumerable sequence && children is not string)
            foreach (var child in sequence.Cast<object>()) node.Children.Add(FromPayload(child, node));
        return node;
    }

    private void AddCondition() => Children.Add(new RequirementExpressionEditorNodeVm0219(this) { Kind = "leaf", PublicLabel = "Новое условие" });
    private void AddGroup() => Children.Add(new RequirementExpressionEditorNodeVm0219(this) { Kind = "any_of" });
    private void Remove() => Parent?.Children.Remove(this);
    private string ThresholdSuffix() => MinimumValue > 1 && (LeafType == "skill_rank" || LeafType == "mastery_band" || LeafType == "attribute" || LeafType == "subattribute") ? $" ≥ {MinimumValue}" : string.Empty;
    private static string Text(IDictionary<string, object> map, string key, string fallback = "") => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? fallback : fallback;
    private static int Number(IDictionary<string, object> map, string key, int fallback) => map.TryGetValue(key, out var value) && int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}
