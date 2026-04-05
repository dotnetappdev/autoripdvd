; ============================================================================
;  AutoRip DVD — Inno Setup 6 Installer Script
;  Targets:  Windows 10 1809+ / Windows 11  (x64 only)
;  Produces: AutoRipDVD-Setup-2.1.0.exe     (direct-download installer)
;
;  Features:
;    • "All users" or "Current user only" install — user is prompted at startup
;    • Database placed in %PROGRAMDATA%\AutoRipDVD  (all-users) or
;                         %APPDATA%\AutoRipDVD       (per-user)
;      communicated to the app via the AUTORIP_DATA_DIR environment variable
;    • Prerequisite checks: .NET 10 Desktop Runtime, Windows App SDK 1.5
;      Both are downloaded from Microsoft on-demand if missing
;    • Optional desktop shortcut (unchecked by default)
;    • Clean uninstall removes shortcuts, registry keys, env var;
;      the data folder is intentionally left so the user keeps their history
;
;  BEFORE BUILDING:
;    1. Run: dotnet publish AutoRipDVD.sln -c Release -r win-x64 --self-contained false
;            -o installer\publish
;    2. Place app icon at installer\assets\app.ico     (256×256 recommended)
;    3. (Optional) Place wizard image at installer\assets\wizard.bmp  (164×314 px)
;    4. Open this file in Inno Setup 6 (https://jrsoftware.org/isdl.php) and compile
; ============================================================================

#define AppName        "AutoRip DVD"
#define AppVersion     "2.1.0"
#define AppPublisher   "SAS Products"
#define AppURL         "https://github.com/dotnetappdev/autoripdvd2"
#define AppExeName     "AutoRipDVD.exe"
#define AppDescription "Professional DVD & Blu-ray Ripping Suite"

; Unique GUID — do NOT change after first release (controls upgrade detection)
#define AppId          "{{7F3A2B1C-E4D5-4F6A-8B9C-0D1E2F3A4B5C}"

; Prerequisite download URLs (pinned versions — update when bumping .NET / AppSDK)
#define DotNetUrl  "https://download.visualstudio.microsoft.com/download/pr/dotnet-runtime-10.0.0-win-x64.exe"
#define WinAppSdkUrl "https://aka.ms/windowsappsdk/1.5/1.5.240311000/windowsappruntimeinstall-x64.exe"
; Alternative: install Windows App Runtime via winget
;   winget install --id Microsoft.WindowsAppRuntime -e

; ── [Setup] ──────────────────────────────────────────────────────────────────

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases

; Default install dir resolves automatically:
;   Admin  (all-users)  → C:\Program Files\AutoRip DVD
;   User   (per-user)   → C:\Users\<name>\AppData\Local\Programs\AutoRip DVD
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}

; Allow the user to choose all-users vs per-user at the first wizard page
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Output
OutputDir=dist
OutputBaseFilename=AutoRipDVD-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
DiskSpanning=no

