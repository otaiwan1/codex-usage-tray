# Project context

## Purpose

Codex Usage Tray is a low-overhead Windows tray application that shows the remaining Codex seven-day usage percentage directly in its icon. Hover text shows the reset countdown, available percentage, earned reset count, and the time of the last refresh.

## Architecture

- `src/CodexUsageTray`: windowless WinForms application and tray UI.
- `CodexAccountRateLimitReader`: launches a short-lived local `codex app-server`, performs the documented initialization handshake, and reads the account-level rate-limit snapshot.
- `CodexAccountRateLimitParser`: selects `rateLimitsByLimitId.codex`, with the compatible single-bucket response as fallback, then extracts only the seven-day window and reset-credit count.
- `CodexUsageLogReader`: reads only recent tails of local Codex session JSONL files as an offline fallback and for immediate local-session updates.
- `UsageFileWatcher`: uses `FileSystemWatcher` plus a short debounce to react to Codex writes without periodic polling.
- `TrayIconRenderer`: renders a small percentage icon using Windows GDI+.
- `scripts/CodexUsageTray.ps1`: per-user install, startup configuration, status, and uninstall entry point.
- `.github/workflows/release.yml`: builds and attaches the self-contained EXE and checksum to version-tag releases.
- `tests/CodexUsageTray.Tests`: parser, selection, and formatting tests.

## Data boundary

The primary source is the documented, read-only `account/rateLimits/read` method exposed by the user's local Codex app-server. The monitor neither reads nor copies authentication material; the Codex process uses its existing login and returns only structured account-limit fields. The application also reads `%USERPROFILE%\.codex\sessions\**\*.jsonl` as a fallback. It never retains, logs, or transmits session contents.

The seven-day window is identified by `window_minutes == 10080`. Remaining percentage is `100 - used_percent`, clamped to 0-100. Credits are displayed separately because they are not the same as the percentage-based allowance.

## Runtime behavior

- Initial launch scans a bounded number of newest files and at most a bounded tail of each file.
- Local-session updates are filesystem-event driven.
- An account refresh runs at startup, at most once when hover data is older than two minutes, every 15 minutes while idle, or on manual refresh. Each app-server process is terminated after one response.
- The reset countdown is formatted only when the user hovers over the icon.
- No visible main window or high-frequency polling is used.

## Installation boundary

The installer copies one executable into `%LOCALAPPDATA%\Programs\CodexUsageTray`, stores non-sensitive install state in `%LOCALAPPDATA%\CodexUsageTray`, and manages explicitly named shortcuts in the current user's Startup and Start Menu folders. It never removes the ChatGPT Desktop App. When requested and missing, ChatGPT is installed from Microsoft Store product `9PLM9XGG6VKS` through `winget`.
