# Decisions

## D-001: Read the local Codex rate-limit event stream (superseded by D-005)

**Context:** The product needs the account's seven-day percentage and reset time. Official public documentation does not establish a supported account-usage REST endpoint for this UI state.

**Decision:** Read the narrow `rate_limits` object from recent local Codex session JSONL events. Never retain, display, or transmit other event contents.

**Rationale:** This reuses data already written by Codex, requires no additional authentication, and supports event-driven updates with no recurring network cost.

**Consequences:** Values are last-known rather than independently refreshed, and a future Codex schema change may require a parser update. The tooltip exposes the last report time so staleness is visible.

## D-005: Prefer the account-level Codex app-server snapshot

**Context:** Local session JSONL does not change while the user drives Codex sessions on remote SSH hosts, so its last-known percentage can lag behind Codex Desktop.

**Decision:** Use the documented local `codex app-server` initialization handshake and read-only `account/rateLimits/read` method as the primary source. Prefer `rateLimitsByLimitId.codex`; use the legacy single-bucket response only when it is unlabelled or labelled `codex`. Keep JSONL filesystem events as a low-latency local update and offline fallback.

**Rationale:** The app-server returns the current account snapshot shared across local and remote sessions without reading credentials or session content. A short-lived request at startup, left-click/context-menu manual refresh, stale hover, and a 15-minute background interval bounds CPU and network use.

**Consequences:** Live refresh requires an accessible local `codex.exe` and connectivity. When unavailable, the UI remains functional with last-known JSONL data. The executable is rediscovered on every refresh so startup ordering with ChatGPT Desktop does not permanently disable the live source.

## D-006: Keep both usage windows in one snapshot

**Context:** Codex now exposes distinct five-hour and seven-day usage windows, while the tray icon has room for only one percentage at a time.

**Decision:** Parse both `300`-minute and `10080`-minute windows into the same snapshot. Default the icon to seven days; on left-click, switch its selected period immediately, identify it with a `5` or `7` badge, and attempt the existing bounded account refresh. Keep the hover tooltip independent of that selection and always list both windows with their reset details on separate lines.

**Rationale:** A view-only switch avoids extra polling or duplicate app-server requests while keeping both limits quickly accessible and visually distinguishable.

**Consequences:** Older fallback events that contain only one supported window show the other period as unavailable until a snapshot containing it is read.

## D-007: Isolate optional monitored accounts and simplify single-window UI

**Context:** The local Codex login can be Plus while the user's remote sessions use a different Pro account. A Pro response may contain only a seven-day window. Reusing local session fallback in separate mode could silently display the wrong account.

**Decision:** Default to the local Codex home. Let installation select a separate, user-owned `CODEX_HOME` and authenticate it with Codex CLI device-auth. Force file-based credential storage for both the separate login and app-server child process so Windows keyring state cannot cross profiles; never copy or inspect credentials, and disable local JSONL fallback in separate mode. Show period badges and left-click toggling only when both windows exist; a seven-day-only response uses the plain percentage icon and a concise tooltip.

**Consequences:** Separate mode needs Codex CLI and one interactive login. If its account refresh fails, the monitor shows no new value rather than falling back to local Plus. Uninstall preserves the separate login directory, which users can remove explicitly after confirming it is no longer needed.

## D-008: Use a transparent icon with a period ribbon and condensed numerals

**Context:** The corner `5` or `7` badge obscured the last digit of the percentage. A dark tile and bottom ribbon made the numerals too small for the Windows taskbar.

**Decision:** Keep the icon background transparent. When both usage windows exist, draw the selected `5h` or `7d` in a colored ribbon below the percentage. Use Bahnschrift SemiBold Condensed for the percentage and preserve its aspect ratio so two digits can occupy more height without stretching; fall back to Segoe UI if the font is unavailable. Omit the ribbon for a single-window response.

**Consequences:** The period marker no longer covers the percentage. The ribbon text remains small at taskbar size, with its color and the tooltip providing additional period cues.

## D-009: Render at the notification area's native size and follow its theme

**Context:** Windows reports a 16-pixel small icon on the current display, while the tray rendered only a 32-pixel icon. Scaling the narrow numerals down could weaken or remove individual pixels. An actual taskbar screenshot still showed broken-looking `64` after native-size vector rendering. [Battery Percentage Icon 2](https://github.com/soleon/Percentage) demonstrates a simpler 16-pixel text icon using Microsoft Sans Serif and a theme-based foreground color.

**Decision:** Render the icon at `SystemInformation.SmallIconSize` and use `SystemUsesLightTheme` to choose a light foreground for a dark taskbar or a dark foreground for a light taskbar when allowance is healthy; retain amber and red for low allowance. Render one- and two-digit percentages at every icon size with hinted Microsoft Sans Serif text through a grayscale coverage mask instead of vector paths. Keep the condensed vector font for three digits, and use a compact period color strip without tiny text at sizes up to 20 pixels. React to Windows user-preference and display-setting events, and also read the current settings on each normal display update.

**Consequences:** The taskbar no longer needs to shrink a 32-pixel image to 16 pixels, and small numerals use the system's hinted text rasterization. Three-digit numbers retain a narrower font to fit. The `5h`/`7d` text appears inside the ribbon at larger icon sizes; on smaller icons, the color strip and context-menu status identify the selected period. Theme changes require no additional polling.

## D-002: Use a windowless .NET 8 WinForms application

**Context:** The application is Windows-only and should consume minimal resources.

**Decision:** Use `ApplicationContext`, `NotifyIcon`, `FileSystemWatcher`, and generated GDI+ icons. Do not host a browser engine or create a main window.

**Consequences:** The release can be published as a self-contained single EXE. The project remains Windows-specific by design.

## D-003: Use per-user shortcuts for installation and startup

**Context:** Installation, startup settings, configuration changes, and uninstall should work on personal Windows laptops without elevation.

**Decision:** Install under `%LOCALAPPDATA%` and manage narrowly named Startup and Start Menu `.lnk` files. Launch the packaged ChatGPT app through its AUMID instead of its versioned WindowsApps path.

**Rationale:** This avoids administrator privileges and remains stable when the Microsoft Store updates ChatGPT in place.

**Consequences:** Settings apply only to the current Windows user. Uninstall removes the monitor and managed shortcuts but intentionally leaves ChatGPT installed.

## D-004: Distribute a checksummed self-contained release

**Context:** Other laptops should not need a .NET SDK or runtime preinstalled.

**Decision:** GitHub version tags publish a self-contained `win-x64` EXE and a SHA-256 file. The installer verifies the checksum before installing remote packages.

**Consequences:** The release download is larger than a framework-dependent build, but installation has fewer prerequisites.
