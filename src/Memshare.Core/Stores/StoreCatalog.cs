namespace Memshare.Stores;

/// <summary>The stores checked out on this machine: the Git repositories directly under the stores root.</summary>
public sealed class StoreCatalog
{
    private StoreCatalog(string root, IReadOnlyList<Store> stores)
    {
        Root = root;
        Stores = stores;
    }

    /// <summary>The stores root.</summary>
    public string Root { get; }

    /// <summary>The stores, ordered by name.</summary>
    public IReadOnlyList<Store> Stores { get; }

    /// <summary>Finds the stores under a root. A missing root means no stores.</summary>
    public static StoreCatalog Discover(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!Directory.Exists(root))
        {
            return new StoreCatalog(root, []);
        }

        var stores = Directory.EnumerateDirectories(root)
            .Where(d => Path.Exists(Path.Combine(d, ".git")))
            .Select(d => new Store(Path.GetFileName(d), d))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList();
        return new StoreCatalog(root, stores);
    }

    /// <summary>The store with this name, if there is one.</summary>
    public Store? Find(string name) =>
        Stores.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// The store a command works on: the one named, or the only one installed.
    /// </summary>
    /// <exception cref="MemshareException">No such store, no store at all, or several and none named.</exception>
    public Store Pick(string? name)
    {
        if (name is not null)
        {
            return Find(name) ?? throw new MemshareException($"There is no store '{name}' in {Root}.");
        }

        return Stores.Count switch
        {
            0 => throw new MemshareException($"No store is installed in {Root}."),
            1 => Stores[0],
            _ => throw new MemshareException(
                $"Several stores are installed ({string.Join(", ", Stores.Select(s => s.Name))}): name one."),
        };
    }
}
