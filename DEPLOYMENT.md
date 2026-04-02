# Deployment Guide

## Building and Running AutoRip DVD

### Option 1: Debug/Test Run (Quick)

```powershell
# Build and run directly
dotnet build -c Debug
dotnet run --project AutoRipDVD\AutoRipDVD.csproj
```

### Option 2: Release Build (Recommended)

```powershell
# Clean previous builds
dotnet clean

# Restore packages
dotnet restore

# Build release version with self-contained WindowsAppSDK
dotnet build -c Release

# Run the application
cd AutoRipDVD\bin\Release\net8.0-windows10.0.19041.0\win-x64
.\AutoRipDVD.exe
```

### Option 3: Publish Standalone (Distribution)

```powershell
# Publish self-contained application
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false

# Output will be in:
# AutoRipDVD\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\

# Run from publish folder
cd AutoRipDVD\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish
.\AutoRipDVD.exe
```

## Troubleshooting DLL Errors

### If you still get Microsoft.ui.xaml.dll errors:

1. **Install Windows App SDK Runtime**
   ```powershell
   # Download and install from:
   https://aka.ms/windowsappsdk/1.5/latest/windowsappruntimeinstall-x64.exe
   ```

2. **Verify .NET 8 Desktop Runtime**
   ```powershell
   dotnet --list-runtimes
   # Should show: Microsoft.WindowsDesktop.App 8.0.x
   ```
   
   If missing, install from: https://dotnet.microsoft.com/download/dotnet/8.0

3. **Clean and Rebuild**
   ```powershell
   dotnet clean
   Remove-Item -Recurse -Force bin,obj
   dotnet restore
   dotnet build -c Release
   ```

4. **Check for Corrupted NuGet Cache**
   ```powershell
   dotnet nuget locals all --clear
   dotnet restore
   dotnet build -c Release
   ```

### For Unpackaged Deployment Issues:

The project is configured for unpackaged deployment with:
- `WindowsPackageType=None`
- `WindowsAppSDKSelfContained=true`
- COM wrappers initialization

This means no MSIX packaging is required - it runs as a standard Win32 .exe

### System Requirements:

- **OS**: Windows 10 version 1809 (build 17763) or later
- **Runtime**: .NET 8.0 Desktop Runtime
- **SDK**: Windows App SDK 1.5 (included with build)

## Distribution

When distributing to users:

1. **Copy the entire publish folder** - all DLLs are needed
2. **OR** install Windows App SDK Runtime on target machine
3. **OR** create an MSIX package (requires packaging certificate)

### Quick Distribution Package:

```powershell
# After publishing, create a ZIP
dotnet publish -c Release -r win-x64
cd AutoRipDVD\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish
Compress-Archive -Path * -DestinationPath ..\..\..\..\..\..\AutoRipDVD-v1.0.0-win-x64.zip
```

Users can extract and run `AutoRipDVD.exe` directly.

## Visual Studio

If using Visual Studio 2022:

1. Open `AutoRipDVD.sln`
2. Set build configuration to **Release** and **x64**
3. Build → Build Solution (Ctrl+Shift+B)
4. Right-click project → Publish
5. Choose folder publish target
6. Publish

## Common Issues

### "Application requires .NET Desktop Runtime"
- Install: https://dotnet.microsoft.com/download/dotnet/8.0

### "This application requires Windows App SDK"
- Install: https://aka.ms/windowsappsdk/1.5/latest/windowsappruntimeinstall-x64.exe

### "Could not load file or assembly"
- Ensure all DLLs from publish/bin folder are together
- Don't move .exe without its dependencies

### "Access Denied" when detecting discs
- Run as Administrator for WMI disc detection
- Right-click → Run as Administrator
