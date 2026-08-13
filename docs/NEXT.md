# Current state and next work

## Current state

- The tray application and per-user lifecycle installer are implemented for `win-x64`.
- The application reads the latest seven-day event, renders the percentage icon, and updates from filesystem events.

## Verification target

- Parser and reader tests cover seven-day window selection, clamping, credits, malformed input, partial writes, and newest-report selection.
- Keep Release builds warning-free and preserve the self-contained single-file publish.
- Repeat a launch/idle/exit smoke test after tray lifecycle changes.
- Run the isolated installer lifecycle test after installer changes.

## Known follow-up

- Confirm the larger transparent-background icon on the user's actual Windows scaling and taskbar theme.
- If the Codex event schema changes, update the parser fixture and integration boundary documentation together.
