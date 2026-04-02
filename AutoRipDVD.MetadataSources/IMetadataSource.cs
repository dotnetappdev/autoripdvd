using AutoRipDVD.MetadataSources.Models;

namespace AutoRipDVD.MetadataSources;

public interface IMetadataSource
{
    string Name { get; }
    bool RequiresApiKey { get; }
    Task<List<SearchResult>> SearchAsync(string query, string? mediaType = null);
    Task<MovieDetails?> GetMovieDetailsAsync(string id);
    Task<TVShowDetails?> GetTVShowDetailsAsync(string id);
}
