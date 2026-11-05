using Memshare.Projects;
using Memshare.Stores;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class SharedProjectsTests
{
    [Fact]
    public async Task Projects_are_listed_with_their_last_publish()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        laptop.Write("projects/published/MEMORY.md", "- x\n");
        await laptop.PublishAsync();
        laptop.Write("projects/draft/MEMORY.md", "- y\n");

        var projects = await SharedProjects.ListAsync(sandbox.Git, StoreCatalog.Discover(laptop.StoresRoot), TestContext.Current.CancellationToken);

        Assert.Equal(["draft", "published"], projects.Select(p => p.Slug));
        Assert.Null(projects[0].LastPublished);
        Assert.NotNull(projects[1].LastPublished);
    }
}
