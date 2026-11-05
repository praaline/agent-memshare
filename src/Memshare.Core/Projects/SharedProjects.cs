using System.Globalization;

using Memshare.Git;
using Memshare.Stores;

namespace Memshare.Projects;

/// <summary><c>memshare projects</c>: the projects with shared notes in the stores.</summary>
public static class SharedProjects
{
    /// <summary>The projects of every store, ordered by store and name.</summary>
    public static async Task<IReadOnlyList<SharedProject>> ListAsync(GitRunner git, StoreCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(catalog);
        var projects = new List<SharedProject>();
        foreach (var store in catalog.Stores)
        {
            var directory = Path.Combine(store.Directory, "projects");
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var slug in Directory.EnumerateDirectories(directory).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal))
            {
                projects.Add(new SharedProject(store, slug, await LastCommitAsync(git, store, $"projects/{slug}", cancellationToken).ConfigureAwait(false)));
            }
        }

        return projects;
    }

    /// <summary>When a file of a store was last published (committed); null if never.</summary>
    public static async Task<DateTimeOffset?> LastCommitAsync(GitRunner git, Store store, string relativePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(store);
        var log = await git.RunAsync(store.Directory, ["log", "-1", "--format=%cI", "--", relativePath], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false);
        return log.Succeeded && DateTimeOffset.TryParse(log.Output, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when) ? when : null;
    }
}
