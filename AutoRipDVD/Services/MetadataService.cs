using AutoRipDVD.Models;
using Newtonsoft.Json.Linq;

namespace AutoRipDVD.Services;

/// <summary>
/// FileBot-style metadata service with intelligent matching, multiple sources, and fuzzy search
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
    private readonly HttpClient _httpClient;
    private readonly TitleParser _titleParser;
    private const string OmdbBaseUrl = "http://www.omdbapi.com/";

    public MetadataService(ISettingsService settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
        _titleParser = new TitleParser();
    }

    /// <summary>
    /// FileBot-style auto-matching: parse input and automatically find best match
    /// </summary>
    public async Task<MediaMetadata?> AutoMatchAsync(string input)
    {
        // Parse the input using FileBot-style parsing
        var parsed = _titleParser.Parse(input);

        if (parsed.IsTVShow && parsed.Season.HasValue && parsed.Episode.HasValue)
        {
            // It's a TV show with season/episode
            return await SearchTVShowAsync(parsed.SeriesName, parsed.Season.Value, parsed.Episode.Value);
        }
        else if (parsed.IsTVShow && parsed.Season.HasValue)
        {
            // It's a TV season (no specific episode)
            var results = await SearchAsync(parsed.SeriesName);
            var tvResult = results.FirstOrDefault(r => r.Type == MediaType.TVShow);
            
            if (tvResult != null)
            {
                tvResult.SeasonNumber = parsed.Season;
                return tvResult;
            }
        }
        else
        {
            // It's a movie
            var title = !string.IsNullOrEmpty(parsed.Title) ? parsed.Title : input;
            return await SearchMovieAsync(title, parsed.Year);
        }

        return null;
    }

    public async Task<MediaMetadata?> SearchMovieAsync(string title, int? year = null)
    {
        if (string.IsNullOrEmpty(_settings.Settings.OmdbApiKey))
            return null;

        try
        {
            // First, try exact match with year
            var result = await SearchOmdbMovieAsync(title, year);
            if (result != null)
                return result;

            // If no match and year was provided, try without year
            if (year.HasValue)
            {
                result = await SearchOmdbMovieAsync(title, null);
                if (result != null)
                    return result;
            }

            // Try fuzzy search
            return await FuzzySearchMovieAsync(title, year);
        }
        catch (Exception)
        {
            // Log error
        }

        return null;
    }

    private async Task<MediaMetadata?> SearchOmdbMovieAsync(string title, int? year)
    {
        var queryParams = $"?apikey={_settings.Settings.OmdbApiKey}&t={Uri.EscapeDataString(title)}&type=movie";
        if (year.HasValue)
            queryParams += $"&y={year}";

        var response = await _httpClient.GetStringAsync(OmdbBaseUrl + queryParams);
        var json = JObject.Parse(response);

        if (json["Response"]?.ToString() == "True")
        {
            return new MediaMetadata
            {
                Type = MediaType.Movie,
                Title = json["Title"]?.ToString() ?? title,
                Year = int.TryParse(json["Year"]?.ToString(), out var y) ? y : null,
                ImdbId = json["imdbID"]?.ToString() ?? string.Empty,
                Plot = json["Plot"]?.ToString() ?? string.Empty,
                Genre = json["Genre"]?.ToString() ?? string.Empty,
                Director = json["Director"]?.ToString() ?? string.Empty,
                Actors = json["Actors"]?.ToString() ?? string.Empty,
                PosterUrl = json["Poster"]?.ToString() ?? string.Empty,
                Rating = double.TryParse(json["imdbRating"]?.ToString(), out var r) ? r : null
            };
        }

        return null;
    }

    private async Task<MediaMetadata?> FuzzySearchMovieAsync(string title, int? year)
    {
        // Search for multiple results and pick the best match
        var results = await SearchAsync(title);
        
        if (results.Any())
        {
            // Filter by type
            var movies = results.Where(r => r.Type == MediaType.Movie).ToList();
            
            if (year.HasValue)
            {
                // Try to find match with same year
                var exactYearMatch = movies.FirstOrDefault(m => m.Year == year);
                if (exactYearMatch != null)
                    return exactYearMatch;
                
                // Try to find match within 1 year
                var closeYearMatch = movies.FirstOrDefault(m => 
                    m.Year.HasValue && Math.Abs(m.Year.Value - year.Value) <= 1);
                if (closeYearMatch != null)
                    return closeYearMatch;
            }
            
            // Return first movie result
            if (movies.Any())
            {
                // Get full details for the best match
                return await GetByImdbIdAsync(movies.First().ImdbId);
            }
        }

        return null;
    }

    public async Task<MediaMetadata?> SearchTVShowAsync(string seriesName, int season, int episode)
    {
        if (string.IsNullOrEmpty(_settings.Settings.OmdbApiKey))
            return null;

        try
        {
            // First get the series to ensure it exists
            var seriesResults = await SearchAsync(seriesName);
            var series = seriesResults.FirstOrDefault(r => r.Type == MediaType.TVShow);
            
            if (series == null)
                return null;

            // Now get specific episode
            var queryParams = $"?apikey={_settings.Settings.OmdbApiKey}&t={Uri.EscapeDataString(seriesName)}&Season={season}&Episode={episode}&type=series";
            var response = await _httpClient.GetStringAsync(OmdbBaseUrl + queryParams);
            var json = JObject.Parse(response);

            if (json["Response"]?.ToString() == "True")
            {
                return new MediaMetadata
                {
                    Type = MediaType.TVShow,
                    SeriesName = json["Series"]?.ToString() ?? seriesName,
                    EpisodeTitle = json["Title"]?.ToString() ?? string.Empty,
                    SeasonNumber = season,
                    EpisodeNumber= episode,
                    Year = int.TryParse(json["Year"]?.ToString(), out var y) ? y : null,
                    ImdbId = json["imdbID"]?.ToString() ?? string.Empty,
                    Plot = json["Plot"]?.ToString() ?? string.Empty,
                    Genre = json["Genre"]?.ToString() ?? string.Empty,
                    Director = json["Director"]?.ToString() ?? string.Empty,
                    Actors = json["Actors"]?.ToString() ?? string.Empty,
                    Rating = double.TryParse(json["imdbRating"]?.ToString(), out var r) ? r : null
                };
            }
        }
        catch (Exception)
        {
            // Log error
        }

        return null;
    }

    public async Task<MediaMetadata?> GetByImdbIdAsync(string imdbId)
    {
        if (string.IsNullOrEmpty(_settings.Settings.OmdbApiKey))
            return null;

        try
        {
            var queryParams = $"?apikey={_settings.Settings.OmdbApiKey}&i={imdbId}";
            var response = await _httpClient.GetStringAsync(OmdbBaseUrl + queryParams);
            var json = JObject.Parse(response);

            if (json["Response"]?.ToString() == "True")
            {
                var type = json["Type"]?.ToString()?.ToLower() == "movie" ? MediaType.Movie : MediaType.TVShow;
                
                return new MediaMetadata
                {
                    Type = type,
                    Title = json["Title"]?.ToString() ?? string.Empty,
                    Year = int.TryParse(json["Year"]?.ToString()?.Substring(0, 4), out var y) ? y : null,
                    ImdbId = imdbId,
                    Plot = json["Plot"]?.ToString() ?? string.Empty,
                    Genre = json["Genre"]?.ToString() ?? string.Empty,
                    Director = json["Director"]?.ToString() ?? string.Empty,
                    Actors = json["Actors"]?.ToString() ?? string.Empty,
                    PosterUrl = json["Poster"]?.ToString() ?? string.Empty,
                    Rating = double.TryParse(json["imdbRating"]?.ToString(), out var r) ? r : null
                };
            }
        }
        catch (Exception)
        {
            // Log error
        }

        return null;
    }

    public async Task<List<MediaMetadata>> SearchAsync(string query)
    {
        var results = new List<MediaMetadata>();
        
        if (string.IsNullOrEmpty(_settings.Settings.OmdbApiKey))
            return results;

        try
        {
            var queryParams = $"?apikey={_settings.Settings.OmdbApiKey}&s={Uri.EscapeDataString(query)}";
            var response = await _httpClient.GetStringAsync(OmdbBaseUrl + queryParams);
            var json = JObject.Parse(response);

            if (json["Response"]?.ToString() == "True" && json["Search"] is JArray searchResults)
            {
                foreach (var item in searchResults)
                {
                    var type = item["Type"]?.ToString()?.ToLower() == "movie" ? MediaType.Movie : MediaType.TVShow;
                    
                    var metadata = new MediaMetadata
                    {
                        Type = type,
                        Title = item["Title"]?.ToString() ?? string.Empty,
                        Year = int.TryParse(item["Year"]?.ToString()?.Substring(0, 4), out var y) ? y : null,
                        ImdbId = item["imdbID"]?.ToString() ?? string.Empty,
                        PosterUrl = item["Poster"]?.ToString() ?? string.Empty
                    };

                    if (type == MediaType.TVShow)
                        metadata.SeriesName = metadata.Title;

                    results.Add(metadata);
                }
            }
        }
        catch (Exception)
        {
            // Log error
        }

        return results;
    }

    public async Task<MediaType> DetermineMediaTypeAsync(string title)
    {
        // Use the parser to intelligently detect type
        var parsed = _titleParser.Parse(title);
        
        if (parsed.IsTVShow)
            return MediaType.TVShow;

        // Search OMDb to confirm
        var results = await SearchAsync(parsed.Title ?? title);
        if (results.Any())
        {
            return results[0].Type;
        }

        // Default to movie
        return MediaType.Movie;
    }
}
