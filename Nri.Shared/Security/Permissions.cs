using System;
using System.Collections.Generic;
using Nri.Shared.Domain;

namespace Nri.Shared.Security;

public static class PermissionCodes
{
    public const string SessionView = "session.view";
    public const string CharacterManage = "character.manage";
    public const string RequestModerate = "request.moderate";
    public const string CombatManage = "combat.manage";
    public const string ChatModerate = "chat.moderate";
    public const string AudioManage = "audio.manage";
    public const string AdminPanel = "admin.panel";
}

public static class AccessPolicy
{
    private static readonly Dictionary<UserRole, HashSet<string>> RolePermissions = new Dictionary<UserRole, HashSet<string>>
    {
        {
            UserRole.Player,
            new HashSet<string> { PermissionCodes.SessionView }
        },
        {
            UserRole.Observer,
            new HashSet<string> { PermissionCodes.SessionView }
        },
        {
            UserRole.Admin,
            new HashSet<string>
            {
                PermissionCodes.SessionView,
                PermissionCodes.CharacterManage,
                PermissionCodes.RequestModerate,
                PermissionCodes.CombatManage,
                PermissionCodes.ChatModerate,
                PermissionCodes.AudioManage,
                PermissionCodes.AdminPanel
            }
        },
        {
            UserRole.SuperAdmin,
            new HashSet<string>
            {
                PermissionCodes.SessionView,
                PermissionCodes.CharacterManage,
                PermissionCodes.RequestModerate,
                PermissionCodes.CombatManage,
                PermissionCodes.ChatModerate,
                PermissionCodes.AudioManage,
                PermissionCodes.AdminPanel,
                "*"
            }
        }
    };

    public static bool HasPermission(IEnumerable<UserRole> roles, string permission)
    {
        foreach (var role in roles)
        {
            if (!RolePermissions.ContainsKey(role))
            {
                continue;
            }

            var permissions = RolePermissions[role];
            if (permissions.Contains("*") || permissions.Contains(permission))
            {
                return true;
            }
        }

        return false;
    }
}
