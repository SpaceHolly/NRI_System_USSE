using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminDefinitionsBrowserViewModel : ViewModelBase
{
    private static readonly DataContractJsonSerializerSettings JsonSettings = new DataContractJsonSerializerSettings
    {
        UseSimpleDictionaryFormat = true,
        KnownTypes = new[]
        {
            typeof(object[]),
            typeof(string[]),
            typeof(int[]),
            typeof(long[]),
            typeof(double[]),
            typeof(bool[]),
            typeof(Dictionary<string, object>),
            typeof(Dictionary<string, string>),
            typeof(Dictionary<string, object[]>)
        }
    };
    private readonly CommandApi _api;
    private string _selectedSource = "Starter pack dry-run";
    private string _packPath;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private string _warningMessage = string.Empty;
    private DateTime _lastLoadedAtUtc;
    private DefinitionCategoryUiItem? _selectedCategoryItem;
    private DefinitionListUiItem? _selectedDefinitionItem;
    private DefinitionDetailsUiItem? _selectedDefinition;
    private int _totalDefinitionCount;
    private int _dryRunErrorCount;
    private int _dryRunWarningCount;
    private int _crossReferenceErrorCount;
    private int _crossReferenceWarningCount;
    private string _packId = "fantasy_nri_default_starter";

    public AdminDefinitionsBrowserViewModel(CommandApi api)
    {
        _api = api;
        _packPath = ResolveDefaultPackPath();
        SourceOptions.Add("Starter pack dry-run");
        SourceOptions.Add("Imported definitions (недоступно)");

        LoadFromDryRunPackCommand = new RelayCommand(LoadFromDryRunPack);
        LoadFromServerDefinitionsCommand = new RelayCommand(() => WarningMessage = "Mongo definitions read endpoint пока недоступен; используйте Starter pack dry-run.");
        RefreshCommand = new RelayCommand(LoadFromDryRunPack);
        SearchCommand = new RelayCommand(ApplyFilters);
        ClearSearchCommand = new RelayCommand(() => { SearchText = string.Empty; ApplyFilters(); });
        RunPackDryRunCommand = new RelayCommand(RunPackDryRunOnly);

        InitializeCategories();
    }

    public ObservableCollection<string> SourceOptions { get; } = new ObservableCollection<string>();
    public ObservableCollection<DefinitionCategoryUiItem> Categories { get; } = new ObservableCollection<DefinitionCategoryUiItem>();
    public ObservableCollection<DefinitionListUiItem> Definitions { get; } = new ObservableCollection<DefinitionListUiItem>();
    public ObservableCollection<DefinitionListUiItem> FilteredDefinitions { get; } = new ObservableCollection<DefinitionListUiItem>();
    public ObservableCollection<string> ValidationWarnings { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ValidationErrors { get; } = new ObservableCollection<string>();
    public ObservableCollection<DefinitionFileValidationUiItem> ValidationFiles { get; } = new ObservableCollection<DefinitionFileValidationUiItem>();

    public ICommand LoadFromDryRunPackCommand { get; }
    public ICommand LoadFromServerDefinitionsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand SelectCategoryCommand => SearchCommand;
    public ICommand SelectDefinitionCommand => SearchCommand;
    public ICommand RunPackDryRunCommand { get; }

    public string SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (_selectedSource != value)
            {
                _selectedSource = value;
                Notify();
                if (value != "Starter pack dry-run")
                {
                    WarningMessage = "Mongo definitions read endpoint пока недоступен; browser остаётся read-only.";
                }
            }
        }
    }

    public string PackPath
    {
        get => _packPath;
        set { if (_packPath != value) { _packPath = value; Notify(); } }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (_searchText != value) { _searchText = value; Notify(); ApplyFilters(); } }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { if (_isLoading != value) { _isLoading = value; Notify(); Notify(nameof(CanRefresh)); } }
    }

    public bool CanRefresh => !IsLoading;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } }
    }

    public string WarningMessage
    {
        get => _warningMessage;
        private set { if (_warningMessage != value) { _warningMessage = value; Notify(); Notify(nameof(HasWarning)); } }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);

    public DateTime LastLoadedAtUtc
    {
        get => _lastLoadedAtUtc;
        private set { if (_lastLoadedAtUtc != value) { _lastLoadedAtUtc = value; Notify(); Notify(nameof(LastLoadedText)); } }
    }

    public string LastLoadedText => LastLoadedAtUtc == default ? "ещё не загружалось" : LastLoadedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public DefinitionCategoryUiItem? SelectedCategoryItem
    {
        get => _selectedCategoryItem;
        set
        {
            if (_selectedCategoryItem != value)
            {
                if (_selectedCategoryItem != null) _selectedCategoryItem.IsSelected = false;
                _selectedCategoryItem = value;
                if (_selectedCategoryItem != null) _selectedCategoryItem.IsSelected = true;
                Notify();
                Notify(nameof(SelectedCategoryTitle));
                ApplyFilters();
            }
        }
    }

    public string SelectedCategoryTitle => SelectedCategoryItem?.Title ?? "Все категории";

    public DefinitionListUiItem? SelectedDefinitionItem
    {
        get => _selectedDefinitionItem;
        set
        {
            if (_selectedDefinitionItem != value)
            {
                _selectedDefinitionItem = value;
                Notify();
                SelectedDefinition = value == null ? null : DefinitionDetailsUiItem.From(value);
                ClientLogService.Instance.Debug($"definitions.ui.definition.selected id={value?.Id ?? string.Empty}");
            }
        }
    }

    public DefinitionDetailsUiItem? SelectedDefinition
    {
        get => _selectedDefinition;
        private set { if (_selectedDefinition != value) { _selectedDefinition = value; Notify(); Notify(nameof(HasSelectedDefinition)); } }
    }

    public bool HasSelectedDefinition => SelectedDefinition != null;
    public int TotalDefinitionCount { get => _totalDefinitionCount; private set { if (_totalDefinitionCount != value) { _totalDefinitionCount = value; Notify(); Notify(nameof(ValidationSummary)); } } }
    public int DryRunErrorCount { get => _dryRunErrorCount; private set { if (_dryRunErrorCount != value) { _dryRunErrorCount = value; Notify(); Notify(nameof(ValidationSummary)); } } }
    public int DryRunWarningCount { get => _dryRunWarningCount; private set { if (_dryRunWarningCount != value) { _dryRunWarningCount = value; Notify(); Notify(nameof(ValidationSummary)); } } }
    public int CrossReferenceErrorCount { get => _crossReferenceErrorCount; private set { if (_crossReferenceErrorCount != value) { _crossReferenceErrorCount = value; Notify(); Notify(nameof(ValidationSummary)); } } }
    public int CrossReferenceWarningCount { get => _crossReferenceWarningCount; private set { if (_crossReferenceWarningCount != value) { _crossReferenceWarningCount = value; Notify(); Notify(nameof(ValidationSummary)); } } }
    public string PackId { get => _packId; private set { if (_packId != value) { _packId = value; Notify(); } } }
    public string ValidationSummary => $"Записей: {TotalDefinitionCount}; ошибок: {DryRunErrorCount}; предупреждений: {DryRunWarningCount}; ошибок связей: {CrossReferenceErrorCount}; предупреждений связей: {CrossReferenceWarningCount}";
    public string FilterSummary => $"Показано {FilteredDefinitions.Count} из {Definitions.Count}";

    public void LoadFromDryRunPack()
    {
        if (IsLoading) return;
        ClientLogService.Instance.Info("definitions.ui.load.start source=starter_pack_dry_run");
        IsLoading = true;
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        try
        {
            RunPackDryRun();
            LoadDefinitionsFromPackFiles();
            LastLoadedAtUtc = DateTime.UtcNow;
            ClientLogService.Instance.Info($"definitions.ui.load.done count={Definitions.Count}");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"definitions.ui.load.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RunPackDryRunOnly()
    {
        if (IsLoading) return;
        ClientLogService.Instance.Info("definitions.ui.dry_run.start source=starter_pack");
        IsLoading = true;
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        try
        {
            RunPackDryRun();
            LastLoadedAtUtc = DateTime.UtcNow;
            ClientLogService.Instance.Info("definitions.ui.dry_run.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"definitions.ui.dry_run.error message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RunPackDryRun()
    {
        ValidationWarnings.Clear();
        ValidationErrors.Clear();
        ValidationFiles.Clear();
        DryRunErrorCount = 0;
        DryRunWarningCount = 0;
        CrossReferenceErrorCount = 0;
        CrossReferenceWarningCount = 0;

        try
        {
            var response = _api.DefinitionsPackDryRun(PackPath);
            if (response.Status != Nri.Shared.Contracts.ResponseStatus.Ok)
            {
                WarningMessage = $"Dry-run endpoint недоступен или вернул ошибку: {response.Message}";
                return;
            }

            var payload = response.Payload ?? new Dictionary<string, object>();
            PackId = Str(payload, "packId", PackId);
            TotalDefinitionCount = Int(payload, "loadedDefinitions");
            AddMessages(ValidationErrors, Get(payload, "errors"));
            AddMessages(ValidationWarnings, Get(payload, "warnings"));
            AddMessages(ValidationErrors, Get(payload, "crossReferenceErrors"), "cross-ref: ");
            AddMessages(ValidationWarnings, Get(payload, "crossReferenceWarnings"), "cross-ref: ");
            DryRunErrorCount = AsList(Get(payload, "errors")).Count;
            DryRunWarningCount = AsList(Get(payload, "warnings")).Count;
            CrossReferenceErrorCount = AsList(Get(payload, "crossReferenceErrors")).Count;
            CrossReferenceWarningCount = AsList(Get(payload, "crossReferenceWarnings")).Count;
            foreach (var item in AsDictionaries(Get(payload, "files")))
            {
                ValidationFiles.Add(new DefinitionFileValidationUiItem
                {
                    Category = Str(item, "category"),
                    Path = Str(item, "path"),
                    DefinitionCount = Int(item, "definitionCount"),
                    Errors = AsList(Get(item, "errors")).Count,
                    Warnings = AsList(Get(item, "warnings")).Count
                });
            }
        }
        catch (Exception ex)
        {
            WarningMessage = $"Dry-run endpoint недоступен: {ex.Message}. Локальный pack preview всё равно может быть загружен.";
            ClientLogService.Instance.Warn($"definitions.ui.dry_run.error message={ex.Message}");
        }
    }

    private void LoadDefinitionsFromPackFiles()
    {
        var packDirectory = ResolvePackPath(PackPath);
        if (!Directory.Exists(packDirectory))
        {
            throw new DirectoryNotFoundException($"Папка пакета справочников не найдена: {packDirectory}");
        }

        PackPath = packDirectory;
        Definitions.Clear();
        SelectedDefinitionItem = null;
        var manifestFiles = LoadManifestFiles(packDirectory);
        foreach (var file in manifestFiles)
        {
            var fullPath = Path.Combine(packDirectory, file.Path ?? string.Empty);
            if (!File.Exists(fullPath)) continue;
            var parsed = DeserializeJson<List<DefinitionJsonRecord>>(File.ReadAllText(fullPath)) ?? new List<DefinitionJsonRecord>();
            foreach (var record in parsed)
            {
                var category = FirstNonEmpty(record.Category, file.Category ?? string.Empty);
                var item = new DefinitionListUiItem
                {
                    Id = record.Id ?? string.Empty,
                    Name = FirstNonEmpty(DisplayName(record), record.Name ?? string.Empty, record.Id ?? string.Empty),
                    Category = category,
                    CategoryLabel = CategoryTitle(category),
                    RuleSetIds = JoinList(record.RuleSetIds),
                    Tags = JoinList(record.Tags),
                    Visibility = string.IsNullOrWhiteSpace(record.VisibilityRule) ? "public" : record.VisibilityRule,
                    SchemaVersion = record.SchemaVersion,
                    Archived = record.IsArchived,
                    PublicDescription = record.PublicDescription ?? string.Empty,
                    GMDescription = record.GMDescription ?? string.Empty,
                    SourcePath = file.Path ?? string.Empty,
                    ExtraData = record.ExtraData ?? new Dictionary<string, object>(),
                    ServerOnlyDataPresent = record.ServerOnlyData?.Count > 0
                };
                item.RefreshComputedFields();
                Definitions.Add(item);
            }
        }

        TotalDefinitionCount = Definitions.Count;
        RefreshCategoryCounts();
        ApplyFilters();
    }

    private List<DefinitionPackManifestFileUiItem> LoadManifestFiles(string packDirectory)
    {
        var manifestPath = Path.Combine(packDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return Directory.GetFiles(packDirectory, "*.json")
                .Where(path => !string.Equals(Path.GetFileName(path), "manifest.json", StringComparison.OrdinalIgnoreCase))
                .Select(path => new DefinitionPackManifestFileUiItem { Path = Path.GetFileName(path), Category = Path.GetFileNameWithoutExtension(path) })
                .ToList();
        }

        var manifest = DeserializeJson<DefinitionPackManifestUiItem>(File.ReadAllText(manifestPath)) ?? new DefinitionPackManifestUiItem();
        PackId = string.IsNullOrWhiteSpace(manifest.PackId) ? PackId : manifest.PackId;
        var files = new List<DefinitionPackManifestFileUiItem>();
        foreach (var file in manifest.Files ?? new List<DefinitionPackManifestFileUiItem>())
        {
            files.Add(new DefinitionPackManifestFileUiItem
            {
                Path = file.Path ?? string.Empty,
                Category = file.Category ?? string.Empty
            });
        }

        return files;
    }

    private void ApplyFilters()
    {
        FilteredDefinitions.Clear();
        var category = SelectedCategoryItem?.Id ?? string.Empty;
        var search = SearchText ?? string.Empty;
        foreach (var item in Definitions.Where(item => MatchesCategory(item, category) && MatchesSearch(item, search)).OrderBy(item => item.CategoryLabel).ThenBy(item => item.Name))
        {
            FilteredDefinitions.Add(item);
        }

        if (SelectedDefinitionItem == null || !FilteredDefinitions.Contains(SelectedDefinitionItem))
        {
            SelectedDefinitionItem = FilteredDefinitions.FirstOrDefault();
        }

        Notify(nameof(FilterSummary));
        ClientLogService.Instance.Debug("definitions.ui.search.changed");
    }

    private static bool MatchesCategory(DefinitionListUiItem item, string category)
        => string.IsNullOrWhiteSpace(category) || string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSearch(DefinitionListUiItem item, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        return Contains(item.Id, search)
            || Contains(item.Name, search)
            || Contains(item.Category, search)
            || Contains(item.Tags, search)
            || Contains(item.PublicDescription, search);
    }

    private static bool Contains(string value, string search)
        => (value ?? string.Empty).IndexOf(search ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;

    private void InitializeCategories()
    {
        Categories.Clear();
        foreach (var item in CategoryDefinitions())
        {
            Categories.Add(item);
        }
    }

    private void RefreshCategoryCounts()
    {
        foreach (var category in Categories)
        {
            category.Count = Definitions.Count(item => string.Equals(item.Category, category.Id, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IEnumerable<DefinitionCategoryUiItem> CategoryDefinitions()
    {
        yield return Cat("attribute", "Характеристики", "Core", 10);
        yield return Cat("derived_stat", "Производные параметры", "Core", 20);
        yield return Cat("currency", "Валюты", "Core", 30);
        yield return Cat("skill", "Навыки", "Progression", 40);
        yield return Cat("development_node", "Развитие", "Progression", 50);
        yield return Cat("development_hexagon", "Hexagon развития", "Progression", 60);
        yield return Cat("race", "Расы", "Ancestry", 70);
        yield return Cat("subspecies", "Подвиды", "Ancestry", 80);
        yield return Cat("hybrid", "Гибриды", "Ancestry", 90);
        yield return Cat("hybrid_subtype", "Подтипы гибридов", "Ancestry", 100);
        yield return Cat("race_trait", "Расовые черты", "Ancestry", 110);
        yield return Cat("language", "Языки", "World", 120);
        yield return Cat("continent", "Континенты", "World", 130);
        yield return Cat("country", "Страны", "World", 140);
        yield return Cat("city_state", "Города-государства", "World", 150);
        yield return Cat("region", "Регионы", "World", 160);
        yield return Cat("location_type", "Типы локаций", "World", 170);
        yield return Cat("location", "Локации", "World", 180);
        yield return Cat("item", "Предметы", "Items", 190);
        yield return Cat("weapon", "Оружие", "Items", 200);
        yield return Cat("armor", "Броня", "Items", 210);
        yield return Cat("ammo", "Боеприпасы", "Items", 220);
        yield return Cat("equipment_slot", "Слоты экипировки", "Items", 230);
        yield return Cat("condition", "Состояния", "Combat", 240);
        yield return Cat("condition_group", "Группы состояний", "Combat", 250);
        yield return Cat("faction", "Фракции", "Economy/Factions", 260);
        yield return Cat("organization", "Организации", "Economy/Factions", 270);
        yield return Cat("law", "Законы", "Economy/Factions", 280);
        yield return Cat("restriction", "Ограничения", "Economy/Factions", 290);
        yield return Cat("market_tag", "Рынок", "Economy/Factions", 300);
    }

    private static DefinitionCategoryUiItem Cat(string id, string title, string group, int sortOrder) => new DefinitionCategoryUiItem { Id = id, Title = title, Group = group, SortOrder = sortOrder };
    private static string CategoryTitle(string category) => CategoryDefinitions().FirstOrDefault(item => string.Equals(item.Id, category, StringComparison.OrdinalIgnoreCase))?.Title ?? category;

    private static string ResolveDefaultPackPath()
    {
        var relative = Path.Combine("Nri.Server", "Content", "DefinitionPacks", "fantasy_nri_default_starter");
        foreach (var root in CandidateRoots(AppContext.BaseDirectory).Concat(CandidateRoots(Environment.CurrentDirectory)))
        {
            var candidate = Path.GetFullPath(Path.Combine(root, relative));
            if (Directory.Exists(candidate)) return candidate;
        }

        return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, relative));
    }

    private static string ResolvePackPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return ResolveDefaultPackPath();
        if (Directory.Exists(path)) return Path.GetFullPath(path);
        foreach (var root in CandidateRoots(Environment.CurrentDirectory).Concat(CandidateRoots(AppContext.BaseDirectory)))
        {
            var candidate = Path.GetFullPath(Path.Combine(root, path));
            if (Directory.Exists(candidate)) return candidate;
        }

        return Path.GetFullPath(path);
    }

    private static IEnumerable<string> CandidateRoots(string start)
    {
        var current = new DirectoryInfo(string.IsNullOrWhiteSpace(start) ? Environment.CurrentDirectory : start);
        for (var i = 0; i < 8 && current != null; i++)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static void AddMessages(ObservableCollection<string> target, object? source, string prefix = "")
    {
        foreach (var item in AsList(source))
        {
            var text = Convert.ToString(item, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(text)) target.Add(prefix + text);
        }
    }

    private static object? Get(IDictionary<string, object> map, string key)
    {
        foreach (var pair in map)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        }

        return null;
    }

    private static string Str(IDictionary<string, object> map, string key, string fallback = "") => Convert.ToString(Get(map, key), CultureInfo.InvariantCulture) ?? fallback;
    private static int Int(IDictionary<string, object> map, string key)
    {
        var value = Get(map, key);
        if (value == null) return 0;
        if (value is int i) return i;
        if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return 0;
    }

    private static bool Bool(IDictionary<string, object> map, string key)
    {
        var value = Get(map, key);
        if (value is bool b) return b;
        return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) && parsed;
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    private static string DisplayName(DefinitionJsonRecord record)
    {
        var extra = record.ExtraData;
        if (extra == null) return string.Empty;
        return Str(extra, "displayNameRu");
    }

    private static string JoinList(IEnumerable<string>? value)
    {
        var items = (value ?? Array.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        return items.Count == 0 ? "—" : string.Join(", ", items);
    }

    private static T? DeserializeJson<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        var serializer = new DataContractJsonSerializer(typeof(T), JsonSettings);
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
        {
            var value = serializer.ReadObject(stream);
            return value is T typed ? typed : default;
        }
    }

    private static List<object> AsList(object? value)
    {
        if (value == null) return new List<object>();
        if (value is object[] array) return array.ToList();
        if (value is Array arr) return arr.Cast<object>().ToList();
        if (value is IList list) return list.Cast<object>().ToList();
        return new List<object>();
    }

    private static IEnumerable<Dictionary<string, object>> AsDictionaries(object? value)
    {
        foreach (var item in AsList(value))
        {
            var map = AsDictionary(item);
            if (map != null) yield return map;
        }
    }

    private static Dictionary<string, object>? AsDictionary(object? value)
    {
        if (value is Dictionary<string, object> dictionary) return dictionary;
        if (value is IDictionary<string, object> generic) return new Dictionary<string, object>(generic);
        if (value is IDictionary legacy)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in legacy)
            {
                var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(key)) map[key] = entry.Value ?? string.Empty;
            }

            return map;
        }

        return null;
    }
}

public sealed class DefinitionCategoryUiItem : ViewModelBase
{
    private int _count;
    private bool _isSelected;
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int Count { get => _count; set { if (_count != value) { _count = value; Notify(); Notify(nameof(DisplayTitle)); } } }
    public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; Notify(); } } }
    public string DisplayTitle => $"{Title} ({Count})";
}

