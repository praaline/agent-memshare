namespace Memshare.Configuration;

/// <summary>
/// memshare's configuration on one machine. It is never shared: each machine has its own.
/// </summary>
public sealed record MemshareConfig
{
    /// <summary>The default directory under which stores are checked out.</summary>
    public const string DefaultStoresRoot = "~/agent-memshare";

    /// <summary>The version of this file's format.</summary>
    public int Format { get; init; } = 1;

    /// <summary>This machine's name, shown to the agent; never written to a store.</summary>
    public string? Machine { get; init; }

    /// <summary>The directory under which stores are checked out, one directory per store.</summary>
    public string StoresRoot { get; init; } = DefaultStoresRoot;
}
