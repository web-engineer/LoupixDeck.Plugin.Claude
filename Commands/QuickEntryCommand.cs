using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// Opens Claude's Quick Entry bar by sending its global shortcut. Quick Entry has no deep link,
/// so the shortcut set in the plugin settings must match the one in Claude › Settings › General.
/// </summary>
internal sealed class QuickEntryCommand(ClaudePlugin plugin) : ClaudeCommandBase(plugin)
{
    public const string Name = "Claude.QuickEntry";

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "Quick Entry",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F0D1E", // mdi-flash-outline
        Description = "Open Claude's Quick Entry bar by pressing its global shortcut (set it in the plugin settings to match Claude › Settings › General)."
    };

    public override Task Execute(CommandContext ctx)
    {
        if (!ClaudeApp.IsDesktopInstalled)
        {
            Hint(ctx, "No desktop app");
            return Task.CompletedTask;
        }

        var shortcut = Plugin.QuickEntryShortcut;
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            Hint(ctx, "Set shortcut");
            return Task.CompletedTask;
        }

        ctx.Host.ExecuteCommand($"System.KeyCombination({shortcut})");
        return Task.CompletedTask;
    }
}
