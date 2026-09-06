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
using Nri.Shared.Utilities;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminCombatReadOnlyViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "dev-campaign-core";
    private string _sessionId = "dev-session-core";
    private string _newCombatName = "Тестовый бой";
    private string _newParticipantName = "Участник";
    private string _newParticipantType = "npc";
    private string _newParticipantTeam = "neutral";
    private string _newParticipantVisibility = "player_visible";
    private string _selectedVisibility = "player_visible";
    private string _selectedMapTokenId = string.Empty;
    private string _selectedMapTokenName = string.Empty;
    private string _selectedMapTokenVisibility = "hidden";
    private string _combatMapStatusText = "Боевой слой карты не загружен.";
    private string _activeSceneMapText = "Активная карта сцены не выбрана.";
    private double _combatMapCanvasWidth = 520d;
    private double _combatMapCanvasHeight = 300d;
    private string _combatMapScaleText = "Карта не загружена.";
    private double _combatMapWidthMeters = 1d;
    private double _combatMapHeightMeters = 1d;
    private double _combatMapGridMeters = 5d;
    private AdminCombatTrackerCombatItem? _selectedCombat;
    private AdminCombatTrackerParticipantItem? _selectedParticipant;
    private AdminCombatTrackerParticipantItem? _selectedAttackTarget;
    private AdminCombatTrackerParticipantItem? _selectedArmorUntrained;
    private AdminCombatTrackerParticipantItem? _selectedArmorTrained;
    private AdminCombatSkillOptionVm? _selectedAttackSkill;
    private AdminCombatWeaponOptionVm? _selectedAttackWeapon;
    private AdminCombatFacingOptionVm? _selectedAttackFacing;
    private string _attackResolutionText = "Выберите действующего участника, цель и навык.";
    private string _armorComparisonText = "Выберите двух участников в латах.";
    private AdminCombatMapTokenItem? _selectedCombatMapToken;
    private AdminCombatMapTokenItem? _selectedCombatOverlayToken;
    private bool _isBusy;
    private string _statusMessage = "Трекер боя готов.";
    private string _errorMessage = string.Empty;
    private DateTime _lastRefreshAtUtc;

    public AdminCombatReadOnlyViewModel(CommandApi api)
    {
        _api = api;
        RefreshCombatMapCommand = new RelayCommand(() => Run("Обновить combat map overlay", RefreshCombatMap));
        AddSelectedTokenToCombatCommand = new RelayCommand(() => Run("Добавить токен в бой", AddSelectedTokenToCombat));
        LinkSelectedOverlayTokenCommand = new RelayCommand(() => Run("Привязать выбранный токен", LinkSelectedOverlayToken));
        UnlinkSelectedOverlayTokenCommand = new RelayCommand(() => Run("Отвязать выбранный токен", UnlinkSelectedOverlayToken));
        SyncVisibilityFromTokenCommand = new RelayCommand(() => Run("Синхронизировать видимость токена", SyncVisibilityFromToken));
        FocusSelectedTokenCommand = new RelayCommand(() => Run("Фокус токена", FocusSelectedToken));
        RefreshCommand = new RelayCommand(() => Run("Обновить", Refresh));
        CreateCombatCommand = new RelayCommand(() => Run("Создать бой", CreateCombat));
        AddParticipantCommand = new RelayCommand(() => Run("Добавить участника", AddParticipant));
        UpdateParticipantCommand = new RelayCommand(() => Run("Сохранить участника", UpdateParticipant));
        RemoveParticipantCommand = new RelayCommand(() => Run("Удалить участника", RemoveParticipant));
        SetParticipantVisibilityCommand = new RelayCommand(() => Run("Обновить видимость", SetParticipantVisibility));
        LinkMapTokenCommand = new RelayCommand(() => Run("Привязать токен", LinkMapToken));
        UnlinkMapTokenCommand = new RelayCommand(() => Run("Отвязать токен", UnlinkMapToken));
        RollInitiativeCommand = new RelayCommand(() => Run("Бросить инициативу", RollInitiative));
        StartCombatCommand = new RelayCommand(() => Run("Начать бой", StartCombat));
        PauseCombatCommand = new RelayCommand(() => Run("Пауза", PauseCombat));
        ResumeCombatCommand = new RelayCommand(() => Run("Продолжить", ResumeCombat));
        NextTurnCommand = new RelayCommand(() => Run("Следующий ход", NextTurn));
        SkipTurnCommand = new RelayCommand(() => Run("Пропустить ход", SkipTurn));
        PreviousTurnCommand = new RelayCommand(() => Run("Предыдущий ход", PreviousTurn));
        EndCombatCommand = new RelayCommand(() => Run("Завершить бой", EndCombat));
        AddLogEventCommand = new RelayCommand(() => Run("Добавить событие", AddLogEvent));
        ExecuteAttackCommand = new RelayCommand(() => Run("Разрешить атаку", ExecuteAttack));
        ComparePlateArmorCommand = new RelayCommand(() => Run("Сравнить ношение лат", ComparePlateArmor));
        SelectedAttackFacing = AttackFacingOptions[0];
    }

    public ObservableCollection<AdminCombatTrackerCombatItem> Combats { get; } = new();
    public ObservableCollection<AdminCombatTrackerParticipantItem> Participants { get; } = new();
    public ObservableCollection<AdminCombatTrackerParticipantItem> InitiativeOrder { get; } = new();
    public ObservableCollection<AdminCombatTrackerLogItem> CombatLog { get; } = new();
    public ObservableCollection<AdminCombatMapTokenItem> CombatMapJoinableTokens { get; } = new();
    public ObservableCollection<AdminCombatMapTokenItem> CombatMapOverlayTokens { get; } = new();
    public ObservableCollection<AdminCombatSkillOptionVm> AttackSkillOptions { get; } = new();
    public ObservableCollection<AdminCombatWeaponOptionVm> AttackWeaponOptions { get; } = new();
    public ObservableCollection<AdminCombatFacingOptionVm> AttackFacingOptions { get; } = new(new[]
    {
        new AdminCombatFacingOptionVm("torso", "Корпус / торс"),
        new AdminCombatFacingOptionVm("front", "Лобовая броня"),
        new AdminCombatFacingOptionVm("side", "Бортовая броня"),
        new AdminCombatFacingOptionVm("rear", "Кормовая броня")
    });
    public ObservableCollection<MapGridLineUiItem> CombatMapGridLines { get; } = new();
    public ObservableCollection<SceneMapTilePatchUiItem> CombatMapTilePatches { get; } = new();
    public ObservableCollection<SceneMapAssetInstanceUiItem> CombatMapAssetInstances { get; } = new();
    public ObservableCollection<string> CombatMapWarnings { get; } = new();
    public string[] ParticipantTypes { get; } = { "player_character", "npc", "companion", "enemy", "neutral", "creature", "vehicle", "custom" };
    public string[] VisibilityModes { get; } = { "player_visible", "gm_only", "hidden" };

    public string CampaignId { get => _campaignId; set { _campaignId = value ?? string.Empty; Notify(); } }
    public string SessionId { get => _sessionId; set { _sessionId = value ?? string.Empty; Notify(); } }
    public string NewCombatName { get => _newCombatName; set { _newCombatName = value ?? string.Empty; Notify(); } }
    public string NewParticipantName { get => _newParticipantName; set { _newParticipantName = value ?? string.Empty; Notify(); } }
    public string NewParticipantType { get => _newParticipantType; set { _newParticipantType = value ?? string.Empty; Notify(); } }
    public string NewParticipantTeam { get => _newParticipantTeam; set { _newParticipantTeam = value ?? string.Empty; Notify(); } }
    public string NewParticipantVisibility { get => _newParticipantVisibility; set { _newParticipantVisibility = value ?? string.Empty; Notify(); } }
    public string SelectedVisibility { get => _selectedVisibility; set { _selectedVisibility = value ?? string.Empty; Notify(); } }
    public string SelectedMapTokenId { get => _selectedMapTokenId; set { _selectedMapTokenId = value ?? string.Empty; Notify(); } }
    public string SelectedMapTokenName { get => _selectedMapTokenName; set { _selectedMapTokenName = value ?? string.Empty; Notify(); } }
    public string SelectedMapTokenVisibility { get => _selectedMapTokenVisibility; set { _selectedMapTokenVisibility = value ?? string.Empty; Notify(); } }
    public string CombatMapStatusText { get => _combatMapStatusText; private set { _combatMapStatusText = value ?? string.Empty; Notify(); } }
    public string ActiveSceneMapText { get => _activeSceneMapText; private set { _activeSceneMapText = value ?? string.Empty; Notify(); } }
    public double CombatMapCanvasWidth { get => _combatMapCanvasWidth; private set { if (Math.Abs(_combatMapCanvasWidth - value) > 0.01) { _combatMapCanvasWidth = value; Notify(); } } }
    public double CombatMapCanvasHeight { get => _combatMapCanvasHeight; private set { if (Math.Abs(_combatMapCanvasHeight - value) > 0.01) { _combatMapCanvasHeight = value; Notify(); } } }
    public string CombatMapScaleText { get => _combatMapScaleText; private set { _combatMapScaleText = value ?? string.Empty; Notify(); } }
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; Notify(); } }
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value ?? string.Empty; Notify(); } }
    public string ErrorMessage { get => _errorMessage; private set { _errorMessage = value ?? string.Empty; Notify(); Notify(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public DateTime LastRefreshAtUtc { get => _lastRefreshAtUtc; private set { _lastRefreshAtUtc = value; Notify(); Notify(nameof(LastRefreshText)); } }
    public string LastRefreshText => LastRefreshAtUtc == default ? "не обновлялось" : LastRefreshAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public AdminCombatTrackerCombatItem? SelectedCombat
    {
        get => _selectedCombat;
        set
        {
            _selectedCombat = value;
            Notify();
            if (value != null) LoadCombat(value.CombatId);
        }
    }

    public AdminCombatTrackerParticipantItem? SelectedParticipant
    {
        get => _selectedParticipant;
        set
        {
            _selectedParticipant = value;
            SelectedVisibility = value?.VisibilityMode ?? "player_visible";
            SelectedMapTokenId = value?.MapTokenId ?? string.Empty;
            SelectedMapTokenName = value?.MapTokenDisplayName ?? string.Empty;
            SelectedMapTokenVisibility = value?.MapTokenVisibility ?? "hidden";
            Notify();
            Notify(nameof(SelectedParticipantSummary));
            LoadAttackSkills(value?.CharacterId);
            LoadAttackWeapons(value?.CharacterId);
            SelectedAttackTarget = Participants.FirstOrDefault(x => !string.Equals(x.ParticipantId, value?.ParticipantId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public AdminCombatTrackerParticipantItem? SelectedAttackTarget
    {
        get => _selectedAttackTarget;
        set { _selectedAttackTarget = value; Notify(); }
    }

    public AdminCombatTrackerParticipantItem? SelectedArmorUntrained
    {
        get => _selectedArmorUntrained;
        set { _selectedArmorUntrained = value; Notify(); }
    }

    public AdminCombatTrackerParticipantItem? SelectedArmorTrained
    {
        get => _selectedArmorTrained;
        set { _selectedArmorTrained = value; Notify(); }
    }

    public AdminCombatSkillOptionVm? SelectedAttackSkill
    {
        get => _selectedAttackSkill;
        set { _selectedAttackSkill = value; Notify(); }
    }

    public AdminCombatWeaponOptionVm? SelectedAttackWeapon
    {
        get => _selectedAttackWeapon;
        set { _selectedAttackWeapon = value; Notify(); }
    }

    public AdminCombatFacingOptionVm? SelectedAttackFacing
    {
        get => _selectedAttackFacing;
        set { _selectedAttackFacing = value; Notify(); }
    }

    public string AttackResolutionText
    {
        get => _attackResolutionText;
        private set { _attackResolutionText = value ?? string.Empty; Notify(); }
    }

    public string ArmorComparisonText
    {
        get => _armorComparisonText;
        private set { _armorComparisonText = value ?? string.Empty; Notify(); }
    }

    public AdminCombatMapTokenItem? SelectedCombatMapToken
    {
        get => _selectedCombatMapToken;
        set
        {
            _selectedCombatMapToken = value;
            if (value != null)
            {
                SelectedMapTokenId = value.TokenId;
                SelectedMapTokenName = value.DisplayName;
                SelectedMapTokenVisibility = value.CombatVisibility;
            }
            Notify();
        }
    }

    public AdminCombatMapTokenItem? SelectedCombatOverlayToken
    {
        get => _selectedCombatOverlayToken;
        set
        {
            _selectedCombatOverlayToken = value;
            if (value != null)
            {
                SelectedParticipant = Participants.FirstOrDefault(x => string.Equals(x.ParticipantId, value.ParticipantId, StringComparison.OrdinalIgnoreCase));
            }
            Notify();
        }
    }

    public string CurrentRoundText => SelectedCombat == null
        ? "Раунд: -"
        : SelectedCombat.RoundNumber == 0 && !string.IsNullOrWhiteSpace(SelectedCombat.CurrentParticipantId)
            ? "Предраундовый ход · до раунда 1"
            : $"Раунд {SelectedCombat.RoundNumber} / 5 секунд";
    public string CurrentTurnText => SelectedCombat == null
        ? "Ход не выбран"
        : $"Текущий участник: {SelectedCombat.CurrentParticipantName}";
    public string SelectedParticipantSummary => SelectedParticipant == null ? "Участник не выбран." : $"{SelectedParticipant.DisplayName}: инициатива {SelectedParticipant.InitiativeRoll}, {SelectedParticipant.VisibilityLabel}, {SelectedParticipant.ActionSummary}";

    public ICommand RefreshCommand { get; }
    public ICommand CreateCombatCommand { get; }
    public ICommand AddParticipantCommand { get; }
    public ICommand UpdateParticipantCommand { get; }
    public ICommand RemoveParticipantCommand { get; }
    public ICommand SetParticipantVisibilityCommand { get; }
    public ICommand LinkMapTokenCommand { get; }
    public ICommand UnlinkMapTokenCommand { get; }
    public ICommand RefreshCombatMapCommand { get; }
    public ICommand AddSelectedTokenToCombatCommand { get; }
    public ICommand LinkSelectedOverlayTokenCommand { get; }
    public ICommand UnlinkSelectedOverlayTokenCommand { get; }
    public ICommand SyncVisibilityFromTokenCommand { get; }
    public ICommand FocusSelectedTokenCommand { get; }
    public ICommand RollInitiativeCommand { get; }
    public ICommand StartCombatCommand { get; }
    public ICommand PauseCombatCommand { get; }
    public ICommand ResumeCombatCommand { get; }
    public ICommand NextTurnCommand { get; }
    public ICommand SkipTurnCommand { get; }
    public ICommand PreviousTurnCommand { get; }
    public ICommand EndCombatCommand { get; }
    public ICommand AddLogEventCommand { get; }
    public ICommand ExecuteAttackCommand { get; }
    public ICommand ComparePlateArmorCommand { get; }

    private void ComparePlateArmor()
    {
        if (SelectedCombat == null || SelectedArmorUntrained == null || SelectedArmorTrained == null)
        {
            ArmorComparisonText = "Выберите участника без подготовки и участника с подготовкой.";
            return;
        }

        var first = LoadArmorComparison(SelectedArmorUntrained);
        var second = LoadArmorComparison(SelectedArmorTrained);
        if (first == null || second == null) return;
        ArmorComparisonText =
            $"{SelectedArmorUntrained.DisplayName}\nЗащита лат: {first.Value.Protection}; навык: ранг {first.Value.Rank}; штраф манёвра: -{first.Value.Penalty}\n\n" +
            $"{SelectedArmorTrained.DisplayName}\nЗащита лат: {second.Value.Protection}; навык: ранг {second.Value.Rank}; штраф манёвра: -{second.Value.Penalty}\n\n" +
            (first.Value.Protection == second.Value.Protection
                ? "Вывод: защита одинакова; подготовка уменьшает штраф манёвра, а не усиливает броню."
                : "Вывод: итоговая защита различается; сравните экипировку и дополнительные модификаторы.");
        StatusMessage = "Сравнение рассчитано сервером по экипировке и Character v2 навыкам.";
    }

    private (int Protection, int Rank, int Penalty)? LoadArmorComparison(AdminCombatTrackerParticipantItem participant)
    {
        var response = _api.CombatV1DefensePreview(new Dictionary<string, object>
        {
            ["encounterId"] = SelectedCombat?.CombatId ?? string.Empty,
            ["targetParticipantId"] = participant.ParticipantId,
            ["includeArmor"] = true,
            ["includeShield"] = false,
            ["includeCover"] = false,
            ["includeDistance"] = false,
            ["strictMode"] = true,
            ["requestId"] = $"admin-armor-compare-{Guid.NewGuid():N}"
        });
        if (!EnsureOk(response, $"Не удалось рассчитать защиту для {participant.DisplayName}.")) return null;
        return (
            Int(Get(response.Payload, "armorDefenseBonus")),
            Int(Get(response.Payload, "armorTrainingRank")),
            Int(Get(response.Payload, "effectiveMobilityPenalty")));
    }

    private void LoadAttackSkills(string? characterId)
    {
        AttackSkillOptions.Clear();
        SelectedAttackSkill = null;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            AttackResolutionText = "Для участника без профиля персонажа навыки недоступны.";
            return;
        }

        var response = _api.SkillsList(characterId!);
        if (!EnsureOk(response, "Не удалось загрузить навыки участника.")) return;
        foreach (var map in Maps(Get(response.Payload, "items")))
        {
            if (!Bool(Get(map, "acquired"))) continue;
            AttackSkillOptions.Add(new AdminCombatSkillOptionVm
            {
                SkillCode = Str(Get(map, "skillId")),
                Name = Str(Get(map, "name"), "Навык"),
                DefaultAttribute = Str(Get(map, "defaultAttribute")),
                DefaultSubAttribute = Str(Get(map, "defaultSubAttribute")),
                Rank = Int(Get(map, "rank")),
                MasteryBand = Str(Get(map, "masteryBand"), "Без подготовки"),
                ProficiencyBonus = Int(Get(map, "proficiencyBonus"))
            });
        }

        SelectedAttackSkill = AttackSkillOptions.FirstOrDefault();
        AttackResolutionText = AttackSkillOptions.Count == 0
            ? "У персонажа нет освоенных боевых навыков."
            : "Готово к серверной проверке попадания.";
    }

    private void LoadAttackWeapons(string? characterId)
    {
        AttackWeaponOptions.Clear();
        SelectedAttackWeapon = null;
        if (string.IsNullOrWhiteSpace(characterId)) return;

        var response = _api.CharacterInventoryGet(characterId!);
        if (!EnsureOk(response, "Не удалось загрузить экипированное оружие участника.")) return;
        foreach (var map in Maps(Get(response.Payload, "inventory")))
        {
            var category = Str(Get(map, "definitionCategory"), Str(Get(map, "category"), Str(Get(map, "snapshotCategory"))));
            var isEquipped = Bool(Get(map, "isEquipped")) || Bool(Get(map, "equipped"));
            if (!isEquipped || !string.Equals(category, "weapon", StringComparison.OrdinalIgnoreCase)) continue;
            AttackWeaponOptions.Add(new AdminCombatWeaponOptionVm
            {
                ItemInstanceId = Str(Get(map, "id"), Str(Get(map, "itemId"))),
                DefinitionId = Str(Get(map, "itemDefinitionId"), Str(Get(map, "definitionId"), Str(Get(map, "itemCode")))),
                DisplayName = Str(Get(map, "displayName"), Str(Get(map, "snapshotDisplayName"), Str(Get(map, "name"), "Оружие")))
            });
        }

        SelectedAttackWeapon = AttackWeaponOptions.FirstOrDefault();
        if (SelectedAttackWeapon == null)
            AttackResolutionText = "У участника нет экипированного оружия с привязкой к справочнику.";
    }

    private void ExecuteAttack()
    {
        if (SelectedCombat == null || SelectedParticipant == null || SelectedAttackTarget == null || SelectedAttackSkill == null || SelectedAttackWeapon == null)
        {
            AttackResolutionText = "Выберите действующего участника, цель, экипированное оружие и освоенный навык.";
            return;
        }

        var response = _api.CombatV1WeaponAttackResolve(new Dictionary<string, object>
        {
            ["encounterId"] = SelectedCombat.CombatId,
            ["actorParticipantId"] = SelectedParticipant.ParticipantId,
            ["targetParticipantId"] = SelectedAttackTarget.ParticipantId,
            ["weaponItemInstanceId"] = SelectedAttackWeapon.ItemInstanceId,
            ["weaponDefinitionId"] = SelectedAttackWeapon.DefinitionId,
            ["attackSkillId"] = SelectedAttackSkill.SkillCode,
            ["attackAttributeId"] = SelectedAttackSkill.DefaultAttribute,
            ["spendActionPoint"] = true,
            ["autoApplyDamage"] = true,
            ["damageType"] = "physical",
            ["targetProtectionZone"] = SelectedAttackFacing?.Code ?? "torso",
            ["requestId"] = $"admin-attack-{Guid.NewGuid():N}"
        });
        if (!EnsureOk(response, "Сервер не смог разрешить атаку.")) return;

        var attack = Map(Get(response.Payload, "attackResult")) ?? new Dictionary<string, object>();
        var penetration = Map(Get(response.Payload, "penetrationResult")) ?? new Dictionary<string, object>();
        var preview = Map(Get(response.Payload, "damagePreview")) ?? new Dictionary<string, object>();
        var damage = Map(Get(response.Payload, "damageResult")) ?? new Dictionary<string, object>();
        var weapon = Map(Get(response.Payload, "weaponSummary")) ?? new Dictionary<string, object>();
        var modifier = Int(Get(attack, "totalModifier"));
        var hitResult = DisplayHitResult(Str(Get(attack, "hitResult")));
        var degree = DisplayDegree(Str(Get(attack, "degreeOfSuccess")));
        var penetrated = Bool(Get(penetration, "isPenetrated"));
        var weaponName = Str(Get(weapon, "displayName"), "Экипированное оружие");
        var attackProfileName = Str(Get(weapon, "attackProfileName"), "Основная атака");
        var protectionZone = DisplayProtectionZone(Str(Get(penetration, "protectionZone"), SelectedAttackFacing?.Code ?? "torso"));
        var resourceType = Str(Get(damage, "resourceType")).Trim().ToLowerInvariant();
        var resourceLabel = resourceType == "structure" ? "Прочность" : resourceType == "health" ? "Здоровье" : "Ресурс";
        var previousResource = resourceType == "structure" ? Int(Get(damage, "previousResource")) : Int(Get(damage, "previousHealth"));
        var currentResource = resourceType == "structure" ? Int(Get(damage, "currentResource")) : Int(Get(damage, "currentHealth"));
        var resolutionText =
            $"{weaponName} · {attackProfileName}\n" +
            $"Попадание: d20 {Int(Get(attack, "naturalRoll"))} + {modifier:+0;-0;0} = {Int(Get(attack, "attackTotal"))} против защиты {Int(Get(attack, "targetDefense"))} · {hitResult} · {degree}.\n" +
            $"Пробитие ({protectionZone}): {Int(Get(penetration, "totalPenetration"))} против защиты {Int(Get(penetration, "targetProtection"))} · {(penetrated ? "пробито" : "остановлено")}.\n" +
            $"Урон: до защиты {Int(Get(preview, "damageBeforeMitigation"))}, предотвращено {Int(Get(preview, "mitigatedDamage"))}, применено {Int(Get(damage, "damageApplied"))}.\n" +
            $"{resourceLabel}: {previousResource} → {currentResource}.";
        StatusMessage = "Полная атака разрешена сервером.";
        LoadCombat(SelectedCombat.CombatId);
        AttackResolutionText = resolutionText;
    }

    private void Refresh()
    {
        var response = _api.CombatV1EncounterList(new Dictionary<string, object>
        {
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId,
            ["includeEnded"] = false
        });
        if (!EnsureOk(response, "Не удалось загрузить список боев.")) return;

        Combats.Clear();
        foreach (var item in Maps(Get(response.Payload, "items")))
            Combats.Add(AdminCombatTrackerCombatItem.From(item));
        if (SelectedCombat == null && Combats.Count > 0)
            SelectedCombat = Combats[0];
        else if (SelectedCombat != null)
            LoadCombat(SelectedCombat.CombatId);
        LastRefreshAtUtc = DateTime.UtcNow;
        StatusMessage = "Список боев обновлен.";
    }

    private void LoadCombat(string combatId)
    {
        if (string.IsNullOrWhiteSpace(combatId)) return;
        var response = _api.CombatV1SnapshotFull(new Dictionary<string, object>
        {
            ["encounterId"] = combatId,
            ["includeParticipants"] = true,
            ["includeTurns"] = true,
            ["includeRounds"] = true,
            ["includeActions"] = true,
            ["includeLogs"] = true,
            ["limitActions"] = 100,
            ["limitLogs"] = 100
        });
        if (!EnsureOk(response, "Не удалось загрузить бой.")) return;
        ApplyCombatPayload(response.Payload);
        RefreshCombatMap();
        LastRefreshAtUtc = DateTime.UtcNow;
    }

    private void CreateCombat()
    {
        var response = _api.CombatV1EncounterCreate(new Dictionary<string, object>
        {
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId,
            ["name"] = string.IsNullOrWhiteSpace(NewCombatName) ? "Бой" : NewCombatName,
            ["ruleSetId"] = "fantasy_nri_default",
            ["requestId"] = $"admin-combat-create-{Guid.NewGuid():N}"
        });
        if (!EnsureOk(response, "Не удалось создать бой.")) return;
        var encounterId = Str(Get(response.Payload, "encounterId"));
        Refresh();
        if (!string.IsNullOrWhiteSpace(encounterId)) LoadCombat(encounterId);
        StatusMessage = "Бой создан.";
    }

    private void AddParticipant()
    {
        if (SelectedCombat == null) return;
        var response = _api.CombatV1ParticipantAdd(new Dictionary<string, object>
        {
            ["encounterId"] = SelectedCombat.CombatId,
            ["displayName"] = string.IsNullOrWhiteSpace(NewParticipantName) ? "Участник" : NewParticipantName,
            ["participantType"] = NewParticipantType,
            ["teamId"] = NewParticipantTeam,
            ["isHidden"] = !string.Equals(NewParticipantVisibility, "player_visible", StringComparison.OrdinalIgnoreCase),
            ["isNpc"] = !string.Equals(NewParticipantType, "player_character", StringComparison.OrdinalIgnoreCase),
            ["initiative"] = 0,
            ["requestId"] = $"admin-participant-add-{Guid.NewGuid():N}"
        });
        if (!EnsureOk(response, "Не удалось добавить участника.")) return;
        LoadCombat(SelectedCombat.CombatId);
        StatusMessage = "Участник добавлен.";
    }

    private void UpdateParticipant()
    {
        if (SelectedParticipant == null) return;
        var response = _api.CombatAdminUpdateParticipant(new Dictionary<string, object>
        {
            ["participantId"] = SelectedParticipant.ParticipantId,
            ["displayName"] = SelectedParticipant.DisplayName,
            ["participantType"] = SelectedParticipant.ParticipantType,
            ["teamId"] = SelectedParticipant.TeamId,
            ["visibilityMode"] = SelectedVisibility,
            ["publicStateText"] = SelectedParticipant.PublicStateText,
            ["gmStateText"] = SelectedParticipant.GmStateText,
            ["publicNotes"] = SelectedParticipant.PublicNotes,
            ["gmNotes"] = SelectedParticipant.GmNotes
        });
        if (!EnsureOk(response, "Не удалось сохранить участника.")) return;
        ApplyCombatPayload(response.Payload);
        StatusMessage = "Участник сохранен.";
    }

    private void RemoveParticipant()
    {
        if (SelectedParticipant == null || SelectedCombat == null) return;
        var response = _api.CombatV1ParticipantRemove(new Dictionary<string, object>
        {
            ["encounterId"] = SelectedCombat.CombatId,
            ["participantId"] = SelectedParticipant.ParticipantId,
            ["reason"] = "Удалён GM из состава боя.",
            ["requestId"] = $"admin-participant-remove-{Guid.NewGuid():N}"
        });
        if (!EnsureOk(response, "Не удалось удалить участника.")) return;
        LoadCombat(SelectedCombat.CombatId);
        StatusMessage = "Участник удален.";
    }

    private void SetParticipantVisibility()
    {
        if (SelectedParticipant == null) return;
        var response = _api.CombatAdminSetParticipantVisibility(new Dictionary<string, object>
        {
            ["participantId"] = SelectedParticipant.ParticipantId,
            ["visibilityMode"] = SelectedVisibility
        });
        if (!EnsureOk(response, "Не удалось обновить видимость.")) return;
        ApplyCombatPayload(response.Payload);
    }

    private void LinkMapToken()
    {
        if (SelectedCombat == null || SelectedParticipant == null) return;
        var response = _api.CombatMapAdminLinkParticipantToken(new Dictionary<string, object>
        {
            ["combatId"] = SelectedCombat.CombatId,
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId,
            ["participantId"] = SelectedParticipant.ParticipantId,
            ["tokenId"] = SelectedMapTokenId
        });
        if (!EnsureOk(response, "Не удалось привязать токен карты.")) return;
        ApplyCombatMapPayload(response.Payload);
        RefreshCombatMap();
    }

    private void UnlinkMapToken()
    {
        if (SelectedCombat == null || SelectedParticipant == null) return;
        var response = _api.CombatMapAdminUnlinkParticipantToken(new Dictionary<string, object>
        {
            ["combatId"] = SelectedCombat.CombatId,
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId,
            ["participantId"] = SelectedParticipant.ParticipantId
        });
        if (!EnsureOk(response, "Не удалось отвязать токен карты.")) return;
        ApplyCombatMapPayload(response.Payload);
        RefreshCombatMap();
    }

    private void RefreshCombatMap()
    {
        if (SelectedCombat == null)
        {
            CombatMapStatusText = "Выберите бой для загрузки боевого слоя карты.";
            return;
        }

        var payload = new Dictionary<string, object>
        {
            ["combatId"] = SelectedCombat.CombatId,
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId
        };
        var overlay = _api.CombatMapAdminGetActiveSceneMapOverlay(payload);
        if (!EnsureOk(overlay, "Боевой слой карты недоступен.")) return;
        ApplyCombatMapPayload(overlay.Payload);

        var joinable = _api.CombatMapAdminListJoinableTokens(payload);
        if (EnsureOk(joinable, "Не удалось загрузить токены карты."))
            ApplyJoinableTokens(joinable.Payload);
    }

    private void AddSelectedTokenToCombat()
    {
        if (SelectedCombat == null || SelectedCombatMapToken == null) return;
        var response = _api.CombatMapAdminAddParticipantFromToken(new Dictionary<string, object>
        {
            ["combatId"] = SelectedCombat.CombatId,
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId,
            ["tokenId"] = SelectedCombatMapToken.TokenId
        });
        if (!EnsureOk(response, "Не удалось добавить участника из токена карты.")) return;
        ApplyCombatMapPayload(response.Payload);
        RefreshCombatMap();
    }

    private void LinkSelectedOverlayToken()
    {
        if (SelectedParticipant == null || SelectedCombatMapToken == null) return;
        var response = _api.CombatMapAdminLinkParticipantToken(new Dictionary<string, object>
        {
            ["participantId"] = SelectedParticipant.ParticipantId,
            ["combatId"] = SelectedCombat?.CombatId ?? string.Empty,
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId,
            ["tokenId"] = SelectedCombatMapToken.TokenId
        });
        if (!EnsureOk(response, "Не удалось привязать токен карты.")) return;
        ApplyCombatMapPayload(response.Payload);
        RefreshCombatMap();
    }

    private void UnlinkSelectedOverlayToken()
    {
        if (SelectedParticipant == null && SelectedCombatOverlayToken != null)
            SelectedParticipant = Participants.FirstOrDefault(x => string.Equals(x.ParticipantId, SelectedCombatOverlayToken.ParticipantId, StringComparison.OrdinalIgnoreCase));
        if (SelectedParticipant == null) return;
        var response = _api.CombatMapAdminUnlinkParticipantToken(new Dictionary<string, object>
        {
            ["participantId"] = SelectedParticipant.ParticipantId,
            ["combatId"] = SelectedCombat?.CombatId ?? string.Empty,
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId
        });
        if (!EnsureOk(response, "Не удалось отвязать токен карты.")) return;
        ApplyCombatMapPayload(response.Payload);
        RefreshCombatMap();
    }

    private void SyncVisibilityFromToken()
    {
        if (SelectedParticipant == null) return;
        var response = _api.CombatMapAdminSyncParticipantVisibilityFromToken(new Dictionary<string, object>
        {
            ["participantId"] = SelectedParticipant.ParticipantId,
            ["combatId"] = SelectedCombat?.CombatId ?? string.Empty,
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId
        });
        if (!EnsureOk(response, "Не удалось синхронизировать видимость с токеном.")) return;
        ApplyCombatMapPayload(response.Payload);
    }

    private void FocusSelectedToken()
    {
        if (SelectedParticipant == null) return;
        var response = _api.CombatMapAdminFocusParticipantToken(new Dictionary<string, object>
        {
            ["participantId"] = SelectedParticipant.ParticipantId,
            ["combatId"] = SelectedCombat?.CombatId ?? string.Empty,
            ["campaignId"] = CampaignId,
            ["sessionId"] = SessionId
        });
        if (!EnsureOk(response, "Не удалось сфокусировать токен.")) return;
        ApplyCombatMapPayload(response.Payload);
        StatusMessage = $"Фокус токена: {Str(Get(response.Payload, "focusedMapTokenId"), "нет токена")}";
    }
    private void RollInitiative() => RunCombatV1Command(_api.CombatV1InitiativeSort, new Dictionary<string, object>
    {
        ["sortMode"] = "descending_initiative_then_tiebreaker",
        ["requestId"] = $"admin-initiative-{Guid.NewGuid():N}"
    }, "Инициатива d20 упорядочена.");
    private void StartCombat()
    {
        if (SelectedCombat == null) return;
        var round = _api.CombatV1RoundStart(new Dictionary<string, object>
        {
            ["encounterId"] = SelectedCombat.CombatId,
            ["roundNumber"] = Math.Max(1, SelectedCombat.RoundNumber),
            ["requestId"] = $"admin-round-start-{Guid.NewGuid():N}"
        });
        if (!EnsureOk(round, "Не удалось начать раунд.")) return;
        ApplyCombatPayload(round.Payload);
        var activeParticipantId = Str(Get(round.Payload, "activeParticipantId"));
        var first = Participants.FirstOrDefault(x => string.Equals(x.ParticipantId, activeParticipantId, StringComparison.OrdinalIgnoreCase))
            ?? Participants.OrderByDescending(x => x.InitiativeRoll).FirstOrDefault();
        if (first == null) return;
        var turn = _api.CombatV1TurnStart(new Dictionary<string, object>
        {
            ["encounterId"] = SelectedCombat.CombatId,
            ["participantId"] = first.ParticipantId,
            ["requestId"] = $"admin-turn-start-{Guid.NewGuid():N}"
        });
        if (!EnsureOk(turn, "Не удалось начать первый ход.")) return;
        ApplyCombatPayload(turn.Payload);
        StatusMessage = "Бой начат.";
    }
    private void PauseCombat() => RunCombatCommand(_api.CombatAdminPause, "Бой поставлен на паузу.");
    private void ResumeCombat() => RunCombatCommand(_api.CombatAdminResume, "Бой продолжен.");
    private void NextTurn() => RunCombatV1Command(_api.CombatV1TurnNext, new Dictionary<string, object> { ["requestId"] = $"admin-next-{Guid.NewGuid():N}" }, "Следующий ход.");
    private void SkipTurn() => RunCombatV1Command(_api.CombatV1TurnSkip, new Dictionary<string, object> { ["reason"] = "Пропущено GM.", ["requestId"] = $"admin-skip-{Guid.NewGuid():N}" }, "Ход пропущен.");
    private void PreviousTurn() => RunCombatCommand(_api.CombatAdminPreviousTurn, "Предыдущий ход.");
    private void EndCombat() => RunCombatV1Command(_api.CombatV1EncounterEnd, new Dictionary<string, object> { ["reason"] = "Завершено GM.", ["requestId"] = $"admin-end-{Guid.NewGuid():N}" }, "Бой завершен.");

    private void RunCombatV1Command(Func<Dictionary<string, object>, ResponseEnvelope> command, Dictionary<string, object> payload, string success)
    {
        if (SelectedCombat == null) return;
        payload["encounterId"] = SelectedCombat.CombatId;
        var response = command(payload);
        if (!EnsureOk(response, success)) return;
        ApplyCombatPayload(response.Payload);
        StatusMessage = success;
    }

    private void AddLogEvent()
    {
        if (SelectedCombat == null) return;
        var response = _api.CombatAdminAddTurnEvent(new Dictionary<string, object>
        {
            ["combatId"] = SelectedCombat.CombatId,
            ["eventType"] = "gm.note",
            ["message"] = "GM событие боя",
            ["visibility"] = "gm_only"
        });
        if (!EnsureOk(response, "Не удалось добавить событие.")) return;
        ApplyCombatPayload(response.Payload);
    }

    private void RunCombatCommand(Func<Dictionary<string, object>, ResponseEnvelope> command, string success)
    {
        if (SelectedCombat == null) return;
        var response = command(new Dictionary<string, object> { ["combatId"] = SelectedCombat.CombatId });
        if (!EnsureOk(response, success)) return;
        ApplyCombatPayload(response.Payload);
        StatusMessage = success;
    }

    private void ApplyCombatPayload(Dictionary<string, object> payload)
    {
        var nested = Map(Get(payload, "snapshot"));
        if (nested != null) payload = nested;
        var combatMap = Map(Get(payload, "combat"));
        if (combatMap == null) combatMap = Map(Get(payload, "encounter"));
        if (combatMap != null)
        {
            var item = AdminCombatTrackerCombatItem.From(combatMap);
            var existing = Combats.FirstOrDefault(x => string.Equals(x.CombatId, item.CombatId, StringComparison.OrdinalIgnoreCase));
            if (existing == null) Combats.Insert(0, item);
            else existing.Apply(item);
            _selectedCombat = existing ?? item;
            Notify(nameof(SelectedCombat));
            Notify(nameof(CurrentRoundText));
            Notify(nameof(CurrentTurnText));
        }

        Participants.Clear();
        foreach (var map in Maps(Get(payload, "participants")))
            Participants.Add(AdminCombatTrackerParticipantItem.From(map));

        var activeParticipantId = combatMap == null ? string.Empty : Str(Get(combatMap, "activeParticipantId"));
        var orderedParticipants = Participants.OrderByDescending(x => x.InitiativeRoll).ThenBy(x => x.DisplayName).ToList();
        for (var index = 0; index < orderedParticipants.Count; index++)
        {
            orderedParticipants[index].InitiativeOrderIndex = index + 1;
            orderedParticipants[index].TurnStatus = string.Equals(orderedParticipants[index].ParticipantId, activeParticipantId, StringComparison.OrdinalIgnoreCase) ? "Текущий ход" : "Ожидает";
        }
        if (_selectedCombat != null)
        {
            _selectedCombat.CurrentParticipantName = Participants.FirstOrDefault(x => string.Equals(x.ParticipantId, activeParticipantId, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? "не назначен";
            _selectedCombat.CurrentParticipantId = activeParticipantId;
            Notify(nameof(CurrentRoundText));
            Notify(nameof(CurrentTurnText));
        }

        InitiativeOrder.Clear();
        foreach (var map in Maps(Get(payload, "initiativeOrder")).DefaultIfEmpty())
        {
            if (map == null) continue;
            InitiativeOrder.Add(AdminCombatTrackerParticipantItem.From(map));
        }
        if (InitiativeOrder.Count == 0)
        {
            foreach (var participant in Participants.OrderBy(x => x.InitiativeOrderIndex))
                InitiativeOrder.Add(participant);
        }

        var rawLogs = Get(payload, "recentLogs") ?? Get(payload, "logs");
        if (rawLogs != null)
        {
            CombatLog.Clear();
            foreach (var item in Maps(rawLogs).Select(AdminCombatTrackerLogItem.From).OrderByDescending(x => x.CreatedAtUtc))
                CombatLog.Add(item);
        }

        if (SelectedParticipant != null)
            SelectedParticipant = Participants.FirstOrDefault(x => string.Equals(x.ParticipantId, SelectedParticipant.ParticipantId, StringComparison.OrdinalIgnoreCase));
        LastRefreshAtUtc = DateTime.UtcNow;
    }

    private void ApplyCombatMapPayload(Dictionary<string, object> payload)
    {
        ApplyCombatPayload(payload);
        var sceneMap = Map(Get(payload, "sceneMap"));
        ActiveSceneMapText = sceneMap == null
            ? "Активная карта сцены не выбрана."
            : $"{Str(Get(sceneMap, "name"), Str(Get(sceneMap, "mapId")))} | {Str(Get(sceneMap, "widthMeters"))}x{Str(Get(sceneMap, "heightMeters"))} м | сетка {Str(Get(sceneMap, "gridSizeMeters"))} м";
        _combatMapWidthMeters = sceneMap == null ? 1d : Dbl(Get(sceneMap, "widthMeters"), 1d);
        _combatMapHeightMeters = sceneMap == null ? 1d : Dbl(Get(sceneMap, "heightMeters"), 1d);
        _combatMapGridMeters = sceneMap == null ? 5d : Math.Max(1d, Dbl(Get(sceneMap, "gridSizeMeters"), 5d));

        CombatMapTilePatches.Clear();
        foreach (var map in Maps(Get(payload, "tilePatches")))
            CombatMapTilePatches.Add(SceneMapTilePatchUiItem.From(map));

        CombatMapAssetInstances.Clear();
        foreach (var map in Maps(Get(payload, "assetInstances")))
            CombatMapAssetInstances.Add(SceneMapAssetInstanceUiItem.From(map));

        CombatMapOverlayTokens.Clear();
        foreach (var map in Maps(Get(payload, "combatTokens")))
            CombatMapOverlayTokens.Add(AdminCombatMapTokenItem.From(map, overlay: true));
        RebuildCombatMapCanvas();

        CombatMapWarnings.Clear();
        foreach (var raw in ToEnumerable(Get(payload, "warnings")))
        {
            var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(value)) CombatMapWarnings.Add(value);
        }

        CombatMapStatusText = $"Боевой слой карты: связанных токенов {CombatMapOverlayTokens.Count}, предупреждений {CombatMapWarnings.Count}.";
        LastRefreshAtUtc = DateTime.UtcNow;
    }

    private void RebuildCombatMapCanvas()
    {
        var projection = MapCanvasProjectionHelper.Calculate(_combatMapWidthMeters, _combatMapHeightMeters, 560, 320);
        CombatMapCanvasWidth = projection.CanvasWidth;
        CombatMapCanvasHeight = projection.CanvasHeight;
        CombatMapScaleText = $"Координаты в метрах; 1 м = {projection.Scale:0.###} пикс.; сетка {_combatMapGridMeters:0.##} м";

        CombatMapGridLines.Clear();
        var step = Math.Max(1d, _combatMapGridMeters);
        for (var x = 0d; x <= _combatMapWidthMeters + 0.001d; x += step)
        {
            var px = MapCanvasProjectionHelper.ToPixel(x, projection.Scale);
            CombatMapGridLines.Add(new MapGridLineUiItem { X1 = px, Y1 = 0, X2 = px, Y2 = CombatMapCanvasHeight });
        }
        for (var y = 0d; y <= _combatMapHeightMeters + 0.001d; y += step)
        {
            var py = MapCanvasProjectionHelper.ToPixel(y, projection.Scale);
            CombatMapGridLines.Add(new MapGridLineUiItem { X1 = 0, Y1 = py, X2 = CombatMapCanvasWidth, Y2 = py });
        }

        foreach (var patch in CombatMapTilePatches)
            patch.ApplyScale(projection.Scale);
        foreach (var asset in CombatMapAssetInstances)
            asset.ApplyScale(projection.Scale);
        foreach (var token in CombatMapOverlayTokens)
            token.ApplyScale(projection.Scale);
    }

    private void ApplyJoinableTokens(Dictionary<string, object> payload)
    {
        CombatMapJoinableTokens.Clear();
        foreach (var map in Maps(Get(payload, "tokens")))
            CombatMapJoinableTokens.Add(AdminCombatMapTokenItem.From(map, overlay: false));
        CombatMapStatusText = $"{CombatMapStatusText} Доступно для добавления: {CombatMapJoinableTokens.Count}.";
    }
    private void Run(string action, Action body)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            ClientLogService.Instance.Info($"admin.combat.tracker.{action}.start");
            body();
            ClientLogService.Instance.Info($"admin.combat.tracker.{action}.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Error($"admin.combat.tracker.{action}.error {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool EnsureOk(ResponseEnvelope response, string fallbackError)
    {
        if (response.Status == ResponseStatus.Ok) return true;
        ErrorMessage = string.IsNullOrWhiteSpace(response.Message) ? fallbackError : response.Message;
        StatusMessage = ErrorMessage;
        return false;
    }

    internal static object? Get(Dictionary<string, object>? map, string key)
        => map != null && map.TryGetValue(key, out var value) ? value : null;

    private static Dictionary<string, object>? Map(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }
        return null;
    }

    private static IEnumerable<Dictionary<string, object>> Maps(object? raw)
    {
        if (raw is IEnumerable enumerable && raw is not string)
        {
            foreach (var item in enumerable)
            {
                var map = Map(item);
                if (map != null) yield return map;
            }
        }
    }

    private static IEnumerable ToEnumerable(object? raw)
    {
        if (raw is IEnumerable enumerable && raw is not string) return enumerable;
        return Array.Empty<object>();
    }

    internal static string Str(object? raw, string fallback = "") => string.IsNullOrWhiteSpace(Convert.ToString(raw, CultureInfo.InvariantCulture)) ? fallback : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? fallback;
    internal static int Int(object? raw, int fallback = 0) => int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    internal static double Dbl(object? raw, double fallback = 0d) => double.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    internal static bool Bool(object? raw) => bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var value) && value;
    internal static DateTime Date(object? raw) => DateTime.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value.ToUniversalTime() : DateTime.MinValue;

    internal static string DisplayType(string type)
    {
        return (type ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "player_character" => "Персонаж",
            "npc" => "NPC",
            "companion" => "Компаньон",
            "enemy" => "Враг",
            "neutral" => "Нейтральный",
            "creature" => "Существо",
            "vehicle" => "Техника",
            _ => "Другое"
        };
    }

    internal static string DisplayVisibility(string visibility)
    {
        return (visibility ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "player_visible" => "Виден игрокам",
            "gm_only" => "Только GM",
            "hidden" => "Скрыт",
            _ => "Скрыт"
        };
    }

    private static string DisplayHitResult(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "critical_hit" => "критическое попадание",
        "hit" => "попадание",
        "miss" => "промах",
        "fumble" => "критическая неудача",
        _ => "результат получен"
    };

    private static string DisplayDegree(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "exceptional" => "исключительный успех",
        "strong" => "сильный успех",
        "ordinary" => "обычный успех",
        "failure" => "неудача",
        _ => "степень не определена"
    };

    private static string DisplayProtectionZone(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "front" => "лобовая броня",
        "side" => "бортовая броня",
        "rear" => "кормовая броня",
        _ => "корпус / торс"
    };
}

public sealed class AdminCombatSkillOptionVm
{
    public string SkillCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DefaultAttribute { get; set; } = string.Empty;
    public string DefaultSubAttribute { get; set; } = string.Empty;
    public int Rank { get; set; }
    public string MasteryBand { get; set; } = string.Empty;
    public int ProficiencyBonus { get; set; }
    public string Summary => $"{Name} · ранг {Rank} · {MasteryBand} ({ProficiencyBonus:+0;-0;0})";
    public override string ToString() => Summary;
}

public sealed class AdminCombatWeaponOptionVm
{
    public string ItemInstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public override string ToString() => DisplayName;
}

public sealed class AdminCombatFacingOptionVm
{
    public AdminCombatFacingOptionVm(string code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
    }

    public string Code { get; }
    public string DisplayName { get; }
    public override string ToString() => DisplayName;
}

public sealed class AdminCombatTrackerCombatItem : ViewModelBase
{
    public string CombatId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int CurrentTurnIndex { get; set; }
    public string CurrentParticipantId { get; set; } = string.Empty;
    public string CurrentParticipantName { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public string Summary => $"{Name} · {DisplayStatus} · раунд {RoundNumber}";
    public override string ToString() => Summary;
    public string DisplayStatus => Status switch
    {
        "setup" => "Подготовка",
        "draft" => "Подготовка",
        "active" => "Активен",
        "paused" => "Пауза",
        "ended" => "Завершен",
        "archived" => "Архив",
        _ => Status
    };

    public void Apply(AdminCombatTrackerCombatItem other)
    {
        CombatId = other.CombatId;
        Name = other.Name;
        Status = other.Status;
        RoundNumber = other.RoundNumber;
        CurrentTurnIndex = other.CurrentTurnIndex;
        CurrentParticipantId = other.CurrentParticipantId;
        CurrentParticipantName = other.CurrentParticipantName;
        ParticipantCount = other.ParticipantCount;
        UpdatedAtUtc = other.UpdatedAtUtc;
        Notify(string.Empty);
    }

    public static AdminCombatTrackerCombatItem From(Dictionary<string, object> map) => new()
    {
        CombatId = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "combatId"), AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "id"))),
        Name = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "name"), "Бой"),
        Status = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "status"), "draft"),
        RoundNumber = AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "roundNumber")),
        CurrentTurnIndex = AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "currentTurnIndex"), AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "activeTurnIndex"), -1)),
        CurrentParticipantId = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "currentParticipantId"), AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "activeParticipantId"))),
        CurrentParticipantName = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "currentParticipantName"), "нет активного участника"),
        ParticipantCount = AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "participantCount")),
        UpdatedAtUtc = AdminCombatReadOnlyViewModel.Date(AdminCombatReadOnlyViewModel.Get(map, "updatedAtUtc"))
    };
}

