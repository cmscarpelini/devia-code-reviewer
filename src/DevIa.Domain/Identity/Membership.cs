using DevIa.Domain.Common;
using DevIa.Domain.Enums;

namespace DevIa.Domain.Identity;

/// <summary>Links a <see cref="User"/> to an <see cref="Organization"/> with a role (RBAC).</summary>
public sealed class Membership : Entity
{
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public MembershipRole Role { get; private set; }

    private Membership() { } // EF

    public Membership(Guid organizationId, Guid userId, MembershipRole role)
    {
        if (organizationId == default) throw new DomainException("OrganizationId is required.");
        if (userId == default) throw new DomainException("UserId is required.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
    }

    public void ChangeRole(MembershipRole role) => Role = role;
}
