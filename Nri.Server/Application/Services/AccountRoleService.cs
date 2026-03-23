using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.Audit;
using Nri.Server.Infrastructure;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public sealed class AccountRoleService
{
    private readonly INriRepositoryFactory _repositories;
    private readonly AuditLogService _auditLogService;

    public AccountRoleService(INriRepositoryFactory repositories, AuditLogService auditLogService)
    {
        _repositories = repositories;
        _auditLogService = auditLogService;
    }

    public IReadOnlyCollection<UserRole> SetRoles(string accountId, IEnumerable<UserRole> roles, string actorUserId)
    {
        var account = _repositories.Accounts.GetById(accountId) ?? throw new KeyNotFoundException("Account not found.");
        account.Roles = roles.Distinct().ToList();
        if (account.Roles.Count == 0)
        {
            account.Roles.Add(UserRole.Player);
        }

        _repositories.Accounts.Replace(account);
        _auditLogService.Write("account.roles", actorUserId, "set", accountId, string.Join(",", account.Roles));
        return account.Roles.ToArray();
    }

    public IReadOnlyCollection<UserRole> GrantAdmin(string accountId, string actorUserId)
    {
        var account = _repositories.Accounts.GetById(accountId) ?? throw new KeyNotFoundException("Account not found.");
        if (!account.Roles.Contains(UserRole.Admin)) account.Roles.Add(UserRole.Admin);
        _repositories.Accounts.Replace(account);
        _auditLogService.Write("account.roles", actorUserId, "grantAdmin", accountId, string.Join(",", account.Roles));
        return account.Roles.ToArray();
    }

    public IReadOnlyCollection<UserRole> RevokeAdmin(string accountId, string actorUserId)
    {
        var account = _repositories.Accounts.GetById(accountId) ?? throw new KeyNotFoundException("Account not found.");
        account.Roles = account.Roles.Where(role => role != UserRole.Admin && role != UserRole.SuperAdmin).Distinct().ToList();
        if (account.Roles.Count == 0) account.Roles.Add(UserRole.Player);
        _repositories.Accounts.Replace(account);
        _auditLogService.Write("account.roles", actorUserId, "revokeAdmin", accountId, string.Join(",", account.Roles));
        return account.Roles.ToArray();
    }
}
