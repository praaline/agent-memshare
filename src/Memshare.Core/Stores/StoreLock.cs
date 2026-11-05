namespace Memshare.Stores;

/// <summary>
/// One git operation at a time per store: sessions on the same machine may pull while another
/// publishes. The lock is a file in the store's <c>.git</c> directory, created atomically; a lock
/// older than <see cref="StaleAfter"/> was left by a process that died and is taken over.
/// </summary>
public sealed class StoreLock : IDisposable
{
    /// <summary>The age after which a lock is considered abandoned.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(200);

    private readonly string _path;

    private StoreLock(string path) => _path = path;

    /// <summary>The lock file of a store.</summary>
    public static string PathOf(Store store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return Path.Combine(store.Directory, ".git", "memshare.lock");
    }

    /// <summary>Takes the store's lock, waiting at most <paramref name="wait"/>; null if it stayed busy.</summary>
    public static async Task<StoreLock?> TryAcquireAsync(Store store, TimeSpan wait, CancellationToken cancellationToken)
    {
        var path = PathOf(store);
        var deadline = DateTime.UtcNow + wait;
        while (true)
        {
            try
            {
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    file.Write(System.Text.Encoding.UTF8.GetBytes(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }

                return new StoreLock(path);
            }
            catch (IOException) when (File.Exists(path))
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > StaleAfter)
                {
                    TryDelete(path);
                    continue;
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }

            await Task.Delay(RetryInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Takes the store's lock, or explains that the store is busy.</summary>
    /// <exception cref="MemshareException">The lock stayed busy.</exception>
    public static async Task<StoreLock> AcquireAsync(Store store, TimeSpan wait, CancellationToken cancellationToken) =>
        await TryAcquireAsync(store, wait, cancellationToken).ConfigureAwait(false)
        ?? throw new MemshareException($"Store {store.Name} is busy with another memshare operation: try again in a moment.");

    public void Dispose() => TryDelete(_path);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Another process took it over or removed it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }
}
