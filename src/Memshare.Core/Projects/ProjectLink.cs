using System.Text.RegularExpressions;

using Memshare.Git;

namespace Memshare.Projects;

/// <summary>
/// The link of a project on this machine to its shared notes: a marker file
/// <c>.claude/memshare</c> holding "<c>&lt;store&gt; &lt;slug&gt;</c>" (ADR-0004).
/// </summary>
/// <param name="ProjectRoot">The project's root directory.</param>
/// <param name="StoreName">The store holding the project's shared notes.</param>
/// <param name="Slug">The project's name in the store.</param>
public sealed partial record ProjectLink(string ProjectRoot, string StoreName, string Slug)
{
    /// <summary>The marker's path, relative to the project root.</summary>
    public static readonly string MarkerRelativePath = Path.Combine(".claude", "memshare");

    /// <summary>Whether a slug is valid: lower-case letters, digits and hyphens, starting with a letter or digit.</summary>
    public static bool IsValidSlug(string? slug) => slug is not null && SlugPattern().IsMatch(slug);

    /// <summary>Whether a store name is valid (same rule as slugs, plus dots and underscores).</summary>
    public static bool IsValidStoreName(string? name) => name is not null && StoreNamePattern().IsMatch(name);

    /// <summary>
    /// The root of the project a directory belongs to: the top of its Git working tree, or the
    /// directory itself outside Git.
    /// </summary>
    public static async Task<string> FindProjectRootAsync(GitRunner git, string directory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(directory);
        var result = await git.RunAsync(directory, ["rev-parse", "--show-toplevel"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded && result.Output.Length > 0 ? Path.GetFullPath(result.Output) : Path.GetFullPath(directory);
    }

    /// <summary>
    /// Reads the link of a project. A missing or malformed marker means the project is not linked.
    /// </summary>
    public static ProjectLink? Read(string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);
        var marker = Path.Combine(projectRoot, MarkerRelativePath);
        if (!File.Exists(marker))
        {
            return null;
        }

        var parts = File.ReadAllText(marker).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && IsValidStoreName(parts[0]) && IsValidSlug(parts[1])
            ? new ProjectLink(projectRoot, parts[0], parts[1])
            : null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex StoreNamePattern();
}
