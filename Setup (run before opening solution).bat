:: One time setup for a fresh clone.
::  - Torch plugin: junctions to the Dedicated Server and Torch binaries
::  - Magnetar plugin: Directory.Build.props (local paths), copied from the template
@echo off
cd /d "%~dp0"

if not exist Directory.Build.props (
    copy Directory.Build.props.template Directory.Build.props >nul
    echo Created Directory.Build.props, edit it if the auto-detected Dedicated Server / Magnetar paths are wrong.
)

set /p GAMEDIR="Folder containing SpaceEngineersDedicated.exe (inside Torch: the DedicatedServer64 folder): "
if exist GameBinaries rmdir GameBinaries
mklink /J GameBinaries "%GAMEDIR%"
if errorlevel 1 goto Error

set /p TORCHDIR="Folder containing Torch.Server.exe: "
if exist TorchBinaries rmdir TorchBinaries
mklink /J TorchBinaries "%TORCHDIR%"
if errorlevel 1 goto Error

echo Done! You can now open AutoStation.sln.
goto End

:Error
echo An error occurred creating the junction.

:End
pause
