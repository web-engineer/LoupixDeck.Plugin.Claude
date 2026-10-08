using LoupixDeck.Plugin.Claude.Sessions;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// The "Claude wants you" key. Its button state follows the live sessions (idle / busy / waiting)
/// so the colours are yours to pick per state; the caption names the session that is waiting.
/// Tap to bring that session's window to the front; with several waiting, each tap moves on.
/// </summary>
internal sealed class WaitingCommand(ClaudePlugin plugin) : ClaudeCommandBase(plugin), IDisplayCommand
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
        Icon = "\U000F0027", // mdi-bell-ring-outline
        Description = "Lights up when a Claude Code session is waiting for you (a permission or a question). Tap to jump to it.",
        States =
        [
            new ButtonStateDescriptor { Name = WaitingState, Description = "A session is waiting for your input" },
            new ButtonStateDescriptor { Name = BusyState, Description = "Sessions are working, nothing to do" },
            new ButtonStateDescriptor { Name = IdleState, Description = "No session needs you" }
        ]
    };

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    /// <summary>Keeps the demand gate alive; real updates arrive by push from the session monitor.</summary>
    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(5);

    public string GetText(CommandContext ctx)
    {
        if (!Plugin.WaitingEnabled)
        {
            ctx.Host.SetActiveButtonState(Name, IdleState);
            return "Off";
        }

        Plugin.SessionsGate.Touch();
        var sessions = Plugin.Sessions.Sessions;
        var waiting = sessions.Where(s => s.IsWaiting).ToList();
        if (waiting.Count > 0)
        {
            ctx.Host.SetActiveButtonState(Name, WaitingState);
            var first = waiting[0];
            return waiting.Count > 1 ? $"{waiting.Count} waiting\n{first.ProjectName}" : first.ProjectName;
        }

        ctx.Host.SetActiveButtonState(Name, sessions.Any(s => s.Status == SessionStatus.Busy) ? BusyState : IdleState);
        return sessions.Count == 0 ? "No sessions" : sessions.Any(s => s.Status == SessionStatus.Busy) ? "Working" : "Idle";
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
            Hint(ctx, "Nothing waiting");
            return Task.CompletedTask;
        }

        var target = waiting[_cycle % waiting.Count];
        _cycle = (_cycle + 1) % Math.Max(1, waiting.Count);
        if (!ProcessFocus.Focus(ctx.Host, target)) Hint(ctx, target.Name);
        return Task.CompletedTask;
    });
}
