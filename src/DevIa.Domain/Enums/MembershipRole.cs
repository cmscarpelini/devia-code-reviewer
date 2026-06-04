namespace DevIa.Domain.Enums;

/// <summary>A user's role within an organization (RBAC; full model matures in Phase 2).</summary>
public enum MembershipRole
{
    Developer,
    Reviewer,
    TechLead,
    Admin
}
