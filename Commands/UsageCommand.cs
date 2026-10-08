using LoupixDeck.Plugin.Claude.Rendering;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// A usage gauge for one limit window (5-hour, 7-day, or the 7-day limit of one model). The plugin only reports the value; the
/// key draws it with ordinary layers it brings along (a ring indicator, the window label, the
/// percentage and the time until reset), so every part can be restyled in the button editor.
/// A tap fetches now and shows both windows.
/// </summary>
internal sealed class UsageCommand : ClaudeCommandBase, IValueDisplayCommand
{
    public enum View
    {
        FiveHour,
        Weekly,
        ModelWeekly
    }

    public const string FiveHourName = "Claude.UsageFiveHour";
    public const string WeeklyName = "Claude.UsageWeekly";
    public const string ModelWeeklyName = "Claude.UsageModelWeekly";
    public const string ModelParameter = "Model";

    /// <summary>The model whose weekly limit the per-model key shows unless its Model parameter says otherwise.</summary>
    private const string DefaultModel = "Fable";

    public static readonly IReadOnlyList<string> Names = [FiveHourName, WeeklyName, ModelWeeklyName];

    private readonly View _view;

    public UsageCommand(ClaudePlugin plugin, View view) : base(plugin)
    {
        _view = view;
        Descriptor = view switch
        {
            View.FiveHour => Describe(FiveHourName, "Usage: 5-hour", "5h",
                "Shows how much of your 5-hour Claude limit is used and when it resets (reads the Claude Code sign-in). Tap to refresh."),
            View.Weekly => Describe(WeeklyName, "Usage: weekly", "week",
                "Shows how much of your 7-day Claude limit is used and when it resets (reads the Claude Code sign-in). Tap to refresh."),
            _ => Describe(ModelWeeklyName, "Usage: weekly per model", DefaultModel,
                "Shows how much of one model's 7-day limit is used (Fable unless you name another model) and when it resets. Tap to refresh.",
                new CommandParameter(ModelParameter, typeof(string)) { DefaultValue = DefaultModel })
        };
    }

    public override CommandDescriptor Descriptor { get; }

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    /// <summary>Poll cadence; the reset countdown only needs minute resolution.</summary>
    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(30);

    public AdjustmentValue? GetValue(CommandContext ctx)
    {
        if (!Plugin.UsageEnabled) return new AdjustmentValue(double.NaN, "off");

        Plugin.UsageGate.Touch();
        var snapshot = Plugin.Usage.Snapshot;
        var window = _view switch
        {
            View.FiveHour => snapshot.FiveHour,
            View.Weekly => snapshot.SevenDay,
            _ => snapshot.ModelWeekly.GetValueOrDefault(FirstParameter(ctx.Parameters) ?? DefaultModel)
        };
        if (window is null)
        {
            return new AdjustmentValue(double.NaN, "—") { Detail = snapshot.Error is { } error ? Shorten(error) : null };
        }

        var reset = window.ResetsIn(DateTimeOffset.Now);
        return new AdjustmentValue(window.Utilization / 100.0, $"{window.Utilization:0}%")
        {
            Detail = snapshot.Error is not null ? "! stale" : reset.Length > 0 ? reset : null
        };
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
            : string.Join("\n", new[]
                {
                    snapshot.FiveHour is { } f ? $"5h {f.Utilization:0}% {f.ResetsIn(now)}" : null,
                    snapshot.SevenDay is { } s ? $"7d {s.Utilization:0}% {s.ResetsIn(now)}" : null
                }
                .Concat(snapshot.ModelWeekly.Select(m => $"{m.Key} 7d {m.Value.Utilization:0}% {m.Value.ResetsIn(now)}"))
                .Where(line => line is not null));
        Hint(ctx, text);
    });

    private static string Shorten(string error) => error.Length > 14 ? error[..13] + "…" : error;

    private static CommandDescriptor Describe(string name, string displayName, string label, string description,
        CommandParameter? parameter = null) => new()
    {
        CommandName = name,
        ParameterTemplate = parameter is null ? null : $"({{{parameter.Name}}})",
        Parameters = parameter is null ? [] : [parameter],
        DisplayName = displayName,
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F029A", // mdi-gauge
        Description = description,
        ButtonLayout = new ButtonLayoutDescriptor
        {
            Mode = ButtonLayoutMode.Custom,
            BackgroundColor = ClaudeKey.OrangeHex,
            Layers =
            [
                // A full ring from 12 o'clock, inset so the key's rounded corners never crop it.
                new ButtonLayerDescriptor
                {
                    Kind = ButtonLayerKind.Indicator, Name = "Usage ring", IconScale = 0.84,
                    Color = ClaudeKey.WhiteHex, TrackColor = ClaudeKey.TrackHex, Thickness = 0.11,
                    StartAngle = -90, SweepAngle = 360
                },
                new ButtonLayerDescriptor
                {
                    Kind = ButtonLayerKind.Text, Name = "Window", Text = label, TextSize = 9,
                    OffsetY = -16, BoxWidth = 50, BoxHeight = 12, Color = ClaudeKey.FadedHex
                },
                new ButtonLayerDescriptor
                {
                    Kind = ButtonLayerKind.Text, Name = "Percent", TextSource = ButtonTextSource.Value, TextSize = 17,
                    BoxWidth = 54, BoxHeight = 20, Color = ClaudeKey.WhiteHex
                },
                new ButtonLayerDescriptor
                {
                    Kind = ButtonLayerKind.Text, Name = "Reset", TextSource = ButtonTextSource.Detail, TextSize = 9,
                    OffsetY = 15, BoxWidth = 54, BoxHeight = 12, Color = ClaudeKey.WhiteHex
                }
            ]
        }
    };
}
