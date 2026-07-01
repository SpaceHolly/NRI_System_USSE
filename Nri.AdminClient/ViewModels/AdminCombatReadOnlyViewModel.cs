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

namespace Nri.AdminClient.ViewModels;

public sealed class AdminCombatReadOnlyViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _encounterId = string.Empty;
    private string _encounterName = "Бой не выбран";
    private string _encounterStatus = "unknown";
    private int _roundNumber;
    private int _activeTurnIndex;
    private string _activeParticipantId = string.Empty;
    private string _activeParticipantName = "нет активного участника";
    private string _currentTurnSummary = "Ход не загружен";
    private string _currentRoundSummary = "Раунд не загружен";
    private string _diagnosticsSummary = "Диагностика не загружена";
    private string _replayStatus = "Replay не загружен";
    private string _errorMessage = string.Empty;
    private string _warningMessage = string.Empty;
    private bool _isLoading;
    private bool _isWriteBusy;
    private bool _areCombatReadFlagsEnabled;
    private bool _canUseCombatWriteEndpoints;
    private bool _canUseTurnEngine;
    private bool _canUseAttackRoll;
    private bool _canUseDefensePreview;
    private bool _canUseDamage;
    private bool _canUseConditions;
    private bool _canUseWeaponAttack;
    private bool _canUseFateHook;
    private DateTime _lastRefreshAtUtc;
    private CombatParticipantUiItem? _selectedParticipant;
    private CombatParticipantUiItem? _selectedTargetParticipant;
    private string _writeStatusMessage = string.Empty;
    private string _campaignId = string.Empty;
    private string _sessionId = string.Empty;
    private string _ruleSetId = string.Empty;
    private string _newEncounterName = "New combat encounter";
    private string _newParticipantDisplayName = string.Empty;
    private string _newParticipantCharacterId = string.Empty;
    private string _newParticipantTeamId = "team-a";
    private string _newParticipantType = "npc";
    private int _newParticipantInitiative;
    private int _vitalsMaxHealth = 20;
    private int _vitalsCurrentHealth = 20;
    private int _vitalsTemporaryHealth;
    private int _vitalsMaxMorale;
    private int _vitalsCurrentMorale;
    private int _attackBonus;
    private int _coverModifier;
    private int _situationalModifier;
    private bool _spendActionPoint;
    private string _selectedWeaponDefinitionId = string.Empty;
    private string _selectedAmmoDefinitionId = string.Empty;
    private int _damageAmount = 1;
    private int _damageOverride;
    private string _damageType = "physical";
    private bool _autoApplyDamage;
    private string _selectedConditionDefinitionId = string.Empty;
    private string _selectedConditionInstanceId = string.Empty;
    private int _conditionStackCount = 1;
    private string _conditionDurationMode = "until_removed";
    private int _conditionDurationRounds;
    private string _fateRollContext = "attack_roll";
    private int _fateBaseRoll = 10;
    private string _fateDiceExpression = "1d20";
    private string _lastRulesResultSummary = string.Empty;

    public AdminCombatReadOnlyViewModel(CommandApi api)
    {
        _api = api;
        RefreshSnapshotCommand = new RelayCommand(RefreshSnapshot);
        RefreshLogsCommand = new RelayCommand(RefreshLogs);
        RefreshDiagnosticsCommand = new RelayCommand(RefreshDiagnostics);
        RefreshReplayCommand = new RelayCommand(RefreshReplay);
        RefreshFlagsCommand = new RelayCommand(RefreshFeatureFlags);
        ClearErrorCommand = new RelayCommand(() => { ErrorMessage = string.Empty; WarningMessage = string.Empty; });
        SelectParticipantCommand = new RelayCommand<CombatParticipantUiItem>(item => SelectedParticipant = item);
        CreateEncounterCommand = new RelayCommand(CreateEncounter);
        EndEncounterCommand = new RelayCommand(EndEncounter);
        CancelEncounterCommand = new RelayCommand(CancelEncounter);
        AddParticipantCommand = new RelayCommand(AddParticipant);
        RemoveParticipantCommand = new RelayCommand(RemoveParticipant);
        SetVitalsCommand = new RelayCommand(SetVitals);
        SortInitiativeCommand = new RelayCommand(SortInitiative);
        StartRoundCommand = new RelayCommand(StartRound);
        StartTurnCommand = new RelayCommand(StartTurn);
        EndTurnCommand = new RelayCommand(EndTurn);
        NextTurnCommand = new RelayCommand(NextTurn);
        NextRoundCommand = new RelayCommand(NextRound);
        SkipTurnCommand = new RelayCommand(SkipTurn);
        DelayTurnCommand = new RelayCommand(DelayTurn);
        AttackRollCommand = new RelayCommand(AttackRoll);
        DefensePreviewCommand = new RelayCommand(DefensePreview);
        ApplyDamageCommand = new RelayCommand(ApplyDamage);
        ApplyConditionCommand = new RelayCommand(ApplyCondition);
        RemoveConditionCommand = new RelayCommand(RemoveCondition);
        WeaponAttackResolveCommand = new RelayCommand(WeaponAttackResolve);
        FatePreviewCommand = new RelayCommand(FatePreview);
    }

    public string EncounterId { get => _encounterId; set { if (_encounterId != value) { _encounterId = value; Notify(); NotifyCombatCommandState(); } } }
    public string EncounterName { get => _encounterName; private set { if (_encounterName != value) { _encounterName = value; Notify(); } } }
    public string EncounterStatus { get => _encounterStatus; private set { if (_encounterStatus != value) { _encounterStatus = value; Notify(); } } }
    public int RoundNumber { get => _roundNumber; private set { if (_roundNumber != value) { _roundNumber = value; Notify(); } } }
    public int ActiveTurnIndex { get => _activeTurnIndex; private set { if (_activeTurnIndex != value) { _activeTurnIndex = value; Notify(); } } }
    public string ActiveParticipantId { get => _activeParticipantId; private set { if (_activeParticipantId != value) { _activeParticipantId = value; Notify(); } } }
    public string ActiveParticipantName { get => _activeParticipantName; private set { if (_activeParticipantName != value) { _activeParticipantName = value; Notify(); } } }
    public string CurrentTurnSummary { get => _currentTurnSummary; private set { if (_currentTurnSummary != value) { _currentTurnSummary = value; Notify(); } } }
    public string CurrentRoundSummary { get => _currentRoundSummary; private set { if (_currentRoundSummary != value) { _currentRoundSummary = value; Notify(); } } }
    public string DiagnosticsSummary { get => _diagnosticsSummary; private set { if (_diagnosticsSummary != value) { _diagnosticsSummary = value; Notify(); } } }
    public string ReplayStatus { get => _replayStatus; private set { if (_replayStatus != value) { _replayStatus = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public string WarningMessage { get => _warningMessage; private set { if (_warningMessage != value) { _warningMessage = value; Notify(); Notify(nameof(HasWarning)); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); Notify(nameof(CanRefresh)); } } }
    public bool IsWriteBusy { get => _isWriteBusy; private set { if (_isWriteBusy != value) { _isWriteBusy = value; Notify(); NotifyCombatCommandState(); } } }
    public bool CanRefresh => !IsLoading;
    public bool AreCombatReadFlagsEnabled { get => _areCombatReadFlagsEnabled; private set { if (_areCombatReadFlagsEnabled != value) { _areCombatReadFlagsEnabled = value; Notify(); } } }
    public bool CanUseCombatWriteEndpoints { get => _canUseCombatWriteEndpoints; private set { if (_canUseCombatWriteEndpoints != value) { _canUseCombatWriteEndpoints = value; Notify(); NotifyCombatCommandState(); } } }
    public bool CanUseTurnEngine { get => _canUseTurnEngine; private set { if (_canUseTurnEngine != value) { _canUseTurnEngine = value; Notify(); NotifyCombatCommandState(); } } }
    public bool CanUseAttackRoll { get => _canUseAttackRoll; private set { if (_canUseAttackRoll != value) { _canUseAttackRoll = value; Notify(); NotifyCombatCommandState(); } } }
    public bool CanUseDefensePreview { get => _canUseDefensePreview; private set { if (_canUseDefensePreview != value) { _canUseDefensePreview = value; Notify(); NotifyCombatCommandState(); } } }
    public bool CanUseDamage { get => _canUseDamage; private set { if (_canUseDamage != value) { _canUseDamage = value; Notify(); NotifyCombatCommandState(); } } }
    public bool CanUseConditions { get => _canUseConditions; private set { if (_canUseConditions != value) { _canUseConditions = value; Notify(); NotifyCombatCommandState(); } } }
    public bool CanUseWeaponAttack { get => _canUseWeaponAttack; private set { if (_canUseWeaponAttack != value) { _canUseWeaponAttack = value; Notify(); NotifyCombatCommandState(); } } }
    public bool CanUseFateHook { get => _canUseFateHook; private set { if (_canUseFateHook != value) { _canUseFateHook = value; Notify(); NotifyCombatCommandState(); } } }
    public bool CanCreateEncounter => CanUseCombatWriteEndpoints && !IsWriteBusy;
    public bool CanRunEncounterCommand => CanUseCombatWriteEndpoints && !IsWriteBusy && !string.IsNullOrWhiteSpace(EncounterId);
    public bool CanRunSelectedParticipantCommand => CanRunEncounterCommand && SelectedParticipant != null;
    public bool CanRunTurnCommand => CanUseTurnEngine && !IsWriteBusy && !string.IsNullOrWhiteSpace(EncounterId);
    public bool CanRunSelectedTurnCommand => CanRunTurnCommand && SelectedParticipant != null;
    public bool CanRunTargetCommand => !IsWriteBusy && !string.IsNullOrWhiteSpace(EncounterId) && SelectedTargetParticipant != null;
    public bool CanRunAttackCommand => CanUseAttackRoll && CanRunTargetCommand;
    public bool CanRunDefensePreviewCommand => CanUseDefensePreview && CanRunTargetCommand;
    public bool CanRunDamageCommand => CanUseDamage && CanRunTargetCommand;
    public bool CanRunVitalsCommand => CanUseDamage && CanRunSelectedParticipantCommand;
    public bool CanRunConditionCommand => CanUseConditions && CanRunTargetCommand;
    public bool CanRunWeaponAttackCommand => CanUseWeaponAttack && CanRunTargetCommand;
    public bool CanRunFatePreviewCommand => CanUseFateHook && !IsWriteBusy;
    public DateTime LastRefreshAtUtc { get => _lastRefreshAtUtc; private set { if (_lastRefreshAtUtc != value) { _lastRefreshAtUtc = value; Notify(); Notify(nameof(LastRefreshText)); } } }
    public string LastRefreshText => LastRefreshAtUtc == default ? "ещё не обновлялось" : LastRefreshAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public int ParticipantsCount => Participants.Count;
    public int LogsCount => RecentLogs.Count;
    public int ReplayCount => RecentReplayEvents.Count;

    public CombatParticipantUiItem? SelectedParticipant
    {
        get => _selectedParticipant;
        set
        {
            if (_selectedParticipant != value)
            {
                _selectedParticipant = value;
                Notify();
                Notify(nameof(SelectedParticipantSummary));
                if (SelectedTargetParticipant == null && value != null) SelectedTargetParticipant = Participants.FirstOrDefault(item => item.Id != value.Id) ?? value;
                VitalsMaxHealth = value?.MaxHealth > 0 ? value.MaxHealth : VitalsMaxHealth;
                VitalsCurrentHealth = value?.CurrentHealth > 0 ? value.CurrentHealth : VitalsCurrentHealth;
                VitalsTemporaryHealth = value?.TemporaryHealth ?? VitalsTemporaryHealth;
                VitalsMaxMorale = value?.MaxMorale ?? VitalsMaxMorale;
                VitalsCurrentMorale = value?.CurrentMorale ?? VitalsCurrentMorale;
                NotifyCombatCommandState();
            }
        }
    }

    public CombatParticipantUiItem? SelectedTargetParticipant
    {
        get => _selectedTargetParticipant;
        set
        {
            if (_selectedTargetParticipant != value)
            {
                _selectedTargetParticipant = value;
                Notify();
                Notify(nameof(SelectedTargetSummary));
                NotifyCombatCommandState();
            }
        }
    }

    public string SelectedParticipantSummary => SelectedParticipant == null
        ? "Выберите участника боя."
        : $"{SelectedParticipant.DisplayName} • {SelectedParticipant.HitPointsText} • {SelectedParticipant.ConditionsText}";

    public string SelectedTargetSummary => SelectedTargetParticipant == null
        ? "Target participant is not selected."
        : $"{SelectedTargetParticipant.DisplayName} - {SelectedTargetParticipant.HitPointsText}";

    public string WriteStatusMessage { get => _writeStatusMessage; private set { if (_writeStatusMessage != value) { _writeStatusMessage = value; Notify(); } } }
    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string SessionId { get => _sessionId; set { if (_sessionId != value) { _sessionId = value; Notify(); } } }
    public string RuleSetId { get => _ruleSetId; set { if (_ruleSetId != value) { _ruleSetId = value; Notify(); } } }
    public string NewEncounterName { get => _newEncounterName; set { if (_newEncounterName != value) { _newEncounterName = value; Notify(); } } }
    public string NewParticipantDisplayName { get => _newParticipantDisplayName; set { if (_newParticipantDisplayName != value) { _newParticipantDisplayName = value; Notify(); } } }
    public string NewParticipantCharacterId { get => _newParticipantCharacterId; set { if (_newParticipantCharacterId != value) { _newParticipantCharacterId = value; Notify(); } } }
    public string NewParticipantTeamId { get => _newParticipantTeamId; set { if (_newParticipantTeamId != value) { _newParticipantTeamId = value; Notify(); } } }
    public string NewParticipantType { get => _newParticipantType; set { if (_newParticipantType != value) { _newParticipantType = value; Notify(); } } }
    public int NewParticipantInitiative { get => _newParticipantInitiative; set { if (_newParticipantInitiative != value) { _newParticipantInitiative = value; Notify(); } } }
    public int VitalsMaxHealth { get => _vitalsMaxHealth; set { if (_vitalsMaxHealth != value) { _vitalsMaxHealth = value; Notify(); } } }
    public int VitalsCurrentHealth { get => _vitalsCurrentHealth; set { if (_vitalsCurrentHealth != value) { _vitalsCurrentHealth = value; Notify(); } } }
    public int VitalsTemporaryHealth { get => _vitalsTemporaryHealth; set { if (_vitalsTemporaryHealth != value) { _vitalsTemporaryHealth = value; Notify(); } } }
    public int VitalsMaxMorale { get => _vitalsMaxMorale; set { if (_vitalsMaxMorale != value) { _vitalsMaxMorale = value; Notify(); } } }
    public int VitalsCurrentMorale { get => _vitalsCurrentMorale; set { if (_vitalsCurrentMorale != value) { _vitalsCurrentMorale = value; Notify(); } } }
    public int AttackBonus { get => _attackBonus; set { if (_attackBonus != value) { _attackBonus = value; Notify(); } } }
    public int CoverModifier { get => _coverModifier; set { if (_coverModifier != value) { _coverModifier = value; Notify(); } } }
    public int SituationalModifier { get => _situationalModifier; set { if (_situationalModifier != value) { _situationalModifier = value; Notify(); } } }
    public bool SpendActionPoint { get => _spendActionPoint; set { if (_spendActionPoint != value) { _spendActionPoint = value; Notify(); } } }
    public string SelectedWeaponDefinitionId { get => _selectedWeaponDefinitionId; set { if (_selectedWeaponDefinitionId != value) { _selectedWeaponDefinitionId = value; Notify(); } } }
    public string SelectedAmmoDefinitionId { get => _selectedAmmoDefinitionId; set { if (_selectedAmmoDefinitionId != value) { _selectedAmmoDefinitionId = value; Notify(); } } }
    public int DamageAmount { get => _damageAmount; set { if (_damageAmount != value) { _damageAmount = value; Notify(); } } }
    public int DamageOverride { get => _damageOverride; set { if (_damageOverride != value) { _damageOverride = value; Notify(); } } }
    public string DamageType { get => _damageType; set { if (_damageType != value) { _damageType = value; Notify(); } } }
    public bool AutoApplyDamage { get => _autoApplyDamage; set { if (_autoApplyDamage != value) { _autoApplyDamage = value; Notify(); } } }
    public string SelectedConditionDefinitionId { get => _selectedConditionDefinitionId; set { if (_selectedConditionDefinitionId != value) { _selectedConditionDefinitionId = value; Notify(); } } }
    public string SelectedConditionInstanceId { get => _selectedConditionInstanceId; set { if (_selectedConditionInstanceId != value) { _selectedConditionInstanceId = value; Notify(); } } }
    public int ConditionStackCount { get => _conditionStackCount; set { if (_conditionStackCount != value) { _conditionStackCount = value; Notify(); } } }
    public string ConditionDurationMode { get => _conditionDurationMode; set { if (_conditionDurationMode != value) { _conditionDurationMode = value; Notify(); } } }
    public int ConditionDurationRounds { get => _conditionDurationRounds; set { if (_conditionDurationRounds != value) { _conditionDurationRounds = value; Notify(); } } }
    public string FateRollContext { get => _fateRollContext; set { if (_fateRollContext != value) { _fateRollContext = value; Notify(); } } }
    public int FateBaseRoll { get => _fateBaseRoll; set { if (_fateBaseRoll != value) { _fateBaseRoll = value; Notify(); } } }
    public string FateDiceExpression { get => _fateDiceExpression; set { if (_fateDiceExpression != value) { _fateDiceExpression = value; Notify(); } } }
    public string LastRulesResultSummary { get => _lastRulesResultSummary; private set { if (_lastRulesResultSummary != value) { _lastRulesResultSummary = value; Notify(); } } }

    public ObservableCollection<CombatParticipantUiItem> Participants { get; } = new ObservableCollection<CombatParticipantUiItem>();
    public ObservableCollection<CombatLogUiItem> RecentLogs { get; } = new ObservableCollection<CombatLogUiItem>();
    public ObservableCollection<CombatReplayUiItem> RecentReplayEvents { get; } = new ObservableCollection<CombatReplayUiItem>();
    public ObservableCollection<CombatDiagnosticsSectionUiItem> DiagnosticsSections { get; } = new ObservableCollection<CombatDiagnosticsSectionUiItem>();
    public ObservableCollection<CombatInitiativeUiItem> InitiativeOrder { get; } = new ObservableCollection<CombatInitiativeUiItem>();

    public ICommand RefreshSnapshotCommand { get; }
    public ICommand RefreshLogsCommand { get; }
    public ICommand RefreshDiagnosticsCommand { get; }
    public ICommand RefreshReplayCommand { get; }
    public ICommand RefreshFlagsCommand { get; }
    public ICommand ClearErrorCommand { get; }
    public ICommand SelectParticipantCommand { get; }
    public ICommand CreateEncounterCommand { get; }
    public ICommand EndEncounterCommand { get; }
    public ICommand CancelEncounterCommand { get; }
    public ICommand AddParticipantCommand { get; }
    public ICommand RemoveParticipantCommand { get; }
    public ICommand SetVitalsCommand { get; }
    public ICommand SortInitiativeCommand { get; }
    public ICommand StartRoundCommand { get; }
    public ICommand StartTurnCommand { get; }
    public ICommand EndTurnCommand { get; }
    public ICommand NextTurnCommand { get; }
    public ICommand NextRoundCommand { get; }
    public ICommand SkipTurnCommand { get; }
    public ICommand DelayTurnCommand { get; }
    public ICommand AttackRollCommand { get; }
    public ICommand DefensePreviewCommand { get; }
    public ICommand ApplyDamageCommand { get; }
    public ICommand ApplyConditionCommand { get; }
    public ICommand RemoveConditionCommand { get; }
    public ICommand WeaponAttackResolveCommand { get; }
    public ICommand FatePreviewCommand { get; }

    private void CreateEncounter()
    {
        if (!RequireWriteFlags(CanUseCombatWriteEndpoints, "Combat write flags are disabled.")) return;
        if (string.IsNullOrWhiteSpace(CampaignId) || string.IsNullOrWhiteSpace(SessionId) || string.IsNullOrWhiteSpace(RuleSetId))
        {
            ErrorMessage = "CampaignId, SessionId and RuleSetId are required to create an encounter.";
            return;
        }

        RunWrite(CommandNames.CombatV1EncounterCreate, () =>
        {
            var response = _api.CombatV1EncounterCreate(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId },
                { "ruleSetId", RuleSetId },
                { "name", FirstNonEmpty(NewEncounterName, "Combat encounter") },
                { "requestId", NewRequestId() }
            });
            if (!HandleWriteResponse(response, "create encounter")) return false;
            var createdId = Str(response.Payload, "encounterId");
            if (!string.IsNullOrWhiteSpace(createdId)) EncounterId = createdId;
            WriteStatusMessage = $"Encounter created: {FirstNonEmpty(createdId, EncounterId)}";
            return true;
        });
    }

    private void EndEncounter()
    {
        if (!EnsureEncounterId() || !RequireWriteFlags(CanUseCombatWriteEndpoints, "Combat write flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1EncounterEnd, () => SendSimpleWrite(_api.CombatV1EncounterEnd(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "reason", "ended from Admin Combat panel" },
            { "requestId", NewRequestId() }
        }), "end encounter"));
    }

    private void CancelEncounter()
    {
        if (!EnsureEncounterId() || !RequireWriteFlags(CanUseCombatWriteEndpoints, "Combat write flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1EncounterCancel, () => SendSimpleWrite(_api.CombatV1EncounterCancel(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "reason", "cancelled from Admin Combat panel" },
            { "requestId", NewRequestId() }
        }), "cancel encounter"));
    }

    private void AddParticipant()
    {
        if (!EnsureEncounterId() || !RequireWriteFlags(CanUseCombatWriteEndpoints, "Combat write flags are disabled.")) return;
        if (string.IsNullOrWhiteSpace(NewParticipantDisplayName))
        {
            ErrorMessage = "Participant display name is required.";
            return;
        }

        RunWrite(CommandNames.CombatV1ParticipantAdd, () => SendSimpleWrite(_api.CombatV1ParticipantAdd(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "characterId", NewParticipantCharacterId },
            { "displayName", NewParticipantDisplayName },
            { "participantType", FirstNonEmpty(NewParticipantType, "npc") },
            { "teamId", NewParticipantTeamId },
            { "isNpc", !string.Equals(NewParticipantType, "player_character", StringComparison.OrdinalIgnoreCase) },
            { "isPlayerControlled", string.Equals(NewParticipantType, "player_character", StringComparison.OrdinalIgnoreCase) },
            { "initiative", NewParticipantInitiative },
            { "requestId", NewRequestId() }
        }), "add participant"));
    }

    private void RemoveParticipant()
    {
        if (!EnsureSelectedParticipant() || !RequireWriteFlags(CanUseCombatWriteEndpoints, "Combat write flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1ParticipantRemove, () => SendSimpleWrite(_api.CombatV1ParticipantRemove(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "participantId", SelectedParticipant!.Id },
            { "reason", "removed from Admin Combat panel" },
            { "requestId", NewRequestId() }
        }), "remove participant"));
    }

    private void SetVitals()
    {
        if (!EnsureSelectedParticipant() || !RequireWriteFlags(CanUseDamage, "Vitals endpoint flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1ParticipantVitalsSet, () => SendSimpleWrite(_api.CombatV1ParticipantVitalsSet(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "participantId", SelectedParticipant!.Id },
            { "maxHealth", VitalsMaxHealth },
            { "currentHealth", VitalsCurrentHealth },
            { "temporaryHealth", VitalsTemporaryHealth },
            { "maxMorale", VitalsMaxMorale },
            { "currentMorale", VitalsCurrentMorale },
            { "reason", "set from Admin Combat panel" },
            { "requestId", NewRequestId() }
        }), "set vitals"));
    }

    private void SortInitiative()
    {
        if (!EnsureEncounterId() || !RequireWriteFlags(CanUseTurnEngine, "Turn engine flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1InitiativeSort, () => SendSimpleWrite(_api.CombatV1InitiativeSort(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "sortMode", "descending_initiative_then_tiebreaker" },
            { "requestId", NewRequestId() }
        }), "sort initiative"));
    }

    private void StartRound()
    {
        if (!EnsureEncounterId() || !RequireWriteFlags(CanUseTurnEngine, "Turn engine flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1RoundStart, () => SendSimpleWrite(_api.CombatV1RoundStart(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "roundNumber", RoundNumber <= 0 ? 1 : RoundNumber },
            { "requestId", NewRequestId() }
        }), "start round"));
    }

    private void StartTurn()
    {
        var participantId = SelectedParticipant?.Id ?? ActiveParticipantId;
        if (!EnsureEncounterId() || !RequireParticipantId(participantId, "Select a participant to start turn.") || !RequireWriteFlags(CanUseTurnEngine, "Turn engine flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1TurnStart, () => SendSimpleWrite(_api.CombatV1TurnStart(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "participantId", participantId },
            { "requestId", NewRequestId() }
        }), "start turn"));
    }

    private void EndTurn()
    {
        var participantId = SelectedParticipant?.Id ?? ActiveParticipantId;
        if (!EnsureEncounterId() || !RequireParticipantId(participantId, "Select a participant to end turn.") || !RequireWriteFlags(CanUseTurnEngine, "Turn engine flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1TurnEnd, () => SendSimpleWrite(_api.CombatV1TurnEnd(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "participantId", participantId },
            { "reason", "ended from Admin Combat panel" },
            { "requestId", NewRequestId() }
        }), "end turn"));
    }

    private void NextTurn()
    {
        if (!EnsureEncounterId() || !RequireWriteFlags(CanUseTurnEngine, "Turn engine flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1TurnNext, () => SendSimpleWrite(_api.CombatV1TurnNext(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "requestId", NewRequestId() }
        }), "next turn"));
    }

    private void NextRound()
    {
        if (!EnsureEncounterId() || !RequireWriteFlags(CanUseTurnEngine, "Turn engine flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1RoundNext, () => SendSimpleWrite(_api.CombatV1RoundNext(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "requestId", NewRequestId() }
        }), "next round"));
    }

    private void SkipTurn()
    {
        var participantId = SelectedParticipant?.Id ?? ActiveParticipantId;
        if (!EnsureEncounterId() || !RequireParticipantId(participantId, "Select a participant to skip turn.") || !RequireWriteFlags(CanUseTurnEngine, "Turn engine flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1TurnSkip, () => SendSimpleWrite(_api.CombatV1TurnSkip(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "participantId", participantId },
            { "reason", "skipped from Admin Combat panel" },
            { "requestId", NewRequestId() }
        }), "skip turn"));
    }

    private void DelayTurn()
    {
        var participantId = SelectedParticipant?.Id ?? ActiveParticipantId;
        if (!EnsureEncounterId() || !RequireParticipantId(participantId, "Select a participant to delay turn.") || !RequireWriteFlags(CanUseTurnEngine, "Turn engine flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1TurnDelay, () => SendSimpleWrite(_api.CombatV1TurnDelay(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "participantId", participantId },
            { "reason", "delayed from Admin Combat panel" },
            { "requestId", NewRequestId() }
        }), "delay turn"));
    }

    private void AttackRoll()
    {
        if (!EnsureTargetParticipant() || !RequireWriteFlags(CanUseAttackRoll, "Attack flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1AttackRoll, () =>
        {
            var response = _api.CombatV1AttackRoll(new Dictionary<string, object>
            {
                { "encounterId", EncounterId },
                { "actorParticipantId", FirstNonEmpty(ActiveParticipantId, SelectedParticipant?.Id ?? string.Empty) },
                { "targetParticipantId", SelectedTargetParticipant!.Id },
                { "weaponDefinitionId", SelectedWeaponDefinitionId },
                { "attackBonus", AttackBonus },
                { "coverModifier", CoverModifier },
                { "situationalModifier", SituationalModifier },
                { "spendActionPoint", SpendActionPoint },
                { "requestId", NewRequestId() }
            });
            if (!HandleWriteResponse(response, "attack roll")) return false;
            LastRulesResultSummary = BuildAttackSummary(response.Payload);
            WriteStatusMessage = LastRulesResultSummary;
            return true;
        });
    }

    private void DefensePreview()
    {
        if (!EnsureTargetParticipant() || !RequireWriteFlags(CanUseAttackRoll || AreCombatReadFlagsEnabled, "Defense preview flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1DefensePreview, () =>
        {
            var response = _api.CombatV1DefensePreview(new Dictionary<string, object>
            {
                { "encounterId", EncounterId },
                { "targetParticipantId", SelectedTargetParticipant!.Id },
                { "attackerParticipantId", FirstNonEmpty(ActiveParticipantId, SelectedParticipant?.Id ?? string.Empty) },
                { "ruleSetId", RuleSetId },
                { "weaponDefinitionId", SelectedWeaponDefinitionId },
                { "coverModifierOverride", CoverModifier },
                { "requestId", NewRequestId() }
            });
            if (!HandleWriteResponse(response, "defense preview")) return false;
            LastRulesResultSummary = $"Defense: {Int(response.Payload, "targetDefense")} (armor {Int(response.Payload, "armorDefenseBonus")}, shield {Int(response.Payload, "shieldDefenseBonus")}, cover {Int(response.Payload, "coverDefenseBonus")})";
            WriteStatusMessage = LastRulesResultSummary;
            return true;
        }, refreshAfterSuccess: false);
    }

    private void ApplyDamage()
    {
        if (!EnsureTargetParticipant() || !RequireWriteFlags(CanUseDamage, "Damage flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1DamageApply, () =>
        {
            var response = _api.CombatV1DamageApply(new Dictionary<string, object>
            {
                { "encounterId", EncounterId },
                { "attackerParticipantId", FirstNonEmpty(ActiveParticipantId, SelectedParticipant?.Id ?? string.Empty) },
                { "targetParticipantId", SelectedTargetParticipant!.Id },
                { "damageAmount", DamageAmount },
                { "damageType", FirstNonEmpty(DamageType, "physical") },
                { "damageSource", "Admin Combat panel" },
                { "allowAutoDefeat", true },
                { "reason", "applied from Admin Combat panel" },
                { "requestId", NewRequestId() }
            });
            if (!HandleWriteResponse(response, "apply damage")) return false;
            LastRulesResultSummary = $"Damage applied: {Int(response.Payload, "damageApplied")} HP {Int(response.Payload, "previousHealth")} -> {Int(response.Payload, "currentHealth")}";
            WriteStatusMessage = LastRulesResultSummary;
            return true;
        });
    }

    private void ApplyCondition()
    {
        if (!EnsureTargetParticipant() || !RequireWriteFlags(CanUseConditions, "Condition flags are disabled.")) return;
        if (string.IsNullOrWhiteSpace(SelectedConditionDefinitionId))
        {
            ErrorMessage = "ConditionDefinitionId is required.";
            return;
        }

        RunWrite(CommandNames.CombatV1ConditionApply, () => SendSimpleWrite(_api.CombatV1ConditionApply(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "targetParticipantId", SelectedTargetParticipant!.Id },
            { "conditionDefinitionId", SelectedConditionDefinitionId },
            { "sourceParticipantId", FirstNonEmpty(ActiveParticipantId, SelectedParticipant?.Id ?? string.Empty) },
            { "stackCount", ConditionStackCount <= 0 ? 1 : ConditionStackCount },
            { "durationMode", FirstNonEmpty(ConditionDurationMode, "until_removed") },
            { "durationRounds", ConditionDurationRounds },
            { "requestId", NewRequestId() }
        }), "apply condition"));
    }

    private void RemoveCondition()
    {
        if (!EnsureTargetParticipant() || !RequireWriteFlags(CanUseConditions, "Condition flags are disabled.")) return;
        if (string.IsNullOrWhiteSpace(SelectedConditionInstanceId) && string.IsNullOrWhiteSpace(SelectedConditionDefinitionId))
        {
            ErrorMessage = "Condition instance id or definition id is required.";
            return;
        }

        RunWrite(CommandNames.CombatV1ConditionRemove, () => SendSimpleWrite(_api.CombatV1ConditionRemove(new Dictionary<string, object>
        {
            { "encounterId", EncounterId },
            { "targetParticipantId", SelectedTargetParticipant!.Id },
            { "conditionInstanceId", SelectedConditionInstanceId },
            { "conditionDefinitionId", SelectedConditionDefinitionId },
            { "reason", "removed from Admin Combat panel" },
            { "requestId", NewRequestId() }
        }), "remove condition"));
    }

    private void WeaponAttackResolve()
    {
        if (!EnsureTargetParticipant() || !RequireWriteFlags(CanUseWeaponAttack, "Weapon attack flags are disabled.")) return;
        if (string.IsNullOrWhiteSpace(SelectedWeaponDefinitionId))
        {
            ErrorMessage = "WeaponDefinitionId is required for weapon attack.";
            return;
        }

        RunWrite(CommandNames.CombatV1WeaponAttackResolve, () =>
        {
            var payload = new Dictionary<string, object>
            {
                { "encounterId", EncounterId },
                { "actorParticipantId", FirstNonEmpty(ActiveParticipantId, SelectedParticipant?.Id ?? string.Empty) },
                { "targetParticipantId", SelectedTargetParticipant!.Id },
                { "weaponDefinitionId", SelectedWeaponDefinitionId },
                { "ammoDefinitionId", SelectedAmmoDefinitionId },
                { "attackBonus", AttackBonus },
                { "damageType", FirstNonEmpty(DamageType, "physical") },
                { "coverModifier", CoverModifier },
                { "situationalModifier", SituationalModifier },
                { "spendActionPoint", SpendActionPoint },
                { "autoApplyDamage", AutoApplyDamage },
                { "requestId", NewRequestId() }
            };
            if (DamageOverride > 0) payload["damageOverride"] = DamageOverride;
            var response = _api.CombatV1WeaponAttackResolve(payload);
            if (!HandleWriteResponse(response, "weapon attack")) return false;
            LastRulesResultSummary = BuildWeaponAttackSummary(response.Payload);
            WriteStatusMessage = LastRulesResultSummary;
            return true;
        });
    }

    private void FatePreview()
    {
        if (!RequireWriteFlags(CanUseFateHook, "Fate hook flags are disabled.")) return;
        RunWrite(CommandNames.CombatV1FatePreview, () =>
        {
            var response = _api.CombatV1FatePreview(new Dictionary<string, object>
            {
                { "encounterId", EncounterId },
                { "rollContext", FirstNonEmpty(FateRollContext, "attack_roll") },
                { "actorParticipantId", FirstNonEmpty(ActiveParticipantId, SelectedParticipant?.Id ?? string.Empty) },
                { "targetParticipantId", SelectedTargetParticipant?.Id ?? string.Empty },
                { "baseRoll", FateBaseRoll },
                { "diceExpression", FirstNonEmpty(FateDiceExpression, "1d20") },
                { "useFateEngine", true },
                { "requestId", NewRequestId() }
            });
            if (!HandleWriteResponse(response, "fate preview")) return false;
            LastRulesResultSummary = $"Fate: applied={Bool(response.Payload, "applied")} modifier={Int(response.Payload, "fateModifier")} {Str(response.Payload, "fateSummary")}";
            WriteStatusMessage = LastRulesResultSummary;
            return true;
        }, refreshAfterSuccess: false);
    }

    private void RefreshSnapshot()
    {
        if (!EnsureEncounterId()) return;
        RunReadOnly("combat.ui.snapshot.refresh", () =>
        {
            RefreshFeatureFlags();
            ClientLogService.Instance.Info($"combat.ui.snapshot.refresh.start encounterId={EncounterId}");
            var response = _api.CombatV1SnapshotFull(new Dictionary<string, object>
            {
                { "encounterId", EncounterId },
                { "includeParticipants", true },
                { "includeTurns", true },
                { "includeRounds", true },
                { "includeActions", true },
                { "includeLogs", true },
                { "includeReplayEvents", false },
                { "includeDiagnostics", true },
                { "limitLogs", 100 },
                { "limitActions", 100 }
            });

            if (!HandleResponseIssue(response, "snapshot")) return;
            ApplySnapshot(response.Payload);
            LastRefreshAtUtc = DateTime.UtcNow;
            ClientLogService.Instance.Info($"combat.ui.snapshot.refresh.done encounterId={EncounterId} participants={Participants.Count} logs={RecentLogs.Count}");
        });
    }

    private void RefreshLogs()
    {
        if (!EnsureEncounterId()) return;
        RunReadOnly("combat.ui.logs.refresh", () =>
        {
            var response = _api.CombatV1LogsList(new Dictionary<string, object>
            {
                { "encounterId", EncounterId },
                { "limit", 100 },
                { "offset", 0 }
            });

            if (!HandleResponseIssue(response, "logs", warningOnly: true)) return;
            RecentLogs.Clear();
            foreach (var entry in AsList(Get(response.Payload, "items")).Select(AsDictionary))
            {
                RecentLogs.Add(CombatLogUiItem.From(entry));
            }

            Notify(nameof(LogsCount));
            ClientLogService.Instance.Info($"combat.ui.logs.refresh.done encounterId={EncounterId} count={RecentLogs.Count}");
        });
    }

    private void RefreshReplay()
    {
        if (!EnsureEncounterId()) return;
        RunReadOnly("combat.ui.replay.refresh", () =>
        {
            var response = _api.CombatV1ReplayList(new Dictionary<string, object>
            {
                { "encounterId", EncounterId },
                { "limit", 100 }
            });

            if (!HandleResponseIssue(response, "replay", warningOnly: true))
            {
                ReplayStatus = "Replay выключен или недоступен.";
                return;
            }

            RecentReplayEvents.Clear();
            foreach (var entry in AsList(Get(response.Payload, "items")).Select(AsDictionary))
            {
                RecentReplayEvents.Add(CombatReplayUiItem.From(entry));
            }

            ReplayStatus = RecentReplayEvents.Count == 0 ? "Replay feed пуст." : $"Replay events: {RecentReplayEvents.Count}";
            Notify(nameof(ReplayCount));
            ClientLogService.Instance.Info($"combat.ui.replay.refresh.done encounterId={EncounterId} count={RecentReplayEvents.Count}");
        });
    }

    private void RefreshDiagnostics()
    {
        if (!EnsureEncounterId()) return;
        RunReadOnly("combat.ui.diagnostics.refresh", () =>
        {
            var response = _api.CombatV1DiagnosticsRun(new Dictionary<string, object>
            {
                { "encounterId", EncounterId },
                { "includeEncounterValidation", true },
                { "includeParticipantValidation", true },
                { "includeInitiativeValidation", true },
                { "includeTurnValidation", true },
                { "includeActionValidation", true },
                { "strictMode", false }
            });

            if (!HandleResponseIssue(response, "diagnostics", warningOnly: true)) return;
            ApplyDiagnostics(response.Payload);
            ClientLogService.Instance.Info($"combat.ui.diagnostics.refresh.done encounterId={EncounterId} sections={DiagnosticsSections.Count}");
        });
    }

    private void RefreshFeatureFlags()
    {
        try
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            if (response.Status != ResponseStatus.Ok)
            {
                WarningMessage = FriendlyMessage(response, "Снимок функций и модулей недоступен.");
                return;
            }

            var flags = AsList(Get(response.Payload, "flags")).Select(AsDictionary).ToList();
            var required = new[]
            {
                "Combat.UseCombatSystemV1",
                "Combat.UseCombatEncounterRuntime",
                "Combat.UseCombatReadEndpoints",
                "Combat.UseCombatSnapshotReadEndpoints"
            };
            var missingOrDisabled = required.Where(name => !FlagEnabled(flags, name)).ToList();
            AreCombatReadFlagsEnabled = missingOrDisabled.Count == 0;
            CanUseCombatWriteEndpoints = RequiredFlagsEnabled(flags,
                "Combat.UseCombatSystemV1",
                "Combat.UseCombatEncounterRuntime",
                "Combat.UseCombatWriteEndpoints");
            CanUseTurnEngine = RequiredFlagsEnabled(flags,
                "Combat.UseCombatSystemV1",
                "Combat.UseCombatEncounterRuntime",
                "Combat.UseCombatInitiativeOrder",
                "Combat.UseCombatTurnEngine",
                "Combat.UseCombatWriteEndpoints");
            CanUseAttackRoll = RequiredFlagsEnabled(flags,
                "Combat.UseCombatSystemV1",
                "Combat.UseCombatEncounterRuntime",
                "Combat.UseCombatTurnEngine",
                "Combat.UseCombatActionEconomySkeleton",
                "Combat.UseCombatAttackRollMvp",
                "Combat.UseCombatHitCalculationMvp",
                "Combat.UseCombatAttackActionEndpoint",
                "Combat.UseCombatWriteEndpoints");
            CanUseDefensePreview = RequiredFlagsEnabled(flags,
                "Combat.UseCombatSystemV1",
                "Combat.UseCombatEncounterRuntime",
                "Combat.UseCombatReadEndpoints",
                "Combat.UseCombatDefenseMvp",
                "Combat.UseCombatDefensePreviewEndpoint");
            CanUseDamage = RequiredFlagsEnabled(flags,
                "Combat.UseCombatSystemV1",
                "Combat.UseCombatEncounterRuntime",
                "Combat.UseCombatDamageMvp",
                "Combat.UseCombatDamageApplicationEndpoint",
                "Combat.UseCombatParticipantVitals",
                "Combat.UseCombatWriteEndpoints");
            CanUseConditions = RequiredFlagsEnabled(flags,
                "Combat.UseCombatSystemV1",
                "Combat.UseCombatEncounterRuntime",
                "Combat.UseCombatConditionsMvp",
                "Combat.UseCombatConditionApplyEndpoint",
                "Combat.UseCombatConditionRemoveEndpoint",
                "Combat.UseCombatWriteEndpoints");
            CanUseWeaponAttack = RequiredFlagsEnabled(flags,
                "Combat.UseCombatSystemV1",
                "Combat.UseCombatEncounterRuntime",
                "Combat.UseCombatTurnEngine",
                "Combat.UseCombatActionEconomySkeleton",
                "Combat.UseCombatAttackRollMvp",
                "Combat.UseCombatHitCalculationMvp",
                "Combat.UseCombatAttackActionEndpoint",
                "Combat.UseCombatWeaponIntegrationMvp",
                "Combat.UseCombatWriteEndpoints");
            CanUseFateHook = RequiredFlagsEnabled(flags,
                "Combat.UseCombatSystemV1",
                "Combat.UseCombatReadEndpoints",
                "Combat.UseCombatFateHookMvp");
            if (!AreCombatReadFlagsEnabled)
            {
                WarningMessage = "Чтение боя выключено. Включите нужные dev/test флаги функций.";
            }
            else if (!CanUseCombatWriteEndpoints)
            {
                WarningMessage = "Combat write flags выключены. Управление боем недоступно.";
            }
            ClientLogService.Instance.Info("combat.ui.flags.snapshot.loaded");
        }
        catch (Exception ex)
        {
            WarningMessage = $"Снимок функций и модулей недоступен: {ex.Message}";
        }
    }

    private void ApplySnapshot(IDictionary<string, object> payload)
    {
        var encounter = AsDictionary(Get(payload, "encounter"));
        EncounterName = FirstNonEmpty(Str(encounter, "name"), Str(encounter, "id"), EncounterId);
        EncounterStatus = Str(encounter, "status");
        CampaignId = FirstNonEmpty(CampaignId, Str(encounter, "campaignId"));
        SessionId = FirstNonEmpty(SessionId, Str(encounter, "sessionId"));
        RuleSetId = FirstNonEmpty(RuleSetId, Str(encounter, "ruleSetId"));
        RoundNumber = Int(encounter, "roundNumber");
        ActiveTurnIndex = Int(encounter, "activeTurnIndex");
        ActiveParticipantId = Str(encounter, "activeParticipantId");

        Participants.Clear();
        foreach (var participant in AsList(Get(payload, "participants")).Select(AsDictionary))
        {
            Participants.Add(CombatParticipantUiItem.From(participant, ActiveParticipantId));
        }

        SelectedParticipant = Participants.FirstOrDefault(item => item.Id == ActiveParticipantId) ?? Participants.FirstOrDefault();
        SelectedTargetParticipant = Participants.FirstOrDefault(item => item.Id != SelectedParticipant?.Id) ?? SelectedParticipant;
        ActiveParticipantName = Participants.FirstOrDefault(item => item.Id == ActiveParticipantId)?.DisplayName ?? FirstNonEmpty(ActiveParticipantId, "нет активного участника");
        Notify(nameof(ParticipantsCount));

        var round = AsDictionary(Get(payload, "currentRound"));
        CurrentRoundSummary = $"Раунд {Int(round, "roundNumber")} • ходов: {Int(round, "turnCount")}";

        var turn = AsDictionary(Get(payload, "currentTurn"));
        CurrentTurnSummary = $"Раунд {Int(turn, "roundNumber")} / ход {Int(turn, "turnIndex")} • участник: {FirstNonEmpty(Str(turn, "participantId"), ActiveParticipantId, "не указан")} • {Str(turn, "status")}";

        InitiativeOrder.Clear();
        foreach (var entry in AsList(Get(payload, "initiativeOrder")).Select(AsDictionary))
        {
            InitiativeOrder.Add(CombatInitiativeUiItem.From(entry));
        }

        RecentLogs.Clear();
        foreach (var entry in AsList(Get(payload, "recentLogs")).Select(AsDictionary))
        {
            RecentLogs.Add(CombatLogUiItem.From(entry));
        }
        Notify(nameof(LogsCount));

        RecentReplayEvents.Clear();
        foreach (var entry in AsList(Get(payload, "recentReplayEvents")).Select(AsDictionary))
        {
            RecentReplayEvents.Add(CombatReplayUiItem.From(entry));
        }
        ReplayStatus = RecentReplayEvents.Count == 0 ? "Replay feed не запрошен или пуст." : $"Replay events: {RecentReplayEvents.Count}";
        Notify(nameof(ReplayCount));

        ApplyDiagnosticsSummary(AsDictionary(Get(payload, "diagnostics")));

        var warnings = AsList(Get(payload, "warnings")).Select(Convert.ToString).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (warnings.Length > 0) WarningMessage = string.Join("; ", warnings);
    }

    private void ApplyDiagnostics(IDictionary<string, object> payload)
    {
        ApplyDiagnosticsSummary(AsDictionary(Get(payload, "summary")));
        DiagnosticsSections.Clear();
        foreach (var section in AsList(Get(payload, "sections")).Select(AsDictionary))
        {
            DiagnosticsSections.Add(CombatDiagnosticsSectionUiItem.From(section));
        }
    }

    private void ApplyDiagnosticsSummary(IDictionary<string, object> summary)
    {
        var errors = Int(summary, "errorCount");
        var warnings = Int(summary, "warningCount");
        DiagnosticsSummary = $"Ншибки: {errors} • предупреждения: {warnings} • участников: {Int(summary, "participantCount")} • logs: {Int(summary, "logCount")}";
    }

    private bool EnsureEncounterId()
    {
        if (!string.IsNullOrWhiteSpace(EncounterId)) return true;
        WarningMessage = "Выберите или укажите бой.";
        ErrorMessage = string.Empty;
        return false;
    }

    private void RunReadOnly(string operation, Action action)
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            action();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ншибка чтения combat data: {ex.Message}";
            ClientLogService.Instance.Error($"{operation}.error encounterId={EncounterId} message={ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RunWrite(string command, Func<bool> action, bool refreshAfterSuccess = true)
    {
        if (IsWriteBusy) return;
        try
        {
            IsWriteBusy = true;
            ErrorMessage = string.Empty;
            WarningMessage = string.Empty;
            ClientLogService.Instance.Info($"combat.ui.command.start command={command}");
            var success = action();
            ClientLogService.Instance.Info($"combat.ui.command.done command={command} success={success}");
            if (success && refreshAfterSuccess)
            {
                RefreshSnapshot();
                RefreshLogs();
                RefreshDiagnostics();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Combat command failed: {ex.Message}";
            ClientLogService.Instance.Error($"combat.ui.command.error command={command} message={ex.Message}");
        }
        finally
        {
            IsWriteBusy = false;
        }
    }

    private bool SendSimpleWrite(ResponseEnvelope response, string source)
    {
        if (!HandleWriteResponse(response, source)) return false;
        WriteStatusMessage = response.Message;
        LastRulesResultSummary = response.Message;
        return true;
    }

    private bool HandleWriteResponse(ResponseEnvelope response, string source)
    {
        if (response.Status == ResponseStatus.Ok) return true;
        ErrorMessage = FriendlyWriteMessage(response, $"{source}: command failed.");
        return false;
    }

    private bool RequireWriteFlags(bool enabled, string message)
    {
        if (enabled) return true;
        WarningMessage = message;
        return false;
    }

    private bool EnsureSelectedParticipant()
    {
        if (EnsureEncounterId() && SelectedParticipant != null) return true;
        WarningMessage = "Select a participant first.";
        return false;
    }

    private bool EnsureTargetParticipant()
    {
        if (EnsureEncounterId() && SelectedTargetParticipant != null) return true;
        WarningMessage = "Select a target participant first.";
        return false;
    }

    private bool RequireParticipantId(string participantId, string message)
    {
        if (!string.IsNullOrWhiteSpace(participantId)) return true;
        WarningMessage = message;
        return false;
    }

    private void NotifyCombatCommandState()
    {
        Notify(nameof(CanCreateEncounter));
        Notify(nameof(CanRunEncounterCommand));
        Notify(nameof(CanRunSelectedParticipantCommand));
        Notify(nameof(CanRunTurnCommand));
        Notify(nameof(CanRunSelectedTurnCommand));
        Notify(nameof(CanRunTargetCommand));
        Notify(nameof(CanRunAttackCommand));
        Notify(nameof(CanRunDefensePreviewCommand));
        Notify(nameof(CanRunDamageCommand));
        Notify(nameof(CanRunVitalsCommand));
        Notify(nameof(CanRunConditionCommand));
        Notify(nameof(CanRunWeaponAttackCommand));
        Notify(nameof(CanRunFatePreviewCommand));
    }

    private static string NewRequestId() => Guid.NewGuid().ToString("N");

    private static string FriendlyWriteMessage(ResponseEnvelope response, string fallback)
    {
        var message = string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
        if (response.Status == ResponseStatus.Forbidden || response.ErrorCode == ErrorCode.Forbidden) return message.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0 ? "Combat flags выключены для этой команды." : "Недостаточно прав администратора для combat command.";
        if (response.Status == ResponseStatus.NotFound || response.ErrorCode == ErrorCode.NotFound) return "Combat entity not found.";
        return message;
    }

    private static string BuildAttackSummary(IDictionary<string, object> payload)
    {
        return $"Attack: {Str(payload, "hitResult")} roll {Int(payload, "naturalRoll")} total {Int(payload, "attackTotal")} vs {Int(payload, "targetDefense")}";
    }

    private static string BuildWeaponAttackSummary(IDictionary<string, object> payload)
    {
        var attack = AsDictionary(Get(payload, "attackResult"));
        var preview = AsDictionary(Get(payload, "damagePreview"));
        return $"Weapon attack: {Str(attack, "hitResult")} damage preview {Int(preview, "finalDamage")}";
    }

    private bool HandleResponseIssue(ResponseEnvelope response, string source, bool warningOnly = false)
    {
        if (response.Status == ResponseStatus.Ok) return true;
        var message = FriendlyMessage(response, $"{source}: read endpoint недоступен.");
        if (warningOnly)
        {
            WarningMessage = message;
        }
        else
        {
            ErrorMessage = message;
        }
        return false;
    }

    private static string FriendlyMessage(ResponseEnvelope response, string fallback)
    {
        var message = string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
        if (response.Status == ResponseStatus.Forbidden || response.ErrorCode == ErrorCode.Forbidden) return "Медостаточно прав администратора для чтения combat data.";
        if (response.Status == ResponseStatus.NotFound || response.ErrorCode == ErrorCode.NotFound) return "Бой не найден.";
        if (message.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("выключ", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Чтение боя выключено. Включите нужные dev/test флаги функций.";
        }
        return message;
    }

    private static bool FlagEnabled(IEnumerable<IDictionary<string, object>> flags, string name)
    {
        return flags.Any(flag =>
            string.Equals(Str(flag, "name"), name, StringComparison.OrdinalIgnoreCase) &&
            Bool(flag, "effectiveValue"));
    }

    private static bool RequiredFlagsEnabled(IEnumerable<IDictionary<string, object>> flags, params string[] names)
    {
        var materialized = flags.ToList();
        return names.All(name => FlagEnabled(materialized, name));
    }

    internal static object? Get(IDictionary<string, object> source, string key)
    {
        if (source.TryGetValue(key, out var value)) return value;
        var match = source.Keys.FirstOrDefault(candidate => string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase));
        return match == null ? null : source[match];
    }

    internal static IDictionary<string, object> AsDictionary(object? value)
    {
        if (value is IDictionary<string, object> dictionary) return dictionary;
        return new Dictionary<string, object>();
    }

    internal static IEnumerable<object?> AsList(object? value)
    {
        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable) yield return item;
        }
    }

    internal static string Str(IDictionary<string, object> source, string key)
        => Convert.ToString(Get(source, key), CultureInfo.InvariantCulture) ?? string.Empty;

    internal static int Int(IDictionary<string, object> source, string key)
    {
        var value = Get(source, key);
        if (value == null) return 0;
        if (value is int i) return i;
        if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return 0;
    }

    internal static long Long(IDictionary<string, object> source, string key)
    {
        var value = Get(source, key);
        if (value == null) return 0;
        if (value is long l) return l;
        if (long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return 0;
    }

    internal static bool Bool(IDictionary<string, object> source, string key)
    {
        var value = Get(source, key);
        if (value == null) return false;
        if (value is bool b) return b;
        return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) && parsed;
    }

    internal static DateTime Date(IDictionary<string, object> source, string key)
    {
        var value = Get(source, key);
        if (value is DateTime dt) return dt;
        return DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTime.MinValue;
    }

    internal static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class CombatParticipantUiItem
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDefeated { get; set; }
    public bool IsHidden { get; set; }
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public int TemporaryHealth { get; set; }
    public int CurrentMorale { get; set; }
    public int MaxMorale { get; set; }
    public int ConditionCount { get; set; }
    public List<string> ActiveConditionNames { get; set; } = new List<string>();
    public int Initiative { get; set; }
    public bool HasActedThisRound { get; set; }
    public bool IsCurrentTurn { get; set; }
    public int ActionPoints { get; set; }
    public int MinorActionPoints { get; set; }
    public int ReactionCount { get; set; }
    public int ReactionLimit { get; set; }
    public string PositionSummary { get; set; } = string.Empty;
    public string CoverState { get; set; } = string.Empty;
    public string VisibilityState { get; set; } = string.Empty;
    public string HitPointsText => MaxHealth > 0 ? $"{CurrentHealth}/{MaxHealth} (+{TemporaryHealth})" : "HP не заданы";
    public string MoraleText => MaxMorale > 0 ? $"{CurrentMorale}/{MaxMorale}" : "не задана";
    public string ConditionsText => ConditionCount == 0 ? "нет состояний" : string.Join(", ", ActiveConditionNames.Take(4));
    public string TurnStateText => IsCurrentTurn ? "текущий ход" : HasActedThisRound ? "действовал" : "ожидает";

    public static CombatParticipantUiItem From(IDictionary<string, object> source, string activeParticipantId)
    {
        var id = AdminCombatReadOnlyViewModel.Str(source, "id");
        var conditionNames = AdminCombatReadOnlyViewModel.AsList(AdminCombatReadOnlyViewModel.Get(source, "activeConditionIds"))
            .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        return new CombatParticipantUiItem
        {
            Id = id,
            DisplayName = AdminCombatReadOnlyViewModel.FirstNonEmpty(AdminCombatReadOnlyViewModel.Str(source, "displayName"), id),
            CharacterId = AdminCombatReadOnlyViewModel.Str(source, "characterId"),
            TeamId = AdminCombatReadOnlyViewModel.Str(source, "teamId"),
            ParticipantType = AdminCombatReadOnlyViewModel.Str(source, "participantType"),
            IsActive = AdminCombatReadOnlyViewModel.Bool(source, "isActive"),
            IsDefeated = AdminCombatReadOnlyViewModel.Bool(source, "isDefeated"),
            IsHidden = AdminCombatReadOnlyViewModel.Bool(source, "isHidden"),
            CurrentHealth = AdminCombatReadOnlyViewModel.Int(source, "currentHealth"),
            MaxHealth = AdminCombatReadOnlyViewModel.Int(source, "maxHealth"),
            TemporaryHealth = AdminCombatReadOnlyViewModel.Int(source, "temporaryHealth"),
            CurrentMorale = AdminCombatReadOnlyViewModel.Int(source, "currentMorale"),
            MaxMorale = AdminCombatReadOnlyViewModel.Int(source, "maxMorale"),
            ConditionCount = AdminCombatReadOnlyViewModel.Int(source, "conditionCount"),
            ActiveConditionNames = conditionNames,
            Initiative = AdminCombatReadOnlyViewModel.Int(source, "initiative"),
            HasActedThisRound = AdminCombatReadOnlyViewModel.Bool(source, "hasActedThisRound"),
            IsCurrentTurn = string.Equals(id, activeParticipantId, StringComparison.OrdinalIgnoreCase),
            ActionPoints = AdminCombatReadOnlyViewModel.Int(source, "actionPoints"),
            MinorActionPoints = AdminCombatReadOnlyViewModel.Int(source, "minorActionPoints"),
            ReactionCount = AdminCombatReadOnlyViewModel.Int(source, "reactionCount"),
            ReactionLimit = AdminCombatReadOnlyViewModel.Int(source, "reactionLimit"),
            PositionSummary = AdminCombatReadOnlyViewModel.Str(source, "positionSummary"),
            CoverState = AdminCombatReadOnlyViewModel.Str(source, "coverState"),
            VisibilityState = AdminCombatReadOnlyViewModel.Str(source, "visibilityState")
        };
    }
}

