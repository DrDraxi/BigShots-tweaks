@echo off
REM Convenience wrapper: runs Setup-BigShotsFolder.ps1 with execution policy bypass.
REM Pass -Revert to undo: Setup-BigShotsFolder.bat -Revert
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-BigShotsFolder.ps1" %*
