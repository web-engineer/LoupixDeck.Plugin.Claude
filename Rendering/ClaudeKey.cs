namespace LoupixDeck.Plugin.Claude.Rendering;

/// <summary>
/// The shared Claude look: a Claude-orange background with white icons and text, so Claude keys
/// stand out from the rest of the deck. Commands declare it as ordinary layers and a background
/// colour, which the user can restyle in the button editor.
/// </summary>
internal static class ClaudeKey
{
    public const string OrangeHex = "#FF551C";
    public const string WhiteHex = "#FFFFFF";

    /// <summary>Indicator track: black at 20 % over the orange.</summary>
    public const string TrackHex = "#33000000";

    /// <summary>Secondary text: white at 70 %.</summary>
    public const string FadedHex = "#B3FFFFFF";
}
