# Current state and next work

## Current state

- The tray application and per-user lifecycle installer are implemented for `win-x64`.
- The application defaults to the local Codex account; installation can opt into a separate device-auth account using an isolated `CODEX_HOME`. Local filesystem fallback is disabled for the separate account to prevent cross-account data.
- Both windows present: the icon has a `5` or `7` badge and left-click toggles the period while refreshing. Seven-day-only: the icon has no badge and left-click only refreshes. Hover lists only available windows.
- Version `1.0.4` adds account-source selection and single-window presentation.

## Verification target

- Parser and reader tests cover app-server bucket selection, five-hour/seven-day window selection, reset credits, nullable reset times, clamping, malformed input, partial writes, newest-report selection, dual-period multiline tooltips, and period icon badges.
- Keep Release builds warning-free and preserve the self-contained single-file publish.
- Repeat a launch/idle/exit smoke test after tray lifecycle changes.
- Run the isolated installer lifecycle test after installer changes.

## Known follow-up

- Confirm the larger transparent-background icon on the user's actual Windows scaling and taskbar theme.
- If the Codex app-server or event schema changes, update the relevant parser fixtures and integration boundary documentation together.
