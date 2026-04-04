# AutoRip DVD — UI Guide

This guide describes every panel in the application with layout diagrams.
Screenshots should be placed in `docs/screenshots/` using the filenames listed.

---

## Dashboard  `(screenshots/dashboard.png)`

The main view shown on launch. Shows active and recently completed jobs.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ AutoRip DVD v2.0                                           🔔 ⚙ —  □  ✕   │
├────────────┬─────────────────────────────────────────────────────────────────┤
│            │ ┌──── Active Jobs ──────────────────────────────────────────┐  │
│ 📊 Dashboard│ │                                                           │  │
│ 📋 Jobs    │ │  🎬 Inception (2010)                        Drive: D:     │  │
│ 📝 Logs    │ │  ████████████████████░░░░░  Ripping…  72%  [Cancel]      │  │
│ 🎬 Transcode│ │  ETA: 4m 32s  •  Speed: 8.2 MB/s  •  Temp: D:\Ripped    │  │
│ ⚙ Settings │ │                                                           │  │
│            │ │  🎬 The Dark Knight (2008)                Queued         │  │
│            │ └──────────────────────────────────────────────────────────┘  │
│            │                                                                 │
│            │ ┌──── Recently Completed ───────────────────────────────────┐  │
│            │ │  ✓  Dune Part Two (2024)   1080p H.265  4.2 GB  2m ago  │  │
│            │ │  ✓  Oppenheimer (2023)     1080p H.264  8.1 GB  1h ago  │  │
│            │ └──────────────────────────────────────────────────────────┘  │
│            │                                                                 │
│            │ ┌──── Drives ─────────────────────────────────────────────┐   │
│            │ │  D:  BD-RE  •  INCEPTION          [Rip]  [ISO]  [Info] │   │
│            │ └─────────────────────────────────────────────────────────┘   │
├────────────┴─────────────────────────────────────────────────────────────────┤
│  Auto-Rip: ● ON    D: Ripping 72%    Last: Completed 2m ago                  │
└──────────────────────────────────────────────────────────────────────────────┘
```

**Key controls:**
- **Auto-Rip toggle** (status bar) — enable/disable fully automatic mode
- **[Rip]** — start manual rip with title selection dialog
- **[ISO]** — create ISO image from current disc
- **[Info]** — show disc info dialog (protection, region, IFO structure)
- **[Cancel]** — cancel the active job

---

## Title Selection Dialog  `(screenshots/title_selection.png)`

Opens when clicking **[Rip]** in manual mode. Shows all titles found by MakeMKV.

```
┌──── Select Titles to Rip — INCEPTION ────────────────────────────────┐
│                                                                        │
│  ☑  Title 1  ★ Main Feature   2h 22m  7.8 GB  48 ch  H.264  1080p  │
│  ☐  Title 2  Bonus Feature    0h 43m  2.1 GB  12 ch  H.264  1080p  │
│  ☐  Title 3  Making Of        0h 28m  1.4 GB   8 ch  H.264  1080p  │
│  ☐  Title 4  Deleted Scenes   0h 08m  0.4 GB   4 ch  H.264  1080p  │
│                                                                        │
│  Audio:     ■ Eng DTS-HD 7.1  ■ Eng AC3 5.1  □ Fre AC3 5.1        │
│  Subtitles: ■ English [Default]  □ French  □ Spanish                │
│                                                                        │
│                    [Select All]  [Select None]  [OK]  [Cancel]       │
└────────────────────────────────────────────────────────────────────────┘
```

---

## DVD Shrink–Style Track Selector  `(screenshots/track_selector.png)`

Full track picker accessible from **Transcode → Track Selector** tab.

```
┌──── Track Selector — INCEPTION (2010) ──────────────────────────────────────────┐
│                                                                                   │
│  Source: D:\Ripped\Inception (2010).mkv  •  2h 22m  •  7.8 GB                  │
│                                                                                   │
│ ┌──── Video ──────────────────────────────────────────────────────────────────┐  │
│ │  H.264 High@4.1  •  1920×1080  •  23.976 fps  •  18.4 Mb/s  •  SDR  16:9  │  │
│ └─────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                   │
│ ┌──── Audio Tracks ───────────────────── Selected: 4.2 GB ───────────────────┐  │
│ │  ☑  1. DTS-HD MA  7.1  English  [Lossless]           ★   4.1 GB          │  │
│ │  ☐  2. AC3        5.1  English                            299 MB          │  │
│ │  ☐  3. AC3        5.1  French                             299 MB          │  │
│ │  ☐  4. AC3        2.0  English  Commentary               127 MB           │  │
│ │                              [Select All]  [Select None]  [Preferred Langs]│  │
│ └─────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                   │
│ ┌──── Subtitle Tracks ────────────────────── Selected: 12 MB ────────────────┐  │
│ │  ☑  1. PGS (Bitmap)  English   [Default]               6 MB               │  │
│ │  ☑  2. PGS (Bitmap)  English   [Forced]  🔥 Burn-in    6 MB               │  │
│ │  ☐  3. PGS (Bitmap)  French                             6 MB               │  │
│ │  ☐  4. PGS (Bitmap)  Spanish                            6 MB               │  │
│ │           [Select All]  [Select None]  [Clear Burn-in]  [Preferred Langs]  │  │
│ └─────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                   │
│ ┌──── Video Preview ──────────────────────────────── ☑ Subtitle Overlay ─────┐  │
│ │                                                                              │  │
│ │   [thumb1] [thumb2] [thumb3] [thumb4] [thumb5] [thumb6] [thumb7] [thumb8]  │  │
│ │                                                                              │  │
│ │  ┌──────────────────────────────────────────────────────────────────────┐  │  │
│ │  │                                                                      │  │  │
│ │  │            [  Large preview frame — 640×360 JPEG  ]                  │  │  │
│ │  │                                                                      │  │  │
│ │  │       They say we only use 10% of our brain...                      │  │  │
│ │  │                   (subtitle text rendered here)                      │  │  │
│ │  └──────────────────────────────────────────────────────────────────────┘  │  │
│ │  ├────────────────────────────────────────────────────────────────────┤   │  │
│ │  0:00                       1:11:30                           2:22:48  │  │  │
│ │                    ◄◄  ▐▌  ►► 01:11:30          Speed: 8.2 MB/s       │  │  │
│ └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                   │
│  Estimated output:  5.1 GB   (65% of source)            [Apply]  [Cancel]       │
└───────────────────────────────────────────────────────────────────────────────────┘
```

**Key interactions:**
- Click any filmstrip thumbnail to jump to that position
- Drag the seek bar to scrub to any frame
- Tick **Subtitle Overlay** to see a subtitle track rendered in the preview
- Click **🔥** on a subtitle row to assign it as the burn-in target (one at a time)
- **[Preferred Langs]** — auto-selects tracks matching the saved language preferences

---

## AnyDVD HD–Style Language Picker  `(screenshots/language_picker.png)`

Accessed from **Settings → Languages** or the **[Preferred Langs]** button.

```
┌──── Language Selection ────────────────────────────────────────────────────────┐
│                                                                                  │
│  [ Subtitle Languages ]  [ Audio Languages ]     🔍 Search: ___________        │
│                                                                                  │
│  🇸🇦 Arabic          🇰🇷 Korean                                               │
│  🇧🇬 Bulgarian       🇱🇻 Latvian                                               │
│  🇪🇸 Catalan         🇱🇹 Lithuanian                                            │
│  🇨🇳 Chinese (Simpl.)🇲🇾 Malay                                                │
│  🇹🇼 Chinese (Trad.) 🇳🇴 Norwegian                                             │
│  🇭🇷 Croatian        🇮🇷 Persian                                               │
│  🇨🇿 Czech           🇵🇱 Polski                                                │
│  🇩🇰 Danish          🇵🇹 Portuguese                                            │
│  🇳🇱 Dutch           🇧🇷 Portuguese (Brazil)                                  │
│  ████████████████████████████████████████ ← red highlight = selected           │
│  🇬🇧 English ●       🇷🇴 Romanian                                              │
│  🇪🇪 Estonian        🇷🇺 Russian                                               │
│  🇫🇮 Finnish         🇷🇸 Serbian                                               │
│  🇫🇷 Français        🇸🇰 Slovak                                                │
│  🇩🇪 Deutsch         🇸🇮 Slovenian                                             │
│  🇬🇷 Greek           🇪🇸 Spanish                                               │
│  🇮🇱 Hebrew          🇸🇪 Swedish                                               │
│  🇮🇳 Hindi           🇹🇭 Thai                                                  │
│  🇭🇺 Hungarian       🇹🇷 Turkish                                               │
│  🇮🇩 Indonesian      🇺🇦 Ukrainian                                             │
│  🇮🇹 Italian         🇻🇳 Vietnamese                                            │
│  🇯🇵 Japanese                                                                   │
│                                                                                  │
│  Selected: English                                                               │
│                                                                                  │
│  [Select All]  [Select None]     [Default]          [Save]  [Cancel]           │
└──────────────────────────────────────────────────────────────────────────────────┘
```

---

## Transcoding Panel  `(screenshots/transcode.png)`

Full HandBrake-style encoding UI under the **Transcode** section.

```
┌──── Transcode ─────────────────────────────────────────────────────────────────┐
│                                                                                  │
│  Source: Inception (2010).mkv         Output: Inception (2010)_encoded.mkv      │
│                                                                                  │
│ ┌──── Preset ─────────────────────────────────────────────────────────────────┐ │
│ │  Category: [General ▼]   Preset: [Fast 1080p30 ▼]   [Save Custom Preset]  │ │
│ │  H.264 MKV 1080p30 | HQ 1080p Surround | H.265 MKV | 4K H.265 | NVENC… │ │
│ └─────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                  │
│ ┌──── Video ────────────────────────────┐ ┌──── Audio ──────────────────────┐  │
│ │ Encoder: [H.264 (x264)         ▼]    │ │ Encoder: [AAC              ▼]  │  │
│ │ Quality: ○ RF  ● Bitrate              │ │ Bitrate: [160 kbps         ▼]  │  │
│ │ RF:  [──●────────] 22                 │ │ Mixdown: [Dolby Pro Logic II▼] │  │
│ │ Preset: [medium   ▼]  Tune: [film ▼] │ │ ☑ Passthrough TrueHD          │  │
│ │ Profile:[high     ▼]                  │ │ ☑ Passthrough DTS             │  │
│ │ ☐ 2-Pass  ☑ Turbo 1st Pass           │ │ ☐ Passthrough AC3             │  │
│ └───────────────────────────────────────┘ └────────────────────────────────┘  │
│                                                                                  │
│ ┌──── Picture ──────────────────────────┐ ┌──── Filters ────────────────────┐  │
│ │ Width:  [1920] Height: [1080]         │ │ ☐ Deinterlace [default    ▼]  │  │
│ │ ☑ Keep Aspect Ratio                   │ │ ☐ Detelecine (NTSC 3:2)       │  │
│ │ Crop: ☑ Auto  ○ Custom               │ │ ☐ Denoise  [medium ▼][film ▼] │  │
│ │ T:[0] B:[0] L:[0] R:[0]              │ │ ☐ Sharpen  [medium ▼]         │  │
│ │ HDR: [Pass-through  ▼]               │ │ ☐ Deblock  ☐ Grayscale        │  │
│ └───────────────────────────────────────┘ └────────────────────────────────┘  │
│                                                                                  │
│ ┌──── Output ───────────────────────────┐ ┌──── Subtitles ──────────────────┐  │
│ │ Format: [MKV ▼]  ☑ Chapter Markers   │ │ ☑ Include Subtitles            │  │
│ │ ☐ Optimize for Web (MP4 fast-start)  │ │ ☐ Burn First Subtitle          │  │
│ └───────────────────────────────────────┘ │ ☐ Forced Subtitles Only        │  │
│                                            └────────────────────────────────┘  │
│                                                                                  │
│  Progress: ████████████████████░░░░  72%   ETA: 4m 32s   45.2 fps              │
│                                                                                  │
│             [Track Selector]   [Language Picker]   [▶ Encode]  [■ Cancel]       │
└──────────────────────────────────────────────────────────────────────────────────┘
```

---

## Copy Protection Report  `(screenshots/protection_report.png)`

Shown in the Disc Info dialog before ripping.

```
┌──── Disc Analysis — INCEPTION ─────────────────────────────────────┐
│                                                                      │
│  Disc Type:   DVD-9 (Dual Layer)                                    │
│  Volume:      INCEPTION                                              │
│  Capacity:    7.84 GB  •  2 layers                                  │
│                                                                      │
│  ┌──── Copy Protection ─────────────────────────────────────────┐  │
│  │  ● CSS            Content Scramble System (decrypted by MKV) │  │
│  │  ○ ARccOS         Not detected                               │  │
│  │  ● UOPs           Skip restrictions present                  │  │
│  │  ○ APS            Not detected                               │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  Region Codes:  R1 (USA/Canada)  R4 (Latin America)                │
│                                                                      │
│  ┌──── IFO Structure ───────────────────────────────────────────┐  │
│  │  Title Sets: 4  •  Total Titles: 9                           │  │
│  │  Main Feature: Title 1 (2h 22m, 48 chapters)                 │  │
│  │  Audio Tracks: AC3 5.1 English, DTS 5.1 English, AC3 2.0    │  │
│  │  Subpictures: English, French, Spanish, Portuguese           │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                      │
│                              [Rip Disc]  [Create ISO]  [Close]      │
└──────────────────────────────────────────────────────────────────────┘
```

---

## ISO Creation Dialog  `(screenshots/iso_dialog.png)`

```
┌──── Create ISO — INCEPTION ──────────────────────────────────────────┐
│                                                                        │
│  Output: D:\Ripped\ISO\Inception (2010).iso                           │
│          [Browse…]                                                     │
│                                                                        │
│  ┌──── Creation Mode ─────────────────────────────────────────────┐  │
│  │  ● Raw Sector Copy (fast, no external tools required)          │  │
│  │    Sectors are CSS-encrypted — use MakeMKV/AnyDVD to decrypt  │  │
│  │                                                                │  │
│  │  ○ MakeMKV Decrypted (DRM-free ISO, requires mkisofs)         │  │
│  │    Strips CSS, produces a playable ISO image                   │  │
│  │                                                                │  │
│  │  ○ ImgBurn (handles ARccOS bad sectors, requires ImgBurn)     │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│  ☑ Eject disc after ISO is complete                                   │
│                                                                        │
│  Progress: ████████████████████░░░░  72%   8.1 MB/s   ETA: 4m 12s   │
│                                                                        │
│                             [▶ Create ISO]          [Cancel]          │
└────────────────────────────────────────────────────────────────────────┘
```

---

## Settings — Paths  `(screenshots/settings_paths.png)`

```
┌──── Settings ────────────────────────────────────────────────────────────┐
│  General  |  Paths  |  Languages  |  Ripping  |  Encoding  |  ISO  |... │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  Tool Paths                                                                │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │ MakeMKV:      C:\Program Files (x86)\MakeMKV\makemkvcon64.exe  [📁] │ │
│  │ HandBrake:    C:\Program Files\HandBrake\HandBrakeCLI.exe      [📁] │ │
│  │ ffmpeg:       C:\Program Files\ffmpeg\bin\ffmpeg.exe           [📁] │ │
│  │ ffprobe:      C:\Program Files\ffmpeg\bin\ffprobe.exe          [📁] │ │
│  │ mkvmerge:     C:\Program Files\MKVToolNix\mkvmerge.exe         [📁] │ │
│  │ Tesseract:    C:\Program Files\Tesseract-OCR\tesseract.exe     [📁] │ │
│  │ ImgBurn:      C:\Program Files (x86)\ImgBurn\ImgBurn.exe       [📁] │ │
│  │ mkisofs:      (auto-detect from PATH)                          [📁] │ │
│  └──────────────────────────────────────────────────────────────────────┘ │
│                                                                            │
│  Output Paths                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │ Ripped Media: D:\Ripped                                        [📁] │ │
│  │ ISO Images:   D:\Ripped\ISO                 (blank = same as above) │ │
│  │ Temp Work:    C:\Users\User\AppData\Local\Temp\AutoRipDVD      [📁] │ │
│  └──────────────────────────────────────────────────────────────────────┘ │
│                                                                            │
│  [Test All Paths]                        [Save]  [Cancel]                 │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Taking Screenshots

AutoRip DVD is a WinUI 3 application. To capture screenshots for this docs folder:

1. Build the app in **Release** mode
2. Run it and navigate to each panel
3. Use **Windows + Shift + S** (Snipping Tool) or **ShareX** to capture
4. Save with the filename shown above (e.g. `dashboard.png`)
5. Place them in `docs/screenshots/`

The README will automatically display them once present.
