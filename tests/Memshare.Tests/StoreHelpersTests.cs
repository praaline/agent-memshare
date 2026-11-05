using Memshare.Stores;

namespace Memshare.Tests;

public sealed class StoreHelpersTests
{
    [Theory]
    [InlineData("README.md", "README.conflict-20261105103000.md")]
    [InlineData("projects/app/plan.md", "projects/app/plan.conflict-20261105103000.md")]
    [InlineData("notes", "notes.conflict-20261105103000")]
    public void A_conflict_copy_is_named_after_the_file_and_the_time(string file, string copy) =>
        Assert.Equal(copy, ConflictCopies.NameFor(file, new DateTimeOffset(2026, 11, 5, 10, 30, 0, TimeSpan.Zero)));

    [Fact]
    public void Added_lines_are_read_with_their_file_and_line_number()
    {
        const string diff = """
            diff --git a/notes.md b/notes.md
            index 1111111..2222222 100644
            --- a/notes.md
            +++ b/notes.md
            @@ -3,0 +4,2 @@ heading
            +first added
            +second added
            diff --git a/gone.md b/gone.md
            deleted file mode 100644
            --- a/gone.md
            +++ /dev/null
            @@ -1 +0,0 @@
            -removed
            diff --git a/new.md b/new.md
            new file mode 100644
            --- /dev/null
            +++ b/new.md
            @@ -0,0 +1 @@
            +brand new
            """;

        var lines = StagedChanges.Parse(diff);

        Assert.Equal(
            [new AddedLine("notes.md", 4, "first added"), new AddedLine("notes.md", 5, "second added"), new AddedLine("new.md", 1, "brand new")],
            lines);
    }

    [Fact]
    public void Only_files_with_opening_and_closing_markers_count_as_conflicted()
    {
        AddedLine[] lines =
        [
            new("heading.md", 1, "Title"),
            new("heading.md", 2, "======="),
            new("conflict.md", 1, "<<<<<<< HEAD"),
            new("conflict.md", 2, "======="),
            new("conflict.md", 3, ">>>>>>> theirs"),
            new("half.md", 1, "<<<<<<< not closed"),
        ];

        Assert.Equal(["conflict.md"], ConflictMarkers.FilesWithMarkers(lines));
    }
}
