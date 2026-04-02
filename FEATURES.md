# AutoRip DVD - Complete Feature List

## 📀 Disc Detection & Ripping

### Automatic Detection
- **WMI-Based Monitoring** - Continuously monitors optical drives for disc insertion
- **DVD Detection** - Automatically identifies VIDEO_TS folder structure
- **Blu-ray Detection** - Recognizes BDMV folder structure
- **Multi-Drive Support** - Handles multiple optical drives simultaneously
- **Drive Information** - Displays detailed drive properties (manufacturer, model, capabilities)
- **Disc Information** - Shows disc type, capacity, protection, read speed, layers

### Title Selection & Filtering
- **MakeMKV-Style GUI** - Visual title browser with checkboxes
- **Main Feature Detection** - Automatically identifies the longest/primary title
- **Episode Filtering** - Separates TV episodes (15-90 minutes) from extras
- **Duration Filtering** - Configurable minimum title length threshold
- **Smart Extras Removal** - Filters trailers, menus, FBI warnings
- **Manual Override** - Select specific titles individually
- **Title Information** - Duration, size, chapters, codec, resolution per title
- **Batch Selection** - Select all/none with one click

### Copy Protection Handling
- **CSS Decryption** - Standard DVD copy protection
- **AACS Support** - Blu-ray AACS encryption
- **BD+ Support** - Blu-ray BD+ protection
- **CPPM Support** - Audio DVD protection
- **Region Code Bypass** - Works with any region disc
- **UOPs Removal** - Bypasses user operation prohibitions

### Ripping Quality
- **Lossless MKV** - Preserves 100% original quality
- **Quality Presets** - Original/High/Medium/Low
- **Multi-Audio** - All audio tracks or primary only
- **DTS Preservation** - Keeps lossless DTS audio
- **TrueHD Preservation** - Maintains Dolby TrueHD tracks
- **Dolby Atmos** - Preserves object-based audio
- **Chapter Markers** - Retains DVD/BD chapter points
- **All Subtitles** - Includes all subtitle tracks
- **Forced Subtitles** - Option for forced subs only
- **Multi-Angle** - Supports multiple camera angles
- **Picture-in-Picture** - BD PiP video tracks

---

## 🎬 Metadata Matching (FileBot-Style)

### Intelligent Parsing
- **Regex Title Extraction** - Advanced pattern matching
- **Year Detection** - Extracts (YYYY) from titles
- **Season/Episode** - S01E01, 1x01, "Season 1 Episode 1" formats
- **Multi-Part Episodes** - S01E01-E02 support
- **Anime Specials** - Handles OVA, Special, Movie tags
- **Disc Label Parsing** - Intelligent cleanup of disc labels
- **Roman Numerals** - Converts II, III, IV to 2, 3, 4

### Multiple Metadata Sources
- **TheMovieDB (TMDB)** - Comprehensive movie/TV database
  - Movie search with year filtering
  - TV show search with season/episode data
  - Poster and backdrop images
  - Genres, cast, crew information
  - Rating and vote count

- **OMDb API** - IMDb data access
  - IMDb ID resolution
  - Plot summaries
  - Ratings and reviews
  - Release dates

- **TheTVDB** - TV show specialist
  - Series and episode data
  - Air dates and networks
  - Episode titles and summaries
  - Season/episode artwork

- **AniDB** - Anime database
  - Anime series identification
  - Episode numbering
  - Japanese/English titles
  - Air dates and studios

### Fuzzy Matching
- **Levenshtein Distance** - Handles typos and variations
- **Case-Insensitive** - Matches regardless of capitalization
- **Non-Alphanumeric Removal** - Ignores special characters
- **Alternative Titles** - Searches known variations
- **Year Range Matching** - ±2 years tolerance
- **Manual Override** - Custom search queries

### Match History
- **SQLite Cache** - Stores successful matches
- **Instant Lookup** - Reuses previous matches
- **Match Confidence** - Score-based ranking
- **Source Tracking** - Records which API returned match
- **Timestamp Tracking** - When match was found

