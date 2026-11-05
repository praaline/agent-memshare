---
name: memshare-handoff
description: Write a handoff for the current project in shared memory (memshare), so that a later session or another machine can continue the work. Use when the user asks for a handoff, to continue later or elsewhere, or to record where the work stopped.
---

# memshare-handoff: record where the work stopped

A handoff is `projects/<slug>/handoff.md` in the project's store. The session-start hook shows it,
with the date it was published, at the start of every session in the project, on every machine.
Write what the next session needs to continue, not the story of this one.

## Steps

1. **Find the project.** The session-start context names the linked project
   (`<store>/projects/<slug>`). If the project isn't linked, propose a slug (`memshare link`
   without one suggests it), link it with the user's agreement, and continue.

2. **Gather the facts**, checking rather than recalling:
   - the branch, the last commit, and whether it is pushed (`git status -sb`, `git log -1 --oneline`);
   - what works and what doesn't: tests run, builds, what was verified and how;
   - what was done in this session;
   - proposals waiting for the user's decision;
   - the next steps, in order;
   - pitfalls and context a newcomer would trip over.

3. **Write `handoff.md`**, replacing the previous one (it stays in the store's history):

   ```markdown
   # Handoff: <project> (<yyyy-mm-dd>)

   ## Goal
   ## State
   ## Done in this session
   ## Awaiting decision
   ## Next steps
   ## Pitfalls and context
   ```

   Be concrete: commit hashes, file paths, commands, exact error messages. Say which machine or
   platform a path or command is for. No secrets, nothing about other projects.

4. **Index it.** `projects/<slug>/MEMORY.md` has one line for the handoff; add it or update it:
   `- [Handoff](handoff.md) — state at <commit> (<date>), next: <first step>`.

5. **Show and publish.** Run `memshare diff <store>` and show the user the handoff. A handoff is
   meant to be published: publish it (`memshare publish <store> -m "<project>: handoff"`) unless
   the user wants changes first. If publish stops on something that looks like a secret, show it
   to the user; never bypass the check on your own.
