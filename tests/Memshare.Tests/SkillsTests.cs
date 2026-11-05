using Memshare.Agents;
using Memshare.Diagnostics;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class SkillsTests
{
    [Fact]
    public void The_skills_are_bundled_with_frontmatter_naming_them()
    {
        Assert.Equal(["memshare-handoff", "memshare-save"], BundledSkills.All.Keys);
        foreach (var (name, content) in BundledSkills.All)
        {
            Assert.StartsWith($"---\nname: {name}\ndescription: ", content, StringComparison.Ordinal);
            Assert.Contains("\n---\n", content[3..], StringComparison.Ordinal);
            Assert.DoesNotContain("\r", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Install_puts_the_skills_in_place_and_restores_a_changed_one()
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");
        var claude = Installed.ClaudeDirectory(laptop);
        await Installed.InstallAsync(laptop, "laptop", laptop.StoresRoot, Installed.FakeExecutable(sandbox));
        var save = Path.Combine(claude, "skills", "memshare-save", "SKILL.md");

        Assert.Equal(BundledSkills.All["memshare-save"], File.ReadAllText(save));
        Assert.True(File.Exists(Path.Combine(claude, "skills", "memshare-handoff", "SKILL.md")));

        File.AppendAllText(save, "edited here\n");
        var findings = await new Doctor(laptop.Environment(), sandbox.Git, Installed.Agents, Installed.Installer(laptop).InstalledExecutable, laptop.Time)
            .CheckAsync(checkRemotes: false, TestContext.Current.CancellationToken);
        Assert.Contains(findings, f => f.Severity == Severity.Warning && f.Message.StartsWith("skill memshare-save differs", StringComparison.Ordinal));

        var again = await Installed.InstallAsync(laptop, null, null, Installed.FakeExecutable(sandbox));
        Assert.Single(again.Changes, c => c.StartsWith("skill memshare-save", StringComparison.Ordinal));
        Assert.Equal(BundledSkills.All["memshare-save"], File.ReadAllText(save));
    }

    [Fact]
    public async Task Uninstall_removes_the_skills_and_nothing_else_in_the_skills_directory()
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");
        var claude = Installed.ClaudeDirectory(laptop);
        var mine = Path.Combine(claude, "skills", "my-own", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(mine)!);
        File.WriteAllText(mine, "---\nname: my-own\n---\n");
        await Installed.InstallAsync(laptop, "laptop", laptop.StoresRoot, Installed.FakeExecutable(sandbox));

        Installed.Installer(laptop).Uninstall();

        Assert.False(Directory.Exists(Path.Combine(claude, "skills", "memshare-save")));
        Assert.False(Directory.Exists(Path.Combine(claude, "skills", "memshare-handoff")));
        Assert.True(File.Exists(mine));
    }
}
