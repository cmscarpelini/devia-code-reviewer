using DevIa.Domain.Identity;

namespace DevIa.Application.Abstractions.Persistence;

/// <summary>Persistence port for the connected <see cref="CodeRepository"/> aggregate.</summary>
public interface ICodeRepositoryRepository
{
    Task<CodeRepository?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CodeRepository?> GetByGithubIdAsync(long githubRepoId, CancellationToken cancellationToken = default);

    Task<CodeRepository?> GetByFullNameAsync(string fullName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CodeRepository>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CodeRepository>> ListAllAsync(CancellationToken cancellationToken = default);

    void Add(CodeRepository repository);
}