---

## 🎛️ HandBrake Integration

### Quality Presets
- **Fast 1080p30** - Quick encoding, good quality
- **HQ 1080p30** - High quality balanced encoding
- **Super HQ 1080p30** - Maximum quality at 1080p
- **4K** - 2160p Ultra HD encoding
- **Custom** - User-defined settings

### Video Encoding
- **x264 (H.264)** - Most compatible, fast
- **x265 (H.265/HEVC)** - 50% smaller files, slower
- **x265 10-bit** - Better color accuracy
- **VP9** - Google's codec, good quality
- **AV1** - Next-gen codec, best compression
- **Constant Quality (RF)** - RF 18-28 quality scale
- **Two-Pass Encoding** - Better quality distribution
- **Turbo First Pass** - Faster two-pass mode

### Hardware Acceleration
- **NVIDIA NVENC** - H.264 and H.265 GPU encoding
- **Intel QuickSync** - Fast CPU-integrated encoding
- **AMD VCE** - AMD GPU encoding
- **Auto-Detection** - Detects available hardware encoders
- **Fallback to Software** - Uses CPU if hardware unavailable

### x264/x265 Advanced Settings
- **Presets** - ultrafast to veryslow (speed vs quality)
- **Tuning** - film, animation, grain, stillimage
- **Profile** - baseline, main, high (compatibility)
- **Custom Options** - Direct encoder string override

### Audio Encoding
- **AAC** - High quality, very compatible
- **AC3** - Dolby Digital, 5.1 surround
- **E-AC3** - Dolby Digital Plus
- **MP3** - Universal compatibility
- **Opus** - Best quality at low bitrates
- **FLAC** - Lossless audio compression
- **Vorbis** - Ogg Vorbis open codec
- **Passthrough** - Copy original without re-encoding
- **Bitrate Control** - 96-320 kbps
- **Multi-Track** - Multiple audio streams in output

### Filters & Processing
- **Deinterlacing** - Removes interlacing artifacts
- **Denoise** - Reduces video noise (light/medium/strong)
- **Sharpen** - Enhances detail sharpness
- **Deblock** - Removes compression blocking
- **Decomb** - Selective deinterlacing
- **Detelecine** - Removes 3:2 pulldown

### Advanced Options
- **Process Priority** - Low/Normal/High CPU priority
- **Prevent Sleep** - Keeps system awake during encoding
- **Low Disk Space Pause** - Auto-pause at threshold (configurable GB)
- **Preview Scanning** - Number of preview frames (1-30)
- **LibDVDNav** - Enable/disable DVD navigation
- **Queue Management** - Batch process multiple files

---

## 📊 Job Management & Progress

### Queue System
- **Async Processing** - Background job execution
- **Priority Queue** - Order jobs by importance
- **Parallel Jobs** - Multiple simultaneous rips (multi-drive)
- **Job Cancellation** - Stop active jobs cleanly
- **Retry Logic** - Automatic retry on failure
- **Exponential Backoff** - Smart retry intervals

### Progress Tracking
- **Real-Time Progress** - Live % completion
- **ETA Calculation** - Estimated time remaining
- **Speed Metrics** - MB/s throughput
- **Stage Indication** - Detecting/Ripping/Transcoding status
- **Indeterminate Progress** - For scanning operations
- **Multi-Job View** - See all active jobs at once

### Job History
- **SQLite Database** - Persistent job storage
- **Completion Tracking** - Start/end timestamps
- **Status History** - All status changes logged
- **Output Paths** - Links to ripped files
- **Error Messages** - Full error details
- **Search & Filter** - Find past jobs
- **Export History** - CSV/JSON export

---

## 🔧 Advanced Configuration

### Path Management
- **MakeMKV Path** - Custom MakeMKV installation
- **HandBrake Path** - Custom HandBrake CLI location
- **Output Directory** - Where ripped files are saved
- **Temp Directory** - Intermediate processing files
- **Log Directory** - Application logs location
- **MakeMKV Data Dir** - MakeMKV settings folder

