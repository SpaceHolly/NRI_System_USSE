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
    }

    public void Register(string command, ICommandHandler handler)
    {
        _handlers[command] = handler;
    }

    public ResponseEnvelope Dispatch(string connectionId, RequestEnvelope request)
    {
        var requestId = NormalizeRequestId(request.RequestId);
        request.RequestId = requestId;
        _logger.Session($"request.received requestId={requestId} command={request.Command} connectionId={connectionId}");
        try
        {
            if (string.IsNullOrWhiteSpace(request.Command))
            {
                _logger.Debug($"request.validation.failed requestId={requestId} reason=missing-command");
                return Error(requestId, ResponseStatus.ValidationFailed, ErrorCode.InvalidRequest, "Command is required.");
            }

            if (!_handlers.ContainsKey(request.Command))
            {
                _logger.Debug($"request.validation.failed requestId={requestId} reason=unsupported-command command={request.Command}");
                return Error(requestId, ResponseStatus.Error, ErrorCode.InvalidCommand, $"Unsupported command: {request.Command}");
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
                    _logger.Session($"request.auth.failed requestId={requestId} command={request.Command}");
                    return Error(requestId, ResponseStatus.Unauthorized, ErrorCode.Unauthorized, "Auth token is invalid.");
                }

                context.Session = session;
                _logger.Session($"request.auth.ok requestId={requestId} command={request.Command} userId={session.UserId}");
            }
            else
            {
                _logger.Session($"request.auth.skipped requestId={requestId} command={request.Command}");
            }

            _logger.Debug($"request.handler.start requestId={requestId} command={request.Command}");
            var response = _handlers[request.Command].Handle(context);
            response.RequestId = requestId;
            response.ServerUtc = DateTime.UtcNow;
            _logger.Debug($"request.handler.done requestId={requestId} command={request.Command} status={response.Status}");
            return response;
        }
        catch (System.Collections.Generic.KeyNotFoundException ex)
        {
            _logger.Debug($"request.handler.failed requestId={requestId} command={request.Command} type=NotFound message={ex.Message}");
            return Error(requestId, ResponseStatus.NotFound, ErrorCode.NotFound, ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.Debug($"request.handler.failed requestId={requestId} command={request.Command} type=Validation message={ex.Message}");
            return Error(requestId, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.Debug($"request.handler.failed requestId={requestId} command={request.Command} type=Conflict message={ex.Message}");
            return Error(requestId, ResponseStatus.Conflict, ErrorCode.Conflict, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Admin($"request.handler.failed requestId={requestId} command={request.Command} type=Unauthorized message={ex.Message}");
            return Error(requestId, ResponseStatus.Unauthorized, ErrorCode.Unauthorized, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Debug($"request.handler.failed requestId={requestId} command={request.Command} type=InternalError exception={ex}");
            return Error(requestId, ResponseStatus.Error, ErrorCode.InternalError, "Internal server error.");
        }
    }

    private static ResponseEnvelope Error(string requestId, ResponseStatus status, ErrorCode code, string message)
    {
        return new ResponseEnvelope
        {
            RequestId = requestId,
            ServerUtc = DateTime.UtcNow,
            Status = status,
            ErrorCode = code,
            Message = message
        };
    }

    private static string NormalizeRequestId(string? incoming)
    {
        return string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString("N") : incoming.Trim();
    }
}
