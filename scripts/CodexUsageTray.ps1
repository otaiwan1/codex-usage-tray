<#
.SYNOPSIS
安裝、設定、檢查或解除安裝 Codex Usage Tray。

.DESCRIPTION
以目前 Windows 使用者身分管理 Codex Usage Tray。Install 預設會建立監控程式、ChatGPT 的開機啟動捷徑與 Start Menu 捷徑；遠端下載時會驗證 GitHub Release 提供的 SHA-256。

.PARAMETER Action
Install、Configure、Status 或 Uninstall。

.PARAMETER MonitorAutoStart
控制監控程式的開機啟動。Default 在 Install 時代表 Enable，在其他 Action 代表 Keep。

.PARAMETER ChatGptAutoStart
控制 ChatGPT Desktop App 的開機啟動。Default 行為同 MonitorAutoStart。

.PARAMETER StartMenuShortcut
控制監控程式的 Start Menu 捷徑。

.PARAMETER PackagePath
使用本機 EXE 而不是從 GitHub Release 下載，適合開發與離線部署。

.EXAMPLE
.\CodexUsageTray.ps1 -Action Install

.EXAMPLE
.\CodexUsageTray.ps1 -Action Configure -ChatGptAutoStart Disable

.EXAMPLE
.\CodexUsageTray.ps1 -Action Uninstall
#>

#requires -Version 5.1

