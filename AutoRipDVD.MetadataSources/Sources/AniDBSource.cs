using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoRipDVD.MetadataSources.Models;

namespace AutoRipDVD.MetadataSources.Sources;

public class AniDBSource : IMetadataSource
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://api.anidb.net:9001/httpapi";
    private string _clientName = "autorip";
    private int _clientVersion = 1;

    public string Name => "AniDB";
    public bool RequiresApiKey => false; // Uses client name/version

    public AniDBSource(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public void SetClient(string clientName, int version)
    {
        _clientName = clientName;
        _clientVersion = version;
    }

    public async Task<List<SearchResult>> SearchAsync(string query, string? mediaType = null)
    {
        // AniDB API requires specific search by anime ID
        // For now, return empty - would need to implement anime title search
        return new List<SearchResult>();
    }

    public Task<MovieDetails?> GetMovieDetailsAsync(string id)
    {
        // AniDB is anime-focused, treat as TV show
        return Task.FromResult<MovieDetails?>(null);
    }

    public async Task<TVShowDetails?> GetTVShowDetailsAsync(string animeId)
    {
        var url = $"{BaseUrl}?request=anime&client={_clientName}&clientver={_clientVersion}&protover=1&aid={animeId}";

        try
        {
            // Note: AniDB has strict rate limiting and requires client registration
            // This is a simplified implementation
            var response = await _httpClient.GetStringAsync(url);
            
            // AniDB returns XML, would need proper XML parsing
            // For now, return null - full implementation would parse XML response
            return null;
        }
        catch
        {
            return null;
        }
    }
}
