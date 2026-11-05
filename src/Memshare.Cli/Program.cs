using System.CommandLine;

using Memshare;
using Memshare.Cli.Commands;

var root = new RootCommand("memshare: curated, shared memory for coding agents across your machines.")
{
    StatusCommand.Create(),
    StoreCommands.Diff(),
    StoreCommands.Publish(),
    StoreCommands.Pull(),
    StoreCommands.Projects(),
    LinkCommands.Link(),
    LinkCommands.Unlink(),
    HookCommands.Create(),
    SetupCommands.Install(),
    SetupCommands.Uninstall(),
    SetupCommands.Doctor(),
    StoreSetupCommands.Create(),
    MergeDriverCommand.Create(),
};

try
{
    return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
}
catch (MemshareException e)
{
    await Console.Error.WriteLineAsync($"memshare: {e.Message}").ConfigureAwait(false);
    return 1;
}
