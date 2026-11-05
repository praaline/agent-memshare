namespace Memshare.Stores;

/// <summary>A store checked out on this machine (ADR-0004).</summary>
/// <param name="Name">The store's name: its directory name under the stores root.</param>
/// <param name="Directory">The store's checkout.</param>
public sealed record Store(string Name, string Directory)
{
    /// <summary>The directory of a project's shared notes in this store.</summary>
    public string ProjectDirectory(string slug) => Path.Combine(Directory, "projects", slug);
}
