using System.Text;

using Memshare.Git;
using Memshare.Stores;

namespace Memshare.Projects;

/// <summary>A slug for a project that has none yet: its Git remote's repository name, or its directory's name.</summary>
public static class SlugSuggestion
{
    /// <summary>Suggests a slug for the project at a root directory.</summary>
    public static async Task<string?> SuggestAsync(GitRunner git, string projectRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(projectRoot);
        var origin = await git.RunAsync(projectRoot, ["remote", "get-url", "origin"], GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false);
        var name = origin.Succeeded && origin.Output.Length > 0 ? StoreUrl.Parse(origin.Output).DefaultName : Path.GetFileName(projectRoot.TrimEnd('/', '\\'));
        var slug = Slugify(name);
        return ProjectLink.IsValidSlug(slug) ? slug : null;
    }

    /// <summary>Lower case, letters and digits kept, every other run of characters becomes one hyphen.</summary>
    public static string Slugify(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var slug = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var result = slug.ToString().Trim('-');
        return result.Length > 63 ? result[..63].TrimEnd('-') : result;
    }
}
