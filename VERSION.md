# AutoRip DVD — Version History

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
