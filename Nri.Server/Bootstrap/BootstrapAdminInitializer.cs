using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Application;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Configuration;
using Nri.Shared.Domain;

namespace Nri.Server.Bootstrap;

public static class BootstrapAdminInitializer
{
    public static void Ensure(ServerConfig config, INriRepositoryFactory repositories, IServerLogger logger)
    {
        var adminExists = repositories.Accounts.Find(FilterDefinition<UserAccount>.Empty)
            .Any(account => !account.Archived && !account.Deleted && account.Roles.Any(role => role == UserRole.Admin || role == UserRole.SuperAdmin));

        if (adminExists)
        {
            logger.Debug("BootstrapAdmin skipped: admin or superadmin already exists.");
            return;
        }

        var bootstrap = config.BootstrapAdmin;
        if (!bootstrap.Enabled)
        {
            logger.Debug("BootstrapAdmin skipped: disabled in configuration.");
            return;
        }

        if (string.IsNullOrWhiteSpace(bootstrap.Login))
        {
            throw new InvalidOperationException("BootstrapAdmin.Login is required when bootstrap is enabled.");
        }

        var existing = repositories.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Login, bootstrap.Login)).FirstOrDefault();
        if (existing != null)
        {
            if (!bootstrap.PromoteExistingUser)
            {
                throw new InvalidOperationException($"BootstrapAdmin login '{bootstrap.Login}' already exists and PromoteExistingUser=false.");
            }

            EnsureProfile(existing, repositories);
            PromoteToBootstrapSuperAdmin(existing, repositories);
            logger.Admin($"Bootstrap superadmin promoted for existing user '{existing.Login}'.");
            repositories.AuditLogs.Insert(new AuditLogEntry { Category = "bootstrap-admin", ActorUserId = existing.Id, Action = "promote", Target = existing.Id, DetailsJson = existing.Login });
            return;
        }

        if (string.IsNullOrWhiteSpace(bootstrap.Password))
        {
            throw new InvalidOperationException("BootstrapAdmin.Password is required when creating the initial superadmin.");
        }

        var profile = new UserProfile();
        repositories.Profiles.Insert(profile);

        var salt = PasswordHasher.CreateSalt();
        var account = new UserAccount
        {
            Login = bootstrap.Login,
            PasswordSalt = salt,
            PasswordHash = PasswordHasher.Hash(bootstrap.Password, salt),
            ProfileId = profile.Id,
            Status = AccountStatus.Active,
            Roles = new List<UserRole> { UserRole.SuperAdmin }
        };
        repositories.Accounts.Insert(account);
        profile.UserAccountId = account.Id;
        repositories.Profiles.Replace(profile);

        logger.Admin($"Bootstrap superadmin created for login '{account.Login}'.");
        repositories.AuditLogs.Insert(new AuditLogEntry { Category = "bootstrap-admin", ActorUserId = account.Id, Action = "create", Target = account.Id, DetailsJson = account.Login });
    }

    private static void PromoteToBootstrapSuperAdmin(UserAccount account, INriRepositoryFactory repositories)
    {
        account.Status = AccountStatus.Active;
        account.Archived = false;
        account.Deleted = false;
        if (!account.Roles.Contains(UserRole.SuperAdmin))
        {
            account.Roles.Add(UserRole.SuperAdmin);
        }
        if (!account.Roles.Contains(UserRole.Admin))
        {
            account.Roles.Add(UserRole.Admin);
        }

        account.Roles = account.Roles.Distinct().ToList();
        repositories.Accounts.Replace(account);
    }

    private static void EnsureProfile(UserAccount account, INriRepositoryFactory repositories)
    {
        if (!string.IsNullOrWhiteSpace(account.ProfileId) && repositories.Profiles.GetById(account.ProfileId) != null)
        {
            return;
        }

        var profile = new UserProfile { UserAccountId = account.Id };
        repositories.Profiles.Insert(profile);
        account.ProfileId = profile.Id;
        repositories.Accounts.Replace(account);
    }
}
