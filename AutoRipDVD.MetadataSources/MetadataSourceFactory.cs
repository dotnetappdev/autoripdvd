using AutoRipDVD.MetadataSources.Sources;

namespace AutoRipDVD.MetadataSources;

public class MetadataSourceFactory
{
    private readonly Dictionary<string, IMetadataSource> _sources = new();
    private readonly HttpClient _httpClient;

    public MetadataSourceFactory(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        InitializeSources();
    }

    private void InitializeSources()
    {
        _sources["TMDB"] = new TMDBSource(_httpClient);
        _sources["OMDb"] = new OMDbSource(_httpClient);
        _sources["TVDB"] = new TVDBSource(_httpClient);
        _sources["AniDB"] = new AniDBSource(_httpClient);
    }

    public IMetadataSource? GetSource(string sourceName)
    {
        return _sources.TryGetValue(sourceName, out var source) ? source : null;
    }

    public List<IMetadataSource> GetAllSources()
    {
        return _sources.Values.ToList();
    }

    public List<string> GetSourceNames()
    {
        return _sources.Keys.ToList();
    }

    public void ConfigureSource(string sourceName, string apiKey)
    {
        var source = GetSource(sourceName);
        if (source == null) return;

        switch (source)
        {
            case TMDBSource tmdb:
                tmdb.SetApiKey(apiKey);
                break;
            case OMDbSource omdb:
                omdb.SetApiKey(apiKey);
                break;
            case TVDBSource tvdb:
                tvdb.SetApiKey(apiKey);
                break;
        }
    }
}
