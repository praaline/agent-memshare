using Memshare.Configuration;
using Memshare.Platform;
using Memshare.Stores;

namespace Memshare.Agents;

/// <summary>What an agent integration needs to set itself up on a machine.</summary>
/// <param name="Environment">The machine.</param>
/// <param name="Config">memshare's configuration on it.</param>
/// <param name="Stores">The stores installed on it.</param>
/// <param name="ExecutablePath">The installed memshare executable, which the agent's hook runs.</param>
/// <param name="Time">The clock, for backup file names.</param>
public sealed record InstallContext(
    MemshareEnvironment Environment,
    MemshareConfig Config,
    StoreCatalog Stores,
    string ExecutablePath,
    TimeProvider Time)
{
    /// <summary>The machine's name as shown to the agent.</summary>
    public string Machine => Config.Machine ?? System.Environment.MachineName.ToLowerInvariant();
}
