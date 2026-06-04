using DevIa.Application.Abstractions.Persistence;

namespace DevIa.Application.Identity;

/// <summary>Activates or deactivates a connected repository (SPEC-0002 management screen).</summary>
public sealed class SetRepositoryActive(ICodeRepositoryRepository repositories, IUnitOfWork unitOfWork)
{
    /// <returns><c>true</c> if the repository was found and updated; <c>false</c> if not found.</returns>
    public async Task<bool> HandleAsync(Guid repositoryId, bool active, CancellationToken cancellationToken = default)
    {
        var repository = await repositories.GetByIdAsync(repositoryId, cancellationToken);
        if (repository is null)
            return false;

        if (active)
            repository.Activate();
        else
            repository.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
