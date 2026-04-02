using AutoRipDVD.Models;
using System.Text.RegularExpressions;

namespace AutoRipDVD.Services;

/// <summary>
/// FileBot-style title parser that intelligently extracts metadata from filenames and disc labels
/// </summary>
public class TitleParser
{
    // Common patterns for movies
    private static readonly Regex MovieYearPattern = new(@"[\(\[\{]?(\d{4})[\)\]\}]?", RegexOptions.Compiled);
    private static readonly Regex ResolutionPattern = new(@"\b(720p|1080p|2160p|4K|BluRay|BDRip|DVDRip|WEB-DL|WEBRip)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // Common patterns for TV shows
    private static readonly Regex TvSeasonEpisodePattern1 = new(@"[Ss](\d{1,2})[Ee](\d{1,2})", RegexOptions.Compiled);
    private static readonly Regex TvSeasonEpisodePattern2 = new(@"(\d{1,2})x(\d{1,2})", RegexOptions.Compiled);
    private static readonly Regex TvSeasonPattern = new(@"Season\s*(\d{1,2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TvDiscPattern = new(@"Disc\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // Release group and quality markers to remove
    private static readonly string[] RemovePatterns = new[]
    {
        "BluRay", "BDRip", "DVDRip", "WEB-DL", "WEBRip", "HDTV", "720p", "1080p", "2160p", "4K",
        "x264", "x265", "h264", "h265", "AAC", "AC3", "DTS", "REMUX", "REMASTERED",
        "PROPER", "REPACK", "EXTENDED", "UNRATED", "DC", "THEATRICAL"
    };

    public ParsedTitle Parse(string input)
    {
        var result = new ParsedTitle { OriginalInput = input };

        // Clean up the input
        var cleaned = CleanInput(input);

        // Check for TV show patterns first
        var tvMatch = TvSeasonEpisodePattern1.Match(cleaned);
        if (!tvMatch.Success)
            tvMatch = TvSeasonEpisodePattern2.Match(cleaned);

        if (tvMatch.Success)
        {
            result.IsTVShow = true;
            result.Season = int.Parse(tvMatch.Groups[1].Value);
            result.Episode = int.Parse(tvMatch.Groups[2].Value);
            result.SeriesName = ExtractSeriesName(cleaned, tvMatch.Index);
        }
        else
        {
            // Check for season disc pattern (e.g., "Series S01 Disc 1")
            var seasonMatch = TvSeasonPattern.Match(cleaned);
            var discMatch = TvDiscPattern.Match(cleaned);
            
            if (seasonMatch.Success)
            {
                result.IsTVShow = true;
                result.Season = int.Parse(seasonMatch.Groups[1].Value);
                result.SeriesName = ExtractSeriesName(cleaned, seasonMatch.Index);
            }
            else
            {
                // Assume it's a movie
                result.IsTVShow = false;
                result.Title = ExtractMovieTitle(cleaned, out var year);
                result.Year = year;
            }
        }

        return result;
    }

    private string CleanInput(string input)
    {
        // Remove common separators and replace with spaces
        var cleaned = input.Replace('.', ' ')
                          .Replace('_', ' ')
                          .Replace('-', ' ');

        // Remove resolution and quality markers
        foreach (var pattern in RemovePatterns)
        {
            cleaned = Regex.Replace(cleaned, $@"\b{pattern}\b", "", RegexOptions.IgnoreCase);
        }

        // Remove anything in brackets/parentheses that looks like metadata
        cleaned = Regex.Replace(cleaned, @"\[.*?\]", "");
        cleaned = Regex.Replace(cleaned, @"\{.*?\}", "");

        // Clean up multiple spaces
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        return cleaned;
    }

    private string ExtractSeriesName(string input, int beforeIndex)
    {
        var title = input.Substring(0, beforeIndex).Trim();
        
        // Remove common separators at the end
        title = title.TrimEnd(' ', '-', '_', '.');
        
        return title;
    }

    private string ExtractMovieTitle(string input, out int? year)
    {
        year = null;

        // Look for year pattern
        var yearMatch = MovieYearPattern.Match(input);
        if (yearMatch.Success)
        {
            if (int.TryParse(yearMatch.Groups[1].Value, out var y) && y >= 1900 && y <= DateTime.Now.Year + 2)
            {
                year = y;
                
                // Extract title before year
                var title = input.Substring(0, yearMatch.Index).Trim();
                title = title.TrimEnd(' ', '-', '_', '.');
                
                return title;
            }
        }

        // No year found, return cleaned input
        return input;
    }

    public class ParsedTitle
    {
        public string OriginalInput { get; set; } = string.Empty;
        public bool IsTVShow { get; set; }
        
        // Movie fields
        public string Title { get; set; } = string.Empty;
        public int? Year { get; set; }
        
        // TV Show fields
        public string SeriesName { get; set; } = string.Empty;
        public int? Season { get; set; }
        public int? Episode { get; set; }
    }
}
