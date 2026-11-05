namespace Memshare.Platform;

/// <summary>Paths as shown to the user and the agent.</summary>
public static class DisplayPath
{
    /// <summary>
    /// A path with forward slashes on every OS (<c>C:/Users/…</c> on Windows): usable from PowerShell,
    /// cmd and Git Bash alike, and free of escaping problems in Markdown.
    /// </summary>
    public static string Of(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Path.DirectorySeparatorChar == '\\' ? path.Replace('\\', '/') : path;
    }
}
