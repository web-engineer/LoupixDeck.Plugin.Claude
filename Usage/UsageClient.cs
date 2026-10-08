using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Usage;

/// <summary>
/// Polls the usage endpoint Claude Code itself uses for <c>/usage</c>. It is not a documented API:
/// the response is parsed leniently, a 429 backs the poll off up to half an hour, and any failure
/// leaves the previous numbers on the key with an error note instead of blanking it.
/// Runs only while a usage key is in demand (see <see cref="DemandGate"/>).
/// </summary>
internal sealed class UsageClient : IDisposable
{
    private const string Endpoint = "https://api.anthropic.com/api/oauth/usage";
    private static readonly TimeSpan BaseInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxInterval = TimeSpan.FromMinutes(30);

    private readonly IPluginLogger _logger;
    private readonly Func<string?> _manualToken;
    private readonly HttpClient _http;
    private readonly object _lock = new();
    private CancellationTokenSource? _loop;
    private TimeSpan _interval = BaseInterval;
    private UsageSnapshot _snapshot = UsageSnapshot.Empty;

    public event Action? Changed;

    public UsageClient(IPluginLogger logger, Func<string?> manualToken)
    {
        _logger = logger;
        _manualToken = manualToken;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LoupixDeck.Plugin.Claude/0.1 (+https://github.com/web-engineer/LoupixDeck.Plugin.Claude)");
        _http.DefaultRequestHeaders.Add("anthropic-beta", "oauth-2025-04-20");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public UsageSnapshot Snapshot { get { lock (_lock) return _snapshot; } }

    public bool IsRunning { get { lock (_lock) return _loop is not null; } }

    public void Start()
    {
        lock (_lock)
        {
            if (_loop is not null) return;
            _loop = new CancellationTokenSource();
            _interval = BaseInterval;
            _ = RunAsync(_loop.Token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? loop;
        lock (_lock)
        {
            loop = _loop;
            _loop = null;
        }

        if (loop is null) return;
        loop.Cancel();
        loop.Dispose();
    }

    /// <summary>Fetches now, outside the loop's schedule (a tap on the key, or the settings Test button).</summary>
    public Task<UsageSnapshot> RefreshAsync(CancellationToken ct = default) => FetchAsync(ct);

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await FetchAsync(ct).ConfigureAwait(false);
                TimeSpan wait;
                lock (_lock) wait = _interval;
                await Task.Delay(wait, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error("Usage poll loop stopped", ex);
        }
    }

    private async Task<UsageSnapshot> FetchAsync(CancellationToken ct)
    {
        var previous = Snapshot;
        UsageSnapshot next;
        try
        {
            var (token, source) = CredentialSource.Resolve(_manualToken());
            if (token is null)
            {
                next = previous with { Error = source, FetchedAt = DateTimeOffset.Now };
            }
            else
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    TimeSpan backoff;
                    lock (_lock)
                    {
                        _interval = _interval * 2 > MaxInterval ? MaxInterval : _interval * 2;
                        backoff = _interval;
                    }

                    _logger.Warn($"Usage endpoint rate limited (429); next poll in {backoff.TotalMinutes:0} min");
                    next = previous with { Error = "Rate limited", FetchedAt = DateTimeOffset.Now };
                }
                else if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    next = previous with { Error = "Sign in to Claude Code again", FetchedAt = DateTimeOffset.Now };
                }
                else if (!response.IsSuccessStatusCode)
                {
                    next = previous with { Error = $"HTTP {(int)response.StatusCode}", FetchedAt = DateTimeOffset.Now };
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    next = Parse(body);
                    lock (_lock) _interval = BaseInterval;
                    if (next.Error is null)
                    {
                        _logger.Info($"Usage via {source}: 5h {Percent(next.FiveHour)} / 7d {Percent(next.SevenDay)}" +
                                     string.Concat(next.ModelWeekly.Select(m => $" / {m.Key} 7d {Percent(m.Value)}")));
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            next = previous with { Error = ex is HttpRequestException ? "Offline" : ex.Message, FetchedAt = DateTimeOffset.Now };
        }

        bool changed;
        lock (_lock)
        {
            changed = next != _snapshot;
            _snapshot = next;
        }

        if (changed) Changed?.Invoke();
        return next;
    }

    private static string Percent(UsageWindow? w) => w is null ? "?" : $"{w.Utilization:0}%";

    /// <summary>
    /// Reads <c>five_hour</c> / <c>seven_day</c> → <c>utilization</c>, <c>resets_at</c>, and the model-scoped
    /// weekly entries of <c>limits</c>. Anything missing is simply absent.
    /// </summary>
    internal static UsageSnapshot Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var five = Window(root, "five_hour");
            var seven = Window(root, "seven_day");
            if (five is null && seven is null) return new UsageSnapshot(null, null, DateTimeOffset.Now, "Unexpected response");
            return new UsageSnapshot(five, seven, DateTimeOffset.Now, null) { ModelWeekly = ModelWeekly(root) };
        }
        catch (JsonException)
        {
            return new UsageSnapshot(null, null, DateTimeOffset.Now, "Unexpected response");
        }
    }

    /// <summary>
    /// <c>limits</c> entries of kind <c>weekly_scoped</c> with a model scope:
    /// <c>{"kind":"weekly_scoped","percent":67,"resets_at":"…","scope":{"model":{"display_name":"Fable"}}}</c>.
    /// </summary>
    private static Dictionary<string, UsageWindow> ModelWeekly(JsonElement root)
    {
        var result = new Dictionary<string, UsageWindow>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("limits", out var limits) || limits.ValueKind != JsonValueKind.Array) return result;

        foreach (var limit in limits.EnumerateArray())
        {
            if (limit.ValueKind != JsonValueKind.Object
                || !limit.TryGetProperty("kind", out var kind) || kind.GetString() != "weekly_scoped"
                || !limit.TryGetProperty("scope", out var scope) || scope.ValueKind != JsonValueKind.Object
                || !scope.TryGetProperty("model", out var model) || model.ValueKind != JsonValueKind.Object
                || !model.TryGetProperty("display_name", out var name) || name.GetString() is not { Length: > 0 } modelName
                || !limit.TryGetProperty("percent", out var percent) || percent.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            result[modelName] = new UsageWindow(Math.Clamp(percent.GetDouble(), 0, 100), ResetsAt(limit));
        }

        return result;
    }

    private static DateTimeOffset? ResetsAt(JsonElement w)
    {
        if (!w.TryGetProperty("resets_at", out var r)) return null;
        if (r.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)) return at;
        if (r.ValueKind == JsonValueKind.Number && r.TryGetInt64(out var epoch)) return epoch > 10_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(epoch) : DateTimeOffset.FromUnixTimeSeconds(epoch);
        return null;
    }

    private static UsageWindow? Window(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object) return null;
        if (!w.TryGetProperty("utilization", out var u)) return null;

        double pct;
        if (u.ValueKind == JsonValueKind.Number) pct = u.GetDouble();
        else if (u.ValueKind == JsonValueKind.String && double.TryParse(u.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) pct = parsed;
        else return null;

        return new UsageWindow(Math.Clamp(pct, 0, 100), ResetsAt(w));
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
    }
}
