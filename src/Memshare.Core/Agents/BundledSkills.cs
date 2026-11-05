namespace Memshare.Agents;

/// <summary>
/// The skills shipped inside the executable (from the repository's <c>skills/</c>), by name. Installing
/// them from the executable keeps them in step with the tool's version.
/// </summary>
public static class BundledSkills
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Skills = new(Load);

    /// <summary>Each skill's name and the content of its <c>SKILL.md</c>.</summary>
    public static IReadOnlyDictionary<string, string> All => Skills.Value;

    private static SortedDictionary<string, string> Load()
    {
        var assembly = typeof(BundledSkills).Assembly;
        var skills = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            // "skills/<name>/SKILL.md"; the separator is the build machine's.
            var parts = resource.Replace('\\', '/').Split('/');
            if (parts is ["skills", var name, "SKILL.md"])
            {
                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new StreamReader(stream);
                skills[name] = reader.ReadToEnd().ReplaceLineEndings("\n");
            }
        }

        return skills;
    }
}
