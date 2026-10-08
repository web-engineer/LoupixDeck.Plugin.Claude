namespace LoupixDeck.Plugin.Claude.Sessions;

/// <summary>Claude Code's own status words as written to its session files.</summary>
internal enum SessionStatus
{
    Unknown,
    Idle,
    Busy,
    Waiting
}

/// <summary>One live Claude Code session, as read from <c>~/.claude/sessions/&lt;pid&gt;.json</c>.</summary>
internal sealed record SessionInfo(
    int Pid,
    string SessionId,
    string Name,
    string Cwd,
    string Entrypoint,
    SessionStatus Status,
    DateTimeOffset StatusUpdatedAt)
{
    public bool IsWaiting => Status == SessionStatus.Waiting;

    /// <summary>Last path segment of the working directory, the most recognisable short label.</summary>
    public string ProjectName
    {
        get
        {
            var trimmed = Cwd.TrimEnd('/', '\\');
            var i = trimmed.LastIndexOfAny(['/', '\\']);
            return i >= 0 ? trimmed[(i + 1)..] : trimmed;
        }
    }
}
