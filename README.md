# Codex Usage Tray

一個低耗能、無主視窗的 Windows tray App：

- tray icon 以放大的彩色文字直接顯示 Codex 7 天額度的剩餘百分比，背景透明。
- 滑鼠移到 icon 時顯示 reset 倒數、剩餘百分比、可用重置次數與最後更新時間。
- 透過本機 `codex app-server` 的唯讀 `account/rateLimits/read` 取得帳號最新值，因此在本機操作 remote SSH session 也能更新。
- 保留 `%USERPROFILE%\.codex\sessions` 的 rate-limit 事件作為即時本機更新與離線備援。
- 背景每 15 分鐘才同步一次；滑鼠 hover 時僅在資料超過 2 分鐘未同步才查詢，沒有高頻輪詢。

## 一鍵安裝

在 PowerShell 執行：

```powershell
$installer = Join-Path $env:TEMP 'CodexUsageTray.ps1'
Invoke-WebRequest 'https://raw.githubusercontent.com/otaiwan1/codex-usage-tray/main/scripts/CodexUsageTray.ps1' -OutFile $installer
powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Action Install
```

預設會：

- 安裝到 `%LOCALAPPDATA%\Programs\CodexUsageTray`。
- 建立監控程式與 ChatGPT Desktop App 的開機啟動捷徑。
- 建立 Start Menu 捷徑並立即啟動監控程式。
- 若缺少 ChatGPT Desktop App，透過 Microsoft Store 安裝正式版。
- 從 GitHub Release 下載 self-contained EXE 並驗證 SHA-256。

## 設定與解除安裝

```powershell
# 查看目前狀態
powershell -ExecutionPolicy Bypass -File .\scripts\CodexUsageTray.ps1 -Action Status

# 關閉 ChatGPT 開機啟動，但保留監控程式開機啟動
powershell -ExecutionPolicy Bypass -File .\scripts\CodexUsageTray.ps1 -Action Configure -ChatGptAutoStart Disable

# 關閉監控程式開機啟動
powershell -ExecutionPolicy Bypass -File .\scripts\CodexUsageTray.ps1 -Action Configure -MonitorAutoStart Disable

# 重新開啟兩者的開機啟動
powershell -ExecutionPolicy Bypass -File .\scripts\CodexUsageTray.ps1 -Action Configure -MonitorAutoStart Enable -ChatGptAutoStart Enable

# 解除安裝監控程式與它管理的捷徑；不會解除安裝 ChatGPT
powershell -ExecutionPolicy Bypass -File .\scripts\CodexUsageTray.ps1 -Action Uninstall
```

`Install` 亦支援 `-InstallDirectory`、`-PackagePath`、`-NoLaunch`、`-SkipChatGptInstall` 等參數；使用 `Get-Help .\scripts\CodexUsageTray.ps1 -Detailed` 可查看參數。

## 直接使用

執行 `CodexUsageTray.exe` 後，程式只會出現在 system tray。右鍵可立即重新讀取或結束程式。

「立即重新讀取」會先向本機 Codex app-server 讀取目前登入帳號的最新額度；若 Codex 執行檔或網路暫時無法使用，才回退到最近一次本機 session event。可用環境變數 `CODEX_USAGE_TRAY_CODEX_EXE` 指定其他 `codex.exe` 路徑。

## 開發

```powershell
dotnet build CodexUsageTray.sln
dotnet test CodexUsageTray.sln
dotnet publish src/CodexUsageTray/CodexUsageTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

發布結果位於 `artifacts\win-x64\CodexUsageTray.exe`。
