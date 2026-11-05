using Memshare.Agents.ClaudeCode;
using Memshare.Diagnostics;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class DoctorTests
{
    [Fact]
    public async Task A_complete_installation_has_no_problems()
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");
        Installed.Stores(sandbox, laptop).Init("notes");
        laptop.Write(".gitattributes", Memshare.Stores.StoreTemplate.Files[".gitattributes"]);
        await laptop.PublishAsync();
        Installed.ClaudeDirectory(laptop);
        await Installed.InstallAsync(laptop, "laptop", laptop.StoresRoot, Installed.FakeExecutable(sandbox));

        var findings = await Check(sandbox, laptop);

        Assert.DoesNotContain(findings, f => f.Severity != Severity.Ok);
        Assert.Contains(findings, f => f.Message.StartsWith("remote reachable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_pieces_are_reported_with_what_to_do()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var laptop = await Machine.CloneAsync(sandbox, remote, "laptop");
        var claude = Installed.ClaudeDirectory(laptop);
        await Installed.InstallAsync(laptop, "laptop", laptop.StoresRoot, Installed.FakeExecutable(sandbox));
        var settingsPath = Path.Combine(claude, "settings.json");
        var settings = ClaudeSettings.Load(settingsPath);
        ClaudeSettings.RemoveHook(settings);
        ClaudeSettings.Save(settingsPath, settings);
        laptop.Write("draft.md", "x\n");
        Directory.Move(remote.Path, remote.Path + ".away");

        var findings = await Check(sandbox, laptop);

        Assert.Contains(findings, f => f.Severity == Severity.Problem && f.Message.Contains("no session-start hook", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Severity == Severity.Problem && f.Message.StartsWith("remote not reachable", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Severity == Severity.Warning && f.Message.Contains("unpublished", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Severity == Severity.Warning && f.Message.Contains("store init", StringComparison.Ordinal));
    }

    private static Task<IReadOnlyList<Finding>> Check(Sandbox sandbox, Machine machine) =>
        new Doctor(machine.Environment(), sandbox.Git, Installed.Agents, Installed.Installer(machine).InstalledExecutable, machine.Time)
            .CheckAsync(checkRemotes: true, TestContext.Current.CancellationToken);
}

public sealed class SshCommandKeyTests
{
    [Theory]
    [InlineData("ssh -F none -i \"/home/me/.ssh/memshare-notes_ed25519\" -o BatchMode=yes", "/home/me/.ssh/memshare-notes_ed25519")]
    [InlineData("ssh -F /dev/null -i /home/me/.ssh/old_ed25519 -o IdentitiesOnly=yes", "/home/me/.ssh/old_ed25519")]
    [InlineData("ssh -i 'C:/Users/Jane Doe/.ssh/key'", "C:/Users/Jane Doe/.ssh/key")]
    [InlineData("ssh -o BatchMode=yes", "")]
    public void The_key_is_read_from_quoted_and_unquoted_ssh_commands(string command, string key) =>
        Assert.Equal(key, Memshare.Diagnostics.Doctor.ExtractKey(command));
}
