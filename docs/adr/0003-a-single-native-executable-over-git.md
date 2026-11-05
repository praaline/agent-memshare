# ADR-0003: A single native executable, over the git command

- **Status:** Accepted
- **Date:** 2026-11-05

## Context

The tool runs at every session start and as a Git merge driver, on Linux, Windows and macOS. It must
start fast and install without a runtime. It works on Git repositories that the user reaches with
their own SSH keys, deploy keys and credential helpers.

## Decision

- C# on .NET 10, published with **Native AOT**: one self-contained executable per platform
  (linux, win, osx × x64, arm64), named `memshare`.
- The same executable is the CLI, the session-start hook and the merge driver.
- Git operations run the user's **`git` executable**, not a Git library. It honours the user's SSH
  configuration, credential helpers and per-repository `core.sshCommand`, keeps rebase and
  merge-driver behaviour standard, and needs no native library.
- Code is AOT-compatible: JSON through source generation, no reflection-based serialisation, trim
  warnings treated as errors.

## Consequences

- Native AOT can't build for another operating system: release builds need a build machine for
  each OS.
- `git` is a runtime prerequisite.
