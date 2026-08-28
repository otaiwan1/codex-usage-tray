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
