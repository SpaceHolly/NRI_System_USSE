using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed class InitialDevelopmentChoiceVm
{
    public InitialDevelopmentChoiceVm(string id, string displayName)
    {
        Id = id ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public override string ToString() => DisplayName;
}

public partial class PlayerMainViewModel
{
    private bool _initialDevelopmentPending;
    private bool _initialDevelopmentConfirmationOpen;
    private bool _initialDevelopmentUseSingleClass = true;
    private int _initialDevelopmentRevision;
    private string _initialDevelopmentStatus = string.Empty;
    private InitialDevelopmentChoiceVm? _initialDevelopmentFirstClass;
    private InitialDevelopmentChoiceVm? _initialDevelopmentSecondClass;
    private InitialDevelopmentChoiceVm? _initialDevelopmentMagicMethod;
    private InitialDevelopmentChoiceVm? _initialDevelopmentBasicElement;

    public ObservableCollection<InitialDevelopmentChoiceVm> InitialDevelopmentBaseClasses { get; } = new();
    public ObservableCollection<InitialDevelopmentChoiceVm> InitialDevelopmentMagicMethods { get; } = new();
    public ObservableCollection<InitialDevelopmentChoiceVm> InitialDevelopmentBasicElements { get; } = new();

    public ICommand InitialDevelopmentReviewCommand { get; private set; } = null!;
    public ICommand InitialDevelopmentConfirmCommand { get; private set; } = null!;
    public ICommand InitialDevelopmentCancelReviewCommand { get; private set; } = null!;

    public bool IsInitialDevelopmentPending
    {
        get => _initialDevelopmentPending;
        private set { if (_initialDevelopmentPending == value) return; _initialDevelopmentPending = value; Notify(); }
    }

    public bool IsInitialDevelopmentConfirmationOpen
    {
        get => _initialDevelopmentConfirmationOpen;
        private set { if (_initialDevelopmentConfirmationOpen == value) return; _initialDevelopmentConfirmationOpen = value; Notify(); }
    }

    public bool InitialDevelopmentUseSingleClass
    {
        get => _initialDevelopmentUseSingleClass;
        set
        {
            if (_initialDevelopmentUseSingleClass == value) return;
            _initialDevelopmentUseSingleClass = value;
            Notify();
            Notify(nameof(InitialDevelopmentUseTwoClasses));
            Notify(nameof(InitialDevelopmentSummary));
        }
    }

    public bool InitialDevelopmentUseTwoClasses
    {
        get => !_initialDevelopmentUseSingleClass;
        set { if (value) InitialDevelopmentUseSingleClass = false; }
    }

    public InitialDevelopmentChoiceVm? InitialDevelopmentFirstClass
    {
        get => _initialDevelopmentFirstClass;
        set { if (_initialDevelopmentFirstClass == value) return; _initialDevelopmentFirstClass = value; Notify(); Notify(nameof(InitialDevelopmentSummary)); }
    }

    public InitialDevelopmentChoiceVm? InitialDevelopmentSecondClass
    {
        get => _initialDevelopmentSecondClass;
        set { if (_initialDevelopmentSecondClass == value) return; _initialDevelopmentSecondClass = value; Notify(); Notify(nameof(InitialDevelopmentSummary)); }
    }

    public InitialDevelopmentChoiceVm? InitialDevelopmentMagicMethod
    {
        get => _initialDevelopmentMagicMethod;
        set { if (_initialDevelopmentMagicMethod == value) return; _initialDevelopmentMagicMethod = value; Notify(); Notify(nameof(InitialDevelopmentSummary)); }
    }

    public InitialDevelopmentChoiceVm? InitialDevelopmentBasicElement
    {
        get => _initialDevelopmentBasicElement;
        set { if (_initialDevelopmentBasicElement == value) return; _initialDevelopmentBasicElement = value; Notify(); Notify(nameof(InitialDevelopmentSummary)); }
    }

    public string InitialDevelopmentStatus
    {
        get => _initialDevelopmentStatus;
        private set { _initialDevelopmentStatus = value ?? string.Empty; Notify(); }
    }

    public string InitialDevelopmentSummary
    {
        get
        {
            var classes = InitialDevelopmentUseSingleClass
                ? $"{InitialDevelopmentFirstClass?.DisplayName ?? "не выбран"}: ранг 2"
                : $"{InitialDevelopmentFirstClass?.DisplayName ?? "не выбран"}: ранг 1; {InitialDevelopmentSecondClass?.DisplayName ?? "не выбран"}: ранг 1";
            return $"Классы: {classes}. Метод магии: {InitialDevelopmentMagicMethod?.DisplayName ?? "не выбран"}. Стихия: {InitialDevelopmentBasicElement?.DisplayName ?? "не выбрана"}. Монеты опыта не расходуются.";
        }
    }

    private void InitializeInitialDevelopment02112()
    {
        InitialDevelopmentReviewCommand = new RelayCommand(ReviewInitialDevelopment02112);
        InitialDevelopmentConfirmCommand = new RelayCommand(CompleteInitialDevelopment02112);
        InitialDevelopmentCancelReviewCommand = new RelayCommand(() => IsInitialDevelopmentConfirmationOpen = false);
    }

    private void LoadInitialDevelopment02112()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId))
        {
            IsInitialDevelopmentPending = false;
            return;
        }

        var response = _api.InitialDevelopmentGet(SelectedCharacterId);
        if (response.Status != ResponseStatus.Ok)
        {
            IsInitialDevelopmentPending = false;
            InitialDevelopmentStatus = response.Message;
            return;
        }

        var payload = response.Payload ?? new Dictionary<string, object>();
        IsInitialDevelopmentPending = ReadInitialDevelopmentBool02112(payload, "isPending");
        _initialDevelopmentRevision = ReadInitialDevelopmentInt02112(payload, "profileRevision");
        ReplaceInitialDevelopmentOptions02112(InitialDevelopmentBaseClasses, payload, "baseClassOptions");
        ReplaceInitialDevelopmentOptions02112(InitialDevelopmentMagicMethods, payload, "magicMethodOptions");
        ReplaceInitialDevelopmentOptions02112(InitialDevelopmentBasicElements, payload, "basicMagicDirectionOptions");
        InitialDevelopmentFirstClass ??= InitialDevelopmentBaseClasses.FirstOrDefault();
        InitialDevelopmentSecondClass ??= InitialDevelopmentBaseClasses.Skip(1).FirstOrDefault();
        InitialDevelopmentMagicMethod ??= InitialDevelopmentMagicMethods.FirstOrDefault();
        InitialDevelopmentBasicElement ??= InitialDevelopmentBasicElements.FirstOrDefault();
        InitialDevelopmentStatus = IsInitialDevelopmentPending
            ? "Выберите стартовые классы и магию. Этот пакет не расходует монеты опыта."
            : "Начальное развитие завершено.";
        IsInitialDevelopmentConfirmationOpen = false;
        Notify(nameof(InitialDevelopmentSummary));
    }

    private void ReviewInitialDevelopment02112()
    {
        var error = ValidateInitialDevelopmentSelection02112();
        if (!string.IsNullOrEmpty(error))
        {
            InitialDevelopmentStatus = error;
            return;
        }
        InitialDevelopmentStatus = "Проверьте выбранный стартовый пакет перед применением.";
        IsInitialDevelopmentConfirmationOpen = true;
    }

    private void CompleteInitialDevelopment02112()
    {
        var error = ValidateInitialDevelopmentSelection02112();
        if (!string.IsNullOrEmpty(error))
        {
            InitialDevelopmentStatus = error;
            return;
        }

        var rank = InitialDevelopmentUseSingleClass ? 2 : 1;
        var grants = new List<Dictionary<string, object>>
        {
            new() { ["developmentNodeId"] = InitialDevelopmentFirstClass!.Id, ["rank"] = rank }
        };
        if (InitialDevelopmentUseTwoClasses)
            grants.Add(new Dictionary<string, object> { ["developmentNodeId"] = InitialDevelopmentSecondClass!.Id, ["rank"] = 1 });

        var response = _api.InitialDevelopmentComplete(
            SelectedCharacterId,
            _initialDevelopmentRevision,
            "initial-development-" + Guid.NewGuid().ToString("N"),
            grants,
            InitialDevelopmentMagicMethod!.Id,
            InitialDevelopmentBasicElement!.Id);
        if (response.Status != ResponseStatus.Ok)
        {
            InitialDevelopmentStatus = response.Message;
            return;
        }

        IsInitialDevelopmentPending = false;
        IsInitialDevelopmentConfirmationOpen = false;
        InitialDevelopmentStatus = "Начальное развитие завершено. Монеты опыта не изменились.";
        LoadClassAndSkills();
    }

    private string ValidateInitialDevelopmentSelection02112()
    {
        if (InitialDevelopmentFirstClass == null) return "Выберите стартовый класс.";
        if (InitialDevelopmentUseTwoClasses && InitialDevelopmentSecondClass == null) return "Выберите второй стартовый класс.";
        if (InitialDevelopmentUseTwoClasses && string.Equals(InitialDevelopmentFirstClass.Id, InitialDevelopmentSecondClass?.Id, StringComparison.OrdinalIgnoreCase))
            return "Для варианта с двумя классами выберите разные классы.";
        if (InitialDevelopmentMagicMethod == null) return "Выберите первичный метод магии.";
        if (InitialDevelopmentBasicElement == null) return "Выберите базовую стихию.";
        return string.Empty;
    }

    private static void ReplaceInitialDevelopmentOptions02112(ObservableCollection<InitialDevelopmentChoiceVm> target, IDictionary<string, object> payload, string key)
    {
        target.Clear();
        if (!payload.TryGetValue(key, out var raw) || raw is not IEnumerable values || raw is string) return;
        foreach (var value in values)
        {
            var map = value as IDictionary<string, object>;
            if (map == null) continue;
            var id = map.TryGetValue("developmentNodeId", out var rawId) ? Convert.ToString(rawId) ?? string.Empty : string.Empty;
            var name = map.TryGetValue("displayName", out var rawName) ? Convert.ToString(rawName) ?? string.Empty : string.Empty;
            if (!string.IsNullOrWhiteSpace(id)) target.Add(new InitialDevelopmentChoiceVm(id, name));
        }
    }

    private static bool ReadInitialDevelopmentBool02112(IDictionary<string, object> payload, string key) =>
        payload.TryGetValue(key, out var raw) && (raw is bool value ? value : bool.TryParse(Convert.ToString(raw), out var parsed) && parsed);

    private static int ReadInitialDevelopmentInt02112(IDictionary<string, object> payload, string key) =>
        payload.TryGetValue(key, out var raw) && int.TryParse(Convert.ToString(raw), out var value) ? value : 0;
}
