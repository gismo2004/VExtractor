@echo off
rem Double-click to build the controller catalog for the OptoV integration.
rem Checks for the .NET runtime, the one thing VExtractor cannot report missing itself, then
rem starts the guided run. The window stays open until you press a key.

setlocal
cd /d "%~dp0"
title VExtractor
echo.
echo VExtractor - builds the controller catalog for the OptoV integration.
echo.

rem ---- .NET 8 runtime -----------------------------------------------------------------
set "DOTNET_OK="
where dotnet >nul 2>nul && (
    for /f "tokens=1,2" %%a in ('dotnet --list-runtimes 2^>nul') do (
        if "%%a"=="Microsoft.NETCore.App" (
            for /f "tokens=1 delims=." %%v in ("%%b") do if %%v GEQ 8 set "DOTNET_OK=1"
        )
    )
)
if not defined DOTNET_OK (
    echo The .NET 8 runtime is not installed. VExtractor cannot start without it.
    echo.
    echo   Get it here: https://dotnet.microsoft.com/download/dotnet/8.0
    echo   Under "Run desktop apps", take the x64 installer. Run it, click through,
    echo   then double-click this file again.
    echo.
    start "" "https://dotnet.microsoft.com/download/dotnet/8.0"
    goto :end
)

rem ---- Go -------------------------------------------------------------------------------
rem This window is kept open below, so the program need not wait for a key of its own.
set "VEXTRACTOR_LAUNCHER=1"
if exist "%~dp0VExtractor.exe" (
    "%~dp0VExtractor.exe"
) else (
    echo VExtractor.exe is not next to this file. Extract the whole release archive into one folder.
)

:end
echo.
pause
endlocal
