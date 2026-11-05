using Memshare.Stores;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class StoreTests
{
    [Fact]
    public void Only_git_repositories_under_the_root_are_stores()
    {
        using var sandbox = new Sandbox();
        var root = sandbox.Directory("stores");
        sandbox.Directory("stores", "work", ".git");
        sandbox.Directory("stores", "personal", ".git");
        sandbox.Directory("stores", "not-a-store");

        var catalog = StoreCatalog.Discover(root);

        Assert.Equal(["personal", "work"], catalog.Stores.Select(s => s.Name));
    }

    [Fact]
    public void A_missing_root_has_no_stores()
    {
        using var sandbox = new Sandbox();

        Assert.Empty(StoreCatalog.Discover(Path.Combine(sandbox.Root, "nowhere")).Stores);
    }

    [Fact]
    public void Picking_without_a_name_needs_exactly_one_store()
    {
        using var sandbox = new Sandbox();
        var root = sandbox.Directory("stores");
        Assert.Throws<MemshareException>(() => StoreCatalog.Discover(root).Pick(null));

        sandbox.Directory("stores", "work", ".git");
        Assert.Equal("work", StoreCatalog.Discover(root).Pick(null).Name);

        sandbox.Directory("stores", "personal", ".git");
        var error = Assert.Throws<MemshareException>(() => StoreCatalog.Discover(root).Pick(null));
        Assert.Contains("personal, work", error.Message, StringComparison.Ordinal);
        Assert.Equal("personal", StoreCatalog.Discover(root).Pick("personal").Name);
        Assert.Throws<MemshareException>(() => StoreCatalog.Discover(root).Pick("other"));
    }

    [Fact]
    public async Task Unpublished_counts_changed_files_and_unpushed_commits()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var store = new Store("notes", await remote.CloneAsync("laptop"));
        Task<int> Unpublished() => StoreState.CountUnpublishedAsync(sandbox.Git, store, CancellationToken.None);

        Assert.Equal(0, await Unpublished());

        File.WriteAllText(Path.Combine(store.Directory, "a.md"), "a\n");
        File.WriteAllText(Path.Combine(store.Directory, "b.md"), "b\n");
        Assert.Equal(2, await Unpublished());

        await sandbox.GitOk(store.Directory, "add", "-A");
        await sandbox.GitOk(store.Directory, "commit", "--quiet", "-m", "notes");
        Assert.Equal(1, await Unpublished());

        await sandbox.GitOk(store.Directory, "push", "--quiet");
        Assert.Equal(0, await Unpublished());
    }

    [Fact]
    public async Task The_origin_is_the_remote_cloned_from()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var store = new Store("notes", await remote.CloneAsync("laptop"));

        Assert.Equal(remote.Path, await StoreState.OriginAsync(sandbox.Git, store, CancellationToken.None));
    }
}
