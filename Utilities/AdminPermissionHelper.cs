using System;
using System.Collections.Generic;
using System.Linq;
using SProtectAgentWeb.Api.Configuration;

namespace SProtectAgentWeb.Api.Utilities;

public class AdminPermissionHelper
{
    private readonly HashSet<string> _superUsers;

    public AdminPermissionHelper(AppConfig config)
    {
        _superUsers = config.Server
            .GetSuperUsers()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool HasSuperPermission(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        return _superUsers.Contains(username);
    }
}
