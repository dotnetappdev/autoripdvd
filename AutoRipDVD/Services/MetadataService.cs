using AutoRipDVD.Models;
using AutoRipDVD.MetadataSources;
using AutoRipDVD.MetadataSources.Models;
using AutoRipDVD.MetadataSources.Sources;

namespace AutoRipDVD.Services;

/// <summary>
/// FileBot-style metadata service. Priority: OMDb → TMDB → TVDB.
/// Auto-matches disc volume labels to movies or TV shows intelligently.
/// </summary>
public interface IMetadataService
{
    Task<MediaMetadata?> SearchMovieAsync(string title, int? year = null);
    Task<MediaMetadata?> SearchTVShowAsync(string seriesName, int season, int episode);
    Task<MediaMetadata?> GetByImdbIdAsync(string imdbId);
    Task<List<MediaMetadata>> SearchAsync(string query);
    Task<MediaType> DetermineMediaTypeAsync(string title);
    Task<MediaMetadata?> AutoMatchAsync(string input);
}

public class MetadataService : IMetadataService
{
    private readonly ISettingsService _settings;
    private readonly TitleParser _titleParser;
    private readonly MetadataSourceFactory _sourceFactory;
    private readonly HttpClient _httpClient;

    public MetadataService(ISettingsService settings)
    {
        _settings = settings;
        _titleParser = new TitleParser();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _sourceFactory = new MetadataSourceFactory(_httpClient);
    }

    // ── Source configuration ──────────────────────────────────────────────────

