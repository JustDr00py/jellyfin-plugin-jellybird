using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>One candidate torrent, as returned by jellybird's GET /api/torrents (cached-first sorted).</summary>
public class TorrentCandidate
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("seeders")]
    public int Seeders { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Debrid provider this is already cached on ("realdebrid"/"torbox"), or empty if not cached anywhere.</summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("magnet")]
    public string Magnet { get; set; } = string.Empty;

    [JsonPropertyName("cached")]
    public bool Cached { get; set; }
}
