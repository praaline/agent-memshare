using Memshare.Agents.ClaudeCode;
using Memshare.Configuration;
using Memshare.Installation;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class InstallerTests
{
    [Fact]
    public async Task Installing_sets_up_the_configuration_executable_and_claude_code()
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");
        laptop.Write("global/CLAUDE.md", "Be brief.\n");
        var claude = Installed.ClaudeDirectory(laptop);
        File.WriteAllText(Path.Combine(claude, "CLAUDE.md"), "# My own instructions\n");
        var installer = Installed.Installer(laptop);

        var result = await installer.InstallAsync("laptop", laptop.StoresRoot, Installed.FakeExecutable(sandbox), TestContext.Current.CancellationToken);

        Assert.Equal("memshare build 1", File.ReadAllText(installer.InstalledExecutable));
        Assert.Equal("laptop", ConfigFile.Load(laptop.Environment()).Machine);
        var settings = ClaudeSettings.Load(Path.Combine(claude, "settings.json"));
        Assert.Equal([ClaudeSettings.HookCommand(installer.InstalledExecutable)], ClaudeSettings.MemshareHooks(settings));
        Assert.Single(ClaudeSettings.AskRules(settings));
        var instructions = File.ReadAllText(Path.Combine(claude, "CLAUDE.md"));
        Assert.StartsWith("# My own instructions\n", instructions, StringComparison.Ordinal);
        Assert.Contains("global/CLAUDE.md", instructions, StringComparison.Ordinal);
        Assert.Contains("This machine: `laptop`", instructions, StringComparison.Ordinal);
        Assert.NotEmpty(result.Changes);
    }

    [Fact]
    public async Task Installing_again_changes_nothing_but_a_new_build_is_copied()
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");
        Installed.ClaudeDirectory(laptop);
        var installer = Installed.Installer(laptop);
        await installer.InstallAsync("laptop", laptop.StoresRoot, Installed.FakeExecutable(sandbox), TestContext.Current.CancellationToken);

        Assert.Empty((await installer.InstallAsync(null, null, Installed.FakeExecutable(sandbox), TestContext.Current.CancellationToken)).Changes);
        var update = await installer.InstallAsync(null, null, Installed.FakeExecutable(sandbox, "memshare build 2"), TestContext.Current.CancellationToken);

        Assert.Single(update.Changes);
        Assert.Equal("memshare build 2", File.ReadAllText(installer.InstalledExecutable));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("My Laptop")]
    public async Task A_machine_name_is_required_and_checked(string? name)
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");
        File.Delete(laptop.Environment().ConfigFilePath);

        await Assert.ThrowsAsync<MemshareException>(() => Installed.InstallAsync(laptop, name, null, Installed.FakeExecutable(sandbox)));
    }

    [Fact]
    public async Task Without_claude_code_the_user_is_told()
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");

        var result = await Installed.InstallAsync(laptop, "laptop", laptop.StoresRoot, Installed.FakeExecutable(sandbox));

        Assert.Contains(result.Warnings, w => w.Contains("No supported coding agent", StringComparison.Ordinal));
        Assert.False(Directory.Exists(laptop.Environment().ClaudeDirectory));
    }

    [Fact]
    public async Task Uninstalling_removes_only_what_install_added()
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");
        var claude = Installed.ClaudeDirectory(laptop);
        File.WriteAllText(Path.Combine(claude, "CLAUDE.md"), "# Mine\n");
        File.WriteAllText(Path.Combine(claude, "settings.json"), """{ "theme": "dark" }""");
        var installer = Installed.Installer(laptop);
        await installer.InstallAsync("laptop", laptop.StoresRoot, Installed.FakeExecutable(sandbox), TestContext.Current.CancellationToken);

        var changes = installer.Uninstall();

        Assert.Equal(2 + Memshare.Agents.BundledSkills.All.Count, changes.Count); // settings, block, each skill
        Assert.Equal("# Mine\n", File.ReadAllText(Path.Combine(claude, "CLAUDE.md")));
        var settings = ClaudeSettings.Load(Path.Combine(claude, "settings.json"));
        Assert.Equal("dark", settings["theme"]!.GetValue<string>());
        Assert.Empty(ClaudeSettings.MemshareHooks(settings));
        Assert.Empty(ClaudeSettings.AskRules(settings));
        Assert.True(Directory.Exists(laptop.Store.Directory));
    }

    [Fact]
    public void Machine_names_are_lower_case_letters_digits_and_hyphens()
    {
        Assert.True(Installer.IsValidMachineName("box-2"));
        Assert.False(Installer.IsValidMachineName("Box"));
        Assert.False(Installer.IsValidMachineName("-box"));
    }
}
