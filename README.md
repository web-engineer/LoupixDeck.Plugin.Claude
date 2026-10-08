# LoupixDeck.Plugin.Claude

![Claude](icon.png)

A [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck) plugin for day-to-day
Claude: open Claude (optionally with a prompt), Quick Entry, the app's Settings,
a Claude Code session launcher, a usage gauge for your 5-hour and 7-day limits,
and a key that lights when a Claude Code session is waiting for you. Built
against `LoupixDeck.PluginSdk` 1.28.0 and targets .NET 10; runs on macOS, Linux
and Windows, with the limits listed under [Platform support](#platform-support).

This is an independent project and is not affiliated with Anthropic.

## Platform support

| Feature | macOS | Windows | Linux |
|---|---|---|---|
| Open, New Code session, Continue last | Desktop app (deep link) or browser | Desktop app (deep link) or browser | Browser only |
| Quick Entry | Yes | Yes | `No desktop app` (needs the desktop app on every OS) |
| Claude Settings | Yes | Yes | `No desktop app` |
| Waiting key: state and caption | Yes | Yes | Yes |
| Waiting key: tap focuses the session | Yes | Desktop app sessions only; otherwise the key shows the session name | Key shows the session name |
| Usage key | Yes | Yes | Yes |

Deep links (`claude://`) need the Claude desktop app, which exists for macOS and
Windows. Without it:

- `Claude.Open` falls back to `https://claude.ai/new`.
- `Claude.NewCodeSession` and `Claude.ContinueLast` fall back to `https://claude.ai/code`.
- `Claude.Settings` shows `No desktop app`.

## Install

**From the Plugin Store.** Once the plugin is listed, it appears under
LoupixDeck → Plugins → Store and updates from there.

**Manually.** Download `claude-<version>-any.zip` from the
[Releases](https://github.com/web-engineer/LoupixDeck.Plugin.Claude/releases)
page and unzip it into the plugin folder:

| OS | Folder |
|---|---|
| macOS, Linux | `~/.config/LoupixDeck/plugins/claude/` |
| Windows | `%APPDATA%\LoupixDeck\plugins\claude\` |

Restart LoupixDeck, then enable the plugin for your device under
Plugins → Installed. The folder must contain `LoupixDeck.Plugin.Claude.dll`,
its `.deps.json`, `icon.png` and `plugin.json`.

**Then**, for the keys that need it:

- Quick Entry: set Claude › Settings › General › Quick Entry to the same
  shortcut as the plugin's *Quick Entry shortcut* setting (see below).
- Usage: run `claude` once and sign in (`claude`, then `/login`), on every OS.
  The plugin then finds the sign-in itself. See [Usage key](#usage-key).
- Keystrokes (Quick Entry, Settings) need the Accessibility permission that
  LoupixDeck already asks for on macOS.

## Build from source

Requires the .NET 10 SDK. The project sits at the repository root:

```bash
dotnet build -c Release
```

The build restores `LoupixDeck.PluginSdk` from nuget.org. A post-build step copies
the output into the host's plugin folder, so a rebuild followed by a LoupixDeck
restart is the whole loop:

| Host build | Target folder |
|---|---|
| Release host (default) | `~/.config/LoupixDeck/plugins/claude/` |
| Debug host (`-p:LoupixDebugHost=true`) | `~/.config/LoupixDeck/debug/plugins/claude/` |
| Windows | `%AppData%\LoupixDeck\plugins\claude\` |

Pass `-p:LoupixSkipInstall=true` to build without copying. `LoupixDeck.PluginSdk.dll`
and `*.runtimeconfig.json` are deliberately not copied: the host supplies the SDK
assembly and a second copy breaks plugin loading. Stop LoupixDeck before
rebuilding; the host holds the DLL open.

## Settings

Open LoupixDeck → Plugins → Claude → Settings. Saving applies immediately; the
workers restart from the next key render.

| Setting | Key | Default | Notes |
|---|---|---|---|
| Quick Entry shortcut | `quick_entry_shortcut` | `Alt+Space` (macOS, i.e. Option+Space), `Ctrl+Alt+Space` (Windows, Linux) | Sent as a key press. Must match Claude › Settings › General › Quick Entry. Empty: the key shows `Set shortcut`. |
| Settings shortcut | `settings_shortcut` | `Cmd+Comma` (macOS), `Ctrl+Comma` (Windows, Linux) | The app's own Settings shortcut; change it only if Claude's differs. Empty falls back to the default. |
| Show usage | `show_usage` | on | Off: no requests are made and the usage key shows `off`. |
| Access token (optional) | `access_token` | empty | Only needed when the Claude Code sign-in cannot be found. See [Usage key](#usage-key). |
| Alert me when Claude wants input | `alert_waiting` | on | Off: the session folder is not watched and the waiting key shows `Off`. |
| Session folder (optional) | `sessions_dir` | empty (`~/.claude/sessions`) | Override only if Claude Code keeps its session files elsewhere. |

Key names in the shortcut fields use the host's macro syntax: `Cmd` and `Win`
both mean the Command key on macOS, and the comma key is spelled `Comma`.

Two buttons on the page:

- **Test usage lookup** resolves the token, queries the usage endpoint and reports
  where the token came from and both windows, for example
  `Token from macOS Keychain. 5-hour window 42% used, resets in 3h12; 7-day window 18% used, resets in 4d.`
  or the reason it failed.
- **List Claude Code sessions** reports each live session as
  `name (project): busy|waiting|idle`, or that there are none.

## Commands

Command ids are permanent. All commands are in the group "Claude".

| Command id | Parameter | Targets | What it does |
|---|---|---|---|
| `Claude.Open` | `Prompt` (optional) | all | Opens a new chat via `claude://claude.ai/new`, or `https://claude.ai/new` without the desktop app. With a prompt it adds `?q=<prompt>` (URL-escaped) to prefill the box. |
| `Claude.QuickEntry` | none | all | Sends the *Quick Entry shortcut* with `System.KeyCombination(...)`. Quick Entry has no deep link. Without the desktop app it shows `No desktop app`. |
| `Claude.Settings` | none | all | Activates the desktop app, waits 450 ms, then sends the *Settings shortcut* (Cmd+Comma on macOS, Ctrl+Comma on Windows). Without the app it shows `No desktop app`. |
| `Claude.NewCodeSession` | none | all | Opens `claude://code/new`, or `https://claude.ai/code`. |
| `Claude.ContinueLast` | none | all | Opens `claude://code/continue?session=last`, or `https://claude.ai/code`. |
| `Claude.Waiting` | none | touch buttons | Stateful key for Claude Code sessions. See below. |
| `Claude.Usage` | none | touch buttons | Image key with usage rings. See below. |

When nothing could be launched, a touch button briefly shows `Claude not found`.

### Waiting key

`Claude.Waiting` has three button states, so you choose the colours per state in
LoupixDeck:

| State | Meaning | Caption |
|---|---|---|
| `waiting` | At least one session is waiting on a permission prompt or a question | The project name; `N waiting` plus the first project name when several |
| `busy` | Sessions are working, none needs you | `Working` |
| `idle` | No session needs you | `Idle`, or `No sessions` |

A session that has merely finished its turn is `idle` and does not light the key.

Tap the key to focus the waiting session. With several waiting, each tap moves
to the next one. With nothing waiting it shows `Nothing waiting`.

The key reads the session files Claude Code writes to `~/.claude/sessions/<pid>.json`
(`status` is `busy`, `waiting` or `idle`). This covers the CLI, the VS Code
extension and the desktop app's Code tab. No hooks and no Claude Code
configuration are needed. Sessions whose process has exited are ignored, so a
crashed session never lights the key.

Focusing:

- **macOS.** The plugin walks up the process tree from the session's pid to the
  first app bundle (VS Code, Terminal, iTerm and so on) and activates it. For the
  Claude desktop app it opens `claude://code/needs-input`, which also selects the
  right tab.
- **Windows.** Only sessions started from the Claude desktop app are focused
  (via `claude://code/needs-input`). For anything else the key shows the session
  name instead of focusing a window.
- **Linux.** The key never focuses a window; it shows the session name.

### Usage key

`Claude.Usage` draws two rings: the outer ring is the 5-hour window, the inner
ring is the 7-day window. Rings are green normally, amber from 80 % and red from
95 %. The centre shows the 5-hour percentage, the 7-day percentage (`wk NN%`) and
the time until the 5-hour window resets. Tap to fetch now and show a short
overlay with both percentages and reset times.

**This is an undocumented endpoint.** The key calls
`https://api.anthropic.com/api/oauth/usage`, the same unofficial endpoint Claude
Code's `/usage` uses, with the Claude Code OAuth token. It may change or
disappear without notice, and it may rate-limit you. The response is parsed
leniently; a response without either window shows `Unexpected response`.

- The key polls every 5 minutes while it is in use.
- On HTTP 429 the poll backs off, doubling from 5 minutes up to a 30 minute cap,
  and returns to 5 minutes after a good response.
- On any failure the last good values stay on the key with a `! stale` marker where
  the reset time normally is. With no values yet the key shows a dash and a short error.
- A 401 or 403 shows `Sign in to Claude Code again`.

**Token lookup order:**

1. macOS only: the Keychain item `Claude Code-credentials`, read with the
   `security` command line tool.
2. `~/.claude/.credentials.json`, which Claude Code writes on Linux and Windows
   (on Windows `%USERPROFILE%\.claude\.credentials.json`).
3. The *Access token* setting.

On every OS the fix is the same: run `claude` once and sign in (`claude`, then
`/login`), and the token is found automatically. If the plugin says it cannot
find a sign-in, paste into the *Access token* field either the whole credentials
JSON (the `claudeAiOauth` object Claude Code stores) or the bare access token
(it starts with `sk-ant-`).

The token is held only in memory and is never logged. A pasted token is stored in
the plugin's `settings.json` like any other setting. Access tokens expire and
Claude Code refreshes them when it runs; the plugin does not refresh anything, so
a pasted token will need pasting again. Prefer the automatic lookup.

## Polling and cost

Both workers start on demand and stop when nobody is looking:

| Worker | Runs while | Stops after |
|---|---|---|
| Session watcher (file watcher plus a 10 s rescan) | A `Claude.Waiting` key is on the current page and *Alert me when Claude wants input* is on | 30 s without the key being rendered |
| Usage poller (every 5 min) | A `Claude.Usage` key is on the current page and *Show usage* is on | 12 min without the key being rendered |

Both toggles are on by default. With a LoupixDeck build that pauses display
polling while the device is off, both workers also stop when the device is off.
The launcher commands run no background work.

## Privacy

The only network traffic is the usage request to `api.anthropic.com`, made with
your Claude Code token, and only while a usage key is in use. Nothing else leaves
the machine. Session files in `~/.claude/sessions` are read locally and never
sent anywhere. Launcher commands open `claude://` links or claude.ai pages
through your system, which is the same as opening them yourself.

## Releasing and the Plugin Store

Packaging follows the SDK's
[Packaging & Distribution](https://github.com/RadiatorTwo/LoupixDeck.PluginSdk/blob/master/docs/Packaging-and-Distribution.md)
guide. `.github/workflows/release.yml` calls the SDK's reusable
`plugin-release.yml`; nothing else is needed in this repository.

1. Bump the version in three places so they match: `plugin.json` (`version`),
   `LoupixDeck.Plugin.Claude.csproj` (`<Version>`) and
   `ClaudePlugin.Metadata.Version`.
2. Push, then publish a GitHub Release whose tag is exactly `v<version>`, with
   release notes.
3. The workflow attaches `claude-<version>-any.zip`, `plugin.json` and
   `SHA256SUMS` to the release.
4. First listing, and every later release: open a pull request against
   [RadiatorTwo/LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck) that adds
   or updates the `claude` entry in `plugin-store.json` (`repository`
   `web-engineer/LoupixDeck.Plugin.Claude`, `commandPrefixes` `["Claude."]`,
   `minSdkVersion` `1.28.0`), with the `release` object from the workflow's job
   summary.

The repository must be public for the Store and the release download URLs to work.

## Logging

Plugin lines are prefixed `plugin:claude` in LoupixDeck's log
(`~/.config/LoupixDeck/loupixdeck-startup.log` on macOS and Linux). At startup
the plugin logs whether the Claude desktop app was found. Workers log when they
start and stop, a successful usage lookup logs the two percentages and the token
source (never the token), and a 429 is logged as a warning.
