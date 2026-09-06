using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed partial class PlayerProductionViewModel
{
    private PlayerLimitedProductionCandidate0196? _selectedLimitedCandidate0196;
    private PlayerLimitedProductionProject0196? _selectedLimitedProject0196;
    private int _limitedBatchSize0196 = 3;
    private string _limitedProjectName0196 = string.Empty;
    private string _limitedState0196 = "Выберите допущенный прототип и проверьте партию.";

    public ObservableCollection<PlayerLimitedProductionCandidate0196> LimitedCandidates0196 { get; } = new();
    public ObservableCollection<PlayerLimitedProductionProject0196> LimitedProjects0196 { get; } = new();
    public ObservableCollection<PlayerCraftLine0191> LimitedRequirements0196 { get; } = new();
    public ObservableCollection<PlayerCraftLine0191> LimitedResources0196 { get; } = new();
    public ObservableCollection<PlayerCraftLine0191> LimitedStages0196 { get; } = new();
    public ObservableCollection<int> LimitedBatchSizes0196 { get; } = new() { 1, 2, 3 };

    public ICommand RefreshLimitedProductionCommand0196 { get; private set; } = null!;
    public ICommand PreviewLimitedProductionCommand0196 { get; private set; } = null!;
    public ICommand CreateLimitedProductionCommand0196 { get; private set; } = null!;
    public ICommand SubmitLimitedProductionCommand0196 { get; private set; } = null!;
    public ICommand CancelLimitedProductionCommand0196 { get; private set; } = null!;

    public PlayerLimitedProductionCandidate0196? SelectedLimitedCandidate0196
    {
        get => _selectedLimitedCandidate0196;
        set
        {
            if (_selectedLimitedCandidate0196 == value)
                return;
            _selectedLimitedCandidate0196 = value;
            Notify();
            LimitedProjectName0196 = value == null ? string.Empty : "Партия: " + value.BlueprintName;
            LimitedRequirements0196.Clear();
            LimitedResources0196.Clear();
            Notify(nameof(LimitedDraftSummary0196));
        }
    }

    public PlayerLimitedProductionProject0196? SelectedLimitedProject0196
    {
        get => _selectedLimitedProject0196;
        set
        {
            if (_selectedLimitedProject0196 == value)
                return;
            _selectedLimitedProject0196 = value;
            Notify();
            if (value != null && !value.IsPlaceholder)
                LoadLimitedProductionProject0196();
        }
    }

    public int LimitedBatchSize0196
    {
        get => _limitedBatchSize0196;
        set
        {
            if (_limitedBatchSize0196 == value)
                return;
            _limitedBatchSize0196 = value;
            Notify();
            Notify(nameof(LimitedDraftSummary0196));
        }
    }

    public string LimitedProjectName0196
    {
        get => _limitedProjectName0196;
        set
        {
            if (_limitedProjectName0196 == value)
                return;
            _limitedProjectName0196 = value;
            Notify();
            Notify(nameof(LimitedDraftSummary0196));
        }
    }

    public string LimitedState0196
    {
        get => _limitedState0196;
        private set
        {
            if (_limitedState0196 == value)
                return;
            _limitedState0196 = value;
            Notify();
        }
    }

    public string LimitedDraftSummary0196
        => SelectedLimitedCandidate0196 == null
            ? "Допущенный прототип не выбран."
            : $"{SelectedLimitedCandidate0196.BlueprintName}\n"
              + $"Партия: {LimitedBatchSize0196} шт. из {SelectedLimitedCandidate0196.RemainingUnits} доступных\n"
              + "Лимит и материалы резервирует сервер после решения GM.";

    private void InitializeLimitedProduction0196()
    {
        RefreshLimitedProductionCommand0196 =
            new RelayCommand(() => RefreshLimitedProduction0196(false));
        PreviewLimitedProductionCommand0196 =
            new RelayCommand(PreviewLimitedProduction0196);
        CreateLimitedProductionCommand0196 =
            new RelayCommand(CreateLimitedProduction0196);
        SubmitLimitedProductionCommand0196 =
            new RelayCommand(SubmitLimitedProduction0196);
        CancelLimitedProductionCommand0196 =
            new RelayCommand(CancelLimitedProduction0196);
    }

    private void RefreshLimitedProduction0196(bool silent)
    {
        try
        {
            var selectedPrototypeId = SelectedLimitedCandidate0196?.PrototypeId;
            var candidates = _api.ProjectLimitedProductionAvailableList(
                LimitedProductionBasePayload0196());
            EnsureLimitedProductionOk0196(candidates);
            LimitedCandidates0196.Clear();
            foreach (var item in CraftItems0191(candidates))
            {
                var candidate = PlayerLimitedProductionCandidate0196.From(item);
                if (candidate.RemainingUnits > 0)
                    LimitedCandidates0196.Add(candidate);
            }

            var projects = _api.ProjectLimitedProductionList(
                LimitedProductionBasePayload0196());
            EnsureLimitedProductionOk0196(projects);
            LimitedProjects0196.Clear();
            foreach (var item in CraftItems0191(projects))
                LimitedProjects0196.Add(PlayerLimitedProductionProject0196.From(item));

            if (LimitedCandidates0196.Count == 0)
                LimitedCandidates0196.Add(
                    PlayerLimitedProductionCandidate0196.Placeholder(
                        "Нет прототипов, допущенных GM к ограниченному производству."));
            if (LimitedProjects0196.Count == 0)
                LimitedProjects0196.Add(
                    PlayerLimitedProductionProject0196.Placeholder(
                        "Проекты ограниченных партий пока не созданы."));
            SelectedLimitedCandidate0196 = LimitedCandidates0196.FirstOrDefault(
                x => !x.IsPlaceholder
                     && string.Equals(
                         x.PrototypeId,
                         selectedPrototypeId,
                         StringComparison.Ordinal))
                ?? LimitedCandidates0196.FirstOrDefault(x => !x.IsPlaceholder);
            if (SelectedLimitedProject0196 == null
                || LimitedProjects0196.All(x => x.ProjectId != SelectedLimitedProject0196.ProjectId))
                SelectedLimitedProject0196 =
                    LimitedProjects0196.FirstOrDefault(x => !x.IsPlaceholder);
            LimitedState0196 = "Ограниченные партии обновлены.";
        }
        catch (Exception ex)
        {
            LimitedState0196 = ex.Message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Ограниченные партии выключены feature flags."
                : "Ограниченные партии пока недоступны.";
            if (!silent)
                ErrorMessage = ex.Message;
        }
    }

    private void PreviewLimitedProduction0196()
    {
        if (!RequireLimitedCandidate0196())
            return;
        try
        {
            var payload = LimitedProductionBasePayload0196();
            payload["prototypeId"] = SelectedLimitedCandidate0196!.PrototypeId;
            payload["batchSize"] = LimitedBatchSize0196;
            var response = _api.ProjectLimitedProductionPreview(payload);
            EnsureLimitedProductionOk0196(response);
            var preview = CraftMap0191(
                response.Payload.TryGetValue("preview", out var raw) ? raw : null);
            FillCraftLines0191(preview, "requirements", LimitedRequirements0196);
            FillCraftLines0191(preview, "resources", LimitedResources0196);
            LimitedState0196 = "Партия проверена. Ресурсы ещё не зарезервированы.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            LimitedState0196 = "Не удалось проверить ограниченную партию.";
        }
    }

    private void CreateLimitedProduction0196()
    {
        if (!RequireLimitedCandidate0196())
            return;
        if (string.IsNullOrWhiteSpace(LimitedProjectName0196))
        {
            ErrorMessage = "Укажите понятное название партии.";
            return;
        }
        try
        {
            var payload = LimitedProductionBasePayload0196();
            payload["prototypeId"] = SelectedLimitedCandidate0196!.PrototypeId;
            payload["batchSize"] = LimitedBatchSize0196;
            payload["name"] = LimitedProjectName0196.Trim();
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsureLimitedProductionOk0196(_api.ProjectLimitedProductionCreate(payload));
            LimitedState0196 = "Черновик ограниченной партии создан.";
            RefreshLimitedProduction0196(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            LimitedState0196 = "Не удалось создать проект партии.";
        }
    }

    private void SubmitLimitedProduction0196()
    {
        if (!RequireLimitedProject0196())
            return;
        if (MessageBox.Show(
                "Отправить ограниченную партию GM на согласование?",
                "Ограниченная партия",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        MutateLimitedProduction0196(
            _api.ProjectLimitedProductionSubmit,
            "Проект партии отправлен GM.");
    }

    private void CancelLimitedProduction0196()
    {
        if (!RequireLimitedProject0196())
            return;
        if (MessageBox.Show(
                "Отменить проект партии? Резерв будет освобождён, если производство ещё не началось.",
                "Ограниченная партия",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        MutateLimitedProduction0196(
            _api.ProjectLimitedProductionCancel,
            "Проект партии отменён.");
    }

    private void MutateLimitedProduction0196(
        Func<Dictionary<string, object>, ResponseEnvelope> command,
        string success)
    {
        try
        {
            var payload = LimitedProductionBasePayload0196();
            payload["projectId"] = SelectedLimitedProject0196!.ProjectId;
            payload["expectedRevision"] = SelectedLimitedProject0196.Revision;
            payload["operationId"] = Guid.NewGuid().ToString("N");
            EnsureLimitedProductionOk0196(command(payload));
            LimitedState0196 = success;
            RefreshLimitedProduction0196(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            LimitedState0196 = "Действие не выполнено.";
        }
    }

    private void LoadLimitedProductionProject0196()
    {
        try
        {
            var response = _api.ProjectLimitedProductionGet(
                new Dictionary<string, object>
                {
                    ["projectId"] = SelectedLimitedProject0196!.ProjectId
                });
            EnsureLimitedProductionOk0196(response);
            var item = CraftMap0191(
                response.Payload.TryGetValue("item", out var raw) ? raw : null);
            SelectedLimitedProject0196.Apply(item);
            FillCraftLines0191(item, "requirements", LimitedRequirements0196);
            FillCraftLines0191(item, "resources", LimitedResources0196);
            FillCraftLines0191(item, "stages", LimitedStages0196);
            LimitedState0196 = SelectedLimitedProject0196.StatusLabel;
            Notify(nameof(SelectedLimitedProject0196));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            LimitedState0196 = "Не удалось открыть проект партии.";
        }
    }

    private bool RequireLimitedCandidate0196()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(_activeCharacterIdAccessor()))
        {
            ErrorMessage = "Сначала выберите активного персонажа.";
            return false;
        }
        if (SelectedLimitedCandidate0196 == null
            || SelectedLimitedCandidate0196.IsPlaceholder)
        {
            ErrorMessage = "Выберите допущенный прототип.";
            return false;
        }
        if (LimitedBatchSize0196 < 1
            || LimitedBatchSize0196 > Math.Min(3, SelectedLimitedCandidate0196.RemainingUnits))
        {
            ErrorMessage = "Размер партии превышает доступный лимит.";
            return false;
        }
        return true;
    }

    private bool RequireLimitedProject0196()
    {
        ErrorMessage = string.Empty;
        if (SelectedLimitedProject0196 != null
            && !SelectedLimitedProject0196.IsPlaceholder)
            return true;
        ErrorMessage = "Выберите проект ограниченной партии.";
        return false;
    }

    private Dictionary<string, object> LimitedProductionBasePayload0196()
    {
        var payload = new Dictionary<string, object> { ["campaignId"] = CampaignId };
        var characterId = _activeCharacterIdAccessor();
        if (!string.IsNullOrWhiteSpace(characterId))
            payload["characterId"] = characterId;
        return payload;
    }

    private static void EnsureLimitedProductionOk0196(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.Message)
                    ? "Ограниченная партия недоступна."
                    : response.Message);
    }
}

public sealed class PlayerLimitedProductionCandidate0196
{
    public string PrototypeId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string BlueprintName { get; private set; } = string.Empty;
    public int RemainingUnits { get; private set; }
    public int ProducedUnits { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string Warning { get; private set; } = string.Empty;
    public bool IsPlaceholder { get; private set; }

    public string Summary => IsPlaceholder
        ? Name
        : $"{Name}\nЧертёж: {BlueprintName}\nДоступно: {RemainingUnits} из 3";

    public static PlayerLimitedProductionCandidate0196 From(
        IDictionary<string, object> map)
        => new()
        {
            PrototypeId = PlayerCraftParsing0191.Read(map, "prototypeId"),
            Name = PlayerCraftParsing0191.First(
                PlayerCraftParsing0191.Read(map, "name"),
                "Допущенный прототип"),
            BlueprintName = PlayerCraftParsing0191.First(
                PlayerCraftParsing0191.Read(map, "blueprintName"),
                "Чертёж"),
            RemainingUnits = PlayerCraftParsing0191.ReadInt(map, "remainingUnits"),
            ProducedUnits = PlayerCraftParsing0191.ReadInt(map, "producedUnits"),
            Status = PlayerCraftParsing0191.Read(map, "status"),
            Warning = PlayerCraftParsing0191.Read(map, "warning")
        };

    public static PlayerLimitedProductionCandidate0196 Placeholder(string text)
        => new() { Name = text, IsPlaceholder = true };
}

public sealed class PlayerLimitedProductionProject0196
{
    public string ProjectId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string BlueprintName { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public string CurrentStageName { get; private set; } = string.Empty;
    public int BatchSize { get; private set; }
    public int Revision { get; private set; }
    public int ProgressPercent { get; private set; }
    public string ResultSummary { get; private set; } = string.Empty;
    public bool IsPlaceholder { get; private set; }

    public string Summary => IsPlaceholder
        ? Name
        : $"{Name}\n{BlueprintName} · {BatchSize} шт.\n{StatusLabel} · {ProgressPercent}%";

    public static PlayerLimitedProductionProject0196 From(
        IDictionary<string, object> map)
    {
        var item = new PlayerLimitedProductionProject0196();
        item.Apply(map);
        return item;
    }

    public void Apply(IDictionary<string, object> map)
    {
        ProjectId = PlayerCraftParsing0191.Read(map, "projectId");
        Name = PlayerCraftParsing0191.First(
            PlayerCraftParsing0191.Read(map, "name"),
            "Ограниченная партия");
        BlueprintName = PlayerCraftParsing0191.Read(map, "blueprintName");
        StatusLabel = PlayerCraftParsing0191.First(
            PlayerCraftParsing0191.Read(map, "statusLabel"),
            "Состояние не указано");
        CurrentStageName = PlayerCraftParsing0191.Read(map, "currentStageName");
        BatchSize = PlayerCraftParsing0191.ReadInt(map, "batchSize");
        Revision = PlayerCraftParsing0191.ReadInt(map, "revision");
        ProgressPercent = PlayerCraftParsing0191.ReadInt(map, "progressPercent");
        if (map.TryGetValue("result", out var rawResult))
        {
            var result = PlayerProductionViewModel.CraftMap0191(rawResult);
            var quantity = PlayerCraftParsing0191.ReadInt(result, "quantity");
            var summary = PlayerCraftParsing0191.Read(result, "summary");
            ResultSummary = quantity > 0
                ? $"Готовая партия: {quantity} шт. {summary}".Trim()
                : summary;
        }
    }

    public static PlayerLimitedProductionProject0196 Placeholder(string text)
        => new() { Name = text, IsPlaceholder = true };
}
