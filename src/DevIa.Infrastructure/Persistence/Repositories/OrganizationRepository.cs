using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevIa.Infrastructure.Persistence.Repositories;

public sealed class OrganizationRepository(DevIaDbContext db) : IOrganizationRepository
{
    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Organizations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Organization?> GetByGithubIdAsync(long githubOrgId, CancellationToken cancellationToken = default)
        => db.Organizations.FirstOrDefaultAsync(x => x.GithubOrgId == githubOrgId, cancellationToken);

    public void Add(Organization organization) => db.Organizations.Add(organization);
}
