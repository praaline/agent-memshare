using Memshare.Configuration;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class ConfigFileTests
{
    [Fact]
    public void Without_a_file_the_defaults_apply()
    {
        using var sandbox = new Sandbox();

        var config = ConfigFile.Load(sandbox.Environment());

        Assert.Null(config.Machine);
        Assert.Equal(MemshareConfig.DefaultStoresRoot, config.StoresRoot);
    }

    [Fact]
    public void A_saved_configuration_reads_back()
    {
        using var sandbox = new Sandbox();
        var environment = sandbox.Environment();

        ConfigFile.Save(environment, new MemshareConfig { Machine = "laptop", StoresRoot = "~/notes" });

        Assert.Equal(new MemshareConfig { Machine = "laptop", StoresRoot = "~/notes" }, ConfigFile.Load(environment));
    }

    [Fact]
    public void An_invalid_file_is_reported_with_its_path()
    {
        using var sandbox = new Sandbox();
        var environment = sandbox.Environment();
        Directory.CreateDirectory(environment.ConfigDirectory);
        File.WriteAllText(environment.ConfigFilePath, "{ not json");

        var error = Assert.Throws<MemshareException>(() => ConfigFile.Load(environment));

        Assert.Contains(environment.ConfigFilePath, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_newer_format_is_refused()
    {
        using var sandbox = new Sandbox();
        var environment = sandbox.Environment();
        Directory.CreateDirectory(environment.ConfigDirectory);
        File.WriteAllText(environment.ConfigFilePath, """{ "format": 99 }""");

        var error = Assert.Throws<MemshareException>(() => ConfigFile.Load(environment));

        Assert.Contains("Update memshare", error.Message, StringComparison.Ordinal);
    }
}
