using AutoRipDVD.Models;

namespace AutoRipDVD.Services;

/// <summary>
/// Intelligent title filtering service - identifies main content vs extras/bonus features
/// Based on Automatic Ripping Machine's title filtering logic
/// </summary>
public interface ITitleFilterService
{
    List<TitleInfo> FilterTitles(List<TitleInfo> titles, MediaType mediaType, bool includeExtras = false);
    bool IsLikelyExtra(TitleInfo title, int? mainFeatureDuration = null);
    TitleInfo? GetMainFeature(List<TitleInfo> titles);
}

public class TitleFilterService : ITitleFilterService
{
    // Minimum duration for a standard movie (in minutes)
    private const int MinMovieDuration = 40;
    
    // Minimum duration for a TV episode (in minutes)
    private const int MinEpisodeDuration = 15;
    private const int MaxEpisodeDuration = 90;
    
    // Common patterns in extra/bonus titles
    private static readonly string[] ExtraPatterns = new[]
    {
        "trailer", "preview", "teaser", "promo",
        "behind the scenes", "making of", "featurette",
        "deleted scene", "bonus", "extra", "interview",
        "commentary", "gag reel", "blooper", "outtake",
        "credits", "menu", "advertisement", "commercial",
        "coming soon", "also available"
    };

    public List<TitleInfo> FilterTitles(List<TitleInfo> titles, MediaType mediaType, bool includeExtras = false)
    {
        if (!titles.Any())
            return titles;

        // First, mark all titles as main or extra
        var mainFeature = GetMainFeature(titles);
        var mainDuration = mainFeature?.Duration.TotalMinutes;

        foreach (var title in titles)
        {
            title.IsMainFeature = title == mainFeature;
        }

        if (mediaType == MediaType.Movie)
        {
            return FilterMovieTitles(titles, includeExtras);
        }
        else if (mediaType == MediaType.TVShow)
        {
            return FilterTVTitles(titles, includeExtras);
        }

        return titles;
    }

    private List<TitleInfo> FilterMovieTitles(List<TitleInfo> titles, bool includeExtras)
    {
        if (includeExtras)
            return titles;

        // For movies, typically we want the longest title (main feature)
        var mainFeature = GetMainFeature(titles);
        
        if (mainFeature != null)
        {
            // Return main feature only
            return new List<TitleInfo> { mainFeature };
        }

        // Fallback: return titles longer than minimum movie duration
        return titles.Where(t => t.Duration.TotalMinutes >= MinMovieDuration).ToList();
    }

    private List<TitleInfo> FilterTVTitles(List<TitleInfo> titles, bool includeExtras)
    {
        var filtered = new List<TitleInfo>();

        // For TV shows, we want all episode-length titles, excluding extras
        foreach (var title in titles)
        {
            var durationMinutes = title.Duration.TotalMinutes;
            
            // Check if it's episode-length
            if (durationMinutes >= MinEpisodeDuration && durationMinutes <= MaxEpisodeDuration)
            {
                // Check if it's not an extra
                if (!IsLikelyExtra(title) || includeExtras)
                {
                    filtered.Add(title);
                }
            }
        }

        // If we filtered out everything, be more lenient
        if (!filtered.Any())
        {
            filtered = titles.Where(t => 
                t.Duration.TotalMinutes >= MinEpisodeDuration && 
                t.Duration.TotalMinutes <= MaxEpisodeDuration
            ).ToList();
        }

        return filtered;
    }

    public bool IsLikelyExtra(TitleInfo title, int? mainFeatureDuration = null)
    {
        var name = title.Name.ToLower();
        
        // Check against known extra patterns
        if (ExtraPatterns.Any(pattern => name.Contains(pattern)))
            return true;

        // Very short titles are likely extras or menus
        if (title.Duration.TotalMinutes < 3)
            return true;

        // Check duration relative to main feature (for movies)
        if (mainFeatureDuration.HasValue)
        {
            // Titles significantly shorter than main feature are likely extras
            var percentOfMain = (title.Duration.TotalMinutes / mainFeatureDuration.Value) * 100;
            if (percentOfMain < 40) // Less than 40% of main feature
                return true;
        }

        return false;
    }

    public TitleInfo? GetMainFeature(List<TitleInfo> titles)
    {
        if (!titles.Any())
            return null;

        // Main feature is typically:
        // 1. The longest title
        // 2. Has the most chapters
        // 3. Is not obviously an extra

        var candidates = titles
            .Where(t => !IsLikelyExtra(t))
            .Where(t => t.Duration.TotalMinutes >= MinMovieDuration)
            .ToList();

        if (!candidates.Any())
            candidates = titles.Where(t => t.Duration.TotalMinutes >= MinMovieDuration).ToList();

        if (!candidates.Any())
            return titles.OrderByDescending(t => t.Duration).FirstOrDefault();

        // Score each candidate
        var scored = candidates.Select(t => new
        {
            Title = t,
            Score = (t.Duration.TotalMinutes * 2) + (t.ChapterCount * 5) + (t.SizeBytes / 1_000_000_000.0 * 10)
        }).OrderByDescending(x => x.Score);

        return scored.First().Title;
    }
}
