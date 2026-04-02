using System.Net.Http;
using System.Text.Json;
using AutoRipDVD.Models;

namespace AutoRipDVD.Services;

public interface ITmdbService
{
    Task<List<TmdbSearchResult>> SearchMovieAsync(string query, int? year = null);
    Task<List<TmdbSearchResult>> SearchTVShowAsync(string query);
    Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbId);
    Task<TmdbTVShowDetails?> GetTVShowDetailsAsync(int tmdbId);
    Task<List<TmdbSearchResult>> SearchMultiAsync(string query);
}

public class TmdbService : ITmdbService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settings;
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500";

    public TmdbService(ISettingsService settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
    }

    public async Task<List<TmdbSearchResult>> SearchMovieAsync(string query, int? year = null)
    {
        var apiKey = _settings.Settings.TmdbApiKey;
        if (string.IsNullOrEmpty(apiKey))
            return new List<TmdbSearchResult>();

        var yearParam = year.HasValue ? $"&year={year}" : "";
        var url = $"{BaseUrl}/search/movie?api_key={apiKey}&query={Uri.EscapeDataString(query)}{yearParam}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<TmdbSearchResponse>(response);
            
            return result?.Results?.Select(r => new TmdbSearchResult
            {
                Id = r.Id,
                Title = r.Title ?? r.Name ?? "",
                OriginalTitle = r.OriginalTitle ?? r.OriginalName ?? "",
                Year = ParseYear(r.ReleaseDate ?? r.FirstAirDate),
                Overview = r.Overview ?? "",
                PosterPath = string.IsNullOrEmpty(r.PosterPath) ? "" : ImageBaseUrl + r.PosterPath,
                MediaType = r.MediaType ?? "movie",
                VoteAverage = r.VoteAverage,
                Popularity = r.Popularity
            }).ToList() ?? new List<TmdbSearchResult>();
        }
        catch
        {
            return new List<TmdbSearchResult>();
        }
    }

    public async Task<List<TmdbSearchResult>> SearchTVShowAsync(string query)
    {
        var apiKey = _settings.Settings.TmdbApiKey;
        if (string.IsNullOrEmpty(apiKey))
            return new List<TmdbSearchResult>();

        var url = $"{BaseUrl}/search/tv?api_key={apiKey}&query={Uri.EscapeDataString(query)}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<TmdbSearchResponse>(response);
            
            return result?.Results?.Select(r => new TmdbSearchResult
            {
                Id = r.Id,
                Title = r.Name ?? r.Title ?? "",
                OriginalTitle = r.OriginalName ?? r.OriginalTitle ?? "",
                Year = ParseYear(r.FirstAirDate ?? r.ReleaseDate),
                Overview = r.Overview ?? "",
                PosterPath = string.IsNullOrEmpty(r.PosterPath) ? "" : ImageBaseUrl + r.PosterPath,
                MediaType = "tv",
                VoteAverage = r.VoteAverage,
                Popularity = r.Popularity
            }).ToList() ?? new List<TmdbSearchResult>();
        }
        catch
        {
            return new List<TmdbSearchResult>();
        }
    }

    public async Task<List<TmdbSearchResult>> SearchMultiAsync(string query)
    {
        var apiKey = _settings.Settings.TmdbApiKey;
        if (string.IsNullOrEmpty(apiKey))
            return new List<TmdbSearchResult>();

        var url = $"{BaseUrl}/search/multi?api_key={apiKey}&query={Uri.EscapeDataString(query)}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<TmdbSearchResponse>(response);
            
            return result?.Results?
                .Where(r => r.MediaType == "movie" || r.MediaType == "tv")
                .Select(r => new TmdbSearchResult
                {
                    Id = r.Id,
                    Title = (r.MediaType == "tv" ? r.Name : r.Title) ?? "",
                    OriginalTitle = (r.MediaType == "tv" ? r.OriginalName : r.OriginalTitle) ?? "",
                    Year = ParseYear(r.MediaType == "tv" ? r.FirstAirDate : r.ReleaseDate),
                    Overview = r.Overview ?? "",
                    PosterPath = string.IsNullOrEmpty(r.PosterPath) ? "" : ImageBaseUrl + r.PosterPath,
                    MediaType = r.MediaType ?? "movie",
                    VoteAverage = r.VoteAverage,
                    Popularity = r.Popularity
                }).ToList() ?? new List<TmdbSearchResult>();
        }
        catch
        {
            return new List<TmdbSearchResult>();
        }
    }

    public async Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbId)
    {
        var apiKey = _settings.Settings.TmdbApiKey;
        if (string.IsNullOrEmpty(apiKey))
            return null;

        var url = $"{BaseUrl}/movie/{tmdbId}?api_key={apiKey}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var movie = JsonSerializer.Deserialize<TmdbMovieDetailsResponse>(response);
            
            if (movie == null) return null;

            return new TmdbMovieDetails
            {
                Id = movie.Id,
                Title = movie.Title ?? "",
                OriginalTitle = movie.OriginalTitle ?? "",
                Year = ParseYear(movie.ReleaseDate),
                Overview = movie.Overview ?? "",
                PosterPath = string.IsNullOrEmpty(movie.PosterPath) ? "" : ImageBaseUrl + movie.PosterPath,
                BackdropPath = string.IsNullOrEmpty(movie.BackdropPath) ? "" : ImageBaseUrl + movie.BackdropPath,
                ImdbId = movie.ImdbId ?? "",
                Genres = movie.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                Runtime = movie.Runtime,
                VoteAverage = movie.VoteAverage,
                Tagline = movie.Tagline ?? ""
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<TmdbTVShowDetails?> GetTVShowDetailsAsync(int tmdbId)
    {
        var apiKey = _settings.Settings.TmdbApiKey;
        if (string.IsNullOrEmpty(apiKey))
            return null;

        var url = $"{BaseUrl}/tv/{tmdbId}?api_key={apiKey}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var show = JsonSerializer.Deserialize<TmdbTVShowDetailsResponse>(response);
            
            if (show == null) return null;

            return new TmdbTVShowDetails
            {
                Id = show.Id,
                Name = show.Name ?? "",
                OriginalName = show.OriginalName ?? "",
                FirstAirYear = ParseYear(show.FirstAirDate),
                Overview = show.Overview ?? "",
                PosterPath = string.IsNullOrEmpty(show.PosterPath) ? "" : ImageBaseUrl + show.PosterPath,
                BackdropPath = string.IsNullOrEmpty(show.BackdropPath) ? "" : ImageBaseUrl + show.BackdropPath,
                Genres = show.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                NumberOfSeasons = show.NumberOfSeasons,
                NumberOfEpisodes = show.NumberOfEpisodes,
                VoteAverage = show.VoteAverage
            };
        }
        catch
        {
            return null;
        }
    }

    private static int? ParseYear(string? date)
    {
        if (string.IsNullOrEmpty(date) || date.Length < 4)
            return null;
        
        return int.TryParse(date.Substring(0, 4), out var year) ? year : null;
    }

    // JSON response classes
    private class TmdbSearchResponse
    {
        public List<TmdbResult>? Results { get; set; }
    }

    private class TmdbResult
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Name { get; set; }
        public string? OriginalTitle { get; set; }
        public string? OriginalName { get; set; }
        public string? Overview { get; set; }
        public string? PosterPath { get; set; }
        public string? ReleaseDate { get; set; }
        public string? FirstAirDate { get; set; }
        public string? MediaType { get; set; }
        public double VoteAverage { get; set; }
        public double Popularity { get; set; }
    }

    private class TmdbMovieDetailsResponse
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? OriginalTitle { get; set; }
        public string? Overview { get; set; }
        public string? PosterPath { get; set; }
        public string? BackdropPath { get; set; }
        public string? ReleaseDate { get; set; }
        public string? ImdbId { get; set; }
        public int? Runtime { get; set; }
        public double VoteAverage { get; set; }
        public string? Tagline { get; set; }
        public List<Genre>? Genres { get; set; }
    }

    private class TmdbTVShowDetailsResponse
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? OriginalName { get; set; }
        public string? Overview { get; set; }
        public string? PosterPath { get; set; }
        public string? BackdropPath { get; set; }
        public string? FirstAirDate { get; set; }
        public int NumberOfSeasons { get; set; }
        public int NumberOfEpisodes { get; set; }
        public double VoteAverage { get; set; }
        public List<Genre>? Genres { get; set; }
    }

    private class Genre
    {
        public string Name { get; set; } = "";
    }
}
