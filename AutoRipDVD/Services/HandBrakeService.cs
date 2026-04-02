using AutoRipDVD.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AutoRipDVD.Services;

public interface IHandBrakeService
{
    Task<bool> TranscodeAsync(string inputPath, string outputPath, IProgress<double>? progress = null);
    Task<bool> TranscodeAsync(string inputPath, string outputPath, string preset, int quality, IProgress<double>? progress = null);
    Task<List<string>> GetPresetsAsync();
}

public class HandBrakeService : IHandBrakeService
{
    private readonly ISettingsService _settings;
    private readonly ILogService _logService;

    public HandBrakeService(ISettingsService settings, ILogService logService)
    {
        _settings = settings;
        _logService = logService;
    }

    public async Task<bool> TranscodeAsync(string inputPath, string outputPath, IProgress<double>? progress = null)
    {
        return await TranscodeAsync(inputPath, outputPath, _settings.Settings.HandBrakePreset, _settings.Settings.Quality, progress);
    }

    public async Task<bool> TranscodeAsync(string inputPath, string outputPath, string preset, int quality, IProgress<double>? progress = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            
            await _logService.LogAsync($"Transcoding {Path.GetFileName(inputPath)} with HandBrake");

            // Build HandBrake arguments
            var arguments = $"--preset \"{preset}\" " +
                          $"--encoder x264 " +
                          $"--quality {quality} " +
                          $"--input \"{inputPath}\" " +
                          $"--output \"{outputPath}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _settings.Settings.HandBrakePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logService.LogAsync(e.Data);
                    
                    // Parse progress: "Encoding: task 1 of 1, 45.67 %"
                    var match = Regex.Match(e.Data, @"(\d+\.\d+)\s*%");
                    if (match.Success && double.TryParse(match.Groups[1].Value, out var percentage))
                    {
                        progress?.Report(percentage);
                    }
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            var success = process.ExitCode == 0;
            await _logService.LogAsync(success 
                ? $"Successfully transcoded {Path.GetFileName(inputPath)}"
                : $"Failed to transcode {Path.GetFileName(inputPath)}");

            return success;
        }
        catch (Exception ex)
        {
            await _logService.LogAsync($"Error transcoding: {ex.Message}");
            return false;
        }
    }

    public async Task<List<string>> GetPresetsAsync()
    {
        var presets = new List<string>();

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _settings.Settings.HandBrakePath,
                    Arguments = "--preset-list",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Parse preset names from output
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("<") && !line.Contains("Preset:"))
                {
                    var preset = line.Trim().TrimStart('<').Trim();
                    if (!string.IsNullOrEmpty(preset))
                        presets.Add(preset);
                }
            }
        }
        catch (Exception ex)
        {
            await _logService.LogAsync($"Error getting HandBrake presets: {ex.Message}");
        }

        return presets;
    }
}
