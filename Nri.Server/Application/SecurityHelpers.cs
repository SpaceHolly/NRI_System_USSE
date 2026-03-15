using System;
using System.Linq;
using System.Security.Cryptography;
using Nri.Shared.Domain;
using Nri.Shared.Security;

namespace Nri.Server.Application;

public static class PasswordHasher
{
    public static string CreateSalt()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            var salt = new byte[16];
            rng.GetBytes(salt);
            return Convert.ToBase64String(salt);
        }
    }

    public static string Hash(string password, string salt)
    {
        var pbkdf2 = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 100000, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }
}

public static class RoleGuard
{
    public static void EnsureRole(UserAccount account, params UserRole[] roles)
    {
        if (roles.Any(r => account.Roles.Contains(r)))
        {
            return;
        }

        throw new UnauthorizedAccessException("Insufficient role.");
    }

    public static void EnsurePermission(UserAccount account, string permission)
    {
        if (!AccessPolicy.HasPermission(account.Roles, permission))
        {
            throw new UnauthorizedAccessException($"Permission '{permission}' is required.");
        }
    }
}
