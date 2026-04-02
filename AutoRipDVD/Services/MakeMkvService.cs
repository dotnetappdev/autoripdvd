using AutoRipDVD.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AutoRipDVD.Services;

public interface IMakeMkvService
{
    Task<List<TitleInfo>> ScanDiscAsync(string driveLetter, IProgress<string>? progress = null);
    Task<bool> RipTitlesAsync(string driveLetter, List<int> titleIndices, string outputPath, IProgress<double>? progress = null);
}

public class MakeMkvService : IMakeMkvService
{
    private readonly ISettingsService _settings;
    private readonly ILogService _logService;

    public MakeMkvService(ISettingsService settings, ILogService logService)
    {
        _settings = settings;
        _logService = logService;
    }

    public async Task<List<TitleInfo>> ScanDiscAsync(string driveLetter, IProgress<string>? progress = null)
    {
        var titles = new List<TitleInfo>();
        
        progress?.Report("Scanning disc with MakeMKV...");
        await _logService.LogAsync($"Scanning disc in {driveLetter}");

        try
        {
            var driveIndex = GetDriveIndex(driveLetter);
            var arguments = $"-r --cache=1 info disc:{driveIndex}";
            
            var output = await RunMakeMkvAsync(arguments);
            titles = ParseTitles(output);
            
            await _logService.LogAsync($"Found {titles.Count} titles on disc");
        }
        catch (Exception ex)
        {
            await _logService.LogAsync($"Error scanning disc: {ex.Message}");
            throw;
        }

        return titles;
    }

    public async Task<bool> RipTitlesAsync(string driveLetter, List<int> titleIndices, string outputPath, IProgress<double>? progress = null)
    {
        try
        {
            Directory.CreateDirectory(outputPath);
            var driveIndex = GetDriveIndex(driveLetter);

            foreach (var titleIndex in titleIndices)
            {
                await _logService.LogAsync($"Ripping title {titleIndex} from {driveLetter}");
                
                var arguments = $"-r --minlength={_settings.Settings.MinimumTitleLengthSeconds} mkv disc:{driveIndex} {titleIndex} \"{outputPath}\"";
                
                await RunMakeMkvWithProgressAsync(arguments, progress);
            }

            return true;
        }
        catch (Exception ex)
        {
            await _logService.LogAsync($"Error ripping titles: {ex.Message}");
            return false;
        }
    }

    private async Task<string> RunMakeMkvAsync(string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _settings.Settings.MakeMkvPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output;
    }

    private async Task RunMakeMkvWithProgressAsync(string arguments, IProgress<double>? progress = null)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _settings.Settings.MakeMkvPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logService.LogAsync(e.Data);
                
                // Parse progress: PRGV:current,total,max
                var match = Regex.Match(e.Data, @"PRGV:(\d+),(\d+),(\d+)");
                if (match.Success)
                {
                    var current = int.Parse(match.Groups[1].Value);
                    var total = int.Parse(match.Groups[2].Value);
                    var max = int.Parse(match.Groups[3].Value);
                    
                    if (max > 0)
                    {
                        var percentage = (double)current / max * 100.0;
                        progress?.Report(percentage);
                    }
                }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();
    }

    private List<TitleInfo> ParseTitles(string output)
    {
        var titles = new List<TitleInfo>();
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            // Parse TINFO lines: TINFO:index,code,value
            if (line.StartsWith("TINFO:"))
            {
                var parts = line.Substring(6).Split(',', 3);
                if (parts.Length >= 3)
                {
                    var titleIndex = int.Parse(parts[0]);
                    var code = int.Parse(parts[1]);
                    var value = parts[2].Trim('"');

                    // Ensure title exists in list
                    while (titles.Count <= titleIndex)
                    {
                        titles.Add(new TitleInfo { Index = titles.Count });
                    }

                    var title = titles[titleIndex];

                    // Parse different codes
                    switch (code)
                    {
                        case 2: // Title name
                            title.Name = value;
                            break;
                        case 9: // Duration in seconds
                            if (int.TryParse(value, out var seconds))
                                title.Duration = TimeSpan.FromSeconds(seconds);
                            break;
                        case 10: // Size in bytes
                            if (long.TryParse(value, out var size))
                                title.SizeBytes = size;
                            break;
                        case 8: // Chapter count
                            if (int.TryParse(value, out var chapters))
                                title.ChapterCount = chapters;
                            break;
                    }
                }
            }
        }

        // Determine main feature (longest title)
        if (titles.Any())
        {
            var longestTitle = titles.OrderByDescending(t => t.Duration).First();
            longestTitle.IsMainFeature = true;
        }

        return titles.Where(t => t.Duration.TotalSeconds > 0).ToList();
    }

    private int GetDriveIndex(string driveLetter)
    {
        // Convert drive letter to drive index (A: = 0, B: = 1, etc.)
        var letter = driveLetter.TrimEnd(':').ToUpper()[0];
        return letter - 'A';
    }
}
