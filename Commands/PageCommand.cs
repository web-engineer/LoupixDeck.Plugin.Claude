using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>
/// Opens one page of the Claude app (Design, Customize, Scheduled) through its <c>claude://</c>
/// deep link, or the same page on claude.ai when the desktop app is not installed.
/// </summary>
internal sealed class PageCommand : LauncherCommandBase
{
    public const string DesignName = "Claude.Design";
    public const string CustomizeName = "Claude.Customize";
    public const string ScheduledName = "Claude.Scheduled";

    private readonly string _path;

    private PageCommand(ClaudePlugin plugin, string name, string displayName, string icon, string caption,
        string path, string description) : base(plugin)
    {
        _path = path;
        Descriptor = new CommandDescriptor
        {
            CommandName = name,
            DisplayName = displayName,
            Group = ClaudePlugin.GroupName,
            Icon = icon,
            Description = description,
            ButtonLayout = Layout(caption)
        };
    }

    public override CommandDescriptor Descriptor { get; }

    public static IEnumerable<PageCommand> All(ClaudePlugin plugin) =>
    [
        new(plugin, DesignName, "Design", "\U000F0E0C", "Design", // mdi-palette-outline
            ClaudeApp.DesignPath, "Open Claude's Design page."),
        new(plugin, CustomizeName, "Customize", "\U000F1542", "Customize", // mdi-tune-variant
            ClaudeApp.CustomizePath, "Open Claude's Customize page (skills, connectors and plugins)."),
        new(plugin, ScheduledName, "Scheduled", "\U000F00F0", "Scheduled", // mdi-calendar-clock
            ClaudeApp.ScheduledPath, "Open Claude Code's scheduled tasks.")
    ];

    public override Task Execute(CommandContext ctx)
    {
        if (!ClaudeApp.OpenLink(ctx.Host, _path)) Hint(ctx, "Claude not found");
        return Task.CompletedTask;
    }
}
