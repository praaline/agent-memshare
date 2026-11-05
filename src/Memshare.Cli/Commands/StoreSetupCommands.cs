using System.CommandLine;

using Memshare.Platform;
using Memshare.Stores;

namespace Memshare.Cli.Commands;

/// <summary><c>memshare store add | init | remove</c>.</summary>
internal static class StoreSetupCommands
{
    /// <summary>Exit code when the host doesn't accept the deploy key yet.</summary>
    private const int KeyNotAccepted = 3;

    public static Command Create() => new("store", "Add, initialise or remove stores on this machine.") { Add(), Init(), Remove() };

    private static StoreInstaller Installer() =>
        new(CommandContext.Environment, CommandContext.Git, Integrations.All, Integrations.InstalledExecutable, CommandContext.Time);

    private static Command Add()
    {
        var url = new Argument<string>("url") { Description = "The store's Git remote (SSH, HTTPS or a local path)." };
        var name = new Option<string?>("--name") { Description = "The store's name on this machine; by default the repository's name." };
        var hostKey = new Option<string?>("--host-key") { Description = "The SSH host key to pin, as a known_hosts line, instead of reading it from the host." };
        var trust = new Option<bool>("--trust-host-key") { Description = "Pin the host key read from the host without asking (only on a network you trust)." };
        var command = new Command("add", "Clone a store onto this machine, with its own deploy key for SSH remotes.") { url, name, hostKey, trust };
        command.SetAction(async (parse, cancellationToken) =>
        {
            var result = await Installer().AddAsync(
                parse.GetValue(url)!,
                parse.GetValue(name),
                parse.GetValue(hostKey),
                fingerprint => parse.GetValue(trust) || ConfirmHostKey(fingerprint),
                cancellationToken).ConfigureAwait(false);

            if (result.Store is null)
            {
                await Console.Out.WriteLineAsync($"""
                    The host does not accept this machine's deploy key for the store yet. Register it with write access:
                      GitHub:  the repository > Settings > Deploy keys > Add deploy key, tick "Allow write access"
                      GitLab:  the project > Settings > Repository > Deploy keys, tick "Grant write permissions"
                      Gitea:   the repository > Settings > Deploy Keys > Add Deploy Key, tick "Enable write access"
                    Key:
                      {result.PublicKey}
                    Then run the same command again.
                    """).ConfigureAwait(false);
                return KeyNotAccepted;
            }

            await Console.Out.WriteLineAsync($"added store {result.Store.Name} in {DisplayPath.Of(result.Store.Directory)}").ConfigureAwait(false);
            if (result.IsEmpty)
            {
                await Console.Out.WriteLineAsync($"The store is empty: memshare store init {result.Store.Name}, review, then memshare publish {result.Store.Name} -m \"New store\".").ConfigureAwait(false);
            }
            else if (result.NeedsInit)
            {
                await Console.Out.WriteLineAsync($"The store has no {StoreManifest.FileName}: memshare store init {result.Store.Name} adds it (and any missing template file); then publish.").ConfigureAwait(false);
            }

            return 0;
        });
        return command;
    }

    private static Command Init()
    {
        var store = new Argument<string?>("store") { Description = "The store; may be left out when only one is installed.", Arity = ArgumentArity.ZeroOrOne };
        var command = new Command("init", "Add the files of a new store that it lacks (never overwriting one); review and publish them.") { store };
        command.SetAction(async (parse, _) =>
        {
            var added = Installer().Init(parse.GetValue(store));
            await Console.Out.WriteLineAsync(added.Count == 0
                ? "nothing to add: the store has every template file."
                : $"added {string.Join(", ", added)}: review, then memshare publish -m \"...\".").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command Remove()
    {
        var store = new Argument<string>("store") { Description = "The store to remove from this machine." };
        var force = new Option<bool>("--force") { Description = "Remove it even with unpublished changes (they are lost)." };
        var command = new Command("remove", "Remove a store's checkout from this machine; its remote is untouched.") { store, force };
        command.SetAction(async (parse, cancellationToken) =>
        {
            var removed = await Installer().RemoveAsync(parse.GetValue(store)!, parse.GetValue(force), cancellationToken).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"""
                removed {DisplayPath.Of(removed.Directory)}.
                The deploy key stays ({DisplayPath.Of(SshAccess.KeyPath(CommandContext.Environment, removed.Name))}): to retire this machine's access, delete the key on the Git host too.
                """).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static bool ConfirmHostKey(string fingerprint)
    {
        if (Console.IsInputRedirected)
        {
            throw new MemshareException($"The host's key must be confirmed ({fingerprint}): run interactively, or give it with --host-key.");
        }

        Console.Out.Write($"The Git host's SSH key is {fingerprint}\nCompare it with the fingerprint your Git host publishes. Trust it? [y/N] ");
        var answer = Console.In.ReadLine();
        return string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase) || string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }
}
