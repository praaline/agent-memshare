# User guide

## What you need

- Git, and Claude Code installed and signed in.
- A Git host for your stores, reachable from every machine: GitHub, GitLab, Gitea, or any SSH server.
- Linux, macOS (x64 or arm64) or Windows (x64 or arm64).

## Install on a machine

Linux and macOS:

```bash
curl -fsSL https://github.com/praaline/agent-memshare/releases/latest/download/install.sh | bash -s -- --machine laptop
```

Windows (PowerShell):

```powershell
& ([scriptblock]::Create((irm https://github.com/praaline/agent-memshare/releases/latest/download/install.ps1))) -Machine desktop
```

The machine name (lower-case letters, digits, hyphens) appears in the agent's session context. It is
never written to a store. The scripts verify the download against the release's `SHA256SUMS`, then
run `memshare install`, which:

- copies `memshare` to `~/.local/bin` (Windows: `%LOCALAPPDATA%\Programs\memshare`, which the
  PowerShell script adds to your `PATH`);
- writes its configuration to `~/.config/memshare/config.json` (Windows: `%APPDATA%\memshare`);
- adds to Claude Code (`~/.claude`): a session-start hook, a rule that makes every edit of a store
  need your approval, a marked block in `CLAUDE.md` that imports the stores' instructions, and the
  skills `memshare-save` and `memshare-handoff`. Your own settings and instructions are kept, and
  `settings.json` is backed up first.

Running `memshare install` again repairs or updates the setup. It also installs a newer executable
when you run it from one.

## Create a store

A store is a Git repository. Create an empty **private** repository on your Git host (for example
`my-memory`), then on your first machine:

```bash
memshare store add git@github.com:you/my-memory.git
```

For an SSH remote, memshare:

1. creates a deploy key for this store on this machine (`~/.ssh/memshare-<store>_ed25519`);
2. shows the host's key fingerprint, and pins it after you confirm it (compare with the fingerprint
   your Git host publishes, or pass the key with `--host-key`);
3. checks access. Until the host knows the deploy key, it prints the key and how to register it, and
   stops. Register the key with **write access**, then run the same command again:
   - GitHub: repository > Settings > Deploy keys > Add deploy key, tick "Allow write access";
   - GitLab: project > Settings > Repository > Deploy keys, tick "Grant write permissions";
   - Gitea: repository > Settings > Deploy Keys > Add Deploy Key, tick "Enable write access".

HTTPS remotes and local paths use your own Git credentials instead.

Then lay out the new store and publish it:

```bash
memshare store init
memshare diff
memshare publish -m "New store"
```

A store contains:

| Path | Content |
|---|---|
| `global/CLAUDE.md` | instructions for every session on every machine; keep it short |
| `global/<topic>.md` | longer shared notes, each referenced from `global/CLAUDE.md` |
| `projects/<slug>/MEMORY.md` | a project's index: one line per note, `- [Title](file.md) — hook` |
| `projects/<slug>/<topic>.md` | the project's notes |
| `projects/<slug>/handoff.md` | where the work stopped |

You can have several stores, for example one personal and one for work: run `store add` for each.

## Link your projects

In a project's directory:

```bash
memshare link              # suggests a name from the Git remote, or the directory
memshare link my-app       # or give it
```

Linking writes a small marker (`.claude/memshare`, kept out of the project's Git) and nothing else.
Use the same name on every machine: that is how the project's notes follow it, whatever its path.

## Daily use

- **Start a session as usual.** The hook updates the stores and tells the agent about them, the
  project's index and handoff, unpublished changes and anything needing attention.
- **Save something:** ask the agent, for example *"save to shared memory that deployments need the
  VPN"* or *"share what you learned in this project"*. The `memshare-save` skill decides with you
  what is general and what belongs to the project, and leaves the rest local. You approve each edit,
  then review the result with `memshare diff`.
- **Publish:** *"publish it"*, or `memshare publish -m "what changed"`. Until then nothing leaves the
  machine.
- **Hand off:** *"write a handoff"*. The `memshare-handoff` skill records the goal, state, next steps
  and pitfalls, and publishes them. The next session, on any machine, starts from there.

## Commands

| Command | What it does |
|---|---|
| `memshare status` | this machine, its stores, unpublished changes, the current project's link |
| `memshare diff [STORE]` | what publishing would send |
| `memshare publish [STORE] -m MESSAGE` | commit, merge with the remote, push |
| `memshare pull` | update the stores now (the hook does it at every session start) |
| `memshare projects` | the projects with shared notes, and when they were last published |
| `memshare link [SLUG] [--store S]` / `unlink` | link or unlink the current project |
| `memshare store add URL` / `init` / `remove NAME` | manage the stores on this machine |
| `memshare doctor [--offline]` | check the setup and say how to fix what's wrong |
| `memshare install` / `uninstall` | set up or remove the agent integration |

`STORE` can be left out when only one store is installed.

## Several machines

Install on each machine, run `memshare store add` with the same URL (each machine gets its own
deploy key), and link your projects with the same names. When two machines change files at the same
time:

- **Indexes** (`MEMORY.md`) merge entry by entry. If both changed the same entry differently, both
  versions are kept, and the next session points out the duplicate for you to resolve.
- **Other files:** the other machine's version stays in place, and yours is kept next to it as
  `<name>.conflict-<time>.md`. The next session offers to merge them. Nothing is lost.

## Secrets

Never put secrets in shared memory. As a safety net, `publish` stops when an added line contains
something with the format of a real secret: private keys, tokens of the main providers, passwords in
URLs. It ignores names (`password=`), references to variables (`${DB_PASSWORD}`, `$env:TOKEN`) and
placeholders. Findings are shown masked. If one is a false alarm, add `memshare:allow-secret` to the
line, or publish with `--allow-secrets`.

## Troubleshooting

Run `memshare doctor`: it checks Git, the configuration, each store (format, deploy key, remote,
merge driver, unpublished changes, conflict copies) and the agent integration, and says what to do
for each problem.

- **"Could not update" at session start:** offline, or the Git host is unreachable. The session uses
  the local copy, and the next session or `memshare pull` catches up.
- **"The host does not accept this machine's deploy key":** register the printed key with write
  access, then run the command again.
- **"Not published: what looks like a secret":** remove it; if it is not a secret, see
  [Secrets](#secrets).
- **"is busy":** another memshare operation on the same store is running; try again in a moment.

## Updating and removing

- **Update:** run the install script again, or run `memshare install` from a newer executable.
- **Retire a machine:** delete its deploy key on the Git host. The machine can no longer read or
  publish.
- **Remove from a machine:** `memshare uninstall` removes the hook, the rule, the instructions block
  and the skills. Stores, keys, the configuration and the executable stay; delete them by hand if you
  want: the stores root, `~/.ssh/memshare-*`, the configuration directory and the executable.
