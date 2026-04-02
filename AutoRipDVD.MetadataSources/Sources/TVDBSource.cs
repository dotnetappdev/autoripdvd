using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoRipDVD.MetadataSources.Models;

namespace AutoRipDVD.MetadataSources.Sources;

public class TVDBSource : IMetadataSource
{
    private readonly HttpClient _httpClient;
    private string _apiKey = string.Empty;
    private string? _token;
    private const string BaseUrl = "https://api4.thetvdb.com/v4";

    public string Name => "TheTVDB";
    public bool RequiresApiKey => true;

    public TVDBSource(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public void SetApiKey(string apiKey)
    {
        _apiKey = apiKey;
    }

    private async Task<bool> AuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_token))
            return true;

        if (string.IsNullOrEmpty(_apiKey))
            return false;

        try
        {
            var loginData = JsonSerializer.Serialize(new { apikey = _apiKey });
            var content = new StringContent(loginData, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/login", content);
            
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(result);
            _token = tokenResponse?.Data?.Token;

            if (!string.IsNullOrEmpty(_token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            }

            return !string.IsNullOrEmpty(_token);
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<SearchResult>> SearchAsync(string query, string? mediaType = null)
    {
        if (!await AuthenticateAsync())
            return new List<SearchResult>();

        var url = $"{BaseUrl}/search?query={Uri.EscapeDataString(query)}&type=series";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<TvdbSearchResponse>(response, options);

            return result?.Data?.Select(r => new SearchResult
            {
                Source = Name,
                Id = r.TvdbId ?? "",
                Title = r.Name ?? "",
                OriginalTitle = r.Name ?? "",
                Year = int.TryParse(r.Year, out var year) ? year : null,
                Overview = r.Overview ?? "",
                PosterUrl = r.ImageUrl ?? "",
                MediaType = "tv",
                Rating = 0
            }).ToList() ?? new List<SearchResult>();
        }
        catch
        {
            return new List<SearchResult>();
        }
    }

    public Task<MovieDetails?> GetMovieDetailsAsync(string id)
    {
        // TVDB is TV shows only
        return Task.FromResult<MovieDetails?>(null);
    }

    public async Task<TVShowDetails?> GetTVShowDetailsAsync(string id)
    {
        if (!await AuthenticateAsync())
            return null;

        var url = $"{BaseUrl}/series/{id}/extended";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<TvdbSeriesResponse>(response, options);

            if (result?.Data == null)
                return null;

            var show = result.Data;

            return new TVShowDetails
            {
                Source = Name,
                Id = show.Id?.ToString() ?? "",
                Name = show.Name ?? "",
                OriginalName = show.Name ?? "",
                FirstAirYear = int.TryParse(show.FirstAired?.Substring(0, 4), out var year) ? year : null,
                Overview = show.Overview ?? "",
                PosterUrl = show.Image ?? "",
                Genres = show.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                NumberOfSeasons = show.Seasons?.Count ?? 0,
                Rating = show.Score ?? 0,
                Status = show.Status?.Name ?? "",
                Network = show.OriginalNetwork?.Name ?? ""
            };
        }
        catch
        {
            return null;
        }
    }

    private class TokenResponse
    {
        public TokenData? Data { get; set; }
    }

    private class TokenData
    {
        public string? Token { get; set; }
    }

    private class TvdbSearchResponse
    {
        public List<TvdbSearchResult>? Data { get; set; }
    }

    private class TvdbSearchResult
    {
        [JsonPropertyName("tvdb_id")]
        public string? TvdbId { get; set; }
        public string? Name { get; set; }
        public string? Overview { get; set; }
        public string? Year { get; set; }
        [JsonPropertyName("image_url")]
        public string? ImageUrl { get; set; }
    }

    private class TvdbSeriesResponse
    {
        public TvdbSeries? Data { get; set; }
    }

    private class TvdbSeries
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Overview { get; set; }
        [JsonPropertyName("first_aired")]
        public string? FirstAired { get; set; }
        public string? Image { get; set; }
        public List<TvdbGenre>? Genres { get; set; }
        public List<TvdbSeason>? Seasons { get; set; }
        public double? Score { get; set; }
        public TvdbStatus? Status { get; set; }
        [JsonPropertyName("original_network")]
        public TvdbNetwork? OriginalNetwork { get; set; }
    }

    private class TvdbGenre
    {
        public string Name { get; set; } = "";
    }

    private class TvdbSeason
    {
        public int Number { get; set; }
    }

    private class TvdbStatus
    {
        public string Name { get; set; } = "";
    }

    private class TvdbNetwork
    {
        public string Name { get; set; } = "";
    }
}