; Icon (comment out if assets\app.ico doesn't exist yet)
; SetupIconFile=assets\app.ico
; WizardImageFile=assets\wizard.bmp
; WizardSmallImageFile=assets\wizard-small.bmp

WizardStyle=modern
WizardResizable=yes
ShowLanguageDialog=auto

; Architecture — x64 only
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64

; Uninstall
UninstallDisplayName={#AppName} {#AppVersion}
; UninstallDisplayIcon={app}\{#AppExeName}
CreateUninstallRegKey=yes

; Minimum Windows version (1809 = build 17763)
MinVersion=10.0.17763

; ── [Languages] ──────────────────────────────────────────────────────────────

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ── [Tasks] ──────────────────────────────────────────────────────────────────

[Tasks]
Name: "desktopicon"; \
  Description: "{cm:CreateDesktopIcon}"; \
  GroupDescription: "{cm:AdditionalIcons}"; \
  Flags: unchecked

; ── [Dirs] ───────────────────────────────────────────────────────────────────

[Dirs]
; Create the data directory during install.
; "uninsneveruninstall" keeps it on uninstall so the user's history is preserved.

; Per-user install
Name: "{userappdata}\AutoRipDVD"; \
  Flags: uninsneveruninstall; \
  Check: not IsAdminInstallMode

; All-users install — write permission for all users via Everyone ACE
Name: "{commonappdata}\AutoRipDVD"; \
  Permissions: everyone-modify; \
  Flags: uninsneveruninstall; \
  Check: IsAdminInstallMode

; ── [Files] ──────────────────────────────────────────────────────────────────

[Files]
; Application binaries (built by: dotnet publish … -o installer\publish)
Source: "publish\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

; ── [Registry] ───────────────────────────────────────────────────────────────

[Registry]

; ── Install metadata (used by update checks, diagnostics) ──
Root: HKCU; Subkey: "SOFTWARE\{#AppName}"; \
  ValueType: string; ValueName: "InstallScope"; ValueData: "user"; \
  Flags: uninsdeletekey; Check: not IsAdminInstallMode

Root: HKCU; Subkey: "SOFTWARE\{#AppName}"; \
  ValueType: string; ValueName: "InstallVersion"; ValueData: "{#AppVersion}"; \
  Check: not IsAdminInstallMode

Root: HKLM; Subkey: "SOFTWARE\{#AppName}"; \
  ValueType: string; ValueName: "InstallScope"; ValueData: "machine"; \
  Flags: uninsdeletekey; Check: IsAdminInstallMode

Root: HKLM; Subkey: "SOFTWARE\{#AppName}"; \
  ValueType: string; ValueName: "InstallVersion"; ValueData: "{#AppVersion}"; \
  Check: IsAdminInstallMode

; ── AUTORIP_DATA_DIR environment variable ──────────────────────────────────
;
; The app reads this variable at startup to find the correct database location.
;   Per-user   → written to HKCU\Environment          (user scope env var)
;   All-users  → written to HKLM\SYSTEM\...Environment (machine scope env var)
;
; Both use REG_EXPAND_SZ so %APPDATA% / %PROGRAMDATA% expand correctly.

; Per-user install: user-scope environment variable
Root: HKCU; Subkey: "Environment"; \
  ValueType: expandsz; ValueName: "AUTORIP_DATA_DIR"; \
  ValueData: "%APPDATA%\AutoRipDVD"; \
  Flags: uninsdeletevalue; \
  Check: not IsAdminInstallMode

; All-users install: machine-scope environment variable
Root: HKLM; \
  Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; \
  ValueType: expandsz; ValueName: "AUTORIP_DATA_DIR"; \
  ValueData: "%PROGRAMDATA%\AutoRipDVD"; \
  Flags: uninsdeletevalue; \
  Check: IsAdminInstallMode

; ── [Icons] ──────────────────────────────────────────────────────────────────

[Icons]
; Start menu
Name: "{group}\{#AppName}";          Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; Desktop (optional task)
Name: "{userdesktop}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  Tasks: desktopicon

; ── [Run] ────────────────────────────────────────────────────────────────────

[Run]
; Offer to launch the app after install finishes
Filename: "{app}\{#AppExeName}"; \
  Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; \
  Flags: nowait postinstall skipifsilent

; ── [UninstallRun] ───────────────────────────────────────────────────────────

[UninstallRun]
; Broadcast WM_SETTINGCHANGE so running applications notice the removed env var
Filename: "{cmd}"; \
  Parameters: "/c ""setx AUTORIP_DATA_DIR """" 2>nul"""; \
  Flags: runhidden; RunOnceId: "ClearEnvVar"

; ── [Code] ───────────────────────────────────────────────────────────────────

[Code]

// ── Prerequisite detection ────────────────────────────────────────────────────

/// Returns true if .NET 10 Desktop Runtime (x64) is installed.
/// Detection: checks for the SharedFx directory under the dotnet installation.
function IsDotNet10Installed: Boolean;
var
  FindRec : TFindRec;
  DotNetBase : String;
begin
  Result := False;
  DotNetBase := ExpandConstant('{pf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if FindFirst(DotNetBase + '\10.*', FindRec) then
  begin
    Result := True;
    FindClose(FindRec);
  end;
end;

/// Returns true if Windows App SDK 1.5 runtime is present.
/// Detection: looks for the package registration key that the runtime installer writes.
function IsWinAppSdk15Installed: Boolean;
begin
  Result :=
    RegKeyExists(HKLM, 'SOFTWARE\Microsoft\WindowsAppRuntime\1.5') or
    RegKeyExists(HKCU, 'SOFTWARE\Microsoft\WindowsAppRuntime\1.5') or
    // Fallback: check for a known DLL shipped with the runtime
    FileExists(ExpandConstant('{pf64}\WindowsApps\Microsoft.WindowsAppRuntime.1.5') + '\*');
end;

// ── Prerequisite download + install ──────────────────────────────────────────

var
  PrereqPage : TOutputProgressWizardPage;

/// Downloads a file from URL to a local temp path; returns the path or '' on failure.
function DownloadFile(const URL, FileName: String): String;
var
  TargetPath : String;
begin
  TargetPath := ExpandConstant('{tmp}\') + FileName;
  DownloadTemporaryFile(URL, FileName, '', nil);
  if FileExists(TargetPath) then
    Result := TargetPath
  else
    Result := '';
end;

/// Runs a prerequisite installer silently.  Returns the exit code.
function RunSilent(const ExePath, Params: String): Integer;
var
  ResultCode : Integer;
begin
  if not Exec(ExePath, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    ResultCode := -1;
  Result := ResultCode;
end;

// ── Main init ─────────────────────────────────────────────────────────────────

function InitializeSetup: Boolean;
var
  MsgResult : Integer;
  TmpPath    : String;
begin
  Result := True;  // continue with setup

  // ── Check .NET 10 Desktop Runtime ──────────────────────────────────────────
  if not IsDotNet10Installed then
  begin
    MsgResult := MsgBox(
      '.NET 10 Desktop Runtime (x64) is required but was not found.' + #13#10 +
      #13#10 +
      'Click OK to download and install it now (requires an internet connection),' + #13#10 +
      'or Cancel to quit this installer.',
      mbConfirmation,
      MB_OKCANCEL);

    if MsgResult <> IDOK then
    begin
      Result := False;
      Exit;
    end;

    // Download .NET 10 Desktop Runtime
    try
      TmpPath := DownloadTemporaryFile(
        '{#DotNetUrl}',
        'dotnet-runtime-10-win-x64.exe',
        '',   // no SHA256 hash check
        nil);
    except
      MsgBox(
        'Failed to download .NET 10 Desktop Runtime.' + #13#10 +
        'Please download it manually from:' + #13#10 +
        'https://dotnet.microsoft.com/download/dotnet/10.0',
        mbError, MB_OK);
      Result := False;
      Exit;
    end;

    if RunSilent(TmpPath, '/install /quiet /norestart') > 1 then
    begin
      MsgBox(
        '.NET 10 Desktop Runtime installation may have failed.' + #13#10 +
        'Please install it manually and re-run this installer.',
        mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  // ── Check Windows App SDK 1.5 ─────────────────────────────────────────────
  if not IsWinAppSdk15Installed then
  begin
    MsgResult := MsgBox(
      'Windows App Runtime 1.5 is required but was not found.' + #13#10 +
      #13#10 +
      'Click OK to download and install it now (requires an internet connection),' + #13#10 +
      'or Cancel to quit this installer.',
      mbConfirmation,
      MB_OKCANCEL);

    if MsgResult <> IDOK then
    begin
      Result := False;
      Exit;
    end;

    try
      TmpPath := DownloadTemporaryFile(
        '{#WinAppSdkUrl}',
        'WindowsAppRuntimeInstall-x64.exe',
        '',
        nil);
    except
      MsgBox(
        'Failed to download Windows App Runtime.' + #13#10 +
        'Please download it manually from:' + #13#10 +
        'https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads',
        mbError, MB_OK);
      Result := False;
      Exit;
    end;

    if RunSilent(TmpPath, '--quiet') > 1 then
    begin
      MsgBox(
        'Windows App Runtime installation may have failed.' + #13#10 +
        'Please install it manually and re-run this installer.',
        mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;
end;

// ── Post-install: broadcast environment variable change ───────────────────────

procedure CurStepChanged(CurStep: TSetupStep);
var
  Dummy : DWORD;
begin
  if CurStep = ssDone then
  begin
    // Tell the Windows shell that environment variables changed so that
    // newly launched apps (Explorer, CMD prompts) pick up AUTORIP_DATA_DIR
    // without requiring a full logoff.
    SendBroadcastMessage($001A {WM_SETTINGCHANGE}, 0,
      PAnsiChar('Environment'));
  end;
end;

// ── Upgrade: keep existing data dir env var if upgrading ──────────────────────

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
end;

// ── Uninstall: summary page note about data directory ────────────────────────

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    MsgBox(
      '{#AppName} has been uninstalled.' + #13#10 + #13#10 +
      'Your rip history and settings database has been kept at:' + #13#10 +
      ExpandConstant('{userappdata}\AutoRipDVD') + #13#10 + #13#10 +
      'Delete that folder manually if you want to remove all data.',
      mbInformation, MB_OK);
  end;
end;
