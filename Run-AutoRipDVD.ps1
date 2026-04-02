# AutoRip DVD Launcher
# This script builds and runs the AutoRip DVD application

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "   AutoRip DVD - Quick Launcher   " -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Check if app is already running
$runningProcess = Get-Process -Name "AutoRipDVD" -ErrorAction SilentlyContinue
if ($runningProcess) {
    Write-Host "⚠ AutoRip DVD is already running (PID: $($runningProcess.Id))" -ForegroundColor Yellow
    $choice = Read-Host "Kill existing process and restart? (Y/N)"
    if ($choice -eq 'Y' -or $choice -eq 'y') {
        Stop-Process -Name "AutoRipDVD" -Force
        Start-Sleep -Seconds 2
        Write-Host "✓ Stopped existing process" -ForegroundColor Green
    } else {
        Write-Host "Exiting..." -ForegroundColor Yellow
        exit
    }
}

# Build
Write-Host "Building application..." -ForegroundColor Yellow
dotnet build -c Release --nologo -v quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed!" -ForegroundColor Red
    pause
    exit 1
}

Write-Host "✓ Build successful!" -ForegroundColor Green
Write-Host ""

# Find the executable
$exePath = "AutoRipDVD\bin\x64\Release\net8.0-windows10.0.19041.0\AutoRipDVD.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "✗ Executable not found at: $exePath" -ForegroundColor Red
    Write-Host "Try running 'dotnet publish -c Release' instead" -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "Launching AutoRip DVD..." -ForegroundColor Green
Write-Host ""
Write-Host "Location: $exePath" -ForegroundColor Gray
Write-Host ""

# Run the application
Start-Process -FilePath $exePath -WorkingDirectory (Get-Location)

Write-Host "✓ Application started!" -ForegroundColor Green
Write-Host ""
Write-Host "If you see DLL errors, run:" -ForegroundColor Yellow
Write-Host "  1. Install Windows App SDK Runtime:" -ForegroundColor Gray
Write-Host "     https://aka.ms/windowsappsdk/1.5/latest/windowsappruntimeinstall-x64.exe" -ForegroundColor Gray
Write-Host "  2. Or publish self-contained:" -ForegroundColor Gray
Write-Host "     dotnet publish -c Release --self-contained" -ForegroundColor Gray
Write-Host ""
