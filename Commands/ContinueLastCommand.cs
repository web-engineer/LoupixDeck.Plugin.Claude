using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>Reopens the most recent Claude Code session in the desktop app.</summary>
internal sealed class ContinueLastCommand(ClaudePlugin plugin) : LauncherCommandBase(plugin)
{
    public const string Name = "Claude.ContinueLast";

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "Continue last session",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F02DA", // mdi-history
        Description = "Open the Claude desktop app on your most recent Claude Code session.",
        ButtonLayout = Layout("Continue")
    };

    public override Task Execute(CommandContext ctx)
    {
        if (!ClaudeApp.OpenLink(ctx.Host, ClaudeApp.ContinueLastCodePath)) Hint(ctx, "Claude not found");
        return Task.CompletedTask;
    }
}
