using AutoRipDVD.Models;
using System.Text.RegularExpressions;

namespace AutoRipDVD.Services;

/// <summary>
/// FileBot-style naming service.
///
/// Movie format:   {n} ({y})                  → "The Matrix (1999)"
/// TV format:      {n}/Season {s}/{n} - {s00e00} - {t}
///                 → "Breaking Bad/Season 01/Breaking Bad - S01E01 - Pilot"
///
/// Plex folder conventions are used when CreatePlexFolderStructure is true:
///   Movies:    {OutputPath}/Movies/{n} ({y})/{n} ({y}).mkv
///   TV Shows:  {OutputPath}/TV Shows/{n}/Season {ss}/{n} - {s00e00} - {t}.mkv
/// </summary>
public interface IFileNamingService
{
    string GetMovieFolder(MediaMetadata meta, string outputRoot);
    string GetMovieFilePath(MediaMetadata meta, string outputRoot, string extension = ".mkv");
    string GetTVShowEpisodeFilePath(MediaMetadata meta, string outputRoot, int titleIndex, string extension = ".mkv");
    string GetTVShowFolder(MediaMetadata meta, string outputRoot);
    string FormatMovieName(MediaMetadata meta);
    string FormatEpisodeName(MediaMetadata meta, int? titleIndex = null);
    string SanitizePath(string name);
}

public class FileNamingService : IFileNamingService
{
    private readonly ISettingsService _settings;

    private static readonly char[] InvalidFileChars = Path.GetInvalidFileNameChars();
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

    // Replacements for common problematic characters that are valid on some filesystems
    private static readonly (string From, string To)[] FilenameReplacements =
    {
        (":", " -"),
        ("/", " "),
        ("\\", " "),
        ("*", "x"),
        ("?", ""),
        ("\"", "'"),
        ("<", "("),
        (">", ")"),
        ("|", "-"),
    };

    public FileNamingService(ISettingsService settings)
    {
        _settings = settings;
    }

    // ── Movies ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Plex-compatible folder for a movie.
    /// e.g. {OutputRoot}/Movies/The Matrix (1999)
    /// </summary>
    public string GetMovieFolder(MediaMetadata meta, string outputRoot)
    {
        var name = FormatMovieName(meta);
        return _settings.Settings.CreatePlexFolderStructure
            ? Path.Combine(outputRoot, "Movies", name)
            : Path.Combine(outputRoot, name);
    }

    /// <summary>
    /// Returns the full output file path for a movie.
    /// e.g. {OutputRoot}/Movies/The Matrix (1999)/The Matrix (1999).mkv
    /// </summary>
    public string GetMovieFilePath(MediaMetadata meta, string outputRoot, string extension = ".mkv")
    {
        var folder = GetMovieFolder(meta, outputRoot);
        var fileName = FormatMovieName(meta) + extension;
        return Path.Combine(folder, fileName);
    }

    // ── TV Shows ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Season folder for a TV show.
    /// e.g. {OutputRoot}/TV Shows/Breaking Bad/Season 01
    /// </summary>
    public string GetTVShowFolder(MediaMetadata meta, string outputRoot)
    {
        var showName = SanitizePath(meta.SeriesName.IfEmpty(meta.Title));
        var season = meta.SeasonNumber ?? 1;
        var seasonFolder = $"Season {season:D2}";

        return _settings.Settings.CreatePlexFolderStructure
            ? Path.Combine(outputRoot, "TV Shows", showName, seasonFolder)
            : Path.Combine(outputRoot, showName, seasonFolder);
    }

    /// <summary>
    /// Returns the full output file path for a TV episode.
    /// e.g. Breaking Bad - S01E01 - Pilot.mkv
    /// When episode info is missing, uses the title index to generate a name.
    /// </summary>
    public string GetTVShowEpisodeFilePath(MediaMetadata meta, string outputRoot, int titleIndex, string extension = ".mkv")
    {
        var folder = GetTVShowFolder(meta, outputRoot);
        var fileName = FormatEpisodeName(meta, titleIndex) + extension;
        return Path.Combine(folder, fileName);
    }

    // ── Formatters ────────────────────────────────────────────────────────────

    /// <summary>
    /// FileBot {n} ({y}) format for movies.
    /// "The Matrix (1999)" or "The Matrix" if year unknown.
    /// </summary>
    public string FormatMovieName(MediaMetadata meta)
    {
        var title = SanitizePath(meta.Title.IfEmpty("Unknown"));
        return meta.Year.HasValue ? $"{title} ({meta.Year})" : title;
    }

    /// <summary>
    /// FileBot {n} - {s00e00} - {t} format for TV episodes.
    /// "Breaking Bad - S01E01 - Pilot"
    /// When no episode metadata, falls back to "ShowName - S01E{titleIndex:D2}"
    /// </summary>
    public string FormatEpisodeName(MediaMetadata meta, int? titleIndex = null)
    {
        var show = SanitizePath(meta.SeriesName.IfEmpty(meta.Title).IfEmpty("Unknown"));
        var season = meta.SeasonNumber ?? 1;

        // Try to use real episode number, otherwise use disc title index
        var episodeNum = meta.EpisodeNumber ?? titleIndex ?? 1;
        var seasonEp = $"S{season:D2}E{episodeNum:D2}";

        var epTitle = meta.EpisodeTitle.IfEmpty(null);

        return epTitle != null
            ? $"{show} - {seasonEp} - {SanitizePath(epTitle)}"
            : $"{show} - {seasonEp}";
    }

    // ── Sanitisation ──────────────────────────────────────────────────────────

    public string SanitizePath(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Unknown";

        foreach (var (from, to) in FilenameReplacements)
            name = name.Replace(from, to);

        // Remove any remaining invalid chars
        name = string.Join("", name.Split(InvalidFileChars));

        // Collapse multiple spaces
        name = Regex.Replace(name, @"\s{2,}", " ");

        return name.Trim().TrimEnd('.');
    }
}

internal static class StringExtensions
{
    public static string IfEmpty(this string? s, string fallback)
        => string.IsNullOrWhiteSpace(s) ? fallback : s;

    public static string? IfEmpty(this string? s, string? fallback)
        => string.IsNullOrWhiteSpace(s) ? fallback : s;
}
