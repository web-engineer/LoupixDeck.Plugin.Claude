using LoupixDeck.Plugin.Claude.Rendering;
using LoupixDeck.Plugin.Claude.Sessions;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// The "Claude wants you" key. Its button state follows the live sessions (idle / busy / waiting),
/// and each state brings its own editable layers: an hourglass while sessions work, Zzz when idle,
/// and an orange bell with the project name on white when a session is waiting. The plugin only
/// reports the project name (the key's value text); the layers draw everything.
/// Tap to bring that session's window to the front; with several waiting, each tap moves on.
/// </summary>
internal sealed class WaitingCommand(ClaudePlugin plugin) : ClaudeCommandBase(plugin), IValueDisplayCommand
{
    public const string Name = "Claude.Waiting";
    public const string IdleState = "idle";
    public const string BusyState = "busy";
    public const string WaitingState = "waiting";

    private int _cycle;

    /// <summary>The state last set on each key, so a key is only switched when its state changes.</summary>
    private readonly Dictionary<string, string> _shownStates = new();
    private readonly Lock _shownGate = new();

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "Current state",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F009F", // mdi-bell-ring-outline
        Description = "Shows what your Claude Code sessions are doing: working, idle, or waiting for you (a permission or a question). Tap to jump to the one that is waiting.",
        States =
        [
            new ButtonStateDescriptor
            {
                Name = WaitingState, Description = "A session is waiting for your input",
                // Inverted so it stands out from the other Claude keys.
                Layout = StateLayout(ClaudeKey.WhiteHex,
                    new ButtonLayerDescriptor
                    {
                        Kind = ButtonLayerKind.Symbol, Name = "Bell", Glyph = "\U000F009F", // mdi-bell-ring-outline
                        IconScale = 0.4, OffsetY = -14, Color = ClaudeKey.OrangeHex
                    },
                    new ButtonLayerDescriptor
                    {
                        Kind = ButtonLayerKind.Text, Name = "Project", TextSource = ButtonTextSource.Value, TextSize = 12,
                        OffsetY = 16, BoxWidth = 84, BoxHeight = 18, Color = ClaudeKey.OrangeHex
                    },
                    new ButtonLayerDescriptor
                    {
                        Kind = ButtonLayerKind.Text, Name = "More waiting", TextSource = ButtonTextSource.Detail, TextSize = 9,
                        OffsetY = 31, BoxWidth = 84, BoxHeight = 12, Color = ClaudeKey.OrangeHex
                    })
            },
            new ButtonStateDescriptor
            {
                Name = BusyState, Description = "Sessions are working, nothing to do",
                Layout = StateLayout(ClaudeKey.OrangeHex,
                    new ButtonLayerDescriptor
                    {
                        Kind = ButtonLayerKind.Symbol, Name = "Hourglass", Glyph = "\U000F051F", // mdi-timer-sand
                        IconScale = 0.58, Color = ClaudeKey.WhiteHex
                    })
            },
            new ButtonStateDescriptor
            {
                Name = IdleState, Description = "No session needs you",
                Layout = StateLayout(ClaudeKey.OrangeHex,
                    new ButtonLayerDescriptor
                    {
                        Kind = ButtonLayerKind.Symbol, Name = "Zzz", Glyph = "\U000F04B2", // mdi-sleep
                        IconScale = 0.58, Color = ClaudeKey.WhiteHex
                    },
                    // Shows "Off" while alerts are switched off; empty otherwise.
                    new ButtonLayerDescriptor
                    {
                        Kind = ButtonLayerKind.Text, Name = "Status", TextSource = ButtonTextSource.Value, TextSize = 9,
                        OffsetY = 33, BoxWidth = 84, BoxHeight = 12, Color = ClaudeKey.WhiteHex
                    })
            }
        ],
        // Every state brings its own layers.
        ButtonLayout = new ButtonLayoutDescriptor { Mode = ButtonLayoutMode.None }
    };

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    /// <summary>Keeps the demand gate alive; real updates arrive by push from the session monitor.</summary>
    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(5);

    public AdjustmentValue? GetValue(CommandContext ctx)
    {
        if (!Plugin.WaitingEnabled)
        {
            Show(ctx, IdleState);
            return new AdjustmentValue(double.NaN, "Off");
        }

        Plugin.SessionsGate.Touch();
        var sessions = Plugin.Sessions.Sessions;
        var waiting = sessions.Where(s => s.IsWaiting).ToList();
        if (waiting.Count > 0)
        {
            Show(ctx, WaitingState);
            return new AdjustmentValue(double.NaN, waiting[0].ProjectName)
            {
                Detail = waiting.Count > 1 ? $"+{waiting.Count - 1} more" : null
            };
        }

        Show(ctx, sessions.Any(s => s.Status == SessionStatus.Busy) ? BusyState : IdleState);
        return null;
    }

    /// <summary>Forgets the states set so far, so every key is switched again on its next poll.</summary>
    public void ResetShownStates()
    {
        lock (_shownGate) _shownStates.Clear();
    }

    /// <summary>
    /// Switches the key to <paramref name="state"/>, but only when that differs from what this key
    /// was last switched to. Setting it on every poll would undo the user picking another state in
    /// the button editor to restyle it.
    /// </summary>
    private void Show(CommandContext ctx, string state)
    {
        var key = ctx.ButtonKey ?? string.Empty;
        lock (_shownGate)
        {
            if (_shownStates.TryGetValue(key, out var shown) && shown == state) return;
            _shownStates[key] = state;
        }

        ctx.Host.SetActiveButtonState(Name, state);
    }

    private static ButtonLayoutDescriptor StateLayout(string background, params ButtonLayerDescriptor[] layers) => new()
    {
        Mode = ButtonLayoutMode.Custom,
        BackgroundColor = background,
        Layers = layers
    };

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