public sealed class DefinitionListUiItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategoryLabel { get; set; } = string.Empty;
    public string RuleSetIds { get; set; } = "—";
    public string Tags { get; set; } = "—";
    public string Visibility { get; set; } = "public";
    public int SchemaVersion { get; set; }
    public bool Archived { get; set; }
    public string ArchivedText { get; private set; } = "Нет";
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public bool ServerOnlyDataPresent { get; set; }
    public string ServerOnlyDataText => ServerOnlyDataPresent ? "present: yes" : "present: no";
    public void RefreshComputedFields() => ArchivedText = Archived ? "Да" : "Нет";
}

public sealed class DefinitionDetailsUiItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategoryLabel { get; set; } = string.Empty;
    public string RuleSetIds { get; set; } = "—";
    public string Tags { get; set; } = "—";
    public string Visibility { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string ArchivedText { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string ServerOnlyDataText { get; set; } = "present: no";
    public ObservableCollection<DefinitionKeyValueUiItem> ExtraDataRows { get; } = new ObservableCollection<DefinitionKeyValueUiItem>();
    public ObservableCollection<DefinitionKeyValueUiItem> PreviewRows { get; } = new ObservableCollection<DefinitionKeyValueUiItem>();

    public static DefinitionDetailsUiItem From(DefinitionListUiItem source)
    {
        var details = new DefinitionDetailsUiItem
        {
            Id = source.Id,
            Name = source.Name,
            Category = source.Category,
            CategoryLabel = source.CategoryLabel,
            RuleSetIds = source.RuleSetIds,
            Tags = source.Tags,
            Visibility = source.Visibility,
            SchemaVersion = source.SchemaVersion,
            ArchivedText = source.ArchivedText,
            PublicDescription = string.IsNullOrWhiteSpace(source.PublicDescription) ? "Описание отсутствует." : source.PublicDescription,
            GMDescription = string.IsNullOrWhiteSpace(source.GMDescription) ? "GM description отсутствует." : source.GMDescription,
            SourcePath = source.SourcePath,
            ServerOnlyDataText = source.ServerOnlyDataText
        };

        foreach (var key in PreviewKeysFor(source.Category))
        {
            if (TryGet(source.ExtraData, key, out var value))
            {
                details.PreviewRows.Add(new DefinitionKeyValueUiItem { Key = key, Value = SafeValue(value) });
            }
        }

        foreach (var pair in source.ExtraData.OrderBy(pair => pair.Key))
        {
            details.ExtraDataRows.Add(new DefinitionKeyValueUiItem { Key = pair.Key, Value = SafeValue(pair.Value) });
        }

        if (details.PreviewRows.Count == 0)
        {
            details.PreviewRows.Add(new DefinitionKeyValueUiItem { Key = "preview", Value = "Для этой категории используется общий ExtraData preview." });
        }

        return details;
    }

    private static IEnumerable<string> PreviewKeysFor(string category)
    {
        switch ((category ?? string.Empty).ToLowerInvariant())
        {
            case "weapon":
                return new[] { "weaponType", "handedness", "damageDraft", "accuracyDraft", "linkedSkillIds", "ammoDefinitionIds", "equipmentSlotIds" };
            case "armor":
                return new[] { "armorType", "equipmentSlotIds", "physicalArmorDraft", "magicArmorDraft", "heightFitMode" };
            case "ammo":
                return new[] { "ammoType", "compatibleWeaponIds", "damageModifierDraft", "maxStack" };
            case "race":
                return new[] { "parentRaceId", "traits", "attributeModifiers" };
            case "language":
                return new[] { "continentId", "countryIds", "cityStateIds" };
            case "country":
                return new[] { "primaryLanguageIds", "secondaryLanguageIds", "continentId" };
            case "faction":
            case "organization":
                return new[] { "countryId", "cityStateId", "parentFactionId" };
            case "law":
            case "restriction":
                return new[] { "countryIds", "cityStateIds", "relatedLawIds", "appliesToTags" };
            default:
                return Array.Empty<string>();
        }
    }

    private static bool TryGet(Dictionary<string, object> map, string key, out object value)
    {
        foreach (var pair in map)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static string SafeValue(object? value)
    {
        if (value == null) return "—";
        if (value is object[] array) return Trim(string.Join(", ", array.Select(item => SafeValue(item))));
        if (value is Array arr) return Trim(string.Join(", ", arr.Cast<object>().Select(item => SafeValue(item))));
        if (value is IDictionary dictionary)
        {
            var pairs = dictionary.Keys.Cast<object>().Select(key => $"{key}: {SafeValue(dictionary[key])}");
            return Trim(string.Join("; ", pairs));
        }

        return Trim(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "—");
    }

    private static string Trim(string value) => value.Length <= 500 ? value : value.Substring(0, 500) + "…";
}

public sealed class DefinitionKeyValueUiItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class DefinitionFileValidationUiItem
{
    public string Category { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int DefinitionCount { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
}

[DataContract]
internal sealed class DefinitionPackManifestFileUiItem
{
    [DataMember(Name = "path")]
    public string? Path { get; set; }

    [DataMember(Name = "category")]
    public string? Category { get; set; }
}

[DataContract]
internal sealed class DefinitionPackManifestUiItem
{
    [DataMember(Name = "packId")]
    public string? PackId { get; set; }

    [DataMember(Name = "files")]
    public List<DefinitionPackManifestFileUiItem>? Files { get; set; }
}

[DataContract]
internal sealed class DefinitionJsonRecord
{
    [DataMember(Name = "id")]
    public string? Id { get; set; }

    [DataMember(Name = "category")]
    public string? Category { get; set; }

    [DataMember(Name = "ruleSetIds")]
    public List<string>? RuleSetIds { get; set; }

    [DataMember(Name = "name")]
    public string? Name { get; set; }

    [DataMember(Name = "publicDescription")]
    public string? PublicDescription { get; set; }

    [DataMember(Name = "gmDescription")]
    public string? GMDescription { get; set; }

    [DataMember(Name = "visibilityRule")]
    public string? VisibilityRule { get; set; }

    [DataMember(Name = "tags")]
    public List<string>? Tags { get; set; }

    [DataMember(Name = "schemaVersion")]
    public int SchemaVersion { get; set; }

    [DataMember(Name = "isArchived")]
    public bool IsArchived { get; set; }

    [DataMember(Name = "serverOnlyData")]
    public Dictionary<string, object>? ServerOnlyData { get; set; }

    [DataMember(Name = "extraData")]
    public Dictionary<string, object>? ExtraData { get; set; }
}
