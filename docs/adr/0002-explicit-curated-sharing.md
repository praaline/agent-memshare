# ADR-0002: Explicit, curated sharing

- **Status:** Accepted
- **Date:** 2026-11-05

## Context

Coding agents keep their memory on the machine. There are two ways to share it between machines:

- **Synchronise everything the agent writes**, automatically, in the background, or capture
  conversations and recall them by similarity.
- **Share deliberately** what the user chooses, as reviewed text.

What is shared becomes instructions in every session on every machine. A wrong, outdated or
sensitive note written once, unseen, spreads everywhere. Instructions the user wrote, such as
standing preferences, architecture guidance or how to use their infrastructure, are not captured by
automatic approaches at all.

## Decision

`memshare` shares deliberately:

- Nothing is written to a store unless the user asks. The agent's permission settings make every
  edit of a store need the user's approval.
- Nothing leaves the machine until `memshare publish`. No hook commits or pushes.
- The content is Markdown in Git: readable, reviewable with `memshare diff`, versioned, revertible.
- There is no background process, database, embedding or model call.
- A skill helps turn what the agent learned on one machine into proposed shared notes. The user
  reviews them before anything is published.

## Consequences

- Shared memory stays small and trusted, at the cost of a deliberate step to share something.
- `memshare` can run alongside tools that capture or synchronise automatically. It doesn't touch the
  agent's own memory.
