using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>Response body from jellybird's POST /api/add.</summary>
public class AddTorrentResponse
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("torrent_id")]
    public string TorrentId { get; set; } = string.Empty;

    /// <summary>True if the torrent was already cached on the debrid provider.</summary>
    [JsonPropertyName("cached")]
    public bool Cached { get; set; }

    /// <summary>True if jellybird triggered an immediate sync because the content was cached.</summary>
    [JsonPropertyName("synced")]
    public bool Synced { get; set; }
}
