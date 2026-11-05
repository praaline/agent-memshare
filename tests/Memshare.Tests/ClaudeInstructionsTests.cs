using Memshare.Agents.ClaudeCode;

namespace Memshare.Tests;

public sealed class ClaudeInstructionsTests
{
    private static readonly string Block = ClaudeInstructions.Render(["~/agent-memshare/personal/global/CLAUDE.md"], "laptop", "Linux");

    [Fact]
    public void The_block_imports_the_stores_and_names_the_machine()
    {
        Assert.Equal("""
            <!-- memshare:begin -->
            <!-- Managed by memshare: edit the shared instructions in their store, not here. -->
            ## Shared memory (memshare)

            @~/agent-memshare/personal/global/CLAUDE.md

            This machine: `laptop` (Linux).
            <!-- memshare:end -->

            """.ReplaceLineEndings("\n"), Block);
    }

    [Fact]
    public void A_missing_file_becomes_the_block()
    {
        Assert.Equal(Block, ClaudeInstructions.Apply(null, Block));
    }

    [Fact]
    public void The_users_content_is_kept_and_the_block_appended()
    {
        var updated = ClaudeInstructions.Apply("# Mine\n\nKeep this.\n", Block);

        Assert.Equal("# Mine\n\nKeep this.\n\n" + Block, updated);
    }

    [Fact]
    public void An_existing_block_is_replaced_in_place()
    {
        var content = "# Before\n\n" + Block + "\n# After\n";
        var newer = ClaudeInstructions.Render([], "laptop", "Linux");

        var updated = ClaudeInstructions.Apply(content, newer);

        Assert.Equal("# Before\n\n" + newer + "\n# After\n", updated);
        Assert.Equal(newer, ClaudeInstructions.Extract(updated));
    }

    [Fact]
    public void Removing_the_block_restores_the_users_content()
    {
        Assert.Equal("# Mine\n\nKeep this.\n", ClaudeInstructions.Remove(ClaudeInstructions.Apply("# Mine\n\nKeep this.\n", Block)));
        Assert.Equal("# After\n", ClaudeInstructions.Remove(Block + "\n# After\n"));
        Assert.Equal(string.Empty, ClaudeInstructions.Remove(Block));
        Assert.Equal("no block\n", ClaudeInstructions.Remove("no block\n"));
    }
}
