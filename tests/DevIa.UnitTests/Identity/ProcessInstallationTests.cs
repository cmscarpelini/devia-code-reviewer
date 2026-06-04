using DevIa.Application.Identity;
using DevIa.UnitTests.PullRequests; // reuse the repository fakes
using Microsoft.Extensions.Logging.Abstractions;

namespace DevIa.UnitTests.Identity;

public class ProcessInstallationTests
{
    private sealed class Harness
    {
        public readonly FakeOrganizationRepository Orgs = new();
        public readonly FakeCodeRepositoryRepository Repos = new();
        public readonly FakeUnitOfWork Uow = new();

        public ProcessInstallation Handler() => new(Orgs, Repos, Uow, NullLogger<ProcessInstallation>.Instance);
    }

    private static InstallationWebhookInput Input(string action, params (long Id, string FullName)[] repos) => new(
        action, InstallationId: 999, AccountGithubId: 100, AccountLogin: "acme",
        repos.Select(r => new InstallationRepositoryInput(r.Id, r.FullName)).ToList());

    [Fact]
    public async Task Created_records_the_installation_id_on_the_organization()
    {
        var ctx = new Harness();

        await ctx.Handler().HandleAsync(Input("created", (200, "acme/app")));

        var org = Assert.Single(ctx.Orgs.Items);
        Assert.Equal(999, org.InstallationId);
    }

    [Fact]
    public async Task Created_connects_org_and_active_repositories()
    {
        var ctx = new Harness();

        await ctx.Handler().HandleAsync(Input("created", (200, "acme/app"), (201, "acme/lib")));

        Assert.Single(ctx.Orgs.Items);
        Assert.Equal(2, ctx.Repos.Items.Count);
        Assert.All(ctx.Repos.Items, r => Assert.True(r.IsActive));
    }

    [Fact]
    public async Task Removed_deactivates_existing_repository_without_duplicating()
    {
        var ctx = new Harness();
        await ctx.Handler().HandleAsync(Input("created", (200, "acme/app")));

        await ctx.Handler().HandleAsync(Input("removed", (200, "acme/app")));

        var repository = Assert.Single(ctx.Repos.Items);
        Assert.False(repository.IsActive);
    }

    [Fact]
    public async Task Added_reuses_the_existing_organization()
    {
        var ctx = new Harness();
        await ctx.Handler().HandleAsync(Input("created", (200, "acme/app")));

        await ctx.Handler().HandleAsync(Input("added", (201, "acme/lib")));

        Assert.Single(ctx.Orgs.Items);
        Assert.Equal(2, ctx.Repos.Items.Count);
    }
}
