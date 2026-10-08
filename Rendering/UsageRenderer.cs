using LoupixDeck.Plugin.Claude.Usage;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Rendering;

/// <summary>
/// Draws the usage keys in the Claude look: white rings on a darkened track over orange. The
/// combined key nests the 5-hour (outer) and 7-day (inner) rings around the 5-hour percentage;
/// a single-window key shows one ring around its label, percentage and time until reset.
/// Rings are inset well inside the key so the rounded corners of the touch display never crop them.
/// </summary>
internal static class UsageRenderer
{
    public static void DrawCombined(IRenderCanvas canvas, UsageSnapshot snapshot)
    {
        canvas.Clear(ClaudeKey.Orange);
        var s = ClaudeKey.Scale(canvas);
        var cx = canvas.Width / 2;
        var cy = canvas.Height / 2;

        Ring(canvas, cx, cy, (int)(31 * s), (int)(6 * s), snapshot.FiveHour);
        Ring(canvas, cx, cy, (int)(22 * s), (int)(4 * s), snapshot.SevenDay);

        // No room for an error inside the rings; a tap shows it.
        if (!snapshot.HasData)
        {
            canvas.DrawText("—", 0, cy - (int)(9 * s), canvas.Width, (int)(18 * s), ClaudeKey.White, 14 * s, TextHAlign.Center, TextVAlign.Middle, bold: true);
            return;
        }

        var main = snapshot.FiveHour ?? snapshot.SevenDay!;
        canvas.DrawText($"{main.Utilization:0}%", 0, cy - (int)(9 * s), canvas.Width, (int)(18 * s),
            ClaudeKey.White, 13 * s, TextHAlign.Center, TextVAlign.Middle, bold: true);
        if (snapshot.Error is not null)
        {
            canvas.DrawText("!", 0, cy + (int)(7 * s), canvas.Width, (int)(10 * s), ClaudeKey.White, 9 * s, TextHAlign.Center, TextVAlign.Middle, bold: true);
        }
    }

    public static void DrawSingle(IRenderCanvas canvas, UsageSnapshot snapshot, UsageWindow? window, string label, DateTimeOffset now)
    {
        canvas.Clear(ClaudeKey.Orange);
        var s = ClaudeKey.Scale(canvas);
        var w = canvas.Width;
        var cx = w / 2;
        var cy = canvas.Height / 2;

        Ring(canvas, cx, cy, (int)(31 * s), (int)(6 * s), window);

        canvas.DrawText(label, 0, cy - (int)(21 * s), w, (int)(11 * s), ClaudeKey.Faded, 9 * s, TextHAlign.Center, TextVAlign.Middle, bold: true);

        if (window is null)
        {
            canvas.DrawText("—", 0, cy - (int)(9 * s), w, (int)(18 * s), ClaudeKey.White, 16 * s, TextHAlign.Center, TextVAlign.Middle, bold: true);
            if (snapshot.Error is not null)
            {
                canvas.DrawText(ShortError(snapshot.Error, 10), 0, cy + (int)(9 * s), w, (int)(11 * s), ClaudeKey.White, 8 * s, TextHAlign.Center, TextVAlign.Middle);
            }

            return;
        }

        canvas.DrawText($"{window.Utilization:0}%", 0, cy - (int)(10 * s), w, (int)(20 * s),
            ClaudeKey.White, 17 * s, TextHAlign.Center, TextVAlign.Middle, bold: true);

        var reset = window.ResetsIn(now);
        var footer = snapshot.Error is not null ? "! stale" : reset.Length > 0 ? $"↻ {reset}" : string.Empty;
        if (footer.Length > 0)
        {
            canvas.DrawText(footer, 0, cy + (int)(10 * s), w, (int)(11 * s), ClaudeKey.White, 9 * s, TextHAlign.Center, TextVAlign.Middle);
        }
    }

    public static void DrawDisabled(IRenderCanvas canvas)
    {
        canvas.Clear(ClaudeKey.Orange);
        var s = ClaudeKey.Scale(canvas);
        var cx = canvas.Width / 2;
        var cy = canvas.Height / 2;
        canvas.DrawCircle(cx, cy, (int)(31 * s), (int)(6 * s), ClaudeKey.Track);
        canvas.DrawText("off", 0, cy - (int)(10 * s), canvas.Width, (int)(20 * s), ClaudeKey.White, 14 * s, TextHAlign.Center, TextVAlign.Middle, bold: true);
    }

    private static void Ring(IRenderCanvas canvas, int cx, int cy, int radius, int stroke, UsageWindow? window)
    {
        var x = cx - radius;
        var y = cy - radius;
        var d = radius * 2;
        canvas.DrawArc(x, y, d, d, -90f, 360f, stroke, ClaudeKey.Track);
        if (window is null) return;

        var sweep = (float)(360.0 * Math.Clamp(window.Utilization, 0, 100) / 100.0);
        if (sweep < 2f) sweep = 2f;
        canvas.DrawArc(x, y, d, d, -90f, sweep, stroke, ClaudeKey.White);
    }

    private static string ShortError(string error, int max) => error.Length > max ? error[..(max - 1)] + "…" : error;
}
