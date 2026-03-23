using System;
using System.Collections.Generic;
using Nri.Server.Application;
using Nri.Shared.Contracts;

namespace Nri.Server.Transport;

public interface IRequestRouter
{
    ResponseEnvelope Route(CommandContext context);
}

public interface IRequestHandler
{
    string CommandName { get; }
    ResponseEnvelope Handle(CommandContext context);
}

public sealed class RequestRouter : IRequestRouter
{
    private readonly Dictionary<string, IRequestHandler> _handlers = new Dictionary<string, IRequestHandler>(StringComparer.OrdinalIgnoreCase);

    public RequestRouter(IEnumerable<IRequestHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            _handlers[handler.CommandName] = handler;
        }
    }

    public ResponseEnvelope Route(CommandContext context)
    {
        if (!_handlers.TryGetValue(context.Request.Command, out var handler))
        {
            throw new KeyNotFoundException($"Handler not registered for command '{context.Request.Command}'.");
        }

        return handler.Handle(context);
    }
}

public sealed class RoutedCommandHandler : ICommandHandler
{
    private readonly IRequestRouter _router;

    public RoutedCommandHandler(IRequestRouter router)
    {
        _router = router;
    }

    public ResponseEnvelope Handle(CommandContext context)
    {
        return _router.Route(context);
    }
}
