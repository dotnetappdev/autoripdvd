namespace AutoRipDVD.MetadataSources.Models;

public class SearchResult
{
    public string Source { get; set; } = string.Empty; // "TMDB", "OMDb", "TVDB", etc.
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OriginalTitle { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Overview { get; set; } = string.Empty;
    public string PosterUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty; // "movie", "tv", "anime"
    public double Rating { get; set; }
    public double Popularity { get; set; }
    public string Language { get; set; } = string.Empty;
}

public class MovieDetails
{
    public string Source { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OriginalTitle { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Overview { get; set; } = string.Empty;
    public string PosterUrl { get; set; } = string.Empty;
    public string BackdropUrl { get; set; } = string.Empty;
    public string ImdbId { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public int? Runtime { get; set; }
    public double Rating { get; set; }
    public string Tagline { get; set; } = string.Empty;
    public string Director { get; set; } = string.Empty;
    public List<string> Cast { get; set; } = new();
}

public class TVShowDetails
{
    public string Source { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public int? FirstAirYear { get; set; }
    public string Overview { get; set; } = string.Empty;
    public string PosterUrl { get; set; } = string.Empty;
    public string BackdropUrl { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public int NumberOfSeasons { get; set; }
    public int NumberOfEpisodes { get; set; }
    public double Rating { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
}

public class EpisodeDetails
{
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;
    public DateTime? AirDate { get; set; }
    public string StillPath { get; set; } = string.Empty;
}
