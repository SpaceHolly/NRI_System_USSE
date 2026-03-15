using Nri.Shared.Contracts;

namespace Nri.Server.Application;

public interface IStubService
{
    ResponseEnvelope Handle(RequestEnvelope request);
}

public class AccountService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "AccountService");
}

public class CharacterService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "CharacterService");
}

public class RequestService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "RequestService");
}

public class CombatService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "CombatService");
}

public class ChatService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "ChatService");
}

public class AudioService : IStubService
{
    public ResponseEnvelope Handle(RequestEnvelope request) => StubResponses.NotImplemented(request, "AudioService");
}

public static class StubResponses
{
    public static ResponseEnvelope NotImplemented(RequestEnvelope request, string source)
    {
        return new ResponseEnvelope
        {
            RequestId = request.RequestId,
            Status = ResponseStatus.Error,
            ErrorCode = ErrorCode.Unknown,
            Message = $"{source} is a stub and will be implemented in next stages."
        };
    }
}
