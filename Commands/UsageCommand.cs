using LoupixDeck.Plugin.Claude.Rendering;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// Usage gauge: 5-hour and 7-day limits as rings. The key is repainted on the host's timer and
/// immediately after each fetch; a tap fetches now and shows when the windows reset.
/// </summary>
internal sealed class UsageCommand(ClaudePlugin plugin) : ClaudeCommandBase(plugin), IDisplayImageCommand
{
    public const string Name = "Claude.Usage";

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "Usage limits",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F0A9D", // mdi-gauge
        Description = "Shows how much of your 5-hour and 7-day Claude limits is used (reads the Claude Code sign-in). Tap to refresh and see the reset times.",
        ButtonLayout = new ButtonLayoutDescriptor { Mode = ButtonLayoutMode.None }
    };

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
        UsageRenderer.Draw(canvas, Plugin.Usage.Snapshot, DateTimeOffset.Now);
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
        ctx.Host.RequestButtonRefresh(Name);

        var now = DateTimeOffset.Now;
        var text = snapshot.Error is not null && !snapshot.HasData
            ? snapshot.Error
            : string.Join("\n",
                snapshot.FiveHour is { } f ? $"5h {f.Utilization:0}% ↻{f.ResetsIn(now)}" : null,
                snapshot.SevenDay is { } s ? $"7d {s.Utilization:0}% ↻{s.ResetsIn(now)}" : null);
        Hint(ctx, text);
    });
}
