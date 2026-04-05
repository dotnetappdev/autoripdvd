# AutoRip DVD  —  Professional DVD & Blu-ray Ripping Suite

**Version 2.1.0** · [Changelog](VERSION.md) · [Quick Start](QUICKSTART.md) · [Full Feature List](FEATURES.md) · [Deployment Guide](DEPLOYMENT.md)

A professional, open-source Windows application that combines the best of **MakeMKV**, **DVDFab**, **AnyDVD HD**, **DVD Shrink**, and **HandBrake** into a single modern WinUI 3 interface.  Insert a disc — AutoRip handles everything automatically, or let you take full control of every track, subtitle, language, and encoding detail.

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![WinUI 3](https://img.shields.io/badge/WinUI-3.0-0078D4?logo=microsoft)
![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows)
![License](https://img.shields.io/badge/license-MIT-green)
![Installer](https://img.shields.io/badge/installer-Inno%20Setup-blue)

---

## UI Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│  AutoRip DVD                                          🔔  ⚙  —  □  ✕  │
├────────────┬─────────────────────────────────────────────────────────────┤
│            │                                                              │
│  Dashboard │   Active Jobs                          Drive: D: [BDMV]     │
│  Jobs      │  ┌─────────────────────────────────────────────────────┐   │
│  Logs      │  │  🎬 Inception (2010)          Ripping…  ████░ 72%  │   │
│  Transcode │  │  🎬 The Dark Knight (2008)    Queued                │   │
│  Settings  │  └─────────────────────────────────────────────────────┘   │
│            │                                                              │
│            │   Recently Completed                                         │
│            │  ┌─────────────────────────────────────────────────────┐   │
│            │  │  ✓ Dune Part Two (2024)    1080p H.265  4.2 GB     │   │
│            │  └─────────────────────────────────────────────────────┘   │
├────────────┴─────────────────────────────────────────────────────────────┤
│  Auto-Rip: ON   Drive D: Ready   Last: Completed 2 min ago               │
└──────────────────────────────────────────────────────────────────────────┘
```

> Screenshots live in [`docs/screenshots/`](docs/screenshots/) — see the [UI Guide](docs/UI_GUIDE.md) for annotated panels.

---

## Feature Highlights

| Category | Feature | Inspired By |
|---|---|---|
| Disc Analysis | Binary IFO parser — reads VIDEO_TS directly | MakeMKV internals |
| Disc Analysis | CSS / ARccOS / AACS / BD+ detection | AnyDVD HD |
| Disc Analysis | Region code detection (DVD & Blu-ray) | DVDFab |
| Ripping | Full SINFO/TINFO stream parsing | MakeMKV robot mode |
| Ripping | Disc backup mode (raw folder copy) | MakeMKV backup |
| ISO | Raw sector copy via Win32 (like `dd`) | ImgBurn |
| ISO | MakeMKV decrypt + mkisofs ISO | DVDFab ISO mode |
| ISO | ImgBurn CLI with ARccOS skip | ImgBurn |
| Transcoding | 20-preset library (General/HQ/4K/HW) | HandBrake |
| Transcoding | Full audio pass-through matrix | HandBrake |
| Transcoding | HDR pass-through / tone-map to SDR | HandBrake |
| Transcoding | Subtitle burn-in / soft / SRT extract | HandBrake |
| Track Picker | Per-track audio/subtitle checkbox + size | DVD Shrink |
| Track Picker | Live video preview with filmstrip | DVD Shrink |
| Track Picker | Subtitle overlay preview per track | DVD Shrink |
| Disc Tree | Hierarchical title/track tree with checkboxes | MakeMKV |
| Disc Tree | Right-panel Info + editable name field | MakeMKV |
| Disc Tree | Disc-detection gating on toolbar buttons | AnyDVD HD |
| Installer | Inno Setup .exe with per-user / all-users choice | Standard Windows apps |
| Installer | MSIX packaging script for Windows Store | Microsoft Store |
| Languages | 40-language picker with flag emoji | AnyDVD HD |
| Languages | Preferred language auto-apply | AnyDVD HD |
| Media Info | ffprobe stream info (HDR, Atmos, DTS:X) | MediaInfo |
| Subtitles | SRT extraction, VOBsub→SRT OCR | SubRip |
| Subtitles | MKV subtitle mux via mkvmerge | MKVToolNix |
| Metadata | TMDB, OMDb, TVDB, AniDB | FileBot |

---

## What's New in v2.1

### MakeMKV-Style Disc Tree & Track Selection
- **Hierarchical disc tree** — the Title Selection dialog now shows a full tree: Disc root → Titles → Chapters / Video / Audio tracks / Subtitle tracks, exactly like MakeMKV's left panel
- **Per-track checkboxes** — include or exclude individual audio and subtitle streams before ripping; unchecking a title propagates to all its child tracks automatically
- **Two-column layout** — "Type" and "Description" column headers with proportional alignment across all tree levels
- **Right-panel Info** — clicking any tree node (title, audio track, subtitle, video stream) populates the Properties name field and a detailed Info text box on the right, mirroring MakeMKV's interface
- **Disc-detection gating** — the "Open Disc" and "Disc Info" toolbar buttons on the Dashboard are **disabled** until a DVD or Blu-ray disc is actually detected in a drive; they enable automatically when a disc is inserted and disable again on ejection
- **Auto-scan on Open** — when a disc is already in the drive, clicking "Open Disc" pre-scans it before showing the dialog so the tree is immediately populated

### Installer
- **Inno Setup Windows installer** (`installer/AutoRipDVD-Setup.iss`) with per-user and all-users install modes
- **MSIX packaging script** (`installer/package-msix.ps1`) for Windows Store and enterprise deployment
- Database location automatically determined from install scope (see [Database Location](#database-location) below)

---

## What's New in v2.0

### Disc Analysis Engine
- **IFO Parser** — reads DVD binary structure without any external tool: titles, chapters, audio/subpicture track attributes, region codes, copy-protection flags
- **Copy Protection Detector** — reports CSS, ARccOS bad-sector protection, RCE, APS/Macrovision, AACS, BD+, Cinavia, UOPs before ripping begins
- **Deep Stream Info** — full MakeMKV SINFO/TINFO parsing gives codec, language, resolution, fps, bitrate, channel count, forced/default flags per stream

### ISO Disc Image Dumping (3 modes)
- **Raw sector copy** — Win32 `CreateFile` direct device read, no external tools, ~full drive speed
- **MakeMKV decrypted** — strips CSS/AACS, produces a DRM-free ISO via mkisofs
- **ImgBurn** — sector-accurate with ARccOS bad-sector skipping

### HandBrake-Style Transcoding
- **20 built-in presets** across General, HQ, Super HQ, Matroska, Web, Devices, 4K, Hardware
- **Per-job overrides** — every preset option configurable per rip, not just globally
- **Output formats** — MKV, MP4, WebM, M4V
- **Audio passthrough matrix** — TrueHD, DTS/DTS-HD, AC3 with AAC fallback
- **HDR handling** — pass-through or tone-map to SDR
- **Filters** — deinterlace (+ presets), detelecine, denoise (+ tune), sharpen, deblock, grayscale

### AnyDVD HD–Style Language Picker
- 40 languages with flag emoji, native names, and red/green selection highlight
- Separate Subtitle / Audio language tabs
- Search filter, Select All/None, Reset to Defaults
- Saved to settings and auto-applied to every new disc

### DVD Shrink–Style Track Selector + Preview
- Per-track audio checkboxes with codec, channels, language, bitrate, Atmos/DTS:X badge
- Per-track subtitle checkboxes with forced/default flags, bitmap vs text type indicator
- **🔥 Burn-in** assignment — pick exactly which subtitle track to burn into video
- Estimated file-size per track and total selected size
- **Live video preview** with 8-frame filmstrip (skip first/last 5% for clean thumbnails)
- **Subtitle overlay preview** — renders chosen subtitle into the frame so you see the actual text before encoding
- Seek slider to any position in the title

### Subtitle Tools
- Extract all tracks to SRT/SUP via ffmpeg
- Convert VOBsub → SRT via Tesseract OCR
- Merge external SRT into MKV via mkvmerge (non-destructive)
- Chapter → SRT generation

---

## System Requirements

### Required
| Software | Version | Purpose |
|---|---|---|
| Windows 10 / 11 | 1809 (build 17763)+ | OS |
| .NET 8 Runtime | 8.0+ | App framework |
| MakeMKV | Latest | Disc decryption & ripping |

### Optional — unlock additional features
| Software | Purpose | Where to get |
|---|---|---|
| HandBrake CLI | Transcoding | [handbrake.fr](https://handbrake.fr/downloads2.php) |
| ffmpeg + ffprobe | Media analysis, preview frames, subtitle extraction | [ffmpeg.org](https://ffmpeg.org/download.html) |
| MKVToolNix | Subtitle muxing (mkvmerge) | [mkvtoolnix.download](https://mkvtoolnix.download/) |
| Tesseract OCR | VOBsub → SRT conversion | [github.com/UB-Mannheim/tesseract](https://github.com/UB-Mannheim/tesseract/wiki) |
| ImgBurn | ISO creation with ARccOS skip | [imgburn.com](https://www.imgburn.com/) |
| mkisofs / genisoimage | Folder → ISO wrapping | Via cdrtools on Windows |

### Hardware
- Optical drive (DVD-ROM or BD-ROM)
- **For raw ISO:** Administrator rights (Win32 device access)
- **For hardware encoding:** NVIDIA/Intel/AMD GPU with NVENC/QSV/VCE

---

## Installation

### Option A — Windows Installer (Recommended)

Download `AutoRipDVD-Setup-2.1.0.exe` from the [Releases page](../../releases) and run it.

The installer will:
1. Prompt you to choose **Install for all users** (requires admin) or **Current user only**
2. Automatically download and install **.NET 10 Desktop Runtime** and **Windows App SDK 1.5** if missing
3. Create a Start Menu shortcut and optional Desktop shortcut
4. Place your database in the correct location based on your choice (see [Database Location](#database-location))

```
AutoRipDVD-Setup-2.1.0.exe   — standard Inno Setup installer
```

If you prefer to install the Windows App Runtime manually (or via package manager), run:

```powershell
winget install --id Microsoft.WindowsAppRuntime -e
```

### Option B — Windows Store / MSIX

For enterprise or Microsoft Store distribution, use the MSIX packaging script:

```powershell
# 1. Publish the application first
dotnet publish AutoRipDVD\AutoRipDVD.csproj -c Release -r win-x64 `
    --self-contained false -o installer\publish-msix

# 2. Build the MSIX (test-signed, for local install)
.\installer\package-msix.ps1

# 3. Build production-signed MSIX (for Store submission)
.\installer\package-msix.ps1 -CertPath "certs\MyStore.pfx" -CertPassword "s3cr3t"

# 4. Install locally (enable sideloading first in Windows Settings)
Add-AppxPackage .\installer\dist\AutoRipDVD-2.1.0-x64.msix
```

> **Note:** The Windows Store requires a separate publisher certificate from Microsoft Partner Center.  
> The Inno Setup installer (`.exe`) and the MSIX are **complementary** — use the `.exe` for direct downloads and the `.msix` for Store / `winget` submission.

### Option C — Build from Source

```powershell
git clone https://github.com/dotnetappdev/autoripdvd2.git
cd autoripdvd2
dotnet build AutoRipDVD.sln -c Release
cd AutoRipDVD\bin\Release\net10.0-windows10.0.19041.0\win-x64
.\AutoRipDVD.exe
```

### Building the Installer from Source

```powershell
# 1. Publish to the expected location
dotnet publish AutoRipDVD\AutoRipDVD.csproj -c Release -r win-x64 `
    --self-contained false -o installer\publish

# 2. (Optional) Add assets
#    installer\assets\app.ico        — 256×256 application icon
#    installer\assets\wizard.bmp     — 164×314 Inno Setup wizard banner

# 3. Open in Inno Setup 6 and click Build → Compile
#    Output: installer\dist\AutoRipDVD-Setup-2.1.0.exe
#    Download Inno Setup: https://jrsoftware.org/isdl.php
```

---

## Database Location

AutoRip DVD stores its settings and rip history in a SQLite database.  The location
depends on how the app was installed — exactly the same convention used by applications
like VLC, Kodi, and MPC-HC:

| Install type | Database path |
|---|---|
| **All users** (installer, admin prompt accepted) | `C:\ProgramData\AutoRipDVD\autorip.db` |
| **Current user** (installer, no admin required) | `C:\Users\<you>\AppData\Roaming\AutoRipDVD\autorip.db` |
| **Standalone / built from source** | `C:\Users\<you>\AppData\Roaming\AutoRipDVD\autorip.db` |
| **MSIX / Windows Store** | `C:\Users\<you>\AppData\Local\Packages\SASProducts.AutoRipDVD\LocalState\autorip.db` |

The installer writes the `AUTORIP_DATA_DIR` environment variable (machine scope for  
all-users installs, user scope for per-user installs) which the app reads at startup to  
find the correct folder.  Uninstalling removes the variable but **leaves the data folder intact**
so you don't lose your history — delete it manually if needed.

---

## Development notes

- **Database path exposed:** The app now exposes the SQLite file location via `IDatabase.DatabasePath` and the Settings UI displays this path. When adding code that needs the DB path, resolve `IDatabase` from DI and read `DatabasePath`.
- **Folder picker (WinUI3):** Initialize `FolderPicker` with a window handle. Use `WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow)` and `WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd)`.
- **DI helper methods:** If you see `'GetRequiredService'` missing, add `using Microsoft.Extensions.DependencyInjection;` where `IServiceProvider` is used.
- **XAML compiler:** The WinUI XAML tool can fail if Windows App SDK tooling doesn't match the target. Ensure the Windows App SDK is installed and the project TFMs match the SDK version when building.


### Install Optional Tools

**MakeMKV** (required for ripping):
```
https://www.makemkv.com/download/
```

**ffmpeg** (preview thumbnails, subtitle extraction, media analysis):
```powershell
winget install Gyan.FFmpeg
# Installs to: C:\Program Files\ffmpeg\bin\ffmpeg.exe
```

**MKVToolNix** (subtitle muxing):
```powershell
winget install MKVToolNix.MKVToolNix
```

**Tesseract** (VOBsub → SRT OCR):
```powershell
winget install UB-Mannheim.TesseractOCR
```

### First-Run Setup
1. Launch AutoRip DVD
2. Open **Settings → Paths** and verify/set all tool paths
3. Open **Settings → API Keys** and add your OMDb key ([free at omdbapi.com](https://www.omdbapi.com/apikey.aspx))
4. Open **Settings → Languages** to set your preferred subtitle and audio languages
5. Insert a disc — the "Open Disc" and "Disc Info" buttons activate automatically

---

## Quick Start

### Fully Automatic (Fire and Forget)
1. Enable **Auto-Rip** in Settings
2. Insert a disc
3. AutoRip detects, analyses, fetches metadata, rips, transcodes, and ejects

### Manual with Full Control
1. Insert a disc — detection notification appears
2. Click **Select Titles** to open the DVD Shrink–style panel:
   - Browse the filmstrip preview
   - Tick/untick audio tracks by language
   - Tick/untick subtitle tracks; set burn-in target
3. Click **Language Picker** to set preferred languages globally
4. Click **Transcode Settings** to pick a preset or customise encoding
5. Click **Start Rip**

### ISO Dump
1. Insert a disc
2. Click **Create ISO** (or enable **Auto Create ISO** in Settings)
3. Choose mode: Raw (fast, encrypted) / Decrypted (DRM-free) / ImgBurn
4. Monitor progress in the Dashboard

---

## Output Structure

### Movies
```
D:\Ripped\
  └── Inception (2010)\
      └── Inception (2010).mkv
```

### TV Shows  
```
D:\Ripped\
  └── Breaking Bad\
      ├── S01E01 - Pilot.mkv
      ├── S01E02 - Cat's in the Bag.mkv
      └── ...
```

### ISO Images
```
D:\Ripped\ISO\
  ├── Inception (2010).iso
  └── Dune Part Two (2024).iso
```

---

## Architecture

```
AutoRipDVD.sln
├── AutoRipDVD                     Main WinUI 3 application
│   ├── Models/
│   │   ├── Models.cs              Core data classes + AppSettings
│   │   └── StreamModels.cs        Stream info, presets, protection models
│   ├── Services/
│   │   ├── DiscDetectionService   WMI drive monitoring
│   │   ├── IfoParserService       DVD IFO binary parser
│   │   ├── CopyProtectionService  CSS/AACS/BD+ detection
│   │   ├── MakeMkvService         MakeMKV CLI (TINFO/SINFO parsing)
│   │   ├── DiscAnalyzerService    Pre-rip disc analysis orchestrator
│   │   ├── IsoCreatorService      Raw/decrypted/ImgBurn ISO creation
│   │   ├── HandBrakeService       HandBrake CLI with TranscodePreset
│   │   ├── TranscodePresetService 20 built-in + custom presets
│   │   ├── FfprobeService         ffprobe JSON stream analysis
│   │   ├── DiscPreviewService     Filmstrip + subtitle overlay preview
│   │   ├── SubtitleService        Extract/convert/mux subtitles
│   │   ├── MetadataService        Multi-source metadata orchestrator
│   │   ├── FileNamingService      Plex/Emby-compatible path builder
│   │   ├── RipJobQueue            Async job pipeline
│   │   ├── SettingsService        SQLite-backed settings
│   │   ├── LogService             Rotating file logger
│   │   ├── SoundService           System sound notifications
│   │   └── NotificationService    Webhook (Slack/Discord)
│   └── ViewModels/
│       ├── MainViewModel          Dashboard
│       ├── TranscodeViewModel     HandBrake-style encoding UI
│       ├── TrackSelectorViewModel DVD Shrink-style track picker + preview
│       ├── SubtitleLanguagePickerViewModel  AnyDVD-style language grid
│       ├── TitleSelectionViewModel          MakeMKV-style title browser
│       ├── SettingsViewModel
│       ├── JobsViewModel
│       └── LogsViewModel
├── AutoRipDVD.Database            SQLite DAL
│   ├── DatabaseInitializer        Schema creation + migrations
│   └── Repositories/              Settings / Jobs / MatchHistory
└── AutoRipDVD.MetadataSources     Pluggable metadata providers
    └── Sources/                   TMDB / OMDb / TVDB / AniDB
```

---

## Ripping Pipeline (detailed)

```
Disc inserted
    │
    ▼
┌─────────────────────────────┐
│  1. Disc Analysis           │  IFO parse + protection detect
│     (before any ripping)    │  Reports CSS/AACS/regions
└──────────────┬──────────────┘
               │
    ▼
┌─────────────────────────────┐
│  2. MakeMKV Scan            │  TINFO + SINFO → stream details
│     (full title list)       │  Audio codecs, langs, resolutions
└──────────────┬──────────────┘
               │
    ▼
┌─────────────────────────────┐
│  3. Metadata Fetch          │  TMDB / OMDb / TVDB / AniDB
│     (title + year + type)   │  Fuzzy match, history cache
└──────────────┬──────────────┘
               │
    ▼
┌─────────────────────────────┐
│  4. Title Filter            │  Main feature / episodes / extras
│     + Language Apply        │  Preferred audio + subtitle langs
└──────────────┬──────────────┘
               │
    ▼
┌─────────────────────────────┐
│  5. Rip via MakeMKV         │  Decrypt → lossless MKV to temp
│     (progress 0–70 %)       │
└──────────────┬──────────────┘
               │
    ├──────────┤  (if AutoCreateIso)
    ▼          ▼
┌──────────┐ ┌────────────────┐
│  6a. ISO │ │  6b. Transcode │  HandBrake preset → output format
│  Creation│ │  (70–100 %)    │  Track selection, subtitle handling
└──────────┘ └────────────────┘
               │
    ▼
┌─────────────────────────────┐
│  7. Eject + Notify          │  Sound + webhook notification
└─────────────────────────────┘
```

---

## Settings Reference

### Paths
| Setting | Default | Description |
|---|---|---|
| MakeMkvPath | `C:\...\makemkvcon64.exe` | MakeMKV executable |
| HandBrakePath | `C:\...\HandBrakeCLI.exe` | HandBrake CLI |
| FfmpegPath | `C:\...\ffmpeg.exe` | ffmpeg (preview + subtitles) |
| FfprobePath | `C:\...\ffprobe.exe` | ffprobe (stream analysis) |
| MkvMergePath | `C:\...\mkvmerge.exe` | MKVToolNix merge |
| TesseractPath | `C:\...\tesseract.exe` | OCR for VOBsub→SRT |
| ImgBurnPath | `C:\...\ImgBurn.exe` | ImgBurn ISO mode |
| OutputPath | `D:\Ripped` | Final media output |
| IsoOutputPath | _(same as OutputPath)_ | ISO file output |
| TempPath | `%TEMP%\AutoRipDVD` | Working directory |

### Language Preferences
| Setting | Example | Description |
|---|---|---|
| PreferredAudioLanguages | `eng,fra` | ISO 639-2 codes, comma-separated |
| PreferredSubtitleLanguages | `eng` | Applied to every new disc |

### ISO Settings
| Setting | Default | Description |
|---|---|---|
| AutoCreateIso | `false` | Create ISO automatically after ripping |
| DefaultIsoMode | `RawSectorCopy` | `RawSectorCopy` / `MakeMkvDecrypted` / `ImgBurn` |
| EjectAfterIso | `true` | Eject disc once ISO is finished |
| VerifyIsoAfterCreation | `false` | Verify sector count after writing |

### Preview
| Setting | Default | Description |
|---|---|---|
| FilmstripFrameCount | `8` | Number of filmstrip thumbnails |
| PreviewWidth | `640` | Main preview image width (px) |
| PreviewHeight | `360` | Main preview image height (px) |
| AutoLoadPreview | `true` | Auto-generate filmstrip on source open |
| PreviewSubtitleOverlay | `true` | Allow subtitle rendering in preview |

---

## Transcoding Presets

| Preset | Category | Encoder | Quality | Audio |
|---|---|---|---|---|
| Fast 480p30 | General | x264 | RF 22 | AAC Stereo |
| Fast 720p30 | General | x264 | RF 22 | AAC DPL2 |
| Fast 1080p30 | General | x264 | RF 22 | AAC DPL2 |
| HQ 720p30 Surround | HQ | x264 | RF 20 | AAC 5.1 |
| HQ 1080p30 Surround | HQ | x264 | RF 20 | AAC 5.1 |
| Super HQ 1080p30 | Super HQ | x264 | RF 18 | AAC 5.1, 2-pass |
| Super HQ 2160p60 4K | Super HQ | x265 10-bit | RF 18 | AAC 7.1 |
| H.264 MKV 1080p30 | Matroska | x264 | RF 20 | Passthrough |
| H.265 MKV 1080p30 | Matroska | x265 | RF 22 | Passthrough |
| H.265 MKV 2160p (4K) | Matroska | x265 10-bit | RF 20 | Passthrough |
| Web 720p30 | Web | x264 | RF 23 | AAC Stereo |
| Web 1080p30 | Web | x264 | RF 22 | AAC DPL2 |
| Apple TV 4K | Devices | x265 10-bit | RF 20 | AAC 5.1 |
| Chromecast 1080p | Devices | x264 | RF 22 | AAC DPL2 |
| Android 720p | Devices | x264 | RF 23 | AAC Stereo |
| 4K H.265 2160p | 4K | x265 10-bit | RF 18 | Passthrough |
| 4K AV1 2160p | 4K | AV1 | CQ 28 | Opus |
| NVIDIA NVENC H.264 1080p | Hardware | NVENC H.264 | RF 22 | AAC DPL2 |
| NVIDIA NVENC H.265 1080p | Hardware | NVENC H.265 | RF 22 | AAC DPL2 |
| Intel QuickSync H.264 1080p | Hardware | QSV H.264 | RF 22 | AAC DPL2 |
| AMD VCE H.264 1080p | Hardware | VCE H.264 | RF 22 | AAC DPL2 |

Custom presets are saved to `%AppData%\AutoRipDVD\custom_presets.json`.

---

## Supported Languages (Subtitle / Audio Picker)

Arabic · Bulgarian · Catalan · Chinese (Simplified) · Chinese (Traditional) · Croatian · Czech · Danish · Dutch · **English** · Estonian · Finnish · French · German · Greek · Hebrew · Hindi · Hungarian · Indonesian · Italian · Japanese · Korean · Latvian · Lithuanian · Malay · Norwegian · Persian · Polish · Portuguese · Portuguese (Brazil) · Romanian · Russian · Serbian · Slovak · Slovenian · Spanish · Swedish · Thai · Turkish · Ukrainian · Vietnamese

---

## Troubleshooting

### Disc not detected
- Check drive is ready in Windows Explorer
- Restart disc detection: Settings → Advanced → Restart Detection

### Raw ISO fails with "Access Denied"
- **Run AutoRip DVD as Administrator** — Win32 raw device access (`\\.\D:`) requires elevated privileges

### Preview thumbnails not appearing
- Set **FfmpegPath** and **FfprobePath** in Settings → Paths
- Install ffmpeg: `winget install Gyan.FFmpeg`

### Subtitle overlay not rendering
- PGS/VOBsub overlay needs ffmpeg ≥ 5.0
- Check **PreviewSubtitleOverlay** is enabled in Settings

### MakeMKV decrypted ISO fails
- Install mkisofs: download cdrtools for Windows or use `winget install oscdimg`
- Set **MkisofsPath** in Settings if auto-detect fails

### VOBsub → SRT conversion poor quality
- Install Tesseract language data for the disc language (`tesseract-ocr-xxx.exe`)
- Bitmap subtitles have OCR accuracy limits; SDH text tracks produce better results

---

## External Tool CLI Reference

### MakeMKV Robot Mode
```powershell
# Scan disc (returns TINFO/SINFO/CINFO lines)
makemkvcon64.exe -r --cache=1 info disc:0

# Rip title 0 to folder
makemkvcon64.exe -r mkv disc:0 0 "D:\Temp"

# Full backup (decrypt)
makemkvcon64.exe -r --decrypt backup disc:0 "D:\Backup"
```

### HandBrake CLI
```powershell
# Encode with preset
HandBrakeCLI.exe --preset "H.265 MKV 1080p30" -i "input.mkv" -o "output.mkv"

# Custom encode (x265, RF20, AAC 5.1, all tracks, burned subtitles)
HandBrakeCLI.exe -i "input.mkv" -o "output.mkv" --encoder x265 --quality 20 `
  --aencoder av_aac --ab 320 --mixdown 5point1 --all-audio `
  --subtitle 1 --subtitle-burned 1 --markers --format av_mkv
```

### ffprobe Stream Info
```powershell
ffprobe -v quiet -print_format json -show_streams -show_chapters "input.mkv"
```

### ISO Creation (raw)
```powershell
# Equivalent of what AutoRip does internally
dd if=\\.\D: of="output.iso" bs=2048   # requires dd for Windows
```

---

## Contributing

1. Fork the repository
2. Create a branch: `git checkout -b feature/my-feature`
3. Commit your changes
4. Push and open a Pull Request

Please keep each PR focused on a single feature or fix.

---

## Acknowledgements

| Tool / Project | Role |
|---|---|
| [MakeMKV](https://www.makemkv.com/) | Disc decryption & ripping engine |
| [HandBrake](https://handbrake.fr/) | Video transcoding |
| [ffmpeg / ffprobe](https://ffmpeg.org/) | Media analysis, preview, subtitle extraction |
| [MKVToolNix](https://mkvtoolnix.download/) | MKV muxing |
| [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) | Bitmap subtitle OCR |
| [OMDb API](https://www.omdbapi.com/) | Movie / TV metadata |
| [TMDB](https://www.themoviedb.org/) | Movie / TV metadata + artwork |
| [TheTVDB](https://thetvdb.com/) | TV show metadata |
| [AniDB](https://anidb.net/) | Anime metadata |
| [FileBot](https://www.filebot.net/) | Naming convention inspiration |
| [Automatic Ripping Machine](https://github.com/automatic-ripping-machine/automatic-ripping-machine) | Auto-rip pipeline inspiration |
| [DVD Shrink](https://www.dvdshrink.org/) | Track selector UI inspiration |
| [AnyDVD HD](https://www.redfox.biz/anydvdhd.html) | Language picker UI inspiration |

---

## License

MIT License — see [LICENSE](LICENSE) for details.

**Disclaimer:** This software is intended for making personal backup copies of media you legally own. Respect copyright law in your jurisdiction.
