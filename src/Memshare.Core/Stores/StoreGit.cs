using Memshare.Git;

namespace Memshare.Stores;

/// <summary>
/// git as memshare runs it on a store: commits under one neutral author and committer, so that they
/// don't reveal which machine made them (ADR-0004), and no editor is ever opened.
/// </summary>
public static class StoreGit
{
    /// <summary>The name of the neutral author.</summary>
    public const string AuthorName = "memshare";

    /// <summary>The e-mail address of the neutral author.</summary>
    public const string AuthorEmail = "memshare@localhost";

    /// <summary>A runner configured for writing to stores.</summary>
    public static GitRunner For(GitRunner git)
    {
        ArgumentNullException.ThrowIfNull(git);
        return git.WithEnvironment(new Dictionary<string, string?>
        {
            ["GIT_AUTHOR_NAME"] = AuthorName,
            ["GIT_AUTHOR_EMAIL"] = AuthorEmail,
            ["GIT_COMMITTER_NAME"] = AuthorName,
            ["GIT_COMMITTER_EMAIL"] = AuthorEmail,
            ["GIT_EDITOR"] = "true",
            ["GIT_SEQUENCE_EDITOR"] = "true",
        });
    }
}
