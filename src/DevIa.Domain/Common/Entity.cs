namespace DevIa.Domain.Common;

/// <summary>
/// Base type for all entities. Identity-based equality on <see cref="Id"/>.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }

    public override bool Equals(object? obj)
        => obj is Entity other
           && GetType() == other.GetType()
           && Id != default
           && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();
}
