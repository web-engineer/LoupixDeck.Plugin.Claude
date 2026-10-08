using LoupixDeck.Plugin.Claude.Rendering;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// Usage gauges. The combined key shows the 5-hour and 7-day limits as two rings; the single keys
/// show one window with its percentage and the time until it resets. Keys are repainted on the
/// host's timer and immediately after each fetch; a tap fetches now and shows when the windows reset.
/// </summary>
internal sealed class UsageCommand : ClaudeCommandBase, IDisplayImageCommand
{
    public enum View
    {
        Combined,
        FiveHour,
        Weekly
    }

    public const string Name = "Claude.Usage";
    public const string FiveHourName = "Claude.UsageFiveHour";
    public const string WeeklyName = "Claude.UsageWeekly";

    public static readonly IReadOnlyList<string> Names = [Name, FiveHourName, WeeklyName];

    private readonly View _view;

    public UsageCommand(ClaudePlugin plugin, View view) : base(plugin)
    {
        _view = view;
        Descriptor = view switch
        {
            View.FiveHour => Describe(FiveHourName, "Usage: 5-hour",
                "Shows how much of your 5-hour Claude limit is used and when it resets (reads the Claude Code sign-in). Tap to refresh."),
            View.Weekly => Describe(WeeklyName, "Usage: weekly",
                "Shows how much of your 7-day Claude limit is used and when it resets (reads the Claude Code sign-in). Tap to refresh."),
            _ => Describe(Name, "Usage limits",
                "Shows how much of your 5-hour (outer ring) and 7-day (inner ring) Claude limits is used (reads the Claude Code sign-in). Tap to refresh and see the reset times.")
        };
    }

    public override CommandDescriptor Descriptor { get; }

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    /// <summary>Repaint cadence; the reset countdown only needs minute resolution.</summary>
    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(30);

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        if (!Plugin.UsageEnabled)
        {
            UsageRenderer.DrawDisabled(canvas);
            return true;
        }

        Plugin.UsageGate.Touch();
        var snapshot = Plugin.Usage.Snapshot;
        var now = DateTimeOffset.Now;
        switch (_view)
        {
            case View.FiveHour:
                UsageRenderer.DrawSingle(canvas, snapshot, snapshot.FiveHour, "5h", now);
                break;
            case View.Weekly:
                UsageRenderer.DrawSingle(canvas, snapshot, snapshot.SevenDay, "week", now);
                break;
            default:
                UsageRenderer.DrawCombined(canvas, snapshot);
                break;
        }

        return true;
    }

    public override Task Execute(CommandContext ctx) => GuardAsync(ctx, "usage refresh", async () =>
    {
        if (!Plugin.UsageEnabled)
        {
            Hint(ctx, "Usage is off");
            return;
        }

        Plugin.UsageGate.Touch();
        var snapshot = await Plugin.Usage.RefreshAsync().ConfigureAwait(false);
        Plugin.RefreshUsageKeys();

        var now = DateTimeOffset.Now;
        var text = snapshot.Error is not null && !snapshot.HasData
            ? snapshot.Error
            : string.Join("\n",
                snapshot.FiveHour is { } f ? $"5h {f.Utilization:0}% ↻{f.ResetsIn(now)}" : null,
                snapshot.SevenDay is { } s ? $"7d {s.Utilization:0}% ↻{s.ResetsIn(now)}" : null);
        Hint(ctx, text);
    });

    private static CommandDescriptor Describe(string name, string displayName, string description) => new()
    {
        CommandName = name,
        DisplayName = displayName,
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F029A", // mdi-gauge
        Description = description,
        ButtonLayout = new ButtonLayoutDescriptor { Mode = ButtonLayoutMode.None }
    };
}
