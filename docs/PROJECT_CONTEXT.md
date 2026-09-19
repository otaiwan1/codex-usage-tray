# Project context

## Purpose

Codex Usage Tray is a low-overhead Windows tray application that shows the remaining Codex five-hour or seven-day usage percentage directly in its icon. If both windows exist, left-click switches between them and refreshes; if only seven-day exists, the icon has no period badge and left-click only refreshes. Hover text shows the available windows, reset countdowns, earned reset count, and last refresh time.

## Architecture

- `src/CodexUsageTray`: windowless WinForms application and tray UI.
- `CodexAccountRateLimitReader`: launches a short-lived local `codex app-server`, performs the documented initialization handshake, and reads the account-level rate-limit snapshot.
- `MonitorSettings`: selects either the default local `%USERPROFILE%\.codex` account or a configured isolated `CODEX_HOME` account.
- `CodexAccountRateLimitParser`: selects `rateLimitsByLimitId.codex`, with the compatible single-bucket response as fallback, then extracts the five-hour and seven-day windows plus the reset-credit count.
- `CodexUsageLogReader`: reads only recent tails of local Codex session JSONL files as an offline fallback and for immediate local-session updates.
- `UsageFileWatcher`: uses `FileSystemWatcher` plus a short debounce to react to Codex writes without periodic polling.
- `TrayIconRenderer`: renders a small percentage icon using Windows GDI+.
- `scripts/CodexUsageTray.ps1`: per-user install, startup configuration, status, and uninstall entry point.
- `.github/workflows/release.yml`: builds and attaches the self-contained EXE and checksum to version-tag releases.
- `tests/CodexUsageTray.Tests`: parser, selection, and formatting tests.

## Data boundary

The primary source is the documented, read-only `account/rateLimits/read` method exposed by the user's local Codex app-server. The monitor neither reads nor copies authentication material; the Codex process uses its existing login and returns only structured account-limit fields. Local-account mode also reads `%USERPROFILE%\.codex\sessions\**\*.jsonl` as a fallback. Separate-account mode sets an isolated `CODEX_HOME` for app-server and never uses local session fallback. It never retains, logs, or transmits session contents.

The five-hour and seven-day windows are identified by `window_minutes == 300` and `window_minutes == 10080`, respectively. Remaining percentage is `100 - used_percent`, clamped to 0-100. Credits are displayed separately because they are not the same as the percentage-based allowance.

## Runtime behavior

- Initial launch scans a bounded number of newest files and at most a bounded tail of each file.
- Local-session updates are filesystem-event driven in local-account mode only.
- An account refresh runs at startup, at most once when hover data is older than two minutes, every 15 minutes while idle, or on left-click/context-menu manual refresh. Left-click also switches the selected five-hour/seven-day period before attempting the refresh. Each app-server process is terminated after one response.
- Reset countdowns are formatted only when the user hovers over the icon; the tooltip lists only windows present in the account response.
- Tooltip update timestamps are labelled and include seconds.
- No visible main window or high-frequency polling is used.

## Installation boundary

The installer copies one executable into `%LOCALAPPDATA%\Programs\CodexUsageTray`, stores non-sensitive install state and account-source settings in `%LOCALAPPDATA%\CodexUsageTray`, and manages explicitly named shortcuts in the current user's Startup and Start Menu folders. Its optional separate-account mode runs `codex login --device-auth` with an isolated `CODEX_HOME`; it does not copy credentials or remove that login on uninstall. It never removes the ChatGPT Desktop App. When requested and missing, ChatGPT is installed from Microsoft Store product `9PLM9XGG6VKS` through `winget`.
