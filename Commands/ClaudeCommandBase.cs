using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Commands;

/// <summary>Shared plumbing: late-bound plugin access, parameter helpers and error guarding.</summary>
internal abstract class ClaudeCommandBase(ClaudePlugin plugin) : IPluginCommand
{
    protected ClaudePlugin Plugin { get; } = plugin;

    public abstract CommandDescriptor Descriptor { get; }

    public virtual ButtonTargets SupportedTargets => ButtonTargets.All;

    public abstract Task Execute(CommandContext ctx);

    protected static string? FirstParameter(string[] parameters) =>
        parameters.Length > 0 && !string.IsNullOrWhiteSpace(parameters[0]) ? parameters[0].Trim() : null;

    /// <summary>Runs <paramref name="action"/> and routes any exception to the host logger instead of the caller.</summary>
    protected static async Task GuardAsync(CommandContext ctx, string what, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ctx.Host.Logger.Error($"Claude {what} failed", ex);
        }
    }

    /// <summary>Short on-key feedback for a touch button; silent for other targets.</summary>
    protected static void Hint(CommandContext ctx, string text)
    {
        if (ctx.Target == ButtonTargets.TouchButton && ctx.SourceIndex is { } slot)
        {
            ctx.Host.OverlayTouchText(slot, text, TimeSpan.FromSeconds(2));
        }
    }
}
