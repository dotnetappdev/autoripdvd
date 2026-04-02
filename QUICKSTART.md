# Quick Start Guide

## First Time Setup (5 minutes)

### 1. Install Prerequisites

**MakeMKV (Required)**
```powershell
# Download and install from:
https://www.makemkv.com/download/

# Default install path:
C:\Program Files (x86)\MakeMKV\makemkvcon64.exe
```

**HandBrake CLI (Optional, for transcoding)**
```powershell
# Download from:
https://handbrake.fr/downloads.php

# Choose "CLI" version
# Default install path:
C:\Program Files\HandBrake\HandBrakeCLI.exe
```

### 2. Get OMDb API Key (Free)

1. Go to: https://www.omdbapi.com/apikey.aspx
2. Select "FREE" plan (1,000 requests/day)
3. Enter your email
4. Check email and click activation link
5. Copy your API key

### 3. Build and Run

```powershell
# Navigate to project folder
cd d:\sasproducts\autoripdvd

# Restore packages and build
dotnet restore
dotnet build AutoRipDVD.sln -c Release

# Run the application
cd AutoRipDVD\bin\Release\net8.0-windows10.0.19041.0\win-x64
.\AutoRipDVD.exe
```

### 4. Configure Settings

When the app launches:

1. Click the **Settings** (gear icon) in navigation
2. **Paths Section:**
   - MakeMKV: `C:\Program Files (x86)\MakeMKV\makemkvcon64.exe`
   - HandBrake: `C:\Program Files\HandBrake\HandBrakeCLI.exe`
   - Output: `D:\Ripped` (or your preferred folder)

3. **API Keys:**
   - Paste your OMDb API key

4. **Ripping Options:**
   - ✅ Auto Rip
   - ✅ Rip Main Feature Only (uncheck for TV shows to get all episodes)
   - ✅ Eject When Complete
   - ✅ Auto Transcode (check if you want MP4 instead of MKV)

5. Click **Save Settings**

## First Rip

### Movie Disc:
1. Insert a DVD or Blu-ray movie
2. Watch the Dashboard - it auto-detects the disc
3. See metadata fetching in real-time
4. Watch progress bar as it rips
5. Disc ejects when complete
6. Find your movie in: `Output\Movie Title (Year)\Movie Title (Year).mkv`

### TV Show Disc:
1. Insert a TV show DVD/Blu-ray
2. App automatically:
   - Detects it's a TV show
   - Filters out extras/menus
   - Identifies episodes only
3. Watch progress as each episode rips
4. Find episodes in: `Output\Series Name\S01E01 - Episode Title.mkv`

## Monitoring

### Dashboard
- View active ripping jobs
- See real-time progress
- Check recent logs

### Jobs Page
- View all jobs (current and past)
- Check completion status
- Find output paths

### Logs Page
- Filter logs with search box
- See detailed MakeMKV/HandBrake output
- Troubleshoot any issues

## Tips

### For TV Shows:
- **Uncheck "Rip Main Feature Only"** to get all episodes
- The app automatically filters out trailers and extras
- Episodes are named in Plex/Emby format

### For Movies:
- **Keep "Rip Main Feature Only" checked** to skip extras
- Uncheck it if you want bonus features too

### For Quality:
- **MKV output** (no transcode) - Perfect quality, large files
- **MP4 output** (with transcode) - Smaller files, slight quality loss
- Adjust HandBrake quality slider (18-28, lower = better quality)

### For Multiple Drives:
- The app can handle multiple optical drives
- Insert discs in all drives
- Jobs process in parallel

## Troubleshooting

**Disc not detected?**
- Wait 30 seconds after inserting
- Open Windows Explorer and check if drive shows the disc
- Try ejecting and reinserting

**"API key invalid"?**
- Check you activated the key via email
- Verify you pasted it correctly (no spaces)
- Check your daily quota isn't exceeded (1,000/day on free plan)

**MakeMKV not found?**
- Verify installation path in Settings matches actual install location
- Try running MakeMKV GUI to ensure it works
- Check MakeMKV license/beta key is valid

**HandBrake fails?**
- Ensure you downloaded CLI version, not GUI
- Check disk space (need ~2-3x the source size temporarily)
- Try a different preset in Settings

## Advanced

### Custom Naming:
Edit output in code: `MediaMetadata.GetFormattedFolderName()`

### Custom Filtering:
Adjust title filtering in `TitleFilterService.cs`

### Notifications:
Set webhook URL in Settings for Slack/Discord alerts

### API Limits:
Free OMDb tier = 1,000 requests/day
Upgrade at omdbapi.com if needed

## File Structure

```
d:\sasproducts\autoripdvd\
├── AutoRipDVD.sln              # Solution file
├── AutoRipDVD\
│   ├── Models\
│   │   └── Models.cs            # Data models
│   ├── Services\
│   │   ├── DiscDetectionService.cs    # Disc monitoring
│   │   ├── MakeMkvService.cs          # MakeMKV integration
│   │   ├── HandBrakeService.cs        # HandBrake integration
│   │   ├── MetadataService.cs         # OMDb/TVDB API
│   │   ├── TitleParser.cs             # FileBot-style parsing
│   │   ├── TitleFilterService.cs      # Smart title filtering
│   │   ├── RipJobQueue.cs             # Job management
│   │   ├── SettingsService.cs         # Settings persistence
│   │   ├── LogService.cs              # Logging
│   │   └── NotificationService.cs     # Webhooks
│   ├── ViewModels\
│   │   ├── MainViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   ├── JobsViewModel.cs
│   │   └── LogsViewModel.cs
│   ├── Views\
│   │   ├── MainWindow.xaml            # Main window
│   │   ├── DashboardPage.xaml         # Dashboard
│   │   ├── SettingsPage.xaml          # Settings
│   │   ├── JobsPage.xaml              # Jobs history
│   │   └── LogsPage.xaml              # Logs viewer
│   ├── App.xaml                       # Application
│   └── AutoRipDVD.csproj              # Project file
└── README.md                           # Full documentation
```

## Next Steps

- [ ] Rip your first disc!
- [ ] Configure notifications (optional)
- [ ] Set up multiple drives (optional)
- [ ] Customize HandBrake presets (optional)
- [ ] Star the GitHub repo ⭐

## Help

Having issues? Check:
1. **Logs page** in the app for errors
2. **README.md** for detailed troubleshooting
3. **GitHub Issues** to report bugs or ask questions

Enjoy automated disc ripping! 🎬
