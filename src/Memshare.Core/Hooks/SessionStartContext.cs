using System.Globalization;
using System.Text;

using Memshare.Configuration;
using Memshare.Git;
using Memshare.Platform;
using Memshare.Projects;
using Memshare.Stores;

namespace Memshare.Hooks;

/// <summary>
/// What the session-start hook tells the agent: where the stores are, what could not be updated, what is
/// unpublished, and the linked project's shared index, handoff and conflict copies. Updates the stores
/// first (reading only: nothing is committed or pushed). Never throws for a store's problem: the
/// session starts anyway, with a note.
/// </summary>
/// <param name="environment">The machine.</param>
/// <param name="git">The user's git.</param>
/// <param name="time">The clock, for the names of conflict copies.</param>
public sealed class SessionStartContext(MemshareEnvironment environment, GitRunner git, TimeProvider time)
{
    /// <summary>How much of a project's index is shown, in characters; the rest is read on demand.</summary>
    public const int IndexBudget = 8000;

    /// <summary>How long the hook waits for a store busy with another memshare operation.</summary>
    public static readonly TimeSpan LockWait = TimeSpan.FromSeconds(3);

    /// <summary>Builds the context; empty when no store is installed.</summary>
    public async Task<string> BuildAsync(CancellationToken cancellationToken)
    {
        var config = ConfigFile.Load(environment);
        var catalog = StoreCatalog.Discover(environment.ExpandPath(config.StoresRoot));
        if (catalog.Stores.Count == 0)
        {
            return string.Empty;
        }

        // Stores are independent: update them at the same time.
        var updates = await Task.WhenAll(catalog.Stores.Select(s => UpdateAsync(s, cancellationToken))).ConfigureAwait(false);
        var notUpdated = catalog.Stores.Where((_, i) => !updates[i]).Select(s => s.Name).ToList();
        var unpublished = new List<string>();
        foreach (var store in catalog.Stores)
        {
            if (await StoreState.CountUnpublishedAsync(git, store, cancellationToken).ConfigureAwait(false) > 0)
            {
                unpublished.Add(store.Name);
            }
        }

        var machine = config.Machine ?? Environment.MachineName.ToLowerInvariant();
        var text = new StringBuilder();
        text.Append("Shared memory (memshare; read freely; write only when the user asks to save, then `memshare publish`): stores");
        foreach (var store in catalog.Stores)
        {
            text.Append(CultureInfo.InvariantCulture, $" `{store.Name}` ({DisplayPath.Of(store.Directory)})");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"; this machine `{machine}` ({MemshareEnvironment.OperatingSystemName}).");
        if (notUpdated.Count > 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Could not update: {string.Join(", ", notUpdated)} (offline or remote unreachable): working on the local copy.");
        }

        if (unpublished.Count > 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Unpublished changes in: {string.Join(", ", unpublished)} (`memshare diff`). Publish only if the user asks.");
        }

        await AppendProjectAsync(text, catalog, cancellationToken).ConfigureAwait(false);
        return text.ToString();
    }

    private async Task<bool> UpdateAsync(Store store, CancellationToken cancellationToken)
    {
        try
        {
            using var storeLock = await StoreLock.TryAcquireAsync(store, LockWait, cancellationToken).ConfigureAwait(false);
            if (storeLock is null)
            {
                return true; // Another operation is updating it right now.
            }

            return (await new StoreSync(git, time).PullAsync(store, cancellationToken).ConfigureAwait(false)).Succeeded;
        }
        catch (MemshareException)
        {
            return false;
        }
    }

    private async Task AppendProjectAsync(StringBuilder text, StoreCatalog catalog, CancellationToken cancellationToken)
    {
        var root = await ProjectLink.FindProjectRootAsync(git, environment.ProjectDirectory ?? environment.CurrentDirectory, cancellationToken)
            .ConfigureAwait(false);
        if (ProjectLink.Read(root) is not { } link)
        {
            return;
        }

        if (catalog.Find(link.StoreName) is not { } store)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"This project is linked to `{link.StoreName}/projects/{link.Slug}`, but store `{link.StoreName}` is not installed on this machine.");
            return;
        }

        var directory = store.ProjectDirectory(link.Slug);
        text.Append(CultureInfo.InvariantCulture, $"This project is linked to `{store.Name}/projects/{link.Slug}` ({DisplayPath.Of(directory)}).");
        var index = Path.Combine(directory, "MEMORY.md");
        if (File.Exists(index))
        {
            var content = await File.ReadAllTextAsync(index, cancellationToken).ConfigureAwait(false);
            text.AppendLine(" Its shared index (read the topic files when relevant):");
            text.AppendLine(Truncate(content, DisplayPath.Of(index)));
            var parsed = Merge.IndexDocument.Parse(content);
            var duplicates = Merge.IndexCheck.DuplicateTargets(parsed);
            if (duplicates.Count > 0)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"The index has several entries for {string.Join(", ", duplicates)} (often two machines' versions of one change): offer the user to keep one.");
            }

            var missing = Merge.IndexCheck.MissingTargets(parsed, directory);
            if (missing.Count > 0)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"The index points to missing files: {string.Join(", ", missing)}. Offer the user to fix the entries.");
            }
        }
        else
        {
            text.AppendLine(" No shared notes yet.");
        }

        var handoff = Path.Combine(directory, "handoff.md");
        if (File.Exists(handoff))
        {
            var published = await SharedProjects.LastCommitAsync(git, store, $"projects/{link.Slug}/handoff.md", cancellationToken).ConfigureAwait(false);
            var when = published is { } p ? "published " + p.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "not published yet";
            text.AppendLine(CultureInfo.InvariantCulture, $"A handoff exists ({DisplayPath.Of(handoff)}, {when}): read it before starting work, and tell the user where it left off.");
        }

        var copies = ConflictCopies.In(directory);
        if (copies.Count > 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Conflict copies there: {string.Join(", ", copies)}: another machine changed the same file; this machine's version was kept as the copy. Offer the user to merge them.");
        }
    }

    internal static string Truncate(string index, string path)
    {
        var trimmed = index.TrimEnd();
        if (trimmed.Length <= IndexBudget)
        {
            return trimmed;
        }

        var cut = trimmed.LastIndexOf('\n', IndexBudget);
        return trimmed[..(cut > 0 ? cut : IndexBudget)] + $"\n(The index continues: read {path} for the rest.)";
    }
}
