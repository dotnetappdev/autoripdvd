using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoRipDVD.MetadataSources.Models;

namespace AutoRipDVD.MetadataSources.Sources;

public class OMDbSource : IMetadataSource
{
    private readonly HttpClient _httpClient;
    private string _apiKey = string.Empty;
    private const string BaseUrl = "http://www.omdbapi.com/";

    public string Name => "OMDb";
    public bool RequiresApiKey => true;

    public OMDbSource(HttpClient? httpClient = null)
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

        var typeParam = mediaType?.ToLower() switch
        {
            "movie" => "&type=movie",
            "tv" => "&type=series",
            _ => ""
        };

        var url = $"{BaseUrl}?apikey={_apiKey}&s={Uri.EscapeDataString(query)}{typeParam}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<OmdbSearchResponse>(response, options);

            if (result?.Response != "True" || result.Search == null)
                return new List<SearchResult>();

            return result.Search.Select(r => new SearchResult
            {
                Source = Name,
                Id = r.ImdbID ?? "",
                Title = r.Title ?? "",
                Year = int.TryParse(r.Year, out var year) ? year : null,
                PosterUrl = r.Poster != "N/A" ? r.Poster ?? "" : "",
                MediaType = r.Type == "series" ? "tv" : "movie",
                Rating = 0
            }).ToList();
        }
        catch
        {
            return new List<SearchResult>();
        }
    }

    public async Task<MovieDetails?> GetMovieDetailsAsync(string imdbId)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var url = $"{BaseUrl}?apikey={_apiKey}&i={imdbId}&plot=full";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var movie = JsonSerializer.Deserialize<OmdbDetailsResponse>(response, options);

            if (movie?.Response != "True")
                return null;

            return new MovieDetails
            {
                Source = Name,
                Id = movie.ImdbID ?? "",
                Title = movie.Title ?? "",
                Year = int.TryParse(movie.Year, out var year) ? year : null,
                Overview = movie.Plot ?? "",
                PosterUrl = movie.Poster != "N/A" ? movie.Poster ?? "" : "",
                ImdbId = movie.ImdbID ?? "",
                Genres = movie.Genre?.Split(',').Select(g => g.Trim()).ToList() ?? new List<string>(),
                Runtime = ParseRuntime(movie.Runtime),
                Rating = double.TryParse(movie.ImdbRating, out var rating) ? rating : 0,
                Director = movie.Director ?? "",
                Cast = movie.Actors?.Split(',').Select(a => a.Trim()).ToList() ?? new List<string>()
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<TVShowDetails?> GetTVShowDetailsAsync(string imdbId)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var url = $"{BaseUrl}?apikey={_apiKey}&i={imdbId}&plot=full";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var show = JsonSerializer.Deserialize<OmdbDetailsResponse>(response, options);

            if (show?.Response != "True" || show.Type != "series")
                return null;

            return new TVShowDetails
            {
                Source = Name,
                Id = show.ImdbID ?? "",
                Name = show.Title ?? "",
                FirstAirYear = int.TryParse(show.Year?.Split('–')[0], out var year) ? year : null,
                Overview = show.Plot ?? "",
                PosterUrl = show.Poster != "N/A" ? show.Poster ?? "" : "",
                Genres = show.Genre?.Split(',').Select(g => g.Trim()).ToList() ?? new List<string>(),
                NumberOfSeasons = int.TryParse(show.TotalSeasons, out var seasons) ? seasons : 0,
                Rating = double.TryParse(show.ImdbRating, out var rating) ? rating : 0
            };
        }
        catch
        {
            return null;
        }
    }

    private static int? ParseRuntime(string? runtime)
    {
        if (string.IsNullOrEmpty(runtime))
            return null;

        var numbers = new string(runtime.Where(char.IsDigit).ToArray());
        return int.TryParse(numbers, out var minutes) ? minutes : null;
    }

    private class OmdbSearchResponse
    {
        public string? Response { get; set; }
        public List<OmdbSearchResult>? Search { get; set; }
    }

    private class OmdbSearchResult
    {
        public string? Title { get; set; }
        public string? Year { get; set; }
        [JsonPropertyName("imdbID")]
        public string? ImdbID { get; set; }
        public string? Type { get; set; }
        public string? Poster { get; set; }
    }

    private class OmdbDetailsResponse
    {
        public string? Response { get; set; }
        public string? Title { get; set; }
        public string? Year { get; set; }
        [JsonPropertyName("imdbID")]
        public string? ImdbID { get; set; }
        public string? Type { get; set; }
        public string? Plot { get; set; }
        public string? Poster { get; set; }
        public string? Genre { get; set; }
        public string? Director { get; set; }
        public string? Actors { get; set; }
        public string? Runtime { get; set; }
        [JsonPropertyName("imdbRating")]
        public string? ImdbRating { get; set; }
        public string? TotalSeasons { get; set; }
    }
}
