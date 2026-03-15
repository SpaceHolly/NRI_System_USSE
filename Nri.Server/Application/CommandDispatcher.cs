using System;
using System.Collections.Generic;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Contracts;

namespace Nri.Server.Application;

public class CommandContext
{
    public string ConnectionId { get; set; } = string.Empty;
    public RequestEnvelope Request { get; set; } = new RequestEnvelope();
    public AuthSession? Session { get; set; }
}

public interface ICommandHandler
{
    ResponseEnvelope Handle(CommandContext context);
public interface ICommandHandler
{
    ResponseEnvelope Handle(RequestEnvelope request);
}

public class CommandDispatcher
{
    private readonly IServerLogger _logger;
    private readonly SessionManager _sessionManager;
    private readonly Dictionary<string, ICommandHandler> _handlers = new Dictionary<string, ICommandHandler>();
    private readonly HashSet<string> _anonymousCommands = new HashSet<string>
    {
        CommandNames.AuthRegister,
        CommandNames.AuthLogin
    };

    public CommandDispatcher(IServerLogger logger, SessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
    private readonly Dictionary<string, ICommandHandler> _handlers = new Dictionary<string, ICommandHandler>();

    public CommandDispatcher(IServerLogger logger)
    {
        _logger = logger;
    }

    public void Register(string command, ICommandHandler handler)
    {
        _handlers[command] = handler;
    }

    public ResponseEnvelope Dispatch(string connectionId, RequestEnvelope request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Command))
            {
                return Error(request.RequestId, ResponseStatus.ValidationFailed, ErrorCode.InvalidRequest, "Command is required.");
            }

            if (!_handlers.ContainsKey(request.Command))
            {
                return Error(request.RequestId, ResponseStatus.Error, ErrorCode.InvalidCommand, $"Unsupported command: {request.Command}");
            }

            var context = new CommandContext
            {
                ConnectionId = connectionId,
                Request = request
            };

            if (!_anonymousCommands.Contains(request.Command))
            {
                AuthSession? session;
                if (!_sessionManager.TryResolve(request.AuthToken, out session) || session == null)
                {
                    return Error(request.RequestId, ResponseStatus.Unauthorized, ErrorCode.Unauthorized, "Auth token is invalid.");
                }

                context.Session = session;
            }

            var response = _handlers[request.Command].Handle(context);
            response.RequestId = request.RequestId;
            return response;
        }
        catch (System.Collections.Generic.KeyNotFoundException ex)
        {
            _logger.Debug($"NotFound for {request.Command}: {ex.Message}");
            return Error(request.RequestId, ResponseStatus.NotFound, ErrorCode.NotFound, ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.Debug($"Validation error for {request.Command}: {ex.Message}");
            return Error(request.RequestId, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.Debug($"Conflict error for {request.Command}: {ex.Message}");
            return Error(request.RequestId, ResponseStatus.Conflict, ErrorCode.Conflict, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Admin($"Unauthorized command attempt {request.Command}: {ex.Message}");
            return Error(request.RequestId, ResponseStatus.Unauthorized, ErrorCode.Unauthorized, ex.Message);
            return Error(request.RequestId, ResponseStatus.Forbidden, ErrorCode.Forbidden, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Debug($"Dispatcher internal error: {ex}");
            return Error(request.RequestId, ResponseStatus.Error, ErrorCode.InternalError, "Internal server error.");
        }
    }

    private static ResponseEnvelope Error(string requestId, ResponseStatus status, ErrorCode code, string message)
    {
        return new ResponseEnvelope
        {
            RequestId = requestId,
            Status = status,
            ErrorCode = code,
            Message = message
        };
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
