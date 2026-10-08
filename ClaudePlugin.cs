using LoupixDeck.Plugin.Claude.Commands;
using LoupixDeck.Plugin.Claude.Platform;
using LoupixDeck.Plugin.Claude.Sessions;
using LoupixDeck.Plugin.Claude.Usage;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude;

/// <summary>
/// LoupixDeck plugin entry point. Owns the two background workers (the Claude Code session
/// monitor and the usage poller), each started on demand by its key and stopped when the key is
/// no longer shown or its setting is off, and exposes the launcher commands.
/// </summary>
public sealed class ClaudePlugin : LoupixPlugin, IPluginSettingsPage
{
    public const string GroupName = "Claude";

    private const string QuickEntryKey = "quick_entry_shortcut";
    private const string SettingsShortcutKey = "settings_shortcut";
    private const string ShowUsageKey = "show_usage";
    private const string AccessTokenKey = "access_token";
    private const string AlertWaitingKey = "alert_waiting";
    private const string SessionsDirKey = "sessions_dir";

    /// <summary>Demand windows: a worker stops this long after its key was last rendered.</summary>
    private static readonly TimeSpan SessionsIdle = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UsageIdle = TimeSpan.FromMinutes(12);

    private readonly Lazy<IReadOnlyList<IPluginCommand>> _commands;
    private IPluginHost _host = null!;
    private SessionMonitor _sessions = null!;
    private UsageClient _usage = null!;
    private DemandGate _sessionsGate = null!;
    private DemandGate _usageGate = null!;

