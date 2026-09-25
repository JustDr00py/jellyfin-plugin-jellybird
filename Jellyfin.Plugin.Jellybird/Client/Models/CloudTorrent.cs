using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>One torrent in a debrid cloud, as returned by jellybird's GET /api/cloud.</summary>
public class CloudTorrent
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>jellybird's normalized status, e.g. "ready", "downloading", "error".</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public int Files { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
