namespace DevIa.Application.PullRequests;

/// <summary>
/// Provider-neutral input for a GitHub <c>pull_request</c> webhook. The Api parses the
/// GitHub JSON into this shape so the Application stays free of GitHub-specific payloads.
/// </summary>
public sealed record PullRequestWebhookInput(
    string Action,
    long GithubOrgId,
    string OrgName,
    long GithubRepoId,
    string RepoFullName,
    string DefaultBranch,
    int PrNumber,
    string Title,
    string BaseBranch,
    string Url,
    string State,
    string HeadSha,
    long AuthorGithubId,
    string AuthorLogin);
