namespace AutoRipDVD.Models;

public class TmdbSearchResult
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalTitle { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Overview { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public double VoteAverage { get; set; }
    public double Popularity { get; set; }
}

public class TmdbMovieDetails
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalTitle { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Overview { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    public string BackdropPath { get; set; } = string.Empty;
    public string ImdbId { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public int? Runtime { get; set; }
    public double VoteAverage { get; set; }
    public string Tagline { get; set; } = string.Empty;
}

public class TmdbTVShowDetails
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public int? FirstAirYear { get; set; }
    public string Overview { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    public string BackdropPath { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public int NumberOfSeasons { get; set; }
    public int NumberOfEpisodes { get; set; }
    public double VoteAverage { get; set; }
}