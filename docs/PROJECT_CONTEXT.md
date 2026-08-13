# Project context

## Purpose

Codex Usage Tray is a low-overhead Windows tray application that shows the remaining Codex seven-day usage percentage directly in its icon. Hover text shows the reset countdown, available percentage, credits, and the time of the last Codex report.

## Architecture

- `src/CodexUsageTray`: windowless WinForms application and tray UI.
- `CodexUsageLogReader`: reads only recent tails of local Codex session JSONL files and extracts `event_msg` / `token_count` / `rate_limits` data.
- `UsageFileWatcher`: uses `FileSystemWatcher` plus a short debounce to react to Codex writes without periodic polling.
- `TrayIconRenderer`: renders a small percentage icon using Windows GDI+.
- `scripts/CodexUsageTray.ps1`: per-user install, startup configuration, status, and uninstall entry point.
- `.github/workflows/release.yml`: builds and attaches the self-contained EXE and checksum to version-tag releases.
- `tests/CodexUsageTray.Tests`: parser, selection, and formatting tests.

## Data boundary

The application reads `%USERPROFILE%\.codex\sessions\**\*.jsonl`. It does not read `auth.json`, use browser cookies, call an undocumented service, or transmit session contents. The local JSONL schema is an integration boundary owned by Codex and may change in a future Codex release.

The seven-day window is identified by `window_minutes == 10080`. Remaining percentage is `100 - used_percent`, clamped to 0-100. Credits are displayed separately because they are not the same as the percentage-based allowance.

## Runtime behavior

- Initial launch scans a bounded number of newest files and at most a bounded tail of each file.
- Normal updates are filesystem-event driven.
- The reset countdown is formatted only when the user hovers over the icon.
- No visible main window and no background network request are used.

## Installation boundary

The installer copies one executable into `%LOCALAPPDATA%\Programs\CodexUsageTray`, stores non-sensitive install state in `%LOCALAPPDATA%\CodexUsageTray`, and manages explicitly named shortcuts in the current user's Startup and Start Menu folders. It never removes the ChatGPT Desktop App. When requested and missing, ChatGPT is installed from Microsoft Store product `9PLM9XGG6VKS` through `winget`.
