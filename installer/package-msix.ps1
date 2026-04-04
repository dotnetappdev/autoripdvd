#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and packages AutoRip DVD as an MSIX for Windows Store / direct distribution.

.DESCRIPTION
    This script:
      1. Publishes the app (dotnet publish)
      2. Creates a Package.appxmanifest if one doesn't exist
      3. Invokes the Windows SDK MakeAppx + SignTool to produce a signed .msix
      4. Optionally submits to Windows Store via Partner Center CLI (winappstore-cli)

    NOTE: Inno Setup produces a traditional .exe installer (ideal for direct downloads).
    MSIX is the format required by the Microsoft Store.  They are complementary:
      • Inno Setup .exe  → GitHub Releases, your own website, enterprise deployment
      • MSIX             → Microsoft Store, Windows Package Manager (winget) submission

.PARAMETER Version
    Version to embed in the manifest (default: reads from AutoRipDVD.csproj).

.PARAMETER CertPath
    Path to a .pfx code-signing certificate.
    If omitted, the package is self-signed with a temporary test cert (not Store-ready).

.PARAMETER CertPassword
    Password for the .pfx file (if CertPath is specified).

.PARAMETER OutputDir
    Directory where the .msix file is written (default: installer\dist).

.PARAMETER Publish
    If specified, attempts to upload to Windows Store using the Partner Center CLI.

.EXAMPLE
    # Build unsigned test package
    .\installer\package-msix.ps1

.EXAMPLE
    # Build production-signed package
    .\installer\package-msix.ps1 -CertPath "certs\MyStore.pfx" -CertPassword "s3cr3t"

.EXAMPLE
    # Build and submit to Store
    .\installer\package-msix.ps1 -CertPath "certs\MyStore.pfx" -CertPassword "s3cr3t" -Publish
#>

