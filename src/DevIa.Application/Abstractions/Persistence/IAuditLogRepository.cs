using DevIa.Domain.Audit;

namespace DevIa.Application.Abstractions.Persistence;

/// <summary>Append-only audit trail writes.</summary>
public interface IAuditLogRepository
{
    void Add(AuditLog auditLog);
}
