# Changelog

All notable changes are listed here. Versions follow [Semantic Versioning](https://semver.org); before
1.0, a minor version may change behaviour or file formats, and says so.

## 0.1.1 (2026-11-06)

- Windows install script: puts the install directory on the PATH before running `memshare install`,
  which no longer reports it as missing just before the script adds it.

## 0.1.0 (2026-11-05)

First release.

- Stores: Markdown in Git, with `global/` instructions imported into every session and per-project
  notes, indexes and handoffs.
- Commands: `install`, `uninstall`, `doctor`, `store add | init | remove`, `status`, `diff`,
  `publish`, `pull`, `projects`, `link`, `unlink`.
- Claude Code: a session-start hook, an approval rule for edits of stores, a managed instructions
  block, and the skills `memshare-save` and `memshare-handoff`.
- Deploy key per machine and store, pinned host keys, a neutral commit author.
- Merging: indexes entry by entry (a Git merge driver), other files kept side by side; conflict
  markers are never published.
- A conservative secret check on publish.
- Linux, macOS and Windows, x64 and arm64: one Native AOT executable.
