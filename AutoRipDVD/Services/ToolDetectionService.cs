using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AutoRipDVD.Services;

/// <summary>
/// Best-effort detection of common external tools (MakeMKV, HandBrake).
/// Provides path discovery and lightweight version probing.
/// </summary>
public class ToolDetectionService
{
    public ToolDetectionService() { }

    public string? DetectMakeMkvPath()
    {
        // Common install location
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MakeMKV", "makemkvcon64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MakeMKV", "makemkvcon64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MakeMKV", "makemkvcon.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public string? DetectHandBrakePath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "HandBrake", "HandBrakeCLI.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "HandBrake", "HandBrakeCLI.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public string? ProbeVersion(string exePath, string args = "--version")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return null;

            var psi = new ProcessStartInfo(exePath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return null;
            var outp = p.StandardOutput.ReadToEnd();
            var err  = p.StandardError.ReadToEnd();
            p.WaitForExit(3000);

            var text = string.IsNullOrWhiteSpace(outp) ? err : outp;
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Try to extract a short version token
            var m = Regex.Match(text, @"\d+\.\d+(?:\.\d+)?");
            return m.Success ? m.Value : text.Split(new[] {'\r','\n'} , StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
