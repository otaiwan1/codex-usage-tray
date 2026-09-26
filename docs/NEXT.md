# Current state and next work

## Current state

- The tray application and per-user lifecycle installer are implemented for `win-x64`.
- The application defaults to the local Codex account; installation can opt into a separate device-auth account using an isolated `CODEX_HOME`. Local filesystem fallback is disabled for the separate account to prevent cross-account data.
- Both windows present: the icon has a colored `5h` or `7d` ribbon at larger icon sizes, or a compact color strip at small taskbar sizes; left-click toggles the period while refreshing. Seven-day-only: the icon has no ribbon and left-click only refreshes. Hover lists only available windows.
- The icon renders at Windows' small-icon size. Healthy allowance uses theme-neutral text; low allowance remains amber or red. One- and two-digit values use hinted text at every icon size so higher Windows scaling does not switch back to the narrow vector font.
- Version `1.0.4` adds account-source selection and single-window presentation.

## Verification target

- Parser and reader tests cover app-server bucket selection, five-hour/seven-day window selection, reset credits, nullable reset times, clamping, malformed input, partial writes, newest-report selection, dual-period multiline tooltips, and period icon ribbons.
- Keep Release builds warning-free and preserve the self-contained single-file publish.
- Repeat a launch/idle/exit smoke test after tray lifecycle changes.
- Run the isolated installer lifecycle test after installer changes.

## Known follow-up

- Confirm the native-size, theme-aware icon on the user's actual taskbar after installation, especially the 16-pixel numerals.
- If the Codex app-server or event schema changes, update the relevant parser fixtures and integration boundary documentation together.
