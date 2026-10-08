namespace LoupixDeck.Plugin.Claude.Usage;

/// <summary>One usage window as the usage endpoint reports it: percent used and when it resets.</summary>
internal sealed record UsageWindow(double Utilization, DateTimeOffset? ResetsAt)
{
    public string ResetsIn(DateTimeOffset now)
    {
        if (ResetsAt is not { } at) return string.Empty;
        var left = at - now;
        if (left <= TimeSpan.Zero) return "now";
        if (left.TotalHours >= 48) return $"{(int)left.TotalDays}d";
        if (left.TotalHours >= 1) return $"{(int)left.TotalHours}h{left.Minutes:00}";
        return $"{left.Minutes}m";
    }
}

/// <summary>
/// The last thing we learned about usage. <see cref="Error"/> is set when the fetch failed; the
/// windows then hold the previous good values (if any) so the key keeps showing something useful.
/// </summary>
internal sealed record UsageSnapshot(
    UsageWindow? FiveHour,
    UsageWindow? SevenDay,
    DateTimeOffset? FetchedAt,
    string? Error)
{
    public static readonly UsageSnapshot Empty = new(null, null, null, null);

    public bool HasData => FiveHour is not null || SevenDay is not null;
}
