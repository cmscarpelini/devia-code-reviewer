using DevIa.Domain.Common;
using DevIa.Domain.Enums;
using DevIa.Domain.Identity;
using DevIa.Domain.Reviews;

namespace DevIa.UnitTests.Domain;

public class EntityInvariantTests
{
    [Fact]
    public void User_requires_positive_github_id()
        => Assert.Throws<DomainException>(() => new User(0, "octocat"));

    [Fact]
    public void User_requires_login()
        => Assert.Throws<DomainException>(() => new User(1, ""));

    [Fact]
    public void CodeRepository_starts_active_and_can_be_deactivated()
    {
        var repo = new CodeRepository(Guid.NewGuid(), 99, "org/repo", "main");
        Assert.True(repo.IsActive);

        repo.Deactivate();
        Assert.False(repo.IsActive);

        repo.Activate();
        Assert.True(repo.IsActive);
    }

    [Fact]
    public void Finding_requires_title_and_description()
    {
        Assert.Throws<DomainException>(() =>
            new Finding(Severity.Minor, FindingCategory.Style, "f.cs", 1, title: "", description: "d"));
        Assert.Throws<DomainException>(() =>
            new Finding(Severity.Minor, FindingCategory.Style, "f.cs", 1, title: "t", description: ""));
    }

    [Fact]
    public void Finding_line_cannot_be_negative()
        => Assert.Throws<DomainException>(() =>
            new Finding(Severity.Minor, FindingCategory.Style, "f.cs", -1, "t", "d"));

    [Fact]
    public void Entities_have_identity_based_equality()
    {
        var a = new User(1, "octocat");
        var b = new User(2, "hubber");

        Assert.Equal(a, a);
        Assert.NotEqual(a, b);
    }
}
