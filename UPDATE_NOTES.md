# AutoRip DVD - Update Notes

## Latest Release - v1.0.0 (April 2, 2026)

### 🎉 Major Features Added

#### Comprehensive HandBrake Features  
- **Advanced Video Encoding**
  - Support for x264, x265 (8-bit and 10-bit), VP9, and AV1 codecs
  - Hardware acceleration: NVIDIA NVENC, Intel QuickSync, AMD VCE
  - Two-pass encoding with turbo first pass option
  - x264/x265 presets (ultrafast to veryslow) and tuning (film, animation, grain)
  - Custom encoder options for advanced users
  - Constant Quality (RF) encoding from RF 18-28

- **Audio Encoding Options**
  - Multiple codecs: AAC, AC3, E-AC3, MP3, Opus, FLAC, Vorbis
  - Audio passthrough for lossless preservation
  - Configurable bitrate from 96-320 kbps
  - Multi-track audio support

- **Video Filters**
  - Deinterlacing with multiple algorithms
  - Denoise (light/medium/strong presets)
  - Sharpen for detail enhancement  
  - Deblock to remove compression artifacts
  - Decomb for selective deinterlacing
  - Detelecine for 3:2 pulldown removal

- **System Integration**
  - Process priority control (Low/Normal/High)
  - Prevent system sleep during encoding
  - Auto-pause on low disk space (configurable threshold)
  - Preview frame scanning (1-30 frames)
  - LibDVDNav toggle

#### Enhanced Settings UI with Unsaved Changes Detection

**NEW: Professional Settings Management**
- **Apply Button** - Save changes without closing (Ctrl+S shortcut)
- **Cancel Button** - Discard all changes and restore original values
- **Change Tracking** - Real-time detection of modified settings
- **Unsaved Changes Warning** - InfoBar appears when settings are modified
- **Navigation Guard** - Warns before leaving with unsaved changes
  - "Save" - Applies changes and navigates away
  - "Discard" - Cancels changes and navigates away
  - "Cancel" - Stays on settings page

**Benefits:**
- ✅ Never lose settings by accident
- ✅ Clear visual indicator when changes are pending
- ✅ Matches Windows 11 and professional app UX patterns
- ✅ Keyboard shortcut (Ctrl+S) for quick saves
- ✅ Tooltips and helpful hints

#### Advanced Quality Presets

**MakeMKV Quality Options:**
- Original - Full quality, lossless (default)
- High - Minimal compression, near-lossless
- Medium - Balanced quality vs file size
- Low - Maximum compression for space savings

**Quality Preservation Controls:**
- DTS audio preservation toggle
- TrueHD/Dolby Atmos preservation
- Include all audio tracks or primary only
- All subtitles or forced only
- Chapter marker preservation

#### Multi-Source Metadata Integration

**Four Metadata Sources:**
1. **TheMovieDB (TMDB)** - Movies and TV shows
   - Comprehensive database with high-quality metadata
   - Posters, backdrops, and artwork
   - Cast, crew, and genre information

2. **OMDb API** - IMDb data access
   - IMDb ratings and reviews
   - Plot summaries
   - Release dates

3. **TheTVDB** - TV show specialist
   - Episode titles and air dates
   - Season/episode artwork
   - Network and series information

4. **AniDB** - Anime database
   - Anime-specific metadata
   - Japanese and English titles
   - Episode numbering for anime

**Smart Matching:**
- Fuzzy matching algorithms
- Levenshtein distance for typo handling
- Year-based matching (±2 years tolerance)
- Alternative title search
- Match history caching in SQLite

#### Enhanced Data Models

**New Enums:**
- `MakeMKVQuality` - Quality preset selection
- `VideoEncoderType` - 11 video encoder options
- `AudioEncoderType` - 9 audio encoder options
- `ProcessPriorityLevel` - CPU priority levels
- `LogVerbosity` - Logging detail levels

**Extended AppSettings (50+ properties):**
- All MakeMKV configuration options
- All HandBrake advanced settings
- Notification system settings (Slack, Discord, webhooks)
- UI preferences (theme, advanced mode, system tray)
- Path management (temp, logs, data directories)

#### Professional Logging System

**Log Levels:**
- Minimal - Errors only
- Standard - Info + Errors (default)
- Verbose - Detailed operation logs
- Debug - Everything including debug info

**Log Management:**
- Copy logs to video output folder
- Custom log location
- Auto-cleanup (delete logs older than 30 days)
- Rotating logs to prevent disk overflow
- MakeMKV and HandBrake debug output

#### Notification System

**Supported Platforms:**
- Generic webhook (POST with JSON payload)
- Slack integration (direct webhooks)
- Discord integration (Discord webhooks)
- Pushbullet, IFTTT, and other webhook services

**Notification Types:**
- Completion alerts (when ripping finishes)
- Error alerts (immediate failure notifications)
- Custom payload configuration

### 🐛 Bug Fixes

