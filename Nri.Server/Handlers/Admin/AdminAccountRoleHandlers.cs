using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.Application;
using Nri.Server.Application.Services;
using Nri.Server.Infrastructure;
using Nri.Server.Transport;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.Server.Handlers.Admin;

public sealed class AdminAccountRoleHandlers
{
    private readonly INriRepositoryFactory _repositories;
    private readonly AccountRoleService _roleService;

    public AdminAccountRoleHandlers(INriRepositoryFactory repositories, AccountRoleService roleService)
    {
        _repositories = repositories;
        _roleService = roleService;
    }

    public IEnumerable<IRequestHandler> CreateHandlers()
    {
        return new IRequestHandler[]
        {
            new DelegateRequestHandler(CommandNames.AdminAccountRolesSet, HandleSetRoles),
            new DelegateRequestHandler(CommandNames.AdminAccountGrantAdmin, HandleGrantAdmin),
            new DelegateRequestHandler(CommandNames.AdminAccountRevokeAdmin, HandleRevokeAdmin)
        };
    }

    private ResponseEnvelope HandleSetRoles(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        var accountId = RequireString(context.Request.Payload, "accountId");
        var roles = ReadRoles(context.Request.Payload).ToArray();
        var result = _roleService.SetRoles(accountId, roles, actor.Id);
        return Ok("Account roles updated.", accountId, result);
    }

    private ResponseEnvelope HandleGrantAdmin(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        var accountId = RequireString(context.Request.Payload, "accountId");
        var result = _roleService.GrantAdmin(accountId, actor.Id);
        return Ok("Admin role granted.", accountId, result);
    }

    private ResponseEnvelope HandleRevokeAdmin(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        var accountId = RequireString(context.Request.Payload, "accountId");
        var result = _roleService.RevokeAdmin(accountId, actor.Id);
        return Ok("Admin role revoked.", accountId, result);
    }

    private UserAccount RequireSuperAdmin(CommandContext context)
    {
        if (context.Session == null) throw new UnauthorizedAccessException("Session is required.");
        var actor = _repositories.Accounts.GetById(context.Session.UserId) ?? throw new KeyNotFoundException("Account not found.");
        RoleGuard.EnsureRole(actor, UserRole.SuperAdmin);
        return actor;
    }

    private static IEnumerable<UserRole> ReadRoles(IDictionary<string, object> payload)
    {
        if (!payload.TryGetValue("roles", out var raw) || !(raw is IEnumerable items)) return new[] { UserRole.Player };
        foreach (var item in items)
        {
            if (Enum.TryParse<UserRole>(Convert.ToString(item), true, out var role))
            {
                yield return role;
            }
        }
    }

    private static string RequireString(IDictionary<string, object> map, string key)
    {
        var value = map.TryGetValue(key, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{key} is required.");
        return value;
    }

    private static ResponseEnvelope Ok(string message, string accountId, IReadOnlyCollection<UserRole> roles)
    {
        return new ResponseEnvelope
        {
            Status = ResponseStatus.Ok,
            Message = message,
            Payload = new Dictionary<string, object>
            {
                { "accountId", accountId },
                { "roles", roles.Select(role => role.ToString()).Cast<object>().ToArray() }
            }
        };
    }

    private sealed class DelegateRequestHandler : IRequestHandler
    {
        private readonly Func<CommandContext, ResponseEnvelope> _handler;

        public DelegateRequestHandler(string commandName, Func<CommandContext, ResponseEnvelope> handler)
        {
            CommandName = commandName;
            _handler = handler;
        }

        public string CommandName { get; }

        public ResponseEnvelope Handle(CommandContext context) => _handler(context);
    }
}
