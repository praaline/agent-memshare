using Memshare.Configuration;
using Memshare.Stores;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class StoreInstallerTests
{
    [Fact]
    public async Task Adding_a_store_clones_it_configures_it_and_imports_its_instructions()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        laptop.Write("global/CLAUDE.md", "Be brief.\n");
        await laptop.PublishAsync();
        var desktop = await NewMachineAsync(sandbox, "desktop");
        Installed.ClaudeDirectory(desktop);
        await Installed.InstallAsync(desktop, "desktop", null, Installed.FakeExecutable(sandbox));

        var result = await Installed.Stores(sandbox, desktop).AddAsync(remote.Path, "personal", null, _ => false, TestContext.Current.CancellationToken);

        Assert.NotNull(result.Store);
        Assert.False(result.IsEmpty);
        Assert.True(result.NeedsInit);
        Assert.Equal("Be brief.\n", File.ReadAllText(Path.Combine(result.Store.Directory, "global", "CLAUDE.md")));
        Assert.Equal("memshare", (await sandbox.GitOk(result.Store.Directory, "config", "user.name")).Output);
        Assert.Equal("true", (await sandbox.GitOk(result.Store.Directory, "config", "pull.rebase")).Output);
        var instructions = File.ReadAllText(Path.Combine(desktop.Environment().ClaudeDirectory, "CLAUDE.md"));
        Assert.Contains("personal/global/CLAUDE.md", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_store_is_named_after_the_repository_by_default()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox, "team-notes");
        var desktop = await NewMachineAsync(sandbox, "desktop");

        var result = await Installed.Stores(sandbox, desktop).AddAsync(remote.Path, null, null, _ => false, TestContext.Current.CancellationToken);

        Assert.Equal("team-notes", result.Store?.Name);
    }

    [Fact]
    public async Task A_store_that_exists_already_is_not_overwritten()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");

        var error = await Assert.ThrowsAsync<MemshareException>(() =>
            Installed.Stores(sandbox, laptop).AddAsync(remote.Path, "notes", null, _ => false, TestContext.Current.CancellationToken));

        Assert.Contains("exists already", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_store_of_a_newer_format_is_refused_and_not_left_behind()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        laptop.Write(StoreManifest.FileName, """{ "format": 99 }""");
        await laptop.PublishAsync();
        var desktop = await NewMachineAsync(sandbox, "desktop");

        await Assert.ThrowsAsync<MemshareException>(() =>
            Installed.Stores(sandbox, desktop).AddAsync(remote.Path, "notes", null, _ => false, TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(Path.Combine(desktop.StoresRoot, "notes")));
    }

    [Fact]
    public async Task An_empty_store_is_initialised_from_the_template_and_published()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateEmptyAsync(sandbox);
        var desktop = await NewMachineAsync(sandbox, "desktop");
        var stores = Installed.Stores(sandbox, desktop);

        var added = await stores.AddAsync(remote.Path, "notes", null, _ => false, TestContext.Current.CancellationToken);
        var files = stores.Init("notes");

        Assert.True(added.IsEmpty);
        Assert.Equal(StoreTemplate.Files.Keys.Order(StringComparer.Ordinal), files.Order(StringComparer.Ordinal));
        Assert.Equal(StoreManifest.SupportedFormat, StoreManifest.ReadFormat(added.Store!));
        Assert.Empty(stores.Init("notes"));
        var published = await new StorePublisher(sandbox.Git, desktop.Time).PublishAsync(added.Store!, "New store", allowSecrets: false, TestContext.Current.CancellationToken);
        Assert.True(published.Pushed);
    }

    [Fact]
    public async Task Initialising_never_overwrites_a_file()
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");
        laptop.Write("global/CLAUDE.md", "Mine.\n");

        var added = Installed.Stores(sandbox, laptop).Init(null);

        Assert.DoesNotContain("global/CLAUDE.md", added);
        Assert.DoesNotContain("README.md", added);
        Assert.Equal("Mine.\n", laptop.Read("global/CLAUDE.md"));
    }

    [Fact]
    public async Task Removing_a_store_with_unpublished_changes_needs_force()
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");
        laptop.Write("draft.md", "not published\n");
        var stores = Installed.Stores(sandbox, laptop);

        await Assert.ThrowsAsync<MemshareException>(() => stores.RemoveAsync("notes", force: false, TestContext.Current.CancellationToken));
        await stores.RemoveAsync("notes", force: true, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(laptop.Store.Directory));
    }

    private static async Task<Machine> NewMachineAsync(Sandbox sandbox, string name)
    {
        // A machine with its own (empty) stores root: it gets its stores through `store add`.
        var placeholder = await StoreRemote.CreateEmptyAsync(sandbox, $"placeholder-{name}");
        var machine = await Machine.CloneAsync(sandbox, placeholder, name, "placeholder");
        Directory.Delete(machine.Store.Directory, recursive: true);
        ConfigFile.Save(machine.Environment(), new MemshareConfig { Machine = name, StoresRoot = machine.StoresRoot });
        return machine;
    }
}
