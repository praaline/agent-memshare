using System.Text.RegularExpressions;

using Memshare.Stores;

namespace Memshare.Secrets;

/// <summary>
/// The conservative secret check of <c>memshare publish</c> (ADR-0006): only values whose format is
/// specific enough to be a real secret: private key blocks, tokens with a documented prefix and length,
/// and passwords embedded in URLs. Never names (<c>password=</c>), references to variables or
/// placeholders. A line containing <see cref="AllowMarker"/> is skipped.
/// </summary>
public static partial class SecretScanner
{
    /// <summary>Put on a line to publish it even though it looks like a secret.</summary>
    public const string AllowMarker = "memshare:allow-secret";

    private static readonly (string Kind, Regex Pattern)[] Patterns =
    [
        ("private key", PrivateKey()),
        ("GitHub token", GitHubToken()),
        ("GitHub fine-grained token", GitHubFineGrained()),
        ("GitLab token", GitLabToken()),
        ("AWS access key", AwsAccessKey()),
        ("Slack token", SlackToken()),
        ("Slack webhook", SlackWebhook()),
        ("Anthropic API key", AnthropicKey()),
        ("OpenAI API key", OpenAiKey()),
        ("Google API key", GoogleKey()),
        ("Stripe secret key", StripeKey()),
        ("npm token", NpmToken()),
        ("Hugging Face token", HuggingFaceToken()),
        ("JSON Web Token", JsonWebToken()),
    ];

    /// <summary>The added lines that contain something that looks like a secret.</summary>
    public static IReadOnlyList<SecretFinding> Scan(IEnumerable<AddedLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var findings = new List<SecretFinding>();
        foreach (var line in lines)
        {
            if (line.Text.Contains(AllowMarker, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var (kind, pattern) in Patterns)
            {
                foreach (Match match in pattern.Matches(line.Text))
                {
                    findings.Add(new SecretFinding(line.Path, line.LineNumber, kind, Mask(match.Value)));
                }
            }

            foreach (Match match in UrlCredentials().Matches(line.Text))
            {
                var password = match.Groups["password"].Value;
                if (!IsPlaceholder(password))
                {
                    findings.Add(new SecretFinding(line.Path, line.LineNumber, "password in a URL", Mask(password)));
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// Whether a value is a reference or a placeholder rather than a secret: a variable (<c>$X</c>,
    /// <c>${X}</c>, <c>%X%</c>, <c>$env:X</c>, <c>{{x}}</c>), a placeholder (<c>&lt;password&gt;</c>, <c>***</c>,
    /// <c>changeme</c>, <c>example</c>, <c>password</c>), or too short to matter.
    /// </summary>
    internal static bool IsPlaceholder(string value)
    {
        if (value.Length < 6 || value.StartsWith('$') || value.StartsWith('%') || value.StartsWith('<') || value.StartsWith('{') || value.StartsWith('*'))
        {
            return true;
        }

        var lower = value.ToUpperInvariant();
        string[] placeholders = ["PASSWORD", "PASSWD", "SECRET", "CHANGEME", "EXAMPLE", "PLACEHOLDER", "REDACTED", "XXXX", "YOUR", "TOKEN", "DUMMY", "SAMPLE"];
        return placeholders.Any(p => lower.Contains(p, StringComparison.Ordinal));
    }

    private static string Mask(string value) =>
        value.Length <= 8 ? new string('*', value.Length) : value[..6] + new string('*', Math.Min(value.Length - 6, 12));

    [GeneratedRegex(@"-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY(?: BLOCK)?-----", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKey();

    [GeneratedRegex(@"\b(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{36}\b", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubToken();

    [GeneratedRegex(@"\bgithub_pat_[A-Za-z0-9]{22}_[A-Za-z0-9]{59}\b", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubFineGrained();

    [GeneratedRegex(@"\bglpat-[A-Za-z0-9_-]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex GitLabToken();

    [GeneratedRegex(@"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b", RegexOptions.CultureInvariant)]
    private static partial Regex AwsAccessKey();

    [GeneratedRegex(@"\bxox[abprs]-[A-Za-z0-9-]{10,}", RegexOptions.CultureInvariant)]
    private static partial Regex SlackToken();

    [GeneratedRegex(@"https://hooks\.slack\.com/services/T[A-Z0-9]+/B[A-Z0-9]+/[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex SlackWebhook();

    [GeneratedRegex(@"\bsk-ant-[a-z]+\d{2}-[A-Za-z0-9_-]{40,}", RegexOptions.CultureInvariant)]
    private static partial Regex AnthropicKey();

    [GeneratedRegex(@"\bsk-(?:proj-|svcacct-|admin-)?[A-Za-z0-9_-]{16,}T3BlbkFJ[A-Za-z0-9_-]{16,}", RegexOptions.CultureInvariant)]
    private static partial Regex OpenAiKey();

    [GeneratedRegex(@"\bAIza[0-9A-Za-z_-]{35}\b", RegexOptions.CultureInvariant)]
    private static partial Regex GoogleKey();

    [GeneratedRegex(@"\b[sr]k_live_[0-9A-Za-z]{24,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex StripeKey();

    [GeneratedRegex(@"\bnpm_[A-Za-z0-9]{36}\b", RegexOptions.CultureInvariant)]
    private static partial Regex NpmToken();

    [GeneratedRegex(@"\bhf_[A-Za-z]{34}\b", RegexOptions.CultureInvariant)]
    private static partial Regex HuggingFaceToken();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{16,}", RegexOptions.CultureInvariant)]
    private static partial Regex JsonWebToken();

    [GeneratedRegex(@"\b[a-zA-Z][a-zA-Z0-9+.-]*://[^\s/:@]+:(?<password>[^\s/@]+)@[^\s/@]", RegexOptions.CultureInvariant)]
    private static partial Regex UrlCredentials();
}
