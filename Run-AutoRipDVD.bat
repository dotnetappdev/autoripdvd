@echo off
REM AutoRip DVD Quick Launcher (Batch version)
REM For PowerShell version with more features, run: powershell -ExecutionPolicy Bypass -File Run-AutoRipDVD.ps1

echo ===================================
echo    AutoRip DVD - Quick Launcher
echo ===================================
echo.

echo Building application...
dotnet build -c Release --nologo -v quiet

if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo Build successful!
echo.

echo Launching AutoRip DVD...
start "" "AutoRipDVD\bin\x64\Release\net8.0-windows10.0.19041.0\AutoRipDVD.exe"

echo.
echo Application started!
echo If you see DLL errors, see DEPLOYMENT.md for troubleshooting.
echo.
