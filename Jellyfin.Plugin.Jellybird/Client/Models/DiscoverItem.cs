using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>One title from jellybird's GET /api/discover, labelled with what the library already has.</summary>
public class DiscoverItem : SearchResult
{
    [JsonPropertyName("vote_average")]
    public double VoteAverage { get; set; }

    [JsonPropertyName("in_library")]
    public bool InLibrary { get; set; }

    /// <summary>How many episode files a show has in the library (TV only; 0 when unknown).</summary>
    [JsonPropertyName("episodes")]
    public int Episodes { get; set; }
}
