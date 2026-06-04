namespace DevIa.Application.Reviews;

/// <summary>Fetches the unified diff for a PR version (GitHub adapter; auth matures with onboarding).</summary>
public interface IDiffSource
{
    Task<string> GetDiffAsync(string repoFullName, int prNumber, string headSha, CancellationToken cancellationToken = default);
}
