using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>Brings the Claude desktop app forward and opens its Settings with the app's shortcut.</summary>
internal sealed class SettingsCommand(ClaudePlugin plugin) : ClaudeCommandBase(plugin)
{
    public const string Name = "Claude.Settings";

    /// <summary>Time for the app to come to the front before the shortcut lands in it.</summary>
    private static readonly TimeSpan ActivateDelay = TimeSpan.FromMilliseconds(450);

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = Name,
        DisplayName = "Claude Settings",
        Group = ClaudePlugin.GroupName,
        Icon = "\U000F08BB", // mdi-cog-outline
        Description = "Bring the Claude desktop app to the front and open its Settings."
    };

    public override Task Execute(CommandContext ctx) => GuardAsync(ctx, "settings", async () =>
    {
        if (!ClaudeApp.IsDesktopInstalled)
        {
            Hint(ctx, "No desktop app");
            return;
        }

        if (!ClaudeApp.Activate(ctx.Host)) return;
        await Task.Delay(ActivateDelay).ConfigureAwait(false);
        ctx.Host.ExecuteCommand($"System.KeyCombination({Plugin.SettingsShortcut})");
    });
}
