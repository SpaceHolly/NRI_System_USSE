using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed partial class SystemToolsViewModel
{
    private const string CoreReferencePackId0188 = "nri.reference.demo.core";
    private string _referencePackSummary = "Эталонный пакет ещё не проверялся.";
    private string _referencePackVersion = "1.0.0";
    private string _referencePackValidation = "Запустите предварительную проверку перед применением.";
    private bool _referencePackApplyConfirmed;
    private ReferencePackRecordUiItem? _selectedReferencePackRecord;

    public ObservableCollection<ReferencePackRecordUiItem> ReferencePackRecords { get; } = new();
    public ObservableCollection<SystemIssueUiItem> ReferencePackIssues { get; } = new();

    public ICommand PreviewReferencePackCommand { get; private set; } = null!;
    public ICommand ApplyReferencePackCommand { get; private set; } = null!;
    public ICommand RefreshReferencePackStatusCommand { get; private set; } = null!;

    public string ReferencePackDisplayName => "Эталонный демонстрационный пакет";
    public string ReferencePackVersion
    {
        get => _referencePackVersion;
        private set
        {
            if (_referencePackVersion == value) return;
            _referencePackVersion = value;
            Notify();
        }
    }

    public string ReferencePackSummary
    {
        get => _referencePackSummary;
        private set
        {
            if (_referencePackSummary == value) return;
            _referencePackSummary = value;
            Notify();
        }
    }

    public string ReferencePackValidation
    {
        get => _referencePackValidation;
        private set
        {
            if (_referencePackValidation == value) return;
            _referencePackValidation = value;
            Notify();
        }
    }

    public bool ReferencePackApplyConfirmed
    {
        get => _referencePackApplyConfirmed;
        set
        {
            if (_referencePackApplyConfirmed == value) return;
            _referencePackApplyConfirmed = value;
            Notify();
        }
    }

    public ReferencePackRecordUiItem? SelectedReferencePackRecord
    {
        get => _selectedReferencePackRecord;
        set
        {
            if (_selectedReferencePackRecord == value) return;
            _selectedReferencePackRecord = value;
            Notify();
            Notify(nameof(SelectedReferencePackRecordSummary));
        }
    }

    public string SelectedReferencePackRecordSummary =>
        SelectedReferencePackRecord == null
            ? "Выберите запись, чтобы увидеть её связи и результат проверки."
            : SelectedReferencePackRecord.ReferenceCount == 0
                ? $"{SelectedReferencePackRecord.DisplayName}: внешние связи не требуются."
                : $"{SelectedReferencePackRecord.DisplayName}: {SelectedReferencePackRecord.ReferenceSummary}";

    private void InitializeReferencePack0188()
    {
        PreviewReferencePackCommand = new RelayCommand(
            () => RunSafe("admin.reference_pack.preview", PreviewReferencePack0188));
        ApplyReferencePackCommand = new RelayCommand(
            () => RunSafe("admin.reference_pack.apply", ApplyReferencePack0188));
        RefreshReferencePackStatusCommand = new RelayCommand(
            () => RunSafe("admin.reference_pack.status", RefreshReferencePackStatus0188));
    }

    private void PreviewReferencePack0188()
    {
        LoadReferencePackResponse0188(
            _api.DefinitionPackAdminPreview(CoreReferencePackId0188),
            "Предварительная проверка пакета недоступна.");
    }

    private void RefreshReferencePackStatus0188()
    {
        LoadReferencePackResponse0188(
            _api.DefinitionPackAdminStatus(CoreReferencePackId0188),
            "Состояние эталонного пакета недоступно.");
    }

    private void ApplyReferencePack0188()
    {
        if (!ReferencePackApplyConfirmed)
        {
            ReferencePackValidation = "Подтвердите применение после просмотра предварительного плана.";
            StatusMessage = ReferencePackValidation;
            return;
        }

        LoadReferencePackResponse0188(
            _api.DefinitionPackAdminApply(CoreReferencePackId0188),
            "Эталонный пакет не удалось применить.");
        ReferencePackApplyConfirmed = false;
    }

    private void LoadReferencePackResponse0188(ResponseEnvelope response, string fallback)
    {
        ReferencePackRecords.Clear();
        ReferencePackIssues.Clear();
        if (!IsOk(response))
        {
            ReferencePackSummary = Friendly(response, fallback);
            ReferencePackValidation = ReferencePackSummary;
            StatusMessage = ReferencePackSummary;
            ErrorMessage = ReferencePackSummary;
            ClientLogService.Instance.Warn($"admin.reference_pack.response status={response.Status}");
            return;
        }

        var payload = response.Payload;
        ReferencePackVersion = Str(payload, "semanticVersion", "1.0.0");
        foreach (var record in AsDictionaries(Get(payload, "records")))
        {
            ReferencePackRecords.Add(new ReferencePackRecordUiItem
            {
                DisplayName = Str(record, "displayName", "Без названия"),
                Category = LocalizeReferencePackCategory0188(Str(record, "category")),
                Classification = LocalizeReferencePackClassification0188(Str(record, "classification")),
                ReferenceCount = Int(record, "referenceCount"),
                ReferenceSummary = Str(record, "referenceSummary", "Без связей"),
                Findings = JoinList(Get(record, "findings"))
            });
        }

        AddIssues(ReferencePackIssues, Get(payload, "errors"), "Ошибка");
        AddIssues(ReferencePackIssues, Get(payload, "warnings"), "Предупреждение");
        var valid = Bool(payload, "isValid");
        var applied = Bool(payload, "applied");
        ReferencePackValidation = valid
            ? "Проверка пройдена: ссылки и версии согласованы."
            : "Проверка не пройдена. Пакет нельзя применять.";
        ReferencePackSummary =
            $"Версия {ReferencePackVersion}; записей: {ReferencePackRecords.Count}; " +
            $"создано: {Int(payload, "createdCount")}; обновлено: {Int(payload, "updatedCount")}; " +
            $"пропущено: {Int(payload, "skippedCount")}.";
        if (applied) ReferencePackSummary = "Пакет применён. " + ReferencePackSummary;
        SelectedReferencePackRecord = ReferencePackRecords.FirstOrDefault();
        StatusMessage = ReferencePackSummary;
        ErrorMessage = valid ? string.Empty : ReferencePackValidation;
    }

    private static string LocalizeReferencePackClassification0188(string value) =>
        value switch
        {
            "Create" => "Будет создано",
            "AlreadyCurrent" => "Актуально",
            "SafeUpdate" => "Безопасное обновление",
            "UserModifiedConflict" => "Изменено пользователем",
            "MissingDependency" => "Нет зависимости",
            "InvalidReference" => "Ошибка связи",
            "ArchivedTarget" => "Запись в архиве",
            "IncompatibleSchema" => "Несовместимая схема",
            _ => "Требует проверки"
        };

    private static string LocalizeReferencePackCategory0188(string value)
    {
        if (Contains(value, "weapon")) return "Оружие";
        if (Contains(value, "armor")) return "Броня";
        if (Contains(value, "magic")
            || Contains(value, "spell")
            || Contains(value, "effect")) return "Магия и эффекты";
        if (Contains(value, "location")
            || Contains(value, "lore")
            || Contains(value, "language")) return "Мир и знания";
        if (Contains(value, "faction")
            || Contains(value, "organization")
            || Contains(value, "currency")
            || Contains(value, "market")) return "Общество и экономика";
        if (Contains(value, "technology")
            || Contains(value, "recipe")
            || Contains(value, "blueprint")) return "Технологии";
        if (Contains(value, "resource")) return "Ресурсы";
        return "Общие справочники";
    }
}

public sealed class ReferencePackRecordUiItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public int ReferenceCount { get; set; }
    public string ReferenceSummary { get; set; } = string.Empty;
    public string Findings { get; set; } = string.Empty;
}
