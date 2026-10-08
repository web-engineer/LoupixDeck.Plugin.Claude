using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Rendering;

/// <summary>
/// The shared Claude look: a Claude-orange background with white icons and text, so Claude keys
/// stand out from the rest of the deck. Declared layouts use the hex values (the user can restyle
/// them); the waiting key, which still draws itself, uses the colours. Drawing sizes are designed
/// on a 90 px key and scaled to the key actually being drawn.
/// </summary>
internal static class ClaudeKey
{
    public const string OrangeHex = "#FF551C";
    public const string WhiteHex = "#FFFFFF";

    /// <summary>Indicator track: black at 20 % over the orange.</summary>
    public const string TrackHex = "#33000000";

    /// <summary>Secondary text: white at 70 %.</summary>
    public const string FadedHex = "#B3FFFFFF";

    public static readonly PluginColor Orange = new(0xFF, 0x55, 0x1C);
    public static readonly PluginColor White = PluginColor.White;
    public static readonly PluginColor Faded = new(255, 255, 255, 140);

    /// <summary>Scale from the 90 px design key to this canvas.</summary>
    public static float Scale(IRenderCanvas canvas) => Math.Min(canvas.Width, canvas.Height) / 90f;

    /// <summary>A single centred icon, for status keys whose state is clear without words.</summary>
    public static void DrawIcon(IRenderCanvas canvas, string symbol, PluginColor tint)
    {
        canvas.Clear(Orange);
        var s = Scale(canvas);
        var icon = (int)(52 * s);
        canvas.DrawSymbol(symbol, (canvas.Width - icon) / 2, (canvas.Height - icon) / 2, icon, icon, tint);
    }
}
