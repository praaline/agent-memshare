using System.Runtime.InteropServices;

namespace Memshare.Platform;

/// <summary>
/// Where memshare finds things on this machine. Built from the process for real runs, and by hand
/// in tests, so that no test touches the user's home directory: everything not given explicitly is
/// derived from <see cref="HomeDirectory"/>.
/// </summary>
/// <param name="HomeDirectory">The user's home directory; <c>~</c> in paths expands to it.</param>
/// <param name="ConfigDirectory">The directory of memshare's own configuration on this machine.</param>
/// <param name="CurrentDirectory">The directory the command runs in.</param>
/// <param name="ProjectDirectory">The project the agent works in, when the agent says so (hooks).</param>
public sealed record MemshareEnvironment(
    string HomeDirectory,
    string ConfigDirectory,
    string CurrentDirectory,
    string? ProjectDirectory)
{
    private string? _claudeDirectory;

    /// <summary>The configuration file on this machine.</summary>
    public string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

    /// <summary>The pinned host keys of the stores' Git hosts.</summary>
    public string KnownHostsPath => Path.Combine(ConfigDirectory, "known_hosts");

    /// <summary>Claude Code's user directory (<c>CLAUDE_CONFIG_DIR</c>, by default <c>~/.claude</c>).</summary>
    public string ClaudeDirectory
    {
        get => _claudeDirectory ?? Path.Combine(HomeDirectory, ".claude");
        init => _claudeDirectory = value;
    }

    /// <summary>The user's SSH directory, where deploy keys are kept.</summary>
    public string SshDirectory => Path.Combine(HomeDirectory, ".ssh");

    /// <summary>
    /// Where <c>memshare install</c> puts the executable: <c>~/.local/bin</c>, or
    /// <c>%LOCALAPPDATA%\Programs\memshare</c> on Windows.
    /// </summary>
    public string InstallDirectory => OperatingSystem.IsWindows()
        ? Path.Combine(HomeDirectory, "AppData", "Local", "Programs", "memshare")
        : Path.Combine(HomeDirectory, ".local", "bin");

    /// <summary>The executable's file name on this OS.</summary>
    public static string ExecutableName => OperatingSystem.IsWindows() ? "memshare.exe" : "memshare";

    /// <summary>The operating system, as shown to the user and the agent.</summary>
    public static string OperatingSystemName =>
        OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsMacOS() ? "macos"
        : OperatingSystem.IsLinux() ? "linux"
        : "unknown";

    /// <summary>A readable description of the OS and its version.</summary>
    public static string OperatingSystemDescription
    {
        get
        {
            if (OperatingSystem.IsLinux() && File.Exists("/etc/os-release"))
            {
                var pretty = File.ReadLines("/etc/os-release")
                    .FirstOrDefault(l => l.StartsWith("PRETTY_NAME=", StringComparison.Ordinal));
                if (pretty is not null)
                {
                    return pretty["PRETTY_NAME=".Length..].Trim('"');
                }
            }

            return RuntimeInformation.OSDescription;
        }
    }

    /// <summary>
    /// The environment of the current process. <c>MEMSHARE_CONFIG_DIR</c> overrides the configuration
    /// directory; otherwise it is <c>%APPDATA%\memshare</c> on Windows and
    /// <c>$XDG_CONFIG_HOME/memshare</c> (default <c>~/.config/memshare</c>) elsewhere.
    /// <c>CLAUDE_PROJECT_DIR</c>, set by Claude Code for hooks, names the project.
    /// </summary>
    public static MemshareEnvironment FromProcess()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDirectory = NonEmpty("MEMSHARE_CONFIG_DIR") ?? DefaultConfigDirectory(home);
        var environment = new MemshareEnvironment(home, configDirectory, Environment.CurrentDirectory, NonEmpty("CLAUDE_PROJECT_DIR"));
        return NonEmpty("CLAUDE_CONFIG_DIR") is { } claude ? environment with { ClaudeDirectory = claude } : environment;
    }

    /// <summary>Expands a leading <c>~</c> to the home directory and makes the path absolute.</summary>
    public string ExpandPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path == "~")
        {
            return HomeDirectory;
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            path = Path.Combine(HomeDirectory, path[2..]);
        }

        return Path.GetFullPath(path, CurrentDirectory);
    }

    /// <summary>
    /// A path under the home directory written as <c>~/…</c> (forward slashes), or null when it is
    /// elsewhere.
    /// </summary>
    public string? HomeRelative(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var relative = Path.GetRelativePath(HomeDirectory, Path.GetFullPath(path));
        if (relative == ".")
        {
            return "~";
        }

        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? null
            : "~/" + relative.Replace('\\', '/');
    }

    private static string DefaultConfigDirectory(string home)
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "memshare");
        }

        var xdg = NonEmpty("XDG_CONFIG_HOME");
        return Path.Combine(xdg ?? Path.Combine(home, ".config"), "memshare");
    }

    private static string? NonEmpty(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
