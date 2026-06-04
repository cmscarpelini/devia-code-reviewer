using DevIa.Domain.Identity;

namespace DevIa.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Used for idempotent upsert on sign-in (SPEC-0002).</summary>
    Task<User?> GetByGithubIdAsync(long githubUserId, CancellationToken cancellationToken = default);

    void Add(User user);
}