    private void ConfigureSources()
    {
        var s = _settings.Settings;
        if (!string.IsNullOrEmpty(s.OmdbApiKey))
            _sourceFactory.ConfigureSource("OMDb", s.OmdbApiKey);
        if (!string.IsNullOrEmpty(s.TmdbApiKey))
            _sourceFactory.ConfigureSource("TMDB", s.TmdbApiKey);
        if (!string.IsNullOrEmpty(s.TvdbApiKey))
            _sourceFactory.ConfigureSource("TVDB", s.TvdbApiKey);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>FileBot-style auto-match: parse disc label and find best match across all sources.</summary>
    public async Task<MediaMetadata?> AutoMatchAsync(string input)
    {
        ConfigureSources();
        var parsed = _titleParser.Parse(input);

        if (parsed.IsTVShow && parsed.Season.HasValue && parsed.Episode.HasValue)
            return await SearchTVShowAsync(parsed.SeriesName, parsed.Season.Value, parsed.Episode.Value);

        if (parsed.IsTVShow && parsed.Season.HasValue)
        {
            var results = await SearchAsync(parsed.SeriesName);
            var tv = results.FirstOrDefault(r => r.Type == MediaType.TVShow);
            if (tv != null) { tv.SeasonNumber = parsed.Season; return tv; }
        }

        var title = !string.IsNullOrEmpty(parsed.Title) ? parsed.Title : input;
        return await SearchMovieAsync(title, parsed.Year);
    }

    public async Task<MediaMetadata?> SearchMovieAsync(string title, int? year = null)
    {
        ConfigureSources();

        // 1. Try OMDb first (fastest, exact title match)
        var omdb = _sourceFactory.GetSource("OMDb");
        if (omdb != null)
        {
            var result = await SearchOmdbMovieAsync(omdb, title, year);
            if (result != null) return result;
        }

        // 2. Fall back to TMDB
        var tmdb = _sourceFactory.GetSource("TMDB");
        if (tmdb != null)
        {
            var results = await tmdb.SearchAsync(title, "movie");
            var best = PickBestResult(results, year);
            if (best != null)
            {
                var details = await tmdb.GetMovieDetailsAsync(best.Id);
                if (details != null) return MapMovieDetails(details);
            }
        }

        return null;
    }

    public async Task<MediaMetadata?> SearchTVShowAsync(string seriesName, int season, int episode)
    {
        ConfigureSources();

        // 1. OMDb episode lookup
        var omdb = _sourceFactory.GetSource("OMDb");
        if (omdb != null)
        {
            var result = await SearchOmdbEpisodeAsync(omdb, seriesName, season, episode);
            if (result != null) return result;
        }

        // 2. TMDB TV
        var tmdb = _sourceFactory.GetSource("TMDB");
        if (tmdb != null)
        {
            var results = await tmdb.SearchAsync(seriesName, "tv");
            var best = results.FirstOrDefault();
            if (best != null)
            {
                var details = await tmdb.GetTVShowDetailsAsync(best.Id);
                if (details != null)
                {
                    var meta = MapTVShowDetails(details);
                    meta.SeasonNumber = season;
                    meta.EpisodeNumber = episode;
                    return meta;
                }
            }
        }

        return null;
    }

    public async Task<MediaMetadata?> GetByImdbIdAsync(string imdbId)
    {
        ConfigureSources();
        var omdb = _sourceFactory.GetSource("OMDb");
        if (omdb == null) return null;

        var movie = await omdb.GetMovieDetailsAsync(imdbId);
        if (movie != null) return MapMovieDetails(movie);

        var tv = await omdb.GetTVShowDetailsAsync(imdbId);
        if (tv != null) return MapTVShowDetails(tv);

        return null;
    }

    public async Task<List<MediaMetadata>> SearchAsync(string query)
    {
        ConfigureSources();
        var results = new List<MediaMetadata>();

        // OMDb first
        var omdb = _sourceFactory.GetSource("OMDb");
        if (omdb != null)
        {
            var searchResults = await omdb.SearchAsync(query);
            foreach (var r in searchResults)
                results.Add(MapSearchResult(r));
        }

        // TMDB if no OMDb results
        if (results.Count == 0)
        {
            var tmdb = _sourceFactory.GetSource("TMDB");
            if (tmdb != null)
            {
                var searchResults = await tmdb.SearchAsync(query);
                foreach (var r in searchResults)
                    results.Add(MapSearchResult(r));
            }
        }

        return results;
    }

    public async Task<MediaType> DetermineMediaTypeAsync(string title)
    {
        var parsed = _titleParser.Parse(title);
        if (parsed.IsTVShow) return MediaType.TVShow;

        var results = await SearchAsync(parsed.Title ?? title);
        return results.FirstOrDefault()?.Type ?? MediaType.Movie;
    }

    // ── OMDb helpers ──────────────────────────────────────────────────────────

    private static async Task<MediaMetadata?> SearchOmdbMovieAsync(IMetadataSource omdb, string title, int? year)
    {
        var searchResults = await omdb.SearchAsync(title, "movie");
        var best = PickBestResult(searchResults, year);
        if (best == null) return null;

        var details = await omdb.GetMovieDetailsAsync(best.Id);
        return details != null ? MapMovieDetails(details) : null;
    }

    private static async Task<MediaMetadata?> SearchOmdbEpisodeAsync(
        IMetadataSource omdb, string seriesName, int season, int episode)
    {
        var searchResults = await omdb.SearchAsync(seriesName, "tv");
        var series = searchResults.FirstOrDefault();
        if (series == null) return null;

        var details = await omdb.GetTVShowDetailsAsync(series.Id);
        if (details == null) return null;

        var meta = MapTVShowDetails(details);
        meta.SeasonNumber = season;
        meta.EpisodeNumber = episode;
        return meta;
    }

    // ── Scoring / selection ───────────────────────────────────────────────────

    private static SearchResult? PickBestResult(List<SearchResult> results, int? year)
    {
        if (!results.Any()) return null;

        if (year.HasValue)
        {
            var exact = results.FirstOrDefault(r => r.Year == year);
            if (exact != null) return exact;

            var close = results.FirstOrDefault(r => r.Year.HasValue && Math.Abs(r.Year.Value - year.Value) <= 1);
            if (close != null) return close;
        }

        // Prefer by popularity (TMDB) or just first result
        return results.OrderByDescending(r => r.Popularity).ThenByDescending(r => r.Rating).First();
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static MediaMetadata MapMovieDetails(MovieDetails d) => new()
    {
        Type = MediaType.Movie,
        Title = d.Title,
        Year = d.Year,
        ImdbId = d.ImdbId,
        Plot = d.Overview,
        Genre = string.Join(", ", d.Genres),
        Director = d.Director,
        Actors = string.Join(", ", d.Cast.Take(5)),
        PosterUrl = d.PosterUrl,
        Rating = d.Rating > 0 ? d.Rating : null
    };

    private static MediaMetadata MapTVShowDetails(TVShowDetails d) => new()
    {
        Type = MediaType.TVShow,
        Title = d.Name,
        SeriesName = d.Name,
        Year = d.FirstAirYear,
        Plot = d.Overview,
        Genre = string.Join(", ", d.Genres),
        PosterUrl = d.PosterUrl,
        Rating = d.Rating > 0 ? d.Rating : null
    };

    private static MediaMetadata MapSearchResult(SearchResult r)
    {
        var type = r.MediaType == "tv" ? MediaType.TVShow : MediaType.Movie;
        return new MediaMetadata
        {
            Type = type,
            Title = r.Title,
            SeriesName = type == MediaType.TVShow ? r.Title : string.Empty,
            Year = r.Year,
            ImdbId = r.Id,
            PosterUrl = r.PosterUrl,
            Plot = r.Overview,
            Rating = r.Rating > 0 ? r.Rating : null
        };
    }
}
