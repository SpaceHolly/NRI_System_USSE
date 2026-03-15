using Nri.Server.Infrastructure;
using Nri.Server.Logging;
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
        var hub = new ServiceHub(repositories, sessions, logger);

        var dispatcher = new CommandDispatcher(logger, sessions);

        dispatcher.Register(CommandNames.AuthRegister, new DelegateCommandHandler(hub.Register));
        dispatcher.Register(CommandNames.AuthLogin, new DelegateCommandHandler(hub.Login));
        dispatcher.Register(CommandNames.AuthLogout, new DelegateCommandHandler(hub.Logout));
        dispatcher.Register(CommandNames.SessionValidate, new DelegateCommandHandler(hub.SessionValidate));
        dispatcher.Register(CommandNames.AuthRegister, new DelegateCommandHandler(hub.Register));
        dispatcher.Register(CommandNames.AuthLogin, new DelegateCommandHandler(hub.Login));
        dispatcher.Register(CommandNames.AuthLogout, new DelegateCommandHandler(hub.Logout));

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

        dispatcher.Register(CommandNames.LockAcquire, new DelegateCommandHandler(hub.LockAcquire));
        dispatcher.Register(CommandNames.LockRelease, new DelegateCommandHandler(hub.LockRelease));
        dispatcher.Register(CommandNames.LockForceRelease, new DelegateCommandHandler(hub.LockForceRelease));
        dispatcher.Register(CommandNames.LockStatus, new DelegateCommandHandler(hub.LockStatus));

        return new ServerRuntime(dispatcher, sessions);
        dispatcher.Register(CommandNames.SessionValidate, new DelegateCommandHandler(hub.SessionValidate));

        dispatcher.Register(CommandNames.LockAcquire, new DelegateCommandHandler(hub.LockAcquire));
        dispatcher.Register(CommandNames.LockRelease, new DelegateCommandHandler(hub.LockRelease));
        dispatcher.Register(CommandNames.LockStatus, new DelegateCommandHandler(hub.LockStatus));

        return new ServerRuntime(dispatcher, sessions);
public static class ServiceRegistry
{
    public static CommandDispatcher BuildDispatcher(IServerLogger logger)
    {
        var accountService = new AccountService();
        var characterService = new CharacterService();
        var requestService = new RequestService();
        var combatService = new CombatService();
        var chatService = new ChatService();
        var audioService = new AudioService();

        var dispatcher = new CommandDispatcher(logger);
        dispatcher.Register(CommandNames.AuthLogin, new StubCommandHandler(accountService));
        dispatcher.Register(CommandNames.CharacterGet, new StubCommandHandler(characterService));
        dispatcher.Register(CommandNames.RequestCreate, new StubCommandHandler(requestService));
        dispatcher.Register(CommandNames.CombatStart, new StubCommandHandler(combatService));
        dispatcher.Register(CommandNames.ChatSend, new StubCommandHandler(chatService));
        dispatcher.Register(CommandNames.AudioStateSet, new StubCommandHandler(audioService));
        return dispatcher;
    }
}

public class StubCommandHandler : ICommandHandler
{
    private readonly IStubService _service;

    public StubCommandHandler(IStubService service)
    {
        _service = service;
    }

    public ResponseEnvelope Handle(RequestEnvelope request)
    {
        return _service.Handle(request);
    }
}
