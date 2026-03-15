using System;
using System.Collections.Generic;
using Nri.Server.Logging;
using Nri.Shared.Contracts;

namespace Nri.Server.Application;

public interface ICommandHandler
{
    ResponseEnvelope Handle(RequestEnvelope request);
}

public class CommandDispatcher
{
    private readonly IServerLogger _logger;
    private readonly Dictionary<string, ICommandHandler> _handlers = new Dictionary<string, ICommandHandler>();

    public CommandDispatcher(IServerLogger logger)
    {
        _logger = logger;
    }

    public void Register(string command, ICommandHandler handler)
    {
        _handlers[command] = handler;
    }

    public ResponseEnvelope Dispatch(RequestEnvelope request)
    {
        if (!_handlers.ContainsKey(request.Command))
        {
            return new ResponseEnvelope
            {
                RequestId = request.RequestId,
                Status = ResponseStatus.Error,
                ErrorCode = ErrorCode.InvalidCommand,
                Message = $"Command '{request.Command}' is not registered."
            };
        }

        _logger.Session($"Dispatch command: {request.Command} ({request.RequestId})");
        return _handlers[request.Command].Handle(request);
    }
}
