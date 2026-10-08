using LoupixDeck.Plugin.Claude.Usage;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Rendering;

/// <summary>
/// Draws the usage key: an outer ring for the 5-hour window, an inner ring for the 7-day window,
/// the 5-hour percentage in the middle and the time until it resets underneath. Rings turn amber
/// above 80 % and red above 95 %, so a glance tells you whether to slow down.
/// </summary>
internal static class UsageRenderer
{
    private static readonly PluginColor Background = new(24, 23, 22);
    private static readonly PluginColor Track = new(58, 56, 54);
    private static readonly PluginColor Ok = new(96, 180, 120);
    private static readonly PluginColor Warn = new(232, 168, 56);
    private static readonly PluginColor Hot = new(226, 76, 60);
    private static readonly PluginColor Text = PluginColor.White;
    private static readonly PluginColor Muted = new(160, 156, 150);

    public static void Draw(IRenderCanvas canvas, UsageSnapshot snapshot, DateTimeOffset now)
    {
        canvas.Clear(Background);
        var w = canvas.Width;
        var h = canvas.Height;
        var cx = w / 2;
        var cy = h / 2;

        Ring(canvas, cx, cy, Math.Min(w, h) / 2 - 4, 6, snapshot.FiveHour);
        Ring(canvas, cx, cy, Math.Min(w, h) / 2 - 13, 4, snapshot.SevenDay);

        if (!snapshot.HasData)
        {
            canvas.DrawText("—", 0, cy - 14, w, 28, Muted, 22f, TextHAlign.Center, TextVAlign.Middle, bold: true);
            if (snapshot.Error is not null)
            {
                canvas.DrawText(ShortError(snapshot.Error), 10, h - 26, w - 20, 14, Muted, 9f, TextHAlign.Center, TextVAlign.Middle);
            }

            return;
        }

        var main = snapshot.FiveHour ?? snapshot.SevenDay!;
        canvas.DrawText($"{main.Utilization:0}%", 0, cy - 16, w, 24, Text, 20f, TextHAlign.Center, TextVAlign.Middle, bold: true);

        var sub = snapshot.FiveHour is not null && snapshot.SevenDay is not null
            ? $"wk {snapshot.SevenDay.Utilization:0}%"
            : snapshot.FiveHour is null ? "week" : "5h";
        canvas.DrawText(sub, 0, cy + 6, w, 12, Muted, 9f, TextHAlign.Center, TextVAlign.Middle);

        var reset = main.ResetsIn(now);
        var footer = snapshot.Error is not null ? "! stale" : reset.Length > 0 ? $"↻ {reset}" : string.Empty;
        if (footer.Length > 0)
        {
            canvas.DrawText(footer, 0, cy + 18, w, 12, snapshot.Error is null ? Muted : Warn, 9f, TextHAlign.Center, TextVAlign.Middle);
        }
    }

    public static void DrawDisabled(IRenderCanvas canvas)
    {
        canvas.Clear(Background);
        var cx = canvas.Width / 2;
        var cy = canvas.Height / 2;
        canvas.DrawCircle(cx, cy, Math.Min(canvas.Width, canvas.Height) / 2 - 6, 4, Track);
        canvas.DrawText("off", 0, cy - 10, canvas.Width, 20, Muted, 14f, TextHAlign.Center, TextVAlign.Middle, bold: true);
    }

    private static void Ring(IRenderCanvas canvas, int cx, int cy, int radius, int stroke, UsageWindow? window)
    {
        var x = cx - radius;
        var y = cy - radius;
        var d = radius * 2;
        canvas.DrawArc(x, y, d, d, -90f, 360f, stroke, Track);
        if (window is null) return;

        var sweep = (float)(360.0 * window.Utilization / 100.0);
        if (sweep < 2f) sweep = 2f;
        canvas.DrawArc(x, y, d, d, -90f, sweep, stroke, Colour(window.Utilization));
    }

    private static PluginColor Colour(double pct) => pct >= 95 ? Hot : pct >= 80 ? Warn : Ok;

    private static string ShortError(string error) => error.Length > 18 ? error[..17] + "…" : error;
}
