using DevIa.Domain.Common;

namespace DevIa.Domain.Audit;

/// <summary>Append-only record of a state-changing action.</summary>
public sealed class AuditLog : Entity
{
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = null!;
    public string EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }
    public string? Metadata { get; private set; } // JSON payload (jsonb)
    public DateTimeOffset CreatedAt { get; private set; }

    private AuditLog() { } // EF

    public AuditLog(Guid? actorUserId, string action, string entityType, Guid entityId, string? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(action)) throw new DomainException("Action is required.");
        if (string.IsNullOrWhiteSpace(entityType)) throw new DomainException("EntityType is required.");
        if (entityId == default) throw new DomainException("EntityId is required.");

        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Metadata = metadata;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
