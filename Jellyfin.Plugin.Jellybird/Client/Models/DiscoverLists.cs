using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>The lists and genres jellybird's GET /api/discover/lists offers for one media type.</summary>
public class DiscoverLists
{
    [JsonPropertyName("lists")]
    public List<DiscoverList> Lists { get; set; } = [];

    [JsonPropertyName("genres")]
    public List<Genre> Genres { get; set; } = [];
}

/// <summary>A named TMDB list, e.g. "trending" / "Trending".</summary>
public class DiscoverList
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

/// <summary>A TMDB genre.</summary>
public class Genre
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
