using Memshare.Projects;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class LinkTests
{
    [Fact]
    public async Task Linking_writes_the_marker_and_keeps_it_out_of_the_projects_git()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var project = sandbox.Directory("project");
        await sandbox.GitOk(project, "init", "--quiet");
        var environment = laptop.Environment(currentDirectory: sandbox.Directory("project", "src"));

        var link = await ProjectLinker.LinkAsync(environment, sandbox.Git, "my-app", null, TestContext.Current.CancellationToken);
        await ProjectLinker.LinkAsync(environment, sandbox.Git, "my-app", null, TestContext.Current.CancellationToken);

        Assert.Equal(new ProjectLink(project, "notes", "my-app"), link);
        Assert.Equal(link, ProjectLink.Read(project));
        var status = await sandbox.GitOk(project, "status", "--porcelain", "--untracked-files=all");
        Assert.Empty(status.Lines);
        var exclude = File.ReadAllLines(Path.Combine(project, ".git", "info", "exclude"));
        Assert.Single(exclude, l => l == ".claude/memshare");
    }

    [Fact]
    public async Task Linking_outside_git_only_writes_the_marker()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var folder = sandbox.Directory("folder");

        await ProjectLinker.LinkAsync(laptop.Environment(folder), sandbox.Git, "notes-folder", "notes", TestContext.Current.CancellationToken);

        Assert.Equal("notes-folder", ProjectLink.Read(folder)?.Slug);
    }

    [Theory]
    [InlineData("My App")]
    [InlineData("../escape")]
    [InlineData("")]
    public async Task An_invalid_slug_is_refused(string slug)
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");

        await Assert.ThrowsAsync<MemshareException>(() =>
            ProjectLinker.LinkAsync(laptop.Environment(sandbox.Directory("project")), sandbox.Git, slug, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Linking_to_an_unknown_store_is_refused()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");

        var error = await Assert.ThrowsAsync<MemshareException>(() =>
            ProjectLinker.LinkAsync(laptop.Environment(sandbox.Directory("project")), sandbox.Git, "app", "work", TestContext.Current.CancellationToken));

        Assert.Contains("no store 'work'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unlinking_removes_the_marker_only()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var environment = laptop.Environment(sandbox.Directory("project"));
        await ProjectLinker.LinkAsync(environment, sandbox.Git, "app", null, TestContext.Current.CancellationToken);

        var link = await ProjectLinker.UnlinkAsync(environment, sandbox.Git, TestContext.Current.CancellationToken);

        Assert.Equal("app", link.Slug);
        Assert.Null(ProjectLink.Read(environment.CurrentDirectory));
        await Assert.ThrowsAsync<MemshareException>(() => ProjectLinker.UnlinkAsync(environment, sandbox.Git, TestContext.Current.CancellationToken));
    }
}
