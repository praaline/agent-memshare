namespace Memshare.Tests.Support;

/// <summary>
/// A store's remote as a bare repository in the sandbox, with an initial commit, and clones of it
/// that act as the store on different machines.
/// </summary>
internal sealed class StoreRemote
{
    private readonly Sandbox _sandbox;

    private StoreRemote(Sandbox sandbox, string path)
    {
        _sandbox = sandbox;
        Path = path;
    }

    public string Path { get; }

    public static async Task<StoreRemote> CreateAsync(Sandbox sandbox, string name = "notes")
    {
        var path = sandbox.Directory("remotes", name + ".git");
        await sandbox.GitOk(path, "init", "--bare", "--quiet");
        var remote = new StoreRemote(sandbox, path);

        var seed = await remote.CloneAsync("seed", name);
        File.WriteAllText(System.IO.Path.Combine(seed, "README.md"), "# Store\n");
        File.WriteAllText(System.IO.Path.Combine(seed, ".gitattributes"), "* text=auto eol=lf\n**/MEMORY.md merge=union\n");
        await sandbox.GitOk(seed, "add", "-A");
        await sandbox.GitOk(seed, "commit", "--quiet", "-m", "Initial commit");
        await sandbox.GitOk(seed, "push", "--quiet", "-u", "origin", "main");
        return remote;
    }

    /// <summary>A remote nobody has published to yet.</summary>
    public static async Task<StoreRemote> CreateEmptyAsync(Sandbox sandbox, string name = "notes")
    {
        var path = sandbox.Directory("remotes", name + ".git");
        await sandbox.GitOk(path, "init", "--bare", "--quiet");
        return new StoreRemote(sandbox, path);
    }

    /// <summary>Clones the store into a machine's stores root (<c>machines/&lt;machine&gt;/stores/&lt;name&gt;</c>).</summary>
    public async Task<string> CloneAsync(string machine, string name = "notes")
    {
        var storesRoot = StoresRoot(machine);
        var target = System.IO.Path.Combine(storesRoot, name);
        await _sandbox.GitOk(storesRoot, "clone", "--quiet", Path, target);
        return target;
    }

    public string StoresRoot(string machine) => _sandbox.Directory("machines", machine, "stores");
}
