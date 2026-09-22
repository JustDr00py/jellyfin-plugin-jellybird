using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>Response body from jellybird's GET /api/library/check.</summary>
public class ExistsResponse
{
    [JsonPropertyName("exists")]
    public bool Exists { get; set; }
}
