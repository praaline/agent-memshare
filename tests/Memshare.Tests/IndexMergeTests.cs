using Memshare.Merge;

namespace Memshare.Tests;

public sealed class IndexMergeTests
{
    private const string Ancestor = """
        # Index

        ## Build
        - [Build](build.md) — how to build
        - [Tests](tests.md) — how to test

        ## Deploy
        - [Helm](helm.md) — the chart

        """;

    [Fact]
    public void Entries_added_on_both_sides_are_both_kept_where_they_were_added()
    {
        var ours = Ancestor.Replace("- [Tests](tests.md) — how to test\n", "- [Tests](tests.md) — how to test\n- [Lint](lint.md) — formatting\n", StringComparison.Ordinal);
        var theirs = Ancestor.Replace("- [Helm](helm.md) — the chart\n", "- [Helm](helm.md) — the chart\n- [Flyway](flyway.md) — migrations\n", StringComparison.Ordinal);

        var merged = IndexMerge.Merge(Ancestor, ours, theirs);

        Assert.Equal("""
            # Index

            ## Build
            - [Build](build.md) — how to build
            - [Tests](tests.md) — how to test
            - [Lint](lint.md) — formatting

            ## Deploy
            - [Helm](helm.md) — the chart
            - [Flyway](flyway.md) — migrations

            """, merged);
    }

    [Fact]
    public void A_changed_description_and_an_added_entry_merge_into_one_entry_each()
    {
        var ours = Ancestor.Replace("— how to build", "— how to build (Release)", StringComparison.Ordinal);
        var theirs = Ancestor.Replace("- [Tests](tests.md) — how to test\n", "- [Tests](tests.md) — how to test\n- [Lint](lint.md) — formatting\n", StringComparison.Ordinal);

        var merged = IndexMerge.Merge(Ancestor, ours, theirs);

        Assert.Contains("- [Build](build.md) — how to build (Release)\n", merged, StringComparison.Ordinal);
        Assert.DoesNotContain("— how to build\n", merged, StringComparison.Ordinal);
        Assert.Contains("- [Lint](lint.md) — formatting\n", merged, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_removed_on_one_side_and_unchanged_on_the_other_is_removed()
    {
        var ours = Ancestor.Replace("- [Tests](tests.md) — how to test\n", string.Empty, StringComparison.Ordinal);

        var merged = IndexMerge.Merge(Ancestor, ours, Ancestor);

        Assert.DoesNotContain("tests.md", merged, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_removed_on_one_side_but_changed_on_the_other_keeps_the_change()
    {
        var ours = Ancestor.Replace("- [Tests](tests.md) — how to test\n", string.Empty, StringComparison.Ordinal);
        var theirs = Ancestor.Replace("— how to test", "— how to test, with coverage", StringComparison.Ordinal);

        var merged = IndexMerge.Merge(Ancestor, ours, theirs);

        Assert.Contains("- [Tests](tests.md) — how to test, with coverage\n", merged, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_changed_differently_on_both_sides_keeps_both_versions()
    {
        var ours = Ancestor.Replace("— the chart", "— the chart, v2", StringComparison.Ordinal);
        var theirs = Ancestor.Replace("— the chart", "— the chart and its values", StringComparison.Ordinal);

        var merged = IndexMerge.Merge(Ancestor, ours, theirs);

        Assert.Contains("- [Helm](helm.md) — the chart, v2\n- [Helm](helm.md) — the chart and its values\n", merged, StringComparison.Ordinal);
        Assert.Equal(["helm.md"], IndexCheck.DuplicateTargets(IndexDocument.Parse(merged)));
    }

    [Fact]
    public void A_section_added_on_both_sides_under_the_same_heading_is_merged()
    {
        var ours = Ancestor + "## Notes\n- [A](a.md) — a\n";
        var theirs = Ancestor + "## Notes\n- [B](b.md) — b\n";

        var merged = IndexMerge.Merge(Ancestor, ours, theirs);

        Assert.EndsWith("## Notes\n- [A](a.md) — a\n- [B](b.md) — b\n", merged, StringComparison.Ordinal);
        Assert.Single(IndexDocument.Parse(merged).Sections, s => s.Heading == "## Notes");
    }

    [Fact]
    public void A_section_removed_on_one_side_goes_unless_the_other_changed_it()
    {
        var withoutDeploy = Ancestor[..Ancestor.IndexOf("## Deploy", StringComparison.Ordinal)];
        var changedDeploy = Ancestor.Replace("— the chart", "— the chart, v2", StringComparison.Ordinal);

        Assert.DoesNotContain("## Deploy", IndexMerge.Merge(Ancestor, withoutDeploy, Ancestor), StringComparison.Ordinal);
        Assert.Contains("— the chart, v2", IndexMerge.Merge(Ancestor, withoutDeploy, changedDeploy), StringComparison.Ordinal);
    }

    [Fact]
    public void A_flat_index_and_windows_line_endings_are_handled()
    {
        const string ancestor = "- [A](a.md) — a\n";
        var ours = "- [A](a.md) — a\r\n- [B](b.md) — b\r\n";
        var theirs = "- [A](a.md) — a\n- [C](c.md) — c\n";

        Assert.Equal("- [A](a.md) — a\n- [B](b.md) — b\n- [C](c.md) — c\n", IndexMerge.Merge(ancestor, ours, theirs));
    }

    [Fact]
    public void Identical_changes_on_both_sides_appear_once()
    {
        var both = Ancestor.Replace("— the chart", "— the chart, v2", StringComparison.Ordinal);

        Assert.Equal(both, IndexMerge.Merge(Ancestor, both, both));
    }

    [Fact]
    public void Rendering_a_parsed_index_gives_it_back()
    {
        Assert.Equal(Ancestor, IndexDocument.Parse(Ancestor).Render());
    }
}
