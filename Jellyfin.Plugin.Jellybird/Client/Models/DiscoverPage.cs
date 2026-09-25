using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>One page of a TMDB list, as returned by jellybird's GET /api/discover.</summary>
public class DiscoverPage
{
    [JsonPropertyName("results")]
    public List<DiscoverItem> Results { get; set; } = [];

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}
