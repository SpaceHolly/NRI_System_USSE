using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Configuration;
using Nri.Shared.Domain;

namespace Nri.Server.Infrastructure;

public class AuthSession
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
}

public class SessionManager
{
    private readonly TokenConfig _config;
    private readonly INriRepositoryFactory _repositories;
    private readonly Dictionary<string, AuthSession> _sessions = new Dictionary<string, AuthSession>();
    private readonly object _sync = new object();

    public SessionManager(TokenConfig config, INriRepositoryFactory repositories)
    {
        _config = config;
        _repositories = repositories;
    }

    public string CreateSession(string userId, string connectionId)
    {
        var token = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var session = new AuthSession
        {
            Token = token,
            UserId = userId,
            ConnectionId = connectionId,
            CreatedUtc = now,
            ExpiresUtc = now.AddHours(_config.TokenLifetimeHours)
        };

        lock (_sync)
        {
            _sessions[token] = session;
        }

        UpsertPresence(session, true);
        return token;
    }

    public bool TryResolve(string? token, out AuthSession? session)
    {
        session = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_sessions.ContainsKey(token))
            {
                return false;
            }

            var current = _sessions[token];
            if (current.ExpiresUtc < DateTime.UtcNow)
            {
                _sessions.Remove(token);
                UpsertPresence(current, false);
                return false;
            }

            current.ExpiresUtc = DateTime.UtcNow.AddHours(_config.TokenLifetimeHours);
            current.ConnectionId = current.ConnectionId;
            session = current;
            UpsertPresence(current, true);
            return true;
        }
    }

    public void Logout(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        lock (_sync)
        {
            if (!_sessions.ContainsKey(token))
            {
                return;
            }

            var session = _sessions[token];
            _sessions.Remove(token);
            UpsertPresence(session, false);
        }
    }

    public void DisconnectByConnection(string connectionId)
    {
        List<string> toRemove;
        lock (_sync)
        {
            toRemove = _sessions.Values.Where(s => s.ConnectionId == connectionId).Select(s => s.Token).ToList();
            foreach (var token in toRemove)
            {
                var session = _sessions[token];
                _sessions.Remove(token);
                UpsertPresence(session, false);
            }
        }
    }

    private void UpsertPresence(AuthSession session, bool online)
    {
        var existing = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, session.UserId)).FirstOrDefault();
        if (existing == null)
        {
            _repositories.Presence.Insert(new SessionUserState
            {
                UserId = session.UserId,
                AuthToken = session.Token,
                ConnectionId = session.ConnectionId,
                IsOnline = online,
                LastSeenUtc = DateTime.UtcNow
            });
            return;
        }

        existing.AuthToken = session.Token;
        existing.ConnectionId = session.ConnectionId;
        existing.IsOnline = online;
        existing.LastSeenUtc = DateTime.UtcNow;
        _repositories.Presence.Replace(existing);
    }
}
