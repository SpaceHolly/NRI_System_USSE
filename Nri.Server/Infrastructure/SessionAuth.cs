using System;
using System.Collections.Generic;

namespace Nri.Server.Infrastructure;

public class SessionManager
{
    private readonly Dictionary<string, string> _tokenToUser = new Dictionary<string, string>();

    public string CreateSession(string userId)
    {
        var token = Guid.NewGuid().ToString("N");
        _tokenToUser[token] = userId;
        return token;
    }

    public bool TryResolveUser(string token, out string userId)
    {
        return _tokenToUser.TryGetValue(token, out userId!);
    }
}

public class AuthServiceStub
{
    public bool ValidateToken(string? token)
    {
        return !string.IsNullOrWhiteSpace(token);
    }
}
