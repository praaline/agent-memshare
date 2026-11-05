using Memshare.Stores;
using Memshare.Tests.Support;

namespace Memshare.Tests;

/// <summary>git running memshare's merge driver while two machines publish the same index.</summary>
public sealed class MergeDriverTests
{
    [Fact]
    public async Task Git_merges_an_index_entry_by_entry_through_the_driver()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        laptop.Write(".gitattributes", StoreTemplate.Files[".gitattributes"]);
        laptop.Write("projects/app/MEMORY.md", "- [Build](build.md) — how to build\n- [Tests](tests.md) — how to test\n");
        await laptop.PublishAsync();
        var desktop = await Machine.CloneAsync(sandbox, remote, "desktop");
        foreach (var machine in new[] { laptop, desktop })
        {
            await StoreConfiguration.ConfigureAsync(sandbox.Git, machine.Store, Installed.RealExecutable, null, TestContext.Current.CancellationToken);
        }

        // Adjacent lines changed on each side: git's own merge conflicts here, and merge=union would
        // keep the old and new version of both entries. The driver merges entry by entry.
        laptop.Write("projects/app/MEMORY.md", "- [Build](build.md) — how to build (Release)\n- [Tests](tests.md) — how to test\n");
        await laptop.PublishAsync();
        desktop.Write("projects/app/MEMORY.md", "- [Build](build.md) — how to build\n- [Tests](tests.md) — how to test, with coverage\n");
        var result = await desktop.PublishAsync();

        Assert.Empty(result.ConflictCopies);
        Assert.Equal(
            "- [Build](build.md) — how to build (Release)\n- [Tests](tests.md) — how to test, with coverage\n",
            desktop.Read("projects/app/MEMORY.md"));
    }

    [Fact]
    public async Task Without_the_driver_configured_a_conflicting_index_becomes_a_conflict_copy()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        laptop.Write(".gitattributes", StoreTemplate.Files[".gitattributes"]);
        laptop.Write("projects/app/MEMORY.md", "- [Build](build.md) — how to build\n- [Tests](tests.md) — how to test\n");
        await laptop.PublishAsync();
        var desktop = await Machine.CloneAsync(sandbox, remote, "desktop");

        laptop.Write("projects/app/MEMORY.md", "- [Build](build.md) — how to build (Release)\n- [Tests](tests.md) — how to test\n");
        await laptop.PublishAsync();
        desktop.Write("projects/app/MEMORY.md", "- [Build](build.md) — how to build\n- [Tests](tests.md) — how to test, with coverage\n");
        var result = await desktop.PublishAsync();

        Assert.Single(result.ConflictCopies);
        Assert.Equal("- [Build](build.md) — how to build (Release)\n- [Tests](tests.md) — how to test\n", desktop.Read("projects/app/MEMORY.md"));
        Assert.DoesNotContain(desktop.Files(), f => File.ReadAllText(f).Contains("<<<<<<<", StringComparison.Ordinal));
    }
}
