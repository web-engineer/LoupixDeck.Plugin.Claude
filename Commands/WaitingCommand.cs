using LoupixDeck.Plugin.Claude.Rendering;
using LoupixDeck.Plugin.Claude.Sessions;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// The "Claude wants you" key. Drawn in the Claude look: an hourglass while sessions work, Zzz when
/// idle, and inverted (orange on white) with the project name when a session is waiting. The
/// button state still follows the live sessions (idle / busy / waiting) for anything keyed to it.
/// Tap to bring that session's window to the front; with several waiting, each tap moves on.
/// </summary>
internal sealed class WaitingCommand(ClaudePlugin plugin) : ClaudeCommandBase(plugin), IDisplayImageCommand
{
    public const string Name = "Claude.Waiting";
    public const string IdleState = "idle";
    public const string BusyState = "busy";
    public const string WaitingState = "waiting";

    private int _cycle;

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "Waiting for input",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F009F", // mdi-bell-ring-outline
        Description = "Lights up when a Claude Code session is waiting for you (a permission or a question). Tap to jump to it.",
        States =
        [
            new ButtonStateDescriptor { Name = WaitingState, Description = "A session is waiting for your input" },
            new ButtonStateDescriptor { Name = BusyState, Description = "Sessions are working, nothing to do" },
            new ButtonStateDescriptor { Name = IdleState, Description = "No session needs you" }
        ],
        ButtonLayout = new ButtonLayoutDescriptor { Mode = ButtonLayoutMode.None }
    };

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    /// <summary>Keeps the demand gate alive; real updates arrive by push from the session monitor.</summary>
    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(5);

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        if (!Plugin.WaitingEnabled)
        {
            ctx.Host.SetActiveButtonState(Name, IdleState);
            ClaudeKey.DrawIcon(canvas, "bell-off-outline", ClaudeKey.Faded);
            return true;
        }

        Plugin.SessionsGate.Touch();
        var sessions = Plugin.Sessions.Sessions;
        var waiting = sessions.Where(s => s.IsWaiting).ToList();
        if (waiting.Count > 0)
        {
            ctx.Host.SetActiveButtonState(Name, WaitingState);
            DrawWaiting(canvas, waiting[0].ProjectName, waiting.Count);
            return true;
        }

        var busy = sessions.Any(s => s.Status == SessionStatus.Busy);
        ctx.Host.SetActiveButtonState(Name, busy ? BusyState : IdleState);
        if (busy) ClaudeKey.DrawIcon(canvas, "timer-sand", ClaudeKey.White);
        else ClaudeKey.DrawIcon(canvas, "sleep", sessions.Count == 0 ? ClaudeKey.Faded : ClaudeKey.White);
        return true;
    }

    /// <summary>Inverted so it stands out from the other Claude keys: orange bell and project name on white.</summary>
    private static void DrawWaiting(IRenderCanvas canvas, string project, int count)
    {
        canvas.Clear(ClaudeKey.White);
        var s = ClaudeKey.Scale(canvas);
        var w = canvas.Width;

        var icon = (int)(34 * s);
        canvas.DrawSymbol("bell-ring-outline", (w - icon) / 2, (int)(10 * s), icon, icon, ClaudeKey.Orange);

        var top = (int)(47 * s);
        var width = w - (int)(10 * s);
        if (count == 1)
        {
            var (size, lines) = TextFit.Wrap(canvas, project, width, 13 * s, 9 * s, 2, bold: true);
            TextFit.DrawLines(canvas, lines, size, true, 0, top, w, ClaudeKey.Orange);
            return;
        }

        // Several waiting: the first project on one line, the rest counted under it.
        var fit = TextFit.FitSingleLine(canvas, project, width, 13 * s, 8 * s, bold: true);
        var bottom = TextFit.DrawLines(canvas, [project], fit, true, 0, top, w, ClaudeKey.Orange);
        canvas.DrawText($"+{count - 1} more", 0, bottom, w, (int)(11 * s), ClaudeKey.Orange, 9 * s, TextHAlign.Center, TextVAlign.Middle);
    }

    public override Task Execute(CommandContext ctx) => GuardAsync(ctx, "focus", () =>
    {
        if (!Plugin.WaitingEnabled)
        {
            Hint(ctx, "Alerts are off");
            return Task.CompletedTask;
        }

        Plugin.SessionsGate.Touch();
        Plugin.Sessions.Rescan();
        var waiting = Plugin.Sessions.Waiting;
        if (waiting.Count == 0)
        {
            ctx.Host.Logger.Info($"Waiting key tapped: nothing waiting ({Plugin.Sessions.Sessions.Count} live sessions in {Plugin.Sessions.Directory})");
            Hint(ctx, "Nothing waiting");
            return Task.CompletedTask;
        }

        var target = waiting[_cycle % waiting.Count];
        _cycle = (_cycle + 1) % Math.Max(1, waiting.Count);
        if (!ProcessFocus.Focus(ctx.Host, target)) Hint(ctx, target.Name);
        return Task.CompletedTask;
    });
}
