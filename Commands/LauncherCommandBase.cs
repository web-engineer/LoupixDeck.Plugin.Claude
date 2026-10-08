using LoupixDeck.Plugin.Claude.Rendering;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// A launcher key. It brings the Claude look as ordinary layers (icon and caption, white on the
/// Claude-orange background), so the user can restyle each part in the button editor.
/// </summary>
internal abstract class LauncherCommandBase(ClaudePlugin plugin) : ClaudeCommandBase(plugin)
{
    /// <summary>Icon above a caption, white on orange.</summary>
    protected static ButtonLayoutDescriptor Layout(string caption) => new()
    {
        Mode = ButtonLayoutMode.Custom,
        BackgroundColor = ClaudeKey.OrangeHex,
        Layers =
        [
            new ButtonLayerDescriptor { Kind = ButtonLayerKind.Symbol, Name = "Icon", IconScale = 0.46, OffsetY = -9, Color = ClaudeKey.WhiteHex },
            new ButtonLayerDescriptor
            {
                Kind = ButtonLayerKind.Text, Name = "Caption", Text = caption, TextSize = 13,
                OffsetY = 26, BoxWidth = 88, BoxHeight = 22, Color = ClaudeKey.WhiteHex
            }
        ]
    };
}
