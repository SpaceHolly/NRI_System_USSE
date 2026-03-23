using Nri.Server.Application.Services;
using Nri.Server.Application.Validation;
using Nri.Server.Audit;
using Nri.Server.Handlers.Admin;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Server.Transport;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;

namespace Nri.Server.Application;

public sealed class ServerRuntime
{
    public ServerRuntime(CommandDispatcher dispatcher, SessionManager sessions)
    {
        Dispatcher = dispatcher;
        Sessions = sessions;
    }

    public CommandDispatcher Dispatcher { get; }
    public SessionManager Sessions { get; }
}

public static class ServiceRegistry
{
    public static ServerRuntime Build(ServerConfig config, IServerLogger logger)
    {
        var mongo = new MongoContext(config, logger);
        var repositories = new MongoRepositoryFactory(mongo);
        var sessions = new SessionManager(config.Tokens, repositories);
        var hub = new ServiceHub(repositories, sessions, logger, config.AudioFolderPath);
        var auditLogService = new AuditLogService(repositories, logger);
        var validationService = new DefinitionValidationService(
            new ClassDefinitionValidator(),
            new SkillDefinitionValidator(),
            new DefinitionReferenceValidator(repositories.ClassDefinitions, repositories.DefinitionSkills));
        var classDefinitionService = new ClassDefinitionService(repositories.ClassDefinitions, validationService, auditLogService);
        var skillDefinitionService = new SkillDefinitionService(repositories.DefinitionSkills, validationService, auditLogService);
        var adminDefinitionRouter = new RequestRouter(new AdminDefinitionHandlers(repositories, classDefinitionService, skillDefinitionService).CreateHandlers());

        var dispatcher = new CommandDispatcher(logger, sessions);

        dispatcher.Register(CommandNames.AuthRegister, new DelegateCommandHandler(hub.Register));
        dispatcher.Register(CommandNames.AuthLogin, new DelegateCommandHandler(hub.Login));
        dispatcher.Register(CommandNames.AuthLogout, new DelegateCommandHandler(hub.Logout));
        dispatcher.Register(CommandNames.SessionValidate, new DelegateCommandHandler(hub.SessionValidate));

        dispatcher.Register(CommandNames.ProfileGet, new DelegateCommandHandler(hub.ProfileGet));
        dispatcher.Register(CommandNames.ProfileUpdate, new DelegateCommandHandler(hub.ProfileUpdate));

        dispatcher.Register(CommandNames.AdminAccountsPending, new DelegateCommandHandler(hub.AdminPendingAccounts));
        dispatcher.Register(CommandNames.AdminAccountsApprove, new DelegateCommandHandler(hub.AdminApproveAccount));
        dispatcher.Register(CommandNames.AdminAccountsArchive, new DelegateCommandHandler(hub.AdminArchiveAccount));
        dispatcher.Register(CommandNames.AdminAccountProfile, new DelegateCommandHandler(hub.AdminAccountProfile));
        dispatcher.Register(CommandNames.AdminPlayersList, new DelegateCommandHandler(hub.AdminPlayersList));

        dispatcher.Register(CommandNames.CharacterListMine, new DelegateCommandHandler(hub.CharacterListMine));
        dispatcher.Register(CommandNames.CharacterListByOwner, new DelegateCommandHandler(hub.CharacterListByOwner));
        dispatcher.Register(CommandNames.CharacterGetActive, new DelegateCommandHandler(hub.CharacterGetActive));
        dispatcher.Register(CommandNames.CharacterGetDetails, new DelegateCommandHandler(hub.CharacterGetDetails));
        dispatcher.Register(CommandNames.CharacterGetSummary, new DelegateCommandHandler(hub.CharacterGetSummary));
        dispatcher.Register(CommandNames.CharacterGetCompanions, new DelegateCommandHandler(hub.CharacterGetCompanions));
        dispatcher.Register(CommandNames.CharacterGetInventory, new DelegateCommandHandler(hub.CharacterGetInventory));
        dispatcher.Register(CommandNames.CharacterGetReputation, new DelegateCommandHandler(hub.CharacterGetReputation));
        dispatcher.Register(CommandNames.CharacterGetHoldings, new DelegateCommandHandler(hub.CharacterGetHoldings));

        dispatcher.Register(CommandNames.CharacterUpdateBasicInfo, new DelegateCommandHandler(hub.CharacterUpdateBasicInfo));
        dispatcher.Register(CommandNames.CharacterUpdateStats, new DelegateCommandHandler(hub.CharacterUpdateStats));
        dispatcher.Register(CommandNames.CharacterUpdateVisibility, new DelegateCommandHandler(hub.CharacterUpdateVisibility));
        dispatcher.Register(CommandNames.CharacterUpdateMoney, new DelegateCommandHandler(hub.CharacterUpdateMoney));
        dispatcher.Register(CommandNames.CharacterUpdateInventory, new DelegateCommandHandler(hub.CharacterUpdateInventory));
        dispatcher.Register(CommandNames.CharacterUpdateReputation, new DelegateCommandHandler(hub.CharacterUpdateReputation));
        dispatcher.Register(CommandNames.CharacterUpdateHoldings, new DelegateCommandHandler(hub.CharacterUpdateHoldings));

        dispatcher.Register(CommandNames.CharacterCreate, new DelegateCommandHandler(hub.CharacterCreate));
        dispatcher.Register(CommandNames.CharacterArchive, new DelegateCommandHandler(hub.CharacterArchive));
        dispatcher.Register(CommandNames.CharacterRestore, new DelegateCommandHandler(hub.CharacterRestore));
        dispatcher.Register(CommandNames.CharacterTransfer, new DelegateCommandHandler(hub.CharacterTransfer));
        dispatcher.Register(CommandNames.CharacterAssignActive, new DelegateCommandHandler(hub.CharacterAssignActive));



        dispatcher.Register(CommandNames.CombatStart, new DelegateCommandHandler(hub.CombatStart));
        dispatcher.Register(CommandNames.CombatEnd, new DelegateCommandHandler(hub.CombatEnd));
        dispatcher.Register(CommandNames.CombatGetState, new DelegateCommandHandler(hub.CombatGetState));
        dispatcher.Register(CommandNames.CombatGetHistory, new DelegateCommandHandler(hub.CombatGetHistory));
        dispatcher.Register(CommandNames.CombatNextTurn, new DelegateCommandHandler(hub.CombatNextTurn));
        dispatcher.Register(CommandNames.CombatPreviousTurn, new DelegateCommandHandler(hub.CombatPreviousTurn));
        dispatcher.Register(CommandNames.CombatNextRound, new DelegateCommandHandler(hub.CombatNextRound));
        dispatcher.Register(CommandNames.CombatSkipTurn, new DelegateCommandHandler(hub.CombatSkipTurn));
        dispatcher.Register(CommandNames.CombatSelectActive, new DelegateCommandHandler(hub.CombatSelectActive));
        dispatcher.Register(CommandNames.CombatReorderBeforeStart, new DelegateCommandHandler(hub.CombatReorderBeforeStart));
        dispatcher.Register(CommandNames.CombatReorderSlotMembers, new DelegateCommandHandler(hub.CombatReorderSlotMembers));
        dispatcher.Register(CommandNames.CombatAddParticipant, new DelegateCommandHandler(hub.CombatAddParticipant));
        dispatcher.Register(CommandNames.CombatRemoveParticipant, new DelegateCommandHandler(hub.CombatRemoveParticipant));
        dispatcher.Register(CommandNames.CombatDetachCompanion, new DelegateCommandHandler(hub.CombatDetachCompanion));
        dispatcher.Register(CommandNames.CombatVisibleState, new DelegateCommandHandler(hub.CombatVisibleState));
        dispatcher.Register(CommandNames.CombatParticipants, new DelegateCommandHandler(hub.CombatParticipants));
        dispatcher.Register(CommandNames.CombatTimeline, new DelegateCommandHandler(hub.CombatTimeline));

        dispatcher.Register(CommandNames.DefinitionsClassesGet, new DelegateCommandHandler(hub.DefinitionsClassesGet));
        dispatcher.Register(CommandNames.DefinitionsSkillsGet, new DelegateCommandHandler(hub.DefinitionsSkillsGet));
        dispatcher.Register(CommandNames.DefinitionsReload, new DelegateCommandHandler(hub.DefinitionsReload));
        dispatcher.Register(CommandNames.DefinitionsVersionGet, new DelegateCommandHandler(hub.DefinitionsVersionGet));

        dispatcher.Register(CommandNames.ClassTreeGet, new DelegateCommandHandler(hub.ClassTreeGet));
        dispatcher.Register(CommandNames.ClassTreeNodeGet, new DelegateCommandHandler(hub.ClassTreeNodeGet));
        dispatcher.Register(CommandNames.ClassTreeAvailableGet, new DelegateCommandHandler(hub.ClassTreeAvailableGet));
        dispatcher.Register(CommandNames.ClassTreeAcquireNode, new DelegateCommandHandler(hub.ClassTreeAcquireNode));
        dispatcher.Register(CommandNames.ClassTreeRecalculate, new DelegateCommandHandler(hub.ClassTreeRecalculate));

        dispatcher.Register(CommandNames.SkillsList, new DelegateCommandHandler(hub.SkillsList));
        dispatcher.Register(CommandNames.SkillsAvailable, new DelegateCommandHandler(hub.SkillsAvailable));
        dispatcher.Register(CommandNames.SkillsGet, new DelegateCommandHandler(hub.SkillsGet));
        dispatcher.Register(CommandNames.SkillsAcquire, new DelegateCommandHandler(hub.SkillsAcquire));

        dispatcher.Register(CommandNames.AdminClassTreeSetState, new DelegateCommandHandler(hub.AdminClassTreeSetState));
        dispatcher.Register(CommandNames.AdminSkillsSetState, new DelegateCommandHandler(hub.AdminSkillsSetState));
        dispatcher.Register(CommandNames.AdminCharacterProgressRecalculate, new DelegateCommandHandler(hub.AdminCharacterProgressRecalculate));

        dispatcher.Register(CommandNames.AdminDefinitionsClassList, new RoutedCommandHandler(adminDefinitionRouter));
        dispatcher.Register(CommandNames.AdminDefinitionsClassGet, new RoutedCommandHandler(adminDefinitionRouter));
        dispatcher.Register(CommandNames.AdminDefinitionsClassSave, new RoutedCommandHandler(adminDefinitionRouter));
        dispatcher.Register(CommandNames.AdminDefinitionsSkillList, new RoutedCommandHandler(adminDefinitionRouter));
        dispatcher.Register(CommandNames.AdminDefinitionsSkillGet, new RoutedCommandHandler(adminDefinitionRouter));
        dispatcher.Register(CommandNames.AdminDefinitionsSkillSave, new RoutedCommandHandler(adminDefinitionRouter));

        dispatcher.Register(CommandNames.RequestCreate, new DelegateCommandHandler(hub.RequestCreate));
        dispatcher.Register(CommandNames.RequestCancel, new DelegateCommandHandler(hub.RequestCancel));
        dispatcher.Register(CommandNames.RequestListMine, new DelegateCommandHandler(hub.RequestListMine));
        dispatcher.Register(CommandNames.RequestListPending, new DelegateCommandHandler(hub.RequestListPending));
        dispatcher.Register(CommandNames.RequestGetDetails, new DelegateCommandHandler(hub.RequestGetDetails));
        dispatcher.Register(CommandNames.RequestApprove, new DelegateCommandHandler(hub.RequestApprove));
        dispatcher.Register(CommandNames.RequestReject, new DelegateCommandHandler(hub.RequestReject));
        dispatcher.Register(CommandNames.RequestHistory, new DelegateCommandHandler(hub.RequestHistory));

        dispatcher.Register(CommandNames.DiceRequest, new DelegateCommandHandler(hub.DiceRequest));
        dispatcher.Register(CommandNames.DiceHistory, new DelegateCommandHandler(hub.DiceHistory));
        dispatcher.Register(CommandNames.DiceVisibleFeed, new DelegateCommandHandler(hub.DiceVisibleFeed));
        dispatcher.Register(CommandNames.DiceGetDetails, new DelegateCommandHandler(hub.DiceGetDetails));

        dispatcher.Register(CommandNames.PresenceList, new DelegateCommandHandler(hub.PresenceList));

        dispatcher.Register(CommandNames.ChatSend, new DelegateCommandHandler(hub.ChatSend));
        dispatcher.Register(CommandNames.ChatHistoryGet, new DelegateCommandHandler(hub.ChatHistoryGet));
        dispatcher.Register(CommandNames.ChatHistoryLoadMore, new DelegateCommandHandler(hub.ChatHistoryLoadMore));
        dispatcher.Register(CommandNames.ChatVisibleFeed, new DelegateCommandHandler(hub.ChatVisibleFeed));
        dispatcher.Register(CommandNames.ChatMarkRead, new DelegateCommandHandler(hub.ChatMarkRead));
        dispatcher.Register(CommandNames.ChatUnreadGet, new DelegateCommandHandler(hub.ChatUnreadGet));

        dispatcher.Register(CommandNames.ChatSlowModeGet, new DelegateCommandHandler(hub.ChatSlowModeGet));
        dispatcher.Register(CommandNames.ChatSlowModeSet, new DelegateCommandHandler(hub.ChatSlowModeSet));
        dispatcher.Register(CommandNames.ChatRestrictionsGet, new DelegateCommandHandler(hub.ChatRestrictionsGet));
        dispatcher.Register(CommandNames.ChatRestrictionsMuteUser, new DelegateCommandHandler(hub.ChatRestrictionsMuteUser));
        dispatcher.Register(CommandNames.ChatRestrictionsUnmuteUser, new DelegateCommandHandler(hub.ChatRestrictionsUnmuteUser));
        dispatcher.Register(CommandNames.ChatRestrictionsLockPlayers, new DelegateCommandHandler(hub.ChatRestrictionsLockPlayers));
        dispatcher.Register(CommandNames.ChatRestrictionsUnlockPlayers, new DelegateCommandHandler(hub.ChatRestrictionsUnlockPlayers));

        dispatcher.Register(CommandNames.AudioStateGet, new DelegateCommandHandler(hub.AudioStateGet));
        dispatcher.Register(CommandNames.AudioStateSync, new DelegateCommandHandler(hub.AudioStateSync));
        dispatcher.Register(CommandNames.AudioModeGet, new DelegateCommandHandler(hub.AudioModeGet));
        dispatcher.Register(CommandNames.AudioModeSet, new DelegateCommandHandler(hub.AudioModeSet));
        dispatcher.Register(CommandNames.AudioOverrideClear, new DelegateCommandHandler(hub.AudioOverrideClear));

        dispatcher.Register(CommandNames.AudioLibraryGet, new DelegateCommandHandler(hub.AudioLibraryGet));
        dispatcher.Register(CommandNames.AudioTrackSelect, new DelegateCommandHandler(hub.AudioTrackSelect));
        dispatcher.Register(CommandNames.AudioTrackNext, new DelegateCommandHandler(hub.AudioTrackNext));
        dispatcher.Register(CommandNames.AudioTrackReload, new DelegateCommandHandler(hub.AudioTrackReload));

        dispatcher.Register(CommandNames.AudioClientSettingsGet, new DelegateCommandHandler(hub.AudioClientSettingsGet));
        dispatcher.Register(CommandNames.AudioClientSettingsSet, new DelegateCommandHandler(hub.AudioClientSettingsSet));

        dispatcher.Register(CommandNames.VisibilityGet, new DelegateCommandHandler(hub.VisibilityGet));
        dispatcher.Register(CommandNames.VisibilityUpdate, new DelegateCommandHandler(hub.VisibilityUpdate));
        dispatcher.Register(CommandNames.CharacterPublicViewGet, new DelegateCommandHandler(hub.CharacterPublicViewGet));
        dispatcher.Register(CommandNames.CharacterVisibleToMeGet, new DelegateCommandHandler(hub.CharacterVisibleToMeGet));

        dispatcher.Register(CommandNames.NotesCreate, new DelegateCommandHandler(hub.NotesCreate));
        dispatcher.Register(CommandNames.NotesList, new DelegateCommandHandler(hub.NotesList));
        dispatcher.Register(CommandNames.NotesGet, new DelegateCommandHandler(hub.NotesGet));
        dispatcher.Register(CommandNames.NotesUpdate, new DelegateCommandHandler(hub.NotesUpdate));
        dispatcher.Register(CommandNames.NotesArchive, new DelegateCommandHandler(hub.NotesArchive));

        dispatcher.Register(CommandNames.ReferenceList, new DelegateCommandHandler(hub.ReferenceList));
        dispatcher.Register(CommandNames.ReferenceGet, new DelegateCommandHandler(hub.ReferenceGet));
        dispatcher.Register(CommandNames.ReferenceCreate, new DelegateCommandHandler(hub.ReferenceCreate));
        dispatcher.Register(CommandNames.ReferenceUpdate, new DelegateCommandHandler(hub.ReferenceUpdate));
        dispatcher.Register(CommandNames.ReferenceArchive, new DelegateCommandHandler(hub.ReferenceArchive));
        dispatcher.Register(CommandNames.ReferenceReload, new DelegateCommandHandler(hub.ReferenceReload));

        dispatcher.Register(CommandNames.UpdateVersionGet, new DelegateCommandHandler(hub.UpdateVersionGet));
        dispatcher.Register(CommandNames.UpdateManifestGet, new DelegateCommandHandler(hub.UpdateManifestGet));
        dispatcher.Register(CommandNames.UpdateClientDownloadInfo, new DelegateCommandHandler(hub.UpdateClientDownloadInfo));

        dispatcher.Register(CommandNames.BackupCreate, new DelegateCommandHandler(hub.BackupCreate));
        dispatcher.Register(CommandNames.BackupList, new DelegateCommandHandler(hub.BackupList));
        dispatcher.Register(CommandNames.BackupRestore, new DelegateCommandHandler(hub.BackupRestore));
        dispatcher.Register(CommandNames.BackupExport, new DelegateCommandHandler(hub.BackupExport));

        dispatcher.Register(CommandNames.AdminLocksList, new DelegateCommandHandler(hub.AdminLocksList));
        dispatcher.Register(CommandNames.AdminLocksForceRelease, new DelegateCommandHandler(hub.AdminLocksForceRelease));
        dispatcher.Register(CommandNames.AdminServerStatus, new DelegateCommandHandler(hub.AdminServerStatus));
        dispatcher.Register(CommandNames.AdminSessionsList, new DelegateCommandHandler(hub.AdminSessionsList));
        dispatcher.Register(CommandNames.AdminDiagnosticsGet, new DelegateCommandHandler(hub.AdminDiagnosticsGet));

        dispatcher.Register(CommandNames.LockAcquire, new DelegateCommandHandler(hub.LockAcquire));
        dispatcher.Register(CommandNames.LockRelease, new DelegateCommandHandler(hub.LockRelease));
        dispatcher.Register(CommandNames.LockForceRelease, new DelegateCommandHandler(hub.LockForceRelease));
        dispatcher.Register(CommandNames.LockStatus, new DelegateCommandHandler(hub.LockStatus));

        return new ServerRuntime(dispatcher, sessions);
    }
}
