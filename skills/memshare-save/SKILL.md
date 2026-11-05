---
name: memshare-save
description: Save knowledge to shared memory (memshare) so that sessions on the user's other machines get it. Use when the user asks to save, share or remember something in shared memory, or to promote what was learned in this project or session (including this machine's auto memory) to shared memory. Proposes the notes and shows the diff; publishing is a separate request.
---

# memshare-save: propose shared notes

Shared memory is the user's curated knowledge, shared between their machines through memshare
stores (Git repositories). What is published becomes instructions in every session on every
machine, so it must be correct, general where it claims to be, and free of secrets. You propose;
the user decides.

## Where things are

The session-start context names the stores, their paths, this machine and the linked project
(`<store>/projects/<slug>`). If it is missing, run `memshare status`. Don't explore the home
directory to orient yourself. With several stores, ask which one, unless the user named it.

A store holds:

- `global/CLAUDE.md`: short instructions for every session, imported on every machine;
- `global/<topic>.md`: longer shared notes, each referenced from `global/CLAUDE.md` with a line
  saying when to read it;
- `projects/<slug>/MEMORY.md`: one project's index, one line per note,
  `- [Title](file.md) — one-line hook`; and the notes `projects/<slug>/<topic>.md`.

## Steps

1. **Collect the candidates.** What the user asked to save, in their words. If they asked to share
   what was learned, also read this project's auto memory (the directory named in your memory
   instructions) and recall the session. Skip what is already in the store: read the target index
   and files first.

2. **Classify each candidate.**

   | Verdict | Means | Goes to |
   |---|---|---|
   | global | true in any project on any of the user's machines: their preferences and way of working, their tools and infrastructure, OS and shell behaviour, CLI gotchas | `global/` |
   | project | true for this project only: its code, decisions, deployment, conventions, state | `projects/<slug>/` |
   | local | true on this machine only (paths, local setup), transient, or not worth sharing | stays out |

   - **Cross-stack test** for global: would it still help in a project on a completely different
     stack? A fact about one library or framework fails it: it is project knowledge, even if other
     projects use the same library.
   - **When unsure, choose the narrower scope** (project rather than global, local rather than
     project). A wrong promotion reaches every session; a missed one costs little.
   - **Never shared:** secrets of any kind (passwords, tokens, keys, connection strings with
     credentials); anything about another project; where a lesson came from ("learned in project
     X", "from a conversation"); the content of conversations.

3. **Write the notes.**
   - Rewrite each lesson to stand on its own, general for its scope, with a usable example. When a
     path or command is for one platform, say which (Linux, Windows/PowerShell, macOS).
   - Prefer updating an existing note to adding a near-duplicate.
   - Project notes: the topic file plus its index line in `projects/<slug>/MEMORY.md`.
   - Global notes: a short rule goes into `global/CLAUDE.md` directly; a longer note goes into
     `global/<topic>.md` with a one-line pointer in `global/CLAUDE.md`.
   - If the project is not linked yet, propose a slug (`memshare link` without one suggests it)
     and link it before writing its notes.
   - Leave the auto memory as it is: it stays on this machine.

4. **Show the result.** Run `memshare diff <store>` and summarise for the user: what you propose
   (global or project, and why), what you left local and why. Ask whether to publish.

5. **Publish only when the user says so**, with `memshare publish <store> -m "<what>"`. If publish
   stops on something that looks like a secret, show the user what it found and fix it with them;
   never bypass the check on your own.