    public ClaudePlugin()
    {
        _commands = new Lazy<IReadOnlyList<IPluginCommand>>(BuildCommands, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public override PluginMetadata Metadata { get; } = new()
    {
        Id = "claude",
        Name = "Claude",
        Version = new Version(0, 1, 0),
        SdkVersion = SdkInfo.Version,
        Author = "Craig Lawson",
        Description = "Open Claude, Quick Entry and Settings from the deck, watch your usage limits, and get a key that lights when a Claude Code session is waiting for you.",
        Icon = LoadEmbeddedIcon("LoupixDeck.Plugin.Claude.icon.png")
    };

    // ---- state the commands read ----

    internal SessionMonitor Sessions => _sessions;
    internal UsageClient Usage => _usage;
    internal DemandGate SessionsGate => _sessionsGate;
    internal DemandGate UsageGate => _usageGate;

    internal bool UsageEnabled => _host.Settings.Get(ShowUsageKey, true);
    internal bool WaitingEnabled => _host.Settings.Get(AlertWaitingKey, true);
    internal string QuickEntryShortcut => _host.Settings.Get(QuickEntryKey, ClaudeApp.DefaultQuickEntryShortcut) ?? string.Empty;
    internal string SettingsShortcut => _host.Settings.Get(SettingsShortcutKey, ClaudeApp.DefaultSettingsShortcut) is { Length: > 0 } s ? s : ClaudeApp.DefaultSettingsShortcut;

    private string SessionsDirectory =>
        _host.Settings.Get(SessionsDirKey, string.Empty) is { Length: > 0 } dir ? dir : SessionMonitor.DefaultDirectory;

    // ---- lifecycle ----

    public override void Initialize(IPluginHost host)
    {
        _host = host;
        _usage = new UsageClient(host.Logger, () => host.Settings.Get<string>(AccessTokenKey));
        _usage.Changed += () => _host.RequestButtonRefresh(UsageCommand.Name);
        _usageGate = new DemandGate(() => UsageEnabled, StartUsage, StopUsage, UsageIdle);

        _sessions = CreateMonitor();
        _sessionsGate = new DemandGate(() => WaitingEnabled, StartSessions, StopSessions, SessionsIdle);

        host.Logger.Info(ClaudeApp.IsDesktopInstalled
            ? "Claude desktop app found; deep links enabled"
            : "Claude desktop app not found; launcher keys open claude.ai in the browser");
    }

    public override void Shutdown()
    {
        _sessionsGate?.Dispose();
        _usageGate?.Dispose();
        _sessions?.Dispose();
        _usage?.Dispose();
    }

    public override IEnumerable<IPluginCommand> GetCommands() => _commands.Value;

    private IReadOnlyList<IPluginCommand> BuildCommands() =>
    [
        new OpenCommand(this),
        new QuickEntryCommand(this),
        new SettingsCommand(this),
        new NewCodeSessionCommand(this),
        new ContinueLastCommand(this),
        new WaitingCommand(this),
        new UsageCommand(this)
    ];

    public override IReadOnlyList<CommandGroupDescriptor> GetCommandGroups() =>
    [
        new CommandGroupDescriptor
        {
            Group = GroupName,
            Description = "Claude: open the app, Quick Entry, Settings, usage limits and a key that lights when Claude Code is waiting for you",
            Icon = "\U000F0D37"
        }
    ];

    // ---- workers ----

    private SessionMonitor CreateMonitor()
    {
        var monitor = new SessionMonitor(SessionsDirectory, _host.Logger);
        monitor.Changed += OnSessionsChanged;
        return monitor;
    }

    private void StartSessions()
    {
        _host.Logger.Info("Session watcher started");
        _sessions.Start();
    }

    private void StopSessions()
    {
        _host.Logger.Info("Session watcher stopped");
        _sessions.Stop();
    }

    private void StartUsage()
    {
        _host.Logger.Info("Usage poller started");
        _usage.Start();
    }

    private void StopUsage()
    {
        _host.Logger.Info("Usage poller stopped");
        _usage.Stop();
    }

    private void OnSessionsChanged()
    {
        var waiting = _sessions.Waiting;
        _host.Logger.Info($"Claude Code sessions: {_sessions.Sessions.Count} live, {waiting.Count} waiting" +
                          (waiting.Count > 0 ? $" ({string.Join(", ", waiting.Select(s => s.Name))})" : string.Empty));
        _host.SetActiveButtonState(WaitingCommand.Name,
            waiting.Count > 0 ? WaitingCommand.WaitingState
            : _sessions.Sessions.Any(s => s.Status == SessionStatus.Busy) ? WaitingCommand.BusyState
            : WaitingCommand.IdleState);
        _host.RequestButtonRefresh(WaitingCommand.Name);
    }

    // ---- IPluginSettingsPage ----

    public IReadOnlyList<PluginSettingDescriptor> SettingsSchema =>
    [
        new PluginSettingDescriptor
        {
            Key = "launcher",
            Label = "Launcher",
            Kind = PluginSettingKind.Heading,
            Description = "Shortcuts are sent as key presses, so they must match what the Claude app expects. Key names follow the host's macro syntax, e.g. \"Alt+Space\" or \"Cmd+Comma\"."
        },
        new PluginSettingDescriptor
        {
            Key = QuickEntryKey,
            Label = "Quick Entry shortcut",
            Kind = PluginSettingKind.Text,
            DefaultValue = ClaudeApp.DefaultQuickEntryShortcut,
            Description = "Set Claude › Settings › General › Quick Entry to the same shortcut (Option+Space recommended on macOS)."
        },
        new PluginSettingDescriptor
        {
            Key = SettingsShortcutKey,
            Label = "Settings shortcut",
            Kind = PluginSettingKind.Text,
            DefaultValue = ClaudeApp.DefaultSettingsShortcut,
            Description = "The app's own Settings shortcut; only change it if Claude's differs."
        },
        new PluginSettingDescriptor
        {
            Key = "usage",
            Label = "Usage limits",
            Kind = PluginSettingKind.Heading,
            Description = "The usage key asks Anthropic every 5 minutes, using the account Claude Code is signed in with, but only while the key is on screen and the device is on."
        },
        new PluginSettingDescriptor
        {
            Key = ShowUsageKey,
            Label = "Show usage",
            Kind = PluginSettingKind.Toggle,
            DefaultValue = true,
            Description = "Off: no requests are made and the key shows \"off\"."
        },
        new PluginSettingDescriptor
        {
            Key = AccessTokenKey,
            Label = "Access token (optional)",
            Kind = PluginSettingKind.Password,
            DefaultValue = string.Empty,
            Description = "Only needed when the Claude Code sign-in cannot be found automatically. See the README for where each OS keeps it."
        },
        new PluginSettingDescriptor
        {
            Key = "sessions",
            Label = "Claude Code sessions",
            Kind = PluginSettingKind.Heading,
            Description = "The waiting key follows the session files Claude Code writes while it runs; nothing needs installing in Claude Code."
        },
        new PluginSettingDescriptor
        {
            Key = AlertWaitingKey,
            Label = "Alert me when Claude wants input",
            Kind = PluginSettingKind.Toggle,
            DefaultValue = true,
            Description = "Off: the session folder is not watched and the key shows \"Off\"."
        },
        new PluginSettingDescriptor
        {
            Key = SessionsDirKey,
            Label = "Session folder (optional)",
            Kind = PluginSettingKind.Text,
            DefaultValue = string.Empty,
            Description = "Leave blank for ~/.claude/sessions."
        }
    ];

    public IReadOnlyList<PluginSettingAction> SettingsActions =>
    [
        new PluginSettingAction { Label = "Test usage lookup", Invoke = TestUsageAsync },
        new PluginSettingAction { Label = "List Claude Code sessions", Invoke = ListSessionsAsync }
    ];

    public void OnSettingsSaved()
    {
        // Workers restart lazily from the next key render with the new settings.
        _usageGate.Reset();
        _sessionsGate.Reset();

        var old = _sessions;
        _sessions = CreateMonitor();
        old.Changed -= OnSessionsChanged;
        old.Dispose();

        _host.RequestButtonRefresh(UsageCommand.Name);
        _host.RequestButtonRefresh(WaitingCommand.Name);
    }

    private async Task<string> TestUsageAsync()
    {
        var (token, source) = CredentialSource.Resolve(_host.Settings.Get<string>(AccessTokenKey));
        if (token is null) return source;

        var snapshot = await _usage.RefreshAsync().ConfigureAwait(false);
        if (!snapshot.HasData) return $"Token from {source}, but the lookup failed: {snapshot.Error}";

        var now = DateTimeOffset.Now;
        var five = snapshot.FiveHour is { } f ? $"5-hour window {f.Utilization:0}% used, resets in {f.ResetsIn(now)}" : "5-hour window not reported";
        var seven = snapshot.SevenDay is { } s ? $"7-day window {s.Utilization:0}% used, resets in {s.ResetsIn(now)}" : "7-day window not reported";
        return $"Token from {source}. {five}; {seven}.";
    }

    private Task<string> ListSessionsAsync()
    {
        var probe = new SessionMonitor(SessionsDirectory, _host.Logger);
        probe.Rescan();
        var sessions = probe.Sessions;
        if (sessions.Count == 0) return Task.FromResult($"No live Claude Code sessions in {probe.Directory}");
        var lines = sessions.Select(s => $"{s.Name} ({s.ProjectName}): {s.Status.ToString().ToLowerInvariant()}");
        return Task.FromResult($"{sessions.Count} live session(s): " + string.Join("; ", lines));
    }

    private static byte[]? LoadEmbeddedIcon(string name)
    {
        try
        {
            using var stream = typeof(ClaudePlugin).Assembly.GetManifestResourceStream(name);
            if (stream is null) return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
