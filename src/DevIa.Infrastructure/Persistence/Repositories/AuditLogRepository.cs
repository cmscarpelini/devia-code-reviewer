using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Audit;

namespace DevIa.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(DevIaDbContext db) : IAuditLogRepository
{
    public void Add(AuditLog auditLog) => db.AuditLogs.Add(auditLog);
}
