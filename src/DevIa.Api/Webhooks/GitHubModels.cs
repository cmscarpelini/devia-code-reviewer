using System.Text.Json.Serialization;
using DevIa.Application.Identity;
using DevIa.Application.PullRequests;

namespace DevIa.Api.Webhooks;

/// <summary>
/// Minimal projection of the GitHub <c>pull_request</c> webhook payload — only the fields
/// needed by slice B1. Mapped to the provider-neutral <see cref="PullRequestWebhookInput"/>.
/// </summary>
internal sealed record GitHubPullRequestEvent(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("repository")] GitHubRepository Repository,
    [property: JsonPropertyName("pull_request")] GitHubPullRequestPayload PullRequest)
{
    public PullRequestWebhookInput ToInput() => new(
        Action: Action,
        GithubOrgId: Repository.Owner.Id,
        OrgName: Repository.Owner.Login,
        GithubRepoId: Repository.Id,
        RepoFullName: Repository.FullName,
        DefaultBranch: Repository.DefaultBranch,
        PrNumber: PullRequest.Number,
        Title: PullRequest.Title,
        BaseBranch: PullRequest.Base.Ref,
        Url: PullRequest.HtmlUrl,
        State: PullRequest.State,
        HeadSha: PullRequest.Head.Sha,
        AuthorGithubId: PullRequest.User.Id,
        AuthorLogin: PullRequest.User.Login);
}

internal sealed record GitHubRepository(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("default_branch")] string DefaultBranch,
    [property: JsonPropertyName("owner")] GitHubOwner Owner);

internal sealed record GitHubOwner(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("login")] string Login);

internal sealed record GitHubPullRequestPayload(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("head")] GitHubRef Head,
    [property: JsonPropertyName("base")] GitHubRef Base,
    [property: JsonPropertyName("user")] GitHubUser User);

internal sealed record GitHubRef(
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("ref")] string Ref);

internal sealed record GitHubUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("login")] string Login);

/// <summary>
/// Projection of the GitHub <c>installation</c> / <c>installation_repositories</c> payloads.
/// The affected repositories depend on the action.
/// </summary>
internal sealed record GitHubInstallationEvent(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("installation")] GitHubInstallation Installation,
    [property: JsonPropertyName("repositories")] GitHubRepoRef[]? Repositories,
    [property: JsonPropertyName("repositories_added")] GitHubRepoRef[]? RepositoriesAdded,
    [property: JsonPropertyName("repositories_removed")] GitHubRepoRef[]? RepositoriesRemoved)
{
    public InstallationWebhookInput ToInput()
    {
        var affected = Action.ToLowerInvariant() switch
        {
            "added" => RepositoriesAdded,
            "removed" => RepositoriesRemoved,
            _ => Repositories
        } ?? [];

        var repositories = affected
            .Select(r => new InstallationRepositoryInput(r.Id, r.FullName))
            .ToList();

        return new InstallationWebhookInput(
            Action, Installation.Id, Installation.Account.Id, Installation.Account.Login, repositories);
    }
}

internal sealed record GitHubInstallation(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("account")] GitHubOwner Account);

internal sealed record GitHubRepoRef(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("full_name")] string FullName);
