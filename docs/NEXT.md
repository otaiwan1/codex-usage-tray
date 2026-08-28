# Current state and next work

## Current state

- The tray application and per-user lifecycle installer are implemented for `win-x64`.
- The application reads the current account-level five-hour and seven-day windows through Codex app-server, renders the selected percentage with a `5` or `7` icon badge, and keeps filesystem events as a fast fallback.
- Left-click switches the selected icon period immediately and attempts a refresh; stale hover refresh remains enabled, and the multiline tooltip always lists both periods with their reset details.
- Version `1.0.3` labels the tooltip update time with seconds and adds immediate left-click refresh.

## Verification target

- Parser and reader tests cover app-server bucket selection, five-hour/seven-day window selection, reset credits, nullable reset times, clamping, malformed input, partial writes, newest-report selection, dual-period multiline tooltips, and period icon badges.
- Keep Release builds warning-free and preserve the self-contained single-file publish.
- Repeat a launch/idle/exit smoke test after tray lifecycle changes.
- Run the isolated installer lifecycle test after installer changes.

## Known follow-up

- Confirm the larger transparent-background icon on the user's actual Windows scaling and taskbar theme.
- If the Codex app-server or event schema changes, update the relevant parser fixtures and integration boundary documentation together.
