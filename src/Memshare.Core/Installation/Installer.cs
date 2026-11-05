using System.Text.RegularExpressions;

using Memshare.Agents;
using Memshare.Configuration;
using Memshare.Git;
using Memshare.Platform;
using Memshare.Stores;

namespace Memshare.Installation;

/// <summary>The outcome of <c>memshare install</c>.</summary>
/// <param name="ExecutablePath">Where the executable is installed.</param>
/// <param name="Changes">What changed on the machine.</param>
/// <param name="Warnings">What the user should know or do.</param>
public sealed record InstallResult(string ExecutablePath, IReadOnlyList<string> Changes, IReadOnlyList<string> Warnings);

/// <summary>
/// <c>memshare install</c> and <c>uninstall</c>: sets up a machine (ADR-0007). Idempotent: running it
/// again repairs or updates what it set up, and changes nothing else.
/// </summary>
/// <param name="environment">The machine.</param>
/// <param name="git">The user's git.</param>
/// <param name="integrations">The agents to set up.</param>
/// <param name="time">The clock.</param>
public sealed partial class Installer(MemshareEnvironment environment, GitRunner git, IReadOnlyList<IAgentIntegration> integrations, TimeProvider time)
{
    /// <summary>The installed executable's path.</summary>
    public string InstalledExecutable => Path.Combine(environment.InstallDirectory, MemshareEnvironment.ExecutableName);

    /// <summary>Whether a machine name is valid: lower-case letters, digits and hyphens.</summary>
    public static bool IsValidMachineName(string? name) => name is not null && MachineNamePattern().IsMatch(name);

    /// <summary>Sets up the machine.</summary>
    /// <param name="machine">The machine's name; keeps the configured one when null.</param>
    /// <param name="storesRoot">The stores root; keeps the configured one (or the default) when null.</param>
    /// <param name="sourceExecutable">The executable being run, copied to the install directory.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="MemshareException">No machine name, or an invalid one.</exception>
    public async Task<InstallResult> InstallAsync(string? machine, string? storesRoot, string sourceExecutable, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceExecutable);
        var config = ConfigFile.Load(environment);
        machine ??= config.Machine;
        if (machine is null)
        {
            throw new MemshareException("Name this machine: memshare install --machine <name> (lower-case letters, digits and hyphens).");
        }

        if (!IsValidMachineName(machine))
        {
            throw new MemshareException($"'{machine}' is not a valid machine name: use lower-case letters, digits and hyphens.");
        }

        var changes = new List<string>();
        var warnings = new List<string>();
        var updated = config with { Machine = machine, StoresRoot = storesRoot ?? config.StoresRoot };
        if (updated != config || !File.Exists(environment.ConfigFilePath))
        {
            ConfigFile.Save(environment, updated);
            changes.Add($"configuration {DisplayPath.Of(environment.ConfigFilePath)}");
        }

        if (CopyExecutable(sourceExecutable))
        {
            changes.Add($"executable {DisplayPath.Of(InstalledExecutable)}");
        }

        var root = environment.ExpandPath(updated.StoresRoot);
        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);
            changes.Add($"stores root {DisplayPath.Of(root)}");
        }

        var context = Context(updated);
        foreach (var store in context.Stores.Stores)
        {
            // The merge driver runs the installed executable: keep every clone pointing at it.
            if (await StoreConfiguration.ConfiguredDriverAsync(git, store, cancellationToken).ConfigureAwait(false) != StoreConfiguration.MergeDriverCommand(InstalledExecutable))
            {
                await StoreConfiguration.ConfigureAsync(git, store, InstalledExecutable, sshCommand: null, cancellationToken).ConfigureAwait(false);
                changes.Add($"merge driver and settings of store {store.Name}");
            }
        }

        var present = integrations.Where(i => i.IsPresent(context)).ToList();
        if (present.Count == 0)
        {
            warnings.Add($"No supported coding agent found ({string.Join(", ", integrations.Select(i => i.Name))}): install one, then run memshare install again.");
        }

        foreach (var integration in present)
        {
            changes.AddRange(integration.Apply(context));
        }

        if (!IsOnPath(environment.InstallDirectory))
        {
            warnings.Add($"{DisplayPath.Of(environment.InstallDirectory)} is not on your PATH: add it to run `memshare` by name (the agent's hook uses the full path and works anyway).");
        }

        return new InstallResult(InstalledExecutable, changes, warnings);
    }

    /// <summary>Removes the agents' hook, rule and instructions block. Stores, keys, configuration and the executable stay.</summary>
    public IReadOnlyList<string> Uninstall()
    {
        var context = Context(ConfigFile.Load(environment));
        return integrations.SelectMany(i => i.Remove(context)).ToList();
    }

    /// <summary>The context the agent integrations work with.</summary>
    public InstallContext Context(MemshareConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new InstallContext(environment, config, StoreCatalog.Discover(environment.ExpandPath(config.StoresRoot)), InstalledExecutable, time);
    }

    private bool CopyExecutable(string source)
    {
        var target = InstalledExecutable;
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return false;
        }

        if (File.Exists(target) && FilesEqual(source, target))
        {
            return false;
        }

        Directory.CreateDirectory(environment.InstallDirectory);
        var temporary = target + ".new";
        File.Copy(source, temporary, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temporary, File.GetUnixFileMode(source) | UnixFileMode.UserExecute);
        }

        if (OperatingSystem.IsWindows() && File.Exists(target))
        {
            // A running executable can't be replaced on Windows, but it can be renamed.
            var old = target + ".old";
            File.Delete(old);
            File.Move(target, old);
        }

        File.Move(temporary, target, overwrite: true);
        return true;
    }

    private static bool FilesEqual(string a, string b)
    {
        var infoA = new FileInfo(a);
        var infoB = new FileInfo(b);
        return infoA.Length == infoB.Length && File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
    }

    private static bool IsOnPath(string directory)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(p => string.Equals(p.TrimEnd('/', '\\'), directory.TrimEnd('/', '\\'), comparison));
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex MachineNamePattern();
}
