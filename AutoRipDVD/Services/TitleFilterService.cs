using AutoRipDVD.Models;

namespace AutoRipDVD.Services;

/// <summary>
/// Intelligent title filtering - identifies main content vs extras/bonus features.
/// Based on Automatic Ripping Machine's title filtering logic.
/// </summary>
public interface ITitleFilterService
{
    List<TitleInfo> FilterTitles(List<TitleInfo> titles, MediaType mediaType, bool includeExtras = false);
    Task<List<TitleInfo>> FilterTitlesAsync(List<TitleInfo> titles, MediaType mediaType, bool includeExtras = false);
    bool IsLikelyExtra(TitleInfo title, int? mainFeatureDurationMinutes = null);
    TitleInfo? GetMainFeature(List<TitleInfo> titles);
}

public class TitleFilterService : ITitleFilterService
{
    private const int MinMovieDurationMinutes  = 40;
    private const int MinEpisodeDurationMinutes = 15;
    private const int MaxEpisodeDurationMinutes = 90;

    private static readonly string[] ExtraKeywords =
    {
        "trailer", "preview", "teaser", "promo",
        "behind the scenes", "making of", "featurette",
        "deleted scene", "bonus", "extra", "interview",
        "commentary", "gag reel", "blooper", "outtake",
        "credits", "menu", "advertisement", "commercial",
        "coming soon", "also available", "short film",
        "sing-along", "karaoke"
    };

    public List<TitleInfo> FilterTitles(List<TitleInfo> titles, MediaType mediaType, bool includeExtras = false)
    {
        if (!titles.Any()) return titles;

        var main = GetMainFeature(titles);
        foreach (var t in titles)
            t.IsMainFeature = t == main;

        return mediaType == MediaType.TVShow
            ? FilterTVTitles(titles, includeExtras)
            : FilterMovieTitles(titles, includeExtras);
    }

    public Task<List<TitleInfo>> FilterTitlesAsync(List<TitleInfo> titles, MediaType mediaType, bool includeExtras = false)
        => Task.FromResult(FilterTitles(titles, mediaType, includeExtras));

    private List<TitleInfo> FilterMovieTitles(List<TitleInfo> titles, bool includeExtras)
    {
        if (includeExtras) return titles;

        var main = GetMainFeature(titles);
        if (main != null) return new List<TitleInfo> { main };

        return titles.Where(t => t.Duration.TotalMinutes >= MinMovieDurationMinutes).ToList();
    }

    private List<TitleInfo> FilterTVTitles(List<TitleInfo> titles, bool includeExtras)
    {
        var episodes = titles
            .Where(t => t.Duration.TotalMinutes >= MinEpisodeDurationMinutes &&
                        t.Duration.TotalMinutes <= MaxEpisodeDurationMinutes &&
                        (includeExtras || !IsLikelyExtra(t)))
            .ToList();

        // Fallback: be more lenient if everything was filtered
        if (!episodes.Any())
            episodes = titles
                .Where(t => t.Duration.TotalMinutes >= MinEpisodeDurationMinutes &&
                            t.Duration.TotalMinutes <= MaxEpisodeDurationMinutes)
                .ToList();

        return episodes;
    }

    public bool IsLikelyExtra(TitleInfo title, int? mainFeatureDurationMinutes = null)
    {
        var nameLower = title.Name.ToLowerInvariant();
        if (ExtraKeywords.Any(k => nameLower.Contains(k))) return true;

        // Very short titles are menus or promos
        if (title.Duration.TotalMinutes < 3) return true;

        // Titles far shorter than the main feature
        if (mainFeatureDurationMinutes.HasValue)
        {
            var ratio = title.Duration.TotalMinutes / (double)mainFeatureDurationMinutes.Value;
            if (ratio < 0.40) return true;
        }

        return false;
    }

    public TitleInfo? GetMainFeature(List<TitleInfo> titles)
    {
        if (!titles.Any()) return null;

        var candidates = titles
            .Where(t => t.Duration.TotalMinutes >= MinMovieDurationMinutes && !IsLikelyExtra(t))
            .ToList();

        if (!candidates.Any())
            candidates = titles.Where(t => t.Duration.TotalMinutes >= MinMovieDurationMinutes).ToList();

        if (!candidates.Any())
            return titles.OrderByDescending(t => t.Duration).First();

        // Score: duration weight + chapter count + file size
        return candidates
            .Select(t => new
            {
                Title  = t,
                Score  = t.Duration.TotalMinutes * 2.0 +
                         t.ChapterCount            * 5.0 +
                         t.SizeBytes / 1_073_741_824.0  * 10.0
            })
            .OrderByDescending(x => x.Score)
            .First()
            .Title;
    }
}
