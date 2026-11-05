using Memshare.Configuration;
using Memshare.Platform;
using Memshare.Stores;

namespace Memshare.Tests.Support;

/// <summary>
/// One of the user's machines in a test: its own clone of a store, its own memshare configuration,
/// and the operations a user runs on it.
/// </summary>
internal sealed class Machine
{
    private readonly Sandbox _sandbox;

    private Machine(Sandbox sandbox, string name, Store store, string storesRoot)
    {
        _sandbox = sandbox;
        Name = name;
        Store = store;
        StoresRoot = storesRoot;
        ConfigDirectory = sandbox.Directory("machines", name, "config");
        ConfigFile.Save(Environment(), new MemshareConfig { Machine = name, StoresRoot = storesRoot });
    }

    public string Name { get; }

    public Store Store { get; }

    public string StoresRoot { get; }

    public string ConfigDirectory { get; }

    public TimeProvider Time { get; set; } = new FixedTime();

    public Memshare.Git.GitRunner Git => _sandbox.Git;

    public static async Task<Machine> CloneAsync(Sandbox sandbox, StoreRemote remote, string name, string store = "notes") =>
        new(sandbox, name, new Store(store, await remote.CloneAsync(name, store)), remote.StoresRoot(name));

    public MemshareEnvironment Environment(string? currentDirectory = null, string? projectDirectory = null) =>
        new(_sandbox.Home, ConfigDirectory, currentDirectory ?? _sandbox.Home, projectDirectory);

    public string PathOf(string relative) => Path.Combine(Store.Directory, relative);

    public void Write(string relative, string content)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public string Read(string relative) => File.ReadAllText(PathOf(relative));

    public bool Has(string relative) => File.Exists(PathOf(relative));

    public void Delete(string relative) => File.Delete(PathOf(relative));

    public Task<PublishResult> PublishAsync(string message = "notes", TimeSpan? lockWait = null, bool allowSecrets = false) =>
        new StorePublisher(_sandbox.Git, Time, lockWait).PublishAsync(Store, message, allowSecrets, TestContext.Current.CancellationToken);

    public async Task<PullResult> PullAsync()
    {
        using var storeLock = await StoreLock.AcquireAsync(Store, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        return await new StoreSync(_sandbox.Git, Time).PullAsync(Store, TestContext.Current.CancellationToken);
    }

    public Task<int> UnpublishedAsync() =>
        StoreState.CountUnpublishedAsync(_sandbox.Git, Store, TestContext.Current.CancellationToken);

    /// <summary>Every file of the store's working tree, outside .git.</summary>
    public IEnumerable<string> Files() =>
        Directory.EnumerateFiles(Store.Directory, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
}
