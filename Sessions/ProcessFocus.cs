using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Sessions;

/// <summary>
/// Brings the window that hosts a Claude Code session to the front. The session file only knows
/// the CLI's pid, so on macOS we walk up the process tree to the first ancestor that lives inside
/// an app bundle (VS Code, Terminal, iTerm, the Claude desktop app, …) and activate that bundle.
/// Sessions owned by the Claude desktop app use its own "needs input" deep link, which also
/// selects the right tab. Other platforms fall back to a hint on the key.
/// </summary>
internal static class ProcessFocus
{
    private const int MaxDepth = 8;

    /// <summary>Returns true when something was activated.</summary>
    public static bool Focus(IPluginHost host, SessionInfo session)
    {
        if (OperatingSystem.IsMacOS())
        {
            var bundle = FindOwningBundle(session.Pid);
            host.Logger.Info($"Focus {session.Name}: pid {session.Pid} belongs to {bundle ?? "no app bundle"}");
            if (bundle is not null)
            {
                if (bundle.EndsWith("/Claude.app", StringComparison.OrdinalIgnoreCase))
                {
                    return ClaudeApp.OpenLink(host, ClaudeApp.CodeNeedsInputPath) || Activate(bundle);
                }

                return Activate(bundle);
            }

            host.Logger.Info($"No app bundle found above pid {session.Pid}; cannot focus session {session.Name}");
            return false;
        }

        if (session.Entrypoint.Contains("desktop", StringComparison.OrdinalIgnoreCase) && ClaudeApp.IsDesktopInstalled)
        {
            return ClaudeApp.OpenLink(host, ClaudeApp.CodeNeedsInputPath);
        }

        return false;
    }

    private static bool Activate(string bundlePath)
    {
        try
        {
            ClaudeApp.Run("open", bundlePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Path of the first ".app" bundle found walking the parent chain from <paramref name="pid"/>, or null.</summary>
    internal static string? FindOwningBundle(int pid)
    {
        var current = pid;
        for (var depth = 0; depth < MaxDepth && current > 1; depth++)
        {
            var line = ClaudeApp.Capture("ps", "-o", "ppid=,comm=", "-p", current.ToString());
            if (string.IsNullOrWhiteSpace(line)) return null;

            var trimmed = line.Trim();
            var space = trimmed.IndexOf(' ');
            if (space < 0 || !int.TryParse(trimmed[..space], out var parent)) return null;
            var command = trimmed[(space + 1)..].Trim();

            var app = command.IndexOf(".app/Contents/", StringComparison.OrdinalIgnoreCase);
            if (app > 0) return command[..(app + 4)];

            current = parent;
        }

        return null;
    }
}