public sealed class CombatLogUiItem
{
    public DateTime CreatedAtUtc { get; set; }
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ActorParticipantId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string RoundTurnText => $"R{RoundNumber}/T{TurnIndex}";
    public string CreatedText => CreatedAtUtc == DateTime.MinValue ? string.Empty : CreatedAtUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    public static CombatLogUiItem From(IDictionary<string, object> source) => new CombatLogUiItem
    {
        CreatedAtUtc = AdminCombatReadOnlyViewModel.Date(source, "createdAtUtc"),
        RoundNumber = AdminCombatReadOnlyViewModel.Int(source, "roundNumber"),
        TurnIndex = AdminCombatReadOnlyViewModel.Int(source, "turnIndex"),
        EventType = AdminCombatReadOnlyViewModel.Str(source, "eventType"),
        ActorParticipantId = AdminCombatReadOnlyViewModel.Str(source, "actorParticipantId"),
        Message = AdminCombatReadOnlyViewModel.Str(source, "message"),
        Visibility = AdminCombatReadOnlyViewModel.Str(source, "visibility"),
        RequestId = AdminCombatReadOnlyViewModel.Str(source, "requestId")
    };
}

public sealed class CombatReplayUiItem
{
    public long SequenceNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string ActorParticipantId { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public string CreatedText => CreatedAtUtc == DateTime.MinValue ? string.Empty : CreatedAtUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    public static CombatReplayUiItem From(IDictionary<string, object> source) => new CombatReplayUiItem
    {
        SequenceNumber = AdminCombatReadOnlyViewModel.Long(source, "sequenceNumber"),
        CreatedAtUtc = AdminCombatReadOnlyViewModel.Date(source, "createdAtUtc"),
        EventType = AdminCombatReadOnlyViewModel.Str(source, "eventType"),
        RoundNumber = AdminCombatReadOnlyViewModel.Int(source, "roundNumber"),
        TurnIndex = AdminCombatReadOnlyViewModel.Int(source, "turnIndex"),
        ActorParticipantId = AdminCombatReadOnlyViewModel.Str(source, "actorParticipantId"),
        Visibility = AdminCombatReadOnlyViewModel.Str(source, "visibility")
    };
}

public sealed class CombatDiagnosticsSectionUiItem
{
    public string Section { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public string StatusText => IsValid ? "OK" : "Есть проблемы";

    public static CombatDiagnosticsSectionUiItem From(IDictionary<string, object> source)
    {
        return new CombatDiagnosticsSectionUiItem
        {
            Section = AdminCombatReadOnlyViewModel.Str(source, "section"),
            IsValid = AdminCombatReadOnlyViewModel.Bool(source, "isValid"),
            ErrorCount = AdminCombatReadOnlyViewModel.AsList(AdminCombatReadOnlyViewModel.Get(source, "errors")).Count(),
            WarningCount = AdminCombatReadOnlyViewModel.AsList(AdminCombatReadOnlyViewModel.Get(source, "warnings")).Count()
        };
    }
}

public sealed class CombatInitiativeUiItem
{
    public string ParticipantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Initiative { get; set; }
    public int OrderIndex { get; set; }
    public bool IsDelayed { get; set; }
    public bool IsSkipped { get; set; }
    public bool IsDefeated { get; set; }

    public static CombatInitiativeUiItem From(IDictionary<string, object> source) => new CombatInitiativeUiItem
    {
        ParticipantId = AdminCombatReadOnlyViewModel.Str(source, "participantId"),
        DisplayName = AdminCombatReadOnlyViewModel.Str(source, "displayName"),
        Initiative = AdminCombatReadOnlyViewModel.Int(source, "initiative"),
        OrderIndex = AdminCombatReadOnlyViewModel.Int(source, "orderIndex"),
        IsDelayed = AdminCombatReadOnlyViewModel.Bool(source, "isDelayed"),
        IsSkipped = AdminCombatReadOnlyViewModel.Bool(source, "isSkipped"),
        IsDefeated = AdminCombatReadOnlyViewModel.Bool(source, "isDefeated")
    };
}

