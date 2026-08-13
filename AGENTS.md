# Codex Usage Tray

## Repository guide

- The app is a windowless .NET 8 WinForms tray application for Windows.
- Read `docs/PROJECT_CONTEXT.md` before architecture or data-source changes.
- Record durable tradeoffs in `docs/DECISIONS.md`; keep current work in `docs/NEXT.md`.
- Build with `dotnet build CodexUsageTray.sln`.
- Test with `dotnet test CodexUsageTray.sln`.
- Test the installer with `powershell -NoProfile -ExecutionPolicy Bypass -File tests/Installer.Tests.ps1 -PackagePath artifacts/win-x64/CodexUsageTray.exe` after publishing.
- Publish with `dotnet publish src/CodexUsageTray/CodexUsageTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64`.
- Keep background work event-driven. Do not add frequent polling or network calls without an explicit product decision.
- Never log or copy Codex session contents; extract only the rate-limit fields required by the UI.
- Keep installer actions per-user and uninstall only paths recorded in installer state or explicitly supplied by the user.
- Preserve unrelated user changes. Update these context files only for material changes.
