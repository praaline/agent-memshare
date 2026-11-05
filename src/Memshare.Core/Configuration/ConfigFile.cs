using System.Text.Json;

using Memshare.Platform;

namespace Memshare.Configuration;

/// <summary>Reads and writes the machine's configuration file.</summary>
public static class ConfigFile
{
    /// <summary>The highest configuration format this version understands.</summary>
    public const int SupportedFormat = 1;

    /// <summary>
    /// Reads the configuration, or returns the defaults when there is no file yet.
    /// </summary>
    /// <exception cref="MemshareException">The file is not valid, or has a newer format.</exception>
    public static MemshareConfig Load(MemshareEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var path = environment.ConfigFilePath;
        if (!File.Exists(path))
        {
            return new MemshareConfig();
        }

        MemshareConfig? config;
        try
        {
            config = JsonSerializer.Deserialize(File.ReadAllText(path), MemshareJsonContext.Default.MemshareConfig);
        }
        catch (JsonException e)
        {
            throw new MemshareException($"{path} is not valid: {e.Message}", e);
        }

        if (config is null)
        {
            throw new MemshareException($"{path} is empty.");
        }

        if (config.Format > SupportedFormat)
        {
            throw new MemshareException(
                $"{path} has format {config.Format}; this version of memshare understands up to {SupportedFormat}. Update memshare.");
        }

        return config;
    }

    /// <summary>Writes the configuration, replacing the file in one step.</summary>
    public static void Save(MemshareEnvironment environment, MemshareConfig config)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(config);
        Directory.CreateDirectory(environment.ConfigDirectory);
        var path = environment.ConfigFilePath;
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(config, MemshareJsonContext.Default.MemshareConfig) + "\n");
        File.Move(temporary, path, overwrite: true);
    }
}