### Naming & Organization
- **Plex/Emby Structure** - Media server-compatible folders
  - `Movie Title (Year)/Movie Title (Year).mkv`
  - `Series Name/S01E01 - Episode Title.mkv`
- **Custom Templates** - User-defined naming patterns
- **Safe File Names** - Auto-sanitizes invalid characters
- **Year Inclusion** - (YYYY) appended to movie titles
- **Episode Numbers** - Zero-padded S01E01 format

### Notification System
- **Webhook Support** - Generic webhook POST
- **Slack Integration** - Direct Slack webhooks
- **Discord Integration** - Discord webhook messages
- **Completion Alerts** - Notify when jobs complete
- **Error Alerts** - Immediate failure notifications
- **Custom Payloads** - Configurable JSON bodies

### Logging & Debugging
- **Log Levels** - Minimal/Standard/Verbose/Debug
- **Rotating Logs** - Auto-rotate when size limit reached
- **Log Location** - Copy logs to video folder
- **Custom Log Path** - Specify log directory
- **MakeMKV Debug** - Enable MakeMKV debug output
- **HandBrake Verbose** - Detailed HandBrake logs
- **Auto-Cleanup** - Delete logs older than 30 days

### System Integration
- **Auto-Start** - Open discs on Windows disc insertion
- **System Tray** - Minimize to notification area
- **Start Minimized** - Launch hidden to tray
- **Auto-Eject** - Eject disc when ripping completes
- **Prevent Sleep** - Keep system awake during processing
- **Process Priority** - Control CPU usage impact

---

## 🎨 User Interface

### Modern WinUI 3 Design
- **Fluent Design** - Windows 11 native styling
- **Acrylic Backgrounds** - Translucent UI elements
- **Smooth Animations** - Fluid page transitions
- **Responsive Layout** - Adapts to window size
- **Touch Support** - Works with touch screens

### Theming
- **Light Mode** - Bright, clean interface
- **Dark Mode** - Easy on the eyes
- **System Theme** - Follows Windows settings
- **High Contrast** - Accessibility support

### Pages & Navigation
- **Dashboard** - Active job monitoring
- **Jobs Page** - Complete job history
- **Settings Page** - Windows 11-style categorized settings
  - General
  - Paths
  - API Keys
  - Ripping Options
  - MakeMKV Settings
  - HandBrake Settings
  - Notifications
  - Advanced
- **Logs Page** - Real-time log viewer
- **About Page** - Version info and credits

### Dialogs
- **Title Selection** - MakeMKV-style title browser
- **Disc Information** - Detailed drive/disc properties
- **Metadata Search** - Manual metadata lookup
- **Error Dialogs** - User-friendly error messages
- **Confirmation Prompts** - Prevent accidental actions

### Accessibility
- **Keyboard Navigation** - Full keyboard support
- **Screen Reader** - NVDA/JAWS compatible
- **High Contrast** - Themes for vision impairment
- **Focus Indicators** - Clear focus states
- **Alt Text** - Image descriptions

---

## 🔒 Security & Privacy

### Data Protection
- **Local Processing** - All ripping happens on-device
- **Encrypted API Keys** - Stored securely in SQLite
- **No Telemetry** - Zero usage tracking
- **No Cloud Upload** - Files stay on your machine
- **Secure Connections** - HTTPS for all API calls

### File Permissions
- **User-Level Access** - No admin rights required
- **Restricted Folders** - Proper Windows ACLs
- **Temp Cleanup** - Secure deletion of temp files

---

## 📈 Performance Optimizations

### Efficiency
- **Async/Await** - Non-blocking I/O operations
- **Parallel Processing** - Multi-core CPU utilization
- **Memory Management** - Efficient resource usage
- **Disk I/O** - Buffered read/write operations
- **API Rate Limiting** - Respects API quotas

### Resource Control
- **Process Priority** - User-configurable CPU priority
- **Thread Pool** - Managed threading
- **Memory Limits** - Prevents excessive RAM usage
- **Disk Space Monitoring** - Checks before operations

