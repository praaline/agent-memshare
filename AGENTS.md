# AGENTS.md

Instructions for AI coding agents working on this repository (Claude Code, Codex, and any tool that reads
`AGENTS.md`). Humans read the same documents.

## Project in one paragraph

`memshare` shares curated memory for coding agents across a user's machines: global instructions,
per-project notes and handoffs, kept as Markdown in Git repositories ("stores") that the user controls. Nothing
is shared unless the user asks, and nothing leaves a machine until `memshare publish`
([ADR-0002](docs/adr/0002-explicit-curated-sharing.md)). One .NET 10 Native AOT executable, which is the
command line, the agent's session-start hook and a Git merge driver, and which runs the user's `git`
([ADR-0003](docs/adr/0003-a-single-native-executable-over-git.md)). Start with the
[design overview](docs/design.md) and the [ADRs](docs/adr/).

## Commands

| Task | Command (repository root) |
|---|---|
| Build (warnings are errors) | `dotnet build` |
| All tests | `dotnet test` |
| One test class or method | `dotnet test --filter-class "*StoreTests"`, `--filter-method "*Unpublished*"` |
| Format check | `dotnet format --verify-no-changes` |
| Native executable | `dotnet publish src/Memshare.Cli -c Release -r linux-x64 -o artifacts/linux-x64` (also `win-x64`, `osx-arm64`, …, each on its own OS) |
| Run from source | `dotnet run --project src/Memshare.Cli -- status` |

The test runner is Microsoft.Testing.Platform (`global.json`).

## Rules

1. **Native AOT stays clean**: no reflection-based serialisation (JSON through `MemshareJsonContext`), no trim or
   AOT warnings; `dotnet publish` of `Memshare.Cli` must succeed without warnings.
2. **Git through `GitRunner` only**: the user's `git` executable, arguments as a list, never through a shell; git
   never prompts.
3. **Reading never writes, and nothing is sent without `publish`**: commands other than `publish` (and `pull`,
   which only fetches) don't commit or push; the session-start hook never fails the session.
4. **No conflict markers are ever committed** to a store ([ADR-0005](docs/adr/0005-merging-without-conflict-markers.md)).
5. **Secret check: specific patterns only** ([ADR-0006](docs/adr/0006-a-conservative-secret-check.md)): each new
   pattern comes with tests of what it must not match.
6. **Cross-platform**: paths through `Path`, no assumptions about separators or line endings; Linux, Windows and
   macOS behave alike.
7. **Tests are part of the change**. Tests never touch the real home directory or the user's git configuration:
   use `Sandbox` (a temporary home, an isolated git) and `StoreRemote` (a bare repository whose clones act as
   machines).
8. **Packages**: public NuGet packages only, versions only in `Directory.Packages.props`; keep dependencies few.
9. **No secrets** in source, tests or docs; test data uses `example.invalid`.
10. **Keep docs current**: the design overview for behaviour, an ADR for each architectural decision.

## Conventions

- .NET 10, latest C#, nullable enabled, `TreatWarningsAsErrors`, `AnalysisMode=Recommended`, `.editorconfig`.
- File-scoped namespaces matching folders (`Memshare.Stores`); one type per file.
- `sealed` by default; `public` in `Memshare.Core` only for what the CLI uses.
- Async with `CancellationToken`; `ConfigureAwait(false)` in `Memshare.Core`, not in tests.
- Users see `MemshareException` messages as is: write them as plain sentences that say what to do.
- Test names are sentences with underscores describing the behaviour.
