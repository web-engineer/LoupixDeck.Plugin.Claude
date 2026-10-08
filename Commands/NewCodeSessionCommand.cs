using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>Starts a new Claude Code session in the desktop app.</summary>
internal sealed class NewCodeSessionCommand(ClaudePlugin plugin) : ClaudeCommandBase(plugin)
{
    public const string Name = "Claude.NewCodeSession";

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "New Code session",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F0174", // mdi-code-tags
        Description = "Open the Claude desktop app on a new Claude Code session."
    };

    public override Task Execute(CommandContext ctx)
    {
        if (!ClaudeApp.OpenLink(ctx.Host, ClaudeApp.NewCodeSessionPath)) Hint(ctx, "Claude not found");
        return Task.CompletedTask;
    }
}
