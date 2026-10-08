using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Rendering;

/// <summary>
/// The shared Claude look: every key the plugin draws has a Claude-orange background with white
/// icons and text, so Claude keys stand out from the rest of the deck. Sizes are designed on a
/// 90 px key and scaled to the key actually being drawn.
/// </summary>
internal static class ClaudeKey
{
    public static readonly PluginColor Orange = new(217, 119, 87);
    public static readonly PluginColor White = PluginColor.White;
    public static readonly PluginColor Faded = new(255, 255, 255, 140);

    /// <summary>Ring track: black at 20 % over the orange.</summary>
    public static readonly PluginColor Track = new(0, 0, 0, 51);

    /// <summary>Scale from the 90 px design key to this canvas.</summary>
    public static float Scale(IRenderCanvas canvas) => Math.Min(canvas.Width, canvas.Height) / 90f;

    /// <summary>A launcher key: icon above, caption below, white on orange.</summary>
    public static void DrawLauncher(IRenderCanvas canvas, string symbol, string caption)
    {
        canvas.Clear(Orange);
        var s = Scale(canvas);
        var w = canvas.Width;

        var icon = (int)(38 * s);
        canvas.DrawSymbol(symbol, (w - icon) / 2, (int)(14 * s), icon, icon, White);

        var size = TextFit.FitSingleLine(canvas, caption, w - (int)(8 * s), 15 * s, 9 * s, bold: true);
        canvas.DrawText(caption, 0, (int)(58 * s), w, (int)(20 * s), White, size, TextHAlign.Center, TextVAlign.Middle, bold: true);
    }

    /// <summary>A single centred icon, for status keys whose state is clear without words.</summary>
    public static void DrawIcon(IRenderCanvas canvas, string symbol, PluginColor tint)
    {
        canvas.Clear(Orange);
        var s = Scale(canvas);
        var icon = (int)(52 * s);
        canvas.DrawSymbol(symbol, (canvas.Width - icon) / 2, (canvas.Height - icon) / 2, icon, icon, tint);
    }
}