---

## 🔄 Future Roadmap

### v1.1 (Planned)
- [ ] ISO Image Creation
- [ ] CD Audio Ripping
- [ ] Multi-Language UI
- [ ] Custom Naming Templates
- [ ] OpenSubtitles Integration
- [ ] NFO File Creation
- [ ] Artwork Download

### v1.2 (Future)
- [ ] Network Share Support
- [ ] Web UI (Remote Access)
- [ ] Mobile App
- [ ] Cloud Backup (OneDrive/Google Drive)
- [ ] Container Support (Docker)
- [ ] Linux/macOS Support

### Community Requests
- [ ] 3D Blu-ray Support
- [ ] UHD 4K HDR Metadata
- [ ] Dolby Vision Preservation
- [ ] SRT Subtitle Extraction
- [ ] GPU HEVC 10-bit
- [ ] Batch Folder Ripping

---

## 📊 Comparison with Other Tools

| Feature | AutoRip DVD | MakeMKV | HandBrake | Automatic Ripping Machine | DVD Decrypter |
|---------|-------------|---------|-----------|---------------------------|---------------|
| **Auto Disc Detection** | ✅ | ❌ | ❌ | ✅ | ❌ |
| **GUI Interface** | ✅ Modern WinUI 3 | ✅ Qt | ✅ | ❌ CLI Only | ✅ Legacy |
| **Metadata Matching** | ✅ Multi-source | ❌ | ❌ | ⚠️ Basic | ❌ |
| **Copy Protection** | ✅ Via MakeMKV | ✅ | ❌ | ✅ | ⚠️ Old DVDs Only |
| **Quality Presets** | ✅ Both MKV & Transcode | ✅ MKV Only | ✅ Transcode Only | ⚠️ Limited | ❌ |
| **Hardware Encoding** | ✅ NVENC/QSV/VCE | ❌ | ✅ | ❌ | ❌ |
| **Batch Processing** | ✅ Queue System | ⚠️ Manual | ✅ | ✅ | ❌ |
| **Notifications** | ✅ Webhook/Slack/Discord | ❌ | ❌ | ⚠️ Basic | ❌ |
| **TV Episode Detection** | ✅ Advanced | ❌ | ❌ | ✅ | ❌ |
| **Title Filtering** | ✅ Smart + Manual | ⚠️ Manual Only | ❌ | ✅ | ⚠️ Basic |
| **Cross-Platform** | ❌ Windows Only | ✅ Win/Mac/Linux | ✅ | ✅ Linux | ❌ Windows Only |
| **Active Development** | ✅ 2026 | ✅ | ✅ | ✅ | ❌ Discontinued |
| **Open Source** | ✅ | ❌ | ✅ | ✅ | ❌ |

---

## 💡 What Makes AutoRip DVD Special?

### All-in-One Solution
Combines the best features from multiple tools:
- **MakeMKV** - Disc ripping and protection handling
- **HandBrake** - Professional transcoding with hardware acceleration
- **FileBot** - Intelligent metadata matching
- **ARM** - Automatic workflow and disc detection
- **Modern UI** - WinUI 3 with Windows 11 design

### User Experience Focus
- **Zero Configuration** - Works out of the box with sensible defaults
- **Guided Setup** - First-time wizard for configuration
- **Visual Feedback** - Always know what's happening
- **Error Recovery** - Gracefully handles failures
- **Documentation** - Comprehensive guides and tooltips

### Developer-Friendly
- **Clean Architecture** - MVVM pattern, separation of concerns
- **Extensible** - Plugin system for metadata sources
- **Well-Commented** - Code is readable and documented
- **Type-Safe** - Leverages C# strong typing
- **Async-First** - Non-blocking throughout

### Community-Driven
- **Open Development** - Public roadmap
- **Issue Tracking** - GitHub issues for bugs/features
- **Feature Votes** - Community prioritization
- **Pull Requests Welcome** - Contribution-friendly

---

**AutoRip DVD - Making disc ripping simple, fast, and intelligent.**
