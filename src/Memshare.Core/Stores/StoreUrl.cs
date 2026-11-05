using System.Text.RegularExpressions;

namespace Memshare.Stores;

/// <summary>
/// A store's remote URL. SSH remotes (<c>ssh://user@host:port/path</c> or <c>user@host:path</c>) get a
/// deploy key and a pinned host key; other remotes (HTTPS, local paths) use the user's own git setup.
/// </summary>
/// <param name="Url">The URL as given.</param>
/// <param name="IsSsh">Whether git reaches it over SSH.</param>
/// <param name="Host">The SSH host.</param>
/// <param name="Port">The SSH port, when not the default.</param>
/// <param name="RepositoryPath">The repository's path on the host, or the URL's path.</param>
public sealed partial record StoreUrl(string Url, bool IsSsh, string? Host, int? Port, string RepositoryPath)
{
    /// <summary>Parses a remote URL.</summary>
    /// <exception cref="MemshareException">The URL is empty or malformed.</exception>
    public static StoreUrl Parse(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new MemshareException("The store's URL is empty.");
        }

        url = url.Trim();
        if (url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
            {
                throw new MemshareException($"'{url}' is not a valid SSH URL.");
            }

            return new StoreUrl(url, true, uri.Host, uri.IsDefaultPort || uri.Port == 22 ? null : uri.Port, uri.AbsolutePath.TrimStart('/'));
        }

        // scp-like: [user@]host:path, but not a Windows drive (C:\…) or a URL with a scheme.
        if (!url.Contains("://", StringComparison.Ordinal) && ScpLike().Match(url) is { Success: true } scp && scp.Groups["host"].Value.Length > 1)
        {
            return new StoreUrl(url, true, scp.Groups["host"].Value, null, scp.Groups["path"].Value.TrimStart('/'));
        }

        var path = Uri.TryCreate(url, UriKind.Absolute, out var other) && !other.IsFile ? other.AbsolutePath : url;
        return new StoreUrl(url, false, null, null, path);
    }

    /// <summary>The store name a URL suggests: the repository's name without <c>.git</c>.</summary>
    public string DefaultName
    {
        get
        {
            var last = RepositoryPath.TrimEnd('/', '\\').Split('/', '\\').LastOrDefault() ?? string.Empty;
            return last.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? last[..^4] : last;
        }
    }

    /// <summary>The host as written in a <c>known_hosts</c> file.</summary>
    public string KnownHostsName => Port is { } port ? $"[{Host}]:{port}" : Host ?? string.Empty;

    [GeneratedRegex(@"^(?:(?<user>[^@/\s]+)@)?(?<host>[^:/\s]+):(?<path>[^/].*|/.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ScpLike();
}