- **Fixed:** SQLite SettingsService syntax error (duplicate closing braces)
- **Fixed:** System.Text.Json security vulnerability (upgraded from 8.0.0 to 8.0.5)
- **Fixed:** OutputBasePath vs OutputPath property mismatch in settings
- **Fixed:** VideoQuality vs Quality property mismatch
- **Fixed:** TranscodeAfterRip vs AutoTranscode property mismatch
- **Fixed:** Duplicate BrowseHandBrake command declaration

### 📄 Documentation Updates

#### README.md
- Comprehensive feature list with 60+ features detailed
- System requirements (minimum and recommended)
- Installation guide (build from source + pre-built binary)
- Quick start guide
- API key setup instructions
- Usage guide (automatic and manual modes)
- Output structure examples
- Advanced configuration
- Feature comparison table
- Troubleshooting section

#### FEATURES.md (NEW)
- Complete feature catalog organized by category
- 200+ individual features documented
- Detailed comparison with MakeMKV, HandBrake, ARM, DVDDecrypter
- What makes AutoRip DVD special
- Future roadmap (v1.1, v1.2, community requests)

### ⚙️ Technical Improvements

#### Architecture
- Proper MVVM separation with change tracking
- Type-safe enum usage instead of string configs
- Observable properties with dependency notification
- Command pattern for all user actions
- Async/await throughout for non-blocking operations

#### Performance
- SQLite caching for metadata matches
- Parallel job processing support
- Efficient memory management
- Optimized disk I/O with buffering

#### Security
- Encrypted API key storage in SQLite
- Local processing only (no cloud uploads)
- Secure temp file cleanup
- Proper Windows ACLs for restricted folders

### 🔄 Breaking Changes

**Property Renames:**
- `settings.Quality` → `settings.VideoQuality`
- `settings.OutputBasePath` → `settings.OutputPath`
- `settings.AutoTranscode` → `settings.TranscodeAfterRip`

**Mitigation:** The ViewModel handles these differences automatically.  No user action required.

### 📋 Known Issues

1. **File/Folder Pickers Not Implemented**
   - Browse buttons for paths show TODO comments
   - Workaround: Manually type paths in text boxes
   - Fix planned for v1.1

2. **Hardware Encoder Auto-Detection**
   - Currently requires manual selection
   - Fix planned for v1.1

3. **Theme Radio Buttons**
   - Tag-based selection may not work correctly
   - Currently using ElementTheme enum binding
   - May need XAML adjustment

### 🚀 Upgrade Instructions

#### From Source (Fresh Clone)
```powershell
git pull origin main
dotnet restore
dotnet build -c Release
```

#### Settings Migration
- All settings are automatically migrated to SQLite
- Previous JSON settings files are no longer used
- New properties are initialized with sensible defaults
- No manual migration required

#### API Keys
If upgrading from pre-v1.0:
1. Re-enter your OMDb API key in Settings
2. Add TMDB API key (optional, recommended)
3. Add TVDB API key (optional, for TV shows)

### 📝 TODO for v1.1

**Critical:**
- [ ] Implement file/folder picker dialogs
- [ ] Add hardware encoder auto-detection
- [ ] Fix theme radio button selection
- [ ] Add ISO image creation
- [ ] Add CD audio ripping support

**Nice to Have:**
- [ ] Multi-language UI (i18n)
- [ ] Custom naming templates with variables
- [ ] OpenSubtitles integration
- [ ] NFO file creation for media servers
- [ ] Artwork download and embedding
- [ ] System tray minimize functionality

**Future:**
- [ ] Network share output support
- [ ] Web UI for remote access
- [ ] Mobile companion app
- [ ] Docker container support
- [ ] Linux/macOS support (via AvaloniaUI)

### 💬 User Feedback

We're actively collecting feedback! Please report:
- **Bugs** - GitHub Issues
- **Feature Requests** - GitHub Discussions
- **General Feedback** - GitHub Discussions or email

### 🙏 Credits

This release incorporates inspiration and patterns from:
- **MakeMKV** - Disc ripping and copy protection handling
- **HandBrake** - Professional video encoding
- **FileBot** - Intelligent metadata matching
- **Automatic Ripping Machine (ARM)** - Automatic workflow
- **Windows 11** - Modern settings UI design

Special thanks to:
- TheMovieDB for their excellent free API
- OMDb for providing IMDb data access
- TheTVDB community for TV metadata
- AniDB for anime information

---

## Previous Releases

### v0.9.0 (Development)
- Initial WinUI 3 application structure
- Basic MakeMKV integration
- Simple HandBrake transcoding
- OMDb metadata lookup
- Automatic disc detection
- Job queue system

### v0.8.0 (Development)
- Project initialization
- Core service layer
- MVVM architecture setup
- SQLite database integration
- Basic UI pages (Dashboard, Jobs, Logs)

---

**AutoRip DVD** - Making disc ripping simple, powerful, and intelligent.
