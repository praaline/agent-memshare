# agent-memshare

Curated, shared memory for coding agents across your machines.

Coding agents remember things per machine. If you work on a laptop and a desktop, or on Linux and
Windows, what you taught the agent on one is missing on the other. `memshare` shares the knowledge
*you choose* with every machine you use:

- your global instructions: preferences, ways of working, your tools and infrastructure;
- each project's notes, with an index the agent reads at the start of every session;
- handoffs, to continue the work in a later session or on another machine.

It all lives as Markdown in Git repositories ("stores") that you control, on any Git host. Claude Code
is supported today.

## How it works

```
you: "save this to shared memory"          another machine, next session
          │                                            ▲
   agent writes a note in the store   ──►  memshare publish  ──►  Git host  ──►  session-start hook
   (you approve the edit, review the diff)                                      pulls, shows the
                                                                                project's notes
```

- At the start of each session, a hook updates the stores and tells the agent where they are, and
  shows the current project's index and handoff.
- The stores' global instructions are imported into every session.
- When you ask, the agent writes notes (the `memshare-save` skill helps it decide what is worth
  sharing, and where). You review the diff and publish it.

## Principles

- **You decide what is shared.** Nothing is shared unless you ask, every edit of a store needs your
  approval, and nothing leaves the machine until you `publish`.
- **Projects are identified by name, not by path.** The same project can live at different paths on
  Linux, Windows and macOS.
- **Read live, never copy.** Shared instructions are loaded from one place into every session.
- **Never break a file.** Indexes changed on two machines are merged entry by entry. Other
  concurrent edits are kept side by side for you to merge. Conflict markers are never published.
- **Least privilege.** One revocable deploy key per machine and store, and pinned host keys.
- **No secrets.** Publishing stops on values that look like real secrets (keys, tokens, passwords in
  URLs), without false alarms on names or variable references.
- **Simple.** One executable, Git, files and one session-start hook. No background process, no
  database, no model calls.

## Quick start

Install on each machine (the scripts download the release for your platform, check its checksum and
run `memshare install`):

```bash
# Linux, macOS
curl -fsSL https://github.com/praaline/agent-memshare/releases/latest/download/install.sh | bash -s -- --machine laptop
```

```powershell
# Windows (PowerShell)
& ([scriptblock]::Create((irm https://github.com/praaline/agent-memshare/releases/latest/download/install.ps1))) -Machine desktop
```

Create an empty **private** repository for your store on your Git host, then:

```bash
memshare store add git@github.com:you/my-memory.git   # creates this machine's deploy key, explains how to register it
memshare store init                                   # first machine only: lays out the store
memshare publish -m "New store"
cd ~/src/my-project && memshare link                  # links the project (suggests a name from its remote)
```

Then, in a Claude Code session: *"save to shared memory that our integration tests need Docker"*, or
*"write a handoff"*. The [user guide](docs/user-guide.md) covers every step, Git hosts, Windows and
troubleshooting.

## Compared with similar tools

| | memshare | [claude-memsync](https://github.com/MarimerLLC/claude-utils) | [memmy-agent](https://github.com/MemTensor/memmy-agent) |
|---|---|---|---|
| What is shared | what you choose: instructions, project notes, handoffs | everything Claude Code writes to its auto memory | captured conversation turns, summarised and embedded |
| Who decides | you, per change, after reviewing the diff | automatic | automatic, selected by models |
| Across machines | yes, through Git | yes, through Git | a local memory service; see its documentation for other devices |
| Across agents | Claude Code (others can be added) | Claude Code | many agents on one machine |
| Project identity | a name you give, independent of paths | the project's absolute path (same paths needed everywhere) | its own sessions and sources |
| What runs | a hook at session start | a background daemon | local services, plus models for summaries and embeddings |
| Storage | Markdown in Git | Markdown in Git | SQLite with vectors |

They solve different problems and can run alongside memshare:

- **claude-memsync** synchronises Claude Code's own memory automatically. memshare never touches that
  memory, so both can be used: claude-memsync for everything the agent notes, memshare for the curated
  knowledge you want in every session and project. memshare's save skill draws on claude-memsync's
  `/distill` rubric for telling general lessons from project ones.
- **memmy-agent** recalls past conversations across agents on one machine. memshare adds knowledge you
  wrote and reviewed, shared across machines.

## Documentation

- [User guide](docs/user-guide.md): installing, stores and Git hosts, daily use, several machines,
  troubleshooting
- [Security](docs/security.md): the trust model, keys, secrets
- [Design overview](docs/design.md) and [architecture decisions](docs/adr/)

## Building from source

.NET 10 SDK; Native AOT also needs the platform's native toolchain (clang on Linux, Xcode command-line
tools on macOS, "Desktop development with C++" on Windows).

```bash
dotnet test
dotnet publish src/Memshare.Cli -c Release -r linux-x64 -o artifacts/linux-x64   # or win-x64, osx-arm64, …
./artifacts/linux-x64/memshare install --machine laptop
```

## License

[MIT](LICENSE)
