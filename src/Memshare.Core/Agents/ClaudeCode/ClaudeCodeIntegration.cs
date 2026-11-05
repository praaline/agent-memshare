using System.Globalization;

using Memshare.Diagnostics;
using Memshare.Platform;
using Memshare.Stores;

namespace Memshare.Agents.ClaudeCode;

/// <summary>memshare in Claude Code (ADR-0007).</summary>
public sealed class ClaudeCodeIntegration : IAgentIntegration
{
    public string Name => "Claude Code";

    public bool IsPresent(InstallContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Directory.Exists(context.Environment.ClaudeDirectory);
    }

    /// <summary>The "ask" rule covering every store: Claude Code asks before any edit under the stores root.</summary>
    public static string AskRule(MemshareEnvironment environment, string storesRoot)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var path = environment.HomeRelative(storesRoot) ?? "/" + PosixAbsolute(storesRoot);
        return $"Edit({path}/**)";
    }

    /// <summary>
    /// An absolute path in the POSIX form Claude Code's permission rules use: <c>C:\Users\a</c> becomes
    /// <c>/c/Users/a</c>.
    /// </summary>
    public static string PosixAbsolute(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            return "/" + char.ToLowerInvariant(path[0]) + path[2..].Replace('\\', '/');
        }

        return path.Replace('\\', '/');
    }

    /// <summary>How Claude Code imports a store's global instructions.</summary>
    public static IReadOnlyList<string> Imports(InstallContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Stores.Stores
            .Select(s => Path.Combine(s.Directory, "global", "CLAUDE.md"))
            .Where(File.Exists)
            .Select(p => context.Environment.HomeRelative(p) ?? p.Replace('\\', '/'))
            .ToList();
    }

    public IReadOnlyList<string> Apply(InstallContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var changes = new List<string>();
        var settingsPath = SettingsPath(context);
        var settings = ClaudeSettings.Load(settingsPath);
        var before = ClaudeSettings.Serialize(settings);
        if (ClaudeSettings.SetHook(settings, context.ExecutablePath))
        {
            changes.Add($"session-start hook in {DisplayPath.Of(settingsPath)}");
        }

        if (ClaudeSettings.AddAskRule(settings, AskRule(context.Environment, StoresRoot(context))))
        {
            changes.Add($"ask rule for edits of stores in {DisplayPath.Of(settingsPath)}");
        }

        if (ClaudeSettings.Serialize(settings) != before)
        {
            Backup(context, settingsPath, changes);
            ClaudeSettings.Save(settingsPath, settings);
        }

        if (WriteInstructions(context))
        {
            changes.Add($"shared instructions block in {DisplayPath.Of(InstructionsPath(context))}");
        }

        foreach (var (name, content) in BundledSkills.All)
        {
            var path = SkillPath(context, name);
            if (!File.Exists(path) || File.ReadAllText(path) != content)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content);
                changes.Add($"skill {name} in {DisplayPath.Of(Path.GetDirectoryName(path)!)}");
            }
        }

        return changes;
    }

    public IReadOnlyList<string> Remove(InstallContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var changes = new List<string>();
        var settingsPath = SettingsPath(context);
        if (File.Exists(settingsPath))
        {
            var settings = ClaudeSettings.Load(settingsPath);
            var hook = ClaudeSettings.RemoveHook(settings);
            var rule = ClaudeSettings.RemoveAskRule(settings, AskRule(context.Environment, StoresRoot(context)));
            if (hook || rule)
            {
                Backup(context, settingsPath, changes);
                ClaudeSettings.Save(settingsPath, settings);
                changes.Add($"removed the hook and the ask rule from {DisplayPath.Of(settingsPath)}");
            }
        }

        var instructionsPath = InstructionsPath(context);
        if (File.Exists(instructionsPath))
        {
            var content = File.ReadAllText(instructionsPath);
            var without = ClaudeInstructions.Remove(content);
            if (without != content)
            {
                File.WriteAllText(instructionsPath, without);
                changes.Add($"removed the shared instructions block from {DisplayPath.Of(instructionsPath)}");
            }
        }

        foreach (var name in BundledSkills.All.Keys)
        {
            var directory = Path.GetDirectoryName(SkillPath(context, name))!;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
                changes.Add($"removed the skill {name}");
            }
        }

        return changes;
    }

    public void RefreshInstructions(InstallContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = InstructionsPath(context);
        if (File.Exists(path) && ClaudeInstructions.Extract(File.ReadAllText(path)) is not null)
        {
            WriteInstructions(context);
        }
    }

    public IEnumerable<Finding> Check(InstallContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        const string area = "Claude Code";
        if (!IsPresent(context))
        {
            yield return Finding.Warning(area, $"{DisplayPath.Of(context.Environment.ClaudeDirectory)} not found: is Claude Code installed for this user?");
            yield break;
        }

        var settingsPath = SettingsPath(context);
        System.Text.Json.Nodes.JsonObject settings;
        string? error = null;
        try
        {
            settings = ClaudeSettings.Load(settingsPath);
        }
        catch (MemshareException e)
        {
            settings = [];
            error = e.Message;
        }

        if (error is not null)
        {
            yield return Finding.Problem(area, error);
            yield break;
        }

        var hooks = ClaudeSettings.MemshareHooks(settings);
        var expected = ClaudeSettings.HookCommand(context.ExecutablePath);
        yield return hooks switch
        {
            [] => Finding.Problem(area, "no session-start hook: run `memshare install`."),
            [var only] when only == expected => Finding.Ok(area, $"session-start hook runs {DisplayPath.Of(context.ExecutablePath)}"),
            [var only] => Finding.Problem(area, $"the session-start hook runs `{only}`, not the installed executable: run `memshare install`."),
            _ => Finding.Problem(area, "several memshare session-start hooks: run `memshare install` to keep one."),
        };
        if (!File.Exists(context.ExecutablePath))
        {
            yield return Finding.Problem(area, $"the hook's executable {DisplayPath.Of(context.ExecutablePath)} does not exist: run `memshare install`.");
        }

        var rule = AskRule(context.Environment, StoresRoot(context));
        yield return ClaudeSettings.AskRules(settings).Contains(rule)
            ? Finding.Ok(area, $"edits of stores need approval ({rule})")
            : Finding.Problem(area, $"no ask rule {rule}: edits of stores would not need approval. Run `memshare install`.");

        var instructionsPath = InstructionsPath(context);
        var current = File.Exists(instructionsPath) ? ClaudeInstructions.Extract(File.ReadAllText(instructionsPath)) : null;
        yield return current switch
        {
            null => Finding.Problem(area, $"no shared instructions block in {DisplayPath.Of(instructionsPath)}: run `memshare install`."),
            _ when current == RenderBlock(context) => Finding.Ok(area, $"shared instructions imported ({Imports(context).Count} store(s))"),
            _ => Finding.Warning(area, $"the shared instructions block in {DisplayPath.Of(instructionsPath)} is out of date: run `memshare install`."),
        };

        foreach (var (name, content) in BundledSkills.All)
        {
            var path = SkillPath(context, name);
            yield return !File.Exists(path)
                ? Finding.Problem(area, $"skill {name} is not installed: run `memshare install`.")
                : File.ReadAllText(path) == content
                    ? Finding.Ok(area, $"skill {name}")
                    : Finding.Warning(area, $"skill {name} differs from this version's: run `memshare install` (changes made there are replaced).");
        }
    }

    private static string StoresRoot(InstallContext context) => context.Environment.ExpandPath(context.Config.StoresRoot);

    private static string SettingsPath(InstallContext context) => Path.Combine(context.Environment.ClaudeDirectory, "settings.json");

    private static string SkillPath(InstallContext context, string name) =>
        Path.Combine(context.Environment.ClaudeDirectory, "skills", name, "SKILL.md");

    private static string InstructionsPath(InstallContext context) => Path.Combine(context.Environment.ClaudeDirectory, "CLAUDE.md");

    private static string RenderBlock(InstallContext context) =>
        ClaudeInstructions.Render(Imports(context), context.Machine, MemshareEnvironment.OperatingSystemDescription);

    private static bool WriteInstructions(InstallContext context)
    {
        var path = InstructionsPath(context);
        var content = File.Exists(path) ? File.ReadAllText(path) : null;
        var updated = ClaudeInstructions.Apply(content, RenderBlock(context));
        if (updated == content)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, updated);
        return true;
    }

    private static void Backup(InstallContext context, string path, List<string> changes)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var backup = $"{path}.bak-memshare-{context.Time.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";
        if (!File.Exists(backup))
        {
            File.Copy(path, backup);
            changes.Add($"backup {DisplayPath.Of(backup)}");
        }
    }
}
