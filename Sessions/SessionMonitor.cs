using System.Diagnostics;
using System.Text.Json;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Sessions;

/// <summary>
/// Watches Claude Code's session directory and keeps a snapshot of the live sessions. Claude Code
/// rewrites <c>&lt;pid&gt;.json</c> whenever a session's status flips (busy / waiting / idle), so a
/// file watcher plus a slow safety rescan is enough; no hooks need installing. Sessions whose
/// process is gone are dropped so a crashed session never lights the deck.
/// </summary>
internal sealed class SessionMonitor : IDisposable
{
    private static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(150);

    private readonly string _directory;
    private readonly IPluginLogger _logger;
    private readonly object _lock = new();
    private FileSystemWatcher? _watcher;
    private Timer? _rescanTimer;
    private Timer? _debounceTimer;
    private IReadOnlyList<SessionInfo> _sessions = [];
    private string _fingerprint = string.Empty;
    private bool _running;

    public event Action? Changed;

    public SessionMonitor(string directory, IPluginLogger logger)
    {
        _directory = directory;
        _logger = logger;
    }

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "sessions");

    public string Directory => _directory;

    /// <summary>Live sessions, newest status change first.</summary>
    public IReadOnlyList<SessionInfo> Sessions { get { lock (_lock) return _sessions; } }

    public IReadOnlyList<SessionInfo> Waiting => Sessions.Where(s => s.IsWaiting).ToList();

    public bool IsRunning { get { lock (_lock) return _running; } }

    public void Start()
    {
        lock (_lock)
        {
            if (_running) return;
            _running = true;
        }

        try
        {
            if (System.IO.Directory.Exists(_directory))
            {
                _watcher = new FileSystemWatcher(_directory, "*.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    IncludeSubdirectories = false
                };
                _watcher.Changed += OnFileEvent;
                _watcher.Created += OnFileEvent;
                _watcher.Deleted += OnFileEvent;
                _watcher.Renamed += OnFileEvent;
                _watcher.Error += (_, e) => _logger.Warn($"Session watcher error: {e.GetException().Message}");
                _watcher.EnableRaisingEvents = true;
            }
            else
            {
                _logger.Info($"Claude Code session directory not found yet: {_directory}");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not watch {_directory}: {ex.Message}");
        }

        _debounceTimer = new Timer(_ => Rescan(), null, Timeout.Infinite, Timeout.Infinite);
        _rescanTimer = new Timer(_ => Rescan(), null, TimeSpan.Zero, RescanInterval);
    }

    public void Stop()
    {
        FileSystemWatcher? watcher;
        Timer? rescan, debounce;
        lock (_lock)
        {
            if (!_running) return;
            _running = false;
            watcher = _watcher; _watcher = null;
            rescan = _rescanTimer; _rescanTimer = null;
            debounce = _debounceTimer; _debounceTimer = null;
        }

        try { if (watcher is not null) { watcher.EnableRaisingEvents = false; watcher.Dispose(); } } catch { }
        rescan?.Dispose();
        debounce?.Dispose();
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        // Claude Code writes the file in one go but the OS may raise several events; coalesce.
        try { _debounceTimer?.Change(Debounce, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
    }

    /// <summary>Re-reads every session file. Safe to call from any thread; raises <see cref="Changed"/> only when something differs.</summary>
    public void Rescan()
    {
        List<SessionInfo> found = [];
        try
        {
            if (System.IO.Directory.Exists(_directory))
            {
                foreach (var file in System.IO.Directory.EnumerateFiles(_directory, "*.json"))
                {
                    var session = TryRead(file);
                    if (session is not null && IsAlive(session.Pid)) found.Add(session);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Session rescan failed: {ex.Message}");
        }

        found.Sort((a, b) => b.StatusUpdatedAt.CompareTo(a.StatusUpdatedAt));
        var fingerprint = string.Join("|", found.Select(s => $"{s.Pid}:{s.Status}:{s.Name}"));

        bool changed;
        lock (_lock)
        {
            changed = fingerprint != _fingerprint;
            _fingerprint = fingerprint;
            _sessions = found;
        }

        if (changed) Changed?.Invoke();
    }

    private SessionInfo? TryRead(string file)
    {
        try
        {
            // The file may be mid-rewrite; a parse failure is simply "try again on the next event".
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var pid = root.TryGetProperty("pid", out var p) && p.TryGetInt32(out var pidValue) ? pidValue : 0;
            if (pid <= 0) return null;

            var status = Str(root, "status") switch
            {
                "busy" => SessionStatus.Busy,
                "waiting" => SessionStatus.Waiting,
                "idle" => SessionStatus.Idle,
                _ => SessionStatus.Unknown
            };

            var updated = root.TryGetProperty("statusUpdatedAt", out var u) && u.TryGetInt64(out var ms)
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                : root.TryGetProperty("updatedAt", out var u2) && u2.TryGetInt64(out var ms2)
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ms2)
                    : DateTimeOffset.MinValue;

            return new SessionInfo(
                pid,
                Str(root, "sessionId") ?? string.Empty,
                Str(root, "name") ?? $"pid {pid}",
                Str(root, "cwd") ?? string.Empty,
                Str(root, "entrypoint") ?? string.Empty,
                status,
                updated);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch
        {
            return true; // cannot tell (permissions): keep it rather than hide a real session
        }
    }

    public void Dispose() => Stop();
}
