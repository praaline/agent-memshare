using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class GitRunnerTests
{
    [Fact]
    public async Task A_successful_command_returns_its_output()
    {
        using var sandbox = new Sandbox();

        var result = await sandbox.Git.RunAsync(sandbox.Root, "--version");

        Assert.True(result.Succeeded);
        Assert.StartsWith("git version", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_command_reports_its_exit_code_and_error()
    {
        using var sandbox = new Sandbox();

        var result = await sandbox.Git.RunAsync(sandbox.Root, "rev-parse", "--show-toplevel");

        Assert.False(result.Succeeded);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("not a git repository", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_isolated_configuration_is_used()
    {
        using var sandbox = new Sandbox();

        var result = await sandbox.Git.RunAsync(sandbox.Root, "config", "user.email");

        Assert.Equal("test@example.invalid", result.Output);
    }
}
