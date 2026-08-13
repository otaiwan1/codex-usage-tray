#requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Join-Path ([IO.Path]::GetTempPath()) "CodexUsageTrayInstallerTests-$([Guid]::NewGuid().ToString('N'))"
$installDirectory = Join-Path $root 'install'
$startupDirectory = Join-Path $root 'startup'
$programsDirectory = Join-Path $root 'programs'
$stateDirectory = Join-Path $root 'state'
$installer = Join-Path $PSScriptRoot '..\scripts\CodexUsageTray.ps1'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

try {
    & $installer -Action Install -PackagePath $PackagePath -NoLaunch `
        -InstallDirectory $installDirectory -StartupDirectory $startupDirectory `
        -ProgramsDirectory $programsDirectory -StateDirectory $stateDirectory

    Assert-True (Test-Path -LiteralPath (Join-Path $installDirectory 'CodexUsageTray.exe')) 'executable installed'
    Assert-True (Test-Path -LiteralPath (Join-Path $startupDirectory 'Codex Usage Tray.lnk')) 'monitor startup shortcut created'
    Assert-True (Test-Path -LiteralPath (Join-Path $startupDirectory 'ChatGPT (Codex Usage Tray).lnk')) 'ChatGPT startup shortcut created'
    Assert-True (Test-Path -LiteralPath (Join-Path $programsDirectory 'Codex Usage Tray.lnk')) 'Start Menu shortcut created'
    Assert-True (Test-Path -LiteralPath (Join-Path $stateDirectory 'install-state.json')) 'state created'

    $statusText = (& $installer -Action Status -InstallDirectory $installDirectory `
        -StartupDirectory $startupDirectory -ProgramsDirectory $programsDirectory -StateDirectory $stateDirectory | Out-String)
    Assert-True ($statusText -match 'Running\s+: False') 'status is scoped to the installed executable path'

    & $installer -Action Configure -MonitorAutoStart Disable -ChatGptAutoStart Disable -StartMenuShortcut Disable `
        -InstallDirectory $installDirectory -StartupDirectory $startupDirectory `
        -ProgramsDirectory $programsDirectory -StateDirectory $stateDirectory

    Assert-True (-not (Test-Path -LiteralPath (Join-Path $startupDirectory 'Codex Usage Tray.lnk'))) 'monitor startup shortcut removed'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $startupDirectory 'ChatGPT (Codex Usage Tray).lnk'))) 'ChatGPT startup shortcut removed'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $programsDirectory 'Codex Usage Tray.lnk'))) 'Start Menu shortcut removed'

    & $installer -Action Uninstall -InstallDirectory $installDirectory `
        -StartupDirectory $startupDirectory -ProgramsDirectory $programsDirectory -StateDirectory $stateDirectory

    Assert-True (-not (Test-Path -LiteralPath (Join-Path $installDirectory 'CodexUsageTray.exe'))) 'executable removed'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $stateDirectory 'install-state.json'))) 'state removed'
    Write-Host 'Installer integration tests passed.'
}
finally {
    if (Test-Path -LiteralPath $root -PathType Container) {
        $resolvedRoot = [IO.Path]::GetFullPath($root)
        $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if ($resolvedRoot.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $resolvedRoot) -like 'CodexUsageTrayInstallerTests-*') {
            Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
        }
    }
}
