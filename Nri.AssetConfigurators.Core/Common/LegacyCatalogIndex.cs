using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Nri.AssetConfigurators.Core.Common;

public sealed class LegacyCatalogIndex
{
    private readonly IReadOnlyDictionary<string, CatalogOption> _optionsByKey;
    private readonly IReadOnlyDictionary<string, ComponentDefinition> _componentsByKey;

    public LegacyCatalogIndex(
        IEnumerable<CatalogOption> options,
        IEnumerable<ComponentDefinition> components)
    {
        var optionList = options.ToList();
        var componentList = components.ToList();

        Options = new ReadOnlyCollection<CatalogOption>(optionList);
        Components = new ReadOnlyCollection<ComponentDefinition>(componentList);
        _optionsByKey = new ReadOnlyDictionary<string, CatalogOption>(
            optionList.ToDictionary(item => item.Key, StringComparer.Ordinal));
        _componentsByKey = new ReadOnlyDictionary<string, ComponentDefinition>(
            componentList.ToDictionary(item => item.Key, StringComparer.Ordinal));
    }

    public IReadOnlyList<CatalogOption> Options { get; }
    public IReadOnlyList<ComponentDefinition> Components { get; }

    public CatalogOption RequireOption(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !_optionsByKey.TryGetValue(key, out var option))
            throw new KeyNotFoundException("Не найден вариант классического каталога: " + key);

        return option;
    }

    public ComponentDefinition RequireComponent(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !_componentsByKey.TryGetValue(key, out var component))
            throw new KeyNotFoundException("Не найден компонент классического каталога: " + key);

        return component;
    }

    public string DisplayName(string key)
    {
        if (_optionsByKey.TryGetValue(key ?? string.Empty, out var option))
            return option.DisplayName;
        if (_componentsByKey.TryGetValue(key ?? string.Empty, out var component))
            return component.DisplayName;

        return string.Empty;
    }

    public CatalogOption RequireOptionByDisplayName(string displayName, string category = "")
    {
        var option = Options.FirstOrDefault(item =>
            string.Equals(item.DisplayName, displayName, StringComparison.Ordinal) &&
            (string.IsNullOrEmpty(category) ||
             string.Equals(item.Category, category, StringComparison.Ordinal)));
        if (option == null)
            throw new KeyNotFoundException("Не найден вариант классического каталога: " + displayName);
        return option;
    }

    public ComponentDefinition RequireComponentByDisplayName(
        string displayName,
        AssetComponentCategory? category = null)
    {
        var component = Components.FirstOrDefault(item =>
            string.Equals(item.DisplayName, displayName, StringComparison.Ordinal) &&
            (!category.HasValue || item.Category == category.Value));
        if (component == null)
            throw new KeyNotFoundException("Не найден компонент классического каталога: " + displayName);
        return component;
    }
}
