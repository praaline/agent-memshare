using Memshare.Agents;
using Memshare.Agents.ClaudeCode;
using Memshare.Installation;
using Memshare.Stores;

namespace Memshare.Tests.Support;

/// <summary>memshare installed on a test machine: a fake executable, Claude Code's directory, the installer.</summary>
internal static class Installed
{
    public static readonly IReadOnlyList<IAgentIntegration> Agents = [new ClaudeCodeIntegration()];

    /// <summary>A file standing in for the memshare executable being run.</summary>
    public static string FakeExecutable(Sandbox sandbox, string content = "memshare build 1")
    {
        var path = Path.Combine(sandbox.Directory("download"), "memshare");
        File.WriteAllText(path, content);
        return path;
    }

    public static Installer Installer(Machine machine) => new(machine.Environment(), machine.Git, Agents, machine.Time);

    public static Task<InstallResult> InstallAsync(Machine machine, string? name, string? storesRoot, string executable) =>
        Installer(machine).InstallAsync(name, storesRoot, executable, TestContext.Current.CancellationToken);

    /// <summary>The real memshare executable, built next to the tests (for git's merge driver).</summary>
    public static string RealExecutable =>
        Path.Combine(AppContext.BaseDirectory, Memshare.Platform.MemshareEnvironment.ExecutableName);

    public static StoreInstaller Stores(Sandbox sandbox, Machine machine) =>
        new(machine.Environment(), sandbox.Git, Agents, Installer(machine).InstalledExecutable, machine.Time);

    public static string ClaudeDirectory(Machine machine) => Directory.CreateDirectory(machine.Environment().ClaudeDirectory).FullName;
}
