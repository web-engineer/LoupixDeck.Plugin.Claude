using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>Opens Claude on a new chat, optionally prefilled with a prompt.</summary>
internal sealed class OpenCommand(ClaudePlugin plugin) : LauncherCommandBase(plugin)
{
    public const string Name = "Claude.Open";
    public const string PromptParameter = "Prompt";

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "Open Claude",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F036A", // mdi-message-text-outline
        Description = "Open the Claude app on a new chat (the web app when the desktop app is not installed). An optional prompt is prefilled.",
        ParameterTemplate = "({Prompt})",
        Parameters = [new CommandParameter(PromptParameter, typeof(string)) { DefaultValue = string.Empty }],
        ButtonLayout = Layout("Open Claude")
    };

    public override Task Execute(CommandContext ctx)
    {
        var path = ClaudeApp.NewChatPath;
        if (FirstParameter(ctx.Parameters) is { } prompt)
        {
            path += "?q=" + Uri.EscapeDataString(prompt);
        }

        if (!ClaudeApp.OpenLink(ctx.Host, path)) Hint(ctx, "Claude not found");
        return Task.CompletedTask;
    }
}