public sealed class AdminCombatTrackerParticipantItem : ViewModelBase
{
    public string ParticipantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string ControllerUserId { get; set; } = string.Empty;
    public int InitiativeRoll { get; set; }
    public int InitiativeOrderIndex { get; set; }
    public string TurnStatus { get; set; } = string.Empty;
    public int StandardActions { get; set; }
    public int MinorActions { get; set; }
    public bool ReactionAvailable { get; set; }
    public bool Natural20BonusTurn { get; set; }
    public bool Natural1FirstTurnPenalty { get; set; }
    public string VisibilityMode { get; set; } = string.Empty;
    public string PublicStateText { get; set; } = string.Empty;
    public string GmStateText { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GmNotes { get; set; } = string.Empty;
    public string MapTokenId { get; set; } = string.Empty;
    public string MapTokenDisplayName { get; set; } = string.Empty;
    public string MapTokenVisibility { get; set; } = string.Empty;

    public string TypeLabel => AdminCombatReadOnlyViewModel.DisplayType(ParticipantType);
    public string VisibilityLabel => AdminCombatReadOnlyViewModel.DisplayVisibility(VisibilityMode);
    public string ActionSummary => $"Половины действия: {StandardActions}/2; реакция {(ReactionAvailable ? "доступна" : "потрачена")}";
    public string InitiativeSummary => InitiativeRoll <= 0 ? "-" : Natural20BonusTurn ? $"{InitiativeRoll} + доп. ход" : Natural1FirstTurnPenalty ? $"{InitiativeRoll} / ограничен" : InitiativeRoll.ToString(CultureInfo.InvariantCulture);
    public string TokenSummary => string.IsNullOrWhiteSpace(MapTokenId) ? "Без токена" : $"{MapTokenDisplayName} ({MapTokenId})";
    public override string ToString() => DisplayName;

    public static AdminCombatTrackerParticipantItem From(Dictionary<string, object> map) => new()
    {
        ParticipantId = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "participantId"), AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "id"))),
        DisplayName = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "displayName"), "Участник"),
        ParticipantType = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "participantType"), "custom"),
        TeamId = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "teamId"), "neutral"),
        CharacterId = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "characterId")),
        ControllerUserId = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "controllerUserId")),
        InitiativeRoll = AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "initiativeRoll"), AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "initiative"))),
        InitiativeOrderIndex = AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "initiativeOrderIndex"), 9999),
        TurnStatus = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "turnStatus"), "waiting"),
        StandardActions = AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "standardActions"), AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "actionPoints"), 2)),
        MinorActions = AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "minorActions"), AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "minorActionPoints"))),
        ReactionAvailable = AdminCombatReadOnlyViewModel.Get(map, "reactionAvailable") != null
            ? AdminCombatReadOnlyViewModel.Bool(AdminCombatReadOnlyViewModel.Get(map, "reactionAvailable"))
            : AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "reactionCount")) < AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "reactionLimit"), 1),
        Natural20BonusTurn = AdminCombatReadOnlyViewModel.Bool(AdminCombatReadOnlyViewModel.Get(map, "natural20BonusTurn")),
        Natural1FirstTurnPenalty = AdminCombatReadOnlyViewModel.Bool(AdminCombatReadOnlyViewModel.Get(map, "natural1FirstTurnPenalty")),
        VisibilityMode = AdminCombatReadOnlyViewModel.Get(map, "isHidden") != null
            ? (AdminCombatReadOnlyViewModel.Bool(AdminCombatReadOnlyViewModel.Get(map, "isHidden")) ? "hidden" : "player_visible")
            : AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "visibilityMode"), "hidden"),
        PublicStateText = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "publicStateText")),
        GmStateText = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "gmStateText")),
        PublicNotes = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "publicNotes")),
        GmNotes = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "gmNotes")),
        MapTokenId = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "mapTokenId")),
        MapTokenDisplayName = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "mapTokenDisplayName")),
        MapTokenVisibility = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "mapTokenVisibility"), "hidden")
    };
}

