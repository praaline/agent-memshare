# ADR-0007: Agent integration, Claude Code first

- **Status:** Accepted
- **Date:** 2026-11-05

## Context

The shared memory must reach the agent at the start of every session, and writes to it must stay
under the user's control. Claude Code is the first target; other agents read similar instruction
files.

## Decision

`memshare install` sets up Claude Code on the machine, idempotently, and `uninstall` reverses it:

- a **SessionStart hook** that runs the installed `memshare hook session-start`;
- an **ask** permission rule for edits under the stores root;
- a **managed block** in `~/.claude/CLAUDE.md`, between markers, importing each store's
  `global/CLAUDE.md` and naming the machine. The rest of the file is the user's and is preserved;
- two **skills**, versioned with the tool:
  - `memshare-save` reviews what the agent learned (its own memory and the session) and proposes
    shared notes. It decides whether each lesson is general (true in any project) or bound to one
    project; when unsure, it keeps the lesson local. It rewrites notes to be neutral and records no
    provenance. It ends with `memshare diff`; publishing is a separate request.
  - `memshare-handoff` writes a project's handoff with fixed sections.

Agent-specific code sits behind one interface, so other agents (for example those reading
`AGENTS.md`) can be added. A Claude Code plugin may later carry the hook and skills, but `install`
remains the single setup step, since a plugin can't carry the platform binary or the permission rule.

## Consequences

- One command sets up a machine, and `doctor` verifies it.
- Changes to the user's Claude Code settings are limited to the hook, one rule and one marked block.
