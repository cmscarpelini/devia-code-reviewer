using DevIa.Domain.Identity;

namespace DevIa.Application.Abstractions.Persistence;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Organization?> GetByGithubIdAsync(long githubOrgId, CancellationToken cancellationToken = default);

    void Add(Organization organization);
}