public sealed class AdminCombatMapTokenItem : ViewModelBase
{
    public string TokenId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantName { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public string LinkStatus { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = string.Empty;
    public string TokenVisibility { get; set; } = string.Empty;
    public string BadgeText { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double SizeMeters { get; set; } = 1d;
    public double RadiusMeters { get; set; }
    public double PixelX { get; set; }
    public double PixelY { get; set; }
    public double PixelLeft { get; set; }
    public double PixelTop { get; set; }
    public double PixelDiameter { get; set; } = 20d;
    public bool IsCurrentTurn { get; set; }
    public bool CanJoinCombat { get; set; }
    public string CombatVisibility => TokenVisibility switch
    {
        "PlayerVisible" => "player_visible",
        "GmOnly" => "gm_only",
        _ => "hidden"
    };
    public string PositionText => $"{X:0.##}; {Y:0.##} м";
    public string Summary => string.IsNullOrWhiteSpace(ParticipantId)
        ? $"{DisplayName} | {TokenType} | {PositionText}"
        : $"{ParticipantName} -> {DisplayName} | {LinkStatus} | {PositionText}";
    public string TurnBadge => IsCurrentTurn ? "Текущий ход" : string.Empty;
    public string CanvasLabel => string.IsNullOrWhiteSpace(ParticipantName) ? DisplayName : ParticipantName;
    public string CanvasBadge => FirstNonEmpty(BadgeText, TurnBadge, AdminCombatReadOnlyViewModel.DisplayVisibility(VisibilityMode));
    public string SideStatusBadge => $"{AdminCombatReadOnlyViewModel.DisplayType(TokenType)} / {AdminCombatReadOnlyViewModel.DisplayVisibility(VisibilityMode)}";

    public void ApplyScale(double scale)
    {
        PixelX = MapCanvasProjectionHelper.ToPixel(X, scale);
        PixelY = MapCanvasProjectionHelper.ToPixel(Y, scale);
        var meters = RadiusMeters > 0 ? RadiusMeters * 2d : Math.Max(1d, SizeMeters);
        PixelDiameter = Math.Max(18d, MapCanvasProjectionHelper.ToPixel(meters, scale));
        PixelLeft = PixelX - (PixelDiameter / 2d);
        PixelTop = PixelY - (PixelDiameter / 2d);
        Notify(nameof(PixelX));
        Notify(nameof(PixelY));
        Notify(nameof(PixelLeft));
        Notify(nameof(PixelTop));
        Notify(nameof(PixelDiameter));
        Notify(nameof(CanvasLabel));
        Notify(nameof(CanvasBadge));
        Notify(nameof(SideStatusBadge));
    }

    public static AdminCombatMapTokenItem From(Dictionary<string, object> map, bool overlay)
    {
        return new AdminCombatMapTokenItem
        {
            TokenId = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "mapTokenId"), AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "tokenId"))),
            ParticipantId = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "participantId")),
            DisplayName = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "mapTokenDisplayName"), AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "displayName"), AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "tokenId")))),
            ParticipantName = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "participantName"), AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "displayName"))),
            TokenType = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "tokenType")),
            LinkStatus = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "linkStatus"), overlay ? "linked" : "joinable"),
            VisibilityMode = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "visibilityMode")),
            TokenVisibility = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "tokenVisibility"), AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "visibility"))),
            BadgeText = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "mapBadgeText")),
            X = double.TryParse(Convert.ToString(AdminCombatReadOnlyViewModel.Get(map, "x"), CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : 0d,
            Y = double.TryParse(Convert.ToString(AdminCombatReadOnlyViewModel.Get(map, "y"), CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var y) ? y : 0d,
            SizeMeters = AdminCombatReadOnlyViewModel.Dbl(AdminCombatReadOnlyViewModel.Get(map, "size"), 1d),
            RadiusMeters = AdminCombatReadOnlyViewModel.Dbl(AdminCombatReadOnlyViewModel.Get(map, "radius"), 0d),
            IsCurrentTurn = AdminCombatReadOnlyViewModel.Bool(AdminCombatReadOnlyViewModel.Get(map, "isCurrentTurn")),
            CanJoinCombat = AdminCombatReadOnlyViewModel.Bool(AdminCombatReadOnlyViewModel.Get(map, "canJoinCombat"))
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value)) return value;
        return string.Empty;
    }
}

public sealed class AdminCombatTrackerLogItem
{
    public long SequenceNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public string DisplayText => $"Раунд {RoundNumber}, ход {TurnIndex}: {Message}";
    public override string ToString() => DisplayText;

    public static AdminCombatTrackerLogItem From(Dictionary<string, object> map) => new()
    {
        SequenceNumber = long.TryParse(Convert.ToString(AdminCombatReadOnlyViewModel.Get(map, "sequenceNumber"), CultureInfo.InvariantCulture), out var seq) ? seq : 0L,
        CreatedAtUtc = AdminCombatReadOnlyViewModel.Date(AdminCombatReadOnlyViewModel.Get(map, "createdAtUtc")),
        EventType = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "eventType")),
        RoundNumber = AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "roundNumber")),
        TurnIndex = AdminCombatReadOnlyViewModel.Int(AdminCombatReadOnlyViewModel.Get(map, "turnIndex"), -1),
        Message = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "message")),
        Visibility = AdminCombatReadOnlyViewModel.Str(AdminCombatReadOnlyViewModel.Get(map, "visibility"))
    };
}



