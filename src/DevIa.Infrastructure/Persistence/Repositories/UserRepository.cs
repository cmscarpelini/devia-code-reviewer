using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevIa.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(DevIaDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<User?> GetByGithubIdAsync(long githubUserId, CancellationToken cancellationToken = default)
        => db.Users.FirstOrDefaultAsync(x => x.GithubUserId == githubUserId, cancellationToken);

    public void Add(User user) => db.Users.Add(user);
}
