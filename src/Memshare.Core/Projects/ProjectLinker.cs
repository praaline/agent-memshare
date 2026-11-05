using Memshare.Configuration;
using Memshare.Git;
using Memshare.Platform;
using Memshare.Stores;

namespace Memshare.Projects;

/// <summary>
/// <c>memshare link</c> and <c>unlink</c>: the marker that tells memshare which shared notes belong to
/// a project on this machine (ADR-0004). Linking writes nothing to the store.
/// </summary>
public static class ProjectLinker
{
    /// <summary>Links the current project to a project slug in a store.</summary>
    /// <exception cref="MemshareException">Invalid slug, or no such store.</exception>
    public static async Task<ProjectLink> LinkAsync(
        MemshareEnvironment environment,
        GitRunner git,
        string slug,
        string? storeName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(git);
        if (!ProjectLink.IsValidSlug(slug))
        {
            throw new MemshareException($"'{slug}' is not a valid project name: use lower-case letters, digits and hyphens, starting with a letter or digit.");
        }

        var config = ConfigFile.Load(environment);
        var store = StoreCatalog.Discover(environment.ExpandPath(config.StoresRoot)).Pick(storeName);
        var root = await ProjectLink.FindProjectRootAsync(git, environment.ProjectDirectory ?? environment.CurrentDirectory, cancellationToken)
            .ConfigureAwait(false);

        var marker = Path.Combine(root, ProjectLink.MarkerRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        await File.WriteAllTextAsync(marker, $"{store.Name} {slug}\n", cancellationToken).ConfigureAwait(false);
        await ExcludeFromGitAsync(git, root, cancellationToken).ConfigureAwait(false);
        return new ProjectLink(root, store.Name, slug);
    }

    /// <summary>Removes the current project's link; the shared notes stay in the store.</summary>
    /// <exception cref="MemshareException">The project is not linked.</exception>
    public static async Task<ProjectLink> UnlinkAsync(MemshareEnvironment environment, GitRunner git, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(git);
        var root = await ProjectLink.FindProjectRootAsync(git, environment.ProjectDirectory ?? environment.CurrentDirectory, cancellationToken)
            .ConfigureAwait(false);
        var link = ProjectLink.Read(root) ?? throw new MemshareException($"This project ({DisplayPath.Of(root)}) is not linked.");
        File.Delete(Path.Combine(root, ProjectLink.MarkerRelativePath));
        return link;
    }

    /// <summary>
    /// Keeps the marker out of the project's Git through <c>.git/info/exclude</c>, without touching the
    /// project's own <c>.gitignore</c>. Nothing to do outside Git or when it is ignored already.
    /// </summary>
    private static async Task ExcludeFromGitAsync(GitRunner git, string root, CancellationToken cancellationToken)
    {
        const string pattern = ".claude/memshare";
        var excludePath = await git.RunAsync(root, ["rev-parse", "--git-path", "info/exclude"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (!excludePath.Succeeded)
        {
            return;
        }

        var ignored = await git.RunAsync(root, ["check-ignore", "--quiet", pattern], GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false);
        if (ignored.Succeeded)
        {
            return;
        }

        var path = Path.GetFullPath(excludePath.Output, root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var existing = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : string.Empty;
        var separator = existing.Length == 0 || existing.EndsWith('\n') ? string.Empty : "\n";
        await File.AppendAllTextAsync(path, $"{separator}{pattern}\n", cancellationToken).ConfigureAwait(false);
    }
}
