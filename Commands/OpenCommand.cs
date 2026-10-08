using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>Opens Claude on a new, empty chat.</summary>
internal sealed class OpenCommand(ClaudePlugin plugin) : LauncherCommandBase(plugin)
{
    public const string Name = "Claude.Open";

    // No prompt parameter: a prefilled prompt makes Claude warn that a link is filling in the
    // message, and the actions panel would fill an empty one with the action's name anyway.
    // Keys saved with a parameter, e.g. "Claude.Open(Open Claude)", still run; it is ignored.
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "Open Claude",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F036A", // mdi-message-text-outline
        Description = "Open the Claude app on a new chat (the web app when the desktop app is not installed).",
        ButtonLayout = Layout("Open Claude")
    };

    public override Task Execute(CommandContext ctx)
    {
        if (!ClaudeApp.OpenLink(ctx.Host, ClaudeApp.NewChatPath)) Hint(ctx, "Claude not found");
        return Task.CompletedTask;
    }
}
