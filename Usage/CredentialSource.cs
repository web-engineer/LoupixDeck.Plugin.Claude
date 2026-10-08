using System.Text.Json;
using LoupixDeck.Plugin.Claude.Platform;

namespace LoupixDeck.Plugin.Claude.Usage;

/// <summary>
/// Finds the Claude Code OAuth access token that the usage endpoint accepts. Order: macOS Keychain
/// (where Claude Code keeps it on a Mac), then the plain credentials file Claude Code writes on
/// Linux and Windows, then a token the user pasted into the plugin settings. The token is only
/// ever held in memory and never logged.
/// </summary>
internal static class CredentialSource
{
    public const string KeychainService = "Claude Code-credentials";

    public static string CredentialsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");

    /// <summary>Returns the token and a short description of where it came from, or null and the reason.</summary>
    public static (string? Token, string Source) Resolve(string? manualToken)
    {
        if (OperatingSystem.IsMacOS())
        {
            var json = ClaudeApp.Capture("security", "find-generic-password", "-s", KeychainService, "-w");
            if (ExtractToken(json) is { } fromKeychain) return (fromKeychain, "macOS Keychain");
        }

        try
        {
            if (File.Exists(CredentialsFile) && ExtractToken(File.ReadAllText(CredentialsFile)) is { } fromFile)
            {
                return (fromFile, "~/.claude/.credentials.json");
            }
        }
        catch
        {
            // unreadable file: fall through
        }

        if (!string.IsNullOrWhiteSpace(manualToken)) return (manualToken.Trim(), "plugin settings");

        return (null, OperatingSystem.IsMacOS()
            ? "No Claude Code sign-in found in the Keychain or ~/.claude/.credentials.json. Run `claude` once and sign in, or paste a token in the plugin settings."
            : "No Claude Code sign-in found at ~/.claude/.credentials.json. Run `claude` once and sign in, or paste a token in the plugin settings.");
    }

    /// <summary>Accepts the credential JSON (<c>{"claudeAiOauth":{"accessToken":…}}</c>) or a bare token.</summary>
    internal static string? ExtractToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        if (!text.StartsWith('{')) return text.StartsWith("sk-ant-", StringComparison.Ordinal) ? text : null;

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth)
                && oauth.TryGetProperty("accessToken", out var token)
                && token.ValueKind == JsonValueKind.String)
            {
                var value = token.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
