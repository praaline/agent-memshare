using Memshare.Merge;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class IndexCheckTests
{
    [Fact]
    public void Entries_pointing_to_missing_files_are_found_and_web_links_ignored()
    {
        using var sandbox = new Sandbox();
        var directory = sandbox.Directory("project");
        File.WriteAllText(Path.Combine(directory, "present.md"), "x");
        var index = IndexDocument.Parse("""
            - [Present](present.md) — here
            - [Anchor](present.md#part) — here too
            - [Gone](gone.md) — missing
            - [Web](https://example.invalid/page) — web
            - [Local anchor](#top) — anchor
            """);

        Assert.Equal(["gone.md"], IndexCheck.MissingTargets(index, directory));
    }

    [Fact]
    public void Several_entries_for_one_file_are_duplicates()
    {
        var index = IndexDocument.Parse("- [A](a.md) — one\n- [A](a.md) — two\n- [B](b.md) — b\n");

        Assert.Equal(["a.md"], IndexCheck.DuplicateTargets(index));
    }
}
