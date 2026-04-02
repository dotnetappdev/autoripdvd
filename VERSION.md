# AutoRip DVD - Version History

## Version 1.0.0 (April 2, 2026) - Initial Release

### Features
- ✅ Automatic DVD and Blu-ray disc detection
- ✅ MakeMKV integration for protected disc ripping
- ✅ HandBrake CLI integration for transcoding
- ✅ FileBot-style intelligent title parsing
- ✅ OMDb API integration for movie/TV metadata
- ✅ Smart title filtering (removes extras, identifies episodes)
- ✅ Automatic episode detection for TV shows
- ✅ Main feature detection for movies
- ✅ Windows Management Instrumentation disc monitoring
- ✅ Asynchronous job queue processing
- ✅ Real-time progress tracking
- ✅ WinUI 3 modern interface
- ✅ Light and Dark mode support
- ✅ Webhook notifications (Slack, Discord, etc.)
- ✅ Comprehensive logging system
- ✅ Job history tracking
- ✅ Configurable ripping options
- ✅ Auto-eject on completion
- ✅ Batch processing support
- ✅ Plex/Emby-compatible naming

### Technical Details
- **Framework**: .NET 8.0, WinUI 3
- **Platform**: Windows 10/11 (x64)
- **Architecture**: MVVM with dependency injection
- **External Tools**: MakeMKV CLI, HandBrake CLI
- **APIs**: OMDb, TVDB support

### Known Limitations
- Single optical drive polling interval: 5 seconds
- Free OMDb API: 1,000 requests/day limit
- MakeMKV beta key required after trial period
- Windows-only (no cross-platform support)

### Roadmap for Future Versions
- v1.1.0: Title selection dialog with preview
- v1.2.0: Multiple audio track selection
- v1.3.0: Subtitle extraction and selection
- v2.0.0: CD audio ripping support
- v2.1.0: ISO creation for data discs
- v2.2.0: Custom naming templates
- v3.0.0: Network drive support
- v3.1.0: Direct TMDB API integration

---

## Versioning Scheme

This project follows [Semantic Versioning](https://semver.org/):

**MAJOR.MINOR.PATCH**

- **MAJOR**: Incompatible API changes or major feature overhauls
- **MINOR**: New backwards-compatible functionality
- **PATCH**: Backwards-compatible bug fixes

---

## How to Check Your Version

1. Build the application
2. Right-click `AutoRipDVD.exe` → Properties → Details tab
3. Check "Product version"

Or in PowerShell:
```powershell
(Get-Item ".\AutoRipDVD.exe").VersionInfo.FileVersion
```
