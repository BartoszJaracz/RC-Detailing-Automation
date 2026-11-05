@echo off
echo [RC Automation] Building the plugin...
cd /d "%~dp0src"
dotnet build BricsCadRc.csproj -c Release
if errorlevel 1 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)
echo.
echo [OK] RcAutomation.dll ready in the bin\ folder
echo.
echo To load it in BricsCAD type: NETLOAD
echo Select file: %~dp0bin\RcAutomation.dll
pause
