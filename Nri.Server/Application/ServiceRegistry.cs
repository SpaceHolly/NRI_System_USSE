using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Contracts;

namespace Nri.Server.Application;

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
