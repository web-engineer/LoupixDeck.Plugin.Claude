using LoupixDeck.Plugin.Claude.Rendering;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// A launcher key that draws itself in the Claude look (icon and caption, white on orange), so it
/// is labelled however it was put on the button. Other targets just run the command.
/// </summary>
internal abstract class LauncherCommandBase(ClaudePlugin plugin) : ClaudeCommandBase(plugin), IDisplayImageCommand
{
    /// <summary>MDI symbol id drawn on the key.</summary>
    protected abstract string Symbol { get; }

    /// <summary>Text under the icon.</summary>
    protected abstract string Caption { get; }

    /// <summary>The key never changes; repaint rarely.</summary>
    public TimeSpan UpdateInterval => TimeSpan.FromHours(1);

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        ClaudeKey.DrawLauncher(canvas, Symbol, Caption);
        return true;
    }

    protected static ButtonLayoutDescriptor SelfDrawn { get; } = new() { Mode = ButtonLayoutMode.None };
}
