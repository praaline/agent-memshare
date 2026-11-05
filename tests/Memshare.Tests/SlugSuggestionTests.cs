using Memshare.Projects;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class SlugSuggestionTests
{
    [Theory]
    [InlineData("agent-memshare", "agent-memshare")]
    [InlineData("My Project", "my-project")]
    [InlineData("Foo.Bar_Baz", "foo-bar-baz")]
    [InlineData("--x--", "x")]
    public void Names_become_slugs(string name, string slug) => Assert.Equal(slug, SlugSuggestion.Slugify(name));

    [Fact]
    public async Task The_git_remote_names_the_project()
    {
        using var sandbox = new Sandbox();
        var project = sandbox.Directory("checkout");
        await sandbox.GitOk(project, "init", "--quiet");
        await sandbox.GitOk(project, "remote", "add", "origin", "git@git.example.invalid:me/Web-Shop.git");

        Assert.Equal("web-shop", await SlugSuggestion.SuggestAsync(sandbox.Git, project, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Without_a_remote_the_directory_names_the_project()
    {
        using var sandbox = new Sandbox();
        var project = sandbox.Directory("Notes Folder");

        Assert.Equal("notes-folder", await SlugSuggestion.SuggestAsync(sandbox.Git, project, TestContext.Current.CancellationToken));
    }
}
