using Memshare.Git;
using Memshare.Platform;

namespace Memshare.Stores;

/// <summary>
/// SSH access to a store (ADR-0004): a deploy key per machine and store, with no passphrase (the hook
/// runs unattended) and write access to that repository only; the host key pinned in memshare's own
/// <c>known_hosts</c>; and an SSH command that uses nothing else (not the user's agent or SSH config).
/// </summary>
public static class SshAccess
{
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(20);

    /// <summary>The deploy key of a store on this machine.</summary>
    public static string KeyPath(MemshareEnvironment environment, string storeName)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return Path.Combine(environment.SshDirectory, $"memshare-{storeName}_ed25519");
    }

    /// <summary>The SSH command git uses for a store (<c>core.sshCommand</c>).</summary>
    public static string SshCommand(string keyPath, string knownHostsPath)
    {
        ArgumentNullException.ThrowIfNull(keyPath);
        ArgumentNullException.ThrowIfNull(knownHostsPath);
        return "ssh -F none"
            + $" -i \"{keyPath.Replace('\\', '/')}\""
            + " -o IdentitiesOnly=yes -o IdentityAgent=none"
            + $" -o \"UserKnownHostsFile={knownHostsPath.Replace('\\', '/')}\""
            + " -o StrictHostKeyChecking=yes -o BatchMode=yes -o ConnectTimeout=8";
    }

    /// <summary>Creates the store's deploy key if it doesn't exist; returns its public key.</summary>
    public static async Task<string> EnsureKeyAsync(MemshareEnvironment environment, string storeName, string machine, CancellationToken cancellationToken)
    {
        var key = KeyPath(environment, storeName);
        if (!File.Exists(key))
        {
            Directory.CreateDirectory(environment.SshDirectory);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(environment.SshDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var result = await ToolRunner.RunAsync(
                "ssh-keygen",
                environment.SshDirectory,
                ["-q", "-t", "ed25519", "-N", string.Empty, "-C", $"memshare {storeName} {machine}", "-f", key],
                new Dictionary<string, string?>(),
                ToolTimeout,
                cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new MemshareException($"Could not create the deploy key {key}: {result.StandardError.Trim()}");
            }
        }

        return (await File.ReadAllTextAsync(key + ".pub", cancellationToken).ConfigureAwait(false)).Trim();
    }

    /// <summary>Whether a host's key is pinned already.</summary>
    public static bool IsPinned(MemshareEnvironment environment, StoreUrl url)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(url);
        return File.Exists(environment.KnownHostsPath)
            && File.ReadLines(environment.KnownHostsPath).Any(l => l.Split(' ', 2)[0].Split(',').Contains(url.KnownHostsName));
    }

    /// <summary>A host's ed25519 key line, as <c>ssh-keyscan</c> reports it, with its fingerprint.</summary>
    public static async Task<(string Line, string Fingerprint)> ScanAsync(MemshareEnvironment environment, StoreUrl url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(url);
        List<string> arguments = ["-T", "8", "-t", "ed25519"];
        if (url.Port is { } port)
        {
            arguments.AddRange(["-p", port.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        }

        arguments.Add(url.Host!);
        var scan = await ToolRunner.RunAsync("ssh-keyscan", environment.HomeDirectory, arguments, new Dictionary<string, string?>(), ToolTimeout, cancellationToken)
            .ConfigureAwait(false);
        var line = scan.Lines.FirstOrDefault(l => !l.StartsWith('#'));
        if (line is null)
        {
            throw new MemshareException($"Could not read the SSH host key of {url.KnownHostsName} (offline, or no ed25519 key). Give it with --host-key.");
        }

        return (line, await FingerprintAsync(environment, line, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The SHA256 fingerprint of a <c>known_hosts</c> line.</summary>
    public static async Task<string> FingerprintAsync(MemshareEnvironment environment, string knownHostsLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var temporary = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(temporary, knownHostsLine + "\n", cancellationToken).ConfigureAwait(false);
            var result = await ToolRunner.RunAsync("ssh-keygen", environment.HomeDirectory, ["-l", "-f", temporary], new Dictionary<string, string?>(), ToolTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new MemshareException($"'{knownHostsLine}' is not a valid host key line.");
            }

            return result.Output;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>Pins a host key: adds its line, for the URL's host, to memshare's <c>known_hosts</c>.</summary>
    public static void Pin(MemshareEnvironment environment, StoreUrl url, string knownHostsLine)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(knownHostsLine);
        var parts = knownHostsLine.Trim().Split(' ', 2);
        if (parts.Length < 2)
        {
            throw new MemshareException($"'{knownHostsLine}' is not a host key line (expected: <host> <type> <key>).");
        }

        Directory.CreateDirectory(environment.ConfigDirectory);
        File.AppendAllText(environment.KnownHostsPath, $"{url.KnownHostsName} {parts[1]}\n");
    }

    /// <summary>Whether git can reach a remote with an SSH command (or the user's own setup when null).</summary>
    public static Task<GitResult> ProbeAsync(GitRunner git, MemshareEnvironment environment, string url, string? sshCommand, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(environment);
        var runner = sshCommand is null ? git : git.WithEnvironment(new Dictionary<string, string?> { ["GIT_SSH_COMMAND"] = sshCommand });
        return runner.RunAsync(environment.HomeDirectory, ["ls-remote", "--heads", url], TimeSpan.FromSeconds(20), cancellationToken);
    }
}
