using Memshare.Configuration;
using Memshare.Git;
using Memshare.Platform;
using Memshare.Stores;

namespace Memshare.Cli.Commands;

/// <summary>What every command runs with: the process's environment, the user's git, the clock.</summary>
internal static class CommandContext
{
    public static MemshareEnvironment Environment { get; } = MemshareEnvironment.FromProcess();

    public static GitRunner Git { get; } = new();

    public static TimeProvider Time => TimeProvider.System;

    /// <summary>The stores installed on this machine.</summary>
    public static StoreCatalog Stores()
    {
        var config = ConfigFile.Load(Environment);
        return StoreCatalog.Discover(Environment.ExpandPath(config.StoresRoot));
    }
}
