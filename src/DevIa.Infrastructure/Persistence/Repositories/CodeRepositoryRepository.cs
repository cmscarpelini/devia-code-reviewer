using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevIa.Infrastructure.Persistence.Repositories;

public sealed class CodeRepositoryRepository(DevIaDbContext db) : ICodeRepositoryRepository
{
    public Task<CodeRepository?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Repositories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<CodeRepository?> GetByGithubIdAsync(long githubRepoId, CancellationToken cancellationToken = default)
        => db.Repositories.FirstOrDefaultAsync(x => x.GithubRepoId == githubRepoId, cancellationToken);

    public Task<CodeRepository?> GetByFullNameAsync(string fullName, CancellationToken cancellationToken = default)
        => db.Repositories.FirstOrDefaultAsync(x => x.FullName == fullName, cancellationToken);

    public async Task<IReadOnlyList<CodeRepository>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
        => await db.Repositories
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CodeRepository>> ListAllAsync(CancellationToken cancellationToken = default)
        => await db.Repositories.OrderBy(x => x.FullName).ToListAsync(cancellationToken);

    public void Add(CodeRepository repository) => db.Repositories.Add(repository);
}
