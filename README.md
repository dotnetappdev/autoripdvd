# AutoRip DVD - Complete DVD/Blu-ray Ripping Solution v1.0.0

**Version 1.0.0** | [Changelog](VERSION.md) | [Quick Start Guide](QUICKSTART.md) | [Deployment Guide](DEPLOYMENT.md)

A comprehensive, professional Windows application for automatic and manual DVD/Blu-ray disc ripping with intelligent metadata matching, advanced quality controls, and batch processing capabilities. Combines the best features of MakeMKV, HandBrake, and FileBot into one modern WinUI 3 application.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![WinUI 3](https://img.shields.io/badge/WinUI-3-green.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4.svg)

---

## 🌟 Key Features

### 🎬 Professional Disc Ripping
- **✓ Automatic Disc Detection** - Monitors optical drives using WMI
- **✓ Copy Protection Handling** - Supports CSS, AACS, BD+ via MakeMKV
- **✓ Manual Title Selection** - MakeMKV-style GUI for choosing specific titles
- **✓ Smart Title Filtering** - Automatically identifies main features vs extras
- **✓ Multi-Disc Support** - Rip from multiple drives simultaneously
- **✓ Start/Stop Controls** - Full manual override of automatic ripping
- **✓ Detailed Disc Info** - View drive and disc properties

### 🔍 Intelligent Metadata Matching (FileBot-Style)
- **✓ Multi-Source Search** - TMDB, OMDb, TheTVDB, AniDB integration
- **✓ Automatic Title Parsing** - Regex-based extraction of titles, years, episodes
- **✓ Fuzzy Matching** - Finds best match even with imperfect disc labels
- **✓ Movie Detection** - Year extraction and TMDB lookup
- **✓ TV Show Detection** - S01E01, 1x01, Season/Episode pattern recognition
- **✓ Match History** - SQLite cache of successful matches
- **✓ Manual Search** - Override with custom metadata queries

### ⚙️ Advanced HandBrake Features
- **✓ Quality Presets** - Fast 1080p, High Quality, 4K UHD, custom
- **✓ Video Encoders** - x264, x265/HEVC, VP9, AV1 support
- **✓ RF Quality Control** - Constant quality (RF 18-28) with tooltips
- **✓ Audio Encoders** - AAC, AC3, MP3, Opus, FLAC, pass-through
- **✓ Audio Bitrate** - Configurable from 96-320 kbps
- **✓ Hardware Encoding** - NVENC, QuickSync, VCE support
- **✓ Process Priority** - Low/Normal/High to control system impact
- **✓ Prevent Sleep** - Keeps system awake during encoding
- **✓ Low Disk Space Pause** - Auto-pause when < 10GB free
- **✓ Minimum Title Duration** - Filter titles shorter than threshold
- **✓ Logging Levels** - Standard/Verbose/Debug output

### 🎛️ MakeMKV Quality Controls
- **✓ Quality Presets** - Original/High/Medium/Low compression
- **✓ Audio Preservation** - Keep DTS, TrueHD, Dolby Atmos
- **✓ Multi-Track Audio** - Include all audio tracks or primary only
- **✓ Subtitle Inclusion** - All subtitles, forced only, or none
- **✓ Chapter Markers** - Preserve DVD/Blu-ray chapters
- **✓ Video Streams** - Multiple angles and PiP support

### 📊 Professional Quality
- **✓ Batch Processing** - Async job queue with parallel execution
- **✓ Progress Tracking** - Real-time progress bars with ETA
- **✓ Job History** - SQLite database of all ripping operations
- **✓ Error Recovery** - Automatic retry with exponential backoff
- **✓ Webhook Notifications** - Slack, Discord, Pushbullet, IFTTT
- **✓ Comprehensive Logging** - Rotating logs with verbosity control

### 🎨 Modern Windows 11 UI
- **✓ WinUI 3 Fluent Design** - Native Windows 11 styling
- **✓ Settings Organization** - Windows 11-style categorized pages
- **✓ Light/Dark Themes** - System-integrated or manual selection
- **✓ Responsive Layout** - Adapts to window size
- **✓ Keyboard Navigation** - Full accessibility support
- **✓ Live Dashboard** - Real-time job monitoring

---

## 📋 System Requirements

### Software Dependencies
- **Windows 10/11** (version 1809 or later)
- **.NET 8 Runtime** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **MakeMKV** - [Download](https://www.makemkv.com/)
- **HandBrake CLI** (optional) - [Download](https://handbrake.fr/)

### Hardware Requirements
- Optical drive (DVD or Blu-ray)
- Sufficient disk space for ripped content

## Installation

### Option 1: Build from Source

1. **Clone the repository**
   ```powershell
   git clone https://github.com/yourusername/autoripdvd.git
   cd autoripdvd
   ```

2. **Install MakeMKV**
   - Download and install from [makemkv.com](https://www.makemkv.com/)
   - Note the installation path (typically `C:\Program Files (x86)\MakeMKV\makemkvcon64.exe`)

3. **Install HandBrake CLI** (optional)
   - Download from [handbrake.fr](https://handbrake.fr/)
   - Note the installation path (typically `C:\Program Files\HandBrake\HandBrakeCLI.exe`)

4. **Build the application**
   ```powershell
   dotnet build AutoRipDVD.sln -c Release
   ```

5. **Run the application**
   ```powershell
   cd AutoRipDVD\bin\Release\net8.0-windows10.0.19041.0\win-x64
   .\AutoRipDVD.exe
   ```

### Option 2: Download Pre-built Binary
*Coming soon - check Releases page*

## Configuration

### Initial Setup

1. **Launch AutoRip DVD**
2. **Navigate to Settings** (gear icon)
3. **Configure paths:**
   - Set MakeMKV executable path
   - Set HandBrake CLI path (if using)
   - Set output folder for ripped media
4. **Add API keys:**
   - Get a free OMDb API key from [omdbapi.com](https://www.omdbapi.com/apikey.aspx)
   - (Optional) Add TVDB API key for enhanced TV show metadata
5. **Configure ripping options:**
   - Enable/disable auto-rip
   - Choose main feature only vs all titles
   - Set minimum title length
   - Configure auto-eject and transcoding

### API Keys

#### OMDb API Key (Required for metadata)
1. Visit [omdbapi.com/apikey.aspx](https://www.omdbapi.com/apikey.aspx)
2. Choose the free tier (1,000 requests/day)
3. Verify your email
4. Copy the API key into AutoRip DVD settings

#### TVDB API Key (Optional, for TV shows)
1. Create account at [thetvdb.com](https://thetvdb.com/)
2. Generate API key from your account settings
3. Add to AutoRip DVD settings

## Usage

### Automatic Mode
1. **Enable Auto Rip** in Settings
2. **Insert a disc** - The app automatically detects and starts ripping
3. **Monitor progress** on the Dashboard
4. **Disc ejects** when complete (if enabled)
5. **Find your ripped media** in the output folder

### Manual Mode
1. **Disable Auto Rip** in Settings
2. **Insert a disc** - The app detects but waits
3. **Preview titles** in the job details
4. **Select specific titles** to rip (episodes, features, etc.)
5. **Start the rip** manually
6. **Monitor progress** on the Dashboard

### Output Structure

**Movies:**
```
OutputFolder/
  └── Movie Title (2024)/
      ├── Movie Title (2024).mkv  (or .mp4 if transcoded)
      └── ...
```

**TV Shows:**
```
OutputFolder/
  └── Series Name/
      ├── S01E01 - Episode Title.mkv
      ├── S01E02 - Episode Title.mkv
      └── ...
```

## How It Works

### Disc Detection
- Uses Windows Management Instrumentation (WMI) to monitor optical drives
- Detects DVD (VIDEO_TS folder) and Blu-ray (BDMV folder) automatically
- Triggers ripping workflow on disc insertion

### Title Filtering (Like Automatic Ripping Machine)
- **Scans all titles** on the disc using MakeMKV
- **Analyzes duration, chapters, and file size** to identify main content
- **Filters out extras** - Removes trailers, menus, bonus features
- **For TV shows** - Identifies episode-length titles (15-90 minutes)
- **For movies** - Selects the longest title as main feature
- **Manual override** - Preview and select specific titles if needed

### Metadata Matching (Like FileBot)
- **Parses disc label** using intelligent regex patterns
- **Extracts movie title and year** - e.g., "Inception (2010)"
- **Detects TV episodes** - Recognizes S01E01, 1x01, Season 1 patterns
- **Searches OMDb/TVDB** for accurate metadata
- **Fuzzy matching** - Finds best match even with imperfect labels
- **Auto-naming** - Creates Plex/Emby-compatible folder structure

### Ripping Pipeline
1. **Scan** - MakeMKV scans disc and lists all titles
2. **Identify** - Parse disc label and fetch metadata
3. **Filter** - Remove extras, keep main content
4. **Rip** - MakeMKV extracts selected titles to MKV
5. **Transcode** (optional) - HandBrake converts to MP4
6. **Organize** - Move to output folder with proper naming
7. **Eject** - Eject disc when complete

## Notifications

### Webhook Configuration
AutoRip DVD supports generic webhook notifications compatible with most services:

**Slack:**
1. Create Incoming Webhook in Slack
2. Paste webhook URL in Settings

**Discord:**
1. Create webhook in Discord server settings
2. Paste webhook URL in Settings

**IFTTT/Pushbullet/etc:**
- Configure webhook that accepts JSON POST with `title` and `message` fields

## Troubleshooting

### Disc Not Detected
- Ensure disc is fully inserted and drive is ready
- Check Windows Device Manager for drive issues
- Try manually opening/closing the drive tray

### MakeMKV Errors
- Ensure MakeMKV is installed and path is correct in Settings
- Verify MakeMKV can open the disc manually
- Check MakeMKV beta key if using trial version
- Some discs may require updated MakeMKV version

### Metadata Not Found
- Verify OMDb API key is valid and has remaining quota
- Try manually searching for the title in the job details
- Check disc label matches the actual content
- Edit metadata manually if auto-detection fails

### HandBrake Transcoding Fails
- Ensure HandBrake CLI is installed and path is correct
- Check available disk space
- Verify preset name matches available presets
- Review logs for specific HandBrake errors

## Command Line Tools

The app uses these command-line tools:

### MakeMKV CLI
```powershell
# Scan disc
makemkvcon64.exe -r info disc:0

# Rip title
makemkvcon64.exe -r mkv disc:0 0 "C:\Output"
```

### HandBrakeCLI
```powershell
# Transcode
HandBrakeCLI.exe --preset "Fast 1080p30" -i "input.mkv" -o "output.mp4"
```

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## Roadmap

- [ ] Multi-language subtitle extraction
- [ ] Audio track selection
- [ ] Custom naming templates
- [ ] CD audio ripping (abcde integration)
- [ ] ISO creation for data discs
- [ ] Network drive support
- [ ] IMDB/TMDB direct API integration
- [ ] Chapter marker preservation
- [ ] Batch disc handling workflow

## License

This project is licensed under the MIT License - see LICENSE file for details.

## Acknowledgments

- **MakeMKV** - Disc ripping engine
- **HandBrake** - Video transcoding
- **OMDb API** - Movie/TV metadata
- **FileBot** - Inspiration for intelligent title parsing
- **Automatic Ripping Machine** - Inspiration for filtering logic

## Support

For issues, questions, or feature requests:
- Open an issue on GitHub
- Check the Wiki for detailed guides
- Review logs in the app for troubleshooting

---

**Disclaimer:** This software is for backing up media you own. Respect copyright laws in your jurisdiction.