param(
    [string] $Version      = '',
    [string] $CertPath     = '',
    [string] $CertPassword = '',
    [string] $OutputDir    = "$PSScriptRoot\dist",
    [switch] $Publish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve paths ─────────────────────────────────────────────────────────────

$RepoRoot    = Split-Path $PSScriptRoot -Parent
$CsprojPath  = Join-Path $RepoRoot 'AutoRipDVD\AutoRipDVD.csproj'
$PublishDir  = Join-Path $PSScriptRoot 'publish-msix'
$ManifestSrc = Join-Path $PSScriptRoot 'Package.appxmanifest'

# ── Resolve version ───────────────────────────────────────────────────────────

if (-not $Version) {
    [xml]$csproj = Get-Content $CsprojPath
    $Version = $csproj.Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
    if (-not $Version) { $Version = '2.1.0' }
}

# Windows Store requires 4-part version (major.minor.patch.0)
$VersionFour = if ($Version -match '^\d+\.\d+\.\d+$') { "$Version.0" } else { $Version }

Write-Host "Building AutoRip DVD $Version for MSIX packaging..." -ForegroundColor Cyan

# ── Step 1 — dotnet publish ───────────────────────────────────────────────────

Write-Host "`n[1/5] Publishing application..." -ForegroundColor Yellow

& dotnet publish $CsprojPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $PublishDir `
    -p:Version=$Version `
    -p:WindowsPackageType=None `
    2>&1 | Write-Host

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

# ── Step 2 — generate / copy Package.appxmanifest ────────────────────────────

Write-Host "`n[2/5] Preparing package manifest..." -ForegroundColor Yellow

if (-not (Test-Path $ManifestSrc)) {
    Write-Warning "Package.appxmanifest not found at $ManifestSrc — generating a template."
    $ManifestSrc = Join-Path $PSScriptRoot 'Package.appxmanifest'
    New-AppxManifest -OutputPath $ManifestSrc -Version $VersionFour
}

# Patch version in manifest
[xml]$manifest = Get-Content $ManifestSrc
$manifest.Package.Identity.Version = $VersionFour
$manifest.Save($ManifestSrc)

Copy-Item $ManifestSrc (Join-Path $PublishDir 'AppxManifest.xml') -Force

# ── Step 3 — locate Windows SDK tools ────────────────────────────────────────

Write-Host "`n[3/5] Locating Windows SDK tools..." -ForegroundColor Yellow

function Find-SdkTool([string]$Name) {
    $kitsRoot = 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots'
    $ver = (Get-ItemProperty $kitsRoot -ErrorAction SilentlyContinue).KitsRoot10
    if ($ver) {
        $candidate = Join-Path $ver "bin\*\x64\$Name"
        $found = (Resolve-Path $candidate -ErrorAction SilentlyContinue | Select-Object -Last 1)
        if ($found) { return $found.Path }
    }
    # Fallback: PATH
    $inPath = Get-Command $Name -ErrorAction SilentlyContinue
    if ($inPath) { return $inPath.Source }
    return $null
}

$MakeAppx = Find-SdkTool 'makeappx.exe'
$SignTool  = Find-SdkTool 'signtool.exe'

if (-not $MakeAppx) {
    throw @'
makeappx.exe not found.  Install the Windows 11 SDK:
  winget install Microsoft.WindowsSDK.10.0.22621
'@
}

Write-Host "  makeappx : $MakeAppx"
Write-Host "  signtool : $($SignTool ?? '(not found — skipping signing)')"

# ── Step 4 — MakeAppx pack ────────────────────────────────────────────────────

Write-Host "`n[4/5] Creating MSIX package..." -ForegroundColor Yellow

$null = New-Item $OutputDir -ItemType Directory -Force
$MsixPath = Join-Path $OutputDir "AutoRipDVD-$Version-x64.msix"

& $MakeAppx pack /d $PublishDir /p $MsixPath /o /nv
if ($LASTEXITCODE -ne 0) { throw "makeappx failed (exit $LASTEXITCODE)" }

Write-Host "  Package created: $MsixPath"

# ── Step 5 — code signing ─────────────────────────────────────────────────────

Write-Host "`n[5/5] Signing..." -ForegroundColor Yellow

if ($CertPath -and (Test-Path $CertPath)) {
    # Production signing with a Store or enterprise certificate
    $signArgs = @('sign', '/fd', 'SHA256', '/f', $CertPath)
    if ($CertPassword) { $signArgs += @('/p', $CertPassword) }
    $signArgs += $MsixPath

    & $SignTool @signArgs
    if ($LASTEXITCODE -ne 0) { throw "signtool failed (exit $LASTEXITCODE)" }
    Write-Host "  Package signed with: $CertPath"
}
else {
    Write-Warning @"
No certificate provided — creating a self-signed test certificate.
This package can be installed locally (Enable-WindowsOptionalFeature or
Install-Certificate) but cannot be submitted to the Microsoft Store.
"@

    # Generate a temporary self-signed cert valid for 1 year
    $TestCert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=AutoRipDVD Test" `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(1)

    if ($SignTool) {
        & $SignTool sign /fd SHA256 /sha1 $TestCert.Thumbprint /td SHA256 $MsixPath
    } else {
        Write-Warning "signtool.exe not found — package is unsigned."
    }
}

# ── Optional: submit to Windows Store ────────────────────────────────────────

if ($Publish) {
    Write-Host "`n[Extra] Submitting to Windows Store..." -ForegroundColor Yellow

    $storeCli = Get-Command 'winappstore' -ErrorAction SilentlyContinue
    if (-not $storeCli) {
        Write-Warning @"
Windows Store submission CLI (winappstore-cli) not found.
Install it with: npm install -g winappstore-cli
Then configure your Partner Center credentials:
  winappstore configure --tenant-id <tid> --client-id <cid> --client-secret <cs>
"@
    } else {
        Write-Host "  Uploading $MsixPath to Partner Center..."
        & winappstore submit --package $MsixPath
    }
}

# ── Done ──────────────────────────────────────────────────────────────────────

Write-Host "`n✓ Done!  Package: $MsixPath" -ForegroundColor Green

# ── Helper: generate a template appxmanifest ──────────────────────────────────

function New-AppxManifest {
    param([string]$OutputPath, [string]$Version)

    $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<!--
  AutoRip DVD — Package.appxmanifest (Windows Store / MSIX)

  Before submitting to the Store:
    1. Replace "YourPublisherName" with the CN from your Store publisher certificate
    2. Replace "YourPublisherDisplayName" with your display name in Partner Center
    3. Update the Logo paths to point to actual PNG assets
    4. Optionally add the "rescap:Capability" entries for any restricted capabilities
       your app uses (e.g. removableStorage for optical drives)
-->
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap mp rescap">

  <Identity
    Name="SASProducts.AutoRipDVD"
    Publisher="CN=YourPublisherName"
    Version="$Version"
    ProcessorArchitecture="x64"/>

  <mp:PhoneIdentity
    PhoneProductId="7f3a2b1c-e4d5-4f6a-8b9c-0d1e2f3a4b5c"
    PhonePublisherId="00000000-0000-0000-0000-000000000000"/>

  <Properties>
    <DisplayName>AutoRip DVD</DisplayName>
    <PublisherDisplayName>YourPublisherDisplayName</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop"
                        MinVersion="10.0.17763.0"
                        MaxVersionTested="10.0.22621.0"/>
    <!-- .NET 10 Desktop Runtime -->
    <PackageDependency
      Name="Microsoft.DotNet.DesktopRuntime.10"
      MinVersion="10.0.0.0"
      Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"/>
    <!-- Windows App SDK 1.5 -->
    <PackageDependency
      Name="Microsoft.WindowsAppRuntime.1.5"
      MinVersion="5000.312.429.0"
      Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"/>
  </Dependencies>

  <Resources>
    <Resource Language="en-us"/>
  </Resources>

  <Applications>
    <Application Id="AutoRipDVD"
                 Executable="AutoRipDVD.exe"
                 EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="AutoRip DVD"
        Description="Professional DVD and Blu-ray ripping suite"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile
          Wide310x150Logo="Assets\Wide310x150Logo.png"
          Square310x310Logo="Assets\Square310x310Logo.png"
          Square71x71Logo="Assets\Square71x71Logo.png"/>
        <uap:SplashScreen Image="Assets\SplashScreen.png" BackgroundColor="#1a1a2e"/>
      </uap:VisualElements>
    </Application>
  </Applications>

  <Capabilities>
    <!-- Optical drive access — may require restricted capability approval -->
    <rescap:Capability Name="removableStorage"/>
    <!-- Read/write to user-chosen output folders -->
    <Capability Name="picturesLibrary"/>
    <Capability Name="videosLibrary"/>
    <!-- Network for metadata lookups -->
    <Capability Name="internetClient"/>
  </Capabilities>

</Package>
"@
    $xml | Set-Content $OutputPath -Encoding UTF8
}
