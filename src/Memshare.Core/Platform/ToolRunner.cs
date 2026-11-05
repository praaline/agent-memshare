using System.ComponentModel;
using System.Diagnostics;
using System.Text;

using Memshare.Git;

namespace Memshare.Platform;

/// <summary>
/// Runs an external program (git, ssh-keygen, ssh-keyscan) with an argument list, never through a
/// shell, with no input and a time limit.
/// </summary>
public static class ToolRunner
{
    /// <summary>Runs a program and collects its output.</summary>
    /// <exception cref="MemshareException">The program is not installed or cannot be started.</exception>
    public static async Task<GitResult> RunAsync(
        string program,
        string workingDirectory,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string?> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environment);

        var startInfo = new ProcessStartInfo(program)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception e)
        {
            throw new MemshareException($"{program} was not found: install it and make sure it is on the PATH.", e);
        }

        // No input, ever: a program waiting for it would hang.
        process.StandardInput.Close();
        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new GitResult(-1, await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false), TimedOut: true);
        }

        return new GitResult(process.ExitCode, await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false), TimedOut: false);
    }
}
