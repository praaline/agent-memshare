using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memshare.Agents.ClaudeCode;

/// <summary>
/// memshare's entries in Claude Code's <c>settings.json</c>: the SessionStart hook and the "ask" rule
/// for edits of stores. Everything else in the file is left exactly as it is.
/// </summary>
public static class ClaudeSettings
{
    private const string HookArguments = "hook session-start";

    /// <summary>Reads the settings, or an empty object when there is no file.</summary>
    /// <exception cref="MemshareException">The file is not valid JSON: it is never overwritten then.</exception>
    public static JsonObject Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) as JsonObject ?? throw new MemshareException($"{path} does not contain a JSON object: fix it, then run the command again.");
        }
        catch (JsonException e)
        {
            throw new MemshareException($"{path} is not valid JSON ({e.Message}): fix it, then run the command again.", e);
        }
    }

    /// <summary>Writes the settings, indented, replacing the file in one step.</summary>
    public static void Save(string path, JsonObject settings)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".memshare-tmp";
        File.WriteAllText(temporary, Serialize(settings));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>The settings as text, as they would be written.</summary>
    public static string Serialize(JsonObject settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            settings.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    /// <summary>The command line of the hook for an executable.</summary>
    /// <remarks>
    /// Forward slashes work in every shell Claude Code may use (bash, PowerShell, cmd). A path with
    /// spaces is quoted, which needs bash: such hooks name it as their shell.
    /// </remarks>
    public static string HookCommand(string executablePath)
    {
        ArgumentNullException.ThrowIfNull(executablePath);
        var path = executablePath.Replace('\\', '/');
        return path.Contains(' ', StringComparison.Ordinal) ? $"\"{path}\" {HookArguments}" : $"{path} {HookArguments}";
    }

    /// <summary>Whether a hook command is memshare's session-start hook (whatever the executable's path).</summary>
    public static bool IsMemshareHook(string? command) =>
        command is not null
        && command.EndsWith(HookArguments, StringComparison.Ordinal)
        && command.Contains("memshare", StringComparison.OrdinalIgnoreCase);

    /// <summary>The session-start hook commands of memshare in the settings.</summary>
    public static IReadOnlyList<string> MemshareHooks(JsonObject settings) =>
        SessionStartGroups(settings)
            .SelectMany(Hooks)
            .Select(h => h["command"]?.GetValue<string>())
            .Where(IsMemshareHook)
            .OfType<string>()
            .ToList();

    /// <summary>The SessionStart hook commands, all of them (to spot other tools doing the same job).</summary>
    public static IReadOnlyList<string> SessionStartCommands(JsonObject settings) =>
        SessionStartGroups(settings)
            .SelectMany(Hooks)
            .Select(h => h["command"]?.GetValue<string>())
            .OfType<string>()
            .ToList();

    /// <summary>Sets memshare's hook (replacing any previous one); true if the settings changed.</summary>
    public static bool SetHook(JsonObject settings, string executablePath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var command = HookCommand(executablePath);
        if (MemshareHooks(settings) is [var only] && only == command)
        {
            return false;
        }

        RemoveHook(settings);
        var hooks = settings["hooks"] as JsonObject ?? [];
        settings["hooks"] = hooks;
        var sessionStart = hooks["SessionStart"] as JsonArray ?? [];
        hooks["SessionStart"] = sessionStart;
        var hook = new JsonObject { ["type"] = "command", ["command"] = command, ["timeout"] = 30 };
        if (command.StartsWith('"'))
        {
            hook["shell"] = "bash";
        }

        sessionStart.Add((JsonNode)new JsonObject { ["hooks"] = new JsonArray(hook) });
        return true;
    }

    /// <summary>Removes memshare's hook, and groups or sections left empty by that; true if the settings changed.</summary>
    public static bool RemoveHook(JsonObject settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var changed = false;
        if (settings["hooks"] is not JsonObject hooks || hooks["SessionStart"] is not JsonArray groups)
        {
            return false;
        }

        foreach (var group in groups.OfType<JsonObject>().ToList())
        {
            if (group["hooks"] is not JsonArray list)
            {
                continue;
            }

            foreach (var hook in list.OfType<JsonObject>().Where(h => IsMemshareHook(h["command"]?.GetValue<string>())).ToList())
            {
                list.Remove(hook);
                changed = true;
            }

            if (list.Count == 0)
            {
                groups.Remove(group);
            }
        }

        if (groups.Count == 0)
        {
            hooks.Remove("SessionStart");
        }

        if (hooks.Count == 0)
        {
            settings.Remove("hooks");
        }

        return changed;
    }

    /// <summary>The "ask" permission rules.</summary>
    public static IReadOnlyList<string> AskRules(JsonObject settings) =>
        (settings?["permissions"]?["ask"] as JsonArray)?.Select(r => r?.GetValue<string>()).OfType<string>().ToList() ?? [];

    /// <summary>Adds an "ask" rule if missing; true if the settings changed.</summary>
    public static bool AddAskRule(JsonObject settings, string rule)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (AskRules(settings).Contains(rule))
        {
            return false;
        }

        var permissions = settings["permissions"] as JsonObject ?? [];
        settings["permissions"] = permissions;
        var ask = permissions["ask"] as JsonArray ?? [];
        permissions["ask"] = ask;
        ask.Add((JsonNode?)JsonValue.Create(rule));
        return true;
    }

    /// <summary>Removes an "ask" rule; true if the settings changed.</summary>
    public static bool RemoveAskRule(JsonObject settings, string rule)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings["permissions"] is not JsonObject permissions || permissions["ask"] is not JsonArray ask)
        {
            return false;
        }

        var matches = ask.Where(r => r?.GetValue<string>() == rule).ToList();
        foreach (var match in matches)
        {
            ask.Remove(match);
        }

        if (ask.Count == 0)
        {
            permissions.Remove("ask");
        }

        if (permissions.Count == 0)
        {
            settings.Remove("permissions");
        }

        return matches.Count > 0;
    }

    private static IEnumerable<JsonObject> SessionStartGroups(JsonObject settings) =>
        (settings?["hooks"]?["SessionStart"] as JsonArray)?.OfType<JsonObject>() ?? [];

    private static IEnumerable<JsonObject> Hooks(JsonObject group) =>
        (group["hooks"] as JsonArray)?.OfType<JsonObject>() ?? [];
}
