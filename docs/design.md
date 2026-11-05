# Design overview

How `memshare` works. The decisions behind it are in [the ADRs](adr/).

## Concepts

| Concept | Meaning |
|---|---|
| Store | A Git repository with a fixed layout, checked out under the stores root (default `~/agent-memshare/`). A machine can use several stores, for example one personal and one for work. |
| Machine | A named installation. The name stays on the machine: it appears in the session context, never in commits. |
| Link | A local marker in a project (`.claude/memshare`, kept out of the project's Git) naming a store and a project slug. Per machine and per clone. |
| Publish | Commit a store's changes, rebase on the remote, push. The only operation that sends anything. |
| Handoff | `projects/<slug>/handoff.md`: where work stopped, for the next session or machine. |
| Conflict copy | `<name>.conflict-<time>.md`: this machine's version of a file that another machine changed too. |

## Store layout

```
<store>/
├─ memshare.json            format version, e.g. {"format": 1}
├─ README.md
├─ .gitattributes           LF line endings; MEMORY.md uses the memshare merge driver
├─ global/CLAUDE.md         instructions for every session (imported)
├─ global/*.md              other shared notes, referenced from global/CLAUDE.md
└─ projects/<slug>/         MEMORY.md (index), topic files, handoff.md
```

Other folders are allowed and are read only when an instruction points to them.

## Commands

| Command | What it does |
|---|---|
| `memshare install --machine NAME [--stores-root PATH]` | Sets up this machine: copies the executable to `~/.local/bin` (`%LOCALAPPDATA%\Programs\memshare` on Windows), writes the configuration, and adds the agent's hook, permission rule and instructions block. Idempotent. |
| `memshare uninstall` | Removes the hook, the permission rule and the instructions block. Stores, keys, configuration and the executable stay. |
| `memshare store add URL [--name N] [--host-key LINE]` | For SSH remotes: creates the store's deploy key, pins the host key (after you confirm its fingerprint), and if the host doesn't accept the key yet, explains how to register it. Then clones and configures the store. |
| `memshare store init [STORE]` | Adds the template files a store lacks (never overwriting one): lays out a new store, or completes an older one. |
| `memshare store remove NAME [--force]` | Removes a store's checkout from this machine (refused with unpublished changes unless forced). |
| `memshare status` | Machine, stores, unpublished changes, link of the current project. |
| `memshare diff [STORE]` | Unpublished changes, new files included. |
| `memshare publish [STORE] -m MESSAGE` | Secret check, commit, rebase (merge driver, conflict copies), push. |
| `memshare pull` | Updates every store now. |
| `memshare projects` | Shared projects and their last publish. |
| `memshare link [SLUG] [--store S]` | Links the current project; without a slug, suggests one from the Git remote. |
| `memshare unlink` | Removes the link. |
| `memshare doctor [--offline]` | Checks Git, the configuration, each store (format, deploy key, remote, unpublished changes, conflict copies), the agent's hook, permission rule and instructions block, and the current project's link. |
| `memshare hook session-start` | Called by the agent at session start. Reads only and never fails the session. |
| `memshare merge-driver BASE OURS THEIRS` | Called by Git to merge `MEMORY.md`. |

## Files on a machine

| Path | Content |
|---|---|
| `~/.config/memshare/config.json` (`%APPDATA%\memshare` on Windows) | machine name, stores root |
| `~/.config/memshare/known_hosts` | pinned SSH host keys of the stores' Git hosts |
| `~/.ssh/memshare-<store>_ed25519` | the store's deploy key on this machine |
| `~/agent-memshare/<store>/` | the stores |
| `~/.claude/settings.json` | the session-start hook and the ask rule (the rest is untouched) |
| `~/.claude/CLAUDE.md` | the block between `<!-- memshare:begin -->` and `<!-- memshare:end -->` (the rest is yours) |
| `~/.claude/skills/memshare-save/`, `memshare-handoff/` | the skills, installed from the executable (replaced on each install) |

## Session start

1. Pull each store, with a lock and a timeout per store and a bound on the whole hook.
2. Tell the agent: the stores and their paths; this machine and its OS; stores that could not be
   updated; unpublished changes; the linked project's index (within a size budget) and its handoff
   with the date it was published; conflict copies waiting to be merged; duplicate index entries
   and entries pointing to missing files.

## Skills

Installed for the agent by `memshare install`, in the version that matches the executable:

- **memshare-save**: proposes shared notes from what the user asks to save, or from what the agent
  learned (the project's auto memory and the session). Each candidate is classified as global (true
  in any project: the user's preferences, tools and infrastructure, OS and CLI behaviour), project, or
  local; a lesson about one library stays project knowledge; when unsure, the narrower scope wins.
  Notes are rewritten to stand alone, without secrets, other projects or provenance. It ends with
  `memshare diff`; publishing is the user's call.
- **memshare-handoff**: writes `projects/<slug>/handoff.md` with fixed sections (goal, state, done,
  awaiting decision, next steps, pitfalls) from checked facts, indexes it, and publishes it.

## Components

```
src/Memshare.Cli      the memshare executable: commands, hook, merge driver entry point
src/Memshare.Core     configuration, stores, Git, links, publish and pull, secret check, agent integration
src/Memshare.Merge    MEMORY.md parser and three-way entry merge
skills/               memshare-save, memshare-handoff
templates/store/      layout of a new store
tests/                unit tests and integration tests (two clones of a bare repository act as two machines)
```
