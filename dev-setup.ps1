#Requires -Version 5.1
<#
.SYNOPSIS
    One-time developer setup for ChecksumVerify.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "`nChecksumVerify — Developer Setup`n" -ForegroundColor Cyan

# .NET 10 SDK
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "[!] .NET SDK not found. Installing .NET 10 via winget..." -ForegroundColor Yellow
    winget install Microsoft.DotNet.SDK.10 --silent --accept-package-agreements
} else {
    $version = (dotnet --version)
    Write-Host "[OK] .NET SDK found: $version" -ForegroundColor Green
}

# WinUI 3 templates
$templatePackage = 'Microsoft.WindowsAppSDK.WinUI.CSharp.Templates'
# 'dotnet new uninstall' without arguments lists installed template packages (output is localized, so match the package id only).
$installedTemplates = dotnet new uninstall 2>&1 | Out-String
if ($installedTemplates -match [regex]::Escape($templatePackage)) {
    Write-Host "[!] WinUI 3 templates are already installed." -ForegroundColor Yellow
} else {
    Write-Host "`nInstalling WinUI 3 project templates..."
    dotnet new install $templatePackage 2>&1 | Out-Null
    Write-Host "[OK] WinUI 3 templates installed." -ForegroundColor Green
}

# Inno Setup
# Returns the path of an Inno Setup 7+ ISCC.exe, or $null. Also reports older versions via $script:oldMajors.
function Find-Iscc7 {
    $candidates = @()
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { $candidates += $cmd.Source }
    $candidates += Get-ChildItem -Path "${env:ProgramFiles(x86)}", $env:ProgramFiles, "$env:LOCALAPPDATA\Programs" `
        -Filter 'ISCC.exe' -Recurse -Depth 2 -ErrorAction SilentlyContinue | ForEach-Object FullName

    $found = $null
    $script:oldMajors = @()
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        # The compiler banner reads "Inno Setup 7 Command-Line Compiler" (v6: "Inno Setup 6.x.x ...")
        $banner = & $candidate '/?' 2>&1 | Out-String
        if ($banner -match 'Inno Setup\s+(\d+)') {
            if ([int]$Matches[1] -ge 7) { if (-not $found) { $found = $candidate } }
            else { $script:oldMajors += [int]$Matches[1] }
        }
    }
    return $found
}

$iscc7 = Find-Iscc7
if ($iscc7) {
    Write-Host "[OK] Inno Setup 7 found: $iscc7" -ForegroundColor Green
} else {
    Write-Host "`nInstalling Inno Setup 7 via winget..."
    winget install JRSoftware.InnoSetup.7 --silent --accept-package-agreements
    $iscc7 = Find-Iscc7
    if ($iscc7) {
        Write-Host "[OK] Inno Setup 7 installed: $iscc7" -ForegroundColor Green
    } else {
        Write-Host "[!] Inno Setup 7 was installed but ISCC.exe could not be located." -ForegroundColor Yellow
    }
}

# Only v7 should be installed: warn about older versions, but never uninstall them automatically.
$oldMajors = @($oldMajors | Select-Object -Unique)
if ($oldMajors.Count -gt 0) {
    Write-Host "[!] Inno Setup $($oldMajors -join ', ') is installed. Please remove it (Settings -> Apps, or 'winget uninstall JRSoftware.InnoSetup') so only version 7 remains." -ForegroundColor Yellow
}

# Make 'iscc' resolve to v7 (user PATH for new shells, $env:PATH for this one).
if ($iscc7) {
    $isccDir = Split-Path $iscc7
    $resolved = Get-Command iscc -ErrorAction SilentlyContinue
    if (-not $resolved -or (Split-Path $resolved.Source) -ne $isccDir) {
        $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
        $userEntries = @($userPath -split ';' | Where-Object { $_ })
        if ($userEntries -notcontains $isccDir) {
            [Environment]::SetEnvironmentVariable('Path', (($isccDir, $userPath) -join ';').TrimEnd(';'), 'User')
        }
        $env:PATH = "$isccDir;$env:PATH"
        Write-Host "[OK] Added '$isccDir' to the user PATH. Restart open terminals so 'iscc' resolves." -ForegroundColor Green
    }
}

Write-Host "`n[Done] Setup complete. Run 'dotnet build' to get started.`n" -ForegroundColor Cyan
