namespace Memshare.Stores;

/// <summary>The files of a new store (ADR-0004), relative to its root.</summary>
public static class StoreTemplate
{
    /// <summary>The template's files and their content.</summary>
    public static IReadOnlyDictionary<string, string> Files { get; } = new Dictionary<string, string>
    {
        [StoreManifest.FileName] = StoreManifest.Serialize(new StoreManifest()),
        [".gitattributes"] = """
            # LF everywhere: the same files are read on Linux, macOS and Windows.
            * text=auto eol=lf
            # Indexes changed on several machines merge entry by entry (memshare's merge driver).
            **/MEMORY.md merge=memshare-index

            """,
        ["README.md"] = """
            # Shared memory

            A memshare store: knowledge for coding agents, shared between my machines.

            | Path | Content |
            |---|---|
            | `global/CLAUDE.md` | instructions for every session, on every machine |
            | `global/…` | other shared notes, referenced from `global/CLAUDE.md` |
            | `projects/<slug>/` | one project's shared notes: `MEMORY.md` (index), topic files, `handoff.md` |

            Rules: no secrets, ever. A project's notes are about that project only.

            """,
        ["global/CLAUDE.md"] = """
            # Shared instructions

            Instructions for every session, on every machine. Keep them short and general; project
            knowledge belongs in `projects/<slug>/`.

            - Shared memory is written only when I ask to save something to it; nothing is published
              until I ask (`memshare publish`).
            - Never write secrets (passwords, tokens, keys) to shared memory.

            """,
        ["projects/.gitkeep"] = string.Empty,
    };
}
