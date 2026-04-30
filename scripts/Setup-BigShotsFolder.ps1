<#
.SYNOPSIS
  One-shot installer for BigShotsTweaks: renames the BIG SHOTS folder to drop
  the registered-trademark glyph, patches Steam's appmanifest, and copies the
  mod DLL into the game's Mods/ folder.

.DESCRIPTION
  Approach (option B from the README): rename `BIG SHOTS®` -> `BIG SHOTS`,
  patch `installdir` in `appmanifest_<appid>.acf`, then copy
  `BigShotsTweaks.dll` into `<game>\Mods\`.

  Idempotent. Use -Revert to undo the rename + manifest patch (does not
  uninstall the mod).

  Auto-detects Steam via the registry and common install paths, then scans
  every library in `libraryfolders.vdf` for the BigShots manifest.

  WARNING: Steam's "Verify Integrity of Game Files" can reset the manifest
  back to the depot value (with the ®). If that happens, just re-run.

.PARAMETER SteamRoot
  Path to your Steam install. If omitted, auto-detected.

.PARAMETER OriginalName
  Folder name Steam ships with. Default: "BIG SHOTS®"

.PARAMETER NewName
  Clean folder name to rename to. Default: "BIG SHOTS"

.PARAMETER Revert
  Rename clean -> ® and restore installdir. Does not uninstall the mod.

.PARAMETER NoInstallMod
  Skip the mod-DLL install step.

.PARAMETER Yes
  Skip the interactive confirmation prompt.

.EXAMPLE
  ./Setup-BigShotsFolder.ps1
  ./Setup-BigShotsFolder.ps1 -Revert
  ./Setup-BigShotsFolder.ps1 -SteamRoot "D:\Steam" -Yes
#>
[CmdletBinding()]
param(
    [string]$SteamRoot     = "",
    [string]$OriginalName  = "BIG SHOTS$([char]0x00AE)",
    [string]$NewName       = "BIG SHOTS",
    [switch]$Revert,
    [switch]$NoInstallMod,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

function Find-SteamRoot {
    $candidates = New-Object System.Collections.Generic.List[string]
    $regSources = @(
        @{ Path = 'HKCU:\Software\Valve\Steam';                   Name = 'SteamPath'   },
        @{ Path = 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam';       Name = 'InstallPath' },
        @{ Path = 'HKLM:\SOFTWARE\Valve\Steam';                   Name = 'InstallPath' }
    )
    foreach ($src in $regSources) {
        try {
            $val = (Get-ItemProperty -Path $src.Path -Name $src.Name -ErrorAction Stop).$($src.Name)
            if ($val) { $candidates.Add($val.Replace('/', '\')) }
        } catch { }
    }
    $drives = (Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Free -ne $null -and $_.Name.Length -eq 1 }).Root
    $relPaths = @('Program Files (x86)\Steam','Program Files\Steam','Steam','Games\Steam','SteamLibrary')
    foreach ($drive in $drives) { foreach ($rel in $relPaths) { $candidates.Add((Join-Path $drive $rel)) } }
    foreach ($c in $candidates | Select-Object -Unique) {
        if (Test-Path (Join-Path $c 'steamapps\libraryfolders.vdf')) { return $c }
        if (Test-Path (Join-Path $c 'steamapps')) { return $c }
    }
    return $null
}

function Get-SteamLibraryPaths {
    param([string]$root)
    $libs = New-Object System.Collections.Generic.List[string]
    $libs.Add($root)
    $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
    if (Test-Path $vdf) {
        $vdfText = [System.IO.File]::ReadAllText($vdf, [System.Text.Encoding]::UTF8)
        foreach ($m in [regex]::Matches($vdfText, '"path"\s+"([^"]+)"')) {
            $p = $m.Groups[1].Value -replace '\\\\','\'
            if ((Test-Path $p) -and -not $libs.Contains($p)) { $libs.Add($p) }
        }
    }
    return $libs
}

function Find-ModDll {
    param([string]$scriptDir)
    $candidates = @(
        (Join-Path $scriptDir 'BigShotsTweaks.dll'),
        (Join-Path $scriptDir '..\mod\bin\Release\BigShotsTweaks.dll')
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return (Resolve-Path $c).Path } }
    return $null
}

function Write-Banner {
    Write-Host ""
    Write-Host "== BigShots Tweaks Setup ==" -ForegroundColor Cyan
    Write-Host ""
}

# --- Resolve Steam + manifest ---------------------------------------------

Write-Banner

if (-not $SteamRoot) {
    $SteamRoot = Find-SteamRoot
    if (-not $SteamRoot) { throw "Could not auto-detect Steam install. Pass -SteamRoot 'X:\Path\To\Steam'." }
    Write-Host "Auto-detected Steam: $SteamRoot"
}
if (-not (Test-Path (Join-Path $SteamRoot 'steamapps'))) {
    throw "Steam path '$SteamRoot' has no steamapps folder. Pass -SteamRoot to override."
}

if ($Revert) { $fromName = $NewName;      $toName = $OriginalName }
else         { $fromName = $OriginalName; $toName = $NewName      }

$manifest = $null
foreach ($lib in Get-SteamLibraryPaths $SteamRoot) {
    $libSteamApps = Join-Path $lib 'steamapps'
    if (-not (Test-Path $libSteamApps)) { continue }
    foreach ($m in (Get-ChildItem -Path $libSteamApps -Filter 'appmanifest_*.acf' -File -ErrorAction SilentlyContinue)) {
        $content = [System.IO.File]::ReadAllText($m.FullName, [System.Text.Encoding]::UTF8)
        if ($content -match '"installdir"\s+"([^"]+)"') {
            $installdir = $Matches[1]
            if ($installdir -eq $fromName -or $installdir -eq $toName) {
                $manifest = [PSCustomObject]@{
                    Path       = $m.FullName
                    Installdir = $installdir
                    Content    = $content
                    LibraryDir = $lib
                }
                break
            }
        }
    }
    if ($manifest) { break }
}
if (-not $manifest) {
    throw "Could not find appmanifest with installdir matching '$fromName' or '$toName' in any Steam library. Is BIG SHOTS installed?"
}

$commonDir = Join-Path $manifest.LibraryDir 'steamapps\common'
$fromPath  = Join-Path $commonDir $fromName
$toPath    = Join-Path $commonDir $toName
$gameDir   = if (Test-Path $toPath) { $toPath } elseif (Test-Path $fromPath) { $fromPath } else { $null }
$modsDir   = if ($gameDir) { Join-Path $gameDir 'Mods' } else { $null }

# --- Find the mod DLL -----------------------------------------------------

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$dllPath = $null
if (-not $NoInstallMod -and -not $Revert) { $dllPath = Find-ModDll $scriptDir }

# --- Plan summary + confirmation ------------------------------------------

$alreadyDone = (Test-Path $toPath) -and -not (Test-Path $fromPath) -and ($manifest.Installdir -eq $toName)
$steamProc   = Get-Process -Name 'steam' -ErrorAction SilentlyContinue

Write-Host "Library:           $($manifest.LibraryDir)"
Write-Host "Manifest:          $($manifest.Path)"
Write-Host "Current installdir: $($manifest.Installdir)"
Write-Host ""

if ($alreadyDone -and ($Revert -or $NoInstallMod -or -not $dllPath -or (Test-Path (Join-Path $modsDir 'BigShotsTweaks.dll')))) {
    Write-Host "Nothing to do - folder is '$toName', manifest matches" -ForegroundColor Green
    if (-not $Revert -and -not $NoInstallMod -and $dllPath -and (Test-Path (Join-Path $modsDir 'BigShotsTweaks.dll'))) {
        Write-Host "Mod is installed at $modsDir." -ForegroundColor Green
    }
    return
}

Write-Host "Plan:" -ForegroundColor Yellow
if ($steamProc)   { Write-Host "  - Stop Steam (pid $($steamProc.Id))" }
if (-not $alreadyDone) {
    Write-Host "  - Rename folder: '$fromName' -> '$toName'"
    Write-Host "  - Patch installdir in $($manifest.Path)"
}
if (-not $Revert -and -not $NoInstallMod) {
    if ($dllPath) {
        Write-Host "  - Copy $(Split-Path -Leaf $dllPath) -> $modsDir\BigShotsTweaks.dll"
    } else {
        Write-Host "  - (mod DLL not found alongside script; skipping install)"
    }
}
Write-Host ""

if (-not $Yes) {
    Write-Host "Press [Enter] to continue, or type [q] then [Enter] to cancel:" -ForegroundColor Yellow
    $resp = Read-Host
    if ($resp -match '^[qQ]') { Write-Host "Aborted."; return }
}

# --- Execute --------------------------------------------------------------

if ($steamProc) {
    Write-Host "Stopping Steam..." -ForegroundColor Yellow
    Stop-Process -Name 'steam' -Force
    Start-Sleep -Seconds 2
}

if (-not $alreadyDone) {
    if (-not (Test-Path $fromPath)) { throw "Source folder does not exist: $fromPath" }
    if (Test-Path $toPath)          { throw "Destination already exists: $toPath. Resolve manually before re-running." }

    Write-Host "Renaming folder: '$fromName' -> '$toName'"
    Rename-Item -LiteralPath $fromPath -NewName $toName
    $gameDir = $toPath
    $modsDir = Join-Path $gameDir 'Mods'

    Write-Host "Patching installdir..."
    $patched = $manifest.Content -replace '("installdir"\s+")[^"]+"', "`$1$toName`""
    $backupPath = "$($manifest.Path).bst.bak"
    if (-not (Test-Path $backupPath)) {
        Copy-Item -LiteralPath $manifest.Path -Destination $backupPath
        Write-Host "Backed up original manifest to: $backupPath"
    }
    [System.IO.File]::WriteAllText($manifest.Path, $patched, (New-Object System.Text.UTF8Encoding($false)))
}

# --- Mod install ----------------------------------------------------------

if (-not $Revert -and -not $NoInstallMod -and $dllPath) {
    if (-not (Test-Path $modsDir)) {
        Write-Host ""
        Write-Host "!! ALERT !!" -ForegroundColor Red -BackgroundColor Black
        Write-Host "Mods folder does not exist: $modsDir" -ForegroundColor Red
        Write-Host "Did you install MelonLoader?" -ForegroundColor Red
        Write-Host "Get it from https://melonloader.co/ and run it against BigShots.exe." -ForegroundColor Red
        Write-Host "The mod will not load until MelonLoader is installed." -ForegroundColor Red
        Write-Host ""
        if (-not $Yes) {
            Write-Host "[c] Create Mods folder anyway and copy the DLL"
            Write-Host "[q] Cancel mod install (rename + patch already applied)"
            Write-Host "[Enter] Skip mod install"
            $resp = Read-Host "Choice"
            if ($resp -match '^[qQ]') { Write-Host "Skipped mod install."; return }
            if ($resp -notmatch '^[cC]') { Write-Host "Skipped mod install."; return }
        }
        New-Item -ItemType Directory -Path $modsDir | Out-Null
    }
    $dest = Join-Path $modsDir 'BigShotsTweaks.dll'
    Copy-Item -LiteralPath $dllPath -Destination $dest -Force
    Write-Host "Installed: $dest" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Restart Steam and launch BIG SHOTS." -ForegroundColor Green
Write-Host "Reminder: 'Verify Integrity of Game Files' may reset the rename. Re-run if so."
