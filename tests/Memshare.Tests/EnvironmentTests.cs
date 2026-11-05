using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class EnvironmentTests
{
    [Theory]
    [InlineData("~", "")]
    [InlineData("~/agent-memshare", "agent-memshare")]
    [InlineData("~/a/b", "a/b")]
    public void A_leading_tilde_is_the_home_directory(string path, string underHome)
    {
        using var sandbox = new Sandbox();

        var expanded = sandbox.Environment().ExpandPath(path);

        Assert.Equal(Path.GetFullPath(Path.Combine(sandbox.Home, underHome)), expanded);
    }

    [Fact]
    public void A_relative_path_is_relative_to_the_current_directory()
    {
        using var sandbox = new Sandbox();
        var current = sandbox.Directory("work");

        Assert.Equal(Path.Combine(current, "x"), sandbox.Environment(current).ExpandPath("x"));
    }
}
