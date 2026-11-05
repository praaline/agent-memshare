using Memshare.Git;
using Memshare.Platform;

namespace Memshare.Tests.Support;

/// <summary>
/// A temporary directory standing in for a user's machine: a home directory, memshare's configuration
/// directory, and a git isolated from the real user's configuration. Deleted when disposed.
/// </summary>
internal sealed class Sandbox : IDisposable
{
    public Sandbox()
    {
        Root = Path.Combine(Path.GetTempPath(), "memshare-tests", Guid.NewGuid().ToString("N"));
        Home = System.IO.Directory.CreateDirectory(Path.Combine(Root, "home")).FullName;
        var gitConfig = Path.Combine(Root, "gitconfig");
        File.WriteAllText(gitConfig, """
            [user]
            	name = Test
            	email = test@example.invalid
            [init]
            	defaultBranch = main
            [core]
            	autocrlf = false
            [pull]
            	rebase = true
            """);
        Git = new GitRunner(new Dictionary<string, string?>
        {
            ["GIT_CONFIG_GLOBAL"] = gitConfig,
            ["GIT_CONFIG_NOSYSTEM"] = "1",
        });
    }

    public string Root { get; }

    public string Home { get; }

    public GitRunner Git { get; }

    /// <summary>An environment whose home and configuration live in the sandbox.</summary>
    public MemshareEnvironment Environment(string? currentDirectory = null, string? projectDirectory = null) =>
        new(Home, Path.Combine(Home, ".config", "memshare"), currentDirectory ?? Home, projectDirectory);

    /// <summary>Creates a directory in the sandbox and returns its full path.</summary>
    public string Directory(params string[] parts) =>
        System.IO.Directory.CreateDirectory(Path.Combine([Root, .. parts])).FullName;

    /// <summary>Runs git and fails the test if it fails.</summary>
    public async Task<GitResult> GitOk(string directory, params string[] arguments)
    {
        var result = await Git.RunAsync(directory, arguments);
        Assert.True(result.Succeeded, $"git {string.Join(' ', arguments)} failed: {result.StandardError}");
        return result;
    }

    public void Dispose()
    {
        if (!System.IO.Directory.Exists(Root))
        {
            return;
        }

        // Git marks its object files read-only, which Windows refuses to delete.
        foreach (var file in System.IO.Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        System.IO.Directory.Delete(Root, recursive: true);
    }
}
