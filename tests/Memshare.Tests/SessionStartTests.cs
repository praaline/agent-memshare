using Memshare.Hooks;
using Memshare.Projects;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class SessionStartTests
{
    [Fact]
    public async Task Without_stores_there_is_nothing_to_say()
    {
        using var sandbox = new Sandbox();

        var context = await new SessionStartContext(sandbox.Environment(), sandbox.Git, new FixedTime())
            .BuildAsync(TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, context);
    }

    [Fact]
    public async Task The_context_names_the_stores_the_machine_and_the_linked_projects_notes()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var desktop = await Machine.CloneAsync(sandbox, remote, "desktop");
        laptop.Write("projects/app/MEMORY.md", "- [Build](build.md) — how to build\n");
        laptop.Write("projects/app/handoff.md", "# Handoff\n");
        await laptop.PublishAsync();
        var project = sandbox.Directory("project");
        await ProjectLinker.LinkAsync(desktop.Environment(project), sandbox.Git, "app", null, TestContext.Current.CancellationToken);

        var context = await Build(desktop, project);

        Assert.Contains("stores `notes`", context, StringComparison.Ordinal);
        Assert.Contains("this machine `desktop`", context, StringComparison.Ordinal);
        Assert.Contains("linked to `notes/projects/app`", context, StringComparison.Ordinal);
        Assert.Contains("- [Build](build.md) — how to build", context, StringComparison.Ordinal);
        Assert.Contains("A handoff exists", context, StringComparison.Ordinal);
        Assert.Contains("published 20", context, StringComparison.Ordinal);
        Assert.DoesNotContain("Unpublished", context, StringComparison.Ordinal);
        Assert.DoesNotContain("Could not update", context, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unpublished_changes_and_conflict_copies_are_mentioned()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        laptop.Write("projects/app/plan.conflict-20261105103000.md", "other version\n");
        var project = sandbox.Directory("project");
        await ProjectLinker.LinkAsync(laptop.Environment(project), sandbox.Git, "app", null, TestContext.Current.CancellationToken);

        var context = await Build(laptop, project);

        Assert.Contains("Unpublished changes in: notes", context, StringComparison.Ordinal);
        Assert.Contains("Conflict copies there: plan.conflict-20261105103000.md", context, StringComparison.Ordinal);
        Assert.Contains("No shared notes yet", context, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_and_broken_index_entries_are_pointed_out()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        laptop.Write("projects/app/build.md", "x\n");
        laptop.Write("projects/app/MEMORY.md", "- [Build](build.md) — one\n- [Build](build.md) — two\n- [Gone](gone.md) — missing\n");
        var project = sandbox.Directory("project");
        await ProjectLinker.LinkAsync(laptop.Environment(project), sandbox.Git, "app", null, TestContext.Current.CancellationToken);

        var context = await Build(laptop, project);

        Assert.Contains("several entries for build.md", context, StringComparison.Ordinal);
        Assert.Contains("missing files: gone.md", context, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreachable_remote_is_reported_and_the_local_copy_used()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        Directory.Move(remote.Path, remote.Path + ".away");

        var context = await Build(laptop, sandbox.Directory("project"));

        Assert.Contains("Could not update: notes", context, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_link_to_a_store_not_installed_here_is_explained()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var project = sandbox.Directory("project");
        Directory.CreateDirectory(Path.Combine(project, ".claude"));
        File.WriteAllText(Path.Combine(project, ".claude", "memshare"), "work app\n");

        var context = await Build(laptop, project);

        Assert.Contains("store `work` is not installed", context, StringComparison.Ordinal);
    }

    [Fact]
    public void A_long_index_is_cut_at_a_line_with_a_pointer_to_the_file()
    {
        var index = string.Concat(Enumerable.Range(0, 1000).Select(i => $"- [Entry {i}](e{i}.md) — hook\n"));

        var shown = SessionStartContext.Truncate(index, "/notes/MEMORY.md");

        Assert.True(shown.Length < SessionStartContext.IndexBudget + 100);
        Assert.EndsWith("(The index continues: read /notes/MEMORY.md for the rest.)", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("\n- [Entry 999]", shown, StringComparison.Ordinal);
    }

    private static Task<string> Build(Machine machine, string project) =>
        new SessionStartContext(machine.Environment(projectDirectory: project), machine.Git, machine.Time)
            .BuildAsync(TestContext.Current.CancellationToken);
}
