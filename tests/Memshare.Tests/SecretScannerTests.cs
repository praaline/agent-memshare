using Memshare.Secrets;
using Memshare.Stores;
using Memshare.Tests.Support;

namespace Memshare.Tests;

/// <summary>
/// Secret-looking values are built at run time, so that this file itself contains nothing a scanner
/// would flag.
/// </summary>
public sealed class SecretScannerTests
{
    public static TheoryData<string, string> Secrets => new()
    {
        { "-----BEGIN OPENSSH " + "PRIVATE KEY-----", "private key" },
        { "-----BEGIN " + "PRIVATE KEY-----", "private key" },
        { "token: " + "ghp_" + new string('a', 36), "GitHub token" },
        { "github_pat_" + new string('B', 22) + "_" + new string('c', 59), "GitHub fine-grained token" },
        { "GITLAB=" + "glpat-" + new string('x', 20), "GitLab token" },
        { "key " + "AKIA" + "ABCDEFGHIJKLMNOP", "AWS access key" },
        { "xoxb-" + "1234567890-abcdef", "Slack token" },
        { "sk-ant-" + "api03-" + new string('k', 90), "Anthropic API key" },
        { "sk-proj-" + new string('a', 20) + "T3Blbk" + "FJ" + new string('b', 20), "OpenAI API key" },
        { "AIza" + new string('d', 35), "Google API key" },
        { "eyJ" + "hbGciOiJIUzI1NiJ9" + ".eyJ" + "zdWIiOiIxMjM0NTY3ODkwIn0" + "." + "abcdefghijklmnopqrstuvwxyz", "JSON Web Token" },
        { "postgres://app:" + "s3cr3tPassw0rdValue" + "@db.example.invalid/app", "password in a URL" },
    };

    [Theory]
    [MemberData(nameof(Secrets))]
    public void Values_with_a_specific_format_are_found(string line, string kind)
    {
        var finding = Assert.Single(SecretScanner.Scan([new AddedLine("notes.md", 3, line)]));

        Assert.Equal(("notes.md", 3, kind), (finding.Path, finding.LineNumber, finding.Kind));
        Assert.Contains('*', finding.Masked);
    }

    [Theory]
    [InlineData("password: ${DB_PASSWORD}")]
    [InlineData("PGPASSWORD=app")]
    [InlineData("export GITHUB_TOKEN=$GITHUB_TOKEN")]
    [InlineData("$env:API_KEY = Read-Host")]
    [InlineData("The password is in the vault (vault_db_password).")]
    [InlineData("connection: postgres://user:${PASSWORD}@db/app")]
    [InlineData("https://user:changeme@example.invalid")]
    [InlineData("https://user:<password>@example.invalid")]
    [InlineData("https://user:%TOKEN%@example.invalid")]
    [InlineData("ssh://gitea@git.example.invalid:2222/me/notes.git")]
    [InlineData("Host=postgres;Port=5432;Database=app;Username=app;Password=app")]
    [InlineData("api_key: <your key>")]
    [InlineData("ghp_short")]
    [InlineData("AKIAEXAMPLE")]
    [InlineData("-----BEGIN PUBLIC KEY-----")]
    [InlineData("Use `memshare publish` to share.")]
    public void Names_references_and_placeholders_are_not_secrets(string line)
    {
        Assert.Empty(SecretScanner.Scan([new AddedLine("notes.md", 1, line)]));
    }

    [Fact]
    public void A_line_marked_as_allowed_is_skipped()
    {
        var line = "example: " + "ghp_" + new string('a', 36) + " <!-- " + SecretScanner.AllowMarker + " -->";

        Assert.Empty(SecretScanner.Scan([new AddedLine("notes.md", 1, line)]));
    }

    [Fact]
    public async Task Publishing_a_secret_is_refused_unless_allowed()
    {
        using var sandbox = new Sandbox();
        var laptop = await Machine.CloneAsync(sandbox, await StoreRemote.CreateAsync(sandbox), "laptop");
        laptop.Write("notes.md", "# Notes\n\nOur key: " + "AKIA" + "ABCDEFGHIJKLMNOP" + "\n");

        var error = await Assert.ThrowsAsync<MemshareException>(() => laptop.PublishAsync());
        Assert.Contains("notes.md:3: AWS access key (AKIAAB", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ABCDEFGHIJKLMNOP", error.Message, StringComparison.Ordinal);

        var result = await laptop.PublishAsync(allowSecrets: true);
        Assert.True(result.Pushed);
    }
}
