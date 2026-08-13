# Codex Usage Tray

一個低耗能、無主視窗的 Windows tray App：

- tray icon 以放大的彩色文字直接顯示 Codex 7 天額度的剩餘百分比，背景透明。
- 滑鼠移到 icon 時顯示 reset 倒數、剩餘百分比、Credits 與最後回報時間。
- 只讀取 Codex 已寫入 `%USERPROFILE%\.codex\sessions` 的 rate-limit 事件。
- 使用檔案系統事件更新；沒有固定頻率輪詢，也不發送背景網路請求。

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

數值是 Codex 最近一次本機回報。若剛登入或從未執行 Codex，先啟動一次 Codex 工作，等待它產生 usage event，再按「立即重新讀取」。

## 開發

```powershell
dotnet build CodexUsageTray.sln
dotnet test CodexUsageTray.sln
dotnet publish src/CodexUsageTray/CodexUsageTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

發布結果位於 `artifacts\win-x64\CodexUsageTray.exe`。
