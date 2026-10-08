using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// Opens Claude's Quick Entry bar by sending its global shortcut. Quick Entry has no deep link,
/// so the shortcut set in the plugin settings must match Claude › Settings › System › Quick access shortcut.
/// </summary>
internal sealed class QuickEntryCommand(ClaudePlugin plugin) : LauncherCommandBase(plugin)
{
    public const string Name = "Claude.QuickEntry";

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "Quick Entry",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F07B7", // mdi-console-line
        Description = "Open Claude's Quick Entry bar by pressing its global shortcut (set it in the plugin settings to match Claude › Settings › System › Quick access shortcut).",
        ButtonLayout = SelfDrawn
    };

    protected override string Symbol => "console-line";
    protected override string Caption => "Claude";

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