[CmdletBinding()]
param(
    [ValidateSet('Install', 'Configure', 'Status', 'Uninstall')]
    [string]$Action = 'Install',

    [ValidateSet('Default', 'Enable', 'Disable', 'Keep')]
    [string]$MonitorAutoStart = 'Default',

    [ValidateSet('Default', 'Enable', 'Disable', 'Keep')]
    [string]$ChatGptAutoStart = 'Default',

    [ValidateSet('Default', 'Enable', 'Disable', 'Keep')]
    [string]$StartMenuShortcut = 'Default',

    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs\CodexUsageTray'),
    [string]$PackagePath,
    [string]$PackageUri = 'https://github.com/otaiwan1/codex-usage-tray/releases/latest/download/CodexUsageTray.exe',
    [string]$ChecksumUri = 'https://github.com/otaiwan1/codex-usage-tray/releases/latest/download/CodexUsageTray.exe.sha256',
    [string]$ChatGptAppId,
    [string]$ChatGptStoreId = '9PLM9XGG6VKS',
    [switch]$SkipChecksum,
    [switch]$SkipChatGptInstall,
    [switch]$NoLaunch,

    [string]$StartupDirectory = [Environment]::GetFolderPath('Startup'),
    [string]$ProgramsDirectory = [Environment]::GetFolderPath('Programs'),
    [string]$StateDirectory = (Join-Path $env:LOCALAPPDATA 'CodexUsageTray')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$monitorShortcutName = 'Codex Usage Tray.lnk'
$chatGptShortcutName = 'ChatGPT (Codex Usage Tray).lnk'
$stateFileName = 'install-state.json'
$executableName = 'CodexUsageTray.exe'

function Get-FullSafePath {
    param([Parameter(Mandatory)][string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetPathRoot($fullPath)
    if ([string]::IsNullOrWhiteSpace($root) -or
        $fullPath.TrimEnd('\') -eq $root.TrimEnd('\')) {
        throw "拒絕使用磁碟根目錄作為安裝路徑：$fullPath"
    }

    return $fullPath.TrimEnd('\')
}

function Resolve-Setting {
    param(
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][string]$InstallDefault
    )

    if ($Value -ne 'Default') {
        return $Value
    }

    if ($Action -eq 'Install') {
        return $InstallDefault
    }

    return 'Keep'
}

function Get-State {
    $statePath = Join-Path $StateDirectory $stateFileName
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        return $null
    }

    return Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
}

function Save-State {
    param(
        [Parameter(Mandatory)][string]$InstalledPath,
        [Parameter(Mandatory)][string]$Sha256,
        [string]$ResolvedChatGptAppId
    )

    New-Item -ItemType Directory -Path $StateDirectory -Force | Out-Null
    $state = [ordered]@{
        schemaVersion = 1
        installDirectory = $InstalledPath
        executablePath = Join-Path $InstalledPath $executableName
        sha256 = $Sha256
        chatGptAppId = $ResolvedChatGptAppId
        updatedAt = [DateTimeOffset]::Now.ToString('o')
    }
    $state | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $StateDirectory $stateFileName) -Encoding UTF8
}

function Set-Shortcut {
    param(
        [Parameter(Mandatory)][string]$ShortcutPath,
        [Parameter(Mandatory)][string]$TargetPath,
        [string]$Arguments,
        [string]$WorkingDirectory,
        [string]$Description
    )

    New-Item -ItemType Directory -Path (Split-Path -Parent $ShortcutPath) -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($ShortcutPath)
        $shortcut.TargetPath = $TargetPath
        if ($Arguments) { $shortcut.Arguments = $Arguments }
        if ($WorkingDirectory) { $shortcut.WorkingDirectory = $WorkingDirectory }
        if ($Description) { $shortcut.Description = $Description }
        $shortcut.Save()
    }
    finally {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    }
}

function Set-ShortcutState {
    param(
        [Parameter(Mandatory)][string]$Setting,
        [Parameter(Mandatory)][string]$ShortcutPath,
        [Parameter(Mandatory)][scriptblock]$EnableAction
    )

    switch ($Setting) {
        'Enable' { & $EnableAction }
        'Disable' { Remove-Item -LiteralPath $ShortcutPath -Force -ErrorAction SilentlyContinue }
        'Keep' { }
        default { throw "不支援的捷徑設定：$Setting" }
    }
}

function Get-ChatGptAppId {
    if ($ChatGptAppId) {
        return $ChatGptAppId
    }

    $startApp = Get-StartApps | Where-Object { $_.Name -eq 'ChatGPT' } | Select-Object -First 1
    if ($startApp) {
        return $startApp.AppID
    }

    return $null
}

function Ensure-ChatGptAppId {
    $appId = Get-ChatGptAppId
    if ($appId) {
        return $appId
    }

    if ($SkipChatGptInstall) {
        throw '找不到 ChatGPT Desktop App；請先安裝，或移除 -SkipChatGptInstall 讓 Script 透過 Microsoft Store 安裝。'
    }

    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw '找不到 winget，無法自動安裝 ChatGPT Desktop App。'
    }

    Write-Host '正在透過 Microsoft Store 安裝 ChatGPT Desktop App...'
    & $winget.Source install --id $ChatGptStoreId --exact --source msstore `
        --accept-source-agreements --accept-package-agreements --silent --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        throw "ChatGPT Desktop App 安裝失敗，winget exit code: $LASTEXITCODE"
    }

    $appId = Get-ChatGptAppId
    if (-not $appId) {
        # Current stable Store package. Get-StartApps can lag briefly after installation.
        $appId = 'OpenAI.Codex_2p2nqsd0c76g0!App'
    }

    return $appId
}

function Stop-InstalledMonitor {
    param([Parameter(Mandatory)][string]$ExecutablePath)

    Get-Process -Name 'CodexUsageTray' -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            if ([string]::Equals($_.Path, $ExecutablePath, [StringComparison]::OrdinalIgnoreCase)) {
                Stop-Process -Id $_.Id
                Wait-Process -Id $_.Id -Timeout 10 -ErrorAction SilentlyContinue
            }
        }
        catch {
            # Ignore a process that exits while being inspected.
        }
    }
}

function Copy-PackageWithRetry {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    $maximumAttempts = 20
    for ($attempt = 1; $attempt -le $maximumAttempts; $attempt++) {
        try {
            Copy-Item -LiteralPath $Source -Destination $Destination -Force
            return
        }
        catch [IO.IOException] {
            if ($attempt -eq $maximumAttempts) {
                throw
            }

            Start-Sleep -Milliseconds 250
        }
    }
}

function Test-MonitorRunning {
    param([Parameter(Mandatory)][string]$ExecutablePath)

    foreach ($process in (Get-Process -Name 'CodexUsageTray' -ErrorAction SilentlyContinue)) {
        try {
            if ([string]::Equals($process.Path, $ExecutablePath, [StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
        catch {
            # Ignore a process that exits while being inspected.
        }
    }

    return $false
}

function Get-DownloadedPackage {
    param([Parameter(Mandatory)][string]$TemporaryDirectory)

    if ($PackagePath) {
        $resolved = (Resolve-Path -LiteralPath $PackagePath).Path
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "找不到 package：$resolved"
        }
        return $resolved
    }

    $downloadPath = Join-Path $TemporaryDirectory $executableName
    Write-Host "正在下載 $PackageUri"
    Invoke-WebRequest -Uri $PackageUri -OutFile $downloadPath -UseBasicParsing

    if (-not $SkipChecksum) {
        $checksumPath = Join-Path $TemporaryDirectory "$executableName.sha256"
        Invoke-WebRequest -Uri $ChecksumUri -OutFile $checksumPath -UseBasicParsing
        $checksumText = Get-Content -LiteralPath $checksumPath -Raw
        $match = [regex]::Match($checksumText, '(?i)\b[0-9a-f]{64}\b')
        if (-not $match.Success) {
            throw '下載的 SHA-256 檔案格式不正確。'
        }

        $actual = (Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash
        if (-not [string]::Equals($actual, $match.Value, [StringComparison]::OrdinalIgnoreCase)) {
            throw '下載的執行檔 SHA-256 不符，已停止安裝。'
        }
    }

    return $downloadPath
}

function Show-Status {
    $state = Get-State
    $resolvedDirectory = if ($state) { [string]$state.installDirectory } else { Get-FullSafePath $InstallDirectory }
    $executablePath = Join-Path $resolvedDirectory $executableName
    [pscustomobject]@{
        Installed = Test-Path -LiteralPath $executablePath -PathType Leaf
        InstallDirectory = $resolvedDirectory
        MonitorAutoStart = Test-Path -LiteralPath (Join-Path $StartupDirectory $monitorShortcutName)
        ChatGptAutoStart = Test-Path -LiteralPath (Join-Path $StartupDirectory $chatGptShortcutName)
        StartMenuShortcut = Test-Path -LiteralPath (Join-Path $ProgramsDirectory $monitorShortcutName)
        Running = Test-MonitorRunning $executablePath
        ChatGptAppId = Get-ChatGptAppId
    }
}

if ($env:OS -ne 'Windows_NT') {
    throw '此安裝程式僅支援 Windows。'
}

$existingState = Get-State
if ($existingState -and -not $PSBoundParameters.ContainsKey('InstallDirectory')) {
    $InstallDirectory = [string]$existingState.installDirectory
}
$InstallDirectory = Get-FullSafePath $InstallDirectory
$StateDirectory = Get-FullSafePath $StateDirectory

$monitorSetting = Resolve-Setting $MonitorAutoStart 'Enable'
$chatGptSetting = Resolve-Setting $ChatGptAutoStart 'Enable'
$startMenuSetting = Resolve-Setting $StartMenuShortcut 'Enable'
$installedExecutable = Join-Path $InstallDirectory $executableName
$startupMonitorShortcut = Join-Path $StartupDirectory $monitorShortcutName
$startupChatGptShortcut = Join-Path $StartupDirectory $chatGptShortcutName
$startMenuMonitorShortcut = Join-Path $ProgramsDirectory $monitorShortcutName

switch ($Action) {
    'Status' {
        Show-Status | Format-List
        break
    }

    'Uninstall' {
        Stop-InstalledMonitor $installedExecutable
        Remove-Item -LiteralPath $startupMonitorShortcut -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $startupChatGptShortcut -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $startMenuMonitorShortcut -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $installedExecutable -Force -ErrorAction SilentlyContinue

        if ((Test-Path -LiteralPath $InstallDirectory -PathType Container) -and
            -not (Get-ChildItem -LiteralPath $InstallDirectory -Force | Select-Object -First 1)) {
            Remove-Item -LiteralPath $InstallDirectory -Force
        }

        Remove-Item -LiteralPath (Join-Path $StateDirectory $stateFileName) -Force -ErrorAction SilentlyContinue
        if ((Test-Path -LiteralPath $StateDirectory -PathType Container) -and
            -not (Get-ChildItem -LiteralPath $StateDirectory -Force | Select-Object -First 1)) {
            Remove-Item -LiteralPath $StateDirectory -Force
        }

        Write-Host 'Codex Usage Tray 已解除安裝；ChatGPT Desktop App 本身未被移除。'
        break
    }

    'Configure' {
        if (-not (Test-Path -LiteralPath $installedExecutable -PathType Leaf)) {
            throw '尚未安裝 Codex Usage Tray，請先使用 -Action Install。'
        }

        $resolvedChatGptAppId = Get-ChatGptAppId
        if ($chatGptSetting -eq 'Enable') {
            $resolvedChatGptAppId = Ensure-ChatGptAppId
        }

        Set-ShortcutState $monitorSetting $startupMonitorShortcut {
            Set-Shortcut $startupMonitorShortcut $installedExecutable $null $InstallDirectory '顯示 Codex 7d 剩餘額度'
        }
        Set-ShortcutState $chatGptSetting $startupChatGptShortcut {
            Set-Shortcut $startupChatGptShortcut (Join-Path $env:WINDIR 'explorer.exe') "shell:AppsFolder\$resolvedChatGptAppId" $null '登入 Windows 時啟動 ChatGPT'
        }
        Set-ShortcutState $startMenuSetting $startMenuMonitorShortcut {
            Set-Shortcut $startMenuMonitorShortcut $installedExecutable $null $InstallDirectory '顯示 Codex 7d 剩餘額度'
        }

        $hash = (Get-FileHash -LiteralPath $installedExecutable -Algorithm SHA256).Hash
        Save-State $InstallDirectory $hash $resolvedChatGptAppId
        Show-Status | Format-List
        break
    }

    'Install' {
        $temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) "CodexUsageTray-$([Guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
        try {
            $sourcePackage = Get-DownloadedPackage $temporaryDirectory
            $sourceHash = (Get-FileHash -LiteralPath $sourcePackage -Algorithm SHA256).Hash
            New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
            Stop-InstalledMonitor $installedExecutable
            Copy-PackageWithRetry $sourcePackage $installedExecutable

            $resolvedChatGptAppId = Get-ChatGptAppId
            if ($chatGptSetting -eq 'Enable') {
                $resolvedChatGptAppId = Ensure-ChatGptAppId
            }

            Set-ShortcutState $monitorSetting $startupMonitorShortcut {
                Set-Shortcut $startupMonitorShortcut $installedExecutable $null $InstallDirectory '顯示 Codex 7d 剩餘額度'
            }
            Set-ShortcutState $chatGptSetting $startupChatGptShortcut {
                Set-Shortcut $startupChatGptShortcut (Join-Path $env:WINDIR 'explorer.exe') "shell:AppsFolder\$resolvedChatGptAppId" $null '登入 Windows 時啟動 ChatGPT'
            }
            Set-ShortcutState $startMenuSetting $startMenuMonitorShortcut {
                Set-Shortcut $startMenuMonitorShortcut $installedExecutable $null $InstallDirectory '顯示 Codex 7d 剩餘額度'
            }

            Save-State $InstallDirectory $sourceHash $resolvedChatGptAppId
            if (-not $NoLaunch) {
                Start-Process -FilePath $installedExecutable -WorkingDirectory $InstallDirectory -WindowStyle Hidden
            }
        }
        finally {
            if (Test-Path -LiteralPath $temporaryDirectory -PathType Container) {
                Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
            }
        }

        Show-Status | Format-List
        break
    }
}
