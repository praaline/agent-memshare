using Memshare.Configuration;
using Memshare.Git;
using Memshare.Platform;
using Memshare.Projects;
using Memshare.Stores;

namespace Memshare.Status;

/// <summary>One store in a status report.</summary>
/// <param name="Store">The store.</param>
/// <param name="Unpublished">Changes on this machine not published yet.</param>
/// <param name="Origin">The URL of its remote, if any.</param>
public sealed record StoreStatus(Store Store, int Unpublished, string? Origin);

/// <summary>What <c>memshare status</c> shows.</summary>
/// <param name="Machine">This machine's name.</param>
/// <param name="OperatingSystem">This machine's operating system.</param>
/// <param name="StoresRoot">The stores root.</param>
/// <param name="Stores">The stores and their state.</param>
/// <param name="ProjectRoot">The current project's root.</param>
/// <param name="Link">The current project's link, if it is linked.</param>
public sealed record StatusReport(
    string Machine,
    string OperatingSystem,
    string StoresRoot,
    IReadOnlyList<StoreStatus> Stores,
    string ProjectRoot,
    ProjectLink? Link)
{
    /// <summary>Gathers the status of this machine and of the current project.</summary>
    public static async Task<StatusReport> GatherAsync(
        MemshareEnvironment environment,
        GitRunner git,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(git);

        var config = ConfigFile.Load(environment);
        var root = environment.ExpandPath(config.StoresRoot);
        var catalog = StoreCatalog.Discover(root);

        var stores = new List<StoreStatus>();
        foreach (var store in catalog.Stores)
        {
            stores.Add(new StoreStatus(
                store,
                await StoreState.CountUnpublishedAsync(git, store, cancellationToken).ConfigureAwait(false),
                await StoreState.OriginAsync(git, store, cancellationToken).ConfigureAwait(false)));
        }

        var projectRoot = await ProjectLink.FindProjectRootAsync(
            git, environment.ProjectDirectory ?? environment.CurrentDirectory, cancellationToken).ConfigureAwait(false);

        return new StatusReport(
            config.Machine ?? Environment.MachineName.ToLowerInvariant(),
            MemshareEnvironment.OperatingSystemName,
            root,
            stores,
            projectRoot,
            ProjectLink.Read(projectRoot));
    }
}
