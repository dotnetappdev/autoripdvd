using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoRipDVD.MetadataSources.Models;

namespace AutoRipDVD.MetadataSources.Sources;

public class TMDBSource : IMetadataSource
{
    private readonly HttpClient _httpClient;
    private string _apiKey = string.Empty;
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500";

    public string Name => "TheMovieDB";
    public bool RequiresApiKey => true;

    public TMDBSource(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public void SetApiKey(string apiKey)
    {
        _apiKey = apiKey;
    }

    public async Task<List<SearchResult>> SearchAsync(string query, string? mediaType = null)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return new List<SearchResult>();

        var endpoint = mediaType?.ToLower() switch
        {
            "movie" => "/search/movie",
            "tv" => "/search/tv",
            _ => "/search/multi"
        };

        var url = $"{BaseUrl}{endpoint}?api_key={_apiKey}&query={Uri.EscapeDataString(query)}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<TmdbSearchResponse>(response, options);

            return result?.Results?
                .Where(r => r.MediaType == "movie" || r.MediaType == "tv")
                .Select(r => new SearchResult
                {
                    Source = Name,
                    Id = r.Id.ToString(),
                    Title = r.MediaType == "tv" ? r.Name ?? "" : r.Title ?? "",
                    OriginalTitle = r.MediaType == "tv" ? r.OriginalName ?? "" : r.OriginalTitle ?? "",
                    Year = ParseYear(r.MediaType == "tv" ? r.FirstAirDate : r.ReleaseDate),
                    Overview = r.Overview ?? "",
                    PosterUrl = string.IsNullOrEmpty(r.PosterPath) ? "" : ImageBaseUrl + r.PosterPath,
                    MediaType = r.MediaType ?? "movie",
                    Rating = r.VoteAverage,
                    Popularity = r.Popularity
                }).ToList() ?? new List<SearchResult>();
        }
        catch
        {
            return new List<SearchResult>();
        }
    }

    public async Task<MovieDetails?> GetMovieDetailsAsync(string id)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var url = $"{BaseUrl}/movie/{id}?api_key={_apiKey}&append_to_response=credits";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var movie = JsonSerializer.Deserialize<TmdbMovieResponse>(response, options);

            if (movie == null) return null;

            return new MovieDetails
            {
                Source = Name,
                Id = movie.Id.ToString(),
                Title = movie.Title ?? "",
                OriginalTitle = movie.OriginalTitle ?? "",
                Year = ParseYear(movie.ReleaseDate),
                Overview = movie.Overview ?? "",
                PosterUrl = string.IsNullOrEmpty(movie.PosterPath) ? "" : ImageBaseUrl + movie.PosterPath,
                BackdropUrl = string.IsNullOrEmpty(movie.BackdropPath) ? "" : "https://image.tmdb.org/t/p/original" + movie.BackdropPath,
                ImdbId = movie.ImdbId ?? "",
                Genres = movie.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                Runtime = movie.Runtime,
                Rating = movie.VoteAverage,
                Tagline = movie.Tagline ?? "",
                Director = movie.Credits?.Crew?.FirstOrDefault(c => c.Job == "Director")?.Name ?? "",
                Cast = movie.Credits?.Cast?.Take(10).Select(c => c.Name).ToList() ?? new List<string>()
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<TVShowDetails?> GetTVShowDetailsAsync(string id)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var url = $"{BaseUrl}/tv/{id}?api_key={_apiKey}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var show = JsonSerializer.Deserialize<TmdbTVResponse>(response, options);

            if (show == null) return null;

            return new TVShowDetails
            {
                Source = Name,
                Id = show.Id.ToString(),
                Name = show.Name ?? "",
                OriginalName = show.OriginalName ?? "",
                FirstAirYear = ParseYear(show.FirstAirDate),
                Overview = show.Overview ?? "",
                PosterUrl = string.IsNullOrEmpty(show.PosterPath) ? "" : ImageBaseUrl + show.PosterPath,
                BackdropUrl = string.IsNullOrEmpty(show.BackdropPath) ? "" : "https://image.tmdb.org/t/p/original" + show.BackdropPath,
                Genres = show.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                NumberOfSeasons = show.NumberOfSeasons,
                NumberOfEpisodes = show.NumberOfEpisodes,
                Rating = show.VoteAverage,
                Status = show.Status ?? "",
                Network = show.Networks?.FirstOrDefault()?.Name ?? ""
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

        return int.TryParse(date[..4], out var year) ? year : null;
    }

    // JSON response classes
    private class TmdbSearchResponse
    {
        public List<TmdbSearchResult>? Results { get; set; }
    }

    private class TmdbSearchResult
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("original_title")]
        public string? OriginalTitle { get; set; }
        [JsonPropertyName("original_name")]
        public string? OriginalName { get; set; }
        public string? Overview { get; set; }
        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }
        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }
        [JsonPropertyName("first_air_date")]
        public string? FirstAirDate { get; set; }
        [JsonPropertyName("media_type")]
        public string? MediaType { get; set; }
        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }
        public double Popularity { get; set; }
    }

    private class TmdbMovieResponse
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        [JsonPropertyName("original_title")]
        public string? OriginalTitle { get; set; }
        public string? Overview { get; set; }
        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }
        [JsonPropertyName("backdrop_path")]
        public string? BackdropPath { get; set; }
        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }
        [JsonPropertyName("imdb_id")]
        public string? ImdbId { get; set; }
        public int? Runtime { get; set; }
        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }
        public string? Tagline { get; set; }
        public List<Genre>? Genres { get; set; }
        public Credits? Credits { get; set; }
    }

    private class TmdbTVResponse
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("original_name")]
        public string? OriginalName { get; set; }
        public string? Overview { get; set; }
        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }
        [JsonPropertyName("backdrop_path")]
        public string? BackdropPath { get; set; }
        [JsonPropertyName("first_air_date")]
        public string? FirstAirDate { get; set; }
        [JsonPropertyName("number_of_seasons")]
        public int NumberOfSeasons { get; set; }
        [JsonPropertyName("number_of_episodes")]
        public int NumberOfEpisodes { get; set; }
        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }
        public string? Status { get; set; }
        public List<Genre>? Genres { get; set; }
        public List<Network>? Networks { get; set; }
    }

    private class Genre
    {
        public string Name { get; set; } = "";
    }

    private class Network
    {
        public string Name { get; set; } = "";
    }

    private class Credits
    {
        public List<Cast>? Cast { get; set; }
        public List<Crew>? Crew { get; set; }
    }

    private class Cast
    {
        public string Name { get; set; } = "";
    }

    private class Crew
    {
        public string Name { get; set; } = "";
        public string Job { get; set; } = "";
    }
}
