# ADR-0004: Stores, and project identity by slug

- **Status:** Accepted
- **Date:** 2026-11-05

## Context

The same project is often checked out at different paths on different machines and operating
systems. Identifying a project by its path would split it.

## Decision

- A **store** is a Git repository with a fixed layout ([design](../design.md#store-layout)):
  `global/` for every session, `projects/<slug>/` for one project, and a `memshare.json` with the
  format version. `memshare` refuses a format newer than it knows.
- A machine can use several stores, checked out under the stores root (default `~/agent-memshare/`).
- A project is identified by a **slug** (`a-z`, `0-9`, `-`). `memshare link` writes a local marker
  in the project (`.claude/memshare`, excluded from the project's Git through `.git/info/exclude`),
  which names the store and the slug. The link writes nothing to the store; the same slug links the
  project on every machine, whatever its path. `link` suggests a slug from the Git remote's
  repository name.
- Each machine reaches a store with its own **deploy key**, write access to that repository only;
  offboarding a machine means deleting its key on the Git host. Host keys are pinned per store.
  Commits use one neutral author, so they don't reveal which machine made them.

## Consequences

- Linking is a step per machine and clone, but no path translation is ever needed.
- Any Git host with deploy keys works (GitHub, GitLab, Gitea, a plain SSH server).
