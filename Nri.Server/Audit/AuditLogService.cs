using System;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Audit;

public sealed class AuditLogService
{
    private readonly INriRepositoryFactory _repositories;
    private readonly IServerLogger _logger;

    public AuditLogService(INriRepositoryFactory repositories, IServerLogger logger)
    {
        _repositories = repositories;
        _logger = logger;
    }

    public void Write(string category, string actorUserId, string action, string target, string details)
    {
        _repositories.AuditLogs.Insert(new AuditLogEntry
        {
            Category = category,
            ActorUserId = actorUserId,
            Action = action,
            Target = target,
            DetailsJson = details ?? string.Empty,
            CreatedUtc = DateTime.UtcNow
        });

        _logger.Audit($"{category}:{action} actor={actorUserId} target={target} details={details}");
    }
}
