using Nri.Shared.Configuration;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.Networking;

public class ClientSessionState
{
    public string? AuthToken { get; set; }
    public string? SessionId { get; set; }
}

public interface IJsonTcpClient
{
    ResponseEnvelope Send(RequestEnvelope request);
}

public class JsonTcpClientStub : IJsonTcpClient
{
    private readonly ClientConfig _config;

    public JsonTcpClientStub(ClientConfig config)
    {
        _config = config;
    }

    public ResponseEnvelope Send(RequestEnvelope request)
    {
        return new ResponseEnvelope
        {
            RequestId = request.RequestId,
            Status = ResponseStatus.Error,
            ErrorCode = ErrorCode.Unknown,
            Message = $"Player client TCP stub ({_config.ServerHost}:{_config.ServerPort})"
        };
    }
}
