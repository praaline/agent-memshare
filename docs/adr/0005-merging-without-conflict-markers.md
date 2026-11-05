# ADR-0005: Merging without conflict markers

- **Status:** Accepted
- **Date:** 2026-11-05

## Context

Several machines publish to the same store. Files are read by agents as instructions: a conflict
marker published into a file confuses every session. Indexes (`MEMORY.md`) change most often, one
line per entry; a line-based union keeps both versions of an edited entry and duplicates entries.

## Decision

- **`MEMORY.md`** is merged by `memshare merge-driver`, registered for `MEMORY.md` in the store's
  `.gitattributes` (`merge=memshare-index`) and in each clone's Git configuration (Git never takes
  drivers from a repository). The driver parses the index into a preamble and `##` sections, and
  each section into lines: entries keyed by their link target, other lines by their text. It merges
  three ways, sections by heading and lines by key:
  - added on either side, kept (after the line it followed, after our own additions there);
  - removed on one side and unchanged on the other, removed;
  - changed on one side, the change is kept, also against a removal on the other side;
  - an entry changed differently on both sides, both versions kept; the session-start hook then
    reports the duplicate entry for the user to resolve.

  The driver always succeeds and never writes conflict markers.
- **Any other conflict** (and the driver's last resort): the remote version stays in place and this
  machine's version is kept next to it as `<name>.conflict-<time>.md`. The session-start hook asks
  for the copies to be merged.
- Publishing never commits a file containing conflict markers.

## Consequences

- No change is lost and no file is broken. Real conflicts are rare and reach the user as copies to
  merge.
- A clone without the driver configured gets conflict copies instead of merged indexes; `memshare install`
  configures every store's clone, and `memshare doctor` reports a missing driver.
