using Memshare.Stores;
using Memshare.Tests.Support;

namespace Memshare.Tests;

/// <summary>Publishing and pulling between two machines sharing one store.</summary>
public sealed class PublishTests
{
    private static readonly string Copy = ConflictCopies.NameFor("README.md", FixedTime.Default);

    [Fact]
    public async Task What_one_machine_publishes_the_other_receives()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var desktop = await Machine.CloneAsync(sandbox, remote, "desktop");

        laptop.Write("projects/app/MEMORY.md", "- [Build](build.md) — how to build\n");
        var result = await laptop.PublishAsync("app: build notes");

        Assert.True(result.Committed);
        Assert.True(result.Pushed);
        Assert.Contains("projects/app/MEMORY.md", result.Summary, StringComparison.Ordinal);
        Assert.Equal(0, await laptop.UnpublishedAsync());
        Assert.True((await desktop.PullAsync()).Succeeded);
        Assert.Equal("- [Build](build.md) — how to build\n", desktop.Read("projects/app/MEMORY.md"));
    }

    [Fact]
    public async Task Commits_carry_the_neutral_author_not_the_machine()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");

        laptop.Write("global/CLAUDE.md", "Be brief.\n");
        await laptop.PublishAsync();

        var author = await sandbox.GitOk(laptop.Store.Directory, "log", "-1", "--format=%an <%ae> / %cn <%ce>");
        Assert.Equal("memshare <memshare@localhost> / memshare <memshare@localhost>", author.Output);
    }

    [Fact]
    public async Task Publishing_without_changes_sends_nothing()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");

        var result = await laptop.PublishAsync();

        Assert.False(result.Committed);
        Assert.False(result.Pushed);
    }

    [Fact]
    public async Task Edits_of_the_same_file_on_two_machines_are_both_kept()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var desktop = await Machine.CloneAsync(sandbox, remote, "desktop");

        laptop.Write("README.md", "# Store\nfrom the laptop\n");
        await laptop.PublishAsync();
        desktop.Write("README.md", "# Store\nfrom the desktop\n");
        var result = await desktop.PublishAsync();

        Assert.Equal([Copy], result.ConflictCopies);
        Assert.Equal("# Store\nfrom the laptop\n", desktop.Read("README.md"));
        Assert.Equal("# Store\nfrom the desktop\n", desktop.Read(Copy));
        await laptop.PullAsync();
        Assert.Equal("# Store\nfrom the desktop\n", laptop.Read(Copy));
        Assert.DoesNotContain(laptop.Files(), f => File.ReadAllText(f).Contains("<<<<<<<", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_file_deleted_here_but_changed_there_keeps_the_change()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var desktop = await Machine.CloneAsync(sandbox, remote, "desktop");

        laptop.Write("README.md", "# Store\nchanged\n");
        await laptop.PublishAsync();
        desktop.Delete("README.md");
        var result = await desktop.PublishAsync();

        Assert.Equal("# Store\nchanged\n", desktop.Read("README.md"));
        Assert.Empty(result.ConflictCopies);
    }

    [Fact]
    public async Task A_file_changed_here_but_deleted_there_survives_as_a_copy()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var desktop = await Machine.CloneAsync(sandbox, remote, "desktop");

        laptop.Delete("README.md");
        await laptop.PublishAsync();
        desktop.Write("README.md", "# Store\nchanged\n");
        var result = await desktop.PublishAsync();

        Assert.False(desktop.Has("README.md"));
        Assert.Equal([Copy], result.ConflictCopies);
        Assert.Equal("# Store\nchanged\n", desktop.Read(Copy));
    }

    [Fact]
    public async Task Index_entries_added_on_two_machines_are_both_kept()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        laptop.Write("projects/app/MEMORY.md", "- [A](a.md) — first\n");
        await laptop.PublishAsync();
        var desktop = await Machine.CloneAsync(sandbox, remote, "desktop");

        laptop.Write("projects/app/MEMORY.md", "- [A](a.md) — first\n- [B](b.md) — from the laptop\n");
        await laptop.PublishAsync();
        desktop.Write("projects/app/MEMORY.md", "- [A](a.md) — first\n- [C](c.md) — from the desktop\n");
        var result = await desktop.PublishAsync();

        Assert.Empty(result.ConflictCopies);
        var index = desktop.Read("projects/app/MEMORY.md");
        Assert.Contains("- [B](b.md) — from the laptop", index, StringComparison.Ordinal);
        Assert.Contains("- [C](c.md) — from the desktop", index, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Conflict_markers_are_never_published()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");

        laptop.Write("notes.md", "<<<<<<< HEAD\nmine\n=======\ntheirs\n>>>>>>> abc123\n");
        var error = await Assert.ThrowsAsync<MemshareException>(() => laptop.PublishAsync());

        Assert.Contains("notes.md", error.Message, StringComparison.Ordinal);
        var remoteLog = await sandbox.GitOk(remote.Path, "log", "--oneline");
        Assert.Single(remoteLog.Lines);
    }

    [Fact]
    public async Task Offline_the_changes_are_committed_and_published_later()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var away = remote.Path + ".away";
        Directory.Move(remote.Path, away);

        laptop.Write("notes.md", "written offline\n");
        var error = await Assert.ThrowsAsync<MemshareException>(() => laptop.PublishAsync());
        Assert.Contains("committed on this machine", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, await laptop.UnpublishedAsync());

        Directory.Move(away, remote.Path);
        var result = await laptop.PublishAsync();
        Assert.True(result.Pushed);
        Assert.Equal(0, await laptop.UnpublishedAsync());
    }

    [Fact]
    public async Task A_busy_store_is_reported()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        using var held = await StoreLock.AcquireAsync(laptop.Store, TimeSpan.Zero, TestContext.Current.CancellationToken);

        laptop.Write("notes.md", "x\n");
        var error = await Assert.ThrowsAsync<MemshareException>(() => laptop.PublishAsync(lockWait: TimeSpan.FromMilliseconds(300)));

        Assert.Contains("busy", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_abandoned_lock_is_taken_over()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var lockFile = StoreLock.PathOf(laptop.Store);
        File.WriteAllText(lockFile, "12345");
        File.SetLastWriteTimeUtc(lockFile, DateTime.UtcNow - StoreLock.StaleAfter - TimeSpan.FromMinutes(1));

        laptop.Write("notes.md", "x\n");
        var result = await laptop.PublishAsync(lockWait: TimeSpan.Zero);

        Assert.True(result.Pushed);
        Assert.False(File.Exists(lockFile));
    }

    [Fact]
    public async Task Pulling_keeps_local_changes_unpublished()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var desktop = await Machine.CloneAsync(sandbox, remote, "desktop");

        laptop.Write("from-laptop.md", "a\n");
        await laptop.PublishAsync();
        desktop.Write("README.md", "# Store\nlocal edit\n");
        Assert.True((await desktop.PullAsync()).Succeeded);

        Assert.True(desktop.Has("from-laptop.md"));
        Assert.Equal("# Store\nlocal edit\n", desktop.Read("README.md"));
        Assert.Equal(1, await desktop.UnpublishedAsync());
    }

    [Fact]
    public async Task The_first_publish_to_an_empty_remote_works()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateEmptyAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        Assert.True((await laptop.PullAsync()).Succeeded);

        laptop.Write("global/CLAUDE.md", "Be brief.\n");
        var result = await laptop.PublishAsync("first notes");
        var desktop = await Machine.CloneAsync(sandbox, remote, "desktop");

        Assert.True(result.Pushed);
        Assert.Equal("Be brief.\n", desktop.Read("global/CLAUDE.md"));
    }
}
