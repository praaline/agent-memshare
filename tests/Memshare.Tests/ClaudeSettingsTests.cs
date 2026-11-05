using System.Text.Json.Nodes;

using Memshare.Agents.ClaudeCode;

namespace Memshare.Tests;

public sealed class ClaudeSettingsTests
{
    private const string Existing = """
        {
          "theme": "dark",
          "hooks": {
            "SessionStart": [
              { "hooks": [ { "type": "command", "command": "other-tool start" } ] }
            ],
            "Stop": [ { "hooks": [ { "type": "command", "command": "notify" } ] } ]
          },
          "permissions": { "allow": [ "Bash(ls:*)" ] }
        }
        """;

    [Fact]
    public void The_hook_and_rule_are_added_and_everything_else_is_kept()
    {
        var settings = (JsonObject)JsonNode.Parse(Existing)!;

        Assert.True(ClaudeSettings.SetHook(settings, "/home/me/.local/bin/memshare"));
        Assert.True(ClaudeSettings.AddAskRule(settings, "Edit(~/agent-memshare/**)"));

        Assert.Equal("dark", settings["theme"]!.GetValue<string>());
        Assert.Equal(["other-tool start", "/home/me/.local/bin/memshare hook session-start"], ClaudeSettings.SessionStartCommands(settings));
        Assert.Equal("notify", settings["hooks"]!["Stop"]![0]!["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.Equal("Bash(ls:*)", settings["permissions"]!["allow"]![0]!.GetValue<string>());
        Assert.Equal(["Edit(~/agent-memshare/**)"], ClaudeSettings.AskRules(settings));
    }

    [Fact]
    public void Setting_again_changes_nothing_and_a_new_path_replaces_the_old_one()
    {
        var settings = new JsonObject();
        ClaudeSettings.SetHook(settings, "/old/memshare");
        ClaudeSettings.AddAskRule(settings, "Edit(~/agent-memshare/**)");

        Assert.False(ClaudeSettings.SetHook(settings, "/old/memshare"));
        Assert.False(ClaudeSettings.AddAskRule(settings, "Edit(~/agent-memshare/**)"));
        Assert.True(ClaudeSettings.SetHook(settings, "/new/memshare"));
        Assert.Equal(["/new/memshare hook session-start"], ClaudeSettings.MemshareHooks(settings));
    }

    [Fact]
    public void Removing_leaves_other_hooks_and_drops_what_became_empty()
    {
        var settings = (JsonObject)JsonNode.Parse(Existing)!;
        ClaudeSettings.SetHook(settings, "/home/me/.local/bin/memshare");
        ClaudeSettings.AddAskRule(settings, "Edit(~/agent-memshare/**)");

        Assert.True(ClaudeSettings.RemoveHook(settings));
        Assert.True(ClaudeSettings.RemoveAskRule(settings, "Edit(~/agent-memshare/**)"));

        Assert.Equal(["other-tool start"], ClaudeSettings.SessionStartCommands(settings));
        Assert.Null(settings["permissions"]!["ask"]);
        Assert.False(ClaudeSettings.RemoveHook(settings));
    }

    [Fact]
    public void A_hook_of_another_tool_is_not_taken_for_memshares()
    {
        Assert.False(ClaudeSettings.IsMemshareHook("\"$HOME/.local/bin/other-sync\" hook session-start"));
        Assert.True(ClaudeSettings.IsMemshareHook("C:/Users/me/AppData/Local/Programs/memshare/memshare.exe hook session-start"));
    }

    [Fact]
    public void A_path_with_spaces_is_quoted_and_runs_in_bash()
    {
        var settings = new JsonObject();

        ClaudeSettings.SetHook(settings, @"C:\Users\Jane Doe\AppData\Local\Programs\memshare\memshare.exe");

        var hook = settings["hooks"]!["SessionStart"]![0]!["hooks"]![0]!;
        Assert.Equal("\"C:/Users/Jane Doe/AppData/Local/Programs/memshare/memshare.exe\" hook session-start", hook["command"]!.GetValue<string>());
        Assert.Equal("bash", hook["shell"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(@"C:\Users\me\notes", "/c/Users/me/notes")]
    [InlineData("/srv/notes", "/srv/notes")]
    public void Absolute_paths_take_the_posix_form_of_permission_rules(string path, string posix) =>
        Assert.Equal(posix, ClaudeCodeIntegration.PosixAbsolute(path));
}
