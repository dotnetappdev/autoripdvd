# AutoRip DVD — Version History

---

## Version 2.1.0 (April 2026) — Installer & MakeMKV-Style UI

### New: Windows Installer (Inno Setup)

- **`installer/AutoRipDVD-Setup.iss`** — full Inno Setup 6 script producing
  `AutoRipDVD-Setup-2.1.0.exe`
- **Install scope selection** — first page of the installer lets the user choose:
  - **All users** (elevated / admin) — installs to `%ProgramFiles%\AutoRip DVD`,
    data directory `%PROGRAMDATA%\AutoRipDVD\`
  - **Current user only** (no elevation needed) — installs to
    `%LOCALAPPDATA%\Programs\AutoRip DVD`, data directory `%APPDATA%\AutoRipDVD\`
- **Prerequisite auto-install** — checks for .NET 10 Desktop Runtime and
  Windows App SDK 1.5; downloads and installs them silently if missing
- **AUTORIP_DATA_DIR environment variable** written by the installer (machine scope for
  all-users, user scope for per-user) so the app always finds the right database
- **Clean uninstall** — removes shortcuts, registry keys, env var; data folder is
  intentionally preserved (displayed in a message box at end of uninstall)
- **Optional desktop shortcut** (unchecked by default, standard installer behaviour)
- `WM_SETTINGCHANGE` broadcast after install so running apps pick up the new env var
  without requiring a restart

### New: MSIX / Windows Store Packaging

- **`installer/package-msix.ps1`** — PowerShell script that:
  1. Runs `dotnet publish` into a staging folder
  2. Generates a `Package.appxmanifest` template if one is absent
  3. Uses the Windows SDK `makeappx.exe` to pack the MSIX
  4. Signs the package with a provided `.pfx` cert or a temporary self-signed cert
  5. Optionally submits to Windows Store via `winappstore-cli`
- **`installer/Package.appxmanifest`** (generated on first run) — Store manifest with
  correct package identity, dependencies on .NET 10 and Windows App SDK 1.5,
  `removableStorage` restricted capability for optical drive access
- Separate from Inno Setup: the `.exe` installer is for direct distribution, the
  `.msix` is for Store / `winget` / enterprise deployment

### New: MakeMKV-Style Disc Tree

- **`DiscTreeNodes.cs`** — tree node hierarchy:
  `DiscRootNode → TitleTreeNode → ChaptersNode / VideoTrackNode / AudioTrackNode / SubtitleTrackNode`
- **Two-panel title selection dialog** (1060 px wide):
  - Left: `TreeView` with "Type / Description" column headers; each row has a
    checkbox, icon, type label, and description; all nodes auto-expanded on scan
  - Right: Output folder (read-only, from Settings), Properties (editable title name,
    Profile selector), Info text area (selected node details), selection badge
- **Per-track include/exclude** — audio and subtitle track checkboxes are independent;
  unchecking a title automatically unchecks all its child tracks
- **Right-panel info** — every node type provides a `BuildInfoText()` that fills the
  info area: title information (name, source file, duration, size, chapters, segment
  map, output filename), audio track details, subtitle track details, video codec info
- **Selected node name editing** — the Name field in Properties is editable for title
  nodes; changes write back to `TitleInfo.Name` for output file naming

### New: Disc-Detection Gating

- `MainViewModel.HasDiscInserted` — observable bool updated live via
  `IDiscDetectionService.DiscInserted` / `DiscEjected` events and on startup
- `MainViewModel.DetectedDriveLetter` — drive letter of the first detected disc
- Dashboard **"Open Disc"** and **"Disc Info"** buttons bound to `HasDiscInserted`;
  they are **disabled (greyed out) until a DVD or Blu-ray is detected** and re-enable
  automatically on ejection/re-insertion
- "Open Disc" pre-populates the drive letter from `DetectedDriveLetter` and
  auto-triggers a scan before opening the Title Selection dialog

### Database path resolution (`DatabaseInitializer`)

- `GetDefaultDbPath()` now checks `AUTORIP_DATA_DIR` (machine scope first, then user scope)
  before falling back to `%APPDATA%\AutoRipDVD` — enabling the installer to direct
  all-users installs to `%PROGRAMDATA%\AutoRipDVD` with no code changes
- `EnvironmentVariableTarget` calls are wrapped in a try/catch for non-Windows
  compatibility

### Version bump

- `AutoRipDVD.csproj` — `<Version>2.1.0</Version>`, `<AssemblyVersion>2.1.0.0</AssemblyVersion>`

---

## Version 2.0.0 (April 2026) — Major Feature Release

### New: Disc Analysis Engine
- **IfoParserService** — reads DVD binary IFO files directly (no external tool):
  title structure, chapter timestamps from Cell Playback Info Table, audio/subpicture track
  attributes (codec, channels, language, sample rate), region codes, protection flags
- **CopyProtectionService** — detects CSS (VOB pack header check), ARccOS bad sectors,
  RCE region lock, APS/Macrovision, AACS (Certificate dir), BD+, Cinavia, UOPs
- **DiscAnalyzerService** — orchestrates IFO parse + protection scan into a
  `DiscAnalysisResult` before any ripping starts
- MakeMKV SINFO/TINFO full parser — codec, language, resolution, fps, bitrate, channel
  count, forced/default flags per stream; drive listing; disc backup mode

### New: ISO Disc Image Dumping
- **Raw sector copy** — Win32 `CreateFile` + `ReadFile` direct device I/O, no external
  tools, reports MB/s speed and ETA
- **MakeMKV decrypted ISO** — `backup --decrypt` → mkisofs/genisoimage/oscdimg for
  DRM-free ISO with correct UDF+ISO-9660 bridge filesystem
- **ImgBurn CLI** — `/MODE READ` with ARccOS bad-sector skip
- New `RipStatus.CreatingIso` pipeline step; `RipJob.IsoPath` property

### New: HandBrake-Style Transcoding
- **TranscodePresetService** — 20 built-in presets (General, HQ, Super HQ, Matroska,
  Web, Devices, 4K, Hardware); custom preset save/load via JSON
- **TranscodePreset record** — full encoding spec: video encoder, quality, bitrate mode,
  resolution, crop, all filter options, audio passthrough matrix, subtitle handling, HDR
- **TranscodeWithPresetAsync** — per-job track selection overrides, subtitle burn-in,
  HDR tone mapping, process priority, multi-format output (MKV/MP4/WebM/M4V)
- Output format selector, AudioMixdown enum

### New: AnyDVD HD–Style Language Picker
- **SubtitleLanguagePickerViewModel** — 40 languages with ISO 639-2, English + native
  names, flag emoji, red selection highlight
- Dual tabs (Subtitle / Audio), live search, SelectAll/None, Save, Reset to Defaults
- Preferences saved to AppSettings as comma-separated ISO 639-2 codes

### New: DVD Shrink–Style Track Selector + Live Preview
- **TrackSelectorViewModel** — per-track audio/subtitle checkboxes with codec, channels,
  language, bitrate, Atmos/DTS:X badge, estimated size contribution
- 🔥 Burn-in assignment per subtitle track (only one at a time)
- **DiscPreviewService** — filmstrip of 8 evenly-spaced JPEG thumbnails via ffmpeg
- Seek slider (0–100 %) with live preview update
- **Subtitle overlay preview** — renders chosen subtitle into frame so user sees actual text
- Animated WebP clip extraction; frame cache with auto-pruning

### New: Media Analysis
- **FfprobeService** — JSON stream analysis: VideoStreamInfo (HDR10/HLG detection,
  colour primaries, bit depth), AudioStreamInfo (Atmos/DTS:X), SubtitleStreamInfo,
  ChapterInfo, container format

### New: Subtitle Tools
- SRT extraction from MKV via ffmpeg
- VOBsub (.idx/.sub) → SRT via Tesseract OCR
- Merge external SRT into MKV via mkvmerge (non-destructive)
- Chapter → SRT generation
- Forced subtitle auto-detection

### Settings additions
- Tool paths: ffmpeg, ffprobe, mkvmerge, mkvextract, Tesseract, ImgBurn, mkisofs
- ISO: AutoCreateIso, DefaultIsoMode, IsoOutputPath, EjectAfterIso, etc.
- Preview: FilmstripFrameCount, PreviewWidth/Height, ThumbnailSize, AutoLoadPreview
- Audio/Subtitle: DefaultOutputFormat, DefaultAudioMixdown, language prefs,
  passthrough flags, AudioGainDb, BurnForcedSubtitles, ExtractSubtitlesToSrt
- Picture: AutoCrop, KeepAspectRatio, MaxWidth/Height, HdrHandling
- Filters: DeinterlacePreset, EnableDetelecine, DenoiseTune, SharpenPreset, Grayscale

---

## Version 1.0.0 (April 2026) — Initial Release

- Automatic DVD/Blu-ray disc detection via WMI
- MakeMKV integration (ripping)
- HandBrake CLI integration (transcoding)
- FileBot-style intelligent title parsing
- OMDb / TVDB / TMDB / AniDB metadata sources
- Smart title filtering (main feature, episodes, extras)
- Async job queue with cancellation
- WinUI 3 Fluent Design interface
- Light/Dark theme support
- Webhook notifications (Slack, Discord)
- SQLite job history and match history
- Rotating log files with verbosity control
- Sound notifications (completion, error, eject)

---

## Versioning

This project uses [Semantic Versioning](https://semver.org/): **MAJOR.MINOR.PATCH**
